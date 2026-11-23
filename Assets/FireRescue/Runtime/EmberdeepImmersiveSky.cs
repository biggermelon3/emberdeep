using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FireRescue
{
    // Distant spherical environment seen through an angular window (the aperture) around its subjects. The dome
    // follows head translation and never rotates; its content (hills, clouds) is at infinity, in world directions.
    //
    // Headset test 3, "sometimes when I move my head around, the skybox changes": the window was re-aimed and
    // re-sized every frame from the LIVE head and from live renderer bounds that took in the pump, the held tool and
    // whatever moved on the board. The board is ~.6 m away, so a 10 cm head move swung it ~10 deg, and pumping or
    // carrying resized it. Now the window is a world-fixed set of directions:
    // - measured from an ANCHOR: the seat at placement (FireRescueGame.SeatAnchor = placedHead; a recenter re-anchors,
    //   the play-area options keep it), or, with none given (the camp), the eye when the main subject was last placed;
    // - from a STATIC footprint in subject space: the board's plinth (EmberdeepRimHud.MeasurePlinth) + canopy headroom
    //   + both piece-tray zones; the camp sign and set from their renderers minus the pump and the sheep; only the
    //   legacy tool flow's equipment subjects stay live;
    // - covering that footprint from every eye in a seated head-motion ENVELOPE around the anchor (sway, bob, lean),
    //   so a head moving inside it never brings the board's silhouette to the sky's edge;
    // - EASED to a new target (placement, tilt, board shrink, level change). Tick(0) evaluates at once.
    // A head that wanders past Leash from the anchor (standing up, walking round the board) re-anchors where it is,
    // eased, and comes back to the seat once it returns.
    public sealed class EmberdeepImmersiveSky : MonoBehaviour
    {
        public const float RevealSeconds=4f;
        // Envelope around the anchor, metres: sway each side, bob up and down, lean toward / back from the board.
        public const float EnvelopeSway=.12f,EnvelopeBob=.10f,EnvelopeLean=.10f,EnvelopeBack=.05f;
        public const float Leash=.35f;          // m: a head further than this from the anchor re-anchors at the eye
        public const float EaseSeconds=.18f;    // time constant of the window's glide to a new target
        public const float SnapDegrees=50f;     // a bigger jump (recenter to a new heading) cuts instead of sweeping
        // Arena units over the plinth top: incident crowns measure 2.7-2.9 (EmberdeepRimHud.CanopyTop), + sparks.
        public const float CanopyHeadroom=EmberdeepRimHud.CanopyTop+.4f;
        public const float TrayHeadroom=.8f;    // arena units over the plinth top: standing pieces in the tray
        public const float PumpHalfWidth=2f;    // arena units around the hammer pump's axis: its head, lever and step plates
        const float MaxRadius=72;
        // Empty or loading arena (no plinth yet): the authored miniature's footprint, arena units.
        static readonly Bounds FallbackFootprint=new(new Vector3(0,2.1f,6),new Vector3(11.4f,5.8f,13.4f));

        public float Progress { get; private set; }
        public Transform Backdrop=>backdrop;
        public float AnimationClock=>skyClock;
        // The window as drawn (eased) and the target it eases to: world axis; radius in degrees (full sky inside,
        // then a Feather-wide fade). Anchor: the eye the window is measured from this frame.
        public Vector3 WindowAxis {get;private set;}=Vector3.forward;
        public float WindowRadius {get;private set;}
        public Vector3 TargetAxis {get;private set;}=Vector3.forward;
        public float TargetRadius {get;private set;}
        public float Feather=>menuOnly?8:12;
        public Vector3 Anchor=>anchor;
        public bool Seated=>seat.HasValue;
        public bool Roaming=>roaming;

        Camera view;
        Transform backdrop,arena;
        Material material;
        CameraClearFlags savedClear;
        Color savedBackground;
        bool configured,menuOnly,hasWindow,wasShown,latched,roaming;
        Vector3? seat;
        Vector3 latchedEye,anchor;
        Matrix4x4 latchedPose;
        readonly Transform[] subjects=new Transform[5];
        readonly List<MeshRenderer>[] renderers={new(),new(),new(),new(),new()};
        // Static subjects (the camp sign and set): each renderer's bounds in its subject's space, cached on refresh.
        readonly List<Bounds>[] localBounds={new(),new(),new(),new(),new()};
        readonly List<Bounds> footprint=new(3);
        readonly List<Vector2> points=new(256);
        readonly Vector3[] eyes=new Vector3[7];
        float refreshAt,skyClock;

        // Legacy tool flow only (FireRescueGame passes nulls in the spell flow): explicit subjects exclude HUD,
        // hands, particles and aim previews, and are measured live from the anchor.
        public void SetEquipmentSubjects(Transform rack,Transform held,Transform left,Transform right)
        {
            SetSubject(1,rack);SetSubject(2,held);SetSubject(3,left);SetSubject(4,right);
        }
        // The seat the board was placed from; null: latch the eye whenever the main subject is placed.
        public void SetAnchor(Vector3? seatEye)=>seat=seatEye;
        void SetSubject(int index,Transform value)
        {
            if(subjects[index]==value)return;
            subjects[index]=value;refreshAt=-1;
        }
        public void InitializeMenu(Camera camera,Transform menu)
        {menuOnly=true;Initialize(camera,menu,true);}
        public void Initialize(Camera camera,Transform subject,bool immediate=false)
        {
            if(configured || !camera || !subject)return;
            var shader=Resources.Load<Shader>("EmberdeepImmersiveSky");
            if(!shader || !shader.isSupported){Debug.LogError("Emberdeep sky shader unavailable");return;}
            view=camera;arena=subject;subjects[0]=subject;refreshAt=-1;savedClear=view.clearFlags;savedBackground=view.backgroundColor;
            material=new Material(shader){name="Emberdeep view-relative sky environment"};
            var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name="Emberdeep distant sky dome";sphere.layer=gameObject.layer;
            backdrop=sphere.transform;backdrop.SetParent(transform,false);
            var collider=sphere.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            var renderer=sphere.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            configured=true;Progress=immediate?1:0;Tick(0,false);
        }
        public void Tick(float dt,bool ready)
        {
            if(!configured || !view || !backdrop || !arena)return;
            bool shown=!menuOnly || arena.gameObject.activeInHierarchy;
            backdrop.gameObject.SetActive(shown);
            view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=Color.clear;
            var eye=view.transform.position;
            backdrop.SetPositionAndRotation(eye,Quaternion.identity);
            backdrop.localScale=Vector3.one*Mathf.Min(120,view.farClipPlane*.8f);
            UpdateAnchor(eye);
            var floor=menuOnly?arena.position:arena.TransformPoint(new Vector3(0,-.4f,6));
            var forward=Vector3.ProjectOnPlane(floor-anchor,Vector3.up);
            float distance=forward.magnitude;
            forward=distance>.01f?forward/distance:Vector3.ProjectOnPlane(arena.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.1f)forward=Vector3.forward;
            var right=Vector3.Cross(Vector3.up,forward);
            bool any=Measure(forward,right,out var axis,out float radius);
            if(any){TargetAxis=axis;TargetRadius=radius;Ease(dt,shown);}else hasWindow=false;
            wasShown=shown;
            material.SetVector("_MaskForward",forward);material.SetVector("_MaskRight",right);
            if(hasWindow)
            {
                float r=WindowRadius*Mathf.Deg2Rad,f=Feather*Mathf.Deg2Rad,elevation=Mathf.Asin(Mathf.Clamp(WindowAxis.y,-1,1));
                material.SetVector("_MaskAxis",WindowAxis);
                material.SetVector("_MaskCos",new Vector4(Mathf.Cos(r+f),Mathf.Cos(r),0,0));
                material.SetVector("_RevealRange",new Vector4(elevation-r-12*Mathf.Deg2Rad,elevation+r+12*Mathf.Deg2Rad,0,0));
            }
            else material.SetVector("_MaskCos",new Vector4(1,1,0,0));
            if(ready && float.IsFinite(dt) && dt>0)Progress=Mathf.Clamp01(Progress+Mathf.Min(dt,.1f)/RevealSeconds);
            if(ready && float.IsFinite(dt) && dt>0)skyClock+=Mathf.Min(dt,.1f);
            material.SetFloat("_SkyTime",skyClock);
            material.SetFloat("_Reveal",Progress);
        }
        // Home = the seat, or the eye latched when the main subject last moved (placement). Beyond Leash the window
        // roams with the head, re-anchoring in Leash-sized steps that Ease glides over.
        void UpdateAnchor(Vector3 eye)
        {
            var pose=arena.localToWorldMatrix;
            if(!latched || pose!=latchedPose){latchedEye=eye;latchedPose=pose;latched=true;}
            var home=seat ?? latchedEye;
            if((eye-home).sqrMagnitude<=Leash*Leash){anchor=home;roaming=false;}
            else if(!roaming || (eye-anchor).sqrMagnitude>Leash*Leash){anchor=eye;roaming=true;}
        }
        void Ease(float dt,bool shown)
        {
            bool snap=!hasWindow || !wasShown || !shown || !float.IsFinite(dt) || dt<=0
                || Vector3.Angle(WindowAxis,TargetAxis)>SnapDegrees;
            if(snap){WindowAxis=TargetAxis;WindowRadius=TargetRadius;}
            else
            {
                float k=1-Mathf.Exp(-Mathf.Min(dt,.1f)/EaseSeconds);
                WindowAxis=Vector3.Slerp(WindowAxis,TargetAxis,k).normalized;WindowRadius=Mathf.Lerp(WindowRadius,TargetRadius,k);
            }
            hasWindow=true;
        }
        static bool IsExcluded(MeshRenderer r,bool dynamicViews)
        {
            var m=r.sharedMaterial;
            if(r.GetComponent<TextMesh>() || r.name=="Spray aim" || r.name=="Instruction plate"
                || r.name=="Refill step plate" || !m || m.renderQueue>=3000)return true;
            // The camp's playground moves under the hands; its rest pose is inside the set's footprint anyway.
            return dynamicViews && (r.GetComponentInParent<EmberdeepHammerPumpView>(true) || r.GetComponentInParent<EmberdeepSheepView>(true));
        }
        void Refresh()
        {
            for(int i=0;i<subjects.Length;i++)
            {
                renderers[i].Clear();localBounds[i].Clear();
                if(!subjects[i] || (i==0 && !menuOnly))continue;
                subjects[i].GetComponentsInChildren(true,renderers[i]);
                bool fixedSubject=menuOnly;
                for(int j=renderers[i].Count-1;j>=0;j--)if(IsExcluded(renderers[i][j],fixedSubject))renderers[i].RemoveAt(j);
                if(!fixedSubject)continue;
                var toSubject=subjects[i].worldToLocalMatrix;
                foreach(var r in renderers[i])
                {
                    var filter=r.GetComponent<MeshFilter>();
                    var mesh=filter?filter.sharedMesh:null;
                    var toLocal=toSubject*r.localToWorldMatrix;
                    Bounds b=default;
                    if(mesh)
                    {
                        var m=mesh.bounds;
                        for(int c=0;c<8;c++)
                        {
                            var p=toLocal.MultiplyPoint3x4(Corner(m,c));
                            if(c==0)b=new Bounds(p,Vector3.zero);else b.Encapsulate(p);
                        }
                    }
                    else b=new Bounds(toLocal.MultiplyPoint3x4(Vector3.zero),Vector3.zero);
                    localBounds[i].Add(b);
                }
            }
            // The board: its plinth (re-measured, so a board squeeze or a new level follows), crowns over it, and
            // the piece tray (centred on the front lip since headset test 4; its widest row, for either pump side, so neither
            // a hand swap nor a level's slot count moves the window).
            footprint.Clear();
            if(!menuOnly)
            {
                if(EmberdeepRimHud.MeasurePlinth(arena,out var board))
                {
                    var top=board.max;top.y+=CanopyHeadroom;
                    var plinth=new Bounds();plinth.SetMinMax(board.min,top);footprint.Add(plinth);
                    for(int pump=-1;pump<=1;pump+=2)
                    {
                        var c=EmberdeepPieceView.TrayCentre(board,pump);
                        var tray=new Bounds();
                        tray.SetMinMax(new Vector3(c.x-EmberdeepPieceView.TrayWidth*.5f,board.max.y-.15f,c.z-EmberdeepPieceView.TrayDepth*.5f),
                            new Vector3(c.x+EmberdeepPieceView.TrayWidth*.5f,board.max.y+TrayHeadroom,c.z+EmberdeepPieceView.TrayDepth*.5f));
                        footprint.Add(tray);
                        // The hammer pump at the front corner on this side (both sides, so a water-hand swap does not move the
                        // window). The right-hand tray used to cover it; since the tray is centred (headset test 4) it is its own box.
                        float capTop=EmberdeepBoardLayout.PumpHeightFor(EmberdeepPlayArea.Height)+EmberdeepHammerPumpView.CapTopHeight/FireMiniature.Scale;
                        var at=new Vector3(pump*EmberdeepWaterTuning.PumpSideX,0,EmberdeepWaterTuning.PumpForward);
                        var pumpBox=new Bounds();
                        pumpBox.SetMinMax(new Vector3(at.x-PumpHalfWidth,board.max.y-.15f,at.z-PumpHalfWidth),new Vector3(at.x+PumpHalfWidth,Mathf.Max(capTop,board.max.y)+.3f,at.z+PumpHalfWidth));
                        footprint.Add(pumpBox);
                    }
                }
                else footprint.Add(FallbackFootprint);
            }
            refreshAt=Time.unscaledTime+.5f;
        }
        static Vector3 Corner(Bounds b,int c)=>b.center+Vector3.Scale(b.extents,new Vector3((c&1)==0?-1:1,(c&2)==0?-1:1,(c&4)==0?-1:1));
        bool Measure(Vector3 forward,Vector3 right,out Vector3 axis,out float radius)
        {
            axis=forward;radius=0;
            if(Time.unscaledTime>=refreshAt || refreshAt<0)Refresh();
            // The seated head's reach: every footprint corner as seen from each of these eyes.
            eyes[0]=anchor;
            eyes[1]=anchor-right*EnvelopeSway;eyes[2]=anchor+right*EnvelopeSway;
            eyes[3]=anchor-Vector3.up*EnvelopeBob;eyes[4]=anchor+Vector3.up*EnvelopeBob;
            eyes[5]=anchor+forward*EnvelopeLean;eyes[6]=anchor-forward*EnvelopeBack;
            points.Clear();
            for(int group=0;group<subjects.Length;group++)
            {
                var subject=subjects[group];
                if(!subject)continue;
                if(group==0 && !menuOnly)
                {
                    // The board's place keeps its window while the arena is hidden (between levels, the shop).
                    var m=subject.localToWorldMatrix;
                    foreach(var box in footprint)ProjectBox(box,m,subject,eyes.Length,forward,right);
                    // A narrower crown adds sky above the forest; the trays widen the base below.
                    if(points.Count==0)continue;
                    float lo=0,hi=0,top=-Mathf.PI*.5f;
                    foreach(var p in points){lo=Mathf.Min(lo,p.x);hi=Mathf.Max(hi,p.x);top=Mathf.Max(top,p.y);}
                    top=Mathf.Clamp(top+18*Mathf.Deg2Rad,8*Mathf.Deg2Rad,40*Mathf.Deg2Rad);
                    points.Add(new Vector2(lo*.55f,top));points.Add(new Vector2(hi*.55f,top));
                }
                else if(!subject.gameObject.activeInHierarchy)continue;
                else if(menuOnly)
                {
                    // Union of the active renderers' cached boxes, in subject space: page switches still reshape it.
                    bool found=false;Bounds union=default;
                    for(int j=0;j<renderers[group].Count;j++)
                    {
                        var r=renderers[group][j];
                        if(!r || !r.enabled || !r.gameObject.activeInHierarchy)continue;
                        if(!found){union=localBounds[group][j];found=true;}else union.Encapsulate(localBounds[group][j]);
                    }
                    if(found)ProjectBox(union,subject.localToWorldMatrix,subject,eyes.Length,forward,right);
                }
                else
                {
                    // Legacy equipment: live world bounds from the anchor alone.
                    bool found=false;Bounds bounds=default;
                    foreach(var r in renderers[group])
                    {
                        if(!r || !r.enabled || !r.gameObject.activeInHierarchy)continue;
                        if(!found){bounds=r.bounds;found=true;}else bounds.Encapsulate(r.bounds);
                    }
                    if(found)ProjectBox(bounds,Matrix4x4.identity,null,1,forward,right);
                }
            }
            if(points.Count==0)return false;
            var low=new Vector2(float.MaxValue,float.MaxValue);var high=new Vector2(float.MinValue,float.MinValue);
            foreach(var p in points){low=Vector2.Min(low,p);high=Vector2.Max(high,p);}
            var center=(low+high)*.5f;
            Vector3 Direction(Vector2 p)=>(forward*Mathf.Cos(p.x)+right*Mathf.Sin(p.x))*Mathf.Cos(p.y)+Vector3.up*Mathf.Sin(p.y);
            axis=Direction(center);
            foreach(var p in points)radius=Mathf.Max(radius,Vector3.Angle(axis,Direction(p)));
            radius=Mathf.Min(radius+(menuOnly?2:4),MaxRadius);
            return true;
        }
        // A box (in the space m maps to the world) seen from the first eyeCount eyes; an eye inside it sees no edge.
        void ProjectBox(Bounds box,Matrix4x4 m,Transform space,int eyeCount,Vector3 forward,Vector3 right)
        {
            for(int e=0;e<eyeCount;e++)
            {
                var eye=eyes[e];
                if(box.Contains(space?space.InverseTransformPoint(eye):eye))continue;
                for(int c=0;c<8;c++)Project(m.MultiplyPoint3x4(Corner(box,c)),eye,forward,right);
            }
        }
        void Project(Vector3 world,Vector3 eye,Vector3 forward,Vector3 right)
        {
            var d=world-eye;
            if(d.sqrMagnitude<.0001f || Vector3.Dot(d,forward)<=.01f)return;
            float az=Mathf.Atan2(Vector3.Dot(d,right),Vector3.Dot(d,forward));
            points.Add(new Vector2(az,Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)));
        }
        void OnDestroy()
        {
            if(configured && view && view.clearFlags==CameraClearFlags.SolidColor && view.backgroundColor==Color.clear)
            {view.clearFlags=savedClear;view.backgroundColor=savedBackground;}
            if(backdrop)Destroy(backdrop.gameObject);if(material)Destroy(material);
        }
    }
}

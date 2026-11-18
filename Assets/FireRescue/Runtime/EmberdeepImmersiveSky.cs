using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FireRescue
{
    // Distant spherical environment with a view-to-subject angular aperture.
    // Geometry follows head translation; world-space scenery does not spin with
    // the head. The aperture continuously follows the forest from any viewpoint.
    public sealed class EmberdeepImmersiveSky : MonoBehaviour
    {
        public const float RevealSeconds=4f;
        public float Progress { get; private set; }
        public Transform Backdrop=>backdrop;
        Camera view;
        Transform backdrop,arena;
        Material material;
        CameraClearFlags savedClear;
        Color savedBackground;
        bool configured,menuOnly;
        readonly Transform[] subjects=new Transform[5];
        readonly List<MeshRenderer>[] renderers={new(),new(),new(),new(),new()};
        readonly List<Vector2> points=new List<Vector2>(48);
        float refreshAt,skyClock;
        public float AnimationClock=>skyClock;
        // Explicit subjects exclude HUD, hands, particles and aim previews.
        public void SetEquipmentSubjects(Transform rack,Transform held,Transform left,Transform right)
        {
            SetSubject(1,rack);SetSubject(2,held);SetSubject(3,left);SetSubject(4,right);
        }
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
            backdrop.gameObject.SetActive(!menuOnly || arena.gameObject.activeInHierarchy);
            view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=Color.clear;
            backdrop.SetPositionAndRotation(view.transform.position,Quaternion.identity);
            backdrop.localScale=Vector3.one*Mathf.Min(120,view.farClipPlane*.8f);
            var floor=menuOnly?arena.position:arena.TransformPoint(new Vector3(0,-.4f,6));
            var delta=floor-view.transform.position;
            var forward=Vector3.ProjectOnPlane(delta,Vector3.up);
            float distance=forward.magnitude;
            forward=distance>.01f?forward/distance:Vector3.ProjectOnPlane(view.transform.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.1f)forward=arena.forward;
            var right=Vector3.Cross(Vector3.up,forward);
            UpdateOutline(forward,right);
            material.SetVector("_MaskForward",forward);material.SetVector("_MaskRight",right);
            if(ready && float.IsFinite(dt) && dt>0)Progress=Mathf.Clamp01(Progress+Mathf.Min(dt,.1f)/RevealSeconds);
            if(ready && float.IsFinite(dt) && dt>0)skyClock+=Mathf.Min(dt,.1f);
            material.SetFloat("_SkyTime",skyClock);
            material.SetFloat("_Reveal",Progress);
        }
        void UpdateOutline(Vector3 forward,Vector3 right)
        {
            if(Time.unscaledTime>=refreshAt)
            {
                for(int i=0;i<subjects.Length;i++)
                {
                    renderers[i].Clear();
                    if(subjects[i])subjects[i].GetComponentsInChildren(true,renderers[i]);
                    for(int j=renderers[i].Count-1;j>=0;j--)
                    {
                        var r=renderers[i][j];var m=r.sharedMaterial;
                        if(r.GetComponent<TextMesh>() || r.name=="Spray aim" || r.name=="Instruction plate"
                            || r.name=="Refill step plate" || !m || m.renderQueue>=3000)renderers[i].RemoveAt(j);
                    }
                }
                refreshAt=Time.unscaledTime+.5f;
            }
            points.Clear();
            for(int group=0;group<subjects.Length;group++)
            {
                bool found=false;Bounds bounds=default;
                foreach(var r in renderers[group])
                {
                    if(!r || !r.enabled || !r.gameObject.activeInHierarchy)continue;
                    if(!found){bounds=r.bounds;found=true;}else bounds.Encapsulate(r.bounds);
                }
                if(found && !bounds.Contains(view.transform.position))
                {
                    for(int corner=0;corner<8;corner++)
                        Project(bounds.center+Vector3.Scale(bounds.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1)),forward,right);
                }
                if(group==0 && !menuOnly)
                {
                    // Empty/loading arena still gets a stable miniature footprint.
                    if(points.Count==0)
                        for(int corner=0;corner<8;corner++)Project(arena.TransformPoint(new Vector3((corner&1)==0?-5.7f:5.7f,(corner&2)==0?-.8f:5,(corner&4)==0?-.7f:12.7f)),forward,right);
                    float lo=0,hi=0,top=-Mathf.PI*.5f;
                    foreach(var p in points){lo=Mathf.Min(lo,p.x);hi=Mathf.Max(hi,p.x);top=Mathf.Max(top,p.y);}
                    // A narrower crown adds sky above the forest; equipment widens the base below.
                    top=Mathf.Clamp(top+18*Mathf.Deg2Rad,8*Mathf.Deg2Rad,40*Mathf.Deg2Rad);
                    points.Add(new Vector2(lo*.55f,top));points.Add(new Vector2(hi*.55f,top));
                }
            }
            if(points.Count==0){material.SetVector("_MaskCos",new Vector4(1,1,0,0));return;}
            var low=new Vector2(float.MaxValue,float.MaxValue);var high=new Vector2(float.MinValue,float.MinValue);
            foreach(var p in points){low=Vector2.Min(low,p);high=Vector2.Max(high,p);}
            var center=(low+high)*.5f;
            Vector3 Direction(Vector2 p)=>(forward*Mathf.Cos(p.x)+right*Mathf.Sin(p.x))*Mathf.Cos(p.y)+Vector3.up*Mathf.Sin(p.y);
            var axis=Direction(center);float radius=0;
            foreach(var p in points)radius=Mathf.Max(radius,Vector3.Angle(axis,Direction(p))*Mathf.Deg2Rad);
            radius=Mathf.Min(radius+(menuOnly?2:4)*Mathf.Deg2Rad,72*Mathf.Deg2Rad);
            material.SetVector("_MaskAxis",axis);
            material.SetVector("_MaskCos",new Vector4(Mathf.Cos(radius+(menuOnly?8:12)*Mathf.Deg2Rad),Mathf.Cos(radius),0,0));
            material.SetVector("_RevealRange",new Vector4(center.y-radius-12*Mathf.Deg2Rad,center.y+radius+12*Mathf.Deg2Rad,0,0));
        }
        void Project(Vector3 world,Vector3 forward,Vector3 right)
        {
            var d=world-view.transform.position;
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

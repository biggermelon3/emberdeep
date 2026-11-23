using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FireRescue
{
    // LoopSpec §5 pieces + §11b Plan (workstream L3), presentation only (FireRun owns every rule):
    // - the piece tray: one row in the middle of the board's front lip, on the apron between the player and the front row
    //   (headset test 4, 2026-11-22: beside the board on the right it sat out of view and nobody saw the tower tip);
    // - the pieces: placeholder primitives, every object with a stable name for the art pass
    //   ("Piece tray", "Piece tower", "Piece excavator", "Piece plane token", "Plane bomber", "Plane drop marker",
    //   "Dug trench", "Placement ghost");
    // - the placement ghost on the drop square (green-white when it fits, red when not) with the tower's rain radius,
    //   or the plane's drop strip;
    // - the excavator's planned line and the dirt it has dug; the plane's marker, strip, fly-over and water curtain;
    // - headset test 3: each piece's visible box for the hand grasp (GraspBox), the hover highlight on what a hand would
    //   take, and the tower tip (EmberdeepTowerTip, anchored by TipAnchor).
    // Child of the arena: every position is in arena units. Lit clay and the veil shader, so none of it is greyed by the
    // Plan grade: in Plan, what you can pick up keeps its colour.
    public sealed class EmberdeepPieceView : MonoBehaviour
    {
        public const int PlaneId=100;
        // One row of slots, as many as the level hands out. Store build (owner, 2026-11-22): towers only, at most 3 + 2 SLOT
        // upgrades = 5. MaxSlots 6 still fits the generated grids' sets (towers, an excavator, the plane token last).
        // TrayWidth/TrayDepth are the widest tray.
        public const int MaxSlots=6;
        public const float SlotPitch=.9f,TrayRim=.1f,TrayDepth=1.0f,TrayInset=.12f,TrayScale=.8f,TrayLift=.12f;
        public const float TrayWidth=MaxSlots*SlotPitch+TrayRim*2;
        public const float TrenchWidth=.62f,TrenchHeight=.05f;
        // Squares sit between trees, under the canopies: from the seat the ground line and dirt hide. So both are also drawn
        // as a thin line just over the canopy (like the fire and forecast cues), and a placed excavator flies a flag.
        public const float OverlayHeight=2.2f,OverlayWidth=.09f,FlagHeight=2.35f;
        public static float FreeSide=>EmberdeepWaterHand.Left?1:-1;
        // The side the pump stands on for the current water hand (+1 right, -1 left).
        public static float PumpSideNow=>EmberdeepWaterTuning.PumpSide(EmberdeepWaterHand.Left);
        // Headset test 2026-11-22: the pump stands on the free-hand side and keeps the front corner (EmberdeepWaterTuning
        // PumpSideX/PumpForward). No tray slot stands within PumpKeepOut of the pump cap's axis (a fist's strike reach plus
        // a piece's grab reach, arena units), so a hammering fist never picks up a piece. The centred row clears it by
        // construction (its outer slot is ~2.75 units in from the pump's line); if a layout ever brought them together, the
        // tray would slide back just far enough. Only the tray's z moves.
        public static readonly float PumpKeepOut=(EmberdeepWaterTuning.StrikeRadius+EmberdeepPieceGrasp.GrabRadius)/FireMiniature.Scale+.05f;
        // Headset test 4 (2026-11-22): the tray is centred across the board, its row TrayInset in from the plinth's front
        // edge (in front of the creek and the front row), whichever hand holds the water. pumpSide: the pump's side.
        public static Vector3 TrayCentre(Bounds board,float pumpSide)
        {
            float x=board.center.x,z=board.min.z+TrayInset+TrayDepth*.5f;
            float outer=(MaxSlots-1)*.5f*SlotPitch;
            float dx=Mathf.Max(0,Mathf.Abs(pumpSide*EmberdeepWaterTuning.PumpSideX-x)-outer);
            float clear=Mathf.Sqrt(Mathf.Max(0,PumpKeepOut*PumpKeepOut-dx*dx));
            z=Mathf.Max(z,EmberdeepWaterTuning.PumpForward+clear);
            return new Vector3(x,board.max.y,z);
        }
        // Slot `slot` of a row of `slots`, centred on the tray. Slot 0 is on the water-hand side, so the last slot (the plane
        // token, which comes in the fight while the water hand is busy) is on the free-hand side (freeSide +1: right).
        public static Vector3 SlotLocal(int slot,int slots,float freeSide)
        {
            slots=Mathf.Clamp(slots,1,MaxSlots);slot=Mathf.Clamp(slot,0,slots-1);
            return new Vector3(freeSide*(slot-(slots-1)*.5f)*SlotPitch,TrayLift,0);
        }
        // Slots this level's tray shows: its pieces, plus the plane token's on a plane level.
        public static int Slots(FireRun run)=>run==null?1:Mathf.Clamp(run.Pieces.Length+(run.PlaneEarnable?1:0),1,MaxSlots);
        public static int PlaneSlot(FireRun run)=>Mathf.Min(run.Pieces.Length,MaxSlots-1);
        public static int PieceSlot(int piece)=>Mathf.Clamp(piece,0,MaxSlots-1);

        // What is being carried this frame (by a hand, the mouse or a test).
        public struct Carry
        {
            public int Id;            // piece index, PlaneId, or -1
            public Vector3 Local;     // arena-local position of the carried piece's base
            public int Square;        // ghost square or -1
            public bool Valid;        // dropping now would place it
            public float Yaw;         // plane drop line, board degrees
            public int Drawing;       // excavator whose line is being drawn, or -1
            public int Hover0,Hover1; // what each hand would take if it pinched or closed now (piece, PlaneId, or -1)
            public static Carry None=>new Carry{Id=-1,Square=-1,Drawing=-1,Hover0=-1,Hover1=-1};
            public bool Hovered(int id)=>id>=0 && (Hover0==id || Hover1==id);
        }
        // Hover highlight (headset test 3): the piece a pinch or close would take grows a little, lifts, and wears a soft
        // gold glow with a ring at its foot.
        public const float HoverScale=1.1f,HoverLift=.06f;

        FireRun run;FireRescueVfx effects;
        Material clay,veil;MaterialPropertyBlock block;
        Transform tray,piecesRoot,trenchRoot,lineRoot,ghostRoot,planeToken,bomber,marker,curtain,stripRoot,drawCue;
        Transform ghostTower,ghostExcavator,ghostPlane,ghostRing;
        readonly List<Transform> pieces=new List<Transform>();
        readonly List<Transform> rotors=new List<Transform>(),booms=new List<Transform>();
        readonly List<Renderer> tanks=new List<Renderer>(),rains=new List<Renderer>();
        readonly List<Transform> trench=new List<Transform>(),lineMarks=new List<Transform>(),lineLinks=new List<Transform>(),lineHigh=new List<Transform>(),trenchHigh=new List<Transform>();
        readonly List<Transform> flags=new List<Transform>();
        readonly List<Renderer> ghostRenderers=new List<Renderer>();
        // Each piece's visible box in its own root's space (unscaled piece units), and the plane token's.
        readonly List<Bounds> pieceBounds=new List<Bounds>();Bounds tokenBounds;
        readonly Transform[] hoverGlow=new Transform[2];readonly Renderer[] hoverColumn=new Renderer[2],hoverRing=new Renderer[2];
        EmberdeepTowerTip tip;
        Renderer curtainRenderer,markerRing;
        Transform propeller;
        Mesh ringMesh;
        AudioSource voice,drone,engine;
        float clock,curtainAlpha;int shownTrench;
        Vector3 lastHead;bool hadPlane;

        public Transform Tray=>tray;
        public Transform Piece(int i)=>i>=0 && i<pieces.Count?pieces[i]:null;
        public Transform PlaneToken=>planeToken;
        public Transform Bomber=>bomber;
        public Transform Ghost=>ghostRoot;
        public bool GhostVisible=>ghostRoot && ghostRoot.gameObject.activeSelf;
        public bool GhostValid {get;private set;}
        public bool TrayVisible=>tray && tray.gameObject.activeInHierarchy;
        public int TrenchPieces=>shownTrench;
        public int LineMarks {get;private set;}
        public float RingRadius {get;private set;}
        public EmberdeepTowerTip Tip=>tip;
        // The hover glow drawn for this hand (piece, PlaneId, or -1): tests and captures.
        public int HoverShown(int hand)=>hand>=0 && hand<2 && hoverGlow[hand] && hoverGlow[hand].gameObject.activeSelf?shownHover[hand]:-1;
        readonly int[] shownHover={-1,-1};

        // ---- Grasp boxes (EmberdeepPieceGrasp) ------------------------------------------------------------------------
        // A piece's visible box in world space: the parts it shows in the tray or on the board (not the rain column, not
        // the excavator's flag). Rotates and scales with the piece (tray scale, hover, the board's tilt).
        public EmberdeepPieceGrasp.Candidate GraspBox(int id)
        {
            var root=id==PlaneId?planeToken:Piece(id);
            if(!root)return new EmberdeepPieceGrasp.Candidate(id,new Vector3(float.NaN,float.NaN,float.NaN));
            var b=id==PlaneId?tokenBounds:id<pieceBounds.Count?pieceBounds[id]:new Bounds(Vector3.up*.5f,Vector3.one);
            var s=root.lossyScale;
            return new EmberdeepPieceGrasp.Candidate(id,root.TransformPoint(b.center),root.rotation,
                new Vector3(Mathf.Abs(b.extents.x*s.x),Mathf.Abs(b.extents.y*s.y),Mathf.Abs(b.extents.z*s.z)));
        }
        public Bounds LocalBox(int id)=>id==PlaneId?tokenBounds:id>=0 && id<pieceBounds.Count?pieceBounds[id]:default;
        // Bounds of every mesh under root, in root's space; parts switched off under root (rain, flag) are left out.
        static Bounds MeasureLocal(Transform root)
        {
            bool any=false;var b=new Bounds();
            foreach(var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!mf.sharedMesh || HiddenBelow(mf.transform,root))continue;
                var m=root.worldToLocalMatrix*mf.transform.localToWorldMatrix;var mb=mf.sharedMesh.bounds;
                for(int c=0;c<8;c++)
                {
                    var p=m.MultiplyPoint3x4(mb.center+Vector3.Scale(mb.extents,new Vector3((c&1)==0?-1:1,(c&2)==0?-1:1,(c&4)==0?-1:1)));
                    if(!any){b=new Bounds(p,Vector3.zero);any=true;}else b.Encapsulate(p);
                }
            }
            return any?b:new Bounds(Vector3.up*.5f,Vector3.one);
        }
        static bool HiddenBelow(Transform t,Transform root){for(;t && t!=root;t=t.parent)if(!t.gameObject.activeSelf)return true;return false;}
        // Where the tower tip hangs: over the top of a tower's tray slot (world), whether or not it is in the slot now.
        public Vector3 TipAnchor(int piece)
        {
            var slot=tray.localPosition+SlotLocal(PieceSlot(piece),Slots(run),FreeSide);
            float top=piece<pieceBounds.Count?pieceBounds[piece].max.y:1.8f;
            return transform.TransformPoint(slot+Vector3.up*top*TrayScale);
        }

        public void Initialize(FireRescueVfx vfx)
        {
            effects=vfx;block=new MaterialPropertyBlock();
            var tutorial=Resources.Load<Shader>("EmberdeepTutorial");var veilShader=Resources.Load<Shader>("EmberdeepOutcomeVeil");
            clay=tutorial?new Material(tutorial){name="Piece clay"}:new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Piece clay"};
            veil=veilShader?new Material(veilShader){name="Piece ghost"}:new Material(clay){name="Piece ghost"};
            // Voices live on their own objects: they move to where the sound happens, the board pieces never do.
            voice=Group("Piece voice",transform).gameObject.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=.8f;voice.minDistance=.4f;voice.dopplerLevel=0;
            BuildTray();BuildGhosts();BuildPlane();
            trenchRoot=Group("Dug trench",transform);lineRoot=Group("Excavator line",transform);
            drawCue=Flat("Excavator draw cue",transform,RingMesh(),veil);drawCue.gameObject.SetActive(false);
            for(int h=0;h<2;h++)
            {
                hoverGlow[h]=Group("Piece hover "+(h==0?"left":"right"),transform);
                hoverColumn[h]=Part("Hover glow",PrimitiveType.Cylinder,hoverGlow[h],Vector3.zero,Vector3.one,new Color(1,.85f,.4f,.18f),veil).GetComponent<Renderer>();
                hoverRing[h]=Flat("Hover ring",hoverGlow[h],RingMesh(),veil).GetComponent<Renderer>();
                hoverGlow[h].gameObject.SetActive(false);
            }
            tip=new GameObject("Tower tip").AddComponent<EmberdeepTowerTip>();tip.transform.SetParent(transform,false);
        }

        // ---- Build ---------------------------------------------------------------------------------------------------
        static Transform Group(string name,Transform parent){var t=new GameObject(name).transform;t.SetParent(parent,false);return t;}
        GameObject Part(string name,PrimitiveType type,Transform parent,Vector3 at,Vector3 scale,Color color,Material material=null)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=at;go.transform.localScale=scale;
            var c=go.GetComponent<Collider>();c.enabled=false;Destroy(c);
            var r=go.GetComponent<Renderer>();r.sharedMaterial=material?material:clay;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            Tint(r,color);return go;
        }
        void Tint(Renderer r,Color color)
        {
            if(!r)return;block.Clear();
            if(r.sharedMaterial==veil)block.SetColor("_Color",color);else block.SetColor("_BaseColor",color);
            r.SetPropertyBlock(block);
        }
        Transform Flat(string name,Transform parent,Mesh mesh,Material material)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;
            r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;return go.transform;
        }
        Mesh RingMesh()
        {
            if(ringMesh)return ringMesh;
            const int segments=64;var v=new Vector3[segments*2+2];var t=new int[segments*6];
            for(int i=0;i<=segments;i++){float a=i*Mathf.PI*2/segments;var d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));v[i*2]=d;v[i*2+1]=d*.93f;}
            for(int i=0;i<segments;i++){int k=i*2;t[i*6]=k;t[i*6+1]=k+1;t[i*6+2]=k+2;t[i*6+3]=k+1;t[i*6+4]=k+3;t[i*6+5]=k+2;}
            ringMesh=new Mesh{name="Piece ring",vertices=v,triangles=t};ringMesh.RecalculateBounds();return ringMesh;
        }
        void BuildTray()
        {
            tray=Group("Piece tray",transform);
            var wood=new Color(.50f,.32f,.17f);
            trayBoard=Part("Tray board",PrimitiveType.Cube,tray,new Vector3(0,-.02f,0),new Vector3(TrayWidth,.12f,TrayDepth),wood).transform;
            for(int s=0;s<2;s++)
            {
                trayEnds[s]=Part("Tray rim",PrimitiveType.Cube,tray,Vector3.zero,new Vector3(TrayRim,.12f,TrayDepth),wood*.85f).transform;
                traySides[s]=Part("Tray rim",PrimitiveType.Cube,tray,Vector3.zero,new Vector3(TrayWidth,.12f,TrayRim),wood*.85f).transform;
            }
            for(int slot=0;slot<MaxSlots;slot++)traySlots[slot]=Part("Tray slot",PrimitiveType.Cylinder,tray,Vector3.zero,new Vector3(.74f,.012f,.74f),new Color(.30f,.19f,.10f)).transform;
            FitTray(MaxSlots,1);
        }
        readonly Transform[] trayEnds=new Transform[2],traySides=new Transform[2],traySlots=new Transform[MaxSlots];Transform trayBoard;
        int trayShown=-1;float traySide;
        // The tray's board, rims and slot discs sized to a row of `slots`.
        void FitTray(int slots,float freeSide)
        {
            slots=Mathf.Clamp(slots,1,MaxSlots);
            if(slots==trayShown && freeSide==traySide)return;
            trayShown=slots;traySide=freeSide;
            float width=slots*SlotPitch+TrayRim*2;
            trayBoard.localScale=new Vector3(width,.12f,TrayDepth);
            for(int s=0;s<2;s++)
            {
                float sign=s==0?-1:1;
                trayEnds[s].localPosition=new Vector3(sign*(width-TrayRim)*.5f,.07f,0);
                traySides[s].localPosition=new Vector3(0,.07f,sign*(TrayDepth-TrayRim)*.5f);traySides[s].localScale=new Vector3(width,.12f,TrayRim);
            }
            for(int i=0;i<MaxSlots;i++)
            {
                bool on=i<slots;if(traySlots[i].gameObject.activeSelf!=on)traySlots[i].gameObject.SetActive(on);
                if(on)traySlots[i].localPosition=SlotLocal(i,slots,freeSide)-Vector3.up*TrayLift+Vector3.up*.045f;
            }
        }
        public int TraySlotsShown=>trayShown;
        Transform BuildTower(Transform parent,Material material,Color tank,List<Renderer> renderers=null)
        {
            var root=new GameObject("Piece tower").transform;root.SetParent(parent,false);
            var steel=new Color(.62f,.66f,.66f);
            for(int i=0;i<4;i++)
            {
                float a=i*Mathf.PI*.5f+Mathf.PI*.25f;
                var leg=Part("Tower leg",PrimitiveType.Cube,root,new Vector3(Mathf.Cos(a)*.22f,.56f,Mathf.Sin(a)*.22f),new Vector3(.07f,1.12f,.07f),steel,material);
                renderers?.Add(leg.GetComponent<Renderer>());
            }
            var body=Part("Tower tank",PrimitiveType.Cylinder,root,new Vector3(0,1.32f,0),new Vector3(.7f,.3f,.7f),tank,material);
            var cap=Part("Tower cap",PrimitiveType.Cylinder,root,new Vector3(0,1.66f,0),new Vector3(.76f,.05f,.76f),new Color(.92f,.95f,.95f),material);
            var rotor=Part("Tower sprinkler",PrimitiveType.Cube,root,new Vector3(0,1.75f,0),new Vector3(.82f,.05f,.08f),new Color(.98f,.78f,.32f),material);
            if(renderers!=null){renderers.Add(body.GetComponent<Renderer>());renderers.Add(cap.GetComponent<Renderer>());renderers.Add(rotor.GetComponent<Renderer>());}
            else
            {
                tanks.Add(body.GetComponent<Renderer>());rotors.Add(rotor.transform);
                // The shower while it rains: one soft, see-through column over the whole rain radius.
                var rain=Part("Tower rain",PrimitiveType.Cylinder,root,new Vector3(0,.88f,0),Vector3.one,new Color(.6f,.85f,1,.15f),veil);
                rain.SetActive(false);rains.Add(rain.GetComponent<Renderer>());
            }
            return root;
        }
        Transform BuildExcavator(Transform parent,Material material,List<Renderer> renderers=null)
        {
            var root=new GameObject("Piece excavator").transform;root.SetParent(parent,false);
            var yellow=new Color(.98f,.72f,.12f);var dark=new Color(.18f,.17f,.16f);
            var parts=new List<GameObject>();
            for(int s=-1;s<=1;s+=2)parts.Add(Part("Excavator track",PrimitiveType.Cube,root,new Vector3(s*.24f,.1f,0),new Vector3(.16f,.2f,.78f),dark,material));
            parts.Add(Part("Excavator body",PrimitiveType.Cube,root,new Vector3(0,.34f,-.04f),new Vector3(.5f,.26f,.56f),yellow,material));
            parts.Add(Part("Excavator cab",PrimitiveType.Cube,root,new Vector3(-.1f,.6f,-.1f),new Vector3(.26f,.28f,.28f),new Color(.55f,.82f,.92f),material));
            var boom=new GameObject("Excavator boom").transform;boom.SetParent(root,false);boom.localPosition=new Vector3(.12f,.46f,.14f);
            parts.Add(Part("Excavator arm",PrimitiveType.Cube,boom,new Vector3(0,.12f,.26f),new Vector3(.08f,.08f,.56f),yellow,material));
            parts.Add(Part("Excavator bucket",PrimitiveType.Cube,boom,new Vector3(0,-.04f,.56f),new Vector3(.22f,.16f,.14f),dark,material));
            boom.localRotation=Quaternion.Euler(18,0,0);
            if(renderers==null)
            {
                // Seen over the canopy once it stands on the board.
                var flag=new GameObject("Excavator flag").transform;flag.SetParent(root,false);
                Part("Flag pole",PrimitiveType.Cylinder,flag,new Vector3(-.1f,FlagHeight*.5f,-.1f),new Vector3(.04f,FlagHeight*.5f,.04f),new Color(.2f,.18f,.16f));
                Part("Flag pennant",PrimitiveType.Cube,flag,new Vector3(-.1f,FlagHeight-.12f,.08f),new Vector3(.03f,.2f,.34f),new Color(.98f,.72f,.12f));
                flag.gameObject.SetActive(false);flags.Add(flag);
            }
            if(renderers!=null)foreach(var p in parts)renderers.Add(p.GetComponent<Renderer>());else booms.Add(boom);
            return root;
        }
        Transform BuildPlaneModel(Transform parent,string name,Material material,float scale,List<Renderer> renderers=null)
        {
            var root=new GameObject(name).transform;root.SetParent(parent,false);root.localScale=Vector3.one*scale;
            var red=new Color(.86f,.22f,.12f);var cream=new Color(.96f,.93f,.84f);
            var parts=new List<GameObject>
            {
                Part("Plane fuselage",PrimitiveType.Cube,root,Vector3.zero,new Vector3(.32f,.3f,1.7f),red,material),
                Part("Plane wing",PrimitiveType.Cube,root,new Vector3(0,.1f,.15f),new Vector3(2.6f,.06f,.44f),cream,material),
                Part("Plane tailplane",PrimitiveType.Cube,root,new Vector3(0,.08f,-.74f),new Vector3(.95f,.05f,.26f),cream,material),
                Part("Plane fin",PrimitiveType.Cube,root,new Vector3(0,.3f,-.74f),new Vector3(.05f,.38f,.3f),red,material),
                Part("Plane tank",PrimitiveType.Cube,root,new Vector3(0,-.2f,.05f),new Vector3(.26f,.14f,.7f),new Color(.18f,.48f,.78f),material),
            };
            var prop=Part("Plane propeller",PrimitiveType.Cube,root,new Vector3(0,0,.88f),new Vector3(.9f,.07f,.03f),new Color(.2f,.2f,.2f),material);
            parts.Add(prop);
            if(renderers!=null)foreach(var p in parts)renderers.Add(p.GetComponent<Renderer>());else if(name=="Plane bomber")propeller=prop.transform;
            return root;
        }
        void BuildGhosts()
        {
            ghostRoot=Group("Placement ghost",transform);
            ghostTower=BuildTower(ghostRoot,veil,Color.white,ghostRenderers);
            ghostExcavator=BuildExcavator(ghostRoot,veil,ghostRenderers);
            ghostPlane=Group("Ghost plane marker",ghostRoot);
            ghostRenderers.Add(Part("Ghost marker pole",PrimitiveType.Cylinder,ghostPlane,new Vector3(0,.35f,0),new Vector3(.05f,.35f,.05f),Color.white,veil).GetComponent<Renderer>());
            ghostRenderers.Add(Part("Ghost marker flag",PrimitiveType.Cube,ghostPlane,new Vector3(0,.6f,.15f),new Vector3(.03f,.2f,.3f),Color.white,veil).GetComponent<Renderer>());
            ghostRing=Flat("Ghost rain radius",ghostRoot,RingMesh(),veil);ghostRenderers.Add(ghostRing.GetComponent<Renderer>());
            ghostRoot.gameObject.SetActive(false);
            stripRoot=Group("Plane drop strip",transform);
            for(int s=-1;s<=1;s+=2)Part("Strip edge",PrimitiveType.Cube,stripRoot,new Vector3(s*EmberdeepPieceTuning.PlaneHalfWidth,.04f,0),new Vector3(.05f,.02f,1),Color.white,veil);
            Part("Strip arrow",PrimitiveType.Cube,stripRoot,new Vector3(0,.05f,0),new Vector3(.12f,.02f,1.2f),Color.white,veil);
            stripRoot.gameObject.SetActive(false);
        }
        void BuildPlane()
        {
            planeToken=Group("Piece plane token",transform);
            Part("Token base",PrimitiveType.Cylinder,planeToken,new Vector3(0,.04f,0),new Vector3(.62f,.04f,.62f),new Color(.86f,.22f,.12f));
            BuildPlaneModel(planeToken,"Token plane",clay,.32f).localPosition=new Vector3(0,.3f,0);
            tokenBounds=MeasureLocal(planeToken);planeToken.gameObject.SetActive(false);
            bomber=BuildPlaneModel(transform,"Plane bomber",clay,1);bomber.gameObject.SetActive(false);
            curtain=Group("Plane water curtain",bomber);
            curtainRenderer=Part("Curtain sheet",PrimitiveType.Cube,curtain,Vector3.zero,Vector3.one,new Color(.6f,.85f,1,.35f),veil).GetComponent<Renderer>();
            marker=Group("Plane drop marker",transform);
            Part("Marker pole",PrimitiveType.Cylinder,marker,new Vector3(0,.4f,0),new Vector3(.05f,.4f,.05f),new Color(.95f,.95f,.9f));
            Part("Marker flag",PrimitiveType.Cube,marker,new Vector3(0,.68f,.16f),new Vector3(.03f,.22f,.32f),new Color(1,.45f,.12f));
            markerRing=Flat("Marker ring",marker,RingMesh(),veil).GetComponent<Renderer>();
            marker.gameObject.SetActive(false);
            drone=bomber.gameObject.AddComponent<AudioSource>();drone.playOnAwake=false;drone.loop=true;drone.clip=DroneClip;drone.spatialBlend=.85f;drone.minDistance=.3f;drone.dopplerLevel=0;
            engine=Group("Excavator engine",transform).gameObject.AddComponent<AudioSource>();engine.playOnAwake=false;engine.loop=true;engine.clip=EngineClip;engine.spatialBlend=.9f;engine.minDistance=.2f;engine.dopplerLevel=0;
        }

        // Rebuilds the level's pieces (BuildIncident). The tray, ghosts and plane persist.
        public void Build(FireRun current)
        {
            run=current;
            foreach(var p in pieces)if(p){p.gameObject.SetActive(false);Destroy(p.gameObject);}
            if(piecesRoot){Destroy(piecesRoot.gameObject);}
            pieces.Clear();rotors.Clear();booms.Clear();tanks.Clear();rains.Clear();flags.Clear();pieceBounds.Clear();
            piecesRoot=Group("Pieces",transform);
            for(int i=0;i<run.Pieces.Length;i++)
            {
                var p=run.Pieces[i];
                var t=p.Kind==FirePieceKind.Tower?BuildTower(piecesRoot,clay,new Color(.12f,.45f,.74f)):BuildExcavator(piecesRoot,clay);
                if(p.Kind==FirePieceKind.Tower){booms.Add(null);flags.Add(null);}else{rotors.Add(null);tanks.Add(null);rains.Add(null);}
                t.name=(p.Kind==FirePieceKind.Tower?"Piece tower ":"Piece excavator ")+i;pieces.Add(t);pieceBounds.Add(MeasureLocal(t));
            }
            for(int h=0;h<2;h++){hoverGlow[h].gameObject.SetActive(false);shownHover[h]=-1;}
            foreach(var t in trench)if(t)t.gameObject.SetActive(false);shownTrench=0;
            ghostRoot.gameObject.SetActive(false);stripRoot.gameObject.SetActive(false);marker.gameObject.SetActive(false);bomber.gameObject.SetActive(false);
            if(drone && drone.isPlaying)drone.Stop();if(engine && engine.isPlaying)engine.Stop();
            hadPlane=false;
        }
        public void Bind(FireRun current)
        {
            if(run==current)return;
            Unbind();run=current;
            run.TowerRained+=OnTowerRained;run.ExcavatorDug+=OnDug;run.PlaneArrived+=OnPlaneArrived;
        }
        void Unbind(){if(run==null)return;run.TowerRained-=OnTowerRained;run.ExcavatorDug-=OnDug;run.PlaneArrived-=OnPlaneArrived;}
        public int TowerRains {get;private set;}
        public int PlaneRuns {get;private set;}
        void OnTowerRained(int i)
        {
            TowerRains++;if(!effects || i<0 || i>=run.Pieces.Length)return;
            var at=transform.TransformPoint(run.RainPoint(run.Pieces[i])+Vector3.up*1.2f);
            effects.Burst(at,run.TowerRadius*FireMiniature.Scale);voice.transform.position=at;voice.PlayOneShot(RainClip,.5f);
        }
        void OnDug(int piece,int square){if(voice)voice.PlayOneShot(ThudClip,.35f);}
        void OnPlaneArrived(){PlaneRuns++;if(drone){drone.volume=EmberdeepPieceTuning.PlaneVolume;drone.Play();}}
        public void PlayPlace(Vector3 world){if(voice){voice.transform.position=world;voice.PlayOneShot(ThudClip,.5f);}}

        // ---- Per frame ---------------------------------------------------------------------------------------------
        public void Render(FireRun current,Bounds board,Carry carry,Camera eye,float dt)
        {
            run=current;dt=Mathf.Clamp(dt,0,.1f);clock+=dt;
            bool live=run.BoardRules && run.HasPieces && (run.Phase==FirePhase.Preparing || run.Phase==FirePhase.Fighting);
            float side=FreeSide;int slots=Slots(run);
            tray.gameObject.SetActive(live && (run.Pieces.Length>0 || run.PlaneEarnable));
            tray.localPosition=TrayCentre(board,PumpSideNow);FitTray(slots,side);
            for(int i=0;i<pieces.Count && i<run.Pieces.Length;i++)RenderPiece(i,run.Pieces[i],carry,side,slots,live,dt);
            // Plane token: in the tray once the charge is full (fight only).
            bool tokenHeld=carry.Id==PlaneId;
            planeToken.gameObject.SetActive(live && (run.PlaneReady || tokenHeld));
            if(planeToken.gameObject.activeSelf)
            {
                planeToken.localPosition=tokenHeld?carry.Local:tray.localPosition+SlotLocal(PlaneSlot(run),slots,side);
                planeToken.localRotation=Quaternion.Euler(0,tokenHeld?carry.Yaw:clock*40,0);
                planeToken.localScale=Vector3.one*(tokenHeld?1:TrayScale*(1+.06f*Mathf.Sin(clock*6)));
                if(!tokenHeld && carry.Hovered(PlaneId)){planeToken.localScale*=HoverScale;planeToken.localPosition+=Vector3.up*HoverLift;}
            }
            RenderHover(carry,live);
            RenderGhost(carry,live);
            RenderLines(carry,live);
            RenderTrench();
            RenderPlane(live,dt);
        }
        void RenderPiece(int i,FireRun.Piece p,Carry carry,float side,int slots,bool live,float dt)
        {
            var t=pieces[i];bool held=carry.Id==i;
            bool show=live;
            if(t.gameObject.activeSelf!=show)t.gameObject.SetActive(show);if(!show)return;
            if(held){t.localPosition=carry.Local;t.localScale=Vector3.one;t.localRotation=Quaternion.identity;}
            else if(!p.Placed){t.localPosition=tray.localPosition+SlotLocal(PieceSlot(i),slots,side);t.localScale=Vector3.one*TrayScale;t.localRotation=Quaternion.identity;}
            else if(p.Kind==FirePieceKind.Tower){t.localPosition=run.Squares[p.Square];t.localScale=Vector3.one;t.localRotation=Quaternion.identity;}
            else
            {
                t.localPosition=run.ExcavatorPosition(i);t.localScale=Vector3.one;
                // Face along the line (the next square to drive, else the first drawn one).
                int next=p.Driven<p.Line.Count?p.Line[p.Driven]:p.Line.Count>0?p.Line[p.Line.Count-1]:-1;
                var heading=next>=0?run.Squares[next]-t.localPosition:Vector3.zero;heading.y=0;
                if(heading.sqrMagnitude>.01f)t.localRotation=Quaternion.Slerp(t.localRotation,Quaternion.LookRotation(heading),1-Mathf.Exp(-dt*8));
            }
            if(!held && carry.Hovered(i)){t.localScale*=HoverScale;t.localPosition+=Vector3.up*HoverLift;}
            if(p.Kind==FirePieceKind.Tower)
            {
                if(rotors[i])rotors[i].localRotation=Quaternion.Euler(0,p.Raining?clock*520:0,0);
                Tint(tanks[i],p.Raining?Color.Lerp(new Color(.25f,.65f,.95f),Color.white,.3f+.3f*Mathf.Sin(clock*14)):p.Used?new Color(.30f,.38f,.42f):new Color(.12f,.45f,.74f));
                if(p.Raining && effects)effects.Sprinkler(transform.TransformPoint(run.Squares[p.Square]+Vector3.up*1.75f),run.TowerRadius*FireMiniature.Scale,dt);
                var rain=rains[i];
                if(rain)
                {
                    bool raining=p.Raining && p.Placed && !held;if(rain.gameObject.activeSelf!=raining)rain.gameObject.SetActive(raining);
                    if(raining)
                    {
                        // Fades in and out over the first and last half second of the 4 s shower.
                        float fade=Mathf.Clamp01((EmberdeepPieceTuning.TowerSeconds-p.RainRemaining)/.5f)*Mathf.Clamp01(p.RainRemaining/.5f);
                        rain.transform.localScale=new Vector3(run.TowerRadius*2,.88f,run.TowerRadius*2);
                        Tint(rain,new Color(.62f,.86f,1,(.10f+.05f*Mathf.Sin(clock*11))*fade));
                    }
                }
            }
            else if(booms[i])
            {
                bool flag=p.Placed && !held;if(flags[i] && flags[i].gameObject.activeSelf!=flag)flags[i].gameObject.SetActive(flag);
                bool driving=p.Driving && !p.Finished;
                booms[i].localRotation=Quaternion.Euler(driving?18+22*Mathf.Sin(clock*6.5f):18,0,0);
            }
        }
        // One soft gold column around the hovered piece's visible box and a ring at its foot, per hand (one when both hands
        // hover the same piece).
        void RenderHover(Carry carry,bool live)
        {
            float pulse=.5f+.5f*Mathf.Sin(clock*6);
            for(int h=0;h<2;h++)
            {
                int id=h==0?carry.Hover0:carry.Hover1;
                if(h==1 && id==carry.Hover0)id=-1;
                var root=id==PlaneId?planeToken:Piece(id);
                bool show=live && id>=0 && id!=carry.Id && root && root.gameObject.activeInHierarchy;
                if(hoverGlow[h].gameObject.activeSelf!=show)hoverGlow[h].gameObject.SetActive(show);
                shownHover[h]=show?id:-1;if(!show)continue;
                var b=LocalBox(id);float s=root.localScale.x; // arena units: pieces are children of this view (no tilt between)
                float radius=Mathf.Max(b.extents.x,b.extents.z)*s;
                hoverGlow[h].localPosition=root.localPosition;hoverGlow[h].localRotation=root.localRotation;
                var column=hoverColumn[h].transform;
                column.localPosition=new Vector3(b.center.x*s,b.center.y*s,b.center.z*s);
                column.localScale=new Vector3(radius*2.5f,b.extents.y*s*1.12f,radius*2.5f);
                Tint(hoverColumn[h],new Color(1,.86f,.45f,.12f+.08f*pulse));
                var ring=hoverRing[h].transform;ring.localPosition=Vector3.up*(.03f-HoverLift);
                float r=radius*1.7f+.04f*pulse;ring.localScale=new Vector3(r,1,r);
                Tint(hoverRing[h],new Color(1,.8f,.3f,.55f+.35f*pulse));
            }
        }
        void RenderGhost(Carry carry,bool live)
        {
            bool show=live && carry.Id>=0 && carry.Square>=0;
            if(ghostRoot.gameObject.activeSelf!=show)ghostRoot.gameObject.SetActive(show);
            bool plane=carry.Id==PlaneId;
            stripRoot.gameObject.SetActive(show && plane || run.PlaneState==FireRun.PlaneStage.Marked);
            if(!show){GhostValid=false;RingRadius=0;return;}
            GhostValid=carry.Valid;
            var kind=plane?FirePieceKind.Plane:run.Pieces[carry.Id].Kind;
            ghostTower.gameObject.SetActive(kind==FirePieceKind.Tower);ghostExcavator.gameObject.SetActive(kind==FirePieceKind.Excavator);
            ghostPlane.gameObject.SetActive(kind==FirePieceKind.Plane);ghostRing.gameObject.SetActive(kind==FirePieceKind.Tower);
            ghostRoot.localPosition=run.Squares[carry.Square]+Vector3.up*.01f;
            ghostRoot.localRotation=plane?Quaternion.Euler(0,carry.Yaw,0):Quaternion.identity;
            float pulse=.5f+.5f*Mathf.Sin(clock*7);
            var color=carry.Valid?new Color(.78f,1,.86f,.42f+.14f*pulse):new Color(1,.25f,.18f,.42f+.14f*pulse);
            foreach(var r in ghostRenderers)Tint(r,color);
            RingRadius=kind==FirePieceKind.Tower?run.TowerRadius:0;
            ghostRing.localScale=new Vector3(RingRadius,1,RingRadius);ghostRing.localPosition=Vector3.up*.03f;
            Tint(ghostRing.GetComponent<Renderer>(),carry.Valid?new Color(.55f,.88f,1,.55f+.15f*pulse):new Color(1,.3f,.2f,.5f));
            if(plane)PlaceStrip(run.Squares[carry.Square],carry.Yaw,carry.Valid?new Color(1,.8f,.4f,.5f):new Color(1,.3f,.2f,.45f));
        }
        void PlaceStrip(Vector3 centre,float yaw,Color color)
        {
            // Long enough to cross the whole board along the line.
            float length=16;
            stripRoot.localPosition=centre;stripRoot.localRotation=Quaternion.Euler(0,yaw,0);
            for(int i=0;i<stripRoot.childCount;i++)
            {
                var c=stripRoot.GetChild(i);var s=c.localScale;
                c.localScale=new Vector3(s.x,s.y,c.name=="Strip arrow"?1.2f:length);
                Tint(c.GetComponent<Renderer>(),color);
            }
        }
        void RenderLines(Carry carry,bool live)
        {
            int n=0,links=0;bool cue=false;Vector3 cueAt=default;
            if(live)for(int i=0;i<run.Pieces.Length;i++)
            {
                var p=run.Pieces[i];if(p.Kind!=FirePieceKind.Excavator || !p.Placed || p.Finished)continue;
                bool drawing=carry.Drawing==i;
                if(p.Line.Count==0 && carry.Id!=i){cue=true;cueAt=run.Squares[p.Square];}
                var color=drawing?new Color(1,.86f,.35f):p.LineDone?new Color(1,.62f,.2f):new Color(1,.75f,.3f);
                var prev=run.Squares[p.Driven==0?p.Square:p.Line[p.Driven-1]];
                for(int k=p.Driven;k<p.Line.Count;k++)
                {
                    var at=run.Squares[p.Line[k]];
                    var mark=Pooled(lineMarks,n++,"Line square",PrimitiveType.Cylinder,lineRoot);
                    mark.localPosition=at+Vector3.up*.03f;mark.localScale=new Vector3(.34f,.012f,.34f);Tint(mark.GetComponent<Renderer>(),color);
                    var link=Pooled(lineLinks,links,"Line link",PrimitiveType.Cube,lineRoot);
                    Span(link,prev,at,.07f,.02f);Tint(link.GetComponent<Renderer>(),color);
                    var high=Pooled(lineHigh,links++,"Line over canopy",PrimitiveType.Cube,lineRoot);
                    Span(high,prev+Vector3.up*OverlayHeight,at+Vector3.up*OverlayHeight,OverlayWidth,.03f);Tint(high.GetComponent<Renderer>(),color);
                    prev=at;
                }
            }
            for(int k=n;k<lineMarks.Count;k++)if(lineMarks[k].gameObject.activeSelf)lineMarks[k].gameObject.SetActive(false);
            for(int k=links;k<lineHigh.Count;k++)if(lineHigh[k].gameObject.activeSelf)lineHigh[k].gameObject.SetActive(false);
            for(int k=links;k<lineLinks.Count;k++)if(lineLinks[k].gameObject.activeSelf)lineLinks[k].gameObject.SetActive(false);
            LineMarks=n;
            // A placed excavator with no line yet pulses a ring: touch it with a fingertip and draw.
            drawCue.gameObject.SetActive(cue);
            if(cue)
            {
                float pulse=.5f+.5f*Mathf.Sin(clock*5);drawCue.localPosition=cueAt+Vector3.up*.02f;drawCue.localScale=new Vector3(.55f+.1f*pulse,1,.55f+.1f*pulse);
                Tint(drawCue.GetComponent<Renderer>(),new Color(1,.8f,.35f,.4f+.3f*pulse));
            }
            bool driving=false;foreach(var p in run.Pieces)if(p.Kind==FirePieceKind.Excavator && p.Driving && !p.Finished)driving=true;
            if(engine){if(driving && run.Phase==FirePhase.Fighting && !engine.isPlaying){engine.volume=.25f;engine.Play();}else if((!driving || run.Phase!=FirePhase.Fighting) && engine.isPlaying)engine.Stop();}
            if(engine && driving)for(int i=0;i<run.Pieces.Length;i++)if(run.Pieces[i].Driving && !run.Pieces[i].Finished)engine.transform.position=transform.TransformPoint(run.ExcavatorPosition(i));
        }
        Transform Pooled(List<Transform> pool,int index,string name,PrimitiveType type,Transform parent)
        {
            while(pool.Count<=index)pool.Add(Part(name,type,parent,Vector3.zero,Vector3.one,Color.white).transform);
            var t=pool[index];if(!t.gameObject.activeSelf)t.gameObject.SetActive(true);return t;
        }
        static void Span(Transform t,Vector3 a,Vector3 b,float width,float height)
        {
            var d=b-a;d.y=0;float length=d.magnitude;
            t.localPosition=(a+b)*.5f+Vector3.up*(height*.5f+.01f);
            t.localRotation=length>1e-4f?Quaternion.LookRotation(d/length):Quaternion.identity;
            t.localScale=new Vector3(width,height,Mathf.Max(length,width));
        }
        void RenderTrench()
        {
            int count=run.DugCount;
            for(int i=0;i<count;i++)
            {
                var s=run.DugSegment(i);var a=new Vector3(s.x,0,s.y);var b=new Vector3(s.z,0,s.w);
                var t=Pooled(trench,i,"Dug dirt",PrimitiveType.Cube,trenchRoot);
                // Each segment is dirt from square to square; zero length = the excavator's own square.
                Span(t,a,b,TrenchWidth,TrenchHeight);
                if((b-a).sqrMagnitude<1e-4f)t.localScale=new Vector3(TrenchWidth,TrenchHeight,TrenchWidth);
                else t.localScale=new Vector3(TrenchWidth,TrenchHeight,t.localScale.z+TrenchWidth);
                Tint(t.GetComponent<Renderer>(),i%2==0?new Color(.36f,.24f,.14f):new Color(.32f,.21f,.12f));
                var high=Pooled(trenchHigh,i,"Dug line over canopy",PrimitiveType.Cube,trenchRoot);
                Span(high,a+Vector3.up*OverlayHeight,b+Vector3.up*OverlayHeight,OverlayWidth*1.4f,.03f);
                if((b-a).sqrMagnitude<1e-4f)high.localScale=new Vector3(OverlayWidth*2.5f,.03f,OverlayWidth*2.5f);
                Tint(high.GetComponent<Renderer>(),new Color(.55f,.36f,.18f));
            }
            for(int i=count;i<trench.Count;i++)if(trench[i].gameObject.activeSelf)trench[i].gameObject.SetActive(false);
            for(int i=count;i<trenchHigh.Count;i++)if(trenchHigh[i].gameObject.activeSelf)trenchHigh[i].gameObject.SetActive(false);
            shownTrench=count;
        }
        void RenderPlane(bool live,float dt)
        {
            var state=run.PlaneState;
            marker.gameObject.SetActive(live && state!=FireRun.PlaneStage.None);
            bool flying=live && state==FireRun.PlaneStage.Flying;
            if(marker.gameObject.activeSelf)
            {
                marker.localPosition=run.PlaneMarker;marker.localRotation=Quaternion.LookRotation(run.PlaneDirection);
                float pulse=.5f+.5f*Mathf.Sin(clock*(state==FireRun.PlaneStage.Marked?4+6*(1-run.PlaneCountdown/EmberdeepPieceTuning.PlaneDelaySeconds):3));
                markerRing.transform.localScale=new Vector3(.7f+.25f*pulse,1,.7f+.25f*pulse);markerRing.transform.localPosition=Vector3.up*.03f;
                Tint(markerRing,new Color(1,.55f,.2f,.35f+.35f*pulse));
                if(state==FireRun.PlaneStage.Marked){float yaw=Mathf.Atan2(run.PlaneDirection.x,run.PlaneDirection.z)*Mathf.Rad2Deg;PlaceStrip(run.PlaneMarker,yaw,new Color(1,.7f,.3f,.35f+.2f*pulse));}
            }
            if(bomber.gameObject.activeSelf!=flying)bomber.gameObject.SetActive(flying);
            if(!flying)
            {
                if(drone && drone.isPlaying){drone.volume=Mathf.MoveTowards(drone.volume,0,dt*.8f);if(drone.volume<=0)drone.Stop();}
                hadPlane=false;return;
            }
            var at=run.PlanePosition;
            bomber.localPosition=at;bomber.localRotation=Quaternion.LookRotation(run.PlaneDirection)*Quaternion.Euler(0,0,4*Mathf.Sin(clock*2));
            if(propeller)propeller.localRotation=Quaternion.Euler(0,0,clock*1400);
            // Over the board it drops: a falling curtain under the plane and splashes where it lands.
            float head=run.PlaneHead;bool over=false;
            foreach(var t in run.Trees)if(Mathf.Abs(Vector3.Dot(t.Position-run.PlaneMarker,run.PlaneDirection)-head)<1.2f){over=true;break;}
            curtainAlpha=Mathf.MoveTowards(curtainAlpha,over?1:0,dt*4);
            curtain.gameObject.SetActive(curtainAlpha>.01f);
            curtain.localPosition=new Vector3(0,-EmberdeepPieceTuning.PlaneHeight*.5f,-.5f);
            curtain.localScale=new Vector3(EmberdeepPieceTuning.PlaneHalfWidth*2,EmberdeepPieceTuning.PlaneHeight,.9f);
            Tint(curtainRenderer,new Color(.62f,.86f,1,.32f*curtainAlpha*(.8f+.2f*Mathf.Sin(clock*30))));
            if(!hadPlane){lastHead=at;hadPlane=true;}
            if(over && effects && (at-lastHead).sqrMagnitude>.36f)
            {
                lastHead=at;var ground=transform.TransformPoint(new Vector3(at.x,0,at.z));
                effects.Burst(ground,EmberdeepPieceTuning.PlaneHalfWidth*FireMiniature.Scale*1.3f);
            }
            if(drone)drone.pitch=1+.08f*Mathf.Clamp(-head/6,-1,1);
        }

        // ---- Audio, synthesized once -------------------------------------------------------------------------------
        static AudioClip thud,rain,droneClip,engineClip;
        static AudioClip ThudClip=>thud?thud:thud=Synth("Piece set down",.16f,t=>(Mathf.Sin(2*Mathf.PI*180*t)+.6f*Mathf.Sin(2*Mathf.PI*410*t))*Mathf.Exp(-t*32)*.6f*Mathf.Clamp01(t*600));
        static AudioClip RainClip
        {
            get
            {
                if(rain)return rain;var r=new System.Random(3);
                rain=Synth("Tower rain",1.6f,t=>{float n=(float)(r.NextDouble()*2-1);return n*.32f*Mathf.Clamp01(t*8)*Mathf.Exp(-t*1.4f);});return rain;
            }
        }
        // A radial-engine drone with a slow beat: loops while the plane flies.
        static AudioClip DroneClip=>droneClip?droneClip:droneClip=Synth("Plane drone",2,t=>
        {
            float b=Mathf.Sin(2*Mathf.PI*88*t)*.45f+Mathf.Sin(2*Mathf.PI*176*t+Mathf.Sin(2*Mathf.PI*1.5f*t))*.25f+Mathf.Sin(2*Mathf.PI*264*t)*.1f;
            return b*(.8f+.2f*Mathf.Sin(2*Mathf.PI*11*t))*.45f;
        });
        static AudioClip EngineClip=>engineClip?engineClip:engineClip=Synth("Excavator putter",1,t=>
            (Mathf.Sin(2*Mathf.PI*62*t)*.5f+Mathf.Sin(2*Mathf.PI*124*t)*.2f)*(.55f+.45f*Mathf.Pow(Mathf.Abs(Mathf.Sin(2*Mathf.PI*8*t)),4))*.5f);
        static AudioClip Synth(string name,float seconds,System.Func<float,float> wave)
        {
            const int rate=22050;int n=Mathf.CeilToInt(seconds*rate);var data=new float[n];
            for(int i=0;i<n;i++)data[i]=wave(i/(float)rate);
            var clip=AudioClip.Create(name,n,1,rate,false);clip.SetData(data,0);return clip;
        }
        void OnDestroy(){Unbind();if(clay)Destroy(clay);if(veil)Destroy(veil);if(ringMesh)Destroy(ringMesh);}
    }
}

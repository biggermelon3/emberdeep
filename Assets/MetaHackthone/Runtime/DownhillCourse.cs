using System.Collections.Generic;
using UnityEngine;

namespace PhysicalAssembly
{
    public sealed class DownhillCourse : MonoBehaviour
    {
        public WorkshopButton ModeButton;
        public DownhillRotationWheel RotationWheel;
        public Transform WorkbenchFrame;
        public Transform SandboxFrame;
        public Transform StartPoint;
        public BuildingArea FinishArea, PlayArea;
        public bool SandboxPolish;
        public BuildingArea BuildArea;
        [Min(.0001f), Tooltip("Contact margin for this miniature course. Applied at runtime because Collider.contactOffset is not saved in scenes.")]
        public float ContactOffset=.001f;
        public void ReleaseGeneratedMaterialOwnership() { materials.Clear(); meshes.Clear(); }
        readonly List<Mesh> meshes=new();
        Transform geometryParent;
        public TextMesh StatusLabel;
        readonly Dictionary<Color,Material> materials=new();
        public Transform MakeGridCircular(Transform visuals)
        {
            var grid=visuals.Find("Core direction grid") ?? visuals;
            var pad=grid.Find("Centered grid pad");
            if(!pad) return null;
            var filter=pad.GetComponent<MeshFilter>();
            if(filter.sharedMesh && (filter.sharedMesh.name=="Circular build pad" || filter.sharedMesh.name=="CenteredGridPad")) return pad;
            float diameter=Mathf.Max(pad.localScale.x,pad.localScale.z);
            var mesh=DownhillParts.Cylinder(.5f,1);
            var vertices=mesh.vertices;
            for(int i=0;i<vertices.Length;i++) vertices[i]=Quaternion.Euler(0,0,90)*vertices[i];
            mesh.vertices=vertices; mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.name="Circular build pad";
            meshes.Add(mesh); filter.sharedMesh=mesh;
            pad.localScale=new Vector3(diameter,pad.localScale.y,diameter);
            var worktop=visuals.Find("Worktop");
            var top=worktop?worktop.GetComponent<BoxCollider>():null;
            if(top) { var surface=top.transform.TransformPoint(top.center+Vector3.up*top.size.y*.5f);
                pad.position+=WorkbenchFrame.up*(.091f-Vector3.Dot(pad.position-surface,WorkbenchFrame.up)); }
            float radius=diameter*.5f-.008f;
            foreach(Transform line in grid)
            {
                bool x=line.name=="Grid X", z=line.name=="Grid Z";
                if(!x && !z) continue;
                var position=line.localPosition; var scale=line.localScale;
                float offset=x?position.x-pad.localPosition.x:position.z-pad.localPosition.z;
                line.gameObject.SetActive(Mathf.Abs(offset)<radius);
                if(Mathf.Abs(offset)>=radius) continue;
                float length=2*Mathf.Sqrt(radius*radius-offset*offset);
                if(x) { scale.z=length; position.z=pad.localPosition.z; }
                else { scale.x=length; position.x=pad.localPosition.x; }
                position.y=pad.localPosition.y+pad.localScale.y*.5f+.0005f;
                line.localScale=scale; line.localPosition=position;
            }
            var outline=new GameObject("Circular grid border"); outline.transform.SetParent(grid,false);
            outline.transform.localPosition=pad.localPosition+Vector3.up*(pad.localScale.y*.5f+.0005f);
            outline.transform.localScale=new Vector3(diameter,.001f,diameter);
            var rim=DownhillParts.Ring(.5f,.486f,1); vertices=rim.vertices;
            for(int i=0;i<vertices.Length;i++) vertices[i]=Quaternion.Euler(0,0,90)*vertices[i];
            rim.vertices=vertices; rim.RecalculateNormals(); rim.RecalculateBounds(); rim.name="Circular grid outline"; meshes.Add(rim);
            outline.AddComponent<MeshFilter>().sharedMesh=rim;
            outline.AddComponent<MeshRenderer>().sharedMaterial=Mat(SandboxInterface.Cyan);
            return pad;
        }
        public static DownhillCourse Build(DownhillSettings settings,Transform parent)
        {
            var go=new GameObject("Downhill tabletop"); go.transform.SetParent(parent,false);
            var c=go.AddComponent<DownhillCourse>(); c.Construct(settings);
            c.ConfigurePhysics();
            return c;
        }
        public void ConfigurePhysics()
        {
            foreach(var collider in GetComponentsInChildren<Collider>(true)) collider.contactOffset=Mathf.Max(.0001f,ContactOffset);
        }
        Material Mat(Color color)
        {
            if(!materials.TryGetValue(color,out var material)) materials[color]=material=PartFactory.Material(color);
            return material;
        }
        GameObject Box(string name,Vector3 pos,Vector3 size,Color color,bool solid=true)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(geometryParent,false); go.transform.localPosition=pos; go.transform.localScale=size;
            if(!solid) DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial=Mat(color); return go;
        }
        public TextMesh Label(string text,Vector3 pos,float size,Quaternion rotation)
        {
            var go=new GameObject(text); go.transform.SetParent(geometryParent,false); go.transform.localPosition=pos; go.transform.localRotation=rotation;
            var label=go.AddComponent<TextMesh>(); label.text=text; label.anchor=TextAnchor.MiddleCenter; label.alignment=TextAlignment.Center;
            label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize=64; label.characterSize=size; label.color=new Color(.95f,.84f,.62f);
            go.GetComponent<Renderer>().sharedMaterial=label.font.material; return label;
        }
        void Construct(DownhillSettings s)
        {
            var wood=new Color(.57f,.31f,.13f); var light=new Color(.86f,.61f,.32f); var edge=new Color(.32f,.16f,.07f);
            WorkbenchFrame=new GameObject("Centered workbench frame").transform; WorkbenchFrame.SetParent(transform,false);
            SandboxFrame=new GameObject("Sandbox frame coordinates").transform; SandboxFrame.SetParent(transform,false); SandboxFrame.localPosition=s.SandboxOffset;
            geometryParent=WorkbenchFrame;
            var dark=new Color(.085f,.14f,.19f); var brass=new Color(.72f,.48f,.24f); var tray=new Color(.14f,.22f,.27f);
            Box("Worktop",new Vector3(0,s.SurfaceHeight-s.WorkbenchSize.y*.5f,0),s.WorkbenchSize,dark);
            for(int side=-1;side<=1;side+=2)
            {
                Box("Brass edge",new Vector3(0,s.SurfaceHeight-.009f,side*(s.WorkbenchSize.z*.5f-.007f)),new Vector3(s.WorkbenchSize.x,.018f,.014f),brass,false);
                for(int end=-1;end<=1;end+=2)
                    Box("Workbench leg",new Vector3(side*(s.WorkbenchSize.x*.5f-.08f),(s.SurfaceHeight-s.WorkbenchSize.y)*.5f,end*(s.WorkbenchSize.z*.5f-.08f)),new Vector3(.055f,s.SurfaceHeight-s.WorkbenchSize.y,.055f),dark);
            }
            Box("Centered grid pad",new Vector3(0,s.SurfaceHeight+.001f,0),new Vector3(s.GridSize.x,.002f,s.GridSize.y),new Color(.16f,.25f,.29f),false);
            for(int i=-4;i<=4;i++)
            {
                Box("Grid X",new Vector3(i*.05f,s.SurfaceHeight+.0025f,0),new Vector3(.001f,.001f,s.GridSize.y-.03f),new Color(.23f,.37f,.39f),false);
                Box("Grid Z",new Vector3(0,s.SurfaceHeight+.0025f,i*.05f),new Vector3(s.GridSize.x-.04f,.001f,.001f),new Color(.23f,.37f,.39f),false);
            }
            for(int i=1;i<8;i++)
            {
                var pos=s.KitPosition(i); pos.y=s.SurfaceHeight+.005f;
                var size=i<3?new Vector3(.29f,.01f,.12f):i<5?new Vector3(.20f,.01f,.12f):new Vector3(.19f,.01f,.23f);
                Box(i<3?"Large wheel tray":i<5?"Small wheel tray":"Block tray",pos,size,tray);
            }
            Label("LARGE",new Vector3(s.LargeTrayOffset.x,s.SurfaceHeight+.006f,-.26f),.009f,Quaternion.Euler(90,0,0));
            Label("SMALL",new Vector3(s.SmallTrayOffset.x,s.SurfaceHeight+.006f,-.26f),.009f,Quaternion.Euler(90,0,0));
            EnsureControls(s);
            geometryParent=SandboxFrame;
            Box("Sandbox frame",new Vector3(0,.66f,.65f),new Vector3(2.85f,.10f,.78f),wood);
            Box("Decorative stream",new Vector3(0,.716f,.65f),new Vector3(2.76f,.012f,.70f),new Color(.09f,.42f,.48f),false);
            for(int side=-1;side<=1;side+=2) Box("Sandbox edge",new Vector3(0,.74f,.65f+side*.38f),new Vector3(2.85f,.10f,.025f),light);
            Vector3 a=new(-1.35f,1.2565f,.65f),b=new(-.10f,.819f,.65f); var delta=b-a;
            var ramp=Box("Starting downhill ramp",(a+b)*.5f-Vector3.up*.018f,new Vector3(delta.magnitude,.036f,.64f),light);
            ramp.transform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
            for(int i=0;i<5;i++) Box("Ramp support",new Vector3(-1.30f+i*.24f,.78f+i*-.005f,.65f),new Vector3(.035f,.45f-i*.075f,.29f),wood);
            Box("Rocky path",new Vector3(.075f,.8f,.65f),new Vector3(.35f,.038f,.64f),light);
            Box("Wheel size barrier",new Vector3(.075f,.819f+s.BarrierHeight*.5f,.65f),new Vector3(.018f,s.BarrierHeight,.64f),new Color(.42f,.40f,.35f));
            for(int i=0;i<4;i++)
            {
                var rock=Box("Road stone",new Vector3(-.055f+i*.06f,.823f,.65f+(i%2==0?-.28f:.28f)),new Vector3(.034f,.010f,.037f),new Color(.48f,.47f,.43f)); rock.transform.localRotation=Quaternion.Euler(0,i*31,3);
            }
            float gapStart=.50f,gapEnd=gapStart+s.GapWidth;
            void Planks(float start,float stop)
            {
                var support=new GameObject("Bridge collision surface"); support.transform.SetParent(SandboxFrame,false);
                support.transform.localPosition=new Vector3((start+stop)*.5f,.802f,.65f);
                support.AddComponent<BoxCollider>().size=new Vector3(stop-start,.034f,.64f);
                for(float x=start;x<stop;x+=.045f)
                {
                    float end=Mathf.Min(x+.045f,stop);
                    Box("Bridge plank",new Vector3((x+end)*.5f,.802f,.65f),new Vector3(end-x,.034f,.64f),iColor(x),false);
                    Box("Plank seam",new Vector3(x,.820f,.65f),new Vector3(.002f,.001f,.64f),edge,false);
                }
            }
            Planks(.25f,gapStart); Planks(gapEnd,.72f);
            Color iColor(float x)=>Mathf.FloorToInt(x*100)%2==0?wood:light;
            Box("Finish road",new Vector3(1.055f,.80f,.65f),new Vector3(.67f,.038f,.64f),light);
            for(int side=-1;side<=1;side+=2) Box("Flag post",new Vector3(1.32f,1.0f,.65f+side*.19f),new Vector3(.016f,.38f,.016f),edge,false);
            Box("Finish banner",new Vector3(1.32f,1.12f,.65f),new Vector3(.012f,.12f,.38f),new Color(.78f,.12f,.12f),false);
            for(int i=0;i<6;i++) Box("Flag check",new Vector3(1.311f,1.10f+(i%2)*.04f,.50f+(i/2)*.10f),new Vector3(.004f,.04f,.05f),Color.white,false);
            Label("START",new Vector3(-1.22f,1.32f,.80f),.012f,Quaternion.identity);
            Label("DOWNHILL SANDBOX",new Vector3(0,1.72f,1.03f),.022f,Quaternion.identity);
            StatusLabel=Label("BUILD • TURN WHEEL • PRESS START",new Vector3(0,1.52f,1.03f),.010f,Quaternion.identity);
            // Low-cost miniature scenery outside the playable lane.
            for(int i=0;i<16;i++)
            {
                float x=-1.3f+i*.17f,z=.65f+(i%2==0?-.30f:.30f);
                var shrub=GameObject.CreatePrimitive(PrimitiveType.Sphere); shrub.name="Miniature shrub"; DestroyImmediate(shrub.GetComponent<Collider>());
                shrub.transform.SetParent(SandboxFrame,false); shrub.transform.localPosition=new Vector3(x,.78f,z); shrub.transform.localScale=new Vector3(.085f,.075f,.065f);
                shrub.GetComponent<Renderer>().sharedMaterial=Mat(i%3==0?new Color(.48f,.57f,.17f):new Color(.20f,.38f,.14f));
                Box("Bank stone",new Vector3(x+.04f,.75f,z),new Vector3(.05f,.06f,.04f),new Color(.41f,.44f,.43f),false);
            }
        }
        public void EnsureControls(DownhillSettings settings)
        {
            if(ModeButton && RotationWheel) return;
            var previous=geometryParent; geometryParent=WorkbenchFrame;
            var worktop=WorkbenchFrame.Find("Worktop") ?? WorkbenchFrame.Find("Workbench visuals/Worktop");
            var top=worktop?worktop.GetComponent<BoxCollider>():null;
            var pad=WorkbenchFrame.Find("Workbench visuals/Core direction grid/Centered grid pad");
            float height=top?WorkbenchFrame.InverseTransformPoint(top.transform.TransformPoint(top.center+Vector3.up*top.size.y*.5f)).y:pad?WorkbenchFrame.InverseTransformPoint(pad.position).y-.091f:settings.SurfaceHeight;
            if(!ModeButton)
            {
                var button=Box("Start Return",new Vector3(.50f,height+.016f,-.38f),new Vector3(.16f,.09f,.025f),new Color(.08f,.50f,.36f));
                button.transform.localRotation=Quaternion.Euler(90,0,0);
                button.GetComponent<Collider>().isTrigger=true;
                ModeButton=button.AddComponent<WorkshopButton>(); ModeButton.Command="Downhill Start Return";
                var label=Label("START",Vector3.zero,.009f,Quaternion.identity);
                label.transform.SetParent(button.transform,false); label.transform.localPosition=new Vector3(0,0,-.55f);
                label.transform.localScale=new Vector3(1/.16f,1/.09f,1/.025f);
            }
            if(!RotationWheel)
                RotationWheel=DownhillRotationWheel.Create(WorkbenchFrame,new Vector3(settings.CorePosition.x,height+.04f,settings.CorePosition.z),Mat(new Color(.08f,.66f,.60f)));
            geometryParent=previous;
        }
        void OnDestroy()
        {
            foreach(var material in materials.Values) if(material) { if(Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
            foreach(var mesh in meshes) if(mesh) { if(Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        }
    }
}



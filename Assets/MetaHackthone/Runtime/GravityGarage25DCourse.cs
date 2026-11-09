using System;
using System.Collections.Generic;
using UnityEngine;

namespace PhysicalAssembly
{
    public sealed class GravityGarage25DCourse : MonoBehaviour
    {
        public static float UnitScale => 3*BuildingGeometry.Unit/.98f;
        public const float TrackHalfWidth=1.2f;
        public GravityGarage25DParameters Parameters { get; private set; }
        public float Scale { get; private set; }
        public Collider UpperLanding { get; private set; }
        public Collider LowerRoad { get; private set; }
        public Collider LowerLanding { get; private set; }
        public bool DrillCourse { get; private set; }
        public GameObject Sandstone { get; private set; }
        public SandboxWater Water { get; private set; }
        public const float LowerGapStart=1.6f+770f/1090f*15.75f, LowerGapEnd=1.6f+828f/1090f*15.75f;
        public float LowerSurface(float x) {
            if(!DrillCourse)return Parameters.Lower(x);
            float px=(x-1.6f)/15.75f*1090f;
            float drop=px<310?Mathf.Max(0,px)*.31f:px<590?96.1f:px<770?96.1f+(px-590)*.29f:158.3f;
            if(x>=LowerGapStart && x<LowerGapEnd)return float.NaN;
            return 2.3f-drop*(1.74f/158.3f);
        }
        public void ResetSandstone() { if(Sandstone)Sandstone.SetActive(true); foreach(var effect in GetComponents<SandboxDrillBurst>())Destroy(effect); }

        public readonly List<TextMesh> Labels=new();
        readonly List<UnityEngine.Object> resources=new();
        public Vector3 Point(float x,float y,float z) => transform.TransformPoint(new Vector3(x-9,y,-z)*Scale);
        public Vector3 Coordinates(Vector3 p) {var local=transform.InverseTransformPoint(p)/Scale;return new Vector3(local.x+9,local.y,-local.z);}
        public float WorldScale => Scale*transform.lossyScale.x;
        public Quaternion Heading(float x,bool lower=false)
        {
            var f=lower?(Func<float,float>)LowerSurface:Parameters.Upper;
            float a=f(x-.01f),b=f(x+.01f); float slope=float.IsNaN(a)||float.IsNaN(b)?0:(b-a)/.02f;
            return Quaternion.LookRotation(transform.TransformDirection(new Vector3(1,slope,0)),transform.up);
        }
        public void Build(GravityGarage25DParameters parameters,float scale,bool drillCourse=false)
        {
            DrillCourse=drillCourse; parameters.Validate(); Parameters=parameters.Copy(); Scale=scale;
            var wood=Material(new Color(.72f,.50f,.27f)); var edge=Material(new Color(.79f,.55f,.28f));
            var red=Material(new Color(.72f,.17f,.13f)); var dark=Material(new Color(.14f,.22f,.21f));
            var physics=new PhysicsMaterial("25D timber grip"){staticFriction=.65f,dynamicFriction=.55f,bounciness=0,frictionCombine=PhysicsMaterialCombine.Minimum};resources.Add(physics);
            Road("Upper descent",.65f,9.55f,-1.55f,parameters.Upper,wood,edge,physics);
            UpperLanding=Road("Upper landing",parameters.LandingX,16.35f,-1.55f,parameters.Upper,wood,edge,physics);
            if(DrillCourse) {
                LowerRoad=Road("Lower descent and sandstone",1.6f,LowerGapStart,2.25f,LowerSurface,wood,edge,physics);
                LowerLanding=Road("Lower landing",LowerGapEnd,17.35f,2.25f,LowerSurface,wood,edge,physics);
                float rx=1.6f+555f/1090f*15.75f, h=78f*1.74f/158.3f;
                Sandstone=Primitive("Drill sandstone",PrimitiveType.Cube,new Vector3(rx,LowerSurface(rx)+h*.5f,2.25f),new Vector3(.42f,h,TrackHalfWidth*2),Material(new Color(.64f,.48f,.32f)));
                Sandstone.AddComponent<BoxCollider>();
            } else LowerLanding=LowerRoad=Road("Lower track",1.6f,17.35f,2.25f,LowerSurface,wood,edge,physics);
            Rock("Rock A",parameters.SupportedRockX,parameters.Upper(parameters.SupportedRockX),-1.55f,parameters.rockSize,physics);
            if(!DrillCourse && parameters.rock2Enabled)Rock("Rock B",parameters.rock2Pos,LowerSurface(parameters.rock2Pos),2.25f,parameters.rock2Size,physics);
            if(DrillCourse) { Water=gameObject.AddComponent<SandboxWater>(); Water.Initialize(this); }
            else { Box("Diorama table",new Vector3(9,-.38f,0),new Vector3(19,.4f,8),wood);
            for(float x=0;x<19;x+=.7f)Box("Table seam",new Vector3(x,-.171f,0),new Vector3(.018f,.003f,8),dark); }
            // Pipes are deliberate transport visuals; they do not block the vehicle.
            float inletY=parameters.Upper(16.35f)+.45f;
            Tube("Upper inlet",new Vector3(16.5f,inletY,-1.55f),Vector3.right,.58f,.8f,red);
            Bend("Upper elbow",new Vector3(16.9f,inletY-.5f,-1.55f),true,red);
            Tube("Pipe drop",new Vector3(17.4f,(inletY-.6f)*.5f,-1.55f),Vector3.up,.49f,inletY-.4f,red);
            Tube("Lower outlet",new Vector3(1.35f,2.7f,2.25f),Vector3.right,.58f,.9f,red);
            Bend("Lower elbow",new Vector3(.9f,2.2f,2.25f),false,red);
            Tube("Lower riser",new Vector3(.4f,1.05f,2.25f),Vector3.up,.49f,2.3f,red);
            Label("ROCK A",parameters.SupportedRockX,parameters.Upper(parameters.SupportedRockX)+parameters.rockSize*2+.35f,-1.55f);
            Label("JUMP",9.25f,parameters.Upper(9.25f)+.55f,-1.55f);Label("PIPE",15.3f,parameters.Upper(15.3f)+.65f,-1.55f);
            Label("LOWER TRACK",4,LowerSurface(4)+.6f,2.25f);
            var heading=Quaternion.LookRotation(transform.right,transform.up);
            CourseArchVisuals.Place(transform,true,Point(2.8f,parameters.Upper(2.8f),-1.55f),heading,3f*WorldScale);
            CourseArchVisuals.Place(transform,false,Point(16.8f,LowerSurface(16.8f),2.25f),heading,3f*WorldScale);
            var leaves=Material(new Color(.27f,.46f,.31f));
            foreach(var p in new[]{new Vector3(2,0,-3.2f),new Vector3(6,0,3.5f),new Vector3(12,0,-3.3f),new Vector3(18,0,3.4f)}) {
                Box("Tree trunk",p+Vector3.up*.45f,new Vector3(.12f,.9f,.12f),wood);
                var tree=Primitive("Tree canopy",PrimitiveType.Sphere,p+Vector3.up*1.1f,new Vector3(.75f,1.3f,.75f),leaves);
            }
            SetLabels(parameters.showLabels); Physics.SyncTransforms();
        }
        Material Material(Color color) { var m=PartFactory.Material(color);resources.Add(m);return m; }
        GameObject Primitive(string name,PrimitiveType type,Vector3 at,Vector3 size,Material material)
        {
            var o=GameObject.CreatePrimitive(type);o.name=name;DestroyImmediate(o.GetComponent<Collider>());o.transform.SetParent(transform,false);
            o.transform.localPosition=new Vector3(at.x-9,at.y,-at.z)*Scale;o.transform.localScale=size*Scale;o.GetComponent<Renderer>().sharedMaterial=material;return o;
        }
        void Box(string name,Vector3 at,Vector3 size,Material m) => Primitive(name,PrimitiveType.Cube,at,size,m);
        void Tube(string name,Vector3 at,Vector3 axis,float radius,float length,Material m)
        {
            var o=new GameObject(name);o.transform.SetParent(transform,false);o.transform.position=Point(at.x,at.y,at.z);o.transform.rotation=transform.rotation*Quaternion.FromToRotation(Vector3.right,axis);
            var mesh=DownhillParts.Ring(radius*Scale,(radius-.12f)*Scale,length*Scale);resources.Add(mesh);o.AddComponent<MeshFilter>().sharedMesh=mesh;o.AddComponent<MeshRenderer>().sharedMaterial=m;
        }
        void Bend(string name,Vector3 center,bool upper,Material material)
        {
            Vector3 At(float t){float a=t*Mathf.PI*.5f;return center+new Vector3((upper?1:-1)*Mathf.Sin(a)*.5f,Mathf.Cos(a)*.5f,0);}
            for(int i=0;i<12;i++){var a=At(i/12f);var b=At((i+1)/12f);Tube(name,(a+b)*.5f,(b-a).normalized,.49f,Vector3.Distance(a,b)+.008f,material);}
        }
        Collider Road(string name,float start,float end,float z,Func<float,float> height,Material wood,Material edge,PhysicsMaterial physics)
        {
            var v=new List<Vector3>();var t=new List<int>();int count=Mathf.CeilToInt((end-start)/.04f);
            for(int i=0;i<=count;i++) {float x=Mathf.Lerp(start,end,(float)i/count),h=height(x);if(float.IsNaN(h))h=height(x-.001f);v.Add(new Vector3(x-9,h,-z-TrackHalfWidth)*Scale);v.Add(new Vector3(x-9,h,-z+TrackHalfWidth)*Scale);v.Add(new Vector3(x-9,h-.21f,-z-TrackHalfWidth)*Scale);v.Add(new Vector3(x-9,h-.21f,-z+TrackHalfWidth)*Scale);}
            void Quad(int a,int b,int c,int d){t.AddRange(new[]{a,b,c,a,c,d});}
            for(int i=0;i<count;i++){int k=i*4;Quad(k,k+1,k+5,k+4);Quad(k+2,k,k+4,k+6);Quad(k+1,k+3,k+7,k+5);Quad(k+3,k+2,k+6,k+7);}
            Quad(0,2,3,1);int last=count*4;Quad(last,last+1,last+3,last+2);
            var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();resources.Add(mesh);
            var o=new GameObject(name);o.transform.SetParent(transform,false);o.AddComponent<MeshFilter>().sharedMesh=mesh;o.AddComponent<MeshRenderer>().sharedMaterial=wood;
            var collider=o.AddComponent<MeshCollider>();collider.sharedMesh=mesh;collider.sharedMaterial=physics;collider.contactOffset=.001f*WorldScale;
            // Thin painted seams follow the deck and have no colliders.
            for(float x=start+.25f;x<end;x+=.62f)Box("Plank seam",new Vector3(x,height(x)+.001f,z),new Vector3(.012f,.002f,TrackHalfWidth*2-.02f),edge);
            for(float x=start+.4f;x<end;x+=2.25f)for(int side=-1;side<=1;side+=2){float h=height(x)-.21f;Box("Timber support",new Vector3(x,h*.5f,z+side*.65f),new Vector3(.14f,h,.14f),edge);}
            return collider;
        }
        void Rock(string name,float x,float y,float z,float radius,PhysicsMaterial physics)
        {
            var o=new GameObject(name);o.transform.SetParent(transform,false);o.transform.position=Point(x,y+radius,z);o.transform.localRotation=Quaternion.Euler(0,90,0);
            var mesh=StoneMesh(radius);resources.Add(mesh);o.AddComponent<MeshFilter>().sharedMesh=mesh;o.AddComponent<MeshRenderer>().sharedMaterial=Material(new Color(.53f,.56f,.52f));
            var c=o.AddComponent<MeshCollider>();c.sharedMesh=mesh;c.convex=true;var stone=new PhysicsMaterial("25D smooth stone"){staticFriction=.12f,dynamicFriction=.12f,frictionCombine=PhysicsMaterialCombine.Minimum,bounciness=0};resources.Add(stone);c.sharedMaterial=stone;c.contactOffset=.001f*WorldScale;
        }
        Mesh StoneMesh(float radius)
        {
            // Beveled ends give the stone a broad, faceted silhouette. Both wheel tracks
            // meet the same circular profile used by the HTML's radius parameter.
            const int count=64;var vertices=new List<Vector3>();var triangles=new List<int>();
            float[] positions={-.56f,-.46f,.46f,.56f},radii={.45f,1,1,.45f};
            Vector3 P(int ring,int side){float angle=side*Mathf.PI*2/count;return new Vector3(positions[ring],Mathf.Cos(angle)*radius*radii[ring],Mathf.Sin(angle)*radius*radii[ring])*Scale;}
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){int k=vertices.Count;vertices.AddRange(new[]{a,b,c,d});triangles.AddRange(new[]{k,k+2,k+1,k,k+3,k+2});}
            for(int ring=0;ring<3;ring++)for(int side=0;side<count;side++)Quad(P(ring,side),P(ring+1,side),P(ring+1,side+1),P(ring,side+1));
            for(int side=0;side<count;side++){Quad(P(3,side),new Vector3(.56f,0,0)*Scale,new Vector3(.56f,0,0)*Scale,P(3,side+1));Quad(new Vector3(-.56f,0,0)*Scale,P(0,side),P(0,side+1),new Vector3(-.56f,0,0)*Scale);}
            var mesh=new Mesh{name="Faceted garage stone"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        void Flag(float x,float y,float z,Material cloth) {var pole=Material(new Color(.81f,.74f,.58f));Box("Flag pole",new Vector3(x,y+.5f,z),new Vector3(.035f,1,.035f),pole);Box("Flag",new Vector3(x+.18f,y+.85f,z),new Vector3(.36f,.25f,.015f),cloth);}
        void Label(string text,float x,float y,float z)
        {
            var o=new GameObject(text);o.transform.SetParent(transform,false);o.transform.position=Point(x,y,z);var label=o.AddComponent<TextMesh>();label.text=text;label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=48;label.characterSize=.075f*Scale;label.anchor=TextAnchor.MiddleCenter;label.color=new Color(.92f,.97f,.91f);label.GetComponent<Renderer>().sharedMaterial=label.font.material;Labels.Add(label);
        }
        public void SetLabels(bool visible) {foreach(var label in Labels)if(label)label.gameObject.SetActive(visible);}
        public void FrameCamera(Camera camera,float angle,float zoom)
        {
            float yaw=angle*Mathf.Deg2Rad;
            var center=Point(9,2.6f,.25f);var offset=new Vector3(-3-Mathf.Sin(yaw)*6,11,-24);
            camera.transform.SetPositionAndRotation(center+transform.TransformDirection(offset)*WorldScale,Quaternion.LookRotation(-transform.TransformDirection(offset),transform.up));
            camera.orthographic=true;camera.orthographicSize=Mathf.Max(5.9f,10f/Mathf.Max(.4f,camera.aspect))*WorldScale*.94f/zoom;camera.nearClipPlane=.01f;camera.farClipPlane=100;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.23f,.20f);
            foreach(var label in Labels)if(label)label.transform.rotation=camera.transform.rotation;
        }
        void OnDestroy() {foreach(var r in resources)if(r){if(Application.isPlaying)Destroy(r);else DestroyImmediate(r);}resources.Clear();}
    }
}

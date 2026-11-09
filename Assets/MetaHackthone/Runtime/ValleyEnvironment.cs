using System.Collections.Generic;
using UnityEngine;

namespace PhysicalAssembly
{
    // Scenery owns no colliders. Each material is combined into one static mesh.
    public sealed class ValleyEnvironment : MonoBehaviour
    {
        readonly List<Object> resources=new();
        readonly Dictionary<Material,List<CombineInstance>> batches=new();
        [SerializeField] List<Transform> foam=new();
        public static readonly Vector3[] WaterPath={new(-2,8.7f,10),new(-2,8.7f,7.35f),new(-2,1.5f,7.35f),new(-2,1.5f,6.2f),new(-.5f,.8f,4.5f),new(1,.25f,1.3f),new(0,-.16f,-.5f),new(-8,-.16f,-.5f)};
        Vector3[] flow=>WaterPath;
        Mesh cube,sphere,cone,mountain;Material water;
        void Start()
        {
            // Authored scenery remains editable as separate pieces. Batch only its
            // stationary visuals in players; foam, water and colliders are untouched.
            var groups=new Dictionary<Material,List<CombineInstance>>();
            var sources=new List<MeshRenderer>();
            foreach(var r in GetComponentsInChildren<MeshRenderer>(true)) {
                if(!r.name.StartsWith("Scenery piece ") || !r.enabled || r.sharedMaterials.Length!=1)continue;
                var filter=r.GetComponent<MeshFilter>();if(!filter || !filter.sharedMesh || !r.sharedMaterial)continue;
                if(!groups.TryGetValue(r.sharedMaterial,out var items))groups.Add(r.sharedMaterial,items=new());
                items.Add(new CombineInstance{mesh=filter.sharedMesh,transform=transform.worldToLocalMatrix*r.transform.localToWorldMatrix});sources.Add(r);
            }
            foreach(var group in groups){var mesh=new Mesh{name="Authored valley batch",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.CombineMeshes(group.Value.ToArray());resources.Add(mesh);
                var go=new GameObject("Batched authored scenery");go.transform.SetParent(transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=group.Key;}
            foreach(var r in sources)r.enabled=false;
        }
        public void Build(GravityGarage3DCourse course)
            => Build(course,null);
        public void Build(GravityGarageDesign design)
            => Build(null,design);
        void Build(GravityGarage3DCourse course,GravityGarageDesign design)
        {
            cube=Primitive(PrimitiveType.Cube);sphere=Primitive(PrimitiveType.Sphere);cone=Cone();mountain=Mountain();
            var timber=Material(new Color(.27f,.16f,.08f));var stone=Material(new Color(.30f,.36f,.35f));var grass=Material(new Color(.27f,.41f,.20f));var pine=Material(new Color(.09f,.24f,.18f));var distant=Material(new Color(.35f,.48f,.49f));
            Add(cube,stone,new Vector3(0,-.6f,course?2.5f:3.8f),new Vector3(25,.35f,course?15:12.4f),Quaternion.identity);
            if(course) {
            for(float d=0;d<course.Length;d+=.3f){var s=course.SampleLocal(d);var rotation=Quaternion.LookRotation(s.Tangent,s.Normal);Add(cube,timber,s.Position-Vector3.up*.07f,new Vector3(course.Level.DeckWidth,.14f,.025f),rotation);}
            for(float d=.5f;d<course.Length;d+=1.8f){var s=course.SampleLocal(d);var side=Vector3.Cross(Vector3.up,s.Tangent).normalized;foreach(int sign in new[]{-1,1}){var p=s.Position+side*(sign*course.Level.DeckWidth*.43f);Add(cube,timber,new Vector3(p.x,p.y*.5f-.1f,p.z),new Vector3(.16f,p.y+.2f,.16f),Quaternion.identity);}Add(cube,timber,s.Position-Vector3.up*.25f,new Vector3(course.Level.DeckWidth+.3f,.23f,.2f),Quaternion.LookRotation(s.Tangent,s.Normal));}
            } else BuildFirstCourseTimber(design,timber);
            for(int i=0;i<12;i++){float x=-11+i*2.1f;float h=3+(i%4)*.7f;Add(sphere,stone,new Vector3(x,h*.42f,8.4f),new Vector3(3.3f,h,3.5f),Quaternion.Euler(0,i*37,0));Add(sphere,grass,new Vector3(x,h*.83f,8.4f),new Vector3(3.2f,.35f,3.2f),Quaternion.identity);if(Mathf.Abs(x+2)>2)Tree(new Vector3(x,h*.93f,8.4f),1.5f+(i%3)*.35f,timber,pine);}
            Add(cube,stone,new Vector3(-2,4.2f,9.5f),new Vector3(3.5f,8.5f,4),Quaternion.identity);Add(cube,grass,new Vector3(-2,8.55f,9.5f),new Vector3(3.7f,.22f,4.2f),Quaternion.identity);
            for(int i=0;i<8;i++)Add(mountain,distant,new Vector3(-14+i*4,0,15+(i%2)*2),new Vector3(7,8+(i%3)*2,6),Quaternion.Euler(0,i*51,0));
            for(int side=course?-1:1;side<=1;side+=2)for(int i=0;i<6;i++){var p=new Vector3(-9+i*3.7f,-.35f,side*5.5f);Add(sphere,stone,p,new Vector3(3.5f,.9f,1.8f),Quaternion.identity);Add(sphere,grass,p+Vector3.up*.35f,new Vector3(3.3f,.25f,1.6f),Quaternion.identity);if(side>0&&i!=2)Tree(p+Vector3.up*.5f,1.5f,timber,pine);}
            foreach(var batch in batches){
                if(!Application.isPlaying) {
                    int i=0;
                    foreach(var item in batch.Value) {
                        var piece=new GameObject("Scenery piece "+i++);piece.transform.SetParent(transform,false);
                        piece.transform.localPosition=item.transform.GetColumn(3);piece.transform.localRotation=item.transform.rotation;piece.transform.localScale=item.transform.lossyScale;
                        piece.AddComponent<MeshFilter>().sharedMesh=item.mesh;piece.AddComponent<MeshRenderer>().sharedMaterial=batch.Key;
                    }
                    continue;
                }
                var go=new GameObject("Combined valley scenery");go.transform.SetParent(transform,false);var mesh=new Mesh{name="Valley static batch",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.CombineMeshes(batch.Value.ToArray());resources.Add(mesh);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=batch.Key;}
            water=new Material(Resources.Load<Shader>("ValleyWater"));resources.Add(water);
            WaterRibbon("Source pool, spillway, curtain and descending stream",flow,new[]{1.3f,1.05f,1.05f,1.6f,.7f,.6f,.65f,.65f});
            float riverWidth=course?course.Level.Radius+course.Level.DeckWidth*.5f+.5f:2.1f;
            WaterRibbon("River beneath the bridge",new[]{new Vector3(12,-.18f,-.5f),new Vector3(-12,-.18f,-.5f)},new[]{riverWidth,riverWidth});
            var white=Material(new Color(.72f,.92f,.89f));
            for(int i=0;i<64;i++){var go=new GameObject("Downstream foam");go.transform.SetParent(transform,false);go.AddComponent<MeshFilter>().sharedMesh=cube;go.AddComponent<MeshRenderer>().sharedMaterial=white;foam.Add(go.transform);}
        }
        void BuildFirstCourseTimber(GravityGarageDesign design,Material timber)
        {
            var points=design.Points;
            for(int i=0;i<points.Length-1;i++) {
                if(i==5) continue; // Leave the gameplay jump open.
                var a=new Vector3(points[i].x-7.39f,design.LandingY-points[i].y,0);
                var b=new Vector3(points[i+1].x-7.39f,design.LandingY-points[i+1].y,0);
                var tangent=(b-a).normalized;var normal=new Vector3(-tangent.y,tangent.x,0);
                var rotation=Quaternion.LookRotation(tangent,normal);
                for(float d=.08f;d<Vector3.Distance(a,b);d+=.3f)
                    Add(cube,timber,a+tangent*d+normal*.001f,new Vector3(2.4f,.002f,.018f),rotation);
                foreach(int side in new[]{-1,1})
                    Add(cube,timber,(a+b)*.5f-normal*.14f+Vector3.forward*side*1.05f,new Vector3(.14f,.22f,Vector3.Distance(a,b)),rotation);
                for(float d=.25f;d<Vector3.Distance(a,b);d+=1.5f) {
                    var p=a+tangent*d;float height=p.y+.4f;
                    foreach(int side in new[]{-1,1})Add(cube,timber,new Vector3(p.x,p.y-height*.5f-.08f,side*1.05f),new Vector3(.16f,height,.16f),Quaternion.identity);
                    Add(cube,timber,p-normal*.25f,new Vector3(2.65f,.20f,.16f),rotation);
                }
            }
        }
        Mesh Mountain()
        {
            var mesh=new Mesh();var v=new List<Vector3>();var t=new List<int>();var corners=new[]{new Vector3(-.5f,0,-.4f),new Vector3(.4f,0,-.5f),new Vector3(.6f,0,.4f),new Vector3(-.4f,0,.5f)};
            for(int i=0;i<4;i++){int n=v.Count;v.Add(corners[i]);v.Add(new Vector3(.1f,1,-.1f));v.Add(corners[(i+1)%4]);t.AddRange(new[]{n,n+1,n+2});}mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();resources.Add(mesh);return mesh;
        }
        void Tree(Vector3 p,float height,Material trunk,Material leaves){Add(cube,trunk,p+Vector3.up*height*.4f,new Vector3(.12f,height*.8f,.12f),Quaternion.identity);for(int i=0;i<3;i++)Add(cone,leaves,p+Vector3.up*(height*.25f+i*height*.22f),new Vector3(height*(.7f-i*.13f),height*.6f,height*(.7f-i*.13f)),Quaternion.identity);}
        Material Material(Color color){var m=PartFactory.Material(color);resources.Add(m);return m;}
        Mesh Primitive(PrimitiveType type){var go=GameObject.CreatePrimitive(type);var mesh=go.GetComponent<MeshFilter>().sharedMesh;DestroyImmediate(go);return mesh;}
        Mesh Cone(){var v=new List<Vector3>();var t=new List<int>();for(int i=0;i<10;i++){float a=i*Mathf.PI*.2f;v.Add(new Vector3(Mathf.Cos(a)*.5f,0,Mathf.Sin(a)*.5f));}v.Add(Vector3.up);v.Add(Vector3.zero);for(int i=0;i<10;i++){t.AddRange(new[]{i,10,(i+1)%10,11,i,(i+1)%10});}var mesh=new Mesh();mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();resources.Add(mesh);return mesh;}
        void Add(Mesh mesh,Material material,Vector3 p,Vector3 scale,Quaternion rotation){if(!batches.TryGetValue(material,out var batch))batches.Add(material,batch=new());batch.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(p,rotation,scale)});}
        void WaterRibbon(string name,Vector3[] path,float[] widths)
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();float distance=0;
            for(int i=0;i<path.Length;i++){if(i>0)distance+=Vector3.Distance(path[i-1],path[i]);for(int side=-1;side<=1;side+=2){Vector3 across=name.StartsWith("River")?Vector3.forward:Vector3.right;v.Add(path[i]+across*side*widths[i]);uv.Add(new Vector2((side+1)*.5f,distance));}if(i>0){int a=(i-1)*2;indices.AddRange(new[]{a,a+1,a+2,a+1,a+3,a+2});}}
            var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();resources.Add(mesh);var go=new GameObject(name);go.transform.SetParent(transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=water;
        }
        void Update()
        {
            for(int i=0;i<foam.Count;i++){if(i>=48){float age=Mathf.Repeat(Time.time*.85f+i*.618f,1);float angle=i*2.4f;foam[i].localPosition=new Vector3(-2+Mathf.Cos(angle)*age*.7f,1.55f+Mathf.Sin(age*Mathf.PI)*.45f,6.5f+Mathf.Sin(angle)*age*.5f);foam[i].localScale=Vector3.one*(.045f*(1-age));continue;}float t=Mathf.Repeat(i/(float)foam.Count+Time.time*.035f,1)*(flow.Length-1);int segment=Mathf.Min((int)t,flow.Length-2);var p=Vector3.Lerp(flow[segment],flow[segment+1],t-segment);p.x+=Mathf.Sin(i*17)*.65f;foam[i].localPosition=p+Vector3.up*.025f;foam[i].localScale=new Vector3(.12f,.025f,.07f);}
        }
        public void ReleaseResourceOwnership() => resources.Clear();
        void OnDestroy(){foreach(var resource in resources)if(resource){if(Application.isPlaying)Destroy(resource);else DestroyImmediate(resource);}}
    }
}

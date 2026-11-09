using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PhysicalAssembly
{
    public sealed class GravityGarage3DCourse : MonoBehaviour
    {
        [field: SerializeField] public GravityGarage3DLevelAsset Level { get; private set; }
        public float WorldScale => transform.lossyScale.x;
        public float Length => distances[distances.Count-1];
        public float TurnStart,LowerStart,WallDistance;
        [field: SerializeField] public GameObject Wall { get; private set; }
        public bool WallBroken { get; private set; }
        [field: SerializeField] public Vector3 WeakPoint { get; private set; }
        [field: SerializeField] public Collider Road { get; private set; }
        [SerializeField] public List<Bounds> WaterVolumes=new();
        [SerializeField] List<Vector3> points=new();
        [SerializeField] List<float> distances=new();
        readonly List<Object> resources=new();
        public Vector3 Coordinates(Vector3 world) => transform.InverseTransformPoint(world);
        public CourseLayoutMarkers Markers;
        public SandboxSplineZone SplineZone;
        [HideInInspector] public Level2BezierTrack BezierTrack;
        public Vector3[] RoutePoints => points.ToArray();
        public void BindAuthored()
        {
            if(!Markers) return;
            Road=Markers.Roads.FirstOrDefault();
            points.Clear(); distances.Clear();
            if(BezierTrack) {
                foreach(var p in BezierTrack.Samples) Add(p);
            } else foreach(var node in Markers.Route) {
                var p=transform.InverseTransformPoint(node.position);
                if(points.Count==0 || (points[points.Count-1]-p).sqrMagnitude>1e-8f) Add(p);
            }
            if(points.Count<2) throw new System.InvalidOperationException("Course route needs at least two different points.");
            TurnStart=BezierTrack?BezierTrack.TurnDistance:Project(Markers.Turn.position).Distance;
            LowerStart=BezierTrack?BezierTrack.LowerDistance:Project(Markers.Lower.position).Distance;
            WallDistance=Project(Markers.WeakPoint.position).Distance;
            WeakPoint=transform.InverseTransformPoint(Markers.WeakPoint.position);
        }
        public void ReleaseResourceOwnership() => resources.Clear();
        public void Build(GravityGarage3DLevelAsset level,float scale)
        {
            if(Markers) throw new System.InvalidOperationException("Authored courses cannot be regenerated at runtime.");
            Level=level; transform.localScale=Vector3.one*scale;
            WaterVolumes.Add(new Bounds(new Vector3(0,-50.09f,-.5f),new Vector3(26,99.82f,(level.Radius+level.DeckWidth*.5f+.5f)*2)));
            var waterPath=ValleyEnvironment.WaterPath;
            for(int i=0;i<waterPath.Length-1;i++){var bounds=new Bounds(waterPath[i],Vector3.zero);bounds.Encapsulate(waterPath[i+1]);var min=bounds.min;var max=bounds.max;min.x-=1.1f;max.x+=1.1f;min.z-=.12f;max.z+=.12f;min.y=-10;bounds.SetMinMax(min,max);WaterVolumes.Add(bounds);}
            const int straight=112,turn=96;
            for(int i=0;i<=straight;i++) {
                float t=i/(float)straight;
                Add(new Vector3(-level.LaneLength*.5f+t*level.LaneLength,level.StartHeight-level.UpperDrop*t+level.BalanceRise*Mathf.Pow(Mathf.Sin(Mathf.PI*Mathf.Clamp01((t-.32f)/.18f)),2),level.Radius));
            }
            TurnStart=Length;
            for(int i=1;i<=turn;i++) {
                float t=i/(float)turn,a=t*Mathf.PI;
                Add(new Vector3(level.LaneLength*.5f+level.Radius*Mathf.Sin(a),level.StartHeight-level.UpperDrop-level.TurnDrop*t,level.Radius*Mathf.Cos(a)));
            }
            LowerStart=Length;
            for(int i=1;i<=straight;i++) {
                float t=i/(float)straight;
                Add(new Vector3(level.LaneLength*.5f-t*level.LaneLength,level.StartHeight-level.UpperDrop-level.TurnDrop-level.LowerDrop*t,-level.Radius));
            }
            var wood=Material(new Color(.55f,.33f,.16f));
            var rail=Material(new Color(.32f,.18f,.08f));
            var physics=new PhysicsMaterial("Valley timber"){staticFriction=.5f,dynamicFriction=.4f,bounciness=0,frictionCombine=PhysicsMaterialCombine.Minimum};resources.Add(physics);
            Road=Strip("Continuous wooden road",-level.DeckWidth*.5f,level.DeckWidth*.5f,0,0,wood,physics);
            var smooth=new PhysicsMaterial("Smooth timber rails"){staticFriction=0,dynamicFriction=0,bounciness=0,frictionCombine=PhysicsMaterialCombine.Minimum};resources.Add(smooth);
            for(int side=-1;side<=1;side+=2) {
                float edge=side*level.DeckWidth*.5f;
                Strip("Physical guardrail",edge,edge,0,level.RailHeight,rail,smooth);
            }
            WallDistance=LowerStart+(Length-LowerStart)*.68f;
            var wall=SampleLocal(WallDistance);
            Wall=Box("Drill wall",wall.Position+Vector3.up*.7f,new Vector3(.24f,1.4f,level.DeckWidth),Material(new Color(.62f,.47f,.3f)),true);
            WeakPoint=wall.Position+Vector3.up*level.WeakPointHeight;
            var target=Box("Yellow weak point",WeakPoint+Vector3.right*.126f,new Vector3(.015f,level.WeakPointRadius*2,level.WeakPointHalfWidth*2),Material(Color.yellow),false);
            target.transform.SetParent(Wall.transform,true);
            Arch(1.6f,"START",rail);Arch(Length-1,"FINISH",rail);
            if(level.Scenery)gameObject.AddComponent<ValleyEnvironment>().Build(this);
            Physics.SyncTransforms();
        }
        Material Material(Color c){var m=PartFactory.Material(c);resources.Add(m);return m;}
        void Add(Vector3 p){distances.Add(points.Count==0?0:Length+Vector3.Distance(points[points.Count-1],p));points.Add(p);}
        public Garage3DSample SampleLocal(float distance)
        {
            distance=Mathf.Clamp(distance,0,Length);int i=distances.BinarySearch(distance);if(i<0)i=~i-1;i=Mathf.Clamp(i,0,points.Count-2);
            var tangent=(points[i+1]-points[i]).normalized;
            var right=Vector3.Cross(Vector3.up,tangent).normalized;
            return new Garage3DSample {Distance=distance,Position=Vector3.Lerp(points[i],points[i+1],(distance-distances[i])/(distances[i+1]-distances[i])),Tangent=tangent,Normal=Vector3.Cross(tangent,right),Section=distance<TurnStart?Garage3DSection.Upper:distance<LowerStart?Garage3DSection.ConnectionArea:distance<Length-2?Garage3DSection.Lower:Garage3DSection.Finish};
        }
        public Garage3DSample Sample(float distance){var s=SampleLocal(distance);s.Position=transform.TransformPoint(s.Position);s.Tangent=transform.TransformDirection(s.Tangent);s.Normal=transform.TransformDirection(s.Normal);return s;}
        public Garage3DSample Project(Vector3 world)
        {
            var p=Coordinates(world);float best=float.PositiveInfinity,d=0;
            for(int i=0;i<points.Count-1;i++){var v=points[i+1]-points[i];float t=Mathf.Clamp01(Vector3.Dot(p-points[i],v)/v.sqrMagnitude);float sq=(p-points[i]-t*v).sqrMagnitude;if(sq<best){best=sq;d=Mathf.Lerp(distances[i],distances[i+1],t);}}
            return Sample(d);
        }
        MeshCollider Strip(string name,float left,float right,float low,float high,Material material,PhysicsMaterial physics)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();bool rail=Mathf.Approximately(left,right);int stride=rail?4:2;
            for(int i=0;i<points.Count;i++) {
                var sample=SampleLocal(distances[i]);var side=Vector3.Cross(Vector3.up,sample.Tangent).normalized;
                vertices.Add(sample.Position+side*left+Vector3.up*low);
                if(rail){float outside=left+Mathf.Sign(left)*.1f;vertices.Add(sample.Position+side*left+Vector3.up*high);vertices.Add(sample.Position+side*outside+Vector3.up*high);vertices.Add(sample.Position+side*outside+Vector3.up*low);}
                else vertices.Add(sample.Position+side*right+Vector3.up*high);
            }
            for(int i=0;i<points.Count-1;i++) {
                int a=i*stride;
                if(!rail){triangles.AddRange(new[]{a,a+2,a+1,a+1,a+2,a+3});continue;}
                for(int j=0;j<4;j++){int x=a+j,y=a+4+j,z=a+(j+1)%4,w=a+4+(j+1)%4;if(left>0)triangles.AddRange(new[]{x,y,z,z,y,w});else triangles.AddRange(new[]{x,z,y,z,w,y});}
            }
            var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();resources.Add(mesh);
            var go=new GameObject(name);go.transform.SetParent(transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
            var c=go.AddComponent<MeshCollider>();c.sharedMesh=mesh;c.sharedMaterial=physics;return c;
        }
        GameObject Box(string name,Vector3 p,Vector3 size,Material material,bool solid)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(transform,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;
            if(!solid)DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        void Arch(float d,string title,Material material)
        {
            var sample=Sample(d);
            CourseArchVisuals.Place(transform,title=="START",sample.Position,Quaternion.LookRotation(sample.Tangent,sample.Normal),Level.DeckWidth*WorldScale);
        }
        public void ResetCourse(){WallBroken=false;if(Wall)Wall.SetActive(true);foreach(var burst in GetComponents<SandboxDrillBurst>())Destroy(burst);}
        public bool TryDrill(Vector3 previous,Vector3 tip,float radius)
        {
            if(WallBroken)return false;
            if(Markers) {
                if(!Wall || !Wall.activeInHierarchy)return false;
                var delta=tip-previous;
                float length=delta.magnitude;
                radius=Mathf.Max(0,radius);
                foreach(var collider in Wall.GetComponentsInChildren<Collider>()) {
                    if(!collider.enabled || collider.isTrigger)continue;
                    bool hit=(collider.ClosestPoint(previous)-previous).sqrMagnitude<=radius*radius
                        || (collider.ClosestPoint(tip)-tip).sqrMagnitude<=radius*radius;
                    if(!hit && length>0) {
                        if(radius>0)hit=Physics.SphereCastAll(previous,radius,delta/length,length,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider==collider);
                        else hit=collider.Raycast(new Ray(previous,delta/length),out _,length);
                    }
                    if(!hit)continue;
                    var point=collider.ClosestPoint(tip);
                    WallBroken=true;Wall.SetActive(false);
                    if(Application.isPlaying)gameObject.AddComponent<SandboxDrillBurst>().Begin(point,WorldScale);
                    return true;
                }
                return false;
            }var a=Coordinates(previous);var b=Coordinates(tip);float plane=WeakPoint.x+.12f,dx=b.x-a.x;
            if(dx>=0||a.x<plane||b.x>plane)return false;
            var p=Vector3.Lerp(a,b,(plane-a.x)/dx);var offset=p-WeakPoint;offset.x=0;
            if(Mathf.Abs(offset.y)+radius/WorldScale>Level.WeakPointRadius || Mathf.Abs(offset.z)+radius/WorldScale>Level.WeakPointHalfWidth)return false;
            WallBroken=true;Wall.SetActive(false);gameObject.AddComponent<SandboxDrillBurst>().Begin(transform.TransformPoint(p),WorldScale);return true;
        }
        public Pose ReleasePose(AssemblyPart[] parts,AssemblyPart core,float transfer)
        {
            var s=Markers?new Garage3DSample{Position=Markers.Start.position,Tangent=Markers.Start.forward,Normal=Markers.Start.up}:Sample(2);
            return SurfacePose(s,parts,core,transfer);
        }
        public Pose SurfacePose(Garage3DSample s,AssemblyPart[] parts,AssemblyPart core,float transfer)
        {
            var rb=core.MainBody.Rigidbody;var inverse=Quaternion.Inverse(rb.rotation);var wheels=parts.SelectMany(p=>p.GetComponentsInChildren<PassiveWheel>()).ToArray();
            var rotation=Quaternion.LookRotation(s.Tangent,s.Normal);
            if(wheels.Length>=2){var local=wheels.Select(w=>inverse*(w.transform.position-rb.position)).ToArray();float mid=(local.Min(p=>p.z)+local.Max(p=>p.z))*.5f;var front=Enumerable.Range(0,wheels.Length).Where(i=>local[i].z>mid).ToArray();var rear=Enumerable.Range(0,wheels.Length).Where(i=>local[i].z<=mid).ToArray();if(front.Length>0&&rear.Length>0){float z=front.Average(i=>local[i].z)-rear.Average(i=>local[i].z),y=front.Average(i=>local[i].y)-rear.Average(i=>local[i].y),r=front.Average(i=>wheels[i].Radius)-rear.Average(i=>wheels[i].Radius);float pitch=Mathf.Asin(Mathf.Clamp(r/Mathf.Sqrt(z*z+y*y),-1,1))-Mathf.Atan2(y,z);rotation*=Quaternion.Euler(-pitch*Mathf.Rad2Deg,0,0);}}
            var delta=rotation*inverse;float height=.3f*WorldScale;
            if(wheels.Length>0)height=wheels.Max(w=>w.Radius*transfer-Vector3.Dot(delta*(w.transform.position-rb.position)*transfer,s.Normal));
            float along=wheels.Length>0?wheels.Average(w=>Vector3.Dot(delta*(w.transform.position-rb.position)*transfer,s.Tangent)):0;
            return new Pose(s.Position-s.Tangent*along+s.Normal*(height+.003f*WorldScale),rotation);
        }
        void OnDestroy(){foreach(var resource in resources)if(resource){if(Application.isPlaying)Destroy(resource);else DestroyImmediate(resource);}}
    }
}

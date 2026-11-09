using System.Collections.Generic;
using UnityEngine;

namespace PhysicalAssembly
{
    public sealed class GravityGarageCourse : MonoBehaviour
    {
        [field: SerializeField] public GravityGarageDesign Design { get; private set; }
        [field: SerializeField] public float Scale { get; private set; }
        [field: SerializeField] public Transform Frame { get; private set; }
        [field: SerializeField] public Collider Landing { get; private set; }
        readonly List<Object> resources=new();
        [SerializeField] GameObject generated;
        DownhillCourse course;
        public Vector3 Point(Vector2 p) => Frame.TransformPoint(new Vector3((p.x-7.39f)*Scale,.82f+(Design.LandingY-p.y)*Scale,.65f));
        public Vector3 Coordinates(Vector3 world) { var p=Frame.InverseTransformPoint(world); return new Vector3(p.x/Scale+7.39f,Design.LandingY-(p.y-.82f)/Scale,(p.z-.65f)/Scale); }
        public float WorldScale => Scale*Frame.lossyScale.x;
        public Bounds VisualBounds {
            get {var renderers=generated.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)if(!r.GetComponentInParent<ValleyEnvironment>())bounds.Encapsulate(r.bounds);return bounds;}
        }
        public CourseLayoutMarkers Markers;
        public void Rebuild(DownhillCourse owner,GravityGarageDesign design,float worldScale)
        {
            if(Markers) throw new System.InvalidOperationException("Authored courses cannot be regenerated at runtime.");
            if(!course) {
                course=owner; Frame=owner.SandboxFrame;
                var obsolete=new List<GameObject>();
                foreach(Transform t in Frame) if(t!=owner.StartPoint && t.gameObject!=generated && (!owner.FinishArea || t!=owner.FinishArea.transform) && (!owner.PlayArea || t!=owner.PlayArea.transform)) obsolete.Add(t.gameObject);
                foreach(var obj in obsolete) {obj.SetActive(false); Dispose(obj);}
            }
            Clear(); Design=design; Scale=worldScale/Frame.lossyScale.x;
            generated=new GameObject("Gravity Garage course"); generated.transform.SetParent(Frame,false);
            var road=Material(new Color(.55f,.32f,.14f),.84f);
            var rock=Material(new Color(.30f,.36f,.35f),.9f);
            var points=design.Points;
            for(int i=0;i<points.Length-1;i++) if(i!=5) {
                var a=Point(points[i]); var b=Point(points[i+1]); var delta=b-a;
                var obj=GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name=i==6?"Garage landing":"Garage road "+i;
                obj.transform.SetParent(generated.transform,true);
                obj.transform.rotation=Quaternion.LookRotation(Frame.forward,Frame.up)*Quaternion.Euler(0,0,Mathf.Atan2(Frame.InverseTransformDirection(delta).y,Frame.InverseTransformDirection(delta).x)*Mathf.Rad2Deg);
                // Offset by the road normal so the collider top matches Surface().
                obj.transform.position=(a+b)*.5f-obj.transform.up*(.04f*WorldScale);
                obj.transform.localScale=new Vector3(delta.magnitude/Frame.lossyScale.x,.08f*Scale,2.4f*Scale);
                obj.GetComponent<Renderer>().sharedMaterial=road.Item1; var c=obj.GetComponent<Collider>(); c.sharedMaterial=road.Item2; c.contactOffset=.001f*WorldScale;
                if(i==6) Landing=c;
            }
            design.RockX=design.SupportedX(design.RockX); design.Rock2X=design.SupportedX(design.Rock2X);
            Rock("A",design.RockX,design.RockRadius,rock);
            if(design.SecondRock) Rock("B",design.Rock2X,design.Rock2Radius,rock);
            var scenery=new GameObject("Valley scenery (visual only)");
            scenery.transform.SetParent(generated.transform,false);
            scenery.transform.localPosition=new Vector3(0,.82f,.65f);
            scenery.transform.localScale=Vector3.one*Scale;
            scenery.AddComponent<ValleyEnvironment>().Build(design);
            Label("DESCENT 1",design.Surface(3.2f,out _));
            Label("ROCK A",design.Surface(design.RockX,out _)+Vector2.down*(design.RockRadius*2));
            Label("DESCENT 2",design.Surface(7.1f,out _));Label("LANDING",design.Surface(12f,out _));
            if(design.SecondRock)Label("ROCK B",design.Surface(design.Rock2X,out _)+Vector2.down*(design.Rock2Radius*2));
            var release=design.Surface(design.ReleaseX,out var releaseNormal);
            owner.StartPoint.position=Point(release);
            var tangent=Frame.TransformDirection(new Vector3(-releaseNormal.y,-releaseNormal.x,0));
            owner.StartPoint.rotation=Quaternion.LookRotation(tangent,Frame.TransformDirection(new Vector3(releaseNormal.x,-releaseNormal.y,0)));
            var heading=Quaternion.LookRotation(Frame.right,Frame.up);
            CourseArchVisuals.Place(generated.transform,true,Point(design.Surface(design.ReleaseX+.65f,out _)),heading,3f*WorldScale);
            // GoalX also drives the success check; the stable-landing requirement is unchanged.
            CourseArchVisuals.Place(generated.transform,false,Point(new Vector2(design.GoalX,design.LandingY)),heading,3f*WorldScale);
            Physics.SyncTransforms();
        }
        void Label(string text,Vector2 point)
        {
            var obj=new GameObject(text+" label");obj.transform.SetParent(generated.transform,false);
            obj.transform.position=Point(point)+Frame.up*(.28f*WorldScale)-Frame.forward*(1.3f*WorldScale);
            var label=obj.AddComponent<TextMesh>();label.text=text;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;
            label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=48;label.characterSize=.055f*Scale;label.color=new Color(.9f,.94f,1);
            label.GetComponent<Renderer>().sharedMaterial=label.font.material;
        }
        System.Tuple<Material,PhysicsMaterial> Material(Color color,float friction)
        {
            var visual=PartFactory.Material(color); var physics=new PhysicsMaterial("Garage surface") {staticFriction=friction,dynamicFriction=friction,bounciness=0,frictionCombine=PhysicsMaterialCombine.Maximum};
            resources.Add(visual); resources.Add(physics); return System.Tuple.Create(visual,physics);
        }
        void Rock(string id,float x,float radius,System.Tuple<Material,PhysicsMaterial> material)
        {
            var surface=Design.Surface(x,out var normal); var obj=new GameObject("Garage rock "+id);
            obj.transform.SetParent(generated.transform,false); obj.transform.position=Point(surface+normal*radius); obj.transform.localRotation=Quaternion.Euler(0,90,0);
            var mesh=DownhillParts.Cylinder(radius*Scale,1.6f*Scale); resources.Add(mesh);
            obj.AddComponent<MeshFilter>().sharedMesh=mesh; obj.AddComponent<MeshRenderer>();
            var shape=obj.AddComponent<MeshCollider>(); shape.sharedMesh=mesh; shape.convex=true;
            obj.GetComponent<Renderer>().sharedMaterial=material.Item1; obj.GetComponent<Collider>().sharedMaterial=material.Item2; obj.GetComponent<Collider>().contactOffset=.001f*WorldScale;
            obj.AddComponent<GravityGarageRock>().Index=id=="A"?0:1;
        }
        static void Dispose(Object value) { if(Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        void Clear() { if(generated) { generated.SetActive(false); Dispose(generated); } foreach(var r in resources) if(r) Dispose(r); resources.Clear(); }
        public void ReleaseResourceOwnership() { resources.Clear(); }
        void OnDestroy() { foreach(var r in resources) if(r) Dispose(r); resources.Clear(); }
    }
}

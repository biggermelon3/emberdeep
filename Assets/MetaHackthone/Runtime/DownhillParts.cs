using System;
using System.Collections.Generic;
using UnityEngine;

namespace PhysicalAssembly
{
    public static partial class DownhillParts
    {
        public const float PuzzleSmallRadius=.26f, PuzzleLargeRadius=.46f, PuzzleLargeDrag=3f;
        public static bool IsWheel(PartKind kind) => kind==PartKind.DownhillSmallWheel || kind==PartKind.DownhillLargeWheel;
        public static bool IsKit(PartKind kind) => kind>=PartKind.DownhillLongBlock && kind<=PartKind.DownhillLargeWheel || kind==PartKind.DownhillCore || kind==PartKind.DownhillCube || kind==PartKind.DownhillDrill;
        public static AssemblyPart Create(AssemblyWorld world,PartKind kind,Pose pose,float scale=1,DownhillSettings settings=null,int gridVersion=-1,bool buildingCore=false,float designWheelRadius=0)
        {
            if(gridVersion==DesignerGridVersion) return CreateDesigner(world,kind,pose,scale,buildingCore);
            if(gridVersion>=9 && IsWheel(kind) && designWheelRadius<=0) designWheelRadius=kind==PartKind.DownhillSmallWheel?.20f:.37f;
            if(gridVersion>=9 && IsWheel(kind) && (!float.IsFinite(designWheelRadius)||designWheelRadius<.16f||designWheelRadius>.44f)) throw new ArgumentOutOfRangeException(nameof(designWheelRadius));
            if(gridVersion<0) gridVersion=buildingCore?4:3;
            if(!IsKit(kind)) throw new ArgumentException("Not a downhill part",nameof(kind));
            bool garage=gridVersion>=5;
            var root=new GameObject(kind.ToString()); root.SetActive(false);
            var part=root.AddComponent<AssemblyPart>(); part.Kind=kind; part.GridVersion=gridVersion; part.BuildingCore=buildingCore; part.GeometryScale=scale; part.DesignWheelRadius=designWheelRadius;
            var red=new Color(.76f,.16f,.10f); var gold=new Color(.93f,.61f,.20f);
            if(IsWheel(kind))
            {
                float designRadius=kind==PartKind.DownhillSmallWheel?.18f:.34f;
                if(gridVersion>=6) designRadius=.65f*BuildingGeometry.Unit/GravityGarageDesign.UnitScale*(kind==PartKind.DownhillSmallWheel?1:34f/18f);
                if(gridVersion>=7) designRadius=kind==PartKind.DownhillSmallWheel?.40f:.46f;
                if(gridVersion>=8) designRadius=kind==PartKind.DownhillSmallWheel?PuzzleSmallRadius:PuzzleLargeRadius;
                if(gridVersion>=9) designRadius=designWheelRadius>0?designWheelRadius:(kind==PartKind.DownhillSmallWheel?.20f:.37f);
                float unitScale=gridVersion>=9?GravityGarage25DCourse.UnitScale:GravityGarageDesign.UnitScale;
                float radius=(garage?designRadius*unitScale:(kind==PartKind.DownhillSmallWheel?.075f:.13f))*scale, width=(garage?.12f*unitScale:.055f)*scale;
                float wheelMass=(gridVersion>=9?.55f:.625f)*Mathf.Pow(designRadius/.24f,2);
                var hub=Body(root,"hub",garage?wheelMass*.2f:.04f); part.MainBody=hub;
                float axleDrop=garage && gridVersion<6?.17f*GravityGarageDesign.UnitScale*scale:0;
                Shape(hub,"Recessed hub",Cylinder(radius*.38f,width*.94f),gold,true).transform.localPosition=Vector3.down*axleDrop;
                if(garage && gridVersion<6) {
                    var bracket=GameObject.CreatePrimitive(PrimitiveType.Cube); bracket.name="Passive drop axle bracket"; bracket.transform.SetParent(hub.transform,false);
                    bracket.transform.localPosition=Vector3.down*axleDrop*.5f; bracket.transform.localScale=new Vector3(width*.3f,axleDrop,radius*.22f); bracket.GetComponent<Renderer>().sharedMaterial=PartFactory.Material(gold);
                }
                PartFactory.Socket(hub,"mount",Vector3.left*(width*.5f+.001f*scale),Vector3.left,"grid",Vector3.up);
                PartFactory.Socket(hub,"mount_opposite",Vector3.right*(width*.5f+.001f*scale),Vector3.right,"grid",Vector3.up);
                foreach(var socket in hub.GetComponentsInChildren<AssemblySocket>()) socket.FaceMount=true;
                var tire=Body(root,"tire",garage?wheelMass*.8f:.08f); tire.Attachable=false;
                tire.transform.localPosition=Vector3.down*axleDrop;
                var tireShape=Shape(tire,"Rubber tire",Ring(radius,radius*.40f,width),new Color(.055f,.065f,.075f),true);
                tireShape.GetComponent<Collider>().sharedMaterial=new PhysicsMaterial("Downhill rubber") { staticFriction=gridVersion>=9?.65f:garage?.84f:.03f,dynamicFriction=gridVersion>=9?.65f:garage?.84f:.03f,bounciness=0,frictionCombine=gridVersion>=9?PhysicsMaterialCombine.Minimum:garage?PhysicsMaterialCombine.Maximum:PhysicsMaterialCombine.Minimum };
                var hinge=tire.gameObject.AddComponent<HingeJoint>(); hinge.connectedBody=hub.GetComponent<Rigidbody>();
                hinge.axis=Vector3.right; hinge.autoConfigureConnectedAnchor=false; hinge.anchor=Vector3.zero; hinge.connectedAnchor=Vector3.down*axleDrop;
                hinge.useMotor=false; hinge.useSpring=false; hinge.enableCollision=false;
                var wheel=tire.gameObject.AddComponent<PassiveWheel>(); wheel.Radius=radius;
                wheel.Resistance=garage?.003f:kind==PartKind.DownhillSmallWheel?(settings?settings.SmallWheelResistance:.005f):(settings?settings.LargeWheelResistance:.5f);
                if(gridVersion==8 && kind==PartKind.DownhillLargeWheel) wheel.Resistance=PuzzleLargeDrag;
                // Alternating radial marks make physical rotation readable.
                for(int side=-1;side<=1;side+=2) for(int i=0;i<8;i++)
                {
                    float a=i*Mathf.PI/4;
                    var spoke=GameObject.CreatePrimitive(PrimitiveType.Cube); spoke.name="Wheel rotation mark";
                    UnityEngine.Object.DestroyImmediate(spoke.GetComponent<Collider>());
                    spoke.transform.SetParent(tire.transform,false); spoke.transform.localPosition=new Vector3(side*width*.51f,Mathf.Cos(a)*radius*.68f,Mathf.Sin(a)*radius*.68f);
                    spoke.transform.localRotation=Quaternion.Euler(i*45,0,0); spoke.transform.localScale=new Vector3(.002f*scale,radius*.36f,.009f*scale);
                    spoke.GetComponent<Renderer>().sharedMaterial=PartFactory.Material(gold);
                }
            }
            else
            {
                var body=Body(root,"body",garage?(buildingCore?3.5f:1.75f):kind==PartKind.DownhillLongBlock?.22f:.10f); part.MainBody=body;
                Vector3 size=kind==PartKind.DownhillLongBlock?(settings?settings.CoreSize:new Vector3(.18f,.08f,.18f)):new Vector3(.16f,.08f,.20f);
                if(buildingCore && gridVersion>=4) size=BuildingGeometry.SandboxCoreSize;
                if(garage && kind==PartKind.DownhillShortBlock) size=new Vector3(BuildingGeometry.Unit*2,BuildingGeometry.Unit,BuildingGeometry.Unit);
                Color color=kind==PartKind.DownhillLongBlock?red:kind==PartKind.DownhillShortBlock?new Color(.12f,.38f,.79f):kind==PartKind.DownhillTriangle?gold:new Color(.35f,.56f,.13f);
                if(gridVersion>=9) color=new Color(.67f,.43f,.23f);
                if(kind==PartKind.DownhillTriangle)
                {
                    size=new Vector3(.16f,.14f,.20f);
                    Shape(body,"Triangle",Prism(new[]{new Vector2(-.10f,-.07f),new Vector2(.10f,-.07f),new Vector2(-.10f,.07f)},.16f,scale),color,true);
                    PartFactory.Socket(body,"base",new Vector3(0,-.07f*scale,0),Vector3.down,"grid");
                    PartFactory.Socket(body,"back",new Vector3(0,0,-.10f*scale),Vector3.back,"grid");
                }
                else if(kind==PartKind.DownhillIrregular)
                {
                    Shape(body,"Irregular body",Prism(new[]{new Vector2(-.10f,-.04f),new Vector2(.10f,-.04f),new Vector2(.10f,.025f),new Vector2(.035f,.025f),new Vector2(.035f,.09f),new Vector2(-.10f,.06f)},.16f,scale),color,true);
                    PartFactory.Socket(body,"base",Vector3.down*.04f*scale,Vector3.down,"grid");
                    PartFactory.Socket(body,"back",new Vector3(0,0,-.10f*scale),Vector3.back,"grid");
                }
                else
                {
                    var shape=GameObject.CreatePrimitive(PrimitiveType.Cube); shape.name="Painted block"; shape.transform.SetParent(body.transform,false); shape.transform.localScale=size*scale;
                    shape.GetComponent<Renderer>().sharedMaterial=PartFactory.Material(color);
                    // Author the same socket identities at both sizes.
                    if(buildingCore && gridVersion>=4) BuildingGeometry.BoxUnitCenters(body,size,false);
                    else if(buildingCore)
                    {
                        PartFactory.Socket(body,"core_x-",Vector3.left*size.x*.5f,Vector3.left,"grid",Vector3.up);
                        PartFactory.Socket(body,"core_x+",Vector3.right*size.x*.5f,Vector3.right,"grid",Vector3.up);
                    }
                    else if(gridVersion>=3) BuildingGeometry.BoxGrid(body,size);
                    else BuildingGeometry.BoxGrid(body,size,false);
                    if(!buildingCore && gridVersion<3 && kind==PartKind.DownhillLongBlock)
                    {
                        PartFactory.Socket(body,"grid_axle_x+",Vector3.right*size.x*.5f,Vector3.right,"grid",Vector3.up);
                        PartFactory.Socket(body,"grid_axle_x-",Vector3.left*size.x*.5f,Vector3.left,"grid",Vector3.up);
                        PartFactory.Socket(body,"grid_axle_z+",Vector3.forward*size.z*.5f,Vector3.forward,"grid",Vector3.up);
                        PartFactory.Socket(body,"grid_axle_z-",Vector3.back*size.z*.5f,Vector3.back,"grid",Vector3.up);
                    }
                    foreach(var socket in body.GetComponentsInChildren<AssemblySocket>()) socket.transform.localPosition*=scale;
                }
            }
            part.Bodies=root.GetComponentsInChildren<AssemblyBody>(true); part.Sockets=root.GetComponentsInChildren<AssemblySocket>(true);
            foreach(var body in part.Bodies) { body.Part=part; BuildingGeometry.Markers(body); }
            root.transform.SetPositionAndRotation(pose.position,pose.rotation); root.SetActive(true);
            Physics.SyncTransforms(); part.Initialize(world);
            foreach(var body in part.Bodies)
            {
                body.Rigidbody.solverIterations=garage?96:48; body.Rigidbody.solverVelocityIterations=garage?32:16;
                foreach(var collider in body.Colliders) collider.contactOffset=.001f*scale*(garage?GravityGarageDesign.UnitScale:1);
                body.Rigidbody.ResetInertiaTensor();
                if(garage) { body.Rigidbody.linearDamping=.003f; body.Rigidbody.angularDamping=.003f; }
            }
            for(int i=0;i<part.Bodies.Length;i++) for(int j=i+1;j<part.Bodies.Length;j++)
                foreach(var a in part.Bodies[i].Colliders) foreach(var b in part.Bodies[j].Colliders) world.Collisions.Acquire(a,b,"internal:"+part.InstanceId);
            root.AddComponent<DownhillPartResources>().Capture();
            return part;
        }
        static AssemblyBody Body(GameObject root,string id,float mass)
        {
            var go=new GameObject(id); go.transform.SetParent(root.transform,false);
            var body=go.AddComponent<AssemblyBody>(); body.Id=id;
            var rb=go.GetComponent<Rigidbody>(); rb.mass=mass; rb.linearDamping=0; rb.angularDamping=0;
            // Preserve slow coasting at miniature scale.
            rb.sleepThreshold=.00001f;
            return body;
        }
        static GameObject Shape(AssemblyBody body,string name,Mesh mesh,Color color,bool collider)
        {
            var go=new GameObject(name); go.transform.SetParent(body.transform,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh; go.AddComponent<MeshRenderer>().sharedMaterial=PartFactory.Material(color);
            if(collider) { var c=go.AddComponent<MeshCollider>(); c.sharedMesh=mesh; c.convex=true; }
            return go;
        }
        public static Mesh Cylinder(float radius,float width) => Ring(radius,0,width);
        public static Mesh Ring(float radius,float inner,float width)
        {
            const int n=64; var v=new List<Vector3>(); var t=new List<int>();
            for(int i=0;i<n;i++)
            {
                float a=i*Mathf.PI*2/n,b=(i+1)*Mathf.PI*2/n;
                Vector3 P(float x,float r,float angle)=>new(x,Mathf.Cos(angle)*r,Mathf.Sin(angle)*r);
                void Quad(Vector3 p,Vector3 q,Vector3 r,Vector3 s) { int k=v.Count; v.AddRange(new[]{p,q,r,s}); t.AddRange(new[]{k,k+2,k+1,k,k+3,k+2}); }
                float h=width*.5f;
                Quad(P(-h,radius,a),P(h,radius,a),P(h,radius,b),P(-h,radius,b));
                Quad(P(h,inner,a),P(-h,inner,a),P(-h,inner,b),P(h,inner,b));
                Quad(P(h,radius,a),P(h,inner,a),P(h,inner,b),P(h,radius,b));
                Quad(P(-h,inner,a),P(-h,radius,a),P(-h,radius,b),P(-h,inner,b));
            }
            var mesh=new Mesh { name="Recessed wheel mesh" }; mesh.SetVertices(v); mesh.SetTriangles(t,0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        static Mesh Prism(Vector2[] outline,float width,float scale)
        {
            var v=new List<Vector3>(); var t=new List<int>(); int n=outline.Length;
            for(int side=0;side<2;side++) foreach(var p in outline) v.Add(new Vector3((side==0?-1:1)*width*.5f,p.y,p.x)*scale);
            for(int i=1;i<n-1;i++) { t.AddRange(new[]{0,i+1,i,n,n+i,n+i+1}); }
            for(int i=0;i<n;i++) { int j=(i+1)%n; t.AddRange(new[]{i,j,n+j,i,n+j,n+i}); }
            for(int i=0;i<t.Count;i+=3) (t[i+1],t[i+2])=(t[i+2],t[i+1]);
            var mesh=new Mesh { name="Wooden prism" }; mesh.SetVertices(v); mesh.SetTriangles(t,0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}







































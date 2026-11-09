using System;
using UnityEngine;

namespace PhysicalAssembly
{
    public sealed class PartFactory : MonoBehaviour
    {
        public AssemblyWorld World;
        public AssemblyPart[] Prefabs;
        public const int LatestGridVersion=10;
        public static int DefaultGridVersion(PartKind kind) => kind switch
        {
            // These already use unit centers; preserve their prefab/save identity.
            PartKind.DownhillCore or PartKind.DownhillCube or PartKind.DownhillDrill => DownhillParts.DesignerGridVersion,
            PartKind.SmallCube or PartKind.Cuboid => 2,
            PartKind.Wheel or PartKind.Weapon or PartKind.PhysicsSphere => 1,
            _ => 3
        };
        public AssemblyPart Create(PartKind kind,Vector3 position,Quaternion rotation,bool legacyGrid=false)
            => Create(kind,position,rotation,legacyGrid?0:DefaultGridVersion(kind));
        public AssemblyPart Create(PartKind kind,Vector3 position,Quaternion rotation,int gridVersion,bool buildingCore=false,float geometryScale=1,float designWheelRadius=0)
        {
            ValidateGridVersion(gridVersion);
            if(!World.EditingEnabled) throw new InvalidOperationException("Retry before adding parts.");
            if(DownhillParts.IsKit(kind))
            {
                return DownhillParts.Create(World,kind,new Pose(position,rotation),scale:geometryScale,gridVersion:gridVersion,buildingCore:buildingCore,designWheelRadius:designWheelRadius);
            }
            var prefab=Prefabs==null?null:Array.Find(Prefabs,p=>p && p.Kind==kind && p.GridVersion==gridVersion);
            var part=prefab?Instantiate(prefab,position,rotation):BuildDefinition(kind,gridVersion);
            if(!prefab) part.transform.SetPositionAndRotation(position,rotation);
            Physics.SyncTransforms();
            part.InstanceId=Guid.NewGuid().ToString("N"); part.Initialize(World);
            World.Challenge?.ApplyBuildingPhysics(part);
            if(kind==PartKind.Wheel)
            {
                for(int i=0;i<part.Bodies.Length;i++) for(int j=i+1;j<part.Bodies.Length;j++)
                foreach(var a in part.Bodies[i].Colliders) foreach(var b in part.Bodies[j].Colliders)
                    World.Collisions.Acquire(a,b,"internal:"+part.InstanceId);
            }
            return part;
        }
        public static AssemblyPart BuildDefinition(PartKind kind,bool legacyGrid=false)
            => BuildDefinition(kind,legacyGrid?0:DefaultGridVersion(kind));
        static void ValidateGridVersion(int gridVersion)
        {
            if(gridVersion<0 || gridVersion>LatestGridVersion) throw new ArgumentOutOfRangeException(nameof(gridVersion));
        }
        public static AssemblyPart BuildDefinition(PartKind kind,int gridVersion)
        {
            ValidateGridVersion(gridVersion);
            var root=new GameObject(kind.ToString()); var part=root.AddComponent<AssemblyPart>(); part.Kind=kind;
            part.GridVersion=gridVersion;
            var teal=new Color(.05f,.72f,.65f); var dark=new Color(.065f,.09f,.13f); var orange=new Color(1,.42f,.08f);
            switch(kind)
            {
                case PartKind.Chassis:
                    part.MainBody=Body(root,"chassis",Vector3.zero,HustlePartProfile.ChassisSize,PrimitiveType.Cube,teal,.16f);
                    part.MainBody.GetComponent<Rigidbody>().angularDamping=2;
                    root.AddComponent<ChassisSteering>();
                    BuildingGeometry.BoxGrid(part.MainBody,HustlePartProfile.ChassisSize,gridVersion);
                    for(int side=-1;side<=1;side+=2) for(int end=-1;end<=1;end+=2)
                        Socket(part.MainBody,$"wheel_{side}_{end}",new Vector3(side*HustlePartProfile.ChassisSize.x*.5f,0,end*.17f),Vector3.right*side,"wheel");
                    Socket(part.MainBody,"weapon",new Vector3(0,HustlePartProfile.ChassisSize.y*.5f,.12f),Vector3.up,"weapon");
                    Decor(part.MainBody.transform,"Forward stripe",new Vector3(0,HustlePartProfile.ChassisSize.y*.5f+.004f,.22f),new Vector3(.10f,.008f,.05f),PrimitiveType.Cube,orange);
                    break;
                case PartKind.SmallCube:
                case PartKind.Cuboid:
                case PartKind.Pyramid:
                    var dimensions=kind==PartKind.Cuboid?(gridVersion>=2?BuildingGeometry.CuboidSize:BuildingGeometry.LegacyCuboidSize):BuildingGeometry.CubeSize;
                    part.MainBody=Body(root,"body",Vector3.zero,dimensions,PrimitiveType.Cube,teal,kind==PartKind.Cuboid?.15f:.05f);
                    if(kind==PartKind.Pyramid)
                    {
                        var shape=part.MainBody.transform.Find("Shape").gameObject;
                        UnityEngine.Object.DestroyImmediate(shape.GetComponent<BoxCollider>());
                        shape.transform.localScale=Vector3.one;
                        var pyramid=BuildingGeometry.Pyramid(part.MainBody,gridVersion);
                        shape.GetComponent<MeshFilter>().sharedMesh=pyramid;
                        var pc=shape.AddComponent<MeshCollider>(); pc.sharedMesh=pyramid; pc.convex=true;
                    }
                    else if(gridVersion>=2) BuildingGeometry.BoxUnitCenters(part.MainBody,dimensions);
                    else BuildingGeometry.BoxGrid(part.MainBody,dimensions,gridVersion);
                    break;
                case PartKind.PhysicsSphere:
                    part.MainBody=Body(root,"sphere",Vector3.zero,Vector3.one*.04f,PrimitiveType.Cube,orange,.025f);
                    var sphereShape=part.MainBody.transform.Find("Shape").gameObject;
                    UnityEngine.Object.DestroyImmediate(sphereShape.GetComponent<BoxCollider>());
                    var spherePrimitive=GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    sphereShape.GetComponent<MeshFilter>().sharedMesh=spherePrimitive.GetComponent<MeshFilter>().sharedMesh;
                    UnityEngine.Object.DestroyImmediate(spherePrimitive);
                    sphereShape.AddComponent<SphereCollider>(); part.MainBody.Attachable=false;
                    part.MainBody.GetComponent<Rigidbody>().collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
                    break;
                case PartKind.Wheel:
                    var mount=Body(root,"mount",Vector3.zero,new Vector3(HustlePartProfile.MountWidth,.04f,.04f),PrimitiveType.Cube,orange,.04f);
                    var hub=Body(root,"hub",new Vector3(.08f,-.025f,0),new Vector3(HustlePartProfile.HubWidth,.04f,.04f),PrimitiveType.Cube,dark,.04f);
                    var wheel=Body(root,"wheel",new Vector3(.08f,-.025f,0),Vector3.one,PrimitiveType.Sphere,dark,.08f);
                    wheel.GetComponent<Rigidbody>().angularDamping=1;
                    hub.Attachable=false; wheel.Attachable=false;
                    // A spherical collider scaled into a wheel is not supported by PhysX: use a convex cylinder mesh.
                    UnityEngine.Object.DestroyImmediate(wheel.GetComponent<Collider>());
                    var visual=wheel.GetComponent<MeshFilter>(); var cylinder=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    visual.sharedMesh=cylinder.GetComponent<MeshFilter>().sharedMesh; UnityEngine.Object.DestroyImmediate(cylinder);
                    wheel.transform.localScale=Vector3.one; visual.gameObject.GetComponent<MeshRenderer>().sharedMaterial=Material(dark);
                    // Bake the cylinder's rotation and dimensions into vertices; physics bodies keep unit scale.
                    var mesh=UnityEngine.Object.Instantiate(visual.sharedMesh); var vertices=mesh.vertices;
                    for(int i=0;i<vertices.Length;i++) vertices[i]=Quaternion.Euler(0,0,90)*Vector3.Scale(vertices[i],new Vector3(HustlePartProfile.WheelDiameter,HustlePartProfile.WheelWidth*.5f,HustlePartProfile.WheelDiameter));
                    mesh.vertices=vertices; mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.name="WheelGeometry"; visual.sharedMesh=mesh;
                    var collider=wheel.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh=mesh; collider.convex=true;
                    collider.sharedMaterial=new PhysicsMaterial("Hustle rubber") { staticFriction=.7f,dynamicFriction=.7f,bounciness=0,frictionCombine=PhysicsMaterialCombine.Average };
                    var suspension=hub.gameObject.AddComponent<ConfigurableJoint>(); suspension.connectedBody=mount.GetComponent<Rigidbody>(); suspension.autoConfigureConnectedAnchor=false;
                    suspension.anchor=Vector3.zero; suspension.connectedAnchor=new Vector3(.08f,-.025f,0);
                    suspension.xMotion=suspension.zMotion=ConfigurableJointMotion.Locked; suspension.yMotion=ConfigurableJointMotion.Limited;
                    suspension.linearLimit=new SoftJointLimit { limit=HustlePartProfile.SuspensionTravel }; suspension.yDrive=new JointDrive { positionSpring=120,positionDamper=6,maximumForce=100 };
                    suspension.angularXMotion=suspension.angularYMotion=suspension.angularZMotion=ConfigurableJointMotion.Locked; suspension.enablePreprocessing=false;
                    var axle=wheel.gameObject.AddComponent<HingeJoint>(); axle.connectedBody=hub.GetComponent<Rigidbody>(); axle.axis=Vector3.right;
                    axle.autoConfigureConnectedAnchor=false; axle.anchor=Vector3.zero; axle.connectedAnchor=Vector3.zero; axle.enableCollision=false;
                    Socket(mount,"mount",new Vector3(HustlePartProfile.MountSocketX,0,0),Vector3.left,"wheel"); part.MainBody=mount;
                    var motor=root.AddComponent<WheelMotor>(); motor.Wheel=wheel; motor.Hub=hub;
                    break;
                case PartKind.Weapon:
                    part.MainBody=Body(root,"weapon",Vector3.zero,new Vector3(.12f,.08f,.18f),PrimitiveType.Cube,orange,.25f);
                    Socket(part.MainBody,"mount",new Vector3(0,-.05f,0),Vector3.down,"weapon");
                    Decor(part.MainBody.transform,"Barrel",new Vector3(0,.015f,.13f),new Vector3(.045f,.045f,.16f),PrimitiveType.Cube,dark);
                    var muzzle=new GameObject("Muzzle").transform; muzzle.SetParent(part.MainBody.transform,false); muzzle.localPosition=new Vector3(0,.015f,.23f);
                    root.AddComponent<VehicleWeapon>().Muzzle=muzzle;
                    break;
                default:
                    var size=kind==PartKind.Beam?HustlePartProfile.BeamSize:Vector3.one*.10f;
                    part.MainBody=Body(root,"body",Vector3.zero,size,PrimitiveType.Cube,kind==PartKind.Beam?orange:teal,.15f);
                    BuildingGeometry.BoxGrid(part.MainBody,size,gridVersion);
                    break;
            }
            part.Bodies=root.GetComponentsInChildren<AssemblyBody>(); part.Sockets=root.GetComponentsInChildren<AssemblySocket>();
            foreach(var body in part.Bodies) { body.Part=part; BuildingGeometry.Markers(body); }
            HustleVisualLibrary.Apply(part);
            WheelHubGeometry.Apply(part);
            return part;
        }
        static AssemblyBody Body(GameObject root,string id,Vector3 pos,Vector3 size,PrimitiveType type,Color color,float mass)
        {
            var go=new GameObject(id); go.transform.SetParent(root.transform,false); go.transform.localPosition=pos;
            var primitive=GameObject.CreatePrimitive(type); primitive.name="Shape"; primitive.transform.SetParent(go.transform,false); primitive.transform.localScale=size;
            var body=go.AddComponent<AssemblyBody>(); body.Id=id; var rb=go.GetComponent<Rigidbody>(); rb.mass=mass; rb.linearDamping=.01f; rb.angularDamping=0;
            primitive.GetComponent<Renderer>().sharedMaterial=Material(color);
            if(type==PrimitiveType.Sphere)
            {
                // Wheel mesh/collider is authored directly on the body below.
                go.AddComponent<MeshFilter>().sharedMesh=primitive.GetComponent<MeshFilter>().sharedMesh;
                go.AddComponent<MeshRenderer>().sharedMaterial=primitive.GetComponent<Renderer>().sharedMaterial;
                UnityEngine.Object.DestroyImmediate(primitive);
            }
            return body;
        }
        public static Material Material(Color color)
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit"); if(!shader) shader=Shader.Find("Standard");
            var m=new Material(shader); m.color=color; m.SetFloat("_Smoothness",.25f); return m;
        }
        public static void Socket(AssemblyBody body,string id,Vector3 pos,Vector3 normal,string family,Vector3? tangent=null)
        {
            var go=new GameObject("Socket_"+id); go.transform.SetParent(body.transform,false); go.transform.localPosition=pos;
            var up=Mathf.Abs(Vector3.Dot(normal,Vector3.up))>.9f?Vector3.forward:Vector3.up;
            go.transform.localRotation=Quaternion.LookRotation(normal,tangent??up);
            var socket=go.AddComponent<AssemblySocket>(); socket.Id=id; socket.Body=body; socket.Family=family;
            if(family!="grid") Decor(go.transform,"Marker",Vector3.zero,Vector3.one*.015f,PrimitiveType.Sphere,new Color(1,.9f,.3f));
        }
        static void Decor(Transform parent,string name,Vector3 pos,Vector3 scale,PrimitiveType type,Color color)
        {
            var go=GameObject.CreatePrimitive(type); go.name=name; go.transform.SetParent(parent,false); go.transform.localPosition=pos; go.transform.localScale=scale;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); go.GetComponent<Renderer>().sharedMaterial=Material(color);
        }
    }
}

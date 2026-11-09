using System;
using UnityEngine;

namespace PhysicalAssembly
{
    public static partial class DownhillParts
    {
        public const int DesignerGridVersion=10;
        public static readonly PartKind[] DesignerKit={PartKind.DownhillCore,PartKind.DownhillLongBlock,
            PartKind.DownhillShortBlock,PartKind.DownhillCube,PartKind.DownhillTriangle,
            PartKind.DownhillLargeWheel,PartKind.DownhillLargeWheel,PartKind.DownhillSmallWheel,PartKind.DownhillSmallWheel};

        static AssemblyPart CreateDesigner(AssemblyWorld world,PartKind kind,Pose pose,float scale,bool buildingCore)
        {
            var root=new GameObject(kind.ToString()); root.SetActive(false);
            var part=root.AddComponent<AssemblyPart>(); part.Kind=kind; part.GridVersion=DesignerGridVersion;
            part.BuildingCore=buildingCore || kind==PartKind.DownhillCore; part.GeometryScale=scale;
            float u=BuildingGeometry.Unit;
            var gold=new Color(.89f,.60f,.29f);
            if(IsWheel(kind))
            {
                float radius=(kind==PartKind.DownhillLargeWheel?.44f:.30f)/.38f*u*scale;
                float width=.23f/.38f*u*scale;
                var hub=Body(root,"hub",.04f); part.MainBody=hub;
                Shape(hub,"Hub",Cylinder(radius*.35f,width*.96f),gold,true);
                PartFactory.Socket(hub,"mount",Vector3.left*width*.5f,Vector3.left,"grid",Vector3.up);
                var tire=Body(root,"tire",.12f);
                tire.Attachable=false;
                var shape=Shape(tire,"Rubber tire",Ring(radius,radius*.39f,width),new Color(.07f,.09f,.12f),true);
                shape.GetComponent<Collider>().sharedMaterial=new PhysicsMaterial("Designer rubber") {staticFriction=.65f,dynamicFriction=.65f,frictionCombine=PhysicsMaterialCombine.Minimum};
                var hinge=tire.gameObject.AddComponent<HingeJoint>(); hinge.connectedBody=hub.GetComponent<Rigidbody>();
                hinge.axis=Vector3.right; hinge.autoConfigureConnectedAnchor=false; hinge.anchor=hinge.connectedAnchor=Vector3.zero;
                hinge.enableCollision=false;
                var passive=tire.gameObject.AddComponent<PassiveWheel>(); passive.Radius=radius; passive.Resistance=.003f;
            }
            else
            {
                var body=Body(root,"body",part.BuildingCore?.22f:.14f); part.MainBody=body;
                if(kind==PartKind.DownhillDrill) { SandboxDrill.Create(body,u,scale); PartFactory.Socket(body,"base",SandboxDrill.BaseCenter(u,scale),Vector3.back,"grid"); }
                else if(kind==PartKind.DownhillTriangle)
                {
                    // Right triangular prism, extruded along X. Normals lie on the real faces.
                    Shape(body,"Triangular prism",Prism(new[]{new Vector2(-u,-u*.5f),new Vector2(u,-u*.5f),new Vector2(-u,u*.5f)},u,scale),gold,true);
                    PartFactory.Socket(body,"end_x-",new Vector3(-u*.5f,-u/6,-u/3)*scale,Vector3.left,"grid");
                    PartFactory.Socket(body,"end_x+",new Vector3(u*.5f,-u/6,-u/3)*scale,Vector3.right,"grid");
                    PartFactory.Socket(body,"back",new Vector3(0,0,-u)*scale,Vector3.back,"grid");
                    for(int i=0;i<2;i++) {
                        float z=(i==0?-.5f:.5f)*u;
                        PartFactory.Socket(body,"base_"+i,new Vector3(0,-u*.5f,z)*scale,Vector3.down,"grid");
                        PartFactory.Socket(body,"slope_"+i,new Vector3(0,-z*.5f,z)*scale,new Vector3(0,2,1).normalized,"grid");
                    }
                }
                else
                {
                    Vector3 size=part.BuildingCore?Vector3.one*u:kind==PartKind.DownhillCube?Vector3.one*(2*u):new Vector3(u,u,(kind==PartKind.DownhillLongBlock?3:2)*u);
                    var cube=GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name="Painted block"; cube.transform.SetParent(body.transform,false);
                    cube.transform.localScale=size*scale; cube.GetComponent<Renderer>().sharedMaterial=PartFactory.Material(part.BuildingCore?new Color(.10f,.56f,.76f):gold);
                    BuildingGeometry.BoxUnitCenters(body,size);
                    foreach(var socket in body.GetComponentsInChildren<AssemblySocket>()) socket.transform.localPosition*=scale;
                }
            }
            part.Bodies=root.GetComponentsInChildren<AssemblyBody>(true); part.Sockets=root.GetComponentsInChildren<AssemblySocket>(true);
            foreach(var body in part.Bodies) body.Part=part;
            root.transform.SetPositionAndRotation(pose.position,pose.rotation); root.SetActive(true); Physics.SyncTransforms(); part.Initialize(world);
            foreach(var body in part.Bodies) {
                body.Rigidbody.solverIterations=96; body.Rigidbody.solverVelocityIterations=32;
                body.Rigidbody.linearDamping=body.Rigidbody.angularDamping=.003f;
                foreach(var collider in body.Colliders) collider.contactOffset=.0005f*scale;
                body.Rigidbody.ResetInertiaTensor();
            }
            for(int i=0;i<part.Bodies.Length;i++) for(int j=i+1;j<part.Bodies.Length;j++)
                foreach(var a in part.Bodies[i].Colliders) foreach(var b in part.Bodies[j].Colliders) world.Collisions.Acquire(a,b,"internal:"+part.InstanceId);
            root.AddComponent<DownhillPartResources>().Capture(); return part;
        }
    }
}

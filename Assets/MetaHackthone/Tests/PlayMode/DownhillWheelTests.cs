using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace PhysicalAssembly.Tests
{
    public sealed class DownhillWheelTests
    {
        readonly List<GameObject> objects=new();
        float previousStep;
        [SetUp] public void SetPhysicsStep() { previousStep=Time.fixedDeltaTime; Time.fixedDeltaTime=1f/60; }
        [TearDown] public void RestorePhysicsStep() => Time.fixedDeltaTime=previousStep;
        [TearDown] public void Cleanup() { foreach(var go in objects) if(go) Object.DestroyImmediate(go); objects.Clear(); }
        AssemblyPart Vehicle(PartKind kind,float x)
        {
            var root=new GameObject("Wheel comparison"); objects.Add(root); var world=root.AddComponent<AssemblyWorld>();
            float radius=(kind==PartKind.DownhillSmallWheel?.075f:.13f)/3;
            // Keep the comparison rig's original wheelbase: four large wheels do not
            // fit on the square player core. This isolates tire behavior from layout.
            var rigSettings=ScriptableObject.CreateInstance<DownhillSettings>();
            rigSettings.CoreSize=new Vector3(.18f,.08f,.42f);
            var core=DownhillParts.Create(world,PartKind.DownhillLongBlock,new Pose(new Vector3(x,5+radius+.001f,0),Quaternion.identity),1f/3,rigSettings);
            Object.DestroyImmediate(rigSettings);
            objects.Add(core.gameObject);
            for(int side=-1;side<=1;side+=2) for(int end=-1;end<=1;end+=2)
            {
                var wheel=DownhillParts.Create(world,kind,new Pose(core.MainBody.Rigidbody.position,Quaternion.identity),1f/3); objects.Add(wheel.gameObject);
                var socket=core.Sockets.Where(s=>s.Id.Contains(side>0?"x+":"x-")&&!world.SocketOccupied(s)).OrderBy(s=>Mathf.Abs(s.transform.localPosition.z-end*.045f)+Mathf.Abs(s.transform.localPosition.y)*2).First();
                var mount=wheel.FindSocket("mount"); var rotation=Quaternion.Euler(0,side>0?0:180,0);
                foreach(var body in wheel.Bodies) { body.Rigidbody.position=socket.WorldPosition-rotation*mount.transform.localPosition; body.Rigidbody.rotation=rotation; body.transform.SetPositionAndRotation(body.Rigidbody.position,rotation); }
                Physics.SyncTransforms();
                Assert.IsTrue(world.Connect(new ConnectionRecord { id=Guid.NewGuid().ToString("N"),a=new BodyRef(wheel.InstanceId,mount.Body.Id),b=new BodyRef(core.InstanceId,socket.Body.Id),socketA=mount.Id,socketB=socket.Id,anchorA=mount.transform.localPosition,anchorB=socket.transform.localPosition }));
            }
            var bodies=world.Parts.Values.SelectMany(p=>p.Bodies).ToArray();
            for(int i=0;i<bodies.Length;i++) for(int j=i+1;j<bodies.Length;j++)
                foreach(var a in bodies[i].Colliders) foreach(var b in bodies[j].Colliders) world.Collisions.Acquire(a,b,"vehicle");
            return core;
        }
        void Ground(float x,bool barrier)
        {
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube); objects.Add(ground); ground.transform.position=new Vector3(x,4.95f,4); ground.transform.localScale=new Vector3(1.5f,.1f,12); ground.GetComponent<Collider>().contactOffset=.001f;
            if(!barrier) return;
            var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube); objects.Add(obstacle); obstacle.transform.position=new Vector3(x,5.009f,.6f); obstacle.transform.localScale=new Vector3(1.5f,.018f,.018f); obstacle.GetComponent<Collider>().contactOffset=.001f;
        }
        static void Roll(AssemblyPart core,float speed)
        {
            foreach(var part in core.World.Parts.Values) foreach(var body in part.Bodies)
            {
                body.Rigidbody.linearVelocity=Vector3.forward*speed; body.Rigidbody.angularVelocity=Vector3.zero;
                var wheel=body.GetComponent<PassiveWheel>(); if(wheel) body.Rigidbody.angularVelocity=Vector3.right*(speed/wheel.Radius);
                body.Rigidbody.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            }
        }
        [UnityTest] public IEnumerator SmallWheelsCoastFartherWithEqualStartingSpeed()
        {
            Ground(-3,false); Ground(3,false); var small=Vehicle(PartKind.DownhillSmallWheel,-3); var large=Vehicle(PartKind.DownhillLargeWheel,3);
            yield return new WaitForSeconds(.25f); Roll(small,.6f); Roll(large,.6f);
            float a=small.MainBody.Rigidbody.position.z,b=large.MainBody.Rigidbody.position.z;
            yield return new WaitForSeconds(12);
            float ds=small.MainBody.Rigidbody.position.z-a,dl=large.MainBody.Rigidbody.position.z-b;
            Debug.Log($"DOWNHILL_COAST small={ds:F3} large={dl:F3}");
            Assert.Greater(ds,.3f); Assert.GreaterOrEqual(ds,dl*1.5f,$"small={ds}, large={dl}");
        }
        [UnityTest] public IEnumerator LargeWheelsClearBarrierWhileSmallWheelsStop()
        {
            Ground(-3,true); Ground(3,true); var small=Vehicle(PartKind.DownhillSmallWheel,-3); var large=Vehicle(PartKind.DownhillLargeWheel,3);
            yield return new WaitForSeconds(.25f); Roll(small,2.2f); Roll(large,2.2f);
            yield return new WaitForSeconds(2);
            Assert.Less(small.MainBody.Rigidbody.position.z,.65f,"Small wheels should be stopped by the barrier.");
            Assert.Greater(large.MainBody.Rigidbody.position.z,.9f,"Large wheels should roll across the same barrier.");
            Assert.Less(large.MainBody.Rigidbody.linearVelocity.z,2.2f,"The barrier must dissipate momentum.");
        }
    }
}






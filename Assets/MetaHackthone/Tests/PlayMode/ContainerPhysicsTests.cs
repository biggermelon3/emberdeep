using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PhysicalAssembly.Tests
{
    public sealed class ContainerPhysicsTests
    {
        GameObject root; AssemblyWorld world; PartFactory factory;
        [SetUp] public void Setup() { Time.fixedDeltaTime=1f/60; root=new GameObject("Container physics"); world=root.AddComponent<AssemblyWorld>(); factory=root.AddComponent<PartFactory>(); factory.World=world; }
        [TearDown] public void Cleanup() { foreach(var p in world.Parts.Values.ToArray()) Object.DestroyImmediate(p.gameObject); Object.DestroyImmediate(root); }
        [UnityTest] public IEnumerator ContainerRetainsBallsWhileHeldLiftedAndTilted()
        {
            float u=BuildingGeometry.Unit;
            var floor=factory.Create(PartKind.Chassis,new Vector3(0,2,0),Quaternion.identity);
            var walls=new List<(Vector3 side,AssemblyPart part)>();
            float y=2+HustlePartProfile.ChassisSize.y/2+u/2;
            // Centered sections on a 3x6 base: unit end walls and two-unit side
            // walls enclose the same balls without using removed corner points.
            foreach(var position in new[]{new Vector3(0,y,-1.5f*u),new Vector3(0,y,1.5f*u),new Vector3(-u,y,0),new Vector3(u,y,0)})
            {
                var wall=factory.Create(position.x==0?PartKind.SmallCube:PartKind.Cuboid,position+Vector3.up*.01f,position.x==0?Quaternion.identity:Quaternion.Euler(0,90,0));
                walls.Add((position-new Vector3(0,y,0),wall));
                foreach(var body in world.Parts.Values.SelectMany(p=>p.Bodies)) body.Rigidbody.useGravity=false;
                var candidate=world.Query(wall); Assert.IsTrue(candidate.IsValid,"Wall must find a clear grid placement.");
                Assert.IsTrue(world.TryBeginAssembly(candidate));
                yield return new WaitForSeconds(.3f);
                Assert.Less(Vector3.Distance(candidate.SnappedPose.position,wall.MainBody.Rigidbody.position),.002f);
            }
            Assert.AreEqual(4,world.Connections.Count);
            // The reduced grid can translate the enclosure within the chassis.
            // Check the actual inner wall planes, including the whole sphere,
            // rather than assuming the pre-snap opening stayed centered at zero.
            Physics.SyncTransforms();
            var minimum=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            var maximum=new Vector2(float.PositiveInfinity,float.PositiveInfinity);
            foreach(var wall in walls)
            {
                var bounds=wall.part.MainBody.Colliders[0].bounds;
                if(wall.side.x<0) minimum.x=bounds.max.x-floor.MainBody.Rigidbody.position.x;
                if(wall.side.x>0) maximum.x=bounds.min.x-floor.MainBody.Rigidbody.position.x;
                if(wall.side.z<0) minimum.y=bounds.max.z-floor.MainBody.Rigidbody.position.z;
                if(wall.side.z>0) maximum.y=bounds.min.z-floor.MainBody.Rigidbody.position.z;
            }
            const float radius=.02f,tolerance=.003f;
            Assert.Greater(maximum.x-minimum.x,radius*2);
            Assert.Greater(maximum.y-minimum.y,radius*2);
            var grip=new Pose(floor.MainBody.Rigidbody.position,Quaternion.identity); Assert.IsTrue(world.TryGrab(0,floor,grip));
            var balls=Enumerable.Range(0,3).Select(i=>factory.Create(PartKind.PhysicsSphere,new Vector3((i-1)*.023f,2.18f+i*.05f,0),Quaternion.identity)).ToArray();
            for(int i=0;i<180;i++)
            {
                float t=Mathf.Clamp01((i-60)/120f);
                world.SetHoldPose(0,new Pose(grip.position+Vector3.up*t*.2f,Quaternion.Euler(0,0,t*8)));
                yield return new WaitForFixedUpdate();
            }
            foreach(var ball in balls)
            {
                var local=floor.MainBody.transform.InverseTransformPoint(ball.MainBody.Rigidbody.position);
                Assert.Greater(local.y,HustlePartProfile.ChassisSize.y/2+radius-tolerance,"Ball fell through the floor.");
                Assert.That(local.x,Is.InRange(minimum.x+radius-tolerance,maximum.x-radius+tolerance));
                Assert.That(local.z,Is.InRange(minimum.y+radius-tolerance,maximum.y-radius+tolerance));
            }
            Assert.AreEqual(4,world.Connections.Count); world.ReleaseAll();
        }
        [UnityTest] public IEnumerator AlignmentReservesConnectionsAndCancellationFreesThem()
        {
            var a=factory.Create(PartKind.SmallCube,new Vector3(0,2,0),Quaternion.identity);
            var b=factory.Create(PartKind.SmallCube,new Vector3(BuildingGeometry.Unit+.01f,2,0),Quaternion.identity);
            a.MainBody.Rigidbody.useGravity=b.MainBody.Rigidbody.useGravity=false;
            var candidate=world.Query(a); Assert.IsTrue(candidate.IsValid); Assert.IsTrue(world.TryBeginAssembly(candidate));
            Assert.IsFalse(world.TryBeginAssembly(candidate)); Assert.IsFalse(world.Query(b).IsValid);
            world.enabled=false; yield return null;
            Assert.IsFalse(world.IsBusy); Assert.IsEmpty(world.Connections);
            Assert.IsFalse(a.MainBody.Rigidbody.isKinematic); Assert.IsFalse(b.MainBody.Rigidbody.isKinematic);
            world.enabled=true; Assert.IsTrue(world.TryBeginAssembly(world.Query(a)));
            yield return new WaitForSeconds(.3f); Assert.AreEqual(1,world.Connections.Count);
        }
    }
}

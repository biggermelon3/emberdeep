using NUnit.Framework;
using UnityEngine;
namespace PhysicalAssembly.Tests
{
    public sealed class SandboxConnectionTests
    {
        GameObject root;
        [TearDown]public void Cleanup(){Object.DestroyImmediate(root);}
        [TestCase(0f)] [TestCase(.5f)] [TestCase(-.5f)] public void TransferPreservesEveryBodyMotionAndConfiguration(float boost)
        {
            root=new GameObject("Snapshot test");var a=root.AddComponent<Rigidbody>();
            var child=new GameObject("Wheel");child.transform.SetParent(root.transform);var b=child.AddComponent<Rigidbody>();
            a.position=new Vector3(1,2,3);b.position=new Vector3(2,2,3);
            a.rotation=Quaternion.Euler(12,23,34);b.rotation=Quaternion.Euler(55,66,77);
            a.linearVelocity=new Vector3(.1f,.2f,.3f);b.linearVelocity=new Vector3(-1,2,3);
            a.angularVelocity=new Vector3(2,-3,1);b.angularVelocity=new Vector3(15,2,-4);b.useGravity=false;
            var bodies=new[]{a,b};var positions=new[]{a.position,b.position};var rotations=new[]{a.rotation,b.rotation};
            var velocities=new[]{a.linearVelocity,b.linearVelocity};var angular=new[]{a.angularVelocity,b.angularVelocity};
            var entrance=new Pose(new Vector3(1,0,0),Quaternion.Euler(0,30,0));
            var transfer=new SandboxConnectionTransfer(bodies,entrance);
            Assert.IsTrue(a.isKinematic);Assert.IsFalse(a.detectCollisions);Assert.IsFalse(b.useGravity);
            transfer.Move(new Pose(Vector3.up*200,Quaternion.Euler(100,0,30)));
            var exit=new Pose(new Vector3(12,-30,8),Quaternion.Euler(12,150,10));transfer.Move(exit);transfer.Restore(boost);
            var delta=exit.rotation*Quaternion.Inverse(entrance.rotation);
            for(int i=0;i<2;i++) {
                Assert.Less(Vector3.Distance(bodies[i].position,exit.position+delta*(positions[i]-entrance.position)),.0001f);
                Assert.Less(Quaternion.Angle(bodies[i].rotation,delta*rotations[i]),.05f);
                Assert.Less(Vector3.Distance(bodies[i].linearVelocity,delta*velocities[i]+exit.rotation*Vector3.forward*boost),.0001f);
                Assert.Less(Vector3.Distance(bodies[i].angularVelocity,delta*angular[i]),.0001f);
                Assert.IsFalse(bodies[i].isKinematic);Assert.IsTrue(bodies[i].detectCollisions);
            }
            Assert.IsTrue(a.useGravity);Assert.IsFalse(b.useGravity);
        }
        [Test]public void SuccessfulExitClearsSpinAndTransverseMotion()
        {
            root=new GameObject("Spinning car");var b=root.AddComponent<Rigidbody>();
            b.rotation=Quaternion.Euler(25,40,15);b.linearVelocity=new Vector3(2,3,4);b.angularVelocity=Vector3.one*4;
            var t=new SandboxConnectionTransfer(new[]{b},new Pose(Vector3.zero,Quaternion.identity));
            var pose=new Pose(Vector3.up,Quaternion.Euler(0,90,0));var velocity=Vector3.right*5;
            t.Release(b,pose,velocity);
            Assert.Less(Vector3.Distance(pose.position,b.position),.0001f);
            Assert.Less(Quaternion.Angle(pose.rotation,b.rotation),.01f);
            Assert.AreEqual(velocity,b.linearVelocity);Assert.AreEqual(Vector3.zero,b.angularVelocity);
            Assert.IsFalse(b.isKinematic);Assert.IsTrue(b.detectCollisions);Assert.IsTrue(b.useGravity);
        }
        [Test]public void ZeroMotionDoesNotAcquireAnimationVelocity()
        {
            root=new GameObject("Stopped car");var b=root.AddComponent<Rigidbody>();
            var t=new SandboxConnectionTransfer(new[]{b},new Pose(Vector3.zero,Quaternion.identity));
            t.Move(new Pose(Vector3.up*1000,Quaternion.Euler(0,180,0)));t.Restore();
            Assert.AreEqual(Vector3.zero,b.linearVelocity);Assert.AreEqual(Vector3.zero,b.angularVelocity);Assert.IsTrue(b.detectCollisions);
        }
        [Test]public void SweptGateAcceptsAirborneCrossingsButRejectsOutsideAperture()
        {
            root=new GameObject("Gate");var track=root.AddComponent<Level2BezierTrack>();
            track.Samples=new[]{Vector3.zero,Vector3.forward*10};track.TurnDistance=2;track.LowerDistance=8;
            var zone=root.AddComponent<SandboxSplineZone>();zone.BezierTrack=track;zone.GateWidth=3;zone.RebuildPath();
            Assert.IsTrue(zone.CrossesGate(new Vector3(0,1,-10),new Vector3(0,1,30),true));
            Assert.IsTrue(zone.CrossesGate(new Vector3(0,1,30),new Vector3(0,1,-10),false));
            Assert.IsFalse(zone.CrossesGate(new Vector3(2,1,-10),new Vector3(2,1,30),true));
            Assert.IsFalse(zone.CrossesGate(new Vector3(0,3,-10),new Vector3(0,3,30),true));
        }
    }
}

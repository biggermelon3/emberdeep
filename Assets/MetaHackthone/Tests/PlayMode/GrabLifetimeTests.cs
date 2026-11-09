using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PhysicalAssembly.Tests
{
    public sealed class GrabLifetimeTests
    {
        GameObject root;
        AssemblyWorld world;
        PartFactory factory;
        [SetUp] public void Setup()
        {
            Time.fixedDeltaTime=1f/60;
            root=new GameObject("Grab lifetime test"); world=root.AddComponent<AssemblyWorld>();
            factory=root.AddComponent<PartFactory>(); factory.World=world;
        }
        [TestCase(0)] [TestCase(1)] public void SandboxHoldSurvivesLossAndRequiresConfirmedOpening(int hand)
        {
            world.FloatingBuild=true;
            var part=factory.Create(PartKind.SmallCube,new Vector3(0,2,0),Quaternion.identity);
            var pose=new Pose(part.MainBody.Rigidbody.position,Quaternion.identity);var driver=new HandGrabDriver(world,hand);
            driver.Sample(true,.08f,false,pose,part,true,0);
            driver.Sample(true,.01f,true,pose,part,true,.1f);
            Assert.AreSame(part,world.HeldPart(hand));
            driver.Sample(false,0,false,default,null,false,10);Assert.AreSame(part,world.HeldPart(hand));
            driver.Sample(true,.04f,false,pose,part,true,11);Assert.AreSame(part,world.HeldPart(hand));
            driver.Sample(false,0,false,default,null,false,11.1f);
            driver.Sample(true,.04f,false,pose,part,true,12);Assert.AreSame(part,world.HeldPart(hand));
            driver.Sample(true,.04f,false,pose,part,true,12.09f);Assert.IsNull(world.HeldPart(hand));
            Assert.IsTrue(part.MainBody.Rigidbody.isKinematic);Assert.IsFalse(part.MainBody.Rigidbody.useGravity);
            Assert.AreEqual(1,part.MainBody.Rigidbody.linearDamping);
        }
        [TearDown] public void Cleanup()
        {
            if(world) foreach(var part in world.Parts.Values.ToArray()) if(part) Object.DestroyImmediate(part.gameObject);
            if(root) Object.DestroyImmediate(root);
        }
        [TestCase(false,0)] [TestCase(true,0)] [TestCase(false,1)] [TestCase(true,1)]
        public void CurledFingersCannotPreventImmediatePinchRelease(bool tripod,int hand)
        {
            var part=factory.Create(PartKind.SmallCube,new Vector3(0,2,0),Quaternion.identity);
            var pose=new Pose(part.MainBody.Rigidbody.position,Quaternion.identity);
            var driver=new HandGrabDriver(world,hand);
            float strength=GrabLatch.GripStrength(.4f,.4f,tripod?0:.4f,tripod?0:.4f,tripod?.05f:.09f);
            driver.Sample(true,.08f,false,pose,part,true,0);
            driver.Sample(true,.08f,false,pose,part,true,.01f,strength);
            Assert.IsNull(world.HeldPart(hand),"Finger curl alone cannot grab.");
            driver.Sample(true,.014f,false,pose,part,true,.02f,strength);
            Assert.AreSame(part,world.HeldPart(hand));
            pose.position+=Vector3.up*.2f;
            driver.Sample(true,.014f,false,pose,null,true,.1f,strength);
            Assert.Less(Vector3.Distance(pose.position,part.MainBody.Rigidbody.position),.0001f);
            var heldPosition=part.MainBody.Rigidbody.position;
            pose.position+=Vector3.right*.1f;
            driver.Sample(true,.025f,true,pose,null,true,.101f,strength);
            Assert.IsNull(world.HeldPart(hand));
            Assert.IsFalse(part.MainBody.Rigidbody.isKinematic);
            Assert.AreEqual(heldPosition,part.MainBody.Rigidbody.position,"The separated sample must not move the object before release.");
            driver.Sample(true,.03f,true,pose,part,true,.102f,strength);
            Assert.IsNull(world.HeldPart(hand),"A stale provider flag must not regrab.");
        }
        [TestCase(0)] [TestCase(1)] public void FistGrabMovesThenReleasesAtLastHeldPose(int hand)
        {
            var part=factory.Create(PartKind.SmallCube,new Vector3(0,2,0),Quaternion.identity);
            var pose=new Pose(part.MainBody.Rigidbody.position,Quaternion.identity);var driver=new HandGrabDriver(world,hand);
            driver.Sample(true,.08f,false,pose,part,true,0,0);
            driver.Sample(true,.08f,false,pose,part,true,.01f,1);Assert.AreSame(part,world.HeldPart(hand));
            pose.position+=Vector3.up*.1f;driver.Sample(true,.08f,false,pose,null,true,.02f,1);
            var before=part.MainBody.Rigidbody.position;pose.position+=Vector3.right;
            driver.Sample(true,.08f,false,pose,null,true,.03f,.8999f);
            Assert.IsNull(world.HeldPart(hand));Assert.AreEqual(before,part.MainBody.Rigidbody.position);
            driver.Sample(true,.08f,false,pose,part,true,.04f,1);Assert.AreSame(part,world.HeldPart(hand));
        }
        [TestCase(0)] [TestCase(1)]
        public void SphereDropsOnFirstSeparatedSample(int hand)
        {
            var sphere=factory.Create(PartKind.PhysicsSphere,new Vector3(0,2,0),Quaternion.identity);
            var pose=new Pose(sphere.MainBody.Rigidbody.position,Quaternion.identity);
            var driver=new HandGrabDriver(world,hand);
            driver.Sample(true,.06f,false,pose,sphere,true,0);
            driver.Sample(true,.014f,false,pose,sphere,true,.01f);
            Assert.AreSame(sphere,world.HeldPart(hand));
            driver.Sample(true,.025f,true,pose,null,true,.011f,1);
            Assert.IsNull(world.HeldPart(hand));
            Assert.IsFalse(sphere.MainBody.Rigidbody.isKinematic);
            Assert.IsTrue(sphere.MainBody.Rigidbody.useGravity);
            Assert.IsEmpty(world.Connections);
        }
        [Test] public void CompoundPartReleasesOnSeparationAndSurvivesBriefTrackingGap()
        {
            var wheel=factory.Create(PartKind.Wheel,new Vector3(0,2,0),Quaternion.identity);
            var origin=new Pose(wheel.MainBody.Rigidbody.position,Quaternion.identity);
            var before=wheel.Bodies.Select(b=>b.Rigidbody.position-origin.position).ToArray();
            var driver=new HandGrabDriver(world,0);
            driver.Sample(true,.06f,false,origin,wheel,true,0);
            driver.Sample(true,.014f,false,origin,wheel,true,.01f);
            Assert.AreSame(wheel,world.HeldPart(0));
            var moved=new Pose(new Vector3(1,2,1),Quaternion.Euler(0,90,0));
            driver.Sample(true,.014f,false,moved,null,true,.2f);
            for(int i=0;i<wheel.Bodies.Length;i++)
                Assert.That(Vector3.Distance(wheel.Bodies[i].Rigidbody.position,moved.position+moved.rotation*before[i]),Is.LessThan(.0001f));
            driver.Sample(true,.025f,false,moved,null,true,.25f);
            Assert.IsNull(world.HeldPart(0),"The first separated sample must release without a timer.");
            Assert.IsTrue(wheel.Bodies.All(b=>!b.Rigidbody.isKinematic && b.Rigidbody.interpolation==RigidbodyInterpolation.Interpolate));
            driver.Sample(true,.014f,false,moved,wheel,true,.30f);
            Assert.AreSame(wheel,world.HeldPart(0),"A fresh pinch can grab again.");
            driver.Sample(false,0,false,default,null,true,.31f);
            driver.Sample(true,.014f,false,moved,null,true,.45f);
            Assert.AreSame(wheel,world.HeldPart(0),"Brief optical occlusion must preserve ownership.");
            driver.Sample(true,.06f,false,moved,null,true,.50f);
            Assert.IsNull(world.HeldPart(0));
            Assert.IsTrue(wheel.Bodies.All(b=>!b.Rigidbody.isKinematic && b.Rigidbody.interpolation==RigidbodyInterpolation.Interpolate));
            driver.Sample(true,.014f,false,moved,wheel,true,.7f);
            Assert.AreSame(wheel,world.HeldPart(0));
            driver.Sample(false,0,false,default,null,true,1.1f);
            Assert.IsNull(world.HeldPart(0),"Prolonged tracking loss releases without attaching.");
        }
        [TestCase(0)] [TestCase(1)]
        public void PinchReleasesBeforeOpeningPoseAndCanRegrab(int hand)
        {
            var part=factory.Create(PartKind.SmallCube,new Vector3(0,2,0),Quaternion.identity);
            VerifyImmediateRelease(world,part,hand);
        }
        [TestCase(0)] [TestCase(1)] public void WorkshopPinchReleasesImmediately(int hand)
        {
            var go=new GameObject("Workshop pinch test");
            var workshop=go.AddComponent<WorkshopBootstrap>(); workshop.SpawnOnStart=false;
            var part=workshop.Factory.Create(PartKind.SmallCube,new Vector3(0,3,0),Quaternion.identity);
            try { VerifyImmediateRelease(workshop.World,part,hand); }
            finally
            {
                Object.DestroyImmediate(part.gameObject);
                Object.DestroyImmediate(workshop.Arena.gameObject);
                Object.DestroyImmediate(go);
            }
        }
        public static void VerifyImmediateRelease(AssemblyWorld world,AssemblyPart part,int hand)
        {
            var pose=new Pose(part.MainBody.Rigidbody.position,Quaternion.identity);
            var driver=new HandGrabDriver(world,hand);
            driver.Sample(true,.08f,true,pose,part,true,0,.4f);
            driver.Sample(true,.08f,true,pose,part,true,.01f,.4f);
            Assert.IsNull(world.HeldPart(hand),"Partial curl and stale pinch flags cannot grab.");
            driver.Sample(true,.014f,false,pose,part,true,.02f);
            Assert.AreSame(part,world.HeldPart(hand));
            pose.position+=Vector3.up*.2f;
            driver.Sample(true,.014f,false,pose,null,true,.1f);
            var held=new Pose(part.MainBody.Rigidbody.position,part.MainBody.Rigidbody.rotation);
            pose.position+=Vector3.right; pose.rotation=Quaternion.Euler(0,90,0);
            driver.Sample(true,.025f,true,pose,null,true,.11f,1);
            Assert.IsNull(world.HeldPart(hand));
            Assert.Less(Vector3.Distance(held.position,part.MainBody.Rigidbody.position),.0001f);
            Assert.Less(Quaternion.Angle(held.rotation,part.MainBody.Rigidbody.rotation),.001f);
            driver.Sample(true,.025f,false,held,part,true,.115f);
            driver.Sample(true,.014f,false,held,part,true,.12f);
            Assert.AreSame(part,world.HeldPart(hand));
            driver.Cancel();
        }
        [TestCase(0)] [TestCase(1)]
        public void CompoundPartPreservesTrackingGraceAndReleasesBeforeOpeningPose(int hand)
        {
            var wheel=factory.Create(PartKind.Wheel,new Vector3(0,2,0),Quaternion.identity);
            var origin=new Pose(wheel.MainBody.Rigidbody.position,Quaternion.identity);
            var before=wheel.Bodies.Select(b=>b.Rigidbody.position-origin.position).ToArray();
            var driver=new HandGrabDriver(world,hand);
            driver.Sample(true,.06f,false,origin,wheel,true,0);
            driver.Sample(true,.014f,false,origin,wheel,true,.01f);
            var moved=new Pose(new Vector3(1,2,1),Quaternion.Euler(0,90,0));
            driver.Sample(true,.014f,false,moved,null,true,.2f);
            for(int i=0;i<wheel.Bodies.Length;i++)
                Assert.Less(Vector3.Distance(wheel.Bodies[i].Rigidbody.position,moved.position+moved.rotation*before[i]),.0001f);
            driver.Sample(false,0,false,default,null,true,.3f);
            driver.Sample(true,.014f,false,moved,null,true,.45f);
            Assert.AreSame(wheel,world.HeldPart(hand),"Recovery with touching fingertips preserves latch.");
            driver.Sample(false,0,false,default,null,true,.5f);
            var positions=wheel.Bodies.Select(b=>b.Rigidbody.position).ToArray();
            driver.Sample(true,.025f,true,new Pose(Vector3.one*5,Quaternion.identity),null,true,.6f,1);
            Assert.IsNull(world.HeldPart(hand),"Separated recovery releases immediately.");
            for(int i=0;i<positions.Length;i++) Assert.Less(Vector3.Distance(positions[i],wheel.Bodies[i].Rigidbody.position),.0001f);
            Assert.IsTrue(wheel.Bodies.All(b=>!b.Rigidbody.isKinematic));
            driver.Sample(true,.025f,false,moved,wheel,true,.65f);
            driver.Sample(true,.014f,false,moved,wheel,true,.7f);
            Assert.AreSame(wheel,world.HeldPart(hand));
            driver.Sample(false,0,false,default,null,true,1.04f);
            Assert.AreSame(wheel,world.HeldPart(hand));
            driver.Sample(false,0,false,default,null,true,1.06f);
            Assert.IsNull(world.HeldPart(hand));
            driver.Sample(true,.014f,false,moved,wheel,true,1.1f);
            Assert.IsNull(world.HeldPart(hand),"Timeout requires reopening before regrab.");
            driver.Sample(true,.025f,false,moved,wheel,true,1.2f);
            driver.Sample(true,.014f,false,moved,wheel,true,1.3f);
            Assert.AreSame(wheel,world.HeldPart(hand));
            driver.Cancel();
        }
        [UnityTest] public IEnumerator AttachingToOtherHandKeepsOwnershipAndNewPartFollowsThatHand()
        {
            var target=factory.Create(PartKind.Block,new Vector3(0,2,0),Quaternion.identity);
            var source=factory.Create(PartKind.Block,new Vector3(.11f,2,0),Quaternion.identity);
            Assert.IsTrue(world.TryGrab(0,target,new Pose(target.MainBody.Rigidbody.position,Quaternion.identity)));
            Assert.IsTrue(world.TryGrab(1,source,new Pose(source.MainBody.Rigidbody.position,Quaternion.identity)));
            world.Release(1,true);
            Assert.IsTrue(world.IsBusy);
            for(int i=0;i<25;i++)
            {
                Assert.AreSame(target,world.HeldPart(0),"Attaching must not release the supporting hand.");
                world.SetHoldPose(0,new Pose(new Vector3(i*.01f,2,i*.003f),Quaternion.Euler(0,i*2,0)));
                yield return new WaitForFixedUpdate();
            }
            Assert.AreEqual(1,world.Connections.Count);
            Assert.IsFalse(world.IsBusy);
            world.SetHoldPose(0,new Pose(new Vector3(1,2,1),Quaternion.Euler(0,90,0)));
            Physics.SyncTransforms(); var edge=world.Connections[0];
            Assert.That(Vector3.Distance(world.Resolve(edge.a).transform.TransformPoint(edge.anchorA),world.Resolve(edge.b).transform.TransformPoint(edge.anchorB)),Is.LessThan(.002f));
            Assert.IsTrue(source.MainBody.Rigidbody.isKinematic);
            world.Release(0,false);
            Assert.IsFalse(source.MainBody.Rigidbody.isKinematic);
            Assert.IsFalse(target.MainBody.Rigidbody.isKinematic);
        }
        [UnityTest] public IEnumerator ReleasingSupportingHandDuringAlignmentRestoresDynamicBodies()
        {
            var target=factory.Create(PartKind.Block,new Vector3(0,2,0),Quaternion.identity);
            var source=factory.Create(PartKind.Block,new Vector3(.11f,2,0),Quaternion.identity);
            world.TryGrab(0,target,new Pose(target.MainBody.Rigidbody.position,Quaternion.identity));
            Assert.IsTrue(world.TryBeginAssembly(world.Query(source)));
            yield return new WaitForFixedUpdate();
            world.Release(0,false);
            yield return new WaitForSeconds(.35f);
            Assert.IsFalse(world.HasHolds); Assert.IsFalse(world.IsBusy);
            Assert.AreEqual(1,world.Connections.Count);
            Assert.IsFalse(target.MainBody.Rigidbody.isKinematic);
            Assert.IsFalse(source.MainBody.Rigidbody.isKinematic);
        }
    }
}

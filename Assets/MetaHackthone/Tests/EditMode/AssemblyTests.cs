using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace PhysicalAssembly.Tests
{
    public sealed class GestureTests
    {
        [TestCase(false)] [TestCase(true)]
        public void PinchHasOneContactBoundaryAndNoReleaseMargin(bool providerFlag)
        {
            var grab=new GrabLatch();
            Assert.IsFalse(grab.Update(true,.014f,providerFlag,0));
            Assert.IsFalse(grab.Update(true,.04f,providerFlag,0));
            Assert.IsFalse(grab.Update(true,.015001f,providerFlag,0));
            Assert.IsTrue(grab.Update(true,.015f,providerFlag,0));
            Assert.IsFalse(grab.Update(true,.015001f,providerFlag,1),"A fist or provider flag cannot prolong a pinch.");
            Assert.IsFalse(grab.Update(true,.08f,providerFlag,1),"Do not switch to fist after releasing a pinch.");
            grab.Update(true,.04f,false,0);
            Assert.IsTrue(grab.Update(true,.014f,false,0));
        }
        [Test] public void FullFistRequiresAllFingersAndThumbClosure()
        {
            Assert.AreEqual(1,GrabLatch.GripStrength(1,1,1,1,.025f));
            Assert.Less(GrabLatch.GripStrength(1,1,1,1,.06f),GrabLatch.FullGripThreshold);
            for(int finger=0;finger<4;finger++) {
                var curls=new[]{1f,1f,1f,1f};curls[finger]=.8f;
                Assert.Less(GrabLatch.GripStrength(curls[0],curls[1],curls[2],curls[3],.02f),GrabLatch.FullGripThreshold);
            }
            Assert.Less(GrabLatch.GripStrength(.4f,.4f,.4f,.4f,.025f),GrabLatch.FullGripThreshold);
        }
        [Test] public void FistMaintainsItsOwnLifetimeAndOpensImmediately()
        {
            var grab=new GrabLatch();
            Assert.IsFalse(grab.Update(true,.08f,false,1)); // closed at acquisition must open first
            grab.Update(true,.08f,false,0);
            Assert.IsTrue(grab.Update(true,.08f,false,1));
            Assert.IsTrue(grab.Update(true,.1f,false,1));
            Assert.IsFalse(grab.Update(true,.014f,true,.8999f),"Opening fist releases even if the fingertips form a pinch.");
            Assert.IsFalse(grab.Update(true,.014f,true,0));
            grab.Update(true,.04f,false,0);
            Assert.IsTrue(grab.Update(true,.014f,false,0));
        }
        [Test] public void MissingSamplesPreserveLatchUntilExplicitReset()
        {
            var grab=new GrabLatch();grab.Update(true,.08f,false,0);
            Assert.IsTrue(grab.Update(true,.014f,false,0));
            Assert.IsTrue(grab.Update(false,0,false,0));
            Assert.IsFalse(grab.Update(true,.015001f,true,1));
            grab.Reset();Assert.IsFalse(grab.Update(true,.014f,true,0));
            grab.Update(true,.04f,false,0);Assert.IsTrue(grab.Update(true,.014f,false,0));
        }
        [Test] public void FingerCurlIsScaleIndependentAndAcceptsPartialBend()
        {
            foreach(float scale in new[]{.7f,1f,1.3f})
            {
                var root=Vector3.one; var middle=root+Vector3.forward*.04f*scale;
                var tip=middle+new Vector3(.8f,0,.6f)*.04f*scale;
                Assert.That(GrabLatch.FingerCurl(root,middle,tip),Is.EqualTo(.4f).Within(.0001f));
                Assert.That(GrabLatch.FingerCurl(root,middle,middle+Vector3.forward*.04f*scale),Is.LessThan(.001f));
            }
            Assert.AreEqual(0,GrabLatch.FingerCurl(Vector3.zero,Vector3.zero,Vector3.zero));
        }
        [Test] public void JointDistancePinchWorksWithoutProviderFlagAndRejectsJitter()
        {
            var pinch=new PinchLatch();
            Assert.IsFalse(pinch.Update(true,.02f,false),"Require an open hand first.");
            Assert.IsFalse(pinch.Update(true,.06f,false));
            Assert.IsTrue(pinch.Update(true,.02f,false));
            Assert.IsTrue(pinch.Update(true,.035f,false),"Jitter within release hysteresis must not click again.");
            Assert.IsFalse(pinch.Update(true,.05f,false));
            Assert.IsFalse(pinch.Update(true,.035f,false));
        }
        [Test] public void PinchTrackingLossReleasesAndRequiresOpeningBeforeReactivation()
        {
            var pinch=new PinchLatch(); pinch.Update(true,.06f,false); pinch.Update(true,.02f,false);
            Assert.IsFalse(pinch.Update(false,0,false));
            Assert.IsFalse(pinch.Update(true,.02f,true));
            pinch.Update(true,.06f,false); Assert.IsTrue(pinch.Update(true,.035f,true));
            Assert.IsFalse(pinch.Update(true,.07f,true),"A stale provider flag must not latch a visibly open hand.");
        }
        [TestCase(false,Fingers.Index,HandCommand.Forward)]
        [TestCase(false,Fingers.Index|Fingers.Middle,HandCommand.Reverse)]
        [TestCase(true,Fingers.Index,HandCommand.Left)]
        [TestCase(true,Fingers.Index|Fingers.Middle,HandCommand.Right)]
        [TestCase(true,Fingers.Index|Fingers.Middle|Fingers.Thumb,HandCommand.Fire)]
        [TestCase(false,Fingers.Index|Fingers.Middle|Fingers.Thumb,HandCommand.None)]
        [TestCase(true,Fingers.Index|Fingers.Middle|Fingers.Ring,HandCommand.None)]
        public void ExactFingerPatterns(bool left,Fingers fingers,HandCommand expected) => Assert.AreEqual(expected,GestureRecognizer.Classify(left,fingers));
        [Test] public void DebounceAndTrackingLossRequireFreshActivation()
        {
            var g=new GestureRecognizer(); Assert.AreEqual(HandCommand.None,g.Update(true,false,Fingers.Index,0));
            Assert.AreEqual(HandCommand.None,g.Update(true,false,Fingers.Index,.10f));
            Assert.AreEqual(HandCommand.Forward,g.Update(true,false,Fingers.Index,.16f));
            Assert.AreEqual(HandCommand.None,g.Update(false,false,Fingers.Index,.17f));
            Assert.AreEqual(HandCommand.None,g.Update(true,false,Fingers.Index,.18f));
            Assert.AreEqual(HandCommand.Forward,g.Update(true,false,Fingers.Index,.34f));
        }
        [Test] public void FireTransitionNeverKeepsSteering()
        {
            var g=new GestureRecognizer(); g.Update(true,true,Fingers.Index|Fingers.Middle,0); g.Update(true,true,Fingers.Index|Fingers.Middle,.2f);
            Assert.AreEqual(HandCommand.None,g.Update(true,true,Fingers.Index|Fingers.Middle|Fingers.Thumb,.21f));
            Assert.AreEqual(HandCommand.Fire,g.Update(true,true,Fingers.Index|Fingers.Middle|Fingers.Thumb,.4f));
        }
        [Test] public void ReleaseHysteresisRejectsBriefJitterThenStops()
        {
            var g=new GestureRecognizer(); g.Update(true,false,Fingers.Index,0); g.Update(true,false,Fingers.Index,.2f);
            Assert.AreEqual(HandCommand.Forward,g.Update(true,false,Fingers.None,.21f));
            Assert.AreEqual(HandCommand.None,g.Update(true,false,Fingers.None,.28f));
        }
    }
    public sealed class AssemblyTests
    {
        GameObject root; AssemblyWorld world; PartFactory factory; readonly List<AssemblyPart> parts=new();
        [SetUp] public void Setup() { root=new GameObject("Test world"); world=root.AddComponent<AssemblyWorld>(); factory=root.AddComponent<PartFactory>(); factory.World=world; }
        [TearDown] public void Teardown() { foreach(var p in world.Parts.Values.ToArray()) if(p) UnityEngine.Object.DestroyImmediate(p.gameObject); UnityEngine.Object.DestroyImmediate(root); parts.Clear(); }
        AssemblyPart Part(PartKind kind=PartKind.Block) { var p=factory.Create(kind,new Vector3(parts.Count,2,0),Quaternion.identity); parts.Add(p); return p; }
        ConnectionRecord Join(AssemblyPart a,AssemblyPart b,Vector3? offset=null)
        {
            var pair=(from sa in a.Sockets from sb in b.Sockets where sa.Family==sb.Family && !world.SocketOccupied(sa) && !world.SocketOccupied(sb) select (sa,sb)).First();
            var c=new ConnectionRecord { id=Guid.NewGuid().ToString("N"),a=new BodyRef(a.InstanceId,pair.sa.Body.Id),b=new BodyRef(b.InstanceId,pair.sb.Body.Id),socketA=pair.sa.Id,socketB=pair.sb.Id,anchorA=pair.sa.transform.localPosition,anchorB=pair.sb.transform.localPosition };
            Assert.IsTrue(world.Connect(c)); return c;
        }
        [Test] public void GraphCycleAndParallelEdgesRemainConnectedUntilLastPathRemoved()
        {
            var a=Part(); var b=Part(); var c=Part(); var ab=Join(a,b); Join(b,c); Join(c,a); var second=Join(a,b,Vector3.up*.01f);
            world.RemoveConnection(ab.id); Assert.AreEqual(3,world.GetConnectedParts(a.InstanceId).Count);
            world.TryDetachPart(c); Assert.AreEqual(2,world.GetConnectedParts(a.InstanceId).Count);
            world.RemoveConnection(second.id); Assert.AreEqual(1,world.GetConnectedParts(a.InstanceId).Count);
        }
        [Test] public void DuplicateReverseEndpointConnectionIsRejected()
        {
            var a=Part(); var b=Part(); var c=Join(a,b);
            Assert.IsFalse(world.Connect(new ConnectionRecord {id="reverse",a=c.b,b=c.a,socketA=c.socketB,socketB=c.socketA,anchorA=c.anchorB,anchorB=c.anchorA}));
        }
        [Test] public void WheelDetachPreservesInternalAxleAndSuspension()
        {
            var chassis=Part(PartKind.Chassis); var wheel=Part(PartKind.Wheel); var other=Part(PartKind.Wheel);
            Join(chassis,wheel); Join(chassis,other); world.TryDetachPart(wheel);
            Assert.AreEqual(2,wheel.GetComponentsInChildren<Joint>().Length);
            Assert.AreEqual(3,wheel.Bodies.Length); Assert.AreEqual(1,world.GetConnectedParts(wheel.InstanceId).Count);
            Assert.AreEqual(2,world.GetConnectedParts(chassis.InstanceId).Count);
        }
        [Test] public void SecondHandCannotMoveSameConnectedAssembly()
        {
            var a=Part(); var b=Part(); Join(a,b);
            Assert.IsTrue(world.TryGrab(0,a,new Pose(Vector3.zero,Quaternion.identity)));
            Assert.IsFalse(world.TryGrab(1,b,new Pose(Vector3.zero,Quaternion.identity)));
            world.ReleaseAll(); Assert.IsFalse(a.MainBody.Rigidbody.isKinematic); Assert.IsFalse(b.MainBody.Rigidbody.isKinematic);
        }
        [Test] public void CollisionSuppressionRetainsOtherOwners()
        {
            var a=Part().MainBody.Colliders[0]; var b=Part().MainBody.Colliders[0];
            world.Collisions.Acquire(a,b,"one"); world.Collisions.Acquire(a,b,"two"); world.Collisions.Release("one");
            Assert.IsTrue(Physics.GetIgnoreCollision(a,b)); world.Collisions.Release("two"); Assert.IsFalse(Physics.GetIgnoreCollision(a,b));
        }
        [Test] public void SaveRoundTripRemapsIdsAndPreservesCompoundBodies()
        {
            var a=Part(PartKind.Chassis); var b=Part(PartKind.Wheel); Join(a,b);
            var service=root.AddComponent<AssemblySaveService>(); service.World=world; service.Factory=factory;
            var data=service.Capture(a); var json=JsonUtility.ToJson(data); var decoded=JsonUtility.FromJson<AssemblySaveV1>(json);
            var loaded=service.Restore(decoded,new Pose(new Vector3(5,3,0),Quaternion.Euler(0,90,0)));
            Assert.AreEqual(2,loaded.Count); Assert.IsFalse(loaded.Any(p=>p.InstanceId==a.InstanceId||p.InstanceId==b.InstanceId));
            Assert.AreEqual(2,world.GetConnectedParts(loaded[0].InstanceId).Count); Assert.AreEqual(3,loaded.Single(p=>p.Kind==PartKind.Wheel).Bodies.Length);
            Assert.AreEqual(a.InstanceId,data.parts[0].id);
        }
        [Test] public void InvalidSaveRejectedBeforeSpawning()
        {
            var a=Part(); var service=root.AddComponent<AssemblySaveService>(); service.World=world; service.Factory=factory;
            var save=service.Capture(a); save.parts[0].bodies[0].position=new Vector3(float.NaN,0,0);
            Assert.Throws<InvalidDataException>(()=>service.Restore(save,new Pose())); Assert.AreEqual(1,world.Parts.Count);
            save.parts[0].bodies[0].position=Vector3.zero; save.formatVersion=99;
            Assert.Throws<InvalidDataException>(()=>AssemblySaveService.Validate(save));
        }
        [Test] public void SaveBodyPosesUseConstructionFrame()
        {
            var part=Part(PartKind.Wheel); var service=root.AddComponent<AssemblySaveService>(); service.World=world;
            var save=service.Capture(part); Assert.Less(save.parts[0].bodies.Single(b=>b.id=="mount").position.magnitude,.0001f);
            Assert.Greater(save.parts[0].bodies.Single(b=>b.id=="wheel").position.magnitude,.04f);
        }
    }
}

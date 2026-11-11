using CloudGarden.Integration;
using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    public sealed class EmberdeepPortableAimPlayTests
    {
        GameObject head;
        [SetUp] public void Setup(){head=new GameObject("Portable aim head");head.transform.position=Vector3.up*1.65f;}
        [TearDown] public void Cleanup()=>Object.DestroyImmediate(head);
        [TestCase(true)] [TestCase(false)]
        public void LiveJointsProduceAimWithoutAnySdkPointerOrPointerUpdateEvent(bool left)
        {
            var input=new PortableJointAim();var index=new Vector3(left?-.2f:.2f,1.3f,.4f);
            Assert.IsTrue(input.TryRead(10,head.transform,left,index,index+Vector3.right*.02f,.04f,.02f,out var pose));
            Assert.That(Vector3.Distance(pose.position,index+Vector3.right*.01f),Is.LessThan(.001f));
            Assert.Less(Vector3.Angle(pose.rotation*Vector3.forward,ShoulderHandAim.Pointing(head.transform,left,pose.position)),.01f);
            Assert.IsTrue(input.TryRead(10,head.transform,left,index,index,.01f,.02f,out var sameFrame));
            Assert.AreEqual(pose,sameFrame,"Multiple UI/gameplay reads in a frame must use the same ray");
        }
        [Test] public void InvalidJointsAndTrackingResetCannotReuseAnOldRayOrDragRecoveryTowardIt()
        {
            var input=new PortableJointAim();var near=new Vector3(.2f,1.3f,.4f);var far=new Vector3(-.2f,1.4f,.6f);
            Assert.IsTrue(input.TryRead(1,head.transform,false,near,near,.04f,.02f,out _));
            Assert.IsFalse(input.TryRead(2,head.transform,false,new Vector3(float.NaN,0,0),near,.04f,.02f,out _));
            Assert.IsTrue(input.TryRead(3,head.transform,false,far,far,.04f,.02f,out var recovered));
            Assert.AreEqual(far,recovered.position);
            input.Reset();
            Assert.IsTrue(input.TryRead(4,head.transform,false,near,near,.04f,.02f,out recovered));
            Assert.AreEqual(near,recovered.position);
        }
        [Test] public void DroppingOneHandDoesNotInvalidateTheOtherHand()
        {
            var left=new PortableJointAim();var right=new PortableJointAim();var point=new Vector3(.2f,1.2f,.5f);
            Assert.IsTrue(right.TryRead(1,head.transform,false,point,point,.04f,.02f,out var expected));
            left.Reset();
            Assert.IsTrue(right.TryRead(1,head.transform,false,point,point,.04f,.02f,out var actual));
            Assert.AreEqual(expected,actual);
        }
        [Test] public void FocusAndPauseMustBothRecoverBeforeHandsCanResume()
        {
            var provider=head.AddComponent<MetaShadowPlayer>();provider.enabled=false;
            provider.Player=head.AddComponent<PhysicalAssembly.MetaXR.MetaPlayerRig>();
            Assert.IsTrue(provider.HeadReady);
            provider.SendMessage("OnApplicationPause",true);
            provider.SendMessage("OnApplicationFocus",true);
            Assert.IsFalse(provider.HeadReady,"A focus event must not override an outstanding pause");
            provider.SendMessage("OnApplicationFocus",false);
            provider.SendMessage("OnApplicationPause",false);
            Assert.IsFalse(provider.HeadReady,"A resume event must not override lost focus");
            provider.SendMessage("OnApplicationFocus",true);Assert.IsTrue(provider.HeadReady);
        }
        static PortableXRHandInput.Sample Native(bool tips=true)
        {
            var at=new Vector3(.2f,1.3f,.4f);
            return PortableXRHandInput.FromJoints(true,new Pose(at,Quaternion.identity),true,at,true,at+Vector3.right*.02f,
                tips,at,true,at+Vector3.right*.04f);
        }
        [Test] public void NativeTipLossRetainsAimButCannotProvidePinchOrOldJointValues()
        {
            var input=new PortableXRHandInput();input.Record(0,Native(),10,1);
            Assert.IsTrue(input.TryReadFresh(0,10,1,out var sample));Assert.IsTrue(sample.PinchAvailable);
            input.Record(0,Native(false),11,1.02f);
            Assert.IsTrue(input.TryReadFresh(0,11,1.02f,out sample));Assert.IsFalse(sample.PinchAvailable);
            var filter=new PortableJointAim();
            Assert.IsTrue(filter.TryRead(11,head.transform,true,sample.IndexBase,sample.ThumbBase,.08f,.02f,out _));
            input.Record(0,default,12,1.04f);
            Assert.IsFalse(input.TryReadFresh(0,12,1.04f,out _),"Invalid native update must clear, not reuse, the old ray");
        }
        [Test] public void NativeFreshnessHandlesUpdateOrderButRejectsStoppedSubsystemAndPause()
        {
            var input=new PortableXRHandInput();input.Record(0,Native(),20,1);
            Assert.IsTrue(input.TryReadFresh(0,21,1.02f,out _));
            Assert.IsFalse(input.TryReadFresh(0,22,1.04f,out _));
            Assert.IsFalse(input.TryReadFresh(0,21,1.2f,out _));
            input.Invalidate();Assert.IsFalse(input.TryReadFresh(0,20,1,out _));
            input.Record(0,Native(),21,1.02f);Assert.IsTrue(input.TryReadFresh(0,21,1.02f,out _));
            input.Record(0,Native(),22,float.NaN);Assert.IsFalse(input.TryReadFresh(0,22,1.04f,out _));
        }
        [Test] public void NativeLostHandDoesNotInvalidateOppositeHand()
        {
            var input=new PortableXRHandInput();input.Record(0,Native(),1,1);input.Record(1,Native(),1,1);
            input.Invalidate(0);Assert.IsFalse(input.TryReadFresh(0,1,1,out _));Assert.IsTrue(input.TryReadFresh(1,1,1,out _));
        }
        [Test] public void NativeTrackingSpaceConversionKeepsOffsetsAndPinchDistance()
        {
            head.transform.SetPositionAndRotation(new Vector3(2,1,-3),Quaternion.Euler(0,90,0));
            var local=Native();var world=local.ToWorld(head.transform);
            Assert.That(Vector3.Distance(world.IndexBase,head.transform.TransformPoint(local.IndexBase)),Is.LessThan(.0001f));
            Assert.That(world.Pinch,Is.EqualTo(.04f).Within(.0001f));
            Assert.That(Quaternion.Angle(world.Wrist.rotation,head.transform.rotation),Is.LessThan(.001f));
        }
        [Test] public void NativeMalformedOrMissingBaseJointsNeverBecomeAim()
        {
            var wrist=new Pose(Vector3.zero,Quaternion.identity);
            Assert.IsFalse(PortableXRHandInput.FromJoints(true,wrist,false,Vector3.zero,true,Vector3.one,true,Vector3.zero,true,Vector3.one).AimValid);
            Assert.IsFalse(PortableXRHandInput.FromJoints(true,wrist,true,new Vector3(float.NaN,0,0),true,Vector3.one,true,Vector3.zero,true,Vector3.one).AimValid);
            var sample=PortableXRHandInput.FromJoints(true,wrist,true,Vector3.zero,true,Vector3.one,true,new Vector3(float.NaN,0,0),true,Vector3.one);
            Assert.IsTrue(sample.AimValid);Assert.IsFalse(sample.PinchAvailable);
        }
    }
}

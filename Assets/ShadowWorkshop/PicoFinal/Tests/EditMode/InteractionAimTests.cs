using CloudGarden.Integration;
using NUnit.Framework;
using UnityEngine;

namespace ShapeOfMemory.Tests
{
    public sealed class InteractionAimTests
    {
        [Test] public void UninitializedAndStalePointersCannotAimButUpdateOrderAllowsOneFrame()
        {
            var sample=new InteractionAimFreshness();
            var pose=new Pose(new Vector3(.2f,1.2f,.4f),Quaternion.Euler(12,25,0));
            Assert.IsFalse(sample.TryRead(10,true,pose,out _),"A newly attached pointer has no proven pose");
            sample.Record(10,true);
            Assert.IsTrue(sample.TryRead(10,true,pose,out _));
            Assert.IsTrue(sample.TryRead(11,true,pose,out _),"Gameplay may run before this frame's XR update");
            Assert.IsFalse(sample.TryRead(12,true,pose,out _),"A stopped hand provider must not keep aiming");
            Assert.IsFalse(sample.TryRead(9,true,pose,out _));
        }

        [Test] public void ReadsFinalFilteredPoseAfterHandEventInsteadOfEarlierRuntimePose()
        {
            var sample=new InteractionAimFreshness();
            var native=new Pose(new Vector3(.4f,1,.5f),Quaternion.Euler(0,70,0));
            // The observer may run before ShoulderHandAim in the hand event. It
            // records freshness, not the still-native transform at that instant.
            sample.Record(25,true);
            var filtered=new Pose(native.position+Vector3.up*.025f,Quaternion.Euler(10,15,0));
            Assert.IsTrue(sample.TryRead(25,true,filtered,out var actual));
            Assert.That(Vector3.Distance(actual.position,filtered.position),Is.LessThan(.00001f));
            Assert.That(Quaternion.Angle(actual.rotation,filtered.rotation),Is.LessThan(.001f));
            Assert.Greater(Quaternion.Angle(actual.rotation,native.rotation),40);
        }

        [Test] public void TrackingLossAndSystemPauseRequireANewUpdateBeforeRecovery()
        {
            var sample=new InteractionAimFreshness();var pose=new Pose(Vector3.one,Quaternion.identity);
            sample.Record(8,true);Assert.IsTrue(sample.TryRead(8,true,pose,out _));
            Assert.IsFalse(sample.TryRead(8,false,pose,out _));
            Assert.IsFalse(sample.TryRead(8,true,pose,out _),"Tracking availability alone must not revive an old ray");
            sample.Record(9,true);Assert.IsTrue(sample.TryRead(9,true,pose,out _));
            sample.Invalidate();Assert.IsFalse(sample.TryRead(9,true,pose,out _),"System gesture/focus invalidation persists");
            sample.Record(10,true);Assert.IsTrue(sample.TryRead(10,true,pose,out _));
            sample.Record(10,false);Assert.IsFalse(sample.TryRead(10,true,pose,out _),"Missing aim joints invalidate the sample");
        }

        [Test] public void MalformedPosesAreRejectedAndHandsHaveIndependentFreshness()
        {
            var left=new InteractionAimFreshness();var right=new InteractionAimFreshness();
            var pose=new Pose(Vector3.zero,Quaternion.identity);left.Record(20,true);
            Assert.IsTrue(left.TryRead(20,true,pose,out _));Assert.IsFalse(right.TryRead(20,true,pose,out _));
            Assert.IsFalse(left.TryRead(20,true,new Pose(new Vector3(float.NaN,0,0),Quaternion.identity),out _));
            Assert.IsFalse(left.TryRead(20,true,new Pose(Vector3.zero,new Quaternion(0,0,0,0)),out _));
            Assert.IsFalse(left.TryRead(20,true,new Pose(Vector3.zero,new Quaternion(0,0,float.PositiveInfinity,1)),out _));
        }
    }
}

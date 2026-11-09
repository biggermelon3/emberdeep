using NUnit.Framework;
using UnityEngine;

namespace PhysicalAssembly.Tests
{
    public class LaunchGestureTests
    {
        static LaunchGesture.HandSample Gun => new() { Valid=true,Fingers=Fingers.Thumb|Fingers.Index,TipDistance=.07f };
        static LaunchGesture.HandSample Trigger => new() { Valid=true,Fingers=Fingers.Index,TipDistance=.02f };
        [Test] public void RequiresContinuousSecondAndLeftWinsTie()
        {
            var g=new LaunchGesture();
            Assert.AreEqual(LaunchGesture.Change.None,g.Sample(Gun,Gun,true,0));
            Assert.IsTrue(g.Reserved); Assert.AreEqual(LaunchGesture.Change.None,g.Sample(Gun,Gun,true,.999f));
            Assert.AreEqual(LaunchGesture.Change.Activated,g.Sample(Gun,Gun,true,1)); Assert.AreEqual(0,g.Owner);
        }
        [Test] public void InterruptedHoldRestartsAndEarlierRightOwns()
        {
            var g=new LaunchGesture(); g.Sample(Gun,default,true,0); g.Sample(default,Gun,true,.7f);
            g.Sample(Gun,Gun,true,.8f);
            Assert.AreEqual(LaunchGesture.Change.None,g.Sample(Gun,Gun,true,1));
            Assert.AreEqual(LaunchGesture.Change.Activated,g.Sample(Gun,Gun,true,1.71f)); Assert.AreEqual(1,g.Owner);
            Assert.AreEqual(LaunchGesture.Change.None,g.Sample(Trigger,Gun,true,1.8f));
            Assert.AreEqual(LaunchGesture.Change.Fired,g.Sample(Gun,Trigger,true,1.9f));
            Assert.IsTrue(g.Consumed(1)); Assert.IsFalse(g.Aiming);
            Assert.AreEqual(LaunchGesture.Change.None,g.Sample(default,Trigger,true,5));
        }
        [TestCase(false)] [TestCase(true)] public void ThumbFoldOrJointApproachFiresOnce(bool approach)
        {
            var g=new LaunchGesture(); g.Sample(Gun,default,true,0); g.Sample(Gun,default,true,1);
            var trigger=Trigger; if(approach) trigger.Fingers=Gun.Fingers;
            Assert.AreEqual(LaunchGesture.Change.Fired,g.Sample(trigger,default,true,1.1f));
            Assert.AreEqual(LaunchGesture.Change.None,g.Sample(trigger,default,true,3));
            g.Sample(Gun,default,true,4); Assert.IsFalse(g.Consumed(0));
            Assert.AreEqual(LaunchGesture.Change.Activated,g.Sample(Gun,default,true,5));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] public void InvalidTrackingPoseOrDisabledInputCancels(int reason)
        {
            var g=new LaunchGesture(); g.Sample(Gun,default,true,0); g.Sample(Gun,default,true,1);
            var sample=Gun; if(reason==0) sample.Valid=false; if(reason==1) sample.Fingers|=Fingers.Middle;
            Assert.AreEqual(LaunchGesture.Change.Cancelled,g.Sample(sample,default,reason!=2,1.1f));
            Assert.IsFalse(g.Reserved);
        }
        [TestCase(-90,5)] [TestCase(0,5)] [TestCase(40,40)] [TestCase(90,80)]
        public void ElevationClampsInRotatedArena(float input,float expected)
        {
            var arena=Quaternion.Euler(0,137,0);
            var direction=arena*new Vector3(0,Mathf.Sin(input*Mathf.Deg2Rad),Mathf.Cos(input*Mathf.Deg2Rad));
            var result=NarrowGapController.LimitAim(direction,Vector3.up,arena*Vector3.forward);
            Assert.That(Mathf.Asin(result.y)*Mathf.Rad2Deg,Is.EqualTo(expected).Within(.001f));
            Assert.That(Vector3.Angle(Vector3.ProjectOnPlane(result,Vector3.up),arena*Vector3.forward),Is.LessThan(.1f));
        }
    }
}

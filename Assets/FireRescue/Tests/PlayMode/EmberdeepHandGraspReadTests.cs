using System;
using System.Collections.Generic;
using NUnit.Framework;
using Oculus.Interaction.Input;
using UnityEngine;

namespace FireRescue.Tests
{
    public sealed class EmberdeepHandGraspReadTests
    {
        [TestCase(.5f,false)][TestCase(1f,true)][TestCase(1.5f,false)]
        public void WholeHandMeasureUsesTrackedJointsAtAnyScaleOrOrientation(float scale,bool left)
        {
            var hand=new SampleHand(scale,left,true);
            Assert.IsTrue(EmberdeepHandGrasp.TryRead(hand,out var strength,out var contact));
            Assert.That(strength,Is.EqualTo(1).Within(.001f));
            Assert.That(Vector3.Distance(contact,(hand.Joints[HandJointId.HandWristRoot].position+hand.Joints[HandJointId.HandMiddle1].position)*.5f),Is.LessThan(.00001f));
            var open=new SampleHand(scale,left,false);
            Assert.IsTrue(EmberdeepHandGrasp.TryRead(open,out strength,out _));Assert.Less(strength,.01f);
        }
        [Test] public void PinchingIndexAndThumbDoesNotPretendOtherFingersAreWrapped()
        {
            var hand=new SampleHand(1,false,false);
            hand.Joints[HandJointId.HandThumbTip]=hand.Joints[HandJointId.HandIndexTip];
            Assert.IsTrue(EmberdeepHandGrasp.TryRead(hand,out var strength,out _));Assert.Less(strength,.1f);
        }
        [Test] public void MissingLowConfidenceOrNonfiniteFingerCannotAcquireControl()
        {
            var hand=new SampleHand(1,false,true);hand.Confidence=false;
            Assert.IsFalse(EmberdeepHandGrasp.TryRead(hand,out _,out _));hand.Confidence=true;
            hand.Joints[HandJointId.HandRingTip]=new Pose(new Vector3(float.NaN,0,0),Quaternion.identity);
            Assert.IsFalse(EmberdeepHandGrasp.TryRead(hand,out _,out _));
            hand.Joints.Remove(HandJointId.HandRingTip);Assert.IsFalse(EmberdeepHandGrasp.TryRead(hand,out _,out _));
        }
        sealed class SampleHand:IHand
        {
            public readonly Dictionary<HandJointId,Pose> Joints=new();
            public bool Confidence=true;
            public SampleHand(float scale,bool left,bool curled)
            {
                Scale=scale;Handedness=left?Handedness.Left:Handedness.Right;
                var q=Quaternion.Euler(31,74,-18);var offset=new Vector3(.3f,1.1f,.6f);
                void Put(HandJointId id,Vector3 p)=>Joints[id]=new Pose(offset+q*(p*scale),q);
                Put(HandJointId.HandWristRoot,Vector3.zero);
                var roots=new[]{HandJointId.HandIndex1,HandJointId.HandMiddle1,HandJointId.HandRing1,HandJointId.HandPinky1};
                var mids=new[]{HandJointId.HandIndex2,HandJointId.HandMiddle2,HandJointId.HandRing2,HandJointId.HandPinky2};
                var tips=new[]{HandJointId.HandIndexTip,HandJointId.HandMiddleTip,HandJointId.HandRingTip,HandJointId.HandPinkyTip};
                for(int f=0;f<4;f++)
                {
                    var root=new Vector3((left?-1:1)*f*.015f,0,.04f);
                    Put(roots[f],root);Put(mids[f],root+Vector3.forward*.03f);
                    Put(tips[f],root+(curled?new Vector3(0,.01f,.01f):Vector3.forward*.06f));
                }
                Put(HandJointId.HandThumbTip,new Vector3(.02f,0,.04f));
            }
            public Handedness Handedness {get;}
            public float Scale {get;}
            public bool IsConnected=>true;public bool IsHighConfidence=>true;public bool IsTrackedDataValid=>true;
            public bool IsDominantHand=>false;public bool IsPointerPoseValid=>true;public int CurrentDataVersion=>1;
            public event Action WhenHandUpdated {add{} remove{}}
            public bool GetFingerIsHighConfidence(HandFinger finger)=>Confidence;
            public bool GetFingerIsPinching(HandFinger finger)=>false;public bool GetIndexFingerIsPinching()=>false;
            public float GetFingerPinchStrength(HandFinger finger)=>0;
            public bool GetRootPose(out Pose pose)=>GetJointPose(HandJointId.HandWristRoot,out pose);
            public bool GetPointerPose(out Pose pose)=>GetRootPose(out pose);
            public bool GetJointPose(HandJointId joint,out Pose pose)=>Joints.TryGetValue(joint,out pose);
            public bool GetJointPoseLocal(HandJointId joint,out Pose pose)=>GetJointPose(joint,out pose);
            public bool GetJointPoseFromWrist(HandJointId joint,out Pose pose)=>GetJointPose(joint,out pose);
            public bool GetJointPosesLocal(out ReadOnlyHandJointPoses poses){poses=default;return false;}
            public bool GetJointPosesFromWrist(out ReadOnlyHandJointPoses poses)=>GetJointPosesLocal(out poses);
            public bool GetPalmPoseLocal(out Pose pose)=>GetRootPose(out pose);
        }
    }
}

using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Oculus.Interaction.Input;
using PhysicalAssembly.MetaXR;
using UnityEngine;

namespace PhysicalAssembly.Tests
{
    public sealed class MetaGrabInputTests
    {
        static float ReadGrip(IHand hand) => (float)typeof(MetaHandInputAdapter)
            .GetMethod("ReadFullGrip",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{hand});

        [TestCase(.7f)] [TestCase(1f)] [TestCase(1.3f)]
        public void TrackedFistRequiresEveryFingerAndFoldedThumbAtAnyHandScale(float scale)
        {
            var hand=new JointHand(scale);
            Assert.That(ReadGrip(hand),Is.GreaterThanOrEqualTo(GrabLatch.FullGripThreshold));
            var fingers=new[]{HandJointId.HandIndexTip,HandJointId.HandMiddleTip,HandJointId.HandRingTip,HandJointId.HandPinkyTip};
            foreach(var tip in fingers)
            {
                var closed=hand.Joints[tip];
                hand.Joints[tip]=new Vector3(closed.x,0,.06f);
                Assert.That(ReadGrip(hand),Is.LessThan(GrabLatch.FullGripThreshold),tip.ToString());
                hand.Joints[tip]=closed;
            }
            hand.Joints[HandJointId.HandThumbTip]=new Vector3(-.07f,0,0);
            Assert.That(ReadGrip(hand),Is.LessThan(GrabLatch.FullGripThreshold),"An unfolded thumb cannot start a fist grab.");
            hand.Joints[HandJointId.HandThumbTip]=new Vector3(-.02f,0,0);
            hand.LowConfidence=HandFinger.Ring;
            Assert.AreEqual(0,ReadGrip(hand),"Unreliable finger data must not start a fist grab.");
            hand.LowConfidence=HandFinger.Invalid;
            hand.Joints.Remove(HandJointId.HandPinky2);
            Assert.AreEqual(0,ReadGrip(hand),"A missing joint must not start a fist grab.");
        }

        sealed class JointHand : IHand
        {
            public readonly Dictionary<HandJointId,Vector3> Joints=new();
            public HandFinger LowConfidence=HandFinger.Invalid;
            public JointHand(float scale)
            {
                Scale=scale;
                var roots=new[]{HandJointId.HandIndex1,HandJointId.HandMiddle1,HandJointId.HandRing1,HandJointId.HandPinky1};
                var mids=new[]{HandJointId.HandIndex2,HandJointId.HandMiddle2,HandJointId.HandRing2,HandJointId.HandPinky2};
                var tips=new[]{HandJointId.HandIndexTip,HandJointId.HandMiddleTip,HandJointId.HandRingTip,HandJointId.HandPinkyTip};
                for(int i=0;i<4;i++)
                {
                    Joints[roots[i]]=new Vector3(i*.02f,0,0);
                    Joints[mids[i]]=new Vector3(i*.02f,0,.03f);
                    Joints[tips[i]]=new Vector3(i*.02f,-.03f,.02f);
                }
                Joints[HandJointId.HandThumbTip]=new Vector3(-.02f,0,0);
            }
            public Handedness Handedness => Handedness.Left;
            public bool IsConnected => true;
            public bool IsTrackedDataValid => true;
            public bool IsHighConfidence => true;
            public bool IsDominantHand => false;
            public float Scale { get; }
            public bool IsPointerPoseValid => false;
            public int CurrentDataVersion => 0;
            public event System.Action WhenHandUpdated { add { } remove { } }
            public bool GetFingerIsPinching(HandFinger finger) => false;
            public bool GetIndexFingerIsPinching() => false;
            public bool GetFingerIsHighConfidence(HandFinger finger) => finger!=LowConfidence;
            public float GetFingerPinchStrength(HandFinger finger) => 0;
            public bool GetJointPose(HandJointId joint,out Pose pose)
            {
                bool found=Joints.TryGetValue(joint,out var local);
                var rotation=Quaternion.Euler(30,70,15);
                pose=new Pose(new Vector3(1,2,3)+rotation*(local*Scale),rotation);
                return found;
            }
            public bool GetPointerPose(out Pose pose) { pose=default; return false; }
            public bool GetJointPoseLocal(HandJointId joint,out Pose pose) { pose=default; return false; }
            public bool GetJointPoseFromWrist(HandJointId joint,out Pose pose) { pose=default; return false; }
            public bool GetPalmPoseLocal(out Pose pose) { pose=default; return false; }
            public bool GetRootPose(out Pose pose) { pose=default; return false; }
            public bool GetJointPosesLocal(out ReadOnlyHandJointPoses poses) { poses=default; return false; }
            public bool GetJointPosesFromWrist(out ReadOnlyHandJointPoses poses) { poses=default; return false; }
        }
    }
}

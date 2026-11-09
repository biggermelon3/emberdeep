using Oculus.Interaction.Input;
using PhysicalAssembly;
using UnityEngine;

namespace FireRescue
{
    // Read the existing SDK hand only. Reuse the car game's scale-independent
    // curl/whole-hand measurement; authored display bones never feed this path.
    public static class EmberdeepHandGrasp
    {
        static readonly HandJointId[] Roots={HandJointId.HandIndex1,HandJointId.HandMiddle1,HandJointId.HandRing1,HandJointId.HandPinky1};
        static readonly HandJointId[] Middles={HandJointId.HandIndex2,HandJointId.HandMiddle2,HandJointId.HandRing2,HandJointId.HandPinky2};
        static readonly HandJointId[] Tips={HandJointId.HandIndexTip,HandJointId.HandMiddleTip,HandJointId.HandRingTip,HandJointId.HandPinkyTip};
        public static bool TryRead(IHand hand,out float strength,out Vector3 contact)
        {
            strength=0;contact=default;
            if(hand==null || !hand.IsConnected || !hand.IsTrackedDataValid || !hand.IsHighConfidence
                || !float.IsFinite(hand.Scale) || hand.Scale<.1f || hand.Scale>3
                || !Joint(hand,HandJointId.HandWristRoot,out var wrist)
                || !Joint(hand,HandJointId.HandMiddle1,out var middleRoot))return false;
            contact=(wrist+middleRoot)*.5f;
            if(!hand.GetFingerIsHighConfidence(HandFinger.Thumb) || !Joint(hand,HandJointId.HandThumbTip,out var thumb)
                || !Joint(hand,HandJointId.HandIndex1,out var indexRoot))return false;
            float curl=1;
            for(int f=0;f<4;f++)
            {
                if(!hand.GetFingerIsHighConfidence((HandFinger)(f+1)) || !Joint(hand,Roots[f],out var root)
                    || !Joint(hand,Middles[f],out var middle) || !Joint(hand,Tips[f],out var tip))return false;
                if((middle-root).sqrMagnitude<1e-8f || (tip-middle).sqrMagnitude<1e-8f)return false;
                curl=Mathf.Min(curl,GrabLatch.FingerCurl(root,middle,tip));
            }
            strength=GrabLatch.GripStrength(curl,curl,curl,curl,Vector3.Distance(thumb,indexRoot)/hand.Scale);
            return float.IsFinite(strength);
        }
        static bool Joint(IHand hand,HandJointId joint,out Vector3 p)
        {
            bool found=hand.GetJointPose(joint,out var pose);p=pose.position;
            return found && float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z);
        }
    }
}

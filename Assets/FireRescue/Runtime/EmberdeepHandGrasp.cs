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
        public static bool TryReadPalmFrame(IHand hand,out Quaternion rotation)
        {
            rotation=Quaternion.identity;
            if(hand==null || !hand.IsTrackedDataValid || !hand.IsHighConfidence
                || !hand.GetJointPose(HandJointId.HandWristRoot,out var wrist)
                || !Joint(hand,HandJointId.HandMiddle1,out var middle))return false;
            var distal=middle-wrist.position;
            var palmar=wrist.rotation*(hand.Handedness==Handedness.Left?Constants.LeftPalmar:Constants.RightPalmar);
            if(!float.IsFinite(distal.sqrMagnitude) || !float.IsFinite(palmar.sqrMagnitude)
                || Vector3.Cross(distal,palmar).sqrMagnitude<.000001f)return false;
            rotation=Quaternion.LookRotation(distal,palmar);return true;
        }
        // Object manipulation measures four-finger closure independently from
        // thumb/index pinching. Keep the historical full-fist reader below intact.
        public static bool TryReadClosure(IHand hand,out float strength,out Vector3 contact)
        {
            strength=0;contact=default;
            if(hand==null || !hand.IsConnected || !hand.IsTrackedDataValid || !hand.IsHighConfidence
                || !Joint(hand,HandJointId.HandWristRoot,out var wrist) || !Joint(hand,HandJointId.HandMiddle1,out var middle))return false;
            contact=(wrist+middle)*.5f;
            for(int i=0;i<4;i++)
            {
                if(!hand.GetFingerIsHighConfidence((HandFinger)(i+1)) || !Joint(hand,Roots[i],out var a)
                    || !Joint(hand,Middles[i],out var b) || !Joint(hand,Tips[i],out var c)
                    || (b-a).sqrMagnitude<1e-8f || (c-b).sqrMagnitude<1e-8f)return false;
                strength+=GrabLatch.FingerCurl(a,b,c);
            }
            strength/=4;return true;
        }
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
        public static bool IsOpenPalm(IHand hand)
        {
            if(hand==null || !hand.IsTrackedDataValid || !hand.IsHighConfidence || !hand.GetFingerIsHighConfidence(HandFinger.Thumb))return false;
            for(int i=0;i<4;i++)
                if(!hand.GetFingerIsHighConfidence((HandFinger)(i+1)) || !Joint(hand,Roots[i],out var a)
                    || !Joint(hand,Middles[i],out var b) || !Joint(hand,Tips[i],out var c)
                    || (b-a).sqrMagnitude<1e-8f || (c-b).sqrMagnitude<1e-8f || GrabLatch.FingerCurl(a,b,c)>.24f)return false;
            return Joint(hand,HandJointId.HandThumb1,out var ta) && Joint(hand,HandJointId.HandThumb2,out var tb)
                && Joint(hand,HandJointId.HandThumbTip,out var tc) && (tb-ta).sqrMagnitude>1e-8f
                && (tc-tb).sqrMagnitude>1e-8f && GrabLatch.FingerCurl(ta,tb,tc)<.3f;
        }
        static readonly HandJointId[] AllTips={HandJointId.HandThumbTip,HandJointId.HandIndexTip,HandJointId.HandMiddleTip,HandJointId.HandRingTip,HandJointId.HandPinkyTip};
        static bool Curl(IHand hand,int finger,out float curl)
        {
            curl=0;
            if(!Joint(hand,Roots[finger],out var a) || !Joint(hand,Middles[finger],out var b) || !Joint(hand,Tips[finger],out var c)
                || (b-a).sqrMagnitude<1e-8f || (c-b).sqrMagnitude<1e-8f)return false;
            curl=GrabLatch.FingerCurl(a,b,c);return true;
        }
        // Gesture spells: palm frame, per-finger curls, the pointing line and every
        // tracked fingertip. A curled or covered finger is still a tracked joint here;
        // only the verified open palm keeps the high-confidence requirement.
        public static void ReadSpellFeatures(IHand hand,ref EmberdeepSpellHand result)
        {
            if(hand==null || !hand.IsConnected || !hand.IsTrackedDataValid)return;
            if(hand.GetJointPose(HandJointId.HandWristRoot,out var root) && Joint(hand,HandJointId.HandMiddle1,out var knuckle)
                && float.IsFinite(root.position.sqrMagnitude))
            {
                var distal=knuckle-root.position;
                var palmar=root.rotation*(hand.Handedness==Handedness.Left?Constants.LeftPalmar:Constants.RightPalmar);
                if(float.IsFinite(palmar.sqrMagnitude) && Vector3.Cross(distal,palmar).sqrMagnitude>.000001f)
                {result.Valid=true;result.Contact=(root.position+knuckle)*.5f;result.Frame=Quaternion.LookRotation(distal,palmar);}
            }
            result.Open=IsOpenPalm(hand);
            result.CurlsValid=Curl(hand,0,out result.Index) & Curl(hand,1,out result.Middle)
                & Curl(hand,2,out result.Ring) & Curl(hand,3,out result.Little);
            for(int finger=0;finger<5;finger++)
            {
                if(!Joint(hand,AllTips[finger],out var tip))continue;
                result.TipMask|=1<<finger;
                if(finger==0)result.ThumbTip=tip;else if(finger==1)result.IndexTip=tip;else if(finger==2)result.MiddleTip=tip;
                else if(finger==3)result.RingTip=tip;else result.LittleTip=tip;
            }
            result.PointValid=Joint(hand,HandJointId.HandWristRoot,out result.Wrist) & Joint(hand,HandJointId.HandIndex1,out result.IndexBase)
                && (result.TipMask&2)!=0;
        }
        static bool Joint(IHand hand,HandJointId joint,out Vector3 p)
        {
            bool found=hand.GetJointPose(joint,out var pose);p=pose.position;
            return found && float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z);
        }
    }
}

using Oculus.Interaction.Input;
using UnityEngine;

namespace FireRescue
{
    public enum EmberdeepHandPoseKind
    {
        Free, ExtinguisherGrip, PistolGrip, CannonGrip, LeverGrip, ValveGrip,
        ButtonPress, IntakeGrip, CannonPump
    }

    // Display-only values. Neither these curls nor the display anchor are fed to
    // IHand, interaction hit testing, pinch detection, or the held object's pose.
    public static class EmberdeepHandPose
    {
        public const float ContactAcquireSeconds=.16f;
        public const float ContactReleaseSeconds=.12f;
        public const float MaximumContactReach=.18f;

        // Anchor convention: +Z follows the tool/lever, +X the transverse handle
        // axis. This is a palm-centre frame, with forward toward the fingers and
        // up facing out of the palm. Offsets are metres, independent of mesh scale.
        public static Pose GripContact(EmberdeepHandPoseKind kind,bool left)
        {
            float side=left?-1:1;
            switch(kind)
            {
                case EmberdeepHandPoseKind.ExtinguisherGrip:
                    // Handle top is 26mm above Grip. Keep the solid palm above
                    // it; the wrist-side shift seats the curled fingers beside it.
                    return new Pose(new Vector3(side*.012f,.045f,-.006f),Quaternion.LookRotation(Vector3.left*side,Vector3.down));
                case EmberdeepHandPoseKind.PistolGrip:
                    return new Pose(new Vector3(side*.026f,-.006f,-.008f),
                        Quaternion.LookRotation(Quaternion.Euler(-12,0,0)*Vector3.forward,Vector3.left*side));
                case EmberdeepHandPoseKind.CannonGrip:
                    return new Pose(new Vector3(side*.028f,-.008f,-.01f),
                        Quaternion.LookRotation(Quaternion.Euler(-6,0,0)*Vector3.forward,Vector3.left*side));
                case EmberdeepHandPoseKind.ValveGrip:
                    return new Pose(new Vector3(side*.022f,.018f,-.022f),
                        Quaternion.LookRotation(new Vector3(-side*.15f,1,0),Vector3.forward));
                case EmberdeepHandPoseKind.LeverGrip:
                    return new Pose(new Vector3(side*.004f,.022f,-.008f),
                        Quaternion.LookRotation(Quaternion.Euler(0,-side*8,0)*Vector3.forward,Vector3.down));
                case EmberdeepHandPoseKind.CannonPump:
                    return new Pose(new Vector3(side*.009f,.023f,-.005f),
                        Quaternion.LookRotation(Quaternion.Euler(0,-side*12,0)*Vector3.forward,Vector3.down));
                case EmberdeepHandPoseKind.IntakeGrip:
                    return new Pose(new Vector3(side*.005f,.027f,-.004f),Quaternion.LookRotation(Vector3.forward,Vector3.down));
                default:return Pose.identity;
            }
        }

        // Ring/middle establish the hold first, then thumb and distal joints
        // close around it. SmoothStep reaches the final shape without overshoot.
        public static float ClosureWeight(HandJointId joint,float progress)
        {
            if(!IsArticulated(joint))return 0;
            var finger=HandJointUtils.JointToFingerList[(int)joint];
            float delay=finger==HandFinger.Thumb?.12f:finger==HandFinger.Index?.16f:finger==HandFinger.Middle?.04f:0;
            if(joint==HandJointId.HandIndex3 || joint==HandJointId.HandMiddle3 || joint==HandJointId.HandRing3 || joint==HandJointId.HandPinky3)delay+=.1f;
            float t=Mathf.Clamp01((progress-delay)/(1-delay));return t*t*(3-2*t);
        }

        public static float Curl(EmberdeepHandPoseKind kind, HandFinger finger, float amount=0)
        {
            amount=float.IsFinite(amount)?Mathf.Clamp01(amount):0;
            switch(kind)
            {
                case EmberdeepHandPoseKind.ExtinguisherGrip:
                    return finger==HandFinger.Thumb?.72f:finger==HandFinger.Index?.32f+.35f*amount:.82f;
                case EmberdeepHandPoseKind.PistolGrip:
                    return finger==HandFinger.Thumb?.84f:finger==HandFinger.Index?.15f+.58f*amount:.94f;
                case EmberdeepHandPoseKind.CannonGrip:
                    return finger==HandFinger.Thumb?.65f:finger==HandFinger.Index?.45f+.22f*amount:.69f;
                case EmberdeepHandPoseKind.LeverGrip:
                    return finger==HandFinger.Thumb?.88f:.84f+.1f*amount;
                case EmberdeepHandPoseKind.ValveGrip:
                    return finger==HandFinger.Thumb?.55f:finger==HandFinger.Index?.65f:.54f+.12f*amount;
                case EmberdeepHandPoseKind.ButtonPress:
                    return finger==HandFinger.Index?.02f+.12f*amount:finger==HandFinger.Thumb?.36f:.78f;
                case EmberdeepHandPoseKind.IntakeGrip:
                    return finger==HandFinger.Thumb?.68f:finger==HandFinger.Index?.71f:.6f;
                case EmberdeepHandPoseKind.CannonPump:
                    return finger==HandFinger.Thumb?.9f:.91f-.16f*amount;
                default:return 0;
            }
        }

        public static bool IsArticulated(HandJointId joint)
        {
            // OpenXR and legacy OVR have different numeric joint layouts; tips
            // are interleaved with metacarpals in OpenXR. Never use index ranges.
            switch(joint)
            {
                case HandJointId.HandThumb1:case HandJointId.HandThumb2:case HandJointId.HandThumb3:
                case HandJointId.HandIndex1:case HandJointId.HandIndex2:case HandJointId.HandIndex3:
                case HandJointId.HandMiddle1:case HandJointId.HandMiddle2:case HandJointId.HandMiddle3:
                case HandJointId.HandRing1:case HandJointId.HandRing2:case HandJointId.HandRing3:
                case HandJointId.HandPinky1:case HandJointId.HandPinky2:case HandJointId.HandPinky3:return true;
                default:return false;
            }
        }

        public static Quaternion JointRotation(EmberdeepHandPoseKind kind, HandJointId joint, Quaternion neutral, float amount=0,bool left=false)
        {
            int index=(int)joint;
            if(kind==EmberdeepHandPoseKind.Free || !IsArticulated(joint))return neutral;
            var finger=HandJointUtils.JointToFingerList[index];
            float curl=Curl(kind,finger,amount);
            float angle=finger==HandFinger.Thumb?38:joint==HandJointId.HandIndex1 || joint==HandJointId.HandMiddle1
                || joint==HandJointId.HandRing1 || joint==HandJointId.HandPinky1?66:82;
            // Derive from the SDK's COMPILED coordinate convention: OpenXR uses
            // +X flexion, legacy OVR -Z. Cross also handles handedness correctly.
            var distal=left?Constants.LeftDistal:Constants.RightDistal;
            var palmar=left?Constants.LeftPalmar:Constants.RightPalmar;
            var rotation=neutral*Quaternion.AngleAxis(angle*curl,Vector3.Cross(distal,palmar).normalized);
            if(joint==HandJointId.HandThumb1)
                rotation*=Quaternion.AngleAxis((left?24:-24)*curl,left?Constants.LeftDorsal:Constants.RightDorsal);
            return rotation;
        }
    }
}

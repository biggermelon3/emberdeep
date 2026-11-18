using Oculus.Interaction.Input;
using UnityEngine;

namespace FireRescue
{
    public enum EmberdeepHandPoseKind
    {
        Free, ExtinguisherGrip, PistolGrip, CannonGrip, LeverGrip, ValveGrip,
        ButtonPress, IntakeGrip, CannonPump, BodyGrip, NozzleGrip, ForwardSupport
    }

    // Contact frames are shared by carrying and presentation. Finger curls are
    // display-only and never feed IHand, gesture detection or trigger input.
    public static class EmberdeepHandPose
    {
        public const float ContactAcquireSeconds=.16f;
        public const float ContactReleaseSeconds=.12f;
        public const float MaximumContactReach=.18f;
        public const float MaximumContactAngle=85f;
        public static bool CanAlignPalm(Quaternion tracked,Quaternion target)
            =>Quaternion.Angle(tracked,target)<=MaximumContactAngle;

        // The physical palm and the displayed palm use exactly the same frame.
        public static Pose ObjectFromPalm(Pose palm,Pose contact)
        {
            var rotation=palm.rotation*Quaternion.Inverse(contact.rotation);
            return new Pose(palm.position-rotation*contact.position,rotation);
        }

        // Anchor convention: +Z follows the tool/lever, +X the transverse handle
        // axis. This is a palm-centre frame, with forward toward the fingers and
        // up facing out of the palm. Offsets are metres, independent of mesh scale.
        public static Pose GripContact(EmberdeepHandPoseKind kind,bool left)
        {
            float side=left?-1:1;
            switch(kind)
            {
                case EmberdeepHandPoseKind.ExtinguisherGrip:
                    // Approach the rear carrying handle from the matching side.
                    // A 15-degree relaxed wrist lets all fingers wrap under the
                    // fixed bar while the heel of the palm operates the upper lever.
                    return new Pose(new Vector3(side*.018f,.035f,-.004f),
                        Quaternion.LookRotation(new Vector3(-side*.966f,-.259f,0),new Vector3(side*.259f,-.966f,0)));
                case EmberdeepHandPoseKind.PistolGrip:
                    return new Pose(new Vector3(side*.026f,-.006f,-.008f),
                        Quaternion.LookRotation(Quaternion.Euler(-12,0,0)*Vector3.forward,Vector3.left*side));
                case EmberdeepHandPoseKind.CannonGrip:
                    return new Pose(new Vector3(side*.028f,-.008f,-.01f),
                        Quaternion.LookRotation(Quaternion.Euler(-6,0,0)*Vector3.forward,Vector3.left*side));
                case EmberdeepHandPoseKind.ValveGrip:
                    // At nine/three o'clock the wrists approach from below,
                    // fingers point up and each thumb points toward the wheel.
                    return new Pose(new Vector3(side*.006f,-.02f,-.025f),
                        Quaternion.LookRotation(Vector3.up,Vector3.forward));
                case EmberdeepHandPoseKind.LeverGrip:
                    return new Pose(new Vector3(side*.012f,.03f,-.012f),
                        Quaternion.LookRotation(Vector3.forward,Vector3.down));
                case EmberdeepHandPoseKind.CannonPump:
                    return new Pose(new Vector3(side*.009f,.023f,-.005f),
                        Quaternion.LookRotation(Quaternion.Euler(0,-side*12,0)*Vector3.forward,Vector3.down));
                case EmberdeepHandPoseKind.IntakeGrip:
                    return new Pose(new Vector3(side*.005f,.027f,-.004f),Quaternion.LookRotation(Vector3.forward,Vector3.down));
                case EmberdeepHandPoseKind.BodyGrip:
                    return new Pose(new Vector3(side*.065f,0,0),Quaternion.LookRotation(Vector3.forward,Vector3.left*side));
                case EmberdeepHandPoseKind.NozzleGrip:
                    // Barrel follows wrist-to-knuckles; palm faces the horn from
                    // the matching side. Curl provides the wrap, not a 90-degree
                    // rotation of the anatomical palm frame.
                    return new Pose(new Vector3(side*.032f,.004f,.025f),Quaternion.LookRotation(Vector3.forward,Vector3.left*side));
                case EmberdeepHandPoseKind.ForwardSupport:
                    return new Pose(new Vector3(side*.035f,-.015f,0),Quaternion.LookRotation(Vector3.forward,Vector3.left*side));
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
                    return finger==HandFinger.Thumb?.60f+.10f*amount:finger==HandFinger.Index?.58f+.12f*amount:
                        finger==HandFinger.Middle?.64f+.12f*amount:finger==HandFinger.Ring?.68f+.10f*amount:.73f+.08f*amount;
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
                case EmberdeepHandPoseKind.BodyGrip:
                    return finger==HandFinger.Thumb?.48f:finger==HandFinger.Index?.46f:.42f;
                case EmberdeepHandPoseKind.NozzleGrip:
                    return finger==HandFinger.Thumb?.78f:finger==HandFinger.Index?.86f:.89f;
                case EmberdeepHandPoseKind.ForwardSupport:
                    return finger==HandFinger.Thumb?.62f:finger==HandFinger.Index?.57f:.75f;
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

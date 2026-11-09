using System.Linq;
using Oculus.Interaction.Input;
using UnityEngine;
using Hand = Oculus.Interaction.Input.Hand;

namespace CloudGarden.Integration
{
    public sealed class MetaGardenHands : MonoBehaviour, IHandPoseProvider
    {
        public Transform Rig;
        Hand[] sources;
        OVRHand[] systemHands;
        bool focused = true;
        void OnApplicationFocus(bool value) => focused = value;
        void OnApplicationPause(bool value) => focused = !value;
        public GardenHandPose Read(bool left)
        {
            if (!focused || !Rig || !Rig.gameObject.activeInHierarchy) return default;
            if (sources == null) { sources = Rig.GetComponentsInChildren<Hand>(true); systemHands = Rig.GetComponentsInChildren<OVRHand>(true); }
            if (systemHands.Any(h => h && h.GetHand() == (left ? OVRPlugin.Hand.HandLeft : OVRPlugin.Hand.HandRight) && h.IsSystemGestureInProgress)) return default;
            var hand = sources.FirstOrDefault(h => h && h.isActiveAndEnabled && h.Handedness == (left ? Handedness.Left : Handedness.Right) && h.IsConnected && h.IsTrackedDataValid);
            if (!hand || !hand.GetRootPose(out var wrist) || !hand.GetJointPose(HandJointId.HandIndexTip, out var index)
                || !hand.GetJointPose(HandJointId.HandThumbTip, out var thumb) || !hand.GetJointPose(HandJointId.HandMiddle1, out var middleRoot)) return default;
            // Meta joint poses already use world space. Approximate the palm centre
            // halfway between the wrist and middle knuckle, matching XR Hands' curl scale.
            var palm = Vector3.Lerp(wrist.position, middleRoot.position, .5f);
            float length = Vector3.Distance(palm, wrist.position);
            bool valid = hand.GetJointPose(HandJointId.HandMiddleTip, out var middle)
                & hand.GetJointPose(HandJointId.HandRingTip, out var ring)
                & hand.GetJointPose(HandJointId.HandPinkyTip, out var little);
            valid &= length > .025f;
            float curl = valid ? (Curl(index.position, palm, length) + Curl(middle.position, palm, length)
                + Curl(ring.position, palm, length) + Curl(little.position, palm, length)) * .25f : 0;
            return new GardenHandPose { tracked = true, fingersValid = valid, curl = curl, palm = palm,
                tip = index.position, wrist = wrist.position,
                pinching = (hand.GetIndexFingerIsPinching() || Vector3.Distance(index.position, thumb.position) / Mathf.Max(.1f, hand.Scale) < .032f) && (!valid || curl < .8f) };
        }
        static float Curl(Vector3 tip, Vector3 palm, float length) => GardenHandPose.FingerCurl(tip, palm, length);
    }
}

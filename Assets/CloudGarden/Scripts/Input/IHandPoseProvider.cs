using UnityEngine;

namespace CloudGarden
{
    public struct GardenHandPose
    {
        public bool tracked, pinching;
        public bool fingersValid;
        public float curl;
        public Vector3 palm, tip, wrist;
        public static float FingerCurl(Vector3 tip, Vector3 palm, float palmLength)
            => 1 - Mathf.InverseLerp(.45f, 1.6f, Vector3.Distance(tip, palm) / Mathf.Max(.025f, palmLength));
    }
    public interface IHandPoseProvider { GardenHandPose Read(bool left); }
}

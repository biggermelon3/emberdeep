using UnityEngine;

namespace PhysicalAssembly
{
    // Pinch and full-fist grabs have independent lifetimes. Tracked distances
    // are normalized by hand scale; provider flags cannot prolong a pinch.
    public sealed class GrabLatch
    {
        public bool IsClosed { get; private set; }
        public const float CloseDistance=.015f,OpenDistance=CloseDistance;
        public const float FullGripThreshold=.9f;
        public const float ReleaseDistance=OpenDistance;
        bool armed, fistGrab;
        public static float FingerCurl(Vector3 root,Vector3 middle,Vector3 tip)
        {
            var lower=middle-root; var upper=tip-middle;
            if(lower.sqrMagnitude<1e-8f || upper.sqrMagnitude<1e-8f) return 0;
            return Mathf.Clamp01(1-Vector3.Dot(lower.normalized,upper.normalized));
        }
        public static float GripStrength(float index,float middle,float ring,float pinky,float thumbToIndexRoot)
        {
            float fourFingers=Mathf.Min(Mathf.Min(index,middle),Mathf.Min(ring,pinky));
            float thumb=Mathf.InverseLerp(.055f,.025f,thumbToIndexRoot);
            return Mathf.Min(fourFingers,thumb);
        }
        public bool Update(bool valid,float tipDistance,bool systemPinch,float gripStrength)
        {
            // The driver owns the tracking-loss timeout and calls Reset on cancellation.
            if(!valid) return IsClosed;
            bool pinch=tipDistance<=CloseDistance;
            bool fist=gripStrength>=FullGripThreshold;
            if(IsClosed) {
                IsClosed=fistGrab?fist:pinch;
                if(!IsClosed) armed=!pinch && !fist;
                return IsClosed;
            }
            if(!armed) { if(!pinch && !fist) armed=true; return false; }
            if(pinch || fist) { fistGrab=fist; IsClosed=true; }
            return IsClosed;
        }
        public void Reset() { armed=false; IsClosed=false; fistGrab=false; }
    }
}

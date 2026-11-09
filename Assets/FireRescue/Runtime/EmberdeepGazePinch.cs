using CloudGarden.ShadowPuzzle;
using UnityEngine;

namespace FireRescue
{
    // Legacy type name retained for compatibility. Activation requires a settled
    // target and a new pinch edge; acquiring a target under a held pinch is inert.
    // Hand-ray UI uses one instance per pointing hand with the existing gesture filter.
    public sealed class EmberdeepGazePinch
    {
        public const float DwellSeconds = .12f;
        readonly ShadowPinchGesture left = new ShadowPinchGesture();
        readonly ShadowPinchGesture right = new ShadowPinchGesture();
        int target;
        float dwell;

        public bool Sample(int targetId, bool available, bool leftTracked, float leftDistance,
            bool rightTracked, float rightDistance, float dt)
        {
            if (!available || targetId == 0 || float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0)
            {
                Reset();
                return false;
            }
            if (target != targetId)
            {
                Reset();
                target = targetId;
            }

            // A delayed frame cannot establish a stable target or open-hand rearm by itself.
            float observed = Mathf.Min(dt, .025f);
            dwell = Mathf.Min(DwellSeconds, dwell + observed);
            left.Sample(leftTracked && leftDistance >= 0, leftDistance, observed);
            right.Sample(rightTracked && rightDistance >= 0, rightDistance, observed);
            if (dwell < DwellSeconds || (!left.Pressed && !right.Pressed)) return false;

            // Consume both hands, including an open or just-about-to-pinch second hand.
            // Callers must also Reset on modal/page changes even if they reuse target IDs.
            Reset();
            return true;
        }

        public void Reset()
        {
            target = 0;
            dwell = 0;
            left.Reset();
            right.Reset();
        }
    }
}

using UnityEngine;

namespace CloudGarden
{
    public struct GardenGestureResult
    {
        public bool knead, pat, toggleRain;
        public Vector3 patPoint;
    }

    // Input-only state: no scene, clock or XR dependency, so recorded hands can exercise the same rules.
    public sealed class GardenGestureRecognizer
    {
        readonly GardenHandPose[] previous = new GardenHandPose[2];
        readonly float[] fistTime = new float[2], patDistance = new float[2], cooldown = new float[2];
        readonly bool[] fistLatched = new bool[2];
        float kneadDistance, kneadCooldown;

        public void Reset()
        {
            for (int i = 0; i < 2; i++)
            { previous[i] = default; fistTime[i] = patDistance[i] = cooldown[i] = 0; fistLatched[i] = false; }
            kneadDistance = kneadCooldown = 0;
        }

        public static bool IsFist(GardenHandPose hand) => hand.tracked && hand.fingersValid && hand.curl >= .8f && !hand.pinching;

        public GardenGestureResult Step(GardenHandPose left, GardenHandPose right, Vector3 center,
            Vector3 up, float reach, float dt, bool allowWeather, GardenSettings settings)
        {
            var result = new GardenGestureResult();
            if (dt <= 0 || dt > .1f)
            {
                for (int i = 0; i < 2; i++) { previous[i] = default; fistTime[i] = patDistance[i] = 0; }
                kneadDistance = 0; return result;
            }
            kneadCooldown = Mathf.Max(0, kneadCooldown - dt);
            bool leftContact = Contact(left, center, reach), rightContact = Contact(right, center, reach);
            UpdateHand(0, left, leftContact, up, dt, allowWeather, settings, ref result);
            UpdateHand(1, right, rightContact, up, dt, allowWeather, settings, ref result);

            if (leftContact && rightContact && Open(left) && Open(right)
                && previous[0].tracked && previous[1].tracked)
            {
                var dl = left.palm - previous[0].palm; var dr = right.palm - previous[1].palm;
                var span = right.palm - left.palm;
                // Shearing/rubbing counts; simply pulling the two hands apart remains a stretch.
                float shear = Vector3.ProjectOnPlane(dl - dr, span.normalized).magnitude;
                bool continuous = dl.magnitude <= dt * 2.5f && dr.magnitude <= dt * 2.5f;
                if (continuous && shear > dt * .12f && span.sqrMagnitude > .0025f)
                    kneadDistance += shear;
                else kneadDistance = Mathf.Max(0, kneadDistance - dt * .15f);
                if (kneadCooldown <= 0 && kneadDistance >= settings.kneadTravel)
                { result.knead = true; result.pat = false; kneadDistance = 0; kneadCooldown = settings.gestureCooldown; }
            }
            else kneadDistance = 0;
            previous[0] = left; previous[1] = right;
            return result;
        }

        void UpdateHand(int i, GardenHandPose hand, bool contact, Vector3 up, float dt,
            bool allowWeather, GardenSettings settings, ref GardenGestureResult result)
        {
            cooldown[i] = Mathf.Max(0, cooldown[i] - dt);
            // Only an observed open hand re-arms weather; tracking loss cannot re-trigger a held fist.
            if (hand.tracked && hand.fingersValid && hand.curl < .55f) fistLatched[i] = false;
            if (contact && allowWeather && IsFist(hand))
            {
                fistTime[i] += dt;
                if (!fistLatched[i] && fistTime[i] >= settings.fistHoldSeconds)
                { result.toggleRain = true; fistLatched[i] = true; }
            }
            else fistTime[i] = 0;

            if (!contact || !Open(hand) || !previous[i].tracked) { patDistance[i] = 0; return; }
            var delta = hand.palm - previous[i].palm;
            float down = -Vector3.Dot(delta, up.normalized);
            if (delta.magnitude <= dt * 2.5f && down > dt * .18f) patDistance[i] += down;
            else patDistance[i] = 0;
            if (cooldown[i] <= 0 && patDistance[i] >= settings.patTravel)
            { result.pat = true; result.patPoint = hand.palm; patDistance[i] = 0; cooldown[i] = settings.gestureCooldown; }
        }

        static bool Contact(GardenHandPose hand, Vector3 center, float reach)
            => hand.tracked && Vector3.Distance(hand.palm, center) <= reach;
        static bool Open(GardenHandPose hand) => hand.fingersValid && hand.curl < .55f && !hand.pinching;
    }
}

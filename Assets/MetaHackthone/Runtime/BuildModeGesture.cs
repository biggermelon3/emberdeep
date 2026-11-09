using System;

namespace PhysicalAssembly
{
    public enum ModeGestureDirection { Open, Close }

    // Pure state machine: hand tracking and presentation live outside the rules.
    public sealed class BuildModeGesture
    {
        public const Fingers Three = Fingers.Thumb | Fingers.Index | Fingers.Middle;
        public int Owner { get; private set; } = -1;
        public Fingers Completed { get; private set; }
        public bool Armed => Owner >= 0;
        public event Action<int, int> Step; // hand, action index (arming is zero)
        readonly float[] stableSince = { -1, -1 }, armSince = { -1, -1 }, lastSeen = { -1, -1 };
        readonly Fingers[] raw = new Fingers[2], stable = new Fingers[2];
        readonly bool[] needsNeutral = new bool[2];
        float armedAt, missingSince = -1;
        int step;
        public void Reset(bool requireNeutral = false)
        {
            Owner = -1; Completed = Fingers.None; missingSince = -1;
            for (int i = 0; i < 2; i++) { armSince[i] = stableSince[i] = lastSeen[i] = -1; raw[i] = stable[i] = Fingers.None; needsNeutral[i] = requireNeutral; }
        }
        public bool Sample(int hand, bool tracked, Fingers fingers, ModeGestureDirection direction, bool allowed, float now)
        {
            if (!allowed) { Reset(true); return false; }
            bool closing = direction == ModeGestureDirection.Close;
            Fingers start = closing ? Three : Fingers.Thumb;
            if (!tracked)
            {
                armSince[hand] = -1;
                if (Owner == hand)
                {
                    if (missingSince < 0) missingSince = now;
                    if (now - missingSince >= .35f) Reset(true);
                }
                return false;
            }
            if (Owner == hand && missingSince >= 0)
            {
                if(now-missingSince>=.35f) { Reset(true); return false; }
                armedAt += now - missingSince; missingSince = -1;
            }
            lastSeen[hand] = now;
            if (raw[hand] != fingers || stableSince[hand] < 0) { raw[hand] = fingers; stableSince[hand] = now; }
            if (now - stableSince[hand] < .12f) return false;
            stable[hand] = fingers;
            if (needsNeutral[hand]) { if (fingers != start) needsNeutral[hand] = false; return false; }
            if (!Armed)
            {
                if (fingers != start) { armSince[hand] = -1; return false; }
                if (armSince[hand] < 0) armSince[hand] = now;
                if (now - armSince[hand] < 2) return false;
                Owner = hand; armedAt = now; Completed = closing ? Fingers.None : Fingers.Thumb; step = 0;
                Step?.Invoke(hand, step++); return false;
            }
            if (Owner != hand) return false;
            if (now - armedAt > 8 || (fingers & (Fingers.Ring | Fingers.Pinky)) != 0 ||
                (!closing && (fingers & Completed) != Completed) ||
                (closing && (fingers & Completed) != 0)) { Reset(true); return false; }
            Fingers progress = closing ? Three & ~fingers : Three & fingers;
            for(int f=0;f<3;f++)
            {
                var finger=(Fingers)(1<<f);
                if ((progress & finger) != 0 && (Completed & finger) == 0)
                { Completed |= finger; Step?.Invoke(hand, step++); }
            }
            if (Completed != Three) return false;
            Reset(true); return true;
        }
    }
}

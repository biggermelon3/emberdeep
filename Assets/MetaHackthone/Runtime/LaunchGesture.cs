namespace PhysicalAssembly
{
    // Sample both hands together so ownership never depends on callback order.
    public sealed class LaunchGesture
    {
        public struct HandSample
        {
            public bool Valid;
            public Fingers Fingers;
            public float TipDistance;
            public bool Gun => Valid && Fingers == (Fingers.Thumb | Fingers.Index) && TipDistance > .04f;
            public bool Trigger => Valid && (Fingers == PhysicalAssembly.Fingers.Index ||
                Fingers == (PhysicalAssembly.Fingers.Thumb | PhysicalAssembly.Fingers.Index) && TipDistance <= .028f);
        }
        public enum Change { None, Activated, Fired, Cancelled }
        public int Owner { get; private set; } = -1;
        public bool Aiming => Owner >= 0;
        public bool Reserved => Aiming || since[0] >= 0 || since[1] >= 0;
        public float Progress { get; private set; }
        readonly float[] since = { -1, -1 };
        readonly bool[] consumed = new bool[2];
        public bool Consumed(int hand) => consumed[hand];
        public bool BlocksInput => Reserved || consumed[0] || consumed[1];
        public void Reset()
        {
            if (Owner >= 0) consumed[Owner] = true;
            Owner = -1; since[0] = since[1] = -1; Progress = 0;
        }
        public Change Sample(HandSample left, HandSample right, bool allowed, float now)
        {
            Release(0, left); Release(1, right);
            if (!allowed) { bool active = Reserved; Reset(); return active ? Change.Cancelled : Change.None; }
            if (Aiming)
            {
                var hand = Owner == 0 ? left : right;
                if (hand.Trigger) { Reset(); return Change.Fired; }
                if (!hand.Gun) { Reset(); return Change.Cancelled; }
                return Change.None;
            }
            bool wasReserved=Reserved;
            Arm(0, left, now); Arm(1, right, now);
            int winner = -1;
            for (int i = 0; i < 2; i++)
                if (since[i] >= 0 && now - since[i] >= 1 && (winner < 0 || since[i] < since[winner])) winner = i;
            Progress = System.Math.Max(since[0] < 0 ? 0 : now - since[0], since[1] < 0 ? 0 : now - since[1]);
            if (winner < 0) return wasReserved && !Reserved ? Change.Cancelled : Change.None;
            Owner = winner; since[0] = since[1] = -1; Progress = 1; return Change.Activated;
        }
        void Arm(int i, HandSample hand, float now)
        { if (!hand.Gun || consumed[i]) since[i] = -1; else if (since[i] < 0) since[i] = now; }
        void Release(int i, HandSample hand)
        { if (hand.Valid && hand.TipDistance >= .045f && (hand.Fingers & Fingers.Thumb) != 0) consumed[i] = false; }
    }
}

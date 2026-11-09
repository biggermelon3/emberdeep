using UnityEngine;

namespace FireRescue
{
    // The showcase has its own two-scene route and keeps the expedition in memory
    // while visiting camp. No other game mode is part of this session.
    public static class EmberdeepSession
    {
        public const string CampScene="EmberdeepCamp", FireScene="EmberdeepFireground";
        public static FireRun Run { get; private set; } = new FireRun();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void NewExpedition()=>Run=new FireRun();
    }
}

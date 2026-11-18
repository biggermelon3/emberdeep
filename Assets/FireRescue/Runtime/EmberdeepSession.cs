using UnityEngine;

namespace FireRescue
{
    // The showcase has its own two-scene route and keeps the expedition in memory
    // while visiting camp. No other game mode is part of this session.
    public static class EmberdeepSession
    {
        public const string CampScene="EmberdeepCamp", FireScene="EmberdeepFireground";
        // Optional playtest switch: every spell owned from the first level. Off by
        // default so spells, crystals and their lessons arrive one purchase at a time.
        public static bool PlaytestAllSpells=false;
        public static FireRun Run { get; private set; } = new FireRun(goddessTutorial:true){PlaytestAllSpells=PlaytestAllSpells};
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void NewExpedition()=>Run=new FireRun(goddessTutorial:true){PlaytestAllSpells=PlaytestAllSpells};
#if UNITY_EDITOR
        public static void BeginEquipmentTestRun()=>Run=new FireRun();
#endif
    }
}

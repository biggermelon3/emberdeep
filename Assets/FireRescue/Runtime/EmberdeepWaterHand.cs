using UnityEngine;

namespace FireRescue
{
    // LoopSpec §3.6: which hand aims and wears the gem. Left by default. The camp settings
    // page (workstream O) will call Set; until then the fireground has a dev key.
    // The tray (L3) should read Left to mirror itself.
    public static class EmberdeepWaterHand
    {
        public const string PrefsKey="Emberdeep.WaterHand";
        public const int LeftValue=0, RightValue=1;
        static int cached=-1;
        public static event System.Action Changed;
        public static bool Left
        {
            get
            {
                if(cached<0)cached=PlayerPrefs.GetInt(PrefsKey,LeftValue)==RightValue?RightValue:LeftValue;
                return cached==LeftValue;
            }
        }
        // 0 = left hand, 1 = right hand, matching every hand array in FireRescueGame.
        public static int Index=>Left?0:1;
        public static int OtherIndex=>Left?1:0;
        public static void Set(bool left)
        {
            int value=left?LeftValue:RightValue;
            if(cached==value && PlayerPrefs.GetInt(PrefsKey,LeftValue)==value)return;
            cached=value;PlayerPrefs.SetInt(PrefsKey,value);PlayerPrefs.Save();
            Changed?.Invoke();
        }
        public static void Toggle()=>Set(!Left);
        // Tests and domain reloads re-read the stored value.
        public static void Reload()=>cached=-1;
        public static string Name(bool left)=>left?"left":"right";
        public static string WaterName=>Name(Left);
        public static string OtherName=>Name(!Left);
    }
}

using UnityEngine;

namespace FireRescue
{
    // Plan-phase voice-over (owner, 2026-11-22). The camp's forest spirit introduces towers in TOWER HILL and wind in
    // WINDY RIDGE: one line each, once per install, as that level's Plan phase starts. Plan time is frozen until the bell,
    // so the line always finishes.
    // - SHOW HINTS and How to play (EmberdeepFirstHints.Reset) bring the lines back.
    // - A line whose clip is missing never plays: FireRescueGame.Spirit.cs then never creates the spirit (ship-safe).
    public static class EmberdeepPlanSpirit
    {
        public const int TowerStage=2,WindStage=3; // FireRun.LevelNames[2] TOWER HILL, [3] WINDY RIDGE
        public const string KeyPrefix="Emberdeep.Spirit.v1.";
        public static string Key(EmberdeepSpiritLine line)=>KeyPrefix+line.Id;
        public static bool Heard(EmberdeepSpiritLine line)=>PlayerPrefs.GetInt(Key(line),0)==1;
        public static void MarkHeard(EmberdeepSpiritLine line){PlayerPrefs.SetInt(Key(line),1);PlayerPrefs.Save();}
        public static void Forget(EmberdeepSpiritLine line){PlayerPrefs.DeleteKey(Key(line));PlayerPrefs.Save();}
        // The Plan as the player sees it: the board in Plan with no screen over it. The level select lays the next or browsed
        // level out in Plan behind its carousel (results NEXT starts that Plan, then opens the select), and the upgrades can
        // stand over a Plan board too, so Preparing alone also holds there (owner, PICO build 2026-11-22: the lines started
        // on the level select).
        public static bool BoardInView(bool planning,bool levelSelectOpen,bool upgradesOpen)=>planning && !levelSelectOpen && !upgradesOpen;
        // The Plan view ended mid-line. A screen put over the board cut it, so it was not heard and comes back with the
        // next Plan. The bell, or leaving the level, keep it heard (once per install).
        public static bool CutByScreen(bool speaking,bool levelSelectOpen,bool upgradesOpen)=>speaking && (levelSelectOpen || upgradesOpen);
        public static void ResetAll(){foreach(var l in EmberdeepSpiritLines.Plan)PlayerPrefs.DeleteKey(Key(l));PlayerPrefs.Save();}
        // Subscribed at startup in every scene (the camp's SHOW HINTS must clear them too). Idempotent.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Hook(){EmberdeepFirstHints.Reset-=ResetAll;EmberdeepFirstHints.Reset+=ResetAll;}
        public static EmberdeepSpiritLine LineFor(int stage)=>stage==TowerStage?EmberdeepSpiritLines.PlanTower:stage==WindStage?EmberdeepSpiritLines.PlanWind:null;
        // The line to say at this level's Plan start, or null: a topic level, not heard yet, and its clip is present.
        public static EmberdeepSpiritLine Due(int stage,System.Func<EmberdeepSpiritLine,bool> hasClip)
        {
            var line=LineFor(stage);
            return line!=null && !Heard(line) && hasClip!=null && hasClip(line)?line:null;
        }
    }
}

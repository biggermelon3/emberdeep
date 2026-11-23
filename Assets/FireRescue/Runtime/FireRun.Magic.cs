using UnityEngine;

namespace FireRescue
{
    public sealed partial class FireRun
    {
        public bool GoddessTutorial { get; }
        // LoopSpec §8: 6 stages. The camp tutorial (§9) teaches the palm, the pump and the sheep first, so one palm
        // lesson (palm water and sheep: the old PALM RESCUE grove) is left before the five incidents. Only Stage
        // numbering moved: IncidentIndex still means the five incidents 0-4 (piece sets and boards key on it).
        public const int PalmLessons=1;
        public const int PalmLessonTrees=5,PalmLessonColumns=3,PalmLessonStarts=3;
        public const float PalmLessonHeat=84;
        public bool IsPalmLesson => GoddessTutorial && Stage<PalmLessons;
        public int IncidentIndex => GoddessTutorial?Mathf.Max(0,Stage-PalmLessons):Stage;
        public int StageCount => GoddessTutorial?PalmLessons+Names.Length:Names.Length;
        public string StageName => StageNameFor(GoddessTutorial,Stage);
        // Emberdeep level names (LoopSpec §13): place names, one per stage (palm lesson + the five incidents). The legacy
        // tool flow keeps FireRun.Names. Phrase-matched in Translations.tsv.
        public static readonly string[] LevelNames={"SHEEP MEADOW","WELL HOLLOW","TOWER HILL","WINDY RIDGE","HAYFIELD","EMBERDEEP"};
        public static string StageNameFor(bool goddessTutorial,int stage)=>goddessTutorial?LevelNames[Mathf.Clamp(stage,0,LevelNames.Length-1)]:
            Names[Mathf.Clamp(stage,0,Names.Length-1)];
        // Palm water is the first item, so the next spell opens once the palm lesson is cleared.
        public int EquipmentIncidents => GoddessTutorial?Mathf.Max(0,ClearedIncidents-(PalmLessons-1)):ClearedIncidents;
        // The forced "buy your first magic upgrade" shop lesson after lesson 1 is cut with the stage cut: stage 2 now
        // teaches the pump when the gem first runs low (a forced bigger gem right before works against that), and
        // the lesson was a text instruction. Kept as a property so old call sites compile; it never blocks now.
        public bool NeedsMagicUpgrade => false;
        // A board that stands still: no burn-down, no spread, no sheep catching fire. It was the old single-tree first
        // lesson, which the camp tutorial replaced; the remaining palm lesson is a live grove. Kept for LV.
        public bool StillLesson => false;
        public bool PalmTutorialComplete {get;private set;}
        // LoopSpec §3: in the production (goddess) flow the gem on the water hand is the one
        // water store. It refills passively after a pause, and the fist-hammer pump tops it up.
        // The legacy tool flow keeps its time-recharged palm water unchanged.
        public bool GemMode => GoddessTutorial;
        // Seconds for the passive refill to fill an empty gem (shop text).
        public float MagicFillSeconds => GemMode?MagicCapacity/EmberdeepWaterTuning.PassivePerSecond:MagicLevel==0?5:10;
        public float NextMagicFillSeconds => GemMode?(MagicCapacity+EmberdeepWaterTuning.GemCapacityPerLevel)/EmberdeepWaterTuning.PassivePerSecond:10;
        public void CompletePalmTutorial(){PalmTutorialComplete=true;Learn(EmberdeepInteractionDemo.Action.Goddess);}
        public float NextMagicRecharge => (MagicCapacity+20)/10f;
        public int MagicLevel { get; private set; }
        public float MagicCapacity => GemMode?EmberdeepWaterTuning.GemCapacity+MagicLevel*EmberdeepWaterTuning.GemCapacityPerLevel:30+MagicLevel*20;
        public float MagicWater { get; private set; }=30;
        public const float MagicFlow=EmberdeepWaterTuning.GemFlow;
        public float MagicPower => 45+MagicLevel*18;
        public float MagicRecharge => GemMode?EmberdeepWaterTuning.PassivePerSecond:MagicCapacity/MagicFillSeconds;
        public float MagicRadius => .82f+MagicLevel*.12f;
        public float MagicRange => (14+MagicLevel*3)*FireMiniature.Scale;
        public int MagicUpgradeCost => MagicLevel<4?75+MagicLevel*100:0;
        public bool CanUpgradeMagic => InSupply && !Supply.Locked && MagicUpgradeCost>0 && Coins>=MagicUpgradeCost;
        public float MagicConsumed { get; private set; }
        // Water the pump has put into the gem; passive refill is not counted.
        public float MagicPumped { get; private set; }
        public bool GemEmpty => MagicWater<.01f;
        public float GemFill01 => Mathf.Clamp01(MagicWater/Mathf.Max(1,MagicCapacity));
        // Seconds since the gem last sprayed; passive refill runs once this passes the delay.
        public float GemIdleSeconds { get; private set; }
        public bool GemPassiveRefilling => GemMode && GemIdleSeconds>=EmberdeepWaterTuning.PassiveDelaySeconds && MagicWater<MagicCapacity
            && (Phase==FirePhase.Fighting || Phase==FirePhase.Cleared);
        public bool BuyMagicUpgrade()
        {
            if(!CanUpgradeMagic)return false;
            Coins-=MagicUpgradeCost;MagicLevel++;return true;
        }
        // Independent from every tank and manual supply transaction. No wall clock catch-up.
        // Partial charge remains usable, so lifting the support hand never wastes a charge.
        // Gem mode: touching the gem counts as spraying even when it is dry, so the passive
        // refill only starts PassiveDelaySeconds after the fingertip lifts.
        public float TickMagic(float dt,bool cast,bool eligible)
        {
            if(!eligible || !float.IsFinite(dt) || dt<=0 || (Phase!=FirePhase.Fighting && Phase!=FirePhase.Cleared))return 0;
            dt=Mathf.Min(dt,.1f);
            if(cast && Phase==FirePhase.Fighting && !Supply.Locked)
            {
                GemIdleSeconds=0;
                float used=Mathf.Min(MagicWater,MagicFlow*dt);
                MagicWater-=used;MagicConsumed+=used;return used*MagicPower/MagicFlow;
            }
            if(GemMode)
            {
                float waited=GemIdleSeconds;GemIdleSeconds=Mathf.Min(GemIdleSeconds+dt,1000);
                float refill=GemIdleSeconds-Mathf.Max(waited,EmberdeepWaterTuning.PassiveDelaySeconds);
                // Summed frame times overshoot the exact wait by float rounding (15 x .1f > 1.5);
                // that sliver is not a real frame of refill.
                if(refill<.0001f)refill=0;
                if(refill>0)MagicWater=Mathf.Min(MagicCapacity,MagicWater+EmberdeepWaterTuning.PassivePerSecond*refill);
                if(MagicCapacity-MagicWater<.001f)MagicWater=MagicCapacity; // float drift never leaves it a hair short of full
                return 0;
            }
            MagicWater=Mathf.Min(MagicCapacity,MagicWater+MagicRecharge*dt);return 0;
        }
        // One accepted pump strike. Returns the units the gem actually took (it stops at full).
        public float PumpGem(float units)
        {
            if(!GemMode || Phase!=FirePhase.Fighting || Supply.Locked || !PositiveFinite(units))return 0;
            float accepted=Mathf.Min(units,Mathf.Max(0,MagicCapacity-MagicWater));
            MagicWater+=accepted;MagicPumped+=accepted;return accepted;
        }
        // LoopSpec §3.5: a level always starts with a full gem; no pumping before it.
        void FillGemForStage(){MagicWater=MagicCapacity;GemIdleSeconds=EmberdeepWaterTuning.PassiveDelaySeconds;}
    }
}

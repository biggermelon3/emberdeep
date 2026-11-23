using UnityEngine;

namespace FireRescue
{
    // LoopSpec §7 slim progression and the §11b level select, kept apart from the board sim in FireRun.cs.
    // Gem: GODDESS HAND levels (MagicLevel, FireRun.Magic). Pump: strike water per level here.
    // Piece slot and tower radius tiles arrive with L3.
    public sealed partial class FireRun
    {
        public const int PumpLevels=3;
        public const float PumpWaterPerLevel=1,PumpBeatWaterPerLevel=1.5f;
        static readonly int[] PumpPrices={60,140,260};
        public int PumpLevel {get;private set;}
        public int PumpUpgradeCost=>PumpLevel<PumpLevels?PumpPrices[PumpLevel]:0;
        public bool CanUpgradePump=>GemMode && InSupply && !Supply.Locked && !NeedsMagicUpgrade && PumpUpgradeCost>0 && Coins>=PumpUpgradeCost;
        public float PumpStrikeWater=>EmberdeepWaterTuning.StrikeWater+PumpLevel*PumpWaterPerLevel;
        public float PumpBeatWater=>EmberdeepWaterTuning.OnBeatWater+PumpLevel*PumpBeatWaterPerLevel;
        public float PumpStrikeUnits(bool onBeat)=>onBeat?PumpBeatWater:PumpStrikeWater;
        public bool BuyPumpUpgrade()
        {
            if(!CanUpgradePump)return false;
            Coins-=PumpUpgradeCost;PumpLevel++;return true;
        }

        // Every stage up to the furthest one reached, plus the one after any cleared stage.
        public int HighestUnlockedStage
        {
            get
            {
                int best=Stage;
                for(int i=0;i<IncidentStars.Length && i<StageCount;i++)if(IncidentStars[i]>0)best=Mathf.Max(best,i+1);
                return Mathf.Clamp(best,0,StageCount-1);
            }
        }
        public bool StageUnlocked(int stage)=>stage>=0 && stage<StageCount && stage<=HighestUnlockedStage;
        public bool CanSelectStage(int stage)=>GoddessTutorial && StageUnlocked(stage) && !NeedsMagicUpgrade && !Supply.Locked
            && (Phase==FirePhase.Ready || Phase==FirePhase.Cleared || Phase==FirePhase.Won || Phase==FirePhase.Lost
                || Phase==FirePhase.Preparing && CountdownRemaining<0);
        // Level select: lays out the chosen stage in its Plan phase. Earnings and upgrades carry over, as in a replay.
        public bool SelectStage(int stage,bool prepare=true)
        {
            if(!CanSelectStage(stage))return false;
            EvacuateSafeAnimals();
            Stage=stage;Phase=FirePhase.Ready;
            return StartStage(prepare);
        }
    }
}

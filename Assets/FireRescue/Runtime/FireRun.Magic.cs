using UnityEngine;

namespace FireRescue
{
    public sealed partial class FireRun
    {
        public bool GoddessTutorial { get; }
        public bool IsPalmLesson => GoddessTutorial && Stage<3;
        public int IncidentIndex => GoddessTutorial?Mathf.Max(0,Stage-3):Stage;
        public int StageCount => GoddessTutorial?8:5;
        public string StageName => IsPalmLesson?(Stage==0?"GODDESS HAND":Stage==1?"GROWING MAGIC":"PALM RESCUE"):Names[IncidentIndex];
        // Palm water is the first item, so the next spell opens once the three palm lessons are cleared.
        public int EquipmentIncidents => GoddessTutorial?Mathf.Max(0,ClearedIncidents-2):ClearedIncidents;
        public bool NeedsMagicUpgrade => GoddessTutorial && Stage==0 && Phase==FirePhase.Cleared && MagicLevel==0;
        public bool PalmTutorialComplete {get;private set;}
        // Spell mode leans on the well basin: the first lesson stays quick, later crystals recover slowly.
        public float MagicFillSeconds => MagicLevel==0?5:SpellMode?30:10;
        public float NextMagicFillSeconds => SpellMode?30:10;
        public void CompletePalmTutorial(){PalmTutorialComplete=true;Learn(EmberdeepInteractionDemo.Action.Goddess);}
        public float NextMagicRecharge => (MagicCapacity+20)/10f;
        public int MagicLevel { get; private set; }
        public float MagicCapacity => 30+MagicLevel*20;
        public float MagicWater { get; private set; }=30;
        public const float MagicFlow=14;
        public float MagicPower => 45+MagicLevel*18;
        public float MagicRecharge => MagicCapacity/MagicFillSeconds;
        public float MagicRadius => .82f+MagicLevel*.12f;
        public float MagicRange => (14+MagicLevel*3)*FireMiniature.Scale;
        public int MagicUpgradeCost => MagicLevel<4?75+MagicLevel*100:0;
        public bool CanUpgradeMagic => InSupply && !Supply.Locked && MagicUpgradeCost>0 && Coins>=MagicUpgradeCost;
        public float MagicConsumed { get; private set; }
        public bool BuyMagicUpgrade()
        {
            if(!CanUpgradeMagic)return false;
            Coins-=MagicUpgradeCost;MagicLevel++;return true;
        }
        // Independent from every tank and manual supply transaction. No wall clock catch-up.
        // Partial charge remains usable, so lifting the support hand never wastes a charge.
        public float TickMagic(float dt,bool cast,bool eligible)
        {
            if(!eligible || !float.IsFinite(dt) || dt<=0 || (Phase!=FirePhase.Fighting && Phase!=FirePhase.Cleared))return 0;
            dt=Mathf.Min(dt,.1f);
            if(cast && Phase==FirePhase.Fighting && !Supply.Locked)
            {
                float used=Mathf.Min(MagicWater,MagicFlow*dt);
                MagicWater-=used;MagicConsumed+=used;return used*MagicPower/MagicFlow;
            }
            MagicWater=Mathf.Min(MagicCapacity,MagicWater+MagicRecharge*dt);return 0;
        }
    }
}

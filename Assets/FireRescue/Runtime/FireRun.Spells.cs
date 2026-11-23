using UnityEngine;

namespace FireRescue
{
    // Gesture spells (WATER BOLT, RAIN FAN, WATER ORB). LoopSpec §11 cuts them from the
    // production flow: they are only owned with PlaytestAllSpells. Each keeps its own tank,
    // which refills at stage start and recovers slowly; palm water uses the gem.
    public sealed partial class FireRun
    {
        public const float SpellRecoveryDelaySeconds=2,SpellRecoverySeconds=45;
        public const float FanFlow=12,FanSpreadDegrees=20;
        public bool SpellMode=>GoddessTutorial;
        bool playtestAllSpells;
        // Playtest switch: every spell is owned and full from the first level.
        public bool PlaytestAllSpells
        {
            get=>playtestAllSpells;
            set{playtestAllSpells=value;if(value)GrantAllSpells();}
        }
        void GrantAllSpells(){for(int i=0;i<4;i++){owned[i]=true;tanks[i]=CapacityFor((FireEquipmentId)i);}}
        // Legacy tool flow: the station works once the palm lessons are done.
        public bool StationOpen=>SpellMode || !IsPalmLesson;
        // Production flow sells no spells; the playtest switch grants them all instead.
        public bool SpellsCut=>SpellMode && !playtestAllSpells;
        bool SpellCut(FireEquipmentId id)=>SpellsCut && id!=FireEquipmentId.Extinguisher;
        readonly float[] spellIdle=new float[4];
        public float PowerFor(FireEquipmentId id)=>FireEquipment.Get(id).Power+upgrades[(int)id]*27;
        public float WidthFor(FireEquipmentId id)=>FireEquipment.Get(id).Width+upgrades[(int)id]*.16f;
        public float RangeFor(FireEquipmentId id)=>FireEquipment.Get(id).Range+upgrades[(int)id]*2.5f;
        public float SpeedFor(FireEquipmentId id)=>FireEquipment.Get(id).Speed+upgrades[(int)id]*.2f;
        public float SpellWater(FireEquipmentId id)=>id==FireEquipmentId.Extinguisher?MagicWater:WaterFor(id);
        public float SpellCapacity(FireEquipmentId id)=>id==FireEquipmentId.Extinguisher?MagicCapacity:CapacityFor(id);
        public float SpellFill01(FireEquipmentId id)=>Owns(id)?Mathf.Clamp01(SpellWater(id)/Mathf.Max(1,SpellCapacity(id))):0;
        public static EmberdeepInteractionDemo.Action LessonFor(FireEquipmentId id)=>id==FireEquipmentId.PulsePistol?EmberdeepInteractionDemo.Action.Bolt:
            id==FireEquipmentId.RainFan?EmberdeepInteractionDemo.Action.Fan:id==FireEquipmentId.PressureCannon?EmberdeepInteractionDemo.Action.Orb:EmberdeepInteractionDemo.Action.Goddess;
        // The newest purchased spell that has not yet put out a fire; -1 when every owned spell is learned.
        public int SpellLesson
        {
            get
            {
                if(!SpellMode)return -1;
                for(int i=1;i<4;i++)if(owned[i] && NeedsTutorial(LessonFor((FireEquipmentId)i)))return i;
                return -1;
            }
        }
        public bool CanCast(FireEquipmentId id)=>SpellMode && Owns(id) && Phase==FirePhase.Fighting && !Supply.Locked;
        float Spend(FireEquipmentId id,float amount)
        {
            int i=(int)id;float used=Mathf.Min(tanks[i],amount);
            tanks[i]-=used;ConsumedWater+=used;spellIdle[i]=0;return used;
        }
        void BeginSpellStage()
        {
            if(!SpellMode)return;
            for(int i=1;i<4;i++)if(owned[i])tanks[i]=CapacityFor((FireEquipmentId)i);
            System.Array.Clear(spellIdle,0,spellIdle.Length);
        }
        public static float BoltCost(float charge)=>3+7*Mathf.Clamp01(charge);
        // One shot per release. A weak crystal fires a proportionally weaker shot.
        public bool CastBolt(float charge,out float amount,out float radius)
        {
            amount=radius=0;var id=FireEquipmentId.PulsePistol;
            if(!CanCast(id) || !float.IsFinite(charge) || tanks[(int)id]<1)return false;
            charge=Mathf.Clamp01(charge);float cost=BoltCost(charge),paid=Spend(id,cost);
            amount=PowerFor(id)*(.3f+.9f*charge)*paid/cost;radius=.5f+.6f*charge+WidthFor(id);
            return amount>0;
        }
        // Continuous wide spray; the caller splits the amount across the streams.
        public float TickFan(float dt,bool cast)
        {
            var id=FireEquipmentId.RainFan;
            if(!cast || !CanCast(id) || !float.IsFinite(dt) || dt<=0)return 0;
            float used=Spend(id,FanFlow*Mathf.Min(dt,.1f));
            return used*PowerFor(id)/FanFlow;
        }
        public static float OrbCost(float charge)=>8+20*Mathf.Clamp01(charge);
        public bool CastOrb(float charge,out float amount,out float radius)
        {
            amount=radius=0;var id=FireEquipmentId.PressureCannon;
            if(!CanCast(id) || !float.IsFinite(charge) || tanks[(int)id]<1)return false;
            charge=Mathf.Clamp01(charge);float cost=OrbCost(charge),paid=Spend(id,cost);
            amount=PowerFor(id)*(.6f+charge)*paid/cost;radius=1.2f+2*charge+upgrades[(int)id]*.2f;
            return amount>0;
        }
        public void TickSpellRecovery(float dt,bool eligible)
        {
            if(!SpellMode || !eligible || !float.IsFinite(dt) || dt<=0 || (Phase!=FirePhase.Fighting && Phase!=FirePhase.Cleared))return;
            dt=Mathf.Min(dt,.1f);
            for(int i=1;i<4;i++)
            {
                if(!owned[i])continue;
                spellIdle[i]+=dt;float capacity=CapacityFor((FireEquipmentId)i);
                if(spellIdle[i]<SpellRecoveryDelaySeconds || tanks[i]>=capacity)continue;
                tanks[i]=Mathf.Min(capacity,tanks[i]+capacity/SpellRecoverySeconds*dt);
            }
        }
    }
}

using System;
using UnityEngine;

namespace FireRescue
{
    public enum FirePhase { Ready, Fighting, Cleared, Lost, Won }
    public enum TreeState { Safe, Burning, Saved, Burnt }
    public enum FirePropKind { Reservoir, Cabin }

    // All economy and fire simulation live here so desktop and hands share the same rules.
    public sealed class FireRun
    {
        public sealed class Tree
        {
            public Vector3 Position;
            public TreeState State;
            public float Heat, Wetness, Fuel = 100;
        }
        public sealed class Prop
        {
            public FirePropKind Kind;
            public Vector3 Position;
            public float Charge01, ActiveRemaining, Health01 = 1, Wetness;
            public bool Used;
        }
        public const float ReservoirCapacity = 70;
        public const float ReservoirDuration = 4;
        public const float ReservoirRadius = 2.4f;
        public const int CabinReward = 50;
        public const float RecoveryDelaySeconds=3,RecoveryUnitsPerSecond=.25f;
        static readonly float[] ParTimes = { 25, 50, 70, 90, 110 };
        static readonly string[] Objectives = {
            "Put out three fires. Dock at the free water station to refill.",
            "Fill the blue tower to rain on the whole grove.",
            "Save the woodland cabin. Its roof shows its health.",
            "Split your attention: two towers and a threatened cabin.",
            "Save both cabins. Chain water towers across the forest."
        };
        static readonly string[] Hints = {
            "Keep the landing dot on the fire until it turns green.",
            "Spray the blue tank: a full tank rains for four seconds.",
            "Clear nearby flames or cool the cabin directly. Save it for $50.",
            "Fill a tower, then fight another pocket while it rains.",
            "Use cannon splashes on clusters and towers on separate fronts."
        };
        public Prop[] Props { get; private set; } = Array.Empty<Prop>();
        public string Objective => Objectives[Stage];
        public string Hint => Hints[Stage];
        public int Combo { get; private set; }
        public int BestCombo { get; private set; }
        public int Score { get; private set; }
        public int Stars { get; private set; }
        public int StageBonus { get; private set; }
        public float Elapsed { get; private set; }
        public float ParTime => ParTimes[Stage];
        public int[] IncidentStars { get; } = new int[5];
        public int CabinsSaved { get { int count=0; foreach(var prop in Props) if(prop.Kind==FirePropKind.Cabin && prop.Health01>0) count++; return count; } }
        public int CabinsTotal { get { int count=0; foreach(var prop in Props) if(prop.Kind==FirePropKind.Cabin) count++; return count; } }
        float comboRemaining;
        public event Action<int> PropActivated;
        public event Action<int> CabinLost;
        public static readonly string[] Names = { "FIRST RESPONSE", "PARK PATROL", "THE GROVE", "FOREST FRONT", "FOREST INFERNO" };
        static readonly int[] Counts = { 3, 9, 18, 30, 42 };
        static readonly int[] Starts = { 3, 5, 8, 12, 18 };
        static readonly int[] Prices = { 75, 200, 400, 700 };
        public Tree[] Trees { get; private set; } = Array.Empty<Tree>();
        public FirePhase Phase { get; private set; } = FirePhase.Ready;
        public int Stage { get; private set; }
        public int Coins { get; private set; }
        public WaterStationTier StationTier { get; private set; }=WaterStationTier.LeverWell;
        public int StationUpgradeCost=>StationTier==WaterStationTier.LeverWell?100:StationTier==WaterStationTier.RotaryHydrant?200:0;
        public bool CanUpgradeStation=>InSupply && !Supply.Locked && StationUpgradeCost>0 && Coins>=StationUpgradeCost;
        public bool BuyStationUpgrade()
        {
            if(!CanUpgradeStation)return false;
            Coins-=StationUpgradeCost;StationTier=(WaterStationTier)((int)StationTier+1);ResetRecovery();return true;
        }
        readonly bool[] owned={true,false,false,false};
        readonly int[] upgrades=new int[4];
        readonly float[] tanks={60,0,0,0};
        public EmberdeepWaterSupply Supply { get; }
        public float SuppliedWater { get; private set; }
        public float ConsumedWater { get; private set; }
        double recoveredWater,recoveryElapsed,recoveryRemainder;
        public float RecoveredWater=>(float)recoveredWater;
        public bool RecoveryWaiting { get; private set; }
        public bool RecoveryActive { get; private set; }
        public float RecoveryDelayRemaining=>(float)Math.Max(0,RecoveryDelaySeconds-recoveryElapsed);
        public int AssistedRefills { get; internal set; }
        bool triggerNeedsOpen;
        EntryCheckpoint entryCheckpoint;
        sealed class EntryCheckpoint
        {
            public float[] Tanks;
            public bool[] Owned;
            public int[] Upgrades;
            public WaterStationTier StationTier;
            public FireEquipmentId Equipped;
            public float Supplied, Consumed;
            public double Recovered;
            public int Coins, Score, Rescued, BestCombo, Assisted;
        }
        public FireRun(){Supply=new EmberdeepWaterSupply(this);}
        public float WaterFor(FireEquipmentId id)=>Owns(id)?tanks[(int)id]:0;
        public float CapacityFor(FireEquipmentId id)=>FireEquipment.Get(id).Capacity+upgrades[(int)id]*25;
        public FireEquipmentId Equipped { get; private set; }
        public FireEquipment Equipment => FireEquipment.Get(Equipped);
        public int Upgrade => upgrades[(int)Equipped];
        public int ClearedIncidents => Stage+(Phase==FirePhase.Cleared || Phase==FirePhase.Won?1:0);
        public bool InSupply => Phase==FirePhase.Ready || Phase==FirePhase.Cleared;
        public float Charge01 { get; private set; }
        float cooldown;
        bool wasTrigger;
        float triggerChargeSeconds;
        bool chargeReady;
        public bool Owns(FireEquipmentId id) => (int)id>=0 && (int)id<owned.Length && owned[(int)id];
        public bool CanBuy(FireEquipmentId id) => (int)id>=0 && (int)id<owned.Length && !Supply.Locked && InSupply && !Owns(id) && ClearedIncidents>=FireEquipment.Get(id).Unlock && Coins>=FireEquipment.Get(id).Price;
        public bool BuyEquipment(FireEquipmentId id)
        {if(!CanBuy(id))return false;Coins-=FireEquipment.Get(id).Price;owned[(int)id]=true;ResetRecovery();return true;}
        public bool Equip(FireEquipmentId id)
        {if(!Owns(id) || Supply.Locked)return false;CancelTrigger();Equipped=id;ResetRecovery();return true;}
        public void CancelTrigger(){Charge01=0;wasTrigger=false;SprayAmount=0;triggerChargeSeconds=0;chargeReady=false;}
        public bool PrimeCannon(float charge)
        {
            if(PositiveFinite(charge))ResetRecovery();
            if(Supply.Locked || triggerNeedsOpen || Phase!=FirePhase.Fighting || Equipment.Delivery!=FireDelivery.Charge || cooldown>0 || Water<8 || !PositiveFinite(charge))return false;
            Charge01=Mathf.Max(Charge01,Mathf.Min(AffordableCharge01,Mathf.Clamp01(charge)));chargeReady|=charge>.1f;wasTrigger=true;return true;
        }
        public float Speed => Equipment.Speed+Upgrade*.2f;
        public float ShotRadius { get; private set; }
        public float ShotWetness { get; private set; }
        public float ShotSpread => Equipment.Delivery==FireDelivery.Fan?14:Equipment.Delivery==FireDelivery.Foam?4:0;
        public int Rescued { get; private set; }
        public float Water { get=>tanks[(int)Equipped]; private set {ConsumedWater+=Mathf.Max(0,tanks[(int)Equipped]-value);tanks[(int)Equipped]=value;} }
        public float AffordableCharge01=>Mathf.Clamp01((Water-8)/20);
        // Manual supply and slow recovery are the only grant transactions.
        // Direct hits and visual effects cannot fill a tank.
        internal float TransferWater(EmberdeepWaterSupply authority,float requested)
        {
            if(!ReferenceEquals(authority,Supply) || !Supply.Locked || Supply.Tool!=Equipped || !PositiveFinite(requested) || Phase==FirePhase.Lost)return 0;
            float actual=Mathf.Min(requested,Mathf.Max(0,Capacity-Water));
            ResetRecovery();
            tanks[(int)Equipped]+=actual;SuppliedWater+=actual;
            return actual;
        }
        internal void CancelSupplyInput(){CancelTrigger();triggerNeedsOpen=true;ResetRecovery();}
        public void ResetRecovery()
        {recoveryElapsed=recoveryRemainder=0;RecoveryWaiting=RecoveryActive=false;}
        // GD-WATER-10/11: the caller supplies live fireground eligibility and
        // raw held input, including a dry trigger. Call once after Tick; no clock
        // or coroutine may accumulate missed/background intervals for this API.
        public float TickRecovery(float dt,bool eligible,bool triggerActive)
        {
            if(!PositiveFinite(dt) || !eligible || triggerActive || !Owns(Equipped)
                || (Phase!=FirePhase.Fighting && Phase!=FirePhase.Cleared)
                || Supply.Locked || wasTrigger || Charge01>0 || SprayAmount>0)
            {ResetRecovery();return 0;}
            if(Water>=Capacity){ResetRecovery();return 0;}
            double accepted=Math.Min(dt,.1f),remaining=Math.Max(0,RecoveryDelaySeconds-recoveryElapsed);
            double delayed=Math.Min(accepted,remaining);
            recoveryElapsed=Math.Min(RecoveryDelaySeconds,recoveryElapsed+delayed);
            // Exact nominal boundaries (30 x .1, 180 x 1/60) must not leak a
            // float-rounding-sized grant before the three-second wait is over.
            double active=accepted-delayed;
            if(remaining>0 && active<.000001)active=0;
            if(RecoveryDelaySeconds-recoveryElapsed<.000001)recoveryElapsed=RecoveryDelaySeconds;
            RecoveryWaiting=recoveryElapsed<RecoveryDelaySeconds;RecoveryActive=!RecoveryWaiting;
            if(active<=0)return 0;
            double exact=Math.Min(Capacity,(double)Water+recoveryRemainder+active*RecoveryUnitsPerSecond);
            float after=(float)exact,actual=after-Water;
            recoveryRemainder=exact-after; // Carry sub-float rounding across frames.
            tanks[(int)Equipped]=after;recoveredWater+=actual;
            if(after>=Capacity){RecoveryWaiting=RecoveryActive=false;recoveryRemainder=0;}
            return actual;
        }
        public float Capacity => Equipment.Capacity + Upgrade * 25;
        public float Range => Equipment.Range + Upgrade * 2.5f;
        public float Power => Equipment.Power + Upgrade * 27;
        public float Width => Equipment.Width + Upgrade * .16f;
        public int UpgradeFor(FireEquipmentId id)=>upgrades[(int)id];
        public int UpgradeCostFor(FireEquipmentId id)=>upgrades[(int)id]<Prices.Length?Prices[upgrades[(int)id]]:0;
        public int UpgradeCost => Upgrade < Prices.Length ? Prices[Upgrade] : 0;
        public int BurnLimit => Mathf.Max(1, Mathf.CeilToInt(Trees.Length * .3f));
        public int Burnt { get { int n=0; foreach(var t in Trees) if(t.State==TreeState.Burnt)n++; return n; } }
        public int Burning { get { int n=0; foreach(var t in Trees) if(t.State==TreeState.Burning)n++; return n; } }
        public bool CanUpgrade => !Supply.Locked && InSupply && Upgrade < Prices.Length && Coins >= UpgradeCost;
        float spreadTime;
        public event Action<int> Extinguished;

        public bool StartStage()
        {
            if(Phase!=FirePhase.Ready && Phase!=FirePhase.Cleared) return false;
            Supply.Interrupt("Sector changed");
            ResetRecovery();
            if(Phase==FirePhase.Cleared) Stage++;
            entryCheckpoint=new EntryCheckpoint{Tanks=(float[])tanks.Clone(),Owned=(bool[])owned.Clone(),Upgrades=(int[])upgrades.Clone(),StationTier=StationTier,Equipped=Equipped,Supplied=SuppliedWater,Consumed=ConsumedWater,Recovered=recoveredWater,
                Coins=Coins,Score=Score,Rescued=Rescued,BestCombo=BestCombo,Assisted=AssistedRefills};
            int count=Counts[Stage], columns=Stage==0?3:Stage==1?3:Stage==4?7:6;
            Trees=new Tree[count];
            for(int i=0;i<count;i++)
            {
                float x=(i%columns-(columns-1)*.5f)*1.18f;
                float z=3+(i/columns)*1.15f;
                // Later incidents have two groves separated by an access lane.
                if(Stage>=2 && columns%2==0)x+=Mathf.Sign(x)*.24f;
                if(Stage==4)z+=Mathf.Sin((i%columns)*1.2f)*.16f;
                Trees[i]=new Tree { Position=new Vector3(x,.7f,z), State=TreeState.Safe };
            }
            // Distribute the initial fires throughout the forest, deterministically.
            for(int i=0;i<Starts[Stage];i++) { var tree=Trees[i*count/Starts[Stage]]; tree.State=TreeState.Burning; tree.Heat=100; }
            BuildProps();
            Combo=Stars=StageBonus=0;Elapsed=comboRemaining=0;
            Phase=FirePhase.Fighting; spreadTime=0;CancelTrigger();cooldown=0; return true;
        }
        // Retry rewinds the whole attempt's accounting, never just its danger or its tank.
        public bool RetryStage()
        {
            if(entryCheckpoint==null || (Phase!=FirePhase.Fighting && Phase!=FirePhase.Lost))return false;
            Supply.Interrupt("Retry checkpoint");
            Array.Copy(entryCheckpoint.Tanks,tanks,tanks.Length);
            Array.Copy(entryCheckpoint.Owned,owned,owned.Length);
            Array.Copy(entryCheckpoint.Upgrades,upgrades,upgrades.Length);
            StationTier=entryCheckpoint.StationTier;
            Equipped=entryCheckpoint.Equipped;
            SuppliedWater=entryCheckpoint.Supplied;ConsumedWater=entryCheckpoint.Consumed;
            recoveredWater=entryCheckpoint.Recovered;
            Coins=entryCheckpoint.Coins;Score=entryCheckpoint.Score;Rescued=entryCheckpoint.Rescued;
            BestCombo=entryCheckpoint.BestCombo;AssistedRefills=entryCheckpoint.Assisted;
            Phase=FirePhase.Ready;
            return StartStage();
        }
        void BuildProps()
        {
            if(Stage==0){Props=Array.Empty<Prop>();return;}
            if(Stage==1){Props=new[]{Reservoir(-.59f,3.575f)};return;}
            if(Stage==2){Props=new[]{Reservoir(-2.6f,3.575f),Cabin(4.25f,4.15f)};return;}
            if(Stage==3){Props=new[]{Reservoir(-2.6f,3.575f),Reservoir(2.6f,5.875f),Cabin(4.25f,5.3f)};return;}
            Props=new[]{Reservoir(-2.95f,3.65f),Reservoir(2.95f,5.915f),Reservoir(.59f,8.1f),Cabin(-4.7f,5.3f),Cabin(4.7f,7.6f)};
        }
        // Elevated tanks are visible and hittable above the front tree collision targets.
        static Prop Reservoir(float x,float z)=>new Prop{Kind=FirePropKind.Reservoir,Position=new Vector3(x,1.45f,z)};
        static Prop Cabin(float x,float z)=>new Prop{Kind=FirePropKind.Cabin,Position=new Vector3(x,.7f,z)};
        public bool BuyUpgrade()
        {
            if(!CanUpgrade)return false;
            Coins-=UpgradeCost; upgrades[(int)Equipped]++;ResetRecovery();return true;
        }
        public void Restart()
        {
            Supply.Interrupt("New expedition");entryCheckpoint=null;SuppliedWater=ConsumedWater=0;recoveredWater=0;ResetRecovery();AssistedRefills=0;triggerNeedsOpen=false;
            Stage=Coins=Rescued=0;Equipped=FireEquipmentId.Extinguisher;StationTier=WaterStationTier.LeverWell;
            for(int i=0;i<4;i++){owned[i]=i==0;upgrades[i]=0;tanks[i]=i==0?60:0;}
            Trees=Array.Empty<Tree>();Phase=FirePhase.Ready;spreadTime=cooldown=0;CancelTrigger();
            Props=Array.Empty<Prop>();Combo=BestCombo=Score=Stars=StageBonus=0;Elapsed=comboRemaining=0;Array.Clear(IncidentStars,0,IncidentStars.Length);
        }
        public float SprayAmount { get; private set; }
        public bool Tick(float dt, bool spraying, bool manualCharge=false)
        {
            SprayAmount=0;
            if(spraying || manualCharge || Supply.Locked)ResetRecovery();
            if(float.IsNaN(dt) || float.IsInfinity(dt) || dt<=0)return false;
            dt=Mathf.Min(dt,.1f); // No catch-up burst after a pause or lost focus.
            if(Phase!=FirePhase.Fighting)return false;
            if(Supply.Locked && Supply.Assisted){CancelTrigger();return false;}
            if(Supply.Locked){CancelTrigger();spraying=false;}
            else if(triggerNeedsOpen){if(!spraying)triggerNeedsOpen=false;CancelTrigger();spraying=false;}
            Elapsed+=dt;comboRemaining=Mathf.Max(0,comboRemaining-dt);if(comboRemaining<=0)Combo=0;
            cooldown=Mathf.Max(0,cooldown-dt);
            ShotRadius=.6f+Width;ShotWetness=Equipment.Delivery==FireDelivery.Foam?3:1;
            if(Equipment.Delivery==FireDelivery.Charge)
            {
                if(spraying && !manualCharge && Water>=8 && cooldown<=0)
                {
                    triggerChargeSeconds+=dt;chargeReady|=triggerChargeSeconds>=.12f;
                    Charge01=Mathf.Min(AffordableCharge01,Charge01+dt/1.2f);
                }
                if(!spraying && wasTrigger && chargeReady && cooldown<=0)
                {
                    float cost=8+20*Charge01;
                    if(Water+.00001f>=cost){Water-=Mathf.Min(Water,cost);SprayAmount=Power*(.6f+Charge01);ShotRadius=1.2f+2*Charge01+Upgrade*.2f;cooldown=.6f;}
                    Charge01=0;
                }
                if(!spraying){Charge01=0;triggerChargeSeconds=0;chargeReady=false;}
            }
            else if(Equipment.Delivery==FireDelivery.Pulse)
            {
                if(spraying && cooldown<=0 && Water>=3){Water-=3;SprayAmount=Power*.22f;cooldown=.22f;}
            }
            else if(spraying && Water>0)
            {
                float used=Mathf.Min(Water,Equipment.Flow*dt);Water-=used;SprayAmount=Power*used/Equipment.Flow;
            }
            wasTrigger=spraying;
            foreach(var tree in Trees)
            {
                tree.Wetness=Mathf.Max(0,tree.Wetness-dt*.7f);
                if(tree.State!=TreeState.Burning)continue;
                tree.Fuel-=dt*(.8f+Stage*.15f);
                if(tree.Fuel<=0)tree.State=TreeState.Burnt;
            }
            spreadTime+=dt;
            if(spreadTime>=10-Stage) { spreadTime=0; Spread(); }
            TickProps(dt);
            CheckOutcome();
            return Phase==FirePhase.Fighting && SprayAmount>0;
        }
        void CheckOutcome()
        {
            if(Phase!=FirePhase.Fighting)return;
            if(Burnt>=BurnLimit){Phase=FirePhase.Lost;CancelTrigger();}
            else if(Burning==0)
            {
                StageBonus=CabinsSaved*CabinReward;
                Coins+=25*(Stage+1)+StageBonus;
                Stars=1+(Burnt==0?1:0)+(CabinsSaved==CabinsTotal && Elapsed<=ParTime?1:0);
                IncidentStars[Stage]=Stars;
                Score+=Stars*250+CabinsSaved*200;
                Phase=Stage==Names.Length-1?FirePhase.Won:FirePhase.Cleared;
                CancelTrigger();
            }
            if(Phase!=FirePhase.Fighting){ResetRecovery();foreach(var prop in Props)prop.ActiveRemaining=0;}
        }
        public void ApplyWater(Vector3 point,float amount,float radius=-1,float wetness=1)
        {
            if(Phase!=FirePhase.Fighting || !PositiveFinite(amount) || !Finite(point.x) || !Finite(point.y) || !Finite(point.z) || !Finite(radius) || !Finite(wetness))return;
            if(radius<0)radius=.6f+Width;
            for(int i=0;i<Trees.Length;i++)
            {
                var tree=Trees[i];if(Vector3.Distance(tree.Position,point)>radius)continue;
                tree.Wetness=Mathf.Max(tree.Wetness,wetness);
                if(tree.State!=TreeState.Burning)continue;
                tree.Heat=Mathf.Max(0,tree.Heat-amount);
                if(tree.Heat>0)continue;
                tree.State=TreeState.Saved;Coins+=25;Rescued++;
                Combo++;BestCombo=Mathf.Max(BestCombo,Combo);comboRemaining=3;
                Score+=100+Mathf.Min(Combo-1,9)*20;
                Extinguished?.Invoke(i);
            }
            CheckOutcome();
        }
        // Only a direct projectile collision fills a tower. Area water still affects trees,
        // but a sprinkler cannot charge itself or another tower through its own spray.
        public void ApplyPropWater(int index,float amount)
        {
            if(Phase!=FirePhase.Fighting || index<0 || index>=Props.Length || !PositiveFinite(amount))return;
            var prop=Props[index];
            if(prop.Kind==FirePropKind.Reservoir)
            {
                if(prop.Used)return;
                prop.Charge01=Mathf.Min(1,prop.Charge01+amount/ReservoirCapacity);
                if(prop.Charge01>=1){prop.Used=true;prop.ActiveRemaining=ReservoirDuration;PropActivated?.Invoke(index);}
            }
            else if(prop.Health01>0)
            {
                prop.Health01=Mathf.Min(1,prop.Health01+amount*.0009f);
                prop.Wetness=Mathf.Min(3,prop.Wetness+amount*.045f);
            }
        }
        void TickProps(float dt)
        {
            for(int i=0;i<Props.Length && Phase==FirePhase.Fighting;i++)
            {
                var prop=Props[i];
                if(prop.Kind==FirePropKind.Reservoir)
                {
                    if(prop.ActiveRemaining<=0)continue;
                    float duration=Mathf.Min(dt,prop.ActiveRemaining);
                    prop.ActiveRemaining=Mathf.Max(0,prop.ActiveRemaining-duration);
                    ApplyWater(prop.Position,duration*35,ReservoirRadius,2);
                }
                else if(prop.Health01>0)
                {
                    prop.Wetness=Mathf.Max(0,prop.Wetness-dt);
                    if(prop.Wetness>0)continue;
                    int threats=0;
                    foreach(var tree in Trees)if(tree.State==TreeState.Burning && Vector3.Distance(tree.Position,prop.Position)<2)threats++;
                    prop.Health01=Mathf.Max(0,prop.Health01-threats*dt*.012f);
                    if(prop.Health01<=0)CabinLost?.Invoke(i);
                }
            }
        }
        static bool Finite(float value)=>!float.IsNaN(value) && !float.IsInfinity(value);
        static bool PositiveFinite(float value)=>value>0 && Finite(value);
        void Spread()
        {
            // One new ignition per interval; do not cascade through a forest in one frame.
            for(int i=0;i<Trees.Length;i++) if(Trees[i].State==TreeState.Burning)
                for(int j=0;j<Trees.Length;j++) if(Trees[j].State==TreeState.Safe && Trees[j].Wetness<.2f && Vector3.Distance(Trees[i].Position,Trees[j].Position)<1.7f)
                { Trees[j].State=TreeState.Burning; Trees[j].Heat=100; return; }
        }
    }
}

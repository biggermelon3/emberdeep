using System;
using UnityEngine;

namespace FireRescue
{
    public enum AnimalState { Waiting, Running, Celebrating, Rescued, Lost }
    public sealed partial class FireRun
    {
        public sealed class Animal
        {
            public Vector3 Position,Home,Refuge,Velocity;
            public bool OnFire;
            public float Health=1,Wetness,Threat,StateTime,AnimationTime,WaterReaction;
            public AnimalState State;
            public int HeldBy=-1;
            public float Affection,PetCooldown;
            public bool ReturningToGround;
            public Vector3 Landing;
            // Where a rescued sheep settles on the safe bank, within reach for petting.
            public Vector3 Pasture;
        }
        // A pet sets Affection to PetAffection (it decays 1/s) and blocks the next pet for PetCooldownSeconds.
        public const float PetAffection=1.5f,PetCooldownSeconds=.8f;
        // Rescued sheep stay on the board: after the coin pops at the refuge (the creek's far bank, z 2.65) they
        // wade across the creek (z 1.62-2.58) to a pasture spot on the near bank, within reach, and graze.
        public const float PastureDelay=.6f,PastureWalkSpeed=.8f,PastureZ=1.05f,PastureRowStep=.55f,PastureRowSide=.35f,PastureMaxX=2.7f;
        public static bool CanHold(Animal a)=>a.State==AnimalState.Waiting || a.State==AnimalState.Running || a.State==AnimalState.Celebrating;
        public static bool CanPet(Animal a)=>CanHold(a) || a.State==AnimalState.Rescued;
        static readonly int[] FlockCounts={3,4,5,6,8};
        static readonly int[] FlockRewards={25,40,55,65,80};
        public Animal[] Animals { get; private set; }=Array.Empty<Animal>();
        public const float AnimalHeight=.30f, AnimalRadius=.24f;
        public int AnimalsOnFire {get {int n=0;foreach(var a in Animals)if(a.OnFire && a.State!=AnimalState.Lost)n++;return n;}}
        public int AnimalReward=>FlockRewards[IncidentIndex];
        public int AnimalsSaved { get {int n=0;foreach(var a in Animals)if(a.State==AnimalState.Rescued)n++;return n;} }
        public int AnimalsLost { get {int n=0;foreach(var a in Animals)if(a.State==AnimalState.Lost)n++;return n;} }
        public event Action<int> AnimalRescued;
        void BuildAnimals()
        {
            if(Level!=null){BuildLevelAnimals(Level.SheepHomes);return;}
            float back=3;foreach(var t in Trees)back=Mathf.Max(back,t.Position.z);
            Animals=new Animal[FlockCounts[IncidentIndex]];
            // Inter-column lanes leave tree trunks and the front river clear (FieldLanes: the gaps of the generated field).
            float[] lanes=FieldLanes();
            for(int i=0;i<Animals.Length;i++)
            {
                float x=lanes[i%lanes.Length];
                int row=i/lanes.Length;
                var home=new Vector3(x,0,back+1.05f+row*1.15f);
                // Rows alternate sides of the lane, so a later sheep never walks through an earlier one.
                // Refuge and pasture are authored board z; BoardZ moves them onto the compact board's apron (§5 Reach).
                var pasture=new Vector3(Mathf.Clamp(x+(row%2==0?-PastureRowSide:PastureRowSide),-PastureMaxX,PastureMaxX),0,BoardZ(PastureZ-row*PastureRowStep));
                Animals[i]=new Animal{Home=home,Position=home,Refuge=new Vector3(x,0,BoardZ(2.65f)),Pasture=pasture,AnimationTime=i*.73f};
            }
        }
        // Authored homes (EmberdeepLevels): each sheep runs straight down its own lane to the creek. The n-th sheep of a
        // lane takes the n-th pasture row, alternating sides of the lane as in the generated flock above.
        void BuildLevelAnimals(Vector2[] homes)
        {
            Animals=new Animal[homes.Length];
            for(int i=0;i<homes.Length;i++)
            {
                float x=homes[i].x;int row=0;
                for(int j=0;j<i;j++)if(Mathf.Abs(homes[j].x-x)<.01f)row++;
                var home=new Vector3(x,0,homes[i].y);
                var pasture=new Vector3(Mathf.Clamp(x+(row%2==0?-PastureRowSide:PastureRowSide),-PastureMaxX,PastureMaxX),0,BoardZ(PastureZ-row*PastureRowStep));
                Animals[i]=new Animal{Home=home,Position=home,Refuge=new Vector3(x,0,BoardZ(2.65f)),Pasture=pasture,AnimationTime=i*.73f};
            }
        }
        // The one splash rule for sheep: ApplyAnimalWater, the palm aim's sheep rings and its aim assist all use it.
        public static bool WaterReachesSheep(Animal a,Vector3 point,float radius)=>a!=null && Vector3.Distance(a.Position+Vector3.up*AnimalHeight,point)<=radius+AnimalRadius;
        // Water can still put this sheep out (ApplyAnimalWater): on fire, on the ground (Waiting/Running) and not in a hand.
        public static bool CanDouseSheep(Animal a)=>a!=null && a.OnFire && a.HeldBy<0 && (a.State==AnimalState.Waiting || a.State==AnimalState.Running);
        public void ApplyAnimalWater(Vector3 point,float amount,float radius)
        {
            if(Phase!=FirePhase.Fighting || !PositiveFinite(amount) || !Finite(radius) || radius<0 || !Finite(point.sqrMagnitude))return;
            foreach(var a in Animals)
            {
                if(a.State!=AnimalState.Waiting && a.State!=AnimalState.Running)continue;
                if(!WaterReachesSheep(a,point,radius))continue;
                if(a.Wetness<1)a.WaterReaction=.85f;
                a.Wetness=Mathf.Min(6,a.Wetness+amount*.4f);a.OnFire=false;
                a.Health=Mathf.Min(1,a.Health+amount*.001f);
            }
        }
        bool EscapeClear(Animal a)
        {
            var segment=a.Refuge-a.Position;
            foreach(var t in Trees)
            {
                if(t.State!=TreeState.Burning)continue;
                var p=t.Position;p.y=0;
                float u=segment.sqrMagnitude>.001f?Mathf.Clamp01(Vector3.Dot(p-a.Position,segment)/segment.sqrMagnitude):0;
                if(Vector3.Distance(p,a.Position+segment*u)<1.05f)return false;
            }
            return true;
        }
        void TickAnimals(float dt)
        {
            if(Phase==FirePhase.Lost || Phase==FirePhase.Ready || Phase==FirePhase.Preparing)return;
            for(int i=0;i<Animals.Length;i++)
            {
                var a=Animals[i];a.AnimationTime+=dt;a.StateTime+=dt;a.Velocity=Vector3.zero;
                a.WaterReaction=Mathf.Max(0,a.WaterReaction-dt);
                a.Affection=Mathf.Max(0,a.Affection-dt);a.PetCooldown=Mathf.Max(0,a.PetCooldown-dt);
                if(a.State==AnimalState.Rescued){if(a.HeldBy<0 && a.StateTime>=PastureDelay)MoveAnimal(a,a.Pasture,dt,PastureWalkSpeed);continue;}
                if(a.State==AnimalState.Lost)continue;
                if(a.State==AnimalState.Celebrating)
                {if(a.StateTime>=1.15f)RewardAnimal(i);continue;}
                a.Wetness=Mathf.Max(0,a.Wetness-dt);a.Threat=0;
                if(Phase==FirePhase.Fighting)
                    foreach(var t in Trees)
                        if(t.State==TreeState.Burning && Vector2.Distance(new Vector2(t.Position.x,t.Position.z),new Vector2(a.Position.x,a.Position.z))<1.9f)a.Threat++;
                if(a.Wetness<=0 && Elapsed>8 && a.Threat>0 && !StillLesson)a.OnFire=true;
                if(a.OnFire)a.Health=Mathf.Max(0,a.Health-dt*.024f);
                if(a.Health<=0){a.State=AnimalState.Lost;a.StateTime=0;a.OnFire=false;a.HeldBy=-1;continue;}
                if(a.HeldBy>=0)continue;
                if(a.ReturningToGround)
                {SettleAnimal(a,dt);continue;}
                if(a.Position.y>0)
                {a.Position=new Vector3(a.Position.x,Mathf.MoveTowards(a.Position.y,0,dt*5),a.Position.z);continue;}
                if(a.OnFire || !EscapeClear(a))
                {
                    a.State=AnimalState.Waiting;
                    // Bounded, individually phased wandering behind the trees.
                    // Panic changes destination more often and runs four times faster.
                    float cycle=a.OnFire?.72f:3.8f;
                    int step=Mathf.FloorToInt(a.AnimationTime/cycle);
                    float seed=(i+1)*17.37f+step*12.79f;
                    var target=a.Home+new Vector3(Mathf.Sin(seed)*(a.OnFire?.65f:.28f),0,Mathf.Cos(seed*1.71f)*(a.OnFire?.45f:.23f));
                    if(a.OnFire)target=a.Home+new Vector3(Mathf.Sin(a.AnimationTime*4+i)*.65f,0,Mathf.Cos(a.AnimationTime*5+i)*.45f);
                    if(a.OnFire || a.AnimationTime%cycle>1.15f)MoveAnimal(a,target,dt,a.OnFire?2.1f:.38f);
                    continue;
                }
                bool queued=false;
                foreach(var ahead in Animals)
                {
                    if(ReferenceEquals(a,ahead) || ahead.HeldBy>=0 || ahead.ReturningToGround || ahead.State==AnimalState.Lost || (ahead.State==AnimalState.Rescued && ahead.StateTime>.45f))continue;
                    if(Mathf.Abs(ahead.Position.x-a.Position.x)<.5f && ahead.Position.z<a.Position.z && a.Position.z-ahead.Position.z<1.1f){queued=true;break;}
                }
                if(queued){a.State=AnimalState.Waiting;continue;}
                // Brief individual reaction, followed by a staggered escape.
                if(a.AnimationTime<i*.73f+.6f+i*.14f)continue;
                a.State=AnimalState.Running;
                MoveAnimal(a,a.Refuge,dt,1.25f);
                if(Vector3.Distance(a.Position,a.Refuge)<.01f){a.State=AnimalState.Celebrating;a.StateTime=0;}
            }
        }
        static void MoveAnimal(Animal a,Vector3 target,float dt,float speed)
        {
            var next=Vector3.MoveTowards(a.Position,target,dt*speed);
            a.Velocity=(next-a.Position)/dt;a.Position=next;
        }
        internal static void SettleAnimal(Animal a,float dt)
        {
            a.Position=Vector3.MoveTowards(a.Position,a.Landing,dt*8);
            if(Vector3.Distance(a.Position,a.Landing)<.001f){a.Position=a.Landing;a.ReturningToGround=false;}
        }
        void RewardAnimal(int i)
        {
            var a=Animals[i];if(a.State==AnimalState.Rescued || a.State==AnimalState.Lost)return;
            a.State=AnimalState.Rescued;a.HeldBy=-1;a.StateTime=0;Coins+=AnimalReward;AnimalRescued?.Invoke(i);
        }
        // Leaving a contained incident cannot strand a surviving sheep or lose its
        // earned reward while its arrival animation is playing. Retry rewinds Coins.
        public void EvacuateSafeAnimals()
        {
            if(Phase!=FirePhase.Cleared && Phase!=FirePhase.Won)return;
            for(int i=0;i<Animals.Length;i++)RewardAnimal(i);
        }
    }
}

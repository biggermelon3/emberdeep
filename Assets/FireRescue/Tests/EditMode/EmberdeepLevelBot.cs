using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace FireRescue.Tests
{
    // LoopSpec §8 level gate (workstream LV): a scripted "reasonable player" for the authored levels, on the FireRun sim
    // only (no scene). It plays the way a seated player can, with human costs:
    //   - Plan: sets the tray pieces on the level's hint squares and draws the excavator's hint line, then rings the bell.
    //   - Fight: one target at a time. Moving the aim costs AimSeconds (a short hop to a neighbour costs NearSeconds),
    //     and no water lands while it moves. Priority: a sheep on fire, then soaking the forecast tree if it catches
    //     within SoakLead seconds, then the burning tree or grass closest to the next forecast catch (it feeds the front).
    //   - Water: palm water at the gem's flow and power (FireRun.TickMagic), splash radius MagicRadius, soaking like the
    //     real jet. Empty gem: it walks a hand to the pump, hammers off-beat strikes every StrikeSeconds until the gem is
    //     RefillTo full, and walks back. Passive refill runs as in the game.
    //   - The plane, once earned: it lays the line over the most burning fuel it can cover.
    // It never carries sheep by hand, and never uses the rhythm bonus, so a real player has slack on top.
    public sealed class EmberdeepLevelBot
    {
        public float Dt=.05f, ReactSeconds=1, AimSeconds=.5f, NearSeconds=.25f, SoakSeconds=.25f, SoakLead=2;
        public float ToPumpSeconds=.8f, StrikeSeconds=.6f, RefillTo=.85f, PlaneSeconds=1.5f, MaxSeconds=240;
        public bool UsePieces=true, Pump=true, Soak=true, Act=true;
        // A reasonable player soaks the one tree the forecast names, once per catch. A skilled one keeps soaking whatever
        // the forecast moves to, which can stall a small front completely.
        public bool Skilled;
        // Lesson checks: leave one piece kind in the tray (towers, the excavator, the plane).
        public bool Towers=true, Dig=true, Plane=true;
        // A sheep that was on fire gets a proper soak (it catches again as soon as it dries while a fire is near).
        public float SheepWetness=4;

        public sealed class Result
        {
            public FirePhase Phase;public float Seconds;public int Burnt,Limit,Stars,Trees;
            public int SheepSaved,SheepLost,SheepTotal,CabinsSaved,CabinsTotal;
            public int TimesDry,Catches,ForecastChecked,ForecastMisses,TowersRained;public float Pumped;public bool PlaneCalled;
            public float FirstDry=-1;
            public bool Cleared=>Phase==FirePhase.Cleared || Phase==FirePhase.Won;
            public override string ToString()=>$"{Phase} {Seconds:0.0}s burnt {Burnt}/{Limit} stars {Stars} sheep {SheepSaved}/{SheepTotal} (lost {SheepLost}) cabins {CabinsSaved}/{CabinsTotal} "+
                $"dry {TimesDry}x (first {FirstDry:0.0}s) pumped {Pumped:0} towers {TowersRained} plane {(PlaneCalled?"yes":"no")} catches {Catches} forecast misses {ForecastMisses}/{ForecastChecked}";
        }

        public static FireRun Load(int stage)
        {
            var run=new FireRun(true);typeof(FireRun).GetProperty("Stage").SetValue(run,stage);
            if(!run.StartStage(true))throw new System.InvalidOperationException("stage "+stage+" did not start");
            return run;
        }
        // Plan: the hint pieces. Returns false if any hint is illegal.
        public static bool PlaceHints(FireRun run,EmberdeepLevel level,StringBuilder why=null,bool towers=true,bool dig=true)
        {
            int tower=0,digger=0;
            for(int i=0;i<run.Pieces.Length;i++)
            {
                var p=run.Pieces[i];
                if(p.Kind==FirePieceKind.Tower && towers && tower<level.TowerHints.Length)
                {
                    int sq=level.Square(level.TowerHints[tower++]);
                    var block=run.CanPlace(i,sq);if(block!=FirePlaceBlock.None){why?.Append($"tower {tower} square {sq}: {block}; ");return false;}
                    run.PlacePiece(i,sq);
                }
                else if(p.Kind==FirePieceKind.Excavator && dig && digger++==0 && level.DigHint.Length>0)
                {
                    int sq=level.Square(level.DigHint[0]);
                    var block=run.CanPlace(i,sq);if(block!=FirePlaceBlock.None){why?.Append($"excavator square {sq}: {block}; ");return false;}
                    run.PlacePiece(i,sq);run.StartLine(i);
                    for(int k=1;k<level.DigHint.Length;k++)
                        if(!run.ExtendLine(i,level.Square(level.DigHint[k]))){why?.Append($"dig line step {k} ({level.DigHint[k]}) refused; ");return false;}
                    if(level.DigHint.Length>1 && !run.FinishLine(i)){why?.Append("dig line not finished; ");return false;}
                }
            }
            return true;
        }
        public static void StartFight(FireRun run)
        {
            run.BeginCountdown();for(int i=0;i<40 && run.Phase==FirePhase.Preparing;i++)run.TickPreparation(.1f);
        }

        float soakedFor=-1;
        enum Mode { Idle, Moving, Spraying, ToPump, Pumping, FromPump }
        struct Target { public int Kind, Index; } // Kind 0 none, 1 tree, 2 grass, 3 sheep
        public Result Play(int stage)=>Play(Load(stage),EmberdeepLevels.For(stage));
        public Result Play(FireRun run,EmberdeepLevel level)
        {
            if(UsePieces && Act)PlaceHints(run,level,null,Towers,Dig);
            StartFight(run);
            var r=new Result{Limit=run.BurnLimit,Trees=run.Trees.Length,SheepTotal=run.Animals.Length,CabinsTotal=run.CabinsTotal};
            run.TowerRained+=_=>r.TowersRained++;
            var mode=Mode.Idle;float timer=ReactSeconds;mode=Mode.Moving;
            var target=new Target();Vector3 aim=new Vector3(0,.7f,3);float soakLeft=0;bool wasDry=false,waiting=false;
            var before=new TreeState[run.CellCount];
            for(int step=0;step<MaxSeconds/Dt && run.Phase==FirePhase.Fighting;step++)
            {
                bool cast=false;
                if(Act)
                {
                    timer-=Dt;
                    if(UsePieces && Plane && run.PlaneReady && mode!=Mode.Pumping && mode!=Mode.ToPump && CallPlane(run)){r.PlaneCalled=true;mode=Mode.Moving;timer=PlaneSeconds;target=default;}
                    switch(mode)
                    {
                        case Mode.Moving:if(timer<=0)mode=Mode.Spraying;break;
                        case Mode.ToPump:if(timer<=0){mode=Mode.Pumping;timer=StrikeSeconds;}break;
                        case Mode.Pumping:
                            if(timer<=0){r.Pumped+=run.PumpGem(run.PumpStrikeUnits(false));timer+=StrikeSeconds;}
                            if(run.MagicWater>=run.MagicCapacity*RefillTo){mode=Mode.FromPump;timer=AimSeconds;target=default;}
                            break;
                        case Mode.FromPump:if(timer<=0)mode=Mode.Idle;break;
                    }
                    if(mode==Mode.Idle || mode==Mode.Spraying)
                    {
                        // Without the pump: stop touching the gem and let the passive refill fill it back up first.
                        if(run.GemEmpty && !Pump)waiting=true;
                        if(waiting && run.MagicWater>=run.MagicCapacity*RefillTo)waiting=false;
                        if(run.GemEmpty && Pump){mode=Mode.ToPump;timer=ToPumpSeconds;target=default;}
                        else
                        {
                            var next=Choose(run,target,ref soakLeft);
                            if(next.Kind==0){mode=Mode.Idle;target=next;}
                            else if(next.Kind!=target.Kind || next.Index!=target.Index)
                            {
                                var at=Where(run,next);
                                timer=Flat(at,aim)<1.3f?NearSeconds:AimSeconds;mode=Mode.Moving;target=next;aim=at;
                            }
                            else{mode=Mode.Spraying;cast=!run.GemEmpty && !waiting;}
                        }
                    }
                }
                float amount=run.TickMagic(Dt,cast,true);
                if(amount>0 && target.Kind!=0)
                {
                    run.ApplyWater(Where(run,target),amount,run.MagicRadius,EmberdeepBoardTuning.FullSoak);
                    if(soakLeft>0)soakLeft-=Dt;
                }
                if(run.GemEmpty && !wasDry){r.TimesDry++;if(r.FirstDry<0)r.FirstDry=run.Elapsed;}
                wasDry=run.GemEmpty;
                if(run.Phase!=FirePhase.Fighting)break;
                // The forecast read just before the frame must be the catch that frame makes (LoopSpec §4.4).
                int expected=run.ForecastCell(0);
                if(before.Length!=run.CellCount)before=new TreeState[run.CellCount];
                for(int c=0;c<before.Length;c++)before[c]=run.Cell(c).State;
                run.Tick(Dt,false);
                for(int c=0;c<before.Length;c++)
                    if(before[c]==TreeState.Safe && run.Cell(c).State==TreeState.Burning){r.Catches++;r.ForecastChecked++;if(c!=expected)r.ForecastMisses++;}
            }
            r.Phase=run.Phase;r.Seconds=run.Elapsed;r.Burnt=run.Burnt;r.Stars=run.Stars;
            r.SheepSaved=run.AnimalsSaved;r.SheepLost=run.AnimalsLost;r.CabinsSaved=run.CabinsSaved;
            return r;
        }
        // Tuning summary for one stage: no player, the bot, and the bot without its pieces / pump / soaking.
        public static string Report(int stage)
        {
            var sb=new StringBuilder();
            sb.Append("idle    ").Append(Idle(stage)).AppendLine();
            sb.Append("bot     ").Append(new EmberdeepLevelBot().Play(stage)).AppendLine();
            sb.Append("soakall ").Append(new EmberdeepLevelBot{Skilled=true}.Play(stage)).AppendLine();
            sb.Append("nopiece ").Append(new EmberdeepLevelBot{UsePieces=false}.Play(stage)).AppendLine();
            sb.Append("nopump  ").Append(new EmberdeepLevelBot{Pump=false}.Play(stage)).AppendLine();
            var level=EmberdeepLevels.For(stage);
            if(level.DigHint.Length>0)sb.Append("nodig   ").Append(new EmberdeepLevelBot{Dig=false}.Play(stage)).AppendLine();
            if(level.Pieces.Plane)sb.Append("noplane ").Append(new EmberdeepLevelBot{Plane=false}.Play(stage)).AppendLine();
            return sb.ToString();
        }
        // No player at all: when is the level lost?
        public static Result Idle(int stage,float max=300)
        {
            var bot=new EmberdeepLevelBot{Act=false,UsePieces=false,MaxSeconds=max};return bot.Play(stage);
        }

        Target Choose(FireRun run,Target current,ref float soakLeft)
        {
            // 1. A sheep on fire.
            int sheep=-1;float best=float.MaxValue;
            for(int i=0;i<run.Animals.Length;i++)
            {
                var a=run.Animals[i];if(!a.OnFire || (a.State!=AnimalState.Waiting && a.State!=AnimalState.Running))continue;
                float d=current.Kind==3 && current.Index==i?-1:i;if(d<best){best=d;sheep=i;}
            }
            if(sheep>=0)return new Target{Kind=3,Index=sheep};
            if(current.Kind==3)
            {
                var a=run.Animals[current.Index];
                if((a.State==AnimalState.Waiting || a.State==AnimalState.Running) && a.Wetness<SheepWetness)return current;
            }
            // 2. Soak the next tree to catch, if it catches soon and is dry (a short tap is enough).
            int f=run.ForecastCell(0);
            if(current.Kind!=0 && soakLeft>0 && IsCell(run,current) && run.Cell(CellOf(run,current)).State==TreeState.Safe)return current;
            soakLeft=0;
            float catchAt=f>=0?run.Elapsed+run.ForecastSeconds(0):-1;
            if(Soak && f>=0 && run.ForecastSeconds(0)<=SoakLead && run.Cell(f).Wetness<EmberdeepBoardTuning.WetBlock && (Skilled || Mathf.Abs(catchAt-soakedFor)>.01f))
            {soakLeft=SoakSeconds;soakedFor=catchAt;return CellTarget(run,f);}
            // 3. Keep putting out the current fire; otherwise the fire closest to the next catch.
            if(IsCell(run,current) && run.Cell(CellOf(run,current)).State==TreeState.Burning)return current;
            var front=f>=0?run.Cell(f).Position:new Vector3(0,0,3);
            int pick=-1;best=float.MaxValue;
            for(int c=0;c<run.CellCount;c++)
            {
                var fuel=run.Cell(c);if(fuel.State!=TreeState.Burning)continue;
                // Trees first: grass never chars toward the limit and burns out in seconds (a clear needs trees out only),
                // so it gets water only when no tree is burning near the front.
                float d=Flat(fuel.Position,front)+(run.IsGrassCell(c)?3:0)+fuel.Heat*.002f;
                if(d<best){best=d;pick=c;}
            }
            return pick<0?new Target():CellTarget(run,pick);
        }
        static bool IsCell(FireRun run,Target t)=>t.Kind==1 || t.Kind==2;
        static int CellOf(FireRun run,Target t)=>t.Kind==1?t.Index:run.Trees.Length+t.Index;
        static Target CellTarget(FireRun run,int cell)=>run.IsGrassCell(cell)?new Target{Kind=2,Index=cell-run.Trees.Length}:new Target{Kind=1,Index=cell};
        static Vector3 Where(FireRun run,Target t)
        {
            if(t.Kind==1)return run.Trees[t.Index].Position;
            if(t.Kind==2)return run.Grass[t.Index].Position;
            if(t.Kind==3)return run.Animals[t.Index].Position+Vector3.up*FireRun.AnimalHeight;
            return Vector3.zero;
        }
        static float Flat(Vector3 a,Vector3 b)=>Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z));
        // The line over the most burning fuel (eight headings through every square).
        static bool CallPlane(FireRun run)
        {
            int bestSquare=-1;float bestYaw=0;int most=1;
            for(int s=0;s<run.Squares.Length;s++)
                for(int k=0;k<8;k++)
                {
                    float yaw=k*22.5f;var dir=FireRun.YawDirection(yaw);var at=run.Squares[s];int n=0;
                    for(int c=0;c<run.CellCount;c++)
                    {
                        var p=run.Cell(c);if(p.State!=TreeState.Burning || run.IsGrassCell(c))continue;
                        float across=Mathf.Abs((p.Position.x-at.x)*dir.z-(p.Position.z-at.z)*dir.x);
                        if(across<=EmberdeepPieceTuning.PlaneHalfWidth)n++;
                    }
                    if(n>most){most=n;bestSquare=s;bestYaw=yaw;}
                }
            return bestSquare>=0 && run.CallPlane(bestSquare,bestYaw);
        }
    }
}

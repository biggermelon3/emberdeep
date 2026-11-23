using System;
using System.Collections.Generic;
using UnityEngine;

namespace FireRescue
{
    public enum FireFuel { Tree, Grass }

    // LoopSpec §4.1-4.4 (workstream L2): every board-sim number in one block. All values are *tune*.
    // These rules run in the Emberdeep (goddess) flow only; the legacy tool flow keeps its original
    // spread order, 1.7 reach and .7/s drying (§11: legacy stays untouched).
    public static class EmberdeepBoardTuning
    {
        public readonly struct Fuel
        {
            // IgniteHeat: heat on catching, i.e. the water it takes to put out.
            // Burn (+BurnPerIncident x incident): fuel used per second of 100; at 0 it is charred.
            // Catch (-CatchPerIncident x incident, at least 1): seconds between this fuel's catches.
            // WetDecay: wetness lost per second.
            public readonly float IgniteHeat, Burn, BurnPerIncident, Catch, CatchPerIncident, WetDecay;
            public Fuel(float igniteHeat,float burn,float burnPerIncident,float catchSeconds,float catchPerIncident,float wetDecay)
            {IgniteHeat=igniteHeat;Burn=burn;BurnPerIncident=burnPerIncident;Catch=catchSeconds;CatchPerIncident=catchPerIncident;WetDecay=wetDecay;}
            public float CatchSeconds(int incident)=>Mathf.Max(1,Catch-CatchPerIncident*incident);
            public float BurnPerSecond(int incident)=>Burn+BurnPerIncident*incident;
        }
        //                                          heat  burn/s  +/inc  catch s  -/inc  dry/s
        public static readonly Fuel Tree =new Fuel( 100,  .8f,    .15f,  10,      1,     .35f);
        public static readonly Fuel Grass=new Fuel(  28,  14,     0,     3,       0,     .35f);
        public static Fuel Of(FireFuel kind)=>kind==FireFuel.Grass?Grass:Tree;
        // Grass: sim anchor height in forest units (tufts are about .3 tall) and the score for putting a patch out.
        public const float GrassHeight=.12f;
        // The grass clock starts half a period in (first catch at 1.5 s), so grass catches fall on half
        // seconds and never tie with a tree catch (whole seconds): the order never depends on frame rate.
        public const float GrassClockOffset=.5f;
        public const int GrassScore=20;
        // §4.3 wetness. Palm water soaks to FullSoak; Spread() skips fuel while Wetness >= WetBlock.
        // A full soak blocks for (FullSoak-WetBlock)/WetDecay = 8 s.
        public const float FullSoak=3, WetBlock=.2f;
        // The wet look (darker, glossy) is full down to WetLookFull and fades out exactly as the block ends,
        // so "looks wet" always means "won't catch".
        public const float WetLookFull=1.4f;
        public static float WetLook(float wetness)=>Mathf.Clamp01((wetness-WetBlock)/(WetLookFull-WetBlock));
        // §4.2 wind. Reach toward a neighbour at angle a from downwind = BaseReach + WindReach*strength*cos(a):
        // 2.2 downwind and 1.2 upwind at strength 1, 1.7 everywhere when calm.
        public const float BaseReach=1.7f, WindReach=.5f;
        public const float WindWarningSeconds=3;
        // §4.4 forecast: how many upcoming catches to show, and how far ahead to look.
        public const int ForecastSlots=2, ForecastEvents=24;
        public const float ForecastHorizon=45, ForecastRefreshSeconds=.5f;
    }

    // One level's wind. Degrees name the downwind direction on the board: 0 away from the player (+z),
    // 90 to the right (+x), 180 toward the player. Strength 0 (calm) to 1.
    [Serializable]
    public struct FireWindPlan
    {
        public float Degrees, Strength;
        public float ShiftAt, ShiftDegrees, ShiftStrength; // ShiftAt in fight seconds; 0 = no shift
        public bool Shifts=>ShiftAt>0;
        public FireWindPlan(float degrees,float strength,float shiftAt=0,float shiftDegrees=0,float shiftStrength=0)
        {Degrees=degrees;Strength=strength;ShiftAt=shiftAt;ShiftDegrees=shiftDegrees;ShiftStrength=shiftStrength;}
        public static Vector3 Direction(float degrees)=>new Vector3(Mathf.Sin(degrees*Mathf.Deg2Rad),0,Mathf.Cos(degrees*Mathf.Deg2Rad));
    }

    public sealed partial class FireRun
    {
        // Keyed by IncidentIndex. The levels author their wind (EmberdeepLevels, workstream LV); this is the
        // incident stages' copy. Keep shifts off whole and half seconds (catch times).
        public static readonly FireWindPlan[] Winds=EmberdeepLevels.IncidentWinds();

        public bool BoardRules=>GoddessTutorial;
        // Per-level fire clocks (EmberdeepLevel.TreeCatch/TreeBurn); grass, and a board without a level, use the
        // fuel tuning's per-incident values.
        public float CatchSeconds(FireFuel kind)=>kind==FireFuel.Tree && Level!=null?Mathf.Max(1,Level.TreeCatch):EmberdeepBoardTuning.Of(kind).CatchSeconds(IncidentIndex);
        public float BurnPerSecond(FireFuel kind)=>kind==FireFuel.Tree && Level!=null?100/Mathf.Max(1,Level.TreeBurn):EmberdeepBoardTuning.Of(kind).BurnPerSecond(IncidentIndex);
        public Tree[] Grass { get; private set; }=Array.Empty<Tree>();
        // Cells index trees first, then grass.
        public int CellCount=>Trees.Length+Grass.Length;
        public Tree Cell(int cell)=>cell<Trees.Length?Trees[cell]:Grass[cell-Trees.Length];
        public bool IsGrassCell(int cell)=>cell>=Trees.Length;
        public int GrassBurning { get { int n=0; foreach(var g in Grass) if(g.State==TreeState.Burning)n++; return n; } }

        // Dev/test override for this run's wind; null uses the table.
        public FireWindPlan? WindOverride { get=>windOverride; set{windOverride=value;version++;} }
        FireWindPlan? windOverride;
        public FireWindPlan WindPlan=>!BoardRules?default:windOverride??(Level!=null?Level.Wind:Winds[Mathf.Clamp(IncidentIndex,0,Winds.Length-1)]);
        // Wind is a pure function of fight time, so the forecast can look past a shift.
        public Vector3 WindAt(float elapsed,out float strength)
        {
            var plan=WindPlan;bool shifted=plan.Shifts && elapsed>=plan.ShiftAt;
            strength=Mathf.Clamp01(shifted?plan.ShiftStrength:plan.Strength);
            return FireWindPlan.Direction(shifted?plan.ShiftDegrees:plan.Degrees);
        }
        public Vector3 WindDirection=>WindAt(Elapsed,out _);
        public float WindStrength { get { WindAt(Elapsed,out var s); return s; } }
        public bool WindShiftPending=>BoardRules && WindPlan.Shifts && Elapsed<WindPlan.ShiftAt;
        public float WindShiftIn=>WindShiftPending?WindPlan.ShiftAt-Elapsed:-1;
        // The ~3 s heads-up before a shift: the weathervane swings and gusts play.
        public bool WindWarning=>Phase==FirePhase.Fighting && WindShiftPending && WindShiftIn<=EmberdeepBoardTuning.WindWarningSeconds;
        public Vector3 NextWindDirection=>WindShiftPending?FireWindPlan.Direction(WindPlan.ShiftDegrees):WindDirection;
        public float NextWindStrength=>WindShiftPending?Mathf.Clamp01(WindPlan.ShiftStrength):WindStrength;

        float grassSpreadTime;
        int version; // bumps on every change the forecast depends on
        struct FuelSnap { public float X, Z, Wetness, Fuel; public TreeState State; public FireFuel Kind; }
        FuelSnap[] cells=Array.Empty<FuelSnap>();
        int[] sources=Array.Empty<int>();

        // Grass is authored per level (EmberdeepLevels: g/G on squares and meadow cells). No level, no grass.
        void BuildGrass(int columns)
        {
            Grass=Array.Empty<Tree>();grassSpreadTime=0;version++;
            if(!BoardRules || Level==null)return;
            var patches=Level.Grass;var list=new List<Tree>(patches.Length);
            foreach(var patch in patches)
                list.Add(new Tree{Position=new Vector3(patch.At.x,EmberdeepBoardTuning.GrassHeight,patch.At.y),Kind=FireFuel.Grass,
                    State=patch.Burning?TreeState.Burning:TreeState.Safe,Heat=patch.Burning?EmberdeepBoardTuning.Grass.IgniteHeat:0});
            Grass=list.ToArray();
            if(Grass.Length>0)grassSpreadTime=CatchSeconds(FireFuel.Grass)*EmberdeepBoardTuning.GrassClockOffset;
        }

        // Board rules, once per fighting frame: drying, burning, then catches in time order across the
        // tree and grass clocks. Overshoot carries over, so the forecast's timeline matches frame-stepped play.
        void TickBoard(float dt)
        {
            bool still=StillLesson;
            for(int c=0;c<CellCount;c++)
            {
                var fuel=Cell(c);var spec=EmberdeepBoardTuning.Of(fuel.Kind);
                fuel.Wetness=Mathf.Max(0,fuel.Wetness-dt*spec.WetDecay);
                if(fuel.State!=TreeState.Burning || still)continue;
                fuel.Fuel-=dt*BurnPerSecond(fuel.Kind);
                if(fuel.Fuel<=0){fuel.State=TreeState.Burnt;version++;}
            }
            if(still)return;
            float treeEvery=CatchSeconds(FireFuel.Tree),grassEvery=CatchSeconds(FireFuel.Grass);
            spreadTime+=dt;if(Grass.Length>0)grassSpreadTime+=dt;
            for(int guard=0;guard<4;guard++)
            {
                float treeOver=spreadTime-treeEvery,grassOver=Grass.Length>0?grassSpreadTime-grassEvery:-1;
                if(treeOver<0 && grassOver<0)break;
                // The larger overshoot happened first; a tie goes to trees, as in the forecast.
                bool tree=treeOver>=grassOver;
                if(tree)spreadTime-=treeEvery;else grassSpreadTime-=grassEvery;
                Catch(tree?FireFuel.Tree:FireFuel.Grass);
            }
        }
        void Catch(FireFuel kind)
        {
            int count=Snapshot();var wind=WindAt(Elapsed,out float strength);
            int cell=Choose(cells,count,kind,wind,strength,ref sources,cuts,cutCount);
            if(cell<0)return;
            var fuel=Cell(cell);fuel.State=TreeState.Burning;fuel.Heat=EmberdeepBoardTuning.Of(kind).IgniteHeat;version++;
        }
        int Snapshot()
        {
            int count=CellCount;
            if(cells.Length<count){cells=new FuelSnap[count];sources=new int[count];}
            for(int c=0;c<count;c++)
            {
                var fuel=Cell(c);
                cells[c]=new FuelSnap{X=fuel.Position.x,Z=fuel.Position.z,Wetness=fuel.Wetness,Fuel=fuel.Fuel,State=fuel.State,Kind=fuel.Kind};
            }
            return count;
        }
        // The one deterministic catch choice, shared by play and the forecast. Among Safe, dry fuel of
        // this kind within wind-scaled reach of any burning fuel, the lowest distance/reach wins: downwind
        // neighbours first, then the nearest. Ties go to the lower cell index, then the lower source.
        // cuts: the excavator's dug lines (FireRun.Pieces.cs); no catch crosses one.
        static int Choose(FuelSnap[] cells,int count,FireFuel kind,Vector3 wind,float strength,ref int[] sources,Vector4[] cuts=null,int cutCount=0)
        {
            if(sources.Length<count)sources=new int[count];
            int burning=0;
            for(int i=0;i<count;i++)if(cells[i].State==TreeState.Burning)sources[burning++]=i;
            if(burning==0)return -1;
            int best=-1;float bestScore=float.PositiveInfinity;
            for(int j=0;j<count;j++)
            {
                if(cells[j].Kind!=kind || cells[j].State!=TreeState.Safe || cells[j].Wetness>=EmberdeepBoardTuning.WetBlock)continue;
                for(int s=0;s<burning;s++)
                {
                    int i=sources[s];
                    float dx=cells[j].X-cells[i].X,dz=cells[j].Z-cells[i].Z,d=Mathf.Sqrt(dx*dx+dz*dz);
                    float along=d>1e-5f?(dx*wind.x+dz*wind.z)/d:0;
                    float reach=EmberdeepBoardTuning.BaseReach+EmberdeepBoardTuning.WindReach*strength*along;
                    if(d>=reach)continue;
                    if(cutCount>0 && CutBetween(cuts,cutCount,cells[i].X,cells[i].Z,cells[j].X,cells[j].Z))continue;
                    float score=d/reach;
                    if(score<bestScore){bestScore=score;best=j;}
                }
            }
            return best;
        }

        // §4.4: the next catches if nobody intervenes, from the same Choose() and clocks the sim uses.
        readonly int[] forecast={-1,-1};
        readonly float[] forecastAt=new float[EmberdeepBoardTuning.ForecastSlots];
        int forecastVersion=-1;float forecastElapsed=-1;
        public int ForecastCell(int slot){EnsureForecast();return slot>=0 && slot<forecast.Length?forecast[slot]:-1;}
        // Fight seconds until that catch (Plan phase: seconds after Start).
        public float ForecastSeconds(int slot){EnsureForecast();return slot>=0 && slot<forecast.Length && forecast[slot]>=0?Mathf.Max(0,forecastAt[slot]-Elapsed):-1;}
        // For code that edits fuel state directly (tests, tools); the sim's own changes are tracked.
        public void InvalidateForecast()=>version++;
        void EnsureForecast()
        {
            if(forecastVersion==version && Elapsed>=forecastElapsed && Elapsed-forecastElapsed<EmberdeepBoardTuning.ForecastRefreshSeconds
                && (forecast[0]<0 || Elapsed<=forecastAt[0]))return;
            forecastVersion=version;forecastElapsed=Elapsed;
            for(int k=0;k<forecast.Length;k++)forecast[k]=-1;
            if(!BoardRules || (Phase!=FirePhase.Fighting && Phase!=FirePhase.Preparing) || StillLesson)return;
            int count=Snapshot();
            bool hasGrass=Grass.Length>0;
            float treeEvery=CatchSeconds(FireFuel.Tree),grassEvery=CatchSeconds(FireFuel.Grass);
            float treeClock=spreadTime,grassClock=grassSpreadTime,t=0;int found=0;
            for(int e=0;e<EmberdeepBoardTuning.ForecastEvents && found<forecast.Length;e++)
            {
                float toTree=Mathf.Max(0,treeEvery-treeClock),toGrass=hasGrass?Mathf.Max(0,grassEvery-grassClock):float.PositiveInfinity;
                bool tree=toTree<=toGrass;float step=tree?toTree:toGrass;
                if(t+step>EmberdeepBoardTuning.ForecastHorizon)break;
                Advance(count,step);t+=step;
                treeClock+=step;if(hasGrass)grassClock+=step;
                if(tree)treeClock-=treeEvery;else grassClock-=grassEvery;
                var wind=WindAt(Elapsed+t,out float strength);
                int cell=Choose(cells,count,tree?FireFuel.Tree:FireFuel.Grass,wind,strength,ref sources,cuts,cutCount);
                if(cell<0)continue;
                cells[cell].State=TreeState.Burning;cells[cell].Fuel=100;
                forecast[found]=cell;forecastAt[found]=Elapsed+t;found++;
            }
        }
        void Advance(int count,float seconds)
        {
            if(seconds<=0)return;
            for(int c=0;c<count;c++)
            {
                var spec=EmberdeepBoardTuning.Of(cells[c].Kind);
                cells[c].Wetness=Mathf.Max(0,cells[c].Wetness-seconds*spec.WetDecay);
                if(cells[c].State!=TreeState.Burning)continue;
                cells[c].Fuel-=seconds*BurnPerSecond(cells[c].Kind);
                if(cells[c].Fuel<=0)cells[c].State=TreeState.Burnt;
            }
        }

        // Water on grass: the patch soaks like a tree and goes out at heat 0 (no combo, small score).
        bool ApplyGrassWater(Vector3 point,float amount,float radius,float wetness)
        {
            bool changed=false;
            foreach(var grass in Grass)
            {
                if(Vector3.Distance(grass.Position,point)>radius)continue;
                if(wetness>grass.Wetness){grass.Wetness=wetness;changed=true;}
                if(grass.State!=TreeState.Burning)continue;
                grass.Heat=Mathf.Max(0,grass.Heat-amount);changed=true;
                if(grass.Heat>0)continue;
                grass.State=TreeState.Saved;Score+=EmberdeepBoardTuning.GrassScore;
            }
            return changed;
        }
        // A contained fire smothers any grass still alight; nothing ticks after the outcome.
        void SmotherGrass()
        {
            foreach(var grass in Grass)if(grass.State==TreeState.Burning){grass.State=TreeState.Saved;grass.Heat=0;version++;}
        }
    }
}

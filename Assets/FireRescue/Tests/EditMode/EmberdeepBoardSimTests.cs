using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    // LoopSpec §4.1-4.4 (workstream L2): grass, wind, lasting wetness and the forecast, in the goddess flow.
    public sealed class EmberdeepBoardSimTests
    {
        // The field layout belongs to the generator (workstream O reshapes it), so these tests find their
        // trees by position: an interior tree with a close neighbour on each side, on a board with grass.
        // Since the levels are hand-authored (LV), the rules run on the generated grids they were written against
        // (EmberdeepLevels.GeneratedGrid via FireRun.LevelOverride), so retuning a level never moves these tests.
        sealed class Cross { public FireRun Run; public int Centre, Right, Left, Back, Front; }
        static Cross Field()
        {
            for(int stage=5;stage>=2;stage--) // incidents 4..1 (stage cut, T: was stages 7..4)
            {
                var run=Board(stage);if(run.Grass.Length==0)continue;
                for(int i=0;i<run.Trees.Length;i++)
                {
                    int r=Toward(run,i,Vector2.right,1.3f),l=Toward(run,i,Vector2.left,1.3f),b=Toward(run,i,Vector2.up,1.3f),f=Toward(run,i,Vector2.down,1.3f);
                    bool grass=false;foreach(var g in run.Grass)grass|=Flat(g.Position,run.Trees[i].Position)<EmberdeepBoardTuning.BaseReach;
                    if(r>=0 && l>=0 && b>=0 && f>=0 && grass)return new Cross{Run=run,Centre=i,Right=r,Left=l,Back=b,Front=f};
                }
            }
            Assert.Fail("no interior tree with grass nearby on any board");return null;
        }
        static float Flat(Vector3 a,Vector3 b)=>Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z));
        // The nearest tree from `from` within 25 degrees of `direction` (board x/z), between `beyond` and `within` away.
        static int Toward(FireRun run,int from,Vector2 direction,float within,float beyond=0)
        {
            int best=-1;float near=within;var p=run.Trees[from].Position;
            for(int j=0;j<run.Trees.Length;j++)
            {
                var d=new Vector2(run.Trees[j].Position.x-p.x,run.Trees[j].Position.z-p.z);float m=d.magnitude;
                if(j==from || m<=beyond || m>=near || Vector2.Angle(d,direction)>25)continue;
                near=m;best=j;
            }
            return best;
        }
        static float TreeEvery(FireRun run)=>run.CatchSeconds(FireFuel.Tree);

        static FireRun Board(int stage)
        {
            var run=new FireRun(true);typeof(FireRun).GetProperty("Stage").SetValue(run,stage);run.LevelOverride=EmberdeepLevels.GeneratedGrid(stage);
            Assert.IsTrue(run.StartStage());Assert.AreEqual(FirePhase.Fighting,run.Phase);return run;
        }
        // Only the given trees burn; grass is taken out of play unless asked for.
        static void Only(FireRun run,bool grass,params int[] burning)
        {
            foreach(var t in run.Trees){t.State=TreeState.Safe;t.Heat=0;t.Wetness=0;t.Fuel=100;}
            foreach(var g in run.Grass){g.State=grass?TreeState.Safe:TreeState.Burnt;g.Heat=0;g.Wetness=0;g.Fuel=100;}
            foreach(var b in burning){run.Trees[b].State=TreeState.Burning;run.Trees[b].Heat=100;}
            run.InvalidateForecast();
        }
        static void Run(FireRun run,float seconds){float end=run.Elapsed+seconds;for(int i=0;i<10000 && run.Elapsed<end-1e-4f && run.Phase==FirePhase.Fighting;i++)run.Tick(.05f,false);}
        static TreeState[] States(FireRun run){var s=new TreeState[run.CellCount];for(int c=0;c<s.Length;c++)s[c]=run.Cell(c).State;return s;}
        // Ticks until the next catch (or the time runs out) and returns the cell that caught.
        static int NextCatch(FireRun run,float seconds,float dt=.05f)
        {
            for(float t=0;t<seconds && run.Phase==FirePhase.Fighting;t+=dt)
            {
                var before=States(run);run.Tick(dt,false);
                for(int c=0;c<before.Length;c++)if(before[c]==TreeState.Safe && run.Cell(c).State==TreeState.Burning)return c;
            }
            return -1;
        }
        // Every catch over an idle stretch, in order, with the forecast read just before each frame.
        static List<int> Play(FireRun run,float seconds,float dt,List<int> forecasts=null)
        {
            var caught=new List<int>();
            for(float t=0;t<seconds && run.Phase==FirePhase.Fighting;t+=dt)
            {
                int expected=run.ForecastCell(0);var before=States(run);run.Tick(dt,false);
                for(int c=0;c<before.Length;c++)if(before[c]==TreeState.Safe && run.Cell(c).State==TreeState.Burning){caught.Add(c);forecasts?.Add(expected);}
            }
            return caught;
        }

        [TestCase(90f)] [TestCase(270f)] [TestCase(0f)] [TestCase(180f)]
        public void DownwindNeighbourCatchesFirst(float degrees)
        {
            var x=Field();var run=x.Run;run.WindOverride=new FireWindPlan(degrees,1);Only(run,false,x.Centre);
            int expected=degrees==90?x.Right:degrees==270?x.Left:degrees==0?x.Back:x.Front;
            Assert.AreEqual(expected,run.ForecastCell(0),"forecast");
            Assert.AreEqual(expected,NextCatch(run,TreeEvery(run)+1),"actual");
        }
        [Test] public void WindReachIsLongerDownwindAndShorterUpwind()
        {
            // Two trees on a diagonal between 1.2 and 1.7 apart: in reach when calm (1.7) and downwind (up to 2.2),
            // out of reach upwind (1.2).
            var x=Field();int corner=x.Centre,diagonal=-1;
            foreach(var way in new[]{new Vector2(1,1),new Vector2(-1,1),new Vector2(1,-1),new Vector2(-1,-1)})
                if(diagonal<0)diagonal=Toward(x.Run,corner,way,EmberdeepBoardTuning.BaseReach-.02f,EmberdeepBoardTuning.BaseReach-EmberdeepBoardTuning.WindReach+.02f);
            if(diagonal<0)Assert.Inconclusive("this field has no diagonal neighbours between 1.2 and 1.7 apart");
            int stage=x.Run.Stage;var p=x.Run.Trees[corner].Position;var q=x.Run.Trees[diagonal].Position;
            float upwind=Mathf.Atan2(p.x-q.x,p.z-q.z)*Mathf.Rad2Deg; // blowing from the diagonal back at the burning tree
            var calm=Board(stage);calm.WindOverride=new FireWindPlan(0,0);Only(calm,false,corner);
            foreach(var t in calm.Trees)if(t!=calm.Trees[corner] && t!=calm.Trees[diagonal])t.State=TreeState.Saved;
            calm.InvalidateForecast();Assert.AreEqual(diagonal,NextCatch(calm,TreeEvery(calm)+1),"calm reach 1.7 covers the diagonal");
            var up=Board(stage);up.WindOverride=new FireWindPlan(upwind,1);Only(up,false,corner);
            foreach(var t in up.Trees)if(t!=up.Trees[corner] && t!=up.Trees[diagonal])t.State=TreeState.Saved;
            up.InvalidateForecast();Assert.AreEqual(-1,up.ForecastCell(0));Assert.AreEqual(-1,NextCatch(up,30),"1.2 upwind reach stops it");
            Assert.AreEqual(2.2f,EmberdeepBoardTuning.BaseReach+EmberdeepBoardTuning.WindReach,1e-5f);
            Assert.AreEqual(1.2f,EmberdeepBoardTuning.BaseReach-EmberdeepBoardTuning.WindReach,1e-5f);
        }
        [Test] public void SpreadIsDeterministicAcrossRunsAndFrameRates()
        {
            // Incident 3: 30 trees, 10 grass patches, wind to the left that shifts toward the player at 26.25 s.
            var a=Board(4);var b=Board(4);var c=Board(4); // FOREST FRONT (stage 4 since the stage cut)
            Assert.Greater(a.Grass.Length,0);Assert.IsTrue(a.WindPlan.Shifts);
            var first=Play(a,60,.05f);var second=Play(b,60,.05f);var coarse=Play(c,60,.1f);
            Assert.Greater(first.Count,4,"fire spread through trees and grass");
            CollectionAssert.AreEqual(first,second,"same inputs, same catches");
            CollectionAssert.AreEqual(first,coarse,"10 Hz and 20 Hz frames give the same catch order");
        }
        [Test] public void ForecastIsTheActualNextCatch()
        {
            var run=Board(4);var forecasts=new List<int>();var caught=Play(run,70,.05f,forecasts);
            Assert.Greater(caught.Count,4);
            CollectionAssert.AreEqual(caught,forecasts,"every catch was the forecast's first slot the frame before");
            // The second slot is the catch after the next one, when nobody intervenes.
            var again=Board(4);int second=again.ForecastCell(1);Assert.GreaterOrEqual(second,0);
            Assert.AreEqual(again.ForecastCell(0),NextCatch(again,20));Assert.AreEqual(second,NextCatch(again,20));
        }
        [Test] public void PlanPhaseForecastIsWhatCatchesAfterStart()
        {
            var run=new FireRun(true);typeof(FireRun).GetProperty("Stage").SetValue(run,4);run.LevelOverride=EmberdeepLevels.GeneratedGrid(4); // was stage 6 before the stage cut
            Assert.IsTrue(run.StartStage(true));Assert.AreEqual(FirePhase.Preparing,run.Phase);
            int next=run.ForecastCell(0),after=run.ForecastCell(1);
            Assert.GreaterOrEqual(next,0);Assert.GreaterOrEqual(after,0);Assert.Greater(run.ForecastSeconds(0),0);
            for(int i=0;i<200;i++)run.TickPreparation(.1f);Assert.AreEqual(FirePhase.Preparing,run.Phase,"time is stopped in Plan");
            Assert.AreEqual(next,run.ForecastCell(0));
            run.BeginCountdown();for(int i=0;i<31 && run.Phase==FirePhase.Preparing;i++)run.TickPreparation(.1f);
            Assert.AreEqual(FirePhase.Fighting,run.Phase);
            Assert.AreEqual(next,NextCatch(run,20));Assert.AreEqual(after,NextCatch(run,20));
        }
        [Test] public void SoakingTheForecastTreeMovesTheForecastHonestly()
        {
            var x=Field();var run=x.Run;int Right=x.Right;run.WindOverride=new FireWindPlan(90,1);Only(run,false,x.Centre);
            Assert.AreEqual(Right,run.ForecastCell(0));
            Run(run,TreeEvery(run)-4);Assert.AreEqual(Right,run.ForecastCell(0));
            // Soaked 4 s before the catch it is still wet then; soaked 9+ s before it would be dry again.
            run.ApplyWater(run.Trees[Right].Position,1,.1f,EmberdeepBoardTuning.FullSoak);
            int next=run.ForecastCell(0);Assert.AreNotEqual(Right,next,"a tree still wet at the catch is not forecast");
            Assert.AreEqual(next,NextCatch(run,6));Assert.AreEqual(TreeState.Safe,run.Trees[Right].State);
        }
        [Test] public void FullSoakProtectsForAboutEightSeconds()
        {
            Assert.AreEqual(8,(EmberdeepBoardTuning.FullSoak-EmberdeepBoardTuning.WetBlock)/EmberdeepBoardTuning.Tree.WetDecay,.01f);
            var x=Field();var run=x.Run;int Right=x.Right;run.WindOverride=new FireWindPlan(90,1);Only(run,false,x.Centre);
            // Soak the downwind tree 4 s before the tree catch: it is still wet then, and is passed over.
            Run(run,TreeEvery(run)-4);
            run.ApplyWater(run.Trees[Right].Position,1,.1f,EmberdeepBoardTuning.FullSoak);float soaked=run.Elapsed;
            int first=NextCatch(run,6);Assert.GreaterOrEqual(first,0);Assert.AreNotEqual(Right,first);
            Assert.AreEqual(TreeState.Safe,run.Trees[Right].State,"wet at the catch: skipped");
            Run(run,7.9f-(run.Elapsed-soaked));
            Assert.GreaterOrEqual(run.Trees[Right].Wetness,EmberdeepBoardTuning.WetBlock,"still protected at 7.9 s");
            Assert.Greater(EmberdeepBoardTuning.WetLook(run.Trees[Right].Wetness),0,"and still looks wet");
            Run(run,8.1f-(run.Elapsed-soaked));
            Assert.Less(run.Trees[Right].Wetness,EmberdeepBoardTuning.WetBlock,"dry again after 8 s");
            Assert.Zero(EmberdeepBoardTuning.WetLook(run.Trees[Right].Wetness),"looks dry exactly when it can catch");
            foreach(var t in run.Trees)if(t!=run.Trees[x.Centre] && t!=run.Trees[Right])t.State=TreeState.Saved;
            run.InvalidateForecast();Assert.AreEqual(Right,run.ForecastCell(0),"once dry it can be forecast to catch again");
        }
        [Test] public void GrassIsShortOffTheTrunksAndCatchesBeforeTrees()
        {
            var x=Field();var run=x.Run;Assert.Greater(run.Grass.Length,0);
            foreach(var g in run.Grass)
            {
                Assert.Less(g.Position.y,.3f);Assert.AreEqual(FireFuel.Grass,g.Kind);
                foreach(var t in run.Trees)Assert.Greater(Vector2.Distance(new Vector2(g.Position.x,g.Position.z),new Vector2(t.Position.x,t.Position.z)),.6f);
            }
            run.WindOverride=new FireWindPlan(0,0);Only(run,true,x.Centre);
            float start=run.Elapsed;int first=NextCatch(run,TreeEvery(run)+1);
            Assert.IsTrue(run.IsGrassCell(first),"grass catches first");Assert.Less(run.Elapsed-start,1.6f,"within its 1.5 s clock");
            Assert.AreEqual(EmberdeepBoardTuning.Grass.IgniteHeat,run.Cell(first).Heat,"low heat");
            Assert.Less(EmberdeepBoardTuning.Grass.IgniteHeat,EmberdeepBoardTuning.Tree.IgniteHeat);
        }
        [Test] public void GrassBurnsBrieflyAndNeverCountsAsCharredTrees()
        {
            var x=Field();var run=x.Run;run.WindOverride=new FireWindPlan(0,0);Only(run,true,x.Centre);
            int patch=NextCatch(run,TreeEvery(run)+1);Assert.IsTrue(run.IsGrassCell(patch));
            float lit=run.Elapsed;int burnt=run.Burnt;
            for(int i=0;i<400 && run.Cell(patch).State==TreeState.Burning;i++)run.Tick(.05f,false);
            Assert.AreEqual(TreeState.Burnt,run.Cell(patch).State);
            Assert.AreEqual(100/EmberdeepBoardTuning.Grass.Burn,run.Elapsed-lit,.15f,"burns about 7 s");
            Assert.AreEqual(burnt,run.Burnt,"charred grass is not a charred tree");
        }
        [Test] public void GrassIsQuickToPutOut()
        {
            var x=Field();var run=x.Run;int Centre=x.Centre;Only(run,true,Centre);
            var grass=run.Grass[0];grass.State=TreeState.Burning;grass.Heat=EmberdeepBoardTuning.Grass.IgniteHeat;
            int score=run.Score,rescued=run.Rescued;
            run.ApplyWater(grass.Position,30,.1f);run.ApplyWater(run.Trees[Centre].Position,30,.1f);
            Assert.AreEqual(TreeState.Saved,grass.State,"one splash puts grass out");
            Assert.AreEqual(TreeState.Burning,run.Trees[Centre].State);Assert.AreEqual(70,run.Trees[Centre].Heat,.001f);
            Assert.AreEqual(score+EmberdeepBoardTuning.GrassScore,run.Score);Assert.AreEqual(rescued,run.Rescued,"tree rescues only");
        }
        [Test] public void ClearingTheTreesSmothersBurningGrass()
        {
            var x=Field();var run=x.Run;int Centre=x.Centre;Only(run,true,Centre);
            var grass=run.Grass[0];grass.State=TreeState.Burning;grass.Heat=10;
            foreach(var a in run.Animals)run.ApplyAnimalWater(a.Position,100,.6f);
            run.ApplyWater(run.Trees[Centre].Position,1000,.1f);
            Assert.That(run.Phase,Is.EqualTo(FirePhase.Cleared).Or.EqualTo(FirePhase.Won),"contained means no burning trees");Assert.AreEqual(TreeState.Saved,grass.State);
            Assert.AreEqual(-1,run.ForecastCell(0),"no forecast after the fight");
        }
        [Test] public void WindShiftGivesThreeSecondsOfWarning()
        {
            var x=Field();var run=x.Run;run.WindOverride=new FireWindPlan(90,.8f,5.25f,270,1);Only(run,false,x.Centre);
            Assert.AreEqual(1,run.WindDirection.x,1e-4f);Assert.AreEqual(.8f,run.WindStrength,1e-5f);
            Run(run,2.1f);Assert.IsFalse(run.WindWarning);
            Run(run,.3f);Assert.IsTrue(run.WindWarning);
            Assert.AreEqual(-1,run.NextWindDirection.x,1e-4f);Assert.AreEqual(1,run.NextWindStrength,1e-5f);
            Run(run,5.2f-run.Elapsed);Assert.AreEqual(1,run.WindDirection.x,1e-4f,"old wind until the shift");
            Run(run,.1f);
            Assert.AreEqual(-1,run.WindDirection.x,1e-4f);Assert.IsFalse(run.WindWarning);Assert.IsFalse(run.WindShiftPending);
        }
        [Test] public void EveryIncidentHasAWindEntryAndPalmLessonsAreCalm()
        {
            Assert.AreEqual(FireRun.Names.Length,FireRun.Winds.Length);
            var lesson=new FireRun(true);Assert.IsTrue(lesson.StartStage());Assert.IsTrue(lesson.IsPalmLesson);
            Assert.Zero(lesson.WindStrength);Assert.Zero(lesson.Grass.Length);Assert.GreaterOrEqual(lesson.ForecastCell(0),0,"the one palm lesson is a live grove since the stage cut");
            foreach(var plan in FireRun.Winds)
                if(plan.Shifts)Assert.AreNotEqual(0,(plan.ShiftAt*2)%1,"shift off the whole and half-second catch times");
        }
        [Test] public void LegacyToolFlowKeepsItsOriginalRules()
        {
            var run=new FireRun();Assert.IsTrue(run.StartStage());Assert.IsFalse(run.BoardRules);
            Assert.Zero(run.Grass.Length);Assert.Zero(run.WindStrength);Assert.AreEqual(-1,run.ForecastCell(0));
            run.ApplyWater(run.Trees[0].Position+Vector3.forward*.01f,.01f,.05f,1);Assert.AreEqual(1,run.Trees[0].Wetness);
            for(int i=0;i<12;i++)run.Tick(.1f,false);
            Assert.AreEqual(1-1.2f*.7f,run.Trees[0].Wetness,.001f,"legacy drying stays .7/s");
        }
    }
}

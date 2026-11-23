using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    // LoopSpec §8 level gate (workstream LV): every hand-authored level (EmberdeepLevels) checked headless on the sim.
    //   - it fits the board and builds exactly what its map says;
    //   - with no player it is lost, in a 25-90 s band that grows across the levels;
    //   - a scripted reasonable player (EmberdeepLevelBot) clears it within par, with its pieces on the hint squares;
    //   - every hint square is legal in Plan and within seated reach;
    //   - the forecast names every catch, idle and in play;
    //   - each level's one new thing matters: without it the same player does worse.
    public sealed class EmberdeepLevelTests
    {
        const float LossMin=25, LossMax=90;
        // Horizontal distance from the seated eye that a hand reaches on the board: the back row of a 4-row field
        // (.68 m since the 2026-11-22 headset test moved the default board 10 cm out, EmberdeepBoardLayout) plus half a
        // square (.72 m on the centre line), and a couple of centimetres for a hint one column off it.
        const float ReachMetres=.74f;
        static int Stages=>EmberdeepLevels.Count;
        static EmberdeepLevel Level(int stage)=>EmberdeepLevels.For(stage);

        [Test] public void SixStagesOnePalmLessonThenTheFiveIncidents()
        {
            Assert.AreEqual(FireRun.PalmLessons+FireRun.Names.Length,Stages);
            var run=new FireRun(true);Assert.AreEqual(Stages,run.StageCount);
            Assert.AreEqual(Stages-FireRun.PalmLessons,FireRun.Winds.Length);Assert.AreEqual(Stages-FireRun.PalmLessons,FireRun.PieceSets.Length);
            Assert.IsNull(new FireRun().Level,"the legacy tool flow keeps its generated field");
        }

        [Test] public void EveryLevelFitsTheBoardAndBuildsWhatItsMapSays()
        {
            for(int stage=0;stage<Stages;stage++)
            {
                var level=Level(stage);string at="stage "+stage;
                Assert.LessOrEqual(level.Columns,EmberdeepBoardLayout.MaxColumns,at);Assert.LessOrEqual(level.Rows,EmberdeepBoardLayout.MaxRows,at);
                Assert.Greater(level.Starts,0,at);Assert.Greater(level.BurnLimit,0,at);Assert.Greater(level.Par,0,at);
                Assert.AreEqual(0,level.TreeCatch%1,1e-5f,at+": whole-second tree clock, so catches never tie the half-second grass clock");
                if(level.Wind.Shifts)Assert.AreNotEqual(0,(level.Wind.ShiftAt*2)%1,at+": shift off whole and half seconds");
                var run=EmberdeepLevelBot.Load(stage);
                Assert.AreSame(level,run.Level,at);
                Assert.AreEqual(level.Trees.Length,run.Trees.Length,at);Assert.AreEqual(level.Trees.Count(t=>t.Burning),run.Burning,at);
                Assert.AreEqual(level.Grass.Length,run.Grass.Length,at);Assert.AreEqual(level.Cabins.Length,run.CabinsTotal,at);
                Assert.AreEqual(level.SheepHomes.Length,run.Animals.Length,at);Assert.AreEqual(level.BurnLimit,run.BurnLimit,at);Assert.AreEqual(level.Par,run.ParTime,at);
                Assert.AreEqual(level.Pieces,run.PieceSet,at);
                Assert.Less(level.BurnLimit,run.Trees.Length,at+": the limit is reachable");
                // Grid cells map back to their trees.
                for(int i=0;i<level.Trees.Length;i++)Assert.AreEqual(i,run.FieldTree(level.Trees[i].Column,level.Trees[i].Row),at);
                foreach(var p in run.Props)
                {
                    Assert.Less(Mathf.Abs(p.Position.x),EmberdeepRimHud.DefaultBoard.extents.x-.5f,at+" cabin on the board");
                    foreach(var t in run.Trees)Assert.Greater(Flat(p.Position,t.Position),.55f,at+" cabin clear of trunks");
                    foreach(var a in run.Animals)Assert.Greater(Flat(p.Position,a.Home),.9f,at+" cabin clear of sheep homes");
                }
                foreach(var g in run.Grass)foreach(var t in run.Trees)Assert.Greater(Flat(g.Position,t.Position),.6f,at+" grass off the trunks");
                foreach(var a in run.Animals)
                {
                    foreach(var t in run.Trees)Assert.GreaterOrEqual(Flat(a.Home,t.Position),.45f,at+" sheep home in a lane");
                    Assert.Less(a.Refuge.z,EmberdeepBoardLayout.FrontRowZ,at);
                }
            }
            Assert.Throws<System.ArgumentException>(()=>{var _=new EmberdeepLevel{Map=new[]{". .",".T."}}.Trees;},"glyphs are checked");
            Assert.Throws<System.ArgumentException>(()=>{var _=new EmberdeepLevel{Map=new[]{"...",".T"}}.Trees;},"widths are checked");
        }

        [Test] public void WithNoPlayerEachLevelIsLostInABandThatGrows()
        {
            float previous=0;var sb=new StringBuilder();
            for(int stage=0;stage<Stages;stage++)
            {
                var idle=EmberdeepLevelBot.Idle(stage);sb.AppendLine($"stage {stage} idle: {idle}");
                Assert.AreEqual(FirePhase.Lost,idle.Phase,"stage "+stage);
                Assert.That(idle.Seconds,Is.InRange(LossMin,LossMax),"stage "+stage);
                Assert.GreaterOrEqual(idle.Seconds,previous,"stage "+stage+": no-action loss time grows across the levels");
                previous=idle.Seconds;
            }
            TestContext.WriteLine(sb.ToString());
        }

        [Test] public void AReasonablePlayerClearsEveryLevelWithinPar()
        {
            var sb=new StringBuilder();
            for(int stage=0;stage<Stages;stage++)
            {
                var r=new EmberdeepLevelBot().Play(stage);sb.AppendLine($"stage {stage} bot: {r}");
                Assert.True(r.Cleared,$"stage {stage}: {r}");
                Assert.Less(r.Burnt,r.Limit,"stage "+stage);
                Assert.LessOrEqual(r.Seconds,Level(stage).Par,$"stage {stage}: par leaves room for a three-star clear");
                Assert.Zero(r.SheepLost,"stage "+stage);
            }
            TestContext.WriteLine(sb.ToString());
        }

        [Test] public void HintSquaresAreLegalInPlanAndWithinSeatedReach()
        {
            for(int stage=0;stage<Stages;stage++)
            {
                var level=Level(stage);var run=EmberdeepLevelBot.Load(stage);string at="stage "+stage;
                int towers=run.Pieces.Count(p=>p.Kind==FirePieceKind.Tower),diggers=run.Pieces.Count(p=>p.Kind==FirePieceKind.Excavator);
                Assert.AreEqual(towers,level.TowerHints.Length,at+": a hint for every tower");
                Assert.AreEqual(diggers>0,level.DigHint.Length>0,at+": a dig line for the excavator");
                var why=new StringBuilder();
                Assert.True(EmberdeepLevelBot.PlaceHints(run,level,why),at+": "+why);
                Assert.AreEqual(run.Pieces.Length,run.Pieces.Count(p=>p.Placed),at+": every piece placed");
                foreach(var gh in level.TowerHints.Concat(level.DigHint))
                {
                    int square=level.Square(gh);Assert.True(run.ValidSquare(square),at);
                    Assert.LessOrEqual(ReachFromEye(run.Squares[square]),ReachMetres,$"{at}: square {gh} is out of seated reach");
                }
                // Every square of the board is on it and clear of trunks.
                foreach(var s in run.Squares)
                {
                    Assert.Less(Mathf.Abs(s.x),EmberdeepRimHud.DefaultBoard.extents.x,at);
                    foreach(var t in run.Trees)Assert.Greater(Flat(s,t.Position),.55f,at);
                }
                // The dig line is cut before the fire gets there: the excavator finishes its whole line.
                if(level.DigHint.Length>1)
                {
                    EmberdeepLevelBot.StartFight(run);
                    int digger=System.Array.FindIndex(run.Pieces,p=>p.Kind==FirePieceKind.Excavator);
                    for(int i=0;i<200 && !run.Pieces[digger].Finished && run.Phase==FirePhase.Fighting;i++)run.Tick(.05f,false);
                    Assert.AreEqual(level.DigHint.Length-1,run.Pieces[digger].Driven,at+": the trench is dug to its end before fire reaches it");
                }
            }
        }

        [Test] public void TheForecastNamesEveryCatchIdleAndInPlay()
        {
            for(int stage=0;stage<Stages;stage++)
            {
                // Plan: the first forecast catch is the first catch after the bell.
                var run=EmberdeepLevelBot.Load(stage);int next=run.ForecastCell(0);
                Assert.GreaterOrEqual(next,0,"stage "+stage);
                EmberdeepLevelBot.StartFight(run);int caught=-1;
                for(int i=0;i<2000 && caught<0 && run.Phase==FirePhase.Fighting;i++)
                {
                    var before=Enumerable.Range(0,run.CellCount).Select(c=>run.Cell(c).State).ToArray();run.Tick(.05f,false);
                    for(int c=0;c<before.Length;c++)if(before[c]==TreeState.Safe && run.Cell(c).State==TreeState.Burning){caught=c;break;}
                }
                Assert.AreEqual(next,caught,"stage "+stage+": Plan forecast");
                var idle=EmberdeepLevelBot.Idle(stage);
                Assert.Greater(idle.Catches,0,"stage "+stage);Assert.Zero(idle.ForecastMisses,"stage "+stage+" idle");
                var played=new EmberdeepLevelBot().Play(stage);Assert.Zero(played.ForecastMisses,"stage "+stage+" in play");
            }
        }

        // One new thing per level, and it matters on that board.
        [Test] public void EachLevelsNewThingMatters()
        {
            var sb=new StringBuilder();
            // Palm lesson: palm water and sheep. The passive refill is enough (the pump is the next level's lesson), and
            // a sheep is in danger if nobody acts.
            var palm=new EmberdeepLevelBot{Pump=false}.Play(0);sb.AppendLine("palm, no pump: "+palm);
            Assert.True(palm.Cleared,"the palm lesson doesn't need the pump: "+palm);
            Assert.True(SheepCatchFire(0),"a sheep catches fire if nobody acts");
            // Incident 0: the pump. The gem runs dry with fire still burning, and the passive refill alone does worse.
            var pump=new EmberdeepLevelBot().Play(1);var noPump=new EmberdeepLevelBot{Pump=false}.Play(1);
            sb.AppendLine("pump: "+pump).AppendLine("pump level, no pump: "+noPump);
            Assert.Greater(pump.TimesDry,0);Assert.Greater(pump.Pumped,0);
            Assert.True(Worse(noPump,pump),"without pumping it goes worse");
            // Incident 1: towers and Plan.
            var noTower=new EmberdeepLevelBot{UsePieces=false}.Play(2);var tower=new EmberdeepLevelBot().Play(2);
            sb.AppendLine("tower: "+tower).AppendLine("tower level, no tower: "+noTower);
            Assert.Greater(tower.TowersRained,0);Assert.True(Worse(noTower,tower),"without the tower it goes worse");
            // Incident 2: wind. The fire runs downwind, and the same player does better in a calm.
            Assert.Greater(DownwindShare(3),.7f,"most catches go downwind before the shift");
            var windy=new EmberdeepLevelBot().Play(3);
            var calmRun=EmberdeepLevelBot.Load(3);calmRun.WindOverride=new FireWindPlan(0,0);var calm=new EmberdeepLevelBot().Play(calmRun,Level(3));
            sb.AppendLine("wind: "+windy).AppendLine("wind level, calm: "+calm);
            Assert.True(Worse(windy,calm) || DownwindShare(3)>.9f,"the wind shapes the fire");
            // Incident 3: grass. The excavator and the plane were cut for the store build (owner 2026-11-22): no authored
            // level hands them out; their mechanics stay covered on the generated grids (EmberdeepPieceTests).
            var grass=new EmberdeepLevelBot().Play(4);var noTowers=new EmberdeepLevelBot{UsePieces=false}.Play(4);
            sb.AppendLine("grass: "+grass).AppendLine("grass level, no towers: "+noTowers);
            Assert.Greater(GrassCatches(4),3,"fire runs through the grass");
            Assert.True(Worse(noTowers,grass),"without the towers it goes worse");
            var last=new EmberdeepLevelBot().Play(5);sb.AppendLine("last: "+last);
            for(int stage=0;stage<Stages;stage++)
            {Assert.Zero(Level(stage).Pieces.Excavators,"stage "+stage+": excavator cut");Assert.False(Level(stage).Pieces.Plane,"stage "+stage+": plane cut");}
            TestContext.WriteLine(sb.ToString());
        }

        // The tuning table for the report (always passes; read it in the test output).
        [Test] public void TuningTable()
        {
            var sb=new StringBuilder();
            for(int stage=0;stage<Stages;stage++){sb.AppendLine("== stage "+stage);sb.Append(EmberdeepLevelBot.Report(stage));}
            TestContext.WriteLine(sb.ToString());Debug.Log("[EmberdeepLevels]\n"+sb);
        }

        // ---- helpers ------------------------------------------------------------------------------------------------
        static float Flat(Vector3 a,Vector3 b)=>Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z));
        // Default seated placement (FireMiniature.PlaceCompact): the front row EyeToFrontRow ahead of the eye, board
        // centred on the heading, Scale metres per unit.
        static float ReachFromEye(Vector3 board)=>new Vector2(board.x*FireMiniature.Scale,
            EmberdeepBoardLayout.EyeToFrontRow+(board.z-EmberdeepBoardLayout.FrontRowZ)*FireMiniature.Scale).magnitude;
        // a did worse than b.
        static bool Worse(EmberdeepLevelBot.Result a,EmberdeepLevelBot.Result b)
        {
            if(a.Cleared!=b.Cleared)return !a.Cleared;
            return a.Stars<b.Stars || a.Burnt>b.Burnt || a.SheepLost>b.SheepLost || a.CabinsSaved<b.CabinsSaved || a.Seconds>b.Seconds*1.1f;
        }
        static bool SheepCatchFire(int stage)
        {
            var run=EmberdeepLevelBot.Load(stage);EmberdeepLevelBot.StartFight(run);
            for(int i=0;i<1200 && run.Phase==FirePhase.Fighting;i++){run.Tick(.05f,false);if(run.AnimalsOnFire>0)return true;}
            return false;
        }
        static int GrassCatches(int stage)
        {
            var run=EmberdeepLevelBot.Load(stage);EmberdeepLevelBot.StartFight(run);int n=0;
            var before=new TreeState[run.CellCount];
            for(int i=0;i<1200 && run.Phase==FirePhase.Fighting;i++)
            {
                for(int c=0;c<before.Length;c++)before[c]=run.Cell(c).State;run.Tick(.05f,false);
                for(int c=run.Trees.Length;c<before.Length;c++)if(before[c]==TreeState.Safe && run.Cell(c).State==TreeState.Burning)n++;
            }
            return n;
        }
        // Share of idle catches (before any wind shift) that land downwind of the fire that was burning.
        static float DownwindShare(int stage)
        {
            var run=EmberdeepLevelBot.Load(stage);EmberdeepLevelBot.StartFight(run);int down=0,all=0;
            var before=new TreeState[run.CellCount];
            for(int i=0;i<2400 && run.Phase==FirePhase.Fighting && !(run.WindPlan.Shifts && run.Elapsed>=run.WindPlan.ShiftAt);i++)
            {
                var wind=run.WindDirection;
                for(int c=0;c<before.Length;c++)before[c]=run.Cell(c).State;run.Tick(.05f,false);
                for(int c=0;c<before.Length;c++)
                {
                    if(before[c]!=TreeState.Safe || run.Cell(c).State!=TreeState.Burning)continue;
                    // Its nearest burning neighbour before the catch is its source.
                    var p=run.Cell(c).Position;float best=float.MaxValue;Vector3 from=p;
                    for(int s=0;s<before.Length;s++)if(before[s]==TreeState.Burning && Flat(run.Cell(s).Position,p)<best){best=Flat(run.Cell(s).Position,p);from=run.Cell(s).Position;}
                    all++;if((p.x-from.x)*wind.x+(p.z-from.z)*wind.z>.1f)down++;
                }
            }
            return all==0?0:(float)down/all;
        }
    }
}

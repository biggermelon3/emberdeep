using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    // LoopSpec §5 pieces (workstream L3): per-level sets, squares, placement rules, the tower's auto-rain, the
    // excavator's dug line blocking spread, the plane's strip, its charge and the piece upgrades.
    public sealed class EmberdeepPieceTests
    {
        // The run at an incident, in Plan or already fighting. The rules run on the generated grids these tests were
        // written against (EmberdeepLevels.GeneratedGrid via FireRun.LevelOverride; the levels are hand-authored since
        // LV), so retuning a level never moves them. The authored sets are checked on the real levels below.
        static FireRun At(int incident,bool plan=true)
        {
            var run=new FireRun(true);int stage=incident+FireRun.PalmLessons;
            typeof(FireRun).GetProperty("Stage").SetValue(run,stage);run.LevelOverride=EmberdeepLevels.GeneratedGrid(stage);
            Assert.True(run.StartStage(plan));Assert.AreEqual(incident,run.IncidentIndex);return run;
        }
        static FireRun Authored(int incident)
        {
            var run=new FireRun(true);typeof(FireRun).GetProperty("Stage").SetValue(run,incident+FireRun.PalmLessons);
            Assert.True(run.StartStage(true));return run;
        }
        static void Fight(FireRun run){if(run.Phase==FirePhase.Preparing){run.BeginCountdown();for(int i=0;i<31;i++)run.TickPreparation(.1f);}Assert.AreEqual(FirePhase.Fighting,run.Phase);}
        static void Coins(FireRun run,int coins)=>typeof(FireRun).GetProperty("Coins").SetValue(run,coins);
        static IEnumerable<FireRun.Tree> Fuel(FireRun run){for(int c=0;c<run.CellCount;c++)yield return run.Cell(c);}
        static void Calm(FireRun run,float degrees=0,float strength=0)=>run.WindOverride=new FireWindPlan(degrees,strength);
        // Every fuel to Saved (never catches again), so a test lights exactly what it needs.
        static void Quench(FireRun run){foreach(var f in Fuel(run)){f.State=TreeState.Saved;f.Heat=0;f.Wetness=0;}foreach(var a in run.Animals)a.OnFire=false;run.InvalidateForecast();}
        static int Square(FireRun run,int column,int row)=>row*run.SquareColumns+column;
        // One tree kept alight so the fight doesn't end as Cleared while a test has quenched the rest.
        static FireRun.Tree KeepAlive(FireRun run,int column,int row){var t=run.Trees[row*run.FieldColumns+column];t.State=TreeState.Burning;t.Heat=100;t.Fuel=100;run.InvalidateForecast();return t;}
        static int TreeAt(FireRun run,int column,int row)=>row*run.FieldColumns+column;
        static float Flat(Vector3 a,Vector3 b)=>new Vector2(a.x-b.x,a.z-b.z).magnitude;

        [Test] public void EachIncidentHandsOutItsPieceSetAndOnlyCabinsArePreplaced()
        {
            for(int incident=0;incident<5;incident++)
            {
                var run=Authored(incident);var set=FireRun.PieceSets[incident];
                Assert.AreEqual(set.Towers,run.Pieces.Count(p=>p.Kind==FirePieceKind.Tower),$"incident {incident} towers");
                Assert.AreEqual(set.Excavators,run.Pieces.Count(p=>p.Kind==FirePieceKind.Excavator),$"incident {incident} excavators");
                Assert.AreEqual(set.Plane,run.PlaneEarnable);
                Assert.True(run.Pieces.All(p=>!p.Placed),"Everything starts in the tray");
                Assert.False(run.Props.Any(p=>p.Kind==FirePropKind.Reservoir),"No pre-placed reservoirs in the board flow");
            }
            // The authored sets (EmberdeepLevels): pump level none; towers from incident 1. The excavator and the plane were
            // cut for the store build (owner 2026-11-22): towers only. FireRun.PieceSets is the levels' copy.
            for(int incident=0;incident<5;incident++)
                Assert.AreEqual(EmberdeepLevels.For(incident+FireRun.PalmLessons).Pieces,FireRun.PieceSets[incident],"incident "+incident);
            Assert.AreEqual(0,FireRun.PieceSets[0].Towers+FireRun.PieceSets[0].Excavators);Assert.False(FireRun.PieceSets[0].Plane);
            Assert.Greater(FireRun.PieceSets[1].Towers,0);Assert.Zero(FireRun.PieceSets[1].Excavators+FireRun.PieceSets[2].Excavators);
            Assert.True(FireRun.PieceSets.All(s=>s.Excavators==0 && !s.Plane),"excavator and plane cut");
            // Palm lessons share incident 0 but never get pieces; the legacy flow keeps its towers.
            var palm=new FireRun(true);palm.StartStage(true);Assert.True(palm.IsPalmLesson);Assert.IsEmpty(palm.Pieces);Assert.False(palm.HasPieces);
            var legacy=new FireRun();legacy.StartStage();legacy.ApplyWater(Vector3.zero,10000,100);legacy.StartStage();
            Assert.IsEmpty(legacy.Pieces);Assert.True(legacy.Props.Any(p=>p.Kind==FirePropKind.Reservoir));
        }

        [Test] public void SquaresAreTheGapsBetweenTreesPlusASideAndBackRing()
        {
            var run=At(3);int columns=run.FieldColumns,rows=run.Trees.Length/columns;
            Assert.AreEqual(columns+1,run.SquareColumns);Assert.AreEqual(rows,run.SquareRows);
            for(int h=0;h<rows-1;h++)for(int g=1;g<columns;g++)
            {
                // An interior square sits in the middle of its four trees.
                var s=run.Squares[Square(run,g,h)];
                float[] d={Flat(s,run.Trees[TreeAt(run,g-1,h)].Position),Flat(s,run.Trees[TreeAt(run,g,h)].Position),
                    Flat(s,run.Trees[TreeAt(run,g-1,h+1)].Position),Flat(s,run.Trees[TreeAt(run,g,h+1)].Position)};
                Assert.AreEqual(d.Max(),d.Min(),.02f);
            }
            foreach(var s in run.Squares)
            {
                Assert.Greater(s.z,EmberdeepBoardLayout.FrontRowZ,"No squares in front of the field (creek, refuge)");
                Assert.Less(Mathf.Abs(s.x),EmberdeepRimHud.DefaultBoard.extents.x,"On the board");
                foreach(var t in run.Trees)Assert.Greater(Flat(s,t.Position),.55f,"Never on a trunk");
            }
            // Grass grows on squares.
            foreach(var g in run.Grass)Assert.Less(run.Squares.Min(s=>Flat(s,g.Position)),.01f);
            Assert.True(run.Adjacent(Square(run,2,0),Square(run,3,1)));Assert.False(run.Adjacent(Square(run,2,0),Square(run,4,0)));
        }

        [Test] public void PiecesGoOnFreeSquaresNotBurningOnesAndOnlyLeftoversMoveInTheFight()
        {
            var run=At(3);Calm(run);
            int tower=0,other=1,excavator=2;
            Assert.AreEqual(FirePieceKind.Excavator,run.Pieces[excavator].Kind);
            int free=Enumerable.Range(0,run.Squares.Length).First(s=>!run.SquareBurning(s) && !run.SquareTaken(s));
            Assert.AreEqual(FirePlaceBlock.None,run.CanPlace(tower,free));Assert.True(run.PlacePiece(tower,free));
            Assert.AreEqual(FirePlaceBlock.Occupied,run.CanPlace(other,free),"One piece per square");
            Assert.AreEqual(FirePlaceBlock.NoSquare,run.CanPlace(other,-1));Assert.AreEqual(FirePlaceBlock.NoSquare,run.CanPlace(other,run.Squares.Length));
            // A burning square: its own grass alight.
            var patch=run.Grass.First();int grassSquare=run.NearestSquare(patch.Position);
            patch.State=TreeState.Burning;patch.Heat=10;
            Assert.True(run.SquareBurning(grassSquare));Assert.AreEqual(FirePlaceBlock.Burning,run.CanPlace(other,grassSquare));
            patch.State=TreeState.Safe;patch.Heat=0;
            // The cabin's square is taken.
            var cabin=run.Props.First(p=>p.Kind==FirePropKind.Cabin);int cabinSquare=run.NearestSquare(cabin.Position,2);
            if(Flat(run.Squares[cabinSquare],cabin.Position)<EmberdeepPieceTuning.CabinKeepOut)Assert.AreEqual(FirePlaceBlock.Occupied,run.CanPlace(other,cabinSquare));
            // An excavator's planned line is taken too.
            int start=Enumerable.Range(0,run.Squares.Length).First(s=>s!=free && run.CanPlace(excavator,s)==FirePlaceBlock.None
                && Enumerable.Range(0,run.Squares.Length).Any(n=>run.Adjacent(s,n) && n!=free && run.CanPlace(other,n)==FirePlaceBlock.None));
            Assert.True(run.PlacePiece(excavator,start));
            int next=Enumerable.Range(0,run.Squares.Length).First(n=>run.Adjacent(start,n) && n!=free && run.CanPlace(other,n)==FirePlaceBlock.None);
            Assert.True(run.StartLine(excavator));Assert.True(run.ExtendLine(excavator,next));
            Assert.AreEqual(FirePlaceBlock.Occupied,run.CanPlace(other,next));
            // Plan: a placed piece can be picked back up and moved.
            Assert.True(run.CanLift(tower));Assert.True(run.LiftPiece(tower));Assert.False(run.Pieces[tower].Placed);Assert.True(run.PlacePiece(tower,free));
            // Fight: the leftover still goes down, a placed one stays put, nothing is lifted.
            Fight(run);
            int spare=Enumerable.Range(0,run.Squares.Length).First(s=>run.CanPlace(other,s)==FirePlaceBlock.None);
            Assert.True(run.PlacePiece(other,spare),"Leftovers are placeable during the fight");
            Assert.AreEqual(FirePlaceBlock.Spent,run.CanPlace(tower,spare==0?1:0));Assert.False(run.CanLift(tower));Assert.False(run.LiftPiece(tower));
            // Not between levels.
            run.ApplyWater(Vector3.zero,10000,100);Assert.AreNotEqual(FirePhase.Fighting,run.Phase);
            Assert.AreEqual(FirePlaceBlock.Phase,run.CanPlace(other,free));
            // The plane is fight-only and only when earned.
            var inferno=At(4);Assert.True(inferno.PlaneEarnable);
            inferno.ChargePlaneForTest();Assert.False(inferno.PlaneReady,"Not in Plan");Assert.AreEqual(FirePlaceBlock.Phase,inferno.CanCallPlane(0));
            Fight(inferno);Assert.True(inferno.PlaneReady);Assert.AreEqual(FirePlaceBlock.None,inferno.CanCallPlane(0));
            var grove=At(2);grove.ChargePlaneForTest();Fight(grove);Assert.False(grove.PlaneReady,"Not earnable on this level");
        }

        [Test] public void TowerArrivesFullAndRainsOnceWhenFireComesWithinItsRadius()
        {
            var run=At(2);Calm(run);int tower=0;
            int square=Square(run,run.SquareColumns-1,run.SquareRows-1); // back corner
            Quench(run);Assert.True(run.PlacePiece(tower,square));var p=run.Pieces[tower];
            var rain=run.RainPoint(p);Assert.AreEqual(EmberdeepPieceTuning.TowerRadius,run.TowerRadius);
            // Time is stopped in Plan: nothing rains, even with fire next to it.
            var near=run.Trees.Where(t=>Vector3.Distance(t.Position,rain)<run.TowerRadius-.2f).OrderByDescending(t=>Vector3.Distance(t.Position,rain)).First();
            var far=run.Trees.Where(t=>Vector3.Distance(t.Position,rain)>run.TowerRadius+.05f).OrderBy(t=>Vector3.Distance(t.Position,rain)).First();
            far.State=TreeState.Burning;far.Heat=100;run.InvalidateForecast();
            Fight(run);
            for(int i=0;i<10;i++)run.Tick(.1f,false);
            Assert.False(p.Used,"Fire outside the radius doesn't set it off");Assert.False(p.Raining);
            int rained=0;run.TowerRained+=_=>rained++;
            near.State=TreeState.Burning;near.Heat=100;run.InvalidateForecast();
            float t0=run.Elapsed;run.Tick(.1f,false);
            Assert.True(p.Raining,"Fire within the radius: it rains on its own");Assert.True(p.Used);Assert.AreEqual(1,rained);
            // It arrived full: 4 s of rain, enough to put that tree out and soak it.
            for(int i=0;i<60 && p.Raining;i++)run.Tick(.1f,false);
            Assert.False(p.Raining);Assert.AreEqual(EmberdeepPieceTuning.TowerSeconds,run.Elapsed-t0,.15f);
            Assert.AreEqual(TreeState.Saved,near.State);Assert.Greater(near.Wetness,1);
            Assert.AreEqual(TreeState.Burning,far.State,"Outside the radius stays alight");Assert.AreEqual(100,far.Heat,.01f);
            // Once only.
            var again=run.Trees.First(t=>t!=near && t.State==TreeState.Saved && Vector3.Distance(t.Position,rain)<run.TowerRadius);
            again.State=TreeState.Burning;again.Heat=100;run.Tick(.1f,false);
            Assert.False(p.Raining);Assert.AreEqual(1,rained);
            // A tower placed in the fight next to fire goes off at once.
            var fresh=At(2);Calm(fresh);Fight(fresh);
            int hot=Enumerable.Range(0,fresh.Squares.Length).First(s=>fresh.CanPlace(1,s)==FirePlaceBlock.None
                && fresh.Trees.Any(t=>t.State==TreeState.Burning && Vector3.Distance(t.Position,new Vector3(fresh.Squares[s].x,.7f,fresh.Squares[s].z))<2));
            Assert.True(fresh.PlacePiece(1,hot));fresh.Tick(.05f,false);Assert.True(fresh.Pieces[1].Raining);
        }

        [Test] public void TowerRadiusUpgradeWidensWhatSetsItOffAndWhatItRainsOn()
        {
            var run=At(2);Coins(run,1000);
            Assert.True(run.CanUpgradeTower);Assert.True(run.BuyTowerUpgrade());Assert.AreEqual(1000-EmberdeepPieceTuning.TowerPrices[0],run.Coins);
            Assert.AreEqual(EmberdeepPieceTuning.TowerRadius+EmberdeepPieceTuning.TowerRadiusPerLevel,run.TowerRadius,1e-4f);
            Calm(run);Quench(run);int square=Square(run,run.SquareColumns-1,run.SquareRows-1);Assert.True(run.PlacePiece(0,square));
            var rain=run.RainPoint(run.Pieces[0]);
            var edge=run.Trees.First(t=>{float d=Vector3.Distance(t.Position,rain);return d>EmberdeepPieceTuning.TowerRadius+.05f && d<run.TowerRadius-.05f;});
            edge.State=TreeState.Burning;edge.Heat=100;Fight(run);run.Tick(.1f,false);
            Assert.True(run.Pieces[0].Raining,"Only the upgraded radius reaches it");
            Assert.False(run.BuyTowerUpgrade(),"Not mid-fight");
            var shop=At(2);Coins(shop,1000);
            for(int i=0;i<4;i++)shop.BuyTowerUpgrade();Assert.AreEqual(shop.TowerLevels,shop.TowerLevel);Assert.Zero(shop.TowerUpgradeCost);Assert.False(shop.CanUpgradeTower);
        }

        [Test] public void SlotUpgradeAddsATowerToEveryLevelWithPieces()
        {
            var run=At(1);Coins(run,1000);int before=run.Pieces.Length;
            Assert.True(run.BuySlotUpgrade());Assert.AreEqual(before+1,run.Pieces.Length,"Shows up in the tray being planned");
            Assert.AreEqual(FirePieceKind.Tower,run.Pieces[before].Kind);
            Assert.True(run.RetryStage(true));Assert.AreEqual(before+1,run.Pieces.Length,"And on the next try");
            Assert.True(run.BuySlotUpgrade());Assert.False(run.CanUpgradeSlot,"Two slots at most");Assert.AreEqual(before+2,run.Pieces.Length);
            // Towers stay ahead of the excavator in the tray.
            var front=At(3);Coins(front,1000);front.BuySlotUpgrade();
            Assert.AreEqual(new[]{FirePieceKind.Tower,FirePieceKind.Tower,FirePieceKind.Tower,FirePieceKind.Excavator},front.Pieces.Select(p=>p.Kind).ToArray());
            // A level without pieces stays without.
            var first=At(0);Coins(first,1000);first.BuySlotUpgrade();Assert.IsEmpty(first.Pieces);
            first.Restart();Assert.Zero(first.SlotLevel);Assert.Zero(first.TowerLevel);
        }

        // The core rule: a catch never crosses a dug line, even where wind's 2.2 downwind reach would jump the gap.
        [Test] public void SpreadNeverCrossesADugLineEvenDownwind()
        {
            foreach(bool dig in new[]{true,false})
            {
                var run=At(3);Calm(run,90,1); // downwind to the right, full strength: 2.2 downwind reach
                int excavator=System.Array.FindIndex(run.Pieces,p=>p.Kind==FirePieceKind.Excavator);
                // A line down the gap between tree columns 1 and 2, the whole depth of the field.
                int g=2,top=Square(run,g,0);float cut=run.Squares[top].x;
                if(dig)
                {
                    Assert.True(run.PlacePiece(excavator,top));
                    Assert.True(run.StartLine(excavator));Assert.AreEqual(2,run.TraceLine(excavator,Square(run,g,2)),"Traced square by square");
                    Assert.True(run.FinishLine(excavator));
                }
                // Dig it with no fire anywhere, then light one tree just left of it.
                Quench(run);foreach(var f in Fuel(run))if(f.Position.x>cut){f.State=TreeState.Safe;f.Wetness=0;}
                var alive=KeepAlive(run,0,2); // 2.36+ from anything across: out of reach
                Fight(run);
                for(int i=0;i<40;i++)run.Tick(.1f,false);
                if(dig){Assert.True(run.Pieces[excavator].Finished);Assert.AreEqual(3,run.DugCount,"Its own square plus two segments");}
                var source=run.Trees[TreeAt(run,1,1)];source.State=TreeState.Burning;source.Heat=100;run.InvalidateForecast();
                // The forecast is honest about the cut too.
                int forecast=run.ForecastCell(0);
                if(dig)Assert.True(forecast<0 || run.Cell(forecast).Position.x<cut,"The forecast never crosses the line");
                for(int i=0;i<300 && run.Phase==FirePhase.Fighting;i++)
                {
                    run.Tick(.1f,false);
                    source.Heat=100;source.Fuel=100;alive.Fuel=100; // keep the fires burning
                }
                int crossed=Fuel(run).Count(f=>f.Position.x>cut && f.State==TreeState.Burning);
                if(dig)Assert.Zero(crossed,"Nothing caught across the dug line in 30 s of full downwind");
                else Assert.Greater(crossed,0,"Control: without the line the fire jumps the gap");
            }
            // The segment test itself: across is blocked, beside (parallel, .59 away) is not.
            var check=At(3);int exc=System.Array.FindIndex(check.Pieces,p=>p.Kind==FirePieceKind.Excavator);
            check.PlacePiece(exc,Square(check,2,0));check.StartLine(exc);check.TraceLine(exc,Square(check,2,2));check.FinishLine(exc);
            Calm(check);Quench(check);KeepAlive(check,7,2);Fight(check);for(int i=0;i<30;i++)check.Tick(.1f,false);
            Assert.True(check.SpreadBlocked(check.Trees[TreeAt(check,1,1)].Position,check.Trees[TreeAt(check,2,1)].Position));
            Assert.True(check.SpreadBlocked(check.Trees[TreeAt(check,1,1)].Position,check.Trees[TreeAt(check,2,2)].Position));
            Assert.False(check.SpreadBlocked(check.Trees[TreeAt(check,1,0)].Position,check.Trees[TreeAt(check,1,1)].Position),"Beside the line, not across it");
        }

        [Test] public void ExcavatorDrivesAboutASquareASecondClearsWhatItDigsAndStopsAtFire()
        {
            var run=At(3);Calm(run);Quench(run);KeepAlive(run,7,2);
            int excavator=System.Array.FindIndex(run.Pieces,p=>p.Kind==FirePieceKind.Excavator);var p=run.Pieces[excavator];
            // A diagonal square (2,0) -> (3,1) runs over tree (2,1): dug, that tree is cleared (dirt, not charred).
            // Square (4,1) holds a grass patch (the fire test below).
            int a=Square(run,2,0),b=Square(run,3,1),c=Square(run,4,1);
            var corner=run.Trees[TreeAt(run,2,1)];corner.State=TreeState.Safe;
            Assert.True(run.PlacePiece(excavator,a));Assert.True(run.StartLine(excavator));
            Assert.False(run.ExtendLine(excavator,Square(run,4,0)),"Not adjacent");
            Assert.True(run.ExtendLine(excavator,b));Assert.True(run.ExtendLine(excavator,c));
            Assert.AreEqual(1,run.TraceLine(excavator,Square(run,5,1)));
            run.TraceLine(excavator,c);Assert.AreEqual(2,p.Line.Count,"Back onto the previous square takes the last one off");
            Assert.True(run.FinishLine(excavator));Assert.False(run.ExtendLine(excavator,Square(run,5,1)),"Finished lines are fixed");
            // Time stopped: it waits for the fight.
            int burnt=run.Burnt;
            Fight(run);run.Tick(.05f,false);Assert.True(p.Driving);Assert.AreEqual(0,p.Driven);Assert.True(run.IsDug(a));
            for(int i=0;i<20;i++)run.Tick(.05f,false); // ~1.05 s
            Assert.AreEqual(1,p.Driven);Assert.AreEqual(TreeState.Cleared,corner.State,"The tree under its path is cleared");
            Assert.AreEqual(burnt,run.Burnt,"Cleared trees never count as charred");
            // Fire on the next square: it stops there for good.
            var ahead=run.Grass.First(gr=>Flat(gr.Position,run.Squares[c])<.1f);
            ahead.State=TreeState.Burning;ahead.Heat=50;
            for(int i=0;i<40;i++)run.Tick(.05f,false);
            Assert.True(p.Finished);Assert.AreEqual(1,p.Driven,"Stopped short of the fire");Assert.False(run.IsDug(c));
            Assert.AreEqual(run.Squares[b],run.ExcavatorPosition(excavator));
            Assert.AreEqual(EmberdeepPieceTuning.ExcavatorMaxSquares,4);
        }

        [Test] public void PlaneDropsWaterOnItsStripOnly()
        {
            var run=At(4);Calm(run);Fight(run);
            Assert.False(run.PlaneReady);Assert.False(run.CallPlane(0,90));
            run.ChargePlaneForTest();Assert.True(run.PlaneReady);
            // Every tree alight; the strip runs across the board (yaw 90 = along +x) through a middle square.
            foreach(var t in run.Trees){t.State=TreeState.Burning;t.Heat=100;t.Fuel=100;t.Wetness=0;}
            int square=Square(run,run.SquareColumns/2,1);var marker=run.Squares[square];
            Assert.True(run.CallPlane(square,90));Assert.Zero(run.PlaneCharge01,"The charge is spent");Assert.False(run.PlaneReady);
            Assert.AreEqual(FireRun.PlaneStage.Marked,run.PlaneState);
            bool InStrip(Vector3 p)=>Mathf.Abs(p.z-marker.z)<=EmberdeepPieceTuning.PlaneHalfWidth;
            var inside=run.Trees.Where(t=>InStrip(t.Position)).ToArray();var outside=run.Trees.Where(t=>!InStrip(t.Position)).ToArray();
            Assert.IsNotEmpty(inside);Assert.IsNotEmpty(outside);
            int arrived=0;run.PlaneArrived+=()=>arrived++;
            // About 3 s later it flies the line.
            for(int i=0;i<29;i++){run.Tick(.1f,false);foreach(var t in run.Trees)t.Fuel=100;}
            Assert.AreEqual(FireRun.PlaneStage.Marked,run.PlaneState);Assert.True(inside.All(t=>t.State==TreeState.Burning),"Nothing before it arrives");
            for(int i=0;i<60 && run.PlaneState!=FireRun.PlaneStage.None && run.Phase==FirePhase.Fighting;i++){run.Tick(.1f,false);foreach(var t in outside)t.Fuel=100;}
            Assert.AreEqual(1,arrived);Assert.AreEqual(FireRun.PlaneStage.None,run.PlaneState);
            foreach(var t in inside){Assert.AreEqual(TreeState.Saved,t.State,"Under the strip: out");Assert.Greater(t.Wetness,2,"and soaked");}
            foreach(var t in outside){Assert.AreEqual(TreeState.Burning,t.State,"Off the strip: untouched");Assert.Less(t.Wetness,.01f);}
            // It can be earned again.
            run.ChargePlaneForTest();Assert.True(run.PlaneReady);
        }

        [Test] public void PlaneChargesFromSheepSavedAndChains()
        {
            var run=At(4);Calm(run);Fight(run);Quench(run);KeepAlive(run,6,3);
            Assert.Zero(run.PlaneCharge01);
            // A chain of three trees put out.
            var three=run.Trees.Take(3).ToArray();foreach(var t in three){t.State=TreeState.Burning;t.Heat=10;}
            foreach(var t in three)run.ApplyWater(t.Position,100,.05f);
            Assert.AreEqual(3,run.Combo);run.Tick(.05f,false);
            Assert.AreEqual(EmberdeepPieceTuning.PlanePerCombo,run.PlaneCharge01,1e-4f);
            run.Animals[0].State=AnimalState.Rescued;run.Tick(.05f,false);
            Assert.AreEqual(EmberdeepPieceTuning.PlanePerCombo+EmberdeepPieceTuning.PlanePerSheep,run.PlaneCharge01,1e-4f);
            // No charge on levels without the plane.
            var grove=At(2);Fight(grove);grove.Animals[0].State=AnimalState.Rescued;grove.Tick(.05f,false);Assert.Zero(grove.PlaneCharge01);
        }

        // ---- Grasp (headset test 3: pinch or grab near the visible box) --------------------------------------------
        // A tray tower's visible box (world m): 4.9 cm wide, 11.4 cm tall, standing on the origin.
        static readonly EmberdeepPieceGrasp.Candidate Tower=new EmberdeepPieceGrasp.Candidate(7,new Vector3(0,.057f,0),Quaternion.identity,new Vector3(.0245f,.057f,.0245f));
        static List<EmberdeepPieceGrasp.Candidate> One=>new List<EmberdeepPieceGrasp.Candidate>{Tower};
        // A hand: palm centre, closure, then fingertips thumb..pinky (null = not tracked); pinch from the two tips.
        static EmberdeepPieceGrasp.Hand H(Vector3 palm,float closure,Vector3? thumb=null,Vector3? index=null,Vector3? middle=null,Vector3? ring=null,Vector3? pinky=null)
        {
            var h=new EmberdeepPieceGrasp.Hand{Valid=true,Palm=palm,Closure=closure};
            var tips=new[]{thumb,index,middle,ring,pinky};for(int f=0;f<5;f++)if(tips[f].HasValue)h.SetTip(f,tips[f].Value);
            h.PinchFromTips();return h;
        }
        // Thumb and index either side of a point, the palm 8 cm behind and above it, middle finger loosely in.
        static EmberdeepPieceGrasp.Hand Pinching(Vector3 at,float aperture,float closure=.25f)
        {
            var palm=at+new Vector3(0,.03f,-.08f);
            return H(palm,closure,at+Vector3.left*aperture*.5f,at+Vector3.right*aperture*.5f,palm+new Vector3(.015f,-.025f,.06f));
        }
        static readonly EmberdeepPieceGrasp.Hand None=new EmberdeepPieceGrasp.Hand();

        [Test] public void GraspBoxDistanceIsSignedAndFollowsTheBoxTurn()
        {
            var box=new EmberdeepPieceGrasp.Candidate(1,Vector3.zero,Quaternion.Euler(0,45,0),new Vector3(.1f,.1f,.1f));
            Assert.AreEqual(-.1f,box.Distance(Vector3.zero),1e-5f,"Centre: as deep as the half size");
            Assert.AreEqual(.05f,box.Distance(Vector3.up*.15f),1e-5f,"Above the top");
            // Turned 45 degrees: the corner points along +x, .1*sqrt2 out.
            Assert.AreEqual(.2f-.1f*Mathf.Sqrt(2),box.Distance(Vector3.right*.2f),1e-4f);
            Assert.AreEqual(0,box.Distance(box.Closest(new Vector3(.3f,.2f,-.1f))),1e-5f,"Closest lies on the box");
            Assert.AreEqual(.02f,new EmberdeepPieceGrasp.Candidate(2,Vector3.zero).Distance(Vector3.up*.02f),1e-6f,"A bare point");
        }

        [Test] public void PinchAtTheTopOfATowerTakesItAndPartingTheTipsLetsGo()
        {
            var grasp=new EmberdeepPieceGrasp();var top=new Vector3(0,.105f,0); // the tank, just under the cap
            grasp.Step(Pinching(top,.05f),None,One,.02f);
            Assert.AreEqual(7,grasp.Hover(0),"An open pinch around the tank hovers it");Assert.False(grasp.OwnsHand(0));
            grasp.Step(Pinching(top,.008f),None,One,.02f);
            Assert.AreEqual(7,grasp.Grabbed[0],"The tips meeting take it at once");Assert.AreEqual(EmberdeepPieceGrasp.Mode.Pinch,grasp.HeldMode(0));
            Assert.AreEqual(top.x,grasp.GrabAnchor(0).x,1e-4f);Assert.AreEqual(-1,grasp.Hover(0),"A carrying hand hovers nothing");
            // It follows the pinch point, and a fist closing around it meanwhile keeps it.
            var moved=top+new Vector3(.1f,.05f,0);grasp.Step(Pinching(moved,.01f),None,One,.02f);
            Assert.AreEqual(moved,grasp.Anchor(0));
            grasp.Step(Pinching(moved,.06f,.8f),None,One,.02f);Assert.True(grasp.OwnsHand(0),"Tips apart inside a fist: still held");
            grasp.Step(Pinching(moved,.06f,.25f),None,One,.02f);Assert.AreEqual(7,grasp.Released[0],"Tips parted: let go");
            // A pinch a little above the cap (within 3 cm of the box) still takes it.
            grasp.Reset();var over=new Vector3(0,.114f+.025f,0);
            grasp.Step(Pinching(over,.05f),None,One,.02f);grasp.Step(Pinching(over,.01f),None,One,.02f);Assert.True(grasp.OwnsHand(0),"2.5 cm over the cap");
        }

        [Test] public void ClosingTheHandBesideATowerGrabsItAndOpeningLetsGo()
        {
            var grasp=new EmberdeepPieceGrasp();
            // Palm 4.6 cm from the tower's side, fingers toward it (tips 1-2 cm off), thumb and index well apart (no pinch).
            var palm=new Vector3(-.07f,.06f,0);
            EmberdeepPieceGrasp.Hand Side(float closure)=>H(palm,closure,palm+new Vector3(.03f,.03f,.03f),palm+new Vector3(.035f,0,-.015f),
                palm+new Vector3(.034f,-.01f,0),palm+new Vector3(.032f,-.02f,.01f),palm+new Vector3(.028f,-.03f,.015f));
            grasp.Step(Side(.1f),None,One,.02f);grasp.Step(Side(.1f),None,One,.02f);
            Assert.AreEqual(7,grasp.Hover(0),"An open hand beside it hovers it");
            grasp.Step(Side(.6f),None,One,.02f);Assert.False(grasp.OwnsHand(0),"A short dwell first");
            for(int i=0;i<3 && !grasp.OwnsHand(0);i++)grasp.Step(Side(.6f),None,One,.02f);
            Assert.AreEqual(EmberdeepPieceGrasp.Mode.Grab,grasp.HeldMode(0));Assert.AreEqual(palm,grasp.Anchor(0));
            grasp.Step(Side(.3f),None,One,.02f);Assert.True(grasp.OwnsHand(0),"Half open: still held");
            grasp.Step(Side(.1f),None,One,.02f);Assert.AreEqual(7,grasp.Released[0],"Open: let go");
            // Palm only (no fingertips tracked): within 5 cm of the box, as before.
            grasp.Reset();var bare=new Vector3(-.07f,.06f,0);
            grasp.Step(H(bare,.1f),None,One,.02f);for(int i=0;i<5;i++)grasp.Step(H(bare,.7f),None,One,.02f);Assert.True(grasp.OwnsHand(0),"Palm 4.55 cm off");
            // Lost tracking drops it; a blocked hand (holding a sheep, casting) never grabs.
            for(int i=0;i<20;i++)grasp.Step(None,None,One,.02f);Assert.False(grasp.OwnsHand(0));
            grasp.Reset();grasp.Step(None,H(bare,.1f),One,.02f,false,true);for(int i=0;i<5;i++)grasp.Step(None,H(bare,.7f),One,.02f,false,true);
            Assert.False(grasp.OwnsHand(1));Assert.AreEqual(-1,grasp.Hover(1));
        }

        [Test] public void NearMissesFistsPointsAndSpentPinchesNeverTakeAPiece()
        {
            var grasp=new EmberdeepPieceGrasp();
            // Pinch closing 4.5 cm off the tank's side.
            var beside=new Vector3(-.0245f-.045f,.1f,0);
            grasp.Step(Pinching(beside,.05f),None,One,.02f);Assert.AreEqual(-1,grasp.Hover(0));
            grasp.Step(Pinching(beside,.008f),None,One,.02f);Assert.False(grasp.OwnsHand(0),"Pinch 4.5 cm off");
            // Closing hand whose nearest point is 6 cm off.
            grasp.Reset();var palm=new Vector3(-.0245f-.1f,.06f,0);
            EmberdeepPieceGrasp.Hand Off(float c)=>H(palm,c,palm+new Vector3(.02f,.03f,.04f),palm+new Vector3(.04f,0,0),palm+new Vector3(.035f,-.01f,0));
            grasp.Step(Off(.1f),None,One,.02f);for(int i=0;i<6;i++)grasp.Step(Off(.7f),None,One,.02f);Assert.False(grasp.OwnsHand(0),"Grab 6 cm off");
            // A fist that arrives closed (it never opened near a piece) never grabs, even pushed inside.
            grasp.Reset();grasp.Step(H(new Vector3(.3f,.06f,0),.1f),None,One,.02f);
            for(int i=0;i<6;i++)grasp.Step(H(new Vector3(.3f-i*.06f,.06f,0),.9f),None,One,.02f);
            for(int i=0;i<6;i++)grasp.Step(H(Vector3.up*.06f,.9f),None,One,.02f);Assert.False(grasp.OwnsHand(0),"Closed fist arriving");
            // A pointing hand (the excavator's draw): opened near, then the index out and the rest curled.
            grasp.Reset();var p=new Vector3(-.13f,.08f,0);
            EmberdeepPieceGrasp.Hand Point(float c,bool open)=>H(p,c,p+new Vector3(.03f,-.01f,.03f),p+new Vector3(.1f,0,0),open?p+new Vector3(.1f,-.01f,.01f):p+new Vector3(.03f,-.03f,0));
            grasp.Step(Point(.1f,true),None,One,.02f);
            Assert.True(Point(.6f,false).Pointing);
            for(int i=0;i<6;i++)grasp.Step(Point(.6f,false),None,One,.02f);Assert.False(grasp.OwnsHand(0),"A point is not a grab");
            Assert.AreEqual(-1,grasp.Hover(0),"Nor does it hover");
            // A pinch that closed in the air is spent: moved in closed, it takes nothing until it opens again.
            grasp.Reset();var air=new Vector3(0,.3f,0);
            grasp.Step(Pinching(air,.05f),None,One,.02f);grasp.Step(Pinching(air,.008f),None,One,.02f);
            for(int i=0;i<4;i++)grasp.Step(Pinching(new Vector3(0,.105f,0),.008f),None,One,.02f);Assert.False(grasp.OwnsHand(0),"Spent pinch");
            grasp.Step(Pinching(new Vector3(0,.105f,0),.05f),None,One,.02f);grasp.Step(Pinching(new Vector3(0,.105f,0),.008f),None,One,.02f);
            Assert.True(grasp.OwnsHand(0),"Opened and pinched again: taken");
        }

        [Test] public void TwoPiecesCloseTogetherGoToTheNearerOneAtATime()
        {
            // Two tray towers one slot apart (8 cm centres, 3.1 cm between the boxes).
            var b=new EmberdeepPieceGrasp.Candidate(8,new Vector3(.08f,.057f,0),Quaternion.identity,Tower.Extents);
            var both=new List<EmberdeepPieceGrasp.Candidate>{Tower,b};var grasp=new EmberdeepPieceGrasp();
            // Pinch point 0.6 cm off the first, 2.5 cm off the second: the first.
            var at=new Vector3(.031f,.1f,0);
            grasp.Step(Pinching(at,.05f),None,both,.02f);Assert.AreEqual(7,grasp.Hover(0));
            grasp.Step(Pinching(at,.008f),None,both,.02f);Assert.AreEqual(7,grasp.Held(0));
            // Inside one box and near the other: the one it is inside.
            var other=new EmberdeepPieceGrasp();var inB=new Vector3(.07f,.1f,0);
            other.Step(Pinching(inB,.05f),None,both,.02f);Assert.AreEqual(8,other.Hover(0));
            // Holding the first, the same hand closing again on the second takes nothing more.
            grasp.Step(Pinching(b.Center,.06f,.8f),None,both,.02f);grasp.Step(Pinching(b.Center,.008f,.8f),None,both,.02f);
            Assert.AreEqual(7,grasp.Held(0));Assert.AreEqual(-1,grasp.Grabbed[0]);
            // The other hand can't take the held one; it gets the free one.
            var heldNow=new EmberdeepPieceGrasp.Candidate(7,b.Center,Quaternion.identity,Tower.Extents);
            var list=new List<EmberdeepPieceGrasp.Candidate>{heldNow,b};
            grasp.Step(Pinching(b.Center,.01f,.8f),Pinching(b.Center+Vector3.left*.01f,.05f),list,.02f);Assert.AreEqual(8,grasp.Hover(1));
            grasp.Step(Pinching(b.Center,.01f,.8f),Pinching(b.Center+Vector3.left*.01f,.008f),list,.02f);
            Assert.AreEqual(8,grasp.Held(1));Assert.AreEqual(7,grasp.Held(0));
        }

        [Test] public void ANearerRivalTargetTakesTheHandInstead()
        {
            var grasp=new EmberdeepPieceGrasp();var top=new Vector3(0,.105f,0);
            grasp.Rival[0]=-.02f; // a sheep the palm is deeper in than the pinch point is in the tower
            grasp.Step(Pinching(top,.05f),None,One,.02f);Assert.AreEqual(-1,grasp.Hover(0));
            grasp.Step(Pinching(top,.008f),None,One,.02f);Assert.False(grasp.OwnsHand(0),"The sheep is nearer");
            grasp.Reset();grasp.Rival[0]=.02f; // the sheep is 2 cm off, the pinch point inside the tank
            grasp.Step(Pinching(top,.05f),None,One,.02f);Assert.AreEqual(7,grasp.Hover(0));
        }

        [Test] public void TowerTipComesBackWithTheFirstTimeHintsUnderTheirKeyVersion()
        {
            EmberdeepTowerTip.Hook();
            int prior=PlayerPrefs.GetInt(EmberdeepTowerTip.Key,-1);
            var hints=((EmberdeepFirstHints.Move[])System.Enum.GetValues(typeof(EmberdeepFirstHints.Move))).Select(m=>PlayerPrefs.GetInt(EmberdeepFirstHints.Key(m),-1)).ToArray();
            try
            {
                StringAssert.Contains("."+EmberdeepFirstHints.KeyVersion+".",EmberdeepTowerTip.Key);
                PlayerPrefs.DeleteKey(EmberdeepTowerTip.Key);Assert.True(EmberdeepTowerTip.Needed,"New: shown");
                EmberdeepTowerTip.MarkDone();Assert.False(EmberdeepTowerTip.Needed,"A tower set down: gone");
                EmberdeepFirstHints.ResetAll();Assert.True(EmberdeepTowerTip.Needed,"SHOW HINTS brings it back");
            }
            finally
            {
                if(prior<0)PlayerPrefs.DeleteKey(EmberdeepTowerTip.Key);else PlayerPrefs.SetInt(EmberdeepTowerTip.Key,prior);
                var moves=(EmberdeepFirstHints.Move[])System.Enum.GetValues(typeof(EmberdeepFirstHints.Move));
                for(int i=0;i<moves.Length;i++)if(hints[i]>=0)PlayerPrefs.SetInt(EmberdeepFirstHints.Key(moves[i]),hints[i]);
                PlayerPrefs.Save();
            }
        }

        [Test] public void SegmentDistanceHandlesCrossingsEndsAndPoints()
        {
            Assert.AreEqual(0,FireRun.SegmentDistance(-1,0,1,0,0,-1,0,1),1e-5f,"Crossing");
            Assert.AreEqual(.5f,FireRun.SegmentDistance(-1,0,1,0,0,.5f,0,2),1e-5f,"Past the end");
            Assert.AreEqual(0,FireRun.SegmentDistance(-1,0,1,0,0,0,0,0),1e-5f,"A dug point on the path");
            Assert.AreEqual(1,FireRun.SegmentDistance(0,0,0,0,1,0,1,0),1e-5f,"Two points");
            Assert.AreEqual(.59f,FireRun.SegmentDistance(0,0,0,1,.59f,-1,.59f,2),1e-5f,"Parallel");
        }
    }
}

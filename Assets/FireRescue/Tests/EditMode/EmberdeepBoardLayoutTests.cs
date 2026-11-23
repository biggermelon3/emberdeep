using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FireRescue.Tests
{
    // LoopSpec §5 Reach + §10 options (workstream O): the compact Emberdeep board, its seated placement and the
    // play-area offsets. The legacy tool flow keeps the authored layout.
    public sealed class EmberdeepBoardLayoutTests
    {
        static FireRun Stage(int stage,bool goddess=true)
        {
            var run=new FireRun(goddess);typeof(FireRun).GetProperty("Stage").SetValue(run,stage);
            Assert.True(run.StartStage(true));return run;
        }
        static int Rows(FireRun run)=>run.Trees.Select(t=>Mathf.RoundToInt((t.Position.z-3)/1.15f)).Distinct().Count();

        [Test] public void EveryEmberdeepFieldIsAtMostFourRowsAndEightColumns()
        {
            int widest=0;
            for(int stage=0;stage<6;stage++) // stage cut (T): 6 stages
            {
                var run=Stage(stage);
                Assert.LessOrEqual(Rows(run),EmberdeepBoardLayout.MaxRows,"stage "+stage);
                Assert.LessOrEqual(run.FieldColumns,EmberdeepBoardLayout.MaxColumns,"stage "+stage);
                // Authored levels (LV) may leave clearings, so the field is a grid of FieldColumns x FieldRows cells that
                // holds every tree once (the squares and lanes read the grid, not trees / columns).
                Assert.LessOrEqual(run.FieldRows,EmberdeepBoardLayout.MaxRows,"stage "+stage);
                Assert.LessOrEqual(run.Trees.Length,run.FieldColumns*run.FieldRows,"stage "+stage);
                for(int c=0;c<run.FieldColumns;c++)for(int r=0;r<run.FieldRows;r++)
                {int i=run.FieldTree(c,r);if(i>=0){Assert.AreEqual(run.FieldX(c),run.Trees[i].Position.x,1e-4f);Assert.AreEqual(run.FieldZ(r),run.Trees[i].Position.z,1e-4f);}}
                float back=run.Trees.Max(t=>t.Position.z);
                Assert.LessOrEqual(back,3+3*1.15f+.17f,"no tree behind the fourth row");
                widest=Mathf.Max(widest,run.FieldColumns);
                Assert.Greater(run.Burning,0);Assert.Less(run.Burning,run.Trees.Length+1);
            }
            Assert.AreEqual(EmberdeepBoardLayout.MaxColumns,widest,"one level uses the full width");
        }

        [Test] public void LegacyToolFlowKeepsTheAuthoredField()
        {
            int[] counts={3,9,18,30,42};
            for(int incident=0;incident<5;incident++)
            {
                var run=Stage(incident,false);Assert.False(run.CompactBoard);
                Assert.AreEqual(counts[incident],run.Trees.Length);
                foreach(var a in run.Animals)Assert.AreEqual(2.65f,a.Refuge.z,1e-5f);
            }
            float[][] lanes={new[]{-.59f,.59f},new[]{-.59f,.59f},new[]{-2.6f,-1.42f,0,1.42f,2.6f},new[]{-2.6f,-1.42f,0,1.42f,2.6f},new[]{-2.95f,-1.77f,-.59f,.59f,1.77f,2.95f}};
            for(int incident=0;incident<5;incident++)Assert.That(Stage(incident,false).FieldLanes(),Is.EqualTo(lanes[incident]).Within(1e-4f),"incident "+incident);
        }

        [Test] public void SheepRunInLanesAndRescuedSheepGrazeOnTheCompactApron()
        {
            for(int stage=0;stage<6;stage++) // stage cut (T): 6 stages
            {
                var run=Stage(stage);float front=run.Trees.Min(t=>t.Position.z),back=run.Trees.Max(t=>t.Position.z);
                foreach(var a in run.Animals)
                {
                    foreach(var t in run.Trees)if(t.Position.z<front+.5f)
                        Assert.GreaterOrEqual(Mathf.Abs(t.Position.x-a.Home.x),.45f,$"stage {stage}: lane clear of front-row trunks");
                    Assert.Greater(a.Home.z,back+.5f,"homes behind the grove");
                    Assert.Less(a.Refuge.z,front,"refuge in front of the grove");Assert.Greater(a.Refuge.z,a.Pasture.z,"pasture on the near bank");
                    Assert.GreaterOrEqual(a.Pasture.z-FireRun.AnimalRadius,EmberdeepBoardLayout.CompactFront,"pasture on the board");
                }
            }
        }

        [Test] public void PropsStandClearOfTrunksOnTheBoard()
        {
            for(int stage=FireRun.PalmLessons;stage<6;stage++) // the incidents (stage cut, T)
            {
                var run=Stage(stage);
                foreach(var p in run.Props)
                {
                    Assert.Less(Mathf.Abs(p.Position.x),5.2f);Assert.Greater(p.Position.z,3);Assert.Less(p.Position.z,EmberdeepBoardLayout.CompactBack-1);
                    foreach(var t in run.Trees)Assert.Greater(Vector2.Distance(new Vector2(p.Position.x,p.Position.z),new Vector2(t.Position.x,t.Position.z)),.55f,$"stage {stage} {p.Kind}");
                    foreach(var a in run.Animals)Assert.Greater(Vector2.Distance(new Vector2(p.Position.x,p.Position.z),new Vector2(a.Home.x,a.Home.z)),.9f,$"stage {stage} {p.Kind} vs sheep home");
                }
            }
        }

        [Test] public void ApronSqueezeIsMonotonicAndKeepsTheField()
        {
            Assert.AreEqual(EmberdeepBoardLayout.CompactFront,EmberdeepBoardLayout.Z(EmberdeepBoardLayout.AuthoredFront),1e-4f);
            Assert.AreEqual(EmberdeepBoardLayout.CompactBack,EmberdeepBoardLayout.Z(EmberdeepBoardLayout.AuthoredBack),1e-4f);
            for(float z=3;z<=6.7f;z+=.1f)Assert.AreEqual(z,EmberdeepBoardLayout.Z(z),1e-5f,"trees never move");
            float previous=float.NegativeInfinity;
            for(float z=-1;z<13;z+=.05f)
            {
                float c=EmberdeepBoardLayout.Z(z);Assert.Greater(c,previous);previous=c;
                Assert.AreEqual(z,EmberdeepBoardLayout.Authored(c),1e-4f);
            }
            // The creek shader works in authored space: the compact creek centre maps back to 2.1.
            var centre=EmberdeepBoardLayout.CreekFrame.MultiplyPoint3x4(new Vector3(1,0,EmberdeepBoardLayout.Z(2.1f)));
            Assert.AreEqual(2.1f,centre.z,1e-4f);Assert.AreEqual(1,centre.x,1e-6f);
            // Order on the apron: front tip < path < creek < refuge < front row.
            Assert.Less(EmberdeepBoardLayout.Z(1.38f),EmberdeepBoardLayout.Z(1.62f));Assert.Less(EmberdeepBoardLayout.Z(2.65f),3);
        }

        [Test] public void SeatedPlacementPutsTheRowsWithinReachAtDeskHeight()
        {
            var arena=new GameObject("arena").transform;
            try
            {
                var head=new Vector3(.4f,1.2f,-.3f);var forward=Quaternion.Euler(20,37,0)*Vector3.forward;
                var flat=Vector3.ProjectOnPlane(forward,Vector3.up).normalized;
                FireMiniature.PlaceCompact(arena,head,forward,0);
                float Ahead(float z)=>Vector3.Dot(arena.TransformPoint(new Vector3(0,0,z))-head,flat);
                // Headset test 2026-11-22: front row 40 cm out, board top 36 cm under the eye (was 30 / 45).
                Assert.AreEqual(.40f,Ahead(3),1e-4f,"front row");
                Assert.AreEqual(.676f,Ahead(3+3*1.15f),1e-3f,"back row (fourth)");
                Assert.AreEqual(.36f,head.y-arena.position.y,1e-4f,"a little above desk height");
                Assert.AreEqual(1,Vector3.Dot(arena.up,Vector3.up),1e-5f,"flat");
                Assert.Greater(Ahead(EmberdeepBoardLayout.CompactFront),.2f,"the front tip stays off the chest");
                float mid=Mathf.Atan2(.36f,Ahead(3+1.5f*1.15f))*Mathf.Rad2Deg;Assert.That(mid,Is.InRange(32f,35f),"~34 deg down to the middle of 4 rows");
                float mid3=Mathf.Atan2(.36f,Ahead(3+1.15f))*Mathf.Rad2Deg;Assert.Less(mid3,37f,"3 rows");
                float squares=Ahead(3+3*1.15f+1.15f*.5f);Assert.LessOrEqual(squares,.725f,"back-row piece squares within a seated lean");
                // Options: + height raises, + distance pushes away; both keep it flat.
                FireMiniature.PlaceCompact(arena,head,forward,0,.1f,.05f);
                Assert.AreEqual(.45f,Ahead(3),1e-4f);Assert.AreEqual(.26f,head.y-arena.position.y,1e-4f);
                // The options still reach the old close spot (front row 30 cm, top 45 cm under the eye).
                FireMiniature.PlaceCompact(arena,head,forward,0,EmberdeepPlayArea.HeightMin,EmberdeepPlayArea.DistanceMin);
                Assert.LessOrEqual(Ahead(3),.30f+1e-4f);Assert.GreaterOrEqual(head.y-arena.position.y,.45f-1e-4f);
                // Floor guard: a very low eye never puts the board top under 45 cm above the floor.
                FireMiniature.PlaceCompact(arena,new Vector3(0,.7f,0),Vector3.forward,0,-.15f);
                Assert.AreEqual(EmberdeepBoardLayout.FloorClearance,arena.position.y,1e-4f);
                // Headset test 4 defaults (-14 / +12): front row 52 cm out, top 50 cm under the eye, ~37 deg to the middle (flat).
                FireMiniature.PlaceCompact(arena,head,forward,0,EmberdeepPlayArea.DefaultHeight,EmberdeepPlayArea.DefaultDistance);
                Assert.AreEqual(.52f,Ahead(3),1e-4f);Assert.AreEqual(.50f,head.y-arena.position.y,1e-4f);
                Assert.That(Mathf.Atan2(.50f,Ahead(3+1.5f*1.15f))*Mathf.Rad2Deg,Is.InRange(36f,38.5f));
                // The legacy board keeps its placement.
                FireMiniature.Place(arena,head,forward,0);Assert.AreEqual(.65f,head.y-arena.position.y,1e-4f);
            }
            finally{Object.DestroyImmediate(arena.gameObject);}
        }

        [Test] public void PumpCapIsBesideTheBoardAtChestHeight()
        {
            // Pump cap (origin + .272 m) relative to the eye, for a play-area height offset (headset test 3, 2026-11-22).
            static float CapBelowEye(float height)=>EmberdeepBoardLayout.EyeDrop-height
                -(EmberdeepBoardLayout.PumpHeightFor(height)*FireMiniature.Scale+EmberdeepHammerPumpView.CapTopHeight);
            // Default seat (-14 / +6): ~30 cm under the eye (was ~42), ~27.5 cm ahead (was ~32).
            Assert.AreEqual(.30f,CapBelowEye(EmberdeepPlayArea.DefaultHeight),1e-4f);
            Assert.AreEqual(EmberdeepBoardLayout.PumpHeightFor(EmberdeepPlayArea.DefaultHeight),EmberdeepBoardLayout.PumpHeight,1e-4f);
            Assert.AreEqual(EmberdeepWaterTuning.PumpHeight,EmberdeepBoardLayout.PumpHeight);
            float ahead=EmberdeepBoardLayout.EyeToFrontRow+EmberdeepPlayArea.DefaultDistance-(EmberdeepBoardLayout.FrontRowZ-EmberdeepBoardLayout.PumpForward)*FireMiniature.Scale;
            Assert.AreEqual(.275f,ahead,1e-4f);
            // The height option moves it half as far as the board: never at eye level, never sunk under the board top.
            Assert.AreEqual(.155f,CapBelowEye(EmberdeepPlayArea.HeightMax),1e-3f);
            Assert.AreEqual(.405f,CapBelowEye(EmberdeepPlayArea.HeightMin),1e-3f);
            foreach(float h in new[]{EmberdeepPlayArea.HeightMin,EmberdeepPlayArea.DefaultHeight,0,EmberdeepPlayArea.HeightMax})
            {
                float overTop=EmberdeepBoardLayout.EyeDrop-h-CapBelowEye(h);
                Assert.That(overTop,Is.InRange(.05f,.31f),"cap over the board top at "+h);
            }
            Assert.AreEqual(EmberdeepBoardLayout.PumpHeight,EmberdeepBoardLayout.PumpHeightFor(float.NaN),1e-4f);
            float side=EmberdeepBoardLayout.PumpSideX*FireMiniature.Scale;Assert.That(side,Is.InRange(.3f,.45f));
            // Its body stays outside the island's front bulge (authored half width 3.4 at that depth).
            Assert.Greater(EmberdeepBoardLayout.PumpSideX-.6f,3.4f);
            Assert.AreEqual(EmberdeepBoardLayout.PumpSideX,EmberdeepWaterTuning.PumpSideX);
        }

        [Test] public void TerrainMeshesAreReadableForTheRuntimeFit()
        {
            foreach(var mesh in AssetDatabase.LoadAllAssetsAtPath("Assets/FireRescue/ArtV2/Models/EDV2_Terrain.fbx").OfType<Mesh>())
                Assert.True(mesh.isReadable,mesh.name+": EmberdeepBoardFit needs Read/Write on EDV2_Terrain.fbx");
        }

        [Test] public void PlayAreaOffsetsAreClampedRoundedAndAnnounced()
        {
            var saved=EmberdeepPlayAreaPrefs.Save();
            int changes=0;void Count()=>changes++;
            EmberdeepPlayArea.Changed+=Count;
            try
            {
                EmberdeepPlayArea.ResetToDefault();changes=0;
                EmberdeepPlayArea.SetHeight(.0449f);Assert.AreEqual(.04f,EmberdeepPlayArea.Height,1e-5f);Assert.AreEqual(1,changes);
                EmberdeepPlayArea.SetHeight(.04f);Assert.AreEqual(1,changes,"no event without a change");
                EmberdeepPlayArea.SetHeight(1);Assert.AreEqual(EmberdeepPlayArea.HeightMax,EmberdeepPlayArea.Height,1e-5f);
                EmberdeepPlayArea.SetHeight(-1);Assert.AreEqual(EmberdeepPlayArea.HeightMin,EmberdeepPlayArea.Height,1e-5f);
                EmberdeepPlayArea.SetDistance(-1);Assert.AreEqual(EmberdeepPlayArea.DistanceMin,EmberdeepPlayArea.Distance,1e-5f);
                EmberdeepPlayArea.SetDistance(1);Assert.AreEqual(EmberdeepPlayArea.DistanceMax,EmberdeepPlayArea.Distance,1e-5f);
                EmberdeepPlayArea.SetDistance(float.NaN);Assert.AreEqual(EmberdeepPlayArea.DefaultDistance,EmberdeepPlayArea.Distance,1e-5f,"NaN falls back to the default");
                EmberdeepPlayArea.SetHeight01(.5f);Assert.AreEqual(-.10f,EmberdeepPlayArea.Height,1e-5f,"middle of -35..+15");
                EmberdeepPlayArea.NudgeDistance(2);Assert.AreEqual(EmberdeepPlayArea.DefaultDistance+.04f,EmberdeepPlayArea.Distance,1e-5f);
                EmberdeepPlayArea.Reload();Assert.AreEqual(EmberdeepPlayArea.DefaultDistance+.04f,EmberdeepPlayArea.Distance,1e-5f,"stored");
            }
            finally{EmberdeepPlayArea.Changed-=Count;saved.Restore();}
        }
    }
}

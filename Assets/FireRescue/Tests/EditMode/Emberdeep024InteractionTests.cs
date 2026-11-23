using NUnit.Framework;
using UnityEngine;
namespace FireRescue.Tests
{
    public sealed class Emberdeep024InteractionTests
    {
        static FireToolManipulation.Hand Hand(Vector3 at,float closure=0)=>new(){Valid=true,HasGripFrame=true,GripRotation=Quaternion.Euler(180,0,0),Palm=new Pose(at,Quaternion.identity),Contact=at,Closure=closure};
        [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)][TestCase(4)]
        // Headset test 3 (2026-11-22): the press starts on the first touching frame (was a .10 s dwell) and a lift stops it
        // after EmberdeepPalmGesture.ReleaseGrace (was at once), so a one-frame blink of the tip does not cut the water.
        public void AnySingleTrackedRightFingertipPressesAtOnceAndALiftStopsAfterTheGrace(int finger)
        {
            var arena=new GameObject("Gesture arena");arena.transform.localScale=Vector3.one*.08f;
            try
            {
                var gesture=new EmberdeepPalmGesture();var left=Hand(new Vector3(0,.4f,.3f));var tips=new Vector3[5];tips[finger]=left.Contact+Vector3.up*.02f;
                gesture.Sample(left,tips,1<<finger,true,true,arena.transform,1.12f,.06f);Assert.True(gesture.Aiming);Assert.True(gesture.Casting,"no dwell");
                gesture.Sample(left,tips,1<<finger,true,true,arena.transform,1.12f,.06f);Assert.True(gesture.Casting);
                gesture.Sample(left,tips,0,true,true,arena.transform,1.12f,.02f);Assert.True(gesture.Casting,"a blink of the tip keeps the press");
                gesture.Sample(left,tips,0,true,true,arena.transform,1.12f,.07f);Assert.False(gesture.Casting,"a lift past the grace stops it");Assert.True(gesture.Aiming);
                tips[finger]=left.Contact+Vector3.down*.02f;
                for(int i=0;i<4;i++)gesture.Sample(left,tips,1<<finger,true,true,arena.transform,1.12f,.06f);
                Assert.False(gesture.Casting,"Touching the palm side must not trigger the dorsal button");
                // A lost water hand keeps its last pose for HandGrace, then lets go.
                left.Valid=false;gesture.Sample(left,tips,31,true,true,arena.transform,1.12f,.06f);Assert.True(gesture.Aiming);Assert.True(gesture.Bridged);
                gesture.Sample(left,tips,31,true,true,arena.transform,1.12f,.06f);Assert.False(gesture.Aiming);
            }
            finally{Object.DestroyImmediate(arena);}
        }
        [Test] public void PreparationRequiresExplicitStartFreezesDamageAndRejectsWater()
        {
            var run=new FireRun(true);Assert.True(run.StartStage(true));var tree=run.Trees[0];float heat=tree.Heat;
            for(int i=0;i<100;i++){run.Tick(.1f,true);run.TickMagic(.1f,true,true);run.TickPreparation(.1f);run.ApplyWater(tree.Position,100,1);}
            Assert.AreEqual(FirePhase.Preparing,run.Phase);Assert.Zero(run.Elapsed);Assert.AreEqual(heat,tree.Heat);Assert.AreEqual(EmberdeepWaterTuning.GemCapacity,run.MagicWater,"The gem is full before the level and Plan cannot spend it");Assert.AreEqual(60,run.Water);
            Assert.True(run.BeginCountdown());Assert.False(run.BeginCountdown());
            run.TickPreparation(0);run.TickPreparation(float.NaN);Assert.AreEqual(3,run.CountdownRemaining);
            for(int i=0;i<29;i++)run.TickPreparation(.1f);Assert.AreEqual(FirePhase.Preparing,run.Phase);
            run.TickPreparation(.1f);Assert.AreEqual(FirePhase.Fighting,run.Phase);Assert.Zero(run.Elapsed);
        }
        [Test] public void ProductionReplayKeepsCoinsUpgradesAndPaysForEachNewRescueOnly()
        {
            var run=new FireRun(true);run.StartStage();run.ApplyWater(Vector3.zero,10000,100);run.EvacuateSafeAnimals();Assert.AreEqual(100,run.Coins);
            Assert.True(run.BuyMagicUpgrade());Assert.AreEqual(25,run.Coins);
            Assert.True(run.RetryStage(true));Assert.AreEqual(0,run.Stage);Assert.AreEqual(25,run.Coins);Assert.AreEqual(1,run.MagicLevel);Assert.AreEqual((EmberdeepWaterTuning.GemCapacity+EmberdeepWaterTuning.GemCapacityPerLevel),run.MagicWater);
            Assert.True(run.RetryStage(true));Assert.AreEqual(25,run.Coins,"Restart alone cannot pay a rescue reward");
            run.BeginCountdown();for(int i=0;i<31;i++)run.TickPreparation(.1f);
            run.ApplyWater(Vector3.zero,10000,100);run.EvacuateSafeAnimals();Assert.AreEqual(125,run.Coins);
            run.EvacuateSafeAnimals();Assert.AreEqual(125,run.Coins);
            Assert.True(run.RetryStage());Assert.AreEqual(125,run.Coins);Assert.AreEqual(1,run.MagicLevel);
        }
        [Test] public void SheepNeedsNearbyFreshGraspFollowsReleasesAndPetsWithoutFreeMoney()
        {
            var arena=new GameObject("Sheep arena");arena.transform.localScale=Vector3.one*.08f;
            try
            {
                var run=new FireRun(true);run.StartStage();var a=run.Animals[0];var interaction=new EmberdeepSheepInteraction();
                var h=Hand(arena.transform.TransformPoint(EmberdeepSheepInteraction.Center(a)),.65f);
                for(int i=0;i<4;i++)interaction.Step(run,arena.transform,h,default,.05f);Assert.False(interaction.OwnsHand(0),"Already closed hand cannot acquire by collision");
                h.Closure=0;for(int i=0;i<5;i++)interaction.Step(run,arena.transform,h,default,.05f);Assert.Greater(a.Affection,0);Assert.Zero(run.Coins);
                h.Closure=.65f;for(int i=0;i<3;i++)interaction.Step(run,arena.transform,h,default,.05f);Assert.AreEqual(0,interaction.HeldAnimal(0));
                var start=a.Position;h.Contact+=Vector3.up*.16f;
                for(int i=0;i<10;i++)interaction.Step(run,arena.transform,h,default,.05f);Assert.Greater(a.Position.y,start.y+1.5f);Assert.Zero(run.Coins);
                run.Tick(.1f,false);Assert.AreEqual(0,a.HeldBy);Assert.AreEqual(AnimalState.Waiting,a.State);
                h.Closure=0;interaction.Step(run,arena.transform,h,default,.05f);Assert.False(interaction.OwnsHand(0));Assert.AreEqual(-1,a.HeldBy);
                for(int i=0;i<5;i++)run.Tick(.1f,false);Assert.AreEqual(0,a.Position.y,.001f);
                interaction.Reset();Assert.Zero(run.Coins);
            }
            finally{Object.DestroyImmediate(arena);}
        }
        [Test] public void SheepHoldingExpiresOnTrackingLossAndCannotBeClaimedByBothHands()
        {
            var arena=new GameObject("Sheep arena");arena.transform.localScale=Vector3.one*.08f;
            try
            {
                var run=new FireRun(true);run.StartStage();var a=run.Animals[0];var interaction=new EmberdeepSheepInteraction();var h=Hand(arena.transform.TransformPoint(EmberdeepSheepInteraction.Center(a)));
                interaction.Step(run,arena.transform,h,h,.1f);h.Closure=.65f;interaction.Step(run,arena.transform,h,h,.1f);
                Assert.True(interaction.OwnsHand(0));Assert.AreNotEqual(interaction.HeldAnimal(0),interaction.HeldAnimal(1));
                for(int i=0;i<4;i++)interaction.Step(run,arena.transform,default,default,.1f);Assert.False(interaction.OwnsHand(0));Assert.AreEqual(-1,a.HeldBy);
            }
            finally{Object.DestroyImmediate(arena);}
        }
    }
}

using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    public sealed class EmberdeepAnimalTests
    {
        static void Clear(FireRun run){foreach(var a in run.Animals)run.ApplyAnimalWater(a.Position,100,.6f);foreach(var t in run.Trees)if(t.State==TreeState.Burning)run.ApplyWater(t.Position,1000,.1f);}
        static void Tick(FireRun run,float seconds){for(int i=0;i<Mathf.CeilToInt(seconds/.05f);i++)run.Tick(.05f,false);}
        [Test] public void EverySectorHasAnAuthoredFlockAndNoTreeCash()
        {
            var run=new FireRun();int[] counts={3,4,5,6,8};
            for(int stage=0;stage<5;stage++)
            {
                Assert.True(run.StartStage());Assert.AreEqual(counts[stage],run.Animals.Length);
                int before=run.Coins;run.ApplyWater(run.Trees[0].Position,1000,.1f);
                Assert.AreEqual(before,run.Coins,"Extinguishing a tree earns score, not cash");
                Clear(run);Tick(run,14);Assert.AreEqual(counts[stage],run.AnimalsSaved,"Allow the final queued sheep to finish celebrating");
            }
        }
        [Test] public void ClearCorridorRescuesBeforeTheWholeIncidentIsOut()
        {
            var run=new FireRun();run.StartStage();run.ApplyWater(run.Trees[0].Position,1000,.1f);run.ApplyWater(run.Trees[1].Position,1000,.1f);
            Tick(run,4);Assert.AreEqual(FirePhase.Fighting,run.Phase);Assert.Greater(run.AnimalsSaved,0);Assert.Greater(run.Coins,0);
        }
        [Test] public void BlockedFlockWandersWithinItsSafeHomeArea()
        {
            var run=new FireRun();run.StartStage();var home=run.Animals[1].Home;Tick(run,3);
            Assert.AreEqual(AnimalState.Waiting,run.Animals[1].State);Assert.Greater(Vector3.Distance(home,run.Animals[1].Position),.01f);Assert.Less(Vector3.Distance(home,run.Animals[1].Position),.5f);Assert.Zero(run.Coins);
        }
        [Test] public void WaterProtectsWithoutRewardingRepeatedHits()
        {
            var run=new FireRun();run.StartStage();Tick(run,12);var a=run.Animals[1];Assert.Less(a.Health,1);
            run.ApplyAnimalWater(a.Position+Vector3.up*.45f,50,.6f);float health=a.Health;
            Tick(run,4);Assert.AreEqual(health,a.Health,.0001f);Assert.Greater(a.Wetness,0);Assert.Zero(run.Coins);
        }
        [Test] public void FarFireDoesNotDamageAnimalsAndPauseDoesNotAdvanceThem()
        {
            var run=new FireRun();run.StartStage();foreach(var t in run.Trees)t.Position+=Vector3.right*100;
            var a=run.Animals[0];run.Tick(0,false);run.Tick(float.NaN,false);Assert.Zero(a.AnimationTime);
            Tick(run,12);Assert.AreEqual(1,a.Health);Assert.AreEqual(3,run.AnimalsSaved);
        }
        [Test] public void LostSheepReduceOnlyPotentialRewardAndCannotBeRevivedForCash()
        {
            var run=new FireRun();run.StartStage();run.Animals[0].Health=.001f;Tick(run,8.2f);
            Assert.AreEqual(AnimalState.Lost,run.Animals[0].State);Clear(run);Tick(run,10);
            Assert.AreEqual(2,run.AnimalsSaved);Assert.AreEqual(25+2*25,run.Coins);
            run.ApplyAnimalWater(run.Animals[0].Position,10000,100);run.EvacuateSafeAnimals();Assert.AreEqual(75,run.Coins);
        }
        [Test] public void ArrivalPaysOnceAndSafeDepartureSettlesUnfinishedAnimations()
        {
            var run=new FireRun();run.StartStage();Clear(run);Assert.AreEqual(25,run.Coins);
            run.EvacuateSafeAnimals();Assert.AreEqual(100,run.Coins);Tick(run,20);run.EvacuateSafeAnimals();Assert.AreEqual(100,run.Coins);
            Assert.True(run.StartStage());Assert.AreEqual(100,run.Coins);Assert.Zero(run.AnimalsSaved);
        }
        [Test] public void RetryRewindsAnimalPayoutAndHealthTogether()
        {
            var run=new FireRun();run.StartStage();run.ApplyWater(run.Trees[0].Position,1000,.1f);run.ApplyWater(run.Trees[1].Position,1000,.1f);Tick(run,4);
            Assert.Greater(run.Coins,0);Assert.True(run.RetryStage());Assert.Zero(run.Coins);Assert.Zero(run.AnimalsSaved);
            foreach(var a in run.Animals){Assert.AreEqual(1,a.Health);Assert.AreEqual(AnimalState.Waiting,a.State);}
        }
        [Test] public void InvalidWaterAndBackgroundTimeCannotCreateProtectionOrCash()
        {
            var run=new FireRun();run.StartStage();var a=run.Animals[0];
            run.ApplyAnimalWater(a.Position,float.NaN,1);run.ApplyAnimalWater(a.Position,-1,1);run.ApplyAnimalWater(new Vector3(float.NaN,0,0),100,1);
            Assert.Zero(a.Wetness);run.Tick(100,false);Assert.That(a.AnimationTime,Is.LessThanOrEqualTo(.10001f));Assert.Zero(run.Coins);
        }
        [Test] public void FireCausesVisiblePanicUntilWaterExtinguishesAndProtects()
        {
            var run=new FireRun();run.StartStage();Tick(run,9);var a=run.Animals[1];
            Assert.True(a.OnFire);Assert.Greater(a.Velocity.magnitude,.4f);Assert.Greater(run.AnimalsOnFire,0);
            var before=a.Health;Tick(run,1);Assert.Less(a.Health,before);
            run.ApplyAnimalWater(a.Position+Vector3.up*FireRun.AnimalHeight,30,.1f);Assert.False(a.OnFire);
            Tick(run,3);Assert.False(a.OnFire);Assert.Greater(a.Wetness,0);
        }
        [Test] public void LastBurningAnimalKeepsWaterAvailableAfterTreesAreExtinguished()
        {
            var run=new FireRun();run.StartStage();Tick(run,9);
            foreach(var t in run.Trees)if(t.State==TreeState.Burning)run.ApplyWater(t.Position,1000,.1f);
            Assert.Zero(run.Burning);Assert.Greater(run.AnimalsOnFire,0);Assert.AreEqual(FirePhase.Fighting,run.Phase);
            foreach(var a in run.Animals)run.ApplyWater(a.Position+Vector3.up*FireRun.AnimalHeight,100,.25f);
            run.Tick(.05f,false);Assert.AreEqual(FirePhase.Cleared,run.Phase);
        }
        [Test] public void SheepSharingAnEscapeLaneQueueInsteadOfRunningThroughEachOther()
        {
            var run=new FireRun();run.StartStage();Clear(run);
            for(int frame=0;frame<200;frame++)
            {
                run.Tick(.05f,false);var a=run.Animals[0];var b=run.Animals[2];
                if(a.State!=AnimalState.Rescued && b.State!=AnimalState.Rescued)
                    Assert.Greater(Vector3.Distance(a.Position,b.Position),1,"Followers wait for the arrival celebration to finish");
            }
            Assert.AreEqual(3,run.AnimalsSaved);
        }
    }
}

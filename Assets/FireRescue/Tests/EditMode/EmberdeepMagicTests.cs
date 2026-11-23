using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    public sealed class EmberdeepMagicTests
    {
        // Intent changed (LoopSpec §8 stage cut, T): the single still tree moved to the camp tutorial. The one palm
        // lesson left is the old PALM RESCUE grove: 5 trees, 3 alight, sheep, live spread. Clearing it leads straight
        // to the first incident (no forced magic upgrade) with a full gem.
        [Test] public void ProductionPalmLessonIsALiveGroveThenTheFirstIncident()
        {
            EmberdeepSession.NewExpedition();var run=EmberdeepSession.Run;run.StartStage();
            Assert.True(run.IsPalmLesson);Assert.False(run.StillLesson);Assert.AreEqual(FireRun.LevelNames[0],run.StageName);
            Assert.AreEqual(FireRun.PalmLessonTrees,run.Trees.Length);Assert.AreEqual(FireRun.PalmLessonStarts,run.Burning);Assert.Greater(run.Animals.Length,0);
            foreach(var t in run.Trees)if(t.State==TreeState.Burning)Assert.AreEqual(FireRun.PalmLessonHeat,t.Heat);
            Assert.AreEqual(EmberdeepWaterTuning.GemCapacity,run.MagicCapacity);float tank=run.Water;
            int before=run.Burning;
            for(int i=0;i<600 && run.Burning+run.Burnt<=before;i++)run.Tick(.05f,false);
            Assert.Greater(run.Burning+run.Burnt,before,"The fire spreads in the palm lesson now");
            run.ApplyWater(Vector3.zero,10000,100);run.EvacuateSafeAnimals();
            Assert.AreEqual(FirePhase.Cleared,run.Phase);Assert.AreEqual(tank,run.Water);Assert.False(run.NeedsMagicUpgrade);
            Assert.True(run.StartStage());Assert.False(run.IsPalmLesson);Assert.AreEqual(1,run.Stage);Assert.AreEqual(0,run.IncidentIndex);
            Assert.AreEqual(run.MagicCapacity,run.MagicWater);
        }
        // Intent changed (LoopSpec §3.3): the gem waits 1.5 s after spraying before its passive
        // refill; the old crystal recharged from the first idle frame.
        [Test] public void GemUsesItsOwnBudgetPausesAndIgnoresTheToolSupply()
        {
            var run=new FireRun(true);run.StartStage();float amount=0;
            for(int i=0;i<30;i++)amount+=run.TickMagic(.1f,true,true);
            Assert.AreEqual(0,run.MagicWater);Assert.AreEqual(EmberdeepWaterTuning.GemCapacity,run.MagicConsumed,.001f);Assert.AreEqual(45*EmberdeepWaterTuning.GemCapacity/14f,amount,.001f);
            Assert.AreEqual(60,run.Water);Assert.AreEqual(0,run.SuppliedWater);
            run.TickMagic(20,false,false);run.TickMagic(float.NaN,false,true);Assert.AreEqual(0,run.MagicWater);
            run.TickMagic(20,false,true);Assert.AreEqual(0,run.MagicWater,"No background catch-up: one clamped frame is still inside the wait");
            for(int i=0;i<1000;i++)run.TickMagic(.1f,false,true);
            Assert.AreEqual(run.MagicCapacity,run.MagicWater);
        }
        // Intent changed (LoopSpec §8 stage cut, T): "waiting is safe" belonged to the still single-tree lesson, which
        // moved to the camp tutorial; the palm lesson left is a live grove, so only the retry accounting is checked.
        [Test] public void RetryRestoresMagicAccountingInThePalmLesson()
        {
            var run=new FireRun(true);run.StartStage();Assert.True(run.IsPalmLesson);
            for(int i=0;i<20;i++)run.Tick(.1f,false);Assert.AreEqual(FirePhase.Fighting,run.Phase);
            run.TickMagic(.1f,true,true);Assert.Greater(run.MagicConsumed,0);Assert.True(run.RetryStage());
            Assert.Greater(run.MagicConsumed,0);Assert.AreEqual(run.MagicCapacity,run.MagicWater);
        }
        [Test] public void UpgradeIsPaidSupplyOnlyAndImprovesDurationWithoutManualRefill()
        {
            var run=new FireRun(true);run.StartStage();Assert.False(run.BuyMagicUpgrade());
            run.ApplyWater(run.Trees[0].Position,100,100);run.EvacuateSafeAnimals();
            Assert.True(run.CanUpgradeMagic);int coins=run.Coins,cost=run.MagicUpgradeCost;
            // Intent changed (LoopSpec §3.4): gem 40, +20 per upgrade (was mana 30, +20).
            Assert.True(run.BuyMagicUpgrade());Assert.AreEqual(coins-cost,run.Coins);Assert.AreEqual((EmberdeepWaterTuning.GemCapacity+EmberdeepWaterTuning.GemCapacityPerLevel),run.MagicCapacity);
            Assert.AreEqual(EmberdeepWaterTuning.GemCapacity,run.MagicWater,"Purchasing capacity does not refill magic");
            run.StartStage();Assert.AreEqual((EmberdeepWaterTuning.GemCapacity+EmberdeepWaterTuning.GemCapacityPerLevel),run.MagicWater);run.TickMagic(.1f,true,true);run.RetryStage();Assert.AreEqual(1,run.MagicLevel);Assert.AreEqual((EmberdeepWaterTuning.GemCapacity+EmberdeepWaterTuning.GemCapacityPerLevel),run.MagicWater);
            run.Restart();Assert.AreEqual(0,run.MagicLevel);Assert.AreEqual(EmberdeepWaterTuning.GemCapacity,run.MagicWater);
        }
        [Test] public void PalmStackRequiresGroundOpenFingersAndTheOtherHandsTouch()
        {
            var go=new GameObject("Magic gesture arena");go.transform.localScale=Vector3.one*.08f;
            try
            {
                var gesture=new EmberdeepPalmGesture();var rotation=Quaternion.Euler(180,0,0);
                var left=new FireToolManipulation.Hand{Valid=true,HasGripFrame=true,GripRotation=rotation,Contact=new Vector3(0,.4f,.3f),Palm=new Pose(new Vector3(0,.4f,.3f),rotation)};
                var tips=new Vector3[5];tips[1]=left.Contact+Vector3.up*.02f;int mask=2;
                gesture.Sample(left,tips,0,true,true,go.transform,1.12f,.08f);Assert.True(gesture.Aiming);Assert.False(gesture.Casting);
                gesture.Sample(left,tips,mask,true,true,go.transform,1.12f,.08f);Assert.True(gesture.Casting);Assert.AreEqual(0,gesture.Target.y,.0001f);
                mask=0;gesture.Sample(left,tips,mask,true,true,go.transform,1.12f,.1f);Assert.False(gesture.Casting);Assert.True(gesture.Aiming);
                mask=2;gesture.Sample(left,tips,mask,true,true,go.transform,1.12f,.08f);Assert.True(gesture.Casting,"a new touch presses at once");Assert.AreEqual(2,gesture.Presses);
                // A closed hand (Closure .6, openness .4) stops aiming once HandGrace has passed.
                left.Closure=.6f;gesture.Sample(left,tips,mask,false,true,go.transform,1.12f,.1f);Assert.False(gesture.Aiming);left.Closure=0;
                gesture.Sample(left,tips,mask,true,false,go.transform,1.12f,.08f);Assert.False(gesture.Aiming);
                left.GripRotation=Quaternion.identity;gesture.Sample(left,tips,mask,true,true,go.transform,1.12f,.08f);Assert.False(gesture.Aiming,"Upward palm must not target behind itself");
            }
            finally{Object.DestroyImmediate(go);}
        }
    }
}

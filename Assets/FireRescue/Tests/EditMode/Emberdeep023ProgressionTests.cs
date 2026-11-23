using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    public sealed class Emberdeep023ProgressionTests
    {
        static void Clear(FireRun run){run.ApplyWater(Vector3.zero,10000,100);run.EvacuateSafeAnimals();}
        // Intent changed (LoopSpec §8 stage cut, T): one palm lesson (was three) precedes the five incidents; the camp
        // tutorial teaches the single tree. The forced first magic upgrade after lesson 1 is gone.
        [Test] public void OnePalmLessonPrecedesAllFiveOriginalIncidents()
        {
            // Compact board (LoopSpec §5 Reach): at most 4 rows; the legacy tool flow keeps 3/9/18/30/42.
            // The boards are the hand-authored levels (EmberdeepLevels, LV); their own gate is EmberdeepLevelTests.
            var run=new FireRun(true);var counts=new int[6];for(int s=0;s<6;s++)counts[s]=EmberdeepLevels.For(s).Trees.Length;
            Assert.AreEqual(6,run.StageCount);
            for(int stage=0;stage<6;stage++)
            {
                Assert.True(run.StartStage());Assert.AreEqual(stage,run.Stage);Assert.AreEqual(counts[stage],run.Trees.Length);
                Assert.AreEqual(stage<1,run.IsPalmLesson);Assert.AreEqual(Mathf.Max(0,stage-1),run.IncidentIndex,"IncidentIndex still counts the five incidents");
                Clear(run);
                Assert.False(run.NeedsMagicUpgrade,"No forced shop lesson");
                Assert.AreEqual(stage==5?FirePhase.Won:FirePhase.Cleared,run.Phase);
            }
            Assert.False(run.StartStage());Assert.AreEqual(6,run.ClearedIncidents);
        }
        // Intent changed (LoopSpec §3.3): the gem refills passively at 2.5/s after a 1.5 s wait,
        // so a bigger gem takes longer; it used to recover in 5 s, then 30 s for the basin.
        [Test] public void EmptyGemWaitsThenRefillsPassivelyAtTheTunedRate()
        {
            var run=new FireRun(true);run.StartStage();
            for(int i=0;i<30;i++)run.TickMagic(.1f,true,true);Assert.Zero(run.MagicWater);
            for(int i=0;i<15;i++)run.TickMagic(.1f,false,true);Assert.Zero(run.MagicWater,"Nothing during the 1.5 s wait");
            Assert.True(run.GemPassiveRefilling);
            run.TickMagic(.1f,false,true);Assert.AreEqual(EmberdeepWaterTuning.PassivePerSecond*.1f,run.MagicWater,.001f);
            for(int i=0;i<158;i++)run.TickMagic(.1f,false,true);Assert.Less(run.MagicWater,run.MagicCapacity);
            run.TickMagic(.1f,false,true);Assert.AreEqual(EmberdeepWaterTuning.GemCapacity,run.MagicWater,.01f);
            Assert.False(run.GemPassiveRefilling);
            run.TickMagic(.1f,true,true);for(int i=0;i<10;i++)run.TickMagic(.1f,false,true);
            Assert.Less(run.MagicWater,run.MagicCapacity,"Spraying again restarts the wait");
            Clear(run);run.BuyMagicUpgrade();run.StartStage();
            for(int i=0;i<50;i++)run.TickMagic(.1f,true,true);Assert.Zero(run.MagicWater);
            for(int i=0;i<15+239;i++)run.TickMagic(.1f,false,true);Assert.Less(run.MagicWater,run.MagicCapacity);
            run.TickMagic(.1f,false,true);Assert.AreEqual(EmberdeepWaterTuning.GemCapacity+EmberdeepWaterTuning.GemCapacityPerLevel,run.MagicWater,.01f);
        }
        [Test] public void FourthLevelExtinguisherIsWiderAndStrongerThanFirstUpgradedPalm()
        {
            var run=new FireRun(true);run.StartStage();Clear(run);run.BuyMagicUpgrade();
            var extinguisher=FireEquipment.Get(FireEquipmentId.Extinguisher);
            Assert.Greater(.6f+extinguisher.Width,run.MagicRadius);Assert.Greater(extinguisher.Power,run.MagicPower);
            Assert.Greater(extinguisher.Flow,FireRun.MagicFlow);Assert.Greater(extinguisher.Range*FireMiniature.Scale,run.MagicRange);
        }
        [Test] public void SuccessfulTutorialsPersistAcrossRetryButResetForNewGame()
        {
            var run=new FireRun(true);run.StartStage();run.CompletePalmTutorial();run.LearnWeapon(FireEquipmentId.Extinguisher);
            Assert.False(run.NeedsTutorial(EmberdeepInteractionDemo.Action.Goddess));run.RetryStage();
            Assert.True(run.PalmTutorialComplete);Assert.False(run.NeedsTutorial(EmberdeepInteractionDemo.Action.Extinguisher));
            run.Restart();Assert.False(run.PalmTutorialComplete);Assert.True(run.NeedsTutorial(EmberdeepInteractionDemo.Action.Extinguisher));
        }
    }
}

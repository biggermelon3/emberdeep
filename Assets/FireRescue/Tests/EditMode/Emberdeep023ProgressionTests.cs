using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    public sealed class Emberdeep023ProgressionTests
    {
        static void Clear(FireRun run){run.ApplyWater(Vector3.zero,10000,100);run.EvacuateSafeAnimals();}
        [Test] public void ThreePalmLevelsPrecedeAllFiveOriginalIncidents()
        {
            var run=new FireRun(true);int[] counts={1,3,5,3,9,18,30,42};
            for(int stage=0;stage<8;stage++)
            {
                Assert.True(run.StartStage());Assert.AreEqual(stage,run.Stage);Assert.AreEqual(counts[stage],run.Trees.Length);
                Assert.AreEqual(stage<3,run.IsPalmLesson);Assert.AreEqual(Mathf.Max(0,stage-3),run.IncidentIndex);
                Assert.AreEqual(stage<4?0:stage==4?1:stage==5?2:stage==6?3:5,run.Props.Length);
                Clear(run);
                if(stage==0)
                {
                    Assert.True(run.NeedsMagicUpgrade);Assert.False(run.StartStage());
                    Assert.False(run.BuyUpgrade());Assert.False(run.BuyStationUpgrade());Assert.False(run.BuyEquipment(FireEquipmentId.PulsePistol));
                    Assert.True(run.BuyMagicUpgrade());
                }
                Assert.AreEqual(stage==7?FirePhase.Won:FirePhase.Cleared,run.Phase);
            }
            Assert.False(run.StartStage());Assert.AreEqual(8,run.ClearedIncidents);
        }
        // Spell mode: after the first lesson crystals recover slowly so the well basin matters.
        [Test] public void EmptyMagicRecoversInFiveSecondsThenThirtyAfterCapacityUpgrade()
        {
            var run=new FireRun(true);run.StartStage();
            for(int i=0;i<30;i++)run.TickMagic(.1f,true,true);Assert.Zero(run.MagicWater);
            for(int i=0;i<49;i++)run.TickMagic(.1f,false,true);Assert.Less(run.MagicWater,run.MagicCapacity);
            run.TickMagic(.1f,false,true);Assert.AreEqual(30,run.MagicWater,.001f);
            Clear(run);run.BuyMagicUpgrade();run.StartStage();
            for(int i=0;i<40;i++)run.TickMagic(.1f,true,true);Assert.Zero(run.MagicWater);
            for(int i=0;i<299;i++)run.TickMagic(.1f,false,true);Assert.Less(run.MagicWater,run.MagicCapacity);
            run.TickMagic(.1f,false,true);Assert.AreEqual(50,run.MagicWater,.01f);
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

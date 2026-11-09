using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    public sealed class EmberdeepStationTierTests
    {
        static void Clear(FireRun run)
        {
            foreach(var tree in run.Trees)if(tree.State==TreeState.Burning)run.ApplyWater(tree.Position,100,.1f);
            Assert.AreEqual(FirePhase.Cleared,run.Phase);
        }
        static FireRun AtTier(WaterStationTier tier)
        {
            var run=new FireRun();
            while((int)run.StationTier<(int)tier)
            {
                while(run.Coins<run.StationUpgradeCost){Assert.IsTrue(run.StartStage());Clear(run);}
                Assert.IsTrue(run.BuyStationUpgrade());
            }
            return run;
        }
        static void Drain(FireRun run)
        {
            if(run.InSupply)Assert.IsTrue(run.StartStage());
            run.Tick(.01f,false);
            for(int i=0;i<100 && run.Water>0;i++)run.Tick(.1f,true);
            run.CancelTrigger();Assert.Zero(run.Water);
        }
        static void Dock(FireRun run)
        {
            for(int i=0;i<5;i++)run.Supply.SampleDock(WaterSourceKind.Hydrant,0,0,0,true,.05f);
            Assert.IsTrue(run.Supply.Locked);
        }
        static float LeverStroke(EmberdeepWaterSupply s)
        {s.SampleLever(0,true,true);s.SampleLever(.08f,true,true);return s.SampleLever(.01f,true,true);}

        [Test] public void StarterWellHasNoValveButtonOrIdleWaterPath()
        {
            var run=new FireRun();Assert.AreEqual(WaterStationTier.LeverWell,run.StationTier);
            Assert.AreEqual(100,run.StationUpgradeCost);Assert.IsFalse(run.CanUpgradeStation);
            Drain(run);Dock(run);var s=run.Supply;
            Assert.IsFalse(s.SetValveOpen(true));Assert.IsFalse(s.TurnValve(90));
            s.SampleButton(false,true);Assert.IsFalse(s.SampleButton(true,true));
            for(int i=0;i<50;i++)Assert.Zero(s.Tick(.1f,true));
            Assert.Zero(run.Water);Assert.Zero(run.SuppliedWater);
            Assert.AreEqual(12,LeverStroke(s));Assert.AreEqual(12,run.Water);Assert.AreEqual(12,run.SuppliedWater);
        }
        [Test] public void LeverRequiresCompleteDownAndReturnWithoutDuplicateCredits()
        {
            var run=new FireRun();Drain(run);Dock(run);var s=run.Supply;
            s.SampleLever(.09f,true,true);Assert.Zero(s.SampleLever(0,true,true));
            for(int i=0;i<20;i++){s.SampleLever(.07f,true,true);Assert.Zero(s.SampleLever(.01f,true,true));}
            Assert.Zero(run.Water);s.SampleLever(.08f,true,true);Assert.IsTrue(s.LeverReturnRequired);
            Assert.Zero(s.SampleLever(.04f,true,true));Assert.IsTrue(s.LeverReturnRequired);
            Assert.AreEqual(12,s.SampleLever(.02f,true,true));Assert.IsFalse(s.LeverReturnRequired);
            for(int i=0;i<20;i++)Assert.Zero(s.SampleLever(.01f,true,true));
            Assert.AreEqual(12,run.Water);
        }
        [Test] public void ReleasingOrLosingTrackingCancelsUnfinishedLeverStroke()
        {
            var run=new FireRun();Drain(run);Dock(run);var s=run.Supply;
            s.SampleLever(0,true,true);s.SampleLever(.08f,true,true);s.SampleLever(.01f,false,true);
            Assert.Zero(s.SampleLever(.01f,true,true));Assert.Zero(run.Water);
            s.SampleLever(.08f,true,true);s.SampleLever(.01f,true,false);
            Assert.IsFalse(s.Locked);Assert.IsFalse(s.LeverReturnRequired);Assert.Zero(run.Water);
            Dock(run);Assert.Zero(s.SampleLever(0,true,true));Assert.Zero(run.Water);
        }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(.31f)]
        public void InvalidLeverSampleCancelsPendingCredit(float invalid)
        {
            var run=new FireRun();Drain(run);Dock(run);var s=run.Supply;
            s.SampleLever(0,true,true);s.SampleLever(.08f,true,true);Assert.Zero(s.SampleLever(invalid,true,true));
            Assert.Zero(s.SampleLever(0,true,true));Assert.Zero(run.SuppliedWater);
        }
        [Test] public void FinalLeverStrokeCreditsOnlyRemainingCapacity()
        {
            var run=new FireRun();Drain(run);Clear(run);Assert.IsTrue(run.BuyUpgrade());
            Dock(run);float credited=0;run.Supply.PumpStroke+=v=>credited+=v;
            for(int i=0;i<7;i++)Assert.AreEqual(12,LeverStroke(run.Supply));
            Assert.AreEqual(1,LeverStroke(run.Supply));Assert.AreEqual(85,run.Water);
            Assert.AreEqual(85,credited);Assert.AreEqual(85,run.SuppliedWater);
            Assert.Zero(LeverStroke(run.Supply));Assert.AreEqual(HydrantRefillState.DockedFull,run.Supply.HydrantState);
        }
        [Test] public void RotaryRequiresSignedDisplacementAndRejectsOtherTierControls()
        {
            var run=AtTier(WaterStationTier.RotaryHydrant);Drain(run);Dock(run);var s=run.Supply;
            Assert.Zero(LeverStroke(s));s.SampleButton(false,true);Assert.IsFalse(s.SampleButton(true,true));
            for(int i=0;i<20;i++){s.TurnValve(10);s.TurnValve(-10);}
            Assert.IsFalse(s.ValveOpen);Assert.IsFalse(s.TurnValve(44));Assert.IsTrue(s.TurnValve(1));
            Assert.AreEqual(3,s.Tick(.1f,true));Assert.IsTrue(s.TurnValve(-45));Assert.Zero(s.Tick(.1f,true));
            Assert.AreEqual(3,run.Water);
        }
        [Test] public void ButtonNeedsRearmAndEachNewPressTogglesOnce()
        {
            var run=AtTier(WaterStationTier.PushButton);Drain(run);Dock(run);var s=run.Supply;
            Assert.IsFalse(s.SetValveOpen(true));Assert.IsFalse(s.TurnValve(90));Assert.Zero(LeverStroke(s));
            Assert.IsFalse(s.SampleButton(true,true));Assert.Zero(s.Tick(.1f,true));
            s.SampleButton(false,true);Assert.IsTrue(s.SampleButton(true,true));Assert.IsTrue(s.ValveOpen);
            for(int i=0;i<10;i++)Assert.IsFalse(s.SampleButton(true,true));
            Assert.AreEqual(3,s.Tick(.1f,true));s.SampleButton(false,true);
            Assert.IsTrue(s.SampleButton(true,true));Assert.IsFalse(s.ValveOpen);Assert.Zero(s.Tick(.1f,true));
            s.SampleButton(false,true);Assert.IsTrue(s.SampleButton(true,true));
            for(int i=0;i<30;i++)s.Tick(.1f,true);
            Assert.AreEqual(run.Capacity,run.Water);Assert.IsFalse(s.ValveOpen);
            Assert.IsFalse(s.SampleButton(true,true));Assert.AreEqual(run.Capacity,run.SuppliedWater);
        }
        [Test] public void TrackingLossDisconnectsButtonFlowWithoutExtraWater()
        {
            var run=AtTier(WaterStationTier.PushButton);Drain(run);Dock(run);var s=run.Supply;
            s.SampleButton(false,true);s.SampleButton(true,true);s.Tick(.1f,true);float water=run.Water;
            Assert.Zero(s.Tick(.1f,false));Assert.IsFalse(s.Locked);Assert.IsFalse(s.ValveOpen);
            Dock(run);Assert.IsFalse(s.SampleButton(true,true));Assert.Zero(s.Tick(.1f,true));Assert.AreEqual(water,run.Water);
        }
        [Test] public void StationPurchasesCostExactlyAndNeverChangeAnyTank()
        {
            var run=new FireRun();Assert.IsFalse(run.BuyStationUpgrade());run.StartStage();Clear(run);
            int coins=run.Coins;float water=run.Water;
            Dock(run);Assert.IsFalse(run.CanUpgradeStation);Assert.IsFalse(run.BuyStationUpgrade());run.Supply.Disconnect();
            Assert.IsTrue(run.BuyStationUpgrade());Assert.AreEqual(coins-100,run.Coins);Assert.AreEqual(200,run.StationUpgradeCost);
            Assert.AreEqual(water,run.Water);Assert.Zero(run.SuppliedWater);
            run.StartStage();Assert.IsFalse(run.BuyStationUpgrade());Clear(run);
            while(run.Coins<200){run.StartStage();Clear(run);}
            coins=run.Coins;Assert.IsTrue(run.BuyStationUpgrade());Assert.AreEqual(coins-200,run.Coins);
            Assert.AreEqual(WaterStationTier.PushButton,run.StationTier);Assert.Zero(run.StationUpgradeCost);
            Assert.IsFalse(run.CanUpgradeStation);Assert.IsFalse(run.BuyStationUpgrade());Assert.AreEqual(water,run.Water);
        }
        [Test] public void RetryRestoresPurchasedAssetsTierAndEntryEconomyTogether()
        {
            var run=AtTier(WaterStationTier.RotaryHydrant);
            while(run.Coins<250){run.StartStage();Clear(run);}
            Assert.IsTrue(run.BuyEquipment(FireEquipmentId.PulsePistol));Assert.IsTrue(run.BuyUpgrade());
            int coins=run.Coins,upgrade=run.Upgrade;float water=run.Water;
            run.StartStage();var tree=run.Trees[0];run.ApplyWater(tree.Position,100,.1f);Assert.Greater(run.Coins,coins);
            Assert.IsTrue(run.RetryStage());Assert.AreEqual(coins,run.Coins);Assert.AreEqual(water,run.Water);
            Assert.IsTrue(run.Owns(FireEquipmentId.PulsePistol));Assert.AreEqual(upgrade,run.Upgrade);
            Assert.AreEqual(WaterStationTier.RotaryHydrant,run.StationTier);
            Assert.IsTrue(run.RetryStage());Assert.AreEqual(coins,run.Coins);
            run.Restart();Assert.AreEqual(WaterStationTier.LeverWell,run.StationTier);Assert.Zero(run.Coins);
            Assert.IsFalse(run.Owns(FireEquipmentId.PulsePistol));Assert.Zero(run.Upgrade);Assert.AreEqual(60,run.Water);
        }
    }
}

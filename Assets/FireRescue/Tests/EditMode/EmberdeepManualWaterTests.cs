using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    public sealed class EmberdeepManualWaterTests
    {
        static FireRun ActiveDry()
        {
            var run=new FireRun();
            // This fixture exercises the upgraded rotary source; starter-well rules have separate tests.
            typeof(FireRun).GetProperty(nameof(FireRun.StationTier)).SetValue(run,WaterStationTier.RotaryHydrant);
            run.StartStage();
            for(int i=0;i<50;i++)run.Tick(.1f,true);
            Assert.Zero(run.Water);return run;
        }
        static void Dock(FireRun run,WaterSourceKind kind=WaterSourceKind.Hydrant,int hand=1)
        {
            if(kind==WaterSourceKind.Hydrant)typeof(FireRun).GetProperty(nameof(FireRun.StationTier)).SetValue(run,WaterStationTier.RotaryHydrant);
            for(int i=0;i<5;i++)run.Supply.SampleDock(kind,hand,.01f,0,true,.05f);
            Assert.IsTrue(run.Supply.Locked);
        }
        static void Clear(FireRun run)
        {foreach(var tree in run.Trees)if(tree.State==TreeState.Burning)run.ApplyWater(tree.Position,100,.1f);}
        static float Stroke(EmberdeepWaterSupply supply)
        {
            supply.SamplePump(0,true,true);supply.SamplePump(.08f,true,true);
            return supply.SamplePump(.01f,true,true);
        }

        [Test] public void NewExpeditionSuppliesOnlyStarterTank()
        {
            var run=new FireRun();Assert.AreEqual(60,run.Water);
            Assert.Zero(run.WaterFor(FireEquipmentId.PulsePistol));Assert.Zero(run.SuppliedWater);
            run.StartStage();Clear(run);Assert.IsTrue(run.BuyEquipment(FireEquipmentId.PulsePistol));
            run.Equip(FireEquipmentId.PulsePistol);Assert.Zero(run.Water);
            run.Restart();Assert.AreEqual(60,run.Water);Assert.Zero(run.SuppliedWater);
            Assert.IsFalse(run.Owns(FireEquipmentId.PulsePistol));
        }
        [Test] public void SimulationTickAloneCannotCreditRecoveryWithoutExplicitEligibility()
        {
            // GD-WATER-10 supersedes the blanket no-regeneration rule. Ordinary
            // simulation still cannot guess focus/tracking/shop eligibility or
            // double-credit the separate recovery transaction.
            var run=ActiveDry();for(int i=0;i<100;i++)run.Tick(.1f,false);
            Assert.Zero(run.Water);Clear(run);for(int i=0;i<100;i++)run.Tick(.1f,false);
            Assert.Zero(run.Water);Assert.IsTrue(run.StartStage());Assert.Zero(run.Water);
            Assert.Zero(run.RecoveredWater);Assert.Zero(run.SuppliedWater);
        }
        [Test] public void UpgradeAddsCapacityWithoutWaterOrSupplyCredit()
        {
            var run=ActiveDry();Clear(run);Assert.IsTrue(run.BuyUpgrade());
            Assert.AreEqual(85,run.Capacity);Assert.Zero(run.Water);Assert.Zero(run.SuppliedWater);
        }
        [Test] public void DockNeedsContinuousAlignedDwellAndDoesNotGrantWater()
        {
            var run=ActiveDry();var s=run.Supply;
            s.SampleDock(WaterSourceKind.Hydrant,1,.01f,0,true,.1f);
            s.SampleDock(WaterSourceKind.Hydrant,1,.01f,36,true,.1f);
            s.SampleDock(WaterSourceKind.Hydrant,1,.01f,0,true,.1f);
            s.SampleDock(WaterSourceKind.Hydrant,1,.01f,0,true,.1f);
            Assert.IsFalse(s.Locked,"A failed alignment must reset the dwell");
            Assert.IsTrue(s.SampleDock(WaterSourceKind.Hydrant,1,.01f,0,true,.05f));
            Assert.AreEqual(HydrantRefillState.DockedClosed,s.HydrantState);
            Assert.Zero(s.Tick(.1f,true));Assert.Zero(run.Water);
        }
        [TestCase(float.NaN,0)] [TestCase(.01f,float.PositiveInfinity)]
        [TestCase(-.01f,0)] [TestCase(.09f,0)] [TestCase(.01f,36)]
        public void InvalidDockCannotOwnOrTransfer(float distance,float angle)
        {
            var run=ActiveDry();for(int i=0;i<20;i++)run.Supply.SampleDock(WaterSourceKind.Hydrant,1,distance,angle,true,.1f);
            Assert.IsFalse(run.Supply.Locked);Assert.IsFalse(run.Supply.SetValveOpen(true));Assert.Zero(run.Supply.Tick(.1f,true));
        }
        [Test] public void AlternatingHandsCannotCombineDwellOrStealDock()
        {
            var run=ActiveDry();for(int i=0;i<12;i++)run.Supply.SampleDock(WaterSourceKind.Hydrant,i%2,0,0,true,.1f);
            Assert.IsFalse(run.Supply.Locked);Dock(run,WaterSourceKind.Hydrant,0);
            Assert.IsFalse(run.Supply.SampleDock(WaterSourceKind.Creek,1,0,0,true,.1f));
            Assert.AreEqual(0,run.Supply.OwnerHand);Assert.AreEqual(WaterSourceKind.Hydrant,run.Supply.Kind);
        }
        [Test] public void SignedValveDetentsAndReleaseDiscardPartialTurn()
        {
            var run=ActiveDry();Dock(run);var s=run.Supply;
            s.TurnValve(30);s.ReleaseValve();Assert.IsFalse(s.TurnValve(20));Assert.IsFalse(s.ValveOpen);
            s.ReleaseValve();for(int i=0;i<10;i++){s.TurnValve(2);s.TurnValve(-2);}Assert.IsFalse(s.ValveOpen);
            Assert.IsTrue(s.TurnValve(45));Assert.AreEqual(3,s.Tick(.1f,true));
            s.ReleaseValve();Assert.IsTrue(s.ValveOpen,"Release holds the accepted detent");
            Assert.IsTrue(s.TurnValve(-45));Assert.Zero(s.Tick(.1f,true));Assert.AreEqual(3,run.Water);
        }
        [Test] public void HydrantTransferClampsToCapacityAndCountersAgree()
        {
            var run=ActiveDry();Dock(run);run.Supply.SetValveOpen(true);float sum=0;
            run.Supply.WaterTransferred+=value=>sum+=value;
            for(int i=0;i<40;i++)run.Supply.Tick(.1f,true);
            Assert.AreEqual(60,run.Water);Assert.AreEqual(60,run.SuppliedWater);Assert.AreEqual(60,sum);
            Assert.AreEqual(HydrantRefillState.DockedFull,run.Supply.HydrantState);Assert.IsFalse(run.Supply.ValveOpen);
            Assert.Zero(run.Supply.Tick(.1f,true));Assert.That(60+run.SuppliedWater-run.ConsumedWater,Is.EqualTo(run.Water).Within(.001f));
        }
        [Test] public void DisconnectAndTrackingLossRetainPartialWaterButNeverResumeFlow()
        {
            var run=ActiveDry();Dock(run);run.Supply.SetValveOpen(true);run.Supply.Tick(.1f,true);
            Assert.Zero(run.Supply.Tick(.1f,false));Assert.IsFalse(run.Supply.Locked);
            Assert.Zero(run.Supply.Tick(.1f,true));Assert.AreEqual(3,run.Water);
            Dock(run);Assert.IsFalse(run.Supply.ValveOpen);run.Supply.SetValveOpen(true);run.Supply.Tick(.1f,true);
            run.Supply.Disconnect();run.Supply.Disconnect();Assert.AreEqual(6,run.Water);Assert.AreEqual(6,run.SuppliedWater);
        }
        [Test] public void DockBlocksWeaponsAndRequiresReleaseAfterUnlock()
        {
            var run=ActiveDry();Dock(run);run.Supply.SetValveOpen(true);run.Supply.Tick(.1f,true);
            Assert.IsFalse(run.Tick(.1f,true));Assert.IsFalse(run.Equip(FireEquipmentId.Extinguisher));
            Assert.AreEqual(3,run.Water);run.Supply.Disconnect();
            Assert.IsFalse(run.Tick(.1f,true));Assert.AreEqual(3,run.Water);
            run.Tick(.02f,false);Assert.IsTrue(run.Tick(.1f,true));Assert.Less(run.Water,3);
        }
        [Test] public void SourceKindsAreExclusiveAndUnprimedCreekCannotPay()
        {
            var run=ActiveDry();Dock(run);Assert.IsFalse(run.Supply.PlaceIntake(true));Assert.Zero(Stroke(run.Supply));
            run.Supply.Disconnect();Dock(run,WaterSourceKind.Creek);Assert.IsFalse(run.Supply.SetValveOpen(true));
            Assert.Zero(Stroke(run.Supply));Assert.Zero(run.Water);
        }
        [Test] public void CreekNeedsHomeOutAndReturnAndJitterCannotDuplicate()
        {
            var run=ActiveDry();Dock(run,WaterSourceKind.Creek);run.Supply.PlaceIntake(true);
            run.Supply.SamplePump(.08f,true,true);Assert.Zero(run.Supply.SamplePump(.01f,true,true));
            run.Supply.SamplePump(.079f,true,true);Assert.Zero(run.Supply.SamplePump(0,true,true));
            Assert.AreEqual(12,Stroke(run.Supply));for(int i=0;i<20;i++)Assert.Zero(run.Supply.SamplePump(.01f,true,true));
            Assert.AreEqual(12,run.Water);Assert.AreEqual(12,run.SuppliedWater);
            Assert.AreEqual(12,Stroke(run.Supply));Assert.AreEqual(24,run.Water);
        }
        [Test] public void InterruptedStrokeCannotBeBankedOnReconnect()
        {
            var run=ActiveDry();Dock(run,WaterSourceKind.Creek);var s=run.Supply;s.PlaceIntake(true);
            s.SamplePump(0,true,true);s.SamplePump(.08f,true,true);s.PlaceIntake(false);s.PlaceIntake(true);
            Assert.Zero(s.SamplePump(0,true,true));Assert.Zero(run.Water);
            s.SamplePump(.08f,true,true);s.SamplePump(.08f,false,true);Assert.Zero(s.SamplePump(0,true,true));
            Assert.Zero(run.Water);Assert.AreEqual(12,Stroke(s));
            s.SamplePump(.08f,true,true);s.SamplePump(0,true,false);Assert.IsFalse(s.Locked);Assert.AreEqual(12,run.Water);
        }
        [Test] public void CreekLastStrokeCreditsOnlyMissingWater()
        {
            var run=new FireRun();run.StartStage();run.Tick(.1f,true);float missing=run.Capacity-run.Water;
            Dock(run,WaterSourceKind.Creek);run.Supply.PlaceIntake(true);
            Assert.That(Stroke(run.Supply),Is.EqualTo(missing).Within(.0001f));
            Assert.AreEqual(CreekRefillState.Full,run.Supply.CreekState);Assert.AreEqual(run.Capacity,run.Water);
            Assert.Zero(Stroke(run.Supply));Assert.That(run.SuppliedWater,Is.EqualTo(missing).Within(.0001f));
        }
        [Test] public void RetryRestoresEntryWaterAndRemovesAttemptIncomeAndSupply()
        {
            var run=new FireRun();run.StartStage();run.Tick(.1f,true);Clear(run);run.StartStage();
            float entryWater=run.Water;int entryCoins=run.Coins;float entrySpent=run.ConsumedWater;
            run.ApplyWater(run.Trees[0].Position,100,.1f);run.Tick(.1f,true);
            Dock(run);run.Supply.SetValveOpen(true);run.Supply.Tick(.1f,true);
            Assert.Greater(run.Coins,entryCoins);Assert.Greater(run.SuppliedWater,0);
            Assert.IsTrue(run.RetryStage());Assert.AreEqual(1,run.Stage);Assert.AreEqual(entryCoins,run.Coins);
            Assert.AreEqual(entryWater,run.Water);Assert.Zero(run.SuppliedWater);Assert.AreEqual(entrySpent,run.ConsumedWater);
            Assert.IsFalse(run.Supply.Locked);Assert.AreEqual(5,run.Burning);
            Assert.IsTrue(run.RetryStage());Assert.AreEqual(entryWater,run.Water);Assert.AreEqual(entryCoins,run.Coins);
        }
        [Test] public void RetryCannotReplaySettledSectorForAnotherReward()
        {
            var run=new FireRun();Assert.IsFalse(run.RetryStage());run.StartStage();Clear(run);
            Assert.IsFalse(run.RetryStage());Assert.AreEqual(100,run.Coins);
        }
        [Test] public void InvalidTimeNeverTransfersAndPauseDoesNotCatchUp()
        {
            var run=ActiveDry();Dock(run);run.Supply.SetValveOpen(true);
            Assert.Zero(run.Supply.Tick(float.NaN,true));Assert.Zero(run.Supply.Tick(float.PositiveInfinity,true));Assert.Zero(run.Supply.Tick(-1,true));
            Assert.AreEqual(3,run.Supply.Tick(100,true));Assert.AreEqual(3,run.Water);
        }
        [Test] public void AssistedModeRecordsOnceAndCannotOverrideTrackingLoss()
        {
            var run=ActiveDry();Assert.IsFalse(run.Supply.BeginAssistedRefill());Dock(run);
            Assert.IsTrue(run.Supply.BeginAssistedRefill());Assert.IsFalse(run.Supply.BeginAssistedRefill());
            run.Supply.SetValveOpen(true);Assert.Zero(run.Supply.Tick(.1f,false));Assert.Zero(run.Water);
            Assert.IsFalse(run.Supply.Assisted);Assert.AreEqual(1,run.AssistedRefills);
        }
        [Test] public void AssistedSupplyPausesAllHazardClocksButNormalSupplyDoesNot()
        {
            var run=ActiveDry();Dock(run);float fuel=run.Trees[0].Fuel,elapsed=run.Elapsed;
            run.Tick(.1f,false);Assert.Less(run.Trees[0].Fuel,fuel);Assert.Greater(run.Elapsed,elapsed);
            fuel=run.Trees[0].Fuel;elapsed=run.Elapsed;run.Supply.BeginAssistedRefill();run.Supply.SetValveOpen(true);
            for(int i=0;i<20;i++){run.Tick(.1f,true);run.Supply.Tick(.1f,true);}
            Assert.AreEqual(fuel,run.Trees[0].Fuel);Assert.AreEqual(elapsed,run.Elapsed);Assert.Greater(run.Water,0);
            Assert.Zero(run.SprayAmount);
        }
        [TestCase(7.5f,false)] [TestCase(8f,true)] [TestCase(9f,true)] [TestCase(18f,true)]
        public void CannonCapsChargeToAvailableWaterWithoutSpendingOnCancellation(float units,bool canFire)
        {
            var run=new FireRun();for(int i=0;i<3;i++){run.StartStage();Clear(run);}
            Assert.IsTrue(run.BuyEquipment(FireEquipmentId.PressureCannon));run.Equip(FireEquipmentId.PressureCannon);run.StartStage();
            Dock(run);run.Supply.SetValveOpen(true);
            while(run.Water+0.00001f<units)run.Supply.Tick(Mathf.Min(.1f,(units-run.Water)/30),true);
            run.Supply.Disconnect();run.Tick(.02f,false);
            for(int i=0;i<15;i++)run.Tick(.1f,true);
            Assert.That(run.Charge01,Is.LessThanOrEqualTo(Mathf.Clamp01((units-8)/20)+.00001f));
            Assert.That(run.Water,Is.EqualTo(units).Within(.0001f));
            Assert.AreEqual(canFire,run.Tick(.02f,false));
            if(canFire)Assert.That(run.Water,Is.EqualTo(0).Within(.0001f));else Assert.AreEqual(units,run.Water);
            Assert.Zero(run.Charge01);
        }
        [Test] public void SupplyDockCancelsExistingCannonChargeAndCreekPumpCannotPrimeWeapon()
        {
            var run=new FireRun();for(int i=0;i<3;i++){run.StartStage();Clear(run);}
            run.BuyEquipment(FireEquipmentId.PressureCannon);run.Equip(FireEquipmentId.PressureCannon);run.StartStage();
            Dock(run);run.Supply.SetValveOpen(true);for(int i=0;i<20;i++)run.Supply.Tick(.1f,true);
            run.Supply.Disconnect();run.Tick(.02f,false);Assert.IsTrue(run.PrimeCannon(1));Assert.Greater(run.Charge01,0);
            Dock(run,WaterSourceKind.Creek);Assert.Zero(run.Charge01);Assert.IsFalse(run.PrimeCannon(1));
            run.Supply.PlaceIntake(true);Assert.AreEqual(12,Stroke(run.Supply));Assert.Zero(run.Charge01);
            float water=run.Water;run.Supply.Disconnect();Assert.IsFalse(run.Tick(.02f,false));Assert.AreEqual(water,run.Water);
        }
    }
}

using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    // GD-WATER-10/11. Domain clock is explicitly sampled; real scene gates and
    // tracking are covered by the production-root PlayMode suite.
    public sealed class EmberdeepWaterRecoveryTests
    {
        static FireRun Dry()
        {
            var run=new FireRun();run.StartStage();
            for(int i=0;i<100 && run.Water>0;i++)run.Tick(.1f,true);
            run.Tick(.01f,false);Assert.Zero(run.Water);return run;
        }
        static void Clear(FireRun run)
        {foreach(var tree in run.Trees)if(tree.State==TreeState.Burning)run.ApplyWater(tree.Position,100,.1f);}
        static void Recover(FireRun run,int frames,float dt=.1f)
        {for(int i=0;i<frames;i++)run.TickRecovery(dt,true,false);}
        static void Wait(FireRun run)
        {float before=run.Water;Recover(run,30);Assert.AreEqual(before,run.Water,"The three-second wait grants no water");}
        static void Dock(FireRun run)
        {for(int i=0;i<5;i++)run.Supply.SampleDock(WaterSourceKind.Hydrant,0,0,0,true,.05f);Assert.IsTrue(run.Supply.Locked);}
        static void Stroke(FireRun run)
        {run.Supply.SampleLever(0,true,true);run.Supply.SampleLever(.085f,true,true);run.Supply.SampleLever(0,true,true);}
        static void Balanced(FireRun run)
        {
            float total=0;foreach(var equipment in FireEquipment.All)total+=run.WaterFor(equipment.Id);
            Assert.That(total,Is.EqualTo(60+run.SuppliedWater+run.RecoveredWater-run.ConsumedWater).Within(.0002f));
        }

        [Test] public void ThreeSecondDelayThenTwentySecondsRestoresFiveFractionalUnits()
        {
            var run=Dry();Recover(run,29);Assert.Zero(run.Water);Assert.IsTrue(run.RecoveryWaiting);Assert.IsFalse(run.RecoveryActive);
            Assert.That(run.RecoveryDelayRemaining,Is.EqualTo(.1f).Within(.000001f));
            Assert.Zero(run.TickRecovery(.1f,true,false));Assert.Zero(run.Water);
            Assert.IsFalse(run.RecoveryWaiting);Assert.IsTrue(run.RecoveryActive);
            Recover(run,200);Assert.That(run.Water,Is.EqualTo(5).Within(.000001f));Assert.That(run.RecoveredWater,Is.EqualTo(5).Within(.000001f));
            Assert.That(run.TickRecovery(.004f,true,false),Is.EqualTo(.001f).Within(.000001f));
            Assert.Zero(run.SuppliedWater);Balanced(run);
        }
        [TestCase(.1f,230)] [TestCase(.05f,460)] [TestCase(.02f,1150)] [TestCase(1f/60,1380)]
        public void AcceptedFrameStepsHaveEquivalentRecovery(float dt,int frames)
        {
            var run=Dry();Recover(run,frames,dt);
            Assert.That(run.Water,Is.EqualTo(5).Within(.00001f));Assert.That(run.RecoveredWater,Is.EqualTo(5).Within(.00001f));Balanced(run);
        }
        [Test] public void FrameCrossingDelayCreditsOnlyItsEligibleRemainder()
        {
            var run=Dry();Recover(run,29);Assert.Zero(run.TickRecovery(.07f,true,false));
            Assert.That(run.TickRecovery(.1f,true,false),Is.EqualTo(.0175f).Within(.000001f));Balanced(run);
        }
        [Test] public void RecoveryEventuallyFillsCapacityWithoutOverflowOrActiveIndicator()
        {
            var run=Dry();Recover(run,2430);
            Assert.AreEqual(run.Capacity,run.Water);Assert.AreEqual(60,run.RecoveredWater);
            Assert.IsFalse(run.RecoveryActive);Assert.IsFalse(run.RecoveryWaiting);
            Assert.Zero(run.TickRecovery(1000,true,false));Assert.AreEqual(60,run.RecoveredWater);Balanced(run);
        }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(0)] [TestCase(-.1f)]
        public void InvalidElapsedTimeResetsDelayAndCannotCredit(float dt)
        {
            var run=Dry();Recover(run,29);Assert.Zero(run.TickRecovery(dt,true,false));
            Assert.AreEqual(3,run.RecoveryDelayRemaining);Assert.IsFalse(run.RecoveryWaiting);Assert.IsFalse(run.RecoveryActive);
            Wait(run);Assert.Zero(run.RecoveredWater);
        }
        [Test] public void CatchupTimeAcceptsOnlyOneBoundedIntervalAndBanksNothing()
        {
            var run=Dry();Assert.Zero(run.TickRecovery(1000,true,false));
            Assert.That(run.RecoveryDelayRemaining,Is.EqualTo(2.9f).Within(.000001f));Recover(run,29);Assert.Zero(run.Water);
            Assert.That(run.TickRecovery(1000,true,false),Is.EqualTo(.025f).Within(.000001f));
            Assert.That(run.TickRecovery(.1f,true,false),Is.EqualTo(.025f).Within(.000001f));
            Assert.That(run.Water,Is.EqualTo(.05f).Within(.000001f));
        }
        [Test] public void ExternalSuspensionRestartsDelayAndNeverCatchesUp()
        {
            var run=Dry();Wait(run);Recover(run,10);float before=run.Water;
            Assert.Zero(run.TickRecovery(10000,false,false));Assert.AreEqual(before,run.Water);
            Assert.IsFalse(run.RecoveryActive);Assert.IsFalse(run.RecoveryWaiting);Wait(run);
            Assert.That(run.TickRecovery(.1f,true,false),Is.EqualTo(.025f).Within(.000001f));
        }
        [Test] public void HeldDryTriggerIsBlockedEvenWhenNoShotWasProduced()
        {
            var run=Dry();
            for(int i=0;i<60;i++)
            {
                Assert.IsFalse(run.Tick(.1f,true));
                Assert.Zero(run.TickRecovery(.1f,true,false),"Domain remembers the held dry trigger even if a caller forgets the raw input");
            }
            run.CancelTrigger();for(int i=0;i<60;i++)Assert.Zero(run.TickRecovery(.1f,true,true));
            run.Tick(.01f,false);Wait(run);Recover(run,200);float before=run.Water,consumed=run.ConsumedWater;
            Assert.IsFalse(run.Tick(.1f,false));Assert.AreEqual(before,run.Water);Assert.AreEqual(consumed,run.ConsumedWater);
            Assert.Zero(run.SprayAmount);Assert.Zero(run.Charge01);
        }
        [Test] public void ChargeAndReleaseShotCannotShareARecoveryInterval()
        {
            var run=new FireRun();
            for(int i=0;i<3;i++){run.StartStage();Clear(run);}
            Assert.IsTrue(run.BuyEquipment(FireEquipmentId.PressureCannon));Assert.IsTrue(run.Equip(FireEquipmentId.PressureCannon));
            Dock(run);for(int i=0;i<4;i++)Stroke(run);run.Supply.Disconnect();run.StartStage();run.Tick(.1f,false);
            Assert.IsTrue(run.PrimeCannon(.5f));Assert.Greater(run.Charge01,0);
            Assert.Zero(run.TickRecovery(.1f,true,false));Assert.AreEqual(3,run.RecoveryDelayRemaining);
            Assert.IsTrue(run.Tick(.1f,false));Assert.Greater(run.SprayAmount,0);
            Assert.Zero(run.TickRecovery(.1f,true,false),"A cannon release shot is still weapon operation");
            run.Tick(.1f,false);Wait(run);Assert.Greater(run.TickRecovery(.1f,true,false),0);Balanced(run);
        }
        [Test] public void ManualSupplyAndDisconnectRestartDelayWithoutDoubleCredit()
        {
            var run=Dry();Wait(run);Recover(run,20);float recovered=run.RecoveredWater;
            Dock(run);Assert.AreEqual(3,run.RecoveryDelayRemaining);Stroke(run);
            for(int i=0;i<60;i++)Assert.Zero(run.TickRecovery(.1f,true,false));
            Assert.AreEqual(12,run.SuppliedWater);Assert.AreEqual(recovered,run.RecoveredWater);
            run.Supply.Disconnect();Assert.AreEqual(3,run.RecoveryDelayRemaining);Wait(run);
            Assert.That(run.TickRecovery(.1f,true,false),Is.EqualTo(.025f).Within(.000001f));Balanced(run);
        }
        [Test] public void ActiveOwnedLoadoutAloneRecoversAndSwitchesRestartWait()
        {
            var run=Dry();Clear(run);Assert.IsTrue(run.BuyEquipment(FireEquipmentId.PulsePistol));
            Wait(run);Recover(run,40);Assert.AreEqual(1,run.Water);Assert.Zero(run.WaterFor(FireEquipmentId.PulsePistol));
            Assert.IsTrue(run.Equip(FireEquipmentId.PulsePistol));Assert.Zero(run.Water);Wait(run);Recover(run,40);
            Assert.AreEqual(1,run.Water);Assert.AreEqual(1,run.WaterFor(FireEquipmentId.Extinguisher));
            for(int i=0;i<10;i++)
            {run.Equip(i%2==0?FireEquipmentId.Extinguisher:FireEquipmentId.PulsePistol);Recover(run,20);}
            Assert.AreEqual(1,run.WaterFor(FireEquipmentId.Extinguisher));Assert.AreEqual(1,run.WaterFor(FireEquipmentId.PulsePistol));Balanced(run);
        }
        [Test] public void PurchasesAndUpgradesGrantNoWaterAndResetWaitWithoutMultiplyingRate()
        {
            var run=Dry();Clear(run);Wait(run);Recover(run,40);float water=run.Water;
            Assert.IsTrue(run.BuyUpgrade());Assert.AreEqual(water,run.Water);Wait(run);Recover(run,200);
            Assert.That(run.Water-water,Is.EqualTo(5).Within(.000001f));Assert.AreEqual(85,run.Capacity);
            run.StartStage();Clear(run);Wait(run);water=run.Water;
            Assert.IsTrue(run.BuyEquipment(FireEquipmentId.PulsePistol));Assert.AreEqual(water,run.Water);Assert.Zero(run.WaterFor(FireEquipmentId.PulsePistol));Wait(run);
            Assert.IsTrue(run.BuyStationUpgrade());Assert.AreEqual(water,run.Water);Wait(run);Balanced(run);
        }
        [Test] public void RetryRestoresBothCreditsConsumptionAndAllTanksFromSameCheckpoint()
        {
            var run=Dry();Wait(run);Recover(run,40);Dock(run);Stroke(run);run.Supply.Disconnect();Clear(run);
            Assert.IsTrue(run.BuyEquipment(FireEquipmentId.PulsePistol));Assert.IsTrue(run.Equip(FireEquipmentId.PulsePistol));Wait(run);Recover(run,40);
            float foam=run.WaterFor(FireEquipmentId.Extinguisher),pistol=run.Water,supplied=run.SuppliedWater,recovered=run.RecoveredWater,consumed=run.ConsumedWater;
            run.StartStage();run.Tick(.1f,false);Wait(run);Recover(run,200);Dock(run);Stroke(run);run.Supply.Disconnect();run.Tick(.1f,false);run.Tick(.1f,true);run.CancelTrigger();
            Assert.Greater(run.RecoveredWater,recovered);Assert.Greater(run.SuppliedWater,supplied);Assert.Greater(run.ConsumedWater,consumed);
            for(int i=0;i<3;i++)
            {
                Assert.IsTrue(run.RetryStage());Assert.AreEqual(foam,run.WaterFor(FireEquipmentId.Extinguisher));Assert.AreEqual(pistol,run.Water);
                Assert.AreEqual(supplied,run.SuppliedWater);Assert.AreEqual(recovered,run.RecoveredWater);Assert.AreEqual(consumed,run.ConsumedWater);
                Wait(run);Balanced(run);
            }
            run.Restart();Assert.AreEqual(60,run.Water);Assert.Zero(run.RecoveredWater);Assert.Zero(run.SuppliedWater);Assert.Zero(run.ConsumedWater);
        }
        [Test] public void SectorAndOutcomeTransitionsRestartDelayAndTerminalPhasesBlockRecovery()
        {
            var ready=new FireRun();Recover(ready,100);Assert.Zero(ready.RecoveredWater);Assert.IsFalse(ready.RecoveryActive);
            var run=Dry();Wait(run);Clear(run);Assert.AreEqual(FirePhase.Cleared,run.Phase);Wait(run);
            Assert.Greater(run.TickRecovery(.1f,true,false),0);float before=run.Water;run.StartStage();Assert.AreEqual(before,run.Water);Wait(run);
            foreach(var tree in run.Trees)tree.State=TreeState.Burnt;run.Tick(.1f,false);Assert.AreEqual(FirePhase.Lost,run.Phase);
            Recover(run,100);Assert.AreEqual(before,run.Water);Assert.IsFalse(run.RecoveryWaiting);Assert.IsFalse(run.RecoveryActive);
            var won=Dry();for(int i=0;i<5;i++){Clear(won);if(i<4)won.StartStage();}
            Assert.AreEqual(FirePhase.Won,won.Phase);Recover(won,100);Assert.Zero(won.Water);Assert.Zero(won.RecoveredWater);
        }
    }
}

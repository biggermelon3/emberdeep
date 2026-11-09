using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    public sealed class FireEquipmentTests
    {
        static void Clear(FireRun run)
        {for(int i=0;i<run.Trees.Length;i++)if(run.Trees[i].State==TreeState.Burning)run.ApplyWater(run.Trees[i].Position,100,.1f);}
        static FireRun Supply(int rounds)
        {var run=new FireRun();for(int i=0;i<rounds;i++){run.StartStage();Clear(run);}return run;}
        [Test] public void ShopHasUnlocksAtomicCostsAndNoDuplicatePurchases()
        {
            var run=new FireRun();run.StartStage();Assert.AreEqual(FireEquipmentId.Extinguisher,run.Equipped);
            Assert.IsFalse(run.BuyEquipment(FireEquipmentId.PulsePistol));Assert.IsFalse(run.Equip(FireEquipmentId.PulsePistol));
            Clear(run);Assert.AreEqual(100,run.Coins);Assert.IsTrue(run.BuyEquipment(FireEquipmentId.PulsePistol));Assert.Zero(run.Coins);
            Assert.IsFalse(run.BuyEquipment(FireEquipmentId.PulsePistol));Assert.IsFalse(run.BuyEquipment(FireEquipmentId.PressureCannon));Assert.Zero(run.Coins);
            Assert.IsTrue(run.Owns(FireEquipmentId.Extinguisher));Assert.IsTrue(run.Equip(FireEquipmentId.PulsePistol));
            Assert.Zero(run.Water,"Purchased equipment starts empty");
        }
        [Test] public void NaturalRoundRewardsPayForTheEntireEquipmentRoute()
        {
            var run=new FireRun();
            for(int i=0;i<3;i++)
            {run.StartStage();Clear(run);Assert.IsTrue(run.BuyEquipment((FireEquipmentId)(i+1)),"Purchase after round "+(i+1));}
            Assert.AreEqual(75,run.Coins);for(int i=0;i<4;i++)Assert.IsTrue(run.Owns((FireEquipmentId)i));
        }
        [Test] public void SwitchingPreservesTankAndTuningAndCancelsCharge()
        {
            var run=Supply(3);Assert.IsTrue(run.BuyEquipment(FireEquipmentId.PressureCannon));Assert.IsTrue(run.BuyUpgrade());
            int foamLevel=run.Upgrade;run.StartStage();run.Tick(.1f,true);float foamWater=run.Water;
            run.Equip(FireEquipmentId.PressureCannon);Assert.Zero(run.Upgrade);ManualWaterTestSupport.Fill(run);run.Tick(.1f,true);Assert.Greater(run.Charge01,0);
            float cannonWater=run.Water;run.Equip(FireEquipmentId.Extinguisher);Assert.Zero(run.Charge01);Assert.AreEqual(foamWater,run.Water);Assert.AreEqual(foamLevel,run.Upgrade);
            run.Equip(FireEquipmentId.PressureCannon);run.Tick(.02f,false);Assert.Zero(run.SprayAmount);Assert.AreEqual(cannonWater,run.Water);
        }
        [Test] public void PulseAndChargeHaveDifferentTriggerBehaviorAndCancellationNeverFires()
        {
            var run=Supply(3);run.BuyEquipment(FireEquipmentId.PulsePistol);run.BuyEquipment(FireEquipmentId.PressureCannon);run.Equip(FireEquipmentId.PulsePistol);run.StartStage();ManualWaterTestSupport.Fill(run);
            Assert.IsTrue(run.Tick(.02f,true));float first=run.SprayAmount;Assert.IsFalse(run.Tick(.02f,true));Assert.Zero(run.SprayAmount);
            for(int i=0;i<12;i++)run.Tick(.02f,true);Assert.Less(run.Water,run.Capacity-3);
            run.Equip(FireEquipmentId.PressureCannon);ManualWaterTestSupport.Fill(run);for(int i=0;i<15;i++)run.Tick(.1f,true);
            Assert.Zero(run.SprayAmount);Assert.Greater(run.Charge01,.9f);Assert.IsTrue(run.Tick(.02f,false));Assert.Greater(run.SprayAmount,first*5);Assert.Greater(run.ShotRadius,3);
            for(int i=0;i<20;i++)run.Tick(.1f,true);run.CancelTrigger();Assert.IsFalse(run.Tick(.02f,false));Assert.Zero(run.SprayAmount);
        }
        [Test] public void FoamTemporarilyStopsSpreadAndRestartClearsInventory()
        {
            var run=Supply(1);run.BuyEquipment(FireEquipmentId.PulsePistol);run.StartStage();
            for(int i=0;i<run.Trees.Length;i++)if(run.Trees[i].State==TreeState.Safe)run.ApplyWater(run.Trees[i].Position,.01f,.1f,12);
            int initial=run.Burning;for(int i=0;i<95;i++)run.Tick(.1f,false);Assert.AreEqual(initial,run.Burning);
            for(int i=0;i<190;i++)run.Tick(.1f,false);Assert.Greater(run.Burning,initial);
            run.Restart();Assert.IsFalse(run.Owns(FireEquipmentId.PulsePistol));Assert.AreEqual(FireEquipmentId.Extinguisher,run.Equipped);Assert.Zero(run.Coins);
        }
        [TestCase(0)] [TestCase(1)] public void PhysicalPickupRequiresFreshPinchAndReturnRequiresMovingAway(int hand)
        {
            var grip=new FireEquipmentGrab();var dock=Vector3.zero;
            Assert.IsFalse(grip.Sample(hand,true,.01f,dock,dock,true,.02f));
            for(int i=0;i<6;i++)grip.Sample(hand,true,.04f,dock,dock,true,.02f);
            Assert.IsTrue(grip.Sample(hand,true,.01f,dock,dock,true,.02f));Assert.IsTrue(grip.Held);Assert.AreEqual(hand,grip.Hand);
            for(int i=0;i<20;i++)grip.Sample(hand,true,.04f,dock,dock,true,.02f);Assert.IsTrue(grip.Held,"Opening to arm trigger must not immediately holster");
            grip.Sample(hand,true,.04f,Vector3.forward*.3f,dock,true,.02f);
            for(int i=0;i<20;i++)grip.Sample(hand,true,.04f,dock,dock,true,.02f);Assert.IsFalse(grip.Held);
            grip.EquipForDesktop(hand);for(int i=0;i<20;i++)grip.Sample(hand,false,0,dock,dock,true,.02f);Assert.IsTrue(grip.Held,"System-menu tracking suppression retains ownership; presentation holds the last pose");
        }
    }
}


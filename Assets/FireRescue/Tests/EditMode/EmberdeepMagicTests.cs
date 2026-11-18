using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    public sealed class EmberdeepMagicTests
    {
        [Test] public void ProductionLessonHasOneFireAndHalfTankCanSaveIt()
        {
            EmberdeepSession.NewExpedition();var run=EmberdeepSession.Run;run.StartStage();
            Assert.True(run.IsPalmLesson);Assert.AreEqual(1,run.Trees.Length);Assert.AreEqual(1,run.Burning);
            Assert.AreEqual(run.Capacity/2,run.MagicCapacity);float tank=run.Water;
            for(int i=0;i<100 && run.Phase==FirePhase.Fighting;i++)
            {run.Tick(.05f,false);float amount=run.TickMagic(.05f,true,true);run.ApplyWater(run.Trees[0].Position,amount,run.MagicRadius,3);}
            Assert.AreEqual(FirePhase.Cleared,run.Phase);Assert.Greater(run.MagicWater,0);Assert.AreEqual(tank,run.Water);
            Assert.False(run.StartStage());run.EvacuateSafeAnimals();Assert.True(run.BuyMagicUpgrade());
            run.StartStage();Assert.True(run.IsPalmLesson);Assert.AreEqual(1,run.Stage);Assert.AreEqual(run.MagicCapacity,run.MagicWater);
        }
        [Test] public void MagicUsesItsOwnBudgetPausesAndCannotReceivePumpWater()
        {
            var run=new FireRun(true);run.StartStage();float amount=0;
            for(int i=0;i<30;i++)amount+=run.TickMagic(.1f,true,true);
            Assert.AreEqual(0,run.MagicWater);Assert.AreEqual(30,run.MagicConsumed,.001f);Assert.AreEqual(45*30/14f,amount,.001f);
            Assert.AreEqual(60,run.Water);Assert.AreEqual(0,run.SuppliedWater);
            run.TickMagic(20,false,false);run.TickMagic(float.NaN,false,true);Assert.AreEqual(0,run.MagicWater);
            run.TickMagic(20,false,true);Assert.AreEqual(.6f,run.MagicWater,.0001f,"No background catch-up");
            for(int i=0;i<1000;i++)run.TickMagic(.1f,false,true);
            Assert.AreEqual(run.MagicCapacity,run.MagicWater);
        }
        [Test] public void LessonWaitIsSafeAndRetryRestoresMagicAccounting()
        {
            var run=new FireRun(true);run.StartStage();
            for(int i=0;i<1800;i++)run.Tick(.1f,false);
            Assert.AreEqual(FirePhase.Fighting,run.Phase);Assert.AreEqual(0,run.AnimalsOnFire);Assert.AreEqual(0,run.Burnt);
            run.TickMagic(.1f,true,true);Assert.Greater(run.MagicConsumed,0);Assert.True(run.RetryStage());
            Assert.Greater(run.MagicConsumed,0);Assert.AreEqual(run.MagicCapacity,run.MagicWater);
        }
        [Test] public void UpgradeIsPaidSupplyOnlyAndImprovesDurationWithoutManualRefill()
        {
            var run=new FireRun(true);run.StartStage();Assert.False(run.BuyMagicUpgrade());
            run.ApplyWater(run.Trees[0].Position,100,100);run.EvacuateSafeAnimals();
            Assert.True(run.CanUpgradeMagic);int coins=run.Coins,cost=run.MagicUpgradeCost;
            Assert.True(run.BuyMagicUpgrade());Assert.AreEqual(coins-cost,run.Coins);Assert.AreEqual(50,run.MagicCapacity);
            Assert.AreEqual(30,run.MagicWater,"Purchasing capacity does not refill magic");
            run.StartStage();Assert.AreEqual(50,run.MagicWater);run.TickMagic(.1f,true,true);run.RetryStage();Assert.AreEqual(1,run.MagicLevel);Assert.AreEqual(50,run.MagicWater);
            run.Restart();Assert.AreEqual(0,run.MagicLevel);Assert.AreEqual(30,run.MagicWater);
        }
        [Test] public void PalmStackRequiresGroundOpenFingersBothTrackedAndDwell()
        {
            var go=new GameObject("Magic gesture arena");go.transform.localScale=Vector3.one*.08f;
            try
            {
                var gesture=new EmberdeepPalmGesture();var rotation=Quaternion.Euler(180,0,0);
                var left=new FireToolManipulation.Hand{Valid=true,HasGripFrame=true,GripRotation=rotation,Contact=new Vector3(0,.4f,.3f),Palm=new Pose(new Vector3(0,.4f,.3f),rotation)};
                var tips=new Vector3[5];tips[1]=left.Contact+Vector3.up*.02f;int mask=2;
                gesture.Sample(left,tips,mask,true,true,go.transform,1.12f,.08f);Assert.True(gesture.Aiming);Assert.False(gesture.Casting);
                gesture.Sample(left,tips,mask,true,true,go.transform,1.12f,.08f);Assert.True(gesture.Casting);Assert.AreEqual(0,gesture.Target.y,.0001f);
                mask=0;gesture.Sample(left,tips,mask,true,true,go.transform,1.12f,.08f);Assert.False(gesture.Casting);Assert.True(gesture.Aiming);
                mask=2;gesture.Sample(left,tips,mask,true,true,go.transform,1.12f,.08f);Assert.False(gesture.Casting,"Reacquisition needs fresh dwell");
                gesture.Sample(left,tips,mask,false,true,go.transform,1.12f,.08f);Assert.False(gesture.Aiming);
                gesture.Sample(left,tips,mask,true,false,go.transform,1.12f,.08f);Assert.False(gesture.Aiming);
                left.GripRotation=Quaternion.identity;gesture.Sample(left,tips,mask,true,true,go.transform,1.12f,.08f);Assert.False(gesture.Aiming,"Upward palm must not target behind itself");
            }
            finally{Object.DestroyImmediate(go);}
        }
    }
}

using System;
using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    public sealed class FireTacticsTests
    {
        static void Clear(FireRun run)
        {
            for(int i=0;i<run.Trees.Length;i++)
                if(run.Trees[i].State==TreeState.Burning)run.ApplyWater(run.Trees[i].Position,100,.1f);
        }
        static FireRun Incident(int stage)
        {
            var run=new FireRun();run.StartStage();
            for(int i=0;i<stage;i++){Clear(run);run.StartStage();}
            return run;
        }
        static int Find(FireRun run,FirePropKind kind)
        {return Array.FindIndex(run.Props,p=>p.Kind==kind);}

        [Test] public void EveryIncidentAddsReadableTargetsWithoutBlockingTreeCenters()
        {
            var run=Incident(0);Assert.IsEmpty(run.Props);
            string previous=run.Objective;
            for(int stage=1;stage<5;stage++)
            {
                Clear(run);Assert.IsTrue(run.StartStage());
                Assert.AreNotEqual(previous,run.Objective);previous=run.Objective;
                Assert.IsNotEmpty(run.Hint);Assert.GreaterOrEqual(Find(run,FirePropKind.Reservoir),0);
                Assert.AreEqual(stage<2?0:stage==4?2:1,run.CabinsTotal);
                foreach(var prop in run.Props)
                {
                    Assert.That(Mathf.Abs(prop.Position.x),Is.LessThan(5));
                    foreach(var tree in run.Trees)
                        Assert.Greater(Vector3.Distance(prop.Position,tree.Position),.8f,"Separate collision targets at stage "+stage);
                }
            }
        }

        [Test] public void TowerNeedsAFullDirectHitBudgetThenRainsOnceAndCannotChargeOtherTowers()
        {
            var run=Incident(3);int index=Find(run,FirePropKind.Reservoir);var tower=run.Props[index];
            int activations=0;run.PropActivated+=i=>activations++;
            run.ApplyWater(tower.Position,100,.1f);
            Assert.Zero(tower.Charge01,"Splash damage does not fill a tower without a direct prop collision");
            run.ApplyPropWater(index,35);Assert.That(tower.Charge01,Is.EqualTo(.5f).Within(.001f));Assert.Zero(activations);
            run.ApplyPropWater(index,35);Assert.AreEqual(1,activations);Assert.IsTrue(tower.Used);
            Assert.AreEqual(4,tower.ActiveRemaining);
            int saved=run.Rescued;
            for(int i=0;i<41;i++)run.Tick(.1f,false);
            Assert.GreaterOrEqual(run.Rescued-saved,2,"A tower rewards choosing a burning cluster");
            Assert.Zero(tower.ActiveRemaining);
            run.ApplyPropWater(index,1000);Assert.AreEqual(1,activations);Assert.Zero(tower.ActiveRemaining);
            for(int i=0;i<run.Props.Length;i++)if(i!=index && run.Props[i].Kind==FirePropKind.Reservoir)Assert.Zero(run.Props[i].Charge01);
        }

        [Test] public void CabinThreatCanBeCooledAndLostCabinCannotBeResurrected()
        {
            var run=Incident(2);int index=Find(run,FirePropKind.Cabin);var cabin=run.Props[index];
            // Make the nearest tree a known active threat, independent of the level seed.
            int closest=0;for(int i=1;i<run.Trees.Length;i++)
                if(Vector3.Distance(run.Trees[i].Position,cabin.Position)<Vector3.Distance(run.Trees[closest].Position,cabin.Position))closest=i;
            run.Trees[closest].State=TreeState.Burning;run.Trees[closest].Heat=100;
            run.Tick(.1f,false);Assert.Less(cabin.Health01,1);
            run.ApplyPropWater(index,10);float protectedHealth=cabin.Health01;
            run.Tick(.1f,false);Assert.AreEqual(protectedHealth,cabin.Health01);
            cabin.Wetness=0;cabin.Health01=.0001f;int lost=0;run.CabinLost+=i=>lost++;
            run.Tick(.1f,false);Assert.Zero(cabin.Health01);Assert.AreEqual(1,lost);
            run.ApplyPropWater(index,1000);run.Tick(.1f,false);Assert.Zero(cabin.Health01);Assert.AreEqual(1,lost);
            Clear(run);Assert.AreEqual(FirePhase.Cleared,run.Phase,"Optional cabin loss must not lock progression");
            Assert.Zero(run.StageBonus);
        }

        [Test] public void ProtectedCabinPaysOnceAndDoesNotReplaceTreeRewards()
        {
            var run=Incident(2);int before=run.Coins;int fires=run.Burning;
            Clear(run);Assert.AreEqual(before+fires*25+75+FireRun.CabinReward,run.Coins);
            Assert.AreEqual(FireRun.CabinReward,run.StageBonus);
            int paid=run.Coins;Clear(run);run.Tick(.1f,false);run.ApplyPropWater(0,1000);
            Assert.AreEqual(paid,run.Coins);Assert.AreEqual(3,run.Stars);Assert.AreEqual(3,run.IncidentStars[2]);
        }

        [Test] public void ComboRewardsTimingWithoutInflatingCashAndResetsAfterThreeSeconds()
        {
            var run=Incident(0);run.ApplyWater(run.Trees[0].Position,100,.1f);run.ApplyWater(run.Trees[1].Position,100,.1f);
            Assert.AreEqual(2,run.Combo);Assert.AreEqual(2,run.BestCombo);Assert.AreEqual(50,run.Coins);Assert.AreEqual(220,run.Score);
            for(int i=0;i<31;i++)run.Tick(.1f,false);
            Assert.Zero(run.Combo);run.ApplyWater(run.Trees[2].Position,100,.1f);
            Assert.AreEqual(1,run.Combo);Assert.AreEqual(2,run.BestCombo);Assert.AreEqual(100,run.Coins);Assert.AreEqual(1070,run.Score);
            run.StartStage();Assert.Zero(run.Combo);Assert.Zero(run.Stars);Assert.AreEqual(3,run.IncidentStars[0]);
            run.Restart();Assert.Zero(run.Score);Assert.Zero(run.BestCombo);Assert.IsEmpty(run.Props);
            Assert.That(run.IncidentStars,Is.All.Zero);
        }

        [Test] public void LateImperfectClearStillProgressesWithOneStar()
        {
            var run=Incident(1);for(int i=0;i<510;i++)run.Tick(.1f,false);
            run.Trees[0].State=TreeState.Burnt;Clear(run);
            Assert.AreEqual(FirePhase.Cleared,run.Phase);Assert.AreEqual(1,run.Stars);Assert.IsTrue(run.StartStage());
        }

        [Test] public void PausedOrInvalidTimeCannotAdvanceTacticsAndInvalidHitsCannotEarnRewards()
        {
            var run=Incident(2);run.ApplyPropWater(0,70);float time=run.Props[0].ActiveRemaining,health=run.Props[1].Health01;
            run.Tick(0,false);run.Tick(float.NaN,false);run.Tick(float.PositiveInfinity,false);run.Tick(-1,false);
            Assert.Zero(run.Elapsed);Assert.AreEqual(time,run.Props[0].ActiveRemaining);Assert.AreEqual(health,run.Props[1].Health01);
            int money=run.Coins;run.ApplyWater(new Vector3(float.NaN,0,0),1000);
            run.ApplyWater(Vector3.zero,float.PositiveInfinity,100);run.ApplyPropWater(-1,100);run.ApplyPropWater(1,float.NaN);
            Assert.AreEqual(money,run.Coins);Assert.AreEqual(health,run.Props[1].Health01);
            run.Tick(100,false);Assert.That(run.Elapsed,Is.EqualTo(.1f).Within(.0001f));
        }

        [Test] public void PhysicalCannonPumpNeedsTravelAndUsesNormalReleaseAndCancelRules()
        {
            var run=Incident(2);Clear(run);Assert.IsTrue(run.BuyEquipment(FireEquipmentId.PressureCannon));
            Assert.IsFalse(run.PrimeCannon(1));run.Equip(FireEquipmentId.PressureCannon);run.StartStage();ManualWaterTestSupport.Fill(run);
            for(int i=0;i<20;i++)run.Tick(.1f,true,true);
            Assert.Zero(run.Charge01,"Holding the pump without pulling does not charge automatically");
            Assert.IsFalse(run.Tick(.1f,false));
            Assert.IsTrue(run.PrimeCannon(.7f));run.Tick(.02f,true,true);Assert.That(run.Charge01,Is.EqualTo(.7f).Within(.0001f));
            Assert.Zero(run.SprayAmount);Assert.IsTrue(run.Tick(.02f,false));Assert.Greater(run.SprayAmount,100);
            Assert.IsFalse(run.PrimeCannon(1),"Cannot bypass the shot cooldown with pump motion");
            for(int i=0;i<40;i++)run.Tick(.02f,false);
            Assert.IsTrue(run.PrimeCannon(1));run.CancelTrigger();Assert.IsFalse(run.Tick(.02f,false));
            Assert.IsFalse(run.PrimeCannon(float.NaN));Assert.Zero(run.Charge01);
        }
    }
}

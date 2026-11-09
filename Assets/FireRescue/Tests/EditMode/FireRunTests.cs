using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    // Campaign and weapon regressions obtain water through the shipping supply
    // rules. They must not quietly reintroduce idle regeneration in their setup.
    internal static class ManualWaterTestSupport
    {
        public static void Fill(FireRun run)
        {
            float before=run.Water, supplied=run.SuppliedWater;
            if(before>=run.Capacity)return;
            var supply=run.Supply;
            for(int frame=0;frame<6 && !supply.Locked;frame++)
            {
                supply.SampleDock(WaterSourceKind.Hydrant,1,0,0,true,.05f);
                run.Tick(.05f,false);
            }
            Assert.IsTrue(supply.Locked,"Refill setup must complete physical alignment");
            Assert.AreEqual(WaterStationTier.LeverWell,run.StationTier);
            for(int frame=0;frame<200 && run.Water<run.Capacity;frame++)
            {
                supply.SampleLever(0,true,true);supply.SampleLever(.085f,true,true);supply.SampleLever(0,true,true);
                run.Tick(.1f,false); // Ordinary refilling does not suspend fire clocks.
            }
            Assert.AreEqual(run.Capacity,run.Water,"Reachable free hydrant fills the tank");
            Assert.That(run.SuppliedWater-supplied,Is.EqualTo(run.Water-before).Within(.001f));
            supply.Disconnect();
            run.Tick(.02f,false); // Domain release gate; hand gesture tests cover stable opening.
        }
    }

    public sealed class FireRunTests
    {
        [Test] public void HandTrackingLossAndMenuRequireFreshOpenThenPinch()
        {
            var input=new FireSprayGesture();
            Assert.IsFalse(input.Sample(true,.01f,false,.02f),"Entering with a closed hand must not spray");
            for(int i=0;i<6;i++)input.Sample(true,.04f,false,.02f);
            Assert.IsTrue(input.Sample(true,.01f,false,.02f));
            Assert.IsTrue(input.Sample(true,.01f,false,.02f));
            Assert.IsFalse(input.Sample(false,.01f,false,.02f));
            Assert.IsFalse(input.Sample(true,.01f,false,.02f),"Tracking recovery with held pinch");
            for(int i=0;i<6;i++)input.Sample(true,.04f,false,.02f);
            Assert.IsTrue(input.Sample(true,.01f,false,.02f));
            Assert.IsFalse(input.Sample(true,.01f,true,.02f));
            Assert.IsFalse(input.Sample(true,.01f,false,.02f),"Leaving a menu with held pinch");
            for(int i=0;i<6;i++)input.Sample(true,.04f,false,.02f);
            Assert.IsTrue(input.Sample(true,.01f,false,.02f));
            Assert.IsFalse(input.Sample(true,.04f,false,.02f),"Release immediately stops flow");
        }
        [TestCase(1.65f,0f)] [TestCase(1.15f,73f)]
        public void MiniatureFitsTableAndHandAimExtinguishesAtEitherHeight(float eyeHeight,float yaw)
        {
            var go=new GameObject("Miniature test");
            try
            {
                var arena=go.transform;var head=new Vector3(2,eyeHeight,-3);
                var forward=Quaternion.Euler(0,yaw,0)*Vector3.forward;
                FireMiniature.Place(arena,head,forward,0);
                var center=arena.TransformPoint(new Vector3(0,0,6));
                Assert.That(center.y,Is.EqualTo(eyeHeight-.65f).Within(.001f));
                Assert.That(Vector3.Distance(new Vector3(head.x,center.y,head.z),center),Is.EqualTo(.8f).Within(.001f));
                Assert.That(arena.TransformVector(Vector3.right*11).magnitude,Is.LessThan(1f));
                Assert.That(arena.TransformVector(Vector3.forward*13).magnitude,Is.LessThan(1.1f));
                var run=new FireRun();run.StartStage();
                foreach(bool left in new[]{false,true})
                {
                    ManualWaterTestSupport.Fill(run);
                    var target=arena.TransformPoint(run.Trees[left?0:2].Position);
                    var palm=new Pose(head+arena.rotation*new Vector3(left?-.2f:.2f,-.25f,.25f),Quaternion.identity);
                    var pointerOrigin=palm.position+arena.rotation*new Vector3(.03f,.06f,-.08f);
                    var pointer=new Pose(pointerOrigin,Quaternion.LookRotation(target-pointerOrigin));
                    var ray=FireMiniature.FromHand(arena,palm,pointer);
                    var delta=target-ray.origin;
                    Assert.That(Vector3.Cross(delta,ray.direction).magnitude,Is.LessThan(.001f),"Pointer and barrel converge on the same miniature target");
                    var jet=new WaterJet();var targets=new Vector3[run.Trees.Length];
                    for(int j=0;j<targets.Length;j++)targets[j]=arena.TransformPoint(run.Trees[j].Position);
                    var velocity=WaterJet.AimVelocity(ray.origin,target,3.2f);
                    for(int i=0;i<35;i++){run.Tick(.1f,true);jet.Step(.1f,ray.origin,velocity,run.SprayAmount,run.Range*.08f,arena,targets,hit=>run.ApplyWater(arena.InverseTransformPoint(hit.Point),hit.Amount));}
                    Assert.AreEqual(TreeState.Saved,run.Trees[left?0:2].State);
                }
            }
            finally { Object.DestroyImmediate(go); }
        }
        static void Hit(FireRun run,int i){run.Tick(.1f,true);run.ApplyWater(run.Trees[i].Position,run.SprayAmount);}
        static void PutOut(FireRun run,int i)
        {
            for(int step=0;step<500 && run.Trees[i].State==TreeState.Burning && run.Phase==FirePhase.Fighting;step++)
            {
                if(run.Water<.1f)ManualWaterTestSupport.Fill(run);
                Hit(run,i);
            }
            Assert.AreEqual(TreeState.Saved,run.Trees[i].State);
        }
        [Test] public void ExtinguishingPaysExactlyOnce()
        {
            var run=new FireRun();run.StartStage();PutOut(run,0);Assert.AreEqual(25,run.Coins);Assert.AreEqual(1,run.Rescued);
            for(int i=0;i<10;i++)Hit(run,0);Assert.AreEqual(25,run.Coins);
        }
        [Test] public void MissingAndOutOfRangeSpraysConsumeWaterButDoNotDamage()
        {
            var run=new FireRun();run.StartStage();run.Tick(.1f,true);
            run.ApplyWater(new Vector3(0,1,-100),run.SprayAmount);
            Assert.Less(run.Water,run.Capacity);foreach(var t in run.Trees)Assert.AreEqual(100,t.Heat);Assert.Zero(run.Coins);
        }
        [Test] public void EmptyTankStaysEmptyUntilDeliberateHydrantRefill()
        {
            var run=new FireRun();run.StartStage();for(int i=0;i<60;i++)run.Tick(.1f,true);
            Assert.AreEqual(0,run.Water);Assert.IsFalse(run.Tick(.1f,true));Assert.AreEqual(0,run.Water);
            for(int i=0;i<40;i++)run.Tick(.1f,false);
            Assert.Zero(run.Water,"Releasing or waiting cannot grant water");
            ManualWaterTestSupport.Fill(run);Assert.AreEqual(run.Capacity,run.Water);
        }
        [Test] public void UpgradeIsAtomicAndIncreasesAllNozzleStats()
        {
            var run=new FireRun();run.StartStage();Assert.IsFalse(run.BuyUpgrade());Assert.Zero(run.Upgrade);
            for(int i=0;i<3;i++)PutOut(run,i);Assert.AreEqual(100,run.Coins);
            float power=run.Power,range=run.Range,width=run.Width,capacity=run.Capacity,water=run.Water;
            Assert.IsTrue(run.BuyUpgrade());Assert.AreEqual(25,run.Coins);Assert.AreEqual(1,run.Upgrade);
            Assert.Greater(run.Power,power);Assert.Greater(run.Range,range);Assert.Greater(run.Width,width);Assert.Greater(run.Capacity,capacity);
            Assert.AreEqual(water,run.Water,"Tuning changes capacity without granting water");Assert.IsFalse(run.BuyUpgrade());Assert.AreEqual(25,run.Coins);
        }
        [Test] public void FiveIncidentCampaignIsWinnableWithEarnedUpgrades()
        {
            var run=new FireRun();run.StartStage();
            for(int stage=0;stage<5;stage++)
            {
                for(int i=0;i<run.Trees.Length;i++)if(run.Trees[i].State==TreeState.Burning)PutOut(run,i);
                // A spread may ignite an earlier tree while later rows are being extinguished.
                for(int pass=0;pass<5 && run.Phase==FirePhase.Fighting;pass++)for(int i=0;i<run.Trees.Length;i++)if(run.Trees[i].State==TreeState.Burning)PutOut(run,i);
                Assert.AreEqual(stage==4?FirePhase.Won:FirePhase.Cleared,run.Phase,"Stage "+stage);
                while(run.CanUpgrade)run.BuyUpgrade();
                if(stage<4)Assert.IsTrue(run.StartStage());
            }
            Assert.AreEqual(FirePhase.Won,run.Phase);Assert.IsFalse(run.StartStage());
        }
        [Test] public void IdleFiresSpreadAndEventuallyLoseWithoutReward()
        {
            var run=new FireRun();run.StartStage();for(int i=0;i<3;i++)PutOut(run,i);run.StartStage();
            int initial=run.Burning;for(int i=0;i<100;i++)run.Tick(.1f,false);Assert.Greater(run.Burning,initial);
            int money=run.Coins;for(int i=0;i<1600 && run.Phase==FirePhase.Fighting;i++)run.Tick(.1f,false);
            Assert.AreEqual(FirePhase.Lost,run.Phase);Assert.AreEqual(money,run.Coins);Assert.IsFalse(run.StartStage());
        }
        [Test] public void RestartClearsProgressAndReentryCannotSkipAnActiveIncident()
        {
            var run=new FireRun();run.StartStage();PutOut(run,0);Assert.IsFalse(run.StartStage());
            run.Restart();Assert.Zero(run.Coins);Assert.Zero(run.Rescued);Assert.Zero(run.Upgrade);Assert.Zero(run.Stage);Assert.IsTrue(run.StartStage());Assert.AreEqual(3,run.Burning);
        }
    }
}

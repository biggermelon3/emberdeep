using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    public sealed class WaterJetTests
    {
        GameObject board;
        [SetUp] public void Setup(){board=new GameObject("Water test board");board.transform.localScale=Vector3.one*.08f;}
        [TearDown] public void Cleanup()=>Object.DestroyImmediate(board);
        [Test] public void GravityBendsJetAndReleaseKeepsExistingWaterInFlight()
        {
            var jet=new WaterJet();var origin=new Vector3(0,.4f,0);int hits=0;Vector3 landing=default;
            System.Action<WaterJet.Impact> hit=p=>{hits++;landing=p.Point;};
            jet.Step(.02f,origin,Vector3.forward*2,10,2,board.transform,System.Array.Empty<Vector3>(),hit);
            Assert.Zero(hits,"Water cannot damage at emission time");Assert.Greater(jet.Count,0);
            for(int i=0;i<5;i++)jet.Step(.02f,default,default,0,2,board.transform,System.Array.Empty<Vector3>(),hit);
            Assert.Greater(jet.Count,0);Assert.Less(jet.Packets[0].Position.y,origin.y-.03f);Assert.Zero(hits);
            for(int i=0;i<30;i++)jet.Step(.02f,default,default,0,2,board.transform,System.Array.Empty<Vector3>(),hit);
            Assert.Greater(hits,0);Assert.That(landing.y,Is.EqualTo(0).Within(.001f));Assert.That(landing.z,Is.InRange(.55f,.6f));Assert.Zero(jet.Count);
        }
        [Test] public void PredictedImpactMatchesSweptCollisionAndDoesNotPierceFrontTree()
        {
            var jet=new WaterJet();var origin=new Vector3(0,.3f,0);var target=new Vector3(0,.056f,.45f);
            var targets=new[]{target,target+Vector3.forward*.1f};var velocity=WaterJet.AimVelocity(origin,target,3.2f);
            Assert.IsTrue(WaterJet.Predict(origin,velocity,2,board.transform,targets,out var prediction));Assert.Zero(prediction.Tree);
            int hits=0;float amount=0;
            System.Action<WaterJet.Impact> hit=p=>{hits++;amount+=p.Amount;Assert.Zero(p.Tree);Assert.Less(Vector3.Distance(prediction.Point,p.Point),.002f);};
            jet.Step(.02f,origin,velocity,5,2,board.transform,targets,hit);
            Assert.Zero(hits);
            for(int i=0;i<50;i++)jet.Step(.02f,default,default,0,2,board.transform,targets,hit);
            Assert.Greater(hits,0);Assert.That(amount,Is.EqualTo(5).Within(.001f));Assert.Zero(jet.Count);
        }
        [Test] public void FireOnlyCoolsOnArrivalAndSteamStateDecaysAfterRelease()
        {
            var run=new FireRun();run.StartStage();var jet=new WaterJet();var targets=new Vector3[3];
            for(int i=0;i<3;i++)targets[i]=board.transform.TransformPoint(run.Trees[i].Position);
            var origin=new Vector3(0,.3f,-.1f);var velocity=WaterJet.AimVelocity(origin,targets[1],3.2f);
            run.Tick(.02f,true);jet.Step(.02f,origin,velocity,run.SprayAmount,2,board.transform,targets,h=>run.ApplyWater(board.transform.InverseTransformPoint(h.Point),h.Amount));
            Assert.AreEqual(100,run.Trees[1].Heat);Assert.Zero(run.Trees[1].Wetness);
            for(int i=0;i<20;i++){run.Tick(.02f,false);jet.Step(.02f,default,default,0,2,board.transform,targets,h=>run.ApplyWater(board.transform.InverseTransformPoint(h.Point),h.Amount));}
            Assert.Less(run.Trees[1].Heat,100);Assert.Greater(run.Trees[1].Wetness,.1f);
            float heat=run.Trees[1].Heat;for(int i=0;i<100;i++)run.Tick(.02f,false);
            Assert.Zero(run.Trees[1].Wetness);Assert.AreEqual(heat,run.Trees[1].Heat);
        }
        [Test] public void ExpiredWaterCannotHitOutsideItsRange()
        {
            var jet=new WaterJet();int hits=0;
            jet.Step(.02f,new Vector3(0,.5f,0),Vector3.forward*3,1,.1f,board.transform,System.Array.Empty<Vector3>(),p=>hits++);
            for(int i=0;i<20;i++)jet.Step(.02f,default,default,0,.1f,board.transform,System.Array.Empty<Vector3>(),p=>hits++);
            Assert.Zero(jet.Count);Assert.Zero(hits);
        }
        [Test] public void WholeCampaignCanBeExtinguishedWithArcsFromOneStandingPosition()
        {
            var run=new FireRun();run.StartStage();var origin=new Vector3(0,.4f,-.12f);
            for(int stage=0;stage<5;stage++)
            {
                var targets=new Vector3[run.Trees.Length];for(int i=0;i<targets.Length;i++)targets[i]=board.transform.TransformPoint(run.Trees[i].Position);
                var jet=new WaterJet();
                for(int pass=0;pass<5 && run.Phase==FirePhase.Fighting;pass++)
                for(int i=0;i<targets.Length && run.Phase==FirePhase.Fighting;i++)
                {
                    var velocity=WaterJet.AimVelocity(origin,targets[i],3.2f+run.Upgrade*.25f);
                    for(int frame=0;frame<400 && run.Trees[i].State==TreeState.Burning;frame++)
                    {
                        bool spray=run.Water>1;
                        if(!spray)ManualWaterTestSupport.Fill(run);
                        run.Tick(.02f,true);
                        jet.Step(.02f,origin,velocity,run.SprayAmount,run.Range*.08f,board.transform,targets,h=>run.ApplyWater(board.transform.InverseTransformPoint(h.Point),h.Amount));
                    }
                }
                Assert.AreEqual(stage==4?FirePhase.Won:FirePhase.Cleared,run.Phase,"Ballistic stage "+stage);
                while(run.CanUpgrade)run.BuyUpgrade();if(stage<4)run.StartStage();
            }
        }
        [Test] public void FanReachesSeparateTreesAndInFlightWaterKeepsOriginalEquipmentStats()
        {
            var jet=new WaterJet();var origin=new Vector3(0,.3f,0);
            var targets=new[]{new Vector3(-.094f,.056f,.45f),new Vector3(0,.056f,.45f),new Vector3(.094f,.056f,.45f)};
            var trees=new System.Collections.Generic.HashSet<int>();float total=0;
            System.Action<WaterJet.Impact> impact=h=>{if(h.Tree>=0)trees.Add(h.Tree);total+=h.Amount;Assert.AreEqual(2.1f,h.Radius);Assert.AreEqual(3,h.Wetness);};
            jet.Step(.1f,origin,WaterJet.AimVelocity(origin,targets[1],3.2f),100,2,board.transform,targets,impact,2.1f,3,14,FireDelivery.Fan);
            for(int i=0;i<30;i++)jet.Step(.02f,default,default,0,.01f,board.transform,targets,impact,.01f,0);
            Assert.AreEqual(3,trees.Count);Assert.That(total,Is.EqualTo(100).Within(.01f));Assert.Zero(jet.Count);
        }
    }
}

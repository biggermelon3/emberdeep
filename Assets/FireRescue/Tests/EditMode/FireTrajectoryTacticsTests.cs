using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    public sealed class FireTrajectoryTacticsTests
    {
        GameObject board;
        [SetUp] public void SetUp(){board=new GameObject("Tactical trajectory board");}
        [TearDown] public void TearDown(){Object.DestroyImmediate(board);}

        static FireRun Incident(int stage)
        {
            var run=new FireRun();run.StartStage();
            for(int s=0;s<stage;s++)
            {
                for(int i=0;i<run.Trees.Length;i++)if(run.Trees[i].State==TreeState.Burning)run.ApplyWater(run.Trees[i].Position,100,.1f);
                run.StartStage();
            }
            return run;
        }
        Vector3[] Targets(FireRun run)
        {
            var result=new Vector3[run.Trees.Length+run.Props.Length];
            for(int i=0;i<run.Trees.Length;i++)result[i]=board.transform.TransformPoint(run.Trees[i].Position);
            for(int i=0;i<run.Props.Length;i++)result[run.Trees.Length+i]=board.transform.TransformPoint(run.Props[i].Position);
            return result;
        }
        // The physical model's muzzle-minus-grip anchors, in metres. These tests
        // use the same coupled muzzle/trajectory solver as the desktop aiming path.
        static Vector3 Offset(FireEquipmentId id)
        {
            if(id==FireEquipmentId.Extinguisher)return new Vector3(0,-.07f,.205f);
            return new Vector3(0,.055f,id==FireEquipmentId.PressureCannon?.275f:id==FireEquipmentId.RainFan?.22f:.195f);
        }
        static void Launch(Vector3 palm,Vector3 target,FireEquipment equipment,out Vector3 origin,out Vector3 velocity)
        {
            var offset=Offset(equipment.Id);velocity=WaterJet.AimFromGrip(palm,target,equipment.Speed,offset);
            origin=palm+Quaternion.LookRotation(velocity)*offset;
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void DesktopMouseCanFirstHitEachTacticalTargetWithEveryUnlockedTool(int stage)
        {
            var run=Incident(stage);var head=new Vector3(0,1.65f,0);var cameraRotation=Quaternion.Euler(55,0,0);
            FireMiniature.Place(board.transform,head,cameraRotation*Vector3.forward,0);
            var targets=Targets(run);var palm=head+cameraRotation*FireMiniature.DesktopGripLocal;
            var failures=new List<string>();
            foreach(var equipment in FireEquipment.All)
            {
                if(equipment.Unlock>stage)continue;
                for(int prop=0;prop<run.Props.Length;prop++)
                {
                    int expected=run.Trees.Length+prop;var visibleTarget=targets[expected];
                    var cursorRay=new Ray(head,(visibleTarget-head).normalized);
                    var target=WaterJet.MouseTarget(cursorRay,board.transform,targets);
                    Launch(palm,target,equipment,out var origin,out var velocity);
                    bool predicted=WaterJet.Predict(origin,velocity,equipment.Range*FireMiniature.Scale,board.transform,targets,out var impact);
                    if(!predicted || impact.Tree!=expected)
                        failures.Add($"stage={stage+1} tool={equipment.Id} prop={prop}/{run.Props[prop].Kind} first={(predicted?impact.Tree:-999)} expected={expected} target={visibleTarget} mouseTarget={target} muzzle={origin}");
                }
            }
            Assert.IsEmpty(failures,string.Join("\n",failures));
        }

        [TestCase(1.65f,0f)] [TestCase(1.15f,73f)]
        public void EveryTacticalTargetHasAComfortableStandingOrSeatedHandTrajectory(float eyeHeight,float yaw)
        {
            var head=new Vector3(2,eyeHeight,-3);var facing=Quaternion.Euler(0,yaw,0);
            FireMiniature.Place(board.transform,head,facing*Vector3.forward,0);
            var failures=new List<string>();
            for(int stage=1;stage<5;stage++)
            {
                var run=Incident(stage);var targets=Targets(run);
                foreach(var equipment in FireEquipment.All)
                {
                    if(equipment.Unlock>stage)continue;
                    for(int prop=0;prop<run.Props.Length;prop++)
                    {
                        int expected=run.Trees.Length+prop;bool reachable=false;Vector3 successfulPalm=default;
                        // These remain in the player's ordinary chest/shoulder workspace:
                        // no walking, leaning across the board, or overhead reach is allowed.
                        foreach(float x in new[]{-.3f,0,.3f})
                        foreach(float y in new[]{-.18f,-.32f,-.45f})
                        foreach(float z in new[]{.25f,.4f})
                        {
                            var palm=head+facing*new Vector3(x,y,z);
                            Launch(palm,targets[expected],equipment,out var origin,out var velocity);
                            if(WaterJet.Predict(origin,velocity,equipment.Range*FireMiniature.Scale,board.transform,targets,out var impact) && impact.Tree==expected)
                            {reachable=true;successfulPalm=palm;break;}
                        }
                        if(!reachable)failures.Add($"stage={stage+1} tool={equipment.Id} prop={prop}/{run.Props[prop].Kind} inaccessible from all 18 comfortable hand poses");
                        else TestContext.WriteLine($"eye={eyeHeight} yaw={yaw} stage={stage+1} tool={equipment.Id} prop={prop} palm={successfulPalm}");
                    }
                }
            }
            Assert.IsEmpty(failures,string.Join("\n",failures));
        }

        [Test] public void CannonEmitsOnePhysicalWaterBombWithOneImpactAndFrozenShotParameters()
        {
            board.transform.localScale=Vector3.one*FireMiniature.Scale;
            var jet=new WaterJet();var origin=new Vector3(0,.4f,0);var targets=new[]{new Vector3(0,.056f,.6f)};
            var velocity=WaterJet.AimVelocity(origin,targets[0],3.7f);
            Assert.IsTrue(WaterJet.Predict(origin,velocity,2.56f,board.transform,targets,out var prediction));
            int impacts=0;WaterJet.Impact actual=default;
            System.Action<WaterJet.Impact> hit=p=>{impacts++;actual=p;};
            jet.Step(.02f,origin,velocity,240,2.56f,board.transform,targets,hit,3.2f,1,0,FireDelivery.Charge);
            Assert.AreEqual(1,jet.Count,"A charged shot is one bomb, not a rapid burst of separately damaging droplets");Assert.Zero(impacts);
            for(int i=0;i<100;i++)jet.Step(.02f,default,default,0,.01f,board.transform,targets,hit,.01f,7,14,FireDelivery.Fan);
            Assert.AreEqual(1,impacts);Assert.Zero(jet.Count);Assert.Zero(actual.Tree);
            Assert.AreEqual(240,actual.Amount);Assert.AreEqual(3.2f,actual.Radius);Assert.AreEqual(1,actual.Wetness);Assert.AreEqual(FireDelivery.Charge,actual.Delivery);
            Assert.Less(Vector3.Distance(actual.Point,prediction.Point),.002f);
        }
    }
}

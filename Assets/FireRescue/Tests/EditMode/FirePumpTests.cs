using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    public sealed class FirePumpTests
    {
        static readonly Pose Handle=new Pose(new Vector3(.2f,1,.5f),Quaternion.Euler(0,45,0));
        static Vector3 At(float z)=>Handle.position+Handle.rotation*new Vector3(0,0,z);
        static void Open(FirePumpGesture pump){for(int i=0;i<6;i++)pump.Sample(true,.04f,At(0),Handle,.02f);}
        [Test] public void PullInToolSpaceAndReleaseReturnsOneChargedGesture()
        {
            var pump=new FirePumpGesture();pump.Sample(true,.01f,At(0),Handle,.02f);Assert.IsFalse(pump.Active);
            Open(pump);pump.Sample(true,.01f,At(0),Handle,.02f);Assert.IsTrue(pump.Active);
            pump.Sample(true,.01f,At(-.09f),Handle,.02f);Assert.That(pump.Pull01,Is.EqualTo(1).Within(.001f));
            pump.Sample(true,.04f,At(-.09f),Handle,.02f);Assert.IsTrue(pump.Released);Assert.IsFalse(pump.Active);
            pump.Sample(true,.04f,At(-.09f),Handle,.02f);Assert.IsFalse(pump.Released);
        }
        [Test] public void MissingHandAndSidewaysEscapeCancelWithoutFiring()
        {
            var pump=new FirePumpGesture();Open(pump);pump.Sample(true,.01f,At(0),Handle,.02f);
            pump.Sample(true,.01f,At(-.09f),Handle,.02f);pump.Sample(false,0,At(-.09f),Handle,.02f);
            Assert.IsTrue(pump.Cancelled);Assert.IsFalse(pump.Released);Assert.Zero(pump.Pull01);
            pump.Sample(true,.01f,At(0),Handle,.02f);Assert.IsFalse(pump.Active);
            Open(pump);pump.Sample(true,.01f,At(0),Handle,.02f);pump.Sample(true,.01f,At(0)+Handle.rotation*Vector3.right*.2f,Handle,.02f);
            Assert.IsTrue(pump.Cancelled);Assert.IsFalse(pump.Released);
        }
        [Test] public void IncidentCompletionCancelsArmedCannonAndRemainingRain()
        {
            var run=new FireRun();
            for(int stage=0;stage<3;stage++)
            {run.StartStage();for(int i=0;i<run.Trees.Length;i++)if(run.Trees[i].State==TreeState.Burning)run.ApplyWater(run.Trees[i].Position,100,.1f);}
            run.BuyEquipment(FireEquipmentId.PressureCannon);run.Equip(FireEquipmentId.PressureCannon);run.StartStage();
            ManualWaterTestSupport.Fill(run);Assert.IsTrue(run.PrimeCannon(1));run.ApplyPropWater(0,70);
            for(int i=0;i<run.Trees.Length;i++)if(run.Trees[i].State==TreeState.Burning)run.ApplyWater(run.Trees[i].Position,100,.1f);
            Assert.AreEqual(FirePhase.Cleared,run.Phase);Assert.Zero(run.Charge01);Assert.Zero(run.Props[0].ActiveRemaining);
            run.Tick(.1f,false);Assert.Zero(run.SprayAmount);
        }
        [Test] public void InvalidPinchSampleCancelsPulledPumpRatherThanReleasing()
        {
            var pump=new FirePumpGesture();Open(pump);pump.Sample(true,.01f,At(0),Handle,.02f);
            pump.Sample(true,.01f,At(-.09f),Handle,.02f);pump.Sample(true,float.NaN,At(-.09f),Handle,.02f);
            Assert.IsTrue(pump.Cancelled);Assert.IsFalse(pump.Released);Assert.Zero(pump.Pull01);
        }
    }
}

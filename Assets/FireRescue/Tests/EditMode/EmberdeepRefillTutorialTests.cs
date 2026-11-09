using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    public sealed class EmberdeepRefillTutorialTests
    {
        static FireRun Dry()
        {
            // Historical hydrant/creek regression uses the purchased rotary tier:
            // TakeTool remains correct here. The starter well's no-tool TryPump
            // and natural grasp stages are covered by NaturalPumpTutorialTests.
            var run=new FireRun();typeof(FireRun).GetProperty(nameof(FireRun.StationTier)).SetValue(run,WaterStationTier.RotaryHydrant);run.StartStage();run.Tick(.02f,false);
            for(int i=0;i<100 && run.Water>0;i++)run.Tick(.1f,true);
            run.CancelTrigger();return run;
        }
        static void Dock(FireRun run,WaterSourceKind kind)
        {for(int i=0;i<3;i++)run.Supply.SampleDock(kind,1,0,0,true,.1f);}

        [Test] public void TutorialReflectsAcceptedHydrantStatesWithoutSupplyingWater()
        {
            var run=Dry();
            Assert.AreEqual(EmberdeepRefillTutorial.Step.TakeTool,EmberdeepRefillTutorial.Describe(run,false,false,true).Stage);
            Assert.AreEqual(EmberdeepRefillTutorial.Step.BringTool,EmberdeepRefillTutorial.Describe(run,false,true,true).Stage);
            run.Supply.SampleDock(WaterSourceKind.Hydrant,1,0,0,true,.1f);
            Assert.AreEqual(EmberdeepRefillTutorial.Step.HoldStill,EmberdeepRefillTutorial.Describe(run,false,true,true).Stage);
            Dock(run,WaterSourceKind.Hydrant);
            var closed=EmberdeepRefillTutorial.Describe(run,false,true,true);
            Assert.AreEqual(EmberdeepRefillTutorial.Target.Valve,closed.Focus);
            Assert.That(closed.Text,Does.Contain("45"));
            for(int i=0;i<100;i++)EmberdeepRefillTutorial.Describe(run,false,true,true);
            Assert.Zero(run.Water);Assert.Zero(run.SuppliedWater);Assert.IsFalse(run.Supply.ValveOpen);
            run.Supply.SetValveOpen(true);
            Assert.AreEqual(EmberdeepRefillTutorial.Step.Filling,EmberdeepRefillTutorial.Describe(run,false,true,true).Stage);
            for(int i=0;i<30;i++)run.Supply.Tick(.1f,true);
            Assert.AreEqual(EmberdeepRefillTutorial.Step.Full,EmberdeepRefillTutorial.Describe(run,false,true,true).Stage);
            Assert.AreEqual(EmberdeepRefillTutorial.Target.Grip,EmberdeepRefillTutorial.Describe(run,false,true,true).Focus);
        }

        [Test] public void CreekTutorialSeparatesIntakePlacementFromCompletedPumpStroke()
        {
            var run=Dry();Dock(run,WaterSourceKind.Creek);
            Assert.AreEqual(EmberdeepRefillTutorial.Target.Intake,EmberdeepRefillTutorial.Describe(run,true,true,true).Focus);
            Assert.AreEqual(EmberdeepRefillTutorial.Target.IntakeSocket,EmberdeepRefillTutorial.Describe(run,true,true,true,true).Focus);
            Assert.Zero(run.Water);
            run.Supply.PlaceIntake(true);
            Assert.AreEqual(EmberdeepRefillTutorial.Step.PullPump,EmberdeepRefillTutorial.Describe(run,true,true,true).Stage);
            run.Supply.SamplePump(0,true,true);run.Supply.SamplePump(.09f,true,true);
            Assert.AreEqual(EmberdeepRefillTutorial.Step.ReturnPump,EmberdeepRefillTutorial.Describe(run,true,true,true).Stage);
            Assert.Zero(run.Water);
            run.Supply.SamplePump(0,true,true);Assert.AreEqual(12,run.Water);
        }

        [Test] public void TutorialDoesNotOverrideOtherSourceOrTrackingPause()
        {
            var run=Dry();Dock(run,WaterSourceKind.Hydrant);
            Assert.AreEqual(EmberdeepRefillTutorial.Step.Busy,EmberdeepRefillTutorial.Describe(run,true,true,true).Stage);
            var paused=EmberdeepRefillTutorial.Describe(run,false,true,false);
            Assert.AreEqual(EmberdeepRefillTutorial.Step.Unavailable,paused.Stage);
            Assert.AreEqual(EmberdeepRefillTutorial.Target.None,paused.Focus);
            Assert.Zero(run.Water);Assert.IsFalse(run.Supply.ValveOpen);
        }

        [Test] public void CircularCouplingIgnoresRollButRejectsWrongFacingAxis()
        {
            var socket=new GameObject("Socket");var port=new GameObject("Port");
            try
            {
                socket.transform.rotation=Quaternion.Euler(0,180,0);
                port.transform.rotation=Quaternion.Euler(0,0,170);
                Assert.That(EmberdeepRefillInteraction.AlignmentAngle(port.transform,socket.transform),Is.LessThan(.01f));
                port.transform.rotation=Quaternion.Euler(0,80,170);
                Assert.That(EmberdeepRefillInteraction.AlignmentAngle(port.transform,socket.transform),Is.GreaterThan(EmberdeepWaterSupply.AlignmentDegrees));
            }
            finally{Object.DestroyImmediate(socket);Object.DestroyImmediate(port);}
        }
    }
}

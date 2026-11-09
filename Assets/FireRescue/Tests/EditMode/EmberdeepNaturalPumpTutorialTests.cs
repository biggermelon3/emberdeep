using NUnit.Framework;

namespace FireRescue.Tests
{
    public sealed class EmberdeepNaturalPumpTutorialTests
    {
        [Test] public void NoToolOffersNaturalGraspTrialAndKeepsEmptyReturnSeparateFromTankState()
        {
            var run=new FireRun();float water=run.Water;
            var trial=EmberdeepRefillTutorial.Describe(run,false,false,true);
            Assert.AreEqual(EmberdeepRefillTutorial.Step.TryPump,trial.Stage);
            Assert.AreEqual(EmberdeepRefillTutorial.Target.Lever,trial.Focus);
            Assert.That(trial.Text,Does.Contain("wrap fingers").And.Contain("Dock a tool"));
            Assert.That(trial.Text.ToLowerInvariant(),Does.Not.Contain("pinch"));
            var down=EmberdeepRefillTutorial.Describe(run,false,false,true,false,true,false);
            var back=EmberdeepRefillTutorial.Describe(run,false,false,true,false,true,true);
            Assert.AreEqual(EmberdeepRefillTutorial.Step.LowerLever,down.Stage);
            Assert.AreEqual(EmberdeepRefillTutorial.Step.ReturnLever,back.Stage);
            Assert.That(back.Text,Does.Contain("small splash"));
            Assert.IsFalse(run.Supply.Locked);Assert.IsFalse(run.Supply.LeverReturnRequired);
            Assert.AreEqual(water,run.Water);Assert.Zero(run.SuppliedWater);
        }

        [Test] public void ConnectedLeverUsesWrapGraspWhileHeldToolAndCreekKeepTheirOwnInstructions()
        {
            var run=new FireRun();
            Assert.AreEqual(EmberdeepRefillTutorial.Target.Socket,EmberdeepRefillTutorial.Describe(run,false,true,true).Focus);
            Assert.AreEqual(EmberdeepRefillTutorial.Target.Pickup,EmberdeepRefillTutorial.Describe(run,true,false,true).Focus);
            run.StartStage();run.Tick(.1f,false);run.Tick(.1f,true);run.CancelTrigger();
            for(int i=0;i<3;i++)run.Supply.SampleDock(WaterSourceKind.Hydrant,1,0,0,true,.1f);
            var connected=EmberdeepRefillTutorial.Describe(run,false,false,true);
            Assert.AreEqual(EmberdeepRefillTutorial.Step.LowerLever,connected.Stage);
            Assert.That(connected.Text,Does.Contain("wrap fingers").And.Contain("8 cm").And.Contain("+12"));
            Assert.That(connected.Text.ToLowerInvariant(),Does.Not.Contain("pinch"));
            Assert.AreEqual(EmberdeepRefillTutorial.Step.Unavailable,EmberdeepRefillTutorial.Describe(run,false,false,false,false,true,true).Stage);
        }
    }
}

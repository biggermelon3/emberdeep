using NUnit.Framework;

namespace FireRescue.Tests
{
    public sealed class EmberdeepStationTutorialTests
    {
        static FireRun Docked(WaterStationTier tier)
        {
            var run=new FireRun();
            // Fixture tier setup isolates presentation from shop/economy tests.
            typeof(FireRun).GetProperty(nameof(FireRun.StationTier)).SetValue(run,tier);
            run.StartStage();run.Tick(.02f,false);
            for(int i=0;i<100 && run.Water>0;i++)run.Tick(.1f,true);
            run.CancelTrigger();
            for(int i=0;i<3;i++)run.Supply.SampleDock(WaterSourceKind.Hydrant,1,0,0,true,.1f);
            Assert.IsTrue(run.Supply.Locked);return run;
        }

        [Test] public void LeverTutorialKeepsReturnInstructionUntilAcceptedStrokeCompletes()
        {
            var run=Docked(WaterStationTier.LeverWell);
            Assert.AreEqual(EmberdeepRefillTutorial.Step.LowerLever,EmberdeepRefillTutorial.Describe(run,false,true,true).Stage);
            run.Supply.SampleLever(0,true,true);run.Supply.SampleLever(.08f,true,true);run.Supply.SampleLever(.04f,true,true);
            var returning=EmberdeepRefillTutorial.Describe(run,false,true,true);
            Assert.AreEqual(EmberdeepRefillTutorial.Step.ReturnLever,returning.Stage);
            Assert.AreEqual(EmberdeepRefillTutorial.Target.Lever,returning.Focus);
            for(int i=0;i<20;i++)EmberdeepRefillTutorial.Describe(run,false,true,true);
            Assert.Zero(run.Water,"Drawing the tutorial cannot complete a stroke");
            Assert.AreEqual(12,run.Supply.SampleLever(0,true,true));
            Assert.AreEqual(EmberdeepRefillTutorial.Step.LowerLever,EmberdeepRefillTutorial.Describe(run,false,true,true).Stage);
        }

        [Test] public void RotaryAndButtonTutorialsDescribeDifferentAcceptedActions()
        {
            var rotary=Docked(WaterStationTier.RotaryHydrant);
            var turn=EmberdeepRefillTutorial.Describe(rotary,false,true,true);
            Assert.AreEqual(EmberdeepRefillTutorial.Target.Valve,turn.Focus);
            Assert.That(turn.Text,Does.Contain("45 degrees"));Assert.That(turn.Text,Does.Not.Contain("TAP"));
            rotary.Supply.TurnValve(45);
            Assert.That(EmberdeepRefillTutorial.Describe(rotary,false,true,true).Text,Does.Contain("turn back"));
            var button=Docked(WaterStationTier.PushButton);
            Assert.AreEqual(EmberdeepRefillTutorial.Step.PressButton,EmberdeepRefillTutorial.Describe(button,false,true,true).Stage);
            button.Supply.SampleButton(false,true);button.Supply.SampleButton(true,true);
            var filling=EmberdeepRefillTutorial.Describe(button,false,true,true);
            Assert.AreEqual(EmberdeepRefillTutorial.Target.Button,filling.Focus);Assert.That(filling.Text,Does.Contain("Press again to stop"));
            button.Supply.SampleButton(false,true);button.Supply.SampleButton(true,true);
            Assert.AreEqual(EmberdeepRefillTutorial.Step.PressButton,EmberdeepRefillTutorial.Describe(button,false,true,true).Stage);
            Assert.Zero(button.Water,"Tutorial/action cues alone cannot advance timed filling");
        }
    }
}

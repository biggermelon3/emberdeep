using NUnit.Framework;
namespace FireRescue.Tests
{
    public sealed class EmberdeepSqueezeTests
    {
        [Test] public void LightGripAndFireHaveSeparateThresholdsAndHysteresis()
        {
            var grab=new EmberdeepSqueeze();var fire=new EmberdeepSqueeze(EmberdeepSqueeze.Fire,EmberdeepSqueeze.StopFire);
            grab.Sample(true,0,.025f);fire.Sample(true,0,.025f);
            for(int i=0;i<4;i++){grab.Sample(true,.5f,.025f);fire.Sample(true,.5f,.025f);}
            Assert.IsTrue(grab.Closed);Assert.IsFalse(fire.Closed);
            for(int i=0;i<4;i++)fire.Sample(true,.9f,.025f);Assert.IsTrue(fire.Closed);
            fire.Sample(true,.65f,.025f);Assert.IsTrue(fire.Closed);fire.Sample(true,.5f,.025f);Assert.IsFalse(fire.Closed);
            grab.Sample(true,.3f,.025f);Assert.IsTrue(grab.Closed);grab.Sample(true,.1f,.025f);Assert.IsFalse(grab.Closed);
        }
        [Test] public void InvalidSamplesAndLongFrameCannotForgeFreshSqueeze()
        {
            var input=new EmberdeepSqueeze();input.Sample(true,0,.025f);input.Sample(true,1,20);Assert.IsFalse(input.Closed);
            input.Sample(false,1,.025f);for(int i=0;i<10;i++)input.Sample(true,1,.025f);Assert.IsFalse(input.Closed);
            input.Sample(true,float.NaN,.025f);Assert.IsFalse(input.Closed);
        }
        [Test] public void FreshGraspHasBoundedAcquisitionGraceWithoutRepeatedPressEvents()
        {
            var input=new EmberdeepSqueeze();input.Sample(true,0,.025f);
            for(int i=0;i<3;i++)input.Sample(true,.5f,.025f);
            Assert.IsTrue(input.Pressed);Assert.IsTrue(input.CanAcquire);
            input.Sample(true,.5f,.025f);Assert.IsFalse(input.Pressed);Assert.IsTrue(input.CanAcquire);
            for(int i=0;i<12;i++)input.Sample(true,.5f,.025f);
            Assert.IsTrue(input.Closed);Assert.IsFalse(input.CanAcquire);
            input.Sample(false,0,.025f);Assert.IsFalse(input.CanAcquire);
        }
    }
}

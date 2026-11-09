using NUnit.Framework;

namespace FireRescue.Tests
{
    public sealed class EmberdeepGripGestureTests
    {
        static void Sample(EmberdeepGripGesture grip,float strength,int count)
        {for(int i=0;i<count;i++)grip.Sample(true,strength,.025f);}
        [Test] public void FistRequiresObservedOpenHandAndStableClosure()
        {
            var g=new EmberdeepGripGesture();Sample(g,1,20);Assert.IsFalse(g.Closed);
            Sample(g,0,3);Sample(g,.85f,2);Assert.IsFalse(g.Closed);
            Sample(g,.85f,1);Assert.IsTrue(g.Pressed);Assert.IsTrue(g.Closed);
            Sample(g,.85f,1);Assert.IsFalse(g.Pressed);Assert.IsTrue(g.Closed);
        }
        [Test] public void HysteresisKeepsGripButReleaseNeedsFreshOpenDwell()
        {
            var g=new EmberdeepGripGesture();Sample(g,0,3);Sample(g,1,3);
            Sample(g,.6f,10);Assert.IsTrue(g.Closed);Assert.IsFalse(g.Pressed);
            Sample(g,.55f,1);Assert.IsFalse(g.Closed);Sample(g,1,10);Assert.IsFalse(g.Closed);
            Sample(g,.55f,3);Sample(g,.8f,3);Assert.IsTrue(g.Pressed);
        }
        [Test] public void ThresholdJitterCannotAccumulateCloseDwell()
        {
            var g=new EmberdeepGripGesture();Sample(g,0,3);
            for(int i=0;i<20;i++){Sample(g,.81f,1);Sample(g,.79f,1);}
            Assert.IsFalse(g.Pressed);Assert.IsFalse(g.Closed);
        }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-.1f)] [TestCase(1.1f)]
        public void InvalidStrengthRequiresOpenAfterRecovery(float strength)
        {
            var g=new EmberdeepGripGesture();Sample(g,0,3);Sample(g,1,3);
            g.Sample(true,strength,.025f);Assert.IsFalse(g.Closed);
            Sample(g,1,10);Assert.IsFalse(g.Closed);
        }
        [Test] public void TrackingLossAndLongFramesCannotCreateFreshGrip()
        {
            var g=new EmberdeepGripGesture();Sample(g,0,3);Sample(g,1,3);
            g.Sample(false,1,.025f);Sample(g,1,10);Assert.IsFalse(g.Closed);
            g.Sample(true,0,10);g.Sample(true,1,10);Assert.IsFalse(g.Closed);
            g.Reset();Sample(g,0,3);g.Sample(true,1,float.NaN);Sample(g,1,10);Assert.IsFalse(g.Closed);
        }
        [Test] public void SharedStrokeNeverCompletesFromReturnOnlyOrPartialCycles()
        {
            var s=new EmberdeepLeverStroke();Assert.IsFalse(s.Sample(.09f,true,true));Assert.IsFalse(s.Sample(0,true,true));
            for(int i=0;i<20;i++){s.Sample(.07f,true,true);Assert.IsFalse(s.Sample(.01f,true,true));}
            s.Sample(.085f,true,true);Assert.IsTrue(s.ReturnRequired);Assert.IsFalse(s.Sample(.04f,true,true));
            Assert.IsTrue(s.ReturnRequired);Assert.IsTrue(s.Sample(.02f,true,true));Assert.IsFalse(s.ReturnRequired);
            Assert.IsFalse(s.Sample(.02f,true,true));s.Sample(.085f,true,true);s.Reset();Assert.IsFalse(s.Sample(0,true,true));
        }
    }
}

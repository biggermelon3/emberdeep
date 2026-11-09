using NUnit.Framework;

namespace FireRescue.Tests
{
    public sealed class EmberdeepGazePinchTests
    {
        const float Open = .04f, Closed = .01f, Dt = .02f;

        static void Ready(EmberdeepGazePinch input, int target = 1,
            bool leftTracked = true, bool rightTracked = true)
        {
            for (int i = 0; i < 8; i++)
                Assert.IsFalse(input.Sample(target, true, leftTracked, Open, rightTracked, Open, Dt));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void EitherTrackedHandCanActivateAndHeldPinchDoesNotRepeat(bool useLeft)
        {
            var input = new EmberdeepGazePinch();
            Ready(input, 1, useLeft, !useLeft);
            Assert.IsTrue(input.Sample(1, true, useLeft, Closed, !useLeft, Closed, Dt));
            for (int i = 0; i < 20; i++)
                Assert.IsFalse(input.Sample(1, true, useLeft, Closed, !useLeft, Closed, Dt));
        }

        [Test]
        public void NegativeUnityInstanceIdIsAValidTarget()
        {
            var input = new EmberdeepGazePinch();
            Ready(input, -123);
            Assert.IsTrue(input.Sample(-123, true, true, Closed, false, Open, Dt));
        }

        [Test]
        public void NoGazeOrUnavailableTargetCannotActivate()
        {
            var input = new EmberdeepGazePinch();
            Ready(input);
            Assert.IsFalse(input.Sample(0, true, true, Closed, true, Closed, Dt));
            Ready(input);
            Assert.IsFalse(input.Sample(1, false, true, Closed, true, Closed, Dt));
            for (int i = 0; i < 10; i++)
                Assert.IsFalse(input.Sample(1, true, true, Closed, true, Closed, Dt));
        }

        [Test]
        public void AcquiringTargetUnderHeldPinchRequiresOpeningBeforeActivation()
        {
            var input = new EmberdeepGazePinch();
            for (int i = 0; i < 8; i++) input.Sample(0, true, true, Open, false, Open, Dt);
            input.Sample(0, true, true, Closed, false, Open, Dt);
            for (int i = 0; i < 10; i++)
                Assert.IsFalse(input.Sample(1, true, true, Closed, false, Open, Dt));
            Ready(input, 1, true, false);
            Assert.IsTrue(input.Sample(1, true, true, Closed, false, Open, Dt));
        }

        [Test]
        public void PinchBeforeGazeDwellDoesNotBecomeAnActivationWhenHeld()
        {
            var input = new EmberdeepGazePinch();
            for (int i = 0; i < 4; i++)
                Assert.IsFalse(input.Sample(1, true, true, Open, false, Open, Dt));
            Assert.IsFalse(input.Sample(1, true, true, Closed, false, Open, Dt));
            for (int i = 0; i < 10; i++)
                Assert.IsFalse(input.Sample(1, true, true, Closed, false, Open, Dt));
            Ready(input, 1, true, false);
            Assert.IsTrue(input.Sample(1, true, true, Closed, false, Open, Dt));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void LostHandCannotReactivateUntilAValidOpening(bool useLeft)
        {
            var input = new EmberdeepGazePinch();
            Ready(input, 1, useLeft, !useLeft);
            Assert.IsFalse(input.Sample(1, true, false, Closed, false, Closed, Dt));
            for (int i = 0; i < 10; i++)
                Assert.IsFalse(input.Sample(1, true, useLeft, Closed, !useLeft, Closed, Dt));
            Ready(input, 1, useLeft, !useLeft);
            Assert.IsTrue(input.Sample(1, true, useLeft, Closed, !useLeft, Closed, Dt));
        }

        [Test]
        public void ModalResetRequiresFreshOpeningEvenWhenTargetIdIsReused()
        {
            var input = new EmberdeepGazePinch();
            Ready(input);
            input.Reset();
            for (int i = 0; i < 10; i++)
                Assert.IsFalse(input.Sample(1, true, true, Closed, true, Closed, Dt));
            Ready(input);
            Assert.IsTrue(input.Sample(1, true, true, Open, true, Closed, Dt));
        }

        [Test]
        public void ChangingOrJitteringTargetDoesNotTransferArmedPinch()
        {
            var input = new EmberdeepGazePinch();
            Ready(input);
            Assert.IsFalse(input.Sample(2, true, true, Closed, true, Open, Dt));
            for (int i = 0; i < 20; i++)
                Assert.IsFalse(input.Sample(i % 2 + 1, true, true, i % 2 == 0 ? Open : Closed,
                    true, i % 2 == 0 ? Closed : Open, Dt));
            Ready(input, 2);
            Assert.IsTrue(input.Sample(2, true, true, Closed, true, Open, Dt));
        }

        [Test]
        public void SimultaneousAndFollowingHandPinchesAreConsumedAcrossPages()
        {
            var input = new EmberdeepGazePinch();
            Ready(input);
            Assert.IsTrue(input.Sample(1, true, true, Closed, true, Closed, Dt));
            for (int i = 0; i < 10; i++)
                Assert.IsFalse(input.Sample(2, true, true, Closed, true, Closed, Dt));

            Ready(input, 2);
            Assert.IsTrue(input.Sample(2, true, true, Closed, true, Open, Dt));
            for (int i = 0; i < 10; i++)
                Assert.IsFalse(input.Sample(3, true, true, Closed, true, Closed, Dt));
            Ready(input, 3);
            Assert.IsTrue(input.Sample(3, true, true, Open, true, Closed, Dt));
        }

        [Test]
        public void DelayedFrameAndInvalidDistanceCannotArmAnActivation()
        {
            var input = new EmberdeepGazePinch();
            Assert.IsFalse(input.Sample(1, true, true, Open, false, Open, 10));
            Assert.IsFalse(input.Sample(1, true, true, Closed, false, Open, Dt));
            Ready(input, 1, true, false);
            Assert.IsFalse(input.Sample(1, true, true, float.NaN, false, Open, Dt));
            Assert.IsFalse(input.Sample(1, true, true, Closed, false, Open, Dt));
            Ready(input, 1, true, false);
            Assert.IsFalse(input.Sample(1, true, true, Closed, false, Open, float.PositiveInfinity));
            for (int i = 0; i < 10; i++)
                Assert.IsFalse(input.Sample(1, true, true, Closed, false, Open, Dt));
        }
    }
}

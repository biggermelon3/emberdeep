using NUnit.Framework;
using UnityEngine;

namespace CloudGarden.Tests
{
    public sealed class GardenGestureTests
    {
        GardenSettings settings;
        GardenGestureRecognizer recognizer;
        [SetUp] public void SetUp()
        { settings = ScriptableObject.CreateInstance<GardenSettings>(); recognizer = new GardenGestureRecognizer(); }
        [TearDown] public void TearDown() => Object.DestroyImmediate(settings);
        static GardenHandPose Hand(Vector3 point, float curl = 0, bool pinch = false)
            => new GardenHandPose { tracked = true, fingersValid = true, curl = curl, pinching = pinch, palm = point, tip = point, wrist = point + Vector3.up * .08f };
        GardenGestureResult Step(GardenHandPose left, GardenHandPose right = default, bool weather = true, float dt = .05f)
            => recognizer.Step(left, right, Vector3.zero, Vector3.up, .28f, dt, weather, settings);

        [Test] public void FistRequiresHoldAndOpenHandToRetrigger()
        {
            var fist = Hand(Vector3.zero, 1);
            for (int i = 0; i < 11; i++) Assert.IsFalse(Step(fist).toggleRain);
            Assert.IsTrue(Step(fist).toggleRain);
            for (int i = 0; i < 30; i++) Assert.IsFalse(Step(fist).toggleRain);
            Step(default); // Tracking loss does not re-arm a held fist.
            for (int i = 0; i < 20; i++) Assert.IsFalse(Step(fist).toggleRain);
            Step(Hand(Vector3.zero));
            int triggers = 0;
            for (int i = 0; i < 13; i++) if (Step(fist).toggleRain) triggers++;
            Assert.AreEqual(1, triggers);
        }

        [Test] public void LostTrackingAndLongFramesRestartFistDwell()
        {
            var fist = Hand(Vector3.zero, 1);
            for (int i = 0; i < 8; i++) Step(fist);
            Step(default);
            for (int i = 0; i < 8; i++) Assert.IsFalse(Step(fist).toggleRain);
            Assert.IsFalse(Step(fist, dt: .5f).toggleRain);
            for (int i = 0; i < 11; i++) Assert.IsFalse(Step(fist).toggleRain);
            Assert.IsTrue(Step(fist).toggleRain);
        }

        [Test] public void PinchMissingFingersDistanceAndManipulationCannotTriggerWeather()
        {
            var invalid = Hand(Vector3.zero, 1); invalid.fingersValid = false;
            foreach (var hand in new[] { invalid, Hand(Vector3.zero, 1, true), Hand(Vector3.right, 1) })
            {
                recognizer.Reset();
                for (int i = 0; i < 30; i++) Assert.IsFalse(Step(hand).toggleRain);
            }
            for (int i = 0; i < 30; i++) Assert.IsFalse(Step(Hand(Vector3.zero, 1), weather: false).toggleRain);
        }

        [Test] public void PatIntegratesMotionAndHasCooldown()
        {
            Step(Hand(Vector3.up * .2f), dt: .02f);
            int count = 0;
            for (int i = 1; i <= 8; i++)
                if (Step(Hand(Vector3.up * (.2f - i * .01f)), dt: .02f).pat) count++;
            Assert.AreEqual(1, count);
            for (int i = 0; i < 15; i++) Assert.IsFalse(Step(Hand(Vector3.up * .12f)).pat);
            Step(Hand(Vector3.up * .1f), dt: .02f);
            Assert.IsTrue(Step(Hand(Vector3.up * .08f), dt: .02f).pat);
        }

        [Test] public void TrackingRecoveryAndTeleportCannotPat()
        {
            Step(Hand(Vector3.up * .25f)); Step(default);
            Assert.IsFalse(Step(Hand(Vector3.zero)).pat);
            Assert.IsFalse(Step(Hand(Vector3.down * .25f)).pat);
            Assert.IsFalse(Step(Hand(Vector3.down * .25f)).knead);
        }

        [Test] public void TwoHandShearKneadsButStretchAndStationaryHandsDoNot()
        {
            var left = Hand(Vector3.left * .1f); var right = Hand(Vector3.right * .1f);
            Step(left, right);
            for (int i = 0; i < 20; i++) Assert.IsFalse(Step(left, right).knead);
            for (int i = 0; i < 4; i++)
            {
                left.palm.x -= .02f; right.palm.x += .02f;
                Assert.IsFalse(Step(left, right).knead);
            }
            recognizer.Reset(); left = Hand(Vector3.left * .1f); right = Hand(Vector3.right * .1f);
            Step(left, right);
            int count = 0;
            for (int i = 0; i < 8; i++)
            {
                left.palm.z += i % 2 == 0 ? .02f : -.02f;
                right.palm.z -= i % 2 == 0 ? .02f : -.02f;
                if (Step(left, right).knead) count++;
            }
            Assert.AreEqual(1, count);
        }

        [Test] public void FingerCurlIsNormalizedByHandSize()
        {
            float small = GardenHandPose.FingerCurl(new Vector3(.025f, 0, 0), Vector3.zero, .05f);
            float large = GardenHandPose.FingerCurl(new Vector3(.05f, 0, 0), Vector3.zero, .1f);
            Assert.AreEqual(small, large, .001f); Assert.Greater(small, .8f);
            Assert.Less(GardenHandPose.FingerCurl(Vector3.up * .14f, Vector3.zero, .07f), .1f);
        }

        [Test] public void FrictionNeedsProximityAndRealMotionWithBoundedCharge()
        {
            float still = CloudContactCharging.ChargeDelta(.3f, .38f, 0, .05f, settings);
            Assert.Greater(still, 0);
            Assert.AreEqual(still, CloudContactCharging.ChargeDelta(.3f, .38f, .001f, .05f, settings));
            Assert.AreEqual(0, CloudContactCharging.ChargeDelta(1, .38f, .02f, .05f, settings));
            float rubbing = CloudContactCharging.ChargeDelta(.3f, .38f, .02f, .05f, settings);
            Assert.Greater(rubbing, still); Assert.LessOrEqual(rubbing, .05f * settings.contactChargeRateLimit);
            Assert.AreEqual(still, CloudContactCharging.ChargeDelta(.3f, .38f, 1, .05f, settings));
        }

        [Test] public void ChargingUsesElapsedTimeAndRejectsLongFrames()
        {
            float slow = CloudContactCharging.ChargeDelta(.3f, .38f, .01f, .05f, settings);
            float fast = CloudContactCharging.ChargeDelta(.3f, .38f, .002f, .01f, settings);
            Assert.AreEqual(slow, fast * 5, .0001f);
            Assert.AreEqual(0, CloudContactCharging.ChargeDelta(.3f, .38f, 1, 1, settings));
        }
    }
}

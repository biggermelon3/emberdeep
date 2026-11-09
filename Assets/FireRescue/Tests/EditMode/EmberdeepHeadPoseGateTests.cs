using NUnit.Framework;
using UnityEngine;
using UnityEngine.XR;

namespace FireRescue.Tests
{
    public sealed class EmberdeepHeadPoseGateTests
    {
        const InputTrackingState Complete = InputTrackingState.Position | InputTrackingState.Rotation;
        static Pose Head(float height = 1.65f) => new Pose(new Vector3(2, height, -3), Quaternion.identity);
        static bool Allows(Pose head, float floor = 0, TrackingOriginModeFlags origin = TrackingOriginModeFlags.Floor,
            Vector3? raw = null, bool trackedReadable = true, bool tracked = true,
            bool stateReadable = true, InputTrackingState state = Complete, bool positionReadable = true) =>
            EmberdeepHeadPoseGate.Allows(head, floor, trackedReadable, tracked, stateReadable, state,
                origin, positionReadable, raw ?? new Vector3(0, 1.65f, 0));

        [TestCase(1.65f, 0f)] [TestCase(1.1f, 0f)] [TestCase(3.1f, 2f)]
        public void ValidFloorPosesAcceptStandingSeatedAndTranslatedRig(float eye, float floor)
        {
            Assert.IsTrue(Allows(Head(eye), floor, raw: new Vector3(0, eye - floor, 0)));
        }

        [Test] public void FloorOriginZeroCannotBeMaskedByPrefabHeightOffset()
        {
            Assert.IsFalse(Allows(Head(1.2f), raw: Vector3.zero));
            Assert.IsFalse(Allows(Head(1.1176f), raw: new Vector3(0, .2f, 0)));
            Assert.IsTrue(Allows(Head(1.2f), raw: new Vector3(0, 1.2f, 0)));
        }

        [Test] public void DeviceOriginZeroIsLegitimateWhenWorldPoseAndTrackingAreValid()
        {
            Assert.IsTrue(Allows(Head(1.1176f), origin: TrackingOriginModeFlags.Device, raw: Vector3.zero));
            Assert.IsFalse(Allows(Head(1.1176f), origin: TrackingOriginModeFlags.Device, raw: Vector3.zero, tracked: false));
            Assert.IsFalse(Allows(Head(0), origin: TrackingOriginModeFlags.Device, raw: Vector3.zero));
        }

        [Test] public void UnknownOriginNeverAcceptsAStableLookingWorldPose()
        {
            Assert.IsFalse(Allows(Head(), origin: TrackingOriginModeFlags.Unknown));
        }

        [TestCase(InputTrackingState.None)]
        [TestCase(InputTrackingState.Position)]
        [TestCase(InputTrackingState.Rotation)]
        [TestCase(InputTrackingState.Velocity | InputTrackingState.AngularVelocity)]
        public void ReadableIncompleteTrackingStateRejectsPose(InputTrackingState state)
        {
            Assert.IsFalse(Allows(Head(), state: state));
        }

        [Test] public void UnavailableOptionalSignalsDifferFromExplicitTrackingFailure()
        {
            Assert.IsTrue(Allows(Head(), trackedReadable: false, tracked: false, stateReadable: false,
                state: InputTrackingState.None, positionReadable: false, raw: Vector3.zero));
            Assert.IsFalse(Allows(Head(), trackedReadable: true, tracked: false));
            Assert.IsTrue(Allows(Head(), state: Complete | InputTrackingState.Velocity));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void InvalidWorldOrFloorDataRejectsEvenWithValidTracking(int invalid)
        {
            var head = Head(); float floor = 0;
            if (invalid == 0) head.position.x = float.NaN;
            if (invalid == 1) head.position.y = float.PositiveInfinity;
            if (invalid == 2) head.rotation = new Quaternion(0, 0, 0, 0);
            if (invalid == 3) head.rotation = new Quaternion(float.NaN, 0, 0, 1);
            if (invalid == 4) floor = float.NegativeInfinity;
            if (invalid == 5) head.position.y = .44f;
            Assert.IsFalse(Allows(head, floor));
        }

        [TestCase(TrackingOriginModeFlags.Floor)] [TestCase(TrackingOriginModeFlags.Device)]
        public void ReadableNonfiniteRawPoseRejectsEveryOrigin(TrackingOriginModeFlags origin)
        {
            Assert.IsFalse(Allows(Head(), origin: origin, raw: new Vector3(float.NaN, 1.65f, 0)));
        }

        [Test] public void EachFrameIsIndependentAndRecoveryDoesNotNeedAnOriginEvent()
        {
            Assert.IsTrue(Allows(Head()));
            Assert.IsFalse(Allows(Head(), raw: Vector3.zero));
            Assert.IsFalse(Allows(Head(), tracked: false));
            Assert.IsTrue(Allows(Head()));
        }
    }
}

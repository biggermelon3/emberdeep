using NUnit.Framework;

namespace PhysicalAssembly.Tests
{
    public sealed class BuildModeGestureTests
    {
        BuildModeGesture gesture; float now; int steps;
        [SetUp] public void Setup() { gesture=new BuildModeGesture(); now=0; steps=0; gesture.Step+=(_,__)=>steps++; }
        bool Pose(Fingers pose,bool building=false,int hand=0,float duration=.16f)
        {
            bool switched=false;
            for(float until=now+duration;now<until;now+=.02f) switched|=gesture.Sample(hand,true,pose,building?ModeGestureDirection.Close:ModeGestureDirection.Open,true,now);
            return switched;
        }
        [TestCase(0)] [TestCase(1)] public void EitherHandOpensInEitherOrder(int hand)
        {
            Assert.IsFalse(Pose(Fingers.Thumb,false,hand,2.4f)); Assert.IsTrue(gesture.Armed); Assert.AreEqual(1,steps);
            Assert.IsFalse(Pose(Fingers.Thumb|Fingers.Middle,false,hand));
            Assert.IsTrue(Pose(BuildModeGesture.Three,false,hand)); Assert.AreEqual(3,steps);
            Assert.IsFalse(Pose(BuildModeGesture.Three,true,hand,3)); Assert.IsFalse(gesture.Armed,"Held completion pose must not arm the reverse sequence.");
        }
        [Test] public void ClosingAnyOrderProducesFourActions()
        {
            Pose(BuildModeGesture.Three,true,0,2.4f);
            Pose(Fingers.Index|Fingers.Middle,true); Pose(Fingers.Middle,true);
            Assert.IsTrue(Pose(Fingers.None,true)); Assert.AreEqual(4,steps);
        }
        [Test] public void SimultaneousChangesStillProduceSeparateNotes()
        {
            Pose(Fingers.Thumb,false,0,2.4f); Assert.IsTrue(Pose(BuildModeGesture.Three)); Assert.AreEqual(3,steps);
        }
        [Test] public void OtherHandCannotCompleteOwnersSequence()
        {
            Pose(Fingers.Thumb,false,0,2.4f); Assert.IsFalse(Pose(BuildModeGesture.Three,false,1)); Assert.AreEqual(0,gesture.Owner);
        }
        [Test] public void FoldingThumbOrReopeningClosedFingerCancels()
        {
            Pose(Fingers.Thumb,false,0,2.4f); Pose(Fingers.None); Assert.IsFalse(gesture.Armed);
            gesture.Reset(); Pose(BuildModeGesture.Three,true,0,2.4f); Pose(Fingers.Index|Fingers.Middle,true); Pose(BuildModeGesture.Three,true);
            Assert.IsFalse(gesture.Armed);
        }
        [Test] public void TrackingLossAndBlockedInteractionCancelWithoutSwitching()
        {
            Pose(Fingers.Thumb,false,0,2.4f);
            gesture.Sample(0,false,Fingers.None,ModeGestureDirection.Open,true,now); now+=.4f;
            Assert.IsFalse(gesture.Sample(0,true,BuildModeGesture.Three,ModeGestureDirection.Open,true,now)); Assert.IsFalse(gesture.Armed);
            gesture.Reset(); Pose(Fingers.Thumb,false,0,2.4f);
            Assert.IsFalse(gesture.Sample(0,true,BuildModeGesture.Three,ModeGestureDirection.Open,false,now)); Assert.IsFalse(gesture.Armed);
        }
        [Test] public void ArmingNeedsTwoSecondsAndSequenceExpires()
        {
            Pose(Fingers.Thumb,false,0,1); Assert.IsFalse(gesture.Armed);
            Pose(Fingers.None); Pose(Fingers.Thumb,false,0,2.4f); Assert.IsTrue(gesture.Armed);
            now+=9; Pose(Fingers.Thumb); Assert.IsFalse(gesture.Armed);
        }
        [TestCase(false)] [TestCase(true)] public void RingAndLittleFingerCancelBothDirections(bool closing)
        {
            Pose(closing?BuildModeGesture.Three:Fingers.Thumb,closing,0,2.4f);
            Assert.IsTrue(gesture.Armed);
            Pose(BuildModeGesture.Three|Fingers.Ring|Fingers.Pinky,closing);
            Assert.IsFalse(gesture.Armed);
        }
        [Test] public void NarrowGapKeepsOpeningToBuildAndClosingToDock()
        {
            var go=new UnityEngine.GameObject("Narrow Gap gesture mapping");
            try
            {
                var controller=go.AddComponent<NarrowGapController>();
                var state=typeof(NarrowGapController).GetProperty("State");
                state.SetValue(controller,ChallengeState.Building);
                Assert.AreEqual(ModeGestureDirection.Close,((IBuildModeTarget)controller).GestureDirection);
                state.SetValue(controller,ChallengeState.LaunchReady);
                Assert.AreEqual(ModeGestureDirection.Open,((IBuildModeTarget)controller).GestureDirection);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}

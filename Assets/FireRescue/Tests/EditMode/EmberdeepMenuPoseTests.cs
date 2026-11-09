using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    public sealed class EmberdeepMenuPoseTests
    {
        static Pose Head(float height = 1.65f, float yaw = 0) =>
            new Pose(new Vector3(2, height, -3), Quaternion.Euler(0, yaw, 0));

        static Pose Place(EmberdeepMenuPose policy, Pose head, float floor = 0)
        {
            Assert.IsFalse(policy.TryUpdate(true, head, floor, .1f, out _));
            for (int i = 0; i < 3; i++) Assert.IsFalse(policy.TryUpdate(true, head, floor, .1f, out _));
            Assert.IsTrue(policy.TryUpdate(true, head, floor, .1f, out var result));
            return result;
        }

        [Test] public void StartupAtFloorNeverFallsBackEvenWhenTrackingClaimsReady()
        {
            var policy = new EmberdeepMenuPose();
            for (int i = 0; i < 200; i++)
                Assert.IsFalse(policy.TryUpdate(true, new Pose(Vector3.zero, Quaternion.identity), 0, 1, out _));
            Assert.IsFalse(policy.IsPlaced);
            Assert.AreEqual(0, policy.StableSeconds);
            Place(policy, Head());
        }

        [TestCase(1.65f, 0f, 0f)] [TestCase(1.1f, 0f, 90f)] [TestCase(3.1f, 2f, -90f)]
        public void StablePosePlacesExactlyAtSeatedOrStandingEyeHeight(float eye, float floor, float yaw)
        {
            var policy = new EmberdeepMenuPose();
            var head = Head(eye, yaw);
            var result = Place(policy, head, floor);
            var forward = head.rotation * Vector3.forward;
            Assert.That(result.position.y, Is.EqualTo(eye).Within(.000001f));
            Assert.That(Vector3.Distance(result.position, head.position + forward * 1.05f), Is.LessThan(.00001f));
            Assert.That(Vector3.Angle(result.rotation * Vector3.up, Vector3.up), Is.LessThan(.001f));
            Assert.That(Vector3.Angle(result.rotation * Vector3.forward, forward), Is.LessThan(.001f));
            Assert.IsFalse(policy.TryUpdate(true, Head(eye + .2f, yaw + 90), floor, .1f, out var anchored));
            Assert.AreEqual(result.position, anchored.position);
        }

        [Test] public void RecenterRequiresANewStableWindowAndUsesTheNewHeading()
        {
            var policy = new EmberdeepMenuPose();
            Place(policy, Head());
            policy.RequestRecenter();
            Assert.IsFalse(policy.IsPlaced);
            Assert.AreEqual(0, policy.StableSeconds);
            var result = Place(policy, Head(1.2f, 90));
            Assert.That(result.position.x, Is.EqualTo(3.05f).Within(.00001f));
            Assert.That(result.position.z, Is.EqualTo(-3).Within(.00001f));
        }

        [Test] public void VerticalLookPreservesLastHeadingButCannotInventInitialHeading()
        {
            var policy = new EmberdeepMenuPose();
            var vertical = new Pose(Head().position, Quaternion.Euler(-90, 90, 0));
            for (int i = 0; i < 12; i++) Assert.IsFalse(policy.TryUpdate(true, vertical, 0, .1f, out _));
            Place(policy, Head(1.65f, 90));
            policy.RequestRecenter();
            var result = Place(policy, vertical);
            Assert.That(Vector3.Angle(result.rotation * Vector3.forward, Vector3.right), Is.LessThan(.001f));
        }

        [TestCase(false, 0)] [TestCase(true, 1)] [TestCase(true, 2)]
        [TestCase(true, 3)] [TestCase(true, 4)] [TestCase(true, 5)] [TestCase(true, 6)]
        public void InvalidInputDiscardsPreviousStableObservations(bool tracked, int invalid)
        {
            var policy = new EmberdeepMenuPose();
            for (int i = 0; i < 4; i++) Assert.IsFalse(policy.TryUpdate(true, Head(), 0, .1f, out _));
            var head = Head();
            float floor = 0, dt = .1f;
            if (invalid == 1) head.position.x = float.NaN;
            if (invalid == 2) head.rotation = new Quaternion(0, float.PositiveInfinity, 0, 1);
            if (invalid == 3) head.rotation = new Quaternion(0, 0, 0, 0);
            if (invalid == 4) floor = float.NaN;
            if (invalid == 5) dt = float.PositiveInfinity;
            if (invalid == 6) dt = -.1f;
            Assert.IsFalse(policy.TryUpdate(tracked, head, floor, dt, out _));
            Assert.AreEqual(0, policy.StableSeconds);
            Place(policy, Head());
        }

        [TestCase(.06f, 0f)] [TestCase(0f, 6f)]
        public void MovingOutsideStableWindowRestartsAcquisition(float displacement, float yaw)
        {
            var policy = new EmberdeepMenuPose();
            for (int i = 0; i < 4; i++) Assert.IsFalse(policy.TryUpdate(true, Head(), 0, .1f, out _));
            var next = Head(1.65f, yaw); next.position.x += displacement;
            var result = Place(policy, next);
            Assert.That(result.position.y, Is.EqualTo(next.position.y));
        }

        [Test] public void HitchAndSmallCumulativeDriftDoNotSatisfyStability()
        {
            var policy = new EmberdeepMenuPose();
            Assert.IsFalse(policy.TryUpdate(true, Head(), 0, 20, out _));
            Assert.IsFalse(policy.TryUpdate(true, Head(), 0, 20, out _));
            Assert.That(policy.StableSeconds, Is.EqualTo(.1f).Within(.00001f));
            for (int i = 1; i < 12; i++)
            {
                var head = Head(); head.position.x += i * .02f;
                Assert.IsFalse(policy.TryUpdate(true, head, 0, .05f, out _));
            }
        }

        [Test] public void FloorOriginChangeRestartsAcquisition()
        {
            var policy = new EmberdeepMenuPose();
            for (int i = 0; i < 4; i++) Assert.IsFalse(policy.TryUpdate(true, Head(), 0, .1f, out _));
            Place(policy, Head(), .1f);
        }
    }

    public sealed class EmberdeepHudPoseTests
    {
        [TestCase(1.1f,0f,90f,70f,20f)]
        [TestCase(1.65f,0f,-45f,-70f,-25f)]
        [TestCase(3.1f,2f,145f,0f,30f)]
        public void SeatedStandingAndOffsetOriginPlaceHudAtComfortableHorizontalDistance(float eye,float floor,float yaw,float pitch,float roll)
        {
            var head=new Pose(new Vector3(2,eye,-3),Quaternion.Euler(pitch,yaw,roll));
            Assert.IsTrue(EmberdeepHudPose.TryCalculate(head,floor,Vector3.zero,out var panel));
            var heading=Quaternion.Euler(0,yaw,0)*Vector3.forward;
            var horizontal=Vector3.ProjectOnPlane(panel.position-head.position,Vector3.up);
            Assert.That(horizontal.magnitude,Is.EqualTo(.9f).Within(.00001f));
            Assert.Less(Vector3.Angle(horizontal,heading),.02f);
            Assert.That(panel.position.y,Is.EqualTo(head.position.y-.04f).Within(.00001f));
            Assert.Less(Vector3.Angle(panel.rotation*Vector3.up,Vector3.up),.02f);
            Assert.Less(Vector3.Angle(panel.rotation*Vector3.forward,heading),.02f);
        }

        [TestCase(85f)] [TestCase(89.9f)] [TestCase(90f)] [TestCase(90.1f)] [TestCase(95f)] [TestCase(-90f)]
        public void NearVerticalHeadKeepsPreviousHorizontalHeadingAcrossThePole(float pitch)
        {
            var head=new Pose(new Vector3(-2,1.2f,4),Quaternion.Euler(pitch,-120,25));
            var previous=new Vector3(3,2,0);
            Assert.IsTrue(EmberdeepHudPose.TryCalculate(head,0,previous,out var panel));
            Assert.Less(Vector3.Distance(panel.position,head.position+Vector3.right*.9f-Vector3.up*.04f),.00001f);
            Assert.Less(Vector3.Angle(panel.rotation*Vector3.forward,Vector3.right),.02f);
            Assert.Less(Vector3.Angle(panel.rotation*Vector3.up,Vector3.up),.02f);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
        public void InvalidHeadOrMissingVerticalFallbackDoesNotProducePlacement(int invalid)
        {
            var head=new Pose(new Vector3(2,1.2f,-1),Quaternion.identity);float floor=0;var previous=Vector3.forward;
            if(invalid==0)head.position.x=float.NaN;
            if(invalid==1)head.rotation=new Quaternion(0,float.PositiveInfinity,0,1);
            if(invalid==2)head.rotation=new Quaternion(0,0,0,0);
            if(invalid==3)floor=float.NaN;
            if(invalid==4)head.position.y=.44f;
            if(invalid==5){head.rotation=Quaternion.Euler(90,50,0);previous=Vector3.up;}
            if(invalid==6){head.rotation=Quaternion.Euler(-90,50,0);previous=new Vector3(float.NaN,0,1);}
            Assert.IsFalse(EmberdeepHudPose.TryCalculate(head,floor,previous,out var panel));
            Assert.AreEqual(default(Pose),panel);
        }

        [Test] public void NonUnitQuaternionIsNormalizedAndValidHeadingDoesNotNeedFallback()
        {
            var rotation=Quaternion.Euler(35,60,12);var head=new Pose(new Vector3(0,.45f,0),rotation);
            Assert.IsTrue(EmberdeepHudPose.TryCalculate(head,0,Vector3.zero,out var unit));
            head.rotation=new Quaternion(rotation.x*4,rotation.y*4,rotation.z*4,rotation.w*4);
            Assert.IsTrue(EmberdeepHudPose.TryCalculate(head,0,new Vector3(float.NaN,0,0),out var scaled));
            Assert.Less(Vector3.Distance(unit.position,scaled.position),.00001f);
            Assert.Less(Quaternion.Angle(unit.rotation,scaled.rotation),.08f);
        }
    }
}

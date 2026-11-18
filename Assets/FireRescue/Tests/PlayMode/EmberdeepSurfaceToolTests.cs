using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    public sealed class EmberdeepSurfaceToolTests
    {
        GameObject root;Material material;FireEquipmentView tool,rack;FireToolManipulation input;
        FireToolManipulation.Hand owner,other;int OwnerIndex=1;
        const float Dt=.025f;
        [TearDown] public void Cleanup(){if(root)Object.DestroyImmediate(root);if(material)Object.DestroyImmediate(material);}
        static FireToolManipulation.Hand At(Vector3 p,float c)=>new(){Valid=true,Palm=new Pose(p,Quaternion.identity),Contact=p,Closure=c};
        void Setup(FireEquipmentId id,bool body=false,int hand=1)
        {
            root=new GameObject("Surface tool regression");material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            FireEquipmentView Build(string name){var v=new GameObject(name).AddComponent<FireEquipmentView>();v.transform.SetParent(root.transform);v.Build(id,material,material,material);return v;}
            tool=Build("Tool");rack=Build("Rack");rack.gameObject.SetActive(false);OwnerIndex=hand;
            input=new FireToolManipulation();owner=At(body?new Vector3(hand==0?-.055f:.055f,id==FireEquipmentId.Extinguisher?-.07f:.025f,0):tool.Grip.position,.5f);
            other=At(Vector3.left*2,0);input.Begin(tool,hand,owner);
        }
        bool Step(int n=1,bool permitted=true)
        {bool returned=false;for(int i=0;i<n;i++)returned|=input.Step(tool,rack,OwnerIndex==0?owner:other,OwnerIndex==0?other:owner,Dt,permitted);return returned;}
        void Nozzle(){other=At(tool.Nozzle.position,.5f);Step(4);Assert.True(input.NozzleHeld);}
        [TestCase(0)] [TestCase(1)] public void SideDotsAcquireWithAnAlreadyClosedHandWithinFiftyMilliseconds(int hand)
        {
            Setup(FireEquipmentId.Extinguisher,true,hand);rack.gameObject.SetActive(true);input.Reset();
            var h=At(new Vector3(hand==0?-.055f:.055f,-.07f,0),.65f);
            Assert.AreEqual(-1,input.FindPickup(rack,hand==0?h:default,hand==1?h:default,Dt));
            Assert.AreEqual(hand,input.FindPickup(rack,hand==0?h:default,hand==1?h:default,Dt));
            input.Begin(tool,hand,h);Assert.AreEqual(FireToolRegion.Body,input.Region);Assert.True(input.HasPhysicalGrip);
        }
        [Test] public void TriggerIsRightHandOnlyAndNozzleIsNeverAPrimaryPickup()
        {
            Setup(FireEquipmentId.Extinguisher);
            Assert.True(tool.TryPrimaryGrab(tool.Grip.position,1,out var r,out _));Assert.AreEqual(FireToolRegion.Trigger,r);
            Assert.False(tool.TryPrimaryGrab(tool.Grip.position,0,out _,out _));
            Assert.False(tool.TryPrimaryGrab(tool.Nozzle.position,0,out _,out _));
            Assert.False(tool.TryPrimaryGrab(new Vector3(.055f,-.07f,0),0,out _,out _));
            Assert.False(tool.TryPrimaryGrab(new Vector3(-.055f,-.07f,0),1,out _,out _));
        }
        [Test] public void DistantOrInvalidHandsNeverAcquire()
        {
            Setup(FireEquipmentId.Extinguisher);rack.gameObject.SetActive(true);input.Reset();
            var h=At(Vector3.one*2,1);for(int i=0;i<40;i++)Assert.AreEqual(-1,input.FindPickup(rack,default,h,Dt));
            h.Contact=rack.Grip.position;h.Valid=false;for(int i=0;i<10;i++)Assert.AreEqual(-1,input.FindPickup(rack,default,h,Dt));
        }
        [TestCase(FireEquipmentId.Extinguisher)] [TestCase(FireEquipmentId.PulsePistol)]
        [TestCase(FireEquipmentId.RainFan)] [TestCase(FireEquipmentId.PressureCannon)]
        public void HalfClosureStopsWaterButFullPalmReleasesIntoHover(FireEquipmentId id)
        {
            Setup(id);Step(12);if(tool.Nozzle)Nozzle();owner.Closure=1;Step(4);Assert.True(input.Firing);
            owner.Closure=.45f;Step(30);Assert.False(input.Firing);Assert.True(input.HasPhysicalGrip);Assert.False(input.Floating);
            owner.Closure=0;Step(4);Assert.True(input.Floating);Assert.False(input.HasPhysicalGrip);Assert.False(input.TriggerValid);
            if(tool.Nozzle){Assert.True(input.NozzleHeld,"Left hand can retain nozzle while bottle floats");other.Closure=0;Step();Assert.False(input.NozzleHeld);}
            Assert.False(input.Firing);
        }
        [Test] public void HoverIsStillUntilThreeSecondsWobblesThenShrinksAndReturnsAtFive()
        {
            Setup(FireEquipmentId.Extinguisher);Step(12);owner.Closure=0;Step(4);
            var p=tool.transform.position;var q=tool.transform.rotation;
            Step(116);Assert.That(input.FloatingSeconds,Is.EqualTo(2.9f).Within(.001f));Assert.AreEqual(p,tool.transform.position);Assert.AreEqual(q,tool.transform.rotation);
            Step(20);Assert.Greater(Quaternion.Angle(q,tool.transform.rotation),.2f);Assert.AreEqual(Vector3.one,tool.transform.localScale);
            Step(56);Assert.Less(tool.transform.localScale.x,1);Assert.Greater(tool.transform.localScale.x,0);
            bool returned=false;for(int i=0;i<10 && !returned;i++)returned=Step();Assert.True(returned);Assert.False(input.Floating);Assert.AreEqual(Vector3.one,tool.transform.localScale);
        }
        [TestCase(0)] [TestCase(1)] public void EitherHandCanRegrabFloatingBodyAndResetTheReturnTimer(int hand)
        {
            Setup(FireEquipmentId.Extinguisher);Step(12);owner.Closure=0;Step(4);Step(128);
            var h=At(tool.transform.TransformPoint(new Vector3(hand==0?-.055f:.055f,-.07f,0)),.6f);
            if(hand==1)owner=h;else other=h;
            Step(2);Assert.False(input.Floating);Assert.True(input.HasPhysicalGrip);Assert.AreEqual(hand,input.Owner);Assert.Zero(input.FloatingSeconds);
        }
        [Test] public void OpenBodyHandCanRegraspTheTopWithoutReturningToRack()
        {
            Setup(FireEquipmentId.Extinguisher,true);Step(12);owner.Closure=0;Step(4);Assert.True(input.Floating);
            owner=At(tool.Grip.position,.5f);Step(2);Assert.AreEqual(FireToolRegion.Trigger,input.Region);Assert.False(input.Floating);
            Step(12);Nozzle();owner.Closure=1;Step(4);Assert.True(input.Firing);
        }
        [Test] public void PickupPreservesHeadingAndSmoothlyAlignsInsteadOfTeleporting()
        {
            Setup(FireEquipmentId.Extinguisher);tool.transform.rotation=Quaternion.Euler(12,75,8);
            owner=At(tool.Grip.position+Vector3.right*.06f,.5f);owner.Palm.rotation=Quaternion.Euler(0,-70,0);owner.HasGripFrame=true;owner.GripRotation=Quaternion.Euler(0,160,0);
            var p=tool.transform.position;var q=tool.transform.rotation;input.Begin(tool,1,owner);
            Assert.AreEqual(p,tool.transform.position);Assert.AreEqual(q,tool.transform.rotation);
            var target=owner.Contact-q*tool.PrimaryContact(input.Region,false).position;
            Step();Assert.Greater(Vector3.Distance(tool.transform.position,p),0);Assert.Less(Vector3.Distance(tool.transform.position,p),Vector3.Distance(target,p));
            Assert.Less(Quaternion.Angle(q,tool.transform.rotation),.02f);
            Step(16);Assert.Less(Vector3.Distance(target,tool.transform.position),.001f);
            owner.Palm.rotation=Quaternion.Euler(0,-40,0);Step();float turn=Quaternion.Angle(q,tool.transform.rotation);Assert.Greater(turn,0);Assert.Less(turn,30);
        }
        [TestCase(-60f)] [TestCase(0f)] [TestCase(70f)] public void NozzleFollowsKnucklesRegardlessOfPalmRoll(float roll)
        {
            Setup(FireEquipmentId.Extinguisher);Step(12);Nozzle();other.HasGripFrame=true;
            other.GripRotation=Quaternion.Euler(10,-30,roll);other.Palm.rotation=Quaternion.Euler(100,170,40);
            Step(14);Assert.Greater(Vector3.Dot(tool.Muzzle.forward,other.GripRotation*Vector3.forward),.999f);
            var contact=EmberdeepHandPose.GripContact(EmberdeepHandPoseKind.NozzleGrip,true);
            Assert.Less(Vector3.Distance(other.Contact,tool.Nozzle.TransformPoint(contact.position)),.001f);
        }
        [Test] public void SlowlyApproachingClosedLeftHandAcquiresNozzleWithoutTakingBottle()
        {
            Setup(FireEquipmentId.Extinguisher);Step(12);other=At(Vector3.left*2,.8f);Step(40);
            other.Contact=tool.Nozzle.position+Vector3.left*.08f;Step(2);Assert.True(input.NozzleHeld);Assert.AreEqual(1,input.Owner);
        }
        [Test] public void BodyHandoffAndRightTriggerRegraspPreserveBottleHeading()
        {
            Setup(FireEquipmentId.Extinguisher,true);Step(12);var q=tool.transform.rotation;
            other=At(tool.transform.TransformPoint(new Vector3(-.055f,-.07f,0)),.6f);Step(4);
            Assert.AreEqual(0,input.Owner);Assert.AreEqual(FireToolRegion.Body,input.Region);Assert.Less(Quaternion.Angle(q,tool.transform.rotation),.01f);
            owner.Closure=0;Step();owner=At(tool.Grip.position,.5f);Step(4);Assert.AreEqual(1,input.Owner);Assert.AreEqual(FireToolRegion.Trigger,input.Region);
        }
        [Test] public void InvalidTrackingAndInterfaceInterruptionCancelFireWithoutDropping()
        {
            Setup(FireEquipmentId.RainFan);Step(12);owner.Closure=1;Step(4);Assert.True(input.Firing);
            var p=tool.transform.position;owner.Valid=false;owner.Closure=0;owner.Contact=Vector3.one*20;Step(240);
            Assert.False(input.Floating);Assert.True(input.HasPhysicalGrip);Assert.AreEqual(p,tool.transform.position);Assert.False(input.Firing);
            owner=At(p+tool.PrimaryContact(input.Region,false).position,1);Step(10);Assert.False(input.Firing);
            owner.Closure=.45f;Step();owner.Closure=1;Step(4);Assert.True(input.Firing);Step(1,false);Assert.False(input.TriggerValid);
        }
        [Test] public void HoverTimerPausesWhenInputIsSuspendedAndLongFramesDoNotSkipReturn()
        {
            Setup(FireEquipmentId.RainFan);Step(12);owner.Closure=0;Step(4);Step(20);float elapsed=input.FloatingSeconds;
            Step(200,false);Assert.AreEqual(elapsed,input.FloatingSeconds);
            Assert.False(input.Step(tool,rack,other,owner,100,true));Assert.Less(input.FloatingSeconds-elapsed,.051f);
        }
        [Test] public void ExplicitStationParkHasNoAutomaticReturnOrDistantRegrasp()
        {
            Setup(FireEquipmentId.Extinguisher,true);input.Park();owner=At(Vector3.one*3,1);var p=tool.transform.position;Step(240);
            Assert.AreEqual(p,tool.transform.position);Assert.True(input.Adjusting);Assert.False(input.Floating);
        }
        [TestCase(FireEquipmentId.PulsePistol)] [TestCase(FireEquipmentId.RainFan)] [TestCase(FireEquipmentId.PressureCannon)]
        public void FrontSupportAimsWithoutStealingOwnershipAndTrackingLossCancelsCharge(FireEquipmentId id)
        {
            Setup(id);Step(12);other=At(tool.transform.TransformPoint(new Vector3(0,.015f,.12f)),0);Step();other.Closure=.5f;Step(4);
            Assert.True(input.SupportHeld);Assert.AreEqual(1,input.Owner);
            other.Contact+=Vector3.left*.10f;Step(8);Assert.Less(tool.Muzzle.forward.x,-.2f);
            owner.Closure=1;Step(4);Assert.True(input.Firing);other.Valid=false;Step();Assert.False(input.TriggerValid);Step(6);Assert.False(input.Firing);
        }
    }
}

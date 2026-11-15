using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    public sealed class EmberdeepSurfaceToolTests
    {
        GameObject root;
        Material material;
        FireEquipmentView tool,rack;
        FireToolManipulation input;
        FireToolManipulation.Hand owner,other;
        const float Dt=.025f;
        [TearDown] public void Cleanup(){if(root)Object.DestroyImmediate(root);if(material)Object.DestroyImmediate(material);}
        static FireToolManipulation.Hand At(Vector3 point,float closure)=>new(){Valid=true,Palm=new Pose(point,Quaternion.identity),Contact=point,Closure=closure};
        void Setup(FireEquipmentId id,bool body=false,int hand=1)
        {
            root=new GameObject("Surface tool regression");material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            FireEquipmentView Build(string name){var v=new GameObject(name).AddComponent<FireEquipmentView>();v.transform.SetParent(root.transform);v.Build(id,material,material,material);return v;}
            tool=Build("Tool");rack=Build("Rack");rack.gameObject.SetActive(false);
            input=new FireToolManipulation();owner=At(body?tool.transform.TransformPoint(new Vector3(0,id==FireEquipmentId.Extinguisher?-.1f:.025f,0)):tool.Grip.position,.5f);
            other=At(Vector3.left*2,0);input.Begin(tool,hand,owner);OwnerIndex=hand;
        }
        int OwnerIndex;
        bool Step(int count=1,bool permitted=true)
        {bool returned=false;for(int i=0;i<count;i++)returned|=input.Step(tool,rack,OwnerIndex==0?owner:other,OwnerIndex==0?other:owner,Dt,permitted);return returned;}
        void HoldNozzle()
        {other=At(tool.Nozzle.position,0);Step();other.Closure=.5f;Step(4);Assert.IsTrue(input.NozzleHeld);}
        [TestCase(0)] [TestCase(1)] public void ExtinguisherRearHandleLeavesPalmClearAndSqueezeKeepsFixedBarStill(int hand)
        {
            Setup(FireEquipmentId.Extinguisher,false,hand);
            var local=tool.PrimaryContact(FireToolRegion.Trigger,hand==0);
            var contacts=new EmberdeepHandContact();contacts.Add(tool.transform);contacts.Prepare();
            var palm=tool.transform.TransformPoint(local.position);
            Assert.Less(Vector3.Distance(palm,contacts.Resolve(palm,.007f)),.0001f,"Palm must sit outside the handles");
            Assert.Less(local.position.z,-.055f,"Wrap behind the bottle shoulder, not over the valve block");
            Assert.IsTrue(tool.TryGrabRegion(palm,out var region,out _));Assert.AreEqual(FireToolRegion.Trigger,region);
            var fixedBar=tool.transform.Find("Fixed carrying handle");var moving=tool.transform.Find("Squeeze handle");
            var barPose=fixedBar.localPosition;var rest=moving.localPosition;
            tool.Animate(1,0,true,.02f);
            Assert.AreEqual(barPose,fixedBar.localPosition);Assert.That(rest.y-moving.localPosition.y,Is.EqualTo(.01f).Within(.0001f));
            tool.Animate(1,0,false,.1f);Assert.Less(Vector3.Distance(rest,moving.localPosition),.0001f);
        }
        [TestCase(0)] [TestCase(1)] public void ForwardSupportPalmStaysOutsideTheCannonGrip(int hand)
        {
            Setup(FireEquipmentId.PressureCannon,false,hand);tool.CaptureSupport(Vector3.zero);
            var contact=EmberdeepHandPose.GripContact(EmberdeepHandPoseKind.ForwardSupport,hand==0);
            var palm=tool.SupportGrip.TransformPoint(contact.position);
            var contacts=new EmberdeepHandContact();contacts.Add(tool.transform);contacts.Prepare();
            Assert.Less(Vector3.Distance(palm,contacts.Resolve(palm,.007f)),.0001f,
                "Fixed support palm must not start inside a solid part");
        }
        [TestCase(0)] [TestCase(1)] public void RepeatedFullOpeningStopsFireWithoutDroppingAndCanSqueezeAgain(int hand)
        {
            Setup(FireEquipmentId.Extinguisher,false,hand);Step();HoldNozzle();
            for(int cycle=0;cycle<4;cycle++)
            {
                owner.Closure=1;Step(4);Assert.IsTrue(input.Firing);
                owner.Closure=0;Assert.IsFalse(Step(30));Assert.IsFalse(input.Firing);
                Assert.IsFalse(input.TriggerValid,"Full open must cancel any stored charge");
                Assert.IsTrue(input.HasPhysicalGrip);Assert.IsFalse(input.Adjusting);
                owner.Contact+=Vector3.up*.02f;owner.Palm.position=owner.Contact;Step();
                Assert.Less(Vector3.Distance(owner.Contact,tool.Grip.position),.001f);
            }
        }
        [TestCase(0)] [TestCase(1)] public void NozzlePhysicalAndDisplayedPalmUseOneCanonicalFrame(int hand)
        {
            Setup(FireEquipmentId.Extinguisher,false,hand);Step();HoldNozzle();
            other.HasGripFrame=true;
            var contact=EmberdeepHandPose.GripContact(EmberdeepHandPoseKind.NozzleGrip,hand!=0);
            foreach(var turn in new[]{Quaternion.identity,Quaternion.Euler(20,40,10),Quaternion.Euler(-10,-35,25)})
            {
                other.Contact=tool.transform.position+new Vector3(hand==0?.13f:-.13f,.05f,.18f);
                other.GripRotation=turn*contact.rotation;Step();
                Assert.Less(Quaternion.Angle(turn,tool.Nozzle.rotation),.01f);
                Assert.Less(Vector3.Distance(other.Contact,tool.Nozzle.TransformPoint(contact.position)),.001f);
            }
        }
        [TestCase(0)] [TestCase(1)] public void CanonicalPrimaryContactFollowsPhysicalPalmWithoutAnAcquisitionAngleOffset(int hand)
        {
            Setup(FireEquipmentId.Extinguisher,false,hand);
            owner.HasGripFrame=true;var contact=tool.PrimaryContact(FireToolRegion.Trigger,hand==0);
            var turn=Quaternion.Euler(15,-20,10);owner.GripRotation=turn*contact.rotation;Step();
            Assert.Less(Quaternion.Angle(turn,tool.transform.rotation),.01f);
            Assert.Less(Vector3.Distance(owner.Contact,tool.transform.TransformPoint(contact.position)),.001f);
            owner.Closure=0;Step(25);Assert.IsTrue(input.HasPhysicalGrip);
        }
        [TestCase(FireEquipmentId.Extinguisher)] [TestCase(FireEquipmentId.PulsePistol)]
        [TestCase(FireEquipmentId.RainFan)] [TestCase(FireEquipmentId.PressureCannon)]
        public void BodyGripCannotFireButCanRegraspFunctionalHandle(FireEquipmentId id)
        {
            Setup(id,true);Assert.AreEqual(FireToolRegion.Body,input.Region);Step();owner.Closure=1;Step(6);Assert.IsFalse(input.Firing);
            input.Park();owner.Closure=0;Step();Assert.IsTrue(input.Adjusting);
            owner=At(tool.Grip.position,.5f);Step(4);Assert.AreEqual(FireToolRegion.Trigger,input.Region);Assert.IsFalse(input.Adjusting);
            if(tool.Nozzle)HoldNozzle();
            Step(3);owner.Closure=1;Step(4);Assert.IsTrue(input.Firing);
            owner.Closure=.5f;Step();Assert.IsFalse(input.Firing);Assert.IsFalse(input.Adjusting);
        }
        [TestCase(0)] [TestCase(1)] public void RackReturnRequiresFullOpenDwellAndWorksWithHiddenRack(int hand)
        {
            Setup(FireEquipmentId.PulsePistol,false,hand);other.Closure=.5f;owner.Contact+=Vector3.forward*.4f;owner.Palm.position=owner.Contact;Step(2);
            owner.Closure=0;Assert.IsFalse(Step(25));Assert.IsFalse(input.Adjusting,"Opening away from rack retains the tool");
            owner=At(tool.Grip.position,.5f);Step(4);
            owner.Contact-=Vector3.forward*.4f;owner.Palm.position=owner.Contact;Step(2);
            Assert.IsFalse(Step(25),"Half grip near rack is not a release");
            owner.Closure=0;Assert.IsFalse(Step(5));bool returned=false;
            for(int i=0;i<20 && !returned;i++)returned=Step();Assert.IsTrue(returned);
        }
        [TestCase(0)] [TestCase(1)] public void BothOpenHandsKeepToolHeldAwayFromRack(int hand)
        {
            Setup(FireEquipmentId.PressureCannon,false,hand);
            owner.Contact+=Vector3.forward*.5f;owner.Palm.position=owner.Contact;Step(2);
            owner.Closure=0;other.Closure=0;Assert.IsFalse(Step(12));
            other.Closure=.5f;Assert.IsFalse(Step(30),"One open hand retains the tool");
            Assert.IsFalse(input.Adjusting);
            other.Closure=0;Assert.IsFalse(Step(12),"Reclosing either hand resets the dwell");
            bool returned=false;for(int i=0;i<30;i++)returned|=Step();Assert.IsFalse(returned);Assert.IsTrue(input.HasPhysicalGrip);
        }
        [TestCase(0)] [TestCase(1)] public void RelaxedHoldSurvivesMissingTrackingBlockedInputAndLongFrames(int hand)
        {
            Setup(FireEquipmentId.RainFan,false,hand);
            owner.Contact+=Vector3.forward*.5f;owner.Palm.position=owner.Contact;Step(2);
            owner.Closure=0;other.Closure=0;other.Valid=false;
            Assert.IsFalse(Step(30),"A missing other hand is not an open hand");
            other.Valid=true;Assert.IsFalse(Step(10));Step(1,false);
            Assert.IsFalse(Step(10),"Interface interruption resets accumulated dwell");
            other.Closure=.5f;Step();other.Closure=0;
            Assert.IsFalse(input.Step(tool,rack,hand==0?owner:other,hand==0?other:owner,10,true),"A long frame cannot instantly release");
            Assert.IsFalse(Step(16));bool returned=false;
            for(int i=0;i<30;i++)returned|=Step();Assert.IsFalse(returned);Assert.IsTrue(input.HasPhysicalGrip);
        }
        [Test] public void ClosedHandCannotPickupAndPickupCannotFire()
        {
            Setup(FireEquipmentId.PulsePistol);input.Reset();rack.gameObject.SetActive(true);owner.Closure=1;
            for(int i=0;i<10;i++)Assert.AreEqual(-1,input.FindPickup(rack,other,owner,Dt));
            owner.Closure=0;input.FindPickup(rack,other,owner,Dt);owner.Closure=.5f;
            int picked=-1;for(int i=0;i<4;i++){int value=input.FindPickup(rack,other,owner,Dt);if(value>=0)picked=value;}
            Assert.AreEqual(1,picked);input.Begin(tool,picked,owner);owner.Closure=1;Step(10);Assert.IsFalse(input.Firing);
        }
        [Test] public void SlightlyEarlyGraspCanCatchHandleButAnOldFistCannot()
        {
            Setup(FireEquipmentId.Extinguisher);input.Reset();rack.gameObject.SetActive(true);
            owner=At(Vector3.right*.5f,0);input.FindPickup(rack,other,owner,Dt);owner.Closure=.5f;
            for(int i=0;i<4;i++)Assert.AreEqual(-1,input.FindPickup(rack,other,owner,Dt));
            owner.Contact=rack.Grip.position+new Vector3(.06f,.04f,0);
            Assert.AreEqual(1,input.FindPickup(rack,other,owner,Dt));
            input.Reset();owner=At(Vector3.right*.5f,0);input.FindPickup(rack,other,owner,Dt);owner.Closure=.5f;
            for(int i=0;i<20;i++)input.FindPickup(rack,other,owner,Dt);
            owner.Contact=rack.Grip.position;Assert.AreEqual(-1,input.FindPickup(rack,other,owner,Dt));
        }
        [Test] public void TopGripSnapsToFunctionalAnchorAndBodyKeepsItsOwnPose()
        {
            Setup(FireEquipmentId.Extinguisher);
            owner=At(tool.Grip.position+new Vector3(.06f,.03f,0),.5f);input.Begin(tool,1,owner);Step();
            Assert.AreEqual(FireToolRegion.Trigger,input.Region);
            Assert.Less(Vector3.Distance(tool.Grip.position,owner.Contact),.001f);
            owner=At(tool.transform.TransformPoint(new Vector3(0,-.13f,-.05f)),.5f);input.Begin(tool,1,owner);
            Assert.AreEqual(FireToolRegion.Body,input.Region);Assert.NotNull(tool.SurfaceGrip);
            Assert.Greater(Vector3.Distance(tool.SurfaceGrip.position,tool.Grip.position),.15f);
        }
        [Test] public void ExtinguisherRequiresNozzleAndUsesItsActualPose()
        {
            Setup(FireEquipmentId.Extinguisher);Step();owner.Closure=1;Step(4);Assert.IsFalse(input.Firing);
            owner.Closure=.5f;HoldNozzle();other.Contact+=Vector3.left*.15f;other.Palm.rotation=Quaternion.Euler(0,-35,0);Step();
            Assert.That(Vector3.Distance(other.Contact,tool.Nozzle.TransformPoint(EmberdeepHandPose.GripContact(EmberdeepHandPoseKind.NozzleGrip,true).position)),Is.LessThan(.001f));
            Assert.That(Vector3.Angle(tool.Muzzle.forward,Quaternion.Euler(0,-35,0)*Vector3.forward),Is.LessThan(.1f));
            owner.Closure=1;Step(4);Assert.IsTrue(input.Firing);
            other.Valid=false;Step();Assert.IsFalse(input.Firing);Assert.IsFalse(input.TriggerValid);
            other.Valid=true;Step(5);Assert.IsFalse(input.NozzleHeld,"Returning closed hand cannot reacquire the nozzle");
        }
        [Test] public void TrackingAndInterfaceLossCancelFireWithoutMovingOrDroppingTool()
        {
            Setup(FireEquipmentId.RainFan);Step();owner.Closure=1;Step(4);Assert.IsTrue(input.Firing);
            var pose=tool.transform.position;owner.Valid=false;owner.Contact=Vector3.one*20;Step(20);
            Assert.IsFalse(input.Firing);Assert.AreEqual(pose,tool.transform.position);
            owner=At(pose+tool.Grip.localPosition,1);Step(6);Assert.IsFalse(input.Firing);
            owner.Closure=.5f;Step();owner.Closure=1;Step(4);Assert.IsTrue(input.Firing);
            Step(1,false);Assert.IsFalse(input.Firing);Assert.IsFalse(input.TriggerValid);
        }
        [Test] public void ExplicitlyParkedToolRequiresNearbyRegrip()
        {
            Setup(FireEquipmentId.Extinguisher,true);input.Park();owner.Closure=0;Step();var position=tool.transform.position;
            owner=At(Vector3.one*5,1);Step(10);Assert.AreEqual(position,tool.transform.position);Assert.IsTrue(input.Adjusting);
        }
        [TestCase(FireEquipmentId.PulsePistol)] [TestCase(FireEquipmentId.RainFan)] [TestCase(FireEquipmentId.PressureCannon)]
        public void FrontSupportSteersWithoutMovingThePrimaryGrasp(FireEquipmentId id)
        {
            Setup(id);other=At(tool.transform.TransformPoint(new Vector3(0,.015f,.12f)),0);Step();
            other.Closure=.5f;Step(4);Assert.IsTrue(input.SupportHeld);
            var primary=tool.Grip.position;other.Contact+=Vector3.left*.12f;Step();
            Assert.That(Vector3.Distance(primary,tool.Grip.position),Is.LessThan(.001f));
            Assert.Less(tool.Muzzle.forward.x,-.2f);other.Closure=0;Step();Assert.IsFalse(input.SupportHeld);
        }
        [TestCase(FireEquipmentId.PulsePistol)] [TestCase(FireEquipmentId.RainFan)] [TestCase(FireEquipmentId.PressureCannon)]
        public void LostSupportingHandCancelsRatherThanReleasingAChargedShot(FireEquipmentId id)
        {
            Setup(id);other=At(tool.transform.TransformPoint(new Vector3(0,.015f,.12f)),0);Step();
            other.Closure=.5f;Step(4);owner.Closure=1;Step(4);Assert.IsTrue(input.Firing);
            other.Valid=false;Step();Assert.IsFalse(input.Firing);Assert.IsFalse(input.TriggerValid,"Caller must cancel charge, not interpret this as trigger release");
            Step(5);Assert.IsFalse(input.Firing,"A closed primary fist cannot restart firing after the support was lost");
        }
    }
}

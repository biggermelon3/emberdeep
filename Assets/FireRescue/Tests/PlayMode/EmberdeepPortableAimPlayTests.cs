using System.Linq;
using CloudGarden.Integration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem; // WriteValueIntoEvent

namespace FireRescue.Tests
{
    public sealed class EmberdeepPortableAimPlayTests
    {
        GameObject head;
        [SetUp] public void Setup(){head=new GameObject("Portable aim head");head.transform.position=Vector3.up*1.65f;}
        [TearDown] public void Cleanup()=>Object.DestroyImmediate(head);
        [TestCase(true)] [TestCase(false)]
        public void LiveJointsProduceAimWithoutAnySdkPointerOrPointerUpdateEvent(bool left)
        {
            var input=new PortableJointAim();var index=new Vector3(left?-.2f:.2f,1.3f,.4f);
            Assert.IsTrue(input.TryRead(10,head.transform,left,index,index+Vector3.right*.02f,.04f,.02f,out var pose));
            Assert.That(Vector3.Distance(pose.position,index+Vector3.right*.01f),Is.LessThan(.001f),"The ray starts at the thumb-index web");
            Assert.Less(Vector3.Angle(pose.rotation*Vector3.forward,PortableJointAim.ShoulderRay(head.transform,left,pose.position)),.01f,
                "Without a wrist joint the ray is the shoulder ray");
            Assert.IsTrue(input.TryRead(10,head.transform,left,index,index,.01f,.02f,out var sameFrame));
            Assert.AreEqual(pose,sameFrame,"Multiple UI/gameplay reads in a frame must use the same ray");
        }
        [Test] public void ShoulderIsTheInteractionSdkEstimateAndIgnoresHeadPitch()
        {
            var expected=head.transform.position+new Vector3(PortableJointAim.ShoulderOut,-PortableJointAim.ShoulderDrop,-PortableJointAim.ShoulderBack);
            Assert.That(Vector3.Distance(PortableJointAim.Shoulder(head.transform,false),expected),Is.LessThan(.0001f));
            Assert.That(PortableJointAim.Shoulder(head.transform,true).x,Is.EqualTo(-PortableJointAim.ShoulderOut).Within(.0001f));
            foreach(float pitch in new[]{35f,89.9f,-89.9f})
            {
                head.transform.rotation=Quaternion.Euler(pitch,0,0);
                Assert.That(Vector3.Distance(PortableJointAim.Shoulder(head.transform,false),expected),Is.LessThan(.001f),"pitch "+pitch);
            }
        }
        // Right hand in front of the chest; the hand axis (wrist to index knuckle) tilted up by `up` degrees.
        static readonly Vector3 Index=new(.2f,1.3f,.4f),Thumb=Index+Vector3.right*.02f;
        static Vector3 Wrist(float up,float yaw=0)=>Index-Quaternion.Euler(-up,yaw,0)*Vector3.forward*.09f;
        Vector3 Settle(PortableJointAim aim,Vector3 wrist,float pinch,ref int frame,int frames=200)
        {
            Pose pose=default;
            for(int i=0;i<frames;i++)Assert.IsTrue(aim.TryRead(frame++,head.transform,false,wrist,Index,Thumb,pinch,.02f,out pose));
            return pose.rotation*Vector3.forward;
        }
        [Test] public void DirectionBlendsTheShoulderRayWithTheHandAxis()
        {
            var aim=new PortableJointAim();var wrist=Wrist(20);
            Assert.IsTrue(aim.TryRead(1,head.transform,false,wrist,Index,Thumb,.05f,.02f,out var pose));
            var shoulder=PortableJointAim.ShoulderRay(head.transform,false,PortableJointAim.Web(Index,Thumb));
            var hand=(Index-wrist).normalized;
            Assert.Less(Vector3.Angle(shoulder,hand),PortableJointAim.HandTrustFull);
            var expected=Vector3.Slerp(shoulder,hand,PortableJointAim.HandBlend);
            Assert.Less(Vector3.Angle(pose.rotation*Vector3.forward,expected),.05f);
            Assert.That(aim.Last.HandWeight,Is.EqualTo(PortableJointAim.HandBlend).Within(1e-5f));
            Assert.That(Vector3.Distance(pose.position,PortableJointAim.Web(Index,Thumb)),Is.LessThan(.0001f));
        }
        [Test] public void TiltingTheWristAloneTurnsTheRay()
        {
            int frame=0;var aim=new PortableJointAim();
            var low=Settle(aim,Wrist(0),.05f,ref frame);var high=Settle(aim,Wrist(20),.05f,ref frame);
            Assert.That(Vector3.Angle(low,high),Is.InRange(PortableJointAim.HandBlend*20*.7f,PortableJointAim.HandBlend*20*1.3f),
                "Same knuckles, wrist raised 20 degrees: the ray rises by about the hand blend's share");
            Assert.Greater(high.y,low.y);
        }
        [Test] public void ABentBackWristFallsBackToTheShoulderRay()
        {
            var aim=new PortableJointAim();var back=Index+Vector3.forward*.09f; // hand axis points back at the player
            Assert.IsTrue(aim.TryRead(1,head.transform,false,back,Index,Thumb,.05f,.02f,out var pose));
            Assert.Zero(aim.Last.HandWeight);
            Assert.Less(Vector3.Angle(pose.rotation*Vector3.forward,PortableJointAim.ShoulderRay(head.transform,false,pose.position)),.05f);
        }
        float Swing(float pinch)
        {
            int frame=0;var aim=new PortableJointAim();var start=Settle(aim,Wrist(0),.05f,ref frame);
            Pose pose=default;
            for(int i=0;i<4;i++)Assert.IsTrue(aim.TryRead(frame++,head.transform,false,Wrist(0,8),Index,Thumb,pinch,.02f,out pose));
            return Vector3.Angle(start,pose.rotation*Vector3.forward);
        }
        [Test] public void OnlyAClosingPinchHoldsTheDirection()
        {
            float open=Swing(.05f),ready=Swing(PortableJointAim.HoldFrom),closing=Swing(.02f);
            Assert.Greater(open,.5f,"An open hand's ray follows a wrist turn");
            Assert.That(ready,Is.EqualTo(open).Within(.001f),"A pinch-ready hand at the hold threshold is not held at all");
            Assert.Less(closing,open*.5f,"Near the click the pinch dip is held back");
            var aim=new PortableJointAim();
            aim.TryRead(1,head.transform,false,Wrist(0),Index,Thumb,.05f,.02f,out _);Assert.Zero(aim.Last.Hold);
            aim.TryRead(2,head.transform,false,Wrist(0),Index,Thumb,.02f,.02f,out _);Assert.That(aim.Last.Hold,Is.EqualTo(1).Within(1e-5f));
        }
        [Test] public void ReleasingThePinchGlidesBackWithoutAOneFrameSnap()
        {
            int frame=0;var aim=new PortableJointAim();
            var target=Settle(new PortableJointAim(),Wrist(0,8),.05f,ref frame);frame=0;
            Settle(aim,Wrist(0),.05f,ref frame);
            var held=Settle(aim,Wrist(0,8),.02f,ref frame,10);
            float gap=Vector3.Angle(held,target);Assert.Greater(gap,1,"Still held when the pinch opens");
            var previous=held;float largest=0;
            for(int i=0;i<30;i++)
            {
                Assert.IsTrue(aim.TryRead(frame++,head.transform,false,Wrist(0,8),Index,Thumb,.05f,.02f,out var pose));
                var now=pose.rotation*Vector3.forward;largest=Mathf.Max(largest,Vector3.Angle(previous,now));previous=now;
            }
            Assert.Less(largest,gap*.45f,"The catch-up is spread over several frames");
            Assert.Less(Vector3.Angle(Settle(aim,Wrist(0,8),.05f,ref frame),target),.1f);
        }
        [Test] public void AimLogLineNamesTheSourceWithOriginOffsetAndBlend()
        {
            var sample=new EmberdeepHandRay.Sample(true,new Ray(new Vector3(0,0,.08f),Vector3.forward),.04f){Source=MetaShadowPlayer.AimSource.Fallback};
            var detail=new MetaShadowPlayer.MenuAimDetail{Portable=true,Source=MetaShadowPlayer.AimSource.Fallback,WebValid=true,Web=new Vector3(0,0,.08f),
                Blend=new PortableJointAim.Detail{HandWeight=.5f,Shoulder=Vector3.forward,Hand=Vector3.forward}};
            var line=EmberdeepHandRay.Describe(false,new Pose(Vector3.zero,Quaternion.identity),sample,detail);
            StringAssert.StartsWith("EMBERDEEP_AIM source=fallback hand=R aim=joints select=joints",line);
            StringAssert.Contains("origin_wrist_mm=80",line);StringAssert.Contains("origin_web_mm=0",line);
            StringAssert.Contains("blend=0.50/0.50",line);
            sample.Source=MetaShadowPlayer.AimSource.System;sample.SystemSelect=true;
            detail.Source=MetaShadowPlayer.AimSource.System;detail.SystemSelect=true;detail.Select=.25f;detail.SelectReady=true;
            detail.RuntimeAimValid=true;detail.RuntimeAimFlags=3;detail.RuntimeAim=new Pose(new Vector3(0,0,.05f),Quaternion.Euler(-10,0,0));
            line=EmberdeepHandRay.Describe(false,new Pose(Vector3.zero,Quaternion.identity),sample,detail);
            StringAssert.StartsWith("EMBERDEEP_AIM source=system hand=R aim=openxr_hand_interaction select=system",line);
            StringAssert.Contains("select_value=0.25 select_pressed=0 select_ready=1",line);
            StringAssert.Contains("tracking=0x3 smooth_mm=30 smooth_deg=10.0",line);
            detail.Portable=false;
            StringAssert.Contains("aim=meta_pointer",EmberdeepHandRay.Describe(true,default,sample,detail));
        }
        // PortableSystemAim: the runtime's XR_EXT_hand_interaction aim on the portable rig.
        static PortableSystemAim.Raw SystemRaw(Vector3 position,Quaternion rotation,float select=0)=>new()
            {Tracked=true,Aim=new Pose(position,rotation),TrackingState=3,SelectAvailable=true,Select=select,SelectReady=true};
        [Test] public void SystemAimIsTheRuntimePoseInWorldSpace()
        {
            head.transform.SetPositionAndRotation(new Vector3(1,0,-2),Quaternion.Euler(0,90,0));
            var aim=new PortableSystemAim();var local=new Pose(new Vector3(.1f,1.2f,.3f),Quaternion.Euler(20,10,0));
            Assert.AreEqual(PortableSystemAim.Report.Tracked,aim.Sample(1,true,SystemRaw(local.position,local.rotation),head.transform,1,.016f,out var reading));
            Assert.That(Vector3.Distance(reading.Aim.position,head.transform.TransformPoint(local.position)),Is.LessThan(1e-5f));
            Assert.Less(Vector3.Angle(reading.Aim.rotation*Vector3.forward,head.transform.rotation*local.rotation*Vector3.forward),.01f);
            Assert.AreEqual(reading.Aim.position,reading.RawAim.position,"The first sample is the runtime's pose as is");
        }
        [Test] public void SystemAimSmoothingIsLight()
        {
            var aim=new PortableSystemAim();int frame=0;
            for(int i=0;i<30;i++)aim.Sample(1,true,SystemRaw(Vector3.zero,Quaternion.identity),head.transform,frame++,1/72f,out _);
            var turned=Quaternion.Euler(0,10,0);PortableSystemAim.Reading reading=default;
            for(int i=0;i<11;i++)aim.Sample(1,true,SystemRaw(Vector3.zero,turned),head.transform,frame++,1/72f,out reading); // ~150 ms
            float gap=Vector3.Angle(reading.Aim.rotation*Vector3.forward,head.transform.rotation*turned*Vector3.forward);
            Assert.Less(gap,1f,"A 10 degree turn has caught up to within a degree in 150 ms");
            for(int i=0;i<60;i++)aim.Sample(1,true,SystemRaw(Vector3.zero,turned),head.transform,frame++,1/72f,out reading);
            Assert.Less(Vector3.Angle(reading.Aim.rotation*Vector3.forward,head.transform.rotation*turned*Vector3.forward),.05f);
        }
        [Test] public void FallbackOnlyUntilTheRuntimeHasAimedThenItsLossMeansNoRay()
        {
            var aim=new PortableSystemAim();var raw=SystemRaw(Vector3.zero,Quaternion.identity);var untracked=raw;untracked.Tracked=false;
            Assert.AreEqual(PortableSystemAim.Report.Unreported,aim.Sample(0,false,default,head.transform,1,.02f,out _),"No device: fall back");
            Assert.AreEqual(PortableSystemAim.Report.Unreported,aim.Sample(0,true,untracked,head.transform,2,.02f,out _),"A device that never tracked: fall back");
            Assert.AreEqual(PortableSystemAim.Report.Tracked,aim.Sample(0,true,raw,head.transform,3,.02f,out _));
            Assert.AreEqual(PortableSystemAim.Report.Lost,aim.Sample(0,true,untracked,head.transform,4,.02f,out _),"After aiming, the runtime decides");
            Assert.AreEqual(PortableSystemAim.Report.Unreported,aim.Sample(1,true,untracked,head.transform,4,.02f,out _),"Per hand");
            Assert.AreEqual(PortableSystemAim.Report.Unreported,aim.Sample(0,false,default,head.transform,5,.02f,out _),"Device gone: fall back");
            Assert.AreEqual(PortableSystemAim.Report.Unreported,aim.Sample(0,true,untracked,head.transform,6,.02f,out _),"A returning device proves itself again");
            Assert.AreEqual(PortableSystemAim.Report.Unreported,aim.Sample(0,true,raw,null,7,.02f,out _),"No tracking space");
        }
        [Test] public void SystemSelectCountsOnceProvenAndHasHysteresis()
        {
            var aim=new PortableSystemAim();int frame=0;
            PortableSystemAim.Reading Select(float value){aim.Sample(1,true,SystemRaw(Vector3.zero,Quaternion.identity,value),head.transform,frame++,.02f,out var r);return r;}
            var reading=Select(.6f);Assert.IsTrue(reading.SelectAvailable);Assert.IsFalse(reading.SelectProven,"A runtime that never closes the select cannot own clicks");
            Assert.IsFalse(reading.SelectPressed);
            reading=Select(PortableSystemAim.SelectClose);Assert.IsTrue(reading.SelectProven);Assert.IsTrue(reading.SelectPressed);
            Assert.IsTrue(Select(.65f).SelectPressed,"Held above SelectOpen");
            Assert.IsFalse(Select(PortableSystemAim.SelectOpen).SelectPressed,"Released at SelectOpen");
            Assert.IsFalse(Select(.7f).SelectPressed,"Reopened: closes only at SelectClose again");
            Assert.IsTrue(aim.Sample(0,true,SystemRaw(Vector3.zero,Quaternion.identity,0),head.transform,frame,.02f,out var other)==PortableSystemAim.Report.Tracked && other.SelectProven,
                "Proof covers both hands");
        }
        [Test] public void SameFrameReadsShareOneSystemReading()
        {
            var aim=new PortableSystemAim();
            aim.Sample(1,true,SystemRaw(Vector3.zero,Quaternion.identity),head.transform,5,.02f,out _);
            Assert.AreEqual(PortableSystemAim.Report.Tracked,aim.Read(false,head.transform,5,.02f,out var cached),"Read reuses this frame's sample");
            Assert.AreEqual(Vector3.zero,cached.RawAim.position-head.transform.position);
        }
        // One state event, the way the OpenXR provider delivers it (also writes the 1-bit ready flag).
        static void WriteAim(UnityEngine.InputSystem.InputDevice device,bool tracked,int state,Vector3 position,Quaternion rotation,float select,bool ready)
        {
            using(UnityEngine.InputSystem.LowLevel.StateEvent.From(device,out var eventPtr))
            {
                device.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("pointer/isTracked").WriteValueIntoEvent(tracked?1f:0f,eventPtr);
                device.TryGetChildControl<UnityEngine.InputSystem.Controls.IntegerControl>("pointer/trackingState").WriteValueIntoEvent(state,eventPtr);
                device.TryGetChildControl<UnityEngine.InputSystem.Controls.Vector3Control>("pointer/position").WriteValueIntoEvent(position,eventPtr);
                device.TryGetChildControl<UnityEngine.InputSystem.Controls.QuaternionControl>("pointer/rotation").WriteValueIntoEvent(rotation,eventPtr);
                device.TryGetChildControl<UnityEngine.InputSystem.Controls.AxisControl>("pointerActivateValue").WriteValueIntoEvent(select,eventPtr);
                device.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("pointerActivateReady").WriteValueIntoEvent(ready?1f:0f,eventPtr);
                UnityEngine.InputSystem.LowLevel.InputState.Change(device,eventPtr);
            }
        }
        [Test] public void HandInteractionDeviceLeavesTheControllerUsagesAndIsReadAsTheSystemAim()
        {
            if(!UnityEngine.InputSystem.InputSystem.ListLayouts().Contains(PortableSystemAim.Layout))Assert.Ignore("OpenXR's HandInteraction layout is not registered in this editor");
            var probe=new UnityEngine.InputSystem.InputAction(binding:"<XRController>{LeftHand}/devicePosition");
            UnityEngine.InputSystem.InputDevice device=null;
            try
            {
                device=UnityEngine.InputSystem.InputSystem.AddDevice(PortableSystemAim.Layout,"Emberdeep test hand interaction");
                UnityEngine.InputSystem.InputSystem.SetDeviceUsage(device,UnityEngine.InputSystem.CommonUsages.LeftHand);
                PortableSystemAim.Install();
                Assert.AreEqual(PortableSystemAim.Usages[0],device.usages[0].ToString(),"The hand usage is traded for our own");
                Assert.AreEqual(1,device.usages.Count);
                probe.Enable();Assert.Zero(probe.controls.Count,"<XRController>{LeftHand} bindings (ISDK controllers) no longer see the hand");
                Assert.AreSame(device,PortableSystemAim.Device(true));Assert.IsNull(PortableSystemAim.Device(false));
                UnityEngine.InputSystem.InputSystem.SetDeviceUsage(device,UnityEngine.InputSystem.CommonUsages.LeftHand);
                Assert.AreEqual(PortableSystemAim.Usages[0],device.usages[0].ToString(),"A usage put back later is traded again");
                WriteAim(device,true,3,new Vector3(.1f,1.2f,.3f),Quaternion.Euler(30,0,0),.9f,true);
                Assert.IsTrue(PortableSystemAim.TryReadRaw(true,out var raw),"The adopted device reads");
                Assert.IsTrue(raw.Tracked,"isTracked is a byte, not a pressed button");Assert.AreEqual(3,raw.TrackingState);
                Assert.IsTrue(raw.SelectReady);
                Assert.That(Vector3.Distance(raw.Aim.position,new Vector3(.1f,1.2f,.3f)),Is.LessThan(1e-5f));
                Assert.Less(Quaternion.Angle(raw.Aim.rotation,Quaternion.Euler(30,0,0)),.01f);
                Assert.IsTrue(raw.SelectAvailable);Assert.That(raw.Select,Is.EqualTo(.9f).Within(1e-5f));
                WriteAim(device,false,0,new Vector3(.1f,1.2f,.3f),Quaternion.identity,0,false);
                Assert.IsTrue(PortableSystemAim.TryReadRaw(true,out raw));Assert.IsFalse(raw.Tracked);Assert.IsFalse(raw.SelectReady);
                WriteAim(device,true,1,new Vector3(.1f,1.2f,.3f),Quaternion.identity,0,true); // position only
                Assert.IsTrue(PortableSystemAim.TryReadRaw(true,out raw));Assert.IsFalse(raw.Tracked,"Both position and rotation must be tracked");
                UnityEngine.InputSystem.InputSystem.RemoveDevice(device);device=null;
                Assert.IsNull(PortableSystemAim.Device(true),"A removed device is forgotten");
                Assert.IsFalse(PortableSystemAim.TryReadRaw(true,out _));
            }
            finally{probe.Dispose();if(device!=null)UnityEngine.InputSystem.InputSystem.RemoveDevice(device);}
        }
        [Test] public void InvalidJointsAndTrackingResetCannotReuseAnOldRayOrDragRecoveryTowardIt()
        {
            var input=new PortableJointAim();var near=new Vector3(.2f,1.3f,.4f);var far=new Vector3(-.2f,1.4f,.6f);
            Assert.IsTrue(input.TryRead(1,head.transform,false,near,near,.04f,.02f,out _));
            Assert.IsFalse(input.TryRead(2,head.transform,false,new Vector3(float.NaN,0,0),near,.04f,.02f,out _));
            Assert.IsTrue(input.TryRead(3,head.transform,false,far,far,.04f,.02f,out var recovered));
            Assert.AreEqual(far,recovered.position);
            input.Reset();
            Assert.IsTrue(input.TryRead(4,head.transform,false,near,near,.04f,.02f,out recovered));
            Assert.AreEqual(near,recovered.position);
        }
        [Test] public void DroppingOneHandDoesNotInvalidateTheOtherHand()
        {
            var left=new PortableJointAim();var right=new PortableJointAim();var point=new Vector3(.2f,1.2f,.5f);
            Assert.IsTrue(right.TryRead(1,head.transform,false,point,point,.04f,.02f,out var expected));
            left.Reset();
            Assert.IsTrue(right.TryRead(1,head.transform,false,point,point,.04f,.02f,out var actual));
            Assert.AreEqual(expected,actual);
        }
        [Test] public void FocusAndPauseMustBothRecoverBeforeHandsCanResume()
        {
            var provider=head.AddComponent<MetaShadowPlayer>();provider.enabled=false;
            provider.Player=head.AddComponent<PhysicalAssembly.MetaXR.MetaPlayerRig>();
            Assert.IsTrue(provider.HeadReady);
            provider.SendMessage("OnApplicationPause",true);
            provider.SendMessage("OnApplicationFocus",true);
            Assert.IsFalse(provider.HeadReady,"A focus event must not override an outstanding pause");
            provider.SendMessage("OnApplicationFocus",false);
            provider.SendMessage("OnApplicationPause",false);
            Assert.IsFalse(provider.HeadReady,"A resume event must not override lost focus");
            provider.SendMessage("OnApplicationFocus",true);Assert.IsTrue(provider.HeadReady);
        }
        static PortableXRHandInput.Sample Native(bool tips=true)
        {
            var at=new Vector3(.2f,1.3f,.4f);
            return PortableXRHandInput.FromJoints(true,new Pose(at,Quaternion.identity),true,at,true,at+Vector3.right*.02f,
                tips,at,true,at+Vector3.right*.04f);
        }
        [Test] public void NativeTipLossRetainsAimButCannotProvidePinchOrOldJointValues()
        {
            var input=new PortableXRHandInput();input.Record(0,Native(),10,1);
            Assert.IsTrue(input.TryReadFresh(0,10,1,out var sample));Assert.IsTrue(sample.PinchAvailable);
            input.Record(0,Native(false),11,1.02f);
            Assert.IsTrue(input.TryReadFresh(0,11,1.02f,out sample));Assert.IsFalse(sample.PinchAvailable);
            var filter=new PortableJointAim();
            Assert.IsTrue(filter.TryRead(11,head.transform,true,sample.IndexBase,sample.ThumbBase,.08f,.02f,out _));
            input.Record(0,default,12,1.04f);
            Assert.IsFalse(input.TryReadFresh(0,12,1.04f,out _),"Invalid native update must clear, not reuse, the old ray");
        }
        [Test] public void NativeFreshnessHandlesUpdateOrderButRejectsStoppedSubsystemAndPause()
        {
            var input=new PortableXRHandInput();input.Record(0,Native(),20,1);
            Assert.IsTrue(input.TryReadFresh(0,21,1.02f,out _));
            Assert.IsFalse(input.TryReadFresh(0,22,1.04f,out _));
            Assert.IsFalse(input.TryReadFresh(0,21,1.2f,out _));
            input.Invalidate();Assert.IsFalse(input.TryReadFresh(0,20,1,out _));
            input.Record(0,Native(),21,1.02f);Assert.IsTrue(input.TryReadFresh(0,21,1.02f,out _));
            input.Record(0,Native(),22,float.NaN);Assert.IsFalse(input.TryReadFresh(0,22,1.04f,out _));
        }
        [Test] public void NativeLostHandDoesNotInvalidateOppositeHand()
        {
            var input=new PortableXRHandInput();input.Record(0,Native(),1,1);input.Record(1,Native(),1,1);
            input.Invalidate(0);Assert.IsFalse(input.TryReadFresh(0,1,1,out _));Assert.IsTrue(input.TryReadFresh(1,1,1,out _));
        }
        [Test] public void NativeTrackingSpaceConversionKeepsOffsetsAndPinchDistance()
        {
            head.transform.SetPositionAndRotation(new Vector3(2,1,-3),Quaternion.Euler(0,90,0));
            var local=Native();var world=local.ToWorld(head.transform);
            Assert.That(Vector3.Distance(world.IndexBase,head.transform.TransformPoint(local.IndexBase)),Is.LessThan(.0001f));
            Assert.That(world.Pinch,Is.EqualTo(.04f).Within(.0001f));
            Assert.That(Quaternion.Angle(world.Wrist.rotation,head.transform.rotation),Is.LessThan(.001f));
        }
        [Test] public void NativeMalformedOrMissingBaseJointsNeverBecomeAim()
        {
            var wrist=new Pose(Vector3.zero,Quaternion.identity);
            Assert.IsFalse(PortableXRHandInput.FromJoints(true,wrist,false,Vector3.zero,true,Vector3.one,true,Vector3.zero,true,Vector3.one).AimValid);
            Assert.IsFalse(PortableXRHandInput.FromJoints(true,wrist,true,new Vector3(float.NaN,0,0),true,Vector3.one,true,Vector3.zero,true,Vector3.one).AimValid);
            var sample=PortableXRHandInput.FromJoints(true,wrist,true,Vector3.zero,true,Vector3.one,true,new Vector3(float.NaN,0,0),true,Vector3.one);
            Assert.IsTrue(sample.AimValid);Assert.IsFalse(sample.PinchAvailable);
        }
    }
}

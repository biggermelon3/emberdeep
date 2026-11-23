using NUnit.Framework;
using PhysicalAssembly;
using UnityEngine;

namespace FireRescue.Tests
{
    public sealed class EmberdeepHandRayTests
    {
        const float Dt=.02f,Open=.05f,Closed=.01f;
        [Test] public void SystemPointerPoseRemainsAuthoritativeThroughPinchAndWristAimChanges()
        {
            var input=new EmberdeepHandRay();var pointer=new Pose(new Vector3(.12f,.3f,.08f),Quaternion.identity);
            var open=input.FromPointerPose(true,pointer,Open);
            Assert.IsTrue(open.Valid);Assert.Less(Vector3.Angle(open.Ray.direction,Vector3.forward),.01f);
            var curled=input.FromPointerPose(true,pointer,Closed);
            Assert.IsTrue(curled.Valid);Assert.Less(Vector3.Angle(curled.Ray.direction,Vector3.forward),.01f);
            Assert.AreEqual(pointer.position,curled.Ray.origin,"Pinch distance alone must not change the system pointer");
            pointer.position+=Vector3.right*.02f;pointer.rotation=Quaternion.Euler(0,35,0);
            var aimed=input.FromPointerPose(true,pointer,Closed);
            Assert.Less(Vector3.Angle(aimed.Ray.direction,pointer.rotation*Vector3.forward),.01f);
            Assert.AreEqual(pointer.position,aimed.Ray.origin,"Intentional system pointer movement remains usable while pinching");
        }
        [Test] public void TrackedClosedHandCanAimButTrackingLossNeverReusesARay()
        {
            var input=new EmberdeepHandRay();var pointer=new Pose(Vector3.zero,Quaternion.identity);
            Assert.IsTrue(input.FromPointerPose(true,pointer,Closed).Valid);
            Assert.IsTrue(input.FromPointerPose(true,pointer,Open).Valid);
            Assert.IsFalse(input.FromPointerPose(false,pointer,Open).Valid);
            pointer.position=Vector3.right;
            Assert.AreEqual(pointer.position,input.FromPointerPose(true,pointer,Closed).Ray.origin);
            Assert.IsTrue(input.FromPointerPose(true,pointer,Open).Valid);
            input.Reset();Assert.IsTrue(input.FromPointerPose(true,pointer,Closed).Valid,"Click rearm belongs to the menu, not ray visibility");
        }
        [Test] public void InvalidPointerPoseCancelsRatherThanReusingPreviousAim()
        {
            var input=new EmberdeepHandRay();var pointer=new Pose(Vector3.zero,Quaternion.identity);
            input.FromPointerPose(true,pointer,Open);
            Assert.IsFalse(input.FromPointerPose(true,new Pose(new Vector3(float.NaN,0,0),Quaternion.identity),Open).Valid);
            Assert.IsTrue(input.FromPointerPose(true,pointer,Closed).Valid);
            Assert.IsFalse(input.FromPointerPose(true,new Pose(Vector3.zero,new Quaternion(0,0,0,0)),Open).Valid);
            Assert.IsFalse(input.FromPointerPose(true,pointer,float.NaN).Valid);
            Assert.IsTrue(input.FromPointerPose(true,pointer,Open).Valid);
        }

        GameObject root;
        EmberdeepHandRayMenu menu;
        WorkshopButton[] buttons;
        [SetUp] public void Setup()
        {
            root=new GameObject("Hand ray rules");menu=root.AddComponent<EmberdeepHandRayMenu>();
            buttons=new[]{Button("A",-.15f),Button("B",.15f)};Physics.SyncTransforms();
        }
        WorkshopButton Button(string name,float x)
        {
            var go=new GameObject(name);go.transform.SetParent(root.transform,false);go.transform.position=new Vector3(x,0,1);
            var collider=go.AddComponent<BoxCollider>();collider.size=new Vector3(.2f,.2f,.03f);
            return go.AddComponent<WorkshopButton>();
        }
        [TearDown] public void Cleanup(){if(root)Object.DestroyImmediate(root);}
        EmberdeepHandRay.Sample Point(int target,float pinch,bool valid=true)=>new(valid,new Ray(Vector3.zero,buttons[target].transform.position),pinch);
        void Ready(int leftTarget=0,int rightTarget=1)
        {for(int i=0;i<8;i++)Assert.IsFalse(menu.ProcessSamples(Point(leftTarget,Open),Point(rightTarget,Open),buttons,true,Dt));}
        void ReadyLeft(int target=0)
        {for(int i=0;i<8;i++)Assert.IsFalse(menu.ProcessSamples(Point(target,Open),default,buttons,true,Dt));}
        Camera View()
        {
            var go=new GameObject("View");go.transform.SetParent(root.transform,false);
            var camera=go.AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=60;camera.aspect=1;
            menu.ViewCamera=camera;return camera;
        }

        [Test] public void RayIsVisibleOnlyOverAnEligibleTargetAndHidesImmediatelyOnExit()
        {
            var miss=new EmberdeepHandRay.Sample(true,new Ray(Vector3.zero,Vector3.up),Open);
            menu.ProcessSamples(miss,miss,buttons,true,Dt);
            Assert.IsTrue(menu.HasPointer);Assert.IsFalse(menu.OwnsInput);
            Assert.IsFalse(menu.RayVisible(0));Assert.IsFalse(menu.RayVisible(1));
            menu.ProcessSamples(Point(0,Open),miss,buttons,true,Dt);
            Assert.IsTrue(menu.RayVisible(0));Assert.IsFalse(menu.RayVisible(1));Assert.IsTrue(menu.IsHovered(buttons[0]));
            menu.ProcessSamples(miss,Point(1,Open),buttons,true,Dt);
            Assert.IsFalse(menu.RayVisible(0));Assert.IsTrue(menu.RayVisible(1));Assert.IsFalse(menu.IsHovered(buttons[0]));
            menu.ProcessSamples(miss,Point(1,Open),buttons,true,Dt,_=>false);
            Assert.IsFalse(menu.RayVisible(1));Assert.IsFalse(menu.OwnsInput);
            menu.ProcessSamples(Point(0,Open),Point(1,Open),buttons,true,Dt);
            menu.ResetInput();Assert.IsFalse(menu.RayVisible(0));Assert.IsFalse(menu.RayVisible(1));
        }

        // A ray towards (x,0,1) meets the buttons' near face (z=.985) at x*.985.
        static EmberdeepHandRay.Sample Across(float faceX,float pinch)=>new(true,new Ray(Vector3.zero,new Vector3(faceX/.985f,0,1)),pinch);
        [Test] public void ClosingPinchRetainsSettledButtonAcrossSmallDriftWithoutSelectingItsNeighbor()
        {
            // Button A ends at x=-.05; B starts at -.045. A closing ray 9 mm past
            // A's edge enters B's physical collider but stays within the pinch retention.
            buttons[1].transform.position=new Vector3(.055f,0,1);Physics.SyncTransforms();ReadyLeft();
            Assert.Less(.009f,EmberdeepHandRayMenu.PinchRetention);
            var drift=Across(-.041f,Closed);
            Assert.IsTrue(menu.ProcessSamples(drift,default,buttons,true,Dt));
            Assert.AreSame(buttons[0],menu.PressedButton,"The settled target owns the click despite a small closing motion");
            Assert.IsFalse(menu.IsHovered(buttons[1]));
            for(int i=0;i<10;i++)Assert.IsFalse(menu.ProcessSamples(Point(1,Closed),default,buttons,true,Dt));
            ReadyLeft(1);Assert.IsTrue(menu.ProcessSamples(Point(1,Closed),default,buttons,true,Dt));
            Assert.AreSame(buttons[1],menu.PressedButton,"The neighbor requires its own fresh open and pinch");
        }

        [Test] public void LargePinchMovementCannotTransferAPreparedClickToAnotherButton()
        {
            ReadyLeft();
            Assert.IsFalse(menu.ProcessSamples(Point(1,Closed),default,buttons,true,Dt));
            Assert.IsNull(menu.PressedButton);
            for(int i=0;i<10;i++)Assert.IsFalse(menu.ProcessSamples(Point(1,Closed),default,buttons,true,Dt));
            ReadyLeft(1);Assert.IsTrue(menu.ProcessSamples(Point(1,Closed),default,buttons,true,Dt));
            Assert.AreSame(buttons[1],menu.PressedButton);
        }

        [TestCase(2f,0f,1f)]
        [TestCase(0f,0f,-1f)]
        public void OffscreenOrBehindCameraButtonsCannotOwnInputOrShowRays(float x,float y,float z)
        {
            View();buttons[0].transform.position=new Vector3(x,y,z);Physics.SyncTransforms();
            for(int i=0;i<8;i++)Assert.IsFalse(menu.ProcessSamples(Point(0,Open),default,buttons,true,Dt));
            Assert.IsFalse(menu.ProcessSamples(Point(0,Closed),default,buttons,true,Dt));
            Assert.IsNull(menu.HoveredButton);Assert.IsNull(menu.PressedButton);
            Assert.IsFalse(menu.OwnsInput);Assert.IsFalse(menu.RayVisible(0));
        }

        [Test] public void TargetLeavingTheViewCancelsPreparedPinchAndCannotResumeHeld()
        {
            var camera=View();ReadyLeft();Assert.IsTrue(menu.RayVisible(0));
            camera.transform.rotation=Quaternion.Euler(0,110,0);
            Assert.IsFalse(menu.ProcessSamples(Point(0,Closed),default,buttons,true,Dt));
            Assert.IsFalse(menu.OwnsInput);Assert.IsFalse(menu.RayVisible(0));
            camera.transform.rotation=Quaternion.identity;
            for(int i=0;i<10;i++)Assert.IsFalse(menu.ProcessSamples(Point(0,Closed),default,buttons,true,Dt));
            ReadyLeft();Assert.IsTrue(menu.ProcessSamples(Point(0,Closed),default,buttons,true,Dt));
        }

        [Test] public void OnlyTheHandPointingAtATargetMayClickIt()
        {
            var miss=new EmberdeepHandRay.Sample(true,new Ray(Vector3.zero,Vector3.up),Open);
            for(int i=0;i<8;i++)menu.ProcessSamples(Point(0,Open),miss,buttons,true,Dt);
            miss.Pinch=Closed;
            Assert.IsFalse(menu.ProcessSamples(Point(0,Open),miss,buttons,true,Dt));
            Assert.IsNull(menu.PressedButton,"Right pinch cannot activate the left target");
            Assert.IsTrue(menu.ProcessSamples(Point(0,Closed),miss,buttons,true,Dt));
            Assert.AreSame(buttons[0],menu.PressedButton);Assert.Zero(menu.PointerHand);
        }
        [Test] public void BothHandsCanPointAtDifferentButtonsAndClickTheirOwnTarget()
        {
            Ready();Assert.IsTrue(menu.ProcessSamples(Point(0,Open),Point(1,Closed),buttons,true,Dt));
            Assert.AreSame(buttons[1],menu.PressedButton);Assert.AreEqual(1,menu.PointerHand);
            Ready();Assert.IsTrue(menu.ProcessSamples(Point(0,Closed),Point(1,Open),buttons,true,Dt));
            Assert.AreSame(buttons[0],menu.PressedButton);Assert.Zero(menu.PointerHand);
        }
        [Test] public void SimultaneousPinchesProduceOneActionAndCannotClickThroughNextPage()
        {
            Ready();Assert.IsTrue(menu.ProcessSamples(Point(0,Closed),Point(1,Closed),buttons,true,Dt));
            Assert.NotNull(menu.PressedButton);
            for(int i=0;i<12;i++)Assert.IsFalse(menu.ProcessSamples(Point(1,Closed),Point(0,Closed),buttons,true,Dt));
            menu.ResetInput();
            for(int i=0;i<12;i++)Assert.IsFalse(menu.ProcessSamples(Point(0,Closed),Point(1,Closed),buttons,true,Dt));
            Ready();Assert.IsTrue(menu.ProcessSamples(Point(0,Closed),Point(1,Open),buttons,true,Dt));
        }
        [Test] public void OwnershipLossAndNonfiniteSamplesRequireFreshOpening()
        {
            Ready();menu.ProcessSamples(Point(0,Open),Point(1,Open),buttons,false,Dt);
            Assert.IsFalse(menu.OwnsInput);Assert.IsFalse(menu.HasPointer);
            for(int i=0;i<10;i++)Assert.IsFalse(menu.ProcessSamples(Point(0,Closed),Point(1,Closed),buttons,true,Dt));
            Ready();var invalid=Point(0,float.NaN);
            menu.ProcessSamples(invalid,default,buttons,true,Dt);
            Assert.IsFalse(menu.ProcessSamples(Point(0,Closed),default,buttons,true,Dt));
        }
        [Test] public void ExistingSupplyOwnerCanRestrictWhichButtonsReceiveAnyHandInput()
        {
            for(int i=0;i<8;i++)menu.ProcessSamples(Point(0,Open),Point(1,Open),buttons,true,Dt,b=>b==buttons[1]);
            Assert.IsFalse(menu.IsHovered(buttons[0]));Assert.IsTrue(menu.IsHovered(buttons[1]));
            Assert.IsFalse(menu.ProcessSamples(Point(0,Closed),Point(1,Open),buttons,true,Dt,b=>b==buttons[1]));
            Assert.IsTrue(menu.ProcessSamples(Point(0,Closed),Point(1,Closed),buttons,true,Dt,b=>b==buttons[1]));
            Assert.AreSame(buttons[1],menu.PressedButton);
        }
        [Test] public void MissingPinchTipsRetainLivePointingButRequireFreshOpenBeforeClick()
        {
            ReadyLeft();var tracker=new EmberdeepHandRay();var pose=new Pose(Vector3.zero,Quaternion.LookRotation(buttons[0].transform.position));
            var obscured=tracker.FromPointerPose(true,pose,float.PositiveInfinity,false);
            Assert.IsTrue(EmberdeepHandRay.Valid(obscured));Assert.IsTrue(obscured.PinchUnavailable);
            for(int i=0;i<12;i++)Assert.IsFalse(menu.ProcessSamples(obscured,default,buttons,true,Dt));
            Assert.AreSame(buttons[0],menu.HoveredButton);Assert.IsTrue(menu.RayVisible(0));
            for(int i=0;i<12;i++)Assert.IsFalse(menu.ProcessSamples(Point(0,Closed),default,buttons,true,Dt));
            ReadyLeft();Assert.IsTrue(menu.ProcessSamples(Point(0,Closed),default,buttons,true,Dt));
        }
        [Test] public void ClosedHandCanFindAButtonButCannotClickUntilReopened()
        {
            var tracker=new EmberdeepHandRay();var pose=new Pose(Vector3.zero,Quaternion.LookRotation(buttons[0].transform.position));
            for(int i=0;i<20;i++)Assert.IsFalse(menu.ProcessSamples(tracker.FromPointerPose(true,pose,Closed),default,buttons,true,Dt));
            Assert.AreSame(buttons[0],menu.HoveredButton);
            ReadyLeft();Assert.IsTrue(menu.ProcessSamples(Point(0,Closed),default,buttons,true,Dt));
        }
        [Test] public void OpenHandEdgeJitterDoesNotKeepCancellingRearm()
        {
            // A ends at x=-.05. Alternate a few millimetres outside its edge (inside the
            // hover hysteresis) and just inside it, with real live rays.
            menu.ProcessSamples(Point(0,Open),default,buttons,true,Dt);
            for(int i=0;i<8;i++)
            {
                Assert.IsFalse(menu.ProcessSamples(Across(i%2==0?-.047f:-.053f,Open),default,buttons,true,Dt));
                Assert.AreSame(buttons[0],menu.HoveredButton);
            }
            Assert.IsTrue(menu.ProcessSamples(Point(0,Closed),default,buttons,true,Dt));
        }
        [Test] public void HoverHysteresisIsSmallAndTheCursorStaysOnTheTrueRay()
        {
            menu.ProcessSamples(Point(0,Open),default,buttons,true,Dt);
            // Retained 3 mm past A's edge: the cursor is where the ray is, not clamped onto the edge.
            menu.ProcessSamples(Across(-.047f,Open),default,buttons,true,Dt);
            Assert.AreSame(buttons[0],menu.HoveredButton);
            Assert.That(menu.RayEnd(0).x,Is.EqualTo(-.047f).Within(.0005f),"No snap to the button edge");
            // 10 mm past the edge an open hand lets go: no sticky button.
            menu.ProcessSamples(Across(-.04f,Open),default,buttons,true,Dt);
            Assert.IsNull(menu.HoveredButton);Assert.IsFalse(menu.OwnsInput);
            // A near miss never acquires a new button: only a real hit does.
            menu.ResetInput();
            menu.ProcessSamples(Across(.047f,Open),default,buttons,true,Dt);
            Assert.IsNull(menu.HoveredButton,"B starts at x=.05; 3 mm short is a miss");
            menu.ProcessSamples(Across(.053f,Open),default,buttons,true,Dt);
            Assert.AreSame(buttons[1],menu.HoveredButton);
            Assert.That(menu.RayEnd(0).x,Is.EqualTo(.053f).Within(.0005f));
        }
        [Test] public void OpenHandOverANeighbourMovesThereAtOnce()
        {
            // 5 mm gap: with an open hand a real hit on B beats A's hysteresis.
            buttons[1].transform.position=new Vector3(.055f,0,1);Physics.SyncTransforms();
            menu.ProcessSamples(Point(0,Open),default,buttons,true,Dt);
            menu.ProcessSamples(Across(-.044f,Open),default,buttons,true,Dt);
            Assert.AreSame(buttons[1],menu.HoveredButton);
        }
        [Test] public void IdleCampRayEndsOnThePanelBetweenButtonsInsteadOfBehindIt()
        {
            menu.ShowUnfocusedRays=true;
            // Between A (ends -.05) and B (starts .05): no button, but the cursor stays on their plane.
            menu.ProcessSamples(Across(0,Open),default,buttons,true,Dt);
            Assert.IsNull(menu.HoveredButton);Assert.IsTrue(menu.RayVisible(0));
            Assert.That(menu.RayEnd(0).z,Is.EqualTo(.985f).Within(.01f));
            // Well off the panel it falls back to the fixed idle length.
            var up=new EmberdeepHandRay.Sample(true,new Ray(Vector3.zero,Vector3.up),Open);
            menu.ProcessSamples(up,default,buttons,true,Dt);
            Assert.That(menu.RayEnd(0).y,Is.EqualTo(EmberdeepHandRayMenu.IdleRayLength).Within(.0001f));
        }
        [Test] public void DrawnLineStartsAtTheSystemAimOrigin()
        {
            var pointer=new Pose(new Vector3(.1f,1.2f,.3f),Quaternion.LookRotation(buttons[0].transform.position-new Vector3(.1f,1.2f,.3f)));
            var sample=new EmberdeepHandRay().FromPointerPose(true,pointer,Open);
            Assert.AreEqual(pointer.position,sample.VisualOrigin,"No lead along the ray and no offset to a joint");
            Assert.AreEqual(pointer.position,sample.Ray.origin);
        }
        [Test] public void SystemSelectSpanSitsOnThePinchScale()
        {
            Assert.Less(EmberdeepHandRay.SystemSpan(true,1),ShadowPinchClose,"pressed closes");
            Assert.Less(EmberdeepHandRay.SystemSpan(false,EmberdeepPinchClick.CloseValue),ShadowPinchClose,"a full-strength select closes even before the runtime's flag");
            Assert.GreaterOrEqual(EmberdeepHandRay.SystemSpan(true,0),ShadowPinchRelease,"a pressed flag left on by an opened hand is the runtime's late release, not a pinch");
            Assert.GreaterOrEqual(EmberdeepHandRay.SystemSpan(true,EmberdeepPinchClick.ReleaseValue),ShadowPinchRelease);
            foreach(float strength in new[]{0f,.3f,.79f,.8f,.89f,float.NaN})
                Assert.GreaterOrEqual(EmberdeepHandRay.SystemSpan(false,strength),ShadowPinchRelease,"not pressed and under CloseValue is open: "+strength);
            Assert.Greater(EmberdeepHandRay.SystemSpan(false,0),EmberdeepHandRay.SystemSpan(false,.6f),"it narrows as the select nears");
            Assert.Less(EmberdeepHandRay.SystemSpan(false,.7f),EmberdeepHandRayMenu.PinchClosingSpan,"a near-closed select counts as closing");
            Assert.Greater(EmberdeepHandRay.SystemSpan(false,.2f),EmberdeepHandRayMenu.PinchClosingSpan);
        }
        // ShadowPinch.CloseDistance / ReleaseDistance (ShadowWorkshop.Runtime, not referenced by this test assembly).
        const float ShadowPinchClose=.018f,ShadowPinchRelease=.020f;
        EmberdeepHandRay.Sample Select(int target,float value,bool pressed,float tips=float.PositiveInfinity)
            =>EmberdeepHandRay.Sample.WithSelect(new Ray(Vector3.zero,buttons[target].transform.position),value,pressed,tips);
        [Test] public void SystemSelectClicksOncePerPressAndRearmsOnAnOrdinaryRelease()
        {
            for(int i=0;i<8;i++)Assert.IsFalse(menu.ProcessSamples(Select(0,.1f,false),default,buttons,true,Dt));
            for(int i=0;i<8;i++)Assert.IsFalse(menu.ProcessSamples(Select(0,.85f,false),default,buttons,true,Dt),"a near-closed select the runtime has not pressed never clicks");
            Assert.IsTrue(menu.ProcessSamples(Select(0,1,true),default,buttons,true,Dt));
            for(int i=0;i<10;i++)Assert.IsFalse(menu.ProcessSamples(Select(0,1,true),default,buttons,true,Dt),"held: one click");
            // Meta's IsPinching stays on while the fingers part; the strength falling to ReleaseValue is the release.
            for(int i=0;i<3;i++)Assert.IsFalse(menu.ProcessSamples(Select(0,.5f,true),default,buttons,true,Dt));
            Assert.IsTrue(menu.ProcessSamples(Select(0,.95f,true),default,buttons,true,Dt),"released and pinched again: a new click, without the runtime letting go");
            for(int i=0;i<3;i++)Assert.IsFalse(menu.ProcessSamples(Select(0,.75f,true,.03f),default,buttons,true,Dt),"tips 3 cm apart release a half-strength select");
            Assert.IsTrue(menu.ProcessSamples(Select(0,1,true,.012f),default,buttons,true,Dt));
            for(int i=0;i<3;i++)Assert.IsFalse(menu.ProcessSamples(Select(0,.75f,true,.015f),default,buttons,true,Dt));
            Assert.IsFalse(menu.ProcessSamples(Select(0,1,true,.012f),default,buttons,true,Dt),"no release (strength and tips both still closed): no click");
        }

        // Headset test 2 (2026-11-22): quick repeated pinches on one button (volume +) failed unless the hand opened wide.
        const float Frame=1/72f;
        int Pinches(int count,int closedFrames,int openFrames,System.Func<bool,EmberdeepHandRay.Sample> sample,bool ownerResets=false)
        {
            int presses=0;
            for(int i=0;i<10;i++)if(menu.ProcessSamples(sample(false),default,buttons,true,Frame))presses++;
            for(int n=0;n<count;n++)
            {
                for(int i=0;i<closedFrames;i++)
                    if(menu.ProcessSamples(sample(true),default,buttons,true,Frame)){presses++;if(ownerResets)menu.ResetInput();}
                for(int i=0;i<openFrames;i++)if(menu.ProcessSamples(sample(false),default,buttons,true,Frame))presses++;
            }
            return presses;
        }
        [Test] public void FiveQuickPinchesOnOneButtonGiveFivePresses()
        {
            // ~7 pinches a second, opening only to 3 cm (ShadowPinchGesture wanted 80 ms past 2 cm after every reset).
            Assert.AreEqual(5,Pinches(5,5,5,closed=>Point(0,closed?.012f:.03f)),"tip distance");
            menu.ResetInput();
            // Meta: strength 1 pressed, then a relaxed .45 with the runtime still holding IsPinching.
            Assert.AreEqual(5,Pinches(5,5,5,closed=>Select(0,closed?1:.45f,true)),"system select with a late runtime release");
            menu.ResetInput();
            // PICO: PortableSystemAim's own pressed state (closes at .8, opens at .6), value peaking at .85.
            bool pico=false;
            Assert.AreEqual(5,Pinches(5,5,5,closed=>{float v=closed?.85f:.5f;pico=pico?v>.6f:v>=.8f;return Select(0,v,pico);}),"PICO select");
            menu.ResetInput();
            Assert.AreEqual(5,Pinches(5,5,5,closed=>Point(0,closed?.012f:.03f),true),"the fireground resets the menu after every press");
        }
        [Test] public void PinchMovePinchAcrossTwoButtonsGivesTwoPresses()
        {
            int presses=0;WorkshopButton last=null;
            void Step(EmberdeepHandRay.Sample sample,int frames)
            {for(int i=0;i<frames;i++)if(menu.ProcessSamples(sample,default,buttons,true,Frame)){presses++;last=menu.PressedButton;}}
            Step(Point(0,Open),10);Step(Point(0,.012f),4);
            Assert.AreEqual(1,presses);Assert.AreSame(buttons[0],last);
            Step(Point(0,.03f),3);Step(Point(1,.03f),10);Step(Point(1,.012f),4);
            Assert.AreEqual(2,presses,"release, move, pinch: the second button clicks");Assert.AreSame(buttons[1],last);
            // Held across to the first button, released there, pinched again.
            Step(Point(0,.012f),6);Assert.AreEqual(2,presses,"held across: nothing");
            Step(Point(0,.03f),10);Step(Point(0,.012f),2);
            Assert.AreEqual(3,presses);Assert.AreSame(buttons[0],last);
        }
        [Test] public void APinchHeldWhileTheRayMovesOntoAButtonGivesNone()
        {
            var miss=new EmberdeepHandRay.Sample(true,new Ray(Vector3.zero,Vector3.up),Open);
            for(int i=0;i<10;i++)menu.ProcessSamples(miss,default,buttons,true,Frame);
            miss.Pinch=.012f;
            for(int i=0;i<5;i++)Assert.IsFalse(menu.ProcessSamples(miss,default,buttons,true,Frame));
            for(int i=0;i<40;i++)Assert.IsFalse(menu.ProcessSamples(Point(0,.012f),default,buttons,true,Frame),"held onto the button");
            Assert.AreSame(buttons[0],menu.HoveredButton,"it still hovers");
            // Closing just as the ray arrives (inside the dwell) is not a click either.
            for(int i=0;i<10;i++)menu.ProcessSamples(miss,default,buttons,true,Frame);
            miss.Pinch=.03f;for(int i=0;i<3;i++)menu.ProcessSamples(miss,default,buttons,true,Frame);
            Assert.IsFalse(menu.ProcessSamples(Point(1,.03f),default,buttons,true,Frame));
            Assert.IsFalse(menu.ProcessSamples(Point(1,.012f),default,buttons,true,Frame),"closing as it arrives");
            Assert.AreEqual(EmberdeepPinchClick.Event.Early,menu.Click(0).Last,"consumed inside the dwell");
            for(int i=0;i<10;i++)Assert.IsFalse(menu.ProcessSamples(Point(1,.012f),default,buttons,true,Frame),"and held");
            for(int i=0;i<10;i++)menu.ProcessSamples(Point(1,.03f),default,buttons,true,Frame);
            Assert.IsTrue(menu.ProcessSamples(Point(1,.012f),default,buttons,true,Frame),"an ordinary release and pinch then clicks");
        }
        [Test] public void AOneFrameTrackingGlitchCannotDoubleClick()
        {
            for(int i=0;i<10;i++)menu.ProcessSamples(Point(0,Open),default,buttons,true,Frame);
            Assert.IsTrue(menu.ProcessSamples(Point(0,.012f),default,buttons,true,Frame));
            for(int n=0;n<5;n++)
            {
                Assert.IsFalse(menu.ProcessSamples(Point(0,.04f),default,buttons,true,Frame));
                Assert.IsFalse(menu.ProcessSamples(Point(0,.012f),default,buttons,true,Frame),"one open frame is not a release");
            }
        }
        [Test] public void IdleRayShowsOnlyNearAPanelWithHysteresis()
        {
            Assert.IsFalse(menu.ShowUnfocusedRays,"buttons-only by default (fireground)");menu.ShowUnfocusedRays=true; // camp
            var miss=new EmberdeepHandRay.Sample(true,new Ray(Vector3.zero,Vector3.up),Closed);
            for(int i=0;i<20;i++)Assert.IsFalse(menu.ProcessSamples(miss,default,buttons,true,Dt));
            Assert.IsFalse(menu.RayVisible(0),"pointing away from every panel: no ray (headset test 2)");Assert.IsFalse(menu.RayShown(0));
            Assert.IsTrue(menu.HasPointer,"the hand still counts as pointing");
            // A spans x -.25..-.05 on the face plane. Show within .08 of it, hide past .12.
            menu.ProcessSamples(Across(-.32f,Open),default,buttons,true,Dt);
            Assert.IsTrue(menu.RayVisible(0));Assert.IsNull(menu.HoveredButton);Assert.IsFalse(menu.OwnsInput);
            menu.ProcessSamples(Across(-.36f,Open),default,buttons,true,Dt);Assert.IsTrue(menu.RayVisible(0),"hysteresis holds it");
            menu.ProcessSamples(Across(-.38f,Open),default,buttons,true,Dt);Assert.IsFalse(menu.RayVisible(0));
            Assert.IsTrue(menu.RayShown(0),"the line shrinks out");
            menu.ProcessSamples(Across(-.36f,Open),default,buttons,true,Dt);Assert.IsFalse(menu.RayVisible(0),"coming back needs the show reach");
            for(int i=0;i<10;i++)menu.ProcessSamples(Across(-.38f,Open),default,buttons,true,Dt);
            Assert.IsFalse(menu.RayShown(0),"gone after the fade");
            menu.ProcessSamples(Across(-.32f,Open),default,buttons,true,Dt);Assert.IsTrue(menu.RayVisible(0));
            menu.ProcessSamples(default,default,buttons,true,Dt);
            Assert.IsFalse(menu.RayVisible(0));Assert.IsFalse(menu.RayShown(0),"a lost hand's line goes at once");
            menu.ShowUnfocusedRays=false;
            menu.ProcessSamples(Across(-.32f,Open),default,buttons,true,Dt);Assert.IsFalse(menu.RayVisible(0),"buttons only");
            menu.ProcessSamples(Point(0,Open),default,buttons,true,Dt);Assert.IsTrue(menu.RayVisible(0));
        }
    }
}

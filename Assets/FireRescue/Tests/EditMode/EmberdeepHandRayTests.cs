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
        [Test] public void TrackingRecoveryWithClosedHandHasNoStaleRay()
        {
            var input=new EmberdeepHandRay();var pointer=new Pose(Vector3.zero,Quaternion.identity);
            Assert.IsFalse(input.FromPointerPose(true,pointer,Closed).Valid);
            Assert.IsTrue(input.FromPointerPose(true,pointer,Open).Valid);
            Assert.IsFalse(input.FromPointerPose(false,pointer,Open).Valid);
            Assert.IsFalse(input.FromPointerPose(true,pointer,Closed).Valid);
            Assert.IsTrue(input.FromPointerPose(true,pointer,Open).Valid);
            input.Reset();Assert.IsFalse(input.FromPointerPose(true,pointer,Closed).Valid,"Ownership reset also requires reopening");
        }
        [Test] public void InvalidPointerPoseCancelsRatherThanReusingPreviousAim()
        {
            var input=new EmberdeepHandRay();var pointer=new Pose(Vector3.zero,Quaternion.identity);
            input.FromPointerPose(true,pointer,Open);
            Assert.IsFalse(input.FromPointerPose(true,new Pose(new Vector3(float.NaN,0,0),Quaternion.identity),Open).Valid);
            Assert.IsFalse(input.FromPointerPose(true,pointer,Closed).Valid);
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

        [Test] public void ClosingPinchRetainsSettledButtonAcrossSmallDriftWithoutSelectingItsNeighbor()
        {
            // Button A ends at x=-.05; B starts at -.04. A closing ray at
            // x=-.035 enters B's physical collider but remains close to A's edge.
            buttons[1].transform.position=new Vector3(.06f,0,1);Physics.SyncTransforms();ReadyLeft();
            var drift=new EmberdeepHandRay.Sample(true,new Ray(Vector3.zero,new Vector3(-.035f,0,1)),Closed);
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
    }
}

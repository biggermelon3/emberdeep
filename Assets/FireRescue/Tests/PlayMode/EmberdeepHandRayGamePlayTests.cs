using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PhysicalAssembly;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    // Synthetic index rays use the same scene adapter as tracked joints. These
    // verify ownership and real scene buttons, not physical headset usability.
    public sealed class EmberdeepHandRayGamePlayTests
    {
        FireRescueGame game;
        const float Dt=.02f,Open=.04f,Closed=.01f;

        [UnitySetUp] public IEnumerator Load()
        {
            EmberdeepSession.NewExpedition();
            yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);
            yield return null;yield return null;
            game=Object.FindFirstObjectByType<FireRescueGame>();Assert.NotNull(game);
            game.enabled=false;
            game.SendMessage("OnApplicationPause",false);game.SendMessage("OnApplicationFocus",true);
            Assert.IsTrue(game.TakeEquipment(1));game.Refresh();Physics.SyncTransforms();
            Assert.NotNull(game.MenuInput);game.MenuInput.ViewCamera=game.Player.ViewCamera;
        }
        WorkshopButton Button(string command)=>game.GetComponentsInChildren<WorkshopButton>(true).Single(b=>b.Command==command);
        EmberdeepHandRay.Sample Point(WorkshopButton button,float pinch)
        {
            var origin=game.Player.ViewCamera.transform.position+Vector3.down*.2f+Vector3.right*.1f;
            return new EmberdeepHandRay.Sample(true,new Ray(origin,button.GetComponent<Collider>().bounds.center-origin),pinch);
        }
        bool Sample(int hand,WorkshopButton button,float pinch,bool available=true)
        {
            var sample=Point(button,pinch);
            return game.ProcessHandMenuInput(hand==0?sample:default,hand==1?sample:default,available,Dt);
        }
        void Ready(int hand,WorkshopButton button)
        {
            Physics.SyncTransforms();
            for(int i=0;i<8;i++)Assert.IsFalse(Sample(hand,button,Open));
            Assert.AreSame(button,game.MenuInput.HoveredButton);
        }
        void Click(int hand,string command)
        {var button=Button(command);Ready(hand,button);Assert.IsTrue(Sample(hand,button,Closed),command);}
        // Synthetic provider boundary: invoke the SAME physical-hand policy used
        // after production MetaShadowPlayer.ReadHand. This deliberately does not
        // bypass hardware gates or claim IHand/headset sampling is exercised.
        bool HandPolicy(int hand,Vector3 palm,Vector3 tip,float pinch=Open)
        {
            var policy=typeof(FireRescueGame).GetMethod("AllowHandInterface",BindingFlags.Instance|BindingFlags.NonPublic,
                null,new[]{typeof(int),typeof(EmberdeepRefillInteraction.Hand)},null);
            Assert.NotNull(policy,"Production physical-hand policy must be shared with this regression");
            var physical=new EmberdeepRefillInteraction.Hand(true,new Pose(palm,Quaternion.identity),pinch,tip);
            return (bool)policy.Invoke(game,new object[]{hand,physical});
        }
        Vector3 FreePalm=>game.Player.ViewCamera.transform.position+Vector3.up*.25f;
        bool PolicySample(int hand,WorkshopButton button,Vector3 palm,float pinch)
        {
            var physical=new EmberdeepRefillInteraction.Hand(true,new Pose(palm,Quaternion.identity),pinch,palm);
            var ray=new EmberdeepHandRay.Sample(true,new Ray(palm,button.GetComponent<Collider>().bounds.center-palm),pinch);
            return game.ProcessHandInterfaceSamples(hand==0?physical:default,hand==1?physical:default,
                hand==0?ray:default,hand==1?ray:default,true,Dt);
        }
        void LookAtButton(WorkshopButton button)
        {
            // A synthetic head TURN to the real panel; camera position and panel
            // geometry are unchanged. The real viewport eligibility stays enabled.
            Physics.SyncTransforms();var camera=game.Player.ViewCamera;
            camera.transform.rotation=Quaternion.LookRotation(button.GetComponent<Collider>().bounds.center-camera.transform.position,Vector3.up);
            game.MenuInput.ViewCamera=camera;
            var viewport=camera.WorldToViewportPoint(button.transform.position);
            Assert.Greater(viewport.z,0);Assert.That(viewport.x,Is.InRange(0f,1f));Assert.That(viewport.y,Is.InRange(0f,1f));
        }
        void PolicyReady(int hand,WorkshopButton button,Vector3 palm)
        {
            LookAtButton(button);
            for(int i=0;i<8;i++)Assert.IsFalse(PolicySample(hand,button,palm,Open));
            Assert.IsTrue(game.MenuInput.RayVisible(hand));Assert.AreSame(button,game.MenuInput.HoveredButton);
        }
        static void Clear(FireRun run)
        {
            for(int i=0;i<run.Trees.Length;i++)
                if(run.Trees[i].State==TreeState.Burning)run.ApplyWater(run.Trees[i].Position,100,.1f);
            Assert.AreEqual(FirePhase.Cleared,run.Phase);
        }

        Transform Hud=>game.transform.Find("Rescue station");
        static Pose WorldPose(Transform value)=>new Pose(value.position,value.rotation);
        static void AssertWorldPose(Pose expected,Transform actual)
        {
            Assert.Less(Vector3.Distance(expected.position,actual.position),.0001f);
            Assert.Less(Quaternion.Angle(expected.rotation,actual.rotation),.08f);
        }
        void AssertComfortableHud(Pose head,Vector3 heading)
        {
            Assert.NotNull(Hud);heading=Vector3.ProjectOnPlane(heading,Vector3.up).normalized;
            Assert.Less(Vector3.Distance(head.position+heading*.9f-Vector3.up*.04f,Hud.position),.0001f);
            Assert.Less(Vector3.Angle(Hud.forward,heading),.03f);
            Assert.Less(Vector3.Angle(Hud.up,Vector3.up),.03f);
        }
        void PlaceHudDirect()
        {
            var method=typeof(FireRescueGame).GetMethod("PlaceHud",BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.NotNull(method);method.Invoke(game,null);
        }

        [UnityTest] public IEnumerator HudStartsComfortablyInFrontAndRemainsWorldFixedUntilRecenter()
        {
            var camera=game.Player.ViewCamera.transform;Assert.NotNull(Hud);
            AssertComfortableHud(WorldPose(camera),camera.forward);
            var panel=WorldPose(Hud);var arena=WorldPose(game.Arena);
            camera.SetPositionAndRotation(camera.position+new Vector3(.3f,-.4f,-.2f),Quaternion.Euler(65,125,30));
            var movedHead=WorldPose(camera);game.Refresh();
            // Actual desktop Update runs while the synthetic head looks away.
            // This checks anchoring, not an XR tracking-provider simulation.
            game.enabled=true;
            yield return null;yield return null;
            game.enabled=false;
            AssertWorldPose(panel,Hud);AssertWorldPose(movedHead,camera);AssertWorldPose(arena,game.Arena);
            PlaceHudDirect();AssertComfortableHud(movedHead,camera.forward);
            Assert.Greater(Vector3.Distance(panel.position,Hud.position),.2f);
            // Desktop Recenter deliberately resets its preview camera; XR camera
            // preservation is covered separately by shop placement below.
            game.Recenter();AssertComfortableHud(WorldPose(camera),camera.forward);
            yield return null;
        }

        [UnityTest] public IEnumerator ShopOpenAndCloseUseCurrentHeadWithoutMovingCameraOrArena()
        {
            Clear(game.Run);var camera=game.Player.ViewCamera.transform;
            var arena=WorldPose(game.Arena);var scale=game.Arena.localScale;float floor=game.Player.FloorHeight;
            camera.SetPositionAndRotation(new Vector3(2,floor+1.1f,-2),Quaternion.Euler(60,100,18));
            var seated=WorldPose(camera);game.OpenShop();
            Assert.IsTrue(game.ShopOpen);AssertComfortableHud(seated,Quaternion.Euler(0,100,0)*Vector3.forward);
            AssertWorldPose(seated,camera);AssertWorldPose(arena,game.Arena);Assert.AreEqual(scale,game.Arena.localScale);
            Assert.IsTrue(Button("Leave shop").transform.IsChildOf(Hud));
            camera.SetPositionAndRotation(new Vector3(2.2f,floor+1.7f,-1.6f),Quaternion.Euler(-35,-50,-10));
            var standing=WorldPose(camera);game.CloseShop();
            Assert.IsFalse(game.ShopOpen);AssertComfortableHud(standing,Quaternion.Euler(0,-50,0)*Vector3.forward);
            AssertWorldPose(standing,camera);AssertWorldPose(arena,game.Arena);Assert.AreEqual(scale,game.Arena.localScale);
            yield return null;
        }

        [UnityTest] public IEnumerator HudVerticalFallbackAndUnavailableHeadCannotMovePanelToFloor()
        {
            var camera=game.Player.ViewCamera.transform;float floor=game.Player.FloorHeight;
            camera.SetPositionAndRotation(new Vector3(-1,floor+1.2f,2),Quaternion.Euler(10,73,0));
            PlaceHudDirect();var heading=Hud.forward;
            camera.SetPositionAndRotation(new Vector3(-.8f,floor+1.25f,2.2f),Quaternion.Euler(89.9f,-80,20));
            PlaceHudDirect();AssertComfortableHud(WorldPose(camera),heading);var stable=WorldPose(Hud);
            camera.position=new Vector3(-.8f,floor+.2f,2.2f);PlaceHudDirect();AssertWorldPose(stable,Hud);
            camera.SetPositionAndRotation(new Vector3(.5f,floor+1.65f,1),Quaternion.Euler(25,-20,0));
            game.SendMessage("OnApplicationFocus",false);PlaceHudDirect();AssertWorldPose(stable,Hud);
            game.SendMessage("OnApplicationFocus",true);game.SendMessage("OnApplicationPause",true);
            PlaceHudDirect();AssertWorldPose(stable,Hud);
            game.SendMessage("OnApplicationPause",false);PlaceHudDirect();AssertComfortableHud(WorldPose(camera),camera.forward);
            yield return null;
        }

        [UnityTest] public IEnumerator HudRepositionClearsHoverAndRequiresReleaseBeforeShopClick()
        {
            Clear(game.Run);game.Refresh();var shop=Button("Gear shop");Ready(0,shop);
            Assert.IsTrue(game.MenuInput.RayVisible(0));var camera=game.Player.ViewCamera.transform;
            camera.SetPositionAndRotation(camera.position+Vector3.right*.15f,Quaternion.Euler(10,35,0));
            PlaceHudDirect();AssertComfortableHud(WorldPose(camera),camera.forward);
            Assert.IsNull(game.MenuInput.HoveredButton);Assert.IsFalse(game.MenuInput.RayVisible(0));
            for(int i=0;i<12;i++)Assert.IsFalse(Sample(0,shop,Closed));
            Assert.IsFalse(game.ShopOpen,"Relocation cannot convert an already closed pinch into a UI click");
            Ready(0,shop);Assert.IsTrue(Sample(0,shop,Closed));Assert.IsTrue(game.ShopOpen);
            yield return null;
        }

        [UnityTest] public IEnumerator LeftHandCompletesRealShopWithoutClickingThroughModal()=>Shop(0);
        [UnityTest] public IEnumerator RightHandCompletesRealShopWithoutClickingThroughModal()=>Shop(1);
        IEnumerator Shop(int hand)
        {
            Clear(game.Run);game.Refresh();
            Click(hand,"Gear shop");Assert.IsTrue(game.ShopOpen);Assert.IsFalse(game.HoldingEquipment);
            var next=Button("Next gear");Physics.SyncTransforms();
            for(int i=0;i<12;i++)Assert.IsFalse(Sample(hand,next,Closed));
            Assert.AreEqual(FireEquipmentId.Extinguisher,game.SelectedEquipment,"Opening shop cannot reuse its pinch on the next page");
            Click(hand,"Next gear");Assert.AreEqual(FireEquipmentId.PulsePistol,game.SelectedEquipment);
            int before=game.Run.Coins;float supplied=game.Run.SuppliedWater;
            Click(hand,"Buy gear");Assert.IsTrue(game.Run.Owns(FireEquipmentId.PulsePistol));
            Assert.AreEqual(before-FireEquipment.Get(FireEquipmentId.PulsePistol).Price,game.Run.Coins);
            Assert.Zero(game.Run.WaterFor(FireEquipmentId.PulsePistol));Assert.AreEqual(supplied,game.Run.SuppliedWater);
            for(int i=0;i<10;i++)Assert.IsFalse(Sample(hand,Button("Buy gear"),Closed));
            Assert.AreEqual(before-FireEquipment.Get(FireEquipmentId.PulsePistol).Price,game.Run.Coins);
            Click(hand,"Leave shop");Assert.IsFalse(game.ShopOpen);
            Click(hand,"Return equipment");Assert.IsTrue(game.HoldingEquipment);
            Assert.AreEqual(FireEquipmentId.PulsePistol,game.Run.Equipped);Assert.Zero(game.Run.Water);
            yield return null;
        }

        [UnityTest] public IEnumerator LeftFreeHandCanOpenShopWhileRightToolHandIsNearSource()=>FreeHandShop(0);
        [UnityTest] public IEnumerator RightFreeHandCanOpenShopWhileLeftToolHandIsNearSource()=>FreeHandShop(1);
        IEnumerator FreeHandShop(int freeHand)
        {
            Clear(game.Run);game.ReturnEquipment();Assert.IsTrue(game.TakeEquipment(1-freeHand));game.Refresh();
            var shop=Button("Gear shop");var near=game.HydrantStation.Socket.position;
            game.HeldTool.transform.position=near;var far=FreePalm;
            Assert.IsTrue(EmberdeepRefillInteraction.NearStation(near,game.HydrantStation));
            Assert.IsFalse(EmberdeepRefillInteraction.NearStation(far,game.HydrantStation));
            Assert.IsFalse(HandPolicy(1-freeHand,near,near),"Direct work still reserves the nearby tool hand");
            Assert.IsTrue(HandPolicy(freeHand,far,far),"The other hand cannot inherit the tool hand's proximity lock");
            PolicyReady(freeHand,shop,far);Assert.IsTrue(PolicySample(freeHand,shop,far,Closed));
            Assert.IsTrue(game.ShopOpen);Assert.IsFalse(game.HoldingEquipment);Assert.IsFalse(game.Run.Supply.Locked);
            Assert.IsFalse(game.MenuInput.RayVisible(1-freeHand));yield return null;
        }

        [UnityTest] public IEnumerator ClearedDockedShopCancelsHalfStrokeAndCannotReusePinchOnPurchase()
        {
            game.Run.Tick(.1f,false);for(int i=0;i<100 && game.Run.Water>0;i++)game.Run.Tick(.1f,true);
            game.Run.CancelTrigger();Assert.Zero(game.Run.Water);Clear(game.Run);
            var supply=game.Run.Supply;
            for(int i=0;i<4;i++)supply.SampleDock(WaterSourceKind.Hydrant,1,0,0,true,.1f);
            Assert.IsTrue(supply.Locked);supply.SampleLever(0,true,true);supply.SampleLever(.085f,true,true);
            Assert.IsTrue(supply.LeverReturnRequired);game.Refresh();
            int coins=game.Run.Coins;float water=game.Run.Water,supplied=game.Run.SuppliedWater;
            var far=FreePalm;var shop=Button("Gear shop");Assert.IsTrue(shop.Interactable);
            PolicyReady(0,shop,far);Assert.IsTrue(PolicySample(0,shop,far,Closed));
            Assert.IsTrue(game.ShopOpen);Assert.IsFalse(game.HoldingEquipment);Assert.IsFalse(supply.Locked);
            Assert.IsFalse(supply.LeverReturnRequired);Assert.IsFalse(supply.ValveOpen);
            Assert.IsFalse(game.RefillInteraction.OwnsControl);Assert.IsFalse(game.HydrantStation.OutletActive);
            Assert.AreEqual(coins,game.Run.Coins);Assert.AreEqual(water,game.Run.Water);Assert.AreEqual(supplied,game.Run.SuppliedWater);
            // Select an affordable item without changing the held UI pinch; the
            // purchase button must require release even though its target changed.
            game.SelectEquipment(1);game.Refresh();var buy=Button("Buy gear");Assert.IsTrue(buy.Interactable);LookAtButton(buy);
            for(int i=0;i<12;i++)Assert.IsFalse(PolicySample(0,buy,far,Closed));
            Assert.IsFalse(game.Run.Owns(FireEquipmentId.PulsePistol));Assert.AreEqual(coins,game.Run.Coins);
            PolicyReady(0,buy,far);Assert.IsTrue(PolicySample(0,buy,far,Closed));Assert.IsTrue(game.Run.Owns(FireEquipmentId.PulsePistol));
            Assert.AreEqual(coins-FireEquipment.Get(FireEquipmentId.PulsePistol).Price,game.Run.Coins);
            yield return null;
        }

        [UnityTest] public IEnumerator ActiveLeverOperatorCannotOpenShopEvenAfterPointingAwayFromSource()
        {
            Clear(game.Run);game.ReturnEquipment();game.Refresh();var home=game.HydrantStation.Lever.position;
            var idle=new EmberdeepRefillInteraction.Hand(true,new Pose(FreePalm,Quaternion.identity),Open,FreePalm);
            for(int i=0;i<5;i++)game.ProcessRefillHands(new EmberdeepRefillInteraction.Hand(true,new Pose(home,Quaternion.identity),Open,home),idle,.025f);
            game.ProcessRefillHands(new EmberdeepRefillInteraction.Hand(true,new Pose(home,Quaternion.identity),Closed,home),idle,.025f);
            Assert.IsTrue(game.RefillInteraction.EmptyOutlet);Assert.AreEqual(0,game.RefillInteraction.OperatingHand);
            var far=FreePalm;Assert.IsFalse(HandPolicy(0,far,far),"Ownership survives a UI sample until refill observes release");
            Assert.IsTrue(HandPolicy(1,far,far),"The other hand remains available");
            var shop=Button("Gear shop");LookAtButton(shop);
            for(int i=0;i<8;i++)Assert.IsFalse(PolicySample(0,shop,far,Open));
            Assert.IsFalse(PolicySample(0,shop,far,Closed));Assert.IsFalse(game.MenuInput.RayVisible(0));Assert.IsFalse(game.ShopOpen);
            game.RefillInteraction.Reset();
            for(int i=0;i<8;i++)Assert.IsFalse(PolicySample(0,shop,far,Closed));
            PolicyReady(0,shop,far);Assert.IsTrue(PolicySample(0,shop,far,Closed));Assert.IsTrue(game.ShopOpen);
            yield return null;
        }

        [UnityTest] public IEnumerator FightingShopRemainsUnavailableThroughPhysicalPolicyAndViewportGate()
        {
            Assert.AreEqual(FirePhase.Fighting,game.Run.Phase);game.Refresh();var shop=Button("Gear shop");
            Assert.IsFalse(shop.Interactable);var far=FreePalm;Assert.IsTrue(HandPolicy(0,far,far));LookAtButton(shop);
            for(int i=0;i<8;i++)Assert.IsFalse(PolicySample(0,shop,far,Open));
            Assert.IsFalse(PolicySample(0,shop,far,Closed));Assert.IsFalse(game.MenuInput.RayVisible(0));Assert.IsFalse(game.ShopOpen);
            Assert.IsTrue(game.HoldingEquipment);yield return null;
        }

        [UnityTest] public IEnumerator PointingAtHudCancelsChargedCannonBeforeAnyClickOrRelease()
        {
            for(int i=0;i<3;i++){Clear(game.Run);if(i<2)game.StartNext();}
            Assert.IsTrue(game.Run.BuyEquipment(FireEquipmentId.PressureCannon));
            game.SelectEquipment(3);game.StartNext();Assert.IsTrue(game.TakeEquipment(1));
            var supply=game.Run.Supply;
            for(int i=0;i<4;i++)supply.SampleDock(WaterSourceKind.Hydrant,1,0,0,true,.1f);
            typeof(FireRun).GetProperty(nameof(FireRun.StationTier)).SetValue(game.Run,WaterStationTier.RotaryHydrant);
            Assert.IsTrue(supply.SetValveOpen(true));
            for(int i=0;i<120;i++)supply.Tick(.1f,true);
            supply.Disconnect();game.Run.Tick(Dt,false);game.Refresh();
            Assert.IsTrue(game.Run.PrimeCannon(1));Assert.Greater(game.Run.Charge01,0);
            float water=game.Run.Water,consumed=game.Run.ConsumedWater;
            var button=Button("Return equipment");Physics.SyncTransforms();
            Assert.IsFalse(Sample(1,button,Open));
            Assert.Zero(game.Run.Charge01,"HUD ownership cancels charge before waiting for a pinch");
            Assert.IsFalse(game.Run.Tick(Dt,false),"Release after UI cancellation cannot launch a bomb");
            Assert.AreEqual(water,game.Run.Water);Assert.AreEqual(consumed,game.Run.ConsumedWater);
            Ready(1,button);Assert.IsTrue(Sample(1,button,Closed));Assert.IsFalse(game.HoldingEquipment);
            for(int i=0;i<15;i++)Assert.IsFalse(Sample(1,button,Closed));
            Assert.IsFalse(game.HoldingEquipment,"Held UI pinch cannot take the tool again");
            yield return null;
        }

        void PrepareChargedCannon(int hand)
        {
            for(int i=0;i<3;i++){Clear(game.Run);if(i<2)game.StartNext();}
            Assert.IsTrue(game.Run.BuyEquipment(FireEquipmentId.PressureCannon));
            game.SelectEquipment(3);game.StartNext();Assert.IsTrue(game.TakeEquipment(hand));
            var supply=game.Run.Supply;
            typeof(FireRun).GetProperty(nameof(FireRun.StationTier)).SetValue(game.Run,WaterStationTier.RotaryHydrant);
            for(int i=0;i<4;i++)supply.SampleDock(WaterSourceKind.Hydrant,hand,0,0,true,.1f);
            Assert.IsTrue(supply.SetValveOpen(true));for(int i=0;i<100;i++)supply.Tick(.1f,true);
            supply.Disconnect();game.Run.Tick(Dt,false);game.Refresh();
            Assert.IsTrue(game.Run.PrimeCannon(1));Assert.Greater(game.Run.Charge01,0);
        }

        [UnityTest] public IEnumerator VisibleHudOutsideHeadIntentConeCannotHoverOrCancelChargeFromEitherHand()
        {
            PrepareChargedCannon(1);var button=Button("Return equipment");LookAtButton(button);
            Assert.That(game.MenuInput.MaximumHeadAngle,Is.EqualTo(25f));
            var camera=game.Player.ViewCamera;camera.transform.rotation*=Quaternion.Euler(30,0,0);
            var viewport=camera.WorldToViewportPoint(button.transform.position);
            Assert.Greater(viewport.z,0);Assert.That(viewport.x,Is.InRange(0f,1f));Assert.That(viewport.y,Is.InRange(0f,1f));
            Assert.Greater(Vector3.Angle(camera.transform.forward,button.transform.position-camera.transform.position),25f);
            float charge=game.Run.Charge01,water=game.Run.Water;
            for(int hand=0;hand<2;hand++)
            {
                // Open samples ensure this is the head-intent rule, not merely
                // the closed-tool-hand acquisition rule or offscreen rejection.
                for(int i=0;i<10;i++)Assert.IsFalse(Sample(hand,button,Open));
                Assert.IsFalse(Sample(hand,button,Closed));Assert.IsFalse(game.MenuInput.HasTarget(hand));
                Assert.IsFalse(game.MenuInput.RayVisible(hand));Assert.IsNull(game.MenuInput.HoveredButton);
                Assert.AreEqual(charge,game.Run.Charge01);Assert.AreEqual(water,game.Run.Water);Assert.IsTrue(game.HoldingEquipment);
            }
            yield return null;
        }

        [UnityTest] public IEnumerator LeftChargingHandReservesUiImmediatelyButRequiresOpenBeforeClick()=>ClosedCannonUi(0);
        [UnityTest] public IEnumerator RightChargingHandReservesUiImmediatelyButRequiresOpenBeforeClick()=>ClosedCannonUi(1);
        IEnumerator ClosedCannonUi(int hand)
        {
            PrepareChargedCannon(hand);var button=Button("Return equipment");LookAtButton(button);
            float charge=game.Run.Charge01,water=game.Run.Water;var far=FreePalm;
            Assert.Greater(charge,0);
            for(int i=0;i<12;i++)Assert.IsFalse(PolicySample(hand,button,far,Closed));
            Assert.IsTrue(game.MenuInput.HasTarget(hand));Assert.IsTrue(game.MenuInput.RayVisible(hand));
            Assert.Zero(game.Run.Charge01,"Clear pointing intent reserves the hand immediately, before clicking is armed");
            Assert.AreEqual(water,game.Run.Water);Assert.IsTrue(game.HoldingEquipment);
            Assert.IsTrue(HandPolicy(hand,far,far,Open));Ready(hand,button);
            Assert.Zero(game.Run.Charge01,"An intentional open-hand UI target still cancels the weapon safely");
            Assert.IsTrue(game.MenuInput.HasTarget(hand));Assert.IsTrue(HandPolicy(hand,far,far,Closed));
            Assert.IsTrue(Sample(hand,button,Closed));Assert.IsFalse(game.HoldingEquipment);
            Assert.AreEqual(water,game.Run.Water,"UI confirmation must not launch the cancelled cannon");
            yield return null;
        }

        [UnityTest] public IEnumerator LookingAwayDropsToolHandUiOwnershipAndClosedReturnCannotClick()
        {
            var button=Button("Return equipment");LookAtButton(button);Ready(1,button);
            var camera=game.Player.ViewCamera.transform;var lookingAtPanel=camera.rotation;
            camera.rotation=lookingAtPanel*Quaternion.Euler(30,0,0);
            Assert.IsFalse(Sample(1,button,Closed));Assert.IsFalse(game.MenuInput.HasTarget(1));
            Assert.IsFalse(game.MenuInput.RayVisible(1));Assert.IsTrue(game.HoldingEquipment);
            camera.rotation=lookingAtPanel;
            for(int i=0;i<12;i++)Assert.IsFalse(Sample(1,button,Closed));
            Assert.AreSame(button,game.MenuInput.HoveredButton,"Closed return reserves UI, but cannot click without reopening");Assert.IsTrue(game.HoldingEquipment);
            Ready(1,button);Assert.IsTrue(Sample(1,button,Closed));Assert.IsFalse(game.HoldingEquipment);
            yield return null;
        }

        static EmberdeepRefillInteraction.Hand PhysicalAt(Vector3 point,float pinch=Open,bool tracked=true)
            =>new EmberdeepRefillInteraction.Hand(tracked,new Pose(point,Quaternion.identity),pinch,point);
        bool PointWithPhysical(int hand,EmberdeepRefillInteraction.Hand physical,EmberdeepRefillInteraction.Hand other,
            WorkshopButton button,bool pointing=true)
        {
            var direction=pointing?button.GetComponent<Collider>().bounds.center-physical.Palm.position:-game.Player.ViewCamera.transform.forward;
            var ray=new EmberdeepHandRay.Sample(physical.Tracked,new Ray(physical.Palm.position,direction),physical.Pinch);
            return game.ProcessHandInterfaceSamples(hand==0?physical:other,hand==1?physical:other,
                hand==0?ray:default,hand==1?ray:default,true,Dt);
        }
        void RefillWithPhysical(int hand,EmberdeepRefillInteraction.Hand physical,EmberdeepRefillInteraction.Hand other)
            =>game.ProcessRefillHands(hand==0?physical:other,hand==1?physical:other,Dt);

        [UnityTest] public IEnumerator LeftHandPointingAtMenuNearWellCannotLeakPinchIntoPump()=>NearWellUiIntent(0);
        [UnityTest] public IEnumerator RightHandPointingAtMenuNearWellCannotLeakPinchIntoPump()=>NearWellUiIntent(1);
        IEnumerator NearWellUiIntent(int hand)
        {
            game.ReturnEquipment();var button=Button("Return equipment");LookAtButton(button);
            var at=game.HydrantStation.Lever.position;var other=PhysicalAt(FreePalm);
            Assert.IsFalse(HandPolicy(hand,at,at),"Without a pointed target this location belongs to direct manipulation");
            // First closed sample has no prior dwell/open. It reserves the menu
            // immediately, even though it is not allowed to click yet.
            for(int i=0;i<8;i++)
            {
                var physical=PhysicalAt(at,Closed);
                Assert.IsFalse(PointWithPhysical(hand,physical,other,button));RefillWithPhysical(hand,physical,other);
                Assert.IsTrue(game.MenuInput.HasTarget(hand));Assert.IsFalse(game.RefillInteraction.OwnsControl);
                Assert.IsFalse(game.RefillInteraction.EmptyOutlet);Assert.IsFalse(game.HoldingEquipment);
            }
            // Losing the target cannot reinterpret that same closed pinch as a
            // lever grab. A real release restores free physical interaction.
            for(int i=0;i<8;i++)
            {
                var physical=PhysicalAt(at,Closed);
                Assert.IsFalse(PointWithPhysical(hand,physical,other,button,false));RefillWithPhysical(hand,physical,other);
                Assert.IsFalse(game.RefillInteraction.OwnsControl);
            }
            for(int i=0;i<6;i++)
            {
                var physical=PhysicalAt(at);Assert.IsFalse(PointWithPhysical(hand,physical,other,button,false));RefillWithPhysical(hand,physical,other);
            }
            var closed=PhysicalAt(at,Closed);Assert.IsFalse(PointWithPhysical(hand,closed,other,button,false));RefillWithPhysical(hand,closed,other);
            Assert.IsTrue(game.RefillInteraction.EmptyOutlet);Assert.AreEqual(hand,game.RefillInteraction.OperatingHand);
            Assert.Zero(game.Run.SuppliedWater,"Trying the empty outlet never refills a tank");yield return null;
        }

        [UnityTest] public IEnumerator NearRackAndSourceUiIntentCanSettleThenClickWithoutPhysicalAcquisition()
        {
            game.ReturnEquipment();var button=Button("Return equipment");LookAtButton(button);
            var rack=game.transform.Find("Equipment rack").GetComponentInChildren<FireEquipmentView>();Assert.NotNull(rack);
            var at=rack.Grip.position;var other=PhysicalAt(FreePalm);
            Assert.IsFalse(HandPolicy(0,at,at),"The ordinary pickup exclusion is present");
            for(int i=0;i<8;i++)
            {
                var physical=PhysicalAt(at);Assert.IsFalse(PointWithPhysical(0,physical,other,button));RefillWithPhysical(0,physical,other);
                Assert.IsTrue(game.MenuInput.HasTarget(0));Assert.IsFalse(game.HoldingEquipment);
            }
            Assert.IsTrue(PointWithPhysical(0,PhysicalAt(at,Closed),other,button));Assert.IsTrue(game.HoldingEquipment);
            Assert.Zero(game.Run.Charge01);Assert.IsFalse(game.RefillInteraction.OwnsControl);yield return null;
        }

        [UnityTest] public IEnumerator DockedUiIntentCancelsPendingHalfStrokeBeforeDwellAndCannotPump()
        {
            game.Run.Tick(.1f,false);for(int i=0;i<100 && game.Run.Water>0;i++)game.Run.Tick(.1f,true);
            game.Run.CancelTrigger();var supply=game.Run.Supply;
            for(int i=0;i<4;i++)supply.SampleDock(WaterSourceKind.Hydrant,1,0,0,true,.1f);
            supply.SampleLever(0,true,true);supply.SampleLever(.085f,true,true);Assert.IsTrue(supply.LeverReturnRequired);
            game.Refresh();var button=Button("Next incident");LookAtButton(button);
            var at=game.HydrantStation.Lever.position;var owner=PhysicalAt(game.HeldTool.Grip.position);
            float water=game.Run.Water;
            for(int i=0;i<10;i++)
            {
                var physical=PhysicalAt(at+(i%2==0?Vector3.zero:Vector3.down*.085f),Closed);
                Assert.IsFalse(PointWithPhysical(0,physical,owner,button));RefillWithPhysical(0,physical,owner);
                Assert.IsTrue(supply.Locked);Assert.IsFalse(supply.LeverReturnRequired);Assert.IsFalse(game.RefillInteraction.OwnsControl);
                Assert.AreEqual(water,game.Run.Water);Assert.IsFalse(supply.Assisted);
            }
            yield return null;
        }

        [UnityTest] public IEnumerator UiDockOwnerKeepsTrackingWhileOtherHandOwnsLeverAndCompletesStroke()
        {
            game.Run.Tick(.1f,false);for(int i=0;i<100 && game.Run.Water>0;i++)game.Run.Tick(.1f,true);
            game.Run.CancelTrigger();var supply=game.Run.Supply;
            for(int i=0;i<4;i++)supply.SampleDock(WaterSourceKind.Hydrant,1,0,0,true,.1f);
            var home=game.HydrantStation.Lever.position;var owner=PhysicalAt(game.HeldTool.Grip.position);
            for(int i=0;i<6;i++)game.ProcessRefillHands(PhysicalAt(home),owner,Dt);
            game.ProcessRefillHands(PhysicalAt(home,Closed),owner,Dt);Assert.AreEqual(0,game.RefillInteraction.OperatingHand);
            game.Refresh();var button=Button("Next incident");LookAtButton(button);float water=game.Run.Water;
            var down=PhysicalAt(home+Vector3.down*.085f,Closed);var uiOwner=PhysicalAt(owner.Palm.position,Closed);
            Assert.IsFalse(PointWithPhysical(1,uiOwner,down,button));RefillWithPhysical(1,uiOwner,down);
            Assert.IsTrue(game.MenuInput.HasTarget(1));Assert.IsTrue(supply.Locked);Assert.IsTrue(supply.LeverReturnRequired);
            Assert.AreEqual(0,game.RefillInteraction.OperatingHand);
            // An actual operator cannot steal its own gesture back through a ray.
            Assert.IsFalse(PointWithPhysical(0,down,uiOwner,button));Assert.IsFalse(game.MenuInput.HasTarget(0));
            var returned=PhysicalAt(home,Closed);Assert.IsFalse(PointWithPhysical(1,uiOwner,returned,button));RefillWithPhysical(1,uiOwner,returned);
            Assert.IsTrue(supply.Locked);Assert.That(game.Run.Water,Is.EqualTo(water+12).Within(.001f));
            yield return null;
        }

        [UnityTest] public IEnumerator ReservedUiCannotHideDockOwnerTrackingLossOrContinueValveTransfer()
        {
            game.Run.Tick(.1f,false);for(int i=0;i<100 && game.Run.Water>0;i++)game.Run.Tick(.1f,true);
            game.Run.CancelTrigger();typeof(FireRun).GetProperty(nameof(FireRun.StationTier)).SetValue(game.Run,WaterStationTier.RotaryHydrant);
            var supply=game.Run.Supply;for(int i=0;i<4;i++)supply.SampleDock(WaterSourceKind.Hydrant,1,0,0,true,.1f);
            Assert.IsTrue(supply.SetValveOpen(true));game.Refresh();var button=Button("Next incident");LookAtButton(button);
            var ui=PhysicalAt(game.HydrantStation.Valve.position,Closed);var owner=PhysicalAt(game.HeldTool.Grip.position);
            Assert.IsFalse(PointWithPhysical(0,ui,owner,button));Assert.IsTrue(game.MenuInput.HasTarget(0));
            float water=game.Run.Water;
            // Fresh refill data loses the owner after the UI frame: this must not
            // consult cached UI tracking or tick the supply with literal true.
            owner.Tracked=false;game.ProcessRefillHands(ui,owner,Dt);
            Assert.IsFalse(supply.Locked);Assert.IsFalse(supply.ValveOpen);Assert.IsFalse(game.HoldingEquipment);
            Assert.AreEqual(water,game.Run.Water);Assert.IsFalse(game.RefillInteraction.OwnsControl);
            yield return null;
        }

        [UnityTest] public IEnumerator SupplyOwnershipAllowsOnlyAssistanceAndNeverReusesItsPinch()
        {
            game.Run.Tick(.1f,false);game.Run.Tick(.1f,true);game.Run.CancelTrigger();
            Assert.Less(game.Run.Water,game.Run.Capacity);
            var supply=game.Run.Supply;
            for(int i=0;i<4;i++)supply.SampleDock(WaterSourceKind.Hydrant,1,0,0,true,.1f);
            Assert.IsTrue(supply.Locked);game.Refresh();Physics.SyncTransforms();
            var blocked=Button("Return equipment");
            for(int i=0;i<8;i++)Assert.IsFalse(Sample(0,blocked,Open));
            Assert.IsFalse(game.MenuInput.OwnsInput);Assert.IsFalse(Sample(0,blocked,Closed));
            Assert.IsTrue(game.HoldingEquipment);Assert.IsTrue(supply.Locked);
            Click(0,"Next incident");Assert.IsTrue(supply.Assisted);Assert.IsTrue(supply.Locked);
            var assist=Button("Next incident");
            for(int i=0;i<12;i++)Assert.IsFalse(Sample(0,assist,Closed));
            Assert.Zero(game.Run.Charge01);Assert.IsFalse(game.Spraying);
            yield return null;
        }

        [UnityTest] public IEnumerator FocusAndPauseRejectUiAndRequireFreshOpenAfterRecovery()
        {
            var button=Button("Return equipment");Ready(0,button);
            game.SendMessage("OnApplicationFocus",false);
            for(int i=0;i<10;i++)Assert.IsFalse(Sample(0,button,i<8?Open:Closed));
            Assert.IsFalse(game.MenuInput.OwnsInput);Assert.IsTrue(game.HoldingEquipment);
            game.SendMessage("OnApplicationFocus",true);
            for(int i=0;i<10;i++)Assert.IsFalse(Sample(0,button,Closed));
            Ready(0,button);game.SendMessage("OnApplicationPause",true);
            for(int i=0;i<10;i++)Assert.IsFalse(Sample(0,button,i<8?Open:Closed));
            Assert.IsTrue(game.HoldingEquipment);Assert.IsFalse(game.MenuInput.OwnsInput);
            game.SendMessage("OnApplicationPause",false);
            for(int i=0;i<10;i++)Assert.IsFalse(Sample(0,button,Closed));
            Click(0,"Return equipment");Assert.IsFalse(game.HoldingEquipment);
            yield return null;
        }
    }
}

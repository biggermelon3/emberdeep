using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    // Starts from a real palm approach rather than pre-snapping the tool. These
    // synthetic scene tests do not establish physical tracking or comfort.
    public sealed class EmberdeepRefillApproachPlayTests
    {
        FireRescueGame game;
        const float Dt=.025f;
        IEnumerator Load(int hand)
        {
            EmberdeepSession.NewExpedition();yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);
            yield return null;yield return null;
            game=Object.FindFirstObjectByType<FireRescueGame>();Assert.NotNull(game);game.enabled=false;
            game.SendMessage("OnApplicationFocus",true);game.SendMessage("OnApplicationPause",false);
            // This approach fixture targets the rotary upgrade; separate shop
            // scene coverage earns and purchases tiers through production rules.
            typeof(FireRun).GetProperty(nameof(FireRun.StationTier)).SetValue(game.Run,WaterStationTier.RotaryHydrant);
            game.HydrantStation.SetTier(game.Run.StationTier);
            Assert.IsTrue(game.TakeEquipment(hand));game.Run.Tick(.02f,false);
            for(int i=0;i<100 && game.Run.Water>0;i++)game.Run.Tick(.1f,true);
            game.Run.CancelTrigger();Assert.Zero(game.Run.Water);
        }
        void Sample(int hand,Pose palm,float pinch,Vector3 tip)
        {
            var h=new EmberdeepRefillInteraction.Hand(true,palm,pinch,tip);
            var other=new EmberdeepRefillInteraction.Hand(true,new Pose(game.Arena.position+Vector3.up,Quaternion.identity),.04f);
            game.ProcessRefillHands(hand==0?h:other,hand==1?h:other,Dt);
        }
        Pose DockPalm()
        {
            var tool=game.HeldTool;var socket=game.HydrantStation.Socket;
            var rotation=socket.rotation*Quaternion.Euler(0,180,0)*Quaternion.Inverse(tool.RefillPort.localRotation);
            return new Pose(socket.position+rotation*(tool.Grip.localPosition-tool.RefillPort.localPosition),Quaternion.Euler(80,45,110));
        }
        void Approach(int hand)
        {
            var palm=DockPalm();palm.position+=game.HydrantStation.Socket.right*.14f;
            for(int i=0;i<12;i++)
            {
                Assert.IsTrue(EmberdeepRefillInteraction.TryGuideHeldTool(game.HeldTool,palm,game.HydrantStation,game.CreekStation));
                Assert.That(Vector3.Distance(palm.position,game.HeldTool.Grip.position),Is.LessThan(.0001f));
                Sample(hand,palm,.04f,palm.position);
            }
            Assert.IsFalse(game.Run.Supply.Locked,"Orientation guidance never bypasses actual port distance");
            Assert.Zero(game.Run.Water);
            palm=DockPalm();palm.position+=game.HydrantStation.Socket.right*.055f;
            for(int i=0;i<9;i++)
            {
                EmberdeepRefillInteraction.TryGuideHeldTool(game.HeldTool,palm,game.HydrantStation,game.CreekStation);
                Sample(hand,palm,.04f,palm.position);
            }
            Assert.IsFalse(game.Run.Supply.Locked,"An incomplete hold must not connect");
            Assert.Greater(game.Run.Supply.Alignment01,0);
            for(int i=0;i<3;i++)
            {
                if(!game.Run.Supply.Locked)EmberdeepRefillInteraction.TryGuideHeldTool(game.HeldTool,palm,game.HydrantStation,game.CreekStation);
                Sample(hand,palm,.04f,palm.position);
            }
            Assert.IsTrue(game.Run.Supply.Locked);Assert.Zero(game.Run.Water);Assert.IsFalse(game.Run.Supply.ValveOpen);
        }
        [UnityTest] public IEnumerator LeftHandApproachesGuidedDockThenTurnsWithFingertip()=>ApproachAndFill(0);
        [UnityTest] public IEnumerator RightHandApproachesGuidedDockThenTurnsWithFingertip()=>ApproachAndFill(1);
        IEnumerator ApproachAndFill(int hand)
        {
            yield return Load(hand);Approach(hand);
            var station=game.HydrantStation;
            station.ShowTutorial(game.Run,game.HeldTool,true,null,true);
            Assert.AreEqual(EmberdeepRefillTutorial.Target.Valve,station.TutorialCue.Focus);
            // Wrist/palm stays 16 cm away: only the fingertip reaches the broad wheel.
            var palm=new Pose(station.Valve.position+station.transform.right*.16f,Quaternion.identity);
            for(int i=0;i<6;i++)Sample(hand,palm,.04f,station.Valve.position);
            Sample(hand,palm,.01f,station.Valve.position);
            Assert.IsFalse(game.Run.Supply.ValveOpen,"Closing the pinch alone cannot rotate a valve");
            palm.rotation=Quaternion.AngleAxis(8,station.Valve.forward)*palm.rotation;
            Sample(hand,palm,.04f,station.Valve.position);
            Assert.IsFalse(game.Run.Supply.ValveOpen,"A released tap or partial rotation cannot grant water");Assert.Zero(game.Run.Water);
            palm.rotation=Quaternion.identity;
            for(int i=0;i<6;i++)Sample(hand,palm,.04f,station.Valve.position);
            Sample(hand,palm,.01f,station.Valve.position);
            palm.rotation=Quaternion.AngleAxis(50,station.Valve.forward)*palm.rotation;
            Sample(hand,palm,.01f,station.Valve.position);
            Assert.IsTrue(game.Run.Supply.ValveOpen,"A held rotation past 45 degrees starts filling");Assert.Greater(game.Run.Water,0);
            Sample(hand,palm,.04f,station.Valve.position);
            for(int i=0;i<90;i++)Sample(hand,palm,.04f,station.Valve.position);
            Assert.AreEqual(game.Run.Capacity,game.Run.Water);Assert.IsFalse(game.Run.Supply.ValveOpen);
            station.ShowTutorial(game.Run,game.HeldTool,true,null,true);
            Assert.AreEqual(EmberdeepRefillTutorial.Step.Full,station.TutorialCue.Stage);
            float full=game.Run.Water;
            var grip=game.HeldTool.Grip.position;palm.position=grip+Vector3.up*.15f;
            for(int i=0;i<6;i++)Sample(hand,palm,.04f,grip);
            Sample(hand,palm,.01f,grip);
            Assert.IsFalse(game.Run.Supply.Locked);Assert.AreEqual(full,game.Run.Water);
            yield return null;
        }

        [UnityTest] public IEnumerator FingertipAtUndockedValveCannotGrantAnyWater()
        {
            yield return Load(0);var valve=game.HydrantStation.Valve.position;
            game.HeldTool.transform.position=game.Arena.position+Vector3.up;
            var palm=new Pose(valve+Vector3.right*.16f,Quaternion.identity);
            for(int i=0;i<8;i++)Sample(0,palm,.04f,valve);
            Sample(0,palm,.01f,valve);
            palm.rotation=Quaternion.AngleAxis(50,game.HydrantStation.Valve.forward)*palm.rotation;
            Sample(0,palm,.01f,valve);Sample(0,palm,.04f,valve);
            Assert.IsFalse(game.Run.Supply.Locked);Assert.IsFalse(game.Run.Supply.ValveOpen);
            Assert.Zero(game.Run.Water);Assert.Zero(game.Run.SuppliedWater);
            yield return null;
        }

        [UnityTest] public IEnumerator MalformedFingertipInterruptsAcceptedFlowWithoutExtraTransfer()
        {
            yield return Load(1);Approach(1);game.Run.Supply.SetValveOpen(true);game.Run.Supply.Tick(.1f,true);
            float partial=game.Run.Water;
            Sample(1,new Pose(game.HydrantStation.Valve.position,Quaternion.identity),.04f,new Vector3(float.NaN,0,0));
            Assert.IsFalse(game.Run.Supply.Locked);Assert.AreEqual(partial,game.Run.Water);
            Assert.IsFalse(game.Run.Supply.ValveOpen);Assert.Zero(game.Run.Charge01);
            yield return null;
        }

        [UnityTest] public IEnumerator LeftExtinguisherFiringPinchCannotBeCapturedByCreek()=>ClosedApproach(0,FireEquipmentId.Extinguisher);
        [UnityTest] public IEnumerator RightExtinguisherFiringPinchCannotBeCapturedByCreek()=>ClosedApproach(1,FireEquipmentId.Extinguisher);
        [UnityTest] public IEnumerator LeftPistolFiringPinchCannotBeCapturedByCreek()=>ClosedApproach(0,FireEquipmentId.PulsePistol);
        [UnityTest] public IEnumerator RightPistolFiringPinchCannotBeCapturedByCreek()=>ClosedApproach(1,FireEquipmentId.PulsePistol);
        [UnityTest] public IEnumerator LeftFanFiringPinchCannotBeCapturedByCreek()=>ClosedApproach(0,FireEquipmentId.RainFan);
        [UnityTest] public IEnumerator RightFanFiringPinchCannotBeCapturedByCreek()=>ClosedApproach(1,FireEquipmentId.RainFan);
        [UnityTest] public IEnumerator LeftCannonChargeCannotBeCapturedByCreek()=>ClosedApproach(0,FireEquipmentId.PressureCannon);
        [UnityTest] public IEnumerator RightCannonChargeCannotBeCapturedByCreek()=>ClosedApproach(1,FireEquipmentId.PressureCannon);
        IEnumerator ClosedApproach(int hand,FireEquipmentId id)
        {
            yield return Load(hand);
            // Earn unlocks through the production run; buy only this fixture's tool.
            for(int stage=0;stage<3;stage++)
            {
                foreach(var tree in game.Run.Trees)if(tree.State==TreeState.Burning)game.Run.ApplyWater(tree.Position,100,.1f);
                Assert.AreEqual(FirePhase.Cleared,game.Run.Phase);
                if(stage<2)game.StartNext();
            }
            if(id!=FireEquipmentId.Extinguisher)Assert.IsTrue(game.Run.BuyEquipment(id));
            game.SelectEquipment((int)id);game.StartNext();Assert.IsTrue(game.TakeEquipment(hand));
            // ReturnEquipment positions the real rack for the current hand. Its
            // pickup must remain outside the new creek's orientation-guidance band.
            game.ReturnEquipment();
            var rackTools=game.transform.Find("Equipment rack").GetComponentsInChildren<FireEquipmentView>();
            Assert.AreEqual(1,rackTools.Length);Assert.AreEqual(id,rackTools[0].Id);
            var rackGrip=rackTools[0].Grip;var preview=game.HeldTool;var socket=game.CreekStation.Socket;
            var dockingRotation=socket.rotation*Quaternion.Euler(0,180,0)*Quaternion.Inverse(preview.RefillPort.localRotation);
            var dockingGrip=socket.position+dockingRotation*(preview.Grip.localPosition-preview.RefillPort.localPosition);
            float pickupClearance=Vector3.Distance(rackGrip.position,dockingGrip);
            Assert.Greater(pickupClearance,.18f,$"{id}, hand {hand}: rack pickup must clear the .16 m guide plus buffer");
            var previewPosition=preview.transform.position;var previewRotation=preview.transform.rotation;
            Assert.IsFalse(EmberdeepRefillInteraction.TryGuideHeldTool(preview,new Pose(rackGrip.position,rackGrip.rotation),
                game.HydrantStation,game.CreekStation,.04f),"Opening at the actual rack grip cannot pull the tool toward either source");
            Assert.AreEqual(previewPosition,preview.transform.position);Assert.AreEqual(previewRotation,preview.transform.rotation);
            TestContext.WriteLine($"Synthetic layout: {id}, hand {hand}, rack-to-creek grip clearance {pickupClearance:F4} m");
            Assert.IsTrue(game.TakeEquipment(hand));
            var supply=game.Run.Supply;
            for(int i=0;i<4;i++)supply.SampleDock(WaterSourceKind.Hydrant,hand,0,0,true,.1f);
            Assert.IsTrue(supply.SetValveOpen(true));for(int i=0;i<100;i++)supply.Tick(.1f,true);
            supply.Disconnect();game.Run.Tick(Dt,false);
            if(id==FireEquipmentId.PressureCannon)Assert.IsTrue(game.Run.PrimeCannon(1));
            else Assert.IsTrue(game.Run.Tick(.1f,true));
            float charge=game.Run.Charge01,water=game.Run.Water,supplied=game.Run.SuppliedWater;
            var tool=game.HeldTool;var station=game.CreekStation;
            EmberdeepRefillInteraction.Snap(tool,station.Socket);
            var dockingPalm=new Pose(tool.Grip.position,Quaternion.Euler(30,75,15));
            // Even perfectly aligned ports must not connect under a firing pinch.
            var beforePosition=tool.transform.position;var beforeRotation=tool.transform.rotation;
            for(int i=0;i<20;i++)
            {
                Assert.IsFalse(EmberdeepRefillInteraction.TryGuideHeldTool(tool,dockingPalm,game.HydrantStation,station,.01f));
                Sample(hand,dockingPalm,.01f,dockingPalm.position);
            }
            Assert.AreEqual(beforePosition,tool.transform.position);Assert.AreEqual(beforeRotation,tool.transform.rotation);
            Assert.IsFalse(supply.Locked);Assert.Zero(supply.Alignment01);
            Assert.AreEqual(charge,game.Run.Charge01);Assert.AreEqual(water,game.Run.Water);Assert.AreEqual(supplied,game.Run.SuppliedWater);
            // Inside the former socket sphere but outside the intended grip band.
            var passing=dockingPalm;passing.position+=station.transform.right*(hand==0?-.17f:.17f);
            Assert.IsTrue(EmberdeepRefillInteraction.NearStation(passing.position,station));
            Assert.IsFalse(EmberdeepRefillInteraction.TryGuideHeldTool(tool,passing,game.HydrantStation,station,.04f));
            Assert.AreEqual(beforePosition,tool.transform.position);Assert.AreEqual(beforeRotation,tool.transform.rotation);
            for(int i=0;i<5;i++)Sample(hand,dockingPalm,.04f,dockingPalm.position);
            Assert.Greater(supply.Alignment01,0);Assert.IsFalse(supply.Locked);
            Sample(hand,dockingPalm,.01f,dockingPalm.position);Assert.Zero(supply.Alignment01);
            Assert.IsTrue(EmberdeepRefillInteraction.TryGuideHeldTool(tool,dockingPalm,game.HydrantStation,station,.04f));
            for(int i=0;i<9;i++)Sample(hand,dockingPalm,.04f,dockingPalm.position);
            Assert.IsFalse(supply.Locked,"A closed sample discards the previous partial dwell");
            for(int i=0;i<3;i++)Sample(hand,dockingPalm,.04f,dockingPalm.position);
            Assert.IsTrue(supply.Locked);Assert.AreEqual(WaterSourceKind.Creek,supply.Kind);Assert.AreEqual(hand,supply.OwnerHand);
            Assert.AreEqual(water,game.Run.Water);Assert.AreEqual(supplied,game.Run.SuppliedWater,"Docking alone never transfers water");
            yield return null;
        }

        [UnityTest] public IEnumerator SecondSectorOptionalCreekStaysOutsideBothNormalToolSides()
        {
            yield return Load(1);Assert.AreEqual(0,game.Run.Stage);Assert.IsFalse(game.CreekStation.gameObject.activeSelf);
            foreach(var tree in game.Run.Trees)if(tree.State==TreeState.Burning)game.Run.ApplyWater(tree.Position,100,.1f);
            game.StartNext();Assert.AreEqual(1,game.Run.Stage);Assert.IsTrue(game.CreekStation.gameObject.activeSelf);
            for(int hand=0;hand<2;hand++)
            {
                Assert.IsTrue(game.TakeEquipment(hand));var creek=game.CreekStation;
                var local=game.Arena.InverseTransformPoint(creek.transform.position);
                Assert.That(local.x,Is.EqualTo(hand==0?-4f:4f).Within(.0001f));
                Assert.That(local.z,Is.EqualTo(3.5f).Within(.0001f));
                Assert.That(Mathf.Abs(Vector3.Dot(creek.transform.position-game.Arena.position,game.Arena.right)),Is.EqualTo(.32f).Within(.0001f));
                // Geometric reach envelope only: synthetic head/anchors cannot
                // establish comfort, hand tracking or usability on a headset.
                foreach(var control in new[]{creek.Pump,creek.Lever,game.HydrantStation.Lever})
                {
                    float horizontal=Vector3.ProjectOnPlane(control.position-game.Player.ViewCamera.transform.position,Vector3.up).magnitude;
                    Assert.LessOrEqual(horizontal,.75f,$"{control.name}, hand {hand}: layout reach envelope");
                    TestContext.WriteLine($"Synthetic layout: {control.name}, hand {hand}, head horizontal distance {horizontal:F4} m; not a comfort verdict");
                }
                // Enable the production XR tutorial using synthetic availability;
                // desktop status intentionally presents keyboard shortcuts instead.
                creek.ShowTutorial(game.Run,game.HeldTool,true,null,true);
                bool optional=false;foreach(var label in creek.GetComponentsInChildren<TextMesh>(true))
                    optional|=label.gameObject.activeInHierarchy && label.text.Contains("OPTIONAL");
                Assert.IsTrue(optional,"The extra source must advertise that it is optional");
                Assert.IsTrue(game.HydrantStation.gameObject.activeInHierarchy,"The familiar free source remains reachable");
            }
            yield return null;
        }
    }
}

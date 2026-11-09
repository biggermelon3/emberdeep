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
    }
}

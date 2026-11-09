using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    // Synthetic tracked palms drive the production adapter in the actual scene.
    // These checks and captures establish Editor behavior, not headset acceptance.
    public sealed class EmberdeepWaterPlayTests
    {
        FireRescueGame game;
        const float Step=.025f;

        IEnumerator Load(int hand)
        {
            EmberdeepSession.NewExpedition();
            yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);
            yield return null;yield return null;
            game=Object.FindFirstObjectByType<FireRescueGame>();
            Assert.NotNull(game);Assert.NotNull(game.HydrantStation);
            game.enabled=false;
            game.SendMessage("OnApplicationPause",false);
            game.SendMessage("OnApplicationFocus",true);
            // These existing fixture/edge tests exercise the rotary tier. Shop
            // progression is covered separately through earned coins and purchases.
            typeof(FireRun).GetProperty(nameof(FireRun.StationTier)).SetValue(game.Run,WaterStationTier.RotaryHydrant);
            game.HydrantStation.SetTier(game.Run.StationTier);
            Assert.IsTrue(game.TakeEquipment(hand));
        }

        void Sample(int hand,Vector3 point,float pinch=.04f,Quaternion? rotation=null,bool tracked=true)
        {
            var active=new EmberdeepRefillInteraction.Hand(tracked,new Pose(point,rotation??Quaternion.identity),pinch);
            var other=new EmberdeepRefillInteraction.Hand(true,new Pose(game.Arena.position+Vector3.up*1.2f,Quaternion.identity),.04f);
            game.ProcessRefillHands(hand==0?active:other,hand==1?active:other,Step);
        }
        void Open(int hand,Vector3 point,Quaternion? rotation=null)
        {for(int i=0;i<5;i++)Sample(hand,point,.04f,rotation);}
        void TurnValve(int hand,float degrees=50)
        {
            var valve=game.HydrantStation.Valve;var neutral=valve.rotation;
            Open(hand,valve.position,neutral);Sample(hand,valve.position,.01f,neutral);
            var turned=Quaternion.AngleAxis(degrees,valve.forward)*neutral;
            Sample(hand,valve.position,.01f,turned);Sample(hand,valve.position,.04f,turned);
        }
        void Dock(int hand,EmberdeepWaterStationView station)
        {
            EmberdeepRefillInteraction.Snap(game.HeldTool,station.Socket);
            for(int i=0;i<9;i++)Sample(hand,game.HeldTool.Grip.position);
            Assert.IsFalse(game.Run.Supply.Locked,"An incomplete aligned hold cannot snap");
            for(int i=0;i<3;i++)Sample(hand,game.HeldTool.Grip.position);
            Assert.IsTrue(game.Run.Supply.Locked);
            Assert.AreEqual(hand,game.Run.Supply.OwnerHand);
            Assert.Less(Vector3.Distance(game.HeldTool.RefillPort.position,station.Socket.position),.001f);
            Assert.That(Quaternion.Angle(game.HeldTool.RefillPort.rotation,station.Socket.rotation*Quaternion.Euler(0,180,0)),Is.LessThan(.1f));
        }
        void Drain(float desired=0)
        {
            game.Run.Tick(.02f,false);
            for(int i=0;i<100 && game.Run.Water>desired;i++)game.Run.Tick(.1f,true);
            game.Run.CancelTrigger();
        }
        static void Clear(FireRun run)
        {for(int i=0;i<run.Trees.Length;i++)if(run.Trees[i].State==TreeState.Burning)run.ApplyWater(run.Trees[i].Position,100,.1f);}

        [UnityTest] public IEnumerator LeftHandHydrantDockValveFullAndDisconnect()=>Hydrant(0);
        [UnityTest] public IEnumerator RightHandHydrantDockValveFullAndDisconnect()=>Hydrant(1);
        IEnumerator Hydrant(int hand)
        {
            yield return Load(hand);
            Drain();Assert.Zero(game.Run.Water);
            Assert.IsFalse(game.CreekStation.gameObject.activeSelf,"Trailhead teaches one source first");
            Dock(hand,game.HydrantStation);
            var valve=game.HydrantStation.Valve;
            Quaternion neutral=valve.rotation;
            Open(hand,valve.position,neutral);
            for(int i=0;i<20;i++)Sample(hand,valve.position,.04f,neutral);
            Assert.Zero(game.Run.Water,"Docking with a closed valve gives no water");

            // Failed partial turn returns to its last detent; it is not an accessible tap.
            Sample(hand,valve.position,.01f,neutral);
            Sample(hand,valve.position,.01f,Quaternion.AngleAxis(20,valve.forward)*neutral);
            Sample(hand,valve.position,.04f,Quaternion.AngleAxis(20,valve.forward)*neutral);
            Assert.IsFalse(game.Run.Supply.ValveOpen);Assert.Zero(game.Run.Water);

            // A tap alone is deliberately not a rotary action in this tier.
            Open(hand,valve.position,neutral);
            Sample(hand,valve.position,.01f,neutral);
            Assert.IsFalse(game.Run.Supply.ValveOpen);
            Sample(hand,valve.position,.04f,neutral);
            Assert.IsFalse(game.Run.Supply.ValveOpen);Assert.Zero(game.Run.Water);
            TurnValve(hand);
            Assert.IsTrue(game.Run.Supply.ValveOpen);
            Assert.Greater(game.Run.Water,0);
            Assert.IsFalse(game.Run.Tick(.025f,true),"A supply-owned tool cannot fire");
            Assert.Zero(game.Run.SprayAmount);Assert.Zero(game.Run.Charge01);
            Open(hand,valve.position,neutral);
            Sample(hand,valve.position,.01f,neutral);
            Sample(hand,valve.position,.01f,Quaternion.AngleAxis(-50,valve.forward)*neutral);
            Sample(hand,valve.position,.04f,Quaternion.AngleAxis(-50,valve.forward)*neutral);
            Assert.IsFalse(game.Run.Supply.ValveOpen,"Reverse physical turn closes the accepted detent");
            float partial=game.Run.Water;
            for(int i=0;i<10;i++)Sample(hand,valve.position,.04f,neutral);
            Assert.AreEqual(partial,game.Run.Water);

            Open(hand,valve.position,neutral);
            Sample(hand,valve.position,.01f,neutral);
            Sample(hand,valve.position,.01f,Quaternion.AngleAxis(50,valve.forward)*neutral);
            Assert.IsTrue(game.Run.Supply.ValveOpen,"A deliberate 45-degree turn opens flow");
            Sample(hand,valve.position,.04f,Quaternion.AngleAxis(50,valve.forward)*neutral);
            for(int i=0;i<100;i++)Sample(hand,valve.position,.04f,neutral);
            Assert.AreEqual(game.Run.Capacity,game.Run.Water);
            Assert.AreEqual(HydrantRefillState.DockedFull,game.Run.Supply.HydrantState);
            Assert.IsFalse(game.Run.Supply.ValveOpen);
            Assert.That(game.Run.SuppliedWater,Is.EqualTo(game.Run.Capacity).Within(.001f));
            game.Refresh();
            if(hand==1)Capture(game.Player.ViewCamera,"Logs/EmberdeepM1/Editor-Hydrant.png");

            // A held pinch from another action cannot disconnect by moving to the grip.
            Sample(hand,valve.position,.01f,neutral);
            Sample(hand,game.HeldTool.Grip.position,.01f,neutral);
            Assert.IsTrue(game.Run.Supply.Locked);
            Open(hand,game.HeldTool.Grip.position,neutral);
            Sample(hand,game.HeldTool.Grip.position,.01f,neutral);
            Assert.IsFalse(game.Run.Supply.Locked);Assert.IsTrue(game.HoldingEquipment);
            float full=game.Run.Water;
            Assert.IsFalse(game.Run.Tick(.025f,true),"Disconnect cannot become a firing edge");
            Assert.AreEqual(full,game.Run.Water);
            game.Run.Tick(.025f,false);
            Assert.IsTrue(game.Run.Tick(.025f,true));Assert.Less(game.Run.Water,full);
            yield return null;
        }

        [UnityTest] public IEnumerator LeftHandCreekIntakeAndCompleteStrokes()=>Creek(0);
        [UnityTest] public IEnumerator RightHandCreekIntakeAndCompleteStrokes()=>Creek(1);
        IEnumerator Creek(int hand)
        {
            yield return Load(hand);Clear(game.Run);game.StartNext();
            Assert.AreEqual(1,game.Run.Stage);Assert.IsTrue(game.CreekStation.gameObject.activeSelf);
            Assert.IsTrue(game.TakeEquipment(hand));Drain();Assert.Zero(game.Run.Water);
            Dock(hand,game.CreekStation);
            var station=game.CreekStation;
            Open(hand,station.Pump.position);
            Sample(hand,station.Pump.position,.01f);
            Sample(hand,station.Pump.position-station.Pump.forward*.09f,.01f);
            Sample(hand,station.Pump.position,.01f);
            Sample(hand,station.Pump.position,.04f);
            Assert.Zero(game.Run.Water,"An unprimed creek cannot transfer");

            Open(hand,station.Intake.position);
            Sample(hand,station.Intake.position,.01f);
            Sample(hand,station.IntakeSocket.position,.01f);
            Sample(hand,station.IntakeSocket.position,.04f);
            Assert.IsTrue(game.Run.Supply.IntakePlaced);
            Assert.AreEqual(CreekRefillState.Primed,game.Run.Supply.CreekState);
            Assert.Zero(game.Run.Water,"Placing the intake is not a stroke");

            Open(hand,station.Pump.position);
            Sample(hand,station.Pump.position,.01f);
            Sample(hand,station.Pump.position-station.Pump.forward*.04f,.01f);
            Sample(hand,station.Pump.position,.04f);
            Assert.Zero(game.Run.Water,"Partial travel cannot grant water");
            Open(hand,station.Pump.position);
            Sample(hand,station.Pump.position,.01f);
            Sample(hand,station.Pump.position-station.Pump.forward*.09f,.01f);
            Assert.Zero(game.Run.Water,"Outward travel still requires the return");
            Sample(hand,station.Pump.position,.01f);
            Assert.That(game.Run.Water,Is.EqualTo(12).Within(.001f));
            for(int i=0;i<10;i++)Sample(hand,station.Pump.position,.01f);
            Assert.That(game.Run.Water,Is.EqualTo(12).Within(.001f),"Repeated return callbacks pay once");
            for(int stroke=0;stroke<4;stroke++)
            {Sample(hand,station.Pump.position-station.Pump.forward*.09f,.01f);Sample(hand,station.Pump.position,.01f);}
            Assert.AreEqual(game.Run.Capacity,game.Run.Water);
            Assert.That(game.Run.SuppliedWater,Is.EqualTo(60).Within(.001f));
            Assert.Zero(game.Run.Charge01);Assert.Zero(game.Run.SprayAmount);
            game.Refresh();if(hand==1)Capture(game.Player.ViewCamera,"Logs/EmberdeepM1/Editor-Creek.png");
            Sample(hand,station.Pump.position,.04f);
            yield return null;
        }

        [UnityTest] public IEnumerator TrackingAndPauseInterruptFlowWithoutResumeGrant()
        {
            yield return Load(0);Drain();Dock(0,game.HydrantStation);
            var valve=game.HydrantStation.Valve;
            TurnValve(0);
            float partial=game.Run.Water;Assert.Greater(partial,0);
            Sample(0,valve.position,.01f,null,false);
            Assert.IsFalse(game.Run.Supply.Locked);Assert.IsFalse(game.HoldingEquipment);
            for(int i=0;i<20;i++)Sample(0,valve.position,.01f);
            Assert.AreEqual(partial,game.Run.Water);Assert.Zero(game.Run.SprayAmount);
            Assert.IsTrue(game.TakeEquipment(0));Dock(0,game.HydrantStation);
            TurnValve(0);
            partial=game.Run.Water;
            game.SendMessage("OnApplicationPause",true);
            game.SendMessage("OnApplicationFocus",true);
            for(int i=0;i<20;i++)Sample(0,valve.position,.04f);
            Assert.AreEqual(partial,game.Run.Water,"Focus recovery alone cannot override pause");
            Assert.IsFalse(game.Run.Supply.Locked);
            game.SendMessage("OnApplicationPause",false);
            for(int i=0;i<20;i++)Sample(0,valve.position,.01f);
            Assert.AreEqual(partial,game.Run.Water);
            yield return null;
        }

        [UnityTest] public IEnumerator CreekPumpCannotChargeOrLaunchCannonAndWeaponPumpStillWorksAfterward()
        {
            yield return Load(1);
            for(int stage=0;stage<3;stage++){Clear(game.Run);if(stage<2)game.StartNext();}
            Assert.IsTrue(game.Run.BuyEquipment(FireEquipmentId.PressureCannon));
            game.SelectEquipment((int)FireEquipmentId.PressureCannon);game.StartNext();
            Assert.IsTrue(game.TakeEquipment(1));Assert.Zero(game.Run.Water);
            Dock(1,game.CreekStation);var station=game.CreekStation;
            Open(1,station.Intake.position);Sample(1,station.Intake.position,.01f);
            Sample(1,station.IntakeSocket.position,.01f);Sample(1,station.IntakeSocket.position,.04f);
            Open(1,station.Pump.position);Sample(1,station.Pump.position,.01f);
            for(int stroke=0;stroke<3;stroke++)
            {Sample(1,station.Pump.position-station.Pump.forward*.09f,.01f);Sample(1,station.Pump.position,.01f);}
            Assert.That(game.Run.Water,Is.EqualTo(36).Within(.001f));
            Assert.IsFalse(game.Run.PrimeCannon(1),"Supply mode owns the tool");
            Sample(1,station.Pump.position,.04f);
            Assert.IsFalse(game.Run.Tick(.025f,false));Assert.Zero(game.Run.SprayAmount);Assert.Zero(game.Run.Charge01);
            Open(1,game.HeldTool.Grip.position);Sample(1,game.HeldTool.Grip.position,.01f);
            Assert.IsFalse(game.Run.Supply.Locked);
            Assert.IsFalse(game.Run.PrimeCannon(1),"Detach requires open rearming");
            game.Run.Tick(.025f,false);
            var cannonPump=new FirePumpGesture();var handle=new Pose(game.HeldTool.Pump.position,game.HeldTool.transform.rotation);
            for(int i=0;i<5;i++)cannonPump.Sample(true,.04f,handle.position,handle,.025f);
            cannonPump.Sample(true,.01f,handle.position,handle,.025f);
            cannonPump.Sample(true,.01f,handle.position-handle.rotation*Vector3.forward*.09f,handle,.025f);
            Assert.IsTrue(game.Run.PrimeCannon(cannonPump.Pull01));
            cannonPump.Sample(true,.04f,handle.position-handle.rotation*Vector3.forward*.09f,handle,.025f);
            Assert.IsTrue(cannonPump.Released);
            Assert.IsTrue(game.Run.Tick(.025f,false));Assert.Greater(game.Run.SprayAmount,0);
            Assert.That(game.Run.Water,Is.EqualTo(8).Within(.001f));
            yield return null;
        }

        [UnityTest] public IEnumerator NonfiniteTrackedSamplesInterruptHydrantWithoutAnotherTransfer()
        {
            for(int invalid=0;invalid<3;invalid++)
            {
                yield return Load(0);Drain();Dock(0,game.HydrantStation);
                var valve=game.HydrantStation.Valve;
                TurnValve(0);
                float partial=game.Run.Water;Assert.Greater(partial,0);
                if(invalid==0)Sample(0,new Vector3(float.NaN,valve.position.y,valve.position.z));
                else if(invalid==1)Sample(0,valve.position,.04f,new Quaternion(float.PositiveInfinity,0,0,1));
                else Sample(0,valve.position,float.NaN);
                Assert.IsFalse(game.Run.Supply.Locked,"Malformed tracked sample must interrupt case "+invalid);
                Assert.AreEqual(partial,game.Run.Water,"Malformed sample cannot transfer case "+invalid);
                Assert.IsFalse(game.HoldingEquipment);Assert.Zero(game.Run.SprayAmount);
            }
        }

        [UnityTest] public IEnumerator InterruptedCreekStrokeReturnsIntakeAndRequiresFreshPriming()
        {
            yield return Load(1);Clear(game.Run);game.StartNext();Assert.IsTrue(game.TakeEquipment(1));Drain();
            var station=game.CreekStation;var rest=station.Intake.position;
            Dock(1,station);
            Open(1,station.Intake.position);Sample(1,station.Intake.position,.01f);
            Sample(1,station.IntakeSocket.position,.01f);Sample(1,station.IntakeSocket.position,.04f);
            Assert.IsTrue(game.Run.Supply.IntakePlaced);
            Open(1,station.Pump.position);Sample(1,station.Pump.position,.01f);
            Sample(1,station.Pump.position-station.Pump.forward*.09f,.01f);
            Sample(1,station.Pump.position,.01f,null,false);
            Assert.Zero(game.Run.Water);Assert.Zero(game.Run.SuppliedWater);
            Assert.IsFalse(game.Run.Supply.Locked);Assert.IsFalse(game.HoldingEquipment);
            Assert.Less(Vector3.Distance(station.Intake.position,rest),.001f,"Interrupted intake returns to its reachable anchor");
            Assert.IsTrue(game.TakeEquipment(1));Dock(1,station);
            Assert.IsFalse(game.Run.Supply.IntakePlaced);
            Open(1,station.Pump.position);Sample(1,station.Pump.position,.01f);
            Sample(1,station.Pump.position-station.Pump.forward*.09f,.01f);Sample(1,station.Pump.position,.01f);
            Assert.Zero(game.Run.Water,"Reconnecting cannot restore an uncompleted stroke or prime");
            yield return null;
        }

        [UnityTest] public IEnumerator AssistanceConsumesPinchWithoutOperatingValveOrLosingCreekPrime()
        {
            yield return Load(0);Drain();Dock(0,game.HydrantStation);
            var valve=game.HydrantStation.Valve;
            Open(0,valve.position);
            Assert.IsTrue(game.Run.Supply.BeginAssistedRefill());
            game.RefillInteraction.ConsumeInput(game.Run);
            for(int i=0;i<5;i++)Sample(0,valve.position,.01f);
            Sample(0,valve.position,.04f);
            Assert.IsTrue(game.Run.Supply.Locked);Assert.IsTrue(game.Run.Supply.Assisted);
            Assert.IsFalse(game.Run.Supply.ValveOpen);Assert.Zero(game.Run.Water);
            TurnValve(0);
            Assert.IsTrue(game.Run.Supply.ValveOpen,"A subsequent deliberate gesture still works");
            game.ReturnEquipment();Clear(game.Run);game.StartNext();game.TakeEquipment(0);Drain();
            var station=game.CreekStation;Dock(0,station);
            Open(0,station.Intake.position);Sample(0,station.Intake.position,.01f);
            Sample(0,station.IntakeSocket.position,.01f);Sample(0,station.IntakeSocket.position,.04f);
            Open(0,station.Pump.position);Sample(0,station.Pump.position,.01f);
            Sample(0,station.Pump.position-station.Pump.forward*.09f,.01f);
            game.Run.Supply.BeginAssistedRefill();game.RefillInteraction.ConsumeInput(game.Run);
            Sample(0,station.Pump.position,.01f);
            Assert.Zero(game.Run.Water,"The UI action cancels the unfinished stroke");
            Assert.IsTrue(game.Run.Supply.IntakePlaced);Assert.IsTrue(game.RefillInteraction.IntakePlaced);
            Open(0,station.Pump.position);Sample(0,station.Pump.position,.01f);
            Sample(0,station.Pump.position-station.Pump.forward*.09f,.01f);Sample(0,station.Pump.position,.01f);
            Assert.That(game.Run.Water,Is.EqualTo(12).Within(.001f));
            yield return null;
        }

        [UnityTest] public IEnumerator CampRoundTripRetainsPartialTankAndRefillCallbacksHaveOneLiveOwner()
        {
            yield return Load(1);Drain(30);
            var run=game.Run;float partial=run.Water;
            game.ReturnToMenu();
            float deadline=Time.realtimeSinceStartup+5;
            while(SceneManager.GetActiveScene().name!=EmberdeepSession.CampScene && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.AreEqual(EmberdeepSession.CampScene,SceneManager.GetActiveScene().name);
            yield return null;yield return null;
            var camp=Object.FindFirstObjectByType<EmberdeepCamp>();Assert.NotNull(camp);
            camp.EnterFireground();deadline=Time.realtimeSinceStartup+5;
            while(SceneManager.GetActiveScene().name!=EmberdeepSession.FireScene && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.AreEqual(EmberdeepSession.FireScene,SceneManager.GetActiveScene().name);
            yield return null;yield return null;
            game=Object.FindFirstObjectByType<FireRescueGame>();game.enabled=false;
            game.SendMessage("OnApplicationPause",false);game.SendMessage("OnApplicationFocus",true);
            Assert.AreSame(run,game.Run);Assert.AreEqual(partial,run.Water);
            Assert.IsTrue(game.TakeEquipment(1));Dock(1,game.HydrantStation);
            var valve=game.HydrantStation.Valve;
            TurnValve(1);
            Assert.Greater(run.Water,partial);
            Open(1,game.HeldTool.Grip.position);Sample(1,game.HeldTool.Grip.position,.01f);
            Assert.IsFalse(run.Supply.Locked);
            yield return null;
            // Unity Test Framework automatically fails error/exception logs. Informational
            // OVRManager lifecycle logs are expected when the scene's player is destroyed.
        }

        static void Capture(Camera camera,string path)
        {
            var target=new RenderTexture(1920,1080,24);var prior=camera.targetTexture;var active=RenderTexture.active;
            var pixels=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1920,1080),0,0);pixels.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally{camera.targetTexture=prior;RenderTexture.active=active;Object.Destroy(pixels);Object.Destroy(target);}
        }
    }
}

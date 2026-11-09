using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace FireRescue.Tests
{
    // Real fireground owner gates with synthetic accepted frame time/tracking.
    // These do not fake an XR subsystem or establish headset usability.
    public sealed class EmberdeepRecoveryGamePlayTests
    {
        FireRescueGame game;
        const float Dt=.1f;
        [UnitySetUp] public IEnumerator Load()
        {
            EmberdeepSession.NewExpedition();
            yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);yield return null;yield return null;
            game=Object.FindFirstObjectByType<FireRescueGame>();Assert.NotNull(game);game.enabled=false;
            game.SendMessage("OnApplicationPause",false);game.SendMessage("OnApplicationFocus",true);
            Assert.IsTrue(game.TakeEquipment(1));game.Run.Tick(Dt,false);
            for(int i=0;i<100 && game.Run.Water>0;i++)game.Run.Tick(Dt,true);
            game.Run.Tick(Dt,false);game.Run.CancelTrigger();game.Run.ResetRecovery();Assert.Zero(game.Run.Water);
        }
        void Sample(int frames,bool tracking=true,bool trigger=false)
        {for(int i=0;i<frames;i++)game.ProcessWaterRecovery(Dt,tracking,trigger);}
        static void Clear(FireRun run)
        {foreach(var tree in run.Trees)if(tree.State==TreeState.Burning)run.ApplyWater(tree.Position,100,.1f);}
        void Field(string name,object value)
        {
            var field=typeof(FireRescueGame).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.NotNull(field,name);field.SetValue(game,value);
        }
        object Navigation=>typeof(FireRescueGame).GetField("navigation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game);

        [UnityTest] public IEnumerator ActiveFightingAndClearedRackRecoverOnlyTheEquippedTank()
        {
            Sample(230);Assert.That(game.Run.Water,Is.EqualTo(5).Within(.001f));
            Assert.That(game.Run.RecoveredWater,Is.EqualTo(5).Within(.001f));Assert.Zero(game.Run.SuppliedWater);
            Clear(game.Run);Assert.AreEqual(FirePhase.Cleared,game.Run.Phase);
            Assert.IsTrue(game.Run.BuyEquipment(FireEquipmentId.PulsePistol));
            game.SelectEquipment((int)FireEquipmentId.PulsePistol);game.ReturnEquipment();
            Assert.AreEqual(FireEquipmentId.Extinguisher,game.Run.Equipped,"Browsing cannot change recovery recipient");
            float stored=game.Run.Water;game.Run.ResetRecovery();Sample(230);
            Assert.That(game.Run.WaterFor(FireEquipmentId.Extinguisher),Is.EqualTo(stored+5).Within(.001f));
            Assert.Zero(game.Run.WaterFor(FireEquipmentId.PulsePistol));Assert.IsFalse(game.HoldingEquipment);
            Assert.IsTrue(game.TakeEquipment(0));Assert.AreEqual(FireEquipmentId.PulsePistol,game.Run.Equipped);
            Sample(29);Assert.Zero(game.Run.Water);Sample(201);
            Assert.That(game.Run.Water,Is.EqualTo(5).Within(.001f));
            Assert.That(game.Run.WaterFor(FireEquipmentId.Extinguisher),Is.EqualTo(stored+5).Within(.001f));
            Assert.IsFalse(game.Spraying);Assert.Zero(game.Run.SprayAmount);yield return null;
        }

        [UnityTest] public IEnumerator PauseStopsRecoveryAndResumeRestartsDelay()=>Unavailable(0);
        [UnityTest] public IEnumerator FocusLossStopsRecoveryAndResumeRestartsDelay()=>Unavailable(1);
        [UnityTest] public IEnumerator TrackingSuspensionStopsRecoveryAndResumeRestartsDelay()=>Unavailable(2);
        [UnityTest] public IEnumerator WorldRepositioningStopsRecoveryAndRestartsDelay()=>Unavailable(3);
        [UnityTest] public IEnumerator ReturningToCampStopsRecoveryAndRestartsDelay()=>Unavailable(4);
        [UnityTest] public IEnumerator LoadingTransitionStopsRecoveryAndRestartsDelay()=>Unavailable(5);
        [UnityTest] public IEnumerator ZeroTimeScaleStopsRecoveryWithoutUnscaledCatchup()=>Unavailable(6);
        [UnityTest] public IEnumerator UnstartedFiregroundCannotRecoverBeforeGameplayBegins()=>Unavailable(7);
        IEnumerator Unavailable(int gate)
        {
            Sample(20);Assert.Zero(game.Run.Water);float oldScale=Time.timeScale;
            void Set(bool blocked)
            {
                if(gate==0)game.SendMessage("OnApplicationPause",blocked);
                if(gate==1)game.SendMessage("OnApplicationFocus",!blocked);
                if(gate==3)Field("acquiringPose",blocked);
                if(gate==4)Field("returningToCamp",blocked);
                if(gate==5)Navigation.GetType().GetProperty("IsTransitioning").SetValue(Navigation,blocked);
                if(gate==6)Time.timeScale=blocked?0:oldScale;
                if(gate==7)Field("introStarted",!blocked);
            }
            try
            {
                Set(true);Sample(100,gate!=2);Assert.Zero(game.Run.Water);Assert.Zero(game.Run.RecoveredWater);
                Assert.IsFalse(game.Run.RecoveryActive);
                Set(false);Sample(29);Assert.Zero(game.Run.Water,"Suspended time and previous partial delay cannot carry over");
                Sample(201);Assert.That(game.Run.Water,Is.EqualTo(5).Within(.001f));Assert.IsFalse(game.Spraying);
            }
            finally{Set(false);Time.timeScale=oldScale;}
            yield return null;
        }

        [UnityTest] public IEnumerator ShopStopsRecoveryAndCloseRequiresAFullNewDelay()
        {
            Clear(game.Run);Sample(20);game.OpenShop();Assert.IsTrue(game.ShopOpen);
            float water=game.Run.Water;Sample(230);Assert.AreEqual(water,game.Run.Water);Assert.Zero(game.Run.RecoveredWater);
            game.CloseShop();Sample(29);Assert.AreEqual(water,game.Run.Water);
            Sample(201);Assert.That(game.Run.Water,Is.EqualTo(water+5).Within(.001f));yield return null;
        }

        [UnityTest] public IEnumerator DryHeldTriggerCannotRecoverIntoIntermittentShots()
        {
            for(int i=0;i<230;i++)
            {
                Assert.IsFalse(game.Run.Tick(.01f,true));game.ProcessWaterRecovery(Dt,true,true);
                Assert.Zero(game.Run.Water);Assert.Zero(game.Run.SprayAmount);
            }
            Assert.Zero(game.Run.RecoveredWater);game.Run.Tick(.01f,false);Sample(230);
            Assert.That(game.Run.Water,Is.EqualTo(5).Within(.001f));Assert.Zero(game.Run.SprayAmount);
            Assert.IsFalse(game.Run.Tick(.01f,false),"Recovered affordability cannot itself fire a shot");yield return null;
        }

        [UnityTest] public IEnumerator RawMouseHeldBeforeGestureArmingStillBlocksTheProductionRecoveryInput()
        {
            Assert.IsFalse(game.Player.IsXR,"This test injects the desktop device; XR raw-pinching remains a provider boundary");
            var method=typeof(FireRescueGame).GetMethod("RecoveryTriggerHeld",BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.NotNull(method);var previousBackground=InputSystem.settings.backgroundBehavior;
            var mouse=InputSystem.AddDevice<Mouse>();
            try
            {
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                Field("desktopReady",false);
                InputSystem.QueueStateEvent(mouse,new MouseState{buttons=1});InputSystem.Update();
                Assert.IsTrue(Mouse.current.leftButton.isPressed);
                bool raw=(bool)method.Invoke(game,new object[]{false,float.PositiveInfinity});
                Assert.IsTrue(raw,"No armed/successful gameplay shot is required for raw held input to block recovery");
                Sample(230,true,raw);Assert.Zero(game.Run.Water);Assert.Zero(game.Run.RecoveredWater);
                InputSystem.QueueStateEvent(mouse,new MouseState{buttons=0});InputSystem.Update();
                Assert.IsFalse((bool)method.Invoke(game,new object[]{false,float.PositiveInfinity}));
                Sample(230);Assert.That(game.Run.Water,Is.EqualTo(5).Within(.001f));Assert.IsFalse(game.Spraying);
            }
            finally{InputSystem.RemoveDevice(mouse);InputSystem.settings.backgroundBehavior=previousBackground;}
            yield return null;
        }

        [UnityTest] public IEnumerator EmptyOutletOwnershipPreventsRecoveryWithoutASupplyLock()
        {
            var at=game.HydrantStation.Lever.position;var other=new EmberdeepRefillInteraction.Hand(true,new Pose(game.Arena.position+Vector3.up,Quaternion.identity),.04f);
            for(int i=0;i<6;i++)game.ProcessRefillHands(new EmberdeepRefillInteraction.Hand(true,new Pose(at,Quaternion.identity),.04f,at),other,.025f);
            game.ProcessRefillHands(new EmberdeepRefillInteraction.Hand(true,new Pose(at,Quaternion.identity),.01f,at),other,.025f);
            Assert.IsTrue(game.RefillInteraction.EmptyOutlet);Assert.IsTrue(game.RefillInteraction.OwnsControl);Assert.IsFalse(game.Run.Supply.Locked);
            Sample(230);Assert.Zero(game.Run.Water);Assert.Zero(game.Run.RecoveredWater);
            game.ProcessRefillHands(new EmberdeepRefillInteraction.Hand(true,new Pose(at,Quaternion.identity),.04f,at),other,.025f);
            Assert.IsFalse(game.RefillInteraction.OwnsControl);Sample(29);Assert.Zero(game.Run.Water);Sample(201);
            Assert.That(game.Run.Water,Is.EqualTo(5).Within(.001f));Assert.Zero(game.Run.SuppliedWater);yield return null;
        }

        [UnityTest] public IEnumerator ChargedCannonBlocksRecoveryEvenWhenOuterTriggerIsReleased()
        {
            for(int i=0;i<3;i++){Clear(game.Run);if(i<2)game.StartNext();}
            Assert.IsTrue(game.Run.BuyEquipment(FireEquipmentId.PressureCannon));
            game.SelectEquipment(3);game.StartNext();Assert.IsTrue(game.TakeEquipment(0));
            typeof(FireRun).GetProperty(nameof(FireRun.StationTier)).SetValue(game.Run,WaterStationTier.RotaryHydrant);
            var supply=game.Run.Supply;for(int i=0;i<4;i++)supply.SampleDock(WaterSourceKind.Hydrant,0,0,0,true,Dt);
            Assert.IsTrue(supply.SetValveOpen(true));for(int i=0;i<4;i++)supply.Tick(Dt,true);
            supply.Disconnect();game.Run.Tick(.01f,false);Assert.IsTrue(game.Run.PrimeCannon(.2f));
            float water=game.Run.Water;Assert.Less(water,game.Run.Capacity);Sample(230);
            Assert.AreEqual(water,game.Run.Water);Assert.Zero(game.Run.RecoveredWater);Assert.Greater(game.Run.Charge01,0);
            game.Run.CancelTrigger();game.Run.Tick(.01f,false);Sample(29);Assert.AreEqual(water,game.Run.Water);
            Sample(201);Assert.That(game.Run.Water,Is.EqualTo(water+5).Within(.001f));Assert.Zero(game.Run.SprayAmount);yield return null;
        }

        [UnityTest] public IEnumerator DesktopManualIntentBlocksRecoveryBeforeConnectionOrCompletedStroke()
        {
            Field("desktopDock",WaterSourceKind.Creek);Sample(230);Assert.Zero(game.Run.Water);Assert.Zero(game.Run.RecoveredWater);
            Field("desktopDock",WaterSourceKind.None);Field("desktopOutletTravel",.085f);
            Sample(230);Assert.Zero(game.Run.Water);Assert.Zero(game.Run.RecoveredWater);
            Field("desktopOutletTravel",0f);Sample(29);Assert.Zero(game.Run.Water);Sample(201);
            Assert.That(game.Run.Water,Is.EqualTo(5).Within(.001f));yield return null;
        }

        [UnityTest] public IEnumerator DockedManualStrokeAndRecoveryNeverCreditTheSameInterval()
        {
            var supply=game.Run.Supply;for(int i=0;i<4;i++)supply.SampleDock(WaterSourceKind.Hydrant,1,0,0,true,Dt);
            Assert.IsTrue(supply.Locked);supply.SampleLever(0,true,true);supply.SampleLever(.085f,true,true);supply.SampleLever(0,true,true);
            Assert.That(game.Run.SuppliedWater,Is.EqualTo(12).Within(.001f));float supplied=game.Run.SuppliedWater;
            Sample(230);Assert.That(game.Run.Water,Is.EqualTo(12).Within(.001f));Assert.Zero(game.Run.RecoveredWater);
            supply.Disconnect();game.Run.Tick(.01f,false);Sample(29);Assert.That(game.Run.Water,Is.EqualTo(12).Within(.001f));
            Sample(201);Assert.That(game.Run.Water,Is.EqualTo(17).Within(.001f));Assert.AreEqual(supplied,game.Run.SuppliedWater);
            Assert.That(game.Run.Water,Is.EqualTo(60+game.Run.SuppliedWater+game.Run.RecoveredWater-game.Run.ConsumedWater).Within(.001f));
            yield return null;
        }

        [UnityTest] public IEnumerator TerminalPhasesCannotAccumulateRecovery()
        {
            // Phase injection isolates the scene gate; normal win/loss progression
            // and checkpoint accounting remain covered by domain/gameplay tests.
            foreach(var phase in new[]{FirePhase.Won,FirePhase.Lost})
            {
                typeof(FireRun).GetProperty(nameof(FireRun.Phase)).SetValue(game.Run,phase);
                Sample(230);Assert.Zero(game.Run.Water);Assert.Zero(game.Run.RecoveredWater);Assert.IsFalse(game.Run.RecoveryActive);
            }
            yield return null;
        }
    }
}

using System.Collections;
using System.Linq;
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
        static void Clear(FireRun run)
        {
            for(int i=0;i<run.Trees.Length;i++)
                if(run.Trees[i].State==TreeState.Burning)run.ApplyWater(run.Trees[i].Position,100,.1f);
            Assert.AreEqual(FirePhase.Cleared,run.Phase);
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

using System.Collections;
using System.Linq;
using NUnit.Framework;
using PhysicalAssembly;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    // Real scene and shop callbacks, synthetic hand samples. Fire hits use only
    // spray paid for by Tick; this fixture never sets coins, tanks or station tier.
    public sealed class EmberdeepStationShopPlayTests
    {
        FireRescueGame game;
        const float Dt=.025f,OpenPinch=.04f,ClosedPinch=.01f;

        void Sample(Vector3 contact,float pinch=OpenPinch,Quaternion? rotation=null)
        {
            var hand=new EmberdeepRefillInteraction.Hand(true,new Pose(contact,rotation??Quaternion.identity),pinch,contact);
            var idle=new EmberdeepRefillInteraction.Hand(true,new Pose(game.Arena.position+Vector3.up,Quaternion.identity),OpenPinch);
            game.ProcessRefillHands(idle,hand,Dt);
        }
        void Open(Vector3 at,Quaternion? rotation=null){for(int i=0;i<6;i++)Sample(at,OpenPinch,rotation);}
        void Lever(Vector3 at,float strength,int frames=1)
        {
            var hand=new EmberdeepRefillInteraction.Hand(true,new Pose(at,Quaternion.identity),OpenPinch,at)
                {GripValid=true,GripStrength=strength,GripContact=at,HasGripContact=true};
            var idle=new EmberdeepRefillInteraction.Hand(true,new Pose(game.Arena.position+Vector3.up,Quaternion.identity),OpenPinch);
            for(int i=0;i<frames;i++)game.ProcessRefillHands(idle,hand,Dt);
        }
        void Detach()
        {
            var grip=game.HeldTool.Grip.position;Open(grip);Sample(grip,ClosedPinch);
            Assert.IsFalse(game.Run.Supply.Locked);game.Run.Tick(Dt,false);
        }
        void Dock()
        {
            if(!game.HoldingEquipment)Assert.IsTrue(game.TakeEquipment(1));
            EmberdeepRefillInteraction.Snap(game.HeldTool,game.HydrantStation.Socket);
            for(int i=0;i<12;i++)Sample(game.HeldTool.Grip.position);
            Assert.IsTrue(game.Run.Supply.Locked);
        }
        void FillCurrentStation()
        {
            Dock();var station=game.HydrantStation;
            if(game.Run.StationTier==WaterStationTier.LeverWell)
            {
                Lever(station.Lever.position,0,6);Lever(station.Lever.position,1,4);
                for(int i=0;i<10 && game.Run.Water<game.Run.Capacity-.001f;i++)
                {
                    Lever(station.Lever.position-station.transform.up*.09f,1);
                    Lever(station.Lever.position,1);
                }
                Lever(station.Lever.position,0,4);
            }
            else if(game.Run.StationTier==WaterStationTier.RotaryHydrant)
            {
                var neutral=station.Valve.rotation;Open(station.Valve.position,neutral);
                Sample(station.Valve.position,ClosedPinch,neutral);
                var turned=Quaternion.AngleAxis(50,station.Valve.forward)*neutral;
                Sample(station.Valve.position,ClosedPinch,turned);Sample(station.Valve.position,OpenPinch,turned);
                for(int i=0;i<100 && game.Run.Water<game.Run.Capacity-.001f;i++)Sample(station.Valve.position);
            }
            else
            {
                Open(station.Button.position);Sample(station.Button.position,ClosedPinch);Sample(station.Button.position);
                Assert.IsTrue(game.Run.Supply.ValveOpen);
                Open(station.Button.position);Sample(station.Button.position,ClosedPinch);Sample(station.Button.position);
                Assert.IsFalse(game.Run.Supply.ValveOpen,"The second fresh press stops automatic fill");
                float stopped=game.Run.Water;Open(station.Button.position);Assert.AreEqual(stopped,game.Run.Water);
                Sample(station.Button.position,ClosedPinch);Sample(station.Button.position);
                for(int i=0;i<100 && game.Run.Water<game.Run.Capacity-.001f;i++)Sample(station.Button.position);
            }
            Assert.AreEqual(game.Run.Capacity,game.Run.Water);Assert.IsFalse(game.Run.Supply.ValveOpen);
            Detach();game.Refresh();
        }
        void FightCurrentIncident()
        {
            for(int step=0;step<1400 && game.Run.Phase==FirePhase.Fighting;step++)
            {
                if(game.Run.Water<.01f)FillCurrentStation();
                var fire=game.Run.Trees.FirstOrDefault(t=>t.State==TreeState.Burning);
                if(fire==null)break;
                game.Run.Tick(.1f,true);
                if(game.Run.SprayAmount>0)
                    game.Run.ApplyWater(fire.Position,game.Run.SprayAmount,game.Run.ShotRadius,game.Run.ShotWetness);
            }
            game.Run.CancelTrigger();game.Refresh();
            Assert.AreEqual(FirePhase.Cleared,game.Run.Phase,"Earn shop currency by extinguishing paid-for fire samples");
            Assert.Greater(game.Run.ConsumedWater,0);
        }
        IEnumerator BuyNextStationThroughShop()
        {
            game.OpenShop();game.Refresh();Physics.SyncTransforms();
            var button=game.GetComponentsInChildren<WorkshopButton>(true).Single(b=>b.Command=="Upgrade water station");
            Assert.IsTrue(button.gameObject.activeInHierarchy);Assert.IsTrue(button.Interactable);
            int coins=game.Run.Coins,cost=game.Run.StationUpgradeCost;float water=game.Run.Water;
            var priorTier=game.Run.StationTier;var camera=game.Player.ViewCamera;
            var cameraPosition=camera.transform.position;var cameraRotation=camera.transform.rotation;
            try
            {
                // Editor-only UI framing: no scene/player placement is saved.
                var target=button.GetComponent<Collider>().bounds.center;
                camera.transform.SetPositionAndRotation(target-button.transform.forward*.7f,Quaternion.LookRotation(button.transform.forward));
                Physics.SyncTransforms();yield return new WaitForSecondsRealtime(.5f);
                var sample=new EmberdeepHandRay.Sample(true,new Ray(camera.transform.position,target-camera.transform.position),OpenPinch);
                for(int i=0;i<8;i++)game.ProcessHandMenuInput(default,sample,true,Dt);
                Assert.AreSame(button,game.MenuInput.HoveredButton);
                sample.Pinch=ClosedPinch;Assert.IsTrue(game.ProcessHandMenuInput(default,sample,true,Dt));
                Assert.AreEqual((WaterStationTier)((int)priorTier+1),game.Run.StationTier);
                Assert.AreEqual(game.Run.StationTier,game.HydrantStation.Tier);
                Assert.AreEqual(coins-cost,game.Run.Coins);Assert.AreEqual(water,game.Run.Water,"A station purchase supplies no water");
            }
            finally{camera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);}
            game.CloseShop();Assert.IsTrue(game.TakeEquipment(1));
        }

        [UnityTest] public IEnumerator EarnedCoinsUpgradeProductionStationFromLeverThroughRotaryToButton()
        {
            EmberdeepSession.NewExpedition();yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);
            yield return null;yield return null;
            game=Object.FindFirstObjectByType<FireRescueGame>();Assert.NotNull(game);game.enabled=false;
            game.SendMessage("OnApplicationFocus",true);game.SendMessage("OnApplicationPause",false);
            Assert.IsTrue(game.TakeEquipment(1));Assert.AreEqual(WaterStationTier.LeverWell,game.Run.StationTier);
            Assert.Zero(game.Run.Coins);Assert.IsFalse(game.Run.CanUpgradeStation);
            FightCurrentIncident();
            Assert.Greater(game.Run.SuppliedWater,0,"The starter tank cannot clear all three isolated fires without a lever refill");
            Assert.GreaterOrEqual(game.Run.Coins,100);yield return BuyNextStationThroughShop();
            Assert.IsFalse(game.HydrantStation.LeverVisual.gameObject.activeInHierarchy);Assert.IsTrue(game.HydrantStation.Valve.gameObject.activeInHierarchy);
            float beforeRotary=game.Run.SuppliedWater;
            do
            {
                game.StartNext();Assert.IsTrue(game.TakeEquipment(1));FightCurrentIncident();
                Assert.Less(game.Run.Stage,4,"Currency progression must permit the final station before the final incident");
            }while(game.Run.Coins<200);
            Assert.Greater(game.Run.SuppliedWater,beforeRotary,"Rotary supply actually participates in firefighting");
            yield return BuyNextStationThroughShop();
            Assert.IsTrue(game.HydrantStation.ButtonVisual.gameObject.activeInHierarchy);Assert.IsFalse(game.HydrantStation.Valve.gameObject.activeInHierarchy);
            Assert.AreEqual(0,game.Run.StationUpgradeCost);Assert.IsFalse(game.Run.CanUpgradeStation);
            game.StartNext();Assert.IsTrue(game.TakeEquipment(1));game.Run.Tick(Dt,false);
            for(int i=0;i<100 && game.Run.Water>0;i++)game.Run.Tick(.1f,true);
            game.Run.CancelTrigger();Assert.Zero(game.Run.Water);
            float beforeButton=game.Run.SuppliedWater;FillCurrentStation();
            Assert.That(game.Run.SuppliedWater-beforeButton,Is.EqualTo(game.Run.Capacity).Within(.001f));
            Assert.AreEqual(WaterStationTier.PushButton,game.Run.StationTier);
            yield return null;
        }
    }
}

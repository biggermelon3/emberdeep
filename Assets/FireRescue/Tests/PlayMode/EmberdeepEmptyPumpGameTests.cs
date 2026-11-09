using System.Collections;
using System.Linq;
using NUnit.Framework;
using PhysicalAssembly;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    public sealed class EmberdeepEmptyPumpGameTests
    {
        [UnityTest] public IEnumerator LeftEmptyGraspDrivesOutletWithoutWaterOrSameHandUI()=>Run(0);
        [UnityTest] public IEnumerator RightEmptyGraspDrivesOutletWithoutWaterOrSameHandUI()=>Run(1);
        IEnumerator Run(int hand)
        {
            EmberdeepSession.NewExpedition();yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);
            yield return null;yield return null;
            var game=Object.FindFirstObjectByType<FireRescueGame>();Assert.NotNull(game);game.enabled=false;
            game.SendMessage("OnApplicationFocus",true);game.SendMessage("OnApplicationPause",false);
            game.ReturnEquipment();var station=game.HydrantStation;var home=station.Lever.position;
            float water=game.Run.Water,supplied=game.Run.SuppliedWater;int coins=game.Run.Coins;
            void Sample(float strength,Vector3 contact,int count=1,bool valid=true)
            {
                var active=new EmberdeepRefillInteraction.Hand(valid,new Pose(contact,station.Lever.rotation),.04f,contact)
                    {GripValid=valid,GripStrength=strength,GripContact=contact,HasGripContact=true};
                var idle=new EmberdeepRefillInteraction.Hand(true,new Pose(home+Vector3.up,Quaternion.identity),.04f);
                for(int i=0;i<count;i++)game.ProcessRefillHands(hand==0?active:idle,hand==1?active:idle,.025f);
            }
            Sample(0,home,4);Sample(1,home,4);
            Assert.IsTrue(game.RefillInteraction.EmptyOutlet);Assert.IsFalse(game.Run.Supply.Locked);
            Assert.IsFalse(game.HoldingEquipment);
            // UI receives a valid aimed pinch sample, but the operating hand owns
            // the lever. It must not activate a second action while grasping.
            var button=game.GetComponentsInChildren<WorkshopButton>(true).First(b=>b.Interactable && b.gameObject.activeInHierarchy);
            var center=button.GetComponent<Collider>().bounds.center;
            var ui=new EmberdeepHandRay.Sample(true,new Ray(game.Player.ViewCamera.transform.position,center-game.Player.ViewCamera.transform.position),.04f);
            for(int i=0;i<6;i++)Assert.IsFalse(game.ProcessHandMenuInput(hand==0?ui:default,hand==1?ui:default,true,.025f));
            ui.Pinch=.01f;Assert.IsFalse(game.ProcessHandMenuInput(hand==0?ui:default,hand==1?ui:default,true,.025f));
            Assert.IsTrue(game.RefillInteraction.EmptyOutlet);Assert.IsFalse(game.MenuInput.RayVisible(hand));
            Sample(1,home-station.transform.up*.09f);Sample(1,home);
            Assert.IsTrue(station.OutletActive,"The production event wiring starts visible empty-outlet water");
            Assert.AreEqual(water,game.Run.Water);Assert.AreEqual(supplied,game.Run.SuppliedWater);Assert.AreEqual(coins,game.Run.Coins);
            Sample(1,home,1,false);Assert.IsFalse(station.OutletActive);Assert.IsFalse(game.RefillInteraction.OwnsControl);
            // A tracked fist returning after loss is not a new grasp.
            Sample(1,home,12);Assert.IsFalse(game.RefillInteraction.OwnsControl);
            Assert.IsTrue(game.TakeEquipment(1-hand),"The other hand can take the owned tool after empty-pump tracking loss");
            Assert.AreEqual(hand==1,game.LeftHanded);
            // Taking the tool with the left hand mirrors/repositions the real
            // station. Follow its current physical lever, not the old world point.
            home=station.Lever.position;
            game.HeldTool.transform.position=home+Vector3.right*2;
            Assert.Greater(Vector3.Distance(game.HeldTool.RefillPort.position,station.Socket.position),EmberdeepWaterSupply.CaptureMetres);
            Sample(0,home,4);Sample(1,home,4);
            Assert.IsTrue(game.RefillInteraction.EmptyOutlet,"The free hand grasps the lever at its current mirrored station position");
            Assert.AreEqual(hand,game.RefillInteraction.OperatingHand);Assert.IsFalse(game.Run.Supply.Locked);
            Sample(1,home,1,false);Assert.IsTrue(game.HoldingEquipment,"Loss of the free pump hand must not holster the other hand's tool");
            yield return null;
        }
    }
}

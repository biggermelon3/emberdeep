using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    public sealed class EmberdeepMagicLessonPlayTests
    {
        [UnityTest] public IEnumerator NewGameUsesRealPalmWaterThenTeachesUpgradeAndKeepsToolsRetiredInFourthSector()
        {
            EmberdeepSession.NewExpedition();yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);yield return null;yield return null;
            var game=Object.FindFirstObjectByType<FireRescueGame>();game.SendMessage("OnApplicationFocus",true);game.SendMessage("OnApplicationPause",false);EmberdeepTestFlow.StartPrepared(game.Run);game.Refresh();yield return null;
            Assert.True(game.Run.IsPalmLesson);Assert.AreEqual(1,game.Run.Trees.Length);Assert.False(game.TakeEquipment());
            Assert.True(game.HydrantStation.gameObject.activeSelf);Assert.False(game.CreekStation.gameObject.activeSelf);Assert.False(game.HoldingEquipment);
            Assert.NotNull(game.GetComponentsInChildren<EmberdeepInteractionDemo>().Single(d=>d.Current==EmberdeepInteractionDemo.Action.Goddess));
            Assert.AreEqual(1,game.GetComponentsInChildren<EmberdeepManaCrystal>(true).Count(c=>c.name=="Left hand mana crystal"));
            var background=InputSystem.settings.backgroundBehavior;var editor=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var mouse=InputSystem.AddDevice<Mouse>();var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                var point=game.Player.ViewCamera.WorldToScreenPoint(game.Arena.TransformPoint(game.Run.Trees[0].Position));
                float deadline=Time.realtimeSinceStartup+6;int frame=Time.frameCount;
                while(game.Run.Phase==FirePhase.Fighting && Time.realtimeSinceStartup<deadline)
                {
                    InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(point.x,point.y)});
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));yield return null;
                }
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
                Assert.Greater(Time.frameCount,frame);Assert.AreEqual(FirePhase.Cleared,game.Run.Phase,"Actual water packets must hit and save the tutorial tree");
                Assert.Greater(game.Run.MagicConsumed,0);Assert.LessOrEqual(game.Run.MagicConsumed,30);Assert.AreEqual(60,game.Run.Water);
                game.Run.EvacuateSafeAnimals();game.OpenShop();game.ShowShopPage(EmberdeepShopPage.Magic);game.RefreshShopPresentation();
                Assert.True(game.ShopCanBuy);Assert.AreEqual("magic:1",game.ShopItemId);Assert.True(game.BuyShopSelection());
                Assert.AreEqual(1,game.Run.MagicLevel);game.CloseShop();game.StartNext();yield return null;
                Assert.AreEqual(1,game.Run.Stage);Assert.True(game.Run.IsPalmLesson);Assert.True(game.HydrantStation.gameObject.activeSelf);
                Assert.True(game.Run.PalmTutorialComplete);Assert.False(game.TakeEquipment(1));
                for(int level=1;level<3;level++)
                {Assert.AreEqual(level,game.Run.Stage);Assert.True(game.Run.IsPalmLesson);Assert.False(game.CreekStation.gameObject.activeSelf);EmberdeepTestFlow.StartPrepared(game.Run);game.Run.ApplyWater(Vector3.zero,10000,100);game.StartNext();yield return null;}
                Assert.AreEqual(3,game.Run.Stage);Assert.AreEqual(3,game.Run.Trees.Length);Assert.False(game.Run.IsPalmLesson);
                // Spell mode keeps the hand-held tools retired: no rack, no pickup; the well and its basin stay.
                var socket=game.HydrantStation.transform.position;
                Assert.False(game.TakeEquipment(0));Assert.False(game.TakeEquipment(1));Assert.False(game.HoldingEquipment);
                Assert.False(game.transform.Find("Equipment rack").gameObject.activeSelf);
                Assert.AreEqual(socket,game.HydrantStation.transform.position);Assert.False(game.CreekStation.gameObject.activeSelf);
                Assert.True(game.Run.StationOpen);Assert.True(game.Basin.gameObject.activeInHierarchy);
                Assert.AreEqual(50,game.Run.MagicWater,.1f);
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editor;
            }
        }
    }
}

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
        [UnityTest] public IEnumerator NewGameUsesRealPalmWaterThenKeepsToolsRetiredAfterThePalmLesson()
        {
            // LoopSpec §11b: the palm demo is now the first-time ghost hint, shown until the player has sprayed twice
            // (PlayerPrefs). Start this new game as a first-time player and put the counts back afterwards.
            var hintKey=EmberdeepFirstHints.Key(EmberdeepFirstHints.Move.Water);int hintCount=PlayerPrefs.GetInt(hintKey,-1);PlayerPrefs.DeleteKey(hintKey);
            EmberdeepSession.NewExpedition();yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);yield return null;yield return null;
            var game=Object.FindFirstObjectByType<FireRescueGame>();game.SendMessage("OnApplicationFocus",true);game.SendMessage("OnApplicationPause",false);EmberdeepTestFlow.StartPrepared(game.Run);game.Refresh();yield return null;
            // Intent changed (LoopSpec §8 stage cut, T): one palm lesson, the 5-tree grove; the single tree moved to the camp.
            Assert.True(game.Run.IsPalmLesson);Assert.AreEqual(FireRun.PalmLessonTrees,game.Run.Trees.Length);Assert.False(game.TakeEquipment());
            Assert.False(game.HydrantStation.gameObject.activeSelf);Assert.True(game.Pump.gameObject.activeSelf);Assert.False(game.CreekStation.gameObject.activeSelf);Assert.False(game.HoldingEquipment);
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
                while(game.Run.Trees[0].State==TreeState.Burning && Time.realtimeSinceStartup<deadline)
                {
                    InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(point.x,point.y)});
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));yield return null;
                }
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
                Assert.Greater(Time.frameCount,frame);Assert.AreEqual(TreeState.Saved,game.Run.Trees[0].State,"Actual water packets must hit and save a lesson tree");
                Assert.Greater(game.Run.MagicConsumed,0);Assert.LessOrEqual(game.Run.MagicConsumed,EmberdeepWaterTuning.GemCapacity);Assert.AreEqual(60,game.Run.Water);
                game.Run.ApplyWater(Vector3.zero,10000,100);Assert.AreEqual(FirePhase.Cleared,game.Run.Phase);
                game.Run.EvacuateSafeAnimals();game.OpenShop();game.ShowShopPage(EmberdeepShopPage.Magic);game.RefreshShopPresentation();
                Assert.True(game.ShopCanBuy);Assert.AreEqual("magic:1",game.ShopItemId);Assert.True(game.BuyShopSelection());
                Assert.AreEqual(1,game.Run.MagicLevel);game.CloseShop();game.StartNext();yield return null;
                Assert.AreEqual(1,game.Run.Stage);Assert.False(game.Run.IsPalmLesson,"Stage 2 is the first incident");Assert.True(game.Pump.gameObject.activeSelf);
                Assert.True(game.Run.PalmTutorialComplete);Assert.False(game.TakeEquipment(1));
                Assert.AreEqual(0,game.Run.IncidentIndex);Assert.AreEqual(EmberdeepLevels.For(1).Trees.Length,game.Run.Trees.Length);Assert.False(game.CreekStation.gameObject.activeSelf);
                // Spell mode keeps the hand-held tools retired: no rack, no pickup. Intent changed
                // (LoopSpec §3): the well and basin are gone; the hammer pump stays put beside the player.
                var pump=game.Pump.transform.position;
                Assert.False(game.TakeEquipment(0));Assert.False(game.TakeEquipment(1));Assert.False(game.HoldingEquipment);
                Assert.False(game.transform.Find("Equipment rack").gameObject.activeSelf);
                Assert.AreEqual(pump,game.Pump.transform.position);Assert.False(game.CreekStation.gameObject.activeSelf);
                Assert.False(game.HydrantStation.gameObject.activeSelf);Assert.True(game.Pump.gameObject.activeInHierarchy);
                Assert.AreEqual(EmberdeepWaterTuning.GemCapacity+EmberdeepWaterTuning.GemCapacityPerLevel,game.Run.MagicWater,.1f,"Each level starts with a full gem");
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);
                if(hintCount<0)PlayerPrefs.DeleteKey(hintKey);else PlayerPrefs.SetInt(hintKey,hintCount);
                InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editor;
            }
        }
    }
}

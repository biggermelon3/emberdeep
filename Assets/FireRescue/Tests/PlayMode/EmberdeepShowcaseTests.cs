using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PhysicalAssembly;
using PhysicalAssembly.MetaXR;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace FireRescue.Tests
{
    public sealed class EmberdeepShowcaseTests
    {
        [UnityTest] public IEnumerator IsolatedCampCampaignAndResumePreserveProgress()
        {
            // The Unity test runner injects its own temporary bootstrap scene.
            var scenes=Enumerable.Range(0,SceneManager.sceneCountInBuildSettings).Select(SceneUtility.GetScenePathByBuildIndex)
                .Where(p=>!Path.GetFileName(p).StartsWith("InitTestScene")).ToArray();
            CollectionAssert.AreEqual(new[]{"Assets/FireRescue/Scenes/EmberdeepCamp.unity","Assets/FireRescue/Scenes/EmberdeepFireground.unity"},scenes);
            foreach(var other in new[]{"MainMenu","Workshop","CloudGardenDemo","ShadowWorkshop","DownhillSandbox","NarrowGap","FireRescue"})
                Assert.IsFalse(Application.CanStreamedLevelBeLoaded(other),other);
            EmberdeepSession.NewExpedition();
            yield return SceneManager.LoadSceneAsync(EmberdeepSession.CampScene);yield return null;yield return null;
            var camp=Object.FindFirstObjectByType<EmberdeepCamp>();Assert.NotNull(camp);OnePlayer();
            var background=InputSystem.settings.backgroundBehavior;var editor=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var mouse=InputSystem.AddDevice<Mouse>();
            try
            {
                camp.SendMessage("OnApplicationFocus",true);
                yield return WaitPlaced(camp);
                Capture(camp.Player.ViewCamera,"Logs/Emberdeep-Camp.png");
                var button=camp.GetComponentsInChildren<WorkshopButton>().Single(b=>b.Command=="New Game");
                Assert.IsNull(button.GetComponent<MetaMenuButton>(),"Camp uses its gaze and pinch input path");Physics.SyncTransforms();
                var screen=camp.Player.ViewCamera.WorldToScreenPoint(button.transform.position);
                float hoverUntil=Time.realtimeSinceStartup+.2f;int hoverFrames=0;
                while(Time.realtimeSinceStartup<hoverUntil || hoverFrames<8)
                {
                    InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(screen.x,screen.y)});
                    hoverFrames++;yield return null;
                }
                Assert.AreSame(button,camp.HoveredButton);
                InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(screen.x,screen.y),buttons=1});yield return null;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(screen.x,screen.y)});
                yield return WaitScene(EmberdeepSession.FireScene);
                var game=Object.FindFirstObjectByType<FireRescueGame>();Assert.NotNull(game);Assert.IsTrue(game.StandaloneShowcase);OnePlayer();
                Assert.IsFalse(game.GetComponentsInChildren<WorkshopButton>().Any(b=>b.Command=="Main menu"));
                // LoopSpec §11b: camp is on the pause panel and the results (the dashboard is gone in the spell flow).
                Assert.IsTrue(game.GetComponentsInChildren<WorkshopButton>(true).Any(b=>b.Command=="Pause camp"));
                game.SendMessage("OnApplicationFocus",true);yield return new WaitForSeconds(.3f);
                Capture(game.Player.ViewCamera,"Logs/Emberdeep-FirstResponse.png");game.enabled=false;
                for(int lesson=0;lesson<FireRun.PalmLessons;lesson++){Assert.True(game.Run.IsPalmLesson);Clear(game.Run);if(game.Run.NeedsMagicUpgrade)Assert.True(game.Run.BuyMagicUpgrade());game.StartNext();}
                // Intent changed (LoopSpec §3, §11): spells are not sold and the gem is the one water
                // store, so the resume regression carries a partly drained gem instead of a spell tank.
                Clear(game.Run);Assert.IsFalse(game.Run.BuyEquipment(FireEquipmentId.PulsePistol),"Spells are cut from the production shop");
                game.StartNext();EmberdeepTestFlow.StartPrepared(game.Run);
                Assert.AreEqual(game.Run.MagicCapacity,game.Run.MagicWater,"Each level loads with a full gem");
                for(int i=0;i<10;i++)game.Run.TickMagic(.1f,true,true);game.Run.Tick(.02f,false);
                var run=game.Run;float water=run.MagicWater,fuel=run.Trees[0].Fuel;int coins=run.Coins;
                Assert.Greater(water,0);Assert.Less(water,run.MagicCapacity,"Resume regression uses a genuinely drained gem");
                game.ReturnToMenu();yield return WaitScene(EmberdeepSession.CampScene);OnePlayer();
                Assert.AreSame(run,EmberdeepSession.Run);Assert.AreEqual(water,run.MagicWater);Assert.AreEqual(fuel,run.Trees[0].Fuel);
                camp=Object.FindFirstObjectByType<EmberdeepCamp>();camp.SendMessage("OnApplicationFocus",true);
                yield return WaitPlaced(camp);camp.EnterFireground();yield return WaitScene(EmberdeepSession.FireScene);
                game=Object.FindFirstObjectByType<FireRescueGame>();game.enabled=false;OnePlayer();
                Assert.AreSame(run,game.Run);Assert.AreEqual(2,run.Stage);Assert.AreEqual(coins,run.Coins);Assert.AreEqual(FireEquipmentId.Extinguisher,run.Equipped);
                // Scene transitions preserve partial water exactly; waiting cannot refill it.
                Assert.AreEqual(water,run.MagicWater);Assert.LessOrEqual(run.Trees[0].Fuel,fuel);Assert.Greater(run.Trees[0].Fuel,fuel-2);
                for(int stage=2;stage<6;stage++) // stage cut (T): 6 stages, one palm lesson
                {
                    Assert.AreEqual(stage,run.Stage);Assert.AreEqual(EmberdeepLevels.For(stage).Trees.Length,run.Trees.Length); // the authored level (EmberdeepLevels, LV)
                    if(stage==5){game.enabled=true;game.SendMessage("OnApplicationFocus",true);yield return new WaitForSeconds(.3f);Capture(game.Player.ViewCamera,"Logs/Emberdeep-Forest.png");game.enabled=false;}
                    Clear(run);game.Refresh();Assert.AreEqual(stage==5?FirePhase.Won:FirePhase.Cleared,run.Phase);
                    if(stage<5){game.StartNext();yield return null;}
                }
                game.ReturnToMenu();yield return WaitScene(EmberdeepSession.CampScene);OnePlayer();
                camp=Object.FindFirstObjectByType<EmberdeepCamp>();
                camp.SendMessage("OnApplicationFocus",true);yield return WaitPlaced(camp);
                Assert.IsTrue(camp.GetComponentsInChildren<WorkshopButton>().Any(b=>b.Command=="Continue"));
                camp.GetComponentsInChildren<WorkshopButton>().Single(b=>b.Command=="New Game").Press();
                Assert.IsTrue(camp.Transitioning,"New Game enters the fire scene immediately");
                Assert.AreNotSame(run,EmberdeepSession.Run);
                yield return WaitScene(EmberdeepSession.FireScene);
                game=Object.FindFirstObjectByType<FireRescueGame>();Assert.NotNull(game);OnePlayer();
                Assert.AreSame(EmberdeepSession.Run,game.Run);Assert.AreEqual(FirePhase.Preparing,game.Run.Phase);
                Assert.Zero(game.Run.Stage);Assert.Zero(game.Run.Coins);Assert.Zero(game.Run.Rescued);
                Assert.AreEqual(FireEquipmentId.Extinguisher,game.Run.Equipped);
            }
            finally{InputSystem.RemoveDevice(mouse);InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editor;}
        }
        static void Clear(FireRun run)
        {EmberdeepTestFlow.StartPrepared(run);for(int i=0;i<run.Trees.Length;i++)if(run.Trees[i].State==TreeState.Burning)run.ApplyWater(run.Trees[i].Position,100,.1f);run.EvacuateSafeAnimals();}
        static void OnePlayer()=>Assert.AreEqual(1,Object.FindObjectsByType<MetaPlayerRig>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length);
        static IEnumerator WaitScene(string name)
        {float deadline=Time.realtimeSinceStartup+15;while(SceneManager.GetActiveScene().name!=name && Time.realtimeSinceStartup<deadline)yield return null;Assert.AreEqual(name,SceneManager.GetActiveScene().name);yield return null;yield return null;}
        static IEnumerator WaitPlaced(EmberdeepCamp camp)
        {float deadline=Time.realtimeSinceStartup+10;while(!camp.IsPlaced && Time.realtimeSinceStartup<deadline)yield return null;Assert.IsTrue(camp.IsPlaced,"Menu placement did not stabilize");yield return null;}
        static void Capture(Camera camera,string path)
        {
            var target=new RenderTexture(1920,1080,24);var prior=camera.targetTexture;var active=RenderTexture.active;var pixels=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1920,1080),0,0);pixels.Apply();Directory.CreateDirectory("Logs");File.WriteAllBytes(path,pixels.EncodeToPNG());}
            finally{camera.targetTexture=prior;RenderTexture.active=active;Object.Destroy(pixels);Object.Destroy(target);}
        }
    }
}

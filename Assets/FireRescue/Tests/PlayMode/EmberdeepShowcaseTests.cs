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
                Assert.IsTrue(game.GetComponentsInChildren<WorkshopButton>().Any(b=>b.Command=="Return to camp"));
                game.SendMessage("OnApplicationFocus",true);yield return new WaitForSeconds(.3f);
                Capture(game.Player.ViewCamera,"Logs/Emberdeep-FirstResponse.png");game.enabled=false;
                Clear(game.Run);Assert.IsTrue(game.Run.BuyEquipment(FireEquipmentId.PulsePistol));
                game.Run.Equip(FireEquipmentId.PulsePistol);game.StartNext();
                Assert.Zero(game.Run.Water,"Purchased pistol starts empty");
                var supply=game.Run.Supply;
                for(int i=0;i<6;i++)supply.SampleDock(WaterSourceKind.Hydrant,1,0,0,true,.05f);
                Assert.AreEqual(WaterStationTier.LeverWell,game.Run.StationTier);
                for(int i=0;i<30 && game.Run.Water<game.Run.Capacity;i++){supply.SampleLever(0,true,true);supply.SampleLever(.085f,true,true);supply.SampleLever(0,true,true);}
                Assert.AreEqual(game.Run.Capacity,game.Run.Water);supply.Disconnect();game.Run.Tick(.02f,false);
                for(int shot=0;shot<24;shot++)game.Run.Tick(.1f,true);
                var run=game.Run;float water=run.Water,fuel=run.Trees[0].Fuel;int coins=run.Coins;
                Assert.Greater(water,0);Assert.Less(water,run.Capacity,"Resume regression uses a genuinely depleted purchased tank");
                game.ReturnToMenu();yield return WaitScene(EmberdeepSession.CampScene);OnePlayer();
                Assert.AreSame(run,EmberdeepSession.Run);Assert.AreEqual(water,run.Water);Assert.AreEqual(fuel,run.Trees[0].Fuel);
                camp=Object.FindFirstObjectByType<EmberdeepCamp>();camp.SendMessage("OnApplicationFocus",true);
                yield return WaitPlaced(camp);camp.EnterFireground();yield return WaitScene(EmberdeepSession.FireScene);
                game=Object.FindFirstObjectByType<FireRescueGame>();game.enabled=false;OnePlayer();
                Assert.AreSame(run,game.Run);Assert.AreEqual(1,run.Stage);Assert.AreEqual(coins,run.Coins);Assert.AreEqual(FireEquipmentId.PulsePistol,run.Equipped);
                // Scene transitions preserve partial water exactly; waiting cannot refill it.
                Assert.AreEqual(water,run.Water);Assert.LessOrEqual(run.Trees[0].Fuel,fuel);Assert.Greater(run.Trees[0].Fuel,fuel-2);
                for(int stage=1;stage<5;stage++)
                {
                    Assert.AreEqual(stage,run.Stage);Assert.AreEqual(new[]{3,9,18,30,42}[stage],run.Trees.Length);
                    if(stage==4){game.enabled=true;game.SendMessage("OnApplicationFocus",true);yield return new WaitForSeconds(.3f);Capture(game.Player.ViewCamera,"Logs/Emberdeep-Forest.png");game.enabled=false;}
                    Clear(run);game.Refresh();Assert.AreEqual(stage==4?FirePhase.Won:FirePhase.Cleared,run.Phase);
                    if(stage<4){game.StartNext();yield return null;}
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
                Assert.AreSame(EmberdeepSession.Run,game.Run);Assert.AreEqual(FirePhase.Fighting,game.Run.Phase);
                Assert.Zero(game.Run.Stage);Assert.Zero(game.Run.Coins);Assert.Zero(game.Run.Rescued);
                Assert.AreEqual(FireEquipmentId.Extinguisher,game.Run.Equipped);
            }
            finally{InputSystem.RemoveDevice(mouse);InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editor;}
        }
        static void Clear(FireRun run)
        {for(int i=0;i<run.Trees.Length;i++)if(run.Trees[i].State==TreeState.Burning)run.ApplyWater(run.Trees[i].Position,100,.1f);}
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

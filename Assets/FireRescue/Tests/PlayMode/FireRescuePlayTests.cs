using System.Collections;
using System.IO;
using System.Linq;
using CloudGarden.Integration;
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
    public sealed class FireRescuePlayTests
    {
        [UnityTest] public IEnumerator MouseAimsSpraysAndRefillsWithoutMenuOcclusion()
        {
            yield return SceneManager.LoadSceneAsync("FireRescue");yield return null;yield return null;
            var game=Object.FindFirstObjectByType<FireRescueGame>();
            var background=InputSystem.settings.backgroundBehavior;
            var editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var mouse=InputSystem.AddDevice<Mouse>();
            game.SendMessage("OnApplicationFocus",true);
            try
            {
                var p=game.Player.ViewCamera.WorldToScreenPoint(game.Arena.TransformPoint(game.Run.Trees[1].Position));
                InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(p.x,p.y)});yield return null;yield return null;
                float until=Time.realtimeSinceStartup+8;
                while(game.Run.Trees[1].State==TreeState.Burning && Time.realtimeSinceStartup<until)
                {
                    InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(p.x,p.y),buttons=1});yield return null;
                }
                Capture(game.Player.ViewCamera,"Logs/FireRescue-InputDiagnostic.png");
                Assert.AreEqual(TreeState.Saved,game.Run.Trees[1].State,$"water={game.Run.Water} heat={game.Run.Trees[1].Heat} spraying={game.Spraying} point={p} mouse={Mouse.current.position.ReadValue()} screen={Screen.width}x{Screen.height} focus={Application.isFocused}");Assert.GreaterOrEqual(game.Run.Coins,25);
                float water=game.Run.Water;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(p.x,p.y)});yield return new WaitForSeconds(.5f);
                Assert.Greater(game.Run.Water,water);Assert.IsFalse(game.Spraying);
                p=game.Player.ViewCamera.WorldToScreenPoint(game.Arena.TransformPoint(game.Run.Trees[0].Position));
                InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(p.x,p.y),buttons=1});yield return new WaitForSeconds(.4f);
                Assert.IsTrue(game.Spraying);Assert.Greater(game.Effects.VisibleWater,0);Assert.Greater(game.Effects.VisibleSteam,0);Capture(game.Player.ViewCamera,"Logs/FireRescue-Spraying.png");
                game.SendMessage("OnApplicationFocus",false);float fuel=game.Run.Trees[0].Fuel;
                yield return new WaitForSeconds(.3f);Assert.AreEqual(fuel,game.Run.Trees[0].Fuel);Assert.IsFalse(game.Spraying);
            }
            finally{InputSystem.RemoveDevice(mouse);InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;}
            yield return SceneManager.LoadSceneAsync("MainMenu");yield return null;
        }
        static IEnumerator Wait(GardenNavigation nav)
        {
            float until=Time.realtimeSinceStartup+25;while(nav.IsTransitioning && Time.realtimeSinceStartup<until)yield return null;
            Assert.IsFalse(nav.IsTransitioning);yield return null;yield return null;
        }
        [UnityTest] public IEnumerator MenuEntryButtonsCampaignAndReturnUseOnePlayer()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");yield return null;yield return null;
            var nav=GardenNavigation.Current;var player=nav.Player;nav.EnterMode(GameMode.FireRescue);yield return Wait(nav);
            var game=Object.FindFirstObjectByType<FireRescueGame>();Assert.NotNull(game);Assert.IsTrue(nav.InFire);Assert.IsFalse(nav.InShadow);Assert.AreSame(player,game.Player);
            Assert.That(game.Arena.lossyScale.x,Is.EqualTo(.08f).Within(.001f));
            Assert.That(game.Arena.TransformPoint(new Vector3(0,0,6)).y,Is.EqualTo(player.ViewCamera.transform.position.y-.65f).Within(.001f));
            Assert.AreEqual(3,game.Run.Burning);Assert.AreEqual(1,Object.FindObjectsByType<MetaPlayerRig>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length);
            yield return new WaitForSeconds(.2f);Capture(player.ViewCamera,"Logs/FireRescue-FirstResponse.png");
            game.enabled=false;
            for(int stage=0;stage<5;stage++)
            {
                if(stage==4){game.enabled=true;game.SendMessage("OnApplicationFocus",true);yield return new WaitForSeconds(2.1f);Capture(player.ViewCamera,"Logs/FireRescue-Forest.png");game.enabled=false;}
                for(int pass=0;pass<6 && game.Run.Phase==FirePhase.Fighting;pass++)
                for(int i=0;i<game.Run.Trees.Length;i++)
                {
                    var target=game.Run.Trees[i];
                    for(int step=0;step<350 && target.State==TreeState.Burning;step++)
                    {
                        if(game.Run.Water<.1f)for(int refill=0;refill<70;refill++)game.Run.Tick(.1f,false);
                        game.Run.Tick(.1f,true);game.Run.ApplyWater(target.Position,game.Run.SprayAmount);
                    }
                    Assert.AreNotEqual(FirePhase.Lost,game.Run.Phase);
                }
                Assert.AreEqual(stage==4?FirePhase.Won:FirePhase.Cleared,game.Run.Phase);game.Refresh();
                while(game.Run.CanUpgrade)Assert.IsTrue(game.BuyUpgrade());
                if(stage<4){game.StartNext();yield return null;}
            }
            // Native button surfaces are registered and desktop rays can reach every control.
            Physics.SyncTransforms();
            foreach(var button in game.GetComponentsInChildren<WorkshopButton>())
            {
                Assert.NotNull(button.GetComponent<MetaMenuButton>());
                var from=player.ViewCamera.transform.position;var to=button.transform.position;
                Assert.IsTrue(Physics.Raycast(new Ray(from,to-from),out var hit,30,~0,QueryTriggerInteraction.Collide));
                Assert.AreSame(button,hit.collider.GetComponent<WorkshopButton>(),button.name);
            }
            game.Refresh();yield return null;Capture(player.ViewCamera,"Logs/FireRescue-Victory.png");
            nav.ReturnToMainMenu();yield return Wait(nav);Assert.IsTrue(nav.InMainMenu);Assert.AreSame(player,nav.Player);
            Assert.IsNull(Object.FindFirstObjectByType<FireRescueGame>());Assert.AreEqual(1,Object.FindObjectsByType<MetaPlayerRig>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length);
        }
        [UnityTest] public IEnumerator ShopPurchasePickUpAndFourEquipmentViewsWorkInScene()
        {
            yield return SceneManager.LoadSceneAsync("FireRescue");yield return null;yield return null;
            var game=Object.FindFirstObjectByType<FireRescueGame>();game.SendMessage("OnApplicationFocus",true);
            game.OpenShop();Assert.IsFalse(game.ShopOpen,"No shopping during a fire");
            for(int round=0;round<3;round++)
            {
                for(int i=0;i<game.Run.Trees.Length;i++)if(game.Run.Trees[i].State==TreeState.Burning)game.Run.ApplyWater(game.Run.Trees[i].Position,100,.1f);
                game.OpenShop();Assert.IsTrue(game.ShopOpen);Assert.IsFalse(game.HoldingEquipment);
                game.SelectEquipment(round+1);Assert.IsTrue(game.BuySelectedEquipment());Assert.IsFalse(game.BuySelectedEquipment());
                yield return null;game.Refresh();
                Capture(game.Player.ViewCamera,"Logs/FireGear-Shop-"+(round+1)+".png");
                game.CloseShop();Assert.IsTrue(game.TakeEquipment(round%2));Assert.IsTrue(game.HoldingEquipment);Assert.AreEqual((FireEquipmentId)(round+1),game.Run.Equipped);
                yield return null;
                var views=game.GetComponentsInChildren<FireEquipmentView>(true);Assert.AreEqual(2,views.Length);
                Assert.Greater(views[0].GetComponentsInChildren<Renderer>().Length,15);
                Assert.NotNull(views[0].Muzzle);Assert.NotNull(views[0].Grip);
                game.ReturnEquipment();Assert.IsFalse(game.HoldingEquipment);Assert.IsTrue(game.TakeEquipment());
                game.StartNext();yield return null;
            }
            game.OpenShop();Assert.IsFalse(game.ShopOpen);Assert.AreEqual(FireEquipmentId.PressureCannon,game.Run.Equipped);
            yield return SceneManager.LoadSceneAsync("MainMenu");yield return null;
        }
        [UnityTest] public IEnumerator CannonChargesWithoutDamageThenReleasesARealMouseShot()
        {
            yield return SceneManager.LoadSceneAsync("FireRescue");yield return null;yield return null;
            var game=Object.FindFirstObjectByType<FireRescueGame>();
            for(int round=0;round<3;round++)
            {
                for(int i=0;i<game.Run.Trees.Length;i++)if(game.Run.Trees[i].State==TreeState.Burning)game.Run.ApplyWater(game.Run.Trees[i].Position,100,.1f);
                if(round<2)game.StartNext();
            }
            game.OpenShop();game.SelectEquipment(3);Assert.IsTrue(game.BuySelectedEquipment());game.CloseShop();game.TakeEquipment();game.StartNext();yield return null;
            var background=InputSystem.settings.backgroundBehavior;var editor=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var mouse=InputSystem.AddDevice<Mouse>();game.SendMessage("OnApplicationFocus",true);
            try
            {
                var target=game.Run.Trees[0];var screen=game.Player.ViewCamera.WorldToScreenPoint(game.Arena.TransformPoint(target.Position));var point=new Vector2(screen.x,screen.y);
                InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;yield return null;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=point,buttons=1});yield return new WaitForSeconds(1.4f);
                Assert.Greater(game.Run.Charge01,.95f);Assert.AreEqual(100,target.Heat);Assert.Zero(game.Effects.VisibleWater);
                Capture(game.Player.ViewCamera,"Logs/FireGear-Cannon-Charging.png");
                InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;
                float deadline=Time.realtimeSinceStartup+1;
                while(game.Effects.VisibleWater==0 && Time.realtimeSinceStartup<deadline)yield return null;
                Assert.Greater(game.Effects.VisibleWater,0,"The cannon must have a visible in-flight water bomb");
                Capture(game.Player.ViewCamera,"Logs/FireGear-Cannon-Shot.png");
                yield return new WaitForSeconds(.65f);Assert.Less(target.Heat,100);Assert.GreaterOrEqual(game.Run.Rescued,2,"Charged splash should cover multiple trees");
            }
            finally{InputSystem.RemoveDevice(mouse);InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editor;}
            yield return SceneManager.LoadSceneAsync("MainMenu");yield return null;
        }
        [UnityTest] public IEnumerator RealMouseFillsTowerAndRainExtinguishesCluster()
        {
            yield return SceneManager.LoadSceneAsync("FireRescue");yield return null;yield return null;
            var game=Object.FindFirstObjectByType<FireRescueGame>();
            for(int i=0;i<game.Run.Trees.Length;i++)game.Run.ApplyWater(game.Run.Trees[i].Position,100,.1f);
            game.Run.BuyEquipment(FireEquipmentId.PulsePistol);game.SelectEquipment(1);game.TakeEquipment();game.StartNext();yield return null;
            var background=InputSystem.settings.backgroundBehavior;var editor=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var mouse=InputSystem.AddDevice<Mouse>();game.SendMessage("OnApplicationFocus",true);
            try
            {
                var tower=game.Run.Props[0];var screen=game.Player.ViewCamera.WorldToScreenPoint(game.Arena.TransformPoint(tower.Position));var point=new Vector2(screen.x,screen.y);
                InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;yield return null;
                int saved=game.Run.Rescued;float deadline=Time.realtimeSinceStartup+7;
                while(!tower.Used && Time.realtimeSinceStartup<deadline){InputSystem.QueueStateEvent(mouse,new MouseState{position=point,buttons=1});yield return null;}
                Assert.IsTrue(tower.Used,$"A real projectile must hit the tower; charge={tower.Charge01} phase={game.Run.Phase}");
                InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return new WaitForSeconds(.3f);
                Capture(game.Player.ViewCamera,"Logs/FirePlay-RainTower.png");
                yield return new WaitForSeconds(3.8f);Assert.GreaterOrEqual(game.Run.Rescued-saved,2);
                // Move to the cabin incident for an actual rendered layout, not only pure rules.
                for(int i=0;i<game.Run.Trees.Length;i++)if(game.Run.Trees[i].State==TreeState.Burning)game.Run.ApplyWater(game.Run.Trees[i].Position,100,.1f);
                game.StartNext();yield return new WaitForSeconds(.3f);Capture(game.Player.ViewCamera,"Logs/FirePlay-CabinRescue.png");
            }
            finally{InputSystem.RemoveDevice(mouse);InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editor;}
            yield return SceneManager.LoadSceneAsync("MainMenu");yield return null;
        }
        [UnityTest] public IEnumerator ShopButtonsRespondToActualMouseClicks()
        {
            yield return SceneManager.LoadSceneAsync("FireRescue");yield return null;yield return null;
            var game=Object.FindFirstObjectByType<FireRescueGame>();
            for(int i=0;i<game.Run.Trees.Length;i++)game.Run.ApplyWater(game.Run.Trees[i].Position,100,.1f);
            game.OpenShop();yield return null;
            var background=InputSystem.settings.backgroundBehavior;var editor=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var mouse=InputSystem.AddDevice<Mouse>();game.SendMessage("OnApplicationFocus",true);
            try
            {
                foreach(var name in new[]{"Next gear","Buy gear","Leave shop"})
                {
                    var button=game.GetComponentsInChildren<WorkshopButton>().First(b=>b.name==name);
                    var screen=game.Player.ViewCamera.WorldToScreenPoint(button.transform.position);var point=new Vector2(screen.x,screen.y);
                    InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;yield return null;
                    InputSystem.QueueStateEvent(mouse,new MouseState{position=point,buttons=1});yield return null;yield return null;
                }
                Assert.AreEqual(FireEquipmentId.PulsePistol,game.SelectedEquipment);Assert.IsTrue(game.Run.Owns(FireEquipmentId.PulsePistol));Assert.IsFalse(game.ShopOpen);
                game.OpenShop();game.SelectEquipment(3);game.CloseShop();game.StartNext();
                Assert.IsTrue(game.Run.Owns(game.SelectedEquipment),"Leaving an unowned shop preview must restore a usable tool for VR");
                Assert.IsTrue(game.TakeEquipment(0));Assert.IsTrue(game.HoldingEquipment);
                float tank=game.Run.Water;game.CycleOwnedEquipment(1);
                Assert.IsFalse(game.HoldingEquipment);Assert.AreEqual(FireEquipmentId.PulsePistol,game.SelectedEquipment);
                Assert.IsTrue(game.TakeEquipment(0));Assert.AreEqual(FireEquipmentId.PulsePistol,game.Run.Equipped);
                game.CycleOwnedEquipment(1);Assert.AreEqual(FireEquipmentId.Extinguisher,game.SelectedEquipment,"Skip all unowned previews in combat");
                game.TakeEquipment(0);Assert.AreEqual(tank,game.Run.Water,"Cycling the rack must not provide free water");
            }
            finally{InputSystem.RemoveDevice(mouse);InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editor;}
            yield return SceneManager.LoadSceneAsync("MainMenu");yield return null;
        }
        static void Capture(Camera camera,string path)
        {
            var target=new RenderTexture(1920,1080,24);var prior=camera.targetTexture;var active=RenderTexture.active;var pixels=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1920,1080),0,0);pixels.Apply();Directory.CreateDirectory("Logs");File.WriteAllBytes(path,pixels.EncodeToPNG());}
            finally{camera.targetTexture=prior;RenderTexture.active=active;Object.Destroy(pixels);Object.Destroy(target);}
        }
    }
}


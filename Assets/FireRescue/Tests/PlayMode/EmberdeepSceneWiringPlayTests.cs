using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Oculus.Interaction;
using PhysicalAssembly;
using PhysicalAssembly.MetaXR;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    // Real scene transition and production public adapters, with synthetic poses.
    // Editor IsXR is deliberately false: these tests establish scene wiring, not
    // live XR tracking, the private Update sampling branch, or physical comfort.
    public sealed class EmberdeepSceneWiringPlayTests
    {
        const float Dt=.025f,Open=.04f,Closed=.01f;
        EmberdeepCamp camp;
        FireRescueGame game;
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;
        float volume;

        [SetUp] public void Setup()
        {
            background=InputSystem.settings.backgroundBehavior;
            editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            volume=AudioListener.volume;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }
        [TearDown] public void Cleanup()
        {
            if(camp)camp.enabled=false;if(game)game.enabled=false;
            AudioListener.volume=volume;
            InputSystem.settings.backgroundBehavior=background;
            InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
        }

        static EmberdeepHandVisuals CheckExistingRigBinding(Component owner,MetaPlayerRig player)
        {
            var adapters=owner.GetComponents<EmberdeepHandVisuals>();
            Assert.AreEqual(1,adapters.Length,"Each scene installs exactly one hand rendering adapter");
            var adapter=adapters[0];Assert.NotNull(adapter.RuntimeMaterial);
            var sdkHands=player.XRRig.GetComponentsInChildren<HandVisual>(true)
                .Where(h=>!h.GetComponentInParent<Oculus.Interaction.DistanceReticles.ReticleGhostDrawer>(true)).ToArray();
            Assert.AreEqual(2,sdkHands.Length,"Bind the two live hands, not the four distance-grab preview hands");
            Assert.AreEqual(sdkHands.Length,adapter.BoundVisualCount);
            int skins=0;
            foreach(var hand in sdkHands)
                foreach(var skin in hand.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    skins++;
                    foreach(var material in skin.sharedMaterials)Assert.AreSame(adapter.RuntimeMaterial,material);
                }
            Assert.Greater(skins,0,"A component with no bound hand mesh is not a successful installation");
            foreach(var ray in player.XRRig.GetComponentsInChildren<RayInteractorRayVisual>(true))
                Assert.IsFalse(ray.enabled,"The native SDK ray must not coexist with the contextual scene ray");
            return adapter;
        }
        static WorkshopButton Button(Component owner,string command)=>owner.GetComponentsInChildren<WorkshopButton>(true).Single(b=>b.Command==command);
        static EmberdeepHandRay.Sample Point(Camera camera,WorkshopButton button,float pinch)
        {
            var origin=camera.transform.position-camera.transform.up*.15f;
            return new EmberdeepHandRay.Sample(true,new Ray(origin,button.GetComponent<Collider>().bounds.center-origin),pinch);
        }
        void CampSample(int hand,EmberdeepHandRay.Sample sample)=>camp.ProcessHandMenuInput(hand==0?sample:default,hand==1?sample:default,true,Dt);
        bool GameSample(int hand,EmberdeepHandRay.Sample sample)=>game.ProcessHandMenuInput(hand==0?sample:default,hand==1?sample:default,true,Dt);
        static void Capture(Camera camera,string name)
        {
            const string directory="Logs/EmberdeepInteractionStages";
            var target=new RenderTexture(1600,900,24);var prior=camera.targetTexture;var active=RenderTexture.active;
            var pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();
                Directory.CreateDirectory(directory);File.WriteAllBytes(Path.Combine(directory,name+".png"),pixels.EncodeToPNG());
                File.WriteAllText(Path.Combine(directory,"Editor-synthetic-captures.txt"),
                    "Editor PlayMode scene wiring tests with synthetic hand samples. Camp and fireground use the unchanged scene camera. Detail images temporarily reposition only the Editor camera, restored immediately. No headset tracking or performance claim; no renderer or world object is hidden for capture. Game root input update is disabled while synthetic samples drive its public adapters.");
            }
            finally{camera.targetTexture=prior;RenderTexture.active=active;Object.Destroy(pixels);target.Release();Object.Destroy(target);}
        }
        void CaptureStation(string name)
        {
            var camera=game.Player.ViewCamera;var position=camera.transform.position;var rotation=camera.transform.rotation;
            try
            {
                var station=game.Pump.transform;
                var focus=station.position+Vector3.up*.2f;
                camera.transform.position=focus+game.Arena.rotation*new Vector3(.25f,.25f,-.5f);
                camera.transform.rotation=Quaternion.LookRotation(focus-camera.transform.position);
                Capture(camera,name);
            }
            finally{camera.transform.SetPositionAndRotation(position,rotation);}
        }
        void RefillSample(int hand,Pose palm,float pinch,Vector3 tip)
        {
            var sample=new EmberdeepRefillInteraction.Hand(true,palm,pinch,tip);
            var idle=new EmberdeepRefillInteraction.Hand(true,new Pose(game.Arena.position+Vector3.up,Quaternion.identity),Open);
            game.ProcessRefillHands(hand==0?sample:idle,hand==1?sample:idle,Dt);
        }
        // One fist swing through the cap: rest above the rearm height, then fall at ~1.4 m/s.
        bool Hammer(int hand)
        {
            var top=game.Pump.CapTop;var up=game.Pump.Up;var none=default(EmberdeepFistHammer.Hand);
            var fist=new EmberdeepFistHammer.Hand(true,top+up*.15f,.95f);
            game.SamplePumpHands(hand==0?fist:none,hand==1?fist:none,.014f);
            for(int i=1;i<=10;i++)
            {
                fist.Fist=top+up*(.15f-.02f*i);
                if(game.SamplePumpHands(hand==0?fist:none,hand==1?fist:none,.014f))return true;
            }
            return false;
        }
        void CheckStationRefresh(string tank)
        {
            game.Refresh();
            var station=game.HydrantStation;
            Assert.AreEqual(tank,station.transform.Find("Tank status").GetComponent<TextMesh>().text);
            // No fake XR provider: Editor updates the cue but shows only the
            // keyboard status panel, never a misleading visible HANDS PAUSED.
            Assert.IsFalse(game.Player.IsXR);
            Assert.AreEqual(EmberdeepRefillTutorial.Step.Unavailable,station.TutorialCue.Stage);
            Assert.AreEqual(station.TutorialCue.Text,station.transform.Find("Refill step").GetComponent<TextMesh>().text);
            Assert.IsFalse(station.transform.Find("Refill step").gameObject.activeSelf);
            Assert.IsFalse(station.transform.Find("Refill step plate").gameObject.activeSelf);
            Assert.IsTrue(station.transform.Find("Supply status").gameObject.activeSelf);
            Assert.IsTrue(station.transform.Find("Instruction plate").gameObject.activeSelf);
            Assert.IsFalse(station.transform.Find("Refill next action marker").gameObject.activeSelf);
            Assert.IsFalse(station.GetComponentsInChildren<LineRenderer>(true).Single(l=>l.name=="Refill alignment guide").enabled);
            Assert.That(Quaternion.Angle(station.transform.Find("Refill step").rotation,game.Player.ViewCamera.transform.rotation),Is.LessThan(.01f));
        }

        [UnityTest] public IEnumerator LeftHandCampToFiregroundManualRefillWiring()=>CompleteSceneRoute(0);
        [UnityTest] public IEnumerator RightHandCampToFiregroundManualRefillWiring()=>CompleteSceneRoute(1);
        IEnumerator CompleteSceneRoute(int hand)
        {
            EmberdeepSession.NewExpedition();yield return SceneManager.LoadSceneAsync(EmberdeepSession.CampScene);
            yield return null;yield return null;
            camp=Object.FindFirstObjectByType<EmberdeepCamp>();Assert.NotNull(camp);
            camp.SendMessage("OnApplicationFocus",true);camp.SendMessage("OnApplicationPause",false);
            float deadline=Time.realtimeSinceStartup+10;
            while(!camp.IsPlaced && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(camp.IsPlaced);yield return null;
            var campHands=CheckExistingRigBinding(camp,camp.Player);
            Assert.IsTrue(campHands.ContainsOcclusionPoint(camp.MenuRoot.position),"The menu board's interior must be registered by Camp.Start");
            Assert.AreSame(camp.Player.ViewCamera,camp.MenuInput.ViewCamera,"Production sampling installs the viewport eligibility camera");
            camp.enabled=false;Physics.SyncTransforms();
            var start=Button(camp,"New Game");var aim=Point(camp.Player.ViewCamera,start,Open);
            var miss=new EmberdeepHandRay.Sample(true,new Ray(aim.Ray.origin,Vector3.up),Open);
            // Headset test 2 (2026-11-22): the camp ray shows only on or near the sign, never beside the palm-water aim.
            CampSample(hand,miss);Assert.IsFalse(camp.MenuInput.RayVisible(hand),"No ray pointing away from the sign");Assert.IsFalse(camp.MenuInput.OwnsInput);
            var near=new EmberdeepHandRay.Sample(true,new Ray(aim.Ray.origin,start.transform.position+start.transform.right*(start.transform.lossyScale.x*.5f+.04f)-aim.Ray.origin),Open);
            CampSample(hand,near);Assert.IsTrue(camp.MenuInput.RayVisible(hand),"Camp shows live hand aim near the sign before acquiring a button");Assert.IsFalse(camp.MenuInput.OwnsInput);
            for(int i=0;i<8;i++)CampSample(hand,aim);
            Assert.AreSame(start,camp.HoveredButton);Assert.IsTrue(camp.MenuInput.RayVisible(hand));
            if(hand==1)Capture(camp.Player.ViewCamera,"Editor-synthetic-Camp-hover");
            CampSample(hand,miss);Assert.IsFalse(camp.MenuInput.RayVisible(hand));Assert.IsNull(camp.HoveredButton);
            for(int i=0;i<8;i++)CampSample(hand,aim);
            aim.Pinch=Closed;CampSample(hand,aim);
            deadline=Time.realtimeSinceStartup+15;
            while(SceneManager.GetActiveScene().name!=EmberdeepSession.FireScene && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.AreEqual(EmberdeepSession.FireScene,SceneManager.GetActiveScene().name,"The real New Game button enters only the fireground");
            yield return null;yield return null;
            game=Object.FindFirstObjectByType<FireRescueGame>();Assert.NotNull(game);Assert.IsNull(Object.FindFirstObjectByType<EmberdeepCamp>());
            var gameHands=CheckExistingRigBinding(game,game.Player);
            Assert.IsTrue(gameHands.ContainsOcclusionPoint(game.Arena.TransformPoint(new Vector3(0,-.12f,6))));
            Assert.AreSame(game.Player.ViewCamera,game.MenuInput.ViewCamera);
            game.enabled=false;game.SendMessage("OnApplicationFocus",true);game.SendMessage("OnApplicationPause",false);
            Assert.True(game.Run.IsPalmLesson);Assert.False(game.TakeEquipment(hand));
            for(int stage=0;stage<FireRun.PalmLessons;stage++) // stage cut (T): one palm lesson
            {EmberdeepTestFlow.StartPrepared(game.Run);game.Run.ApplyWater(Vector3.zero,10000,100);game.Run.EvacuateSafeAnimals();if(game.Run.NeedsMagicUpgrade)Assert.True(game.Run.BuyMagicUpgrade());game.StartNext();}
            EmberdeepTestFlow.StartPrepared(game.Run);game.Refresh();Physics.SyncTransforms();
            // Spell mode retires the hand-held tools: nothing to pick up or dock.
            // Intent changed (LoopSpec §3): the well, its lever and the altar basin are gone;
            // a fist hammered onto the pump cap refills the gem directly.
            Assert.IsFalse(game.Run.IsPalmLesson);Assert.IsFalse(game.TakeEquipment(hand));Assert.IsFalse(game.HoldingEquipment);
            Assert.IsFalse(game.transform.Find("Equipment rack").gameObject.activeSelf);
            Assert.IsFalse(Button(game,"Return equipment").gameObject.activeSelf);
            Assert.IsFalse(game.HydrantStation.gameObject.activeSelf);Assert.IsTrue(game.Pump.gameObject.activeInHierarchy);
            if(hand==1)Capture(game.Player.ViewCamera,"Editor-synthetic-Fireground-overview");
            for(int i=0;i<100 && !game.Run.GemEmpty;i++)game.Run.TickMagic(.1f,true,true);
            Assert.IsTrue(game.Run.GemEmpty);
            for(int stroke=0;stroke<3;stroke++)
            {
                float before=game.Run.MagicWater;
                Assert.IsTrue(Hammer(hand),"A fast fist through the cap is one strike");
                Assert.That(game.Run.MagicWater,Is.EqualTo(before+EmberdeepWaterTuning.StrikeWater).Within(.01f));
            }
            Assert.AreEqual(60,game.Run.Water,"The retired tool tank receives nothing");
            game.Refresh();
            if(hand==1)CaptureStation("Editor-detail-synthetic-Pump-strike");
            yield return null;
        }
    }
}

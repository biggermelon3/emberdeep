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
                var station=game.HydrantStation.transform;
                var focus=station.TransformPoint(new Vector3(0,.22f,.025f));
                camera.transform.position=station.TransformPoint(new Vector3(.4f,.45f,-.78f));
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
        void LeverSample(int hand,Pose palm,float closure,int frames=1)
        {
            var sample=new EmberdeepRefillInteraction.Hand(true,palm,closure>.5f?Closed:Open,palm.position)
                {GripValid=false,GripStrength=0};
            var idle=new EmberdeepRefillInteraction.Hand(true,new Pose(game.Arena.position+Vector3.up,Quaternion.identity),Open);
            for(int i=0;i<frames;i++)game.ProcessRefillHands(hand==0?sample:idle,hand==1?sample:idle,Dt);
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
            CampSample(hand,miss);Assert.IsFalse(camp.MenuInput.RayVisible(hand));
            for(int i=0;i<8;i++)CampSample(hand,aim);
            Assert.AreSame(start,camp.HoveredButton);Assert.IsTrue(camp.MenuInput.RayVisible(hand));
            if(hand==1)Capture(camp.Player.ViewCamera,"Editor-synthetic-Camp-hover");
            CampSample(hand,miss);Assert.IsFalse(camp.MenuInput.RayVisible(hand));
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
            Assert.IsTrue(game.TakeEquipment(hand));game.Refresh();Physics.SyncTransforms();
            if(hand==1)Capture(game.Player.ViewCamera,"Editor-synthetic-Fireground-overview");
            Assert.IsTrue(gameHands.ContainsOcclusionPoint(game.HeldTool.transform.TransformPoint(new Vector3(0,-.045f,0))),"Equipping registers the held extinguisher's interior");
            var returnButton=Button(game,"Return equipment");var ui=Point(game.Player.ViewCamera,returnButton,Open);
            for(int i=0;i<8;i++)Assert.IsFalse(GameSample(hand,ui));
            Assert.AreSame(returnButton,game.MenuInput.HoveredButton);Assert.IsTrue(game.MenuInput.RayVisible(hand));
            miss=new EmberdeepHandRay.Sample(true,new Ray(ui.Ray.origin,Vector3.up),Open);
            GameSample(hand,miss);Assert.IsFalse(game.MenuInput.RayVisible(hand));
            game.Run.Tick(Dt,false);
            for(int i=0;i<100 && game.Run.Water>0;i++)game.Run.Tick(.1f,true);
            game.Run.CancelTrigger();Assert.Zero(game.Run.Water);CheckStationRefresh("EMPTY");
            var tool=game.HeldTool;var station=game.HydrantStation;
            var rotation=station.Socket.rotation*Quaternion.Euler(0,180,0)*Quaternion.Inverse(tool.RefillPort.localRotation);
            var dockingGrip=station.Socket.position+rotation*(tool.Grip.localPosition-tool.RefillPort.localPosition);
            var palm=new Pose(dockingGrip+station.Socket.right*.14f,Quaternion.Euler(80,45,110));
            for(int i=0;i<12;i++)
            {
                Assert.IsTrue(EmberdeepRefillInteraction.TryGuideHeldTool(tool,palm,station,game.CreekStation));
                RefillSample(hand,palm,Open,palm.position);
            }
            Assert.IsFalse(game.Run.Supply.Locked);Assert.Zero(game.Run.Water);
            palm.position=dockingGrip+station.Socket.right*.055f;
            for(int i=0;i<12;i++)
            {
                if(!game.Run.Supply.Locked)EmberdeepRefillInteraction.TryGuideHeldTool(tool,palm,station,game.CreekStation);
                RefillSample(hand,palm,Open,palm.position);
            }
            Assert.IsTrue(game.Run.Supply.Locked);Assert.IsFalse(game.Run.Supply.ValveOpen);Assert.Zero(game.Run.Water);
            CheckStationRefresh("EMPTY");Physics.SyncTransforms();
            if(hand==1)CaptureStation("Editor-detail-synthetic-Hydrant-docked");
            ui=Point(game.Player.ViewCamera,returnButton,Open);
            for(int i=0;i<8;i++)Assert.IsFalse(GameSample(hand,ui));
            ui.Pinch=Closed;Assert.IsFalse(GameSample(hand,ui));Assert.IsFalse(game.MenuInput.RayVisible(hand));
            Assert.IsTrue(game.Run.Supply.Locked,"A blocked HUD action must not detach the supply owner");
            Assert.AreEqual(WaterStationTier.LeverWell,game.Run.StationTier);
            var lever=station.Lever.position;
            palm=new Pose(lever,station.Lever.rotation);
            LeverSample(hand,palm,0,6);
            LeverSample(hand,palm,1,4);Assert.IsFalse(game.Run.Supply.ValveOpen);
            for(int stroke=0;stroke<5;stroke++)
            {
                palm.position=lever-station.transform.up*.085f;
                LeverSample(hand,palm,1);
                Assert.That(game.Run.Water,Is.EqualTo(stroke*12).Within(.001f),"Downstroke alone cannot credit water");
                palm.position=lever;LeverSample(hand,palm,1);
                CheckStationRefresh(stroke==4?"FULL":"WATER");
            }
            LeverSample(hand,palm,0,4);
            Assert.AreEqual(game.Run.Capacity,game.Run.Water);Assert.IsFalse(game.Run.Supply.ValveOpen);CheckStationRefresh("FULL");
            if(hand==1)CaptureStation("Editor-detail-synthetic-Hydrant-full");
            float full=game.Run.Water,supplied=game.Run.SuppliedWater;var grip=tool.Grip.position;
            palm.position=grip+Vector3.up*.15f;
            for(int i=0;i<6;i++)RefillSample(hand,palm,Open,grip);
            RefillSample(hand,palm,Closed,grip);
            Assert.IsFalse(game.Run.Supply.Locked);Assert.IsTrue(game.HoldingEquipment);Assert.AreEqual(hand==0,game.LeftHanded);
            Assert.AreEqual(full,game.Run.Water);Assert.AreEqual(supplied,game.Run.SuppliedWater);Assert.Zero(game.Run.Charge01);
            CheckStationRefresh("FULL");yield return null;
        }
    }
}

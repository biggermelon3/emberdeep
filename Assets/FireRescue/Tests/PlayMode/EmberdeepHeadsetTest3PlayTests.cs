using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PhysicalAssembly;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    // Headset test 3 (2026-11-22), menus: the camp tutorial's small sign and the water-first ray rule, drag sliders on the
    // camp settings page and on the pause panel, paired choices shown by colour. Synthetic rays and pinch spans through
    // the production menu path; writes captures to Logs/EmberdeepHeadsetTest3/ (rewritten each run).
    public sealed class EmberdeepHeadsetTest3PlayTests
    {
        const string Output="Logs/EmberdeepHeadsetTest3";
        const float Open=.04f,Closed=.01f,Dt=.02f;
        static readonly EmberdeepFirstHints.Move[] Moves=(EmberdeepFirstHints.Move[])System.Enum.GetValues(typeof(EmberdeepFirstHints.Move));
        int[] hints;int tutorialSeen;EmberdeepSettingsPrefs prefs;
        InputSettings.BackgroundBehavior background;InputSettings.EditorInputBehaviorInPlayMode editorInput;

        [SetUp] public void Save()
        {
            hints=Moves.Select(m=>PlayerPrefs.GetInt(EmberdeepFirstHints.Key(m),-1)).ToArray();
            tutorialSeen=PlayerPrefs.GetInt(EmberdeepCamp.TutorialPrefsKey,-1);
            prefs=EmberdeepSettingsPrefs.Save();
            foreach(var key in new[]{EmberdeepPlayArea.HeightKey,EmberdeepPlayArea.DistanceKey,EmberdeepPlayArea.TiltedKey,EmberdeepWaterHand.PrefsKey})PlayerPrefs.DeleteKey(key);
            EmberdeepPlayArea.Reload();EmberdeepWaterHand.Reload();
            background=InputSystem.settings.backgroundBehavior;editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Directory.CreateDirectory(Output);
        }
        [TearDown] public void Restore()
        {
            for(int i=0;i<Moves.Length;i++)if(hints[i]<0)PlayerPrefs.DeleteKey(EmberdeepFirstHints.Key(Moves[i]));else PlayerPrefs.SetInt(EmberdeepFirstHints.Key(Moves[i]),hints[i]);
            if(tutorialSeen<0)PlayerPrefs.DeleteKey(EmberdeepCamp.TutorialPrefsKey);else PlayerPrefs.SetInt(EmberdeepCamp.TutorialPrefsKey,tutorialSeen);
            prefs.Restore();
            InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
        }

        // ---- Camp ----------------------------------------------------------------------------------------------------
        static IEnumerator LoadCamp()
        {
            EmberdeepSession.NewExpedition();
            yield return SceneManager.LoadSceneAsync(EmberdeepSession.CampScene);yield return null;yield return null;
            var camp=Object.FindFirstObjectByType<EmberdeepCamp>();Assert.NotNull(camp);camp.SendMessage("OnApplicationFocus",true);
            float deadline=Time.realtimeSinceStartup+10;while(!camp.IsPlaced && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(camp.IsPlaced);
        }
        static EmberdeepCamp Camp=>Object.FindFirstObjectByType<EmberdeepCamp>();
        static WorkshopButton CampButton(EmberdeepCamp camp,string command)=>camp.GetComponentsInChildren<WorkshopButton>(true).Single(b=>b.Command==command);
        // Where a seated player's water hand is while it aims: in front of the chest on its side.
        static Vector3 HandOrigin(EmberdeepCamp camp,bool left)
        {
            var eye=camp.Player.ViewCamera.transform.position;var menu=camp.MenuRoot;
            var forward=Vector3.ProjectOnPlane(menu.forward,Vector3.up).normalized;var right=Vector3.Cross(Vector3.up,forward);
            return eye+right*(left?-.17f:.17f)+Vector3.down*.32f+forward*.32f;
        }

        // The tutorial's sign is a small plank at the top of the posts; an open palm aimed at the campfire past it gets the
        // water and no ray, and pointing straight at a tutorial button still shows the ray.
        [UnityTest] public IEnumerator TutorialSignIsSmallAndAnAimingPalmKeepsTheWater()
        {
            yield return LoadCamp();var camp=Camp;camp.enabled=false;Physics.SyncTransforms();
            var menu=camp.MenuRoot;var play=camp.Play;Assert.NotNull(play);Assert.IsTrue(play.Built);
            Assert.IsTrue(camp.MenuBoardShown);Assert.IsFalse(camp.TutorialSignShown);
            Assert.AreEqual(EmberdeepHandRayMenu.PanelShowReach,camp.MenuInput.ShowReach);
            camp.StartTutorial();yield return null;Physics.SyncTransforms();
            Assert.AreEqual(EmberdeepCamp.MenuPage.Tutorial,camp.ActivePage);Assert.IsTrue(camp.TutorialActive);
            Assert.IsFalse(camp.MenuBoardShown,"the full board folds away");Assert.IsTrue(camp.TutorialSignShown);
            var sign=camp.TutorialSignBounds;
            float bottom=menu.InverseTransformPoint(sign.center-Vector3.up*sign.extents.y).y,top=menu.InverseTransformPoint(sign.center+Vector3.up*sign.extents.y).y;
            Assert.LessOrEqual(top-bottom,.17f,"a plank, not a board (the board is .82 tall)");
            Assert.GreaterOrEqual(bottom,.2f,"its lower edge is 20 cm over the old board's centre");
            Assert.AreEqual(EmberdeepCamp.TutorialShowReach,camp.MenuInput.ShowReach,"the near-panel reach shrinks");
            var skip=CampButton(camp,"Skip tutorial");Assert.IsTrue(skip.gameObject.activeInHierarchy);
            var skipSize=Vector3.Scale(skip.GetComponent<BoxCollider>().size,skip.transform.lossyScale);
            Assert.GreaterOrEqual(Mathf.Min(skipSize.x,skipSize.y),.03f,"SKIP is still a 3 cm target");

            int water=play.WaterHandLeft?0:1;var hand=HandOrigin(camp,water==0);
            void Point(Vector3 at,Vector3? from=null)
            {
                var origin=from??hand;var s=new EmberdeepHandRay.Sample(true,new Ray(origin,at-origin),Open);
                camp.ProcessHandMenuInput(water==0?s:default,water==1?s:default,true,Dt);
            }
            play.PalmAimOverride=true;
            // 1. The open palm aims at the campfire: the ray stays down, the water hand is free.
            var campfire=play.FireWorld(EmberdeepCampPlay.Campfire);
            var plane=new Plane(-menu.forward,menu.position);var toFire=new Ray(hand,campfire-hand);
            if(plane.Raycast(toFire,out float along) && along<Vector3.Distance(hand,campfire))
                Assert.Less(menu.InverseTransformPoint(toFire.GetPoint(along)).y,bottom-.3f,"the aiming line passes far under the plank");
            for(int i=0;i<10;i++)Point(campfire);
            Assert.IsFalse(camp.MenuInput.RayVisible(water));Assert.IsFalse(play.WaterRayUp);
            yield return null;Assert.IsTrue(play.WaterHandFree,"the palm gesture may cast");
            // Looking down at the island (the tutorial's view), the plank is out of sight, and the menu only takes buttons
            // in view: SKIP cannot raise a ray at all.
            var view=camp.Player.ViewCamera.transform;var lookDown=view.rotation;
            Assert.Greater(camp.Player.ViewCamera.WorldToViewportPoint(skip.transform.position).y,1.05f,"SKIP is above the view while looking at the island");
            Point(skip.transform.position);Assert.IsFalse(camp.MenuInput.RayVisible(water));
            // Looking up at the plank:
            view.rotation=Quaternion.LookRotation(menu.TransformPoint(new Vector3(0,EmberdeepCamp.TutorialBoardY,0))-view.position,Vector3.up);
            for(int i=0;i<10;i++)Point(campfire);
            // 2. Just past the plank's edge (2 cm under SKIP, inside the shrunk 3 cm reach): the aiming palm keeps the ray
            //    down; the same ray from a hand that is not aiming would raise it.
            //    Straight on (a hand raised toward the plank), so the ray cannot graze the button's underside.
            var under=skip.transform.TransformPoint(new Vector3(0,-.5f,-.5f))-menu.up*.02f;var raised=under-menu.forward*.7f;
            for(int i=0;i<5;i++)Point(under,raised);
            Assert.IsFalse(camp.MenuInput.RayVisible(water),"aiming palm past the edge: no ray");Assert.IsNull(camp.HoveredButton);
            play.PalmAimOverride=false;Point(under,raised);
            Assert.IsTrue(camp.MenuInput.RayVisible(water),"not aiming: the near-panel ray finds the plank");
            play.PalmAimOverride=true;for(int i=0;i<10;i++)Point(campfire);
            Assert.IsFalse(camp.MenuInput.RayShown(water));
            // 3. Pointing straight at SKIP with the same open palm: the ray shows, and SKIP is the target.
            for(int i=0;i<3;i++)Point(skip.transform.position);
            Assert.IsTrue(camp.MenuInput.RayVisible(water));Assert.AreSame(skip,camp.HoveredButton);Assert.IsTrue(play.WaterRayUp);
            // Sign and island in one frame: halfway between the plank and the campfire, wide.
            var both=Quaternion.LookRotation(Vector3.Lerp(menu.TransformPoint(new Vector3(0,EmberdeepCamp.TutorialBoardY,0)),campfire,.5f)-view.position,Vector3.up);
            CaptureCamp(camp,"1b-tutorial-skip-ray",both,84);
            CaptureCamp(camp,"1c-tutorial-sign-front",Quaternion.LookRotation(menu.TransformPoint(new Vector3(0,.05f,0))-view.position,Vector3.up),55);
            for(int i=0;i<10;i++)Point(campfire);
            Assert.IsFalse(camp.MenuInput.RayShown(water));
            view.rotation=lookDown;
            // The shown aim (desktop: scripted from the view's palm, the same aim renderer).
            for(int i=0;i<12;i++){play.SprayAt(campfire);yield return null;}
            Assert.IsTrue(play.Aiming);Assert.IsTrue(play.Aim.GetComponent<MeshRenderer>().enabled,"the aim arc draws");Assert.IsFalse(play.WaterRayUp);
            CaptureCamp(camp,"1-tutorial-sign-aim",both,84);
            play.PalmAimOverride=null;camp.SkipTutorial();yield return null;
            Assert.IsTrue(camp.MenuBoardShown,"the board is back after the tutorial");Assert.AreEqual(EmberdeepHandRayMenu.PanelShowReach,camp.MenuInput.ShowReach);
        }

        // Camp settings: pinch the HEIGHT knob and drag; the value follows live and is saved on release. Paired choices
        // read by colour, in English and in Chinese.
        [UnityTest] public IEnumerator CampSliderDragsLiveAndSavesOnRelease()
        {
            EmberdeepPlayArea.SetTilted(false); // the slider test reads FLAT chosen (tilted is the default since headset test 4)
            yield return LoadCamp();var camp=Camp;camp.enabled=false;camp.ShowPage(EmberdeepCamp.MenuPage.Settings);yield return null;Physics.SyncTransforms();
            Assert.IsTrue(camp.OptionChosen("Board flat"));Assert.IsFalse(camp.OptionChosen("Board tilted"));
            Assert.IsTrue(camp.OptionChosen("Water hand left"));Assert.IsFalse(camp.OptionChosen("Water hand right"));
            Assert.IsFalse(camp.MenuRoot.GetComponentsInChildren<Transform>(true).Any(t=>t.name.EndsWith(" chosen")),"no underline marks");
            var track=CampButton(camp,"Height track");var page=track.transform.parent;float y=track.transform.localPosition.y;
            var hand=HandOrigin(camp,false);
            Ray At(float t)
            {
                var p=page.TransformPoint(new Vector3(Mathf.Lerp(EmberdeepCamp.TrackLeft,EmberdeepCamp.TrackRight,t),y,EmberdeepCamp.ControlZ-.0125f));
                return new Ray(hand,p-hand);
            }
            void Right(Ray ray,float pinch)=>camp.ProcessMenuInput(ray,true,false,float.PositiveInfinity,true,pinch,Dt);
            float start=EmberdeepPlayArea.Height01;int commits=camp.MenuInput.DragCommits,changes=0;
            void Count()=>changes++;EmberdeepPlayArea.Changed+=Count;
            try
            {
                for(int i=0;i<8;i++)Right(At(start),Open);
                Right(At(start),Closed);
                Assert.IsTrue(camp.SliderHeld("Height"),"the pinch on the knob grabs it");Assert.AreEqual(start,EmberdeepPlayArea.Height01,1e-4f,"no jump");
                for(int i=1;i<=10;i++)Right(At(Mathf.Lerp(start,.8f,i/10f)),Closed);
                Assert.AreEqual(Mathf.Lerp(EmberdeepPlayArea.HeightMin,EmberdeepPlayArea.HeightMax,.8f),EmberdeepPlayArea.Height,.011f,"live");
                Assert.Greater(changes,5,"the board would re-place at every centimetre");Assert.AreEqual(commits,camp.MenuInput.DragCommits,"not saved while held");
                Assert.AreEqual(.8f,camp.SliderKnob01("Height"),.02f,"the knob follows");
                var h=EmberdeepPlayArea.Height;Assert.AreEqual(Mathf.Round(h*100),h*100,1e-3f,"whole centimetres");
                CaptureCamp(camp,"2-camp-slider-held",Quaternion.LookRotation(camp.MenuRoot.position-camp.Player.ViewCamera.transform.position,Vector3.up),50);
                // Still pinched, the ray wanders onto TILTED: it never toggles.
                var tilted=CampButton(camp,"Board tilted");
                for(int i=0;i<12;i++)Right(new Ray(hand,tilted.transform.position-hand),Closed);
                Assert.IsFalse(EmberdeepPlayArea.Tilted,"a held pinch dragged onto another control never clicks it");Assert.IsTrue(camp.SliderHeld("Height"));
                for(int i=0;i<6;i++)Right(At(.8f),Closed);
                for(int i=0;i<3;i++)Right(At(.8f),Open);
                Assert.IsFalse(camp.SliderHeld("Height"));Assert.AreEqual(commits+1,camp.MenuInput.DragCommits,"saved once, on release");
                Assert.AreEqual(Mathf.Lerp(EmberdeepPlayArea.HeightMin,EmberdeepPlayArea.HeightMax,.8f),EmberdeepPlayArea.Height,.011f);
                Assert.AreEqual(EmberdeepPlayArea.Height,PlayerPrefs.GetFloat(EmberdeepPlayArea.HeightKey),1e-5f);
                Assert.IsFalse(EmberdeepPlayArea.Tilted);
            }
            finally{EmberdeepPlayArea.Changed-=Count;}
            // Colour, not an underline: pick RIGHT and TILTED, capture, then the same in Chinese.
            EmberdeepWaterHand.Set(false);EmberdeepPlayArea.SetTilted(true);camp.RefreshSettings();yield return null;
            Assert.IsTrue(camp.OptionChosen("Water hand right"));Assert.IsTrue(camp.OptionChosen("Board tilted"));Assert.IsFalse(camp.OptionChosen("Board flat"));
            var look=Quaternion.LookRotation(camp.MenuRoot.position-camp.Player.ViewCamera.transform.position,Vector3.up);
            CaptureCamp(camp,"2b-camp-options-colour",look,50);
            var locale=LocalizationSettings.SelectedLocale;var zh=LocalizationSettings.AvailableLocales.GetLocale("zh-Hans");Assert.NotNull(zh);
            try
            {
                LocalizationSettings.SelectedLocale=zh;var localized=camp.GetComponent<EmberdeepLocalizedText>();
                float deadline=Time.realtimeSinceStartup+10;
                while(Time.realtimeSinceStartup<deadline && !(localized.Ready && localized.LocaleCode=="zh-Hans"))yield return null;
                localized.RefreshLabels();camp.RefreshSettings();yield return null;
                Assert.IsTrue(camp.OptionChosen("Board tilted"));
                CaptureCamp(camp,"2c-camp-options-colour-zh",look,50);
            }
            finally{LocalizationSettings.SelectedLocale=locale;}
        }
        // Captures taken mid-frame (right after synthetic samples): the menu clears the button tints on every sample and
        // EmberdeepForestUi re-applies them in LateUpdate, before a real frame renders. Do that here too.
        static void SkinFrame()
        {
            foreach(var ui in Object.FindObjectsByType<EmberdeepForestUi>(FindObjectsSortMode.None))
                ui.SendMessage("LateUpdate",SendMessageOptions.DontRequireReceiver);
        }
        static void CaptureCamp(EmberdeepCamp camp,string name,Quaternion? look=null,float fov=0)
        {
            var camera=camp.Player.ViewCamera;var target=new RenderTexture(1600,1200,24){antiAliasing=4};
            var prior=camera.targetTexture;var active=RenderTexture.active;float priorFov=camera.fieldOfView;var rotation=camera.transform.rotation;
            var pixels=new Texture2D(1600,1200,TextureFormat.RGB24,false);
            try
            {
                if(look.HasValue)camera.transform.rotation=look.Value;if(fov>0)camera.fieldOfView=fov;
                SkinFrame();camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1600,1200),0,0);pixels.Apply();File.WriteAllBytes(Output+"/"+name+".png",pixels.EncodeToPNG());
            }
            finally{camera.targetTexture=prior;RenderTexture.active=active;camera.fieldOfView=priorFov;camera.transform.rotation=rotation;target.Release();Object.Destroy(target);Object.Destroy(pixels);}
        }

        // ---- Pause panel ---------------------------------------------------------------------------------------------
        static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
        static void Clear(FireRun run){foreach(var a in run.Animals)run.ApplyAnimalWater(a.Position,100,.6f);foreach(var t in run.Trees)if(t.State==TreeState.Burning)run.ApplyWater(t.Position,1000,.1f);}
        static IEnumerator LoadFireground(int stage)
        {
            EmberdeepSession.NewExpedition();var run=EmberdeepSession.Run;
            for(int guard=0;guard<20 && run.Stage<stage;guard++)
            {
                if(run.Phase==FirePhase.Ready && !run.StartStage(true))break;
                EmberdeepTestFlow.StartPrepared(run);
                for(int i=0;i<400 && run.Phase==FirePhase.Fighting;i++){Clear(run);run.Tick(.05f,false);}
                if(run.Phase!=FirePhase.Cleared)break;
                for(int i=0;i<400 && run.AnimalsSaved<run.Animals.Length;i++)run.Tick(.05f,false);
                if(!run.StartStage(true))break;
            }
            Assert.AreEqual(stage,run.Stage);
            yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);yield return null;yield return null;
            Object.FindFirstObjectByType<FireRescueGame>().SendMessage("OnApplicationFocus",true);
            yield return Wait(1.2f);
        }
        static WorkshopButton GameButton(FireRescueGame game,string command)=>game.GetComponentsInChildren<WorkshopButton>(true).Single(b=>b.Command==command);

        // Pause panel: the HEIGHT slider replaces the steppers. Pinch and drag: the board rises live under the still panel,
        // whole centimetres, saved on release; the ends still nudge; FLAT / TILTED read by colour.
        [UnityTest] public IEnumerator PauseSliderMovesTheBoardLiveAndSavesOnRelease()
        {
            EmberdeepPlayArea.SetTilted(false); // flat: a raised tilted board lifts the panel by design (tilted is the default since headset test 4)
            yield return LoadFireground(3);
            var game=Object.FindFirstObjectByType<FireRescueGame>();EmberdeepTestFlow.StartPrepared(game.Run);yield return null;
            Assert.True(game.OpenPause());yield return null;yield return null;
            var panel=game.Arena.Find("Pause panel");Assert.True(game.PausePanelVisible);
            foreach(var name in new[]{"Pause height down","Pause height track","Pause height up","Pause distance down","Pause distance track","Pause distance up"})
                Assert.True(GameButton(game,name).gameObject.activeInHierarchy,name);
            Assert.IsTrue(game.PauseTiltChosen(false));Assert.IsFalse(game.PauseTiltChosen(true));
            Assert.AreEqual(EmberdeepPlayArea.DefaultHeight01,game.PauseKnob01(true),.01f,"the knob sits on the default");
            yield return Wait(.3f);
            // One frame from here to the release: the game's own desktop input runs between frames.
            var camera=game.Player.ViewCamera;var eye=camera.transform.position;
            var view=camera.transform.rotation;camera.transform.rotation=Quaternion.LookRotation(panel.position-eye,Vector3.up); // look at the panel
            var forward=Vector3.ProjectOnPlane(panel.forward,Vector3.up).normalized;var hand=eye+Vector3.Cross(Vector3.up,forward)*.17f+Vector3.down*.3f+forward*.3f;
            Ray At(float t)
            {
                var p=panel.TransformPoint(new Vector3(Mathf.Lerp(FireRescueGame.PauseTrackLeft,FireRescueGame.PauseTrackRight,t),FireRescueGame.PauseHeightY,-.0275f));
                return new Ray(hand,p-hand);
            }
            bool Right(Ray ray,float pinch)=>game.ProcessHandMenuInput(default,new EmberdeepHandRay.Sample(true,ray,pinch),true,Dt);
            float start=EmberdeepPlayArea.Height01,arenaY=game.Arena.position.y;int commits=game.MenuInput.DragCommits;
            float goal=start+.2f; // +10 cm on the 50 cm track
            for(int i=0;i<8;i++)Assert.IsFalse(Right(At(start),Open));
            Assert.AreSame(GameButton(game,"Pause height track"),game.MenuInput.HoveredButton);
            Assert.IsFalse(Right(At(start),Closed),"a grab, not a press");Assert.IsTrue(game.PauseSliderHeld);
            for(int i=1;i<=10;i++)Right(At(Mathf.Lerp(start,goal,i/10f)),Closed);
            Assert.AreEqual(.10f,game.Arena.position.y-arenaY,.012f,"the board rose live");Assert.AreEqual(commits,game.MenuInput.DragCommits,"not saved yet");
            camera.transform.rotation=Quaternion.LookRotation(panel.position-eye,Vector3.up);
            CapturePause(game,"3-pause-slider-held",panel.position+Vector3.down*.1f);
            for(int i=0;i<6;i++)Right(At(goal),Closed);
            for(int i=0;i<3;i++)Right(At(goal),Open);
            Assert.IsFalse(game.PauseSliderHeld);Assert.AreEqual(commits+1,game.MenuInput.DragCommits,"saved once, on release");
            Assert.AreEqual(EmberdeepPlayArea.Height,PlayerPrefs.GetFloat(EmberdeepPlayArea.HeightKey),1e-5f);
            camera.transform.rotation=view;
            yield return null;yield return null;
            Assert.AreEqual("+10 cm",game.PauseHeightLabel);Assert.AreEqual(goal,game.PauseKnob01(true),.02f);
            // The ends still nudge 2 cm.
            GameButton(game,"Pause height up").Press();yield return null;Assert.AreEqual("+12 cm",game.PauseHeightLabel);
            GameButton(game,"Pause board tilted").Press();yield return null;yield return null;
            Assert.IsTrue(game.PauseTiltChosen(true));Assert.IsFalse(game.PauseTiltChosen(false));
            yield return Wait(.3f);CapturePause(game,"3b-pause-tilted-colour",panel.position+Vector3.down*.1f);
            var locale=LocalizationSettings.SelectedLocale;var zh=LocalizationSettings.AvailableLocales.GetLocale("zh-Hans");Assert.NotNull(zh);
            try
            {
                LocalizationSettings.SelectedLocale=zh;var localized=game.GetComponent<EmberdeepLocalizedText>();
                if(localized)
                {
                    float deadline=Time.realtimeSinceStartup+10;
                    while(Time.realtimeSinceStartup<deadline && !(localized.Ready && localized.LocaleCode=="zh-Hans"))yield return null;
                    localized.RefreshLabels();
                }
                yield return Wait(.2f);CapturePause(game,"3c-pause-colour-zh",panel.position+Vector3.down*.1f);
            }
            finally{LocalizationSettings.SelectedLocale=locale;}
            game.ClosePause();
        }
        static void CapturePause(FireRescueGame game,string name,Vector3 look,float fov=60)
        {
            var camera=game.Player.ViewCamera;var eye=camera.transform.position;
            var rotation=camera.transform.rotation;float priorFov=camera.fieldOfView;var prior=camera.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(1600,1200,24,RenderTextureFormat.ARGB32){antiAliasing=4};var texture=new Texture2D(1600,1200,TextureFormat.RGBA32,false);
            try
            {
                camera.transform.rotation=Quaternion.LookRotation(look-eye,Vector3.up);camera.fieldOfView=fov;
                SkinFrame();camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1600,1200),0,0);texture.Apply();
                File.WriteAllBytes(Output+"/"+name+".png",texture.EncodeToPNG());
            }
            finally{camera.transform.rotation=rotation;camera.fieldOfView=priorFov;camera.targetTexture=prior;RenderTexture.active=active;target.Release();Object.Destroy(target);Object.Destroy(texture);}
        }
    }
}

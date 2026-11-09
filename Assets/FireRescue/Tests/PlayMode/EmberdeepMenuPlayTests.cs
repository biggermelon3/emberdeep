using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PhysicalAssembly;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    // Synthetic system pointer poses and joint distances use the production collider/input path.
    // These Editor tests do not substitute for headset tracking or comfort verification.
    public sealed class EmberdeepMenuPlayTests
    {
        const string SoundKey="Emberdeep.Sound";
        const float Open=.04f,Closed=.01f,Dt=.02f;
        EmberdeepCamp camp;
        bool hadSoundPreference;
        int soundPreference;
        float audioVolume;
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;

        [UnitySetUp] public IEnumerator LoadPlacedCamp()
        {
            hadSoundPreference=PlayerPrefs.HasKey(SoundKey);
            soundPreference=PlayerPrefs.GetInt(SoundKey,1);audioVolume=AudioListener.volume;
            background=InputSystem.settings.backgroundBehavior;
            editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            EmberdeepSession.NewExpedition();
            yield return SceneManager.LoadSceneAsync(EmberdeepSession.CampScene);
            yield return null;yield return null;
            camp=Object.FindFirstObjectByType<EmberdeepCamp>();Assert.NotNull(camp);
            camp.SendMessage("OnApplicationFocus",true);
            yield return WaitPlaced();
            // Prevent LateUpdate's desktop input from replacing the injected joint samples.
            camp.enabled=false;Physics.SyncTransforms();
        }

        [TearDown] public void RestorePreferencesAndInput()
        {
            try
            {
                if(hadSoundPreference)PlayerPrefs.SetInt(SoundKey,soundPreference);
                else PlayerPrefs.DeleteKey(SoundKey);
                PlayerPrefs.Save();
            }
            finally
            {
                AudioListener.volume=audioVolume;
                InputSystem.settings.backgroundBehavior=background;
                InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
                if(camp)camp.enabled=false;
            }
        }

        [UnityTest] public IEnumerator ARealButtonRequiresAHandRayInsteadOfHeadGaze()
        {
            camp.ShowPage(EmberdeepCamp.MenuPage.Main);
            Capture("Logs/Emberdeep-Menu-Main.png");
            var button=Button("Settings");var ray=RayTo(button);
            for(int i=0;i<10;i++)camp.ProcessMenuInput(ray,true,false,float.PositiveInfinity,false,float.PositiveInfinity,Dt);
            Assert.IsNull(camp.HoveredButton);Assert.IsFalse(button.IsHovered,"Head direction alone must not select a hand target");
            Assert.IsFalse(button.transform.parent.Find("Settings ray outline").gameObject.activeSelf);
            Assert.AreEqual(EmberdeepCamp.MenuPage.Main,camp.ActivePage);
            Assert.IsTrue(camp.GetComponentsInChildren<TextMesh>().Any(t=>t.text.Contains("SHOW AN OPEN HAND")));
            for(int i=0;i<10;i++)camp.ProcessMenuInput(ray,true,true,Open,false,float.PositiveInfinity,Dt);
            Assert.AreSame(button,camp.HoveredButton);Assert.IsTrue(button.IsHovered);
            Assert.IsTrue(button.transform.parent.Find("Settings ray outline").gameObject.activeSelf);
            Assert.IsTrue(camp.GetComponentsInChildren<LineRenderer>().Any(l=>l.enabled && l.name.Contains("contextual hand ray")));
            Capture("Logs/Emberdeep-Menu-Hover.png");
            var miss=new Ray(ray.origin,Vector3.up);
            camp.ProcessMenuInput(miss,true,true,Open,false,float.PositiveInfinity,Dt);
            Assert.IsNull(camp.HoveredButton);Assert.IsFalse(button.IsHovered);
            Assert.IsFalse(camp.GetComponentsInChildren<LineRenderer>().Any(l=>l.enabled && l.name.Contains("contextual hand ray")),"No idle ray after leaving an interactable");
            camp.ProcessMenuInput(ray,false,true,Closed,true,Closed,Dt);
            Assert.IsNull(camp.HoveredButton);Assert.IsFalse(button.IsHovered);
            yield return null;
        }

        [UnityTest] public IEnumerator LeftHandNavigatesSettingsAndQuitWithoutHeldPinchClickThrough()
        {yield return NavigatePages(true);}

        [UnityTest] public IEnumerator RightHandNavigatesSettingsAndQuitWithoutHeldPinchClickThrough()
        {yield return NavigatePages(false);}

        [UnityTest] public IEnumerator LeftSystemPointerClicksVisibleButtonAwayFromHeadCenter()
        {yield return HandRaySelection(true);}

        [UnityTest] public IEnumerator RightSystemPointerClicksVisibleButtonAwayFromHeadCenter()
        {yield return HandRaySelection(false);}

        IEnumerator HandRaySelection(bool left)
        {
            var button=Button("Settings");var view=camp.Player.ViewCamera;var camera=view.transform;
            var oldRotation=camera.rotation;float oldFov=view.fieldOfView,oldAspect=view.aspect;
            Vector3 origin=camera.position+camera.right*(left?-.18f:.18f)-camera.up*.12f+camera.forward*.2f;
            var pointer=new Pose(origin,Quaternion.LookRotation(button.transform.position-origin));
            var tracker=new EmberdeepHandRay();
            var miss=new EmberdeepHandRay.Sample(true,new Ray(origin,Vector3.up),Open);
            // Keep the target in peripheral view, outside the head-center ray.
            // An offscreen target is deliberately ineligible in the current interaction.
            view.fieldOfView=90;view.aspect=1;camera.rotation=Quaternion.Euler(0,30,0);
            try
            {
                var viewport=view.WorldToViewportPoint(button.transform.position);
                Assert.That(viewport.x,Is.InRange(0f,1f));Assert.That(viewport.y,Is.InRange(0f,1f));Assert.Greater(viewport.z,0);
                Assert.IsFalse(button.GetComponent<Collider>().Raycast(new Ray(camera.position,camera.forward),out _,3),"The head-center ray does not aim at this button");
                for(int i=0;i<8;i++)
                {
                    var pointed=tracker.FromPointerPose(true,pointer,Open);
                    camp.ProcessHandMenuInput(left?pointed:miss,left?miss:pointed,true,Dt);
                }
                Assert.AreSame(button,camp.HoveredButton);Assert.IsTrue(button.IsHovered);
                miss.Pinch=Closed;
                var open=tracker.FromPointerPose(true,pointer,Open);
                camp.ProcessHandMenuInput(left?open:miss,left?miss:open,true,Dt);
                Assert.AreEqual(EmberdeepCamp.MenuPage.Main,camp.ActivePage,"The other hand cannot click this ray's target");
                // Small pointer movement during pinch remains on the intended button;
                // the curling fingertip is no longer an aiming input.
                pointer.position-=Vector3.up*.018f;
                var closed=tracker.FromPointerPose(true,pointer,Closed);
                camp.ProcessHandMenuInput(left?closed:miss,left?miss:closed,true,Dt);
                Assert.AreEqual(EmberdeepCamp.MenuPage.Settings,camp.ActivePage);
                Assert.IsFalse(camp.MenuInput.OwnsInput,"Opening a modal clears both hands' ownership");
            }
            finally{camera.rotation=oldRotation;view.fieldOfView=oldFov;view.aspect=oldAspect;}
            yield return null;
        }

        [UnityTest] public IEnumerator LostPointingHandCannotBeReplacedByOtherPinchAndRecoveryMustReopen()
        {
            var ray=RayTo(Button("Settings"));
            var left=new EmberdeepHandRay.Sample(true,ray,Open);
            var right=new EmberdeepHandRay.Sample(true,new Ray(ray.origin,Vector3.up),Open);
            for(int i=0;i<8;i++)camp.ProcessHandMenuInput(left,right,true,Dt);
            left.Valid=false;right.Pinch=Closed;
            camp.ProcessHandMenuInput(left,right,true,Dt);
            Assert.IsNull(camp.HoveredButton);Assert.AreEqual(EmberdeepCamp.MenuPage.Main,camp.ActivePage);
            left.Valid=true;left.Pinch=Closed;
            for(int i=0;i<10;i++)camp.ProcessHandMenuInput(left,right,true,Dt);
            Assert.AreEqual(EmberdeepCamp.MenuPage.Main,camp.ActivePage);
            left.Pinch=Open;for(int i=0;i<8;i++)camp.ProcessHandMenuInput(left,right,true,Dt);
            left.Pinch=Closed;camp.ProcessHandMenuInput(left,right,true,Dt);
            Assert.AreEqual(EmberdeepCamp.MenuPage.Settings,camp.ActivePage);
            yield return null;
        }

        IEnumerator NavigatePages(bool left)
        {
            Select("Settings",left);
            Assert.AreEqual(EmberdeepCamp.MenuPage.Settings,camp.ActivePage);
            Capture("Logs/Emberdeep-Menu-Settings.png");
            HoldOn("Back",left);
            Assert.AreEqual(EmberdeepCamp.MenuPage.Settings,camp.ActivePage,"Held entry pinch must not press Back");
            Select("Back",left);Assert.AreEqual(EmberdeepCamp.MenuPage.Main,camp.ActivePage);
            Select("Quit",left);Assert.AreEqual(EmberdeepCamp.MenuPage.QuitConfirmation,camp.ActivePage);
            Capture("Logs/Emberdeep-Menu-Quit.png");
            HoldOn("Yes, Quit",left);Assert.IsFalse(camp.QuitRequested,"Modal cannot reuse the opening pinch");
            Select("Cancel",left);
            Assert.AreEqual(EmberdeepCamp.MenuPage.Main,camp.ActivePage);Assert.IsFalse(camp.QuitRequested);
            yield return null;
        }

        [UnityTest] public IEnumerator SoundToggleUsesPointingHandPinchAndHeldInputDoesNotToggleAgain()
        {
            bool initial=camp.SoundEnabled;
            Select("Settings",true);Select("Sound",false);
            Assert.AreEqual(!initial,camp.SoundEnabled);
            Assert.That(AudioListener.volume,Is.EqualTo(initial?0:1).Within(.0001f));
            Assert.AreEqual(initial?0:1,PlayerPrefs.GetInt(SoundKey));
            HoldOn("Sound",false);Assert.AreEqual(!initial,camp.SoundEnabled);
            // WorkshopButton debounces repeated presses using real unscaled time.
            yield return new WaitForSecondsRealtime(.5f);
            Select("Sound",true);Assert.AreEqual(initial,camp.SoundEnabled);
        }

        [UnityTest] public IEnumerator RecenterUsesFreshEyeHeightAndHeadingThenRemainsAnchored()
        {
            var view=camp.Player.ViewCamera.transform;
            float floor=camp.Player.FloorHeight;
            var positions=new[]{new Vector3(.3f,floor+.95f,-.2f),new Vector3(-.4f,floor+1.8f,.25f)};
            var rotations=new[]{Quaternion.Euler(18,-105,0),Quaternion.Euler(-12,67,0)};
            for(int i=0;i<positions.Length;i++)
            {
                view.SetPositionAndRotation(positions[i],rotations[i]);
                camp.Recenter();Assert.IsFalse(camp.IsPlaced);Assert.IsFalse(camp.MenuRoot.gameObject.activeSelf);
                camp.enabled=true;yield return null;
                Assert.IsFalse(camp.IsPlaced,"One observation cannot establish fresh placement");
                yield return WaitPlaced();camp.enabled=false;
                var heading=Vector3.ProjectOnPlane(view.forward,Vector3.up).normalized;
                Assert.That(camp.MenuRoot.position.y,Is.EqualTo(view.position.y).Within(.001f));
                Assert.That(Vector3.Distance(camp.MenuRoot.position,view.position+heading*EmberdeepMenuPose.PanelDistance),Is.LessThan(.002f));
                Assert.That(Vector3.Angle(camp.MenuRoot.forward,heading),Is.LessThan(.1f));
                var anchoredPosition=camp.MenuRoot.position;var anchoredRotation=camp.MenuRoot.rotation;
                view.position+=Vector3.right*.2f;view.rotation=Quaternion.Euler(0,25,0);
                camp.enabled=true;yield return new WaitForSecondsRealtime(.12f);camp.enabled=false;
                Assert.That(Vector3.Distance(camp.MenuRoot.position,anchoredPosition),Is.LessThan(.001f));
                Assert.That(Quaternion.Angle(camp.MenuRoot.rotation,anchoredRotation),Is.LessThan(.1f));
            }
        }

        [UnityTest] public IEnumerator ConfirmQuitRequiresModalAndIsAnEditorNoOpAfterRequest()
        {
            if(!Application.isEditor)Assert.Ignore("Application.Quit must not terminate a player test runner");
            camp.ConfirmQuit();Assert.IsFalse(camp.QuitRequested);
            Select("Quit",true);Select("Yes, Quit",false);
            Assert.IsTrue(camp.QuitRequested);Assert.IsFalse(camp.Transitioning);
            yield return null;
            Assert.AreEqual(EmberdeepSession.CampScene,SceneManager.GetActiveScene().name);
            Assert.IsTrue(camp.gameObject.activeInHierarchy);
            camp.ConfirmQuit();Assert.IsTrue(camp.QuitRequested);
        }

        WorkshopButton Button(string command)=>camp.GetComponentsInChildren<WorkshopButton>().Single(b=>b.Command==command);
        Ray RayTo(WorkshopButton button)
        {
            Physics.SyncTransforms();var origin=camp.Player.ViewCamera.transform.position;
            var ray=new Ray(origin,button.transform.position-origin);
            Assert.IsTrue(button.GetComponent<Collider>().Raycast(ray,out _,3),"Production button collider must be reachable");
            return ray;
        }
        void Select(string command,bool left)
        {
            var ray=RayTo(Button(command));
            for(int i=0;i<8;i++)camp.ProcessMenuInput(ray,true,left,Open,!left,Open,Dt);
            camp.ProcessMenuInput(ray,true,left,Closed,!left,Closed,Dt);
        }
        void HoldOn(string command,bool left)
        {
            var ray=RayTo(Button(command));
            for(int i=0;i<10;i++)camp.ProcessMenuInput(ray,true,left,Closed,!left,Closed,Dt);
        }
        IEnumerator WaitPlaced()
        {
            float deadline=Time.realtimeSinceStartup+10;int startFrame=Time.frameCount;
            while(!camp.IsPlaced && Time.realtimeSinceStartup<deadline)yield return null;
            object Flag(string name)=>typeof(EmberdeepCamp).GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(camp);
            Assert.IsTrue(camp.IsPlaced,$"Menu failed to acquire stable placement: frames={Time.frameCount-startFrame}, focused={Flag("focused")}, paused={Flag("paused")}, enabled={camp.enabled}, xr={camp.Player.IsXR}, eye={camp.Player.ViewCamera.transform.position}, floor={camp.Player.FloorHeight}, dt={Time.unscaledDeltaTime}");
            yield return null;
        }
        void Capture(string path)
        {
            var camera=camp.Player.ViewCamera;var target=new RenderTexture(1920,1080,24);
            var prior=camera.targetTexture;var active=RenderTexture.active;
            var pixels=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1920,1080),0,0);pixels.Apply();
                Directory.CreateDirectory("Logs");File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=prior;RenderTexture.active=active;
                Object.Destroy(pixels);Object.Destroy(target);
            }
        }
    }
}

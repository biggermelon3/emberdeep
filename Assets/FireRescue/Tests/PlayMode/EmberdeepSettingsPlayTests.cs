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
    // Saves and restores every §10 option so tests never leave the editor's PlayerPrefs changed.
    public sealed class EmberdeepSettingsPrefs
    {
        static readonly string[] FloatKeys={EmberdeepPlayArea.HeightKey,EmberdeepPlayArea.DistanceKey,EmberdeepAudioSettings.MusicKey,EmberdeepAudioSettings.SfxKey};
        static readonly string[] IntKeys={EmberdeepWaterHand.PrefsKey,EmberdeepAudioSettings.LegacySoundKey,EmberdeepPlayArea.TiltedKey};
        readonly float?[] floats=new float?[FloatKeys.Length];readonly int?[] ints=new int?[IntKeys.Length];float listener;
        public static EmberdeepSettingsPrefs Save()
        {
            var s=new EmberdeepSettingsPrefs{listener=AudioListener.volume};
            for(int i=0;i<FloatKeys.Length;i++)s.floats[i]=PlayerPrefs.HasKey(FloatKeys[i])?PlayerPrefs.GetFloat(FloatKeys[i]):null;
            for(int i=0;i<IntKeys.Length;i++)s.ints[i]=PlayerPrefs.HasKey(IntKeys[i])?PlayerPrefs.GetInt(IntKeys[i]):null;
            return s;
        }
        public void Restore()
        {
            for(int i=0;i<FloatKeys.Length;i++)if(floats[i].HasValue)PlayerPrefs.SetFloat(FloatKeys[i],floats[i].Value);else PlayerPrefs.DeleteKey(FloatKeys[i]);
            for(int i=0;i<IntKeys.Length;i++)if(ints[i].HasValue)PlayerPrefs.SetInt(IntKeys[i],ints[i].Value);else PlayerPrefs.DeleteKey(IntKeys[i]);
            PlayerPrefs.Save();EmberdeepPlayArea.Reload();EmberdeepAudioSettings.Reload();EmberdeepWaterHand.Reload();AudioListener.volume=listener;
        }
    }

    // LoopSpec §10 options page in camp (workstream O), through the production hand-ray path (synthetic rays and pinch
    // spans): drag tracks (a pinch grabs, the value saves on release), -/+ steppers, the water hand pair (by colour),
    // separate volumes. Writes the page captures to
    // Logs/EmberdeepBoardFit/after/.
    public sealed class EmberdeepSettingsPlayTests
    {
        const float Open=.04f,Closed=.01f,Dt=.02f;
        const string Folder="Logs/EmberdeepBoardFit/after";
        EmberdeepCamp camp;EmberdeepSettingsPrefs saved;
        InputSettings.BackgroundBehavior background;InputSettings.EditorInputBehaviorInPlayMode editorInput;

        [UnitySetUp] public IEnumerator LoadCampSettings()
        {
            saved=EmberdeepSettingsPrefs.Save();
            foreach(var key in new[]{EmberdeepPlayArea.HeightKey,EmberdeepPlayArea.DistanceKey,EmberdeepPlayArea.TiltedKey,EmberdeepAudioSettings.MusicKey,EmberdeepAudioSettings.SfxKey,EmberdeepWaterHand.PrefsKey,EmberdeepAudioSettings.LegacySoundKey})
                PlayerPrefs.DeleteKey(key);
            EmberdeepPlayArea.Reload();EmberdeepAudioSettings.Reload();EmberdeepWaterHand.Reload();
            background=InputSystem.settings.backgroundBehavior;editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            EmberdeepSession.NewExpedition();
            yield return SceneManager.LoadSceneAsync(EmberdeepSession.CampScene);yield return null;yield return null;
            camp=Object.FindFirstObjectByType<EmberdeepCamp>();Assert.NotNull(camp);camp.SendMessage("OnApplicationFocus",true);
            float deadline=Time.realtimeSinceStartup+10;while(!camp.IsPlaced && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(camp.IsPlaced);camp.enabled=false;Physics.SyncTransforms();
            Select("Settings");Assert.AreEqual(EmberdeepCamp.MenuPage.Settings,camp.ActivePage);
        }
        [TearDown] public void Restore()
        {
            saved?.Restore();
            InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
            if(camp)camp.enabled=false;
        }

        [UnityTest] public IEnumerator EveryOptionIsAHandRayControlWithAnIconAndAShortLabel()
        {
            string[] commands={"Height track","Height down","Height up","Distance track","Distance down","Distance up","Board flat","Board tilted","Water hand left","Water hand right",
                "Music track","Music down","Music up","Sound track","Sound down","Sound up","Recenter Menu","Show hints","Back"};
            foreach(var command in commands)
            {
                var b=Button(command);var box=b.GetComponent<BoxCollider>();Assert.NotNull(box,command);Assert.IsTrue(box.isTrigger);
                var size=Vector3.Scale(box.size,b.transform.lossyScale);
                Assert.GreaterOrEqual(Mathf.Min(size.x,size.y),.03f-1e-4f,command+": targets of at least 3 cm (§11b)");
                RayTo(b);
            }
            var page=camp.MenuRoot.Find("Settings");
            foreach(var icon in new[]{"HEIGHT icon","DISTANCE icon","TILT icon","WATER HAND icon","MUSIC icon","SOUND icon","Height down icon","Height up icon","Recenter Menu icon","Back icon"})
                Assert.NotNull(page.Find(icon),icon);
            var words=page.GetComponentsInChildren<TextMesh>().Select(t=>t.text).ToArray();
            foreach(var word in new[]{"HEIGHT","DISTANCE","TILT","FLAT","TILTED","WATER HAND","LEFT","RIGHT","MUSIC","SOUND","RECENTER","SHOW HINTS","BACK"})Assert.Contains(word,words);
            var english=File.ReadAllLines("Assets/FireRescue/Localization/Translations.tsv").Select(l=>l.Split('\t')[0].ToUpperInvariant()).ToHashSet();
            foreach(var word in new[]{"SETTINGS","HEIGHT","DISTANCE","TILT","FLAT","TILTED","WATER HAND","LEFT","RIGHT","MUSIC","SOUND","RECENTER","SHOW HINTS","BACK"})
                Assert.IsTrue(english.Contains(word),word+" has a zh-Hans row in Translations.tsv");
            Directory.CreateDirectory(Folder);Capture(Folder+"/options-page.png");
            yield return null;
        }

        [UnityTest] public IEnumerator TracksSetWherePinchedAndSteppersNudge()
        {
            // Headset test 2 defaults (-14 / +6 cm) sit on the default ticks; the readout is the offset from the default.
            Assert.AreEqual(EmberdeepPlayArea.DefaultHeight,EmberdeepPlayArea.Height,1e-5f);Assert.AreEqual(EmberdeepPlayArea.DefaultHeight01,camp.SliderKnob01("Height"),.01f,"knob on the default");
            Assert.AreEqual(EmberdeepPlayArea.DefaultDistance01,camp.SliderKnob01("Distance"),.01f);
            var page=camp.MenuRoot.Find("Settings");
            Assert.AreEqual(EmberdeepPlayArea.DefaultHeight01,Mathf.InverseLerp(EmberdeepCamp.TrackLeft,EmberdeepCamp.TrackRight,page.Find("Height default tick").localPosition.x),.01f,"tick on the default");
            Assert.AreEqual(2,page.GetComponentsInChildren<TextMesh>().Count(t=>t.text=="0 cm"),"both defaults read 0 cm");
            PinchTrack("Height",1);Assert.AreEqual(EmberdeepPlayArea.HeightMax,EmberdeepPlayArea.Height,.011f);Assert.AreEqual(1,camp.SliderKnob01("Height"),.05f);
            Assert.AreEqual(EmberdeepPlayArea.HeightMax,PlayerPrefs.GetFloat(EmberdeepPlayArea.HeightKey),.011f,"stored");
            yield return new WaitForSecondsRealtime(.5f);
            PinchTrack("Height",.25f);Assert.AreEqual(Mathf.Lerp(EmberdeepPlayArea.HeightMin,EmberdeepPlayArea.HeightMax,.25f),EmberdeepPlayArea.Height,.011f);
            Select("Distance up");Assert.AreEqual(EmberdeepPlayArea.DefaultDistance+.02f,EmberdeepPlayArea.Distance,1e-4f);
            Assert.IsTrue(page.GetComponentsInChildren<TextMesh>().Any(t=>t.text=="+2 cm"),"offset from the default");
            HoldOn("Distance up");Assert.AreEqual(EmberdeepPlayArea.DefaultDistance+.02f,EmberdeepPlayArea.Distance,1e-4f,"a held pinch steps once");
            yield return new WaitForSecondsRealtime(.5f);
            Select("Distance down",false);Assert.AreEqual(EmberdeepPlayArea.DefaultDistance,EmberdeepPlayArea.Distance,1e-4f,"either hand");
            PinchTrack("Music",0);Assert.AreEqual(0,EmberdeepAudioSettings.Music,1e-4f);
            PinchTrack("Sound",.25f);Assert.AreEqual(.25f,EmberdeepAudioSettings.Sfx,.026f);Assert.AreEqual(EmberdeepAudioSettings.Sfx,AudioListener.volume,1e-4f,"effects = listener volume");
            Assert.AreEqual(0,EmberdeepAudioSettings.Music,1e-4f,"music stays separate");
            Assert.IsTrue(camp.MenuRoot.Find("Settings").GetComponentsInChildren<TextMesh>().Any(t=>t.text==Mathf.RoundToInt(EmberdeepAudioSettings.Sfx*100)+"%"));
            yield return null;
        }

        [UnityTest] public IEnumerator WaterHandPairSwitchesTheStoredHandAndShowsTheChoice()
        {
            Assert.IsTrue(EmberdeepWaterHand.Left,"default left");
            var page=camp.MenuRoot.Find("Settings");
            Assert.IsTrue(camp.OptionChosen("Water hand left"));Assert.IsFalse(camp.OptionChosen("Water hand right"));
            Select("Water hand right");Assert.IsFalse(EmberdeepWaterHand.Left);Assert.AreEqual(EmberdeepWaterHand.RightValue,PlayerPrefs.GetInt(EmberdeepWaterHand.PrefsKey));
            Assert.IsFalse(camp.OptionChosen("Water hand left"));Assert.IsTrue(camp.OptionChosen("Water hand right"));
            Directory.CreateDirectory(Folder);Capture(Folder+"/options-page-right-hand.png");
            yield return new WaitForSecondsRealtime(.5f);
            Select("Water hand left",false);Assert.IsTrue(EmberdeepWaterHand.Left);
        }

        [UnityTest] public IEnumerator TiltPairStoresTheChoiceAndShowsIt()
        {
            Assert.IsTrue(EmberdeepPlayArea.Tilted,"tilted by default (headset test 4)");
            var page=camp.MenuRoot.Find("Settings");
            Assert.IsFalse(camp.OptionChosen("Board flat"));Assert.IsTrue(camp.OptionChosen("Board tilted"));
            int changes=0;void Count()=>changes++;EmberdeepPlayArea.Changed+=Count;
            try
            {
                Select("Board flat");Assert.IsFalse(EmberdeepPlayArea.Tilted);Assert.AreEqual(0,PlayerPrefs.GetInt(EmberdeepPlayArea.TiltedKey,1));Assert.AreEqual(1,changes);
                Assert.AreEqual(0,EmberdeepPlayArea.TiltDegrees,1e-5f);
                Assert.IsTrue(camp.OptionChosen("Board flat"));Assert.IsFalse(camp.OptionChosen("Board tilted"));
                Directory.CreateDirectory(Folder);Capture(Folder+"/options-page-flat.png");
                yield return new WaitForSecondsRealtime(.5f);
                // A change from elsewhere (the pause panel) shows here too.
                EmberdeepPlayArea.SetTilted(true);Assert.AreEqual(2,changes);
                Assert.IsFalse(camp.OptionChosen("Board flat"));Assert.IsTrue(camp.OptionChosen("Board tilted"));
                Assert.AreEqual(EmberdeepBoardLayout.TiltDegrees,EmberdeepPlayArea.TiltDegrees,1e-5f);
                Select("Board tilted",false);Assert.AreEqual(2,changes,"no event without a change");
            }
            finally{EmberdeepPlayArea.Changed-=Count;}
        }

        // SHOW HINTS (for the rim/hints session): brings every first-time hint back; dimmed while none was used up.
        [UnityTest] public IEnumerator ShowHintsBringsTheFirstTimeHintsBack()
        {
            var moves=(EmberdeepFirstHints.Move[])System.Enum.GetValues(typeof(EmberdeepFirstHints.Move));
            var before=moves.Select(m=>PlayerPrefs.HasKey(EmberdeepFirstHints.Key(m))?(int?)EmberdeepFirstHints.Count(m):null).ToArray();
            try
            {
                EmberdeepFirstHints.ResetAll();yield return null;
                Assert.IsTrue(EmberdeepFirstHints.AllFresh);Assert.IsTrue(camp.ShowHintsDimmed,"nothing to bring back");
                foreach(var m in moves)PlayerPrefs.SetInt(EmberdeepFirstHints.Key(m),EmberdeepFirstHints.Shows);
                camp.RefreshSettings();Assert.IsFalse(EmberdeepFirstHints.AllFresh);Assert.IsFalse(camp.ShowHintsDimmed);
                Select("Show hints");Assert.IsTrue(EmberdeepFirstHints.AllFresh,"every hint shows again");Assert.IsTrue(camp.ShowHintsDimmed);
                Directory.CreateDirectory(Folder);Capture(Folder+"/options-page-hints.png");
            }
            finally
            {
                for(int i=0;i<moves.Length;i++)if(before[i].HasValue)PlayerPrefs.SetInt(EmberdeepFirstHints.Key(moves[i]),before[i].Value);else PlayerPrefs.DeleteKey(EmberdeepFirstHints.Key(moves[i]));
                PlayerPrefs.Save();
            }
        }

        [UnityTest] public IEnumerator OptionsPageReadsInChinese()
        {
            var locale=LocalizationSettings.SelectedLocale;var zh=LocalizationSettings.AvailableLocales.GetLocale("zh-Hans");Assert.NotNull(zh);
            try
            {
                LocalizationSettings.SelectedLocale=zh;var localized=camp.GetComponent<EmberdeepLocalizedText>();
                float deadline=Time.realtimeSinceStartup+10;
                while(Time.realtimeSinceStartup<deadline && !(localized.Ready && localized.LocaleCode=="zh-Hans"))yield return null;
                localized.RefreshLabels();yield return null;
                var words=camp.MenuRoot.Find("Settings").GetComponentsInChildren<TextMesh>().Select(t=>t.text).ToArray();
                foreach(var word in new[]{"高度","距离","倾角","平放","斜放","喷水手","左手","右手","音乐","音效","重置位置","返回"})Assert.Contains(word,words);
                Directory.CreateDirectory(Folder);Capture(Folder+"/options-page-zh.png");
            }
            finally{LocalizationSettings.SelectedLocale=locale;}
            yield return null;
        }

        WorkshopButton Button(string command)=>camp.GetComponentsInChildren<WorkshopButton>().Single(b=>b.Command==command);
        Ray RayTo(WorkshopButton button)=>RayToPoint(button,button.transform.position);
        Ray RayToPoint(WorkshopButton button,Vector3 point)
        {
            Physics.SyncTransforms();var origin=camp.Player.ViewCamera.transform.position;var ray=new Ray(origin,point-origin);
            Assert.IsTrue(button.GetComponent<Collider>().Raycast(ray,out _,3),button.Command+": production collider must be reachable");
            return ray;
        }
        void Press(Ray ray,bool left)
        {
            for(int i=0;i<8;i++)camp.ProcessMenuInput(ray,true,left,Open,!left,Open,Dt);
            camp.ProcessMenuInput(ray,true,left,Closed,!left,Closed,Dt);
        }
        void Select(string command,bool left=true)=>Press(RayTo(Button(command)),left);
        void HoldOn(string command){var ray=RayTo(Button(command));for(int i=0;i<10;i++)camp.ProcessMenuInput(ray,true,true,Closed,false,Closed,Dt);}
        // Pinch the track at t (0 = left end, 1 = right end of the knob's travel).
        void PinchTrack(string name,float t)
        {
            var track=Button(name+" track");var page=track.transform.parent;var local=track.transform.localPosition;
            local.x=Mathf.Lerp(EmberdeepCamp.TrackLeft,EmberdeepCamp.TrackRight,t);local.z-=.0125f;
            var ray=RayToPoint(track,page.TransformPoint(local));Press(ray,true);
            // Headset test 3: the pinch grabs the slider; the value is saved on release.
            Assert.IsTrue(camp.SliderHeld(name),name+" grabbed");
            for(int i=0;i<3;i++)camp.ProcessMenuInput(ray,true,true,Open,false,Open,Dt);
            Assert.IsFalse(camp.SliderHeld(name),name+" released");
        }
        void Capture(string path)
        {
            var camera=camp.Player.ViewCamera;var target=new RenderTexture(1600,1200,24){antiAliasing=4};
            var prior=camera.targetTexture;var active=RenderTexture.active;float fov=camera.fieldOfView;var rotation=camera.transform.rotation;
            var pixels=new Texture2D(1600,1200,TextureFormat.RGB24,false);
            try
            {
                camera.transform.rotation=Quaternion.LookRotation(camp.MenuRoot.position-camera.transform.position,Vector3.up);camera.fieldOfView=50;
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1600,1200),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally{camera.targetTexture=prior;RenderTexture.active=active;camera.fieldOfView=fov;camera.transform.rotation=rotation;target.Release();Object.Destroy(target);Object.Destroy(pixels);}
        }
    }
}

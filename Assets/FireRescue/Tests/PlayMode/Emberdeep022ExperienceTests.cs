using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    public sealed class Emberdeep022ExperienceTests
    {
        const string Output="Logs/Emberdeep024/Captures";
        static IEnumerator Localized(GameObject root,string code)
        {
            yield return LocalizationSettings.InitializationOperation;
            LocalizationSettings.SelectedLocale=LocalizationSettings.AvailableLocales.GetLocale(code);
            var adapter=root.GetComponent<EmberdeepLocalizedText>();float until=Time.realtimeSinceStartup+10;
            while((!adapter.Ready || adapter.LocaleCode!=code) && Time.realtimeSinceStartup<until)yield return null;
            Assert.True(adapter.Ready);for(int i=0;i<3;i++)yield return null;adapter.RefreshLabels();
        }
        static void Capture(Camera camera,string name)
        {
            Directory.CreateDirectory(Output);var prior=camera.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(1400,1000,24,RenderTextureFormat.ARGB32);var texture=new Texture2D(1400,1000,TextureFormat.RGBA32,false);
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1400,1000),0,0);texture.Apply();File.WriteAllBytes(Output+"/"+name+".png",texture.EncodeToPNG());}
            finally{camera.targetTexture=prior;RenderTexture.active=active;target.Release();Object.Destroy(target);Object.Destroy(texture);}
        }
        static void CheckChinese(GameObject root,string scope)
        {
            root.GetComponent<EmberdeepLocalizedText>().RefreshLabels();
            var texts=root.GetComponentsInChildren<TextMesh>().Where(t=>t.GetComponent<Renderer>().enabled).ToArray();
            var untranslated=texts.Where(t=>Regex.IsMatch(t.text,"[A-Za-z]{3,}")).Select(t=>t.text).ToArray();
            File.WriteAllLines(Output+"/"+scope+"-text.txt",texts.Select(t=>t.text));
            Assert.IsEmpty(untranslated,"Visible English remains: "+string.Join(" | ",untranslated));
            foreach(var text in texts)foreach(char ch in text.text)if(ch>='\u4e00' && ch<='\u9fff')Assert.True(text.font.HasCharacter(ch),"Missing glyph: "+ch);
        }
        [UnityTearDown] public IEnumerator ResetLanguage(){if(LocalizationSettings.HasSettings){LocalizationSettings.SelectedLocale=LocalizationSettings.AvailableLocales.GetLocale("en");yield return null;}}
        [TestCase(SystemLanguage.Chinese,"zh-Hans")] [TestCase(SystemLanguage.ChineseSimplified,"zh-Hans")]
        [TestCase(SystemLanguage.ChineseTraditional,"zh-Hans")] [TestCase(SystemLanguage.English,"en")]
        [TestCase(SystemLanguage.French,"en")] public void SystemLanguageSelectsSupportedLocale(SystemLanguage system,string expected)
            =>Assert.AreEqual(expected,EmberdeepSystemLocaleSelector.Code(system));
        [UnityTest] public IEnumerator ChineseAndEnglishRenderAndQuitRequiresExplicitConfirmation()
        {
            Directory.CreateDirectory(Output);EmberdeepSession.NewExpedition();yield return SceneManager.LoadSceneAsync(EmberdeepSession.CampScene);yield return null;
            var camp=Object.FindFirstObjectByType<EmberdeepCamp>();float until=Time.realtimeSinceStartup+10;
            while(!camp.IsPlaced && Time.realtimeSinceStartup<until)yield return null;
            yield return Localized(camp.gameObject,"en");Capture(camp.Player.ViewCamera,"Menu-English");
            yield return Localized(camp.gameObject,"zh-Hans");CheckChinese(camp.gameObject,"Menu-Chinese");Capture(camp.Player.ViewCamera,"Menu-Chinese");
            Assert.AreEqual(1,camp.ImmersiveSky.Progress);float clock=camp.ImmersiveSky.AnimationClock;int frame=Time.frameCount;
            for(int i=0;i<5;i++)yield return null;Assert.Greater(Time.frameCount,frame);Assert.Greater(camp.ImmersiveSky.AnimationClock,clock);
            camp.ShowPage(EmberdeepCamp.MenuPage.Settings);yield return null;CheckChinese(camp.gameObject,"Settings-Chinese");
            camp.ShowPage(EmberdeepCamp.MenuPage.QuitConfirmation);yield return null;CheckChinese(camp.gameObject,"Quit-Chinese");Capture(camp.Player.ViewCamera,"Quit-Chinese");
            Assert.False(camp.QuitRequested);camp.ShowPage(EmberdeepCamp.MenuPage.Main);
            yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);yield return null;yield return null;
            var game=Object.FindFirstObjectByType<FireRescueGame>();yield return Localized(game.gameObject,"zh-Hans");
            CheckChinese(game.gameObject,"Preparation-Chinese");Capture(game.Player.ViewCamera,"Preparation-Chinese");
            Assert.True(game.BeginIncident());yield return null;CheckChinese(game.gameObject,"Countdown-Chinese");Capture(game.Player.ViewCamera,"Countdown-Chinese");
            EmberdeepTestFlow.StartPrepared(game.Run);game.Refresh();yield return null;CheckChinese(game.gameObject,"Fireground-Chinese");Capture(game.Player.ViewCamera,"Fireground-Chinese");
            game.ConfirmQuit();Assert.False(game.QuitRequested,"Confirmation must be inert without the modal");
            game.RequestQuit();Assert.True(game.QuitConfirmationOpen);float elapsed=game.Run.Elapsed;int water=(int)game.Run.Water;
            for(int i=0;i<10;i++)yield return null;Assert.AreEqual(elapsed,game.Run.Elapsed);Assert.AreEqual(water,(int)game.Run.Water);
            Capture(game.Player.ViewCamera,"Game-Quit-Chinese");game.CancelQuit();Assert.False(game.QuitRequested);Assert.False(game.QuitConfirmationOpen);
            game.Run.ApplyWater(Vector3.zero,10000,100);game.Run.EvacuateSafeAnimals();game.OpenShop();yield return null;
            CheckChinese(game.gameObject,"Shop-Lesson-Chinese");Capture(game.Player.ViewCamera,"Shop-Lesson-Chinese");
            Assert.False(game.Run.NeedsMagicUpgrade,"Stage cut (T): no forced shop lesson");Assert.True(game.BuyShopSelection());
            for(int i=0;i<4;i++){game.SelectEquipment(i);yield return null;CheckChinese(game.gameObject,"Shop-"+i);}
            game.ShowShopPage(EmberdeepShopPage.Magic);yield return null;CheckChinese(game.gameObject,"Magic-Shop-Chinese");Capture(game.Player.ViewCamera,"Magic-Shop-Chinese");
            game.ShowShopPage(EmberdeepShopPage.Equipment);Capture(game.Player.ViewCamera,"Shop-Chinese");
            // LoopSpec §11b: the spell flow sells from upgrade tiles (was the gear shop's CASH line).
            yield return Localized(game.gameObject,"en");Assert.True(game.GetComponentsInChildren<TextMesh>().Any(t=>t.text.Contains("GEM")));
            game.CloseShop();game.RequestQuit();game.ConfirmQuit();Assert.True(game.QuitRequested);game.ReturnToMenu();yield return null;yield return null;
        }
        [UnityTest] public IEnumerator EveryVisualTutorialMovesAndHasNoInputColliders()
        {
            var root=new GameObject("Tutorial gallery"){layer=30};var cameraObject=new GameObject("Gallery camera");
            var camera=cameraObject.AddComponent<Camera>();camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.055f,.09f);
            camera.transform.position=new Vector3(0,.08f,-.55f);camera.transform.LookAt(new Vector3(0,-.025f,0));camera.fieldOfView=49;
            var demo=root.AddComponent<EmberdeepInteractionDemo>();
            foreach(EmberdeepInteractionDemo.Action mode in System.Enum.GetValues(typeof(EmberdeepInteractionDemo.Action)))
            {
                demo.Show(mode);foreach(var t in root.GetComponentsInChildren<Transform>())t.gameObject.layer=30;
                yield return null;
                Assert.False(root.GetComponentsInChildren<Collider>().Any(c=>c.enabled));
                var hand=root.GetComponentsInChildren<MeshFilter>().First(m=>m.sharedMesh.name=="Articulated tutorial hand");
                var before=hand.sharedMesh.vertices;for(int i=0;i<36;i++)demo.Animate(.08f);
                Assert.Greater(demo.AnimationTime,2);if(mode!=EmberdeepInteractionDemo.Action.Goddess)Assert.False(before.SequenceEqual(hand.sharedMesh.vertices));
                else Assert.True(root.GetComponentsInChildren<LineRenderer>().Any(l=>l.name=="Demonstrated water" && l.enabled));
                Capture(camera,"Tutorial-"+mode);
            }
            Object.Destroy(root);Object.Destroy(cameraObject);yield return null;
        }
        [Test] public void BurningTreesAddBoundedUpdraftsAndSavedTreesRemoveThem()
        {
            var root=new GameObject("Updraft test");var cameraObject=new GameObject("View");
            try
            {
                var camera=cameraObject.AddComponent<Camera>();var fx=new GameObject("FX").AddComponent<EmberdeepEnvironmentVfx>();fx.Initialize(root.transform);
                var run=new FireRun();run.StartStage();fx.Tick(.1f,true,camera);int baseline=fx.TriangleCount;
                fx.Tick(.1f,true,camera,run);Assert.Greater(fx.TriangleCount,baseline);Assert.LessOrEqual(fx.TriangleCount,EmberdeepEnvironmentVfx.MaximumTriangles);
                run.ApplyWater(Vector3.zero,10000,100);fx.Tick(.1f,true,camera,run);Assert.AreEqual(baseline,fx.TriangleCount);
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(cameraObject);}
        }
    }
}

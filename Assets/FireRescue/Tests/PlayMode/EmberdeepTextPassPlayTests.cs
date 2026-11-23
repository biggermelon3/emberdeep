using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PhysicalAssembly;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    // LoopSpec §13 text pass (workstream X): every Emberdeep text screen from the seated desktop eye, in English and
    // Simplified Chinese. In zh-Hans no visible label may keep English words (phrase matching, Translations.tsv) and
    // every CJK glyph must be in the font. Captures and the visible text of each screen go to Logs/EmberdeepTextPass/.
    public sealed class EmberdeepTextPassPlayTests
    {
        const string Output="Logs/EmberdeepTextPass";
        const float SeatedEye=1.2f;
        static readonly string[] Locales={"en","zh-Hans"};
        int? tutorialSeen;int[] hints;

        [UnitySetUp] public IEnumerator SavePrefs()
        {
            tutorialSeen=PlayerPrefs.HasKey(EmberdeepCamp.TutorialPrefsKey)?PlayerPrefs.GetInt(EmberdeepCamp.TutorialPrefsKey):null;
            var moves=(EmberdeepFirstHints.Move[])System.Enum.GetValues(typeof(EmberdeepFirstHints.Move));
            hints=moves.Select(m=>PlayerPrefs.GetInt(EmberdeepFirstHints.Key(m),-1)).ToArray();
            Directory.CreateDirectory(Output);yield break;
        }
        [UnityTearDown] public IEnumerator RestorePrefs()
        {
            if(tutorialSeen.HasValue)PlayerPrefs.SetInt(EmberdeepCamp.TutorialPrefsKey,tutorialSeen.Value);else PlayerPrefs.DeleteKey(EmberdeepCamp.TutorialPrefsKey);
            var moves=(EmberdeepFirstHints.Move[])System.Enum.GetValues(typeof(EmberdeepFirstHints.Move));
            for(int i=0;i<moves.Length;i++)if(hints[i]<0)PlayerPrefs.DeleteKey(EmberdeepFirstHints.Key(moves[i]));else PlayerPrefs.SetInt(EmberdeepFirstHints.Key(moves[i]),hints[i]);
            PlayerPrefs.Save();
            if(LocalizationSettings.HasSettings){LocalizationSettings.SelectedLocale=LocalizationSettings.AvailableLocales.GetLocale("en");yield return null;}
        }

        static IEnumerator Localize(GameObject root,string code)
        {
            yield return LocalizationSettings.InitializationOperation;
            LocalizationSettings.SelectedLocale=LocalizationSettings.AvailableLocales.GetLocale(code);
            var adapter=root.GetComponent<EmberdeepLocalizedText>();float until=Time.realtimeSinceStartup+10;
            while((!adapter.Ready || adapter.LocaleCode!=code) && Time.realtimeSinceStartup<until)yield return null;
            Assert.True(adapter.Ready);for(int i=0;i<3;i++)yield return null;adapter.RefreshLabels();yield return null;
        }
        // The visible labels of one screen; in zh-Hans none may keep an English word (3+ letters: "cm", "x3" are fine).
        static void Check(GameObject root,string code,string screen)
        {
            root.GetComponent<EmberdeepLocalizedText>().RefreshLabels();
            var texts=root.GetComponentsInChildren<TextMesh>().Where(t=>t.GetComponent<Renderer>().enabled && t.text.Trim().Length>0).ToArray();
            File.WriteAllLines($"{Output}/{screen}-{code}-text.txt",texts.Select(t=>t.text.Replace("\n"," / ")));
            if(code=="en")return;
            var english=texts.Where(t=>Regex.IsMatch(t.text,"[A-Za-z]{3,}")).Select(t=>t.text).ToArray();
            Assert.IsEmpty(english,screen+": visible English in zh-Hans: "+string.Join(" | ",english));
            foreach(var t in texts)foreach(char ch in t.text)if(ch>='一' && ch<='鿿')Assert.True(t.font.HasCharacter(ch),screen+": missing glyph "+ch);
        }
        static void Capture(Camera camera,string name,Quaternion? look=null,float fov=-1)
        {
            var rotation=camera.transform.rotation;float priorFov=camera.fieldOfView;var prior=camera.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(1280,960,24,RenderTextureFormat.ARGB32){antiAliasing=4};var texture=new Texture2D(1280,960,TextureFormat.RGBA32,false);
            try
            {
                if(look.HasValue)camera.transform.rotation=look.Value;if(fov>0)camera.fieldOfView=fov;
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1280,960),0,0);texture.Apply();
                File.WriteAllBytes($"{Output}/{name}.png",texture.EncodeToPNG());
            }
            finally{camera.transform.rotation=rotation;camera.fieldOfView=priorFov;camera.targetTexture=prior;RenderTexture.active=active;target.Release();Object.Destroy(target);Object.Destroy(texture);}
        }
        static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
        static WorkshopButton Button(Component root,string command)=>root.GetComponentsInChildren<WorkshopButton>(true).Single(b=>b.Command==command);

        [UnityTest] public IEnumerator CampMenuOptionsQuitAndTutorialReadInBothLanguages()
        {
            EmberdeepSession.NewExpedition();
            yield return SceneManager.LoadSceneAsync(EmberdeepSession.CampScene);yield return null;
            var camp=Object.FindFirstObjectByType<EmberdeepCamp>();Assert.NotNull(camp);
            camp.Player.ViewCamera.transform.position=new Vector3(0,SeatedEye,0);
            camp.SendMessage("OnApplicationFocus",true);camp.SendMessage("OnApplicationPause",false);
            float deadline=Time.realtimeSinceStartup+10;while(!camp.IsPlaced && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(camp.IsPlaced);var view=camp.Player.ViewCamera;
            // The sign carries the name only; the main buttons show CAPS labels over stable commands.
            var labels=camp.MenuRoot.GetComponentsInChildren<TextMesh>(true).Select(t=>t.text).ToArray();
            foreach(var word in new[]{"NEW GAME","HOW TO PLAY","SETTINGS","QUIT"})Assert.Contains(word,labels);
            Assert.False(labels.Any(l=>l.Contains("EXPEDITION")),"No tagline");
            foreach(var code in Locales)
            {
                yield return Localize(camp.gameObject,code);
                camp.ShowPage(EmberdeepCamp.MenuPage.Main);yield return null;Check(camp.gameObject,code,"camp-menu");Capture(view,"camp-menu-"+code);
                camp.ShowPage(EmberdeepCamp.MenuPage.Settings);yield return null;Check(camp.gameObject,code,"options");Capture(view,"options-"+code);
                camp.ShowPage(EmberdeepCamp.MenuPage.QuitConfirmation);yield return null;Check(camp.gameObject,code,"quit");Capture(view,"quit-"+code);
                camp.ShowPage(EmberdeepCamp.MenuPage.Main);yield return null;
                if(camp.Play && camp.Play.Built)
                {
                    Button(camp,"How to play").Press();yield return Wait(1.2f);
                    Assert.AreEqual(EmberdeepCamp.MenuPage.Tutorial,camp.ActivePage);
                    Check(camp.gameObject,code,"tutorial");Capture(view,"tutorial-"+code);
                    camp.SkipTutorial();yield return null;Assert.AreEqual(EmberdeepCamp.MenuPage.Main,camp.ActivePage);
                }
            }
        }

        static void Clear(FireRun run){foreach(var a in run.Animals)run.ApplyAnimalWater(a.Position,100,.6f);foreach(var t in run.Trees)if(t.State==TreeState.Burning)run.ApplyWater(t.Position,1000,.1f);}
        static Quaternion Seat(FireRescueGame game,float pitch)=>Quaternion.LookRotation(game.Arena.forward,Vector3.up)*Quaternion.Euler(pitch,0,0);
        // From the seated eye, turned to one panel and zoomed (34 deg) so the words can be reviewed at capture size.
        static void Zoom(FireRescueGame game,string root,string name)
        {
            var panel=game.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==root);Assert.NotNull(panel,root);var camera=game.Player.ViewCamera;
            Capture(camera,name,Quaternion.LookRotation(panel.position-camera.transform.position,Vector3.up),34);
        }
        static IEnumerator ClearLevel(FireRun run)
        {
            EmberdeepTestFlow.StartPrepared(run);yield return null;
            for(int i=0;i<400 && run.Phase==FirePhase.Fighting;i++){Clear(run);run.Tick(.05f,false);}
            Assert.AreEqual(FirePhase.Cleared,run.Phase);run.EvacuateSafeAnimals();
        }

        [UnityTest] public IEnumerator FightBannerResultsUpgradesAndLevelCardReadInBothLanguages()
        {
            // First-time ghost hands retired, so they don't cover the cards in the captures (restored in TearDown).
            foreach(EmberdeepFirstHints.Move m in System.Enum.GetValues(typeof(EmberdeepFirstHints.Move)))PlayerPrefs.SetInt(EmberdeepFirstHints.Key(m),EmberdeepFirstHints.Shows);
            EmberdeepSession.NewExpedition();
            yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);yield return null;yield return null;
            var game=Object.FindFirstObjectByType<FireRescueGame>();game.SendMessage("OnApplicationFocus",true);
            yield return Wait(.3f);var run=game.Run;var view=game.Player.ViewCamera;
            Assert.AreEqual(FirePhase.Preparing,run.Phase);Assert.AreEqual(FireRun.LevelNames[0],run.StageName);
            // Plan has no text at all (§11b).
            foreach(var code in Locales){yield return Localize(game.gameObject,code);Check(game.gameObject,code,"plan");Capture(view,"plan-"+code,Seat(game,50),92);}
            // Cleared (still in zh-Hans from the Plan loop): the banner over the board, then it hands over to the results
            // panel, which would stand behind it from the seat. The English banner is shot on the next level's clear below.
            var outcome=game.Feedback.Outcome;
            yield return ClearLevel(run);yield return Wait(.5f);Assert.True(outcome.BannerVisible);Assert.False(game.ResultsVisible);
            Capture(view,"banner-zh-Hans",Seat(game,30),92);Zoom(game,"Outcome banner","banner-zoom-zh-Hans");
            yield return Wait(FireRescueGame.ResultsDelay+FireRescueGame.ResultsRise);Assert.True(game.ResultsVisible);
            Assert.False(outcome.BannerVisible,"The banner shrinks away as the results panel rises");
            foreach(var code in Locales)
            {
                yield return Localize(game.gameObject,code);Check(game.gameObject,code,"results");Capture(view,"results-"+code,Seat(game,14),92);
                Zoom(game,"Results panel","results-zoom-"+code);
            }
            Button(game,"Results upgrades").Press();yield return null;yield return null;Assert.True(game.UpgradesVisible);
            foreach(var code in Locales){yield return Localize(game.gameObject,code);Check(game.gameObject,code,"upgrades");Zoom(game,"Upgrades panel","upgrades-"+code);}
            Button(game,"Upgrades back").Press();yield return null;yield return null;
            // Level select: the next level's card (name + chip) over its dimmed board.
            Button(game,"Results next").Press();yield return null;yield return null;Assert.True(game.LevelSelectVisible);
            Assert.AreEqual(1,game.SelectedLevel);Assert.AreEqual(FireRun.LevelNames[1],FireRescueGame.LevelName(run,1));
            yield return Wait(.8f);
            foreach(var code in Locales)
            {
                yield return Localize(game.gameObject,code);Check(game.gameObject,code,"level-card");
                Capture(view,"level-card-"+code,Seat(game,12),92);Zoom(game,"Level select","level-card-zoom-"+code);
            }
            // Pause over the next level's Plan.
            Assert.True(game.PlaySelectedLevel());yield return null;Assert.True(game.OpenPause());yield return Wait(.4f);
            foreach(var code in Locales){yield return Localize(game.gameObject,code);Check(game.gameObject,code,"pause");Zoom(game,"Pause panel","pause-"+code);}
            game.ClosePause();yield return null;
            // The English banner on this level's clear, then the same hand-off.
            yield return Localize(game.gameObject,"en");
            yield return ClearLevel(run);yield return Wait(.5f);Assert.True(outcome.BannerVisible);
            Capture(view,"banner-en",Seat(game,30),92);Zoom(game,"Outcome banner","banner-zoom-en");
            yield return Wait(FireRescueGame.ResultsDelay+FireRescueGame.ResultsRise);
            Assert.True(game.ResultsVisible);Assert.False(outcome.BannerVisible);Capture(view,"results-handoff-en",Seat(game,14),92);
        }
    }
}

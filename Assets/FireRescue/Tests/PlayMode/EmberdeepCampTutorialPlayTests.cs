using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PhysicalAssembly;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    // LoopSpec §9 camp tutorial (workstream T), end to end in the camp scene on the desktop path: How to play, real palm
    // water packets on the campfire, the gem run dry, pump strikes filling it, the scripted spark/sheep/sapling moment,
    // the pet and the hop, the finish; the first NEW GAME asking about the tutorial once (headset test 2026-11-22: it no
    // longer starts by itself), HOW TO PLAY carrying on into the game, SKIP being remembered; camp sheep staying on the moss.
    // Voice-over (2026-11-22): the forest spirit comes out of the tent, says each step's lines with subtitles, and the
    // steps wait for it; the lines run VoiceSpeed times faster (silent) here.
    // Captures come from the seated desktop eye (1.2 m; the tutorial tips the desktop view 30 degrees down) and go to
    // Logs/EmberdeepCampTutorial/. Not a headset capture: the ghost and gem sit at their desktop spots.
    public sealed class EmberdeepCampTutorialPlayTests
    {
        const string Output="Logs/EmberdeepCampTutorial";
        const float SeatedEye=1.2f,VoiceSpeed=30;
        EmberdeepCamp camp;int? seen,water,pump;int offIsland;

        [UnitySetUp] public IEnumerator SavePrefs()
        {
            seen=Get(EmberdeepCamp.TutorialPrefsKey);water=Get(EmberdeepFirstHints.Key(EmberdeepFirstHints.Move.Water));pump=Get(EmberdeepFirstHints.Key(EmberdeepFirstHints.Move.Pump));
            EmberdeepWaterHand.Reload();EmberdeepForestSpirit.VoiceSpeed=VoiceSpeed;yield break;
        }
        [UnityTearDown] public IEnumerator RestorePrefs()
        {
            EmberdeepCamp.TutorialPromptOnDesktop=false;EmberdeepForestSpirit.VoiceSpeed=1;
            Put(EmberdeepCamp.TutorialPrefsKey,seen);Put(EmberdeepFirstHints.Key(EmberdeepFirstHints.Move.Water),water);Put(EmberdeepFirstHints.Key(EmberdeepFirstHints.Move.Pump),pump);
            PlayerPrefs.Save();yield break;
        }
        static int? Get(string key)=>PlayerPrefs.HasKey(key)?PlayerPrefs.GetInt(key):null;
        static void Put(string key,int? value){if(value.HasValue)PlayerPrefs.SetInt(key,value.Value);else PlayerPrefs.DeleteKey(key);}

        IEnumerator LoadCamp()
        {
            EmberdeepSession.NewExpedition();
            yield return SceneManager.LoadSceneAsync(EmberdeepSession.CampScene);yield return null;
            camp=Object.FindFirstObjectByType<EmberdeepCamp>();Assert.NotNull(camp);
            // A seated eye: the sign is placed at eye height, so everything below follows it.
            camp.Player.ViewCamera.transform.position=new Vector3(0,SeatedEye,0);
            camp.SendMessage("OnApplicationFocus",true);camp.SendMessage("OnApplicationPause",false);
            float deadline=Time.realtimeSinceStartup+10;while(!camp.IsPlaced && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(camp.IsPlaced);Assert.NotNull(camp.Play);Assert.IsTrue(camp.Play.Built,"camp art kit present");
            yield return null;
        }
        static IEnumerator Until(System.Func<bool> done,float seconds,System.Action each=null)
        {
            float deadline=Time.realtimeSinceStartup+seconds;
            while(!done() && Time.realtimeSinceStartup<deadline){each?.Invoke();yield return null;}
        }
        static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
        void Capture(string name)
        {
            Directory.CreateDirectory(Output);var camera=camp.Player.ViewCamera;
            var prior=camera.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(1280,960,24,RenderTextureFormat.ARGB32){antiAliasing=4};var texture=new Texture2D(1280,960,TextureFormat.RGBA32,false);
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1280,960),0,0);texture.Apply();
                File.WriteAllBytes(Output+"/"+name+".png",texture.EncodeToPNG());
            }
            finally{camera.targetTexture=prior;RenderTexture.active=active;target.Release();Object.Destroy(target);Object.Destroy(texture);}
        }
        WorkshopButton Button(string command)=>camp.GetComponentsInChildren<WorkshopButton>(true).Single(b=>b.Command==command);
        // Counts frames with any camp sheep off the flat moss top (EmberdeepCampPlay.SheepWalk).
        void CountOffIsland(EmberdeepCampPlay play){foreach(var a in play.Scene.Animals)if(!EmberdeepCampPlay.OnSheepWalk(EmberdeepCampPlay.ToIsland(a.Position)))offIsland++;}

        [UnityTest] public IEnumerator HowToPlayTeachesPalmWaterThePumpAndTheSheep()
        {
            yield return LoadCamp();var play=camp.Play;
            Assert.AreEqual(EmberdeepCamp.MenuPage.Main,camp.ActivePage,"Desktop never starts it by itself");
            Assert.IsTrue(Button("How to play").gameObject.activeInHierarchy);
            Capture("0-how-to-play-entry");
            Button("How to play").Press();yield return null;
            Assert.AreEqual(EmberdeepCamp.MenuPage.Tutorial,camp.ActivePage);Assert.AreEqual(EmberdeepCampTutorialFlow.Step.Intro,play.Flow.Current);
            Assert.IsTrue(Button("Skip tutorial").gameObject.activeInHierarchy);Assert.IsFalse(Button("New Game").gameObject.activeInHierarchy);
            Assert.IsTrue(play.CampfireLit);Assert.AreEqual(EmberdeepCampPlay.GemCapacity,play.GemWater);

            // 0. The forest spirit comes out of the tent, then says hello with subtitles under it.
            var spirit=play.Spirit;Assert.AreEqual(EmberdeepForestSpirit.State.Arriving,spirit.Phase);
            yield return Wait(.7f);Assert.IsTrue(spirit.Rendered);Assert.IsNull(spirit.Line,"Not a word on the way out");Capture("0b-spirit-arrives");
            Assert.IsTrue(play.PumpHeld);Assert.IsFalse(play.PumpShown,"No pump during the intro: it waits for its own lines");
            yield return Until(()=>spirit.Speaking,3);Assert.AreSame(EmberdeepSpiritLines.Intro1,spirit.Line);
            Assert.AreEqual(EmberdeepForestSpirit.State.Hovering,spirit.Phase);
            yield return null;Assert.IsFalse(string.IsNullOrEmpty(spirit.SubtitleShown),"Subtitle under the spirit");
            float below=Vector3.Dot(spirit.Position-spirit.SubtitleRoot.position,Vector3.up);
            Assert.AreEqual(EmberdeepForestSpirit.SubtitleBelow,below,.01f,"The subtitle hangs under the orb");
            Capture("0c-spirit-says-hello");
            Assert.IsNull(play.HintShowing,"No ghost during the intro");
            yield return Until(()=>play.Flow.Current==EmberdeepCampTutorialFlow.Step.Campfire,4);
            Assert.AreEqual(EmberdeepCampTutorialFlow.Step.Campfire,play.Flow.Current,"The intro's lines, then the campfire");
            Assert.IsTrue(play.Director.Said(EmberdeepSpiritLines.Intro2));Assert.AreEqual(0,play.Flow.Chip,"the drop chip lights");
            yield return Wait(.4f);
            Assert.AreEqual(EmberdeepInteractionDemo.Action.Goddess,play.HintShowing,"Ghost palm-water hint");
            Assert.IsTrue(play.Director.Said(EmberdeepSpiritLines.FireAim),"The spirit asks for palm water");
            Capture("1-campfire");

            // 1. Palm water: real packets from the palm put the campfire out.
            float sprayed=0;bool shot=false;
            yield return Until(()=>!play.CampfireLit,8,()=>{play.SprayAt(play.FireWorld(EmberdeepCampPlay.Campfire));sprayed+=Time.unscaledDeltaTime;if(sprayed>.8f && !shot){shot=true;Capture("1b-campfire-spray");}});
            Assert.IsFalse(play.CampfireLit,"Water packets put the campfire out");
            Assert.Greater(play.GemWater,0);Assert.Less(play.GemWater,EmberdeepCampPlay.GemCapacity);
            Assert.IsTrue(play.Director.Said(EmberdeepSpiritLines.FireFirstSpray),"\"Too fiddly?\" on the first spray");
            Assert.IsFalse(play.PumpShown,"Still no pump during the campfire step");
            yield return Until(()=>play.Flow.Current==EmberdeepCampTutorialFlow.Step.Pump,4);

            // 2. The pump rises in only once its lines are said (joke, then how-to), on the water the campfire left: it
            // glows, strikes top the gem up. No emptying, no relight, no passive refill in this step.
            Assert.AreEqual(EmberdeepCampTutorialFlow.Step.Pump,play.Flow.Current);
            float left=play.GemWater;int before=play.PumpStrikes;
            Assert.IsTrue(play.PumpHeld,"hidden while the spirit introduces it");Assert.IsFalse(play.PumpShown);
            play.StrikePump();Assert.AreEqual(before,play.PumpStrikes,"no strike lands on a hidden pump");
            yield return Until(()=>play.PumpShown,4);
            Assert.IsTrue(play.PumpShown,"the pump rises in");
            Assert.IsTrue(play.Director.Said(EmberdeepSpiritLines.PumpJoke) && play.Director.Said(EmberdeepSpiritLines.PumpHow),"after both lines");
            Assert.IsFalse(play.Spirit.Speaking,"...and once the how-to has finished");
            Assert.AreEqual(left,play.GemWater,.001f,"No passive refill while the tutorial teaches the pump");
            yield return Until(()=>play.PumpGlowing,2);
            Assert.IsTrue(play.PumpGlowing,"The pump step lights the pump");Assert.IsFalse(play.GemEmpty);
            yield return Wait(EmberdeepCampPlay.PumpRiseSeconds+.2f);
            Assert.AreEqual(EmberdeepInteractionDemo.Action.Hammer,play.HintShowing);
            Assert.IsFalse(play.CampfireLit,"No relight in the pump step");
            Capture("2-pump-glows");
            int strikes=0;
            while(!play.GemPumpedFull && strikes<20){play.StrikePump();strikes++;if(strikes==2){yield return Wait(.12f);Capture("2b-pump-strike");}yield return Wait(.25f);}
            Assert.IsTrue(play.GemPumpedFull,"Hammering topped the gem up");Assert.GreaterOrEqual(strikes,1);
            yield return Until(()=>play.Director.Said(EmberdeepSpiritLines.PumpDone),2);Assert.IsTrue(play.Director.Said(EmberdeepSpiritLines.PumpDone),"\"There. Full.\"");
            yield return Wait(.2f);Capture("2c-gem-full");

            // 3. A spark lands on a sheep; it bolts and lights the sapling. Put both out.
            yield return Until(()=>play.Flow.Current==EmberdeepCampTutorialFlow.Step.Sheep,3);
            Assert.AreEqual(EmberdeepCampTutorialFlow.Step.Sheep,play.Flow.Current);
            yield return Until(()=>play.SheepMoment==EmberdeepCampPlay.Moment.Spark,3);
            yield return Wait(.45f);Capture("3a-spark");
            yield return Until(()=>play.SheepOnFire,2);Assert.IsTrue(play.SheepOnFire);
            Assert.AreEqual(AnimalState.Running,play.Scene.Animals[EmberdeepCampPlay.TutorialSheep].State);
            yield return null;Assert.AreSame(EmberdeepSpiritLines.SheepFire,spirit.Line,"\"You fluffy idiot\" as it catches");
            yield return Wait(.15f);Capture("3b-sheep-on-fire");
            yield return Until(()=>play.SaplingBurning,3);Assert.IsTrue(play.SaplingBurning,"The burning sheep lights the sapling");
            yield return Wait(.4f);Assert.AreEqual(EmberdeepInteractionDemo.Action.Goddess,play.HintShowing);Capture("3c-sapling-catches");
            offIsland=0;
            yield return Until(()=>!play.SheepOnFire,5,()=>{CountOffIsland(play);play.SprayAt(play.SheepWorld(EmberdeepCampPlay.TutorialSheep));});
            Assert.IsFalse(play.SheepOnFire);
            yield return Until(()=>!play.SaplingBurning,8,()=>{CountOffIsland(play);if(play.GemEmpty)play.StrikePump();else play.SprayAt(play.FireWorld(EmberdeepCampPlay.Sapling));});
            Assert.IsFalse(play.SaplingBurning);
            Assert.Zero(offIsland,"The burning sheep's dash and panic stay on the island's moss");
            yield return Until(()=>play.SheepMoment==EmberdeepCampPlay.Moment.Saved,1);Assert.AreEqual(EmberdeepCampPlay.Moment.Saved,play.SheepMoment);

            // 4. Pet the saved sheep: B's pet + hop.
            yield return Until(()=>play.Flow.Current==EmberdeepCampTutorialFlow.Step.Pet,3);
            Assert.AreEqual(EmberdeepCampTutorialFlow.Step.Pet,play.Flow.Current);
            yield return Wait(.5f);Assert.AreEqual(EmberdeepInteractionDemo.Action.Slap,play.HintShowing,"Ghost stroke over the sheep");
            Capture("4-pet-hint");
            Assert.IsTrue(play.PetSheep(EmberdeepCampPlay.TutorialSheep));yield return Wait(.2f);
            Assert.IsTrue(play.Flock.Hopping(EmberdeepCampPlay.TutorialSheep),"The boing hop");Capture("4b-pet-hop");

            yield return Until(()=>play.Flow.Current==EmberdeepCampTutorialFlow.Step.Done,4);
            Assert.AreEqual(EmberdeepCampTutorialFlow.Step.Done,play.Flow.Current);
            Assert.IsTrue(play.Director.Said(EmberdeepSpiritLines.PetHow) || play.Director.Said(EmberdeepSpiritLines.SheepFire));
            yield return Until(()=>spirit.Line==EmberdeepSpiritLines.Outro,2);Assert.AreSame(EmberdeepSpiritLines.Outro,spirit.Line,"The send-off");
            yield return Wait(.2f);Capture("5-done");
            yield return Until(()=>!play.Flow.Active,EmberdeepCampTutorialFlow.DoneSeconds+EmberdeepCampTutorialFlow.OutroHold+3);
            Assert.AreEqual(EmberdeepCamp.MenuPage.Main,camp.ActivePage);Assert.IsTrue(EmberdeepCamp.TutorialSeen);
            Assert.AreEqual(EmberdeepForestSpirit.State.Leaving,spirit.Phase,"It goes back into the tent");
            yield return Until(()=>!spirit.Visible,EmberdeepForestSpirit.LeaveSeconds+1);Assert.IsFalse(spirit.Visible);
            Assert.AreEqual(Quaternion.identity,camp.Player.ViewCamera.transform.rotation);
            Assert.IsFalse(camp.Transitioning,"How to play from the main page returns to the menu");
        }

        [UnityTest] public IEnumerator FirstPlayAsksAboutTheTutorialOnceAndSkipIsRemembered()
        {
            EmberdeepCamp.ResetTutorialSeen();EmberdeepCamp.TutorialPromptOnDesktop=true;
            yield return LoadCamp();yield return Wait(.3f);
            Assert.AreEqual(EmberdeepCamp.MenuPage.Main,camp.ActivePage,"First launch never starts the tutorial by itself");
            Assert.IsFalse(camp.TutorialActive);Assert.IsTrue(camp.AsksBeforePlay);
            Button("New Game").Press();yield return null;
            Assert.AreEqual(EmberdeepCamp.MenuPage.TutorialPrompt,camp.ActivePage,"NEW GAME asks first");
            Assert.IsFalse(camp.Transitioning);Assert.IsTrue(camp.PlayPending);
            Assert.IsTrue(Button("Prompt how to play").gameObject.activeInHierarchy);Assert.IsTrue(Button("Prompt skip").gameObject.activeInHierarchy);
            Assert.IsFalse(Button("New Game").gameObject.activeInHierarchy);
            Assert.IsTrue(camp.GetComponentsInChildren<TextMesh>().Any(t=>t.text==EmberdeepCamp.PromptTitle));
            Assert.IsTrue(camp.GetComponentsInChildren<TextMesh>().Any(t=>t.text==EmberdeepCamp.PromptText));
            yield return Wait(.2f);Capture("6-first-play-prompt");
            Button("Prompt skip").Press();yield return null;
            Assert.IsTrue(EmberdeepCamp.TutorialSeen,"SKIP is remembered");Assert.IsFalse(camp.AsksBeforePlay,"...so it never asks again");
            Assert.IsTrue(camp.Transitioning,"SKIP starts the game");Assert.IsFalse(camp.PlayPending);
            Assert.IsFalse(camp.TutorialActive);
        }

        [UnityTest] public IEnumerator FirstPlayHowToPlayRunsTheTutorialThenStartsTheGame()
        {
            EmberdeepCamp.ResetTutorialSeen();EmberdeepCamp.TutorialPromptOnDesktop=true;
            yield return LoadCamp();yield return null;
            Button("New Game").Press();yield return null;
            Assert.AreEqual(EmberdeepCamp.MenuPage.TutorialPrompt,camp.ActivePage);
            Button("Prompt how to play").Press();yield return null;
            Assert.AreEqual(EmberdeepCamp.MenuPage.Tutorial,camp.ActivePage);Assert.IsTrue(camp.TutorialActive);
            Assert.IsTrue(camp.PlayPending,"The NEW GAME press waits for the tutorial");Assert.IsFalse(camp.Transitioning);
            Assert.IsFalse(camp.GetComponentsInChildren<TextMesh>().Any(t=>t.text.Contains("PINCH")),"No sentences on the tutorial sign");
            yield return Wait(.3f);
            Button("Skip tutorial").Press();yield return null;
            Assert.IsTrue(EmberdeepCamp.TutorialSeen);Assert.IsFalse(camp.TutorialActive);Assert.IsNull(camp.Play.HintShowing);
            Assert.IsFalse(camp.Play.Spirit.Speaking,"SKIP silences the spirit");Assert.AreEqual(1f,EmberdeepAudioSettings.Ducking,.001f,"...and the duck lets go");
            Assert.IsTrue(camp.Transitioning,"Done with the tutorial, the game starts");Assert.IsFalse(camp.PlayPending);
        }

        // The owner's rule: a line whose deed is already done is skipped. Put the campfire out while the spirit is still
        // saying hello, and the campfire step is done without its lines.
        [UnityTest] public IEnumerator PuttingTheCampfireOutDuringTheIntroSkipsItsLines()
        {
            yield return LoadCamp();var play=camp.Play;
            Button("How to play").Press();yield return null;
            yield return Until(()=>!play.CampfireLit,8,()=>play.SprayAt(play.FireWorld(EmberdeepCampPlay.Campfire)));
            Assert.IsFalse(play.CampfireLit);Assert.AreEqual(EmberdeepCampTutorialFlow.Step.Intro,play.Flow.Current,"still saying hello");
            yield return Until(()=>play.Flow.Current==EmberdeepCampTutorialFlow.Step.Pump,8);
            Assert.AreEqual(EmberdeepCampTutorialFlow.Step.Pump,play.Flow.Current);Assert.IsFalse(play.CampfireLit,"not relit to teach it again");
            Assert.IsTrue(play.Director.Said(EmberdeepSpiritLines.Intro2),"the hello is never cut");
            Assert.IsFalse(play.Director.Said(EmberdeepSpiritLines.FireAim));Assert.IsFalse(play.Director.Said(EmberdeepSpiritLines.FirePress));
            Button("Skip tutorial").Press();yield return null;
            Assert.AreEqual(EmberdeepForestSpirit.State.Leaving,play.Spirit.Phase);
        }

        [UnityTest] public IEnumerator CampSheepArePettableAndTheCampfireRelightsOutsideTheTutorial()
        {
            yield return LoadCamp();var play=camp.Play;Assert.IsFalse(play.Flow.Active);
            Assert.IsTrue(play.PumpShown,"Outside the tutorial the pump is simply there");Assert.IsFalse(play.PumpHeld);
            Assert.IsTrue(play.PetSheep(EmberdeepCampPlay.Lamb));yield return Wait(.15f);
            Assert.IsTrue(play.Flock.Hopping(EmberdeepCampPlay.Lamb));
            Assert.IsFalse(play.PetSheep(EmberdeepCampPlay.Lamb),"Pet cooldown");
            yield return Until(()=>!play.CampfireLit,8,()=>play.SprayAt(play.FireWorld(EmberdeepCampPlay.Campfire)));
            Assert.IsFalse(play.CampfireLit);
            yield return Until(()=>play.CampfireLit,EmberdeepCampPlay.RelightSeconds+1);
            Assert.IsTrue(play.CampfireLit,"Practice any time: the campfire relights");
        }
    }
}

using System.Collections;
using System.Linq;
using NUnit.Framework;
using PhysicalAssembly;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    // LoopSpec §3 in the real fireground scene. Editor IsXR is false: fists are synthetic
    // samples through FireRescueGame.SamplePumpHands, the mouse and SPACE stand in for the
    // palm. Neither establishes tracking quality, comfort or the feel of the strike.
    public sealed class EmberdeepWaterLoopPlayTests
    {
        FireRescueGame game;
        bool hadHand;int savedHand;
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;
        Mouse mouse;Keyboard keyboard;
        [SetUp] public void Setup()
        {
            hadHand=PlayerPrefs.HasKey(EmberdeepWaterHand.PrefsKey);savedHand=PlayerPrefs.GetInt(EmberdeepWaterHand.PrefsKey,0);
            EmberdeepWaterHand.Set(true);
            background=InputSystem.settings.backgroundBehavior;editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            mouse=InputSystem.AddDevice<Mouse>();keyboard=InputSystem.AddDevice<Keyboard>();
        }
        [TearDown] public void Cleanup()
        {
            if(game)game.enabled=false;
            InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
            if(hadHand)PlayerPrefs.SetInt(EmberdeepWaterHand.PrefsKey,savedHand);else PlayerPrefs.DeleteKey(EmberdeepWaterHand.PrefsKey);
            EmberdeepWaterHand.Reload();EmberdeepWaterTuning.RhythmEnabled=EmberdeepWaterTuning.RhythmEnabledDefault;
            EmberdeepSession.NewExpedition();
        }
        IEnumerator Enter()
        {
            EmberdeepSession.NewExpedition();yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);yield return null;yield return null;
            game=Object.FindFirstObjectByType<FireRescueGame>();Assert.NotNull(game);
            game.SendMessage("OnApplicationFocus",true);game.SendMessage("OnApplicationPause",false);
            EmberdeepTestFlow.StartPrepared(game.Run);game.Refresh();yield return null;
            Assert.True(game.Run.GemMode);Assert.AreEqual(FirePhase.Fighting,game.Run.Phase);
        }
        IEnumerator Frames(int count,Vector2 pointer,params Key[] keys)
        {
            for(int i=0;i<count;i++)
            {
                InputSystem.QueueStateEvent(mouse,new MouseState{position=pointer});
                InputSystem.QueueStateEvent(keyboard,keys.Length>0?new KeyboardState(keys):new KeyboardState());yield return null;
            }
        }
        static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
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

        [UnityTest] public IEnumerator EmptyGemGlowsThePumpThenHammeredSplashesFillItForSpraying()
        {
            yield return Enter();
            var run=game.Run;var camera=game.Player.ViewCamera;
            Assert.False(game.HydrantStation.gameObject.activeSelf,"The well station is gone from the production flow");
            Assert.True(game.Pump.gameObject.activeInHierarchy);Assert.Greater(game.Arena.InverseTransformPoint(game.Pump.transform.position).x,0,"Left water hand: pump on the free (right) side");
            var tree=game.Arena.TransformPoint(run.Trees[0].Position);var pointer=(Vector2)camera.WorldToScreenPoint(tree);
            yield return Frames(3,pointer);
            Assert.True(game.ManaCrystal.gameObject.activeInHierarchy,"Desktop shows the gem in view");
            Assert.True(game.AimVfx.LastHasImpact,"The aim marks the first surface the water hits");
            Assert.GreaterOrEqual(game.AimVfx.LastCrownCount,1,"The aimed tree's crown is highlighted");
            Assert.True(game.AimVfx.GetComponent<Renderer>().enabled);
            for(int i=0;i<40 && !run.GemEmpty;i++)run.TickMagic(.1f,true,true);
            Assert.True(run.GemEmpty);
            // The shown level drains at 2.5/s, so the gem dulls a moment after it is really empty.
            float wait=Time.realtimeSinceStartup+2;
            while(!game.ManaCrystal.Dull && Time.realtimeSinceStartup<wait)yield return Frames(1,pointer);
            Assert.True(game.ManaCrystal.Dull,"An empty gem goes dull");Assert.True(game.Pump.Glowing,"An empty gem lights the pump");
            yield return Frames(3,pointer,Key.Space);
            Assert.True(game.GemSputtering,"Touching a dry gem sputters");Assert.True(game.WaterAudio.Sputtering);
            yield return Frames(2,pointer);
            float before=run.MagicWater;
            Assert.True(Hammer(0),"A fast fist through the cap is one strike");
            Assert.AreEqual(before+EmberdeepWaterTuning.StrikeWater,run.MagicWater,.01f);
            Assert.AreEqual(1,game.Pump.SplashCount,"Each strike throws a splash toward the gem");
            Assert.Greater(game.Pump.InFlightUnits,0);
            yield return Frames(2,pointer);
            Assert.False(game.Pump.Glowing,"Water on its way: the pump calms down");
            float shown=game.GemDisplayFill;int gulps=game.WaterAudio.Gulps;float tone=0;
            float until=Time.realtimeSinceStartup+EmberdeepWaterTuning.SplashFlightSeconds+.5f;
            while(game.Pump.SplashCount>0 && Time.realtimeSinceStartup<until)yield return Frames(1,pointer);
            for(int i=0;i<10;i++){yield return Frames(1,pointer);tone=Mathf.Max(tone,game.WaterAudio.FillToneVolume);}
            Assert.Greater(tone,0,"The gem filling plays its rising tone");
            Assert.Zero(game.Pump.SplashCount);Assert.Greater(game.WaterAudio.Gulps,gulps,"The splash lands with a gulp");
            Assert.Greater(game.GemDisplayFill,shown,"The level rises when the splash lands");Assert.False(game.ManaCrystal.Dull);
            for(int stroke=0;stroke<4;stroke++)Assert.True(Hammer(stroke%2),"Either hand can keep hammering");
            Assert.AreEqual(5,game.PumpStrikes);
            float pumped=run.MagicWater,consumed=run.MagicConsumed;
            yield return Frames(20,pointer,Key.Space);
            Assert.Greater(run.MagicConsumed,consumed,"The pumped water sprays");Assert.Less(run.MagicWater,pumped);
            Assert.True(game.MagicSpraying || run.GemEmpty);Assert.AreEqual(60,run.Water,"No tool tank is involved");
            yield return Frames(2,pointer);
        }
        [UnityTest] public IEnumerator RhythmBonusOnlyLandsOnTheBeatAndCanBeSwitchedOff()
        {
            yield return Enter();
            var run=game.Run;for(int i=0;i<40 && !run.GemEmpty;i++)run.TickMagic(.1f,true,true);
            Assert.True(Hammer(0));Assert.AreEqual(EmberdeepWaterTuning.StrikeWater,run.MagicWater,.01f,"The first strike sets the beat");
            Assert.True(game.PumpRhythm.Active(Time.unscaledTimeAsDouble));
            Assert.AreEqual(EmberdeepPumpGrade.Plain,game.Pump.LastGrade,"The strike that starts a beat is neither good nor off");
            yield return null;
            // ONE cue: a ring shrinking onto a fixed target ring.
            float target=EmberdeepHammerPumpView.CapRadius*EmberdeepWaterTuning.BeatTargetRing;
            Assert.True(game.Pump.RhythmVisible);Assert.AreEqual(target,game.Pump.TargetRadius,1e-5f);
            float approach=game.Pump.ApproachRadius;Assert.GreaterOrEqual(approach,target);
            yield return null;yield return null;
            Assert.AreEqual(target,game.Pump.TargetRadius,1e-5f,"The target ring never moves");
            Assert.Less(game.Pump.ApproachRadius,approach,"The approach ring closes on it");
            double start=Time.unscaledTimeAsDouble;
            while(!game.PumpRhythm.InWindow(Time.unscaledTimeAsDouble) && Time.unscaledTimeAsDouble-start<2)yield return null;
            Assert.True(Hammer(1));Assert.AreEqual(EmberdeepWaterTuning.StrikeWater+EmberdeepWaterTuning.OnBeatWater,run.MagicWater,.01f,"On the beat: bonus water");
            Assert.AreEqual(1,game.PumpBeatStrikes);Assert.True(game.Pump.RhythmVisible || game.PumpRhythm.Active(Time.unscaledTimeAsDouble));
            Assert.AreEqual(EmberdeepPumpGrade.OnBeat,game.Pump.LastGrade);Assert.AreEqual(1,game.WaterAudio.Chimes,"A clean good-pump sound");
            Assert.Greater(game.Pump.Flash,0,"A short flash after the strike");
            // Half a beat later is off the beat: plain water, a rusty creak.
            start=Time.unscaledTimeAsDouble;
            while(Mathf.Abs(game.PumpRhythm.Phase01(Time.unscaledTimeAsDouble)-.5f)>.12f && Time.unscaledTimeAsDouble-start<2)yield return null;
            float offBefore=run.MagicWater;int creaks=game.WaterAudio.Creaks;Assert.True(Hammer(0));
            Assert.AreEqual(EmberdeepPumpGrade.OffBeat,game.Pump.LastGrade);Assert.AreEqual(offBefore+EmberdeepWaterTuning.StrikeWater,run.MagicWater,.01f);
            Assert.AreEqual(creaks+1,game.WaterAudio.Creaks,"Off the beat: a rusty creak");
            yield return Wait(EmberdeepWaterTuning.StrikeFlashSeconds+.1f);Assert.Zero(game.Pump.Flash,"The flash is short");
            EmberdeepWaterTuning.RhythmEnabled=false;
            start=Time.unscaledTimeAsDouble;
            while(!game.PumpRhythm.InWindow(Time.unscaledTimeAsDouble) && Time.unscaledTimeAsDouble-start<2)yield return null;
            float before=run.MagicWater;Assert.True(Hammer(0));
            Assert.AreEqual(before+EmberdeepWaterTuning.StrikeWater,run.MagicWater,.01f,"With the toggle off every strike is a plain strike");
            Assert.AreEqual(EmberdeepPumpGrade.Plain,game.Pump.LastGrade);
            yield return null;Assert.False(game.Pump.RhythmVisible);
        }
        [UnityTest] public IEnumerator WaterHandSwapMirrorsTheGemThePumpAndTheHints()
        {
            yield return Enter();
            // The pump stands on the FREE-hand side (headset test 2026-11-22), so it mirrors the other way to the gem.
            var pumpLeft=game.Arena.InverseTransformPoint(game.Pump.transform.position);
            Assert.True(game.WaterHandLeft);Assert.True(game.ManaCrystal.FollowsLeft);Assert.Greater(pumpLeft.x,0);
            Assert.Less(Vector3.Dot(game.Pump.transform.right,game.Arena.right),0,"A pump on the right turns its spout to the player's centre");
            EmberdeepWaterHand.Set(false);yield return Frames(2,Vector2.zero);
            Assert.False(game.WaterHandLeft);Assert.False(game.ManaCrystal.FollowsLeft);Assert.AreEqual("Right hand mana crystal",game.ManaCrystal.name);
            var pumpRight=game.Arena.InverseTransformPoint(game.Pump.transform.position);
            Assert.AreEqual(-pumpLeft.x,pumpRight.x,.001f,"Right water hand: the pump moves to the left (free) side");Assert.AreEqual(pumpLeft.z,pumpRight.z,.001f);
            Assert.Greater(Vector3.Dot(game.Pump.transform.right,game.Arena.right),0,"Its spout turns to face the player's centre");
            Assert.True(Hammer(1));
            game.Run.ApplyWater(Vector3.zero,10000,100);game.Run.EvacuateSafeAnimals();
            game.OpenShop();game.RefreshShopPresentation();
            Assert.AreEqual(EmberdeepShopPage.Magic,game.ShopPage);
            Assert.True(game.BuyShopSelection(),"The first-upgrade lesson text gives way to the item text");game.RefreshShopPresentation();
            // LoopSpec §11b: the spell flow's upgrade tiles carry no hint text (the palm is taught by a mirrored
            // ghost hand), so the old "Open right palm" shop line is gone. The gem tile still sells the upgrade.
            Assert.True(game.UpgradesVisible);
            game.CloseShop();
            EmberdeepWaterHand.Set(true);yield return Frames(2,Vector2.zero);
            Assert.AreEqual(pumpLeft.x,game.Arena.InverseTransformPoint(game.Pump.transform.position).x,.001f);
        }
        [UnityTest] public IEnumerator PumpingTheGemFullRaisesTheFillToneAndChimesOnce()
        {
            yield return Enter();
            var run=game.Run;for(int i=0;i<40 && !run.GemEmpty;i++)run.TickMagic(.1f,true,true);
            float wait=Time.realtimeSinceStartup+2;
            while(game.GemDisplayFill>.01f && Time.realtimeSinceStartup<wait)yield return null;
            int full=game.WaterAudio.FullChimes;float loud=0,lowPitch=9,highPitch=0;
            for(int strikes=0;strikes<20 && run.MagicWater<run.MagicCapacity-.01f;strikes++)
            {
                Assert.True(Hammer(strikes%2));
                for(int f=0;f<6;f++)
                {
                    yield return null;
                    if(game.WaterAudio.FillToneAudible){loud=Mathf.Max(loud,game.WaterAudio.FillToneVolume);lowPitch=Mathf.Min(lowPitch,game.WaterAudio.FillTonePitch);highPitch=Mathf.Max(highPitch,game.WaterAudio.FillTonePitch);}
                }
            }
            wait=Time.realtimeSinceStartup+3;
            while(game.GemDisplayFill<.999f && Time.realtimeSinceStartup<wait){yield return null;if(game.WaterAudio.FillToneAudible)highPitch=Mathf.Max(highPitch,game.WaterAudio.FillTonePitch);}
            Assert.Greater(loud,0,"Pumping plays the filling tone");
            Assert.Greater(highPitch,lowPitch+.3f,"Its pitch climbs with the level");
            Assert.AreEqual(full+1,game.WaterAudio.FullChimes,"One chime when the gem is full");
            yield return Wait(1);Assert.AreEqual(full+1,game.WaterAudio.FullChimes,"...and only one");
        }
        [UnityTest] public IEnumerator ShopShowsOnlyTheGemUpgradeInTheProductionFlow()
        {
            yield return Enter();
            game.Run.ApplyWater(Vector3.zero,10000,100);game.Run.EvacuateSafeAnimals();
            game.OpenShop();game.RefreshShopPresentation();
            Assert.True(game.ShopCanBuy);Assert.AreEqual("magic:1",game.ShopItemId);
            Assert.True(game.BuyShopSelection());Assert.AreEqual(EmberdeepWaterTuning.GemCapacity+EmberdeepWaterTuning.GemCapacityPerLevel,game.Run.MagicCapacity);
            Assert.False(game.Run.NeedsMagicUpgrade);
            game.ShowShopPage(EmberdeepShopPage.Equipment);
            Assert.AreEqual(EmberdeepShopPage.Magic,game.ShopPage,"Spells are cut from the shop");
            game.ShowShopPage(EmberdeepShopPage.Station);Assert.AreEqual(EmberdeepShopPage.Magic,game.ShopPage,"Station tiers are cut");
            game.ShowShopPage(EmberdeepShopPage.Tuning);Assert.AreEqual(EmberdeepShopPage.Magic,game.ShopPage,"Tool tuning is cut");
            foreach(var command in new[]{"Previous gear","Next gear","Tune gear","Upgrade water station","View magic","Compare station"})
                Assert.False(game.GetComponentsInChildren<WorkshopButton>(true).Single(b=>b.Command==command).gameObject.activeSelf,command+" is hidden");
            Assert.AreEqual("magic:2",game.ShopItemId);
            game.CloseShop();yield return null;
        }
        static string ShopText(FireRescueGame game)=>string.Join("\n",game.GetComponentsInChildren<TextMesh>(true).Where(t=>t.gameObject.activeInHierarchy).Select(t=>t.text));
    }
}

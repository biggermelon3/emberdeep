using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    // LoopSpec §3: one gem, passive refill, fist-hammer pump, rhythm bonus, water hand.
    public sealed class EmberdeepWaterLoopTests
    {
        const float Dt=1/72f;
        static void Drain(FireRun run){for(int i=0;i<100 && !run.GemEmpty;i++)run.TickMagic(.1f,true,true);Assert.True(run.GemEmpty);}

        [Test] public void GemStartsEveryLevelFullAndPlanCannotSpendIt()
        {
            var run=new FireRun(goddessTutorial:true);
            Assert.AreEqual(EmberdeepWaterTuning.GemCapacity,run.MagicCapacity);Assert.AreEqual(run.MagicCapacity,run.MagicWater,"A new expedition starts full");
            Assert.True(run.StartStage(true));Assert.AreEqual(FirePhase.Preparing,run.Phase);
            for(int i=0;i<20;i++){run.TickMagic(.1f,true,true);Assert.Zero(run.PumpGem(4));}
            Assert.AreEqual(run.MagicCapacity,run.MagicWater,"No spraying or pumping before the level");
            Assert.True(run.BeginCountdown());for(int i=0;i<31;i++)run.TickPreparation(.1f);Assert.AreEqual(FirePhase.Fighting,run.Phase);
            Drain(run);Assert.True(run.RetryStage());Assert.AreEqual(run.MagicCapacity,run.MagicWater,"A retry refills the gem");
            Drain(run);run.ApplyWater(Vector3.zero,10000,100);run.EvacuateSafeAnimals();Assert.True(run.BuyMagicUpgrade());
            Assert.True(run.StartStage());Assert.AreEqual(run.MagicCapacity,run.MagicWater,"The next level loads with a full gem");
        }
        [Test] public void PumpStrikesFillTheGemOnlyWhileFightingAndStopAtFull()
        {
            var run=new FireRun(goddessTutorial:true);run.StartStage();Drain(run);
            Assert.AreEqual(4,run.PumpGem(EmberdeepWaterTuning.StrikeWater));Assert.AreEqual(7,run.PumpGem(EmberdeepWaterTuning.OnBeatWater));
            Assert.AreEqual(11,run.MagicWater,.001f);Assert.AreEqual(11,run.MagicPumped,.001f);
            Assert.Zero(run.PumpGem(float.NaN));Assert.Zero(run.PumpGem(-3));
            for(int i=0;i<20;i++)run.PumpGem(4);
            Assert.AreEqual(run.MagicCapacity,run.MagicWater);Assert.AreEqual(run.MagicCapacity,run.MagicPumped,.001f,"Overflow is not counted");
            Assert.Zero(run.PumpGem(4),"A full gem takes nothing");
            Assert.AreEqual(60,run.Water);Assert.Zero(run.SuppliedWater,"The retired tool tank and supply never see gem water");
            var legacy=new FireRun();legacy.StartStage();Assert.Zero(legacy.PumpGem(4),"The legacy tool flow has no hammer pump");
        }
        [Test] public void TouchingADryGemHoldsOffThePassiveRefill()
        {
            var run=new FireRun(goddessTutorial:true);run.StartStage();Drain(run);
            for(int i=0;i<30;i++)Assert.Zero(run.TickMagic(.1f,true,true),"A dry gem sprays nothing");
            Assert.Zero(run.MagicWater,"Still touching: still spraying, so no refill");Assert.False(run.GemPassiveRefilling);
            for(int i=0;i<15;i++)run.TickMagic(.1f,false,true);Assert.Zero(run.MagicWater);
            for(int i=0;i<4;i++)run.TickMagic(.1f,false,true);Assert.AreEqual(1,run.MagicWater,.001f);
            run.PumpGem(4);Assert.AreEqual(5,run.MagicWater,.001f);
            run.TickMagic(.1f,false,true);Assert.AreEqual(5.25f,run.MagicWater,.001f,"Pumping does not reset the passive refill");
        }
        [Test] public void LegacyPalmWaterKeepsItsTimedRecharge()
        {
            var run=new FireRun();run.StartStage();
            Assert.False(run.GemMode);Assert.AreEqual(30,run.MagicCapacity);Assert.AreEqual(30,run.MagicWater);
            for(int i=0;i<30;i++)run.TickMagic(.1f,true,true);Assert.Zero(run.MagicWater);
            run.TickMagic(.1f,false,true);Assert.AreEqual(.6f,run.MagicWater,.0001f,"Legacy recharge has no wait");
        }

        // ---- Fist hammer ----
        static readonly Vector3 Top=new(.3f,.9f,.4f);
        static EmberdeepFistHammer.Hand Fist(float height,float lateral=0,float closure=.95f)=>new(true,Top+Vector3.up*height+Vector3.right*lateral,closure);
        static readonly EmberdeepFistHammer.Hand None=default;
        // Holds the hand still at 'from', then moves it down at 'speed' until it is at 'to'.
        static int Swing(EmberdeepFistHammer hammer,int hand,float from,float to,float speed,float lateral=0,float closure=.95f)
        {
            int strikes=0;
            for(int i=0;i<4;i++)hammer.Sample(hand==0?Fist(from,lateral,closure):None,hand==1?Fist(from,lateral,closure):None,Top,Vector3.up,Dt);
            for(float h=from;h>to;)
            {
                h=Mathf.Max(to,h-speed*Dt);
                if(hammer.Sample(hand==0?Fist(h,lateral,closure):None,hand==1?Fist(h,lateral,closure):None,Top,Vector3.up,Dt)){strikes++;Assert.AreEqual(hand,hammer.StrikeHand);}
            }
            return strikes;
        }
        [Test] public void AFastFistDownThroughTheCapIsOneStrikeThenItMustRearm()
        {
            var hammer=new EmberdeepFistHammer();
            Assert.AreEqual(1,Swing(hammer,0,.18f,-.02f,1.2f));
            Assert.GreaterOrEqual(hammer.StrikeSpeed,0f);
            Assert.AreEqual(0,Swing(hammer,0,EmberdeepWaterTuning.RearmHeight*.8f,-.02f,1.5f),"Bouncing on the cap without lifting earns nothing");
            Assert.AreEqual(1,Swing(hammer,0,EmberdeepWaterTuning.RearmHeight+.02f,-.02f,1.2f),"Lifting above the rearm height arms the next strike");
            Assert.AreEqual(1,Swing(hammer,1,.2f,-.02f,1.2f),"Either hand can hammer");
        }
        [Test] public void SlowOpenOrOffTargetHandsNeverStrike()
        {
            Assert.AreEqual(0,Swing(new EmberdeepFistHammer(),0,.18f,-.02f,.2f),"Lowering a fist slowly onto the cap is not a strike");
            Assert.AreEqual(0,Swing(new EmberdeepFistHammer(),0,.18f,-.02f,1.4f,0,.2f),"An open hand slapping down is not a fist");
            Assert.AreEqual(0,Swing(new EmberdeepFistHammer(),0,.18f,-.02f,1.4f,EmberdeepWaterTuning.StrikeRadius+.03f),"A fist beside the pump misses");
            Assert.AreEqual(1,Swing(new EmberdeepFistHammer(),0,.18f,-.02f,1.4f,EmberdeepWaterTuning.StrikeRadius*.7f),"Off-centre on the cap still counts");
        }
        [Test] public void LostTrackingDisarmsAndInvalidSamplesAreIgnored()
        {
            var hammer=new EmberdeepFistHammer();
            for(int i=0;i<4;i++)hammer.Sample(Fist(.2f),None,Top,Vector3.up,Dt);Assert.True(hammer.Armed(0));
            hammer.Sample(None,None,Top,Vector3.up,Dt);Assert.False(hammer.Armed(0),"Tracking loss disarms");
            Assert.False(hammer.Sample(Fist(.01f),None,Top,Vector3.up,Dt),"Reappearing on the cap is not a strike");
            Assert.False(hammer.Sample(new EmberdeepFistHammer.Hand(true,new Vector3(float.NaN,0,0),1),None,Top,Vector3.up,Dt));
            Assert.False(hammer.Sample(Fist(.2f),None,Top,Vector3.up,float.NaN));Assert.False(hammer.Armed(0));
            Assert.False(hammer.Sample(Fist(.2f),None,Top,Vector3.zero,Dt),"A degenerate pump axis resets");
        }

        // ---- Rhythm ----
        [Test] public void FirstStrikeSetsTheBeatAndOnlyStrikesInTheWindowEarnTheBonus()
        {
            var rhythm=new EmberdeepPumpRhythm();const double t0=10;float beat=EmberdeepWaterTuning.BeatSeconds,window=EmberdeepWaterTuning.BeatWindowSeconds;
            Assert.False(rhythm.Active(t0));Assert.False(rhythm.Strike(t0),"The first strike only starts the beat");
            Assert.True(rhythm.Active(t0+.1));
            Assert.True(rhythm.Strike(t0+beat+window*.5),"Inside the window");Assert.AreEqual(1,rhythm.Streak);
            Assert.False(rhythm.Strike(t0+2*beat+window*1.6),"Late");Assert.AreEqual(0,rhythm.Streak);
            Assert.True(rhythm.Strike(t0+3*beat-window*.5),"An off-beat strike does not move the beat");
            Assert.True(rhythm.InWindow(t0+4*beat));Assert.False(rhythm.InWindow(t0+4*beat+window*1.5));
            Assert.AreEqual(.5f,rhythm.Phase01(t0+4.5*beat),.001f);
            double later=t0+3*beat+EmberdeepWaterTuning.RhythmSessionSeconds+.1;
            Assert.False(rhythm.Active(later),"A pause ends the run");
            Assert.False(rhythm.Strike(later),"The next strike starts a new beat");
            Assert.True(rhythm.Strike(later+beat),"...measured from that strike");
            Assert.False(rhythm.Strike(double.NaN));
        }

        // ---- Water hand ----
        [Test] public void WaterHandIsStoredInPlayerPrefsAndDefaultsToLeft()
        {
            bool had=PlayerPrefs.HasKey(EmberdeepWaterHand.PrefsKey);int saved=PlayerPrefs.GetInt(EmberdeepWaterHand.PrefsKey,0);
            int changes=0;void Count()=>changes++;
            EmberdeepWaterHand.Changed+=Count;
            try
            {
                PlayerPrefs.DeleteKey(EmberdeepWaterHand.PrefsKey);EmberdeepWaterHand.Reload();
                Assert.True(EmberdeepWaterHand.Left);Assert.AreEqual(0,EmberdeepWaterHand.Index);Assert.AreEqual(1,EmberdeepWaterHand.OtherIndex);
                EmberdeepWaterHand.Set(false);
                Assert.False(EmberdeepWaterHand.Left);Assert.AreEqual(1,EmberdeepWaterHand.Index);Assert.AreEqual(EmberdeepWaterHand.RightValue,PlayerPrefs.GetInt(EmberdeepWaterHand.PrefsKey));
                Assert.AreEqual(1,changes);EmberdeepWaterHand.Set(false);Assert.AreEqual(1,changes,"Setting the same hand is not a change");
                EmberdeepWaterHand.Reload();Assert.False(EmberdeepWaterHand.Left,"The choice survives a reload");
                EmberdeepWaterHand.Toggle();Assert.True(EmberdeepWaterHand.Left);Assert.AreEqual(2,changes);
                PlayerPrefs.SetInt(EmberdeepWaterHand.PrefsKey,7);EmberdeepWaterHand.Reload();Assert.True(EmberdeepWaterHand.Left,"Unknown values fall back to left");
            }
            finally
            {
                EmberdeepWaterHand.Changed-=Count;
                if(had)PlayerPrefs.SetInt(EmberdeepWaterHand.PrefsKey,saved);else PlayerPrefs.DeleteKey(EmberdeepWaterHand.PrefsKey);
                EmberdeepWaterHand.Reload();
            }
        }
    }
}

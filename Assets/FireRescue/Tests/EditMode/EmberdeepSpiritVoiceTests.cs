using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Step=FireRescue.EmberdeepCampTutorialFlow.Step;
using Lines=FireRescue.EmberdeepSpiritLines;
using Facts=FireRescue.EmberdeepSpiritDirector.Facts;

namespace FireRescue.Tests
{
    // Camp tutorial voice-over (2026-11-22): the forest spirit's line table, its clips and subtitle rows, which line plays
    // when (EmberdeepSpiritDirector: skip a line whose deed is done, reactions, reminders), and the steps waiting for it.
    public sealed class EmberdeepSpiritVoiceTests
    {
        [Test] public void EveryLineHasItsClipAndOrderedCardsInsideTheSpeech()
        {
            Assert.AreEqual(15,Lines.All.Length);
            Assert.AreEqual(Lines.All.Length,Lines.All.Select(l=>l.Id).Distinct().Count(),"ids are unique");
            foreach(var line in Lines.All)
            {
                var clip=Resources.Load<AudioClip>(line.Resource);
                Assert.NotNull(clip,line.Id+": clip at Resources/"+line.Resource);
                Assert.AreEqual(line.Seconds,clip.length,.05f,line.Id+": table length = clip length");
                Assert.AreEqual(1,clip.channels,line.Id+" is mono");
                Assert.Less(line.SpeechEnd,line.Seconds,line.Id);Assert.LessOrEqual(line.DoneAt,line.Seconds,line.Id);
                Assert.Greater(line.Cards.Length,0,line.Id);Assert.AreEqual(0,line.Cards[0].At,line.Id+": the first card shows at once");
                for(int i=1;i<line.Cards.Length;i++)
                {
                    Assert.Greater(line.Cards[i].At,line.Cards[i-1].At,line.Id+" cards in order");
                    Assert.Less(line.Cards[i].At,line.SpeechEnd,line.Id+" cards start while it speaks");
                }
                Assert.AreEqual(line.Cards[^1].Text,line.CardAt(line.Seconds),line.Id+": the last card stays to the end");
                foreach(var card in line.Cards)Assert.LessOrEqual(card.Text.Length,80,line.Id+": a card fits in 2-3 lines");
            }
        }
        // Owner's headset test 2026-11-22: chained lines had no audible gap. Last word to next first word = AfterWords +
        // PauseAfter + the next clip's ~.12 s lead.
        [Test] public void ChainedLinesLeaveAnAudibleGap()
        {
            foreach(var line in Lines.All)
            {
                if(line==Lines.Outro)continue; // the last line: the spirit leaves after it
                float gap=Lines.AfterWords+line.PauseAfter+.12f;
                Assert.GreaterOrEqual(gap,1f,line.Id+": ~1 s of silence before the next line");
            }
            Assert.Greater(Lines.Intro1.PauseAfter,Lines.Intro2.PauseAfter,"a longer beat after the hello");
            Assert.Greater(Lines.PumpJoke.PauseAfter,Lines.PumpHow.PauseAfter,"a joke gets room to land");
        }
        // Cards switch at their measured times, the line ends just after its last word, then the spirit stays quiet for
        // the line's pause (Busy) before the next may start. Silent clock (VoiceSpeed 2: no audio in EditMode).
        [Test] public void CardsFollowTheLineClockAndAPauseFollowsTheLine()
        {
            var go=new GameObject("Spirit test");float speed=EmberdeepForestSpirit.VoiceSpeed;
            try
            {
                EmberdeepForestSpirit.VoiceSpeed=2;var spirit=go.AddComponent<EmberdeepForestSpirit>();var line=Lines.Intro1;
                spirit.Say(line);
                void Run(float lineSeconds){for(float t=0;t<lineSeconds/2-1e-4f;t+=.01f)spirit.Tick(.01f,false,true,null,Vector3.zero,Vector3.up,Vector3.up);}
                Run(line.Cards[1].At-.05f);Assert.AreEqual(line.Cards[0].Text,spirit.Card,"first card until the second sentence starts");
                Run(.1f);Assert.AreEqual(line.Cards[1].Text,spirit.Card,"second card as its first word starts");
                Run(line.Cards[2].At-line.Cards[1].At);Assert.AreEqual(line.Cards[2].Text,spirit.Card);
                Run(line.DoneAt-line.Cards[2].At-.1f);Assert.IsTrue(spirit.Speaking,"still speaking");
                Run(.2f);Assert.IsFalse(spirit.Speaking);Assert.IsTrue(spirit.Busy,"then a pause");
                Assert.AreEqual(line.PauseAfter,spirit.PauseLeft,.15f);
                Run(line.PauseAfter+.1f);Assert.IsFalse(spirit.Busy,"free for the next line");
                spirit.Say(Lines.Intro2);spirit.Silence();Assert.IsFalse(spirit.Busy,"SKIP clears the pause too");
            }
            finally{EmberdeepForestSpirit.VoiceSpeed=speed;Object.DestroyImmediate(go);EmberdeepAudioSettings.SetDuck(1);}
        }
        // Plan-phase lines (owner's recording 2026-11-22): clip, timing and a zh row for every card.
        [Test] public void PlanLinesHaveTheirClipsTimedCardsAndZhRows()
        {
            var rows=File.ReadAllLines("Assets/FireRescue/Localization/Translations.tsv").Where(l=>l.Contains('\t'))
                .Select(l=>l.Split('\t')).GroupBy(c=>c[0]).ToDictionary(g=>g.Key,g=>g.First()[1]);
            foreach(var line in Lines.Plan)
            {
                var clip=Resources.Load<AudioClip>(line.Resource);
                Assert.NotNull(clip,line.Id+": clip at Resources/"+line.Resource);
                Assert.AreEqual(line.Seconds,clip.length,.05f,line.Id+": table length = clip length");
                Assert.Less(line.SpeechEnd,line.Seconds,line.Id);Assert.AreEqual(0,line.Cards[0].At,line.Id);
                for(int i=1;i<line.Cards.Length;i++)
                {Assert.Greater(line.Cards[i].At,line.Cards[i-1].At,line.Id);Assert.Less(line.Cards[i].At,line.SpeechEnd,line.Id);}
                foreach(var card in line.Cards)
                {
                    Assert.LessOrEqual(card.Text.Length,80,line.Id);
                    Assert.IsTrue(rows.TryGetValue(card.Text,out var zh) && zh.Any(c=>c>=0x2E80),line.Id+": zh row for \""+card.Text+"\"");
                }
            }
        }
        [Test] public void EverySubtitleCardHasAZhHansRow()
        {
            var rows=File.ReadAllLines("Assets/FireRescue/Localization/Translations.tsv").Where(l=>l.Contains('\t'))
                .Select(l=>l.Split('\t')).GroupBy(c=>c[0]).ToDictionary(g=>g.Key,g=>g.First()[1]); // other sessions' duplicate rows are harmless
            foreach(var line in Lines.All)foreach(var card in line.Cards)
            {
                Assert.IsTrue(rows.TryGetValue(card.Text,out var zh),line.Id+": no Translations.tsv row for \""+card.Text+"\"");
                Assert.IsTrue(zh.Any(c=>c>=0x2E80),line.Id+": the zh row is Chinese");
            }
        }
        [Test] public void SubtitlesWrapIntoBalancedLines()
        {
            var en=EmberdeepForestSpirit.Wrap(Lines.Outro.Cards[1].Text);
            Assert.That(en.Split('\n').All(l=>l.Length<=EmberdeepForestSpirit.WrapLatin+3),en);
            Assert.AreEqual(Lines.Outro.Cards[1].Text,en.Replace('\n',' '),"English breaks at spaces only");
            var zh=EmberdeepForestSpirit.Wrap("还是跟人类待久了，你把什么都当成理所当然？");
            var rows=zh.Split('\n');Assert.Greater(rows.Length,1,zh);
            Assert.That(rows.All(r=>r.Length<=EmberdeepForestSpirit.WrapCjk+1),zh);
            Assert.That(rows.Skip(1).All(r=>"，。？！".IndexOf(r[0])<0),"no line starts with punctuation: "+zh);
            Assert.AreEqual("Go on, top it up.",EmberdeepForestSpirit.Wrap("Go on, top it up."));
        }

        // ---- Director ----------------------------------------------------------------------------------------------
        // Quiet frames for up to seconds; stops at (and returns) the first line started.
        static EmberdeepSpiritLine Run(EmberdeepSpiritDirector d,ref Facts f,float seconds,bool speaking=false)
        {
            for(float t=0;t<seconds;t+=.05f){var line=d.Tick(.05f,f,speaking);if(line!=null)return line;}
            return null;
        }
        [Test] public void TheIntroWaitsForTheArrivalThenPlaysBothLinesInOrder()
        {
            var d=new EmberdeepSpiritDirector();d.Begin();var f=new Facts{Step=Step.Intro};
            Assert.IsNull(d.Tick(.05f,f,false),"Not a word while it comes out of the tent");
            Assert.AreSame(Lines.Intro1,Run(d,ref f,EmberdeepSpiritDirector.IntroDelay));
            Assert.IsNull(d.Tick(.05f,f,true),"One line at a time");Assert.IsFalse(d.IntroDone);
            Assert.AreSame(Lines.Intro2,d.Tick(.05f,f,false));Assert.IsTrue(d.IntroDone);Assert.IsTrue(d.QueueEmpty);
        }
        [Test] public void CampfireLinesAreSkippedWhenTheDeedIsDone()
        {
            // Put out during the intro: neither campfire line plays.
            var d=new EmberdeepSpiritDirector();d.Begin();var f=new Facts{Step=Step.Campfire,CampfireOut=true};
            Assert.IsNull(Run(d,ref f,1));Assert.IsTrue(d.QueueEmpty);Assert.IsFalse(d.Said(Lines.FireAim));
            // Already spraying while the aim line plays: the press line is dropped, the "too fiddly" reaction comes next.
            d=new EmberdeepSpiritDirector();d.Begin();f=new Facts{Step=Step.Campfire};
            Assert.AreSame(Lines.FireAim,d.Tick(.05f,f,false));
            f.Spraying=true;d.Tick(.05f,f,true);f.Spraying=false;
            Assert.AreSame(Lines.FireFirstSpray,d.Tick(.05f,f,false));Assert.IsFalse(d.Said(Lines.FirePress));
            // The fire goes out under a playing line: that line is never cut (the flow waits), nothing else follows.
            d=new EmberdeepSpiritDirector();d.Begin();f=new Facts{Step=Step.Campfire};
            Assert.AreSame(Lines.FireAim,d.Tick(.05f,f,false));f.CampfireOut=true;
            Assert.IsNull(d.Tick(.05f,f,true));Assert.IsNull(d.Tick(.05f,f,false));Assert.IsTrue(d.QueueEmpty);
        }
        [Test] public void RemindersComeAfterSilenceOnlyWhileTheDeedIsUndone()
        {
            var d=new EmberdeepSpiritDirector();d.Begin();var f=new Facts{Step=Step.Campfire};
            Assert.AreSame(Lines.FireAim,d.Tick(.05f,f,false));Assert.AreSame(Lines.FirePress,d.Tick(.05f,f,false));
            Assert.IsNull(Run(d,ref f,EmberdeepSpiritDirector.NagAfter-.3f),"Quiet a while first");
            Assert.AreSame(Lines.FireNag1,Run(d,ref f,.6f));
            Assert.IsNull(Run(d,ref f,EmberdeepSpiritDirector.SecondNagAfter-.3f));
            Assert.AreSame(Lines.FireNag2,Run(d,ref f,.6f));
            Assert.IsNull(Run(d,ref f,30),"Two reminders, then the ghost hand alone");
            // Spraying (missing, say) holds the reminder.
            d=new EmberdeepSpiritDirector();d.Begin();f=new Facts{Step=Step.Campfire};d.Tick(.05f,f,false);d.Tick(.05f,f,false);
            f.Spraying=true;Assert.AreSame(Lines.FireFirstSpray,d.Tick(.05f,f,false));
            Assert.IsNull(Run(d,ref f,EmberdeepSpiritDirector.NagAfter+2),"no reminder while the water flows");
        }
        [Test] public void PumpLinesFollowTheStrikes()
        {
            var d=new EmberdeepSpiritDirector();d.Begin();var f=new Facts{Step=Step.Pump,PumpStrikes=3}; // strikes from before
            Assert.AreSame(Lines.PumpJoke,d.Tick(.05f,f,false));
            // The how-to is never skipped (the camp pump stays hidden until it is said: EmberdeepCampPlay.PumpHeld).
            f.PumpStrikes=4;d.Tick(.05f,f,true);
            Assert.AreSame(Lines.PumpHow,d.Tick(.05f,f,false),"the how-to always plays");
            Assert.IsNull(Run(d,ref f,EmberdeepSpiritDirector.NagAfter+1),"no reminder once struck");
            f.PumpMet=true;Assert.AreSame(Lines.PumpDone,d.Tick(.05f,f,false));
            Assert.IsNull(Run(d,ref f,3),"once");
            // Not struck at all: the how-to, then one reminder.
            d=new EmberdeepSpiritDirector();d.Begin();f=new Facts{Step=Step.Pump};
            Assert.AreSame(Lines.PumpJoke,d.Tick(.05f,f,false));Assert.AreSame(Lines.PumpHow,d.Tick(.05f,f,false));
            Assert.AreSame(Lines.PumpNag,Run(d,ref f,EmberdeepSpiritDirector.NagAfter+.3f));
        }
        [Test] public void TheSheepScareCutsInAndThePetLineIsSkippedIfAlreadyPetted()
        {
            var d=new EmberdeepSpiritDirector();d.Begin();var f=new Facts{Step=Step.Sheep};
            Assert.IsNull(Run(d,ref f,2),"Nothing until the spark lands");
            f.SheepLit=true;f.SheepBurning=true;Assert.AreSame(Lines.SheepFire,d.Tick(.05f,f,false));
            f.SaplingBurning=true;Assert.AreSame(Lines.SheepNag,Run(d,ref f,EmberdeepSpiritDirector.SheepNagAfter+.3f));
            d=new EmberdeepSpiritDirector();d.Begin();f=new Facts{Step=Step.Sheep,SheepLit=true,SheepBurning=true};
            d.Tick(.05f,f,false);f.SheepBurning=false;
            Assert.IsNull(Run(d,ref f,EmberdeepSpiritDirector.SheepNagAfter+1),"No reminder once both are out");
            f.Step=Step.Pet;f.Petted=true;Assert.IsNull(d.Tick(.05f,f,false),"Petted before it asked");
            f.Step=Step.Done;Assert.AreSame(Lines.Outro,d.Tick(.05f,f,false));
        }

        // ---- Flow --------------------------------------------------------------------------------------------------
        [Test] public void StepsWaitForTheSpiritToStopTalking()
        {
            var flow=new EmberdeepCampTutorialFlow();flow.Begin(true);
            Assert.AreEqual(Step.Intro,flow.Current);Assert.AreEqual(-1,flow.Chip,"No chip lit during the intro");
            for(int i=0;i<40;i++)flow.Tick(.05f,true,true,true,true,false,true);
            Assert.AreEqual(Step.Intro,flow.Current,"Only the intro's last line moves on");
            for(int i=0;i<40;i++)flow.Tick(.05f,false,false,false,false,true,false);
            Assert.AreEqual(Step.Intro,flow.Current,"Not while the spirit talks");Assert.IsTrue(flow.Completing);
            for(int i=0;i<20;i++)flow.Tick(.05f,false,false,false,false,true,true);
            Assert.AreEqual(Step.Campfire,flow.Current);
            // Done: the celebration and the outro both play out, then a quiet beat.
            flow=new EmberdeepCampTutorialFlow();flow.Begin();
            foreach(var _ in new[]{1,2,3,4})for(int i=0;i<25;i++)flow.Tick(.05f,true,true,true,true,true,true);
            Assert.AreEqual(Step.Done,flow.Current);
            for(int i=0;i<200;i++)flow.Tick(.05f,false,false,false,false,true,false);
            Assert.IsFalse(flow.Finished,"Not while the outro plays");
            for(int i=0;i<30;i++)flow.Tick(.05f,false,false,false,false,true,true);
            Assert.IsTrue(flow.Finished);
        }
    }
}

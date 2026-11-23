using System.Collections;
using System.Linq;
using NUnit.Framework;
using PhysicalAssembly;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Lines=FireRescue.EmberdeepSpiritLines;

namespace FireRescue.Tests
{
    // Plan-phase voice-over (2026-11-22) in the real fireground: TOWER HILL's Plan says plan.tower once, but only if its
    // clip is recorded. With no clip (the ship-safe default) there is no spirit at all and the level plays as before.
    // Never under the level select (headset, 2026-11-22): results NEXT lays TOWER HILL out in Plan behind it, and the line
    // waits for PLAY; a select opened mid-line sends the spirit away and the line is not counted as heard.
    public sealed class EmberdeepPlanSpiritPlayTests
    {
        int?[] saved;
        [SetUp] public void Save()
        {
            saved=Lines.Plan.Select(l=>PlayerPrefs.HasKey(EmberdeepPlanSpirit.Key(l))?(int?)PlayerPrefs.GetInt(EmberdeepPlanSpirit.Key(l)):null).ToArray();
            EmberdeepPlanSpirit.ResetAll();EmberdeepForestSpirit.VoiceSpeed=30;
        }
        [TearDown] public void Restore()
        {
            EmberdeepForestSpirit.VoiceSpeed=1;
            for(int i=0;i<Lines.Plan.Length;i++){var k=EmberdeepPlanSpirit.Key(Lines.Plan[i]);if(saved[i].HasValue)PlayerPrefs.SetInt(k,saved[i].Value);else PlayerPrefs.DeleteKey(k);}
            PlayerPrefs.Save();
        }
        // An unfocused editor sends OnApplicationFocus(false), which holds the game's Update (and the spirit) still.
        static void Focus(){var g=Object.FindFirstObjectByType<FireRescueGame>();if(g)g.SendMessage("OnApplicationFocus",true);}
        static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until){Focus();yield return null;}}
        static void Clear(FireRun run){foreach(var a in run.Animals)run.ApplyAnimalWater(a.Position,100,.6f);foreach(var t in run.Trees)if(t.State==TreeState.Burning)run.ApplyWater(t.Position,1000,.1f);}
        static void AdvanceTo(FireRun run,int stage)
        {
            for(int guard=0;guard<20 && run.Stage<stage;guard++)
            {
                if(run.Phase==FirePhase.Ready && !run.StartStage(true))break;
                EmberdeepTestFlow.StartPrepared(run);
                for(int i=0;i<400 && run.Phase==FirePhase.Fighting;i++){Clear(run);run.Tick(.05f,false);}
                if(run.Phase!=FirePhase.Cleared)break;
                for(int i=0;i<400 && run.AnimalsSaved<run.Animals.Length;i++)run.Tick(.05f,false);
                if(run.NeedsMagicUpgrade && !run.BuyMagicUpgrade())break;
                if(!run.StartStage(true))break;
            }
        }
        static WorkshopButton Button(FireRescueGame game,string command)=>game.GetComponentsInChildren<WorkshopButton>(true).Single(b=>b.Command==command);
        static bool Recorded=>Resources.Load<AudioClip>(Lines.PlanTower.Resource);
        static IEnumerator Load(int stage)
        {
            EmberdeepSession.NewExpedition();AdvanceTo(EmberdeepSession.Run,stage);Assert.AreEqual(stage,EmberdeepSession.Run.Stage);
            yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);yield return null;yield return null;
            Object.FindFirstObjectByType<FireRescueGame>().SendMessage("OnApplicationFocus",true);
            yield return Wait(1.2f);
        }

        [UnityTest] public IEnumerator TowerHillPlanSaysItsLineOnceAndOnlyWhenRecorded()
        {
            yield return Load(EmberdeepPlanSpirit.TowerStage);
            var game=Object.FindFirstObjectByType<FireRescueGame>();Assert.NotNull(game);
            Assert.AreEqual(FirePhase.Preparing,EmberdeepSession.Run.Phase,"in the Plan phase");
            bool recorded=Resources.Load<AudioClip>(Lines.PlanTower.Resource);
            yield return Wait(EmberdeepForestSpirit.ArriveSeconds+.6f);
            if(!recorded)
            {
                Assert.IsNull(game.PlanSpirit,"no clip: no spirit at all (ship-safe)");Assert.IsNull(game.PlanLine);
                Assert.IsFalse(EmberdeepPlanSpirit.Heard(Lines.PlanTower));
                Assert.AreEqual(1f,EmberdeepAudioSettings.Ducking,.001f,"nothing ducks the board's sound");
            }
            else
            {
                Assert.NotNull(game.PlanSpirit,"recorded: the spirit comes");Assert.AreSame(Lines.PlanTower,game.PlanLine);
                Assert.IsTrue(EmberdeepPlanSpirit.Heard(Lines.PlanTower),"said, and remembered");
                Assert.IsTrue(game.PlanSpirit.Visible);
            }
        }
        [UnityTest] public IEnumerator OtherLevelsHaveNoPlanSpirit()
        {
            yield return Load(1);
            var game=Object.FindFirstObjectByType<FireRescueGame>();
            yield return Wait(EmberdeepForestSpirit.ArriveSeconds+.3f);
            Assert.IsNull(game.PlanSpirit,"WELL HOLLOW has no plan line");Assert.IsNull(game.PlanLine);
        }

        [UnityTest] public IEnumerator ResultsNextIntoTowerHillWaitsForTheLevelSelectToClose()
        {
            // The owner's repro: WELL HOLLOW cleared, NEXT; TOWER HILL is already in Plan behind the level select.
            yield return Load(1);
            var game=Object.FindFirstObjectByType<FireRescueGame>();var run=game.Run;
            EmberdeepTestFlow.StartPrepared(run);yield return null;
            for(int i=0;i<400 && run.Phase==FirePhase.Fighting;i++){Clear(run);run.Tick(.05f,false);}
            Assert.AreEqual(FirePhase.Cleared,run.Phase);run.EvacuateSafeAnimals();
            yield return Wait(FireRescueGame.ResultsDelay+FireRescueGame.ResultsRise+.9f);
            Assert.True(game.ResultsVisible);
            Button(game,"Results next").Press();yield return null;yield return null;
            Assert.True(game.LevelSelectOpen);Assert.AreEqual(EmberdeepPlanSpirit.TowerStage,run.Stage);Assert.AreEqual(FirePhase.Preparing,run.Phase);
            yield return Wait(EmberdeepForestSpirit.ArriveSeconds+.6f);
            Assert.True(game.LevelSelectOpen,"still choosing");
            Assert.IsFalse(game.PlanSpirit && game.PlanSpirit.Visible,"no spirit over the level select");
            Assert.IsNull(game.PlanLine);Assert.IsFalse(EmberdeepPlanSpirit.Heard(Lines.PlanTower),"the line is still due");
            Assert.AreEqual(1f,EmberdeepAudioSettings.Ducking,.001f,"nothing ducks the level select");

            Assert.True(game.PlaySelectedLevel());yield return null;Assert.False(game.LevelSelectOpen);
            yield return Wait(EmberdeepForestSpirit.ArriveSeconds+.6f);
            if(!Recorded){Assert.IsNull(game.PlanSpirit,"no clip: no spirit at all (ship-safe)");Assert.IsNull(game.PlanLine);yield break;}
            Assert.NotNull(game.PlanSpirit,"PLAY: the spirit comes");Assert.AreSame(Lines.PlanTower,game.PlanLine);
            Assert.IsTrue(game.PlanSpirit.Visible);Assert.AreEqual(1,game.PlanSpirit.LinesStarted);
            Assert.IsTrue(EmberdeepPlanSpirit.Heard(Lines.PlanTower),"said, and remembered");
        }
        [UnityTest] public IEnumerator ALevelSelectOpenedMidLineSendsTheSpiritAwayUnheard()
        {
            EmberdeepForestSpirit.VoiceSpeed=2; // silent, and the line lasts ~13 s: long enough to cut
            yield return Load(EmberdeepPlanSpirit.TowerStage);
            var game=Object.FindFirstObjectByType<FireRescueGame>();
            if(!Recorded){Assert.IsNull(game.PlanSpirit,"no clip: no spirit at all (ship-safe)");yield break;}
            Assert.NotNull(game.PlanSpirit);
            float until=Time.realtimeSinceStartup+EmberdeepForestSpirit.ArriveSeconds+2;
            while(!game.PlanSpirit.Speaking && Time.realtimeSinceStartup<until){Focus();yield return null;}
            Assert.IsTrue(game.PlanSpirit.Speaking,"mid-line");Assert.IsTrue(EmberdeepPlanSpirit.Heard(Lines.PlanTower),"counts as heard as it starts");

            game.OpenLevelSelect(game.Run.Stage);yield return null;
            Assert.True(game.LevelSelectOpen);
            Assert.IsFalse(game.PlanSpirit.Speaking,"cut");Assert.AreEqual(EmberdeepForestSpirit.State.Leaving,game.PlanSpirit.Phase);
            Assert.IsFalse(EmberdeepPlanSpirit.Heard(Lines.PlanTower),"a cut line was not heard");
            yield return Wait(EmberdeepForestSpirit.LeaveSeconds+.5f);
            Assert.IsFalse(game.PlanSpirit.Visible,"gone while the level select is up");
            Assert.IsFalse(EmberdeepPlanSpirit.Heard(Lines.PlanTower));

            EmberdeepForestSpirit.VoiceSpeed=30;
            Assert.True(game.PlaySelectedLevel());yield return null;
            Assert.AreEqual(EmberdeepForestSpirit.State.Arriving,game.PlanSpirit.Phase,"back with the Plan");
            yield return Wait(EmberdeepForestSpirit.ArriveSeconds+.6f);
            Assert.AreEqual(2,game.PlanSpirit.LinesStarted,"said again, from the start");
            Assert.IsTrue(EmberdeepPlanSpirit.Heard(Lines.PlanTower),"and now heard");
        }
    }
}

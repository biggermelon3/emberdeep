using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    // Owner's first headset test (2026-11-22) in the fireground: the pump on the free-hand side with the tray clear behind
    // it, the ONE rhythm cue (a ring shrinking onto a fixed target ring) with a short good / off-beat flash, and the start
    // bell's alarm. Seated-eye captures go to Logs/EmberdeepHeadsetFix/ (the camp pump, the camp sheep and the first-play
    // prompt are captured by EmberdeepCampTutorialPlayTests).
    public sealed class EmberdeepHeadsetFixPlayTests
    {
        const string Output="Logs/EmberdeepHeadsetFix";
        FireRescueGame game;int[] hints;int waterHand;
        static readonly EmberdeepFirstHints.Move[] Moves=(EmberdeepFirstHints.Move[])System.Enum.GetValues(typeof(EmberdeepFirstHints.Move));

        // The seated desktop eye; look: the world point to centre, or null for the board view turned by yaw.
        void Capture(string name,Vector3? look=null,float pitch=44,float yaw=0,float fov=92)
        {
            Directory.CreateDirectory(Output);var camera=game.Player.ViewCamera;
            var rotation=camera.transform.rotation;float priorFov=camera.fieldOfView;var prior=camera.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(1200,1100,24,RenderTextureFormat.ARGB32){antiAliasing=4};var texture=new Texture2D(1200,1100,TextureFormat.RGBA32,false);
            try
            {
                camera.transform.rotation=look.HasValue?Quaternion.LookRotation(look.Value-camera.transform.position,Vector3.up)
                    :Quaternion.LookRotation(game.Arena.forward,Vector3.up)*Quaternion.Euler(pitch,yaw,0);
                camera.fieldOfView=fov;
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1200,1100),0,0);texture.Apply();
                File.WriteAllBytes(Output+"/"+name+".png",texture.EncodeToPNG());
            }
            finally{camera.transform.rotation=rotation;camera.fieldOfView=priorFov;camera.targetTexture=prior;RenderTexture.active=active;target.Release();Object.Destroy(target);Object.Destroy(texture);}
        }
        static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
        static void Clear(FireRun run){foreach(var a in run.Animals)run.ApplyAnimalWater(a.Position,100,.6f);foreach(var t in run.Trees)if(t.State==TreeState.Burning)run.ApplyWater(t.Position,1000,.1f);run.ApplyWater(Vector3.zero,10000,100);}
        static void AdvanceToIncident(FireRun run,int incident)
        {
            for(int guard=0;guard<20;guard++)
            {
                if(run.Phase==FirePhase.Ready && !run.StartStage(true))break;
                if(run.IncidentIndex==incident && !run.IsPalmLesson && run.Phase==FirePhase.Preparing)return;
                EmberdeepTestFlow.StartPrepared(run);
                for(int i=0;i<400 && run.Phase==FirePhase.Fighting;i++){Clear(run);run.Tick(.05f,false);}
                if(run.Phase!=FirePhase.Cleared)break;
                for(int i=0;i<400 && run.AnimalsSaved<run.Animals.Length;i++)run.Tick(.05f,false);
                if(run.NeedsMagicUpgrade && !run.BuyMagicUpgrade())break;
                if(!run.StartStage(true))break;
            }
            Assert.AreEqual(incident,run.IncidentIndex);Assert.AreEqual(FirePhase.Preparing,run.Phase);
        }
        IEnumerator Load(int incident)
        {
            hints=Moves.Select(m=>PlayerPrefs.GetInt(EmberdeepFirstHints.Key(m),-1)).ToArray();
            foreach(var m in Moves)PlayerPrefs.SetInt(EmberdeepFirstHints.Key(m),99);
            waterHand=PlayerPrefs.GetInt(EmberdeepWaterHand.PrefsKey,-1);EmberdeepWaterHand.Set(true);
            EmberdeepSession.NewExpedition();EmberdeepSession.Run.LevelOverride=EmberdeepLevels.GeneratedGrid(incident+FireRun.PalmLessons);
            AdvanceToIncident(EmberdeepSession.Run,incident);
            yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);yield return null;yield return null;
            game=Object.FindFirstObjectByType<FireRescueGame>();game.SendMessage("OnApplicationFocus",true);
            yield return Wait(.6f);
        }
        [TearDown] public void Restore()
        {
            EmberdeepWaterTuning.RhythmEnabled=EmberdeepWaterTuning.RhythmEnabledDefault;
            if(hints!=null)for(int i=0;i<Moves.Length;i++)if(hints[i]<0)PlayerPrefs.DeleteKey(EmberdeepFirstHints.Key(Moves[i]));else PlayerPrefs.SetInt(EmberdeepFirstHints.Key(Moves[i]),hints[i]);
            if(waterHand<0)PlayerPrefs.DeleteKey(EmberdeepWaterHand.PrefsKey);else PlayerPrefs.SetInt(EmberdeepWaterHand.PrefsKey,waterHand);
            PlayerPrefs.Save();EmberdeepWaterHand.Reload();hints=null;
        }
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
        IEnumerator UntilPhase(float phase,float within)
        {
            double start=Time.unscaledTimeAsDouble;
            while(Mathf.Abs(game.PumpRhythm.Phase01(Time.unscaledTimeAsDouble)-phase)>within && Time.unscaledTimeAsDouble-start<2)yield return null;
        }

        [UnityTest] public IEnumerator PumpOnTheFreeHandSideWithTheTrayBehindItAndOneRhythmCue()
        {
            yield return Load(3);
            var run=game.Run;var view=game.PieceView;var hud=game.RimHud;
            Assert.AreEqual(FirePhase.Preparing,run.Phase);Assert.True(view.TrayVisible,"This level has pieces in the tray");
            // Left water hand: the pump and the tray both stand on the free (right) side; the pump keeps the front.
            var cap=game.Arena.InverseTransformPoint(game.Pump.CapTop);var tray=game.Arena.InverseTransformPoint(view.Tray.position);
            Assert.Greater(cap.x,0,"Pump on the free-hand side");Assert.AreEqual(hud.Board.center.x,tray.x,.01f,"Tray centred on the front lip (headset test 4)");
            // The pump stands in the upright frame (= the arena when flat; the editor's saved prefs may leave the board Tilted).
            var capUpright=game.UprightToWorld.inverse.MultiplyPoint3x4(game.Pump.CapTop);
            Assert.AreEqual(EmberdeepWaterTuning.PumpForward,capUpright.z,.01f,"The pump keeps its front corner");
            Assert.Greater(cap.x-tray.x-EmberdeepPieceView.TrayWidth*.5f,0,"The pump stands beside the widest tray, not over it");
            float nearest=float.MaxValue;
            for(int i=0;i<run.Pieces.Length;i++)
            {
                var piece=view.Piece(i);if(!piece || !piece.gameObject.activeInHierarchy)continue;
                var d=piece.position-game.Pump.CapTop;d=Vector3.ProjectOnPlane(d,game.Pump.Up);nearest=Mathf.Min(nearest,d.magnitude);
            }
            Assert.Less(nearest,float.MaxValue);
            Assert.Greater(nearest,EmberdeepWaterTuning.StrikeRadius+EmberdeepPieceGrasp.GrabRadius,"No tray piece within a hammering fist's grab reach: "+nearest);
            yield return Wait(.3f);
            Capture("1-plan-free-side-pump-and-tray",null,40,26);

            // The start bell rings its alarm.
            game.SendMessage("OnApplicationFocus",true); // an unfocused editor holds the countdown still
            int alarms=game.Feedback.Alarms;Assert.True(game.StrikeBell());Assert.AreEqual(alarms+1,game.Feedback.Alarms,"Striking the bell plays the alarm");
            for(float until=Time.realtimeSinceStartup+4.5f;Time.realtimeSinceStartup<until && run.Phase==FirePhase.Preparing;){game.SendMessage("OnApplicationFocus",true);yield return null;}
            Assert.AreEqual(FirePhase.Fighting,run.Phase);yield return null;

            // An empty gem lights the pump; the first strike starts the beat (plain).
            for(int i=0;i<40 && !run.GemEmpty;i++)run.TickMagic(.1f,true,true);
            yield return Wait(.5f);
            Capture("2-gem-empty-pump-glows",null,40,26);
            Assert.True(Hammer(1),"The free (right) fist strikes");Assert.AreEqual(EmberdeepPumpGrade.Plain,game.Pump.LastGrade);
            yield return Wait(.3f);
            // Mid-beat: the one cue, a ring closing on the fixed target ring.
            yield return UntilPhase(.5f,.08f);
            Assert.True(game.Pump.RhythmVisible);Assert.Greater(game.Pump.ApproachRadius,game.Pump.TargetRadius*1.2f);Assert.Zero(game.Pump.Flash);
            Capture("3-one-cue-ring-closing",game.Pump.CapTop,0,0,52);
            yield return UntilPhase(.93f,.04f);
            Assert.Less(game.Pump.ApproachRadius,game.Pump.TargetRadius*1.15f,"Almost meeting the target");
            Capture("3b-one-cue-ring-meeting",game.Pump.CapTop,0,0,52);
            // On the beat: a short gold flash.
            for(double s=Time.unscaledTimeAsDouble;!game.PumpRhythm.InWindow(Time.unscaledTimeAsDouble) && Time.unscaledTimeAsDouble-s<1;)yield return null;
            Assert.True(Hammer(1));Assert.AreEqual(EmberdeepPumpGrade.OnBeat,game.Pump.LastGrade);
            yield return null;Capture("4-good-pump-flash",game.Pump.CapTop,0,0,52);
            // Off the beat: a short rust flash.
            yield return UntilPhase(.5f,.1f);
            Assert.True(Hammer(0),"Either hand still strikes");Assert.AreEqual(EmberdeepPumpGrade.OffBeat,game.Pump.LastGrade);
            yield return null;Capture("5-offbeat-flash",game.Pump.CapTop,0,0,52);
            yield return Wait(.4f);Capture("6-fight-wide",null,40,26);
        }
    }
}

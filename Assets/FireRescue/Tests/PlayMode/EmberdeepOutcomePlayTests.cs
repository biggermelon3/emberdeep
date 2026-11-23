using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    public sealed class EmberdeepOutcomePlayTests
    {
        const string Output="Logs/EmberdeepOutcome";
        static void Capture(Camera camera,string name)
        {
            Directory.CreateDirectory(Output);var prior=camera.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(1400,1000,24,RenderTextureFormat.ARGB32){antiAliasing=4};var texture=new Texture2D(1400,1000,TextureFormat.RGBA32,false);
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1400,1000),0,0);texture.Apply();File.WriteAllBytes(Output+"/"+name+".png",texture.EncodeToPNG());}
            finally{camera.targetTexture=prior;RenderTexture.active=active;target.Release();Object.Destroy(target);Object.Destroy(texture);}
        }
        static void Clear(FireRun run){foreach(var a in run.Animals)run.ApplyAnimalWater(a.Position,100,.6f);foreach(var t in run.Trees)if(t.State==TreeState.Burning)run.ApplyWater(t.Position,1000,.1f);}
        static void Char(FireRun run,int count)
        {
            foreach(var t in run.Trees){if(count<=0)break;if(t.State==TreeState.Safe){t.State=TreeState.Burnt;t.Fuel=0;count--;}}
        }
        // Plays the goddess run forward through cleared stages so the scene opens on a real grove.
        static void AdvanceTo(FireRun run,int stage)
        {
            for(int guard=0;guard<20 && run.Stage<stage;guard++)
            {
                if(run.Phase==FirePhase.Ready && !run.StartStage(true))break;
                EmberdeepTestFlow.StartPrepared(run);
                for(int i=0;i<400 && run.Phase==FirePhase.Fighting;i++){Clear(run);run.Tick(.05f,false);}
                if(run.Phase!=FirePhase.Cleared)break;
                for(int i=0;i<400 && run.AnimalsSaved<run.Animals.Length;i++)run.Tick(.05f,false); // sheep rewards pay for the magic upgrade
                if(run.NeedsMagicUpgrade && !run.BuyMagicUpgrade())break;
                if(!run.StartStage(true))break;
            }
        }
        static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
        static Light Lamp(FireRescueGame game){foreach(var l in game.Arena.GetComponentsInChildren<Light>(true))if(l.name=="Warm evening sun")return l;return null;}

        [UnityTest] public IEnumerator LossAndClearPlayInWorldMomentsAndRetryRestoresTheBoard()
        {
            EmberdeepSession.NewExpedition();AdvanceTo(EmberdeepSession.Run,3); // THE GROVE: stage 3 since the stage cut (was 5)
            Assert.AreEqual(3,EmberdeepSession.Run.Stage,"Played forward to a real grove");Assert.AreEqual(2,EmberdeepSession.Run.IncidentIndex);
            yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);yield return null;yield return null;
            var game=Object.FindFirstObjectByType<FireRescueGame>();var view=game.Feedback.Outcome;
            Assert.NotNull(view,"FireFeedback creates the outcome view next to the game");
            EmberdeepTestFlow.StartPrepared(game.Run);yield return null;yield return null;
            Assert.AreEqual(FirePhase.Fighting,game.Run.Phase);
            int limit=game.Run.BurnLimit;
            Assert.True(view.Meter.gameObject.activeInHierarchy);Assert.AreEqual(limit,view.MeterPips);Assert.False(view.BannerVisible);
            var lamp=Lamp(game);Assert.NotNull(lamp);float intensity=lamp.intensity;var ambient=RenderSettings.ambientLight;Assert.Zero(view.VeilStrength);
            var camera=game.Player.ViewCamera;

            Char(game.Run,limit-1-game.Run.Burnt);yield return null;yield return Wait(EmberdeepOutcomeView.MeterPop+.1f);
            Assert.AreEqual(limit-1,view.MeterCharred,view.DebugState);Assert.AreEqual(FirePhase.Fighting,view.ObservedPhase,view.DebugState);Capture(camera,"meter-one-left");

            Char(game.Run,1);game.Run.Tick(.05f,false);Assert.AreEqual(FirePhase.Lost,game.Run.Phase);
            yield return null;
            Assert.True(view.BannerVisible,view.DebugState+" run="+game.Run.Phase);Assert.AreEqual(FirePhase.Lost,view.Moment);Assert.IsNotEmpty(view.BannerTitle);
            yield return Wait(EmberdeepOutcomeView.LossFlare+EmberdeepOutcomeView.LossDarken+.3f);
            Assert.Less(lamp.intensity,intensity*.5f,"The board darkens");
            Assert.Greater(view.VeilStrength,.5f,"The veil darkens the whole board");
            Assert.AreEqual(ambient,RenderSettings.ambientLight,"Global ambient is never touched");
            Assert.AreEqual(limit,view.MeterCharred);
            Assert.Greater(Vector3.Dot(view.Banner.forward,(view.Banner.position-camera.transform.position).normalized),.99f,"Banner faces the player");
            Capture(camera,"fail-banner");

            game.RetrySector();yield return Wait(EmberdeepOutcomeView.BannerHide+.2f);
            Assert.False(view.BannerVisible);Assert.AreEqual(intensity,lamp.intensity,1e-4f,"Retry restores the board light");
            Assert.Zero(view.VeilStrength);

            EmberdeepTestFlow.StartPrepared(game.Run);yield return null;
            for(int i=0;i<200 && game.Run.Phase==FirePhase.Fighting;i++){Clear(game.Run);game.Run.Tick(.05f,false);}
            Assert.AreEqual(FirePhase.Cleared,game.Run.Phase);yield return null;
            Assert.True(view.BannerVisible);Assert.AreEqual(FirePhase.Cleared,view.Moment);
            yield return Wait(.9f);Capture(camera,"clear-banner");
            yield return Wait(EmberdeepOutcomeView.ClearFlare+EmberdeepOutcomeView.ClearSettle);
            Assert.AreEqual(intensity,lamp.intensity,1e-3f,"The clear flare settles back");Assert.Zero(view.VeilStrength);
        }

        [UnityTest] public IEnumerator PetPlaysTheHopAndRescuedSheepStayFullSize()
        {
            var root=new GameObject("Sheep pet hop");
            try
            {
                var run=new FireRun();run.StartStage();var view=root.AddComponent<EmberdeepSheepView>();view.Initialize(run);
                view.Render();var a=run.Animals[0];
                Assert.True(view.Pet(0));view.Render();Assert.True(view.Hopping(0));Assert.AreEqual(1,view.Audio.PetsPlayed);
                Assert.False(view.Pet(0),"Cooldown");
                float peak=0,minStretch=1;
                for(float t=0;t<EmberdeepSheepHop.Duration+.1f;t+=.02f)
                {
                    a.AnimationTime+=.02f;view.Render();var sheep=view.SheepRoot(0);
                    peak=Mathf.Max(peak,sheep.localPosition.y-a.Position.y);minStretch=Mathf.Min(minStretch,sheep.localScale.y/.65f);
                }
                Assert.Greater(peak,.3f,"The hop leaves the ground");Assert.Less(minStretch,.8f,"and squashes");Assert.False(view.Hopping(0));
                yield return null;
                foreach(var s in run.Animals)run.ApplyAnimalWater(s.Position,100,.6f);
                foreach(var t in run.Trees)if(t.State==TreeState.Burning)run.ApplyWater(t.Position,1000,.1f);
                for(int i=0;i<400;i++)run.Tick(.05f,false);
                view.Render();
                for(int i=0;i<run.Animals.Length;i++)
                {
                    Assert.AreEqual(AnimalState.Rescued,run.Animals[i].State);
                    Assert.Greater(view.SheepRoot(i).GetChild(0).localScale.x,.8f,"Rescued sheep stay on the board to be petted");
                }
                Assert.True(view.Pet(1));view.Render();Assert.True(view.Hopping(1),"Rescued sheep boing too");
            }
            finally{Object.Destroy(root);}
            yield return null;
        }
    }
}

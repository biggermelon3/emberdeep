using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    // LoopSpec §4.1-4.4 (workstream L2) smoke check on the real fireground from the desktop seat:
    // grass, weathervane, wind streaks, wet look and forecast rings all present and live. Writes review
    // captures (full frame + crop) to Logs/EmberdeepBoard.
    public sealed class EmberdeepBoardCuesPlayTests
    {
        const string Folder="Logs/EmberdeepBoard";
        const int Stage=4; // incident 3 (stage 4 since the stage cut, was 6): 30 trees, 10 grass patches, wind to the left shifting toward the player

        [UnityTest] public IEnumerator GrassWindWetnessAndForecastShowOnTheBoard()
        {
            EmberdeepSession.NewExpedition();var run=EmberdeepSession.Run;
            typeof(FireRun).GetProperty("Stage").SetValue(run,Stage);
            run.LevelOverride=EmberdeepLevels.GeneratedGrid(Stage); // the generated grid described above (levels are authored since LV)
            Assert.IsTrue(run.StartStage(true));Assert.AreEqual(FirePhase.Preparing,run.Phase);
            var background=InputSystem.settings.backgroundBehavior;var editor=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var mouse=InputSystem.AddDevice<Mouse>();
            try
            {
                yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);yield return null;yield return null;
                var game=Object.FindFirstObjectByType<FireRescueGame>();Assert.NotNull(game);Assert.AreSame(run,game.Run);
                game.SendMessage("OnApplicationFocus",true);
                var camera=game.Player.ViewCamera;var arena=game.Arena;
                InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(4,Screen.height-4)});
                yield return null;yield return null;
                // Plan: grass in one draw, the vane into the wind, rings on the next catches.
                var grass=game.GrassView;Assert.NotNull(grass);Assert.AreEqual(run.Grass.Length,grass.Patches);
                Assert.AreEqual(1,grass.GetComponentsInChildren<MeshRenderer>().Length,"all grass shares one draw");
                Assert.IsTrue(grass.GetComponent<MeshRenderer>().enabled);
                var vane=game.Weathervane;Assert.NotNull(vane);Assert.IsTrue(vane.gameObject.activeInHierarchy);
                var upwind=-arena.TransformDirection(run.WindDirection);
                Assert.Greater(Vector3.Dot(Flat(vane.Pointing),Flat(upwind)),.95f,"the arrow points into the wind");
                int next=run.ForecastCell(0);Assert.GreaterOrEqual(next,0);
                Assert.AreEqual(EmberdeepBoardTuning.ForecastSlots,game.TreeReadout.Rings,"a ring per forecast catch, in Plan");
                Capture(camera,arena,run,next,"1-PlanForecastRings");

                // Fight: the forecast catch happens, grass burns low and red, smoke leans downwind.
                EmberdeepTestFlow.StartPrepared(run);Assert.AreEqual(FirePhase.Fighting,run.Phase);
                for(int guard=0;guard<600 && run.Cell(next).State==TreeState.Safe;guard++){run.Tick(.05f,false);if(guard%4==0)yield return null;}
                Assert.AreEqual(TreeState.Burning,run.Cell(next).State,"the forecast cell is the one that caught");
                int patch=-1;for(int g=0;g<run.Grass.Length;g++)if(run.Grass[g].State==TreeState.Burning){patch=g;break;}
                for(int guard=0;guard<600 && patch<0;guard++)
                {
                    run.Tick(.05f,false);if(guard%4==0)yield return null;
                    for(int g=0;g<run.Grass.Length;g++)if(run.Grass[g].State==TreeState.Burning){patch=g;break;}
                }
                Assert.GreaterOrEqual(patch,0,"grass caught");
                // A soaked tree next to the fire: darker and glossy for the length of its protection.
                int wet=WetCandidate(run);run.ApplyWater(run.Trees[wet].Position,1,.1f,EmberdeepBoardTuning.FullSoak);
                // The front-most patch alight too, aimed at from the seat so the crowns in front dither out.
                int shown=FrontGrass(run);var front=run.Grass[shown];
                if(front.State!=TreeState.Burning){front.State=TreeState.Burning;front.Heat=EmberdeepBoardTuning.Grass.IgniteHeat;run.InvalidateForecast();}
                var screen=camera.WorldToScreenPoint(arena.TransformPoint(front.Position));
                float settle=Time.realtimeSinceStartup+.6f;
                while(Time.realtimeSinceStartup<settle)
                {
                    run.Cell(run.Trees.Length+patch).Fuel=100;front.Fuel=100;
                    InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(screen.x,screen.y)});yield return null;
                }
                var flames=game.Effects.transform.Find("Turbulent flame particles").GetComponent<ParticleSystem>();
                Assert.Greater(GrassFlames(flames,patch),3,"burning grass shows its own flames");Assert.Greater(GrassFlames(flames,shown),3);
                Assert.Greater(grass.StateOf(patch).x,0,"burning grass tints");
                Assert.AreEqual(1,EmberdeepBoardTuning.WetLook(run.Trees[wet].Wetness),.001f);
                var block=new MaterialPropertyBlock();
                arena.Find("Incident forest/Tree "+wet).GetComponentInChildren<Renderer>().GetPropertyBlock(block);
                Assert.AreEqual(1,block.GetFloat("_Wetness"),.001f,"the tree tint shows the soak");
                Assert.Greater(game.Effects.LastWind.magnitude,.5f,"smoke leans with the wind");
                Capture(camera,arena,run,run.Trees.Length+shown,"2-BurningGrass");
                InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(4,Screen.height-4)});
                for(int i=0;i<12;i++){front.Fuel=100;yield return null;}
                Capture(camera,arena,run,wet,"3-WetTree");

                // Wind cues: streaks turn with the board wind; the warning swings the vane and gusts.
                settle=Time.realtimeSinceStartup+1.5f;while(Time.realtimeSinceStartup<settle)yield return null;
                float simDegrees=Mathf.Atan2(run.WindDirection.x,run.WindDirection.z)*Mathf.Rad2Deg;
                Assert.Less(Mathf.Abs(Mathf.DeltaAngle(simDegrees,game.EnvironmentVfx.WindDegrees)),2,"wind streaks follow the board wind");
                Capture(camera,arena,run,-1,"4-WindCues");
                for(int guard=0;guard<2000 && !run.WindWarning;guard++){run.Tick(.05f,false);if(guard%8==0)yield return null;}
                Assert.IsTrue(run.WindWarning);
                for(int i=0;i<4;i++)yield return null;
                Assert.IsTrue(game.EnvironmentVfx.Gusting);Assert.AreEqual(1,vane.Gusts);
                // Real-time through the dither and the swing, stopping before the shift lands.
                for(int guard=0;guard<2000 && run.WindShiftIn>.6f;guard++){run.Tick(Mathf.Clamp(Time.unscaledDeltaTime,.005f,.05f),false);yield return null;}
                Assert.IsTrue(run.WindWarning,"still in the warning window");
                var nextUpwind=-arena.TransformDirection(run.NextWindDirection);
                Assert.Greater(Vector3.Dot(Flat(vane.Pointing),Flat(nextUpwind)),.85f,"the vane has swung to the coming wind");
                Capture(camera,arena,run,-1,"5-WindShiftWarning");
                File.WriteAllText(Folder+"/CaptureScope.txt","Desktop seat (FireMiniature.Place), real fireground scene, stage "+Stage+
                    " (incident "+run.IncidentIndex+"). Sim stepped by the test. Not a headset capture.");
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editor;
                EmberdeepSession.NewExpedition();
            }
        }

        static Vector3 Flat(Vector3 v){v.y=0;return v.normalized;}
        static int FrontGrass(FireRun run)
        {
            int best=0;for(int g=1;g<run.Grass.Length;g++)
            {var p=run.Grass[g].Position;var b=run.Grass[best].Position;if(p.z+Mathf.Abs(p.x)*.3f<b.z+Mathf.Abs(b.x)*.3f)best=g;}
            return best;
        }
        // A Safe tree that is not about to be forecast-burned and sits near the front for the capture.
        static int WetCandidate(FireRun run)
        {
            int best=-1;float near=float.PositiveInfinity;
            for(int i=0;i<run.Trees.Length;i++)
            {
                var t=run.Trees[i];if(t.State!=TreeState.Safe)continue;
                float score=t.Position.z+Mathf.Abs(t.Position.x)*.5f;if(score<near){near=score;best=i;}
            }
            Assert.GreaterOrEqual(best,0);return best;
        }
        static int GrassFlames(ParticleSystem flames,int patch)
        {
            var buffer=new ParticleSystem.Particle[flames.main.maxParticles];int count=flames.GetParticles(buffer),own=0;
            for(int i=0;i<count;i++)if(buffer[i].randomSeed==FireRescueVfx.GrassSeed+patch)own++;
            return own;
        }
        static void Capture(Camera camera,Transform arena,FireRun run,int cell,string name)
        {
            const int width=1920,height=1080;
            Directory.CreateDirectory(Folder);
            var target=new RenderTexture(width,height,24){antiAliasing=4};var prior=camera.targetTexture;var active=RenderTexture.active;
            var pixels=new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target;camera.aspect=width/(float)height;camera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();
                File.WriteAllBytes(Folder+"/"+name+".png",pixels.EncodeToPNG());
                if(cell<0)return;
                var p=camera.WorldToViewportPoint(arena.TransformPoint(run.Cell(cell).Position));
                int cw=640,ch=400,x=Mathf.Clamp(Mathf.RoundToInt(p.x*width)-cw/2,0,width-cw),y=Mathf.Clamp(Mathf.RoundToInt(p.y*height)-ch/2,0,height-ch);
                var crop=new Texture2D(cw,ch,TextureFormat.RGB24,false);crop.SetPixels(pixels.GetPixels(x,y,cw,ch));crop.Apply();
                File.WriteAllBytes(Folder+"/"+name+"-crop.png",crop.EncodeToPNG());Object.Destroy(crop);
            }
            finally{camera.targetTexture=prior;camera.ResetAspect();RenderTexture.active=active;target.Release();Object.Destroy(target);Object.Destroy(pixels);}
        }
    }
}

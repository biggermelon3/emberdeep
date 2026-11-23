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
    // LoopSpec §4.6 on the real fireground, seen from the desktop seat (FireMiniature.Place).
    // Also writes review captures (full frame + crop) to Logs/EmberdeepReadability.
    public sealed class EmberdeepReadabilityPlayTests
    {
        // Stage 7 = incident 4: 7 columns x 4 rows on the compact board (LoopSpec §5 Reach; was 6 rows). Index = row*7 + column.
        // Column 3 in front of BackRow: 3, 10, 17. Which of them hide the aim point depends on the seat (since the 2026-11-22
        // placement the sight line to BackRow's foot passes under 17's crown and through 3 and 10).
        const int LowHeat=2, BackRow=24, Mid=19, Side=12;
        static readonly int[] InFront={3,10,17};
        const string Folder="Logs/EmberdeepReadability";

        [UnityTest] public IEnumerator LowHeatBackRowAndSeeThroughReadFromTheSeat()
        {
            EmberdeepSession.NewExpedition();var run=EmberdeepSession.Run;
            typeof(FireRun).GetProperty("Stage").SetValue(run,5); // FOREST INFERNO (stage 5 since the stage cut, was 7)
            run.LevelOverride=EmberdeepLevels.GeneratedGrid(5); // the 7x4 generated grid these indices name (levels are authored since LV)
            Assert.IsTrue(run.StartStage());Assert.AreEqual(28,run.Trees.Length);
            Stage(run);
            var background=InputSystem.settings.backgroundBehavior;var editor=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var mouse=InputSystem.AddDevice<Mouse>();
            try
            {
                yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);yield return null;yield return null;
                var game=Object.FindFirstObjectByType<FireRescueGame>();Assert.NotNull(game);Assert.AreSame(run,game.Run);
                game.SendMessage("OnApplicationFocus",true);
                var camera=game.Player.ViewCamera;var away=new Vector2(4,Screen.height-4);
                // Aim off the board: nothing fades and every burning tree carries a pin and bar.
                InputSystem.QueueStateEvent(mouse,new MouseState{position=away});
                yield return Hold(run,.8f);
                var readout=game.TreeReadout;Assert.NotNull(readout);Assert.AreEqual(28,readout.Count);
                Assert.AreEqual(run.Burning,readout.Markers,"one pin and heat bar per burning tree");
                Assert.Zero(readout.Faded);
                Assert.AreEqual(1,readout.GetComponentsInChildren<MeshRenderer>().Length,"all markers share one draw");
                var flames=game.Effects.transform.Find("Turbulent flame particles").GetComponent<ParticleSystem>();
                Assert.GreaterOrEqual(Flames(flames,LowHeat),4,"a 3%-heat, soaked tree still shows a readable flame");
                int lit=FlamePixels(camera,game.Arena,run,LowHeat,flames.GetComponent<ParticleSystemRenderer>());
                Debug.Log($"[Readability] low-heat soaked tree: {Flames(flames,LowHeat)} flame cards, {lit} lit pixels at 1080p");
                Assert.Greater(lit,60,"the low-heat flame must actually draw from the seat, not just exist");
                Capture(camera,game.Arena,run,LowHeat,"1-LowHeatBurning");
                Capture(camera,game.Arena,run,BackRow,"2-BackRowMarker");
                // Aim at the back-row fire: the trees in front of it on the sight line dither out, the target stays.
                var target=camera.WorldToScreenPoint(game.Arena.TransformPoint(run.Trees[BackRow].Position));
                InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(target.x,target.y)});
                yield return Hold(run,.45f);
                int Occluder=-1;
                for(int i=0;i<readout.Count;i++)if(readout.FadeOf(i)>.001f)
                {
                    Assert.Contains(i,InFront,"only trees on the sight line fade (LoopSpec §4.6, narrowed 2026-11-22)");
                    if(Occluder<0)Occluder=i;
                }
                Assert.GreaterOrEqual(Occluder,0,"a tree hiding the aim point fades");
                var occluder=game.Arena.Find("Incident forest/Tree "+Occluder).GetComponentInChildren<Renderer>();
                Assert.GreaterOrEqual(readout.FadeOf(Occluder),EmberdeepTreeReadout.MaximumFade-.01f,"the tree hiding the aim point fades");
                Assert.IsTrue(readout.FadingMaterial(Occluder));
                Assert.IsTrue(occluder.sharedMaterial.IsKeywordEnabled(EmberdeepTreeReadout.FadeKeyword),"fade is the alpha-clip variant");
                Assert.Zero(readout.FadeOf(BackRow),"the aimed tree stays solid");Assert.Zero(readout.FadeOf(LowHeat));
                Capture(camera,game.Arena,run,Occluder,"3-FrontTreeFading");
                // Aim away again: the fade clears and the shared opaque materials come back.
                InputSystem.QueueStateEvent(mouse,new MouseState{position=away});
                yield return Hold(run,.45f);
                Assert.Zero(readout.FadeOf(Occluder));Assert.IsFalse(readout.FadingMaterial(Occluder));
                Assert.IsFalse(occluder.sharedMaterial.IsKeywordEnabled(EmberdeepTreeReadout.FadeKeyword));
                // Heat reaches 0: one clear out puff, and that tree's marker goes.
                int markers=readout.Markers;
                run.Trees[LowHeat].Heat=1;run.ApplyWater(run.Trees[LowHeat].Position,5,.3f);
                Assert.AreEqual(TreeState.Saved,run.Trees[LowHeat].State);
                yield return null;yield return null;
                Assert.AreEqual(1,readout.Outs);Assert.AreEqual(markers-1,readout.Markers);Assert.Greater(game.Effects.VisibleSteam,8);
                yield return new WaitForSeconds(.3f);
                Assert.Zero(Flames(flames,LowHeat),"a Saved tree shows no flame");
                Capture(camera,game.Arena,run,LowHeat,"4-OutPuff");
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editor;
                EmberdeepSession.NewExpedition();
            }
        }

        static void Stage(FireRun run)
        {
            foreach(var tree in run.Trees){tree.State=TreeState.Safe;tree.Heat=0;tree.Wetness=0;}
            Burn(run,LowHeat,3);Burn(run,BackRow,85);Burn(run,Mid,60);Burn(run,Side,40);
            run.Trees[LowHeat].Wetness=3;
        }
        static void Burn(FireRun run,int tree,float heat){run.Trees[tree].State=TreeState.Burning;run.Trees[tree].Heat=heat;}
        // The playtest case: palm water has just soaked a nearly-out tree. Hold that state.
        static IEnumerator Hold(FireRun run,float seconds)
        {
            float until=Time.realtimeSinceStartup+seconds;
            while(Time.realtimeSinceStartup<until){run.Trees[LowHeat].Heat=3;run.Trees[LowHeat].Wetness=3;yield return null;}
        }
        static int Flames(ParticleSystem flames,int tree)
        {
            var buffer=new ParticleSystem.Particle[flames.main.maxParticles];int count=flames.GetParticles(buffer),own=0;
            for(int i=0;i<count;i++)if(buffer[i].randomSeed==tree+1 && buffer[i].remainingLifetime>.1f)own++;
            return own;
        }
        // Pixels around the tree that the flame cards visibly brighten (renderer on vs off).
        static int FlamePixels(Camera camera,Transform arena,FireRun run,int tree,ParticleSystemRenderer flames)
        {
            const int width=1920,height=1080;
            var target=new RenderTexture(width,height,24);var prior=camera.targetTexture;var active=RenderTexture.active;
            var pixels=new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target;camera.aspect=width/(float)height;RenderTexture.active=target;
                var p=run.Trees[tree].Position;
                var low=camera.WorldToViewportPoint(arena.TransformPoint(p-Vector3.up*.8f));
                var high=camera.WorldToViewportPoint(arena.TransformPoint(p+Vector3.up*1.7f));
                var side=camera.WorldToViewportPoint(arena.TransformPoint(p+Vector3.right*.8f));
                int x0=Mathf.Clamp(Mathf.RoundToInt((2*low.x-side.x)*width),0,width-1),x1=Mathf.Clamp(Mathf.RoundToInt(side.x*width),0,width-1);
                int y0=Mathf.Clamp(Mathf.RoundToInt(low.y*height),0,height-1),y1=Mathf.Clamp(Mathf.RoundToInt(high.y*height),0,height-1);
                var region=new Rect(Mathf.Min(x0,x1),Mathf.Min(y0,y1),Mathf.Max(1,Mathf.Abs(x1-x0)),Mathf.Max(1,Mathf.Abs(y1-y0)));
                flames.enabled=false;camera.Render();pixels.ReadPixels(region,0,0);pixels.Apply();
                var before=pixels.GetPixels32(0);
                flames.enabled=true;camera.Render();pixels.ReadPixels(region,0,0);pixels.Apply();
                var after=pixels.GetPixels32(0);
                int w=(int)region.width,h=(int)region.height,count=0;
                for(int y=0;y<h;y++)for(int x=0;x<w;x++){int i=y*width+x;if(after[i].r>before[i].r+25 && after[i].r>after[i].b+40)count++;}
                return count;
            }
            finally
            {
                flames.enabled=true;camera.targetTexture=prior;camera.ResetAspect();RenderTexture.active=active;
                Object.Destroy(pixels);Object.Destroy(target);
            }
        }
        static void Capture(Camera camera,Transform arena,FireRun run,int tree,string name)
        {
            const int width=1920,height=1080,cropW=640,cropH=420;
            var target=new RenderTexture(width,height,24);var prior=camera.targetTexture;var active=RenderTexture.active;
            var pixels=new Texture2D(width,height,TextureFormat.RGB24,false);Texture2D crop=null;
            try
            {
                camera.targetTexture=target;camera.aspect=width/(float)height;camera.Render();
                RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();
                Directory.CreateDirectory(Folder);File.WriteAllBytes($"{Folder}/{name}.png",pixels.EncodeToPNG());
                var focus=camera.WorldToViewportPoint(arena.TransformPoint(run.Trees[tree].Position+Vector3.up*.9f));
                int x=Mathf.Clamp(Mathf.RoundToInt(focus.x*width)-cropW/2,0,width-cropW),y=Mathf.Clamp(Mathf.RoundToInt(focus.y*height)-cropH/2,0,height-cropH);
                crop=new Texture2D(cropW,cropH,TextureFormat.RGB24,false);crop.SetPixels(pixels.GetPixels(x,y,cropW,cropH));crop.Apply();
                File.WriteAllBytes($"{Folder}/{name}-crop.png",crop.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=prior;camera.ResetAspect();RenderTexture.active=active;
                Object.Destroy(pixels);Object.Destroy(target);if(crop)Object.Destroy(crop);
            }
        }
    }
}

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
    // LoopSpec §11b layout A, end to end in the fireground: no panel in Plan/Fight, rim HUD, pause token panel,
    // bell start, results, upgrades and level select. Writes seated-eye captures to Logs/EmberdeepRimUi/.
    public sealed class EmberdeepRimUiPlayTests
    {
        const string Output="Logs/EmberdeepRimUi";
        // The seated eye from the desktop rig (FireMiniature.Place puts the board .65 m below it), with a headset-like
        // vertical field of view. Pitch is the head looking down.
        static void Capture(FireRescueGame game,string name,float pitch,float fov=92)
        {
            Directory.CreateDirectory(Output);var camera=game.Player.ViewCamera;
            var rotation=camera.transform.rotation;float priorFov=camera.fieldOfView;var prior=camera.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(1200,1100,24,RenderTextureFormat.ARGB32){antiAliasing=4};var texture=new Texture2D(1200,1100,TextureFormat.RGBA32,false);
            try
            {
                camera.transform.rotation=Quaternion.LookRotation(game.Arena.forward,Vector3.up)*Quaternion.Euler(pitch,0,0);camera.fieldOfView=fov;
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1200,1100),0,0);texture.Apply();
                File.WriteAllBytes(Output+"/"+name+".png",texture.EncodeToPNG());
            }
            finally{camera.transform.rotation=rotation;camera.fieldOfView=priorFov;camera.targetTexture=prior;RenderTexture.active=active;target.Release();Object.Destroy(target);Object.Destroy(texture);}
        }
        static void Clear(FireRun run){foreach(var a in run.Animals)run.ApplyAnimalWater(a.Position,100,.6f);foreach(var t in run.Trees)if(t.State==TreeState.Burning)run.ApplyWater(t.Position,1000,.1f);}
        static void Char(FireRun run,int count){foreach(var t in run.Trees){if(count<=0)break;if(t.State==TreeState.Safe){t.State=TreeState.Burnt;t.Fuel=0;count--;}}}
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
        static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
        static WorkshopButton Button(FireRescueGame game,string command)=>game.GetComponentsInChildren<WorkshopButton>(true).Single(b=>b.Command==command);
        // Every renderer on a screen draws after the outcome veil, so the dim never cuts a band across a panel.
        static void AssertAfterVeil(FireRescueGame game,string root)
        {
            int veil=Resources.Load<Shader>("EmberdeepOutcomeVeil").renderQueue;
            var renderers=game.Arena.Find(root).GetComponentsInChildren<Renderer>(true);Assert.IsNotEmpty(renderers);
            foreach(var r in renderers)foreach(var m in r.sharedMaterials)
                if(m)Assert.Greater(m.renderQueue,veil,$"{root}/{r.name} ({m.name}) draws before the veil");
            foreach(var text in game.Arena.Find(root).GetComponentsInChildren<TextMesh>(true))
                Assert.AreEqual(FireRescueGame.ScreenTextQueue,text.GetComponent<Renderer>().sharedMaterial.renderQueue,$"{root}/{text.name} text above its board");
        }
        static int[] SaveHints(){var m=(EmberdeepFirstHints.Move[])System.Enum.GetValues(typeof(EmberdeepFirstHints.Move));return m.Select(x=>PlayerPrefs.GetInt(EmberdeepFirstHints.Key(x),-1)).ToArray();}
        static void RestoreHints(int[] saved)
        {
            var m=(EmberdeepFirstHints.Move[])System.Enum.GetValues(typeof(EmberdeepFirstHints.Move));
            for(int i=0;i<m.Length;i++)if(saved[i]<0)PlayerPrefs.DeleteKey(EmberdeepFirstHints.Key(m[i]));else PlayerPrefs.SetInt(EmberdeepFirstHints.Key(m[i]),saved[i]);
        }

        [UnityTest] public IEnumerator LayoutAWalksEveryScreenAndKeepsTheRayOutOfTheFight()
        {
            var hints=SaveHints();EmberdeepFirstHints.ResetAll();
            try
            {
                EmberdeepSession.NewExpedition();AdvanceTo(EmberdeepSession.Run,3);Assert.AreEqual(3,EmberdeepSession.Run.Stage); // THE GROVE (was stage 5 before the stage cut)
                yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);yield return null;yield return null;
                var game=Object.FindFirstObjectByType<FireRescueGame>();game.SendMessage("OnApplicationFocus",true);
                yield return Wait(.3f);
                var run=game.Run;var hud=game.RimHud;Assert.NotNull(hud);Assert.True(hud.Built);
                // The rim and panels are laid out from the measured plinth: the authored terrain squeezed to the compact
                // board (LoopSpec §5 Reach, EmberdeepBoardFit); same width.
                Assert.AreEqual(EmberdeepBoardLayout.CompactFront,hud.Board.min.z,.05f);Assert.AreEqual(EmberdeepBoardLayout.CompactBack,hud.Board.max.z,.05f);
                Assert.AreEqual(EmberdeepRimHud.DefaultBoard.size.x,hud.Board.size.x,.05f);Assert.AreEqual(0,hud.Board.max.y,.05f);

                // Plan: no panel, no ray, bell + token + strip on the rim, the bell's ghost hint.
                Assert.AreEqual(FirePhase.Preparing,run.Phase);
                Assert.False(game.transform.Find("Rescue station/Main controls").gameObject.activeInHierarchy,"No dashboard in the spell flow");
                Assert.False(game.RayAllowed);Assert.False(game.MenuInput.HasPointer,"The ray is off in Plan");
                Assert.True(hud.BellVisible);Assert.True(hud.TokenVisible);Assert.True(hud.StripVisible);Assert.False(hud.PlaneVisible,"No plane until L3");
                Assert.AreEqual(run.Animals.Length,hud.SheepIcons);Assert.AreEqual(run.BurnLimit,hud.MeterPips);
                Assert.True(game.Hints.Visible,"First time: a ghost fist over the bell");Assert.AreEqual(EmberdeepFirstHints.Move.Bell,game.Hints.Showing);
                // The strip faces the seated eye: a bezel, not a vertical plinth face.
                var eye=game.Player.ViewCamera.transform.position;
                Assert.Greater(Vector3.Dot(hud.Strip.right,game.Arena.right),.999f,"Strip parallel to the front edge");
                var toEye=Vector3.ProjectOnPlane(eye-hud.Strip.position,game.Arena.right).normalized;
                Assert.Greater(Vector3.Dot(-hud.Strip.forward,toEye),.999f,"Strip pitched up to the eye like a bezel");
                Assert.Greater(Vector3.Angle(-hud.Strip.forward,game.Arena.up),15,"Not lying flat either");
                Capture(game,"1-plan-bell",50);

                // Bell start: the countdown runs, the bell rings, the hint counts down.
                Assert.True(game.StrikeBell());Assert.Greater(run.CountdownRemaining,2.9f);Assert.False(game.StrikeBell(),"One strike");
                Assert.AreEqual(1,EmberdeepFirstHints.Count(EmberdeepFirstHints.Move.Bell));
                yield return Wait(.4f);Capture(game,"2-bell-countdown",50);
                EmberdeepTestFlow.StartPrepared(run);yield return null;yield return null;
                Assert.AreEqual(FirePhase.Fighting,run.Phase);Assert.False(hud.BellVisible);Assert.True(hud.TokenVisible);
                Assert.False(game.RayAllowed);yield return null;Assert.False(game.MenuInput.HasPointer,"The ray is off in the fight");

                // Fight HUD: one sheep saved, one on fire, two charred trees, plane ring shown for the capture.
                run.Animals[0].State=AnimalState.Rescued;run.Animals[1].OnFire=true;Char(run,2);
                hud.PlaneAvailable=true;hud.PlaneCharge01=.6f;yield return Wait(.5f);
                Assert.AreEqual(2,hud.MeterCharred);Assert.True(hud.PlaneVisible);
                Assert.AreNotEqual(hud.SheepColor(0),hud.SheepColor(1));Assert.AreNotEqual(hud.SheepColor(1),hud.SheepColor(2));
                Capture(game,"3-fight-hud",50);Capture(game,"3b-fight-wide",38);
                hud.PlaneAvailable=false;run.Animals[1].OnFire=false;

                // Pause: the token opens a frozen pause panel where the ray works.
                Assert.True(game.OpenPause());yield return null;yield return null;
                Assert.True(game.PausePanelVisible);Assert.True(game.RayAllowed);float elapsed=run.Elapsed;
                for(int i=0;i<10;i++)yield return null;Assert.AreEqual(elapsed,run.Elapsed,"Paused");
                AssertAfterVeil(game,"Pause panel");Assert.True(game.Weathervane.gameObject.activeSelf,"The vane stays up under the pause panel");
                yield return Wait(.4f);Capture(game,"4-pause",10);
                Button(game,"Pause resume").Press();yield return null;Assert.False(game.PauseOpen);Assert.False(game.RayAllowed);

                // Lost: RETRY big, CAMP small, the rim meter glows full.
                Char(run,run.BurnLimit);run.Tick(.05f,false);Assert.AreEqual(FirePhase.Lost,run.Phase);
                yield return Wait(FireRescueGame.ResultsDelay+FireRescueGame.ResultsRise+.3f);
                Assert.True(game.ResultsVisible);Assert.True(game.RayAllowed);
                Assert.True(Button(game,"Results retry big").gameObject.activeInHierarchy);Assert.True(Button(game,"Results camp").gameObject.activeInHierarchy);
                Assert.False(Button(game,"Results next").gameObject.activeInHierarchy);Assert.AreEqual(run.BurnLimit,hud.MeterCharred);
                Capture(game,"5-lost",14);Capture(game,"5b-lost-rim",50);
                Button(game,"Results retry big").Press();yield return null;
                Assert.AreEqual(FirePhase.Preparing,run.Phase);Assert.AreEqual(3,run.Stage);Assert.False(game.ResultsVisible);

                // Cleared: stars, sheep, coins; NEXT big, RETRY and UPGRADES small.
                EmberdeepTestFlow.StartPrepared(run);yield return null;
                for(int i=0;i<200 && run.Phase==FirePhase.Fighting;i++){Clear(run);run.Tick(.05f,false);}
                Assert.AreEqual(FirePhase.Cleared,run.Phase);run.EvacuateSafeAnimals();
                yield return Wait(FireRescueGame.ResultsDelay+FireRescueGame.ResultsRise+.9f);
                Assert.True(game.ResultsVisible);Assert.True(Button(game,"Results next").gameObject.activeInHierarchy);
                Assert.True(Button(game,"Results upgrades").gameObject.activeInHierarchy);Assert.False(Button(game,"Results retry big").gameObject.activeInHierarchy);
                Capture(game,"6-cleared",14);

                // Upgrades: tiles; an unaffordable one still answers the ray (it explains itself) but buys nothing; slot and
                // tower are L3's piece upgrades.
                Button(game,"Results upgrades").Press();yield return null;yield return null;
                Assert.True(game.UpgradesVisible);Assert.False(game.ResultsVisible);
                Assert.AreEqual(run.CanUpgradeMagic,game.UpgradeBuyable(0));Assert.AreEqual(run.CanUpgradePump,game.UpgradeBuyable(1));
                Assert.AreEqual(run.CanUpgradeSlot,game.UpgradeBuyable(2));Assert.AreEqual(run.CanUpgradeTower,game.UpgradeBuyable(3));
                Capture(game,"7-upgrades",14);
                if(run.CanUpgradePump){int level=run.PumpLevel;Button(game,"Upgrade pump").Press();Assert.AreEqual(level+1,run.PumpLevel);}
                Button(game,"Upgrades back").Press();yield return null;yield return null;Assert.False(game.UpgradesVisible);Assert.True(game.ResultsVisible);

                // Level select over the next level's dimmed board; slap/arrows browse, PLAY starts its Plan.
                Button(game,"Results next").Press();yield return null;yield return null;
                Assert.True(game.LevelSelectOpen);Assert.True(game.LevelSelectVisible);Assert.AreEqual(4,run.Stage);Assert.AreEqual(4,game.SelectedLevel);
                Assert.AreEqual(FirePhase.Preparing,run.Phase);Assert.False(hud.StripVisible,"The rim stands down under the carousel");
                Assert.Greater(game.Feedback.Outcome.Dim,0,"The board behind is dimmed");Assert.True(game.RayAllowed);
                Assert.True(game.Hints.Visible);Assert.AreEqual(EmberdeepFirstHints.Move.Slap,game.Hints.Showing);
                yield return Wait(.6f);Capture(game,"8-level-select",12);
                Assert.False(game.Weathervane.gameObject.activeSelf,"No rooster through the carousel");AssertAfterVeil(game,"Level select");
                game.BrowseLevel(-1);Assert.AreEqual(3,game.SelectedLevel);yield return Wait(FireRescueGame.BrowseSettle+.4f);
                Assert.AreEqual(3,run.Stage,"The board behind follows the card");Capture(game,"8b-level-select-replay",12);
                AssertAfterVeil(game,"Level select"); // the localizer re-assigned the renamed cards' font materials
                game.BrowseLevel(2);Assert.AreEqual(5,game.SelectedLevel);yield return null;
                Assert.False(Button(game,"Level play").Interactable,"Stage 6 is locked");Assert.AreEqual(3,run.Stage,"A locked card leaves the board");
                Capture(game,"8c-level-select-locked",12);
                game.BrowseLevel(-1);yield return null;Assert.True(Button(game,"Level play").Interactable);Button(game,"Level play").Press();yield return null;
                Assert.False(game.LevelSelectOpen);Assert.AreEqual(FirePhase.Preparing,run.Phase);Assert.AreEqual(4,run.Stage);Assert.False(game.RayAllowed);
                yield return null;Assert.True(game.Weathervane.gameObject.activeSelf,"The vane is back for the Plan phase");
            }
            finally{RestoreHints(hints);}
        }
    }
}

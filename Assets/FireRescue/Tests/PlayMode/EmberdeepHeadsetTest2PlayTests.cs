using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using PhysicalAssembly;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    // Headset test 2 (2026-11-22), rim and between-level screens: the bell over the field and its hint, the pause token
    // on the plinth (no clipping), the pause panel's board steppers, the upgrades page and the level select's slap hint.
    // Writes captures and numbers to Logs/EmberdeepHeadsetTest2/ (rewritten each run).
    public sealed class EmberdeepHeadsetTest2PlayTests
    {
        const string Output="Logs/EmberdeepHeadsetTest2";
        static readonly CultureInfo C=CultureInfo.InvariantCulture;
        static readonly EmberdeepFirstHints.Move[] Moves=(EmberdeepFirstHints.Move[])System.Enum.GetValues(typeof(EmberdeepFirstHints.Move));
        int[] hints;EmberdeepSettingsPrefs prefs;int tilted;
        [SetUp] public void Save()
        {
            hints=Moves.Select(m=>PlayerPrefs.GetInt(EmberdeepFirstHints.Key(m),-1)).ToArray();EmberdeepFirstHints.ResetAll();
            prefs=EmberdeepSettingsPrefs.Save();tilted=PlayerPrefs.GetInt(EmberdeepPlayArea.TiltedKey,-1);
            PlayerPrefs.DeleteKey(EmberdeepPlayArea.HeightKey);PlayerPrefs.DeleteKey(EmberdeepPlayArea.DistanceKey);PlayerPrefs.DeleteKey(EmberdeepPlayArea.TiltedKey);
            EmberdeepPlayArea.Reload();Directory.CreateDirectory(Output);
        }
        [TearDown] public void Restore()
        {
            for(int i=0;i<Moves.Length;i++)if(hints[i]<0)PlayerPrefs.DeleteKey(EmberdeepFirstHints.Key(Moves[i]));else PlayerPrefs.SetInt(EmberdeepFirstHints.Key(Moves[i]),hints[i]);
            if(tilted<0)PlayerPrefs.DeleteKey(EmberdeepPlayArea.TiltedKey);else PlayerPrefs.SetInt(EmberdeepPlayArea.TiltedKey,tilted);
            prefs.Restore();
        }
        static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
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
        static IEnumerator Load(int stage)
        {
            EmberdeepSession.NewExpedition();AdvanceTo(EmberdeepSession.Run,stage);Assert.AreEqual(stage,EmberdeepSession.Run.Stage);
            yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);yield return null;yield return null;
            Object.FindFirstObjectByType<FireRescueGame>().SendMessage("OnApplicationFocus",true);
            yield return Wait(1.2f); // the rim re-measures the plinth every second
        }
        static WorkshopButton Button(FireRescueGame game,string command)=>game.GetComponentsInChildren<WorkshopButton>(true).Single(b=>b.Command==command);
        static float Down(Vector3 eye,Vector3 p)=>Mathf.Atan2(eye.y-p.y,Vector3.ProjectOnPlane(p-eye,Vector3.up).magnitude)*Mathf.Rad2Deg;
        // From the seated eye, looking at a point (pitch only, along the board's heading), headset-like field of view.
        static void Capture(FireRescueGame game,string name,Vector3 look,float fov=92)
        {
            var camera=game.Player.ViewCamera;var eye=camera.transform.position;
            var rotation=camera.transform.rotation;float priorFov=camera.fieldOfView;var prior=camera.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(1200,1100,24,RenderTextureFormat.ARGB32){antiAliasing=4};var texture=new Texture2D(1200,1100,TextureFormat.RGBA32,false);
            try
            {
                var flat=Vector3.ProjectOnPlane(game.Arena.forward,Vector3.up).normalized;
                var dir=look-eye;var side=Vector3.Cross(Vector3.up,flat);dir-=side*Vector3.Dot(dir,side); // no yaw: the seat faces the board
                camera.transform.rotation=Quaternion.LookRotation(dir.normalized,Vector3.up);camera.fieldOfView=fov;
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1200,1100),0,0);texture.Apply();
                File.WriteAllBytes(Output+"/"+name+".png",texture.EncodeToPNG());
            }
            finally{camera.transform.rotation=rotation;camera.fieldOfView=priorFov;camera.targetTexture=prior;RenderTexture.active=active;target.Release();Object.Destroy(target);Object.Destroy(texture);}
        }
        // Zoomed: straight at the point (yaw too).
        static void CaptureAt(FireRescueGame game,string name,Vector3 look,float fov)=>CaptureFrom(game,name,game.Player.ViewCamera.transform.position,look,fov);
        // A camera of its own (the view camera carries desktop props in front of it), for side views.
        static void CaptureFrom(FireRescueGame game,string name,Vector3 from,Vector3 look,float fov)
        {
            var view=game.Player.ViewCamera;var go=new GameObject("Capture camera");var camera=go.AddComponent<Camera>();
            var target=new RenderTexture(1200,1100,24,RenderTextureFormat.ARGB32){antiAliasing=4};var texture=new Texture2D(1200,1100,TextureFormat.RGBA32,false);var active=RenderTexture.active;
            try
            {
                camera.CopyFrom(view);camera.nearClipPlane=.005f;camera.fieldOfView=fov;
                go.transform.SetPositionAndRotation(from,Quaternion.LookRotation(look-from,Vector3.up));
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1200,1100),0,0);texture.Apply();
                File.WriteAllBytes(Output+"/"+name+".png",texture.EncodeToPNG());
            }
            finally{RenderTexture.active=active;camera.targetTexture=null;target.Release();Object.Destroy(target);Object.Destroy(texture);Object.Destroy(go);}
        }
        static void Note(string text){File.AppendAllText(Output+"/numbers.txt",text+"\n");Debug.Log("EMBERDEEP_HT2 "+text);}
        static Bounds Bounds(Transform root)
        {
            var rs=root.GetComponentsInChildren<Renderer>().Where(r=>r.enabled && !(r is ParticleSystemRenderer)).ToArray();
            var b=rs.Length>0?rs[0].bounds:new Bounds(root.position,Vector3.zero);foreach(var r in rs)b.Encapsulate(r.bounds);return b;
        }

        // ---- Bell ----------------------------------------------------------------------------------------------
        // Angular box (x across, y down, degrees) of a bounds from the seat, over its 8 corners.
        static Rect Span(Vector3 eye,Vector3 forward,Vector3 right,Bounds b)
        {
            float x0=float.MaxValue,x1=float.MinValue,y0=float.MaxValue,y1=float.MinValue;
            for(int i=0;i<8;i++)
            {
                var p=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                float across=Mathf.Atan2(Vector3.Dot(p-eye,right),Vector3.Dot(p-eye,forward))*Mathf.Rad2Deg,down=Down(eye,p);
                x0=Mathf.Min(x0,across);x1=Mathf.Max(x1,across);y0=Mathf.Min(y0,down);y1=Mathf.Max(y1,down);
            }
            return Rect.MinMaxRect(x0,y0,x1,y1);
        }
        // Owner's call (2026-11-22): the bell hangs aside (EmberdeepRimHud.BellAcross) so it never hides the rooster
        // weathervane from the default seat in Plan, on any level that shows both.
        [UnityTest] public IEnumerator BellNeverHidesTheWeathervaneFromTheSeat()
        {
            EmberdeepSession.NewExpedition();int stages=EmberdeepSession.Run.StageCount,both=0;
            for(int stage=0;stage<stages;stage++)
            {
                yield return Load(stage);
                var game=Object.FindFirstObjectByType<FireRescueGame>();var hud=game.RimHud;
                if(game.Run.Phase!=FirePhase.Preparing || !hud || !hud.BellVisible || !game.Weathervane || !game.Weathervane.gameObject.activeInHierarchy)continue;
                var eye=game.Player.ViewCamera.transform.position;
                var forward=Vector3.ProjectOnPlane(game.Arena.forward,Vector3.up).normalized;var right=Vector3.Cross(Vector3.up,forward);
                var bell=Span(eye,forward,right,Bounds(hud.Bell));var vane=Span(eye,forward,right,Bounds(game.Weathervane.transform));
                float across=Mathf.Max(vane.xMin-bell.xMax,bell.xMin-vane.xMax),down=Mathf.Max(vane.yMin-bell.yMax,bell.yMin-vane.yMax);
                Note(string.Format(C,"stage {0}: bell {1:F1}..{2:F1} across, {3:F1}..{4:F1} down; vane {5:F1}..{6:F1} across, {7:F1}..{8:F1} down; gap {9:F1} deg",
                    stage,bell.xMin,bell.xMax,bell.yMin,bell.yMax,vane.xMin,vane.xMax,vane.yMin,vane.yMax,Mathf.Max(across,down)));
                Assert.Greater(Mathf.Max(across,down),1f,"Stage "+stage+": the bell covers the weathervane from the seat");
                both++;
            }
            Assert.Greater(both,0,"At least one level shows the bell and the weathervane together in Plan");
        }
        [UnityTest] public IEnumerator BellFloatsOverTheFieldPlaysItsHintAndIgnoresACarryingHand()
        {
            File.WriteAllText(Output+"/numbers.txt","");
            yield return Load(3);
            var game=Object.FindFirstObjectByType<FireRescueGame>();var run=game.Run;var hud=game.RimHud;
            Assert.AreEqual(FirePhase.Preparing,run.Phase);Assert.True(hud.BellVisible);
            var field=EmberdeepRimHud.BellOver(run,EmberdeepWaterHand.Left?-1:1);var bellLocal=game.Arena.InverseTransformPoint(hud.Bell.position);
            Assert.AreEqual(field.x,bellLocal.x,.01f,"Over the field, BellAcross toward the water hand");Assert.AreEqual(field.y,bellLocal.z,.01f);
            Assert.Greater(bellLocal.y,hud.Board.max.y+EmberdeepRimHud.CanopyTop,"Over the canopy");
            Assert.Greater(Vector3.Dot(hud.BellUp,Vector3.up),.999f,"Upright");
            Assert.True(game.Hints.Visible,"The hammer hint plays on it");Assert.AreEqual(EmberdeepFirstHints.Move.Bell,game.Hints.Showing);
            Assert.Less(Vector3.Distance(game.Hints.Demo.transform.position,hud.BellTop),.03f);
            var eye=game.Player.ViewCamera.transform.position;
            var bellBounds=Bounds(hud.Bell);
            Note(string.Format(C,"bell: top {0:F3} m ahead (flat), {1:F1} deg down, base {2:F1} deg, {3:F3} m from the eye; over z {4:F2}",
                Vector3.ProjectOnPlane(hud.BellTop-eye,Vector3.up).magnitude,Down(eye,hud.BellTop),Down(eye,new Vector3(bellBounds.center.x,bellBounds.min.y,bellBounds.center.z)),Vector3.Distance(eye,hud.BellTop),field.y));
            // The rooster weathervane (Plan's wind): measured here, asserted clear on every level by
            // BellNeverHidesTheWeathervaneFromTheSeat (the bell hangs aside, EmberdeepRimHud.BellAcross).
            if(game.Weathervane && game.Weathervane.gameObject.activeInHierarchy)
            {
                var vane=Bounds(game.Weathervane.transform);var seatRight=Vector3.Cross(Vector3.up,Vector3.ProjectOnPlane(game.Arena.forward,Vector3.up).normalized);
                float Side(Vector3 p)=>Mathf.Atan2(Vector3.Dot(p-eye,seatRight),Vector3.ProjectOnPlane(p-eye,Vector3.up).magnitude)*Mathf.Rad2Deg;
                Note(string.Format(C,"vane: {0:F1}..{1:F1} deg down, {2:F1}..{3:F1} deg across; bell {4:F1}..{5:F1} down, {6:F1}..{7:F1} across",
                    Down(eye,new Vector3(vane.center.x,vane.max.y,vane.center.z)),Down(eye,new Vector3(vane.center.x,vane.min.y,vane.center.z)),Side(vane.min),Side(vane.max),
                    Down(eye,new Vector3(bellBounds.center.x,bellBounds.max.y,bellBounds.center.z)),Down(eye,new Vector3(bellBounds.center.x,bellBounds.min.y,bellBounds.center.z)),
                    Side(bellBounds.min),Side(bellBounds.max)));
            }
            yield return Wait(.5f);
            Capture(game,"1-plan-bell-hint",hud.BellTop+Vector3.down*.12f);CaptureAt(game,"1b-plan-bell-hint-zoom",hud.BellTop+Vector3.up*.04f,40);

            // A hand that owns a piece never strikes; the same fist free does.
            EmberdeepFistHammer.Hand Fist(float above)=>new EmberdeepFistHammer.Hand(true,hud.BellTop+Vector3.up*above,1);
            var held=Fist(.02f);
            var handAt=new FireToolManipulation.Hand{Valid=true,Palm=new Pose(held.Fist,Quaternion.identity),Contact=held.Fist};
            var candidates=new List<EmberdeepPieceGrasp.Candidate>{new(0,held.Fist)};
            handAt.Closure=0;game.PieceGrasp.Step(handAt,default,candidates,.02f);
            handAt.Closure=1;for(int i=0;i<10 && !game.PieceGrasp.OwnsHand(0);i++)game.PieceGrasp.Step(handAt,default,candidates,.02f);
            Assert.True(game.PieceGrasp.OwnsHand(0),"Hand 0 holds a piece");
            bool Swing(){bool struck=false;for(float h=.16f;h>=.0f;h-=.02f)struck|=game.SampleBellHands(Fist(h),default,.02f);return struck;}
            Assert.False(Swing(),"A carrying hand passes through the bell");Assert.AreEqual(FirePhase.Preparing,run.Phase);Assert.Less(run.CountdownRemaining,0);
            game.PieceGrasp.Cancel(0);
            Assert.True(Swing(),"The free fist strikes it");Assert.Greater(run.CountdownRemaining,2.5f);
            Assert.AreEqual(1,EmberdeepFirstHints.Count(EmberdeepFirstHints.Move.Bell));
            yield return Wait(EmberdeepRimHud.BellRingHold+EmberdeepRimHud.BellLeaveSeconds*.5f);
            Assert.Greater(hud.BellLeave,.2f,"Rising away");Capture(game,"2-bell-leaving",hud.BellTop+Vector3.down*.12f);
            yield return Wait(EmberdeepRimHud.BellLeaveSeconds*.6f);
            Assert.AreEqual(1,hud.BellLeave,1e-3f,"Gone before the fight");Capture(game,"2b-countdown-where-it-hung",hud.BellTop+Vector3.down*.12f);
        }

        // ---- Pause token -----------------------------------------------------------------------------------------
        // What the token touches: every board renderer whose mesh has vertices inside the coin's volume (slightly grown),
        // and the coin's lowest point against the plinth top.
        static string TokenContacts(FireRescueGame game,out int touching,out float lowest)
        {
            var hud=game.RimHud;var coin=hud.Token.GetComponentsInChildren<Renderer>(true).First(r=>r.name=="Wooden token");
            var c=coin.transform;var sb=new StringBuilder();touching=0;
            // Coin: a unit cylinder scaled (diameter, half-thickness, diameter): local y +-1, radius .5.
            lowest=float.PositiveInfinity;
            for(int i=0;i<32;i++){float a=i*Mathf.PI/16;foreach(float y in new[]{-1f,1f}){var p=game.Arena.InverseTransformPoint(c.TransformPoint(new Vector3(Mathf.Cos(a)*.5f,y,Mathf.Sin(a)*.5f)));lowest=Mathf.Min(lowest,p.y);}}
            var bounds=coin.bounds;bounds.Expand(.01f);
            foreach(var r in game.Arena.GetComponentsInChildren<MeshRenderer>())
            {
                if(!r.enabled || !r.gameObject.activeInHierarchy || r.transform.IsChildOf(hud.Root) || !r.bounds.Intersects(bounds))continue;
                var f=r.GetComponent<MeshFilter>();var mesh=f?f.sharedMesh:null;
                if(!mesh || !mesh.isReadable){sb.AppendFormat(C,"  {0} (bounds only, mesh unreadable)\n",Path(r.transform));touching++;continue;}
                int inside=0;float deepest=0;var verts=mesh.vertices;
                foreach(var v in verts)
                {
                    // Inside the coin: within its radius and between its faces (local y -1..1); depth over the underside.
                    var local=c.InverseTransformPoint(r.transform.TransformPoint(v));
                    float radial=new Vector2(local.x,local.z).magnitude;
                    if(radial<.5f && local.y>-1 && local.y<1){inside++;deepest=Mathf.Max(deepest,(local.y+1)*c.lossyScale.y);}
                }
                if(inside>0){touching++;sb.AppendFormat(C,"  {0}: {1} vertices inside the coin, up to {2:F4} m over its underside\n",Path(r.transform),inside,deepest);}
            }
            return sb.ToString();
        }
        static string Path(Transform t){var s=t.name;for(var p=t.parent;p && p.name!="Arena" && p.parent;p=p.parent)s=p.name+"/"+s;return s;}
        [UnityTest] public IEnumerator PauseTokenSitsOnThePlinthWithoutClipping()
        {
            yield return Load(3);
            var game=Object.FindFirstObjectByType<FireRescueGame>();var hud=game.RimHud;
            void Measure(string label)
            {
                var report=TokenContacts(game,out int touching,out float lowest);
                var eye=game.Player.ViewCamera.transform.position;
                Note(string.Format(C,"token [{0}]: centre local {1}, lowest edge {2:F3} units over the board top (ground {3:F3}), {4:F1} deg down, {5} meshes inside it{6}\n{7}",
                    label,game.Arena.InverseTransformPoint(hud.TokenCenter).ToString("F2"),lowest,hud.TokenGround,Down(eye,hud.TokenCenter),touching,touching==0?"":":",report));
                Assert.AreEqual(0,touching,label+": nothing on the board cuts into the token\n"+report);
                Assert.GreaterOrEqual(lowest,hud.TokenGround-1e-3f,label+": its lowest edge clears the ground");
                CaptureAt(game,"3-token-"+label,hud.TokenCenter,30);
                var side=Vector3.Cross(Vector3.up,Vector3.ProjectOnPlane(game.Arena.forward,Vector3.up).normalized);
                float outward=Vector3.Dot(hud.TokenCenter-game.Arena.position,side)<0?-1:1;
                CaptureFrom(game,"3b-token-side-"+label,hud.TokenCenter+side*outward*.1f+Vector3.up*.012f,hud.TokenCenter,40);
            }
            Measure("plan-default");
            EmberdeepTestFlow.StartPrepared(game.Run);yield return Wait(.3f);
            Measure("fight-default");
            EmberdeepPlayArea.SetTilted(true);yield return Wait(.3f);Measure("fight-tilted");
            EmberdeepPlayArea.SetHeight(EmberdeepPlayArea.HeightMin);EmberdeepPlayArea.SetDistance(EmberdeepPlayArea.DistanceMax);yield return Wait(.3f);Measure("fight-tilted-low-far");
            EmberdeepPlayArea.SetTilted(false);EmberdeepPlayArea.SetHeight(EmberdeepPlayArea.HeightMax);EmberdeepPlayArea.SetDistance(EmberdeepPlayArea.DistanceMin);yield return Wait(.3f);Measure("fight-flat-high-near");
        }

        // ---- Pause panel: board placement --------------------------------------------------------------------------
        [UnityTest] public IEnumerator PauseStepsMoveTheBoardUnderAStillPanel()
        {
            // Flat: on the tilted board (the default since headset test 4) a raised board lifts the strip into the panel,
            // and the panel then rises by design (FireRescueGame.PlaceScreens).
            EmberdeepPlayArea.SetTilted(false);
            yield return Load(3);
            var game=Object.FindFirstObjectByType<FireRescueGame>();var run=game.Run;
            EmberdeepTestFlow.StartPrepared(run);yield return null;
            Assert.True(game.OpenPause());yield return null;yield return null;
            var panel=game.Arena.Find("Pause panel");Assert.True(game.PausePanelVisible);
            Assert.AreEqual("0 cm",game.PauseHeightLabel);Assert.AreEqual("0 cm",game.PauseDistanceLabel);
            var eye=game.Player.ViewCamera.transform.position;
            foreach(var name in new[]{"Pause resume","Pause height down","Pause height up","Pause distance down","Pause distance up","Pause board flat","Pause board tilted"})
            {
                var b=Button(game,name);Assert.True(b.gameObject.activeInHierarchy,name);
                float d=Down(eye,b.transform.position);Assert.Less(d,15,name+" within reach of a relaxed ray");Assert.Greater(d,-12,name);
            }
            Note(string.Format(C,"pause panel centre {0:F1} deg down; steppers {1:F1} deg; tilt row {2:F1} deg",
                Down(eye,panel.position),Down(eye,Button(game,"Pause height up").transform.position),Down(eye,Button(game,"Pause board flat").transform.position)));
            yield return Wait(.3f);Capture(game,"4-pause-board-steppers",panel.position);Capture(game,"4b-pause-and-board",panel.position+Vector3.down*.3f);
            var still=panel.position;var arena=game.Arena.position;
            Button(game,"Pause height up").Press();yield return null;yield return null;
            Assert.AreEqual(.02f,game.Arena.position.y-arena.y,.002f,"The board rose 2 cm");Assert.AreEqual("+2 cm",game.PauseHeightLabel);
            Assert.Less(Vector3.Distance(still,panel.position),1e-3f,"The panel holds still");
            game.PauseNudgeDistance(-1);game.PauseNudgeDistance(-1);yield return null;yield return null;Assert.AreEqual("-4 cm",game.PauseDistanceLabel);
            Assert.Less(Vector3.Distance(still,panel.position),1e-3f);
            Button(game,"Pause board tilted").Press();yield return null;yield return null;Assert.True(EmberdeepPlayArea.Tilted);
            Assert.Greater(Vector3.Angle(game.Arena.up,Vector3.up),20,"The board tilts");
            // The tilt raises the back edge and its strip: the panel may only lift, to keep clear of the strip.
            Assert.GreaterOrEqual(Down(eye,still)+1e-3f,Down(eye,panel.position),"The panel never drops");
            var strip=game.RimHud.Strip.GetComponentsInChildren<Renderer>().Aggregate(new Bounds(game.RimHud.Strip.position,Vector3.zero),(b,r)=>{b.Encapsulate(r.bounds);return b;});
            var board=panel.Find("Pause board").GetComponent<Renderer>().bounds;
            float stripTop=Down(eye,new Vector3(strip.center.x,strip.max.y,strip.center.z)),panelBottom=Down(eye,new Vector3(board.center.x,board.min.y,board.center.z));
            Note(string.Format(C,"tilted while paused: panel moved {0:F3} m, bottom {1:F1} deg, strip top {2:F1} deg",Vector3.Distance(still,panel.position),panelBottom,stripTop));
            Assert.Less(panelBottom,stripTop,"Its lower edge stays over the strip");
            yield return Wait(.3f);Capture(game,"4c-pause-tilted-board",panel.position+Vector3.down*.3f);
            // Re-opened on the tilted board: still upright and over the strip.
            game.ClosePause();yield return null;Assert.True(game.OpenPause());yield return null;yield return null;
            Assert.Less(Vector3.Angle(panel.up,Vector3.up),15,"Upright on a tilted board");
            float centre=Down(eye,panel.position);Assert.Less(centre,FireRescueGame.PanelDown+.5f);Assert.Greater(centre,-15);
            Note(string.Format(C,"tilted: pause centre {0:F1} deg down, panel up vs world up {1:F1} deg",centre,Vector3.Angle(panel.up,Vector3.up)));
            Button(game,"Pause board flat").Press();yield return null;Assert.False(EmberdeepPlayArea.Tilted);
            game.ClosePause();
        }

        // ---- Upgrades ----------------------------------------------------------------------------------------------
        [UnityTest] public IEnumerator UpgradesShowModelsUnitsAndTheGainOnHover()
        {
            yield return Load(3);
            var game=Object.FindFirstObjectByType<FireRescueGame>();var run=game.Run;
            EmberdeepTestFlow.StartPrepared(run);yield return null;
            for(int i=0;i<200 && run.Phase==FirePhase.Fighting;i++){Clear(run);run.Tick(.05f,false);}
            Assert.AreEqual(FirePhase.Cleared,run.Phase);run.EvacuateSafeAnimals();
            yield return Wait(FireRescueGame.ResultsDelay+FireRescueGame.ResultsRise+.5f);
            Button(game,"Results upgrades").Press();yield return null;yield return null;Assert.True(game.UpgradesVisible);
            var root=game.Arena.Find("Upgrades panel");
            for(int i=0;i<8;i++)yield return null; // headset test 3: the models are rendered to pictures, one tile per frame
            var words=new[]{"GEM","PUMP","SLOT","TOWER"};
            for(int i=0;i<4;i++)
            {
                var tile=root.Find("Upgrade tile "+words[i]);Assert.NotNull(tile,words[i]);
                Assert.NotNull(game.UpgradePicture(i),words[i]+" has a picture of its model");
            }
            Assert.AreEqual($"{run.MagicCapacity:0}",game.UpgradeNumberText(0),"The CURRENT value");Assert.AreEqual($"+{run.PumpStrikeWater:0}",game.UpgradeNumberText(1));
            Assert.AreEqual(FireRescueGame.UpgradesPrompt,game.UpgradeExplainText);
            yield return Wait(.3f);var eye=game.Player.ViewCamera.transform.position;
            Note(string.Format(C,"upgrades centre {0:F1} deg down",Down(eye,root.position)));
            Capture(game,"5-upgrades",root.position);
            for(int i=0;i<4;i++)
            {
                game.ForcedUpgradeHover=i;yield return null;yield return Wait(.35f);
                Assert.AreEqual(i,game.HoveredUpgrade);Assert.AreNotEqual(FireRescueGame.UpgradesPrompt,game.UpgradeExplainText);
                Assert.True(game.UpgradeShowsGain(i),"Green arrow and the value after buying");
                CaptureAt(game,"5"+(char)('b'+i)+"-upgrades-hover-"+new[]{"gem","pump","slot","tower"}[i],root.position,40);
            }
            game.ForcedUpgradeHover=1;yield return null;Assert.AreEqual($"+{run.PumpStrikeWater+FireRun.PumpWaterPerLevel:0}",game.UpgradeNumberText(1));
            game.ForcedUpgradeHover=-1;yield return null;
            if(game.UpgradeBuyable(1)){int level=run.PumpLevel;Button(game,"Upgrade pump").Press();yield return null;Assert.AreEqual(level+1,run.PumpLevel);}
        }

        // ---- Level select ------------------------------------------------------------------------------------------
        [UnityTest] public IEnumerator FirstLevelSelectShowsTheSlapHintAndBothArrows()
        {
            yield return Load(0);
            var game=Object.FindFirstObjectByType<FireRescueGame>();var run=game.Run;
            Assert.AreEqual(0,run.HighestUnlockedStage,"A first-time player: one level unlocked");
            game.OpenLevelSelect(0);yield return null;yield return null;Assert.True(game.LevelSelectVisible);
            Assert.True(game.Hints.Visible,"The slap hint shows with a single level unlocked");Assert.AreEqual(EmberdeepFirstHints.Move.Slap,game.Hints.Showing);
            var zone=game.SlapZone();Assert.True(zone.Contains(game.Hints.Demo.transform.position,out _),"It plays where a slap counts");
            Assert.True(game.BrowseArrowsVisible);
            Assert.False(game.Arena.Find("Level select").GetComponentsInChildren<Renderer>(true).First(r=>r.name=="Level previous arrow").gameObject.activeSelf,"Nothing before level 1");
            game.BrowseLevel(1);yield return null;yield return Wait(.4f);
            Assert.True(game.Arena.Find("Level select").GetComponentsInChildren<Renderer>(true).First(r=>r.name=="Level previous arrow").gameObject.activeSelf,"Both arrows mid-carousel");
            game.BrowseLevel(-1);yield return Wait(.6f);
            var select=game.Arena.Find("Level select");var eye=game.Player.ViewCamera.transform.position;
            Note(string.Format(C,"slap hint {0:F2} m from the eye, {1:F1} deg down; carousel centre {2:F1} deg",Vector3.Distance(eye,game.Hints.Demo.transform.position),
                Down(eye,game.Hints.Demo.transform.position),Down(eye,select.position)));
            Capture(game,"6-level-select-slap-hint",select.position+Vector3.down*.05f);
            yield return Wait(.5f);Capture(game,"6b-level-select-slap-hint-later",select.position+Vector3.down*.05f);
        }
    }
}

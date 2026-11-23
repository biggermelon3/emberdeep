using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using PhysicalAssembly;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    // Headset test 2026-11-22 follow-up: the seated view of the default board, the rim readouts, every between-level
    // panel and the see-through tree fade, measured and captured from the desktop seat (the same eye the board is
    // placed from). Writes Logs/EmberdeepSeatedView/<label>/ (label from Logs/EmberdeepSeatedView/label.txt, else
    // "latest"): view.txt (numbers) and one PNG per view. Asserts only the targets of that follow-up:
    //   - front row ~40 cm, 4-row mid-board <= 35 deg down, back-row squares within ~72 cm (horizontal, from the eye);
    //   - the readout strip behind the back edge, above the board's skyline, under every panel;
    //   - every panel button between eye level and 15 deg below it.
    public sealed class EmberdeepSeatedViewPlayTests
    {
        const string Root="Logs/EmberdeepSeatedView";
        // A seated shoulder relative to the eye: 19 cm to the side, 23 cm down, 8 cm back (reach is reported from it too).
        static readonly Vector3 Shoulder=new(.19f,-.23f,-.08f);
        static string Folder
        {
            get
            {
                string label="latest";
                try{if(File.Exists(Root+"/label.txt"))label=File.ReadAllText(Root+"/label.txt").Trim();}catch(IOException){}
                if(string.IsNullOrEmpty(label))label="latest";
                var f=Root+"/"+label;Directory.CreateDirectory(f);return f;
            }
        }
        static readonly CultureInfo C=CultureInfo.InvariantCulture;
        static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}

        // ---- geometry from the eye ------------------------------------------------------------------------------
        sealed class Seat
        {
            public Vector3 Eye,Forward,Right;
            public Seat(FireRescueGame game)
            {
                Eye=game.Player.ViewCamera.transform.position;
                Forward=Vector3.ProjectOnPlane(game.Arena.forward,Vector3.up).normalized;Right=Vector3.Cross(Vector3.up,Forward);
            }
            public float Ahead(Vector3 p)=>Vector3.Dot(p-Eye,Forward);
            public float Flat(Vector3 p)=>Vector3.ProjectOnPlane(p-Eye,Vector3.up).magnitude;
            public float Below(Vector3 p)=>Eye.y-p.y;
            // Degrees below eye level (negative = above).
            public float Down(Vector3 p)=>Mathf.Atan2(Below(p),Flat(p))*Mathf.Rad2Deg;
            public float FromShoulder(Vector3 p,float side)
            {
                var s=Eye+Right*(Shoulder.x*side)+Vector3.up*Shoulder.y+Forward*Shoulder.z;return Vector3.Distance(p,s);
            }
        }
        static Bounds RendererBounds(Transform root,bool active=true)
        {
            var rs=root.GetComponentsInChildren<Renderer>(!active).Where(r=>!(r is ParticleSystemRenderer) && r.enabled && (!active || r.gameObject.activeInHierarchy)).ToArray();
            if(rs.Length==0)return new Bounds(root.position,Vector3.zero);
            var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;
        }
        // The board's skyline from the eye: the highest-looking top of anything on the board (not the rim HUD or panels).
        // vane: include the weathervane (thin, centred); trees: only the incident forest.
        static (float angle,string name) Skyline(FireRescueGame game,Seat seat,bool vane=true,bool treesOnly=false)
        {
            float best=90;string name="";
            var hud=game.RimHud?game.RimHud.Root:null;var vaneRoot=game.Weathervane?game.Weathervane.transform:null;
            var forest=game.Arena.Find("Incident forest");
            foreach(var r in game.Arena.GetComponentsInChildren<MeshRenderer>())
            {
                if(!r.enabled || !r.gameObject.activeInHierarchy)continue;
                var t=r.transform;if(hud && t.IsChildOf(hud))continue;
                if(!vane && vaneRoot && t.IsChildOf(vaneRoot))continue;
                if(treesOnly && (!forest || !t.IsChildOf(forest)))continue;
                if(t.GetComponentInParent<WorkshopButton>() || IsScreen(t))continue;
                var b=r.bounds;if(b.size.x>.5f)continue; // terrain slabs: their top is the board top
                var top=new Vector3(b.center.x,b.max.y,b.center.z);float a=seat.Down(top);
                if(a<best){best=a;name=r.name+" @"+seat.Ahead(top).ToString("F2",C)+" m, "+(top.y-game.Arena.position.y).ToString("F3",C)+" m up, x "+Vector3.Dot(top-seat.Eye,seat.Right).ToString("F2",C);}
            }
            return (best,name);
        }
        static string Skylines(FireRescueGame game)
        {
            var seat=new Seat(game);var all=Skyline(game,seat);var board=Skyline(game,seat,false);var trees=Skyline(game,seat,false,true);
            string v="";
            if(game.Weathervane && game.Weathervane.gameObject.activeInHierarchy)
            {var b=RendererBounds(game.Weathervane.transform);v=string.Format(C,", vane x {0:F3}..{1:F3} m",Vector3.Dot(b.min-seat.Eye,seat.Right),Vector3.Dot(b.max-seat.Eye,seat.Right));}
            return string.Format(C,"skyline {0:F1} deg ({1}); without the vane {2:F1} ({3}); trees {4:F1} ({5}){6}",all.angle,all.name,board.angle,board.name,trees.angle,trees.name,v);
        }
        static bool IsScreen(Transform t)
        {
            for(var p=t;p;p=p.parent)if(p.name=="Pause panel" || p.name=="Results panel" || p.name=="Level select" || p.name=="Upgrades panel")return true;
            return false;
        }

        // ---- capture --------------------------------------------------------------------------------------------
        static void Capture(FireRescueGame game,string name,float pitch,float fov=92)
        {
            var camera=game.Player.ViewCamera;
            var rotation=camera.transform.rotation;float priorFov=camera.fieldOfView;var prior=camera.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(1200,1100,24,RenderTextureFormat.ARGB32){antiAliasing=4};var texture=new Texture2D(1200,1100,TextureFormat.RGBA32,false);
            try
            {
                camera.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(game.Arena.forward,Vector3.up),Vector3.up)*Quaternion.Euler(pitch,0,0);camera.fieldOfView=fov;
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1200,1100),0,0);texture.Apply();
                File.WriteAllBytes(Folder+"/"+name+".png",texture.EncodeToPNG());
            }
            finally{camera.transform.rotation=rotation;camera.fieldOfView=priorFov;camera.targetTexture=prior;RenderTexture.active=active;target.Release();Object.Destroy(target);Object.Destroy(texture);}
        }

        // ---- flow helpers (as EmberdeepRimUiPlayTests) ------------------------------------------------------------
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
        static WorkshopButton Button(FireRescueGame game,string command)=>game.GetComponentsInChildren<WorkshopButton>(true).Single(b=>b.Command==command);
        static IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);yield return null;yield return null;
            Object.FindFirstObjectByType<FireRescueGame>().SendMessage("OnApplicationFocus",true);
            yield return Wait(1.2f); // the rim re-measures the plinth every second
        }
        static int[] SaveHints(){var m=(EmberdeepFirstHints.Move[])System.Enum.GetValues(typeof(EmberdeepFirstHints.Move));return m.Select(x=>PlayerPrefs.GetInt(EmberdeepFirstHints.Key(x),-1)).ToArray();}
        static void RestoreHints(int[] saved)
        {
            var m=(EmberdeepFirstHints.Move[])System.Enum.GetValues(typeof(EmberdeepFirstHints.Move));
            for(int i=0;i<m.Length;i++)if(saved[i]<0)PlayerPrefs.DeleteKey(EmberdeepFirstHints.Key(m[i]));else PlayerPrefs.SetInt(EmberdeepFirstHints.Key(m[i]),saved[i]);
        }

        // ---- the board, per depth ---------------------------------------------------------------------------------
        static string Board(FireRescueGame game,out float mid,out float front,out float backSquare)
        {
            var seat=new Seat(game);var arena=game.Arena;var run=game.Run;
            float fz=run.Trees.Min(t=>t.Position.z),bz=run.Trees.Max(t=>t.Position.z);
            front=seat.Ahead(arena.TransformPoint(new Vector3(0,0,fz)));float back=seat.Ahead(arena.TransformPoint(new Vector3(0,0,bz)));
            mid=seat.Down(arena.TransformPoint(new Vector3(0,0,(fz+bz)*.5f)));
            float frontAngle=seat.Down(arena.TransformPoint(new Vector3(0,0,fz))),backAngle=seat.Down(arena.TransformPoint(new Vector3(0,0,bz)));
            // Back-row piece squares (L3): the deepest row of squares.
            backSquare=0;float worst=0,shoulder=0;string worstAt="";
            if(run.Squares.Length>0)
            {
                float deepest=run.Squares.Max(s=>s.z);int side=EmberdeepWaterHand.Left?1:-1; // the free hand places pieces
                foreach(var s in run.Squares)
                {
                    if(s.z<deepest-.01f)continue;var w=arena.TransformPoint(s);
                    float flat=seat.Flat(w);if(Mathf.Abs(s.x)<.7f)backSquare=Mathf.Max(backSquare,flat);
                    if(flat>worst){worst=flat;worstAt=$"({s.x:F2},{s.z:F2})";}
                    shoulder=Mathf.Max(shoulder,Mathf.Min(seat.FromShoulder(w,side),seat.FromShoulder(w,-side)));
                }
            }
            var board=game.RimHud?game.RimHud.Board:EmberdeepRimHud.DefaultBoard;
            float frontEdge=seat.Ahead(arena.TransformPoint(new Vector3(0,0,board.min.z))),backEdge=seat.Ahead(arena.TransformPoint(new Vector3(0,0,board.max.z)));
            return string.Format(C,"stage {0} ({1} rows, {2} trees): front row {3:F3} m @ {4:F1} deg, back row {5:F3} m @ {6:F1} deg, board top {7:F3} m below the eye, "+
                "mid-field {8:F1} deg down; back-row squares: centre {9:F3} m, worst {10:F3} m {11} (from the nearer shoulder, 3D: worst {12:F3} m); "+
                "plinth front edge {13:F3} m, back edge {14:F3} m; {15}",
                run.Stage,run.FieldRows,run.Trees.Length,front,frontAngle,back,backAngle,seat.Below(arena.position),mid,backSquare,worst,worstAt,shoulder,frontEdge,backEdge,Skylines(game));
        }
        static string Rim(FireRescueGame game)
        {
            var seat=new Seat(game);var hud=game.RimHud;var sb=new StringBuilder();
            if(hud.StripVisible)
            {
                var b=RendererBounds(hud.Strip);
                sb.AppendFormat(C,"strip centre {0:F3} m ahead, {1:F3} m below the eye, {2:F1} deg down (top {3:F1}, bottom {4:F1}), {5:F3} m wide x {6:F3} m tall, faces the eye {7:F2}; ",
                    seat.Ahead(hud.Strip.position),seat.Below(hud.Strip.position),seat.Down(hud.Strip.position),seat.Down(new Vector3(b.center.x,b.max.y,b.center.z)),
                    seat.Down(new Vector3(b.center.x,b.min.y,b.center.z)),hud.Strip.lossyScale.x*EmberdeepRimHud.StripWidth,hud.Strip.lossyScale.y*EmberdeepRimHud.StripHeight,
                    Vector3.Dot(-hud.Strip.forward,(seat.Eye-hud.Strip.position).normalized));
            }
            if(hud.TokenVisible)sb.AppendFormat(C,"token {0:F3} m ahead, {1:F3} m to the side, {2:F1} deg down; ",seat.Ahead(hud.TokenCenter),Vector3.Dot(hud.TokenCenter-seat.Eye,seat.Right),seat.Down(hud.TokenCenter));
            if(hud.BellVisible)sb.AppendFormat(C,"bell {0:F3} m ahead, {1:F3} m to the side, {2:F1} deg down; ",seat.Ahead(hud.BellTop),Vector3.Dot(hud.BellTop-seat.Eye,seat.Right),seat.Down(hud.BellTop));
            sb.Append(Skylines(game));
            return sb.ToString();
        }
        // Every active button of a panel: degrees below eye level. Also the panel's own extent and the strip's top.
        static string Panel(FireRescueGame game,string root,out float lowest,out float highest,out float panelBottom)
        {
            var seat=new Seat(game);var t=game.Arena.Find(root);var sb=new StringBuilder(root+": ");
            lowest=-90;highest=90;
            foreach(var b in t.GetComponentsInChildren<WorkshopButton>())
            {
                var p=b.GetComponent<Collider>()?b.GetComponent<Collider>().bounds.center:b.transform.position;float a=seat.Down(p);
                lowest=Mathf.Max(lowest,a);highest=Mathf.Min(highest,a);
                sb.AppendFormat(C,"{0} {1:F1} deg ({2:F2} m); ",b.Command,a,Vector3.Distance(p,seat.Eye));
            }
            var bounds=RendererBounds(t);panelBottom=seat.Down(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z));
            sb.AppendFormat(C,"panel centre {0:F1} deg at {1:F2} m, top {2:F1}, bottom {3:F1}",seat.Down(bounds.center),seat.Flat(bounds.center),
                seat.Down(new Vector3(bounds.center.x,bounds.max.y,bounds.center.z)),panelBottom);
            if(game.RimHud && game.RimHud.StripVisible)
            {
                var s=RendererBounds(game.RimHud.Strip);sb.AppendFormat(C,"; strip top {0:F1} deg",seat.Down(new Vector3(s.center.x,s.max.y,s.center.z)));
            }
            sb.Append("; ").Append(Skylines(game));
            return sb.ToString();
        }
        static float Centre(FireRescueGame game,string root){var seat=new Seat(game);return seat.Down(RendererBounds(game.Arena.Find(root)).center);}

        [UnityTest] public IEnumerator DefaultBoardRimAndPanelsFromTheSeat()
        {
            var hints=SaveHints();EmberdeepFirstHints.ResetAll();var saved=EmberdeepSettingsPrefs.Save();var log=new StringBuilder();
            float deepFront=0,deepMid=0,deepSquare=0;string deepLine="";
            try
            {
                PlayerPrefs.DeleteKey(EmberdeepPlayArea.HeightKey);PlayerPrefs.DeleteKey(EmberdeepPlayArea.DistanceKey);PlayerPrefs.DeleteKey(EmberdeepPlayArea.TiltedKey);EmberdeepPlayArea.Reload();
                // Measured flat: these limits were set for the flat board. Tilted (the default since headset test 4) raises the
                // strip and so the pause panel: its top button sits ~5 deg over eye level (BOARD hand-over).
                EmberdeepPlayArea.SetTilted(false);
                // The default board at each depth: 2 rows (stage 1), 3 rows (stage 3), 4 rows (stage 5).
                foreach(int stage in new[]{1,5})
                {
                    EmberdeepSession.NewExpedition();var run=EmberdeepSession.Run;typeof(FireRun).GetProperty("Stage").SetValue(run,stage);Assert.IsTrue(run.StartStage(true));
                    yield return Load();var game=Object.FindFirstObjectByType<FireRescueGame>();
                    var line=Board(game,out float mid,out float front,out float backSquare);log.AppendLine(line);Debug.Log("EMBERDEEP_SEATED "+line);
                    log.AppendLine("  plan rim: "+Rim(game));
                    Capture(game,$"1-board-stage{stage}",mid);
                    if(stage==5){Capture(game,"1b-board-stage5-natural",30);deepFront=front;deepMid=mid;deepSquare=backSquare;deepLine=line;}
                }

                EmberdeepSession.NewExpedition();AdvanceTo(EmberdeepSession.Run,3);Assert.AreEqual(3,EmberdeepSession.Run.Stage);
                yield return Load();
                {
                    var game=Object.FindFirstObjectByType<FireRescueGame>();var run=game.Run;var hud=game.RimHud;
                    var line=Board(game,out float mid,out _,out _);log.AppendLine(line);log.AppendLine("  plan rim: "+Rim(game));
                    Capture(game,"1-board-stage3",mid);Capture(game,"2-plan-rim-stage3",mid);

                    // Fight readouts: one sheep saved, one on fire, two charred trees, the plane ring half charged.
                    Assert.True(game.StrikeBell());EmberdeepTestFlow.StartPrepared(run);yield return null;yield return null;
                    run.Animals[0].State=AnimalState.Rescued;if(run.Animals.Length>1)run.Animals[1].OnFire=true;Char(run,2);
                    hud.PlaneAvailable=true;hud.PlaneCharge01=.6f;yield return Wait(.6f);
                    log.AppendLine("  fight rim: "+Rim(game));
                    var seat=new Seat(game);var sky=Skyline(game,seat,false); // the vane stands in the strip's middle gap
                    var strip=RendererBounds(hud.Strip);float stripBottom=seat.Down(new Vector3(strip.center.x,strip.min.y,strip.center.z));
                    Capture(game,"3-fight-readouts",mid);Capture(game,"3b-fight-readouts-strip",seat.Down(hud.Strip.position));
                    hud.PlaneAvailable=false;if(run.Animals.Length>1)run.Animals[1].OnFire=false;

                    // Pause.
                    Assert.True(game.OpenPause());yield return Wait(.5f);
                    log.AppendLine("  "+Panel(game,"Pause panel",out float low,out float high,out float bottom));
                    Capture(game,"4-pause",Centre(game,"Pause panel"));Capture(game,"4b-pause-and-board",Centre(game,"Pause panel")+14);
                    float pauseLow=low,pauseHigh=high,pauseBottom=bottom;
                    Button(game,"Pause resume").Press();yield return null;

                    // Lost.
                    Char(run,run.BurnLimit);run.Tick(.05f,false);Assert.AreEqual(FirePhase.Lost,run.Phase);
                    yield return Wait(FireRescueGame.ResultsDelay+FireRescueGame.ResultsRise+.3f);Assert.True(game.ResultsVisible);
                    log.AppendLine("  lost "+Panel(game,"Results panel",out low,out high,out bottom));
                    Capture(game,"5-lost",Centre(game,"Results panel"));Capture(game,"5b-lost-and-rim",Centre(game,"Results panel")+14);
                    float lostLow=low,lostHigh=high,lostBottom=bottom;
                    Button(game,"Results retry big").Press();yield return null;

                    // Cleared.
                    EmberdeepTestFlow.StartPrepared(run);yield return null;
                    for(int i=0;i<200 && run.Phase==FirePhase.Fighting;i++){Clear(run);run.Tick(.05f,false);}
                    Assert.AreEqual(FirePhase.Cleared,run.Phase);run.EvacuateSafeAnimals();
                    yield return Wait(FireRescueGame.ResultsDelay+FireRescueGame.ResultsRise+.9f);Assert.True(game.ResultsVisible);
                    log.AppendLine("  cleared "+Panel(game,"Results panel",out low,out high,out bottom));
                    Capture(game,"6-cleared",Centre(game,"Results panel"));
                    float clearedLow=low,clearedHigh=high;

                    // Upgrades.
                    Button(game,"Results upgrades").Press();yield return Wait(.3f);Assert.True(game.UpgradesVisible);
                    log.AppendLine("  "+Panel(game,"Upgrades panel",out low,out high,out bottom));
                    Capture(game,"7-upgrades",Centre(game,"Upgrades panel"));
                    float upgradesLow=low,upgradesHigh=high;
                    Button(game,"Upgrades back").Press();yield return null;yield return null;

                    // Level select.
                    Button(game,"Results next").Press();yield return Wait(.8f);Assert.True(game.LevelSelectVisible);
                    log.AppendLine("  "+Panel(game,"Level select",out low,out high,out bottom));
                    Capture(game,"8-level-select",Centre(game,"Level select"));Capture(game,"8b-level-select-and-board",Centre(game,"Level select")+14);
                    float selectLow=low,selectHigh=high,selectBottom=bottom;
                    var selectSky=Skyline(game,new Seat(game));

                    File.WriteAllText(Folder+"/view.txt",log.ToString());
                    // Targets (headset test 2026-11-22): buttons between eye level and 15 deg below; the strip over the
                    // skyline; panels over the strip and the skyline.
                    // The level cards (browse targets; slap is the main browse) may stand up to 4.5 deg over eye level: the carousel is
                    // ~14 deg from card centre to the PLAY row, and that row must clear a 4-row forest's skyline.
                    foreach(var (name,lo,hi) in new[]{("pause",pauseLow,pauseHigh),("lost",lostLow,lostHigh),("cleared",clearedLow,clearedHigh),("upgrades",upgradesLow,upgradesHigh),("select",selectLow,selectHigh)})
                    {
                        Assert.LessOrEqual(lo,15.5f,$"{name}: lowest button\n{log}");Assert.GreaterOrEqual(hi,name=="select"?-4.5f:-.5f,$"{name}: highest button\n{log}");
                    }
                    Assert.Less(stripBottom,sky.angle+.01f,$"strip seen over the board's skyline\n{log}");
                    foreach(var (name,b) in new[]{("pause",pauseBottom),("lost",lostBottom)})
                        Assert.Less(b,seat.Down(new Vector3(strip.center.x,strip.max.y,strip.center.z)),$"{name} panel stays over the strip\n{log}");
                    Assert.Less(selectBottom,selectSky.angle,$"level select over the board's skyline\n{log}");
                    // Headset test 4 default (-14 / +12 cm, measured flat): front row 52 cm, ~37 deg to the middle of 4 rows;
                    // the back-row squares (~.84 m flat, ~.80 m on the default tilted board) need a seated lean, the owner's
                    // call ("want it farther").
                    Assert.AreEqual(.52f,deepFront,.025f,"front row ~52 cm: "+deepLine);
                    Assert.LessOrEqual(deepMid,41f,"4-row mid-board look-down: "+deepLine);
                    Assert.LessOrEqual(deepSquare,.86f,"back-row squares within a seated lean: "+deepLine);
                }
            }
            finally{RestoreHints(hints);saved.Restore();EmberdeepPlayArea.Reload();File.WriteAllText(Folder+"/view.txt",log.ToString());}
        }

        // The see-through fade from the seat: aim the desktop mouse through the front rows at a back-row fire.
        [UnityTest] public IEnumerator TreeFadeFromTheSeat()
        {
            const int BackRow=24;
            EmberdeepSession.NewExpedition();var run=EmberdeepSession.Run;
            typeof(FireRun).GetProperty("Stage").SetValue(run,5);run.LevelOverride=EmberdeepLevels.GeneratedGrid(5);
            Assert.IsTrue(run.StartStage());
            foreach(var tree in run.Trees){tree.State=TreeState.Safe;tree.Heat=0;tree.Wetness=0;}
            run.Trees[BackRow].State=TreeState.Burning;run.Trees[BackRow].Heat=85;
            var background=InputSystem.settings.backgroundBehavior;var editor=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var mouse=InputSystem.AddDevice<Mouse>();
            try
            {
                yield return Load();var game=Object.FindFirstObjectByType<FireRescueGame>();var camera=game.Player.ViewCamera;
                var target=camera.WorldToScreenPoint(game.Arena.TransformPoint(run.Trees[BackRow].Position));
                InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(target.x,target.y)});
                yield return Wait(.5f);
                var readout=game.TreeReadout;var faded=new List<int>();
                for(int i=0;i<readout.Count;i++)if(readout.FadeOf(i)>.001f)faded.Add(i);
                int Occluder=faded.Count>0?faded[faded.Count-1]:17; // the faded tree nearest the target
                string line=string.Format(C,"fade: MaximumFade {0:F2} (coverage {1:P0}), FadeMargin {2:F2}, faded trees [{3}], tree {4} fade {5:F2}",
                    EmberdeepTreeReadout.MaximumFade,1-EmberdeepTreeReadout.MaximumFade,EmberdeepTreeReadout.FadeMargin,string.Join(",",faded),Occluder,readout.FadeOf(Occluder));
                Debug.Log("EMBERDEEP_SEATED "+line);File.WriteAllText(Folder+"/fade.txt",line+"\n");
                var seat=new Seat(game);var at=game.Arena.TransformPoint(run.Trees[Occluder].Position);
                Capture(game,"9-tree-fade",seat.Down(at),60);
                InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(4,Screen.height-4)});yield return Wait(.4f);
                Capture(game,"9b-tree-no-fade",seat.Down(at),60);
                Assert.Greater(faded.Count,0,"aiming through the front rows fades the tree in the way: "+line);
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editor;
                EmberdeepSession.NewExpedition();
            }
        }
    }
}

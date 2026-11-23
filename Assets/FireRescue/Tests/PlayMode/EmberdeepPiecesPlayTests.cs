using System.Collections;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    // LoopSpec §5-§6 (workstream L3) in the fireground: the time-stopped Plan look and its flood back, the tray and the
    // placement ghost, a tower's rain, the excavator's line and trench, and the plane run. Headset test 3: towers pinched
    // and grabbed from the tray by synthetic tracked hands (hover highlight first), an excavator's draw vs a pinch, and the
    // tower tip (flat, tilted, right water hand, zh-Hans; off the bell). Writes seated-eye captures to Logs/EmberdeepPieces/.
    public sealed class EmberdeepPiecesPlayTests
    {
        const string Output="Logs/EmberdeepPieces";
        // The seated desktop eye looking down at the board (pitch) and turned toward the tray (yaw), headset-like fov.
        static void Capture(FireRescueGame game,string name,float pitch=46,float yaw=0,float fov=92,Quaternion? look=null)
        {
            Directory.CreateDirectory(Output);var camera=game.Player.ViewCamera;
            var rotation=camera.transform.rotation;float priorFov=camera.fieldOfView;var prior=camera.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(1200,1100,24,RenderTextureFormat.ARGB32){antiAliasing=4};var texture=new Texture2D(1200,1100,TextureFormat.RGBA32,false);
            try
            {
                camera.transform.rotation=look??Quaternion.LookRotation(game.Arena.forward,Vector3.up)*Quaternion.Euler(pitch,yaw,0);camera.fieldOfView=fov;
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1200,1100),0,0);texture.Apply();
                File.WriteAllBytes(Output+"/"+name+".png",texture.EncodeToPNG());
            }
            finally{camera.transform.rotation=rotation;camera.fieldOfView=priorFov;camera.targetTexture=prior;RenderTexture.active=active;target.Release();Object.Destroy(target);Object.Destroy(texture);}
        }
        static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
        static void Clear(FireRun run){foreach(var a in run.Animals)run.ApplyAnimalWater(a.Position,100,.6f);foreach(var t in run.Trees)if(t.State==TreeState.Burning)run.ApplyWater(t.Position,1000,.1f);run.ApplyWater(Vector3.zero,10000,100);}
        // Clears levels until the given incident is laid out in Plan (keyed by IncidentIndex, never Stage).
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
        FireRescueGame game;int[] hints;int waterHand,towerTip=-2;bool? tilted;
        static readonly EmberdeepFirstHints.Move[] Moves=(EmberdeepFirstHints.Move[])System.Enum.GetValues(typeof(EmberdeepFirstHints.Move));
        // tip: leave the tower tip new (shown); otherwise it is retired with the ghost hands, out of the captures.
        IEnumerator Load(int incident,bool tip=false)
        {
            // Retire the first-time ghost hands so they stay out of the captures (restored in TearDown).
            hints=Moves.Select(m=>PlayerPrefs.GetInt(EmberdeepFirstHints.Key(m),-1)).ToArray();
            foreach(var m in Moves)PlayerPrefs.SetInt(EmberdeepFirstHints.Key(m),99);
            towerTip=PlayerPrefs.GetInt(EmberdeepTowerTip.Key,-1);
            if(tip)PlayerPrefs.DeleteKey(EmberdeepTowerTip.Key);else PlayerPrefs.SetInt(EmberdeepTowerTip.Key,1);
            tilted=EmberdeepPlayArea.Tilted;if(tilted.Value)EmberdeepPlayArea.SetTilted(false);
            waterHand=PlayerPrefs.GetInt(EmberdeepWaterHand.PrefsKey,-1);EmberdeepWaterHand.Set(true);
            // The generated grid these piece tests were written against (levels are hand-authored since LV).
            EmberdeepSession.NewExpedition();EmberdeepSession.Run.LevelOverride=EmberdeepLevels.GeneratedGrid(incident+FireRun.PalmLessons);
            AdvanceToIncident(EmberdeepSession.Run,incident);
            // By path in the editor: the shared editor's build list is switched between this game and the legacy scenes by
            // several sessions, and these tests never leave the fireground.
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/FireRescue/Scenes/"+EmberdeepSession.FireScene+".unity",new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);
#endif
            yield return null;yield return null;
            game=Object.FindFirstObjectByType<FireRescueGame>();game.SendMessage("OnApplicationFocus",true);
            yield return Wait(.6f);
            Assert.NotNull(game.PieceView);Assert.NotNull(game.PlanLook);
        }
        [TearDown] public void Restore()
        {
            if(hints!=null)for(int i=0;i<Moves.Length;i++)if(hints[i]<0)PlayerPrefs.DeleteKey(EmberdeepFirstHints.Key(Moves[i]));else PlayerPrefs.SetInt(EmberdeepFirstHints.Key(Moves[i]),hints[i]);
            if(waterHand<0)PlayerPrefs.DeleteKey(EmberdeepWaterHand.PrefsKey);else PlayerPrefs.SetInt(EmberdeepWaterHand.PrefsKey,waterHand);
            if(towerTip==-1)PlayerPrefs.DeleteKey(EmberdeepTowerTip.Key);else if(towerTip>=0)PlayerPrefs.SetInt(EmberdeepTowerTip.Key,towerTip);
            if(tilted.HasValue && EmberdeepPlayArea.Tilted!=tilted.Value)EmberdeepPlayArea.SetTilted(tilted.Value);
            if(game)game.ClearScriptPieceHands();
            PlayerPrefs.Save();EmberdeepWaterHand.Reload();hints=null;towerTip=-2;tilted=null;
        }
        int FreeSquare(int piece,System.Func<int,bool> also=null)
        {
            var run=game.Run;
            return Enumerable.Range(0,run.Squares.Length).First(s=>run.CanPlace(piece,s)==FirePlaceBlock.None && (also==null || also(s)));
        }
        // The real start: strike the bell, then the 3 s countdown runs out in the game's own Update (which is what starts
        // the fireground's sim clock).
        IEnumerator StartFight()
        {
            Assert.True(game.StrikeBell());
            for(float until=Time.realtimeSinceStartup+4.5f;Time.realtimeSinceStartup<until && game.Run.Phase==FirePhase.Preparing;)yield return null;
            Assert.AreEqual(FirePhase.Fighting,game.Run.Phase);yield return null;
        }
        static ParticleSystem Flames(FireRescueGame game)=>game.Effects.GetComponentsInChildren<ParticleSystem>().First(p=>p.name=="Turbulent flame particles");

        [UnityTest] public IEnumerator PlanStopsTimeThenColourFloodsBackWhenTheFireResumes()
        {
            yield return Load(2);
            var run=game.Run;var look=game.PlanLook;var flames=Flames(game);
            Assert.AreEqual(FirePhase.Preparing,run.Phase);Assert.True(look.Planning);
            Assert.AreEqual(1,Shader.GetGlobalFloat(EmberdeepPlanLook.GradeId),1e-4f,"Whitish grade on the board");
            Assert.AreEqual(1,Shader.GetGlobalFloat(EmberdeepPlanLook.ClockOnId));
            Assert.True(game.Effects.Frozen);Assert.True(flames.isPaused,"Fire frozen mid-motion");Assert.Greater(game.Effects.VisibleFlames,30,"Warmed up, not empty");
            float clock=Shader.GetGlobalFloat(EmberdeepPlanLook.ClockId),env=game.EnvironmentVfx.Clock;int count=game.Effects.VisibleFlames;
            var before=new ParticleSystem.Particle[count];flames.GetParticles(before);
            yield return Wait(.5f);
            Assert.AreEqual(clock,Shader.GetGlobalFloat(EmberdeepPlanLook.ClockId),"Shader time stopped");
            Assert.AreEqual(env,game.EnvironmentVfx.Clock,"Creek, wind streaks and updrafts stopped");
            var after=new ParticleSystem.Particle[count];Assert.AreEqual(count,flames.GetParticles(after));
            for(int i=0;i<count;i++)Assert.AreEqual(before[i].position,after[i].position,"Every flame tongue holds still");
            Assert.Greater(look.HumVolume,.05f,"The ambience is a low hum");Assert.Zero(look.CrackleVolume);
            Capture(game,"1-PlanLook");
            // Start: the bell, the countdown (still stopped), then the fire resumes and colour floods back.
            Assert.True(game.StrikeBell());yield return null;Assert.True(look.Planning,"Time stays stopped through the countdown");
            EmberdeepTestFlow.StartPrepared(run);yield return null;yield return null;
            Assert.AreEqual(FirePhase.Fighting,run.Phase);Assert.False(game.Effects.Frozen);Assert.False(flames.isPaused);
            Assert.True(look.Flooding);Assert.AreEqual(1,look.Whooshes,"With a whoosh");
            yield return Wait(.3f);
            Assert.Greater(look.FloodRadius,.05f);Assert.Less(look.FloodRadius,EmberdeepPieceTuning.FloodReach);
            Capture(game,"1b-ColourFloodsBack");
            yield return Wait(EmberdeepPieceTuning.FloodSeconds);
            Assert.False(look.Flooding);Assert.Zero(Shader.GetGlobalFloat(EmberdeepPlanLook.GradeId));
            Assert.Greater(Shader.GetGlobalFloat(EmberdeepPlanLook.ClockId),clock,"The clock runs again");
            yield return Wait(.5f);Assert.Greater(look.CrackleVolume,0,"The fire's crackle comes up");Assert.Less(look.HumVolume,.05f);
            // Leaving the fireground hands the globals back (the camp runs on _Time, ungraded).
            Object.Destroy(look);yield return null;
            Assert.Zero(Shader.GetGlobalFloat(EmberdeepPlanLook.ClockOnId));Assert.Zero(Shader.GetGlobalFloat(EmberdeepPlanLook.GradeId));
        }

        [UnityTest] public IEnumerator TrayBesideTheBoardOnTheFreeHandSideAndAGhostOnTheDropSquare()
        {
            yield return Load(2);
            var run=game.Run;var view=game.PieceView;var hud=game.RimHud;
            Assert.True(view.TrayVisible);Assert.AreEqual(2,run.Pieces.Length);
            // Headset test 4: the tray is one row centred on the board's front lip, in front of the front row.
            var tray=game.Arena.InverseTransformPoint(view.Tray.position);
            Assert.AreEqual(hud.Board.center.x,tray.x,.01f,"Centred");Assert.Less(tray.z+EmberdeepPieceView.TrayDepth*.5f,EmberdeepBoardLayout.FrontRowZ,"In front of the front row");
            Assert.AreEqual(2,view.TraySlotsShown,"One slot per piece");
            {
                // Seated numbers for the hand-over (default seat, whatever tilt the editor's prefs hold).
                var seat=game.Player.ViewCamera.transform.position;var fwd=Vector3.ProjectOnPlane(game.Arena.forward,Vector3.up).normalized;
                string SeatAt(Vector3 w){var d=w-seat;float f=Vector3.Dot(d,fwd);return $"{f:0.000} m out, {Mathf.Atan2(-d.y,f)*Mathf.Rad2Deg:0.0} deg down";}
                float backZ=run.Trees.Max(t=>t.Position.z);
                Debug.Log($"EMBERDEEP_TRAY_SEAT tilt={game.BoardTilt:0} tray: {SeatAt(view.Tray.position+game.Arena.up*EmberdeepPieceView.TrayLift*FireMiniature.Scale)} front row: {SeatAt(game.Arena.TransformPoint(new Vector3(0,0,EmberdeepBoardLayout.FrontRowZ)))} back row: {SeatAt(game.Arena.TransformPoint(new Vector3(0,0,backZ)))} 4th row: {SeatAt(game.Arena.TransformPoint(new Vector3(0,0,EmberdeepBoardLayout.FrontRowZ+3*EmberdeepBoardLayout.RowSpacing)))}");
            }
            // Within seated reach of the free hand: from about where that shoulder is (18 cm aside, 20 cm under the eye), the
            // palm centre must get within GrabReach of every tray piece's visible box (EmberdeepPieceGrasp's grab rule; a
            // fingertip or a pinch reaches further still). Since headset test 3 a hand takes a piece at its box, not at
            // its middle, so the tray's centre is no longer what the arm has to reach.
            var eye=game.Arena.InverseTransformPoint(game.Player.ViewCamera.transform.position);
            var shoulder=game.Arena.TransformPoint(eye+new Vector3(.18f,-.2f,0)/FireMiniature.Scale);
            float centre=Vector3.Distance(shoulder,view.Tray.position),reach=0;int farthest=-1;
            for(int i=0;i<run.Pieces.Length;i++)
            {
                if(run.Pieces[i].Placed)continue;
                float palm=view.GraspBox(i).Distance(shoulder)-EmberdeepPieceGrasp.GrabReach;
                if(palm>reach){reach=palm;farthest=i;}
            }
            Assert.GreaterOrEqual(farthest,0,"Tray pieces measured");
            Debug.Log($"EMBERDEEP_TRAY_REACH centre={centre:0.000} palm-to-farthest-piece={reach:0.000} piece={farthest}");
            Assert.Less(reach,.6f,$"Palm {reach:0.00} m from the free shoulder to grasp tray piece {farthest} (tray centre {centre:0.00} m)");
            Assert.False(game.RayAllowed,"No ray in Plan: pieces go by hand");
            // Carry a tower over a free square: a valid ghost with the rain radius.
            int square=FreeSquare(0,s=>run.Squares[s].z>4 && Mathf.Abs(run.Squares[s].x)<3);
            Assert.True(game.CarryPiece(0,game.SquareWorld(square)));yield return null;
            Assert.AreEqual(square,game.GhostSquare);Assert.True(view.GhostVisible);Assert.True(view.GhostValid);
            Assert.AreEqual(run.TowerRadius,view.RingRadius,1e-4f);
            Assert.AreEqual(view.Ghost.localPosition.x,run.Squares[square].x,.01f);
            Capture(game,"2-TrayAndGhost",46,22);
            // Over a burning square (its grass alight): red ghost, and it won't go down there.
            var patch=run.Grass.First();int burning=run.NearestSquare(patch.Position);patch.State=TreeState.Burning;patch.Heat=10;
            game.MoveCarried(game.SquareWorld(burning));yield return null;
            Assert.AreEqual(FirePlaceBlock.Burning,game.GhostBlock);Assert.False(view.GhostValid);
            Capture(game,"2b-GhostOnFire",46,10);
            Assert.False(game.DropCarried(),"Not on a burning square");Assert.False(run.Pieces[0].Placed,"Back to the tray");
            patch.State=TreeState.Safe;patch.Heat=0;
            // Down on the free square; in Plan it can be picked up again and moved.
            Assert.True(game.CarryPiece(0,game.SquareWorld(square)));Assert.True(game.DropCarried());yield return null;
            Assert.AreEqual(square,run.Pieces[0].Square);Assert.AreEqual(run.Squares[square].x,view.Piece(0).localPosition.x,.01f);
            // Regression: the set-down sound moves its own voice, never the board pieces' root.
            Assert.AreEqual(Vector3.zero,view.transform.localPosition);Assert.AreEqual(Quaternion.identity,view.transform.localRotation);
            Assert.That(Vector3.Distance(view.Tray.position,game.Arena.TransformPoint(EmberdeepPieceView.TrayCentre(hud.Board,EmberdeepPieceView.PumpSideNow))),Is.LessThan(.001f));
            int other=FreeSquare(0,s=>s!=square);
            Assert.True(game.CarryPiece(0,game.SquareWorld(other)));Assert.True(game.DropCarried());Assert.AreEqual(other,run.Pieces[0].Square);
            // Right water hand: the tray stays where it is (centred); the pump goes to the left.
            var leftHandTray=tray;
            EmberdeepWaterHand.Set(false);yield return null;yield return null;
            tray=game.Arena.InverseTransformPoint(view.Tray.position);
            Assert.AreEqual(leftHandTray.x,tray.x,.001f,"Right water hand: the tray is still centred");Assert.AreEqual(leftHandTray.z,tray.z,.001f);
            Assert.AreEqual(hud.Board.min.z+EmberdeepPieceView.TrayInset+EmberdeepPieceView.TrayDepth*.5f,tray.z,.001f,"Front at the board's front");
            Assert.Less(game.Arena.InverseTransformPoint(game.Pump.CapTop).x,0,"The pump is on the free (left) side");
            EmberdeepWaterHand.Set(true);yield return null;
        }

        [UnityTest] public IEnumerator TowerRainsOnItsOwnWhenFireComesWithinItsRadius()
        {
            yield return Load(2);
            var run=game.Run;
            int square=FreeSquare(0,s=>Mathf.Abs(run.Squares[s].x)<3 && run.Trees.Any(t=>t.State==TreeState.Burning && Vector3.Distance(t.Position,new Vector3(run.Squares[s].x,.7f,run.Squares[s].z))<run.TowerRadius*.8f));
            Assert.True(game.CarryPiece(0,game.SquareWorld(square)));Assert.True(game.DropCarried());
            yield return Wait(.3f);Assert.False(run.Pieces[0].Raining,"Time is stopped in Plan");
            yield return StartFight();
            for(int i=0;i<30 && !run.Pieces[0].Raining;i++)yield return null;
            Assert.True(run.Pieces[0].Raining);Assert.AreEqual(1,game.PieceView.TowerRains);
            yield return null; // the view draws before the sim ticks within a frame
            Assert.True(game.PieceView.Piece(0).Find("Tower rain").gameObject.activeSelf,"The shower shows");
            yield return Wait(1.2f);
            Capture(game,"3-TowerRain");
            yield return Wait(EmberdeepPieceTuning.TowerSeconds);
            Assert.False(run.Pieces[0].Raining);Assert.True(run.Pieces[0].Used);
        }

        [UnityTest] public IEnumerator ExcavatorDrawsItsLineAndDigsATrench()
        {
            yield return Load(3);
            var run=game.Run;int excavator=System.Array.FindIndex(run.Pieces,p=>p.Kind==FirePieceKind.Excavator);
            // Down the left side ring of squares, front to back (no grass there, and the seated eye sees past the outer
            // column), then one square in behind the back row.
            int start=0,end=(run.SquareRows-1)*run.SquareColumns;
            for(int s=start;s<=end;s+=run.SquareColumns)Assert.False(run.SquareBurning(s) || run.SquareTaken(s,excavator),"A free side ring");
            Assert.True(game.CarryPiece(excavator,game.SquareWorld(start)));Assert.True(game.DropCarried());
            Assert.True(game.DrawLine(excavator,end,end+1));int drawn=run.Pieces[excavator].Line.Count;Assert.AreEqual(run.SquareRows,drawn);
            yield return null;Assert.AreEqual(drawn,game.PieceView.LineMarks,"Its planned line is on the board");
            Capture(game,"4-ExcavatorLine");
            yield return StartFight();
            yield return Wait(2.7f);
            Assert.GreaterOrEqual(run.Pieces[excavator].Driven,2,"About a square a second");Assert.GreaterOrEqual(game.PieceView.TrenchPieces,3);
            Capture(game,"4b-Trench");
        }

        [UnityTest] public IEnumerator PlaneIsEarnedThenFliesTheLineAndDropsOnTheStrip()
        {
            yield return Load(4);
            var run=game.Run;var hud=game.RimHud;
            Assert.True(run.PlaneEarnable);yield return null;Assert.True(hud.PlaneAvailable);Assert.True(hud.PlaneVisible,"The rim ring shows the plane is earnable");
            yield return StartFight();
            Assert.False(game.PieceView.PlaneToken.gameObject.activeSelf,"Not yet earned");
            run.ChargePlaneForTest();yield return null;yield return null;
            Assert.True(hud.PlaneReady);Assert.True(game.PieceView.PlaneToken.gameObject.activeSelf,"The token appears in the tray");
            // A marker on a middle square; the line runs across the board (wrist yaw 90 = along +x).
            int square=1*run.SquareColumns+run.SquareColumns/2;
            Assert.True(game.CarryPiece(EmberdeepPieceView.PlaneId,game.SquareWorld(square)));game.MoveCarried(game.SquareWorld(square),90);yield return null;
            Assert.True(game.PieceView.GhostValid);
            Capture(game,"5a-PlaneMarker");
            Assert.True(game.DropCarried());Assert.AreEqual(FireRun.PlaneStage.Marked,run.PlaneState);yield return null;Assert.Zero(hud.PlaneCharge01,"The ring empties: the plane is out");
            var marker=run.Squares[square];
            var inStrip=run.Trees.Where(t=>Mathf.Abs(t.Position.z-marker.z)<=EmberdeepPieceTuning.PlaneHalfWidth && t.State==TreeState.Burning).ToArray();
            Assert.IsNotEmpty(inStrip);
            yield return Wait(EmberdeepPieceTuning.PlaneDelaySeconds+.1f);
            Assert.AreEqual(FireRun.PlaneStage.Flying,run.PlaneState);Assert.AreEqual(1,game.PieceView.PlaneRuns);
            // Capture as it crosses the middle of the board.
            for(int i=0;i<200 && run.PlaneState==FireRun.PlaneStage.Flying && run.PlaneHead<-.5f;i++)yield return null;
            Assert.True(game.PieceView.Bomber.gameObject.activeSelf);
            Capture(game,"5-PlaneRun",40);
            for(int i=0;i<400 && run.PlaneState!=FireRun.PlaneStage.None;i++)yield return null;
            Assert.True(inStrip.All(t=>t.State!=TreeState.Burning),"Everything alight under the strip is out");
        }

        // ---- Headset test 3: pinch / grab by hand, the hover highlight, the tower tip ------------------------------------
        // Synthetic tracked hands in world space, arena-aligned: the pinch's palm sits behind and above it (toward the seat).
        EmberdeepPieceGrasp.Hand PinchAt(Vector3 at,float aperture,float closure=.25f)
        {
            var a=game.Arena;var palm=at+a.up*.03f-a.forward*.08f;
            var h=new EmberdeepPieceGrasp.Hand{Valid=true,Palm=palm,Closure=closure};
            h.SetTip(0,at-a.right*aperture*.5f);h.SetTip(1,at+a.right*aperture*.5f);h.SetTip(2,palm+a.right*.015f-a.up*.025f+a.forward*.06f);
            h.PinchFromTips();return h;
        }
        // An open or closing hand beside a box, on the player's side of the tray (the palm 4.5 cm off, fingers toward it):
        // the tray is one row across the front lip (headset test 4), so its neighbours are left and right.
        EmberdeepPieceGrasp.Hand Beside(EmberdeepPieceGrasp.Candidate box,float closure,float offset=0)
        {
            var a=game.Arena;var outward=-a.forward;
            var palm=box.Center+outward*(Mathf.Max(box.Extents.x,box.Extents.z)+.045f)+a.right*offset;
            var h=new EmberdeepPieceGrasp.Hand{Valid=true,Palm=palm,Closure=closure};
            h.SetTip(0,palm+a.up*.035f-outward*.02f-a.forward*.03f);h.SetTip(1,palm-outward*.03f+a.up*.012f);h.SetTip(2,palm-outward*.032f);
            h.SetTip(3,palm-outward*.03f-a.up*.012f);h.SetTip(4,palm-outward*.026f-a.up*.024f);
            return h;
        }
        static readonly EmberdeepPieceGrasp.Hand NoHand=new EmberdeepPieceGrasp.Hand();
        // The free hand (the water hand is LEFT in these tests).
        const int Free=1;
        void Hands(EmberdeepPieceGrasp.Hand free)=>game.SetScriptPieceHands(NoHand,free);
        // A pinch or palm anchor that hangs the carried piece's base over a square.
        Vector3 Over(int square,float lift=2.2f)=>game.Arena.TransformPoint(game.Run.Squares[square]+Vector3.up*lift);
        // Seated eye turned toward the tray (yaw), for the hover captures.
        float TrayYaw(){var d=game.Arena.InverseTransformPoint(game.PieceView.Tray.position)-game.Arena.InverseTransformPoint(game.Player.ViewCamera.transform.position);return Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;}
        float TrayPitch(){var d=game.Arena.InverseTransformPoint(game.PieceView.Tray.position)-game.Arena.InverseTransformPoint(game.Player.ViewCamera.transform.position);return Mathf.Atan2(-d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg;}
        // A close-up toward a world point (fov in degrees) for the zoom shots.
        static void CaptureAt(FireRescueGame game,string name,Vector3 target,float fov)
        {
            // World up, like a level head (on the tilted board the arena's axes would roll the shot).
            var eye=game.Player.ViewCamera.transform.position;
            Capture(game,name,0,0,fov,Quaternion.LookRotation(target-eye,Vector3.up));
        }

        [UnityTest] public IEnumerator PinchATowerAtItsTopFromTheTrayAndSetItOnASquare()
        {
            yield return Load(2);
            var run=game.Run;var view=game.PieceView;Assert.AreEqual(FirePieceKind.Tower,run.Pieces[0].Kind);
            int square=FreeSquare(0,s=>run.Squares[s].z>4 && Mathf.Abs(run.Squares[s].x)<3);
            var box=game.PieceBox(0);var top=box.Center+game.Arena.up*(box.Extents.y-.008f); // the tank, under the cap
            // Thumb and index open around the tank: the hover highlight marks the tower that a pinch would take.
            Hands(PinchAt(top,.055f));yield return Wait(.15f);
            Assert.AreEqual(0,game.PieceGrasp.Hover(Free),"The open pinch hovers the tower");
            Assert.AreEqual(0,view.HoverShown(Free),"Its glow shows");Assert.AreEqual(-1,game.CarriedPiece);
            Assert.Greater(view.Piece(0).localScale.x,EmberdeepPieceView.TrayScale*1.05f,"It grows a little");
            Capture(game,"6-HoverPinch",TrayPitch(),TrayYaw());CaptureAt(game,"6a-HoverPinch-zoom",box.Center,34);
            // The tips meet: it's taken and glides into the fingers.
            Hands(PinchAt(top,.008f));yield return null;yield return null;
            Assert.AreEqual(0,game.CarriedPiece);Assert.AreEqual(Free,game.CarryHand);Assert.AreEqual(EmberdeepPieceGrasp.Mode.Pinch,game.PieceGrasp.HeldMode(Free));
            Assert.True(game.PieceBlocksWater(Free),"A carrying hand never sprays");
            Assert.AreEqual(-1,view.HoverShown(Free),"No glow on a carried piece");
            // Over the square: the ghost; tips apart: it goes down there.
            Hands(PinchAt(Over(square),.008f));yield return Wait(.35f);
            Assert.AreEqual(square,game.GhostSquare);Assert.True(view.GhostValid);
            var held=game.Arena.InverseTransformPoint(view.Piece(0).position);
            Assert.AreEqual(run.Squares[square].x,held.x,.15f,"The piece hangs from the fingers over the square");
            Capture(game,"6b-PinchCarry",46,10);
            Hands(PinchAt(Over(square),.06f));yield return null;yield return null;
            Assert.AreEqual(square,run.Pieces[0].Square,"Set down where the ghost was");Assert.AreEqual(-1,game.CarriedPiece);
            Assert.True(game.PieceBlocksWater(Free),"The opening hand holds the water a moment");
            Assert.False(EmberdeepTowerTip.Needed,"A tower set down retires the tower tip");
            yield return Wait(FireRescueGame.ReleaseSpraySeconds+.1f);
            Hands(PinchAt(game.Arena.TransformPoint(new Vector3(0,6,-4)),.06f));yield return null;
            Assert.False(game.PieceBlocksWater(Free),"Then the hand is free");
        }

        [UnityTest] public IEnumerator GrabATowerFromTheSideOfTheTrayAndSetItOnASquare()
        {
            yield return Load(2);
            var run=game.Run;var view=game.PieceView;Assert.AreEqual(FirePieceKind.Tower,run.Pieces[1].Kind);
            int square=FreeSquare(1,s=>run.Squares[s].z>4 && Mathf.Abs(run.Squares[s].x)<3);
            var box=game.PieceBox(1);
            // Open hand beside the outer tower: hovered (its neighbour isn't).
            Hands(Beside(box,.1f));yield return Wait(.15f);
            Assert.AreEqual(1,game.PieceGrasp.Hover(Free));Assert.AreEqual(1,view.HoverShown(Free));
            float off=box.Distance(Beside(box,.1f).Palm);Assert.Greater(off,.03f,"The palm is outside the tower ("+off.ToString("0.000")+" m)");
            Capture(game,"6c-HoverGrab",TrayPitch(),TrayYaw());CaptureAt(game,"6d-HoverGrab-zoom",box.Center,34);
            // Close: grabbed after the short dwell.
            Hands(Beside(box,.7f));yield return Wait(.15f);
            Assert.AreEqual(1,game.CarriedPiece);Assert.AreEqual(EmberdeepPieceGrasp.Mode.Grab,game.PieceGrasp.HeldMode(Free));
            Assert.True(game.PieceBlocksWater(Free));
            var fist=Beside(box,.7f);fist.Palm=Over(square,1.8f);
            Hands(fist);yield return Wait(.35f);
            Assert.AreEqual(square,game.GhostSquare);Assert.True(view.GhostValid);
            fist.Closure=.1f;Hands(fist);yield return null;yield return null;
            Assert.AreEqual(square,run.Pieces[1].Square,"Open: set down");
            // In Plan it comes back up the same way (a pinch on the placed tower's top).
            yield return Wait(.1f);
            var placed=game.PieceBox(1);var cap=placed.Center+game.Arena.up*(placed.Extents.y-.01f);
            Hands(PinchAt(cap,.055f));yield return Wait(.1f);Assert.AreEqual(1,game.PieceGrasp.Hover(Free),"A placed tower hovers in Plan");
            Hands(PinchAt(cap,.008f));yield return null;yield return null;
            Assert.AreEqual(1,game.CarriedPiece);Assert.False(run.Pieces[1].Placed,"Lifted off the board");
            Hands(PinchAt(cap,.06f));yield return null;
        }

        [UnityTest] public IEnumerator AFingertipOnAPlacedExcavatorDrawsAndAPinchLiftsIt()
        {
            yield return Load(3);
            var run=game.Run;int excavator=System.Array.FindIndex(run.Pieces,p=>p.Kind==FirePieceKind.Excavator);
            int start=0;Assert.True(game.CarryPiece(excavator,game.SquareWorld(start)));Assert.True(game.DropCarried());yield return null;
            var box=game.PieceBox(excavator);var a=game.Arena;
            // A point: index tip on the excavator, the rest curled, palm and thumb well back. It draws, it doesn't grab.
            var palm=box.Center-a.forward*.1f+a.up*.03f;
            var point=new EmberdeepPieceGrasp.Hand{Valid=true,Palm=palm,Closure=.6f};
            point.SetTip(1,box.Center-a.forward*(box.Extents.z-.004f));point.SetTip(2,palm+a.forward*.02f-a.up*.02f);point.SetTip(0,palm+a.forward*.01f+a.right*.025f-a.up*.01f); // thumb tucked by the palm
            Assert.True(point.Pointing);
            Hands(point);yield return null;yield return null;
            Assert.AreEqual(excavator,game.DrawingPiece,"A fingertip touch starts the line");Assert.AreEqual(-1,game.CarriedPiece);
            Hands(NoHand);yield return Wait(.5f);Assert.AreEqual(-1,game.DrawingPiece);
            // A pinch on its cab lifts it instead (Plan).
            box=game.PieceBox(excavator);var cab=box.Center+a.up*(box.Extents.y*.6f);
            Hands(PinchAt(cab,.055f));yield return Wait(.1f);Assert.AreEqual(excavator,game.PieceGrasp.Hover(Free));
            Hands(PinchAt(cab,.008f));yield return null;yield return null;
            Assert.AreEqual(excavator,game.CarriedPiece,"Pinched up");Assert.AreEqual(-1,game.DrawingPiece);
            Hands(PinchAt(cab,.06f));yield return null;
        }

        [UnityTest] public IEnumerator TowerTipFloatsOverTheTrayFacingTheSeatUntilATowerIsSetDown()
        {
            yield return Load(2,tip:true);
            var run=game.Run;var tip=game.PieceView.Tip;Assert.True(EmberdeepTowerTip.Needed);
            // The PLAN card comes first (headset test 4); the tip waits until it has shrunk to its tag.
            yield return Wait(.4f);Assert.True(game.PlanCardBig);Assert.False(tip.Visible,"The tip waits for the PLAN card");
            yield return Wait(EmberdeepPlanBanner.BigSeconds+EmberdeepPlanBanner.ShrinkSeconds);
            Assert.True(game.TowerTipWanted);Assert.True(tip.Visible,"Plan, towers in the tray, never placed: the tip shows");
            Assert.AreEqual(EmberdeepTowerTip.Title,tip.TitleText.text);
            var numbers=new List<string>();
            yield return CheckTip("flat",numbers);
            Capture(game,"7-TowerTip",TrayPitch()-6,TrayYaw()*.6f);CaptureAt(game,"7a-TowerTip-zoom",tip.Center,30);
            // Tilted board: the tray tips with the board, the tip stays upright, turned to the eye, off the bell.
            EmberdeepPlayArea.SetTilted(true);yield return Wait(.9f);
            Assert.True(tip.Visible);yield return CheckTip("tilted",numbers);
            Capture(game,"7b-TowerTip-tilted",TrayPitch()-6,TrayYaw()*.6f);CaptureAt(game,"7c-TowerTip-tilted-zoom",tip.Center,30);
            EmberdeepPlayArea.SetTilted(false);yield return Wait(.4f);
            // Right water hand: the tray (and the tip) stay on the right, with the bell shifted right too; still off it.
            EmberdeepWaterHand.Set(false);yield return Wait(.9f);yield return CheckTip("right water hand",numbers);
            EmberdeepWaterHand.Set(true);yield return Wait(.3f);
            // zh-Hans: the words are translated, every glyph in the font.
            yield return Localize("zh-Hans");yield return Wait(.2f);
            foreach(var t in tip.Lines.Append(tip.TitleText))
            {
                Assert.False(Regex.IsMatch(t.text,"[A-Za-z]{3,}"),"English left in zh-Hans: "+t.text);
                foreach(char ch in t.text)if(ch>='一' && ch<='鿿')Assert.True(t.font.HasCharacter(ch),"Missing glyph "+ch);
            }
            numbers.Add("zh-Hans: "+string.Join(" / ",tip.TitleText.text,tip.Lines[0].text,tip.Lines[1].text));
            yield return CheckTip("zh-Hans",numbers);
            CaptureAt(game,"7d-TowerTip-zh-zoom",tip.Center,30);
            yield return Localize("en");
            // A tower set down: gone for good.
            int square=FreeSquare(0);Assert.True(game.CarryPiece(0,game.SquareWorld(square)));Assert.True(game.DropCarried());yield return null;yield return null;
            Assert.False(EmberdeepTowerTip.Needed);Assert.False(tip.Visible,"Gone once a tower is placed");
            // SHOW HINTS brings it back (tower 1 is still in the tray).
            EmberdeepFirstHints.ResetAll();yield return null;yield return null;
            Assert.True(EmberdeepTowerTip.Needed);Assert.True(tip.Visible,"Back after SHOW HINTS");
            // The bell struck (counting down to the fight): no text from here on.
            Assert.True(game.StrikeBell());yield return null;yield return null;Assert.False(tip.Visible,"Not once the countdown runs");
            Directory.CreateDirectory(Output);File.WriteAllLines(Output+"/tower-tip.txt",numbers);
        }
        // Headset test 4: PLAN at each level's start (a card over the tray), a small tag on the tray's front edge after ~3 s,
        // gone with the bell. Flat and tilted; en and zh-Hans; clear of the bell and the readout strip from the seat.
        [UnityTest] public IEnumerator PlanBannerAtTheLevelStartShrinksToATagAndGoesWithTheBell()
        {
            yield return Load(2,tip:true);
            var banner=game.PlanBanner;Assert.NotNull(banner,"Made with the board pieces");
            yield return Wait(.5f);
            Assert.True(banner.Visible,"Shown in Plan");Assert.True(banner.Big);Assert.True(banner.LineShown);
            Assert.AreEqual(EmberdeepPlanBanner.Title,banner.TitleText.text);Assert.AreEqual(EmberdeepPlanBanner.Line,banner.LineText.text);
            Assert.False(game.PieceView.Tip.Visible,"The tower tip waits for the card");
            var numbers=new List<string>();
            yield return CheckPlanBanner("big flat",numbers);
            Capture(game,"8-PlanBanner",TrayPitch()-12,0);
            EmberdeepPlayArea.SetTilted(true);yield return Wait(.3f);
            yield return CheckPlanBanner("big tilted",numbers);
            Capture(game,"8a-PlanBanner-tilted",TrayPitch()-12,0);
            // An unfocused editor sends OnApplicationFocus(false), which holds the game (and the card's clock) still.
            for(float until=Time.realtimeSinceStartup+EmberdeepPlanBanner.BigSeconds+EmberdeepPlanBanner.ShrinkSeconds;Time.realtimeSinceStartup<until;){game.SendMessage("OnApplicationFocus",true);yield return null;}
            Assert.True(banner.Small);Assert.False(banner.LineShown,"The tag is the word only");
            Assert.AreEqual(EmberdeepPlanBanner.SmallTitle/EmberdeepPlanBanner.TitleHeight*banner.Size.y,banner.ShownSize.y,.002f,"Shrunk");
            var front=game.PieceView.transform.TransformPoint(game.PieceView.Tray.localPosition+Vector3.back*EmberdeepPieceView.TrayDepth*.5f);
            var c=new Vector3[4];banner.Corners(c);
            Assert.AreEqual(front.y+EmberdeepPlanBanner.SmallLift,Mathf.Min(c[0].y,c[3].y),.004f,"Its foot on the tray's front edge");
            Assert.Less(Vector3.Distance(new Vector3(front.x,0,front.z),new Vector3(banner.Center.x,0,banner.Center.z)),.03f);
            Assert.True(game.PieceView.Tip.Visible,"The tip shows once the card has shrunk");
            numbers.Add($"small tag tilted: {banner.ShownSize.x:0.000} x {banner.ShownSize.y:0.000} m");
            Capture(game,"8b-PlanTag-tilted",TrayPitch()-6,0);CaptureAt(game,"8c-PlanTag-zoom",banner.Center,20);
            EmberdeepPlayArea.SetTilted(false);yield return Wait(.2f);
            // zh-Hans: translated, every glyph in the font.
            yield return Localize("zh-Hans");yield return Wait(.2f);
            Assert.False(Regex.IsMatch(banner.TitleText.text,"[A-Za-z]{3,}"),"English left in zh-Hans: "+banner.TitleText.text);
            foreach(char ch in banner.TitleText.text+banner.LineText.text)if(ch>='一' && ch<='鿿')Assert.True(banner.TitleText.font.HasCharacter(ch),"Missing glyph "+ch);
            numbers.Add("zh-Hans: "+banner.TitleText.text+" / "+banner.LineText.text);
            yield return Localize("en");
            // The bell struck (counting down to the fight): gone.
            Assert.True(game.StrikeBell());yield return null;yield return null;Assert.False(banner.Visible,"Not once the countdown runs");
            Directory.CreateDirectory(Output);File.WriteAllLines(Output+"/plan-banner.txt",numbers);
        }
        IEnumerator CheckPlanBanner(string label,List<string> numbers)
        {
            yield return null;yield return null;
            var banner=game.PlanBanner;var eye=game.Player.ViewCamera.transform.position;
            Assert.Less(Vector3.Angle(banner.TitleText.transform.forward,banner.Center-eye),1f,label+": turned to the eye");
            Assert.Less(Mathf.Abs(Vector3.Dot(banner.TitleText.transform.right,Vector3.up)),.01f,label+": upright (no roll)");
            var corners=new Vector3[4];banner.Corners(corners);
            var forward=Vector3.ProjectOnPlane(game.Arena.forward,Vector3.up).normalized;var right=Vector3.Cross(Vector3.up,forward);
            Vector2 Angles(Vector3 p){var d=p-eye;float f=Vector3.Dot(d,forward),r=Vector3.Dot(d,right),u=Vector3.Dot(d,Vector3.up);
                return new Vector2(Mathf.Atan2(r,f),Mathf.Atan2(u,new Vector2(f,r).magnitude))*Mathf.Rad2Deg;}
            Rect Box(IEnumerable<Vector3> points){var a=points.Select(Angles).ToArray();float x0=a.Min(v=>v.x),x1=a.Max(v=>v.x),y0=a.Min(v=>v.y),y1=a.Max(v=>v.y);return Rect.MinMaxRect(x0,y0,x1,y1);}
            Rect RendererBox(Transform root){var pts=new List<Vector3>();foreach(var r in root.GetComponentsInChildren<Renderer>())if(r.enabled && r.gameObject.activeInHierarchy){var b=r.bounds;for(int i=0;i<8;i++)pts.Add(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));}return pts.Count>0?Box(pts):Rect.zero;}
            var card=Box(corners);
            Assert.Greater(corners.Min(p=>p.y),game.PieceView.Tray.position.y+.1f,label+": over the towers in the tray");
            var hud=game.RimHud;
            if(hud && hud.Bell && hud.Bell.gameObject.activeInHierarchy){var bell=RendererBox(hud.Bell);Assert.False(card.Overlaps(bell),label+": clear of the bell "+bell+" card "+card);}
            if(hud && hud.Strip && hud.Strip.gameObject.activeInHierarchy){var strip=RendererBox(hud.Strip);Assert.False(card.Overlaps(strip),label+": clear of the readout strip "+strip+" card "+card);}
            float distance=Vector3.Distance(eye,banner.Center);
            numbers.Add($"{label}: card {banner.ShownSize.x:0.000} x {banner.ShownSize.y:0.000} m at {distance:0.000} m, {-card.yMax:0.0}..{-card.yMin:0.0} deg down, {card.xMin:0.0}..{card.xMax:0.0} deg across");
        }

        // Faces the eye, hangs over the tower, readable, and its view box clear of the bell's (angles from the seat).
        IEnumerator CheckTip(string label,List<string> numbers)
        {
            yield return null;
            var tip=game.PieceView.Tip;var eye=game.Player.ViewCamera.transform.position;
            Assert.Less(Vector3.Angle(tip.Facing,tip.Center-eye),1f,label+": turned to the eye");
            var corners=new Vector3[4];tip.Corners(corners);
            var anchor=game.PieceView.TipAnchor(game.TowerTipPiece);
            Assert.Greater(corners.Min(c=>c.y),anchor.y,label+": above the tower");
            float distance=Vector3.Distance(eye,tip.Center);
            // Real world sizes (the tip hangs under the scaled arena): the panel as laid out, the line as rendered.
            Assert.AreEqual(tip.Size.x,Vector3.Distance(corners[1],corners[2]),.003f,label+": panel width in metres");
            var line=tip.Lines[0];float lineHeight=line.GetComponent<Renderer>().localBounds.size.y*line.transform.lossyScale.y;
            Assert.AreEqual(EmberdeepTowerTip.LineHeight,lineHeight,.002f,label+": prompt line height in metres");
            float letters=Mathf.Atan2(lineHeight,distance)*Mathf.Rad2Deg;
            Assert.Greater(letters,.8f,label+": prompt line "+letters.ToString("0.00")+" deg tall");
            Assert.Less(Mathf.Abs(Vector3.Dot(tip.TitleText.transform.right,Vector3.up)),.01f,label+": upright (no roll)");
            // Angular boxes from the seat, in the arena's heading.
            var forward=Vector3.ProjectOnPlane(game.Arena.forward,Vector3.up).normalized;var right=Vector3.Cross(Vector3.up,forward);
            Vector2 Angles(Vector3 p){var d=p-eye;float f=Vector3.Dot(d,forward),r=Vector3.Dot(d,right),u=Vector3.Dot(d,Vector3.up);
                return new Vector2(Mathf.Atan2(r,f),Mathf.Atan2(u,new Vector2(f,r).magnitude))*Mathf.Rad2Deg;}
            Rect Box(IEnumerable<Vector3> points){var a=points.Select(Angles).ToArray();float x0=a.Min(v=>v.x),x1=a.Max(v=>v.x),y0=a.Min(v=>v.y),y1=a.Max(v=>v.y);return Rect.MinMaxRect(x0,y0,x1,y1);}
            var tipBox=Box(corners);
            // Nothing nearer the eye stands in front of it: the pump (beside the tray) never covers the words.
            var pump=game.Pump;Assert.NotNull(pump);
            float tipRange=Vector3.Distance(eye,tip.Center);
            foreach(var r in pump.GetComponentsInChildren<Renderer>().Where(r=>r.enabled && r.gameObject.activeInHierarchy))
            {
                var b=r.bounds;var points=Enumerable.Range(0,8).Select(i=>b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))).ToArray();
                if(points.Min(q=>Vector3.Distance(eye,q))>=tipRange)continue;
                var part=Box(points);
                Assert.False(part.Overlaps(tipBox),$"{label}: the pump's {r.name} ({part}) covers the tip ({tipBox})");
            }
            var hud=game.RimHud;Assert.True(hud && hud.BellVisible,"The bell hangs in Plan");
            var bellPoints=hud.Bell.GetComponentsInChildren<Renderer>().Where(r=>r.enabled && r.gameObject.activeInHierarchy).SelectMany(r=>{var b=r.bounds;
                return Enumerable.Range(0,8).Select(i=>b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));});
            var bellBox=Box(bellPoints);
            Assert.False(tipBox.Overlaps(bellBox),$"{label}: the tip ({tipBox}) covers the bell ({bellBox})");
            float gap=Mathf.Max(bellBox.xMin-tipBox.xMax,tipBox.xMin-bellBox.xMax,bellBox.yMin-tipBox.yMax,tipBox.yMin-bellBox.yMax);
            numbers.Add($"{label}: tip {tip.Size.x*100:0.0}x{tip.Size.y*100:0.0} cm at {distance:0.00} m, raised {tip.Clearance*100:0.0} cm over the pump, prompt letters {letters:0.00} deg; view box yaw {tipBox.xMin:0.0}..{tipBox.xMax:0.0} pitch {tipBox.yMin:0.0}..{tipBox.yMax:0.0}; bell yaw {bellBox.xMin:0.0}..{bellBox.xMax:0.0} pitch {bellBox.yMin:0.0}..{bellBox.yMax:0.0}; clear by {gap:0.0} deg");
        }
        IEnumerator Localize(string code)
        {
            yield return LocalizationSettings.InitializationOperation;
            LocalizationSettings.SelectedLocale=LocalizationSettings.AvailableLocales.GetLocale(code);
            var adapter=game.GetComponent<EmberdeepLocalizedText>();float until=Time.realtimeSinceStartup+10;
            while((!adapter.Ready || adapter.LocaleCode!=code) && Time.realtimeSinceStartup<until)yield return null;
            Assert.True(adapter.Ready);for(int i=0;i<3;i++)yield return null;adapter.RefreshLabels();yield return null;yield return null;
        }
    }
}

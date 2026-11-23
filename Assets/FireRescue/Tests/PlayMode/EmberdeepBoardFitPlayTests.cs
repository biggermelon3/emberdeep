using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    // LoopSpec §5 Reach (workstream O): the board as the seated eye sees it. Measures the reach numbers on the real
    // fireground (desktop seat = FireMiniature placement from the eye) and writes seated captures plus the numbers to
    // Logs/EmberdeepBoardFit/<Label>/.
    public sealed class EmberdeepBoardFitPlayTests
    {
        const string Folder="Logs/EmberdeepBoardFit";
        const string Label="after";
        static readonly int[] Stages={5,3}; // incidents 4 and 2 (stage cut: was 7, 5)

        public struct Reach
        {
            public float FrontRow,BackRow,Drop,MidAngle,RimAngle,EdgeAngle,EdgeDistance,BackEdge;
            public Vector3 Pump,Pasture;
        }
        // Horizontal distances along the board's forward axis, from the eye; angles in degrees below horizontal.
        public static Reach Measure(FireRescueGame game)
        {
            var eye=game.Player.ViewCamera.transform.position;var arena=game.Arena;var run=game.Run;
            float front=float.PositiveInfinity,back=float.NegativeInfinity;
            foreach(var t in run.Trees){front=Mathf.Min(front,t.Position.z);back=Mathf.Max(back,t.Position.z);}
            float Ahead(Vector3 world)=>Vector3.Dot(world-eye,arena.forward);
            float Below(Vector3 world)=>eye.y-world.y;
            float Angle(Vector3 world)=>Mathf.Atan2(Below(world),Ahead(world))*Mathf.Rad2Deg;
            var board=game.RimHud?game.RimHud.Board:EmberdeepRimHud.DefaultBoard;
            var edge=arena.TransformPoint(new Vector3(0,board.max.y,board.min.z));
            var r=new Reach{
                FrontRow=Ahead(arena.TransformPoint(new Vector3(0,0,front))),
                BackRow=Ahead(arena.TransformPoint(new Vector3(0,0,back))),
                Drop=Below(arena.position),
                MidAngle=Angle(arena.TransformPoint(new Vector3(0,0,(front+back)*.5f))),
                RimAngle=game.RimHud && game.RimHud.Strip?Angle(game.RimHud.Strip.position):float.NaN,
                EdgeAngle=Angle(edge),EdgeDistance=Ahead(edge),
                BackEdge=Ahead(arena.TransformPoint(new Vector3(0,0,board.max.z)))};
            if(game.Pump){var cap=game.Pump.CapTop;r.Pump=new Vector3(Vector3.Dot(cap-eye,arena.right),-Below(cap),Ahead(cap));}
            if(run.Animals.Length>0){var p=arena.TransformPoint(run.Animals[0].Pasture);r.Pasture=new Vector3(Vector3.Dot(p-eye,arena.right),-Below(p),Ahead(p));}
            return r;
        }
        static string Describe(FireRun run,Reach r)
        {
            var c=CultureInfo.InvariantCulture;
            return string.Format(c,"stage {0} ({1}): trees {2}, front row {3:F3} m, back row {4:F3} m, board top {5:F3} m below the eye, "+
                "mid-board {6:F1} deg down, rim strip {7:F1} deg down, plinth front edge {8:F3} m at {9:F1} deg, plinth back edge {10:F3} m, "+
                "pump cap (right {11:F3}, up {12:F3}, ahead {13:F3}) m, first pasture (right {14:F3}, up {15:F3}, ahead {16:F3}) m",
                run.Stage,run.StageName,run.Trees.Length,r.FrontRow,r.BackRow,r.Drop,r.MidAngle,r.RimAngle,r.EdgeDistance,r.EdgeAngle,r.BackEdge,
                r.Pump.x,r.Pump.y,r.Pump.z,r.Pasture.x,r.Pasture.y,r.Pasture.z);
        }
        // Headset-like vertical field of view, head pitched to the middle of the board.
        static void Capture(FireRescueGame game,string path,float pitch,float fov=92)
        {
            var camera=game.Player.ViewCamera;
            var rotation=camera.transform.rotation;float priorFov=camera.fieldOfView;var prior=camera.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(1200,1100,24,RenderTextureFormat.ARGB32){antiAliasing=4};var texture=new Texture2D(1200,1100,TextureFormat.RGBA32,false);
            try
            {
                camera.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(game.Arena.forward,Vector3.up),Vector3.up)*Quaternion.Euler(pitch,0,0); // flat heading: a tilted arena pitches its forwardcamera.fieldOfView=fov;
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1200,1100),0,0);texture.Apply();
                File.WriteAllBytes(path,texture.EncodeToPNG());
            }
            finally{camera.transform.rotation=rotation;camera.fieldOfView=priorFov;camera.targetTexture=prior;RenderTexture.active=active;target.Release();Object.Destroy(target);Object.Destroy(texture);}
        }
        // Straight down from above the board's middle: the footprint of the island, field, pump and rim UI.
        static void CaptureFromAbove(FireRescueGame game,string path)
        {
            var camera=game.Player.ViewCamera;var t=camera.transform;var position=t.position;var rotation=t.rotation;
            try
            {
                t.SetPositionAndRotation(game.Arena.TransformPoint(new Vector3(0,22,5.2f)),Quaternion.LookRotation(-game.Arena.up,game.Arena.forward));
                Capture(game,path,90,44);
            }
            finally{t.SetPositionAndRotation(position,rotation);}
        }
        static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}

        [UnityTest] public IEnumerator SeatedViewOfTheBoardIsMeasuredAndCaptured()
        {
            var folder=Folder+"/"+Label;Directory.CreateDirectory(folder);var log=new StringBuilder();
            var saved=EmberdeepSettingsPrefs.Save();
            // The default placement, whatever the editor's saved options are.
            PlayerPrefs.DeleteKey(EmberdeepPlayArea.HeightKey);PlayerPrefs.DeleteKey(EmberdeepPlayArea.DistanceKey);PlayerPrefs.DeleteKey(EmberdeepPlayArea.TiltedKey);EmberdeepPlayArea.Reload();
            EmberdeepPlayArea.SetTilted(false); // Measure() reads along the arena's own axes: the flat board (tilted by default since headset test 4)
            try{
            foreach(int stage in Stages)
            {
                EmberdeepSession.NewExpedition();var run=EmberdeepSession.Run;
                typeof(FireRun).GetProperty("Stage").SetValue(run,stage);
                Assert.IsTrue(run.StartStage(true));
                yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);yield return null;yield return null;
                var game=Object.FindFirstObjectByType<FireRescueGame>();Assert.NotNull(game);Assert.AreSame(run,game.Run);
                game.SendMessage("OnApplicationFocus",true);
                yield return Wait(1.2f); // the rim re-measures the plinth every second
                var reach=Measure(game);var line=Describe(run,reach);log.AppendLine(line);Debug.Log("EMBERDEEP_BOARD_FIT "+Label+" "+line);
                Capture(game,folder+"/seated-stage"+stage+".png",reach.MidAngle);
                Capture(game,folder+"/seated-stage"+stage+"-rim.png",reach.MidAngle+12);
                CaptureFromAbove(game,folder+"/above-stage"+stage+".png");
                // Reach targets since headset test 4 (2026-11-22, default options -14 / +12 cm, measured flat): front row
                // ~52 cm, back row ~80 cm, board top ~50 cm below the eye, ~37 deg to the middle of 4 rows (~39 for 3).
                Assert.AreEqual(.52f,reach.FrontRow,.02f,line);Assert.AreEqual(.50f,reach.Drop,.005f,line);
                if(stage==5)Assert.AreEqual(.796f,reach.BackRow,.015f,line);else Assert.LessOrEqual(reach.BackRow,.81f,line);
                Assert.That(reach.MidAngle,Is.InRange(35f,41f),line);
                Assert.Greater(reach.EdgeDistance,.2f,"the plinth's front tip stays off the chest: "+line);
                Assert.Less(reach.BackEdge,1.16f,line); // +6 cm since headset test 4
                // Headset test 3 (2026-11-22): at the default seat the pump cap is PumpCapBelowEye (~30 cm) under the eye and
                // PumpAhead (~27.5 cm) ahead of it (was ~42 / ~32 cm).
                Assert.AreEqual(EmberdeepBoardLayout.PumpCapBelowEye,-reach.Pump.y,.005f,"pump cap at chest height: "+line);
                Assert.AreEqual(EmberdeepBoardLayout.PumpAhead,reach.Pump.z,.005f,"pump cap within a bent arm: "+line);
                Assert.That(Mathf.Abs(reach.Pump.x),Is.InRange(.3f,.45f),"pump beside the board on the water-hand side: "+line);
                Assert.Less(reach.Pasture.z,reach.FrontRow,"rescued sheep graze in front of the grove, within reach: "+line);
            }
            }
            finally{saved.Restore();}
            File.WriteAllText(folder+"/reach.txt",log.ToString());
        }

        // The authored terrain is squeezed to the compact board; the rim UI measures it; the options move it live.
        [UnityTest] public IEnumerator CompactTerrainFitsAndThePlayAreaOptionsApplyLive()
        {
            var saved=EmberdeepSettingsPrefs.Save();
            try
            {
                PlayerPrefs.DeleteKey(EmberdeepPlayArea.HeightKey);PlayerPrefs.DeleteKey(EmberdeepPlayArea.DistanceKey);PlayerPrefs.DeleteKey(EmberdeepPlayArea.TiltedKey);EmberdeepPlayArea.Reload();
                EmberdeepPlayArea.SetTilted(false); // Measure() reads along the arena's own axes: the flat board
                EmberdeepSession.NewExpedition();var run=EmberdeepSession.Run;typeof(FireRun).GetProperty("Stage").SetValue(run,4);Assert.IsTrue(run.StartStage(true)); // incident 3 (was stage 6)
                yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);yield return null;yield return null;
                var game=Object.FindFirstObjectByType<FireRescueGame>();game.SendMessage("OnApplicationFocus",true);
                yield return Wait(1.2f);
                Assert.NotNull(game.BoardFit);Assert.AreEqual(0,game.BoardFit.SkippedMeshes);Assert.AreEqual(10,game.BoardFit.FittedMeshes);
                var board=game.RimHud.Board;
                Assert.AreEqual(EmberdeepBoardLayout.CompactFront,board.min.z,.05f);Assert.AreEqual(EmberdeepBoardLayout.CompactBack,board.max.z,.05f);
                Assert.AreEqual(EmberdeepRimHud.DefaultBoard.size.x,board.size.x,.05f,"width unchanged");Assert.AreEqual(0,board.max.y,.05f);
                var creek=game.Arena.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name=="CreekSurface");
                var creekBounds=creek.sharedMesh.bounds;
                Assert.AreEqual(EmberdeepBoardLayout.Z(1.62f),creekBounds.min.z,.02f);Assert.AreEqual(EmberdeepBoardLayout.Z(2.58f),creekBounds.max.z,.02f);
                Assert.True(creek.sharedMesh.vertices.All(v=>v.z<3),"the creek stays in front of the trees");
                // The vane stands behind the deepest sheep home on the board.
                Assert.Less(game.Weathervane.transform.localPosition.z,EmberdeepBoardLayout.CompactBack-.3f);

                // Live: + height raises, + distance pushes away, from the same anchor; the board stays flat.
                var before=Measure(game);var position=game.Arena.position;var rotation=game.Arena.rotation;
                EmberdeepPlayArea.SetHeight(EmberdeepPlayArea.DefaultHeight+.08f);EmberdeepPlayArea.SetDistance(EmberdeepPlayArea.DefaultDistance+.06f);yield return null;
                var after=Measure(game);
                Assert.AreEqual(before.Drop-.08f,after.Drop,.002f);Assert.AreEqual(before.FrontRow+.06f,after.FrontRow,.002f);
                Assert.Less(Quaternion.Angle(rotation,game.Arena.rotation),.01f);
                // The pump follows the distance 1:1 and half the height (EmberdeepBoardLayout.PumpHeightFollow).
                Assert.AreEqual(.08f*EmberdeepBoardLayout.PumpHeightFollow,after.Pump.y-before.Pump.y,.002f,"the pump follows half the height");
                Assert.AreEqual(.06f,after.Pump.z-before.Pump.z,.002f,"the pump follows the distance");
                EmberdeepPlayArea.ResetToDefault();EmberdeepPlayArea.SetTilted(false);yield return null; // the reset tilts it (the default since headset test 4)
                Assert.Less(Vector3.Distance(position,game.Arena.position),.001f,"back to the default");
            }
            finally{saved.Restore();}
        }

        // Tilted (headset test 2 option): live from Changed, the arena pitches about the front row; the front row stays,
        // the back rises, the rim strip follows the raised back, the desktop seat re-aims, the upright frame stays flat.
        [UnityTest] public IEnumerator TiltedPlayAreaRePlacesTheBoardLive()
        {
            var saved=EmberdeepSettingsPrefs.Save();
            try
            {
                PlayerPrefs.DeleteKey(EmberdeepPlayArea.HeightKey);PlayerPrefs.DeleteKey(EmberdeepPlayArea.DistanceKey);PlayerPrefs.DeleteKey(EmberdeepPlayArea.TiltedKey);EmberdeepPlayArea.Reload();
                EmberdeepPlayArea.SetTilted(false); // tilted by default since headset test 4: start flat, then tilt
                EmberdeepSession.NewExpedition();var run=EmberdeepSession.Run;typeof(FireRun).GetProperty("Stage").SetValue(run,5);Assert.IsTrue(run.StartStage(true)); // incident 4: 4 rows
                yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);yield return null;yield return null;
                var game=Object.FindFirstObjectByType<FireRescueGame>();game.SendMessage("OnApplicationFocus",true);
                yield return Wait(1.2f);
                var arena=game.Arena;var cam=game.Player.ViewCamera.transform;
                float front=float.PositiveInfinity,back=float.NegativeInfinity;
                foreach(var t in game.Run.Trees){front=Mathf.Min(front,t.Position.z);back=Mathf.Max(back,t.Position.z);}
                Assert.AreEqual(EmberdeepBoardLayout.FrontRowZ,front,.01f);Assert.Greater(back-front,1f,"at least 2 rows");
                var frontLocal=new Vector3(0,0,front);var backLocal=new Vector3(0,0,back);
                var frontFlat=arena.TransformPoint(frontLocal);var backFlat=arena.TransformPoint(backLocal);var flatMatrix=arena.localToWorldMatrix;
                Assert.AreEqual(0,game.BoardTilt);Assert.AreEqual(FireRescueGame.DesktopPitchFor(EmberdeepPlayArea.DefaultHeight,EmberdeepPlayArea.DefaultDistance,0),cam.eulerAngles.x,.05f,"desktop seat on the field middle");
                var strip=game.RimHud && game.RimHud.Strip?game.RimHud.Strip.position:Vector3.zero;
                Assert.NotNull(game.Pump);var capFlat=game.Pump.CapTop;
                Directory.CreateDirectory(Folder+"/"+Label);
                Capture(game,Folder+"/"+Label+"/seated-flat.png",cam.eulerAngles.x);

                EmberdeepPlayArea.SetTilted(true);yield return null;yield return null;
                Assert.AreEqual(EmberdeepBoardLayout.TiltDegrees,game.BoardTilt,1e-4f);
                Assert.AreEqual(EmberdeepBoardLayout.TiltDegrees,Vector3.Angle(arena.up,Vector3.up),.01f,"pitched");
                Assert.Less(Vector3.Distance(frontFlat,arena.TransformPoint(frontLocal)),.001f,"the front row stays put");
                float depth=(back-front)*FireMiniature.Scale;
                Assert.AreEqual(depth*Mathf.Sin(EmberdeepBoardLayout.TiltDegrees*Mathf.Deg2Rad),arena.TransformPoint(backLocal).y-backFlat.y,.002f,"the back rises");
                for(int i=0;i<16;i++)Assert.AreEqual(flatMatrix[i],game.UprightToWorld[i],1e-4f,"upright frame = the flat board");
                Assert.AreEqual(FireRescueGame.DesktopPitchCompact,cam.eulerAngles.x,.05f,"desktop seat re-aimed");
                // The pump is struck by a real fist: it stays upright in the world, its cap exactly where the flat board has it.
                Assert.Less(Vector3.Distance(capFlat,game.Pump.CapTop),.001f,"pump cap unmoved by the tilt");
                Assert.Less(Vector3.Angle(game.Pump.Up,Vector3.up),.01f,"pump upright in the world");
                Assert.Less(cam.eulerAngles.x,FireRescueGame.DesktopPitchFor(EmberdeepPlayArea.DefaultHeight,EmberdeepPlayArea.DefaultDistance,0)-2,"tilted looks down less");
                if(game.RimHud && game.RimHud.Strip && game.RimHud.Strip.gameObject.activeInHierarchy){yield return null;Assert.Greater(game.RimHud.Strip.position.y,strip.y+.1f,"the strip rides the raised back edge");}
                Capture(game,Folder+"/"+Label+"/seated-tilted.png",cam.eulerAngles.x);

                EmberdeepPlayArea.SetTilted(false);yield return null;
                Assert.Less(Vector3.Distance(backFlat,arena.TransformPoint(backLocal)),.001f,"flat again");
                Assert.AreEqual(0,Vector3.Angle(arena.up,Vector3.up),.01f);
            }
            finally{saved.Restore();}
        }
    }
}

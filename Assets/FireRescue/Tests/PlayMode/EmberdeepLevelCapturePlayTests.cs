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
    // LoopSpec §8 (workstream LV): every hand-authored level laid out on the real fireground in its Plan look (time
    // stopped, whitish grade, forecast rings), seen from the seated desktop eye, plus a straight-down view. Writes
    // Logs/EmberdeepLevels/stage<N>-plan.png, stage<N>-above.png and levels.txt (what each board holds).
    public sealed class EmberdeepLevelCapturePlayTests
    {
        const string Folder="Logs/EmberdeepLevels";
        static readonly EmberdeepFirstHints.Move[] Moves=(EmberdeepFirstHints.Move[])System.Enum.GetValues(typeof(EmberdeepFirstHints.Move));
        int[] hints;

        [UnityTest] public IEnumerator EveryLevelInItsPlanLookFromTheSeat()
        {
            Directory.CreateDirectory(Folder);var log=new StringBuilder();
            // Retire the first-time ghost hands so they stay out of the captures (restored in TearDown).
            hints=Moves.Select(m=>PlayerPrefs.GetInt(EmberdeepFirstHints.Key(m),-1)).ToArray();
            foreach(var m in Moves)PlayerPrefs.SetInt(EmberdeepFirstHints.Key(m),99);
            for(int stage=0;stage<EmberdeepLevels.Count;stage++)
            {
                EmberdeepSession.NewExpedition();var run=EmberdeepSession.Run;
                typeof(FireRun).GetProperty("Stage").SetValue(run,stage);Assert.IsTrue(run.StartStage(true));
                yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);yield return null;yield return null;
                var game=Object.FindFirstObjectByType<FireRescueGame>();Assert.NotNull(game);Assert.AreSame(run,game.Run);
                game.SendMessage("OnApplicationFocus",true);
                yield return Wait(1.4f); // the Plan look warms the fire, then freezes it; the rim re-measures each second
                Assert.AreEqual(FirePhase.Preparing,run.Phase,"stage "+stage+": still in Plan");
                Assert.AreSame(EmberdeepLevels.For(stage),run.Level);
                var reach=EmberdeepBoardFitPlayTests.Measure(game);
                Capture(game,$"{Folder}/stage{stage}-plan.png",reach.MidAngle);
                CaptureFromAbove(game,$"{Folder}/stage{stage}-above.png");
                log.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "stage {0} ({1}): {2}x{3} field, {4} trees ({5} alight), {6} grass, {7} cabins, {8} sheep, pieces {9} tower {10} excavator{11}, "+
                    "wind {12:0} deg {13:0.0}, limit {14}, par {15:0} s, forecast cell {16} in {17:0.0} s; front row {18:F3} m, back row {19:F3} m",
                    stage,run.StageName,run.FieldColumns,run.FieldRows,run.Trees.Length,run.Burning,run.Grass.Length,run.CabinsTotal,run.Animals.Length,
                    run.PieceSet.Towers,run.PieceSet.Excavators,run.PieceSet.Plane?" + plane":"",run.WindPlan.Degrees,run.WindPlan.Strength,
                    run.BurnLimit,run.ParTime,run.ForecastCell(0),run.ForecastSeconds(0),reach.FrontRow,reach.BackRow));
                game.ReturnToMenu();yield return Wait(.3f);
            }
            File.WriteAllText(Folder+"/levels.txt",log.ToString());Debug.Log("[EmberdeepLevels]\n"+log);
        }
        [TearDown] public void Restore()
        {
            if(hints!=null)for(int i=0;i<Moves.Length;i++)if(hints[i]<0)PlayerPrefs.DeleteKey(EmberdeepFirstHints.Key(Moves[i]));else PlayerPrefs.SetInt(EmberdeepFirstHints.Key(Moves[i]),hints[i]);
            PlayerPrefs.Save();hints=null;
        }

        // Headset-like vertical field of view, head pitched to the middle of the board (as EmberdeepBoardFitPlayTests).
        static void Capture(FireRescueGame game,string path,float pitch,float fov=92)
        {
            var camera=game.Player.ViewCamera;
            var rotation=camera.transform.rotation;float priorFov=camera.fieldOfView;var prior=camera.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(1200,1100,24,RenderTextureFormat.ARGB32){antiAliasing=4};var texture=new Texture2D(1200,1100,TextureFormat.RGBA32,false);
            try
            {
                camera.transform.rotation=Quaternion.LookRotation(game.Arena.forward,Vector3.up)*Quaternion.Euler(pitch,0,0);camera.fieldOfView=fov;
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1200,1100),0,0);texture.Apply();
                File.WriteAllBytes(path,texture.EncodeToPNG());
            }
            finally{camera.transform.rotation=rotation;camera.fieldOfView=priorFov;camera.targetTexture=prior;RenderTexture.active=active;target.Release();Object.Destroy(target);Object.Destroy(texture);}
        }
        static void CaptureFromAbove(FireRescueGame game,string path)
        {
            var camera=game.Player.ViewCamera;var t=camera.transform;var position=t.position;var rotation=t.rotation;
            try
            {
                t.SetPositionAndRotation(game.Arena.TransformPoint(new Vector3(0,22,5.6f)),Quaternion.LookRotation(-game.Arena.up,game.Arena.forward));
                Capture(game,path,90,40);
            }
            finally{t.SetPositionAndRotation(position,rotation);}
        }
        static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
    }
}

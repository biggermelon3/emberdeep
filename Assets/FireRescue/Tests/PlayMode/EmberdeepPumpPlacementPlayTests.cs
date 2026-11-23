using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    // Headset test 3 (2026-11-22): "the pump a bit higher and a bit closer". On the real fireground, from the seated eye:
    // the cap is PumpCapBelowEye under the eye and PumpAhead ahead at the default seat. It follows the distance option
    // 1:1 and half the height option, and it stays world-upright when Tilted. Seated-eye captures look toward the pump,
    // and the board shows beside it: Logs/EmberdeepPump/seated-*.png, numbers in Logs/EmberdeepPump/pump.txt.
    public sealed class EmberdeepPumpPlacementPlayTests
    {
        const string Folder="Logs/EmberdeepPump";

        [UnityTest] public IEnumerator PumpCapSitsThirtyCentimetresUnderTheEyeAndFollowsTheOptions()
        {
            Directory.CreateDirectory(Folder);var log=new StringBuilder();var saved=EmberdeepSettingsPrefs.Save();
            try
            {
                PlayerPrefs.DeleteKey(EmberdeepPlayArea.HeightKey);PlayerPrefs.DeleteKey(EmberdeepPlayArea.DistanceKey);PlayerPrefs.DeleteKey(EmberdeepPlayArea.TiltedKey);
                EmberdeepWaterHand.Set(true);EmberdeepPlayArea.Reload();
                EmberdeepPlayArea.SetTilted(false); // measured over the flat board first (tilted by default since headset test 4), tilted below
                EmberdeepSession.NewExpedition();var run=EmberdeepSession.Run;typeof(FireRun).GetProperty("Stage").SetValue(run,5);Assert.IsTrue(run.StartStage(true));
                yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);yield return null;yield return null;
                var game=Object.FindFirstObjectByType<FireRescueGame>();Assert.NotNull(game);game.SendMessage("OnApplicationFocus",true);
                yield return Wait(1.2f);
                Assert.NotNull(game.Pump);Assert.True(game.Pump.gameObject.activeInHierarchy,"spell-mode pump shown");
                var eye=game.Player.ViewCamera.transform.position;

                var cap=Measure(game,eye);log.AppendLine(Line("default",cap));
                Assert.AreEqual(EmberdeepBoardLayout.PumpCapBelowEye,-cap.y,.005f,"cap under the eye");
                Assert.AreEqual(EmberdeepBoardLayout.PumpAhead,cap.z,.005f,"cap ahead of the eye");
                Assert.That(cap.x,Is.InRange(.3f,.45f),"beside the board on the free (right) side");
                Capture(game,Folder+"/seated-default.png");

                // Height extremes: half the board's move (cap 16-41 cm under the eye), distance 1:1.
                foreach(float h in new[]{EmberdeepPlayArea.HeightMin,EmberdeepPlayArea.HeightMax})
                {
                    EmberdeepPlayArea.SetHeight(h);yield return null;
                    var at=Measure(game,eye);float drop=eye.y-game.Arena.position.y;
                    log.AppendLine(Line("height "+h.ToString("F2",CultureInfo.InvariantCulture),at)+string.Format(CultureInfo.InvariantCulture,"  board top under eye {0:F3}",drop));
                    // Over the board top (FireMiniature.PlaceCompact's floor guard may hold a low board up; the pump rides it).
                    float capBelowEye=EmberdeepBoardLayout.PumpCapBelowEye-EmberdeepBoardLayout.PumpHeightFollow*(h-EmberdeepPlayArea.DefaultHeight);
                    Assert.AreEqual(EmberdeepBoardLayout.EyeDrop-h-capBelowEye,drop+at.y,.005f,"cap over the board top at height "+h);
                    if(Mathf.Abs(drop-(EmberdeepBoardLayout.EyeDrop-h))<.002f)Assert.AreEqual(capBelowEye,-at.y,.005f,"cap under the eye at height "+h);
                    Assert.That(-at.y,Is.InRange(.12f,.45f),"never at eye level, never down at the knees: "+h);
                    Capture(game,Folder+"/seated-height"+(h<0?"-35":"+15")+".png");
                }
                EmberdeepPlayArea.SetHeight(EmberdeepPlayArea.DefaultHeight);
                EmberdeepPlayArea.SetDistance(EmberdeepPlayArea.DistanceMax);yield return null;
                var far=Measure(game,eye);log.AppendLine(Line("distance +.30",far));
                Assert.AreEqual(EmberdeepBoardLayout.PumpAhead+EmberdeepPlayArea.DistanceMax-EmberdeepPlayArea.DefaultDistance,far.z,.005f,"follows the distance 1:1");
                EmberdeepPlayArea.SetDistance(EmberdeepPlayArea.DefaultDistance);yield return null;

                // Tilted: the cap stays where the flat board has it, upright in the world.
                var capFlat=game.Pump.CapTop;
                EmberdeepPlayArea.SetTilted(true);yield return null;yield return null;
                Assert.Less(Vector3.Distance(capFlat,game.Pump.CapTop),.001f,"cap unmoved by the tilt");
                Assert.Less(Vector3.Angle(game.Pump.Up,Vector3.up),.01f,"upright in the world");
                log.AppendLine(Line("tilted",Measure(game,eye)));
                Capture(game,Folder+"/seated-tilted.png");
                EmberdeepPlayArea.SetTilted(false);yield return null;
                File.WriteAllText(Folder+"/pump.txt",log.ToString());Debug.Log("[EmberdeepPump]\n"+log);
            }
            finally{saved.Restore();EmberdeepPlayArea.Reload();EmberdeepWaterHand.Reload();EmberdeepSession.NewExpedition();}
        }
        // Cap relative to the eye along the board's flat heading: right, up, ahead (m).
        static Vector3 Measure(FireRescueGame game,Vector3 eye)
        {
            var flat=Vector3.ProjectOnPlane(game.Arena.forward,Vector3.up).normalized;var right=Vector3.Cross(Vector3.up,flat);var d=game.Pump.CapTop-eye;
            return new Vector3(Vector3.Dot(d,right),d.y,Vector3.Dot(d,flat));
        }
        static string Line(string name,Vector3 cap)=>string.Format(CultureInfo.InvariantCulture,
            "{0,-14} cap right {1:F3}  under eye {2:F3}  ahead {3:F3} m",name,cap.x,-cap.y,cap.z);
        // From the seated eye, turned toward the pump (the board's right half shows beside it).
        static void Capture(FireRescueGame game,string path)
        {
            var camera=game.Player.ViewCamera;var t=camera.transform;
            var rotation=t.rotation;float fov=camera.fieldOfView;var prior=camera.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(1200,1000,24,RenderTextureFormat.ARGB32){antiAliasing=4};var texture=new Texture2D(1200,1000,TextureFormat.RGBA32,false);
            try
            {
                var look=Vector3.Lerp(game.Pump.CapTop,game.Arena.TransformPoint(new Vector3(0,0,4)),.35f)-t.position;
                t.rotation=Quaternion.LookRotation(look,Vector3.up);camera.fieldOfView=80;
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1200,1000),0,0);texture.Apply();
                File.WriteAllBytes(path,texture.EncodeToPNG());
            }
            finally{t.rotation=rotation;camera.fieldOfView=fov;camera.targetTexture=prior;RenderTexture.active=active;target.Release();Object.Destroy(target);Object.Destroy(texture);}
        }
        static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
    }
}

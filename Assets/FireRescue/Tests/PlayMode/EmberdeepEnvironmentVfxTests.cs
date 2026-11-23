using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    public sealed class EmberdeepEnvironmentVfxTests
    {
        const string Output="Logs/EmberdeepUsability/Water";
        [Test] public void EffectsStayBoundedReuseMeshAndPauseWithoutCatchup()
        {
            var root=new GameObject("Environment probe");var cameraObject=new GameObject("View");
            try
            {
                var camera=cameraObject.AddComponent<Camera>();
                var fx=new GameObject("Effects").AddComponent<EmberdeepEnvironmentVfx>();fx.Initialize(root.transform);
                fx.Tick(.1f,true,camera);var mesh=fx.EffectMesh;
                fx.Initialize(root.transform);Assert.AreSame(mesh,fx.EffectMesh);
                Assert.Greater(fx.TriangleCount,100);Assert.LessOrEqual(fx.TriangleCount,EmberdeepEnvironmentVfx.MaximumTriangles);
                Assert.AreEqual(1,fx.GetComponentsInChildren<Renderer>().Length);
                float t=fx.Clock;fx.Tick(20,false,camera);fx.Tick(float.NaN,true,camera);Assert.AreEqual(t,fx.Clock);
                for(int i=0;i<160;i++)fx.Tick(.1f,true,camera);
                Assert.AreSame(mesh,fx.EffectMesh);Assert.Less(fx.LocalBounds.max.x,7.7f);
                Assert.Greater(fx.LocalBounds.min.x,-7.7f);Assert.Greater(fx.LocalBounds.min.z,1.5f);
                Assert.Less(fx.LocalBounds.min.z,2.5f,"Effects must reach the near river");
                Assert.Less(fx.LocalBounds.max.z,12.3f);Assert.Less(fx.LocalBounds.max.y,7.7f);
                var local=mesh.vertices[0];root.transform.SetPositionAndRotation(new Vector3(2,1,4),Quaternion.Euler(0,80,0));root.transform.localScale=Vector3.one*.08f;
                fx.Tick(0,false,camera);Assert.AreEqual(local,mesh.vertices[0]);
                Assert.AreEqual(0,fx.GetComponentsInChildren<Collider>().Length);
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(cameraObject);}
        }
        static Texture2D Capture(Camera camera,string name)
        {
            var game=Object.FindFirstObjectByType<FireRescueGame>();game?.ImmersiveSky?.Tick(0,false);
            if(game)foreach(var r in game.GetComponentsInChildren<Renderer>())
                if(r.name=="Instruction plate" || r.name=="Refill step plate" || r.name=="Splash footprint preview" || r.name=="Spray aim")r.enabled=false;
            var old=camera.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(1600,1000,24,RenderTextureFormat.ARGB32);
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                var pixels=new Texture2D(1600,1000,TextureFormat.RGBA32,false);pixels.ReadPixels(new Rect(0,0,1600,1000),0,0);pixels.Apply();
                Directory.CreateDirectory(Output);File.WriteAllBytes(Output+"/"+name+".png",pixels.EncodeToPNG());return pixels;
            }
            finally{camera.targetTexture=old;RenderTexture.active=active;target.Release();Object.DestroyImmediate(target);}
        }
        [UnityTest] public IEnumerator ProductionSceneShowsAnimatedWaterAndKeepsRearViewTransparent()
        {
            EmberdeepSession.NewExpedition();yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);yield return null;yield return null;
            var game=Object.FindFirstObjectByType<FireRescueGame>();Assert.NotNull(game.EnvironmentVfx);
            Assert.AreEqual(0,game.EnvironmentVfx.transform.childCount,"Waterfall and its rock mountain must be removed");
            var fx=game.EnvironmentVfx;var camera=game.Player.ViewCamera;
            int frame=Time.frameCount;yield return null;Assert.Greater(Time.frameCount,frame);
            game.SendMessage("OnApplicationFocus",true);game.SendMessage("OnApplicationPause",false);
            float until=Time.realtimeSinceStartup+10;
            while(game.ImmersiveSky.Progress<1 && Time.realtimeSinceStartup<until)yield return null;
            Assert.AreEqual(1,game.ImmersiveSky.Progress);
            // LoopSpec §6 (L3): a level loads in its Plan phase, where time is stopped (the creek too). Start it with the
            // bell and let the countdown and the colour flood finish before looking at the water.
            Assert.True(game.StrikeBell());until=Time.realtimeSinceStartup+5;
            while(game.Run.Phase==FirePhase.Preparing && Time.realtimeSinceStartup<until)yield return null;
            Assert.AreEqual(FirePhase.Fighting,game.Run.Phase);
            until=Time.realtimeSinceStartup+EmberdeepPieceTuning.FloodSeconds+.3f;while(Time.realtimeSinceStartup<until)yield return null;
            var shader=Resources.Load<Shader>("EmberdeepEnvironmentFx");Assert.IsTrue(shader.isSupported);
#if UNITY_EDITOR
            Assert.IsFalse(UnityEditor.ShaderUtil.ShaderHasError(shader));
#endif
            foreach(var collider in fx.GetComponentsInChildren<Collider>())Assert.IsFalse(collider.enabled);
            var hud=game.transform.Find("Rescue station");var hudRenderers=hud.GetComponentsInChildren<Renderer>();
            foreach(var r in hudRenderers)r.enabled=false;
            // Art-review camera only; production HUD placement remains unchanged.
            camera.transform.position=game.Arena.TransformPoint(new Vector3(-5,7,3));
            camera.transform.LookAt(game.Arena.TransformPoint(new Vector3(0,1.5f,9.6f)));camera.fieldOfView=55;
            fx.gameObject.SetActive(false);
            Object.Destroy(Capture(camera,"01-Creek-Before"));fx.gameObject.SetActive(true);
            yield return null;Object.Destroy(Capture(camera,"02-Forest-Atmosphere"));
            camera.transform.position=game.Arena.TransformPoint(new Vector3(-1.5f,2.2f,-1.3f));
            camera.transform.LookAt(game.Arena.TransformPoint(new Vector3(1,0,2.1f)));camera.fieldOfView=48;
            var before=Capture(camera,"03-Creek-Grazing");
            float clock=fx.Clock;until=Time.realtimeSinceStartup+4;
            while(fx.Clock-clock<.75f && Time.realtimeSinceStartup<until)yield return null;
            Assert.Greater(fx.Clock,clock+.5f);
            var after=Capture(camera,"04-Creek-Moving");
            var a=before.GetPixels32();var b=after.GetPixels32();int changed=0,water=0;
            for(int i=0;i<a.Length;i++)
            {
                if(Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b)>16)changed++;
                if(b[i].g>b[i].r+15 && b[i].b>b[i].r+8 && b[i].a>80)water++;
            }
            Assert.Greater(changed,100,"Water must visibly animate in rendered frames");Assert.Greater(water,100,"Creek must render water, not only mesh data");
            Object.Destroy(before);Object.Destroy(after);
            camera.transform.position=game.Arena.TransformPoint(new Vector3(0,8,-4));camera.transform.rotation=game.Arena.rotation*Quaternion.Euler(0,180,0);
            var rear=Capture(camera,"05-Rear-Transparent");foreach(var p in rear.GetPixels32())Assert.Zero(p.a);Object.Destroy(rear);
            foreach(var r in hudRenderers)if(r)r.enabled=true;
            File.WriteAllText(Output+"/CaptureScope.txt","Actual Unity scene and production effects; live frame advancement. HUD hidden for art review. Not headset passthrough or performance acceptance.");
            game.ReturnToMenu();until=Time.realtimeSinceStartup+10;
            while(SceneManager.GetActiveScene().name!=EmberdeepSession.CampScene && Time.realtimeSinceStartup<until)yield return null;
            yield return null;Assert.IsNull(Object.FindFirstObjectByType<EmberdeepEnvironmentVfx>());
        }
    }
}

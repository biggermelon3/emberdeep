using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    public sealed class EmberdeepImmersiveSkyTests
    {
        GameObject root;
        Camera camera;
        EmberdeepImmersiveSky sky;
        UnityEngine.Rendering.RenderPipelineAsset previousPipeline;
        bool restorePipeline;
        const string Output="Logs/EmberdeepImmersiveSky";
        void CreateIsolatedSky()
        {
            previousPipeline=QualitySettings.renderPipeline;restorePipeline=true;
            QualitySettings.renderPipeline=EmberdeepArtLibrary.Load().Pipeline;
            root=new GameObject("Sky render test"){layer=30};
            camera=root.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<30;
            camera.clearFlags=CameraClearFlags.Skybox;camera.backgroundColor=new Color(.1f,.2f,.3f,.4f);
            camera.fieldOfView=110;camera.aspect=1;camera.farClipPlane=100;
            sky=root.AddComponent<EmberdeepImmersiveSky>();sky.Initialize(camera);
        }
        [TearDown] public void Cleanup()
        {
            if(root)Object.DestroyImmediate(root);
            if(restorePipeline)QualitySettings.renderPipeline=previousPipeline;
            restorePipeline=false;
        }
        static Texture2D Render(Camera view,int width=256,int height=256)
        {
            var previous=view.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);
            try
            {
                view.targetTexture=target;view.Render();RenderTexture.active=target;
                var pixels=new Texture2D(width,height,TextureFormat.RGBA32,false);
                pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();return pixels;
            }
            finally{view.targetTexture=previous;RenderTexture.active=active;target.Release();Object.DestroyImmediate(target);}
        }
        [Test] public void RenderedAlphaRisesFromBottomThenFullyOccludesBackground()
        {
            CreateIsolatedSky();Physics.SyncTransforms();
            var pixels=Render(camera);
            Assert.Less(pixels.GetPixel(128,24).a,.01f);Assert.Less(pixels.GetPixel(128,232).a,.01f);
            Object.DestroyImmediate(pixels);
            for(int i=0;i<20;i++)sky.Tick(.1f,true);
            Assert.That(sky.Progress,Is.EqualTo(.5f).Within(.001f));pixels=Render(camera);
            Assert.Greater(pixels.GetPixel(128,24).a,.98f,"Lower view must be covered first");
            Assert.Less(pixels.GetPixel(128,232).a,.02f,"Upper view must still expose the underlay midway");
            Object.DestroyImmediate(pixels);
            for(int i=0;i<21;i++)sky.Tick(.1f,true);pixels=Render(camera);
            for(int y=8;y<256;y+=24)for(int x=8;x<256;x+=24)Assert.Greater(pixels.GetPixel(x,y).a,.98f);
            Assert.Greater(Vector3.Distance((Vector4)pixels.GetPixel(128,24),(Vector4)pixels.GetPixel(128,232)),.05f,"The environment must have visible sky/ground colour separation");
            Object.DestroyImmediate(pixels);
        }
        [Test] public void BackgroundDoesNotCoverForegroundObjectsOrAddInteractionColliders()
        {
            CreateIsolatedSky();for(int i=0;i<41;i++)sky.Tick(.1f,true);
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.layer=30;cube.transform.SetParent(root.transform,false);
            cube.transform.localPosition=Vector3.forward*2;cube.transform.localScale=Vector3.one*.5f;
            var material=new Material(Resources.Load<Material>("EmberdeepMenuUnlit"));material.SetColor("_BaseColor",Color.red);
            cube.GetComponent<Renderer>().sharedMaterial=material;
            try
            {
                Physics.SyncTransforms();var pixels=Render(camera);var center=pixels.GetPixel(128,128);Object.DestroyImmediate(pixels);
                Assert.Greater(center.r,.8f);Assert.Less(center.g,.15f);Assert.Greater(center.a,.98f);
                foreach(var collider in sky.Backdrop.GetComponents<Collider>())Assert.IsFalse(collider.enabled);
            }
            finally{Object.DestroyImmediate(material);}
        }
        [Test] public void ProgressPausesWithoutCatchupAndCameraStateIsRestored()
        {
            CreateIsolatedSky();sky.Tick(.1f,true);float progress=sky.Progress;
            sky.Tick(20,false);sky.Tick(float.NaN,true);Assert.AreEqual(progress,sky.Progress);
            sky.Tick(20,true);Assert.That(sky.Progress-progress,Is.EqualTo(.025f).Within(.001f));
            camera.transform.SetPositionAndRotation(new Vector3(2,1,3),Quaternion.Euler(20,70,0));
            sky.Tick(0,false);Assert.AreEqual(camera.transform.position,sky.Backdrop.position);
            Assert.That(Quaternion.Angle(sky.Backdrop.rotation,Quaternion.identity),Is.LessThan(.001f),"Sky must not rotate with the player's head");
            Object.DestroyImmediate(sky);
            Assert.AreEqual(CameraClearFlags.Skybox,camera.clearFlags);
            Assert.AreEqual(new Color(.1f,.2f,.3f,.4f),camera.backgroundColor);
        }
        [UnityTest] public IEnumerator FiregroundRevealsDuringLiveFramesAndCampRemovesIt()
        {
            yield return SceneManager.LoadSceneAsync(EmberdeepSession.CampScene);yield return null;
            EmberdeepSession.NewExpedition();yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);
            yield return null;yield return null;
            var game=Object.FindFirstObjectByType<FireRescueGame>();Assert.NotNull(game.ImmersiveSky);
            game.SendMessage("OnApplicationFocus",true);game.SendMessage("OnApplicationPause",false);
            // Sector two exposes both existing refill stations for the art review.
            game.Run.ApplyWater(Vector3.zero,200,100,1);game.StartNext();yield return null;
            Assert.AreEqual(1,game.Run.Stage);Assert.IsTrue(game.CreekStation.gameObject.activeSelf);
            Directory.CreateDirectory(Output);
            void Capture(string name)
            {
                var pixels=Render(game.Player.ViewCamera,1600,1000);
                File.WriteAllBytes(Output+"/"+name+".png",pixels.EncodeToPNG());Object.Destroy(pixels);
            }
            Capture("01-Entering");int firstFrame=Time.frameCount;float start=game.ImmersiveSky.Progress;
            float deadline=Time.realtimeSinceStartup+12;
            while(game.ImmersiveSky.Progress<.5f && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.Greater(Time.frameCount,firstFrame);Assert.Greater(game.ImmersiveSky.Progress,start);Capture("02-Rising");
            float held=game.ImmersiveSky.Progress;game.SendMessage("OnApplicationPause",true);
            for(int i=0;i<5;i++)yield return null;
            Assert.AreEqual(held,game.ImmersiveSky.Progress);game.SendMessage("OnApplicationPause",false);
            game.Recenter();Assert.AreEqual(held,game.ImmersiveSky.Progress,"Recenter must not replay the reveal");
            while(game.ImmersiveSky.Progress<1 && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.AreEqual(1,game.ImmersiveSky.Progress);Capture("03-Surrounded");
            game.Player.ViewCamera.transform.rotation=Quaternion.Euler(0,0,0);Capture("04-Level-View");
            File.WriteAllText(Output+"/CaptureScope.txt","Live Editor frames; synthetic sector advancement and desktop camera. Not worn-headset or passthrough-compositor evidence.");
            game.ReturnToMenu();
            while(SceneManager.GetActiveScene().name!=EmberdeepSession.CampScene && Time.realtimeSinceStartup<deadline+10)yield return null;
            yield return null;Assert.IsNull(Object.FindFirstObjectByType<EmberdeepImmersiveSky>());
        }
    }
}

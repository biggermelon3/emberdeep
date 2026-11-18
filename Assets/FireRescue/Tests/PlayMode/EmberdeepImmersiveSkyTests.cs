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
        Transform arena;
        EmberdeepImmersiveSky sky;
        UnityEngine.Rendering.RenderPipelineAsset previousPipeline;
        bool restorePipeline;
        const string Output="Logs/EmberdeepUsability/Sky";
        void CreateIsolatedSky()
        {
            previousPipeline=QualitySettings.renderPipeline;restorePipeline=true;
            QualitySettings.renderPipeline=EmberdeepArtLibrary.Load().Pipeline;
            root=new GameObject("Sky render test"){layer=30};
            camera=new GameObject("View").AddComponent<Camera>();camera.transform.SetParent(root.transform,false);
            camera.transform.position=new Vector3(0,6.6f,-6);camera.enabled=false;camera.cullingMask=1<<30;
            camera.clearFlags=CameraClearFlags.Skybox;camera.backgroundColor=new Color(.1f,.2f,.3f,.4f);
            camera.fieldOfView=50;camera.aspect=1;camera.farClipPlane=100;
            arena=new GameObject("Arena").transform;arena.SetParent(root.transform,false);
            sky=root.AddComponent<EmberdeepImmersiveSky>();sky.Initialize(camera,arena);
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
        [Test] public void AngularEnvironmentRevealsUpwardsWithTransparentOutside()
        {
            CreateIsolatedSky();camera.fieldOfView=70;
            var pixels=Render(camera);Assert.Less(pixels.GetPixel(128,128).a,.01f);Object.DestroyImmediate(pixels);
            for(int i=0;i<20;i++)sky.Tick(.1f,true);
            pixels=Render(camera);Assert.Greater(pixels.GetPixel(128,35).a,.98f);Assert.Less(pixels.GetPixel(128,220).a,.02f);Object.DestroyImmediate(pixels);
            for(int i=0;i<21;i++)sky.Tick(.1f,true);
            pixels=Render(camera);Assert.Greater(pixels.GetPixel(128,128).a,.98f);Object.DestroyImmediate(pixels);
            Assert.Greater(sky.Backdrop.GetComponent<MeshFilter>().sharedMesh.vertexCount,100,"Use a surrounding environment, not a quad");
            camera.fieldOfView=160;pixels=Render(camera);
            Assert.Less(pixels.GetPixel(128,8).a,.01f);Assert.Less(pixels.GetPixel(128,248).a,.01f);
            Directory.CreateDirectory(Output);File.WriteAllBytes(Output+"/Angular-Alpha.png",pixels.EncodeToPNG());Object.DestroyImmediate(pixels);
        }
        [Test] public void BackgroundDoesNotCoverForegroundObjectsOrAddInteractionColliders()
        {
            CreateIsolatedSky();for(int i=0;i<41;i++)sky.Tick(.1f,true);
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.layer=30;cube.transform.SetParent(root.transform,false);
            cube.transform.position=camera.transform.position+Vector3.forward*2;cube.transform.localScale=Vector3.one*.5f;
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
            Vector3 fixedPosition=sky.Backdrop.position;
            camera.transform.SetPositionAndRotation(new Vector3(2,1,3),Quaternion.Euler(20,70,0));
            sky.Tick(0,false);Assert.AreEqual(camera.transform.position,sky.Backdrop.position,"The distant dome follows head translation");
            Assert.That(Quaternion.Angle(sky.Backdrop.rotation,Quaternion.identity),Is.LessThan(.001f),"Sky must not rotate with the player's head");
            arena.SetPositionAndRotation(new Vector3(3,0,5),Quaternion.Euler(0,70,0));arena.localScale=Vector3.one*.08f;
            sky.Tick(0,false);Assert.AreEqual(camera.transform.position,sky.Backdrop.position);
            Assert.AreEqual(progress+.025f,sky.Progress,"Recenter must not replay the reveal");
            Object.DestroyImmediate(sky);
            Assert.AreEqual(CameraClearFlags.Skybox,camera.clearFlags);
            Assert.AreEqual(new Color(.1f,.2f,.3f,.4f),camera.backgroundColor);
        }
        [TestCase(0,90)] [TestCase(0,-90)] [TestCase(0,180)]
        [TestCase(85,0)] [TestCase(-85,0)]
        public void LookingOutsideFiregroundDoesNotWriteColorOrAlpha(float pitch,float yaw)
        {
            CreateIsolatedSky();for(int i=0;i<41;i++)sky.Tick(.1f,true);
            camera.transform.rotation=Quaternion.Euler(pitch,yaw,0);camera.fieldOfView=12;sky.Tick(0,false);
            var pixels=Render(camera);
            try { foreach(var pixel in pixels.GetPixels32())Assert.AreEqual(new Color32(0,0,0,0),pixel); }
            finally { Object.DestroyImmediate(pixels); }
        }
        [TestCase(-80,-25)] [TestCase(-80,0)] [TestCase(-80,25)]
        [TestCase(0,-25)] [TestCase(0,0)] [TestCase(0,25)]
        [TestCase(80,-25)] [TestCase(80,0)] [TestCase(80,25)]
        [TestCase(180,0)]
        public void ForestKeepsItsSkyDuringOrbitAndHeadTurns(float orbit,float turn)
        {
            CreateIsolatedSky();for(int i=0;i<41;i++)sky.Tick(.1f,true);
            var target=new Vector3(0,3,6);
            camera.transform.position=new Vector3(0,6.6f,6)+Quaternion.Euler(0,orbit,0)*Vector3.back*12;
            camera.transform.LookAt(target);camera.transform.Rotate(0,turn,0,Space.Self);camera.fieldOfView=90;
            sky.Tick(0,false);var pixels=Render(camera);
            var uv=camera.WorldToViewportPoint(target);
            try { Assert.Greater(pixels.GetPixel(Mathf.RoundToInt(uv.x*255),Mathf.RoundToInt(uv.y*255)).a,.97f,"Forest background must stay filled when the viewer moves or turns"); }
            finally { Object.DestroyImmediate(pixels); }
        }
        [TestCase(-8,0)] [TestCase(8,0)]
        [TestCase(-8,-20)] [TestCase(8,20)]
        public void NearEquipmentCornersAndTheirSurroundingsHaveBackground(float x,float yaw)
        {
            CreateIsolatedSky();
            var tool=GameObject.CreatePrimitive(PrimitiveType.Cube);tool.transform.SetParent(root.transform,false);
            tool.transform.position=new Vector3(x,1,-2);tool.transform.localScale=new Vector3(1.2f,2,1);
            var renderer=tool.GetComponent<MeshRenderer>();renderer.sharedMaterial=Resources.Load<Material>("EmberdeepMenuUnlit");
            sky.SetEquipmentSubjects(tool.transform,null,null,null);
            camera.transform.rotation=Quaternion.Euler(30,yaw,0);camera.fieldOfView=130;
            for(int i=0;i<41;i++)sky.Tick(.1f,true);
            var bounds=renderer.bounds;renderer.forceRenderingOff=true;
            var pixels=Render(camera,512,512);
            try
            {
                for(int corner=0;corner<8;corner++)
                {
                    var point=bounds.center+Vector3.Scale(bounds.extents+Vector3.one*.1f,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                    var uv=camera.WorldToViewportPoint(point);
                    Assert.That(uv.x,Is.InRange(0f,1f));Assert.That(uv.y,Is.InRange(0f,1f));
                    Assert.Greater(pixels.GetPixel(Mathf.RoundToInt(uv.x*511),Mathf.RoundToInt(uv.y*511)).a,.98f,"Sky must surround the near tool, including its lower corners");
                }
            }
            finally { Object.DestroyImmediate(pixels); }
            // Moving the same held tool must update coverage without waiting for hierarchy refresh.
            renderer.forceRenderingOff=false;tool.transform.position=new Vector3(-x,1,-2);sky.Tick(0,false);renderer.forceRenderingOff=true;
            camera.transform.LookAt(tool.transform.position);sky.Tick(0,false);
            pixels=Render(camera);Assert.Greater(pixels.GetPixel(128,128).a,.98f);Object.DestroyImmediate(pixels);
            tool.SetActive(false);sky.Tick(0,false);pixels=Render(camera);
            Assert.Less(pixels.GetPixel(128,128).a,.65f,"Hidden equipment must leave the opaque envelope; a wider circular feather may remain");Object.DestroyImmediate(pixels);
        }
        [UnityTest] public IEnumerator MenuAndFiregroundHaveImmediateSubjectBoundedSky()
        {
            yield return SceneManager.LoadSceneAsync(EmberdeepSession.CampScene);yield return null;
            EmberdeepSession.NewExpedition();yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);
            yield return null;yield return null;
            var game=Object.FindFirstObjectByType<FireRescueGame>();Assert.NotNull(game.ImmersiveSky);
            game.SendMessage("OnApplicationFocus",true);game.SendMessage("OnApplicationPause",false);
            // Production keeps one left station and one right platform across sectors.
            EmberdeepTestFlow.StartPrepared(game.Run);game.Run.ApplyWater(Vector3.zero,200,100,1);game.Run.EvacuateSafeAnimals();game.Run.BuyMagicUpgrade();game.StartNext();yield return null;
            Assert.AreEqual(1,game.Run.Stage);Assert.IsFalse(game.CreekStation.gameObject.activeSelf);
            Directory.CreateDirectory(Output);
            void Capture(string name)
            {
                game.ImmersiveSky.Tick(0,false);var pixels=Render(game.Player.ViewCamera,1600,1000);
                File.WriteAllBytes(Output+"/"+name+".png",pixels.EncodeToPNG());Object.Destroy(pixels);
            }
            Capture("01-Entering");int firstFrame=Time.frameCount;float start=game.ImmersiveSky.Progress;
            float deadline=Time.realtimeSinceStartup+12;
            yield return null;
            Assert.Greater(Time.frameCount,firstFrame);Assert.AreEqual(1,game.ImmersiveSky.Progress);Capture("02-Rising");
            float held=game.ImmersiveSky.Progress;game.SendMessage("OnApplicationPause",true);
            for(int i=0;i<5;i++)yield return null;
            Assert.AreEqual(held,game.ImmersiveSky.Progress);game.SendMessage("OnApplicationPause",false);
            game.Recenter();Assert.AreEqual(held,game.ImmersiveSky.Progress,"Recenter must not replay the reveal");
            while(game.ImmersiveSky.Progress<1 && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.AreEqual(1,game.ImmersiveSky.Progress);Capture("03-Local-Backdrop");
            Assert.AreEqual(game.Player.ViewCamera.transform.position,game.ImmersiveSky.Backdrop.position);
            game.Player.ViewCamera.transform.rotation=Quaternion.Euler(0,0,0);Capture("04-Level-View");
            game.Player.ViewCamera.transform.rotation=Quaternion.Euler(0,90,0);Capture("05-Side-Transparent");
            game.Player.ViewCamera.transform.rotation=Quaternion.Euler(0,180,0);Capture("06-Behind-Transparent");
            game.ReturnEquipment();
            foreach(var r in game.transform.Find("Rescue station").GetComponentsInChildren<Renderer>())r.enabled=false;
            foreach(var label in game.GetComponentsInChildren<TextMesh>())label.GetComponent<Renderer>().enabled=false;
            foreach(var r in game.GetComponentsInChildren<Renderer>())
                if(r.name=="Instruction plate" || r.name=="Refill step plate")r.enabled=false;
            Directory.CreateDirectory(Output+"/Orbit");
            for(int frame=0;frame<72;frame++)
            {
                float angle=Mathf.Lerp(-50,50,frame/71f);
                var position=new Vector3(0,7.5f,6)+Quaternion.Euler(0,angle,0)*Vector3.back*10;
                var view=game.Player.ViewCamera;view.transform.position=game.Arena.TransformPoint(position);
                view.transform.LookAt(game.Arena.TransformPoint(new Vector3(0,4.2f,6)));view.fieldOfView=65;
                yield return null;
                game.ImmersiveSky.Tick(0,false);var pixels=Render(view,960,600);
                File.WriteAllBytes(Output+"/Orbit/"+frame.ToString("D3")+".png",pixels.EncodeToPNG());Object.Destroy(pixels);
                if(frame==0 || frame==36 || frame==71)Capture("Orbit-"+frame);
            }
            for(int frame=0;frame<36;frame++)
            {
                var view=game.Player.ViewCamera;view.transform.position=game.Arena.TransformPoint(new Vector3(0,7.5f,-4));
                view.transform.LookAt(game.Arena.TransformPoint(new Vector3(0,4.2f,6)));
                view.transform.Rotate(0,Mathf.Lerp(-25,25,frame/35f),0,Space.Self);
                yield return null;game.ImmersiveSky.Tick(0,false);
                var pixels=Render(view,960,600);File.WriteAllBytes(Output+"/Orbit/"+(frame+72).ToString("D3")+".png",pixels.EncodeToPNG());Object.Destroy(pixels);
            }
            var wideView=game.Player.ViewCamera;
            wideView.transform.position=game.Arena.TransformPoint(new Vector3(0,9,-9));
            wideView.transform.LookAt(game.Arena.TransformPoint(new Vector3(0,2,5)));wideView.fieldOfView=95;
            yield return null;Capture("Wide-Outline");
            deadline=Time.realtimeSinceStartup+12;
            File.WriteAllText(Output+"/CaptureScope.txt","Live Editor frames; synthetic sector advancement and desktop camera. Not worn-headset or passthrough-compositor evidence.");
            game.ReturnToMenu();
            while(SceneManager.GetActiveScene().name!=EmberdeepSession.CampScene && Time.realtimeSinceStartup<deadline+10)yield return null;
            yield return null;var camp=Object.FindFirstObjectByType<EmberdeepCamp>();Assert.NotNull(camp.ImmersiveSky);Assert.AreEqual(1,camp.ImmersiveSky.Progress);
        }
    }
}

using System.Collections;
using System.IO;
using System.Reflection;
using System.Linq;
using PhysicalAssembly;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    public sealed class EmberdeepSurfaceSceneTests
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static FireToolManipulation.Hand At(Vector3 p,float closure)=>new(){Valid=true,Palm=new Pose(p,Quaternion.identity),Contact=p,Closure=closure};
        [UnityTest] public IEnumerator ActualScenePickupUsesSurfacePolicyAndCanReturnToRack()
        {
            EmberdeepSession.NewExpedition();yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);yield return null;
            var game=Object.FindFirstObjectByType<FireRescueGame>();Assert.NotNull(game);
            int frame=Time.frameCount;yield return null;Assert.Greater(Time.frameCount,frame);
            float skyDeadline=Time.realtimeSinceStartup+10;
            while(game.ImmersiveSky && game.ImmersiveSky.Progress<1 && Time.realtimeSinceStartup<skyDeadline)yield return null;
            Assert.AreEqual(1,game.ImmersiveSky.Progress,"Manipulation capture must include the completed environment transition");
            game.ReturnEquipment();
            var type=typeof(FireRescueGame);var samples=(FireToolManipulation.Hand[])type.GetField("toolHands",Private).GetValue(game);
            var rack=(FireEquipmentView)type.GetField("rackView",Private).GetValue(game);
            var pickup=type.GetMethod("ProcessToolPickup",Private);var carry=type.GetMethod("ProcessCarriedTool",Private);
            void Pick()=>pickup.Invoke(game,new object[]{.025f});
            void Carry()=>carry.Invoke(game,new object[]{.025f,true});
            samples[0]=At(Vector3.left*2,0);samples[1]=At(rack.Grip.position,0);Pick();
            samples[1].Closure=.5f;for(int i=0;i<4;i++)Pick();
            Assert.IsTrue(game.HoldingEquipment);Assert.AreEqual(FireToolRegion.Trigger,game.ToolManipulation.Region);
            Assert.That(Vector3.Distance(game.HeldTool.transform.position,rack.transform.position),Is.LessThan(.001f));
            // A carrying fist must not also activate the real RETURN TOOL button.
            var returnButton=game.GetComponentsInChildren<WorkshopButton>(true).First(b=>b.Command=="Return equipment");
            var ray=new Ray(game.Player.ViewCamera.transform.position,(returnButton.transform.position-game.Player.ViewCamera.transform.position).normalized);
            game.MenuInput.MaximumHeadAngle=180;Physics.SyncTransforms();
            for(int i=0;i<8;i++)Assert.IsFalse(game.ProcessHandMenuInput(default,new EmberdeepHandRay.Sample(true,ray,.04f),true,.025f));
            for(int i=0;i<5;i++)Assert.IsFalse(game.ProcessHandMenuInput(default,new EmberdeepHandRay.Sample(true,ray,.01f),true,.025f));
            Assert.IsTrue(game.HoldingEquipment);Assert.IsFalse(returnButton.IsHovered);
            var offset=game.Player.ViewCamera.transform.TransformPoint(new Vector3(.08f,-.08f,.55f))-samples[1].Contact;
            samples[1].Contact+=offset;samples[1].Palm.position=samples[1].Contact;Carry();Carry();
            samples[0]=At(game.HeldTool.Nozzle.position,0);Carry();samples[0].Closure=.5f;for(int i=0;i<4;i++)Carry();
            Assert.IsTrue(game.ToolManipulation.NozzleHeld);
            samples[0].Contact+=game.Player.ViewCamera.transform.right*-.16f;samples[0].Palm.position=samples[0].Contact;Carry();
            samples[1].Closure=1;for(int i=0;i<4;i++)Carry();Assert.IsTrue(game.ToolManipulation.Firing);
            samples[1].Closure=.5f;Carry();Assert.IsTrue(game.HoldingEquipment);Assert.IsFalse(game.ToolManipulation.Firing);
            Directory.CreateDirectory("Logs/EmberdeepSurfaceGrip");
            var camera=game.Player.ViewCamera;var old=camera.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(1600,1000,24,RenderTextureFormat.ARGB32);var pixels=new Texture2D(1600,1000,TextureFormat.RGBA32,false);
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1600,1000),0,0);pixels.Apply();
                File.WriteAllBytes("Logs/EmberdeepSurfaceGrip/Editor-Independent-Nozzle.png",pixels.EncodeToPNG());
            }
            finally{camera.targetTexture=old;RenderTexture.active=active;target.Release();Object.Destroy(target);Object.Destroy(pixels);}
            samples[0].Closure=0;samples[1].Contact-=offset;samples[1].Palm.position=samples[1].Contact;Carry();Carry();
            samples[1].Closure=0;for(int i=0;i<20 && game.HoldingEquipment;i++)Carry();
            Assert.IsFalse(game.HoldingEquipment);Assert.IsFalse(game.ToolManipulation.NozzleHeld);
            // Relaxing both hands keeps the main tool held until an explicit rack return.
            samples[0]=At(Vector3.left*2,0);samples[1]=At(rack.Grip.position,0);Pick();
            samples[1].Closure=.5f;for(int i=0;i<4;i++)Pick();Assert.IsTrue(game.HoldingEquipment);
            samples[1].Contact+=offset;samples[1].Palm.position=samples[1].Contact;Carry();Carry();
            samples[1].Closure=0;for(int i=0;i<10;i++)Carry();Assert.IsTrue(game.HoldingEquipment);
            for(int i=0;i<30;i++)Carry();Assert.IsTrue(game.HoldingEquipment);game.ReturnEquipment();
            game.ReturnToMenu();float deadline=Time.realtimeSinceStartup+10;
            while(SceneManager.GetActiveScene().name!=EmberdeepSession.CampScene && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.AreEqual(EmberdeepSession.CampScene,SceneManager.GetActiveScene().name);
        }
    }
}

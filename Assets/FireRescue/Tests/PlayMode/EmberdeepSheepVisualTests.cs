using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    public sealed class EmberdeepSheepVisualTests
    {
        [UnityTest] public IEnumerator CuteSheepRenderAndAnimateWithoutBarsOrColliders()
        {
            var root=new GameObject("Animal visual verification"){layer=30};var run=new FireRun();run.StartStage();
            var view=root.AddComponent<EmberdeepSheepView>();view.Initialize(run);
            foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
            var camera=new GameObject("Sheep portrait").AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<30;
            var pipeline=QualitySettings.renderPipeline;QualitySettings.renderPipeline=EmberdeepArtLibrary.Load().Pipeline;
            var rt=new RenderTexture(960,600,24);var old=RenderTexture.active;
            try
            {
                Assert.AreEqual(3,view.Count);Assert.Zero(root.GetComponentsInChildren<Collider>().Length);Assert.Zero(root.GetComponentsInChildren<TextMesh>().Length);
                for(int i=0;i<run.Animals.Length;i++)run.Animals[i].Position=new Vector3((i-1)*1.1f,0,i==1?.3f:0);
                view.Render();camera.transform.position=new Vector3(1.7f,1.5f,-4.4f);camera.transform.LookAt(new Vector3(0,.5f,0));camera.fieldOfView=39;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.37f,.62f,.56f);camera.targetTexture=rt;
                Directory.CreateDirectory("Logs/Emberdeep024/SheepVisual");
                for(int frame=0;frame<24;frame++)
                {
                    var a=run.Animals[1];a.State=AnimalState.Celebrating;a.StateTime=frame/24f;a.AnimationTime=frame/12f;
                    run.Animals[0].Threat=1;run.Animals[0].AnimationTime=frame/12f;run.Animals[2].Affection=1.5f-frame/30f;run.Animals[2].WaterReaction=.8f;run.Animals[2].AnimationTime=frame/12f;
                    view.Render();yield return null;camera.Render();RenderTexture.active=rt;
                    var pixels=new Texture2D(960,600,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,960,600),0,0);pixels.Apply();
                    File.WriteAllBytes($"Logs/Emberdeep024/SheepVisual/{frame:000}.png",pixels.EncodeToPNG());Object.Destroy(pixels);
                }
                Assert.Greater(root.GetComponentsInChildren<MeshRenderer>().Length,3);
            }
            finally{camera.targetTexture=null;RenderTexture.active=old;rt.Release();Object.Destroy(rt);Object.Destroy(camera.gameObject);Object.Destroy(root);QualitySettings.renderPipeline=pipeline;}
        }
    }
}

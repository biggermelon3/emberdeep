using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    public sealed class FireFeedbackPlayTests
    {
        [UnityTest] public IEnumerator FeedbackStormUsesBoundedVoicesAndSilenceStopsEverySound()
        {
            var root=new GameObject("Feedback stress test");
            try
            {
                var feedback=root.AddComponent<FireFeedback>();feedback.Initialize();
                var sources=root.GetComponentsInChildren<AudioSource>();
                Assert.AreEqual(4,sources.Length);
                var flowClip=sources[0].clip;
                feedback.Initialize();Assert.AreSame(flowClip,sources[0].clip,"Initialize must not leak another set of clips");
                for(int i=0;i<100;i++)
                {
                    feedback.Tick(true,.8f,FireDelivery.Charge,.016f);
                    feedback.Impact(true);feedback.Rescue(i+1);
                    if(i%20==0)feedback.Launch(1);
                }
                Assert.AreEqual(4,root.GetComponentsInChildren<AudioSource>().Length);
                feedback.Complete(true);feedback.Silence();
                foreach(var voice in sources)Assert.IsFalse(voice.isPlaying,"Tracking loss/menu pause must stop every voice");
                Assert.Zero(sources[0].volume);Assert.Zero(sources[1].volume);
                feedback.Tick(false,0,FireDelivery.Foam,.016f);
                foreach(var voice in sources)Assert.IsFalse(voice.isPlaying,"No delayed rescue cue after Silence");
            }
            finally{Object.Destroy(root);}
            yield return null;
        }

        [UnityTest] public IEnumerator CannonAndSprinklerEffectsStayPooledAndAcceptNonTreeImpacts()
        {
            var root=new GameObject("Effect stress test");
            try
            {
                var effects=root.AddComponent<FireRescueVfx>();effects.Initialize();effects.Initialize();
                Assert.AreEqual(7,root.GetComponentsInChildren<ParticleSystem>().Length);
                for(int i=0;i<50;i++)
                {
                    effects.Launch(Vector3.zero,Vector3.forward,1);
                    effects.Burst(Vector3.zero,.24f);effects.Rescue(Vector3.zero,10);
                    effects.Sprinkler(Vector3.zero,.192f,.1f);
                    effects.Sprinkler(Vector3.right*.3f,.192f,.1f);
                    effects.Impact(new WaterJet.Impact{Point=Vector3.zero,Normal=Vector3.up,Tree=43},true);
                }
                var emitters=root.GetComponentsInChildren<ParticleSystem>();
                Assert.AreEqual(7,emitters.Length);
                Assert.Greater(effects.VisibleSplash,0);
                foreach(var emitter in emitters)Assert.LessOrEqual(emitter.particleCount,emitter.main.maxParticles);
                effects.Clear();foreach(var emitter in emitters)Assert.Zero(emitter.particleCount);
            }
            finally{Object.Destroy(root);}
            yield return null;
        }
        [UnityTest] public IEnumerator CannonCoreActuallyDrawsPixelsWithZeroVisualVelocity()
        {
            var root=new GameObject("Water cannon rendering probe");var arena=new GameObject("Probe arena");
            var cameraObject=new GameObject("Probe camera");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
            var target=new RenderTexture(128,128,24);var pixels=new Texture2D(128,128,TextureFormat.RGB24,false);var old=RenderTexture.active;
            try
            {
                var effects=root.AddComponent<FireRescueVfx>();effects.Initialize();
                foreach(var child in root.GetComponentsInChildren<Transform>())child.gameObject.layer=30;
                camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                camera.orthographic=true;camera.orthographicSize=.12f;camera.nearClipPlane=.01f;camera.farClipPlane=5;
                camera.transform.position=new Vector3(0,.3f,-1);camera.targetTexture=target;
                var jet=new WaterJet();jet.Step(.02f,new Vector3(0,.3f,0),Vector3.forward*3.7f,240,3,arena.transform,new Vector3[0],null,3.2f,1,0,FireDelivery.Charge);
                effects.Tick(new FireRun(),arena.transform,jet,.02f);Assert.Greater(effects.VisibleWater,0);
                yield return null;
                var renderer=root.transform.Find("Ballistic water droplets").GetComponent<ParticleSystemRenderer>();
                renderer.enabled=false;camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,128,128),0,0);pixels.Apply();var before=pixels.GetPixels32();
                renderer.enabled=true;camera.Render();pixels.ReadPixels(new Rect(0,0,128,128),0,0);pixels.Apply();var after=pixels.GetPixels32();
                int changed=0;for(int i=0;i<after.Length;i++)if(after[i].b>before[i].b+12)changed++;
                Assert.Greater(changed,20,"Particle count alone is insufficient: a stationary cannon core must render visible pixels");
                System.IO.Directory.CreateDirectory("Logs");System.IO.File.WriteAllBytes("Logs/FirePlay-CannonPixelProbe.png",pixels.EncodeToPNG());
            }
            finally{RenderTexture.active=old;camera.targetTexture=null;Object.Destroy(pixels);Object.Destroy(target);Object.Destroy(root);Object.Destroy(arena);Object.Destroy(cameraObject);}
            yield return null;
        }
    }
}

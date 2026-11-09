using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    public sealed class EmberdeepV2VfxTests
    {
        static readonly Vector3[] NoTargets=new Vector3[0];
        static void Flow(WaterJet jet,Transform arena,float dt,float amount,FireDelivery delivery=FireDelivery.Foam)
        {jet.Step(dt,new Vector3(0,1,0),Vector3.right*2.8f,amount,100,arena,NoTargets,null,.82f,1,0,delivery);}

        [UnityTest] public IEnumerator ReusesOriginalParticleBudgetsAndOneBoundedStrandRenderer()
        {
            var root=new GameObject("ArtV2 budget probe");var arena=new GameObject("Probe arena");
            try
            {
                var effects=root.AddComponent<FireRescueVfx>();effects.Initialize();effects.Initialize();
                var emitters=root.GetComponentsInChildren<ParticleSystem>();Assert.AreEqual(7,emitters.Length);
                int[] budgets={1024,384,160,256,384,768,84};int total=0;
                for(int i=0;i<emitters.Length;i++){Assert.AreEqual(budgets[i],emitters[i].main.maxParticles);total+=emitters[i].main.maxParticles;}
                Assert.AreEqual(3060,total);Assert.AreEqual(8,root.GetComponentsInChildren<Renderer>().Length);
                var jet=new WaterJet();var run=new FireRun();
                for(int i=0;i<16;i++)
                {
                    jet.Step(.1f,new Vector3(0,1000,0),Vector3.right*2.8f,1,100,arena.transform,NoTargets,null,.82f,1,0,FireDelivery.Foam);
                    effects.Tick(run,arena.transform,jet,.1f);
                }
                Assert.AreEqual(FireRescueVfx.MaximumStreamLinks,effects.VisibleStreamLinks);
                Assert.LessOrEqual(effects.StreamTriangleCount,3072);
                var mesh=root.GetComponentInChildren<MeshFilter>().sharedMesh;
                Assert.LessOrEqual(mesh.vertexCount,2880);
                foreach(var emitter in emitters)Assert.LessOrEqual(emitter.particleCount,emitter.main.maxParticles);
                effects.Clear();Assert.Zero(effects.VisibleStreamLinks);Assert.Zero(effects.StreamTriangleCount);
                Assert.IsFalse(root.transform.Find("Bounded ballistic water strands").GetComponent<Renderer>().enabled);
            }
            finally{Object.Destroy(root);Object.Destroy(arena);}
            yield return null;
        }

        [UnityTest] public IEnumerator ShortReleaseSplitsStreamsAndPulseOrCannonNeverBecomeContinuousLines()
        {
            var root=new GameObject("ArtV2 burst continuity probe");var arena=new GameObject("Probe arena");
            try
            {
                var effects=root.AddComponent<FireRescueVfx>();effects.Initialize();var run=new FireRun();var jet=new WaterJet();
                Flow(jet,arena.transform,.01f,1);effects.Tick(run,arena.transform,jet,.01f);
                Assert.AreEqual(1,effects.VisibleStreamLinks);
                Flow(jet,arena.transform,.01f,0);effects.Tick(run,arena.transform,jet,.01f);
                Flow(jet,arena.transform,.01f,1);effects.Tick(run,arena.transform,jet,.01f);
                Assert.AreEqual(4,jet.Count);Assert.AreEqual(2,effects.VisibleStreamLinks,"Do not bridge the release even when the gap is below .04 seconds");
                foreach(var delivery in new[]{FireDelivery.Pulse,FireDelivery.Charge})
                {
                    jet.Clear();effects.Clear();Flow(jet,arena.transform,.1f,1,delivery);effects.Tick(run,arena.transform,jet,.1f);
                    Assert.Greater(effects.VisibleWater,0);Assert.Zero(effects.VisibleStreamLinks);
                }
            }
            finally{Object.Destroy(root);Object.Destroy(arena);}
            yield return null;
        }

        [UnityTest] public IEnumerator PresentationNeverMutatesWaterPacketsOrAppliesAnAdditionalHit()
        {
            var root=new GameObject("ArtV2 authority probe");var arena=new GameObject("Probe arena");
            try
            {
                var effects=root.AddComponent<FireRescueVfx>();effects.Initialize();var run=new FireRun();run.StartStage();var jet=new WaterJet();
                Flow(jet,arena.transform,.1f,1);var before=(WaterJet.Packet[])jet.Packets.Clone();int count=jet.Count;
                float heat=run.Trees[0].Heat,water=run.Water;int coins=run.Coins;
                effects.Tick(run,arena.transform,jet,.1f);
                effects.Impact(new WaterJet.Impact{Point=run.Trees[0].Position,Normal=Vector3.up,Tree=0,Amount=500},true);
                Assert.AreEqual(count,jet.Count);for(int i=0;i<count;i++)Assert.AreEqual(before[i],jet.Packets[i]);
                Assert.AreEqual(heat,run.Trees[0].Heat);Assert.AreEqual(water,run.Water);Assert.AreEqual(coins,run.Coins);
                Assert.Greater(effects.VisibleSplash,0);Assert.Greater(effects.VisibleSteam,0);
                var flame=root.transform.Find("Turbulent flame particles").GetComponent<ParticleSystem>();
                run.ApplyWater(run.Trees[0].Position,100,.1f);effects.Tick(run,arena.transform,jet,.1f);
                var particles=new ParticleSystem.Particle[1024];int n=flame.GetParticles(particles),cooling=0;
                for(int i=0;i<n;i++)if(particles[i].randomSeed==1){cooling++;Assert.LessOrEqual(particles[i].remainingLifetime,.18001f);}
                Assert.Greater(cooling,0,"Existing flames must visibly contract when their tree is extinguished");
            }
            finally{Object.Destroy(root);Object.Destroy(arena);}
            yield return null;
        }

        [UnityTest] public IEnumerator ContinuousWaterMeshActuallyDrawsCyanPixelsWithoutDropletRenderers()
        {
            var root=new GameObject("ArtV2 strand pixel probe");var arena=new GameObject("Probe arena");
            var cameraObject=new GameObject("Probe camera");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
            var target=new RenderTexture(160,160,24);var pixels=new Texture2D(160,160,TextureFormat.RGB24,false);var old=RenderTexture.active;
            try
            {
                var effects=root.AddComponent<FireRescueVfx>();effects.Initialize();var jet=new WaterJet();var run=new FireRun();
                for(int i=0;i<8;i++){Flow(jet,arena.transform,.02f,1);effects.Tick(run,arena.transform,jet,.02f);}
                Assert.Greater(effects.VisibleStreamLinks,5);
                foreach(var child in root.GetComponentsInChildren<Transform>())child.gameObject.layer=30;
                foreach(var renderer in root.GetComponentsInChildren<ParticleSystemRenderer>())renderer.enabled=false;
                camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                camera.orthographic=true;camera.orthographicSize=.3f;camera.nearClipPlane=.01f;camera.farClipPlane=5;
                camera.transform.position=new Vector3(.2f,.95f,-1);camera.targetTexture=target;
                yield return null;
                camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,160,160),0,0);pixels.Apply();
                int cyan=0;foreach(var pixel in pixels.GetPixels32())if(pixel.g>20 && pixel.b>20)cyan++;
                Assert.Greater(cyan,30,"The strand mesh must produce visible water, not only populated vertex buffers");
                System.IO.Directory.CreateDirectory("Logs/EmberdeepArtV2");
                System.IO.File.WriteAllBytes("Logs/EmberdeepArtV2/WaterStrandPixelProbe-Editor.png",pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=old;camera.targetTexture=null;Object.Destroy(pixels);Object.Destroy(target);
                Object.Destroy(root);Object.Destroy(arena);Object.Destroy(cameraObject);
            }
            yield return null;
        }
    }
}

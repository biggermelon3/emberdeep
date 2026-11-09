using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    public sealed class EmberdeepFlameBaselineTests
    {
        [UnityTest] public IEnumerator FlameCardsStayBroadAndContractAfterAnAcceptedWaterHit()
        {
            var root=new GameObject("Flame baseline geometry probe");var arena=new GameObject("Probe arena");
            var randomState=Random.state;
            try
            {
                Random.InitState(8842);
                var effects=root.AddComponent<FireRescueVfx>();effects.Initialize();
                var run=new FireRun();run.StartStage();var jet=new WaterJet();
                effects.Tick(run,arena.transform,jet,.1f);
                var flames=root.transform.Find("Turbulent flame particles").GetComponent<ParticleSystem>();
                var particles=new ParticleSystem.Particle[1024];int count=flames.GetParticles(particles);
                Assert.Greater(count,0);
                float largest=0;
                for(int i=0;i<count;i++)
                {
                    var size=particles[i].startSize3D;
                    Assert.That(size.y/size.x,Is.InRange(1.25f,2.5f),"Long narrow cards read as rigid spikes at tabletop scale");
                    Assert.Less(size.y,.2f,"One flame card must not dominate the miniature");
                    if(particles[i].randomSeed==1)largest=Mathf.Max(largest,size.y);
                }
                Assert.Greater(largest,0);
                run.ApplyWater(run.Trees[0].Position,100,.1f);effects.Tick(run,arena.transform,jet,.1f);
                count=flames.GetParticles(particles);int cooling=0;
                for(int i=0;i<count;i++)if(particles[i].randomSeed==1)
                {
                    cooling++;
                    Assert.Less(particles[i].startSize3D.y,largest);
                    Assert.LessOrEqual(particles[i].remainingLifetime,.18001f);
                }
                Assert.Greater(cooling,0,"Existing particles must contract, not merely stop spawning");
                Assert.AreEqual(7,root.GetComponentsInChildren<ParticleSystem>().Length);
                Assert.AreEqual(1024,flames.main.maxParticles);
                Assert.AreEqual(384,root.transform.Find("Rolling smoke").GetComponent<ParticleSystem>().main.maxParticles);
            }
            finally{Random.state=randomState;Object.Destroy(root);Object.Destroy(arena);}
            yield return null;
        }

        [UnityTest] public IEnumerator RefinedFlameRetainsTheOriginalSoftAlphaContourInsteadOfAnOpaqueTriangle()
        {
            var cameraObject=new GameObject("Flame contour probe camera");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
            var card=new GameObject("One flame card");card.layer=30;
            var mesh=new Mesh();var target=new RenderTexture(384,256,24);
            var pixels=new Texture2D(384,256,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            Material baseline=null,refined=null;
            try
            {
                mesh.vertices=new[]{new Vector3(-.0325f,-.065f,0),new Vector3(.0325f,-.065f,0),new Vector3(-.0325f,.065f,0),new Vector3(.0325f,.065f,0)};
                mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one};
                mesh.colors=new[]{new Color(1,.4f,.06f,.65f),new Color(1,.4f,.06f,.65f),new Color(1,.4f,.06f,.65f),new Color(1,.4f,.06f,.65f)};
                mesh.triangles=new[]{0,2,1,1,2,3};mesh.RecalculateBounds();
                card.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=card.AddComponent<MeshRenderer>();
                var original=Resources.Load<Shader>("FireRescueParticles");var current=Resources.Load<Shader>("EmberdeepV2Particles");
                Assert.IsNotNull(original);Assert.IsNotNull(current);
                baseline=new Material(original);baseline.SetFloat("_Flame",1);baseline.SetFloat("_DstBlend",1);
                refined=new Material(current);refined.SetFloat("_Style",1);refined.SetFloat("_DstBlend",1);
                camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                camera.orthographic=true;camera.orthographicSize=.09f;camera.nearClipPlane=.01f;camera.farClipPlane=2;
                camera.transform.position=new Vector3(0,0,-.5f);camera.targetTexture=target;
                yield return null;
                System.IO.Directory.CreateDirectory("Logs/EmberdeepFlameFeedback");
                renderer.sharedMaterial=baseline;var originalPixels=Capture(camera,target,pixels);
                System.IO.File.WriteAllBytes("Logs/EmberdeepFlameFeedback/Editor-Flame-OriginalBaseline.png",pixels.EncodeToPNG());
                renderer.sharedMaterial=refined;var refinedPixels=Capture(camera,target,pixels);
                System.IO.File.WriteAllBytes("Logs/EmberdeepFlameFeedback/Editor-Flame-Refined.png",pixels.EncodeToPNG());
                float originalPlateau=ContourPlateau(originalPixels,384,out int originalFootprint);
                float refinedPlateau=ContourPlateau(refinedPixels,384,out int refinedFootprint);
                Assert.Greater(originalFootprint,150);Assert.Greater(refinedFootprint,150,"The fire must remain visibly rendered");
                Assert.That(refinedFootprint/(float)originalFootprint,Is.InRange(.6f,1.6f),"Do not replace the flame with an oversized glow or a thin needle");
                Assert.Less(refinedPlateau,Mathf.Max(.45f,originalPlateau+.15f),"A broad bright plateau with a thin edge is the rejected solid triangle look; preserve a graded alpha contour");
            }
            finally
            {
                RenderTexture.active=previous;camera.targetTexture=null;Object.Destroy(cameraObject);Object.Destroy(card);
                Object.Destroy(mesh);Object.Destroy(target);Object.Destroy(pixels);if(baseline)Object.Destroy(baseline);if(refined)Object.Destroy(refined);
            }
            yield return null;
        }
        static Color32[] Capture(Camera camera,RenderTexture target,Texture2D pixels)
        {
            camera.Render();RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();return pixels.GetPixels32();
        }
        static float ContourPlateau(Color32[] pixels,int width,out int footprint)
        {
            int bright=0;footprint=0;
            // Several cross-sections through the broad body avoid judging a single moving tip.
            for(int y=92;y<=148;y+=8)
            {
                int peak=0;for(int x=0;x<width;x++)peak=Mathf.Max(peak,pixels[y*width+x].r);
                if(peak<10)continue;
                for(int x=0;x<width;x++)
                {
                    int red=pixels[y*width+x].r;
                    if(red>peak*.10f)footprint++;
                    if(red>peak*.85f)bright++;
                }
            }
            return footprint>0?bright/(float)footprint:1;
        }
    }
}

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    // Tilted board (EmberdeepPlayArea, LoopSpec §10): FireRescueVfx emits fire along the board's up and lays the ground
    // discs on the board; a flat board keeps the exact old frame (identity tilt, horizontal billboards).
    public sealed class EmberdeepTiltedVfxPlayTests
    {
        const float Tilt=30;

        static int Burning(FireRun run)
        {
            for(int i=0;i<run.Trees.Length;i++)if(run.Trees[i].State==TreeState.Burning)return i;
            run.Trees[0].State=TreeState.Burning;run.Trees[0].Heat=100;return 0;
        }
        static Vector3 MeanFlameVelocity(GameObject root,int tree)
        {
            var flame=root.transform.Find("Turbulent flame particles").GetComponent<ParticleSystem>();
            var particles=new ParticleSystem.Particle[1024];int n=flame.GetParticles(particles),own=0;var sum=Vector3.zero;
            for(int i=0;i<n;i++)if(particles[i].randomSeed==(uint)(tree+1)){sum+=particles[i].velocity;own++;}
            Assert.Greater(own,10,"the burning tree must emit flame tongues");
            return sum/own;
        }

        [UnityTest] public IEnumerator TiltedBoardFlamesRiseAlongTheBoardAndGroundDiscsLieOnIt()
        {
            var root=new GameObject("Tilted VFX probe");var arena=new GameObject("Tilted probe arena");
            var cameraObject=new GameObject("Tilted VFX camera");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
            var mesh=new Mesh();
            try
            {
                arena.transform.SetPositionAndRotation(new Vector3(0,1,.5f),Quaternion.Euler(-Tilt,20,0));arena.transform.localScale=Vector3.one*FireMiniature.Scale;
                var up=arena.transform.up;
                var effects=root.AddComponent<FireRescueVfx>();effects.Initialize();
                var run=new FireRun();run.StartStage();int tree=Burning(run);var jet=new WaterJet();
                for(int s=0;s<12;s++)effects.Tick(run,arena.transform,jet,.05f);

                Assert.Less(Vector3.Angle(effects.BoardTilt*Vector3.up,up),.01f,"BoardTilt maps world up onto the board's up");
                var rise=MeanFlameVelocity(root,tree);
                Assert.Less(Vector3.Angle(rise,up),10,"flames rise along the board's up");
                Assert.Greater(Vector3.Angle(rise,Vector3.up),Tilt-10,"not along world up");

                Assert.IsTrue(effects.GroundDiscsOnBoard);
                foreach(var name in new[]{"Fire ground glow","Water contact ripples"})
                {
                    var disc=root.transform.Find(name);var r=disc.GetComponent<ParticleSystemRenderer>();
                    Assert.AreEqual(ParticleSystemRenderMode.Billboard,r.renderMode,name);
                    Assert.AreEqual(ParticleSystemRenderSpace.Local,r.alignment,name);
                    Assert.Less(Vector3.Angle(disc.forward,up),.5f,name+" lies on the board (normal = board up)");
                }

                // A real ripple, baked: every vertex lies in the board plane through the contact point.
                var point=arena.transform.TransformPoint(new Vector3(.5f,0,5));
                effects.Impact(new WaterJet.Impact{Point=point,Normal=up,Tree=-1,Amount=1},false);
                var ripples=root.transform.Find("Water contact ripples").GetComponent<ParticleSystem>();
                Assert.Greater(ripples.particleCount,0);
                cameraObject.transform.SetPositionAndRotation(point+up*.3f-arena.transform.forward*.3f,Quaternion.LookRotation(point-(point+up*.3f-arena.transform.forward*.3f)));
                ripples.GetComponent<ParticleSystemRenderer>().BakeMesh(mesh,camera,ParticleSystemBakeMeshOptions.Default);
                Assert.Greater(mesh.vertexCount,0);
                float off=0;var centre=point+up*.003f;
                foreach(var v in mesh.vertices)off=Mathf.Max(off,Mathf.Abs(Vector3.Dot(v-centre,up)));
                Assert.Less(off,.002f,"the ripple quad lies flat on the tilted board (max out-of-plane "+off+" m)");
            }
            finally{Object.Destroy(root);Object.Destroy(arena);Object.Destroy(cameraObject);Object.Destroy(mesh);}
            yield return null;
        }

        [UnityTest] public IEnumerator FlatBoardKeepsTheOldFrameAndSwitchesBackFromTilted()
        {
            var root=new GameObject("Flat VFX probe");var arena=new GameObject("Flat probe arena");
            try
            {
                arena.transform.SetPositionAndRotation(Vector3.zero,Quaternion.Euler(0,40,0));arena.transform.localScale=Vector3.one*FireMiniature.Scale;
                var effects=root.AddComponent<FireRescueVfx>();effects.Initialize();
                var run=new FireRun();run.StartStage();int tree=Burning(run);var jet=new WaterJet();
                for(int s=0;s<12;s++)effects.Tick(run,arena.transform,jet,.05f);
                var q=effects.BoardTilt;
                Assert.AreEqual(Quaternion.identity.x,q.x);Assert.AreEqual(Quaternion.identity.y,q.y);
                Assert.AreEqual(Quaternion.identity.z,q.z);Assert.AreEqual(Quaternion.identity.w,q.w);
                Assert.Less(Vector3.Angle(MeanFlameVelocity(root,tree),Vector3.up),10);
                var glow=root.transform.Find("Fire ground glow");
                Assert.IsFalse(effects.GroundDiscsOnBoard);
                Assert.AreEqual(ParticleSystemRenderMode.HorizontalBillboard,glow.GetComponent<ParticleSystemRenderer>().renderMode);

                arena.transform.rotation=Quaternion.Euler(-Tilt,40,0);effects.Tick(run,arena.transform,jet,.05f);
                Assert.IsTrue(effects.GroundDiscsOnBoard);
                Assert.AreEqual(ParticleSystemRenderMode.Billboard,glow.GetComponent<ParticleSystemRenderer>().renderMode);
                arena.transform.rotation=Quaternion.Euler(0,40,0);effects.Tick(run,arena.transform,jet,.05f);
                Assert.IsFalse(effects.GroundDiscsOnBoard);
                Assert.AreEqual(ParticleSystemRenderMode.HorizontalBillboard,glow.GetComponent<ParticleSystemRenderer>().renderMode);
                Assert.AreEqual(ParticleSystemRenderSpace.View,glow.GetComponent<ParticleSystemRenderer>().alignment);
                Assert.AreEqual(Quaternion.identity,glow.localRotation);
            }
            finally{Object.Destroy(root);Object.Destroy(arena);}
            yield return null;
        }
    }
}

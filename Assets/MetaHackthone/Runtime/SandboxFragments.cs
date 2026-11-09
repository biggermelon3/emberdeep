using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PhysicalAssembly
{
    // Return and reward effects share one bounded, scene-owned pool. No physics debris.
    public sealed class SandboxFragments
    {
        public const int Budget=512;
        public static int ActiveCount { get; private set; }
        static readonly Stack<ParticleSystem> pool=new();
        ParticleSystem system;
        public ParticleSystem.Particle[] Points { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { pool.Clear(); ActiveCount=0; }
        public SandboxFragments(Transform parent,int requested)
        {
            int count=Mathf.Clamp(requested,0,Budget-ActiveCount); ActiveCount+=count;
            Points=new ParticleSystem.Particle[count];
            while(pool.Count>0 && !system) system=pool.Pop();
            if(!system) {
                system=new GameObject("Pooled yellow fragments").AddComponent<ParticleSystem>();
                system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var renderer=system.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial=new Material(Resources.Load<Shader>("SandboxBoundary"));
                renderer.shadowCastingMode=ShadowCastingMode.Off;
                system.gameObject.AddComponent<SandboxParticleResources>().Material=renderer.sharedMaterial;
            }
            // Keep pooled systems at scene root: OnDisable may release them while the
            // owner hierarchy is being destroyed, when Unity forbids reparenting.
            if(system.gameObject.scene!=parent.gameObject.scene)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(system.gameObject,parent.gameObject.scene);
            system.gameObject.SetActive(true);
            var main=system.main; main.loop=false; main.playOnAwake=false; main.maxParticles=Mathf.Max(1,count);
            main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=system.emission; emission.enabled=false;
            system.Play();
        }
        public void Set(int index,Vector3 position,float size,float alpha=1)
        {
            Points[index].position=position; Points[index].startColor=new Color(1,.75f,.03f,alpha);
            Points[index].startSize=size; Points[index].startLifetime=10; Points[index].remainingLifetime=10;
        }
        public void Draw() { if(system) system.SetParticles(Points,Points.Length); }
        public void Clear() { if(system) system.Clear(); }
        public void Release()
        {
            if(Points==null) return;
            ActiveCount=Mathf.Max(0,ActiveCount-Points.Length); Points=null;
            if(!system) return;
            system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            system.gameObject.SetActive(false);
            if(pool.Count<16) pool.Push(system); else Object.Destroy(system.gameObject);
            system=null;
        }
    }
}

using UnityEngine;

namespace FireRescue
{
    // Shared short CC0 recording. Domain-clock scheduling stops with gameplay;
    // two concurrent voices and a flock-wide gap prevent an eight-sheep chorus.
    public sealed class EmberdeepSheepAudio : MonoBehaviour
    {
        FireRun run;
        AudioSource[] voices;
        float[] next;
        bool[] burning;
        bool[] affectionate;
        float lastClock,flockGap;
        readonly System.Random random=new(7241);
        public void Initialize(FireRun source)
        {
            run=source;voices=new AudioSource[run.Animals.Length];next=new float[voices.Length];burning=new bool[voices.Length];affectionate=new bool[voices.Length];
            var clip=Resources.Load<AudioClip>("EmberdeepSheepBleat");
            for(int i=0;i<voices.Length;i++)
            {
                var go=new GameObject("Sheep voice "+i);go.transform.SetParent(transform,false);
                var voice=go.AddComponent<AudioSource>();voice.clip=clip;voice.playOnAwake=false;voice.loop=false;
                voice.spatialBlend=1;voice.rolloffMode=AudioRolloffMode.Linear;voice.minDistance=.35f;voice.maxDistance=5;
                voices[i]=voice;next[i]=3+i*1.7f+(float)random.NextDouble()*4;
            }
        }
        void Update()
        {
            if(run==null || voices.Length==0)return;
            float clock=run.Animals[0].AnimationTime,dt=Mathf.Max(0,clock-lastClock);lastClock=clock;
            if(dt<=0 || AudioListener.pause || AudioListener.volume<=0 || run.Phase==FirePhase.Lost)
            {foreach(var voice in voices)if(voice.isPlaying)voice.Stop();return;}
            flockGap=Mathf.Max(0,flockGap-dt);int playing=0;foreach(var voice in voices)if(voice.isPlaying)playing++;
            for(int i=0;i<voices.Length;i++)
            {
                var a=run.Animals[i];var v=voices[i];v.transform.localPosition=a.Position+Vector3.up*FireRun.AnimalHeight;
                if(a.State==AnimalState.Rescued || a.State==AnimalState.Lost){v.Stop();continue;}
                if(a.OnFire!=burning[i]){next[i]=a.OnFire?0:2+(float)random.NextDouble()*4;burning[i]=a.OnFire;}
                if(a.Affection>0 && !affectionate[i] && !a.OnFire)next[i]=0;
                affectionate[i]=a.Affection>0;
                next[i]-=dt;
                if(next[i]>0 || flockGap>0 || playing>=2 || v.isPlaying)continue;
                v.pitch=a.OnFire?1.7f+(float)random.NextDouble()*.2f:1.05f+(float)random.NextDouble()*.18f;
                v.volume=a.OnFire?.42f:.24f;v.Play();playing++;flockGap=a.OnFire?.22f:.6f;
                next[i]=a.OnFire?.65f+(float)random.NextDouble()*.45f:7+(float)random.NextDouble()*8;
            }
        }
        void OnDisable(){if(voices!=null)foreach(var v in voices)if(v)v.Stop();}
    }
}

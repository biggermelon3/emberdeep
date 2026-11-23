using UnityEngine;

namespace FireRescue
{
    // A sheep that catches fire gives one distinct alarm (ignite whoosh + "baa-BAA!"). After that it only
    // reminds now and then, through ONE voice shared by the whole flock, so a burning flock never becomes a
    // chorus. Calm sheep are near-silent. Fire/calm scheduling runs on the domain clock and stops with
    // gameplay; a pet is an event and plays at once. Clips are baked once from the shared CC0 bleat.
    public sealed class EmberdeepSheepAudio : MonoBehaviour
    {
        // Catching fire: one alarm. A second sheep igniting inside the flock gap skips straight to reminders.
        public const float AlarmVolume=.34f,AlarmPitch=1.55f,AlarmFlockGap=1.2f,AlarmRepeatPerSheep=6f;
        // Still burning: sparse reminders, several seconds per sheep, one voice for the flock.
        public const float FirstReminder=3.5f,ReminderInterval=5.5f,ReminderJitter=1.5f,ReminderFlockGap=2.2f;
        public const float ReminderVolume=.16f,ReminderPitch=1.5f,PitchJitter=.12f,VolumeJitter=.2f;
        // Not burning: one quiet bleat for the whole flock every so often.
        public const float CalmInterval=26f,CalmJitter=10f,CalmVolume=.05f,CalmPitch=1.08f;
        // Petting: a boing at take-off, then a happy bleat. Pitch varies a little per pet.
        public const float PetVolume=.3f,PetPitchJitter=.07f;
        // Baked variants: a short reminder (first 70%) and the alarm's double bleat.
        const float ShortFraction=.7f,AlarmSecondBleatAt=.25f,AlarmSecondBleatRatio=1.15f,PetBleatAt=.11f,PetBleatRatio=1.28f;
        const int SampleRate=22050;

        FireRun run;
        AudioSource voice,pet;
        AudioClip bleat,shortBleat,alarm,happy;
        readonly System.Collections.Generic.List<AudioClip> baked=new();
        float[] nextReminder,lastAlarm;
        bool[] burning;
        float lastClock,flockGap,alarmGap,calmNext,clock,busyUntil;
        int speaker=-1;
        readonly System.Random random=new(7241);
        public int Speaker=>speaker;
        // What has played so far (tests, tuning logs).
        public int AlarmsPlayed {get;private set;}
        public int RemindersPlayed {get;private set;}
        public int CalmPlayed {get;private set;}
        public int PetsPlayed {get;private set;}
        public AudioClip AlarmClip=>alarm;
        public AudioClip PetClip=>happy;

        public void Initialize(FireRun source)
        {
            InitializeVoices();
            run=source;int n=run.Animals.Length;
            nextReminder=new float[n];lastAlarm=new float[n];burning=new bool[n];
            for(int i=0;i<n;i++)lastAlarm[i]=-AlarmRepeatPerSheep;
            calmNext=CalmInterval*.5f+Jitter(CalmJitter);
        }
        // Pet sounds only, for sheep outside a fire run (camp).
        public void InitializeVoices()
        {
            if(voice)return;
            bleat=Resources.Load<AudioClip>("EmberdeepSheepBleat");
            float[] source=Samples(bleat,out int rate);
            if(source!=null)
            {
                shortBleat=Bake("Sheep bleat short",Mix(rate,(Resample(source,rate,1,ShortFraction),0,1)),rate);
                alarm=Bake("Sheep catches fire",Mix(rate,(Whoosh(rate),0,.55f),(Resample(source,rate,1,.45f),.04f,1),(Resample(source,rate,AlarmSecondBleatRatio,1),AlarmSecondBleatAt,1)),rate);
                happy=Bake("Sheep pet boing",Mix(rate,(Boing(rate),0,.75f),(Resample(source,rate,PetBleatRatio,1),PetBleatAt,.7f)),rate);
            }
            else
            {
                // No readable samples: fall back to the plain bleat plus a synthesized boing.
                shortBleat=alarm=bleat;happy=Bake("Sheep pet boing",Boing(SampleRate),SampleRate);
            }
            voice=Voice("Flock voice");pet=Voice("Pet voice");
        }
        AudioSource Voice(string label)
        {
            var go=new GameObject(label);go.transform.SetParent(transform,false);
            var v=go.AddComponent<AudioSource>();v.playOnAwake=false;v.loop=false;
            v.spatialBlend=1;v.rolloffMode=AudioRolloffMode.Linear;v.minDistance=.35f;v.maxDistance=5;v.dopplerLevel=0;
            return v;
        }

        // Plays the happy boing at a sheep (local to this transform). The caller owns the hop and cooldown.
        public void Pet(Vector3 localPosition,float delay=0)
        {
            if(!pet || AudioListener.pause)return;
            pet.transform.localPosition=localPosition;pet.Stop();pet.clip=happy;
            pet.pitch=1+Jitter(PetPitchJitter);pet.volume=PetVolume;PetsPlayed++;
            if(delay>0)pet.PlayDelayed(delay);else pet.Play();
        }

        void Update()=>Tick();
        // One scheduling step on the domain clock (Animals[0].AnimationTime). Public for tests.
        public void Tick()
        {
            if(run==null || voice==null || run.Animals.Length==0 || burning==null || burning.Length!=run.Animals.Length)return;
            float now=run.Animals[0].AnimationTime,dt=Mathf.Max(0,now-lastClock);lastClock=now;
            if(dt<=0 || AudioListener.pause || AudioListener.volume<=0 || run.Phase==FirePhase.Lost){if(voice.isPlaying)voice.Stop();speaker=-1;return;}
            dt=Mathf.Min(dt,.1f);clock+=dt;
            flockGap=Mathf.Max(0,flockGap-dt);alarmGap=Mathf.Max(0,alarmGap-dt);
            bool anyBurning=false;int due=-1;float mostOverdue=0;
            for(int i=0;i<burning.Length;i++)
            {
                var a=run.Animals[i];
                bool onFire=a.OnFire && a.State!=AnimalState.Rescued && a.State!=AnimalState.Lost;
                if(onFire && !burning[i])
                {
                    burning[i]=true;
                    bool alarmed=alarmGap<=0 && clock-lastAlarm[i]>=AlarmRepeatPerSheep;
                    if(alarmed){AlarmsPlayed++;Say(i,alarm,AlarmVolume,AlarmPitch+Jitter(.05f));alarmGap=AlarmFlockGap;flockGap=Mathf.Max(flockGap,ReminderFlockGap);lastAlarm[i]=clock;}
                    nextReminder[i]=FirstReminder+(alarmed?0:Jitter(ReminderJitter)+ReminderJitter);
                }
                else if(!onFire)burning[i]=false;
                if(!burning[i])continue;
                anyBurning=true;nextReminder[i]-=dt;
                if(nextReminder[i]<=mostOverdue){mostOverdue=nextReminder[i];due=i;}
            }
            if(speaker>=0 && speaker<run.Animals.Length && voice.isPlaying)voice.transform.localPosition=Mouth(run.Animals[speaker]);
            bool busy=clock<busyUntil;
            if(due>=0 && flockGap<=0 && !busy)
            {
                RemindersPlayed++;Say(due,random.NextDouble()<.5?bleat:shortBleat,ReminderVolume*(1+Jitter(VolumeJitter)),ReminderPitch+Jitter(PitchJitter));
                nextReminder[due]=ReminderInterval+Jitter(ReminderJitter);flockGap=ReminderFlockGap;
            }
            if(anyBurning){calmNext=Mathf.Max(calmNext,CalmInterval*.5f);return;}
            calmNext-=dt;
            if(calmNext>0 || clock<busyUntil)return;
            calmNext=CalmInterval+Jitter(CalmJitter);
            int start=random.Next(run.Animals.Length);
            for(int k=0;k<run.Animals.Length;k++)
            {
                int i=(start+k)%run.Animals.Length;var a=run.Animals[i];
                if(a.State==AnimalState.Lost || a.HeldBy>=0)continue;
                CalmPlayed++;Say(i,shortBleat,CalmVolume,CalmPitch+Jitter(PitchJitter));break;
            }
        }
        void Say(int sheep,AudioClip clip,float volume,float pitch)
        {
            speaker=sheep;voice.Stop();voice.clip=clip;voice.volume=volume;voice.pitch=pitch;
            busyUntil=clock+(clip?clip.length/Mathf.Max(.1f,pitch):0);
            voice.transform.localPosition=Mouth(run.Animals[sheep]);voice.Play();
        }
        static Vector3 Mouth(FireRun.Animal a)=>a.Position+Vector3.up*FireRun.AnimalHeight;
        float Jitter(float range)=>((float)random.NextDouble()*2-1)*range;
        void OnDisable(){if(voice)voice.Stop();if(pet)pet.Stop();speaker=-1;}
        void OnDestroy(){foreach(var c in baked)if(c){if(Application.isPlaying)Destroy(c);else DestroyImmediate(c);}baked.Clear();}

        // ---- Baking --------------------------------------------------------------------------------
        static float[] Samples(AudioClip clip,out int rate)
        {
            rate=SampleRate;
            if(!clip)return null;
            if(clip.loadState!=AudioDataLoadState.Loaded)clip.LoadAudioData();
            if(clip.loadState!=AudioDataLoadState.Loaded || clip.channels!=1)return null;
            var data=new float[clip.samples];
            if(!clip.GetData(data,0))return null;
            rate=clip.frequency;return data;
        }
        // Linear resample by ratio (>1 = higher and shorter), keeping the first fraction with a 25 ms fade out.
        static float[] Resample(float[] source,int rate,float ratio,float fraction)
        {
            int length=Mathf.Max(1,Mathf.FloorToInt(source.Length*fraction/ratio));
            var result=new float[length];int fade=Mathf.Max(1,Mathf.Min(length/4,Mathf.RoundToInt(.025f*rate)));
            for(int i=0;i<length;i++)
            {
                float at=i*ratio;int a=Mathf.Min(source.Length-1,(int)at),b=Mathf.Min(source.Length-1,a+1);
                result[i]=Mathf.Lerp(source[a],source[b],at-a)*(fraction<1?Mathf.Clamp01((length-1-i)/(float)fade):1);
            }
            return result;
        }
        static float[] Mix(int rate,params (float[] samples,float at,float gain)[] parts)
        {
            int length=0;foreach(var p in parts)length=Mathf.Max(length,Mathf.RoundToInt(p.at*rate)+p.samples.Length);
            var result=new float[length];
            foreach(var p in parts){int start=Mathf.RoundToInt(p.at*rate);for(int i=0;i<p.samples.Length;i++)result[start+i]+=p.samples[i]*p.gain;}
            for(int i=0;i<length;i++)result[i]=Mathf.Clamp(result[i],-.95f,.95f);
            return result;
        }
        AudioClip Bake(string label,float[] samples,int rate)
        {
            var clip=AudioClip.Create(label,samples.Length,1,rate,false);clip.SetData(samples,0);baked.Add(clip);return clip;
        }
        // Low "fwoomp" of catching fire: filtered noise swell over a falling thump.
        static float[] Whoosh(int rate)
        {
            int count=Mathf.RoundToInt(.32f*rate);var s=new float[count];uint seed=90731u;float filtered=0,phase=0;
            for(int i=0;i<count;i++)
            {
                float t=i/(float)rate;seed=seed*1664525u+1013904223u;float noise=((seed>>8)/16777215f)*2-1;
                filtered=Mathf.Lerp(filtered,noise,.09f+.25f*Mathf.Clamp01(t/.12f));
                phase+=2*Mathf.PI*Mathf.Lerp(95,48,t/.32f)/rate;
                float envelope=Mathf.Clamp01(t/.018f)*Mathf.Exp(-t*9);
                s[i]=(filtered*1.6f+Mathf.Sin(phase)*.45f)*envelope;
            }
            return s;
        }
        // Cartoon spring: a rising twang with a decaying wobble.
        static float[] Boing(int rate)
        {
            int count=Mathf.RoundToInt(.42f*rate);var s=new float[count];float phase=0;
            for(int i=0;i<count;i++)
            {
                float t=i/(float)rate;
                float f=230*(1+.95f*(1-Mathf.Exp(-t*9)))*(1+.12f*Mathf.Sin(2*Mathf.PI*15*t)*Mathf.Exp(-t*5));
                phase+=2*Mathf.PI*f/rate;
                float envelope=Mathf.Clamp01(t/.004f)*Mathf.Exp(-t*6.5f)*Mathf.Clamp01((count-1-i)/(.012f*rate));
                s[i]=(Mathf.Sin(phase)*.62f+Mathf.Sin(phase*2)*.14f)*envelope;
            }
            return s;
        }
    }
}

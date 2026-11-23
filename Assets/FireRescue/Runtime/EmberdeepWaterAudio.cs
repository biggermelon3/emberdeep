using UnityEngine;

namespace FireRescue
{
    // Water-loop sounds (LoopSpec §3), placeholders synthesized once (gameplay never allocates audio data). Separate from
    // FireFeedback's spray voices:
    // - pump strike: a thunk on every strike, plus a clean "good pump" ping on the beat or a rusty creak off it
    //   (the strike that starts a beat, and every strike with the rhythm toggle off, is the thunk alone);
    // - the gulp when a splash lands in the gem;
    // - the gem filling: a glassy tone whose pitch climbs with the fill level, loud while the pump fills it and only a
    //   faint shimmer during the slow passive refill, and a chime when it reaches full;
    // - the dry sputter of an empty gem.
    public sealed class EmberdeepWaterAudio : MonoBehaviour
    {
        const int Rate=22050;
        // Fill tone: a .5 s loop of 440 Hz (+ octave, 6 Hz shimmer) that loops seamlessly; pitch spans FillPitch over 0..1.
        public const float FillLoopSeconds=.5f,FillVolume=.12f,FillRateForFull=.35f,FullAt=.995f,FullRearm=.9f;
        public static readonly Vector2 FillPitch=new(.7f,1.6f);
        AudioClip thunk,gulp,goodPump,creak,sputter,fillTone,fullChime;
        AudioSource strike,land,bonus,dry,fill;
        float lastFill=-1;bool fullLatched;
        public bool Sputtering=>dry && dry.isPlaying && dry.volume>.001f;
        public int Gulps {get;private set;}
        // On-beat strikes ("good pump"), off-beat strikes (creak), full-gem chimes.
        public int Chimes {get;private set;}
        public int Creaks {get;private set;}
        public int FullChimes {get;private set;}
        public bool FillToneAudible=>fill && fill.isPlaying && fill.volume>.001f;
        public float FillTonePitch=>fill?fill.pitch:0;
        public float FillToneVolume=>fill?fill.volume:0;
        public void Initialize()
        {
            if(strike)return;
            thunk=Clip("Pump thunk",.12f,Thunk);gulp=Clip("Gem gulp",.22f,Gulp);
            goodPump=Clip("Good pump",.34f,GoodPump);creak=Clip("Rusty pump creak",.32f,Creak);
            sputter=Clip("Dry gem sputter",.64f,Sputter);
            fillTone=Clip("Gem filling tone",FillLoopSeconds,FillTone);fullChime=Clip("Gem full chime",.9f,FullChime);
            strike=Voice("Pump strike");land=Voice("Gem gulp");bonus=Voice("Pump beat grade");
            dry=Voice("Gem sputter");dry.loop=true;dry.clip=sputter;
            fill=Voice("Gem filling");fill.loop=true;fill.clip=fillTone;
        }
        AudioSource Voice(string name)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);
            var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.dopplerLevel=0;source.volume=0;source.priority=110;
            return source;
        }
        public void Strike(bool onBeat)=>Strike(onBeat?EmberdeepPumpGrade.OnBeat:EmberdeepPumpGrade.Plain);
        public void Strike(EmberdeepPumpGrade grade)
        {
            if(!strike)return;
            strike.pitch=grade==EmberdeepPumpGrade.OnBeat?1.1f:grade==EmberdeepPumpGrade.OffBeat?.86f:.95f+Random.value*.1f;
            strike.PlayOneShot(thunk,.32f);
            if(grade==EmberdeepPumpGrade.OnBeat){bonus.pitch=1;bonus.PlayOneShot(goodPump,.26f);Chimes++;}
            else if(grade==EmberdeepPumpGrade.OffBeat){bonus.pitch=.94f+Random.value*.12f;bonus.PlayOneShot(creak,.22f);Creaks++;}
        }
        public void Gulp(bool onBeat)
        {
            if(!land)return;
            land.pitch=onBeat?1.18f:.92f+Random.value*.14f;land.PlayOneShot(gulp,onBeat?.3f:.24f);Gulps++;
        }
        // Once per frame with the gem's shown fill (0..1). A jump (level start, reset) is a snap, not a fill: no sound.
        public void Fill(float fill01,float dt)
        {
            if(!fill)return;
            fill01=float.IsFinite(fill01)?Mathf.Clamp01(fill01):0;
            dt=float.IsFinite(dt)?Mathf.Clamp(dt,0,.1f):0;
            float rate=0;
            if(lastFill<0 || Mathf.Abs(fill01-lastFill)>.25f)fullLatched=fill01>=FullAt;
            else if(dt>0)rate=(fill01-lastFill)/dt;
            lastFill=fill01;
            float target=rate>0?Mathf.Clamp01(rate/FillRateForFull)*FillVolume:0;
            fill.volume=Mathf.MoveTowards(fill.volume,target,dt*(target>fill.volume?1.5f:.6f));
            fill.pitch=Mathf.Lerp(FillPitch.x,FillPitch.y,fill01);
            if(fill.volume>.001f){if(!fill.isPlaying)fill.Play();}else if(fill.isPlaying)fill.Stop();
            if(fill01<FullRearm)fullLatched=false;
            if(!fullLatched && rate>0 && fill01>=FullAt){fullLatched=true;bonus.pitch=1;bonus.PlayOneShot(fullChime,.24f);FullChimes++;}
        }
        // The next Fill call starts fresh (the gem was reset or snapped full).
        public void ResetFill(){lastFill=-1;if(fill){fill.volume=0;fill.Stop();}}
        public void Tick(bool sputtering,float dt)
        {
            if(!dry)return;
            float step=float.IsFinite(dt)?Mathf.Clamp(dt,0,.1f)*3:0;
            dry.volume=Mathf.MoveTowards(dry.volume,sputtering?.2f:0,step);
            if(dry.volume>.001f){if(!dry.isPlaying)dry.Play();}else if(dry.isPlaying)dry.Stop();
        }
        public void Silence(){if(dry){dry.volume=0;dry.Stop();}ResetFill();}
        delegate float Wave(float t,System.Random random);
        static AudioClip Clip(string name,float seconds,Wave wave)
        {
            int count=Mathf.CeilToInt(seconds*Rate);var data=new float[count];var random=new System.Random(name.Length*7919);
            for(int i=0;i<count;i++)data[i]=Mathf.Clamp(wave(i/(float)Rate,random),-1,1);
            var clip=AudioClip.Create(name,count,1,Rate,false);clip.SetData(data,0);return clip;
        }
        static float Noise(System.Random r)=>(float)(r.NextDouble()*2-1);
        static float Thunk(float t,System.Random r)
        {
            float body=Mathf.Sin(2*Mathf.PI*(170*t-120*t*t))*Mathf.Exp(-t*38);
            return body*.8f+Noise(r)*Mathf.Exp(-t*260)*.35f;
        }
        static float Gulp(float t,System.Random r)
        {
            // Two rising bubbles: the splash lands, then the gem swallows.
            float a=t<.11f?Mathf.Sin(2*Mathf.PI*(260*t+1500*t*t))*Mathf.Sin(Mathf.PI*t/.11f):0;
            float u=t-.08f;
            float b=u>0 && u<.12f?Mathf.Sin(2*Mathf.PI*(340*u+1800*u*u))*Mathf.Sin(Mathf.PI*u/.12f)*.7f:0;
            return (a+b)*.75f+Noise(r)*Mathf.Exp(-t*90)*.08f;
        }
        // On the beat: a clean, well-oiled "ka-ching" (a short low click, then two bright pure notes a fifth apart).
        static float GoodPump(float t,System.Random r)
        {
            float click=Mathf.Sin(2*Mathf.PI*900*t)*Mathf.Exp(-t*160)*.4f;
            float u=t-.035f;
            float ping=u<0?0:(Mathf.Sin(2*Mathf.PI*1046.5f*u)*.5f+Mathf.Sin(2*Mathf.PI*1568f*u)*.32f*Mathf.Exp(-u*4))*Mathf.Exp(-u*7.5f)*Mathf.Min(1,u*500);
            return click+ping;
        }
        // Off the beat: a rusty hinge. A scratchy sawtooth whose pitch wobbles and sags, with grit, swelling then dying.
        static float Creak(float t,System.Random r)
        {
            float f=330-170*t/.32f+28*Mathf.Sin(2*Mathf.PI*23*t)+12*Mathf.Sin(2*Mathf.PI*61*t);
            float phase=2*Mathf.PI*(330*t-265.6f*t*t)+28/23f*(1-Mathf.Cos(2*Mathf.PI*23*t))+12/61f*(1-Mathf.Cos(2*Mathf.PI*61*t));
            float saw=0;for(int k=1;k<=6;k++)saw+=Mathf.Sin(k*phase)/k;
            float env=Mathf.Sin(Mathf.PI*Mathf.Clamp01(t/.32f))*Mathf.Clamp01(t*60);
            float grit=Noise(r)*(.25f+.25f*Mathf.Abs(Mathf.Sin(2*Mathf.PI*f*.5f*t)));
            return (saw*.33f+grit*.35f)*env*.8f;
        }
        // 220 cycles of 440 Hz and 3 shimmer cycles in .5 s: the loop has no seam.
        static float FillTone(float t,System.Random r)
        {
            float w=2*Mathf.PI*440*t;
            return (Mathf.Sin(w)*.55f+Mathf.Sin(2*w)*.18f+Mathf.Sin(3*w)*.06f)*(.85f+.15f*Mathf.Sin(2*Mathf.PI*6*t));
        }
        // Full: a quick glassy arpeggio C6 E6 G6 C7 with a ringing tail.
        static float FullChime(float t,System.Random r)
        {
            float sum=0;float[] notes={1046.5f,1318.5f,1568f,2093f};
            for(int i=0;i<notes.Length;i++)
            {
                float u=t-i*.06f;if(u<0)continue;
                sum+=(Mathf.Sin(2*Mathf.PI*notes[i]*u)+.25f*Mathf.Sin(2*Mathf.PI*notes[i]*2.01f*u)*Mathf.Exp(-u*8))*Mathf.Exp(-u*(i==3?3.2f:6f))*Mathf.Min(1,u*600)*.3f;
            }
            return sum;
        }
        static float Sputter(float t,System.Random r)
        {
            // Irregular short spits, quiet gaps between them.
            float[] at={.02f,.09f,.13f,.26f,.31f,.44f,.52f,.57f};
            float sum=0;
            foreach(var start in at)
            {
                float u=t-start;if(u<0 || u>.035f)continue;
                sum+=Noise(r)*Mathf.Sin(Mathf.PI*u/.035f)*.6f+Mathf.Sin(2*Mathf.PI*520*u)*Mathf.Exp(-u*90)*.25f;
            }
            return sum;
        }
        void OnDisable()=>Silence();
        void OnDestroy(){foreach(var c in new[]{thunk,gulp,goodPump,creak,sputter,fillTone,fullChime})if(c)Destroy(c);}
    }
}

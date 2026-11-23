using UnityEngine;

namespace FireRescue
{
    // Four fixed voices. Clips are synthesized once; gameplay never allocates audio data
    // or adds AudioSources. Hand tracking has no controller rumble, so pitch and short
    // material sounds communicate pressure, cooling and successful extinguishes.
    public sealed class FireFeedback : MonoBehaviour
    {
        const int SampleRate=22050;
        enum Sound { Flow, Pressure, Impact, Launch, Rescue, Complete, Failure, Alarm }
        AudioSource flow, pressure, contact, accent;
        AudioClip[] clips;
        float impactClock, rescueClock;
        int pendingCombo;
        bool wasSpraying;
        // End-of-level stings: one clear fanfare, one heavy loss. Durations are the clip lengths.
        public const float ClearStingVolume=.32f,LossStingVolume=.36f,ClearStingSeconds=1.35f,LossStingSeconds=1.7f;
        // Striking the start bell (FireRescueGame.StrikeBell): a placeholder fire-alarm ring on the accent voice.
        public const float AlarmVolume=.26f,AlarmSeconds=1.25f;
        public int Alarms {get;private set;}
        // In-world outcome moments (banner, board light, burn meter). Only with a FireRescueGame alongside.
        public EmberdeepOutcomeView Outcome {get;private set;}

        public void Initialize()
        {
            if(flow)return;
            clips=new AudioClip[8];
            clips[(int)Sound.Flow]=Synthesize(Sound.Flow,1.2f);
            clips[(int)Sound.Pressure]=Synthesize(Sound.Pressure,.5f);
            clips[(int)Sound.Impact]=Synthesize(Sound.Impact,.17f);
            clips[(int)Sound.Launch]=Synthesize(Sound.Launch,.28f);
            clips[(int)Sound.Rescue]=Synthesize(Sound.Rescue,.34f);
            clips[(int)Sound.Complete]=Synthesize(Sound.Complete,ClearStingSeconds);
            clips[(int)Sound.Failure]=Synthesize(Sound.Failure,LossStingSeconds);
            clips[(int)Sound.Alarm]=Synthesize(Sound.Alarm,AlarmSeconds);
            flow=Voice("Water flow",true,clips[(int)Sound.Flow]);
            pressure=Voice("Pressure rising",true,clips[(int)Sound.Pressure]);
            contact=Voice("Cooling and water impacts",false,null);
            accent=Voice("Water cannon and rescue cues",false,null);
            var game=GetComponent<FireRescueGame>();
            if(game && !Outcome)
            {
                Outcome=new GameObject("Outcome moments").AddComponent<EmberdeepOutcomeView>();
                Outcome.transform.SetParent(transform,false);Outcome.Initialize(game);
            }
        }
        AudioSource Voice(string name,bool loop,AudioClip clip)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);
            var source=go.AddComponent<AudioSource>();
            source.playOnAwake=false;source.loop=loop;source.clip=clip;
            source.spatialBlend=0;source.volume=0;source.priority=loop?160:100;
            source.dopplerLevel=0;return source;
        }
        public void Tick(bool spraying,float charge,FireDelivery delivery,float dt)
        {
            if(!flow)return;
            if(float.IsNaN(dt) || float.IsInfinity(dt) || dt<=0)return;
            dt=Mathf.Min(dt,.1f);charge=Mathf.Clamp01(charge);
            impactClock=Mathf.Max(0,impactClock-dt);rescueClock=Mathf.Max(0,rescueClock-dt);
            bool continuous=spraying && delivery!=FireDelivery.Charge && delivery!=FireDelivery.Pulse;
            float volume=continuous?(delivery==FireDelivery.Fan?.15f:.11f):0;
            flow.pitch=delivery==FireDelivery.Foam?.82f:1.1f;
            Loop(flow,volume,dt*1.2f);
            pressure.pitch=.75f+charge*1.05f;
            Loop(pressure,charge>0?.055f+.065f*charge:0,dt*2);
            if(delivery==FireDelivery.Pulse && spraying && !wasSpraying && !accent.isPlaying)
                Play(accent,Sound.Launch,.10f,1.8f);
            wasSpraying=spraying;
            if(pendingCombo>0 && rescueClock<=0)
            {
                // Multi-tree splashes become one rising note instead of a pile of voices.
                Play(accent,Sound.Rescue,.2f,Mathf.Pow(2,(Mathf.Min(pendingCombo,6)-1)*2/12f));
                pendingCombo=0;rescueClock=.11f;
            }
        }
        static void Loop(AudioSource voice,float target,float step)
        {
            voice.volume=Mathf.MoveTowards(voice.volume,target,step);
            if(voice.volume>.001f){if(!voice.isPlaying)voice.Play();}
            else if(voice.isPlaying)voice.Stop();
        }
        void Play(AudioSource voice,Sound sound,float volume,float pitch=1)
        {
            voice.Stop();voice.clip=clips[(int)sound];voice.pitch=pitch;voice.volume=volume;voice.Play();
        }
        public void Impact(bool hot)
        {
            if(!contact || impactClock>0)return;
            impactClock=hot?.13f:.22f;
            Play(contact,Sound.Impact,hot?.16f:.07f,hot?1.1f:.8f);
        }
        public void Launch(float charge)
        {
            if(!accent)return;
            charge=Mathf.Clamp01(charge);pressure.Stop();pressure.volume=0;
            Play(accent,Sound.Launch,.20f+.08f*charge,1.15f-.3f*charge);
            rescueClock=.12f;
        }
        public void Rescue(int combo)
        {if(accent)pendingCombo=Mathf.Max(pendingCombo,Mathf.Max(1,combo));}
        public void Complete(bool won)
        {
            if(!accent)return;
            pendingCombo=0;rescueClock=1;
            flow.Stop();flow.volume=0;pressure.Stop();pressure.volume=0;
            Play(accent,won?Sound.Complete:Sound.Failure,won?ClearStingVolume:LossStingVolume);
        }
        public void Alarm()
        {
            if(!accent)return;
            pendingCombo=0;Play(accent,Sound.Alarm,AlarmVolume);Alarms++;
        }
        public void Silence()
        {
            if(flow){flow.Stop();flow.volume=0;}
            if(pressure){pressure.Stop();pressure.volume=0;}
            if(contact)contact.Stop();if(accent)accent.Stop();
            impactClock=rescueClock=0;pendingCombo=0;wasSpraying=false;
        }

        static AudioClip Synthesize(Sound sound,float duration)
        {
            int count=Mathf.CeilToInt(duration*SampleRate);
            var samples=new float[count];uint random=71327u+(uint)sound*98413u;
            float filtered=0, phase=0;
            for(int i=0;i<count;i++)
            {
                float t=i/(float)SampleRate, u=t/duration;
                random=random*1664525u+1013904223u;
                float noise=((random>>8)/16777215f)*2-1;
                filtered=Mathf.Lerp(filtered,noise,.13f);
                float value;
                switch(sound)
                {
                    case Sound.Flow:
                        value=filtered*.75f+Mathf.Sin(t*2*Mathf.PI*125)*.018f;
                        break;
                    case Sound.Pressure:
                        value=(Mathf.Sin(t*2*Mathf.PI*160)*.19f+Mathf.Sin(t*2*Mathf.PI*320)*.055f)*(1+.08f*Mathf.Sin(t*2*Mathf.PI*8));
                        break;
                    case Sound.Impact:
                        value=(noise*.17f+filtered*.6f)*Mathf.Exp(-u*3.5f);
                        break;
                    case Sound.Launch:
                        phase+=2*Mathf.PI*Mathf.Lerp(145,48,u)/SampleRate;
                        value=(Mathf.Sin(phase)*.55f+filtered*.55f)*Mathf.Exp(-u*5);
                        break;
                    case Sound.Rescue:
                        value=(Mathf.Sin(t*2*Mathf.PI*660)*.25f+Mathf.Sin(t*2*Mathf.PI*990)*.1f)*Mathf.Exp(-u*5);
                        break;
                    case Sound.Complete:
                        // Rising C / E / G, then a ringing C-E-G-C chord with a soft sparkle on top.
                        value=Note(t,0,523.25f)+Note(t,.11f,659.25f)+Note(t,.22f,783.99f)
                            +Bell(t,.36f,1046.5f,.2f)+Bell(t,.36f,783.99f,.12f)+Bell(t,.36f,659.25f,.1f)+Bell(t,.36f,523.25f,.1f)
                            +noise*.03f*Mathf.Clamp01((t-.36f)/.02f)*Mathf.Exp(-Mathf.Max(0,t-.36f)*5);
                        break;
                    case Sound.Alarm:
                    {
                        // A brass fire-alarm bell: the clapper strikes 17 times a second (alternate sides a little softer),
                        // each strike rings inharmonic partials over the bell's own hum, then it stops and rings out.
                        const float hits=17,stop=.85f;
                        float since=t<stop?t%(1/hits):t-(Mathf.Floor(stop*hits)/hits);
                        int n=Mathf.FloorToInt(Mathf.Min(t,stop)*hits);
                        float strike=(Mathf.Sin(2*Mathf.PI*1150*since)*.5f+Mathf.Sin(2*Mathf.PI*2420*since)*.3f+Mathf.Sin(2*Mathf.PI*3830*since)*.15f)
                            *Mathf.Exp(-since*(t<stop?32:7))*(n%2==0?1:.8f);
                        float hum=(Mathf.Sin(2*Mathf.PI*1150*t)*.22f+Mathf.Sin(2*Mathf.PI*2420*t)*.1f)*(t<stop?1:Mathf.Exp(-(t-stop)*6));
                        value=(strike*.6f+hum*.4f+noise*.02f*Mathf.Exp(-since*200))*.9f;
                        break;
                    }
                    default:
                        // Loss: a low thud, then a falling minor line (G - Eb - C) over a smoky rumble.
                        float thud=Mathf.Sin(2*Mathf.PI*Mathf.Lerp(90,42,Mathf.Clamp01(t/.25f))*t)*.4f*Mathf.Exp(-t*9);
                        float rumble=filtered*.5f*Mathf.Clamp01(t/.15f)*Mathf.Exp(-t*1.6f);
                        value=thud+rumble+Low(t,.05f,196f)+Low(t,.42f,155.56f)+Low(t,.8f,130.81f)*1.3f;
                        break;
                }
                float edge=Mathf.Min(Mathf.Clamp01(t/.006f),Mathf.Clamp01((duration-t)/.012f));
                // Loop clips meet at zero. One-shots have a fast attack and no clicks.
                samples[i]=Mathf.Clamp(value*edge,-.8f,.8f);
            }
            var clip=AudioClip.Create("Fire rescue "+sound,count,1,SampleRate,false);
            clip.SetData(samples,0);return clip;
        }
        // A ringing tone with a long tail.
        static float Bell(float time,float start,float frequency,float gain)
        {
            float t=time-start;if(t<0)return 0;
            return (Mathf.Sin(t*frequency*2*Mathf.PI)+.3f*Mathf.Sin(t*frequency*4*Mathf.PI)*Mathf.Exp(-t*6))*gain*Mathf.Exp(-t*2.6f)*Mathf.Clamp01(t/.01f);
        }
        // A soft, hollow low note (odd harmonics) that swells and fades.
        static float Low(float time,float start,float frequency)
        {
            float t=time-start;if(t<0)return 0;float w=t*frequency*2*Mathf.PI;
            return (Mathf.Sin(w)+Mathf.Sin(3*w)*.22f+Mathf.Sin(5*w)*.08f)*.2f*Mathf.Clamp01(t/.04f)*Mathf.Exp(-t*2.4f);
        }
        static float Note(float time,float start,float frequency)
        {
            float t=time-start;if(t<0)return 0;
            return Mathf.Sin(t*frequency*2*Mathf.PI)*.22f*Mathf.Exp(-t*8)*Mathf.Clamp01(t/.008f);
        }
        void OnDisable(){Silence();}
        void OnDestroy()
        {
            Silence();if(clips==null)return;
            foreach(var clip in clips)if(clip)Destroy(clip);
        }
    }
}

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace FireRescue
{
    // The camp tutorial's forest spirit (owner, 2026-11-22): a small blue glowing orb (bright core, soft glow and halo,
    // fairy motes circling it, a sparkle trail and falling dust) that comes out of the tent, hovers bobbing over the
    // island, and says its lines with a subtitle under it. Presentation only: EmberdeepCampPlay decides when
    // (EmberdeepSpiritDirector) and where (home = the tent door, hover).
    // Voice: two AudioSources so a line's reverb tail rings on under the next; its own volume is SOUND, and every other
    // sound ducks while it speaks (EmberdeepAudioSettings.SetDuck). The line clock, not the AudioSource, decides when a
    // line is said, so a missing clip, a silent test run or a paused headset never stalls the tutorial.
    // Subtitles: the English card is translated through the camp's EmberdeepLocalizedText (Translations.tsv rows), then
    // wrapped (balanced lines; CJK by characters).
    // Owner's headset test (2026-11-22). Three changes:
    // - The cards ran ahead of the voice. The line clock now follows the playing AudioSource's own position (frame
    //   clamps and hitches no longer drift it), and the card times were re-measured (EmberdeepSpiritLines).
    // - Chained lines had no gap. After each line the spirit keeps quiet for its PauseAfter (Busy) before the next.
    // - The subtitles were hard to read over the campfire and the sky. The band is near-opaque with a wide solid core,
    //   and the letters have a dark outline and drop shadow (Outline/Shadow copies drawn first: sorting order).
    [DefaultExecutionOrder(110)]
    public sealed class EmberdeepForestSpirit : MonoBehaviour
    {
        public enum State { Hidden, Arriving, Hovering, Leaving }
        // Tests: lines run this many times faster than real time, silent. 1 = real time with audio.
        public static float VoiceSpeed=1;
        public const float ArriveSeconds=1.8f,LeaveSeconds=1.1f,SubtitleHold=.6f;
        public const float BobHeight=.012f,BobRate=1.7f;
        // First capture (2026-11-22): at .0075/.03/.065 the orb read as a white speck against the pale camp sky. Bigger,
        // with a deep-blue body behind the glow so it holds against bright backgrounds as well as dark ones.
        public const float CoreRadius=.011f,InnerRadius=.022f,GlowRadius=.044f,BodyRadius=.05f,HaloRadius=.095f,MoteOrbit=.05f;  // m
        public const float SubtitleBelow=.11f,TextSize=.0125f,PanelPad=.016f;   // m
        // Letter outline (4 diagonal copies) and drop shadow, metres in the text's plane; drawn in this order.
        public const float OutlineOffset=.0007f,ShadowOffset=.0016f;
        public const int BandOrder=20,OutlineOrder=21,TextOrder=22;
        public const int WrapLatin=36,WrapCjk=15;
        public const float Duck=.6f,DuckAttack=.15f,DuckRelease=.5f,VoiceBlend=.35f;
        const int Motes=4,TrailSamples=18,MaxDust=40;
        static readonly Color BodyTint=new(.03f,.16f,.42f,.42f),HaloTint=new(.2f,.52f,1f,.2f),GlowTint=new(.3f,.72f,1f,.55f),
            InnerTint=new(.58f,.9f,1f,.9f),CoreTint=new(1f,1f,1f,1f),MoteTint=new(.7f,.95f,1f,1f),
            PanelTint=new(.006f,.018f,.028f,.95f),TextTint=new(.9f,.97f,1f),OutlineTint=new(0,0,0,.9f),ShadowTint=new(0,0,0,.75f);

        public State Phase {get;private set;}
        public EmberdeepSpiritLine Line {get;private set;}
        public float LineTime {get;private set;}
        public bool Speaking=>Line!=null;
        // Speaking, or keeping quiet for the line's PauseAfter: no new line starts.
        public bool Busy=>Line!=null || pause>0;
        public float PauseLeft=>pause;
        public bool Visible=>Phase!=State.Hidden;
        // The subtitle card on show (English source text), or null.
        public string Card {get;private set;}
        public string SubtitleShown=>subtitle && subtitle.gameObject.activeInHierarchy?subtitle.text:null;
        public Transform SubtitleRoot=>subtitleRoot;
        public Vector3 Position {get;private set;}
        public float Size {get;private set;}
        public float Level {get;private set;} // speech level 0..1: the orb swells with the voice
        public int LinesStarted {get;private set;}
        public bool Rendered=>orbFx!=null && orbFx.Renderer.enabled;

        EmberdeepFxBuilder orbFx,panelFx;
        Material bandMaterial;
        Transform subtitleRoot,voiceHost;
        TextMesh subtitle;
        readonly TextMesh[] backing=new TextMesh[5]; // 4 outline copies, then the drop shadow
        EmberdeepLocalizedText localized;
        readonly AudioSource[] sources=new AudioSource[2];
        int source;
        readonly Dictionary<string,AudioClip> clips=new();
        readonly float[] samples=new float[256];
        readonly List<Vector3> trail=new(),ribbon=new();
        struct Dust{public Vector3 At,Velocity;public float Age,Life,Size;}
        readonly List<Dust> dust=new();
        float phaseTime,clock,hold,alpha,dustDebt,pause;
        AudioClip lineClip;
        Vector3 leaveFrom;
        string shownCard,shownLocale;
        bool paused,built;

        void Build()
        {
            if(built)return;built=true;
            var orbHost=new GameObject("Forest spirit glow");orbHost.transform.SetParent(transform,false);
            orbFx=new EmberdeepFxBuilder(orbHost,"Forest spirit glow");orbFx.Material.renderQueue=3002;
            var panelHost=new GameObject("Forest spirit subtitle backing");panelHost.transform.SetParent(transform,false);
            panelFx=new EmberdeepFxBuilder(panelHost,"Forest spirit subtitle backing");
            // Drawn over the scene like the text it backs (EmberdeepSubtitleBand: the FX band with depth ignored).
            var band=Resources.Load<Shader>("EmberdeepSubtitleBand");
            if(band){bandMaterial=new Material(band){name="Forest spirit subtitle band",renderQueue=2999};panelFx.Renderer.sharedMaterial=bandMaterial;}
            else panelFx.Material.renderQueue=2995;
            panelFx.Renderer.sortingOrder=BandOrder;
            subtitleRoot=new GameObject("Forest spirit subtitle").transform;subtitleRoot.SetParent(transform,false);
            var text=new GameObject("Spirit subtitle text");text.transform.SetParent(subtitleRoot,false);
            subtitle=text.AddComponent<TextMesh>();subtitle.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.GetComponent<MeshRenderer>().sharedMaterial=subtitle.font.material;
            subtitle.fontSize=80;subtitle.characterSize=TextSize*20/80;subtitle.anchor=TextAnchor.MiddleCenter;
            subtitle.alignment=TextAlignment.Center;subtitle.color=TextTint;subtitle.lineSpacing=1.05f;subtitle.text="";
            text.GetComponent<MeshRenderer>().sortingOrder=TextOrder;
            for(int i=0;i<backing.Length;i++)
            {
                bool shadow=i==backing.Length-1;
                var go=new GameObject(shadow?"Spirit subtitle shadow":"Spirit subtitle outline "+(i+1));go.transform.SetParent(subtitleRoot,false);
                var offset=shadow?new Vector2(ShadowOffset,-ShadowOffset):new Vector2(i%2==0?-1:1,i<2?-1:1)*OutlineOffset;
                go.transform.localPosition=new Vector3(offset.x,offset.y,.0006f); // just behind the letters
                var t=go.AddComponent<TextMesh>();t.font=subtitle.font;go.GetComponent<MeshRenderer>().sharedMaterial=subtitle.font.material;
                t.fontSize=subtitle.fontSize;t.characterSize=subtitle.characterSize;t.anchor=subtitle.anchor;t.alignment=subtitle.alignment;
                t.lineSpacing=subtitle.lineSpacing;t.color=shadow?ShadowTint:OutlineTint;t.text="";
                go.GetComponent<MeshRenderer>().sortingOrder=OutlineOrder;backing[i]=t;
            }
            subtitleRoot.gameObject.SetActive(false);
            voiceHost=new GameObject("Forest spirit voice").transform;voiceHost.SetParent(transform,false);
            for(int i=0;i<sources.Length;i++)
            {
                var s=voiceHost.gameObject.AddComponent<AudioSource>();s.playOnAwake=false;s.loop=false;s.priority=0;
                s.spatialBlend=VoiceBlend;s.dopplerLevel=0;s.ignoreListenerVolume=true;s.rolloffMode=AudioRolloffMode.Linear;
                s.minDistance=1;s.maxDistance=20;sources[i]=s;
            }
            orbFx.Renderer.enabled=false;panelFx.Renderer.enabled=false;
        }
        void Awake()=>Build();

        // ---- Presence ----------------------------------------------------------------------------------------------
        public void Appear(){Build();Phase=State.Arriving;phaseTime=0;trail.Clear();dust.Clear();alpha=0;}
        public void Leave()
        {
            if(Phase==State.Hidden || Phase==State.Leaving)return;
            leaveFrom=Position;Phase=State.Leaving;phaseTime=0;
        }
        public void Hide(){Phase=State.Hidden;Silence();dust.Clear();trail.Clear();HideRenderers();}

        // ---- Voice -------------------------------------------------------------------------------------------------
        public void Say(EmberdeepSpiritLine line)
        {
            Build();if(line==null)return;
            Line=line;LineTime=0;hold=0;pause=0;LinesStarted++;Card=line.CardAt(0);lineClip=null;
            if(VoiceSpeed!=1)return;
            var clip=Clip(line);if(!clip)return;
            source=(source+1)%sources.Length;var s=sources[source];
            s.Stop();s.clip=clip;s.volume=EmberdeepAudioSettings.Sfx;s.Play();if(paused)s.Pause();lineClip=clip;
        }
        // Stops at once (SKIP, leaving the camp).
        public void Silence()
        {
            Line=null;Card=null;hold=0;Level=0;pause=0;lineClip=null;
            foreach(var s in sources)if(s){s.Stop();s.clip=null;}
            if(subtitleRoot)subtitleRoot.gameObject.SetActive(false);
        }
        AudioClip Clip(EmberdeepSpiritLine line)
        {
            if(!clips.TryGetValue(line.Id,out var clip)){clip=Resources.Load<AudioClip>(line.Resource);clips[line.Id]=clip;}
            return clip;
        }
        public bool HasClip(EmberdeepSpiritLine line)=>Clip(line);

        // ---- Frame -------------------------------------------------------------------------------------------------
        // shown: the camp set is on screen. live: the camp takes input (placed, tracked, focused); the voice clock and the
        // audio hold while it is not. home/hover: world points; up: the camp's up.
        public void Tick(float dt,bool shown,bool live,Camera cam,Vector3 home,Vector3 hover,Vector3 up)
        {
            Build();
            dt=Mathf.Clamp(dt,0,.1f);
            TickVoice(live?dt:0,live);
            TickDuck(dt);
            if(!shown || !cam || Phase==State.Hidden){HideRenderers();return;}
            clock+=dt;
            Move(dt,home,hover,up,cam);
            voiceHost.position=Position;
            RenderOrb(dt,cam,up);
            RenderSubtitle(cam,up);
        }
        void TickVoice(float dt,bool live)
        {
            bool hush=!live;
            if(hush!=paused)
            {
                paused=hush;
                foreach(var s in sources)if(s && s.clip){if(hush)s.Pause();else s.UnPause();}
            }
            foreach(var s in sources)if(s)s.volume=EmberdeepAudioSettings.Sfx;
            if(Line!=null)
            {
                // The clip's own playback position while it plays (the cards were measured on the clip); the frame clock
                // only when there is no audio (tests, a missing clip) or after the clip has ended.
                var s=sources[source];
                if(lineClip && s && s.clip==lineClip && s.isPlaying)LineTime=Mathf.Max(LineTime,s.timeSamples/(float)lineClip.frequency);
                else LineTime+=dt*VoiceSpeed;
                Card=Line.CardAt(LineTime);
                if(LineTime>=Line.DoneAt){pause=Line.PauseAfter;Line=null;hold=SubtitleHold;}
            }
            else
            {
                if(pause>0)pause=Mathf.Max(0,pause-dt*VoiceSpeed);
                if(hold>0){hold-=dt;if(hold<=0)Card=null;}
            }
            // The orb swells with the voice: the playing source's level, or a stand-in pulse when silent (tests).
            float target=0;
            if(Line!=null)
            {
                if(VoiceSpeed==1 && sources[source] && sources[source].isPlaying)
                {
                    sources[source].GetOutputData(samples,0);float sum=0;foreach(var v in samples)sum+=v*v;
                    float db=20*Mathf.Log10(Mathf.Sqrt(sum/samples.Length)+1e-6f);target=Mathf.Clamp01((db+42)/30);
                }
                else if(VoiceSpeed!=1)target=.5f+.3f*Mathf.Sin(LineTime*14);
            }
            Level=Mathf.MoveTowards(Level,target,dt*(target>Level?12:4));
        }
        float duck=1;
        void TickDuck(float dt)
        {
            float target=Speaking?Duck:1;
            duck=Mathf.MoveTowards(duck,target,dt*(1-Duck)/(target<duck?DuckAttack:DuckRelease));
            EmberdeepAudioSettings.SetDuck(duck);
        }
        void Move(float dt,Vector3 home,Vector3 hover,Vector3 up,Camera cam)
        {
            phaseTime+=dt;
            var right=Vector3.ProjectOnPlane(cam.transform.right,up).normalized;
            var bob=hover+up*(Mathf.Sin(clock*BobRate)*BobHeight)+right*(Mathf.Sin(clock*.9f)*.004f);
            switch(Phase)
            {
                case State.Arriving:
                {
                    float s=Mathf.Clamp01(phaseTime/ArriveSeconds),e=Mathf.SmoothStep(0,1,s);
                    // Out of the door, up in a lazy curve, settling into the bob.
                    var mid=Vector3.Lerp(home,hover,.25f)+up*Vector3.Distance(home,hover)*.55f-right*.06f;
                    Position=Bezier(home,mid,bob,e);Size=Mathf.Lerp(.2f,1,Mathf.SmoothStep(0,1,s*1.6f));alpha=Mathf.Clamp01(s*4);
                    if(s>=1)Phase=State.Hovering;
                    break;
                }
                case State.Hovering:Position=bob;Size=1;alpha=1;break;
                case State.Leaving:
                {
                    float s=Mathf.Clamp01(phaseTime/LeaveSeconds),e=s*s;
                    var mid=Vector3.Lerp(leaveFrom,home,.4f)+up*.1f;
                    Position=Bezier(leaveFrom,mid,home,e);Size=Mathf.Lerp(1,.25f,e);alpha=1-Mathf.SmoothStep(.55f,1,s);
                    if(s>=1){Hide();return;}
                    break;
                }
            }
            trail.Insert(0,Position);if(trail.Count>TrailSamples)trail.RemoveAt(trail.Count-1);
        }
        static Vector3 Bezier(Vector3 a,Vector3 b,Vector3 c,float t)=>Vector3.Lerp(Vector3.Lerp(a,b,t),Vector3.Lerp(b,c,t),t);

        void RenderOrb(float dt,Camera cam,Vector3 up)
        {
            var eye=cam.transform.position;var toEye=(eye-Position).normalized;
            var right=Vector3.Cross(up,toEye).normalized;if(right.sqrMagnitude<.5f)right=cam.transform.right;
            var vup=Vector3.Cross(toEye,right);
            float pulse=1+.06f*Mathf.Sin(clock*3.1f);float speak=Level;
            orbFx.Renderer.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);orbFx.Renderer.transform.localScale=Vector3.one;
            orbFx.Begin();
            // Sparkle trail while it travels.
            float travelled=0;for(int i=1;i<trail.Count;i++)travelled+=Vector3.Distance(trail[i-1],trail[i]);
            if(travelled>.01f && trail.Count>2)orbFx.Ribbon(trail,trail.Count,eye,.014f*Size,Fade(GlowTint,.8f),.2f);
            // Falling fairy dust: a few always, a stream while moving.
            dustDebt+=dt*(travelled>.01f?28:5);
            while(dustDebt>=1 && dust.Count<MaxDust)
            {
                dustDebt-=1;var r=Random.insideUnitSphere;
                dust.Add(new Dust{At=Position+r*.012f*Size,Velocity=r*.012f-up*.018f,Life=Random.Range(.8f,1.5f),Size=Random.Range(.0028f,.0055f)});
            }
            if(dustDebt>=1)dustDebt=0;
            for(int i=dust.Count-1;i>=0;i--)
            {
                var d=dust[i];d.Age+=dt;d.At+=d.Velocity*dt;d.Velocity*=1-dt*.8f;
                if(d.Age>=d.Life){dust.RemoveAt(i);continue;}
                dust[i]=d;float k=1-d.Age/d.Life;float twinkle=.55f+.45f*Mathf.Sin(d.Age*23+i*1.7f);
                orbFx.Dot(d.At,right,vup,d.Size*(.5f+.5f*k),Fade(MoteTint,k*twinkle*.8f));
            }
            // Body and glow layers, outside in, swelling with the voice.
            orbFx.Dot(Position,right,vup,HaloRadius*Size*pulse*(1+.18f*speak),Fade(HaloTint,1+.6f*speak));
            orbFx.Dot(Position,right,vup,BodyRadius*Size*pulse,Fade(BodyTint,1));
            orbFx.Dot(Position,right,vup,GlowRadius*Size*pulse*(1+.25f*speak),Fade(GlowTint,1+.4f*speak));
            orbFx.Dot(Position,right,vup,InnerRadius*Size*(1+.2f*speak),Fade(InnerTint,1));
            orbFx.Dot(Position,right,vup,CoreRadius*Size*(1+.3f*speak),Fade(CoreTint,1));
            // Fairy motes circling on a tipped ring, each with a short tail.
            var ringA=right;var ringB=(up*.45f+toEye*-.9f).normalized;
            for(int m=0;m<Motes;m++)
            {
                float speed=2.2f+.45f*m,phase=m*Mathf.PI*2/Motes,radius=MoteOrbit*(.85f+.25f*(m%2))*Size;
                ribbon.Clear();
                for(int k=0;k<6;k++)
                {
                    float a=(clock-k*.035f)*speed+phase;
                    ribbon.Add(Position+(ringA*Mathf.Cos(a)+ringB*Mathf.Sin(a))*radius+up*(Mathf.Sin(a*2+m)*.004f));
                }
                float twinkle=.6f+.4f*Mathf.Sin(clock*7+m*2.1f);
                orbFx.Ribbon(ribbon,ribbon.Count,eye,.0022f*Size,Fade(MoteTint,.6f*twinkle),.3f);
                orbFx.Dot(ribbon[0],right,vup,.0055f*Size,Fade(MoteTint,twinkle));
            }
            orbFx.End();orbFx.Renderer.enabled=true;
        }
        Color Fade(Color c,float k){c.a=Mathf.Clamp01(c.a*k*alpha);return c;}

        void RenderSubtitle(Camera cam,Vector3 up)
        {
            bool show=Card!=null && Phase!=State.Leaving;
            if(!show){subtitleRoot.gameObject.SetActive(false);panelFx.Renderer.enabled=false;return;}
            if(!localized)localized=GetComponentInParent<EmberdeepLocalizedText>();
            string locale=localized?localized.LocaleCode:"";
            if(Card!=shownCard || locale!=shownLocale)
            {
                shownCard=Card;shownLocale=locale;
                subtitle.text=Wrap(localized?localized.Translate(Card):Card);
                foreach(var t in backing)if(t)t.text=subtitle.text;
            }
            subtitleRoot.gameObject.SetActive(true);
            var at=Position-up*SubtitleBelow;
            // Measure the text square to the world (its renderer bounds are then its own extents), then turn it to the eye.
            subtitleRoot.SetPositionAndRotation(at,Quaternion.identity);
            var bounds=subtitle.GetComponent<MeshRenderer>().bounds;var offset=bounds.center-at;
            subtitleRoot.rotation=Quaternion.LookRotation(at-cam.transform.position,up);
            // A dark band just behind the text, sized to it: EmberdeepSubtitleBand is solid to 55% of its half height, so at
            // 1.6x the text's height every line sits on the solid part (first capture: the old band faded from 20% and
            // the outer lines sat on bare scene).
            float halfW=bounds.extents.x+PanelPad,halfH=bounds.extents.y+PanelPad*.8f;
            var c=at+subtitleRoot.rotation*offset-(cam.transform.position-at).normalized*.002f;
            var r=subtitleRoot.right;
            panelFx.Renderer.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);panelFx.Renderer.transform.localScale=Vector3.one;
            panelFx.Begin();ribbon.Clear();
            for(int i=0;i<=10;i++)ribbon.Add(c+r*Mathf.Lerp(-halfW-halfH,halfW+halfH,i/10f));
            var tint=PanelTint;tint.a*=alpha;
            panelFx.Ribbon(ribbon,ribbon.Count,cam.transform.position,halfH*1.6f,tint,.12f);
            panelFx.End();panelFx.Renderer.enabled=true;
        }
        // Balanced lines: as many as the width needs, each about as long as the others. CJK breaks between characters,
        // never before closing punctuation.
        public static string Wrap(string text)
        {
            if(string.IsNullOrEmpty(text))return text;
            bool cjk=false;foreach(char ch in text)if(ch>=0x2E80){cjk=true;break;}
            if(cjk)
            {
                int lines=Mathf.CeilToInt(text.Length/(float)WrapCjk),width=Mathf.CeilToInt(text.Length/(float)lines);
                var sb=new StringBuilder();int n=0;
                for(int i=0;i<text.Length;i++)
                {
                    sb.Append(text[i]);n++;
                    if(n>=width && i+1<text.Length && !Closing(text[i+1])){sb.Append('\n');n=0;}
                }
                return sb.ToString();
            }
            var words=text.Split(' ');
            int count=Mathf.CeilToInt(text.Length/(float)WrapLatin);int target=Mathf.CeilToInt(text.Length/(float)count)+3;
            var outText=new StringBuilder();int line=0;
            foreach(var w in words)
            {
                if(line>0 && line+1+w.Length>target){outText.Append('\n');line=0;}
                else if(line>0){outText.Append(' ');line++;}
                outText.Append(w);line+=w.Length;
            }
            return outText.ToString();
        }
        static bool Closing(char c)=>"，。？！、：；…）》」』,.?!:;)".IndexOf(c)>=0;

        void HideRenderers()
        {
            if(orbFx!=null)orbFx.Renderer.enabled=false;
            if(panelFx!=null)panelFx.Renderer.enabled=false;
            if(subtitleRoot)subtitleRoot.gameObject.SetActive(false);
        }
        void OnDisable(){duck=1;EmberdeepAudioSettings.SetDuck(1);HideRenderers();}
        void OnDestroy(){EmberdeepAudioSettings.SetDuck(1);orbFx?.Destroy();panelFx?.Destroy();if(bandMaterial)Destroy(bandMaterial);}
    }
}

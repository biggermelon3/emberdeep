using UnityEngine;
using UnityEngine.Rendering;

namespace FireRescue
{
    // LoopSpec §6 "Plan phase look: time stopped" (workstream L3), spell flow only.
    // - Entering Plan: the board goes to a whitish chalk grade, fire and smoke freeze mid-motion (paused particles and
    //   a stopped shader clock), the board's ambience drops to a low hum.
    // - The fire resuming (bell -> countdown -> Fighting): colour floods back from the board centre with a whoosh and
    //   the crackle of the fire comes up.
    // All board-local: three shader globals read by the V2 surface, particle and creek shaders (no post pass, Quest 2),
    // plus FireRescueVfx.SetFrozen and a stopped environment clock. Pieces, sheep, the rim UI and the forecast rings use
    // other shaders, so they stay in colour: what you can touch in Plan is what keeps its colour.
    // Also owns the rim hourglass (§11b Plan): next to the bell, its sand frozen mid-fall, running during the countdown.
    [DefaultExecutionOrder(10040)]
    public sealed class EmberdeepPlanLook : MonoBehaviour
    {
        public static readonly int ClockId=Shader.PropertyToID("_EmberdeepClock"),ClockOnId=Shader.PropertyToID("_EmberdeepClockOn");
        public static readonly int GradeId=Shader.PropertyToID("_EmberdeepPlanGrade"),FloodId=Shader.PropertyToID("_EmberdeepPlanFlood");
        FireRescueGame game;
        float clock,grade,floodClock=-1,stopClock=-1;bool wasPlanning;
        AudioSource hum,crackle,oneShot;
        Transform hourglass,sandTop,sandBottom,sandStream;Renderer glass;Material clay,veil;MaterialPropertyBlock block;
        int hourglassVersion=-1;

        public bool Planning {get;private set;}
        public float Grade=>grade;
        // World-space flood front (m); 0 while time is stopped, growing from the board centre after Start.
        public float FloodRadius {get;private set;}
        public bool Flooding=>floodClock>=0;
        public float HumVolume=>hum?hum.volume:0;
        public float CrackleVolume=>crackle?crackle.volume:0;
        public int Whooshes {get;private set;}
        public Transform Hourglass=>hourglass;

        public void Initialize(FireRescueGame owner)
        {
            game=owner;block=new MaterialPropertyBlock();
            hum=Voice("Plan hum",true,HumClip,0);crackle=Voice("Fire crackle bed",true,CrackleClip,0);oneShot=Voice("Plan whoosh",false,null,1);
        }
        AudioSource Voice(string name,bool loop,AudioClip clip,float volume)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);
            var s=go.AddComponent<AudioSource>();s.playOnAwake=false;s.loop=loop;s.clip=clip;s.volume=volume;s.spatialBlend=.35f;s.dopplerLevel=0;
            if(loop)s.Play();return s;
        }
        // Domain reload or a crash in Play must never leave the editor's board grey or its clock stopped.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetGlobals(){Shader.SetGlobalFloat(ClockOnId,0);Shader.SetGlobalFloat(GradeId,0);Shader.SetGlobalVector(FloodId,Vector4.zero);}

        void LateUpdate()
        {
            if(!game || !game.Arena || game.Effects==null)return;
            var run=game.Run;float dt=Mathf.Min(Time.unscaledDeltaTime,.1f);
            bool live=run.SpellMode && run.Trees.Length>0 && game.Arena.gameObject.activeInHierarchy;
            Planning=live && run.Phase==FirePhase.Preparing;
            game.Effects.SetFrozen(Planning);
            if(!Planning)clock+=dt;
            Shader.SetGlobalFloat(ClockOnId,live?1:0);Shader.SetGlobalFloat(ClockId,clock);
            if(Planning && !wasPlanning){stopClock=0;floodClock=-1;}
            if(!Planning && wasPlanning && run.Phase==FirePhase.Fighting){floodClock=0;Whooshes++;if(oneShot)oneShot.PlayOneShot(WhooshClip,EmberdeepPieceTuning.WhooshVolume);}
            wasPlanning=Planning;
            // Time stopping eases in (a level loading in Plan starts fully graded); the flood eases out from the centre.
            if(Planning){stopClock+=dt;grade=EmberdeepPieceTuning.Grade*Mathf.Clamp01(stopClock/.35f);if(stopClock<.05f)grade=EmberdeepPieceTuning.Grade;FloodRadius=0;}
            else if(floodClock>=0)
            {
                floodClock+=dt;float t=Mathf.Clamp01(floodClock/EmberdeepPieceTuning.FloodSeconds);
                FloodRadius=EmberdeepPieceTuning.FloodReach*(1-(1-t)*(1-t));grade=EmberdeepPieceTuning.Grade;
                if(t>=1){floodClock=-1;grade=0;FloodRadius=0;}
            }
            else{grade=0;FloodRadius=0;}
            if(!live)grade=0;
            var centre=game.Arena.TransformPoint(FieldCentre(run));
            Shader.SetGlobalFloat(GradeId,grade);Shader.SetGlobalVector(FloodId,new Vector4(centre.x,centre.y,centre.z,FloodRadius));
            // Ambience: a low hum while time is stopped; the fire's crackle (scaled by what burns) while it runs.
            float humTarget=Planning?EmberdeepPieceTuning.HumVolume:0;
            float crackleTarget=live && run.Phase==FirePhase.Fighting?EmberdeepPieceTuning.CrackleVolume*Mathf.Clamp01((run.Burning+run.GrassBurning*.5f)/6f):0;
            if(hum){hum.volume=Mathf.MoveTowards(hum.volume,humTarget,dt*(Planning?.4f:.8f));hum.transform.position=centre;}
            if(crackle){crackle.volume=Mathf.MoveTowards(crackle.volume,crackleTarget,dt*.25f);crackle.transform.position=centre;}
            TickHourglass(run,dt);
        }
        static Vector3 FieldCentre(FireRun run)
        {
            float z=0;foreach(var t in run.Trees)z+=t.Position.z;
            return new Vector3(0,0,run.Trees.Length>0?z/run.Trees.Length:6);
        }

        // ---- Hourglass (rim, next to the bell) ------------------------------------------------------------------------
        const float GlassHeight=.064f,GlassRadius=.017f,PlanSand=.62f; // m (x rim UI scale)
        void TickHourglass(FireRun run,float dt)
        {
            var hud=game.RimHud;bool show=Planning && hud && hud.Built && hud.BellVisible;
            if(!show){if(hourglass && hourglass.gameObject.activeSelf)hourglass.gameObject.SetActive(false);return;}
            if(!hourglass)BuildHourglass();
            if(!hourglass.gameObject.activeSelf)hourglass.gameObject.SetActive(true);
            if(hourglassVersion!=hud.BoardVersion+(EmberdeepWaterHand.Left?0:100000) || hourglass.parent!=game.Arena)
            {
                hourglassVersion=hud.BoardVersion+(EmberdeepWaterHand.Left?0:100000);
                var layout=EmberdeepRimHud.LayoutFor(hud.Board,EmberdeepWaterHand.Left?1:-1);
                hourglass.SetParent(game.Arena,false);
                hourglass.localPosition=layout.Hourglass; // the token's corner (the bell floats over the field since 2026-11-22)
                hourglass.localScale=Vector3.one*layout.UnitsPerMetre;
            }
            var cam=game.Player?game.Player.ViewCamera:Camera.main;
            if(cam)
            {
                var flat=Vector3.ProjectOnPlane(cam.transform.position-hourglass.position,game.Arena.up);
                if(flat.sqrMagnitude>1e-6f)hourglass.rotation=Quaternion.LookRotation(-flat,game.Arena.up);
            }
            // Plan: sand frozen mid-fall. Countdown: it runs out with the count.
            bool counting=run.CountdownRemaining>=0;
            float top=counting?PlanSand*Mathf.Clamp01(run.CountdownRemaining/3f):PlanSand;
            float h=GlassHeight*.5f;
            sandTop.localScale=new Vector3(GlassRadius*1.5f,h*.8f,GlassRadius*1.5f)*Mathf.Sqrt(Mathf.Max(.02f,top));
            sandTop.localPosition=Vector3.up*(h*1.45f-(1-top)*h*.35f);
            float bottom=1-top+.2f;
            sandBottom.localScale=new Vector3(GlassRadius*1.6f,h*.7f*bottom,GlassRadius*1.6f);
            sandBottom.localPosition=Vector3.up*(h*.08f+h*.35f*bottom);
            sandStream.gameObject.SetActive(top>.02f);
            // Frozen: the stream holds still; counting: it trickles.
            float wobble=counting?.85f+.3f*Mathf.PerlinNoise(Time.unscaledTime*9,0):1;
            sandStream.localScale=new Vector3(.0016f*wobble,h*.42f,.0016f*wobble);
        }
        void BuildHourglass()
        {
            var tutorial=Resources.Load<Shader>("EmberdeepTutorial");var veilShader=Resources.Load<Shader>("EmberdeepOutcomeVeil");
            clay=tutorial?new Material(tutorial){name="Hourglass clay"}:new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            veil=veilShader?new Material(veilShader){name="Hourglass glass"}:null;
            hourglass=new GameObject("Rim hourglass").transform;hourglass.SetParent(game.Arena,false);
            float h=GlassHeight*.5f;var wood=new Color(.46f,.29f,.15f);var sand=new Color(.95f,.78f,.45f);
            Part("Hourglass base",PrimitiveType.Cylinder,Vector3.up*.002f,new Vector3(.04f,.002f,.04f),wood);
            Part("Hourglass cap",PrimitiveType.Cylinder,Vector3.up*(GlassHeight+.006f),new Vector3(.04f,.002f,.04f),wood);
            for(int i=0;i<3;i++)
            {
                float a=i*Mathf.PI*2/3;
                Part("Hourglass post",PrimitiveType.Cylinder,new Vector3(Mathf.Cos(a)*.017f,h+.004f,Mathf.Sin(a)*.017f),new Vector3(.003f,h,.003f),wood);
            }
            sandTop=Part("Sand top",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.01f,sand).transform;
            sandBottom=Part("Sand bottom",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.01f,sand).transform;
            sandStream=Part("Sand stream",PrimitiveType.Cylinder,Vector3.up*(h+.004f),Vector3.one*.002f,sand).transform;
            if(veil)
            {
                veil.SetColor("_Color",new Color(.85f,.95f,1,.2f));
                for(int i=0;i<2;i++)
                {
                    var bulb=Part(i==0?"Glass top":"Glass bottom",PrimitiveType.Sphere,Vector3.up*(i==0?h*1.5f+.004f:h*.5f+.004f),new Vector3(GlassRadius*2,h*1.05f,GlassRadius*2),Color.white);
                    glass=bulb.GetComponent<Renderer>();glass.sharedMaterial=veil;glass.SetPropertyBlock(null);
                }
            }
        }
        GameObject Part(string name,PrimitiveType type,Vector3 at,Vector3 scale,Color color)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(hourglass,false);go.transform.localPosition=at;go.transform.localScale=scale;
            var c=go.GetComponent<Collider>();c.enabled=false;Destroy(c);
            var r=go.GetComponent<Renderer>();r.sharedMaterial=clay;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            block.Clear();block.SetColor("_BaseColor",color);r.SetPropertyBlock(block);return go;
        }

        // ---- Audio, synthesized once ----------------------------------------------------------------------------------
        static AudioClip humClip,crackleClip,whooshClip;
        static AudioClip HumClip=>humClip?humClip:humClip=Loop("Plan hum",4,t=>
            (Mathf.Sin(2*Mathf.PI*55*t)*.55f+Mathf.Sin(2*Mathf.PI*82.5f*t)*.25f+Mathf.Sin(2*Mathf.PI*110.5f*t)*.12f)*(.75f+.25f*Mathf.Sin(2*Mathf.PI*.5f*t))*.5f);
        static AudioClip CrackleClip
        {
            get
            {
                if(crackleClip)return crackleClip;
                var random=new System.Random(7);const int rate=22050;int n=rate*3;var data=new float[n];float hiss=0;
                for(int i=0;i<n;i++)
                {
                    hiss=hiss*.92f+(float)(random.NextDouble()*2-1)*.08f;data[i]=hiss*.35f;
                    // Sparse pops of a few milliseconds each.
                    if(random.NextDouble()<.0009){int len=random.Next(40,180);float amp=.3f+(float)random.NextDouble()*.5f;
                        for(int k=0;k<len && i+k<n;k++)data[i+k]+=amp*(float)(random.NextDouble()*2-1)*Mathf.Exp(-k/(len*.25f));}
                }
                crackleClip=AudioClip.Create("Fire crackle bed",n,1,rate,false);crackleClip.SetData(data,0);return crackleClip;
            }
        }
        // A rising, filtered rush of air: time starting again.
        static AudioClip WhooshClip
        {
            get
            {
                if(whooshClip)return whooshClip;
                var random=new System.Random(11);const int rate=44100;int n=Mathf.CeilToInt(rate*1.2f);var data=new float[n];float low=0,band=0;
                for(int i=0;i<n;i++)
                {
                    float t=i/(float)rate,u=t/1.2f;float cutoff=Mathf.Lerp(.02f,.35f,Mathf.Sin(u*Mathf.PI*.5f));
                    float noise=(float)(random.NextDouble()*2-1);low+=cutoff*(noise-low);band+=cutoff*(low-band);
                    float envelope=Mathf.Clamp01(t*5)*Mathf.Pow(1-u,1.6f);
                    data[i]=(low-band)*envelope*2.2f+Mathf.Sin(2*Mathf.PI*(70+90*u)*t)*envelope*.12f;
                }
                whooshClip=AudioClip.Create("Time resumes whoosh",n,1,rate,false);whooshClip.SetData(data,0);return whooshClip;
            }
        }
        static AudioClip Loop(string name,float seconds,System.Func<float,float> wave)
        {
            const int rate=22050;int n=Mathf.CeilToInt(seconds*rate);var data=new float[n];
            for(int i=0;i<n;i++)data[i]=wave(i/(float)rate);
            var clip=AudioClip.Create(name,n,1,rate,false);clip.SetData(data,0);return clip;
        }

        void OnDisable(){ResetGlobals();if(game && game.Effects)game.Effects.SetFrozen(false);}
        void OnDestroy()
        {
            ResetGlobals();
            if(hourglass)Destroy(hourglass.gameObject);if(clay)Destroy(clay);if(veil)Destroy(veil);
        }
    }
}

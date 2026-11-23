using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FireRescue
{
    // LoopSpec §3.4 pump beside the player: hammer the cap with a fist, and each strike throws
    // a splash from the spout into the gem. Real metres. Local +x points inward (toward the
    // board's centre line), +z away from the player. Presentation only: FireRescueGame decides
    // strikes and credits the gem; this view never changes water.
    public sealed class EmberdeepHammerPumpView : MonoBehaviour
    {
        public const float CapRadius=.04f, CapTopHeight=.272f;
        static readonly Color Cyan=new(.30f,.86f,.95f), Gold=new(1f,.78f,.32f), Cream=new(.94f,.88f,.75f), Brass=new(.78f,.60f,.30f);
        public Transform Cap {get;private set;}
        public Transform Spout {get;private set;}
        public bool Authored {get;private set;}
        public bool Glowing {get;private set;}
        public bool RhythmVisible {get;private set;}
        public int SplashCount=>splashes.Count;
        public int Strikes {get;private set;}
        // The single rhythm cue (EmberdeepWaterTuning.BeatTargetRing/BeatApproachRing), metres; 0 while hidden.
        public float TargetRadius {get;private set;}
        public float ApproachRadius {get;private set;}
        // Feedback after a strike: the grade and what is left of its flash (1 .. 0).
        public EmberdeepPumpGrade LastGrade {get;private set;}
        public float Flash=>flash;
        // Rest position of the cap's top face: the strike target never moves with the plunger.
        public Vector3 CapTop=>transform.position+transform.up*CapTopHeight;
        public Vector3 Up=>transform.up;
        // Units still in the air; the gem only shows them once they land.
        public float InFlightUnits {get;private set;}
        public event System.Action<float,bool> SplashLanded;
        struct Splash {public float Age,Units;public bool OnBeat;public Vector3 Start;}
        readonly List<Splash> splashes=new();
        readonly List<Vector3> path=new();
        Transform plunger;
        Renderer capRenderer,capBand;
        MaterialPropertyBlock block;
        EmberdeepFxBuilder fx;
        float plunge,flash,clock;
        static readonly Color Rust=new(.72f,.36f,.22f),Pale=new(.78f,.95f,1f);
        bool built;
        GameObject fxHost;
        public void Build(Material body,Material dark,Material water,EmberdeepArtLibrary art)
        {
            if(built)return;built=true;block=new MaterialPropertyBlock();
            var pump=art?art.Create("WellPump",transform,new Vector3(.13f,0,-.065f)):null;
            Authored=pump;
            if(pump)
            {
                pump.name="Hammer pump body";
                foreach(var r in pump.GetComponentsInChildren<Renderer>(true)){r.shadowCastingMode=ShadowCastingMode.Off;if(r.name.StartsWith("Gauge"))r.enabled=false;}
            }
            else
            {
                Part("Pump plinth",PrimitiveType.Cylinder,transform,new Vector3(0,.012f,0),new Vector3(.17f,.012f,.17f),body,new Color(.43f,.50f,.46f));
                Part("Pump casing",PrimitiveType.Cylinder,transform,new Vector3(0,.11f,0),new Vector3(.09f,.095f,.09f),body,new Color(.13f,.43f,.39f));
                var spout=Part("Pump spout",PrimitiveType.Cylinder,transform,new Vector3(.075f,.15f,0),new Vector3(.024f,.05f,.024f),body,new Color(.13f,.43f,.39f));
                spout.localRotation=Quaternion.Euler(0,0,-75);
            }
            Spout=new GameObject("Spout mouth").transform;Spout.SetParent(transform,false);Spout.localPosition=new Vector3(.142f,.142f,-.044f);
            plunger=new GameObject("Hammer plunger").transform;plunger.SetParent(transform,false);
            Part("Plunger rod",PrimitiveType.Cylinder,plunger,new Vector3(0,.226f,0),new Vector3(.014f,.03f,.014f),dark,new Color(.18f,.20f,.20f));
            Cap=Part("Hammer cap",PrimitiveType.Sphere,plunger,new Vector3(0,CapTopHeight-.014f,0),new Vector3(CapRadius*2,.028f,CapRadius*2),body,Cream);
            capRenderer=Cap.GetComponent<Renderer>();
            capBand=Part("Hammer cap band",PrimitiveType.Cylinder,plunger,new Vector3(0,CapTopHeight-.019f,0),new Vector3(CapRadius*2.05f,.004f,CapRadius*2.05f),body,Brass).GetComponent<Renderer>();
            // Kept outside the pump hierarchy: hand contact and the sky outline read the pump's own meshes only.
            fxHost=new GameObject("Pump glow, beat ring and splash");fxHost.transform.SetParent(transform.parent,false);var host=fxHost;
            fx=new EmberdeepFxBuilder(host,"Emberdeep pump effects");
            Render(0,null,default,false,false,false,0,false);
        }
        // Spout points inward: a pump on the player's right turns to face left.
        public void SetSide(bool left,Quaternion arenaRotation)
        {
            transform.rotation=arenaRotation*Quaternion.Euler(0,left?0:180,0);
        }
        public void Strike(float units,bool onBeat)=>Strike(units,onBeat?EmberdeepPumpGrade.OnBeat:EmberdeepPumpGrade.Plain);
        public void Strike(float units,EmberdeepPumpGrade grade)
        {
            Strikes++;plunge=EmberdeepWaterTuning.PlungerTravel;flash=1;LastGrade=grade;
            if(units<=0 || !Spout)return;
            splashes.Add(new Splash{Units=units,OnBeat=grade==EmberdeepPumpGrade.OnBeat,Start=Spout.position});InFlightUnits+=units;
        }
        Color FlashColor=>LastGrade==EmberdeepPumpGrade.OnBeat?Gold:LastGrade==EmberdeepPumpGrade.OffBeat?Rust:Pale;
        // The water was credited at the strike; dropping the visuals loses nothing.
        public void ClearSplashes(){splashes.Clear();InFlightUnits=0;}
        // gem: the gem's world position this frame. glow: the gem is empty (pump calls out).
        // beatPhase: 0 right after a beat .. 1 on the next one. inWindow is not drawn: the ring meeting its target is the
        // only "hit now" signal (headset test 2026-11-22), so nothing else changes colour or size in the window.
        public void Render(float dt,Camera camera,Vector3 gem,bool glow,bool hover,bool rhythm,float beatPhase,bool inWindow)
        {
            if(!built)return;
            dt=float.IsFinite(dt)?Mathf.Clamp(dt,0,.1f):0;clock+=dt;
            Glowing=glow;RhythmVisible=rhythm;
            plunge=Mathf.MoveTowards(plunge,0,dt*EmberdeepWaterTuning.PlungerTravel/.22f);
            plunger.localPosition=Vector3.down*plunge;
            flash=Mathf.MoveTowards(flash,0,dt/EmberdeepWaterTuning.StrikeFlashSeconds);
            float phase=float.IsFinite(beatPhase)?Mathf.Clamp01(beatPhase):0;
            TargetRadius=rhythm?CapRadius*EmberdeepWaterTuning.BeatTargetRing:0;
            ApproachRadius=rhythm?CapRadius*Mathf.Lerp(EmberdeepWaterTuning.BeatApproachRing,EmberdeepWaterTuning.BeatTargetRing,phase):0;
            float pulse=glow?.5f+.5f*Mathf.Sin(clock*6):0;
            // The hover tint steps aside while a beat runs: it would read as a second timing signal.
            var capColor=Color.Lerp(Cream,Cyan,glow?.35f+.45f*pulse:hover && !rhythm?.25f:0);
            capColor=Color.Lerp(capColor,FlashColor,flash*.75f);
            Tint(capRenderer,capColor);Tint(capBand,Color.Lerp(Brass,FlashColor,flash*.6f));
            // Splashes travel even off camera so their water always lands.
            for(int i=splashes.Count-1;i>=0;i--)
            {
                var s=splashes[i];s.Age+=dt;splashes[i]=s;
                if(s.Age<EmberdeepWaterTuning.SplashFlightSeconds)continue;
                splashes.RemoveAt(i);InFlightUnits=Mathf.Max(0,InFlightUnits-s.Units);SplashLanded?.Invoke(s.Units,s.OnBeat);
            }
            if(splashes.Count==0)InFlightUnits=0;
            if(!camera){fx.Renderer.enabled=false;return;}
            fx.Renderer.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);fx.Renderer.transform.localScale=Vector3.one;
            fx.Begin();
            var right=camera.transform.right;var up=camera.transform.up;var eye=camera.transform.position;
            var top=CapTop;var axisA=transform.right;var axisB=transform.forward;
            if(glow)
            {
                fx.Dot(top+Up*.01f+(eye-top).normalized*.02f,right,up,.075f+.012f*pulse,new Color(Cyan.r,Cyan.g,Cyan.b,.22f+.28f*pulse));
                fx.Ring(top+Up*.002f,axisA,axisB,CapRadius*(1.35f+.5f*Mathf.Repeat(clock*.9f,1)),.003f,new Color(Cyan.r,Cyan.g,Cyan.b,.55f*(1-Mathf.Repeat(clock*.9f,1))));
            }
            if(rhythm)
            {
                // ONE cue: the approach ring shrinks onto the fixed target ring and meets it on the beat. The target never
                // changes size, width or colour; the approach ring only grows more opaque as it closes.
                fx.Ring(top+Up*.003f,axisA,axisB,TargetRadius,.0022f,new Color(Gold.r,Gold.g,Gold.b,.5f));
                fx.Ring(top+Up*.0035f,axisA,axisB,ApproachRadius,.0026f,new Color(1,.92f,.62f,.25f+.65f*phase));
            }
            // Feedback only, after a strike: the target ring flashes in the strike's grade colour and fades fast.
            if(flash>0)
            {
                var c=FlashColor;
                fx.Ring(top+Up*.004f,axisA,axisB,CapRadius*EmberdeepWaterTuning.BeatTargetRing,.002f+.004f*flash,new Color(c.r,c.g,c.b,.9f*flash));
            }
            foreach(var s in splashes)DrawSplash(s,gem,eye,right,up);
            fx.End();fx.Renderer.enabled=fx.Quads>0;
        }
        void DrawSplash(Splash s,Vector3 gem,Vector3 eye,Vector3 right,Vector3 up)
        {
            float p=Mathf.Clamp01(s.Age/EmberdeepWaterTuning.SplashFlightSeconds);
            float tail=Mathf.Max(0,p-.4f);
            var start=Spout?Spout.position:s.Start;
            float lift=Mathf.Clamp(Vector3.Distance(start,gem)*.35f,.04f,.16f);
            path.Clear();
            for(int i=0;i<=10;i++){float t=Mathf.Lerp(tail,p,i/10f);path.Add(Arc(start,gem,lift,t));}
            var color=s.OnBeat?new Color(.75f,.95f,1,.9f):new Color(Cyan.r,Cyan.g,Cyan.b,.85f);
            fx.Ribbon(path,path.Count,eye,s.OnBeat?.010f:.008f,color,.3f);
            var head=Arc(start,gem,lift,p);
            fx.Dot(head,right,up,.012f,new Color(.8f,.97f,1,.9f));
            for(int i=0;i<3;i++)
            {
                float t=Mathf.Max(0,p-.08f*(i+1));
                fx.Dot(Arc(start,gem,lift,t)+up*(.006f*Mathf.Sin(i*2.1f+s.Age*20))+right*(.005f*(i-1)),right,up,.006f,new Color(.6f,.9f,1,.65f));
            }
        }
        static Vector3 Arc(Vector3 a,Vector3 b,float lift,float t)=>Vector3.Lerp(a,b,t)+Vector3.up*(lift*4*t*(1-t));
        Transform Part(string name,PrimitiveType type,Transform parent,Vector3 at,Vector3 scale,Material material,Color color)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=at;go.transform.localScale=scale;
            var c=go.GetComponent<Collider>();c.enabled=false;Object.Destroy(c);
            var r=go.GetComponent<Renderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;
            Tint(r,color);return go.transform;
        }
        void Tint(Renderer renderer,Color color)
        {
            if(!renderer)return;
            block.Clear();block.SetColor("_BaseColor",color);block.SetColor("_Color",color);renderer.SetPropertyBlock(block);
        }
        void OnDisable(){if(fx!=null && fx.Renderer)fx.Renderer.enabled=false;}
        void OnDestroy(){fx?.Destroy();if(fxHost)Destroy(fxHost);}
    }
}

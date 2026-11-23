using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FireRescue
{
    // LoopSpec §4.6 readability. Presentation only: nothing here writes FireRun.
    //  - A camera-facing flame pin and heat bar above every burning canopy, all in one dynamic
    //    opaque mesh (one draw call, no per-tree objects), so back rows read past the front rows.
    //  - A clear "out" puff and hiss when a burning tree turns Saved.
    //  - Trees between the eye and the aim point dither out (alpha-clip variant of the surface
    //    shader, swapped in only while fading, so all other draws keep early-Z).
    // FireRescueGame keeps the tree tint (_Heat/_Wetness/_Scorch); this class adds only _Fade.
    public sealed class EmberdeepTreeReadout : MonoBehaviour
    {
        // Forest units (x FireMiniature.Scale = metres). *tune*
        public const float BarGap=.16f, BarWidth=.62f, BarHeight=.09f, Border=.028f, PinGap=.05f, PinHeight=.42f;
        // See-through fade. Headset test 2026-11-22: at .78 (22% of the pixels kept) and the full crown width (1.08) the
        // front rows went to lace over the passthrough. Now a fading tree keeps 75% of its pixels (the 4x4 dither clips 4
        // of 16), and only a tree whose inner crown (FadeMargin of its radius) sits on the eye-to-aim line fades. The aimed
        // tree stays solid within SolidMargin of its radius.
        public const float MaximumFade=.25f, FadeSeconds=.14f, FadeMargin=.6f, SolidMargin=1.08f;
        // A burning tree's underside fire-glow never drops below this, however low its heat.
        public const float MinimumHeatTint=.3f;
        public const string FadeKeyword="EMBERDEEP_DITHER_FADE";
        // §4.4 forecast: a dashed ember ring floats just over the crown of the next tree to catch (and around
        // the next grass patch), so it reads past the front rows. Same mesh, same draw. *tune*
        public const int RingDashes=12;
        public const float RingLift=.10f, RingScale=.78f, RingWidth=.06f, RingSpin=28, GrassRingHeight=.30f, GrassRingRadius=.50f;
        // Grass sits in the gaps under the canopy, so its ring also sends a dashed stalk up to a small halo
        // above the crowns: the same "halo over the canopy" cue the trees get.
        public const float StalkTop=2.55f, StalkDash=.16f, StalkGap=.11f, HaloRadius=.24f;
        const float Lift=.0006f; // metres: layers step toward the eye instead of z-fighting
        // Flame glyph, unit height, centred; first point is the fan centre.
        static readonly Vector2[] Flame={new Vector2(0,-.14f),new Vector2(0,.5f),new Vector2(.13f,.27f),new Vector2(.24f,.08f),new Vector2(.30f,-.12f),
            new Vector2(.28f,-.30f),new Vector2(.17f,-.45f),new Vector2(0,-.5f),new Vector2(-.17f,-.45f),new Vector2(-.28f,-.30f),
            new Vector2(-.30f,-.12f),new Vector2(-.24f,.08f),new Vector2(-.13f,.27f)};
        static readonly int FadeId=Shader.PropertyToID("_Fade");

        Transform[] roots=System.Array.Empty<Transform>();
        Renderer[][] parts=System.Array.Empty<Renderer[]>();
        Material[][][] originals=System.Array.Empty<Material[][]>(), clones=System.Array.Empty<Material[][]>();
        float[] tops=System.Array.Empty<float>(), lows=System.Array.Empty<float>(), radii=System.Array.Empty<float>(), fades=System.Array.Empty<float>();
        bool[] fading=System.Array.Empty<bool>();
        TreeState[] states=System.Array.Empty<TreeState>(), grassStates=System.Array.Empty<TreeState>();
        readonly Dictionary<Material,Material> fadeMaterials=new Dictionary<Material,Material>();
        readonly List<Vector3> vertices=new List<Vector3>(2048);
        readonly List<Color32> colors=new List<Color32>(2048);
        readonly List<int> indices=new List<int>(4096);
        Mesh mesh; MeshRenderer meshRenderer; Material material;
        AudioSource voice; AudioClip outClip;
        Color32 backing, track, outline, outer, core, hot, low, ringBack, ringHot, ringWarm, ringNext;
        float lastTick=-1, outReadyAt;

        public int Markers {get;private set;}
        public int Faded {get;private set;}
        public int Outs {get;private set;}
        public int Rings {get;private set;}
        public int GrassOuts {get;private set;}
        // 0-1 forecast pulse, shared by the rings, the tree ember glow and the grass ember.
        public float Pulse {get;private set;}
        public int Count=>roots.Length;
        public float FadeOf(int tree)=>tree>=0 && tree<fades.Length?fades[tree]:0;
        public bool FadingMaterial(int tree)=>tree>=0 && tree<fading.Length && fading[tree];
        public float TopOf(int tree)=>tree>=0 && tree<tops.Length?tops[tree]:0;

        void Awake()
        {
            var shader=Resources.Load<Shader>("EmberdeepTreeReadout");
            if(!shader)throw new System.InvalidOperationException("EmberdeepTreeReadout shader missing.");
            material=new Material(shader){name="Emberdeep tree readout"};
            mesh=new Mesh{name="Emberdeep burning tree markers"};mesh.MarkDynamic();
            var go=new GameObject("Burning tree markers");go.transform.SetParent(transform,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;meshRenderer=go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial=material;meshRenderer.shadowCastingMode=ShadowCastingMode.Off;meshRenderer.receiveShadows=false;
            meshRenderer.lightProbeUsage=LightProbeUsage.Off;meshRenderer.reflectionProbeUsage=ReflectionProbeUsage.Off;meshRenderer.enabled=false;
            backing=Tone(.07f,.045f,.035f);track=Tone(.30f,.12f,.05f);outline=Tone(.11f,.04f,.02f);
            outer=Tone(1,.40f,.06f);core=Tone(1,.86f,.36f);hot=Tone(1,.30f,.06f);low=Tone(1,.80f,.25f);
            ringBack=Tone(.10f,.04f,.02f);ringHot=Tone(1,.78f,.32f);ringWarm=Tone(1,.38f,.05f);ringNext=Tone(.82f,.26f,.04f);
            outClip=SynthesizeOut();
            var audio=new GameObject("Tree out hiss");audio.transform.SetParent(transform,false);
            voice=audio.AddComponent<AudioSource>();voice.playOnAwake=false;voice.loop=false;voice.clip=outClip;
            voice.spatialBlend=.75f;voice.rolloffMode=AudioRolloffMode.Linear;voice.minDistance=.5f;voice.maxDistance=8;
            voice.dopplerLevel=0;voice.priority=110;
        }
        static Color32 Tone(float r,float g,float b)
        {
            var c=new Color(r,g,b,1);
            return QualitySettings.activeColorSpace==ColorSpace.Linear?(Color32)c.linear:(Color32)c;
        }

        // Called once per incident build. renderers[i] may be null (no fade, bounds from the root).
        public void Build(FireRun run,Transform[] treeRoots,Renderer[][] renderers)
        {
            int count=treeRoots?.Length??0;
            roots=new Transform[count];parts=new Renderer[count][];originals=new Material[count][][];clones=new Material[count][][];
            tops=new float[count];lows=new float[count];radii=new float[count];fades=new float[count];fading=new bool[count];states=new TreeState[count];
            for(int i=0;i<count;i++)
            {
                var root=treeRoots[i];roots[i]=root;if(!root)continue;
                var set=renderers!=null && i<renderers.Length && renderers[i]!=null?renderers[i]:root.GetComponentsInChildren<Renderer>();
                parts[i]=set;originals[i]=new Material[set.Length][];clones[i]=new Material[set.Length][];
                float unit=Mathf.Max(1e-5f,root.lossyScale.y);
                Bounds all=default,crown=default;bool any=false;float crownArea=-1;
                for(int r=0;r<set.Length;r++)
                {
                    if(!set[r])continue;originals[i][r]=set[r].sharedMaterials;
                    var b=set[r].bounds;if(!any){all=b;any=true;}else all.Encapsulate(b);
                    float area=b.extents.x*b.extents.z;if(area>crownArea){crownArea=area;crown=b;}
                }
                if(!any){tops[i]=2;lows[i]=.4f;radii[i]=.55f;}
                else
                {
                    tops[i]=root.InverseTransformPoint(new Vector3(all.center.x,all.max.y,all.center.z)).y;
                    lows[i]=root.InverseTransformPoint(new Vector3(crown.center.x,crown.min.y,crown.center.z)).y;
                    radii[i]=Mathf.Max(crown.extents.x,crown.extents.z)/unit;
                }
                states[i]=run!=null && i<run.Trees.Length?run.Trees[i].State:TreeState.Safe;
            }
            grassStates=new TreeState[run!=null?run.Grass.Length:0];
            for(int g=0;g<grassStates.Length;g++)grassStates[g]=run.Grass[g].State;
            Markers=Faded=Outs=Rings=GrassOuts=0;lastTick=-1;
            if(mesh)mesh.Clear(false);if(meshRenderer)meshRenderer.enabled=false;
        }

        public void Tick(FireRun run,Transform arena,Camera eye,FireRescueVfx effects,bool aiming,Vector3 aimWorld)
        {
            if(run==null || !arena || !mesh)return;
            float now=Time.unscaledTime,dt=lastTick<0?0:Mathf.Clamp(now-lastTick,0,.1f);lastTick=now;
            int count=Mathf.Min(roots.Length,run.Trees.Length);
            // Out moment: Heat reached 0 and the tree turned Saved. Saved trees never relight.
            int outs=0;Vector3 outAt=default;
            for(int i=0;i<count;i++)
            {
                var state=run.Trees[i].State;
                if(states[i]==TreeState.Burning && state==TreeState.Saved && roots[i])
                {
                    float unit=roots[i].lossyScale.y;
                    var crown=roots[i].position+arena.up*(tops[i]*.62f*unit);
                    if(effects)effects.Extinguish(crown,radii[i]*unit,arena.up);
                    outs++;outAt=crown;
                }
                states[i]=state;
            }
            // Grass goes out with a smaller puff at ground level.
            for(int g=0;g<grassStates.Length && g<run.Grass.Length;g++)
            {
                var state=run.Grass[g].State;
                if(grassStates[g]==TreeState.Burning && state==TreeState.Saved)
                {
                    var at=arena.TransformPoint(new Vector3(run.Grass[g].Position.x,.2f,run.Grass[g].Position.z));
                    if(effects)effects.Extinguish(at,EmberdeepGrassView.Radius*arena.lossyScale.y,arena.up);
                    outs++;outAt=at;GrassOuts++;
                }
                grassStates[g]=state;
            }
            if(outs>0){Outs+=outs;PlayOut(outAt,outs,now);}
            Pulse=.5f+.5f*Mathf.Sin(now*Mathf.PI*2*PulseHz(run));
            // See-through: dither every tree whose crown cuts the eye-to-aim segment.
            Faded=0;
            bool sight=aiming && eye && float.IsFinite(aimWorld.sqrMagnitude);
            Vector3 e=sight?arena.InverseTransformPoint(eye.transform.position):default,a=sight?arena.InverseTransformPoint(aimWorld):default;
            for(int i=0;i<count;i++)
            {
                bool occludes=false;
                if(sight && roots[i])
                {
                    var c=arena.InverseTransformPoint(roots[i].position);
                    occludes=Occludes(e,a,c,radii[i]*FadeMargin,c.y+lows[i],c.y+tops[i],radii[i]*SolidMargin);
                }
                fades[i]=Mathf.MoveTowards(fades[i],occludes?MaximumFade:0,dt*MaximumFade/FadeSeconds);
                SetFading(i,fades[i]>.001f);
                if(fading[i])Faded++;
            }
            BuildMarkers(run,arena,eye,now,count);
        }

        // Eye e and aim a in arena space; the crown is a vertical cylinder of radius r from y0 to y1. A tree whose centre
        // is within solid (default r) of the aim point is the one aimed at and stays solid.
        public static bool Occludes(Vector3 e,Vector3 a,Vector3 c,float r,float y0,float y1,float solid=-1)
        {
            float ax=a.x-c.x,az=a.z-c.z;if(solid<0)solid=r;
            if(ax*ax+az*az<solid*solid)return false; // the tree being aimed at stays solid
            float dx=a.x-e.x,dz=a.z-e.z,dd=dx*dx+dz*dz;if(dd<1e-6f)return false;
            float fx=e.x-c.x,fz=e.z-c.z,fd=fx*dx+fz*dz,ff=fx*fx+fz*fz-r*r;
            float disc=fd*fd-dd*ff;if(disc<=0)return false;
            float root=Mathf.Sqrt(disc),t0=(-fd-root)/dd,t1=(-fd+root)/dd;
            if(t1<=0 || t0>=1)return false;
            t0=Mathf.Max(t0,0);t1=Mathf.Min(t1,1);
            float h0=e.y+(a.y-e.y)*t0,h1=e.y+(a.y-e.y)*t1;
            return Mathf.Min(h0,h1)<=y1 && Mathf.Max(h0,h1)>=y0;
        }

        void SetFading(int tree,bool on)
        {
            if(fading[tree]==on)return;
            fading[tree]=on;
            var set=parts[tree];if(set==null)return;
            for(int r=0;r<set.Length;r++)
            {
                var renderer=set[r];var source=originals[tree][r];if(!renderer || source==null)continue;
                if(!on){renderer.sharedMaterials=source;continue;}
                if(clones[tree][r]==null)
                {
                    var swapped=new Material[source.Length];
                    for(int m=0;m<source.Length;m++)swapped[m]=FadeMaterial(source[m]);
                    clones[tree][r]=swapped;
                }
                renderer.sharedMaterials=clones[tree][r];
            }
        }
        Material FadeMaterial(Material source)
        {
            if(!source)return source;
            if(fadeMaterials.TryGetValue(source,out var clone))return clone;
            var keyword=source.shader.keywordSpace.FindKeyword(FadeKeyword);
            if(!keyword.isValid || !source.HasProperty(FadeId)){fadeMaterials[source]=source;return source;}
            clone=new Material(source){name=source.name+" (dither fade)"};clone.EnableKeyword(keyword);
            fadeMaterials[source]=clone;return clone;
        }

        void BuildMarkers(FireRun run,Transform arena,Camera eye,float now,int count)
        {
            vertices.Clear();colors.Clear();indices.Clear();Markers=0;Rings=0;
            if(!eye){meshRenderer.enabled=false;return;}
            var view=eye.transform;Vector3 right=view.right,up=view.up,toward=-view.forward;
            var toLocal=meshRenderer.transform.worldToLocalMatrix;
            for(int i=0;i<count;i++)
            {
                var data=run.Trees[i];if(data.State!=TreeState.Burning || !roots[i])continue;
                float heat=Mathf.Clamp01(data.Heat/100),unit=roots[i].lossyScale.y;
                var anchor=roots[i].position+arena.up*((tops[i]+BarGap)*unit);
                // Mild size compensation so the back row's pins are not half the front row's.
                float s=unit*Mathf.Clamp(Vector3.Distance(view.position,anchor)/.85f,.9f,1.35f);
                var bar=anchor+up*((BarHeight*.5f+Border)*s);
                Quad(toLocal,bar,right,up,(BarWidth*.5f+Border)*s,(BarHeight*.5f+Border)*s,backing);
                Quad(toLocal,bar+toward*Lift,right,up,BarWidth*.5f*s,BarHeight*.5f*s,track);
                if(heat>0)
                {
                    float half=BarWidth*.5f*heat*s;
                    Quad(toLocal,bar-right*(BarWidth*.5f*s-half)+toward*(Lift*2),right,up,half,BarHeight*.5f*s,Color32.Lerp(low,hot,heat));
                }
                float pin=PinHeight*s*(1+.08f*Mathf.Sin(now*9.5f+i*1.7f));
                var centre=anchor+up*((BarHeight+Border*2+PinGap+PinHeight*.5f)*s);
                Fan(toLocal,centre,right,up,pin*1.18f,outline);
                Fan(toLocal,centre+toward*Lift,right,up,pin,outer);
                Fan(toLocal,centre+toward*(Lift*2)-up*(pin*.13f),right,up,pin*.5f,core);
                Markers++;
            }
            BuildRings(run,arena,now,count,right);
            if(Markers+Rings==0){mesh.Clear(false);meshRenderer.enabled=false;return;}
            mesh.Clear(false);mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(indices,0,true);
            meshRenderer.enabled=true;
        }
        // The pulse quickens as the next catch draws near (Plan phase: the first seconds after Start).
        static float PulseHz(FireRun run)
        {
            float eta=run.ForecastSeconds(0);
            return eta<0?1.2f:Mathf.Lerp(3.2f,1.1f,Mathf.Clamp01(eta/8));
        }
        void BuildRings(FireRun run,Transform arena,float now,int count,Vector3 viewRight)
        {
            Rings=0;
            var toLocal=meshRenderer.transform.worldToLocalMatrix;
            Vector3 x=arena.right,z=arena.forward,up=arena.up;
            for(int slot=0;slot<EmberdeepBoardTuning.ForecastSlots;slot++)
            {
                int cell=run.ForecastCell(slot);if(cell<0)continue;
                Vector3 centre;float radius;
                if(cell<run.Trees.Length)
                {
                    if(cell>=count || !roots[cell])continue;
                    float unit=roots[cell].lossyScale.y;
                    centre=roots[cell].position+up*((tops[cell]+RingLift)*unit);radius=Mathf.Max(.34f,radii[cell]*RingScale)*unit;
                }
                else
                {
                    int g=cell-run.Trees.Length;if(g>=run.Grass.Length)continue;
                    var p=run.Grass[g].Position;centre=arena.TransformPoint(new Vector3(p.x,GrassRingHeight,p.z));radius=GrassRingRadius*arena.lossyScale.y;
                }
                float width=RingWidth*arena.lossyScale.y*(slot==0?1:.8f);
                var dash=slot==0?Color32.Lerp(ringWarm,ringHot,Pulse):Color32.Lerp(ringNext,ringWarm,Pulse*.6f);
                float spin=now*RingSpin*(slot==0?1:-.7f)*Mathf.Deg2Rad;
                if(cell>=run.Trees.Length)
                {
                    float scale=arena.lossyScale.y,top=(StalkTop-GrassRingHeight)*scale,period=(StalkDash+StalkGap)*scale;
                    var side=Vector3.ProjectOnPlane(viewRight,up).normalized*(width*.5f);
                    // Dashes climb the stalk, like sparks rising toward the halo.
                    for(float y=Mathf.Repeat(now*.35f*scale,period)+RingWidth*scale;y<top;y+=period)
                    {
                        float y1=Mathf.Min(top,y+StalkDash*scale);
                        Strip(toLocal,centre+up*y,centre+up*y1,side*1.7f,0,ringBack);
                        Strip(toLocal,centre+up*y,centre+up*y1,side,-1,dash);
                    }
                    var halo=centre+up*top;float haloRadius=HaloRadius*scale;
                    for(int d=0;d<RingDashes/2;d++)
                    {
                        float a0=spin+d*Mathf.PI*4/RingDashes,a1=a0+Mathf.PI*4/RingDashes*.56f;
                        Arc(toLocal,halo,x,z,up,haloRadius,width*1.7f,a0-.05f,a1+.05f,0,ringBack);
                        Arc(toLocal,halo,x,z,up,haloRadius,width,a0,a1,Lift*1.5f,dash);
                    }
                }
                for(int d=0;d<RingDashes;d++)
                {
                    float a0=spin+d*Mathf.PI*2/RingDashes,a1=a0+Mathf.PI*2/RingDashes*.56f;
                    Arc(toLocal,centre,x,z,up,radius,width*1.7f,a0-.03f,a1+.03f,0,ringBack);
                    Arc(toLocal,centre,x,z,up,radius,width,a0,a1,Lift*1.5f,dash);
                }
                Rings++;
            }
        }
        // A vertical band from a to b, widened along side; toward < 0 steps it a hair toward the viewer.
        void Strip(Matrix4x4 toLocal,Vector3 a,Vector3 b,Vector3 side,float toward,Color32 color)
        {
            int first=vertices.Count;var lift=toward<0?Vector3.Cross((b-a).normalized,side.normalized)*(Lift*2):Vector3.zero; // up x right = toward the eye
            vertices.Add(toLocal.MultiplyPoint3x4(a-side+lift));vertices.Add(toLocal.MultiplyPoint3x4(a+side+lift));
            vertices.Add(toLocal.MultiplyPoint3x4(b+side+lift));vertices.Add(toLocal.MultiplyPoint3x4(b-side+lift));
            for(int k=0;k<4;k++)colors.Add(color);
            indices.Add(first);indices.Add(first+2);indices.Add(first+1);
            indices.Add(first);indices.Add(first+3);indices.Add(first+2);
        }
        // A flat arc band in the board plane (the readout shader draws both faces).
        void Arc(Matrix4x4 toLocal,Vector3 centre,Vector3 x,Vector3 z,Vector3 up,float radius,float width,float a0,float a1,float lift,Color32 color)
        {
            const int steps=3;int first=vertices.Count;
            for(int k=0;k<=steps;k++)
            {
                float a=Mathf.Lerp(a0,a1,k/(float)steps);var radial=x*Mathf.Cos(a)+z*Mathf.Sin(a);
                vertices.Add(toLocal.MultiplyPoint3x4(centre+up*lift+radial*(radius-width*.5f)));
                vertices.Add(toLocal.MultiplyPoint3x4(centre+up*lift+radial*(radius+width*.5f)));
                colors.Add(color);colors.Add(color);
            }
            for(int k=0;k<steps;k++)
            {
                int p=first+k*2;
                indices.Add(p);indices.Add(p+1);indices.Add(p+3);
                indices.Add(p);indices.Add(p+3);indices.Add(p+2);
            }
        }
        void Quad(Matrix4x4 toLocal,Vector3 centre,Vector3 right,Vector3 up,float halfWidth,float halfHeight,Color32 color)
        {
            int first=vertices.Count;
            vertices.Add(toLocal.MultiplyPoint3x4(centre-right*halfWidth-up*halfHeight));
            vertices.Add(toLocal.MultiplyPoint3x4(centre+right*halfWidth-up*halfHeight));
            vertices.Add(toLocal.MultiplyPoint3x4(centre+right*halfWidth+up*halfHeight));
            vertices.Add(toLocal.MultiplyPoint3x4(centre-right*halfWidth+up*halfHeight));
            for(int k=0;k<4;k++)colors.Add(color);
            indices.Add(first);indices.Add(first+2);indices.Add(first+1);
            indices.Add(first);indices.Add(first+3);indices.Add(first+2);
        }
        void Fan(Matrix4x4 toLocal,Vector3 centre,Vector3 right,Vector3 up,float height,Color32 color)
        {
            int first=vertices.Count;
            for(int k=0;k<Flame.Length;k++){vertices.Add(toLocal.MultiplyPoint3x4(centre+(right*Flame[k].x+up*Flame[k].y)*height));colors.Add(color);}
            for(int k=1;k<Flame.Length;k++){indices.Add(first);indices.Add(first+(k==Flame.Length-1?1:k+1));indices.Add(first+k);}
        }

        void PlayOut(Vector3 at,int trees,float now)
        {
            if(!voice || now<outReadyAt)return;
            outReadyAt=now+.07f;
            voice.transform.position=at;voice.Stop();
            voice.volume=Mathf.Min(.32f,.22f+.03f*(trees-1));voice.pitch=Random.Range(.94f,1.06f);voice.Play();
        }
        // "Pfff-tssh": a soft low puff under a steam hiss that tails off. Synthesized once.
        static AudioClip SynthesizeOut()
        {
            const int rate=22050;const float duration=.6f;
            int count=Mathf.CeilToInt(duration*rate);var samples=new float[count];
            uint random=48271u;float lowpass=0,body=0,phase=0;
            for(int i=0;i<count;i++)
            {
                float t=i/(float)rate,u=t/duration;
                random=random*1664525u+1013904223u;
                float noise=((random>>8)/16777215f)*2-1;
                lowpass=Mathf.Lerp(lowpass,noise,.10f);body=Mathf.Lerp(body,noise,.035f);
                float hiss=(noise-lowpass)*.30f*Mathf.Exp(-u*4.2f)*Mathf.Clamp01(t/.03f);
                float puff=body*2.2f*Mathf.Exp(-t*13);
                phase+=2*Mathf.PI*Mathf.Lerp(120,58,Mathf.Clamp01(t/.09f))/rate;
                float thump=Mathf.Sin(phase)*.28f*Mathf.Exp(-t*32);
                float edge=Mathf.Min(Mathf.Clamp01(t/.004f),Mathf.Clamp01((duration-t)/.02f));
                samples[i]=Mathf.Clamp((hiss+puff+thump)*edge,-.8f,.8f);
            }
            var clip=AudioClip.Create("Emberdeep tree out",count,1,rate,false);clip.SetData(samples,0);return clip;
        }

        void OnDestroy()
        {
            foreach(var pair in fadeMaterials)if(pair.Value && pair.Value!=pair.Key)Destroy(pair.Value);
            fadeMaterials.Clear();
            if(mesh)Destroy(mesh);if(material)Destroy(material);if(outClip)Destroy(outClip);
        }
    }
}

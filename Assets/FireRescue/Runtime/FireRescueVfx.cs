using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FireRescue
{
    // Shared, capped emitters for the entire miniature: no per-tree lights or particle renderers.
    public sealed class FireRescueVfx : MonoBehaviour
    {
        ParticleSystem flames, smoke, embers, steam, splash, water, glow, sparks, ripples;
        Vector3[] celebrationCrowns=System.Array.Empty<Vector3>();
        Vector3 celebrationUp=Vector3.up;
        float celebrationRemaining,celebrationBurstClock,celebrationDriftClock;
        int celebrationPattern;
        static readonly Color[] Festive={new Color(1,.84f,.25f),new Color(1,.45f,.62f),new Color(.45f,1,.62f),new Color(.4f,.82f,1),new Color(1,1,.9f),new Color(.78f,.55f,1)};
        public bool Celebrating=>celebrationRemaining>0;
        public int VisibleCelebration=>sparks?sparks.particleCount:0;
        ParticleSystemRenderer waterRenderer;
        ParticleSystem[] emitters;
        Material fireMaterial, mistMaterial, glowMaterial, waterMaterial, strandMaterial, rippleMaterial;
        public const int MaximumStreamLinks=192;
        Mesh strandMesh;
        MeshRenderer strandRenderer;
        readonly List<Vector3> strandVertices=new List<Vector3>(MaximumStreamLinks*15);
        readonly List<Vector2> strandUvs=new List<Vector2>(MaximumStreamLinks*15);
        readonly List<Color32> strandColors=new List<Color32>(MaximumStreamLinks*15);
        readonly List<int> strandIndices=new List<int>(MaximumStreamLinks*48);
        readonly int[] ageOrder=new int[256];
        readonly Vector3[] launchOrigins=new Vector3[256],launchDirections=new Vector3[256];
        readonly float[,] streamBreakTimes=new float[2,128];
        readonly int[] streamBreakCounts=new int[2],streamBreakCursors=new int[2];
        readonly bool[] streamWasEmitting=new bool[2];
        float streamClock;
        readonly float[] flameClock=new float[42], smokeClock=new float[42], emberClock=new float[42], steamClock=new float[42];
        readonly ParticleSystem.Particle[] buffer=new ParticleSystem.Particle[1024];
        readonly ParticleSystem.Particle[] drops=new ParticleSystem.Particle[768];
        readonly Vector3[] sprinklerCenters=new Vector3[4];
        readonly float[] sprinklerClocks=new float[4];
        readonly bool[] sprinklerSlots=new bool[4];
        int sprinklerPattern;
        float glowClock,rippleReadyAt;
        public int VisibleStreamLinks { get; private set; }
        public int StreamTriangleCount=>strandIndices.Count/3;
        public int VisibleWater => water ? water.particleCount : 0;
        public int VisibleFlames => flames ? flames.particleCount : 0;
        public int VisibleSteam => steam ? steam.particleCount : 0;
        public int VisibleSplash => splash ? splash.particleCount : 0;
        public int VisibleRipples => ripples ? ripples.particleCount : 0;
        public void Initialize()
        {
            if(flames)return;
            var shader=Resources.Load<Shader>("EmberdeepV2Particles");
            if(!shader)throw new System.InvalidOperationException("EmberdeepV2Particles shader missing.");
            // Keep the original soft additive particle layering, rather than V2's almost
            // opaque, narrow flame silhouettes; the shader now grades the colour in layers.
            fireMaterial=new Material(shader){name="Emberdeep soft layered flame"};fireMaterial.SetFloat("_Style",1);fireMaterial.SetFloat("_DstBlend",1);
            rippleMaterial=new Material(shader){name="Emberdeep contact ripples"};rippleMaterial.SetFloat("_Style",5);
            glowMaterial=new Material(shader){name="Emberdeep V2 ember glow"};glowMaterial.SetFloat("_Style",2);glowMaterial.SetFloat("_DstBlend",1);
            mistMaterial=new Material(shader){name="Emberdeep V2 restrained smoke and steam"};
            waterMaterial=new Material(shader){name="Emberdeep V2 liquid droplets"};waterMaterial.SetFloat("_Style",3);
            strandMaterial=new Material(shader){name="Emberdeep V2 continuous water"};strandMaterial.SetFloat("_Style",4);
            flames=Emitter("Turbulent flame particles",1024,fireMaterial,.6f);
            Gradient(flames,new Color(1,.94f,.55f),new Color(1,.38f,.025f),new Color(.8f,.045f,.006f),.6f);
            var noise=flames.noise;noise.enabled=true;noise.strength=.02f;noise.frequency=11;noise.scrollSpeed=.65f;noise.quality=ParticleSystemNoiseQuality.Low;
            smoke=Emitter("Rolling smoke",384,mistMaterial,1.3f);
            Gradient(smoke,new Color(.20f,.19f,.18f),new Color(.33f,.32f,.31f),new Color(.50f,.50f,.49f),.34f);
            Grow(smoke,.55f,1.5f);
            embers=Emitter("Rising embers",160,glowMaterial,.65f);Gradient(embers,new Color(1,.8f,.25f),new Color(1,.3f,.02f),new Color(.7f,.05f,0),1);
            steam=Emitter("Cooling steam",256,mistMaterial,.8f);Gradient(steam,new Color(.86f,.96f,1),new Color(.80f,.92f,.96f),Color.white,.40f);Grow(steam,.4f,1.6f);
            splash=Emitter("Impact droplets",384,waterMaterial,.32f);Gradient(splash,new Color(.75f,.95f,1),new Color(.3f,.75f,1),new Color(.2f,.6f,.8f),.85f);
            var gravity=splash.main;gravity.gravityModifier=1;
            water=Emitter("Ballistic water droplets",768,waterMaterial,2);
            waterRenderer=water.GetComponent<ParticleSystemRenderer>();waterRenderer.renderMode=ParticleSystemRenderMode.Stretch;waterRenderer.velocityScale=.012f;waterRenderer.lengthScale=1.1f;
            glow=Emitter("Fire ground glow",84,glowMaterial,.11f);glow.GetComponent<ParticleSystemRenderer>().renderMode=ParticleSystemRenderMode.HorizontalBillboard;
            sparks=Emitter("Forest celebration sparks",512,glowMaterial,1);Gradient(sparks,Color.white,Color.white,Color.white,1);
            var festiveGravity=sparks.main;festiveGravity.gravityModifier=.05f;
            var flutter=sparks.noise;flutter.enabled=true;flutter.strength=.03f;flutter.frequency=3;flutter.quality=ParticleSystemNoiseQuality.Low;
            ripples=Emitter("Water contact ripples",96,rippleMaterial,.5f);ripples.GetComponent<ParticleSystemRenderer>().renderMode=ParticleSystemRenderMode.HorizontalBillboard;
            Gradient(ripples,Color.white,Color.white,Color.white,.9f);Grow(ripples,.25f,1);
            emitters=new[]{flames,smoke,embers,steam,splash,water,glow,sparks,ripples};
            var strands=new GameObject("Bounded ballistic water strands");strands.transform.SetParent(transform,false);
            strandMesh=new Mesh{name="Emberdeep V2 reusable water mesh"};strandMesh.MarkDynamic();
            strands.AddComponent<MeshFilter>().sharedMesh=strandMesh;strandRenderer=strands.AddComponent<MeshRenderer>();
            strandRenderer.sharedMaterial=strandMaterial;strandRenderer.shadowCastingMode=ShadowCastingMode.Off;strandRenderer.receiveShadows=false;strandRenderer.enabled=false;
        }
        ParticleSystem Emitter(string name,int capacity,Material material,float lifetime)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);var p=go.AddComponent<ParticleSystem>();p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=p.main;main.loop=true;main.playOnAwake=false;main.startLifetime=lifetime;main.startSpeed=0;main.maxParticles=capacity;main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.scalingMode=ParticleSystemScalingMode.Shape;main.startSize3D=true;main.useUnscaledTime=true;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            var emission=p.emission;emission.enabled=false;var shape=p.shape;shape.enabled=false;
            var r=p.GetComponent<ParticleSystemRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.maxParticleSize=.3f;
            p.Play();return p;
        }
        static void Gradient(ParticleSystem p,Color a,Color b,Color c,float opacity)
        {
            var g=new UnityEngine.Gradient();g.SetKeys(new[]{new GradientColorKey(a,0),new GradientColorKey(b,.3f),new GradientColorKey(c,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(opacity,.1f),new GradientAlphaKey(opacity*.65f,.55f),new GradientAlphaKey(0,1)});
            var color=p.colorOverLifetime;color.enabled=true;color.color=g;
        }
        static void Grow(ParticleSystem p,float start,float end)
        {var size=p.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,start,1,end));}
        static void Emit(ParticleSystem system,Vector3 at,Vector3 velocity,Vector3 size,Color color,float lifetime,uint seed=0)
        {system.Emit(new ParticleSystem.EmitParams{position=at,velocity=velocity,startSize3D=size,startColor=color,startLifetime=lifetime,randomSeed=seed},1);}
        public void Tick(FireRun run,Transform arena,WaterJet jet,float dt)
        {
            if(!flames || run==null || !arena || jet==null)return;
            if(float.IsNaN(dt) || float.IsInfinity(dt) || dt<0)dt=0;
            dt=Mathf.Min(dt,.1f);
            TickCelebration(dt);
            for(int i=0;i<steamClock.Length;i++)steamClock[i]=Mathf.Max(0,steamClock[i]-dt);
            glowClock+=dt;bool emitGlow=glowClock>=.08f;if(emitGlow)glowClock=0;
            for(int i=0;i<Mathf.Min(run.Trees.Length,flameClock.Length);i++)
            {
                var tree=run.Trees[i];
                if(tree.Wetness>.1f && emitGlow)Emit(glow,arena.TransformPoint(tree.Position-Vector3.up*.68f),Vector3.zero,Vector3.one*.11f,new Color(.25f,.7f,1,.07f*Mathf.Clamp01(tree.Wetness)),.11f);
                if(tree.State!=TreeState.Burning)continue;
                float intensity=tree.Heat/100, wet=Mathf.Clamp01(tree.Wetness);
                var at=arena.TransformPoint(tree.Position-Vector3.up*.4f);
                flameClock[i]+=dt*(18+16*intensity)*(1-.7f*wet);
                while(flameClock[i]>=1)
                {
                    flameClock[i]--;float scale=Random.Range(.7f,1.15f)*(.4f+.6f*intensity)*(1-.4f*wet);
                    var offset=Random.insideUnitCircle*.024f;
                    // Soft tongues lick up from the trunk foot and out of the boughs, so the whole
                    // tree reads as alight instead of carrying one column of fire over its crown.
                    float lift=Random.value<.4f?Random.Range(0,.02f):Random.Range(.025f,.095f);
                    // Bough flames sit on the viewer's side of the canopy; at the trunk axis the
                    // foliage would hide them.
                    if(lift>.02f)
                    {
                        var eye=Camera.main;var outward=eye?Vector3.ProjectOnPlane(eye.transform.position-at,Vector3.up):new Vector3(offset.x,0,offset.y);
                        if(outward.sqrMagnitude>1e-6f){outward=outward.normalized*Random.Range(.030f,.046f);offset+=new Vector2(outward.x,outward.z);}
                    }
                    Emit(flames,at+new Vector3(offset.x,lift,offset.y),
                        new Vector3(Random.Range(-.02f,.02f),Random.Range(.045f,.09f),Random.Range(-.02f,.02f)),
                        new Vector3(.058f,.108f,.058f)*scale,Color.white,Random.Range(.32f,.6f),(uint)(i+1));
                }
                smokeClock[i]+=dt*2.2f*intensity*(1-wet);
                if(smokeClock[i]>=1){smokeClock[i]--;Emit(smoke,at+Vector3.up*.115f,new Vector3(.012f,.075f,.006f),Vector3.one*Random.Range(.045f,.07f),Color.white,Random.Range(.65f,1.1f));}
                emberClock[i]+=dt*2.5f*intensity*(1-wet);
                if(emberClock[i]>=1){emberClock[i]--;Emit(embers,at+Vector3.up*.05f,new Vector3(Random.Range(-.05f,.05f),Random.Range(.14f,.28f),Random.Range(-.05f,.05f)),Vector3.one*.006f,Color.white,Random.Range(.35f,.8f));}
                if(emitGlow)Emit(glow,arena.TransformPoint(tree.Position-Vector3.up*.68f),Vector3.zero,Vector3.one*(.10f+.04f*intensity),new Color(1,.22f,.015f,.1f*intensity*(1-.7f*wet)),.11f);
            }
            // Reuse the capped flame/steam pool. Sheep carry a separate seed range
            // so watering a tree cannot erase an animal's fire by accident.
            for(int i=0;i<run.Animals.Length;i++)
            {
                var a=run.Animals[i];if(!emitGlow)continue;
                var at=arena.TransformPoint(a.Position+Vector3.up*FireRun.AnimalHeight);
                if(a.OnFire)
                    for(int j=0;j<3;j++)
                    {
                        var offset=arena.TransformVector(new Vector3(Mathf.Sin(a.AnimationTime*7+j*2)*.18f,.06f,(j-1)*.18f));
                        Emit(flames,at+offset,arena.up*.085f,new Vector3(.035f,.07f,.035f),Color.white,.3f,(uint)(1000+i));
                    }
                else if(a.WaterReaction>.3f)Emit(steam,at,arena.up*.045f,Vector3.one*.03f,Color.white,.4f);
            }
            // Water suppresses existing flame tongues too, not only newly emitted particles.
            int count=flames.GetParticles(buffer);
            for(int i=0;i<count;i++)
            {
                int animal=(int)buffer[i].randomSeed-1000;
                if(animal>=0 && animal<run.Animals.Length)
                {if(!run.Animals[animal].OnFire)buffer[i].remainingLifetime=Mathf.Min(buffer[i].remainingLifetime,.08f);continue;}
                int tree=(int)buffer[i].randomSeed-1;if(tree<0 || tree>=run.Trees.Length)continue;
                var data=run.Trees[tree];
                if(data.Wetness>.1f || data.State!=TreeState.Burning)
                {buffer[i].startSize3D*=Mathf.Exp(-dt*5);buffer[i].remainingLifetime=Mathf.Min(buffer[i].remainingLifetime,.18f);}
            }
            flames.SetParticles(buffer,count);
            // Stretch billboards need a nonzero velocity to establish their axis.
            // Cannon cores are placed directly from physics and have zero visual velocity.
            bool hasCannon=false;for(int i=0;i<jet.Count;i++)if(jet.Packets[i].Delivery==FireDelivery.Charge){hasCannon=true;break;}
            var mode=hasCannon?ParticleSystemRenderMode.Billboard:ParticleSystemRenderMode.Stretch;
            if(waterRenderer.renderMode!=mode)waterRenderer.renderMode=mode;
            int n=0;
            for(int i=0;i<jet.Count;i++)
            {
                var packet=jet.Packets[i];
                if(packet.Delivery==FireDelivery.Charge)
                {
                    // A coherent water bomb, with a glint and tail. Setting its visual velocity
                    // to zero prevents the shared stretched renderer turning it into a jet.
                    Vector3 direction=packet.Velocity.normalized;
                    float size=Mathf.Lerp(.035f,.065f,Mathf.Clamp01((packet.Radius-1.2f)/2));
                    drops[n++]=Drop(packet.Position,Vector3.zero,size,new Color(.22f,.7f,1,.85f));
                    drops[n++]=Drop(packet.Position+Vector3.up*size*.12f-direction*size*.08f,Vector3.zero,size*.48f,new Color(.8f,.97f,1,.9f));
                    drops[n++]=Drop(packet.Position-direction*size*.6f,Vector3.zero,size*.65f,new Color(.3f,.8f,1,.35f));
                    continue;
                }
                for(int j=0;j<3;j++)
                {
                    float phase=i*2.4f+j*2.1f;var side=Vector3.Cross(packet.Velocity.normalized,Vector3.up).normalized;
                    float spread=j==0?0:Mathf.Min(.012f,packet.Age*.025f);
                    drops[n++]=Drop(packet.Position+side*(Mathf.Sin(phase)*spread)+Vector3.up*(Mathf.Cos(phase)*spread),packet.Velocity,(j==0?.011f:.005f)*(packet.Delivery==FireDelivery.Foam?1.5f:1),packet.Delivery==FireDelivery.Foam?new Color(.95f,1,1,.85f):j==0?new Color(.62f,.9f,1,.85f):new Color(.75f,.94f,1,.55f));
                }
            }
            water.SetParticles(drops,n);
            UpdateStrands(jet,dt);
        }
        static ParticleSystem.Particle Drop(Vector3 position,Vector3 velocity,float size,Color color)
        {return new ParticleSystem.Particle{position=position,velocity=velocity,startLifetime=2,remainingLifetime=1.5f,startSize=size,startColor=color};}

        void UpdateStrands(WaterJet jet,float dt)
        {
            strandVertices.Clear();strandUvs.Clear();strandColors.Clear();strandIndices.Clear();VisibleStreamLinks=0;
            streamClock+=dt;
            int count=Mathf.Min(jet.Count,ageOrder.Length);
            for(int lane=0;lane<2;lane++)
            {
                var delivery=lane==0?FireDelivery.Foam:FireDelivery.Fan;bool emitting=false;
                for(int i=0;i<count;i++)if(jet.Packets[i].Delivery==delivery && jet.Packets[i].Age<=dt+.00001f){emitting=true;break;}
                if(!emitting && streamWasEmitting[lane])
                {
                    streamBreakTimes[lane,streamBreakCursors[lane]]=streamClock-dt;
                    streamBreakCursors[lane]=(streamBreakCursors[lane]+1)%128;
                    streamBreakCounts[lane]=Mathf.Min(128,streamBreakCounts[lane]+1);
                }
                streamWasEmitting[lane]=emitting;
            }
            // Packet storage swaps removals; array adjacency is never evidence of a shared stream.
            // Stable age ordering is allocation-free. Only nearby emission times are considered.
            for(int i=0;i<count;i++)
            {
                var packet=jet.Packets[i];float age=packet.Age;
                launchOrigins[i]=packet.Position-packet.Velocity*age+WaterJet.Gravity*(.5f*age*age);
                launchDirections[i]=(packet.Velocity-WaterJet.Gravity*age).normalized;
                int at=i;
                while(at>0 && jet.Packets[ageOrder[at-1]].Age>age){ageOrder[at]=ageOrder[at-1];at--;}
                ageOrder[at]=i;
            }
            for(int i=0;i<count && VisibleStreamLinks<MaximumStreamLinks;i++)
            {
                int current=ageOrder[i];var a=jet.Packets[current];
                if(a.Delivery!=FireDelivery.Foam && a.Delivery!=FireDelivery.Fan)continue;
                // At160 packets/s, .04s admits the next sample in a five-lane fan.
                // A released trigger, pulse cooldown, changing source or tool never gets bridged.
                for(int j=i+1;j<count;j++)
                {
                    int candidate=ageOrder[j];var b=jet.Packets[candidate];float gap=b.Age-a.Age;
                    if(gap>.04f)break;
                    if(gap<.001f || b.Delivery!=a.Delivery || Mathf.Abs(a.Radius-b.Radius)>.001f || Mathf.Abs(a.Wetness-b.Wetness)>.001f)continue;
                    if(CrossesRelease(a.Delivery,streamClock-b.Age,streamClock-a.Age))continue;
                    if((launchOrigins[current]-launchOrigins[candidate]).sqrMagnitude>.0004f || Vector3.Dot(launchDirections[current],launchDirections[candidate])<.9998f)continue;
                    if((a.Position-b.Position).sqrMagnitude>.0256f)continue;
                    AddStrand(a,b,gap);VisibleStreamLinks++;break;
                }
            }
            strandMesh.Clear(false);
            if(VisibleStreamLinks==0){strandRenderer.enabled=false;return;}
            strandMesh.SetVertices(strandVertices);strandMesh.SetUVs(0,strandUvs);strandMesh.SetColors(strandColors);
            strandMesh.SetTriangles(strandIndices,0,true);strandRenderer.enabled=true;
        }
        bool CrossesRelease(FireDelivery delivery,float olderBirth,float newerBirth)
        {
            int lane=delivery==FireDelivery.Foam?0:1;
            for(int i=0;i<streamBreakCounts[lane];i++)
            {float stopped=streamBreakTimes[lane,i];if(stopped>olderBirth && stopped<newerBirth)return true;}
            return false;
        }
        void AddStrand(WaterJet.Packet a,WaterJet.Packet b,float gap)
        {
            int first=strandVertices.Count;
            // Hermite midpoint follows the measured velocity and gravity of the live packets.
            Vector3 middle=(a.Position+b.Position)*.5f+(a.Velocity-b.Velocity)*(gap*.125f);
            float radius=a.Delivery==FireDelivery.Foam?.0044f:.0031f;
            Color32 color=a.Delivery==FireDelivery.Foam?new Color(.78f,.95f,.92f,.78f):new Color(.12f,.65f,.79f,.7f);
            for(int ring=0;ring<3;ring++)
            {
                Vector3 center=ring==0?a.Position:ring==1?middle:b.Position;
                Vector3 tangent=(ring==0?middle-a.Position:ring==1?b.Position-a.Position:b.Position-middle).normalized;
                Vector3 side=Vector3.Cross(tangent,Vector3.up);
                if(side.sqrMagnitude<.001f)side=Vector3.Cross(tangent,Vector3.right);
                side.Normalize();Vector3 up=Vector3.Cross(side,tangent).normalized;
                for(int face=0;face<=4;face++)
                {
                    float angle=face*Mathf.PI*.5f;
                    strandVertices.Add(strandRenderer.transform.InverseTransformPoint(center+(side*Mathf.Cos(angle)+up*Mathf.Sin(angle))*radius));
                    strandUvs.Add(new Vector2(face*.25f,ring*.5f));strandColors.Add(color);
                }
            }
            for(int ring=0;ring<2;ring++)for(int face=0;face<4;face++)
            {
                int p=first+ring*5+face,q=p+5;
                strandIndices.Add(p);strandIndices.Add(q);strandIndices.Add(p+1);
                strandIndices.Add(p+1);strandIndices.Add(q);strandIndices.Add(q+1);
            }
        }

        public void Launch(Vector3 origin,Vector3 direction,float charge)
        {
            if(!splash)return;
            charge=Mathf.Clamp01(charge);direction=direction.normalized;
            Vector3 side=Vector3.Cross(direction,Vector3.up).normalized;
            if(side.sqrMagnitude<.01f)side=Vector3.right;
            Vector3 up=Vector3.Cross(side,direction).normalized;
            for(int i=0;i<16;i++)
            {
                float angle=i*Mathf.PI*2/16;
                Vector3 radial=side*Mathf.Cos(angle)+up*Mathf.Sin(angle);
                Emit(splash,origin+radial*.015f,direction*(.25f+.25f*charge)+radial*.18f,Vector3.one*.009f,Color.white,.16f);
            }
            Emit(steam,origin+direction*.015f,direction*.3f,Vector3.one*.04f,new Color(.7f,.9f,1,.4f),.16f);
        }
        public void Burst(Vector3 worldPoint,float worldRadius)
        {
            if(!splash)return;
            float radius=Mathf.Clamp(worldRadius,.04f,.4f);
            // Expand to the actual damage radius. A ring makes the area legible without
            // hiding the diorama under a large opaque flash.
            for(int i=0;i<32;i++)
            {
                float angle=i*Mathf.PI*2/32;
                Vector3 radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                Emit(splash,worldPoint+Vector3.up*.008f,radial*(radius/.22f)+Vector3.up*.8f,Vector3.one*.008f,Color.white,.24f);
            }
            for(int i=0;i<12;i++)
            {
                float angle=i*Mathf.PI*2/12;
                Vector3 offset=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius*.4f;
                Emit(steam,worldPoint+offset+Vector3.up*.02f,Vector3.up*.13f,Vector3.one*.05f,new Color(.75f,.9f,1,.65f),.45f);
            }
            // Two rings spread across the soaked footprint after the crown falls back.
            if(ripples)
            {
                Emit(ripples,worldPoint+Vector3.up*.004f,Vector3.zero,Vector3.one*radius*2.1f,new Color(.62f,.93f,1,.9f),.55f);
                Emit(ripples,worldPoint+Vector3.up*.005f,Vector3.zero,Vector3.one*radius*1.2f,new Color(.85f,1,1,.8f),.38f);
            }
        }
        // Every fire is out. Firework-like pops rise above the saved crowns while
        // bright motes drift up through the whole grove. Presentation only.
        public void Celebrate(Vector3[] crowns,Vector3 up,float seconds)
        {
            if(!sparks || crowns==null || crowns.Length==0 || !float.IsFinite(seconds) || seconds<=0)return;
            celebrationCrowns=crowns;celebrationUp=up.sqrMagnitude>.01f?up.normalized:Vector3.up;
            celebrationRemaining=Mathf.Min(seconds,12);celebrationBurstClock=1;celebrationDriftClock=0;
        }
        void TickCelebration(float dt)
        {
            if(celebrationRemaining<=0 || dt<=0)return;
            celebrationRemaining=Mathf.Max(0,celebrationRemaining-dt);
            celebrationBurstClock+=dt*3.2f;
            while(celebrationBurstClock>=1)
            {
                celebrationBurstClock--;
                var crown=celebrationCrowns[(celebrationPattern*7+3)%celebrationCrowns.Length];
                var centre=crown+celebrationUp*(.10f+.05f*(celebrationPattern%3));
                var color=Festive[celebrationPattern%Festive.Length];celebrationPattern++;
                for(int i=0;i<18;i++)
                {
                    // An even shell of sparks, like a small firework over the tree.
                    float y=1-(i+.5f)/9f,ring=Mathf.Sqrt(Mathf.Max(0,1-y*y)),angle=i*2.399963f;
                    var direction=new Vector3(Mathf.Cos(angle)*ring,y*.7f+.3f,Mathf.Sin(angle)*ring);
                    Emit(sparks,centre,direction*Random.Range(.16f,.26f),Vector3.one*Random.Range(.008f,.013f),color,Random.Range(.65f,1f));
                }
            }
            celebrationDriftClock+=dt*46;
            while(celebrationDriftClock>=1)
            {
                celebrationDriftClock--;
                var crown=celebrationCrowns[Random.Range(0,celebrationCrowns.Length)];
                var offset=Random.insideUnitSphere*.05f;
                Emit(sparks,crown+offset,celebrationUp*Random.Range(.07f,.16f)+Random.insideUnitSphere*.03f,Vector3.one*Random.Range(.004f,.008f),
                    Festive[Random.Range(0,Festive.Length)],Random.Range(.8f,1.3f));
            }
        }
        public void Rescue(Vector3 point,int combo)
        {
            if(!splash)return;
            int count=Mathf.Clamp(8+combo*2,8,18);
            for(int i=0;i<count;i++)
            {
                float angle=i*Mathf.PI*2/count;
                Emit(splash,point+Vector3.up*.04f,new Vector3(Mathf.Cos(angle)*.13f,.65f,Mathf.Sin(angle)*.13f),Vector3.one*.006f,new Color(.55f,1,.82f,.8f),.18f);
            }
        }
        public void Sprinkler(Vector3 center,float radius,float dt)
        {
            if(!splash || dt<=0)return;
            int slot=-1,empty=-1;
            for(int i=0;i<sprinklerCenters.Length;i++)
            {
                if(!sprinklerSlots[i]){if(empty<0)empty=i;continue;}
                if((sprinklerCenters[i]-center).sqrMagnitude<.0001f){slot=i;break;}
            }
            if(slot<0)
            {
                slot=empty>=0?empty:0;sprinklerCenters[slot]=center;
                sprinklerSlots[slot]=true;sprinklerClocks[slot]=0;
            }
            sprinklerClocks[slot]+=Mathf.Min(dt,.1f)*65;
            while(sprinklerClocks[slot]>=1)
            {
                sprinklerClocks[slot]--;
                float angle=sprinklerPattern++*2.399963f+Time.unscaledTime*3;
                Vector3 radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                Emit(splash,center,radial*(Mathf.Clamp(radius,.04f,.4f)/.22f)+Vector3.up*.72f,Vector3.one*.006f,new Color(.8f,.95f,1,.9f),.25f);
            }
        }
        public void Impact(WaterJet.Impact hit,bool hot)
        {
            if(!splash)return;
            for(int i=0;i<3;i++)
                Emit(splash,hit.Point+hit.Normal*.003f,hit.Normal*Random.Range(.2f,.45f)+Random.insideUnitSphere*.3f,Vector3.one*Random.Range(.003f,.007f),Color.white,Random.Range(.12f,.3f));
            if(hot && hit.Tree>=0 && hit.Tree<steamClock.Length && steamClock[hit.Tree]<=0)
            {
                steamClock[hit.Tree]=.06f;
                Emit(steam,hit.Point+Vector3.up*.015f,new Vector3(Random.Range(-.025f,.025f),Random.Range(.08f,.13f),Random.Range(-.02f,.02f)),Vector3.one*Random.Range(.035f,.055f),Color.white,Random.Range(.4f,.65f));
            }
            // A small surface pulse marks wet contact without pretending to be another hit.
            if(hit.Tree<0 && glow)Emit(glow,hit.Point+hit.Normal*.002f,Vector3.zero,Vector3.one*.035f,new Color(.35f,.8f,.9f,.18f),.16f);
            // One ring every few packets: a steady stream leaves a gentle pulse, not a row of plates.
            if(hit.Tree<0 && ripples && hit.Normal.y>.6f && Time.unscaledTime>=rippleReadyAt)
            {
                rippleReadyAt=Time.unscaledTime+.16f;
                Emit(ripples,hit.Point+hit.Normal*.003f,Vector3.zero,Vector3.one*Random.Range(.026f,.038f),new Color(.70f,.95f,1,.7f),Random.Range(.30f,.42f));
            }
        }
        public void Clear()
        {
            if(emitters!=null)foreach(var p in emitters)if(p)p.Clear();
            System.Array.Clear(flameClock,0,42);System.Array.Clear(smokeClock,0,42);System.Array.Clear(emberClock,0,42);System.Array.Clear(steamClock,0,42);glowClock=0;
            System.Array.Clear(sprinklerClocks,0,4);System.Array.Clear(sprinklerSlots,0,4);sprinklerPattern=0;celebrationRemaining=0;rippleReadyAt=0;
            VisibleStreamLinks=0;strandVertices.Clear();strandUvs.Clear();strandColors.Clear();strandIndices.Clear();
            streamClock=0;System.Array.Clear(streamBreakCounts,0,2);System.Array.Clear(streamBreakCursors,0,2);System.Array.Clear(streamWasEmitting,0,2);
            if(strandMesh)strandMesh.Clear(false);if(strandRenderer)strandRenderer.enabled=false;
        }
        void OnDestroy()
        {
            if(fireMaterial)Destroy(fireMaterial);if(mistMaterial)Destroy(mistMaterial);if(glowMaterial)Destroy(glowMaterial);
            if(waterMaterial)Destroy(waterMaterial);if(strandMaterial)Destroy(strandMaterial);if(strandMesh)Destroy(strandMesh);if(rippleMaterial)Destroy(rippleMaterial);
        }
    }
}

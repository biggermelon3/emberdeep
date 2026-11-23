using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FireRescue
{
    // LoopSpec §4.2 wind cue: a rooster weathervane standing on the board. Presentation only.
    // Like a real vane, the arrow and the rooster's head point INTO the wind (upwind) and the tail
    // feathers stream downwind. About 3 s before a shift it dithers, swings to the new wind and a gust
    // whooshes. Placeholder geometry (merged primitives): two meshes, two opaque draws.
    public sealed class EmberdeepWeathervane : MonoBehaviour
    {
        // Arena position (forest units): centred just behind the grove and the sheep homes, tall enough to
        // read over the canopy, never in front of a tree. Follows the field (Place), so a resized board keeps it. *tune*
        public static readonly Vector3 Anchor=new Vector3(0,0,11.7f);
        public const float BehindField=.9f;
        public const float Size=.85f, PivotHeight=2.85f;
        public const float Stiffness=26, Damping=5.5f, WobbleSeconds=.9f;

        Transform rotor;
        Mesh standMesh, rotorMesh; Material iron, copper;
        AudioSource voice; AudioClip gustClip;
        float yaw=180, spin, clock;
        bool warned;
        public float Yaw=>yaw;
        // The arrow's world direction, for tests and captures.
        public Vector3 Pointing=>rotor?rotor.forward:transform.forward;
        public int Gusts {get;private set;}

        public void Build(Transform arena)
        {
            if(rotor)return;
            transform.SetParent(arena,false);transform.localPosition=Anchor;transform.localRotation=Quaternion.identity;transform.localScale=Vector3.one*Size;
            var shader=Resources.Load<Shader>("EmberdeepV2Surface");
            iron=new Material(shader){name="Emberdeep weathervane iron"};iron.SetColor("_BaseColor",new Color(.16f,.15f,.14f));iron.SetFloat("_Roughness",.45f);
            copper=new Material(shader){name="Emberdeep weathervane copper"};copper.SetColor("_BaseColor",new Color(.86f,.55f,.22f));copper.SetFloat("_Roughness",.28f);
            var cube=Primitive(PrimitiveType.Cube);var sphere=Primitive(PrimitiveType.Sphere);var cylinder=Primitive(PrimitiveType.Cylinder);
            var stand=new List<CombineInstance>();
            Part(stand,cylinder,new Vector3(0,.08f,0),Vector3.zero,new Vector3(.55f,.08f,.55f));
            Part(stand,cylinder,new Vector3(0,1.38f,0),Vector3.zero,new Vector3(.075f,1.3f,.075f));
            Part(stand,cube,new Vector3(0,2.05f,0),Vector3.zero,new Vector3(1.15f,.05f,.05f));
            Part(stand,cube,new Vector3(0,2.05f,0),Vector3.zero,new Vector3(.05f,.05f,1.15f));
            for(int k=0;k<4;k++){float a=k*Mathf.PI*.5f;Part(stand,sphere,new Vector3(Mathf.Sin(a)*.6f,2.05f,Mathf.Cos(a)*.6f),Vector3.zero,Vector3.one*(k==0?.17f:.12f));}
            Part(stand,sphere,new Vector3(0,2.72f,0),Vector3.zero,Vector3.one*.14f);
            standMesh=Merge("Emberdeep weathervane stand",stand);
            var body=new List<CombineInstance>();
            // Arrow along +z: diamond head upwind, a vertical fin downwind.
            Part(body,cube,Vector3.zero,Vector3.zero,new Vector3(.05f,.05f,1.6f));
            Part(body,cube,new Vector3(0,0,.82f),new Vector3(0,45,0),new Vector3(.2f,.05f,.2f));
            Part(body,cube,new Vector3(0,.05f,-.74f),Vector3.zero,new Vector3(.03f,.36f,.34f));
            // Rooster on the shaft, head upwind, tail feathers fanned downwind.
            Part(body,cylinder,new Vector3(-.06f,.09f,0),Vector3.zero,new Vector3(.03f,.08f,.03f));
            Part(body,cylinder,new Vector3(.06f,.09f,0),Vector3.zero,new Vector3(.03f,.08f,.03f));
            Part(body,sphere,new Vector3(0,.33f,-.03f),new Vector3(-12,0,0),new Vector3(.30f,.28f,.48f));
            Part(body,sphere,new Vector3(0,.46f,.17f),new Vector3(-30,0,0),new Vector3(.18f,.26f,.16f));
            Part(body,sphere,new Vector3(0,.62f,.23f),Vector3.zero,Vector3.one*.17f);
            Part(body,sphere,new Vector3(0,.75f,.22f),new Vector3(10,0,0),new Vector3(.04f,.13f,.17f));
            Part(body,cube,new Vector3(0,.61f,.34f),new Vector3(20,0,0),new Vector3(.045f,.045f,.11f));
            Part(body,sphere,new Vector3(0,.52f,.29f),Vector3.zero,new Vector3(.035f,.09f,.05f));
            for(int k=0;k<4;k++)Part(body,sphere,new Vector3(0,.50f+k*.03f,-.30f-k*.02f),new Vector3(-20-k*24,0,0),new Vector3(.035f,.44f-k*.04f,.17f));
            rotorMesh=Merge("Emberdeep weathervane rooster",body);
            Destroy(cube);Destroy(sphere);Destroy(cylinder);
            Draw(gameObject,standMesh,iron);
            rotor=new GameObject("Rooster and arrow").transform;rotor.SetParent(transform,false);rotor.localPosition=Vector3.up*PivotHeight;
            Draw(rotor.gameObject,rotorMesh,copper);
            var audio=new GameObject("Gust");audio.transform.SetParent(transform,false);audio.transform.localPosition=Vector3.up*PivotHeight;
            voice=audio.AddComponent<AudioSource>();voice.playOnAwake=false;voice.loop=false;voice.clip=gustClip=SynthesizeGust();
            voice.spatialBlend=.6f;voice.rolloffMode=AudioRolloffMode.Linear;voice.minDistance=.5f;voice.maxDistance=8;voice.dopplerLevel=0;voice.priority=120;
        }
        static Mesh Primitive(PrimitiveType kind)
        {
            var go=GameObject.CreatePrimitive(kind);var mesh=Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
            go.SetActive(false);Destroy(go);return mesh;
        }
        static void Part(List<CombineInstance> parts,Mesh mesh,Vector3 at,Vector3 euler,Vector3 scale)
            =>parts.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(at,Quaternion.Euler(euler),scale)});
        static Mesh Merge(string name,List<CombineInstance> parts){var mesh=new Mesh{name=name};mesh.CombineMeshes(parts.ToArray(),true,true);return mesh;}
        static void Draw(GameObject go,Mesh mesh,Material material)
        {
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;
            r.shadowCastingMode=ShadowCastingMode.On;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
        }

        // Behind the deepest tree or sheep home of this incident.
        public static Vector3 AnchorFor(FireRun run)
        {
            if(run==null || run.Trees.Length==0)return Anchor;
            float back=float.NegativeInfinity;
            foreach(var t in run.Trees)back=Mathf.Max(back,t.Position.z);
            foreach(var a in run.Animals)back=Mathf.Max(back,a.Home.z);
            return new Vector3(0,0,back+BehindField);
        }
        public void Place(FireRun run){if(rotor)transform.localPosition=AnchorFor(run);}
        // Board yaw (degrees, 0 = +z) the arrow should point for a downwind direction: straight upwind.
        public static float UpwindYaw(Vector3 downwind)=>Mathf.Atan2(-downwind.x,-downwind.z)*Mathf.Rad2Deg;

        public void Tick(FireRun run,float dt)
        {
            if(!rotor || run==null)return;
            dt=Mathf.Clamp(dt,0,.1f);
            bool live=run.Phase==FirePhase.Fighting;clock+=live?dt:0;
            float strength=run.WindStrength,target=UpwindYaw(run.WindDirection);
            if(run.WindWarning)
            {
                // Dither on the old wind, then swing to the new one before it arrives.
                float into=EmberdeepBoardTuning.WindWarningSeconds-run.WindShiftIn;
                target=into<WobbleSeconds?target+38*Mathf.Sin(into*15)*Mathf.Sin(into/WobbleSeconds*Mathf.PI):UpwindYaw(run.NextWindDirection);
                strength=Mathf.Max(strength,run.NextWindStrength);
                if(!warned){warned=true;Gusts++;if(voice){voice.volume=.30f;voice.pitch=Random.Range(.95f,1.05f);voice.Play();}}
            }
            else warned=false;
            // Gusty flutter that grows with the wind; a calm vane barely drifts. Time stops in Plan.
            if(live)target+=strength>.01f?strength*7*(Mathf.PerlinNoise(clock*1.9f,.37f)-.5f)*2:3*Mathf.Sin(clock*.4f);
            if(dt<=0)return;
            if(run.Phase==FirePhase.Preparing || run.Phase==FirePhase.Ready){yaw=Mathf.MoveTowardsAngle(yaw,target,dt*240);spin=0;}
            else
            {
                // Underdamped spring: a real vane overshoots a little and settles.
                spin+=(Mathf.DeltaAngle(yaw,target)*Stiffness-spin*Damping)*dt;
                yaw+=spin*dt;
            }
            rotor.localRotation=Quaternion.Euler(0,yaw,0);
        }
        // Snap without animation (incident build, captures).
        public void Settle(FireRun run)
        {
            if(!rotor || run==null)return;
            yaw=UpwindYaw(run.WindDirection);spin=0;rotor.localRotation=Quaternion.Euler(0,yaw,0);
        }

        // A soft rising-and-falling wind whoosh, synthesized once.
        static AudioClip SynthesizeGust()
        {
            const int rate=22050;const float duration=1.7f;
            int count=Mathf.CeilToInt(duration*rate);var samples=new float[count];
            uint random=69069u;float low=0,band=0;
            for(int i=0;i<count;i++)
            {
                float t=i/(float)rate,u=t/duration;
                random=random*1664525u+1013904223u;float noise=((random>>8)/16777215f)*2-1;
                // A filter that opens and closes with the swell: "fwoooosh".
                float swell=Mathf.Sin(Mathf.PI*Mathf.Pow(u,.7f));
                low=Mathf.Lerp(low,noise,.02f+.10f*swell);band=Mathf.Lerp(band,low,.5f);
                float flutter=.85f+.15f*Mathf.Sin(t*23+Mathf.Sin(t*7)*2);
                samples[i]=Mathf.Clamp((low-band*.6f)*3.2f*swell*swell*flutter,-.8f,.8f);
            }
            var clip=AudioClip.Create("Emberdeep wind gust",count,1,rate,false);clip.SetData(samples,0);return clip;
        }

        void OnDestroy()
        {
            if(standMesh)Destroy(standMesh);if(rotorMesh)Destroy(rotorMesh);if(iron)Destroy(iron);if(copper)Destroy(copper);if(gustClip)Destroy(gustClip);
        }
    }
}

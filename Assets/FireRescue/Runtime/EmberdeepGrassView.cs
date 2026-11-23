using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FireRescue
{
    // LoopSpec §4.1 grass, presentation only. Every patch of the incident is one low tuft cluster in a
    // single mesh drawn once with the opaque V2 surface shader. Per-patch state (heat, wetness, scorch,
    // forecast ember) goes in UV1, which that shader reads when _VertexState is 1. The mesh, material
    // and lists are reused across incidents; UV1 is re-uploaded only when a patch's look changes.
    public sealed class EmberdeepGrassView : MonoBehaviour
    {
        // Forest units (x FireMiniature.Scale = metres). Short on purpose: grass never hides a tree or sheep. *tune*
        public const int Blades=14;
        public const float Radius=.30f, Height=.30f, BladeWidth=.034f;
        static readonly Color Pigment=new Color(.55f,.70f,.27f);

        Mesh mesh; MeshRenderer meshRenderer; Material material;
        readonly List<Vector3> vertices=new List<Vector3>(4096), normals=new List<Vector3>(4096);
        readonly List<Color> colors=new List<Color>(4096);
        readonly List<Vector4> states=new List<Vector4>(4096);
        readonly List<int> indices=new List<int>(6144);
        int[] first=System.Array.Empty<int>(), counts=System.Array.Empty<int>();
        Vector4[] shown=System.Array.Empty<Vector4>();
        public int Patches=>first.Length;
        public Vector4 StateOf(int patch)=>patch>=0 && patch<shown.Length?shown[patch]:default;
        public Material Material=>material;

        void Awake()
        {
            var shader=Resources.Load<Shader>("EmberdeepV2Surface");
            if(!shader)throw new System.InvalidOperationException("EmberdeepV2Surface shader missing.");
            material=new Material(shader){name="Emberdeep grass patches"};
            material.SetColor("_BaseColor",Pigment);material.SetFloat("_Roughness",.82f);
            material.SetFloat("_Wind",1);material.SetFloat("_VertexState",1);
            mesh=new Mesh{name="Emberdeep grass patches"};mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;meshRenderer=gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial=material;meshRenderer.shadowCastingMode=ShadowCastingMode.Off;
            meshRenderer.lightProbeUsage=LightProbeUsage.Off;meshRenderer.reflectionProbeUsage=ReflectionProbeUsage.Off;meshRenderer.enabled=false;
        }

        // Called once per incident build. This object lives in arena space with the ground at y 0.
        public void Build(FireRun run)
        {
            vertices.Clear();normals.Clear();colors.Clear();states.Clear();indices.Clear();
            int patches=run!=null?run.Grass.Length:0;
            first=new int[patches];counts=new int[patches];shown=new Vector4[patches];
            for(int g=0;g<patches;g++)
            {
                first[g]=vertices.Count;
                var p=run.Grass[g].Position;Tuft(new Vector3(p.x,0,p.z),g);
                counts[g]=vertices.Count-first[g];
            }
            for(int v=0;v<vertices.Count;v++)states.Add(Vector4.zero);
            mesh.Clear(false);
            if(patches==0){meshRenderer.enabled=false;return;}
            mesh.indexFormat=vertices.Count>65000?IndexFormat.UInt32:IndexFormat.UInt16;
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetUVs(1,states);
            mesh.SetTriangles(indices,0,true);meshRenderer.enabled=true;
        }

        static float Hash(int i)=>Mathf.Repeat(Mathf.Sin(i*12.9898f+78.233f)*43758.5453f,1);
        void Tuft(Vector3 centre,int patch)
        {
            // A worn ground disc, so the patch reads on the meadow and as black char once burnt.
            int disc=vertices.Count;
            Vertex(centre+Vector3.up*.012f,Vector3.up,.85f,0);
            for(int k=0;k<7;k++)
            {
                float angle=k*Mathf.PI*2/7+patch;float r=Radius*(1.05f+.18f*Hash(patch*31+k));
                Vertex(centre+new Vector3(Mathf.Cos(angle)*r,.006f,Mathf.Sin(angle)*r),Vector3.up,.7f,0);
            }
            for(int k=0;k<7;k++)Triangle(disc,disc+1+(k+1)%7,disc+1+k,Vector3.up);
            for(int b=0;b<Blades;b++)
            {
                int seed=patch*97+b*13;
                // Golden-angle disc sampling: even cover without clumps.
                float angle=b*2.39996f+patch*1.7f,r=Radius*.92f*Mathf.Sqrt((b+.5f)/Blades);
                var foot=centre+new Vector3(Mathf.Cos(angle)*r,0,Mathf.Sin(angle)*r);
                float h=Height*Mathf.Lerp(.55f,1,Hash(seed));
                float lean=Mathf.Lerp(-1,1,Hash(seed+1))*.28f,turn=Hash(seed+2)*Mathf.PI*2;
                var tip=foot+new Vector3(Mathf.Cos(turn)*lean*h,h,Mathf.Sin(turn)*lean*h);
                float w=BladeWidth*Mathf.Lerp(.8f,1.2f,Hash(seed+3));
                for(int f=0;f<3;f++)
                {
                    float a0=turn+f*Mathf.PI*2/3,a1=a0+Mathf.PI*2/3;
                    var p0=foot+new Vector3(Mathf.Cos(a0),0,Mathf.Sin(a0))*w;
                    var p1=foot+new Vector3(Mathf.Cos(a1),0,Mathf.Sin(a1))*w;
                    var outward=new Vector3(Mathf.Cos(a0+Mathf.PI/3),0,Mathf.Sin(a0+Mathf.PI/3));
                    // Soft normals tipped upward: toy-like blades, not faceted shards.
                    var normal=(outward+Vector3.up*.9f).normalized;
                    int v=vertices.Count;
                    Vertex(p0,normal,.55f,0);Vertex(p1,normal,.55f,0);Vertex(tip,(normal+Vector3.up).normalized,1,1);
                    Triangle(v,v+1,v+2,outward);
                }
            }
        }
        void Vertex(Vector3 at,Vector3 normal,float ao,float sway)
        {vertices.Add(at);normals.Add(normal);colors.Add(new Color(ao,1,1,sway));}
        // Unity front faces wind clockwise as seen from outside: cross(b-a,c-a) points outward.
        void Triangle(int a,int b,int c,Vector3 outward)
        {
            if(Vector3.Dot(Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]),outward)<0){int t=b;b=c;c=t;}
            indices.Add(a);indices.Add(b);indices.Add(c);
        }

        // Per frame. pulse is the forecast ember's 0-1 pulse, shared with the tree rings.
        public void Tick(FireRun run,float pulse)
        {
            if(run==null || shown.Length==0 || run.Grass.Length!=shown.Length)return;
            bool dirty=false;
            for(int g=0;g<shown.Length;g++)
            {
                var data=run.Grass[g];bool burning=data.State==TreeState.Burning;
                float heat=burning?Mathf.Lerp(EmberdeepTreeReadout.MinimumHeatTint,1,Mathf.Clamp01(data.Heat/EmberdeepBoardTuning.Grass.IgniteHeat)):0;
                float scorch=data.State==TreeState.Burnt?1:data.State==TreeState.Saved?.15f:0;
                var state=new Vector4(heat,EmberdeepBoardTuning.WetLook(data.Wetness),scorch,Ember(run,run.Trees.Length+g,pulse));
                if((state-shown[g]).sqrMagnitude<1e-4f)continue;
                shown[g]=state;dirty=true;
                for(int v=first[g];v<first[g]+counts[g];v++)states[v]=state;
            }
            if(dirty)mesh.SetUVs(1,states);
        }
        // 1 for the next catch, dimmer for the one after; 0 for everything else.
        public static float Ember(FireRun run,int cell,float pulse)
        {
            for(int k=0;k<EmberdeepBoardTuning.ForecastSlots;k++)
                if(run.ForecastCell(k)==cell)return (k==0?1:.6f)*Mathf.Lerp(.45f,1,pulse);
            return 0;
        }

        void OnDestroy(){if(mesh)Destroy(mesh);if(material)Destroy(material);}
    }
}

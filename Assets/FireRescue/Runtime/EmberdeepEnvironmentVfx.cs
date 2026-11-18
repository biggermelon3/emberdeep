using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FireRescue
{
    // Decorative, bounded and arena-local. Never changes water or fire simulation.
    public sealed class EmberdeepEnvironmentVfx : MonoBehaviour
    {
        public const int DustCount=40, WindCount=10, MaximumTriangles=384;
        public float Clock { get; private set; }
        public int TriangleCount=>indices.Count/3;
        public Bounds LocalBounds=>mesh?mesh.bounds:default;
        public Mesh EffectMesh=>mesh;
        Transform arena;
        Mesh mesh;
        Material material;
        Renderer[] creek;
        readonly List<Renderer> bakedFoam=new List<Renderer>();
        MaterialPropertyBlock block;
        readonly List<Vector3> vertices=new List<Vector3>(640);
        readonly List<Vector4> uvs=new List<Vector4>(640);
        readonly List<Color> colors=new List<Color>(640);
        readonly List<int> indices=new List<int>(960);
        public void Initialize(Transform owner)
        {
            if(mesh || !owner)return;
            block=new MaterialPropertyBlock();
            arena=owner;transform.SetParent(arena,false);
            material=new Material(Resources.Load<Shader>("EmberdeepEnvironmentFx")){name="Emberdeep bounded atmosphere"};
            mesh=new Mesh{name="Emberdeep creek and atmosphere"};mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            var water=new List<Renderer>();
            foreach(var r in arena.GetComponentsInChildren<Renderer>())
                if(r.sharedMaterial && r.sharedMaterial.shader.name=="Emberdeep/V2 Shallow Creek")water.Add(r);
            creek=water.ToArray();
            foreach(var r in arena.GetComponentsInChildren<Renderer>())
                if(r.name.Contains("CreekFoam") && r.enabled){bakedFoam.Add(r);r.enabled=false;}

        }
        static float Hash(int i)=>Mathf.Repeat(Mathf.Sin(i*127.1f+19.7f)*43758.5453f,1);
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int style,Color color,float phase=0,float span=1)
        {
            int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
            uvs.Add(new Vector4(0,0,style,phase));uvs.Add(new Vector4(1,0,style,phase));
            uvs.Add(new Vector4(1,span,style,phase));uvs.Add(new Vector4(0,span,style,phase));
            for(int i=0;i<4;i++)colors.Add(color);
            indices.Add(n);indices.Add(n+1);indices.Add(n+2);indices.Add(n);indices.Add(n+2);indices.Add(n+3);
        }
        void Patch(Vector3 center,Vector3 right,Vector3 up,int style,Color color,float phase=0)
            =>Quad(center-right-up,center+right-up,center+right+up,center-right+up,style,color,phase);
        public void Tick(float dt,bool ready,Camera camera,FireRun run=null)
        {
            if(!mesh || !arena || !camera)return;
            if(ready && float.IsFinite(dt) && dt>0)Clock+=Mathf.Min(dt,.1f);
            material.SetFloat("_EffectTime",Clock);
            foreach(var r in creek)if(r)
            {
                r.GetPropertyBlock(block);block.SetMatrix("_WorldToCreek",arena.worldToLocalMatrix);
                block.SetFloat("_CreekFrameEnabled",1);block.SetFloat("_CreekTime",Clock);r.SetPropertyBlock(block);
            }
            vertices.Clear();uvs.Clear();colors.Clear();indices.Clear();
            for(int i=0;i<7;i++)
            {
                float x=Mathf.Lerp(4.05f,-4.05f,Mathf.Repeat(i/7f+Hash(i+140)*.06f+Clock*.050f,1));
                float z=2.1f+.035f*Mathf.Sin((x+4.2f)/.42f*.8f)+(Hash(i)-.5f)*.66f;
                Patch(new Vector3(x,.035f,z),Vector3.right*(.11f+Hash(i+10)*.12f),Vector3.forward*.035f,5,new Color(.70f,.94f,.90f,.27f),i*.7f);
            }
            Vector3 right=arena.InverseTransformDirection(camera.transform.right).normalized;
            Vector3 up=arena.InverseTransformDirection(camera.transform.up).normalized;
            for(int i=0;i<DustCount;i++)
            {
                float age=Mathf.Repeat(Clock*(.035f+Hash(i+90)*.02f)+Hash(i+2),1);
                Vector3 at=new Vector3(Mathf.Lerp(-4.3f,4.3f,age),3.1f+Hash(i+30)*4.2f+Mathf.Sin(Clock*.5f+i)*.15f,3.5f+Hash(i+60)*4.3f);
                float size=.028f+Hash(i+100)*.032f,fade=Mathf.SmoothStep(0,1,Mathf.Min(age,1-age)*8);
                Patch(at,right*size,up*size,2,new Color(.92f,.84f,.60f,fade*.38f),i);
            }
            for(int i=0;i<WindCount;i++)
            {
                float age=Mathf.Repeat(Clock*.14f+i*.34f,1),fade=Mathf.Sin(age*Mathf.PI);
                var eye=arena.InverseTransformPoint(camera.transform.position);
                bool near=i<3;
                var center=new Vector3(Mathf.Lerp(-5.8f,5.8f,age)+(near?eye.x:0),
                    near?Mathf.Max(2,eye.y-3.2f+i*.45f):1.35f+(i%3)*.65f,
                    near?eye.z+2.2f+i*1.35f:2+(i-3)*1.5f);
                for(int j=0;j<10;j++)
                {
                    float a=j/10f,b=(j+1)/10f;
                    Vector3 Point(float t)=>center+new Vector3((t-.5f)*1.65f,Mathf.Sin(t*5+Clock*.6f+i)*.08f,Mathf.Sin(t*3+i)*.10f);
                    float wa=Mathf.Sin(a*Mathf.PI)*.027f,wb=Mathf.Sin(b*Mathf.PI)*.027f;
                    Quad(Point(a)-up*wa,Point(a)+up*wa,Point(b)+up*wb,Point(b)-up*wb,3,new Color(.67f,.84f,.81f,fade*(near?.11f:.19f)));
                }
            }
            // Four bounded updrafts beside actual burning trunks. They stop as
            // soon as that tree is saved; no scene-wide fake smoke or wind source.
            if(run!=null)
            {
                int drawn=0;
                for(int i=0;i<run.Trees.Length && drawn<4;i++)
                {
                    var tree=run.Trees[i];if(tree.State!=TreeState.Burning)continue;drawn++;
                    for(int j=0;j<8;j++)
                    {
                        float a=j/8f,b=(j+1)/8f;
                        Vector3 Point(float t){float angle=t*4+Clock*1.3f+i;return tree.Position+new Vector3(Mathf.Cos(angle)*(.35f+t*.16f),-.3f+t*2.15f,Mathf.Sin(angle)*(.35f+t*.16f));}
                        float wa=Mathf.Sin(a*Mathf.PI)*.025f,wb=Mathf.Sin(b*Mathf.PI)*.025f;
                        Quad(Point(a)-right*wa,Point(a)+right*wa,Point(b)+right*wb,Point(b)-right*wb,3,new Color(1,.86f,.61f,.28f));
                    }
                }
            }
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetUVs(0,uvs);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
        }
        void OnDestroy(){foreach(var r in bakedFoam)if(r)r.enabled=true;if(mesh)Destroy(mesh);if(material)Destroy(material);}
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FireRescue
{
    // One dynamic mesh drawn with the bounded "EmberdeepEnvironmentFx" shader: soft dots
    // (uv.z 0) and soft-edged bands (uv.z 3, u across the band). Presentation only.
    public sealed class EmberdeepFxBuilder
    {
        public readonly Mesh Mesh;
        public readonly Material Material;
        readonly List<Vector3> vertices=new();readonly List<int> indices=new();readonly List<Color> colors=new();readonly List<Vector4> uv=new();
        public int Quads=>indices.Count/6;
        public EmberdeepFxBuilder(GameObject host,string name)
        {
            Mesh=new Mesh{name=name};Mesh.MarkDynamic();
            host.AddComponent<MeshFilter>().sharedMesh=Mesh;
            var renderer=host.AddComponent<MeshRenderer>();
            Material=new Material(Resources.Load<Shader>("EmberdeepEnvironmentFx")){name=name};
            renderer.sharedMaterial=Material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            Renderer=renderer;
        }
        public MeshRenderer Renderer {get;}
        public void Begin(){vertices.Clear();indices.Clear();colors.Clear();uv.Clear();}
        // Builds in world space; the host transform is reset to identity by the caller.
        public void End(){Mesh.Clear();Mesh.SetVertices(vertices);Mesh.SetColors(colors);Mesh.SetUVs(0,uv);Mesh.SetTriangles(indices,0);Mesh.RecalculateBounds();}
        public void Dot(Vector3 center,Vector3 right,Vector3 up,float radius,Color color)
        {
            if(radius<=0 || color.a<=.001f)return;
            Quad(center-right*radius-up*radius,center+right*radius-up*radius,center+right*radius+up*radius,center-right*radius+up*radius,color,0);
        }
        public void Ring(Vector3 center,Vector3 axisA,Vector3 axisB,float radius,float width,Color color,int segments=32)
        {
            if(radius<=0 || width<=0 || color.a<=.001f)return;
            for(int i=0;i<segments;i++)
            {
                float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;
                var ra=axisA*Mathf.Cos(a)+axisB*Mathf.Sin(a);var rb=axisA*Mathf.Cos(b)+axisB*Mathf.Sin(b);
                Quad(center+ra*(radius-width),center+ra*(radius+width),center+rb*(radius+width),center+rb*(radius-width),color,3);
            }
        }
        // Camera-facing band through the points; width and alpha taper toward both ends.
        public void Ribbon(IList<Vector3> points,int count,Vector3 eye,float width,Color color,float taper=.15f)
        {
            if(count<2 || width<=0 || color.a<=.001f)return;
            for(int i=0;i<count-1;i++)
            {
                var a=points[i];var b=points[i+1];var along=b-a;if(along.sqrMagnitude<1e-10f)continue;
                float ta=i/(float)(count-1),tb=(i+1)/(float)(count-1);
                float wa=width*Taper(ta,taper),wb=width*Taper(tb,taper);
                var sa=Vector3.Cross(along,eye-a).normalized;var sb=Vector3.Cross(along,eye-b).normalized;
                int n=vertices.Count;
                vertices.Add(a-sa*wa);vertices.Add(a+sa*wa);vertices.Add(b+sb*wb);vertices.Add(b-sb*wb);
                var ca=color;ca.a*=Taper(ta,taper);var cb=color;cb.a*=Taper(tb,taper);
                colors.Add(ca);colors.Add(ca);colors.Add(cb);colors.Add(cb);
                uv.Add(new Vector4(0,0,3,0));uv.Add(new Vector4(1,0,3,0));uv.Add(new Vector4(1,1,3,0));uv.Add(new Vector4(0,1,3,0));
                indices.Add(n);indices.Add(n+1);indices.Add(n+2);indices.Add(n);indices.Add(n+2);indices.Add(n+3);
            }
        }
        static float Taper(float t,float edge)=>edge<=0?1:Mathf.Clamp01(Mathf.Min(t,1-t)/edge)*.7f+.3f;
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,float style)
        {
            int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
            for(int j=0;j<4;j++)colors.Add(color);
            uv.Add(new Vector4(0,0,style,0));uv.Add(new Vector4(1,0,style,0));uv.Add(new Vector4(1,1,style,0));uv.Add(new Vector4(0,1,style,0));
            indices.Add(n);indices.Add(n+1);indices.Add(n+2);indices.Add(n);indices.Add(n+2);indices.Add(n+3);
        }
        public void Destroy(){if(Mesh)Object.Destroy(Mesh);if(Material)Object.Destroy(Material);}
    }
}

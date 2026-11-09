using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PhysicalAssembly
{
    [RequireComponent(typeof(BuildingArea))]
    public sealed class SandboxBuildVolume : MonoBehaviour
    {
        public Color Color=new(.05f,.9f,1,.5f);
        [Range(.01f,.25f)] public float BandFraction=.04f;
        Mesh mesh; Material material; MeshRenderer visual;
        public void SetVisible(bool value) { if(visual) visual.enabled=value; }
        public void Rebuild()
        {
            if(mesh) Destroy(mesh);
            var area=GetComponent<BuildingArea>(); var vertices=new List<Vector3>(); var colors=new List<Color>(); var indices=new List<int>();
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float outer,float inner) {
                int n=vertices.Count; vertices.AddRange(new[]{a,b,c,d});
                colors.Add(new Color(Color.r,Color.g,Color.b,outer)); colors.Add(new Color(Color.r,Color.g,Color.b,outer));
                colors.Add(new Color(Color.r,Color.g,Color.b,inner)); colors.Add(new Color(Color.r,Color.g,Color.b,inner));
                indices.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            // Four trapezoids per face leave the entire centre absent, not merely alpha zero.
            for(int axis=0;axis<3;axis++) for(int sign=-1;sign<=1;sign+=2) {
                var normal=Vector3.zero; normal[axis]=area.Size[axis]*.5f*sign;
                var u=Vector3.zero; u[(axis+1)%3]=area.Size[(axis+1)%3]*.5f;
                var v=Vector3.zero; v[(axis+2)%3]=area.Size[(axis+2)%3]*.5f;
                var corners=new[]{normal-u-v,normal+u-v,normal+u+v,normal-u+v};
                for(int edge=0;edge<4;edge++) {
                    var a=corners[edge]; var b=corners[(edge+1)%4];
                    var aa=Vector3.Lerp(a,normal,.008f); var bb=Vector3.Lerp(b,normal,.008f);
                    Quad(a,b,bb,aa,.85f,Color.a);
                    Quad(aa,bb,Vector3.Lerp(b,normal,BandFraction*2),Vector3.Lerp(a,normal,BandFraction*2),Color.a,0);
                }
            }
            mesh=new Mesh {name="Hollow sandbox boundary bands"}; mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(indices,0); mesh.RecalculateBounds();
            var filter=GetComponent<MeshFilter>(); if(!filter) filter=gameObject.AddComponent<MeshFilter>(); filter.sharedMesh=mesh;
            visual=GetComponent<MeshRenderer>(); if(!visual) visual=gameObject.AddComponent<MeshRenderer>();
            if(!material) material=new Material(Resources.Load<Shader>("SandboxBoundary"));
            visual.sharedMaterial=material; visual.shadowCastingMode=ShadowCastingMode.Off; visual.receiveShadows=false;
        }
        void OnDestroy() { if(mesh) Destroy(mesh); if(material) Destroy(material); }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FireRescue
{
    // A faint chain of moving motes connects the physical palm to its ground circle.
    // One small mesh, no collisions and no fire/resource authority.
    public sealed class EmberdeepPalmAimVfx : MonoBehaviour
    {
        Mesh mesh;Material material;MeshRenderer display;
        readonly List<Vector3> vertices=new();readonly List<int> indices=new();readonly List<Color> colors=new();readonly List<Vector4> uv=new();
        float clock;
        void Awake()
        {
            mesh=new Mesh{name="Palm aiming motes"};mesh.MarkDynamic();gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            display=gameObject.AddComponent<MeshRenderer>();material=new Material(Resources.Load<Shader>("EmberdeepEnvironmentFx"));display.sharedMaterial=material;
            display.shadowCastingMode=ShadowCastingMode.Off;display.receiveShadows=false;display.enabled=false;
        }
        public void Show(bool visible,Vector3 origin,Vector3 target,Camera camera,bool charged,float dt,bool casting=false,float radius=.05f,Vector3 groundNormal=default)
        {
            display.enabled=visible && camera;if(!display.enabled)return;
            if(float.IsFinite(dt))clock+=Mathf.Clamp(dt,0,.1f);material.SetFloat("_EffectTime",clock);
            vertices.Clear();indices.Clear();colors.Clear();uv.Clear();
            for(int i=0;i<18;i++)
            {
                float t=Mathf.Repeat(i/18f+clock*.45f,1),a=i*2.4f+clock*2;
                float width=casting?.012f:.008f;
                Vector3 point=Vector3.Lerp(origin,target,t)+(camera.transform.right*Mathf.Sin(a)+camera.transform.up*Mathf.Cos(a)*.45f)*width*Mathf.Sin(t*Mathf.PI);
                float size=.0025f+.0015f*Mathf.Sin(t*Mathf.PI);
                Vector3 x=camera.transform.right*size,y=camera.transform.up*size;int n=vertices.Count;
                vertices.AddRange(new[]{point-x-y,point+x-y,point+x+y,point-x+y});
                float alpha=Mathf.Sin(t*Mathf.PI)*.65f;var color=charged?new Color(.35f,.9f,1,alpha):new Color(.55f,.65f,.7f,alpha*.6f);
                for(int j=0;j<4;j++)colors.Add(color);
                uv.Add(new Vector4(0,0,2,i));uv.Add(new Vector4(1,0,2,i));uv.Add(new Vector4(1,1,2,i));uv.Add(new Vector4(0,1,2,i));indices.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            if(casting && charged)
            {
                var normal=groundNormal.sqrMagnitude>.01f?groundNormal.normalized:Vector3.up;
                var right=Vector3.Cross(normal,Vector3.forward).normalized;if(right.sqrMagnitude<.1f)right=Vector3.right;
                var forward=Vector3.Cross(right,normal).normalized;
                radius=float.IsFinite(radius)?Mathf.Clamp(radius,.005f,.5f):.05f;
                Ring(target+normal*.0025f,right,forward,radius*(.38f+.45f*Mathf.Repeat(clock*.8f,1)),.0015f,new Color(.35f,.86f,.92f,.34f));
                var direction=(target-origin).normalized;var side=Vector3.Cross(direction,camera.transform.up).normalized;
                if(side.sqrMagnitude<.1f)side=camera.transform.right;
                Ring(origin+direction*.008f,side,Vector3.Cross(direction,side).normalized,.016f,.0012f,new Color(.5f,.94f,1,.55f));
            }
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
        }
        void Ring(Vector3 center,Vector3 right,Vector3 forward,float radius,float width,Color color)
        {
            const int segments=24;
            for(int i=0;i<segments;i++)
            {
                float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;int n=vertices.Count;
                var ra=right*Mathf.Cos(a)+forward*Mathf.Sin(a);var rb=right*Mathf.Cos(b)+forward*Mathf.Sin(b);
                vertices.AddRange(new[]{center+ra*(radius-width),center+ra*(radius+width),center+rb*(radius+width),center+rb*(radius-width)});
                for(int j=0;j<4;j++)colors.Add(color);
                uv.AddRange(new[]{new Vector4(0,0,5,0),new Vector4(1,0,5,0),new Vector4(1,1,5,0),new Vector4(0,1,5,0)});
                indices.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
        }
        public void Hide(){if(display)display.enabled=false;}
        void OnDestroy(){if(mesh)Destroy(mesh);if(material)Destroy(material);}
    }
}

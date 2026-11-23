using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FireRescue
{
    // Palm water aim feedback. One small mesh, no collisions and no fire/resource authority.
    // Show: the original faint mote chain. ShowAim (LoopSpec §3.7): a bright ballistic arc to
    // the first surface the water hits, a marker on that surface, and a ring around every
    // crown inside the splash radius (own mesh, so it never touches the tree heat tint).
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
        public struct Crown {public Vector3 Center;public float Radius;public bool Burning;}
        readonly List<Vector3> path=new();
        public int LastCrownCount {get;private set;}
        public bool LastHasImpact {get;private set;}
        public Vector3 LastImpact {get;private set;}
        public int LastArcPoints=>path.Count;
        public bool LastArcShown {get;private set;} // false while casting: the stream is the curve (ArcCastingAlpha)
        // velocity/origin match the WaterJet stream; impact is WaterJet.Predict's first hit.
        public void ShowAim(bool visible,Vector3 origin,Vector3 velocity,float range,bool hasImpact,Vector3 impact,Vector3 normal,
            Camera camera,bool charged,bool casting,float dt,IReadOnlyList<Crown> crowns,int crownCount,Vector3 up)
        {
            display.enabled=visible && camera && float.IsFinite(origin.sqrMagnitude) && float.IsFinite(velocity.sqrMagnitude) && velocity.sqrMagnitude>1e-6f;
            LastCrownCount=0;LastHasImpact=false;LastArcShown=false;
            if(!display.enabled)return;
            if(float.IsFinite(dt))clock+=Mathf.Clamp(dt,0,.1f);material.SetFloat("_EffectTime",clock);
            vertices.Clear();indices.Clear();colors.Clear();uv.Clear();
            var eye=camera.transform.position;var cr=camera.transform.right;var cu=camera.transform.up;
            up=up.sqrMagnitude>.01f?up.normalized:Vector3.up;
            // Flight time to the impact from the horizontal distance (exact for a ballistic packet).
            var flat=Vector3.ProjectOnPlane(velocity,Vector3.up);float speed=flat.magnitude;
            float duration=hasImpact && speed>.01f?Vector3.ProjectOnPlane(impact-origin,Vector3.up).magnitude/speed:
                range/Mathf.Max(.1f,velocity.magnitude);
            duration=Mathf.Clamp(float.IsFinite(duration)?duration:.3f,.02f,1.5f);
            path.Clear();const int points=28;
            for(int i=0;i<=points;i++){float t=duration*i/points;path.Add(origin+velocity*t+WaterJet.Gravity*(.5f*t*t));}
            if(hasImpact)path[path.Count-1]=impact;
            var water=charged?new Color(.42f,.93f,1,1):new Color(.58f,.66f,.70f,1);
            float width=EmberdeepWaterTuning.ArcWidth*(casting?1.25f:1);
            // While the water flows (casting) the stream is the curve: no second arc on top of it (EmberdeepWaterTuning.ArcCastingAlpha).
            float arcAlpha=casting?EmberdeepWaterTuning.ArcCastingAlpha:EmberdeepWaterTuning.ArcAlpha;
            LastArcShown=arcAlpha>0;
            if(LastArcShown)
            {
                var arc=water;arc.a=arcAlpha*(charged?1:.55f);
                Ribbon(path,eye,width,arc);
                var core=new Color(.85f,1,1,arc.a*.8f);Ribbon(path,eye,width*.35f,core);
                for(int i=0;i<12;i++)
                {
                    float t=Mathf.Repeat(i/12f+clock*(casting?.9f:.5f),1);
                    var at=Sample(t);float size=(casting?.0042f:.0034f)*(.6f+.4f*Mathf.Sin(t*Mathf.PI));
                    Dot(at,cr,cu,size,new Color(.85f,1,1,Mathf.Sin(t*Mathf.PI)*(charged?.9f:.4f)));
                }
            }
            if(hasImpact)
            {
                LastHasImpact=true;LastImpact=impact;
                var n=normal.sqrMagnitude>.01f?normal.normalized:up;
                var a=Vector3.Cross(n,Mathf.Abs(Vector3.Dot(n,camera.transform.forward))>.95f?cu:camera.transform.forward).normalized;
                var b=Vector3.Cross(n,a).normalized;
                float r=EmberdeepWaterTuning.ImpactMarkerRadius*(casting?1.15f:1);
                var mark=new Color(water.r,water.g,water.b,charged?.95f:.6f);
                RingBand(impact+n*.002f,a,b,r,.0022f,mark,24);
                float pulse=Mathf.Repeat(clock*1.6f,1);
                RingBand(impact+n*.002f,a,b,r*(1.2f+1.3f*pulse),.0014f,new Color(mark.r,mark.g,mark.b,mark.a*(1-pulse)*.8f),24);
                Dot(impact+n*.003f+(eye-impact).normalized*.002f,cr,cu,.0045f,new Color(.9f,1,1,mark.a));
            }
            if(crowns!=null)for(int i=0;i<Mathf.Min(crownCount,crowns.Count);i++)
            {
                var c=crowns[i];if(!float.IsFinite(c.Center.sqrMagnitude) || c.Radius<=0)continue;
                var ax=Vector3.Cross(up,Vector3.forward);if(ax.sqrMagnitude<.01f)ax=Vector3.Cross(up,Vector3.right);ax.Normalize();
                var bx=Vector3.Cross(up,ax).normalized;
                float bob=.08f*Mathf.Sin(clock*3+i*1.3f);
                var ring=c.Burning?new Color(.55f,.95f,1,.95f):new Color(.40f,.88f,1,.78f);if(!charged)ring=new Color(.6f,.68f,.72f,.5f);
                RingBand(c.Center+up*(c.Radius*bob),ax,bx,c.Radius,EmberdeepWaterTuning.CrownRingWidth,ring,32);
                RingBand(c.Center+up*(c.Radius*bob),ax,bx,c.Radius*1.12f,EmberdeepWaterTuning.CrownRingWidth*.45f,new Color(ring.r,ring.g,ring.b,ring.a*.45f),32);
                LastCrownCount++;
            }
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
        }
        Vector3 Sample(float t)
        {
            if(path.Count==0)return default;
            float f=Mathf.Clamp01(t)*(path.Count-1);int i=Mathf.Min(path.Count-2,Mathf.FloorToInt(f));
            return i<0?path[0]:Vector3.Lerp(path[i],path[i+1],f-i);
        }
        void Dot(Vector3 p,Vector3 right,Vector3 up,float size,Color color)
        {
            int n=vertices.Count;var x=right*size;var y=up*size;
            vertices.Add(p-x-y);vertices.Add(p+x-y);vertices.Add(p+x+y);vertices.Add(p-x+y);
            for(int j=0;j<4;j++)colors.Add(color);
            uv.Add(new Vector4(0,0,0,0));uv.Add(new Vector4(1,0,0,0));uv.Add(new Vector4(1,1,0,0));uv.Add(new Vector4(0,1,0,0));
            indices.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
        }
        void RingBand(Vector3 center,Vector3 right,Vector3 forward,float radius,float width,Color color,int segments)
        {
            for(int i=0;i<segments;i++)
            {
                float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;int n=vertices.Count;
                var ra=right*Mathf.Cos(a)+forward*Mathf.Sin(a);var rb=right*Mathf.Cos(b)+forward*Mathf.Sin(b);
                vertices.Add(center+ra*(radius-width));vertices.Add(center+ra*(radius+width));vertices.Add(center+rb*(radius+width));vertices.Add(center+rb*(radius-width));
                for(int j=0;j<4;j++)colors.Add(color);
                uv.Add(new Vector4(0,0,3,0));uv.Add(new Vector4(1,0,3,0));uv.Add(new Vector4(1,1,3,0));uv.Add(new Vector4(0,1,3,0));
                indices.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
        }
        void Ribbon(List<Vector3> points,Vector3 eye,float width,Color color)
        {
            for(int i=0;i<points.Count-1;i++)
            {
                var a=points[i];var b=points[i+1];var along=b-a;if(along.sqrMagnitude<1e-10f)continue;
                var sa=Vector3.Cross(along,eye-a).normalized*width;var sb=Vector3.Cross(along,eye-b).normalized*width;
                // Fade in from the palm so the ribbon never covers the hand itself.
                float fa=Mathf.Clamp01(i/3f),fb=Mathf.Clamp01((i+1)/3f);
                int n=vertices.Count;
                vertices.Add(a-sa);vertices.Add(a+sa);vertices.Add(b+sb);vertices.Add(b-sb);
                var ca=color;ca.a*=fa;var cb=color;cb.a*=fb;
                colors.Add(ca);colors.Add(ca);colors.Add(cb);colors.Add(cb);
                uv.Add(new Vector4(0,0,3,0));uv.Add(new Vector4(1,0,3,0));uv.Add(new Vector4(1,1,3,0));uv.Add(new Vector4(0,1,3,0));
                indices.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
        }
        public void Hide(){if(display)display.enabled=false;}
        void OnDestroy(){if(mesh)Destroy(mesh);if(material)Destroy(material);}
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FireRescue
{
    // Flat procedural icons for the layout A UI (LoopSpec §11b): sheep, star, coin, lock, pause bars, chevron,
    // ring arcs, plus a lathed bell. Icons are double-sided, unit sized (about 1 across) and lie in XY facing -Z.
    // Meshes are shared and built once; ring arcs are per-owner because their sweep changes.
    public static class EmberdeepUiIcons
    {
        static readonly Dictionary<string,Mesh> cache=new();
        public static Mesh Sheep=>Get("sheep",BuildSheep);
        public static Mesh Star=>Get("star",BuildStar);
        public static Mesh Coin=>Get("coin",BuildCoin);
        public static Mesh Lock=>Get("lock",BuildLock);
        public static Mesh PauseBars=>Get("pause",BuildPause);
        public static Mesh Chevron=>Get("chevron",BuildChevron);
        public static Mesh Disc=>Get("disc",()=>Polygon(Circle(Vector2.zero,.5f,.5f,28)));
        public static Mesh Bell=>Get("bell",BuildBell);
        public static Mesh Drop=>Get("drop",BuildDrop);
        public static Mesh Fist=>Get("fist",BuildFist);
        public static Mesh Triangle=>Get("triangle",()=>Polygon(new List<Vector2>{new Vector2(-.3f,-.42f),new Vector2(-.3f,.42f),new Vector2(.42f,0)}));
        public static Mesh Retry=>Get("retry",()=>{var m=new MeshBuilder();m.Ring(Vector2.zero,.42f,.28f,Mathf.PI*.62f,Mathf.PI*2.38f,32,0);
            m.Fan(new List<Vector2>{new Vector2(-.36f,.42f),new Vector2(-.02f,.5f),new Vector2(-.22f,.18f)});return m.Done();});
        public static Mesh Tent=>Get("tent",()=>{var m=new MeshBuilder();m.Fan(new List<Vector2>{new Vector2(-.48f,-.36f),new Vector2(0,.42f),new Vector2(.48f,-.36f)});
            m.Fan(new List<Vector2>{new Vector2(-.1f,-.36f),new Vector2(0,-.06f),new Vector2(.1f,-.36f)});m.Fan(Rect(-.5f,-.44f,1,.07f));return m.Done();});
        public static Mesh Up=>Get("up",()=>{var m=new MeshBuilder();m.Fan(new List<Vector2>{new Vector2(-.4f,0),new Vector2(0,.46f),new Vector2(.4f,0)});m.Fan(Rect(-.14f,-.46f,.28f,.48f));return m.Done();});
        public static Mesh Target=>Get("target",()=>{var m=new MeshBuilder();m.Ring(Vector2.zero,.46f,.38f,0,Mathf.PI*2,32,0);m.Ring(Vector2.zero,.24f,.16f,0,Mathf.PI*2,24,0);
            m.Fan(Circle(Vector2.zero,.06f,.06f,12));return m.Done();});
        public static Mesh Back=>Get("back",()=>{var m=new MeshBuilder();
            m.Quad(new Vector2(.2f,.5f),new Vector2(-.05f,.5f),new Vector2(-.3f,0),new Vector2(-.05f,0),0);
            m.Quad(new Vector2(-.05f,0),new Vector2(-.3f,0),new Vector2(-.05f,-.5f),new Vector2(.2f,-.5f),0);return m.Done();});
        public static Mesh Tree=>Get("tree",BuildTree);
        public static Mesh Plane=>Get("plane",BuildPlane);
        // Board placement on the pause panel (as the camp settings page's icons): minus, plus, height, distance, tilt.
        public static Mesh Minus=>Get("minus",()=>Polygon(Rect(-.34f,-.07f,.68f,.14f)));
        public static Mesh Plus=>Get("plus",()=>Polygons(new List<List<Vector2>>{Rect(-.34f,-.07f,.68f,.14f),Rect(-.07f,-.34f,.14f,.68f)}));
        public static Mesh Height=>Get("height",()=>Polygons(new List<List<Vector2>>{Rect(-.07f,-.3f,.14f,.6f),
            new(){new Vector2(-.3f,.2f),new Vector2(0,.5f),new Vector2(.3f,.2f)},new(){new Vector2(-.3f,-.2f),new Vector2(.3f,-.2f),new Vector2(0,-.5f)},
            Rect(-.48f,-.04f,.24f,.08f),Rect(.24f,-.04f,.24f,.08f)}));
        public static Mesh Distance=>Get("distance",()=>Polygons(new List<List<Vector2>>{
            new(){new Vector2(-.5f,-.5f),new Vector2(-.3f,-.18f),new Vector2(.3f,-.18f),new Vector2(.5f,-.5f)},
            Rect(-.06f,-.1f,.12f,.36f),new(){new Vector2(-.24f,.24f),new Vector2(0,.5f),new Vector2(.24f,.24f)}}));
        public static Mesh Tilt=>Get("tilt",()=>Polygons(new List<List<Vector2>>{Rect(-.48f,-.46f,.96f,.08f),
            new(){new Vector2(-.42f,-.34f),new Vector2(-.48f,-.236f),new Vector2(.30f,.214f),new Vector2(.36f,.11f)},Circle(new Vector2(-.42f,-.34f),.07f,.07f,10)}));
        static Mesh BuildTree()
        {
            var m=new MeshBuilder();
            for(int i=0;i<3;i++){float y=-.18f+i*.2f,w=.42f-i*.1f;m.Fan(new List<Vector2>{new Vector2(-w,y),new Vector2(0,y+.32f),new Vector2(w,y)});}
            m.Fan(Rect(-.06f,-.5f,.12f,.34f));
            return m.Done();
        }
        static Mesh BuildPlane()
        {
            var m=new MeshBuilder();
            m.Fan(new List<Vector2>{new Vector2(0,.5f),new Vector2(.07f,.3f),new Vector2(.07f,-.32f),new Vector2(0,-.42f),new Vector2(-.07f,-.32f),new Vector2(-.07f,.3f)});
            m.Fan(new List<Vector2>{new Vector2(-.48f,-.02f),new Vector2(0,.16f),new Vector2(.48f,-.02f),new Vector2(0,.02f)});
            m.Fan(new List<Vector2>{new Vector2(-.18f,-.42f),new Vector2(0,-.3f),new Vector2(.18f,-.42f),new Vector2(0,-.36f)});
            return m.Done();
        }

        static Mesh Get(string key,System.Func<Mesh> build)
        {
            if(cache.TryGetValue(key,out var mesh) && mesh)return mesh;
            mesh=build();mesh.name="Emberdeep icon "+key;mesh.hideFlags=HideFlags.DontSave;cache[key]=mesh;return mesh;
        }

        // ---- Builders ---------------------------------------------------------------------------------
        static Mesh BuildSheep()
        {
            var parts=new List<List<Vector2>>();
            // Wool: a scalloped oval. Head on the right, two legs below.
            var wool=new List<Vector2>();
            for(int i=0;i<36;i++){float a=i*Mathf.PI*2/36;float r=1+.09f*Mathf.Abs(Mathf.Sin(a*4.5f));wool.Add(new Vector2(-.08f+Mathf.Cos(a)*.36f*r,.04f+Mathf.Sin(a)*.25f*r));}
            parts.Add(wool);
            parts.Add(Circle(new Vector2(.36f,.12f),.13f,.10f,16));
            parts.Add(Rect(-.26f,-.34f,.07f,.16f));parts.Add(Rect(.06f,-.34f,.07f,.16f));
            return Polygons(parts);
        }
        static Mesh BuildStar()
        {
            var p=new List<Vector2>();
            for(int i=0;i<10;i++){float a=Mathf.PI*.5f+i*Mathf.PI/5;float r=i%2==0?.5f:.22f;p.Add(new Vector2(Mathf.Cos(a)*r,Mathf.Sin(a)*r));}
            return Polygon(p);
        }
        static Mesh BuildCoin()
        {
            var m=new MeshBuilder();
            m.Fan(Circle(Vector2.zero,.5f,.5f,28));
            m.Ring(Vector2.zero,.36f,.30f,0,Mathf.PI*2,28,.02f); // embossed rim, slightly proud of the face
            return m.Done();
        }
        static Mesh BuildLock()
        {
            var m=new MeshBuilder();
            m.Fan(Rect(-.3f,-.42f,.6f,.48f));
            m.Ring(new Vector2(0,.06f),.24f,.16f,0,Mathf.PI,16,0);
            m.Quad(new Vector2(-.24f,.0f),new Vector2(-.16f,.0f),new Vector2(-.16f,.07f),new Vector2(-.24f,.07f),0);
            m.Quad(new Vector2(.16f,.0f),new Vector2(.24f,.0f),new Vector2(.24f,.07f),new Vector2(.16f,.07f),0);
            return m.Done();
        }
        static Mesh BuildPause()
        {
            var m=new MeshBuilder();m.Fan(Rect(-.3f,-.4f,.2f,.8f));m.Fan(Rect(.1f,-.4f,.2f,.8f));return m.Done();
        }
        static Mesh BuildChevron()
        {
            var m=new MeshBuilder();
            m.Quad(new Vector2(-.2f,.5f),new Vector2(.05f,.5f),new Vector2(.3f,0),new Vector2(.05f,0),0);
            m.Quad(new Vector2(.05f,0),new Vector2(.3f,0),new Vector2(.05f,-.5f),new Vector2(-.2f,-.5f),0);
            return m.Done();
        }
        static Mesh BuildDrop()
        {
            // Teardrop: the top point plus the round bottom from 30 deg clockwise to 150 deg (both tangent-ish).
            var p=new List<Vector2>{new Vector2(0,.5f)};
            for(int i=0;i<=20;i++){float a=(30-i*12)*Mathf.Deg2Rad;p.Add(new Vector2(Mathf.Cos(a)*.3f,-.12f+Mathf.Sin(a)*.3f));}
            return Polygon(p);
        }
        static Mesh BuildFist()
        {
            var m=new MeshBuilder();
            m.Fan(Rect(-.3f,-.25f,.55f,.42f));
            for(int i=0;i<4;i++)m.Fan(Circle(new Vector2(-.24f+i*.15f,.2f),.085f,.085f,12));
            m.Fan(Rect(-.22f,-.5f,.38f,.26f));
            return m.Done();
        }
        // A tall flared bell with a knob on top, open at the bottom, 8.5 cm: seen from the seated eye (~60 deg above)
        // the flare and the waist still give it a bell silhouette, not a disc.
        static Mesh BuildBell()
        {
            var profile=new[]{new Vector2(0,.085f),new Vector2(.007f,.085f),new Vector2(.009f,.079f),new Vector2(.006f,.073f),
                new Vector2(.012f,.070f),new Vector2(.019f,.064f),new Vector2(.022f,.054f),new Vector2(.022f,.040f),
                new Vector2(.024f,.026f),new Vector2(.029f,.014f),new Vector2(.036f,.005f),new Vector2(.040f,.001f),
                new Vector2(.040f,-.002f),new Vector2(.034f,-.001f),new Vector2(.026f,.008f),new Vector2(.020f,.024f),new Vector2(0,.045f)};
            const int sides=24;var v=new List<Vector3>();var n=new List<Vector3>();var t=new List<int>();
            for(int s=0;s<=sides;s++)
            {
                float a=s*Mathf.PI*2/sides;float c=Mathf.Cos(a),si=Mathf.Sin(a);
                for(int k=0;k<profile.Length;k++)
                {
                    var p=profile[k];v.Add(new Vector3(p.x*c,p.y,p.x*si));
                    var prev=profile[Mathf.Max(0,k-1)];var next=profile[Mathf.Min(profile.Length-1,k+1)];
                    var tangent=(next-prev).normalized;var normal2=new Vector2(-tangent.y,tangent.x); // outward for a top-down profile
                    n.Add(new Vector3(normal2.x*c,normal2.y,normal2.x*si).normalized);
                }
            }
            int rows=profile.Length;
            for(int s=0;s<sides;s++)for(int k=0;k<rows-1;k++)
            {int a=s*rows+k,b=(s+1)*rows+k;t.AddRange(new[]{a,b,a+1,b,b+1,a+1});} // outward-facing (clockwise from outside)
            var mesh=new Mesh();mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetTriangles(t,0);mesh.RecalculateBounds();return mesh;
        }

        // ---- Ring arc (owner keeps the mesh) ------------------------------------------------------------
        // Sweep from 12 o'clock clockwise, 0..1 of a full turn.
        public static void Arc(Mesh mesh,float outer,float inner,float sweep01,int segments=40)
        {
            var m=new MeshBuilder();sweep01=Mathf.Clamp01(sweep01);
            if(sweep01>.001f)m.Ring(Vector2.zero,outer,inner,Mathf.PI*.5f-sweep01*Mathf.PI*2,Mathf.PI*.5f,Mathf.Max(2,Mathf.CeilToInt(segments*sweep01)),0);
            m.Into(mesh);
        }

        // ---- Shared helpers -----------------------------------------------------------------------------
        public static Renderer Icon(string name,Transform parent,Mesh mesh,Material material,Vector3 at,float size)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=at;go.transform.localScale=Vector3.one*size;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;
            r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;return r;
        }
        static List<Vector2> Circle(Vector2 c,float rx,float ry,int n)
        {var p=new List<Vector2>();for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n;p.Add(c+new Vector2(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry));}return p;}
        static List<Vector2> Rect(float x,float y,float w,float h)=>new(){new Vector2(x,y),new Vector2(x,y+h),new Vector2(x+w,y+h),new Vector2(x+w,y)};
        static Mesh Polygon(List<Vector2> p){var m=new MeshBuilder();m.Fan(p);return m.Done();}
        static Mesh Polygons(List<List<Vector2>> parts){var m=new MeshBuilder();foreach(var p in parts)m.Fan(p);return m.Done();}

        sealed class MeshBuilder
        {
            readonly List<Vector3> v=new();readonly List<int> t=new();
            // Convex-ish outline as a centre fan, both windings.
            public void Fan(List<Vector2> p)
            {
                var c=Vector2.zero;foreach(var q in p)c+=q;c/=p.Count;
                int start=v.Count;v.Add(c);foreach(var q in p)v.Add(q);
                for(int i=0;i<p.Count;i++){int a=start+1+i,b=start+1+(i+1)%p.Count;Both(start,a,b);}
            }
            public void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,float z)
            {int s=v.Count;v.Add(new Vector3(a.x,a.y,z));v.Add(new Vector3(b.x,b.y,z));v.Add(new Vector3(c.x,c.y,z));v.Add(new Vector3(d.x,d.y,z));Both(s,s+1,s+2);Both(s,s+2,s+3);}
            public void Ring(Vector2 c,float outer,float inner,float from,float to,int n,float z)
            {
                int s=v.Count;
                for(int i=0;i<=n;i++){float a=Mathf.Lerp(from,to,i/(float)n);var d=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                    v.Add(new Vector3(c.x+d.x*outer,c.y+d.y*outer,-z));v.Add(new Vector3(c.x+d.x*inner,c.y+d.y*inner,-z));}
                for(int i=0;i<n;i++){int a=s+i*2;Both(a,a+2,a+1);Both(a+1,a+2,a+3);}
            }
            void Both(int a,int b,int c){t.Add(a);t.Add(b);t.Add(c);t.Add(a);t.Add(c);t.Add(b);}
            public Mesh Done(){var m=new Mesh();Into(m);return m;}
            public void Into(Mesh m){m.Clear();m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();}
        }
    }
}

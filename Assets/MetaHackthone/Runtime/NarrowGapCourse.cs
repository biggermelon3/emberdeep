using System.Collections.Generic;
using UnityEngine;

namespace PhysicalAssembly
{
    // The editor builder bakes this miniature into a reusable prefab. Its visual
    // meshes are independent of the deliberately simple gameplay colliders.
    public sealed partial class NarrowGapCourse : MonoBehaviour
    {
        public Transform PadVisual,Trajectory,SuccessStars;
        public Transform DockPoint;
        public BuildingArea GateOpening, TargetArea;
        public TextMesh ScoreLabel;
        public AudioClip LaunchSound,WinSound;
        AudioSource audioSource;
        float animationTime=-1;
        Vector3 padRest;
        void Awake() { if(PadVisual) padRest=PadVisual.localPosition; InitializeAimVisuals(); }
        public void Launch()
        {
            animationTime=-1; HideTrajectory();
            Play(LaunchSound);
        }
        public void Celebrate() { if(SuccessStars) SuccessStars.gameObject.SetActive(true); Play(WinSound); }
        void Play(AudioClip clip)
        {
            if(!clip) return;
            if(!audioSource) { audioSource=gameObject.AddComponent<AudioSource>(); audioSource.spatialBlend=.7f; audioSource.volume=.35f; }
            audioSource.PlayOneShot(clip);
        }
        public void ResetEffects()
        { animationTime=-1; RestorePad(); HideTrajectory(); if(SuccessStars) SuccessStars.gameObject.SetActive(false); }
        void Update()
        {
            if(animationTime<0 || !PadVisual) return;
            animationTime+=Time.deltaTime;
            PadVisual.localPosition=padRest+Vector3.down*(Mathf.Sin(animationTime*26)*.025f*Mathf.Exp(-animationTime*8));
            if(animationTime>.6f) { animationTime=-1; PadVisual.localPosition=padRest; }
        }
        public static NarrowGapCourse Build(NarrowGapSettings s)
        {
            var root=new GameObject("Narrow Gap - wooden miniature");
            root.AddComponent<WorkshopEnvironment>();
            var course=root.AddComponent<NarrowGapCourse>();
            var wood=Wood("Honey oak",new Color(.64f,.35f,.14f));
            var lightWood=Wood("Cut maple",new Color(.86f,.63f,.32f));
            var darkWood=Wood("Walnut",new Color(.24f,.12f,.065f));
            var red=Mat("Enamel vermilion",new Color(.75f,.065f,.045f),.45f);
            var brass=Mat("Warm brass",new Color(.69f,.43f,.16f),.55f,.65f);
            var steel=Mat("Spring steel",new Color(.22f,.25f,.26f),.7f,.8f);
            var cream=Mat("Cream lettering",new Color(1,.90f,.66f));
            var green=Mat("Pine green",new Color(.12f,.28f,.16f));
            var sage=Mat("Pine tips",new Color(.32f,.46f,.21f));
            var rock=Mat("Warm limestone",new Color(.39f,.40f,.37f));
            var felt=Mat("Target green felt",new Color(.22f,.40f,.28f));
            var rubber=new PhysicsMaterial("Narrow Gap grip") { staticFriction=.9f,dynamicFriction=.85f,bounciness=0,frictionCombine=PhysicsMaterialCombine.Maximum };
            Transform parent=root.transform;
            GameObject Box(string name,Vector3 pos,Vector3 size,Material mat,bool solid=true,float bevel=.015f)
            {
                var go=new GameObject(name); go.transform.SetParent(parent,false); go.transform.localPosition=pos;
                go.AddComponent<MeshFilter>().sharedMesh=RoundedBox(size,Mathf.Min(bevel,Mathf.Min(size.x,Mathf.Min(size.y,size.z))*.25f));
                go.AddComponent<MeshRenderer>().sharedMaterial=mat;
                if(solid) { var collider=go.AddComponent<BoxCollider>(); collider.size=size; collider.sharedMaterial=rubber; }
                return go;
            }
            GameObject Primitive(string name,PrimitiveType type,Vector3 pos,Vector3 scale,Material mat)
            {
                var go=GameObject.CreatePrimitive(type); go.name=name; go.transform.SetParent(parent,false); go.transform.localPosition=pos; go.transform.localScale=scale;
                DestroyImmediate(go.GetComponent<Collider>()); go.GetComponent<Renderer>().sharedMaterial=mat; return go;
            }
            TextMesh Text(string name,string text,Vector3 pos,float size,Color color,Quaternion rotation)
            {
                var go=new GameObject(name); go.transform.SetParent(parent,false); go.transform.localPosition=pos; go.transform.localRotation=rotation;
                var label=go.AddComponent<TextMesh>(); label.text=text; label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize=64; label.characterSize=size/6.4f; label.anchor=TextAnchor.MiddleCenter; label.color=color;
                go.GetComponent<MeshRenderer>().sharedMaterial=label.font.material; return label;
            }
            void Tree(Vector3 pos,float height)
            {
                Primitive("Pine trunk",PrimitiveType.Cylinder,pos+Vector3.up*height*.2f,new Vector3(.03f,height*.2f,.03f),darkWood);
                for(int i=0;i<3;i++)
                {
                    var tree=new GameObject("Carved pine canopy"); tree.transform.SetParent(parent,false); tree.transform.localPosition=pos+Vector3.up*(height*(.35f+i*.21f));
                    tree.AddComponent<MeshFilter>().sharedMesh=Cone(height*(.34f-i*.065f),height*.5f,9);
                    tree.AddComponent<MeshRenderer>().sharedMaterial=i==2?sage:green;
                }
            }
            Box("Tabletop walnut edge",new Vector3(.1f,.455f,.9f),new Vector3(2.9f,.08f,3.35f),darkWood);
            for(int i=0;i<10;i++) Box("Oak tabletop plank",new Vector3(-1.21f+i*.291f,.495f,.9f),new Vector3(.286f,.01f,3.30f),i%3==0?lightWood:wood,false,.002f);
            Box("Continuous tabletop collision",new Vector3(.1f,.48f,.9f),new Vector3(2.9f,.04f,3.3f),wood).GetComponent<Renderer>().enabled=false;
            Box("Pad wooden plinth",new Vector3(0,.535f,0),new Vector3(s.PadSize.x,.07f,s.PadSize.z),darkWood);
            Box("Pad brass trim",new Vector3(0,.574f,0),new Vector3(s.PadSize.x,.012f,s.PadSize.z),brass,false);
            foreach(float x in new[]{-s.PadSize.x*.25f,s.PadSize.x*.25f}) foreach(float z in new[]{-s.PadSize.z*.25f,s.PadSize.z*.25f})
            {
                var spring=new GameObject("Coiled steel spring"); spring.transform.SetParent(parent,false);
                var line=spring.AddComponent<LineRenderer>(); line.useWorldSpace=false; line.sharedMaterial=steel; line.widthMultiplier=.003f; line.numCornerVertices=2; line.positionCount=33;
                for(int i=0;i<33;i++) { float t=i/32f; line.SetPosition(i,new Vector3(x+Mathf.Cos(t*Mathf.PI*8)*s.PadSize.x*.12f,.58f+t*.075f,z+Mathf.Sin(t*Mathf.PI*8)*s.PadSize.z*.12f)); }
                Primitive("Brass fixing",PrimitiveType.Sphere,new Vector3(x,.578f,z),new Vector3(.009f,.006f,.009f),brass);
            }
            var pad=Box("Jump pad collider",new Vector3(0,s.PadHeight-s.PadSize.y*.5f,0),s.PadSize,red);
            pad.GetComponent<Renderer>().enabled=false;
            course.PadVisual=Box("Red spring pad",pad.transform.localPosition,s.PadSize,red,false,.03f).transform;
            Text("Pad label","CORE",new Vector3(0,.505f,-.10f),.025f,new Color(1,.87f,.62f),Quaternion.Euler(90,0,0));
            for(int side=-1;side<=1;side+=2)
            {
                float x=side*(s.GapWidth*.5f+.14f);
                Box("Solid wooden stake",new Vector3(x,.5f+s.GateHeight*.5f,s.GateDistance),new Vector3(.28f,s.GateHeight,s.GateThickness),wood,true,.025f);
                Box("Stake foot",new Vector3(x,.535f,s.GateDistance),new Vector3(.28f,.07f,.32f),darkWood);
                for(int i=0;i<3;i++) Primitive("Stake brass pin",PrimitiveType.Sphere,new Vector3(x,.58f+i*.47f,s.GateDistance-.083f),new Vector3(.023f,.023f,.009f),brass);
            }
            Text("Gap dimension","29 cm",new Vector3(0,.515f,s.GateDistance-.15f),.025f,new Color(1,.89f,.62f),Quaternion.Euler(90,0,0));
            var random=new System.Random(17);
            for(int i=0;i<14;i++)
            {
                float x=(i%2==0?-1:1)*(.43f+(float)random.NextDouble()*.24f), z=1.3f+(float)random.NextDouble()*1.1f;
                var stone=Primitive("Faceted cliff stone",PrimitiveType.Sphere,new Vector3(x,.56f,z),new Vector3(.23f,.22f+(float)random.NextDouble()*.14f,.28f),rock);
                stone.GetComponent<MeshFilter>().sharedMesh=RockMesh(i+5);
            }
            Box("Target deck",new Vector3(0,s.PadHeight-.06f,s.TargetDistance),new Vector3(s.TargetWidth,.12f,s.TargetLength),lightWood);
            Box("Target felt landing",new Vector3(0,s.PadHeight+.002f,s.TargetDistance),new Vector3(s.TargetWidth-.04f,.004f,s.TargetLength-.04f),felt,false,.001f);
            Box("Target backstop",new Vector3(0,s.PadHeight+.15f,s.TargetDistance+s.TargetLength*.5f),new Vector3(s.TargetWidth,.30f,.05f),wood);
            for(int side=-1;side<=1;side+=2) Box("Target side rail",new Vector3(side*(s.TargetWidth*.5f-.015f),s.PadHeight+.06f,s.TargetDistance),new Vector3(.03f,.12f,s.TargetLength),wood);
            for(int i=0;i<7;i++) Box("Target front joinery",new Vector3(-.38f+i*.127f,s.PadHeight-.04f,s.TargetDistance-s.TargetLength*.5f-.008f),new Vector3(.04f,.10f,.02f),darkWood,false,.003f);
            Primitive("Flagpole",PrimitiveType.Cylinder,new Vector3(.31f,s.PadHeight+.42f,2.38f),new Vector3(.018f,.42f,.018f),brass);
            Primitive("Flag finial",PrimitiveType.Sphere,new Vector3(.31f,s.PadHeight+.85f,2.38f),Vector3.one*.04f,cream);
            var flag=Box("Red finish pennant",new Vector3(.43f,s.PadHeight+.71f,2.38f),new Vector3(.24f,.14f,.012f),red,false,.003f);
            flag.transform.localRotation=Quaternion.Euler(0,-8,-4);
            for(int i=0;i<6;i++) Tree(new Vector3(i%2==0?-.7f:.72f,.50f,1.18f+(i/2)*.58f),.27f+(i%3)*.07f);
            Box("Ball tray",new Vector3(-.72f,.515f,.29f),new Vector3(.48f,.03f,.15f),lightWood);
            for(int side=-1;side<=1;side+=2) Box("Tray lip",new Vector3(-.72f,.54f,.29f+side*.075f),new Vector3(.48f,.04f,.018f),darkWood);
            Box("Tray end left",new Vector3(-.96f,.54f,.29f),new Vector3(.018f,.04f,.15f),darkWood);
            Box("Tray end right",new Vector3(-.48f,.54f,.29f),new Vector3(.018f,.04f,.15f),darkWood);
            Text("Supply tray label","FIVE TO SAVE",new Vector3(-.72f,.503f,.43f),.028f,new Color(1,.88f,.65f),Quaternion.Euler(90,0,0));
            Box("Title plaque",new Vector3(0,1.57f,2.6f),new Vector3(1.28f,.27f,.045f),darkWood,false);
            Text("Level title","NARROW GAP",new Vector3(0,1.61f,2.574f),.09f,new Color(1,.88f,.60f),Quaternion.identity);
            Text("Level subtitle","LEVEL 02  /  BUILD - LAUNCH - SAVE",new Vector3(0,1.51f,2.574f),.028f,new Color(.85f,.74f,.55f),Quaternion.identity);
            Box("Saved plaque",new Vector3(.93f,1.20f,2.5f),new Vector3(.49f,.27f,.04f),darkWood,false);
            course.ScoreLabel=Text("Saved counter","0 / 5\nSAVED",new Vector3(.93f,1.20f,2.475f),.055f,new Color(1,.88f,.60f),Quaternion.identity);
            course.Trajectory=new GameObject("Launch trajectory").transform; course.Trajectory.SetParent(parent,false);
            course.InitializeAimVisuals();
            course.SuccessStars=new GameObject("Five saved celebration").transform; course.SuccessStars.SetParent(parent,false);
            for(int i=0;i<5;i++)
            {
                var star=Box("Golden celebration",new Vector3(-.30f+i*.15f,1.30f+Mathf.Sin(i)*.08f,2.2f),new Vector3(.055f,.055f,.015f),brass,false,.006f);
                star.transform.localRotation=Quaternion.Euler(0,0,45); star.transform.SetParent(course.SuccessStars,true);
            }
            course.SuccessStars.gameObject.SetActive(false);
            course.LaunchSound=Tone("Spring launch",.24f,180,680); course.WinSound=Tone("Five saved chime",.65f,650,1050);
            course.padRest=course.PadVisual.localPosition;
            return course;
        }
        static Material Mat(string name,Color color,float smooth=.2f,float metal=0)
        { var m=PartFactory.Material(color); m.name=name; m.SetFloat("_Smoothness",smooth); m.SetFloat("_Metallic",metal); m.enableInstancing=true; return m; }
        static Material Wood(string name,Color color)
        {
            var tex=new Texture2D(128,256,TextureFormat.RGB24,true); tex.name=name+" grain"; tex.wrapMode=TextureWrapMode.Repeat;
            var pixels=new Color[128*256];
            for(int y=0;y<256;y++) for(int x=0;x<128;x++)
            {
                float wave=Mathf.Sin(x*.38f+Mathf.Sin(y*.022f)*1.7f+Mathf.PerlinNoise(x*.09f,y*.008f)*3);
                float fine=Mathf.PerlinNoise(x*.45f,y*.09f); pixels[y*128+x]=Color.white*(.86f+.035f*wave+.04f*fine);
            }
            tex.SetPixels(pixels); tex.Apply(); var m=Mat(name,color,.3f); m.mainTexture=tex; return m;
        }
        public static Mesh RoundedBox(Vector3 size,float radius)
        {
            var vertices=new List<Vector3>(); var normals=new List<Vector3>(); var uvs=new List<Vector2>(); var triangles=new List<int>();
            Vector3[] faces={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
            var half=size*.5f; var inner=half-Vector3.one*radius;
            foreach(var normal in faces)
            {
                var u=Vector3.Cross(Mathf.Abs(normal.y)>.5f?Vector3.forward:Vector3.up,normal); var v=Vector3.Cross(normal,u); int start=vertices.Count;
                // Edge-adjacent samples make a real bevel, not just rounded corners.
                float[] steps={-1,-.92f,-.80f,0,.80f,.92f,1}; int n=steps.Length;
                for(int y=0;y<n;y++) for(int x=0;x<n;x++)
                {
                    var p=Vector3.Scale(normal+u*steps[x]+v*steps[y],half);
                    var q=new Vector3(Mathf.Clamp(p.x,-inner.x,inner.x),Mathf.Clamp(p.y,-inner.y,inner.y),Mathf.Clamp(p.z,-inner.z,inner.z));
                    var direction=(p-q).normalized; vertices.Add(q+direction*radius); normals.Add(direction); uvs.Add(new Vector2((steps[x]+1)*.5f,(steps[y]+1)*.5f));
                }
                for(int y=0;y<n-1;y++) for(int x=0;x<n-1;x++) { int a=start+y*n+x; triangles.AddRange(new[]{a,a+1,a+n+1,a,a+n+1,a+n}); }
            }
            var mesh=new Mesh { name="Beveled wood mesh" }; mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0,uvs); mesh.SetTriangles(triangles,0); mesh.RecalculateBounds(); return mesh;
        }
        static Mesh Cone(float radius,float height,int sides)
        {
            var vertices=new List<Vector3>(); var indices=new List<int>();
            for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides; int k=vertices.Count;
                vertices.Add(new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius)); vertices.Add(Vector3.up*height); vertices.Add(new Vector3(Mathf.Cos(b)*radius,0,Mathf.Sin(b)*radius)); indices.AddRange(new[]{k,k+1,k+2});
            }
            var m=new Mesh { name="Carved pine cone" }; m.SetVertices(vertices); m.SetTriangles(indices,0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }
        static Mesh RockMesh(int seed)
        {
            var m=RoundedBox(Vector3.one,.22f); var vertices=m.vertices;
            for(int i=0;i<vertices.Length;i++) vertices[i]*=.9f+Mathf.PerlinNoise(vertices[i].x*4+seed,vertices[i].z*4+vertices[i].y)*.3f;
            m.vertices=vertices; m.RecalculateNormals(); m.RecalculateBounds(); m.name="Carved cliff"; return m;
        }
        static AudioClip Tone(string name,float duration,float from,float to)
        {
            const int rate=22050; var samples=new float[(int)(duration*rate)]; float phase=0;
            for(int i=0;i<samples.Length;i++) { float t=i/(float)samples.Length; phase+=Mathf.Lerp(from,to,t)*Mathf.PI*2/rate; samples[i]=Mathf.Sin(phase)*Mathf.Sin(Mathf.PI*t)*Mathf.Exp(-t*3)*.4f; }
            var clip=AudioClip.Create(name,samples.Length,1,rate,false); clip.SetData(samples,0); return clip;
        }
    }
}

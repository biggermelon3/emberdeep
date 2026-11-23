using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FireRescue
{
    // Shared vertex-colour meshes: no physics bodies, per-animal materials, timers,
    // navigation agents or health bars. The domain clock owns all animation time.
    // A pet (Affection jumping up, from hands or Pet()) plays EmberdeepSheepHop and the pet sound.
    // Rescued sheep stay on the safe bank, grazing, so they can be petted.
    public sealed class EmberdeepSheepView : MonoBehaviour
    {
        const float GrazeCycle=5.5f,GrazeDip=24f;
        sealed class Sheep { public Transform Root,Body,Head,LeftEar,RightEar,Coin,Heart;public Transform[] Legs=new Transform[4]; }
        readonly List<Sheep> flock=new();
        readonly List<Mesh> meshes=new();
        Material material;
        FireRun run;
        Mesh body,head,ear,leg,coin,heart;
        readonly Dictionary<Mesh,Material[]> kitMaterials=new();
        public bool Authored {get;private set;}
        readonly List<Vector3> verts=new(),normals=new();
        readonly List<Color> colors=new();readonly List<int> triangles=new();
        static readonly Color Wool=new(.97f,.89f,.69f),Face=new(.39f,.26f,.20f),Pink=new(.93f,.49f,.44f),Dark=new(.08f,.075f,.065f);
        public int Count=>flock.Count;
        public EmberdeepSheepAudio Audio {get;private set;}
        float[] hopStart,lastAffection;
        public Transform SheepRoot(int i)=>flock[i].Root;
        public bool Hopping(int i)=>run!=null && i>=0 && i<flock.Count && EmberdeepSheepHop.Evaluate(run.Animals[i].AnimationTime-hopStart[i],out _,out _);
        // Scripted pet (camp tutorial, tests): same cooldown and effect as a hand pet.
        public bool Pet(int i)=>run!=null && i>=0 && i<run.Animals.Length && EmberdeepSheepInteraction.TryPet(run.Animals[i]);
        public void Initialize(FireRun source)
        {
            run=source;
            hopStart=new float[run.Animals.Length];lastAffection=new float[run.Animals.Length];
            for(int i=0;i<hopStart.Length;i++){hopStart[i]=float.NegativeInfinity;lastAffection[i]=run.Animals[i].Affection;}
            material=new Material(Resources.Load<Shader>("EmberdeepSheep")){name="Warm wool and cocoa faces",enableInstancing=true};
            Authored=LoadKit();
            if(!Authored)BuildMeshes();
            for(int i=0;i<run.Animals.Length;i++)
            {
                var s=new Sheep{Root=new GameObject("Rescue sheep "+(i+1)).transform};s.Root.SetParent(transform,false);
                s.Body=Part(s.Root,body,"Cloud wool body",Vector3.zero);
                s.Head=Part(s.Root,head,"Expressive face",new Vector3(0,.69f,-.38f));
                s.LeftEar=Part(s.Head,ear,"Left soft ear",new Vector3(-.22f,.08f,0));
                s.RightEar=Part(s.Head,ear,"Right soft ear",new Vector3(.22f,.08f,0));s.RightEar.localScale=new Vector3(-1,1,1);
                for(int j=0;j<4;j++)s.Legs[j]=Part(s.Root,leg,"Little hoof "+j,new Vector3((j%2==0?-1:1)*.22f,.28f,(j<2?-.24f:.25f)));
                s.Coin=Part(s.Root,coin,"Rescue coin",Vector3.up*.8f);s.Coin.gameObject.SetActive(false);
                s.Heart=Part(s.Root,heart,"Affection heart",Vector3.up*1.1f);s.Heart.gameObject.SetActive(false);
                flock.Add(s);
            }
            Audio=gameObject.AddComponent<EmberdeepSheepAudio>();Audio.Initialize(run);
            Render();
        }
        Transform Part(Transform parent,Mesh mesh,string label,Vector3 at)
        {
            var t=new GameObject(label).transform;t.SetParent(parent,false);t.localPosition=at;
            t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=t.gameObject.AddComponent<MeshRenderer>();r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            if(mesh && kitMaterials.TryGetValue(mesh,out var authored))r.sharedMaterials=authored;else r.sharedMaterial=material;
            return t;
        }
        // Sculpted sheep from the Blender kit: each animated part is a mesh around its own pivot.
        bool LoadKit()
        {
            var art=EmberdeepArtLibrary.Load();var kit=art?art.Find("Sheep"):null;if(!kit)return false;
            foreach(var filter in kit.GetComponentsInChildren<MeshFilter>(true))
            {
                var shared=filter.sharedMesh;var renderer=filter.GetComponent<Renderer>();if(!shared || !renderer)continue;
                string part=filter.name;
                if(part.StartsWith("Body"))body=shared;else if(part.StartsWith("Head"))head=shared;else if(part.StartsWith("Ear"))ear=shared;
                else if(part.StartsWith("Leg"))leg=shared;else if(part.StartsWith("Coin"))coin=shared;else if(part.StartsWith("Heart"))heart=shared;else continue;
                kitMaterials[shared]=renderer.sharedMaterials;
            }
            if(body && head && ear && leg && coin && heart)return true;
            kitMaterials.Clear();body=head=ear=leg=coin=heart=null;return false;
        }
        public void Render()
        {
            if(run==null)return;
            for(int i=0;i<flock.Count;i++)
            {
                var s=flock[i];var a=run.Animals[i];float t=a.AnimationTime;
                bool moving=a.Velocity.sqrMagnitude>.001f,happy=a.State==AnimalState.Celebrating || (a.Affection>0 && !a.OnFire),done=a.State==AnimalState.Rescued,lost=a.State==AnimalState.Lost;
                float fear=a.OnFire?1:a.Threat>0 && a.Wetness<=0?.4f:0;
                if(a.Affection>lastAffection[i]+.05f && !lost)
                {
                    hopStart[i]=t;
                    if(Audio)Audio.Pet(a.Position+Vector3.up*FireRun.AnimalHeight,EmberdeepSheepHop.TakeoffTime);
                }
                lastAffection[i]=a.Affection;
                bool hopping=EmberdeepSheepHop.Evaluate(t-hopStart[i],out float lift,out float stretch);
                s.Root.localScale=EmberdeepSheepHop.Scale(stretch)*.65f;
                float bounce=a.HeldBy>=0?0:hopping?lift*.65f:moving?Mathf.Abs(Mathf.Sin(t*10))*.09f:happy?Mathf.Abs(Mathf.Sin(t*5))*.08f:Mathf.Sin(t*2)*.012f;
                s.Root.localPosition=a.Position+Vector3.up*bounce;
                float graze=done && !moving && !hopping?GrazeDip*Mathf.Clamp01(Mathf.Sin((t+i*1.3f)*Mathf.PI*2/GrazeCycle)*2-1):0;
                s.Root.localRotation=Quaternion.Slerp(s.Root.localRotation,Quaternion.Euler(0,moving?Mathf.Atan2(-a.Velocity.x,-a.Velocity.z)*Mathf.Rad2Deg:Mathf.Sin(t*.7f+i)*9,0),.22f);
                float size=.9f+i%3*.06f;
                if(lost)size*=1-Mathf.SmoothStep(0,1,a.StateTime/.8f);
                s.Body.localScale=Vector3.one*size;
                s.Head.localScale=Vector3.one*size;
                s.Head.localRotation=Quaternion.Euler(fear*13+graze+Mathf.Sin(t*2)*4,Mathf.Sin(t*15)*a.WaterReaction*18,happy?Mathf.Sin(t*10)*10:fear*Mathf.Sin(t*22)*3);
                s.Body.localRotation=Quaternion.Euler(0,0,lost?Mathf.Min(85,a.StateTime*120):Mathf.Sin(t*20)*a.WaterReaction*6);
                s.LeftEar.localRotation=Quaternion.Euler(0,0,-18-fear*22+Mathf.Sin(t*4)*9);
                s.RightEar.localRotation=Quaternion.Euler(0,0,18+fear*22-Mathf.Sin(t*4+.5f)*9);
                for(int j=0;j<4;j++){s.Legs[j].localScale=Vector3.one*size;s.Legs[j].localRotation=Quaternion.Euler(moving?Mathf.Sin(t*10+(j==0 || j==3?0:Mathf.PI))*32:happy?-18:0,0,0);}
                s.Coin.gameObject.SetActive(done && a.StateTime<1);
                s.Coin.localPosition=new Vector3(0,.8f+a.StateTime*1.3f,0);
                s.Coin.localRotation=Quaternion.Euler(0,t*220,0);
                s.Coin.localScale=Vector3.one*(1-Mathf.Clamp01((a.StateTime-.5f)*2));
                s.Heart.gameObject.SetActive(a.Affection>0 && !lost && !a.OnFire);
                s.Heart.localPosition=Vector3.up*(1.15f+(1.5f-a.Affection)*.22f);
                s.Heart.localScale=Vector3.one*Mathf.Min(1,a.Affection*3);
                if(Camera.main)s.Heart.rotation=Camera.main.transform.rotation;
            }
        }
        void BuildMeshes()
        {
            Begin();Ball(new Vector3(0,.48f,.04f),new Vector3(.36f,.30f,.46f),Wool);
            for(int i=0;i<9;i++){float angle=i*Mathf.PI*2/9;Ball(new Vector3(Mathf.Cos(angle)*.25f,.54f+Mathf.Sin(angle)*.17f,.05f+(i%3-1)*.23f),Vector3.one*.21f,Color.Lerp(Wool,Color.white,.12f*(i%3)));}
            // Larger tufts first, then sparse small curls: readable wool without alpha fur.
            for(int i=0;i<7;i++)
            {
                float a=i*2.399f;
                Ball(new Vector3(Mathf.Cos(a)*.27f,.66f+(i%2)*.075f,Mathf.Sin(a)*.29f),new Vector3(.095f,.09f,.11f),Color.Lerp(Wool,Color.white,.22f));
            }
            Ball(new Vector3(0,.5f,.48f),Vector3.one*.13f,Wool);body=Finish("Soft scalloped wool");
            Begin();Ball(Vector3.zero,new Vector3(.245f,.24f,.24f),Face);
            Ball(new Vector3(0,-.1f,-.15f),new Vector3(.17f,.12f,.13f),new Color(.56f,.39f,.29f));
            for(int side=-1;side<=1;side+=2)
            {
                Ball(new Vector3(side*.105f,.03f,-.21f),new Vector3(.076f,.09f,.033f),Color.white);
                Ball(new Vector3(side*.105f,.024f,-.239f),new Vector3(.038f,.054f,.018f),Dark);
                Ball(new Vector3(side*.117f,.047f,-.252f),Vector3.one*.013f,Color.white);
                Ball(new Vector3(side*.16f,-.075f,-.192f),new Vector3(.047f,.025f,.014f),Pink);
                Ball(new Vector3(side*.07f,-.125f,-.263f),new Vector3(.035f,.012f,.012f),Dark);
            }
            Ball(new Vector3(0,-.075f,-.272f),new Vector3(.035f,.025f,.014f),Dark);
            for(int i=0;i<3;i++)Ball(new Vector3((i-1)*.12f,.20f,-.04f),Vector3.one*.11f,Wool);
            head=Finish("Bright eyes rosy cheeks and wool fringe");
            Begin();Ball(new Vector3(-.1f,0,0),new Vector3(.17f,.07f,.095f),Face);Ball(new Vector3(-.115f,.035f,-.025f),new Vector3(.105f,.025f,.055f),Pink);ear=Finish("Floppy ear");
            Begin();Ball(new Vector3(0,-.08f,0),new Vector3(.065f,.16f,.067f),Face);Ball(new Vector3(0,-.21f,-.013f),new Vector3(.08f,.065f,.10f),Dark);leg=Finish("Rounded hoof");
            Begin();Ball(Vector3.zero,new Vector3(.15f,.15f,.035f),new Color(1,.63f,.10f));Ball(new Vector3(0,0,-.035f),new Vector3(.10f,.10f,.012f),new Color(1,.9f,.34f));coin=Finish("Rescue gold");
            Begin();Ball(new Vector3(-.065f,.035f,0),new Vector3(.09f,.09f,.035f),Pink);Ball(new Vector3(.065f,.035f,0),new Vector3(.09f,.09f,.035f),Pink);
            Ball(new Vector3(0,-.035f,0),new Vector3(.11f,.08f,.035f),Pink);Ball(new Vector3(0,-.09f,0),new Vector3(.055f,.06f,.03f),Pink);heart=Finish("Soft pink heart");
        }
        void Begin(){verts.Clear();normals.Clear();colors.Clear();triangles.Clear();}
        void Ball(Vector3 center,Vector3 radius,Color color)
        {
            const int sides=10,rings=6;int start=verts.Count;
            for(int y=0;y<=rings;y++)for(int x=0;x<=sides;x++)
            {
                float p=y*Mathf.PI/rings,q=x*Mathf.PI*2/sides;var n=new Vector3(Mathf.Sin(p)*Mathf.Cos(q),Mathf.Cos(p),Mathf.Sin(p)*Mathf.Sin(q));
                verts.Add(center+Vector3.Scale(n,radius));normals.Add(new Vector3(n.x/radius.x,n.y/radius.y,n.z/radius.z).normalized);colors.Add(color);
            }
            for(int y=0;y<rings;y++)for(int x=0;x<sides;x++)
            {int a=start+y*(sides+1)+x,b=a+sides+1;triangles.Add(a);triangles.Add(a+1);triangles.Add(b);triangles.Add(a+1);triangles.Add(b+1);triangles.Add(b);}
        }
        Mesh Finish(string label){var m=new Mesh{name=label};m.SetVertices(verts);m.SetNormals(normals);m.SetColors(colors);m.SetTriangles(triangles,0);m.RecalculateBounds();meshes.Add(m);return m;}
        void OnDestroy(){foreach(var m in meshes)if(m)Destroy(m);if(material)Destroy(material);}
    }
}

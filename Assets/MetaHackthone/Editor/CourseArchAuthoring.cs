using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PhysicalAssembly.Editor
{
    // Low-poly timber-and-cloth arches based on E:/Unity/Image/Start.png and End.png.
    public static class CourseArchAuthoring
    {
        const string Folder="Assets/MetaHackthone/Resources/CourseArches";
        [MenuItem("MetaHackthone/Scene editing/Bake start and goal arches")]
        public static void BakeAssets()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            System.IO.Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            font.RequestCharactersInTexture("STARTGOAL",96,FontStyle.Bold);
            // Resolve both labels before asset imports can invalidate the dynamic font cache.
            var meshes=new List<Mesh>();
            foreach(bool start in new[]{true,false})
            {
                string name=start?"StartArch":"GoalArch";
                var builder=new Geometry();
                foreach(float x in new[]{-.46f,.46f})
                {
                    builder.Box(new Vector3(x,.025f,0),new Vector3(.13f,.05f,.14f),0);
                    builder.Pole(new Vector3(x,.45f,0),.028f,.84f,0);
                    builder.Pole(new Vector3(x,.09f,0),.034f,.045f,1);
                    builder.Pole(new Vector3(x,.85f,0),.039f,.025f,1);
                    builder.Diamond(new Vector3(x,.90f,0),new Vector3(.041f,.055f,.041f),1);
                    foreach(float y in new[]{.55f,.80f}) {
                        builder.Pole(new Vector3(x,y,0),.032f,.028f,2);
                        builder.Box(new Vector3(x*.96f,y,-.018f),new Vector3(.07f,.025f,.022f),2);
                    }
                }
                builder.Banner(); builder.Letters(start?"START":"GOAL",font);
                meshes.Add(builder.Mesh(name));
            }
            var atlas=CopyTexture(font.material.mainTexture);Save(atlas,"LetterAtlas.asset");
            var lettering=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Arch lettering"};
            lettering.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/LetterAtlas.asset"));
            lettering.SetColor("_BaseColor",Color.white);lettering.SetFloat("_AlphaClip",1);lettering.SetFloat("_Cutoff",.35f);
            lettering.SetFloat("_Cull",2);lettering.EnableKeyword("_ALPHATEST_ON");lettering.renderQueue=2450;Save(lettering,"Lettering.mat");
            Save(PartFactory.Material(new Color(.57f,.29f,.09f)),"Timber.mat");
            Save(PartFactory.Material(new Color(1f,.64f,.16f)),"Gold.mat");
            Save(PartFactory.Material(new Color(.035f,.25f,.94f)),"StartBlue.mat");
            Save(PartFactory.Material(new Color(.93f,.035f,.035f)),"GoalRed.mat");
            foreach(var mesh in meshes)
            {
                string name=mesh.name;bool start=name=="StartArch";Save(mesh,name+".asset");
                var go=new GameObject(name);
                try {
                    go.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/"+name+".asset");
                    var renderer=go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials=new[]{"Timber.mat","Gold.mat",start?"StartBlue.mat":"GoalRed.mat","Lettering.mat"}.Select(file=>AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+file)).ToArray();
                    renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                    PrefabUtility.SaveAsPrefabAsset(go,Folder+"/"+name+".prefab");
                } finally {UnityEngine.Object.DestroyImmediate(go);}
            }
            AssetDatabase.SaveAssets();
        }
        static void Save(UnityEngine.Object value,string file)
        {
            var path=Folder+"/"+file;var existing=AssetDatabase.LoadMainAssetAtPath(path);
            if(value is Mesh source) {
                // Preserve the material submeshes in one persistent mesh.
                var target=new Mesh{name=source.name};var batches=new CombineInstance[source.subMeshCount];
                for(int i=0;i<batches.Length;i++)batches[i]=new CombineInstance{mesh=source,subMeshIndex=i,transform=Matrix4x4.identity};
                target.CombineMeshes(batches,false,false);target.RecalculateBounds();
                AssetDatabase.CreateAsset(target,path);UnityEngine.Object.DestroyImmediate(source);
                if(target.vertexCount==0)throw new InvalidOperationException("Arch mesh serialization failed: "+path);
            }
            else if(existing){EditorUtility.CopySerialized(value,existing);EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(value);}
            else {AssetDatabase.CreateAsset(value,path);EditorUtility.SetDirty(value);}
            AssetDatabase.SaveAssets();
        }
        static Texture2D CopyTexture(Texture source)
        {
            var previous=RenderTexture.active;var target=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32);
            try {
                Graphics.Blit(source,target);RenderTexture.active=target;
                var texture=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false){name="Arch letter atlas",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                texture.ReadPixels(new Rect(0,0,source.width,source.height),0,0);
                var pixels=texture.GetPixels32();for(int i=0;i<pixels.Length;i++)pixels[i]=new Color32(255,255,255,pixels[i].a);
                texture.SetPixels32(pixels);texture.Apply();return texture;
            } finally {RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);}
        }
        sealed class Geometry
        {
            readonly List<Vector3> vertices=new();readonly List<Vector2> uv=new();
            readonly List<int>[] triangles={new(),new(),new(),new()};
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int material,Vector2? ua=null,Vector2? ub=null,Vector2? uc=null,Vector2? ud=null)
            {
                int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});uv.AddRange(new[]{ua??Vector2.zero,ub??Vector2.up,uc??Vector2.one,ud??Vector2.right});
                triangles[material].AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            public void Box(Vector3 center,Vector3 size,int material)
            {
                Vector3 P(float x,float y,float z)=>center+Vector3.Scale(size,new Vector3(x,y,z))*.5f;
                Quad(P(-1,-1,-1),P(-1,1,-1),P(1,1,-1),P(1,-1,-1),material);
                Quad(P(1,-1,1),P(1,1,1),P(-1,1,1),P(-1,-1,1),material);
                Quad(P(-1,-1,1),P(-1,1,1),P(-1,1,-1),P(-1,-1,-1),material);
                Quad(P(1,-1,-1),P(1,1,-1),P(1,1,1),P(1,-1,1),material);
                Quad(P(-1,1,-1),P(-1,1,1),P(1,1,1),P(1,1,-1),material);
                Quad(P(-1,-1,1),P(-1,-1,-1),P(1,-1,-1),P(1,-1,1),material);
            }
            public void Pole(Vector3 center,float radius,float height,int material)
            {
                Vector3 P(int i,float y)=>center+new Vector3(Mathf.Cos(i*Mathf.PI/5)*radius,y,Mathf.Sin(i*Mathf.PI/5)*radius);
                for(int i=0;i<10;i++) {
                    Quad(P(i,-height*.5f),P(i,height*.5f),P(i+1,height*.5f),P(i+1,-height*.5f),material);
                    Quad(center+Vector3.up*height*.5f,P(i+1,height*.5f),P(i,height*.5f),center+Vector3.up*height*.5f,material);
                }
            }
            public void Diamond(Vector3 center,Vector3 size,int material)
            {
                for(int i=0;i<8;i++) {
                    var a=center+Vector3.Scale(size,new Vector3(Mathf.Cos(i*Mathf.PI/4),0,Mathf.Sin(i*Mathf.PI/4)));
                    var b=center+Vector3.Scale(size,new Vector3(Mathf.Cos((i+1)*Mathf.PI/4),0,Mathf.Sin((i+1)*Mathf.PI/4)));
                    Quad(center+Vector3.up*size.y,b,a,center+Vector3.up*size.y,material);
                    Quad(center-Vector3.up*size.y,a,b,center-Vector3.up*size.y,material);
                }
            }
            public void Banner()
            {
                Vector3 P(float x,float y,float z)=>new Vector3(x,y-.025f*(1-x*x/.1764f),z+.014f*Mathf.Sin(x*12));
                for(int i=0;i<20;i++) {
                    float a=Mathf.Lerp(-.42f,.42f,i/20f),b=Mathf.Lerp(-.42f,.42f,(i+1)/20f);
                    Quad(P(a,.54f,-.006f),P(a,.81f,-.006f),P(b,.81f,-.006f),P(b,.54f,-.006f),2);
                    Quad(P(b,.54f,.006f),P(b,.81f,.006f),P(a,.81f,.006f),P(a,.54f,.006f),2);
                    Quad(P(a,.81f,-.006f),P(a,.81f,.006f),P(b,.81f,.006f),P(b,.81f,-.006f),2);
                    Quad(P(a,.54f,.006f),P(a,.54f,-.006f),P(b,.54f,-.006f),P(b,.54f,.006f),2);
                }
            }
            public void Letters(string text,Font font)
            {
                var glyphs=text.Select(ch=>{if(!font.GetCharacterInfo(ch,out var glyph,96,FontStyle.Bold))throw new InvalidOperationException("Missing arch glyph "+ch);return glyph;}).ToArray();
                float total=glyphs.Sum(g=>g.advance),scale=.62f/total;
                float min=glyphs.Min(g=>g.minY),max=glyphs.Max(g=>g.maxY);
                foreach(int side in new[]{-1,1}) {
                    float cursor=-total*.5f;
                    foreach(var g in glyphs) {
                        Vector3 P(float x,float y)=>new Vector3((cursor+x)*scale*-side,.66f+(y-(min+max)*.5f)*scale,side*.033f);
                        Quad(P(g.minX,g.minY),P(g.minX,g.maxY),P(g.maxX,g.maxY),P(g.maxX,g.minY),3,g.uvBottomLeft,g.uvTopLeft,g.uvTopRight,g.uvBottomRight);cursor+=g.advance;
                    }
                }
            }
            public Mesh Mesh(string name)
            {
                if(vertices.Any(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z)))throw new InvalidOperationException("Invalid arch vertices");
                var mesh=new Mesh{name=name,subMeshCount=4};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);
                mesh.colors=Enumerable.Repeat(Color.white,vertices.Count).ToArray();
                for(int i=0;i<4;i++)mesh.SetTriangles(triangles[i],i);
                mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.UploadMeshData(false);return mesh;
            }
        }
    }
}

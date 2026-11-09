using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace PhysicalAssembly.Editor
{
    public static class CombinedGridPadEditor
    {
        const string Folder="Assets/MetaHackthone/Generated/CombinedGridPad";
        const string MeshName="CenteredGridPad";
        [MenuItem("MetaHackthone/Scene editing/Combine centered grid pad")]
        public static void Bake()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var owner=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DownhillPresentation>(true)).Single();
            var pad=owner.FindGridElement("Centered grid pad");
            if(pad.GetComponent<MeshFilter>().sharedMesh.name==MeshName) return;
            var group=owner.EnsureGridPadGroup();
            var sources=group.Cast<Transform>().Where(t=>t==pad || t.name=="Grid X" || t.name=="Grid Z" || t.name=="Circular grid border" || t.name=="FRONT direction marking").ToArray();
            if(!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/MetaHackthone/Generated","CombinedGridPad");
            var materials=new List<Material>();
            var batches=new List<List<CombineInstance>>();
            var temporary=new List<Mesh>();
            void Add(Mesh mesh,Material material,Transform source,int submesh=0)
            {
                int index=materials.FindIndex(candidate=>candidate==material || EditorJsonUtility.ToJson(candidate)==EditorJsonUtility.ToJson(material));
                if(index<0) { index=materials.Count; materials.Add(material); batches.Add(new List<CombineInstance>()); }
                batches[index].Add(new CombineInstance {mesh=mesh,subMeshIndex=submesh,transform=pad.worldToLocalMatrix*source.localToWorldMatrix});
            }
            foreach(var source in sources) foreach(var renderer in source.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                var text=renderer.GetComponent<TextMesh>();
                if(text)
                {
                    var mesh=TextGeometry(text); temporary.Add(mesh);
                    var texture=CopyTexture(text.font.material.mainTexture);
                    AssetDatabase.CreateAsset(texture,Folder+"/FrontFontAtlas.asset");
                    var material=new Material(renderer.sharedMaterial) {name="Baked FRONT lettering"};
                    material.mainTexture=texture; AssetDatabase.CreateAsset(material,Folder+"/FrontLettering.mat");
                    Add(mesh,material,text.transform);
                }
                else
                {
                    var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
                    for(int i=0;i<mesh.subMeshCount;i++) Add(mesh,renderer.sharedMaterials[i],renderer.transform,i);
                }
            }
            var combined=new Mesh {name=MeshName};
            var submeshes=new List<CombineInstance>();
            foreach(var batch in batches)
            {
                var mesh=new Mesh(); mesh.CombineMeshes(batch.ToArray(),true,true); temporary.Add(mesh);
                submeshes.Add(new CombineInstance {mesh=mesh,transform=Matrix4x4.identity});
            }
            combined.CombineMeshes(submeshes.ToArray(),false,false); combined.RecalculateBounds();
            AssetDatabase.CreateAsset(combined,Folder+"/CenteredGridPad.asset");
            Undo.RegisterFullObjectHierarchyUndo(owner.gameObject,"Combine centered grid pad");
            pad.GetComponent<MeshFilter>().sharedMesh=combined;
            pad.GetComponent<MeshRenderer>().sharedMaterials=materials.ToArray();
            foreach(var source in sources) if(source!=pad) Undo.DestroyObjectImmediate(source.gameObject);
            foreach(var mesh in temporary) UnityEngine.Object.DestroyImmediate(mesh);
            EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("Combined grid pad saved: "+combined.vertexCount+" vertices, "+combined.subMeshCount+" materials.");
        }
        static Texture2D CopyTexture(Texture source)
        {
            var previous=RenderTexture.active;
            var target=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32);
            try
            {
                Graphics.Blit(source,target); RenderTexture.active=target;
                var texture=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false) {name="Static FRONT font atlas",filterMode=source.filterMode,wrapMode=TextureWrapMode.Clamp};
                texture.ReadPixels(new Rect(0,0,source.width,source.height),0,0); texture.Apply(); return texture;
            }
            finally { RenderTexture.active=previous; RenderTexture.ReleaseTemporary(target); }
        }
        static Mesh TextGeometry(TextMesh text)
        {
            text.font.RequestCharactersInTexture(text.text,text.fontSize,text.fontStyle);
            var vertices=new List<Vector3>(); var uv=new List<Vector2>(); var triangles=new List<int>();
            float cursor=0;
            foreach(char ch in text.text)
            {
                if(!text.font.GetCharacterInfo(ch,out var glyph,text.fontSize,text.fontStyle)) throw new InvalidOperationException("Missing glyph "+ch);
                int n=vertices.Count;
                vertices.Add(new Vector3(cursor+glyph.minX,glyph.minY,0)); vertices.Add(new Vector3(cursor+glyph.minX,glyph.maxY,0));
                vertices.Add(new Vector3(cursor+glyph.maxX,glyph.maxY,0)); vertices.Add(new Vector3(cursor+glyph.maxX,glyph.minY,0));
                uv.Add(glyph.uvBottomLeft); uv.Add(glyph.uvTopLeft); uv.Add(glyph.uvTopRight); uv.Add(glyph.uvBottomRight);
                triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3}); cursor+=glyph.advance;
            }
            var mesh=new Mesh {name="Baked FRONT"}; mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetTriangles(triangles,0); mesh.RecalculateBounds();
            // Match the live text renderer's glyph bounds, including its anchor and alignment.
            var target=text.GetComponent<MeshRenderer>().localBounds; var raw=mesh.bounds;
            for(int i=0;i<vertices.Count;i++) {
                var point=vertices[i]-raw.center;
                vertices[i]=target.center+new Vector3(point.x*target.size.x/raw.size.x,point.y*target.size.y/raw.size.y,0);
            }
            mesh.SetVertices(vertices); mesh.colors=Enumerable.Repeat(text.color,vertices.Count).ToArray(); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}

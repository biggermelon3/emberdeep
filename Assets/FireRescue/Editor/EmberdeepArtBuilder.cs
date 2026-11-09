using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FireRescue.Editor
{
    // Builds only the new Emberdeep ArtV2 assets; no shared scene or PlayerSettings mutation.
    public static class EmberdeepArtBuilder
    {
        const string Source="Assets/FireRescue/ArtV2/Models";
        const string Output="Assets/FireRescue/Resources/EmberdeepArtV2";
        static readonly Dictionary<string,Material> Materials=new();
        [MenuItem("Emberdeep/Art V2/Import Blender kit")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode before importing Art V2.");
            Directory.CreateDirectory(Output+"/Materials");Directory.CreateDirectory(Output+"/Prefabs");AssetDatabase.Refresh();
            var shader=Shader.Find("Emberdeep/V2 Sculpted Surface");var creekShader=Shader.Find("Emberdeep/V2 Shallow Creek");
            if(!shader || !creekShader)throw new InvalidOperationException("Art V2 shader import is incomplete.");
            Materials.Clear();var entries=new List<EmberdeepArtLibrary.Entry>();
            var paths=Directory.GetFiles(Source,"*.fbx").OrderBy(x=>x).ToArray();
            if(paths.Length<7)throw new InvalidOperationException("Expected the complete Blender environment kit before integration.");
            foreach(var file in paths)
            {
                string path=file.Replace('\\','/');AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                importer.importCameras=false;importer.importLights=false;importer.importAnimation=false;
                importer.globalScale=1;importer.isReadable=false;importer.meshCompression=ModelImporterMeshCompression.Low;
                importer.importNormals=ModelImporterNormals.Import;importer.SaveAndReimport();
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);var go=(GameObject)PrefabUtility.InstantiatePrefab(model);
                go.name=Path.GetFileNameWithoutExtension(path);
                try
                {
                    foreach(var r in go.GetComponentsInChildren<Renderer>(true))
                    {
                        var mats=r.sharedMaterials;
                        for(int i=0;i<mats.Length;i++)
                        {
                            string semantic=mats[i]?mats[i].name:"Moss";
                            bool water=r.name.Contains("CreekSurface") || semantic.ToLowerInvariant().Contains("water");
                            Color color=mats[i] && mats[i].HasProperty("_BaseColor")?mats[i].GetColor("_BaseColor"):
                                mats[i] && mats[i].HasProperty("_Color")?mats[i].color:Color.white;
                            mats[i]=Material(semantic,color,water?creekShader:shader);
                        }
                        r.sharedMaterials=mats;r.shadowCastingMode=go.name.Contains("Hydrant") || go.name.Contains("CreekPump")?
                            UnityEngine.Rendering.ShadowCastingMode.Off:UnityEngine.Rendering.ShadowCastingMode.On;r.receiveShadows=true;
                    }
                    var prefab=PrefabUtility.SaveAsPrefabAsset(go,Output+"/Prefabs/"+go.name+".prefab");
                    entries.Add(new EmberdeepArtLibrary.Entry{Id=go.name.Replace("EDV2_",""),Prefab=prefab});
                }
                finally{UnityEngine.Object.DestroyImmediate(go);}
            }
            var library=AssetDatabase.LoadAssetAtPath<EmberdeepArtLibrary>(Output+"/Library.asset");
            if(!library){library=ScriptableObject.CreateInstance<EmberdeepArtLibrary>();AssetDatabase.CreateAsset(library,Output+"/Library.asset");}
            library.Models=entries.ToArray();
            library.Surface=Material("Runtime pigment",Color.white,shader);
            library.Creek=Material("Runtime creek",new Color(.035f,.34f,.39f),creekShader);
            library.Panel=Material("Runtime slate panel",new Color(.055f,.12f,.14f),shader);
            library.Accent=Material("Runtime cyan handle",new Color(.27f,.83f,.9f),shader);
            library.Wood=Material("Runtime warm wood",new Color(.42f,.25f,.13f),shader);
            library.Foliage=Material("Runtime sage",new Color(.3f,.55f,.36f),shader);
            library.Ground=Material("Runtime moss",new Color(.26f,.39f,.23f),shader);
            library.Pipeline=CreateLighting();
            EditorUtility.SetDirty(library);AssetDatabase.SaveAssets();
            Debug.Log("EMBERDEEP_ART_V2_IMPORTED "+entries.Count+" Blender models; no scenes modified.");
        }
        static UnityEngine.Rendering.RenderPipelineAsset CreateLighting()
        {
            const string pipelinePath=Output+"/EmberdeepMobileLighting.asset";
            const string rendererPath=Output+"/EmberdeepMobileRenderer.asset";
            var renderer=AssetDatabase.LoadAssetAtPath<ScriptableObject>(rendererPath);
            if(!renderer)
            {
                renderer=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Settings/Mobile_Renderer.asset"));
                renderer.name="EmberdeepMobileRenderer";AssetDatabase.CreateAsset(renderer,rendererPath);
            }
            var rs=new SerializedObject(renderer);rs.FindProperty("m_RendererFeatures").ClearArray();rs.ApplyModifiedPropertiesWithoutUndo();
            var pipeline=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>(pipelinePath);
            if(!pipeline)
            {
                pipeline=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset"));
                pipeline.name="EmberdeepMobileLighting";AssetDatabase.CreateAsset(pipeline,pipelinePath);
            }
            var ps=new SerializedObject(pipeline);ps.FindProperty("m_RendererDataList").arraySize=1;
            ps.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue=renderer;
            ps.FindProperty("m_DefaultRendererIndex").intValue=0;
            ps.FindProperty("m_ShadowDistance").floatValue=3;
            ps.FindProperty("m_ShadowCascadeCount").intValue=1;
            ps.FindProperty("m_MainLightShadowsSupported").boolValue=true;
            ps.FindProperty("m_MainLightShadowmapResolution").intValue=1024;
            ps.FindProperty("m_ShadowDepthBias").floatValue=.15f;
            ps.FindProperty("m_ShadowNormalBias").floatValue=.15f;
            ps.FindProperty("m_AdditionalLightsRenderingMode").intValue=0;
            ps.FindProperty("m_AdditionalLightShadowsSupported").boolValue=false;
            ps.FindProperty("m_SoftShadowsSupported").boolValue=true;
            ps.FindProperty("m_SoftShadowQuality").intValue=1;
            ps.FindProperty("m_RequireDepthTexture").boolValue=false;
            ps.FindProperty("m_RequireOpaqueTexture").boolValue=false;
            ps.FindProperty("m_SupportsHDR").boolValue=false;
            ps.FindProperty("m_MSAA").intValue=4;
            ps.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(renderer);EditorUtility.SetDirty(pipeline);
            return pipeline;
        }
        static Material Material(string semantic,Color color,Shader shader)
        {
            string key=semantic.Replace(" (Instance)","");
            if(Materials.TryGetValue(key,out var found))return found;
            string name=string.Concat(key.Select(c=>char.IsLetterOrDigit(c)||c=='_'?c:'_'));
            string path=Output+"/Materials/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}else mat.shader=shader;
            mat.name=key;mat.SetColor("_BaseColor",color);mat.enableInstancing=true;
            if(mat.HasProperty("_Wind"))mat.SetFloat("_Wind",key.ToLowerInvariant().Contains("leaf") || key.ToLowerInvariant().Contains("canopy")?1:0);
            if(mat.HasProperty("_GroundDetail"))mat.SetFloat("_GroundDetail",key=="Moss"?1:0);
            if(mat.HasProperty("_Roughness"))mat.SetFloat("_Roughness",key.ToLowerInvariant().Contains("metal")?.35f:.75f);
            EditorUtility.SetDirty(mat);Materials[key]=mat;return mat;
        }
    }
}

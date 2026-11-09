using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace CreekDiorama.Editor
{
    // Environment authoring only. Does not modify the car game or add collision walls.
    public static class CreekEnvironmentImporter
    {
        const string Root = "Assets/CreekDiorama";
        public const string ScenePath = Root + "/Scenes/CreekEnvironment.unity";
        [Serializable] class Palette { public Entry[] materials; }
        [Serializable] class Entry { public string name; public float[] color; public float metallic, roughness; }

        [MenuItem("MetaHackthone/Creek Environment/Import Blender environment")]
        public static void Import()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before importing.");
            for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
                if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes first.");
            foreach(var folder in new[]{"Materials","Meshes","Prefabs","Scenes"}) Directory.CreateDirectory(Root+"/"+folder);
            AssetDatabase.Refresh();
            var shader=Shader.Find("Universal Render Pipeline/Lit");
            if(!shader) throw new InvalidOperationException("URP Lit shader missing.");
            var palette=JsonUtility.FromJson<Palette>(File.ReadAllText("ArtSource/CreekDiorama/palette.json"));
            var materials=new Dictionary<string,Material>();
            foreach(var entry in palette.materials)
            {
                var path=Root+"/Materials/"+entry.name+".mat";
                var m=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!m){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}
                m.name=entry.name;
                // Blender's diffuse colors are linear; Unity stores material color properties in sRGB.
                m.SetColor("_BaseColor",new Color(entry.color[0],entry.color[1],entry.color[2],1).gamma);
                m.SetFloat("_Metallic",entry.metallic);m.SetFloat("_Smoothness",1-entry.roughness);
                // Foliage triangles and waterfall ribbons should read from either bank.
                m.SetFloat("_Cull",0);m.doubleSidedGI=true;
                if(entry.name=="Amber lamps") { m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",new Color(1,.34f,.035f)*3); }
                m.enableInstancing=true;EditorUtility.SetDirty(m);materials[entry.name]=m;
            }
            var importer=(ModelImporter)AssetImporter.GetAtPath(Root+"/Models/CreekDiorama.fbx");
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            importer.importCameras=false;importer.importLights=false;importer.importAnimation=false;
            importer.isReadable=true;importer.SaveAndReimport();
            foreach(var external in AssetDatabase.LoadAllAssetsAtPath(Root+"/Models/CreekDiorama.fbx").OfType<Material>())
                if(materials.TryGetValue(external.name,out var material))
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),external.name),material);
            importer.SaveAndReimport();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Models/CreekDiorama.fbx"));
            model.name="Creek environment — original Blender meshes";
            var bounds=new Bounds();bool first=true;
            foreach(var r in model.GetComponentsInChildren<Renderer>()) {if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
            // The full plinth is a 2.86 m desktop miniature, irrespective of FBX unit metadata.
            model.transform.localScale*=2.86f/bounds.size.x;
            SaveProp(model.transform,"Pine tree","PineTree",materials);
            SaveProp(model.transform,"River boulder","RiverBoulder",materials);
            SaveProp(model.transform,"Deck plank 00","BridgePlank",materials);
            var result=new GameObject("Creek Environment");
            foreach(Transform child in model.transform)
            {
                if(child.GetComponentsInChildren<MeshFilter>().Length==0)continue;
                var group=new GameObject(System.Text.RegularExpressions.Regex.Replace(child.name,@"\.\d{3}$", ""));group.transform.SetParent(result.transform,false);
                Combine(child,group.transform,materials);
                PrefabUtility.SaveAsPrefabAsset(group,Root+"/Prefabs/"+Safe(group.name)+".prefab");
            }
            Object.DestroyImmediate(model);
            CreekWaterAuthoring.ApplyToRoot(result);
            foreach(Transform group in result.transform)
                PrefabUtility.SaveAsPrefabAsset(group.gameObject,Root+"/Prefabs/"+Safe(group.name)+".prefab");
            PrefabUtility.SaveAsPrefabAsset(result,Root+"/Prefabs/CreekEnvironment.prefab");
            var camera=new GameObject("Environment preview camera").AddComponent<Camera>();
            camera.tag="MainCamera";camera.transform.position=new Vector3(-2.0f,2.8f,3.8f);
            camera.transform.LookAt(new Vector3(0,.33f,0));camera.orthographic=true;camera.orthographicSize=1.25f;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.065f,.086f,.11f);
            camera.nearClipPlane=.01f;camera.farClipPlane=30;
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
            CreekWaterAuthoring.ConfigureCamera(camera);
            var light=new GameObject("Warm afternoon sunlight").AddComponent<Light>();
            light.type=LightType.Directional;light.color=new Color(1,.86f,.66f);light.intensity=1.45f;
            light.shadows=LightShadows.Soft;light.shadowBias=.025f;light.shadowNormalBias=.12f;
            light.transform.rotation=Quaternion.Euler(48,145,0);
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.49f,.60f,.70f);
            RenderSettings.ambientEquatorColor=new Color(.34f,.38f,.40f);
            RenderSettings.ambientGroundColor=new Color(.20f,.19f,.15f);
            RenderSettings.fog=false;
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            Selection.activeGameObject=result;
            if(SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.FrameSelected();
            Debug.Log("CREEK_ENVIRONMENT_READY renderers="+result.GetComponentsInChildren<Renderer>().Length+" colliders="+result.GetComponentsInChildren<Collider>().Length);
            Capture();
        }
        static string Safe(string s) => string.Join("_",s.Split(Path.GetInvalidFileNameChars())).Replace(' ','_');
        static void SaveProp(Transform model,string prefix,string name,Dictionary<string,Material> palette)
        {
            var source=model.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name.StartsWith(prefix,StringComparison.Ordinal));
            if(!source)return;
            var renderers=source.GetComponentsInChildren<Renderer>();if(renderers.Length==0)return;
            var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            var root=new GameObject(name);root.transform.position=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            Combine(source,root.transform,palette);root.transform.position=Vector3.zero;
            PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/"+name+".prefab");Object.DestroyImmediate(root);
        }
        static void Combine(Transform source,Transform output,Dictionary<string,Material> palette)
        {
            var batches=new Dictionary<Material,List<CombineInstance>>();
            foreach(var filter in source.GetComponentsInChildren<MeshFilter>())
            {
                var renderer=filter.GetComponent<MeshRenderer>();if(!renderer || !filter.sharedMesh)continue;
                for(int i=0;i<filter.sharedMesh.subMeshCount;i++)
                {
                    var material=renderer.sharedMaterials[Mathf.Min(i,renderer.sharedMaterials.Length-1)];
                    if(palette.TryGetValue(material.name,out var mapped))material=mapped;
                    if(!batches.TryGetValue(material,out var list))batches[material]=list=new List<CombineInstance>();
                    list.Add(new CombineInstance{mesh=filter.sharedMesh,subMeshIndex=i,transform=output.worldToLocalMatrix*filter.transform.localToWorldMatrix});
                }
            }
            foreach(var batch in batches)
            {
                var mesh=new Mesh{name=output.name+" "+batch.Key.name,indexFormat=IndexFormat.UInt32};
                mesh.CombineMeshes(batch.Value.ToArray(),true,true);mesh.RecalculateBounds();
                var path=Root+"/Meshes/"+Safe(mesh.name)+".asset";
                var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;EditorUtility.SetDirty(mesh);}
                else AssetDatabase.CreateAsset(mesh,path);
                var part=new GameObject(batch.Key.name);part.transform.SetParent(output,false);
                part.AddComponent<MeshFilter>().sharedMesh=mesh;part.AddComponent<MeshRenderer>().sharedMaterial=batch.Key;
            }
        }
        [MenuItem("MetaHackthone/Creek Environment/Capture preview")]
        public static void Capture()
        {
            var camera=Camera.main;if(!camera)throw new InvalidOperationException("Open the environment scene first.");
            var rt=new RenderTexture(1600,1100,24,RenderTextureFormat.ARGB32);rt.Create();
            var previous=RenderTexture.active;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                RenderTexture.active=rt;
                var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
                Directory.CreateDirectory("Docs/CreekDiorama");File.WriteAllBytes("Docs/CreekDiorama/CreekDiorama-Unity.png",image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }
            finally {RenderTexture.active=previous;rt.Release();Object.DestroyImmediate(rt);}
        }
    }
}

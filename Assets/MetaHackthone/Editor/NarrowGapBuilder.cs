using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CloudGarden.Integration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace PhysicalAssembly.Editor
{
    public static class NarrowGapBuilder
    {
        public const string ScenePath="Assets/MetaHackthone/Scenes/NarrowGap.unity";
        const string Art="Assets/MetaHackthone/Art/NarrowGap";
        const string SettingsPath="Assets/MetaHackthone/Generated/NarrowGapSettings.asset";
        [MenuItem("MetaHackthone/Narrow Gap/Build level assets")]
        public static void Build()
        {
            BuildingSceneAuthoring.ProtectAuthoredScene(ScenePath);
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before rebuilding the level.");
            for(int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before rebuilding the level.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            Directory.CreateDirectory(Art); AssetDatabase.Refresh();
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var settings=AssetDatabase.LoadAssetAtPath<NarrowGapSettings>(SettingsPath);
                if(!settings) { settings=ScriptableObject.CreateInstance<NarrowGapSettings>(); AssetDatabase.CreateAsset(settings,SettingsPath); AssetDatabase.SaveAssets(); }
                settings.PadSize=new Vector3(BuildingGeometry.Unit,.06f,BuildingGeometry.Unit);
                var course=NarrowGapCourse.Build(settings); CombineStaticVisuals(course);
                int index=0; var persisted=new Dictionary<UnityEngine.Object,UnityEngine.Object>();
                T Save<T>(T item,string extension) where T:UnityEngine.Object
                {
                    if(!item || AssetDatabase.Contains(item)) return item;
                    if(persisted.TryGetValue(item,out var saved)) return (T)saved;
                    string path=$"{Art}/Course_{index++:000}.{extension}";
                    var existing=AssetDatabase.LoadAssetAtPath<T>(path);
                    if(existing) { EditorUtility.CopySerialized(item,existing); EditorUtility.SetDirty(existing); persisted[item]=existing; return existing; }
                    AssetDatabase.CreateAsset(item,path); persisted[item]=item; return item;
                }
                foreach(var renderer in course.GetComponentsInChildren<Renderer>(true))
                {
                    var materials=renderer.sharedMaterials;
                    for(int i=0;i<materials.Length;i++) if(materials[i] && !AssetDatabase.Contains(materials[i]))
                    {
                        var m=materials[i]; if(m.mainTexture && !AssetDatabase.Contains(m.mainTexture)) m.mainTexture=Save(m.mainTexture,"asset");
                        materials[i]=Save(m,"mat");
                    }
                    renderer.sharedMaterials=materials;
                }
                foreach(var filter in course.GetComponentsInChildren<MeshFilter>(true)) filter.sharedMesh=Save(filter.sharedMesh,"asset");
                foreach(var collider in course.GetComponentsInChildren<Collider>(true)) if(collider.sharedMaterial) collider.sharedMaterial=Save(collider.sharedMaterial,"physicMaterial");
                course.LaunchSound=SaveAudio(course.LaunchSound,"Launch"); course.WinSound=SaveAudio(course.WinSound,"Win");
                var prefab=PrefabUtility.SaveAsPrefabAsset(course.gameObject,Art+"/NarrowGapCourse.prefab");
                settings=AssetDatabase.LoadAssetAtPath<NarrowGapSettings>(SettingsPath);
                settings.CoursePrefab=prefab.GetComponent<NarrowGapCourse>(); EditorUtility.SetDirty(settings);
                UnityEngine.Object.DestroyImmediate(course.gameObject);
                var entry=new GameObject("Narrow Gap level").AddComponent<NarrowGapEntry>(); entry.Settings=settings;
                entry.StandalonePlayer=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CloudGarden/Prefabs/MetaHackathonPlayer.prefab");
                if(!entry.StandalonePlayer) throw new InvalidOperationException("Shared Meta player prefab is missing.");
                var light=new GameObject("Warm miniature light").AddComponent<Light>(); light.type=LightType.Directional; light.color=new Color(1,.88f,.69f); light.intensity=1.25f; light.shadows=LightShadows.None; light.transform.rotation=Quaternion.Euler(48,-30,0);
                RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.65f,.68f,.72f);
                var camera=new GameObject("Scene preview camera").AddComponent<Camera>(); camera.enabled=false; camera.transform.position=new Vector3(2.3f,2.5f,-3.4f); camera.transform.LookAt(new Vector3(0,.9f,.9f));
                BuildingSceneAuthoring.ConvertScene(SceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ScenePath);
                EditorBuildSettings.scenes=EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath).Concat(new[]{new EditorBuildSettingsScene(ScenePath,true)}).ToArray();
                AssetDatabase.SaveAssets(); Debug.Log("NARROW_GAP_ASSETS_READY");
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }
        static void CombineStaticVisuals(NarrowGapCourse course)
        {
            // Keep every movable upper-pad mesh and the aiming guide independent.
            var filters=course.GetComponentsInChildren<MeshFilter>(true).Where(f=>!f.GetComponent<TextMesh>() && f.GetComponent<MeshRenderer>() && f.GetComponent<MeshRenderer>().enabled && !f.transform.IsChildOf(course.PadVisual) && f.transform!=course.PadCollider.transform && !f.transform.IsChildOf(course.Trajectory) && !f.transform.IsChildOf(course.SuccessStars)).ToArray();
            foreach(var group in filters.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial))
            {
                var mesh=new Mesh { name="Combined "+group.Key.name,indexFormat=IndexFormat.UInt32 };
                mesh.CombineMeshes(group.Select(f=>new CombineInstance { mesh=f.sharedMesh,transform=course.transform.worldToLocalMatrix*f.transform.localToWorldMatrix }).ToArray());
                var go=new GameObject(mesh.name); go.transform.SetParent(course.transform,false); go.AddComponent<MeshFilter>().sharedMesh=mesh; go.AddComponent<MeshRenderer>().sharedMaterial=group.Key;
            }
            foreach(var filter in filters) { UnityEngine.Object.DestroyImmediate(filter.GetComponent<MeshRenderer>()); UnityEngine.Object.DestroyImmediate(filter); }
        }
        static AudioClip SaveAudio(AudioClip clip,string name)
        {
            string path=Art+"/"+name+".wav"; var samples=new float[clip.samples]; clip.GetData(samples,0);
            using(var writer=new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36+samples.Length*2); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(clip.frequency); writer.Write(clip.frequency*2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length*2); foreach(float sample in samples) writer.Write((short)(Mathf.Clamp(sample,-1,1)*32767));
            }
            UnityEngine.Object.DestroyImmediate(clip); AssetDatabase.ImportAsset(path); return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        [MenuItem("MetaHackthone/Narrow Gap/Play level")]
        public static void Play()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) return;
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath); EditorApplication.EnterPlaymode();
        }
        public static void CapturePreview()
        {
            var input=UnityEngine.Object.FindFirstObjectByType<DesktopInputAdapter>();
            if(!input || !input.Workshop.IsChallenge) throw new InvalidOperationException("Play Narrow Gap first.");
            input.FramePlatform(); var camera=input.View;
            var rt=new RenderTexture(1600,1100,24); rt.Create();
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination=rt });
            var old=RenderTexture.active; RenderTexture.active=rt;
            var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); image.Apply();
            Directory.CreateDirectory("Docs"); File.WriteAllBytes("Docs/NarrowGapPreview.png",image.EncodeToPNG());
            RenderTexture.active=old; rt.Release(); UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}

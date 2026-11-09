using System;
using System.Linq;
using CloudGarden.Integration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace PhysicalAssembly.Editor
{
    public static class DownhillBuilder
    {
        public const string ScenePath="Assets/MetaHackthone/Scenes/DownhillSandbox.unity";
        public const string SettingsPath="Assets/MetaHackthone/Generated/DownhillSettings.asset";
        [MenuItem("MetaHackthone/Downhill/Build level assets")]
        public static void Build()
        {
            BuildingSceneAuthoring.ProtectAuthoredScene(ScenePath);
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit play mode first.");
            for(int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes first.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var settings=AssetDatabase.LoadAssetAtPath<DownhillSettings>(SettingsPath);
                if(!settings) { settings=ScriptableObject.CreateInstance<DownhillSettings>(); AssetDatabase.CreateAsset(settings,SettingsPath); }
                var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CloudGarden/Prefabs/MetaHackathonPlayer.prefab");
                if(!player) throw new InvalidOperationException("Shared player prefab missing.");
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var entry=new GameObject("Downhill Sandbox level").AddComponent<DownhillEntry>(); entry.Settings=settings; entry.StandalonePlayer=player;
                var light=new GameObject("Warm tabletop light").AddComponent<Light>(); light.type=LightType.Directional; light.color=new Color(1,.88f,.72f); light.intensity=1.2f; light.shadows=LightShadows.None; light.transform.rotation=Quaternion.Euler(48,-35,0);
                RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.62f,.66f,.70f);
                var camera=new GameObject("Scene preview camera").AddComponent<Camera>(); camera.enabled=false; camera.transform.position=new Vector3(.5f,2.6f,-3); camera.transform.LookAt(new Vector3(0,.9f,.2f));
                BuildingSceneAuthoring.ConvertScene(scene);
                EditorSceneManager.SaveScene(scene,ScenePath);
                EditorBuildSettings.scenes=EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath).Concat(new[]{new EditorBuildSettingsScene(ScenePath,true)}).ToArray();
                UpdateExistingWheel(); AssetDatabase.SaveAssets();
                Debug.Log("DOWNHILL_ASSETS_READY");
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }
        static void UpdateExistingWheel()
        {
            const string path="Assets/MetaHackthone/Generated/Wheel.prefab";
            var prefab=AssetDatabase.LoadAssetAtPath<AssemblyPart>(path); if(!prefab) throw new InvalidOperationException("Existing wheel prefab missing.");
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                WheelHubGeometry.Apply(root.GetComponent<AssemblyPart>());
                var bearing=root.transform.Find("hub/Recessed bearing");
                if(bearing)
                {
                    var filter=bearing.GetComponent<MeshFilter>();
                    if(!AssetDatabase.Contains(filter.sharedMesh))
                    {
                        const string meshPath="Assets/MetaHackthone/Generated/RecessedWheelBearing.asset";
                        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                        if(mesh) { EditorUtility.CopySerialized(filter.sharedMesh,mesh); EditorUtility.SetDirty(mesh); }
                        else { mesh=filter.sharedMesh; AssetDatabase.CreateAsset(mesh,meshPath); }
                        filter.sharedMesh=mesh; bearing.GetComponent<MeshCollider>().sharedMesh=mesh;
                    }
                }
                foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    var material=renderer.sharedMaterial;
                    if(!material || AssetDatabase.Contains(material)) continue;
                    string materialPath="Assets/MetaHackthone/Generated/Wheel_"+renderer.name.Replace(' ','_')+".mat";
                    var existing=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if(existing) { EditorUtility.CopySerialized(material,existing); EditorUtility.SetDirty(existing); renderer.sharedMaterial=existing; }
                    else AssetDatabase.CreateAsset(material,materialPath);
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}

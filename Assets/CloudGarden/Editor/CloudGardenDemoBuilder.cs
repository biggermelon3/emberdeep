using System.IO;
using System.Linq;
using CloudGarden.Integration;
using PhysicalAssembly.MetaXR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloudGarden.Editor
{
    public static class CloudGardenDemoBuilder
    {
        public const string ScenePath = "Assets/CloudGarden/Scenes/CloudGardenDemo.unity";
        const string WorkshopPath = "Assets/MetaHackthone/Scenes/Workshop.unity";
        [MenuItem("MetaHackthone/CloudGarden/Create or rebuild demo")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play mode before rebuilding the demo.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new System.InvalidOperationException("Save open scenes before rebuilding the demo.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            Directory.CreateDirectory("Assets/CloudGarden/Scenes"); Directory.CreateDirectory("Assets/CloudGarden/Prefabs"); AssetDatabase.Refresh();
            try
            {
                var workshop = EditorSceneManager.OpenScene(WorkshopPath, OpenSceneMode.Single);
                var bridge = Object.FindFirstObjectByType<MetaHandInputAdapter>();
                var container = new GameObject("MetaHackathon player"); container.SetActive(false);
                // Temporarily reparent the existing player in an unsaved scene;
                // cloning a live OVR rig invokes SDK singleton checks in edit mode.
                var rig = bridge.XRRig.gameObject; rig.transform.SetParent(container.transform, true);
                var desktop = bridge.DesktopCamera; desktop.transform.SetParent(container.transform, true); desktop.tag = "MainCamera";
                if (!desktop.GetComponent<AudioListener>()) desktop.gameObject.AddComponent<AudioListener>();
                var player = rig.GetComponent<MetaPlayerRig>(); if (!player) player = rig.AddComponent<MetaPlayerRig>();
                player.XRRig = rig.transform; player.DesktopCamera = desktop;
                MetaHandInputAdapter.ConfigureRoomScaleRig(rig.transform);
                container.SetActive(true);
                var prefab = PrefabUtility.SaveAsPrefabAsset(container, "Assets/CloudGarden/Prefabs/MetaHackathonPlayer.prefab");
                Object.DestroyImmediate(container);
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var demo = new GameObject("CloudGarden demo").AddComponent<CloudGardenDemo>(); demo.StandalonePlayer = prefab;
                demo.Settings = AssetDatabase.LoadAssetAtPath<GardenSettings>("Assets/CloudGarden/ScriptableObjects/GardenSettings.asset");
                demo.Settings.surfaceShader = Shader.Find("Universal Render Pipeline/Lit");
                demo.Settings.glowShader = Shader.Find("Universal Render Pipeline/Unlit");
                demo.Settings.particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit"); EditorUtility.SetDirty(demo.Settings);
                var light = new GameObject("Garden daylight").AddComponent<Light>(); light.type = LightType.Directional;
                light.intensity = 1.5f; light.color = new Color(1, .93f, .82f); light.transform.rotation = Quaternion.Euler(48, -32, 0);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.64f, .73f, .78f);
                EditorSceneManager.SaveScene(scene, ScenePath);
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(WorkshopPath, true), new EditorBuildSettingsScene(ScenePath, true) }
                    .Concat(EditorBuildSettings.scenes.Where(s => s.path != WorkshopPath && s.path != ScenePath)).ToArray();
                const string menuPath="Assets/MetaHackthone/Scenes/MainMenu.unity";
                if(File.Exists(menuPath)) EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(menuPath,true)}.Concat(EditorBuildSettings.scenes.Where(s=>s.path!=menuPath)).ToArray();
                AssetDatabase.SaveAssets();
                Debug.Log("CLOUDGARDEN_DEMO_READY " + ScenePath);
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }
        [MenuItem("MetaHackthone/CloudGarden/Play demo")]
        public static void Play()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath); EditorApplication.isPlaying = true;
        }
        public static void BuildWindows()
        {
            Directory.CreateDirectory("Builds/CloudGardenDemo");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                target = BuildTarget.StandaloneWindows64,
                locationPathName = "Builds/CloudGardenDemo/MetaHackathon.exe",
                options = BuildOptions.Development
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.InvalidOperationException("CloudGarden Windows build failed: " + report.summary.result);
            Debug.Log("CLOUDGARDEN_WINDOWS_BUILD_OK");
        }
    }
}

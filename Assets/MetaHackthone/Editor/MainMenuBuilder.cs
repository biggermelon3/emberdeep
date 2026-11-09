using System;
using System.IO;
using System.Linq;
using CloudGarden.Integration;
using PhysicalAssembly.MetaXR;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PhysicalAssembly.Editor
{
    public sealed class MainMenuBuilder : IPreprocessBuildWithReport
    {
        public const string ScenePath="Assets/MetaHackthone/Scenes/MainMenu.unity";
        const string Art="Assets/MetaHackthone/Generated/MainMenu";
        public int callbackOrder=>0;
        public void OnPreprocessBuild(BuildReport report)
        {
            if(report.summary.platform==BuildTarget.Android && PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)=="com.metahackathon2026.emberdeep")
            {
                var expected=new[]{"Assets/FireRescue/Scenes/EmberdeepCamp.unity","Assets/FireRescue/Scenes/EmberdeepFireground.unity"};
                if(!EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).SequenceEqual(expected))
                    throw new BuildFailedException("Emberdeep must contain only its fire camp and fireground scenes, in that order.");
                return;
            }
            if(EditorBuildSettings.scenes.FirstOrDefault(s=>s.enabled)?.path!=ScenePath)
                throw new BuildFailedException("MainMenu must be the first enabled build scene. Use MetaHackthone/Main Menu/Create or update startup menu.");
        }
        public static void EnsureFirstScene()
        {
            if(!File.Exists(ScenePath)) return;
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)}
                .Concat(EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath)).ToArray();
        }
        [MenuItem("MetaHackthone/Main Menu/Create or update startup menu")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            for(int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes first.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Directory.CreateDirectory(Art); AssetDatabase.Refresh();
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CloudGarden/Prefabs/MetaHackathonPlayer.prefab");
                if(!prefab) throw new InvalidOperationException("Shared player prefab is missing.");
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var menu=new GameObject("Main Menu").AddComponent<MainMenuBootstrap>(); menu.PlayerPrefab=prefab;
                menu.PanelMaterial=Material("Panel",new Color(.025f,.045f,.065f));
                menu.CardMaterial=Material("Card",new Color(.065f,.13f,.16f));
                menu.AccentMaterial=Material("Placement",new Color(.07f,.27f,.25f));
                var light=new GameObject("Menu light").AddComponent<Light>(); light.type=LightType.Directional; light.intensity=.8f;
                menu.MenuLight=light;
                light.transform.rotation=Quaternion.Euler(45,-30,0); light.shadows=LightShadows.None;
                // The player prefab supplies both headset and desktop cameras at runtime.
                RenderSettings.fog=false;
                EditorSceneManager.SaveScene(scene,ScenePath);
                // Keep the authored fallback rig dormant until MetaHandInputAdapter resolves the shared player.
                var workshop=EditorSceneManager.OpenScene("Assets/MetaHackthone/Scenes/Workshop.unity",OpenSceneMode.Single);
                var input=workshop.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MetaHandInputAdapter>(true)).Single();
                input.XRRig.gameObject.SetActive(false); input.DesktopCamera.gameObject.SetActive(false);
                WorkshopBootstrap.ConfigureNavigationButtons(input.Workshop.Layout.transform);
                EditorSceneManager.SaveScene(workshop);
                EnsureFirstScene(); AssetDatabase.SaveAssets();
                Debug.Log("MAIN_MENU_ASSETS_READY");
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }
        static Material Material(string name,Color color)
        {
            var path=Art+"/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material)
            {
                var shader=Shader.Find("Universal Render Pipeline/Unlit");
                if(!shader) throw new InvalidOperationException("URP Unlit shader is missing.");
                material=new Material(shader); AssetDatabase.CreateAsset(material,path);
            }
            material.color=color; EditorUtility.SetDirty(material); return material;
        }
    }
}

using System;
using System.IO;
using System.Linq;
using PhysicalAssembly.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ShapeOfMemory.Editor
{
    // The deliverable: Scenes/PicoFinal.unity holds only the MemoryGame director (it builds everything else at runtime).
    // The scene stays out of the shared build list; Build Pico APK builds it alone, under its own package id and name.
    public static class ShapeOfMemoryBuilder
    {
        public const string Root="Assets/ShadowWorkshop/PicoFinal";
        public const string Scene=Root+"/Scenes/PicoFinal.unity";
        public const string IconFolder=Root+"/Art/Icons";
        public const string IconMaterialPath=Root+"/Materials/Memory icon.mat";
        public const string PackageId="com.metahackathon2026.shadow",ProductName="Shape of Memory";
        public const string Apk="Builds/ShapeOfMemory-Pico.apk";

        [MenuItem("MetaHackthone/Pico Final/Create or rebuild scene")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode first.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save open scenes first.");
            var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CloudGarden/Prefabs/MetaHackathonPlayer.prefab");
            if(!player)throw new InvalidOperationException("Rebuild the existing CloudGarden shared-player prefab first.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var icons=ImportIcons();var iconMaterial=IconMaterial();
                Directory.CreateDirectory(Path.GetDirectoryName(Scene));
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var game=new GameObject("Shape of Memory").AddComponent<MemoryGame>();
                game.StandalonePlayer=player;game.IconMaterial=iconMaterial;game.Icons=icons;
                game.Surface=AssetDatabase.LoadAssetAtPath<Material>("Assets/ShadowWorkshop/Materials/Graybox surfaces.mat");
                game.Screen=AssetDatabase.LoadAssetAtPath<Material>("Assets/ShadowWorkshop/Materials/Projection screen.mat");
                game.Glow=AssetDatabase.LoadAssetAtPath<Material>("Assets/ShadowWorkshop/Materials/Tracked hands and lamp.mat");
                // Same light as the other shadow scenes: the pile's materials are URP Lit.
                var light=new GameObject("Shape of Memory lighting").AddComponent<Light>();light.type=LightType.Directional;
                light.intensity=1.2f;light.color=new Color(1,.91f,.78f);light.shadows=LightShadows.None;light.transform.rotation=Quaternion.Euler(48,-28,0);
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.65f,.70f);
                EditorSceneManager.SaveScene(scene,Scene);AssetDatabase.SaveAssets();
                Debug.Log("SHAPE_OF_MEMORY_SCENE_READY icons="+icons.Length);
            }
            finally{if(setup.Any(s=>s.isLoaded&&s.isActive))EditorSceneManager.RestoreSceneManagerSetup(setup);}
        }
        [MenuItem("MetaHackthone/Pico Final/Play")]
        public static void Play()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(!File.Exists(Scene))Build();
            EditorSceneManager.OpenScene(Scene);EditorApplication.isPlaying=true;
        }
        // The editor's own progress, and the game's on every connected headset: the headset keeps its PlayerPrefs in the
        // app's data, so clearing that data resets it (the app, if running, closes and starts fresh next time).
        [MenuItem("MetaHackthone/Pico Final/Clear saved progress")]
        public static void ClearProgress()
        {
            foreach(var level in MemoryProgress.Levels)foreach(var key in new[]{"Solved.","Hints.","Intro."})PlayerPrefs.DeleteKey("ShapeOfMemory."+key+level);
            PlayerPrefs.DeleteKey("ShapeOfMemory.Lift");PlayerPrefs.DeleteKey("ShapeOfMemory.Taught");PlayerPrefs.Save();
            var cleared=new System.Collections.Generic.List<string>();
            foreach(var line in Adb("devices").Split('\n'))
            {
                var parts=line.Trim().Split('\t');if(parts.Length<2||parts[1]!="device")continue;
                cleared.Add(parts[0]+"="+Adb($"-s {parts[0]} shell pm clear {PackageId}").Trim());
            }
            Debug.Log("SHAPE_OF_MEMORY_PROGRESS_CLEARED editor devices: "+(cleared.Count>0?string.Join(", ",cleared):"none connected"));
        }
        // The editor's adb: a second adb (the one on PATH) would kill the editor's adb server.
        static string Adb(string arguments)
        {
            string sdk=UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath,adb=Path.Combine(sdk??"","platform-tools","adb.exe");
            if(!File.Exists(adb))adb="adb";
            try
            {
                using var process=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(adb,arguments){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true});
                string output=process.StandardOutput.ReadToEnd()+process.StandardError.ReadToEnd();process.WaitForExit(15000);return output;
            }
            catch(Exception e){Debug.LogWarning("adb "+arguments+" failed: "+e.Message);return "";}
        }
        [MenuItem("MetaHackthone/Pico Final/Build Pico APK")]
        public static void BuildPico()=>Reveal(BuildPico(false));
        [MenuItem("MetaHackthone/Pico Final/Build Pico APK (development)")]
        public static void BuildPicoDevelopment()=>Reveal(BuildPico(true));
        static void Reveal(BuildReport report){if(report.summary.result==BuildResult.Succeeded&&File.Exists(Apk))EditorUtility.RevealInFinder(Path.GetFullPath(Apk));}
        // PicoBuild.Prepare, this scene alone under its own package id and name, then the Quest configuration and the
        // project's id and name are restored even when the build fails.
        public static BuildReport BuildPico(bool development)
        {
            if(EditorApplication.isPlaying||BuildPipeline.isBuildingPlayer)throw new InvalidOperationException("Wait until the editor is idle.");
            if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.Android)throw new InvalidOperationException("Switch the active build target to Android first.");
            if(!File.Exists(Scene))Build();
            var target=NamedBuildTarget.Android;string id=PlayerSettings.GetApplicationIdentifier(target),name=PlayerSettings.productName;
            PicoBuild.Prepare();
            try
            {
                PlayerSettings.SetApplicationIdentifier(target,PackageId);PlayerSettings.productName=ProductName;
                // An up-to-date incremental build leaves an identical APK untouched, old timestamp and all; start from none.
                Directory.CreateDirectory(Path.GetDirectoryName(Apk));if(File.Exists(Apk))File.Delete(Apk);
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},target=BuildTarget.Android,targetGroup=BuildTargetGroup.Android,
                    locationPathName=Apk,options=development?BuildOptions.Development:BuildOptions.None});
                var apk=new FileInfo(Apk);
                Debug.Log($"SHAPE_OF_MEMORY_PICO_BUILD result={report.summary.result} errors={report.summary.totalErrors} warnings={report.summary.totalWarnings} "+
                    (apk.Exists?$"apk={apk.FullName} bytes={apk.Length} written={apk.LastWriteTime:yyyy-MM-dd HH:mm:ss}":"apk=missing"));
                return report;
            }
            finally
            {
                PlayerSettings.SetApplicationIdentifier(target,id);PlayerSettings.productName=name;
                PicoBuild.Restore();AssetDatabase.SaveAssets();
            }
        }
        // White-on-transparent PNGs from Tools/ShadowArt/build_icons.py: sharp, no mips (they are small and close).
        static Texture2D[] ImportIcons()
        {
            var paths=Directory.Exists(IconFolder)?Directory.GetFiles(IconFolder,"*.png").Select(p=>p.Replace('\\','/')).OrderBy(p=>p).ToArray():Array.Empty<string>();
            foreach(var path in paths)
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);if(importer==null)continue;
                if(importer.alphaIsTransparency&&!importer.mipmapEnabled&&importer.wrapMode==TextureWrapMode.Clamp)continue;
                importer.textureType=TextureImporterType.Default;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
                importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            }
            return paths.Select(AssetDatabase.LoadAssetAtPath<Texture2D>).Where(t=>t).ToArray();
        }
        // URP Unlit with alpha clipping, so icons need no sorting.
        static Material IconMaterial()
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(IconMaterialPath);
            if(!material)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(IconMaterialPath));
                material=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Memory icon"};AssetDatabase.CreateAsset(material,IconMaterialPath);
            }
            material.SetFloat("_AlphaClip",1);material.SetFloat("_Cutoff",.5f);material.EnableKeyword("_ALPHATEST_ON");material.SetFloat("_Cull",0);
            material.renderQueue=(int)RenderQueue.AlphaTest;EditorUtility.SetDirty(material);return material;
        }
    }
}

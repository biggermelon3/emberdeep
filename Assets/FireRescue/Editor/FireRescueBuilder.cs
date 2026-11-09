using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FireRescue.Editor
{
    public static class FireRescueBuilder
    {
        public const string ScenePath="Assets/FireRescue/Scenes/FireRescue.unity";
        public const string CampScenePath="Assets/FireRescue/Scenes/EmberdeepCamp.unity";
        public const string ShowcaseScenePath="Assets/FireRescue/Scenes/EmberdeepFireground.unity";
        public const string ShowcasePackageId="com.metahackathon2026.emberdeep";
        public const string ShowcaseVersion="0.2.6";
        public const int ShowcaseVersionCode=9;
        public const string ShowcaseIconPath="Assets/FireRescue/Art/AppIcon/Emberdeep-Hydrant-Icon.png";
        const string ShowcaseIconBackgroundPath="Assets/FireRescue/Art/AppIcon/Emberdeep-Icon-Background.png";
        public const string ShowcaseApk="Deliverables/Emberdeep/Emberdeep-0.2.6.apk";
        public const string PicoShowcaseApk="Deliverables/Emberdeep/Emberdeep-0.2.6-Pico.apk";
        // Return a fresh whitelist so callers cannot accidentally mutate a future build.
        public static string[] ShowcaseScenes=>new[]{CampScenePath,ShowcaseScenePath};

        [MenuItem("Emberdeep/Create standalone showcase scenes")]
        public static void CreateShowcaseScenes()
        {
            RequireIdleCleanScenes();
            const string menuMaterialPath="Assets/FireRescue/Resources/EmberdeepMenuUnlit.mat";
            if(!AssetDatabase.LoadAssetAtPath<Material>(menuMaterialPath))
            {
                var shader=Shader.Find("Universal Render Pipeline/Unlit");
                if(!shader)throw new InvalidOperationException("Menu unlit shader missing.");
                var menuMaterial=new Material(shader){name="Emberdeep menu unlit"};
                menuMaterial.SetColor("_BaseColor",new Color(.025f,.055f,.067f));
                AssetDatabase.CreateAsset(menuMaterial,menuMaterialPath);
            }
            var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CloudGarden/Prefabs/MetaHackathonPlayer.prefab");
            var panel=AssetDatabase.LoadAssetAtPath<Material>("Assets/FireRescue/Materials/Station charcoal.mat");
            if(!player || !panel || !AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))
                throw new InvalidOperationException("The shared player, station material and original FireRescue scene must exist before creating the showcase.");
            var originals=Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
            // Unity can omit a clean untitled batch-mode scene from its saved setup.
            var setup=originals.Select(s=>new SceneSetup{path=s.path,isLoaded=s.isLoaded,isActive=s==SceneManager.GetActiveScene()}).ToArray();
            bool emptyStartup=originals.Length==1 && string.IsNullOrEmpty(originals[0].path) && originals[0].rootCount==0;
            if(!emptyStartup && originals.Any(s=>string.IsNullOrEmpty(s.path)))
                throw new InvalidOperationException("Save untitled scenes before generating the showcase.");
            try
            {
                // Unity forbids additive scene creation beside an untitled scene.
                // A headless empty startup scene contains no user content to retain.
                var camp=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,emptyStartup?NewSceneMode.Single:NewSceneMode.Additive);
                SceneManager.SetActiveScene(camp);
                foreach(var original in originals)
                    if(original.path==CampScenePath || original.path==ShowcaseScenePath)EditorSceneManager.CloseScene(original,true);
                var campRoot=new GameObject("Emberdeep camp").AddComponent<EmberdeepCamp>();
                campRoot.PlayerPrefab=player;campRoot.PanelMaterial=panel;
                if(!EditorSceneManager.SaveScene(camp,CampScenePath))throw new IOException("Unable to save the Emberdeep camp scene.");

                var source=SceneManager.GetSceneByPath(ScenePath);
                if(!source.IsValid() || !source.isLoaded)source=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
                // Save a copy before changing its component: the source scene and its
                // on-disk bytes are never changed, even when it was already open.
                if(!EditorSceneManager.SaveScene(source,ShowcaseScenePath,true))throw new IOException("Unable to copy the FireRescue scene.");
                var fireground=EditorSceneManager.OpenScene(ShowcaseScenePath,OpenSceneMode.Additive);
                var game=fireground.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<FireRescueGame>(true)).Single();
                game.StandaloneShowcase=true;EditorUtility.SetDirty(game);EditorSceneManager.MarkSceneDirty(fireground);
                if(!EditorSceneManager.SaveScene(fireground))throw new IOException("Unable to save the standalone fireground.");
                AssetDatabase.SaveAssets();Debug.Log("EMBERDEEP_SHOWCASE_SCENES_READY "+string.Join(" | ",ShowcaseScenes));
            }
            finally
            {
                if(emptyStartup)EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                else RestoreShowcaseSceneSetup(setup,originals);
            }
        }
        static void RequireIdleCleanScenes()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Stop Play mode and wait for any current build before preparing Emberdeep.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save open scenes before preparing Emberdeep: "+SceneManager.GetSceneAt(i).name);
        }
        static void RestoreShowcaseSceneSetup(SceneSetup[] setup,Scene[] originals)
        {
            if(setup.Length>0 && setup.All(s=>!string.IsNullOrEmpty(s.path)))
            {EditorSceneManager.RestoreSceneManagerSetup(setup);return;}
            // RestoreSceneManagerSetup cannot reopen an untitled scene by path.
            // Those original scene handles stayed loaded throughout generation.
            var restored=new Scene[originals.Length];
            for(int i=0;i<originals.Length;i++)
            {
                var scene=string.IsNullOrEmpty(setup[i].path)?originals[i]:SceneManager.GetSceneByPath(setup[i].path);
                if(!scene.IsValid())scene=EditorSceneManager.OpenScene(setup[i].path,setup[i].isLoaded?OpenSceneMode.Additive:OpenSceneMode.AdditiveWithoutLoading);
                else if(setup[i].isLoaded && !scene.isLoaded)scene=EditorSceneManager.OpenScene(setup[i].path,OpenSceneMode.Additive);
                else if(!setup[i].isLoaded && scene.isLoaded)EditorSceneManager.CloseScene(scene,false);
                restored[i]=scene;
                if(i>0)EditorSceneManager.MoveSceneAfter(scene,restored[i-1]);
                if(setup[i].isActive && scene.isLoaded)SceneManager.SetActiveScene(scene);
            }
            for(int i=SceneManager.sceneCount-1;i>=0;i--)
            {
                var scene=SceneManager.GetSceneAt(i);
                if(!restored.Any(s=>s.handle==scene.handle))EditorSceneManager.CloseScene(scene,true);
            }
        }

        // Intentionally leaves this temporary whitelist installed for the isolated
        // test invocation. The calling QA runner restores its build-settings backup.
        public static void PrepareShowcaseTests()
        {
            CreateShowcaseScenes();
            EditorBuildSettings.scenes=ShowcaseScenes.Select(p=>new EditorBuildSettingsScene(p,true)).ToArray();
            AssetDatabase.SaveAssets();Debug.Log("EMBERDEEP_SHOWCASE_TEST_SCENES_READY");
        }

        // Headless QA uses the generated portable rig in real camp/fire scenes.
        // BuildShowcase recreates the normal source scenes before platform processing.
        public static void PreparePicoShowcaseTests()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("This isolated test setup requires batch mode.");
            CreateShowcaseScenes();
            PhysicalAssembly.Editor.PicoBuild.Prepare();
            try
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MetaHackthone/Generated/PicoPlayer.prefab");
                if(!prefab)throw new InvalidOperationException("PICO player generation failed.");
                foreach(var path in ShowcaseScenes)
                {
                    var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                    foreach(var root in scene.GetRootGameObjects())
                    {
                        var camp=root.GetComponent<EmberdeepCamp>();if(camp){camp.PlayerPrefab=prefab;EditorUtility.SetDirty(camp);}
                        var game=root.GetComponent<FireRescueGame>();if(game){game.PlayerPrefab=prefab;EditorUtility.SetDirty(game);}
                    }
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                }
                EditorBuildSettings.scenes=ShowcaseScenes.Select(p=>new EditorBuildSettingsScene(p,true)).ToArray();
                AssetDatabase.SaveAssets();Debug.Log("EMBERDEEP_PICO_TEST_SCENES_READY");
            }
            finally { PhysicalAssembly.Editor.PicoBuild.Restore();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); }
        }

        [MenuItem("Emberdeep/Build standalone Quest showcase")]
        public static void BuildShowcaseQuest()=>BuildShowcase(false);
        [MenuItem("Emberdeep/Build standalone PICO showcase")]
        public static void BuildShowcasePico()=>BuildShowcase(true);
        static void BuildShowcase(bool pico)
        {
            RequireIdleCleanScenes();
            var android=NamedBuildTarget.Android;
            if(PlayerSettings.GetScriptingBackend(android)!=ScriptingImplementation.IL2CPP
                || PlayerSettings.Android.targetArchitectures!=AndroidArchitecture.ARM64)
                throw new InvalidOperationException("Emberdeep requires the project's IL2CPP / ARM64 Android configuration.");
            if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.Android)
                throw new InvalidOperationException("Switch the active build target to Android before building the Emberdeep showcase.");
            // A build must not regenerate authored scenes in the shared workspace.
            foreach(var path in ShowcaseScenes)
                if(!AssetDatabase.LoadAssetAtPath<SceneAsset>(path))throw new FileNotFoundException("Create the missing Emberdeep scene explicitly before building",path);
            var previousScenes=EditorBuildSettings.scenes;
            // Android build quality filtering can change the active level while a
            // simple renderPipeline getter/setter pair is in flight. Preserve the
            // entire serialized quality asset, including every excluded level.
            const string qualityPath="ProjectSettings/QualitySettings.asset";
            var previousQualityBytes=File.ReadAllBytes(qualityPath);
            var qualityObject=AssetDatabase.LoadAllAssetsAtPath(qualityPath)[0];
            var previousQualityJson=EditorJsonUtility.ToJson(qualityObject);
            string previousName=PlayerSettings.productName,previousVersion=PlayerSettings.bundleVersion;
            string previousIdentifier=PlayerSettings.GetApplicationIdentifier(android);
            int previousVersionCode=PlayerSettings.Android.bundleVersionCode;
            var previousMinSdk=PlayerSettings.Android.minSdkVersion;
            var previousIcons=CaptureAndroidIcons();
            bool previousBundle=EditorUserBuildSettings.buildAppBundle,previousDevelopment=EditorUserBuildSettings.development;
            bool picoPrepared=false;
            string output=pico?PicoShowcaseApk:ShowcaseApk;
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            try
            {
                if(pico)
                {
                    PhysicalAssembly.Editor.PicoBuild.Prepare();picoPrepared=true;
                    PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel29;
                }
                ApplyShowcaseIcons();
                var art=EmberdeepArtLibrary.Load();
                if(art && art.Pipeline)QualitySettings.renderPipeline=art.Pipeline;
                PlayerSettings.productName="Emberdeep";PlayerSettings.bundleVersion=ShowcaseVersion;PlayerSettings.Android.bundleVersionCode=ShowcaseVersionCode;
                PlayerSettings.SetApplicationIdentifier(android,ShowcasePackageId);
                EditorUserBuildSettings.buildAppBundle=false;EditorUserBuildSettings.development=false;
                EditorBuildSettings.scenes=ShowcaseScenes.Select(p=>new EditorBuildSettingsScene(p,true)).ToArray();
                // An interrupted build must not leave a previous success manifest
                // looking like evidence for the new source revision.
                File.WriteAllText(Path.ChangeExtension(output,"manifest.json"),JsonUtility.ToJson(new ShowcaseManifest{
                    productName="Emberdeep",packageId=ShowcasePackageId,version=ShowcaseVersion,versionCode=ShowcaseVersionCode,
                    iconSource=ShowcaseIconPath,iconSha256=FileSha256(ShowcaseIconPath),
                    createdUtc=DateTime.UtcNow.ToString("O"),result="Building",apkPath=output,requestedScenes=ShowcaseScenes,headset=pico?"PICO 4":"Quest"
                },true));
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                    scenes=ShowcaseScenes,target=BuildTarget.Android,targetGroup=BuildTargetGroup.Android,
                    // Cached player data can produce an empty packedAssets scene
                    // report in Unity 6. Use a clean release build for the audit.
                    locationPathName=output,options=BuildOptions.DetailedBuildReport|BuildOptions.CleanBuildCache});
                WriteShowcaseManifest(report,output,pico);
                if(report.summary.result!=BuildResult.Succeeded || report.summary.totalErrors>0)
                    throw new InvalidOperationException("Emberdeep showcase build failed: "+report.summary.result+" errors="+report.summary.totalErrors);
                Debug.Log("EMBERDEEP_SHOWCASE_BUILD_OK errors=0 scenes=2 apk="+Path.GetFullPath(output));
            }
            finally
            {
                try { if(picoPrepared)PhysicalAssembly.Editor.PicoBuild.Restore(); }
                finally
                {
                    PlayerSettings.productName=previousName;PlayerSettings.bundleVersion=previousVersion;PlayerSettings.Android.bundleVersionCode=previousVersionCode;
                    PlayerSettings.Android.minSdkVersion=previousMinSdk;
                    PlayerSettings.SetApplicationIdentifier(android,previousIdentifier);
                    EditorUserBuildSettings.buildAppBundle=previousBundle;EditorUserBuildSettings.development=previousDevelopment;
                    EditorBuildSettings.scenes=previousScenes;
                    EditorJsonUtility.FromJsonOverwrite(previousQualityJson,qualityObject);
                    EditorUtility.SetDirty(qualityObject);
                    try { RestoreAndroidIcons(previousIcons); }
                    finally
                    {
                        AssetDatabase.SaveAssets();
                        File.WriteAllBytes(qualityPath,previousQualityBytes);
                        AssetDatabase.ImportAsset(qualityPath,ImportAssetOptions.ForceUpdate);
                    }
                }
            }
        }
        sealed class AndroidIconSnapshot
        {
            public PlatformIconKind kind;
            public Texture2D[][] textures;
        }
        static AndroidIconSnapshot[] CaptureAndroidIcons()
        {
            // PlatformIcon instances may be reused by Unity. Copy the texture arrays,
            // including empty layers, rather than retaining mutable icon objects.
            return PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android).Select(kind=>new AndroidIconSnapshot{
                kind=kind,textures=PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android,kind).Select(icon=>icon.GetTextures()).ToArray()
            }).ToArray();
        }
        static void RestoreAndroidIcons(AndroidIconSnapshot[] snapshots)
        {
            foreach(var snapshot in snapshots)
            {
                var icons=PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android,snapshot.kind);
                if(icons.Length!=snapshot.textures.Length)throw new InvalidOperationException("Android icon slots changed during the build.");
                for(int i=0;i<icons.Length;i++)icons[i].SetTextures(snapshot.textures[i]);
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android,snapshot.kind,icons);
            }
        }
        static Texture2D ImportShowcaseIcon(string path)
        {
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(!importer)throw new InvalidOperationException("Emberdeep icon texture is missing: "+path);
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;
            importer.maxTextureSize=1024;importer.npotScale=TextureImporterNPOTScale.ToNearest;
            importer.mipmapEnabled=false;importer.alphaIsTransparency=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;
            importer.ClearPlatformTextureSettings("Android");importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static void ApplyShowcaseIcons()
        {
            if(!File.Exists(ShowcaseIconPath))throw new FileNotFoundException("Emberdeep generated icon source is missing.",ShowcaseIconPath);
            if(!File.Exists(ShowcaseIconBackgroundPath))
            {
                // This plain backing layer is generated through Unity, never hand-authored asset YAML.
                var backing=new Texture2D(32,32,TextureFormat.RGB24,false);
                try
                {
                    backing.SetPixels(Enumerable.Repeat(new Color(0.025f,0.075f,0.06f),32*32).ToArray());backing.Apply();
                    File.WriteAllBytes(ShowcaseIconBackgroundPath,backing.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(backing); }
            }
            var foreground=ImportShowcaseIcon(ShowcaseIconPath);
            var background=ImportShowcaseIcon(ShowcaseIconBackgroundPath);
            if(!foreground || foreground.width!=1024 || foreground.height!=1024)
                throw new InvalidOperationException("Emberdeep launcher icon must import at 1024x1024.");
            var kinds=new[]{AndroidPlatformIconKind.Legacy,AndroidPlatformIconKind.Round,AndroidPlatformIconKind.Adaptive};
            foreach(var kind in kinds)
            {
                var icons=PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android,kind);
                if(icons.Length==0)throw new InvalidOperationException("Required Android icon kind is unavailable: "+kind);
                foreach(var icon in icons)
                    // Android adaptive slots are background first, foreground second.
                    // Verified against Unity 6000.3's generated ic_launcher_*.png resources.
                    icon.SetTextures(kind.Equals(AndroidPlatformIconKind.Adaptive)?new[]{background,foreground}:new[]{foreground});
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android,kind,icons);
            }
            Debug.Log("EMBERDEEP_SHOWCASE_ICONS_APPLIED legacy/round/adaptive source="+ShowcaseIconPath+" imported="+foreground.width+"x"+foreground.height);
        }
        [Serializable] sealed class ShowcaseManifest
        {
            public string productName,packageId,version,createdUtc,result,apkPath,apkSha256,sceneEvidence,headset,iconSource,iconSha256;
            public int versionCode,minimumAndroidApi;
            public int errors,warnings;
            public bool development,sceneWhitelistPassed;
            public string[] requestedScenes,reportedScenes,packedScenes,unexpectedScenes,missingScenes,rawSceneReferences,outputFiles;
            public PackedSource[] packedAssets;
        }
        [Serializable] sealed class PackedSource { public string path;public long packedBytes; }
        static string NormalizeSceneEvidence(string value)
        {
            if(string.IsNullOrEmpty(value))return null;
            value=value.Replace('\\','/');int start=value.IndexOf("Assets/",StringComparison.Ordinal);
            int end=value.IndexOf(".unity",StringComparison.OrdinalIgnoreCase);
            return start>=0 && end>=start?value.Substring(start,end+6-start):null;
        }
        static void WriteShowcaseManifest(BuildReport report,string output,bool pico)
        {
            var packed=report.packedAssets.SelectMany(p=>p.contents).Where(a=>!string.IsNullOrEmpty(a.sourceAssetPath))
                .GroupBy(a=>a.sourceAssetPath.Replace('\\','/')).OrderBy(g=>g.Key,StringComparer.Ordinal)
                .Select(g=>new PackedSource{path=g.Key,packedBytes=g.Sum(a=>(long)a.packedSize)}).ToArray();
            var packedScenes=packed.Select(p=>p.path).Where(p=>p.EndsWith(".unity",StringComparison.OrdinalIgnoreCase)).Distinct().OrderBy(p=>p,StringComparer.Ordinal).ToArray();
            var raw=report.scenesUsingAssets.SelectMany(s=>s.list).SelectMany(a=>a.scenePaths).Where(s=>!string.IsNullOrEmpty(s)).Distinct().OrderBy(s=>s,StringComparer.Ordinal).ToArray();
            var reported=raw.Select(NormalizeSceneEvidence).Where(s=>s!=null).Distinct().OrderBy(s=>s,StringComparer.Ordinal).ToArray();
            var expected=ShowcaseScenes;
            var evidence=reported.Length>0?reported:packedScenes;
            var unexpected=evidence.Concat(packedScenes).Except(expected,StringComparer.Ordinal).Distinct().ToArray();
            var missing=expected.Except(evidence,StringComparer.Ordinal).ToArray();
            bool development=(report.summary.options&BuildOptions.Development)!=0;
            var manifest=new ShowcaseManifest{
                productName=PlayerSettings.productName,packageId=PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),version=PlayerSettings.bundleVersion,
                versionCode=PlayerSettings.Android.bundleVersionCode,minimumAndroidApi=(int)PlayerSettings.Android.minSdkVersion,createdUtc=DateTime.UtcNow.ToString("O"),result=report.summary.result.ToString(),
                apkPath=report.summary.outputPath,apkSha256=File.Exists(output)?FileSha256(output):null,headset=pico?"PICO 4":"Quest",
                iconSource=ShowcaseIconPath,iconSha256=FileSha256(ShowcaseIconPath),
                errors=report.summary.totalErrors,warnings=report.summary.totalWarnings,development=development,
                requestedScenes=expected,reportedScenes=reported,packedScenes=packedScenes,rawSceneReferences=raw,
                unexpectedScenes=unexpected,missingScenes=missing,sceneWhitelistPassed=unexpected.Length==0 && missing.Length==0,
                sceneEvidence=reported.Length>0?"BuildReport.scenesUsingAssets plus packedAssets .unity audit":"BuildReport.packedAssets .unity source paths",
                packedAssets=packed,outputFiles=report.GetFiles().Select(f=>f.path).ToArray()
            };
            string path=Path.ChangeExtension(output,"manifest.json");
            File.WriteAllText(path,JsonUtility.ToJson(manifest,true));
            if(report.summary.result!=BuildResult.Succeeded)return;
            if(development || !manifest.sceneWhitelistPassed)
                throw new InvalidOperationException("Emberdeep build audit failed. Development="+development+" unexpected="+string.Join(",",unexpected)+" missing="+string.Join(",",missing)+". Review "+path);
            Debug.Log("EMBERDEEP_SHOWCASE_SCENE_AUDIT_OK manifest="+path);
        }
        static string FileSha256(string path)
        {
            using(var stream=File.OpenRead(path))using(var hash=SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        }
        [MenuItem("MetaHackthone/Fire Rescue/Create scene")]
        public static void CreateScene()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode first.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save open scenes first.");
            Directory.CreateDirectory("Assets/FireRescue/Scenes");Directory.CreateDirectory("Assets/FireRescue/Materials");AssetDatabase.Refresh();
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var game=new GameObject("Fire Rescue").AddComponent<FireRescueGame>();
                game.PlayerPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CloudGarden/Prefabs/MetaHackathonPlayer.prefab");
                if(!game.PlayerPrefab)throw new InvalidOperationException("Shared player prefab missing.");
                game.GroundMaterial=Material("Forest floor",new Color(.12f,.21f,.16f));
                game.FoliageMaterial=Material("Pine canopy",new Color(.18f,.36f,.23f));
                game.BarkMaterial=Material("Bark",new Color(.3f,.18f,.095f));
                game.PanelMaterial=Material("Station charcoal",new Color(.04f,.075f,.09f));
                game.FlameMaterial=Material("Flame amber",new Color(1,.5f,.03f),true);
                game.WaterMaterial=Material("Water blue",new Color(.16f,.78f,1),true);
                EditorSceneManager.SaveScene(scene,ScenePath);
                if(!EditorBuildSettings.scenes.Any(s=>s.path==ScenePath))EditorBuildSettings.scenes=EditorBuildSettings.scenes.Append(new EditorBuildSettingsScene(ScenePath,true)).ToArray();
                AssetDatabase.SaveAssets();Debug.Log("FIRE_RESCUE_SCENE_READY");
            }
            finally { if(setup.Any(s=>s.isLoaded && s.isActive))EditorSceneManager.RestoreSceneManagerSetup(setup);else EditorSceneManager.OpenScene(ScenePath); }
        }
        static Material Material(string name,Color color,bool unlit=false)
        {
            string path="Assets/FireRescue/Materials/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m)return m;
            var shader=Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit");
            if(!shader)throw new InvalidOperationException("Required URP shader missing.");
            m=new Material(shader){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.15f);AssetDatabase.CreateAsset(m,path);return m;
        }
        [MenuItem("MetaHackthone/Fire Rescue/Play")]
        public static void Play(){if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;if(!File.Exists(ScenePath))CreateScene();EditorSceneManager.OpenScene(ScenePath);EditorApplication.isPlaying=true;}
        [MenuItem("MetaHackthone/Fire Rescue/Build Windows preview")]
        public static void BuildWindows()
        {
            Directory.CreateDirectory("Builds/FireRescue");const string bindings="Builds/FireRescue/RuntimeActionBindings.json";if(File.Exists(bindings))File.Delete(bindings);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath}.Concat(EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path)).Distinct().ToArray(),target=BuildTarget.StandaloneWindows64,locationPathName="Builds/FireRescue/FireRescue.exe",options=BuildOptions.Development});
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded || report.summary.totalErrors>0)throw new InvalidOperationException("Fire build failed: "+report.summary.result);
            Debug.Log("FIRE_RESCUE_BUILD_OK errors="+report.summary.totalErrors);
        }
        [MenuItem("MetaHackthone/Fire Rescue/Build Quest preview")]
        public static void BuildQuest()
        {
            if(PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android)!=ScriptingImplementation.IL2CPP
                || PlayerSettings.Android.targetArchitectures!=AndroidArchitecture.ARM64)
                throw new InvalidOperationException("Quest requires the project's IL2CPP / ARM64 Android configuration.");
            Directory.CreateDirectory("Builds/FireRescueQuest");
            bool bundle=EditorUserBuildSettings.buildAppBundle;
            try
            {
                EditorUserBuildSettings.buildAppBundle=false;
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                    scenes=new[]{ScenePath}.Concat(EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path)).Distinct().ToArray(),
                    target=BuildTarget.Android,locationPathName="Builds/FireRescueQuest/FireRescue-Quest.apk",options=BuildOptions.Development});
                if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded || report.summary.totalErrors>0)
                    throw new InvalidOperationException("Quest fire build failed: "+report.summary.result);
                Debug.Log("FIRE_RESCUE_QUEST_BUILD_OK errors="+report.summary.totalErrors);
            }
            finally { EditorUserBuildSettings.buildAppBundle=bundle; }
        }
    }
}


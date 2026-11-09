using System;
using System.IO;
using System.Linq;
using Oculus.Interaction.OVR.Editor.QuickActions;
using PhysicalAssembly.MetaXR;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace PhysicalAssembly.Editor
{
    public static class ProjectBuilder
    {
        const string Root="Assets/MetaHackthone/Generated";
        const string ScenePath="Assets/MetaHackthone/Scenes/Workshop.unity";
        [MenuItem("MetaHackthone/Generate workshop and configure Quest")]
        public static void Setup()
        {
            BuildingSceneAuthoring.ProtectAuthoredScene(ScenePath);
            Directory.CreateDirectory(Root); Directory.CreateDirectory("Assets/MetaHackthone/Scenes"); AssetDatabase.Refresh();
            PlayerSettings.companyName="MetaHackathon2026"; PlayerSettings.productName="MetaHackthone";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.metahackathon2026.metahackthone");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion=AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false); PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.Vulkan});
            PlayerSettings.colorSpace=ColorSpace.Linear; PlayerSettings.runInBackground=true;
            PlayerSettings.Android.forceInternetPermission=false;
            var config=OVRProjectConfig.CachedProjectConfig; config.handTrackingSupport=OVRProjectConfig.HandTrackingSupport.HandsOnly;
            config.insightPassthroughSupport=OVRProjectConfig.FeatureSupport.Required; OVRProjectConfig.CommitProjectConfig(config);
            var runtime=OVRRuntimeSettings.GetRuntimeSettings(); runtime.HandSkeletonVersion=OVRHandSkeletonVersion.OpenXR; EditorUtility.SetDirty(runtime);
            ConfigureXR(BuildTargetGroup.Android); ConfigureXR(BuildTargetGroup.Standalone);
            ConfigureRendering();
            QualitySettings.vSyncCount=0;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var prefabs=Enum.GetValues(typeof(PartKind)).Cast<PartKind>().Select(CreatePrefab).ToArray();
            var root=new GameObject("MetaHackthone Workshop");
            var world=root.AddComponent<AssemblyWorld>(); var factory=root.AddComponent<PartFactory>(); factory.World=world; factory.Prefabs=prefabs;
            var session=root.AddComponent<WorkshopSession>(); session.World=world;
            var saves=root.AddComponent<AssemblySaveService>(); saves.World=world; saves.Factory=factory; saves.Session=session;
            var workshop=root.AddComponent<WorkshopBootstrap>(); workshop.World=world; workshop.Factory=factory; workshop.Session=session; workshop.Saves=saves;
            var lightGo=new GameObject("Workshop lighting"); var light=lightGo.AddComponent<Light>(); light.type=LightType.Directional; light.intensity=1.5f; light.shadows=LightShadows.None; lightGo.transform.rotation=Quaternion.Euler(55,-30,0);
            RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.65f,.7f,.8f);
            var desktop=new GameObject("Desktop preview camera"); var camera=desktop.AddComponent<Camera>(); camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.08f,.10f,.14f);
            desktop.transform.position=new Vector3(2.5f,2.8f,-1.8f); desktop.transform.LookAt(new Vector3(0,.8f,1.4f)); camera.nearClipPlane=.05f;
            var desktopInput=root.AddComponent<DesktopInputAdapter>(); desktopInput.Workshop=workshop; desktopInput.View=camera;
            OVRQuickActionsAPI.AddOVRInteractionRig(false);
            var rig=UnityEngine.Object.FindFirstObjectByType<OVRCameraRig>(); if(!rig) throw new InvalidOperationException("Meta rig creation failed.");
            MetaHandInputAdapter.ConfigureRoomScaleRig(rig.transform);
            var manager=rig.GetComponent<OVRManager>(); manager.isInsightPassthroughEnabled=true; manager.trackingOriginType=OVRManager.TrackingOrigin.Stage;
            rig.gameObject.AddComponent<OVRPassthroughLayer>(); // v207 creates its underlay internally.
            foreach(var c in rig.GetComponentsInChildren<Camera>(true)) { c.clearFlags=CameraClearFlags.SolidColor; c.backgroundColor=Color.clear; c.nearClipPlane=.03f; }
            var bridge=root.AddComponent<MetaHandInputAdapter>(); bridge.Workshop=workshop; bridge.XRRig=rig.transform; bridge.DesktopCamera=camera;
            BuildingSceneAuthoring.ConvertScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            rig.gameObject.SetActive(false); desktop.SetActive(false);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)}.Concat(EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath)).ToArray();
            MainMenuBuilder.EnsureFirstScene();
            AssetDatabase.SaveAssets(); Debug.Log("METAHACKTHONE_SETUP_OK");
        }
        static void ConfigureXR(BuildTargetGroup group)
        {
            var settings=XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(group);
            if(settings==null)
            {
                var perTarget=ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                // The management editor creates the shared settings asset on demand through its public API.
                var existing=AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget").FirstOrDefault();
                if(existing!=null) { UnityEngine.Object.DestroyImmediate(perTarget); perTarget=AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(AssetDatabase.GUIDToAssetPath(existing)); }
                else { AssetDatabase.CreateAsset(perTarget,Root+"/XRGeneralSettings.asset"); EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey,perTarget,true); }
                settings=ScriptableObject.CreateInstance<XRGeneralSettings>(); settings.name=group+" XR settings"; AssetDatabase.AddObjectToAsset(settings,perTarget); perTarget.SetSettingsForBuildTarget(group,settings);
                var manager=ScriptableObject.CreateInstance<XRManagerSettings>(); manager.name=group+" XR manager"; AssetDatabase.AddObjectToAsset(manager,perTarget); settings.Manager=manager;
            }
            if(settings.Manager==null) { var m=ScriptableObject.CreateInstance<XRManagerSettings>(); AssetDatabase.AddObjectToAsset(m,settings); settings.Manager=m; }
            XRPackageMetadataStore.AssignLoader(settings.Manager,"UnityEngine.XR.OpenXR.OpenXRLoader",group);
            settings.InitManagerOnStart=group==BuildTargetGroup.Android;
            OpenXRFeatureSetManager.InitializeFeatureSets();
            var sets=OpenXRFeatureSetManager.FeatureSetsForBuildTarget(group);
            foreach(var set in sets) if(set.featureSetId.Contains("meta")) set.isEnabled=true;
            OpenXRFeatureSetManager.SetFeaturesFromEnabledFeatureSets(group);
            var openxr=OpenXRSettings.GetSettingsForBuildTargetGroup(group); openxr.renderMode=OpenXRSettings.RenderMode.SinglePassInstanced; EditorUtility.SetDirty(openxr); EditorUtility.SetDirty(settings);
        }
        [MenuItem("MetaHackthone/Rebuild part prefabs")]
        public static void RebuildPartPrefabs()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before rebuilding parts.");
            foreach(PartKind kind in Enum.GetValues(typeof(PartKind))) CreatePrefab(kind);
            AssetDatabase.SaveAssets();
        }
        [MenuItem("MetaHackthone/Rebuild cube and cuboid prefabs")]
        public static void RebuildBuildingBlockPrefabs()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before rebuilding parts.");
            CreatePrefab(PartKind.SmallCube); CreatePrefab(PartKind.Cuboid);
            AssetDatabase.SaveAssets();
        }
        [MenuItem("MetaHackthone/Rebuild shared building prefabs")]
        public static void RebuildSharedBuildingPrefabs()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before rebuilding parts.");
            foreach(var kind in new[]{PartKind.Chassis,PartKind.Beam,PartKind.Block,PartKind.SmallCube,PartKind.Cuboid,PartKind.Pyramid}) CreatePrefab(kind);
            AssetDatabase.SaveAssets();
        }
        static T Persist<T>(T asset,string path) where T:UnityEngine.Object
        {
            var existing=AssetDatabase.LoadAssetAtPath<T>(path);
            if(!existing) { AssetDatabase.CreateAsset(asset,path); return asset; }
            EditorUtility.CopySerialized(asset,existing); EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(asset); return existing;
        }
        static AssemblyPart CreatePrefab(PartKind kind)
        {
            string path=Root+"/"+kind+".prefab";
            var part=PartFactory.BuildDefinition(kind); int i=0;
            foreach(var renderer in part.GetComponentsInChildren<Renderer>())
            {
                var materials=renderer.sharedMaterials;
                for(int n=0;n<materials.Length;n++) if(materials[n] && !AssetDatabase.Contains(materials[n])) materials[n]=Persist(materials[n],$"{Root}/{kind}_Material_{i++}.mat");
                renderer.sharedMaterials=materials;
            }
            foreach(var filter in part.GetComponentsInChildren<MeshFilter>()) if(filter.sharedMesh && !AssetDatabase.Contains(filter.sharedMesh))
            {
                var mesh=Persist(filter.sharedMesh,$"{Root}/{kind}_Mesh_{i++}.asset"); filter.sharedMesh=mesh;
                var meshCollider=filter.GetComponent<MeshCollider>(); if(meshCollider) meshCollider.sharedMesh=mesh;
            }
            foreach(var collider in part.GetComponentsInChildren<Collider>()) if(collider.sharedMaterial && !AssetDatabase.Contains(collider.sharedMaterial)) collider.sharedMaterial=Persist(collider.sharedMaterial,$"{Root}/{kind}_Physics_{i++}.asset");
            var prefab=PrefabUtility.SaveAsPrefabAsset(part.gameObject,path); UnityEngine.Object.DestroyImmediate(part.gameObject); return prefab.GetComponent<AssemblyPart>();
        }
        static void ConfigureRendering()
        {
            foreach(var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset",new[]{"Assets"}))
            {
                var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                pipeline.msaaSampleCount=4; pipeline.renderScale=1; pipeline.supportsHDR=false;
                pipeline.supportsCameraDepthTexture=false; pipeline.supportsCameraOpaqueTexture=false; pipeline.shadowDistance=0;
                EditorUtility.SetDirty(pipeline);
            }
            Directory.CreateDirectory("Assets/MetaHackthone/Resources"); AssetDatabase.Refresh();
            const string materialPath="Assets/MetaHackthone/Resources/AssemblyPreview.mat";
            if(!AssetDatabase.LoadAssetAtPath<Material>(materialPath))
            {
                var material=new Material(Shader.Find("Universal Render Pipeline/Unlit")); material.color=new Color(.2f,1,.65f);
                AssetDatabase.CreateAsset(material,materialPath);
            }
            AssetDatabase.SaveAssets();
        }
        [MenuItem("MetaHackthone/Build Quest APK")]
        public static void BuildQuest()
        {
            BuildQuestPlayer(BuildOptions.None);
        }
        [MenuItem("MetaHackthone/Build Quest APK (clean)")]
        public static void BuildQuestClean()
        {
            BuildQuestPlayer(BuildOptions.CleanBuildCache);
        }
        static void BuildQuestPlayer(BuildOptions extraOptions)
        {
            AndroidBuildValidation.ValidateTarget(BuildTarget.Android,EditorUserBuildSettings.activeBuildTarget);
            Directory.CreateDirectory("Builds");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),locationPathName="Builds/MetaHackthone.apk",target=BuildTarget.Android,options=BuildOptions.Development|BuildOptions.CompressWithLz4|BuildOptions.DetailedBuildReport|extraOptions });
            if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Quest build failed: "+report.summary.result);
            Debug.Log("METAHACKTHONE_BUILD_OK "+report.summary.totalSize);
        }
    }
}

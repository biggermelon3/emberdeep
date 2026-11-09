using System;
using System.IO;
using System.Linq;
using System.Xml;
using CloudGarden.Integration;
using Oculus.Interaction.Editor.UnityXR.QuickActions;
using PhysicalAssembly.MetaXR;
using Unity.XR.CoreUtils;
using Unity.XR.OpenXR.Features.PICOSupport;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace PhysicalAssembly.Editor
{
    public sealed class PicoBuild : IProcessSceneWithReport, IPreprocessBuildWithReport
    {
        const string PlayerPath="Assets/MetaHackthone/Generated/PicoPlayer.prefab";
        const string Manifest="Assets/Plugins/Android/AndroidManifest.xml";
        const string Backup="Library/MetaHackthonePicoBackup";
        [Serializable] public class FeatureState { public string type; public bool enabled; }
        [Serializable] public class Snapshot { public FeatureState[] features; public string projectConfigJson; }
        public int callbackOrder=>100;

        public void OnPreprocessBuild(BuildReport report)
        {
            if(report.summary.platform!=BuildTarget.Android) return;
            bool picoOutput=report.summary.outputPath.EndsWith("-Pico.apk",StringComparison.OrdinalIgnoreCase);
            var pico=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android).GetFeature<PICOFeature>();
            if(picoOutput!=(pico!=null && pico.enabled))
                throw new BuildFailedException(picoOutput?"Run Pico/Prepare build first.":"Restore Quest configuration before building a Quest APK.");
        }

        [MenuItem("MetaHackthone/Pico/Prepare build")]
        public static void Prepare()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Wait until the editor is idle.");
            var settings=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            var features=settings.GetFeatures<OpenXRFeature>();
            if(!features.Any(f=>f is PICOFeature)) throw new InvalidOperationException("PICO OpenXR package has not registered its features yet.");
            if(Directory.Exists(Backup)) throw new InvalidOperationException("Restore the previous Quest configuration before preparing again.");
            Directory.CreateDirectory(Backup);
            try
            {
                File.Copy(Manifest,Backup+"/AndroidManifest.xml");
                File.WriteAllText(Backup+"/features.json",JsonUtility.ToJson(new Snapshot { features=features.Select(f=>new FeatureState { type=f.GetType().FullName,enabled=f.enabled }).ToArray(),projectConfigJson=EditorJsonUtility.ToJson(PICOProjectSetting.GetProjectConfig()) },true));
            }
            catch
            {
                // No configuration has changed yet. Remove only our incomplete
                // backup files so a failed copy does not prevent the next build.
                if(File.Exists(Backup+"/features.json"))File.Delete(Backup+"/features.json");
                if(File.Exists(Backup+"/AndroidManifest.xml"))File.Delete(Backup+"/AndroidManifest.xml");
                Directory.Delete(Backup);throw;
            }
            try
            {
                BuildPlayerPrefab();
                foreach(var feature in features)
                {
                    string type=feature.GetType().Name;
                    feature.enabled=type=="PICOFeature" || type=="PICO4ControllerProfile" || type=="PICO4UltraControllerProfile" ||
                        type=="HandTracking" || type=="MetaHandTrackingAim" || type=="PassthroughFeature" || type=="OpenXRExtensions";
                    EditorUtility.SetDirty(feature);
                }
                var config=PICOProjectSetting.GetProjectConfig();
                config.isHandTracking=true; config.highFrequencyHand=true;
                config.handTrackingSupportType=Unity.XR.PXR.HandTrackingSupport.HandsOnly;
                EditorUtility.SetDirty(config);
                var xml=new XmlDocument(); xml.Load(Manifest);
                var nodes=xml.SelectNodes("//*").Cast<XmlNode>().ToArray();
                foreach(var node in nodes.Reverse())
                {
                    var name=node.Attributes?["name","http://schemas.android.com/apk/res/android"]?.Value??"";
                    if(name.StartsWith("com.oculus.") || name.StartsWith("oculus.") || node.Prefix=="horizonos")
                        node.ParentNode.RemoveChild(node);
                }
                xml.Save(Manifest);
                AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
                Debug.Log("PICO_BUILD_PREPARED: build to Builds/MetaHackthone-Pico.apk, then Restore Quest configuration.");
            }
            catch { Restore(); throw; }
        }

        [MenuItem("MetaHackthone/Pico/Restore Quest configuration")]
        public static void Restore()
        {
            if(BuildPipeline.isBuildingPlayer) throw new InvalidOperationException("Wait for the build to finish.");
            if(!Directory.Exists(Backup)) return;
            var snapshot=JsonUtility.FromJson<Snapshot>(File.ReadAllText(Backup+"/features.json"));
            var settings=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            foreach(var feature in settings.GetFeatures<OpenXRFeature>())
            {
                var state=snapshot.features.FirstOrDefault(s=>s.type==feature.GetType().FullName);
                feature.enabled=state!=null && state.enabled; EditorUtility.SetDirty(feature);
            }
            File.Copy(Backup+"/AndroidManifest.xml",Manifest,true);
            if(!string.IsNullOrEmpty(snapshot.projectConfigJson))
            {
                var config=PICOProjectSetting.GetProjectConfig();
                EditorJsonUtility.FromJsonOverwrite(snapshot.projectConfigJson,config);EditorUtility.SetDirty(config);
            }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            File.Delete(Backup+"/features.json"); File.Delete(Backup+"/AndroidManifest.xml"); Directory.Delete(Backup);
            Debug.Log("QUEST_CONFIGURATION_RESTORED");
        }

        static void BuildPlayerPrefab()
        {
            // The SDK quick action searches globally for an existing XR Origin.
            // Refuse to let it mutate a user's loaded rig while generating ours.
            if(UnityEngine.Object.FindFirstObjectByType<XROrigin>())
                throw new InvalidOperationException("Close scenes containing an active XR Origin before generating the PICO player.");
            var previous=SceneManager.GetActiveScene();
            bool emptyStartup=SceneManager.sceneCount==1 && string.IsNullOrEmpty(previous.path) && !previous.isDirty && previous.rootCount==0;
            if(!emptyStartup && Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Any(s=>string.IsNullOrEmpty(s.path)))
                throw new InvalidOperationException("Save untitled scenes before generating the PICO player.");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,emptyStartup?NewSceneMode.Single:NewSceneMode.Additive);
            var oldSelection=Selection.activeObject;
            try
            {
                SceneManager.SetActiveScene(scene);
                UnityXRQuickActionsAPI.AddUnityXRInteractionRig(false);
                var origin=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<XROrigin>(true)).Single();
                var root=new GameObject("Pico Player"); origin.transform.SetParent(root.transform,false);
                origin.RequestedTrackingOriginMode=XROrigin.TrackingOriginMode.Floor;
                MetaHandInputAdapter.ConfigureRoomScaleRig(origin.transform);
                origin.Camera.clearFlags=CameraClearFlags.SolidColor; origin.Camera.backgroundColor=Color.clear; origin.Camera.nearClipPlane=.03f;
                origin.gameObject.AddComponent<PicoPassthrough>();
                var desktop=new GameObject("Desktop preview camera"); desktop.transform.SetParent(root.transform,false);
                var camera=desktop.AddComponent<Camera>(); camera.enabled=true;
                var player=origin.gameObject.AddComponent<MetaPlayerRig>(); player.XRRig=origin.transform; player.DesktopCamera=camera;
                PrefabUtility.SaveAsPrefabAsset(root,PlayerPath);
            }
            finally
            {
                if(emptyStartup)EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                else { EditorSceneManager.CloseScene(scene,true); SceneManager.SetActiveScene(previous); }
                Selection.activeObject=oldSelection;
            }
        }

        public void OnProcessScene(Scene scene,BuildReport report)
        {
            if(report==null || !report.summary.outputPath.EndsWith("-Pico.apk",StringComparison.OrdinalIgnoreCase)) return;
            if(!OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android).GetFeature<PICOFeature>().enabled)
                throw new BuildFailedException("Run Pico/Prepare build first.");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            if(!prefab) throw new BuildFailedException("Pico player prefab is missing.");
            foreach(var root in scene.GetRootGameObjects())
            {
                if(!root) continue; // A workshop adapter may have replaced another root in this snapshot.
                foreach(var menu in root.GetComponentsInChildren<MainMenuBootstrap>(true)) menu.PlayerPrefab=prefab;
                foreach(var entry in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if(!entry) continue;
                    var serialized=new SerializedObject(entry); var standalone=serialized.FindProperty("StandalonePlayer");
                    if(standalone!=null) { standalone.objectReferenceValue=prefab; serialized.ApplyModifiedPropertiesWithoutUndo(); }
                    if(entry.GetType().FullName=="FireRescue.EmberdeepCamp" || entry.GetType().FullName=="FireRescue.FireRescueGame")
                    {
                        var playerField=serialized.FindProperty("PlayerPrefab");
                        if(playerField==null)throw new BuildFailedException("Fire scene is missing its player prefab field: "+scene.path);
                        playerField.objectReferenceValue=prefab;serialized.ApplyModifiedPropertiesWithoutUndo();
                        var portable=prefab.GetComponentInChildren<MetaPlayerRig>(true);
                        if(!portable || !portable.XRRig.GetComponent<XROrigin>() || portable.XRRig.GetComponent<OVRCameraRig>())
                            throw new BuildFailedException("Fire scene requires the portable PICO UnityXR player: "+scene.path);
                        Debug.Log("EMBERDEEP_PICO_PLAYER_OK scene="+scene.path+" prefab="+AssetDatabase.GetAssetPath(playerField.objectReferenceValue));
                    }
                }
                foreach(var entry in root.GetComponentsInChildren<NarrowGapEntry>(true)) entry.StandalonePlayer=prefab;
                foreach(var entry in root.GetComponentsInChildren<CloudGardenDemo>(true)) entry.StandalonePlayer=prefab;
                foreach(var input in root.GetComponentsInChildren<MetaHandInputAdapter>(true))
                {
                    var portable=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
                    var player=portable.GetComponentInChildren<MetaPlayerRig>(true);
                    var oldRig=input.XRRig; var oldCamera=input.DesktopCamera;
                    player.XRRig.position=oldRig.position; player.XRRig.rotation=oldRig.rotation;
                    input.XRRig=player.XRRig; input.DesktopCamera=player.DesktopCamera;
                    player.XRRig.gameObject.SetActive(false); player.DesktopCamera.gameObject.SetActive(false);
                    var desktop=input.GetComponent<DesktopInputAdapter>(); if(desktop) desktop.View=player.DesktopCamera;
                    UnityEngine.Object.DestroyImmediate(oldRig.gameObject);
                    if(oldCamera) UnityEngine.Object.DestroyImmediate(oldCamera.gameObject);
                }
            }
        }
    }
}

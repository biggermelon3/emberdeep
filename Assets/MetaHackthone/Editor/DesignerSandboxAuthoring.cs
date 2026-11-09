using System.Linq;
using CloudGarden.Integration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PhysicalAssembly.Editor
{
    public static class DesignerSandboxAuthoring
    {
        [MenuItem("MetaHackthone/Downhill/Upgrade designer drag builder")]
        public static void Upgrade()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Stop Play mode before authoring.");
            var scene=SceneManager.GetSceneByPath(DownhillBuilder.ScenePath);
            if(!scene.IsValid() || !scene.isLoaded) scene=EditorSceneManager.OpenScene(DownhillBuilder.ScenePath,OpenSceneMode.Additive);
            var entry=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DownhillEntry>(true)).Single();
            var layout=entry.Layout; var settings=entry.Settings;
            if(settings.DesignerBuilder && layout.Spawns.Length==9 && layout.Spawns[0].Kind==PartKind.DownhillCore && layout.Spawns.All(s=>s && s.Preview)) {
                var existing=layout.Downhill.GetComponent<DownhillPresentation>();
                if(existing.Interface.DesignerControlsVersion>=1) { Debug.Log("DESIGNER_SANDBOX_ALREADY_AUTHORED"); return; }
                existing.Initialize(layout.Downhill,settings,layout); existing.Configure();
                BuildingSceneAuthoring.PersistResources(layout,"DesignerControls");
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); return;
            }
            Undo.RegisterFullObjectHierarchyUndo(layout.gameObject,"Designer sandbox kit");
            var old=layout.Spawns; var pose=old.Length>0 && old[0]?old[0].Pose:new Pose(layout.Downhill.WorkbenchFrame.TransformPoint(settings.CorePosition),layout.Downhill.WorkbenchFrame.rotation);
            float scale=old.Length>0 && old[0]?old[0].SizeMultiplier:.4f;
            var parent=new GameObject("Designer part spawn markers").transform; parent.SetParent(layout.Downhill.WorkbenchFrame,false);
            layout.Spawns=new BuildingPartSpawn[9];
            for(int i=0;i<9;i++) {
                var go=new GameObject($"{i:00} {DownhillParts.DesignerKit[i]} spawn"); go.transform.SetParent(parent,false);
                go.transform.SetPositionAndRotation(pose.position+pose.rotation*DownhillController.DesignerOffset(i),pose.rotation);
                go.transform.localScale=Vector3.one*scale; var marker=go.AddComponent<BuildingPartSpawn>(); marker.Kind=DownhillParts.DesignerKit[i]; layout.Spawns[i]=marker;
            }
            // Keep old marker transforms as an inactive authoring reference; gameplay uses the new explicit kit.
            foreach(var marker in old) if(marker) marker.gameObject.SetActive(false);
            settings.DesignerBuilder=true; settings.UseGravityGarage=true;
            var presentation=layout.Downhill.GetComponent<DownhillPresentation>(); presentation.Initialize(layout.Downhill,settings,layout); presentation.Configure();
            BuildingSceneAuthoring.CreatePreviews(layout,settings);
            BuildingSceneAuthoring.PersistResources(layout,"DesignerSandbox");
            EditorUtility.SetDirty(settings); EditorUtility.SetDirty(layout); AssetDatabase.SaveAssetIfDirty(settings);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("DESIGNER_SANDBOX_AUTHORED: nine parts and rotation/example controls");
        }
    }
}

using System.Linq;
using System.IO;
using CloudGarden.Integration;
using PhysicalAssembly;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PhysicalAssembly.Editor
{
    public static class SandboxExperienceAuthoring
    {
        [MenuItem("MetaHackthone/Downhill/Upgrade fixed sandbox interface")]
        public static void UpgradeFixedInterface()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Stop Play mode before upgrading the interface.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(DownhillBuilder.ScenePath);
            if(!scene.IsValid() || !scene.isLoaded) scene=EditorSceneManager.OpenScene(DownhillBuilder.ScenePath,OpenSceneMode.Additive);
            var entry=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DownhillEntry>(true)).Single();
            var layout=entry.Layout; var course=layout.Downhill;
            var presentation=course.GetComponent<DownhillPresentation>();
            var existing=course.GetComponent<SandboxInterface>();
            if(existing && existing.IsConfigured && existing.CoursePreview && layout.Spawns.All(s=>!s || s.Preview))
            { Debug.Log("SANDBOX_FIXED_UI_ALREADY_AUTHORED"); return; }
            Undo.RegisterFullObjectHierarchyUndo(layout.gameObject,"Upgrade fixed sandbox interface");
            presentation.Initialize(course,entry.Settings,layout);
            presentation.Configure();
            // Empty optional slots are authored omissions, not missing resources.
            BuildingSceneAuthoring.CreatePreviews(layout,entry.Settings,missingOnly:true);
            CaptureCourse(presentation.Interface,course);
            BuildingSceneAuthoring.PersistResources(layout,"SandboxInterface");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("SANDBOX_FIXED_UI_AUTHORED");
        }
        public static void CaptureCourse(SandboxInterface ui,DownhillCourse course)
        {
            const string path="Assets/MetaHackthone/Art/UI/SandboxCoursePreview.png";
            var renderers=Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            var states=renderers.Select(r=>r.enabled).ToArray();
            var scenery=course.SandboxFrame.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();
            var bounds=scenery[0].bounds; foreach(var renderer in scenery) bounds.Encapsulate(renderer.bounds);
            var cameraObject=new GameObject("Temporary course thumbnail camera");
            var camera=cameraObject.AddComponent<Camera>(); camera.enabled=false;
            camera.backgroundColor=new Color(.025f,.065f,.12f); camera.clearFlags=CameraClearFlags.SolidColor;
            camera.orthographic=true; camera.orthographicSize=Mathf.Max(bounds.size.y*.75f,bounds.size.x/4)*1.15f;
            camera.transform.position=bounds.center+course.WorkbenchFrame.rotation*new Vector3(0,1.5f,-3);
            camera.transform.LookAt(bounds.center,course.WorkbenchFrame.up); camera.nearClipPlane=.01f; camera.farClipPlane=20;
            var target=new RenderTexture(768,384,24,RenderTextureFormat.ARGB32); target.Create();
            var previous=RenderTexture.active; Texture2D image=null;
            try
            {
                foreach(var renderer in renderers) if(!renderer.transform.IsChildOf(course.SandboxFrame)) renderer.enabled=false;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination=target });
                RenderTexture.active=target;
                image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,target.width,target.height),0,0); image.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path,image.EncodeToPNG());
                AssetDatabase.ImportAsset(path);
                ui.CoursePreview=AssetDatabase.LoadAssetAtPath<Texture2D>(path); ui.RefreshCourseCard();
            }
            finally
            {
                for(int i=0;i<renderers.Length;i++) if(renderers[i]) renderers[i].enabled=states[i];
                RenderTexture.active=previous; target.Release(); Object.DestroyImmediate(target);
                if(image) Object.DestroyImmediate(image); Object.DestroyImmediate(cameraObject);
            }
        }
        [MenuItem("MetaHackthone/Downhill/Upgrade floating Start")]
        public static void UpgradeStart()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene=EditorSceneManager.OpenScene(DownhillBuilder.ScenePath,OpenSceneMode.Additive);
                var entry=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DownhillEntry>(true)).Single();
                entry.Layout.Downhill.GetComponent<DownhillPresentation>().ConfigureStartButton(entry.Settings);
                BuildingSceneAuthoring.PersistResources(entry.Layout,"FloatingStart");
                EditorUtility.SetDirty(entry.Settings); AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }
        [MenuItem("MetaHackthone/Downhill/Upgrade build experiences")]
        public static void Upgrade()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene=EditorSceneManager.OpenScene(DownhillBuilder.ScenePath,OpenSceneMode.Additive);
                var entry=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DownhillEntry>(true)).Single();
                var layout=entry.Layout; var course=layout.Downhill;
                course.EnsureControls(entry.Settings);
                var presentation=course.GetComponent<DownhillPresentation>();
                if(!presentation) presentation=course.gameObject.AddComponent<DownhillPresentation>();
                presentation.Initialize(course,entry.Settings,layout); presentation.ResetSpawnMarkers(); presentation.ArrangeBuildMenu(); presentation.Configure();
                BuildingSceneAuthoring.CreatePreviews(layout,entry.Settings);
                BuildingSceneAuthoring.PersistResources(layout,"DownhillSandbox");
                EditorUtility.SetDirty(entry.Settings); AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                Debug.Log("SANDBOX_EXPERIENCES_AUTHORED");
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }
    }
}

using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CloudGarden.Integration;
namespace PhysicalAssembly.Editor
{
    public static class GravityGarageAuthoring
    {
        [MenuItem("MetaHackthone/Downhill/Replace saved course with Gravity Garage")]
        public static void Upgrade()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Exit Play mode first.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!=DownhillBuilder.ScenePath) scene=EditorSceneManager.OpenScene(DownhillBuilder.ScenePath);
            var entry=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DownhillEntry>(true)).Single();
            var layout=entry.Layout;var course=layout.Downhill;
            Undo.RegisterFullObjectHierarchyUndo(layout.gameObject,"Replace sandbox course");
            entry.Settings.UseGravityGarage=true;EditorUtility.SetDirty(entry.Settings);
            var garage=course.GetComponent<GravityGarageCourse>();if(!garage) garage=Undo.AddComponent<GravityGarageCourse>(course.gameObject);
            garage.Rebuild(course,entry.Settings.Garage,GravityGarageDesign.UnitScale*DownhillSettings.VehicleScale*entry.Settings.PresentationScale);
            var ui=course.GetComponent<SandboxInterface>(); if(ui) {ui.CourseTitle="Gravity Garage";SandboxExperienceAuthoring.CaptureCourse(ui,course);}
            BuildingSceneAuthoring.PersistResources(layout,"GravityGarage");garage.ReleaseResourceOwnership();
            EditorUtility.SetDirty(garage);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
        }
    }
}

using System;
using System.Linq;
using CloudGarden.Integration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PhysicalAssembly.Editor
{
    public static class DownhillControlsUpgrade
    {
        [MenuItem("MetaHackthone/Downhill/Circular grid and wheel beneath")]
        public static void CircularGrid()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            var scene=SceneManager.GetActiveScene();
            var entry=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DownhillEntry>(true)).Single();
            var layout=entry.Layout; var presentation=layout.Downhill.GetComponent<DownhillPresentation>();
            var pad=presentation.FindGridElement("Centered grid pad");
            if(pad.GetComponent<MeshFilter>().sharedMesh.name=="CenteredGridPad") return;
            bool needsMesh=pad.GetComponent<MeshFilter>().sharedMesh.name!="Circular build pad";
            if(!needsMesh && Vector3.Distance(layout.Downhill.RotationWheel.transform.position,pad.position-layout.Downhill.WorkbenchFrame.up*.041f)<.00001f) return;
            Undo.RegisterFullObjectHierarchyUndo(layout.gameObject,"Circular grid and wheel beneath");
            presentation.ConfigureCircularBuildPad();
            if(needsMesh) BuildingSceneAuthoring.PersistResources(layout,"CircularSandboxPad");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        [MenuItem("MetaHackthone/Scene editing/Refresh Downhill two-sided wheels")]
        public static void RefreshWheelPreviews()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            var scene=SceneManager.GetActiveScene();
            var entry=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DownhillEntry>(true)).SingleOrDefault();
            if(!entry || !entry.Layout || !entry.Layout.Downhill) throw new InvalidOperationException("Open the authored Downhill Sandbox scene.");
            var layout=entry.Layout;
            bool changed=false;
            if(layout.Spawns.Where(s=>s && DownhillParts.IsWheel(s.Kind)).Any(s=>!s.Preview || s.Preview.GetComponentsInChildren<Transform>(true).Count(t=>t.name=="Wheel rotation mark")!=16))
            {
                BuildingSceneAuthoring.CreatePreviews(layout,entry.Settings,true);
                BuildingSceneAuthoring.PersistResources(layout,"DownhillTwoSidedWheels"); changed=true;
            }
            foreach(var label in layout.GetComponentsInChildren<TextMesh>(true))
                if((label.text.StartsWith("BUILD AND TEST") || label.name=="Building instructions") && label.text!=DownhillController.BuildInstructions)
                { label.name="Building instructions"; label.text=DownhillController.BuildInstructions; changed=true; }
            if(changed) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
        }
        [MenuItem("MetaHackthone/Scene editing/Upgrade Downhill table controls")]
        public static void UpgradeActiveScene()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            var scene=SceneManager.GetActiveScene();
            var entry=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DownhillEntry>(true)).SingleOrDefault();
            if(!entry || !entry.Layout || !entry.Layout.Downhill) throw new InvalidOperationException("Open the authored Downhill Sandbox scene.");
            var layout=entry.Layout; var course=layout.Downhill;
            bool needsControls=!course.ModeButton || !course.RotationWheel;
            var legacy=course.WorkbenchFrame.Cast<Transform>().Where(t=>t.name=="Transfer plate (move this group)" || t.name=="Transfer frame" ||
                t.name.StartsWith("Transfer support") || t.name.StartsWith("Full size transfer") || t.name.StartsWith("Plate ") ||
                t.name.StartsWith("Forward arrow") || t.name.StartsWith("TRANSFER")).ToArray();
            if(!needsControls && legacy.Length==0) return;
            foreach(var old in legacy) UnityEngine.Object.DestroyImmediate(old.gameObject);
            course.EnsureControls(entry.Settings);
            var core=layout.Spawns[0].transform;
            var position=course.RotationWheel.transform.position;
            var offset=Vector3.ProjectOnPlane(core.position-position,course.WorkbenchFrame.up);
            course.RotationWheel.transform.position+=offset;
            foreach(var label in layout.GetComponentsInChildren<TextMesh>(true))
                if(label.text.StartsWith("MODE GESTURE"))
                {
                    label.name="Building instructions";
                    label.text=DownhillController.BuildInstructions;
                }
            course.StatusLabel.text="BUILD • TURN WHEEL • PRESS START";
            BuildingSceneAuthoring.PersistResources(layout,"DownhillControls");
            EditorUtility.SetDirty(course); EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}

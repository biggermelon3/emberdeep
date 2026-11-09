using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PhysicalAssembly.Editor
{
    public static class SandboxPolishAuthoring
    {
        [MenuItem("MetaHackthone/Sandbox/Configure palm menu and building boundary")]
        public static void ConfigureActive()
        {
            var course=Object.FindFirstObjectByType<DownhillCourse>();
            if(!course || course.gameObject.scene.name!="DownhillSandbox") throw new System.InvalidOperationException("Open DownhillSandbox first.");
            Undo.RecordObject(course,"Configure sandbox polish"); course.SandboxPolish=true;
            if(!course.BuildArea) {
                var go=new GameObject("Sandbox building boundary"); Undo.RegisterCreatedObjectUndo(go,"Create boundary");
                var frame=course.WorkbenchFrame; go.transform.SetParent(frame,false);
                course.BuildArea=go.AddComponent<BuildingArea>();
                var layout=Object.FindFirstObjectByType<BuildingSceneLayout>();
                var bounds=new Bounds(frame.InverseTransformPoint(layout.Spawns[0].transform.position),Vector3.zero);
                void Include(Renderer renderer) {
                    var b=renderer.bounds;
                    for(int i=0;i<8;i++) bounds.Encapsulate(frame.InverseTransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
                }
                foreach(var spawn in layout.Spawns) { bounds.Encapsulate(frame.InverseTransformPoint(spawn.transform.position)); foreach(var r in spawn.GetComponentsInChildren<Renderer>(true)) Include(r); }
                var pad=course.GetComponent<DownhillPresentation>().FindGridElement("Centered grid pad");
                if(pad && pad.TryGetComponent<Renderer>(out var padRenderer)) Include(padRenderer);
                bounds.Expand(.3f/Mathf.Max(.01f,frame.lossyScale.x));
                go.transform.localPosition=bounds.center; course.BuildArea.Size=Vector3.one*Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
            }
            foreach(var path in new[]{"Assets/Settings/PC_Renderer.asset","Assets/Settings/Mobile_Renderer.asset"}) {
                var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if(renderer.rendererFeatures.Exists(f=>f is SandboxOutlineFeature)) continue;
                var feature=ScriptableObject.CreateInstance<SandboxOutlineFeature>(); feature.name="Sandbox stencil outlines";
                AssetDatabase.AddObjectToAsset(feature,renderer); renderer.rendererFeatures.Add(feature); renderer.SetDirty(); EditorUtility.SetDirty(renderer);
            }
            EditorUtility.SetDirty(course); EditorSceneManager.MarkSceneDirty(course.gameObject.scene);
            EditorSceneManager.SaveScene(course.gameObject.scene); AssetDatabase.SaveAssets();
        }
    }
}

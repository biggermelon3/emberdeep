using UnityEditor;
using UnityEngine;

namespace PhysicalAssembly.Editor
{
    [CustomEditor(typeof(BuildingSceneLayout))]
    public sealed class BuildingSceneLayoutEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var owner=(BuildingSceneLayout)target;
            EditorGUILayout.HelpBox("Edit outside Play mode, then save the scene. Move whole part spawn markers, not their mesh children. Sandbox markers have a Part Size control: it resizes the complete preview and the playable part, including connection points and colliders.",MessageType.Info);
            if(!owner.NarrowGap)
            {
                EditorGUILayout.LabelField("Design Mode / no headset",EditorStyles.boldLabel);
                using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if(GUILayout.Button(owner.Downhill?"Test in Design Mode - Workbench":"Test in Design Mode")) DesignModeEditor.Play(owner,owner.GetComponent<AuthoredSandboxLayout>()?owner.GetComponent<AuthoredSandboxLayout>().EditorLevel:DesignLevelId.GravityGarage);
                    if(!owner.Downhill && !owner.DesignCarSpawn && GUILayout.Button("Add editable test-car spawn")) DesignModeEditor.CreateCarSpawn(owner);
                }
                if(owner.DesignCarSpawn && GUILayout.Button("Select / frame test-car spawn")) DesignModeEditor.Frame(owner.DesignCarSpawn);
                if(owner.Downhill)
                {
                    var presentation=owner.Downhill.GetComponent<DownhillPresentation>();
                    if(presentation && GUILayout.Button("Select / frame table (scale this group)")) DesignModeEditor.Frame(presentation.WorkbenchVisuals);
                    if(GUILayout.Button("Select / frame course")) DesignModeEditor.Frame(owner.Downhill.SandboxFrame);
                    if(GUILayout.Button("Select / frame vehicle spawn")) DesignModeEditor.Frame(owner.Spawns!=null && owner.Spawns.Length>0 && owner.Spawns[0]?owner.Spawns[0].transform:null);
                    if(GUILayout.Button("Select / frame simulation start")) DesignModeEditor.Frame(owner.Downhill.StartPoint);
                    if(GUILayout.Button("Select / frame finish region")) DesignModeEditor.Frame(owner.Downhill.FinishArea?owner.Downhill.FinishArea.transform:null);
                    if(GUILayout.Button("Select / frame safe region")) DesignModeEditor.Frame(owner.Downhill.PlayArea?owner.Downhill.PlayArea.transform:null);
                }
            }
            if(GUILayout.Button("Frame editable layout"))
            {
                var layout=(BuildingSceneLayout)target;
                var renderers=layout.GetComponentsInChildren<Renderer>();
                if(renderers.Length>0 && SceneView.lastActiveSceneView)
                {
                    var bounds=renderers[0].bounds;
                    foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    SceneView.lastActiveSceneView.Frame(bounds,false);
                }
            }
            DrawDefaultInspector();
        }
    }
}

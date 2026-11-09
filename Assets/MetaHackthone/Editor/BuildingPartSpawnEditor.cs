using UnityEditor;
using UnityEngine;

namespace PhysicalAssembly.Editor
{
    [CustomEditor(typeof(BuildingPartSpawn)), CanEditMultipleObjects]
    public sealed class BuildingPartSpawnEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Move and rotate this parent with W / E. Its mesh and connection-point markers move together. Do not edit the preview's individual mesh children. Save the scene outside Play mode.",MessageType.Info);
            DrawDefaultInspector();
            var marker=(BuildingPartSpawn)target;
            if(!DownhillParts.IsKit(marker.Kind)) return;
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUI.BeginChangeCheck();
                float size=EditorGUILayout.Slider(new GUIContent("Part Size","Uniform size relative to the original part. 1 = original, 0.5 = half, 2 = double. Applied to runtime mesh, collider and sockets."),marker.SizeMultiplier,.1f,5f);
                if(EditorGUI.EndChangeCheck())
                    foreach(var selected in targets)
                    {
                        var spawn=(BuildingPartSpawn)selected;
                        Undo.RecordObject(spawn.transform,"Resize complete sandbox part");
                        spawn.transform.localScale=Vector3.one*size;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(spawn.transform);
                    }
            }
            var scale=marker.transform.localScale;
            if((scale-Vector3.one*marker.SizeMultiplier).sqrMagnitude>.000001f)
                EditorGUILayout.HelpBox("Use Part Size to keep all three scale axes equal. Runtime parts use the X scale; stretched preview meshes will not match.",MessageType.Warning);
            if(GUILayout.Button("Select whole part")) Selection.activeGameObject=marker.gameObject;
        }
    }
}

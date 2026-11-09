using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PhysicalAssembly.Editor
{
    [InitializeOnLoad]
    public static class DesignModeEditor
    {
        const string OverrideKey="MetaHackthone.DesignMode.StartSceneOverride";
        const string PreviousKey="MetaHackthone.DesignMode.PreviousStartScene";
        static bool returnedToEditMode;
        static DesignModeEditor()
        {
            EditorApplication.playModeStateChanged+=StateChanged;
            EditorApplication.update+=OpenRequestedTrack;
        }
        static void OpenRequestedTrack()
        {
            if(!returnedToEditMode || Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || !DesignModeSession.HasQueuedTrackEdit) return;
            // The editor has restored its original scenes. Resolve the authored track afresh.
            DesignModeSession.ClearTrackEditRequest();
            returnedToEditMode=false;
            GravityGarageEditor.PreviewLevel2();
        }
        static void StateChanged(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.ExitingEditMode || state==PlayModeStateChange.ExitingPlayMode) returnedToEditMode=false;
            if(state==PlayModeStateChange.ExitingEditMode && !DesignModeSession.HasQueuedEditorLaunch) {
                var layout=FindLayout(SceneManager.GetActiveScene());
                var authored=layout?layout.GetComponent<AuthoredSandboxLayout>():null;
                if(authored) DesignModeSession.QueueEditorLaunch(authored.EditorLevel);
            }
            if(state!=PlayModeStateChange.EnteredEditMode) return;
            returnedToEditMode=true;
            DesignModeSession.SetEnabled(false); DesignModeSession.CancelEditorLaunch();
            if(!SessionState.GetBool(OverrideKey,false)) return;
            var path=SessionState.GetString(PreviousKey,"");
            EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(path)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            SessionState.EraseBool(OverrideKey); SessionState.EraseString(PreviousKey);
        }
        [MenuItem("MetaHackthone/Developer Tools/Design/Open Car Workshop")]
        static void OpenWorkshop() => Open("Assets/MetaHackthone/Scenes/Workshop.unity");
        [MenuItem("MetaHackthone/Developer Tools/Design/Open Sandbox")]
        static void OpenSandbox() => Open("Assets/MetaHackthone/Scenes/DownhillSandbox.unity");
        static void Open(string path)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene=EditorSceneManager.OpenScene(path);
            var layout=FindLayout(scene); if(layout) Frame(layout.transform);
        }
        [MenuItem("MetaHackthone/Developer Tools/Design/Play Design Mode Menu")]
        public static void PlayMenu()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(MainMenuBuilder.ScenePath);
            Launch();
        }
        [MenuItem("MetaHackthone/Developer Tools/Design/Test Current Scene - Workbench")]
        static void TestWorkbench() => TestCurrent();
        static void TestCurrent()
        {
            var layout=FindLayout(SceneManager.GetActiveScene());
            if(!layout || layout.NarrowGap)
            { Debug.LogWarning("Open Car Workshop or Sandbox and select its editable layout first."); return; }
            Play(layout,layout.GetComponent<AuthoredSandboxLayout>()?layout.GetComponent<AuthoredSandboxLayout>().EditorLevel:DesignLevelId.GravityGarage);
        }
        static BuildingSceneLayout FindLayout(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(r=>r.GetComponentsInChildren<BuildingSceneLayout>(true)).FirstOrDefault();
        public static void Play(BuildingSceneLayout layout,DesignLevelId level=DesignLevelId.GravityGarage)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || !layout || layout.NarrowGap) return;
            // Do not reopen the scene: unsaved Scene-view edits must be tested too.
            SceneManager.SetActiveScene(layout.gameObject.scene);
            Launch(level);
        }
        static void Launch(DesignLevelId level=DesignLevelId.GravityGarage)
        {
            SessionState.SetString(PreviousKey,AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool(OverrideKey,true);
            EditorSceneManager.playModeStartScene=null;
            DesignModeSession.QueueEditorLaunch(level);
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            EditorApplication.EnterPlaymode();
        }
        public static void Frame(Transform target)
        {
            if(!target) return;
            Selection.activeGameObject=target.gameObject;
            if(SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.FrameSelected();
        }
        public static Transform CreateCarSpawn(BuildingSceneLayout layout)
        {
            if(!layout || layout.Downhill || layout.NarrowGap || layout.DesignCarSpawn) return layout?layout.DesignCarSpawn:null;
            Undo.RecordObject(layout,"Add design car spawn");
            var marker=new GameObject("Design car spawn"); Undo.RegisterCreatedObjectUndo(marker,"Add design car spawn");
            marker.transform.SetParent(layout.transform,false);
            float surface=layout.Surface?layout.transform.InverseTransformPoint(layout.Surface.bounds.center+Vector3.up*layout.Surface.bounds.extents.y).y:WorkshopBootstrap.DrivingSurfaceOffset;
            marker.transform.localPosition=new Vector3(0,surface+.16f,-.4f);
            layout.DesignCarSpawn=marker.transform;
            EditorUtility.SetDirty(layout); EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            return marker.transform;
        }
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        static void DrawSpawn(BuildingSceneLayout layout,GizmoType type)
        {
            if(!layout.DesignCarSpawn) return;
            var t=layout.DesignCarSpawn; var previous=Gizmos.matrix; var color=Gizmos.color;
            Gizmos.matrix=Matrix4x4.TRS(t.position,t.rotation,Vector3.one); Gizmos.color=new Color(.2f,1,.75f);
            Gizmos.DrawWireCube(Vector3.zero,new Vector3(.4f,.24f,.55f));
            Gizmos.DrawLine(Vector3.zero,Vector3.forward*.5f);
            Gizmos.DrawLine(Vector3.forward*.5f,new Vector3(.08f,0,.38f));
            Gizmos.DrawLine(Vector3.forward*.5f,new Vector3(-.08f,0,.38f));
            Gizmos.matrix=previous; Gizmos.color=color;
        }
    }
}

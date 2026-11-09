using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.EditorTools;
using UnityEditor.SceneManagement;
using UnityEditor.Splines;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

namespace PhysicalAssembly.Editor
{
    [InitializeOnLoad]
    public static class Level2TrackEditing
    {
        static readonly Dictionary<int,string> attempted=new();
        static double nextUpdate;
        static bool rebuilding;
        public static bool Suspended;
        static Level2TrackEditing()
        {
            EditorApplication.update+=Update;
            Undo.undoRedoPerformed+=()=> {attempted.Clear();Flush(false);};
            EditorSceneManager.sceneSaving+=(scene,path)=>Flush(false);
            EditorApplication.playModeStateChanged+=state=> {
                if(state==PlayModeStateChange.ExitingEditMode) {
                    bool valid=Flush(true);
                    try {ValidateSavedSceneIfUnloaded(SandboxSceneAuthoring.ScenePath);} catch(Exception e) {Debug.LogError(e.Message);valid=false;}
                    if(!valid)EditorApplication.isPlaying=false;
                }
                if(state==PlayModeStateChange.EnteredEditMode) attempted.Clear();
            };
            Spline.Changed+=SplineChanged;
        }
        public static Level2BezierTrack[] Tracks() => UnityEngine.Object.FindObjectsByType<Level2BezierTrack>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .Where(t=>t.gameObject.scene.IsValid() && t.gameObject.scene.isLoaded && !EditorUtility.IsPersistent(t)).ToArray();
        static void SplineChanged(Spline spline,int index,SplineModification modification)
        {
            if(Application.isPlaying || Suspended || modification!=SplineModification.KnotInserted)return;
            if(Tracks().Any(t=>t.Curve && t.Curve.Spline==spline) && index>=0 && index<spline.Count)
                spline.SetTangentMode(index,TangentMode.Continuous);
        }
        static void Update()
        {
            if(Application.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating || Suspended || rebuilding || EditorApplication.timeSinceStartup<nextUpdate)return;
            nextUpdate=EditorApplication.timeSinceStartup+.05;
            foreach(var track in Tracks()) {
                var signature=Level2TrackBaker.Signature(track);
                if(signature==track.BakedSignature && string.IsNullOrEmpty(track.ValidationError))continue;
                if(attempted.TryGetValue(track.GetInstanceID(),out var prior) && prior==signature)continue;
                attempted[track.GetInstanceID()]=signature;
                rebuilding=true;
                try {Level2TrackBaker.Rebuild(track);} finally {rebuilding=false;}
            }
        }
        public static bool Flush(bool saveAssets)
        {
            if(Application.isPlaying || Suspended || rebuilding)return true;
            rebuilding=true;bool valid=true;
            try {
                foreach(var track in Tracks()) {
                    if(!Level2TrackBaker.Rebuild(track))valid=false;
                    if(saveAssets) foreach(var strip in track.Strips) if(strip.Mesh) AssetDatabase.SaveAssetIfDirty(strip.Mesh);
                }
            } finally {rebuilding=false;}
            if(!valid) Debug.LogError("Level 2 curve is invalid. The last valid geometry is retained. Correct the curve or Undo before Play/Build.");
            return valid;
        }
        public static void ValidateSavedSceneIfUnloaded(string path)
        {
            var loaded=SceneManager.GetSceneByPath(path);
            if(loaded.IsValid() && loaded.isLoaded)return;
            var preview=EditorSceneManager.OpenPreviewScene(path);
            try {new Level2TrackBuild().OnProcessScene(preview,null);}
            finally {EditorSceneManager.ClosePreviewScene(preview);}
        }
        public static void Edit(Level2BezierTrack track)
        {
            if(!track.gameObject.activeInHierarchy && track.gameObject.scene.path==SandboxSceneAuthoring.ScenePath)
                SandboxSceneAuthoring.Open(DesignLevelId.WaterfallValley);
            Selection.activeGameObject=track.Curve.gameObject;
            // Refresh component-tool targets immediately, including when the Inspector is unfocused.
            ActiveEditorTracker.sharedTracker.ForceRebuild();
            var view=SceneView.lastActiveSceneView??EditorWindow.GetWindow<SceneView>();
            if(track.Samples.Length>0) {
                var bounds=new Bounds(track.transform.TransformPoint(track.Samples[0]),Vector3.zero);
                foreach(var p in track.Samples)bounds.Encapsulate(track.transform.TransformPoint(p));
                bounds.Expand(track.DeckWidth*track.Course.WorldScale);view.Frame(bounds,false);
            }
            view.Focus();
            ToolManager.SetActiveContext<SplineToolContext>();ToolManager.SetActiveTool<SplineMoveTool>();
            SceneView.RepaintAll();
        }
        public static void DrawButton(GravityGarage3DCourse course)
        {
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode)) {
                if(course.BezierTrack && GUILayout.Button("Edit Track Curve")) Edit(course.BezierTrack);
            }
        }
    }
    public sealed class Level2TrackSave : AssetModificationProcessor
    {
        static string[] OnWillSaveAssets(string[] paths) {Level2TrackEditing.Flush(true);return paths;}
    }
    public sealed class Level2TrackBuild : IPreprocessBuildWithReport,IProcessSceneWithReport
    {
        public int callbackOrder=>-1000;
        public void OnPreprocessBuild(BuildReport report)
        {
            if(!Level2TrackEditing.Flush(true))throw new BuildFailedException("Correct the Level 2 track before building.");
            Level2TrackEditing.ValidateSavedSceneIfUnloaded(SandboxSceneAuthoring.ScenePath);
        }
        public void OnProcessScene(Scene scene,BuildReport report)
        {
            foreach(var track in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Level2BezierTrack>(true))) {
                // Also validates unloaded build scenes. Never regenerate geometry in a build or during Play.
                try {Level2TrackBaker.SampleCurve(track);} catch(Exception e) {throw new BuildFailedException("Invalid Level 2 curve: "+e.Message);}
                if(!string.IsNullOrEmpty(track.ValidationError) || track.BakedSignature!=Level2TrackBaker.Signature(track))
                    throw new BuildFailedException("Level 2 geometry is stale. Open DownhillSandbox, correct the curve, and save before building.");
            }
        }
    }
    [CustomEditor(typeof(Level2BezierTrack))]
    public sealed class Level2BezierTrackEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var track=(Level2BezierTrack)target;
            Level2TrackEditing.DrawButton(track.Course);
            EditorGUILayout.HelpBox("Move anchors and tangent handles with Unity's spline tools. Insert on a curve or select an anchor and Delete. Ctrl+Z / Ctrl+Y undo and redo. Road, rails, collision and the saved car path update together. Save the scene after editing.",MessageType.Info);
            EditorGUILayout.LabelField("Saved path",$"{track.Samples.Length} samples, {track.Length:F2} course units");
            if(!string.IsNullOrEmpty(track.ValidationError)) EditorGUILayout.HelpBox(track.ValidationError,MessageType.Error);
        }
    }
    [CustomEditor(typeof(GravityGarage3DCourse))]
    public sealed class GravityGarage3DCourseEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI() {Level2TrackEditing.DrawButton((GravityGarage3DCourse)target);DrawDefaultInspector();}
    }
    [CustomEditor(typeof(CourseLayoutMarkers))]
    public sealed class CourseLayoutMarkersEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var track=((CourseLayoutMarkers)target).GetComponent<Level2BezierTrack>();
            if(!track){DrawDefaultInspector();return;}
            EditorGUILayout.HelpBox("Level 2 route samples and road attachments are derived from Edit Track Curve.",MessageType.Info);
            using(new EditorGUI.DisabledScope(true))DrawDefaultInspector();
        }
    }
    [CustomEditor(typeof(SandboxSplineZone))]
    public sealed class SandboxSplineZoneEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var zone=(SandboxSplineZone)target;
            if(!zone.BezierTrack){DrawDefaultInspector();return;}
            serializedObject.Update();
            using(new EditorGUI.DisabledScope(true))foreach(var name in new[]{"Roads","Points","Stop"})EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
            if(zone.ConnectionArea) {
                EditorGUILayout.HelpBox("Connection area transfers the car automatically and preserves its full incoming motion. Its shape and animation speed do not affect exit physics.",MessageType.Info);
                EditorGUILayout.LabelField("Connection range",$"{zone.GuidanceStart:F2} to {zone.GuidanceEnd:F2} course units");
                DrawPropertiesExcluding(serializedObject,"m_Script","Roads","Points","Stop","BezierTrack","BrakingDistance","CompleteLevelOnArrival","RollingResistance","CenteringDuration","RequireDrillWall");
            } else DrawPropertiesExcluding(serializedObject,"m_Script","Roads","Points","Stop","BezierTrack");
            serializedObject.ApplyModifiedProperties();
        }
    }
}

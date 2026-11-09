using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PhysicalAssembly.Editor
{
    public static class GravityGarageEditor
    {
        [MenuItem("MetaHackthone/Design Mode/" + DesignLevelCatalog.Level1Title)]
        public static void Play() => Launch(DesignLevelId.GravityGarage);
        [MenuItem("MetaHackthone/Design Mode/" + DesignLevelCatalog.Level2Title)]
        public static void PreviewLevel2() => Launch(DesignLevelId.WaterfallValley);
        [MenuItem("MetaHackthone/Design Mode/" + DesignLevelCatalog.Level1Title,true)]
        [MenuItem("MetaHackthone/Design Mode/" + DesignLevelCatalog.Level2Title,true)]
        static bool CanLaunch() => !EditorApplication.isPlayingOrWillChangePlaymode;

        static void Launch(DesignLevelId level)
        {
            SandboxSceneAuthoring.Open(level);
            if(level!=DesignLevelId.WaterfallValley) return;
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=SandboxSceneAuthoring.ScenePath) return; // Opening may have been cancelled.
            var layout=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<AuthoredSandboxLayout>(true)).Single();
            if(layout.Level2.BezierTrack) Level2TrackEditing.Edit(layout.Level2.BezierTrack);
            else Debug.LogWarning("Level 2 has no authored track curve. Run the explicit track migration first.",layout.Level2);
        }
    }
}

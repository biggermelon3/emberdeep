using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PhysicalAssembly.Editor
{
    [InitializeOnLoad]
    public static class DesktopWorkshop
    {
        static DesktopInputAdapter desktop;
        static DesktopWorkshop() { EditorApplication.update+=UpdateFocus; }
        public static void Open()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) return;
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(MainMenuBuilder.ScenePath);
        }
        [MenuItem("MetaHackthone/Play Desktop Game")]
        public static void Play()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) return;
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(MainMenuBuilder.ScenePath);
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            EditorApplication.EnterPlaymode();
        }
        static void UpdateFocus()
        {
            if(Application.isBatchMode || !EditorApplication.isPlaying) { desktop=null; return; }
            if(!desktop) desktop=Object.FindFirstObjectByType<DesktopInputAdapter>();
            if(desktop && desktop.isActiveAndEnabled && desktop.IsDesktop)
                desktop.SetInputFocus(EditorWindow.focusedWindow && EditorWindow.focusedWindow.GetType().Name=="GameView" && !EditorApplication.isPaused);
        }
    }
}

using UnityEngine;

namespace PhysicalAssembly
{
    public enum DesignLevelId { GravityGarage, WaterfallValley }
    public static class DesignLevelCatalog
    {
        public const string Level1Title="Level 1 - Gravity Garage";
        public const string Level2Title="Level 2 - Waterfall Valley";
        public static readonly System.Collections.Generic.IReadOnlyList<DesignLevelId> Levels=
            System.Array.AsReadOnly(new[]{DesignLevelId.GravityGarage,DesignLevelId.WaterfallValley});
        public static string Title(DesignLevelId level) => level==DesignLevelId.GravityGarage?Level1Title:Level2Title;
        public static bool IsValid(DesignLevelId level) => level==DesignLevelId.GravityGarage || level==DesignLevelId.WaterfallValley;
        public static bool PartAvailable(DesignLevelId level,PartKind kind) => level==DesignLevelId.WaterfallValley ||
            (kind!=PartKind.DownhillCube && kind!=PartKind.DownhillDrill);
    }
    // Play-session state only. Player builds always use the normal game flow.
    public static class DesignModeSession
    {
        public static bool Enabled { get; private set; }
        public static DesignLevelId SelectedLevel { get; private set; }
        public static void SelectLevel(DesignLevelId level) { if(!DesignLevelCatalog.IsValid(level)) throw new System.ArgumentOutOfRangeException(nameof(level)); SelectedLevel=Enabled?level:DesignLevelId.GravityGarage; }
#if UNITY_EDITOR
        const string LaunchKey="MetaHackthone.DesignMode.Launch";
        const string LevelKey="MetaHackthone.DesignMode.SelectedLevel";
        const string TrackEditKey="MetaHackthone.DesignMode.EditLevel2Track";
        public static bool HasQueuedTrackEdit => UnityEditor.SessionState.GetBool(TrackEditKey,false);
        public static bool CanEditTrack(DownhillController level) => Application.isPlaying && Enabled && level &&
            level.Course3D && level.Course3D.BezierTrack && level.Course3D.gameObject.activeInHierarchy;
        public static bool RequestTrackEdit(DownhillController level)
        {
            if(!CanEditTrack(level)) return false;
            // Persist only the navigation request across domain reload, never a Play-mode curve copy.
            UnityEditor.SessionState.SetBool(TrackEditKey,true);
            UnityEditor.EditorApplication.isPlaying=false;
            return true;
        }
        public static void ClearTrackEditRequest() => UnityEditor.SessionState.EraseBool(TrackEditKey);
        public static bool HasQueuedEditorLaunch => UnityEditor.SessionState.GetBool(LaunchKey,false);
        public static void QueueEditorLaunch(DesignLevelId level=DesignLevelId.GravityGarage)
        {
            UnityEditor.SessionState.SetBool(LaunchKey,true);
            UnityEditor.SessionState.SetInt(LevelKey,(int)level);
        }
        public static void CancelEditorLaunch() { UnityEditor.SessionState.EraseBool(LaunchKey); UnityEditor.SessionState.EraseInt(LevelKey); }
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Enabled=false;
            SelectedLevel=DesignLevelId.GravityGarage;
#if UNITY_EDITOR
            Enabled=UnityEditor.SessionState.GetBool(LaunchKey,false);
            SelectLevel((DesignLevelId)UnityEditor.SessionState.GetInt(LevelKey,0));
            CancelEditorLaunch();
#endif
        }
        public static void SetEnabled(bool enabled)
        {
            SelectedLevel=DesignLevelId.GravityGarage;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Enabled=enabled;
#else
            Enabled=false;
#endif
        }
    }
}

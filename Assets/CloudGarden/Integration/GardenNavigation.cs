using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PhysicalAssembly;
using PhysicalAssembly.MetaXR;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloudGarden.Integration
{
    public enum GameMode { CarWorkshop, NarrowGap, Sandbox, Shadow, CloudGarden, FireRescue }

    // The menu owns the player and navigation; gameplay scenes own their content.
    public sealed class GardenNavigation : MonoBehaviour
    {
        public const string MenuScene="MainMenu", WorkshopScene="Workshop", GardenScene="CloudGardenDemo",
            NarrowGapScene="NarrowGap", DownhillScene="DownhillSandbox", ShadowScene="EverydayObjects", FireScene="FireRescue";
        public static GardenNavigation Current { get; private set; }
        public MetaPlayerRig Player { get; private set; }
        public MainMenuBootstrap Menu { get; private set; }
        public bool IsTransitioning { get; private set; }
        public bool InGarden => activeScene==GardenScene;
        public bool InNarrowGap => activeScene==NarrowGapScene;
        public bool InDownhill => activeScene==DownhillScene;
        public bool InFire => activeScene==FireScene;
        public bool InShadow => activeScene!=null && activeScene!=WorkshopScene && !InGarden && !InNarrowGap && !InDownhill && !InFire;
        public bool InMainMenu => activeScene==null && Menu;
        public Func<bool> ShadowBusy { get; set; }
        public WorkshopBootstrap DownhillWorkshop { get; set; }
        public WorkshopBootstrap ChallengeWorkshop { get; set; }
        public WorkshopBootstrap CarWorkshop { get; private set; }
        public string Status { get; private set; }="Choose a mode to begin.";
        string activeScene;
        readonly Dictionary<string,SuspendedLevel> retained=new();
        sealed class SuspendedLevel
        {
            public readonly List<GameObject> Roots=new();
            public readonly List<(Rigidbody body,Vector3 position,Quaternion rotation,Vector3 linear,Vector3 angular,bool sleeping,RigidbodyInterpolation interpolation)> Motion=new();
            public Pose Camera;
            public float Fov;
            public Color Background;
            public bool Fog;
            public int PlacementVersion;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current=null; SceneManager.sceneLoaded-=SceneLoaded; SceneManager.sceneLoaded+=SceneLoaded; }
        static void SceneLoaded(Scene scene,LoadSceneMode mode)
        {
            if(scene.name!=WorkshopScene) return;
            var owner=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<WorkshopBootstrap>(true)).FirstOrDefault();
            if(!owner) return;
            var nav=Current;
            if(!nav)
            {
                nav=new GameObject("Game navigation").AddComponent<GardenNavigation>();
                SceneManager.MoveGameObjectToScene(nav.gameObject,scene);
                nav.Player=owner.GetComponent<MetaHandInputAdapter>().XRRig.GetComponent<MetaPlayerRig>();
                nav.activeScene=WorkshopScene;
            }
            nav.CarWorkshop=owner;
            owner.OpenGardenRequested=nav.EnterGarden; owner.OpenShadowRequested=nav.EnterShadow;
            owner.OpenNarrowGapRequested=nav.EnterNarrowGap; owner.OpenDownhillRequested=nav.EnterDownhill;
            owner.ReturnToWorkshopRequested=nav.ReturnToMainMenu;
        }
        void Awake() => Current=this;
        public void InitializeStandalone(MetaPlayerRig player,bool garden=true) { Player=player; activeScene=garden?GardenScene:NarrowGapScene; }
        public void InitializeStandaloneDownhill(MetaPlayerRig player)
        { Player=player; activeScene=DownhillScene; }
        public void InitializeStandaloneFire(MetaPlayerRig player) { Player=player; activeScene=FireScene; }
        public void InitializeStandaloneShadow(MetaPlayerRig player,string sceneName) { Player=player; activeScene=sceneName; }

        public void AttachMenu(MainMenuBootstrap menu,MetaPlayerRig player)
        {
            Menu=menu; Player=player;
            var scene=menu.gameObject.scene;
            foreach(var root in new[]{gameObject,player.XRRig.root.gameObject,player.DesktopCamera.transform.root.gameObject}.Distinct())
                if(root.scene!=scene) SceneManager.MoveGameObjectToScene(root,scene);
        }
        public static string SceneFor(GameMode mode) => mode switch {
            GameMode.CarWorkshop=>WorkshopScene,GameMode.NarrowGap=>NarrowGapScene,GameMode.Sandbox=>DownhillScene,
            GameMode.Shadow=>ShadowScene,GameMode.CloudGarden=>GardenScene,GameMode.FireRescue=>FireScene,_=>throw new ArgumentOutOfRangeException(nameof(mode)) };
        public bool TrySetDesignMode(bool enabled)
        {
#if UNITY_EDITOR
            if(!InMainMenu || IsTransitioning || Menu.PlacementActive) return false;
            if(DesignModeSession.Enabled==enabled) return true;
            IsTransitioning=true; StartCoroutine(ChangeDesignMode(enabled)); return true;
#else
            return false;
#endif
        }
#if UNITY_EDITOR
        IEnumerator ChangeDesignMode(bool enabled)
        {
            // These are temporary Play-mode constructions, never saved assets.
            foreach(var name in new[]{WorkshopScene,DownhillScene})
            {
                retained.Remove(name);
                var scene=SceneManager.GetSceneByName(name);
                if(scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
            CarWorkshop=null; DownhillWorkshop=null;
            DesignModeSession.SetEnabled(enabled);
            IsTransitioning=false; Menu.Show();
        }
#endif
        public void EnterSandbox()
        {
            if(IsTransitioning) return;
            EnterScene(DownhillScene);
        }
        public void EnterDesignLevel(DesignLevelId level)
        {
            if(!DesignLevelCatalog.IsValid(level) || !DesignModeSession.Enabled || !InMainMenu || IsTransitioning || (Menu && Menu.PlacementActive)) return;
            if(DesignModeSession.SelectedLevel==level) { EnterSandbox(); return; }
            IsTransitioning=true; StartCoroutine(ChangeDesignSandbox(level));
        }
        IEnumerator ChangeDesignSandbox(DesignLevelId level)
        {
            retained.Remove(DownhillScene);
            var scene=SceneManager.GetSceneByName(DownhillScene);
            if(scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            DownhillWorkshop=null;
            DesignModeSession.SelectLevel(level);
            IsTransitioning=false; EnterSandbox();
        }
        public void EnterMode(GameMode mode) => EnterScene(SceneFor(mode));
        public void EnterGarden() => EnterMode(GameMode.CloudGarden);
        public void EnterShadow() => EnterMode(GameMode.Shadow);
        public void EnterNarrowGap() => EnterMode(GameMode.NarrowGap);
        public void EnterDownhill() => EnterMode(GameMode.Sandbox);
        public void ReturnToWorkshop() => ReturnToMainMenu();
        WorkshopBootstrap ActiveWorkshop => InDownhill?DownhillWorkshop:InNarrowGap?ChallengeWorkshop:activeScene==WorkshopScene?CarWorkshop:null;

        bool CanLeave()
        {
            var owner=ActiveWorkshop;
            if(InShadow && ShadowBusy?.Invoke()==true) return false;
            if(owner && (owner.World.IsBusy || owner.World.HasHolds))
            { owner.Session.Notify("Release parts and finish attaching before opening the menu."); return false; }
            if(owner && owner.Challenge) owner.Challenge.CancelAim();
            if(owner)
            {
                if(owner.RepositionRequested) owner.ToggleReposition();
                owner.Session.EnterBuild(); owner.Session.ClearInput();
            }
            return true;
        }
        void EnterScene(string scene)
        {
            if(IsTransitioning || scene==activeScene || (Menu && Menu.PlacementActive)) return;
            if(DesignModeSession.Enabled && scene!=WorkshopScene && scene!=DownhillScene)
            { Notify("Design Mode supports Car Workshop and Sandbox. Return to normal modes for other experiences."); return; }
            if(!Application.CanStreamedLevelBeLoaded(scene)) { Notify("This mode is unavailable in this build. Choose another mode."); return; }
            if(!CanLeave()) return;
            IsTransitioning=true; Notify("Opening "+DisplayName(scene)+"...");
            StartCoroutine(Transition(scene));
        }
        public void ReturnToMainMenu()
        {
            if(IsTransitioning || activeScene==null || !CanLeave()) return;
            IsTransitioning=true; StartCoroutine(Transition(null));
        }
        public void Notify(string message) { Status=message; if(Menu) Menu.SetStatus(message); }
        static string DisplayName(string scene) => scene switch {
            WorkshopScene=>"Car Workshop",NarrowGapScene=>"Narrow Gap",DownhillScene=>"Sandbox",
            ShadowScene=>"Shadow",GardenScene=>"Cloud Garden",FireScene=>"Fire Rescue",_=>scene };

        IEnumerator EnsureMenu()
        {
            if(Menu) yield break;
            AsyncOperation load=null;
            try { if(Application.CanStreamedLevelBeLoaded(MenuScene)) load=SceneManager.LoadSceneAsync(MenuScene,LoadSceneMode.Additive); }
            catch(Exception e) { Debug.LogWarning("Main menu load failed: "+e.Message); }
            if(load!=null) yield return load;
        }
        IEnumerator Transition(string destination)
        {
            yield return null; // Leave input callbacks before disabling their objects.
            yield return EnsureMenu();
            if(!Menu) { IsTransitioning=false; Notify("Main menu could not load. Please try again."); yield break; }
            Menu.SetVisible(true); Notify(destination==null?"Choose a mode to begin.":"Opening "+DisplayName(destination)+"...");
            var leaving=ActiveWorkshop;
            if(leaving && leaving.Downhill)
            {
                while(leaving.Downhill.State==DownhillState.Transition) yield return null;
                if(!leaving.Downhill.IsBuilding) leaving.Downhill.TryReturn();
                while(!leaving.Downhill.IsBuilding) yield return null;
            }
            if(activeScene!=null) yield return SuspendActive();
            activeScene=null; ShadowBusy=null;
            SceneManager.SetActiveScene(Menu.gameObject.scene);
            if(destination==null)
            { IsTransitioning=false; Menu.Show(); yield break; }

            if(destination==DownhillScene && !DesignModeSession.Enabled)
            {
                // A normal Sandbox launch begins the campaign, never a retained
                // Level 2 preview/test. A Level 1 construction can still resume.
                if(DownhillWorkshop && (!DownhillWorkshop.Downhill.Settings.Campaign ||
                    DownhillWorkshop.Downhill.CurrentCourse!=SandboxCourseId.GravityGarage))
                {
                    retained.Remove(DownhillScene);
                    var previous=SceneManager.GetSceneByName(DownhillScene);
                    if(previous.IsValid() && previous.isLoaded) yield return SceneManager.UnloadSceneAsync(previous);
                    DownhillWorkshop=null;
                }
                if(!DownhillWorkshop) SandboxCampaignSession.Reset();
            }

            if(retained.TryGetValue(destination,out var saved))
            {
                var scene=SceneManager.GetSceneByName(destination);
                if(scene.IsValid() && scene.isLoaded)
                {
                    SceneManager.SetActiveScene(scene); activeScene=destination;
                    Player.DesktopCamera.transform.SetPositionAndRotation(saved.Camera.position,saved.Camera.rotation);
                    Player.DesktopCamera.fieldOfView=saved.Fov; Player.ViewCamera.backgroundColor=saved.Background; RenderSettings.fog=saved.Fog;
                    foreach(var root in saved.Roots) if(root) root.SetActive(true);
                    foreach(var motion in saved.Motion) if(motion.body)
                    {
                        motion.body.interpolation=RigidbodyInterpolation.None;
                        motion.body.transform.SetPositionAndRotation(motion.position,motion.rotation);
                        motion.body.position=motion.position; motion.body.rotation=motion.rotation;
                        if(!motion.body.isKinematic) { motion.body.linearVelocity=motion.linear; motion.body.angularVelocity=motion.angular; }
                    }
                    var owner=ActiveWorkshop;
                    if(owner)
                    {
                        owner.World.Collisions.Reapply();
                        if(!DesignModeSession.Enabled && PlayAreaPose.IsSet && saved.PlacementVersion!=PlayAreaPose.Version)
                        { owner.ApplyPlayArea(PlayAreaPose.Pose); if(!Player.IsXR) owner.GetComponent<DesktopInputAdapter>().FramePlatform(); }
                    }
                    Physics.SyncTransforms();
                    foreach(var motion in saved.Motion) if(motion.body)
                    { motion.body.interpolation=motion.interpolation; if(motion.sleeping && !motion.body.isKinematic) motion.body.Sleep(); }
                    retained.Remove(destination); Menu.SetVisible(false); IsTransitioning=false; yield break;
                }
                retained.Remove(destination);
            }

            AsyncOperation load=null;
            try { load=SceneManager.LoadSceneAsync(destination,LoadSceneMode.Additive); }
            catch(Exception e) { Debug.LogWarning("Mode load failed: "+e.Message); }
            if(load!=null) yield return load;
            var loaded=SceneManager.GetSceneByName(destination);
            if(!loaded.IsValid() || !loaded.isLoaded)
            { IsTransitioning=false; Menu.Show(); Notify("Could not open this mode. Please try again or choose another."); yield break; }
            SceneManager.SetActiveScene(loaded); activeScene=destination;
            yield return null; // Entry Start methods bind the player and gameplay adapters.
            Menu.SetVisible(false); IsTransitioning=false;
        }
        IEnumerator SuspendActive()
        {
            var scene=SceneManager.GetSceneByName(activeScene);
            var owner=ActiveWorkshop;
            if(owner)
            {
                var saved=new SuspendedLevel {
                    Camera=new Pose(Player.DesktopCamera.transform.position,Player.DesktopCamera.transform.rotation),
                    Fov=Player.DesktopCamera.fieldOfView,Background=Player.ViewCamera.backgroundColor,Fog=RenderSettings.fog,
                    PlacementVersion=PlayAreaPose.Version };
                foreach(var body in owner.World.Parts.Values.SelectMany(p=>p.Bodies).Select(b=>b.Rigidbody))
                {
                    saved.Motion.Add((body,body.position,body.rotation,body.isKinematic?Vector3.zero:body.linearVelocity,body.isKinematic?Vector3.zero:body.angularVelocity,body.IsSleeping(),body.interpolation));
                    // Disabling a Rigidbody adopts its rendered Transform. Flush interpolation first,
                    // otherwise a freshly moved/spawned construction jumps backwards on resume.
                    var position=body.position; var rotation=body.rotation;
                    body.interpolation=RigidbodyInterpolation.None; body.transform.SetPositionAndRotation(position,rotation);
                }
                foreach(var root in scene.GetRootGameObjects())
                    if(root.activeSelf && root!=gameObject && !Player.XRRig.IsChildOf(root.transform) && !Player.DesktopCamera.transform.IsChildOf(root.transform))
                    { saved.Roots.Add(root); root.SetActive(false); }
                retained[activeScene]=saved; yield return null;
            }
            else if(scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
        void OnDestroy() { if(Current==this) Current=null; }
    }
}

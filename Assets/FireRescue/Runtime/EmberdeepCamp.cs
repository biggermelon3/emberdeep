using System.Collections.Generic;
using CloudGarden.Integration;
using PhysicalAssembly;
using PhysicalAssembly.MetaXR;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace FireRescue
{
    // Stable head pose places the menu; each index ray selects its own pinch target.
    // Hand input comes from the existing tracked-joint provider.
    public sealed partial class EmberdeepCamp : MonoBehaviour
    {
        public enum MenuPage { Main, Settings, QuitConfirmation, Tutorial, TutorialPrompt }
        public GameObject PlayerPrefab;
        public Material PanelMaterial;
        public MetaPlayerRig Player { get; private set; }
        public bool Transitioning { get; private set; }
        public bool QuitRequested { get; private set; }
        public MenuPage ActivePage { get; private set; }
        public Transform MenuRoot => panel;
        public bool IsPlaced => placement.IsPlaced;
        public WorkshopButton HoveredButton { get; private set; }
        public bool SoundEnabled => EmberdeepAudioSettings.Sfx>0;
        readonly List<WorkshopButton> buttons=new();
        readonly Dictionary<WorkshopButton,GameObject> highlights=new();
        readonly List<XRInputSubsystem> origins=new(),foundOrigins=new();
        readonly EmberdeepMenuPose placement=new();
        EmberdeepHandRayMenu menuInput;
        public EmberdeepHandRayMenu MenuInput=>menuInput;
        MetaShadowPlayer hands;
        public EmberdeepImmersiveSky ImmersiveSky {get;private set;}
        Transform panel,mainPage,settingsPage,quitPage;
        // The sign stands in a small forest camp: meadow, tent, campfire and a few trees from the Blender kit.
        Transform campSet,campIsland;
        public Transform CampSet=>campSet;
        public Transform CampIsland=>campIsland;
        TextMesh instruction,waiting,menuHint,signTitle;
        GameObject menuBoard;
        static readonly Vector3 BoardOcclusionHalf=new(.448f,.408f,.011f);
        // The full board shows (every page but the tutorial's, EmberdeepCamp.Tutorial.cs).
        public bool MenuBoardShown=>menuBoard && menuBoard.activeInHierarchy;
        Material boardMaterial,buttonMaterial,accentMaterial;
        bool focused=true,paused,wasReady;
        OVRDisplay ovrDisplay;
        float nextOriginScan;
        Color oldAmbient;
        bool oldFog;
        UnityEngine.Rendering.AmbientMode oldAmbientMode;

        void Start()
        {
            gameObject.AddComponent<EmberdeepLocalizedText>();
            var instance=Instantiate(PlayerPrefab);Player=instance.GetComponentInChildren<MetaPlayerRig>(true);
            Player.Initialize(Player.XRRig,Player.DesktopCamera);
            hands=gameObject.AddComponent<MetaShadowPlayer>();hands.Player=Player;hands.IndependentPortableAim=true;
            var handVisuals=gameObject.AddComponent<EmberdeepHandVisuals>();handVisuals.Configure(hands);
            oldAmbient=RenderSettings.ambientLight;oldAmbientMode=RenderSettings.ambientMode;oldFog=RenderSettings.fog;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight=Color.gray;RenderSettings.fog=false;
            // Referenced resource keeps the unlit shader in release builds.
            var unlit=Resources.Load<Material>("EmberdeepMenuUnlit");
            boardMaterial=new Material(unlit?unlit:PanelMaterial);boardMaterial.SetColor("_BaseColor",new Color(.025f,.055f,.067f));
            buttonMaterial=new Material(boardMaterial);buttonMaterial.SetColor("_BaseColor",new Color(.09f,.20f,.24f));
            accentMaterial=new Material(boardMaterial);accentMaterial.SetColor("_BaseColor",new Color(.25f,.88f,.91f));
            panel=new GameObject("Emberdeep eye level menu").transform;panel.SetParent(transform,false);
            menuBoard=Shape("Menu board",panel,Vector3.zero,new Vector3(.90f,.82f,.025f),boardMaterial);
            GetComponent<EmberdeepHandVisuals>().SetOcclusionBox(0,panel,Vector3.zero,BoardOcclusionHalf);
            // LoopSpec §13: the sign carries the name only (the old tagline was cut in the text pass).
            signTitle=Text(panel,"E M B E R D E E P",new Vector3(0,.315f,-.027f),.038f,new Color(1,.73f,.38f));
            mainPage=Page("Main menu");settingsPage=Page("Settings");quitPage=Page("Quit confirmation");
            bool hasProgress=EmberdeepSession.Run.Phase!=FirePhase.Ready;
            // Commands stay stable for tests and logs; the label is what the sign shows (CAPS, §13).
            // Before the first play, both ask about the tutorial once (EmberdeepCamp.Tutorial.cs PlayOrAsk).
            if(hasProgress)Button(mainPage,"Continue",.17f,()=>PlayOrAsk(EnterFireground),"CONTINUE");
            Button(mainPage,"New Game",hasProgress?.07f:.15f,()=>PlayOrAsk(NewGame),"NEW GAME");
            // LoopSpec §9: How to play replays the camp tutorial (EmberdeepCamp.Tutorial.cs).
            Button(mainPage,"How to play",hasProgress?-.03f:.05f,StartTutorial,"HOW TO PLAY");
            Button(mainPage,"Settings",hasProgress?-.13f:-.05f,()=>ShowPage(MenuPage.Settings),"SETTINGS");
            Button(mainPage,"Quit",hasProgress?-.23f:-.15f,()=>ShowPage(MenuPage.QuitConfirmation),"QUIT");
            BuildSettingsPage(); // LoopSpec §10 options: EmberdeepCamp.Settings.cs
            Text(quitPage,"Quit Emberdeep?",new Vector3(0,.125f,-.035f),.03f,Color.white);
            Text(quitPage,"Your progress won't be saved.",new Vector3(0,.06f,-.035f),.014f,new Color(.85f,.87f,.88f));
            Button(quitPage,"Cancel",-.055f,()=>ShowPage(MenuPage.Main),"CANCEL");
            Button(quitPage,"Yes, Quit",-.18f,ConfirmQuit,"QUIT");
            instruction=Text(panel,PointPrompt,new Vector3(0,-.315f,-.035f),.014f,new Color(.68f,.85f,.86f));
            menuHint=Text(panel,"Either hand works. Open your fingers between pinches.",new Vector3(0,-.36f,-.035f),.012f,new Color(.59f,.70f,.73f));
            // A hand's ray shows on or near the sign only (EmberdeepHandRayMenu.PanelShowReach); elsewhere the hand is free
            // for palm water, the pump and the sheep (EmberdeepCampPlay.WaterRayUp).
            menuInput=gameObject.AddComponent<EmberdeepHandRayMenu>();menuInput.Initialize(transform,PanelMaterial);menuInput.ShowUnfocusedRays=true;
            RegisterSettingsSliders(); // headset test 3: drag sliders (EmberdeepCamp.Settings.cs)
            waiting=Text(transform,"Look straight ahead",Vector3.zero,.02f,Color.white);
            if(!Player.IsXR)
            {
                Player.ViewCamera.transform.SetPositionAndRotation(new Vector3(0,1.65f,0),Quaternion.identity);
                Player.ViewCamera.fieldOfView=65;Player.ViewCamera.backgroundColor=new Color(.025f,.055f,.067f);
            }
            EmberdeepAudioSettings.Apply();
            BuildCampSet();
            BuildTutorial(); // LoopSpec §9 camp tutorial + hand playground (EmberdeepCamp.Tutorial.cs, EmberdeepCampPlay)
            // Water first (headset test 3): a water hand aiming its open palm at the island gets no near-sign ray.
            menuInput.PalmAiming=hand=>Play && Play.PalmAimPose(hand);
            ImmersiveSky=gameObject.AddComponent<EmberdeepImmersiveSky>();ImmersiveSky.InitializeMenu(Player.ViewCamera,panel);
            // The sky window opens around the island as well as the sign.
            if(campSet)ImmersiveSky.SetEquipmentSubjects(campSet,null,null,null);
            ShowPage(MenuPage.Main);Recenter();
        }
        // The game is mixed reality with a miniature world, so the camp is a tabletop island
        // floating under the sign: meadow, tent, campfire and trees at diorama scale, with
        // the sign's two posts rising out of it.
        public const float CampScale=.27f,CampDrop=.74f;
        // Island space: -Z is the player's side. The front apron holds the camp, trees frame the sign.
        // Audited 2026-11-21 (surface voxels, 1.35 cm): no prop touches another, a sign post or the board
        // (EmberdeepCampLayoutTests keeps it that way). The sign posts stand at island (+-1.93, -.69), so the
        // side cedars sit behind them, not around them.
        // The sapling the tutorial's burning sheep lights (EmberdeepCampPlay), at the front so palm water reaches it.
        // Declared before CampKit: static initializers run in text order.
        public static readonly Vector3 SaplingAt=new(1.15f,0,-2.35f);
        public static readonly (string Id,Vector3 At,float Yaw,float Scale)[] CampKit=
        {
            ("Campfire",new Vector3(.55f,0,-1.55f),0,1),
            ("Log",new Vector3(1.18f,0,-1.36f),62,1.4f),
            ("Log",new Vector3(.08f,0,-2.22f),0,1.25f),
            ("Tent",new Vector3(-1.02f,0,-1.42f),-58,.92f),
            ("CampCrate",new Vector3(1.70f,0,-1.22f),-20,.9f),
            ("Cedar",new Vector3(-2.0f,0,.48f),20,1.7f),
            ("Cedar",new Vector3(2.05f,0,.6f),140,1.85f),
            ("Cedar",new Vector3(-.6f,0,1.6f),250,1.15f),
            ("Broadleaf",new Vector3(.75f,0,1.55f),70,1.3f),
            ("Rock",new Vector3(1.38f,0,-.48f),30,1f),
            ("Fern",new Vector3(-1.52f,0,-.22f),0,1.4f),
            ("Broadleaf",SaplingAt,30,.45f),
        };
        // Sign posts stand in camp-set space, just outside the menu board.
        public const float SignPostX=.52f,SignPostZ=.035f,IslandZ=.22f;
        void BuildCampSet()
        {
            var art=EmberdeepArtLibrary.Load();if(!art || !art.Find("CampGround"))return;
            campSet=new GameObject("Forest rescue camp set").transform;campSet.SetParent(transform,false);
            campIsland=new GameObject("Miniature camp island").transform;campIsland.SetParent(campSet,false);
            campIsland.localPosition=new Vector3(0,0,IslandZ);campIsland.localScale=Vector3.one*CampScale;
            art.Create("CampGround",campIsland,Vector3.zero);
            foreach(var prop in CampKit)Place(art,prop.Id,prop.At,prop.Yaw,prop.Scale);
            for(int side=0;side<2;side++)
            {
                float x=side==0?-SignPostX:SignPostX;
                var post=art.Create("SignPost",campSet,new Vector3(x,0,SignPostZ));
                if(post){post.localRotation=Quaternion.Euler(0,side*90,0);post.localScale=new Vector3(1,CampDrop+.47f,1);}
                var cap=art.Create("SignPostCap",campSet,new Vector3(x,CampDrop+.47f,SignPostZ));
                if(cap)cap.localRotation=Quaternion.Euler(0,side*90,0);
            }
            foreach(var r in campSet.GetComponentsInChildren<Renderer>(true))r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            // The diorama shader takes its key light from the scene's directional light.
            var lamp=new GameObject("Warm camp sun").AddComponent<Light>();lamp.transform.SetParent(campSet,false);
            lamp.type=LightType.Directional;lamp.intensity=1.1f;lamp.color=new Color(1,.91f,.79f);lamp.shadows=LightShadows.None;
            lamp.transform.localRotation=Quaternion.Euler(48,-35,0);
            campSet.gameObject.SetActive(false);
        }
        void Place(EmberdeepArtLibrary art,string id,Vector3 at,float yaw,float scale)
        {
            var piece=art.Create(id,campIsland,at,scale);
            if(piece)piece.localRotation=Quaternion.Euler(0,yaw,0);
        }
        void PlaceCampSet(Pose menu)
        {
            if(!campSet)return;
            var forward=Vector3.ProjectOnPlane(menu.rotation*Vector3.forward,Vector3.up);
            if(forward.sqrMagnitude<.01f)return;
            // The meadow hangs a fixed drop below the board's centre and never sinks into the floor.
            float top=Mathf.Max(Player.FloorHeight+.25f,menu.position.y-CampDrop);
            campSet.SetPositionAndRotation(new Vector3(menu.position.x,top,menu.position.z),Quaternion.LookRotation(forward.normalized,Vector3.up));
            campSet.gameObject.SetActive(true);
        }
        public void NewGame()
        {
            if(Transitioning)return;
            EmberdeepSession.NewExpedition();EnterFireground();
        }
        public void EnterFireground()
        {
            if(Transitioning)return;
            Transitioning=true;menuInput?.ResetInput();ClearHover();
            SceneManager.LoadSceneAsync(EmberdeepSession.FireScene,LoadSceneMode.Single);
        }
        public void ShowPage(MenuPage page)
        {
            ActivePage=page;menuInput?.ResetInput();ClearHover();
            mainPage.gameObject.SetActive(page==MenuPage.Main);
            settingsPage.gameObject.SetActive(page==MenuPage.Settings);
            quitPage.gameObject.SetActive(page==MenuPage.QuitConfirmation);
            if(tutorialPage)tutorialPage.gameObject.SetActive(page==MenuPage.Tutorial);
            if(promptPage)promptPage.gameObject.SetActive(page==MenuPage.TutorialPrompt);
            SetTutorialSign(page==MenuPage.Tutorial);
            // The tutorial page has no sentences (LoopSpec §9): the ray lines step aside.
            if(instruction)instruction.gameObject.SetActive(page!=MenuPage.Tutorial);
            if(menuHint)menuHint.gameObject.SetActive(page!=MenuPage.Tutorial);
        }
        public void Recenter()
        {
            placement.RequestRecenter();menuInput?.ResetInput();ClearHover();
            if(panel)panel.gameObject.SetActive(false);
            if(campSet)campSet.gameObject.SetActive(false);
            if(waiting)waiting.gameObject.SetActive(true);
        }
        public void ConfirmQuit()
        {
            if(ActivePage!=MenuPage.QuitConfirmation || QuitRequested)return;
            QuitRequested=true;menuInput?.ResetInput();ClearHover();Application.Quit();
        }
        void LateUpdate()
        {
            if(!Player || !panel || Transitioning || QuitRequested)return;
            ImmersiveSky?.Tick(Time.unscaledDeltaTime,true);
            ScanOrigins();var view=Player.ViewCamera.transform;
            if(!placement.IsPlaced)waiting.transform.SetPositionAndRotation(view.position+view.forward*.9f,view.rotation);
            bool ready=focused && !paused && hands.HeadReady && ValidHeadPose(view);
            if(!ready)
            {
                if(wasReady)Recenter();
                wasReady=false;menuInput?.ResetInput();ClearHover();return;
            }
            wasReady=true;
            if(placement.TryUpdate(ready,new Pose(view.position,view.rotation),Player.FloorHeight,Time.unscaledDeltaTime,out var pose))
            {
                panel.SetPositionAndRotation(pose.position,pose.rotation);panel.gameObject.SetActive(true);PlaceCampSet(pose);
                waiting.gameObject.SetActive(false);Physics.SyncTransforms();
                Debug.Log($"EMBERDEEP_MENU_PLACED eyeY={view.position.y:F3} menuY={panel.position.y:F3} forward={view.forward}");
            }
            if(!placement.IsPlaced)
            {
                waiting.transform.SetPositionAndRotation(view.position+view.forward*.9f,view.rotation);
                menuInput?.ResetInput();ClearHover();return;
            }
            ApplyMenuResult(menuInput.Process(hands,Player.ViewCamera,Player.IsXR,buttons,true,Time.unscaledDeltaTime));
            if(Player.IsXR)PokeSliders(Time.unscaledDeltaTime);
            TickSettingsSliders();
            TickTutorialMenu();
        }
        // A fingertip on a slider's track drags it too (EmberdeepHandRayMenu.Poke). The sign hangs ~1 m out, so this only
        // matters for a player who walks up to it.
        void PokeSliders(float dt)
        {
            if(ActivePage!=MenuPage.Settings)return;
            for(int hand=0;hand<2;hand++)
            {
                bool left=hand==0;var visuals=GetComponent<EmberdeepHandVisuals>();
                bool tracked=hands.TryReadFingertip(left,1,out var tip) && (!visuals || visuals.Seen(left));
                menuInput.Poke(hand,tracked,tip,dt);
            }
        }
        // Compatibility entry point for tests/desktop callers with an explicit pointing ray.
        // Runtime XR uses two independent system hand pointers, never head-center selection.
        public void ProcessMenuInput(Ray ray,bool available,bool leftTracked,float leftDistance,bool rightTracked,float rightDistance,float dt)
        {
            ProcessHandMenuInput(new EmberdeepHandRay.Sample(leftTracked,ray,leftDistance),
                new EmberdeepHandRay.Sample(rightTracked,ray,rightDistance),available,dt);
        }
        public void ProcessHandMenuInput(EmberdeepHandRay.Sample left,EmberdeepHandRay.Sample right,bool available,float dt)
        {
            available=available && focused && !paused && IsPlaced && !Transitioning && !QuitRequested;
            ApplyMenuResult(menuInput.ProcessSamples(left,right,buttons,available,dt));
        }
        void ApplyMenuResult(bool pressed)
        {
            HoveredButton=menuInput.HoveredButton;
            foreach(var button in buttons)
            {
                highlights[button].SetActive(menuInput.IsHovered(button));
            }
            instruction.text=!menuInput.HasPointer?HandPrompt:!HoveredButton?PointPrompt:PinchPrompt;
            if(pressed)
            {
                var button=menuInput.PressedButton;
                Debug.Log("EMBERDEEP_HAND_AIM_PINCH hand="+menuInput.PointerHand+" "+button.Command);
                button.Press();
            }
        }
        void ClearHover()
        {
            HoveredButton=null;
            if(instruction)instruction.text=PointPrompt;
            foreach(var b in buttons){b.SetInteractionState(false,false);highlights[b].SetActive(false);}
            menuInput?.ResetInput();
        }
        void ScanOrigins()
        {
            if(Time.unscaledTime<nextOriginScan)return;
            nextOriginScan=Time.unscaledTime+1;SubsystemManager.GetSubsystems(foundOrigins);
            foreach(var origin in foundOrigins)if(!origins.Contains(origin))
            {origins.Add(origin);origin.trackingOriginUpdated+=OriginChanged;}
            if(ovrDisplay==null && Player.IsXR && Player.XRRig.GetComponent<OVRCameraRig>())
            {
                ovrDisplay=OVRManager.display;
                if(ovrDisplay!=null)ovrDisplay.RecenteredPose+=Recenter;
            }
        }
        bool ValidHeadPose(Transform view)
        {
            if(!Player.IsXR)return true;
            var head=InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if(!head.isValid)return false;
            bool tr=head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked,out bool tracked);
            bool sr=head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trackingState,out InputTrackingState state);
            bool pr=head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition,out Vector3 rawPosition);
            var mode=TrackingOriginModeFlags.Unknown;
            foreach(var origin in origins)if(origin.running){mode=origin.GetTrackingOriginMode();break;}
            return EmberdeepHeadPoseGate.Allows(new Pose(view.position,view.rotation),Player.FloorHeight,tr,tracked,sr,state,mode,pr,rawPosition);
        }
        void OriginChanged(XRInputSubsystem _)=>Recenter();
        Transform Page(string name)
        {var go=new GameObject(name);go.transform.SetParent(panel,false);return go.transform;}
        // The ray prompt under the buttons (sentence case, LoopSpec §13): no hand seen / pointing / on a button.
        public const string HandPrompt="Hold up an open hand",PointPrompt="Point at a button and pinch",PinchPrompt="Pinch to select";
        TextMesh Button(Transform parent,string name,float y,System.Action action,string label=null)
        {
            var at=new Vector3(0,y,-.03f);
            var go=Shape(name,parent,at,new Vector3(.68f,.085f,.025f),buttonMaterial);
            var collider=go.AddComponent<BoxCollider>();collider.isTrigger=true;
            EmberdeepForestUi.Apply(go,true);
            var button=go.AddComponent<WorkshopButton>();button.Command=name;button.Pressed=action;buttons.Add(button);
            var highlight=Shape(name+" ray outline",parent,at+Vector3.forward*.01f,new Vector3(.695f,.1f,.01f),accentMaterial);
            highlights.Add(button,highlight);highlight.SetActive(false);
            return Text(parent,label??name,at+Vector3.back*.016f,.025f,Color.white);
        }
        GameObject Shape(string name,Transform parent,Vector3 at,Vector3 size,Material material,PrimitiveType type=PrimitiveType.Cube)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);
            go.transform.localPosition=at;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;
            if(name=="Menu board" || name=="Tutorial board")EmberdeepForestUi.Apply(go);
            var collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);return go;
        }
        TextMesh Text(Transform parent,string value,Vector3 at,float size,Color color)
        {
            var go=new GameObject("Menu label");go.transform.SetParent(parent,false);go.transform.localPosition=at;
            var t=go.AddComponent<TextMesh>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            go.GetComponent<MeshRenderer>().sharedMaterial=t.font.material;t.fontSize=80;t.characterSize=size*20/80;
            t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=color;t.text=value;return t;
        }
        void OnApplicationFocus(bool value){focused=value;if(!value){menuInput?.ResetInput();ClearHover();}else if(panel)Recenter();}
        void OnApplicationPause(bool value){paused=value;if(value){menuInput?.ResetInput();ClearHover();}else if(panel)Recenter();}
        void OnDisable(){menuInput?.ResetInput();ClearHover();}
        void OnDestroy()
        {
            foreach(var origin in origins)origin.trackingOriginUpdated-=OriginChanged;
            if(ovrDisplay!=null)ovrDisplay.RecenteredPose-=Recenter;
            ReleaseSettings();ReleaseTutorial();
            if(boardMaterial)Destroy(boardMaterial);if(buttonMaterial)Destroy(buttonMaterial);if(accentMaterial)Destroy(accentMaterial);
            RenderSettings.ambientLight=oldAmbient;RenderSettings.ambientMode=oldAmbientMode;RenderSettings.fog=oldFog;
        }
    }
}


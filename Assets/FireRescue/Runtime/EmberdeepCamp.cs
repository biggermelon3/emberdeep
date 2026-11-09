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
    public sealed class EmberdeepCamp : MonoBehaviour
    {
        public enum MenuPage { Main, Settings, QuitConfirmation }
        public GameObject PlayerPrefab;
        public Material PanelMaterial;
        public MetaPlayerRig Player { get; private set; }
        public bool Transitioning { get; private set; }
        public bool QuitRequested { get; private set; }
        public MenuPage ActivePage { get; private set; }
        public Transform MenuRoot => panel;
        public bool IsPlaced => placement.IsPlaced;
        public WorkshopButton HoveredButton { get; private set; }
        public bool SoundEnabled => PlayerPrefs.GetInt("Emberdeep.Sound",1)!=0;
        readonly List<WorkshopButton> buttons=new();
        readonly Dictionary<WorkshopButton,GameObject> highlights=new();
        readonly List<XRInputSubsystem> origins=new(),foundOrigins=new();
        readonly EmberdeepMenuPose placement=new();
        EmberdeepHandRayMenu menuInput;
        public EmberdeepHandRayMenu MenuInput=>menuInput;
        MetaShadowPlayer hands;
        Transform panel,mainPage,settingsPage,quitPage;
        TextMesh instruction,waiting,soundLabel;
        Material boardMaterial,buttonMaterial,accentMaterial;
        bool focused=true,paused,wasReady;
        OVRDisplay ovrDisplay;
        float nextOriginScan;
        Color oldAmbient;
        bool oldFog;
        UnityEngine.Rendering.AmbientMode oldAmbientMode;

        void Start()
        {
            var instance=Instantiate(PlayerPrefab);Player=instance.GetComponentInChildren<MetaPlayerRig>(true);
            Player.Initialize(Player.XRRig,Player.DesktopCamera);
            hands=gameObject.AddComponent<MetaShadowPlayer>();hands.Player=Player;
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
            Shape("Menu board",panel,Vector3.zero,new Vector3(.90f,.82f,.025f),boardMaterial);
            GetComponent<EmberdeepHandVisuals>().SetOcclusionBox(0,panel,Vector3.zero,new Vector3(.448f,.408f,.011f));
            Text(panel,"E M B E R D E E P",new Vector3(0,.315f,-.027f),.038f,new Color(1,.73f,.38f));
            Text(panel,"A MINIATURE FIRE EXPEDITION",new Vector3(0,.248f,-.027f),.014f,new Color(.68f,.85f,.86f));
            mainPage=Page("Main menu");settingsPage=Page("Settings");quitPage=Page("Quit confirmation");
            bool hasProgress=EmberdeepSession.Run.Phase!=FirePhase.Ready;
            if(hasProgress)Button(mainPage,"Continue",.145f,EnterFireground);
            Button(mainPage,"New Game",hasProgress?.035f:.12f,NewGame);
            Button(mainPage,"Settings",hasProgress?-.075f:0,()=>ShowPage(MenuPage.Settings));
            Button(mainPage,"Quit",hasProgress?-.185f:-.12f,()=>ShowPage(MenuPage.QuitConfirmation));
            Text(settingsPage,"SETTINGS",new Vector3(0,.16f,-.035f),.02f,Color.white);
            soundLabel=Button(settingsPage,"Sound",.055f,ToggleSound);
            Button(settingsPage,"Recenter Menu",-.065f,Recenter);
            Button(settingsPage,"Back",-.185f,()=>ShowPage(MenuPage.Main));
            Text(quitPage,"Quit Emberdeep?",new Vector3(0,.125f,-.035f),.03f,Color.white);
            Text(quitPage,"This showcase does not save after closing.",new Vector3(0,.06f,-.035f),.014f,new Color(.85f,.87f,.88f));
            Button(quitPage,"Cancel",-.055f,()=>ShowPage(MenuPage.Main));
            Button(quitPage,"Yes, Quit",-.18f,ConfirmQuit);
            instruction=Text(panel,"AIM YOUR HAND AT A BUTTON  -  PINCH",new Vector3(0,-.315f,-.035f),.014f,new Color(.68f,.85f,.86f));
            Text(panel,"Use either hand. Open your fingers before each pinch.",new Vector3(0,-.36f,-.035f),.012f,new Color(.59f,.70f,.73f));
            menuInput=gameObject.AddComponent<EmberdeepHandRayMenu>();menuInput.Initialize(transform,PanelMaterial);
            waiting=Text(transform,"Look ahead\nFinding your headset position...",Vector3.zero,.02f,Color.white);
            if(!Player.IsXR)
            {
                Player.ViewCamera.transform.SetPositionAndRotation(new Vector3(0,1.65f,0),Quaternion.identity);
                Player.ViewCamera.fieldOfView=65;Player.ViewCamera.backgroundColor=new Color(.025f,.055f,.067f);
            }
            AudioListener.volume=SoundEnabled?1:0;
            RefreshSound();ShowPage(MenuPage.Main);Recenter();
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
        }
        public void Recenter()
        {
            placement.RequestRecenter();menuInput?.ResetInput();ClearHover();
            if(panel)panel.gameObject.SetActive(false);
            if(waiting)waiting.gameObject.SetActive(true);
        }
        void ToggleSound()
        {
            PlayerPrefs.SetInt("Emberdeep.Sound",SoundEnabled?0:1);PlayerPrefs.Save();
            AudioListener.volume=SoundEnabled?1:0;RefreshSound();
        }
        void RefreshSound()=>soundLabel.text=SoundEnabled?"Sound: On":"Sound: Off";
        public void ConfirmQuit()
        {
            if(ActivePage!=MenuPage.QuitConfirmation || QuitRequested)return;
            QuitRequested=true;menuInput?.ResetInput();ClearHover();Application.Quit();
        }
        void LateUpdate()
        {
            if(!Player || !panel || Transitioning || QuitRequested)return;
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
                panel.SetPositionAndRotation(pose.position,pose.rotation);panel.gameObject.SetActive(true);
                waiting.gameObject.SetActive(false);Physics.SyncTransforms();
                Debug.Log($"EMBERDEEP_MENU_PLACED eyeY={view.position.y:F3} menuY={panel.position.y:F3} forward={view.forward}");
            }
            if(!placement.IsPlaced)
            {
                waiting.transform.SetPositionAndRotation(view.position+view.forward*.9f,view.rotation);
                menuInput?.ResetInput();ClearHover();return;
            }
            ApplyMenuResult(menuInput.Process(hands,Player.ViewCamera,Player.IsXR,buttons,true,Time.unscaledDeltaTime));
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
            instruction.text=!menuInput.HasPointer?"SHOW AN OPEN HAND TO BEGIN":!HoveredButton?
                "AIM YOUR HAND AT A BUTTON  -  PINCH":"BUTTON FOUND  -  PINCH WITH THE POINTING HAND";
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
            if(instruction)instruction.text="AIM YOUR HAND AT A BUTTON  -  PINCH";
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
        TextMesh Button(Transform parent,string name,float y,System.Action action)
        {
            var at=new Vector3(0,y,-.03f);
            var go=Shape(name,parent,at,new Vector3(.68f,.085f,.025f),buttonMaterial);
            var collider=go.AddComponent<BoxCollider>();collider.isTrigger=true;
            var button=go.AddComponent<WorkshopButton>();button.Command=name;button.Pressed=action;buttons.Add(button);
            var highlight=Shape(name+" ray outline",parent,at+Vector3.forward*.01f,new Vector3(.695f,.1f,.01f),accentMaterial);
            highlights.Add(button,highlight);highlight.SetActive(false);
            return Text(parent,name,at+Vector3.back*.016f,.025f,Color.white);
        }
        GameObject Shape(string name,Transform parent,Vector3 at,Vector3 size,Material material,PrimitiveType type=PrimitiveType.Cube)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);
            go.transform.localPosition=at;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;
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
            if(boardMaterial)Destroy(boardMaterial);if(buttonMaterial)Destroy(buttonMaterial);if(accentMaterial)Destroy(accentMaterial);
            RenderSettings.ambientLight=oldAmbient;RenderSettings.ambientMode=oldAmbientMode;RenderSettings.fog=oldFog;
        }
    }
}


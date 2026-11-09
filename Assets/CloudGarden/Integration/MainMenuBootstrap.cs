using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Oculus.Interaction;
using PhysicalAssembly;
using PhysicalAssembly.MetaXR;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CloudGarden.Integration
{
    public sealed class MainMenuBootstrap : MonoBehaviour
    {
        public GameObject PlayerPrefab;
        public Material PanelMaterial, CardMaterial, AccentMaterial;
        public Light MenuLight;
        public Transform Panel { get; private set; }
        public GardenNavigation Navigation { get; private set; }
        public MetaFloorPlacement Placement { get; private set; }
        public bool PlacementActive => Placement && Placement.IsPlacing;
        public bool Visible => Panel && (Panel.gameObject.activeSelf || designVisible);
        bool designVisible;
        readonly List<WorkshopButton> modeButtons=new();
        WorkshopButton placementButton;
        TextMesh status,placementLabel;
        float inputReadyAt;
        bool waitForRelease;
        readonly HashSet<int> selectedPointers=new();
        readonly List<MetaPinchSelector> selectors=new();
        RayInteractor[] rays;
        float nextBinding;

        void Awake()
        {
            Navigation=GardenNavigation.Current;
            var player=Navigation?Navigation.Player:null;
            if(!player)
            {
                var instance=Instantiate(PlayerPrefab);
                player=instance.GetComponentInChildren<MetaPlayerRig>(true);
                player.Initialize(player.XRRig,player.DesktopCamera);
            }
            if(!Navigation) Navigation=new GameObject("Game navigation").AddComponent<GardenNavigation>();
            Navigation.AttachMenu(this,player);
            rays=player.XRRig.GetComponentsInChildren<RayInteractor>(true);
            BuildMenu();
            Placement=gameObject.AddComponent<MetaFloorPlacement>();
            Placement.Initialize(player,()=>!Navigation.IsTransitioning,pose=> {
                PlayAreaPose.Set(pose.position,pose.rotation,player.FloorHeight);
                EndPlacement("Play area set. Choose a mode when you are ready.");
            });
            if(Navigation.InMainMenu) Show(); else SetVisible(false);
        }
        IEnumerator Start()
        {
            var player=Navigation.Player;
            if(player.IsXR)
            {
                float deadline=Time.realtimeSinceStartup+5;
                while(player.ViewCamera.transform.position.y-player.FloorHeight<.4f && Time.realtimeSinceStartup<deadline) yield return null;
                if(Navigation.InMainMenu) Show();
            }
        }
        void BuildMenu()
        {
            Panel=new GameObject("Mode selection panel").transform; Panel.SetParent(transform,false);
            Box("Menu background",Panel,Vector3.zero,new Vector3(1.28f,1.22f,.018f),PanelMaterial,false);
            Label(Panel,"META HACKTHONE",new Vector3(-.57f,.52f,-.022f),.023f,new Color(.3f,1,.82f));
            Label(Panel,"Choose your mode",new Vector3(-.57f,.435f,-.022f),.052f,Color.white);
            AddMode(GameMode.Sandbox,"WORKBENCH BUILD","Build on a compact bench.",new Vector2(0,.26f),()=>Navigation.EnterSandbox());
            AddMode(GameMode.NarrowGap,"Narrow Gap","Build. Aim. Fit through.",new Vector2(-.305f,.045f));
            AddMode(GameMode.Shadow,"Shadow","Objects become shadow puzzles.",new Vector2(.305f,.045f));
            AddMode(GameMode.CarWorkshop,"Car Workshop","Create, connect, and drive.",new Vector2(-.305f,-.17f));
            AddMode(GameMode.CloudGarden,"Cloud Garden","Grow a little world of your own.",new Vector2(.305f,-.17f));
            placementButton=Button("Place / Move Play Area",new Vector3(0,-.38f,-.025f),new Vector3(1.17f,.115f,.025f),TogglePlacement,true);
            placementLabel=Label(placementButton.transform,"Place / Move Play Area",new Vector3(-.52f,0,-.025f),.033f,Color.white,true);
            status=Label(Panel,"Choose a mode to begin.",new Vector3(-.57f,-.49f,-.024f),.022f,new Color(.7f,.83f,.85f));
            Label(Panel,"POINT + PINCH  /  TOUCH TO SELECT",new Vector3(-.57f,-.565f,-.024f),.018f,new Color(.45f,.60f,.64f));
        }
        void AddMode(GameMode mode,string title,string subtitle,Vector2 position,System.Action action=null)
        {
            var button=Button(title,new Vector3(position.x,position.y,-.025f),new Vector3(.56f,.175f,.025f),action??(()=>Navigation.EnterMode(mode)),false);
            modeButtons.Add(button);
            Label(button.transform,title,new Vector3(-.245f,.033f,-.025f),.036f,Color.white,true);
            Label(button.transform,subtitle,new Vector3(-.245f,-.035f,-.025f),.020f,new Color(.65f,.81f,.83f),true);
        }
        WorkshopButton Button(string title,Vector3 position,Vector3 size,System.Action pressed,bool utility)
        {
            var go=Box(title,Panel,position,size,utility?AccentMaterial:CardMaterial,true);
            var button=go.AddComponent<WorkshopButton>(); button.Command=title; button.Pressed=pressed;
            var target=go.AddComponent<MetaMenuButton>();
            target.Initialize(button.Press,()=>CanPress(utility),button.SetInteractionState,new Vector3(0,0,-.51f),Quaternion.identity);
            target.Ray.WhenPointerEventRaised+=TrackSelection;
            target.Poke.WhenPointerEventRaised+=TrackSelection;
            return button;
        }
        void TrackSelection(PointerEvent evt)
        {
            if(evt.Type==PointerEventType.Select) selectedPointers.Add(evt.Identifier);
            if(evt.Type==PointerEventType.Unselect || evt.Type==PointerEventType.Cancel) selectedPointers.Remove(evt.Identifier);
        }
        bool CanPress(bool utility) => Visible && !Navigation.IsTransitioning && Time.unscaledTime>=inputReadyAt && !waitForRelease && (utility || !PlacementActive);
        public void Show()
        {
            SetVisible(true);
            var player=Navigation.Player;
            if(!player.IsXR)
            {
                player.DesktopCamera.transform.SetPositionAndRotation(new Vector3(0,1.6f,0),Quaternion.identity);
                player.DesktopCamera.fieldOfView=55; player.DesktopCamera.backgroundColor=new Color(.018f,.028f,.044f);
            }
            else player.ViewCamera.backgroundColor=Color.clear;
            RenderSettings.fog=false;
            var head=player.ViewCamera.transform;
            var forward=Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.1f) forward=Vector3.forward;
            Panel.SetPositionAndRotation(head.position+forward*1.55f-Vector3.up*.10f,Quaternion.LookRotation(forward));
            SetStatus(DesignModeSession.Enabled?"Choose a level. Its test car loads automatically.":"Choose a mode to begin.");
            ArmInput();
        }
        void ArmInput() { inputReadyAt=Time.unscaledTime+.25f; nextBinding=0; waitForRelease=true; selectedPointers.Clear(); }
        public void SetVisible(bool visible)
        {
            designVisible=visible && DesignModeSession.Enabled;
            Panel.gameObject.SetActive(visible && !designVisible);
            if(MenuLight) MenuLight.gameObject.SetActive(visible);
            if(!visible) { if(Placement) Placement.Cancel(); selectedPointers.Clear(); }
            else ArmInput();
        }
        public void SetStatus(string message) { if(status) status.text=message; }
        public void TogglePlacement()
        {
            if(Navigation.IsTransitioning) return;
            if(PlacementActive) { Placement.Cancel(); EndPlacement("Placement cancelled. Your previous play area is unchanged."); return; }
            Placement.Begin(); placementLabel.text="Cancel placement";
            SetStatus("Release your pinch, point at the floor, then pinch to place.");
            foreach(var button in modeButtons) button.Interactable=false;
        }
        void EndPlacement(string message)
        {
            placementLabel.text="Place / Move Play Area";
            foreach(var button in modeButtons) button.Interactable=true;
            SetStatus(message); ArmInput();
        }
        void Update()
        {
            if(!Visible) return;
            if(DesignModeSession.Enabled) return;
            var player=Navigation.Player;
            // Bind independently of gameplay adapters, which do not exist at cold startup.
            if(Time.unscaledTime>=nextBinding)
            {
                nextBinding=Time.unscaledTime+.25f; selectors.Clear();
                foreach(var ray in rays)
                {
                    var selector=MetaPinchSelector.BindToRay(ray);
                    if(selector) { if(Navigation.InMainMenu && !Navigation.IsTransitioning) selector.SuppressSelection=null; selectors.Add(selector); }
                }
            }
            bool released=selectors.All(s=>!s.IsPinching) && selectedPointers.Count==0 && (Mouse.current==null || !Mouse.current.leftButton.isPressed);
            if(released && Time.unscaledTime>=inputReadyAt) waitForRelease=false;
            foreach(var button in modeButtons) button.Interactable=!Navigation.IsTransitioning && !PlacementActive;
            placementButton.Interactable=!Navigation.IsTransitioning;
            if(player.IsXR || Mouse.current==null) return;
#if UNITY_EDITOR
            var guiPoint=new Vector2(Mouse.current.position.ReadValue().x,Screen.height-Mouse.current.position.ReadValue().y);
            if(DesignEntryRect.Contains(guiPoint)) return;
#endif
            var mouse=Mouse.current; var raycast=player.ViewCamera.ScreenPointToRay(mouse.position.ReadValue());
            WorkshopButton hovered=null;
            if(Physics.Raycast(raycast,out var hit,20,~0,QueryTriggerInteraction.Collide)) hovered=hit.collider.GetComponent<WorkshopButton>();
            foreach(var button in modeButtons.Append(placementButton)) button.SetInteractionState(button==hovered,button==hovered&&mouse.leftButton.isPressed);
            if(mouse.leftButton.wasPressedThisFrame && hovered && hovered.transform.IsChildOf(Panel) && CanPress(hovered==placementButton)) hovered.Press();
            else if(PlacementActive)
            {
                var floor=new Plane(Vector3.up,Vector3.up*player.FloorHeight);
                if(floor.Raycast(raycast,out var distance)) Placement.SampleDesktop(raycast.GetPoint(distance),mouse.leftButton.wasPressedThisFrame,mouse.leftButton.isPressed);
            }
            if(PlacementActive && Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame) TogglePlacement();
        }
#if UNITY_EDITOR
        static Rect DesignEntryRect => new Rect(14,14,Mathf.Min(310,Screen.width-28),112);
        void OnGUI()
        {
            if(!Visible || !Navigation) return;
            bool previous=GUI.enabled;
            GUI.enabled=!Navigation.IsTransitioning && !PlacementActive;
            if(!DesignModeSession.Enabled)
            {
                GUILayout.BeginArea(DesignEntryRect,GUI.skin.box);
                GUILayout.Label("LEVEL DESIGN / UNITY EDITOR");
                if(GUILayout.Button("Design Mode",GUILayout.Height(32))) Navigation.TrySetDesignMode(true);
                GUILayout.Label("Mouse + keyboard. Ready-built test cars.");
                GUILayout.Label("Switching modes resets temporary builds.");
                GUILayout.EndArea();
            }
            else
            {
                float width=Mathf.Min(440,Screen.width-28),height=Mathf.Min(420,Screen.height-28);
                GUILayout.BeginArea(new Rect((Screen.width-width)*.5f,(Screen.height-height)*.5f,width,height),GUI.skin.box);
                GUILayout.Label("DESIGN MODE",new GUIStyle(GUI.skin.label){fontSize=24,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter});
                GUILayout.Label("Choose a level to test without a headset.");
                GUILayout.Space(10);
                foreach(var level in DesignLevelCatalog.Levels)
                    if(GUILayout.Button(DesignLevelCatalog.Title(level),GUILayout.Height(42))) Navigation.EnterDesignLevel(level);
                GUILayout.Label("Changing sandbox courses resets the temporary sandbox build.");
                GUILayout.Space(8);
                GUILayout.Label("Edit blockouts in Scene view outside Play Mode and save.");
                GUILayout.Label("Each level opens with its test car ready.");
                if(GUILayout.Button("Return to normal modes",GUILayout.Height(30))) Navigation.TrySetDesignMode(false);
                GUILayout.Label("Switching modes resets temporary builds.");
                GUILayout.Label(Navigation.Status);
                GUILayout.EndArea();
            }
            GUI.enabled=previous;
        }
#endif
        static GameObject Box(string name,Transform parent,Vector3 position,Vector3 size,Material material,bool interactive)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(parent,false);
            go.transform.localPosition=position; go.transform.localScale=size; go.GetComponent<Renderer>().sharedMaterial=material;
            if(interactive) go.GetComponent<Collider>().isTrigger=true; else { go.GetComponent<Collider>().enabled=false; Destroy(go.GetComponent<Collider>()); }
            return go;
        }
        static TextMesh Label(Transform parent,string text,Vector3 position,float size,Color color,bool unscale=false)
        {
            var go=new GameObject(text); go.transform.SetParent(parent,false);
            if(unscale)
            {
                var scale=parent.localScale;
                go.transform.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
                go.transform.localPosition=new Vector3(position.x/scale.x,position.y/scale.y,position.z/scale.z);
            }
            else go.transform.localPosition=position;
            var label=go.AddComponent<TextMesh>(); label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            go.GetComponent<MeshRenderer>().sharedMaterial=label.font.material;
            label.text=text; label.characterSize=size*10f/64f; label.fontSize=64;
            label.anchor=TextAnchor.MiddleLeft; label.color=color;
            return label;
        }
    }
}

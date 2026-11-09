using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PhysicalAssembly
{
    [DefaultExecutionOrder(-100)]
    public sealed class WorkshopBootstrap : MonoBehaviour
    {
        public System.Action OpenGardenRequested;
        public System.Action OpenShadowRequested;
        public System.Action OpenNarrowGapRequested,ReturnToWorkshopRequested;
        public NarrowGapSettings ChallengeSettings;
        public NarrowGapController Challenge { get; private set; }
        public System.Action OpenDownhillRequested;
        public DownhillSettings DownhillSettings;
        public DownhillController Downhill { get; private set; }
        public WorkshopButton[] InteractionButtons => Arena.GetComponentsInChildren<WorkshopButton>(true);
        public IBuildModeTarget ModeTarget => Downhill ? Downhill : (IBuildModeTarget)Challenge;
        public bool IsChallenge => ChallengeSettings;
        public PartFactory Factory;
        public AssemblyWorld World;
        public WorkshopSession Session;
        public AssemblySaveService Saves;
        public Transform Arena;
        public BuildingSceneLayout Layout;
        public Transform Menu { get; private set; }
        float trackedFloorHeight;
        Pose lockedPlatformPose;
        bool calibrated;
        public float FloorHeight => trackedFloorHeight;
        public const float DrivingSurfaceOffset=.5f;
        public float DrivingSurfaceHeight => Layout && Layout.Surface?Layout.Surface.bounds.max.y:FloorHeight+DrivingSurfaceOffset;
        public float PlacementSurfaceHeight => Layout && Layout.Surface?Layout.Surface.bounds.max.y:Downhill?FloorHeight+DownhillSettings.SurfaceHeight:DrivingSurfaceHeight;
        bool buildingMenu;
        public bool SpawnOnStart=true;
        public bool RepositionRequested { get; private set; }
        TextMesh modeLabel,statusLabel,commandLabel,scoreLabel;
        LineRenderer preview;
        AttachmentGhost ghost;
        float nextPreview;
        AttachmentCandidate candidate;
        AssemblyFeedback feedback;
        AssemblyPart previewHeld;
        int previewTopology=-1,previewHoldVersion=-1;
        public AttachmentCandidate PreviewCandidate => candidate;
        GameObject buildStand;
        readonly List<GameObject> constructionEnvironment=new();
        WorkshopButton[] challengeButtons;
        void Awake()
        {
            Time.fixedDeltaTime=1f/60; Application.targetFrameRate=90;
            if(!World) World=gameObject.AddComponent<AssemblyWorld>();
            if(!Factory) Factory=gameObject.AddComponent<PartFactory>(); Factory.World=World;
            if(!Session) Session=gameObject.AddComponent<WorkshopSession>(); Session.World=World;
            if(!Saves) Saves=gameObject.AddComponent<AssemblySaveService>(); Saves.World=World; Saves.Factory=Factory; Saves.Session=Session;
            if(!Layout) CreateSceneLayout(false);
            Arena=Layout.transform; Arena.SetParent(null,true);
            Menu=Layout.Menu; buildStand=Layout.BuildStand;
            modeLabel=Layout.ModeLabel; statusLabel=Layout.StatusLabel;
            commandLabel=Layout.CommandLabel; scoreLabel=Layout.ScoreLabel;
            trackedFloorHeight=Arena.position.y; lockedPlatformPose=new Pose(Arena.position,Arena.rotation);
            Layout.HidePreviews();
            if(DownhillSettings)
            {
                Session.AllowDriving=false;
                Downhill=gameObject.AddComponent<DownhillController>(); Downhill.Initialize(this,DownhillSettings,Layout.Downhill);
            }
            else if(IsChallenge)
            {
                Session.AllowDriving=false;
                Challenge=gameObject.AddComponent<NarrowGapController>(); Challenge.Initialize(this,ChallengeSettings,Layout.NarrowGap);
                challengeButtons=Menu.GetComponentsInChildren<WorkshopButton>(true);
                UpdateChallengeButtons(); Challenge.Changed+=UpdateChallengeButtons;
            }
            ConfigureNavigationButtons(Arena);
            foreach(var button in InteractionButtons) BindButton(button);
            foreach(var target in Arena.GetComponentsInChildren<ToyTarget>(true)) target.Session=Session;
            feedback=gameObject.AddComponent<AssemblyFeedback>(); feedback.Workshop=this;
            var line=new GameObject("Attachment preview"); preview=line.AddComponent<LineRenderer>(); preview.positionCount=2; preview.startWidth=preview.endWidth=.005f;
            preview.sharedMaterial=Resources.Load<Material>("AssemblyPreview");
            if(!preview.sharedMaterial) preview.sharedMaterial=PartFactory.Material(new Color(.2f,1,.65f));
            preview.enabled=false;
            ghost=new GameObject("Snapped construction preview").AddComponent<AttachmentGhost>();
            World.TopologyChanged+=ClearPreview; World.HoldsChanged+=ClearPreview; Session.Changed+=ClearPreview;
        }
        void Start()
        {
            if(SpawnOnStart)
            {
                SpawnKit();
                if(DesignModeSession.Enabled && !IsChallenge) TryLoadDesignPreset();
            }
            if(!DesignModeSession.Enabled && PlayAreaPose.IsSet) ApplyPlayArea(PlayAreaPose.Pose);
            Debug.Log($"METAHACKTHONE_READY parts={World.Parts.Count} mode={Session.Mode}");
        }
        public static void ConfigureNavigationButtons(Transform arena)
        {
            foreach(var button in arena.GetComponentsInChildren<WorkshopButton>(true))
            {
                if(button.Command=="CloudGarden") button.Command="Main Menu";
                if(button.Command=="Shadow Workshop" || button.Command=="Narrow Gap" || button.Command=="Downhill Sandbox")
                { button.gameObject.SetActive(false); continue; }
                if(button.Command!="Main Menu" && button.Command!="Workshop") continue;
                button.name="Main Menu";
                foreach(var text in button.GetComponentsInChildren<TextMesh>(true)) text.text="Main Menu";
            }
        }
        // Explicit editor conversion and legacy/test fallback only. Never runs over an authored layout.
        public BuildingSceneLayout CreateSceneLayout(bool authored)
        {
            if(Layout) return Layout;
            if(!Arena) Arena=new GameObject("Editable building layout").transform;
            if(!Arena.GetComponent<WorkshopEnvironment>()) Arena.gameObject.AddComponent<WorkshopEnvironment>();
            Layout=Arena.gameObject.AddComponent<BuildingSceneLayout>(); Layout.Authored=authored;
            if(DownhillSettings)
            {
                Layout.Downhill=DownhillCourse.Build(DownhillSettings,Arena);
                BuildDownhillPanel();
            }
            else if(IsChallenge)
            {
                Layout.NarrowGap=!authored && ChallengeSettings.CoursePrefab?Instantiate(ChallengeSettings.CoursePrefab):NarrowGapCourse.Build(ChallengeSettings);
                Layout.NarrowGap.transform.SetParent(Arena,false);
                BuildChallengePanel();
            }
            else { BuildEnvironment(); BuildPanel(); }
            Layout.Menu=Menu; Layout.BuildStand=buildStand;
            Layout.ModeLabel=modeLabel; Layout.StatusLabel=statusLabel; Layout.CommandLabel=commandLabel; Layout.ScoreLabel=scoreLabel;
            Layout.Surface=DownhillSettings?Layout.Downhill.WorkbenchFrame.Find("Worktop").GetComponent<Collider>():IsChallenge?Layout.NarrowGap.transform.Find("Continuous tabletop collision").GetComponent<Collider>():Arena.Find("Arena").GetComponent<Collider>();
            Layout.CreateDefaultSpawns(DownhillSettings,IsChallenge);
            if(DownhillSettings)
            {
                var presentation=Layout.Downhill.gameObject.AddComponent<DownhillPresentation>();
                presentation.Initialize(Layout.Downhill,DownhillSettings,Layout); presentation.Configure();
            }
            return Layout;
        }
        void BindButton(WorkshopButton button)
        {
            if(DesignModeSession.Enabled && (button.Command=="Save construction" || button.Command=="Load construction"))
                button.gameObject.SetActive(false);
            button.Pressed=()=>
            {
                switch(button.Command)
                {
                    case "Downhill Start Return": Downhill.TrySwitchMode(); break;
                    case "Sandbox Retry": if(Downhill.TryRetry()) Downhill.PalmMenu?.Close(); break;
                    case "Sandbox Back": if(Downhill.TryReturn()) Downhill.PalmMenu?.Close(); break;
                    case "Sandbox Exit": Downhill.Exit(); break;
                    case "Sandbox Quit": Downhill.Exit(); break;
                    case "Sandbox Rotate Left": Downhill.DragBuilder?.RotateSelected(-1); break;
                    case "Sandbox Rotate Right": Downhill.DragBuilder?.RotateSelected(1); break;
                    case "Sandbox Next Level": Downhill.TryAdvanceLevel(); break;
                    case "Sandbox Example": Downhill.TryLoadDesignerExample(); break;
                    case "Sandbox Reset": Downhill.TryResetBuild(); break;
                    case "Drive / Build": Session.ToggleMode(); break;
                    case "Detach": if(Session.Mode==WorkshopMode.Build) World.TryDetachPart(Session.Selected); break;
                    case "Reset kit": ResetKit(); break;
                    case "Save construction": if(!DesignModeSession.Enabled) Saves.Save(); break;
                    case "Load construction": if(!DesignModeSession.Enabled) LoadCar(); break;
                    case "Move arena": ToggleReposition(); break;
                    case "+ Beam": SpawnExtra(PartKind.Beam); break;
                    case "+ Block": SpawnExtra(PartKind.Block); break;
                    case "+ Wheel": SpawnExtra(PartKind.Wheel); break;
                    case "+ Cube": SpawnExtra(PartKind.SmallCube); break;
                    case "+ Cuboid": SpawnExtra(PartKind.Cuboid); break;
                    case "+ Pyramid": SpawnExtra(PartKind.Pyramid); break;
                    case "+ Sphere": SpawnExtra(PartKind.PhysicsSphere); break;
                    case "+ Launcher": SpawnExtra(PartKind.Weapon); break;
                    case "Default Car": if(DesignModeSession.Enabled) TryLoadDesignPreset(); else CreateTestCar(); break;
                    case "CloudGarden": OpenGardenRequested?.Invoke(); break;
                    case "Shadow Workshop": OpenShadowRequested?.Invoke(); break;
                    case "Narrow Gap": OpenNarrowGapRequested?.Invoke(); break;
                    case "Downhill Sandbox": OpenDownhillRequested?.Invoke(); break;
                    case "Launch": Challenge.TryLaunch(); break;
                    case "Retry": Challenge.Retry(); break;
                    case "Build / Dock": if(Challenge.IsBuilding) Challenge.TryEnterLaunch(); else Challenge.TryEnterBuilding(); break;
                    case "Reset Level": Challenge.ResetLevel(); break;
                    case "Main Menu":
                    case "Workshop": if(Challenge) Challenge.CancelAim(); ReturnToWorkshopRequested?.Invoke(); break;
                }
            };
        }
        void BuildEnvironment()
        {
            Box("Arena",new Vector3(0,.73f,0),new Vector3(2.8f,.04f,2.4f),new Color(.055f,.08f,.12f));
            buildStand=Box("Assembly stand",new Vector3(0,.80f,.45f),new Vector3(.13f,.10f,.32f),new Color(.15f,.25f,.30f));
            Box("Far rail",new Vector3(0,.81f,1.18f),new Vector3(2.8f,.12f,.03f),new Color(.08f,.5f,.48f));
            Box("Left rail",new Vector3(-1.38f,.81f,0),new Vector3(.03f,.12f,2.4f),new Color(.08f,.5f,.48f));
            Box("Right rail",new Vector3(1.38f,.81f,0),new Vector3(.03f,.12f,2.4f),new Color(.08f,.5f,.48f));
            Box("Near rail",new Vector3(0,.77f,-1.18f),new Vector3(2.8f,.04f,.03f),new Color(.08f,.5f,.48f));
            for(int i=0;i<3;i++)
            {
                var target=Box("Target "+(i+1),new Vector3((i-1)*.55f,.94f,1.05f),new Vector3(.18f,.34f,.06f),new Color(1,.4f,.12f));
                target.AddComponent<ToyTarget>().Session=Session;
            }
            buildStand.SetActive(false);
            Label("BUILD  /  DRIVE  /  REBUILD",new Vector3(-1.25f,.752f,.98f),.035f,Quaternion.Euler(90,0,0),Color.white);
            // Arena origin stays at the tracked floor; driving surface floats 50 cm above it.
            foreach(Transform child in Arena) child.localPosition+=Vector3.up*(DrivingSurfaceOffset-.75f);
        }
        GameObject Box(string name,Vector3 position,Vector3 size,Color color)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(buildingMenu?Menu:Arena,false); go.transform.localPosition=position; go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=PartFactory.Material(color); constructionEnvironment.Add(go); return go;
        }
        TextMesh Label(string text,Vector3 position,float characterSize,Quaternion rotation,Color color)
        {
            var go=new GameObject(text); go.transform.SetParent(buildingMenu?Menu:Arena,false); go.transform.localPosition=position; go.transform.localRotation=rotation;
            var label=go.AddComponent<TextMesh>(); label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); go.GetComponent<MeshRenderer>().sharedMaterial=label.font.material;
            label.text=text; label.fontSize=64; label.characterSize=characterSize*10f/64f; label.color=color; label.anchor=TextAnchor.MiddleLeft;
            return label;
        }
        void BuildPanel()
        {
            Menu=new GameObject("Workshop menu").transform; Menu.SetParent(Arena,false); buildingMenu=true;
            // Move the outward-facing near-edge panel to the far edge: its front
            // now faces the player standing at the centre of the platform.
            Menu.localPosition=new Vector3(0,0,2.36f);
            Box("Control panel",new Vector3(0,1.03f,-1.18f),new Vector3(1.5f,.91f,.025f),new Color(.025f,.045f,.075f));
            Label("META HACKTHONE",new Vector3(-.69f,1.41f,-1.20f),.065f,Quaternion.identity,new Color(.25f,1,.8f));
            modeLabel=Label("BUILD",new Vector3(.38f,1.41f,-1.20f),.04f,Quaternion.identity,Color.white);
            Button("Drive / Build",new Vector3(-.49f,1.28f,-1.22f),()=>Session.ToggleMode());
            Button("Detach",new Vector3(0,1.28f,-1.22f),()=> { if(Session.Mode==WorkshopMode.Build) World.TryDetachPart(Session.Selected); });
            Button("Reset kit",new Vector3(.49f,1.28f,-1.22f),ResetKit);
            Button("Save construction",new Vector3(-.49f,1.16f,-1.22f),()=>Saves.Save());
            Button("Load construction",new Vector3(0,1.16f,-1.22f),LoadCar);
            Button("Move arena",new Vector3(.49f,1.16f,-1.22f),ToggleReposition);
            Button("+ Beam",new Vector3(-.49f,1.04f,-1.22f),()=>SpawnExtra(PartKind.Beam));
            Button("+ Block",new Vector3(0,1.04f,-1.22f),()=>SpawnExtra(PartKind.Block));
            Button("+ Wheel",new Vector3(.49f,1.04f,-1.22f),()=>SpawnExtra(PartKind.Wheel));
            Button("+ Cube",new Vector3(-.49f,.92f,-1.22f),()=>SpawnExtra(PartKind.SmallCube));
            Button("+ Cuboid",new Vector3(0,.92f,-1.22f),()=>SpawnExtra(PartKind.Cuboid));
            Button("+ Pyramid",new Vector3(.49f,.92f,-1.22f),()=>SpawnExtra(PartKind.Pyramid));
            Button("Default Car",new Vector3(-.49f,.80f,-1.22f),()=>CreateTestCar());
            Button("+ Sphere",new Vector3(0,.80f,-1.22f),()=>SpawnExtra(PartKind.PhysicsSphere));
            Button("+ Launcher",new Vector3(.49f,.80f,-1.22f),()=>SpawnExtra(PartKind.Weapon));
            statusLabel=Label("",new Vector3(-.7f,.66f,-1.21f),.028f,Quaternion.identity,Color.white);
            commandLabel=Label("",new Vector3(-1.32f,.79f,-1.1f),.023f,Quaternion.Euler(75,0,0),new Color(.6f,.95f,.88f));
            scoreLabel=Label("",new Vector3(.85f,.79f,-1.1f),.028f,Quaternion.Euler(75,0,0),Color.white);
            Label("RIGHT: index = forward | index + middle = reverse\nLEFT: index = left | index + middle = right\nLEFT: index + middle + thumb = fire",new Vector3(-.7f,1.62f,-1.1f),.032f,Quaternion.identity,Color.white);
            Button("Main Menu",new Vector3(0,.56f,-1.22f),()=>ReturnToWorkshopRequested?.Invoke());
            buildingMenu=false;
        }
        void BuildDownhillPanel()
        {
            Menu=new GameObject("Downhill controls").transform; Menu.SetParent(Arena,false); buildingMenu=true;
            Menu.localPosition=new Vector3(-.94f,0,.12f);
            Button("Workshop",new Vector3(0,1.09f,0),()=>ReturnToWorkshopRequested?.Invoke());
            Button("Move arena",new Vector3(0,.98f,0),ToggleReposition);
            Label(DownhillController.BuildInstructions,new Vector3(-.22f,1.30f,0),.018f,Quaternion.identity,new Color(1,.88f,.66f));
            statusLabel=Layout.Downhill.StatusLabel;
            buildingMenu=false;
        }
        void BuildChallengePanel()
        {
            Menu=new GameObject("Narrow Gap controls").transform; Menu.SetParent(Arena,false); buildingMenu=true;
            // Beside the loading pad, facing the player, away from the flight path.
            Menu.localPosition=new Vector3(-.79f,.096f,-.57f); Menu.localScale=Vector3.one*.8f;
            Box("Control panel",new Vector3(0,1.02f,0),new Vector3(1.12f,.80f,.035f),new Color(.16f,.085f,.045f));
            Label("NARROW GAP",new Vector3(-.50f,1.35f,-.027f),.058f,Quaternion.identity,new Color(1,.86f,.55f));
            modeLabel=Label("BUILD",new Vector3(.29f,1.35f,-.027f),.026f,Quaternion.identity,Color.white);
            Button("Launch",new Vector3(-.25f,1.23f,-.03f),()=>Challenge.TryLaunch());
            Button("Retry",new Vector3(.25f,1.23f,-.03f),()=>Challenge.Retry());
            Button("+ Cuboid",new Vector3(-.25f,1.11f,-.03f),()=>SpawnExtra(PartKind.Cuboid));
            Button("+ Cube",new Vector3(.25f,1.11f,-.03f),()=>SpawnExtra(PartKind.SmallCube));
            Button("+ Beam",new Vector3(-.25f,.99f,-.03f),()=>SpawnExtra(PartKind.Beam));
            Button("Build / Dock",new Vector3(.25f,.99f,-.03f),()=> { if(Challenge.IsBuilding) Challenge.TryEnterLaunch(); else Challenge.TryEnterBuilding(); });
            Button("Move arena",new Vector3(-.25f,.87f,-.03f),ToggleReposition);
            Button("Workshop",new Vector3(.25f,.87f,-.03f),()=> { Challenge.CancelAim(); ReturnToWorkshopRequested?.Invoke(); });
            Button("Reset Level",new Vector3(-.25f,.75f,-.03f),()=>Challenge.ResetLevel());
            Label("Clear build + refill kit",new Vector3(0,.75f,-.027f),.022f,Quaternion.identity,new Color(1,.86f,.55f));
            statusLabel=Label("",new Vector3(-.51f,.58f,-.027f),.026f,Quaternion.identity,new Color(1,.91f,.73f));
            buildingMenu=false;
        }
        void UpdateChallengeButtons()
        {
            foreach(var button in challengeButtons)
                button.Interactable=button.Command=="Workshop" || button.Command=="Reset Level" || (button.Command=="Retry"?Challenge.CanRetry:button.Command=="Launch"?Challenge.State==ChallengeState.LaunchReady:button.Command=="Build / Dock"?Challenge.IsBuilding||Challenge.State==ChallengeState.LaunchReady&&Challenge.RemainingBuildCycles>0:button.Command=="Move arena"?World.EditingEnabled:Challenge.IsBuilding);
        }
        void Button(string title,Vector3 pos,System.Action action)
        {
            var go=Box(title,pos,new Vector3(.43f,.09f,.025f),new Color(.09f,.21f,.25f)); go.GetComponent<Collider>().isTrigger=true; var button=go.AddComponent<WorkshopButton>(); button.Command=title; button.Pressed=action;
            var label=Label(title,pos+new Vector3(-.19f,0,-.018f),title.Length>14?.028f:.04f,Quaternion.identity,Color.white);
            label.transform.SetParent(go.transform,true);
        }
        public void SpawnExtra(PartKind kind)
        {
            if(Downhill) return;
            if(Session.Mode!=WorkshopMode.Build || World.IsBusy || !World.EditingEnabled) return;
            if(IsChallenge && kind!=PartKind.Cuboid && kind!=PartKind.SmallCube && kind!=PartKind.Beam) return;
            if(IsChallenge && !Challenge.IsBuilding) return;
            if(World.Parts.Count>=50) { Session.Notify("Workshop limit: 50 parts. Reset to clear space."); return; }
            var p=Factory.Create(kind,PartSpawnPoint(new Vector3(.65f,.28f,.2f)),Arena.rotation); Session.Select(p);
        }
        public void SpawnKit()
        {
            if(Downhill) { Downhill.SpawnKit(); return; }
            if(IsChallenge) { Challenge.SpawnKit(); return; }
            foreach(var spawn in Layout.Spawns)
            {
                var part=Factory.Create(spawn.Kind,spawn.transform.position,spawn.transform.rotation);
                if(spawn.Kind==PartKind.Chassis) Session.Select(part);
            }
        }
        Vector3 PartSpawnPoint(Vector3 surfaceRelative) => Arena.TransformPoint(surfaceRelative+Vector3.up*DrivingSurfaceOffset);
        public void LoadCar()
        {
            if(Downhill) return;
            if(!World.EditingEnabled || IsChallenge) return;
            if(World.IsBusy || World.HasHolds) { Session.Notify("Release parts and wait for attachment before loading."); return; }
            RepositionRequested=false;
            Saves.Load(new Pose(PartSpawnPoint(new Vector3(0,.19f,.45f)),Arena.rotation));
        }
        public bool TryLoadDesignPreset()
        {
            if(!DesignModeSession.Enabled || IsChallenge) return false;
            if(Downhill) return Downhill.TryLoadDesignPreset();
            var pose=Layout.DesignCarSpawn?new Pose(Layout.DesignCarSpawn.position,Layout.DesignCarSpawn.rotation)
                :new Pose(PartSpawnPoint(new Vector3(0,.16f,-.4f)),Arena.rotation);
            return CreateTestCar(pose,false);
        }
        public bool CreateTestCar() => CreateTestCar(new Pose(PartSpawnPoint(new Vector3(0,.16f,-.4f)),Arena.rotation),true);
        public bool CreateTestCar(Pose pose,bool startDriving)
        {
            if(Downhill) return false;
            if(!World.EditingEnabled || IsChallenge) return false;
            if(World.IsBusy || World.HasHolds) { Session.Notify("Release parts and wait for attachment before Quick Drive."); return false; }
            var oldParts=World.Parts.Values.ToArray(); var created=new List<AssemblyPart>();
            try
            {
                var chassis=Factory.Create(PartKind.Chassis,pose.position,pose.rotation); created.Add(chassis);
                foreach(var socket in chassis.Sockets.Where(s=>s.Family=="wheel" || s.Family=="weapon"))
                {
                    bool wheel=socket.Family=="wheel";
                    var rotation=pose.rotation*(wheel && socket.transform.localPosition.x<0?Quaternion.Euler(0,180,0):Quaternion.identity);
                    var part=Factory.Create(wheel?PartKind.Wheel:PartKind.Weapon,chassis.MainBody.Rigidbody.position,rotation); created.Add(part);
                    var mount=part.FindSocket("mount");
                    var offset=socket.transform.position-mount.transform.position;
                    foreach(var body in part.Bodies) body.Rigidbody.position+=offset;
                    Physics.SyncTransforms();
                    var connection=new ConnectionRecord {
                        id=System.Guid.NewGuid().ToString("N"), a=new BodyRef(part.InstanceId,mount.Body.Id), b=new BodyRef(chassis.InstanceId,socket.Body.Id),
                        socketA=mount.Id,socketB=socket.Id,anchorA=mount.transform.localPosition,anchorB=socket.transform.localPosition
                    };
                    if(!World.Connect(connection)) throw new System.InvalidOperationException("Could not connect test-car socket "+socket.Id);
                }
                Session.EnterBuild(); RepositionRequested=false;
                foreach(var part in oldParts) { World.UnregisterPart(part); part.gameObject.SetActive(false); Destroy(part.gameObject); }
                buildStand.SetActive(false); Session.Select(chassis); if(startDriving) Session.ToggleMode();
                Session.Notify(startDriving?"Default car ready to drive. Use hand gestures or W/S, A/D and Space. Build to modify it.":"Test car ready. Choose Build / Drive, then W/S to drive, A/D to steer, Space to fire.");
                return true;
            }
            catch(System.Exception error)
            {
                foreach(var part in created) { World.UnregisterPart(part); part.gameObject.SetActive(false); Destroy(part.gameObject); }
                Session.Notify("Quick Drive failed: "+error.Message); return false;
            }
        }
        public void ResetKit()
        {
            if(Downhill) { if(Downhill.IsBuilding) Downhill.TryResetBuild(); else Downhill.TryReturn(); return; }
            if(IsChallenge) { Challenge.Retry(); return; }
            if(!World.EditingEnabled) return;
            if(World.IsBusy) { Session.Notify("Wait for attachment to finish."); return; }
            Session.EnterBuild(); World.ReleaseAll(); RepositionRequested=false;
            buildStand.SetActive(false);
            foreach(var p in World.Parts.Values.ToArray()) { World.UnregisterPart(p); p.gameObject.SetActive(false); Destroy(p.gameObject); }
            SpawnKit(); Session.Notify("Fresh parts ready. Gently grip a part, then open your hand near a connection point.");
        }
        public void ToggleReposition()
        {
            if(!World.EditingEnabled) { Session.Notify("Retry before moving the course."); return; }
            if(RepositionRequested) { RepositionRequested=false; Session.Notify("Arena move cancelled."); return; }
            if(World.IsBusy||World.HasHolds) { Session.Notify("Release parts before moving the arena."); return; }
            if(IsChallenge) Challenge.CancelAim();
            Session.EnterBuild(); RepositionRequested=true;
            Session.Notify("Release your pinch, aim at the floor outline, then pinch. Select Move arena again to cancel.");
        }
        // Locked campaign parts are absent from World.Parts but still belong to
        // the workbench. They must follow room placement before being unlocked.
        System.Collections.Generic.IEnumerable<AssemblyPart> PlacementParts => Downhill
            ? World.Parts.Values.Concat(Downhill.Kit).Where(p=>p).Distinct()
            : World.Parts.Values;
        public void PlaceArena(Vector3 point,Quaternion rotation)
        {
            if(!World.EditingEnabled || !RepositionRequested || World.IsBusy || World.HasHolds) return;
            if(IsChallenge) Challenge.CancelAim();
            point.y=FloorHeight;
            var forward=Vector3.ProjectOnPlane(rotation*Vector3.forward,Vector3.up);
            rotation=Quaternion.LookRotation(forward.sqrMagnitude>.01f?forward:Vector3.forward);
            var previous=Arena.position; var delta=rotation*Quaternion.Inverse(Arena.rotation);
            foreach(var p in PlacementParts) foreach(var b in p.Bodies)
            {
                var position=b.gameObject.activeInHierarchy?b.Rigidbody.position:b.transform.position;
                var orientation=b.gameObject.activeInHierarchy?b.Rigidbody.rotation:b.transform.rotation;
                position=point+delta*(position-previous);orientation=delta*orientation;
                b.transform.SetPositionAndRotation(position,orientation);
                b.Rigidbody.position=position;b.Rigidbody.rotation=orientation;
                if(!b.Rigidbody.isKinematic) { b.Rigidbody.linearVelocity=Vector3.zero; b.Rigidbody.angularVelocity=Vector3.zero; }
            }
            lockedPlatformPose=new Pose(point,rotation);
            Arena.SetPositionAndRotation(point,rotation); Physics.SyncTransforms(); RepositionRequested=false; Session.Notify($"Arena locked. Worktop is {(PlacementSurfaceHeight-FloorHeight)*100:F0} cm above the tracked floor.");
            if(IsChallenge) Challenge.ArenaMoved();
            Debug.Log($"METAHACKTHONE_PLATFORM_PLACED level={(Downhill?"Downhill":"Workshop")} origin={point:F4} yaw={rotation.eulerAngles.y:F2} floor={FloorHeight:F4} surface={PlacementSurfaceHeight:F4}");
        }
        public void PositionAroundPlayer(Transform head,float floorHeight=0)
        {
            if(calibrated) return;
            if(PlayAreaPose.IsSet) { ApplyPlayArea(PlayAreaPose.Pose); return; }
            var forward=Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.5f) forward=Vector3.forward;
            if(!Layout.Authored && !IsChallenge && !Downhill) Menu.localPosition=new Vector3(0,Mathf.Clamp(head.position.y-floorHeight-1.3f,0,.35f),2.36f);
            trackedFloorHeight=floorHeight;
            var origin=new Vector3(head.position.x,floorHeight,head.position.z)+((IsChallenge || Downhill)?forward*(Downhill?DownhillSettings.PlayerDistance:.85f):Vector3.zero);
            RepositionRequested=true; PlaceArena(origin,Quaternion.LookRotation(forward));
            calibrated=!RepositionRequested;
            if(Downhill) { Session.Notify(DownhillSettings.DesignerBuilder?DownhillController.DesignerInstructions:DownhillController.BuildInstructions); return; }
            Session.Notify(IsChallenge?"Build: thumb up 2s, then index + middle. Aim: index + thumb out 1s, then fold thumb to launch.":"Stand inside the platform. Pinch thumb and index to hold a part; separate them to release.");
        }
        // Also works while an attempt is suspended; unlike interactive placement it preserves motion.
        public void ApplyPlayArea(Pose pose)
        {
            if(World.HasHolds || World.IsBusy) return;
            var previous=Arena.position; var delta=pose.rotation*Quaternion.Inverse(Arena.rotation);
            var bodies=PlacementParts.SelectMany(p=>p.Bodies).Select(b=>b.Rigidbody)
                .Select(b=>(body:b,position:b.gameObject.activeInHierarchy?b.position:b.transform.position,rotation:b.gameObject.activeInHierarchy?b.rotation:b.transform.rotation,linear:b.isKinematic?Vector3.zero:b.linearVelocity,angular:b.isKinematic?Vector3.zero:b.angularVelocity,sleeping:b.IsSleeping())).ToArray();
            trackedFloorHeight=pose.position.y; lockedPlatformPose=pose; calibrated=true; RepositionRequested=false;
            Arena.SetPositionAndRotation(pose.position,pose.rotation);
            foreach(var state in bodies)
            {
                var position=pose.position+delta*(state.position-previous); var rotation=delta*state.rotation;
                state.body.transform.SetPositionAndRotation(position,rotation); state.body.position=position; state.body.rotation=rotation;
                if(!state.body.isKinematic) { state.body.linearVelocity=delta*state.linear; state.body.angularVelocity=delta*state.angular; if(state.sleeping) state.body.Sleep(); }
            }
            Physics.SyncTransforms(); ClearPreview();
            if(Challenge) Challenge.ArenaMoved();
        }
        void LateUpdate()
        {
            UpdatePreview();
            // Parts never own this transform. Only explicit placement updates the
            // stored horizontal pose; height and tilt remain locked to calibration.
            if(Arena.position!=lockedPlatformPose.position || Arena.rotation!=lockedPlatformPose.rotation)
            { Debug.LogWarning($"METAHACKTHONE_PLATFORM_CORRECTED from={Arena.position:F4} to={lockedPlatformPose.position:F4}"); Arena.SetPositionAndRotation(lockedPlatformPose.position,lockedPlatformPose.rotation); Physics.SyncTransforms(); }
        }
        void Update()
        {
            if(Downhill) { statusLabel.text=Wrap(Session.Status,38); return; }
            if(IsChallenge)
            {
                modeLabel.text=(Challenge.IsBuilding?"BUILD":Challenge.State==ChallengeState.LaunchReady?"LAUNCH":Challenge.Won?"SAVED!":"FLIGHT")+$"\n{Challenge.RemainingBuildCycles}/10 left";
                statusLabel.text=Wrap(Session.Status,60);
                if(Challenge.Course.ScoreLabel) Challenge.Course.ScoreLabel.text=$"{Challenge.SavedCount} / 5\nSAVED";
                return;
            }
            modeLabel.text=Session.Mode.ToString().ToUpperInvariant();
            if(Session.Mode==WorkshopMode.Drive) buildStand.SetActive(false);
            statusLabel.text=Wrap(Session.Status,72);
            commandLabel.text=$"L: {Session.Left}    R: {Session.Right}";
            scoreLabel.text=$"HITS  {Session.Score:00}";
        }
        public void UpdatePreview()
        {
            var held=World.HeldPart(0); if(!held) held=World.HeldPart(1); if(!held) held=World.HeldPart(2);
            if(Session.Mode!=WorkshopMode.Build || !held || World.IsBusy)
            {
                ClearPreview(); previewHeld=null;
                feedback.Show(Session.Mode==WorkshopMode.Build?Session.Selected:null,null); return;
            }
            if(held!=previewHeld || previewTopology!=World.TopologyVersion || previewHoldVersion!=World.HoldVersion || Time.unscaledTime>=nextPreview)
            {
                nextPreview=Time.unscaledTime+1f/30; previewHeld=held;
                previewTopology=World.TopologyVersion; previewHoldVersion=World.HoldVersion;
                candidate=World.Query(held);
            }
            var blocked=Downhill && !candidate.IsValid?World.BlockedCandidate(held):default;
            ghost.Show(candidate.IsValid?candidate:blocked,held,blocked.IsValid);
            preview.enabled=candidate.IsValid;
            if(candidate.IsValid) { preview.SetPosition(0,candidate.sourcePoint); preview.SetPosition(1,candidate.targetPoint); }
            feedback.Show(Session.Selected,candidate.IsValid?candidate.target.Part:null);
        }
        void ClearPreview()
        {
            candidate=default; nextPreview=0;
            if(ghost) ghost.Show(default);
            if(preview) preview.enabled=false;
        }
        void OnDestroy()
        {
            if(Challenge) Challenge.Changed-=UpdateChallengeButtons;
            if(World) { World.TopologyChanged-=ClearPreview; World.HoldsChanged-=ClearPreview; } if(Session) Session.Changed-=ClearPreview;
            if(ghost) Destroy(ghost.gameObject); if(preview) Destroy(preview.gameObject);
        }
        static string Wrap(string text,int width)
        {
            if(text.Length<=width) return text; int cut=text.LastIndexOf(' ',width); if(cut<1) cut=width;
            return text.Substring(0,cut)+"\n"+text.Substring(cut).TrimStart();
        }
    }
}




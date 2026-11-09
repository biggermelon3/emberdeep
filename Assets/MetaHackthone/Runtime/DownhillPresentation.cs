using UnityEngine;

namespace PhysicalAssembly
{
    // Owns presentation only. Parts remain under unscaled physics roots.
    public sealed class DownhillPresentation : MonoBehaviour
    {
        public Transform WorkbenchVisuals, BuildControls, TestControls, AssemblyAnchor, PaletteAnchor;
        public WorkshopButton ReadyButton, RetryButton, BackButton, ExitButton;
        public FloatingStartButton StartTile { get; private set; }
        public TextMesh Countdown;
        public SandboxInterface Interface;
        [SerializeField] Vector3 authoredSandboxPosition;
        [SerializeField] bool authored;
        bool capturedLayout;
        Vector3 rotationWheelOffset;
        DownhillCourse course;
        DownhillSettings settings;
        BuildingSceneLayout layout;
        Vector3 buildPosition;
        public float TestScale => DownhillSettings.VehicleScale*settings.PresentationScale;
        public float BuildScale => 1f;
        public Vector3 BuildPosition => buildPosition;
        public Vector3 TestPosition => buildPosition+Vector3.back*settings.WorkbenchApproach;

        public void Initialize(DownhillCourse value,DownhillSettings tuning,BuildingSceneLayout owner)
        {
            course=value; settings=tuning; layout=owner;
            if(!course.StartPoint) { course.StartPoint=Child("Simulation start",course.SandboxFrame); course.StartPoint.localPosition=settings.StartPosition; course.StartPoint.localRotation=settings.StartRotation; }
            if(!authored)
            {
                authoredSandboxPosition=course.SandboxFrame.localPosition;
                WorkbenchVisuals=Child("Workbench visuals",course.WorkbenchFrame);
                foreach(var child in GetChildren(course.WorkbenchFrame))
                    if(child!=WorkbenchVisuals && child!=course.RotationWheel.transform && child!=course.ModeButton.transform && child.name!="Part spawn markers")
                        child.SetParent(WorkbenchVisuals,true);
                AssemblyAnchor=Child("Assembly anchor",transform);
                PaletteAnchor=Child("Floating parts anchor",transform);
                BuildControls=Child("Build controls",transform);
                TestControls=Child("Test controls",transform);
                course.ModeButton.gameObject.SetActive(false);
                ReadyButton=Button(BuildControls,"START","Downhill Start Return",Vector3.zero);
                RetryButton=Button(TestControls,"RETRY","Sandbox Retry",Vector3.zero);
                BackButton=Button(TestControls,"BACK TO BUILD","Sandbox Back",Vector3.down*.15f);
                ExitButton=Button(TestControls,"EXIT","Sandbox Exit",Vector3.down*.30f);
                Countdown=Label("Countdown",transform,new Vector3(-.62f,1.43f,-.1f),.075f);
                course.RotationWheel.transform.SetParent(BuildControls,true);
                AddHazards();
                ResetSpawnMarkers();
                ArrangeBuildMenu();
                WorkbenchVisuals.localScale=new Vector3(settings.WorkbenchScale,1,settings.WorkbenchScale);
                BuildControls.localPosition=TestControls.localPosition=new Vector3(-.52f,1.20f,-.32f);
                Countdown.transform.localPosition=new Vector3(-.52f,1.76f,-.32f);
                course.StatusLabel.transform.SetParent(transform,false);
                course.StatusLabel.transform.localPosition=new Vector3(-.52f,1.48f,-.32f);
                course.StatusLabel.characterSize=.0032f; course.StatusLabel.color=Color.white;
                course.SandboxFrame.localScale=Vector3.one*settings.PresentationScale;
                course.SandboxFrame.localPosition=authoredSandboxPosition-Vector3.up*((settings.PresentationScale-1)*.71f)+settings.WorkbenchLevelOffset;
                ConfigureCircularBuildPad();
                authored=true;
            }
            course.ModeButton=ReadyButton;
            ConfigureStartButton(settings);
            // Legacy wheel interaction rotates independently and stays available off the workbench.
            if(Application.isPlaying && !settings.DesignerBuilder && course.RotationWheel.transform.parent!=BuildControls)
                course.RotationWheel.transform.SetParent(BuildControls,true);
            // Capture the saved scene once per instance. Rebinding and switching
            // layouts must not treat an animated/test position as authored data.
            if(!capturedLayout)
            {
                authoredSandboxPosition=course.SandboxFrame.localPosition;
                rotationWheelOffset=transform.InverseTransformPoint(course.RotationWheel.transform.position)-transform.InverseTransformPoint(AssemblyAnchor.position);
                capturedLayout=true;
            }
            if(!Interface) Interface=GetComponent<SandboxInterface>();
            if(!Interface) Interface=gameObject.AddComponent<SandboxInterface>();
            Interface.Initialize(this,course,layout,settings);
            EnsureGridPadGroup();
            if(settings.DesignerBuilder) CourseArchVisuals.EnsurePad(this);
        }
        public void ConfigureStartButton(DownhillSettings tuning)
        {
            StartTile=ReadyButton.GetComponent<FloatingStartButton>();
            if(!StartTile) StartTile=ReadyButton.gameObject.AddComponent<FloatingStartButton>();
            StartTile.Configure(tuning);
            PlaceStartButton(tuning);
        }
        Transform padPivot;
        bool padOffsetCaptured;
        Vector3 padFollowOffset;
        Quaternion padHeadingOffset;
        public Transform FindGridElement(string name) => WorkbenchVisuals.Find(name) ?? WorkbenchVisuals.Find("Core direction grid/"+name);
        public Transform EnsureGridPadGroup()
        {
            if(!padPivot) padPivot=WorkbenchVisuals.Find("Core direction grid");
            if(!padPivot) {
                var pad=FindGridElement("Centered grid pad"); if(!pad) return null;
                padPivot=Child("Core direction grid",WorkbenchVisuals);
                padPivot.SetPositionAndRotation(pad.position,GetComponent<DownhillCourse>().WorkbenchFrame.rotation);
            }
            foreach(var child in GetChildren(WorkbenchVisuals))
                if(child.name=="Centered grid pad" || child.name=="Grid X" || child.name=="Grid Z" || child.name=="Circular grid border" || child.name=="FRONT direction marking") child.SetParent(padPivot,true);
            var wheel=GetComponent<DownhillCourse>().RotationWheel;
            if(wheel && (!Application.isPlaying || settings && settings.DesignerBuilder) && wheel.transform.parent!=padPivot)
                wheel.transform.SetParent(padPivot,true);
            return padPivot;
        }
        public void FollowCore(AssemblyPart core)
        {
            if(!core || !course || !WorkbenchVisuals) return;
            if(!padPivot && !EnsureGridPadGroup()) return;
            var up=course.WorkbenchFrame.up;
            if(!padOffsetCaptured) {
                // Keep editor placement relative to the authored core spawn when following it.
                var origin=layout.Spawns!=null && layout.Spawns.Length>0 && layout.Spawns[0]?layout.Spawns[0].Pose.position:course.WorkbenchFrame.TransformPoint(settings.CorePosition);
                padFollowOffset=course.WorkbenchFrame.InverseTransformVector(Vector3.ProjectOnPlane(padPivot.position-origin,up));
                padHeadingOffset=Quaternion.Inverse(course.WorkbenchFrame.rotation)*padPivot.rotation;
                padOffsetCaptured=true;
            }
            var forward=Vector3.ProjectOnPlane(core.MainBody.Rigidbody.rotation*Vector3.forward,up);
            if(forward.sqrMagnitude<.000001f) return;
            padPivot.position+=Vector3.ProjectOnPlane(core.MainBody.Rigidbody.position+course.WorkbenchFrame.TransformVector(padFollowOffset)-padPivot.position,up);
            padPivot.rotation=Quaternion.LookRotation(forward,up)*padHeadingOffset;
        }
        public void ConfigureCircularBuildPad()
        {
            var level=GetComponent<DownhillCourse>();
            var pad=level.MakeGridCircular(WorkbenchVisuals);
            if(!pad) return;
            level.RotationWheel.transform.position=pad.position-level.WorkbenchFrame.up*.041f;
            rotationWheelOffset=transform.InverseTransformPoint(level.RotationWheel.transform.position)-transform.InverseTransformPoint(AssemblyAnchor.position);
            var front=FindGridElement("FRONT direction marking");
            if(front) front.position=pad.position+level.WorkbenchFrame.up*.005f-level.WorkbenchFrame.forward*(pad.lossyScale.z*.31f);
        }
        void PlaceStartButton(DownhillSettings tuning)
        {
            if(Interface && Interface.IsConfigured) return;
            // Use the level frame, not the sloped start anchor or tracked camera.
            var frame=GetComponent<DownhillCourse>().WorkbenchFrame;
            ReadyButton.transform.SetPositionAndRotation(
                AssemblyAnchor.position+frame.TransformVector(tuning.StartBuildOffset),
                frame.rotation*Quaternion.Euler(0,-15,0));
        }
        public void ArrangeBuildMenu()
        {
            if(!layout.Menu) return;
            layout.Menu.position=transform.TransformPoint(new Vector3(-.52f,-.04f,-.32f));
            foreach(var label in layout.Menu.GetComponentsInChildren<TextMesh>(true))
                if(!label.GetComponentInParent<WorkshopButton>()) label.gameObject.SetActive(false);
        }
        static Transform[] GetChildren(Transform parent)
        {
            var result=new Transform[parent.childCount]; for(int i=0;i<result.Length;i++) result[i]=parent.GetChild(i); return result;
        }
        public void Configure()
        {
            course.SandboxFrame.localPosition=authoredSandboxPosition;
            var pose=layout.Spawns!=null && layout.Spawns.Length>0 && layout.Spawns[0]?layout.Spawns[0].Pose:new Pose(course.WorkbenchFrame.TransformPoint(settings.CorePosition),course.WorkbenchFrame.rotation);
            AssemblyAnchor.SetPositionAndRotation(pose.position,pose.rotation);
            buildPosition=course.SandboxFrame.localPosition;
            PaletteAnchor.SetPositionAndRotation(AssemblyAnchor.position,course.WorkbenchFrame.rotation);
            var wheel=transform.InverseTransformPoint(AssemblyAnchor.position)+rotationWheelOffset;
            if(!padPivot || !course.RotationWheel.transform.IsChildOf(padPivot))
                course.RotationWheel.transform.position=transform.TransformPoint(wheel);
            PlaceStartButton(settings);
            if(Interface) Interface.Place(AssemblyAnchor.position,course.WorkbenchFrame.rotation);
            SetBuilding(true); SetCountdown(""); Physics.SyncTransforms();
        }
        public Pose SpawnPose(int index)
        {
            if(layout.Spawns!=null && index<layout.Spawns.Length && layout.Spawns[index]) return layout.Spawns[index].Pose;
            return DefaultSpawnPose(index);
        }
        public void ResetSpawnMarkers()
        {
            AssemblyAnchor.SetPositionAndRotation(course.WorkbenchFrame.TransformPoint(settings.CorePosition),course.WorkbenchFrame.rotation);
            PaletteAnchor.SetPositionAndRotation(AssemblyAnchor.position,course.WorkbenchFrame.rotation);
            if(layout.Spawns==null) return;
            for(int i=0;i<layout.Spawns.Length;i++)
            { if(!layout.Spawns[i]) continue; var pose=DefaultSpawnPose(i); layout.Spawns[i].transform.SetPositionAndRotation(pose.position,pose.rotation); }
        }
        Pose DefaultSpawnPose(int index)
        {
            if(index==0) return new Pose(AssemblyAnchor.position,AssemblyAnchor.rotation);
            var offsets=new[]{new Vector3(-.4f,.26f,-.14f),new Vector3(.4f,.26f,-.14f),new Vector3(-.17f,.26f,-.14f),new Vector3(.17f,.26f,-.14f),new Vector3(-.32f,-.04f,-.30f),new Vector3(0,-.04f,-.30f),new Vector3(.32f,-.04f,-.30f)};
            return new Pose(PaletteAnchor.TransformPoint(offsets[index-1]),PaletteAnchor.rotation*(index<5?Quaternion.Euler(0,90,0):Quaternion.identity));
        }
        public void SetBuilding(bool building)
        {
            WorkbenchVisuals.gameObject.SetActive(building);
            BuildControls.gameObject.SetActive(building); TestControls.gameObject.SetActive(!building);
            if(settings.DesignerBuilder) foreach(var label in course.SandboxFrame.GetComponentsInChildren<TextMesh>(true)) label.GetComponent<Renderer>().enabled=!building;
            if(layout.Menu) layout.Menu.gameObject.SetActive(building && !Interface);
            if(Interface) Interface.SetBuilding(building);
            if(layout.BuildStand) layout.BuildStand.SetActive(false);
            if(building) SetCountdown("");
        }
        public void SetCountdown(string text) { Countdown.text=text; Countdown.gameObject.SetActive(!string.IsNullOrEmpty(text)); }
        public void SetCoursePosition(Vector3 position) { course.SandboxFrame.localPosition=position; Physics.SyncTransforms(); }
        void AddHazards()
        {
            void Mark(string name,Vector3 position,Vector3 size,Color color)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(course.SandboxFrame,false);
                go.transform.localPosition=position; go.transform.localScale=size;
                var collider=go.GetComponent<Collider>(); collider.enabled=false;
                if(Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
                go.GetComponent<Renderer>().sharedMaterial=PartFactory.Material(color);
            }
            var amber=new Color(1,.56f,.06f);
            Mark("Gap approach marker",new Vector3(.493f,.824f,.65f),new Vector3(.012f,.004f,.64f),amber);
            Mark("Gap landing marker",new Vector3(.507f+settings.GapWidth,.824f,.65f),new Vector3(.012f,.004f,.64f),amber);
            var barrier=course.SandboxFrame.Find("Wheel size barrier");
            if(barrier) barrier.GetComponent<Renderer>().sharedMaterial=PartFactory.Material(new Color(.84f,.24f,.08f));
            for(int i=0;i<4;i++) Mark("Slope edge marker",new Vector3(-1.2f+i*.27f,1.23f-i*.0945f,.34f),new Vector3(.065f,.008f,.02f),amber);
        }
        static Transform Child(string name,Transform parent)
        { var t=new GameObject(name).transform; t.SetParent(parent,false); return t; }
        static WorkshopButton Button(Transform parent,string title,string command,Vector3 position)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=title; go.transform.SetParent(parent,false);
            go.transform.localPosition=position; go.transform.localScale=new Vector3(.34f,.115f,.025f);
            go.GetComponent<Collider>().isTrigger=true;
            go.GetComponent<Renderer>().sharedMaterial=PartFactory.Material(new Color(.055f,.30f,.32f));
            var label=Label(title,go.transform,new Vector3(0,0,-.55f),.025f);
            label.transform.localScale=new Vector3(1/.34f,1/.115f,1/.025f); label.text=title;
            var button=go.AddComponent<WorkshopButton>(); button.Command=command; return button;
        }
        static TextMesh Label(string name,Transform parent,Vector3 position,float size)
        {
            var go=Child(name,parent); go.localPosition=position;
            var label=go.gameObject.AddComponent<TextMesh>(); label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            go.GetComponent<Renderer>().sharedMaterial=label.font.material;
            label.fontSize=64; label.characterSize=size*10/64; label.anchor=TextAnchor.MiddleCenter; label.color=Color.white;
            return label;
        }
    }
}

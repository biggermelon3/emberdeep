using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PhysicalAssembly
{
    public sealed class DesktopInputAdapter : MonoBehaviour
    {
        public WorkshopBootstrap Workshop;
        public Camera View;
        public bool InputFocused { get; private set; }=true;
        float distance, orbitDistance=4.2f, yaw, pitch=40;
        Quaternion rotation;
        readonly SandboxPlaneDrag sandboxDrag=new();
        Vector3 pivot;
        bool cameraReady, heldWithRightButton, rightDragOrbits;
        WorkshopButton hoveredStart;
        GameObject hiddenGarageInterface;
        public bool IsDesktop => View && View.isActiveAndEnabled;
        float ToolbarScale => Mathf.Min(1,Screen.width/744f);
        public Rect ToolbarRect => new Rect(12,12,720,Workshop && Workshop.Downhill?(Workshop.Downhill.DragBuilder?136:104):218);
        public bool IsOverToolbar(Vector2 screenPoint) => (GetComponent<GravityGaragePanel>() && GetComponent<GravityGaragePanel>().Contains(screenPoint)) || ToolbarRect.Contains(new Vector2(screenPoint.x,Screen.height-screenPoint.y)/ToolbarScale);

        void Start()
        {
            if(!IsDesktop) return;
            FramePlatform();
        }
        void HoverStart(WorkshopButton next)
        {
            if(hoveredStart==next) return;
            if(hoveredStart) hoveredStart.SetInteractionState(false,false);
            hoveredStart=next;
            if(hoveredStart) hoveredStart.SetInteractionState(true,false);
        }
        public void FramePlatform()
        {
            RestoreGarageInterface();
            if(Workshop.Downhill && Workshop.Downhill.Settings.Level3D && Workshop.Downhill.Course3D && !Workshop.Downhill.IsBuilding){FrameGarage();return;}
            if(DesignModeSession.Enabled && Workshop.Layout)
            {
                var colliders=Workshop.Layout.GetComponentsInChildren<Collider>().Where(c=>c.enabled && !c.isTrigger && !c.GetComponentInParent<BuildingPartSpawn>()).ToArray();
                if(colliders.Length>0)
                {
                    var bounds=colliders[0].bounds; foreach(var collider in colliders) bounds.Encapsulate(collider.bounds);
                    pivot=bounds.center; yaw=Workshop.Arena.eulerAngles.y+(Workshop.Downhill?8:0); pitch=40;
                    float halfAngle=Mathf.Atan(Mathf.Tan(View.fieldOfView*Mathf.Deg2Rad*.5f)*Mathf.Min(1,View.aspect));
                    orbitDistance=Mathf.Max(1,bounds.extents.magnitude*1.25f/Mathf.Sin(halfAngle));
                    cameraReady=true; ApplyCamera(); return;
                }
            }
            if(Workshop.Downhill && Workshop.Downhill.DragBuilder) { pivot=Workshop.Downhill.Presentation.AssemblyAnchor.position; yaw=Workshop.Arena.eulerAngles.y; pitch=38; orbitDistance=2.3f; cameraReady=true; ApplyCamera(); return; }
            if(Workshop.Downhill)
            {
                pivot=Workshop.Arena.TransformPoint(new Vector3(.12f,.85f,.43f)); yaw=Workshop.Arena.eulerAngles.y+8; pitch=40; orbitDistance=4.5f; cameraReady=true; ApplyCamera(); return;
            }
            pivot=Workshop.Arena.TransformPoint(Workshop.IsChallenge?new Vector3(.1f,.85f,.95f):new Vector3(0,.85f,0));
            yaw=Workshop.Arena.eulerAngles.y+(Workshop.IsChallenge?25:0); pitch=Workshop.IsChallenge?36:40; orbitDistance=Workshop.IsChallenge?5.4f:4.2f; cameraReady=true; ApplyCamera();
        }
        public void FrameSelection()
        {
            if(Workshop.World.HasHolds) return;
            var selected=Workshop.Session.Selected;
            if(!selected) { FramePlatform(); return; }
            var bounds=new Bounds(selected.MainBody.Rigidbody.position,Vector3.zero);
            foreach(var part in Workshop.World.GetConnectedParts(selected.InstanceId))
                foreach(var body in part.Bodies) foreach(var collider in body.Colliders) if(collider) bounds.Encapsulate(collider.bounds);
            pivot=bounds.center;
            float halfAngle=Mathf.Atan(Mathf.Tan(View.fieldOfView*Mathf.Deg2Rad*.5f)*Mathf.Min(1,View.aspect));
            orbitDistance=Mathf.Clamp(bounds.extents.magnitude*1.5f/Mathf.Sin(halfAngle),.8f,8);
            cameraReady=true; ApplyCamera();
        }
        public void FrameGarage()
        {
            var valley=Workshop.Downhill?Workshop.Downhill.Course3D:null;
            if(valley && Workshop.Downhill.Settings.Level3D){
                pivot=valley.transform.TransformPoint(new Vector3(0,4,2));yaw=Workshop.Arena.eulerAngles.y-16;pitch=32;orbitDistance=valley.WorldScale*40;
                var card=Workshop.Downhill.Presentation.Interface;if(card && card.Root && card.Root.gameObject.activeSelf){hiddenGarageInterface=card.Root.gameObject;hiddenGarageInterface.SetActive(false);}
                cameraReady=true;ApplyCamera();return;
            }
            var course=Workshop.Downhill?Workshop.Downhill.GarageCourse:null;
            if(!course) return;
            if(!Workshop.Downhill.Settings.UseGravityGarage) {FramePlatform();return;}
            var bounds=course.VisualBounds;
            yaw=Workshop.Arena.eulerAngles.y;pitch=25;
            pivot=bounds.center+Quaternion.Euler(pitch,yaw,0)*Vector3.right*bounds.extents.magnitude*.4f;
            orbitDistance=Mathf.Max(.8f,bounds.extents.magnitude*2.6f);
            var ui=Workshop.Downhill.Presentation.Interface;
            if(ui && ui.Root && ui.Root.gameObject.activeSelf) {hiddenGarageInterface=ui.Root.gameObject;hiddenGarageInterface.SetActive(false);}
            cameraReady=true;ApplyCamera();
        }
        void RestoreGarageInterface() {if(hiddenGarageInterface)hiddenGarageInterface.SetActive(true);hiddenGarageInterface=null;}
        void ApplyCamera()
        {
            var look=Quaternion.Euler(pitch,yaw,0);
            View.transform.SetPositionAndRotation(pivot-look*Vector3.forward*orbitDistance,look);
        }
        public void SetInputFocus(bool focused)
        {
            if(InputFocused==focused) return;
            InputFocused=focused;
            if(!focused && Workshop && Workshop.World) { HoverStart(null); rightDragOrbits=false; Workshop.World.Release(2,false); if(Workshop.Downhill) Workshop.Downhill.EndRotation(2); Workshop.Session.ClearInput(); }
        }
        void OnApplicationFocus(bool focused) => SetInputFocus(focused);
        void OnDisable() { RestoreGarageInterface();SetInputFocus(false); }
        void OnEnable() { InputFocused=true; }
        void Update()
        {
            if(!IsDesktop || !InputFocused || Mouse.current==null || Keyboard.current==null) return;
            if(!cameraReady) FramePlatform();
            var mouse=Mouse.current; var keys=Keyboard.current; var world=Workshop.World; var session=Workshop.Session;
            // Input System 1.17 normalizes wheel notches to +/-1 by default.
            float scroll=mouse.scroll.ReadValue().y;
            if(InputSystem.settings.scrollDeltaBehavior==InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange &&
                (Application.platform==RuntimePlatform.WindowsEditor || Application.platform==RuntimePlatform.WindowsPlayer)) scroll/=120f;
            bool overToolbar=IsOverToolbar(mouse.position.ReadValue());
            if(mouse.rightButton.wasPressedThisFrame) rightDragOrbits=!overToolbar && !world.HasHolds && !Workshop.RepositionRequested;
            if(mouse.rightButton.wasReleasedThisFrame) rightDragOrbits=false;
            if(keys.escapeKey.wasPressedThisFrame)
            {
                rightDragOrbits=false; world.Release(2,false);
                if(Workshop.Downhill) Workshop.Downhill.EndRotation(2);
                if(Workshop.RepositionRequested) Workshop.ToggleReposition();
                return;
            }
            var ray=View.ScreenPointToRay(mouse.position.ReadValue());
            WorkshopButton start=null;
            if(!overToolbar && Physics.Raycast(ray,out var hoverHit,20,~0,QueryTriggerInteraction.Collide) && hoverHit.collider.GetComponent<FloatingStartButton>())
                start=hoverHit.collider.GetComponent<WorkshopButton>();
            HoverStart(start);
            if(Workshop.Downhill && Workshop.Downhill.Rotating)
            {
                var wheel=Workshop.Downhill.RotationWheel.transform;
                var plane=new Plane(Workshop.Downhill.Course.WorkbenchFrame.up,wheel.position);
                if(plane.Raycast(ray,out float wheelDistance)) Workshop.Downhill.Rotate(2,ray.GetPoint(wheelDistance));
                if(heldWithRightButton?mouse.rightButton.wasReleasedThisFrame:mouse.leftButton.wasReleasedThisFrame) Workshop.Downhill.EndRotation(2);
                return;
            }
            bool designer=Workshop.Downhill && Workshop.Downhill.DragBuilder;
            if(designer && !world.HasHolds && !overToolbar) {
                if(rightDragOrbits && mouse.rightButton.isPressed) yaw=Mathf.Clamp(yaw+mouse.delta.ReadValue().x*.2f,Workshop.Arena.eulerAngles.y-90,Workshop.Arena.eulerAngles.y+90);
                float maximumDistance=Workshop.Downhill.Settings.Level3D && Workshop.Downhill.Course3D && !Workshop.Downhill.IsBuilding?Mathf.Max(4,Workshop.Downhill.Course3D.WorldScale*45):4;
                orbitDistance=Mathf.Clamp(orbitDistance*Mathf.Exp(-scroll*.12f),.6f,maximumDistance); ApplyCamera(); ray=View.ScreenPointToRay(mouse.position.ReadValue());
            }
            // Resolve a right press before camera movement: a part drag owns the
            // gesture until release; an empty-space drag owns camera orbit.
            if((mouse.leftButton.wasPressedThisFrame || (!designer && mouse.rightButton.wasPressedThisFrame)) && !overToolbar && !world.HasHolds)
            {
                if(Workshop.RepositionRequested)
                {
                    var floor=new Plane(Vector3.up,Vector3.up*Workshop.FloorHeight);
                    if(mouse.leftButton.wasPressedThisFrame && floor.Raycast(ray,out float floorDistance)) Workshop.PlaceArena(ray.GetPoint(floorDistance),Workshop.Arena.rotation);
                    return;
                }
                if(Physics.Raycast(ray,out var hit,20,~0,QueryTriggerInteraction.Collide))
                {
                    var button=hit.collider.GetComponent<WorkshopButton>();
                    if(button) { rightDragOrbits=false; if(mouse.leftButton.wasPressedThisFrame && !world.IsBusy) button.Press(); return; }
                    var handle=hit.collider.GetComponentInParent<DownhillRotationWheel>();
                    if(handle)
                    {
                        distance=hit.distance; rotation=Quaternion.identity; rightDragOrbits=false;
                        var wheelPlane=new Plane(handle.Controller.Course.WorkbenchFrame.up,handle.transform.position);
                        if(wheelPlane.Raycast(ray,out float gripDistance) && handle.Controller.BeginRotation(2,ray.GetPoint(gripDistance))) heldWithRightButton=!mouse.leftButton.wasPressedThisFrame;
                        return;
                    }
                    var body=hit.collider.GetComponentInParent<AssemblyBody>();
                    if(body && body.Part)
                    {
                        session.Select(body.Part); distance=hit.distance; rotation=Quaternion.identity;
                        if(session.Mode==WorkshopMode.Build)
                        {
                            rightDragOrbits=false;
                            if(world.TryGrab(2,body.Part,new Pose(hit.point,rotation))) { heldWithRightButton=!mouse.leftButton.wasPressedThisFrame; if(designer) sandboxDrag.Begin(hit.point,Workshop.Downhill.Course.WorkbenchFrame.up,rotation); }
                        }
                    }
                }
            }
            if(!world.HasHolds && !overToolbar && !Workshop.Downhill)
            {
                var delta=mouse.delta.ReadValue();
                if(rightDragOrbits && mouse.rightButton.isPressed) { yaw+=delta.x*.2f; pitch=Mathf.Clamp(pitch-delta.y*.2f,10,85); }
                if(mouse.middleButton.isPressed) pivot-=(View.transform.right*delta.x+View.transform.up*delta.y)*orbitDistance*.0015f;
                orbitDistance=Mathf.Clamp(orbitDistance*Mathf.Exp(-scroll*.12f),.3f,8);
                if(keys.fKey.wasPressedThisFrame) FrameSelection();
                ApplyCamera();
                ray=View.ScreenPointToRay(mouse.position.ReadValue());
            }
            if(designer && DesignModeSession.Enabled && (keys.qKey.wasPressedThisFrame || keys.eKey.wasPressedThisFrame)) Workshop.Downhill.DragBuilder.RotateSelected(keys.eKey.wasPressedThisFrame?1:-1);
            if(designer && world.HeldPart(2)) {
                if(!overToolbar) {
                    world.SetHoldPose(2,sandboxDrag.Sample(ray));
                    world.DragBuilder.SetAim(2,new SandboxDragBuilder.Aim {ray=ray,camera=View,cursor=mouse.position.ReadValue(),maxDistance=20,angle=3});
                } else world.DragBuilder.ClearAim(2);
                if(mouse.leftButton.wasReleasedThisFrame) world.Release(2,!overToolbar);
            }
            else if(world.HeldPart(2))
            {
                if(!overToolbar)
                {
                    distance=Mathf.Clamp(distance+scroll*.12f,.3f,8);
                    float pitchStep=((keys.wKey.wasPressedThisFrame?1:0)-(keys.sKey.wasPressedThisFrame?1:0))*BuildingGeometry.SnapAngle;
                    float yawStep=((keys.dKey.wasPressedThisFrame?1:0)-(keys.aKey.wasPressedThisFrame?1:0))*BuildingGeometry.SnapAngle;
                    float rollStep=((keys.eKey.wasPressedThisFrame?1:0)-(keys.qKey.wasPressedThisFrame?1:0))*BuildingGeometry.SnapAngle;
                    rotation=Quaternion.AngleAxis(rollStep,Vector3.forward)*Quaternion.AngleAxis(yawStep,Vector3.up)*Quaternion.AngleAxis(pitchStep,Vector3.right)*rotation;
                    world.SetHoldPose(2,new Pose(ray.GetPoint(distance),rotation));
                }
                if(heldWithRightButton?mouse.rightButton.wasReleasedThisFrame:mouse.leftButton.wasReleasedThisFrame) world.Release(2,!overToolbar);
            }
            if(Workshop.Downhill)
            {
                if(keys.tabKey.wasPressedThisFrame && !Workshop.Downhill.GarageComparing) Workshop.Downhill.TrySwitchMode();
            }
            else if(Workshop.IsChallenge)
            {
                if(keys.spaceKey.wasPressedThisFrame && !world.HasHolds) Workshop.Challenge.TryLaunch();
                if(keys.rKey.wasPressedThisFrame && !world.HasHolds) Workshop.Challenge.Retry();
            }
            else if(keys.tabKey.wasPressedThisFrame) session.ToggleMode();
            if(keys.deleteKey.wasPressedThisFrame && session.Mode==WorkshopMode.Build) world.TryDetachPart(session.Selected);
            if(session.Mode==WorkshopMode.Drive)
            {
                session.Right=keys.wKey.isPressed?HandCommand.Forward:keys.sKey.isPressed?HandCommand.Reverse:HandCommand.None;
                session.Left=keys.spaceKey.isPressed?HandCommand.Fire:keys.aKey.isPressed?HandCommand.Left:keys.dKey.isPressed?HandCommand.Right:HandCommand.None;
            }
        }
        void OnGUI()
        {
            if(!IsDesktop || !Workshop) return;
            if(Workshop.Downhill) { DownhillGUI(); return; }
            if(Workshop.IsChallenge) { ChallengeGUI(); return; }
            var oldMatrix=GUI.matrix; GUI.matrix=Matrix4x4.Scale(Vector3.one*ToolbarScale);
            GUILayout.BeginArea(ToolbarRect,GUI.skin.box);
            GUILayout.Label($"{(DesignModeSession.Enabled?"DESIGN MODE / CAR WORKSHOP":"DESKTOP WORKSHOP")}   |   {Workshop.Session.Mode.ToString().ToUpperInvariant()}");
            GUILayout.BeginHorizontal();
            bool oldEnabled=GUI.enabled; GUI.enabled=InputFocused && !Workshop.World.HasHolds && !Workshop.World.IsBusy;
            if(GUILayout.Button(DesignModeSession.Enabled?"Reset Test Car":"Default Car - replaces kit",GUILayout.Height(28))) { if(DesignModeSession.Enabled?Workshop.TryLoadDesignPreset():Workshop.CreateTestCar()) FramePlatform(); }
            if(GUILayout.Button("Reset Parts",GUILayout.Height(28))) { Workshop.ResetKit(); FramePlatform(); }
            if(GUILayout.Button("Build / Drive",GUILayout.Height(28))) Workshop.Session.ToggleMode();
            bool ready=GUI.enabled; GUI.enabled=ready && Workshop.Session.Mode==WorkshopMode.Build;
            if(!DesignModeSession.Enabled && GUILayout.Button("Save",GUILayout.Height(28))) Workshop.Saves.Save();
            GUI.enabled=ready;
            if(!DesignModeSession.Enabled && GUILayout.Button("Load",GUILayout.Height(28))) { Workshop.LoadCar(); FrameSelection(); }
            if(GUILayout.Button("Overview",GUILayout.Height(28))) FramePlatform();
            GUI.enabled=oldEnabled; GUILayout.EndHorizontal();
            GUI.enabled=InputFocused && !Workshop.World.IsBusy && Workshop.Session.Mode==WorkshopMode.Build;
            GUILayout.BeginHorizontal();
            foreach(var kind in new[]{PartKind.SmallCube,PartKind.Cuboid,PartKind.Pyramid,PartKind.PhysicsSphere,PartKind.Wheel,PartKind.Weapon,PartKind.Beam,PartKind.Block})
                if(GUILayout.Button("+ "+(kind==PartKind.PhysicsSphere?"Sphere":kind.ToString()),GUILayout.Height(28))) Workshop.SpawnExtra(kind);
            GUILayout.EndHorizontal(); GUI.enabled=oldEnabled;
            GUILayout.BeginHorizontal(); GUI.enabled=ready;
            if(Workshop.ReturnToWorkshopRequested!=null && GUILayout.Button("Main Menu",GUILayout.Height(26))) Workshop.ReturnToWorkshopRequested();
            GUILayout.EndHorizontal(); GUI.enabled=oldEnabled;
            GUILayout.Label("BUILD: Left/right-drag part; release to attach | Wheel: down closer / up farther | Del: detach");
            GUILayout.Label("VIEW: Right-drag empty space: orbit | Middle-drag: pan | Wheel: zoom | F: frame selection");
            GUILayout.Label("DRIVE: W/S: throttle | A/D: steer | Space: fire | Tab: Build/Drive | Esc: release / cancel");
            GUILayout.Label(InputFocused?Workshop.Session.Status:"Click the Game view to use mouse and keyboard.");
            GUILayout.EndArea(); GUI.matrix=oldMatrix;
        }
        void DownhillGUI()
        {
            var old=GUI.matrix; bool enabled=GUI.enabled; GUI.matrix=Matrix4x4.Scale(Vector3.one*ToolbarScale);
            GUILayout.BeginArea(ToolbarRect,GUI.skin.box);
            var level=Workshop.Downhill;
            if(level.Settings.Level3D)GUILayout.Label(level.IsBuilding?"WATERFALL VALLEY: balance forward; aim the drill at the yellow band.":level.Run3D?level.Run3D.Feedback:"Gravity launch countdown");
            GUILayout.Label((DesignModeSession.Enabled?"DESIGN MODE / ":"")+"WORKBENCH BUILD"+"  /  "+level.State);
            GUI.enabled=InputFocused && !Workshop.World.HasHolds && !Workshop.World.IsBusy && !level.GarageComparing;
            GUILayout.BeginHorizontal();
            if(level.IsBuilding)
            {
                if(GUILayout.Button("START",GUILayout.Height(28))) level.TryStart();
                if(GUILayout.Button("RESET",GUILayout.Height(28))) level.TryResetBuild();
                if(DesignModeSession.Enabled && level.DragBuilder && GUILayout.Button("EXAMPLE",GUILayout.Height(28))) level.TryLoadDesignerExample();
                if(DesignModeSession.Enabled && GUILayout.Button("Reset Test Car",GUILayout.Height(28))) Workshop.TryLoadDesignPreset();
                if(GUILayout.Button("QUIT",GUILayout.Height(28))) level.Exit();
            }
            else
            {
                GUI.enabled&=level.State!=DownhillState.Transition;
                if(GUILayout.Button("RETRY",GUILayout.Height(28))) level.TryRetry();
                if(GUILayout.Button("BACK TO BUILD",GUILayout.Height(28))) level.TryReturn();
                GUI.enabled=InputFocused && !level.GarageComparing;
                if(GUILayout.Button("QUIT",GUILayout.Height(28))) level.Exit();
            }
            if(level.CanAdvanceLevel && GUILayout.Button("NEXT LEVEL",GUILayout.Height(28))) { level.TryAdvanceLevel(); FramePlatform(); }
#if UNITY_EDITOR
            if(DesignModeSession.CanEditTrack(level) && GUILayout.Button(new GUIContent("Edit Track","Stop the test and open the saved track in Unity's Scene view."),GUILayout.Height(28)))
                DesignModeSession.RequestTrackEdit(level);
#endif
            if(GUILayout.Button("Overview",GUILayout.Height(28))) FramePlatform();
            GUILayout.EndHorizontal();
            GUI.enabled=enabled;
            if(DesignModeSession.Enabled && level.DragBuilder && level.IsBuilding) {
                GUILayout.BeginHorizontal(); GUI.enabled=InputFocused && !Workshop.World.IsBusy;
                if(GUILayout.Button("LEFT 90 [Q]")) level.DragBuilder.RotateSelected(-1);
                if(GUILayout.Button("RIGHT 90 [E]")) level.DragBuilder.RotateSelected(1);
                GUILayout.EndHorizontal(); GUI.enabled=enabled;
            }
            GUILayout.Label(level.DragBuilder?(DesignModeSession.Enabled?"Left drag: build | Q/E: rotate | Right drag: view | Wheel: zoom":"Left drag: build | Right drag: view | Wheel: zoom"):"Drag parts / rotation wheel | Del: detach | Tab: test / return");
            GUILayout.EndArea(); GUI.matrix=old;
        }
        void ChallengeGUI()
        {
            var old=GUI.matrix; bool enabled=GUI.enabled; GUI.matrix=Matrix4x4.Scale(Vector3.one*ToolbarScale);
            GUILayout.BeginArea(ToolbarRect,GUI.skin.box);
            var challenge=Workshop.Challenge;
            GUILayout.Label($"LEVEL 02 / NARROW GAP     {challenge.State}     SAVED {challenge.SavedCount}/5");
            bool ready=InputFocused && !Workshop.World.HasHolds && !Workshop.World.IsBusy;
            GUILayout.BeginHorizontal(); GUI.enabled=ready && challenge.State==ChallengeState.LaunchReady;
            if(GUILayout.Button("Launch [Space]",GUILayout.Height(32))) challenge.TryLaunch();
            GUI.enabled=ready && challenge.CanSwitchMode;
            if(GUILayout.Button(challenge.IsBuilding?"Dock":"Build",GUILayout.Height(32))) { if(challenge.IsBuilding) challenge.TryEnterLaunch(); else challenge.TryEnterBuilding(); }
            GUI.enabled=ready && challenge.CanRetry;
            if(GUILayout.Button("Retry [R]",GUILayout.Height(32))) challenge.Retry();
            GUI.enabled=ready;
            if(GUILayout.Button("Reset Level",GUILayout.Height(32))) challenge.ResetLevel();
            if(GUILayout.Button("Overview",GUILayout.Height(32))) FramePlatform();
            if(GUILayout.Button("Main Menu",GUILayout.Height(32))) Workshop.ReturnToWorkshopRequested?.Invoke();
            GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); GUI.enabled=ready && challenge.IsBuilding;
            foreach(var kind in new[]{PartKind.Cuboid,PartKind.SmallCube,PartKind.Beam}) if(GUILayout.Button("+ "+kind,GUILayout.Height(26))) Workshop.SpawnExtra(kind);
            if(GUILayout.Button("Detach",GUILayout.Height(26))) Workshop.World.TryDetachPart(Workshop.Session.Selected);
            if(GUILayout.Button("Move arena",GUILayout.Height(26))) Workshop.ToggleReposition();
            GUILayout.EndHorizontal(); GUI.enabled=enabled;
            GUILayout.Label($"{challenge.RemainingBuildCycles}/10 building cycles left. Build around the gold core, dock, load five balls, then Launch.");
            GUILayout.Label("Drag parts | Wheel: depth | W/S, A/D, Q/E: rotate | Del: detach | F: frame");
            GUILayout.Label("Right-drag: orbit | Middle-drag: pan | Retry keeps your build; Reset Level clears it.");
            GUILayout.Label(Workshop.Session.Status); GUILayout.EndArea(); GUI.matrix=old;
        }
    }
}

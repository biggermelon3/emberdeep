using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

namespace PhysicalAssembly.Tests
{
    public sealed class DesktopTests
    {
        WorkshopBootstrap workshop;
        DesktopInputAdapter desktop;
        Mouse mouse; Keyboard keyboard;
        InputDevice[] physicalDevices;
        InputSettings.UpdateMode previousUpdateMode;
        InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
        InputSettings.BackgroundBehavior previousBackground;
        [UnitySetUp] public IEnumerator Setup()
        {
            yield return SceneManager.LoadSceneAsync("Workshop"); yield return null;
            workshop=Object.FindFirstObjectByType<WorkshopBootstrap>(); desktop=workshop.GetComponent<DesktopInputAdapter>();
            Assert.IsTrue(desktop.IsDesktop,"Editor must use desktop input even with an Android build target.");
            desktop.enabled=false; desktop.SetInputFocus(true);
            previousUpdateMode=InputSystem.settings.updateMode;
            previousEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            previousBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;
            physicalDevices=InputSystem.devices.Where(d=>d.enabled && (d is Mouse || d is Keyboard)).ToArray();
            foreach(var device in physicalDevices) InputSystem.DisableDevice(device);
            mouse=InputSystem.AddDevice<Mouse>(); keyboard=InputSystem.AddDevice<Keyboard>();
            Step(new MouseState { position=new Vector2(Screen.width*.5f,Screen.height*.3f) });
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            DesignModeSession.SetEnabled(false);
            if(mouse!=null) InputSystem.RemoveDevice(mouse);
            if(keyboard!=null) InputSystem.RemoveDevice(keyboard);
            if(physicalDevices!=null) foreach(var device in physicalDevices) if(device.added) InputSystem.EnableDevice(device);
            InputSystem.settings.updateMode=previousUpdateMode;
            InputSystem.settings.editorInputBehaviorInPlayMode=previousEditorInput;
            InputSystem.settings.backgroundBehavior=previousBackground;
            foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(root);
            yield return null;
        }
        void Step(MouseState mouseState,params Key[] keys)
        {
            desktop.SetInputFocus(true);
            InputSystem.QueueStateEvent(mouse,mouseState); InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
            InputSystem.Update(); desktop.SendMessage("Update"); Physics.SyncTransforms();
        }
        [UnityTest] public IEnumerator QuickDriveBuildsRealCarDrivesFiresAndRoundTrips()
        {
            Assert.IsTrue(workshop.CreateTestCar());
            Assert.AreEqual(6,workshop.World.Parts.Count); Assert.AreEqual(5,workshop.World.Connections.Count);
            Assert.AreEqual(WorkshopMode.Drive,workshop.Session.Mode);
            foreach(var edge in workshop.World.Connections)
            {
                var a=workshop.World.Resolve(edge.a); var b=workshop.World.Resolve(edge.b);
                Assert.Less(Vector3.Distance(a.Rigidbody.position+a.Rigidbody.rotation*edge.anchorA,b.Rigidbody.position+b.Rigidbody.rotation*edge.anchorB),.001f);
            }
            Assert.AreEqual(4,workshop.World.Parts.Values.Count(p=>p.Kind==PartKind.Wheel));
            Assert.AreEqual(4,Object.FindObjectsByType<HingeJoint>(FindObjectsSortMode.None).Length);
            yield return new WaitForSeconds(.5f);
            var car=workshop.Session.Vehicle; float start=car.MainBody.Rigidbody.position.z;
            var idle=new MouseState {position=new Vector2(Screen.width*.5f,Screen.height*.3f)};
            Step(idle,Key.W); yield return new WaitForSeconds(.8f);
            Assert.Greater(car.MainBody.Rigidbody.position.z,start+.03f);
            Step(idle,Key.S); yield return new WaitForSeconds(1.2f);
            Assert.Less(car.MainBody.Rigidbody.linearVelocity.z,0,"Reverse keyboard input must reverse the generated vehicle.");
            Step(idle,Key.Space); yield return new WaitForSeconds(.1f);
            Assert.IsNotEmpty(Object.FindObjectsByType<ToyProjectile>(FindObjectsInactive.Include,FindObjectsSortMode.None),$"mode={workshop.Session.Mode}, command={workshop.Session.Left}, focused={desktop.InputFocused}");
            Step(idle); Step(idle,Key.Tab);
            Assert.AreEqual(WorkshopMode.Build,workshop.Session.Mode);
            var save=JsonUtility.FromJson<AssemblySaveV1>(JsonUtility.ToJson(workshop.Saves.Capture(car)));
            workshop.ResetKit();
            Assert.AreEqual(15,workshop.World.Parts.Count); Assert.AreEqual(0,workshop.World.Connections.Count);
            foreach(var part in workshop.World.Parts.Values.ToArray()) { workshop.World.UnregisterPart(part); Object.Destroy(part.gameObject); }
            var restored=workshop.Saves.Restore(save,new Pose(new Vector3(0,.7f,-.4f),Quaternion.identity));
            Assert.AreEqual(6,restored.Count); Assert.AreEqual(5,workshop.World.Connections.Count);
            Assert.IsTrue(workshop.CreateTestCar()); Assert.IsTrue(workshop.CreateTestCar());
            Assert.AreEqual(6,workshop.World.Parts.Count); Assert.AreEqual(5,workshop.World.Connections.Count);
            workshop.Session.EnterBuild();
            car=workshop.Session.Selected;
            Assert.IsTrue(workshop.World.TryGrab(2,car,new Pose(car.MainBody.Rigidbody.position,Quaternion.identity)));
            Assert.IsFalse(workshop.CreateTestCar());
            desktop.SetInputFocus(false);
            Assert.IsFalse(workshop.World.HasHolds); Assert.AreEqual(HandCommand.None,workshop.Session.Right);
        }
        [UnityTest] public IEnumerator DefaultAndLoadedCarTurnWithKeyboardOnWorkshopPlatform()
        {
            var idle=new MouseState {position=new Vector2(Screen.width*.5f,Screen.height*.3f)};
            foreach(bool reload in new[]{false,true}) foreach(var throttle in new[]{Key.W,Key.S}) foreach(var steering in new[]{Key.A,Key.D})
            {
                Step(idle);
                Assert.IsTrue(workshop.CreateTestCar());
                workshop.Session.EnterBuild();
                var car=workshop.Session.Selected;
                var placement=new Pose(workshop.Arena.TransformPoint(new Vector3(0,.70f,0)),workshop.Arena.rotation);
                if(reload)
                {
                    var save=JsonUtility.FromJson<AssemblySaveV1>(JsonUtility.ToJson(workshop.Saves.Capture(car)));
                    foreach(var part in workshop.World.Parts.Values.ToArray()) { workshop.World.UnregisterPart(part); part.gameObject.SetActive(false); Object.Destroy(part.gameObject); }
                    car=workshop.Saves.Restore(save,placement).Single(p=>p.Kind==PartKind.Chassis);
                }
                else
                {
                    var grip=new Pose(car.MainBody.Rigidbody.position,car.MainBody.Rigidbody.rotation);
                    Assert.IsTrue(workshop.World.TryGrab(2,car,grip));
                    workshop.World.SetHoldPose(2,placement); workshop.World.Release(2,false);
                }
                yield return new WaitForSeconds(.5f);
                workshop.Session.Select(car); workshop.Session.ToggleMode();
                var start=car.MainBody.Rigidbody.position; var forward=car.MainBody.transform.forward;
                for(int step=0;step<90;step++)
                {
                    Step(idle,throttle,steering); yield return new WaitForFixedUpdate();
                    Assert.AreEqual(WorkshopMode.Drive,workshop.Session.Mode,workshop.Session.Status);
                    foreach(var edge in workshop.World.Connections)
                    {
                        var a=workshop.World.Resolve(edge.a).Rigidbody; var b=workshop.World.Resolve(edge.b).Rigidbody;
                        Assert.Less(Vector3.Distance(a.position+a.rotation*edge.anchorA,b.position+b.rotation*edge.anchorB),.025f);
                    }
                    foreach(var collider in workshop.World.Parts.Values.SelectMany(p=>p.Bodies).SelectMany(b=>b.Colliders))
                    {
                        var bounds=collider.bounds;
                        for(int corner=0;corner<8;corner++)
                        {
                            var point=workshop.Arena.InverseTransformPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1)));
                            Assert.Less(Mathf.Abs(point.x),1.34f,"Car must stay clear of side rails.");
                            Assert.Less(Mathf.Abs(point.z),1.14f,"Car must stay clear of end rails.");
                        }
                    }
                }
                float turn=Vector3.SignedAngle(forward,car.MainBody.transform.forward,Vector3.up)*(steering==Key.A?-1:1);
                float travel=Vector3.Dot(car.MainBody.Rigidbody.position-start,forward)*(throttle==Key.W?1:-1);
                Debug.Log($"Keyboard steering reload={reload} {throttle}+{steering}: {turn:F1} degrees, {travel:F3}m");
                Assert.GreaterOrEqual(turn,30f); Assert.Greater(travel,.1f);
                Assert.Greater(Vector3.Dot(car.MainBody.transform.up,Vector3.up),.85f);
                Step(idle); yield return new WaitForSeconds(.5f);
                Assert.Less(Mathf.Abs(car.MainBody.Rigidbody.angularVelocity.y),.25f,"Releasing steering should let yaw settle.");
            }
        }
        [UnityTest] public IEnumerator MouseBuildCameraToolbarAndFocusUseActualInput()
        {
            // Isolate a visible block away from the kit to make ray acquisition unambiguous.
            foreach(var part in workshop.World.Parts.Values.ToArray()) { workshop.World.UnregisterPart(part); part.gameObject.SetActive(false); Object.Destroy(part.gameObject); }
            var visiblePoint=new Vector2(Screen.width*.5f,Screen.height*.2f);
            var block=workshop.Factory.Create(PartKind.Block,desktop.View.ScreenPointToRay(visiblePoint).GetPoint(2),Quaternion.identity);
            block.MainBody.Rigidbody.useGravity=false; Physics.SyncTransforms();
            var pixel=(Vector2)desktop.View.WorldToScreenPoint(block.MainBody.Rigidbody.position);
            Assert.IsFalse(desktop.IsOverToolbar(pixel));
            var idle=new MouseState {position=pixel};
            Step(new MouseState {position=pixel}.WithButton(MouseButton.Left));
            Assert.AreSame(block,workshop.World.HeldPart(2));
            var initial=block.MainBody.Rigidbody.position; var camera=desktop.View.transform.position;
            Step(new MouseState {position=pixel+new Vector2(30,0),delta=new Vector2(30,0),scroll=new Vector2(0,1)}.WithButton(MouseButton.Left).WithButton(MouseButton.Right));
            Assert.Greater(Vector3.Distance(initial,block.MainBody.Rigidbody.position),.05f);
            Assert.AreEqual(camera,desktop.View.transform.position,"Orbit must not run while holding.");
            var rotationKeys=new[] {Key.W,Key.S,Key.A,Key.D,Key.Q,Key.E};
            var rotationAxes=new[] {Vector3.right,Vector3.right,Vector3.up,Vector3.up,Vector3.forward,Vector3.forward};
            var rotationAngles=new[] {45f,-45f,-45f,45f,-45f,45f};
            for(int i=0;i<rotationKeys.Length;i++)
            {
                Step(new MouseState {position=pixel}.WithButton(MouseButton.Left));
                var before=block.MainBody.Rigidbody.rotation;
                Step(new MouseState {position=pixel}.WithButton(MouseButton.Left),rotationKeys[i]);
                var expected=Quaternion.AngleAxis(rotationAngles[i],rotationAxes[i])*before;
                Assert.Less(Quaternion.Angle(expected,block.MainBody.Rigidbody.rotation),.1f,rotationKeys[i].ToString());
                Assert.AreSame(block,workshop.World.HeldPart(2));
                Assert.AreEqual(camera,desktop.View.transform.position);
            }
            var target=workshop.Factory.Create(PartKind.Block,block.MainBody.Rigidbody.position+Vector3.right*.12f,Quaternion.identity);
            target.MainBody.Rigidbody.useGravity=false;
            Step(idle); Assert.IsFalse(workshop.World.HasHolds);
            yield return new WaitForSeconds(.35f);
            Assert.AreEqual(1,workshop.World.Connections.Count,"Mouse release must use the real attachment pipeline.");
            Step(idle,Key.Delete); Assert.AreEqual(0,workshop.World.Connections.Count);
            Assert.IsTrue(workshop.World.TryGrab(2,block,new Pose(block.MainBody.Rigidbody.position,Quaternion.identity)));
            Step(idle,Key.Escape); Assert.IsFalse(workshop.World.HasHolds); Assert.AreEqual(0,workshop.World.Connections.Count);
            var platform=workshop.Arena.position; var partPosition=block.MainBody.Rigidbody.position;
            Step(new MouseState {position=new Vector2(Screen.width*.95f,Screen.height*.1f),delta=new Vector2(45,20)}.WithButton(MouseButton.Right));
            Assert.Greater(Vector3.Distance(camera,desktop.View.transform.position),.1f);
            Step(new MouseState {position=pixel,delta=new Vector2(20,10)}.WithButton(MouseButton.Middle));
            Step(new MouseState {position=pixel,scroll=new Vector2(0,1)});
            Step(idle,Key.F);
            Assert.AreEqual(platform,workshop.Arena.position); Assert.AreEqual(partPosition,block.MainBody.Rigidbody.position);
            // Put a collider directly behind the toolbar. A left press must not acquire it.
            var toolbarPoint=new Vector2(Screen.width*.1f,Screen.height-30*Mathf.Min(1,Screen.width/744f));
            Assert.IsTrue(desktop.IsOverToolbar(toolbarPoint));
            block.MainBody.Rigidbody.position=desktop.View.ScreenPointToRay(toolbarPoint).GetPoint(1); Physics.SyncTransforms();
            Step(new MouseState {position=toolbarPoint}.WithButton(MouseButton.Left));
            Assert.IsFalse(workshop.World.HasHolds);
            workshop.World.TryGrab(2,block,new Pose(block.MainBody.Rigidbody.position,Quaternion.identity));
            workshop.Session.Right=HandCommand.Forward; desktop.SetInputFocus(false);
            Assert.IsFalse(workshop.World.HasHolds); Assert.AreEqual(HandCommand.None,workshop.Session.Right);
            yield return null;
        }
        [UnityTest] public IEnumerator DesignerDragRotationAndSocketReleaseUseActualMouseInput()
        {
            yield return SceneManager.LoadSceneAsync("DownhillSandbox"); yield return null;
            workshop=Object.FindFirstObjectByType<WorkshopBootstrap>(); desktop=workshop.GetComponent<DesktopInputAdapter>();
            desktop.enabled=false; desktop.SetInputFocus(true); desktop.FramePlatform();
            DesignModeSession.SetEnabled(true);
            var level=workshop.Downhill; var world=workshop.World; var part=level.Kit[2];
            var pixel=(Vector2)desktop.View.WorldToScreenPoint(part.MainBody.Rigidbody.position);
            Assert.IsFalse(desktop.IsOverToolbar(pixel)); Step(new MouseState {position=pixel});
            Step(new MouseState {position=pixel}.WithButton(MouseButton.Left)); Assert.AreSame(part,world.HeldPart(2));
            var before=part.MainBody.Rigidbody.rotation;
            Step(new MouseState {position=pixel}.WithButton(MouseButton.Left),Key.E);
            Assert.That(Quaternion.Angle(before,part.MainBody.Rigidbody.rotation),Is.EqualTo(90).Within(.1f));
            var socket=level.Core.Sockets.First(s=>Vector3.Dot(s.Normal,level.Course.WorkbenchFrame.up)>.99f);
            var target=(Vector2)desktop.View.WorldToScreenPoint(socket.WorldPosition);
            Step(new MouseState {position=target}.WithButton(MouseButton.Left)); yield return new WaitForFixedUpdate();
            var candidate=world.Query(part); Assert.IsTrue(candidate.IsValid); Assert.AreSame(socket,candidate.targetSocket);
            Step(new MouseState {position=target}); yield return null;
            Assert.IsFalse(world.HasHolds); Assert.AreEqual(1,world.Connections.Count);
            Assert.That(Vector3.Distance(part.MainBody.Rigidbody.position,candidate.SnappedPose.position),Is.LessThan(.001f));
        }
        [UnityTest] public IEnumerator DownhillBuildButtonsUseActualMouseInput()
        {
            yield return SceneManager.LoadSceneAsync("DownhillSandbox"); yield return null;
            workshop=Object.FindFirstObjectByType<WorkshopBootstrap>(); desktop=workshop.GetComponent<DesktopInputAdapter>();
            desktop.enabled=false; desktop.SetInputFocus(true);
            DesignModeSession.SetEnabled(true);
            workshop.Downhill.Presentation.Interface.RotateLeftButton.gameObject.SetActive(true);
            workshop.Downhill.Presentation.Interface.RotateRightButton.gameObject.SetActive(true);
            var level=workshop.Downhill; var pivot=level.Core.MainBody.Rigidbody.position;
            Assert.IsTrue(level.RotationWheel.gameObject.activeInHierarchy);
            var part=level.Kit[2]; workshop.Session.Select(part);
            var before=part.MainBody.Rigidbody.rotation;
            var coreRotation=level.Core.MainBody.Rigidbody.rotation;
            foreach(var rotate in new[]{level.Presentation.Interface.RotateRightButton,level.Presentation.Interface.RotateLeftButton}) {
                var screen=(Vector2)desktop.View.WorldToScreenPoint(rotate.transform.position);
                Assert.IsFalse(desktop.IsOverToolbar(screen));
                Step(new MouseState {position=screen});
                Step(new MouseState {position=screen}.WithButton(MouseButton.Left));
                if(rotate==level.Presentation.Interface.RotateRightButton)
                    Assert.That(Quaternion.Angle(before,part.MainBody.Rigidbody.rotation),Is.EqualTo(90).Within(.2f));
                else Assert.That(Quaternion.Angle(before,part.MainBody.Rigidbody.rotation),Is.LessThan(.2f));
                Assert.That(Vector3.Distance(pivot,level.Core.MainBody.Rigidbody.position),Is.LessThan(.0001f));
                Assert.That(Quaternion.Angle(coreRotation,level.Core.MainBody.Rigidbody.rotation),Is.LessThan(.1f));
            }
            desktop.SetInputFocus(false); Assert.IsFalse(workshop.World.HasHolds);
            var button=level.Course.ModeButton;
            Assert.Contains(button,workshop.InteractionButtons);
            var native=button.GetComponent<PhysicalAssembly.MetaXR.MetaMenuButton>();
            Assert.NotNull(native); Assert.NotNull(native.Poke); Assert.NotNull(native.Ray);
            var pixel=(Vector2)desktop.View.WorldToScreenPoint(button.transform.position);
            Assert.IsFalse(desktop.IsOverToolbar(pixel));
            Step(new MouseState { position=pixel });
            Step(new MouseState { position=pixel }.WithButton(MouseButton.Left));
            Assert.IsFalse(level.IsBuilding);
            Step(new MouseState { position=pixel });
            yield return new WaitForSecondsRealtime(level.Settings.TransitionDuration+.1f);
            pixel=(Vector2)desktop.View.WorldToScreenPoint(level.Presentation.BackButton.transform.position);
            Step(new MouseState { position=pixel });
            Step(new MouseState { position=pixel }.WithButton(MouseButton.Left));
            yield return new WaitForSecondsRealtime(level.Settings.TransitionDuration+.1f);
            Assert.IsTrue(level.IsBuilding);
        }

        [UnityTest] public IEnumerator RightDragWheelMovesPartWithoutMovingCamera()
        {
            foreach(var part in workshop.World.Parts.Values.ToArray()) { workshop.World.UnregisterPart(part); part.gameObject.SetActive(false); Object.Destroy(part.gameObject); }
            var visiblePoint=new Vector2(Screen.width*.5f,Screen.height*.2f);
            var block=workshop.Factory.Create(PartKind.Block,desktop.View.ScreenPointToRay(visiblePoint).GetPoint(2),Quaternion.identity);
            block.MainBody.Rigidbody.useGravity=false; Physics.SyncTransforms();
            var pixel=(Vector2)desktop.View.WorldToScreenPoint(block.MainBody.Rigidbody.position);
            var cameraPosition=desktop.View.transform.position; var cameraRotation=desktop.View.transform.rotation;
            var start=block.MainBody.Rigidbody.position; var direction=desktop.View.ScreenPointToRay(pixel).direction;
            Step(new MouseState {position=pixel,delta=new Vector2(25,10),scroll=new Vector2(0,1)}.WithButton(MouseButton.Right));
            Physics.Raycast(desktop.View.ScreenPointToRay(pixel),out var debugHit,20,~0,QueryTriggerInteraction.Collide);
            Assert.AreSame(block,workshop.World.HeldPart(2),$"pixel={pixel}, toolbar={desktop.IsOverToolbar(pixel)}, mode={workshop.Session.Mode}, hit={debugHit.collider}, currentMouse={Mouse.current==mouse}, down={mouse.rightButton.wasPressedThisFrame}, focus={desktop.InputFocused}");
            Assert.Greater(Vector3.Dot(block.MainBody.Rigidbody.position-start,direction),.1f,"Wheel up moves the held object farther away.");
            var farPosition=block.MainBody.Rigidbody.position;
            Step(new MouseState {position=pixel,scroll=new Vector2(0,-1)}.WithButton(MouseButton.Right));
            Assert.Less(Vector3.Dot(block.MainBody.Rigidbody.position-farPosition,direction),-.1f,"Wheel down moves the held object closer.");
            Step(new MouseState {position=pixel}.WithButton(MouseButton.Right).WithButton(MouseButton.Left));
            Step(new MouseState {position=pixel}.WithButton(MouseButton.Right));
            Assert.AreSame(block,workshop.World.HeldPart(2),"Releasing the other button must not drop a right-button hold.");
            Assert.AreEqual(cameraPosition,desktop.View.transform.position);
            Assert.AreEqual(cameraRotation,desktop.View.transform.rotation);
            Step(new MouseState {position=pixel});
            Assert.IsFalse(workshop.World.HasHolds,"Releasing right ends the right-button grab.");
            Step(new MouseState {position=new Vector2(Screen.width*.95f,Screen.height*.1f),delta=new Vector2(40,0)}.WithButton(MouseButton.Right));
            Assert.IsFalse(workshop.World.HasHolds);
            Assert.Greater(Quaternion.Angle(cameraRotation,desktop.View.transform.rotation),1,"Right-dragging empty space still orbits.");
            yield return null;
        }
    }
}

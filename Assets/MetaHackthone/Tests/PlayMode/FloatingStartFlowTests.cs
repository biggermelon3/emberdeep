using System.Collections;
using NUnit.Framework;
using Oculus.Interaction;
using PhysicalAssembly.MetaXR;
using UnityEngine;
using UnityEngine.TestTools;

namespace PhysicalAssembly.Tests
{
    public sealed partial class DownhillTests
    {
        [UnityTest] public IEnumerator FloatingStartAcceptsNativePokeInWorkbench()
        {
            var eye=new GameObject("Poke view"); eye.transform.SetParent(root.transform);
            var view=eye.AddComponent<Camera>(); view.fieldOfView=85; view.aspect=1.6f; eye.transform.position=new Vector3(0,1.6f,-.5f);
            var button=level.Presentation.ReadyButton;
            button.gameObject.AddComponent<MetaMenuButton>().Initialize(workshop);
            settings.TransitionDuration=0;

                yield return null; yield return null;
            var fingertip=new GameObject("Synthetic fingertip"); fingertip.transform.SetParent(root.transform);
            var face=button.transform.position; var forward=button.transform.forward;
            fingertip.transform.position=face-forward*.12f;
            fingertip.AddComponent<PokeInteractor>().InjectAllPokeInteractor(fingertip.transform,.005f);
            yield return null; yield return null;
            for(int i=0;i<26 && level.IsBuilding;i++)
            {
                fingertip.transform.position=face-forward*(.12f-i*.005f);
                yield return null;
            }
            Assert.IsFalse(level.IsBuilding,"Approaching the native poke surface must press START.");
            Assert.IsFalse(button.gameObject.activeInHierarchy);
            fingertip.SetActive(false); Object.Destroy(fingertip);
            Assert.IsTrue(level.TryReturn()); yield return Until(()=>level.IsBuilding);
            yield return null; Assert.IsTrue(button.gameObject.activeInHierarchy);
            yield return new WaitForSeconds(.5f);
        }

        [UnityTest] public IEnumerator FloatingStartStaysAnchoredThroughHeadMovementAndTestReturnInWorkbench()
        {
            var cameraObject=new GameObject("Floating Start view"); cameraObject.transform.SetParent(root.transform);
            var view=cameraObject.AddComponent<Camera>(); view.fieldOfView=85; view.aspect=1.6f;
            view.transform.position=new Vector3(0,1.6f,-.5f);
            var button=level.Presentation.ReadyButton; var tile=level.Presentation.StartTile;
            var target=button.gameObject.AddComponent<MetaMenuButton>(); target.Initialize(workshop);
            Assert.NotNull(target.Ray); Assert.NotNull(target.Poke);
            var pointer=new GameObject("Start ray"); pointer.transform.SetParent(root.transform);
            var selector=pointer.AddComponent<MetaPinchSelector>();
            pointer.AddComponent<RayInteractor>().InjectAllRayInteractor(selector,pointer.transform);
            settings.TransitionDuration=.06f; settings.CountdownStep=.04f; settings.GoDuration=.02f;

                yield return null;
            var wheel=level.RotationWheel.transform.position; var course=level.Course.SandboxFrame.position;
            var anchored=new Pose(button.transform.position,button.transform.rotation);
            var controls=level.Presentation.TestControls; var testPosition=controls.position; var testRotation=controls.rotation;
            Assert.Less(Vector3.Distance(button.transform.position,level.Presentation.AssemblyAnchor.position+
                level.Course.WorkbenchFrame.TransformVector(settings.StartBuildOffset)),.00001f);
            // No hover: walking, looking away and changing FOV cannot move or billboard the UI.
            view.transform.SetPositionAndRotation(new Vector3(3,2,-4),Quaternion.Euler(35,130,20)); view.fieldOfView=50;
            yield return new WaitForSeconds(.35f);
            Assert.AreEqual(anchored.position,button.transform.position); Assert.AreEqual(anchored.rotation,button.transform.rotation);
            Assert.AreEqual(testPosition,controls.position); Assert.AreEqual(testRotation,controls.rotation);
            var cameraPose=new Pose(view.transform.position,view.transform.rotation);
            selector.ApplySample(true,.06f,false);
            pointer.transform.SetPositionAndRotation(button.transform.position-button.transform.forward*.3f,button.transform.rotation);
            yield return null; yield return null; yield return null;
            Assert.IsTrue(button.IsHovered); var frozen=button.transform.position;
            view.transform.position+=Vector3.right*.03f;
            yield return new WaitForSeconds(.06f);
            Assert.Less(Vector3.Distance(frozen,button.transform.position),.00001f);
            Assert.AreEqual(wheel,level.RotationWheel.transform.position); Assert.AreEqual(course,level.Course.SandboxFrame.position);
            // A held piece must still suppress the real SDK selection route.
            var part=level.Kit[5]; Assert.IsTrue(workshop.World.TryGrab(0,part,Grip(part)));
            selector.ApplySample(true,.02f,false); yield return null; yield return null;
            Assert.IsTrue(level.IsBuilding); workshop.World.ReleaseAll();
            selector.ApplySample(true,.06f,false); yield return null; yield return null;
            selector.ApplySample(true,.02f,false); yield return null; yield return null;
            Assert.IsFalse(level.IsBuilding); Assert.IsFalse(button.gameObject.activeInHierarchy);
            selector.ApplySample(true,.06f,false);
            yield return Until(()=>level.State==DownhillState.Running);
            Assert.IsTrue(level.TryReturn()); yield return Until(()=>level.IsBuilding);
            yield return null;
            Assert.IsTrue(button.gameObject.activeInHierarchy); Assert.AreSame(tile,level.Presentation.StartTile);
            Assert.AreEqual(anchored.position,button.transform.position); Assert.AreEqual(anchored.rotation,button.transform.rotation);
            Assert.AreEqual(testPosition,controls.position); Assert.AreEqual(testRotation,controls.rotation);
            Assert.AreEqual(cameraPose.rotation,view.transform.rotation);
            Assert.Less(Vector3.Distance(cameraPose.position+Vector3.right*.03f,view.transform.position),.0001f);
            yield return new WaitForSeconds(.5f);
        }
    }
}

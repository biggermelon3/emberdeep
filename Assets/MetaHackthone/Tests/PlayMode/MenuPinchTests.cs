using System.Collections;
using System.Linq;
using NUnit.Framework;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using PhysicalAssembly.MetaXR;
using UnityEngine;
using UnityEngine.TestTools;

namespace PhysicalAssembly.Tests
{
    public sealed class MenuPinchTests
    {
        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(root);
            yield return null;
        }
        [UnityTest] public IEnumerator JointSamplesDriveSDKRayMenuAndFloorPlacement()
        {
            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Workshop");
            yield return new WaitForSeconds(.4f);
            var workshop=Object.FindFirstObjectByType<WorkshopBootstrap>();
            var buttons=workshop.Menu.GetComponentsInChildren<MetaMenuButton>();
            var beam=buttons.Single(b=>b.name=="+ Beam");
            var move=buttons.Single(b=>b.name=="Move arena");
            var placement=workshop.GetComponent<MetaArenaPlacement>();
            var input=new GameObject("Test tracked pointer");
            var selector=input.AddComponent<MetaPinchSelector>();
            var ray=input.AddComponent<RayInteractor>(); ray.InjectAllRayInteractor(selector,input.transform);
            Aim(input.transform,beam.transform);
            yield return null; yield return null;
            Assert.IsTrue(beam.GetComponent<WorkshopButton>().IsHovered,"Real SDK ray must hover before pinching.");
            selector.ApplySample(true,.06f,false);
            selector.ApplySample(true,.02f,false);
            yield return null; yield return null;
            Assert.AreEqual(16,workshop.World.Parts.Count,"Joint-based pinch must travel through the SDK selector and ray into the button action.");
            selector.ApplySample(true,.035f,false);
            yield return null;
            Assert.AreEqual(16,workshop.World.Parts.Count);
            selector.ApplySample(true,.06f,false);
            yield return null; yield return null;
            Aim(input.transform,move.transform);
            yield return null; yield return null;
            selector.ApplySample(true,.02f,false);
            yield return null; yield return null;
            Assert.IsTrue(workshop.RepositionRequested);
            var original=workshop.Arena.position;
            // The clipped placement plane follows the active preview/head camera.
            // Choose a point inside that plane instead of assuming a fixed camera pose.
            var floorPoint=placement.Target.transform.position+Vector3.right*3;
            floorPoint.y=workshop.FloorHeight;
            input.transform.SetPositionAndRotation(floorPoint+Vector3.up,Quaternion.LookRotation(Vector3.down,Vector3.forward));
            yield return null; yield return null;
            Assert.AreEqual(original,workshop.Arena.position,"The pinch that opens placement must not immediately confirm it.");
            selector.ApplySample(true,.06f,false);
            yield return null; yield return null; yield return null;
            Assert.IsTrue(placement.HasPreview,"Pointing at the floor must display the placement outline.");
            selector.ApplySample(true,.02f,false);
            yield return null; yield return null;
            Assert.IsFalse(workshop.RepositionRequested);
            Assert.That(Vector3.Distance(workshop.Arena.position,floorPoint),Is.LessThan(.01f));
            Assert.IsFalse(placement.HasPreview);
            selector.ApplySample(true,.06f,false);
            yield return new WaitForSeconds(.5f);
            Aim(input.transform,move.transform);
            yield return null; yield return null;
            selector.ApplySample(true,.02f,false);
            yield return null; yield return null;
            Assert.IsTrue(workshop.RepositionRequested);
            selector.ApplySample(true,.06f,false);
            yield return new WaitForSeconds(.5f);
            selector.ApplySample(true,.02f,false);
            yield return null; yield return null;
            Assert.IsFalse(workshop.RepositionRequested,"Move arena must remain selectable to cancel placement.");
            selector.ApplySample(false,0,false);
            Object.Destroy(input);
            foreach(var part in workshop.World.Parts.Values.ToArray()) Object.Destroy(part.gameObject);
            foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(root);
            yield return null;
        }
        [UnityTest] public IEnumerator DownhillPlacementOutlineMatchesSeventyCentimeterWorkbench()
        {
            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("DownhillSandbox");
            yield return new WaitForSeconds(.4f);
            var workshop=Object.FindFirstObjectByType<WorkshopBootstrap>();
            var head=workshop.GetComponent<MetaHandInputAdapter>().DesktopCamera.transform;
            workshop.PositionAroundPlayer(head,.25f);
            workshop.ToggleReposition(); yield return null;
            var placement=workshop.GetComponent<MetaArenaPlacement>();
            var input=new GameObject("Downhill placement pointer");
            var selector=input.AddComponent<MetaPinchSelector>();
            var ray=input.AddComponent<RayInteractor>(); ray.InjectAllRayInteractor(selector,input.transform);
            var floorPoint=placement.Target.transform.position;
            input.transform.SetPositionAndRotation(floorPoint+Vector3.up,Quaternion.LookRotation(Vector3.down,Vector3.forward));
            selector.ApplySample(true,.06f,false);
            yield return null; yield return null; yield return null;
            Assert.IsTrue(placement.HasPreview);
            var outline=workshop.GetComponentsInChildren<LineRenderer>().Single(l=>l.name=="Arena placement outline");
            var top=workshop.Downhill.Course.GetComponentsInChildren<Collider>().Single(c=>c.name=="Worktop").bounds.max.y;
            Assert.That(top-workshop.FloorHeight,Is.EqualTo(.7f).Within(.0001f));
            for(int i=0;i<outline.positionCount;i++) Assert.That(outline.GetPosition(i).y,Is.EqualTo(top+.008f).Within(.0001f));
            Object.Destroy(input);
        }
        [UnityTest] public IEnumerator SharedPointerBindingRetainsLaunchSuppressionAfterRebinding()
        {
            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("NarrowGap");
            yield return null; yield return null;
            var workshop=Object.FindFirstObjectByType<WorkshopBootstrap>();
            var adapter=workshop.GetComponent<MetaHandInputAdapter>();
            var pointer=new GameObject("Shared pointer binding fixture");
            var hand=new PointerHand();
            var native=pointer.AddComponent<IndexPinchSelector>(); native.InjectHand(hand);
            var ray=pointer.AddComponent<RayInteractor>(); ray.InjectAllRayInteractor(native,pointer.transform);
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var rays=typeof(MetaHandInputAdapter).GetField("rays",flags);
            var original=rays.GetValue(adapter);
            try
            {
                rays.SetValue(adapter,new[]{ray});
                adapter.SendMessage("BindPointerPinches");
                var selector=pointer.GetComponent<MetaPinchSelector>(); Assert.NotNull(selector);
                Assert.NotNull(selector.SuppressSelection);
                int presses=0; selector.WhenSelected+=()=>presses++;
                selector.ApplySample(true,.06f,false); selector.ApplySample(true,.02f,true);
                Assert.AreEqual(1,presses);
                selector.ApplySample(true,.06f,false);
                Assert.IsTrue(workshop.Challenge.BeginAim());
                // Recovery must reuse the selector and reapply the workshop's input guard.
                selector.SuppressSelection=null; hand.Tracked=false;
                adapter.SendMessage("BindPointerPinches");
                Assert.AreSame(selector,MetaPinchSelector.BindToRay(ray));
                Assert.AreEqual(1,pointer.GetComponents<MetaPinchSelector>().Length);
                Assert.IsTrue(selector.SuppressSelection());
                selector.ApplySample(true,.02f,true); Assert.AreEqual(1,presses);
                workshop.Challenge.CancelAim();
                selector.ApplySample(true,.06f,false); selector.ApplySample(true,.02f,true);
                Assert.AreEqual(2,presses,"Releasing launch suppression must restore one selection event per pinch.");
            }
            finally { rays.SetValue(adapter,original); Object.DestroyImmediate(pointer); }
        }
        // Supplies tracking state only; selection samples still use the production latch.
        sealed class PointerHand : IHand
        {
            public bool Tracked=true;
            public Handedness Handedness => Handedness.Left;
            public bool IsConnected => Tracked;
            public bool IsTrackedDataValid => Tracked;
            public bool IsHighConfidence => true;
            public bool IsDominantHand => false;
            public float Scale => 1;
            public bool IsPointerPoseValid => false;
            public int CurrentDataVersion => 0;
            public event System.Action WhenHandUpdated { add { } remove { } }
            public bool GetFingerIsPinching(HandFinger finger) => false;
            public bool GetIndexFingerIsPinching() => false;
            public bool GetFingerIsHighConfidence(HandFinger finger) => true;
            public float GetFingerPinchStrength(HandFinger finger) => 0;
            public bool GetPointerPose(out Pose pose) { pose=default; return false; }
            public bool GetJointPose(HandJointId joint,out Pose pose) { pose=default; return false; }
            public bool GetJointPoseLocal(HandJointId joint,out Pose pose) { pose=default; return false; }
            public bool GetJointPoseFromWrist(HandJointId joint,out Pose pose) { pose=default; return false; }
            public bool GetPalmPoseLocal(out Pose pose) { pose=default; return false; }
            public bool GetRootPose(out Pose pose) { pose=default; return false; }
            public bool GetJointPosesLocal(out ReadOnlyHandJointPoses poses) { poses=default; return false; }
            public bool GetJointPosesFromWrist(out ReadOnlyHandJointPoses poses) { poses=default; return false; }
        }
        static void Aim(Transform pointer,Transform button)
        {
            pointer.SetPositionAndRotation(button.position-button.forward,button.rotation);
        }
    }
}

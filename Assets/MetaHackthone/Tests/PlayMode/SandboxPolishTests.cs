using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PhysicalAssembly.Tests
{
    public sealed partial class DownhillTests
    {
        [Test] public void PalmMenuRequiresDwellAndGestureReset()
        {
            var menu=root.AddComponent<SandboxPalmMenu>(); menu.Initialize(level);
            var palm=new Vector3(0,1.2f,.4f); var head=new Vector3(0,1.6f,0);
            Assert.IsFalse(menu.IsOpen);
            menu.Sample(false,default,default,head,.1f);
            menu.Sample(true,palm,Vector3.up,head,.34f); Assert.IsFalse(menu.IsOpen);
            menu.Sample(true,palm,Vector3.up,head,.02f); Assert.IsTrue(menu.IsOpen);
            menu.Close(); menu.Sample(true,palm,Vector3.up,head,1); Assert.IsFalse(menu.IsOpen);
            menu.Sample(true,palm,Vector3.down,head,.01f);
            menu.Sample(true,palm,Vector3.up,head,.51f); Assert.IsTrue(menu.IsOpen);
            menu.Sample(false,palm,Vector3.up,head,.01f); Assert.IsTrue(menu.IsOpen); Assert.IsFalse(menu.CanPress);
            menu.Sample(false,palm,Vector3.up,head,.6f); Assert.IsFalse(menu.IsOpen);
        }
        [Test] public void PalmOpeningAccumulatesOnlyValidRelaxedSamples()
        {
            var menu=root.AddComponent<SandboxPalmMenu>(); menu.Initialize(level);
            var head=new Vector3(0,1.6f,0); var palm=new Vector3(0,.86f,.4f);
            var normal=new Vector3(.89f,.46f,0).normalized;
            menu.Sample(true,palm,normal,head,.20f,3);
            menu.Sample(false,palm,normal,head,.14f,3);
            Assert.That(menu.Progress,Is.EqualTo(.20f/.35f).Within(.001f));
            menu.Sample(true,palm,normal,head,.14f,3); Assert.IsFalse(menu.IsOpen);
            menu.Sample(true,palm,normal,head,.02f,3); Assert.IsTrue(menu.IsOpen);
            menu.Sample(true,palm,Vector3.right,head,1,0); Assert.IsTrue(menu.IsOpen);
            menu.Close(); menu.Sample(true,palm,normal,head,1,3); Assert.IsFalse(menu.IsOpen);
            menu.Sample(true,palm,normal,head,.01f,2);
            menu.Sample(true,palm,normal,head,.2f,3);
            menu.Sample(true,palm,Vector3.right,head,.16f,3);
            Assert.AreEqual(0,menu.Progress);
            menu.Sample(true,palm,normal,head,.2f,3); Assert.IsFalse(menu.IsOpen);
        }
        [Test] public void PalmClosingHysteresisAndRepeatedDownUpNeedNoExtraReset()
        {
            var menu=root.AddComponent<SandboxPalmMenu>(); menu.Initialize(level);
            var head=new Vector3(0,1.6f,0); var palm=new Vector3(0,1,.4f);
            for(int i=0;i<3;i++) {
                menu.Sample(true,palm,Vector3.up,head,.36f,3); Assert.IsTrue(menu.IsOpen);
                menu.Sample(true,palm,Vector3.down,head,.19f,0); Assert.AreEqual(PalmMenuState.Closing,menu.State);
                menu.Sample(true,palm,Vector3.right,head,.02f,0); Assert.AreEqual(PalmMenuState.Open,menu.State);
                menu.Sample(true,palm,Vector3.down,head,.21f,0); Assert.IsFalse(menu.IsOpen);
            }
            menu.Sample(true,palm,Vector3.up,head,.36f);
            var pose=level.Presentation.Interface.LeftPanel.position;
            menu.Sample(false,Vector3.zero,Vector3.zero,head,.59f); Assert.IsTrue(menu.IsOpen);
            Assert.AreEqual(pose,level.Presentation.Interface.LeftPanel.position); Assert.IsFalse(menu.CanPress);
            menu.Sample(true,palm,Vector3.up,head,.01f,0); Assert.IsTrue(menu.CanPress);
            menu.Sample(true,new Vector3(0,.66f,.4f),Vector3.up,head,1,0); Assert.IsTrue(menu.IsOpen);
            menu.Sample(true,new Vector3(0,.64f,.4f),Vector3.up,head,.29f,0); Assert.IsTrue(menu.IsOpen);
            menu.Sample(true,new Vector3(0,.64f,.4f),Vector3.up,head,.02f,0); Assert.IsFalse(menu.IsOpen);
        }
        [Test] public void PalmSuppressionClosesAndRequiresLeavingPose()
        {
            var menu=root.AddComponent<SandboxPalmMenu>(); menu.Initialize(level);
            var head=Vector3.up*1.6f; var palm=Vector3.up*1.2f;
            menu.Sample(true,palm,Vector3.up,head,.4f);
            menu.Sample(true,palm,Vector3.up,head,.01f,4,true); Assert.IsFalse(menu.IsOpen);
            menu.Sample(true,palm,Vector3.up,head,1); Assert.IsFalse(menu.IsOpen);
            menu.Sample(true,palm,Vector3.down,head,.01f);
            menu.Sample(true,palm,Vector3.up,head,.4f); Assert.IsTrue(menu.IsOpen);
        }
        [UnityTest] public IEnumerator SandboxBoundaryReturnsOnlyReleasedLooseParts()
        {
            level.Course.SandboxPolish=true; yield return null;
            var part=level.Kit[5]; var spawn=part.MainBody.Rigidbody.position;
            var outside=level.Course.BuildArea.transform.TransformPoint(level.Course.BuildArea.Size*2);
            Assert.IsTrue(workshop.World.TryGrab(0,part,Grip(part)));
            part.MainBody.Rigidbody.position=outside;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.AreEqual(0,level.BoundaryReturnCount);
            workshop.World.Release(0,false); part.MainBody.Rigidbody.position=outside; part.MainBody.transform.position=outside;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.IsTrue(level.IsReturning(part));
            Assert.IsFalse(workshop.World.TryGrab(0,part,Grip(part)));
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.AreEqual(0,level.BoundaryReturnCount);
            Assert.That(Vector3.Distance(spawn,part.MainBody.Rigidbody.position),Is.LessThan(.001f));
            foreach(var body in part.Bodies) foreach(var collider in body.Colliders) Assert.IsTrue(collider.enabled);
        }
        [UnityTest] public IEnumerator SandboxBoundaryResetClearsEffectsAndPreservesCoreAttachments()
        {
            level.Course.SandboxPolish=true; yield return null;
            AddWheel(workshop,level.Kit[1],1,0);
            var attached=level.Kit[1]; var loose=level.Kit[5];
            var outside=level.Course.BuildArea.transform.TransformPoint(level.Course.BuildArea.Size*2);
            attached.MainBody.Rigidbody.position=outside; loose.MainBody.Rigidbody.position=outside;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.IsFalse(level.IsReturning(attached)); Assert.IsTrue(level.IsReturning(loose));
            Assert.IsTrue(level.TryResetBuild()); Assert.AreEqual(0,level.BoundaryReturnCount);
            yield return null;
            foreach(var part in level.Kit) foreach(var renderer in part.VisualRenderers) Assert.IsTrue(renderer.enabled);
        }
        [UnityTest] public IEnumerator SandboxLooseHighlightsFollowAttachmentGraph()
        {
            level.Course.SandboxPolish=true;
            var highlights=root.AddComponent<SandboxPartHighlights>(); highlights.Level=level;
            yield return null; int loose=SandboxPartHighlights.Draws.Count; Assert.Greater(loose,0);
            AddWheel(workshop,level.Kit[1],1,0); yield return null;
            Assert.Less(SandboxPartHighlights.Draws.Count,loose);
            Assert.IsTrue(workshop.World.TryDetachPart(level.Kit[1])); yield return null;
            Assert.AreEqual(loose,SandboxPartHighlights.Draws.Count);
        }
        [Test] public void BuildingVolumeTestsAllFacesInItsOwnFrame()
        {
            var go=new GameObject("Rotated building cube"); go.transform.SetParent(root.transform);
            go.transform.SetPositionAndRotation(new Vector3(3,2,1),Quaternion.Euler(0,73,0));
            go.transform.localScale=Vector3.one*.4f; var area=go.AddComponent<BuildingArea>(); area.Size=Vector3.one;
            for(int axis=0;axis<3;axis++) for(int sign=-1;sign<=1;sign+=2) {
                var point=Vector3.zero; point[axis]=sign*.49f; Assert.IsTrue(area.Contains(go.transform.TransformPoint(point)));
                point[axis]=sign*.51f; Assert.IsFalse(area.Contains(go.transform.TransformPoint(point)));
            }
        }
    }
}

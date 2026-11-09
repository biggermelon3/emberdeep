using System.Collections;
using System.Linq;
using NUnit.Framework;
using Oculus.Interaction;
using PhysicalAssembly.MetaXR;
using UnityEngine;
using UnityEngine.TestTools;

namespace PhysicalAssembly.Tests
{
    public sealed partial class DownhillTests
    {
        [UnityTest] public IEnumerator SandboxPanelAnchorsStayFixedAcrossHeadMotionAndCourseApproach()
        {
            var eye=new GameObject("Moving headset test"); eye.transform.SetParent(root.transform); eye.AddComponent<Camera>();
            settings.TransitionDuration=.08f; settings.CountdownStep=.02f; settings.GoDuration=.02f;

            var ui=level.Presentation.Interface;
            var anchors=new[]{ui.Root,ui.LeftPanel,ui.RightPanel,ui.CountdownAnchor,ui.StatusAnchor,
                level.Presentation.ReadyButton.transform,level.Presentation.RetryButton.transform};
            var positions=anchors.Select(t=>t.position).ToArray(); var rotations=anchors.Select(t=>t.rotation).ToArray();
            var scales=anchors.Select(t=>t.lossyScale).ToArray();
            void Check()
            {
                for(int i=0;i<anchors.Length;i++)
                {
                    Assert.That(Vector3.Distance(positions[i],anchors[i].position),Is.LessThan(.00001f),anchors[i].name);
                    Assert.That(Quaternion.Angle(rotations[i],anchors[i].rotation),Is.LessThan(.001f),anchors[i].name);
                    Assert.That(Vector3.Distance(scales[i],anchors[i].lossyScale),Is.LessThan(.00001f),anchors[i].name);
                }
            }
            eye.transform.SetPositionAndRotation(new Vector3(2,1.7f,-3),Quaternion.Euler(50,140,20));
            level.Presentation.ReadyButton.SetInteractionState(true,false);
            yield return null; Check();
            var courseStart=level.Course.SandboxFrame.position;
            Assert.IsTrue(level.TryStart());
            yield return Until(()=>level.State==DownhillState.Running); Check();
            Assert.Greater(Vector3.Distance(courseStart,level.Course.SandboxFrame.position),.01f);
            Assert.That(Vector3.Distance(level.Presentation.ReadyButton.transform.position,level.Presentation.RetryButton.transform.position),Is.LessThan(.00001f));
            eye.transform.SetPositionAndRotation(new Vector3(-2,.6f,4),Quaternion.Euler(-45,-120,-20));
            Assert.IsTrue(level.TryRetry()); yield return null; Check();
            Assert.IsTrue(level.TryReturn()); yield return Until(()=>level.IsBuilding); Check();
            Assert.IsTrue(level.TryResetBuild()); Check();
            Assert.IsTrue(ui.RightPanel.gameObject.activeInHierarchy);
        }

        [UnityTest] public IEnumerator SandboxResetRestoresOriginalKitAndHeadingAtWorkbench()
        {

            Assert.IsTrue(level.TryResetBuild());
            var parts=level.Kit.ToArray(); var bodies=parts.SelectMany(p=>p.Bodies).ToArray();
            var poses=bodies.Select(b=>new Pose(b.Rigidbody.position,b.Rigidbody.rotation)).ToArray();
            AddWheel(workshop,parts[1],1,0); AddWheel(workshop,parts[2],-1,0);
            SetHeading(level,90);
            Assert.IsTrue(workshop.World.TryDetachPart(parts[2]));
            Assert.IsTrue(workshop.World.TryGrab(0,parts[5],Grip(parts[5])));
            Assert.IsFalse(level.TryResetBuild()); workshop.World.ReleaseAll();
            parts[5].MainBody.Rigidbody.position+=Vector3.up;
            Assert.IsTrue(level.TryResetBuild());
            Assert.AreEqual(0,workshop.World.Connections.Count);
            CollectionAssert.AreEqual(parts,level.Kit);
            Assert.AreEqual(8,workshop.World.Parts.Count); Assert.AreEqual(0,level.PendingReturnCount);
            for(int i=0;i<bodies.Length;i++)
            {
                Assert.That(Vector3.Distance(poses[i].position,bodies[i].Rigidbody.position),Is.LessThan(.0001f));
                Assert.That(Quaternion.Angle(poses[i].rotation,bodies[i].Rigidbody.rotation),Is.LessThan(.01f));
                Assert.IsTrue(bodies[i].Rigidbody.isKinematic);
            }
            Assert.IsTrue(level.TryStart()); Assert.IsFalse(level.TryResetBuild());
            level.RestoreBuildImmediately(); yield return null;
        }

        [UnityTest] public IEnumerator SandboxResetAcceptsNativePokeAtWorkbench()
        {
            var button=level.Presentation.Interface.ResetButton;
            button.gameObject.AddComponent<MetaMenuButton>().Initialize(workshop);

            AddWheel(workshop,level.Kit[1],1,0);
            yield return null;
            var finger=new GameObject("Reset fingertip"); finger.transform.SetParent(root.transform);
            var face=button.transform.position; var forward=button.transform.forward;
            finger.transform.position=face-forward*.12f;
            finger.AddComponent<PokeInteractor>().InjectAllPokeInteractor(finger.transform,.005f);
            yield return null; yield return null;
            for(int i=0;i<27 && workshop.World.Connections.Count>0;i++)
            { finger.transform.position=face-forward*(.12f-i*.005f); yield return null; }
            Assert.AreEqual(0,workshop.World.Connections.Count,"Native poke must invoke the sandbox reset command.");
            finger.SetActive(false); Object.Destroy(finger); yield return new WaitForSeconds(.5f);
        }
    }
}

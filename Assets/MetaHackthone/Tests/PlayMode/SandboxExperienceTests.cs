using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PhysicalAssembly.Tests
{
    public sealed partial class DownhillTests
    {
        IEnumerator Until(System.Func<bool> predicate,float timeout=5)
        {
            float end=Time.realtimeSinceStartup+timeout;
            while(!predicate() && Time.realtimeSinceStartup<end) yield return null;
            Assert.IsTrue(predicate(),"State transition timed out: "+level.State);
        }
        [UnityTest] public IEnumerator WorkbenchSurviveTenTestRetryBackCycles()
        {
            settings.TransitionDuration=.06f; settings.CountdownStep=.03f; settings.GoDuration=.02f;
            settings.AttemptDuration=30; settings.StallDuration=30;
            BuildReferenceAxle(workshop,PartKind.DownhillLargeWheel);
            float clock=Time.timeScale,step=Time.fixedDeltaTime;

            var ids=workshop.World.Parts.Keys.OrderBy(x=>x).ToArray();
            var original=level.Core.MainBody.Rigidbody.position;
            var arena=new Pose(workshop.Arena.position,workshop.Arena.rotation);
            for(int cycle=0;cycle<10;cycle++)
            {
                Assert.IsTrue(level.Presentation.WorkbenchVisuals.gameObject.activeSelf);
                Assert.AreEqual(2,workshop.World.Connections.Count);
                level.Presentation.ReadyButton.Pressed();
                Assert.AreEqual(DownhillState.Transition,level.State);
                Assert.IsFalse(workshop.World.EditingEnabled);
                Assert.IsTrue(level.Kit.All(p=>!p.gameObject.activeSelf));
                Assert.IsFalse(level.Presentation.WorkbenchVisuals.gameObject.activeSelf);
                var test=level.SimulationCore; var testIds=level.SimulationWorld.Parts.Keys.OrderBy(x=>x).ToArray();
                yield return Until(()=>level.State==DownhillState.Countdown);
                Assert.IsTrue(level.SimulationWorld.Parts.Values.SelectMany(p=>p.Bodies).All(b=>b.Rigidbody.isKinematic));
                yield return Until(()=>level.State==DownhillState.Running);
                Assert.AreEqual(clock*settings.PhysicsSpeed,Time.timeScale,.0001f);
                Assert.AreEqual(step*settings.PhysicsSpeed,Time.fixedDeltaTime,.0001f);
                CollectionAssert.AreEquivalent(new[]{"Sandbox Retry","Sandbox Back","Sandbox Quit"},workshop.InteractionButtons.Where(b=>b.gameObject.activeInHierarchy).Select(b=>b.Command));
                level.Presentation.RetryButton.Pressed();
                Assert.AreEqual(DownhillState.Countdown,level.State); Assert.AreSame(test,level.SimulationCore);
                CollectionAssert.AreEqual(testIds,level.SimulationWorld.Parts.Keys.OrderBy(x=>x));
                yield return Until(()=>level.State==DownhillState.Running);
                level.Presentation.BackButton.Pressed();
                yield return Until(()=>level.IsBuilding);
                yield return null; // deferred destruction of the test root
                CollectionAssert.AreEqual(ids,workshop.World.Parts.Keys.OrderBy(x=>x));
                Assert.AreEqual(8,workshop.World.Parts.Count); Assert.IsNull(level.SimulationWorld);
                Assert.AreEqual(clock,Time.timeScale,.0001f); Assert.AreEqual(step,Time.fixedDeltaTime,.0001f);
                Assert.Less(Vector3.Distance(original,level.Core.MainBody.Rigidbody.position),.0001f);
                Assert.AreEqual(arena.position,workshop.Arena.position); Assert.AreEqual(arena.rotation,workshop.Arena.rotation);
                Assert.IsTrue(level.Kit.SelectMany(p=>p.Bodies).All(b=>b.Rigidbody.isKinematic&&!b.Rigidbody.useGravity));
            }
        }
        [UnityTest] public IEnumerator ReleasedPartsFloatInWorkbenchAndCanBeRegrabbed()
        {
            var p=level.Kit[5]; var rb=p.MainBody.Rigidbody;
            Assert.IsTrue(workshop.World.TryGrab(1,p,new Pose(rb.position,rb.rotation)));
            var target=new Pose(rb.position+Vector3.up*.1f,rb.rotation);
            workshop.World.SetHoldPose(1,target); yield return new WaitForFixedUpdate();
            workshop.World.Release(1,false); var released=rb.position;
            Assert.Less(Vector3.Distance(target.position,released),.001f);
            level.TickReturns(100); yield return new WaitForSeconds(.2f);
            Assert.Less(Vector3.Distance(released,rb.position),.00001f); Assert.IsFalse(rb.useGravity);
            Assert.IsTrue(workshop.World.TryGrab(0,p,new Pose(rb.position,rb.rotation))); workshop.World.Release(0,false);
        }
        [UnityTest] public IEnumerator FastHeldBlockCannotCrossCoreAndCanMoveAway()
        {
            var part=level.Kit[5]; var rb=part.MainBody.Rigidbody;
            var origin=level.Core.MainBody.Rigidbody.position;
            rb.position=origin+Vector3.left*.4f; rb.transform.position=rb.position; Physics.SyncTransforms();
            Assert.IsTrue(workshop.World.TryGrab(1,part,new Pose(rb.position,rb.rotation)));
            workshop.World.SetHoldPose(1,new Pose(origin+Vector3.right*.4f,rb.rotation));
            for(int i=0;i<20;i++) yield return new WaitForFixedUpdate();
            Assert.Less(rb.position.x,origin.x-.1f,"A fast hand must not teleport the block through the core.");
            var stopped=rb.position;
            workshop.World.SetHoldPose(1,new Pose(origin+Vector3.left*.4f,rb.rotation));
            for(int i=0;i<12;i++) yield return new WaitForFixedUpdate();
            Assert.Less(rb.position.x,stopped.x-.1f,"Contact must not trap the held object.");
            workshop.World.Release(1,false);
        }
        [UnityTest] public IEnumerator BlockPreviewAndCommitUseQuarterTurns()
        {
            var part=level.Kit[5]; var target=level.Core.FindSocket("grid_center_x+_0_0"); var source=part.Sockets.First(s=>s.Id.Contains("x-"));
            foreach(float angle in new[]{44f,46f,135f,225f,315f})
            {
                var face=Quaternion.LookRotation(-target.Normal,target.Up);
                var rotation=Quaternion.AngleAxis(angle,target.Normal)*face*Quaternion.Inverse(source.transform.localRotation);
                var rb=part.MainBody.Rigidbody; rb.rotation=rotation; rb.position=target.WorldPosition+target.Normal*.07f-rotation*source.transform.localPosition;
                rb.transform.SetPositionAndRotation(rb.position,rb.rotation); Physics.SyncTransforms();
                var candidate=workshop.World.Query(part); Assert.IsTrue(candidate.IsValid);
                var snapped=candidate.SnappedPose.rotation*source.transform.localRotation;
                float twist=Vector3.SignedAngle(target.Up,snapped*Vector3.up,target.Normal);
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(twist,Mathf.Round(twist/90)*90)),Is.LessThan(.05f));
                Assert.IsTrue(workshop.World.TryBeginAssembly(candidate)); yield return new WaitForSeconds(.35f);
                Assert.AreEqual(1,workshop.World.Connections.Count);
                Assert.That(Quaternion.Angle(candidate.SnappedPose.rotation,rb.rotation),Is.LessThan(.05f));
                Assert.IsTrue(workshop.World.TryDetachPart(part));
            }
        }
        [Test] public void CoreOnlyProfileAndSavedConnectionsRoundTrip()
        {
            Assert.AreEqual(8,level.Core.Sockets.Length);
            Assert.IsTrue(level.Core.Sockets.All(s=>Mathf.Abs((s.transform.localRotation*Vector3.forward).y)<.001f));
            foreach(var part in level.Kit.Skip(1))
            {
                var reference=DownhillParts.Create(workshop.World,part.Kind,new Pose(Vector3.up*5,Quaternion.identity));
                CollectionAssert.AreEqual(reference.Sockets.Select(s=>s.Id),part.Sockets.Select(s=>s.Id));
                CollectionAssert.AreEqual(reference.Sockets.Select(s=>s.transform.localPosition),part.Sockets.Select(s=>s.transform.localPosition));
                workshop.World.UnregisterPart(reference); Object.DestroyImmediate(reference.gameObject);
            }
            BuildReferenceAxle(workshop,PartKind.DownhillLargeWheel);
            var data=workshop.Saves.Capture(level.Core);
            var rules=workshop.World.Rules; workshop.World.Rules=null;
            System.Collections.Generic.List<AssemblyPart> restored;
            try { restored=workshop.Saves.Restore(data,new Pose(Vector3.up*5,Quaternion.identity)); }
            finally { workshop.World.Rules=rules; }
            Assert.AreEqual(8,restored.Single(p=>p.BuildingCore).Sockets.Length);
            foreach(var part in restored) { workshop.World.UnregisterPart(part); Object.DestroyImmediate(part.gameObject); }
        }
        [Test] public void ExitRestoresClockAndUsesMainMenuFallback()
        {
            int calls=0; workshop.ReturnToWorkshopRequested=()=>calls++;
            Assert.IsTrue(level.TryStart()); level.Presentation.ExitButton.Pressed();
            Assert.AreEqual(1,calls); Assert.IsTrue(level.IsBuilding); Assert.IsNull(level.SimulationWorld);
            Assert.IsTrue(workshop.World.EditingEnabled);
        }
        [Test] public void BothPalettesStartClearOfSolidGeometry()
        {
            Physics.SyncTransforms();
            foreach(var part in level.Kit) foreach(var collider in part.Bodies.SelectMany(b=>b.Colliders))
            foreach(var other in Physics.OverlapBox(collider.bounds.center,collider.bounds.extents,Quaternion.identity,~0,QueryTriggerInteraction.Ignore))
            {
                if(other.GetComponentInParent<AssemblyBody>()?.Part==part) continue;
                if(Physics.ComputePenetration(collider,collider.transform.position,collider.transform.rotation,other,other.transform.position,other.transform.rotation,out _,out float depth))
                    Assert.Less(depth,.001f,"Workbench: "+part.Kind+" overlaps "+other.name);
            }
        }
        [Test] public void RestoringAgainDoesNotUndoNewBuildEdits()
        {
            Assert.IsTrue(level.TryStart()); Assert.IsTrue(level.TryReturn());
            var rb=level.Kit[5].MainBody.Rigidbody; rb.position+=Vector3.up*.2f; rb.transform.position=rb.position;
            var edited=rb.position; level.RestoreBuildImmediately();
            Assert.Less(Vector3.Distance(edited,rb.position),.0001f);
        }
        [Test] public void TestReturnPreservesDetachedBranchesAndLoosePalettePoses()
        {
            var branch=WheelBranch();
            Assert.IsTrue(workshop.World.TryGrab(0,branch.wheel,Grip(branch.wheel))); workshop.World.Release(0,false);
            Assert.IsTrue(level.TryStart()); level.RestoreBuildImmediately();

            Assert.AreEqual(8,workshop.World.Parts.Count); Assert.AreEqual(1,workshop.World.Connections.Count);
            var edge=workshop.World.Connections.Single(); var a=workshop.World.Resolve(edge.a).Rigidbody; var b=workshop.World.Resolve(edge.b).Rigidbody;
            Assert.Less(Vector3.Distance(a.position+a.rotation*edge.anchorA,b.position+b.rotation*edge.anchorB),.0001f);
            var loose=level.Kit[1]; var spawn=level.Presentation.SpawnPose(1);
            Assert.Less(Vector3.Distance(loose.MainBody.Rigidbody.position,spawn.position),.0001f);
            Assert.Less(Quaternion.Angle(loose.MainBody.Rigidbody.rotation,spawn.rotation),.05f);
        }
    }
}

using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PhysicalAssembly.Tests
{
    public sealed class GravityGarageTests
    {
        GameObject root; WorkshopBootstrap workshop; DownhillController level; DownhillSettings settings;
        [UnitySetUp] public IEnumerator Setup()
        {
            var scene=SceneManager.GetSceneByName("Garage test fixture");if(!scene.IsValid())scene=SceneManager.CreateScene("Garage test fixture");SceneManager.SetActiveScene(scene);
            settings=ScriptableObject.CreateInstance<DownhillSettings>();settings.UseGravityGarage=true;settings.CountdownStep=.01f;settings.GoDuration=.01f;settings.TransitionDuration=0;
            root=new GameObject("Garage test");root.SetActive(false);workshop=root.AddComponent<WorkshopBootstrap>();workshop.DownhillSettings=settings;root.SetActive(true);
            yield return null;level=workshop.Downhill;level.SetGaragePlayback(4);
        }
        [TearDown] public void Cleanup()
        {
            if(level)level.RestoreBuildImmediately();if(root)Object.DestroyImmediate(root);
            foreach(var p in Object.FindObjectsByType<AssemblyPart>(FindObjectsSortMode.None))Object.DestroyImmediate(p.gameObject);
            foreach(var c in Object.FindObjectsByType<WorkshopEnvironment>(FindObjectsSortMode.None))Object.DestroyImmediate(c.gameObject);
            if(settings)Object.DestroyImmediate(settings);
        }
        [Test] public void ReferenceUsesSharedSocketsPassiveWheelsAndMassScaling()
        {
            Assert.AreEqual(7,level.Kit.Count);Assert.AreEqual(6,workshop.World.Connections.Count);
            Assert.AreEqual(8,level.Core.Sockets.Length);
            var wheels=level.Kit.Where(p=>DownhillParts.IsWheel(p.Kind)).ToArray();Assert.AreEqual(4,wheels.Length);
            foreach(var wheel in wheels) {Assert.IsFalse(wheel.GetComponentInChildren<HingeJoint>().useMotor);Assert.AreEqual(wheel.Kind==PartKind.DownhillLargeWheel?DownhillParts.PuzzleLargeDrag:.003f,wheel.GetComponentInChildren<PassiveWheel>().Resistance);}
            var large=wheels.First(p=>p.Kind==PartKind.DownhillLargeWheel);var small=wheels.First(p=>p.Kind==PartKind.DownhillSmallWheel);
            Assert.That(large.Bodies.Sum(b=>b.Rigidbody.mass)/small.Bodies.Sum(b=>b.Rigidbody.mass),Is.EqualTo(Mathf.Pow(.46f/.26f,2)).Within(.001f));
            Assert.AreEqual(.8f,settings.Garage.Gap);Assert.AreEqual(1.2f,new GravityGarageDesign().Gap);
            Assert.Less(settings.Garage.SupportedX(8.5f),GravityGarageDesign.Edge);
        }
        [UnityTest] public IEnumerator Compare3DAndRestoreConstruction() {yield return Compare(false);}
        [UnityTest] public IEnumerator ComparePlanarAndRestoreConstruction() {yield return Compare(true);}
        [UnityTest] public IEnumerator CompareMiniature3D() {level.Core.GeometryScale=.4f;level.LoadGaragePreset(GravityGaragePreset.BigFront);yield return Compare(false);}
        [UnityTest] public IEnumerator CompareMiniaturePlanar() {level.Core.GeometryScale=.4f;level.LoadGaragePreset(GravityGaragePreset.BigFront);yield return Compare(true);}
        [UnityTest] public IEnumerator ComparisonRestoresDetachedWheel()
        {
            var wheel=level.Kit.Last(p=>DownhillParts.IsWheel(p.Kind));Assert.IsTrue(workshop.World.TryDetachPart(wheel));
            foreach(var body in wheel.Bodies) {body.Rigidbody.position+=Vector3.up*.12f;body.transform.position=body.Rigidbody.position;}
            workshop.Session.Select(wheel);yield return Compare(false);
            Assert.IsTrue(DownhillParts.IsWheel(workshop.Session.Selected.Kind));
        }
        [Test] public void SaveRoundTripRetainsWheelAxleOffsetAndGeometryVersion()
        {
            level.Core.GeometryScale=.4f;level.LoadGaragePreset(GravityGaragePreset.BigFront);
            var save=JsonUtility.FromJson<AssemblySaveV1>(JsonUtility.ToJson(workshop.Saves.Capture(level.Core)));
            var target=new GameObject("Restored garage");target.transform.SetParent(root.transform);
            var world=target.AddComponent<AssemblyWorld>();var factory=target.AddComponent<PartFactory>();factory.World=world;
            var service=target.AddComponent<AssemblySaveService>();service.World=world;service.Factory=factory;
            var parts=service.Restore(save,new Pose(new Vector3(20,3,0),Quaternion.identity));
            Assert.AreEqual(7,parts.Count);Assert.AreEqual(6,world.Connections.Count);
            foreach(var p in parts) {Assert.AreEqual(8,p.GridVersion);Assert.AreEqual(.4f,p.GeometryScale);}
            foreach(var p in parts.Where(p=>DownhillParts.IsWheel(p.Kind)))
                Assert.That(Vector3.Distance(p.FindBody("hub").Rigidbody.position,p.FindBody("tire").Rigidbody.position),Is.EqualTo(0f).Within(.00001f));
        }
        [UnityTest] public IEnumerator WideGapDoesNotAwardAnAirborneFinish()
        {
            settings.Garage.Gap=2.35f;settings.Garage.LandingDrop=.15f;level.RebuildGarageCourse();Assert.IsTrue(level.TryStart());
            while(level.State==DownhillState.Transition || level.State==DownhillState.Countdown || level.State==DownhillState.Running)yield return null;
            Assert.IsFalse(level.GarageResult.Success);Assert.AreEqual(DownhillState.Failed,level.State);
        }
        [UnityTest] public IEnumerator DetachedAxleCanBeSplitBetweenHandsAndAgainAfterRelease()
        {
            var world=workshop.World;
            var beam=level.Kit.First(p=>p.Kind==PartKind.DownhillShortBlock);
            var wheel=world.Connections.Where(e=>e.b.partId==beam.InstanceId).Select(e=>world.Parts[e.a.partId]).First(p=>DownhillParts.IsWheel(p.Kind));
            Assert.IsTrue(world.TryGrab(0,beam,new Pose(beam.MainBody.Rigidbody.position,Quaternion.identity)));
            Assert.AreEqual(3,world.HeldParts(0).Count);
            Assert.IsTrue(world.TryGrab(1,wheel,new Pose(wheel.MainBody.Rigidbody.position,Quaternion.identity)));
            Assert.AreEqual(2,world.HeldParts(0).Count);Assert.AreEqual(1,world.HeldParts(1).Count);
            world.ReleaseAll();
            var remaining=world.GetConnectedParts(beam.InstanceId).First(p=>p!=beam);
            Assert.IsTrue(world.TryGrab(1,remaining,new Pose(remaining.MainBody.Rigidbody.position,Quaternion.identity)));
            Assert.AreEqual(1,world.HeldParts(1).Count);Assert.AreEqual(1,world.GetConnectedParts(beam.InstanceId).Count);
            world.ReleaseAll(); yield return null;
        }
        [UnityTest] public IEnumerator PadTracksCoreAndSmallWheelClearsBlock()
        {
            level.LoadGaragePreset(GravityGaragePreset.BothSmall);
            var wheel=level.Kit.First(p=>DownhillParts.IsWheel(p.Kind));
            Assert.That(wheel.GetComponentInChildren<PassiveWheel>().Radius,Is.EqualTo(.26f*GravityGarageDesign.UnitScale*wheel.GeometryScale).Within(.00001f));
            foreach(var socket in wheel.Sockets) Assert.That(socket.transform.localPosition.y,Is.EqualTo(0).Within(.00001f));
            for(int i=0;i<4;i++) {
                level.Core.MainBody.Rigidbody.rotation=level.Course.WorkbenchFrame.rotation*Quaternion.Euler(0,90*i,0);
                yield return null;
                var pivot=level.Presentation.WorkbenchVisuals.Find("Core direction grid");Assert.IsNotNull(pivot);
                Assert.Less(Vector3.Angle(pivot.forward,level.Core.MainBody.Rigidbody.rotation*Vector3.forward),.1f);
            }
        }
        [UnityTest] public IEnumerator MixedWheelCarCompletesRepeatedRuns()
        {
            foreach(float scale in new[]{.4f,1f}) {
                level.Core.GeometryScale=scale;
                Assert.IsTrue(level.LoadGaragePreset(GravityGaragePreset.BigFront));
                Assert.AreEqual(2,level.Kit.Count(p=>p.Kind==PartKind.DownhillSmallWheel));
                Assert.AreEqual(2,level.Kit.Count(p=>p.Kind==PartKind.DownhillLargeWheel));
                for(int attempt=0;attempt<10;attempt++) {
                    Assert.IsTrue(level.TryStart());
                    while(level.State==DownhillState.Transition || level.State==DownhillState.Countdown || level.State==DownhillState.Running) yield return null;
                    Assert.IsTrue(level.GarageResult.Success,"Scale "+scale+" attempt "+attempt+": "+JsonUtility.ToJson(level.GarageResult));
                    Assert.AreEqual("Stable landing",level.GarageResult.Outcome);
                    level.RestoreBuildImmediately();
                }
            }
        }
        [UnityTest] public IEnumerator OnlyFrontLargeCompletesPuzzle()
        {
            foreach(float scale in new[]{.4f,1f}) {
                level.Core.GeometryScale=scale;level.LoadGaragePreset(GravityGaragePreset.BigFront);
                for(int repeat=0;repeat<5;repeat++) {
                    yield return level.CompareGaragePresets();
                    foreach(var result in level.GarageComparisons)
                        Assert.AreEqual(result.Configuration.StartsWith("BigFront"),result.Success,"Scale "+scale+" repeat "+repeat+": "+JsonUtility.ToJson(result));
                }
            }
        }
        IEnumerator Compare(bool planar)
        {
            level.GaragePlanar=planar;int edges=workshop.World.Connections.Count;var positions=level.Kit.SelectMany(p=>p.Bodies).Select(b=>b.Rigidbody.position).ToArray();
            yield return level.CompareGaragePresets();
            Assert.AreEqual(4,level.GarageComparisons.Count);Assert.IsTrue(level.IsBuilding);Assert.AreEqual(edges,workshop.World.Connections.Count);
            var after=level.Kit.SelectMany(p=>p.Bodies).Select(b=>b.Rigidbody.position).ToArray();Assert.AreEqual(positions.Length,after.Length);
            for(int i=0;i<after.Length;i++)Assert.Less(Vector3.Distance(after[i],positions[i]),.0001f);
            foreach(var result in level.GarageComparisons) {Debug.Log("GARAGE_RESULT "+JsonUtility.ToJson(result));Assert.Greater(result.Time,0);Assert.IsFalse(float.IsNaN(result.MaxPitch));}
            Assert.IsTrue(level.GarageComparisons.Any(r=>r.Success),"Default course must permit at least one reference vehicle to land.");
        }
        [UnityTest] public IEnumerator RetryAndBaselineRestore()
        {
            var start=level.Course.StartPoint.position;Assert.IsTrue(level.TryStart());
            while(level.State==DownhillState.Transition || level.State==DownhillState.Countdown)yield return null;
            Assert.AreEqual(DownhillState.Running,level.State);Assert.IsTrue(level.TryRetry());
            level.RestoreBuildImmediately();Assert.AreEqual(start,level.Course.StartPoint.position);
            Assert.IsFalse(level.SelectGarageCourse(false));Assert.IsTrue(settings.UseGravityGarage);Assert.AreEqual(7,level.Kit.Count);
            Assert.IsTrue(level.SelectGarageCourse(true));Assert.AreEqual(7,level.Kit.Count);
        }
    }
}

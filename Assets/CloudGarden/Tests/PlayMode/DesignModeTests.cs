using System.Collections;
using System.IO;
using System.Linq;
using CloudGarden.Integration;
using NUnit.Framework;
using PhysicalAssembly;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CloudGarden.Tests
{
    public sealed class DesignModeTests
    {
        [UnitySetUp] public IEnumerator Setup()
        {
            DesignModeSession.SetEnabled(false); PlayAreaPose.Reset();
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return null; yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            DesignModeSession.SetEnabled(false); PlayAreaPose.Reset();
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return null;
        }
        static IEnumerator NavigationReady(GardenNavigation nav)
        {
            float deadline=Time.realtimeSinceStartup+20;
            while(nav.IsTransitioning && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.IsFalse(nav.IsTransitioning); yield return null; yield return null;
        }
        static IEnumerator Enable(GardenNavigation nav)
        {
            Assert.IsTrue(nav.TrySetDesignMode(true)); yield return NavigationReady(nav);
            Assert.IsTrue(DesignModeSession.Enabled); Assert.IsTrue(nav.Menu.Visible);
            Assert.IsFalse(nav.Player.IsXR); Assert.IsTrue(nav.Player.DesktopCamera.isActiveAndEnabled);
            Assert.IsFalse(nav.Player.XRRig.gameObject.activeSelf);
        }
        [UnityTest] public IEnumerator ExplicitModeSwitchResetsTemporaryLevelsAndNormalModeKeepsItsKit()
        {
            var nav=GardenNavigation.Current;
            nav.EnterMode(GameMode.CarWorkshop); yield return NavigationReady(nav);
            Assert.AreEqual(15,nav.CarWorkshop.World.Parts.Count);
            nav.ReturnToMainMenu(); yield return NavigationReady(nav);
            yield return Enable(nav);
            Assert.IsFalse(SceneManager.GetSceneByName("Workshop").isLoaded);
            nav.EnterMode(GameMode.CarWorkshop); yield return NavigationReady(nav);
            Assert.AreEqual(6,nav.CarWorkshop.World.Parts.Count);
            Assert.AreEqual(5,nav.CarWorkshop.World.Connections.Count);
            Assert.AreEqual(WorkshopMode.Build,nav.CarWorkshop.Session.Mode);
            Assert.IsFalse(nav.TrySetDesignMode(false),"Mode changes must go through the main menu.");
            nav.ReturnToMainMenu(); yield return NavigationReady(nav);
            Assert.IsTrue(nav.TrySetDesignMode(false)); yield return NavigationReady(nav);
            nav.EnterMode(GameMode.CarWorkshop); yield return NavigationReady(nav);
            Assert.AreEqual(15,nav.CarWorkshop.World.Parts.Count); Assert.IsEmpty(nav.CarWorkshop.World.Connections);
        }
        [UnityTest] public IEnumerator WorkshopResetUsesEditedSpawnAndCannotWritePlayerSave()
        {
            var nav=GardenNavigation.Current; yield return Enable(nav);
            nav.EnterMode(GameMode.CarWorkshop); yield return NavigationReady(nav);
            var owner=nav.CarWorkshop; var marker=owner.Layout.DesignCarSpawn;
            Assert.NotNull(marker);
            marker.SetPositionAndRotation(owner.Arena.TransformPoint(new Vector3(.15f,.70f,-.25f)),owner.Arena.rotation*Quaternion.Euler(0,23,0));
            for(int i=0;i<3;i++)
            {
                Assert.IsTrue(owner.TryLoadDesignPreset());
                var car=owner.Session.Selected;
                Assert.Less(Vector3.Distance(car.MainBody.Rigidbody.position,marker.position),.001f);
                Assert.Less(Quaternion.Angle(car.MainBody.Rigidbody.rotation,marker.rotation),.01f);
                Assert.AreEqual(6,owner.World.Parts.Count); Assert.AreEqual(5,owner.World.Connections.Count);
            }
            var selected=owner.Session.Selected;
            Assert.IsTrue(owner.World.TryGrab(2,selected,new Pose(selected.MainBody.Rigidbody.position,selected.MainBody.Rigidbody.rotation)));
            Assert.IsFalse(owner.TryLoadDesignPreset());
            owner.GetComponent<DesktopInputAdapter>().SetInputFocus(false); Assert.IsFalse(owner.World.HasHolds);
            string before=File.Exists(owner.Saves.SavePath)?File.ReadAllText(owner.Saves.SavePath):null;
            owner.InteractionButtons.Single(b=>b.Command=="Save construction").Press();
            string after=File.Exists(owner.Saves.SavePath)?File.ReadAllText(owner.Saves.SavePath):null;
            Assert.AreEqual(before,after);
            var ids=owner.World.Parts.Keys.OrderBy(id=>id).ToArray();
            nav.ReturnToMainMenu(); yield return NavigationReady(nav);
            nav.EnterMode(GameMode.CarWorkshop); yield return NavigationReady(nav);
            CollectionAssert.AreEqual(ids,nav.CarWorkshop.World.Parts.Keys.OrderBy(id=>id));
        }
        [UnityTest] public IEnumerator DesignerExampleTestsAndReturnsThreeTimesAtWorkbench()
        {
            var nav=GardenNavigation.Current; yield return Enable(nav);

                nav.EnterSandbox(); yield return NavigationReady(nav);
            var owner=nav.DownhillWorkshop; var level=owner.Downhill;
            Assert.AreEqual(7,owner.World.Parts.Count);
            Assert.AreEqual(6,owner.World.Connections.Count);
            Assert.IsTrue(owner.TryLoadDesignPreset());
            var ids=owner.World.Parts.Keys.OrderBy(id=>id).ToArray();
            float clock=Time.timeScale,step=Time.fixedDeltaTime;
            for(int attempt=0;attempt<3;attempt++)
            {
                Assert.IsTrue(level.TryStart());
                Assert.IsFalse(owner.TryLoadDesignPreset(),"Reset must not replace a running vehicle.");
                float deadline=Time.realtimeSinceStartup+50;
                while((level.State==DownhillState.Transition || level.State==DownhillState.Countdown) && Time.realtimeSinceStartup<deadline) yield return new WaitForFixedUpdate();
                Assert.AreEqual(DownhillState.Running,level.State);
                Assert.IsNotNull(level.GarageResult); Assert.AreEqual(6,level.SimulationWorld.Connections.Count);
                var simulation=level.SimulationCore;
                Assert.IsTrue(level.TryRetry()); Assert.AreSame(simulation,level.SimulationCore);
                Assert.IsTrue(level.TryReturn());
                deadline=Time.realtimeSinceStartup+5;
                while(!level.IsBuilding && Time.realtimeSinceStartup<deadline) yield return null;
                Assert.IsTrue(level.IsBuilding); yield return null;
                Assert.IsNull(level.SimulationWorld);
                CollectionAssert.AreEqual(ids,owner.World.Parts.Keys.OrderBy(id=>id));
                Assert.AreEqual(clock,Time.timeScale,.0001f); Assert.AreEqual(step,Time.fixedDeltaTime,.0001f);
            }
            nav.ReturnToMainMenu(); yield return NavigationReady(nav);
        }
        [UnityTest] public IEnumerator Level2TestAndRetryPreserveAuthoredRoads()
        {
            var nav=GardenNavigation.Current; yield return Enable(nav);
            nav.EnterDesignLevel(DesignLevelId.WaterfallValley); yield return NavigationReady(nav);
            var owner=nav.DownhillWorkshop; var level=owner.Downhill;
            var course=owner.Layout.GetComponent<AuthoredSandboxLayout>().Level2;
            var roadIds=course.Markers.Roads.Select(r=>r.GetInstanceID()).ToArray();
            var start=course.Markers.Start.position;
            for(int reset=0;reset<3;reset++) {
                Assert.IsTrue(owner.TryLoadDesignPreset());
                var parts=owner.World.GetConnectedParts(level.Core.InstanceId);
                Assert.AreEqual(7,parts.Count); Assert.AreEqual(6,owner.World.Connections.Count);
                Assert.AreEqual(4,parts.Count(p=>DownhillParts.IsWheel(p.Kind)));
                Assert.IsTrue(parts.Contains(level.Kit[2])); Assert.IsTrue(parts.Contains(level.Kit[4]));
                Assert.IsFalse(parts.Contains(level.Kit[1])); Assert.IsFalse(parts.Contains(level.Kit[3]));
                Assert.AreSame(level.Kit[2],owner.Session.Selected);
                float rearZ=parts.Where(p=>p.Kind==PartKind.DownhillSmallWheel)
                    .Max(p=>level.Presentation.AssemblyAnchor.InverseTransformPoint(p.MainBody.transform.position).z);
                foreach(var wheel in parts.Where(p=>p.Kind==PartKind.DownhillLargeWheel))
                    Assert.Greater(level.Presentation.AssemblyAnchor.InverseTransformPoint(wheel.MainBody.transform.position).z,rearZ);
                var drill=level.Kit[4]; Assert.AreEqual(PartKind.DownhillDrill,drill.Kind);
                Assert.Greater(Vector3.Dot(drill.MainBody.transform.forward,level.Presentation.AssemblyAnchor.forward),.99f);
                Assert.Greater(level.Presentation.AssemblyAnchor.InverseTransformPoint(drill.MainBody.transform.position).z,
                    level.Presentation.AssemblyAnchor.InverseTransformPoint(level.Kit[2].MainBody.transform.position).z);
            }
            Assert.IsTrue(level.TryStart());
            float deadline=Time.realtimeSinceStartup+50;
            while((level.State==DownhillState.Transition || level.State==DownhillState.Countdown) && Time.realtimeSinceStartup<deadline) yield return new WaitForFixedUpdate();
            Assert.AreEqual(DownhillState.Running,level.State);
            Assert.AreSame(course,level.Course3D);
            Assert.AreEqual(1,level.SimulationWorld.Parts.Values.Count(p=>p.Kind==PartKind.DownhillDrill));
            Assert.AreEqual(6,level.SimulationWorld.Connections.Count);
            Assert.Less(Vector3.Distance(level.SimulationCore.MainBody.Rigidbody.position,start),course.WorldScale*3);
            for(int i=0;i<5;i++) yield return new WaitForFixedUpdate();
            deadline=Time.realtimeSinceStartup+5;
            while(!level.Run3D.SplineActive && !level.Run3D.Finished && Time.realtimeSinceStartup<deadline)yield return new WaitForFixedUpdate();
            Assert.IsTrue(level.Run3D.SplineActive,level.Run3D.Feedback);
            Assert.IsTrue(level.SimulationWorld.Parts.Values.SelectMany(p=>p.Bodies).All(b=>b.Rigidbody.isKinematic && !b.Rigidbody.useGravity));
            Assert.IsTrue(level.TryRetry());
            CollectionAssert.AreEqual(roadIds,course.Markers.Roads.Select(r=>r.GetInstanceID()).ToArray());
            Assert.IsTrue(level.TryReturn());
            deadline=Time.realtimeSinceStartup+5;
            while(!level.IsBuilding && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.IsTrue(level.IsBuilding);
            CollectionAssert.AreEqual(roadIds,course.Markers.Roads.Select(r=>r.GetInstanceID()).ToArray());
        }
        [UnityTest] public IEnumerator Level2PreviewSwitchesCoursesWithoutCampaignProgress()
        {
            SandboxCampaignSession.Reset();
            var nav=GardenNavigation.Current; yield return Enable(nav);
            nav.EnterDesignLevel(DesignLevelId.WaterfallValley); yield return NavigationReady(nav);
            var level=nav.DownhillWorkshop.Downhill;
            Assert.AreEqual(DesignLevelId.WaterfallValley,DesignModeSession.SelectedLevel);
            Assert.NotNull(level.Course3D); Assert.IsFalse(level.Settings.Campaign);
            var authored=nav.DownhillWorkshop.Layout.GetComponent<AuthoredSandboxLayout>();
            Assert.AreSame(authored.Level2,level.Course3D);
            Assert.IsFalse(authored.Level1.gameObject.activeSelf);
            Assert.IsTrue(authored.Level2.gameObject.activeSelf);
            Assert.AreEqual(2,level.Presentation.Interface.CourseNumber);
            Assert.AreEqual("Waterfall Valley",level.Presentation.Interface.CourseTitle);
            Assert.IsTrue(level.IsBuilding); Assert.AreEqual(9,nav.DownhillWorkshop.World.Parts.Count);
            foreach(var part in level.Kit.Where(p=>p.Kind==PartKind.DownhillCube || p.Kind==PartKind.DownhillDrill)) {
                Assert.IsTrue(part.gameObject.activeInHierarchy);
                Assert.IsTrue(nav.DownhillWorkshop.World.Parts.ContainsKey(part.InstanceId));
            }
            Assert.IsFalse(SandboxCampaignSession.FirstComplete); Assert.IsFalse(SandboxCampaignSession.AllComplete);
            nav.ReturnToMainMenu(); yield return NavigationReady(nav);
            nav.EnterDesignLevel(DesignLevelId.GravityGarage); yield return NavigationReady(nav);
            Assert.AreEqual(DesignLevelId.GravityGarage,DesignModeSession.SelectedLevel);
            Assert.IsNull(nav.DownhillWorkshop.Downhill.Course3D);
            Assert.NotNull(nav.DownhillWorkshop.Downhill.GarageCourse);
            Assert.AreEqual(7,nav.DownhillWorkshop.World.Parts.Count);
            foreach(var part in nav.DownhillWorkshop.Downhill.Kit.Where(p=>p.Kind==PartKind.DownhillCube || p.Kind==PartKind.DownhillDrill)) {
                Assert.IsFalse(part.gameObject.activeInHierarchy);
                Assert.IsFalse(nav.DownhillWorkshop.World.Parts.ContainsKey(part.InstanceId));
            }
            nav.ReturnToMainMenu(); yield return NavigationReady(nav);
            nav.EnterDesignLevel(DesignLevelId.WaterfallValley); yield return NavigationReady(nav);
            Assert.NotNull(nav.DownhillWorkshop.Downhill.Course3D);
            Assert.AreEqual(9,nav.DownhillWorkshop.World.Parts.Count);
            nav.ReturnToMainMenu(); yield return NavigationReady(nav);
            Assert.IsTrue(nav.TrySetDesignMode(false)); yield return NavigationReady(nav);
            Assert.AreEqual(DesignLevelId.GravityGarage,DesignModeSession.SelectedLevel);
            nav.EnterSandbox(); yield return NavigationReady(nav);
            Assert.AreEqual(SandboxCourseId.GravityGarage,nav.DownhillWorkshop.Downhill.CurrentCourse);
            Assert.IsNull(nav.DownhillWorkshop.Downhill.Course3D);
        }
        [UnityTest] public IEnumerator UnsupportedModesAreNotEnteredFromDesignSession()
        {
            var nav=GardenNavigation.Current; yield return Enable(nav);
            nav.EnterMode(GameMode.NarrowGap); yield return null;
            Assert.IsTrue(nav.InMainMenu); Assert.IsFalse(nav.IsTransitioning);
            Assert.IsFalse(SceneManager.GetSceneByName("NarrowGap").isLoaded);
        }
    }
}

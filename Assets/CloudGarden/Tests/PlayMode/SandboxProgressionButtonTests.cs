using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PhysicalAssembly.Tests
{
    public sealed class SandboxProgressionButtonTests
    {
        WorkshopBootstrap workshop;
        static void CompleteRun(DownhillController level, bool success) =>
            typeof(DownhillController).GetMethod("Resolve", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(level, new object[] { success });

        void AssertSecondLevelParts(bool available)
        {
            var level = workshop.Downhill;
            foreach(var kind in new[] { PartKind.DownhillCube, PartKind.DownhillDrill })
            {
                var part = level.Kit.Single(p => p.Kind == kind);
                Assert.AreEqual(available, part.gameObject.activeInHierarchy, kind + " visibility");
                Assert.AreEqual(available, workshop.World.Parts.ContainsKey(part.InstanceId), kind + " interaction registration");
                Assert.AreEqual(available, part.Bodies.SelectMany(b => b.Colliders).Any(c => c.enabled && c.gameObject.activeInHierarchy), kind + " collision");
            }
        }

        [UnitySetUp] public IEnumerator Setup()
        {
            DesignModeSession.SetEnabled(false); SandboxCampaignSession.Reset();
            yield return SceneManager.LoadSceneAsync("DownhillSandbox"); yield return null;
            workshop = Object.FindFirstObjectByType<WorkshopBootstrap>();
            workshop.Downhill.Settings.TransitionDuration = 0;
            workshop.Downhill.Settings.CountdownStep = .01f;
            workshop.Downhill.Settings.GoDuration = .01f;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            DesignModeSession.SetEnabled(false); SandboxCampaignSession.Reset();
            yield return null;
        }

        [UnityTest] public IEnumerator CompletedFirstRunShowsReachableNextButtonAndPressAdvances()
        {
            var level = workshop.Downhill; var ui = level.Presentation.Interface;
            Assert.IsFalse(ui.NextLevelButton.gameObject.activeInHierarchy);
            AssertSecondLevelParts(false);
            Assert.AreEqual(7, level.Kit.Count(p => p.gameObject.activeInHierarchy));
            Assert.IsTrue(level.TryResetBuild());
            level.RestoreBuildImmediately();
            AssertSecondLevelParts(false);
            DesignModeSession.SetEnabled(true);
            Assert.IsTrue(level.TryLoadDesignerExample());
            DesignModeSession.SetEnabled(false);
            var ids = level.Kit.Select(p => p.InstanceId).ToArray();
            Assert.IsTrue(level.TryStart());
            yield return new WaitForSecondsRealtime(.15f);
            CompleteRun(level, true); yield return null;
            var next = ui.NextLevelButton;
            Assert.IsTrue(next.gameObject.activeInHierarchy);
            Assert.IsTrue(next.Interactable);
            Assert.AreSame(ui.LeftPanel, next.transform.parent, "Next Level belongs with the visible test controls.");
            var retry = level.Presentation.RetryButton.transform;
            Assert.Less(Vector3.Distance(retry.localScale, next.transform.localScale), .00001f);
            float retryTop = ui.LeftPanel.InverseTransformPoint(retry.position).y + retry.localScale.y * .5f;
            float nextBottom = next.transform.localPosition.y - next.transform.localScale.y * .5f;
            Assert.Greater(nextBottom, retryTop, "Next Level must sit above Retry without overlap.");
            var backing = ui.LeftPanel.Find("Action panel backing");
            float panelTop = backing.localPosition.y + backing.GetComponent<MeshFilter>().sharedMesh.bounds.extents.y * backing.localScale.y;
            Assert.Greater(panelTop, next.transform.localPosition.y + next.transform.localScale.y * .5f);
            Assert.IsTrue(next.GetComponentsInChildren<Collider>().Any(c => c.enabled));
            next.Press(); yield return null;
            Assert.AreEqual(SandboxCourseId.TeleportPipes, level.CurrentCourse);
            Assert.AreEqual(2, ui.CourseNumber);
            Assert.IsTrue(level.IsBuilding);
            Assert.IsNotNull(level.Course3D);
            Assert.AreSame(workshop.Layout.GetComponent<AuthoredSandboxLayout>().Level2,level.Course3D);
            Assert.IsFalse(next.gameObject.activeInHierarchy);
            CollectionAssert.AreEqual(ids, level.Kit.Select(p => p.InstanceId));
            AssertSecondLevelParts(true);
            Assert.IsTrue(level.TryStart());
            yield return new WaitForSecondsRealtime(.15f);
            Assert.IsTrue(level.TryRetry());
            level.RestoreBuildImmediately();
            Assert.IsTrue(level.TryResetBuild());
            AssertSecondLevelParts(true);
        }

        [UnityTest] public IEnumerator ReloadedSecondCourseIncludesCubeAndDrill()
        {
            SandboxCampaignSession.Complete(SandboxCourseId.GravityGarage);
            SandboxCampaignSession.Advance();
            yield return SceneManager.LoadSceneAsync("DownhillSandbox"); yield return null;
            workshop = Object.FindFirstObjectByType<WorkshopBootstrap>();
            Assert.AreEqual(SandboxCourseId.TeleportPipes, workshop.Downhill.CurrentCourse);
            AssertSecondLevelParts(true);
            Assert.AreEqual(9, workshop.Downhill.Kit.Count(p => p.gameObject.activeInHierarchy));
        }

        [UnityTest] public IEnumerator FailedAndDesignRunsDoNotUnlockNextLevel()
        {
            var level = workshop.Downhill; var next = level.Presentation.Interface.NextLevelButton;
            CompleteRun(level, false); yield return null;
            Assert.IsFalse(next.gameObject.activeInHierarchy);
            Assert.IsFalse(level.TryAdvanceLevel());
            level.RestoreBuildImmediately();
            AssertSecondLevelParts(false);
            DesignModeSession.SetEnabled(true);
            CompleteRun(level, true); yield return null;
            Assert.IsFalse(SandboxCampaignSession.FirstComplete);
            Assert.IsFalse(next.gameObject.activeInHierarchy);
        }
    }
}

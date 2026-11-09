using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PhysicalAssembly.Tests
{
    public sealed class GravityGarage3DCampaignTests
    {
        GameObject root;WorkshopBootstrap workshop;DownhillSettings settings;GravityGarage3DLevelAsset valley;
        [UnityTest]public IEnumerator CampaignCarryoverRetryAndBuildRestoration()
        {
            SandboxCampaignSession.Reset();DesignModeSession.SetEnabled(false);
            settings=ScriptableObject.CreateInstance<DownhillSettings>();valley=ScriptableObject.CreateInstance<GravityGarage3DLevelAsset>();valley.Scenery=false;
            settings.Level3D=valley;settings.Campaign=settings.DesignerBuilder=settings.UseGravityGarage=settings.SandboxDrillCourse=true;settings.TransitionDuration=0;settings.CountdownStep=settings.GoDuration=.01f;
            root=new GameObject("Valley campaign test");root.SetActive(false);workshop=root.AddComponent<WorkshopBootstrap>();workshop.DownhillSettings=settings;root.SetActive(true);yield return null;
            var level=workshop.Downhill;Assert.AreEqual(SandboxCourseId.GravityGarage,level.CurrentCourse);Assert.IsNull(level.Course3D);Assert.IsNotNull(level.GarageCourse);
            DesignModeSession.SetEnabled(true);Assert.IsTrue(level.TryLoadDesignerExample());DesignModeSession.SetEnabled(false);
            var ids=level.Kit.Select(p=>p.InstanceId).ToArray();var edges=workshop.World.Connections.Select(e=>e.id).ToArray();SandboxCampaignSession.Complete(SandboxCourseId.GravityGarage);
            Assert.IsTrue(level.TryAdvanceLevel());Assert.AreEqual(SandboxCourseId.TeleportPipes,level.CurrentCourse);Assert.AreSame(valley,settings.Level3D);Assert.NotNull(level.Course3D);
            CollectionAssert.AreEqual(ids,level.Kit.Select(p=>p.InstanceId));CollectionAssert.AreEqual(edges,workshop.World.Connections.Select(e=>e.id));
            for(int i=0;i<3;i++){
                Assert.IsTrue(level.TryStart());yield return WaitForRun(level);Assert.NotNull(level.Run3D);Assert.AreEqual(6,level.SimulationWorld.Connections.Count);
                Assert.IsTrue(level.TryRetry());Assert.IsFalse(level.Course3D.WallBroken);yield return WaitForRun(level);Assert.IsTrue(level.TryReturn());yield return null;
                Assert.AreEqual(DownhillState.Build,level.State);CollectionAssert.AreEqual(ids,level.Kit.Select(p=>p.InstanceId));CollectionAssert.AreEqual(edges,workshop.World.Connections.Select(e=>e.id));
            }
        }
        static IEnumerator WaitForRun(DownhillController level)
        {
            float deadline=Time.realtimeSinceStartup+5;
            while((level.State==DownhillState.Transition || level.State==DownhillState.Countdown) && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.AreEqual(DownhillState.Running,level.State,"Countdown must finish before testing retry or return.");
        }
        [TearDown]public void Cleanup()
        {
            DesignModeSession.SetEnabled(false);SandboxCampaignSession.Reset();if(workshop&&workshop.Downhill)workshop.Downhill.RestoreBuildImmediately();if(root)Object.DestroyImmediate(root);
            foreach(var part in Object.FindObjectsByType<AssemblyPart>(FindObjectsSortMode.None))Object.DestroyImmediate(part.gameObject);
            foreach(var environment in Object.FindObjectsByType<WorkshopEnvironment>(FindObjectsSortMode.None))Object.DestroyImmediate(environment.gameObject);
            if(settings)Object.DestroyImmediate(settings);if(valley)Object.DestroyImmediate(valley);
        }
    }
}

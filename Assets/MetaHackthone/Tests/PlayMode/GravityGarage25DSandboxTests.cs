using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PhysicalAssembly.Tests
{
    public sealed class GravityGarage25DSandboxTests
    {
        GameObject root;WorkshopBootstrap workshop;DownhillSettings settings;GravityGarageLevelAsset asset;
        [UnitySetUp] public IEnumerator Setup()
        {
            var scene=SceneManager.GetSceneByName("25D sandbox test");if(!scene.IsValid())scene=SceneManager.CreateScene("25D sandbox test");SceneManager.SetActiveScene(scene);
            asset=ScriptableObject.CreateInstance<GravityGarageLevelAsset>();asset.parameters=GravityGarage25DParameters.UnityCalibrated();
            settings=ScriptableObject.CreateInstance<DownhillSettings>();settings.UseGravityGarage=true;settings.Level25D=asset;settings.CountdownStep=.01f;settings.GoDuration=.01f;settings.TransitionDuration=0;
            root=new GameObject("25D sandbox fixture");root.SetActive(false);workshop=root.AddComponent<WorkshopBootstrap>();workshop.DownhillSettings=settings;root.SetActive(true);yield return null;
        }
        [TearDown] public void Cleanup()
        {
            if(workshop&&workshop.Downhill)workshop.Downhill.RestoreBuildImmediately();if(root)Object.DestroyImmediate(root);
            foreach(var p in Object.FindObjectsByType<AssemblyPart>(FindObjectsSortMode.None))Object.DestroyImmediate(p.gameObject);
            foreach(var e in Object.FindObjectsByType<WorkshopEnvironment>(FindObjectsSortMode.None))Object.DestroyImmediate(e.gameObject);
            if(settings)Object.DestroyImmediate(settings);if(asset)Object.DestroyImmediate(asset);
        }
        [UnityTest] public IEnumerator SavedLevelUsesSameGeometryAndCompletesAfterTransferAndRetry()
        {
            var level=workshop.Downhill;Assert.IsNotNull(level.Course25D);Assert.AreEqual(GravityGarageLevelAsset.Export(asset.parameters),GravityGarageLevelAsset.Export(level.Course25D.Parameters));
            Assert.AreEqual(7,level.Kit.Count);Assert.AreEqual(8,level.Core.Sockets.Length);Assert.IsTrue(level.Kit.All(p=>p.GridVersion==9));
            Assert.IsFalse(root.GetComponent<GravityGaragePanel>().Visible);level.SetGaragePlayback(3);Assert.IsTrue(level.TryStart());
            while(level.State==DownhillState.Transition||level.State==DownhillState.Countdown||level.State==DownhillState.Running)yield return null;
            Assert.AreEqual(DownhillState.Success,level.State,level.Run25D?level.Run25D.Feedback:"No 2.5D runner");Assert.AreEqual(1,level.Run25D.PipeTransfers);
            Assert.IsTrue(level.TryRetry());
            while(level.State==DownhillState.Countdown||level.State==DownhillState.Running)yield return null;
            Assert.AreEqual(DownhillState.Success,level.State,level.Run25D.Feedback);Assert.AreEqual(1,level.Run25D.GetComponents<GravityGarage25DRun>().Length);Assert.AreEqual(6,level.SimulationWorld.Connections.Count);
        }
    }
}

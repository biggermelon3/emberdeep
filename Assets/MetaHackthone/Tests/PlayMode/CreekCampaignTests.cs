using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PhysicalAssembly.Tests
{
    public sealed class CreekCampaignTests
    {
        [UnityTest] public IEnumerator SavedFirstPresetFinishesFiveRuns() => Runs(false);
        [UnityTest] public IEnumerator SavedSecondPresetFinishesFiveRuns() => Runs(true);

        IEnumerator Runs(bool second)
        {
            DesignModeSession.SetEnabled(false); SandboxCampaignSession.Reset();
            yield return SceneManager.LoadSceneAsync("DownhillSandbox");
            yield return null;
            var workshop=Object.FindFirstObjectByType<WorkshopBootstrap>();
            var level=workshop.Downhill;
            if(second) { SandboxCampaignSession.Complete(SandboxCourseId.GravityGarage); Assert.IsTrue(level.TryAdvanceLevel()); }
            for(int attempt=0;attempt<5;attempt++) {
                Assert.AreEqual(second?SandboxCourseId.TeleportPipes:SandboxCourseId.GravityGarage,level.CurrentCourse);
                DesignModeSession.SetEnabled(true);
                DesignModeSession.SelectLevel(second?DesignLevelId.WaterfallValley:DesignLevelId.GravityGarage);
                Assert.IsTrue(level.TryLoadDesignerExample(),"Preset construction failed");
                DesignModeSession.SetEnabled(false);
                Assert.IsTrue(level.TryStart());
                float deadline=Time.realtimeSinceStartup+90;
                while(!level.VictoryInProgress && level.State!=DownhillState.Success && level.State!=DownhillState.Failed && Time.realtimeSinceStartup<deadline)
                    yield return null;
                Assert.IsTrue(level.VictoryInProgress || level.State==DownhillState.Success,
                    "Attempt "+attempt+": "+workshop.Session.Status+" / "+(level.Run3D?level.Run3D.Feedback:level.GarageResult?.Outcome)+" position="+(level.SimulationCore?level.SimulationCore.transform.position.ToString("F4"):"returned")+" result="+JsonUtility.ToJson(level.GarageResult));
                Debug.Log("CREEK_PRESET_PASS level="+(second?2:1)+" attempt="+(attempt+1));
                float returnDeadline=Time.realtimeSinceStartup+5;
                while(level.VictoryInProgress && Time.realtimeSinceStartup<returnDeadline) yield return null;
                Assert.IsFalse(level.VictoryInProgress,"Victory return did not finish");
                Debug.Log("CREEK_RETURN_PASS attempt="+(attempt+1));
            }
        }
        [TearDown] public void Cleanup() { DesignModeSession.SetEnabled(false); SandboxCampaignSession.Reset(); }
    }
}

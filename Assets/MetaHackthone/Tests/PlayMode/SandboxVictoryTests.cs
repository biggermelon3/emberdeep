using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PhysicalAssembly.Tests
{
    public sealed class SandboxVictoryTests
    {
        GameObject root;
        WorkshopBootstrap workshop;
        DownhillController level;
        DownhillSettings settings;
        GravityGarageLevelAsset course;
        SandboxPalmMenu menu;
        static void Resolve(DownhillController l) => typeof(DownhillController).GetMethod("Resolve",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(l,new object[]{true});
        [UnitySetUp] public IEnumerator Setup()
        {
            DesignModeSession.SetEnabled(false); SandboxCampaignSession.Reset();
            var scene=SceneManager.GetSceneByName("Victory fixture"); if(!scene.IsValid()) scene=SceneManager.CreateScene("Victory fixture"); SceneManager.SetActiveScene(scene);
            settings=ScriptableObject.CreateInstance<DownhillSettings>();
            settings.DesignerBuilder=settings.UseGravityGarage=settings.Campaign=settings.SandboxDrillCourse=true;
            settings.TransitionDuration=0; settings.CountdownStep=.01f; settings.GoDuration=.01f;
            course=ScriptableObject.CreateInstance<GravityGarageLevelAsset>(); course.parameters=GravityGarage25DParameters.UnityCalibrated(); settings.Level25D=course;
            root=new GameObject("Victory test"); root.SetActive(false);
            workshop=root.AddComponent<WorkshopBootstrap>(); workshop.DownhillSettings=settings; root.SetActive(true);
            level=workshop.Downhill; level.Course.SandboxPolish=true;
            menu=root.AddComponent<SandboxPalmMenu>(); menu.Initialize(level); menu.enabled=false;
            typeof(DownhillController).GetProperty("PalmMenu").SetValue(level,menu);
            yield return null;
        }
        [TearDown] public void Cleanup()
        {
            DesignModeSession.SetEnabled(false);
            if(level) level.RestoreBuildImmediately();
            if(root) Object.DestroyImmediate(root);
            foreach(var part in Object.FindObjectsByType<AssemblyPart>(FindObjectsInactive.Include,FindObjectsSortMode.None)) Object.DestroyImmediate(part.gameObject);
            foreach(var env in Object.FindObjectsByType<WorkshopEnvironment>(FindObjectsSortMode.None)) Object.DestroyImmediate(env.gameObject);
            Object.DestroyImmediate(settings); Object.DestroyImmediate(course); SandboxCampaignSession.Reset();
        }
        void Open() => menu.Sample(true,new Vector3(0,1.2f,.4f),Vector3.up,Vector3.up*1.6f,.36f,3);
        void Down() => menu.Sample(true,new Vector3(0,1.2f,.4f),Vector3.down,Vector3.up*1.6f,.21f,0);
        [UnityTest] public IEnumerator WinningCopiesReturnSeparatelyAndLeaveLoosePartsUntouched()
        {
            var spawn=level.Kit.ToDictionary(p=>p,p=>p.Bodies.Select(b=>new Pose(b.transform.position,b.transform.rotation)).ToArray());
            DesignModeSession.SetEnabled(true); Assert.IsTrue(level.TryLoadDesignerExample()); DesignModeSession.SetEnabled(false);
            Assert.IsTrue(workshop.World.TryDetachPart(level.Kit[8]));
            var loose=level.Kit.First(p=>p.gameObject.activeSelf && !level.IsCoreConnected(p));
            var loosePosition=loose.MainBody.Rigidbody.position+Vector3.up*.015f;
            loose.MainBody.Rigidbody.position=loosePosition; loose.MainBody.transform.position=loosePosition;
            Assert.That(level.VictoryDecomposeSeconds+level.VictoryTravelSeconds+level.VictoryReconstructSeconds,Is.EqualTo(1.5f).Within(.001));
            Assert.IsTrue(level.TryStart()); yield return new WaitForSecondsRealtime(.08f);
            var mapping=level.SimulationOriginals.ToArray(); Assert.Greater(mapping.Length,1);
            Assert.IsTrue(mapping.All(p=>p.Key.GeometryScale!=p.Value.GeometryScale));
            Resolve(level); Resolve(level);
            Assert.IsTrue(level.VictoryInProgress); Assert.AreEqual(mapping.Length,root.GetComponentsInChildren<SandboxReturnEffect>().Length);
            Assert.IsFalse(level.TryRetry()); Assert.IsFalse(level.TryStart()); Assert.IsFalse(level.TryReturn()); Assert.IsFalse(level.TryResetBuild()); Assert.IsFalse(level.TryAdvanceLevel());
            Assert.IsTrue(level.Presentation.ExitButton.Interactable);
            Assert.IsTrue(level.SimulationWorld.Parts.Values.SelectMany(p=>p.Bodies).All(b=>b.Rigidbody.isKinematic));
            Assert.LessOrEqual(SandboxFragments.ActiveCount,SandboxFragments.Budget);
            yield return new WaitForSecondsRealtime(1.65f);
            Assert.IsTrue(level.IsBuilding); Assert.IsFalse(level.VictoryInProgress); Assert.IsNull(level.SimulationWorld);
            foreach(var pair in mapping) for(int i=0;i<pair.Value.Bodies.Length;i++) {
                var b=pair.Value.Bodies[i]; var expected=spawn[pair.Value][i];
                Assert.Less(Vector3.Distance(expected.position,b.transform.position),.001f,pair.Value.name);
                Assert.Less(Quaternion.Angle(expected.rotation,b.transform.rotation),.1f);
                Assert.IsTrue(b.Colliders.All(c=>c.enabled)); Assert.IsTrue(b.Rigidbody.isKinematic);
            }
            Assert.AreEqual(0,workshop.World.Connections.Count);
            Assert.Less(Vector3.Distance(loosePosition,loose.MainBody.transform.position),.001f);
            Assert.IsTrue(level.CanAdvanceLevel); Assert.IsTrue(workshop.Session.Status.Contains("COMPLETE"));
            Assert.IsFalse(level.Kit.Single(p=>p.Kind==PartKind.DownhillDrill).gameObject.activeSelf);
            Assert.IsNotNull(level.Reward.Preview);
        }
        [UnityTest] public IEnumerator RewardRedirectsReversesAndClaimsOnlyOnSuccessfulAdvance()
        {
            SandboxCampaignSession.Complete(SandboxCourseId.GravityGarage); yield return null;
            var reward=level.Reward; Assert.IsNotNull(reward); var preview=reward.Preview;
            Assert.IsEmpty(preview.GetComponentsInChildren<Collider>()); Assert.IsEmpty(preview.GetComponentsInChildren<AssemblySocket>());
            Assert.IsEmpty(preview.GetComponentsInChildren<AssemblyPart>()); Assert.IsEmpty(preview.GetComponentsInChildren<Rigidbody>());
            Open(); Assert.AreEqual(SandboxRewardState.ToMenu,reward.State); Assert.IsFalse(SandboxCampaignSession.DrillClaimed);
            reward.Tick(.2f); var midway=preview.position; Down(); Assert.AreEqual(midway,preview.position);
            Assert.AreEqual(SandboxRewardState.ToGoal,reward.State);
            reward.Tick(.1f); midway=preview.position; Open(); Assert.AreEqual(midway,preview.position);
            reward.Tick(.46f); Assert.AreEqual(SandboxRewardState.AtMenu,reward.State);
            Assert.IsTrue(level.Presentation.Interface.NextLevelButton.GetComponent<FloatingStartButton>().RewardPulse);
            Down(); reward.Tick(.46f); Assert.AreEqual(SandboxRewardState.Delivering,reward.State);
            menu.Sample(false,default,default,default,.1f); reward.Tick(1);
            Assert.AreEqual(SandboxRewardState.Delivering,reward.State); Assert.IsFalse(reward.HaloVisible);
            menu.Sample(true,Vector3.up*1.2f,Vector3.right,Vector3.up*1.6f,.01f,0);
            reward.Tick(.7f); reward.Tick(.01f); Assert.IsTrue(reward.HaloVisible);
            var goalLocal=level.RewardGoal.InverseTransformPoint(preview.position);
            level.Course.SandboxFrame.position+=Vector3.right; reward.Tick(.01f);
            Assert.Less(Vector3.Distance(level.RewardGoal.TransformPoint(goalLocal),preview.position),.001f);
            Open(); Assert.IsFalse(reward.HaloVisible); reward.Tick(.46f);
            var drill=level.Kit.Single(p=>p.Kind==PartKind.DownhillDrill);
            Assert.IsFalse(drill.gameObject.activeSelf); Assert.IsFalse(workshop.World.Parts.ContainsKey(drill.InstanceId));
            Assert.IsTrue(level.TryAdvanceLevel()); Assert.IsFalse(level.TryAdvanceLevel());
            Assert.AreEqual(SandboxRewardState.Claimed,reward.State); Assert.IsNull(reward.Preview);
            Assert.IsTrue(SandboxCampaignSession.DrillClaimed); Assert.IsTrue(drill.gameObject.activeSelf); Assert.IsTrue(workshop.World.Parts.ContainsKey(drill.InstanceId));
            Assert.IsFalse(level.Presentation.Interface.NextLevelButton.GetComponent<FloatingStartButton>().RewardPulse);
        }
        [UnityTest] public IEnumerator QuitDuringVictoryCleansEffectsAndFinalLevelHasNoReward()
        {
            Assert.IsTrue(level.TryStart()); yield return new WaitForSecondsRealtime(.08f);
            Resolve(level); Assert.IsTrue(level.VictoryInProgress); level.Exit(); yield return null;
            Assert.IsFalse(level.VictoryInProgress); Assert.IsTrue(level.IsBuilding); Assert.AreEqual(0,root.GetComponentsInChildren<SandboxReturnEffect>().Length);
            Assert.IsTrue(!level.Reward || !level.Reward.Preview,"Quit must not recreate a pending reward");
            Assert.IsTrue(level.TryAdvanceLevel());
            Assert.IsTrue(level.TryStart()); yield return new WaitForSecondsRealtime(.08f); Resolve(level);
            yield return new WaitForSecondsRealtime(1.65f);
            Assert.IsTrue(level.IsBuilding); Assert.IsTrue(SandboxCampaignSession.AllComplete); Assert.IsFalse(level.CanAdvanceLevel);
            Assert.IsTrue(!level.Reward || !level.Reward.Preview);
        }
        [UnityTest] public IEnumerator ReminderResumesAfterCoordinatorRecreationWithoutDuplicates()
        {
            SandboxCampaignSession.Complete(SandboxCourseId.GravityGarage); yield return null;
            var old=level.Reward; old.Cancel(); Object.Destroy(old); yield return null; yield return null;
            Assert.IsNotNull(level.Reward.Preview); level.Reward.Reveal(); level.Reward.Reveal();
            Assert.AreEqual(1,root.GetComponents<SandboxRewardCoordinator>().Length);
            Assert.IsFalse(SandboxCampaignSession.DrillClaimed);
            level.Exit(); yield return null; Assert.IsNull(level.Reward.Preview);
        }
        [UnityTest] public IEnumerator RepeatedWinsReusePendingRewardAndReleaseFragmentBudget()
        {
            Transform preview=null;
            for(int i=0;i<2;i++) {
                Assert.IsTrue(level.TryStart()); yield return new WaitForSecondsRealtime(.08f); Resolve(level);
                yield return new WaitForSecondsRealtime(1.65f);
                Assert.IsTrue(level.IsBuilding); Assert.IsNotNull(level.Reward.Preview);
                if(preview) Assert.AreSame(preview,level.Reward.Preview); else preview=level.Reward.Preview;
                Assert.AreEqual(1,root.GetComponents<SandboxRewardCoordinator>().Length);
                Assert.LessOrEqual(SandboxFragments.ActiveCount,96);
            }
            level.Reward.Cancel(); Assert.AreEqual(0,SandboxFragments.ActiveCount);
        }
        [UnityTest] public IEnumerator SceneReloadRestoresUnclaimedReminderWithoutKeepingOldVisuals()
        {
            SandboxCampaignSession.Complete(SandboxCourseId.GravityGarage); yield return null;
            var oldPreview=level.Reward.Preview;
            var previous=root.scene;
            var fresh=SceneManager.CreateScene("Victory reload fixture"); SceneManager.SetActiveScene(fresh);
            yield return SceneManager.UnloadSceneAsync(previous);
            Assert.IsFalse(oldPreview); Assert.AreEqual(0,SandboxFragments.ActiveCount);
            settings.Level25D=course;
            root=new GameObject("Reloaded victory test"); root.SetActive(false);
            workshop=root.AddComponent<WorkshopBootstrap>(); workshop.DownhillSettings=settings; root.SetActive(true);
            level=workshop.Downhill; level.Course.SandboxPolish=true;
            menu=root.AddComponent<SandboxPalmMenu>(); menu.Initialize(level); menu.enabled=false;
            typeof(DownhillController).GetProperty("PalmMenu").SetValue(level,menu);
            yield return null; yield return null;
            Assert.IsTrue(SandboxCampaignSession.FirstComplete); Assert.IsFalse(SandboxCampaignSession.DrillClaimed);
            Assert.IsNotNull(level.Reward.Preview); Assert.IsFalse(level.Kit.Single(p=>p.Kind==PartKind.DownhillDrill).gameObject.activeSelf);
        }
    }
}

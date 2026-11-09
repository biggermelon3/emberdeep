using System.Collections;
using System.Linq;
using CloudGarden.Integration;
using NUnit.Framework;
using PhysicalAssembly;
using PhysicalAssembly.MetaXR;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CloudGarden.Tests
{
    public sealed class GardenNavigationTests
    {
        static IEnumerator WaitForTransition(GardenNavigation navigation)
        {
            float deadline = Time.realtimeSinceStartup + 20;
            while (navigation && navigation.IsTransitioning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(!navigation || !navigation.IsTransitioning, "Scene transition timed out");
            yield return null;
        }
        [UnityTest] public IEnumerator RoundTripRetainsConstructionAndUsesOnePlayer()
        {
            yield return SceneManager.LoadSceneAsync("Workshop"); yield return null;
            var workshop = Object.FindFirstObjectByType<WorkshopBootstrap>();
            Assert.IsTrue(workshop.CreateTestCar());
            workshop.Session.EnterBuild();
            var navigation = GardenNavigation.Current;
            var player = navigation.Player;
            var parts = workshop.World.Parts.Values.ToArray();
            var ids = workshop.World.Parts.Keys.OrderBy(k => k).ToArray();
            int connections = workshop.World.Connections.Count;
            var selected = workshop.Session.Selected;
            var arena = workshop.Arena.position;
            for (int repeat = 0; repeat < 2; repeat++)
            {
                navigation.EnterGarden(); navigation.EnterGarden();
                yield return WaitForTransition(navigation);
                Assert.IsTrue(navigation.InGarden);
                Assert.AreSame(player, navigation.Player);
                Assert.AreEqual(1, Object.FindObjectsByType<MetaPlayerRig>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
                Assert.AreEqual(1, Object.FindObjectsByType<CloudGardenGame>(FindObjectsSortMode.None).Length);
                Assert.IsFalse(workshop.gameObject.activeInHierarchy);
                Assert.IsTrue(parts.All(p => p && !p.gameObject.activeInHierarchy));
                var frozen = parts.Select(p => p.MainBody.Rigidbody.position).ToArray();
                yield return new WaitForSeconds(.15f);
                for (int i = 0; i < parts.Length; i++) Assert.AreEqual(frozen[i], parts[i].MainBody.Rigidbody.position);
                navigation.ReturnToWorkshop(); navigation.ReturnToWorkshop();
                yield return WaitForTransition(navigation);
                Assert.IsFalse(navigation.InGarden);
                CollectionAssert.AreEqual(ids, workshop.World.Parts.Keys.OrderBy(k => k).ToArray());
                Assert.AreEqual(connections, workshop.World.Connections.Count);
                Assert.AreSame(selected, workshop.Session.Selected); Assert.AreEqual(arena, workshop.Arena.position);
                Assert.AreEqual(WorkshopMode.Build, workshop.Session.Mode);
                Assert.AreEqual(HandCommand.None, workshop.Session.Left); Assert.AreEqual(HandCommand.None, workshop.Session.Right);
                Assert.AreEqual(0, Object.FindObjectsByType<CloudGardenGame>(FindObjectsSortMode.None).Length);
            }
        }
        [UnityTest] public IEnumerator HeldPartBlocksEntry()
        {
            yield return SceneManager.LoadSceneAsync("Workshop"); yield return null;
            var workshop = Object.FindFirstObjectByType<WorkshopBootstrap>();
            var part = workshop.Session.Selected;
            // A synthetic hand must not share the desktop adapter's live hand 2.
            Assert.IsTrue(workshop.World.TryGrab(99, part, new Pose(part.transform.position, Quaternion.identity)));
            GardenNavigation.Current.EnterGarden(); yield return null;
            Assert.IsFalse(GardenNavigation.Current.IsTransitioning);
            Assert.IsFalse(GardenNavigation.Current.InGarden);
            Assert.IsTrue(workshop.gameObject.activeInHierarchy);
            workshop.World.ReleaseAll();
        }
        [UnityTest] public IEnumerator DownhillRoundTripKeepsBuildAndRestoresRunningAttempt()
        {
            yield return SceneManager.LoadSceneAsync("Workshop"); yield return null;
            var workshop=Object.FindFirstObjectByType<WorkshopBootstrap>();
            var ids=workshop.World.Parts.Keys.OrderBy(x=>x).ToArray();
            var navigation=GardenNavigation.Current; var player=navigation.Player;
            navigation.EnterDownhill(); navigation.EnterDownhill(); yield return WaitForTransition(navigation); yield return null;
            Assert.IsTrue(navigation.InDownhill); var downhill=navigation.DownhillWorkshop; Assert.NotNull(downhill);
            var level=downhill.Downhill; var core=level.Core; var pose=new Pose(core.MainBody.Rigidbody.position,Quaternion.identity);
            var saved=core.MainBody.Rigidbody.position; Assert.IsTrue(level.TryStart()); yield return new WaitForFixedUpdate();
            navigation.ReturnToWorkshop(); yield return WaitForTransition(navigation);
            Assert.IsFalse(navigation.InDownhill); Assert.IsTrue(level.IsBuilding); Assert.IsNull(level.SimulationWorld);
            CollectionAssert.AreEqual(ids,workshop.World.Parts.Keys.OrderBy(x=>x));
            navigation.EnterDownhill(); yield return WaitForTransition(navigation);
            Assert.AreSame(downhill,navigation.DownhillWorkshop); Assert.AreSame(player,navigation.Player);
            Assert.Less(Vector3.Distance(saved,core.MainBody.Rigidbody.position),.0001f);
            Assert.AreEqual(1,Object.FindObjectsByType<MetaPlayerRig>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length);
            navigation.ReturnToWorkshop(); yield return WaitForTransition(navigation);
        }
        [UnityTest] public IEnumerator DirectDownhillEntryUsesOneRigAndReturns()
        {
            yield return SceneManager.LoadSceneAsync("DownhillSandbox"); yield return null; yield return null;
            var navigation=GardenNavigation.Current; Assert.IsTrue(navigation.InDownhill); Assert.NotNull(navigation.DownhillWorkshop);
            Assert.AreEqual(1,Object.FindObjectsByType<MetaPlayerRig>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length);
            navigation.ReturnToWorkshop();
            float deadline=Time.realtimeSinceStartup+20;
            while(SceneManager.GetActiveScene().name!="MainMenu" && Time.realtimeSinceStartup<deadline) yield return null;
            yield return null; Assert.AreEqual("MainMenu",SceneManager.GetActiveScene().name);
        }
        [UnityTest] public IEnumerator NarrowGapRoundTripKeepsBothConstructionsAndSharedPlayer()
        {
            yield return SceneManager.LoadSceneAsync("Workshop"); yield return null;
            var workshop=Object.FindFirstObjectByType<WorkshopBootstrap>();
            Assert.IsTrue(workshop.CreateTestCar()); workshop.Session.EnterBuild();
            var ids=workshop.World.Parts.Keys.OrderBy(x=>x).ToArray(); var edges=workshop.World.Connections.Select(c=>c.id).ToArray();
            var navigation=GardenNavigation.Current;
            navigation.EnterNarrowGap(); navigation.EnterNarrowGap(); yield return WaitForTransition(navigation); yield return null;
            Assert.IsTrue(navigation.InNarrowGap); Assert.IsFalse(navigation.InGarden);
            var challenge=navigation.ChallengeWorkshop; Assert.NotNull(challenge); Assert.IsTrue(challenge.IsChallenge);
            Assert.AreEqual(38,challenge.World.Parts.Count); Assert.AreEqual(5,challenge.Challenge.Spheres.Count);
            var challengeIds=challenge.World.Parts.Keys.OrderBy(x=>x).ToArray();
            Assert.AreEqual(1,Object.FindObjectsByType<MetaPlayerRig>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length);
            Assert.IsFalse(workshop.gameObject.activeInHierarchy);
            var sphere=challenge.Challenge.Spheres[0]; var pose=sphere.MainBody.Rigidbody.position;
            // Place a load and leave while its attempt is running. It must pause.
            sphere.MainBody.Rigidbody.position=challenge.Arena.TransformPoint(new Vector3(0,.74f,0));
            var launchButton=challenge.Menu.GetComponentsInChildren<MetaMenuButton>().Single(b=>b.name=="Launch");
            launchButton.Ray.PublishPointerEvent(new Oculus.Interaction.PointerEvent(777,Oculus.Interaction.PointerEventType.Select,new Pose(launchButton.transform.position,Quaternion.identity)));
            Assert.AreEqual(ChallengeState.InFlight,challenge.Challenge.State); yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            navigation.ReturnToWorkshop(); yield return WaitForTransition(navigation);
            Assert.IsFalse(navigation.InNarrowGap); Assert.IsFalse(challenge.gameObject.activeInHierarchy);
            var frozen=sphere.MainBody.Rigidbody.position; yield return new WaitForSeconds(.15f); Assert.AreEqual(frozen,sphere.MainBody.Rigidbody.position);
            CollectionAssert.AreEqual(ids,workshop.World.Parts.Keys.OrderBy(x=>x)); CollectionAssert.AreEqual(edges,workshop.World.Connections.Select(c=>c.id));
            navigation.EnterNarrowGap(); yield return WaitForTransition(navigation);
            Assert.AreSame(challenge,navigation.ChallengeWorkshop); CollectionAssert.AreEqual(challengeIds,challenge.World.Parts.Keys.OrderBy(x=>x));
            Assert.AreEqual(ChallengeState.InFlight,challenge.Challenge.State);
            var retryButton=challenge.Menu.GetComponentsInChildren<MetaMenuButton>().Single(b=>b.name=="Retry");
            retryButton.Poke.PublishPointerEvent(new Oculus.Interaction.PointerEvent(778,Oculus.Interaction.PointerEventType.Select,new Pose(retryButton.transform.position,Quaternion.identity)));
            Assert.AreEqual(ChallengeState.LaunchReady,challenge.Challenge.State);
            Assert.IsTrue(challenge.World.EditingEnabled);
            var resetButton=challenge.Menu.GetComponentsInChildren<MetaMenuButton>().Single(b=>b.name=="Reset Level");
            resetButton.Poke.PublishPointerEvent(new Oculus.Interaction.PointerEvent(779,Oculus.Interaction.PointerEventType.Select,new Pose(resetButton.transform.position,Quaternion.identity)));
            Assert.AreEqual(38,challenge.World.Parts.Count); Assert.AreEqual(5,challenge.Challenge.Spheres.Count);
            Assert.IsFalse(challenge.World.Parts.Keys.Intersect(challengeIds).Any());
            var beforeAim=challenge.Challenge.Core.MainBody.Rigidbody.position;
            Assert.IsTrue(challenge.Challenge.BeginAim());
            challenge.Challenge.UpdateAim(challenge.Arena.TransformDirection(new Vector3(1,1,0)),1);
            navigation.ReturnToWorkshop(); yield return WaitForTransition(navigation);
            Assert.IsFalse(challenge.Challenge.IsAiming,"Leaving the level cancels aiming.");
            Assert.That(Vector3.Distance(beforeAim,challenge.Challenge.Core.MainBody.transform.position),Is.LessThan(.0001f),"Suspended bodies retain their restored transform.");
            Assert.IsFalse(challenge.Challenge.Course.Trajectory.gameObject.activeSelf);
            navigation.EnterNarrowGap(); yield return WaitForTransition(navigation);
            Assert.That(Vector3.Distance(beforeAim,challenge.Challenge.Core.MainBody.Rigidbody.position),Is.LessThan(.0001f),"Resumed physics starts at the pre-aim pose.");
            navigation.ReturnToWorkshop(); yield return WaitForTransition(navigation);
            navigation.EnterGarden(); yield return WaitForTransition(navigation); Assert.IsTrue(navigation.InGarden);
            navigation.ReturnToWorkshop(); yield return WaitForTransition(navigation); CollectionAssert.AreEqual(ids,workshop.World.Parts.Keys.OrderBy(x=>x));
        }
        [UnityTest] public IEnumerator DirectNarrowGapEntryUsesOneRigAndReturns()
        {
            yield return SceneManager.LoadSceneAsync("NarrowGap"); yield return null; yield return null;
            var navigation=GardenNavigation.Current;
            Assert.IsTrue(navigation.InNarrowGap); Assert.NotNull(navigation.ChallengeWorkshop);
            Assert.AreEqual(1,Object.FindObjectsByType<MetaPlayerRig>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length);
            navigation.ReturnToWorkshop();
            float deadline=Time.realtimeSinceStartup+20;
            while(SceneManager.GetActiveScene().name!="MainMenu" && Time.realtimeSinceStartup<deadline) yield return null;
            yield return null;
            Assert.IsTrue(GardenNavigation.Current.InMainMenu); Assert.IsNull(Object.FindFirstObjectByType<WorkshopBootstrap>());
        }
        [UnityTest] public IEnumerator DirectDemoEntryUsesMetaPlayerAndReturnsToWorkshop()
        {
            yield return SceneManager.LoadSceneAsync("CloudGardenDemo"); yield return null; yield return null;
            var game = Object.FindFirstObjectByType<CloudGardenGame>();
            Assert.NotNull(game); Assert.IsTrue(game.UsesExternalPlayer);
            Assert.AreEqual(1, Object.FindObjectsByType<MetaPlayerRig>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
            Assert.IsNull(Object.FindFirstObjectByType<WorkshopBootstrap>());
            game.ReturnToWorkshop();
            float deadline = Time.realtimeSinceStartup + 20;
            while (SceneManager.GetActiveScene().name != "MainMenu" && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.IsTrue(GardenNavigation.Current.InMainMenu); Assert.IsNull(Object.FindFirstObjectByType<WorkshopBootstrap>());
            Assert.AreEqual(0, Object.FindObjectsByType<CloudGardenGame>(FindObjectsSortMode.None).Length);
        }
    }
}

using System.Collections;
using System.Linq;
using CloudGarden.Integration;
using NUnit.Framework;
using Oculus.Interaction;
using PhysicalAssembly;
using PhysicalAssembly.MetaXR;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CloudGarden.Tests
{
    public sealed class MainMenuTests
    {
        [UnitySetUp] public IEnumerator Setup()
        {
            PlayAreaPose.Reset();
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null; yield return null; yield return new WaitForSeconds(.3f);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            PlayAreaPose.Reset();
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return null;
        }
        static IEnumerator Wait(GardenNavigation nav)
        {
            float deadline=Time.realtimeSinceStartup+25;
            while(nav.IsTransitioning && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.IsFalse(nav.IsTransitioning,"Transition timed out"); yield return null; yield return null;
        }
        static void OnePlayer()
        {
            Assert.AreEqual(1,Object.FindObjectsByType<MetaPlayerRig>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length);
            Assert.AreEqual(1,Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(l=>l.isActiveAndEnabled));
        }
        [UnityTest] public IEnumerator ColdStartupContainsWorkbenchAndOtherModes()
        {
            var nav=GardenNavigation.Current; Assert.IsTrue(nav.InMainMenu); Assert.IsTrue(nav.Menu.Visible);
            var buttons=nav.Menu.Panel.GetComponentsInChildren<WorkshopButton>();
            CollectionAssert.AreEquivalent(new[]{"Narrow Gap","WORKBENCH BUILD","Shadow","Car Workshop","Cloud Garden","Place / Move Play Area"},buttons.Select(b=>b.Command));
            Assert.IsNull(Object.FindFirstObjectByType<WorkshopBootstrap>());
            Assert.IsEmpty(Object.FindObjectsByType<AssemblyPart>(FindObjectsInactive.Include,FindObjectsSortMode.None));
            Assert.AreEqual(1,SceneManager.sceneCount); OnePlayer(); yield return null;
            Assert.AreEqual("Assets/MetaHackthone/Scenes/MainMenu.unity",SceneUtility.GetScenePathByBuildIndex(0));
            foreach(GameMode mode in System.Enum.GetValues(typeof(GameMode))) Assert.IsTrue(Application.CanStreamedLevelBeLoaded(GardenNavigation.SceneFor(mode)));
        }
        [UnityTest] public IEnumerator EveryModeRoundTripsAndRepeatedSelectDoesNotDuplicateScenes()
        {
            var nav=GardenNavigation.Current; var player=nav.Player;
            foreach(var mode in new[]{GameMode.NarrowGap,GameMode.Sandbox,GameMode.Shadow,GameMode.CarWorkshop,GameMode.CloudGarden})
            {
                nav.EnterMode(mode); nav.EnterMode(mode); yield return Wait(nav);
                Assert.AreEqual(GardenNavigation.SceneFor(mode),SceneManager.GetActiveScene().name);
                Assert.IsFalse(nav.Menu.Visible); Assert.AreSame(player,nav.Player); OnePlayer();
                nav.ReturnToMainMenu(); nav.ReturnToMainMenu(); yield return Wait(nav);
                Assert.IsTrue(nav.InMainMenu); Assert.IsTrue(nav.Menu.Visible);
                Assert.IsNull(Object.FindFirstObjectByType<WorkshopBootstrap>()); OnePlayer();
            }
        }
        [UnityTest] public IEnumerator WorkbenchButtonResumesConstructionAndPreservesRig()
        {
            var nav=GardenNavigation.Current;
            var buttons=nav.Menu.Panel.GetComponentsInChildren<MetaMenuButton>();
            var bench=buttons.Single(b=>b.name=="WORKBENCH BUILD");
            Assert.AreEqual(0,bench.transform.localPosition.x,.0001f);
            var rig=nav.Player.XRRig; var pose=new Pose(rig.position,rig.rotation);
            bench.Ray.PublishPointerEvent(new PointerEvent(500,PointerEventType.Select,default)); yield return Wait(nav);
            var level=nav.DownhillWorkshop.Downhill;
            Assert.IsTrue(level.Presentation.WorkbenchVisuals.gameObject.activeInHierarchy);
            DesignModeSession.SetEnabled(true); Assert.IsTrue(level.TryLoadDesignerExample()); DesignModeSession.SetEnabled(false);
            var ids=level.Kit.Select(p=>p.InstanceId).ToArray();
            nav.ReturnToMainMenu(); yield return Wait(nav); yield return new WaitForSecondsRealtime(.35f);
            bench.Poke.PublishPointerEvent(new PointerEvent(501,PointerEventType.Select,default)); yield return Wait(nav);
            Assert.IsTrue(level.Presentation.WorkbenchVisuals.gameObject.activeInHierarchy);
            Assert.AreEqual(9,nav.DownhillWorkshop.World.Parts.Count); Assert.AreEqual(6,nav.DownhillWorkshop.World.Connections.Count);
            CollectionAssert.AreEqual(ids,level.Kit.Select(p=>p.InstanceId));
            Assert.That(level.TransferScale,Is.EqualTo(level.TestScale/level.Core.GeometryScale).Within(.0001f));
            Assert.AreEqual(pose.position,rig.position); Assert.AreEqual(pose.rotation,rig.rotation); OnePlayer();
        }
        [UnityTest] public IEnumerator RayAndPokeSelectModesThroughActualMenuButtons()
        {
            var nav=GardenNavigation.Current;
            var button=nav.Menu.Panel.GetComponentsInChildren<MetaMenuButton>().Single(b=>b.name=="Car Workshop");
            button.Ray.PublishPointerEvent(new PointerEvent(90,PointerEventType.Select,default)); yield return Wait(nav);
            Assert.AreEqual("Workshop",SceneManager.GetActiveScene().name);
            var back=nav.CarWorkshop.Menu.GetComponentsInChildren<MetaMenuButton>().Single(b=>b.name=="Main Menu");
            back.Poke.PublishPointerEvent(new PointerEvent(91,PointerEventType.Select,default)); yield return Wait(nav);
            Assert.IsTrue(nav.InMainMenu);
            yield return new WaitForSeconds(.35f);
            button=nav.Menu.Panel.GetComponentsInChildren<MetaMenuButton>().Single(b=>b.name=="Narrow Gap");
            button.Poke.PublishPointerEvent(new PointerEvent(92,PointerEventType.Select,default)); yield return Wait(nav);
            Assert.IsTrue(nav.InNarrowGap);
        }
        [UnityTest] public IEnumerator PlacementRequiresReleaseAndCancelPreservesPreviousPose()
        {
            var nav=GardenNavigation.Current; var menu=nav.Menu;
            menu.TogglePlacement(); var point=new Vector3(1.2f,0,2);
            menu.Placement.Target.PublishPointerEvent(new PointerEvent(93,PointerEventType.Select,new Pose(point,Quaternion.identity)));
            Assert.IsTrue(menu.PlacementActive); Assert.IsFalse(PlayAreaPose.IsSet);
            nav.EnterMode(GameMode.CarWorkshop); Assert.IsFalse(nav.IsTransitioning);
            yield return null; yield return null;
            menu.Placement.Target.PublishPointerEvent(new PointerEvent(93,PointerEventType.Unselect,default));
            menu.Placement.Target.PublishPointerEvent(new PointerEvent(93,PointerEventType.Select,new Pose(point,Quaternion.identity)));
            Assert.IsFalse(menu.PlacementActive); Assert.IsTrue(PlayAreaPose.IsSet);
            Assert.That(Vector3.Distance(point,PlayAreaPose.Pose.position),Is.LessThan(.001f));
            var saved=PlayAreaPose.Pose; menu.TogglePlacement(); menu.TogglePlacement();
            Assert.AreEqual(saved.position,PlayAreaPose.Pose.position); Assert.IsNull(Object.FindFirstObjectByType<WorkshopBootstrap>());
            nav.EnterMode(GameMode.CarWorkshop); yield return Wait(nav);
            Assert.That(Vector3.Distance(point,nav.CarWorkshop.Arena.position),Is.LessThan(.001f));
        }
        [UnityTest] public IEnumerator RelocatingRetainedWorkshopPreservesConstructionAndLocalPoses()
        {
            var simulation=Physics.simulationMode; Physics.simulationMode=SimulationMode.Script;
            try
            {
            var nav=GardenNavigation.Current;
            nav.EnterMode(GameMode.CarWorkshop); yield return Wait(nav);
            var owner=nav.CarWorkshop; Assert.IsTrue(owner.CreateTestCar()); owner.Session.EnterBuild();
            var ids=owner.World.Parts.Keys.OrderBy(x=>x).ToArray(); var connections=owner.World.Connections.Select(c=>c.id).ToArray();
            nav.ReturnToMainMenu(); yield return Wait(nav);
            // Inactive rigidbodies may report zero after their native physics actor is removed.
            // The suspended render transforms retain the authoritative saved poses.
            var parts=owner.World.Parts.Values.ToArray(); var positions=parts.Select(p=>owner.Arena.InverseTransformPoint(p.MainBody.transform.position)).ToArray();
            var frozen=parts.Select(p=>p.MainBody.transform.position).ToArray(); yield return new WaitForSeconds(.2f);
            CollectionAssert.AreEqual(frozen,parts.Select(p=>p.MainBody.transform.position));
            PlayAreaPose.Set(new Vector3(2,0,3),Quaternion.Euler(0,65,0),0);
            nav.EnterMode(GameMode.CarWorkshop); yield return Wait(nav);
            CollectionAssert.AreEqual(ids,owner.World.Parts.Keys.OrderBy(x=>x)); CollectionAssert.AreEqual(connections,owner.World.Connections.Select(c=>c.id));
            Assert.That(Vector3.Distance(PlayAreaPose.Pose.position,owner.Arena.position),Is.LessThan(.001f));
            for(int i=0;i<parts.Length;i++) Assert.That(Vector3.Distance(positions[i],owner.Arena.InverseTransformPoint(parts[i].MainBody.Rigidbody.position)),Is.LessThan(.001f));
            for(int step=0;step<3;step++) Physics.Simulate(1f/60);
            for(int i=0;i<parts.Length;i++) Assert.That(Vector3.Distance(positions[i],owner.Arena.InverseTransformPoint(parts[i].MainBody.Rigidbody.position)),Is.LessThan(.04f),"Relocation must not destabilize the connected car.");
            OnePlayer();
            }
            finally { Physics.simulationMode=simulation; }
        }
        [UnityTest] public IEnumerator MissingModeLeavesMenuUsable()
        {
            var nav=GardenNavigation.Current;
            nav.SendMessage("EnterScene","MissingModeForNavigationTest"); yield return null;
            Assert.IsFalse(nav.IsTransitioning); Assert.IsTrue(nav.InMainMenu); StringAssert.Contains("unavailable",nav.Status);
            nav.EnterMode(GameMode.Sandbox); yield return Wait(nav); Assert.IsTrue(nav.InDownhill);
        }
    }
}

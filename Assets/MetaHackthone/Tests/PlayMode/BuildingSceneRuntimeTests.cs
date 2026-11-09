using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace PhysicalAssembly.Tests
{
    public sealed class BuildingSceneRuntimeTests
    {
        GameObject root;
        WorkshopBootstrap workshop;
        [UnitySetUp] public IEnumerator Setup()
        {
            var fixture=SceneManager.GetSceneByName("Authored layout fixture");
            if(!fixture.IsValid()) fixture=SceneManager.CreateScene("Authored layout fixture");
            SceneManager.SetActiveScene(fixture);
            foreach(var name in new[]{"Workshop","NarrowGap","DownhillSandbox","CloudGardenDemo"})
            { var scene=SceneManager.GetSceneByName(name); if(scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene); }
        }
        WorkshopBootstrap Prepare(string level)
        {
            root=new GameObject("Authored runtime test"); root.SetActive(false);
            workshop=root.AddComponent<WorkshopBootstrap>(); workshop.SpawnOnStart=false;
            if(level=="Downhill") workshop.DownhillSettings=UnityEditor.AssetDatabase.LoadAssetAtPath<DownhillSettings>("Assets/MetaHackthone/Generated/DownhillSettings.asset");
            if(level=="NarrowGap") workshop.ChallengeSettings=UnityEditor.AssetDatabase.LoadAssetAtPath<NarrowGapSettings>("Assets/MetaHackthone/Generated/NarrowGapSettings.asset");
            workshop.CreateSceneLayout(true); return workshop;
        }
        [TestCase("Workshop",15)] [TestCase("NarrowGap",38)] [TestCase("Downhill",8)]
        public void StartupUsesEditedLayoutAndSpawnPosesWithoutDuplicatingGeometry(string level,int count)
        {
            Prepare(level); var layout=workshop.Layout; var course=layout.Downhill;
            var menu=layout.Menu; menu.localPosition+=new Vector3(.13f,.08f,.17f); var menuPosition=menu.localPosition;
            var marker=layout.Spawns[0]; marker.transform.localPosition+=new Vector3(.12f,.14f,0);
            marker.transform.localRotation=Quaternion.Euler(0,37,0); var spawn=marker.Pose;
            int colliders=layout.GetComponentsInChildren<Collider>(true).Length;
            root.SetActive(true); workshop.SpawnKit();
            Assert.AreSame(layout,workshop.Layout); Assert.AreEqual(menuPosition,menu.localPosition);
            Assert.That(layout.GetComponentsInChildren<Collider>(true).Length,Is.EqualTo(colliders),"Runtime startup must reuse all authored controls without adding scenery colliders.");
            Assert.That(workshop.World.Parts.Count,Is.EqualTo(count));
            Assert.IsTrue(workshop.World.Parts.Values.Any(p=>Vector3.Distance(p.MainBody.Rigidbody.position,spawn.position)<.001f && Quaternion.Angle(p.MainBody.Rigidbody.rotation,spawn.rotation)<.01f));
            if(course) Assert.AreSame(course,workshop.Downhill.Course);
            if(layout.NarrowGap) Assert.AreSame(layout.NarrowGap,workshop.Challenge.Course);
            var button=menu.GetComponentsInChildren<WorkshopButton>().First(b=>b.Command=="Move arena");
            button.name="Renamed editor control"; button.Press(); Assert.IsTrue(workshop.RepositionRequested);
        }
        [TestCase(3)] [TestCase(7)]
        public void EmptySandboxSlotsStayAbsentAfterReset(int emptySlot)
        {
            Prepare("Downhill"); var layout=workshop.Layout;
            Object.DestroyImmediate(layout.Spawns[emptySlot].gameObject); layout.Spawns[emptySlot]=null;
            var expected=layout.Spawns.Where(spawn=>spawn).Select(spawn=>spawn.Kind).ToArray();
            root.SetActive(true); workshop.SpawnKit(); var level=workshop.Downhill;
            CollectionAssert.AreEqual(expected,level.Kit.Select(part=>part.Kind).ToArray());
            Assert.DoesNotThrow(()=>level.Presentation.ResetSpawnMarkers());

            CollectionAssert.AreEqual(expected,level.Kit.Select(part=>part.Kind).ToArray());
            Assert.AreEqual(7,workshop.World.Parts.Count);
            Assert.IsTrue(level.TryResetBuild());
            Assert.That(Vector3.Distance(level.Core.MainBody.Rigidbody.position,level.Presentation.AssemblyAnchor.position),Is.LessThan(.001f));
        }
        [Test] public void ResetUsesAuthoredWorkshopSpawnMarkers()
        {
            Prepare("Workshop"); var marker=workshop.Layout.Spawns[0]; marker.transform.position=new Vector3(.33f,.89f,.71f);
            root.SetActive(true); workshop.SpawnKit(); workshop.ResetKit();
            var chassis=workshop.World.Parts.Values.Single(p=>p.Kind==PartKind.Chassis);
            Assert.That(Vector3.Distance(chassis.MainBody.Rigidbody.position,marker.transform.position),Is.LessThan(.001f));
        }
        [Test] public void PlacementPreservesEditedTableHeightAboveTrackedFloor()
        {
            Prepare("Downhill"); workshop.Layout.Surface.transform.localPosition+=Vector3.up*.12f;
            root.SetActive(true);
            var head=new GameObject("Test head");
            try
            {
                head.transform.SetPositionAndRotation(new Vector3(2,3.1f,4),Quaternion.Euler(0,73,0));
                workshop.PositionAroundPlayer(head.transform,1.4f);
                Assert.That(workshop.PlacementSurfaceHeight-workshop.FloorHeight,Is.EqualTo(.82f).Within(.0001f));
                Assert.That(workshop.Layout.Surface.bounds.max.y,Is.EqualTo(2.22f).Within(.0001f));
            }
            finally { Object.DestroyImmediate(head); }
        }
        [TearDown] public void Cleanup()
        {
            if(workshop && workshop.World) foreach(var part in workshop.World.Parts.Values.ToArray()) Object.DestroyImmediate(part.gameObject);
            if(workshop && workshop.Arena) Object.DestroyImmediate(workshop.Arena.gameObject);
            if(root) Object.DestroyImmediate(root);
        }
    }
}

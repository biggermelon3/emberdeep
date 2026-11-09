using System.Linq;
using NUnit.Framework;
using PhysicalAssembly.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PhysicalAssembly.Tests
{
    public sealed class AuthoredSandboxTests
    {
        Scene scene;
        AuthoredSandboxLayout layout;
        [SetUp] public void Open()
        {
            scene=EditorSceneManager.OpenScene(SandboxSceneAuthoring.ScenePath,OpenSceneMode.Additive);
            layout=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<AuthoredSandboxLayout>(true)).Single();
        }
        [TearDown] public void Close() { EditorSceneManager.CloseScene(scene,true); }
        [Test] public void SavedCoursesContainPersistentGeometryAndMarkers()
        {
            Assert.NotNull(layout.Level1);Assert.NotNull(layout.Level2);
            foreach(var markers in new[]{layout.Level1.Markers,layout.Level2.Markers}) {
                Assert.NotNull(markers.Start);Assert.NotNull(markers.Finish);Assert.NotNull(markers.SafeRegion);
                Assert.IsNotEmpty(markers.Roads);
                foreach(var filter in markers.GetComponentsInChildren<MeshFilter>(true)) Assert.IsTrue(AssetDatabase.Contains(filter.sharedMesh),filter.name);
                foreach(var renderer in markers.GetComponentsInChildren<Renderer>(true)) foreach(var material in renderer.sharedMaterials)
                    Assert.IsTrue(AssetDatabase.Contains(material),renderer.name);
            }
            Assert.NotNull(layout.Level1.Landing);
            Assert.AreEqual(3,layout.Level2.Markers.Roads.Length);
            Assert.AreEqual(321,layout.Level2.Markers.Route.Length);
            var zone=layout.Level2.SplineZone;
            Assert.NotNull(zone);Assert.NotNull(zone.Stop);
            CollectionAssert.AreEqual(layout.Level2.Markers.Roads,zone.Roads);
            CollectionAssert.AreEqual(layout.Level2.Markers.Route,zone.Points);
            zone.RebuildPath();Assert.Greater(zone.StopDistance,zone.Length*.9f);
            var spawns=layout.GetComponent<BuildingSceneLayout>().Spawns;
            foreach(var level in new[]{DesignLevelId.GravityGarage,DesignLevelId.WaterfallValley,DesignLevelId.GravityGarage}) {
                layout.Show(level);
                foreach(var spawn in spawns) {
                    bool secondOnly=spawn.Kind==PartKind.DownhillCube || spawn.Kind==PartKind.DownhillDrill;
                    Assert.AreEqual(!secondOnly || level==DesignLevelId.WaterfallValley,spawn.gameObject.activeSelf,spawn.name);
                }
            }
        }
        [Test] public void Level2CompatibilityMarkersCannotChangeTheSavedMovementPath()
        {
            var course=layout.Level2;course.BindAuthored();
            Assert.NotNull(course.BezierTrack);
            var before=course.Sample(5).Position;
            course.Markers.Route[40].position+=new Vector3(.15f,.07f,.12f);
            course.BindAuthored();
            Assert.Less(Vector3.Distance(before,course.Sample(5).Position),.00001f);
            course.SplineZone.RebuildPath();
            Assert.Less(Vector3.Distance(before,course.SplineZone.Sample(5).Position),.0001f);
        }
        [Test] public void FinishAndJumpFollowMovedLevel1Roads()
        {
            var markers=layout.Level1.Markers;
            foreach(var marker in new[]{markers.Start,markers.Finish,markers.Jump}) {
                var before=marker.position;marker.parent.position+=Vector3.up*.1f;
                Assert.Less(Vector3.Distance(before+Vector3.up*.1f,marker.position),.00001f);
                Assert.IsTrue(markers.Passed(marker,marker.position+marker.forward*.01f));
                Assert.IsFalse(markers.Passed(marker,marker.position-marker.forward*.01f));
            }
        }
        [Test] public void DrillBreaksMovedWallOnContactAndRetryRestoresIt()
        {
            layout.Show(DesignLevelId.WaterfallValley);
            var course=layout.Level2;course.BindAuthored();
            var wall=course.Wall.GetComponent<BoxCollider>();Assert.NotNull(wall);
            wall.transform.position+=new Vector3(.05f,.02f,0);
            wall.transform.rotation=Quaternion.Euler(0,30,0)*wall.transform.rotation;
            Physics.SyncTransforms();
            int axis=wall.size.x<wall.size.z?0:2;
            var offset=Vector3.up*wall.size.y*.3f;
            var near=wall.center+offset;near[axis]-=wall.size[axis];
            var far=wall.center+offset;far[axis]+=wall.size[axis];
            var from=wall.transform.TransformPoint(near);var to=wall.transform.TransformPoint(far);
            Assert.IsFalse(course.TryDrill(from+Vector3.up*10,to+Vector3.up*10,0));
            Assert.IsTrue(course.TryDrill(from,to,0));Assert.IsTrue(course.WallBroken);
            Assert.IsFalse(course.Wall.activeSelf);Assert.IsFalse(course.TryDrill(from,to,0));
            course.ResetCourse();Physics.SyncTransforms();Assert.IsTrue(course.Wall.activeSelf);
            Assert.IsFalse(course.WallBroken);
            Assert.IsTrue(course.TryDrill(to,from,.001f*course.WorldScale));
            course.ResetCourse();Physics.SyncTransforms();
            var inside=wall.transform.TransformPoint(wall.center);
            Assert.IsTrue(course.TryDrill(inside,inside,0));
        }
        [Test] public void AuthoredCoursesRejectAccidentalRuntimeGeneration()
        {
            Assert.Throws<System.InvalidOperationException>(()=>layout.Level2.Build(layout.Level2.Level,1));
            Assert.Throws<System.InvalidOperationException>(()=>layout.Level1.Rebuild(null,layout.Level1.Design,1));
        }
    }
}

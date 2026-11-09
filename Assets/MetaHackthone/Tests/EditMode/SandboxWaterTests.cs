using NUnit.Framework;
using UnityEngine;

namespace PhysicalAssembly.Tests
{
    public sealed class SandboxWaterTests
    {
        [TestCase(true)] [TestCase(false)]
        public void OnlyDrillCourseReplacesWoodenBase(bool sandbox)
        {
            var root = new GameObject("Water course test");
            try
            {
                var course = root.AddComponent<GravityGarage25DCourse>();
                course.Build(GravityGarage25DParameters.UnityCalibrated(), .1f, sandbox);
                Assert.AreEqual(sandbox, course.Water != null);
                Assert.AreEqual(!sandbox, root.transform.Find("Diorama table") != null);
                Assert.IsNotNull(course.LowerRoad);
                if (!sandbox) return;
                var surface = root.transform.Find("Sandbox water");
                Assert.IsNotNull(surface);
                Assert.IsNull(surface.GetComponent<Collider>());
                Assert.Less(Vector3.Distance(course.Point(9, -.18f, 0), surface.position), .00001f);
                Assert.Less(Vector3.Distance(new Vector3(1.9f, .8f, .1f), surface.localScale), .00001f);
                Assert.IsTrue(surface.GetComponent<Renderer>().sharedMaterial.shader.isSupported);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SweptCrossingDetectsFastEntryAndRejectsMisses()
        {
            Assert.IsTrue(SandboxWater.CrossesWater(new Vector3(5, 1, 0), new Vector3(5, -3, 0), out var hit));
            Assert.AreEqual(-.18f, hit.y, .00001f);
            Assert.IsTrue(SandboxWater.CrossesWater(new Vector3(-2, -1, 0), new Vector3(20, -1, 0), out _));
            Assert.IsFalse(SandboxWater.CrossesWater(new Vector3(5, 1, 5), new Vector3(5, -3, 5), out _));
            Assert.IsFalse(SandboxWater.CrossesWater(new Vector3(5, 1, 0), new Vector3(5, 0, 0), out _));
        }

        [Test]
        public void PhysicalContactSplashesOnceAndRetryClearsIt()
        {
            var root = new GameObject("Water contact test");
            var probe = new GameObject("Physical vehicle probe");
            try
            {
                var course = root.AddComponent<GravityGarage25DCourse>();
                course.Build(GravityGarage25DParameters.UnityCalibrated(), .1f, true);
                var collider = probe.AddComponent<BoxCollider>();
                collider.size = Vector3.one * .02f;
                probe.transform.position = course.Point(5, 1, 0); Physics.SyncTransforms();
                Assert.IsFalse(course.Water.CheckContact(new[] { collider }));
                probe.transform.position = course.Point(5, -.15f, 0); Physics.SyncTransforms();
                collider.isTrigger = true;
                Assert.IsFalse(course.Water.CheckContact(new[] { collider }));
                collider.isTrigger = false; collider.enabled = false;
                Assert.IsFalse(course.Water.CheckContact(new[] { collider }));
                collider.enabled = true; Physics.SyncTransforms();
                Assert.IsTrue(course.Water.CheckContact(new[] { collider }));
                var splash = root.transform.Find("Water splash"); Assert.IsNotNull(splash);
                Assert.IsTrue(course.Water.CheckContact(new[] { collider }));
                Assert.AreSame(splash, root.transform.Find("Water splash"));
                course.Water.ResetContact();
                Assert.IsFalse(course.Water.Contacted);
                Assert.IsNull(root.transform.Find("Water splash"));
                probe.transform.position = course.Point(5, 1, 0); Physics.SyncTransforms();
                Assert.IsFalse(course.Water.CheckContact(new[] { collider }));
            }
            finally { Object.DestroyImmediate(probe); Object.DestroyImmediate(root); }
        }
    }
}

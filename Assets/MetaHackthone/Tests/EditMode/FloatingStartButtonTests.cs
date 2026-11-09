using NUnit.Framework;
using UnityEngine;

namespace PhysicalAssembly.Tests
{
    public sealed class FloatingStartButtonTests
    {
        [Test] public void ConfigurationPreservesAnchoredPoseAndDoesNotDuplicateVisuals()
        {
            var panel=GameObject.CreatePrimitive(PrimitiveType.Cube);
            var settings=ScriptableObject.CreateInstance<DownhillSettings>();
            try
            {
                panel.GetComponent<Collider>().isTrigger=true; panel.AddComponent<WorkshopButton>();
                var label=new GameObject("Label"); label.transform.SetParent(panel.transform); label.AddComponent<TextMesh>();
                var tile=panel.AddComponent<FloatingStartButton>();
                var position=new Vector3(-.66f,1.12f,-.3f); var rotation=Quaternion.Euler(0,-15,0);
                panel.transform.SetPositionAndRotation(position,rotation);
                tile.Configure(settings); int children=panel.transform.childCount;
                tile.Configure(settings);
                Assert.AreEqual(position,panel.transform.position); Assert.AreEqual(rotation,panel.transform.rotation);
                Assert.AreEqual(children,panel.transform.childCount);
                Assert.AreEqual(1,panel.GetComponentsInChildren<Collider>(true).Length);
                Assert.IsTrue(panel.GetComponent<Collider>().isTrigger);
                Assert.AreEqual("START",panel.GetComponentInChildren<TextMesh>().text);
            }
            finally { Object.DestroyImmediate(panel); Object.DestroyImmediate(settings); }
        }
    }
}

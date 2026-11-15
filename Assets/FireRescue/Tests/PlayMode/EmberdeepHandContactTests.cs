using NUnit.Framework;
using UnityEngine;
namespace FireRescue.Tests
{
    public sealed class EmberdeepHandContactTests
    {
        [Test] public void ThinRotatedScaledPartBlocksPalmSweepAndResolvesInterior()
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                go.transform.SetPositionAndRotation(new Vector3(1,2,3),Quaternion.Euler(13,42,7));
                go.transform.localScale=new Vector3(.2f,.12f,.012f);
                var contacts=new EmberdeepHandContact();contacts.Add(go.transform);
                var from=go.transform.TransformPoint(Vector3.back*10);var to=go.transform.TransformPoint(Vector3.forward*10);
                var hit=contacts.Sweep(from,to,.02f);
                Assert.Less(go.transform.InverseTransformPoint(hit).z,-.5f,"Palm cannot tunnel through a thin part");
                var resolved=contacts.Resolve(go.transform.position,.007f);
                Assert.IsFalse(go.GetComponent<MeshFilter>().sharedMesh.bounds.Contains(go.transform.InverseTransformPoint(resolved)));
                go.SetActive(false);Assert.AreEqual(to,contacts.Sweep(from,to,.02f),"Hidden rack parts do not block hands");
            }
            finally{Object.DestroyImmediate(go);}
        }
    }
}

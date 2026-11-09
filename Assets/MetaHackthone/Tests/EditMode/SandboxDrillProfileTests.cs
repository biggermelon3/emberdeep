using NUnit.Framework;
using UnityEngine;
namespace PhysicalAssembly.Tests {
 public sealed class SandboxDrillProfileTests {
  [Test] public void LowerProfileHasOpenGapAndResettableSandstone() {
   var go=new GameObject("Drill recovery course test");
   try { var c=go.AddComponent<GravityGarage25DCourse>();c.Build(GravityGarage25DParameters.UnityCalibrated(),.1f,true);
    Assert.AreEqual(2.3f,c.LowerSurface(1.6f),.0001f);Assert.IsTrue(float.IsNaN(c.LowerSurface(13)));
    Assert.AreEqual(.56f,c.LowerSurface(17.35f),.0001f);Assert.IsFalse(float.IsNaN(c.LowerSurface(GravityGarage25DCourse.LowerGapEnd)));
    Assert.AreNotSame(c.LowerRoad,c.LowerLanding);Assert.IsNotNull(c.Sandstone);c.Sandstone.SetActive(false);c.ResetSandstone();Assert.IsTrue(c.Sandstone.activeSelf);
   } finally {Object.DestroyImmediate(go);}
  }
  [TestCase(1f)] [TestCase(.4f)] [TestCase(.12f)] public void LargeConeKeepsRearSocketAndOneCubeCollision(float scale) {
   var root=new GameObject("Drill recovery part test");AssemblyPart part=null;
   try {var world=root.AddComponent<AssemblyWorld>();part=DownhillParts.Create(world,PartKind.DownhillDrill,new Pose(Vector3.zero,Quaternion.identity),scale,gridVersion:10);
    Assert.AreEqual(1,part.Sockets.Length);var socket=part.Sockets[0];var center=SandboxDrill.BaseCenter(BuildingGeometry.Unit,scale);
    Assert.Less(Vector3.Distance(center,socket.transform.localPosition),.00001f);Assert.Greater(Vector3.Dot(socket.Normal,Vector3.back),.999f);
    var mesh=part.GetComponentInChildren<MeshFilter>().sharedMesh;var collider=part.GetComponentInChildren<BoxCollider>();
    Assert.IsNotNull(collider);Assert.IsNull(part.GetComponentInChildren<MeshCollider>());
    Assert.Less(Vector3.Distance(Vector3.one*BuildingGeometry.Unit*scale,collider.size),.000001f);
    Assert.Less(Vector3.Distance(center,collider.center-Vector3.forward*collider.size.z*.5f),.000001f);
    Assert.AreEqual(center.z,mesh.bounds.min.z,.00001f);Assert.AreEqual(SandboxDrill.Tip(BuildingGeometry.Unit,scale).z,mesh.bounds.max.z,.00001f);
    Assert.AreEqual(1,part.GetComponentsInChildren<Collider>().Length);
   } finally {if(part)Object.DestroyImmediate(part.gameObject);Object.DestroyImmediate(root);}
  }
 }
}

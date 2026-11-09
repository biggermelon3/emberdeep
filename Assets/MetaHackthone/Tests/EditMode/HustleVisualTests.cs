using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
namespace PhysicalAssembly.Tests {
 public sealed class HustleVisualTests {
  [Test] public void ImportedVisualsKeepPhysicsAndUseValidMaterials() {
   var library=Resources.Load<HustleVisualLibrary>("HustleVisualLibrary"); Assert.IsNotNull(library); Assert.AreEqual(7,library.entries.Length);
   foreach(var entry in library.entries) {
    var prefab=AssetDatabase.LoadAssetAtPath<AssemblyPart>("Assets/MetaHackthone/Generated/"+entry.kind+".prefab");
    var body=prefab.FindBody(entry.body); var visual=body.transform.Find("Hustle visual");
    Assert.IsNotNull(visual); Assert.AreEqual(entry.mesh,visual.GetComponent<MeshFilter>().sharedMesh);
    Assert.IsEmpty(visual.GetComponentsInChildren<Collider>()); Assert.IsEmpty(visual.GetComponentsInChildren<Rigidbody>());
    bool recessed=entry.kind==PartKind.Wheel && (entry.body=="hub" || entry.body=="mount");
    Assert.AreEqual(!recessed,visual.GetComponent<Renderer>().enabled); Assert.AreEqual("Universal Render Pipeline/Lit",entry.material.shader.name);
    Assert.IsFalse(entry.mesh.vertices.Any(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z)));
    foreach(var textureName in new[]{"_BaseMap","_BumpMap","_MetallicGlossMap","_OcclusionMap"})
     if(!AssetDatabase.GetAssetPath(entry.material).EndsWith("/Metal.mat")) Assert.IsNotNull(entry.material.GetTexture(textureName));
    var collider=body.GetComponentInChildren<Collider>(); Assert.IsNotNull(collider);
    if(collider is MeshCollider mc) { Assert.AreNotSame(entry.mesh,mc.sharedMesh); Assert.IsTrue(mc.convex); }
   }
   var chassis=AssetDatabase.LoadAssetAtPath<AssemblyPart>("Assets/MetaHackthone/Generated/Chassis.prefab");
   Assert.That(chassis.MainBody.GetComponent<Rigidbody>().mass,Is.EqualTo(.16f)); Assert.IsNotNull(chassis.GetComponent<ChassisSteering>());
   Assert.Less(Vector3.Distance(chassis.MainBody.GetComponentInChildren<BoxCollider>().transform.localScale,HustlePartProfile.ChassisSize),.00001f);
   var wheel=AssetDatabase.LoadAssetAtPath<AssemblyPart>("Assets/MetaHackthone/Generated/Wheel.prefab");
   Assert.AreEqual(3,wheel.GetComponentsInChildren<Rigidbody>().Length); Assert.AreEqual(2,wheel.GetComponentsInChildren<Joint>().Length);
   Assert.That(wheel.FindBody("wheel").GetComponent<Rigidbody>().mass,Is.EqualTo(.08f));
  }
 }
}

using System;
using UnityEngine;
namespace PhysicalAssembly {
 [CreateAssetMenu(menuName="MetaHackthone/Hustle visual library")]
 public sealed class HustleVisualLibrary : ScriptableObject {
  [Serializable] public sealed class Entry { public PartKind kind; public string body; public Mesh mesh; public Material material; }
  public Entry[] entries;
  public static void Apply(AssemblyPart part) {
   var library=Resources.Load<HustleVisualLibrary>("HustleVisualLibrary");
   if(!library) return;
   foreach(var entry in library.entries) {
    if(entry.kind!=part.Kind || !entry.mesh || !entry.material) continue;
    var body=part.FindBody(entry.body); if(!body || body.transform.Find("Hustle visual")) continue;
    // Keep every collider, joint, rigidbody and socket on its original transform.
    foreach(var renderer in body.GetComponentsInChildren<Renderer>()) {
     if(renderer.GetComponentInParent<AssemblySocket>()) continue;
     if(renderer.name=="Barrel" || renderer.name=="Grid points") continue;
     renderer.enabled=false;
    }
    var visual=new GameObject("Hustle visual"); visual.transform.SetParent(body.transform,false);
    visual.AddComponent<MeshFilter>().sharedMesh=entry.mesh;
    visual.AddComponent<MeshRenderer>().sharedMaterial=entry.material;
   }
  }
 }
}

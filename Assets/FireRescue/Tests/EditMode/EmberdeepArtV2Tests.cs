using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    public sealed class EmberdeepArtV2Tests
    {
        [Test] public void AuthoredKitHasRequiredAssetsAndBoundedUniqueGeometry()
        {
            var art=EmberdeepArtLibrary.Load();Assert.NotNull(art);
            foreach(var name in new[]{"Terrain","Cedar","Broadleaf","Cabin","Rock","Fern","Log","Hydrant","CreekPump"})
                Assert.NotNull(art.Find(name),name);
            var meshes=art.Models.SelectMany(e=>e.Prefab.GetComponentsInChildren<MeshFilter>(true)).Select(f=>f.sharedMesh).Distinct();
            long triangles=0;
            foreach(var mesh in meshes)for(int i=0;i<mesh.subMeshCount;i++)triangles+=mesh.GetIndexCount(i)/3;
            Assert.LessOrEqual(triangles,40000,"Unique kit budget; actual repeated scene geometry is measured separately");
            foreach(var entry in art.Models)
                foreach(var renderer in entry.Prefab.GetComponentsInChildren<Renderer>(true))
                    foreach(var material in renderer.sharedMaterials)
                    {
                        Assert.NotNull(material,entry.Id);Assert.IsTrue(material.shader.isSupported,material.name);
                        Assert.IsTrue(material.shader.name.StartsWith("Emberdeep/V2"),material.name);
                    }
        }
        [Test] public void TerrainImportKeepsFrontAndCreekOnCorrectSidesOfPlayArea()
        {
            var go=Object.Instantiate(EmberdeepArtLibrary.Load().Find("Terrain"));
            try
            {
                var bounds=new Bounds(Vector3.zero,Vector3.zero);
                foreach(var r in go.GetComponentsInChildren<Renderer>())bounds.Encapsulate(r.bounds);
                Assert.That(bounds.center.z,Is.InRange(5.5f,6.5f));
                Assert.That(bounds.size.x,Is.InRange(10,11.5f));Assert.Less(bounds.size.z,13.5f);
                var creek=go.GetComponentsInChildren<Renderer>().Single(r=>r.name=="CreekSurface");
                Assert.Greater(creek.bounds.center.z,10,"FBX axis conversion must not mirror the creek toward the player");
                Assert.AreEqual(0,go.GetComponentsInChildren<Collider>().Length,"Decorative meshes cannot intercept hand interaction");
            }
            finally{Object.DestroyImmediate(go);}
        }
    }
}

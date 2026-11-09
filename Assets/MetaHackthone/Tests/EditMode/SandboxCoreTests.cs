using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace PhysicalAssembly.EditTests
{
    public class SandboxCoreTests
    {
        GameObject root;
        AssemblyWorld world;
        [SetUp] public void Setup() { root=new GameObject("Core tests"); world=root.AddComponent<AssemblyWorld>(); }
        [TearDown] public void Cleanup() { foreach(var p in world.Parts.Values.ToArray()) Object.DestroyImmediate(p.gameObject); Object.DestroyImmediate(root); }
        AssemblyPart Part(PartKind kind,float scale=1,bool core=false,int version=-1) => DownhillParts.Create(world,kind,new Pose(Vector3.zero,Quaternion.identity),scale,gridVersion:version,buildingCore:core);

        [TestCase(1f)] [TestCase(.4f)] [TestCase(.32f)]
        public void CoreGeometryAndLegacyLayout(float scale)
        {
            var core=Part(PartKind.DownhillLongBlock,scale,true);
            Assert.AreEqual(8,core.Sockets.Length);
            Assert.AreEqual(BuildingGeometry.SandboxCoreSize*scale,core.GetComponentInChildren<BoxCollider>().transform.localScale);
            foreach(var s in core.Sockets) {
                var n=s.transform.localRotation*Vector3.forward;
                Assert.Less(Mathf.Abs(n.y),.0001f);
                Assert.AreEqual(0,s.transform.localPosition.y);
                Assert.That(Vector3.Dot(s.transform.localPosition,n),Is.EqualTo(BuildingGeometry.Unit*scale).Within(.00001f));
            }
            var old=Part(PartKind.DownhillLongBlock,scale,true,3);
            CollectionAssert.AreEquivalent(new[]{"core_x-","core_x+"},old.Sockets.Select(s=>s.Id));
        }

        static void Mount(AssemblyPart wheel,AssemblySocket target,string face,float spin)
        {
            var mount=wheel.FindSocket(face);
            var rotation=Quaternion.LookRotation(-target.Normal,target.transform.up)*Quaternion.Inverse(mount.transform.localRotation);
            var position=target.WorldPosition-rotation*mount.transform.localPosition;
            foreach(var b in wheel.Bodies) {
                var q=rotation*(b.Id=="tire"?Quaternion.AngleAxis(spin,Vector3.right):Quaternion.identity);
                b.transform.SetPositionAndRotation(position,q); b.Rigidbody.position=position; b.Rigidbody.rotation=q;
            }
            Physics.SyncTransforms();
        }

        [TestCase(1f)] [TestCase(.4f)] [TestCase(.32f)]
        public void EverySocketPairWheelSizeFaceAndRotation(float scale)
        {
            var core=Part(PartKind.DownhillLongBlock,scale,true);
            foreach(var ka in new[]{PartKind.DownhillSmallWheel,PartKind.DownhillLargeWheel})
            foreach(var kb in new[]{PartKind.DownhillSmallWheel,PartKind.DownhillLargeWheel}) {
                var a=Part(ka,scale); var b=Part(kb,scale);
                for(int i=0;i<8;i++) for(int j=i+1;j<8;j++)
                foreach(var fa in new[]{"mount","mount_opposite"}) foreach(var fb in new[]{"mount","mount_opposite"})
                for(int turn=0;turn<16;turn++) {
                    Mount(a,core.Sockets[i],fa,turn*22.5f); Mount(b,core.Sockets[j],fb,turn*22.5f+2.8125f);
                    // At equal height these cardinal-axis cylinders intersect iff their XZ rectangles intersect.
                    var at=a.FindBody("tire"); var bt=b.FindBody("tire");
                    float ra=(ka==PartKind.DownhillLargeWheel?.13f:.075f)*scale, rb=(kb==PartKind.DownhillLargeWheel?.13f:.075f)*scale;
                    var ea=Mathf.Abs(at.transform.right.x)>.9f?new Vector2(.0275f*scale,ra):new Vector2(ra,.0275f*scale);
                    var eb=Mathf.Abs(bt.transform.right.x)>.9f?new Vector2(.0275f*scale,rb):new Vector2(rb,.0275f*scale);
                    var d=at.transform.position-bt.transform.position;
                    bool overlap=Mathf.Abs(d.x)<ea.x+eb.x-.00001f*scale && Mathf.Abs(d.z)<ea.y+eb.y-.00001f*scale;
                    foreach(var w in new[]{a,b}) {
                        var socket=w.FindSocket(w==a?fa:fb); var target=core.Sockets[w==a?i:j];
                        var c=new AttachmentCandidate(socket.Body,target.Body,socket.WorldPosition,target.WorldPosition,socket,target);
                        Assert.AreEqual(!overlap,world.PlacementClear(c,new[]{w}),$"{ka}/{kb} {i}/{j} {fa}/{fb} spin {turn}");
                    }
                }
                world.UnregisterPart(a); world.UnregisterPart(b); Object.DestroyImmediate(a.gameObject); Object.DestroyImmediate(b.gameObject);
            }
        }

        [Test] public void SavesPreserveScaleAndLegacyDefaults()
        {
            var core=Part(PartKind.DownhillLongBlock,.4f,true);
            var factory=root.AddComponent<PartFactory>(); factory.World=world;
            var saves=root.AddComponent<AssemblySaveService>(); saves.World=world; saves.Factory=factory;
            var data=JsonUtility.FromJson<AssemblySaveV1>(JsonUtility.ToJson(saves.Capture(core)));
            var loaded=saves.Restore(data,new Pose(Vector3.up*3,Quaternion.identity)).Single();
            Assert.AreEqual(.4f,loaded.GeometryScale); Assert.AreEqual(8,loaded.Sockets.Length);
            var legacy=JsonUtility.FromJson<PartSaveV1>("{\"gridVersion\":3,\"buildingCore\":true}");
            Assert.AreEqual(1f,legacy.geometryScale);
        }
    }
}

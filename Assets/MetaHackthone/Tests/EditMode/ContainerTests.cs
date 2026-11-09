using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace PhysicalAssembly.Tests
{
    public sealed class ContainerTests
    {
        GameObject root; AssemblyWorld world; PartFactory factory;
        [SetUp] public void Setup() { root=new GameObject("Container tests"); world=root.AddComponent<AssemblyWorld>(); factory=root.AddComponent<PartFactory>(); factory.World=world; }
        [TearDown] public void Cleanup() { foreach(var p in world.Parts.Values.ToArray()) UnityEngine.Object.DestroyImmediate(p.gameObject); UnityEngine.Object.DestroyImmediate(root); }
        AssemblyPart Make(PartKind kind) => factory.Create(kind,Vector3.zero,Quaternion.identity);
        static ConnectionRecord Edge(AssemblySocket a,AssemblySocket b) => new ConnectionRecord {
            id=Guid.NewGuid().ToString("N"),a=new BodyRef(a.Body.Part.InstanceId,a.Body.Id),b=new BodyRef(b.Body.Part.InstanceId,b.Body.Id),
            socketA=a.Id,socketB=b.Id,anchorA=a.transform.localPosition,anchorB=b.transform.localPosition };

        [TestCase(PartKind.SmallCube)] [TestCase(PartKind.Cuboid)] [TestCase(PartKind.Pyramid)] [TestCase(PartKind.Chassis)]
        public void GeometryHasUniqueSurfacePointsAndCorrectBounds(PartKind kind)
        {
            var p=Make(kind); var expected=kind==PartKind.Chassis?HustlePartProfile.ChassisSize:kind==PartKind.Cuboid?BuildingGeometry.CuboidSize:BuildingGeometry.CubeSize;
            Assert.Less(Vector3.Distance(expected,p.MainBody.Colliders[0].bounds.size),.0001f);
            Assert.AreEqual(p.Sockets.Length,p.Sockets.Select(s=>s.Id).Distinct().Count());
            Assert.GreaterOrEqual(p.Sockets.Select(s=>s.transform.forward).Distinct().Count(),kind==PartKind.Pyramid?5:6);
            foreach(var socket in p.Sockets.Where(s=>s.Family=="grid"))
            {
                var outside=socket.transform.position+socket.transform.forward*.01f;
                Assert.Less(Vector3.Distance(p.MainBody.Colliders[0].ClosestPoint(outside),socket.transform.position),.0002f);
            }
        }
        [TestCase(PartKind.Chassis)] [TestCase(PartKind.SmallCube)] [TestCase(PartKind.Cuboid)] [TestCase(PartKind.Pyramid)] [TestCase(PartKind.PhysicsSphere)]
        public void PrefabsMatchRuntimeDefinitions(PartKind kind)
        {
            var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<AssemblyPart>("Assets/MetaHackthone/Generated/"+kind+".prefab");
            Assert.IsNotNull(prefab); var runtime=Make(kind);
            Assert.AreEqual(runtime.GridVersion,prefab.GridVersion);
            Assert.AreEqual(runtime.MainBody.transform.Find("Shape").localScale,prefab.MainBody.transform.Find("Shape").localScale);
            var marker=runtime.MainBody.transform.Find("Grid points");
            if(marker) Assert.AreEqual(runtime.Sockets.Count(s=>s.Family=="grid")*4,prefab.MainBody.transform.Find("Grid points").GetComponent<MeshFilter>().sharedMesh.vertexCount);
            CollectionAssert.AreEqual(runtime.Sockets.Select(s=>s.Id),prefab.Sockets.Select(s=>s.Id));
            foreach(var socket in runtime.Sockets)
            {
                var saved=prefab.FindSocket(socket.Id); Assert.AreEqual(socket.transform.localPosition,saved.transform.localPosition);
                Assert.Less(Quaternion.Angle(socket.transform.localRotation,saved.transform.localRotation),.01f);
            }
        }
        [TestCase(PartKind.SmallCube)] [TestCase(PartKind.Cuboid)] [TestCase(PartKind.Pyramid)]
        public void EveryFaceSnapsToNearestTwistIncludingWraparound(PartKind kind)
        {
            var a=Make(PartKind.SmallCube); var b=Make(kind);
            foreach(var target in b.Sockets) foreach(float angle in new[]{-359f,-68f,-23f,-22f,22f,23f,44f,67f,68f,359f})
            {
                var source=a.Sockets[0];
                var baseline=Quaternion.LookRotation(-target.transform.forward,target.transform.up);
                a.MainBody.Rigidbody.rotation=Quaternion.AngleAxis(angle,-target.transform.forward)*baseline*Quaternion.Inverse(source.transform.localRotation);
                var delta=BuildingGeometry.SnapDelta(source,target);
                var expected=Quaternion.AngleAxis(Mathf.Round(angle/45)*45,-target.transform.forward)*baseline;
                Assert.Less(Quaternion.Angle(expected,delta*source.WorldRotation),.05f);
                Assert.Less(Vector3.Distance(delta*source.Normal,-target.transform.forward),.0001f);
            }
        }
        [TestCase(PartKind.SmallCube,6)] [TestCase(PartKind.Cuboid,10)]
        public void BlocksHaveOnePointPerSquareSection(PartKind kind,int count)
        {
            var part=Make(kind); float u=BuildingGeometry.Unit;
            Assert.AreEqual(2,part.GridVersion); Assert.AreEqual(count,part.Sockets.Length);
            var size=kind==PartKind.Cuboid?new Vector3(2*u,u,u):Vector3.one*u;
            Assert.Less(Vector3.Distance(size,part.MainBody.Colliders[0].bounds.size),.00001f);
            foreach(var normal in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back})
            {
                var face=part.Sockets.Where(s=>Vector3.Dot(s.Normal,normal)>.99f).ToArray();
                bool longFace=kind==PartKind.Cuboid && normal.x==0;
                Assert.AreEqual(longFace?2:1,face.Length);
                var center=Vector3.Scale(normal,size)*.5f;
                var expected=longFace?new[]{center-Vector3.right*u*.5f,center+Vector3.right*u*.5f}:new[]{center};
                foreach(var point in expected) Assert.AreEqual(1,face.Count(s=>Vector3.Distance(s.transform.localPosition,point)<.00001f));
            }
        }
        [TestCase(PartKind.SmallCube)] [TestCase(PartKind.Cuboid)]
        public void EveryBlockSocketOffersAFlushCubePlacement(PartKind kind)
        {
            var target=Make(kind); float u=BuildingGeometry.Unit;
            foreach(var socket in target.Sockets)
            {
                var expected=socket.WorldPosition+socket.Normal*u*.5f;
                var source=factory.Create(PartKind.SmallCube,expected+socket.Normal*.008f,Quaternion.Euler(0,90,0));
                var candidate=world.Query(source);
                Assert.IsTrue(candidate.IsValid,socket.Id);
                Assert.AreSame(socket,candidate.targetSocket);
                Assert.Less(Vector3.Distance(expected,candidate.SnappedPose.position),.0001f);
                world.UnregisterPart(source); UnityEngine.Object.DestroyImmediate(source.gameObject);
            }
        }
        [TestCase(0,24,56)] [TestCase(1,12,28)] [TestCase(2,6,10)] [TestCase(3,6,10)]
        public void SaveVersionsPreserveDimensionsSocketsAndConnections(int version,int cubeCount,int cuboidCount)
        {
            // Give the factory current prefabs, so old saves must bypass them.
            factory.Prefabs=new[]{LoadPrefab(PartKind.SmallCube),LoadPrefab(PartKind.Cuboid)};
            float u=BuildingGeometry.Unit;
            var cube=factory.Create(PartKind.SmallCube,Vector3.zero,Quaternion.identity,version);
            var block=factory.Create(PartKind.Cuboid,Vector3.right*(version>=2?1.5f:2f)*u,Quaternion.identity,version);
            string a=version>=2?"grid_center_x+_0_0":"grid_x+_0_0";
            string b=version>=2?"grid_center_x-_0_0":"grid_x-_1_0";
            Assert.IsTrue(world.Connect(Edge(cube.FindSocket(a),block.FindSocket(b))));
            var service=root.AddComponent<AssemblySaveService>(); service.World=world; service.Factory=factory;
            string json=JsonUtility.ToJson(service.Capture(cube));
            if(version==0) json=json.Replace("\"gridVersion\":0,",""); // Pre-version saves.
            var restored=service.Restore(JsonUtility.FromJson<AssemblySaveV1>(json),new Pose(Vector3.up,Quaternion.identity));
            var restoredCube=restored.Single(p=>p.Kind==PartKind.SmallCube);
            var restoredBlock=restored.Single(p=>p.Kind==PartKind.Cuboid);
            Assert.IsTrue(restored.All(p=>p.GridVersion==version));
            Assert.AreEqual(cubeCount,restoredCube.Sockets.Length); Assert.AreEqual(cuboidCount,restoredBlock.Sockets.Length);
            Assert.Less(Vector3.Distance(new Vector3(version>=2?2*u:3*u,u,u),restoredBlock.MainBody.Colliders[0].bounds.size),.00001f);
            Assert.Less(Vector3.Distance(restoredCube.FindSocket(a).WorldPosition,restoredBlock.FindSocket(b).WorldPosition),.00001f);
            Assert.AreEqual(2,world.GetConnectedParts(restoredCube.InstanceId).Count);
            foreach(var pair in new[]{(cube,restoredCube),(block,restoredBlock)})
                foreach(var socket in pair.Item1.Sockets)
                {
                    var loaded=pair.Item2.FindSocket(socket.Id); Assert.IsNotNull(loaded);
                    Assert.AreEqual(socket.transform.localPosition,loaded.transform.localPosition);
                    Assert.AreEqual(socket.transform.localRotation,loaded.transform.localRotation);
                }
        }
        static AssemblyPart LoadPrefab(PartKind kind) => UnityEditor.AssetDatabase.LoadAssetAtPath<AssemblyPart>("Assets/MetaHackthone/Generated/"+kind+".prefab");
        [Test] public void MixedVersionAssemblyRetainsEachLayoutAndRejectsUnknownVersions()
        {
            var parts=new[]{factory.Create(PartKind.SmallCube,Vector3.zero,Quaternion.identity,0),
                factory.Create(PartKind.Cuboid,Vector3.right,Quaternion.identity,1),
                factory.Create(PartKind.Cuboid,Vector3.right*2,Quaternion.identity,2)};
            Assert.IsTrue(world.Connect(Edge(parts[0].Sockets[0],parts[1].Sockets[0])));
            Assert.IsTrue(world.Connect(Edge(parts[1].Sockets[1],parts[2].Sockets[0])));
            var service=root.AddComponent<AssemblySaveService>(); service.World=world; service.Factory=factory;
            var save=service.Capture(parts[0]); var restored=service.Restore(save,new Pose(Vector3.up,Quaternion.identity));
            CollectionAssert.AreEquivalent(new[]{0,1,2},restored.Select(p=>p.GridVersion));
            CollectionAssert.AreEquivalent(new[]{24,28,10},restored.Select(p=>p.Sockets.Length));
            Assert.AreEqual(3,world.GetConnectedParts(restored[0].InstanceId).Count);
            save.parts[0].gridVersion=PartFactory.LatestGridVersion+1;
            Assert.Throws<InvalidDataException>(()=>service.Restore(save,default));
            Assert.AreEqual(6,world.Parts.Count);
        }
        [TestCase(PartKind.Block,6)] [TestCase(PartKind.DownhillLongBlock,16)] [TestCase(PartKind.DownhillShortBlock,16)]
        public void NewBlocksUseOnlyUnitFaceCenters(PartKind kind,int count)
        {
            var part=Make(kind);
            Assert.AreEqual(3,part.GridVersion);
            Assert.AreEqual(count,part.Sockets.Length);
            Assert.IsTrue(part.Sockets.All(s=>s.Id.StartsWith("grid_center_")));
            Assert.AreEqual(count,part.Sockets.Select(s=>s.transform.localPosition).Distinct().Count());
            foreach(var normal in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back})
            {
                var face=part.Sockets.Where(s=>Vector3.Dot(s.Normal,normal)>.99f).ToArray();
                Assert.IsNotEmpty(face);
                var center=face.Aggregate(Vector3.zero,(sum,s)=>sum+s.transform.localPosition)/face.Length;
                Assert.Less(Vector3.ProjectOnPlane(center,normal).magnitude,.00001f,"Face grid must be centered.");
            }
        }
        [TestCase(1f)] [TestCase(1f/3)] [TestCase(2f/3)]
        public void DownhillUnitCubeHasExactlySixCenteredPointsAtEitherScale(float scale)
        {
            var settings=ScriptableObject.CreateInstance<DownhillSettings>(); settings.CoreSize=BuildingGeometry.CubeSize;
            try
            {
                var cube=DownhillParts.Create(world,PartKind.DownhillLongBlock,new Pose(Vector3.zero,Quaternion.identity),scale,settings);
                Assert.AreEqual(6,cube.Sockets.Length);
                foreach(var socket in cube.Sockets)
                    Assert.Less(Vector3.Distance(socket.transform.localPosition,socket.Normal*BuildingGeometry.Unit*scale*.5f),.00001f);
            }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }
        [TestCase(PartKind.Chassis)] [TestCase(PartKind.Beam)] [TestCase(PartKind.Pyramid)]
        public void OtherBuildingPartsDefaultToCentersAndPreserveLegacySaves(PartKind kind)
        {
            var fresh=Make(kind);
            Assert.AreEqual(3,fresh.GridVersion);
            Assert.IsFalse(fresh.Sockets.Any(s=>s.Id.StartsWith("grid_")&&!s.Id.StartsWith("grid_center_")&&!s.Id.StartsWith("grid_slope")));
            if(kind==PartKind.Pyramid) Assert.AreEqual(5,fresh.Sockets.Length);
            var old=factory.Create(kind,Vector3.right*2,Quaternion.identity,1);
            Assert.GreaterOrEqual(old.Sockets.Length,fresh.Sockets.Length);
            var service=root.AddComponent<AssemblySaveService>(); service.World=world; service.Factory=factory;
            var restored=service.Restore(service.Capture(old),new Pose(Vector3.up,Quaternion.identity)).Single();
            CollectionAssert.AreEqual(old.Sockets.Select(s=>s.Id),restored.Sockets.Select(s=>s.Id));
            CollectionAssert.AreEqual(old.Sockets.Select(s=>s.transform.localPosition),restored.Sockets.Select(s=>s.transform.localPosition));
        }
        [TestCase(1)] [TestCase(3)]
        public void DownhillSaveRoundTripPreservesItsConnectionLayout(int version)
        {
            var core=factory.Create(PartKind.DownhillLongBlock,Vector3.zero,Quaternion.identity,version);
            var wheel=factory.Create(PartKind.DownhillLargeWheel,Vector3.right,Quaternion.identity,version);
            Assert.IsTrue(world.Connect(Edge(core.Sockets[0],wheel.FindSocket("mount"))));
            var service=root.AddComponent<AssemblySaveService>(); service.World=world; service.Factory=factory;
            var restored=service.Restore(service.Capture(core),new Pose(Vector3.up,Quaternion.identity));
            var restoredCore=restored.Single(p=>p.Kind==PartKind.DownhillLongBlock);
            Assert.AreEqual(version,restoredCore.GridVersion);
            CollectionAssert.AreEqual(core.Sockets.Select(s=>s.Id),restoredCore.Sockets.Select(s=>s.Id));
            CollectionAssert.AreEqual(core.Sockets.Select(s=>s.transform.localPosition),restoredCore.Sockets.Select(s=>s.transform.localPosition));
            Assert.AreEqual(2,world.GetConnectedParts(restoredCore.InstanceId).Count);
            var restoredWheel=restored.Single(p=>p.Kind==PartKind.DownhillLargeWheel);
            Assert.Less(Vector3.Distance(restoredWheel.FindSocket("mount").transform.localPosition,Vector3.left*.0285f),.00001f);
            Assert.IsTrue(world.SocketOccupied(restoredWheel.FindSocket("mount")));
            Assert.IsFalse(world.SocketOccupied(restoredWheel.FindSocket("mount_opposite")));
        }
        [TestCase(PartKind.DownhillSmallWheel)] [TestCase(PartKind.DownhillLargeWheel)]
        public void TwoSidedWheelsPreserveBothConnectionsAcrossSave(PartKind kind)
        {
            var a=Make(PartKind.DownhillShortBlock); var b=Make(PartKind.DownhillLongBlock); var wheel=Make(kind);
            Assert.IsTrue(world.Connect(Edge(wheel.FindSocket("mount"),a.Sockets[0])));
            Assert.IsTrue(world.Connect(Edge(wheel.FindSocket("mount_opposite"),b.Sockets[0])));
            var service=root.AddComponent<AssemblySaveService>(); service.World=world; service.Factory=factory;
            var save=service.Capture(wheel); Assert.AreEqual(4,save.formatVersion);
            var restored=service.Restore(save,new Pose(Vector3.up*3,Quaternion.identity));
            var copy=restored.Single(p=>p.Kind==kind);
            Assert.AreEqual(3,world.GetConnectedParts(copy.InstanceId).Count);
            Assert.IsTrue(copy.Sockets.All(world.SocketOccupied));
        }
        [TestCase(PartKind.DownhillLongBlock)] [TestCase(PartKind.DownhillShortBlock)]
        [TestCase(PartKind.DownhillTriangle)] [TestCase(PartKind.DownhillIrregular)]
        [TestCase(PartKind.DownhillSmallWheel)] [TestCase(PartKind.DownhillLargeWheel)]
        public void SandboxGeometryDoublesWithoutChangingMassOrResistance(PartKind kind)
        {
            var old=DownhillParts.Create(world,kind,new Pose(Vector3.zero,Quaternion.identity),1f/3);
            var enlarged=DownhillParts.Create(world,kind,new Pose(Vector3.zero,Quaternion.identity),DownhillSettings.VehicleScale);
            Assert.That(DownhillSettings.VehicleScale,Is.EqualTo(2f/3));
            foreach(var body in old.Bodies)
            {
                var other=enlarged.FindBody(body.Id);
                Assert.AreEqual(body.Rigidbody.mass,other.Rigidbody.mass);
                for(int i=0;i<body.Colliders.Length;i++)
                {
                    if(body.Colliders[i] is MeshCollider mesh)
                    {
                        // Compare the actual collider input. PhysX convex cooking has scale-dependent hull tolerances.
                        var vertices=mesh.sharedMesh.vertices; var scaled=((MeshCollider)other.Colliders[i]).sharedMesh.vertices;
                        Assert.AreEqual(vertices.Length,scaled.Length);
                        for(int j=0;j<vertices.Length;j++) Assert.Less(Vector3.Distance(vertices[j]*2,scaled[j]),.00001f);
                    }
                    else Assert.Less(Vector3.Distance(body.Colliders[i].bounds.size*2,other.Colliders[i].bounds.size),.00001f);
                }
                var wheel=body.GetComponent<PassiveWheel>();
                if(wheel) { Assert.AreEqual(wheel.Resistance,other.GetComponent<PassiveWheel>().Resistance); Assert.That(other.GetComponent<PassiveWheel>().Radius,Is.EqualTo(wheel.Radius*2).Within(.00001f)); }
            }
            foreach(var socket in old.Sockets)
            {
                var other=enlarged.FindSocket(socket.Id);
                Assert.Less(Vector3.Distance(socket.transform.localPosition*2,other.transform.localPosition),.00001f);
                Assert.Less(Vector3.Distance(socket.Normal,other.Normal),.00001f);
            }
            var oldMeshes=old.VisualMeshes; var meshes=enlarged.VisualMeshes;
            Assert.AreEqual(oldMeshes.Length,meshes.Length);
            for(int i=0;i<meshes.Length;i++)
            {
                if(meshes[i].name=="Grid points") continue;
                Assert.Less(Vector3.Distance(oldMeshes[i].GetComponent<Renderer>().bounds.size*2,meshes[i].GetComponent<Renderer>().bounds.size),.00001f);
            }
        }
        [Test] public void FreshPartsIgnoreStalePrefabLayouts()
        {
            var old=factory.Create(PartKind.Cuboid,Vector3.right*2,Quaternion.identity,1);
            factory.Prefabs=new[]{old};
            var fresh=Make(PartKind.Cuboid);
            Assert.AreEqual(2,fresh.GridVersion); Assert.AreEqual(10,fresh.Sockets.Length);
            Assert.Less(Vector3.Distance(BuildingGeometry.CuboidSize,fresh.MainBody.Colliders[0].bounds.size),.00001f);
        }
        [Test] public void CentralConnectionRejectsReusedEitherEndpointAndDetachFreesIt()
        {
            var a=Make(PartKind.SmallCube); var b=Make(PartKind.Cuboid); var c=Make(PartKind.Pyramid);
            var edge=Edge(a.Sockets[0],b.Sockets[0]); Assert.IsTrue(world.Connect(edge));
            Assert.IsFalse(world.Connect(Edge(a.Sockets[0],c.Sockets[0])));
            Assert.IsFalse(world.Connect(Edge(c.Sockets[0],b.Sockets[0])));
            Assert.IsFalse(world.Connect(new ConnectionRecord { id="surface",a=edge.a,b=edge.b }));
            world.TryDetachPart(a); Assert.IsTrue(world.Connect(Edge(a.Sockets[0],c.Sockets[0])));
        }
        [Test] public void OccupiedSocketSaveIsRejectedBeforeSpawningAndOldVersionPreserved()
        {
            var a=Make(PartKind.SmallCube); var b=Make(PartKind.Cuboid); Assert.IsTrue(world.Connect(Edge(a.Sockets[0],b.Sockets[0])));
            var service=root.AddComponent<AssemblySaveService>(); service.World=world; service.Factory=factory;
            var save=service.Capture(a); var duplicate=JsonUtility.FromJson<ConnectionRecord>(JsonUtility.ToJson(save.connections[0])); duplicate.id="duplicate"; save.connections.Add(duplicate);
            Assert.Throws<InvalidDataException>(()=>service.Restore(save,new Pose(Vector3.zero,Quaternion.identity))); Assert.AreEqual(2,world.Parts.Count);
            save.connections.RemoveAt(1); save.formatVersion=2; Assert.Throws<InvalidDataException>(()=>AssemblySaveService.Validate(save));
        }
        [Test] public void ContainerRoundTripAndFailedSocketRestoreAreTransactional()
        {
            var a=Make(PartKind.SmallCube); var b=Make(PartKind.Pyramid); Assert.IsTrue(world.Connect(Edge(a.Sockets[0],b.Sockets[0])));
            var service=root.AddComponent<AssemblySaveService>(); service.World=world; service.Factory=factory;
            var save=service.Capture(a); var restored=service.Restore(save,new Pose(Vector3.right*3,Quaternion.Euler(0,15,0)));
            Assert.AreEqual(2,restored.Count); Assert.AreEqual(2,world.GetConnectedParts(restored[0].InstanceId).Count);
            save.connections[0].socketA="missing";
            Assert.Throws<InvalidDataException>(()=>service.Restore(save,new Pose(Vector3.zero,Quaternion.identity))); Assert.AreEqual(4,world.Parts.Count);
        }
        [Test] public void SphereIsLooseGrabbableAndCannotBeSavedAsConstruction()
        {
            var ball=Make(PartKind.PhysicsSphere); Assert.IsEmpty(ball.Sockets); Assert.IsFalse(ball.MainBody.Attachable);
            Assert.IsTrue(ball.MainBody.Rigidbody.useGravity); Assert.AreEqual(CollisionDetectionMode.ContinuousDynamic,ball.MainBody.Rigidbody.collisionDetectionMode);
            Assert.IsTrue(world.TryGrab(0,ball,new Pose(Vector3.zero,Quaternion.identity))); world.Release(0,true); Assert.IsEmpty(world.Connections);
            var service=root.AddComponent<AssemblySaveService>(); service.World=world;
            Assert.Throws<InvalidOperationException>(()=>service.Capture(ball));
        }
    }
}

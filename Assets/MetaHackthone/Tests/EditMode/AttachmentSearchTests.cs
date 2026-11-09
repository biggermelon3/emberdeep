using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using Object=UnityEngine.Object;

namespace PhysicalAssembly.Tests
{
    public sealed class AttachmentSearchTests
    {
        GameObject root; AssemblyWorld world; PartFactory factory;
        [SetUp] public void Setup() { root=new GameObject("Search tests"); world=root.AddComponent<AssemblyWorld>(); factory=root.AddComponent<PartFactory>(); factory.World=world; }
        [TearDown] public void Cleanup() { foreach(var part in world.Parts.Values.ToArray()) Object.DestroyImmediate(part.gameObject); Object.DestroyImmediate(root); }
        AssemblyPart Make(PartKind kind,Vector3 position) => factory.Create(kind,position,Quaternion.identity);

        // Exhaustive reference with linear occupancy scans and exact collision tests.
        // Uses the production facing preference for timing and search-parity checks.
        public static AttachmentCandidate Reference(AssemblyWorld world,AssemblyPart held)
        {
            var own=new HashSet<AssemblyPart>(world.GetConnectedParts(held.InstanceId));
            AttachmentCandidate best=default; float bestScore=float.MaxValue;
            Physics.SyncTransforms();
            foreach(var target in world.Parts.Values.OrderBy(p=>p.InstanceId))
            {
                if(own.Contains(target)) continue;
                foreach(var member in own) foreach(var a in member.Sockets) foreach(var b in target.Sockets)
                {
                    if(!a.Body.Attachable || !b.Body.Attachable || a.Family!=b.Family || Occupied(world,a) || Occupied(world,b)) continue;
                    float distance=(a.WorldPosition-b.WorldPosition).sqrMagnitude;
                    float radius2=world.SocketDistance*world.SocketDistance;
                    float score=distance+(1+Mathf.Clamp(Vector3.Dot(a.Normal,b.Normal),-1,1))*radius2;
                    if(distance>radius2 || score>=bestScore) continue;
                    var c=new AttachmentCandidate(a.Body,b.Body,a.WorldPosition,b.WorldPosition,a,b);
                    if(!ReferenceClear(world,c,own)) continue;
                    bestScore=score; best=c;
                }
            }
            return best;
        }
        static bool Occupied(AssemblyWorld world,AssemblySocket s) => world.Connections.Any(c=>
            (c.a.partId==s.Body.Part.InstanceId && c.a.bodyId==s.Body.Id && c.socketA==s.Id)||
            (c.b.partId==s.Body.Part.InstanceId && c.b.bodyId==s.Body.Id && c.socketB==s.Id));
        static bool ReferenceClear(AssemblyWorld world,AttachmentCandidate c,HashSet<AssemblyPart> own)
        {
            if(c.sourceSocket.Family!="grid") return true;
            var moving=new HashSet<AssemblyPart>(own);
            foreach(var p in moving) foreach(var body in p.Bodies) foreach(var ac in body.Colliders)
            {
                var current=body.Rigidbody.position+body.Rigidbody.rotation*body.transform.InverseTransformPoint(ac.transform.position);
                var rotation=c.rotationDelta*body.Rigidbody.rotation*Quaternion.Inverse(body.transform.rotation)*ac.transform.rotation;
                var position=c.targetPoint+c.rotationDelta*(current-c.sourcePoint);
                foreach(var other in world.Parts.Values)
                {
                    if(moving.Contains(other)||other.Kind==PartKind.PhysicsSphere) continue;
                    foreach(var ob in other.Bodies) foreach(var bc in ob.Colliders)
                        if(bc && bc.enabled && !bc.isTrigger && Physics.ComputePenetration(ac,position,rotation,bc,ob.Rigidbody.position+ob.Rigidbody.rotation*ob.transform.InverseTransformPoint(bc.transform.position),ob.Rigidbody.rotation*Quaternion.Inverse(ob.transform.rotation)*bc.transform.rotation,out _,out float depth) && depth>.001f) return false;
                }
            }
            return true;
        }
        [Test] public void NearbySearchMatchesReferenceAcrossMovingRotatedAndBlockedLayouts()
        {
            var held=Make(PartKind.SmallCube,new Vector3(0,2,0));
            var target=Make(PartKind.Chassis,new Vector3(0,2,0));
            var blocker=Make(PartKind.Pyramid,new Vector3(0,4,0));
            var random=new System.Random(321);
            for(int i=0;i<36;i++)
            {
                target.MainBody.Rigidbody.rotation=Quaternion.Euler(i%3*15,i*15,i%4*15);
                var socket=target.Sockets[i%target.Sockets.Length];
                held.MainBody.Rigidbody.position=socket.WorldPosition+socket.Normal*(BuildingGeometry.Unit*.5f+.01f);
                held.MainBody.Rigidbody.rotation=Quaternion.Euler((float)random.NextDouble()*30,i*15,0);
                blocker.MainBody.Rigidbody.position=i%3==0?held.MainBody.Rigidbody.position:new Vector3(0,4,0);
                var reference=Reference(world,held); var actual=world.Query(held);
                Assert.AreEqual(reference.IsValid,actual.IsValid,"Layout "+i);
                if(!actual.IsValid) continue;
                Assert.AreSame(reference.targetSocket,actual.targetSocket,"Target socket "+i);
                Assert.AreSame(reference.sourceSocket,actual.sourceSocket,"Source socket "+i);
                Assert.Less(Vector3.Distance(reference.SnappedPose.position,actual.SnappedPose.position),.00001f);
                Assert.Less(Quaternion.Angle(reference.SnappedPose.rotation,actual.SnappedPose.rotation),.05f);
            }
        }
        [Test] public void ContainerWallKeepsItsAlignedFacingInsteadOfTurningIntoTheInterior()
        {
            float u=BuildingGeometry.Unit,y=2+HustlePartProfile.ChassisSize.y/2+u/2;
            Make(PartKind.Chassis,new Vector3(0,2,0));
            // The base has three by six unit sections; align the wall centers to
            // their half-unit offsets rather than the removed corner sockets.
            foreach(float z in new[]{-1.5f*u,1.5f*u})
            {
                var end=Make(PartKind.Cuboid,new Vector3(-u*.5f,y+.01f,z));
                var snap=world.Query(end); Assert.IsTrue(snap.IsValid);
                end.MainBody.Rigidbody.position=snap.SnappedPose.position;
                end.MainBody.Rigidbody.rotation=snap.SnappedPose.rotation;
            }
            var side=Make(PartKind.SmallCube,new Vector3(-u,y+.01f,u*.5f));
            var candidate=world.Query(side); Assert.IsTrue(candidate.IsValid);
            Assert.Less(Quaternion.Angle(Quaternion.identity,candidate.SnappedPose.rotation),.1f);
            Assert.Less(candidate.SnappedPose.position.x,-u/2,"The side wall must stay outside the container interior.");
        }
        [Test] public void DistantPartsNeverReachPairOrPenetrationChecks()
        {
            var held=Make(PartKind.Chassis,Vector3.zero);
            for(int i=0;i<49;i++) Make(PartKind.Cuboid,new Vector3(3+i*.3f,0,0));
            Assert.IsFalse(world.Query(held).IsValid); Assert.AreEqual(0,world.LastSocketPairChecks); Assert.AreEqual(0,world.LastPenetrationChecks);
        }
        [Test] public void DistanceBoundaryAndMovingTargetUseCurrentPhysicsPoses()
        {
            var a=Make(PartKind.SmallCube,new Vector3(0,2,0)); var b=Make(PartKind.SmallCube,new Vector3(2,2,0));
            foreach(float gap in new[]{world.SocketDistance-.0001f,world.SocketDistance+.0001f,.01f})
            {
                b.MainBody.Rigidbody.position=new Vector3(BuildingGeometry.Unit+gap,2,0);
                Assert.AreEqual(Reference(world,a).IsValid,world.Query(a).IsValid);
            }
            b.MainBody.Rigidbody.position=new Vector3(3,2,0); Assert.IsFalse(world.Query(a).IsValid);
        }
        [Test] public void WarmSearchHasNoManagedAllocations()
        {
            var a=Make(PartKind.Chassis,Vector3.zero); Make(PartKind.SmallCube,new Vector3(.18f,0,0));
            for(int i=0;i<8;i++) world.Query(a);
            using var recorder=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC.Alloc",4096,ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            var calibration=new byte[1024]; GC.KeepAlive(calibration);
            int calibrationSamples=recorder.Count;
            recorder.Reset();
            for(int i=0;i<20;i++) world.Query(a);
            int samples=recorder.Count;
            Assert.Greater(calibrationSamples,0,"Allocation recorder must detect a known allocation.");
            Assert.AreEqual(0,samples,"Steady connection searches must not allocate managed memory.");
        }
        [TestCase(15,false)] [TestCase(15,true)] [TestCase(50,false)] [TestCase(50,true)]
        public void BenchmarkSearch(int count,bool near)
        {
            var held=Make(PartKind.Chassis,new Vector3(0,2,0));
            for(int i=1;i<count;i++)
            {
                var kind=i<=8?PartKind.SmallCube:i<=12?PartKind.Cuboid:PartKind.Pyramid;
                Make(kind,i==1 && near?new Vector3(HustlePartProfile.ChassisSize.x/2+BuildingGeometry.Unit/2+.01f,2,0):new Vector3(2+(i%7)*.4f,2,(i/7)*.4f));
            }
            for(int i=0;i<5;i++) { world.Query(held); Reference(world,held); }
            var before=new double[30]; var after=new double[30];
            for(int i=0;i<30;i++)
            {
                long start=Stopwatch.GetTimestamp(); Reference(world,held); before[i]=(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;
                start=Stopwatch.GetTimestamp(); world.Query(held); after[i]=(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;
            }
            Array.Sort(before); Array.Sort(after);
            TestContext.WriteLine($"SEARCH_BENCH count={count} near={near} legacyMedianMs={before[15]:F4} legacyP95Ms={before[28]:F4} optimizedMedianMs={after[15]:F4} optimizedP95Ms={after[28]:F4} pairs={world.LastSocketPairChecks} penetrations={world.LastPenetrationChecks}");
            Assert.Less(after[28],count==15?2:4,"Preview query CPU budget, p95 milliseconds.");
        }
    }
}

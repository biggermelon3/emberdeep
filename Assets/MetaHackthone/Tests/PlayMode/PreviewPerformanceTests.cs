using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace PhysicalAssembly.Tests
{
    public sealed class PreviewPerformanceTests
    {
        GameObject root; WorkshopBootstrap workshop;
        [SetUp] public void Setup() { root=new GameObject("Preview tests"); workshop=root.AddComponent<WorkshopBootstrap>(); workshop.SpawnOnStart=false; }
        [TearDown] public void Cleanup()
        {
            foreach(var p in workshop.World.Parts.Values.ToArray()) if(p) Object.DestroyImmediate(p.gameObject);
            Object.DestroyImmediate(workshop.Arena.gameObject); Object.DestroyImmediate(root);
        }
        AssemblyPart Make(PartKind kind,Vector3 position) => workshop.Factory.Create(kind,position,Quaternion.identity);
        [UnityTest] public IEnumerator PreviewIsSharedAllocationFreeAndClearsOnRelease()
        {
            yield return null;
            var source=Make(PartKind.SmallCube,new Vector3(0,2,0));
            Make(PartKind.SmallCube,new Vector3(BuildingGeometry.Unit+.01f,2,0));
            workshop.Session.Select(source);
            Assert.IsTrue(workshop.World.TryGrab(0,source,new Pose(source.MainBody.Rigidbody.position,Quaternion.identity)));
            long before=workshop.World.QueryCount;
            workshop.UpdatePreview(); Assert.IsTrue(workshop.PreviewCandidate.IsValid);
            for(int i=0;i<20;i++) workshop.UpdatePreview();
            Assert.AreEqual(before+1,workshop.World.QueryCount,"Ghost and highlight must consume the same cached query.");
            using(var recorder=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC.Alloc",4096,ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
            {
                GC.KeepAlive(new byte[1024]); int calibration=recorder.Count; recorder.Reset();
                for(int i=0;i<20;i++) workshop.UpdatePreview();
                int samples=recorder.Count;
                Assert.Greater(calibration,0); Assert.AreEqual(0,samples,"Steady ghost/highlight updates must not allocate.");
            }
            before=workshop.World.QueryCount;
            workshop.World.Release(0,false); Assert.IsFalse(workshop.PreviewCandidate.IsValid); workshop.UpdatePreview();
            Assert.IsFalse(workshop.PreviewCandidate.IsValid); Assert.AreEqual(before,workshop.World.QueryCount);
            yield return null;
        }
        [UnityTest] public IEnumerator ReleaseRevalidatesMovingTargetAndGhostDoesNotRebuildForSocketMemberChanges()
        {
            yield return null;
            var a=Make(PartKind.SmallCube,new Vector3(0,2,0)); var b=Make(PartKind.SmallCube,new Vector3(BuildingGeometry.Unit+.01f,2,0));
            workshop.World.TryGrab(0,a,new Pose(a.MainBody.Rigidbody.position,Quaternion.identity));
            workshop.UpdatePreview(); Assert.IsTrue(workshop.PreviewCandidate.IsValid);
            b.MainBody.Rigidbody.position+=Vector3.right*2;
            workshop.World.Release(0,true); Assert.IsFalse(workshop.World.IsBusy); Assert.IsEmpty(workshop.World.Connections);
            var sa=a.Sockets[0]; var sb=b.Sockets[0];
            Assert.IsTrue(workshop.World.Connect(new ConnectionRecord { id="ghost-group",a=new BodyRef(a.InstanceId,sa.Body.Id),b=new BodyRef(b.InstanceId,sb.Body.Id),socketA=sa.Id,socketB=sb.Id,anchorA=sa.transform.localPosition,anchorB=sb.transform.localPosition }));
            var c=Make(PartKind.SmallCube,new Vector3(0,3,0)); var sc=c.Sockets[0];
            var ghost=Object.FindFirstObjectByType<AttachmentGhost>(FindObjectsInactive.Include);
            ghost.Show(new AttachmentCandidate(sa.Body,sc.Body,sa.WorldPosition,sc.WorldPosition,sa,sc),a); int rebuilds=ghost.RebuildCount;
            ghost.Show(new AttachmentCandidate(sb.Body,sc.Body,sb.WorldPosition,sc.WorldPosition,sb,sc),a);
            Assert.AreEqual(rebuilds,ghost.RebuildCount,"Changing the candidate source member must reuse the held assembly mesh pool.");
            workshop.World.TryDetachPart(b);
            ghost.Show(new AttachmentCandidate(sa.Body,sc.Body,sa.WorldPosition,sc.WorldPosition,sa,sc),a);
            Assert.AreEqual(rebuilds+1,ghost.RebuildCount,"Topology changes must refresh ghost membership.");
        }
        [UnityTest] public IEnumerator CompareFrameCostsInIdenticalWorkshops()
        {
            yield return null;
            // The reference lives in the Editor test assembly; reflection is used only
            // for setup so player builds never depend on that assembly.
            var type=Type.GetType("PhysicalAssembly.Tests.AttachmentSearchTests, PhysicalAssembly.EditTests");
            if(type==null) Assert.Ignore("Frame comparison uses the Editor-only brute-force reference.");
            var legacy=(Func<AssemblyWorld,AssemblyPart,AttachmentCandidate>)Delegate.CreateDelegate(typeof(Func<AssemblyWorld,AssemblyPart,AttachmentCandidate>),type.GetMethod("Reference"));
            foreach(int count in new[]{15,50})
            {
                foreach(var p in workshop.World.Parts.Values.ToArray()) { workshop.World.UnregisterPart(p); p.gameObject.SetActive(false); Object.Destroy(p.gameObject); }
                var held=Make(PartKind.Chassis,new Vector3(0,2,0)); var parts=new List<AssemblyPart> { held };
                for(int i=1;i<count;i++) parts.Add(Make(i<=8?PartKind.SmallCube:i<=12?PartKind.Cuboid:PartKind.Pyramid,new Vector3(2+(i%7)*.4f,2,(i/7)*.4f)));
                foreach(var part in parts) foreach(var body in part.Bodies) { body.Rigidbody.useGravity=false; body.Rigidbody.isKinematic=true; }
                foreach(bool near in new[]{false,true})
                {
                    parts[1].MainBody.Rigidbody.position=near?new Vector3(HustlePartProfile.ChassisSize.x/2+BuildingGeometry.Unit/2+.01f,2,0):new Vector3(2.4f,2,0);
                    foreach(bool reference in new[]{true,false})
                    {
                        var frames=new double[30]; var work=new double[30];
                        for(int i=-5;i<30;i++)
                        {
                            long start=Stopwatch.GetTimestamp();
                            if(reference) legacy(workshop.World,held); else workshop.World.Query(held);
                            double elapsed=(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;
                            yield return null;
                            if(i>=0) { work[i]=elapsed; frames[i]=Time.unscaledDeltaTime*1000; }
                        }
                        Array.Sort(work); Array.Sort(frames);
                        TestContext.WriteLine($"FRAME_BENCH parts={count} near={near} legacy={reference} queryMedianMs={work[15]:F4} queryP95Ms={work[28]:F4} frameMedianMs={frames[15]:F4} frameP95Ms={frames[28]:F4}");
                    }
                }
            }
        }
    }
}

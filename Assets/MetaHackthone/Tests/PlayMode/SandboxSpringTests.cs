using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PhysicalAssembly.Tests
{
    public sealed partial class DownhillTests
    {
        void HoldTicks(int ticks=1) { for(int i=0;i<ticks;i++) workshop.World.SendMessage("FixedUpdate"); }
        static void PlacePart(AssemblyPart part,Vector3 position)
        {
            var offset=position-part.MainBody.Rigidbody.position;
            foreach(var body in part.Bodies)
            { body.Rigidbody.position+=offset; body.transform.SetPositionAndRotation(body.Rigidbody.position,body.Rigidbody.rotation); }
            Physics.SyncTransforms();
        }

        [UnityTest] public IEnumerator CoreOnlyBlockerMembershipUpdatesForWorkbenchAndWholeBranches()
        {
            var world=workshop.World;
            foreach(var c in level.Core.MainBody.Colliders) Assert.IsTrue(world.IsBuildBlocker(c));
            foreach(var p in level.Kit.Where(p=>p!=level.Core)) foreach(var b in p.Bodies) foreach(var c in b.Colliders) Assert.IsFalse(world.IsBuildBlocker(c));
            var (wheel,child)=WheelBranch();
            foreach(var p in new[]{wheel,child}) foreach(var b in p.Bodies) foreach(var c in b.Colliders) Assert.IsTrue(world.IsBuildBlocker(c),"Indirect parts and tires must block.");
            Assert.IsTrue(world.TryGrab(0,wheel,Grip(wheel)));
            foreach(var p in new[]{wheel,child}) foreach(var b in p.Bodies) foreach(var c in b.Colliders) Assert.IsFalse(world.IsBuildBlocker(c),"Detached held branch must stop blocking immediately.");
            world.Release(0,true); yield return new WaitForSeconds(.35f);
            foreach(var p in new[]{wheel,child}) foreach(var b in p.Bodies) foreach(var c in b.Colliders) Assert.IsTrue(world.IsBuildBlocker(c));
            foreach(var edge in world.Connections.ToArray()) world.RemoveConnection(edge.id);
        }

        [UnityTest] public IEnumerator LooseAndOtherHeldSolidsDoNotBlockMovementOrCommittedSnap()
        {
            var world=workshop.World; var part=level.Kit[5]; var loose=level.Kit[6];
            var free=level.Core.MainBody.Rigidbody.position+Vector3.up*.6f;
            PlacePart(loose,free); PlacePart(part,free+Vector3.left*.3f);
            Assert.IsTrue(world.TryGrab(0,loose,Grip(loose)));
            Assert.IsTrue(world.TryGrab(1,part,Grip(part)));
            world.SetHoldPose(1,new Pose(free+Vector3.right*.3f,Quaternion.identity)); HoldTicks(8);
            Assert.Less(Vector3.Distance(part.MainBody.Rigidbody.position,free+Vector3.right*.3f),.001f);
            Assert.Less(Vector3.Distance(free,loose.MainBody.Rigidbody.position),.00001f);
            world.ReleaseAll();

            // Place an actual environment collider directly across the free path.
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(root.transform);
            wall.transform.position=free; wall.transform.localScale=Vector3.one*.2f;
            Assert.IsFalse(world.IsBuildBlocker(wall.GetComponent<Collider>()));
            Assert.IsTrue(world.TryGrab(1,part,Grip(part)));
            world.SetHoldPose(1,new Pose(free+Vector3.left*.3f,Quaternion.identity)); HoldTicks(8);
            Assert.Less(Vector3.Distance(part.MainBody.Rigidbody.position,free+Vector3.left*.3f),.001f);
            world.ReleaseAll(); Object.Destroy(wall);

            var target=level.Core.FindSocket("grid_center_x+_0_0"); var source=part.Sockets.First(s=>s.Id.Contains("x-"));
            var rotation=Quaternion.LookRotation(-target.Normal,target.Up)*Quaternion.Inverse(source.transform.localRotation);
            part.MainBody.Rigidbody.rotation=rotation;
            part.MainBody.transform.rotation=rotation;
            PlacePart(part,target.WorldPosition+target.Normal*.04f-rotation*source.transform.localPosition);
            var candidate=world.Query(part); Assert.IsTrue(candidate.IsValid);
            PlacePart(loose,candidate.SnappedPose.position);
            Assert.IsTrue(world.TryGrab(0,loose,Grip(loose)));
            Assert.IsTrue(world.TryGrab(1,part,Grip(part)));
            Assert.IsTrue(world.Query(part).IsValid,"Loose overlapping part must not hide the socket.");
            Assert.IsTrue(world.PlacementClear(candidate,new[]{part}));
            world.Release(1,true); yield return new WaitForSeconds(.35f);
            Assert.AreEqual(1,world.Connections.Count); Assert.AreSame(loose,world.HeldPart(0));
            Assert.Less(Vector3.Distance(source.WorldPosition,target.WorldPosition),.001f);
            world.ReleaseAll(); world.RemoveConnection(world.Connections[0].id);
        }

        [UnityTest] public IEnumerator SpringReleaseHandoffPauseAndTrackingLossPreserveResolvedPose()
        {
            var world=workshop.World; var part=level.Kit[5]; var rb=part.MainBody.Rigidbody;
            var center=level.Core.MainBody.Rigidbody.position;
            PlacePart(part,center+Vector3.left*.4f);
            var driver=new HandGrabDriver(world,0); var start=Grip(part);
            driver.Sample(true,.04f,false,start,part,true,0); driver.Sample(true,.014f,false,start,part,true,.01f);
            var desired=new Pose(center+Vector3.right*.4f,Quaternion.identity);
            world.SetHoldPose(0,desired);
            Assert.AreEqual(start.position,rb.position,"Input callbacks only update the target.");
            HoldTicks(30); Assert.Less(rb.position.x,center.x-.05f);
            var stopped=rb.position; var receiving=new Pose(stopped+Vector3.up*.03f,Quaternion.Euler(10,20,0));
            Assert.IsTrue(world.TryTransferHold(1,part,receiving)); HoldTicks(3);
            Assert.Less(Vector3.Distance(stopped,rb.position),.0001f,"Handoff must clear velocity without a pose jump.");
            world.SetHoldPose(1,new Pose(receiving.position+Vector3.right*.5f,receiving.rotation)); HoldTicks(20);
            stopped=rb.position; world.Release(1,false); yield return new WaitForSeconds(.1f);
            Assert.Less(Vector3.Distance(stopped,rb.position),.0001f); Assert.IsFalse(rb.useGravity);

            Assert.IsTrue(world.TryGrab(0,part,Grip(part))); world.SetHoldPose(0,desired); HoldTicks(15);
            stopped=rb.position; driver.Sample(false,0,false,default,null,true,1);
            Assert.IsFalse(world.HasHolds); HoldTicks(3); Assert.AreEqual(stopped,rb.position);
            Assert.IsTrue(world.TryGrab(0,part,Grip(part))); world.SetHoldPose(0,desired); HoldTicks(3);
            stopped=rb.position; level.SendMessage("OnApplicationPause",true); HoldTicks(3);
            Assert.IsFalse(world.HasHolds); Assert.AreEqual(stopped,rb.position);
            Assert.IsTrue(world.TryGrab(0,part,Grip(part))); world.SetHoldPose(0,desired);
            level.RestoreBuildImmediately(); Assert.IsFalse(world.HasHolds);
        }
    }
}

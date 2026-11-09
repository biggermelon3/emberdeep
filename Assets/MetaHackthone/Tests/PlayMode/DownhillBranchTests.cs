using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PhysicalAssembly.Tests
{
    public sealed partial class DownhillTests
    {
        ConnectionRecord JoinSockets(AssemblySocket source,AssemblySocket target,bool place=true)
        {
            if(place)
            {
                var delta=BuildingGeometry.SnapDelta(source.WorldRotation,target.WorldRotation);
                var point=source.WorldPosition;
                foreach(var body in source.Body.Part.Bodies)
                {
                    var rb=body.Rigidbody;
                    rb.position=target.WorldPosition+delta*(rb.position-point); rb.rotation=delta*rb.rotation;
                    body.transform.SetPositionAndRotation(rb.position,rb.rotation);
                }
                Physics.SyncTransforms();
            }
            var edge=new ConnectionRecord { id=Guid.NewGuid().ToString("N"),a=new BodyRef(source.Body.Part.InstanceId,source.Body.Id),b=new BodyRef(target.Body.Part.InstanceId,target.Body.Id),socketA=source.Id,socketB=target.Id,anchorA=source.transform.localPosition,anchorB=target.transform.localPosition };
            var rules=workshop.World.Rules;
            try
            {
                if(!place) workshop.World.Rules=null; // Seed a cyclic graph independent of the construction UI.
                Assert.IsTrue(workshop.World.Connect(edge)); return edge;
            }
            finally { workshop.World.Rules=rules; }
        }
        (AssemblyPart wheel,AssemblyPart child) WheelBranch()
        {
            var wheel=level.Kit.First(p=>p.Kind==PartKind.DownhillSmallWheel);
            AddWheel(workshop,wheel,1,0);
            var child=level.Kit.First(p=>p.Kind==PartKind.DownhillShortBlock);
            JoinSockets(child.Sockets.First(s=>s.Id.Contains("x-")),wheel.FindSocket("mount_opposite"));
            return (wheel,child);
        }
        static Pose Grip(AssemblyPart part) => new(part.MainBody.Rigidbody.position,Quaternion.identity);

        [UnityTest] public IEnumerator EitherHandAndDesktopDetachAndReattachWholeBranch()
        {
            var (wheel,child)=WheelBranch(); var world=workshop.World;
            foreach(int hand in new[]{0,1,2})
            {
                var corePose=Grip(level.Core);
                Assert.IsTrue(world.TryGrab(hand,wheel,Grip(wheel)));
                CollectionAssert.AreEquivalent(new[]{wheel,child},world.HeldParts(hand));
                Assert.That(world.Connections.Count,Is.EqualTo(1));
                Assert.IsFalse(child.MainBody.Anchored); Assert.IsTrue(child.MainBody.Rigidbody.isKinematic);
                Assert.That(Vector3.Distance(corePose.position,level.Core.MainBody.Rigidbody.position),Is.LessThan(.00001f));
                Assert.IsTrue(world.Query(child).IsValid,"Every held member uses the owner's attachment policy.");
                Assert.IsFalse(level.TryStart());
                world.Release(hand,true);
                Assert.IsTrue(world.IsBusy);
                yield return new WaitForSeconds(.35f);
                Assert.That(world.Connections.Count,Is.EqualTo(2));
                Assert.That(world.GetConnectedParts(level.Core.InstanceId).Count,Is.EqualTo(3));
                Assert.That(level.PendingReturnCount,Is.Zero);
            }
        }

        [Test] public void AlternatePathsStayWithCoreAndOnlyBoundaryEdgesAreRemoved()
        {
            var (wheel,child)=WheelBranch(); var world=workshop.World;
            // Graph fixture: a second route keeps the child reachable when the wheel is removed.
            JoinSockets(child.Sockets.First(s=>s.Id.Contains("x+")),level.Core.Sockets.First(s=>s.Id.Contains("x-")),false);
            var original=world.Connections.ToArray();
            Assert.IsTrue(world.TryGrab(1,wheel,Grip(wheel)));
            CollectionAssert.AreEqual(new[]{wheel},world.HeldParts(1));
            Assert.That(world.Connections.Count,Is.EqualTo(1));
            Assert.AreSame(original[2],world.Connections[0]);
            Assert.IsTrue(child.MainBody.Anchored); Assert.IsTrue(level.Core.MainBody.Anchored);
        }

        [Test] public void FailedBranchGrabsPreserveEveryConnectionAndPose()
        {
            var (wheel,child)=WheelBranch(); var world=workshop.World;
            var edges=world.Connections.ToArray(); var pose=Grip(wheel);
            var loose=level.Kit.First(p=>p.Kind==PartKind.DownhillTriangle);
            Assert.IsTrue(world.TryGrab(0,loose,Grip(loose)));
            Assert.IsFalse(world.TryGrab(0,wheel,pose));
            CollectionAssert.AreEqual(edges,world.Connections);
            world.Release(0,false);
            Assert.IsTrue(level.BeginRotation(0,level.RotationWheel.transform.position+Vector3.right*.17f));
            Assert.IsFalse(world.TryGrab(1,wheel,pose));
            Assert.IsFalse(world.TryDetachBranch(wheel,level.Core));
            CollectionAssert.AreEqual(edges,world.Connections);
            Assert.That(Vector3.Distance(pose.position,wheel.MainBody.Rigidbody.position),Is.LessThan(.00001f));
            level.EndRotation(0);
            world.EditingEnabled=false;
            Assert.IsFalse(world.TryGrab(1,wheel,pose)); CollectionAssert.AreEqual(edges,world.Connections);
            world.EditingEnabled=true;
        }

        [UnityTest] public IEnumerator AlignmentRejectsBranchGrabWithoutMutatingConstruction()
        {
            var (wheel,child)=WheelBranch(); var world=workshop.World;
            Assert.IsTrue(world.TryGrab(0,wheel,Grip(wheel))); world.Release(0,true);
            Assert.IsTrue(world.IsBusy); var edges=world.Connections.ToArray();
            Assert.IsFalse(world.TryGrab(1,child,Grip(child)));
            CollectionAssert.AreEqual(edges,world.Connections);
            yield return new WaitForSeconds(.35f);
            Assert.That(world.Connections.Count,Is.EqualTo(2));
        }

        [TestCase(0)] [TestCase(1)] public void FreshPinchTransfersRootWithoutPoseJumpOrDuplicateOwnership(int first)
        {
            var (wheel,child)=WheelBranch(); var world=workshop.World;
            int second=1-first; var drivers=new[]{new HandGrabDriver(world,0),new HandGrabDriver(world,1)};
            float now=0;
            void Sample(int hand,float distance,AssemblyPart part,Pose pose) => drivers[hand].Sample(true,distance,false,pose,part,true,now+=.01f);
            Sample(first,.04f,wheel,Grip(wheel)); Sample(first,.014f,wheel,Grip(wheel));
            var moved=Grip(wheel); moved.position+=Vector3.up*.25f; moved.rotation=Quaternion.Euler(20,50,10);
            Sample(first,.014f,wheel,moved);
            var bodies=world.HeldParts(first).SelectMany(p=>p.Bodies).ToArray();
            var poses=bodies.Select(b=>new Pose(b.Rigidbody.position,b.Rigidbody.rotation)).ToArray();
            var receiving=Grip(child); receiving.position+=Vector3.one*.03f; receiving.rotation=Quaternion.Euler(70,10,40);
            int events=0;
            world.HoldsChanged+=()=> { events++; Assert.LessOrEqual((world.HeldPart(0)?1:0)+(world.HeldPart(1)?1:0),1); };
            Sample(second,.04f,wheel,receiving); Sample(second,.014f,wheel,receiving);
            Assert.AreEqual(1,events); Assert.IsNull(world.HeldPart(first)); Assert.AreSame(wheel,world.HeldPart(second));
            CollectionAssert.AreEquivalent(new[]{wheel,child},world.HeldParts(second));
            Sample(first,.014f,wheel,moved); // A still-closed previous owner cannot steal it back.
            Assert.IsNull(world.HeldPart(first));
            world.Release(first,true); drivers[first].Cancel();
            Sample(first,.014f,wheel,moved); // Tracking/focus reset still requires opening.
            Sample(second,.014f,wheel,receiving);
            for(int i=0;i<bodies.Length;i++)
            {
                Assert.Less(Vector3.Distance(poses[i].position,bodies[i].Rigidbody.position),.00001f);
                Assert.Less(Quaternion.Angle(poses[i].rotation,bodies[i].Rigidbody.rotation),.05f);
            }
            Assert.That(level.PendingReturnCount,Is.Zero); Assert.That(world.Connections.Count,Is.EqualTo(1));
            for(int n=0;n<4;n++)
            {
                int to=n%2==0?first:second;
                Sample(to,.04f,wheel,Grip(wheel)); Sample(to,.014f,wheel,Grip(wheel));
                Assert.AreSame(wheel,world.HeldPart(to)); Assert.IsNull(world.HeldPart(1-to));
                Assert.That(level.PendingReturnCount,Is.Zero);
            }
        }

        [Test] public void OccupiedReceivingHandCannotTransferAndTrackingLossDropsOneGroup()
        {
            var (wheel,child)=WheelBranch(); var world=workshop.World;
            var driver=new HandGrabDriver(world,0);
            driver.Sample(true,.04f,false,Grip(wheel),wheel,true,0);
            driver.Sample(true,.014f,false,Grip(wheel),wheel,true,.01f);
            var other=level.Kit.First(p=>p.Kind==PartKind.DownhillTriangle);
            Assert.IsTrue(world.TryGrab(1,other,Grip(other)));
            Assert.IsFalse(world.TryTransferHold(1,child,Grip(child)));
            Assert.AreSame(wheel,world.HeldPart(0)); Assert.AreSame(other,world.HeldPart(1));
            driver.Sample(false,0,false,default,null,true,.2f); Assert.IsNotNull(world.HeldPart(0));
            driver.Sample(false,0,false,default,null,true,.4f); Assert.IsNull(world.HeldPart(0));
            Assert.That(level.PendingReturnCount,Is.Zero);
            Assert.That(world.Connections.Count,Is.EqualTo(1));
        }

        [Test] public void DroppedBranchStaysConnectedAndFloating()
        {
            var (wheel,child)=WheelBranch(); var world=workshop.World;
            Assert.IsTrue(world.TryGrab(0,wheel,Grip(wheel)));
            var pose=Grip(wheel); pose.position+=Vector3.up*.1f; world.SetHoldPose(0,pose); world.Release(0,false);
            var dropped=wheel.MainBody.Rigidbody.position;
            level.TickReturns(100); Assert.That(level.PendingReturnCount,Is.Zero);
            Assert.That(world.Connections.Count,Is.EqualTo(1));
            Assert.Less(Vector3.Distance(dropped,wheel.MainBody.Rigidbody.position),.0001f);
            Assert.IsTrue(world.TryGrab(1,child,Grip(child))); world.Release(1,false);
            Assert.IsTrue(wheel.MainBody.Rigidbody.isKinematic); Assert.IsTrue(child.MainBody.Rigidbody.isKinematic);
        }

        [UnityTest] public IEnumerator BothHubsCanBeOccupiedWhileTireRotatesFreely()
        {
            var (wheel,child)=WheelBranch();
            Assert.IsTrue(workshop.World.SocketOccupied(wheel.FindSocket("mount")));
            Assert.IsTrue(workshop.World.SocketOccupied(wheel.FindSocket("mount_opposite")));
            var tire=wheel.FindBody("tire").Rigidbody; var before=tire.rotation;
            tire.isKinematic=false; tire.useGravity=false;
            tire.angularVelocity=wheel.MainBody.Rigidbody.rotation*Vector3.right*8;
            yield return new WaitForSeconds(.15f);
            Assert.Greater(Quaternion.Angle(before,tire.rotation),10);
            Assert.That(workshop.World.Connections.Count,Is.EqualTo(2));
            Assert.IsFalse(tire.GetComponent<HingeJoint>().useMotor);
            Assert.IsTrue(level.TryStart());
            Assert.That(level.SimulationWorld.Connections.Count,Is.EqualTo(2));
            var clone=level.SimulationWorld.Parts.Values.Single(p=>p.Kind==wheel.Kind);
            Assert.IsTrue(level.SimulationWorld.SocketOccupied(clone.FindSocket("mount")));
            Assert.IsTrue(level.SimulationWorld.SocketOccupied(clone.FindSocket("mount_opposite")));
            Assert.IsTrue(level.TryReturn());
        }
    }
}

using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PhysicalAssembly.Tests
{
    public sealed partial class DownhillTests
    {
        void ApproachWheel(AssemblyPart wheel,AssemblySocket target,bool opposite,float gap=.01f)
        {
            var rotation=Quaternion.LookRotation(-target.Normal,target.Up)*Quaternion.Inverse(wheel.FindSocket("mount").transform.localRotation);
            if(opposite) rotation=Quaternion.AngleAxis(180,target.Up)*rotation;
            foreach(var body in wheel.Bodies)
            {
                body.Rigidbody.position=target.WorldPosition+target.Normal*(.0285f+gap);
                body.Rigidbody.rotation=rotation;
                body.transform.SetPositionAndRotation(body.Rigidbody.position,rotation);
            }
            Physics.SyncTransforms();
        }

        [UnityTest] public IEnumerator BothWheelFacesPreviewAndAttachOutsideBlock()
        {
            foreach(var kind in new[]{PartKind.DownhillSmallWheel,PartKind.DownhillLargeWheel}) foreach(bool opposite in new[]{false,true})
            {
                SetHeading(level,37);
                var wheel=level.Kit.First(p=>p.Kind==kind);
                var target=level.Core.Sockets.First(s=>s.Id.Contains("x+"));
                ApproachWheel(wheel,target,opposite);
                Assert.IsTrue(workshop.World.TryGrab(1,wheel,new Pose(wheel.MainBody.Rigidbody.position,Quaternion.identity)));
                var candidate=workshop.World.Query(wheel);
                Assert.IsTrue(candidate.IsValid,kind+" opposite="+opposite);
                Assert.AreSame(target,candidate.targetSocket);
                var mount=wheel.FindSocket(opposite?"mount_opposite":"mount");
                Assert.AreSame(mount,candidate.sourceSocket);
                Assert.That(Quaternion.Angle(Quaternion.identity,candidate.rotationDelta),Is.LessThan(.05f),"The approaching face must not flip to the other mount.");
                Assert.IsTrue(workshop.World.PlacementClear(candidate,new[]{wheel}));
                workshop.World.Release(1,true);
                Assert.IsTrue(workshop.World.IsBusy,"Release must accept the same acquisition as the preview.");
                Assert.IsFalse(level.TryStart());
                yield return new WaitForSeconds(.35f);
                Assert.That(workshop.World.Connections.Count,Is.EqualTo(1));
                Assert.That(Vector3.Distance(mount.WorldPosition,target.WorldPosition),Is.LessThan(.0001f));
                Assert.That(Vector3.Dot(mount.Normal,target.Normal),Is.LessThan(-.999f));
                Assert.IsTrue(workshop.World.TryDetachPart(wheel));
                foreach(var body in wheel.Bodies) { body.Rigidbody.position=new Vector3(0,5,0); body.transform.position=body.Rigidbody.position; }
            }
        }

        [TestCase(false)] [TestCase(true)] public void WheelAcquisitionKeepsDistanceBoundary(bool opposite)
        {
            var wheel=level.Kit.First(p=>p.Kind==PartKind.DownhillSmallWheel);
            var target=level.Core.Sockets.First(s=>s.Id.Contains("x+"));
            ApproachWheel(wheel,target,opposite,settings.SnapRadius-.001f); Assert.IsTrue(workshop.World.Query(wheel).IsValid);
            ApproachWheel(wheel,target,opposite,settings.SnapRadius+.002f); Assert.IsFalse(workshop.World.Query(wheel).IsValid);
            Assert.That(wheel.Sockets.Length,Is.EqualTo(2));
        }

        [Test] public void OccupiedWheelSocketAndBlockedPlacementAreRejected()
        {
            var wheel=level.Kit.First(p=>p.Kind==PartKind.DownhillSmallWheel);
            var target=level.Core.Sockets.First(s=>s.Id.Contains("x+"));
            ApproachWheel(wheel,target,true);
            var candidate=workshop.World.Query(wheel); Assert.IsTrue(candidate.IsValid);
            var blocker=level.Kit.First(p=>p.Kind==PartKind.DownhillShortBlock);
            blocker.MainBody.Rigidbody.position=candidate.SnappedPose.position;
            blocker.MainBody.transform.position=blocker.MainBody.Rigidbody.position;
            Physics.SyncTransforms();
            Assert.IsTrue(workshop.World.PlacementClear(candidate,new[]{wheel}),"Loose geometry must not veto a snap.");
            var blockingEdge=JoinSockets(blocker.Sockets.First(s=>s.Id.Contains("x-")),level.Core.FindSocket("grid_center_x-_0_0"),false);
            Assert.IsFalse(workshop.World.PlacementClear(candidate,new[]{wheel}));
            workshop.World.RemoveConnection(blockingEdge.id);
            blocker.MainBody.Rigidbody.position=new Vector3(0,5,0); blocker.MainBody.transform.position=blocker.MainBody.Rigidbody.position;
            AddWheel(workshop,wheel,1,target.transform.localPosition.z);
            Assert.IsTrue(workshop.World.SocketOccupied(target));
            var other=level.Kit.Last(p=>p.Kind==PartKind.DownhillSmallWheel);
            ApproachWheel(other,target,true);
            Assert.IsFalse(workshop.World.Query(other).IsValid,"An occupied face must not flip the wheel onto a backwards-facing socket.");
            Assert.IsFalse(workshop.World.Connect(new ConnectionRecord { id=System.Guid.NewGuid().ToString("N"),a=new BodyRef(other.InstanceId,"hub"),b=new BodyRef(level.Core.InstanceId,"body"),socketA="mount",socketB=target.Id,anchorA=other.FindSocket("mount").transform.localPosition,anchorB=target.transform.localPosition }));
        }

        [TestCase(0)] [TestCase(1)] public void RotationKeepsCoreFixedAndOtherHandIndependent(int hand)
        {
            var wheel=level.Kit.First(p=>p.Kind==PartKind.DownhillLargeWheel); AddWheel(workshop,wheel,1,0);
            var loose=level.Kit[5]; int other=1-hand;
            var loosePose=new Pose(loose.MainBody.Rigidbody.position,Quaternion.identity);
            Assert.IsTrue(workshop.World.TryGrab(other,loose,loosePose));
            var bodies=workshop.World.GetConnectedParts(level.Core.InstanceId).SelectMany(p=>p.Bodies).ToArray();
            var positions=bodies.Select(b=>b.Rigidbody.position).ToArray(); var rotations=bodies.Select(b=>b.Rigidbody.rotation).ToArray();
            var pivot=level.Core.MainBody.Rigidbody.position; var center=level.RotationWheel.transform.position;
            Assert.IsTrue(level.BeginRotation(hand,center+Vector3.right*.17f));
            Assert.IsFalse(level.BeginRotation(other,center+Vector3.left*.17f));
            foreach(float degrees in new[]{90f,179f,181f,270f,360f,450f})
            {
                var yaw=Quaternion.AngleAxis(degrees,Vector3.up);
                level.Rotate(hand,center+yaw*Vector3.right*.17f+Vector3.up*.1f);
                Assert.That(Vector3.Distance(pivot,level.Core.MainBody.Rigidbody.position),Is.LessThan(.0001f));
                for(int i=0;i<bodies.Length;i++)
                {
                    Assert.That(Vector3.Distance(pivot+yaw*(positions[i]-pivot),bodies[i].Rigidbody.position),Is.LessThan(.0001f));
                    Assert.That(Quaternion.Angle(yaw*rotations[i],bodies[i].Rigidbody.rotation),Is.LessThan(.05f));
                }
                Assert.That(Vector3.Distance(loosePose.position,loose.MainBody.Rigidbody.position),Is.LessThan(.0001f));
                Assert.IsFalse(workshop.World.Query(loose).IsValid);
                Assert.IsFalse(level.CanDetach(wheel));
            }
            workshop.World.Release(other,true);
            Assert.That(workshop.World.Connections.Count,Is.EqualTo(1));
            level.CancelRotation(); Assert.IsFalse(workshop.World.HasHolds);
        }

        [Test] public void FailedSimulationCreationRestoresBuildAndGhost()
        {
            var wheel=level.Kit.First(p=>p.Kind==PartKind.DownhillLargeWheel); AddWheel(workshop,wheel,1,0);
            SetHeading(level,67); level.SpawnPreview.Refresh();
            var bodies=workshop.World.GetConnectedParts(level.Core.InstanceId).SelectMany(p=>p.Bodies).ToArray();
            var before=bodies.Select(b=>new Pose(b.Rigidbody.position,b.Rigidbody.rotation)).ToArray();
            var connection=workshop.World.Connections.Single(); var socket=connection.socketB;
            try
            {
                connection.socketB="missing socket to exercise reconstruction failure";
                Assert.IsFalse(level.TryStart()); Assert.IsTrue(level.IsBuilding); Assert.IsTrue(workshop.World.EditingEnabled);
                Assert.IsNull(level.SimulationWorld); Assert.IsNull(level.SimulationCore);
                Assert.That(level.SpawnPreview.GetComponentsInChildren<Renderer>().Length,Is.EqualTo(19));
                for(int i=0;i<bodies.Length;i++)
                {
                    Assert.IsTrue(bodies[i].gameObject.activeInHierarchy);
                    Assert.That(Vector3.Distance(before[i].position,bodies[i].Rigidbody.position),Is.LessThan(.0001f));
                    Assert.That(Quaternion.Angle(before[i].rotation,bodies[i].Rigidbody.rotation),Is.LessThan(.05f));
                }
            }
            finally { connection.socketB=socket; }
            Assert.IsTrue(level.TryStart()); Assert.IsTrue(level.TryReturn());
        }

        [Test] public void PreviewTracksTopologyRotationAndMatchesEveryLaunchedBody()
        {
            var preview=level.SpawnPreview; preview.Refresh(); Assert.That(preview.MeshCount,Is.EqualTo(1));
            var wheel=level.Kit.First(p=>p.Kind==PartKind.DownhillLargeWheel); AddWheel(workshop,wheel,1,0);
            preview.Refresh(); Assert.That(preview.MeshCount,Is.EqualTo(19));
            int rebuilds=preview.RebuildCount;
            var fixedPoses=preview.GetComponentsInChildren<MeshFilter>().Select(f=>new Pose(f.transform.position,f.transform.rotation)).ToArray();
            SetHeading(level,123); preview.Refresh(); Assert.That(preview.RebuildCount,Is.EqualTo(rebuilds));
            var rotatedMeshes=preview.GetComponentsInChildren<MeshFilter>();
            for(int i=0;i<fixedPoses.Length;i++)
            {
                Assert.That(Vector3.Distance(fixedPoses[i].position,rotatedMeshes[i].transform.position),Is.LessThan(.0001f));
                Assert.That(Quaternion.Angle(fixedPoses[i].rotation,rotatedMeshes[i].transform.rotation),Is.LessThan(.01f));
            }
            workshop.ToggleReposition(); workshop.PlaceArena(new Vector3(2,0,-3),Quaternion.Euler(0,217,0));
            preview.Refresh(); var placement=level.SandboxPlacement(out var delta);
            var origin=level.Core.MainBody.Rigidbody.position;
            var parts=workshop.World.GetConnectedParts(level.Core.InstanceId);
            var before=parts.SelectMany(p=>p.Bodies).ToDictionary(b=>b,b=>new Pose(b.Rigidbody.position,b.Rigidbody.rotation));
            var visualPoses=preview.GetComponentsInChildren<MeshFilter>().Select(f=>new Pose(f.transform.position,f.transform.rotation)).ToArray();
            Assert.That(preview.GetComponentsInChildren<Collider>(true),Is.Empty);
            Assert.That(preview.GetComponentsInChildren<Rigidbody>(true),Is.Empty);
            var approach=level.Course.transform.TransformVector(level.Presentation.TestPosition-level.Presentation.BuildPosition);
            placement.position+=approach;
            for(int i=0;i<visualPoses.Length;i++) visualPoses[i].position+=approach;
            Assert.IsTrue(level.TryStart());
            Assert.That(preview.GetComponentsInChildren<Renderer>().Length,Is.Zero);
            foreach(var body in level.SimulationWorld.Parts.Values.SelectMany(p=>p.Bodies)) foreach(var collider in body.Colliders)
                foreach(var ground in level.Course.GetComponentsInChildren<Collider>())
                    Assert.IsFalse(Physics.ComputePenetration(collider,collider.transform.position,collider.transform.rotation,ground,ground.transform.position,ground.transform.rotation,out _,out float depth) && depth>.001f,"Enlarged launch penetrates "+ground.name);
            Assert.That(Vector3.Distance(preview.CorePose.position+approach,level.SimulationCore.MainBody.Rigidbody.position),Is.LessThan(.00001f));
            foreach(var part in parts)
            {
                var clone=level.SimulationWorld.Parts.Values.Single(p=>p.Kind==part.Kind);
                foreach(var body in part.Bodies)
                {
                    var actual=clone.FindBody(body.Id).Rigidbody;
                    Assert.That(Vector3.Distance(placement.position+delta*(before[body].position-origin)*level.TransferScale,actual.position),Is.LessThan(.00001f));
                    Assert.That(Quaternion.Angle(delta*before[body].rotation,actual.rotation),Is.LessThan(.05f));
                }
                foreach(var mesh in clone.VisualMeshes.Where(f=>f.name!="Grid points"))
                    Assert.IsTrue(visualPoses.Any(p=>Vector3.Distance(p.position,mesh.transform.position)<.0001f && Quaternion.Angle(p.rotation,mesh.transform.rotation)<.05f),"Launched mesh must match the visible ghost: "+mesh.name);
            }
            Assert.IsTrue(level.TryReturn()); Assert.That(preview.GetComponentsInChildren<Renderer>().Length,Is.EqualTo(19));
            Assert.IsTrue(workshop.World.TryDetachPart(wheel)); preview.Refresh(); Assert.That(preview.MeshCount,Is.EqualTo(1));
        }
    }
}

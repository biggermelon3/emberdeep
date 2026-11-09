using System;
using UnityEngine;

namespace PhysicalAssembly
{
    public enum WorkshopMode { Build, Drive }
    public enum PartKind { Chassis, Wheel, Beam, Block, Weapon, SmallCube, Cuboid, Pyramid, PhysicsSphere, DownhillLongBlock, DownhillShortBlock, DownhillTriangle, DownhillIrregular, DownhillSmallWheel, DownhillLargeWheel, DownhillCore, DownhillCube, DownhillDrill }
    [Serializable] public struct BodyRef
    {
        public string partId, bodyId;
        public BodyRef(string part, string body) { partId = part; bodyId = body; }
    }
    [Serializable] public sealed class ConnectionRecord
    {
        public string id;
        public BodyRef a, b;
        public string socketA, socketB;
        public long attachmentOrder;
        public bool hierarchyEdge, parentIsA;
        public Vector3 anchorA, anchorB;
    }
    public readonly struct AssemblyControlFrame
    {
        public readonly float drive, steer;
        public readonly bool fireHeld;
        public readonly AssemblyPart controller;
        public AssemblyControlFrame(float drive, float steer, bool fire, AssemblyPart controller)
        { this.drive = Mathf.Clamp(drive, -1, 1); this.steer = Mathf.Clamp(steer, -1, 1); fireHeld = fire; this.controller = controller; }
    }
    public interface IAssemblyControlReceiver { void ApplyControl(in AssemblyControlFrame frame, float dt); }
    public readonly struct AttachmentCandidate
    {
        public readonly AssemblyBody source, target;
        public readonly AssemblySocket sourceSocket, targetSocket;
        public readonly Vector3 sourcePoint, targetPoint;
        public readonly Quaternion rotationDelta;
        public readonly Pose SnappedPose;
        public bool IsValid => source && target;
        internal AttachmentCandidate(AssemblyBody a,AssemblyBody b,Vector3 pa,Vector3 pb,AssemblySocket sa,AssemblySocket sb,Quaternion delta,Pose sourcePose)
        { source=a; target=b; sourcePoint=pa; targetPoint=pb; sourceSocket=sa; targetSocket=sb; rotationDelta=delta; SnappedPose=new Pose(pb+delta*(sourcePose.position-pa),delta*sourcePose.rotation); }
        public AttachmentCandidate(AssemblyBody a, AssemblyBody b, Vector3 pa, Vector3 pb, AssemblySocket sa = null, AssemblySocket sb = null)
        { source = a; target = b; sourcePoint = pa; targetPoint = pb; sourceSocket = sa; targetSocket = sb; rotationDelta=sa && sb?BuildingGeometry.SnapDelta(sa,sb):Quaternion.identity; SnappedPose=a?new Pose(pb+rotationDelta*(a.Rigidbody.position-pa),rotationDelta*a.Rigidbody.rotation):default; }
    }
}

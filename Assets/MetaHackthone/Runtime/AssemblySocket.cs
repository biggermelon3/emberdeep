using UnityEngine;
namespace PhysicalAssembly
{
    public sealed class AssemblySocket : MonoBehaviour
    {
        public string Id;
        public string Family = "mount";
        public AssemblyBody Body;
        public bool FaceMount;
        public bool ApproachAllowed(AssemblySocket target,Vector3 sourcePoint,Vector3 sourceNormal,Vector3 targetPoint,Vector3 targetNormal)
            => (!FaceMount || Vector3.Dot(sourceNormal,targetNormal)<0)
                && (!(FaceMount || target.FaceMount) || Vector3.Dot(sourcePoint-targetPoint,targetNormal)>=-.001f);
        public bool CanAcquire(AssemblySocket target,float radius)
        {
            return target && (WorldPosition-target.WorldPosition).sqrMagnitude<=radius*radius
                && ApproachAllowed(target,WorldPosition,Normal,target.WorldPosition,target.Normal);
        }
        // Use authoritative physics poses: interpolated render transforms can lag a hand move.
        public Vector3 WorldPosition => Body.Rigidbody.position+Body.Rigidbody.rotation*transform.localPosition;
        public Quaternion WorldRotation => Body.Rigidbody.rotation*transform.localRotation;
        public Vector3 Normal => WorldRotation*Vector3.forward;
        public Vector3 Up => WorldRotation*Vector3.up;
    }
}

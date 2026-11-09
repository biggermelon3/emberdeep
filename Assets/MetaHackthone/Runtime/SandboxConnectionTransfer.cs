using System.Linq;
using UnityEngine;
namespace PhysicalAssembly
{
    // Animation transforms frozen snapshots; it never integrates vehicle physics.
    public sealed class SandboxConnectionTransfer
    {
        sealed class Snapshot
        {
            public Rigidbody Body;
            public Vector3 Position,Velocity,Angular;
            public Quaternion Rotation;
            public bool Kinematic,Gravity,Collisions;
            public CollisionDetectionMode Detection;
        }
        readonly Snapshot[] saved;
        Pose frame;
        public SandboxConnectionTransfer(Rigidbody[] bodies,Pose entrance)
        {
            frame=entrance;var inverse=Quaternion.Inverse(frame.rotation);
            saved=bodies.Select(b=>new Snapshot {Body=b,Position=inverse*(b.position-frame.position),Rotation=inverse*b.rotation,
                Velocity=inverse*b.linearVelocity,Angular=inverse*b.angularVelocity,Kinematic=b.isKinematic,Gravity=b.useGravity,
                Collisions=b.detectCollisions,Detection=b.collisionDetectionMode}).ToArray();
            foreach(var s in saved){var b=s.Body;if(!b.isKinematic){b.linearVelocity=Vector3.zero;b.angularVelocity=Vector3.zero;}
                b.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;b.isKinematic=true;b.useGravity=false;b.detectCollisions=false;}
        }
        public void Move(Pose next)
        {
            frame=next;
            foreach(var s in saved)if(s.Body){var b=s.Body;b.position=frame.position+frame.rotation*s.Position;b.rotation=frame.rotation*s.Rotation;b.transform.SetPositionAndRotation(b.position,b.rotation);}
        }
        public void Release(Rigidbody core,Pose pose,Vector3 velocity)
        {
            var origin=core.position;var delta=pose.rotation*Quaternion.Inverse(core.rotation);
            foreach(var s in saved)if(s.Body){var b=s.Body;b.position=pose.position+delta*(b.position-origin);b.rotation=delta*b.rotation;b.transform.SetPositionAndRotation(b.position,b.rotation);}
            foreach(var s in saved)if(s.Body){var b=s.Body;b.isKinematic=s.Kinematic;b.useGravity=s.Gravity;b.detectCollisions=s.Collisions;b.collisionDetectionMode=s.Detection;
                if(!b.isKinematic){b.linearVelocity=velocity;b.angularVelocity=Vector3.zero;b.WakeUp();}}
            Physics.SyncTransforms();
        }
        public void Restore(float forwardVelocityDelta=0)
        {
            foreach(var s in saved)if(s.Body){var b=s.Body;b.isKinematic=s.Kinematic;b.useGravity=s.Gravity;b.detectCollisions=s.Collisions;b.collisionDetectionMode=s.Detection;
                if(!b.isKinematic){b.linearVelocity=frame.rotation*(s.Velocity+Vector3.forward*forwardVelocityDelta);b.angularVelocity=frame.rotation*s.Angular;b.WakeUp();}}
            Physics.SyncTransforms();
        }
    }
}

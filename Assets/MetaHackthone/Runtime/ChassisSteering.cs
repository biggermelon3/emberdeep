using UnityEngine;

namespace PhysicalAssembly
{
    // One yaw controller per driven assembly. Wheel motors still provide traction.
    public sealed class ChassisSteering : MonoBehaviour, IAssemblyControlReceiver
    {
        public float MaxYawRate=60f, Response=40f, MaxYawAcceleration=60f;
        public void ApplyControl(in AssemblyControlFrame frame,float dt)
        {
            var car=GetComponent<AssemblyPart>();
            if(frame.controller!=car || !car.World || car.World.HasHolds || car.World.IsBusy || Mathf.Abs(frame.steer)<.01f) return;
            var chassis=car.MainBody.Rigidbody;
            if(chassis.isKinematic || Vector3.Dot(chassis.transform.up,Vector3.up)<.5f) return;
            var parts=car.World.GetConnectedParts(car.InstanceId);
            int grounded=0;
            foreach(var part in parts)
            {
                var motor=part.GetComponent<WheelMotor>();
                if(!motor || !motor.Wheel || motor.Wheel.Rigidbody.isKinematic) continue;
                foreach(var hit in Physics.RaycastAll(motor.Wheel.Rigidbody.position,Vector3.down,HustlePartProfile.WheelDiameter*.5f+.015f,~0,QueryTriggerInteraction.Ignore))
                    if(!hit.collider.GetComponentInParent<AssemblyBody>() && Vector3.Dot(hit.normal,Vector3.up)>.5f) { grounded++; break; }
            }
            if(grounded<2) return;
            // Convert assembly angular acceleration to torque, accounting for the
            // attached modules. The thin chassis alone has very little inertia.
            float mass=0; var center=Vector3.zero;
            foreach(var part in parts) foreach(var body in part.Bodies)
            { mass+=body.Rigidbody.mass; center+=body.Rigidbody.worldCenterOfMass*body.Rigidbody.mass; }
            center/=mass;
            float inertia=0;
            foreach(var part in parts) foreach(var body in part.Bodies)
            {
                var rb=body.Rigidbody;
                var axis=Quaternion.Inverse(rb.rotation*rb.inertiaTensorRotation)*Vector3.up;
                inertia+=Vector3.Dot(Vector3.Scale(axis,axis),rb.inertiaTensor)
                    +rb.mass*Vector3.ProjectOnPlane(rb.worldCenterOfMass-center,Vector3.up).sqrMagnitude;
            }
            float error=frame.steer*MaxYawRate*Mathf.Deg2Rad-Vector3.Dot(chassis.angularVelocity,Vector3.up);
            chassis.AddTorque(Vector3.up*(inertia*Mathf.Clamp(error*Response,-MaxYawAcceleration,MaxYawAcceleration)),ForceMode.Force);
        }
    }
}

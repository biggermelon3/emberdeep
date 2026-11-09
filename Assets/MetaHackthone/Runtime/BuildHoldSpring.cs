using System;
using UnityEngine;

namespace PhysicalAssembly
{
    [Serializable]
    public sealed class BuildSpringSettings
    {
        [Min(.1f)] public float Frequency=6;
        [Range(.1f,2f)] public float DampingRatio=.85f;
        [Min(.001f)] public float MaximumDisplacement=.15f;
        [Range(1,180)] public float MaximumAngularDisplacement=45;
        [Min(.01f)] public float MaximumSpeed=1.5f;
        [Min(1)] public float MaximumAngularSpeed=540;
        [Min(.0001f)] public float PositionTolerance=.001f;
        [Min(.01f)] public float AngularTolerance=1;
    }

    // Kinematic collision solver + a physical damped spring, never a spring on the tracked hand.
    public sealed class BuildHoldSpring
    {
        public Pose Pose { get; private set; }
        public Vector3 Velocity { get; private set; }
        public Vector3 AngularVelocity { get; private set; }
        public bool Recovering { get; private set; }
        public BuildHoldSpring(Pose pose) { Pose=pose; }
        static Vector3 RotationError(Quaternion target,Quaternion current)
        {
            var delta=target*Quaternion.Inverse(current);
            if(delta.w<0) delta=new Quaternion(-delta.x,-delta.y,-delta.z,-delta.w);
            delta.ToAngleAxis(out float degrees,out var axis);
            return degrees<.0001f || axis.sqrMagnitude<.001f?Vector3.zero:axis.normalized*(Mathf.DeltaAngle(0,degrees)*Mathf.Deg2Rad);
        }
        static Vector3 Integrate(Vector3 velocity,Vector3 error,float maximumError,float maximumSpeed,float omega,float damping,float dt)
        {
            // Backward Euler stays stable; displacement clamping prevents wind-up.
            float stiffness=omega*omega;
            return Vector3.ClampMagnitude((velocity+dt*stiffness*Vector3.ClampMagnitude(error,maximumError)) /
                (1+2*damping*omega*dt+stiffness*dt*dt),maximumSpeed);
        }
        public BuildMotionConstraint.Result Tick(Pose target,float dt,BuildSpringSettings settings,BuildMotionConstraint.Group group,Func<Collider,bool> blocks)
        {
            var proposed=target;
            if(Recovering)
            {
                float omega=2*Mathf.PI*Mathf.Max(.1f,settings.Frequency);
                Velocity=Integrate(Velocity,target.position-Pose.position,settings.MaximumDisplacement,settings.MaximumSpeed,omega,settings.DampingRatio,dt);
                AngularVelocity=Integrate(AngularVelocity,RotationError(target.rotation,Pose.rotation),settings.MaximumAngularDisplacement*Mathf.Deg2Rad,settings.MaximumAngularSpeed*Mathf.Deg2Rad,omega,settings.DampingRatio,dt);
                float angularSpeed=AngularVelocity.magnitude;
                proposed=new Pose(Pose.position+Velocity*dt,angularSpeed<.00001f?Pose.rotation:Quaternion.AngleAxis(angularSpeed*dt*Mathf.Rad2Deg,AngularVelocity/angularSpeed)*Pose.rotation);
            }
            var result=group.Move(Pose,proposed,blocks,true); Pose=result.Pose;
            if(result.Contact)
            {
                Recovering=true; Velocity=result.ClipVelocity(Velocity);
                if(result.RotationBlocked) AngularVelocity=Vector3.zero;
            }
            else if(Recovering && Vector3.Distance(Pose.position,target.position)<settings.PositionTolerance &&
                Quaternion.Angle(Pose.rotation,target.rotation)<settings.AngularTolerance && Velocity.magnitude<.01f && AngularVelocity.magnitude<Mathf.Deg2Rad*5)
            { Recovering=false; Velocity=AngularVelocity=Vector3.zero; }
            return result;
        }
    }
}

using UnityEngine;
namespace PhysicalAssembly
{
    public sealed class WheelMotor : MonoBehaviour,IAssemblyControlReceiver
    {
        public AssemblyBody Wheel,Hub;
        public float MotorTorque=.12f, BrakeTorque=.04f, MaxSpeed=12;
        public AnimationCurve TorqueCurve=HustleBehavior.ControlTorqueCurve();
        public void ApplyControl(in AssemblyControlFrame frame,float dt)
        {
            if(!Wheel || Wheel.Rigidbody.isKinematic || !frame.controller) return;
            var control=frame.controller.MainBody.transform;
            float side=Mathf.Sign(Vector3.Dot(Wheel.transform.position-control.position,control.right));
            // Full steering brakes the inside wheels even at full throttle. A
            // positive drive demand on both sides can stall yaw against tire friction.
            float demand=Mathf.Clamp(frame.drive-frame.steer*side,-1,1);
            var axle=Wheel.transform.right; float sign=Mathf.Sign(Vector3.Dot(axle,control.right));
            float speed=Vector3.Dot(Wheel.Rigidbody.angularVelocity,axle)*sign;
            // Counter-torque stays available while reversing, including above the speed limit.
            float limiter=speed*demand>0?Mathf.Clamp01(1-Mathf.Abs(speed)/MaxSpeed):1;
            float torque=demand*MotorTorque*Mathf.Max(0,TorqueCurve.Evaluate(speed*Mathf.Rad2Deg)/350f)*limiter;
            if(Mathf.Abs(demand)<.01f) torque=-Mathf.Clamp(speed*.025f,-BrakeTorque,BrakeTorque);
            Wheel.Rigidbody.AddTorque(axle*(torque*sign));
            if(!Hub.Rigidbody.isKinematic) Hub.Rigidbody.AddTorque(-axle*(torque*sign));
        }
    }
}

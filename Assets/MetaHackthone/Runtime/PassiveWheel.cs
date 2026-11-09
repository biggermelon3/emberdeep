using UnityEngine;

namespace PhysicalAssembly
{
    // Dissipates energy only while touching the road. Never propels the wheel.
    public sealed class PassiveWheel : MonoBehaviour
    {
        public float Resistance = .035f, Radius = .06f;
        Rigidbody body;
        float contactUntil;
        void Awake() => body=GetComponent<Rigidbody>();
        void OnCollisionStay(Collision collision)
        {
            for(int i=0;i<collision.contactCount;i++)
                if(Vector3.Dot(collision.GetContact(i).normal,Vector3.up)>.35f) { contactUntil=Time.fixedTime+Time.fixedDeltaTime*2; break; }
        }
        void FixedUpdate()
        {
            if(body.isKinematic || Time.fixedTime>contactUntil) return;
            var axis=transform.right;
            float speed=Vector3.Dot(body.angularVelocity,axis);
            float deceleration=Resistance/Mathf.Max(.005f,Radius);
            body.AddTorque(-axis*Mathf.Sign(speed)*Mathf.Min(Mathf.Abs(speed)/Time.fixedDeltaTime,deceleration),ForceMode.Acceleration);
            var forward=Vector3.Cross(axis,Vector3.up).normalized;
            float rolling=Vector3.Dot(body.linearVelocity,forward);
            body.AddForce(-forward*Mathf.Sign(rolling)*Mathf.Min(Mathf.Abs(rolling)/Time.fixedDeltaTime,Resistance),ForceMode.Acceleration);
            // Lateral tire grip is dissipative and independent of rolling resistance.
            body.AddForce(-axis*Vector3.Dot(body.linearVelocity,axis)*12,ForceMode.Acceleration);
            // A tire also resists twisting across the contact patch. Dampen
            // scrub only: rotation around the axle remains free to roll.
            body.AddTorque(-Vector3.ProjectOnPlane(body.angularVelocity,axis)*Mathf.Min(12,1/Time.fixedDeltaTime),ForceMode.Acceleration);
        }
    }
}

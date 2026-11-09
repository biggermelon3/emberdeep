using UnityEngine;
namespace PhysicalAssembly
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class AssemblyBody : MonoBehaviour
    {
        public string Id;
        public bool Anchored;
        public bool Attachable=true;
        public AssemblyPart Part;
        public Rigidbody Rigidbody { get; private set; }
        public Collider[] Colliders { get; private set; }
        public void Initialize()
        {
            Rigidbody = GetComponent<Rigidbody>(); Colliders = GetComponentsInChildren<Collider>();
            Rigidbody.solverIterations = 12; Rigidbody.solverVelocityIterations = 4;
            Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            Rigidbody.maxAngularVelocity = 120; Rigidbody.isKinematic = Anchored;
        }
    }
}

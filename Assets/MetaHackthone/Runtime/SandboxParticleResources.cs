using UnityEngine;
namespace PhysicalAssembly
{
    // Pooled systems survive individual effects, but own their generated material until scene unload.
    public sealed class SandboxParticleResources : MonoBehaviour
    {
        public Material Material;
        void OnDestroy() { if(Material) Destroy(Material); }
    }
}

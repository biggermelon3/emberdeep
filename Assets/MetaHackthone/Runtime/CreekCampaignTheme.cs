using UnityEngine;
namespace PhysicalAssembly
{
    // Baked visual-only scene data; no runtime rebuilding or physics registration.
    public sealed class CreekCampaignTheme : MonoBehaviour
    {
        public GameObject Plank, Boulder, Pine, Terrace, Support;
        public Material Water;
        public Transform Visuals;
        public Renderer[] ReplacedRenderers;
    }
}

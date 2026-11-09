using System.Collections;
using UnityEngine;
using Unity.XR.OpenXR.Features.PICOSupport;

namespace PhysicalAssembly.MetaXR
{
    // Present only on the Pico build's portable player prefab.
    public sealed class PicoPassthrough : MonoBehaviour
    {
        IEnumerator Start()
        {
            if(Application.isEditor) yield break;
            for(int attempt=0;attempt<50;attempt++)
            {
                if(PassthroughFeature.EnableSeeThroughManual(true))
                {
                    Debug.Log("METAHACKTHONE_PICO_PASSTHROUGH_READY");
                    yield break;
                }
                yield return new WaitForSeconds(.1f);
            }
            Debug.LogWarning("Pico passthrough unavailable; XR rendering continues.");
        }
    }
}

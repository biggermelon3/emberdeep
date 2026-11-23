using System;
using UnityEngine;

namespace PhysicalAssembly.MetaXR
{
    // Meta's MRUK package creates a DontDestroyOnLoad MRUKGlobalContext before the first scene, and its Update asks
    // OVRPlugin every frame. PICO has no OVRPlugin library, so a PICO build threw DllNotFoundException every frame
    // (13,374 in a 2.5 minute HandFleet session on 2026-11-21). Where OVRPlugin cannot load, switch that updater off.
    // Disable, not destroy: MRUKGlobalContext.OnDestroy re-creates itself unless the app is quitting.
    static class MetaOnlyRuntimeGuard
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void DisableMetaOnlyUpdaters()
        {
            if(Application.isEditor || OVRPluginLoads()) return;
            var type=Type.GetType("Meta.XR.MRUtilityKit.MRUKGlobalContext, meta.xr.mrutilitykit");
            if(type==null) return;
            foreach(var found in UnityEngine.Object.FindObjectsByType(type,FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(found is Behaviour behaviour) { behaviour.enabled=false; Debug.Log("METAHACKTHONE_MRUK_OFF no OVRPlugin on this runtime"); }
        }
        static bool OVRPluginLoads()
        {
            // Any OVRPlugin call loads the library; the value itself does not matter here.
            try { bool initialized=OVRPlugin.initialized; return true; }
            catch(DllNotFoundException) { return false; }
            catch(EntryPointNotFoundException) { return false; }
        }
    }
}

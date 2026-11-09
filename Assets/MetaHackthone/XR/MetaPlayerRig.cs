using UnityEngine;
using XRSettings = UnityEngine.XR.XRSettings;

namespace PhysicalAssembly.MetaXR
{
    // Shared player setup; gameplay adapters must not own camera tracking.
    public sealed class MetaPlayerRig : MonoBehaviour
    {
        public static MetaPlayerRig Shared { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetShared() => Shared=null;
        public Transform XRRig;
        public Camera DesktopCamera;
        public bool IsXR => !Application.isEditor && (XRSettings.isDeviceActive || Application.platform == RuntimePlatform.Android);
        public Camera ViewCamera => !IsXR ? DesktopCamera : XRRig.GetComponent<OVRCameraRig>() is OVRCameraRig ovr ? ovr.centerEyeAnchor.GetComponent<Camera>() : XRRig.GetComponentInChildren<Camera>(true);
        public float FloorHeight => XRRig.GetComponent<OVRCameraRig>() is OVRCameraRig ovr ? ovr.trackingSpace.position.y : XRRig.position.y;
        public void Initialize(Transform rig, Camera desktop)
        {
            Shared=this;
            XRRig = rig; DesktopCamera = desktop;
            if (!desktop.GetComponent<AudioListener>()) desktop.gameObject.AddComponent<AudioListener>();
            MetaHandInputAdapter.ConfigureRoomScaleRig(rig);
            var manager = rig.GetComponent<OVRManager>();
            if(manager)
            {
                manager.usePositionTracking = true; manager.useRotationTracking = true;
                manager.AllowRecenter = false; manager.trackingOriginType = OVRManager.TrackingOrigin.Stage;
            }
            desktop.gameObject.SetActive(!IsXR); rig.gameObject.SetActive(IsXR);
        }
        void OnDestroy() { if(Shared==this) Shared=null; }
    }
}

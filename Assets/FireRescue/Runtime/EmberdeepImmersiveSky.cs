using UnityEngine;
using UnityEngine.Rendering;

namespace FireRescue
{
    // Scene-owned, camera-centred environment. Colour AND alpha rise from the
    // nadir to the zenith, covering passthrough while leaving scene geometry
    // and tracked hands in front. No collider, camera rotation or input owner.
    public sealed class EmberdeepImmersiveSky : MonoBehaviour
    {
        public const float RevealSeconds=4f;
        public float Progress { get; private set; }
        public Transform Backdrop=>backdrop;
        Camera view;
        Transform backdrop;
        Material material;
        CameraClearFlags savedClear;
        Color savedBackground;
        bool configured;

        public void Initialize(Camera camera)
        {
            if(configured || !camera)return;
            var shader=Resources.Load<Shader>("EmberdeepImmersiveSky");
            if(!shader || !shader.isSupported){Debug.LogError("Emberdeep immersive sky shader unavailable");return;}
            view=camera;savedClear=view.clearFlags;savedBackground=view.backgroundColor;
            material=new Material(shader){name="Emberdeep rising forest sky"};
            var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name="Emberdeep surrounding forest sky";sphere.layer=gameObject.layer;
            backdrop=sphere.transform;backdrop.SetParent(transform,false);
            var collider=sphere.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            var renderer=sphere.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            configured=true;Tick(0,false);
        }
        // The scene calls this after placement, independently of UI ownership.
        // Pause/focus loss freezes progress, and recentering never restarts it.
        public void Tick(float dt,bool ready)
        {
            if(!configured || !view || !backdrop)return;
            view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=Color.clear;
            backdrop.SetPositionAndRotation(view.transform.position,Quaternion.identity);
            float radius=Mathf.Min(60,view.farClipPlane*.45f);
            backdrop.localScale=Vector3.one*(radius*2);
            if(ready && float.IsFinite(dt) && dt>0)Progress=Mathf.Clamp01(Progress+Mathf.Min(dt,.1f)/RevealSeconds);
            material.SetFloat("_Reveal",Progress);
        }
        void OnDestroy()
        {
            if(configured && view && view.clearFlags==CameraClearFlags.SolidColor && view.backgroundColor==Color.clear)
            {view.clearFlags=savedClear;view.backgroundColor=savedBackground;}
            if(backdrop)Destroy(backdrop.gameObject);
            if(material)Destroy(material);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace PhysicalAssembly
{
    // Visual water has no solid/trigger collider; only the runner's own parts are sampled.
    public sealed class SandboxWater : MonoBehaviour
    {
        public const float SurfaceY = -.18f;
        public bool Contacted { get; private set; }
        GravityGarage25DCourse course;
        Material material, splashMaterial;
        GameObject surface, splash;
        float splashAge;

        public static bool InFootprint(Vector3 p) => p.x >= -.5f && p.x <= 18.5f && p.z >= -4 && p.z <= 4;
        public void Initialize(GravityGarage25DCourse owner)
        {
            course = owner;
            surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
            surface.name = "Sandbox water";
            DestroyImmediate(surface.GetComponent<Collider>());
            surface.transform.SetParent(transform, false);
            surface.transform.position = course.Point(9, SurfaceY, 0);
            surface.transform.localRotation = Quaternion.Euler(90, 0, 0);
            surface.transform.localScale = new Vector3(19, 8, 1) * course.Scale;
            var shader = Resources.Load<Shader>("SandboxWater");
            if (!shader) throw new System.InvalidOperationException("Sandbox water shader is missing.");
            material = new Material(shader);
            surface.GetComponent<Renderer>().sharedMaterial = material;
            surface.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        // Segment clipping against the bounded volume below the surface catches fast crossings.
        public static bool CrossesWater(Vector3 a, Vector3 b, out Vector3 hit)
        {
            bool crossed=SandboxWaterContactHistory.Crosses(a,b,new Bounds(new Vector3(9,(SurfaceY-10000)*.5f,0),new Vector3(19,10000+SurfaceY,8)),out hit);
            hit.y=SurfaceY;return crossed;
        }
        readonly SandboxWaterContactHistory contacts=new();
        public bool CheckContact(IEnumerable<Collider> colliders)
        {
            if(Contacted)return true;
            if(!contacts.Check(colliders,course.transform.up,course.Coordinates,new Bounds(new Vector3(9,(SurfaceY-10000)*.5f,0),new Vector3(19,10000+SurfaceY,8)),out var hit))return false;
            Contacted=true;ShowSplash(course.Point(hit.x,SurfaceY,hit.z));return true;
        }
        void ShowSplash(Vector3 point)
        {
            splash = new GameObject("Water splash"); splash.transform.SetParent(transform, false);
            splash.transform.position = point; splashAge = 0;
            splashMaterial = PartFactory.Material(new Color(.65f, .95f, 1));
            for (int i = 0; i < 12; i++)
            {
                var drop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                DestroyImmediate(drop.GetComponent<Collider>());
                drop.transform.SetParent(splash.transform, false);
                drop.transform.localScale = Vector3.one * .08f * course.Scale;
                drop.GetComponent<Renderer>().sharedMaterial = splashMaterial;
            }
        }
        void Update()
        {
            if (!splash) return;
            splashAge += Time.deltaTime;
            for (int i = 0; i < splash.transform.childCount; i++)
            {
                float angle = i * Mathf.PI * 2 / 12;
                splash.transform.GetChild(i).localPosition = new Vector3(Mathf.Cos(angle) * splashAge,
                    Mathf.Max(0, 1.7f * splashAge - 2.5f * splashAge * splashAge), Mathf.Sin(angle) * splashAge) * course.Scale;
            }
            if (splashAge > .65f) ClearSplash();
        }
        void Dispose(Object item) { if (!item) return; if (Application.isPlaying) Destroy(item); else DestroyImmediate(item); }
        void ClearSplash() { if (splash) splash.SetActive(false); Dispose(splash); Dispose(splashMaterial); splash = null; splashMaterial = null; }
        public void ResetContact() { Contacted = false; contacts.Reset(); ClearSplash(); }
        void OnDestroy() { ClearSplash(); Dispose(surface); Dispose(material); }
    }
}

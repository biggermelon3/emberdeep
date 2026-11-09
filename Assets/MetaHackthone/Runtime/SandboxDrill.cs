using UnityEngine;

namespace PhysicalAssembly
{
    public sealed class SandboxDrill : MonoBehaviour
    {
        Transform bit;
        Vector3 previous;
        bool sampled;

        // Dimensions recovered from the user's authored sandbox cone preview.
        public static Vector3 BaseCenter(float u, float scale) => Vector3.forward * (.3f * u * 2.79507f - .12865f) * scale;
        public static Vector3 Tip(float u, float scale) => Vector3.forward * (u * 2.79507f - .12865f) * scale;

        public static void Create(AssemblyBody body, float u, float scale)
        {
            float s = u * scale;
            var go = new GameObject("Silver spiral bit");
            go.transform.SetParent(body.transform, false);
            var vertices = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();
            const int rings = 24, sides = 24;
            for (int j = 0; j <= rings; j++) for (int i = 0; i < sides; i++)
            {
                float t = j / (float)rings, a = i * Mathf.PI * 2 / sides;
                float radius = (1 - t) * s * (.32f + .08f * Mathf.Cos(a * 2 - t * Mathf.PI * 12));
                vertices.Add(new Vector3(Mathf.Cos(a) * radius * 1.32886f, Mathf.Sin(a) * radius * 1.60279f,
                    ((.3f + .7f * t) * u * 2.79507f - .12865f) * scale));
            }
            for (int j = 0; j < rings; j++) for (int i = 0; i < sides; i++)
            {
                int a = j * sides + i, b = j * sides + (i + 1) % sides;
                triangles.AddRange(new[] { a, b, a + sides, b, b + sides, a + sides });
            }
            int center = vertices.Count;
            vertices.Add(BaseCenter(u, scale));
            for (int i = 0; i < sides; i++) triangles.AddRange(new[] { center, (i + 1) % sides, i });
            var mesh = new Mesh { name = "Sandbox drill mesh" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = PartFactory.Material(new Color(.78f, .84f, .88f));
            var collision = new GameObject("Bit collision");
            collision.transform.SetParent(body.transform, false);
            var collider = collision.AddComponent<BoxCollider>();
            collider.size = Vector3.one * s;
            collider.center = BaseCenter(u, scale) + Vector3.forward * s * .5f;
            body.gameObject.AddComponent<SandboxDrill>().bit = go.transform;
        }

        public void ResetSample(){sampled=false;}

        void FixedUpdate()
        {
            var body = GetComponent<AssemblyBody>();
            ISandboxDrillTarget target=null;
            foreach(var component in GetComponentsInParent<MonoBehaviour>())if(component is ISandboxDrillTarget found){target=found;break;}
            if(target==null || !target.DrillActive(body.Part) || !bit){sampled=false;return;}
            bit.Rotate(Vector3.forward,720*Time.fixedDeltaTime,Space.Self);
            var tip=transform.TransformPoint(Tip(BuildingGeometry.Unit,body.Part.GeometryScale));
            if(sampled)target.SweepDrill(body.Part,previous,tip,BuildingGeometry.Unit*body.Part.GeometryScale*.1f);
            previous=tip;sampled=true;
        }
    }

    public sealed class SandboxDrillBurst : MonoBehaviour
    {
        GameObject root;
        Material material;
        float age;
        public void Begin(Vector3 point, float scale)
        {
            root = new GameObject("PONG sandstone fragments");
            root.transform.SetParent(transform, true); root.transform.position = point;
            material = PartFactory.Material(new Color(.85f, .65f, .35f));
            for (int i = 0; i < 12; i++)
            {
                var fragment = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(fragment.GetComponent<Collider>());
                fragment.transform.SetParent(root.transform, false);
                fragment.transform.localPosition = Random.insideUnitSphere * .3f * scale;
                fragment.transform.localScale = Vector3.one * .07f * scale;
                fragment.GetComponent<Renderer>().sharedMaterial = material;
            }
            var label = new GameObject("PONG!"); label.transform.SetParent(root.transform, false);
            var text = label.AddComponent<TextMesh>(); text.text = "PONG!"; text.characterSize = .22f * scale;
            text.fontSize = 48; text.anchor = TextAnchor.MiddleCenter;
            if (Camera.main) label.transform.rotation = Camera.main.transform.rotation;
        }
        void Update()
        {
            age += Time.deltaTime;
            if (root) foreach (Transform p in root.transform) p.localPosition += p.localPosition * Time.deltaTime * 3;
            if (age > .65f) Destroy(this);
        }
        void OnDestroy() { if (root) Destroy(root); if (material) Destroy(material); }
    }
}

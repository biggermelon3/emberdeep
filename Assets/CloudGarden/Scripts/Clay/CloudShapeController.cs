using UnityEngine;

namespace CloudGarden
{
    public sealed class CloudShapeController : MonoBehaviour
    {
        public CloudResourceState resources = new CloudResourceState();
        public Vector3 shape = Vector3.one;
        public bool raining;
        public float deformation;
        public MeshRenderer body;
        Mesh mesh;
        Vector3[] original, vertices;
        [HideInInspector] public Vector3 dentPoint;
        [HideInInspector] public float dentDepth;
        MaterialPropertyBlock properties;
        public float Radius => .19f * shape.x;
        public bool Selected { get; set; }

        public void Initialize(Material material)
        {
            var go = new GameObject("Soft cloud mesh", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            body = go.GetComponent<MeshRenderer>();
            body.sharedMaterial = material;
            body.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mesh = MakeMesh();
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            original = mesh.vertices;
            vertices = new Vector3[original.Length];
            var hit = gameObject.AddComponent<SphereCollider>();
            hit.radius = .23f;
            properties = new MaterialPropertyBlock();
        }

        static Mesh MakeMesh()
        {
            const int rings = 16, slices = 28;
            var v = new Vector3[(rings + 1) * (slices + 1)];
            var t = new int[rings * slices * 6];
            for (int y = 0; y <= rings; y++)
                for (int x = 0; x <= slices; x++)
                {
                    float a = x * Mathf.PI * 2 / slices, b = y * Mathf.PI / rings;
                    var n = new Vector3(Mathf.Sin(b) * Mathf.Cos(a), Mathf.Cos(b), Mathf.Sin(b) * Mathf.Sin(a));
                    float puff = 1 + .14f * Mathf.Sin(n.x * 6 + n.z * 3) * Mathf.Sin(n.y * 5 + n.z * 4);
                    v[y * (slices + 1) + x] = Vector3.Scale(n, new Vector3(.23f, .13f, .17f)) * puff;
                }
            int k = 0;
            for (int y = 0; y < rings; y++) for (int x = 0; x < slices; x++)
            {
                int a = y * (slices + 1) + x, b = a + slices + 1;
                t[k++] = a; t[k++] = a + 1; t[k++] = b;
                t[k++] = a + 1; t[k++] = b + 1; t[k++] = b;
            }
            var m = new Mesh { name = "Deformable cloud (475 vertices)" };
            m.vertices = v; m.triangles = t; m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        public void Sculpt(float width, float height)
        {
            var previous = shape;
            shape.x = Mathf.Clamp(width, .6f, 2.45f);
            shape.y = Mathf.Clamp(height, .45f, 1.7f);
            shape.z = Mathf.Clamp(1 / Mathf.Sqrt(shape.x * shape.y), .65f, 1.6f);
            deformation += Vector3.Distance(previous, shape);
            resources.stability = Mathf.Max(.35f, resources.stability - Vector3.Distance(previous, shape) * .1f);
        }

        public void Indent(Vector3 worldPoint, float depth)
        {
            dentPoint = transform.InverseTransformPoint(worldPoint);
            dentDepth = Mathf.Clamp(depth, 0, .055f);
            deformation += dentDepth;
        }

        void Update()
        {
            if (mesh == null) return;
            float breathe = 1 + Mathf.Sin(Time.time * 1.8f + GetInstanceID()) * .012f;
            for (int i = 0; i < original.Length; i++)
            {
                Vector3 v = Vector3.Scale(original[i], shape);
                float influence = Mathf.Clamp01(1 - Vector3.Distance(v, dentPoint) / .18f);
                vertices[i] = (v - v.normalized * dentDepth * influence * influence) * breathe;
            }
            mesh.vertices = vertices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            GetComponent<SphereCollider>().radius = .23f * Mathf.Max(shape.x, shape.z);
            var color = Color.Lerp(new Color(.94f, .96f, 1), new Color(.61f, .81f, .91f), resources.moisture);
            if (raining) color = Color.Lerp(color, new Color(.38f, .57f, .72f), .35f);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_EmissionColor", Selected ? new Color(.08f, .12f, .15f) : Color.black);
            body.SetPropertyBlock(properties);
        }

        void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}

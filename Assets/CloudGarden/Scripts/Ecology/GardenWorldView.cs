using System.Collections.Generic;
using UnityEngine;

namespace CloudGarden
{
    public sealed class GardenWorldView : MonoBehaviour
    {
        readonly List<Material> materials = new List<Material>();
        readonly Transform[] sprouts = new Transform[2];
        readonly Renderer[] beds = new Renderer[2];
        MaterialPropertyBlock properties;
        readonly List<Transform> petals = new List<Transform>();
        readonly Transform[] mist = new Transform[12];
        Transform water, wheel, beacon;
        Renderer pipe;
        Material soil, leaf, stone, brass, cream, pink, blue;
        CloudGardenGame game;
        public Material CloudMaterial { get; private set; }
        public Material GlowMaterial { get; private set; }
        public Material RainMaterial { get; private set; }

        Material Mat(string name, Color color, bool glow = false)
        {
            var m = new Material(game.settings.surfaceShader) { name = name, enableInstancing = true };
            m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", .2f);
            if (glow) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * .3f); }
            materials.Add(m); return m;
        }

        public void Build(CloudGardenGame owner)
        {
            game = owner;
            properties = new MaterialPropertyBlock();
            soil = Mat("Warm terracotta", new Color(.57f, .37f, .30f));
            leaf = Mat("Sage green", new Color(.36f, .60f, .45f));
            stone = Mat("Lavender stone", new Color(.50f, .52f, .65f));
            brass = Mat("Honey brass", new Color(.82f, .64f, .31f));
            cream = Mat("Porcelain", new Color(.94f, .88f, .72f));
            pink = Mat("Coral petals", new Color(.97f, .61f, .56f));
            blue = Mat("Lagoon water", new Color(.26f, .65f, .70f));
            CloudMaterial = Mat("Cloud cotton", Color.white, true);
            GlowMaterial = new Material(game.settings.glowShader);
            GlowMaterial.color = new Color(1, .85f, .49f); materials.Add(GlowMaterial);
            RainMaterial = new Material(game.settings.particleShader);
            RainMaterial.SetColor("_BaseColor", new Color(.48f, .82f, .96f)); materials.Add(RainMaterial);

            // Small floating terrarium, all dimensions in meters.
            var island = Primitive("Floating stone island", PrimitiveType.Cylinder, new Vector3(0, -.085f, 0), new Vector3(1.48f, .075f, 1.04f), stone);
            island.transform.rotation = Quaternion.Euler(0, 13, 0);
            Primitive("Porcelain rim", PrimitiveType.Cylinder, new Vector3(0, -.011f, 0), new Vector3(1.43f, .019f, 1.0f), cream);
            Primitive("Moss blanket", PrimitiveType.Cylinder, new Vector3(0, .006f, 0), new Vector3(1.36f, .013f, .93f), leaf);
            Primitive("Underside heart", PrimitiveType.Sphere, new Vector3(0, -.19f, 0), new Vector3(.83f, .3f, .65f), stone);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2 / 12;
                Primitive("Rim stone", PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * .62f, .02f, Mathf.Sin(a) * .40f), new Vector3(.09f, .06f, .075f), i % 3 == 0 ? brass : cream);
            }
            for (int i = 0; i < 9; i++)
                Primitive("Stepping stone", PrimitiveType.Cylinder, new Vector3((i - 4) * .092f, .032f, -.32f + .02f * Mathf.Sin(i)), new Vector3(.067f, .008f, .045f), cream);

            for (int i = 0; i < 2; i++)
            {
                Vector3 at = new Vector3(GardenModel.Beds[i].x, .033f, GardenModel.Beds[i].y);
                Primitive("Garden border", PrimitiveType.Cylinder, at, new Vector3(.35f, .019f, .30f), cream);
                beds[i] = Primitive("Dry earth", PrimitiveType.Cylinder, at + Vector3.up * .026f, new Vector3(.31f, .013f, .26f), soil).GetComponent<Renderer>();
                var sprout = new GameObject(i == 0 ? "First garden" : "Far garden"); sprout.transform.SetParent(transform, false);
                sprout.transform.localPosition = at + Vector3.up * .045f; sprouts[i] = sprout.transform;
                for (int j = 0; j < 3; j++)
                {
                    Vector3 p = new Vector3((j - 1) * .075f, .11f + j * .018f, Mathf.Sin(j * 4) * .04f);
                    ChildPrimitive(sprout.transform, "Stem", PrimitiveType.Cylinder, new Vector3(p.x, p.y * .45f, p.z), new Vector3(.013f, p.y * .5f, .013f), leaf);
                    for (int k = 0; k < 2; k++)
                    {
                        var l = ChildPrimitive(sprout.transform, "Leaf", PrimitiveType.Sphere, new Vector3(p.x + (k == 0 ? -.026f : .026f), p.y * .55f, p.z), new Vector3(.065f, .016f, .025f), leaf);
                        l.transform.localRotation = Quaternion.Euler(0, k * 40, k == 0 ? -25 : 25);
                    }
                    for (int k = 0; k < 5; k++)
                    {
                        float a = k * Mathf.PI * 2 / 5;
                        var petal = ChildPrimitive(sprout.transform, "Flower petal", PrimitiveType.Sphere, p + new Vector3(Mathf.Cos(a) * .027f, 0, Mathf.Sin(a) * .027f), new Vector3(.049f, .019f, .039f), i == 0 ? pink : cream);
                        petals.Add(petal.transform);
                    }
                    ChildPrimitive(sprout.transform, "Golden center", PrimitiveType.Sphere, p + Vector3.up * .009f, Vector3.one * .032f, brass);
                }
            }

            Vector3 pondAt = new Vector3(0, .04f, .18f);
            Primitive("Pond basin", PrimitiveType.Cylinder, pondAt, new Vector3(.43f, .035f, .34f), cream);
            Primitive("Pond inner stone", PrimitiveType.Cylinder, pondAt + Vector3.up * .037f, new Vector3(.38f, .003f, .29f), stone);
            water = Primitive("Water level", PrimitiveType.Cylinder, pondAt + Vector3.up * .043f, new Vector3(.36f, .004f, .27f), blue).transform;
            var lily = Primitive("Lily pad", PrimitiveType.Cylinder, pondAt + new Vector3(-.085f, .062f, .035f), new Vector3(.065f, .004f, .055f), leaf);
            lily.transform.localRotation = Quaternion.Euler(0, 30, 7);

            Vector3 tower = new Vector3(.40f, .06f, .25f);
            Primitive("Power tower footing", PrimitiveType.Cylinder, tower, new Vector3(.16f, .025f, .16f), stone);
            Primitive("Power tower", PrimitiveType.Cylinder, tower + Vector3.up * .12f, new Vector3(.055f, .12f, .055f), brass);
            for (int i = 0; i < 3; i++) Primitive("Power coil", PrimitiveType.Cylinder, tower + Vector3.up * (.10f + i * .055f), new Vector3(.11f, .008f, .11f), cream);
            beacon = Primitive("Energy globe", PrimitiveType.Sphere, tower + Vector3.up * .27f, Vector3.one * .09f, brass).transform;
            pipe = Primitive("Irrigation conduit", PrimitiveType.Cube, new Vector3(.20f, .055f, .24f), new Vector3(.32f, .025f, .025f), brass).GetComponent<Renderer>();
            wheel = Primitive("Pump wheel", PrimitiveType.Cylinder, new Vector3(.23f, .12f, .24f), new Vector3(.11f, .012f, .11f), stone).transform;
            wheel.localRotation = Quaternion.Euler(90, 0, 0);
            for (int i = 0; i < 4; i++)
            {
                var paddle = ChildPrimitive(wheel, "Wheel spoke", PrimitiveType.Cube, Vector3.zero, new Vector3(.7f, 1.15f, .12f), brass);
                paddle.transform.localRotation = Quaternion.Euler(0, i * 45, 0);
            }

            // The permanent spring is the recoverable resource source.
            Vector3 spring = new Vector3(-.42f, .09f, .25f);
            for (int i = 0; i < 3; i++)
            {
                var crystal = Primitive("Mist spring crystal", PrimitiveType.Cube, spring + new Vector3((i - 1) * .043f, i == 1 ? .055f : 0, 0), new Vector3(.04f, .14f, .04f), blue);
                crystal.transform.localRotation = Quaternion.Euler(0, 45, (i - 1) * -18);
            }
            for (int i = 0; i < mist.Length; i++)
                mist[i] = Primitive("Rising water mote", PrimitiveType.Sphere, spring, Vector3.one * .013f, cream).transform;
            // Distant floating islets create depth without covering the playable area.
            for (int i = 0; i < 5; i++)
            {
                Vector3 p = new Vector3((i - 2) * 1.6f, -.1f + Mathf.Sin(i * 4) * .25f, 2.2f + i % 2);
                ChildPrimitive(game.VirtualEnvironment.transform, "Distant islet", PrimitiveType.Sphere, p, new Vector3(.65f, .17f, .45f), stone);
                ChildPrimitive(game.VirtualEnvironment.transform, "Distant canopy", PrimitiveType.Sphere, p + Vector3.up * .1f, new Vector3(.55f, .15f, .36f), leaf);
            }
        }

        public GameObject Primitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
            => ChildPrimitive(transform, name, type, position, scale, material);

        public static GameObject ChildPrimitive(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name;
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            var collider = go.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            return go;
        }

        public void Refresh(GardenModel model, float dt)
        {
            for (int i = 0; i < 2; i++)
            {
                var p = model.plants[i];
                sprouts[i].localScale = Vector3.one * Mathf.Lerp(.18f, 1, p.growth);
                sprouts[i].localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 1.3f + i) * (p.startled > 0 ? 12 : 2));
                properties.SetColor("_BaseColor", Color.Lerp(new Color(.55f, .43f, .38f), new Color(.28f, .48f, .35f), p.water));
                beds[i].SetPropertyBlock(properties);
            }
            water.localScale = new Vector3(.36f, .002f + model.pond * .022f, .27f);
            water.gameObject.SetActive(model.pond > .01f);
            beacon.localScale = Vector3.one * (.08f + model.energy * .045f);
            properties.SetColor("_BaseColor", Color.Lerp(new Color(.37f, .35f, .3f), new Color(1, .81f, .3f), model.energy));
            pipe.SetPropertyBlock(properties);
            if (model.energy > 0 && model.pond > 0) wheel.Rotate(0, dt * 160, 0, Space.Self);
            for (int i = 0; i < mist.Length; i++)
            {
                float phase = Mathf.Repeat(Time.time * .16f + i / (float)mist.Length, 1);
                Vector2 source = i < 4 ? new Vector2(-.42f, .25f) : GardenModel.Beds[(i / 4 - 1) % 2];
                mist[i].localPosition = new Vector3(source.x + Mathf.Sin(i * 2 + phase * 6) * .045f, .12f + phase * .4f, source.y);
                mist[i].localScale = Vector3.one * Mathf.Sin(phase * Mathf.PI) * .015f;
                mist[i].gameObject.SetActive(i < 4 || model.plants[(i / 4 - 1) % 2].growth > .3f);
            }
        }
        void OnDestroy() { foreach (var m in materials) if (m != null) Destroy(m); }
    }
}

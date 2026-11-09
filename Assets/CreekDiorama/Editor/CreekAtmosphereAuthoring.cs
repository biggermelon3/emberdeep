using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CreekDiorama.Editor
{
    /// <summary>Reusable, collider-free art effects. Dimensions come from the object transform.</summary>
    public static class CreekAtmosphereAuthoring
    {
        const string Root = "Assets/CreekDiorama";
        const string ScenePath = Root + "/Scenes/BeautifulCorner.unity";

        [MenuItem("MetaHackthone/Creek Art/Add clouds and assembly bounds")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            var root = scene.GetRootGameObjects().First(g => g.name.StartsWith("Beautiful Corner"));
            if (root.transform.Find("Atmosphere and assembly area")) throw new InvalidOperationException("Effects already exist; edit their transforms or materials instead of rebuilding.");
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            var clouds = Material("Cloud billboard", "Creek Diorama/Cloud Billboard");
            var bounds = Material("Assembly hologram", "Creek Diorama/Assembly Bounds");
            Directory.CreateDirectory(Root + "/Meshes/Effects");
            AssetDatabase.Refresh();

            var group = new GameObject("Atmosphere and assembly area");
            group.transform.SetParent(root.transform, false);
            Undo.RegisterCreatedObjectUndo(group, "Add clouds and assembly area");

            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Assembly Bounds - scale XYZ to resize";
            Object.DestroyImmediate(box.GetComponent<Collider>());
            SetupRenderer(box.GetComponent<MeshRenderer>(), bounds);
            // Save a neutral reusable prefab; scene instance supplies the level dimensions.
            PrefabUtility.SaveAsPrefabAsset(box, Root + "/Prefabs/AssemblyBounds.prefab");
            Object.DestroyImmediate(box);
            box = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/AssemblyBounds.prefab"), scene);
            box.transform.SetParent(group.transform, false);
            box.transform.localPosition = new Vector3(0, .37f, 1.46f);
            box.transform.localScale = new Vector3(2.45f, .54f, 1.02f);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var mesh = Object.Instantiate(quad.GetComponent<MeshFilter>().sharedMesh);
            mesh.name = "Cloud card with billboard-safe bounds";
            // The vertex shader rotates cards. Expand CPU bounds to prevent premature culling.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            string meshPath = Root + "/Meshes/Effects/CloudCard.asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (old) { EditorUtility.CopySerialized(mesh, old); Object.DestroyImmediate(mesh); mesh = old; }
            else AssetDatabase.CreateAsset(mesh, meshPath);
            quad.GetComponent<MeshFilter>().sharedMesh = mesh;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.name = "Cloud Billboard";
            SetupRenderer(quad.GetComponent<MeshRenderer>(), clouds);
            PrefabUtility.SaveAsPrefabAsset(quad, Root + "/Prefabs/CloudBillboard.prefab");
            Object.DestroyImmediate(quad);

            var bank = new GameObject("Distant Clouds - six billboard cards");
            bank.transform.SetParent(group.transform, false);
            Vector3[] positions = {
                new Vector3(3.111f,-.681f,-2.330f), new Vector3(2.194f,.173f,-2.986f),
                new Vector3(.381f,.063f,-3.537f), new Vector3(-.979f,-.681f,-3.676f),
                new Vector3(3.047f,-1.865f,-1.859f), new Vector3(-1.131f,-1.920f,-3.212f)
            };
            Vector2[] sizes = { new Vector2(1.8f,.85f),new Vector2(1.65f,.8f),new Vector2(2f,.9f),new Vector2(1.8f,.8f),new Vector2(2.2f,1f),new Vector2(2.2f,1f) };
            for (int i = 0; i < positions.Length; i++)
            {
                var card = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/CloudBillboard.prefab"), scene);
                card.name = "Cloud " + (i+1).ToString("00");
                card.transform.SetParent(bank.transform, false);
                card.transform.localPosition = positions[i];
                card.transform.localScale = new Vector3(sizes[i].x, sizes[i].y, 1);
            }
            PrefabUtility.SaveAsPrefabAsset(bank, Root + "/Prefabs/DistantClouds.prefab");
            var camera = scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).First();
            Undo.RecordObject(camera, "Frame atmosphere and assembly area");
            Undo.RecordObject(camera.transform, "Frame atmosphere and assembly area");
            camera.transform.position = new Vector3(-1.3f, 2.3f, 4.6f);
            camera.transform.LookAt(new Vector3(0, .66f, .65f));
            camera.orthographicSize = 1.48f;
            camera.backgroundColor = new Color(.25f,.46f,.66f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            var ground = scene.GetRootGameObjects().FirstOrDefault(g=>g.name.StartsWith("Presentation ground"));
            if (ground) { Undo.RecordObject(ground,"Show floating atmosphere"); ground.SetActive(false); }
            PrefabUtility.SaveAsPrefabAsset(root, Root + "/Prefabs/BeautifulCorner.prefab");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = box;
            if (SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.LookAt(new Vector3(0,.65f,.35f),camera.transform.rotation,3.6f);
            Debug.Log("CREEK_ATMOSPHERE_READY: six cloud cards; one scalable bounds mesh; no colliders or runtime scripts.");
        }

        static Material Material(string name, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (!shader || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Invalid shader: " + shaderName);
            var path = Root + "/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = shader;
            mat.enableInstancing = true;
            return mat;
        }

        static void SetupRenderer(MeshRenderer renderer, Material mat)
        {
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        public static string Capture(string filename)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
            var camera = scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).First();
            var priorTarget = camera.targetTexture;
            var priorActive = RenderTexture.active;
            var target = new RenderTexture(1600, 1100, 24, RenderTextureFormat.ARGBHalf);
            var image = new Texture2D(1600, 1100, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0,0,1600,1100),0,0);
                // HDR targets store linear values; PNG viewers expect display (sRGB) values.
                if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                {
                    var pixels = image.GetPixels();
                    for (int j=0;j<pixels.Length;j++) pixels[j]=pixels[j].gamma;
                    image.SetPixels(pixels);
                }
                image.Apply();
                var path = "Docs/CreekDiorama/" + filename + ".png";
                File.WriteAllBytes(path,image.EncodeToPNG());
                return Path.GetFullPath(path);
            }
            finally
            {
                camera.targetTexture = priorTarget;
                RenderTexture.active = priorActive;
                Object.DestroyImmediate(image);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }
    }
}

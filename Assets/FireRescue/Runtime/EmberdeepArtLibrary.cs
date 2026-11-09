using System;
using UnityEngine;

namespace FireRescue
{
    // Separate authored Art V2 delivery. Missing entries keep the existing blockout usable.
    public sealed class EmberdeepArtLibrary : ScriptableObject
    {
        [Serializable] public struct Entry { public string Id; public GameObject Prefab; }
        public Entry[] Models = Array.Empty<Entry>();
        public Material Surface, Creek, Panel, Accent, Wood, Foliage, Ground;
        public UnityEngine.Rendering.RenderPipelineAsset Pipeline;
        public GameObject Find(string id)
        {
            foreach (var entry in Models) if (entry.Id == id) return entry.Prefab;
            return null;
        }
        public Transform Create(string id, Transform parent, Vector3 position, float scale = 1)
        {
            var prefab = Find(id); if (!prefab) return null;
            var instance = Instantiate(prefab, parent, false).transform;
            instance.localPosition = position; instance.localRotation = Quaternion.identity;
            instance.localScale = Vector3.one * scale;
            return instance;
        }
        public static EmberdeepArtLibrary Load() => Resources.Load<EmberdeepArtLibrary>("EmberdeepArtV2/Library");
    }
}

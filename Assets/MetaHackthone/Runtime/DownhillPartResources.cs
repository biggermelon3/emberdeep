using System.Linq;
using UnityEngine;
namespace PhysicalAssembly
{
    // Only the downhill factory's generated meshes/materials belong to this instance.
    public sealed class DownhillPartResources : MonoBehaviour
    {
        Object[] owned;
        void Awake() => Capture();
        public void Capture()
        {
            owned=GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh).Where(m=>m && (m.name=="Recessed wheel mesh" || m.name=="Wooden prism" || m.name=="GridMarkers" || m.name=="Sandbox drill mesh")).Cast<Object>()
                .Concat(GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials)).Concat(GetComponentsInChildren<Collider>().Select(c=>c.sharedMaterial)).Where(o=>o).Distinct().ToArray();
        }
        public void ReleaseOwnership() => owned=null;
        void OnDestroy() { if(owned!=null) foreach(var item in owned) if(item) {
#if UNITY_EDITOR
                if(UnityEditor.EditorUtility.IsPersistent(item))continue;
#endif
                if(Application.isPlaying) Destroy(item); else DestroyImmediate(item); } }
    }
}

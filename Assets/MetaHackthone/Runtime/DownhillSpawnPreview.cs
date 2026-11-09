using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PhysicalAssembly
{
    // Mesh-only pool: never register preview bodies in either assembly world.
    public sealed class DownhillSpawnPreview : MonoBehaviour
    {
        sealed class Entry
        {
            public Transform transform;
            public MeshFilter filter;
            public AssemblyBody body;
            public Vector3 position;
            public Quaternion rotation;
        }
        readonly List<Entry> pool=new();
        DownhillController controller;
        Material material;
        int topology=-1,count;
        public int RebuildCount { get; private set; }
        public int MeshCount => count;
        public Pose CorePose { get; private set; }
        public void Initialize(DownhillController owner) => controller=owner;
        public void Hide() { foreach(var entry in pool) if(entry.transform) entry.transform.gameObject.SetActive(false); }
        void LateUpdate() => Refresh();
        public void Refresh()
        {
            if(!controller || !controller.isActiveAndEnabled || !controller.IsBuilding || !controller.Core) { Hide(); return; }
            if(topology!=controller.Workshop.World.TopologyVersion) Rebuild();
            CorePose=controller.SandboxPlacement(out var delta);
            var origin=controller.Core.MainBody.Rigidbody.position;
            for(int i=0;i<count;i++)
            {
                var item=pool[i]; if(!item.body) continue;
                var body=item.body.Rigidbody;
                item.transform.gameObject.SetActive(true);
                item.transform.SetPositionAndRotation(CorePose.position+delta*(body.position+body.rotation*item.position-origin)*controller.TransferScale,delta*body.rotation*item.rotation);
            }
        }
        void Rebuild()
        {
            topology=controller.Workshop.World.TopologyVersion; RebuildCount++; count=0;
            if(!material)
            {
                material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                material.SetFloat("_Surface",1); material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite",0); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue=3000;
                material.SetColor("_BaseColor",new Color(.08f,.85f,.75f,.3f));
            }
            foreach(var part in controller.Workshop.World.GetConnectedParts(controller.Core.InstanceId)) foreach(var filter in part.VisualMeshes)
            {
                var renderer=filter.GetComponent<MeshRenderer>(); var body=filter.GetComponentInParent<AssemblyBody>();
                if(!renderer || !renderer.enabled || !body || filter.GetComponentInParent<AssemblySocket>() || filter.name=="Grid points") continue;
                if(count==pool.Count)
                {
                    var go=new GameObject("Sandbox preview mesh"); go.transform.SetParent(transform,false);
                    var entry=new Entry { transform=go.transform,filter=go.AddComponent<MeshFilter>() };
                    var visual=go.AddComponent<MeshRenderer>(); visual.sharedMaterial=material;
                    visual.shadowCastingMode=ShadowCastingMode.Off; visual.receiveShadows=false; pool.Add(entry);
                }
                var item=pool[count++]; item.body=body;
                item.position=body.transform.InverseTransformPoint(filter.transform.position);
                item.rotation=Quaternion.Inverse(body.transform.rotation)*filter.transform.rotation;
                item.filter.sharedMesh=filter.sharedMesh; item.transform.localScale=filter.transform.lossyScale*controller.TransferScale;
            }
            for(int i=count;i<pool.Count;i++) pool[i].transform.gameObject.SetActive(false);
        }
        void OnDestroy() { if(material) { if(Application.isPlaying) Destroy(material); else DestroyImmediate(material); } }
    }
}

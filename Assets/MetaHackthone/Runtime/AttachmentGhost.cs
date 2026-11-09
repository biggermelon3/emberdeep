using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace PhysicalAssembly
{
    // Pooled mesh-only preview, keyed to the held assembly rather than its candidate socket.
    public sealed class AttachmentGhost : MonoBehaviour
    {
        static readonly ProfilerMarker Marker=new("Assembly.Ghost");
        sealed class Entry
        {
            public Transform ghost; public MeshFilter filter; public MeshRenderer renderer;
            public AssemblyBody body; public Vector3 position,scale; public Quaternion rotation;
        }
        readonly List<Entry> pool=new();
        AssemblyPart held; int topology=-1,count; Material material;
        public int RebuildCount { get; private set; }
        public void Show(AttachmentCandidate candidate,AssemblyPart heldPart=null,bool blocked=false)
        {
            using(Marker.Auto())
            {
                if(!candidate.IsValid) { if(gameObject.activeSelf) gameObject.SetActive(false); return; }
                if(!heldPart) heldPart=candidate.source.Part;
                if(!gameObject.activeSelf) gameObject.SetActive(true);
                if(held!=heldPart || topology!=heldPart.World.TopologyVersion) Rebuild(heldPart);
                material.SetColor("_BaseColor",blocked?new Color(1,.15f,.1f,.35f):new Color(.15f,1,.55f,.3f));
                var source=candidate.source.Rigidbody;
                var delta=candidate.SnappedPose.rotation*Quaternion.Inverse(source.rotation);
                var sourcePosition=source.position;
                for(int i=0;i<count;i++)
                {
                    var entry=pool[i]; if(!entry.body) continue;
                    var body=entry.body.Rigidbody;
                    entry.ghost.SetPositionAndRotation(candidate.SnappedPose.position+delta*(body.position+body.rotation*entry.position-sourcePosition),delta*body.rotation*entry.rotation);
                }
            }
        }
        void Rebuild(AssemblyPart part)
        {
            held=part; topology=part.World.TopologyVersion; count=0; RebuildCount++;
            if(!material)
            {
                material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                material.SetFloat("_Surface",1); material.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite",0); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue=3000;
                material.SetColor("_BaseColor",new Color(.15f,1,.55f,.3f));
            }
            foreach(var member in part.World.GetConnectedParts(part.InstanceId)) foreach(var filter in member.VisualMeshes)
            {
                var renderer=filter.GetComponent<MeshRenderer>(); var body=filter.GetComponentInParent<AssemblyBody>();
                if(!renderer || !renderer.enabled || !body || filter.GetComponentInParent<AssemblySocket>()) continue;
                if(count==pool.Count)
                {
                    var go=new GameObject("Preview mesh"); go.transform.SetParent(transform,false);
                    var entry=new Entry { ghost=go.transform,filter=go.AddComponent<MeshFilter>(),renderer=go.AddComponent<MeshRenderer>() };
                    entry.renderer.sharedMaterial=material; entry.renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; entry.renderer.receiveShadows=false;
                    pool.Add(entry);
                }
                var item=pool[count++]; item.body=body; item.position=body.transform.InverseTransformPoint(filter.transform.position);
                item.rotation=Quaternion.Inverse(body.transform.rotation)*filter.transform.rotation; item.scale=filter.transform.lossyScale;
                item.filter.sharedMesh=filter.sharedMesh; item.ghost.localScale=item.scale; item.ghost.gameObject.SetActive(true);
            }
            for(int i=count;i<pool.Count;i++) pool[i].ghost.gameObject.SetActive(false);
        }
        void OnDestroy() { if(material) Destroy(material); }
    }
}

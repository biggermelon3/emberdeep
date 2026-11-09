using System.Collections.Generic;
using UnityEngine;

namespace PhysicalAssembly
{
    public sealed class SandboxPartHighlights : MonoBehaviour
    {
        public DownhillController Level;
        public Color Color=new(1,.72f,.02f,1);
        public float Width=.002f;
        public struct Draw { public Mesh mesh; public Renderer renderer; public Matrix4x4 matrix; public MaterialPropertyBlock properties; }
        public static readonly List<Draw> Draws=new();
        readonly Dictionary<Mesh,Mesh> meshes=new();
        readonly Dictionary<Renderer,MeshRenderer> proxies=new();
        MaterialPropertyBlock properties;
        void Awake() { properties=new MaterialPropertyBlock(); }
        void LateUpdate()
        {
            Draws.Clear();
            if(!Level || !Level.IsBuilding || !Level.Course.SandboxPolish) return;
            properties.SetColor("_Color",Color); properties.SetFloat("_Width",Width);
            foreach(var part in Level.Kit) {
                if(!part || part==Level.Core || Level.IsCoreConnected(part) || Level.IsReturning(part)) continue;
                foreach(var renderer in part.VisualRenderers) {
                    if(!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    var filter=renderer.GetComponent<MeshFilter>(); if(!filter || !filter.sharedMesh) continue;
                    if(!meshes.TryGetValue(filter.sharedMesh,out var mesh)) {
                        mesh=Instantiate(filter.sharedMesh); mesh.name="Smooth outline "+filter.sharedMesh.name;
                        if(mesh.isReadable) {
                            var vertices=mesh.vertices; var normals=mesh.normals; var sums=new Dictionary<Vector3,Vector3>();
                            for(int i=0;i<vertices.Length;i++) { sums.TryGetValue(vertices[i],out var sum); sums[vertices[i]]=sum+normals[i]; }
                            var smooth=new List<Vector3>(vertices.Length); foreach(var vertex in vertices) smooth.Add(sums[vertex].normalized);
                            mesh.SetUVs(3,smooth);
                        }
                        meshes.Add(filter.sharedMesh,mesh);
                    }
                    if(!proxies.TryGetValue(renderer,out var proxy) || !proxy) {
                        var go=new GameObject("Stencil silhouette"); go.transform.SetParent(renderer.transform,false);
                        go.AddComponent<MeshFilter>().sharedMesh=mesh; proxy=go.AddComponent<MeshRenderer>();
                        proxy.enabled=false; proxies[renderer]=proxy;
                    }
                    proxy.SetPropertyBlock(properties);
                    Draws.Add(new Draw {mesh=mesh,renderer=proxy,matrix=renderer.localToWorldMatrix,properties=properties});
                }
            }
        }
        void OnDisable() { Draws.Clear(); }
        void OnDestroy() { Draws.Clear(); foreach(var proxy in proxies.Values) if(proxy) Destroy(proxy.gameObject); foreach(var mesh in meshes.Values) if(mesh) Destroy(mesh); }
    }
}

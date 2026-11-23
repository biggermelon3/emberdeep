using System.Collections.Generic;
using UnityEngine;

namespace FireRescue
{
    // Squeezes the authored terrain to the compact board (EmberdeepBoardLayout.Z) once, when the fireground builds.
    // Flat parts (slate, soil, moss, creek, foam, path) move per vertex, so the creek stays a creek and the plinth stays
    // closed; decorations (rocks, rim growth, lanterns) move rigidly per connected piece, so nothing gets squashed.
    // Mesh names are kept: EmberdeepRimHud.MeasurePlinth finds Slate/Soil/Moss by name and the rim UI follows.
    // Needs the source meshes readable (EDV2_Terrain.fbx: Read/Write on); otherwise the terrain is left authored.
    public sealed class EmberdeepBoardFit : MonoBehaviour
    {
        static readonly string[] RigidParts={"BorderRocks","RimGrowth","Lantern"};
        readonly List<Mesh> owned=new();
        public int FittedMeshes=>owned.Count;
        public int SkippedMeshes {get;private set;}

        public static EmberdeepBoardFit Apply(Transform terrain,Transform arena)
        {
            var fit=terrain.gameObject.AddComponent<EmberdeepBoardFit>();fit.Fit(arena);return fit;
        }
        void Fit(Transform arena)
        {
            foreach(var filter in GetComponentsInChildren<MeshFilter>(true))
            {
                var source=filter.sharedMesh;if(!source)continue;
                if(!source.isReadable){SkippedMeshes++;continue;}
                var toArena=arena.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                var mesh=Instantiate(source);mesh.name=source.name;
                if(Rigid(filter.name))MovePieces(mesh,toArena);else Squeeze(mesh,toArena);
                mesh.RecalculateBounds();filter.sharedMesh=mesh;owned.Add(mesh);
            }
            if(SkippedMeshes>0)Debug.LogWarning($"EMBERDEEP_BOARD_FIT {SkippedMeshes} terrain meshes are not readable (enable Read/Write on EDV2_Terrain.fbx); they keep the authored layout.");
        }
        static bool Rigid(string name){foreach(var part in RigidParts)if(name.StartsWith(part,System.StringComparison.Ordinal))return true;return false;}

        static void Squeeze(Mesh mesh,Matrix4x4 toArena)
        {
            var fromArena=toArena.inverse;
            var vertices=mesh.vertices;var normals=mesh.normals;var tangents=mesh.tangents;
            bool hasNormals=normals.Length==vertices.Length,hasTangents=tangents.Length==vertices.Length;
            for(int i=0;i<vertices.Length;i++)
            {
                var p=toArena.MultiplyPoint3x4(vertices[i]);float k=EmberdeepBoardLayout.Slope(p.z);
                p.z=EmberdeepBoardLayout.Z(p.z);vertices[i]=fromArena.MultiplyPoint3x4(p);
                // A local z scale of k: normals by the inverse transpose, tangents by the map itself.
                if(hasNormals)
                {
                    var n=toArena.MultiplyVector(normals[i]);n.z/=k;normals[i]=fromArena.MultiplyVector(n).normalized;
                }
                if(hasTangents)
                {
                    var t=tangents[i];var d=toArena.MultiplyVector(new Vector3(t.x,t.y,t.z));d.z*=k;d=fromArena.MultiplyVector(d).normalized;
                    tangents[i]=new Vector4(d.x,d.y,d.z,t.w);
                }
            }
            mesh.vertices=vertices;if(hasNormals)mesh.normals=normals;if(hasTangents)mesh.tangents=tangents;
        }

        // Pieces are triangles joined by shared vertices or by vertices at the same position (flat-shaded rocks split
        // every face). Each piece moves by the remap of its centre.
        static void MovePieces(Mesh mesh,Matrix4x4 toArena)
        {
            var vertices=mesh.vertices;int n=vertices.Length;var parent=new int[n];for(int i=0;i<n;i++)parent[i]=i;
            int Find(int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
            void Union(int a,int b){a=Find(a);b=Find(b);if(a!=b)parent[a]=b;}
            var welded=new Dictionary<Vector3Int,int>();
            for(int i=0;i<n;i++)
            {
                var v=vertices[i];var key=new Vector3Int(Mathf.RoundToInt(v.x*1e4f),Mathf.RoundToInt(v.y*1e4f),Mathf.RoundToInt(v.z*1e4f));
                if(welded.TryGetValue(key,out int first))Union(i,first);else welded[key]=i;
            }
            var triangles=new List<int>();
            for(int s=0;s<mesh.subMeshCount;s++)
            {
                mesh.GetTriangles(triangles,s);
                for(int t=0;t+2<triangles.Count;t+=3){Union(triangles[t],triangles[t+1]);Union(triangles[t+1],triangles[t+2]);}
            }
            var sum=new Dictionary<int,Vector2>(); // x: z total, y: count
            var arena=new Vector3[n];
            for(int i=0;i<n;i++)
            {
                arena[i]=toArena.MultiplyPoint3x4(vertices[i]);int root=Find(i);
                sum.TryGetValue(root,out var acc);sum[root]=new Vector2(acc.x+arena[i].z,acc.y+1);
            }
            var fromArena=toArena.inverse;
            for(int i=0;i<n;i++)
            {
                var acc=sum[Find(i)];float centre=acc.x/acc.y;
                var p=arena[i];p.z+=EmberdeepBoardLayout.Z(centre)-centre;vertices[i]=fromArena.MultiplyPoint3x4(p);
            }
            mesh.vertices=vertices;
        }
        void OnDestroy(){foreach(var mesh in owned)if(mesh)Destroy(mesh);owned.Clear();}
    }
}

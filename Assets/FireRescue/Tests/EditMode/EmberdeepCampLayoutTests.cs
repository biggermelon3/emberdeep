using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    // Guards the camp kit against props growing into each other (the tent once swallowed the chest).
    // Surfaces are voxelized in island space; two props sharing a voxel interpenetrate or touch.
    public sealed class EmberdeepCampLayoutTests
    {
        const float Voxel=.05f; // island units: ~1.35 cm at camp scale

        static HashSet<long> Surface(Transform item,Transform island)
        {
            var set=new HashSet<long>();
            foreach(var filter in item.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;if(!mesh)continue;
                var v=mesh.vertices;var tri=mesh.triangles;var w=new Vector3[v.Length];
                for(int i=0;i<v.Length;i++)w[i]=island.InverseTransformPoint(filter.transform.TransformPoint(v[i]));
                for(int k=0;k<tri.Length;k+=3)
                {
                    var a=w[tri[k]];var b=w[tri[k+1]];var c=w[tri[k+2]];
                    float edge=Mathf.Max((a-b).magnitude,Mathf.Max((b-c).magnitude,(c-a).magnitude));
                    int n=Mathf.Max(1,Mathf.CeilToInt(edge/(Voxel*.5f)));
                    for(int i=0;i<=n;i++)for(int j=0;j<=n-i;j++)
                    {
                        var p=a+(b-a)*(i/(float)n)+(c-a)*(j/(float)n);
                        long x=Mathf.FloorToInt(p.x/Voxel)+2000,y=Mathf.FloorToInt(p.y/Voxel)+2000,z=Mathf.FloorToInt(p.z/Voxel)+2000;
                        set.Add((x<<32)|(y<<16)|z);
                    }
                }
            }
            return set;
        }

        [Test] public void CampPropsSignPostsAndBoardNeverInterpenetrate()
        {
            var art=EmberdeepArtLibrary.Load();Assert.NotNull(art);
            var root=new GameObject("Camp layout audit");
            try
            {
                var island=new GameObject("Island").transform;island.SetParent(root.transform,false);
                island.localPosition=new Vector3(0,0,EmberdeepCamp.IslandZ);island.localScale=Vector3.one*EmberdeepCamp.CampScale;
                var items=new List<(string name,Transform t)>();
                foreach(var prop in EmberdeepCamp.CampKit)
                {
                    var piece=art.Create(prop.Id,island,prop.At,prop.Scale);Assert.NotNull(piece,prop.Id);
                    piece.localRotation=Quaternion.Euler(0,prop.Yaw,0);items.Add((prop.Id+" at "+prop.At,piece));
                }
                for(int side=0;side<2;side++)
                {
                    var post=art.Create("SignPost",root.transform,new Vector3((side==0?-1:1)*EmberdeepCamp.SignPostX,0,EmberdeepCamp.SignPostZ));
                    post.localRotation=Quaternion.Euler(0,side*90,0);post.localScale=new Vector3(1,EmberdeepCamp.CampDrop+.47f,1);items.Add(("Sign post "+side,post));
                }
                var board=GameObject.CreatePrimitive(PrimitiveType.Cube);board.transform.SetParent(root.transform,false);
                board.transform.localPosition=new Vector3(0,EmberdeepCamp.CampDrop,0);board.transform.localScale=new Vector3(.90f,.82f,.025f);items.Add(("Menu board",board.transform));
                var surfaces=new List<HashSet<long>>();foreach(var item in items)surfaces.Add(Surface(item.t,island));
                var hits=new List<string>();
                for(int i=0;i<items.Count;i++)for(int j=i+1;j<items.Count;j++)
                {
                    int shared=0;foreach(var key in surfaces[i])if(surfaces[j].Contains(key))shared++;
                    if(shared>0)hits.Add($"{items[i].name} x {items[j].name}: {shared} voxels");
                }
                Assert.IsEmpty(hits,"Camp props interpenetrate:\n"+string.Join("\n",hits));
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
}

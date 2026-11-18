using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    public sealed class EmberdeepArtV3Tests
    {
        [Test] public void CastingFeedbackIsBoundedAndStopsWithoutLingeringRings()
        {
            var root=new GameObject("V3 magic validation");var view=new GameObject("View");
            try
            {
                var camera=view.AddComponent<Camera>();var fx=root.AddComponent<EmberdeepPalmAimVfx>();
                fx.Show(true,Vector3.zero,Vector3.forward,camera,true,float.NaN,true,.12f,Vector3.up);
                var mesh=root.GetComponent<MeshFilter>().sharedMesh;
                Assert.LessOrEqual(mesh.vertexCount,300);Assert.Greater(mesh.vertexCount,72);
                Assert.IsTrue(mesh.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)));
                Assert.Zero(root.GetComponentsInChildren<Collider>().Length);
                fx.Show(true,Vector3.zero,Vector3.forward,camera,true,.02f,false);
                Assert.AreEqual(72,mesh.vertexCount,"Release removes both active rings immediately");
                fx.Show(true,Vector3.zero,Vector3.forward,null,true,.02f,true);
                Assert.IsFalse(root.GetComponent<Renderer>().enabled);
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(view);}
        }
        [Test] public void SoftBoxHasOutwardFacesAndPreservesUnitBounds()
        {
            var mesh=EmberdeepCraftDetails.SoftBox;var v=mesh.vertices;var t=mesh.triangles;
            Assert.LessOrEqual(mesh.bounds.size.x,1.001f);Assert.Greater(mesh.bounds.size.x,.99f);
            for(int i=0;i<t.Length;i+=3)
            {
                var a=v[t[i]];var b=v[t[i+1]];var c=v[t[i+2]];
                Assert.Greater(Vector3.Dot(Vector3.Cross(b-a,c-a),(a+b+c)/3),0,"Closed outward faces prevent hollow-looking fittings");
            }
        }
    }
}

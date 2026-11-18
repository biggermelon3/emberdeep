using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    public sealed class Emberdeep023PresentationTests
    {
        [UnityTest] public IEnumerator PalmDemoPointsInwardAndOnlyRightHandApproaches()
        {
            var root=new GameObject("Palm orientation test");var demo=root.AddComponent<EmberdeepInteractionDemo>();demo.Show(EmberdeepInteractionDemo.Action.Goddess);
            var left=root.GetComponentsInChildren<Transform>().Single(t=>t.name=="Left demonstration hand");
            var right=root.GetComponentsInChildren<Transform>().Single(t=>t.name=="Right demonstration hand");
            var startLeft=left.localPosition;float startRight=right.localPosition.x;
            Assert.Greater(root.transform.InverseTransformDirection(left.forward).x,.7f);
            Assert.Less(root.transform.InverseTransformDirection(right.forward).x,-.7f);
            for(int i=0;i<30;i++)demo.Animate(.1f);
            Assert.AreEqual(startLeft,left.localPosition);Assert.Less(right.localPosition.x,startRight-.10f);
            Assert.Greater(right.localPosition.y,left.localPosition.y);Assert.Less((right.localPosition-left.localPosition).magnitude,.11f);Assert.Greater(right.localPosition.x-left.localPosition.x,.08f,"Only the fingertip reaches across; the right palm stays aside");
            Object.Destroy(root);yield return null;
        }
        [Test] public void WindCoversPlayerForegroundAndForestWithoutIncreasingRibbonSize()
        {
            var root=new GameObject("Wind coverage test");var view=new GameObject("View");var camera=view.AddComponent<Camera>();camera.transform.position=new Vector3(0,8,-4);
            try
            {
                var fx=new GameObject("FX").AddComponent<EmberdeepEnvironmentVfx>();fx.Initialize(root.transform);fx.Tick(.1f,true,camera);
                Assert.Less(fx.LocalBounds.min.z,-1.5f);Assert.Greater(fx.LocalBounds.max.z,10);
                Assert.LessOrEqual(fx.TriangleCount,EmberdeepEnvironmentVfx.MaximumTriangles);
                var vertices=fx.EffectMesh.vertices;int windStart=7*4+EmberdeepEnvironmentVfx.DustCount*4;
                Assert.AreEqual(.054f,Vector3.Distance(vertices[windStart+5*4],vertices[windStart+5*4+1]),.0001f);
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(view);}
        }
        [Test] public void PalmAimMotesAreBoundedVisualOnlyAndCanHideImmediately()
        {
            var root=new GameObject("Aim feedback");var view=new GameObject("View");var camera=view.AddComponent<Camera>();
            try
            {
                var fx=root.AddComponent<EmberdeepPalmAimVfx>();fx.Show(true,new Vector3(0,.4f,0),Vector3.forward,camera,true,.02f);
                Assert.True(root.GetComponent<Renderer>().enabled);Assert.AreEqual(72,root.GetComponent<MeshFilter>().sharedMesh.vertexCount);
                Assert.Zero(root.GetComponentsInChildren<Collider>().Length);fx.Hide();Assert.False(root.GetComponent<Renderer>().enabled);
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(view);}
        }
    }
}

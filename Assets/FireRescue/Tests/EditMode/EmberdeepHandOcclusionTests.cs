using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    public sealed class EmberdeepHandOcclusionTests
    {
        GameObject owner,solid;
        EmberdeepHandVisuals visuals;
        [SetUp] public void SetUp(){owner=new GameObject("Hand visual test");visuals=owner.AddComponent<EmberdeepHandVisuals>();solid=new GameObject("Solid interior");}
        [TearDown] public void TearDown(){Object.DestroyImmediate(owner);Object.DestroyImmediate(solid);}
        [Test] public void RotatedScaledSolidClipsOnlyItsInteriorAndDoesNotMoveTracking()
        {
            solid.transform.SetPositionAndRotation(new Vector3(1,2,3),Quaternion.Euler(0,45,20));solid.transform.localScale=new Vector3(2,.5f,3);
            var center=new Vector3(.2f,.1f,0);var half=new Vector3(.1f,.2f,.3f);
            Assert.IsTrue(visuals.SetOcclusionBox(0,solid.transform,center,half));
            Assert.IsTrue(visuals.ContainsOcclusionPoint(solid.transform.TransformPoint(center)));
            Assert.IsFalse(visuals.ContainsOcclusionPoint(solid.transform.TransformPoint(center+Vector3.right*.11f)));
            Assert.AreEqual(Vector3.zero,owner.transform.position);Assert.AreEqual(new Vector3(1,2,3),solid.transform.position);
        }
        [Test] public void ClearingDisablingAndDestroyingSolidRestoresHandPixels()
        {
            visuals.SetOcclusionBox(0,solid.transform,Vector3.zero,Vector3.one);
            Assert.IsTrue(visuals.ContainsOcclusionPoint(Vector3.zero));solid.SetActive(false);
            Assert.IsFalse(visuals.ContainsOcclusionPoint(Vector3.zero));solid.SetActive(true);
            visuals.ClearOcclusionBox(0);Assert.IsFalse(visuals.ContainsOcclusionPoint(Vector3.zero));
            visuals.SetOcclusionBox(0,solid.transform,Vector3.zero,Vector3.one);Object.DestroyImmediate(solid);
            Assert.IsFalse(visuals.ContainsOcclusionPoint(Vector3.zero));
        }
        [Test] public void InvalidVolumeClearsPreviousSlotWithoutErasingOtherSolid()
        {
            visuals.SetOcclusionBox(0,solid.transform,Vector3.zero,Vector3.one);
            Assert.IsFalse(visuals.SetOcclusionBox(0,solid.transform,Vector3.zero,new Vector3(float.NaN,1,1)));
            Assert.IsFalse(visuals.ContainsOcclusionPoint(Vector3.zero));
            Assert.IsFalse(visuals.SetOcclusionBox(12,solid.transform,Vector3.zero,Vector3.one));
            visuals.SetOcclusionBox(1,solid.transform,Vector3.zero,Vector3.one);visuals.ClearOcclusionBox(0);
            Assert.IsTrue(visuals.ContainsOcclusionPoint(Vector3.zero));visuals.ClearOcclusionBoxes();Assert.IsFalse(visuals.ContainsOcclusionPoint(Vector3.zero));
        }
    }
}

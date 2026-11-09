using NUnit.Framework;
using UnityEngine;

namespace PhysicalAssembly.Tests
{
    public sealed class NarrowGapRuleTests
    {
        [TestCase(0f,1f,true)] [TestCase(.13f,1f,false)] [TestCase(.3f,1f,false)]
        [TestCase(0f,1.9f,false)] [TestCase(0f,.50f,false)]
        public void SweptOpeningRequiresWholeSphereWithinStakes(float x,float y,bool expected)
        {
            Assert.AreEqual(expected,NarrowGapController.CrossesOpening(new Vector3(x,y,0),new Vector3(x,y,2),.9f,.145f,.5f,1.8f,.02f));
        }
        [Test] public void BackwardsAndAlreadyBeyondGateNeverCount()
        {
            Assert.IsFalse(NarrowGapController.CrossesOpening(new Vector3(0,1,2),new Vector3(0,1,0),.9f,.145f,.5f,1.8f,.02f));
            Assert.IsFalse(NarrowGapController.CrossesOpening(new Vector3(0,1,1),new Vector3(0,1,2),.9f,.145f,.5f,1.8f,.02f));
        }
    }
}

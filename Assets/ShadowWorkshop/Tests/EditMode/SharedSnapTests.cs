using NUnit.Framework;
using PhysicalAssembly;
using UnityEngine;

namespace ShadowWorkshopIntegration.Tests
{
    public class SharedSnapTests
    {
        [Test] public void SocketSnapRetainsOriginalEightOrientationSearch()
        {
            var random = new System.Random(1984);
            for (int sample=0;sample<100;sample++)
            {
                Quaternion Rotation() => Quaternion.Euler((float)random.NextDouble()*360,(float)random.NextDouble()*360,(float)random.NextDouble()*360);
                var source=Rotation(); var target=Rotation(); var normal=target*Vector3.forward;
                var facing=Quaternion.LookRotation(-normal,target*Vector3.up);
                var expected=facing; float smallest=float.MaxValue;
                for(int i=0;i<8;i++)
                {
                    var q=Quaternion.AngleAxis(i*45,-normal)*facing; float angle=Quaternion.Angle(source,q);
                    if(angle<smallest){smallest=angle;expected=q;}
                }
                Assert.Less(Quaternion.Angle(expected,BuildingGeometry.SnapDelta(source,target)*source),.06f);
            }
        }
        [Test] public void PuzzleGridRetainsFiveMillimetresAndFifteenDegrees()
        {
            var input=new Pose(new Vector3(.021f,.099f,-.032f),Quaternion.Euler(14,31,46));
            var actual=SnapGeometry.Grid(input,.005f,15);
            Assert.Less(Vector3.Distance(new Vector3(.02f,.10f,-.03f),actual.position),.00001f);
            Assert.Less(Quaternion.Angle(Quaternion.Euler(15,30,45),actual.rotation),.06f);
        }
    }
}

using NUnit.Framework;
using UnityEngine;
namespace PhysicalAssembly.Tests
{
    public sealed class SandboxTrackMomentumTests
    {
        GameObject root;
        SandboxSplineZone Zone(params Vector3[] points)
        {
            root=new GameObject("Momentum test");
            var track=root.AddComponent<Level2BezierTrack>();track.Samples=points;
            var zone=root.AddComponent<SandboxSplineZone>();zone.BezierTrack=track;
            zone.BrakingDistance=0;zone.RollingResistance=0;zone.RebuildPath();return zone;
        }
        [TearDown] public void Cleanup(){Object.DestroyImmediate(root);}
        [Test] public void InsufficientMomentumStopsAtEnergyHeightAndRollsBack()
        {
            var z=Zone(Vector3.zero,new Vector3(10,10,0));
            var m=new SandboxTrackMomentum(1,2);
            float g=9.81f/Mathf.Sqrt(2),t=2/g;
            m.Tick(z,Vector3.down*9.81f,t);
            Assert.That(m.Distance,Is.EqualTo(1+2/g).Within(.00001f));
            m.Tick(z,Vector3.down*9.81f,.1f);
            Assert.Less(m.Speed,0);Assert.IsFalse(m.Arrived);
        }
        [Test] public void SufficientMomentumCrossesCrestWithoutEnergyGain()
        {
            var z=Zone(Vector3.zero,new Vector3(1,1,0),new Vector3(2,0,0),new Vector3(30,0,0));
            var m=new SandboxTrackMomentum(0,6);m.Tick(z,Vector3.down*9.81f,1);
            Assert.Greater(m.Distance,Mathf.Sqrt(2)*2);Assert.That(m.Speed,Is.EqualTo(6).Within(.0001f));
        }
        [Test] public void StationaryCarNeverPropelsItselfEvenNearStop()
        {
            var z=Zone(Vector3.zero,Vector3.right*10);z.BrakingDistance=2;
            var m=new SandboxTrackMomentum(9,0);m.Tick(z,Vector3.zero,10);
            Assert.AreEqual(9,m.Distance);Assert.AreEqual(0,m.Speed);Assert.IsFalse(m.Arrived);
        }
        [Test] public void ResistanceStopsWithoutReversing()
        {
            var z=Zone(Vector3.zero,Vector3.right*100);z.RollingResistance=1;
            var m=new SandboxTrackMomentum(0,2);m.Tick(z,Vector3.down*9.81f,10);
            Assert.That(m.Distance,Is.EqualTo(2).Within(.0001f));Assert.AreEqual(0,m.Speed);
        }
        [Test] public void ParkingRemovesEnergyAndReachesStop()
        {
            var z=Zone(Vector3.zero,new Vector3(10,-3,0));z.BrakingDistance=2;
            var m=new SandboxTrackMomentum(0,3);
            for(int i=0;i<3000 && !m.Arrived;i++)m.Tick(z,Vector3.down*9.81f,1f/240);
            Assert.IsTrue(m.Arrived,"distance="+m.Distance+" speed="+m.Speed);
            Assert.AreEqual(z.StopDistance,m.Distance);Assert.AreEqual(0,m.Speed);
        }
        [Test] public void SegmentIntegrationIsIndependentOfStepAndScale()
        {
            var z=Zone(Vector3.zero,new Vector3(2,-1,0),new Vector3(4,1,0),new Vector3(6,-2,0),new Vector3(30,-2,0));
            var large=new SandboxTrackMomentum(.2f,5);large.Tick(z,Vector3.down*9.81f,2);
            root.transform.localScale=Vector3.one*.04f;
            var small=new SandboxTrackMomentum(.2f,5);
            for(int i=0;i<480;i++)small.Tick(z,Vector3.down*9.81f,1f/240);
            Assert.That(small.Distance,Is.EqualTo(large.Distance).Within(.001f));
            Assert.That(small.Speed,Is.EqualTo(large.Speed).Within(.001f));
        }
        [Test] public void RaisingSameTrackChangesClimbOutcome()
        {
            var z=Zone(Vector3.zero,new Vector3(2,.1f,0),new Vector3(10,.1f,0));
            var low=new SandboxTrackMomentum(.1f,3);low.Tick(z,Vector3.down*9.81f,1);
            Assert.Greater(low.Distance,2);
            z.BezierTrack.Samples=new[]{Vector3.zero,new Vector3(2,3,0),new Vector3(10,3,0)};
            z.RebuildPath();
            var high=new SandboxTrackMomentum(.1f,3);high.Tick(z,Vector3.down*9.81f,.6f);
            Assert.Less(high.Speed,0);Assert.Less(high.Distance,2);Assert.IsFalse(high.Arrived);
        }
        [Test] public void GravityCanStartCarInsideBrakingRegion()
        {
            var z=Zone(Vector3.zero,new Vector3(10,-3,0));z.BrakingDistance=2;
            var m=new SandboxTrackMomentum(z.StopDistance-1,0);
            float before=m.Distance;m.Tick(z,Vector3.down*9.81f,.02f);
            Assert.Greater(m.Distance,before);
        }
        [TestCase(2f,3f,1,6f)]
        [TestCase(6f,-3f,-1,2f)]
        public void TransitionReleasesAtEitherBoundaryWithoutBraking(float start,float speed,int direction,float end)
        {
            var z=Zone(Vector3.zero,Vector3.right*10);z.BrakingDistance=100;
            var m=new SandboxTrackMomentum(start,speed);m.Tick(z,Vector3.zero,3,2,6,true);
            Assert.IsTrue(m.Arrived);Assert.AreEqual(direction,m.ExitDirection);
            Assert.AreEqual(end,m.Distance);Assert.AreEqual(speed,m.Speed);
        }
        [Test] public void ValleyOscillationsDoNotGainEnergy()
        {
            var z=Zone(new Vector3(-10,10,0),Vector3.zero,new Vector3(10,10,0));
            var m=new SandboxTrackMomentum(5,0);
            float initial=9.81f*z.Sample(5).Position.y;
            for(int i=0;i<5000;i++){
                m.Tick(z,Vector3.down*9.81f,1f/240);
                float energy=.5f*m.Speed*m.Speed+9.81f*z.Sample(m.Distance).Position.y;
                Assert.That(energy,Is.EqualTo(initial).Within(.01f));
            }
        }
    }
}

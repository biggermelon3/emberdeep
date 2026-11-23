using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    // LoopSpec §4.6: which trees dither out between the eye and the aim point (arena units).
    public sealed class EmberdeepTreeReadoutTests
    {
        // FireMiniature.Place: eye .65 m above the board, arena origin .32 m ahead (x 1/.08 units).
        static readonly Vector3 Eye=new Vector3(0,8.1f,-4);
        const float Radius=.6f,Low=.4f,Top=2.2f;

        [Test] public void TreeDirectlyInFrontOfABackRowAimFades()
        {
            var aim=new Vector3(0,0,8.75f);
            Assert.IsTrue(EmberdeepTreeReadout.Occludes(Eye,aim,new Vector3(0,0,7.6f),Radius,Low,Top),"the row just in front hides the aim point");
        }
        [Test] public void AimedTreeAndTreesOffTheLineStaySolid()
        {
            var aim=new Vector3(0,0,8.75f);
            Assert.IsFalse(EmberdeepTreeReadout.Occludes(Eye,aim,new Vector3(0,0,8.75f),Radius,Low,Top),"the target itself never fades");
            Assert.IsFalse(EmberdeepTreeReadout.Occludes(Eye,aim,new Vector3(1.18f,0,7.6f),Radius,Low,Top),"a neighbour column is off the line of sight");
            Assert.IsFalse(EmberdeepTreeReadout.Occludes(Eye,aim,new Vector3(0,0,9.9f),Radius,Low,Top),"trees behind the aim point never fade");
        }
        [Test] public void LineOfSightPassingOverTheFrontRowLeavesItSolid()
        {
            // From the seat the sight line to the back row clears the front row's crowns.
            var aim=new Vector3(0,0,8.75f);
            Assert.IsFalse(EmberdeepTreeReadout.Occludes(Eye,aim,new Vector3(0,0,3),Radius,Low,Top));
            // Aiming at the front row's neighbour behind it: the front tree is in the way.
            Assert.IsTrue(EmberdeepTreeReadout.Occludes(Eye,new Vector3(0,0,4.15f),new Vector3(0,0,3),Radius,Low,Top));
        }
        // Headset test 2026-11-22: only the trees right on the aim line fade, and they stay mostly opaque.
        [Test] public void FadeIsNarrowAndMostlyOpaque()
        {
            var aim=new Vector3(0,0,8.75f);float r=Radius*EmberdeepTreeReadout.FadeMargin,solid=Radius*EmberdeepTreeReadout.SolidMargin;
            Assert.IsTrue(EmberdeepTreeReadout.Occludes(Eye,aim,new Vector3(0,0,7.6f),r,Low,Top,solid),"the tree right on the line fades");
            Assert.IsFalse(EmberdeepTreeReadout.Occludes(Eye,aim,new Vector3(.45f,0,7.6f),r,Low,Top,solid),"a crown the line only grazes stays solid");
            Assert.IsTrue(EmberdeepTreeReadout.Occludes(Eye,aim,new Vector3(.45f,0,7.6f),Radius*1.08f,Low,Top),"(it used to fade)");
            Assert.IsFalse(EmberdeepTreeReadout.Occludes(Eye,aim,new Vector3(.5f,0,8.75f),r,Low,Top,solid),"the aimed tree stays solid within its full crown");
            // The 4x4 dither keeps 1 - MaximumFade of the pixels: 70-85 %.
            Assert.That(1-EmberdeepTreeReadout.MaximumFade,Is.InRange(.7f,.85f));
        }
        [Test] public void DegenerateAimNeverFades()
        {
            Assert.IsFalse(EmberdeepTreeReadout.Occludes(Eye,new Vector3(Eye.x,0,Eye.z),new Vector3(0,0,3),Radius,Low,Top));
        }
    }
}

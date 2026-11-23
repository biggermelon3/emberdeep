using NUnit.Framework;

namespace FireRescue.Tests
{
    // Headset test 2 (2026-11-22): sticky menu pinches (EmberdeepPinchClick) and lost hands frozen in the air
    // (EmberdeepLostHand, EmberdeepSightGate). The menu-level click cases are in EmberdeepHandRayTests.
    public sealed class EmberdeepHandInputTests
    {
        const float Frame=1/72f;
        static EmberdeepPinchClick.Reading Tips(float span)=>EmberdeepPinchClick.Reading.Tips(span);
        static EmberdeepPinchClick.Reading Select(float value,bool pressed,float tips=float.PositiveInfinity)
            =>EmberdeepPinchClick.Reading.System(value,pressed,tips);
        static int Run(EmberdeepPinchClick click,EmberdeepPinchClick.Reading reading,int frames,int target=1)
        {int presses=0;for(int i=0;i<frames;i++)if(click.Sample(target,reading,Frame))presses++;return presses;}

        [Test] public void AClickIsTheClosingEdgeAfterAnOrdinaryRelease()
        {
            var click=new EmberdeepPinchClick();
            Assert.Zero(Run(click,Tips(.03f),10));Assert.IsTrue(click.Armed);
            Assert.AreEqual(1,Run(click,Tips(.012f),1));Assert.AreEqual(EmberdeepPinchClick.Event.Press,click.Last);
            Assert.Zero(Run(click,Tips(.012f),30),"held");
            Assert.Zero(Run(click,Tips(.022f),10),"2 cm is still inside the hysteresis: not a release");
            Assert.IsFalse(click.Armed);
            Assert.Zero(Run(click,Tips(EmberdeepPinchClick.ReleaseSpan),2));Assert.IsTrue(click.Armed,"2.6 cm for two frames rearms");
            Assert.AreEqual(1,Run(click,Tips(.012f),1));
        }
        [Test] public void MetasLateRuntimeReleaseDoesNotBlockTheNextClick()
        {
            var click=new EmberdeepPinchClick();
            Assert.Zero(Run(click,Select(.1f,false),10));
            Assert.AreEqual(1,Run(click,Select(.97f,true),1),"pressed edge");
            // IsPinching stays on; the strength drops as the fingers part.
            Assert.Zero(Run(click,Select(.55f,true),2));Assert.IsTrue(click.Armed);
            Assert.AreEqual(1,Run(click,Select(.92f,true),1),"strength back over CloseValue clicks without a new runtime edge");
            Assert.Zero(Run(click,Select(.8f,true),5),"a strong select still held is not a release");
            Assert.Zero(Run(click,Select(.8f,true,.03f),2),"but tips 3 cm apart are");
            Assert.IsTrue(click.Armed);
            Assert.AreEqual(1,Run(click,Select(.8f,false),1)+Run(click,Select(.8f,true),1),"a fresh runtime edge closes below CloseValue");
        }
        [Test] public void AnEdgeIsConsumedWhenUnarmedOffButtonOrInsideTheDwell()
        {
            var click=new EmberdeepPinchClick();
            Assert.Zero(Run(click,Tips(.012f),5));Assert.AreEqual(EmberdeepPinchClick.Event.None,click.Last);
            Assert.IsTrue(click.Closed,"a hand closed from the start is never armed");
            click.Reset();
            Assert.Zero(Run(click,Tips(.03f),5,0));
            Assert.Zero(Run(click,Tips(.012f),1,0));Assert.AreEqual(EmberdeepPinchClick.Event.NoTarget,click.Last);
            Assert.Zero(Run(click,Tips(.012f),20),"then held onto a button");
            Assert.Zero(Run(click,Tips(.03f),3,0));
            Assert.Zero(Run(click,Tips(.03f),2,7));Assert.Zero(Run(click,Tips(.012f),1,7));
            Assert.AreEqual(EmberdeepPinchClick.Event.Early,click.Last);
            Assert.Zero(Run(click,Tips(.03f),9,7));Assert.AreEqual(1,Run(click,Tips(.012f),1,7));
        }
        [Test] public void LostTrackingOrABadFrameNeedsAReleaseBeforeTheNextClick()
        {
            var click=new EmberdeepPinchClick();
            Run(click,Tips(.03f),10);
            Assert.IsFalse(click.Sample(1,default,Frame));Assert.IsFalse(click.Armed);
            Assert.Zero(Run(click,Tips(.012f),5),"back closed: no click");
            Run(click,Tips(.03f),10);Assert.IsFalse(click.Sample(1,Tips(.012f),float.PositiveInfinity));
            Assert.Zero(Run(click,Tips(.012f),5));
            Run(click,Tips(.03f),10);Assert.Zero(Run(click,Select(float.NaN,false),1));Assert.Zero(Run(click,Select(1,true),1));
            Assert.Zero(click.Sample(1,Tips(.03f),10)?1:0,"one long frame counts MaxObservedStep");
            Assert.IsTrue(click.Armed,"a 25 ms step is enough to rearm");
        }
        [Test] public void RestartDwellKeepsTheHandButNotTheSettledTarget()
        {
            var click=new EmberdeepPinchClick();
            Run(click,Tips(.03f),10);click.RestartDwell();
            Assert.Zero(Run(click,Tips(.012f),1));Assert.AreEqual(EmberdeepPinchClick.Event.Early,click.Last);
        }

        // A lost hand: frozen for FreezeSeconds, then hidden until it is valid again.
        [Test] public void ALostHandFreezesBrieflyThenHides()
        {
            var hand=new EmberdeepLostHand();
            Assert.AreEqual(EmberdeepLostHand.State.Live,hand.Step(true,true,Frame));
            Assert.AreEqual(EmberdeepLostHand.State.Frozen,hand.Step(false,true,Frame),"the loss frame freezes the last pose");
            int frozenFrames=0;
            while(hand.Step(false,true,Frame)==EmberdeepLostHand.State.Frozen && frozenFrames<100)frozenFrames++;
            Assert.AreEqual(EmberdeepLostHand.State.Hidden,hand.Current);
            Assert.That((frozenFrames+1)*Frame,Is.EqualTo(EmberdeepLostHand.FreezeSeconds).Within(Frame+.0001f),"about .2 s of freeze");
            for(int i=0;i<500;i++)Assert.AreEqual(EmberdeepLostHand.State.Hidden,hand.Step(false,true,Frame),"never comes back by itself");
            Assert.AreEqual(EmberdeepLostHand.State.Live,hand.Step(true,true,Frame));
            // A blip shorter than the grace never hides it.
            hand.Step(false,true,Frame);for(int i=0;i<5;i++)hand.Step(false,true,Frame);
            Assert.AreEqual(EmberdeepLostHand.State.Frozen,hand.Current);
            Assert.AreEqual(EmberdeepLostHand.State.Live,hand.Step(true,true,Frame));
            // Nothing to freeze (never drawn): hidden at once.
            Assert.AreEqual(EmberdeepLostHand.State.Hidden,new EmberdeepLostHand().Step(false,false,Frame));
            Assert.AreEqual(EmberdeepLostHand.State.Frozen,new EmberdeepLostHand().Step(false,true,float.NaN),"a bad frame time still freezes");
        }
        [Test] public void SightGateBridgesABlipButNotALoss()
        {
            var gate=new EmberdeepSightGate();
            Assert.IsFalse(gate.Step(true,false,Frame),"never seen: a bridged-only hand is not in view");
            Assert.IsTrue(gate.Step(true,true,Frame));
            for(int i=0;i<10;i++)Assert.IsTrue(gate.Step(true,false,Frame),"the rig bridges a short loss");
            Assert.IsTrue(gate.Step(true,true,Frame));
            int frames=0;while(gate.Step(true,false,Frame) && frames<100)frames++;
            Assert.That((frames+1)*Frame,Is.EqualTo(EmberdeepSightGate.LostAfter).Within(Frame+.0001f));
            Assert.IsFalse(gate.Step(true,false,Frame),"stale poses never bring it back");
            Assert.IsTrue(gate.Step(true,true,Frame));
            Assert.IsFalse(gate.Step(false,false,Frame),"no hand at all: gone at once");
        }
    }
}

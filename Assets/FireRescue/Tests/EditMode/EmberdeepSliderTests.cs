using System.Collections.Generic;
using NUnit.Framework;
using PhysicalAssembly;
using UnityEngine;

namespace FireRescue.Tests
{
    // Headset test 3 (2026-11-22): drag sliders (EmberdeepSlider / EmberdeepSliderDrag, run by EmberdeepHandRayMenu) and
    // the water-first ray rule (EmberdeepHandRayMenu.PalmAiming, ShowReach / HideReach).
    public sealed class EmberdeepSliderTests
    {
        const float Dt=.02f,Open=.05f,Closed=.01f;

        // ---- The drag mapping (pure) -------------------------------------------------------------------------------
        [Test] public void AGrabOnTheKnobKeepsItsPlaceAndAGrabElsewhereJumpsThere()
        {
            var drag=new EmberdeepSliderDrag();
            Assert.IsFalse(drag.Begin(0,EmberdeepSliderDrag.Source.Ray,.52f,.5f,50),"on the knob: no jump");
            Assert.AreEqual(.5f,drag.Value01,1e-5f);
            Assert.IsFalse(drag.Move(.53f),"half a step: held by the hysteresis");
            Assert.IsTrue(drag.Move(.54f));Assert.AreEqual(.52f,drag.Value01,1e-5f,"keeps the grab offset (-.02)");
            Assert.IsTrue(drag.Begin(1,EmberdeepSliderDrag.Source.Ray,.8f,.2f,50),"off the knob: the knob jumps to the pinch");
            Assert.AreEqual(.8f,drag.Value01,1e-5f);Assert.AreEqual(1,drag.Hand);
            Assert.IsTrue(drag.Move(.9f));Assert.AreEqual(.9f,drag.Value01,1e-5f,"then follows it 1:1");
            drag.Move(5);Assert.AreEqual(1,drag.Value01,1e-5f,"clamped at the end");
            drag.Move(-3);Assert.AreEqual(0,drag.Value01,1e-5f);
        }
        [Test] public void StepsSnapWithHysteresisSoJitterCannotFlickerAStep()
        {
            var drag=new EmberdeepSliderDrag();drag.Begin(0,EmberdeepSliderDrag.Source.Ray,.5f,.5f,50); // 1 step = .02
            Assert.IsFalse(drag.Move(.512f),".6 step");
            Assert.IsTrue(drag.Move(.5135f));Assert.AreEqual(.52f,drag.Value01,1e-5f,".675 step: the next whole step");
            Assert.IsFalse(drag.Move(.508f),"back .6 step: stays");Assert.AreEqual(.52f,drag.Value01,1e-5f);
            for(int i=0;i<20;i++)Assert.IsFalse(drag.Move(.51f+(i%2==0?.002f:-.002f)),"jitter around the boundary");
            Assert.IsTrue(drag.Move(.506f));Assert.AreEqual(.5f,drag.Value01,1e-5f);
            Assert.IsTrue(drag.Move(.6f));Assert.AreEqual(.6f,drag.Value01,1e-5f,"a fast move takes several steps at once");
            // Whole centimetres on the height slider: every value is a step.
            drag.Begin(0,EmberdeepSliderDrag.Source.Ray,.3333f,.1f,EmberdeepPlayArea.HeightSteps);
            float metres=Mathf.Lerp(EmberdeepPlayArea.HeightMin,EmberdeepPlayArea.HeightMax,drag.Value01);
            Assert.AreEqual(Mathf.Round(metres*100),metres*100,1e-3f);
        }
        [Test] public void ContinuousSlidersFollowThePointerExactly()
        {
            var drag=new EmberdeepSliderDrag();drag.Begin(0,EmberdeepSliderDrag.Source.Ray,.3f,.3f,0);
            Assert.IsTrue(drag.Move(.3013f));Assert.AreEqual(.3013f,drag.Value01,1e-6f);
        }
        [Test] public void ReleaseCommitsTheValueFromBeforeTheFingersOpened()
        {
            var drag=new EmberdeepSliderDrag();drag.Begin(0,EmberdeepSliderDrag.Source.Ray,.5f,.5f,0);
            for(int i=1;i<=10;i++){drag.Move(.5f+.01f*i);Assert.IsTrue(drag.Tick(true,true,Dt));}
            Assert.AreEqual(.6f,drag.Value01,1e-5f);
            // The release frame: the opening fingers dip the ray, then the release is seen.
            drag.Move(.3f);Assert.IsTrue(drag.Tick(true,false,Dt),"not yet: a release must hold for RearmSeconds");
            Assert.IsTrue(drag.Releasing);Assert.IsFalse(drag.Move(.9f),"frozen while the release is confirmed");
            Assert.IsFalse(drag.Tick(true,false,Dt),"released");Assert.IsFalse(drag.Active);
            Assert.AreEqual(.57f,drag.Value01,1e-4f,"the value from ReleaseLead (80 ms) before the release began");
        }
        [Test] public void OneOpenFrameIsNotARelease()
        {
            var drag=new EmberdeepSliderDrag();drag.Begin(0,EmberdeepSliderDrag.Source.Ray,.5f,.5f,0);
            Assert.IsTrue(drag.Tick(true,true,Dt));Assert.IsTrue(drag.Tick(true,false,Dt));
            Assert.IsTrue(drag.Tick(true,true,Dt));Assert.IsFalse(drag.Releasing,"pinched again: still dragging");
            Assert.IsTrue(drag.Move(.7f),"and it moves again");
            Assert.IsTrue(drag.Tick(true,false,Dt));Assert.IsFalse(drag.Tick(true,false,Dt));
        }
        [Test] public void ALostHandEndsTheDragAfterLostSeconds()
        {
            var drag=new EmberdeepSliderDrag();drag.Begin(0,EmberdeepSliderDrag.Source.Ray,.5f,.5f,0);drag.Move(.7f);
            for(int i=0;i<3;i++)Assert.IsTrue(drag.Tick(false,false,.06f),"a short tracking gap keeps the drag");
            Assert.IsTrue(drag.Tick(true,true,Dt),"back in time");
            for(int i=0;i<3;i++)drag.Tick(false,false,.06f);
            Assert.IsFalse(drag.Tick(false,false,.06f),"gone");Assert.AreEqual(.7f,drag.Value01,1e-5f,"ends where it was");
        }

        // ---- Track geometry ------------------------------------------------------------------------------------------
        [Test] public void PointerAndPokeMapOntoATurnedScaledTrack()
        {
            var go=new GameObject("Turned track space");
            try
            {
                go.transform.SetPositionAndRotation(new Vector3(1,2,3),Quaternion.Euler(10,30,0));go.transform.localScale=Vector3.one*2;
                var s=new EmberdeepSlider{Space=go.transform,Left=-.1f,Right=.1f,Y=0,Face=0,HalfHeight=.03f};
                var from=go.transform.TransformPoint(new Vector3(-.2f,.1f,-.5f));var to=go.transform.TransformPoint(new Vector3(.05f,0,0));
                Assert.IsTrue(s.Pointer01(new Ray(from,to-from),out float t));Assert.AreEqual(.75f,t,1e-4f);
                Assert.IsTrue(s.PointOnTrack(new Ray(from,to-from),out var point));Assert.Less(Vector3.Distance(point,to),1e-4f);
                Assert.IsFalse(s.Pointer01(new Ray(from,from-to),out _),"pointing away");
                Assert.IsTrue(s.Poke01(go.transform.TransformPoint(new Vector3(.05f,.01f,-.01f)),out t,out float depth));
                Assert.AreEqual(.75f,t,1e-4f);Assert.AreEqual(.02f,depth,1e-4f,"metres in front of the face (scale 2)");
                Assert.IsTrue(s.Poke01(go.transform.TransformPoint(new Vector3(.05f,0,.01f)),out _,out depth));Assert.Less(depth,0,"pushed through");
                Assert.IsFalse(s.Poke01(go.transform.TransformPoint(new Vector3(.05f,.05f,-.01f)),out _,out _),"above the band");
            }
            finally{Object.DestroyImmediate(go);}
        }

        // ---- Through the menu ----------------------------------------------------------------------------------------
        GameObject root;EmberdeepHandRayMenu menu;WorkshopButton track,other;WorkshopButton[] buttons;
        EmberdeepSlider slider;float value;int commits,otherPresses;readonly List<float> live=new();
        [SetUp] public void Setup()
        {
            root=new GameObject("Slider rules");menu=root.AddComponent<EmberdeepHandRayMenu>();
            // The track spans x -.2..+.2 (knob travel -.18..+.18) at z 1; another button sits just above it.
            track=Button("Track",new Vector3(0,0,1),new Vector3(.4f,.06f,.03f));
            other=Button("Other",new Vector3(0,.1f,1),new Vector3(.2f,.06f,.03f));other.Pressed=()=>otherPresses++;
            buttons=new[]{track,other};Physics.SyncTransforms();
            value=.5f;commits=otherPresses=0;live.Clear();
            slider=new EmberdeepSlider{Name="Test",Track=track,Space=root.transform,Left=-.18f,Right=.18f,Y=0,Face=.985f,HalfHeight=.03f,Steps=36,
                Get01=()=>value,Drag01=t=>{value=t;live.Add(t);},Commit=()=>commits++};
            menu.AddSlider(slider);
        }
        [TearDown] public void Cleanup(){if(root)Object.DestroyImmediate(root);}
        WorkshopButton Button(string name,Vector3 at,Vector3 size)
        {
            var go=new GameObject(name);go.transform.SetParent(root.transform,false);go.transform.position=at;
            go.AddComponent<BoxCollider>().size=size;var b=go.AddComponent<WorkshopButton>();b.Command=name;return b;
        }
        // From the origin to (x,y) on the buttons' near face (z .985).
        static EmberdeepHandRay.Sample At(float x,float y,float pinch)=>new(true,new Ray(Vector3.zero,new Vector3(x/.985f,y/.985f,1)),pinch);
        bool Left(EmberdeepHandRay.Sample s)=>menu.ProcessSamples(s,default,buttons,true,Dt);

        [Test] public void APinchOnTheTrackGrabsAndDragsInsteadOfPressing()
        {
            for(int i=0;i<8;i++)Assert.IsFalse(Left(At(0,0,Open)));
            Assert.IsFalse(Left(At(0,0,Closed)),"a grab is not a press");
            Assert.AreSame(slider,menu.Dragging);Assert.AreEqual(0,menu.DragHand);Assert.AreEqual(EmberdeepSliderDrag.Source.Ray,menu.DragSource);
            Assert.AreEqual(.5f,value,1e-5f,"grabbed on the knob: it stays");
            Assert.IsTrue(menu.OwnsInput);
            // Drag right: x .09 on the face is .75 along the knob's travel.
            for(int i=1;i<=5;i++)Assert.IsFalse(Left(At(.018f*i,0,Closed)));
            Assert.AreEqual(.75f,value,1e-4f,"the value follows live");Assert.Greater(live.Count,1);Assert.Zero(commits,"not saved while held");
            Assert.AreSame(track,menu.HoveredButton);Assert.IsTrue(menu.RayVisible(0));
            Assert.That(menu.RayEnd(0).x,Is.EqualTo(.09f).Within(.002f),"the cursor rides the track");
            // Still pinched, the ray wanders up onto the other button: it never clicks and never takes the hover.
            for(int i=0;i<15;i++)Assert.IsFalse(Left(At(0,.1f,Closed)));
            Assert.Zero(otherPresses);Assert.IsFalse(menu.IsHovered(other));Assert.AreSame(slider,menu.Dragging);
            Assert.AreEqual(.5f,value,1e-4f,"only the position along the track counts");
            // Release: saved once, the hand is free again.
            Assert.IsFalse(Left(At(0,.1f,Open)));Assert.AreSame(slider,menu.Dragging,"a release must hold for RearmSeconds");
            Assert.IsFalse(Left(At(0,.1f,Open)));
            Assert.IsNull(menu.Dragging);Assert.AreEqual(1,commits);Assert.AreEqual(1,menu.DragCommits);
            Assert.IsFalse(Left(At(0,.1f,Open)));
            Assert.AreSame(other,menu.HoveredButton,"the ray is back to ordinary hover");Assert.Zero(otherPresses,"still no click");
            // A fresh pinch on the other button clicks it as usual.
            for(int i=0;i<8;i++)Left(At(0,.1f,Open));
            Assert.IsTrue(Left(At(0,.1f,Closed)));Assert.AreSame(other,menu.PressedButton);
        }
        [Test] public void APinchOffTheKnobJumpsItAndAResetCommits()
        {
            for(int i=0;i<8;i++)Left(At(-.18f,0,Open));
            Left(At(-.18f,0,Closed));Assert.AreEqual(0,value,1e-4f,"jumped to the pinch");Assert.Zero(commits);
            menu.ResetInput();Assert.IsNull(menu.Dragging);Assert.AreEqual(1,commits,"a page change ends the drag where it is and saves it");
        }
        [Test] public void AHeldPinchArrivingOnTheTrackNeverGrabsIt()
        {
            for(int i=0;i<8;i++)Left(At(0,.3f,Open));
            for(int i=0;i<5;i++)Left(At(0,.3f,Closed));
            for(int i=0;i<15;i++)Assert.IsFalse(Left(At(.1f,0,Closed)));
            Assert.IsNull(menu.Dragging,"the closing edge was spent off the track");Assert.AreEqual(.5f,value,1e-5f);
        }
        [Test] public void AFingertipOnTheTrackDragsItUntilLifted()
        {
            menu.Poke(0,true,new Vector3(.09f,0,1.06f),Dt);Assert.IsNull(menu.Dragging,"behind the panel is not a touch");
            menu.Poke(0,true,new Vector3(.09f,0,.95f),Dt);Assert.IsNull(menu.Dragging,"3.5 cm in front: not touching yet");
            menu.Poke(0,true,new Vector3(.09f,0,.976f),Dt);
            Assert.AreSame(slider,menu.Dragging);Assert.AreEqual(EmberdeepSliderDrag.Source.Poke,menu.DragSource);Assert.AreEqual(.75f,value,1e-4f);
            menu.Poke(0,true,new Vector3(-.09f,0,.965f),Dt);Assert.AreEqual(.25f,value,1e-4f,"slides with the tip, 2 cm off the face");
            menu.Poke(1,true,new Vector3(0,0,.976f),Dt);Assert.AreEqual(0,menu.DragHand,"one drag at a time");
            menu.Poke(0,true,new Vector3(-.09f,0,.94f),Dt);Assert.AreSame(slider,menu.Dragging);
            menu.Poke(0,true,new Vector3(-.09f,0,.94f),Dt);Assert.IsNull(menu.Dragging,"lifted off");Assert.AreEqual(1,commits);
        }

        // ---- Water first (camp) --------------------------------------------------------------------------------------
        [Test] public void AnOpenPalmAimingHandGetsARayOnlyOnADirectHit()
        {
            menu.ShowUnfocusedRays=true;
            // The other button spans x -.1..+.1, y .07...13 on the face: x .14 is 4 cm off its right edge.
            var beside=At(.14f,.1f,Open);
            Left(beside);Assert.IsTrue(menu.RayVisible(0),"near the panel: the ray shows");
            menu.ResetInput();
            menu.PalmAiming=hand=>hand==0;
            for(int i=0;i<5;i++)Left(beside);
            Assert.IsFalse(menu.RayVisible(0),"aiming the palm: no near-panel ray");Assert.IsFalse(menu.RayShown(0));
            Left(At(0,.1f,Open));Assert.IsTrue(menu.RayVisible(0),"a direct hit still shows it");Assert.AreSame(other,menu.HoveredButton);
            Left(beside);Assert.IsFalse(menu.RayVisible(0),"and it goes as soon as the ray leaves the button");
            menu.ProcessSamples(default,beside,buttons,true,Dt);Assert.IsTrue(menu.RayVisible(1),"the other hand keeps the near-panel ray");
        }
        [Test] public void TheNearPanelReachCanShrink()
        {
            menu.ShowUnfocusedRays=true;menu.ShowReach=.03f;menu.HideReach=.05f;
            Left(At(.16f,.1f,Open));Assert.IsFalse(menu.RayVisible(0),"6 cm off: outside 3 cm");
            Left(At(.12f,.1f,Open));Assert.IsTrue(menu.RayVisible(0),"2 cm off");
            Left(At(.14f,.1f,Open));Assert.IsTrue(menu.RayVisible(0),"4 cm: held to 5 cm");
            Left(At(.16f,.1f,Open));Assert.IsFalse(menu.RayVisible(0));
            var spare=new GameObject("Default reach");spare.transform.SetParent(root.transform,false);
            Assert.AreEqual(EmberdeepHandRayMenu.PanelShowReach,spare.AddComponent<EmberdeepHandRayMenu>().ShowReach,"default reach unchanged");
        }
    }
}

using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    // Headset test 3 (2026-11-22): the palm water only aimed with a nearly flat hand, and the gem press was unreliable.
    // EmberdeepPalmGesture now aims from openness .70 (holds to .62) and presses on the first touching frame of any other-hand
    // fingertip on a larger back-of-hand box, with grace for blinks of the tip or the water hand. The water hand here is a
    // real grip frame: palm down, fingers forward (x across, y palmar, z distal in GripRotation).
    public sealed class EmberdeepPalmGestureTests
    {
        const float Range=1.12f,Frame=1/72f;
        static readonly Quaternion PalmDown=Quaternion.LookRotation(Vector3.forward,Vector3.down);
        static FireToolManipulation.Hand Water(float closure=0)=>new(){Valid=true,HasGripFrame=true,GripRotation=PalmDown,
            Contact=new Vector3(0,.4f,.3f),Palm=new Pose(new Vector3(0,.4f,.3f),PalmDown),Closure=closure};
        // A point in the water hand's frame: across, height toward the back of the hand (-palmar), toward the knuckles.
        static Vector3 OnHand(FireToolManipulation.Hand h,float across,float height,float along)=>h.Contact+h.GripRotation*new Vector3(across,-height,along);
        static GameObject Arena(){var go=new GameObject("Palm gesture arena");go.transform.localScale=Vector3.one*FireMiniature.Scale;return go;}
        static void Step(EmberdeepPalmGesture g,FireToolManipulation.Hand h,Vector3[] tips,int mask,Transform arena,float dt=Frame,bool strict=false)
            =>g.Sample(h,tips,mask,strict,true,arena,Range,dt);

        [Test] public void OpennessStartsAtSeventyHoldsToSixtyTwoAndStaysClearOfTheGrabs()
        {
            // The grabs close at .42 (openness .58): a hand still aiming can never be grabbing.
            Assert.Greater(EmberdeepPalmGesture.OpenHold,EmberdeepPalmGesture.Openness(EmberdeepSheepInteraction.GrabClosure));
            Assert.Greater(EmberdeepPalmGesture.OpenHold,EmberdeepPalmGesture.Openness(EmberdeepPieceGrasp.Close));
            Assert.Greater(EmberdeepPalmGesture.OpenStart-EmberdeepPalmGesture.OpenHold,.05f,"a hysteresis band wider than closure jitter");
            var go=Arena();
            try
            {
                var g=new EmberdeepPalmGesture();var tips=new Vector3[5];
                Step(g,Water(.31f),tips,0,go.transform);Assert.False(g.Aiming,"openness .69 does not start");
                Step(g,Water(.295f),tips,0,go.transform);Assert.True(g.Aiming,"openness .70 starts (the old gate needed every finger under .24)");
                // A relaxed hand drifting between .64 and .70 never flickers.
                for(int i=0;i<60;i++){Step(g,Water(.33f+.04f*Mathf.Sin(i*1.7f)),tips,0,go.transform);Assert.True(g.Aiming,"held at frame "+i);Assert.False(g.Bridged);}
                // Closing past the hold drops it once HandGrace is over.
                for(int i=0;i<4;i++)Step(g,Water(.40f),tips,0,go.transform);Assert.True(g.Aiming && g.Bridged,"inside HandGrace");
                for(int i=0;i<4;i++)Step(g,Water(.40f),tips,0,go.transform);Assert.False(g.Aiming,"openness .60 stops");
                Step(g,Water(.36f),tips,0,go.transform);Assert.False(g.Aiming,"re-start needs .70 again");
                // The strict read (EmberdeepHandGrasp.IsOpenPalm) still counts: it implies the gate on a tracked hand.
                Step(g,Water(.36f),tips,0,go.transform,Frame,true);Assert.True(g.Aiming);
            }
            finally{Object.DestroyImmediate(go);}
        }

        [Test] public void AQuickTapSpraysAShortSquirtAndANewTapPressesAgain()
        {
            var go=Arena();
            try
            {
                var g=new EmberdeepPalmGesture();var h=Water();var tips=new Vector3[5];
                Step(g,h,tips,0,go.transform);Assert.True(g.Aiming);Assert.False(g.Casting);
                // One frame of the right middle fingertip on the gem.
                tips[2]=OnHand(h,0,.02f,0);Step(g,h,tips,4,go.transform);Assert.True(g.Casting,"the first touching frame presses");
                int frames=1;
                while(g.Casting && frames<60){Step(g,h,tips,0,go.transform);if(g.Casting)frames++;}
                float seconds=frames*Frame;
                Assert.That(seconds,Is.InRange(EmberdeepPalmGesture.MinPress-Frame,EmberdeepPalmGesture.MinPress+Frame),"a tap is a MinPress squirt");
                Step(g,h,tips,4,go.transform);Assert.True(g.Casting);Assert.AreEqual(2,g.Presses);
                // Held, then lifted: water stops ReleaseGrace after the lift.
                for(int i=0;i<30;i++)Step(g,h,tips,4,go.transform);
                int after=0;while(g.Casting && after<60){Step(g,h,tips,0,go.transform);after++;}
                Assert.That(after*Frame,Is.InRange(EmberdeepPalmGesture.ReleaseGrace,EmberdeepPalmGesture.ReleaseGrace+2*Frame));
            }
            finally{Object.DestroyImmediate(go);}
        }

        [Test] public void NearMissesInsideTheNewBoxPressAndClearMissesDoNot()
        {
            var h=Water();var tips=new Vector3[5];
            // (across, height, along) the old box (|x|<.042, height .004-.042, |z|<.048) missed, all on the back of the hand.
            var near=new[]{new Vector3(.048f,.02f,0),new Vector3(-.05f,.02f,.02f),new Vector3(0,.02f,.058f),new Vector3(0,.02f,-.054f),
                new Vector3(0,.047f,0),new Vector3(0,-.002f,0),new Vector3(.03f,.0f,.03f)};
            foreach(var p in near)
            {
                Assert.False(OldBox(p),"the old box missed "+p);
                tips[1]=OnHand(h,p.x,p.y,p.z);Assert.True(EmberdeepPalmGesture.Touching(h,tips,2,false),"presses now: "+p);
            }
            // Clear misses: beside the hand, past the knuckles, a raised hover, the palm side.
            var far=new[]{new Vector3(.065f,.02f,0),new Vector3(0,.02f,.08f),new Vector3(0,.02f,-.075f),new Vector3(0,.07f,0),new Vector3(0,-.012f,0),new Vector3(0,-.03f,.02f)};
            foreach(var p in far){tips[1]=OnHand(h,p.x,p.y,p.z);Assert.False(EmberdeepPalmGesture.Touching(h,tips,2,true),"never presses: "+p);}
            // A held press tolerates PressMargin more on the edge.
            tips[1]=OnHand(h,.06f,.02f,0);Assert.False(EmberdeepPalmGesture.Touching(h,tips,2,false));Assert.True(EmberdeepPalmGesture.Touching(h,tips,2,true));
            // Any fingertip, and only the valid ones.
            for(int f=0;f<5;f++){var t=new Vector3[5];t[f]=OnHand(h,0,.02f,0);Assert.True(EmberdeepPalmGesture.Touching(h,t,1<<f,false),"tip "+f);Assert.False(EmberdeepPalmGesture.Touching(h,t,31&~(1<<f),false));}
        }
        static bool OldBox(Vector3 p)=>p.y>=.004f && p.y<=.042f && Mathf.Abs(p.x)<.042f && Mathf.Abs(p.z)<.048f;

        [Test] public void AJitteryPressWithBlinksKeepsSprayingUntilTheRealLift()
        {
            var go=Arena();
            try
            {
                var g=new EmberdeepPalmGesture();var h=Water();var tips=new Vector3[5];var random=new System.Random(7);
                Step(g,h,tips,0,go.transform);
                int frames=0,dry=0;
                for(int i=0;i<216;i++) // 3 s at 72 Hz
                {
                    float J()=>(float)(random.NextDouble()*2-1);
                    // The pressing index wobbles +-9 mm in every axis around the gem, toward and away from the skin.
                    tips[1]=OnHand(h,J()*.009f,.019f+J()*.012f,J()*.009f);
                    int mask=2;
                    if(i%17==5)mask=0;                       // single-frame dropouts of the tip
                    if(i%53==20 || i%53==21 || i%53==22)mask=0; // three-frame dropouts
                    var water=h;
                    if(i%71==40 || i%71==41)water.Valid=false; // the water hand blinks under the pressing finger
                    if(i%45==30)water.Closure=.45f;           // and its finger read spikes closed for a frame
                    Step(g,water,tips,mask,go.transform);
                    frames++;if(!g.Casting)dry++;
                }
                Assert.AreEqual(0,dry,"no dry frame in "+frames);Assert.AreEqual(1,g.Presses,"one press, never re-started");
                int after=0;while(g.Casting && after<60){Step(g,h,tips,0,go.transform);after++;}
                Assert.LessOrEqual(after*Frame,EmberdeepPalmGesture.ReleaseGrace+2*Frame,"the real lift still stops it");
            }
            finally{Object.DestroyImmediate(go);}
        }

        [Test] public void TheWaterHandsOwnFingersNeverPress([Values(1f,-1f)] float thumbSide)
        {
            var h=Water();var tips=new Vector3[5];
            // Own fingertips of an open, relaxed or closing water hand, in its own frame (across, height, along); the thumb
            // side flips with the hand.
            var own=new[]
            {
                new Vector3(0,0,.17f),new Vector3(.025f,.005f,.16f),new Vector3(-.045f,.0f,.13f), // extended fingers
                new Vector3(.02f,-.03f,.12f),new Vector3(-.03f,-.035f,.10f),                     // relaxed, curled under
                new Vector3(.02f,-.035f,.03f),new Vector3(-.02f,-.03f,.045f),                    // a fist's tips on the palm
                new Vector3(thumbSide*.07f,-.01f,.06f),                                            // thumb spread
                new Vector3(thumbSide*.045f,-.012f,.085f),                                         // thumb along the index
                new Vector3(thumbSide*.02f,-.03f,.04f),                                            // thumb tucked under the palm
            };
            for(int i=0;i<own.Length;i++)
            {
                for(int f=0;f<5;f++)tips[f]=OnHand(h,own[(i+f)%own.Length].x,own[(i+f)%own.Length].y,own[(i+f)%own.Length].z);
                Assert.False(EmberdeepPalmGesture.Touching(h,tips,31,true),"own tip set "+i);
            }
            var go=Arena();
            try
            {
                var g=new EmberdeepPalmGesture();
                for(int i=0;i<40;i++){for(int f=0;f<5;f++)tips[f]=OnHand(h,own[(i+f)%own.Length].x,own[(i+f)%own.Length].y,own[(i+f)%own.Length].z);Step(g,h,tips,31,go.transform);}
                Assert.True(g.Aiming);Assert.False(g.Casting);Assert.AreEqual(0,g.Presses);
            }
            finally{Object.DestroyImmediate(go);}
        }
    }
}

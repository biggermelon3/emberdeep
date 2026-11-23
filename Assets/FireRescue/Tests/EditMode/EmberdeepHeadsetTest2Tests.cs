using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    // Headset test 2 (2026-11-22), the rim/screens half: hint keys versioned and resettable, the pause steppers'
    // hold-to-repeat timing, the board offset labels, the bell over the field.
    public sealed class EmberdeepHeadsetTest2Tests
    {
        static readonly EmberdeepFirstHints.Move[] Moves=(EmberdeepFirstHints.Move[])System.Enum.GetValues(typeof(EmberdeepFirstHints.Move));
        [Test] public void HintKeysAreVersionedSoOldCountsNoLongerRetireThem()
        {
            var saved=new int[Moves.Length];var old=new int[Moves.Length];
            for(int i=0;i<Moves.Length;i++){saved[i]=PlayerPrefs.GetInt(EmberdeepFirstHints.Key(Moves[i]),-1);old[i]=PlayerPrefs.GetInt("Emberdeep.Hint."+Moves[i],-1);}
            int resets=0;void Count()=>resets++;
            EmberdeepFirstHints.Reset+=Count;
            try
            {
                Assert.AreEqual("Emberdeep.Hint."+EmberdeepFirstHints.KeyVersion+".Bell",EmberdeepFirstHints.Key(EmberdeepFirstHints.Move.Bell));
                Assert.AreNotEqual("Emberdeep.Hint.Bell",EmberdeepFirstHints.Key(EmberdeepFirstHints.Move.Bell));
                EmberdeepFirstHints.ResetAll();Assert.AreEqual(1,resets);Assert.True(EmberdeepFirstHints.AllFresh);
                // A device from an earlier test build: every move retired under the old keys.
                foreach(var m in Moves)PlayerPrefs.SetInt("Emberdeep.Hint."+m,2);
                foreach(var m in Moves)Assert.True(EmberdeepFirstHints.Needed(m),m+" shows again in this build");
                var go=new GameObject("Hints");
                try
                {
                    var hints=go.AddComponent<EmberdeepFirstHints>();hints.Performed(EmberdeepFirstHints.Move.Slap);
                    Assert.False(EmberdeepFirstHints.AllFresh);Assert.AreEqual(1,EmberdeepFirstHints.Remaining(EmberdeepFirstHints.Move.Slap));
                    Assert.False(PlayerPrefs.HasKey("Emberdeep.Hint.Slap"),"The old count is dropped once the move is performed");
                }
                finally{Object.DestroyImmediate(go);}
                EmberdeepFirstHints.ResetAll();
                foreach(var m in Moves){Assert.False(PlayerPrefs.HasKey("Emberdeep.Hint."+m));Assert.AreEqual(EmberdeepFirstHints.Shows,EmberdeepFirstHints.Remaining(m));}
            }
            finally
            {
                EmberdeepFirstHints.Reset-=Count;
                for(int i=0;i<Moves.Length;i++)
                {
                    if(saved[i]<0)PlayerPrefs.DeleteKey(EmberdeepFirstHints.Key(Moves[i]));else PlayerPrefs.SetInt(EmberdeepFirstHints.Key(Moves[i]),saved[i]);
                    if(old[i]<0)PlayerPrefs.DeleteKey("Emberdeep.Hint."+Moves[i]);else PlayerPrefs.SetInt("Emberdeep.Hint."+Moves[i],old[i]);
                }
            }
        }
        [Test] public void StepperHoldRepeatsAfterADelayAndStopsOnRelease()
        {
            var hold=new EmberdeepStepperHold();
            Assert.AreEqual(0,hold.Tick(true,0),"Nothing before a press");
            hold.Press(10);Assert.True(hold.Holding);
            Assert.AreEqual(0,hold.Tick(true,10+FireRescueGame.RepeatDelay-.01f),"The press itself was the first step");
            Assert.AreEqual(1,hold.Tick(true,10+FireRescueGame.RepeatDelay+.001f));
            Assert.AreEqual(0,hold.Tick(true,10+FireRescueGame.RepeatDelay+.05f));
            Assert.AreEqual(1,hold.Tick(true,10+FireRescueGame.RepeatDelay+FireRescueGame.RepeatEvery+.002f));
            Assert.AreEqual(1,hold.Tick(true,20),"A long frame repeats once, not many times");
            Assert.AreEqual(0,hold.Tick(true,20.01f));
            Assert.AreEqual(0,hold.Tick(false,20.5f));Assert.False(hold.Holding,"Released");
            Assert.AreEqual(0,hold.Tick(true,21),"A new pinch needs a new press");
        }
        [Test] public void BoardOffsetsReadLikeTheCampSettings()
        {
            Assert.AreEqual("0 cm",FireRescueGame.BoardOffsetLabel(0));Assert.AreEqual("0 cm",FireRescueGame.BoardOffsetLabel(.004f));
            Assert.AreEqual("+2 cm",FireRescueGame.BoardOffsetLabel(.02f));Assert.AreEqual("-14 cm",FireRescueGame.BoardOffsetLabel(-.14f));
        }
        [Test] public void TheBellFloatsCentredOverTheField()
        {
            var run=new FireRun(true);Assert.True(run.StartStage(true));
            var field=EmberdeepRimHud.BellOver(run);
            float x0=float.MaxValue,x1=float.MinValue,z0=float.MaxValue,z1=float.MinValue;
            foreach(var t in run.Trees){x0=Mathf.Min(x0,t.Position.x);x1=Mathf.Max(x1,t.Position.x);z0=Mathf.Min(z0,t.Position.z);z1=Mathf.Max(z1,t.Position.z);}
            float vane=EmberdeepWeathervane.Anchor.x;
            Assert.AreEqual(Mathf.Min((x0+x1)*.5f-EmberdeepRimHud.BellAcross*(x1-x0)*.5f,vane-EmberdeepRimHud.BellVaneClear),field.x,1e-4f,
                "The field's centre, kept BellVaneClear off the vane's line toward the (left, -1) water hand");
            Assert.AreEqual(Mathf.Lerp(z0,z1,EmberdeepRimHud.BellDepth),field.y,1e-4f);
            var l=EmberdeepRimHud.LayoutFor(EmberdeepRimHud.DefaultBoard,1,EmberdeepRimHud.DefaultEye,field);
            Assert.AreEqual(field.x,l.Bell.x,1e-4f);Assert.AreEqual(field.y,l.Bell.z,1e-4f);
            Assert.Greater(l.Bell.y,EmberdeepRimHud.DefaultBoard.max.y+EmberdeepRimHud.CanopyTop,"Clear of the crowns");
            var mirrored=EmberdeepRimHud.LayoutFor(EmberdeepRimHud.DefaultBoard,-1,EmberdeepRimHud.DefaultEye,field);
            Assert.AreEqual(l.Bell,mirrored.Bell,"LayoutFor takes the bell's spot as given");
            // Right water hand (+1): at least BellVaneClear right of the vane; left water hand: as far left of it.
            float rightHand=EmberdeepRimHud.BellOver(run,1).x,leftHand=EmberdeepRimHud.BellOver(run,-1).x;
            Assert.GreaterOrEqual(rightHand,vane+EmberdeepRimHud.BellVaneClear-1e-4f);Assert.LessOrEqual(leftHand,vane-EmberdeepRimHud.BellVaneClear+1e-4f);
            // Every level, measured from the seat: EmberdeepHeadsetTest2PlayTests.BellNeverHidesTheWeathervaneFromTheSeat.
            Assert.AreEqual(EmberdeepRimHud.DefaultField,EmberdeepRimHud.BellOver(null));
        }
    }
}

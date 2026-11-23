using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    // Headset test 2 (2026-11-22): a fingertip tap pets a sheep at once (EmberdeepSheepInteraction.Tap). The stroke pet,
    // the cooldown and grabbing keep their rules; a tap never starts a grab and a grab never counts as a tap.
    public sealed class EmberdeepSheepTapTests
    {
        const float Dt=.05f;
        static FireToolManipulation.Hand Hand(Vector3 at,float closure)=>new(){Valid=true,HasGripFrame=true,GripRotation=Quaternion.Euler(180,0,0),Palm=new Pose(at,Quaternion.identity),Contact=at,Closure=closure};
        static FireRun Pasture()=>FireRun.CampScene(new[]{new Vector3(0,0,-50)},new[]{Vector3.zero}); // one Rescued sheep: pettable, never held
        static FireRun Board(){var run=new FireRun(true);run.StartStage();return run;}                 // Waiting sheep: pettable and holdable
        static GameObject Arena(float size){var arena=new GameObject("Sheep tap arena");arena.transform.localScale=Vector3.one*FireMiniature.Scale*size;return arena;}
        static Vector3 Wool(Transform arena,FireRun.Animal a)=>arena.TransformPoint(EmberdeepSheepInteraction.Center(a));
        static void Step(EmberdeepSheepInteraction pet,FireRun run,Transform arena,FireToolManipulation.Hand h,Vector3? tip)
        {
            pet.Step(run,arena,h,default,Dt,tip,null);
            foreach(var a in run.Animals)a.PetCooldown=Mathf.Max(0,a.PetCooldown-Dt);
        }
        // A pointing hand: the palm contact sits ~8.5 cm behind the index tip.
        static FireToolManipulation.Hand Pointing(Vector3 tip)=>Hand(tip+new Vector3(0,.03f,-.08f),.62f);

        [Test] public void AFingertipTapPetsAtOnce()
        {
            var arena=Arena(1);
            try
            {
                var run=Pasture();var a=run.Animals[0];var pet=new EmberdeepSheepInteraction();int pets=0;pet.Petted+=_=>pets++;
                var wool=Wool(arena.transform,a);var outside=wool+new Vector3(0,0,-.07f);var inside=wool+new Vector3(0,.01f,-.02f);
                for(int i=0;i<3;i++)Step(pet,run,arena.transform,Pointing(outside),outside);
                Assert.Zero(pets);
                Step(pet,run,arena.transform,Pointing(inside),inside);
                Assert.AreEqual(1,pets,"One frame of a pointing fingertip on the wool is a pet");Assert.AreEqual(1,pet.Taps);
                Assert.Greater(a.Affection,0);Assert.Greater(a.PetCooldown,0);Assert.False(pet.OwnsHand(0));
            }
            finally{Object.DestroyImmediate(arena);}
        }

        [Test] public void ASlowHoverDoesNotPetTwiceWithinTheCooldownAndARestingTipNeverRepets()
        {
            var arena=Arena(1);
            try
            {
                var run=Pasture();var a=run.Animals[0];var pet=new EmberdeepSheepInteraction();int pets=0;pet.Petted+=_=>pets++;
                var wool=Wool(arena.transform,a);
                // Approach slowly (10 cm/s) from 8 cm out to 3 cm: the entry pets once.
                for(int i=0;i<=10;i++){var tip=wool+new Vector3(0,0,-.08f+i*.005f);Step(pet,run,arena.transform,Pointing(tip),tip);}
                Assert.AreEqual(1,pets);
                // Hover on the wool's edge (in and out every frame, jitter), then drift out past TapRearm and back in: all
                // ~.5 s after the pet, inside its cooldown.
                var edgeIn=wool+new Vector3(0,0,-(EmberdeepSheepInteraction.PetRadius-.002f));var edgeOut=wool+new Vector3(0,0,-(EmberdeepSheepInteraction.PetRadius+.002f));
                for(int i=0;i<4;i++){var tip=i%2==0?edgeOut:edgeIn;Step(pet,run,arena.transform,Pointing(tip),tip);}
                for(int i=0;i<3;i++)Step(pet,run,arena.transform,Pointing(edgeOut),edgeOut);
                Assert.Greater(a.PetCooldown,.1f,"still cooling down");
                Step(pet,run,arena.transform,Pointing(edgeIn),edgeIn);
                Assert.AreEqual(1,pets,"A hover inside the cooldown is still one pet");
                // Rest the tip in the wool well past the cooldown: no new entry, no new pet.
                for(int i=0;i<40;i++)Step(pet,run,arena.transform,Pointing(edgeIn),edgeIn);
                Assert.AreEqual(1,pets,"A resting fingertip never pets again");
                // Lift off for longer than TapRearm, tap again: a second pet.
                for(int i=0;i<3;i++)Step(pet,run,arena.transform,Pointing(edgeOut+Vector3.back*.03f),null);
                Step(pet,run,arena.transform,Pointing(edgeIn),edgeIn);
                Assert.AreEqual(2,pets);Assert.AreEqual(2,pet.Taps);
            }
            finally{Object.DestroyImmediate(arena);}
        }

        [Test] public void AClosingFistGrabsAndDoesNotPet()
        {
            var arena=Arena(1);
            try
            {
                // 1: an open hand comes down on the sheep (tips in the wool) and closes.
                var run=Board();var a=run.Animals[0];Assert.True(FireRun.CanHold(a));
                var pet=new EmberdeepSheepInteraction();int pets=0;pet.Petted+=_=>pets++;
                var wool=Wool(arena.transform,a);var far=wool+new Vector3(0,.12f,-.12f);
                for(int i=0;i<3;i++)Step(pet,run,arena.transform,Hand(far,.05f),null);
                var palm=wool+Vector3.up*.03f;var tip=wool+new Vector3(0,0,.025f);
                foreach(float closure in new[]{.08f,.3f,.5f,.7f,.9f,.9f})Step(pet,run,arena.transform,Hand(palm,closure),tip);
                Assert.True(pet.OwnsHand(0),"The closing fist holds the sheep");Assert.AreEqual(0,a.HeldBy);
                Assert.Zero(pets,"...and it was not a pet");Assert.Zero(pet.Taps);Assert.AreEqual(0,a.Affection);
                // 2: a half-closed hand whose tips reach the wool with the palm already on the sheep: still a grab, not a tap.
                run=Board();a=run.Animals[0];pet=new EmberdeepSheepInteraction();pets=0;pet.Petted+=_=>pets++;wool=Wool(arena.transform,a);
                for(int i=0;i<3;i++)Step(pet,run,arena.transform,Hand(wool+new Vector3(0,.12f,-.12f),.05f),null);
                palm=wool+new Vector3(0,.035f,-.02f);tip=wool+new Vector3(0,0,.02f);
                foreach(float closure in new[]{.32f,.5f,.7f,.9f})Step(pet,run,arena.transform,Hand(palm,closure),tip);
                Assert.True(pet.OwnsHand(0));Assert.Zero(pets);Assert.Zero(pet.Taps);
            }
            finally{Object.DestroyImmediate(arena);}
        }

        [Test] public void ATapNeverTurnsIntoAGrab()
        {
            var arena=Arena(1);
            try
            {
                var run=Board();var a=run.Animals[0];var pet=new EmberdeepSheepInteraction();int pets=0;pet.Petted+=_=>pets++;
                var wool=Wool(arena.transform,a);var outside=wool+new Vector3(0,0,-.07f);var inside=wool+new Vector3(0,.01f,-.02f);
                for(int i=0;i<3;i++)Step(pet,run,arena.transform,Hand(outside+Vector3.up*.1f,.05f),null); // opened first: a grab is armed
                Step(pet,run,arena.transform,Pointing(outside),outside);Step(pet,run,arena.transform,Pointing(outside),outside);
                Step(pet,run,arena.transform,Pointing(inside),inside);
                Assert.AreEqual(1,pets,"The tap pets a sheep that could be held");
                // The pointing hand (closure above the grab threshold) slides its palm onto the sheep: no grab from a tap.
                for(int i=0;i<8;i++)Step(pet,run,arena.transform,Hand(wool+Vector3.up*.03f,.62f),inside);
                Assert.False(pet.OwnsHand(0),"A tap never starts a grab");Assert.AreEqual(-1,a.HeldBy);
                // Opening and closing again is a real grab.
                Step(pet,run,arena.transform,Hand(wool+Vector3.up*.03f,.1f),inside);
                foreach(float closure in new[]{.5f,.7f,.9f})Step(pet,run,arena.transform,Hand(wool+Vector3.up*.03f,closure),inside);
                Assert.True(pet.OwnsHand(0));Assert.AreEqual(1,pets);
            }
            finally{Object.DestroyImmediate(arena);}
        }

        [Test] public void TapsFollowTheSheepSizeAndThePetGate()
        {
            var board=Arena(1);var camp=Arena(2.03f);
            try
            {
                // Camp sheep are ~2x a board sheep (EmberdeepCampPlay.PetReach): 7 cm from the centre is on the wool there.
                var run=Pasture();var pet=new EmberdeepSheepInteraction{Reach=2.03f,PetClosure=EmberdeepCampPlay.CampPetClosure};int pets=0;pet.Petted+=_=>pets++;
                var wool=Wool(camp.transform,run.Animals[0]);var outside=wool+new Vector3(0,0,-.12f);var inside=wool+new Vector3(0,0,-.07f);
                for(int i=0;i<3;i++)Step(pet,run,camp.transform,Pointing(outside),outside);
                Step(pet,run,camp.transform,Pointing(inside),inside);Assert.AreEqual(1,pets,"Camp tap");
                // At camp a relaxed open hand taps too (camp sheep are never held).
                run=Pasture();pet=new EmberdeepSheepInteraction{Reach=2.03f,PetClosure=EmberdeepCampPlay.CampPetClosure};pets=0;pet.Petted+=_=>pets++;
                wool=Wool(camp.transform,run.Animals[0]);
                for(int i=0;i<3;i++)Step(pet,run,camp.transform,Hand(wool+new Vector3(0,.04f,-.2f),.15f),null);
                Step(pet,run,camp.transform,Hand(wool+new Vector3(0,.04f,-.12f),.15f),wool+new Vector3(0,.01f,-.05f));Assert.AreEqual(1,pets,"Open-hand fingertip tap at camp");
                // The board's radius: the same 7 cm is off a board sheep.
                run=Pasture();pet=new EmberdeepSheepInteraction();pets=0;pet.Petted+=_=>pets++;wool=Wool(board.transform,run.Animals[0]);
                for(int i=0;i<3;i++)Step(pet,run,board.transform,Pointing(wool+new Vector3(0,0,-.12f)),wool+new Vector3(0,0,-.12f));
                Step(pet,run,board.transform,Pointing(wool+new Vector3(0,0,-.07f)),wool+new Vector3(0,0,-.07f));Assert.Zero(pets);
                // Gated off (a level before every fire is out): the tap is spent, and a resting tip does not pet once allowed.
                run=Pasture();pet=new EmberdeepSheepInteraction{PetAllowed=false};pets=0;pet.Petted+=_=>pets++;wool=Wool(board.transform,run.Animals[0]);
                var tipOut=wool+new Vector3(0,0,-.07f);var tipIn=wool+new Vector3(0,0,-.02f);
                for(int i=0;i<3;i++)Step(pet,run,board.transform,Pointing(tipOut),tipOut);
                Step(pet,run,board.transform,Pointing(tipIn),tipIn);Assert.Zero(pets,"No petting while gated");
                pet.PetAllowed=true;for(int i=0;i<5;i++)Step(pet,run,board.transform,Pointing(tipIn),tipIn);Assert.Zero(pets);
                for(int i=0;i<3;i++)Step(pet,run,board.transform,Pointing(tipOut),tipOut);
                Step(pet,run,board.transform,Pointing(tipIn),tipIn);Assert.AreEqual(1,pets,"A fresh tap once allowed");
            }
            finally{Object.DestroyImmediate(board);Object.DestroyImmediate(camp);}
        }
    }
}

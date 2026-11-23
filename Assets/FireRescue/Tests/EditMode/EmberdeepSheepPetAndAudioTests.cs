using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    public sealed class EmberdeepSheepPetAndAudioTests
    {
        static FireToolManipulation.Hand Hand(Vector3 at,float closure=0)=>new(){Valid=true,HasGripFrame=true,GripRotation=Quaternion.Euler(180,0,0),Palm=new Pose(at,Quaternion.identity),Contact=at,Closure=closure};
        static void Clear(FireRun run){foreach(var a in run.Animals)run.ApplyAnimalWater(a.Position,100,.6f);foreach(var t in run.Trees)if(t.State==TreeState.Burning)run.ApplyWater(t.Position,1000,.1f);}
        static void Tick(FireRun run,float seconds){for(int i=0;i<Mathf.CeilToInt(seconds/.05f);i++)run.Tick(.05f,false);}
        static FireRun RescuedFlock()
        {
            var run=new FireRun();run.StartStage();Clear(run);Tick(run,20);
            Assert.AreEqual(run.Animals.Length,run.AnimalsSaved,"Every sheep crossed to safety");return run;
        }
        static GameObject Arena(){var arena=new GameObject("Sheep arena");arena.transform.localScale=Vector3.one*FireMiniature.Scale;return arena;}

        [Test] public void RescuedSheepStayOnTheSafeBankAtSeparatePastureSpots()
        {
            var run=RescuedFlock();
            foreach(var a in run.Animals)
            {
                Assert.AreEqual(AnimalState.Rescued,a.State);
                Assert.Less(Vector3.Distance(a.Position,a.Pasture),.01f,"Rescued sheep amble to their pasture spot");
                Assert.Less(a.Position.z,1.62f,"Pasture is on the creek's near bank, so rescued sheep have crossed the water");
                Assert.Greater(a.Position.z,-.3f,"and still on the board");Assert.LessOrEqual(Mathf.Abs(a.Position.x),FireRun.PastureMaxX);
            }
            for(int i=0;i<run.Animals.Length;i++)for(int j=i+1;j<run.Animals.Length;j++)
                Assert.Greater(Vector3.Distance(run.Animals[i].Pasture,run.Animals[j].Pasture),2*FireRun.AnimalRadius,"No two rescued sheep stand inside each other");
        }

        [Test] public void RescuedSheepCanBePettedButNeverPickedUp()
        {
            var arena=Arena();
            try
            {
                var run=RescuedFlock();var a=run.Animals[0];int coins=run.Coins;
                var interaction=new EmberdeepSheepInteraction();var petted=new List<int>();interaction.Petted+=petted.Add;
                var h=Hand(arena.transform.TransformPoint(EmberdeepSheepInteraction.Center(a)));
                for(int i=0;i<5;i++)interaction.Step(run,arena.transform,h,default,.05f);
                Assert.Greater(a.Affection,0);CollectionAssert.AreEqual(new[]{0},petted);
                h.Closure=.7f;for(int i=0;i<6;i++)interaction.Step(run,arena.transform,h,default,.05f);
                Assert.False(interaction.OwnsHand(0),"A rescued sheep is never grabbed");
                Assert.AreEqual(AnimalState.Rescued,a.State);Assert.AreEqual(-1,a.HeldBy);
                Tick(run,3);Assert.AreEqual(coins,run.Coins,"Petting and closing a hand never re-pays a rescue");
            }
            finally{Object.DestroyImmediate(arena);}
        }

        [Test] public void PettingRepeatsAfterTheCooldownOnlyWithAStroke()
        {
            var arena=Arena();
            try
            {
                var run=RescuedFlock();var a=run.Animals[1];
                var interaction=new EmberdeepSheepInteraction();int pets=0;interaction.Petted+=_=>pets++;
                var center=arena.transform.TransformPoint(EmberdeepSheepInteraction.Center(a));var h=Hand(center);
                for(int i=0;i<5;i++)interaction.Step(run,arena.transform,h,default,.05f);
                Assert.AreEqual(1,pets);
                for(int i=0;i<40;i++){run.Tick(.05f,false);interaction.Step(run,arena.transform,h,default,.05f);}
                Assert.AreEqual(1,pets,"A hand resting on the wool does not boing on a timer");
                Assert.LessOrEqual(a.PetCooldown,0);
                for(int i=0;i<6;i++)
                {
                    h.Contact=h.Palm.position=center+Vector3.right*(i%2==0?.012f:-.012f);
                    run.Tick(.05f,false);interaction.Step(run,arena.transform,h,default,.05f);
                }
                Assert.AreEqual(2,pets,"A stroke along the sheep pets again");
                // Cooldown: an immediate stroke right after a pet does nothing.
                for(int i=0;i<4;i++)
                {
                    h.Contact=h.Palm.position=center+Vector3.right*(i%2==0?.012f:-.012f);
                    run.Tick(.05f,false);interaction.Step(run,arena.transform,h,default,.05f);
                }
                Assert.AreEqual(2,pets,"Cooldown holds the next pet");
            }
            finally{Object.DestroyImmediate(arena);}
        }

        [Test] public void PreparingPhaseSheepCanBePettedAndTheCooldownRuns()
        {
            var arena=Arena();
            try
            {
                var run=new FireRun(true);Assert.True(run.StartStage(true));Assert.AreEqual(FirePhase.Preparing,run.Phase);
                var a=run.Animals[0];var interaction=new EmberdeepSheepInteraction();int pets=0;interaction.Petted+=_=>pets++;
                var center=arena.transform.TransformPoint(EmberdeepSheepInteraction.Center(a));var h=Hand(center);
                for(int i=0;i<5;i++){run.TickPreparedAnimals(.05f);interaction.Step(run,arena.transform,h,default,.05f);}
                Assert.AreEqual(1,pets);Assert.Greater(a.Affection,0);
                for(int i=0;i<20;i++)run.TickPreparedAnimals(.05f);
                Assert.LessOrEqual(a.PetCooldown,0,"Preparation ticks the pet cooldown");
                for(int i=0;i<6;i++)
                {
                    h.Contact=h.Palm.position=center+Vector3.right*(i%2==0?.012f:-.012f);
                    run.TickPreparedAnimals(.05f);interaction.Step(run,arena.transform,h,default,.05f);
                }
                Assert.AreEqual(2,pets);
            }
            finally{Object.DestroyImmediate(arena);}
        }

        [Test] public void ScriptedPetUsesTheSameCooldownAndSkipsBurningSheep()
        {
            var run=new FireRun();run.StartStage();var a=run.Animals[0];
            Assert.True(EmberdeepSheepInteraction.TryPet(a));Assert.AreEqual(FireRun.PetAffection,a.Affection);
            Assert.False(EmberdeepSheepInteraction.TryPet(a),"Cooldown");
            a.PetCooldown=0;a.OnFire=true;Assert.False(EmberdeepSheepInteraction.TryPet(a),"A burning sheep needs water, not a pat");
        }

        [Test] public void HopCrouchesBouncesTwiceWithOvershootAndSettles()
        {
            Assert.False(EmberdeepSheepHop.Evaluate(-.01f,out var lift,out var stretch));Assert.Zero(lift);Assert.AreEqual(1,stretch);
            Assert.False(EmberdeepSheepHop.Evaluate(EmberdeepSheepHop.Duration+.01f,out lift,out stretch));Assert.AreEqual(1,stretch);
            EmberdeepSheepHop.Evaluate(EmberdeepSheepHop.Crouch*.99f,out lift,out stretch);Assert.Less(stretch,.82f,"Crouch before take-off");
            float first=EmberdeepSheepHop.Crouch+EmberdeepSheepHop.FirstAir*.5f;
            EmberdeepSheepHop.Evaluate(first,out lift,out _);Assert.AreEqual(EmberdeepSheepHop.FirstHeight,lift,.01f);
            float land=EmberdeepSheepHop.Crouch+EmberdeepSheepHop.FirstAir+EmberdeepSheepHop.Land*.5f;
            EmberdeepSheepHop.Evaluate(land,out lift,out stretch);Assert.Zero(lift);Assert.Less(stretch,.75f,"Squash on landing");
            float second=land+EmberdeepSheepHop.Land*.5f+EmberdeepSheepHop.SecondAir*.5f;
            EmberdeepSheepHop.Evaluate(second,out lift,out _);Assert.AreEqual(EmberdeepSheepHop.SecondHeight,lift,.01f);Assert.Less(lift,EmberdeepSheepHop.FirstHeight);
            float settle=EmberdeepSheepHop.Duration-EmberdeepSheepHop.Settle;bool overshoot=false;
            for(float t=settle;t<EmberdeepSheepHop.Duration;t+=.01f){EmberdeepSheepHop.Evaluate(t,out lift,out stretch);overshoot|=stretch>1.02f;Assert.Zero(lift);}
            Assert.True(overshoot,"Wobble overshoots rest once");
            EmberdeepSheepHop.Evaluate(EmberdeepSheepHop.Duration-.001f,out _,out stretch);Assert.AreEqual(1,stretch,.01f);
            var scale=EmberdeepSheepHop.Scale(.72f);Assert.AreEqual(1,scale.x*scale.y*scale.z,.01f,"Squash keeps volume");
        }

        [Test] public void BurningFlockAlarmsOnceThenRemindsThroughOneSparseVoice()
        {
            var go=new GameObject("Sheep audio test");
            try
            {
                var run=new FireRun();run.StartStage();Assert.AreEqual(3,run.Animals.Length);
                var audio=go.AddComponent<EmberdeepSheepAudio>();audio.Initialize(run);
                Assert.AreEqual(2,go.GetComponentsInChildren<AudioSource>().Length,"One flock voice plus one pet voice");
                Assert.NotNull(audio.AlarmClip);Assert.NotNull(audio.PetClip);
                var reminderTimes=new List<float>();float time=0;
                void Advance(float seconds)
                {
                    for(int i=0;i<Mathf.RoundToInt(seconds/.05f);i++)
                    {
                        foreach(var a in run.Animals)a.AnimationTime+=.05f;
                        int before=audio.RemindersPlayed;audio.Tick();time+=.05f;
                        if(audio.RemindersPlayed>before)reminderTimes.Add(time);
                    }
                }
                Advance(1);Assert.Zero(audio.AlarmsPlayed);Assert.Zero(audio.CalmPlayed,"Calm sheep stay quiet at the start");
                run.Animals[0].OnFire=true;Advance(.05f);Assert.AreEqual(1,audio.AlarmsPlayed);Assert.AreEqual(0,audio.Speaker);float alarmAt=time;
                run.Animals[1].OnFire=run.Animals[2].OnFire=true;Advance(.05f);
                Assert.AreEqual(1,audio.AlarmsPlayed,"Sheep catching fire together share one alarm");
                Advance(30);
                Assert.Zero(audio.CalmPlayed,"No calm bleats while any sheep burns");
                Assert.GreaterOrEqual(reminderTimes.Count,4,"Reminders keep saying a sheep is on fire");
                Assert.LessOrEqual(reminderTimes.Count,Mathf.FloorToInt(30/EmberdeepSheepAudio.ReminderFlockGap)+1);
                for(int i=1;i<reminderTimes.Count;i++)
                    Assert.GreaterOrEqual(reminderTimes[i]-reminderTimes[i-1],EmberdeepSheepAudio.ReminderFlockGap-.06f,"One voice, spaced across the flock");
                Assert.GreaterOrEqual(reminderTimes[0]-alarmAt,EmberdeepSheepAudio.FirstReminder-.06f,"The alarm is not followed by an instant reminder");
                foreach(var a in run.Animals)a.OnFire=false;
                int reminders=audio.RemindersPlayed;Advance(60);
                Assert.AreEqual(reminders,audio.RemindersPlayed);
                Assert.That(audio.CalmPlayed,Is.InRange(1,Mathf.CeilToInt(60/(EmberdeepSheepAudio.CalmInterval-EmberdeepSheepAudio.CalmJitter))),"Calm flock is near-silent");
                run.Animals[1].OnFire=true;Advance(.05f);Assert.AreEqual(2,audio.AlarmsPlayed,"A new fire later alarms again");
                run.Animals[1].OnFire=false;Advance(1);run.Animals[1].OnFire=true;Advance(.05f);
                Assert.AreEqual(2,audio.AlarmsPlayed,"The same sheep re-igniting soon after only gets reminders");
                audio.Pet(Vector3.zero);Assert.AreEqual(1,audio.PetsPlayed);
            }
            finally{Object.DestroyImmediate(go);}
        }
    }
}

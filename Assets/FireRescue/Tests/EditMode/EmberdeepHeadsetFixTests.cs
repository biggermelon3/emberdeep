using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    // Fixes from the owner's first headset test (2026-11-22): pump on the free-hand side, strike grades for the one
    // rhythm cue and its sounds, gem fill tone + full chime, the start-bell alarm, no petting before every fire is out,
    // camp sheep kept on the island's moss, and reliable petting at camp scale.
    public sealed class EmberdeepHeadsetFixTests
    {
        static FireToolManipulation.Hand Hand(Vector3 at,float closure=0)=>new(){Valid=true,HasGripFrame=true,GripRotation=Quaternion.Euler(180,0,0),Palm=new Pose(at,Quaternion.identity),Contact=at,Closure=closure};
        static FireRun Pasture()=>FireRun.CampScene(new[]{new Vector3(0,0,-50)},new[]{Vector3.zero});
        static GameObject Arena(float size){var arena=new GameObject("Sheep arena");arena.transform.localScale=Vector3.one*FireMiniature.Scale*size;return arena;}
        static int Pets(EmberdeepSheepInteraction interaction,FireRun run,Transform arena,FireToolManipulation.Hand h,int steps)
        {
            int pets=0;void Count(int _)=>pets++;interaction.Petted+=Count;
            for(int i=0;i<steps;i++){interaction.Step(run,arena,h,default,.05f);foreach(var a in run.Animals){a.PetCooldown=Mathf.Max(0,a.PetCooldown-.05f);}}
            interaction.Petted-=Count;return pets;
        }

        [Test] public void ThePumpStandsOnTheFreeHandSide()
        {
            Assert.False(EmberdeepWaterTuning.PumpLeft(true),"Left water hand: pump on the right");
            Assert.True(EmberdeepWaterTuning.PumpLeft(false));
            Assert.AreEqual(1,EmberdeepWaterTuning.PumpSide(true));Assert.AreEqual(-1,EmberdeepWaterTuning.PumpSide(false));
        }

        // Headset test 4 (2026-11-22): one centred row on the board's front lip, whichever hand holds the water, clear of the
        // pump cap, the bell, the rim's token and hourglass for every slot count.
        [Test] public void TheTrayIsOneCentredRowOnTheFrontLipClearOfThePumpBellAndToken()
        {
            var board=EmberdeepRimHud.DefaultBoard;
            float reach=(EmberdeepWaterTuning.StrikeRadius+EmberdeepPieceGrasp.GrabRadius)/FireMiniature.Scale;
            var bell=EmberdeepRimHud.DefaultField;
            foreach(float pumpSide in new[]{1f,-1f})
            {
                var c=EmberdeepPieceView.TrayCentre(board,pumpSide);
                Assert.AreEqual(board.center.x,c.x,1e-5f,"Centred across the board");
                Assert.AreEqual(board.min.z+EmberdeepPieceView.TrayInset+EmberdeepPieceView.TrayDepth*.5f,c.z,1e-5f,"On the front lip: the pump needs no slide back");
                Assert.Less(c.z+EmberdeepPieceView.TrayDepth*.5f,EmberdeepBoardLayout.FrontRowZ-.4f,"In front of the front row");
                var pump=new Vector2(pumpSide*EmberdeepWaterTuning.PumpSideX,EmberdeepWaterTuning.PumpForward);
                // The rim's token and hourglass stand on the water-hand front corner (opposite the pump).
                var rim=EmberdeepRimHud.LayoutFor(board,-pumpSide);
                for(int n=1;n<=EmberdeepPieceView.MaxSlots;n++)for(int s=0;s<n;s++)foreach(float free in new[]{1f,-1f})
                {
                    var p=c+EmberdeepPieceView.SlotLocal(s,n,free);var xz=new Vector2(p.x,p.z);
                    Assert.AreEqual(c.z,p.z,1e-5f,"One row");
                    Assert.Greater(Vector2.Distance(xz,pump),reach,"No slot within a hammering fist's grab reach of the pump cap");
                    Assert.Greater(Vector2.Distance(xz,bell),reach,"...nor of the bell");
                    Assert.Greater(Vector2.Distance(xz,new Vector2(rim.Token.x,rim.Token.z)),reach,"...nor of the token");
                    Assert.Less(Mathf.Abs(p.x-c.x)+.37f,EmberdeepPieceView.TrayWidth*.5f,"Inside the widest tray");
                }
                Assert.Greater(Mathf.Abs(rim.Hourglass.x-c.x)-EmberdeepPieceView.TrayWidth*.5f,.2f,"The widest tray stops short of the hourglass");
            }
            // Slot 0 on the water-hand side; the last (the plane token) on the free-hand side; SlotPitch apart.
            Assert.Greater(EmberdeepPieceView.SlotLocal(4,5,1).x,EmberdeepPieceView.SlotLocal(0,5,1).x,"left water hand: last slot on the right");
            Assert.Less(EmberdeepPieceView.SlotLocal(4,5,-1).x,EmberdeepPieceView.SlotLocal(0,5,-1).x,"right water hand: last slot on the left");
            Assert.AreEqual(EmberdeepPieceView.SlotPitch*4,EmberdeepPieceView.SlotLocal(4,5,1).x-EmberdeepPieceView.SlotLocal(0,5,1).x,1e-5f);
            Assert.AreEqual(0,EmberdeepPieceView.SlotLocal(1,3,1).x,1e-6f,"an odd row is centred on its middle slot");
            Assert.AreEqual(6,EmberdeepPieceView.MaxSlots,"store build: 3 towers + 2 SLOT = 5; generated grids add an excavator or the plane");
        }

        [Test] public void StrikesAreGradedPlainOnBeatOrOffBeat()
        {
            var r=new EmberdeepPumpRhythm();double t=100,beat=EmberdeepWaterTuning.BeatSeconds;
            Assert.AreEqual(EmberdeepPumpGrade.Plain,r.StrikeGraded(t,true),"The first strike only starts the beat");
            Assert.AreEqual(EmberdeepPumpGrade.OnBeat,r.StrikeGraded(t+beat,true));
            Assert.AreEqual(EmberdeepPumpGrade.OffBeat,r.StrikeGraded(t+beat*1.5,true));
            Assert.AreEqual(EmberdeepPumpGrade.Plain,r.StrikeGraded(t+beat*2,false),"Rhythm toggle off: every strike is plain");
            Assert.AreEqual(EmberdeepPumpGrade.Plain,r.StrikeGraded(t+100,true),"A long pause starts a new beat");
        }

        [Test] public void GemFillToneRisesWithTheLevelAndChimesOnceWhenFull()
        {
            var go=new GameObject("Water audio");
            try
            {
                var audio=go.AddComponent<EmberdeepWaterAudio>();audio.Initialize();
                audio.Fill(.2f,.016f);Assert.Zero(audio.FillToneVolume,"The first sample is a snap");
                float fill=.2f;for(int i=0;i<10;i++){fill+=.02f;audio.Fill(fill,.016f);}
                Assert.Greater(audio.FillToneVolume,0,"Filling fast: the tone plays");
                float low=audio.FillTonePitch;
                for(int i=0;i<10;i++){fill+=.02f;audio.Fill(fill,.016f);}
                Assert.Greater(audio.FillTonePitch,low,"Its pitch climbs with the level");
                Assert.Zero(audio.FullChimes);
                while(fill<1){fill=Mathf.Min(1,fill+.02f);audio.Fill(fill,.016f);}
                Assert.AreEqual(1,audio.FullChimes,"Full: one chime");
                for(int i=0;i<30;i++)audio.Fill(1,.016f);
                Assert.AreEqual(1,audio.FullChimes,"Staying full does not chime again");
                Assert.AreEqual(0,audio.FillToneVolume,1e-4f,"A full, still gem is silent");
                // A level start snaps the gem full: no chime.
                audio.ResetFill();audio.Fill(.1f,.016f);audio.Fill(1,.016f);Assert.AreEqual(1,audio.FullChimes);
                // Passive refill (2.5/s of 40) is only a faint shimmer next to pumping.
                audio.ResetFill();audio.Fill(.5f,.016f);
                for(int i=0;i<60;i++)audio.Fill(.5f+i*.001f,.016f);
                Assert.Less(audio.FillToneVolume,EmberdeepWaterAudio.FillVolume*.25f);
                audio.Strike(EmberdeepPumpGrade.OnBeat);audio.Strike(EmberdeepPumpGrade.OffBeat);audio.Strike(EmberdeepPumpGrade.Plain);
                Assert.AreEqual(1,audio.Chimes);Assert.AreEqual(1,audio.Creaks);
            }
            finally{Object.DestroyImmediate(go);}
        }

        [Test] public void TheStartBellRingsAnAlarmOnTheExistingVoices()
        {
            var go=new GameObject("Feedback");
            try
            {
                var feedback=go.AddComponent<FireFeedback>();feedback.Initialize();feedback.Alarm();
                Assert.AreEqual(1,feedback.Alarms);Assert.AreEqual(4,go.GetComponentsInChildren<AudioSource>().Length,"Still four fixed voices");
            }
            finally{Object.DestroyImmediate(go);}
        }

        [Test] public void PettingInALevelWaitsUntilEveryFireIsOut()
        {
            Assert.False(FireRescueGame.PetPhase(FirePhase.Ready));Assert.False(FireRescueGame.PetPhase(FirePhase.Preparing),"Not in Plan");
            Assert.False(FireRescueGame.PetPhase(FirePhase.Fighting),"Not in the fight");Assert.False(FireRescueGame.PetPhase(FirePhase.Lost));
            Assert.True(FireRescueGame.PetPhase(FirePhase.Cleared));Assert.True(FireRescueGame.PetPhase(FirePhase.Won));
            var arena=Arena(1);
            try
            {
                var run=Pasture();var interaction=new EmberdeepSheepInteraction{PetAllowed=false};
                var h=Hand(arena.transform.TransformPoint(EmberdeepSheepInteraction.Center(run.Animals[0])));
                Assert.Zero(Pets(interaction,run,arena.transform,h,10),"Gated off: no pet");
                interaction.PetAllowed=true;Assert.AreEqual(1,Pets(interaction,run,arena.transform,h,5));
            }
            finally{Object.DestroyImmediate(arena);}
        }

        [Test] public void CampScaleSheepPetFromARestingPalmOrARelaxedHand()
        {
            var board=Arena(1);var camp=Arena(2.03f);
            try
            {
                var run=Pasture();
                // The palm's contact point (wrist-knuckle midpoint) rests ~6 cm above a camp sheep's centre on its wool.
                var resting=Hand(camp.transform.TransformPoint(EmberdeepSheepInteraction.Center(run.Animals[0]))+Vector3.up*.06f);
                Assert.Zero(Pets(new EmberdeepSheepInteraction(),run,camp.transform,resting,10),"Board radii miss a camp-size sheep");
                Assert.AreEqual(1,Pets(new EmberdeepSheepInteraction{Reach=2.03f},run,camp.transform,resting,10),"Camp reach: the pet lands");
                run=Pasture();
                var relaxed=Hand(camp.transform.TransformPoint(EmberdeepSheepInteraction.Center(run.Animals[0]))+Vector3.up*.03f,.35f);
                Assert.Zero(Pets(new EmberdeepSheepInteraction{Reach=2.03f},run,camp.transform,relaxed,10),"The board's open-hand gate");
                Assert.AreEqual(1,Pets(new EmberdeepSheepInteraction{Reach=2.03f,PetClosure=EmberdeepCampPlay.CampPetClosure},run,camp.transform,relaxed,10),
                    "A relaxed, slightly cupped hand strokes a camp sheep");
                // The board itself is unchanged: the same hand 6 cm above a board sheep is not a pet.
                run=Pasture();
                var above=Hand(board.transform.TransformPoint(EmberdeepSheepInteraction.Center(run.Animals[0]))+Vector3.up*.06f);
                Assert.Zero(Pets(new EmberdeepSheepInteraction(),run,board.transform,above,10));
            }
            finally{Object.DestroyImmediate(board);Object.DestroyImmediate(camp);}
        }

        [Test] public void AShortTrackingBlipDoesNotRestartAStroke()
        {
            var arena=Arena(2.03f);
            try
            {
                var run=Pasture();var interaction=new EmberdeepSheepInteraction{Reach=2.03f};int pets=0;interaction.Petted+=_=>pets++;
                var h=Hand(arena.transform.TransformPoint(EmberdeepSheepInteraction.Center(run.Animals[0])));
                interaction.Step(run,arena.transform,h,default,.05f);interaction.Step(run,arena.transform,h,default,.05f);
                interaction.Step(run,arena.transform,default,default,.05f); // one frame of low-confidence fingers
                interaction.Step(run,arena.transform,h,default,.05f);interaction.Step(run,arena.transform,h,default,.05f);
                Assert.AreEqual(1,pets,".2 s of contact across a blip is one pet");
                // A real loss (longer than PetGrace) still counts as leaving: coming back pets again after the cooldown.
                for(int i=0;i<6;i++)interaction.Step(run,arena.transform,default,default,.05f);
                run.Animals[0].PetCooldown=0;
                for(int i=0;i<5;i++)interaction.Step(run,arena.transform,h,default,.05f);
                Assert.AreEqual(2,pets);
            }
            finally{Object.DestroyImmediate(arena);}
        }

        [Test] public void CampSheepStayOnTheIslandsMoss()
        {
            Assert.True(EmberdeepCampPlay.OnSheepWalk(EmberdeepCampPlay.SheepHome));Assert.True(EmberdeepCampPlay.OnSheepWalk(EmberdeepCampPlay.LambHome));
            foreach(var p in EmberdeepCampPlay.SheepDash)Assert.True(EmberdeepCampPlay.OnSheepWalk(p),"Dash point "+p);
            // The moss front edge of EDV2_CampGround (island units, raycast 2026-11-22): the walk's front boundary keeps
            // the hooves (+-.12 around a centre) on it everywhere along the front strip.
            var edge=new[]{(0f,-2.72f),(.3f,-2.72f),(.6f,-2.72f),(-.6f,-2.72f),(.75f,-2.62f),(-.75f,-2.62f),(.9f,-2.47f),(-.8f,-2.52f),(-1.1f,-2.4f),(1.1f,-2.4f)};
            var c=EmberdeepCampPlay.SheepWalkCenter;var r=EmberdeepCampPlay.SheepWalkRadii;
            foreach(var (x,mossZ) in edge)
            {
                float front=c.y-r.y*Mathf.Sqrt(1-(x-c.x)*(x-c.x)/(r.x*r.x));
                Assert.GreaterOrEqual(front,mossZ+.12f-.001f,"walk front at x "+x);
                Assert.True(EmberdeepCampPlay.OnSheepWalk(EmberdeepCampPlay.KeepOnIsland(new Vector3(x,0,front-.3f))),"a point past the front clamps back on");
            }
            var inside=new Vector3(.2f,0,-2.3f);Assert.AreEqual(inside,EmberdeepCampPlay.KeepOnIsland(inside),"Inside points stay put");
            var far=new Vector3(3,0,-4);Assert.True(EmberdeepCampPlay.OnSheepWalk(EmberdeepCampPlay.KeepOnIsland(far)));
            var island=new Vector3(.4f,0,-2.5f);
            Assert.Less(Vector3.Distance(island,EmberdeepCampPlay.ToIsland(EmberdeepCampPlay.ToFrame(island,0))),1e-5f);
        }
    }
}

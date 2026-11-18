using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    // Gesture spells: WATER BOLT, RAIN FAN, WATER ORB and the altar basin.
    public sealed class EmberdeepSpellTests
    {
        const float Dt=.02f;
        static EmberdeepSpellHand Hand(Vector3 at,Vector3 normal,Vector3 forward,float curl=0,bool open=true)
        {
            return new EmberdeepSpellHand{Valid=true,Contact=at,Frame=Quaternion.LookRotation(forward,normal),CurlsValid=true,
                Index=curl,Middle=curl,Ring=curl,Little=curl,Open=open && curl<.2f,PointValid=true,
                Wrist=at-forward*.05f,IndexBase=at+forward*.04f,TipMask=31,
                ThumbTip=at+forward*.03f,IndexTip=at+forward*.12f,MiddleTip=at+forward*.12f,RingTip=at+forward*.11f,LittleTip=at+forward*.09f};
        }
        static EmberdeepSpellHand Pointing(Vector3 at,Vector3 forward)
        {
            var hand=Hand(at,Vector3.right,forward,0,false);hand.Middle=.1f;hand.Ring=.9f;hand.Little=.9f;return hand;
        }
        static EmberdeepSpellHand Fist(Vector3 at)=>Hand(at,Vector3.left,Vector3.forward,.95f,false);
        static EmberdeepSpellHand OpenHand(Vector3 at)=>Hand(at,Vector3.left,Vector3.forward);
        static readonly Vector3 Left=new Vector3(-.15f,1,.3f),Right=new Vector3(.15f,1,.3f);

        [Test] public void BoltChargesOnARightFistAndFiresOnceWhenItOpens()
        {
            var bolt=new EmberdeepBoltGesture();var aim=Pointing(Left,Vector3.forward);
            for(int i=0;i<5;i++)bolt.Sample(aim,OpenHand(Right),true,Dt);
            Assert.True(bolt.Aiming);Assert.False(bolt.Charging);
            Assert.That(Vector3.Angle(bolt.Direction,Vector3.forward),Is.LessThan(1));
            for(int i=0;i<25;i++)bolt.Sample(aim,Fist(Right),true,Dt);
            Assert.True(bolt.Charging);Assert.That(bolt.Charge01,Is.EqualTo(.48f).Within(.03f));Assert.False(bolt.Fired);
            bolt.Sample(aim,OpenHand(Right),true,Dt);
            Assert.True(bolt.Fired);Assert.That(bolt.FiredCharge,Is.EqualTo(.48f).Within(.03f));Assert.False(bolt.Charging);
            bolt.Sample(aim,OpenHand(Right),true,Dt);Assert.False(bolt.Fired,"One shot per release");
        }
        [Test] public void BoltNeedsAnObservedOpenHandAndNeverFiresOnTrackingLoss()
        {
            var bolt=new EmberdeepBoltGesture();var aim=Pointing(Left,Vector3.forward);
            for(int i=0;i<50;i++)bolt.Sample(aim,Fist(Right),true,Dt);
            Assert.False(bolt.Charging,"A fist that was never seen open cannot charge");
            bolt.Sample(aim,OpenHand(Right),true,Dt);
            for(int i=0;i<20;i++)bolt.Sample(aim,Fist(Right),true,Dt);
            Assert.True(bolt.Charging);
            for(int i=0;i<10;i++){bolt.Sample(aim,default,true,Dt);Assert.False(bolt.Fired);}
            Assert.True(bolt.Charging,"A brief loss holds the charge");
            for(int i=0;i<30;i++){bolt.Sample(aim,default,true,Dt);Assert.False(bolt.Fired);}
            Assert.False(bolt.Charging,"A long loss cancels without a shot");
            bolt.Sample(aim,OpenHand(Right),true,Dt);Assert.False(bolt.Fired);
        }
        [Test] public void BoltStopsAimingWhenTheLeftHandOpensOrIsDisallowed()
        {
            var bolt=new EmberdeepBoltGesture();
            bolt.Sample(Pointing(Left,Vector3.forward),OpenHand(Right),true,Dt);Assert.True(bolt.Aiming);
            bolt.Sample(OpenHand(Left),OpenHand(Right),true,Dt);Assert.False(bolt.Aiming,"An open palm is not a pointing hand");
            bolt.Sample(Pointing(Left,Vector3.forward),Fist(Right),false,Dt);Assert.False(bolt.Aiming);Assert.False(bolt.Charging);
        }
        [Test] public void FanNeedsBothOpenPalmsForOneSecondAndStopsOnAFist()
        {
            var fan=new EmberdeepFanGesture();var toward=Vector3.forward;
            var left=Hand(Left,Vector3.forward,Vector3.up);var right=Hand(Right,Vector3.forward,Vector3.up);
            for(int i=0;i<40;i++)fan.Sample(left,right,toward,true,Dt);
            Assert.True(fan.Ready);Assert.False(fan.Active);Assert.That(fan.Arm01,Is.EqualTo(.8f).Within(.03f));
            for(int i=0;i<12;i++)fan.Sample(left,right,toward,true,Dt);
            Assert.True(fan.Active);Assert.That(Vector3.Angle(fan.Direction,Vector3.forward),Is.LessThan(1));
            fan.Sample(left,Hand(Right,Vector3.forward,Vector3.up,.9f,false),toward,true,Dt);
            Assert.False(fan.Active,"A visible fist stops the spray at once");Assert.Zero(fan.Arm01);
        }
        [Test] public void FanRejectsStackedHandsAndPalmsTurnedAway()
        {
            var fan=new EmberdeepFanGesture();
            var left=Hand(Left,Vector3.forward,Vector3.up);
            for(int i=0;i<80;i++)fan.Sample(left,Hand(Left+Vector3.right*.06f,Vector3.forward,Vector3.up),Vector3.forward,true,Dt);
            Assert.False(fan.Active,"Palm water stacks the hands; the fan must not start");
            for(int i=0;i<80;i++)fan.Sample(left,Hand(Right,Vector3.down,Vector3.forward),Vector3.forward,true,Dt);
            Assert.False(fan.Active);
        }
        [Test] public void FanToleratesABriefTrackingLossOnly()
        {
            var fan=new EmberdeepFanGesture();
            var left=Hand(Left,Vector3.forward,Vector3.up);var right=Hand(Right,Vector3.forward,Vector3.up);
            for(int i=0;i<60;i++)fan.Sample(left,right,Vector3.forward,true,Dt);
            Assert.True(fan.Active);
            for(int i=0;i<8;i++)fan.Sample(left,default,Vector3.forward,true,Dt);
            Assert.True(fan.Active);
            for(int i=0;i<10;i++)fan.Sample(left,default,Vector3.forward,true,Dt);
            Assert.False(fan.Active);
        }
        static EmberdeepSpellHand Cup()=>Hand(Left,Vector3.up,Vector3.forward,.35f,false);
        static EmberdeepSpellHand Under()=>Hand(Left+Vector3.down*.06f+Vector3.right*.08f,Vector3.up,Vector3.left);
        [Test] public void OrbGrowsWhileTheHiddenRightHandIsLostThenIsHeldAndDropped()
        {
            var orb=new EmberdeepOrbGesture();
            var under=Under();Assert.True(EmberdeepOrbGesture.Under(Cup(),under.IndexTip,0));
            for(int i=0;i<10;i++)orb.Sample(Cup(),under,true,false,Dt);
            Assert.AreEqual(EmberdeepOrbGesture.Stage.Charging,orb.State);
            // The left hand hides the right fingers: no right-hand data at all.
            for(int i=0;i<60;i++)orb.Sample(Cup(),default,true,false,Dt);
            Assert.AreEqual(EmberdeepOrbGesture.Stage.Charging,orb.State);Assert.That(orb.Charge01,Is.GreaterThan(.8f).And.LessThan(.9f));
            for(int i=0;i<60;i++)orb.Sample(Cup(),default,true,false,Dt);
            Assert.AreEqual(EmberdeepOrbGesture.Stage.Held,orb.State,"A full orb settles into the palm");Assert.AreEqual(1,orb.Charge01,.001f);
            Assert.That(orb.Radius,Is.EqualTo(.044f).Within(.001f));
            // Tracking loss and a closed or upward hand never drop it.
            for(int i=0;i<100;i++)orb.Sample(i<50?default:Cup(),default,true,true,Dt);
            Assert.AreEqual(EmberdeepOrbGesture.Stage.Held,orb.State);
            var flipped=Hand(Left,Vector3.down,Vector3.forward);
            for(int i=0;i<3;i++){orb.Sample(flipped,default,true,true,Dt);Assert.False(orb.Thrown);}
            for(int i=0;i<4 && !orb.Thrown;i++)orb.Sample(flipped,default,true,true,Dt);
            Assert.True(orb.Thrown);Assert.AreEqual(1,orb.ThrownCharge,.001f);Assert.AreEqual(EmberdeepOrbGesture.Stage.Idle,orb.State);
        }
        [Test] public void OrbReleasedEarlyIsSmallerAndATapMakesNothing()
        {
            var orb=new EmberdeepOrbGesture();var away=OpenHand(Right+Vector3.right*.3f);
            for(int i=0;i<8;i++)orb.Sample(Cup(),Under(),true,false,Dt);
            for(int i=0;i<5;i++)orb.Sample(Cup(),away,true,false,Dt);
            Assert.AreEqual(EmberdeepOrbGesture.Stage.Idle,orb.State,"Too little water to form an orb");
            for(int i=0;i<40;i++)orb.Sample(Cup(),Under(),true,false,Dt);
            for(int i=0;i<5;i++)orb.Sample(Cup(),away,true,false,Dt);
            Assert.AreEqual(EmberdeepOrbGesture.Stage.Held,orb.State);
            Assert.That(orb.Charge01,Is.GreaterThan(.3f).And.LessThan(.7f));
            var flipped=Hand(Left,Vector3.down,Vector3.forward);
            for(int i=0;i<20;i++)orb.Sample(flipped,away,true,false,Dt);
            Assert.AreEqual(EmberdeepOrbGesture.Stage.Held,orb.State,"No valid landing spot: keep holding");
            for(int i=0;i<10 && !orb.Thrown;i++)orb.Sample(flipped,away,true,true,Dt);
            Assert.True(orb.Thrown);Assert.That(orb.ThrownCharge,Is.GreaterThan(.3f).And.LessThan(.7f));
        }
        [Test] public void OrbDoesNotStartFromADownwardPalmOrADistantHand()
        {
            var orb=new EmberdeepOrbGesture();
            var palmDown=Hand(Left,Vector3.down,Vector3.forward);
            for(int i=0;i<50;i++)orb.Sample(palmDown,Hand(Left+Vector3.up*.06f,Vector3.up,Vector3.left),true,false,Dt);
            Assert.AreEqual(EmberdeepOrbGesture.Stage.Idle,orb.State);
            for(int i=0;i<50;i++)orb.Sample(Cup(),OpenHand(Right),true,false,Dt);
            Assert.AreEqual(EmberdeepOrbGesture.Stage.Idle,orb.State);
        }

        static FireRun SpellRun()
        {
            var run=new FireRun(goddessTutorial:true){PlaytestAllSpells=true};
            Assert.True(run.StartStage());Assert.AreEqual(FirePhase.Fighting,run.Phase);return run;
        }
        [Test] public void EachSpellSpendsOnlyItsOwnCrystal()
        {
            var run=SpellRun();
            float palm=run.SpellWater(FireEquipmentId.Extinguisher),boltWater=run.SpellWater(FireEquipmentId.PulsePistol);
            float fanWater=run.SpellWater(FireEquipmentId.RainFan),orbWater=run.SpellWater(FireEquipmentId.PressureCannon);
            Assert.AreEqual(run.CapacityFor(FireEquipmentId.PulsePistol),boltWater);
            Assert.True(run.CastBolt(1,out float amount,out float radius));
            Assert.AreEqual(boltWater-10,run.SpellWater(FireEquipmentId.PulsePistol),.001f);
            Assert.AreEqual(85*1.2f,amount,.01f);Assert.Greater(radius,1);
            Assert.True(run.CastBolt(0,out float weak,out _));Assert.Less(weak,amount*.3f);
            Assert.AreEqual(boltWater-13,run.SpellWater(FireEquipmentId.PulsePistol),.001f);
            float sprayed=0;for(int i=0;i<10;i++)sprayed+=run.TickFan(.1f,true);
            Assert.AreEqual(fanWater-FireRun.FanFlow,run.SpellWater(FireEquipmentId.RainFan),.001f);Assert.AreEqual(110,sprayed,.01f);
            Assert.True(run.CastOrb(.5f,out float splash,out float wide));
            Assert.AreEqual(orbWater-18,run.SpellWater(FireEquipmentId.PressureCannon),.001f);Assert.AreEqual(150*1.1f,splash,.01f);Assert.AreEqual(2.2f,wide,.001f);
            Assert.AreEqual(palm,run.SpellWater(FireEquipmentId.Extinguisher),"Palm water keeps its own crystal");
            Assert.AreEqual(60,run.Water,"The retired extinguisher tank is untouched");
        }
        [Test] public void AnEmptyCrystalCastsNothingAndAWeakOneCastsLess()
        {
            var run=SpellRun();
            Assert.True(run.CastOrb(.5f,out _,out _));
            while(run.SpellWater(FireEquipmentId.PressureCannon)>=28)Assert.True(run.CastOrb(1,out _,out _));
            float left=run.SpellWater(FireEquipmentId.PressureCannon);Assert.AreEqual(10,left,.001f);
            Assert.True(run.CastOrb(1,out float partial,out _));
            Assert.AreEqual(150*1.6f*left/28,partial,.01f);Assert.Zero(run.SpellWater(FireEquipmentId.PressureCannon));
            Assert.False(run.CastOrb(1,out float none,out _));Assert.Zero(none);
            var unowned=new FireRun(goddessTutorial:true);unowned.StartStage();
            Assert.False(unowned.CastBolt(1,out _,out _),"Spells must be bought first");
            Assert.False(new FireRun().CanCast(FireEquipmentId.PulsePistol),"Legacy tool runs have no spells");
        }
        [Test] public void CrystalsRecoverSlowlyAfterADelay()
        {
            var run=SpellRun();run.CastBolt(1,out _,out _);float after=run.SpellWater(FireEquipmentId.PulsePistol);
            for(int i=0;i<19;i++)run.TickSpellRecovery(.1f,true);
            Assert.AreEqual(after,run.SpellWater(FireEquipmentId.PulsePistol),"Two-second wait");
            run.TickSpellRecovery(50,false);Assert.AreEqual(after,run.SpellWater(FireEquipmentId.PulsePistol));
            for(int i=0;i<30;i++)run.TickSpellRecovery(.1f,true);
            float rate=run.CapacityFor(FireEquipmentId.PulsePistol)/FireRun.SpellRecoverySeconds;
            Assert.That(run.SpellWater(FireEquipmentId.PulsePistol)-after,Is.EqualTo(rate*2.9f).Within(rate*.15f));
            for(int i=0;i<1000;i++)run.TickSpellRecovery(.1f,true);
            Assert.AreEqual(run.CapacityFor(FireEquipmentId.PulsePistol),run.SpellWater(FireEquipmentId.PulsePistol));
        }
        [Test] public void BasinCollectsPumpedWaterAndAHandAbsorbsIntoTheEmptiestCrystal()
        {
            var run=SpellRun();
            Assert.Zero(run.AbsorbBasin(.1f),"An empty basin gives nothing");
            Assert.AreEqual(24,run.AddBasinWater(24));Assert.AreEqual(36,run.AddBasinWater(100),"The basin overflows at its rim");
            Assert.AreEqual(FireRun.BasinCapacity,run.BasinWater);
            Assert.Zero(run.AbsorbBasin(.1f),"Full crystals drink nothing");Assert.AreEqual(FireRun.BasinCapacity,run.BasinWater);
            while(run.SpellWater(FireEquipmentId.PulsePistol)>40)run.CastBolt(1,out _,out _);
            float low=run.SpellWater(FireEquipmentId.PulsePistol),orbFull=run.SpellWater(FireEquipmentId.PressureCannon);
            float moved=0;for(int i=0;i<5;i++)moved+=run.AbsorbBasin(.1f);
            Assert.AreEqual(FireRun.BasinAbsorbPerSecond*.5f,moved,.001f);
            Assert.AreEqual(low+moved,run.SpellWater(FireEquipmentId.PulsePistol),.001f);
            Assert.AreEqual(FireRun.BasinCapacity-moved,run.BasinWater,.001f);Assert.AreEqual(orbFull,run.SpellWater(FireEquipmentId.PressureCannon));
            for(int i=0;i<30;i++)run.TickMagic(.1f,true,true);
            float mana=run.MagicWater;Assert.Less(mana/run.MagicCapacity,.1f);
            Assert.Greater(run.AbsorbBasin(.1f),0);Assert.Greater(run.MagicWater,mana,"Palm water drinks from the basin too");
        }
        [Test] public void StarterWellHasNoFlowAndLegacyRunsHaveNoBasin()
        {
            var run=SpellRun();
            Assert.False(run.SetBasinFlow(true),"The lever well only fills by pumping");
            for(int i=0;i<50;i++)run.TickBasin(.1f);
            Assert.Zero(run.BasinWater);
            var legacy=new FireRun();legacy.StartStage();
            Assert.Zero(legacy.AddBasinWater(10));Assert.False(legacy.SpellMode);Assert.False(legacy.CanCast(FireEquipmentId.RainFan));
        }
        [Test] public void EveryStageStartsWithFullCrystalsAndPurchasesArriveFull()
        {
            var run=SpellRun();
            run.CastBolt(1,out _,out _);run.CastOrb(1,out _,out _);for(int i=0;i<20;i++)run.TickFan(.1f,true);
            Assert.True(run.RetryStage());
            for(int i=1;i<4;i++)Assert.AreEqual(run.CapacityFor((FireEquipmentId)i),run.SpellWater((FireEquipmentId)i));
            run.Restart();Assert.True(run.Owns(FireEquipmentId.PressureCannon),"The playtest switch survives a new expedition");
            Assert.AreEqual(run.CapacityFor(FireEquipmentId.RainFan),run.SpellWater(FireEquipmentId.RainFan));
        }
        static void ClearStage(FireRun run)
        {
            foreach(var tree in run.Trees)if(tree.State==TreeState.Burning)run.ApplyWater(tree.Position,100,.1f);
            Assert.AreEqual(FirePhase.Cleared,run.Phase);run.EvacuateSafeAnimals();
            if(run.NeedsMagicUpgrade)Assert.True(run.BuyMagicUpgrade());
        }
        [Test] public void SpellsAndTheirLessonsArriveOnePurchaseAtATime()
        {
            Assert.False(EmberdeepSession.PlaytestAllSpells,"The shipping session starts with palm water only");
            EmberdeepSession.NewExpedition();var run=EmberdeepSession.Run;
            Assert.True(run.Owns(FireEquipmentId.Extinguisher));
            for(int i=1;i<4;i++){Assert.False(run.Owns((FireEquipmentId)i));Assert.Zero(run.SpellFill01((FireEquipmentId)i));}
            Assert.AreEqual(-1,run.SpellLesson,"Palm water has its own lesson; no spell lesson yet");
            for(int lesson=0;lesson<3;lesson++)
            {
                Assert.True(run.StartStage());Assert.True(run.IsPalmLesson);
                Assert.Zero(run.EquipmentIncidents,"Nothing can be bought during the palm lessons");ClearStage(run);
            }
            Assert.AreEqual(1,run.EquipmentIncidents);
            Assert.True(run.CanBuy(FireEquipmentId.PulsePistol),"The water bolt opens as soon as the palm lessons are cleared");
            Assert.False(run.CanBuy(FireEquipmentId.RainFan));Assert.False(run.CanBuy(FireEquipmentId.PressureCannon));
            Assert.True(run.BuyEquipment(FireEquipmentId.PulsePistol));
            Assert.AreEqual(1,run.SpellFill01(FireEquipmentId.PulsePistol));Assert.AreEqual(1,run.SpellLesson,"A new purchase starts its own lesson");
            run.Learn(EmberdeepInteractionDemo.Action.Bolt);Assert.AreEqual(-1,run.SpellLesson,"The lesson ends once the spell has put out a fire");
            Assert.True(run.StartStage());ClearStage(run);
            Assert.AreEqual(2,run.EquipmentIncidents);Assert.False(run.Owns(FireEquipmentId.RainFan),"Later spells still have to be bought");
        }
        [Test] public void PalmWaterRecoversSlowlyOnceTheFirstLessonIsOver()
        {
            var run=new FireRun(goddessTutorial:true);
            Assert.AreEqual(5,run.MagicFillSeconds);
            run.StartStage();foreach(var tree in run.Trees)if(tree.State==TreeState.Burning)run.ApplyWater(tree.Position,100,.1f);
            run.EvacuateSafeAnimals();Assert.True(run.BuyMagicUpgrade());
            Assert.AreEqual(30,run.MagicFillSeconds);
            Assert.True(run.StationOpen,"The well is usable from the very first level");Assert.True(new FireRun(goddessTutorial:true).StationOpen);
        }
    }
}

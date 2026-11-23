using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    // LoopSpec §11b layout A: the pure parts of the rim UI (token poke, carousel slap, progression, hint counts).
    public sealed class EmberdeepRimUiTests
    {
        static readonly Vector3 Center=new(0,1,0);
        // Skips the Plan countdown (the PlayMode EmberdeepTestFlow equivalent).
        static void Start(FireRun run){if(run.Phase!=FirePhase.Preparing)return;run.BeginCountdown();for(int i=0;i<31;i++)run.TickPreparation(.1f);}
        static EmberdeepFingerPoke.Tip Tip(Vector3 p,float closure=.1f)=>new(true,p,closure);
        static EmberdeepFingerPoke.Tip None=>default;

        [Test] public void FingertipPokeFiresOnceAndRearmsOnlyAfterLeaving()
        {
            var poke=new EmberdeepFingerPoke();
            Assert.False(poke.Sample(Tip(Center+Vector3.up*.10f),None,Center,Vector3.up),"Far above arms the hand");
            Assert.True(poke.Armed(0));
            Assert.False(poke.Sample(Tip(Center+Vector3.up*.03f),None,Center,Vector3.up));Assert.True(poke.Hovering);
            Assert.True(poke.Sample(Tip(Center+Vector3.up*.004f),None,Center,Vector3.up),"Crossing onto the face pokes");
            Assert.AreEqual(0,poke.PokeHand);
            for(int i=0;i<5;i++)
            {
                Assert.False(poke.Sample(Tip(Center+Vector3.up*.03f),None,Center,Vector3.up),"Hovering after a poke does not re-arm");
                Assert.False(poke.Sample(Tip(Center+Vector3.up*.002f),None,Center,Vector3.up),"A second dip without leaving is not a poke");
            }
            poke.Sample(Tip(Center+Vector3.up*.08f),None,Center,Vector3.up);
            poke.Sample(Tip(Center+Vector3.up*.02f),None,Center,Vector3.up);
            Assert.True(poke.Sample(Tip(Center),None,Center,Vector3.up),"Leaving and coming back pokes again");
        }
        [Test] public void FistsTrackingPopsAndOffCentreTipsNeverPoke()
        {
            var poke=new EmberdeepFingerPoke();
            poke.Sample(Tip(Center+Vector3.up*.1f,.9f),None,Center,Vector3.up);
            poke.Sample(Tip(Center+Vector3.up*.02f,.9f),None,Center,Vector3.up);
            Assert.False(poke.Sample(Tip(Center,.9f),None,Center,Vector3.up),"A fist (bell or pump) never pokes");
            poke.Reset();
            Assert.False(poke.Sample(Tip(Center+Vector3.up*.02f),None,Center,Vector3.up),"First seen inside: not armed");
            Assert.False(poke.Sample(Tip(Center),None,Center,Vector3.up),"A tip that pops in over the token is not a poke");
            poke.Reset();
            var side=Vector3.right*(EmberdeepFingerPoke.Radius+.01f);
            poke.Sample(Tip(Center+side+Vector3.up*.1f),None,Center,Vector3.up);poke.Sample(Tip(Center+side+Vector3.up*.02f),None,Center,Vector3.up);
            Assert.False(poke.Sample(Tip(Center+side),None,Center,Vector3.up),"Outside the token's radius");
            poke.Sample(Tip(Center+Vector3.up*.1f),None,Center,Vector3.up);poke.Sample(None,None,Center,Vector3.up);
            Assert.False(poke.Armed(0),"Lost tracking disarms");
        }

        // Headset test 2026-11-22: a pointing hand reads like a fist by its four-finger closure; only pinch poked.
        [Test] public void PointingIndexPokesFistDoesNotAndAQuickStepThroughTheFaceCounts()
        {
            var poke=new EmberdeepFingerPoke();
            EmberdeepFingerPoke.Tip Point(Vector3 p)=>new(true,p,.72f,.08f); // three fingers folded, index straight
            EmberdeepFingerPoke.Tip Fist(Vector3 p)=>new(true,p,.9f,.85f);
            EmberdeepFingerPoke.Tip Pinch(Vector3 p)=>new(true,p,.3f,.5f);
            poke.Sample(Point(Center+Vector3.up*.1f),None,Center,Vector3.up);
            Assert.True(poke.Sample(Point(Center+Vector3.up*.004f),None,Center,Vector3.up),"A pointing index pokes");
            poke.Reset();poke.Sample(Fist(Center+Vector3.up*.1f),None,Center,Vector3.up);poke.Sample(Fist(Center+Vector3.up*.02f),None,Center,Vector3.up);
            Assert.False(poke.Sample(Fist(Center),None,Center,Vector3.up),"A fist never pokes");
            poke.Reset();poke.Sample(Pinch(Center+Vector3.up*.1f),None,Center,Vector3.up);poke.Sample(Pinch(Center+Vector3.up*.02f),None,Center,Vector3.up);
            Assert.True(poke.Sample(Pinch(Center),None,Center,Vector3.up),"A pinch still pokes (fallback)");
            // One tracking step from 3 cm over the face to 4 cm under it, slanting in: it met the face inside the token.
            poke.Reset();poke.Sample(Point(Center+new Vector3(.03f,.1f,0)),None,Center,Vector3.up);poke.Sample(Point(Center+new Vector3(.02f,.03f,0)),None,Center,Vector3.up);
            Assert.True(poke.Sample(Point(Center+new Vector3(-.01f,-.04f,0)),None,Center,Vector3.up),"A quick step through the face");
            // A step that meets the plane outside the token is not a poke, even if it ends over the centre.
            poke.Reset();poke.Sample(Point(Center+new Vector3(.12f,.1f,0)),None,Center,Vector3.up);poke.Sample(Point(Center+new Vector3(.1f,.02f,0)),None,Center,Vector3.up);
            Assert.False(poke.Sample(Point(Center+new Vector3(0,-.01f,0)),None,Center,Vector3.up),"Met the plane outside the radius");
            // A reach past (ending deeper than PressDepth) is not a poke.
            poke.Reset();poke.Sample(Point(Center+Vector3.up*.1f),None,Center,Vector3.up);poke.Sample(Point(Center+Vector3.up*.02f),None,Center,Vector3.up);
            Assert.False(poke.Sample(Point(Center-Vector3.up*(EmberdeepFingerPoke.PressDepth+.01f)),None,Center,Vector3.up),"A reach past the token");
            // The coin is as large as the zone that pokes it.
            Assert.GreaterOrEqual(EmberdeepFingerPoke.Radius,EmberdeepRimHud.TokenDiameter*.5f);
        }

        static EmberdeepSlapGesture.Zone Zone=>new(new Pose(new Vector3(0,1.2f,.45f),Quaternion.identity),new Vector3(.42f,.3f,.26f));
        static EmberdeepSlapGesture.Hand Open(Vector3 p,bool pinching=false)=>new(true,p,true,pinching);
        // Moves the right hand sideways at speed (m/s) for seconds at 72 Hz. Returns the first non-zero result.
        static int Sweep(EmberdeepSlapGesture slap,ref Vector3 at,float speed,float seconds,bool open=true,bool pinching=false)
        {
            const float dt=1/72f;int fired=0;
            for(float t=0;t<seconds;t+=dt)
            {
                at+=Vector3.right*speed*dt;
                int r=slap.Sample(default,new EmberdeepSlapGesture.Hand(true,at,open,pinching),Zone,dt);
                if(fired==0)fired=r;
            }
            return fired;
        }
        [Test] public void OpenHandSlapBrowsesBothWaysWithACooldownAndNoReturnPage()
        {
            var slap=new EmberdeepSlapGesture();var at=new Vector3(-.25f,1.2f,.45f);
            Sweep(slap,ref at,0,.2f);
            Assert.AreEqual(1,Sweep(slap,ref at,1.6f,.18f),"A quick open-hand sweep to the right");
            Assert.AreEqual(0,Sweep(slap,ref at,-1.6f,.18f),"The swing straight back is inside the cooldown and needs a slowdown first");
            Sweep(slap,ref at,0,.6f);
            Assert.AreEqual(-1,Sweep(slap,ref at,-1.6f,.2f),"After a pause, a slap to the left");
        }
        [Test] public void JitterSlowMovesPinchesFistsAndPopsNeverSlap()
        {
            var slap=new EmberdeepSlapGesture();var random=new System.Random(7);const float dt=1/72f;
            var rest=new Vector3(0,1.2f,.45f);
            for(int i=0;i<72*10;i++)
            {
                // Hand tracking noise: +-1.5 cm per frame around a resting palm.
                var noise=new Vector3((float)random.NextDouble()-.5f,(float)random.NextDouble()-.5f,(float)random.NextDouble()-.5f)*.03f;
                Assert.AreEqual(0,slap.Sample(default,Open(rest+noise),Zone,dt),"Jitter at frame "+i);
            }
            var at=new Vector3(-.3f,1.2f,.45f);
            Assert.AreEqual(0,Sweep(slap,ref at,.5f,1f),"A slow reach across is browsing nothing");
            at=new Vector3(-.3f,1.2f,.45f);Sweep(slap,ref at,0,.2f);
            Assert.AreEqual(0,Sweep(slap,ref at,1.6f,.2f,true,true),"A pinching hand (aiming the ray) never slaps");
            at=new Vector3(-.3f,1.2f,.45f);Sweep(slap,ref at,0,.2f);
            Assert.AreEqual(0,Sweep(slap,ref at,1.6f,.2f,false),"A closed hand never slaps");
            slap.Reset();
            Assert.AreEqual(0,slap.Sample(default,Open(new Vector3(-.2f,1.2f,.45f)),Zone,dt));
            Assert.AreEqual(0,slap.Sample(default,Open(new Vector3(.2f,1.2f,.45f)),Zone,dt),"A 40 cm one-frame jump is a tracking pop");
            Assert.AreEqual(0,slap.Sample(default,Open(new Vector3(.2f,1.2f,.45f)),Zone,dt));
            at=new Vector3(-.3f,1.8f,.45f);Sweep(slap,ref at,0,.2f);
            Assert.AreEqual(0,Sweep(slap,ref at,1.6f,.2f),"Outside the zone (above the panel line)");
        }

        [Test] public void PumpUpgradeRaisesStrikeWaterAndCostsCoins()
        {
            var run=new FireRun(goddessTutorial:true);
            Assert.AreEqual(EmberdeepWaterTuning.StrikeWater,run.PumpStrikeUnits(false));
            Assert.AreEqual(EmberdeepWaterTuning.OnBeatWater,run.PumpStrikeUnits(true));
            Assert.False(run.BuyPumpUpgrade(),"No coins");
            Assert.True(run.StartStage(true));Start(run);
            run.ApplyWater(Vector3.zero,10000,100);foreach(var a in run.Animals)run.ApplyAnimalWater(a.Position,100,.6f);
            for(int i=0;i<400 && run.Phase==FirePhase.Fighting;i++)run.Tick(.05f,false);
            Assert.AreEqual(FirePhase.Cleared,run.Phase);run.EvacuateSafeAnimals();
            Assert.False(run.NeedsMagicUpgrade,"Stage cut: no forced first gem upgrade");Assert.True(run.CanUpgradePump,"The pump can be the first buy");
            Assert.True(run.BuyMagicUpgrade());
            // Earn enough for a pump level.
            while(run.Coins<run.PumpUpgradeCost){Assert.True(run.SelectStage(0));Start(run);run.ApplyWater(Vector3.zero,10000,100);
                for(int i=0;i<400 && run.Phase==FirePhase.Fighting;i++){foreach(var a in run.Animals)run.ApplyAnimalWater(a.Position,100,.6f);run.Tick(.05f,false);}
                run.EvacuateSafeAnimals();}
            int coins=run.Coins,price=run.PumpUpgradeCost;
            Assert.True(run.BuyPumpUpgrade());Assert.AreEqual(coins-price,run.Coins);Assert.AreEqual(1,run.PumpLevel);
            Assert.AreEqual(EmberdeepWaterTuning.StrikeWater+FireRun.PumpWaterPerLevel,run.PumpStrikeUnits(false));
        }
        [Test] public void LevelSelectReplaysUnlockedStagesOnlyAndKeepsEarnings()
        {
            var run=new FireRun(goddessTutorial:true);Assert.True(run.StartStage(true));
            Assert.AreEqual(0,run.HighestUnlockedStage);Assert.False(run.CanSelectStage(1),"Stage 2 is locked");
            Assert.True(run.CanSelectStage(0),"The Plan of the current stage can be re-laid");
            Start(run);Assert.False(run.CanSelectStage(0),"Not mid-fight");
            run.ApplyWater(Vector3.zero,10000,100);foreach(var a in run.Animals)run.ApplyAnimalWater(a.Position,100,.6f);
            for(int i=0;i<400 && run.Phase==FirePhase.Fighting;i++)run.Tick(.05f,false);
            Assert.AreEqual(FirePhase.Cleared,run.Phase);Assert.AreEqual(1,run.HighestUnlockedStage,"Clearing stage 1 unlocks stage 2");
            run.EvacuateSafeAnimals();Assert.True(run.BuyMagicUpgrade());
            int coins=run.Coins;
            Assert.True(run.SelectStage(1));Assert.AreEqual(1,run.Stage);Assert.AreEqual(FirePhase.Preparing,run.Phase);
            Assert.AreEqual(coins,run.Coins);Assert.AreEqual(1,run.MagicLevel,"Upgrades carry over");
            Assert.True(run.SelectStage(0),"Back to an earlier stage from Plan");Assert.AreEqual(0,run.Stage);
            Assert.False(run.SelectStage(2));Assert.False(run.SelectStage(-1));Assert.False(run.SelectStage(99));
            Assert.AreEqual(1,run.HighestUnlockedStage,"Replaying stage 1 keeps stage 2 open");
        }
        [Test] public void FirstTimeHintsRetireAfterTwoPerformances()
        {
            var move=EmberdeepFirstHints.Move.Slap;string key=EmberdeepFirstHints.Key(move);
            bool had=PlayerPrefs.HasKey(key);int prior=PlayerPrefs.GetInt(key,0);
            try
            {
                PlayerPrefs.DeleteKey(key);
                Assert.True(EmberdeepFirstHints.Needed(move));Assert.AreEqual(2,EmberdeepFirstHints.Remaining(move));
                var go=new GameObject("Hints");var hints=go.AddComponent<EmberdeepFirstHints>();
                try
                {
                    hints.Performed(move);Assert.AreEqual(1,EmberdeepFirstHints.Remaining(move));
                    hints.Performed(move);Assert.False(EmberdeepFirstHints.Needed(move));
                    hints.Performed(move);Assert.AreEqual(2,EmberdeepFirstHints.Count(move),"Counts stop at the limit");
                }
                finally{Object.DestroyImmediate(go);}
            }
            finally{if(had)PlayerPrefs.SetInt(key,prior);else PlayerPrefs.DeleteKey(key);}
        }
        [Test] public void RimAndPanelsFollowTheMeasuredPlinth()
        {
            // Token and hourglass on the water-hand front corner since the pump took the free-hand one (side is the free-hand
            // sign), in from the rounded corner of the compact plinth. The bell floats over the field's centre (headset test 2).
            var board=EmberdeepRimHud.DefaultBoard;
            var today=EmberdeepRimHud.LayoutFor(board,1);
            Assert.AreEqual(1,today.Size,1e-3f);Assert.AreEqual(today.UnitsPerMetre*EmberdeepRimHud.StripScale,today.StripUnitsPerMetre,1e-4f);
            Assert.AreEqual(.015f-5.555f*EmberdeepRimHud.TokenAcross,today.Token.x,.01f);Assert.AreEqual(-.55f+EmberdeepRimHud.TokenIn*12.5f,today.Token.z,.01f);
            Assert.AreEqual(.015f-5.555f*EmberdeepRimHud.HourglassAcross,today.Hourglass.x,.01f);Assert.AreEqual(.3f,today.Hourglass.z,.01f);
            Assert.Less(Mathf.Abs(today.Token.x-today.Hourglass.x),Mathf.Abs(today.Token.x)*.5f,"token and hourglass share the corner");
            Assert.AreEqual(board.center.x,today.Bell.x,1e-4f,"The bell floats over the middle");Assert.AreEqual(EmberdeepRimHud.DefaultField.y,today.Bell.z,1e-4f);
            Assert.AreEqual(board.max.y+EmberdeepRimHud.CanopyTop+EmberdeepRimHud.BellClear*today.UnitsPerMetre,today.Bell.y,1e-4f,"Its base just over the crowns");
            var field=new Vector2(1.2f,4.15f);var atField=EmberdeepRimHud.LayoutFor(board,1,EmberdeepRimHud.DefaultEye,field);
            Assert.AreEqual(field.x,atField.Bell.x,1e-4f,"Over this level's field");Assert.AreEqual(field.y,atField.Bell.z,1e-4f);
            var mirrored=EmberdeepRimHud.LayoutFor(board,-1);
            Assert.AreEqual(-today.Token.x+2*board.center.x,mirrored.Token.x,1e-4f,"The token mirrors to the other hand");
            // Headset test 2026-11-22: the readout strip stands centred behind the back edge (the vane's gap in its middle),
            // its bottom just over the eye's sight line across the deepest back row's crowns.
            void StripOverTheForest(Bounds b,Vector3 eye,EmberdeepRimHud.RimLayout l,string at)
            {
                Assert.AreEqual(b.center.x,l.Strip.x,1e-4f,at+": centred");Assert.Greater(l.Strip.z,b.max.z,at+": behind the back edge");
                float bottom=l.Strip.y-EmberdeepRimHud.StripHeight*.5f*l.StripUnitsPerMetre;
                float sight=eye.y+(b.max.y+EmberdeepRimHud.CanopyTop-eye.y)*(l.Strip.z-eye.z)/(EmberdeepRimHud.SightRowZ-eye.z);
                Assert.AreEqual(sight+EmberdeepRimHud.StripClear*l.UnitsPerMetre,bottom,1e-3f,at+": bottom on the sight line over the canopy");
                Assert.Greater(bottom,b.max.y,at+": above the board top");
            }
            StripOverTheForest(board,EmberdeepRimHud.DefaultEye,today,"default eye");
            Assert.AreEqual(today.Strip,mirrored.Strip,"The strip does not mirror");
            var low=new Vector3(0,2.6f,-3);var lowEye=EmberdeepRimHud.LayoutFor(board,1,low);StripOverTheForest(board,low,lowEye,"lower eye");
            Assert.Greater(lowEye.Strip.y,today.Strip.y,"A lower eye (higher board) lifts the strip to stay over the canopy");
            // Panels: a fixed angle under eye level at their distance, so a nearer or farther board keeps the buttons up.
            var eye=new Vector3(0,.65f/FireMiniature.Scale,6-.8f/FireMiniature.Scale);
            float Down(Vector3 p,Vector3 from)=>Mathf.Atan2(from.y-p.y,new Vector2(p.x-from.x,p.z-from.z).magnitude)*Mathf.Rad2Deg;
            var results=FireRescueGame.ResultsAnchorFor(board,eye);
            Assert.AreEqual(13.2f,results.z,.02f);Assert.AreEqual(FireRescueGame.PanelDown,Down(results,eye),1e-3f);
            Assert.AreEqual(FireRescueGame.PanelUnit*Vector3.Distance(results,eye)*FireMiniature.Scale/FireRescueGame.ResultsReference,
                FireRescueGame.PanelUnitFor(results,eye,FireRescueGame.ResultsReference),1e-4f);
            var carousel=FireRescueGame.CarouselAnchorFor(board,eye);
            Assert.AreEqual(10.5f,carousel.z,.02f);Assert.AreEqual(FireRescueGame.PanelDown,Down(carousel,eye),1e-3f);
            // A shrunk, wider, raised and shifted board: everything stays on its edges.
            var shrunk=new Bounds(new Vector3(.5f,.6f,3.2f),new Vector3(13.5f,.8f,5.4f));
            var shrunkEye=new Vector3(.5f,eye.y-2,shrunk.min.z-3.75f);
            foreach(float side in new[]{1f,-1f})
            {
                var l=EmberdeepRimHud.LayoutFor(shrunk,side,shrunkEye);
                Assert.Greater(l.Size,1,"A wider board gets a larger rim");Assert.LessOrEqual(l.Size,EmberdeepRimHud.MaxSize);
                Assert.Greater(l.Strip.z,shrunk.max.z,"The strip stands behind the back edge");
                Assert.Greater(l.Token.z,shrunk.min.z);Assert.Greater(l.Bell.z,shrunk.min.z);Assert.Less(l.Bell.z,shrunk.max.z);
                Assert.AreEqual(shrunk.max.y,l.Hourglass.y,1e-4f,"The hourglass stands on the raised top");
                Assert.AreEqual(shrunk.max.y+EmberdeepRimHud.CanopyTop+EmberdeepRimHud.BellClear*l.UnitsPerMetre,l.Bell.y,1e-4f,"The bell floats over the raised canopy");
                Assert.Less((l.Token.x-shrunk.center.x)*side,0,"Water-hand side");Assert.Less((l.Hourglass.x-shrunk.center.x)*side,0);Assert.Less(Mathf.Abs(l.Token.x-shrunk.center.x),shrunk.extents.x);
                Assert.AreEqual(shrunk.center.x,l.Bell.x,1e-4f,"The bell does not mirror");
                Assert.Less(l.Token.z,shrunk.center.z,"The token stays on the front half, in reach");Assert.Less(l.Hourglass.z,shrunk.center.z);
            }
            var behind=FireRescueGame.ResultsAnchorFor(shrunk,shrunkEye);Assert.Greater(behind.z,shrunk.max.z);Assert.AreEqual(shrunk.center.x,behind.x,1e-4f);
            Assert.AreEqual(FireRescueGame.PanelDown,Down(behind,shrunkEye),1e-3f,"The same angle under the eye");
            Assert.Less(FireRescueGame.PanelUnitFor(behind,shrunkEye,FireRescueGame.ResultsReference),FireRescueGame.PanelUnit,"A closer panel shrinks to keep its angular size");
            var over=FireRescueGame.CarouselAnchorFor(shrunk,shrunkEye);Assert.Greater(over.z,shrunk.min.z);Assert.Less(over.z,shrunk.max.z);
            // The compact board from the default seat: a panel is never lower than PanelDown, and rises over its floor.
            var compact=new Bounds(new Vector3(.015f,-.405f,(EmberdeepBoardLayout.CompactFront+EmberdeepBoardLayout.CompactBack)*.5f),
                new Vector3(11.11f,.81f,EmberdeepBoardLayout.CompactBack-EmberdeepBoardLayout.CompactFront));
            var seat=EmberdeepRimHud.DefaultEye;
            float stripTop=EmberdeepRimHud.StripTopDown(compact,seat),sky=EmberdeepRimHud.SkylineDown(compact,seat);
            Assert.Less(stripTop,sky,"The strip stands over the skyline");
            foreach(var below in new[]{FireRescueGame.PauseBelow,FireRescueGame.ResultsBelow})
            {
                float centre=FireRescueGame.PanelCentreDown(stripTop,below);
                // At most PanelDown under eye level, and its lower edge over the strip (from the -14 / +6 default seat the strip
                // is low enough that the cap, not the strip, sets the height).
                Assert.LessOrEqual(centre,FireRescueGame.PanelDown);Assert.LessOrEqual(centre+below,stripTop-FireRescueGame.PanelClear+1e-4f);
            }
            Assert.LessOrEqual(FireRescueGame.PanelCentreDown(sky,FireRescueGame.UpgradesBelow)+FireRescueGame.UpgradesBelow,sky-FireRescueGame.PanelClear+1e-4f);
            // A board raised toward the eye (height option) lifts the floor and the panels with it.
            var raised=seat+Vector3.down*1.5f;
            Assert.Less(FireRescueGame.PanelCentreDown(EmberdeepRimHud.StripTopDown(compact,raised),FireRescueGame.ResultsBelow),
                FireRescueGame.PanelCentreDown(stripTop,FireRescueGame.ResultsBelow));
        }
        [Test] public void PlinthMeasurementIgnoresMossOnLogsAndCabins()
        {
            var arena=new GameObject("Arena").transform;
            try
            {
                arena.localScale=Vector3.one*FireMiniature.Scale;arena.position=new Vector3(3,1,2);arena.rotation=Quaternion.Euler(0,40,0);
                GameObject Part(string name,Transform parent,Vector3 at,Vector3 size)
                {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=at;go.transform.localScale=size;return go;}
                var terrain=new GameObject("Terrain(Clone)").transform;terrain.SetParent(arena,false);
                Part("Slate",terrain,new Vector3(0,-.5f,4),new Vector3(10,.6f,8));Part("Moss.002",terrain,new Vector3(0,-.05f,4),new Vector3(9.6f,.1f,7.6f));
                var cabin=new GameObject("Cabin(Clone)").transform;cabin.SetParent(arena,false);Part("Moss.004",cabin,new Vector3(3,2.5f,6),Vector3.one);
                Assert.True(EmberdeepRimHud.MeasurePlinth(arena,out var board));
                Assert.AreEqual(-.8f,board.min.y,1e-3f);Assert.AreEqual(0,board.max.y,1e-3f,"The cabin roof moss is not the board top");
                Assert.AreEqual(0,board.min.z,1e-3f);Assert.AreEqual(8,board.max.z,1e-3f);Assert.AreEqual(10,board.size.x,1e-3f);
            }
            finally{Object.DestroyImmediate(arena.gameObject);}
        }
        [Test] public void LevelCardsNameEveryStageAndAChip()
        {
            var run=new FireRun(goddessTutorial:true);
            for(int s=0;s<run.StageCount;s++)
            {Assert.IsNotEmpty(FireRescueGame.LevelName(run,s));Assert.IsNotEmpty(FireRescueGame.LevelChip(s));Assert.False(FireRescueGame.LevelChip(s).Contains(" "),"Chips are one word");}
            Assert.AreEqual(6,run.StageCount);Assert.AreEqual(run.StageCount,FireRun.LevelNames.Length);Assert.AreEqual("SHEEP MEADOW",FireRescueGame.LevelName(run,0));Assert.AreEqual(FireRun.LevelNames[1],FireRescueGame.LevelName(run,FireRun.PalmLessons));
            Assert.AreEqual("EMBERDEEP",FireRescueGame.LevelName(run,5));Assert.AreEqual("PUMP",FireRescueGame.LevelChip(1));
            // Text pass (LoopSpec §13): the legacy tool flow keeps its own names.
            Assert.AreEqual(FireRun.Names[0],FireRun.StageNameFor(false,0));Assert.AreEqual(FireRun.Names[4],FireRun.StageNameFor(false,4));
        }
    }
}

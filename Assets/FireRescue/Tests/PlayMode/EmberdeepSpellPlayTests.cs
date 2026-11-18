using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    // Gesture spells in the real fireground scene. Editor IsXR is false: hands are
    // synthetic samples through the public adapter, and the desktop keys stand in
    // for them. Neither establishes live tracking, occlusion or comfort.
    public sealed class EmberdeepSpellPlayTests
    {
        bool playtest;
        FireRescueGame game;
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;
        [SetUp] public void Setup()
        {
            playtest=EmberdeepSession.PlaytestAllSpells;EmberdeepSession.PlaytestAllSpells=true;
            background=InputSystem.settings.backgroundBehavior;editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }
        [TearDown] public void Cleanup()
        {
            if(game)game.enabled=false;
            EmberdeepSession.PlaytestAllSpells=playtest;EmberdeepSession.NewExpedition();
            InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
        }
        IEnumerator Enter()
        {
            EmberdeepSession.NewExpedition();yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);yield return null;yield return null;
            game=Object.FindFirstObjectByType<FireRescueGame>();Assert.NotNull(game);
            game.SendMessage("OnApplicationFocus",true);game.SendMessage("OnApplicationPause",false);
            EmberdeepTestFlow.StartPrepared(game.Run);game.Refresh();yield return null;
            var run=game.Run;Assert.True(run.SpellMode);Assert.AreEqual(FirePhase.Fighting,run.Phase);
            for(int i=0;i<4;i++)Assert.True(run.Owns((FireEquipmentId)i),"The playtest switch owns every spell from the first level");
            Assert.False(game.transform.Find("Equipment rack").gameObject.activeSelf,"Hand-held tools stay hidden");
            Assert.IsNull(game.transform.Find("Spell gesture board"),"No wall of text: lessons are demonstrations");
            Assert.False(game.TakeEquipment());Assert.True(run.StationOpen);Assert.True(game.Basin.gameObject.activeInHierarchy);
        }
        static EmberdeepSpellHand Hand(Vector3 at,Vector3 normal,Vector3 forward,float curl=0)
        {
            return new EmberdeepSpellHand{Valid=true,Contact=at,Frame=Quaternion.LookRotation(forward,normal),CurlsValid=true,
                Index=curl,Middle=curl,Ring=curl,Little=curl,Open=curl<.2f,PointValid=true,
                Wrist=at-forward*.05f,IndexBase=at+forward*.04f,TipMask=31,
                ThumbTip=at+forward*.03f,IndexTip=at+forward*.12f,MiddleTip=at+forward*.12f,RingTip=at+forward*.11f,LittleTip=at+forward*.09f};
        }
        static Vector3 Side(Vector3 direction)
        {var side=Vector3.Cross(Vector3.up,direction);return side.sqrMagnitude<.001f?Vector3.right:side.normalized;}

        [UnityTest] public IEnumerator SyntheticGesturesCastFanBoltAndOrbFromTheirOwnCrystals()
        {
            yield return Enter();
            var run=game.Run;var arena=game.Arena;float palm=run.MagicWater;
            float boltFull=run.SpellWater(FireEquipmentId.PulsePistol),fanFull=run.SpellWater(FireEquipmentId.RainFan),orbFull=run.SpellWater(FireEquipmentId.PressureCannon);
            var far=new Vector3(100,0,100);var idle=Hand(far,Vector3.up,Vector3.forward);

            // RAIN FAN: both open palms face along the island, away from the single burning tree.
            var forward=arena.forward;
            var fanLeft=Hand(arena.TransformPoint(new Vector3(-4.6f,4,0)),forward,Vector3.up);
            var fanRight=Hand(arena.TransformPoint(new Vector3(-2.4f,4,0)),forward,Vector3.up);
            for(int i=0;i<8;i++)game.SampleSpells(fanLeft,fanRight,.1f);
            Assert.True(game.FanSpell.Ready);Assert.False(game.FanSpell.Active,"One full second is required");
            yield return null;Assert.False(game.FanSpraying);Assert.AreEqual(fanFull,run.SpellWater(FireEquipmentId.RainFan));
            for(int i=0;i<3;i++)game.SampleSpells(fanLeft,fanRight,.1f);
            Assert.True(game.FanSpell.Active);
            float until=Time.realtimeSinceStartup+.25f;
            while(Time.realtimeSinceStartup<until)yield return null;
            Assert.True(game.FanSpraying);Assert.Less(run.SpellWater(FireEquipmentId.RainFan),fanFull);
            Assert.AreEqual(FirePhase.Fighting,run.Phase,"The fan was aimed away from the fire");
            game.SampleSpells(fanLeft,Hand(fanRight.Contact,forward,Vector3.up,.95f),.02f);
            Assert.False(game.FanSpell.Active);yield return null;Assert.False(game.FanSpraying);
            float fanAfter=run.SpellWater(FireEquipmentId.RainFan);yield return null;Assert.AreEqual(fanAfter,run.SpellWater(FireEquipmentId.RainFan),.1f);

            // WATER BOLT: the left hand points at bare ground; the right fist charges and its opening fires once.
            var origin=arena.TransformPoint(new Vector3(-1,4,0));
            var aim=(arena.TransformPoint(new Vector3(-4,.7f,9))-origin).normalized;
            var pointing=Hand(origin,Side(aim),aim);pointing.Open=false;pointing.Ring=pointing.Little=.9f;
            var rightAt=arena.TransformPoint(new Vector3(2,4,0));
            game.SampleSpells(pointing,Hand(rightAt,Vector3.left,Vector3.forward),.02f);
            for(int i=0;i<6;i++)game.SampleSpells(pointing,Hand(rightAt,Vector3.left,Vector3.forward,.95f),.1f);
            Assert.True(game.BoltSpell.Charging);Assert.That(game.BoltSpell.Charge01,Is.EqualTo(.5f).Within(.01f));
            yield return null;Assert.AreEqual(boltFull,run.SpellWater(FireEquipmentId.PulsePistol),"Charging is free");
            game.SampleSpells(pointing,Hand(rightAt,Vector3.left,Vector3.forward),.02f);
            Assert.True(game.BoltSpell.Fired);yield return null;yield return null;
            Assert.That(run.SpellWater(FireEquipmentId.PulsePistol),Is.EqualTo(boltFull-FireRun.BoltCost(.5f)).Within(.2f),"One release is one shot");
            Assert.AreEqual(orbFull,run.SpellWater(FireEquipmentId.PressureCannon));Assert.AreEqual(palm,run.MagicWater,.5f);

            // WATER ORB: cupped left palm above the burning tree, right fingers beneath it.
            var above=arena.TransformPoint(run.Trees[0].Position+Vector3.up*3);
            var cup=Hand(above,Vector3.up,arena.forward,.35f);
            var under=Hand(above+Vector3.down*.06f+arena.right*.08f,Vector3.up,-arena.right);
            for(int i=0;i<10;i++)game.SampleSpells(cup,under,.1f);
            Assert.AreEqual(EmberdeepOrbGesture.Stage.Charging,game.OrbSpell.State);
            game.SampleSpells(cup,idle,.1f);
            Assert.AreEqual(EmberdeepOrbGesture.Stage.Held,game.OrbSpell.State);float charge=game.OrbSpell.Charge01;
            Assert.That(charge,Is.GreaterThan(.4f).And.LessThan(.8f));
            yield return null;Assert.True(game.SpellVfx.OrbVisible,"The spinning orb sits in the palm");
            Assert.That(game.SpellVfx.OrbRadius,Is.EqualTo(game.OrbSpell.Radius).Within(.0001f));
            Assert.AreEqual(orbFull,run.SpellWater(FireEquipmentId.PressureCannon),"A held orb has not been paid for yet");
            var drop=Hand(above,Vector3.down,arena.forward);
            for(int i=0;i<3 && !game.OrbSpell.Thrown;i++)game.SampleSpells(drop,idle,.1f);
            Assert.True(game.OrbSpell.Thrown);
            until=Time.realtimeSinceStartup+3;
            while(run.Phase==FirePhase.Fighting && Time.realtimeSinceStartup<until)yield return null;
            Assert.That(run.SpellWater(FireEquipmentId.PressureCannon),Is.EqualTo(orbFull-FireRun.OrbCost(charge)).Within(.5f));
            Assert.AreEqual(FirePhase.Cleared,run.Phase,"The dropped orb must land on the tree and save it");
            Assert.False(run.NeedsTutorial(EmberdeepInteractionDemo.Action.Orb),"Putting out a fire with the orb ends its lesson");
            Assert.True(run.NeedsTutorial(EmberdeepInteractionDemo.Action.Bolt),"A shot at bare ground teaches nothing");
            Assert.True(run.NeedsTutorial(EmberdeepInteractionDemo.Action.Fan));
            // Every fire is out: sparks over the grove and hopping trees.
            Assert.True(game.Celebrating);Assert.True(game.Effects.Celebrating);
            float party=Time.realtimeSinceStartup+.4f;while(Time.realtimeSinceStartup<party)yield return null;
            Assert.Greater(game.Effects.VisibleCelebration,10);
            Assert.False(game.SpellVfx.OrbVisible);Assert.AreEqual(60,run.Water,"The retired extinguisher tank is never used");
        }

        [UnityTest] public IEnumerator DesktopKeysCastSpellsPumpTheBasinAndAbsorbIt()
        {
            yield return Enter();
            var run=game.Run;var mouse=InputSystem.AddDevice<Mouse>();var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                var screen=game.Player.ViewCamera.WorldToScreenPoint(game.Arena.TransformPoint(new Vector3(-3.5f,.7f,8)));
                IEnumerator Hold(float seconds,params Key[] keys)
                {
                    float until=Time.realtimeSinceStartup+seconds;int frames=0;
                    while(Time.realtimeSinceStartup<until || frames<3)
                    {
                        InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(screen.x,screen.y)});
                        InputSystem.QueueStateEvent(keyboard,keys.Length>0?new KeyboardState(keys):new KeyboardState());frames++;yield return null;
                    }
                }
                yield return Hold(.1f);
                float boltFull=run.SpellWater(FireEquipmentId.PulsePistol),fanFull=run.SpellWater(FireEquipmentId.RainFan),orbFull=run.SpellWater(FireEquipmentId.PressureCannon);
                yield return Hold(.3f,Key.Digit2);Assert.AreEqual(boltFull,run.SpellWater(FireEquipmentId.PulsePistol));Assert.Greater(game.SpellCharge01,0);
                yield return Hold(.1f);
                Assert.That(boltFull-run.SpellWater(FireEquipmentId.PulsePistol),Is.InRange(3f,10f));
                yield return Hold(.3f,Key.Digit3);Assert.True(game.FanSpraying);
                yield return Hold(.1f);Assert.False(game.FanSpraying);Assert.Less(run.SpellWater(FireEquipmentId.RainFan),fanFull);
                yield return Hold(.3f,Key.Digit4);yield return Hold(.1f);
                Assert.That(orbFull-run.SpellWater(FireEquipmentId.PressureCannon),Is.InRange(8f,28f));
                Assert.AreEqual(FirePhase.Fighting,run.Phase,"Every shot was aimed at bare ground");
                // R lowers and returns the well lever; each stroke pours into the basin. E holds a hand in it.
                Assert.Zero(run.BasinWater);
                for(int stroke=0;stroke<2;stroke++){yield return Hold(.1f,Key.R);yield return Hold(.1f);}
                Assert.That(run.BasinWater,Is.EqualTo(2*.085f/.08f*12*1.5f).Within(.5f));
                float basin=run.BasinWater,low=run.SpellFill01(FireEquipmentId.PulsePistol)+run.SpellFill01(FireEquipmentId.RainFan)+run.SpellFill01(FireEquipmentId.PressureCannon);
                yield return Hold(.3f,Key.E);
                Assert.Less(run.BasinWater,basin);
                Assert.Greater(run.SpellFill01(FireEquipmentId.PulsePistol)+run.SpellFill01(FireEquipmentId.RainFan)+run.SpellFill01(FireEquipmentId.PressureCannon),low);
                yield return Hold(.05f);Assert.False(game.Absorbing);
            }
            finally{InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);}
        }
    }
}

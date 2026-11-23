using NUnit.Framework;
using UnityEngine;
using Step=FireRescue.EmberdeepCampTutorialFlow.Step;

namespace FireRescue.Tests
{
    // LoopSpec §9 camp tutorial (workstream T): the step flow, the camp FireRun scene and the remembered first launch.
    public sealed class EmberdeepCampTutorialTests
    {
        static void Hold(EmberdeepCampTutorialFlow flow,float seconds,bool fire,bool pump,bool sheep,bool pet)
        {for(float t=0;t<seconds;t+=.05f)flow.Tick(.05f,fire,pump,sheep,pet);}

        [Test] public void StepsRunInOrderAndEachWaitsForItsOwnDeed()
        {
            var flow=new EmberdeepCampTutorialFlow();Assert.IsFalse(flow.Active);Assert.IsFalse(flow.Tick(.1f,true,true,true,true));
            flow.Begin();Assert.AreEqual(Step.Campfire,flow.Current);Assert.AreEqual(0,flow.Chip);
            Hold(flow,5,false,true,true,true);Assert.AreEqual(Step.Campfire,flow.Current,"Only the campfire going out moves on");
            flow.Tick(.05f,true,false,false,false);Assert.IsTrue(flow.Completing);Assert.AreEqual(Step.Campfire,flow.Current,"A done moment first");
            Hold(flow,EmberdeepCampTutorialFlow.Beat+.1f,false,false,false,false);Assert.AreEqual(Step.Pump,flow.Current,"The beat holds even if the fire relights");
            Assert.AreEqual(1,flow.Chip);
            Hold(flow,3,true,false,true,true);Assert.AreEqual(Step.Pump,flow.Current);
            Hold(flow,EmberdeepCampTutorialFlow.Beat+.2f,false,true,false,false);Assert.AreEqual(Step.Sheep,flow.Current);Assert.AreEqual(2,flow.Chip);
            Hold(flow,EmberdeepCampTutorialFlow.Beat+.2f,false,false,true,false);Assert.AreEqual(Step.Pet,flow.Current);Assert.AreEqual(2,flow.Chip,"The pet closes the sheep chip");
            Hold(flow,EmberdeepCampTutorialFlow.Beat+.2f,false,false,false,true);Assert.AreEqual(Step.Done,flow.Current);Assert.AreEqual(EmberdeepCampTutorialFlow.Chips,flow.Chip);
            Assert.IsFalse(flow.Finished);Hold(flow,EmberdeepCampTutorialFlow.DoneSeconds+.1f,false,false,false,false);Assert.IsTrue(flow.Finished);
            flow.Stop();Assert.IsFalse(flow.Active);Assert.AreEqual(Step.Off,flow.Current);
        }
        [Test] public void BadFrameTimesNeitherAdvanceNorBreakTheFlow()
        {
            var flow=new EmberdeepCampTutorialFlow();flow.Begin();
            Assert.IsFalse(flow.Tick(float.NaN,true,true,true,true));Assert.IsFalse(flow.Tick(-1,true,true,true,true));Assert.IsFalse(flow.Tick(float.PositiveInfinity,true,true,true,true));
            Assert.IsFalse(flow.Completing);
            flow.Tick(30,true,false,false,false);Assert.AreEqual(Step.Campfire,flow.Current,"A hitch is clamped; the beat still plays");
        }
        [Test] public void CampSceneHoldsFiresAndCalmPettableSheep()
        {
            var fires=new[]{new Vector3(1,.7f,4),new Vector3(2,1,3)};var sheep=new[]{new Vector3(3,0,2.5f),new Vector3(.3f,0,2.5f)};
            var run=FireRun.CampScene(fires,sheep);
            Assert.AreEqual(2,run.Trees.Length);Assert.AreEqual(2,run.Animals.Length);Assert.IsFalse(run.BoardRules,"No wind for the camp VFX");
            Assert.AreEqual(fires[1],run.Trees[1].Position);Assert.AreEqual(TreeState.Safe,run.Trees[0].State);Assert.Zero(run.Burning);
            foreach(var a in run.Animals)
            {
                Assert.AreEqual(AnimalState.Rescued,a.State);Assert.IsFalse(FireRun.CanHold(a),"Camp sheep are never grabbed");
                Assert.IsTrue(FireRun.CanPet(a));Assert.GreaterOrEqual(a.StateTime,1,"No rescue coin at rest");
            }
            Assert.IsTrue(EmberdeepSheepInteraction.TryPet(run.Animals[0]));Assert.IsFalse(EmberdeepSheepInteraction.TryPet(run.Animals[0]),"cooldown");
            run.Animals[1].OnFire=true;Assert.IsFalse(EmberdeepSheepInteraction.TryPet(run.Animals[1]),"No petting a burning sheep");
        }
        [Test] public void CampFrameKeepsTheIslandInsideThePalmGestureBounds()
        {
            // EmberdeepPalmGesture rejects targets outside |x|<=5.5, 1.6<=z<=12 (arena units); the island spans x -2.6..2.5,
            // z -2.93..2.08 (measured CampGround bounds, island units).
            foreach(var corner in new[]{new Vector3(-2.6f,0,-2.93f),new Vector3(2.5f,0,2.08f),new Vector3(2.5f,0,-2.93f),new Vector3(-2.6f,0,2.08f)})
            {
                var p=EmberdeepCampPlay.ToFrame(corner,0);
                Assert.LessOrEqual(Mathf.Abs(p.x),5.5f);Assert.GreaterOrEqual(p.z,1.6f);Assert.LessOrEqual(p.z,12);
            }
        }
        [Test] public void FirstLaunchIsRememberedInPlayerPrefs()
        {
            int? saved=PlayerPrefs.HasKey(EmberdeepCamp.TutorialPrefsKey)?PlayerPrefs.GetInt(EmberdeepCamp.TutorialPrefsKey):null;
            try
            {
                EmberdeepCamp.ResetTutorialSeen();Assert.IsFalse(EmberdeepCamp.TutorialSeen);
                PlayerPrefs.SetInt(EmberdeepCamp.TutorialPrefsKey,1);Assert.IsTrue(EmberdeepCamp.TutorialSeen);
            }
            finally{if(saved.HasValue)PlayerPrefs.SetInt(EmberdeepCamp.TutorialPrefsKey,saved.Value);else PlayerPrefs.DeleteKey(EmberdeepCamp.TutorialPrefsKey);PlayerPrefs.Save();}
        }
    }
}

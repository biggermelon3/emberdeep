using CloudGarden.Integration;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using PhysicalAssembly;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace FireRescue.Tests
{
    public sealed class Emberdeep024FlowPlayTests
    {
        [UnityTest] public IEnumerator FailureAndDockingReleaseSheepSoHandsCanUseMenus()
        {
            EmberdeepSession.NewExpedition();yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);yield return null;yield return null;
            var game=Object.FindFirstObjectByType<FireRescueGame>();game.enabled=false;EmberdeepTestFlow.StartPrepared(game.Run);
            void HoldBoth()
            {
                FireToolManipulation.Hand H(int i)=>new(){Valid=true,Palm=new Pose(game.Arena.TransformPoint(EmberdeepSheepInteraction.Center(game.Run.Animals[i])),Quaternion.identity),Contact=game.Arena.TransformPoint(EmberdeepSheepInteraction.Center(game.Run.Animals[i]))};
                var left=H(0);var right=H(1);game.SheepInteraction.Step(game.Run,game.Arena,left,right,.1f);
                left.Closure=right.Closure=.7f;game.SheepInteraction.Step(game.Run,game.Arena,left,right,.1f);
                Assert.True(game.SheepInteraction.OwnsHand(0));Assert.True(game.SheepInteraction.OwnsHand(1));
            }
            HoldBoth();foreach(var tree in game.Run.Trees){tree.State=TreeState.Burnt;tree.Fuel=0;}
            game.Run.Tick(.1f,false);Assert.AreEqual(FirePhase.Lost,game.Run.Phase);
            game.SendMessage("ReadSheepHands",.1f);Assert.False(game.SheepInteraction.OwnsHand(0));Assert.False(game.SheepInteraction.OwnsHand(1));
            Assert.AreEqual(-1,game.Run.Animals[0].HeldBy);Assert.AreEqual(-1,game.Run.Animals[1].HeldBy);
            game.RetrySector();EmberdeepTestFlow.StartPrepared(game.Run);HoldBoth();
            for(int i=0;i<8;i++)game.Run.Supply.SampleDock(WaterSourceKind.Hydrant,0,0,0,true,.1f);
            Assert.True(game.Run.Supply.Locked);game.SendMessage("ReadSheepHands",.1f);
            Assert.False(game.SheepInteraction.OwnsHand(0));Assert.False(game.SheepInteraction.OwnsHand(1));
            yield return null;
        }
        [Test] public void PortableTipsKeepIndividualValidityThroughWorldTransform()
        {
            var space=new GameObject("Tracking origin");space.transform.position=Vector3.one;space.transform.rotation=Quaternion.Euler(0,90,0);
            try
            {
                var local=new PortableXRHandInput.Sample{TipMask=4,MiddleTip=Vector3.forward,GripAvailable=false,PinchAvailable=false};
                var world=local.ToWorld(space.transform);Assert.True(world.TryTip(2,out var p));Assert.AreEqual(space.transform.TransformPoint(Vector3.forward),p);
                Assert.False(world.TryTip(1,out _));Assert.False(world.TryTip(4,out _));
            }
            finally{Object.DestroyImmediate(space);}
        }
        [UnityTest] public IEnumerator RealStartButtonCountsDownPausesAndRetryButtonReplaysSettledLevel()
        {
            EmberdeepSession.NewExpedition();yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);yield return null;yield return null;
            var game=Object.FindFirstObjectByType<FireRescueGame>();Assert.NotNull(game);
            game.SendMessage("OnApplicationFocus",true);game.SendMessage("OnApplicationPause",false);
            Assert.AreEqual(FirePhase.Preparing,game.Run.Phase);Assert.Zero(game.Run.Elapsed);
            for(int i=0;i<20;i++)yield return null;Assert.AreEqual(FirePhase.Preparing,game.Run.Phase);Assert.Zero(game.Run.Elapsed);
            // LoopSpec §11b: the spell flow starts by striking the rim bell (was the dashboard START button).
            Assert.False(game.RayAllowed,"No ray in Plan");Assert.True(game.RimHud.BellVisible);
            Assert.True(game.StrikeBell());Assert.AreEqual(1,game.BellStrikes);
            Assert.Greater(game.Run.CountdownRemaining,2.9f);Assert.False(game.BeginIncident());
            game.SendMessage("OnApplicationPause",true);float remaining=game.Run.CountdownRemaining;
            for(int i=0;i<10;i++)yield return null;Assert.AreEqual(remaining,game.Run.CountdownRemaining);Assert.Zero(game.Run.Elapsed);
            game.SendMessage("OnApplicationPause",false);float until=Time.realtimeSinceStartup+7;
            while(game.Run.Phase==FirePhase.Preparing && Time.realtimeSinceStartup<until)yield return null;
            Assert.AreEqual(FirePhase.Fighting,game.Run.Phase);Assert.Less(game.Run.Elapsed,.2f);
            game.Run.ApplyWater(Vector3.zero,10000,100);game.Run.EvacuateSafeAnimals();Assert.AreEqual(100,game.Run.Coins);
            Assert.True(game.Run.BuyMagicUpgrade());game.Refresh();
            // The results panel's RETRY (was the dashboard RETRY SECTOR button).
            float shown=Time.realtimeSinceStartup+FireRescueGame.ResultsDelay+1;while(!game.ResultsVisible && Time.realtimeSinceStartup<shown)yield return null;
            Assert.True(game.ResultsVisible);Assert.True(game.RayAllowed,"The ray is back between levels");
            game.GetComponentsInChildren<WorkshopButton>().Single(b=>b.Command=="Results retry").Press();
            Assert.AreEqual(FirePhase.Preparing,game.Run.Phase);Assert.AreEqual(0,game.Run.Stage);Assert.AreEqual(25,game.Run.Coins);Assert.AreEqual(1,game.Run.MagicLevel);Assert.Zero(game.Run.Elapsed);
            EmberdeepTestFlow.StartPrepared(game.Run);game.Run.ApplyWater(Vector3.zero,10000,100);game.Run.EvacuateSafeAnimals();Assert.AreEqual(125,game.Run.Coins);
            game.StartNext();Assert.AreEqual(1,game.Run.Stage);Assert.AreEqual(FirePhase.Preparing,game.Run.Phase);Assert.Zero(game.Run.Elapsed);
            yield return null;
        }
    }
}

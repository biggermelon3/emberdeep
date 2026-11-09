using NUnit.Framework;
using UnityEngine;

namespace CloudGarden.Tests
{
    public sealed class GardenRulesTests
    {
        GardenSettings settings;
        [SetUp] public void SetUp() { settings = ScriptableObject.CreateInstance<GardenSettings>(); }
        [TearDown] public void TearDown() { Object.DestroyImmediate(settings); }

        [Test] public void RainOnlyReachesNodesInsideItsFootprint()
        {
            var model = new GardenModel(); model.Rain(GardenModel.Beds[0], .19f, .5f);
            Assert.AreEqual(.5f, model.plants[0].water, .001f); Assert.AreEqual(0, model.plants[1].water); Assert.AreEqual(0, model.pond);
        }
        [Test] public void WidenedCloudCanWaterBothBanks()
        {
            var model = new GardenModel(); model.Rain(new Vector2(0, -.12f), .46f, .7f);
            Assert.Greater(model.plants[0].water, .6f); Assert.Greater(model.plants[1].water, .6f);
        }
        [Test] public void NearlyEmptyCloudEmitsOnlyTheWaterItCanAfford()
        {
            var cloud = new CloudResourceState { moisture = .014f };
            float seconds = cloud.SpendRain(1, .07f);
            Assert.AreEqual(.2f, seconds, .0001f); Assert.AreEqual(0, cloud.moisture, .0001f);
            Assert.AreEqual(0, cloud.SpendRain(1, .07f));
        }
        [Test] public void StormMissDoesNotPowerTowerAndPlantsRecover()
        {
            var model = new GardenModel(); Assert.IsFalse(model.Strike(GardenModel.Beds[0], .19f));
            Assert.AreEqual(0, model.energy); Assert.Greater(model.plants[0].startled, 0);
            for (int i = 0; i < 80; i++) model.Tick(.05f, settings);
            Assert.AreEqual(0, model.plants[0].startled);
        }
        [Test] public void PumpNeedsBothWaterAndPower()
        {
            var model = new GardenModel(); model.Strike(GardenModel.Tower, .19f); model.Tick(1, settings);
            Assert.AreEqual(0, model.pumpedWater);
            model.pond = .8f;
            for (int i = 0; i < 100; i++) model.Tick(.05f, settings);
            Assert.Greater(model.pumpedWater, .18f); Assert.Greater(model.plants[0].water, 0); Assert.Less(model.energy, .55f);
        }
        [Test] public void EmptyGardenAlwaysRecoversMist()
        {
            var model = new GardenModel { vapor = 0, pond = 0, energy = 0 };
            for (int i = 0; i < 600; i++) model.Tick(.05f, settings);
            Assert.Greater(model.vapor, settings.seedCost);
        }
        [Test] public void SeedsConsumeResourcesAndRespectSkyCapacity()
        {
            var model = new GardenModel { vapor = 1 }; var mother = new CloudResourceState();
            Assert.IsFalse(model.TryGrowSeed(settings, settings.maxClouds, mother)); Assert.AreEqual(1, model.vapor);
            Assert.IsTrue(model.TryGrowSeed(settings, 1, mother));
            Assert.AreEqual(1 - settings.seedCost, model.vapor, .001f); Assert.Less(mother.mass, 1); Assert.AreEqual(1, model.seedsCreated);
        }
        [Test] public void SpringAloneDoesNotCountAsEcologicalReturnCycle()
        {
            var model = new GardenModel { vapor = 1 }; var mother = new CloudResourceState();
            model.TryGrowSeed(settings, 1, mother); Assert.AreEqual(0, model.cycles);
            model.Rain(GardenModel.Beds[0], .19f, .9f);
            for (int i = 0; i < 500; i++) model.Tick(.05f, settings);
            Assert.Greater(model.transpired, .2f);
            Assert.IsTrue(model.TryGrowSeed(settings, 2, mother)); Assert.AreEqual(1, model.cycles);
        }
        [Test] public void SnapshotDoesNotShareMutablePlantStates()
        {
            var model = new GardenModel(); var saved = model.Copy(); model.plants[0].water = 1;
            Assert.AreEqual(0, saved.plants[0].water);
        }
        [Test] public void TutorialCannotSkipUnmetGoals()
        {
            var model = new GardenModel();
            for (int i = 0; i < 6; i++) Assert.IsFalse(LevelGoalEvaluator.Complete(i, model, false, 0));
            Assert.IsTrue(LevelGoalEvaluator.Complete(0, model, true, 0));
            model.cycles = 3;
            Assert.IsFalse(LevelGoalEvaluator.Complete(5, model, true, 2));
            model.cycles = 4; Assert.IsTrue(LevelGoalEvaluator.Complete(5, model, true, 2));
        }
    }
}

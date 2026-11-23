using NUnit.Framework;

namespace FireRescue.Tests
{
    // Playtest 2026-11-22: SLOT and TOWER were for sale before the player had seen a tower (FireRun.TowersIntroduced).
    public sealed class EmberdeepUpgradeLockTests
    {
        static FireRun AtStage(int stage)
        {
            var run=new FireRun(true);
            typeof(FireRun).GetProperty("Stage").SetValue(run,stage);run.LevelOverride=EmberdeepLevels.GeneratedGrid(stage);
            Assert.True(run.StartStage(true));
            typeof(FireRun).GetProperty("Coins").SetValue(run,9999);
            return run;
        }

        [Test] public void TowerStageIsTheFirstLevelThatHandsOutTowers()
        {
            Assert.Greater(EmberdeepLevels.Stages[FireRun.TowerStage].Pieces.Towers,0);
            for(int s=0;s<FireRun.TowerStage;s++)Assert.Zero(EmberdeepLevels.Stages[s].Pieces.Towers,"stage "+s);
        }

        [Test] public void SlotAndTowerStayLockedBeforeTheTowerLevel()
        {
            for(int stage=0;stage<FireRun.TowerStage;stage++)
            {
                var run=AtStage(stage);
                Assert.False(run.TowersIntroduced,"stage "+stage);
                Assert.False(run.CanUpgradeSlot,"stage "+stage);Assert.False(run.BuySlotUpgrade(),"stage "+stage);
                Assert.False(run.CanUpgradeTower,"stage "+stage);Assert.False(run.BuyTowerUpgrade(),"stage "+stage);
                Assert.Zero(run.SlotLevel);Assert.Zero(run.TowerLevel);
            }
        }

        [Test] public void SlotAndTowerOpenAtTheTowerLevelAndAfter()
        {
            for(int stage=FireRun.TowerStage;stage<EmberdeepLevels.Stages.Length;stage++)
            {
                var run=AtStage(stage);
                Assert.True(run.TowersIntroduced,"stage "+stage);
                Assert.True(run.CanUpgradeSlot,"stage "+stage);Assert.True(run.CanUpgradeTower,"stage "+stage);
            }
        }
    }
}

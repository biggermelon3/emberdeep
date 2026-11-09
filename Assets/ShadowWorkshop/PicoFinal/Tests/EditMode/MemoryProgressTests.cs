using NUnit.Framework;

namespace ShapeOfMemory.Tests
{
    public class MemoryProgressTests
    {
        [Test] public void LevelsOpenInOrderAndAllStayOpenAfterOneRun()
        {
            var p=new MemoryProgress(new MemoryProgress.InMemory());
            Assert.AreEqual(new[]{"Birthday","BonVoyage","GoodMorning","Home"},MemoryProgress.Levels);
            Assert.IsTrue(p.Unlocked(0));Assert.IsFalse(p.Unlocked(1));Assert.AreEqual(0,p.Continue);
            p.Solve(0);Assert.IsTrue(p.Unlocked(1));Assert.IsFalse(p.Unlocked(2));Assert.AreEqual(1,p.Continue);
            for(int i=1;i<p.Count;i++)p.Solve(i);
            Assert.AreEqual(-1,p.Continue,"every memory found");Assert.AreEqual(4,p.SolvedCount);
            for(int i=0;i<p.Count;i++)Assert.IsTrue(p.Unlocked(i));
        }
        [Test] public void HintsKeepTheMostUsedAndLiftIsClamped()
        {
            var store=new MemoryProgress.InMemory();var p=new MemoryProgress(store);
            p.UsedHints(2,1);p.UsedHints(2,2);p.UsedHints(2,1);p.UsedHints(2,9);Assert.AreEqual(2,p.Hints(2));Assert.AreEqual(0,p.Hints(1));
            p.Lift=.05f;Assert.AreEqual(.05f,p.Lift,1e-4f);p.Lift=-1;Assert.AreEqual(-MemoryProgress.MaxLift,p.Lift,1e-4f);
            Assert.AreEqual(-MemoryProgress.MaxLift,new MemoryProgress(store).Lift,1e-4f,"read back from the store");
        }
    }
}

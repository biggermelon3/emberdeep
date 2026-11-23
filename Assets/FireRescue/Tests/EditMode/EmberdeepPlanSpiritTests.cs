using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Lines=FireRescue.EmberdeepSpiritLines;

namespace FireRescue.Tests
{
    // Plan-phase voice-over (2026-11-22): one line per topic level, once per install, only with its clip; SHOW HINTS /
    // How to play (EmberdeepFirstHints.Reset) bring them back.
    public sealed class EmberdeepPlanSpiritTests
    {
        int?[] saved;int[] hints;
        static readonly EmberdeepFirstHints.Move[] Moves=(EmberdeepFirstHints.Move[])System.Enum.GetValues(typeof(EmberdeepFirstHints.Move));
        [SetUp] public void Save()
        {
            saved=Lines.Plan.Select(l=>PlayerPrefs.HasKey(EmberdeepPlanSpirit.Key(l))?(int?)PlayerPrefs.GetInt(EmberdeepPlanSpirit.Key(l)):null).ToArray();
            hints=Moves.Select(m=>PlayerPrefs.GetInt(EmberdeepFirstHints.Key(m),-1)).ToArray();
            EmberdeepPlanSpirit.ResetAll();
        }
        [TearDown] public void Restore()
        {
            for(int i=0;i<Lines.Plan.Length;i++){var k=EmberdeepPlanSpirit.Key(Lines.Plan[i]);if(saved[i].HasValue)PlayerPrefs.SetInt(k,saved[i].Value);else PlayerPrefs.DeleteKey(k);}
            for(int i=0;i<Moves.Length;i++)if(hints[i]<0)PlayerPrefs.DeleteKey(EmberdeepFirstHints.Key(Moves[i]));else PlayerPrefs.SetInt(EmberdeepFirstHints.Key(Moves[i]),hints[i]);
            PlayerPrefs.Save();
        }
        [Test] public void TowerHillAndWindyRidgeEachHaveTheirLine()
        {
            Assert.AreEqual("TOWER HILL",FireRun.LevelNames[EmberdeepPlanSpirit.TowerStage]);
            Assert.AreEqual("WINDY RIDGE",FireRun.LevelNames[EmberdeepPlanSpirit.WindStage]);
            Assert.AreSame(Lines.PlanTower,EmberdeepPlanSpirit.LineFor(EmberdeepPlanSpirit.TowerStage));
            Assert.AreSame(Lines.PlanWind,EmberdeepPlanSpirit.LineFor(EmberdeepPlanSpirit.WindStage));
            foreach(int other in new[]{0,1,4,5})Assert.IsNull(EmberdeepPlanSpirit.LineFor(other),"stage "+other);
            foreach(var l in Lines.Plan)Assert.IsFalse(Lines.All.Contains(l),l.Id+" stays out of the camp's list");
        }
        [Test] public void ALineIsSaidOnceAndOnlyWithItsClip()
        {
            System.Func<EmberdeepSpiritLine,bool> recorded=_=>true,missing=_=>false;
            Assert.IsNull(EmberdeepPlanSpirit.Due(EmberdeepPlanSpirit.TowerStage,missing),"no clip, no line (ship-safe)");
            Assert.AreSame(Lines.PlanTower,EmberdeepPlanSpirit.Due(EmberdeepPlanSpirit.TowerStage,recorded));
            EmberdeepPlanSpirit.MarkHeard(Lines.PlanTower);
            Assert.IsNull(EmberdeepPlanSpirit.Due(EmberdeepPlanSpirit.TowerStage,recorded),"once per install");
            Assert.AreSame(Lines.PlanWind,EmberdeepPlanSpirit.Due(EmberdeepPlanSpirit.WindStage,recorded),"each topic on its own");
            Assert.IsNull(EmberdeepPlanSpirit.Due(1,recorded));
        }
        [Test] public void TheLevelSelectAndUpgradesHoldTheSpiritBack()
        {
            // The board behind the level select (results NEXT, browsing) and the upgrades is already in Plan.
            Assert.IsTrue(EmberdeepPlanSpirit.BoardInView(true,false,false),"Plan, nothing over the board");
            Assert.IsFalse(EmberdeepPlanSpirit.BoardInView(true,true,false),"level select open");
            Assert.IsFalse(EmberdeepPlanSpirit.BoardInView(true,false,true),"upgrades open");
            Assert.IsFalse(EmberdeepPlanSpirit.BoardInView(false,false,false),"not in Plan");
        }
        [Test] public void ALineCutByAScreenComesBackButNotOneCutByTheBell()
        {
            System.Func<EmberdeepSpiritLine,bool> recorded=_=>true;
            EmberdeepPlanSpirit.MarkHeard(Lines.PlanTower); // as the line starts
            Assert.IsTrue(EmberdeepPlanSpirit.CutByScreen(true,true,false),"level select mid-line");
            Assert.IsTrue(EmberdeepPlanSpirit.CutByScreen(true,false,true),"upgrades mid-line");
            Assert.IsFalse(EmberdeepPlanSpirit.CutByScreen(false,true,false),"the line had ended: heard");
            Assert.IsFalse(EmberdeepPlanSpirit.CutByScreen(true,false,false),"the bell or leaving the level: heard");
            EmberdeepPlanSpirit.Forget(Lines.PlanTower);
            Assert.AreSame(Lines.PlanTower,EmberdeepPlanSpirit.Due(EmberdeepPlanSpirit.TowerStage,recorded),"due again after a cut");
        }
        [Test] public void ShowHintsBringsThePlanLinesBack()
        {
            EmberdeepPlanSpirit.Hook();EmberdeepPlanSpirit.Hook(); // idempotent
            foreach(var l in Lines.Plan)EmberdeepPlanSpirit.MarkHeard(l);
            EmberdeepFirstHints.ResetAll();
            foreach(var l in Lines.Plan)Assert.IsFalse(EmberdeepPlanSpirit.Heard(l),l.Id);
        }
        [Test] public void ThePlanLinesAreRecordedOrNotAtAll()
        {
            // Ship-safe: with the real Resources/EmberdeepVoice, a plan line is due exactly when its clip exists.
            System.Func<EmberdeepSpiritLine,bool> real=x=>Resources.Load<AudioClip>(x.Resource);
            Assert.AreEqual(real(Lines.PlanTower),EmberdeepPlanSpirit.Due(EmberdeepPlanSpirit.TowerStage,real)!=null,"plan.tower");
            Assert.AreEqual(real(Lines.PlanWind),EmberdeepPlanSpirit.Due(EmberdeepPlanSpirit.WindStage,real)!=null,"plan.wind");
        }
    }
}

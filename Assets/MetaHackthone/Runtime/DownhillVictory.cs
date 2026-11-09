using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PhysicalAssembly
{
    public sealed partial class DownhillController
    {
        public float VictoryDecomposeSeconds=.25f, VictoryTravelSeconds=.9f, VictoryReconstructSeconds=.35f;
        public bool VictoryInProgress { get; private set; }
        public SandboxRewardCoordinator Reward { get; private set; }
        public IReadOnlyDictionary<AssemblyPart,AssemblyPart> SimulationOriginals => simulationOriginals;
        readonly Dictionary<AssemblyPart,AssemblyPart> simulationOriginals=new();
        readonly List<SandboxReturnEffect> victoryEffects=new();
        readonly List<AssemblyPart> winningParts=new();
        bool rewardsSuppressed;
        string VictoryMessage => CurrentCourse==SandboxCourseId.GravityGarage?
            "LEVEL 1 COMPLETE! Drill reward · Open your palm menu for Next Level.":"ALL LEVELS COMPLETE! Build another car to play again.";

        void BeginVictoryReturn()
        {
            VictoryInProgress=true; State=DownhillState.Transition;
            RestoreClock(); FreezeSimulation(); CompleteBoundaryReturns();
            Workshop.World.EditingEnabled=false;
            winningParts.Clear(); winningParts.AddRange(simulationOriginals.Values);
            var ids=new HashSet<string>(winningParts.Select(p=>p.InstanceId));
            foreach(var edge in Workshop.World.Connections.Where(e=>ids.Contains(e.a.partId)||ids.Contains(e.b.partId)).ToArray())
                Workshop.World.RemoveConnection(edge.id);
            // Original bodies are still inactive; place each body, including articulated parts,
            // before taking destination visual poses. Unused palette parts retain saved poses.
            foreach(var part in winningParts) Restore(spawns[part]);
            saved.RemoveAll(p=>winningParts.Contains(p.Body.Part));
            saved.AddRange(winningParts.SelectMany(p=>spawns[p]));
            foreach(var pair in simulationOriginals) {
                var effect=new GameObject("Victory return "+pair.Value.name).AddComponent<SandboxReturnEffect>();
                effect.transform.SetParent(transform,false);
                var pose=spawns[pair.Value].First(p=>p.Body==pair.Value.MainBody);
                effect.Initialize(pair.Key,pair.Value,Course.WorkbenchFrame,pose.Position,pose.Rotation,
                    VictoryDecomposeSeconds,VictoryTravelSeconds,VictoryReconstructSeconds,
                    Mathf.Max(1,SandboxFragments.Budget/Mathf.Max(1,winningParts.Count)));
                victoryEffects.Add(effect);
            }
            transition=StartCoroutine(VictoryReturn());
        }
        IEnumerator VictoryReturn()
        {
            var courseStart=Course.SandboxFrame.localPosition;
            float age=0, total=VictoryDecomposeSeconds+VictoryTravelSeconds+VictoryReconstructSeconds;
            bool complete=false;
            while(!complete) {
                yield return null;
                age+=Time.unscaledDeltaTime; complete=true;
                foreach(var effect in victoryEffects) if(effect && !effect.Advance(Time.unscaledDeltaTime)) complete=false;
                Presentation.SetCoursePosition(Vector3.Lerp(courseStart,Presentation.BuildPosition,Mathf.SmoothStep(0,1,age/Mathf.Max(.01f,total))));
            }
            transition=null;
            FinishVictoryReturn(true);
        }
        void FinishVictoryReturn(bool completed)
        {
            if(!VictoryInProgress) return;
            VictoryInProgress=false;
            foreach(var effect in victoryEffects) if(effect) { effect.Restore(); Destroy(effect.gameObject); }
            victoryEffects.Clear();
            foreach(var part in winningParts) if(part) Restore(spawns[part]);
            winningParts.Clear();
            if(!completed) return;
            RestoreBuildImmediately();
            Workshop.Session.Notify(VictoryMessage);
            EnsureReward();
        }
        void EnsureReward()
        {
            if(rewardsSuppressed || !Course.SandboxPolish || !Settings.Campaign || DesignModeSession.Enabled || !IsBuilding ||
                CurrentCourse!=SandboxCourseId.GravityGarage || !SandboxCampaignSession.FirstComplete || SandboxCampaignSession.DrillClaimed) return;
            if(!Reward) { Reward=gameObject.AddComponent<SandboxRewardCoordinator>(); Reward.Initialize(this); }
            Reward.Reveal();
        }
        public Transform RewardGoal => GarageCourse && GarageCourse.Markers && GarageCourse.Markers.Finish?
            GarageCourse.Markers.Finish:Course.FinishArea?Course.FinishArea.transform:Course.SandboxFrame;
    }
}

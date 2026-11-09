#if DEVELOPMENT_BUILD && UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using PhysicalAssembly;
using PhysicalAssembly.MetaXR;
using UnityEngine;

namespace CloudGarden.Integration
{
    public sealed partial class QuestSandboxSmokeTest
    {
        IEnumerator RunVictory()
        {
            float deadline=Time.realtimeSinceStartup+30;
            while(!GardenNavigation.Current && Time.realtimeSinceStartup<deadline) yield return null;
            var nav=GardenNavigation.Current; Require(nav,"Navigation missing");
            DesignModeSession.SetEnabled(false); nav.EnterSandbox(); deadline=Time.realtimeSinceStartup+30;
            while((nav.IsTransitioning || !nav.DownhillWorkshop) && Time.realtimeSinceStartup<deadline) yield return null;
            Require(nav.DownhillWorkshop,"Sandbox missing");
            var level=nav.DownhillWorkshop.Downhill;
            yield return new WaitForSecondsRealtime(2);
            Require(level.Course.SandboxPolish && level.PalmMenu,"Polished campaign missing");
            Require(level.TryResetBuild(),"Cannot reset fixture");
            var spawns=level.Kit.ToDictionary(p=>p,p=>p.Bodies.Select(b=>new Pose(b.transform.position,b.transform.rotation)).ToArray());
            DesignModeSession.SetEnabled(true); bool assembled=level.TryLoadDesignerExample(); DesignModeSession.SetEnabled(false);
            Require(assembled,"Cannot assemble fixture");
            var adapter=level.Workshop.GetComponent<MetaHandInputAdapter>(); if(adapter) adapter.enabled=false;
            Require(level.TryStart(),"Cannot start fixture"); deadline=Time.realtimeSinceStartup+15;
            while(level.State!=DownhillState.Running && Time.realtimeSinceStartup<deadline) yield return null;
            Require(level.State==DownhillState.Running,"Countdown did not finish");
            var returned=level.SimulationOriginals.Values.ToArray();
            // Isolate the celebration path from driving skill. Report the injected outcome.
            var shift=level.RewardGoal.position-level.SimulationCore.MainBody.Rigidbody.position;
            foreach(var b in level.SimulationWorld.Parts.Values.SelectMany(p=>p.Bodies)) {
                b.Rigidbody.position+=shift; b.transform.position=b.Rigidbody.position;
            }
            Physics.SyncTransforms();
            Debug.Log(Prefix+"VICTORY_BEGIN injected_success=true");
            typeof(DownhillController).GetMethod("Resolve",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(level,new object[]{true});
            Require(level.VictoryInProgress,"Celebration missing");
            Require(!level.TryRetry() && !level.TryReturn() && !level.TryStart() && !level.TryResetBuild() && !level.TryAdvanceLevel(),"Transition accepted competing action");
            var frames=new System.Collections.Generic.List<float>(); int peak=0;
            while(level.VictoryInProgress) {
                frames.Add(Time.unscaledDeltaTime*1000); peak=Mathf.Max(peak,SandboxFragments.ActiveCount);
                Require(peak<=SandboxFragments.Budget,"Fragment budget exceeded"); yield return null;
            }
            Require(level.IsBuilding && !level.SimulationWorld && level.Workshop.World.Connections.Count==0,"Return did not restore separate build parts");
            foreach(var part in returned) for(int i=0;i<part.Bodies.Length;i++) {
                var body=part.Bodies[i]; var expected=spawns[part][i];
                Require(Vector3.Distance(body.transform.position,expected.position)<.002f,"Wrong returned position: "+part.name);
                Require(Quaternion.Angle(body.transform.rotation,expected.rotation)<.2f,"Wrong returned rotation: "+part.name);
            }
            var drill=level.Kit.Single(p=>p.Kind==PartKind.DownhillDrill);
            Require(!drill.gameObject.activeSelf && !level.Workshop.World.Parts.ContainsKey(drill.InstanceId),"Drill unlocked early");
            Require(level.Reward && level.Reward.Preview,"Reward missing");
            Stage("Victory-goal");
            yield return new WaitForSecondsRealtime(4);
            var camera=FindObjectsByType<Camera>(FindObjectsSortMode.None).First(c=>c.enabled && c.stereoEnabled);
            var menu=level.PalmMenu;
            for(int pass=0;pass<2;pass++) {
                float until=Time.realtimeSinceStartup+5;
                bool captured=false;
                while(Time.realtimeSinceStartup<until) {
                    menu.Sample(true,camera.transform.position+camera.transform.forward*.45f-Vector3.up*.3f,Vector3.up,camera.transform.position,Time.unscaledDeltaTime,3);
                    if(menu.IsOpen && level.Reward.State==SandboxRewardState.AtMenu && !captured) { Stage("Victory-menu-"+pass); captured=true; }
                    frames.Add(Time.unscaledDeltaTime*1000); yield return null;
                }
                Require(captured,"Menu preview did not arrive");
                Require(!SandboxCampaignSession.DrillClaimed,"Opening menu acknowledged reward");
                menu.Sample(true,camera.transform.position-Vector3.up*.3f,Vector3.down,camera.transform.position,.21f,0);
                Require(!menu.IsOpen,"Palm-down did not close");
                yield return new WaitForSecondsRealtime(.6f);
            }
            frames.Sort();
            Debug.Log(Prefix+"VICTORY_FRAME_TIME median_ms="+frames[frames.Count/2]+" p95_ms="+frames[(int)(frames.Count*.95f)]+" max_ms="+frames.Last()+" peak_fragments="+peak);
            Require(runtimeErrors==0,"Runtime errors: "+runtimeErrors);
            if(adapter) adapter.enabled=true;
            Debug.Log(Prefix+"VICTORY_PASS synthetic_gestures=true manual_palm_touch_and_both_eye_review_required");
        }
    }
}
#endif

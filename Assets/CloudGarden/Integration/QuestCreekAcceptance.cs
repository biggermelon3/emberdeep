#if DEVELOPMENT_BUILD && UNITY_ANDROID && !UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PhysicalAssembly;
using PhysicalAssembly.MetaXR;
using UnityEngine;
namespace CloudGarden.Integration
{
    public sealed partial class QuestSandboxSmokeTest
    {
        IEnumerator RunCreek()
        {
            float deadline=Time.realtimeSinceStartup+30;
            while(!GardenNavigation.Current && Time.realtimeSinceStartup<deadline)yield return null;
            var nav=GardenNavigation.Current;Require(nav,"Navigation missing");
            DesignModeSession.SetEnabled(false);SandboxCampaignSession.Reset();nav.EnterSandbox();
            deadline=Time.realtimeSinceStartup+30;
            while((nav.IsTransitioning || !nav.DownhillWorkshop)&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(nav.DownhillWorkshop,"Sandbox missing");var level=nav.DownhillWorkshop.Downhill;
            var adapter=level.Workshop.GetComponent<MetaHandInputAdapter>();bool enabled=adapter && adapter.enabled;
            if(adapter)adapter.enabled=false;
            try {
                for(int course=0;course<2;course++) {
                    Require(course==0?!level.Kit.Single(p=>p.Kind==PartKind.DownhillDrill).gameObject.activeSelf:SandboxCampaignSession.DrillClaimed,"Drill progression wrong");
                    var theme=(course==0?level.GarageCourse.transform:level.Course3D.transform).GetComponent<CreekCampaignTheme>();
                    Require(theme && theme.Visuals && theme.Visuals.GetComponentsInChildren<Collider>(true).Length==0,"Invalid environment");
                    Stage("Creek-level-"+(course+1));yield return new WaitForSecondsRealtime(4);
                    DesignModeSession.SetEnabled(true);DesignModeSession.SelectLevel((DesignLevelId)course);
                    bool assembled=level.TryLoadDesignerExample();DesignModeSession.SetEnabled(false);Require(assembled,"Preset failed");Require(level.TryStart(),"Launch failed");
                    var frames=new List<float>();deadline=Time.realtimeSinceStartup+90;
                    while(!level.VictoryInProgress && level.State!=DownhillState.Success && level.State!=DownhillState.Failed && Time.realtimeSinceStartup<deadline){frames.Add(Time.unscaledDeltaTime*1000);yield return null;}
                    Require(level.VictoryInProgress || level.State==DownhillState.Success,"Preset did not finish: "+level.Workshop.Session.Status);
                    deadline=Time.realtimeSinceStartup+8;
                    while(level.VictoryInProgress && Time.realtimeSinceStartup<deadline){frames.Add(Time.unscaledDeltaTime*1000);yield return null;}
                    Require(!level.VictoryInProgress,"Return timed out");frames.Sort();
                    Debug.Log(Prefix+"CREEK_LEVEL_PASS level="+(course+1)+" synthetic_success=false median_ms="+frames[frames.Count/2]+" p95_ms="+frames[(int)(frames.Count*.95f)]+" max_ms="+frames.Last());
                    if(course==0){Require(level.CanAdvanceLevel,"Next Level locked");Require(level.TryAdvanceLevel(),"Next Level failed");}
                }
                Require(runtimeErrors==0,"Runtime errors during acceptance");Debug.Log(Prefix+"CREEK_PASS");
            } finally {DesignModeSession.SetEnabled(false);if(adapter)adapter.enabled=enabled;}
        }
    }
}
#endif

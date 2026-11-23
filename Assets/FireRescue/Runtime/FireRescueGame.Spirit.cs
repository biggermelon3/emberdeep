using System.Collections.Generic;
using UnityEngine;

namespace FireRescue
{
    // Plan-phase voice-over in the fireground (owner, 2026-11-22). The camp's forest spirit (EmberdeepForestSpirit: orb,
    // voice, subtitles, ducking) appears when the Plan phase of TOWER HILL or WINDY RIDGE starts, says that topic's
    // line once (EmberdeepPlanSpirit), and goes when the Plan ends (the bell, or leaving the level).
    // - "Starts" means the player sees the Plan: never under the level select or the upgrades (the board behind them is
    //   already in Plan). It waits for them to close; if one opens mid-line, the spirit goes and the line is not heard.
    // Ship-safe:
    // - PlanVoice turns the whole path off.
    // - A line without its clip never plays; with no line due, no spirit is created and nothing else in the fireground
    //   changes.
    // Placement, level frame (Arena), like the hints:
    // - it comes up from the board's back corner on the water-hand side, and hovers over the back edge on that side,
    //   PlanSpiritBelowEye under the eye;
    // - its subtitle hangs 11 cm under it (EmberdeepForestSpirit.SubtitleBelow);
    // - this keeps it clear of the bell (field centre), the tray (one centred row on the front lip) and the weathervane
    //   (x 0, behind);
    // - board session check (2026-11-22, default Tilted seat): the PLAN banner card sits over the tray, 32-41° down,
    //   x ~0. This band is ~24° down, -34..-6° across, so they are >= 6° apart (EmberdeepPlanBanner.cs).
    public sealed partial class FireRescueGame
    {
        public const bool PlanVoice=true;
        public const float PlanSpiritBelowEye=.25f,PlanSpiritSide=.85f,PlanSpiritBack=.6f; // m, fraction of the half width, arena units
        public EmberdeepForestSpirit PlanSpirit {get;private set;}
        // The line this Plan phase is saying or said (null: none due).
        public EmberdeepSpiritLine PlanLine {get;private set;}
        EmberdeepSpiritLine planSay;
        bool planWasOn;
        static readonly Dictionary<string,bool> planClips=new();
        // Fresh each play session (a recording imported mid-session is picked up even with domain reload off).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ForgetPlanClips()=>planClips.Clear();
        static bool PlanClip(EmberdeepSpiritLine line)
        {
            if(!planClips.TryGetValue(line.Id,out var has)){has=Resources.Load<AudioClip>(line.Resource);planClips[line.Id]=has;}
            return has;
        }

        void RefreshSpirit()
        {
            float dt=Mathf.Min(Time.unscaledDeltaTime,.1f);
            // Not under the level select or the upgrades: both stand over a board that is already in Plan.
            bool plan=PlanVoice && RimUi && EmberdeepPlanSpirit.BoardInView(Run.Phase==FirePhase.Preparing && PlanFrozen,LevelSelectOpen,ShopOpen);
            if(plan && !planWasOn)
            {
                // The Plan phase just started: the topic's line if it is due and recorded.
                PlanLine=planSay=EmberdeepPlanSpirit.Due(Run.Stage,PlanClip);
                if(planSay!=null)
                {
                    if(!PlanSpirit){PlanSpirit=new GameObject("Plan forest spirit").AddComponent<EmberdeepForestSpirit>();PlanSpirit.transform.SetParent(transform,false);}
                    PlanSpirit.Silence();PlanSpirit.Appear();
                }
            }
            else if(!plan && planWasOn)
            {
                // A line not yet started was never marked; one cut mid-speech by a screen is unmarked again.
                if(PlanSpirit && PlanLine!=null && EmberdeepPlanSpirit.CutByScreen(PlanSpirit.Line==PlanLine,LevelSelectOpen,ShopOpen))
                    EmberdeepPlanSpirit.Forget(PlanLine);
                planSay=null;
                if(PlanSpirit){PlanSpirit.Silence();PlanSpirit.Leave();}
            }
            planWasOn=plan;
            if(!PlanSpirit)return;
            // One line, once the orb has settled; it counts as heard as it starts.
            if(planSay!=null && PlanSpirit.Phase==EmberdeepForestSpirit.State.Hovering && !PlanSpirit.Busy)
            {PlanSpirit.Say(planSay);EmberdeepPlanSpirit.MarkHeard(planSay);planSay=null;}
            var cam=Player?Player.ViewCamera:null;
            bool world=cam && Arena && Arena.gameObject.activeInHierarchy && focused && !paused && !acquiringPose && !returningToCamp && !QuitConfirmationOpen;
            bool live=world && !PauseOpen;
            if(!world || !PlanSpiritPoints(cam,out var home,out var hover)){PlanSpirit.Tick(dt,false,live,cam,default,default,Vector3.up);return;}
            PlanSpirit.Tick(dt,true,live,cam,home,hover,Vector3.up);
        }
        // Home: the board's back corner on the water-hand side, at board level. Hover: over the back edge on that side,
        // PlanSpiritBelowEye under the eye.
        bool PlanSpiritPoints(Camera cam,out Vector3 home,out Vector3 hover)
        {
            home=hover=default;
            if(Run.Trees==null || Run.Trees.Length==0)return false;
            float minX=float.MaxValue,maxX=float.MinValue,maxZ=float.MinValue;
            foreach(var t in Run.Trees){minX=Mathf.Min(minX,t.Position.x);maxX=Mathf.Max(maxX,t.Position.x);maxZ=Mathf.Max(maxZ,t.Position.z);}
            float edgeX=waterLeft?minX:maxX,mid=(minX+maxX)*.5f;
            home=Arena.TransformPoint(new Vector3(edgeX,0,maxZ+PlanSpiritBack));
            var over=Arena.TransformPoint(new Vector3(mid+(edgeX-mid)*PlanSpiritSide,0,maxZ));
            hover=new Vector3(over.x,cam.transform.position.y-PlanSpiritBelowEye,over.z);
            return true;
        }
    }
}

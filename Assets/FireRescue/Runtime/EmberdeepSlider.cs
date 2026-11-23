using System;
using PhysicalAssembly;
using UnityEngine;

namespace FireRescue
{
    // Headset test 3 (2026-11-22): every continuous option is a slider you grab and drag (camp settings: height, distance,
    // music, sound; the pause panel: height, distance). The owner builds the look and registers the slider with its
    // EmberdeepHandRayMenu (AddSlider), which runs the drag:
    //  - grab: the ray's pinch click on the track (EmberdeepPinchClick: an armed closing edge after the dwell, so a pinch
    //    held while dragging onto another control never clicks it), or a fingertip touching the track when the panel is
    //    within reach (Poke);
    //  - drag: the value follows the ray's point on the track line (or the fingertip), snapped to whole steps; the owner
    //    applies it live (Drag01: the board re-places through EmberdeepPlayArea.Changed);
    //  - release: the pinch's ordinary release (EmberdeepPinchClick.Closed falling, seen for RearmSeconds), a fingertip
    //    lifted off, or the hand lost for LostSeconds. The value then commits (Commit: PlayerPrefs.Save).
    public sealed class EmberdeepSlider
    {
        public string Name;
        // The grab target: the track plus a margin, a hand-ray button like any other.
        public WorkshopButton Track;
        // The knob travels Left..Right along Space's local x, at height Y, on the plane z = Face (the viewer is on -z).
        public Transform Space;
        public float Left,Right,Y,Face;
        // A fingertip touches within this of Y (Space units) and of the ends.
        public float HalfHeight=.03f;
        // Whole steps over the track (height/distance: 1 cm each; volume: 5%); 0 = continuous.
        public int Steps;
        public Func<float> Get01;
        public Action<float> Drag01;
        public Action Commit;

        // Where the ray meets the track's plane, along the knob's travel (0..1, unclamped).
        public bool Pointer01(Ray ray,out float t)
        {
            t=0;
            if(!Space || Mathf.Approximately(Left,Right))return false;
            var o=Space.InverseTransformPoint(ray.origin);var d=Space.InverseTransformVector(ray.direction);
            if(Mathf.Abs(d.z)<1e-6f)return false;
            float along=(Face-o.z)/d.z;if(!(along>0))return false;
            t=(o.x+d.x*along-Left)/(Right-Left);return float.IsFinite(t);
        }
        // The ray's point on the track's plane (the cursor while dragging).
        public bool PointOnTrack(Ray ray,out Vector3 world)
        {
            world=default;
            if(!Space)return false;
            var o=Space.InverseTransformPoint(ray.origin);var d=Space.InverseTransformVector(ray.direction);
            if(Mathf.Abs(d.z)<1e-6f)return false;
            float along=(Face-o.z)/d.z;if(!(along>0))return false;
            world=Space.TransformPoint(o+d*along);return true;
        }
        // A fingertip: where it sits along the travel (unclamped), how far in front of the track's face it is (metres,
        // + toward the viewer, - pushed through), and whether it is over the track's band.
        public bool Poke01(Vector3 tip,out float t,out float depth)
        {
            t=0;depth=float.PositiveInfinity;
            if(!Space || Mathf.Approximately(Left,Right) || !float.IsFinite(tip.sqrMagnitude))return false;
            var local=Space.InverseTransformPoint(tip);
            depth=Vector3.Dot(tip-Space.TransformPoint(new Vector3(local.x,local.y,Face)),-Space.forward);
            t=(local.x-Left)/(Right-Left);
            float margin=HalfHeight/Mathf.Abs(Right-Left);
            return Mathf.Abs(local.y-Y)<=HalfHeight && t>=-margin && t<=1+margin;
        }
    }

    // One slider drag (pure state, EditMode-tested). Values are 0..1 along the track.
    public sealed class EmberdeepSliderDrag
    {
        public enum Source{None,Ray,Poke}
        // A step changes once the pointer is StepHysteresis steps past the current one: ray jitter (a few mm at arm's
        // length) cannot flicker a step at a boundary.
        public const float StepHysteresis=.65f;
        // A grab within KnobGrab of the knob keeps the knob where it is under the pinch (no jump); elsewhere on the track
        // the knob jumps to the pinch.
        public const float KnobGrab=.05f;
        // The fingers start to open (and the ray to dip) before the release counts: the value committed is the one from
        // ReleaseLead before the release began (ray drags; never earlier than the grab).
        public const float ReleaseLead=.08f;
        public const float LostSeconds=.2f;
        // Poke (metres in front of the track's face): touches at PokeTouch, lifts off past PokeLift; a tip pushed more than
        // PokeThrough through the panel is a hand behind it, not a touch.
        public const float PokeTouch=.012f,PokeLift=.03f,PokeThrough=.05f;
        const int History=24;
        public bool Active {get;private set;}
        public int Hand {get;private set;}=-1;
        public Source By {get;private set;}
        public float Value01 {get;private set;}
        public int Steps {get;private set;}
        public bool Releasing=>releasing;
        float offset,clock,releaseFor,lostFor,releaseValue;
        bool releasing;
        readonly float[] times=new float[History],values=new float[History];
        int count,head;

        // Starts a drag; true when the value moved (a grab off the knob jumps it to the pinch).
        public bool Begin(int hand,Source by,float pointer01,float value01,int steps)
        {
            Active=true;Hand=hand;By=by;Steps=Mathf.Max(0,steps);clock=releaseFor=lostFor=0;releasing=false;count=head=0;
            float start=Snap(Mathf.Clamp01(float.IsFinite(value01)?value01:0));Value01=start;
            if(!float.IsFinite(pointer01))offset=0;
            else if(Mathf.Abs(pointer01-start)<=KnobGrab)offset=start-pointer01;
            else{offset=0;Value01=Snap(Mathf.Clamp01(pointer01));}
            Record();
            return !Mathf.Approximately(Value01,start);
        }
        // The pointer moved along the track; true when the value changed. Frozen while a release is being confirmed.
        public bool Move(float pointer01)
        {
            if(!Active || releasing || !float.IsFinite(pointer01))return false;
            float raw=Mathf.Clamp01(pointer01+offset),next;
            if(Steps>0)
            {
                if(Mathf.Abs(raw-Value01)*Steps<StepHysteresis)return false;
                next=Snap(raw);
            }
            else next=raw;
            if(Mathf.Approximately(next,Value01))return false;
            Value01=next;return true;
        }
        // Once per frame after Move. tracked: the hand (or fingertip) is seen; held: still pinched (still touching).
        // False on the frame the drag ends; Value01 is then the value to commit.
        public bool Tick(bool tracked,bool held,float dt)
        {
            if(!Active)return false;
            float step=float.IsFinite(dt)?Mathf.Clamp(dt,0,.1f):0;
            clock+=step;Record();
            if(!tracked)
            {
                lostFor+=step;
                if(lostFor<LostSeconds)return true;
                Finish(releasing?releaseValue:Value01);return false;
            }
            lostFor=0;
            if(held){releasing=false;releaseFor=0;return true;}
            // A lifted fingertip does not dip anything: only the ray rewinds.
            if(!releasing){releasing=true;releaseFor=0;releaseValue=ValueAt(clock-(By==Source.Ray?ReleaseLead:0));}
            releaseFor+=step;
            if(releaseFor+1e-5f<EmberdeepPinchClick.RearmSeconds)return true;
            Finish(releaseValue);return false;
        }
        // Ends at the current value (a page change or reset): nothing is rewound.
        public void End(){if(Active)Finish(Value01);}
        void Finish(float value){Value01=value;Active=false;releasing=false;Hand=-1;By=Source.None;}
        float Snap(float t)=>Steps>0?Mathf.Round(t*Steps)/Steps:t;
        void Record()
        {
            times[head]=clock;values[head]=Value01;head=(head+1)%History;count=Mathf.Min(count+1,History);
        }
        // The value the drag showed at time (the latest record at or before it; the oldest if none is that old).
        float ValueAt(float time)
        {
            float value=Value01;
            for(int i=1;i<=count;i++)
            {
                int k=(head-i+History)%History;value=values[k];
                if(times[k]<=time+1e-5f)return value;
            }
            return value;
        }
    }
}

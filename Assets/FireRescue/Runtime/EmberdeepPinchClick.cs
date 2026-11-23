using UnityEngine;

namespace FireRescue
{
    // One hand's click for the Emberdeep hand-ray UI (EmberdeepHandRayMenu). ShadowWorkshop's ShadowPinchGesture stays
    // its grab gesture; this replaces it (and EmberdeepGazePinch) for menu clicks only.
    //
    // A click is the pinch's CLOSING EDGE while the ray has been on the same button for DwellSeconds. The next click
    // needs only an ordinary release: the select falling to ReleaseValue (or the tips ReleaseSpan apart), seen for
    // RearmSeconds. It never waits for a wide-open hand, nor for the runtime's own release: Meta's index IsPinching
    // stays pressed until the fingers are well apart (headset test 2, 2026-11-22: repeated pinches on one button failed
    // unless the hand opened wide). A pinch already closed when the ray reaches a button, or closing as it arrives
    // (dwell not met), is consumed and never clicks: the hand must release first.
    //
    // Two input scales:
    //  - the runtime's select (Meta: index pinch strength + IsPinching; PICO: aim_activate_ext value and its pressed
    //    state from PortableSystemAim). Closes at CloseValue or on the runtime's own pressed edge; releases at
    //    ReleaseValue, or when the tips are ReleaseSpan apart while the value is under CloseValue;
    //  - the thumb-index tip span (PICO before the system select is proven, desktop mouse). Closes under CloseSpan
    //    (ShadowPinch's 18 mm, the same physical pinch), releases at ReleaseSpan.
    public sealed class EmberdeepPinchClick
    {
        public const float CloseSpan=.018f,ReleaseSpan=.026f;
        public const float CloseValue=.9f,ReleaseValue=.6f;
        // RearmSeconds: two frames at 72 Hz, so a one-frame tracking glitch cannot fake a release and a second click.
        // DwellSeconds: the ray must rest on the button this long before a closing edge counts.
        // Each frame counts for at most MaxObservedStep, so one slow frame can neither rearm nor settle a target.
        public const float RearmSeconds=.025f,DwellSeconds=.12f,MaxObservedStep=.025f;
        public enum Event{None,Press,Rearm,Unarmed,NoTarget,Early}
        public struct Reading
        {
            public bool Tracked;
            // Select: the runtime's select drives this hand (Value 0..1, RuntimePressed = its own pressed state).
            public bool Select;public float Value;public bool RuntimePressed;
            // Thumb-index tip distance (m); +inf when unknown (allowed with Select only).
            public float Span;
            public static Reading Tips(float span)=>new(){Tracked=true,Span=span};
            public static Reading System(float value,bool pressed,float tips=float.PositiveInfinity)
                =>new(){Tracked=true,Select=true,Value=value,RuntimePressed=pressed,Span=tips};
        }
        public bool Closed {get;private set;}
        public bool Armed {get;private set;}
        // What the last Sample did (logs): a press, a rearm, or why a closing edge did not click.
        public Event Last {get;private set;}
        public float Dwell=>dwell;
        public int Target=>target;
        int target;
        float dwell,open;
        bool runtimePressed;

        // The click reading for a ray sample: no reading while the ray or its pinch is unavailable.
        public static Reading Of(EmberdeepHandRay.Sample sample)
        {
            if(!EmberdeepHandRay.Valid(sample) || sample.PinchUnavailable)return default;
            return sample.SystemSelect?Reading.System(sample.Select,sample.SelectPressed,sample.TipSpan):Reading.Tips(sample.Pinch);
        }
        // targetId: the button the ray is on (0 = none). True on the frame a click happens.
        public bool Sample(int targetId,Reading reading,float dt)
        {
            Last=Event.None;
            if(!float.IsFinite(dt) || dt<=0){Reset();return false;}
            float step=Mathf.Min(dt,MaxObservedStep);
            if(targetId!=target){target=targetId;dwell=0;}
            if(target!=0)dwell=Mathf.Min(DwellSeconds,dwell+step);
            if(!Usable(reading)){ResetHand();return false;}
            bool close,release;
            if(reading.Select)
            {
                bool edge=reading.RuntimePressed && !runtimePressed;runtimePressed=reading.RuntimePressed;
                close=reading.Value>=CloseValue || edge;
                release=reading.Value<=ReleaseValue || (float.IsFinite(reading.Span) && reading.Span>=ReleaseSpan && reading.Value<CloseValue);
            }
            else
            {
                runtimePressed=false;
                close=reading.Span<CloseSpan;release=reading.Span>=ReleaseSpan;
            }
            bool was=Closed;
            if(was){if(release)Closed=false;}
            else if(close)Closed=true;
            if(Closed)
            {
                open=0;
                if(was)return false;
                bool armed=Armed;Armed=false; // every closing edge is consumed, click or not
                if(!armed){Last=Event.Unarmed;return false;}
                if(target==0){Last=Event.NoTarget;return false;}
                if(dwell<DwellSeconds){Last=Event.Early;return false;}
                Last=Event.Press;return true;
            }
            if(!release){open=0;return false;}
            if(!Armed){open+=step;if(open>=RearmSeconds){Armed=true;Last=Event.Rearm;}}
            return false;
        }
        // The ray's button stays, but the dwell starts again (the other hand clicked and the page may have changed).
        public void RestartDwell()=>dwell=0;
        public void Reset(){target=0;dwell=0;ResetHand();}
        void ResetHand(){Closed=Armed=false;open=0;runtimePressed=false;}
        static bool Usable(Reading r)
        {
            if(!r.Tracked)return false;
            if(r.Select)return float.IsFinite(r.Value) && r.Value>=0 && r.Value<=1 && !float.IsNaN(r.Span);
            return float.IsFinite(r.Span) && r.Span>=0;
        }
    }
}

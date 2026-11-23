using System.Globalization;
using CloudGarden.Integration;
using UnityEngine;

namespace FireRescue
{
    // The UI hand ray is the headset's own aim ray: Meta's system pointer pose on Quest, the OpenXR hand-interaction
    // aim (XR_EXT_hand_interaction /input/aim/pose) on PICO. Only when the runtime reports no aim does PICO fall back to
    // PortableJointAim. The drawn line starts at the ray's own origin. Clicks follow the runtime's select when it has one
    // (Meta index pinch, PICO aim_activate), mapped onto the pinch-span scale the menu and ShadowPinchGesture use;
    // otherwise the thumb-index tip distance. Never infer aim from the fingertip that curls to click. The menu owns
    // click rearming (EmberdeepPinchClick); pointing itself remains visible while closed.
    // Quest: the ISDK hand chain bridges a lost hand at its last pose (EmberdeepHandSight), so the ray also needs the
    // runtime's own tracking; after EmberdeepSightGate.LostAfter without it the ray is gone.
    public sealed class EmberdeepHandRay
    {
        // The runtime select as a pinch span (m), for the menu's closing-pinch retention and the log (clicks read the
        // select itself, EmberdeepPinchClick): closed sits under ShadowPinch.CloseDistance (18 mm); open runs from
        // SelectOpenSpan (strength 0) down to SelectReadySpan (strength SelectFull).
        public const float SelectClosedSpan=.01f,SelectReadySpan=.021f,SelectOpenSpan=.06f,SelectFull=.8f;
        public struct Sample
        {
            public bool Valid;
            public Ray Ray;
            public Vector3 VisualOrigin;
            public float Pinch;
            public bool PinchUnavailable;
            // Where the ray came from, and whether Pinch is the runtime's select rather than the tip distance.
            public MetaShadowPlayer.AimSource Source;
            public bool SystemSelect;
            // SystemSelect only: the runtime's select value (0..1) and its own pressed state, and the thumb-index tip
            // distance (m, +inf when unknown), which EmberdeepPinchClick also accepts as a release.
            public float Select;
            public bool SelectPressed;
            public float TipSpan;
            public Sample(bool valid,Ray ray,float pinch)
            {
                Valid=valid;Ray=ray;VisualOrigin=ray.origin;Pinch=pinch;PinchUnavailable=false;Source=MetaShadowPlayer.AimSource.None;
                SystemSelect=false;Select=0;SelectPressed=false;TipSpan=float.PositiveInfinity;
            }
            // A sample whose clicks follow a runtime select (tests and desktop tools).
            public static Sample WithSelect(Ray ray,float select,bool pressed,float tipSpan=float.PositiveInfinity)
                =>new(true,ray,SystemSpan(pressed,select)){SystemSelect=true,Select=select,SelectPressed=pressed,TipSpan=tipSpan};
        }
        // The last source this hand logged (development builds log one EMBERDEEP_AIM line per change).
        MetaShadowPlayer.AimSource loggedSource;
        bool loggedSelect;
        // Quest: whether the runtime itself still tracks this hand (the ISDK chain bridges a lost hand).
        EmberdeepSightGate sight;
        bool loggedSight=true;
        public void Reset(){} // No cached poses or gesture state in the ray adapter.

        public Sample Read(MetaShadowPlayer provider,bool left)
        {
            if(!provider)return Invalid();
            if(!provider.UsesPortableGrip && !Sighted(provider,left))return Invalid();
            if(!provider.ReadMenuHand(left,out var palm,out var pointer,out var pinch,out bool pinchAvailable,out var detail))
                return Invalid();
            float tips=pinchAvailable && float.IsFinite(pinch)?pinch:float.PositiveInfinity;
            if(detail.SystemSelect){pinch=SystemSpan(detail.SelectPressed,detail.Select);pinchAvailable=true;}
            var sample=FromPointerPose(true,pointer,pinch,pinchAvailable);
            if(!sample.Valid)return sample;
            sample.Source=detail.Source==MetaShadowPlayer.AimSource.None?MetaShadowPlayer.AimSource.System:detail.Source;
            sample.SystemSelect=detail.SystemSelect;
            if(sample.SystemSelect){sample.Select=detail.Select;sample.SelectPressed=detail.SelectPressed;sample.TipSpan=tips;}
            if(Debug.isDebugBuild && (sample.Source!=loggedSource || sample.SystemSelect!=loggedSelect))
            {loggedSource=sample.Source;loggedSelect=sample.SystemSelect;Debug.Log(Describe(left,palm,sample,detail));}
            return sample;
        }
        // Quest's menu ray: the ISDK hand (bridged by LastKnownGoodHand) must also be tracked by the runtime itself.
        bool Sighted(MetaShadowPlayer provider,bool left)
        {
            var hand=provider.TrackedHand(left);
            bool raw=hand && EmberdeepHandSight.RawTracked(hand);
            bool seen=sight.Step(hand,raw,Time.unscaledDeltaTime);
            if(seen!=loggedSight)
            {
                loggedSight=seen;
                if(EmberdeepHandRayMenu.DeviceLog)Debug.Log("EMBERDEEP_AIM hand="+(left?'L':'R')+" sight="+(seen?"seen":"lost")
                    +" raw_tracked="+(raw?1:0)+" bridged="+(hand?1:0));
            }
            return seen;
        }
        // The runtime's select as a pinch span: closed while it is in EmberdeepPinchClick's closed range (a pressed state
        // with the strength under ReleaseValue is the runtime's late release, not a pinch), otherwise open and narrowing
        // as it nears.
        public static float SystemSpan(bool pressed,float strength)
        {
            if(!float.IsFinite(strength))strength=0;
            if(strength>=EmberdeepPinchClick.CloseValue || (pressed && strength>EmberdeepPinchClick.ReleaseValue))return SelectClosedSpan;
            return Mathf.Lerp(SelectOpenSpan,SelectReadySpan,Mathf.Clamp01(strength/SelectFull));
        }
        public Sample FromPointerPose(bool tracked,Pose pointer,float pinch,bool pinchAvailable=true)
        {
            if(!tracked || !Finite(pointer.position) || !Finite(pointer.rotation)
                || (pinchAvailable && (!Finite(pinch) || pinch<0)))return Invalid();
            var q=pointer.rotation;
            float norm=q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w;
            if(norm<.5f || norm>1.5f)return Invalid();
            return new Sample(true,new Ray(pointer.position,pointer.rotation*Vector3.forward),pinch){PinchUnavailable=!pinchAvailable};
        }
        // One device-log line: which aim and which select the hand uses, where the ray starts relative to the wrist
        // and the web, its pitch, the select state, and the fallback's blend or the system's tracking flags.
        public static string Describe(bool left,Pose wrist,Sample sample,MetaShadowPlayer.MenuAimDetail detail)
        {
            var origin=sample.Ray.origin;var direction=sample.Ray.direction;
            bool system=sample.Source!=MetaShadowPlayer.AimSource.Fallback;
            var text=new System.Text.StringBuilder("EMBERDEEP_AIM source=").Append(system?"system":"fallback")
                .Append(" hand=").Append(left?'L':'R')
                .Append(" aim=").Append(!system?"joints":detail.Portable?"openxr_hand_interaction":"meta_pointer")
                .Append(" select=").Append(sample.SystemSelect?"system":"joints")
                .Append(" origin_wrist_mm=").Append(Mm(origin-wrist.position))
                .Append(" origin_web_mm=").Append(detail.WebValid?Mm(origin-detail.Web):"none")
                .Append(" pitch_deg=").Append(F(Mathf.Asin(Mathf.Clamp(direction.y,-1,1))*Mathf.Rad2Deg))
                .Append(" pinch_mm=").Append(sample.PinchUnavailable?"none":F(sample.Pinch*1000,0));
            if(system)
            {
                text.Append(" select_value=").Append(F(detail.Select,2)).Append(" select_pressed=").Append(detail.SelectPressed?1:0)
                    .Append(" select_ready=").Append(detail.SelectReady?1:0);
                if(detail.Portable && detail.RuntimeAimValid)
                    text.Append(" tracking=0x").Append(detail.RuntimeAimFlags.ToString("X",CultureInfo.InvariantCulture))
                        .Append(" smooth_mm=").Append(Mm(origin-detail.RuntimeAim.position))
                        .Append(" smooth_deg=").Append(F(Vector3.Angle(direction,detail.RuntimeAim.rotation*Vector3.forward)));
            }
            else
            {
                var blend=detail.Blend;
                text.Append(" blend=").Append(F(blend.HandWeight,2)).Append('/').Append(F(PortableJointAim.HandBlend,2))
                    .Append(" shoulder_hand_deg=").Append(F(blend.ShoulderHandAngle))
                    .Append(" ray_shoulder_deg=").Append(F(Vector3.Angle(direction,blend.Shoulder)))
                    .Append(" hold=").Append(F(blend.Hold,2));
            }
            return text.ToString();
        }
        static string Mm(Vector3 offset)=>F(offset.magnitude*1000,0);
        static string F(float value,int digits=1)=>value.ToString("F"+digits,CultureInfo.InvariantCulture);
        Sample Invalid(){Reset();return new Sample(false,default,float.PositiveInfinity);}
        public static bool Valid(Sample sample)=>sample.Valid && Finite(sample.Ray.origin) && Finite(sample.VisualOrigin) && Finite(sample.Ray.direction)
            && sample.Ray.direction.sqrMagnitude>.5f && (sample.PinchUnavailable || (Finite(sample.Pinch) && sample.Pinch>=0));
        static bool Finite(Vector3 v)=>Finite(v.x)&&Finite(v.y)&&Finite(v.z);
        static bool Finite(Quaternion q)=>Finite(q.x)&&Finite(q.y)&&Finite(q.z)&&Finite(q.w);
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
}

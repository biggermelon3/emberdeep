using UnityEngine;

namespace FireRescue
{
    // Different thresholds for acquiring an object and operating its trigger.
    // Invalid data cancels actions; it is never interpreted as an open hand.
    public sealed class EmberdeepSqueeze
    {
        public const float Grab=.40f, Open=.20f, Fire=.76f, StopFire=.52f;
        readonly float close,release;
        bool armed;
        float dwell,acquireRemaining;
        public bool CanAcquire=>Closed && acquireRemaining>0;
        public bool Closed { get; private set; }
        public bool Pressed { get; private set; }
        public EmberdeepSqueeze(float close=Grab,float release=Open){this.close=close;this.release=release;}
        public void Reset(){armed=Closed=Pressed=false;dwell=acquireRemaining=0;}
        public void Sample(bool valid,float strength,float dt)
        {
            Pressed=false;
            if(!valid || !float.IsFinite(strength) || strength<0 || strength>1 || !float.IsFinite(dt) || dt<=0){Reset();return;}
            dt=Mathf.Min(dt,.025f);
            acquireRemaining=Mathf.Max(0,acquireRemaining-dt);
            if(strength<=release){Closed=false;armed=true;dwell=acquireRemaining=0;return;}
            if(Closed)return;
            if(!armed || strength<close){dwell=0;return;}
            dwell+=dt;
            if(dwell+.00001f<.075f)return;
            Pressed=Closed=true;armed=false;dwell=0;acquireRemaining=.25f;
        }
    }
    // Legacy synthetic samples retain their pinch contract; production object
    // samples explicitly opt into closure and never fall back after lost fingers.
    public sealed class EmberdeepControlGesture
    {
        readonly EmberdeepSqueeze grasp=new();
        readonly CloudGarden.ShadowPuzzle.ShadowPinchGesture pinch=new();
        bool useGrip;
        public bool Closed=>useGrip?grasp.Closed:pinch.Closed;
        public bool Pressed=>useGrip?grasp.CanAcquire:pinch.Pressed;
        public void Reset(){grasp.Reset();pinch.Reset();}
        public void Sample(EmberdeepRefillInteraction.Hand hand,float dt)
        {
            if(useGrip!=hand.UsesGrip){Reset();useGrip=hand.UsesGrip;}
            if(useGrip)grasp.Sample(hand.Tracked && hand.GripValid,hand.GripStrength,dt);
            else pinch.Sample(hand.Tracked,hand.Pinch,dt);
        }
    }
}

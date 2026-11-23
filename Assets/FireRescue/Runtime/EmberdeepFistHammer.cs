using UnityEngine;

namespace FireRescue
{
    // LoopSpec §3.4 fist hammer. A closed fist moving down through the top of the pump
    // cap faster than StrikeSpeed is one strike; that hand must rise above RearmHeight
    // before it can strike again. Nothing grabs, snaps or attaches. Either hand counts.
    // Lost tracking disarms the hand, so a fist reappearing below the cap never strikes.
    public sealed class EmberdeepFistHammer
    {
        public struct Hand
        {
            public bool Valid;
            public Vector3 Fist;
            public float Closure;
            public Hand(bool valid,Vector3 fist,float closure){Valid=valid;Fist=fist;Closure=closure;}
            public bool Usable=>Valid && float.IsFinite(Fist.x) && float.IsFinite(Fist.y) && float.IsFinite(Fist.z)
                && float.IsFinite(Closure);
        }
        readonly bool[] armed=new bool[2], tracked=new bool[2];
        readonly float[] height=new float[2], speed=new float[2];
        public int StrikeHand {get;private set;}=-1;
        public float StrikeSpeed {get;private set;}
        public bool Struck=>StrikeHand>=0;
        public bool Armed(int hand)=>hand>=0 && hand<2 && armed[hand];
        // Fist above the cap and within reach: the cap can show it is ready to be hit.
        public bool Hovering {get;private set;}
        public float DownSpeed(int hand)=>hand>=0 && hand<2?speed[hand]:0;
        public void Reset()
        {
            for(int i=0;i<2;i++){armed[i]=tracked[i]=false;height[i]=speed[i]=0;}
            StrikeHand=-1;StrikeSpeed=0;Hovering=false;
        }
        // capTop: world point on the top face of the cap. up: world up of the pump.
        public bool Sample(Hand left,Hand right,Vector3 capTop,Vector3 up,float dt)
        {
            StrikeHand=-1;StrikeSpeed=0;Hovering=false;
            if(!float.IsFinite(dt) || dt<=0 || !float.IsFinite(capTop.sqrMagnitude) || up.sqrMagnitude<.5f){Reset();return false;}
            dt=Mathf.Min(dt,.1f);up.Normalize();
            for(int i=0;i<2;i++)
            {
                var hand=i==0?left:right;
                if(!hand.Usable){armed[i]=tracked[i]=false;speed[i]=0;continue;}
                var offset=hand.Fist-capTop;float now=Vector3.Dot(offset,up);
                float lateral=(offset-up*now).magnitude;
                if(tracked[i])
                {
                    float measured=(height[i]-now)/dt;
                    speed[i]=Mathf.Lerp(speed[i],measured,1-Mathf.Exp(-dt/EmberdeepWaterTuning.SpeedSmoothingSeconds));
                }
                else speed[i]=0;
                bool fist=hand.Closure>=EmberdeepWaterTuning.StrikeClosure;
                bool reach=lateral<=EmberdeepWaterTuning.StrikeRadius;
                if(now>=EmberdeepWaterTuning.RearmHeight)armed[i]=true;
                bool crossed=tracked[i] && height[i]>EmberdeepWaterTuning.StrikeContactHeight && now<=EmberdeepWaterTuning.StrikeContactHeight;
                if(StrikeHand<0 && armed[i] && fist && reach && crossed && speed[i]>=EmberdeepWaterTuning.StrikeSpeed)
                {StrikeHand=i;StrikeSpeed=speed[i];armed[i]=false;}
                Hovering|=armed[i] && fist && lateral<=EmberdeepWaterTuning.StrikeRadius*1.6f && now>EmberdeepWaterTuning.StrikeContactHeight
                    && now<EmberdeepWaterTuning.RearmHeight*2.5f;
                height[i]=now;tracked[i]=true;
            }
            return StrikeHand>=0;
        }
    }

    // How one strike landed, for its sound and flash: Plain starts a beat (or the rhythm is off), OnBeat met the ring,
    // OffBeat missed it while a beat was running.
    public enum EmberdeepPumpGrade { Plain, OnBeat, OffBeat }

    // LoopSpec §3.4 rhythm bonus. The first strike of a pumping run sets the beat; later
    // strikes within BeatWindowSeconds of a beat earn OnBeatWater. Off-beat strikes keep the
    // beat (no re-phasing), so the ring the player follows never jumps.
    public sealed class EmberdeepPumpRhythm
    {
        // Registers a strike and grades it. enabled=false (the feel-test toggle) grades every strike Plain.
        public EmberdeepPumpGrade StrikeGraded(double now,bool enabled)
        {
            bool running=Active(now);bool onBeat=Strike(now);
            if(!enabled || !running)return EmberdeepPumpGrade.Plain;
            return onBeat?EmberdeepPumpGrade.OnBeat:EmberdeepPumpGrade.OffBeat;
        }
        double start=-1,last=-1;
        public int Streak {get;private set;}
        public void Reset(){start=last=-1;Streak=0;}
        public bool Active(double now)=>last>=0 && now>=last && now-last<=EmberdeepWaterTuning.RhythmSessionSeconds;
        // 0 right after a beat .. 1 at the next beat.
        public float Phase01(double now)
        {
            if(start<0)return 0;
            double beats=(now-start)/EmberdeepWaterTuning.BeatSeconds;
            return (float)(beats-System.Math.Floor(beats));
        }
        public bool InWindow(double now)
        {
            if(!Active(now))return false;
            double beats=(now-start)/EmberdeepWaterTuning.BeatSeconds;
            double nearest=System.Math.Round(beats);
            return nearest>=1 && System.Math.Abs(beats-nearest)*EmberdeepWaterTuning.BeatSeconds<=EmberdeepWaterTuning.BeatWindowSeconds;
        }
        // Registers a strike at 'now'; true when it lands on the beat.
        public bool Strike(double now)
        {
            if(!double.IsFinite(now))return false;
            if(!Active(now)){start=last=now;Streak=0;return false;}
            bool onBeat=InWindow(now);
            last=now;Streak=onBeat?Streak+1:0;
            return onBeat;
        }
    }
}

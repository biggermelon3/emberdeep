using UnityEngine;

namespace FireRescue
{
    // Tracked features of one hand for the gesture spells. Curl is 0 for a
    // straight finger and 1 for a folded one. Nothing here is guessed after
    // tracking loss: invalid data only ever holds or cancels a spell.
    public struct EmberdeepSpellHand
    {
        public bool Valid;              // palm centre and palm frame are tracked
        public Vector3 Contact;         // palm centre
        public Quaternion Frame;        // forward: wrist to fingers, up: out of the palm
        public bool CurlsValid;
        public float Index,Middle,Ring,Little;
        public bool Open;               // verified five-finger open palm
        public bool PointValid;         // wrist, index knuckle and index tip are tracked
        public Vector3 Wrist,IndexBase;
        public int TipMask;             // thumb, index, middle, ring, little
        public Vector3 ThumbTip,IndexTip,MiddleTip,RingTip,LittleTip;
        public Vector3 Normal=>Frame*Vector3.up;
        public float Closure=>(Index+Middle+Ring+Little)*.25f;
        public bool Seen=>Valid || TipMask!=0;
        public bool TryTip(int finger,out Vector3 tip)
        {
            tip=finger switch{0=>ThumbTip,1=>IndexTip,2=>MiddleTip,3=>RingTip,4=>LittleTip,_=>default};
            return finger>=0 && finger<5 && (TipMask&(1<<finger))!=0 && float.IsFinite(tip.sqrMagnitude);
        }
        public bool Usable=>Valid && float.IsFinite(Contact.sqrMagnitude) && float.IsFinite(Quaternion.Dot(Frame,Frame))
            && Quaternion.Dot(Frame,Frame)>.5f;
    }

    // WATER BOLT. The left hand points; the right hand is the trigger: a fist
    // charges and an observed open hand fires one shot. A lost hand never fires.
    public sealed class EmberdeepBoltGesture
    {
        public const float FullChargeSeconds=1f,MinimumChargeSeconds=.08f,LossGraceSeconds=.4f;
        public const float FistClosure=.68f,OpenClosure=.36f;
        public bool Aiming {get;private set;}
        public bool Charging {get;private set;}
        public float Charge01 {get;private set;}
        public bool Fired {get;private set;}
        public float FiredCharge {get;private set;}
        public Vector3 Origin {get;private set;}
        public Vector3 Direction {get;private set;}
        bool armed,hasDirection;
        float charge,lostLeft,lostRight;
        public void Reset()
        {Aiming=Charging=Fired=armed=hasDirection=false;Charge01=FiredCharge=charge=lostLeft=lostRight=0;}
        public static bool IsPointing(EmberdeepSpellHand hand,bool already)
        {
            if(!hand.Usable || !hand.CurlsValid || !hand.PointValid)return false;
            return already?hand.Index<.46f && hand.Ring+hand.Little>.72f:hand.Index<.30f && hand.Ring>.5f && hand.Little>.5f;
        }
        // One shot per release even if the caller samples less often than it casts.
        public bool TakeFired(){bool fired=Fired;Fired=false;return fired;}
        public void Sample(EmberdeepSpellHand left,EmberdeepSpellHand right,bool allowed,float dt)
        {
            Fired=false;
            if(!allowed || !float.IsFinite(dt) || dt<=0){Reset();return;}
            dt=Mathf.Min(dt,.1f);
            if(left.Usable && left.CurlsValid && left.PointValid)
            {
                lostLeft=0;Aiming=IsPointing(left,Aiming);
                if(Aiming)
                {
                    var finger=left.IndexTip-left.IndexBase;var palm=left.IndexBase-left.Wrist;
                    if(finger.sqrMagnitude>1e-8f && palm.sqrMagnitude>1e-8f)
                    {
                        // The knuckle line steadies a fingertip seen end-on by the headset.
                        var raw=(finger.normalized*.7f+palm.normalized*.3f).normalized;
                        Direction=hasDirection?Vector3.Slerp(Direction,raw,1-Mathf.Exp(-dt*14)).normalized:raw;
                        hasDirection=true;Origin=left.IndexTip;
                    }
                }
            }
            else{lostLeft+=dt;if(lostLeft>LossGraceSeconds)Aiming=false;}
            if(!Aiming || !hasDirection){Charging=false;charge=Charge01=0;hasDirection&=Aiming;return;}
            if(!right.Usable || !right.CurlsValid)
            {
                // Hold the charge briefly; a hand that stays lost cancels without firing.
                lostRight+=dt;
                if(lostRight>LossGraceSeconds){Charging=armed=false;charge=Charge01=0;}
                return;
            }
            lostRight=0;float closure=right.Closure;
            if(Charging)
            {
                if(closure<=OpenClosure)
                {
                    if(charge>=MinimumChargeSeconds){Fired=true;FiredCharge=Mathf.Clamp01(charge/FullChargeSeconds);}
                    Charging=false;charge=0;armed=true;
                }
                else charge+=dt;
            }
            else if(closure<=OpenClosure)armed=true;
            else if(armed && closure>=FistClosure){Charging=true;charge=0;armed=false;}
            Charge01=Charging?Mathf.Clamp01(charge/FullChargeSeconds):0;
        }
    }

    // RAIN FAN. Both open palms pushed toward the forest for one second start
    // a wide spray; closing a hand or turning it away stops it at once.
    public sealed class EmberdeepFanGesture
    {
        public const float ArmSeconds=1f,LossGraceSeconds=.25f,MinimumSeparation=.12f;
        public bool Ready {get;private set;}
        public bool Active {get;private set;}
        public float Arm01 {get;private set;}
        public Vector3 Origin {get;private set;}
        public Vector3 Direction {get;private set;}
        float arm,lost;
        public void Reset(){Ready=Active=false;Arm01=arm=lost=0;}
        bool Posed(EmberdeepSpellHand hand,Vector3 toward)
        {
            if(!hand.Usable)return false;
            bool open=Active?hand.CurlsValid && hand.Closure<.5f:hand.Open;
            return open && Vector3.Dot(hand.Normal,toward)>(Active?.2f:.35f);
        }
        public void Sample(EmberdeepSpellHand left,EmberdeepSpellHand right,Vector3 toward,bool allowed,float dt)
        {
            if(!allowed || !float.IsFinite(dt) || dt<=0 || toward.sqrMagnitude<1e-6f){Reset();return;}
            dt=Mathf.Min(dt,.1f);toward=toward.normalized;
            bool posed=Posed(left,toward) && Posed(right,toward)
                && Vector3.Distance(left.Contact,right.Contact)>=MinimumSeparation
                && Vector3.Angle(left.Normal,right.Normal)<70;
            if(posed)
            {
                lost=0;Ready=true;arm=Mathf.Min(ArmSeconds,arm+dt);
                if(arm>=ArmSeconds-.0001f)Active=true;
                Direction=(left.Normal+right.Normal).normalized;
                Origin=(left.Contact+right.Contact)*.5f+Direction*.03f;
            }
            else
            {
                // Only missing tracking gets a grace period; a visible fist stops now.
                bool missing=!left.Usable || !right.Usable;
                lost+=dt;
                if(!Active || !missing || lost>LossGraceSeconds)Reset();
            }
            Arm01=Mathf.Clamp01(arm/ArmSeconds);
        }
    }

    // WATER ORB. The left palm faces up; right fingers under it grow the orb.
    // The right hand is usually hidden by the left one there, so charging is
    // latched: only a right hand actually seen outside the zone ends it.
    public sealed class EmberdeepOrbGesture
    {
        public enum Stage { Idle, Charging, Held }
        public const float FullChargeSeconds=1.5f,StartDwellSeconds=.12f,ReleaseDwellSeconds=.1f;
        public const float MinimumCharge01=.12f,ExitSeconds=.06f,HiddenFinishSeconds=.6f,LeftLossSeconds=1.5f;
        public Stage State {get;private set;}
        public float Charge01 {get;private set;}
        public bool CupReady {get;private set;}
        public bool Flipped {get;private set;}
        public bool Thrown {get;private set;}
        public float ThrownCharge {get;private set;}
        public Vector3 Center {get;private set;}
        public Vector3 Normal {get;private set;}
        public float Radius=>Mathf.Lerp(.012f,.044f,Charge01);
        float dwell,outside,hidden,lostLeft,release;
        bool rearmed=true;
        public void Reset()
        {State=Stage.Idle;Charge01=ThrownCharge=dwell=outside=hidden=lostLeft=release=0;CupReady=Flipped=Thrown=false;rearmed=true;}
        // Left-hand palm space: +Y leaves the palm, so the zone is behind the hand.
        public static bool Under(EmberdeepSpellHand left,Vector3 point,float margin)
        {
            if(!float.IsFinite(point.sqrMagnitude))return false;
            var local=Quaternion.Inverse(left.Frame)*(point-left.Contact);
            return local.y>=-.13f-margin && local.y<=-.012f+margin*.3f && Mathf.Abs(local.x)<.06f+margin && Mathf.Abs(local.z)<.075f+margin;
        }
        static bool AnyUnder(EmberdeepSpellHand left,EmberdeepSpellHand right,float margin)
        {
            for(int i=0;i<5;i++)if(right.TryTip(i,out var tip) && Under(left,tip,margin))return true;
            if(right.Usable && Under(left,right.Contact,margin))return true;
            return right.PointValid && Under(left,right.IndexBase,margin);
        }
        void Place(EmberdeepSpellHand left)
        {Normal=left.Normal;Center=left.Contact+Normal*(.022f+Radius);}
        public bool TakeThrown(){bool thrown=Thrown;Thrown=false;return thrown;}
        public void Sample(EmberdeepSpellHand left,EmberdeepSpellHand right,bool allowed,bool canDrop,float dt)
        {
            Thrown=false;
            if(!allowed || !float.IsFinite(dt) || dt<=0){Reset();return;}
            dt=Mathf.Min(dt,.1f);
            bool leftOk=left.Usable;float up=leftOk?Vector3.Dot(left.Normal,Vector3.up):0;
            CupReady=leftOk && up>.55f && (!left.CurlsValid || left.Closure<.8f);
            if(State==Stage.Idle)
            {
                Charge01=0;Flipped=false;
                bool under=CupReady && right.Seen && AnyUnder(left,right,0);
                if(!under){dwell=0;if(right.Seen || !CupReady)rearmed=true;return;}
                if(!rearmed)return;
                dwell+=dt;
                if(dwell<StartDwellSeconds)return;
                State=Stage.Charging;dwell=outside=hidden=lostLeft=release=0;rearmed=false;Place(left);return;
            }
            if(!leftOk)
            {
                // Freeze the orb where it was; a long loss dissolves it for free.
                lostLeft+=dt;if(lostLeft>LeftLossSeconds)Reset();
                return;
            }
            lostLeft=0;
            if(State==Stage.Charging)
            {
                Charge01=Mathf.Clamp01(Charge01+dt/FullChargeSeconds);Place(left);
                bool finish=up<.3f;
                if(right.Seen)
                {
                    hidden=0;
                    if(AnyUnder(left,right,.035f))outside=0;
                    else{outside+=dt;finish|=outside>=ExitSeconds;}
                }
                else{hidden+=dt;finish|=Charge01>=1 && hidden>=HiddenFinishSeconds;}
                if(finish){if(Charge01>=MinimumCharge01){State=Stage.Held;release=0;}else Reset();}
                return;
            }
            Place(left);Flipped=up<-.2f;
            bool open=left.Open || (left.CurlsValid && left.Closure<.3f);
            release=Flipped && open && canDrop?release+dt:0;
            if(release<ReleaseDwellSeconds)return;
            Thrown=true;ThrownCharge=Charge01;State=Stage.Idle;Charge01=0;Flipped=false;dwell=release=0;rearmed=false;
        }
    }
}

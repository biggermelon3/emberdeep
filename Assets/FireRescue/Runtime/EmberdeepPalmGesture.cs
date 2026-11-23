using UnityEngine;

namespace FireRescue
{
    // Explicit two-hand spatial gesture. No pinch, pointer or rendered hand feeds this state. The fireground and the camp
    // run this same class with the same inputs (FireToolManipulation.Hand from TryReadClosure / TryReadPortableGrip), so
    // both get the same open-palm and gem-press rules.
    public sealed class EmberdeepPalmGesture
    {
        // Resolves the palm ray onto something worth watering before the board-plane fallback (the fireground: the tree or
        // burning sheep the palm points at, EmberdeepPalmAim). tree: what it snapped to (EmberdeepPalmAim.Pick's target
        // index), reported as AssistTree.
        public delegate bool AimAssist(Vector3 origin,Vector3 direction,float range,out Vector3 target,out int tree);

        // *tune* Palm openness = 1 - Hand.Closure. Closure is the four-finger mean of GrabLatch.FingerCurl (1 - cos of the
        // bend from the proximal phalanx to the tip), read on both runtimes; the sheep and piece grabs use the same scale.
        // 1 = flat hand, 0 = every finger bent 90 deg or more. Headset test 3 (2026-11-22): the palm only aimed nearly flat.
        // The old gate (EmberdeepHandGrasp.IsOpenPalm) wanted EVERY finger under .24 curl (40 deg) and the thumb under .30,
        // with every finger high-confidence: the ring and little fingers of a relaxed hand miss that. Now aiming starts at
        // OpenStart (mean bend ~46 deg) and holds down to OpenHold (~52 deg), so a relaxed hand's jitter cannot flicker it.
        // OpenHold stays above the grab closure (.42, openness .58: EmberdeepSheepInteraction / EmberdeepPieceGrasp), so a
        // closing hand drops the aim before it can grab.
        public const float OpenStart=.70f,OpenHold=.62f;
        public static float Openness(float closure)=>1-closure;

        // *tune* Gem press: any fingertip of the OTHER hand on the back of the water hand. Water-hand frame (GripRotation):
        // x across, y palmar (the palm normal), z toward the knuckles, from Contact (wrist-to-middle-knuckle midpoint, where
        // the gem hangs). Height is measured toward the back of the hand (-y): joint centre line 0, the skin ~.013, the gem
        // .016, a fingertip joint resting on the skin ~.02. Headset test 3 (2026-11-22): the press was unreliable. It needed a
        // .10 s dwell (a quick tap never sprayed), any blink of the touching tip restarted that dwell, a tip tracked into the
        // hand (height under .004) or on the knuckles (z past .048) missed, and a water hand that lost tracking under the
        // pressing finger dropped the press at once.
        public const float PressHalfWidth=.052f;            // m across (was .042): the whole back of the hand
        public const float PressWrist=.056f,PressKnuckles=.062f; // m along (was +-.048): wrist crease to just past the knuckles
        public const float PressLow=-.004f,PressHigh=.05f;  // m height (was .004-.042): a tip tracked into the hand, a light hover
        public const float PressMargin=.012f;               // m: a held press's box grows this much (edge jitter); the low side a quarter
        // A press starts on the first touching frame. It ends after ReleaseGrace without a touch (blinks of the tip), and not
        // before MinPress (a tap is a short visible squirt, not one frame of water). HandGrace: a water hand that blinks out
        // of tracking or openness keeps its last pose that long, and its press can go on from it.
        public const float ReleaseGrace=.08f,MinPress=.15f,HandGrace=.08f;
        // Below PressLow - quarter margin the tip is on the palm side: the water hand's own curled fingers and thumb live there
        // (and they never reach the box from the knuckle end: their tips are past PressKnuckles).

        public bool Aiming {get;private set;}
        public bool Casting {get;private set;}
        public Vector3 Origin {get;private set;}
        public Vector3 Target {get;private set;}
        public int AssistTree {get;private set;}=-1;
        // Presses started since construction (tests, the device log). Not reset.
        public int Presses {get;private set;}
        // This frame used the water hand's last seen pose (HandGrace).
        public bool Bridged {get;private set;}
        bool pressed;float sinceTouch,pressedFor,lostFor;
        FireToolManipulation.Hand lastHand;
        public void Reset(){Aiming=Casting=pressed=Bridged=false;sinceTouch=pressedFor=lostFor=0;AssistTree=-1;}
        // fingersOpen: the caller's strict open-palm read (EmberdeepHandGrasp.IsOpenPalm, PICO OpenPalm). It reads the same
        // finger joints as Closure and implies the closure gate, so for a tracked hand the closure gate decides; true also
        // opens a synthetic hand (tests, PreviewPalmShot).
        public void Sample(FireToolManipulation.Hand left,Vector3[] rightTips,int validTips,bool fingersOpen,
            bool allowed,Transform arena,float range,float dt,AimAssist assist=null)
        {
            if(!allowed || !arena || !float.IsFinite(dt) || dt<=0){Reset();return;}
            dt=Mathf.Min(dt,.1f);
            bool open=left.Usable && (fingersOpen || Openness(left.Closure)>=(Aiming?OpenHold:OpenStart));
            if(open){lastHand=left;lostFor=0;Bridged=false;}
            else if(Aiming && (lostFor+=dt)<=HandGrace){left=lastHand;Bridged=true;}
            else{Reset();return;}
            Vector3 normal=left.GripRotation*Vector3.up;
            Origin=left.Contact+normal*.018f;
            if(assist!=null && assist(Origin,normal,range,out var snapped,out int tree)){Target=snapped;AssistTree=tree;}
            else
            {
                AssistTree=-1;
                var ray=new Ray(Origin,normal);
                var ground=new Plane(arena.up,arena.position);
                if(!ground.Raycast(ray,out float distance) || distance<.04f || distance>range){Reset();return;}
                var target=ray.GetPoint(distance);var local=arena.InverseTransformPoint(target);
                if(Mathf.Abs(local.x)>5.5f || local.z<1.6f || local.z>12){Reset();return;}
                Target=target;
            }
            Aiming=true;
            bool touching=Touching(left,rightTips,validTips,pressed);
            if(touching){if(!pressed){pressed=true;pressedFor=0;Presses++;}sinceTouch=0;}
            else sinceTouch+=dt;
            if(pressed){pressedFor+=dt;if(!touching && sinceTouch>ReleaseGrace && pressedFor>=MinPress)pressed=false;}
            Casting=pressed;
        }
        // Any valid tip of the other hand on the back of the water hand (see Press*). pressed: the held press's larger box.
        public static bool Touching(FireToolManipulation.Hand water,Vector3[] tips,int validTips,bool pressed)
        {
            if(tips==null)return false;
            float m=pressed?PressMargin:0;var inverse=Quaternion.Inverse(water.GripRotation);
            for(int i=0;i<Mathf.Min(5,tips.Length);i++)
            {
                if((validTips&(1<<i))==0 || !float.IsFinite(tips[i].sqrMagnitude))continue;
                var d=inverse*(tips[i]-water.Contact);float height=-d.y;
                if(height>=PressLow-m*.25f && height<=PressHigh+m && Mathf.Abs(d.x)<=PressHalfWidth+m
                    && d.z>=-PressWrist-m && d.z<=PressKnuckles+m)return true;
            }
            return false;
        }
    }
}

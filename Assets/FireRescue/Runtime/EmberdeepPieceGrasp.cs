using System.Collections.Generic;
using UnityEngine;

namespace FireRescue
{
    // LoopSpec §5 placing: pieces are taken by hand (never by ray), pinched or grabbed.
    // Headset test 3 (2026-11-22): the old grasp measured the palm centre to the piece's middle (5 cm), so a tray tower
    // (4.5 cm wide) only came up with the palm pushed inside it. Everything is now measured to each piece's VISIBLE box
    // (EmberdeepPieceView.GraspBox; signed, so the box a point is deepest inside wins):
    // - Pinch: thumb and index tips closing (under PinchClose after being over PinchArm) with their midpoint within
    //   PinchReach of the box. Held until the tips part past PinchRelease, unless the hand has closed into a fist.
    // - Grab: the hand closing (closure from under Arm, opened within ApproachReach of a piece, to over Close for Dwell)
    //   with the palm centre or any fingertip within GrabReach of the box. A pointing hand (index out, the others in) is
    //   never a grab: that is the excavator's draw. Held until the hand opens (closure under Open).
    // - Hover: what a pinch or a close would take right now, for the view's highlight. A pinch or close takes exactly it.
    // - One piece per hand; a held piece is no candidate for the other hand; tracking lost for LostSeconds drops it.
    // - Rival: a per-hand distance set by the caller (a sheep the same hand could grab): only nearer pieces count.
    // World space (metres) throughout. Pure logic: the game supplies boxes and hands each frame and acts on Grabbed/Released.
    public sealed class EmberdeepPieceGrasp
    {
        public const float PinchReach=.03f,GrabReach=.05f,ApproachReach=.12f;
        // Pinch spans (m). A relaxed re-pinch opens to ~3 cm (HI's device notes, EmberdeepPinchClick), so arming and letting go
        // both sit there: 1 cm over the close, ~3x tip jitter.
        public const float PinchArm=.03f,PinchClose=.02f,PinchRelease=.03f;
        public const float Close=.42f,Open=.20f,Arm=.30f,Dwell=.05f,LostSeconds=.35f,PointSpread=.035f;
        // The farthest a hand point can be from a piece's box and still take it (any rule). GrabRadius: the old name, kept
        // for the pump keep-out and its tests (the game also blocks a hand inside the pump's strike zone, FireRescueGame.Pieces).
        public const float Reach=GrabReach,GrabRadius=GrabReach;
        public enum Mode { None, Pinch, Grab }

        public struct Hand
        {
            public bool Valid;
            public Vector3 Palm;          // palm centre (the wrist-to-middle-knuckle midpoint, FireToolManipulation's Contact)
            public float Closure;         // 0 open .. 1 fist (mean finger curl)
            public bool PinchValid;       // thumb-index tip distance readable
            public float Pinch;           // m
            public bool PinchPointValid;  // both tips readable: the pinch happens at their midpoint
            public Vector3 PinchPoint;
            public int Tips;              // bit f: Tip(f) readable (0 thumb, 1 index, 2 middle, 3 ring, 4 pinky)
            public Vector3 Thumb,Index,Middle,Ring,Pinky;
            public bool Usable=>Valid && float.IsFinite(Palm.sqrMagnitude) && float.IsFinite(Closure) && Closure>=0 && Closure<=1;
            public bool HasTip(int f)=>(Tips&(1<<f))!=0;
            public Vector3 Tip(int f)=>f==0?Thumb:f==1?Index:f==2?Middle:f==3?Ring:Pinky;
            public void SetTip(int f,Vector3 p)
            {
                if(f<0 || f>4 || !float.IsFinite(p.sqrMagnitude))return;
                if(f==0)Thumb=p;else if(f==1)Index=p;else if(f==2)Middle=p;else if(f==3)Ring=p;else Pinky=p;
                Tips|=1<<f;
            }
            // Pinch distance and point from the two tips (tests and callers without a separate pinch reading).
            public void PinchFromTips()
            {
                if(!HasTip(0) || !HasTip(1))return;
                PinchValid=PinchPointValid=true;Pinch=Vector3.Distance(Thumb,Index);PinchPoint=(Thumb+Index)*.5f;
            }
            // Index out while the middle finger is in: a point (the excavator draw), never a grab.
            public bool Pointing=>HasTip(1) && HasTip(2) && Vector3.Distance(Index,Palm)-Vector3.Distance(Middle,Palm)>PointSpread;
            public static Hand From(FireToolManipulation.Hand h)=>new Hand{Valid=h.Usable,Palm=h.Contact,Closure=h.Closure};
        }

        // A piece's visible box (world centre, rotation, half extents).
        public readonly struct Candidate
        {
            public readonly int Id;public readonly Vector3 Center;public readonly Quaternion Rotation;public readonly Vector3 Extents;
            public Candidate(int id,Vector3 center,Quaternion rotation,Vector3 extents){Id=id;Center=center;Rotation=rotation;Extents=extents;}
            public Candidate(int id,Bounds world):this(id,world.center,Quaternion.identity,world.extents){}
            // A bare point (no visible box): tests and synthetic callers.
            public Candidate(int id,Vector3 point):this(id,point,Quaternion.identity,Vector3.zero){}
            // Signed distance from p to the box: < 0 inside (the depth), > 0 outside.
            public float Distance(Vector3 p)
            {
                var q=Quaternion.Inverse(Rotation)*(p-Center);
                q=new Vector3(Mathf.Abs(q.x)-Extents.x,Mathf.Abs(q.y)-Extents.y,Mathf.Abs(q.z)-Extents.z);
                float outside=new Vector3(Mathf.Max(q.x,0),Mathf.Max(q.y,0),Mathf.Max(q.z,0)).magnitude;
                return outside+Mathf.Min(Mathf.Max(q.x,Mathf.Max(q.y,q.z)),0);
            }
            // The nearest point of the box to p (p itself when inside).
            public Vector3 Closest(Vector3 p)
            {
                var q=Quaternion.Inverse(Rotation)*(p-Center);
                q=new Vector3(Mathf.Clamp(q.x,-Extents.x,Extents.x),Mathf.Clamp(q.y,-Extents.y,Extents.y),Mathf.Clamp(q.z,-Extents.z,Extents.z));
                return Center+Rotation*q;
            }
        }

        readonly int[] held={-1,-1},hover={-1,-1},dwellOn={-1,-1};
        readonly Mode[] mode=new Mode[2];
        readonly bool[] grabArmed=new bool[2],pinchArmed=new bool[2];
        readonly float[] dwell=new float[2],lost=new float[2],pinchLost=new float[2],hoverDistance={float.PositiveInfinity,float.PositiveInfinity};
        readonly Vector3[] anchor=new Vector3[2],grabAnchor=new Vector3[2],pinchFromPalm=new Vector3[2];
        // This frame's events, one per hand (-1 = none).
        public readonly int[] Grabbed={-1,-1},Released={-1,-1};
        // Set by the caller before Step: a rival target this close (m) to the hand wins over any farther piece.
        public readonly float[] Rival={float.PositiveInfinity,float.PositiveInfinity};
        public int Held(int hand)=>held[hand];
        public Mode HeldMode(int hand)=>held[hand]>=0?mode[hand]:Mode.None;
        public bool OwnsHand(int hand)=>held[hand]>=0;
        public bool Holding(int id)=>held[0]==id || held[1]==id;
        // What this hand would take if it pinched or closed now (-1 = nothing), and how far its nearest point is from it.
        public int Hover(int hand)=>hover[hand];
        public float HoverDistance(int hand)=>hover[hand]>=0?hoverDistance[hand]:float.PositiveInfinity;
        public bool PinchArmed(int hand)=>pinchArmed[hand];
        public bool GrabArmed(int hand)=>grabArmed[hand];
        // Where the hand holds the piece right now: the pinch point (pinch) or the palm centre (grab).
        public Vector3 Anchor(int hand)=>anchor[hand];
        // The anchor at the moment it was taken.
        public Vector3 GrabAnchor(int hand)=>grabAnchor[hand];

        public void Reset()
        {
            for(int h=0;h<2;h++)
            {
                held[h]=hover[h]=dwellOn[h]=Grabbed[h]=Released[h]=-1;mode[h]=Mode.None;grabArmed[h]=pinchArmed[h]=false;
                dwell[h]=lost[h]=pinchLost[h]=0;hoverDistance[h]=Rival[h]=float.PositiveInfinity;
            }
        }
        // Drops what the hand holds without a Released event (the caller decided).
        public void Cancel(int hand){held[hand]=-1;mode[hand]=Mode.None;dwell[hand]=lost[hand]=pinchLost[hand]=0;grabArmed[hand]=false;dwellOn[hand]=-1;}

        // Palm-only hands (no fingertips, no pinch): grab by closing near, as before headset test 3.
        public void Step(FireToolManipulation.Hand left,FireToolManipulation.Hand right,IReadOnlyList<Candidate> candidates,float dt,bool leftBlocked=false,bool rightBlocked=false)
            =>Step(Hand.From(left),Hand.From(right),candidates,dt,leftBlocked,rightBlocked);
        public void Step(Hand left,Hand right,IReadOnlyList<Candidate> candidates,float dt,bool leftBlocked=false,bool rightBlocked=false)
        {
            Grabbed[0]=Grabbed[1]=Released[0]=Released[1]=-1;
            if(!float.IsFinite(dt) || dt<0)dt=0;dt=Mathf.Min(dt,.1f);
            for(int hand=0;hand<2;hand++)
            {
                var h=hand==0?left:right;bool blocked=hand==0?leftBlocked:rightBlocked;
                hover[hand]=-1;hoverDistance[hand]=float.PositiveInfinity;
                if(held[hand]>=0){StepHeld(hand,h,dt);continue;}
                if(!h.Usable || blocked){grabArmed[hand]=pinchArmed[hand]=false;dwell[hand]=0;dwellOn[hand]=-1;continue;}
                StepFree(hand,h,candidates,dt);
            }
        }
        void StepHeld(int hand,in Hand h,float dt)
        {
            if(!h.Usable){lost[hand]+=dt;if(lost[hand]>LostSeconds)Release(hand,h);return;}
            lost[hand]=0;
            bool open;
            if(mode[hand]==Mode.Pinch)
            {
                if(h.PinchPointValid)pinchFromPalm[hand]=h.PinchPoint-h.Palm;
                anchor[hand]=h.PinchPointValid?h.PinchPoint:h.Palm+pinchFromPalm[hand];
                // Parting the tips lets go, unless the hand has closed into a fist around it meanwhile.
                if(h.PinchValid){pinchLost[hand]=0;open=h.Pinch>PinchRelease && h.Closure<Close;}
                else{pinchLost[hand]+=dt;open=pinchLost[hand]>LostSeconds && h.Closure<Open;}
            }
            else
            {
                anchor[hand]=h.Palm;
                // A grabbing hand that eases into a pinch keeps it.
                open=h.Closure<Open && !(h.PinchValid && h.Pinch<PinchRelease);
            }
            if(open)Release(hand,h);
        }
        void Release(int hand,in Hand h)
        {
            Released[hand]=held[hand];held[hand]=-1;mode[hand]=Mode.None;lost[hand]=pinchLost[hand]=dwell[hand]=0;dwellOn[hand]=-1;
            pinchArmed[hand]=h.Usable && h.PinchValid && h.Pinch>PinchArm;grabArmed[hand]=h.Usable && h.Closure<Arm;
        }
        void StepFree(int hand,in Hand h,IReadOnlyList<Candidate> candidates,float dt)
        {
            bool pinchPoint=h.PinchPointValid && float.IsFinite(h.PinchPoint.sqrMagnitude);
            if(h.PinchValid && h.Pinch>PinchArm)pinchArmed[hand]=true;
            else if(!h.PinchValid)pinchArmed[hand]=false; // never a stale arm firing when the tips come back closed
            bool pointing=h.Pointing;float rival=float.IsNaN(Rival[hand])?float.PositiveInfinity:Rival[hand];
            int best=-1;float bestScore=float.PositiveInfinity,bestGrab=float.PositiveInfinity,bestPinch=float.PositiveInfinity,near=float.PositiveInfinity;
            if(candidates!=null)for(int i=0;i<candidates.Count;i++)
            {
                var c=candidates[i];if(Holding(c.Id))continue;
                float grab=c.Distance(h.Palm);
                for(int f=0;f<5;f++)if(h.HasTip(f))grab=Mathf.Min(grab,c.Distance(h.Tip(f)));
                near=Mathf.Min(near,grab);
                float pinch=pinchPoint?c.Distance(h.PinchPoint):float.PositiveInfinity;
                // What makes it takeable right now: an armed pinch whose point is near, or an armed open hand near it.
                float score=float.PositiveInfinity;
                if(pinchArmed[hand] && pinch<=PinchReach)score=pinch;
                if(grabArmed[hand] && !pointing && grab<=GrabReach)score=Mathf.Min(score,grab);
                if(score>=rival || score>=bestScore)continue;
                best=c.Id;bestScore=score;bestGrab=grab;bestPinch=pinch;
            }
            // A grab is armed by opening the hand near a piece, so a fist or a point that arrives closed never grabs.
            if(near>ApproachReach)grabArmed[hand]=false;
            else if(h.Closure<Arm)grabArmed[hand]=true;
            hover[hand]=best;hoverDistance[hand]=bestScore;
            // Pinch: the closing frame takes the hovered piece if the tips met within PinchReach of it (a hover from the open
            // hand alone needs a real close); a pinch that closes on nothing is spent until it opens.
            if(pinchArmed[hand] && h.PinchValid && h.Pinch<PinchClose)
            {
                pinchArmed[hand]=false;
                if(best>=0 && pinchPoint && bestPinch<=PinchReach){Take(hand,h,best,Mode.Pinch);return;}
            }
            // Grab: closed past Close on the same piece for Dwell.
            bool closing=grabArmed[hand] && !pointing && h.Closure>=Close && best>=0 && bestGrab<=GrabReach;
            if(!closing || dwellOn[hand]!=best){dwell[hand]=0;dwellOn[hand]=closing?best:-1;if(!closing)return;}
            dwell[hand]+=dt;
            if(dwell[hand]>=Dwell)Take(hand,h,best,Mode.Grab);
        }
        void Take(int hand,in Hand h,int id,Mode how)
        {
            held[hand]=id;mode[hand]=how;Grabbed[hand]=id;grabArmed[hand]=false;dwell[hand]=0;dwellOn[hand]=-1;lost[hand]=pinchLost[hand]=0;
            if(how==Mode.Pinch){anchor[hand]=h.PinchPoint;pinchFromPalm[hand]=h.PinchPoint-h.Palm;}else anchor[hand]=h.Palm;
            grabAnchor[hand]=anchor[hand];hover[hand]=-1;hoverDistance[hand]=float.PositiveInfinity;
        }
    }
}

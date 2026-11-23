using UnityEngine;

namespace FireRescue
{
    // Open-hand slap left/right to browse the level carousel (LoopSpec §11b). Hands only.
    // A slap: an open palm (not pinching) inside the zone in front of the panel moves sideways faster than Speed
    // and travels at least Travel within Window. Then a Cooldown, and that hand must slow down before it can
    // slap again, so the return swing never pages back. Single-frame jumps larger than MaxStep are tracking
    // pops and restart that hand. Velocity is filtered over Smoothing so jitter alone never reaches Speed.
    public sealed class EmberdeepSlapGesture
    {
        public const float Speed=.9f;          // m/s, filtered lateral palm speed
        public const float Travel=.09f;        // m sideways within Window
        public const float Window=.30f;        // s
        public const float Cooldown=.45f;      // s after a slap, for both hands
        public const float RearmSpeed=.35f;    // m/s: the slapping hand must drop below this first
        public const float MaxStep=.12f;       // m in one frame: a tracking pop, not a hand
        public const float Smoothing=.035f;    // s
        public const float CrossRatio=.9f;     // vertical or depth speed must stay below this x lateral speed
        public struct Hand
        {
            public bool Valid,Open,Pinching;public Vector3 Palm;
            public Hand(bool valid,Vector3 palm,bool open,bool pinching){Valid=valid;Palm=palm;Open=open;Pinching=pinching;}
            public bool Usable=>Valid && float.IsFinite(Palm.x) && float.IsFinite(Palm.y) && float.IsFinite(Palm.z);
        }
        // The zone: a box in front of the panel, in world space. Right is the browse axis.
        public struct Zone
        {
            public Vector3 Center,Right,Up,Forward,HalfExtents;
            public Zone(Pose pose,Vector3 halfExtents){Center=pose.position;Right=pose.rotation*Vector3.right;Up=pose.rotation*Vector3.up;Forward=pose.rotation*Vector3.forward;HalfExtents=halfExtents;}
            public bool Contains(Vector3 p,out Vector3 local)
            {
                var d=p-Center;local=new Vector3(Vector3.Dot(d,Right),Vector3.Dot(d,Up),Vector3.Dot(d,Forward));
                return Mathf.Abs(local.x)<=HalfExtents.x && Mathf.Abs(local.y)<=HalfExtents.y && Mathf.Abs(local.z)<=HalfExtents.z;
            }
        }
        const int History=24;
        sealed class Track
        {
            public bool Tracked,NeedsRearm;public Vector3 Last,Velocity;
            public readonly float[] Times=new float[History],Lateral=new float[History];public int Count,Head;
            public void Clear(){Tracked=false;Velocity=Vector3.zero;Count=0;}
            public void Push(float time,float lateral){Times[Head]=time;Lateral[Head]=lateral;Head=(Head+1)%History;Count=Mathf.Min(Count+1,History);}
        }
        readonly Track[] tracks={new Track(),new Track()};
        float clock,cooldown;
        public int LastDirection {get;private set;}
        public int LastHand {get;private set;}=-1;
        public float LateralSpeed(int hand)=>hand>=0 && hand<2?tracks[hand].Velocity.x:0;
        public void Reset(){foreach(var t in tracks){t.Clear();t.NeedsRearm=false;}cooldown=0;LastDirection=0;LastHand=-1;}
        // Returns +1 for a slap to the right, -1 to the left, 0 otherwise.
        public int Sample(Hand left,Hand right,Zone zone,float dt)
        {
            LastDirection=0;LastHand=-1;
            if(!float.IsFinite(dt) || dt<=0){Reset();return 0;}
            dt=Mathf.Min(dt,.1f);clock+=dt;cooldown=Mathf.Max(0,cooldown-dt);
            int fired=0;
            for(int i=0;i<2;i++)
            {
                var hand=i==0?left:right;var track=tracks[i];
                if(!hand.Usable || !hand.Open || hand.Pinching || !zone.Contains(hand.Palm,out var local)){track.Clear();continue;}
                if(track.Tracked && (hand.Palm-track.Last).magnitude>MaxStep){track.Clear();}
                if(track.Tracked)
                {
                    var d=hand.Palm-track.Last;
                    var measured=new Vector3(Vector3.Dot(d,zone.Right),Vector3.Dot(d,zone.Up),Vector3.Dot(d,zone.Forward))/dt;
                    track.Velocity=Vector3.Lerp(track.Velocity,measured,1-Mathf.Exp(-dt/Smoothing));
                }
                track.Last=hand.Palm;track.Tracked=true;track.Push(clock,local.x);
                float lateral=track.Velocity.x,speed=Mathf.Abs(lateral);
                if(track.NeedsRearm){if(speed<RearmSpeed)track.NeedsRearm=false;continue;}
                if(fired!=0 || cooldown>0 || speed<Speed)continue;
                if(Mathf.Abs(track.Velocity.y)>speed*CrossRatio || Mathf.Abs(track.Velocity.z)>speed*CrossRatio)continue;
                // Travel within the window, in the direction of motion.
                float extreme=local.x;
                for(int k=0;k<track.Count;k++)
                {
                    int idx=(track.Head-1-k+History)%History;if(clock-track.Times[idx]>Window)break;
                    extreme=lateral>0?Mathf.Min(extreme,track.Lateral[idx]):Mathf.Max(extreme,track.Lateral[idx]);
                }
                if(Mathf.Abs(local.x-extreme)<Travel)continue;
                fired=lateral>0?1:-1;LastHand=i;
            }
            if(fired!=0)
            {
                cooldown=Cooldown;LastDirection=fired;
                foreach(var t in tracks){t.Count=0;t.NeedsRearm=true;}
            }
            return fired;
        }
    }
}

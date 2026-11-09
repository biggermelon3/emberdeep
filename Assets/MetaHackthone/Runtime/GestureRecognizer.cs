using System;
namespace PhysicalAssembly
{
    [Flags] public enum Fingers { None=0, Thumb=1, Index=2, Middle=4, Ring=8, Pinky=16 }
    public enum HandCommand { None, Forward, Reverse, Left, Right, Fire }
    public sealed class GestureRecognizer
    {
        public float ActivationSeconds=.15f, ReleaseSeconds=.06f;
        HandCommand pending, active; float pendingSince, invalidSince=-1;
        public static HandCommand Classify(bool left,Fingers extended)
        {
            if(left && extended==(Fingers.Index|Fingers.Middle|Fingers.Thumb)) return HandCommand.Fire;
            if(extended==Fingers.Index) return left?HandCommand.Left:HandCommand.Forward;
            if(extended==(Fingers.Index|Fingers.Middle)) return left?HandCommand.Right:HandCommand.Reverse;
            return HandCommand.None;
        }
        public HandCommand Update(bool tracked,bool left,Fingers extended,float now)
        {
            if(!tracked) { Reset(); return active; }
            var next=Classify(left,extended);
            if(next!=pending) { pending=next; pendingSince=now; }
            if(next==active) invalidSince=-1;
            else
            {
                if(invalidSince<0) invalidSince=now;
                // Fire must not inherit a turn command while its activation timer runs.
                if(next==HandCommand.Fire || now-invalidSince>=ReleaseSeconds) active=HandCommand.None;
                if(next!=HandCommand.None && now-pendingSince>=ActivationSeconds) { active=next; invalidSince=-1; }
            }
            return active;
        }
        public void Reset() { active=pending=HandCommand.None; pendingSince=0; invalidSince=-1; }
    }
}

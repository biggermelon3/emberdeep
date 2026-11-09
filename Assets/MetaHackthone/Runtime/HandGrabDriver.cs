using UnityEngine;

namespace PhysicalAssembly
{
    // Near-grab lifetime is independent of the menu ray's enable/selection state.
    public sealed class HandGrabDriver
    {
        readonly AssemblyWorld world;
        readonly int hand;
        readonly GrabLatch grab=new();
        float lastTracked, openSince=-1; bool frozen,fist;
        public bool NeedsFullGripConfidence => world.HeldPart(hand) && fist;
        public const float TrackingGrace=.35f;
        public HandGrabDriver(AssemblyWorld world,int hand) { this.world=world; this.hand=hand; }
        public void Sample(bool tracked,float tipDistance,bool systemPinch,Pose pose,AssemblyPart candidate,bool allowStart,float now,float gripStrength=0)
        {
            if(world.FloatingBuild && world.HeldPart(hand)) {
                if(!tracked) { openSince=-1; frozen=true; world.FreezeHold(hand); return; }
                bool open=fist?gripStrength<.75f:tipDistance>=.03f;
                if(open) {
                    world.FreezeHold(hand);
                    if(openSince<0) openSince=now;
                    if(now-openSince>=.08f) { world.Release(hand,true); grab.Reset(); openSince=-1; frozen=false; }
                    return;
                }
                openSince=-1;
                if(frozen) { world.RebaseHold(hand,pose); frozen=false; }
                world.SetHoldPose(hand,pose); return;
            }
            if(!tracked)
            {
                if(now-lastTracked>=TrackingGrace) Cancel();
                return;
            }
            lastTracked=now;
            bool before=grab.IsClosed;
            bool closed=grab.Update(true,tipDistance,systemPinch,gripStrength);
            if(world.HeldPart(hand))
            {
                // Release at the last held pose on the first separated sample;
                // opening the fingers must not drag the object to their midpoint.
                if(!closed) world.Release(hand,true);
                else world.SetHoldPose(hand,pose);
                return;
            }
            if(allowStart && closed && !before && candidate && world.TryGrab(hand,candidate,pose)) { fist=gripStrength>=GrabLatch.FullGripThreshold; frozen=false; openSince=-1; }
        }
        public void Cancel() { world.Release(hand,false); grab.Reset(); }
    }
}

using CloudGarden.ShadowPuzzle;
using UnityEngine;

namespace FireRescue
{
    // Pickup latches the grip so index/thumb remain free to operate the trigger.
    // Return to the rack with an open hand to holster. Never fire on the pickup pinch.
    public sealed class FireEquipmentGrab
    {
        readonly ShadowPinchGesture[] pickup={new ShadowPinchGesture(),new ShadowPinchGesture()};
        public bool Held { get; private set; }
        public int Hand { get; private set; }=1;
        bool leftRack;
        float openAtRack;
        public void EquipForDesktop(int hand=1){Held=true;Hand=hand;leftRack=false;openAtRack=0;}
        public void Return(){Held=false;leftRack=false;openAtRack=0;foreach(var g in pickup)g.Reset();}
        public void ConsumePickup(){foreach(var g in pickup)g.Reset();}
        public bool Sample(int hand,bool valid,float pinch,Vector3 palm,Vector3 grip,bool owned,float dt)
        {
            if(hand<0 || hand>1)return false;
            if(Held)
            {
                if(hand!=Hand)return false;
                // System palm menus temporarily suppress app input. Keep ownership;
                // the presentation freezes separately and weapons must rearm.
                if(!valid){openAtRack=0;pickup[hand].Reset();return false;}
                float distance=Vector3.Distance(palm,grip);
                if(distance>.18f)leftRack=true;
                openAtRack=leftRack && distance<.1f && pinch>.03f?openAtRack+dt:0;
                if(openAtRack>.25f){Return();return true;}return false;
            }
            pickup[hand].Sample(valid,pinch,dt);
            if(owned && pickup[hand].Pressed && Vector3.Distance(palm,grip)<.1f)
            {Held=true;Hand=hand;leftRack=false;openAtRack=0;return true;}
            return false;
        }
    }
}

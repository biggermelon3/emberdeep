using CloudGarden.ShadowPuzzle;
using UnityEngine;

namespace FireRescue
{
    // Optional off-hand pump. All distances are metres, relative to the held tool.
    // Tracking loss or moving sideways cancels; neither counts as releasing a shot.
    public sealed class FirePumpGesture
    {
        readonly ShadowPinchGesture pinch=new ShadowPinchGesture();
        public bool Active { get; private set; }
        public float Pull01 { get; private set; }
        public bool Released { get; private set; }
        public bool Cancelled { get; private set; }
        Vector3 start;
        public void Sample(bool valid,float distance,Vector3 hand,Pose handle,float dt)
        {
            Released=Cancelled=false;
            if(!valid || !Finite(distance) || !Finite(hand.x) || !Finite(hand.y) || !Finite(hand.z))
            {Cancelled=Active;Active=false;Pull01=0;pinch.Reset();return;}
            pinch.Sample(true,distance,dt);
            var local=Quaternion.Inverse(handle.rotation)*(hand-handle.position);
            if(!Active)
            {
                Pull01=0;
                if(pinch.Pressed && local.magnitude<.085f){Active=true;start=local;}
                return;
            }
            if(new Vector2(local.x-start.x,local.y-start.y).magnitude>.16f || local.magnitude>.3f)
            {Cancelled=true;Active=false;Pull01=0;pinch.Reset();return;}
            Pull01=Mathf.Max(Pull01,Mathf.Clamp01((start.z-local.z)/.09f));
            if(!pinch.Closed){Released=Pull01>.15f;Cancelled=!Released;Active=false;}
        }
        public void Reset(){pinch.Reset();Active=Released=Cancelled=false;Pull01=0;}
        static bool Finite(float value)=>!float.IsNaN(value) && !float.IsInfinity(value);
    }
}

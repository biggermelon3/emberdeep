using CloudGarden.Integration;
using UnityEngine;

namespace FireRescue
{
    // Quest uses its tracked pointer; PICO uses filtered live knuckle aim.
    // Never infer aim from the fingertip that curls to click. The menu owns
    // open-then-pinch rearming; pointing itself remains visible while closed.
    public sealed class EmberdeepHandRay
    {
        public struct Sample
        {
            public bool Valid;
            public Ray Ray;
            public Vector3 VisualOrigin;
            public float Pinch;
            public bool PinchUnavailable;
            public Sample(bool valid,Ray ray,float pinch){Valid=valid;Ray=ray;VisualOrigin=ray.origin;Pinch=pinch;PinchUnavailable=false;}
        }
        public void Reset(){} // No cached poses or gesture state in the ray adapter.

        public Sample Read(MetaShadowPlayer provider,bool left)
        {
            if(!provider || !provider.ReadMenuHand(left,out var palm,out var pointer,out var pinch,out bool pinchAvailable))return Invalid();
            var sample=FromPointerPose(true,pointer,pinch,pinchAvailable);
            // The mathematical pointer begins near the wrist; the visual starts
            // at the palm, never at the curling index tip.
            sample.VisualOrigin=palm.position;return sample;
        }
        public Sample FromPointerPose(bool tracked,Pose pointer,float pinch,bool pinchAvailable=true)
        {
            if(!tracked || !Finite(pointer.position) || !Finite(pointer.rotation)
                || (pinchAvailable && (!Finite(pinch) || pinch<0)))return Invalid();
            var q=pointer.rotation;
            float norm=q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w;
            if(norm<.5f || norm>1.5f)return Invalid();
            return new Sample(true,new Ray(pointer.position,pointer.rotation*Vector3.forward),pinch){PinchUnavailable=!pinchAvailable};
        }
        Sample Invalid(){Reset();return new Sample(false,default,float.PositiveInfinity);}
        public static bool Valid(Sample sample)=>sample.Valid && Finite(sample.Ray.origin) && Finite(sample.VisualOrigin) && Finite(sample.Ray.direction)
            && sample.Ray.direction.sqrMagnitude>.5f && (sample.PinchUnavailable || (Finite(sample.Pinch) && sample.Pinch>=0));
        static bool Finite(Vector3 v)=>Finite(v.x)&&Finite(v.y)&&Finite(v.z);
        static bool Finite(Quaternion q)=>Finite(q.x)&&Finite(q.y)&&Finite(q.z)&&Finite(q.w);
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
}

using CloudGarden.Integration;
using UnityEngine;

namespace FireRescue
{
    // Meta's system-defined hand pointer pose (wrist/hand aim), also used by
    // HandPointerPose. Never infer aim from a fingertip that must curl to click.
    // The shared provider retains its existing filtered PICO aim fallback.
    public sealed class EmberdeepHandRay
    {
        public struct Sample
        {
            public bool Valid;
            public Ray Ray;
            public Vector3 VisualOrigin;
            public float Pinch;
            public Sample(bool valid,Ray ray,float pinch){Valid=valid;Ray=ray;VisualOrigin=ray.origin;Pinch=pinch;}
        }
        bool opened;
        public void Reset()=>opened=false;

        public Sample Read(MetaShadowPlayer provider,bool left)
        {
            if(!provider || !provider.ReadHand(left,out var palm,out _,out var pinch)
                || !provider.ReadInteractionAim(left,out var pointer))return Invalid();
            var sample=FromPointerPose(true,pointer,pinch);
            // The mathematical pointer begins near the wrist; the visual starts
            // at the palm, never at the curling index tip.
            sample.VisualOrigin=palm.position;return sample;
        }
        public Sample FromPointerPose(bool tracked,Pose pointer,float pinch)
        {
            if(!tracked || !Finite(pointer.position) || !Finite(pointer.rotation)
                || !Finite(pinch) || pinch<0)return Invalid();
            var q=pointer.rotation;
            float norm=q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w;
            if(norm<.5f || norm>1.5f)return Invalid();
            if(pinch>=.035f)opened=true;
            if(!opened)return new Sample(false,default,pinch);
            return new Sample(true,new Ray(pointer.position,pointer.rotation*Vector3.forward),pinch);
        }
        Sample Invalid(){Reset();return new Sample(false,default,float.PositiveInfinity);}
        public static bool Valid(Sample sample)=>sample.Valid && Finite(sample.Ray.origin) && Finite(sample.VisualOrigin) && Finite(sample.Ray.direction)
            && sample.Ray.direction.sqrMagnitude>.5f && Finite(sample.Pinch) && sample.Pinch>=0;
        static bool Finite(Vector3 v)=>Finite(v.x)&&Finite(v.y)&&Finite(v.z);
        static bool Finite(Quaternion q)=>Finite(q.x)&&Finite(q.y)&&Finite(q.z)&&Finite(q.w);
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
}

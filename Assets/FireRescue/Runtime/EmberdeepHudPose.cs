using UnityEngine;

namespace FireRescue
{
    // World-space interface placement is independent of miniature forest scale.
    // Called only at placement/recenter and page transitions, never to follow a head.
    public static class EmberdeepHudPose
    {
        public const float ForwardDistance=.9f;
        public const float EyeDrop=.04f;

        public static bool TryCalculate(Pose head,float floorHeight,Vector3 previousForward,out Pose panel)
        {
            panel=default;
            if(!Finite(head.position) || !Finite(head.rotation) || !Finite(floorHeight)
                || head.position.y-floorHeight<EmberdeepMenuPose.MinimumEyeHeight)return false;
            var q=head.rotation;float norm=q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w;
            if(!Finite(norm) || norm<.000001f)return false;
            var forward=Vector3.ProjectOnPlane(q.normalized*Vector3.forward,Vector3.up);
            if(forward.sqrMagnitude<.01f)
            {
                if(!Finite(previousForward))return false;
                forward=Vector3.ProjectOnPlane(previousForward,Vector3.up);
                if(forward.sqrMagnitude<.01f)return false;
            }
            forward.Normalize();
            panel=new Pose(head.position+forward*ForwardDistance-Vector3.up*EyeDrop,
                Quaternion.LookRotation(forward,Vector3.up));
            return true;
        }

        static bool Finite(float value)=>!float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value)=>Finite(value.x) && Finite(value.y) && Finite(value.z);
        static bool Finite(Quaternion value)=>Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);
    }
}

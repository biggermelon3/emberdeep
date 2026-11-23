using UnityEngine;

namespace FireRescue
{
    // The petted-sheep "boing": a crouch, a big hop, a squash on landing, a smaller hop, then a damped
    // wobble back to rest. Pure function of time since the pet, so any sheep (fireground, camp) can play
    // it from its own clock. Lift is in the sheep's own units (body height ~1); stretch scales Y, and X/Z
    // get 1/sqrt(stretch) so the volume reads constant.
    public static class EmberdeepSheepHop
    {
        public const float Crouch=.08f,FirstAir=.34f,Land=.07f,SecondAir=.22f,Settle=.26f;
        public const float FirstHeight=.55f,SecondHeight=.2f;
        public const float CrouchSquash=.22f,TakeoffStretch=.2f,LandSquash=.28f,SecondStretch=.1f,SecondSquash=.14f,Wobble=.07f;
        public const float Duration=Crouch+FirstAir+Land+SecondAir+Land+Settle;
        // When the pet sound's boing should start, relative to the pet: the first take-off.
        public const float TakeoffTime=Crouch;

        // Returns false (lift 0, stretch 1) outside the hop.
        public static bool Evaluate(float time,out float lift,out float stretch)
        {
            lift=0;stretch=1;
            if(!(time>=0) || time>=Duration)return false;
            float t=time;
            if(t<Crouch){stretch=1-CrouchSquash*Smooth(t/Crouch);return true;}
            t-=Crouch;
            if(t<FirstAir){float u=t/FirstAir;lift=FirstHeight*4*u*(1-u);stretch=1+TakeoffStretch*Mathf.Pow(Mathf.Abs(1-2*u),1.5f);return true;}
            t-=FirstAir;
            if(t<Land){stretch=1-LandSquash*Mathf.Sin(Mathf.PI*t/Land);return true;}
            t-=Land;
            if(t<SecondAir){float u=t/SecondAir;lift=SecondHeight*4*u*(1-u);stretch=1+SecondStretch*Mathf.Pow(Mathf.Abs(1-2*u),1.5f);return true;}
            t-=SecondAir;
            if(t<Land){stretch=1-SecondSquash*Mathf.Sin(Mathf.PI*t/Land);return true;}
            t-=Land;
            float s=t/Settle;stretch=1+Wobble*Mathf.Sin(2*Mathf.PI*1.5f*s)*Mathf.Exp(-4*s);
            return true;
        }
        public static Vector3 Scale(float stretch){float side=1/Mathf.Sqrt(Mathf.Max(.2f,stretch));return new Vector3(side,stretch,side);}
        static float Smooth(float u){u=Mathf.Clamp01(u);return u*u*(3-2*u);}
    }
}

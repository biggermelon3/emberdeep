using UnityEngine;

namespace FireRescue
{
    // A full-hand grasp, independent of thumb/index pinch. Reacquisition always
    // needs an observed open hand; a returning tracked fist cannot activate it.
    public sealed class EmberdeepGripGesture
    {
        public const float CloseStrength=.8f,ReleaseStrength=.55f,DwellSeconds=.06f;
        bool armed;
        float openSeconds,closeSeconds;
        public bool Closed { get; private set; }
        public bool Pressed { get; private set; }
        public void Reset(){armed=Closed=Pressed=false;openSeconds=closeSeconds=0;}
        public void Sample(bool valid,float strength,float dt)
        {
            Pressed=false;
            if(!valid || float.IsNaN(strength) || float.IsInfinity(strength) || strength<0 || strength>1
                || float.IsNaN(dt) || float.IsInfinity(dt) || dt<=0){Reset();return;}
            dt=Mathf.Min(dt,.025f);
            if(strength<=ReleaseStrength)
            {
                Closed=false;closeSeconds=0;openSeconds+=dt;
                if(openSeconds+.00001f>=DwellSeconds)armed=true;
                return;
            }
            openSeconds=0;
            if(Closed)return;
            if(!armed || strength<CloseStrength){closeSeconds=0;return;}
            closeSeconds+=dt;
            if(closeSeconds+.00001f<DwellSeconds)return;
            Closed=Pressed=true;armed=false;closeSeconds=0;
        }
    }
}

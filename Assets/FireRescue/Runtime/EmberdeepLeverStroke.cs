using UnityEngine;

namespace FireRescue
{
    // Mechanical cycle only: both a connected tank and an empty outlet use the
    // same acceptance rule. This class cannot grant water, money or damage.
    public sealed class EmberdeepLeverStroke
    {
        public const float DownMetres=.08f,ReturnMetres=.02f;
        bool started;
        public bool ReturnRequired { get; private set; }
        public float Travel01 { get; private set; }
        public void Reset(){started=ReturnRequired=false;Travel01=0;}
        public static bool ValidTravel(float travel)=>!float.IsNaN(travel) && !float.IsInfinity(travel) && travel>=-.02f && travel<=.3f;
        public bool Sample(float travel,bool held,bool tracked)
        {
            if(!held || !tracked || !ValidTravel(travel)){Reset();return false;}
            Travel01=Mathf.Clamp01(travel/DownMetres);
            if(!started)
            {
                if(travel>ReturnMetres)return false;
                started=true;return false;
            }
            if(travel>=DownMetres-.00001f){ReturnRequired=true;return false;}
            if(!ReturnRequired || travel>ReturnMetres+.00001f)return false;
            ReturnRequired=false;return true;
        }
    }
}

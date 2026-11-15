using UnityEngine;

namespace FireRescue
{
    // Real return travel, including short strokes in the middle of the range.
    // Reversal hysteresis rejects tremor; stationary samples earn nothing.
    public sealed class EmberdeepLeverStroke
    {
        public const float DownMetres=.08f,ReturnMetres=.02f;
        public const float MaximumTravel=.16f, MinimumStroke=.012f, ReversalDeadband=.008f;
        bool started,returning;
        float top,peak,trough,credited;
        public bool ReturnRequired { get; private set; }
        public float Travel01 { get; private set; }
        public float ReturnedMetres { get; private set; }
        public float WaterUnits=>ReturnedMetres/DownMetres*EmberdeepWaterSupply.PumpUnits;
        public void Reset(){started=returning=ReturnRequired=false;top=peak=trough=credited=Travel01=ReturnedMetres=0;}
        public static bool ValidTravel(float travel)=>!float.IsNaN(travel) && !float.IsInfinity(travel) && travel>=-.02f && travel<=.3f;
        public bool Sample(float travel,bool held,bool tracked)
        {
            ReturnedMetres=0;
            if(!held || !tracked || !ValidTravel(travel)){Reset();return false;}
            travel=Mathf.Clamp(travel,0,MaximumTravel);Travel01=travel/MaximumTravel;
            if(!started){started=true;top=peak=trough=travel;return false;}
            if(returning)
            {
                trough=Mathf.Min(trough,travel);
                if(travel-trough>=ReversalDeadband)
                {returning=false;top=trough;peak=travel;credited=0;}
            }
            if(!returning)
            {
                peak=Mathf.Max(peak,travel);
                if(peak-top>=MinimumStroke && peak-travel>=ReversalDeadband)
                {returning=true;trough=travel;}
                else if(travel<top){top=peak=travel;}
            }
            if(returning)
            {
                float total=Mathf.Clamp(peak-travel,0,peak-top);
                ReturnedMetres=Mathf.Max(0,total-credited);credited=Mathf.Max(credited,total);
            }
            ReturnRequired=peak-top>=MinimumStroke && credited<peak-top-.00001f;
            return ReturnedMetres>.000001f;
        }
    }
}

using UnityEditor;
using UnityEngine;

namespace FireRescue.Editor
{
    // Dev toggles for the water loop until the camp settings page (workstream O) ships.
    // In Play mode the fireground also takes F8 (water hand) and F9 (rhythm bonus).
    public static class EmberdeepWaterDevMenu
    {
        const string Hand="Emberdeep/Dev/Water hand is right",Rhythm="Emberdeep/Dev/Pump rhythm bonus";
        [MenuItem(Hand,false,200)]
        static void ToggleHand(){EmberdeepWaterHand.Reload();EmberdeepWaterHand.Toggle();Debug.Log("Emberdeep water hand: "+EmberdeepWaterHand.WaterName);}
        [MenuItem(Hand,true)]
        static bool HandState(){EmberdeepWaterHand.Reload();Menu.SetChecked(Hand,!EmberdeepWaterHand.Left);return true;}
        [MenuItem(Rhythm,false,201)]
        static void ToggleRhythm(){EmberdeepWaterTuning.RhythmEnabled=!EmberdeepWaterTuning.RhythmEnabled;Debug.Log("Emberdeep pump rhythm bonus: "+(EmberdeepWaterTuning.RhythmEnabled?"on":"off"));}
        [MenuItem(Rhythm,true)]
        static bool RhythmState(){Menu.SetChecked(Rhythm,EmberdeepWaterTuning.RhythmEnabled);return true;}
    }
}

using UnityEngine;

namespace FireRescue
{
    // LoopSpec §11b layout A. In the production (spell) flow there is no panel during Plan or Fight:
    // the board's front rim carries the readouts, a poked token pauses, a struck bell starts. The hand ray
    // exists only between levels (results, level select, upgrades) and on the pause panel.
    // The legacy tool flow keeps its dashboard unchanged.
    public sealed partial class FireRescueGame
    {
        bool RimUi=>Run.SpellMode;
        public bool PauseOpen {get;private set;}
        public bool LevelSelectOpen {get;private set;}
        // Screens that freeze the board the way the shop does.
        bool RimBlocksPlay=>RimUi && (PauseOpen || LevelSelectOpen);
        // §11b: the ray is off in Plan and Fight unless a panel is up.
        public bool RayAllowed=>!RimUi || QuitConfirmationOpen || ShopOpen || PauseOpen || LevelSelectOpen
            || (Run.Phase!=FirePhase.Preparing && Run.Phase!=FirePhase.Fighting);

        public EmberdeepRimHud RimHud {get;private set;}

        void RefreshRim()
        {
            if(!RimUi)return;
            if(dashboard && dashboard.gameObject.activeSelf)dashboard.gameObject.SetActive(false);
            if(!RimHud){RimHud=gameObject.AddComponent<EmberdeepRimHud>();RimHud.Initialize(this);}
            RefreshScreens();RefreshHints();RefreshSpirit(); // Plan-phase voice-over (FireRescueGame.Spirit.cs)
        }
    }
}

namespace FireRescue
{
    public sealed partial class FireRescueGame
    {
        public bool BeginIncident()
        {
            if(ShopOpen || QuitConfirmationOpen || QuitRequested)return false;
            bool started=Run.BeginCountdown();if(started){ResetInput();introStarted=false;Refresh();}return started;
        }
        void RefreshPreparation()
        {
            // LoopSpec §11b: the spell flow starts with the rim bell (FireRescueGame.Rim.cs) and shows no Plan text.
            if(Run.Phase!=FirePhase.Preparing || Run.SpellMode)return;
            bool counting=Run.CountdownRemaining>=0;
            status.text=$"{Run.Stage+1}/{Run.StageCount}  {Run.StageName}";
            mission.text=counting?$"STARTING IN {UnityEngine.Mathf.CeilToInt(Run.CountdownRemaining)}":$"GET READY / {Run.Burning} FIRES / {Run.Animals.Length} SHEEP";
            tactics.text=Run.IsPalmLesson?"PALM WATER / Protect the sheep":Run.SpellMode?"SPELLS / Protect the sheep":"TOOLS + PALM WATER / Protect the sheep";
            help.text=counting?"Get comfortable. The fire starts after the countdown.":"Look around, pet the sheep, then press START.";
            actionButton.Interactable=!counting;actionText.text=counting?"GET READY":"START INCIDENT >";
            upgradeButton.Interactable=!counting;upgradeText.text="GEAR SHOP >";
        }
    }
}

using UnityEngine;

namespace FireRescue
{
    // LoopSpec §11b first-time hints in the spell flow. One ghost at a time, on the player's own hand or object:
    // the bell in Plan, the carousel slap in level select, the pump when the gem runs low, the palm water
    // otherwise. Each retires after EmberdeepFirstHints.Shows real performances (PlayerPrefs).
    public sealed partial class FireRescueGame
    {
        public EmberdeepFirstHints Hints {get;private set;}
        bool hintWasSpraying;int hintPumpStrikes;
        // Desktop: the ghost plays beside the on-screen gem.
        static readonly Vector3 DesktopHintLocal=new(-.11f,-.06f,.42f);

        void RefreshHints()
        {
            if(!RimUi){if(Hints)Hints.Hide();return;}
            if(!Hints)Hints=gameObject.AddComponent<EmberdeepFirstHints>();
            if(MagicSpraying && !hintWasSpraying)Hints.Performed(EmberdeepFirstHints.Move.Water);
            hintWasSpraying=MagicSpraying;
            if(PumpStrikes>hintPumpStrikes)Hints.Performed(EmberdeepFirstHints.Move.Pump);
            hintPumpStrikes=PumpStrikes;
            var cam=Player?Player.ViewCamera:null;
            bool world=cam && Arena && Arena.gameObject.activeInHierarchy && focused && !paused && !acquiringPose && !returningToCamp && !QuitConfirmationOpen;
            if(!world){Hints.Hide();return;}
            bool fight=Run.Phase==FirePhase.Fighting && !PauseOpen && !ShopOpen;
            if(LevelSelectOpen && !ShopOpen && SlapHintWanted && EmberdeepFirstHints.Needed(EmberdeepFirstHints.Move.Slap) && selectRoot)
                Hints.Present(EmberdeepFirstHints.Move.Slap,SlapHintPose(),SlapHintScale,false,cam);
            else if(Run.Phase==FirePhase.Preparing && Run.CountdownRemaining<0 && !RimBlocksPlay && RimHud && RimHud.BellVisible
                && EmberdeepFirstHints.Needed(EmberdeepFirstHints.Move.Bell))
                Hints.Present(EmberdeepFirstHints.Move.Bell,Facing(RimHud.BellTop+RimHud.BellUp*.01f,cam),1,false,cam);
            else if(fight && Run.GemMode && pumpView && pumpView.gameObject.activeInHierarchy && (Run.GemEmpty || Run.GemFill01<.2f)
                && EmberdeepFirstHints.Needed(EmberdeepFirstHints.Move.Pump))
                Hints.Present(EmberdeepFirstHints.Move.Pump,Facing(pumpView.CapTop+pumpView.Up*.01f,cam),1,waterLeft,cam); // the free fist (pump on the free-hand side)
            else if(fight && !Run.GemEmpty && EmberdeepFirstHints.Needed(EmberdeepFirstHints.Move.Water))
                Hints.Present(EmberdeepFirstHints.Move.Water,WaterHintPose(cam),Player.IsXR?.9f:.55f,!waterLeft,cam);
            else Hints.Hide();
        }
        // Slap hint (headset test 2 2026-11-22). It was gated on a second unlocked level, so a first-time player (one level)
        // never saw it, yet locked cards browse too: now it shows whenever the carousel has another card. It used to play
        // on the panel (~1.1 m away, half size in view, and not where a slap counts); it plays in the slap zone itself
        // (SlapZone, ~.5 m out), at a hand's real size, sweeping across the cards in view.
        public bool SlapHintWanted=>Run.StageCount>1;
        // Raised so the hand sweeps across the cards' lower half rather than over PLAY (first capture: it covered PLAY).
        public const float SlapHintScale=1.1f,SlapHintRaise=.045f; // -, m over the zone centre (the ghost's fingers point up)
        public Pose SlapHintPose()
        {
            var zone=SlapZone();
            return new Pose(zone.Center+zone.Up*SlapHintRaise,Quaternion.LookRotation(zone.Forward,Vector3.up));
        }
        static Pose Facing(Vector3 at,Camera cam)
        {
            var flat=Vector3.ProjectOnPlane(at-cam.transform.position,Vector3.up);
            return new Pose(at,Quaternion.LookRotation(flat.sqrMagnitude>1e-4f?flat.normalized:Vector3.forward,Vector3.up));
        }
        // On the player's own water hand when it is tracked; else above the near rim on the water-hand side.
        Pose WaterHintPose(Camera cam)
        {
            if(!Player.IsXR)return new Pose(cam.transform.TransformPoint(DesktopHintLocal),cam.transform.rotation);
            var hand=toolHands[WaterHand];
            var at=hand.Usable?hand.Contact+Vector3.up*.07f:Arena.TransformPoint(new Vector3(waterLeft?-2.5f:2.5f,4.6f,3.8f));
            return new Pose(at,cam.transform.rotation);
        }
    }
}

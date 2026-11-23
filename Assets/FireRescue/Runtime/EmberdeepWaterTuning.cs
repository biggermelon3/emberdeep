namespace FireRescue
{
    // Every *tune* value of the water loop (LoopSpec §3) in one place. Distances are real
    // metres (hands, pump, gem); radii marked "forest" are diorama units (x FireMiniature.Scale).
    public static class EmberdeepWaterTuning
    {
        // §3.1 / §3.2 The gem on the back of the water hand is the only water store.
        public const float GemCapacity=40;            // level 0
        public const float GemCapacityPerLevel=20;    // each GODDESS HAND upgrade
        public const float GemFlow=14;                // units/s while spraying (FireRun.MagicFlow)
        public const float GemScale=1.25f;            // visual size of the crystal kit
        // §3.3 Passive refill: only while not spraying, after a short wait.
        public const float PassiveDelaySeconds=1.5f;
        public const float PassivePerSecond=2.5f;     // ~1/3 of steady pumping
        // §3.4 Fist-hammer pump.
        // Forest units, mirrored to the water-hand side. Beside the compact board's front corner (workstream O,
        // EmberdeepBoardLayout: was 3.3/.2/.4 on the old deep board).
        public const float PumpSideX=EmberdeepBoardLayout.PumpSideX, PumpHeight=EmberdeepBoardLayout.PumpHeight, PumpForward=EmberdeepBoardLayout.PumpForward;
        // Headset test 2026-11-22: hammering with the water hand turns the gem away from the eye. The pump stands on the
        // FREE-hand side, so the natural strike is the free fist while the gem stays in view (either hand still strikes).
        public static bool PumpLeft(bool waterLeft)=>!waterLeft;
        public static float PumpSide(bool waterLeft)=>PumpLeft(waterLeft)?-1:1;
        public const float StrikeWater=4;            // units per accepted strike
        public const float OnBeatWater=7;             // units per strike inside the rhythm window
        public const float StrikeSpeed=.55f;          // m/s, minimum downward fist speed at the cap
        public const float StrikeClosure=.55f;        // 0 open .. 1 fist; below this the hand is not a fist
        public const float StrikeRadius=.075f;        // m, horizontal reach around the cap centre
        public const float StrikeContactHeight=.035f; // m above the cap top where the fist meets it
        public const float RearmHeight=.10f;          // m above the cap top before the next strike
        public const float SpeedSmoothingSeconds=.03f;// fist velocity filter (tracking jitter)
        public const float PlungerTravel=.028f;       // m the cap sinks on a strike (visual only)
        public const float SplashFlightSeconds=.30f;  // spout to gem
        // §3.4 Rhythm bonus. Flip RhythmEnabledDefault (or the dev key) to feel-test without it.
        public const bool RhythmEnabledDefault=true;
        public static bool RhythmEnabled=RhythmEnabledDefault;
        public const float BeatSeconds=.55f;
        public const float BeatWindowSeconds=.11f;    // +/- around each beat
        public const float RhythmSessionSeconds=1.4f; // no strike for this long ends the beat
        // The ONE rhythm cue (headset test 2026-11-22: three "hit now" signals confused): a ring shrinks onto a fixed
        // target ring around the cap and meets it on the beat. Radii in cap radii. After a strike the target ring flashes
        // for StrikeFlashSeconds as feedback only: gold on the beat, rust off it, pale on the strike that starts a beat.
        public const float BeatTargetRing=1.2f, BeatApproachRing=2.6f;
        public const float StrikeFlashSeconds=.22f;
        // §3.7 Aim indicator.
        public const float ArcWidth=.0065f;           // m, ribbon from palm to the first surface
        // Playtest 2026-11-22 ("two curves, one water, one aim; can't read where it lands"): while palm water flows the
        // stream itself is the curve, so the aim arc (ribbon, core and its motes) is hidden; the landing marker and the
        // crown rings stay. ArcCastingAlpha > 0 brings the arc back while casting (EmberdeepPalmAimVfx.ShowAim).
        public const float ArcAlpha=.62f, ArcCastingAlpha=0;
        public const float CrownRingRadius=.62f;      // forest units around a highlighted crown
        public const float CrownRingHeight=1.25f;     // forest units above the tree's ground point
        public const float CrownRingWidth=.0035f;     // m
        public const float ImpactMarkerRadius=.012f;  // m
    }
}

namespace FireRescue
{
    // LoopSpec §9 camp tutorial steps, as pure logic (EmberdeepCampPlay feeds it what happened at camp each frame).
    //   Intro: the forest spirit arrives and says hello (voice-over, 2026-11-22; Begin(true)).
    //   Campfire: put the campfire out with palm water.
    //   Pump: top the gem up at the pump (a strike that leaves it full).
    //   Sheep: a spark lights a sheep, it runs and lights the sapling; put both out.
    //   Pet: pet the sheep you saved.
    // A met step holds for Beat seconds (its "done" moment) before the next one starts, and that moment waits while the
    // spirit is still talking (quiet false), so no step starts under a line. Done lingers DoneSeconds for the celebration
    // and until the spirit has been quiet for OutroHold, then Finished tells the camp to bring the menu back.
    public sealed class EmberdeepCampTutorialFlow
    {
        public enum Step { Off, Intro, Campfire, Pump, Sheep, Pet, Done }
        public const float Beat=.9f,DoneSeconds=3f,OutroHold=1.2f;
        public const int Chips=3; // palm water, pump, sheep (the pet closes the sheep chip)
        public Step Current {get;private set;}
        public float StepTime {get;private set;}
        float beat=-1,quietFor;
        public bool Active=>Current!=Step.Off;
        public bool Running=>Current!=Step.Off && Current!=Step.Done;
        // The current step is met and its done moment is playing (or waiting for the spirit to finish).
        public bool Completing=>beat>=0;
        public bool Finished=>Current==Step.Done && StepTime>=DoneSeconds && quietFor>=OutroHold;
        // Which progress chip the step lights (0..2), Chips once everything is done, -1 before the first (intro).
        public int Chip=>Current==Step.Campfire?0:Current==Step.Pump?1:Current==Step.Sheep || Current==Step.Pet?2:Current==Step.Done?Chips:-1;
        public void Begin(bool intro=false){Current=intro?Step.Intro:Step.Campfire;StepTime=0;beat=-1;quietFor=0;}
        public void Stop(){Current=Step.Off;StepTime=0;beat=-1;quietFor=0;}
        // Facts from the camp this frame, with no voice-over (the intro counts as done, the spirit as quiet).
        public bool Tick(float dt,bool campfireOut,bool gemPumpedFull,bool sheepSaved,bool sheepPetted)
            =>Tick(dt,campfireOut,gemPumpedFull,sheepSaved,sheepPetted,true,true);
        // Returns true on the frame the step changes.
        public bool Tick(float dt,bool campfireOut,bool gemPumpedFull,bool sheepSaved,bool sheepPetted,bool introDone,bool quiet)
        {
            if(!Active || !(dt>=0) || float.IsInfinity(dt))return false;
            dt=dt>.1f?.1f:dt;StepTime+=dt;quietFor=quiet?quietFor+dt:0;
            if(Current==Step.Done)return false;
            bool met=Current==Step.Intro?introDone:Current==Step.Campfire?campfireOut:Current==Step.Pump?gemPumpedFull:Current==Step.Sheep?sheepSaved:sheepPetted;
            if(beat<0){if(met)beat=0;return false;}
            if(!quiet)return false;
            beat+=dt;if(beat<Beat)return false;
            Current++;StepTime=0;beat=-1;return true;
        }
    }
}

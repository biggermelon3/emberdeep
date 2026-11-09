namespace FireRescue
{
    // Presentation-only progression reads the accepted supply transaction. Showing
    // a tutorial never changes a tank, valve, checkpoint or input ownership.
    public static class EmberdeepRefillTutorial
    {
        public enum Target { None, Pickup, Socket, Valve, Intake, IntakeSocket, Pump, Grip, Lever, Button }
        public enum Step { TakeTool, BringTool, HoldStill, OpenValve, Filling, PlaceIntake, PullPump, ReturnPump, Full, Busy, Unavailable, LowerLever, ReturnLever, PressButton, TryPump }
        public readonly struct Cue
        {
            public readonly Step Stage;
            public readonly Target Focus;
            public readonly string Text;
            public Cue(Step stage,Target focus,string text){Stage=stage;Focus=focus;Text=text;}
        }
        public static Cue Describe(FireRun run,bool creek,bool held,bool available,bool carryingIntake=false,
            bool emptyOutlet=false,bool emptyReturnRequired=false)
        {
            if(!available)return new Cue(Step.Unavailable,Target.None,"HANDS PAUSED\nShow your hands to continue");
            var supply=run.Supply;
            var kind=creek?WaterSourceKind.Creek:WaterSourceKind.Hydrant;
            if(supply.Locked && supply.Kind!=kind)return new Cue(Step.Busy,Target.None,"OTHER SOURCE CONNECTED\nTake the tool before changing source");
            if(!supply.Locked)
            {
                if(!creek && run.StationTier==WaterStationTier.LeverWell && emptyOutlet)
                    return emptyReturnRequired
                        ?new Cue(Step.ReturnLever,Target.Lever,"RETURN THE LEVER\nRaise the handle to release a small splash\nDock a tool to collect the water")
                        :new Cue(Step.LowerLever,Target.Lever,"LOWER THE HANDLE 8 cm\nKeep pinching, then raise\nDock a tool to collect the water");
                if(!creek && run.StationTier==WaterStationTier.LeverWell && !held)
                    return new Cue(Step.TryPump,Target.Lever,"TRY THE PUMP\nPinch handle; hold, lower + return\nDock a tool to collect the water");
                if(!held)return new Cue(Step.TakeTool,Target.Pickup,creek?"OPTIONAL CREEK REFILL\nPick up a tool to try it":"1  TAKE YOUR TOOL\nOpen hand, then pinch its grip");
                if(supply.Alignment01>0)return new Cue(Step.HoldStill,Target.Socket,"1  HOLD STILL\nKeep the blue rings together");
                return new Cue(Step.BringTool,Target.Socket,creek?"OPTIONAL CREEK REFILL\nRelease pinch; match the blue rings":"1  RELEASE PINCH TO DOCK\nBring the two blue rings together");
            }
            if(run.Water>=run.Capacity-.001f)
                return new Cue(Step.Full,Target.Grip,"3  FULL - TAKE YOUR TOOL\nOpen hand, then pinch its grip");
            if(!creek)
            {
                if(run.StationTier==WaterStationTier.LeverWell)
                    return supply.LeverReturnRequired
                        ?new Cue(Step.ReturnLever,Target.Lever,"2  RETURN THE LEVER\nRaise it to finish: +12 water")
                        :new Cue(Step.LowerLever,Target.Lever,"2  PINCH THE LEVER\nKeep thumb + index finger together\nLower 8 cm, then raise: +12 water");
                if(run.StationTier==WaterStationTier.PushButton)
                    return supply.ValveOpen
                        ?new Cue(Step.Filling,Target.Button,"2  FILLING\nPress again to stop early")
                        :new Cue(Step.PressButton,Target.Button,"2  PRESS THE BLUE BUTTON\nPinch here once to fill");
                return supply.ValveOpen
                    ?new Cue(Step.Filling,Target.Valve,"2  FILLING\nGrip + turn back 45 degrees to stop")
                    :new Cue(Step.OpenValve,Target.Valve,"2  GRIP + TURN THE WHEEL\nKeep pinching and turn 45 degrees");
            }
            if(!supply.IntakePlaced)
                return carryingIntake
                    ?new Cue(Step.PlaceIntake,Target.IntakeSocket,"2  PUT STRAINER IN WATER\nRelease it on the green ring")
                    :new Cue(Step.PlaceIntake,Target.Intake,"2  PICK UP THE STRAINER\nPinch, move to green ring, release");
            return supply.PumpTravel01>=.8f
                ?new Cue(Step.ReturnPump,Target.Pump,"2  PUSH BACK TO FINISH\nReturn the handle for one refill")
                :new Cue(Step.PullPump,Target.Pump,"2  PULL THE BLUE HANDLE\nPinch, pull 8 cm, then push back");
        }
    }
}

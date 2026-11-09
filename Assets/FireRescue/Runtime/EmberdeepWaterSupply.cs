using System;
using UnityEngine;

namespace FireRescue
{
    public enum WaterSourceKind { None, Hydrant, Creek }
    public enum WaterStationTier { LeverWell, RotaryHydrant, PushButton }
    public enum HydrantRefillState { Idle, Aligning, DockedClosed, Filling, DockedFull, Disconnecting }
    public enum CreekRefillState { Idle, IntakePlaced, Primed, Pumping, Full, Interrupted }

    // GD-WATER-01..09. Metres and seconds here are real-world units, not miniature units.
    // The scene adapter supplies validated poses and fresh gesture edges; this object owns
    // connection state, complete strokes and every accepted resource transfer.
    public sealed class EmberdeepWaterSupply
    {
        public const float CaptureMetres=.08f, AlignmentDegrees=35, DockSeconds=.25f;
        public const float ValveDegrees=45, HydrantUnitsPerSecond=30;
        public const float PumpOutMetres=.08f, PumpReturnMetres=.02f, PumpUnits=12;
        readonly FireRun run;
        float alignedSeconds, valveTravel;
        int aligningHand=-1;
        WaterSourceKind aligningKind;
        FireEquipmentId aligningTool;
        bool intakePlaced, pumpStarted, pumpExtended;
        readonly EmberdeepLeverStroke leverStroke=new();
        bool buttonArmed;
        public WaterSourceKind Kind { get; private set; }
        public HydrantRefillState HydrantState { get; private set; }
        public CreekRefillState CreekState { get; private set; }
        public FireEquipmentId Tool { get; private set; }
        public int OwnerHand { get; private set; }=-1;
        public bool Locked=>Kind!=WaterSourceKind.None;
        public bool ValveOpen=>HydrantState==HydrantRefillState.Filling;
        public bool IntakePlaced=>intakePlaced;
        public bool Assisted { get; private set; }
        public float Alignment01=>Mathf.Clamp01(alignedSeconds/DockSeconds);
        public float PumpTravel01 { get; private set; }
        public float LeverTravel01=>leverStroke.Travel01;
        public bool LeverReturnRequired=>leverStroke.ReturnRequired;
        public string Status { get; private set; }="Dock the refill port in the cyan socket";
        public event Action DockConfirmed;
        public event Action<float> WaterTransferred;
        public event Action<float> PumpStroke;
        public event Action Disconnected;

        internal EmberdeepWaterSupply(FireRun run){this.run=run;}

        public bool SampleDock(WaterSourceKind kind,int hand,float portDistanceMetres,float opposingAngleDegrees,bool valid,float dt)
        {
            if(Locked){if(hand!=OwnerHand || kind!=Kind)Status="Source busy - disconnect the docked tool first";return false;}
            if(!valid || hand<0 || hand>1 || (kind!=WaterSourceKind.Hydrant && kind!=WaterSourceKind.Creek)
                || !Finite(portDistanceMetres) || portDistanceMetres<0 || !Finite(opposingAngleDegrees)
                || opposingAngleDegrees<0 || !Finite(dt) || dt<=0 || !run.Owns(run.Equipped) || run.Phase==FirePhase.Lost)
            {ClearAlignment();return false;}
            if(portDistanceMetres>CaptureMetres)
            {ClearAlignment();Status="Move the refill port into the cyan socket";return false;}
            if(opposingAngleDegrees>AlignmentDegrees)
            {ClearAlignment();Status="Align the port arrow with the socket arrow";return false;}
            if(aligningHand!=hand || aligningKind!=kind || aligningTool!=run.Equipped)
            {alignedSeconds=0;aligningHand=hand;aligningKind=kind;aligningTool=run.Equipped;}
            if(kind==WaterSourceKind.Hydrant)HydrantState=HydrantRefillState.Aligning;
            Status="Hold aligned to connect";
            alignedSeconds+=Mathf.Min(dt,.1f);
            if(alignedSeconds+0.00001f<DockSeconds)return false;
            Kind=kind;Tool=run.Equipped;OwnerHand=hand;valveTravel=0;
            run.CancelSupplyInput();ClearStroke();ClearLever();buttonArmed=false;
            HydrantState=kind==WaterSourceKind.Hydrant?(Full?HydrantRefillState.DockedFull:HydrantRefillState.DockedClosed):HydrantRefillState.Idle;
            CreekState=CreekRefillState.Idle;intakePlaced=false;
            Status=Full?"Tank full - pinch the grip to disconnect":kind==WaterSourceKind.Hydrant?StationInstruction:"Connected - place the intake in the marked water socket";
            DockConfirmed?.Invoke();return true;
        }

        string StationInstruction=>run.StationTier==WaterStationTier.LeverWell?"Connected - pull lever down 8 cm, then return":run.StationTier==WaterStationTier.RotaryHydrant?"Connected - rotate the valve 45 degrees":"Connected - press the button to fill";
        // Explicit rotary detent API retained for desktop/tests. The tracked
        // adapter only invokes it through signed TurnValve displacement.
        public bool SetValveOpen(bool open)
        {
            if(Kind!=WaterSourceKind.Hydrant || run.StationTier!=WaterStationTier.RotaryHydrant)return false;
            return SetFlow(open);
        }
        bool SetFlow(bool open)
        {
            valveTravel=0;
            if(Full){HydrantState=HydrantRefillState.DockedFull;Status="Tank full - pinch the grip to disconnect";return !open;}
            HydrantState=open?HydrantRefillState.Filling:HydrantRefillState.DockedClosed;
            Status=run.StationTier==WaterStationTier.PushButton?
                (open?"Filling - press again to stop":"Flow stopped - press the button to fill"):
                (open?"Filling - turn back or disconnect to stop":"Valve closed - turn to fill");
            return true;
        }
        public bool TurnValve(float deltaDegrees)
        {
            if(Kind!=WaterSourceKind.Hydrant || run.StationTier!=WaterStationTier.RotaryHydrant || !Finite(deltaDegrees) || Mathf.Abs(deltaDegrees)<.0001f)return false;
            // Accumulate signed displacement, rather than absolute jitter or wrist speed.
            valveTravel=Mathf.Clamp(valveTravel+deltaDegrees,-ValveDegrees,ValveDegrees);
            if(!ValveOpen && valveTravel>=ValveDegrees-.0001f)return SetValveOpen(true);
            if(ValveOpen && valveTravel<=-ValveDegrees+.0001f)return SetValveOpen(false);
            return false;
        }
        public void ReleaseValve(){valveTravel=0;}

        public bool SampleButton(bool pressed,bool trackingValid)
        {
            if(!trackingValid){Interrupt("Tracking lost - button fill stopped");return false;}
            if(Kind!=WaterSourceKind.Hydrant || run.StationTier!=WaterStationTier.PushButton || run.Phase==FirePhase.Lost)return false;
            if(!pressed){buttonArmed=true;return false;}
            if(!buttonArmed)return false;
            buttonArmed=false;
            if(Full)return false;
            return SetFlow(!ValveOpen);
        }

        public float SampleLever(float travelMetres,bool held,bool trackingValid)
        {
            if(!trackingValid){Interrupt("Tracking lost - unfinished lever stroke cancelled");return 0;}
            if(Kind!=WaterSourceKind.Hydrant || run.StationTier!=WaterStationTier.LeverWell)return 0;
            if(run.Equipped!=Tool || run.Phase==FirePhase.Lost){Interrupt("Tool or sector unavailable");return 0;}
            if(!EmberdeepLeverStroke.ValidTravel(travelMetres))
            {ClearLever();Status="Lever position invalid - return to the upper rest";return 0;}
            if(Full){ClearLever();HydrantState=HydrantRefillState.DockedFull;Status="Tank full - pinch the grip to disconnect";return 0;}
            if(!held){ClearLever();Status=StationInstruction;return 0;}
            if(!leverStroke.Sample(travelMetres,true,true))
            {Status=leverStroke.ReturnRequired?"Return the lever upwards to transfer water":"Lower the lever, then return it upwards";return 0;}
            float transferred=Transfer(PumpUnits);
            if(Full)HydrantState=HydrantRefillState.DockedFull;
            Status=Full?"Tank full - pinch the grip to disconnect":"Stroke accepted - lower and return the lever again";
            if(transferred>0)PumpStroke?.Invoke(transferred);
            return transferred;
        }

        public float Tick(float dt,bool trackingValid)
        {
            if(!trackingValid){Interrupt("Tracking lost - reconnect to resume");return 0;}
            if(!Locked || !Finite(dt) || dt<=0)return 0;
            if(run.Equipped!=Tool || run.Phase==FirePhase.Lost){Interrupt("Tool or sector unavailable");return 0;}
            if(run.StationTier==WaterStationTier.LeverWell || !ValveOpen)return 0;
            float transferred=Transfer(HydrantUnitsPerSecond*Mathf.Min(dt,.1f));
            if(Full){HydrantState=HydrantRefillState.DockedFull;valveTravel=0;Status="Tank full - pinch the grip to disconnect";}
            return transferred;
        }

        public bool PlaceIntake(bool valid)
        {
            if(Kind!=WaterSourceKind.Creek)return false;
            if(!valid)
            {
                intakePlaced=false;ClearStroke();CreekState=CreekRefillState.Interrupted;
                Status="Intake removed - reconnect it before pumping";return false;
            }
            if(intakePlaced)return true;
            intakePlaced=true;ClearStroke();CreekState=CreekRefillState.IntakePlaced;
            // The tool is already exclusively owned and connected at this point.
            CreekState=Full?CreekRefillState.Full:CreekRefillState.Primed;
            Status=Full?"Tank full - pinch the grip to disconnect":"Primed - pull 8 cm, then return the pump";
            return true;
        }
        public float SamplePump(float travelMetres,bool held,bool trackingValid)
        {
            if(!trackingValid){Interrupt("Tracking lost - unfinished stroke cancelled");return 0;}
            if(Kind!=WaterSourceKind.Creek || !intakePlaced)return 0;
            if(!Finite(travelMetres) || travelMetres<-.02f || travelMetres>.3f)
            {ClearStroke();CreekState=CreekRefillState.Interrupted;Status="Pump position invalid - release and start at the detent";return 0;}
            if(Full){ClearStroke();CreekState=CreekRefillState.Full;Status="Tank full - pinch the grip to disconnect";return 0;}
            if(!held){ClearStroke();CreekState=CreekRefillState.Primed;Status="Primed - pull 8 cm, then return the pump";return 0;}
            PumpTravel01=Mathf.Clamp01(travelMetres/PumpOutMetres);
            if(!pumpStarted)
            {
                if(travelMetres>PumpReturnMetres){Status="Start the pump at its home detent";return 0;}
                pumpStarted=true;CreekState=CreekRefillState.Pumping;return 0;
            }
            if(travelMetres>=PumpOutMetres){pumpExtended=true;Status="Return the handle to transfer water";return 0;}
            if(!pumpExtended || travelMetres>PumpReturnMetres)return 0;
            pumpExtended=false; // A second callback at the return detent cannot pay twice.
            float transferred=Transfer(PumpUnits);
            Status=Full?"Tank full - pinch the grip to disconnect":"Stroke accepted - pull and return again";
            CreekState=Full?CreekRefillState.Full:CreekRefillState.Pumping;
            if(transferred>0)PumpStroke?.Invoke(transferred);
            return transferred;
        }
        public bool BeginAssistedRefill()
        {
            if(!Locked || Assisted)return false;
            Assisted=true;run.AssistedRefills++;return true;
        }
        public void Disconnect()
        {
            bool hadLock=Locked;
            if(Kind==WaterSourceKind.Hydrant)HydrantState=HydrantRefillState.Disconnecting;
            ReleaseOwnership();HydrantState=HydrantRefillState.Idle;CreekState=CreekRefillState.Idle;
            Status="Disconnected - open your hand before firing";
            if(hadLock){run.CancelSupplyInput();Disconnected?.Invoke();}
        }
        public void Interrupt(string reason="Refill interrupted")
        {
            bool hadLock=Locked, wasCreek=Kind==WaterSourceKind.Creek;
            ReleaseOwnership();HydrantState=HydrantRefillState.Idle;
            CreekState=wasCreek?CreekRefillState.Interrupted:CreekRefillState.Idle;
            Status=reason;
            if(hadLock){run.CancelSupplyInput();Disconnected?.Invoke();}
        }
        bool Full=>run.WaterFor(Tool)>=run.CapacityFor(Tool)-.00001f;
        float Transfer(float units)
        {
            float actual=run.TransferWater(this,units);
            if(actual>0)WaterTransferred?.Invoke(actual);
            return actual;
        }
        void ClearStroke(){pumpStarted=pumpExtended=false;PumpTravel01=0;}
        void ClearLever()=>leverStroke.Reset();
        void ClearAlignment(){alignedSeconds=0;aligningHand=-1;aligningKind=WaterSourceKind.None;if(!Locked)HydrantState=HydrantRefillState.Idle;}
        void ReleaseOwnership(){Kind=WaterSourceKind.None;OwnerHand=-1;valveTravel=0;intakePlaced=Assisted=buttonArmed=false;ClearAlignment();ClearStroke();ClearLever();}
        static bool Finite(float value)=>!float.IsNaN(value) && !float.IsInfinity(value);
    }
}


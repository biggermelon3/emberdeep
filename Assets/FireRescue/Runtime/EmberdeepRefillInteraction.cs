using System;
using CloudGarden.ShadowPuzzle;
using UnityEngine;

namespace FireRescue
{
    // Reuses tracked poses: the existing pinch for the lever and
    // other controls. Empty-outlet feedback never passes through tank transfer.
    public sealed class EmberdeepRefillInteraction
    {
        public enum ActionKind { None, Valve, Intake, Pump, Lever, Button }
        public struct Hand
        {
            public bool Tracked;
            public Pose Palm;
            public float Pinch;
            public Vector3 ContactPoint;
            public bool HasContactPoint;
            public bool UsesGrip;
            public Vector3 Contact=>UsesGrip?GraspContact:HasContactPoint?ContactPoint:Palm.position;
            public Vector3 FingerContact=>HasContactPoint?ContactPoint:Palm.position;
            public float GripStrength;
            public bool GripValid;
            public Vector3 GripContact;
            public bool HasGripContact;
            public Vector3 GraspContact=>HasGripContact?GripContact:Palm.position;
            public Hand(bool tracked,Pose palm,float pinch):this()
            {Tracked=tracked;Palm=palm;Pinch=pinch;ContactPoint=palm.position;HasContactPoint=false;}
            public Hand(bool tracked,Pose palm,float pinch,Vector3 indexTip):this()
            {Tracked=tracked;Palm=palm;Pinch=pinch;ContactPoint=indexTip;HasContactPoint=true;}
        }
        readonly EmberdeepControlGesture[] gestures={new(),new()};
        readonly bool[] buttonArmed=new bool[2];
        Vector3 lastValveRadial;
        readonly EmberdeepLeverStroke outletStroke=new();
        EmberdeepWaterSupply leverSupply;
        int operatingHand=-1;
        ActionKind operation;
        Quaternion valveStart;
        Vector3 pumpStart;
        float lastValveAngle,valveStartDetent;
        bool intakePlaced;
        public ActionKind CurrentAction=>operation;
        public bool OwnsControl=>operation!=ActionKind.None && operatingHand>=0;
        // Spell-mode well: a wider reach, and a hand that is already closed may take the lever.
        public float LeverReach=.10f;
        public bool LeverAcceptsClosedHand;
        public bool EmptyOutlet { get; private set; }
        public bool EmptyLeverReturnRequired=>EmptyOutlet && outletStroke.ReturnRequired;
        public event Action<float> OutletStroke;
        public int OperatingHand=>operatingHand;
        public float ActionAmount { get; private set; }
        public float ValveVisualDegrees { get; private set; }
        public Transform ActionAnchor=>!ActiveView?null:operation switch {
            ActionKind.Valve=>ActiveView.ValveVisual,ActionKind.Intake=>ActiveView.IntakeVisual,
            ActionKind.Pump=>ActiveView.PumpVisual,ActionKind.Lever=>ActiveView.LeverVisual,
            ActionKind.Button=>ActiveView.ButtonVisual,_=>null };
        public bool Detached { get; private set; }
        public int DetachedHand { get; private set; }
        public bool Interrupted { get; private set; }
        public float PumpTravel { get; private set; }
        public bool IntakePlaced=>intakePlaced;
        public bool CarryingIntake=>operation==ActionKind.Intake;
        public string Hint { get; private set; }="Align the cyan refill coupling with the socket";
        public EmberdeepWaterStationView ActiveView { get; private set; }

        public void Reset()
        {
            leverSupply?.SampleLever(0,false,true);leverSupply=null;
            if(ActiveView && ActiveView.IsCreek)ActiveView.RestoreIntake();
            foreach(var g in gestures)g.Reset();
            buttonArmed[0]=buttonArmed[1]=false;
            outletStroke.Reset();EmptyOutlet=false;
            operatingHand=-1;operation=ActionKind.None;ValveVisualDegrees=ActionAmount=PumpTravel=0;intakePlaced=false;
            ActiveView=null;Detached=Interrupted=false;
        }
        public void ConsumeInput(FireRun run)
        {
            leverSupply?.SampleLever(0,false,true);leverSupply=null;
            foreach(var g in gestures)g.Reset();
            buttonArmed[0]=buttonArmed[1]=false;
            outletStroke.Reset();if(EmptyOutlet)ActiveView=null;EmptyOutlet=false;
            if(operation==ActionKind.Intake && ActiveView)ActiveView.RestoreIntake();
            operation=ActionKind.None;operatingHand=-1;ActionAmount=PumpTravel=0;
            run.Supply.ReleaseValve();
            ValveVisualDegrees=run.Supply.ValveOpen?45:0;
            if(run.Supply.Kind==WaterSourceKind.Creek)run.Supply.SamplePump(0,false,true);
            else run.Supply.SampleLever(0,false,true);
        }
        static Hand Validated(Hand h)
        {
            var p=h.Palm.position;var q=h.Palm.rotation;var c=h.Contact;
            h.Tracked=h.Tracked && Finite(p.x) && Finite(p.y) && Finite(p.z) && Finite(q.x) && Finite(q.y) && Finite(q.z) && Finite(q.w)
                && q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w>.00001f && Finite(h.Pinch) && h.Pinch>=0
                && Finite(c.x) && Finite(c.y) && Finite(c.z);
            var grasp=h.GraspContact;
            h.GripValid=h.GripValid && h.Tracked && Finite(h.GripStrength) && h.GripStrength>=0 && h.GripStrength<=1
                && Finite(grasp.x) && Finite(grasp.y) && Finite(grasp.z);
            if(h.UsesGrip && !h.GripValid)h.Tracked=false;
            return h;
        }
        static bool Finite(float n)=>!float.IsNaN(n) && !float.IsInfinity(n);
        public bool Process(FireRun run,FireEquipmentView tool,EmberdeepWaterStationView hydrant,
            EmberdeepWaterStationView creek,bool held,int holdingHand,Hand left,Hand right,float dt)
        {
            Detached=Interrupted=false;
            if(!Finite(dt) || dt<=0)
            {bool connected=run.Supply.Locked;run.Supply.Interrupt("Invalid hand sample time - reconnect deliberately");Reset();Interrupted=connected;return true;}
            left=Validated(left);right=Validated(right);
            var supply=run.Supply;
            for(int i=0;i<2;i++)
            {
                var h=i==0?left:right;
                gestures[i].Sample(h,Mathf.Min(dt,.025f));
            }
            if(EmptyOutlet)
            {
                // An independently accepted connection changes the recipient. Never
                // carry an empty-outlet half-stroke into a newly docked tank.
                if(supply.Locked){Reset();Hint="Tool connected - release, then grasp the lever";return true;}
                return ProcessEmptyLever(run,held,holdingHand,left,right);
            }
            if(!supply.Locked)
            {
                if(ActiveView){Reset();Interrupted=true;return true;}
                if(hydrant && hydrant.gameObject.activeInHierarchy && run.StationTier==WaterStationTier.LeverWell && run.Phase!=FirePhase.Lost)
                    for(int i=0;i<2;i++)
                    {
                        var free=i==0?left:right;
                        bool grasped=gestures[i].Pressed || (LeverAcceptsClosedHand && gestures[i].Closed);
                        if((held && holdingHand==i) || !free.Tracked || !grasped || Vector3.Distance(free.Contact,hydrant.Lever.position)>LeverReach)continue;
                        supply.Interrupt("Empty outlet - dock a tool to collect water");
                        ActiveView=hydrant;EmptyOutlet=true;outletStroke.Reset();
                        BeginLever(run,free,i);
                        return ProcessEmptyLever(run,held,holdingHand,left,right);
                    }
                if(!held || !tool){supply.Interrupt();return false;}
                var h=holdingHand==0?left:right;
                if(!h.Tracked){supply.Interrupt();return false;}
                // A firing/charging pinch never becomes a source connection.
                if(h.UsesGrip?h.GripStrength>=EmberdeepSqueeze.Fire:h.Pinch<.035f){supply.Interrupt("Relax trigger before docking");return false;}
                var view=hydrant;
                if(creek && creek.gameObject.activeSelf && Vector3.Distance(tool.RefillPort.position,creek.Socket.position)<Vector3.Distance(tool.RefillPort.position,hydrant.Socket.position))view=creek;
                float distance=Vector3.Distance(tool.RefillPort.position,view.Socket.position);
                float angle=AlignmentAngle(tool.RefillPort,view.Socket);
                bool accepted=supply.SampleDock(view==hydrant?WaterSourceKind.Hydrant:WaterSourceKind.Creek,holdingHand,distance,angle,true,dt);
                if(accepted)
                {
                    ActiveView=view;foreach(var g in gestures)g.Reset();outletStroke.Reset();
                    intakePlaced=false;Hint="Docked. Open your hand, then operate the cyan control";
                    Snap(tool,view.Socket);
                }
                else if(distance<.12f)Hint=angle>35?"Turn the coupling to match the socket arrow":"Hold aligned until the socket clicks";
                return accepted || distance<.12f;
            }
            if(!ActiveView){ActiveView=supply.Kind==WaterSourceKind.Creek?creek:hydrant;intakePlaced=supply.IntakePlaced;}
            var owner=supply.OwnerHand==0?left:right;
            var operatorHand=operatingHand==0?left:right;
            if(!owner.Tracked || (operatingHand>=0 && !operatorHand.Tracked))
            {
                supply.Interrupt("Hand tracking lost - dock again");Reset();Interrupted=true;return true;
            }
            Snap(tool,ActiveView.Socket);
            // A release sample rearms the button only while no action owns it.
            // On docking the gesture filters themselves require an open hand.
            if(operation==ActionKind.None && run.StationTier==WaterStationTier.PushButton)
                supply.SampleButton(false,true);
            for(int i=0;i<2;i++)
            {
                var hand=i==0?left:right;
                if(!hand.Tracked)continue;
                if(hand.UsesGrip && supply.Kind==WaterSourceKind.Hydrant && run.StationTier==WaterStationTier.PushButton
                    && operation==ActionKind.None)
                {
                    var at=ActiveView.Button.InverseTransformPoint(hand.FingerContact);
                    if(new Vector2(at.x,at.y).magnitude<.055f && at.z<-.008f)buttonArmed[i]=true;
                    if(buttonArmed[i] && PhysicalButtonDown(hand))
                    {operatingHand=i;operation=ActionKind.Button;buttonArmed[i]=false;supply.SampleButton(true,true);ActionAmount=1;}
                }
                if(operation==ActionKind.None && supply.Kind==WaterSourceKind.Hydrant && run.StationTier==WaterStationTier.LeverWell
                    && gestures[i].Pressed && Vector3.Distance(hand.Contact,ActiveView.Lever.position)<=.10f)
                    BeginLever(run,hand,i);
                bool pinchAtLever=supply.Kind==WaterSourceKind.Hydrant && run.StationTier==WaterStationTier.LeverWell
                    && Vector3.Distance(hand.Contact,ActiveView.Lever.position)<=.10f;
                if(operation==ActionKind.None && gestures[i].Pressed && !pinchAtLever)
                {
                    Vector3 p=hand.Contact;
                    float gripDistance=Vector3.Distance(p,tool.Grip.position);
                    if(hand.UsesGrip && tool.TryGrabRegion(p,out _,out var surface))gripDistance=Vector3.Distance(p,tool.transform.TransformPoint(surface));
                    float intakeDistance=Vector3.Distance(p,ActiveView.Intake.position);
                    float pumpDistance=Vector3.Distance(p,ActiveView.Pump.position);
                    Transform stationControl=run.StationTier==WaterStationTier.LeverWell?ActiveView.Lever:
                        run.StationTier==WaterStationTier.PushButton?ActiveView.Button:ActiveView.Valve;
                    float stationDistance=Vector3.Distance(p,stationControl.position);
                    // Broad controls use the pinching fingertip, not the wrist/palm.
                    // Pick the nearest eligible target when their generous volumes overlap.
                    float controlDistance=supply.Kind==WaterSourceKind.Hydrant?stationDistance:
                        Mathf.Min(intakeDistance,intakePlaced?pumpDistance:float.PositiveInfinity);
                    if(gripDistance<.075f && gripDistance<controlDistance)
                    {supply.Disconnect();Reset();Detached=true;DetachedHand=i;Hint="Disconnected. Open hand before firing";return true;}
                    if(supply.Kind==WaterSourceKind.Hydrant && run.StationTier!=WaterStationTier.LeverWell && stationDistance<.11f)
                    {
                        operatingHand=i;ActionAmount=0;
                        if(run.StationTier==WaterStationTier.PushButton && !hand.UsesGrip)
                        {operation=ActionKind.Button;supply.SampleButton(true,true);ActionAmount=1;}
                        else if(run.StationTier==WaterStationTier.RotaryHydrant)
                        {
                            operation=ActionKind.Valve;valveStart=hand.Palm.rotation;lastValveAngle=0;valveStartDetent=supply.ValveOpen?45:0;
                            lastValveRadial=Vector3.ProjectOnPlane(hand.Contact-ActiveView.Valve.position,ActiveView.Valve.forward);
                            if(hand.UsesGrip && lastValveRadial.magnitude<.018f){operation=ActionKind.None;operatingHand=-1;}
                        }
                        else operatingHand=-1;
                    }
                    else if(supply.Kind==WaterSourceKind.Creek && intakeDistance<.10f && (!intakePlaced || intakeDistance<=pumpDistance))
                    {operation=ActionKind.Intake;operatingHand=i;intakePlaced=false;supply.PlaceIntake(false);}
                    else if(supply.Kind==WaterSourceKind.Creek && intakePlaced && pumpDistance<.10f)
                    {operation=ActionKind.Pump;operatingHand=i;pumpStart=hand.Palm.position;PumpTravel=0;}
                }
                if(operatingHand!=i)continue;
                Transform control=operation switch {
                    ActionKind.Valve=>ActiveView.Valve,ActionKind.Lever=>ActiveView.Lever,
                    ActionKind.Button=>ActiveView.Button,ActionKind.Pump=>ActiveView.Pump,_=>null };
                if(control && Vector3.Distance(hand.Contact,control.position)>.28f)
                {
                    // Leaving a direct control gives up its grip, without a remote
                    // wrist turn/stroke. Accepted flow remains until stopped/full.
                    supply.ReleaseValve();
                    if(operation==ActionKind.Lever){supply.SampleLever(0,false,true);leverSupply=null;}
                    else if(operation==ActionKind.Pump)supply.SamplePump(0,false,true);
                    else if(operation==ActionKind.Button)supply.SampleButton(false,true);
                    gestures[i].Reset();operation=ActionKind.None;operatingHand=-1;ActionAmount=PumpTravel=0;
                    continue;
                }
                bool closed=hand.UsesGrip && operation==ActionKind.Button?PhysicalButtonDown(hand):gestures[i].Closed;
                if(operation==ActionKind.Valve)
                {
                    // Rotation around the fixture's axis; yaw/translation cannot turn the valve.
                    Vector3 from=Vector3.ProjectOnPlane(valveStart*Vector3.up,ActiveView.Valve.forward);
                    Vector3 to=Vector3.ProjectOnPlane(hand.Palm.rotation*Vector3.up,ActiveView.Valve.forward);
                    float angle=Vector3.SignedAngle(from,to,ActiveView.Valve.forward);
                    if(hand.UsesGrip)
                    {
                        var radial=Vector3.ProjectOnPlane(hand.Contact-ActiveView.Valve.position,ActiveView.Valve.forward);
                        float delta=Vector3.SignedAngle(lastValveRadial,radial,ActiveView.Valve.forward);
                        if(radial.magnitude<.018f || Mathf.Abs(delta)>60)closed=false;
                        angle=lastValveAngle+(closed?delta:0);lastValveRadial=radial;
                    }
                    if(closed && Mathf.Abs(angle-lastValveAngle)>.05f)
                    {supply.TurnValve(angle-lastValveAngle);lastValveAngle=angle;}
                    ActionAmount=angle;
                    ValveVisualDegrees=Mathf.Clamp(valveStartDetent+angle,0,45);
                    if(!closed)supply.ReleaseValve();
                }
                else if(operation==ActionKind.Intake)
                {
                    ActiveView.IntakeVisual.position=hand.Contact;
                    if(!closed)
                    {intakePlaced=Vector3.Distance(hand.Contact,ActiveView.IntakeSocket.position)<.08f;supply.PlaceIntake(intakePlaced);if(!intakePlaced)ActiveView.RestoreIntake();}
                }
                else if(operation==ActionKind.Pump)
                {
                    PumpTravel=Mathf.Clamp(Vector3.Dot(hand.Palm.position-pumpStart,-ActiveView.Pump.forward),0,.12f);
                    supply.SamplePump(PumpTravel,closed,true);
                    ActionAmount=PumpTravel;
                }
                else if(operation==ActionKind.Lever)
                {
                    float travel=Vector3.Dot(pumpStart-hand.Palm.position,ActiveView.transform.up);
                    ActionAmount=Mathf.Clamp(travel,0,EmberdeepLeverStroke.MaximumTravel);
                    supply.SampleLever(travel,closed,true);
                }
                else if(operation==ActionKind.Button)
                {ActionAmount=closed?1:0;if(!closed)supply.SampleButton(false,true);}
                if(!closed){operation=ActionKind.None;operatingHand=-1;ActionAmount=PumpTravel=0;leverSupply=null;}
            }
            supply.Tick(dt,true);
            if(operation!=ActionKind.Valve)ValveVisualDegrees=supply.ValveOpen?45:0;
            Hint=supply.Status;
            return true;
        }
        bool PhysicalButtonDown(Hand hand)
        {
            var at=ActiveView.Button.InverseTransformPoint(hand.FingerContact);
            return hand.GripValid && new Vector2(at.x,at.y).magnitude<.045f && at.z>=.006f && at.z<.065f;
        }
        void BeginLever(FireRun run,Hand hand,int index)
        {
            operation=ActionKind.Lever;operatingHand=index;pumpStart=hand.Palm.position;ActionAmount=0;
            // Keep this pinch held; operation ownership prevents the same pinch detaching a tool.
            leverSupply=EmptyOutlet?null:run.Supply;
            if(EmptyOutlet)outletStroke.Sample(0,true,true);else run.Supply.SampleLever(0,true,true);
        }
        bool ProcessEmptyLever(FireRun run,bool held,int holdingHand,Hand left,Hand right)
        {
            var hand=operatingHand==0?left:right;
            if(!ActiveView || !ActiveView.gameObject.activeInHierarchy || run.StationTier!=WaterStationTier.LeverWell || run.Phase==FirePhase.Lost
                || !hand.Tracked || (held && holdingHand==operatingHand) || Vector3.Distance(hand.Contact,ActiveView.Lever.position)>.28f)
            {Reset();Hint="Lever released - release, then grasp again";return true;}
            if(!gestures[operatingHand].Closed)
            {Reset();Hint="Dock a tool to collect water, or grasp the lever to try it";return true;}
            float travel=Vector3.Dot(pumpStart-hand.Palm.position,ActiveView.transform.up);
            ActionAmount=Mathf.Clamp(travel,0,EmberdeepLeverStroke.MaximumTravel);
            if(outletStroke.Sample(travel,true,true))OutletStroke?.Invoke(outletStroke.WaterUnits);
            Hint=outletStroke.ReturnRequired?"Raise the lever to splash water from the empty outlet":"Lower and return the lever - dock a tool to collect water";
            return true;
        }
        // The coupling is circular: rolling a wrist does not misalign its mating axis.
        public static float AlignmentAngle(Transform port,Transform socket)=>Vector3.Angle(port.forward,-socket.forward);

        public static bool NearStation(Vector3 palm,EmberdeepWaterStationView station)=>station && station.gameObject.activeInHierarchy
            && Vector3.Distance(palm,station.Socket.position)<.32f;

        // Guide only an open hand near the grip's intended dock position, not a
        // large sphere around the fixture. The real port/dwell still owns docking.
        public static bool TryGuideHeldTool(FireEquipmentView tool,Pose palm,EmberdeepWaterStationView hydrant,EmberdeepWaterStationView creek,float pinch=.04f)
        {
            if(!tool || !Finite(pinch) || pinch<.035f || !Finite(palm.position.x) || !Finite(palm.position.y) || !Finite(palm.position.z))return false;
            EmberdeepWaterStationView station=null;float nearest=.16f;
            for(int source=0;source<2;source++)
            {
                var candidate=source==0?hydrant:creek;
                if(!candidate || !candidate.gameObject.activeInHierarchy)continue;
                var aligned=candidate.Socket.rotation*Quaternion.Euler(0,180,0)*Quaternion.Inverse(tool.RefillPort.localRotation);
                var dockingGrip=candidate.Socket.position+aligned*(tool.Grip.localPosition-tool.RefillPort.localPosition);
                float distance=Vector3.Distance(palm.position,dockingGrip);
                if(distance>nearest)continue;
                station=candidate;nearest=distance;
            }
            if(!station)return false;
            var rotation=station.Socket.rotation*Quaternion.Euler(0,180,0)*Quaternion.Inverse(tool.RefillPort.localRotation);
            tool.transform.SetPositionAndRotation(palm.position-rotation*tool.Grip.localPosition,rotation);
            return true;
        }
        public static void Snap(FireEquipmentView tool,Transform socket)
        {
            Quaternion rotation=socket.rotation*Quaternion.Euler(0,180,0)*Quaternion.Inverse(tool.RefillPort.localRotation);
            tool.transform.SetPositionAndRotation(socket.position-rotation*tool.RefillPort.localPosition,rotation);
        }
    }
}

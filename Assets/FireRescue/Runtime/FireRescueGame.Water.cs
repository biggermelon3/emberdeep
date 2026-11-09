using CloudGarden.ShadowPuzzle;
using PhysicalAssembly;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace FireRescue
{
    public sealed partial class FireRescueGame
    {
        EmberdeepHandRayMenu handMenu;
        readonly EmberdeepRefillInteraction refill=new();
        readonly EmberdeepMenuPose firegroundPose=new();
        readonly System.Collections.Generic.List<XRInputSubsystem> fireOrigins=new();
        bool acquiringPose;
        bool hudPlaced;
        EmberdeepWaterStationView hydrantStation,creekStation;
        bool interfaceHovered;
        readonly EmberdeepRefillInteraction.Hand[] interfacePhysical=new EmberdeepRefillInteraction.Hand[2];
        readonly bool[] interfacePhysicalBlocked=new bool[2];
        bool InterfaceBlocksPhysical=>interfacePhysicalBlocked[0] || interfacePhysicalBlocked[1];
        float supplyStrokeFlashUntil;
        WaterSourceKind desktopDock;
        readonly EmberdeepLeverStroke desktopOutletStroke=new();
        float desktopOutletTravel;
        public EmberdeepWaterStationView HydrantStation=>hydrantStation;
        public EmberdeepWaterStationView CreekStation=>creekStation;
        public FireEquipmentView HeldTool=>heldView;
        public EmberdeepRefillInteraction RefillInteraction=>refill;

        void BuildWaterStations()
        {
            hydrantStation=new GameObject("WS-01 Hydrant blockout").AddComponent<EmberdeepWaterStationView>();
            hydrantStation.transform.SetParent(transform,false);hydrantStation.Build(FoliageMaterial,PanelMaterial,WaterMaterial,false);
            hydrantStation.SetTier(Run.StationTier);
            creekStation=new GameObject("WS-02 Creek refill blockout").AddComponent<EmberdeepWaterStationView>();
            creekStation.transform.SetParent(transform,false);creekStation.Build(FoliageMaterial,PanelMaterial,WaterMaterial,true);
            Run.Supply.DockConfirmed+=OnSupplyDock;
            Run.Supply.PumpStroke+=OnSupplyStroke;
            Run.Supply.Disconnected+=OnSupplyDisconnect;
            refill.OutletStroke+=OnEmptyOutletStroke;
            PositionWaterStations();
            ConfigureHandOcclusion();
        }
        bool StableFiregroundPose()
        {
            var view=Player.ViewCamera.transform;
            var head=InputDevices.GetDeviceAtXRNode(XRNode.Head);
            bool tr=head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked,out bool tracked);
            bool sr=head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trackingState,out InputTrackingState state);
            bool pr=head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition,out Vector3 raw);
            SubsystemManager.GetSubsystems(fireOrigins);var origin=TrackingOriginModeFlags.Unknown;
            foreach(var item in fireOrigins)if(item.running){origin=item.GetTrackingOriginMode();break;}
            var pose=new Pose(view.position,view.rotation);
            bool valid=head.isValid && EmberdeepHeadPoseGate.Allows(pose,Player.FloorHeight,tr,tracked,sr,state,origin,pr,raw);
            return firegroundPose.TryUpdate(focused && !paused && hands.HeadReady && valid,pose,Player.FloorHeight,Time.unscaledDeltaTime,out _);
        }
        System.Collections.IEnumerator PlaceStableFireground()
        {
            acquiringPose=true;firegroundPose.RequestRecenter();SetWorldVisible(false);
            while(!StableFiregroundPose())yield return null;
            ApplyWorldPose();SetWorldVisible(true);acquiringPose=false;
        }
        void SetWorldVisible(bool visible)
        {
            Arena.gameObject.SetActive(visible);hud.gameObject.SetActive(visible);rack.gameObject.SetActive(visible);
            hydrantStation.gameObject.SetActive(visible);creekStation.gameObject.SetActive(visible && Run.Stage>0);
        }
        void PlaceHud()
        {
            if(!Player || !hud || !hands || !focused || paused || !hands.HeadReady)return;
            var view=Player.ViewCamera.transform;
            var previousForward=hudPlaced?hud.forward:(Arena?Arena.forward:Vector3.zero);
            if(!EmberdeepHudPose.TryCalculate(new Pose(view.position,view.rotation),Player.FloorHeight,previousForward,out var pose))return;
            hud.SetPositionAndRotation(pose.position,pose.rotation);hudPlaced=true;
            // A relocated page cannot reuse a target or pinch from its old pose.
            handMenu?.ResetInput();interfaceHovered=false;Physics.SyncTransforms();
            ClearInterfaceReservation();
        }
        void OnSupplyDock(){CancelWeaponInput();Feedback.Impact(false);}
        void OnSupplyStroke(float units){supplyStrokeFlashUntil=Time.unscaledTime+.25f;Feedback.Impact(false);}
        void OnSupplyDisconnect(){CancelWeaponInput();Feedback.Impact(false);}
        void OnEmptyOutletStroke(float units){hydrantStation.EmitOutletSplash();Feedback.Impact(false);}
        void PositionWaterStations()
        {
            if(!hydrantStation || !Arena)return;
            // Reachable near edge, at chest/waist height; the forest itself never expands.
            float side=LeftHanded?1:-1;
            hydrantStation.transform.SetPositionAndRotation(Arena.TransformPoint(new Vector3(side*3.3f,.2f,.4f)),Arena.rotation);
            creekStation.transform.SetPositionAndRotation(Arena.TransformPoint(new Vector3(-side*4f,.2f,3.5f)),Arena.rotation);
            hydrantStation.SetLeftHanded(LeftHanded);creekStation.SetLeftHanded(!LeftHanded);
        }
        void StopRefill()
        {
            ClearInterfaceReservation();
            Run.Supply.Interrupt("Refill stopped - reconnect deliberately");refill.Reset();desktopDock=WaterSourceKind.None;
            desktopOutletStroke.Reset();desktopOutletTravel=0;
            if(hydrantStation)hydrantStation.StopOutlet();
        }
        void CancelWeaponInput()
        {
            Spraying=false;desktopReady=false;gesture.Reset();pump.Reset();Run.CancelTrigger();
            grip.ConsumePickup();
            if(aimMarker)aimMarker.gameObject.SetActive(false);if(coverage)coverage.enabled=false;
        }
        public void RetrySector()
        {
            ReturnEquipment();
            if(Run.RetryStage()){SelectedEquipment=Run.Equipped;BuildIncident();RefreshEquipment();if(!Player.IsXR)TakeEquipment();}
        }
        public bool BuyStationUpgrade()
        {
            if(!Run.BuyStationUpgrade())return false;
            refill.Reset();CancelWeaponInput();hydrantStation.SetTier(Run.StationTier);
            RefreshWaterStations();RefreshShop();Feedback.Impact(false);return true;
        }
        bool ProcessInterfaceInput()
        {
            EnsureHandMenu();
            if(!Player.IsXR)ClearInterfaceReservation();
            bool accepted=handMenu.Process(hands,Player.ViewCamera,Player.IsXR,buttons,true,Time.unscaledDeltaTime,
                AllowInterfaceButton,null,AllowPointedInterface);
            return AcceptInterfaceInput(accepted);
        }
        public EmberdeepHandRayMenu MenuInput=>handMenu;
        void EnsureHandMenu()
        {
            if(!handMenu){handMenu=gameObject.AddComponent<EmberdeepHandRayMenu>();handMenu.Initialize(transform,PanelMaterial);handMenu.MaximumHeadAngle=25f;}
        }
        bool AllowPointedInterface(int hand,EmberdeepHandRay.Sample ray)
        {
            bool tracked=hands.ReadHand(hand==0,out var palm,out var tip,out var pinch);
            interfacePhysical[hand]=new EmberdeepRefillInteraction.Hand(tracked,palm,pinch,tip);
            return AllowHandInterface(hand,interfacePhysical[hand],ray);
        }
        bool AllowHandInterface(int hand,EmberdeepRefillInteraction.Hand sample,EmberdeepHandRay.Sample ray)
        {
            if(!ValidInterfaceHand(sample) || (refill.OwnsControl && refill.OperatingHand==hand))return false;
            // A real eligible ray + head attention beats proximity alone. Existing
            // physical ownership remains exclusive; no target means normal rules.
            if(handMenu.HasIntent(hand,ray,buttons,AllowInterfaceButton))return true;
            return AllowHandInterface(hand,sample);
        }
        bool AllowHandInterface(int hand,EmberdeepRefillInteraction.Hand sample)
        {
            if(ShopOpen)return true;
            if(refill.OwnsControl && refill.OperatingHand==hand)return false;
            if(!sample.Tracked)return false;
            var palm=sample.Palm;var tip=sample.Contact;
            if(NearWellLever(tip))return false;
            // Direct work owns the hand before the UI gets a chance to consume
            // its pinch. The other hand remains available for distant UI.
            if((grip.Held || Run.Supply.Locked) &&
                (EmberdeepRefillInteraction.NearStation(palm.position,hydrantStation) ||
                 EmberdeepRefillInteraction.NearStation(palm.position,creekStation)))return false;
            return grip.Held || !rackView || Vector3.Distance(tip,rackView.Grip.position)>.12f;
        }
        // Synthetic provider boundary: the same physical-hand + actual ray policy
        // as production, without bypassing it for near-source regression tests.
        public bool ProcessHandInterfaceSamples(EmberdeepRefillInteraction.Hand leftHand,EmberdeepRefillInteraction.Hand rightHand,
            EmberdeepHandRay.Sample left,EmberdeepHandRay.Sample right,bool available,float dt)
        {
            EnsureHandMenu();interfacePhysical[0]=leftHand;interfacePhysical[1]=rightHand;
            if(!AllowHandInterface(0,leftHand,left))left=default;
            if(!AllowHandInterface(1,rightHand,right))right=default;
            return ProcessHandMenuCore(left,right,available,dt);
        }
        public bool ProcessHandMenuInput(EmberdeepHandRay.Sample left,EmberdeepHandRay.Sample right,bool available,float dt)
        {
            EnsureHandMenu();
            interfacePhysical[0]=new EmberdeepRefillInteraction.Hand(EmberdeepHandRay.Valid(left),new Pose(left.Ray.origin,Quaternion.identity),left.Pinch);
            interfacePhysical[1]=new EmberdeepRefillInteraction.Hand(EmberdeepHandRay.Valid(right),new Pose(right.Ray.origin,Quaternion.identity),right.Pinch);
            return ProcessHandMenuCore(left,right,available,dt);
        }
        bool ProcessHandMenuCore(EmberdeepHandRay.Sample left,EmberdeepHandRay.Sample right,bool available,float dt)
        {
            available=available && focused && !paused && !returningToCamp && !acquiringPose;
            if(!available){CancelWeaponInput();ClearInterfaceReservation();}
            if(refill.OwnsControl){if(refill.OperatingHand==0)left=default;else right=default;}
            return AcceptInterfaceInput(handMenu.ProcessSamples(left,right,buttons,available,dt,AllowInterfaceButton));
        }
        bool AcceptInterfaceInput(bool accepted)
        {
            interfaceHovered=handMenu.OwnsInput;
            for(int hand=0;hand<2;hand++)
            {
                var physical=interfacePhysical[hand];
                if(!ValidInterfaceHand(physical))interfacePhysicalBlocked[hand]=false;
                else if(handMenu.HasTarget(hand))interfacePhysicalBlocked[hand]=true;
                else if(physical.Pinch>=.035f)interfacePhysicalBlocked[hand]=false;
            }
            if(interfaceHovered)CancelWeaponInput();
            if(InterfaceBlocksPhysical && !refill.OwnsControl)refill.ConsumeInput(Run);
            if(!accepted)return false;
            CancelWeaponInput();
            refill.ConsumeInput(Run);
            hydrantStation.StopOutlet();
            var pressed=handMenu.PressedButton;handMenu.ResetInput();pressed.Press();return true;
        }
        void ClearInterfaceReservation()
        {
            for(int hand=0;hand<2;hand++){interfacePhysicalBlocked[hand]=false;interfacePhysical[hand]=default;}
        }
        static bool ValidInterfaceHand(EmberdeepRefillInteraction.Hand hand)
        {
            bool Finite(float n)=>!float.IsNaN(n) && !float.IsInfinity(n);
            bool Point(Vector3 p)=>Finite(p.x) && Finite(p.y) && Finite(p.z);
            var q=hand.Palm.rotation;float norm=q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w;
            return hand.Tracked && Point(hand.Palm.position) && Point(hand.Contact) && Finite(norm) && norm>.00001f
                && Finite(hand.Pinch) && hand.Pinch>=0;
        }
        bool BlockRefillForInterface(float dt)
        {
            CancelWeaponInput();refill.ConsumeInput(Run);
            if(Run.Supply.Locked)Run.Supply.Tick(dt,true);
            return true;
        }
        // A fresh shop click in the safe phase deliberately returns the tool and
        // cancels supply through OpenShop. Docking must not hide that exit.
        bool AllowInterfaceButton(WorkshopButton button)=>!Run.Supply.Locked || button.Command=="Next incident"
            || (Run.InSupply && button.Command=="Gear shop");
        bool ProcessRefillInput(Pose palm,bool valid,float dt)
        {
            if(!Player.IsXR)return interfaceHovered?BlockRefillForInterface(dt):DesktopRefill(dt);
            bool l=hands.ReadHand(true,out var lp,out var lt,out var ld);
            bool r=hands.ReadHand(false,out var rp,out var rt,out var rd);
            if(InterfaceBlocksPhysical)return ProcessRefillHands(new EmberdeepRefillInteraction.Hand(l,lp,ld,lt),new EmberdeepRefillInteraction.Hand(r,rp,rd,rt),dt);
            if(grip.Held && !Run.Supply.Locked && !refill.OwnsControl && valid)
                EmberdeepRefillInteraction.TryGuideHeldTool(heldView,new Pose(GripPoint(LeftHanded,palm),palm.rotation),hydrantStation,creekStation,LeftHanded?ld:rd);
            return ProcessRefillHands(new EmberdeepRefillInteraction.Hand(l,lp,ld,lt),new EmberdeepRefillInteraction.Hand(r,rp,rd,rt),dt);
        }
        bool NearWellLever(Vector3 contact)
        {
            if(!hydrantStation || !hydrantStation.gameObject.activeInHierarchy || Run.StationTier!=WaterStationTier.LeverWell)return false;
            return Vector3.Distance(contact,hydrantStation.LeverVisual.position)<.13f;
        }
        // Production path exposed for synthetic joint regressions; never invents a transfer.
        public bool ProcessRefillHands(EmberdeepRefillInteraction.Hand left,EmberdeepRefillInteraction.Hand right,float dt)
        {
            if(!focused || paused || !hands.HeadReady){ResetInput();return true;}
            bool ownerTracked=!Run.Supply.Locked || ValidInterfaceHand(Run.Supply.OwnerHand==0?left:right);
            bool validTime=!float.IsNaN(dt) && !float.IsInfinity(dt) && dt>0;
            if(InterfaceBlocksPhysical && !refill.OwnsControl && ownerTracked && validTime)return BlockRefillForInterface(dt);
            // Keep actual tracking (including the dock owner) while suppressing
            // this UI hand's physical pinch until it has really reopened.
            if(interfacePhysicalBlocked[0] && ValidInterfaceHand(left))left.Pinch=.04f;
            if(interfacePhysicalBlocked[1] && ValidInterfaceHand(right))right.Pinch=.04f;
            bool hadDock=Run.Supply.Locked;
            bool wasEmpty=refill.EmptyOutlet;int previousOperator=refill.OperatingHand;
            bool consumed=refill.Process(Run,heldView,hydrantStation,creekStation,grip.Held,grip.Hand,left,right,dt);
            if(refill.Detached){grip.EquipForDesktop(refill.DetachedHand);LeftHanded=refill.DetachedHand==0;CancelWeaponInput();}
            if(refill.Interrupted){hydrantStation.StopOutlet();if(hadDock)ReturnEquipment();}
            else if(wasEmpty && (Run.Phase==FirePhase.Lost || !(previousOperator==0?left:right).Tracked || !refill.EmptyOutlet))hydrantStation.StopOutlet();
            return consumed;
        }
        bool DesktopRefill(float dt)
        {
            var keys=Keyboard.current;if(keys==null)return false;
            if(!Run.Supply.Locked && grip.Held)
            {
                if(keys.hKey.wasPressedThisFrame)desktopDock=WaterSourceKind.Hydrant;
                if(keys.cKey.wasPressedThisFrame && Run.Stage>0)desktopDock=WaterSourceKind.Creek;
            }
            var supply=Run.Supply;
            var view=supply.Locked?(supply.Kind==WaterSourceKind.Hydrant?hydrantStation:creekStation):desktopDock==WaterSourceKind.Creek?creekStation:hydrantStation;
            if(!supply.Locked && desktopDock!=WaterSourceKind.None)
            {
                desktopOutletStroke.Reset();desktopOutletTravel=0;
                EmberdeepRefillInteraction.Snap(heldView,view.Socket);
                if(supply.SampleDock(desktopDock,grip.Hand,0,0,true,dt))desktopDock=WaterSourceKind.None;
                return true;
            }
            if(!supply.Locked)
            {
                if(Run.StationTier!=WaterStationTier.LeverWell || (!keys.rKey.isPressed && !keys.rKey.wasReleasedThisFrame))return false;
                desktopOutletTravel=keys.rKey.isPressed && !keys.rKey.wasPressedThisFrame?.085f:0;
                if(desktopOutletStroke.Sample(desktopOutletTravel,true,true))OnEmptyOutletStroke(12);
                if(keys.rKey.wasReleasedThisFrame)desktopOutletStroke.Reset();
                return true;
            }
            EmberdeepRefillInteraction.Snap(heldView,view.Socket);
            if(keys.xKey.wasPressedThisFrame){supply.Disconnect();CancelWeaponInput();return true;}
            if(keys.vKey.wasPressedThisFrame && Run.StationTier==WaterStationTier.RotaryHydrant)supply.TurnValve(supply.ValveOpen?-45:45);
            if(Run.StationTier==WaterStationTier.PushButton)supply.SampleButton(keys.vKey.isPressed,true);
            if(keys.iKey.wasPressedThisFrame)supply.PlaceIntake(!supply.IntakePlaced);
            if(supply.Kind==WaterSourceKind.Hydrant && Run.StationTier==WaterStationTier.LeverWell)
            {
                if(keys.rKey.isPressed)supply.SampleLever(keys.rKey.wasPressedThisFrame?0:.085f,true,true);
                if(keys.rKey.wasReleasedThisFrame){supply.SampleLever(0,true,true);supply.SampleLever(0,false,true);}
            }
            else
            {
                if(keys.rKey.isPressed)supply.SamplePump(keys.rKey.wasPressedThisFrame?0:.085f,true,true);
                if(keys.rKey.wasReleasedThisFrame){supply.SamplePump(0,true,true);supply.SamplePump(0,false,true);}
            }
            supply.Tick(dt,true);return true;
        }
        void RefreshWaterStations()
        {
            if(!hydrantStation)return;
            hydrantStation.SetTier(Run.StationTier);
            var s=Run.Supply;float ratio=Run.Water/Run.Capacity;
            string control=Run.StationTier==WaterStationTier.LeverWell?"LOWER + RETURN LEVER":Run.StationTier==WaterStationTier.RotaryHydrant?"TURN THE WHEEL":"PRESS THE BUTTON";
            string hyd=s.Kind==WaterSourceKind.Hydrant?(s.ValveOpen?"FILLING":"DOCKED / "+(ratio>=.999f?"FULL":control)):"FREE WATER\nAlign cyan coupling";
            string creek=s.Kind==WaterSourceKind.Creek?(ratio>=.999f?"FULL / TAKE TOOL":s.IntakePlaced?"PULL + RETURN":"PLACE INTAKE"):"OPTIONAL CREEK / FREE WATER\nRelease pinch, then dock";
            if(!Player.IsXR)
            {
                hyd=s.Kind==WaterSourceKind.Hydrant?(Run.StationTier==WaterStationTier.LeverWell?"R: lower / release to return\nX: leave":"V: operate  |  X: leave"):
                    Run.StationTier==WaterStationTier.LeverWell?"R: try pump / H: dock tool":"H: dock water station";
                creek=s.Kind==WaterSourceKind.Creek?"I: intake / R: pump\nX: leave":"OPTIONAL CREEK / C: dock";
            }
            bool strokeFlash=s.Locked && Time.unscaledTime<supplyStrokeFlashUntil;
            hydrantStation.SetState(hyd,ratio,s.ValveOpen || (strokeFlash && s.Kind==WaterSourceKind.Hydrant),s.Kind==WaterSourceKind.Hydrant,0,false);
            creekStation.SetState(creek,ratio,strokeFlash && s.Kind==WaterSourceKind.Creek,s.Kind==WaterSourceKind.Creek,Player.IsXR?refill.PumpTravel:s.PumpTravel01*.08f,s.IntakePlaced);
            hydrantStation.SetControlAnimation(refill.EmptyOutlet?refill.ActionAmount:!Player.IsXR && !s.Locked?desktopOutletTravel:s.LeverTravel01*.08f,
                Player.IsXR?refill.ValveVisualDegrees:(s.ValveOpen?45:0),
                Player.IsXR?(refill.CurrentAction==EmberdeepRefillInteraction.ActionKind.Button?refill.ActionAmount:0):
                (Keyboard.current!=null && Keyboard.current.vKey.isPressed?1:0));
            bool guide=Player.IsXR && focused && !paused && hands.HeadReady;
            hydrantStation.ShowTutorial(Run,heldView,grip.Held,rackView?rackView.Grip:null,guide,refill.CarryingIntake,refill.EmptyOutlet,refill.EmptyLeverReturnRequired);
            creekStation.ShowTutorial(Run,heldView,grip.Held,rackView?rackView.Grip:null,guide,refill.CarryingIntake);
            hydrantStation.FaceLabels(Player.ViewCamera.transform.rotation);creekStation.FaceLabels(Player.ViewCamera.transform.rotation);
        }
    }
}

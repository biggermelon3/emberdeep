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
        EmberdeepWaterStationView hydrantStation,creekStation;
        bool interfaceHovered;
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
            creekStation.transform.SetPositionAndRotation(Arena.TransformPoint(new Vector3(-side*3.3f,.2f,.4f)),Arena.rotation);
            hydrantStation.SetLeftHanded(LeftHanded);creekStation.SetLeftHanded(!LeftHanded);
        }
        void StopRefill()
        {
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
            bool accepted=handMenu.Process(hands,Player.ViewCamera,Player.IsXR,buttons,true,Time.unscaledDeltaTime,
                AllowInterfaceButton,AllowHandInterface);
            return AcceptInterfaceInput(accepted);
        }
        public EmberdeepHandRayMenu MenuInput=>handMenu;
        void EnsureHandMenu()
        {
            if(!handMenu){handMenu=gameObject.AddComponent<EmberdeepHandRayMenu>();handMenu.Initialize(transform,PanelMaterial);}
        }
        bool AllowHandInterface(int hand)
        {
            if(ShopOpen)return true;
            if(refill.OwnsControl && refill.OperatingHand==hand)return false;
            if(grip.Held && !Run.Supply.Locked && hands.ReadHand(grip.Hand==0,out var owner,out _,out _) &&
                (EmberdeepRefillInteraction.NearStation(owner.position,hydrantStation) ||
                 EmberdeepRefillInteraction.NearStation(owner.position,creekStation)))return false;
            if(!hands.ReadHand(hand==0,out var palm,out var tip,out _))return false;
            if(NearWellLever(hand,palm))return false;
            // Direct work owns the hand before the UI gets a chance to consume
            // its pinch. The other hand remains available for distant UI.
            if((grip.Held || Run.Supply.Locked) &&
                (EmberdeepRefillInteraction.NearStation(palm.position,hydrantStation) ||
                 EmberdeepRefillInteraction.NearStation(palm.position,creekStation)))return false;
            return grip.Held || !rackView || Vector3.Distance(tip,rackView.Grip.position)>.12f;
        }
        public bool ProcessHandMenuInput(EmberdeepHandRay.Sample left,EmberdeepHandRay.Sample right,bool available,float dt)
        {
            EnsureHandMenu();
            available=available && focused && !paused && !returningToCamp && !acquiringPose;
            if(!available)CancelWeaponInput();
            if(refill.OwnsControl){if(refill.OperatingHand==0)left=default;else right=default;}
            return AcceptInterfaceInput(handMenu.ProcessSamples(left,right,buttons,available,dt,AllowInterfaceButton));
        }
        bool AcceptInterfaceInput(bool accepted)
        {
            interfaceHovered=handMenu.OwnsInput;
            if(interfaceHovered)CancelWeaponInput();
            if(!accepted)return false;
            CancelWeaponInput();
            refill.ConsumeInput(Run);
            hydrantStation.StopOutlet();
            var pressed=handMenu.PressedButton;handMenu.ResetInput();pressed.Press();return true;
        }
        bool AllowInterfaceButton(WorkshopButton button)=>!Run.Supply.Locked || button.Command=="Next incident";
        bool ProcessRefillInput(Pose palm,bool valid,float dt)
        {
            if(!Player.IsXR)return DesktopRefill(dt);
            bool l=hands.ReadHand(true,out var lp,out var lt,out var ld);
            bool r=hands.ReadHand(false,out var rp,out var rt,out var rd);
            if(grip.Held && !Run.Supply.Locked && !refill.OwnsControl && valid)
                EmberdeepRefillInteraction.TryGuideHeldTool(heldView,new Pose(GripPoint(LeftHanded,palm),palm.rotation),hydrantStation,creekStation);
            if(interfaceHovered && !Run.Supply.Locked){refill.Reset();return false;}
            return ProcessRefillHands(ReadRefillHand(0,l,lp,lt,ld),ReadRefillHand(1,r,rp,rt,rd),dt);
        }
        EmberdeepRefillInteraction.Hand ReadRefillHand(int index,bool tracked,Pose palm,Vector3 tip,float pinch)
        {
            var sample=new EmberdeepRefillInteraction.Hand(tracked,palm,pinch,tip);
            if(tracked && EmberdeepHandGrasp.TryRead(hands.TrackedHand(index==0),out var strength,out var contact))
            {sample.GripValid=true;sample.GripStrength=strength;sample.GripContact=contact;sample.HasGripContact=true;}
            return sample;
        }
        bool NearWellLever(int index,Pose palm)
        {
            if(!hydrantStation || !hydrantStation.gameObject.activeInHierarchy || Run.StationTier!=WaterStationTier.LeverWell)return false;
            var contact=palm.position;
            if(EmberdeepHandGrasp.TryRead(hands.TrackedHand(index==0),out _,out var center))contact=center;
            return Vector3.Distance(contact,hydrantStation.LeverVisual.position)<.13f;
        }
        // Production path exposed for synthetic joint regressions; never invents a transfer.
        public bool ProcessRefillHands(EmberdeepRefillInteraction.Hand left,EmberdeepRefillInteraction.Hand right,float dt)
        {
            if(!focused || paused || !hands.HeadReady){ResetInput();return true;}
            bool hadDock=Run.Supply.Locked;
            bool wasEmpty=refill.EmptyOutlet;int previousOperator=refill.OperatingHand;
            bool consumed=refill.Process(Run,heldView,hydrantStation,creekStation,grip.Held,grip.Hand,left,right,dt);
            if(refill.Detached){grip.EquipForDesktop(refill.DetachedHand);LeftHanded=refill.DetachedHand==0;CancelWeaponInput();}
            if(refill.Interrupted){hydrantStation.StopOutlet();if(hadDock)ReturnEquipment();}
            else if(wasEmpty && (Run.Phase==FirePhase.Lost || !(previousOperator==0?left:right).GripValid))hydrantStation.StopOutlet();
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
            string creek=s.Kind==WaterSourceKind.Creek?(ratio>=.999f?"FULL / TAKE TOOL":s.IntakePlaced?"PULL + RETURN":"PLACE INTAKE"):"CREEK / FREE WATER\nDock, place intake, pump";
            if(!Player.IsXR)
            {
                hyd=s.Kind==WaterSourceKind.Hydrant?(Run.StationTier==WaterStationTier.LeverWell?"R: lower / release to return\nX: leave":"V: operate  |  X: leave"):
                    Run.StationTier==WaterStationTier.LeverWell?"R: try pump / H: dock tool":"H: dock water station";
                creek=s.Kind==WaterSourceKind.Creek?"I: intake / R: pump\nX: leave":"C: dock creek";
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

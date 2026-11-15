using Oculus.Interaction.Input;
using UnityEngine;

namespace FireRescue
{
    public sealed partial class FireRescueGame
    {
        EmberdeepHandVisuals handVisuals;
        Transform stationHandContact,stationContactParent;
        EmberdeepRefillInteraction.ActionKind stationContactAction;
        int stationContactHand=-1;
        // Rendering requests follow accepted interaction ownership, never pinch
        // distance alone. Animated hands cannot drive the real input samples.
        void LateUpdate()
        {
            if(ImmersiveSky)ImmersiveSky.Tick(Time.unscaledDeltaTime,Player && hands && focused && !paused
                && hands.HeadReady && !acquiringPose && !returningToCamp && Arena && Arena.gameObject.activeInHierarchy);
            if(!handVisuals)return;
            handVisuals.ClearInteractionPoses();
            if(!Player || !hands || !focused || paused || ShopOpen || returningToCamp || acquiringPose)return;
            if(!Run.Supply.Locked && grip.Held && heldView && heldView.gameObject.activeInHierarchy)
            {
                var toolPose=Run.Equipped==FireEquipmentId.Extinguisher?EmberdeepHandPoseKind.ExtinguisherGrip:
                    Run.Equipped==FireEquipmentId.PressureCannon?EmberdeepHandPoseKind.CannonGrip:EmberdeepHandPoseKind.PistolGrip;
                Transform primary=heldView.Grip;
                if(Player.IsXR)
                {
                    switch(toolManipulation.Region)
                    {
                        case FireToolRegion.Body:toolPose=EmberdeepHandPoseKind.BodyGrip;primary=heldView.SurfaceGrip;break;
                        case FireToolRegion.Nozzle:toolPose=EmberdeepHandPoseKind.NozzleGrip;primary=heldView.Nozzle;break;
                        case FireToolRegion.Pump:toolPose=EmberdeepHandPoseKind.CannonPump;primary=heldView.PumpGrip;break;
                    }
                }
                if(!Player.IsXR || toolManipulation.HasPhysicalGrip)
                    handVisuals.SetInteractionPose(grip.Hand,toolPose,primary,Spraying?1:Run.Charge01);
                if(toolManipulation.NozzleHeld)
                    handVisuals.SetInteractionPose(1-grip.Hand,EmberdeepHandPoseKind.NozzleGrip,heldView.Nozzle);
                else if(toolManipulation.SupportHeld)
                    handVisuals.SetInteractionPose(1-grip.Hand,EmberdeepHandPoseKind.ForwardSupport,heldView.SupportGrip);
                if(!refill.OwnsControl && pump.Active && heldView.PumpGrip)
                    handVisuals.SetInteractionPose(1-grip.Hand,EmberdeepHandPoseKind.CannonPump,heldView.PumpGrip,pump.Pull01);
            }
            if(Run.Supply.Locked || refill.OwnsControl)
            {
                var station=refill.ActiveView;
                if(!station || refill.OperatingHand<0){stationContactHand=-1;return;}
                var kind=EmberdeepHandPoseKind.Free;Transform anchor=null;
                switch(refill.CurrentAction)
                {
                    case EmberdeepRefillInteraction.ActionKind.Lever:kind=EmberdeepHandPoseKind.LeverGrip;anchor=station.LeverVisual;break;
                    case EmberdeepRefillInteraction.ActionKind.Valve:kind=EmberdeepHandPoseKind.ValveGrip;anchor=station.ValveVisual;break;
                    case EmberdeepRefillInteraction.ActionKind.Button:kind=EmberdeepHandPoseKind.ButtonPress;anchor=station.ButtonVisual;break;
                    case EmberdeepRefillInteraction.ActionKind.Intake:kind=EmberdeepHandPoseKind.IntakeGrip;anchor=station.IntakeVisual;break;
                    case EmberdeepRefillInteraction.ActionKind.Pump:kind=EmberdeepHandPoseKind.LeverGrip;anchor=station.PumpVisual;break;
                }
                float amount=refill.CurrentAction==EmberdeepRefillInteraction.ActionKind.Lever || refill.CurrentAction==EmberdeepRefillInteraction.ActionKind.Pump
                    ?refill.ActionAmount/.08f:refill.CurrentAction==EmberdeepRefillInteraction.ActionKind.Valve?Mathf.Abs(refill.ActionAmount)/45:refill.ActionAmount;
                if(Player.IsXR && anchor)anchor=StationHandContact(anchor,refill.CurrentAction,refill.OperatingHand);
                handVisuals.SetInteractionPose(refill.OperatingHand,kind,anchor,amount);return;
            }
            stationContactHand=-1;
        }
        Transform StationHandContact(Transform control,EmberdeepRefillInteraction.ActionKind action,int hand)
        {
            if(!stationHandContact){stationHandContact=new GameObject("Accepted station hand contact").transform;stationHandContact.SetParent(transform,false);}
            if(stationContactHand==hand && stationContactParent==control && stationContactAction==action)
            {
                if(action==EmberdeepRefillInteraction.ActionKind.Lever || action==EmberdeepRefillInteraction.ActionKind.Pump)
                    stationHandContact.rotation=refill.ActiveView.transform.rotation;
                return stationHandContact;
            }
            stationContactHand=hand;stationContactParent=control;stationContactAction=action;
            stationHandContact.SetParent(control,false);stationHandContact.localScale=Vector3.one;
            stationHandContact.SetPositionAndRotation(control.position,control.rotation);
            if(action==EmberdeepRefillInteraction.ActionKind.Valve)
            {
                var radial=control.right*(hand==0?-1:1);
                stationHandContact.SetPositionAndRotation(control.position+radial.normalized*.05f,
                    control.rotation);
            }
            else if(action==EmberdeepRefillInteraction.ActionKind.Button)
            {
                var mesh=control.GetComponentInChildren<MeshFilter>();
                if(mesh){EmberdeepHandContact.Surface(mesh,objectHands[hand].FingerContact,out var point,out var normal);stationHandContact.position=point+normal*.007f;}
            }
            return stationHandContact;
        }
        Vector3 GripPoint(bool left,Pose palm)
        {
            var hand=hands.TrackedHand(left);
            if(hand && hand.GetJointPose(HandJointId.HandIndexTip,out var index) &&
                hand.GetJointPose(HandJointId.HandThumbTip,out var thumb))
                return (index.position+thumb.position)*.5f;
            return palm.position;
        }
        void ConfigureHandOcclusion()
        {
            if(!handVisuals)return;
            handVisuals.ClearOcclusionBoxes();
            handVisuals.Contacts.Clear();
            if(heldView)handVisuals.Contacts.Add(heldView.transform);
            if(rackView)handVisuals.Contacts.Add(rackView.transform);
            if(hydrantStation)handVisuals.Contacts.Add(hydrantStation.transform);
            if(creekStation)handVisuals.Contacts.Add(creekStation.transform);
            // Inscribed solid volumes only. Do not erase fingers in handle gaps
            // or move the user's tracked skeleton to imitate physical collision.
            handVisuals.SetOcclusionBox(0,Arena,new Vector3(0,-.27f,6),new Vector3(4.1f,.24f,5.5f));
            if(hydrantStation)handVisuals.SetOcclusionBox(1,hydrantStation.transform,new Vector3(0,.11f,.075f),new Vector3(.027f,.075f,.027f));
            AddToolOcclusion(2,heldView,Run.Equipped);
            AddToolOcclusion(3,rackView,SelectedEquipment);
        }
        void AddToolOcclusion(int slot,FireEquipmentView tool,FireEquipmentId kind)
        {
            if(!tool)return;
            bool extinguisher=kind==FireEquipmentId.Extinguisher;
            handVisuals.SetOcclusionBox(slot,tool.transform,
                extinguisher?new Vector3(0,-.045f,0):new Vector3(0,.012f,.025f),
                extinguisher?new Vector3(.033f,.095f,.033f):new Vector3(.028f,.028f,.068f));
        }
    }
}

using Oculus.Interaction.Input;
using UnityEngine;

namespace FireRescue
{
    public sealed partial class FireRescueGame
    {
        EmberdeepHandVisuals handVisuals;
        // Rendering requests follow accepted interaction ownership, never pinch
        // distance alone. Animated hands cannot drive the real input samples.
        void LateUpdate()
        {
            if(!handVisuals)return;
            handVisuals.ClearInteractionPoses();
            if(!Player || !hands || !focused || paused || ShopOpen || returningToCamp || acquiringPose)return;
            if(!Run.Supply.Locked && grip.Held && heldView && heldView.gameObject.activeInHierarchy)
            {
                var toolPose=Run.Equipped==FireEquipmentId.Extinguisher?EmberdeepHandPoseKind.ExtinguisherGrip:
                    Run.Equipped==FireEquipmentId.PressureCannon?EmberdeepHandPoseKind.CannonGrip:EmberdeepHandPoseKind.PistolGrip;
                handVisuals.SetInteractionPose(grip.Hand,toolPose,heldView.Grip,Spraying?1:Run.Charge01);
                if(!refill.OwnsControl && pump.Active && heldView.PumpGrip)
                    handVisuals.SetInteractionPose(1-grip.Hand,EmberdeepHandPoseKind.CannonPump,heldView.PumpGrip,pump.Pull01);
            }
            if(Run.Supply.Locked || refill.OwnsControl)
            {
                var station=refill.ActiveView;
                if(!station || refill.OperatingHand<0)return;
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
                handVisuals.SetInteractionPose(refill.OperatingHand,kind,anchor,amount);return;
            }
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

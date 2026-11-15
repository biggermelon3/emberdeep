using UnityEngine;

namespace FireRescue
{
    public sealed partial class FireRescueGame
    {
        readonly FireToolManipulation toolManipulation=new();
        readonly FireToolManipulation.Hand[] toolHands=new FireToolManipulation.Hand[2];
        readonly EmberdeepRefillInteraction.Hand[] objectHands=new EmberdeepRefillInteraction.Hand[2];
        public FireToolManipulation ToolManipulation=>toolManipulation;
        bool ToolOwnsHand(int hand)=>grip.Held && (hand==grip.Hand?toolManipulation.HasPhysicalGrip:
            toolManipulation.NozzleHeld || toolManipulation.SupportHeld || pump.Active);
        void ReadObjectHands()
        {
            for(int i=0;i<2;i++)
            {
                bool left=i==0;
                bool poseValid=hands.ReadObjectPose(left,out var palm,out var pinch,out bool pinchValid);
                float strength;Vector3 contact;
                bool closureValid=hands.UsesPortableGrip?hands.TryReadPortableGrip(left,out strength,out contact):
                    EmberdeepHandGrasp.TryReadClosure(hands.TrackedHand(left),out strength,out contact);
                Quaternion gripRotation;
                bool frameValid=hands.UsesPortableGrip?hands.TryReadPortableGripFrame(left,out gripRotation):
                    EmberdeepHandGrasp.TryReadPalmFrame(hands.TrackedHand(left),out gripRotation);
                bool valid=poseValid && closureValid && frameValid;
                toolHands[i]=new FireToolManipulation.Hand{Valid=valid,Palm=palm,Contact=contact,Closure=strength,
                    HasGripFrame=frameValid,GripRotation=gripRotation};
                bool tipValid=hands.ReadHand(left,out _,out var tip,out _);
                objectHands[i]=new EmberdeepRefillInteraction.Hand(valid,palm,pinchValid?pinch:.04f,tipValid?tip:contact)
                    {UsesGrip=true,GripValid=valid,GripStrength=strength,GripContact=contact,HasGripContact=true};
            }
        }
        void ProcessToolPickup(float dt)
        {
            if(grip.Held || !Run.Owns(SelectedEquipment))return;
            var left=toolHands[0];var right=toolHands[1];
            if(refill.OwnsControl){if(refill.OperatingHand==0)left.Valid=false;else right.Valid=false;}
            if(left.Usable && NearWellLever(left.Contact))left.Valid=false;
            if(right.Usable && NearWellLever(right.Contact))right.Valid=false;
            int owner=toolManipulation.FindPickup(rackView,left,right,dt);
            if(owner<0)return;
            var pose=new Pose(rackView.transform.position,rackView.transform.rotation);
            if(!TakeEquipment(owner))return;
            heldView.transform.SetPositionAndRotation(pose.position,pose.rotation);
            toolManipulation.Begin(heldView,owner,toolHands[owner]);
        }
        void ProcessCarriedTool(float dt,bool permitted)
        {
            if(!grip.Held)return;
            var left=toolHands[0];var right=toolHands[1];
            if(refill.OwnsControl){if(refill.OperatingHand==0)left.Valid=false;else right.Valid=false;}
            if(toolManipulation.Step(heldView,rackView,left,right,dt,permitted))ReturnEquipment();
            if(!toolManipulation.TriggerValid){Run.CancelTrigger();pump.Reset();}
        }
    }
}

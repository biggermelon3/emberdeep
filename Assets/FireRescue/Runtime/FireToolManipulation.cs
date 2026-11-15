using UnityEngine;

namespace FireRescue
{
    // Scene input owns carrying; this policy never infers a drop from tracking
    // loss or from relaxing the firing hand. Only a deliberate open hand at the rack releases.
    public sealed class FireToolManipulation
    {
        public struct Hand
        {
            public bool Valid;
            public Pose Palm;
            public bool HasGripFrame;
            public Quaternion GripRotation;
            public Vector3 Contact;
            public float Closure;
            public bool Usable=>Valid && float.IsFinite(Contact.sqrMagnitude) && float.IsFinite(Palm.position.sqrMagnitude)
                && float.IsFinite(Closure) && Closure>=0 && Closure<=1 &&
                float.IsFinite(Palm.rotation.x+Palm.rotation.y+Palm.rotation.z+Palm.rotation.w)
                && Quaternion.Dot(Palm.rotation,Palm.rotation)>.5f
                && (!HasGripFrame || (float.IsFinite(Quaternion.Dot(GripRotation,GripRotation))
                    && Quaternion.Dot(GripRotation,GripRotation)>.5f));
        }
        readonly EmberdeepSqueeze[] grabs={new(),new()};
        readonly EmberdeepSqueeze trigger=new(EmberdeepSqueeze.Fire,EmberdeepSqueeze.StopFire);
        Vector3 anchor;
        Quaternion carryOffset;
        float returnTime;
        bool initialized,leftRack,adjusting;
        public FireToolRegion Region { get; private set; }
        public bool NozzleHeld { get; private set; }
        public bool SupportHeld { get; private set; }
        Vector3 supportAnchor;
        public bool Firing { get; private set; }
        public bool TriggerValid { get; private set; }
        public bool Adjusting=>adjusting;
        public bool HasPhysicalGrip=>initialized && !adjusting;
        public int Owner { get; private set; }=1;
        public void Reset()
        {initialized=leftRack=adjusting=NozzleHeld=SupportHeld=Firing=TriggerValid=false;returnTime=0;foreach(var g in grabs)g.Reset();trigger.Reset();}
        public void Prepare(int owner){Reset();Owner=owner;}
        public void CancelActions()
        {Firing=TriggerValid=NozzleHeld=SupportHeld=false;returnTime=0;trigger.Reset();foreach(var g in grabs)g.Reset();}
        public void Park(){CancelActions();adjusting=true;}
        public int FindPickup(FireEquipmentView rack,Hand left,Hand right,float dt)
        {
            for(int i=0;i<2;i++)
            {
                var h=i==0?left:right;grabs[i].Sample(h.Usable,h.Closure,dt);
                if(rack && h.Usable && grabs[i].CanAcquire && rack.TryGrabRegion(h.Contact,out _,out _))return i;
            }
            return -1;
        }
        public void Begin(FireEquipmentView tool,int owner,Hand hand)
        {
            Reset();Owner=owner;
            if(!tool || !hand.Usable)return;
            if(!tool.TryGrabRegion(hand.Contact,out var region,out var point))return;
            Region=region;anchor=Quaternion.Inverse(tool.transform.rotation)*(hand.Contact-tool.transform.position);
            tool.CaptureGrip(region,hand.Contact,owner==0);
            if(region==FireToolRegion.Trigger)anchor=tool.Grip.localPosition;
            carryOffset=Quaternion.Inverse(hand.Palm.rotation)*tool.transform.rotation;initialized=true;
        }
        // True means an intentional holster, never a lost sample or loose fist.
        public bool Step(FireEquipmentView tool,FireEquipmentView rack,Hand left,Hand right,float dt,bool permitted)
        {
            var owner=Owner==0?left:right;var other=Owner==0?right:left;
            Firing=false;TriggerValid=false;
            if(!permitted || !owner.Usable || !float.IsFinite(dt) || dt<=0)
            {CancelActions();if(tool)tool.ResetNozzle();return false;}
            bool lostSupport=SupportHeld && !other.Usable;
            for(int i=0;i<2;i++){var h=i==0?left:right;grabs[i].Sample(h.Usable,h.Closure,dt);}
            if(!initialized)
            {
                tool.transform.position=owner.Contact-tool.transform.rotation*tool.Grip.localPosition;
                Begin(tool,Owner,owner);return false;
            }
            float fromRack=Vector3.Distance(tool.transform.position,rack.transform.position);
            if(fromRack>.22f)leftRack=true;
            bool open=owner.Closure<=EmberdeepSqueeze.Open;
            returnTime=leftRack && open && fromRack<.18f && rack.TryGrabRegion(owner.Contact,out _,out _,true)
                ?returnTime+Mathf.Min(dt,.025f):0;
            if(returnTime>=.35f){Reset();return true;}
            // Opening only stops the trigger. Explicit docking interruption may
            // park a tool; ordinary grip relaxation never changes its ownership.
            if(adjusting && grabs[Owner].CanAcquire && tool.TryGrabRegion(owner.Contact,out var region,out _))
            {
                Region=region;anchor=Quaternion.Inverse(tool.transform.rotation)*(owner.Contact-tool.transform.position);
                tool.CaptureGrip(region,owner.Contact,Owner==0);
                if(region==FireToolRegion.Trigger)anchor=tool.Grip.localPosition;
                carryOffset=Quaternion.Inverse(owner.Palm.rotation)*tool.transform.rotation;adjusting=false;trigger.Reset();
            }
            if(!adjusting)
            {
                var rotation=owner.Palm.rotation*carryOffset;
                if(owner.HasGripFrame)
                {
                    var gripFrame=tool.PrimaryContact(Region,Owner==0);
                    rotation=owner.GripRotation*Quaternion.Inverse(gripFrame.rotation);
                    anchor=gripFrame.position;
                }
                tool.transform.SetPositionAndRotation(owner.Contact-rotation*anchor,rotation);
            }
            if(!tool.Nozzle)
            {
                if(!other.Usable || other.Closure<=EmberdeepSqueeze.Open || adjusting
                    || Vector3.Distance(other.Contact,owner.Contact)>.45f)SupportHeld=false;
                if(!SupportHeld && !adjusting && other.Usable && grabs[1-Owner].CanAcquire
                    && tool.TryGrabRegion(other.Contact,out var supportRegion,out var point)
                    && supportRegion!=FireToolRegion.Pump && point.z>.055f)
                {SupportHeld=true;tool.CaptureSupport(other.Contact);supportAnchor=tool.transform.InverseTransformPoint(tool.SupportGrip.TransformPoint(
                    EmberdeepHandPose.GripContact(EmberdeepHandPoseKind.ForwardSupport,Owner!=0).position));}
                if(SupportHeld)
                {
                    var target=other.Contact-owner.Contact;
                    var from=tool.transform.rotation*(supportAnchor-anchor);
                    if(target.magnitude<.07f || from.magnitude<.06f)SupportHeld=false;
                    else
                    {
                        var rotation=Quaternion.FromToRotation(from,target)*tool.transform.rotation;
                        tool.transform.SetPositionAndRotation(owner.Contact-rotation*anchor,rotation);
                    }
                }
            }
            if(tool.Nozzle)
            {
                if(!other.Usable || other.Closure<=EmberdeepSqueeze.Open || Vector3.Distance(other.Contact,tool.transform.position)>.65f)
                    NozzleHeld=false;
                if(!NozzleHeld && other.Usable && grabs[1-Owner].CanAcquire && Vector3.Distance(other.Contact,tool.Nozzle.position)<.09f)
                {NozzleHeld=true;}
                if(NozzleHeld)
                {
                    var contact=EmberdeepHandPose.GripContact(EmberdeepHandPoseKind.NozzleGrip,Owner!=0);
                    var frame=other.HasGripFrame?other.GripRotation:other.Palm.rotation*contact.rotation;
                    tool.MoveNozzle(EmberdeepHandPose.ObjectFromPalm(new Pose(other.Contact,frame),contact));
                }
                else tool.ResetNozzle();
            }
            bool ready=!lostSupport && !adjusting && Region==FireToolRegion.Trigger && (!tool.Nozzle || NozzleHeld);
            trigger.Sample(ready,owner.Closure,dt);
            // Full opening cancels cannon charge rather than releasing a shot,
            // while still arming the next deliberate squeeze.
            TriggerValid=ready && !open;Firing=TriggerValid && trigger.Closed;
            tool.UpdateHose();return false;
        }
    }
}

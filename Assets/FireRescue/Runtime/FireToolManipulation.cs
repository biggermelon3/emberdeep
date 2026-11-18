using UnityEngine;

namespace FireRescue
{
    // Scene input owns carrying; this policy never infers a drop from tracking
    // loss. Half closure stops water; a verified open palm releases into a timed hover.
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
        float returnTime,alignTime,floatTime;
        Vector3 floatPosition,originalScale=Vector3.one;
        Quaternion floatRotation;
        public bool Floating {get;private set;}
        public float FloatingSeconds=>floatTime;
        float nozzleTime,transferTime,nozzleRecovery;
        readonly bool[] transferArmed=new bool[2];
        readonly float[] pickupDwell=new float[2];
        bool initialized,adjusting,needsRelax;
        public FireToolRegion Region { get; private set; }
        public bool NozzleHeld { get; private set; }
        public bool SupportHeld { get; private set; }
        Vector3 supportAnchor;
        public bool Firing { get; private set; }
        public bool TriggerValid { get; private set; }
        public bool NeedsTriggerRelax=>!trigger.ReadyForSqueeze;
        public bool Adjusting=>adjusting || Floating;
        public bool HasPhysicalGrip=>initialized && !adjusting && !Floating;
        public int Owner { get; private set; }=1;
        public void Reset()
        {needsRelax=initialized=adjusting=Floating=NozzleHeld=SupportHeld=Firing=TriggerValid=false;returnTime=alignTime=floatTime=nozzleTime=transferTime=nozzleRecovery=0;pickupDwell[0]=pickupDwell[1]=0;transferArmed[0]=transferArmed[1]=false;foreach(var g in grabs)g.Reset();trigger.Reset();}
        public void Prepare(int owner){Reset();Owner=owner;}
        public void CancelActions()
        {needsRelax=true;Firing=TriggerValid=NozzleHeld=SupportHeld=false;returnTime=nozzleTime=transferTime=nozzleRecovery=0;pickupDwell[0]=pickupDwell[1]=0;trigger.Reset();foreach(var g in grabs)g.Reset();}
        public void Park(){CancelActions();adjusting=true;}
        public int FindPickup(FireEquipmentView rack,Hand left,Hand right,float dt)
        {
            for(int i=0;i<2;i++)
            {
                var h=i==0?left:right;grabs[i].Sample(h.Usable,h.Closure,dt);
                bool near=rack && h.Usable && h.Closure>=EmberdeepSqueeze.Grab && rack.TryPrimaryGrab(h.Contact,i,out _,out _);
                pickupDwell[i]=near && float.IsFinite(dt) && dt>0?pickupDwell[i]+Mathf.Min(dt,.025f):0;
                if(pickupDwell[i]>=.05f){pickupDwell[0]=pickupDwell[1]=0;return i;}
            }
            return -1;
        }
        public void Begin(FireEquipmentView tool,int owner,Hand hand)
        {
            bool keepNozzle=NozzleHeld && owner==1;Reset();NozzleHeld=keepNozzle;Owner=owner;transferArmed[1-owner]=true;
            if(keepNozzle)trigger.Sample(true,EmberdeepSqueeze.StopFire,.025f);
            if(!tool || !hand.Usable)return;
            if(!tool.TryPrimaryGrab(hand.Contact,owner,out var region,out var point))return;
            Region=region;anchor=Quaternion.Inverse(tool.transform.rotation)*(hand.Contact-tool.transform.position);
            tool.CaptureGrip(region,hand.Contact,owner==0);
            originalScale=tool.transform.localScale;
            carryOffset=Quaternion.Inverse(hand.Palm.rotation)*tool.transform.rotation;initialized=true;
        }
        // True means the five-second hover has finished. Tracking loss never starts it.
        public bool Step(FireEquipmentView tool,FireEquipmentView rack,Hand left,Hand right,float dt,bool permitted)
        {
            var owner=Owner==0?left:right;var other=Owner==0?right:left;
            Firing=false;TriggerValid=false;
            if(!permitted || !float.IsFinite(dt) || dt<=0)
            {CancelActions();if(tool)tool.ResetNozzle();return false;}
            dt=Mathf.Min(dt,.05f);
            if(Floating || adjusting)
            {
                if(Floating)UpdateNozzle(tool,other,dt);
                int next=FindPickup(tool,left,right,dt);
                if(next>=0)
                {tool.transform.localScale=originalScale;Begin(tool,next,next==0?left:right);return false;}
                if(Floating)
                {
                    floatTime+=dt;
                    float wobble=Mathf.Clamp01((floatTime-3)*3);
                    tool.transform.SetPositionAndRotation(floatPosition+Vector3.up*(Mathf.Sin(floatTime*11)*.008f*wobble),
                        floatRotation*Quaternion.Euler(0,0,Mathf.Sin(floatTime*14)*5*wobble));
                    tool.transform.localScale=originalScale*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(4.65f,5,floatTime)));
                    tool.UpdateHose();
                    if(floatTime>=5){tool.transform.localScale=originalScale;Reset();return true;}
                }
                return false;
            }
            if(!owner.Usable){CancelActions();if(tool)tool.ResetNozzle();return false;}
            bool lostSupport=SupportHeld && !other.Usable;
            for(int i=0;i<2;i++){var h=i==0?left:right;grabs[i].Sample(h.Usable,h.Closure,dt);}
            for(int i=0;i<2;i++){var h=i==0?left:right;if(h.Usable && h.Closure<=EmberdeepSqueeze.Open)transferArmed[i]=true;}
            if(!initialized)
            {
                Begin(tool,Owner,owner);return false;
            }
            bool open=owner.Closure<=EmberdeepSqueeze.Open;
            returnTime=open?returnTime+dt:0;
            if(returnTime>=.10f)
            {
                bool keep=NozzleHeld;CancelActions();NozzleHeld=keep;Floating=true;floatTime=0;
                floatPosition=tool.transform.position;floatRotation=tool.transform.rotation;
                originalScale=tool.transform.localScale;return false;
            }
            // The nozzle is always a secondary aiming contact, never a theft of
            // primary ownership. A deliberate body/handle grasp transfers ownership.
            bool atNozzle=tool.NearNozzle(other.Contact,.10f);
            bool handoff=other.Usable && transferArmed[1-Owner] && !NozzleHeld && !SupportHeld
                && other.Closure>=EmberdeepSqueeze.Grab && !atNozzle
                && Vector3.Distance(other.Contact,owner.Contact)>.045f
                && tool.TryPrimaryGrab(other.Contact,1-Owner,out var nextRegion,out _)
                && nextRegion!=FireToolRegion.Nozzle && nextRegion!=FireToolRegion.Pump
                && (tool.Nozzle || tool.transform.InverseTransformPoint(other.Contact).z<.055f);
            transferTime=handoff?transferTime+Mathf.Min(dt,.025f):0;
            if(transferTime>=.075f)
            {
                int next=1-Owner;Begin(tool,next,other);
                transferArmed[0]=transferArmed[1]=false;
                tool.SetLeftHanded(next==0);return false;
            }
            // Preserve the acquisition heading. Ease the measured contact onto
            // its authored grip over 160 ms, never overwrite orientation with a pose.
            alignTime=Mathf.Min(.16f,alignTime+dt);
            var rotation=owner.Palm.rotation*carryOffset;
            var contactPoint=tool.PrimaryContact(Region,Owner==0).position;
            var alignedAnchor=Vector3.Lerp(anchor,contactPoint,Mathf.SmoothStep(0,1,alignTime/.16f));
            float follow=1-Mathf.Exp(-40*dt);
            tool.transform.SetPositionAndRotation(Vector3.Lerp(tool.transform.position,owner.Contact-rotation*alignedAnchor,follow),
                Quaternion.Slerp(tool.transform.rotation,rotation,follow));
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
                    var from=tool.transform.rotation*(supportAnchor-alignedAnchor);
                    if(target.magnitude<.07f || from.magnitude<.06f)SupportHeld=false;
                    else
                    {
                        var supportedRotation=Quaternion.FromToRotation(from,target)*tool.transform.rotation;
                        tool.transform.SetPositionAndRotation(owner.Contact-supportedRotation*alignedAnchor,supportedRotation);
                    }
                }
            }
            if(owner.Closure<=EmberdeepSqueeze.StopFire)needsRelax=false;
            UpdateNozzle(tool,other,dt);
            bool ready=!lostSupport && !adjusting && Region==FireToolRegion.Trigger && (!tool.Nozzle || NozzleHeld);
            trigger.Sample(ready,owner.Closure,dt);
            // Full opening cancels cannon charge rather than releasing a shot,
            // before a verified open-palm dwell releases into the hover.
            TriggerValid=ready && !open;Firing=TriggerValid && trigger.Closed;
            tool.UpdateHose();return false;
        }
        void UpdateNozzle(FireEquipmentView tool,Hand other,float dt)
        {
            float follow=1-Mathf.Exp(-40*dt);
            if(tool.Nozzle && Owner==1)
            {
                if(NozzleHeld && !other.Usable){nozzleRecovery=.35f;needsRelax=true;}
                else nozzleRecovery=Mathf.Max(0,nozzleRecovery-Mathf.Min(dt,.025f));
                if(!other.Usable || other.Closure<=EmberdeepSqueeze.Open || Vector3.Distance(other.Contact,tool.transform.position)>.65f)
                    NozzleHeld=false;
                // A hand may close while approaching the horn. Proximity + stable
                // closure is intentional here; there is no 250 ms catch deadline.
                nozzleTime=!NozzleHeld && other.Usable && other.Closure>=EmberdeepSqueeze.Grab
                    && tool.NearNozzle(other.Contact,.125f)?nozzleTime+Mathf.Min(dt,.025f):0;
                if(nozzleTime>=.05f){NozzleHeld=true;nozzleTime=nozzleRecovery=0;trigger.Reset();if(!needsRelax)trigger.Sample(true,EmberdeepSqueeze.StopFire,dt);}
                if(NozzleHeld)
                {
                    var contact=EmberdeepHandPose.GripContact(EmberdeepHandPoseKind.NozzleGrip,Owner!=0);
                    var frame=other.HasGripFrame?other.GripRotation:other.Palm.rotation*contact.rotation;
                    var targetPose=EmberdeepHandPose.ObjectFromPalm(new Pose(other.Contact,frame),contact);
                    // +Z of the measured grip frame is wrist -> middle knuckle,
                    // independent of the palm normal even when the fist rolls.
                    tool.MoveNozzle(new Pose(Vector3.Lerp(tool.Nozzle.position,targetPose.position,follow),
                        Quaternion.Slerp(tool.Nozzle.rotation,targetPose.rotation,follow)));
                }
                else if(nozzleRecovery<=0 || (other.Usable && other.Closure<=EmberdeepSqueeze.Open))tool.ResetNozzle();
            }
        }
    }
}

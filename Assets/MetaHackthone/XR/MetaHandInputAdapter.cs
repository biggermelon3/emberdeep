using System.Linq;
using Oculus.Interaction.Input;
using UnityEngine;
using XRSettings = UnityEngine.XR.XRSettings;
using Hand = Oculus.Interaction.Input.Hand;

namespace PhysicalAssembly.MetaXR
{
    [DefaultExecutionOrder(-200)]
    public sealed class MetaHandInputAdapter : MonoBehaviour
    {
        public WorkshopBootstrap Workshop;
        public Transform XRRig;
        public Camera DesktopCamera;
        public float GrabRadius=.075f,ExtendedDot=.80f,CurledDot=.55f;
        readonly GestureRecognizer[] gestures={new(),new()};
        readonly IHand[] hands=new IHand[2];
        readonly HandGrabDriver[] grips=new HandGrabDriver[2];
        readonly Fingers[] fingerState=new Fingers[2];
        readonly LaunchGesture.HandSample[] launchSamples=new LaunchGesture.HandSample[2];
        readonly Vector3[] launchDirections=new Vector3[2];
        bool launchBlockedFrame;
        WorkshopButton[] menuButtons;
        readonly Collider[] overlaps=new Collider[64];
        Hand[] sources; OVRHand[] systemHands;
        readonly MetaPinchSelector[] pointerPinches=new MetaPinchSelector[2];
        Oculus.Interaction.RayInteractor[] rays;
        readonly Oculus.Interaction.RayInteractor[] buildRays=new Oculus.Interaction.RayInteractor[2];
        readonly SandboxPlaneDrag[] rayDrags={new(),new()};
        readonly bool[] rayHolding=new bool[2];
        bool wasDrive;
        bool placed; Transform headAnchor;
        float nextPoseLog;
        readonly int[] sampleVersions={-1,-1};
        static readonly HandJointId[] roots={HandJointId.HandThumb1,HandJointId.HandIndex1,HandJointId.HandMiddle1,HandJointId.HandRing1,HandJointId.HandPinky1};
        static readonly HandJointId[] mids={HandJointId.HandThumb2,HandJointId.HandIndex2,HandJointId.HandMiddle2,HandJointId.HandRing2,HandJointId.HandPinky2};
        static readonly HandJointId[] tips={HandJointId.HandThumbTip,HandJointId.HandIndexTip,HandJointId.HandMiddleTip,HandJointId.HandRingTip,HandJointId.HandPinkyTip};
        void Awake()
        {
            var shared=MetaPlayerRig.Shared;
            if(shared && shared.XRRig!=XRRig)
            {
                // Authored fallback cameras stay inactive until this adapter resolves ownership.
                var oldRig=XRRig; var oldCamera=DesktopCamera;
                XRRig=shared.XRRig; DesktopCamera=shared.DesktopCamera;
                if(oldRig) { oldRig.gameObject.SetActive(false); Destroy(oldRig.gameObject); }
                if(oldCamera) { oldCamera.gameObject.SetActive(false); Destroy(oldCamera.gameObject); }
                var desktop=GetComponent<DesktopInputAdapter>(); if(desktop) desktop.View=DesktopCamera;
            }
            var player=XRRig.GetComponent<MetaPlayerRig>();
            if(!player) player=XRRig.gameObject.AddComponent<MetaPlayerRig>();
            player.Initialize(XRRig,DesktopCamera);
        }
        public static void ConfigureRoomScaleRig(Transform rig)
        {
            // ComprehensiveInteractionRig includes a VR locomotor with gravity,
            // grounding and a player capsule. It moves the tracking origin onto
            // the toy platform and against parts as the user's head moves.
            // MR uses physical room-scale movement, so disable the whole locomotor
            // (including its colliders) and its gesture/controller input groups.
            foreach(var child in rig.GetComponentsInChildren<Transform>(true))
                if(child.name=="Locomotor" || child.name.Contains("Locomotion"))
                    child.gameObject.SetActive(false);
            foreach(var locomotor in rig.GetComponentsInChildren<Oculus.Interaction.Locomotion.FirstPersonLocomotor>(true))
                locomotor.gameObject.SetActive(false);
        }
        void Start()
        {
            sources=XRRig.GetComponentsInChildren<Hand>(true); systemHands=XRRig.GetComponentsInChildren<OVRHand>(true);
            var ovr=XRRig.GetComponent<OVRCameraRig>();
            headAnchor=ovr?ovr.centerEyeAnchor:XRRig.GetComponentInChildren<Camera>(true).transform;
            menuButtons=Workshop.InteractionButtons;
            for(int i=0;i<2;i++) grips[i]=new HandGrabDriver(Workshop.World,i);
            foreach(var button in menuButtons) button.gameObject.AddComponent<MetaMenuButton>().Initialize(Workshop);
            gameObject.AddComponent<MetaArenaPlacement>().Initialize(Workshop,headAnchor?headAnchor:DesktopCamera.transform);
            rays=XRRig.GetComponentsInChildren<Oculus.Interaction.RayInteractor>(true);
            Debug.Log($"METAHACKTHONE_INPUT hands={sources.Length} rays={rays.Length} pokes={XRRig.GetComponentsInChildren<Oculus.Interaction.PokeInteractor>(true).Length} buttons={menuButtons.Length}");
            if(XRRig.gameObject.activeInHierarchy) { if(ovr && OVRManager.display!=null) OVRManager.display.displayFrequency=90; Application.targetFrameRate=90; }
        }
        void BindPointerPinches()
        {
            foreach(var ray in rays)
            {
                var selector=MetaPinchSelector.BindToRay(ray);
                if(!selector) continue;
                int index=selector.Hand.Handedness==Handedness.Left?0:1;
                selector.SuppressSelection=()=>LaunchBlocksInput() || Workshop.Downhill && (Workshop.Downhill.IsRotatingWithHand(index) || Workshop.Downhill.DragBuilder && Workshop.World.HeldPart(index));
                pointerPinches[index]=selector; buildRays[index]=ray;
            }
        }
        void LateUpdate()
        {
            if(!XRRig.gameObject.activeInHierarchy) { if(Workshop.IsChallenge) Workshop.Challenge.CancelAim(); if(Workshop.Downhill) Workshop.Downhill.CancelRotation(); return; }
            if(!pointerPinches[0] || !pointerPinches[1]) BindPointerPinches();
            if(!placed && headAnchor && headAnchor.position.y-XRRig.position.y>.4f) { Workshop.PositionAroundPlayer(headAnchor,XRRig.GetComponent<MetaPlayerRig>().FloorHeight); placed=true; }
            if(Debug.isDebugBuild && headAnchor && Time.unscaledTime>=nextPoseLog)
            {
                nextPoseLog=Time.unscaledTime+2;
                var manager=XRRig.GetComponent<OVRManager>();
                var ovrRig=XRRig.GetComponent<OVRCameraRig>();
                bool trackedHead;
                UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.Head).TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked,out trackedHead);
                Debug.Log($"METAHACKTHONE_WORLD_POSE level={(Workshop.Downhill?"Downhill":"Workshop")} head={headAnchor.position:F4} headYaw={headAnchor.eulerAngles.y:F2} rig={XRRig.position:F4} arena={Workshop.Arena.position:F4} arenaYaw={Workshop.Arena.eulerAngles.y:F2} floor={Workshop.FloorHeight:F4} trackingFloor={(ovrRig?ovrRig.trackingSpace.position.y:XRRig.position.y):F4} surface={Workshop.PlacementSurfaceHeight:F4} origin={(manager?manager.trackingOriginType.ToString():"OpenXR Floor")} headTracked={(manager?OVRPlugin.GetNodePositionTracked(OVRPlugin.Node.EyeCenter):trackedHead)} positionTracking={(manager?manager.usePositionTracking:trackedHead)} moving={Workshop.RepositionRequested} holds={Workshop.World.HasHolds} joints={Workshop.World.Connections.Count}");
            }
            bool driving=Workshop.Session.Mode==WorkshopMode.Drive;
            if(driving!=wasDrive) { ResetGestures(); wasDrive=driving; }
            bool menuActive=menuButtons.Any(b=>b.IsHovered||b.IsSelected);
            SamplePalmMenu();
            SampleLaunchGesture(menuActive);
            for(int i=0;i<2;i++)
            {
                bool left=i==0;
                if(!Workshop.World.HeldPart(i) || hands[i]==null) hands[i]=FindHand(i);
                var hand=hands[i];
                bool systemGesture=systemHands.Any(h=>h && h.GetHand()==(left?OVRPlugin.Hand.HandLeft:OVRPlugin.Hand.HandRight) && h.IsSystemGestureInProgress);
                if(hand==null || !hand.IsConnected || !hand.IsTrackedDataValid || systemGesture || !hand.GetRootPose(out var wrist)) { MissHand(i); continue; }
                if(!hand.GetJointPose(HandJointId.HandIndexTip,out var tip) || !hand.GetJointPose(HandJointId.HandThumbTip,out var thumb)) { MissHand(i); continue; }
                if(Workshop.Downhill && (!hand.IsHighConfidence || !hand.GetFingerIsHighConfidence(HandFinger.Index) || !hand.GetFingerIsHighConfidence(HandFinger.Thumb))) { MissHand(i); continue; }
                if(Workshop.Downhill) {
                    if(sampleVersions[i]==hand.CurrentDataVersion){grips[i].Sample(false,0,false,default,null,false,Time.unscaledTime);continue;}
                    sampleVersions[i]=hand.CurrentDataVersion;
                    if(grips[i].NeedsFullGripConfidence && (!hand.GetFingerIsHighConfidence(HandFinger.Middle)||!hand.GetFingerIsHighConfidence(HandFinger.Ring)||!hand.GetFingerIsHighConfidence(HandFinger.Pinky))){MissHand(i);continue;}
                }
                var grabPose=new Pose((tip.position+thumb.position)*.5f,wrist.rotation);
                float tipDistance=Vector3.Distance(tip.position,thumb.position)/Mathf.Max(.1f,hand.Scale);
                float fullGrip=ReadFullGrip(hand);
                bool pinch=hand.GetIndexFingerIsPinching() || tipDistance<.04f;
                bool modeGesture=false;
                if(Workshop.Downhill && Workshop.Downhill.SampleWheel(i,true,tipDistance,hand.GetIndexFingerIsPinching(),0,grabPose,!menuActive&&!Workshop.RepositionRequested,Time.unscaledTime))
                { SetCommand(i,HandCommand.None); continue; }
                if(Workshop.ModeTarget!=null && Workshop.ModeTarget.SupportsModeGesture)
                {
                    var challenge=Workshop.ModeTarget; bool wasArmed=challenge.ModeGesture.Armed;
                    bool valid=ReadFingers(hand,i);
                    if(valid && hand.GetJointPose(HandJointId.HandMiddleTip,out var middleTip)) challenge.GestureFeedback.SetHandPose(i,RingPose(hand,HandJointId.HandThumb2,thumb),RingPose(hand,HandJointId.HandIndex2,tip),RingPose(hand,HandJointId.HandMiddle2,middleTip));
                    bool completed=challenge.ModeGesture.Sample(i,valid,fingerState[i],challenge.GestureDirection,challenge.CanSwitchMode&&!menuActive&&!LaunchBlocksInput(),Time.unscaledTime);
                    if(completed)
                    {
                        challenge.TrySwitchMode();
                    }
                    modeGesture=wasArmed||completed||challenge.ModeGesture.Armed;
                }
                if(Workshop.Session.Mode==WorkshopMode.Build)
                {
                    var nearby=FindNearbyPart(grabPose.position);
                    var builder=Workshop.Downhill?Workshop.Downhill.DragBuilder:null;
                    var heldBefore=Workshop.World.HeldPart(i);
                    var samplePose=grabPose;
                    bool rayCandidate=false;
                    if(builder && builder.Active && buildRays[i]) {
                        var ray=buildRays[i].Ray;
                        if(!heldBefore) {
                            rayHolding[i]=false;
                            if(!nearby && !menuActive) {
                                nearby=builder.RayPart(ray,buildRays[i].MaxRayLength,out var hitPoint);
                                if(nearby) { rayCandidate=true; rayDrags[i].Begin(hitPoint,Workshop.Downhill.Course.WorkbenchFrame.up,Quaternion.identity); samplePose=rayDrags[i].Sample(ray); }
                            }
                        } else if(rayHolding[i]) {
                            samplePose=rayDrags[i].Sample(ray);
                            builder.SetAim(i,new SandboxDragBuilder.Aim {ray=ray,maxDistance=buildRays[i].MaxRayLength,angle=Workshop.Downhill.Settings.RaySocketAngle});
                        }
                    }
                    grips[i].Sample(true,tipDistance,hand.GetIndexFingerIsPinching(),samplePose,nearby,!LaunchBlocksInput() && !modeGesture && (!builder || !menuActive) && !Workshop.RepositionRequested && Workshop.World.EditingEnabled,Time.unscaledTime,fullGrip);
                    if(builder && !heldBefore && Workshop.World.HeldPart(i)) rayHolding[i]=rayCandidate;
                    var held=Workshop.World.HeldPart(i); if(held) Workshop.Session.Select(held);
                    SetCommand(i,HandCommand.None);
                }
                else
                {
                    bool valid=ReadFingers(hand,i);
                    var command=gestures[i].Update(valid&&!menuActive&&!pinch,left,fingerState[i],Time.unscaledTime);
                    SetCommand(i,command);
                }
            }
        }
        static float ReadFullGrip(IHand hand)
        {
            if(!hand.GetFingerIsHighConfidence(HandFinger.Thumb) || !hand.GetJointPose(HandJointId.HandThumbTip,out var thumb)
                || !hand.GetJointPose(HandJointId.HandIndex1,out var indexRoot)) return 0;
            float curl=1;
            for(int f=1;f<5;f++) {
                if(!hand.GetFingerIsHighConfidence((HandFinger)f) || !hand.GetJointPose(roots[f],out var root)
                    || !hand.GetJointPose(mids[f],out var middle) || !hand.GetJointPose(tips[f],out var tip)) return 0;
                curl=Mathf.Min(curl,GrabLatch.FingerCurl(root.position,middle.position,tip.position));
            }
            float thumbDistance=Vector3.Distance(thumb.position,indexRoot.position)/Mathf.Max(.1f,hand.Scale);
            return GrabLatch.GripStrength(curl,curl,curl,curl,thumbDistance);
        }
        IHand FindHand(int index) => pointerPinches[index]?pointerPinches[index].Hand:sources.FirstOrDefault(h=>h && h.isActiveAndEnabled && h.Handedness==(index==0?Handedness.Left:Handedness.Right) && h.IsTrackedDataValid);
        bool LaunchBlocksInput() => Workshop.IsChallenge && (launchBlockedFrame || Workshop.Challenge.IsAiming || Workshop.Challenge.LaunchGesture.BlocksInput);
        void SamplePalmMenu()
        {
            var menu=Workshop.Downhill?Workshop.Downhill.PalmMenu:null;
            if(!menu || !headAnchor) return;
            var hand=FindHand(0);
            bool suppressed=systemHands.Any(h=>h && h.IsSystemGestureInProgress);
            bool valid=hand!=null && hand.IsConnected && hand.IsTrackedDataValid && hand.IsHighConfidence;
            int extended=0;
            Vector3 palm=default, normal=default;
            if(valid && hand.GetRootPose(out var wrist)
                && hand.GetJointPose(HandJointId.HandIndex1,out var index)
                && hand.GetJointPose(HandJointId.HandPinky1,out var pinky)
                && hand.GetJointPose(HandJointId.HandMiddle1,out var middle)) {
                palm=Vector3.Lerp(wrist.position,middle.position,.5f);
                normal=Vector3.Cross(index.position-wrist.position,pinky.position-wrist.position).normalized;
                // Palm opening does not depend on thumb confidence or on all four
                // fingers being visible. Count only confidently extended fingers.
                for(int f=1;f<5;f++)
                    if(hand.GetFingerIsHighConfidence((HandFinger)f) && hand.GetJointPose(roots[f],out var root)
                        && hand.GetJointPose(mids[f],out var mid) && hand.GetJointPose(tips[f],out var tip)
                        && Vector3.Dot((mid.position-root.position).normalized,(tip.position-mid.position).normalized)>ExtendedDot) extended++;
            } else valid=false;
            menu.Sample(valid,palm,normal,headAnchor.position,Time.unscaledDeltaTime,extended,suppressed);
        }
        public bool IsRightPoke(int identifier)
        {
            foreach(var poke in XRRig.GetComponentsInChildren<Oculus.Interaction.PokeInteractor>(true)) {
                if(poke.Identifier!=identifier) continue;
                var left=FindHand(0); var right=FindHand(1);
                if(right==null || !right.IsTrackedDataValid || !right.GetJointPose(HandJointId.HandIndexTip,out var tip)) return false;
                float distance=(tip.position-poke.Origin).sqrMagnitude;
                return distance<.01f && (left==null || !left.IsTrackedDataValid || !left.GetJointPose(HandJointId.HandIndexTip,out var other) || distance<(other.position-poke.Origin).sqrMagnitude);
            }
            return false;
        }
        void SampleLaunchGesture(bool menuActive)
        {
            launchBlockedFrame=false;
            if(!Workshop.IsChallenge) return;
            var challenge=Workshop.Challenge; var gesture=challenge.LaunchGesture;
            launchBlockedFrame=gesture.Reserved;
            for(int i=0;i<2;i++)
            {
                launchSamples[i]=default;
                var hand=FindHand(i);
                bool system=systemHands.Any(h=>h && h.GetHand()==(i==0?OVRPlugin.Hand.HandLeft:OVRPlugin.Hand.HandRight) && h.IsSystemGestureInProgress);
                if(system || hand==null || !hand.IsConnected || !hand.IsTrackedDataValid || !ReadFingers(hand,i) ||
                    !hand.GetJointPose(HandJointId.HandIndex1,out var root) || !hand.GetJointPose(HandJointId.HandIndexTip,out var tip) ||
                    !hand.GetJointPose(HandJointId.HandThumbTip,out var thumb)) continue;
                launchDirections[i]=tip.position-root.position;
                launchSamples[i]=new LaunchGesture.HandSample { Valid=launchDirections[i].sqrMagnitude>.000001f,
                    Fingers=fingerState[i], TipDistance=Vector3.Distance(tip.position,thumb.position)/Mathf.Max(.1f,hand.Scale) };
            }
            var change=gesture.Sample(launchSamples[0],launchSamples[1],challenge.CanAim && (gesture.Reserved || !menuActive),Time.unscaledTime);
            launchBlockedFrame |= gesture.Reserved || change!=LaunchGesture.Change.None;
            if(change==LaunchGesture.Change.Activated && !challenge.BeginAim()) challenge.CancelAim();
            if(change==LaunchGesture.Change.Cancelled) { challenge.CancelAim(); Workshop.Session.Notify("Aim cancelled. Hold index + thumb out for 1 second to aim."); }
            if(change==LaunchGesture.Change.Fired) { if(!challenge.TryLaunch()) challenge.CancelAim(); }
            if(gesture.Aiming) challenge.UpdateAim(launchDirections[gesture.Owner],Time.unscaledDeltaTime);
            else if(gesture.Reserved) Workshop.Session.Notify($"Hold gun pose to aim: {Mathf.Clamp01(gesture.Progress):P0}");
        }
        static Pose RingPose(IHand hand,HandJointId middle,Pose tip)
        {
            if(!hand.GetJointPose(middle,out var mid) || (tip.position-mid.position).sqrMagnitude<.000001f) return tip;
            return new Pose(Vector3.Lerp(mid.position,tip.position,.65f),Quaternion.LookRotation(tip.position-mid.position));
        }
        AssemblyPart FindNearbyPart(Vector3 point)
        {
            int count=Physics.OverlapSphereNonAlloc(point,GrabRadius,overlaps,~0,QueryTriggerInteraction.Ignore);
            AssemblyPart nearest=null; float best=float.MaxValue;
            for(int c=0;c<count;c++)
            {
                var body=overlaps[c].GetComponentInParent<AssemblyBody>(); if(!body || !body.Part) continue;
                float distance=(overlaps[c].ClosestPoint(point)-point).sqrMagnitude;
                if(distance<best) { best=distance; nearest=body.Part; }
            }
            return nearest;
        }
        public bool PointerOwnsHold(int identifier)
        {
            if(!Workshop || !Workshop.World || !Workshop.Downhill || !Workshop.Downhill.DragBuilder) return false;
            for(int i=0;i<2;i++) if(buildRays[i] && buildRays[i].Identifier==identifier) return Workshop.World.HeldPart(i);
            foreach(var poke in XRRig.GetComponentsInChildren<Oculus.Interaction.PokeInteractor>(true)) {
                if(poke.Identifier!=identifier) continue;
                int owner=-1; float nearest=float.PositiveInfinity;
                for(int i=0;i<2;i++) {
                    var hand=FindHand(i);
                    if(hand!=null && hand.IsTrackedDataValid && hand.GetJointPose(HandJointId.HandIndexTip,out var tip)) {
                        float distance=(tip.position-poke.Origin).sqrMagnitude;
                        if(distance<nearest) { nearest=distance; owner=i; }
                    }
                }
                return owner<0?Workshop.World.HasHolds:Workshop.World.HeldPart(owner);
            }
            return false;
        }
        public bool BlocksMenuInput()
        {
            if(LaunchBlocksInput()) return true;
            if(Workshop.World.HasHolds || Workshop.World.IsBusy) return true;
            if(!XRRig.gameObject.activeInHierarchy || sources==null || Workshop.Session.Mode!=WorkshopMode.Build) return false;
            for(int i=0;i<2;i++)
            {
                var hand=FindHand(i);
                if(hand!=null && hand.IsConnected && hand.IsTrackedDataValid && hand.GetJointPose(HandJointId.HandIndexTip,out var index) && hand.GetJointPose(HandJointId.HandThumbTip,out var thumb)
                    && FindNearbyPart((index.position+thumb.position)*.5f)) return true;
            }
            return false;
        }
        bool ReadFingers(IHand hand,int index)
        {
            for(int f=0;f<5;f++)
            {
                if(!hand.GetFingerIsHighConfidence((HandFinger)f) || !hand.GetJointPose(roots[f],out var root) || !hand.GetJointPose(mids[f],out var mid) || !hand.GetJointPose(tips[f],out var tip)) return false;
                float dot=Vector3.Dot((mid.position-root.position).normalized,(tip.position-mid.position).normalized);
                var bit=(Fingers)(1<<f);
                bool extended=dot>ExtendedDot;
                if(f==0 && hand.GetJointPose(HandJointId.HandIndex1,out var indexRoot)) extended &= Vector3.Distance(tip.position,indexRoot.position)>.035f*hand.Scale;
                if(extended) fingerState[index]|=bit;
                else if(dot<CurledDot || f==0) fingerState[index]&=~bit;
            }
            return true;
        }
        void SetCommand(int index,HandCommand command) { if(index==0) Workshop.Session.Left=command; else Workshop.Session.Right=command; }
        void MissHand(int index) { gestures[index].Reset(); fingerState[index]=Fingers.None; grips[index]?.Sample(false,0,false,default,null,false,Time.unscaledTime); if(Workshop.ModeTarget!=null && Workshop.ModeTarget.SupportsModeGesture) Workshop.ModeTarget.ModeGesture.Sample(index,false,Fingers.None,Workshop.ModeTarget.GestureDirection,Workshop.ModeTarget.CanSwitchMode,Time.unscaledTime); if(Workshop.Downhill) Workshop.Downhill.SampleWheel(index,false,0,false,0,default,false,Time.unscaledTime); SetCommand(index,HandCommand.None); }
        void ResetGestures() { for(int i=0;i<2;i++) { gestures[i].Reset(); fingerState[i]=Fingers.None; SetCommand(i,HandCommand.None); } }
        void OnDisable() { if(Workshop && Workshop.World) { if(Workshop.IsChallenge) Workshop.Challenge.CancelAim(); if(Workshop.Downhill) Workshop.Downhill.CancelRotation(); for(int i=0;i<2;i++) grips[i]?.Cancel(); ResetGestures(); } }
    }
}

using System.Linq;
using CloudGarden.ShadowPuzzle;
using Oculus.Interaction.Input;
using PhysicalAssembly.MetaXR;
using UnityEngine;
using UnityEngine.XR;
using Hand = Oculus.Interaction.Input.Hand;

namespace CloudGarden.Integration
{
    // The scene that owns the shadow game: ShadowDemo (the level browser) or Shape of Memory (the game).
    public interface IShadowHost
    {
        bool Busy { get; }
        void Recenter();
        void LoadLevel(string level);
    }

    public sealed class MetaShadowPlayer : ShadowPlayerContext
    {
        public MetaPlayerRig Player;
        // Emberdeep owns its visible rays. Do not depend on an SDK interactor's
        // active pointer object or update event to aim on the portable PICO rig.
        public bool IndependentPortableAim;
        readonly PortableJointAim[] portableAims={new(),new()};
        readonly PortableXRHandInput portableHands=new();
        // The runtime's own aim ray on the portable rig (XR_EXT_hand_interaction); PortableJointAim is its fallback.
        readonly PortableSystemAim systemAim=new();
        // Where a UI ray comes from: the runtime's aim (Meta pointer pose, OpenXR hand-interaction aim) or our joint ray.
        public enum AimSource{None,System,Fallback}
        Oculus.Interaction.TransformTrackingToWorldTransformer portableSpace;
        public IShadowHost Host;
        Hand[] hands;
        OVRHand[] systemHands;
        bool focused = true, paused;
        Oculus.Interaction.RayInteractor[] rays;
        float nextBind;
        bool headWasReady;
        readonly System.Collections.Generic.List<InteractionAimBinding> interactionAims=new();
        void Update()
        {
            if(!Active)return;
            bool ready=HeadReady;
            if(ready!=headWasReady){headWasReady=ready;Debug.Log("SHAPE_OF_MEMORY_HEAD "+HeadState());}
            if(UnityXRRig && IndependentPortableAim){portableHands.Bind();if(!PortableSystemAim.Installed)PortableSystemAim.Install();return;}
            if(Time.unscaledTime<nextBind)return;
            nextBind=Time.unscaledTime+1;
            rays ??= Player.XRRig.GetComponentsInChildren<Oculus.Interaction.RayInteractor>(true);
            foreach(var ray in rays)
            {
                if(!ray) continue;
                MetaPinchSelector.BindToRay(ray);
                var pointer=ray.GetComponentInChildren<Oculus.Interaction.HandPointerPose>(true);
                // On the UnityXR rig the shoulder aim always takes over: Pico's own aim jitters and follows the wrist.
                if(pointer)
                {
                    var shoulder=ShoulderHandAim.Attach(pointer,Player.ViewCamera.transform,UnityXRRig);
                    if(UnityXRRig && shoulder && pointer.Hand!=null && !interactionAims.Any(a=>a.Pointer==pointer))
                        interactionAims.Add(new InteractionAimBinding(this,pointer,shoulder));
                }
            }
        }
        public override bool Active => Player && Player.IsXR;
        // The Pico build's portable UnityXR rig rather than Meta's OVR rig.
        public bool UnityXRRig => Player && !Player.XRRig.GetComponent<OVRCameraRig>();
        // Hands the rig tracks. The UnityXR rig also carries a static prefab hand (HandSphereMap) that always reads as
        // valid, which must never stand in for a hand that lost tracking.
        Hand[] Hands => hands ??= Player.XRRig.GetComponentsInChildren<Hand>(true).Where(h => !h.GetComponent<FromHandPrefabDataSource>()).ToArray();
        public Hand TrackedHand(bool left) => Active && HeadReady ? Tracked(left) : null;
        // The rig's hand rays (not its controller rays) and the hand each follows.
        public System.Collections.Generic.IEnumerable<(Oculus.Interaction.RayInteractor ray, IHand hand)> HandRays()
        {
            if (!Active) yield break;
            rays ??= Player.XRRig.GetComponentsInChildren<Oculus.Interaction.RayInteractor>(true);
            foreach (var ray in rays)
            {
                var pointer = ray ? ray.GetComponentInChildren<Oculus.Interaction.HandPointerPose>(true) : null;
                if (pointer && pointer.Hand != null) yield return (ray, pointer.Hand);
            }
        }
        Hand Tracked(bool left) => Hands.FirstOrDefault(h => h && h.isActiveAndEnabled && h.IsConnected && h.IsTrackedDataValid
            && h.Handedness == (left ? Handedness.Left : Handedness.Right));
        // The UnityXR rig has no OVRHand. Its runtime (Pico) reports the system gesture (palm up and pinch: the system
        // menu) in the hand-aim flags instead, and that pinch belongs to the system, not the orb.
        readonly bool[] systemGesture = new bool[2];
        bool InSystemGesture(bool left)
        {
            var aim = left ? UnityEngine.XR.Hands.MetaAimHand.left : UnityEngine.XR.Hands.MetaAimHand.right;
            bool now = aim != null && aim.added && ((UnityEngine.XR.Hands.MetaAimFlags)(ulong)(uint)aim.aimFlags.ReadValue() & UnityEngine.XR.Hands.MetaAimFlags.SystemGesture) != 0;
            int i = left ? 0 : 1;
            if (now != systemGesture[i]) { systemGesture[i] = now; Debug.Log($"SHAPE_OF_MEMORY_SYSTEM_GESTURE {(left ? "Left" : "Right")} {(now ? "start" : "end")}"); }
            return now;
        }
        // One line per hand ray for device logs: tracking, ray state, whose aim, which selector.
        public string Report()
        {
            if (rays == null) return "rays=unbound";
            var text = new System.Text.StringBuilder(HeadState()).Append(' ');
            foreach (var ray in rays)
            {
                if (!ray) continue;
                var pointer = ray.GetComponentInChildren<Oculus.Interaction.HandPointerPose>(true);
                if (!pointer || pointer.Hand == null) continue;
                var aim = pointer.GetComponent<ShoulderHandAim>();
                text.Append($"{pointer.Hand.Handedness}:tracked={pointer.Hand.IsTrackedDataValid},ray={ray.State},aim={(aim && aim.Overriding ? "shoulder" : "runtime")},select={(ray.GetComponentInChildren<MetaPinchSelector>(true) ? "distance" : "sdk")} ");
            }
            return text.ToString();
        }
        HeadSignal tracking, presence;
        public override bool HeadReady
        {
            get
            {
                if (!focused || paused || !Player) return false;
                if (!Active) return true;
                var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                return head.isValid
                    && tracking.Allows(head.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked), tracked)
                    && presence.Allows(head.TryGetFeatureValue(CommonUsages.userPresence, out bool present), present);
            }
        }
        // What HeadReady is made of, for device logs.
        public string HeadState()
        {
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            string Read(InputFeatureUsage<bool> usage, HeadSignal signal) =>
                head.TryGetFeatureValue(usage, out bool value) ? value + (signal.Seen ? "" : "(unproven)") : "none";
            return $"ready={HeadReady} focus={focused}/{Application.isFocused} head={head.isValid} tracked={Read(CommonUsages.isTracked, tracking)} present={Read(CommonUsages.userPresence, presence)}";
        }
        public override bool ReadHand(bool left, out Pose palm, out Vector3 tip, out float pinchDistance)
            =>ReadHandPose(left,out palm,out tip,out pinchDistance);
        // Where a menu hand ray comes from: for the drawn line and the EMBERDEEP_AIM device log.
        public struct MenuAimDetail
        {
            // The portable (PICO, UnityXR) rig rather than Meta's OVR rig.
            public bool Portable;
            // System = the runtime's aim, used as is (Portable: lightly smoothed); Fallback = PortableJointAim.
            public AimSource Source;
            // The thumb-index web (虎口): midpoint of the index and thumb knuckles, unfiltered.
            public bool WebValid;public Vector3 Web;
            // Fallback only: how the joint ray's direction was blended this frame.
            public PortableJointAim.Detail Blend;
            // System only, logs: the runtime's aim before any smoothing, and its tracking flags (Portable).
            public bool RuntimeAimValid;public Pose RuntimeAim;public int RuntimeAimFlags;
            // SystemSelect: clicks follow the runtime's select (Meta: index pinch; PICO: aim_activate_ext) rather than
            // the thumb-index tip distance. Select = its strength 0..1, SelectPressed = its closed state,
            // SelectReady = the runtime's readiness (PICO aim_activate ready; Meta index high confidence), logs only.
            public bool SystemSelect,SelectPressed,SelectReady;public float Select;
        }
        // UI pointing and pinch availability are separate: an obscured fingertip
        // must stop clicks without discarding a still-tracked wrist/knuckle ray.
        public bool ReadMenuHand(bool left,out Pose palm,out Pose aim,out float pinch,out bool pinchAvailable)
            =>ReadMenuHand(left,out palm,out aim,out pinch,out pinchAvailable,out _);
        public bool ReadMenuHand(bool left,out Pose palm,out Pose aim,out float pinch,out bool pinchAvailable,out MenuAimDetail detail)
        {
            palm=aim=default;pinch=float.PositiveInfinity;pinchAvailable=false;detail=default;
            if(!ReadInteractionAim(left,out aim,out detail.Source))return false;
            if(UnityXRRig && IndependentPortableAim)
            {
                detail.Portable=true;
                if(ReadPortable(left,out var sample))
                {
                    palm=sample.Wrist;pinchAvailable=sample.PinchAvailable;
                    if(pinchAvailable)pinch=sample.Pinch;
                    detail.WebValid=true;detail.Web=PortableJointAim.Web(sample.IndexBase,sample.ThumbBase);
                }
                // The system ray needs no joints; without them the wrist is unknown and only the system select clicks.
                else if(detail.Source==AimSource.System)palm=aim;
                else return false;
                if(detail.Source==AimSource.Fallback)detail.Blend=portableAims[left?0:1].Last;
                else if(ReadSystemAim(left,out var system)==PortableSystemAim.Report.Tracked) // this frame's cached reading
                {
                    detail.RuntimeAimValid=true;detail.RuntimeAim=system.RawAim;detail.RuntimeAimFlags=system.TrackingState;
                    detail.SystemSelect=system.SelectAvailable && system.SelectProven;
                    detail.SelectPressed=system.SelectPressed;detail.SelectReady=system.SelectReady;detail.Select=system.Select;
                }
                return true;
            }
            var hand=Tracked(left);
            if(!hand || !hand.GetRootPose(out palm) || !float.IsFinite(palm.position.sqrMagnitude))return false;
            pinchAvailable=TryReadPinch(hand,out pinch);
            // Same knuckles as the portable path (OpenXR IndexProximal/ThumbProximal).
            if(hand.GetJointPose(HandJointId.HandIndex1,out var index) && hand.GetJointPose(HandJointId.HandThumb2,out var thumb))
            {detail.Web=PortableJointAim.Web(index.position,thumb.position);detail.WebValid=float.IsFinite(detail.Web.sqrMagnitude);}
            if(detail.Source==AimSource.System)
            {
                // Meta's runtime pinch (OVRHand.GetFingerIsPinching / GetFingerPinchStrength via ISDK): the same select
                // the system UI uses with this pointer pose. Its threshold belongs to the runtime.
                detail.RuntimeAimValid=true;detail.RuntimeAim=aim;
                detail.SystemSelect=true;detail.SelectPressed=hand.GetIndexFingerIsPinching();
                detail.Select=Mathf.Clamp01(hand.GetFingerPinchStrength(Oculus.Interaction.Input.HandFinger.Index));
                detail.SelectReady=hand.GetFingerIsHighConfidence(Oculus.Interaction.Input.HandFinger.Index);
                if(!float.IsFinite(detail.Select))detail.SystemSelect=false;
            }
            return true;
        }
        // The portable rig's system aim this frame (cached per frame, so the ray and the menu detail agree).
        PortableSystemAim.Report ReadSystemAim(bool left,out PortableSystemAim.Reading reading)
        {
            if(!portableSpace)portableSpace=Player.XRRig.GetComponentInChildren<Oculus.Interaction.TransformTrackingToWorldTransformer>(true);
            return systemAim.Read(left,portableSpace?portableSpace.Transform:null,Time.frameCount,Time.unscaledDeltaTime,out reading);
        }
        bool ReadPortable(bool left,out PortableXRHandInput.Sample sample)
        {
            if(!portableSpace)portableSpace=Player.XRRig.GetComponentInChildren<Oculus.Interaction.TransformTrackingToWorldTransformer>(true);
            return portableHands.TryRead(left,portableSpace?portableSpace.Transform:null,out sample);
        }
        public bool TryReadPortableGrip(bool left,out float strength,out Vector3 contact)
        {
            strength=0;contact=default;
            if(!UnityXRRig || !IndependentPortableAim || !HeadReady || !Active || SystemOwnsHand(left)
                || !ReadPortable(left,out var sample) || !sample.GripAvailable)return false;
            strength=sample.GripStrength;contact=sample.GripContact;return true;
        }
        // Raw native sample for gesture spells: per-finger curls and joint points.
        public bool TryReadPortableSample(bool left,out PortableXRHandInput.Sample sample)
        {
            sample=default;
            return UsesPortableGrip && HeadReady && Active && !SystemOwnsHand(left) && ReadPortable(left,out sample);
        }
        public bool IsPortablePalmOpen(bool left)=>UsesPortableGrip && HeadReady && Active && !SystemOwnsHand(left)
            && ReadPortable(left,out var sample) && sample.OpenPalm;
        public bool UsesPortableGrip=>UnityXRRig && IndependentPortableAim;
        static readonly HandJointId[] fingerTips={HandJointId.HandThumbTip,HandJointId.HandIndexTip,HandJointId.HandMiddleTip,HandJointId.HandRingTip,HandJointId.HandPinkyTip};
        // Independent finger validity: a touch does not require a full right-hand fist sample.
        public bool TryReadFingertip(bool left,int finger,out Vector3 point)
        {
            point=default;
            if(finger<0 || finger>=5 || !HeadReady || !Active || !Player.XRRig.gameObject.activeInHierarchy || SystemOwnsHand(left))return false;
            if(UsesPortableGrip)return ReadPortable(left,out var sample) && sample.TryTip(finger,out point);
            var hand=Tracked(left);
            if(!hand || !hand.IsConnected || !hand.IsTrackedDataValid || !hand.GetFingerIsHighConfidence((Oculus.Interaction.Input.HandFinger)finger)
                || !hand.GetJointPose(fingerTips[finger],out var pose))return false;
            point=pose.position;return float.IsFinite(point.sqrMagnitude);
        }
        public bool TryReadPortableGripFrame(bool left,out Quaternion rotation)
        {
            rotation=Quaternion.identity;
            if(!UsesPortableGrip || !HeadReady || !Active || SystemOwnsHand(left)
                || !ReadPortable(left,out var sample) || !sample.GripAvailable)return false;
            rotation=sample.GripRotation;return true;
        }
        // Object grips do not depend on the hand's UI pointer being available.
        public bool ReadObjectPose(bool left,out Pose palm,out float pinch,out bool pinchAvailable)
        {
            palm=default;pinch=float.PositiveInfinity;pinchAvailable=false;
            if(!HeadReady || !Active || !Player.XRRig.gameObject.activeInHierarchy || SystemOwnsHand(left))return false;
            if(UsesPortableGrip)
            {
                if(!ReadPortable(left,out var native))return false;
                palm=native.Wrist;pinchAvailable=native.PinchAvailable;if(pinchAvailable)pinch=native.Pinch;return true;
            }
            var hand=Tracked(left);if(!TryReadObjectRoot(hand,out palm))return false;
            pinchAvailable=TryReadPinch(hand,out pinch);return true;
        }
        public static bool TryReadObjectRoot(IHand hand,out Pose pose)
        {
            pose=default;
            if(hand==null || !hand.IsConnected || !hand.IsTrackedDataValid || !hand.GetRootPose(out pose))return false;
            var q=pose.rotation;float length=Quaternion.Dot(q,q);
            return float.IsFinite(pose.position.sqrMagnitude) && float.IsFinite(length) && length>.5f && length<1.5f;
        }
        public static bool TryReadPinch(IHand hand,out float pinch)
        {
            pinch=float.PositiveInfinity;
            if(hand==null || !hand.IsConnected || !hand.IsTrackedDataValid
                || !hand.GetJointPose(HandJointId.HandIndexTip,out var index)
                || !hand.GetJointPose(HandJointId.HandThumbTip,out var thumb))return false;
            pinch=Vector3.Distance(index.position,thumb.position);
            return float.IsFinite(pinch);
        }
        public bool HasVisualHand(bool left)
        {
            if(!HeadReady || !Active || !Player.XRRig.gameObject.activeInHierarchy)return false;
            var hand=Tracked(left);
            // Rendering does not require two usable pinch tips. Palm rotation
            // can obscure a fingertip while the SDK still has a valid hand pose.
            return hand && hand.GetRootPose(out var root) && float.IsFinite(root.position.sqrMagnitude);
        }
        bool SystemOwnsHand(bool left)
        {
            systemHands ??= Player.XRRig.GetComponentsInChildren<OVRHand>(true);
            return systemHands.Any(h=>h && h.GetHand()==(left?OVRPlugin.Hand.HandLeft:OVRPlugin.Hand.HandRight)
                && h.IsSystemGestureInProgress) || InSystemGesture(left);
        }
        bool ReadHandPose(bool left,out Pose palm,out Vector3 tip,out float pinchDistance)
        {
            palm = default; tip = default; pinchDistance = float.PositiveInfinity;
            if (!HeadReady || !Active || !Player.XRRig.gameObject.activeInHierarchy) return false;
            if(SystemOwnsHand(left))return false;
            if(UnityXRRig && IndependentPortableAim && (!ReadPortable(left,out var native) || !native.PinchAvailable))return false;
            var hand = Tracked(left);
            if (!hand || !hand.GetRootPose(out var wrist) || !hand.GetJointPose(HandJointId.HandMiddle1, out var middle)
                || !hand.GetJointPose(HandJointId.HandIndexTip, out var index) || !hand.GetJointPose(HandJointId.HandThumbTip, out var thumb)) return false;
            // Meta poses are already in world space. Retain the physical 18/20 mm pinch thresholds.
            palm = new Pose(Vector3.Lerp(wrist.position, middle.position, .5f), wrist.rotation);
            tip = index.position; pinchDistance = Vector3.Distance(index.position, thumb.position);
            return true;
        }
        // Meta's pointer pose is the same ray the system and the menu use, so the beam goes where the hand points.
        public override bool ReadAim(bool left, out Pose aim)
        {
            aim = default;
            if (!HeadReady || !Active || !Player.XRRig.gameObject.activeInHierarchy) return false;
            var hand = Tracked(left);
            return hand && hand.IsPointerPoseValid && hand.GetPointerPose(out aim);
        }
        // The UI ray. Meta's OVR rig: the runtime's pointer pose. Emberdeep's portable rig: the runtime's
        // XR_EXT_hand_interaction aim when it reports one, else PortableJointAim from the joints. Older shared scenes on
        // the UnityXR rig keep their filtered SDK pointer binding. None of them uses stale tracking.
        public bool ReadInteractionAim(bool left,out Pose aim)=>ReadInteractionAim(left,out aim,out _);
        public bool ReadInteractionAim(bool left,out Pose aim,out AimSource source)
        {
            aim=default;source=AimSource.None;
            if(!HeadReady || !Active || !Player.XRRig.gameObject.activeInHierarchy)
            {InvalidateInteractionAims();return false;}
            if(SystemOwnsHand(left)){portableAims[left?0:1].Reset();systemAim.Reset(left);return false;}
            if(UnityXRRig && IndependentPortableAim)
            {
                var filter=portableAims[left?0:1];
                switch(ReadSystemAim(left,out var system))
                {
                    case PortableSystemAim.Report.Tracked:filter.Reset();aim=system.Aim;source=AimSource.System;return true;
                    // The runtime has aimed this hand and now says it cannot: no ray, as with Meta's invalid pointer.
                    case PortableSystemAim.Report.Lost:filter.Reset();return false;
                }
                if(!ReadPortable(left,out var sample)){filter.Reset();return false;}
                // Missing tips relax aiming stiffness; they never become an
                // open pinch sample. ReadMenuHand reports PinchAvailable=false.
                if(!filter.TryRead(Time.frameCount,Player.ViewCamera.transform,left,sample.Wrist.position,sample.IndexBase,
                    sample.ThumbBase,sample.PinchAvailable?sample.Pinch:.08f,Time.unscaledDeltaTime,out aim))return false;
                source=AimSource.Fallback;return true;
            }
            if(!UnityXRRig)
            {
                if(!ReadAim(left,out aim))return false;
                source=AimSource.System;return true;
            }
            var handedness=left?Handedness.Left:Handedness.Right;
            foreach(var binding in interactionAims)
            {
                if(binding.Hand.Handedness!=handedness)continue;
                if(binding.TryRead(Time.frameCount,out aim)){source=AimSource.Fallback;return true;}
            }
            return false;
        }
        sealed class InteractionAimBinding : System.IDisposable
        {
            public readonly Oculus.Interaction.HandPointerPose Pointer;
            public readonly IHand Hand;
            readonly MetaShadowPlayer owner;
            readonly ShoulderHandAim shoulder;
            InteractionAimFreshness freshness;
            public InteractionAimBinding(MetaShadowPlayer owner,Oculus.Interaction.HandPointerPose pointer,ShoulderHandAim shoulder)
            {
                this.owner=owner;Pointer=pointer;Hand=pointer.Hand;this.shoulder=shoulder;
                // Subscription order does not affect reading: record freshness here,
                // then read the final transform in Game.Update after the hand event.
                Hand.WhenHandUpdated+=Updated;
            }
            bool Available()=>owner.HeadReady && !owner.InSystemGesture(Hand.Handedness==Handedness.Left)
                && Pointer && Pointer.isActiveAndEnabled && shoulder && shoulder.isActiveAndEnabled && shoulder.Force
                && !(Hand is Oculus.Interaction.Input.Hand data && data.GetComponent<FromHandPrefabDataSource>())
                && Hand.IsConnected && Hand.IsTrackedDataValid
                && Hand.GetJointPose(HandJointId.HandIndex1,out _) && Hand.GetJointPose(HandJointId.HandThumb2,out _);
            void Updated()=>freshness.Record(Time.frameCount,Available());
            public bool TryRead(int frame,out Pose aim)
            {
                bool available=Available() && shoulder.Overriding;
                var pose=available?new Pose(Pointer.transform.position,Pointer.transform.rotation):default;
                return freshness.TryRead(frame,available,pose,out aim);
            }
            public void Invalidate()=>freshness.Invalidate();
            public void Dispose(){Hand.WhenHandUpdated-=Updated;freshness.Invalidate();}
        }
        void InvalidateInteractionAims()
        {foreach(var binding in interactionAims)binding.Invalidate();foreach(var aim in portableAims)aim.Reset();portableHands.Invalidate();systemAim.Reset();}
        void OnEnable()=>nextBind=0;
        void OnDisable()
        {
            InvalidateInteractionAims();
            portableHands.Dispose();
            foreach(var binding in interactionAims)binding.Dispose();interactionAims.Clear();nextBind=0;
        }
        public override void Recenter() { if (HeadReady && Host != null && !Host.Busy) Host.Recenter(); }
        public override void SwitchLevel(string level) => Host?.LoadLevel(level);
        void OnApplicationFocus(bool value) {focused=value;InvalidateInteractionAims();}
        void OnApplicationPause(bool value) {paused=value;InvalidateInteractionAims();}
    }

    // Emberdeep's fallback PICO hand ray, for a runtime that reports no system aim (PortableSystemAim). It is driven
    // directly from the current tracked joints and built like a system hand ray.
    // It starts at the thumb-index web (虎口: the midpoint of the index and thumb knuckles) and points along a blend
    // of two directions: a ray from an estimated shoulder through the web (steady, aimed with the arm), and the hand's
    // own axis, wrist to index knuckle (aimed with the wrist, so the arm need not reach out along the line). A pinch
    // barely moves either knuckle. Only in the last centimetre or so before a pinch closes does the direction lag
    // behind the hand, so the pinch cannot dip it off a button; it glides back afterwards. Missing pointer/aim flags
    // cannot invalidate valid joint aiming; system gestures are gated by the caller.
    public sealed class PortableJointAim
    {
        // Shoulder estimate from the head, yaw only: the Meta Interaction SDK's ShoulderEstimatePosition offset
        // (13 cm out, 25 cm down, 13 cm back). ShoulderHandAim's higher shoulder (14 cm down, 4 cm back) made a
        // chest-height hand aim low, so the arm had to be pushed out level with the button.
        public const float ShoulderOut=.13f,ShoulderDrop=.25f,ShoulderBack=.13f;
        // Share of the hand's own axis in the direction: 0 = shoulder ray only, 1 = hand axis only.
        public const float HandBlend=.5f;
        // The hand axis counts fully within this angle of the shoulder ray, fading to nothing at the second
        // (a sharply bent wrist must not swing the ray).
        public const float HandTrustFull=50f,HandTrustNone=90f;
        // Anti-dip hold: the pinch span (index tip to thumb tip) where it starts and where it is full, the direction's
        // time constant at full hold, and how long the hold takes to let go once the pinch opens again.
        // Clicks close at ShadowPinch.CloseDistance (18 mm); an open or pinch-ready hand above HoldFrom is never held.
        public const float HoldFrom=.035f,HoldFull=.022f,HoldSeconds=.3f,HoldRelease=.15f;
        // Origin: light filtering, so the drawn line stays on the web while the hand moves. Direction: heavy at rest,
        // light in a sweep.
        readonly OneEuro origin=new(5f,10f),direction=new(.8f,1.5f);
        Vector3 lastDirection;
        float held;
        int lastFrame=-1;
        Pose cached;
        // How the latest direction was made, for device logs.
        public struct Detail
        {
            public float HandWeight,ShoulderHandAngle,Hold;
            public Vector3 Shoulder,Hand;
        }
        public Detail Last{get;private set;}
        public void Reset(){origin.Reset();direction.Reset();lastDirection=Vector3.zero;held=0;lastFrame=-1;Last=default;}
        // Without a wrist joint the ray is the shoulder ray alone.
        public bool TryRead(int frame,Transform head,bool left,Vector3 index,Vector3 thumb,float pinch,float dt,out Pose aim)
            =>Read(frame,head,left,false,default,index,thumb,pinch,dt,out aim);
        public bool TryRead(int frame,Transform head,bool left,Vector3 wrist,Vector3 index,Vector3 thumb,float pinch,float dt,out Pose aim)
            =>Read(frame,head,left,true,wrist,index,thumb,pinch,dt,out aim);
        bool Read(int frame,Transform head,bool left,bool hasWrist,Vector3 wrist,Vector3 index,Vector3 thumb,float pinch,float dt,out Pose aim)
        {
            aim=default;
            if(frame<0 || !head || !Finite(head.position) || !Finite(head.forward) || !Finite(index) || !Finite(thumb)
                || (hasWrist && !Finite(wrist)) || !float.IsFinite(pinch) || pinch<0 || !float.IsFinite(dt) || dt<=0)
            {Reset();return false;}
            if(frame==lastFrame){aim=cached;return true;}
            if(frame<lastFrame || frame-lastFrame>1)Reset();
            dt=Mathf.Clamp(dt,.0001f,.05f);
            var at=origin.Filter(Web(index,thumb),dt);
            var shoulder=ShoulderRay(head,left,at);
            var detail=new Detail{Shoulder=shoulder,Hand=shoulder};
            var wanted=shoulder;
            if(hasWrist && (index-wrist).sqrMagnitude>1e-6f)
            {
                var hand=(index-wrist).normalized;float angle=Vector3.Angle(shoulder,hand);
                float weight=HandBlend*Mathf.InverseLerp(HandTrustNone,HandTrustFull,angle);
                wanted=Vector3.Slerp(shoulder,hand,weight).normalized;
                detail.Hand=hand;detail.ShoulderHandAngle=angle;detail.HandWeight=weight;
            }
            var filtered=direction.Filter(wanted,dt);if(filtered.sqrMagnitude<1e-6f)filtered=wanted;filtered.Normalize();
            float hold=Mathf.InverseLerp(HoldFrom,HoldFull,pinch);
            held=hold>=held?hold:Mathf.Max(hold,held-dt/HoldRelease);
            // Per-frame share of the way to the filtered direction: all of it with no hold, the HoldSeconds time
            // constant at full hold, and a smooth glide in between (no one-frame catch-up as the hold lets go).
            float follow=Mathf.Lerp(1,1-Mathf.Exp(-dt/HoldSeconds),held);
            lastDirection=lastDirection.sqrMagnitude>0?Vector3.Slerp(lastDirection,filtered,follow).normalized:filtered;
            detail.Hold=held;Last=detail;
            cached=new Pose(at,Quaternion.LookRotation(lastDirection,Vector3.up));lastFrame=frame;aim=cached;return true;
        }
        // The thumb-index web: midpoint of the index knuckle (IndexProximal / HandIndex1) and the thumb knuckle
        // (ThumbProximal / HandThumb2).
        public static Vector3 Web(Vector3 index,Vector3 thumb)=>Vector3.Lerp(index,thumb,.5f);
        public static Vector3 Shoulder(Transform head,bool left)
        {
            var flat=Heading(head);var right=Vector3.Cross(Vector3.up,flat);
            return head.position+Vector3.down*ShoulderDrop+right*(left?-ShoulderOut:ShoulderOut)-flat*ShoulderBack;
        }
        // From the estimated shoulder through the hand.
        public static Vector3 ShoulderRay(Transform head,bool left,Vector3 hand)
        {
            var direction=hand-Shoulder(head,left);
            return direction.sqrMagnitude<1e-6f?Heading(head):direction.normalized;
        }
        // The head's yaw as a level unit vector.
        static Vector3 Heading(Transform head)
        {
            var flat=Vector3.ProjectOnPlane(head.forward,Vector3.up);
            // Looking straight down (or up), the head's up (or down) axis still points where the body faces.
            if(flat.sqrMagnitude<1e-4f)flat=Vector3.ProjectOnPlane(head.forward.y<0?head.up:-head.up,Vector3.up);
            return flat.sqrMagnitude<1e-4f?Vector3.forward:flat.normalized;
        }
        static bool Finite(Vector3 value)=>float.IsFinite(value.x)&&float.IsFinite(value.y)&&float.IsFinite(value.z);
    }

    // A new hand-ray binding has no usable pose until a real hand update arrives.
    // Allow one previous frame because script Update order can put gameplay before
    // the XR hand update, but never reuse a pose after tracking loss or a pause.
    public struct InteractionAimFreshness
    {
        bool recorded;
        int updatedFrame;
        public void Record(int frame,bool valid)
        {recorded=valid && frame>=0;updatedFrame=frame;}
        public void Invalidate()=>recorded=false;
        public bool TryRead(int frame,bool available,Pose finalPose,out Pose aim)
        {
            aim=default;
            if(!available){Invalidate();return false;}
            if(!recorded || frame<updatedFrame || frame-updatedFrame>1)return false;
            var p=finalPose.position;var q=finalPose.rotation;
            if(!Finite(p.x) || !Finite(p.y) || !Finite(p.z) || !Finite(q.x) || !Finite(q.y) || !Finite(q.z) || !Finite(q.w))return false;
            float norm=q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w;
            if(norm<.5f || norm>1.5f)return false;
            aim=finalPose;return true;
        }
        static bool Finite(float value)=>!float.IsNaN(value) && !float.IsInfinity(value);
    }

    // A head signal (tracked, user present) holds the hands back only once the runtime has reported it true. A runtime
    // can offer a signal and never set it (XR_EXT_user_presence, say), which would otherwise draw no hands and drop
    // every press for the whole session.
    public struct HeadSignal
    {
        public bool Seen { get; private set; }
        public bool Allows(bool readable, bool value)
        {
            if (!readable) return true;
            if (value) Seen = true;
            return value || !Seen;
        }
    }
}

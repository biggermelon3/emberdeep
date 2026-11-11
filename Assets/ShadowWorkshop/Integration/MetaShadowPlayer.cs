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
            if(UnityXRRig && IndependentPortableAim){portableHands.Bind();return;}
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
        // UI pointing and pinch availability are separate: an obscured fingertip
        // must stop clicks without discarding a still-tracked wrist/knuckle ray.
        public bool ReadMenuHand(bool left,out Pose palm,out Pose aim,out float pinch,out bool pinchAvailable)
        {
            palm=aim=default;pinch=float.PositiveInfinity;pinchAvailable=false;
            if(!ReadInteractionAim(left,out aim))return false;
            if(UnityXRRig && IndependentPortableAim)
            {
                if(!ReadPortable(left,out var sample))return false;
                palm=sample.Wrist;pinchAvailable=sample.PinchAvailable;
                if(pinchAvailable)pinch=sample.Pinch;
                return true;
            }
            var hand=Tracked(left);
            if(!hand || !hand.GetRootPose(out palm) || !float.IsFinite(palm.position.sqrMagnitude))return false;
            pinchAvailable=TryReadPinch(hand,out pinch);
            return true;
        }
        bool ReadPortable(bool left,out PortableXRHandInput.Sample sample)
        {
            if(!portableSpace)portableSpace=Player.XRRig.GetComponentInChildren<Oculus.Interaction.TransformTrackingToWorldTransformer>(true);
            return portableHands.TryRead(left,portableSpace?portableSpace.Transform:null,out sample);
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
        // Emberdeep's portable rig owns a joint-driven ray; older shared scenes
        // retain their filtered SDK pointer binding. Neither uses stale tracking.
        public bool ReadInteractionAim(bool left,out Pose aim)
        {
            aim=default;
            if(!HeadReady || !Active || !Player.XRRig.gameObject.activeInHierarchy)
            {InvalidateInteractionAims();return false;}
            if(SystemOwnsHand(left)){portableAims[left?0:1].Reset();return false;}
            if(UnityXRRig && IndependentPortableAim)
            {
                var filter=portableAims[left?0:1];
                if(!ReadPortable(left,out var sample)){filter.Reset();return false;}
                // Missing tips relax aiming stiffness; they never become an
                // open pinch sample. ReadMenuHand reports PinchAvailable=false.
                return filter.TryRead(Time.frameCount,Player.ViewCamera.transform,left,sample.IndexBase,sample.ThumbBase,
                    sample.PinchAvailable?sample.Pinch:.08f,Time.unscaledDeltaTime,out aim);
            }
            if(!UnityXRRig)return ReadAim(left,out aim);
            var handedness=left?Handedness.Left:Handedness.Right;
            foreach(var binding in interactionAims)
            {
                if(binding.Hand.Handedness!=handedness)continue;
                if(binding.TryRead(Time.frameCount,out aim))return true;
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
        {foreach(var binding in interactionAims)binding.Invalidate();foreach(var aim in portableAims)aim.Reset();portableHands.Invalidate();}
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

    // Driven directly from the current tracked joints. Missing pointer/aim flags
    // cannot invalidate valid joint aiming; system gestures are gated by the caller.
    public sealed class PortableJointAim
    {
        readonly OneEuro origin=new(1.2f,3),direction=new(.8f,1.5f);
        Vector3 lastDirection;
        int lastFrame=-1;
        Pose cached;
        public void Reset(){origin.Reset();direction.Reset();lastDirection=Vector3.zero;lastFrame=-1;}
        public bool TryRead(int frame,Transform head,bool left,Vector3 index,Vector3 thumb,float pinch,float dt,out Pose aim)
        {
            aim=default;
            if(frame<0 || !head || !Finite(head.position) || !Finite(head.forward) || !Finite(index) || !Finite(thumb)
                || !float.IsFinite(pinch) || pinch<0 || !float.IsFinite(dt) || dt<=0)
            {Reset();return false;}
            if(frame==lastFrame){aim=cached;return true;}
            if(frame<lastFrame || frame-lastFrame>1)Reset();
            dt=Mathf.Clamp(dt,.0001f,.05f);
            var at=origin.Filter(Vector3.Lerp(index,thumb,.5f),dt);
            var wanted=ShoulderHandAim.Pointing(head,left,at);
            var filtered=direction.Filter(wanted,dt).normalized;
            float hold=Mathf.InverseLerp(.065f,.03f,pinch);
            float follow=1-Mathf.Pow(.96f*hold,dt*72f);
            lastDirection=lastDirection.sqrMagnitude>0?Vector3.Slerp(lastDirection,filtered,follow).normalized:filtered;
            cached=new Pose(at,Quaternion.LookRotation(lastDirection,Vector3.up));lastFrame=frame;aim=cached;return true;
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

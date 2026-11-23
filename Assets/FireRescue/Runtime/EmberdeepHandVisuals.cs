using System.Collections.Generic;
using CloudGarden.Integration;
using Oculus.Interaction;
using Oculus.Interaction.DistanceReticles;
using Oculus.Interaction.Input;
using UnityEngine;

namespace FireRescue
{
    // Reuses the workshop rig's skinned HandVisual and tracked bones. This is a
    // rendering adapter, not a physical hand solver or an additional input system.
    // Explicit interior boxes clip only pixels inside solid gameplay geometry.
    [DefaultExecutionOrder(10000)]
    public sealed class EmberdeepHandVisuals : MonoBehaviour
    {
        public const int MaxOcclusionBoxes=12;
        static readonly int MatricesId=Shader.PropertyToID("_HandClipWorldToLocal");
        static readonly int ExtentsId=Shader.PropertyToID("_HandClipHalfSize");
        struct Box { public Transform Frame; public Vector3 Center,HalfSize; }
        sealed class Visual
        {
            public HandVisual Hand;
            public SkinnedMeshRenderer[] Renderers;
            public Material[][] Materials;
            public bool[] RenderingOff;
            public EmberdeepHandPresentation Presentation;
            public Transform Cuff;
        }
        struct HiddenRenderer { public Renderer Renderer; public bool ForcedOff; }
        struct DisabledVisual { public Behaviour Component; public bool Enabled; }
        readonly Box[] boxes=new Box[MaxOcclusionBoxes];
        readonly Matrix4x4[] matrices=new Matrix4x4[MaxOcclusionBoxes];
        readonly Vector4[] extents=new Vector4[MaxOcclusionBoxes];
        readonly List<Visual> visuals=new();
        readonly List<HiddenRenderer> nativeRays=new();
        readonly List<DisabledVisual> nativeVisuals=new();
        MetaShadowPlayer provider;
        Transform rig;
        Material material;
        bool focused=true,paused;
        struct InteractionPose {public EmberdeepHandPoseKind Kind;public Transform Anchor;public float Amount;}
        readonly InteractionPose[] interactionPoses=new InteractionPose[2];
        public EmberdeepHandContact Contacts {get;}=new();
        public Material RuntimeMaterial=>material;
        public int BoundVisualCount=>visuals.Count;
        public int CuffCount {get{int count=0;foreach(var item in visuals)if(item.Cuff)count++;return count;}}
        public int VisibleCuffCount {get{int count=0;foreach(var item in visuals)if(item.Cuff && item.Cuff.gameObject.activeSelf)count++;return count;}}
        public int FrozenHandCount
        {get{int count=0;foreach(var item in visuals)if(item.Presentation!=null && item.Presentation.Frozen)count++;return count;}}
        public int HiddenHandCount
        {get{int count=0;foreach(var item in visuals)if(item.Presentation!=null && item.Presentation.Hidden)count++;return count;}}
        // Tests and tools: the runtime's own tracking for a hand chain (EmberdeepHandSight.RawTracked by default).
        public System.Func<IHand,bool> RawTracking {get;set;}

        // Whether hand `left` (0 left, 1 right in gameplay) is in view for INPUT this frame: the chain gives a hand and the
        // runtime itself tracks it, with EmberdeepSightGate's short grace (the ISDK chain bridges a lost hand at its
        // last pose, EmberdeepHandSight). Drawing uses the raw state and EmberdeepLostHand's freeze instead.
        public bool Seen(bool left)
        {
            int i=left?0:1;
            if(sightFrame[i]==Time.frameCount)return sightGates[i].Seen;
            sightFrame[i]=Time.frameCount;
            IHand hand=null;
            // Not the UnityXR rig's static prefab hand, which always reads as tracked.
            foreach(var item in visuals)
                if(item.Hand && item.Hand.Hand!=null && (item.Hand.Hand.Handedness==Handedness.Left)==left
                    && !(item.Hand.Hand is Component c && c.GetComponent<FromHandPrefabDataSource>())){hand=item.Hand.Hand;break;}
            bool present=hand!=null && hand.IsConnected && hand.IsTrackedDataValid;
            return sightGates[i].Step(present,present && Raw(hand,out _),Time.unscaledDeltaTime);
        }
        readonly EmberdeepSightGate[] sightGates=new EmberdeepSightGate[2];
        readonly int[] sightFrame={-1,-1};
        readonly EmberdeepLostHand.State[] loggedState=new EmberdeepLostHand.State[2];
        bool Raw(IHand hand,out bool confident)
        {
            if(RawTracking!=null){confident=true;return RawTracking(hand);}
            return EmberdeepHandSight.RawTracked(hand,out confident);
        }

        // The pose is visual feedback only. Hand indices match existing gameplay:
        // 0 left, 1 right. Call after ownership arbitration, before this LateUpdate.
        public void SetInteractionPose(int hand,EmberdeepHandPoseKind kind,Transform anchor,float amount=0)
        {
            if(hand<0 || hand>1)return;
            interactionPoses[hand]=new InteractionPose{Kind=kind,Anchor=anchor,Amount=float.IsFinite(amount)?Mathf.Clamp01(amount):0};
        }
        public void ClearInteractionPoses(){interactionPoses[0]=default;interactionPoses[1]=default;}

        public void Configure(MetaShadowPlayer source)
        {
            ReleaseBindings();provider=source;
            if(!source || !source.Player || !source.Player.XRRig)return;
            rig=source.Player.XRRig;
            var shader=Resources.Load<Shader>("EmberdeepTrackedHand");
            if(!shader){Debug.LogError("Emberdeep tracked-hand shader missing.",this);return;}
            material=new Material(shader){name="Emberdeep tracked hands (scene instance)"};
            var art=EmberdeepArtLibrary.Load();
            // Reuse the existing SDK mesh and selected skeleton. Display copies
            // contain transforms/renderers only, never another hand/input provider.
            foreach(var hand in rig.GetComponentsInChildren<HandVisual>(true))
            {
                // Distance-grab reticles contain manual SyntheticHands too. They
                // can report valid data while intentionally holding an old pose.
                // Their drawer owns ForceOffVisibility; never select them as the
                // player's tracked hand or overwrite their materials/visibility.
                if(hand.GetComponentInParent<ReticleGhostDrawer>(true))
                {
                    foreach(var renderer in hand.GetComponentsInChildren<Renderer>(true))HideNativeRenderer(renderer);
                    continue;
                }
                var renderers=hand.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var item=new Visual{Hand=hand,Renderers=renderers,Materials=new Material[renderers.Length][],RenderingOff=new bool[renderers.Length]};
                for(int i=0;i<renderers.Length;i++)
                {
                    item.RenderingOff[i]=renderers[i].forceRenderingOff;
                    item.Materials[i]=renderers[i].sharedMaterials;
                    var replacement=new Material[item.Materials[i].Length];
                    for(int slot=0;slot<replacement.Length;slot++)replacement[slot]=material;
                    renderers[i].sharedMaterials=replacement;
                }
                // Quest's OVR skeleton is the mesh's own; the portable UnityXR (PICO) rig reports the
                // runtime's skeleton at real hand size, so its palm is fitted to the tracked knuckles.
                item.Presentation=new EmberdeepHandPresentation(hand,material,!hand.GetComponentInParent<OVRCameraRig>(true));
                // Leather-strapped fleece cuff from the Blender kit closes the open wrist of the SDK mesh.
                item.Cuff=art?art.Create("GloveCuff",transform,Vector3.zero):null;
                if(item.Cuff)
                {
                    item.Cuff.name="Guardian glove cuff";item.Cuff.gameObject.SetActive(false);
                    foreach(var r in item.Cuff.GetComponentsInChildren<Renderer>(true))
                    {
                        r.gameObject.layer=hand.gameObject.layer;r.receiveShadows=false;
                        // The sleeve is the same suede as the hand, lit by the same shader.
                        if(r.name.StartsWith("Sleeve")){var mats=r.sharedMaterials;for(int m=0;m<mats.Length;m++)mats[m]=material;r.sharedMaterials=mats;}
                    }
                }
                visuals.Add(item);
            }
            SuppressNativeRayVisuals(rig);
            ApplyOcclusion();UpdateVisibility();
        }

        // Call with an INSCRIBED solid volume, never a whole-tool renderer AABB:
        // the latter would erase fingers legitimately occupying a handle opening.
        // Frame scale/rotation are supported; inactive/destroyed frames auto-clear.
        public bool SetOcclusionBox(int slot,Transform frame,Vector3 localCenter,Vector3 halfSize)
        {
            if(slot<0 || slot>=MaxOcclusionBoxes)return false;
            if(!frame || !Finite(localCenter) || !Finite(halfSize) || halfSize.x<=0 || halfSize.y<=0 || halfSize.z<=0)
            {boxes[slot]=default;return false;}
            boxes[slot]=new Box{Frame=frame,Center=localCenter,HalfSize=halfSize};return true;
        }
        public void ClearOcclusionBox(int slot){if(slot>=0 && slot<MaxOcclusionBoxes)boxes[slot]=default;}
        public void ClearOcclusionBoxes(){for(int i=0;i<MaxOcclusionBoxes;i++)boxes[i]=default;}
        public bool ContainsOcclusionPoint(Vector3 world)
        {
            if(!Finite(world))return false;
            for(int i=0;i<MaxOcclusionBoxes;i++)
            {
                var box=boxes[i];if(!Active(box.Frame))continue;
                var local=box.Frame.InverseTransformPoint(world)-box.Center;
                if(Mathf.Abs(local.x)<box.HalfSize.x && Mathf.Abs(local.y)<box.HalfSize.y && Mathf.Abs(local.z)<box.HalfSize.z)return true;
            }
            return false;
        }
        public void ApplyOcclusion()
        {
            if(!material)return;
            for(int i=0;i<MaxOcclusionBoxes;i++)
            {
                var box=boxes[i];bool active=Active(box.Frame);
                matrices[i]=active?Matrix4x4.Translate(-box.Center)*box.Frame.worldToLocalMatrix:Matrix4x4.identity;
                extents[i]=active?new Vector4(box.HalfSize.x,box.HalfSize.y,box.HalfSize.z,1):Vector4.zero;
            }
            material.SetMatrixArray(MatricesId,matrices);material.SetVectorArray(ExtentsId,extents);
        }
        public bool TryDisplayPalm(bool left,out Pose pose)
        {
            pose=default;
            foreach(var item in visuals)
            {
                if(!item.Hand || item.Hand.Hand==null || (item.Hand.Hand.Handedness==Handedness.Left)!=left || item.Presentation==null || !item.Presentation.HasPose)continue;
                var wrist=item.Presentation.DisplayJoint(HandJointId.HandWristRoot);var middle=item.Presentation.DisplayJoint(HandJointId.HandMiddle1);
                if(!wrist || !middle)continue;
                var distal=middle.position-wrist.position;var palmar=wrist.rotation*(left?Constants.LeftPalmar:Constants.RightPalmar);
                if(Vector3.Cross(distal,palmar).sqrMagnitude<.000001f)continue;
                pose=new Pose((wrist.position+middle.position)*.5f,Quaternion.LookRotation(distal,palmar));return true;
            }
            return false;
        }
        void LateUpdate(){ApplyOcclusion();Contacts.Prepare();UpdateVisibility();}
        void UpdateVisibility()
        {
            bool available=focused && !paused && provider && provider.Active && provider.HeadReady;
            // A system-owned pinch blocks actions, not otherwise valid joint
            // rendering. If the SDK really loses/hides a hand, Present still freezes it.
            bool left=available && provider.HasVisualHand(true);
            bool right=available && provider.HasVisualHand(false);
            foreach(var item in visuals)
            {
                var visual=item.Hand;if(!visual)continue;
                var hand=visual.Hand;
                bool isLeft=hand!=null && hand.Handedness==Handedness.Left;
                bool staticPose=hand is Component component && component.GetComponent<FromHandPrefabDataSource>();
                bool bridged=visual.isActiveAndEnabled && hand!=null && hand.IsConnected && hand.IsTrackedDataValid
                    && !staticPose && (isLeft?left:right);
                // "Valid" must mean tracked now: the chain's LastKnownGoodHand keeps a lost hand valid at its last pose.
                bool confident=false;
                bool raw=hand!=null && Raw(hand,out confident);
                bool show=bridged && raw;
                var pose=interactionPoses[isLeft?0:1];
                item.Presentation?.Present(show,pose.Kind,pose.Anchor,pose.Amount,Time.unscaledDeltaTime);
                if(item.Presentation!=null && item.Presentation.State!=loggedState[isLeft?0:1])
                {
                    loggedState[isLeft?0:1]=item.Presentation.State;
                    if(EmberdeepHandRayMenu.DeviceLog)Debug.Log("EMBERDEEP_HAND hand="+(isLeft?'L':'R')+" state="+item.Presentation.State.ToString().ToLowerInvariant()
                        +" bridged="+(bridged?1:0)+" raw_tracked="+(raw?1:0)+" raw_confident="+(confident?1:0));
                }
                if(show)item.Presentation?.Constrain(Contacts,pose.Kind!=EmberdeepHandPoseKind.Free);
                // SDK owns renderer.enabled and HandVisual.ForceOffVisibility.
                // Its source geometry is masked while the render-only copy is
                // used. SDK updates and visibility decisions remain untouched.
                for(int i=0;i<item.Renderers.Length;i++)if(item.Renderers[i])
                    item.Renderers[i].forceRenderingOff=item.RenderingOff[i] || !show || (item.Presentation!=null && item.Presentation.SkinCount>0);
                if(item.Cuff)
                {
                    bool worn=item.Presentation!=null && item.Presentation.Showing;
                    if(item.Cuff.gameObject.activeSelf!=worn)item.Cuff.gameObject.SetActive(worn);
                    // A held snapshot keeps its cuff where the hand froze; a hidden (lost) hand hides it with the glove.
                    if(worn && !item.Presentation.Frozen && item.Presentation.TryGetWristFrame(out var wrist))
                    {
                        item.Cuff.SetPositionAndRotation(wrist.position,wrist.rotation);
                        var root=item.Presentation.DisplayRoot;if(root)item.Cuff.localScale=Vector3.one*Mathf.Abs(root.lossyScale.x)*item.Presentation.PalmScale/Mathf.Max(1e-5f,Mathf.Abs(transform.lossyScale.x));
                    }
                }
            }
            foreach(var entry in nativeRays)if(entry.Renderer)entry.Renderer.forceRenderingOff=true;
        }
        void SuppressNativeRayVisuals(Transform root)
        {
            foreach(var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if(!(component is RayInteractorRayVisual) && !(component is RayInteractorCursorVisual)
                    && !(component is HandRayInteractorCursorVisual) && !(component is ControllerRayVisual)
                    && !(component is RayInteractorPinchVisual))continue;
                nativeVisuals.Add(new DisabledVisual{Component=component,Enabled=component.enabled});component.enabled=false;
                // PinchVisual also drives a separate skinned arrow mesh. Stopping
                // its writer without hiding that mesh leaves a frozen indicator.
                foreach(var renderer in component.GetComponentsInChildren<Renderer>(true))
                {
                    var hand=renderer.GetComponentInParent<HandVisual>();
                    if(hand && !hand.GetComponentInParent<ReticleGhostDrawer>(true))continue;
                    HideNativeRenderer(renderer);
                }
            }
        }
        void HideNativeRenderer(Renderer renderer)
        {
            foreach(var saved in nativeRays)if(saved.Renderer==renderer)return;
            nativeRays.Add(new HiddenRenderer{Renderer=renderer,ForcedOff=renderer.forceRenderingOff});renderer.forceRenderingOff=true;
        }
        void OnApplicationFocus(bool value){focused=value;UpdateVisibility();}
        void OnApplicationPause(bool value){paused=value;UpdateVisibility();}
        void OnDisable()=>ReleaseBindings();
        void OnEnable(){if(provider)Configure(provider);}
        void OnDestroy()=>ReleaseBindings();
        void ReleaseBindings()
        {
            foreach(var item in visuals)
            {
                item.Presentation?.Dispose();
                if(item.Cuff){item.Cuff.gameObject.SetActive(false);if(Application.isPlaying)Destroy(item.Cuff.gameObject);else DestroyImmediate(item.Cuff.gameObject);}
                for(int i=0;i<item.Renderers.Length;i++)if(item.Renderers[i])
                {item.Renderers[i].sharedMaterials=item.Materials[i];item.Renderers[i].forceRenderingOff=item.RenderingOff[i];}
            }
            visuals.Clear();
            foreach(var item in nativeRays)if(item.Renderer)item.Renderer.forceRenderingOff=item.ForcedOff;
            foreach(var item in nativeVisuals)if(item.Component)item.Component.enabled=item.Enabled;
            nativeRays.Clear();nativeVisuals.Clear();rig=null;
            ClearInteractionPoses();
            if(material){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);material=null;}
        }
        static bool Active(Transform frame)
        {
            if(!frame || !frame.gameObject.activeInHierarchy)return false;
            var scale=frame.lossyScale;
            return Finite(scale) && Mathf.Abs(scale.x)>1e-6f && Mathf.Abs(scale.y)>1e-6f && Mathf.Abs(scale.z)>1e-6f;
        }
        static bool Finite(Vector3 v)=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z);
    }
}

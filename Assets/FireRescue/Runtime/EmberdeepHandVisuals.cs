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
        public Material RuntimeMaterial=>material;
        public int BoundVisualCount=>visuals.Count;
        public int FrozenHandCount
        {get{int count=0;foreach(var item in visuals)if(item.Presentation!=null && item.Presentation.Frozen)count++;return count;}}

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
                item.Presentation=new EmberdeepHandPresentation(hand,material);
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
        void LateUpdate(){ApplyOcclusion();UpdateVisibility();}
        void UpdateVisibility()
        {
            bool available=focused && !paused && provider && provider.Active && provider.HeadReady;
            bool left=available && provider.ReadHand(true,out _,out _,out _);
            bool right=available && provider.ReadHand(false,out _,out _,out _);
            foreach(var item in visuals)
            {
                var visual=item.Hand;if(!visual)continue;
                var hand=visual.Hand;
                bool isLeft=hand!=null && hand.Handedness==Handedness.Left;
                bool staticPose=hand is Component component && component.GetComponent<FromHandPrefabDataSource>();
                bool show=visual.isActiveAndEnabled && hand!=null && hand.IsConnected && hand.IsTrackedDataValid
                    && !staticPose && (isLeft?left:right);
                var pose=interactionPoses[isLeft?0:1];
                item.Presentation?.Present(show,pose.Kind,pose.Anchor,pose.Amount,Time.unscaledDeltaTime);
                // SDK owns renderer.enabled and HandVisual.ForceOffVisibility.
                // Its source geometry is masked while the render-only copy is
                // used. SDK updates and visibility decisions remain untouched.
                for(int i=0;i<item.Renderers.Length;i++)if(item.Renderers[i])
                    item.Renderers[i].forceRenderingOff=item.RenderingOff[i] || !show || (item.Presentation!=null && item.Presentation.SkinCount>0);
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

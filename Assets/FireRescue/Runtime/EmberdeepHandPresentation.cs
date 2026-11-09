using System;
using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FireRescue
{
    // A transform-only copy: there are deliberately no HandVisual, SyntheticHand,
    // provider, colliders or interaction components in this disposable display.
    // SDK transforms remain entirely untouched, including during snapshot baking.
    public sealed class EmberdeepHandPresentation : IDisposable
    {
        readonly HandVisual source;
        readonly Transform[] originals,copies;
        readonly Transform[] joints;
        readonly Quaternion[] neutral;
        readonly GameObject display;
        readonly Skin[] skins;
        readonly Quaternion[] blended;
        bool hasPose,wasValid,disposed;
        float weight,contactProgress,squeeze;
        Transform contactAnchor;
        EmberdeepHandPoseKind contactKind,poseKind;
        Pose lastContact;
        bool hasContact;
        sealed class Skin
        {
            public SkinnedMeshRenderer Source,Live;
            public MeshRenderer Still;
            public Mesh Mesh;
            public bool OriginallyMasked,Visible;
        }
        public bool Frozen {get;private set;}
        public int SnapshotCount {get;private set;}
        public bool HasPose=>hasPose;
        public Transform DisplayRoot=>display?display.transform:null;
        public int SkinCount=>skins.Length;
        public float ContactWeight=>contactProgress*contactProgress*(3-2*contactProgress);
        public bool ContactLocked=>contactProgress>=.999f && contactAnchor;
        public Transform DisplayJoint(HandJointId joint)=>(int)joint>=0&&(int)joint<joints.Length?joints[(int)joint]:null;
        public Renderer SnapshotRenderer(int index)=>index>=0&&index<skins.Length?skins[index].Still:null;

        public EmberdeepHandPresentation(HandVisual hand,Material material)
        {
            source=hand;
            originals=hand.GetComponentsInChildren<Transform>(true);
            copies=new Transform[originals.Length];
            var mapping=new Dictionary<Transform,Transform>(originals.Length);
            display=new GameObject("Emberdeep hand display (render only)");
            if(hand.gameObject.scene.IsValid())SceneManager.MoveGameObjectToScene(display,hand.gameObject.scene);
            // Keep independent world space so rig recentering cannot move a frozen hand.
            for(int i=0;i<originals.Length;i++)
            {
                var original=originals[i];
                var copy=i==0?display.transform:new GameObject(original.name).transform;
                copy.gameObject.layer=original.gameObject.layer;
                if(i>0)copy.SetParent(mapping.TryGetValue(original.parent,out var parent)?parent:display.transform,false);
                copies[i]=copy;mapping.Add(original,copy);
            }
            CopyTrackedSkeleton();
            joints=new Transform[hand.Joints.Count];neutral=new Quaternion[joints.Length];blended=new Quaternion[joints.Length];
            for(int i=0;i<joints.Length;i++)
            {
                if(hand.Joints[i] && mapping.TryGetValue(hand.Joints[i],out var joint))joints[i]=joint;
                neutral[i]=joints[i]?joints[i].localRotation:Quaternion.identity;
            }
            var renderers=hand.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var eligible=new List<Skin>(renderers.Length);
            foreach(var renderer in renderers)
            {
                if(!renderer.sharedMesh || !mapping.TryGetValue(renderer.transform,out var skinTransform))continue;
                var sourceBones=renderer.bones;
                var bones=new Transform[sourceBones.Length];bool complete=true;
                for(int i=0;i<bones.Length;i++)
                    if(sourceBones[i] && !mapping.TryGetValue(sourceBones[i],out bones[i]))complete=false;
                if(!complete)continue;
                var live=skinTransform.gameObject.AddComponent<SkinnedMeshRenderer>();
                live.sharedMesh=renderer.sharedMesh;live.bones=bones;
                if(renderer.rootBone && mapping.TryGetValue(renderer.rootBone,out var rootBone))live.rootBone=rootBone;
                live.localBounds=renderer.localBounds;live.quality=renderer.quality;live.updateWhenOffscreen=true;
                live.shadowCastingMode=ShadowCastingMode.Off;live.receiveShadows=false;
                var materials=new Material[renderer.sharedMaterials.Length];
                for(int i=0;i<materials.Length;i++)materials[i]=material;
                live.sharedMaterials=materials;live.enabled=false;
                var frozen=new GameObject("Emberdeep held hand snapshot");
                frozen.layer=renderer.gameObject.layer;
                if(hand.gameObject.scene.IsValid())SceneManager.MoveGameObjectToScene(frozen,hand.gameObject.scene);
                var mesh=new Mesh{name="Emberdeep reusable hand snapshot"};mesh.MarkDynamic();
                frozen.AddComponent<MeshFilter>().sharedMesh=mesh;
                var still=frozen.AddComponent<MeshRenderer>();still.sharedMaterials=materials;
                still.shadowCastingMode=ShadowCastingMode.Off;still.receiveShadows=false;still.enabled=false;
                eligible.Add(new Skin{Source=renderer,Live=live,Still=still,Mesh=mesh,OriginallyMasked=renderer.forceRenderingOff});
                // Bindposes give a stable neutral skeleton even if Configure runs
                // while the physical hand is already pinching.
                var bindposes=renderer.sharedMesh.bindposes;
                for(int i=0;i<sourceBones.Length && i<bindposes.Length;i++)
                {
                    int joint=Array.IndexOf(joints,bones[i]);if(joint<0 || !sourceBones[i])continue;
                    int parent=Array.IndexOf(sourceBones,sourceBones[i].parent);
                    if(parent>=0 && parent<bindposes.Length)
                        neutral[joint]=(bindposes[parent]*bindposes[i].inverse).rotation;
                }
            }
            skins=eligible.ToArray();
        }

        // appValid includes system-gesture/focus/head-pose gates from the existing
        // provider. sdkVisible also matters: never expose an SDK-hidden live hand.
        public void Present(bool appValid,EmberdeepHandPoseKind kind,Transform anchor,float amount,float deltaTime)
        {
            if(disposed || !source)return;
            bool sdkVisible=false;
            foreach(var skin in skins)
                if(skin.Source && skin.Source.enabled && skin.Source.gameObject.activeInHierarchy && !skin.OriginallyMasked)sdkVisible=true;
            bool valid=appValid && source.isActiveAndEnabled && !source.ForceOffVisibility && sdkVisible;
            if(!valid)
            {
                if(wasValid && hasPose)Freeze();
                wasValid=false;return;
            }
            CopyTrackedSkeleton();
            float dt=float.IsFinite(deltaTime)?Mathf.Clamp(deltaTime,0,.1f):0;
            bool left=source.Hand!=null && source.Hand.Handedness==Handedness.Left;
            bool grip=kind!=EmberdeepHandPoseKind.Free && kind!=EmberdeepHandPoseKind.ButtonPress && anchor;
            bool reachable=false;Pose targetContact=default;
            if(grip && TryGetPalmFrame(out var rawPalm))
            {
                var local=EmberdeepHandPose.GripContact(kind,left);
                targetContact=new Pose(anchor.position+anchor.rotation*local.position,anchor.rotation*local.rotation);
                reachable=Finite(targetContact.position) && Finite(targetContact.rotation)
                    && Vector3.Distance(rawPalm.position,targetContact.position)<=EmberdeepHandPose.MaximumContactReach;
            }
            // An unreachable contact cannot keep an artificial gripping pose. The
            // input owner decides release; this rendering layer only fades back.
            var effectiveKind=grip && !reachable?EmberdeepHandPoseKind.Free:kind;
            if(effectiveKind!=poseKind && effectiveKind!=EmberdeepHandPoseKind.Free){weight=0;poseKind=effectiveKind;}
            weight=Mathf.MoveTowards(weight,effectiveKind==EmberdeepHandPoseKind.Free?0:1,
                dt/(effectiveKind==EmberdeepHandPoseKind.Free?EmberdeepHandPose.ContactReleaseSeconds:EmberdeepHandPose.ContactAcquireSeconds));
            squeeze=Mathf.MoveTowards(squeeze,float.IsFinite(amount)?Mathf.Clamp01(amount):0,dt/.065f);
            float blend=1-Mathf.Exp(-24*dt);
            for(int i=0;i<joints.Length;i++)
            {
                // Wrist/root, forearm, metacarpals and position-only tips
                // remain tracked. Only articulated finger rotations are authored.
                if(!joints[i] || !EmberdeepHandPose.IsArticulated((HandJointId)i))continue;
                var goal=EmberdeepHandPose.JointRotation(effectiveKind,(HandJointId)i,neutral[i],squeeze,left);
                if(!hasPose || weight==0)blended[i]=goal;
                else if(effectiveKind!=EmberdeepHandPoseKind.Free)blended[i]=Quaternion.Slerp(blended[i],goal,blend);
                joints[i].localRotation=Quaternion.Slerp(joints[i].localRotation,blended[i],EmberdeepHandPose.ClosureWeight((HandJointId)i,weight));
            }
            if(reachable)
            {
                if(contactAnchor!=anchor || contactKind!=kind)contactProgress=0;
                contactAnchor=anchor;contactKind=kind;lastContact=targetContact;hasContact=true;
                contactProgress=Mathf.MoveTowards(contactProgress,1,dt/EmberdeepHandPose.ContactAcquireSeconds);
            }
            else
            {
                contactAnchor=null;
                contactProgress=Mathf.MoveTowards(contactProgress,0,dt/EmberdeepHandPose.ContactReleaseSeconds);
            }
            if(hasContact && contactProgress>0)SolvePalmContact(lastContact,ContactWeight);
            else if(anchor && kind==EmberdeepHandPoseKind.ButtonPress)AlignIndexTip(anchor,weight);
            foreach(var skin in skins)
            {
                skin.Visible=skin.Source && skin.Source.enabled && skin.Source.gameObject.activeInHierarchy && !skin.OriginallyMasked;
                skin.Live.enabled=skin.Visible;skin.Still.enabled=false;
            }
            hasPose=true;wasValid=true;Frozen=false;
        }

        void CopyTrackedSkeleton()
        {
            for(int i=0;i<originals.Length;i++)
            {
                if(!originals[i] || !copies[i])continue;
                var a=originals[i];var b=copies[i];
                if(i==0){b.SetPositionAndRotation(a.position,a.rotation);b.localScale=a.lossyScale;}
                else{b.localPosition=a.localPosition;b.localRotation=a.localRotation;b.localScale=a.localScale;}
                if(i>0 && b.gameObject.activeSelf!=a.gameObject.activeSelf)b.gameObject.SetActive(a.gameObject.activeSelf);
            }
        }

        public bool TryGetPalmFrame(out Pose frame)
        {
            frame=default;
            var wrist=DisplayJoint(HandJointId.HandWristRoot);var middle=DisplayJoint(HandJointId.HandMiddle1);
            if(!wrist || !middle)return false;
            var center=(wrist.position+middle.position)*.5f;
            var distal=middle.position-wrist.position;
            bool left=source.Hand!=null && source.Hand.Handedness==Handedness.Left;
            var palmar=wrist.TransformDirection(left?Constants.LeftPalmar:Constants.RightPalmar);
            if(!Finite(center) || distal.sqrMagnitude<=.000001f || Vector3.Cross(distal,palmar).sqrMagnitude<=.000001f)return false;
            frame=new Pose(center,Quaternion.LookRotation(distal,palmar));return true;
        }

        void SolvePalmContact(Pose target,float blend)
        {
            if(!TryGetPalmFrame(out var current))return;
            // Analytic display wrist solve: cancel ALL tracked wrist rotation at
            // full contact, then position the palm on the calibrated grip. No
            // residual 65-degree clamp, provider changes, or camera movement.
            var turn=Quaternion.Slerp(Quaternion.identity,target.rotation*Quaternion.Inverse(current.rotation),blend);
            display.transform.rotation=turn*display.transform.rotation;
            display.transform.position=current.position+turn*(display.transform.position-current.position);
            display.transform.position+=Vector3.ClampMagnitude(target.position-current.position,EmberdeepHandPose.MaximumContactReach)*blend;
        }

        void AlignIndexTip(Transform anchor,float blend)
        {
            var tip=DisplayJoint(HandJointId.HandIndexTip);if(!tip)return;
            // Directly reachable buttons receive a fingertip contact pose. A far
            // ray target cannot pull the visible hand across the play space.
            display.transform.position+=Vector3.ClampMagnitude(anchor.position-tip.position,.08f)*blend;
        }

        void Freeze()
        {
            bool any=false;
            foreach(var skin in skins)
            {
                if(skin.Visible)
                {
                    // This copy still holds the previous APP-valid render pose;
                    // an SDK system gesture arriving this frame cannot alter it.
                    skin.Live.BakeMesh(skin.Mesh,false);
                    skin.Mesh.RecalculateBounds();
                    var bounds=skin.Mesh.bounds;bounds.Expand(.004f);skin.Mesh.bounds=bounds;
                    // The renderer was created with an empty reusable mesh. An
                    // explicit local bound avoids retaining its initial zero AABB.
                    skin.Still.localBounds=bounds;
                    var t=skin.Live.transform;var frozen=skin.Still.transform;
                    frozen.SetPositionAndRotation(t.position,t.rotation);frozen.localScale=t.lossyScale;
                    skin.Still.enabled=true;any=true;
                }
                skin.Live.enabled=false;
            }
            Frozen=any;if(any)SnapshotCount++;
        }

        public void Dispose()
        {
            if(disposed)return;disposed=true;
            foreach(var skin in skins)
            {
                if(skin.Live)skin.Live.enabled=false;
                if(skin.Still){skin.Still.enabled=false;Destroy(skin.Still.gameObject);}
                Destroy(skin.Mesh);
            }
            if(display){display.SetActive(false);Destroy(display);}
            Frozen=false;hasPose=false;
        }
        static void Destroy(UnityEngine.Object value)
        {if(!value)return;if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
        static bool Finite(Vector3 value)=>float.IsFinite(value.x)&&float.IsFinite(value.y)&&float.IsFinite(value.z);
        static bool Finite(Quaternion value)=>float.IsFinite(value.x)&&float.IsFinite(value.y)&&float.IsFinite(value.z)&&float.IsFinite(value.w);
    }
}

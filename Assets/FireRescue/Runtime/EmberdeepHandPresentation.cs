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
        Vector3 safePalm;
        bool hasSafePalm;
        readonly List<Mesh> gloves=new();
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
        // True while the copy is drawn, live or as a held snapshot.
        public bool Showing
        {get{foreach(var skin in skins)if((skin.Live && skin.Live.enabled) || (skin.Still && skin.Still.enabled))return true;return false;}}
        public bool TryGetWristFrame(out Pose frame)
        {
            frame=default;var wrist=DisplayJoint(HandJointId.HandWristRoot);
            if(!wrist || !TryGetPalmFrame(out var palm))return false;
            frame=new Pose(wrist.position,palm.rotation);return true;
        }
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
                live.sharedMesh=GloveMesh(renderer.sharedMesh,bones,hand.Hand!=null && hand.Hand.Handedness==Handedness.Left);live.bones=bones;
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

        Mesh GloveMesh(Mesh source,Transform[] bones,bool left)
        {
            int wrist=-1,middle=-1;
            for(int i=0;i<bones.Length;i++)
            {
                if(!bones[i])continue;
                int joint=Array.IndexOf(joints,bones[i]);
                if(joint==(int)HandJointId.HandWristRoot)wrist=i;else if(joint==(int)HandJointId.HandMiddle1)middle=i;
            }
            var mesh=BakeGlovePanels(source,bones,wrist,middle,left);
            if(mesh!=source)gloves.Add(mesh);
            return mesh;
        }
        // Glove panels baked once from the bind pose: R palm leather, G fingertip caps,
        // B the cream panel on the back of the hand. Alpha 0 marks a baked mesh.
        // Returns the source itself when the mesh cannot be read or the skeleton is unexpected.
        public static Mesh BakeGlovePanels(Mesh source,Transform[] bones,int wrist,int middle,bool left)
        {
            if(!source || !source.isReadable || bones==null)return source;
            var bind=source.bindposes;var cap=new bool[bones.Length];
            for(int i=0;i<bones.Length;i++)
            {
                if(!bones[i])continue;
                // The last phalanx and its tip marker: bones with no grandchildren.
                bool leaf=true;foreach(Transform child in bones[i])if(child.childCount>0)leaf=false;
                cap[i]=leaf && i!=wrist;
            }
            if(wrist<0 || middle<0 || wrist>=bind.Length || middle>=bind.Length)return source;
            var root=bind[wrist].inverse;Vector3 origin=root.GetColumn(3),knuckle=bind[middle].inverse.GetColumn(3);
            var distal=knuckle-origin;float length=distal.magnitude;if(length<.01f)return source;distal/=length;
            var palmar=root.rotation*(left?Constants.LeftPalmar:Constants.RightPalmar);
            palmar=(palmar-distal*Vector3.Dot(palmar,distal)).normalized;var across=Vector3.Cross(distal,palmar);
            var vertices=source.vertices;var normals=source.normals;var weights=source.boneWeights;
            if(normals.Length!=vertices.Length || weights.Length!=vertices.Length)return source;
            var colors=new Color[vertices.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                var p=vertices[i]-origin;float along=Vector3.Dot(p,distal)/length,side=Vector3.Dot(p,across)/length,facing=Vector3.Dot(normals[i],palmar);
                float palm=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.30f,.30f,facing));
                int bone=weights[i].boneIndex0;float tip=bone>=0 && bone<cap.Length && cap[bone]?Mathf.Clamp01(weights[i].weight0*1.6f-.3f):0;
                float dx=side/.43f,dy=(along-.53f)/.50f;
                float panel=facing<.1f?Mathf.Clamp01(1.9f-1.9f*(dx*dx+dy*dy)):0;
                colors[i]=new Color(palm,tip,panel,0);
            }
            var mesh=UnityEngine.Object.Instantiate(source);mesh.name=source.name+" (Emberdeep glove panels)";
            mesh.colors=colors;return mesh;
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
                    && Vector3.Distance(rawPalm.position,targetContact.position)<=EmberdeepHandPose.MaximumContactReach
                    && EmberdeepHandPose.CanAlignPalm(rawPalm.rotation,targetContact.rotation);
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
                // Do not blend toward a stale contact on the opposite side of
                // the wrist, including a sudden tracking-orientation reversal.
                if(hasContact && TryGetPalmFrame(out var releasedPalm)
                    && !EmberdeepHandPose.CanAlignPalm(releasedPalm.rotation,lastContact.rotation))
                {hasContact=false;contactProgress=0;}
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

        public void Constrain(EmberdeepHandContact contacts,bool gripping)
        {
            if(!hasPose || Frozen || contacts==null || !TryGetPalmFrame(out var palm))return;
            if(!gripping)
            {
                var target=hasSafePalm && Vector3.Distance(safePalm,palm.position)<.5f
                    ?contacts.Sweep(safePalm,palm.position,.023f):contacts.Resolve(palm.position,.023f);
                display.transform.position+=target-palm.position;safePalm=target;hasSafePalm=true;
            }
            else {safePalm=palm.position;hasSafePalm=false;}
            // Resolve only display phalanges, retaining the authored grasp palm.
            // Parents precede children in the SDK joint table; repeated passes
            // handle inherited motion without ever writing to the SDK skeleton.
            for(int pass=0;pass<2;pass++)for(int i=0;i<joints.Length;i++)
            {
                var joint=(HandJointId)i;
                if(!joints[i] || joint==HandJointId.HandWristRoot || joint==HandJointId.HandMiddle1
                    || joint==HandJointId.HandIndex1 || joint==HandJointId.HandRing1 || joint==HandJointId.HandPinky1)continue;
                if(!EmberdeepHandPose.IsArticulated(joint) && joint!=HandJointId.HandThumbTip && joint!=HandJointId.HandIndexTip
                    && joint!=HandJointId.HandMiddleTip && joint!=HandJointId.HandRingTip && joint!=HandJointId.HandPinkyTip)continue;
                joints[i].position=contacts.Resolve(joints[i].position,.007f);
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
            // Reject a backwards approach rather than forcing a half-turn to
            // fit a fixed pose. Accepted contacts use quaternion shortest arcs.
            if(!EmberdeepHandPose.CanAlignPalm(current.rotation,target.rotation))return;
            var turn=Quaternion.Slerp(Quaternion.identity,target.rotation*Quaternion.Inverse(current.rotation),blend);
            display.transform.rotation=turn*display.transform.rotation;
            display.transform.position=current.position+turn*(display.transform.position-current.position);
            display.transform.position+=Vector3.ClampMagnitude(target.position-current.position,EmberdeepHandPose.MaximumContactReach)*blend;
        }

        void AlignIndexTip(Transform anchor,float blend)
        {
            var tip=DisplayJoint(HandJointId.HandIndexTip);if(!tip)return;
            if(Vector3.Distance(tip.position,anchor.position)>.18f)return;
            if(TryGetPalmFrame(out var palm))
            {
                var rotation=anchor.rotation*Quaternion.LookRotation(Vector3.forward,Vector3.down);
                if(!EmberdeepHandPose.CanAlignPalm(palm.rotation,rotation))return;
                SolvePalmContact(new Pose(palm.position,rotation),blend);
            }
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
            foreach(var mesh in gloves)Destroy(mesh);gloves.Clear();
            Frozen=false;hasPose=false;
        }
        static void Destroy(UnityEngine.Object value)
        {if(!value)return;if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
        static bool Finite(Vector3 value)=>float.IsFinite(value.x)&&float.IsFinite(value.y)&&float.IsFinite(value.z);
        static bool Finite(Quaternion value)=>float.IsFinite(value.x)&&float.IsFinite(value.y)&&float.IsFinite(value.z)&&float.IsFinite(value.w);
    }
}

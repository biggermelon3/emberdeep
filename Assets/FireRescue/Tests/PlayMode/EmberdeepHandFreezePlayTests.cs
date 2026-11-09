#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using CloudGarden.Integration;
using NUnit.Framework;
using Oculus.Interaction;
using Oculus.Interaction.DistanceReticles;
using Oculus.Interaction.Input;
using PhysicalAssembly.MetaXR;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FireRescue.Tests
{
    // Uses the shipped nested player prefab: four distance-grab ghost hands precede
    // the two live hands. A synthetic one-hand fixture cannot detect this regression.
    public sealed class EmberdeepHandFreezePlayTests
    {
        const string PlayerPath="Assets/CloudGarden/Prefabs/MetaHackathonPlayer.prefab";

        static GameObject ClonePlayer(Transform inactiveParent)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            Assert.NotNull(prefab,"The production player prefab must be exercised");
            return Object.Instantiate(prefab,inactiveParent,false);
        }

        static MetaShadowPlayer Provider(GameObject owner,Transform rig)
        {
            var player=owner.AddComponent<MetaPlayerRig>();player.enabled=false;player.XRRig=rig;
            var provider=owner.AddComponent<MetaShadowPlayer>();provider.enabled=false;provider.Player=player;
            return provider;
        }

        [UnityTest] public IEnumerator ProductionPrefabBindsLiveHandsAndMasksGhostsWithoutChangingTheirMaterials()
        {
            var owner=new GameObject("Production hand binding regression");
            var inactive=new GameObject("Inactive production player fixture");inactive.SetActive(false);
            try
            {
                var clone=ClonePlayer(inactive.transform);
                var player=clone.GetComponentInChildren<MetaPlayerRig>(true);Assert.NotNull(player);
                var hands=player.XRRig.GetComponentsInChildren<HandVisual>(true);
                var ghosts=hands.Where(h=>h.GetComponentInParent<ReticleGhostDrawer>(true)).ToArray();
                var live=hands.Except(ghosts).ToArray();
                Assert.AreEqual(4,ghosts.Length,"The actual distance-hand/controller-reticle nesting must be present");
                Assert.AreEqual(2,live.Length);
                Assert.Contains(hands[0],ghosts,"Regression fixture must preserve the problematic ghost-first traversal");
                var skins=ghosts.SelectMany(h=>h.GetComponentsInChildren<SkinnedMeshRenderer>(true)).ToArray();
                var originalMaterials=skins.Select(s=>s.sharedMaterials).ToArray();
                var originalMasks=skins.Select(s=>s.forceRenderingOff).ToArray();
                foreach(var ghost in ghosts)ghost.ForceOffVisibility=true;
                var provider=owner.AddComponent<MetaShadowPlayer>();provider.enabled=false;provider.Player=player;
                var adapter=owner.AddComponent<EmberdeepHandVisuals>();adapter.Configure(provider);
                Assert.AreEqual(2,adapter.BoundVisualCount,"Reticle pose previews are never player-hand candidates");
                for(int i=0;i<skins.Length;i++)
                {
                    CollectionAssert.AreEqual(originalMaterials[i],skins[i].sharedMaterials);
                    Assert.IsTrue(skins[i].forceRenderingOff,"Reticle geometry must stay hidden even if its drawer changes visibility");
                }
                foreach(var hand in live)
                    foreach(var skin in hand.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        Assert.IsTrue(skin.sharedMaterials.All(m=>m==adapter.RuntimeMaterial));
                // Simulate the SDK drawer changing its own state after Configure.
                ghosts[0].ForceOffVisibility=false;yield return null;
                Assert.IsFalse(ghosts[0].ForceOffVisibility,"The drawer retains ownership of its SDK property");
                Assert.IsTrue(skins[0].forceRenderingOff);
                adapter.enabled=false;
                for(int i=0;i<skins.Length;i++)Assert.AreEqual(originalMasks[i],skins[i].forceRenderingOff);
                Assert.IsFalse(ghosts[0].ForceOffVisibility,"Cleanup cannot overwrite a newer SDK state");
            }
            finally{Object.Destroy(owner);Object.Destroy(inactive);}
            yield return null;
        }

        [UnityTest] public IEnumerator ProductionHandSkeletonContinuesFollowingEventsWhileSdkOwnsVisibility()
        {
            var owner=new GameObject("Live hand pose regression");
            var inactive=new GameObject("Inactive source player");inactive.SetActive(false);
            var isolated=new GameObject("Isolated real SDK hand");isolated.SetActive(false);
            try
            {
                var clone=ClonePlayer(inactive.transform);
                var source=clone.GetComponentsInChildren<HandVisual>(true)
                    .First(h=>!h.GetComponentInParent<ReticleGhostDrawer>(true));
                // Keep the production mesh, both SDK skeletons, joints and bounds;
                // isolate only hardware/interactor behaviours from this deterministic test.
                source.transform.SetParent(isolated.transform,false);
                foreach(var behaviour in source.GetComponentsInChildren<MonoBehaviour>(true))
                    if(behaviour!=source)behaviour.enabled=false;
                var hand=isolated.AddComponent<EmberdeepMovingHandFixture>();hand.Initialize(source);
                source.InjectHand(hand);
                var adapter=owner.AddComponent<EmberdeepHandVisuals>();adapter.Configure(Provider(owner,isolated.transform));
                Assert.AreEqual(1,adapter.BoundVisualCount);
                isolated.SetActive(true);yield return null;
                Assert.IsTrue(source.enabled);Assert.AreSame(hand,source.Hand);
                int updates=0;source.WhenHandVisualUpdated+=()=>updates++;
                hand.Move(new Vector3(.15f,1.1f,.4f),Quaternion.Euler(20,10,5));
                Assert.Greater(updates,0,"SDK HandVisual must remain subscribed to tracking updates");
                Assert.Less(Vector3.Distance(hand.RootPose.position,source.Root.position),.0001f);
                Assert.Less(Quaternion.Angle(hand.FingerRotation,source.Joints[(int)HandJointId.HandIndex1].localRotation),.01f);
                source.ForceOffVisibility=true;yield return null;
                Assert.IsTrue(source.ForceOffVisibility);Assert.IsFalse(source.IsVisible);
                source.ForceOffVisibility=false;yield return null;
                Assert.IsFalse(source.ForceOffVisibility,"Focus masking must not overwrite the SDK visibility decision");
                Assert.IsTrue(source.IsVisible,"SDK renderer.enabled remains independent of Emberdeep's rendering mask");
                int previousUpdates=updates;
                hand.Move(new Vector3(-.12f,1.25f,.55f),Quaternion.Euler(65,-15,8));
                Assert.Greater(updates,previousUpdates);
                Assert.Less(Vector3.Distance(hand.RootPose.position,source.Root.position),.0001f);
                Assert.Less(Quaternion.Angle(hand.FingerRotation,source.Joints[(int)HandJointId.HandIndex1].localRotation),.01f);
                adapter.enabled=false;
                Assert.IsFalse(source.ForceOffVisibility,"Adapter cleanup must preserve the SDK's latest visibility state");
                hand.Move(new Vector3(.2f,.9f,.3f),Quaternion.Euler(35,0,0));
                Assert.Less(Vector3.Distance(hand.RootPose.position,source.Root.position),.0001f);
            }
            finally{Object.Destroy(owner);Object.Destroy(isolated);Object.Destroy(inactive);}
            yield return null;
        }

        [UnityTest] public IEnumerator IndependentPinchSkinnedIndicatorIsHiddenAndRestoredWithoutStoppingInteractor()
        {
            var owner=new GameObject("Pinch indicator regression");
            var rig=new GameObject("Inactive ray fixture");rig.SetActive(false);
            try
            {
                var indicator=new GameObject("SDK independent pinch mesh");indicator.transform.SetParent(rig.transform);
                var skin=indicator.AddComponent<SkinnedMeshRenderer>();
                var visual=indicator.AddComponent<RayInteractorPinchVisual>();
                var interactor=indicator.AddComponent<RayInteractor>();
                var adapter=owner.AddComponent<EmberdeepHandVisuals>();adapter.Configure(Provider(owner,rig.transform));
                Assert.IsFalse(visual.enabled);Assert.IsTrue(interactor.enabled);
                Assert.IsTrue(skin.forceRenderingOff,"Disabling the pose writer alone leaves a frozen skinned indicator");
                adapter.enabled=false;
                Assert.IsTrue(visual.enabled);Assert.IsTrue(interactor.enabled);Assert.IsFalse(skin.forceRenderingOff);
            }
            finally{Object.Destroy(owner);Object.Destroy(rig);}
            yield return null;
        }
    }

    // Supplies only deterministic IHand samples; HandVisual performs the actual
    // production skeleton updates through its normal WhenHandUpdated subscription.
    public sealed class EmberdeepMovingHandFixture : MonoBehaviour,IHand
    {
        Pose[] joints;ReadOnlyHandJointPoses poses;
        public Pose RootPose {get;private set;}
        public Quaternion FingerRotation=>joints[(int)HandJointId.HandIndex1].rotation;
        public void Initialize(HandVisual visual)
        {
            joints=new Pose[Constants.NUM_HAND_JOINTS];
            for(int i=0;i<joints.Length;i++)joints[i]=visual.Joints[i]
                ?new Pose(visual.Joints[i].localPosition,visual.Joints[i].localRotation):Pose.identity;
            poses=new ReadOnlyHandJointPoses(joints);RootPose=Pose.identity;
        }
        public void Move(Vector3 position,Quaternion fingerRotation)
        {
            RootPose=new Pose(position,Quaternion.identity);
            var finger=joints[(int)HandJointId.HandIndex1];finger.rotation=fingerRotation;joints[(int)HandJointId.HandIndex1]=finger;
            CurrentDataVersion++;WhenHandUpdated?.Invoke();
        }
        public Handedness Handedness=>Handedness.Left;
        public bool IsConnected=>true;
        public bool IsHighConfidence=>true;
        public bool IsDominantHand=>false;
        public float Scale=>1;
        public bool IsPointerPoseValid=>true;
        public bool IsTrackedDataValid=>true;
        public int CurrentDataVersion {get;private set;}
        public event Action WhenHandUpdated;
        public bool GetFingerIsPinching(HandFinger finger)=>false;
        public bool GetIndexFingerIsPinching()=>false;
        public bool GetFingerIsHighConfidence(HandFinger finger)=>true;
        public float GetFingerPinchStrength(HandFinger finger)=>0;
        public bool GetRootPose(out Pose pose){pose=RootPose;return true;}
        public bool GetPointerPose(out Pose pose)=>GetRootPose(out pose);
        public bool GetJointPose(HandJointId id,out Pose pose)=>GetJointPoseLocal(id,out pose);
        public bool GetJointPoseLocal(HandJointId id,out Pose pose){pose=joints[(int)id];return true;}
        public bool GetJointPoseFromWrist(HandJointId id,out Pose pose)=>GetJointPoseLocal(id,out pose);
        public bool GetJointPosesLocal(out ReadOnlyHandJointPoses result){result=poses;return true;}
        public bool GetJointPosesFromWrist(out ReadOnlyHandJointPoses result)=>GetJointPosesLocal(out result);
        public bool GetPalmPoseLocal(out Pose pose){pose=Pose.identity;return true;}
    }
}
#endif

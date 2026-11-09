#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Oculus.Interaction;
using Oculus.Interaction.DistanceReticles;
using Oculus.Interaction.Input;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace FireRescue.Tests
{
    public sealed class EmberdeepHandPresentationPlayTests
    {
        [Test] public void EveryInteractionHasDistinctFingerShapeAndTriggerPressAnimatesIndex()
        {
            var kinds=(EmberdeepHandPoseKind[])Enum.GetValues(typeof(EmberdeepHandPoseKind));
            for(int a=1;a<kinds.Length;a++)for(int b=a+1;b<kinds.Length;b++)
            {
                float difference=0;
                for(int f=0;f<5;f++)difference+=Mathf.Abs(EmberdeepHandPose.Curl(kinds[a],(HandFinger)f)-EmberdeepHandPose.Curl(kinds[b],(HandFinger)f));
                Assert.Greater(difference,.05f,$"{kinds[a]} and {kinds[b]} must not be the same generic pinch");
            }
            Assert.Less(EmberdeepHandPose.Curl(EmberdeepHandPoseKind.ButtonPress,HandFinger.Index),.1f);
            Assert.Greater(EmberdeepHandPose.Curl(EmberdeepHandPoseKind.ButtonPress,HandFinger.Middle),.7f);
            Assert.Greater(EmberdeepHandPose.Curl(EmberdeepHandPoseKind.PistolGrip,HandFinger.Index,1),
                EmberdeepHandPose.Curl(EmberdeepHandPoseKind.PistolGrip,HandFinger.Index,0)+.4f);
            Assert.AreEqual(EmberdeepHandPose.Curl(EmberdeepHandPoseKind.ValveGrip,HandFinger.Ring,0),
                EmberdeepHandPose.Curl(EmberdeepHandPoseKind.ValveGrip,HandFinger.Ring,float.NaN));
        }

        [Test] public void JointFlexionFollowsSdkPalmarAxesForBothHandsWithoutMovingWristOrTips()
        {
            var curl=EmberdeepHandPose.JointRotation(EmberdeepHandPoseKind.LeverGrip,HandJointId.HandIndex1,Quaternion.identity);
            Assert.Greater(Vector3.Dot(curl*Constants.RightDistal,Constants.RightPalmar),.5f);
            var leftCurl=EmberdeepHandPose.JointRotation(EmberdeepHandPoseKind.LeverGrip,HandJointId.HandIndex1,Quaternion.identity,0,true);
            Assert.Greater(Vector3.Dot(leftCurl*Constants.LeftDistal,Constants.LeftPalmar),.5f);
            var neutral=Quaternion.Euler(12,23,34);
            Assert.AreEqual(neutral,EmberdeepHandPose.JointRotation(EmberdeepHandPoseKind.CannonGrip,HandJointId.HandWristRoot,neutral));
            Assert.AreEqual(neutral,EmberdeepHandPose.JointRotation(EmberdeepHandPoseKind.CannonGrip,HandJointId.HandIndexTip,neutral));
            foreach(var joint in new[]{HandJointId.HandThumbTip,HandJointId.HandMiddleTip,HandJointId.HandRingTip,HandJointId.HandPinkyTip,HandJointId.HandPinky0})
                Assert.AreEqual(neutral,EmberdeepHandPose.JointRotation(EmberdeepHandPoseKind.CannonGrip,joint,neutral),joint.ToString());
            foreach(var joint in new[]{HandJointId.HandRing1,HandJointId.HandRing2,HandJointId.HandRing3,HandJointId.HandPinky1,HandJointId.HandPinky2,HandJointId.HandPinky3})
                Assert.Greater(Quaternion.Angle(neutral,EmberdeepHandPose.JointRotation(EmberdeepHandPoseKind.LeverGrip,joint,neutral)),40,joint.ToString());
        }

        [Test] public void GripClosureIsProgressiveMonotonicAndContactOffsetsMirror()
        {
            for(int i=0;i<Constants.NUM_HAND_JOINTS;i++)
            {
                var joint=(HandJointId)i;if(!EmberdeepHandPose.IsArticulated(joint))continue;
                float previous=0;
                for(int n=0;n<=20;n++)
                {
                    float closure=EmberdeepHandPose.ClosureWeight(joint,n/20f);
                    Assert.That(closure,Is.InRange(previous,1f),joint.ToString());previous=closure;
                }
                Assert.AreEqual(0,EmberdeepHandPose.ClosureWeight(joint,0));Assert.AreEqual(1,previous);
            }
            Assert.Greater(EmberdeepHandPose.ClosureWeight(HandJointId.HandRing1,.4f),EmberdeepHandPose.ClosureWeight(HandJointId.HandIndex3,.4f));
            foreach(var kind in new[]{EmberdeepHandPoseKind.PistolGrip,EmberdeepHandPoseKind.CannonGrip,
                EmberdeepHandPoseKind.ValveGrip,EmberdeepHandPoseKind.LeverGrip,EmberdeepHandPoseKind.IntakeGrip})
            {
                var left=EmberdeepHandPose.GripContact(kind,true);var right=EmberdeepHandPose.GripContact(kind,false);
                Assert.AreEqual(-left.position.x,right.position.x);Assert.AreEqual(left.position.y,right.position.y);
                Assert.Less(left.position.magnitude,.05f);Assert.Less(right.position.magnitude,.05f);
                Assert.That(Quaternion.Dot(left.rotation,left.rotation),Is.EqualTo(1).Within(.0001f));
            }
        }

        [UnityTest] public IEnumerator BothProductionHandsPoseCopiesWithoutChangingSdkBonesOrInput()
        {
            using(var fixture=new PairFixture())
            {
                yield return null;
                for(int h=0;h<2;h++)
                {
                    fixture.Hands[h].Move(new Vector3(h==0?-.2f:.2f,1.1f,.45f),Quaternion.Euler(7,12,3));
                    var visual=fixture.Visuals[h];var sourceRoot=visual.Root.position;
                    var sourceFinger=visual.Joints[(int)HandJointId.HandIndex1].localRotation;
                    var display=fixture.Displays[h];
                    Assert.Greater(display.SkinCount,0);
                    display.Present(true,EmberdeepHandPoseKind.PistolGrip,null,1,.1f);
                    Assert.IsTrue(display.HasPose);Assert.IsFalse(display.Frozen);
                    Assert.Greater(Quaternion.Angle(sourceFinger,display.DisplayJoint(HandJointId.HandIndex1).localRotation),10);
                    Assert.AreEqual(sourceRoot,visual.Root.position);
                    Assert.AreEqual(sourceFinger,visual.Joints[(int)HandJointId.HandIndex1].localRotation);
                    Assert.AreSame(fixture.Hands[h],visual.Hand);
                    Assert.AreEqual(fixture.Hands[h].RootPose.position,visual.Root.position);
                    Assert.IsFalse(visual.ForceOffVisibility);
                    Assert.IsTrue(visual.IsVisible);
                    Assert.IsEmpty(display.DisplayRoot.GetComponentsInChildren<MonoBehaviour>(true),"Display skeleton cannot become another input stack");
                    display.Present(true,EmberdeepHandPoseKind.Free,null,0,.1f);
                    Assert.Less(Quaternion.Angle(sourceFinger,display.DisplayJoint(HandJointId.HandIndex1).localRotation),.01f);
                }
            }
            yield return null;
        }

        [UnityTest] public IEnumerator InvalidTrackingSnapshotsLastAppPoseOnceAndNeverCreatesOriginHands()
        {
            using(var fixture=new PairFixture())
            {
                yield return null;
                for(int h=0;h<2;h++)
                {
                    var display=fixture.Displays[h];var hand=fixture.Hands[h];
                    display.Present(false,EmberdeepHandPoseKind.Free,null,0,.02f);
                    Assert.IsFalse(display.HasPose);Assert.IsFalse(display.Frozen);Assert.AreEqual(0,display.SnapshotCount);
                    hand.Move(new Vector3(h==0?-.2f:.2f,1.2f,.5f),Quaternion.Euler(15,0,0));
                    display.Present(true,EmberdeepHandPoseKind.IntakeGrip,null,0,.1f);
                    var previous=display.DisplayRoot.position;
                    // The SDK can deliver its system-gesture pose before the app
                    // sees the unavailable flag. That pose must not enter the hold.
                    hand.Move(new Vector3(2,3,4),Quaternion.Euler(80,20,0));
                    fixture.Visuals[h].ForceOffVisibility=true;
                    display.Present(false,EmberdeepHandPoseKind.Free,null,0,.02f);
                    Assert.IsTrue(display.Frozen);Assert.AreEqual(1,display.SnapshotCount);
                    Assert.AreEqual(previous,display.DisplayRoot.position);
                    Renderer snapshot=null;
                    for(int i=0;i<display.SkinCount;i++)if(display.SnapshotRenderer(i).enabled)snapshot=display.SnapshotRenderer(i);
                    Assert.NotNull(snapshot);var position=snapshot.transform.position;var rotation=snapshot.transform.rotation;
                    Assert.Greater(snapshot.GetComponent<MeshFilter>().sharedMesh.vertexCount,0);
                    var frozenMesh=snapshot.GetComponent<MeshFilter>().sharedMesh;
                    foreach(var vertex in frozenMesh.vertices)
                    {
                        Assert.IsTrue(frozenMesh.bounds.Contains(vertex),"Snapshot bounds conservatively contain every baked vertex");
                        Assert.IsTrue(snapshot.localBounds.Contains(vertex),"Renderer cannot retain the initial empty-mesh bounds");
                    }
                    fixture.Isolated.transform.position+=Vector3.one;
                    for(int i=0;i<8;i++)display.Present(false,EmberdeepHandPoseKind.Free,null,0,.02f);
                    Assert.AreEqual(1,display.SnapshotCount,"Unavailable frames reuse the same mesh; no per-frame bake");
                    Assert.AreEqual(position,snapshot.transform.position);Assert.AreEqual(rotation,snapshot.transform.rotation);
                    // App tracking can recover before SDK visibility; hold until both.
                    display.Present(true,EmberdeepHandPoseKind.Free,null,0,.02f);
                    Assert.IsTrue(display.Frozen);Assert.IsTrue(fixture.Visuals[h].ForceOffVisibility);
                    fixture.Visuals[h].ForceOffVisibility=false;
                    hand.Move(new Vector3(.1f,1.2f,.6f),Quaternion.identity);
                    display.Present(true,EmberdeepHandPoseKind.Free,null,0,.1f);
                    Assert.IsFalse(display.Frozen);Assert.IsFalse(snapshot.enabled,"Live and frozen hand must not draw simultaneously");
                }
            }
            yield return null;
        }

        [UnityTest] public IEnumerator GripAnchorChangesOnlyDisplayAndDisposalRemovesEveryOwnedRenderer()
        {
            Renderer[] owned;
            using(var fixture=new PairFixture())
            {
                yield return null;
                var anchor=new GameObject("Display-only grip anchor");
                try
                {
                    var hand=fixture.Hands[0];var visual=fixture.Visuals[0];var display=fixture.Displays[0];
                    hand.Move(new Vector3(-.2f,1,.5f),Quaternion.identity);
                    var wrist=visual.Joints[(int)HandJointId.HandWristRoot];var middle=visual.Joints[(int)HandJointId.HandMiddle1];
                    var center=(wrist.position+middle.position)*.5f;
                    var root=visual.Root.position;var index=visual.Joints[(int)HandJointId.HandIndex1].localRotation;
                    anchor.transform.position=center+Vector3.up*.06f;
                    for(int n=0;n<10;n++)display.Present(true,EmberdeepHandPoseKind.ValveGrip,anchor.transform,.7f,.02f);
                    var copiedCenter=(display.DisplayJoint(HandJointId.HandWristRoot).position+display.DisplayJoint(HandJointId.HandMiddle1).position)*.5f;
                    var contact=EmberdeepHandPose.GripContact(EmberdeepHandPoseKind.ValveGrip,true);
                    Assert.Less(Vector3.Distance(anchor.transform.position+anchor.transform.rotation*contact.position,copiedCenter),.001f);
                    Assert.AreEqual(root,visual.Root.position);Assert.AreEqual(index,visual.Joints[(int)HandJointId.HandIndex1].localRotation);
                    display.Present(false,EmberdeepHandPoseKind.Free,null,0,.02f);
                    owned=display.DisplayRoot.GetComponentsInChildren<Renderer>(true)
                        .Concat(Enumerable.Range(0,display.SkinCount).Select(display.SnapshotRenderer)).ToArray();
                    display.Dispose();
                    Assert.IsFalse(display.Frozen);Assert.IsFalse(display.HasPose);
                    foreach(var renderer in owned)Assert.IsFalse(renderer.enabled);
                    Assert.IsTrue(visual.IsVisible);Assert.IsFalse(visual.ForceOffVisibility);
                }
                finally{Object.Destroy(anchor);}
            }
            yield return null;
            foreach(var renderer in owned)Assert.IsTrue(renderer==null,"Scene/adapter cleanup must dispose snapshot objects as well as skins");
        }

        [UnityTest] public IEnumerator ButtonPressAlignsOnlyDisplayTipAndCapsDistantRayTargetOffset()
        {
            using(var fixture=new PairFixture())
            {
                yield return null;
                var anchor=new GameObject("Button contact");
                try
                {
                    for(int h=0;h<2;h++)
                    {
                        var display=fixture.Displays[h];var visual=fixture.Visuals[h];
                        fixture.Hands[h].Move(new Vector3(h*.2f,1,.4f),Quaternion.Euler(50,0,0));
                        var trackedRoot=visual.Root.position;var trackedIndex=visual.Joints[(int)HandJointId.HandIndex1].localRotation;
                        display.Present(true,EmberdeepHandPoseKind.ButtonPress,null,1,.1f);
                        var tip=display.DisplayJoint(HandJointId.HandIndexTip).position;
                        anchor.transform.position=tip+Vector3.forward*.025f;
                        display.Present(true,EmberdeepHandPoseKind.ButtonPress,anchor.transform,1,.1f);
                        Assert.Less(Vector3.Distance(anchor.transform.position,display.DisplayJoint(HandJointId.HandIndexTip).position),.001f);
                        display.Present(true,EmberdeepHandPoseKind.ButtonPress,null,1,.1f);
                        var initial=display.DisplayRoot.position;
                        anchor.transform.position=display.DisplayJoint(HandJointId.HandIndexTip).position+Vector3.forward*3;
                        display.Present(true,EmberdeepHandPoseKind.ButtonPress,anchor.transform,1,.1f);
                        Assert.That(Vector3.Distance(initial,display.DisplayRoot.position),Is.EqualTo(.08f).Within(.001f));
                        Assert.AreEqual(trackedRoot,visual.Root.position);Assert.AreEqual(trackedIndex,visual.Joints[(int)HandJointId.HandIndex1].localRotation);
                    }
                }
                finally{Object.Destroy(anchor);}
            }
            yield return null;
        }

        [UnityTest] public IEnumerator AnchoredContactCancelsTrackedWristRotationAndFollowsMovingObjectForBothHands()
        {
            using(var fixture=new PairFixture())
            {
                yield return null;
                var anchor=new GameObject("Analytic hand contact");
                try
                {
                    for(int h=0;h<2;h++)foreach(var kind in new[]{EmberdeepHandPoseKind.ExtinguisherGrip,EmberdeepHandPoseKind.PistolGrip,
                        EmberdeepHandPoseKind.CannonGrip,EmberdeepHandPoseKind.LeverGrip,EmberdeepHandPoseKind.ValveGrip,
                        EmberdeepHandPoseKind.CannonPump,EmberdeepHandPoseKind.IntakeGrip})
                    {
                        var display=fixture.Displays[h];var hand=fixture.Hands[h];var source=fixture.Visuals[h];
                        var physicalPosition=new Vector3(h==0?-.15f:.15f,1,.4f);
                        hand.MoveWrist(physicalPosition,Quaternion.identity,Quaternion.Euler(50,0,0));
                        for(int n=0;n<8;n++)display.Present(true,EmberdeepHandPoseKind.Free,null,0,.02f);
                        Assert.IsTrue(display.TryGetPalmFrame(out var raw));
                        anchor.transform.SetPositionAndRotation(raw.position+Vector3.forward*.02f,Quaternion.Euler(12,23,7));
                        // Object mesh scaling cannot shrink the metre-sized contact calibration.
                        anchor.transform.localScale=Vector3.one*.035f;
                        for(int n=0;n<12;n++)display.Present(true,kind,anchor.transform,.4f,.02f);
                        Assert.IsTrue(display.ContactLocked,kind.ToString());
                        var local=EmberdeepHandPose.GripContact(kind,h==0);
                        foreach(var rotation in new[]{Quaternion.Euler(0,155,35),Quaternion.Euler(85,-70,110),Quaternion.Euler(-50,30,-120)})
                        {
                            hand.MoveWrist(physicalPosition,rotation,Quaternion.Euler(55,5,0));
                            var actualRoot=source.Root.position;var actualRotation=source.Root.rotation;
                            var actualIndex=source.Joints[(int)HandJointId.HandIndex1].localRotation;
                            display.Present(true,kind,anchor.transform,.4f,.02f);
                            Assert.IsTrue(display.TryGetPalmFrame(out var locked));
                            Assert.Less(Vector3.Distance(anchor.transform.position+anchor.transform.rotation*local.position,locked.position),.0001f,kind+" palm slips");
                            Assert.Less(Quaternion.Angle(anchor.transform.rotation*local.rotation,locked.rotation),.08f,kind+" retains tracked wrist rotation");
                            Assert.AreEqual(actualRoot,source.Root.position);Assert.AreEqual(actualRotation,source.Root.rotation);
                            Assert.AreEqual(actualIndex,source.Joints[(int)HandJointId.HandIndex1].localRotation);
                        }
                        anchor.transform.position+=new Vector3(.025f,.015f,-.01f);
                        anchor.transform.rotation*=Quaternion.Euler(-30,40,15);
                        display.Present(true,kind,anchor.transform,.7f,.02f);
                        Assert.IsTrue(display.TryGetPalmFrame(out var moved));
                        Assert.Less(Vector3.Distance(anchor.transform.position+anchor.transform.rotation*local.position,moved.position),.0001f);
                        Assert.Less(Quaternion.Angle(anchor.transform.rotation*local.rotation,moved.rotation),.08f,"Animated object drives the contact frame without lag");
                    }
                }
                finally{Object.Destroy(anchor);}
            }
            yield return null;
        }

        [UnityTest] public IEnumerator ContactReleaseBlendsToRealPoseAndUnreachableAnchorCannotDragHand()
        {
            using(var fixture=new PairFixture())
            {
                yield return null;
                var anchor=new GameObject("Reach and release contact");
                try
                {
                    for(int h=0;h<2;h++)
                    {
                        var display=fixture.Displays[h];var hand=fixture.Hands[h];var source=fixture.Visuals[h];
                        hand.MoveWrist(new Vector3(h*.2f,1,.5f),Quaternion.Euler(15,20,5),Quaternion.Euler(10,5,0));
                        display.Present(true,EmberdeepHandPoseKind.Free,null,0,.02f);Assert.IsTrue(display.TryGetPalmFrame(out var raw));
                        anchor.transform.SetPositionAndRotation(raw.position+Vector3.up*.055f,Quaternion.Euler(20,35,45));
                        for(int n=0;n<10;n++)display.Present(true,EmberdeepHandPoseKind.LeverGrip,anchor.transform,0,.02f);
                        Assert.IsTrue(display.ContactLocked);Assert.IsTrue(display.TryGetPalmFrame(out var held));
                        display.Present(true,EmberdeepHandPoseKind.Free,null,0,.02f);
                        Assert.IsFalse(display.ContactLocked);Assert.That(display.ContactWeight,Is.GreaterThan(0).And.LessThan(1));
                        Assert.IsTrue(display.TryGetPalmFrame(out var releasing));
                        Assert.Less(Vector3.Distance(raw.position,releasing.position),Vector3.Distance(raw.position,held.position));
                        Assert.Greater(Vector3.Distance(raw.position,releasing.position),.001f,"Release must not snap directly to tracked pose");
                        for(int n=0;n<8;n++)display.Present(true,EmberdeepHandPoseKind.Free,null,0,.02f);
                        Assert.AreEqual(0,display.ContactWeight);Assert.IsTrue(display.TryGetPalmFrame(out var released));
                        Assert.Less(Vector3.Distance(raw.position,released.position),.0001f);Assert.Less(Quaternion.Angle(raw.rotation,released.rotation),.08f);
                        for(int n=0;n<10;n++)display.Present(true,EmberdeepHandPoseKind.LeverGrip,anchor.transform,0,.02f);
                        anchor.transform.position+=Vector3.right*.6f;
                        for(int n=0;n<10;n++)
                        {
                            display.Present(true,EmberdeepHandPoseKind.LeverGrip,anchor.transform,0,.02f);
                            Assert.IsFalse(display.ContactLocked);Assert.IsTrue(display.TryGetPalmFrame(out var unreachable));
                            Assert.LessOrEqual(Vector3.Distance(raw.position,unreachable.position),EmberdeepHandPose.MaximumContactReach+.0001f);
                        }
                        Assert.AreEqual(0,display.ContactWeight);Assert.IsTrue(display.TryGetPalmFrame(out var returned));
                        Assert.Less(Vector3.Distance(raw.position,returned.position),.0001f);
                        Assert.Less(Quaternion.Angle(source.Joints[(int)HandJointId.HandIndex1].localRotation,display.DisplayJoint(HandJointId.HandIndex1).localRotation),.03f);
                        Assert.AreSame(hand,source.Hand);Assert.IsFalse(source.ForceOffVisibility);
                    }
                }
                finally{Object.Destroy(anchor);}
            }
            yield return null;
        }

        sealed class PairFixture:IDisposable
        {
            readonly GameObject inactive;
            readonly Material material;
            public readonly GameObject Isolated;
            public readonly HandVisual[] Visuals;
            public readonly EmberdeepPresentationHandFixture[] Hands=new EmberdeepPresentationHandFixture[2];
            public readonly EmberdeepHandPresentation[] Displays=new EmberdeepHandPresentation[2];
            public PairFixture()
            {
                inactive=new GameObject("Inactive production source");inactive.SetActive(false);
                var clone=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CloudGarden/Prefabs/MetaHackathonPlayer.prefab"),inactive.transform);
                Visuals=clone.GetComponentsInChildren<HandVisual>(true).Where(h=>!h.GetComponentInParent<ReticleGhostDrawer>(true)).ToArray();
                Assert.AreEqual(2,Visuals.Length);
                Isolated=new GameObject("Isolated two real visual hands");Isolated.SetActive(false);
                material=new Material(Resources.Load<Shader>("EmberdeepTrackedHand"));
                for(int h=0;h<2;h++)
                {
                    var visual=Visuals[h];visual.transform.SetParent(Isolated.transform,false);
                    foreach(var component in visual.GetComponentsInChildren<MonoBehaviour>(true))if(component!=visual)component.enabled=false;
                    Hands[h]=Isolated.AddComponent<EmberdeepPresentationHandFixture>();Hands[h].Initialize(visual,h==0?Handedness.Left:Handedness.Right);
                    visual.InjectHand(Hands[h]);
                }
                Isolated.SetActive(true);
                for(int h=0;h<2;h++)Displays[h]=new EmberdeepHandPresentation(Visuals[h],material);
            }
            public void Dispose()
            {
                foreach(var display in Displays)display?.Dispose();
                Object.Destroy(Isolated);Object.Destroy(inactive);Object.Destroy(material);
            }
        }
    }

    public sealed class EmberdeepPresentationHandFixture:MonoBehaviour,IHand
    {
        Pose[] joints;ReadOnlyHandJointPoses poses;
        public Pose RootPose {get;private set;}
        public Handedness Handedness {get;private set;}
        public void Initialize(HandVisual visual,Handedness handedness)
        {
            Handedness=handedness;joints=new Pose[Constants.NUM_HAND_JOINTS];
            for(int i=0;i<joints.Length;i++)joints[i]=visual.Joints[i]?new Pose(visual.Joints[i].localPosition,visual.Joints[i].localRotation):Pose.identity;
            poses=new ReadOnlyHandJointPoses(joints);RootPose=Pose.identity;
        }
        public void Move(Vector3 position,Quaternion fingerRotation)
        {
            MoveWrist(position,Quaternion.identity,fingerRotation);
        }
        public void MoveWrist(Vector3 position,Quaternion rotation,Quaternion fingerRotation)
        {
            RootPose=new Pose(position,rotation);var finger=joints[(int)HandJointId.HandIndex1];
            finger.rotation=fingerRotation;joints[(int)HandJointId.HandIndex1]=finger;CurrentDataVersion++;WhenHandUpdated?.Invoke();
        }
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

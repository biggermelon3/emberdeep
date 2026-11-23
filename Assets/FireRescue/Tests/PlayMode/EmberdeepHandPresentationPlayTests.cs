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

        // PICO's wrist joint sits elsewhere on the hand than the SDK mesh's: same knuckles, different wrist.
        [UnityTest] public IEnumerator PalmFitSitsMeshPalmUnderTrackedKnucklesAndKeepsFingersTracked()
        {
            using(var fixture=new PairFixture())
            {
                yield return null;
                for(int h=0;h<2;h++)
                {
                    var visual=fixture.Visuals[h];var hand=fixture.Hands[h];bool left=h==0;
                    hand.MoveWrist(new Vector3(left?-.2f:.2f,1.1f,.45f),Quaternion.Euler(0,20,0),hand.Local(HandJointId.HandIndex1).rotation);
                    var fit=new EmberdeepHandPresentation(visual,fixture.Material,true);
                    try
                    {
                        Assert.IsTrue(fit.FitsPalm,"The SDK OpenXR hand must expose wrist, metacarpal and knuckle bindposes");
                        var wristJoint=visual.Joints[(int)HandJointId.HandWristRoot];
                        var restWrist=new Pose(wristJoint.position,wristJoint.rotation);
                        fit.Present(true,EmberdeepHandPoseKind.Free,null,0,.1f);
                        var display=fit.DisplayJoint(HandJointId.HandWristRoot);
                        Assert.Less(Vector3.Distance(restWrist.position,display.position),.0005f,"Mesh skeleton data must fit as itself");
                        Assert.Less(Quaternion.Angle(restWrist.rotation,display.rotation),.5f);
                        Assert.AreEqual(1,fit.PalmScale,.01f);

                        // Move the tracked wrist 12 mm along the hand and 4 mm across, and turn it about the
                        // palm normal; every descendant keeps its world pose.
                        var wrist=hand.Local(HandJointId.HandWristRoot);
                        var moved=new Pose(wrist.position+wrist.rotation*new Vector3(.004f,-.012f,.002f),
                            wrist.rotation*Quaternion.AngleAxis(8,left?Constants.LeftPalmar:Constants.RightPalmar));
                        var inverse=Quaternion.Inverse(moved.rotation);
                        for(int j=0;j<Constants.NUM_HAND_JOINTS;j++)
                        {
                            if(!visual.Joints[j] || visual.Joints[j].parent!=wristJoint)continue;
                            var inRoot=hand.Local((HandJointId)j).GetTransformedBy(wrist);
                            hand.SetLocal((HandJointId)j,new Pose(inverse*(inRoot.position-moved.position),inverse*inRoot.rotation));
                        }
                        hand.SetLocal(HandJointId.HandWristRoot,moved);hand.Publish();
                        Assert.Greater(Vector3.Distance(wristJoint.position,restWrist.position),.01f,"The tracked wrist must have moved");
                        fit.Present(true,EmberdeepHandPoseKind.Free,null,0,.1f);
                        Assert.Less(Vector3.Distance(restWrist.position,display.position),.0005f,"Palm must sit under the knuckles, not on the moved wrist");
                        Assert.Less(Quaternion.Angle(restWrist.rotation,display.rotation),.5f);
                        Assert.AreEqual(1,fit.PalmScale,.01f);
                        for(int j=0;j<Constants.NUM_HAND_JOINTS;j++)
                        {
                            var id=(HandJointId)j;var joint=fit.DisplayJoint(id);
                            if(!joint || id==HandJointId.HandWristRoot || id==HandJointId.HandPalm || id==HandJointId.HandIndex0
                                || id==HandJointId.HandMiddle0 || id==HandJointId.HandRing0 || id==HandJointId.HandPinky0)continue;
                            Assert.Less(Vector3.Distance(visual.Joints[j].position,joint.position),.0001f,id+" must stay on its tracked position");
                            Assert.Less(Quaternion.Angle(visual.Joints[j].rotation,joint.rotation),.1f,id+" must keep its tracked rotation");
                        }
                        Assert.AreEqual(moved.position,hand.Local(HandJointId.HandWristRoot).position,"Display fit must never write tracked input");
                    }
                    finally{fit.Dispose();}
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

        // Headset test 2 (2026-11-22): a lost hand hung in the air. It freezes only for EmberdeepLostHand.FreezeSeconds.
        [UnityTest] public IEnumerator LostHandFreezesForAFifthOfASecondThenHidesUntilTrackingReturns()
        {
            using(var fixture=new PairFixture())
            {
                yield return null;
                for(int h=0;h<2;h++)
                {
                    var display=fixture.Displays[h];var hand=fixture.Hands[h];
                    hand.Move(new Vector3(h==0?-.2f:.2f,1.2f,.5f),Quaternion.Euler(15,0,0));
                    for(int i=0;i<3;i++)display.Present(true,EmberdeepHandPoseKind.Free,null,0,.02f);
                    Assert.IsTrue(display.Showing);Assert.IsFalse(display.Frozen);Assert.IsFalse(display.Hidden);
                    display.Present(false,EmberdeepHandPoseKind.Free,null,0,.02f);
                    Assert.IsTrue(display.Frozen);Assert.IsTrue(display.Showing,"the loss frame holds the last pose");
                    float held=0;
                    while(display.Frozen && held<1){display.Present(false,EmberdeepHandPoseKind.Free,null,0,.02f);held+=.02f;}
                    Assert.That(held,Is.EqualTo(EmberdeepLostHand.FreezeSeconds).Within(.021f),"frozen about .2 s");
                    Assert.IsTrue(display.Hidden);Assert.IsFalse(display.Showing,"then gone: no live copy, no snapshot");
                    for(int i=0;i<display.SkinCount;i++)Assert.IsFalse(display.SnapshotRenderer(i).enabled);
                    for(int i=0;i<50;i++)display.Present(false,EmberdeepHandPoseKind.Free,null,0,.02f);
                    Assert.IsFalse(display.Showing,"stays hidden while lost");Assert.AreEqual(1,display.SnapshotCount,"one bake per loss");
                    hand.Move(new Vector3(h==0?-.1f:.1f,1.1f,.45f),Quaternion.identity);
                    display.Present(true,EmberdeepHandPoseKind.Free,null,0,.02f);
                    Assert.IsTrue(display.Showing);Assert.IsFalse(display.Hidden);Assert.IsFalse(display.Frozen);
                    var wrist=fixture.Visuals[h].Joints[(int)HandJointId.HandWristRoot];
                    Assert.Less(Vector3.Distance(display.DisplayJoint(HandJointId.HandWristRoot).position,wrist.position),.001f,"back live where the hand is");
                }
            }
            yield return null;
        }

        // Headset test 2 (2026-11-22): a fist hammered down through the pump cap and the drawn hand stayed stuck on the cap
        // until the real hand came back up. The drawn palm may trail at a surface by at most PinRelease, then follows.
        [UnityTest] public IEnumerator HammeringThroughAContactNeverLeavesTheHandStuckOnIt()
        {
            using(var fixture=new PairFixture())
            {
                yield return null;
                var cap=GameObject.CreatePrimitive(PrimitiveType.Cube);
                try
                {
                    var display=fixture.Displays[1];var hand=fixture.Hands[1];var start=new Vector3(.2f,1.2f,.5f);
                    hand.Move(start,Quaternion.identity);for(int i=0;i<3;i++)display.Present(true,EmberdeepHandPoseKind.Free,null,0,.02f);
                    Assert.IsTrue(display.TryGetPalmFrame(out var palm0));
                    // A 10 cm block whose top is 6 cm under the palm: the fist goes 14 cm down (through it), then back up.
                    cap.transform.localScale=new Vector3(.12f,.1f,.12f);cap.transform.position=palm0.position+Vector3.down*(.06f+.05f);
                    var contacts=new EmberdeepHandContact();contacts.Add(cap.transform);contacts.Prepare();
                    float worst=0,held=0;bool released=false,releasedInside=false;
                    float Step(float depth)
                    {
                        hand.Move(start+Vector3.down*depth,Quaternion.identity);
                        display.Present(true,EmberdeepHandPoseKind.Free,null,0,.02f);display.TryGetPalmFrame(out var real);
                        display.Constrain(contacts,false);display.TryGetPalmFrame(out var drawn);
                        return Vector3.Distance(real.position,drawn.position);
                    }
                    for(int i=0;i<=14;i++)
                    {
                        float gap=Step(i*.01f);worst=Mathf.Max(worst,gap);held=Mathf.Max(held,gap);
                        if(display.PinReleased){released=true;if(i*.01f>.06f && i*.01f<.16f)releasedInside=true;}
                    }
                    Assert.Greater(held,.005f,"the strike still lands: the drawn palm stops at the top first");
                    Assert.LessOrEqual(worst,EmberdeepHandPresentation.PinRelease+.001f,"never trails the real hand by more than PinRelease");
                    Assert.IsTrue(released && releasedInside,"lets go while the fist is still inside the block");
                    Assert.Less(Step(.14f),.0005f,"through the block: the drawn hand is where the real hand is, not on the cap");
                    // Back up and out: no sticking on the way, and the contact holds again once clear.
                    for(int i=13;i>=0;i--)Assert.LessOrEqual(Step(i*.01f),EmberdeepHandPresentation.PinRelease+.001f,"lifting at "+i+" cm");
                    Assert.IsFalse(display.PinReleased,"clear of the block again");
                    Assert.Less(Step(0),.0005f);
                }
                finally{Object.Destroy(cap);}
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
                    display.Present(true,EmberdeepHandPoseKind.Free,null,0,.02f);display.TryGetPalmFrame(out var rawPalm);
                    anchor.transform.rotation=rawPalm.rotation*Quaternion.Inverse(EmberdeepHandPose.GripContact(EmberdeepHandPoseKind.ValveGrip,true).rotation);
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

        [UnityTest] public IEnumerator ButtonPressAlignsOnlyDisplayTipAndRejectsDistantRayTargetOffset()
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
                        display.TryGetPalmFrame(out var rawButtonPalm);
                        anchor.transform.rotation=rawButtonPalm.rotation*Quaternion.Inverse(Quaternion.LookRotation(Vector3.forward,Vector3.down));
                        display.Present(true,EmberdeepHandPoseKind.ButtonPress,anchor.transform,1,.1f);
                        Assert.Less(Vector3.Distance(anchor.transform.position,display.DisplayJoint(HandJointId.HandIndexTip).position),.001f);
                        display.Present(true,EmberdeepHandPoseKind.ButtonPress,null,1,.1f);
                        var initial=display.DisplayRoot.position;
                        anchor.transform.position=display.DisplayJoint(HandJointId.HandIndexTip).position+Vector3.forward*3;
                        display.Present(true,EmberdeepHandPoseKind.ButtonPress,anchor.transform,1,.1f);
                        Assert.That(Vector3.Distance(initial,display.DisplayRoot.position),Is.LessThan(.001f),"Distant UI cannot pull the hand toward it");
                        Assert.AreEqual(trackedRoot,visual.Root.position);Assert.AreEqual(trackedIndex,visual.Joints[(int)HandJointId.HandIndex1].localRotation);
                    }
                }
                finally{Object.Destroy(anchor);}
            }
            yield return null;
        }

        [UnityTest] public IEnumerator EveryGripRejectsBackwardsWristAndRecoversWithoutChangingTrackedInput()
        {
            using(var fixture=new PairFixture())
            {
                yield return null;var anchor=new GameObject("Handedness and backwards approach audit");
                try
                {
                    for(int h=0;h<2;h++)foreach(EmberdeepHandPoseKind kind in Enum.GetValues(typeof(EmberdeepHandPoseKind)))
                    {
                        if(kind==EmberdeepHandPoseKind.Free)continue;
                        var hand=fixture.Hands[h];var display=fixture.Displays[h];
                        hand.MoveWrist(new Vector3(h*.25f,1,.5f),Quaternion.identity,Quaternion.identity);
                        for(int n=0;n<8;n++)display.Present(true,EmberdeepHandPoseKind.Free,null,0,.02f);
                        display.TryGetPalmFrame(out var raw);
                        var local=kind==EmberdeepHandPoseKind.ButtonPress?
                            new Pose(Vector3.zero,Quaternion.LookRotation(Vector3.forward,Vector3.down)):EmberdeepHandPose.GripContact(kind,h==0);
                        var aligned=raw.rotation*Quaternion.Inverse(local.rotation);
                        anchor.transform.SetPositionAndRotation(raw.position-aligned*local.position,aligned);
                        for(int n=0;n<10;n++)display.Present(true,kind,anchor.transform,0,.02f);
                        if(kind!=EmberdeepHandPoseKind.ButtonPress)Assert.IsTrue(display.ContactLocked,kind+" "+h);
                        foreach(float angle in new[]{100f,179f,181f,260f})
                        {
                            var turned=Quaternion.AngleAxis(angle,Vector3.up)*raw.rotation;
                            anchor.transform.rotation=turned*Quaternion.Inverse(local.rotation);
                            anchor.transform.position=raw.position-anchor.transform.rotation*local.position;
                            for(int n=0;n<10;n++)
                            {
                                display.Present(true,kind,anchor.transform,0,.02f);display.TryGetPalmFrame(out var shown);
                                Assert.IsFalse(display.ContactLocked,kind+" "+h+" "+angle);
                                Assert.Less(Quaternion.Angle(raw.rotation,shown.rotation),.1f,"Backwards target must not flip the visible wrist");
                            }
                        }
                        anchor.transform.SetPositionAndRotation(raw.position-aligned*local.position,aligned);
                        for(int n=0;n<10;n++)display.Present(true,kind,anchor.transform,0,.02f);
                        if(kind!=EmberdeepHandPoseKind.ButtonPress)Assert.IsTrue(display.ContactLocked);
                        Assert.AreEqual(Quaternion.identity,hand.RootPose.rotation,"Display pose cannot change tracked input");
                    }
                }
                finally{Object.Destroy(anchor);}
            }
        }

        [Test] public void AllGripFramesMirrorPalmAndFingerDirectionsAcrossHands()
        {
            Vector3 Mirror(Vector3 v)=>new Vector3(-v.x,v.y,v.z);
            foreach(EmberdeepHandPoseKind kind in Enum.GetValues(typeof(EmberdeepHandPoseKind)))
            {
                if(kind==EmberdeepHandPoseKind.Free || kind==EmberdeepHandPoseKind.ButtonPress)continue;
                var left=EmberdeepHandPose.GripContact(kind,true);var right=EmberdeepHandPose.GripContact(kind,false);
                Assert.Less(Vector3.Distance(Mirror(left.position),right.position),.0001f,kind.ToString());
                Assert.Less(Vector3.Angle(Mirror(left.rotation*Vector3.forward),right.rotation*Vector3.forward),.03f,kind+" finger direction");
                Assert.Less(Vector3.Angle(Mirror(left.rotation*Vector3.up),right.rotation*Vector3.up),.03f,kind+" palm direction");
            }
        }
        [Test] public void ExtinguisherUsesWholeHandSqueezeWithRelaxedFingerSpacing()
        {
            var kind=EmberdeepHandPoseKind.ExtinguisherGrip;
            foreach(var finger in new[]{HandFinger.Index,HandFinger.Middle,HandFinger.Ring,HandFinger.Pinky})
            {
                float relaxed=EmberdeepHandPose.Curl(kind,finger,0),pressed=EmberdeepHandPose.Curl(kind,finger,1);
                Assert.That(relaxed,Is.InRange(.55f,.75f));
                Assert.Greater(pressed-relaxed,.075f,"Every finger takes part in the squeeze");
                Assert.Less(pressed,.85f,"Avoid a crushed or hooked fist");
            }
        }
        [UnityTest] public IEnumerator AlignedContactFollowsMovingObjectWithinNaturalWristRangeForBothHands()
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
                        anchor.transform.SetPositionAndRotation(raw.position+Vector3.forward*.02f,raw.rotation*Quaternion.Inverse(EmberdeepHandPose.GripContact(kind,h==0).rotation));
                        // Object mesh scaling cannot shrink the metre-sized contact calibration.
                        anchor.transform.localScale=Vector3.one*.035f;
                        for(int n=0;n<12;n++)display.Present(true,kind,anchor.transform,.4f,.02f);
                        Assert.IsTrue(display.ContactLocked,kind.ToString());
                        var local=EmberdeepHandPose.GripContact(kind,h==0);
                        foreach(var rotation in new[]{Quaternion.Euler(0,25,5),Quaternion.Euler(25,-20,10),Quaternion.Euler(-20,15,-25)})
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
                        anchor.transform.rotation*=Quaternion.Euler(-10,12,5);
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
                        anchor.transform.SetPositionAndRotation(raw.position+Vector3.up*.055f,raw.rotation*Quaternion.Inverse(EmberdeepHandPose.GripContact(EmberdeepHandPoseKind.LeverGrip,h==0).rotation));
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

        [UnityTest] public IEnumerator EveryProductionGraspLocksBothDisplayHandsAndRendersContactAtlas()
        {
            using(var fixture=new PairFixture())
            {
                yield return null;
                var mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));mat.color=new Color(.22f,.55f,.62f);
                var cameraObject=new GameObject("Grasp pose verification camera");var camera=cameraObject.AddComponent<Camera>();
                camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.07f,.10f,.14f);
                camera.orthographic=true;camera.orthographicSize=.20f;camera.nearClipPlane=.01f;camera.farClipPlane=3;
                var target=new RenderTexture(400,400,24);camera.targetTexture=target;
                var tile=new Texture2D(400,400,TextureFormat.RGB24,false);
                var atlas=new Texture2D(3200,1200,TextureFormat.RGB24,false);
                var extinguisherDetail=new Texture2D(1600,800,TextureFormat.RGB24,false);
                foreach(var visual in fixture.Visuals)foreach(var r in visual.GetComponentsInChildren<Renderer>())r.forceRenderingOff=true;
                int cell=0;
                try
                {
                    foreach(EmberdeepHandPoseKind kind in Enum.GetValues(typeof(EmberdeepHandPoseKind)))
                    {
                        if(kind==EmberdeepHandPoseKind.Free)continue;
                        var item=new GameObject(kind.ToString());Transform anchor;bool station=kind==EmberdeepHandPoseKind.LeverGrip
                            || kind==EmberdeepHandPoseKind.ValveGrip || kind==EmberdeepHandPoseKind.ButtonPress || kind==EmberdeepHandPoseKind.IntakeGrip;
                        if(station)
                        {
                            var view=item.AddComponent<EmberdeepWaterStationView>();view.Build(mat,mat,mat,kind==EmberdeepHandPoseKind.IntakeGrip);
                            view.SetTier(kind==EmberdeepHandPoseKind.ValveGrip?WaterStationTier.RotaryHydrant:
                                kind==EmberdeepHandPoseKind.ButtonPress?WaterStationTier.PushButton:WaterStationTier.LeverWell);
                            anchor=kind==EmberdeepHandPoseKind.LeverGrip?view.LeverVisual:kind==EmberdeepHandPoseKind.ButtonPress?view.ButtonVisual:
                                kind==EmberdeepHandPoseKind.IntakeGrip?view.IntakeVisual:view.ValveVisual;
                            if(kind==EmberdeepHandPoseKind.ValveGrip){var rim=new GameObject("Rim contact").transform;rim.SetParent(anchor,false);anchor=rim;}
                        }
                        else
                        {
                            var view=item.AddComponent<FireEquipmentView>();view.Build(kind==EmberdeepHandPoseKind.PistolGrip?FireEquipmentId.PulsePistol:
                                kind==EmberdeepHandPoseKind.CannonGrip || kind==EmberdeepHandPoseKind.CannonPump || kind==EmberdeepHandPoseKind.ForwardSupport
                                ?FireEquipmentId.PressureCannon:FireEquipmentId.Extinguisher,mat,mat,mat);
                            anchor=view.Grip;
                            if(kind==EmberdeepHandPoseKind.NozzleGrip)anchor=view.Nozzle;
                            if(kind==EmberdeepHandPoseKind.CannonPump)anchor=view.PumpGrip;
                            if(kind==EmberdeepHandPoseKind.BodyGrip){view.CaptureGrip(FireToolRegion.Body,new Vector3(0,-.12f,-.06f));anchor=view.SurfaceGrip;}
                            if(kind==EmberdeepHandPoseKind.ForwardSupport){view.CaptureSupport(new Vector3(0,-.06f,.16f));anchor=view.SupportGrip;}
                        }
                        var contacts=new EmberdeepHandContact();contacts.Add(item.transform);contacts.Prepare();
                        // Let Unity register newly created renderer bounds before
                        // manual camera culling; pose assertions still run below.
                        yield return null;
                        for(int h=0;h<2;h++)
                        {
                            if(kind==EmberdeepHandPoseKind.NozzleGrip)anchor.localPosition=new Vector3(h==0?-.16f:.16f,.04f,.1f);
                            if(kind==EmberdeepHandPoseKind.ValveGrip)anchor.localPosition=Vector3.right*(h==0?-.05f:.05f);
                            if(kind==EmberdeepHandPoseKind.CannonPump)item.GetComponent<FireEquipmentView>().SetLeftHanded(h==1);
                            var hand=fixture.Hands[h];var display=fixture.Displays[h];var source=fixture.Visuals[h];
                            display.DisplayRoot.gameObject.SetActive(true);
                            var wristPosition=anchor.position+new Vector3(h==0?-.035f:.035f,0,-.065f);
                            hand.MoveWrist(wristPosition,Quaternion.identity,Quaternion.identity);
                            for(int n=0;n<8;n++)display.Present(true,EmberdeepHandPoseKind.Free,null,0,.02f);
                            display.TryGetPalmFrame(out var rawApproach);
                            var desired=anchor.rotation*(kind==EmberdeepHandPoseKind.ButtonPress?Quaternion.LookRotation(Vector3.forward,Vector3.down):EmberdeepHandPose.GripContact(kind,h==0).rotation);
                            hand.MoveWrist(wristPosition,desired*Quaternion.Inverse(rawApproach.rotation),Quaternion.identity);
                            var raw=source.Root.position;var rotations=source.Joints.Select(j=>j?j.localRotation:Quaternion.identity).ToArray();
                            for(int n=0;n<14;n++)display.Present(true,kind,anchor,.7f,.02f);
                            if(kind!=EmberdeepHandPoseKind.ButtonPress)Assert.IsTrue(display.ContactLocked,kind+" hand "+h);
                            display.TryGetPalmFrame(out var before);display.Constrain(contacts,true);display.TryGetPalmFrame(out var after);
                            Assert.Less(Vector3.Distance(before.position,after.position),.0001f,"Contact solver must preserve grasp anchor");
                            Assert.AreEqual(raw,source.Root.position);
                            for(int j=0;j<rotations.Length;j++)if(source.Joints[j])Assert.AreEqual(rotations[j],source.Joints[j].localRotation,"Display pose contaminated input");
                            camera.transform.position=anchor.position+new Vector3(h==0?-.34f:.34f,.19f,-.55f);camera.transform.LookAt(anchor.position);
                            camera.Render();var active=RenderTexture.active;RenderTexture.active=target;
                            tile.ReadPixels(new Rect(0,0,400,400),0,0);tile.Apply();RenderTexture.active=active;
                            atlas.SetPixels((cell%8)*400,(2-cell/8)*400,400,400,tile.GetPixels());cell++;
                            if(kind==EmberdeepHandPoseKind.ExtinguisherGrip)
                            {
                                var tool=item.GetComponent<FireEquipmentView>();camera.orthographicSize=.105f;
                                for(int state=0;state<2;state++)
                                {
                                    tool.Animate(1,0,state==1,.1f);contacts.Prepare();
                                    for(int n=0;n<14;n++)display.Present(true,kind,anchor,state,.02f);
                                    display.Constrain(contacts,true);
                                    for(int view=0;view<2;view++)
                                    {
                                        var focus=anchor.position+Vector3.up*.018f;
                                        camera.transform.position=focus+new Vector3(h==0?-.24f:.24f,view==0?.09f:-.015f,-.3f);
                                        camera.transform.LookAt(focus);camera.Render();active=RenderTexture.active;RenderTexture.active=target;
                                        tile.ReadPixels(new Rect(0,0,400,400),0,0);tile.Apply();RenderTexture.active=active;
                                        extinguisherDetail.SetPixels((h*2+state)*400,(1-view)*400,400,400,tile.GetPixels());
                                    }
                                }
                                camera.orthographicSize=.20f;tool.Animate(1,0,false,.1f);
                            }
                            display.DisplayRoot.gameObject.SetActive(false);
                        }
                        fixture.Displays[1].DisplayRoot.gameObject.SetActive(false);item.SetActive(false);Object.Destroy(item);
                    }
                    atlas.Apply();extinguisherDetail.Apply();System.IO.Directory.CreateDirectory("Logs/EmberdeepExtinguisherGrip");
                    System.IO.File.WriteAllBytes("Logs/EmberdeepExtinguisherGrip/Grasp-Atlas.png",atlas.EncodeToPNG());
                    System.IO.File.WriteAllBytes("Logs/EmberdeepExtinguisherGrip/Extinguisher-Detail.png",extinguisherDetail.EncodeToPNG());
                }
                finally{camera.targetTexture=null;target.Release();Object.Destroy(target);Object.Destroy(tile);Object.Destroy(atlas);Object.Destroy(extinguisherDetail);Object.Destroy(cameraObject);Object.Destroy(mat);}
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
            public Material Material=>material;
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
        public Pose Local(HandJointId id)=>joints[(int)id];
        public void SetLocal(HandJointId id,Pose pose)=>joints[(int)id]=pose;
        public void Publish(){CurrentDataVersion++;WhenHandUpdated?.Invoke();}
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

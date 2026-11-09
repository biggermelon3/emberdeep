using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PhysicalAssembly.Tests
{
    public sealed class DesignerSandboxTests
    {
        GameObject root; WorkshopBootstrap workshop; DownhillSettings settings; GravityGarageLevelAsset course;
        DownhillController Level=>workshop.Downhill;
        [UnitySetUp] public IEnumerator Setup()
        {
            var scene=SceneManager.GetSceneByName("Designer sandbox fixture"); if(!scene.IsValid()) scene=SceneManager.CreateScene("Designer sandbox fixture"); SceneManager.SetActiveScene(scene);
            settings=ScriptableObject.CreateInstance<DownhillSettings>(); settings.DesignerBuilder=true; settings.UseGravityGarage=true; settings.TransitionDuration=0; settings.CountdownStep=.01f; settings.GoDuration=.01f;
            course=ScriptableObject.CreateInstance<GravityGarageLevelAsset>(); course.parameters=GravityGarage25DParameters.UnityCalibrated(); settings.Level25D=course;
            root=new GameObject("Designer test"); root.SetActive(false); workshop=root.AddComponent<WorkshopBootstrap>(); workshop.DownhillSettings=settings; root.SetActive(true); yield return null; DesignModeSession.SetEnabled(true);
        }
        [TearDown] public void Cleanup()
        {
            DesignModeSession.SetEnabled(false);
            if(workshop && Level) Level.RestoreBuildImmediately(); if(root) Object.DestroyImmediate(root);
            foreach(var part in Object.FindObjectsByType<AssemblyPart>(FindObjectsSortMode.None)) Object.DestroyImmediate(part.gameObject);
            foreach(var environment in Object.FindObjectsByType<WorkshopEnvironment>(FindObjectsSortMode.None)) Object.DestroyImmediate(environment.gameObject);
            Object.DestroyImmediate(settings); Object.DestroyImmediate(course);
        }
        [TestCase(0,false)] [TestCase(1,false)] [TestCase(0,true)] [TestCase(1,true)]
        public void PinchWheelRotatesConnectedBuildOnceAndReleasesSafely(int hand,bool design)
        {
            Assert.IsTrue(Level.TryLoadDesignerExample()); DesignModeSession.SetEnabled(design);
            var group=Level.Presentation.EnsureGridPadGroup();
            var arch=group.Find(CourseArchVisuals.PadName);Assert.NotNull(arch);
            var archPosition=arch.localPosition;var archRotation=arch.localRotation;
            var courseArch=Level.Course25D.transform.Find("Start arch");
            var coursePose=new Pose(courseArch.position,courseArch.rotation);
            group.position+=new Vector3(.017f,0,.012f); // authored offset must not accumulate during turning
            Level.Presentation.FollowCore(Level.Core);
            var wheel=Level.RotationWheel.transform; var center=wheel.position;
            var localPosition=wheel.localPosition; var localRotation=wheel.localRotation;
            var gridRotation=group.rotation; var pivot=Level.Core.MainBody.Rigidbody.position;
            var parts=workshop.World.GetConnectedParts(Level.Core.InstanceId);
            var bodies=parts.SelectMany(p=>p.Bodies).ToArray();
            var poses=bodies.Select(b=>new Pose(b.Rigidbody.position,b.Rigidbody.rotation)).ToArray();
            var loose=Level.Kit.Where(p=>!parts.Contains(p)).SelectMany(p=>p.Bodies).ToArray();
            var loosePoses=loose.Select(b=>new Pose(b.Rigidbody.position,b.Rigidbody.rotation)).ToArray();
            var edges=workshop.World.Connections.Select(e=>e.id).ToArray();
            var release=Level.SandboxPlacement(out _);
            var up=Level.Course.WorkbenchFrame.up; var radius=wheel.right*DownhillRotationWheel.Radius;
            var grip=new Pose(center+radius,Quaternion.identity);
            Level.SampleWheel(hand,true,.03f,false,0,grip,true,0);
            Assert.IsTrue(Level.SampleWheel(hand,true,.014f,true,0,grip,true,.01f));
            Assert.IsTrue(Level.IsRotatingWithHand(hand)); Assert.IsFalse(Level.IsRotatingWithHand(1-hand));
            Assert.IsFalse(Level.TryStart()); Assert.IsFalse(Level.TryResetBuild());
            Assert.IsFalse(workshop.World.TryGrab(hand,Level.Kit[3],new Pose(Level.Kit[3].transform.position,Quaternion.identity)));
            Assert.IsFalse(Level.BeginRotation(1-hand,grip.position));
            foreach(float angle in new[]{90f,179f,181f,270f,360f,450f}) {
                var turn=Quaternion.AngleAxis(angle,up); grip.position=center+turn*radius;
                Level.SampleWheel(hand,true,.014f,true,0,grip,true,.1f);
                Level.Presentation.FollowCore(Level.Core); // also exercise the next LateUpdate
                for(int i=0;i<bodies.Length;i++) {
                    Assert.Less(Vector3.Distance(pivot+turn*(poses[i].position-pivot),bodies[i].Rigidbody.position),.0001f);
                    Assert.Less(Quaternion.Angle(turn*poses[i].rotation,bodies[i].Rigidbody.rotation),.05f);
                }
                Assert.Less(Quaternion.Angle(turn*gridRotation,group.rotation),.05f);
                Assert.Less(Quaternion.Angle(localRotation,wheel.localRotation),.01f);
                Assert.Less(Vector3.Distance(localPosition,wheel.localPosition),.00001f);
                Assert.AreEqual(archPosition,arch.localPosition);
                Assert.Less(Quaternion.Angle(archRotation,arch.localRotation),.01f);
                Assert.Less(Quaternion.Angle(turn*gridRotation,arch.rotation),.05f);
                Assert.AreEqual(coursePose.position,courseArch.position);
                Assert.Less(Quaternion.Angle(coursePose.rotation,courseArch.rotation),.01f);
            }
            for(int i=0;i<loose.Length;i++) {
                Assert.Less(Vector3.Distance(loosePoses[i].position,loose[i].Rigidbody.position),.0001f);
                Assert.Less(Quaternion.Angle(loosePoses[i].rotation,loose[i].Rigidbody.rotation),.01f);
            }
            CollectionAssert.AreEqual(edges,workshop.World.Connections.Select(e=>e.id));
            var after=Level.SandboxPlacement(out _);
            Assert.Less(Vector3.Distance(release.position,after.position),.0001f);
            Assert.Less(Quaternion.Angle(release.rotation,after.rotation),.05f);
            var beforeOpen=Level.Core.MainBody.Rigidbody.rotation;
            Level.SampleWheel(hand,true,.025f,false,0,new Pose(center-radius,Quaternion.identity),true,.2f);
            Assert.IsFalse(Level.Rotating); Assert.IsFalse(workshop.World.ExternalHold);
            Assert.Less(Quaternion.Angle(beforeOpen,Level.Core.MainBody.Rigidbody.rotation),.01f);
            grip.position=wheel.position+wheel.right*DownhillRotationWheel.Radius;
            Assert.IsTrue(Level.SampleWheel(hand,true,.014f,true,0,grip,true,.3f));
            Level.SampleWheel(hand,false,0,false,0,default,false,.4f); Assert.IsTrue(Level.Rotating);
            Level.SampleWheel(hand,false,0,false,0,default,false,.7f); Assert.IsFalse(Level.Rotating);
            Assert.IsFalse(workshop.World.ExternalHold);
            Assert.IsFalse(Level.SampleWheel(hand,true,.014f,true,0,grip,true,.8f));
            Level.SampleWheel(hand,true,.03f,false,0,grip,true,.9f);
            Assert.IsTrue(Level.SampleWheel(hand,true,.014f,true,0,grip,true,1)); Level.CancelRotation();
            Assert.IsTrue(Level.TryStart()); Assert.IsFalse(wheel.gameObject.activeInHierarchy);
            Assert.IsFalse(arch.gameObject.activeInHierarchy);
            Level.RestoreBuildImmediately(); Assert.IsTrue(wheel.gameObject.activeInHierarchy);
            Assert.IsTrue(arch.gameObject.activeInHierarchy);
            Assert.IsTrue(Level.TryResetBuild()); Assert.IsTrue(wheel.gameObject.activeInHierarchy);
        }
        [Test] public void NewKitUsesSharedGridAndTerminalWheels()
        {
            Assert.AreEqual(9,Level.Kit.Count); Assert.AreEqual(6,Level.Core.Sockets.Length);
            Assert.IsTrue(Level.RotationWheel.gameObject.activeInHierarchy);
            Assert.AreEqual(14,Level.Kit[1].Sockets.Length); Assert.AreEqual(10,Level.Kit[2].Sockets.Length); Assert.AreEqual(24,Level.Kit[3].Sockets.Length);
            Assert.IsTrue(Level.Kit.All(p=>p.GridVersion==10)); Assert.AreEqual(0,workshop.World.Connections.Count);
            foreach(var wheel in Level.Kit.Where(p=>DownhillParts.IsWheel(p.Kind))) {
                Assert.AreEqual(1,wheel.Sockets.Length); Assert.IsTrue(Level.DragBuilder.Compatible(wheel,Level.Core)); Assert.IsFalse(Level.DragBuilder.Compatible(Level.Kit[1],wheel));
            }
            Assert.IsFalse(workshop.World.TryGrab(0,Level.Core,new Pose(Level.Core.transform.position,Quaternion.identity)));
        }
        [TestCase(5,true)] [TestCase(7,true)] [TestCase(5,false)] [TestCase(7,false)]
        public void WheelsSnapDirectlyToCoreAndSurviveTestReturn(int index,bool pointed)
        {
            DesignModeSession.SetEnabled(false);
            var world=workshop.World;var wheel=Level.Kit[index];
            int hand=pointed?0:1;
            var direction=Level.Presentation.AssemblyAnchor.right*(pointed?1:-1);
            var target=Level.Core.Sockets.First(s=>Vector3.Dot(s.Normal,direction)>.99f);
            var corePose=new Pose(Level.Core.MainBody.Rigidbody.position,Level.Core.MainBody.Rigidbody.rotation);
            if(!pointed) {
                var mount=wheel.Sockets.Single();var origin=wheel.MainBody.Rigidbody.position;
                var delta=Quaternion.FromToRotation(mount.Normal,-target.Normal);
                var position=target.WorldPosition+target.Normal*.005f-delta*(mount.WorldPosition-origin);
                foreach(var body in wheel.Bodies) {
                    var rb=body.Rigidbody;rb.position=position+delta*(rb.position-origin);rb.rotation=delta*rb.rotation;
                    body.transform.SetPositionAndRotation(rb.position,rb.rotation);
                }
                Physics.SyncTransforms();
            }
            Assert.IsTrue(world.TryGrab(hand,wheel,PartGrip(wheel)));
            if(pointed) Level.DragBuilder.SetAim(hand,new SandboxDragBuilder.Aim {
                ray=new Ray(target.WorldPosition+target.Normal*.2f,-target.Normal),maxDistance=.3f,angle=1
            });
            var candidate=world.Query(wheel);
            Assert.IsTrue(candidate.IsValid,"Wheel must preview a clear core socket.");
            Assert.AreSame(target,candidate.targetSocket);
            world.Release(hand,true);
            Assert.AreEqual(1,world.Connections.Count);Assert.IsTrue(world.SocketOccupied(target));
            CollectionAssert.AreEquivalent(new[]{Level.Core,wheel},world.GetConnectedParts(Level.Core.InstanceId));
            Assert.Less(Vector3.Distance(target.WorldPosition,wheel.Sockets[0].WorldPosition),.0001f);
            AssemblySaveService.Validate(workshop.Saves.Capture(Level.Core));
            Assert.IsTrue(Level.TryStart());
            Assert.AreEqual(2,Level.SimulationWorld.Parts.Count);Assert.AreEqual(1,Level.SimulationWorld.Connections.Count);
            var copy=Level.SimulationWorld.Parts.Values.Single(p=>DownhillParts.IsWheel(p.Kind));
            Assert.IsFalse(copy.GetComponentInChildren<HingeJoint>().useMotor);
            Level.RestoreBuildImmediately();
            Assert.AreEqual(1,world.Connections.Count);
            Assert.IsTrue(world.TryGrab(hand,wheel,PartGrip(wheel)));Assert.AreEqual(0,world.Connections.Count);
            world.Release(hand,false);
            Assert.Less(Vector3.Distance(corePose.position,Level.Core.MainBody.Rigidbody.position),.0001f);
            Assert.Less(Quaternion.Angle(corePose.rotation,Level.Core.MainBody.Rigidbody.rotation),.01f);
        }
        [Test] public void CourseArchesMarkBothLevelsWithoutBlockingVehiclesOrDependingOnLabels()
        {
            void Check(Transform arch) {
                Assert.NotNull(arch);Assert.IsTrue(arch.gameObject.activeInHierarchy);
                Assert.AreEqual(0,arch.GetComponentsInChildren<Collider>(true).Length);
                Assert.AreEqual(0,arch.GetComponentsInChildren<TextMesh>(true).Length);
                var mesh=arch.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh.vertexCount,Is.GreaterThan(100));Assert.AreEqual(4,mesh.subMeshCount);
                Assert.That(mesh.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)));
            }
            Level.Course25D.SetLabels(false);Level.Presentation.SetBuilding(true);
            Check(Level.Course25D.transform.Find("Start arch"));
            var goal=Level.Course25D.transform.Find("Goal arch");Check(goal);
            Assert.That(Level.Course25D.Coordinates(goal.position).x,Is.EqualTo(16.8f).Within(.001f));
            settings.Level25D=null;Level.RebuildGarageCourse();
            var first=Level.Course.SandboxFrame.Find("Gravity Garage course");
            Check(first.Find("Start arch"));Check(first.Find("Goal arch"));
            Assert.That(Level.GarageCourse.Coordinates(first.Find("Goal arch").position).x,Is.EqualTo(settings.Garage.GoalX).Within(.001f));
            Check(Level.Presentation.EnsureGridPadGroup().Find(CourseArchVisuals.PadName));
        }
        [Test] public void ExampleBuildsThroughRulesAndResetRestoresNinePieces()
        {
            Assert.IsTrue(Level.TryLoadDesignerExample()); Assert.AreEqual(6,workshop.World.Connections.Count);
            Assert.AreEqual(7,workshop.World.GetConnectedParts(Level.Core.InstanceId).Count);
            Assert.IsTrue(Level.TryResetBuild()); Assert.AreEqual(9,Level.Kit.Count); Assert.AreEqual(0,workshop.World.Connections.Count);
        }
        [Test] public void DetachedBranchSurvivesTestAndReturnWithIdentityAndConnections()
        {
            Assert.IsTrue(Level.TryLoadDesignerExample()); var branch=Level.Kit[1]; var world=workshop.World;
            Assert.IsTrue(world.TryGrab(0,branch,new Pose(branch.MainBody.Rigidbody.position,Quaternion.identity)));
            Assert.AreEqual(6,world.HeldParts(0).Count); Assert.AreEqual(5,world.Connections.Count); world.Release(0,false);
            var ids=Level.Kit.Select(p=>p.InstanceId).ToArray(); var edges=world.Connections.Select(c=>c.id).ToArray();
            
            Assert.IsTrue(Level.TryStart()); Level.RestoreBuildImmediately();
            Assert.IsTrue(Level.Presentation.WorkbenchVisuals.gameObject.activeSelf);
            CollectionAssert.AreEqual(ids,Level.Kit.Select(p=>p.InstanceId)); CollectionAssert.AreEqual(edges,world.Connections.Select(c=>c.id));
            Assert.IsTrue(Level.Presentation.WorkbenchVisuals.gameObject.activeSelf);
        }
        [Test] public void VersionTenSaveRestoresIndependentBranch()
        {
            Assert.IsTrue(Level.TryLoadDesignerExample());
            var save=workshop.Saves.Capture(Level.Core); AssemblySaveService.Validate(save);
            var restored=workshop.Saves.Restore(save,new Pose(new Vector3(3,2,0),Quaternion.identity));
            Assert.AreEqual(7,restored.Count); Assert.IsTrue(restored.All(p=>p.GridVersion==10)); Assert.AreEqual(6,workshop.World.GetConnectedParts(restored[0].InstanceId).Count-1);
        }
        [UnityTest] public IEnumerator CurrentCourseTransferRetryAndReturnKeepEditableKit()
        {
            Assert.IsTrue(Level.TryLoadDesignerExample()); var ids=Level.Kit.Select(p=>p.InstanceId).ToArray();
            for(int i=0;i<3;i++) {
                
                Assert.IsTrue(Level.TryStart()); yield return new WaitForSecondsRealtime(.1f);
                Assert.IsNotNull(Level.Run25D); Assert.AreEqual(7,Level.SimulationWorld.Parts.Count); Assert.AreEqual(6,Level.SimulationWorld.Connections.Count);
                Assert.IsTrue(Level.TryRetry()); yield return new WaitForSecondsRealtime(.1f); Assert.IsTrue(Level.TryReturn()); yield return null;
                Assert.AreEqual(DownhillState.Build,Level.State); CollectionAssert.AreEqual(ids,Level.Kit.Select(p=>p.InstanceId));
                Assert.AreEqual(9,Level.Kit.Count); Assert.AreEqual(6,workshop.World.Connections.Count);
            }
        }
        [UnityTest] public IEnumerator CoreOnlyVehicleCanStartAndReturn()
        {
            Assert.IsTrue(Level.TryStart()); yield return new WaitForSecondsRealtime(.1f); Assert.IsNotNull(Level.Run25D);
            Assert.AreEqual(1,Level.SimulationWorld.Parts.Count); Assert.IsTrue(Level.TryReturn()); yield return null;
        }
        [Test] public void QuarterTurnRebasesHeldBranch()
        {
            var part=Level.Kit[2]; var pose=new Pose(part.MainBody.Rigidbody.position,Quaternion.identity); var world=workshop.World;
            Assert.IsTrue(world.TryGrab(0,part,pose)); workshop.Session.Select(part);
            var before=part.MainBody.Rigidbody.rotation; Assert.IsTrue(Level.DragBuilder.RotateSelected(1));
            Assert.That(Quaternion.Angle(before,part.MainBody.Rigidbody.rotation),Is.EqualTo(90).Within(.1f));
            world.SetHoldPose(0,pose); world.Release(0,false); Assert.That(Quaternion.Angle(before,part.MainBody.Rigidbody.rotation),Is.EqualTo(90).Within(.1f));
        }
        [UnityTest] public IEnumerator PointerTargetCanBridgeHeightAndCommitsThePreview()
        {
            var world=workshop.World; var part=Level.Kit[2]; var target=Level.Core.Sockets.Single(s=>Vector3.Dot(s.Normal,Vector3.up)>.99f);
            Assert.IsTrue(world.TryGrab(0,part,new Pose(part.MainBody.Rigidbody.position,Quaternion.identity)));
            Level.DragBuilder.SetAim(0,new SandboxDragBuilder.Aim {ray=new Ray(target.WorldPosition+Vector3.up,Vector3.down),angle=3,maxDistance=2});
            var candidate=world.Query(part); Assert.IsTrue(candidate.IsValid); Assert.AreSame(target,candidate.targetSocket);
            var expected=candidate.SnappedPose; world.Release(0,true); yield return null;
            Assert.AreEqual(1,world.Connections.Count); Assert.That(Vector3.Distance(expected.position,part.MainBody.Rigidbody.position),Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(expected.rotation,part.MainBody.Rigidbody.rotation),Is.LessThan(.1f));
        }
        [UnityTest] public IEnumerator LooseBlockerKeepsIntendedTargetRedAndReleaseDoesNotAttach()
        {
            var world=workshop.World; var part=Level.Kit[1]; var blocker=Level.Kit[3]; float u=BuildingGeometry.Unit;
            var target=Level.Core.Sockets.Single(s=>Vector3.Dot(s.Normal,Vector3.up)>.99f);
            void Place(AssemblyPart p,Vector3 position) { foreach(var b in p.Bodies) { b.Rigidbody.position=position; b.Rigidbody.rotation=Quaternion.identity; b.transform.SetPositionAndRotation(position,Quaternion.identity); } Physics.SyncTransforms(); }
            Place(part,target.WorldPosition+Vector3.up*.5f);
            Assert.IsTrue(world.TryGrab(0,part,new Pose(part.MainBody.Rigidbody.position,Quaternion.identity)));
            Level.DragBuilder.SetAim(0,new SandboxDragBuilder.Aim {ray=new Ray(target.WorldPosition+Vector3.up,Vector3.down),angle=3,maxDistance=2});
            var clear=world.Query(part); Assert.IsTrue(clear.IsValid);
            Place(blocker,clear.SnappedPose.position+Vector3.forward*(1.8f*u));
            Assert.IsFalse(world.Query(part).IsValid); Assert.AreSame(target,world.BlockedCandidate(part).targetSocket);
            var before=part.MainBody.Rigidbody.position; world.Release(0,true); yield return null;
            Assert.AreEqual(0,world.Connections.Count); Assert.That(Vector3.Distance(before,part.MainBody.Rigidbody.position),Is.LessThan(.001f));
        }
        AssemblyPart[] LooseExample()
        {
            Assert.IsTrue(Level.TryLoadDesignerExample());
            var world=workshop.World; var rootPart=Level.Kit[1];
            Assert.IsTrue(world.TryGrab(0,rootPart,new Pose(rootPart.MainBody.Rigidbody.position,Quaternion.identity)));
            world.Release(0,false); DesignModeSession.SetEnabled(false);
            return new[]{Level.Kit[1],Level.Kit[2],Level.Kit[5],Level.Kit[6],Level.Kit[7],Level.Kit[8]};
        }
        static Pose PartGrip(AssemblyPart part) => new Pose(part.MainBody.Rigidbody.position,Quaternion.identity);
        [UnityTest] public IEnumerator ReleasedLooseAssemblyStaysFixedThroughContactAndRegrab()
        {
            var parts=LooseExample();var world=workshop.World;
            var bodies=parts.SelectMany(p=>p.Bodies).ToArray();
            var poses=bodies.Select(b=>new Pose(b.Rigidbody.position,b.Rigidbody.rotation)).ToArray();
            var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);obstacle.transform.SetParent(root.transform);
            obstacle.transform.position=bodies[0].Rigidbody.position;obstacle.transform.localScale=Vector3.one*.2f;
            Physics.SyncTransforms();
            for(int tick=0;tick<150;tick++) {
                yield return new WaitForFixedUpdate();
                for(int i=0;i<bodies.Length;i++) {
                    Assert.IsTrue(bodies[i].Rigidbody.isKinematic);Assert.IsFalse(bodies[i].Rigidbody.useGravity);
                    Assert.Less(Vector3.Distance(poses[i].position,bodies[i].Rigidbody.position),.0001f);
                    Assert.Less(Quaternion.Angle(poses[i].rotation,bodies[i].Rigidbody.rotation),.1f);
                }
            }
            Object.DestroyImmediate(obstacle);
            Assert.IsTrue(world.TryGrab(0,parts[0],PartGrip(parts[0])));world.Release(0,false);
            Assert.IsTrue(bodies.All(b=>b.Rigidbody.isKinematic));
            Assert.IsTrue(Level.TryResetBuild());Assert.IsTrue(Level.Kit.SelectMany(p=>p.Bodies).All(b=>b.Rigidbody.isKinematic));
        }
        [TestCase(0,1)] [TestCase(1,2)] [TestCase(0,5)] [TestCase(2,8)]
        public void AnyLooseMemberMovesWholeGroup(int hand,int index)
        {
            var parts=LooseExample(); var world=workshop.World; world.ConstrainBuildMotion=false;
            var selected=Level.Kit[index]; var edges=world.Connections.Select(e=>e.id).ToArray();
            var before=parts.Select(p=>p.MainBody.Rigidbody.position).ToArray(); var grip=PartGrip(selected);
            Assert.IsTrue(world.TryGrab(hand,selected,grip)); CollectionAssert.AreEquivalent(parts,world.HeldParts(hand));
            grip.position+=Vector3.up*.2f; world.SetHoldPose(hand,grip);
            for(int i=0;i<parts.Length;i++) Assert.Less(Vector3.Distance(before[i]+Vector3.up*.2f,parts[i].MainBody.Rigidbody.position),.0001f);
            CollectionAssert.AreEqual(edges,world.Connections.Select(e=>e.id)); world.Release(hand,false);
        }
        [TestCase(0,1,2)] [TestCase(1,2,1)] [TestCase(0,2,5)]
        public void SecondHandRemovesOnlyPickedPartAndKeepsCorrectRemainder(int first,int selectedIndex,int takenIndex)
        {
            var parts=LooseExample(); var world=workshop.World; world.ConstrainBuildMotion=false;
            var selected=Level.Kit[selectedIndex]; var taken=Level.Kit[takenIndex]; int second=1-first;
            var expected= takenIndex==2?new[]{Level.Kit[1],Level.Kit[5],Level.Kit[6]}:
                takenIndex==1?new[]{Level.Kit[2],Level.Kit[7],Level.Kit[8]}:parts.Where(p=>p!=taken).ToArray();
            var keepEdges=world.Connections.Where(e=>e.a.partId!=taken.InstanceId && e.b.partId!=taken.InstanceId).Select(e=>e.id).ToArray();
            Assert.IsTrue(world.TryGrab(first,selected,PartGrip(selected)));
            var bodies=parts.SelectMany(p=>p.Bodies).ToArray(); var poses=bodies.Select(b=>new Pose(b.Rigidbody.position,b.Rigidbody.rotation)).ToArray();
            int topology=0,holds=0;
            System.Action check=()=> {CollectionAssert.AreEquivalent(expected,world.HeldParts(first)); CollectionAssert.AreEqual(new[]{taken},world.HeldParts(second));};
            System.Action changed=()=>{check();topology++;}; System.Action held=()=>{check();holds++;};
            world.TopologyChanged+=changed; world.HoldsChanged+=held;
            Assert.IsTrue(world.TryGrab(second,taken,PartGrip(taken)));
            world.TopologyChanged-=changed; world.HoldsChanged-=held;
            Assert.AreEqual(1,topology); Assert.AreEqual(1,holds); CollectionAssert.AreEqual(keepEdges,world.Connections.Select(e=>e.id));
            for(int i=0;i<bodies.Length;i++) {Assert.Less(Vector3.Distance(poses[i].position,bodies[i].Rigidbody.position),.0001f);Assert.Less(Quaternion.Angle(poses[i].rotation,bodies[i].Rigidbody.rotation),.01f);}
            var grip=PartGrip(selected); grip.position+=Vector3.up*.2f; world.SetHoldPose(first,grip);
            for(int i=0;i<bodies.Length;i++) Assert.Less(Vector3.Distance(poses[i].position+(expected.Contains(bodies[i].Part)?Vector3.up*.2f:Vector3.zero),bodies[i].Rigidbody.position),.0001f);
            foreach(var part in parts.Where(p=>p!=taken && !expected.Contains(p))) {
                Assert.IsFalse(world.IsSuspended(part)); Assert.IsTrue(part.Bodies.All(b=>b.Rigidbody.isKinematic && !b.Rigidbody.useGravity));
            }
            world.ReleaseAll(); Assert.IsTrue(Level.TryStart()); Level.RestoreBuildImmediately();
            CollectionAssert.AreEqual(keepEdges,world.Connections.Select(e=>e.id));
            foreach(var part in parts) AssemblySaveService.Validate(workshop.Saves.Capture(part));
            Assert.IsTrue(Level.TryResetBuild()); Assert.AreEqual(0,world.Connections.Count);
        }
        [TestCase(0)] [TestCase(1)] public void SameSelectedLeafHandsOverWholeGroupWithFreshPinch(int first)
        {
            var parts=LooseExample(); var world=workshop.World; var selected=Level.Kit[8]; int second=1-first;
            var drivers=new[]{new HandGrabDriver(world,0),new HandGrabDriver(world,1)}; var pose=PartGrip(selected);
            drivers[first].Sample(true,.04f,false,pose,selected,true,0); drivers[first].Sample(true,.014f,true,pose,selected,true,.01f);
            drivers[second].Sample(true,.04f,false,pose,selected,true,.02f); drivers[second].Sample(true,.014f,true,pose,selected,true,.03f);
            Assert.IsNull(world.HeldPart(first)); CollectionAssert.AreEquivalent(parts,world.HeldParts(second)); Assert.AreEqual(5,world.Connections.Count);
            drivers[first].Sample(true,.014f,true,pose,selected,true,.04f); Assert.IsNull(world.HeldPart(first));
            drivers[second].Sample(false,0,false,default,null,false,.2f); Assert.IsTrue(world.HasHolds);
            drivers[second].Sample(false,0,false,default,null,false,.5f); Assert.IsFalse(world.HasHolds);
        }
        [Test] public void RejectedSplitPreservesGroupAndRepeatedSplitsStayIndependent()
        {
            var parts=LooseExample(); var world=workshop.World;var a=Level.Kit[1];var b=Level.Kit[2];
            DesignModeSession.SetEnabled(true);
            Assert.IsTrue(world.TryGrab(0,a,PartGrip(a)));
            Assert.IsTrue(world.TryGrab(1,Level.Kit[3],PartGrip(Level.Kit[3])));
            var edges=world.Connections.Select(e=>e.id).ToArray();
            Assert.IsFalse(world.TryGrab(1,b,PartGrip(b)));CollectionAssert.AreEqual(edges,world.Connections.Select(e=>e.id));CollectionAssert.AreEquivalent(parts,world.HeldParts(0));
            world.Release(1,false);world.EditingEnabled=false;Assert.IsFalse(world.TryGrab(1,b,PartGrip(b)));world.EditingEnabled=true;
            Assert.IsTrue(world.TryGrab(1,b,PartGrip(b)));world.Release(1,false);
            Assert.IsTrue(world.TryGrab(1,Level.Kit[5],PartGrip(Level.Kit[5])));
            CollectionAssert.AreEquivalent(new[]{a,Level.Kit[6]},world.HeldParts(0));CollectionAssert.AreEqual(new[]{Level.Kit[5]},world.HeldParts(1));
            Assert.IsTrue(Level.Kit[5].GetComponentInChildren<HingeJoint>());
            world.ReleaseAll();
            var saved=workshop.Saves.Capture(a);AssemblySaveService.Validate(saved);
            var restored=workshop.Saves.Restore(saved,new Pose(new Vector3(3,2,0),Quaternion.identity));Assert.AreEqual(2,restored.Count);
        }
        [UnityTest] public IEnumerator SpringSplitPreservesPoseAndBothHoldLifetimes()
        {
            LooseExample(); var world=workshop.World; var a=Level.Kit[1];var b=Level.Kit[2];
            Assert.IsTrue(world.TryGrab(0,a,PartGrip(a)));var pose=PartGrip(a);pose.position+=Vector3.up*.15f;world.SetHoldPose(0,pose);
            for(int i=0;i<10;i++) yield return new WaitForFixedUpdate();
            var before=a.MainBody.Rigidbody.position;var other=b.MainBody.Rigidbody.position;
            Assert.IsTrue(world.TryGrab(1,b,PartGrip(b)));yield return new WaitForFixedUpdate();
            Assert.Less(Vector3.Distance(before,a.MainBody.Rigidbody.position),.001f);Assert.Less(Vector3.Distance(other,b.MainBody.Rigidbody.position),.001f);
            world.Release(0,false);Assert.AreSame(b,world.HeldPart(1));world.Release(1,false);Assert.IsFalse(world.HasHolds);
        }
        [Test] public void FreePartsAssembleWithoutCoreAndWheelCannotBecomeTarget()
        {
            var world=workshop.World; var part=Level.Kit[2]; var targetPart=Level.Kit[1];
            var target=targetPart.Sockets.First(s=>Vector3.Dot(s.Normal,Vector3.up)>.99f);
            Assert.IsTrue(world.TryGrab(1,part,new Pose(part.MainBody.Rigidbody.position,Quaternion.identity)));
            Level.DragBuilder.SetAim(1,new SandboxDragBuilder.Aim {ray=new Ray(target.WorldPosition+Vector3.up,Vector3.down),maxDistance=2,angle=1});
            var candidate=world.Query(part); Assert.IsTrue(candidate.IsValid); Assert.AreSame(targetPart,candidate.target.Part);
            world.Release(1,true); Assert.AreEqual(1,world.Connections.Count); Assert.AreEqual(1,world.GetConnectedParts(Level.Core.InstanceId).Count);
            Assert.IsTrue(world.TryGrab(0,targetPart,new Pose(targetPart.MainBody.Rigidbody.position,Quaternion.identity)));
            Assert.AreEqual(2,world.HeldParts(0).Count); world.Release(0,false);
        }
        [Test] public void PlaneDragKeepsHeightAndParallelRayKeepsLastPose()
        {
            var drag=new SandboxPlaneDrag(); drag.Begin(new Vector3(0,2,0),Vector3.up,Quaternion.identity);
            var pose=drag.Sample(new Ray(new Vector3(3,4,5),Vector3.down)); Assert.AreEqual(new Vector3(3,2,5),pose.position);
            Assert.AreEqual(pose.position,drag.Sample(new Ray(Vector3.zero,Vector3.forward)).position);
        }
        [Test] public void TrackingLossClearsRayIntentAndPreservesFreeAssembly()
        {
            Assert.IsTrue(Level.TryLoadDesignerExample()); var branch=Level.Kit[1]; var world=workshop.World;
            var driver=new HandGrabDriver(world,0); var pose=new Pose(branch.MainBody.Rigidbody.position,Quaternion.identity);
            driver.Sample(true,.1f,false,pose,branch,true,1);
            driver.Sample(true,.01f,true,pose,branch,true,1.1f); Assert.AreSame(branch,world.HeldPart(0));
            Level.DragBuilder.SetAim(0,new SandboxDragBuilder.Aim {ray=new Ray(Vector3.up,Vector3.down),maxDistance=2,angle=3});
            driver.Sample(false,0,false,default,null,false,1.2f); Assert.AreSame(branch,world.HeldPart(0));
            driver.Sample(false,0,false,default,null,false,1.5f); Assert.IsFalse(world.HasHolds);
            Assert.IsFalse(Level.DragBuilder.HasAim(branch,out _)); Assert.AreEqual(5,world.Connections.Count);
        }
        [Test] public void TargetHeldByOtherHandCannotBeAcquired()
        {
            var world=workshop.World; var a=Level.Kit[1]; var b=Level.Kit[2];
            Assert.IsTrue(world.TryGrab(0,a,new Pose(a.MainBody.Rigidbody.position,Quaternion.identity)));
            Assert.IsTrue(world.TryGrab(1,b,new Pose(b.MainBody.Rigidbody.position,Quaternion.identity)));
            var target=b.Sockets.First(s=>Vector3.Dot(s.Normal,Vector3.up)>.99f);
            Level.DragBuilder.SetAim(0,new SandboxDragBuilder.Aim {ray=new Ray(target.WorldPosition+Vector3.up,Vector3.down),maxDistance=2,angle=1});
            Assert.IsFalse(world.Query(a).IsValid); world.ReleaseAll(); Assert.AreEqual(0,world.Connections.Count);
        }
        [Test] public void NativeRotationControlWorksDuringHoldWhileStartStaysBlocked()
        {
            var world=workshop.World; var part=Level.Kit[2]; workshop.Session.Select(part);
            Assert.IsTrue(world.TryGrab(0,part,new Pose(part.MainBody.Rigidbody.position,Quaternion.identity)));
            var button=Level.Presentation.Interface.RotateRightButton;
            var native=button.gameObject.AddComponent<PhysicalAssembly.MetaXR.MetaMenuButton>(); native.Initialize(workshop);
            var before=part.MainBody.Rigidbody.rotation;
            native.Poke.PublishPointerEvent(new Oculus.Interaction.PointerEvent(730,Oculus.Interaction.PointerEventType.Select,default));
            Assert.That(Quaternion.Angle(before,part.MainBody.Rigidbody.rotation),Is.EqualTo(90).Within(.1f));
            var start=Level.Presentation.ReadyButton.gameObject.AddComponent<PhysicalAssembly.MetaXR.MetaMenuButton>(); start.Initialize(workshop);
            start.Ray.PublishPointerEvent(new Oculus.Interaction.PointerEvent(731,Oculus.Interaction.PointerEventType.Select,default));
            Assert.IsTrue(Level.IsBuilding); Assert.AreSame(part,world.HeldPart(0)); world.ReleaseAll();
        }
    }
}

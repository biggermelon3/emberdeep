using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace PhysicalAssembly.Tests
{
    public sealed partial class DownhillTests
    {
        GameObject root;
        WorkshopBootstrap workshop;
        DownhillController level;
        DownhillSettings settings;
        [UnitySetUp] public IEnumerator Setup()
        {
            var fixture=SceneManager.GetSceneByName("Downhill physics fixture");
            if(!fixture.IsValid()) fixture=SceneManager.CreateScene("Downhill physics fixture");
            SceneManager.SetActiveScene(fixture);
            foreach(var name in new[]{"Workshop","NarrowGap","DownhillSandbox","CloudGardenDemo"})
            { var scene=SceneManager.GetSceneByName(name); if(scene.IsValid()&&scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene); }
            settings=ScriptableObject.CreateInstance<DownhillSettings>(); settings.TransitionDuration=0;
            root=new GameObject("Downhill test"); root.SetActive(false); root.AddComponent<AudioListener>();
            workshop=root.AddComponent<WorkshopBootstrap>(); workshop.DownhillSettings=settings;
            root.SetActive(true); level=workshop.Downhill; yield return null;
        }
        [TearDown] public void Cleanup()
        {
            if(level) level.RestoreBuildImmediately();
            if(root) Object.DestroyImmediate(root);
            if(workshop) Object.DestroyImmediate(workshop.gameObject);
            foreach(var p in Object.FindObjectsByType<AssemblyPart>(FindObjectsSortMode.None)) Object.DestroyImmediate(p.gameObject);
            foreach(var c in Object.FindObjectsByType<WorkshopEnvironment>(FindObjectsSortMode.None)) Object.DestroyImmediate(c.gameObject);
            if(settings) Object.DestroyImmediate(settings);
        }
        public static void AddWheel(WorkshopBootstrap workshop,AssemblyPart wheel,int side,float z)
        {
            var core=workshop.Downhill.Core;
            var socket=core.Sockets.Where(s=>s.Id.Contains(side>0?"x+":"x-")&&!workshop.World.SocketOccupied(s))
                .OrderBy(s=>Mathf.Abs(s.transform.localPosition.z-z)+Mathf.Abs(s.transform.localPosition.y)*2).First();
            var mount=wheel.FindSocket("mount"); var rotation=core.MainBody.Rigidbody.rotation*Quaternion.Euler(0,side>0?0:180,0);
            foreach(var body in wheel.Bodies)
            {
                body.Rigidbody.position=socket.WorldPosition-rotation*mount.transform.localPosition; body.Rigidbody.rotation=rotation;
                body.transform.SetPositionAndRotation(body.Rigidbody.position,rotation);
            }
            Physics.SyncTransforms();
            Assert.IsTrue(workshop.World.Connect(new ConnectionRecord { id=Guid.NewGuid().ToString("N"),a=new BodyRef(wheel.InstanceId,mount.Body.Id),b=new BodyRef(core.InstanceId,socket.Body.Id),socketA=mount.Id,socketB=socket.Id,anchorA=mount.transform.localPosition,anchorB=socket.transform.localPosition }));
        }
        public static void SetHeading(DownhillController controller,float yaw=0)
        {
            var center=controller.RotationWheel.transform.position;
            var direction=controller.Course.WorkbenchFrame.right*DownhillRotationWheel.Radius;
            Assert.IsTrue(controller.BeginRotation(2,center+direction));
            controller.Rotate(2,center+Quaternion.AngleAxis(yaw,controller.Course.WorkbenchFrame.up)*direction);
            controller.EndRotation(2);
        }
        IEnumerator WaitForPhysics()
        {
            float deadline=Time.time+DownhillController.LaunchDelay+1;
            while(level.State==DownhillState.Countdown && Time.time<deadline) yield return new WaitForFixedUpdate();
            Assert.AreEqual(DownhillState.Running,level.State);
        }
        [UnityTest] public IEnumerator AuthoredWheelSizeSurvivesSimulationTransfer()
        {
            Cleanup();
            settings=ScriptableObject.CreateInstance<DownhillSettings>(); settings.TransitionDuration=0;
            root=new GameObject("Authored wheel test"); root.SetActive(false); root.AddComponent<AudioListener>();
            workshop=root.AddComponent<WorkshopBootstrap>(); workshop.DownhillSettings=settings;
            workshop.CreateSceneLayout(true);
            workshop.Layout.Spawns[1].transform.localScale=Vector3.one*1.5f;
            root.SetActive(true); level=workshop.Downhill; yield return null;
            var wheel=level.Kit[1];
            Assert.That(wheel.GeometryScale,Is.EqualTo(1.5f).Within(.0001f));
            Assert.That(wheel.GetComponentInChildren<PassiveWheel>().Radius,Is.EqualTo(.13f*1.5f).Within(.0001f));
            Assert.That(wheel.FindSocket("mount").transform.localPosition.x,Is.EqualTo(-.0285f*1.5f).Within(.0001f));
            Assert.That(wheel.FindBody("tire").GetComponentInChildren<MeshCollider>().sharedMesh.bounds.size.y,Is.EqualTo(.26f*1.5f).Within(.0001f));
            AddWheel(workshop,wheel,1,0);
            AddWheel(workshop,level.Kit[2],-1,0);
            Assert.IsTrue(level.TryStart());
            yield return null;
            var clone=level.SimulationWorld.Parts.Values.Single(p=>p.Kind==PartKind.DownhillLargeWheel && p.GeometryScale>level.TestScale*1.25f);
            Assert.That(clone.GeometryScale,Is.EqualTo(level.TestScale*1.5f).Within(.0001f));
            Assert.That(clone.GetComponentInChildren<PassiveWheel>().Radius,Is.EqualTo(.13f*level.TestScale*1.5f).Within(.0001f));
            Assert.That(clone.FindSocket("mount").transform.localPosition.x,Is.EqualTo(-.0285f*level.TestScale*1.5f).Within(.0001f));
        }

        [UnityTest] public IEnumerator CarWaitsThreeSecondsBeforeAnyBodyStartsPhysics()
        {
            BuildReferenceAxle(workshop,PartKind.DownhillLargeWheel);
            settings.StallDuration=.2f; settings.AttemptDuration=.5f;
            float started=Time.time;
            Assert.IsTrue(level.TryStart());
            Assert.AreEqual(DownhillState.Countdown,level.State);
            StringAssert.Contains("STARTING IN 3",workshop.Session.Status);
            Assert.IsTrue(level.Presentation.BackButton.Interactable);
            Assert.AreEqual("START",level.Course.ModeButton.GetComponentInChildren<TextMesh>().text);
            Assert.IsFalse(level.TryStart()); Assert.IsFalse(workshop.World.EditingEnabled);
            var bodies=level.SimulationWorld.Parts.Values.SelectMany(p=>p.Bodies).ToArray();
            var poses=bodies.Select(b=>new Pose(b.Rigidbody.position,b.Rigidbody.rotation)).ToArray();
            yield return new WaitForSeconds(1.1f);
            StringAssert.Contains("STARTING IN 2",workshop.Session.Status);
            yield return new WaitForSeconds(1.1f);
            StringAssert.Contains("STARTING IN 1",workshop.Session.Status);
            Assert.AreEqual(DownhillState.Countdown,level.State,"The run and stall limits must not count down while frozen.");
            for(int i=0;i<bodies.Length;i++)
            {
                Assert.IsTrue(bodies[i].Rigidbody.isKinematic);
                Assert.Less(Vector3.Distance(poses[i].position,bodies[i].Rigidbody.position),.00001f);
                Assert.Less(Quaternion.Angle(poses[i].rotation,bodies[i].Rigidbody.rotation),.01f);
            }
            while(level.State==DownhillState.Countdown && Time.time-started<4)
            {
                if(Time.time-started<3) Assert.IsTrue(bodies.All(b=>b.Rigidbody.isKinematic));
                yield return new WaitForFixedUpdate();
            }
            Assert.GreaterOrEqual(Time.time-started,3f);
            Assert.Less(Time.time-started,3.6f);
            Assert.AreEqual(DownhillState.Running,level.State);
            Assert.IsTrue(bodies.All(b=>!b.Rigidbody.isKinematic && b.Rigidbody.useGravity));
            yield return new WaitForSeconds(.1f);
            Assert.IsTrue(bodies.Any(b=>b.Rigidbody.linearVelocity.sqrMagnitude>.0001f),"Gravity must actually move the released vehicle.");
        }
        [UnityTest] public IEnumerator ReturnCancelsCountdownAndRestartGetsFullDelay()
        {
            var original=level.Core.MainBody.Rigidbody.position;
            Assert.IsTrue(level.TryStart()); yield return new WaitForSeconds(1.6f);
            Assert.IsTrue(level.TryReturn()); Assert.IsNull(level.SimulationWorld);
            Assert.Less(Vector3.Distance(original,level.Core.MainBody.Rigidbody.position),.00001f);
            Assert.IsTrue(level.TryStart()); yield return new WaitForSeconds(1.6f);
            Assert.AreEqual(DownhillState.Countdown,level.State,"The cancelled attempt must not release the new car.");
            Assert.IsTrue(level.SimulationCore.MainBody.Rigidbody.isKinematic);
            yield return WaitForPhysics();
            Assert.IsTrue(level.TryReturn());
        }
        [UnityTest] public IEnumerator DisablingDuringCountdownRestoresBuildWithoutDelayedRelease()
        {
            var original=level.Core.MainBody.Rigidbody.position;
            Assert.IsTrue(level.TryStart()); level.enabled=false;
            Assert.IsTrue(level.IsBuilding); Assert.IsNull(level.SimulationWorld);
            Assert.IsTrue(level.Core.gameObject.activeSelf); Assert.IsTrue(workshop.World.EditingEnabled);
            level.enabled=true;
            yield return new WaitForSeconds(3.2f);
            Assert.IsTrue(level.IsBuilding); Assert.IsNull(level.SimulationWorld);
            Assert.Less(Vector3.Distance(original,level.Core.MainBody.Rigidbody.position),.00001f);
        }
        [TestCase(0)] [TestCase(1)] public void LoosePartPinchReleasesImmediately(int hand)
        {
            GrabLifetimeTests.VerifyImmediateRelease(workshop.World,level.Kit.First(p=>p.Kind==PartKind.DownhillShortBlock),hand);
        }
        [TestCase(0)] [TestCase(1)] public void RotationWheelReleasesBeforeOpeningPose(int hand)
        {
            AddWheel(workshop,level.Kit.First(p=>p.Kind==PartKind.DownhillLargeWheel),1,0);
            var pose=new Pose(level.RotationWheel.transform.position+level.Course.WorkbenchFrame.right*DownhillRotationWheel.Radius,Quaternion.identity);
            level.SampleWheel(hand,true,.08f,true,1,pose,true,0);
            Assert.IsFalse(level.SampleWheel(hand,true,.08f,true,1,pose,true,.01f));
            Assert.IsTrue(level.SampleWheel(hand,true,.014f,false,0,pose,true,.02f));
            pose.position+=Vector3.up*.5f;
            level.SampleWheel(hand,true,.014f,false,0,pose,true,.1f);
            level.SampleWheel(hand,false,0,false,0,default,true,.2f);
            Assert.IsTrue(level.Rotating);
            level.SampleWheel(hand,true,.014f,false,0,pose,true,.3f);
            Assert.IsTrue(level.Rotating,"Brief tracking gaps preserve the closed latch.");
            var bodies=workshop.World.GetConnectedParts(level.Core.InstanceId).SelectMany(p=>p.Bodies).ToArray();
            var poses=bodies.Select(b=>new Pose(b.Rigidbody.position,b.Rigidbody.rotation)).ToArray();
            level.SampleWheel(hand,false,0,false,0,default,true,.4f);
            level.SampleWheel(hand,true,.025f,true,1,new Pose(pose.position+Vector3.right,Quaternion.Euler(0,90,0)),true,.5f);
            Assert.IsFalse(level.Rotating); Assert.IsFalse(workshop.World.HasHolds);
            for(int i=0;i<bodies.Length;i++)
            {
                Assert.Less(Vector3.Distance(poses[i].position,bodies[i].Rigidbody.position),.0001f);
                Assert.Less(Quaternion.Angle(poses[i].rotation,bodies[i].Rigidbody.rotation),.001f);
            }
            pose.position=level.RotationWheel.transform.position+level.Course.WorkbenchFrame.right*DownhillRotationWheel.Radius;
            Assert.IsTrue(level.SampleWheel(hand,true,.014f,false,0,pose,true,.6f));
            level.SampleWheel(hand,false,0,false,0,default,true,.94f);
            Assert.IsTrue(level.Rotating);
            level.SampleWheel(hand,false,0,false,0,default,true,.96f);
            Assert.IsFalse(level.Rotating); Assert.IsFalse(workshop.World.HasHolds);
            Assert.IsFalse(level.SampleWheel(hand,true,.014f,false,0,pose,true,1f));
            level.SampleWheel(hand,true,.025f,false,0,pose,true,1.1f);
            Assert.IsTrue(level.SampleWheel(hand,true,.014f,false,0,pose,true,1.2f));
            level.EndRotation(hand);
        }
        [Test] public void InventoryCoreAndWheelGeometry()
        {
            Assert.AreEqual(8,level.Kit.Count); Assert.AreEqual(8,workshop.World.Parts.Count);
            Assert.IsTrue(level.Core.MainBody.Anchored);
            Assert.That(level.Core.MainBody.Rigidbody.mass,Is.EqualTo(.22f));
            Assert.Less(Vector3.Distance(level.Core.GetComponentInChildren<BoxCollider>().bounds.size,BuildingGeometry.SandboxCoreSize),.00001f);
            Assert.AreEqual(8,level.Core.Sockets.Length);
            Assert.IsTrue(level.Core.Sockets.All(s=>Mathf.Abs((s.transform.localRotation*Vector3.forward).y)<.001f));
            foreach(var socket in level.Core.Sockets) { Assert.That(socket.transform.localPosition.y,Is.Zero); }
            Assert.IsFalse(workshop.World.TryGrab(1,level.Core,new Pose(level.Core.MainBody.Rigidbody.position,Quaternion.identity)));
            Assert.AreEqual(2,level.Kit.Count(p=>p.Kind==PartKind.DownhillSmallWheel));
            Assert.AreEqual(2,level.Kit.Count(p=>p.Kind==PartKind.DownhillLargeWheel));
            foreach(var p in level.Kit.Where(p=>DownhillParts.IsWheel(p.Kind)))
            {
                Assert.IsNull(p.GetComponent<WheelMotor>()); var hinge=p.GetComponentInChildren<HingeJoint>(); Assert.IsFalse(hinge.useMotor);
                Assert.Less((p.FindBody("hub").Rigidbody.position-p.FindBody("tire").Rigidbody.position).magnitude,.003f);
                Assert.AreEqual(Vector3.one,p.FindBody("tire").transform.localScale);
            }
        }
        [UnityTest] public IEnumerator PlacementUsesTrackedFloorAndLocksAfterCalibration()
        {
            var head=new GameObject("Test head").transform;
            try
            {
                head.SetPositionAndRotation(new Vector3(2,3.1f,-4),Quaternion.Euler(24,123,0));
                workshop.PositionAroundPlayer(head,1.5f);
                var forward=Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;
                var expected=new Vector3(head.position.x,1.5f,head.position.z)+forward*.65f;
                Assert.Less(Vector3.Distance(workshop.Arena.position,expected),.0001f);
                Assert.That(level.Core.MainBody.Rigidbody.position.y,Is.EqualTo(2.4f).Within(.0001f));
                Assert.That(workshop.PlacementSurfaceHeight,Is.EqualTo(2.2f).Within(.0001f));
                foreach(var name in new[]{"Worktop","Sandbox frame"})
                {
                    var c=level.Course.GetComponentsInChildren<Collider>().Single(c=>c.name==name);
                    Assert.That(c.bounds.max.y,Is.EqualTo(2.2f).Within(.0001f),name);
                }
                head.position+=new Vector3(1,1,1); head.rotation=Quaternion.Euler(0,270,0);
                workshop.PositionAroundPlayer(head,2.5f); yield return null;
                Assert.Less(Vector3.Distance(workshop.Arena.position,expected),.0001f);
                workshop.ToggleReposition(); workshop.PlaceArena(new Vector3(-3,99,2),Quaternion.Euler(30,240,10));
                Assert.That(workshop.Arena.position.y,Is.EqualTo(1.5f));
                Assert.That(Vector3.Angle(workshop.Arena.up,Vector3.up),Is.LessThan(.001f));
                Assert.That(level.Core.MainBody.Rigidbody.position.y,Is.EqualTo(2.4f).Within(.0001f));
                SetHeading(level); Assert.IsTrue(level.TryStart());
                var local=level.Course.SandboxFrame.InverseTransformPoint(level.SimulationCore.MainBody.Rigidbody.position);
                Assert.That(local.z,Is.EqualTo(settings.StartPosition.z).Within(.001f));
                yield return WaitForPhysics();
                // Both terminal checks must use the moved sandbox frame.
                level.SimulationCore.MainBody.Rigidbody.position=level.Course.SandboxFrame.TransformPoint(settings.FinishBounds.center);
                yield return new WaitForFixedUpdate(); yield return null;
                Assert.AreEqual(DownhillState.Success,level.State);
                Assert.IsTrue(level.TryReturn()); Assert.IsTrue(level.TryStart());
                yield return WaitForPhysics();
                level.SimulationCore.MainBody.Rigidbody.position=level.Course.SandboxFrame.TransformPoint(new Vector3(0,.5f,.65f));
                yield return new WaitForFixedUpdate(); yield return null;
                Assert.AreEqual(DownhillState.Failed,level.State);
            }
            finally { Object.DestroyImmediate(head.gameObject); }
        }
        [TestCase(0,0)] [TestCase(-.4f,90)] [TestCase(2.2f,215)]
        public void CoreCentersAheadAtDifferentFloorsAndHeadings(float floor,float heading)
        {
            var head=new GameObject("Calibration head").transform;
            try
            {
                head.SetPositionAndRotation(new Vector3(3,floor+1.6f,-2),Quaternion.Euler(25,heading,0));
                workshop.PositionAroundPlayer(head,floor);
                var forward=Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;
                var expected=new Vector3(head.position.x,floor+.9f,head.position.z)+forward*.65f;
                Assert.Less(Vector3.Distance(expected,level.Core.MainBody.Rigidbody.position),.0001f);
                var pad=level.Presentation.WorkbenchVisuals.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Centered grid pad");
                var wheel=level.RotationWheel.transform.position;
                Assert.That(wheel.x,Is.EqualTo(pad.position.x).Within(.0001f));
                Assert.That(wheel.z,Is.EqualTo(pad.position.z).Within(.0001f));
                Assert.That(pad.position.y-wheel.y,Is.EqualTo(.041f).Within(.0001f));
                Assert.Less(Vector3.Angle(level.Course.WorkbenchFrame.forward,forward),.001f);
            }
            finally { Object.DestroyImmediate(head.gameObject); }
        }
        [Test] public void TrayClearanceAndCourseDimensions()
        {
            var top=level.Course.GetComponentsInChildren<Collider>().Single(c=>c.name=="Worktop").bounds;
            Assert.Less(Vector3.Distance(top.size,Vector3.Scale(settings.WorkbenchSize,new Vector3(.5f,1,.5f))),.00001f);
            var sandbox=level.Course.GetComponentsInChildren<Collider>().Single(c=>c.name=="Sandbox frame").bounds;
            Assert.That(sandbox.size.x,Is.EqualTo(2.85f*settings.PresentationScale).Within(.0001f));
            Assert.That(sandbox.size.z,Is.EqualTo(.78f*settings.PresentationScale).Within(.0001f));
            Assert.Greater(sandbox.min.z,top.max.z);
            var parts=level.Kit.Skip(1).ToArray();
            for(int i=0;i<parts.Length;i++)
            {
                var colliders=parts[i].Bodies.SelectMany(b=>b.Colliders).ToArray();
                for(int j=i+1;j<parts.Length;j++) foreach(var a in colliders) foreach(var b in parts[j].Bodies.SelectMany(x=>x.Colliders))
                    Assert.IsFalse(Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,b,b.transform.position,b.transform.rotation,out _,out float depth)&&depth>.001f,parts[i].Kind+" overlaps "+parts[j].Kind);
                Assert.IsTrue(parts[i].MainBody.Rigidbody.isKinematic);
            }
        }
        [UnityTest] public IEnumerator TableButtonStartsReturnsAndDebounces()
        {
            Assert.IsFalse(((IBuildModeTarget)level).SupportsModeGesture);
            Assert.IsNull(level.GestureFeedback);
            var button=level.Course.ModeButton;
            button.Press(); Assert.IsFalse(level.IsBuilding);
            button.Press(); Assert.IsFalse(level.IsBuilding,"Duplicate press must be debounced.");
            yield return new WaitForSecondsRealtime(.5f);
            level.Presentation.BackButton.Press(); Assert.IsTrue(level.IsBuilding);
        }
        [Test] public void LooseReleaseFloatsWithoutAutomaticReturn()
        {
            var p=level.Kit[5]; var rb=p.MainBody.Rigidbody; var home=rb.position;
            Assert.IsTrue(workshop.World.TryGrab(1,p,new Pose(rb.position,rb.rotation)));
            workshop.World.SetHoldPose(1,new Pose(home+Vector3.up*.1f,rb.rotation)); workshop.World.Release(1,false);
            var released=rb.position; level.TickReturns(100);
            Assert.Less(Vector3.Distance(released,rb.position),.0001f);
            Assert.IsTrue(rb.isKinematic); Assert.IsFalse(rb.useGravity);
            Assert.IsTrue(workshop.World.TryGrab(1,p,new Pose(rb.position,rb.rotation))); workshop.World.Release(1,false);
        }
        [Test] public void AttachedWheelNeverRespawnsAndCannotRemoveCore()
        {
            var p=level.Kit.First(p=>p.Kind==PartKind.DownhillLargeWheel);
            Assert.IsTrue(workshop.World.TryGrab(1,p,new Pose(p.MainBody.Rigidbody.position,Quaternion.identity))); workshop.World.Release(1,false);
            AddWheel(workshop,p,1,0); var position=p.MainBody.Rigidbody.position;
            level.TickReturns(11); Assert.Less(Vector3.Distance(position,p.MainBody.Rigidbody.position),.0001f);
            Assert.IsFalse(workshop.World.TryDetachPart(level.Core));
        }
        [UnityTest] public IEnumerator TransferScalesEveryBodyAndRestoresWithoutDrift()
        {
            var wheels=level.Kit.Where(p=>p.Kind==PartKind.DownhillLargeWheel).ToArray(); AddWheel(workshop,wheels[0],1,0); AddWheel(workshop,wheels[1],-1,0);
            SetHeading(level); var originals=workshop.World.GetConnectedParts(level.Core.InstanceId).SelectMany(p=>p.Bodies).ToArray(); var poses=originals.Select(b=>new Pose(b.Rigidbody.position,b.Rigidbody.rotation)).ToArray();
            for(int cycle=0;cycle<3;cycle++)
            {
                Assert.IsTrue(level.TryStart()); Assert.AreEqual(3,level.SimulationWorld.Parts.Count); Assert.AreEqual(2,level.SimulationWorld.Connections.Count);
                Assert.Less(Vector3.Distance(level.SimulationCore.GetComponentInChildren<BoxCollider>().transform.localScale,BuildingGeometry.SandboxCoreSize*level.TestScale),.00001f);
                foreach(var socket in level.Core.Sockets)
                {
                    var scaled=level.SimulationCore.FindSocket(socket.Id);
                    Assert.IsNotNull(scaled);
                    Assert.Less(Vector3.Distance(socket.transform.localPosition*level.TestScale,scaled.transform.localPosition),.00001f);
                }
                var tire=level.SimulationWorld.Parts.Values.First(p=>DownhillParts.IsWheel(p.Kind)).FindBody("tire");
                Assert.That(tire.GetComponent<PassiveWheel>().Radius,Is.EqualTo(.13f*level.TestScale).Within(.00001f)); Assert.AreEqual(Vector3.one,tire.transform.localScale);
                Assert.IsFalse(level.TryStart());
                yield return WaitForPhysics();
                yield return new WaitForSeconds(.15f);
                Assert.IsTrue(level.TryReturn()); yield return null;
                for(int i=0;i<originals.Length;i++)
                { Assert.Less(Vector3.Distance(poses[i].position,originals[i].Rigidbody.position),.0001f); Assert.Less(Quaternion.Angle(poses[i].rotation,originals[i].Rigidbody.rotation),.01f); }
                Assert.AreEqual(8,workshop.World.Parts.Count); Assert.IsNull(level.SimulationWorld);
            }
        }
        [Test] public void RotationAndHeldPartGuardsPreserveBuild()
        {
            var point=level.RotationWheel.transform.position+Vector3.right*.17f;
            Assert.IsTrue(level.BeginRotation(2,point)); Assert.IsFalse(level.CanSwitchMode); Assert.IsFalse(level.TryStart()); level.EndRotation(2);
            var part=level.Kit[5];
            Assert.IsTrue(workshop.World.TryGrab(1,part,new Pose(part.MainBody.Rigidbody.position,Quaternion.identity)));
            Assert.IsFalse(level.TryStart()); workshop.World.Release(1,false);
            Assert.IsTrue(level.TryStart()); Assert.IsFalse(workshop.World.EditingEnabled); Assert.IsTrue(level.TryReturn());
        }
        [UnityTest] public IEnumerator SquareCoreReferenceCompletesGravityCourseOnRepeatedAttempts()
        {
            BuildReferenceAxle(workshop,PartKind.DownhillLargeWheel);
            SetHeading(level);
            for(int attempt=0;attempt<3;attempt++)
            {
                Assert.IsTrue(level.TryStart());
                yield return WaitForPhysics();
                float deadline=Time.time+settings.AttemptDuration+1;
                while(level.State==DownhillState.Running && Time.time<deadline)
                    yield return new WaitForFixedUpdate();
                var rb=level.SimulationCore.MainBody.Rigidbody;
                Assert.AreEqual(DownhillState.Success,level.State,$"Attempt {attempt}: ended at {rb.position}, velocity {rb.linearVelocity}");
                Assert.IsTrue(level.TryReturn()); yield return null;
            }
        }
        public static void BuildReferenceAxle(WorkshopBootstrap workshop,PartKind kind)
        {
            var core=workshop.Downhill.Core; var wheels=workshop.Downhill.Kit.Where(p=>p.Kind==kind).ToArray();
            for(int i=0;i<2;i++)
            {
                int side=i==0?1:-1; var wheel=wheels[i];
                var socket=core.Sockets.Where(s=>s.Id.Contains(side>0?"x+":"x-"))
                    .OrderBy(s=>s.Id,System.StringComparer.Ordinal).First();
                var mount=wheel.FindSocket("mount"); var rotation=workshop.Arena.rotation*Quaternion.Euler(0,side>0?0:180,0);
                foreach(var body in wheel.Bodies) { body.Rigidbody.position=socket.WorldPosition-rotation*mount.transform.localPosition; body.Rigidbody.rotation=rotation; body.transform.SetPositionAndRotation(body.Rigidbody.position,rotation); }
                Physics.SyncTransforms();
                Assert.IsTrue(workshop.World.Connect(new ConnectionRecord { id=Guid.NewGuid().ToString("N"),a=new BodyRef(wheel.InstanceId,mount.Body.Id),b=new BodyRef(core.InstanceId,socket.Body.Id),socketA=mount.Id,socketB=socket.Id,anchorA=mount.transform.localPosition,anchorB=socket.transform.localPosition }));
            }
        }
    }
}



using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace PhysicalAssembly.Tests
{
    public sealed class NarrowGapTests
    {
        GameObject root;
        WorkshopBootstrap workshop;
        NarrowGapController level;
        NarrowGapSettings settings;
        [UnitySetUp] public IEnumerator Setup()
        {
            // Navigation tests can leave the car workshop loaded. Its rails and
            // parts must not collide with this independent challenge fixture.
            var fixture=SceneManager.GetSceneByName("Narrow Gap physics fixture");
            if(!fixture.IsValid()) fixture=SceneManager.CreateScene("Narrow Gap physics fixture");
            SceneManager.SetActiveScene(fixture);
            foreach(var sceneName in new[]{"Workshop","CloudGardenDemo","NarrowGap"})
            {
                var scene=SceneManager.GetSceneByName(sceneName);
                if(scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
            settings=UnityEditor.AssetDatabase.LoadAssetAtPath<NarrowGapSettings>("Assets/MetaHackthone/Generated/NarrowGapSettings.asset");
            Assert.NotNull(settings); Assert.NotNull(settings.CoursePrefab);
            root=new GameObject("Narrow Gap test"); root.SetActive(false);
            root.AddComponent<AudioListener>();
            workshop=root.AddComponent<WorkshopBootstrap>(); workshop.ChallengeSettings=settings;
            root.SetActive(true); level=workshop.Challenge;
            yield return null;
        }
        [TearDown] public void Cleanup()
        {
            if(workshop) { foreach(var part in workshop.World.Parts.Values.ToArray()) if(part) Object.DestroyImmediate(part.gameObject); Object.DestroyImmediate(workshop.Arena.gameObject); }
            if(root) Object.DestroyImmediate(root);
        }
        [TestCase(0)] [TestCase(1)] public void BuildingPinchReleasesImmediately(int hand)
        {
            Assert.IsTrue(level.TryEnterBuilding());
            var part=workshop.Factory.Create(PartKind.SmallCube,workshop.Arena.TransformPoint(new Vector3(0,2,0)),Quaternion.identity);
            GrabLifetimeTests.VerifyImmediateRelease(workshop.World,part,hand);
        }
        public static AssemblyPart BuildReference(WorkshopBootstrap workshop,bool wide=false)
        {
            var world=workshop.World; var factory=workshop.Factory; var arena=workshop.Arena;
            foreach(var p in world.Parts.Values.Where(p=>p.Kind!=PartKind.PhysicsSphere && p!=workshop.Challenge.Core).ToArray()) { world.UnregisterPart(p); Object.DestroyImmediate(p.gameObject); }
            Assert.IsTrue(workshop.Challenge.TryEnterBuilding());
            float u=BuildingGeometry.Unit,y=arena.InverseTransformPoint(workshop.Challenge.Core.MainBody.Rigidbody.position).y;
            var parts=new List<AssemblyPart>{workshop.Challenge.Core};
            AssemblyPart Add(PartKind kind,Vector3 local,Quaternion rotation)
            {
                var p=factory.Create(kind,arena.TransformPoint(local),arena.rotation*rotation); parts.Add(p); return p;
            }
            void AddThreeUnitSegment(Vector3 center,Quaternion rotation)
            {
                // Tile the original footprint with the current 1x2 block and a cube.
                // Mirror opposite segments so the extra seams and mass stay balanced.
                if(center.z<0 || (Mathf.Abs(center.z)<.001f && center.x<0)) rotation*=Quaternion.Euler(0,180,0);
                Add(PartKind.Cuboid,center-rotation*Vector3.right*u*.5f,rotation);
                Add(PartKind.SmallCube,center+rotation*Vector3.right*u,rotation);
            }
            for(int side=-1;side<=1;side+=2) { Add(PartKind.SmallCube,new Vector3(0,y,side*u),Quaternion.identity); AddThreeUnitSegment(new Vector3(0,y,side*3*u),Quaternion.Euler(0,90,0)); }
            for(int side=-1;side<=1;side+=2) for(int i=-1;i<=1;i++) AddThreeUnitSegment(new Vector3(side*u,y+u,i*3*u),Quaternion.Euler(0,90,0));
            for(int side=-1;side<=1;side+=2) Add(PartKind.SmallCube,new Vector3(0,y+u,side*4*u),Quaternion.identity);
            Physics.SyncTransforms();
            // Connect touching grid faces, using exactly the production socket graph.
            var connected=new List<AssemblyPart>{parts[0]};
            while(connected.Count<parts.Count)
            {
                bool made=false;
                foreach(var a in parts.Where(p=>!connected.Contains(p)))
                {
                    var pair=(from sa in a.Sockets where !world.SocketOccupied(sa)
                              from b in connected from sb in b.Sockets where !world.SocketOccupied(sb)
                              where Vector3.Dot(sa.Normal,sb.Normal)<-.99f && Vector3.Distance(sa.transform.position,sb.transform.position)<.001f
                              select (sa,sb)).FirstOrDefault();
                    if(!pair.sa) continue;
                    Assert.IsTrue(world.Connect(new ConnectionRecord { id=Guid.NewGuid().ToString("N"),a=new BodyRef(a.InstanceId,pair.sa.Body.Id),b=new BodyRef(pair.sb.Body.Part.InstanceId,pair.sb.Body.Id),socketA=pair.sa.Id,socketB=pair.sb.Id,anchorA=pair.sa.transform.localPosition,anchorB=pair.sb.transform.localPosition }));
                    connected.Add(a); made=true; break;
                }
                Assert.IsTrue(made,"Reference hull has a physically touching free connection.");
            }
            if(wide)
            {
                // An extra outer block makes the hull four units wide.
                var a=parts.First(p=>p.Sockets.Any(s=>Vector3.Dot(s.Normal,arena.right)>.99f && Vector3.Distance(arena.InverseTransformPoint(s.WorldPosition),new Vector3(1.5f*u,y+u,0))<.001f)); var cross=Add(PartKind.SmallCube,new Vector3(2*u,y+u,0),Quaternion.identity);
                var pair=(from sa in cross.Sockets from sb in a.Sockets where !world.SocketOccupied(sb) && Vector3.Dot(sa.Normal,sb.Normal)<-.99f && Vector3.Distance(sa.transform.position,sb.transform.position)<.001f select (sa,sb)).First();
                Assert.IsTrue(world.Connect(new ConnectionRecord { id=Guid.NewGuid().ToString("N"),a=new BodyRef(cross.InstanceId,pair.sa.Body.Id),b=new BodyRef(a.InstanceId,pair.sb.Body.Id),socketA=pair.sa.Id,socketB=pair.sb.Id,anchorA=pair.sa.transform.localPosition,anchorB=pair.sb.transform.localPosition }));
            }
            Assert.IsTrue(workshop.Challenge.TryEnterLaunch(),workshop.Challenge.DockProblem);
            y=arena.InverseTransformPoint(workshop.Challenge.Core.MainBody.Rigidbody.position).y;
            for(int i=0;i<workshop.Challenge.Spheres.Count;i++)
            {
                var rb=workshop.Challenge.Spheres[i].MainBody.Rigidbody;
                rb.position=arena.TransformPoint(new Vector3(0,y+u*.5f+.025f,(i-2)*.055f)); rb.linearVelocity=rb.angularVelocity=Vector3.zero;
            }
            Physics.SyncTransforms(); return workshop.Challenge.Core;
        }
        AssemblyPart AttachCube(AssemblyPart target,Vector3 direction)
        {
            var part=workshop.Factory.Create(PartKind.SmallCube,target.MainBody.Rigidbody.position+direction*BuildingGeometry.Unit,Quaternion.identity);
            Physics.SyncTransforms();
            var pair=(from a in part.Sockets from b in target.Sockets where !workshop.World.SocketOccupied(b) && Vector3.Dot(a.Normal,b.Normal)<-.99f && Vector3.Distance(a.WorldPosition,b.WorldPosition)<.001f select (a,b)).First();
            Assert.IsTrue(workshop.World.Connect(new ConnectionRecord { id=Guid.NewGuid().ToString("N"),a=new BodyRef(part.InstanceId,pair.a.Body.Id),b=new BodyRef(target.InstanceId,pair.b.Body.Id),socketA=pair.a.Id,socketB=pair.b.Id,anchorA=pair.a.transform.localPosition,anchorB=pair.b.transform.localPosition }));
            return part;
        }
        [UnityTest] public IEnumerator AimKeepsPivotBallsAndJointsAndCancellationRestores()
        {
            BuildReference(workshop); yield return new WaitForSeconds(.5f);
            var bodies=level.CoreParts.SelectMany(p=>p.Bodies).Concat(level.Spheres.Select(p=>p.MainBody)).ToArray();
            var positions=bodies.Select(b=>b.Rigidbody.position).ToArray();
            var rotations=bodies.Select(b=>b.Rigidbody.rotation).ToArray();
            var kinematic=bodies.Select(b=>b.Rigidbody.isKinematic).ToArray();
            int joints=workshop.World.Connections.Count;
            Assert.IsTrue(level.BeginAim());
            level.UpdateAim(Quaternion.AngleAxis(20,Vector3.up)*settings.LaunchVelocity,1);
            Assert.IsFalse(level.AimBlocked);
            Assert.That(Vector3.Distance(level.DockSocket.WorldPosition,workshop.Arena.TransformPoint(new Vector3(0,settings.PadHeight,0))),Is.LessThan(.001f));
            Assert.IsTrue(bodies.All(b=>b.Rigidbody.isKinematic));
            var aimed=bodies.Select(b=>b.Rigidbody.position).ToArray();
            yield return new WaitForSeconds(.15f);
            for(int i=0;i<bodies.Length;i++) Assert.That(Vector3.Distance(aimed[i],bodies[i].Rigidbody.position),Is.LessThan(.0001f));
            Assert.AreEqual(joints,workshop.World.Connections.Count);
            level.CancelAim();
            for(int i=0;i<bodies.Length;i++)
            {
                Assert.That(Vector3.Distance(positions[i],bodies[i].Rigidbody.position),Is.LessThan(.0001f));
                Assert.That(Quaternion.Angle(rotations[i],bodies[i].Rigidbody.rotation),Is.LessThan(.01f));
                Assert.AreEqual(kinematic[i],bodies[i].Rigidbody.isKinematic);
                if(!kinematic[i]) Assert.AreEqual(Vector3.zero,bodies[i].Rigidbody.linearVelocity);
            }
            Assert.IsFalse(level.Course.Trajectory.gameObject.activeSelf);
        }
        [UnityTest] public IEnumerator AimRejectsBlockedRotationAndRetainsDisplayedVelocity()
        {
            Assert.IsTrue(level.BeginAim()); var velocity=level.AimVelocity; var position=level.Core.transform.position;
            var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.transform.position=position; obstacle.transform.localScale=Vector3.one*.3f; Physics.SyncTransforms();
            level.UpdateAim(Vector3.right+Vector3.up,1);
            Assert.IsTrue(level.AimBlocked); Assert.AreEqual(velocity,level.AimVelocity); Assert.AreEqual(position,level.Core.transform.position);
            Object.DestroyImmediate(obstacle); level.CancelAim(); yield return null;
        }
        [UnityTest] public IEnumerator AimedLaunchUsesCapturedLoadAndRetryRestoresBeforeAim()
        {
            BuildReference(workshop); yield return new WaitForSeconds(.5f);
            var original=level.Core.MainBody.Rigidbody.position;
            Assert.IsTrue(level.BeginAim()); level.UpdateAim(Quaternion.AngleAxis(30,Vector3.up)*settings.LaunchVelocity,1);
            Assert.IsFalse(level.AimBlocked); var velocity=level.AimVelocity;
            var line=level.Course.Guide;
            Assert.That(Vector3.Distance(line.GetPosition(0),level.Core.MainBody.Rigidbody.position),Is.LessThan(.0001f));
            Assert.That(line.widthMultiplier,Is.EqualTo(.004f));
            Assert.That(line.colorGradient.Evaluate(.5f).a,Is.EqualTo(1).Within(.001f));
            Assert.That(line.colorGradient.Evaluate(.75f).a,Is.EqualTo(.5f).Within(.001f));
            Assert.That(line.colorGradient.Evaluate(1).a,Is.EqualTo(0).Within(.001f));
            Assert.IsTrue(level.TryLaunch()); Assert.IsFalse(level.Course.Trajectory.gameObject.activeSelf);
            // Invoke the warmup boundary without advancing simulation: exact velocity is observable.
            level.SendMessage("FixedUpdate"); level.SendMessage("FixedUpdate");
            Assert.AreEqual(5,level.LaunchedBallCount);
            Assert.That(Vector3.Distance(velocity,level.Core.MainBody.Rigidbody.linearVelocity),Is.LessThan(.0001f));
            Assert.IsTrue(level.Retry()); Assert.That(Vector3.Distance(original,level.Core.MainBody.Rigidbody.position),Is.LessThan(.0001f));
        }
        [UnityTest] public IEnumerator AimIsClearedByBuildingResetAndArenaMovement()
        {
            Assert.IsTrue(level.BeginAim()); Assert.IsTrue(level.TryEnterBuilding()); Assert.IsFalse(level.IsAiming);
            Assert.IsTrue(level.TryEnterLaunch()); Assert.IsTrue(level.BeginAim()); Assert.IsTrue(level.ResetLevel()); Assert.IsFalse(level.IsAiming);
            Assert.IsTrue(level.BeginAim()); workshop.ToggleReposition(); Assert.IsFalse(level.IsAiming);
            workshop.PlaceArena(new Vector3(1,0,2),Quaternion.Euler(0,130,0));
            Assert.IsTrue(level.BeginAim()); level.UpdateAim(workshop.Arena.TransformDirection(settings.LaunchVelocity),1);
            Assert.IsFalse(level.AimBlocked); level.CancelAim(); yield return null;
        }
        [UnityTest] public IEnumerator FiringPinchCannotSelectMenuOrGrabUntilItOpens()
        {
            var gun=new LaunchGesture.HandSample { Valid=true,Fingers=Fingers.Thumb|Fingers.Index,TipDistance=.06f };
            var folded=new LaunchGesture.HandSample { Valid=true,Fingers=Fingers.Index,TipDistance=.02f };
            var g=level.LaunchGesture;
            var selector=root.AddComponent<PhysicalAssembly.MetaXR.MetaPinchSelector>();
            selector.SuppressSelection=()=>g.BlocksInput; int presses=0; selector.WhenSelected+=()=>presses++;
            var driver=new HandGrabDriver(workshop.World,0); var ball=level.Spheres[0]; var pose=new Pose(ball.MainBody.Rigidbody.position,Quaternion.identity);
            driver.Sample(true,.06f,false,pose,ball,true,0); selector.ApplySample(true,.06f,false);
            g.Sample(gun,default,true,0); g.Sample(gun,default,true,1); Assert.IsTrue(level.BeginAim());
            selector.ApplySample(true,.02f,true);
            Assert.AreEqual(LaunchGesture.Change.Fired,g.Sample(folded,default,true,1.1f));
            // Even a direct grab request is refused before the launch state locks editing.
            driver.Sample(true,.014f,true,pose,ball,true,1.1f);
            Assert.IsNull(workshop.World.HeldPart(0)); Assert.AreEqual(0,presses);
            Assert.IsTrue(level.TryLaunch()); level.SendMessage("FixedUpdate"); level.SendMessage("FixedUpdate"); Assert.IsTrue(level.Retry());
            selector.ApplySample(true,.02f,true); Assert.AreEqual(0,presses);
            g.Sample(gun,default,false,2); selector.ApplySample(true,.06f,false); selector.ApplySample(true,.02f,true);
            Assert.AreEqual(1,presses,"An open pinch rearms normal menu selection.");
            yield return null;
        }
        [UnityTest] public IEnumerator DisablingDuringWarmupRestoresCapturedSetup()
        {
            var position=level.Core.MainBody.Rigidbody.position;
            Assert.IsTrue(level.BeginAim()); level.UpdateAim(Vector3.up+Vector3.right,1); Assert.IsTrue(level.TryLaunch());
            level.enabled=false;
            Assert.IsFalse(level.IsAiming); Assert.AreEqual(ChallengeState.LaunchReady,level.State);
            Assert.That(Vector3.Distance(position,level.Core.MainBody.Rigidbody.position),Is.LessThan(.0001f));
            level.enabled=true; yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.AreEqual(ChallengeState.LaunchReady,level.State);
        }
        [UnityTest] public IEnumerator CoreFloatsAndLowestSocketDocksWithPhysicsRestored()
        {
            Assert.AreEqual(ChallengeState.LaunchReady,level.State); Assert.AreEqual(6,level.Core.Sockets.Length);
            var initial=level.Core.MainBody.Rigidbody.position;
            Assert.IsTrue(level.TryEnterBuilding()); Assert.AreEqual(9,level.RemainingBuildCycles);
            var bottom=AttachCube(level.Core,Vector3.down); var rb=bottom.MainBody.Rigidbody;
            Assert.IsFalse(rb.useGravity); Assert.AreEqual(settings.BuildDamping,rb.linearDamping);
            yield return new WaitForSeconds(.25f);
            Assert.Greater(level.Core.MainBody.Rigidbody.position.y-initial.y,.34f);
            level.UpdateDockCandidate(); Assert.AreEqual(bottom,level.DockSocket.Body.Part);
            Assert.IsTrue(level.TryEnterLaunch(),level.DockProblem);
            Assert.IsTrue(rb.useGravity); Assert.Less(rb.linearDamping,1);
            Assert.Less(Vector3.Distance(level.DockSocket.WorldPosition,workshop.Arena.TransformPoint(new Vector3(0,settings.PadHeight,0))),.002f);
        }
        [UnityTest] public IEnumerator RightReleaseAttachesButLoosePartsCannotJoinEachOther()
        {
            Assert.IsTrue(level.TryEnterBuilding());
            var core=level.Core; var cube=workshop.Factory.Create(PartKind.SmallCube,core.MainBody.Rigidbody.position+Vector3.right*BuildingGeometry.Unit,Quaternion.identity);
            var pose=new Pose(cube.MainBody.Rigidbody.position,Quaternion.identity);
            var driver=new HandGrabDriver(workshop.World,1);
            driver.Sample(true,.06f,false,pose,cube,true,0);
            driver.Sample(true,.014f,false,pose,cube,true,.01f);
            Assert.AreSame(cube,workshop.World.HeldPart(1));
            Assert.IsTrue(workshop.World.Query(cube).IsValid);
            driver.Sample(true,.025f,true,pose,null,true,.011f,1);
            Assert.IsNull(workshop.World.HeldPart(1));
            yield return new WaitForSeconds(.4f); Assert.IsTrue(level.IsCoreConnected(cube));
            var loose=workshop.Factory.Create(PartKind.SmallCube,new Vector3(1,1,0),Quaternion.identity);
            var second=workshop.Factory.Create(PartKind.SmallCube,new Vector3(1+BuildingGeometry.Unit,1,0),Quaternion.identity);
            Assert.IsFalse(workshop.World.Query(second).IsValid,"A separate assembly must not form away from the core.");
            Assert.IsFalse(level.CanAttach(second,loose,loose.Sockets[0]));
        }
        [UnityTest] public IEnumerator DockingRejectsObstaclesAndKeepsTheBuildCycle()
        {
            Assert.IsTrue(level.TryEnterBuilding());
            var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube); obstacle.transform.SetParent(workshop.Arena,false);
            obstacle.transform.localPosition=new Vector3(0,settings.PadHeight+.04f,0); obstacle.transform.localScale=Vector3.one*.10f;
            Physics.SyncTransforms(); Assert.IsFalse(level.TryEnterLaunch()); Assert.IsTrue(level.IsBuilding); Assert.AreEqual(1,level.BuildCyclesUsed);
            Object.DestroyImmediate(obstacle); Physics.SyncTransforms(); Assert.IsTrue(level.TryEnterLaunch());
            yield return null;
        }
        [UnityTest] public IEnumerator LowFpsFallsBackToMeshAndStaysThereAfterRecovery()
        {
            var feedback=level.GestureFeedback;
            for(int i=0;i<250;i++) feedback.ObserveFrame(1f/90,true);
            Assert.IsFalse(feedback.MeshOnly);
            for(int i=0;i<150;i++) feedback.ObserveFrame(1f/60,true);
            Assert.IsTrue(feedback.MeshOnly);
            for(int i=0;i<250;i++) feedback.ObserveFrame(1f/90,true);
            Assert.IsTrue(feedback.MeshOnly); yield return null;
        }
        [UnityTest] public IEnumerator LeftRemovalPreservesCoreConnectivityAndRightHandCannotRemove()
        {
            Assert.IsTrue(level.TryEnterBuilding()); var bridge=AttachCube(level.Core,Vector3.right); var leaf=AttachCube(bridge,Vector3.right);
            Assert.IsFalse(level.CanDetach(level.Core)); Assert.IsFalse(level.CanDetach(bridge)); Assert.IsTrue(level.CanDetach(leaf));
            Assert.IsFalse(workshop.World.TryGrab(1,leaf,new Pose(leaf.MainBody.Rigidbody.position,Quaternion.identity)));
            Assert.IsFalse(workshop.World.TryGrab(0,bridge,new Pose(bridge.MainBody.Rigidbody.position,Quaternion.identity)));
            var pose=new Pose(leaf.MainBody.Rigidbody.position+Vector3.right*.05f,Quaternion.identity);
            var driver=new HandGrabDriver(workshop.World,0);
            driver.Sample(true,.06f,false,pose,leaf,true,0);
            driver.Sample(true,.014f,false,pose,leaf,true,.01f);
            Assert.AreSame(leaf,workshop.World.HeldPart(0));
            driver.Sample(true,.025f,true,pose,null,true,.011f,1);
            Assert.IsNull(workshop.World.HeldPart(0));
            Assert.IsFalse(level.IsCoreConnected(leaf)); Assert.IsTrue(level.IsCoreConnected(bridge));
            yield return null; Assert.IsFalse(level.IsCoreConnected(leaf),"Left-hand release must not reattach.");
        }
        [UnityTest] public IEnumerator TenBuildCyclesPermitLastLaunchBeforeAutomaticReset()
        {
            Assert.IsTrue(level.TryEnterBuilding()); AttachCube(level.Core,Vector3.up); Assert.IsTrue(level.TryEnterLaunch());
            for(int i=1;i<10;i++) { Assert.IsTrue(level.TryEnterBuilding()); Assert.IsTrue(level.TryEnterLaunch()); }
            Assert.AreEqual(0,level.RemainingBuildCycles); Assert.IsFalse(level.TryEnterBuilding());
            var core=level.Core; Assert.IsTrue(level.TryLaunch());
            yield return new WaitForSeconds(settings.AttemptDuration+.5f);
            Assert.AreEqual(ChallengeState.Resolved,level.State); Assert.AreSame(core,level.Core); Assert.IsFalse(level.CanRetry);
            yield return new WaitForSeconds(5.1f);
            Assert.AreEqual(10,level.RemainingBuildCycles); Assert.AreEqual(ChallengeState.LaunchReady,level.State); Assert.AreNotSame(core,level.Core);
        }
        [UnityTest] public IEnumerator ReferenceContainerDeliversFiveAndRetryPreservesIdentity()
        {
            var hull=BuildReference(workshop);
            yield return new WaitForSeconds(.4f);
            Assert.AreEqual(5,level.Spheres.Count); Assert.AreEqual(20,workshop.World.Connections.Count);
            var ids=workshop.World.Parts.Keys.OrderBy(x=>x).ToArray(); var edges=workshop.World.Connections.Select(c=>c.id).ToArray();
            Assert.IsTrue(level.TryLaunch()); Assert.IsFalse(level.TryLaunch());
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.AreEqual(5,level.LaunchedBallCount,"Every ball in the reference container must be supported at launch.");
            var deadline=Time.time+9;
            while(level.State==ChallengeState.InFlight && Time.time<deadline) yield return new WaitForFixedUpdate();
            var positions=string.Join("; ",level.Spheres.Select(p=>$"{p.MainBody.Rigidbody.position:F3} v={p.MainBody.Rigidbody.linearVelocity:F3}"));
            Assert.IsTrue(level.Won,$"Reference container should deliver five spheres: saved={level.SavedCount}, positions={positions}, hull={hull.MainBody.Rigidbody.position}");
            Assert.IsTrue(level.Course.SuccessStars.gameObject.activeSelf);
            Assert.IsTrue(level.Retry());
            CollectionAssert.AreEqual(ids,workshop.World.Parts.Keys.OrderBy(x=>x).ToArray()); CollectionAssert.AreEqual(edges,workshop.World.Connections.Select(c=>c.id));
            Assert.IsTrue(workshop.World.EditingEnabled); Assert.AreEqual(0,level.SavedCount);
            Assert.Less(Mathf.Abs(hull.MainBody.Rigidbody.position.z),.01f);
            foreach(var p in workshop.World.Parts.Values) foreach(var b in p.Bodies) { Assert.AreEqual(Vector3.zero,b.Rigidbody.linearVelocity); Assert.AreEqual(Vector3.zero,b.Rigidbody.angularVelocity); }
            Assert.IsFalse(workshop.World.TryGrab(99,hull,new Pose(hull.MainBody.Rigidbody.position,Quaternion.identity)), "The docked core cannot be grabbed.");
        }
        [UnityTest] public IEnumerator ResetLevelButtonReplacesConnectedKitDuringFlightAtCurrentArenaPose()
        {
            BuildReference(workshop);
            workshop.ToggleReposition(); workshop.PlaceArena(new Vector3(3,0,-2),Quaternion.Euler(0,68,0));
            var oldParts=workshop.World.Parts.Values.ToArray();
            var oldIds=workshop.World.Parts.Keys.ToArray();
            var arenaPosition=workshop.Arena.position; var arenaRotation=workshop.Arena.rotation;
            Assert.IsTrue(level.TryLaunch()); yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            yield return new WaitForSeconds(.15f);
            var reset=workshop.Menu.GetComponentsInChildren<WorkshopButton>().Single(b=>b.name=="Reset Level");
            Assert.IsTrue(reset.Interactable); reset.Press();
            Assert.AreEqual(ChallengeState.LaunchReady,level.State); Assert.IsTrue(workshop.World.EditingEnabled);
            Assert.AreEqual(38,workshop.World.Parts.Count); Assert.AreEqual(5,level.Spheres.Count);
            Assert.AreEqual(16,workshop.World.Parts.Values.Count(p=>p.Kind==PartKind.Cuboid));
            Assert.AreEqual(13,workshop.World.Parts.Values.Count(p=>p.Kind==PartKind.SmallCube));
            Assert.AreEqual(4,workshop.World.Parts.Values.Count(p=>p.Kind==PartKind.Beam));
            Assert.AreEqual(0,workshop.World.Connections.Count);
            Assert.IsFalse(workshop.World.Parts.Keys.Intersect(oldIds).Any());
            Assert.IsFalse(level.CanRetry); Assert.IsFalse(level.Retry()); Assert.IsFalse(level.Won); Assert.AreEqual(0,level.SavedCount);
            Assert.IsFalse(level.Course.Trajectory.gameObject.activeSelf,"Trajectory is visible only during aiming."); Assert.IsFalse(level.Course.SuccessStars.gameObject.activeSelf);
            Assert.AreEqual(arenaPosition,workshop.Arena.position); Assert.AreEqual(arenaRotation,workshop.Arena.rotation);
            for(int i=0;i<5;i++)
            {
                var rb=level.Spheres[i].MainBody.Rigidbody;
                Assert.Less(Vector3.Distance(workshop.Arena.InverseTransformPoint(rb.position),new Vector3(-.85f+i*.065f,.56f,.29f)),.001f);
                Assert.AreEqual(Vector3.zero,rb.linearVelocity); Assert.AreEqual(Vector3.zero,rb.angularVelocity);
            }
            Assert.NotNull(level.Core,"Reset restores the docked core.");
            yield return null;
            Assert.IsTrue(oldParts.All(p=>!p),"Replaced parts must be destroyed, not left in physics.");
            BuildReference(workshop); Assert.IsTrue(level.TryLaunch()); yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.IsTrue(level.Retry(),"The replacement kit must support a new attempt.");
        }
        [UnityTest] public IEnumerator ResetLevelRejectsHeldPartsAndPlacementAndCancelsPendingLaunch()
        {
            Assert.IsTrue(level.TryEnterBuilding());
            var part=workshop.World.Parts.Values.First(p=>p.Kind==PartKind.Cuboid);
            Assert.IsTrue(workshop.World.TryGrab(99,part,new Pose(part.MainBody.Rigidbody.position,Quaternion.identity)));
            Assert.IsFalse(level.ResetLevel()); Assert.IsTrue(workshop.World.Parts.ContainsKey(part.InstanceId));
            workshop.World.ReleaseAll(); workshop.ToggleReposition();
            Assert.IsFalse(level.ResetLevel()); Assert.AreEqual(38,workshop.World.Parts.Count);
            workshop.ToggleReposition();
            // Recover a ball lost during building, before any launch snapshot exists.
            var lost=level.Spheres[0]; lost.MainBody.Rigidbody.position=Vector3.down*100;
            Assert.IsTrue(level.ResetLevel()); Assert.AreEqual(5,level.Spheres.Count);
            yield return null;
            Assert.IsFalse(lost);
            BuildReference(workshop); Assert.IsTrue(level.TryLaunch());
            Assert.IsTrue(level.ResetLevel(),"Cancel a queued launch before its first physics step.");
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.AreEqual(ChallengeState.LaunchReady,level.State); Assert.IsFalse(level.CanRetry);
            Assert.AreEqual(38,workshop.World.Parts.Count); Assert.AreEqual(5,level.Spheres.Count);
            foreach(var sphere in level.Spheres) Assert.Less(sphere.MainBody.Rigidbody.linearVelocity.z,.01f);
        }
        [UnityTest] public IEnumerator LaunchExpandsWholeAssemblyAndLocksEditingUntilRetry()
        {
            var hull=BuildReference(workshop); var world=workshop.World;
            Assert.IsTrue(world.TryGrab(99,level.Spheres[0],new Pose(level.Spheres[0].MainBody.Rigidbody.position,Quaternion.identity)));
            Assert.IsFalse(level.TryLaunch()); world.ReleaseAll();
            var offpad=workshop.Factory.Create(PartKind.SmallCube,workshop.Arena.TransformPoint(new Vector3(1,.7f,0)),Quaternion.identity);
            offpad.MainBody.Rigidbody.useGravity=false;
            Assert.IsTrue(level.TryLaunch()); yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.AreEqual(5,level.LaunchedBallCount,"All supported balls must receive launch velocity.");
            Assert.IsFalse(world.TryGrab(99,hull,new Pose(hull.MainBody.Rigidbody.position,Quaternion.identity)));
            Assert.IsFalse(world.TryDetachPart(hull)); Assert.Throws<InvalidOperationException>(()=>workshop.Factory.Create(PartKind.SmallCube,Vector3.zero,Quaternion.identity));
            foreach(var part in world.GetConnectedParts(hull.InstanceId)) Assert.Greater(part.MainBody.Rigidbody.linearVelocity.z,2,"Entire hull must launch.");
            Assert.Less(offpad.MainBody.Rigidbody.linearVelocity.magnitude,.01f,"Off-pad stock does not launch.");
            Assert.IsTrue(level.Retry()); Assert.IsFalse(level.Retry()); Assert.AreEqual(ChallengeState.LaunchReady,level.State);
        }
        [UnityTest] public IEnumerator RelocatedCourseRetryRestoresLatestSetup()
        {
            var hull=BuildReference(workshop);
            workshop.ToggleReposition(); workshop.PlaceArena(new Vector3(3,0,-2),Quaternion.Euler(0,68,0));
            var expected=hull.MainBody.Rigidbody.position;
            Assert.IsTrue(level.TryLaunch()); yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            yield return new WaitForSeconds(.25f); Assert.IsTrue(level.Retry());
            Assert.Less(Vector3.Distance(expected,hull.MainBody.Rigidbody.position),.005f);
            workshop.ToggleReposition(); workshop.PlaceArena(new Vector3(2,0,1),Quaternion.Euler(0,30,0));
            expected=hull.MainBody.Rigidbody.position;
            Assert.IsTrue(level.TryLaunch()); yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate(); yield return new WaitForSeconds(.15f);
            Assert.IsTrue(level.Retry()); Assert.Less(Vector3.Distance(expected,hull.MainBody.Rigidbody.position),.005f);
        }
        [UnityTest] public IEnumerator BallsPlacedOnTargetBeforeLaunchCannotScore()
        {
            BuildReference(workshop);
            foreach(var sphere in level.Spheres) sphere.MainBody.Rigidbody.position=workshop.Arena.TransformPoint(new Vector3(0,settings.PadHeight+.03f,settings.TargetDistance));
            Assert.IsTrue(level.TryLaunch()); yield return new WaitForSeconds(1);
            Assert.AreEqual(0,level.SavedCount); Assert.IsFalse(level.Won); Assert.IsTrue(level.Retry());
        }
        [UnityTest] public IEnumerator WideHullPhysicallyHitsStakes()
        {
            var hull=BuildReference(workshop,true);
            var hullColliders=new HashSet<Collider>(level.CoreParts.SelectMany(p=>p.Bodies).SelectMany(b=>b.Colliders));
            var stakes=level.Course.GetComponentsInChildren<BoxCollider>().Where(c=>c.name=="Solid wooden stake").ToArray();
            Assert.AreEqual(2,stakes.Length);
            var probes=stakes.Select(c=>c.gameObject.AddComponent<StakeContactProbe>()).ToArray();
            foreach(var probe in probes) probe.Hull=hullColliders;
            yield return new WaitForSeconds(.4f);
            Assert.IsTrue(level.TryLaunch()); yield return new WaitForSeconds(2f);
            Assert.IsTrue(probes.Any(probe=>probe.Hit),"The connected hull must physically collide with a stake.");
            // The hull can turn while striking a post: its core can pass the
            // post's center plane while the wide branch is still blocked.
            // Require actual contact and failure to clear the post's far face.
            float farFace=stakes.Max(c=>workshop.Arena.InverseTransformPoint(c.transform.TransformPoint(c.center+Vector3.forward*c.size.z*.5f)).z);
            Assert.Less(workshop.Arena.InverseTransformPoint(hull.MainBody.Rigidbody.position).z,farFace,"Over-wide hull should not clear the stakes.");
            // Loose balls can keep flying after their wide container hits the
            // stakes; completion depends on the balls, not the hull position.
            Assert.IsTrue(level.Retry());
        }
        public sealed class StakeContactProbe : MonoBehaviour
        {
            public HashSet<Collider> Hull;
            public bool Hit;
            void OnCollisionEnter(Collision collision) { if(Hull.Contains(collision.collider)) Hit=true; }
        }
        [UnityTest] public IEnumerator RetryButtonStartsDisabledUntilFirstLaunch()
        {
            Assert.IsFalse(level.Retry());
            var retry=workshop.Menu.GetComponentsInChildren<WorkshopButton>().Single(b=>b.name=="Retry");
            Assert.IsFalse(retry.Interactable); Assert.AreEqual(38,workshop.World.Parts.Count);
            BuildReference(workshop); Assert.IsTrue(level.TryLaunch()); yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.IsTrue(retry.Interactable); retry.Press(); Assert.AreEqual(ChallengeState.LaunchReady,level.State);
            Assert.IsFalse(retry.Interactable);
        }
    }
}

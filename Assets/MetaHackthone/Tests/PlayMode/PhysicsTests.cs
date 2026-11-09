using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PhysicalAssembly.Tests
{
    public sealed class PhysicsTests
    {
        [SetUp] public void UseRuntimePhysicsStep() { Time.fixedDeltaTime=1f/60; }
        [UnityTest] public IEnumerator PlatformSupportsPartsWithoutJoiningOrFollowingHead()
        {
            var go=new GameObject("Platform boundary test");
            var workshop=go.AddComponent<WorkshopBootstrap>(); workshop.SpawnOnStart=false;
            var head=new GameObject("Moving head").transform;
            head.position=new Vector3(0,1.7f,0); workshop.PositionAroundPlayer(head);
            var platformPose=new Pose(workshop.Arena.position,workshop.Arena.rotation);
            Assert.IsNull(workshop.Arena.parent);
            Assert.IsEmpty(workshop.Arena.GetComponentsInChildren<Rigidbody>());
            Assert.IsEmpty(workshop.Arena.GetComponentsInChildren<AssemblyBody>());
            var block=workshop.Factory.Create(PartKind.Block,new Vector3(.9f,.7f,0),Quaternion.identity);
            yield return new WaitForSeconds(.5f);
            Assert.Greater(block.MainBody.Rigidbody.position.y,.49f,"Static platform still supports physics parts.");
            Assert.IsFalse(workshop.World.Query(block).IsValid,"Resting on the platform must not offer attachment.");
            for(int i=0;i<10;i++)
            {
                head.position+=new Vector3(.04f,.02f,.03f); head.Rotate(1,5,0);
                workshop.PositionAroundPlayer(head,head.position.y);
                yield return null;
                Assert.AreEqual(platformPose.position,workshop.Arena.position);
                Assert.AreEqual(platformPose.rotation,workshop.Arena.rotation);
            }
            var grip=new Pose(block.MainBody.Rigidbody.position,Quaternion.identity);
            Assert.IsTrue(workshop.World.TryGrab(0,block,grip));
            workshop.World.Release(0,true); yield return new WaitForSeconds(.3f);
            Assert.AreEqual(0,workshop.World.Connections.Count);
            grip.position=block.MainBody.Rigidbody.position;
            Assert.IsTrue(workshop.World.TryGrab(0,block,grip));
            workshop.World.SetHoldPose(0,new Pose(grip.position+Vector3.up*.3f,grip.rotation));
            Assert.That(block.MainBody.Rigidbody.position.y,Is.EqualTo(grip.position.y+.3f).Within(.001f));
            workshop.World.Release(0,false);
            // Even an accidentally registered assembly component under scenery
            // cannot be grabbed, queried, aligned, or joined through a save record.
            var invalid=workshop.Factory.Create(PartKind.Block,block.MainBody.Rigidbody.position+Vector3.right*.12f,Quaternion.identity);
            invalid.transform.SetParent(workshop.Arena,true);
            Assert.IsFalse(workshop.World.TryGrab(1,invalid,grip));
            Assert.IsFalse(workshop.World.Query(block).IsValid);
            var candidate=new AttachmentCandidate(block.MainBody,invalid.MainBody,grip.position,grip.position);
            Assert.IsFalse(workshop.World.TryBeginAssembly(candidate));
            Assert.IsFalse(workshop.World.Connect(new ConnectionRecord { id="environment",a=new BodyRef(block.InstanceId,block.MainBody.Id),b=new BodyRef(invalid.InstanceId,invalid.MainBody.Id) }));
            Object.Destroy(block.gameObject); Object.Destroy(head.gameObject);
            Object.Destroy(workshop.Arena.gameObject); Object.Destroy(go);
            Object.Destroy(GameObject.Find("Attachment preview")); yield return null;
        }
        [UnityTest] public IEnumerator LauncherHitsToyTargetAndUsesReusablePool()
        {
            var go=new GameObject("Weapon test"); var world=go.AddComponent<AssemblyWorld>(); var factory=go.AddComponent<PartFactory>(); factory.World=world;
            var session=go.AddComponent<WorkshopSession>(); session.World=world;
            var chassis=factory.Create(PartKind.Chassis,new Vector3(0,2,-1),Quaternion.identity);
            var weapon=factory.Create(PartKind.Weapon,new Vector3(0,2,0),Quaternion.identity);
            chassis.MainBody.Anchored=true; chassis.MainBody.Rigidbody.isKinematic=true; weapon.MainBody.Anchored=true; weapon.MainBody.Rigidbody.isKinematic=true;
            var target=GameObject.CreatePrimitive(PrimitiveType.Cube); target.transform.position=new Vector3(0,2,1); target.transform.localScale=new Vector3(.5f,.5f,.05f); target.AddComponent<ToyTarget>().Session=session;
            yield return null;
            weapon.GetComponent<VehicleWeapon>().ApplyControl(new AssemblyControlFrame(0,0,true,chassis),Time.fixedDeltaTime);
            yield return new WaitForSeconds(.4f); Assert.AreEqual(1,session.Score);
            weapon.GetComponent<VehicleWeapon>().ApplyControl(new AssemblyControlFrame(0,0,true,chassis),Time.fixedDeltaTime);
            yield return new WaitForSeconds(.4f); Assert.AreEqual(2,session.Score);
            Assert.AreEqual(1,Object.FindObjectsByType<ToyProjectile>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length,"Second shot must reuse the first projectile.");
            Object.Destroy(weapon.gameObject); Object.Destroy(chassis.gameObject); Object.Destroy(target); Object.Destroy(go); yield return null;
        }
        [UnityTest] public IEnumerator WorkshopSceneSpawnsUsableKitAndDesktopUI()
        {
            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Workshop");
            yield return new WaitForSeconds(.4f);
            var workshop=Object.FindFirstObjectByType<WorkshopBootstrap>(); Assert.IsNotNull(workshop);
            Assert.AreEqual(15,workshop.World.Parts.Count); Assert.AreEqual(WorkshopMode.Build,workshop.Session.Mode);
            Assert.AreEqual(16,Object.FindObjectsByType<WorkshopButton>(FindObjectsSortMode.None).Length);
            Assert.NotNull(workshop.Menu.Find("Main Menu"));
            Assert.IsFalse(workshop.Menu.GetComponentsInChildren<WorkshopButton>().Any(b=>b.Command=="Shadow Workshop" || b.Command=="Narrow Gap"));
            foreach(var text in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None)) Assert.IsNotNull(text.font);
            var adapter=workshop.GetComponent<PhysicalAssembly.MetaXR.MetaHandInputAdapter>();
            var locomotors=adapter.XRRig.GetComponentsInChildren<Oculus.Interaction.Locomotion.FirstPersonLocomotor>(true);
            Assert.IsNotEmpty(locomotors,"Regression must exercise the comprehensive rig's bundled VR locomotor.");
            foreach(var locomotor in locomotors)
                Assert.IsFalse(locomotor.gameObject.activeSelf,"VR grounding/capsule movement must be disabled before the headset rig activates.");
            foreach(var node in adapter.XRRig.GetComponentsInChildren<Transform>(true))
                if(node.name=="Locomotor" || node.name.Contains("Locomotion")) Assert.IsFalse(node.gameObject.activeSelf);
            Assert.GreaterOrEqual(adapter.XRRig.GetComponentsInChildren<Oculus.Interaction.Input.Hand>(true).Length,2,"Hand data must be under the registered rig.");
            Assert.GreaterOrEqual(adapter.XRRig.GetComponentsInChildren<Oculus.Interaction.RayInteractor>(true).Length,2,"Both hands need SDK rays.");
            Assert.GreaterOrEqual(adapter.XRRig.GetComponentsInChildren<Oculus.Interaction.PokeInteractor>(true).Length,2,"Both hands need SDK touch interactors.");
            var buttons=workshop.Menu.GetComponentsInChildren<PhysicalAssembly.MetaXR.MetaMenuButton>();
            Assert.AreEqual(16,buttons.Length);
            var addBeam=buttons.Single(b=>b.name=="+ Beam");
            var origin=addBeam.transform.position-addBeam.transform.forward;
            Assert.IsTrue(addBeam.Ray.Raycast(new Ray(origin,addBeam.transform.forward),out var menuHit,2,false),"SDK ray must hit the visible button face.");
            Assert.IsFalse(addBeam.Ray.Raycast(new Ray(origin+addBeam.transform.right*.3f,addBeam.transform.forward),out _,2,false),"A ray outside the button must miss.");
            var pose=new Pose(menuHit.Point,Quaternion.identity);
            addBeam.Ray.PublishPointerEvent(new Oculus.Interaction.PointerEvent(101,Oculus.Interaction.PointerEventType.Hover,pose));
            Assert.IsTrue(addBeam.GetComponent<WorkshopButton>().IsHovered);
            addBeam.Ray.PublishPointerEvent(new Oculus.Interaction.PointerEvent(101,Oculus.Interaction.PointerEventType.Select,pose));
            Assert.AreEqual(16,workshop.World.Parts.Count,"SDK pinch selection must invoke the actual menu action.");
            addBeam.Ray.PublishPointerEvent(new Oculus.Interaction.PointerEvent(101,Oculus.Interaction.PointerEventType.Select,pose));
            Assert.AreEqual(16,workshop.World.Parts.Count,"Holding a pinch must not repeat the action.");
            addBeam.Ray.PublishPointerEvent(new Oculus.Interaction.PointerEvent(101,Oculus.Interaction.PointerEventType.Cancel,pose));
            Assert.IsFalse(addBeam.GetComponent<WorkshopButton>().IsHovered);
            Assert.IsFalse(addBeam.GetComponent<WorkshopButton>().IsSelected);
            var addBlock=buttons.Single(b=>b.name=="+ Block");
            addBlock.Poke.PublishPointerEvent(new Oculus.Interaction.PointerEvent(102,Oculus.Interaction.PointerEventType.Select,pose));
            Assert.AreEqual(17,workshop.World.Parts.Count,"SDK touch must also invoke the actual menu action.");
            var floor=workshop.Arena.Find("Arena").GetComponent<Collider>();
            Assert.That(floor.bounds.max.y,Is.EqualTo(.5f).Within(.001f),"Driving collider top must float 50 cm above the floor.");
            var head=new GameObject("Test head").transform; head.position=new Vector3(1,1.7f,-1); head.rotation=Quaternion.Euler(0,35,0);
            workshop.PositionAroundPlayer(head,.12f);
            Assert.That(floor.bounds.max.y,Is.EqualTo(.62f).Within(.001f),"Placement must add 50 cm to tracking-space floor elevation.");
            Assert.That(Vector2.Distance(new Vector2(workshop.Arena.position.x,workshop.Arena.position.z),new Vector2(head.position.x,head.position.z)),Is.LessThan(.001f),"Player starts inside the platform footprint.");
            var panel=workshop.Menu.Find("Control panel");
            Assert.Greater(Vector3.Dot(-panel.forward,Vector3.ProjectOnPlane(workshop.Arena.position-panel.position,Vector3.up).normalized),.99f,"UI front must face into the platform.");
            Assert.Greater(workshop.Menu.position.y+1.15f,1.2f,"Menu stays raised independently of the floor.");
            var lockedPosition=workshop.Arena.position; var lockedRotation=workshop.Arena.rotation;
            var chassis=workshop.World.Parts.Values.First(p=>p.Kind==PartKind.Chassis);
            workshop.World.TryGrab(0,chassis,new Pose(chassis.MainBody.Rigidbody.position,Quaternion.identity));
            var moveButton=buttons.Single(b=>b.name=="Move arena");
            moveButton.Ray.PublishPointerEvent(new Oculus.Interaction.PointerEvent(105,Oculus.Interaction.PointerEventType.Select,pose));
            Assert.IsFalse(workshop.RepositionRequested,"A building pinch must never trigger platform movement.");
            workshop.World.SetHoldPose(0,new Pose(chassis.MainBody.Rigidbody.position+Vector3.up*.2f,Quaternion.Euler(15,30,0)));
            Assert.AreEqual(lockedPosition,workshop.Arena.position);
            workshop.World.Release(0,false);
            workshop.Arena.position+=Vector3.up; workshop.Arena.rotation=Quaternion.Euler(20,30,10);
            yield return null;
            Assert.That(Vector3.Distance(lockedPosition,workshop.Arena.position),Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(lockedRotation,workshop.Arena.rotation),Is.LessThan(.01f));
            head.position+=Vector3.up*.5f; workshop.PositionAroundPlayer(head,.8f);
            Assert.That(workshop.FloorHeight,Is.EqualTo(.12f).Within(.001f),"Head movement cannot recalibrate the locked platform height.");
            buttons.Single(b=>b.name=="Move arena").GetComponent<WorkshopButton>().Press();
            workshop.PlaceArena(new Vector3(2,1,3),Quaternion.identity);
            Assert.That(floor.bounds.max.y,Is.EqualTo(.62f).Within(.001f),"Moving the arena must preserve its 50 cm floor offset.");
            Object.Destroy(head.gameObject);
            foreach(var p in workshop.World.Parts.Values.ToArray()) Object.Destroy(p.gameObject);
            foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(root);
            yield return null;
        }
        [UnityTest] public IEnumerator FourWheelCarMovesForwardAndStopsReceivingDetachedWheelInput()
        {
            var go=new GameObject("Car test"); var world=go.AddComponent<AssemblyWorld>(); var factory=go.AddComponent<PartFactory>(); factory.World=world;
            var session=go.AddComponent<WorkshopSession>(); session.World=world;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.position=new Vector3(0,-.05f,0); floor.transform.localScale=new Vector3(20,.1f,20);
            var chassis=factory.Create(PartKind.Chassis,new Vector3(0,.16f,0),Quaternion.identity);
            foreach(var socket in chassis.Sockets.Where(s=>s.Family=="wheel"))
            {
                bool left=socket.transform.localPosition.x<0; var rotation=Quaternion.Euler(0,left?180:0,0);
                var wheel=factory.Create(PartKind.Wheel,socket.transform.position-rotation*new Vector3(HustlePartProfile.MountSocketX,0,0),rotation);
                Assert.IsTrue(world.Connect(new ConnectionRecord {id=System.Guid.NewGuid().ToString("N"),a=new BodyRef(wheel.InstanceId,"mount"),b=new BodyRef(chassis.InstanceId,"chassis"),socketA="mount",socketB=socket.Id,anchorA=new Vector3(HustlePartProfile.MountSocketX,0,0),anchorB=socket.transform.localPosition}));
            }
            session.Selected=chassis; yield return new WaitForSeconds(.5f);
            Assert.Less(chassis.MainBody.Rigidbody.position.magnitude,2,"Car exploded before input: "+string.Join("; ",world.Parts.Values.SelectMany(p=>p.Bodies).Select(b=>$"{b.Part.Kind}/{b.Id}: {b.Rigidbody.position} v={b.Rigidbody.linearVelocity}")));
            session.ToggleMode(); Assert.AreEqual(WorkshopMode.Drive,session.Mode);
            float start=chassis.MainBody.Rigidbody.position.z; session.Right=HandCommand.Forward;
            yield return new WaitForSeconds(1.5f);
            Assert.Greater(chassis.MainBody.Rigidbody.position.z-start,.10f,"Wheel torque should move the chassis forward.");
            Assert.Greater(Vector3.Dot(chassis.MainBody.transform.up,Vector3.up),.7f,"Car must remain upright.");
            var before=Vector3.ProjectOnPlane(chassis.MainBody.transform.forward,Vector3.up); session.Left=HandCommand.Right;
            // Allow the differential drive to overcome tire friction at the app's 60 Hz step.
            for(int step=0;step<108;step++) yield return new WaitForFixedUpdate();
            float turn=Vector3.SignedAngle(before,Vector3.ProjectOnPlane(chassis.MainBody.transform.forward,Vector3.up),Vector3.up);
            Assert.Greater(turn,4,"Right-hand-side steering command should turn the car right.");
            before=Vector3.ProjectOnPlane(chassis.MainBody.transform.forward,Vector3.up); session.Left=HandCommand.Left;
            for(int step=0;step<108;step++) yield return new WaitForFixedUpdate();
            turn=Vector3.SignedAngle(before,Vector3.ProjectOnPlane(chassis.MainBody.transform.forward,Vector3.up),Vector3.up);
            Assert.Less(turn,-4,"Left steering command should turn the car left.");
            var detached=world.GetConnectedParts(chassis.InstanceId).First(p=>p.Kind==PartKind.Wheel); world.TryDetachPart(detached);
            Assert.AreEqual(HandCommand.None,session.Right); Assert.AreEqual(1,world.GetConnectedParts(detached.InstanceId).Count);
            foreach(var p in world.Parts.Values.ToArray()) Object.Destroy(p.gameObject); Object.Destroy(floor); Object.Destroy(go); yield return null;
        }
        [UnityTest] public IEnumerator AlignmentCommitsAtCoincidentAnchorsThenDetaches()
        {
            var go=new GameObject("World"); var world=go.AddComponent<AssemblyWorld>(); var factory=go.AddComponent<PartFactory>(); factory.World=world;
            var a=factory.Create(PartKind.Block,new Vector3(0,2,0),Quaternion.identity);
            var b=factory.Create(PartKind.Block,new Vector3(.12f,2,0),Quaternion.identity);
            a.MainBody.Rigidbody.useGravity=false; b.MainBody.Rigidbody.useGravity=false;
            var candidate=world.Query(a); Assert.IsTrue(candidate.IsValid); Assert.IsTrue(world.TryBeginAssembly(candidate));
            yield return new WaitForSeconds(.4f);
            Assert.AreEqual(1,world.Connections.Count); var edge=world.Connections[0];
            Assert.Less(Vector3.Distance(world.Resolve(edge.a).transform.TransformPoint(edge.anchorA),world.Resolve(edge.b).transform.TransformPoint(edge.anchorB)),.002f);
            Assert.IsFalse(a.MainBody.Rigidbody.isKinematic); Assert.IsFalse(world.IsBusy);
            world.TryDetachPart(a); yield return null;
            Assert.AreEqual(0,world.Connections.Count); Assert.AreEqual(0,a.GetComponentsInChildren<ConfigurableJoint>().Length);
            Object.Destroy(a.gameObject); Object.Destroy(b.gameObject); Object.Destroy(go); yield return null;
        }
        [UnityTest] public IEnumerator DestroyDuringAlignmentRestoresSurvivor()
        {
            var go=new GameObject("World"); var world=go.AddComponent<AssemblyWorld>(); var factory=go.AddComponent<PartFactory>(); factory.World=world;
            var a=factory.Create(PartKind.Block,new Vector3(0,2,0),Quaternion.identity);
            var b=factory.Create(PartKind.Block,new Vector3(.12f,2,0),Quaternion.identity);
            a.MainBody.Rigidbody.useGravity=false; b.MainBody.Rigidbody.useGravity=false;
            Assert.IsTrue(world.TryBeginAssembly(world.Query(a))); yield return new WaitForFixedUpdate(); Object.Destroy(b.gameObject);
            yield return new WaitForSeconds(.35f);
            Assert.IsFalse(a.MainBody.Rigidbody.isKinematic); Assert.IsFalse(world.IsBusy); Assert.AreEqual(0,world.Connections.Count);
            Object.Destroy(a.gameObject); Object.Destroy(go); yield return null;
        }
    }
}


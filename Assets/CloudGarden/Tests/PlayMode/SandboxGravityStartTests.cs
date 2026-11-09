using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PhysicalAssembly.Tests
{
    public sealed class SandboxGravityStartTests
    {
        WorkshopBootstrap workshop;
        Vector3 originalGravity;
        DownhillController Level => workshop.Downhill;

        [UnitySetUp] public IEnumerator Setup()
        {
            DesignModeSession.SetEnabled(false);
            originalGravity=Physics.gravity;
            SandboxCampaignSession.Reset();
            yield return SceneManager.LoadSceneAsync("DownhillSandbox");
            yield return null;
            workshop=Object.FindFirstObjectByType<WorkshopBootstrap>();
            Level.Settings.TransitionDuration=0;
            Level.Settings.CountdownStep=.01f;
            Level.Settings.GoDuration=.01f;
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(workshop) Level.RestoreBuildImmediately();
            Physics.gravity=originalGravity;
            DesignModeSession.SetEnabled(false);
            SandboxCampaignSession.Reset();
            var sandbox=SceneManager.GetSceneByName("DownhillSandbox");
            SceneManager.SetActiveScene(SceneManager.CreateScene("Sandbox gravity cleanup"));
            if(sandbox.IsValid()) yield return SceneManager.UnloadSceneAsync(sandbox);
        }

        void Join(AssemblyPart part,AssemblyPart target,Vector3 direction,float yaw,bool turned=false)
        {
            foreach(var body in part.Bodies) {
                body.Rigidbody.rotation=Level.Presentation.AssemblyAnchor.rotation*Quaternion.Euler(0,yaw,0)*Quaternion.Euler(turned?180:0,0,0);
                body.transform.rotation=body.Rigidbody.rotation;
            }
            Physics.SyncTransforms();
            var normal=Level.Presentation.AssemblyAnchor.TransformDirection(direction);
            var socket=target.Sockets.Where(s=>Vector3.Dot(s.Normal,normal)>.99f).OrderBy(s=>Vector3.Distance(s.WorldPosition,target.MainBody.Rigidbody.position)).First();
            var source=part.Sockets.Where(s=>Vector3.Dot(s.Normal,normal)<-.99f).OrderBy(s=>Vector3.Distance(s.WorldPosition,part.MainBody.Rigidbody.position)).First();
            var candidate=new AttachmentCandidate(source.Body,socket.Body,source.WorldPosition,socket.WorldPosition,source,socket);
            // Both normals are already aligned. Preserve the player's half turn
            // around the axle instead of normalizing the socket's twist frame.
            var delta=turned?Quaternion.identity:candidate.rotationDelta;
            foreach(var body in part.Bodies) {
                var rb=body.Rigidbody;
                rb.position=candidate.targetPoint+delta*(rb.position-candidate.sourcePoint);
                rb.rotation=delta*rb.rotation;
                body.transform.SetPositionAndRotation(rb.position,rb.rotation);
            }
            Physics.SyncTransforms();
            Assert.IsTrue(workshop.World.PlacementClear(new AttachmentCandidate(source.Body,socket.Body,source.WorldPosition,socket.WorldPosition,source,socket),new[]{part}),part.Kind.ToString());
            Assert.IsTrue(workshop.World.Connect(new ConnectionRecord {
                id=System.Guid.NewGuid().ToString("N"),a=new BodyRef(part.InstanceId,source.Body.Id),
                b=new BodyRef(target.InstanceId,socket.Body.Id),socketA=source.Id,socketB=socket.Id,
                anchorA=source.transform.localPosition,anchorB=socket.transform.localPosition
            }));
        }

        void BuildCar(bool reversed,bool lengthwise=false,bool turned=false)
        {
            foreach(var part in Level.Kit.Skip(1)) foreach(var body in part.Bodies) {
                body.Rigidbody.position+=Vector3.up*2;
                body.transform.position=body.Rigidbody.position;
            }
            Join(Level.Kit[1],Level.Core,Vector3.forward,lengthwise?0:90);
            Join(Level.Kit[2],Level.Core,Vector3.back,lengthwise?0:90);
            int front=reversed?7:5,rear=reversed?5:7;
            Join(Level.Kit[front],Level.Kit[1],Vector3.right,0,turned);
            Join(Level.Kit[front+1],Level.Kit[1],Vector3.left,180,turned);
            Join(Level.Kit[rear],Level.Kit[2],Vector3.right,0,turned);
            Join(Level.Kit[rear+1],Level.Kit[2],Vector3.left,180,turned);
            if(turned) foreach(var wheel in Level.Kit.Where(p=>DownhillParts.IsWheel(p.Kind)))
                Assert.Less(Vector3.Dot(wheel.MainBody.Rigidbody.rotation*Vector3.up,Level.Presentation.AssemblyAnchor.up),-.99f);
        }

        [UnityTest] public IEnumerator NormalWheelsRollDownhillFromRest() { yield return CheckStart(false); }
        [UnityTest] public IEnumerator ReversedWheelsRollDownhillFromRest() { yield return CheckStart(true); }
        [UnityTest] public IEnumerator LongNormalWheelsRollDownhillFromRest() { yield return CheckStart(false,true); }
        [UnityTest] public IEnumerator LongReversedWheelsRollDownhillFromRest() { yield return CheckStart(true,true); }
        [UnityTest] public IEnumerator WheelsTurned180OnMountsRollDownhill() { yield return CheckStart(false,false,true); }
        [UnityTest] public IEnumerator LongCarWheelsTurned180OnMountsRollDownhill() { yield return CheckStart(false,true,true); }

        [UnityTest] public IEnumerator LaunchHasNoDriveWithoutGravity()
        {
            BuildCar(true,true);
            Physics.gravity=Vector3.zero;
            Assert.IsTrue(Level.TryStart());
            while(Level.State==DownhillState.Transition || Level.State==DownhillState.Countdown) yield return null;
            var body=Level.SimulationCore.MainBody.Rigidbody;
            var start=body.position;
            yield return new WaitForSeconds(.35f);
            Assert.Less((body.position-start).magnitude/Level.GarageWorldScale,.001f);
            foreach(var part in Level.SimulationWorld.Parts.Values) foreach(var b in part.Bodies) {
                Assert.Less(b.Rigidbody.linearVelocity.magnitude,.001f,"No launch impulse or motor is permitted.");
                foreach(var hinge in b.GetComponents<HingeJoint>()) Assert.IsFalse(hinge.useMotor);
            }
        }

        [UnityTest] public IEnumerator OppositeWheelMountDirectionsRollDownTheSameSlope()
        {
            var root=new GameObject("Sandbox mirrored wheel check");
            var world=root.AddComponent<AssemblyWorld>();
            var wheels=new AssemblyPart[2];
            var starts=new float[2];
            float scale=Level.GarageWorldScale/GravityGarageDesign.UnitScale;
            try {
                for(int i=0;i<2;i++) {
                    var p=Level.Settings.Garage.Surface(1.1f,out var normal);
                    var heading=Level.Course.StartPoint.rotation*Quaternion.Euler(0,i*180,0);
                    var wheel=DownhillParts.Create(world,PartKind.DownhillLargeWheel,new Pose(Vector3.zero,heading),scale,null,DownhillParts.DesignerGridVersion);
                    wheels[i]=wheel;wheel.transform.SetParent(root.transform,true);
                    float radius=wheel.GetComponentInChildren<PassiveWheel>().Radius;
                    var up=Level.Course.SandboxFrame.TransformDirection(new Vector3(normal.x,-normal.y,0));
                    var at=Level.GarageCourse.Point(p)+up*(radius+.003f*Level.GarageWorldScale)
                        +Level.Course.SandboxFrame.forward*((i==0?-.5f:.5f)*Level.GarageWorldScale);
                    foreach(var b in wheel.Bodies) {
                        b.Rigidbody.position=at;b.Rigidbody.rotation=heading;b.transform.SetPositionAndRotation(at,heading);
                        b.Rigidbody.isKinematic=false;b.Rigidbody.useGravity=true;
                    }
                    starts[i]=Level.GarageCourse.Coordinates(at).x;
                }
                Physics.SyncTransforms();
                yield return new WaitForSeconds(.7f*Mathf.Sqrt(Level.GarageWorldScale));
                float a=Level.GarageCourse.Coordinates(wheels[0].MainBody.Rigidbody.position).x-starts[0];
                float bProgress=Level.GarageCourse.Coordinates(wheels[1].MainBody.Rigidbody.position).x-starts[1];
                Assert.Greater(a,.1f);Assert.Greater(bProgress,.1f);
                // Free tires may wobble differently; the invariant is downhill travel,
                // not identical distance from independent rigid-body contact solves.
                foreach(var wheel in wheels)
                    Assert.Greater(Vector3.Dot(wheel.MainBody.Rigidbody.linearVelocity,Level.Course.SandboxFrame.right),0,
                        "Reversing the axle direction must not reverse gravity travel.");
            } finally { Object.DestroyImmediate(root); }
        }

        IEnumerator CheckStart(bool reversed,bool lengthwise=false,bool turned=false)
        {
            BuildCar(reversed,lengthwise,turned);
            Assert.IsTrue(Level.TryStart());
            while(Level.State==DownhillState.Transition || Level.State==DownhillState.Countdown) yield return null;
            Assert.AreEqual(DownhillState.Running,Level.State);
            var body=Level.SimulationCore.MainBody.Rigidbody;
            float start=Level.GarageCourse.Coordinates(body.position).x;
            float minSpeed=0,maxSpeed=0,elapsed=0,root=Mathf.Sqrt(Level.GarageWorldScale);
            while(elapsed<1.5f && Level.State==DownhillState.Running) {
                yield return new WaitForFixedUpdate();
                elapsed+=Time.fixedDeltaTime/root;
                float speed=Vector3.Dot(body.linearVelocity,Level.Course.SandboxFrame.right)/root;
                minSpeed=Mathf.Min(minSpeed,speed);maxSpeed=Mathf.Max(maxSpeed,speed);
            }
            float progress=Level.GarageCourse.Coordinates(body.position).x-start;
            TestContext.WriteLine("GRAVITY_START reversed="+reversed+" long="+lengthwise+" mounts180="+turned+" progress="+progress+" minSpeed="+minSpeed+" maxSpeed="+maxSpeed);
            Assert.Greater(progress,.2f,"Gravity must start either wheel layout downhill.");
            Assert.Greater(minSpeed,-.01f,"Release must not kick the vehicle uphill.");
        }
    }
}

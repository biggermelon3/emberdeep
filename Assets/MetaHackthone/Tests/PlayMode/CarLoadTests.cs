using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PhysicalAssembly.Tests
{
    public sealed class CarLoadTests
    {
        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(root);
            yield return null;
        }
        [UnityTest] public IEnumerator DefaultCarLoadsAndSteersLeftAndRightForwardAndReverse()
        {
            yield return SceneManager.LoadSceneAsync("Workshop"); yield return null;
            var workshop=Object.FindFirstObjectByType<WorkshopBootstrap>();
            workshop.GetComponent<DesktopInputAdapter>().enabled=false;
            workshop.Menu.GetComponentsInChildren<WorkshopButton>().Single(b=>b.name=="Default Car").Press();
            Assert.AreEqual(WorkshopMode.Drive,workshop.Session.Mode);
            Assert.AreEqual(6,workshop.World.Parts.Count);
            Assert.AreEqual(5,workshop.World.Connections.Count);
            workshop.Session.EnterBuild();
            var save=JsonUtility.FromJson<AssemblySaveV1>(JsonUtility.ToJson(workshop.Saves.Capture(workshop.Session.Selected)));
            // An ample test surface isolates steering from the workshop's edge rails.
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position=new Vector3(20,.4f,0); floor.transform.localScale=new Vector3(20,.2f,20);
            foreach(int drive in new[]{1,-1}) foreach(int steer in new[]{-1,1})
            {
                workshop.Session.EnterBuild();
                foreach(var part in workshop.World.Parts.Values.ToArray()) { workshop.World.UnregisterPart(part); part.gameObject.SetActive(false); Object.Destroy(part.gameObject); }
                var restored=workshop.Saves.Restore(save,new Pose(new Vector3(20,.72f,0),Quaternion.identity));
                var car=restored.Single(p=>p.Kind==PartKind.Chassis);
                yield return new WaitForSeconds(.5f);
                workshop.Session.Select(car); workshop.Session.ToggleMode();
                var start=car.MainBody.Rigidbody.position;
                var forward=car.MainBody.transform.forward;
                for(int step=0;step<90;step++)
                {
                    workshop.Session.Right=drive>0?HandCommand.Forward:HandCommand.Reverse;
                    workshop.Session.Left=steer>0?HandCommand.Right:HandCommand.Left;
                    yield return new WaitForFixedUpdate();
                    foreach(var edge in workshop.World.Connections)
                    {
                        var a=workshop.World.Resolve(edge.a).Rigidbody; var b=workshop.World.Resolve(edge.b).Rigidbody;
                        Assert.Less(Vector3.Distance(a.position+a.rotation*edge.anchorA,b.position+b.rotation*edge.anchorB),.025f,"Driving must not separate loaded joints.");
                    }
                }
                float distance=Vector3.Dot(car.MainBody.Rigidbody.position-start,forward)*drive;
                float yaw=Vector3.SignedAngle(forward,car.MainBody.transform.forward,Vector3.up)*steer;
                Debug.Log($"Loaded car drive={drive}, steer={steer}: travel={distance:F3}m, turn={yaw:F2} degrees");
                Assert.Greater(distance,.1f,$"Travel direction, drive={drive}, steer={steer}");
                Assert.Greater(yaw,4f,$"Steering direction, drive={drive}, steer={steer}");
                Assert.Greater(Vector3.Dot(car.MainBody.transform.up,Vector3.up),.7f);
            }
        }
        [UnityTest] public IEnumerator SteeringRequiresGroundedConnectedWheels()
        {
            yield return SceneManager.LoadSceneAsync("Workshop"); yield return null;
            var workshop=Object.FindFirstObjectByType<WorkshopBootstrap>();
            workshop.GetComponent<DesktopInputAdapter>().enabled=false;
            foreach(bool airborne in new[]{true,false})
            {
                Assert.IsTrue(workshop.CreateTestCar()); workshop.Session.EnterBuild();
                var car=workshop.Session.Selected;
                yield return new WaitForSeconds(.5f);
                if(airborne)
                {
                    var pose=new Pose(car.MainBody.Rigidbody.position,Quaternion.identity);
                    Assert.IsTrue(workshop.World.TryGrab(2,car,pose));
                    workshop.World.SetHoldPose(2,new Pose(pose.position+Vector3.up*2,pose.rotation));
                    workshop.World.Release(2,false);
                }
                else
                {
                    // Detached wheels still lie on the platform, but cannot supply
                    // support to the chassis steering controller.
                    foreach(var wheel in workshop.World.Parts.Values.Where(p=>p.Kind==PartKind.Wheel).ToArray())
                    {
                        foreach(var edge in workshop.World.Connections.Where(e=>e.a.partId==wheel.InstanceId||e.b.partId==wheel.InstanceId).ToArray())
                            workshop.World.RemoveConnection(edge.id);
                        // Keep detached wheels grounded but clear of the chassis: collision
                        // impulses after detachment are not steering assistance.
                        float side=Mathf.Sign(wheel.MainBody.Rigidbody.position.x-car.MainBody.Rigidbody.position.x);
                        foreach(var body in wheel.Bodies) body.Rigidbody.position+=Vector3.right*side*.45f;
                    }
                }
                foreach(var part in workshop.World.Parts.Values) foreach(var body in part.Bodies)
                { body.Rigidbody.useGravity=false; body.Rigidbody.linearVelocity=Vector3.zero; body.Rigidbody.angularVelocity=Vector3.zero; }
                var initial=car.MainBody.Rigidbody.rotation;
                for(int step=0;step<60;step++)
                {
                    car.GetComponent<ChassisSteering>().ApplyControl(new AssemblyControlFrame(0,1,false,car),Time.fixedDeltaTime);
                    yield return new WaitForFixedUpdate();
                }
                Assert.Less(Quaternion.Angle(initial,car.MainBody.Rigidbody.rotation),2f,airborne?"Airborne car must receive no yaw assistance.":"Detached grounded wheels must not enable yaw assistance.");
            }
        }
        [UnityTest] public IEnumerator RotatedSavedCarRemainsIntactAfterPhysicsResumes()
        {
            yield return SceneManager.LoadSceneAsync("Workshop"); yield return null;
            var workshop=Object.FindFirstObjectByType<WorkshopBootstrap>();
            workshop.GetComponent<DesktopInputAdapter>().enabled=false;
            Assert.IsTrue(workshop.CreateTestCar()); workshop.Session.EnterBuild();
            yield return new WaitForSeconds(.5f);
            var car=workshop.Session.Selected;
            // Save after the car and its independently rotating wheel bodies move.
            var grip=new Pose(car.MainBody.Rigidbody.position,Quaternion.identity);
            Assert.IsTrue(workshop.World.TryGrab(2,car,grip));
            workshop.World.SetHoldPose(2,new Pose(grip.position,Quaternion.Euler(0,63,0)));
            workshop.World.Release(2,false);
            var save=JsonUtility.FromJson<AssemblySaveV1>(JsonUtility.ToJson(workshop.Saves.Capture(car)));
            foreach(float yaw in new[]{0f,90f,-137f})
            {
                foreach(var part in workshop.World.Parts.Values.ToArray()) { workshop.World.UnregisterPart(part); part.gameObject.SetActive(false); Object.Destroy(part.gameObject); }
                var restored=workshop.Saves.Restore(save,new Pose(new Vector3(0,.72f,0),Quaternion.Euler(0,yaw,0)));
                Assert.AreEqual(6,restored.Count); Assert.AreEqual(5,workshop.World.Connections.Count);
                foreach(var wheel in restored.Where(p=>p.Kind==PartKind.Wheel))
                    foreach(var a in wheel.Bodies) foreach(var b in wheel.Bodies.Where(b=>b!=a))
                        foreach(var ca in a.Colliders) foreach(var cb in b.Colliders)
                            Assert.IsTrue(Physics.GetIgnoreCollision(ca,cb),"Internal wheel collision suppression must survive loading.");
                for(int step=0;step<120;step++)
                {
                    yield return new WaitForFixedUpdate();
                    foreach(var edge in workshop.World.Connections)
                    {
                        var a=workshop.World.Resolve(edge.a).Rigidbody; var b=workshop.World.Resolve(edge.b).Rigidbody;
                        Assert.Less(Vector3.Distance(a.position+a.rotation*edge.anchorA,b.position+b.rotation*edge.anchorB),.025f,$"Joint separated at load yaw {yaw}, step {step}");
                    }
                    foreach(var body in restored.SelectMany(p=>p.Bodies))
                        Assert.Less(body.Rigidbody.linearVelocity.magnitude,3f,$"Load explosion at yaw {yaw}: {body.Part.Kind}/{body.Id}");
                }
                Assert.Greater(Vector3.Dot(restored.Single(p=>p.Kind==PartKind.Chassis).MainBody.transform.up,Vector3.up),.85f);
            }
        }
    }
}

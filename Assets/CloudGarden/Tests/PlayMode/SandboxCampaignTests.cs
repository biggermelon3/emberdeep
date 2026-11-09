using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace PhysicalAssembly.Tests
{
 public sealed class SandboxCampaignTests
 {
  WorkshopBootstrap w;
  static void Resolve(DownhillController level,bool success) => typeof(DownhillController).GetMethod("Resolve",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(level,new object[]{success});
  [UnitySetUp] public IEnumerator Setup() {
   DesignModeSession.SetEnabled(false); SandboxCampaignSession.Reset();
   yield return SceneManager.LoadSceneAsync("DownhillSandbox"); yield return null;
   w=Object.FindFirstObjectByType<WorkshopBootstrap>();
   w.Downhill.Settings.TransitionDuration=0; w.Downhill.Settings.CountdownStep=.01f; w.Downhill.Settings.GoDuration=.01f;
  }
  [UnityTearDown] public IEnumerator Cleanup() { DesignModeSession.SetEnabled(false); SandboxCampaignSession.Reset(); yield return SceneManager.LoadSceneAsync("MainMenu"); }
  [Test] public void NormalControlsAndPrematureAdvanceAreBlocked() {
   var l=w.Downhill; var ui=l.Presentation.Interface;
   Assert.IsNotNull(l.GarageCourse); Assert.IsNull(l.Course25D);
   Assert.IsFalse(ui.RotateLeftButton.gameObject.activeSelf); Assert.IsFalse(ui.RotateRightButton.gameObject.activeSelf); Assert.IsFalse(ui.ExampleButton.gameObject.activeSelf);
   Assert.IsFalse(l.TryLoadDesignerExample()); w.Session.Select(l.Kit[1]); Assert.IsFalse(l.DragBuilder.RotateSelected(1));
   Assert.IsFalse(l.TryAdvanceLevel()); Resolve(l,false); Assert.IsFalse(l.CanAdvanceLevel);
   Assert.AreEqual("Gravity Garage",ui.CourseTitle); Assert.AreEqual(2,ui.CourseCount);
  }
  [UnityTest] public IEnumerator WorkbenchCarriesConstructionAndKeepsSecondCourseOnRetry() {

    var l=w.Downhill; l.Settings.TransitionDuration=0; l.Settings.CountdownStep=.01f; l.Settings.GoDuration=.01f;
DesignModeSession.SetEnabled(true); Assert.IsTrue(l.TryLoadDesignerExample()); DesignModeSession.SetEnabled(false);
var ids=l.Kit.Select(p=>p.InstanceId).ToArray(); var edges=w.World.Connections.Select(e=>e.id).ToArray(); var selected=w.Session.Selected;
Resolve(l,true); Assert.IsTrue(l.CanAdvanceLevel); Assert.IsTrue(l.TryAdvanceLevel());
Assert.IsTrue(l.IsBuilding); Assert.IsTrue(l.RotationWheel.gameObject.activeInHierarchy); Assert.IsFalse(l.TryAdvanceLevel()); Assert.IsNotNull(l.Course25D);
Assert.AreEqual("Teleport Pipes",l.Presentation.Interface.CourseTitle); Assert.AreEqual(2,l.Presentation.Interface.CourseNumber);
CollectionAssert.AreEqual(ids,l.Kit.Select(p=>p.InstanceId)); CollectionAssert.AreEqual(edges,w.World.Connections.Select(e=>e.id)); Assert.AreSame(selected,w.Session.Selected);
Assert.IsTrue(l.TryStart()); yield return new WaitForSecondsRealtime(.15f); Assert.IsNotNull(l.Run25D); Assert.IsTrue(l.TryRetry());
yield return new WaitForSecondsRealtime(.15f); Assert.IsTrue(l.TryReturn()); yield return null;
Assert.AreEqual(SandboxCourseId.TeleportPipes,l.CurrentCourse); Assert.IsTrue(l.TryResetBuild()); Assert.AreEqual(9,l.Kit.Count);
Resolve(l,true); Assert.IsTrue(SandboxCampaignSession.AllComplete); Assert.IsFalse(l.CanAdvanceLevel);

  }
  [UnityTest] public IEnumerator LegalDesignerVehicleCompletesBothCourses() {
   var l=w.Downhill;
   void Join(AssemblyPart part,AssemblyPart target,Vector3 direction,float yaw) {
    foreach(var body in part.Bodies) { body.Rigidbody.rotation=l.Presentation.AssemblyAnchor.rotation*Quaternion.Euler(0,yaw,0); body.transform.rotation=body.Rigidbody.rotation; }
    Physics.SyncTransforms();
    var normal=l.Presentation.AssemblyAnchor.TransformDirection(direction);
    var socket=target.Sockets.Where(s=>Vector3.Dot(s.Normal,normal)>.99f).OrderBy(s=>Vector3.Distance(s.WorldPosition,target.MainBody.Rigidbody.position)).First();
    var source=part.Sockets.Where(s=>Vector3.Dot(s.Normal,normal)<-.99f).OrderBy(s=>Vector3.Distance(s.WorldPosition,part.MainBody.Rigidbody.position)).First();
    var candidate=new AttachmentCandidate(source.Body,socket.Body,source.WorldPosition,socket.WorldPosition,source,socket);
    foreach(var body in part.Bodies) {
     var rb=body.Rigidbody; rb.position=candidate.targetPoint+candidate.rotationDelta*(rb.position-candidate.sourcePoint); rb.rotation=candidate.rotationDelta*rb.rotation;
     body.transform.SetPositionAndRotation(rb.position,rb.rotation);
    }
    Physics.SyncTransforms();
    Assert.IsTrue(w.World.PlacementClear(new AttachmentCandidate(source.Body,socket.Body,source.WorldPosition,socket.WorldPosition,source,socket),new[]{part}),part.Kind.ToString());
    Assert.IsTrue(w.World.Connect(new ConnectionRecord{id=System.Guid.NewGuid().ToString("N"),a=new BodyRef(part.InstanceId,source.Body.Id),b=new BodyRef(socket.Body.Part.InstanceId,socket.Body.Id),socketA=source.Id,socketB=socket.Id,anchorA=source.transform.localPosition,anchorB=socket.transform.localPosition}));
   }
   // Crosswise beams keep the chassis between the front and rear wheel axles.
   foreach(var part in l.Kit.Skip(1)) foreach(var body in part.Bodies) { body.Rigidbody.position+=Vector3.up*2; body.transform.position=body.Rigidbody.position; }
   Join(l.Kit[1],l.Core,Vector3.forward,90); Join(l.Kit[2],l.Core,Vector3.back,90);
   Join(l.Kit[5],l.Kit[1],Vector3.right,0); Join(l.Kit[6],l.Kit[1],Vector3.left,180);
   Join(l.Kit[7],l.Kit[2],Vector3.right,0); Join(l.Kit[8],l.Kit[2],Vector3.left,180);
   l.SetGaragePlayback(4);
   for(int course=0;course<2;course++) {
    Assert.IsTrue(l.TryStart()); float deadline=Time.realtimeSinceStartup+15;
    while(l.State!=DownhillState.Success && l.State!=DownhillState.Failed && Time.realtimeSinceStartup<deadline) yield return null;
    Assert.AreEqual(DownhillState.Success,l.State,"Course "+course+": "+w.Session.Status+" "+JsonUtility.ToJson(l.GarageResult));
    if(course==0) Assert.IsTrue(l.TryAdvanceLevel());
   }
  }
  [UnityTest] public IEnumerator NormalSandboxEntryRestartsAtLevelOne() {
   var l=w.Downhill; Resolve(l,true); Assert.IsTrue(l.TryAdvanceLevel());
   var nav=CloudGarden.Integration.GardenNavigation.Current; nav.ReturnToMainMenu();
   while(nav.IsTransitioning) yield return null;
   nav.EnterSandbox(); while(nav.IsTransitioning) yield return null;
   var fresh=nav.DownhillWorkshop.Downhill;
   Assert.AreNotSame(l,fresh); Assert.AreEqual(SandboxCourseId.GravityGarage,fresh.CurrentCourse);
   Assert.IsFalse(fresh.Kit.Single(p=>p.Kind==PartKind.DownhillDrill).gameObject.activeInHierarchy);
   Assert.IsFalse(fresh.Presentation.Interface.NextLevelButton.gameObject.activeInHierarchy);
   Assert.AreEqual(SandboxCourseId.GravityGarage,SandboxCampaignSession.Current); Assert.IsFalse(SandboxCampaignSession.FirstComplete);
  }
  [UnityTest] public IEnumerator LockedDrillFollowsRoomPlacementBeforeUnlock()
  {
   var l=w.Downhill;var drill=l.Kit.Single(p=>p.Kind==PartKind.DownhillDrill);
   var local=w.Arena.InverseTransformPoint(drill.MainBody.transform.position);
   w.ToggleReposition();w.PlaceArena(new Vector3(3,0,-2),Quaternion.Euler(0,70,0));
   Assert.Less(Vector3.Distance(w.Arena.TransformPoint(local),drill.MainBody.transform.position),.001f,"Locked drill must follow initial headset placement");
   w.ApplyPlayArea(new Pose(new Vector3(-4,0,3),Quaternion.Euler(0,150,0)));
   Assert.Less(Vector3.Distance(w.Arena.TransformPoint(local),drill.MainBody.transform.position),.001f,"Locked drill must follow saved room placement");
   Resolve(l,true);Assert.IsTrue(l.TryAdvanceLevel());yield return null;
   Assert.IsTrue(drill.gameObject.activeInHierarchy);
   Assert.Less(Vector3.Distance(w.Arena.TransformPoint(local),drill.MainBody.transform.position),.001f,"Unlocked drill must appear at the moved workbench");
   Assert.IsTrue(drill.GetComponentsInChildren<Renderer>().Any(r=>r.enabled));
   Assert.IsTrue(w.World.TryGrab(2,drill,new Pose(drill.MainBody.Rigidbody.position,drill.MainBody.Rigidbody.rotation)));w.World.Release(2,false);
  }
  [UnityTest] public IEnumerator CompletionUnlocksNextLevelAndDrill() {
   var l=w.Downhill;var drill=l.Kit.Single(p=>p.Kind==PartKind.DownhillDrill);
   Assert.AreEqual(SandboxCourseId.GravityGarage,l.CurrentCourse);
   Assert.IsFalse(drill.gameObject.activeInHierarchy);Assert.IsFalse(w.World.Parts.ContainsKey(drill.InstanceId));
   Assert.IsFalse(l.Presentation.Interface.NextLevelButton.gameObject.activeInHierarchy);
   Resolve(l,false);Assert.IsFalse(l.CanAdvanceLevel);
   Resolve(l,true);Assert.IsTrue(l.Presentation.Interface.NextLevelButton.gameObject.activeInHierarchy);
   l.Presentation.Interface.NextLevelButton.Press();yield return null;
   Assert.AreEqual(SandboxCourseId.TeleportPipes,l.CurrentCourse);
   Assert.IsTrue(drill.gameObject.activeInHierarchy);Assert.IsTrue(w.World.Parts.ContainsKey(drill.InstanceId));
   Assert.IsFalse(l.Presentation.Interface.NextLevelButton.gameObject.activeInHierarchy);
   Assert.IsTrue(l.TryResetBuild());Assert.IsTrue(drill.gameObject.activeInHierarchy);
  }
 }
}

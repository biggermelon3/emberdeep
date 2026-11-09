using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace PhysicalAssembly.Tests
{
 public sealed class GravityGarage3DTests
 {
  GameObject root; GravityGarage3DLevelAsset level; float step,rate;Vector3 gravity;
  GravityGarage3DCourse course; GravityGarage3DRun run; AssemblyWorld world; AssemblyPart[] parts;
  GameObject[] suspendedSceneRoots;
  [SetUp]public void SaveClock(){step=Time.fixedDeltaTime;rate=Time.timeScale;gravity=Physics.gravity;
   // Procedural fixtures must not collide with the authored sandbox running in the Editor.
   var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByName("DownhillSandbox");
   suspendedSceneRoots=scene.IsValid() && scene.isLoaded?scene.GetRootGameObjects().Where(g=>g.activeSelf).ToArray():new GameObject[0];
   foreach(var go in suspendedSceneRoots)go.SetActive(false);
  }
  void Build(float scale=.8f,bool rearLoad=false)
  {
   root=new GameObject("Valley calibration");level=ScriptableObject.CreateInstance<GravityGarage3DLevelAsset>();level.Scenery=false;
   var track=new GameObject("Track");track.transform.SetParent(root.transform);course=track.AddComponent<GravityGarage3DCourse>();course.Build(level,GravityGarage25DCourse.UnitScale*scale);
   SetupCar(scale,rearLoad);
  }
  void SetupCar(float scale,bool rearLoad=false)
  {
   var car=new GameObject("Car");car.transform.SetParent(root.transform);world=car.AddComponent<AssemblyWorld>();parts=CreateCar(world,scale,rearLoad);
   var pose=course.ReleasePose(parts,parts[0],1);var origin=parts[0].MainBody.Rigidbody.position;var delta=pose.rotation*Quaternion.Inverse(parts[0].MainBody.Rigidbody.rotation);
   foreach(var b in parts.SelectMany(p=>p.Bodies)){b.Rigidbody.position=pose.position+delta*(b.Rigidbody.position-origin);b.Rigidbody.rotation=delta*b.Rigidbody.rotation;b.transform.SetPositionAndRotation(b.Rigidbody.position,b.Rigidbody.rotation);}
   Physics.SyncTransforms();run=car.AddComponent<GravityGarage3DRun>();run.Bind(course,parts,parts[0]);Time.fixedDeltaTime=Mathf.Sqrt(course.WorldScale)/240;Time.timeScale=3;
  }
  IEnumerator BuildSavedTrack()
  {
   yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("DownhillSandbox");
   var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByName("DownhillSandbox");
   var roots=scene.GetRootGameObjects();
   var source=roots.SelectMany(r=>r.GetComponentsInChildren<Level2BezierTrack>(true)).Single();
   var copy=Object.Instantiate(source.gameObject);
   foreach(var go in roots)go.SetActive(false);
   root=new GameObject("Saved Bezier track verification");copy.transform.SetParent(root.transform,false);
   copy.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
   copy.transform.localScale=Vector3.one*GravityGarage25DCourse.UnitScale*.8f;copy.SetActive(true);
   course=copy.GetComponent<GravityGarage3DCourse>();course.BindAuthored();course.SplineZone.RebuildPath();
   SetupCar(.8f);
  }
  [UnityTest]public IEnumerator SavedBezierTrackParksCenteredAndRetries()
  {
   yield return BuildSavedTrack();course.SplineZone.GuidanceMode=SandboxGuidanceMode.WholeRoad;
   // Isolate parking/drilling from user-authored upper puzzle difficulty.
   var meshes=course.BezierTrack.Strips.Select(s=>s.Mesh.GetInstanceID()).ToArray();
   for(int attempt=0;attempt<2;attempt++) {
    StartAt(course.LowerStart+1,4);float deadline=Time.realtimeSinceStartup+35;
    while(!run.SplineParked && !run.Finished && Time.realtimeSinceStartup<deadline)yield return null;
    Assert.IsTrue(run.SplineActive,run.Feedback);Assert.IsTrue(run.SplineParked,run.Feedback+" progress="+run.Progress);
    Assert.IsTrue(course.WallBroken);Assert.IsFalse(run.Finished);
    var rb=parts[0].MainBody.Rigidbody;var position=rb.position;
    Assert.Less(Vector3.Distance(position,run.SplineTargetPose.position),.00001f);
    var sample=course.SplineZone.Sample(course.SplineZone.StopDistance);
    Assert.Less(Mathf.Abs(Vector3.Dot(position-sample.Position,Vector3.Cross(sample.Normal,sample.Tangent))),.00001f);
    for(int i=0;i<15;i++)yield return new WaitForFixedUpdate();
    Assert.Less(Vector3.Distance(position,rb.position),.000001f);
    CollectionAssert.AreEqual(meshes,course.BezierTrack.Strips.Select(s=>s.Mesh.GetInstanceID()));
   }
  }
  [UnityTest]public IEnumerator SavedBezierTrackBlocksVehicleWithoutDrill()
  {
   yield return BuildSavedTrack();course.SplineZone.GuidanceMode=SandboxGuidanceMode.WholeRoad;
   var drill=parts.Single(p=>p.Kind==PartKind.DownhillDrill);
   world.RemoveConnection(world.Connections.Single(c=>c.a.partId==drill.InstanceId || c.b.partId==drill.InstanceId).id);
   drill.gameObject.SetActive(false);parts=parts.Where(p=>p!=drill).ToArray();StartAt(course.LowerStart+1,4);
   float deadline=Time.realtimeSinceStartup+35;
   while(!run.Finished && Time.realtimeSinceStartup<deadline)yield return null;
   Assert.IsTrue(run.SplineActive);Assert.IsTrue(run.Finished);Assert.IsFalse(run.Success);Assert.IsFalse(course.WallBroken);
   StringAssert.Contains("Drill wall blocked",run.Feedback);
   Assert.Less(course.Project(parts[0].MainBody.Rigidbody.position).Distance,course.WallDistance);
  }
  [UnityTest]public IEnumerator ConnectionTransfersBetweenDynamicPuzzleSectionsAndRetries()
  {
   Build();AddSpline();course.SplineZone.GuidanceMode=SandboxGuidanceMode.ConnectionArea;
   for(int attempt=0;attempt<2;attempt++) {
    run.StartRun();bool guided=false;float deadline=Time.realtimeSinceStartup+35;
    while(run.GuidanceExits==0 && !run.Finished && Time.realtimeSinceStartup<deadline) {
     if(run.SplineActive){guided=true;Assert.IsTrue(parts[0].MainBody.Rigidbody.isKinematic);}
     else Assert.IsFalse(parts[0].MainBody.Rigidbody.isKinematic);
     yield return new WaitForFixedUpdate();
    }
    Assert.IsTrue(guided,"Never entered connection area: "+run.Feedback+" progress="+run.Progress+" turn="+course.TurnStart/course.Length);
    bool released=guided && run.GuidanceExits==1 && !run.SplineActive;
    Assert.IsTrue(released,"Never returned to physics: "+run.Feedback);
    Assert.IsFalse(run.Finished,run.Feedback);
    Assert.IsTrue(parts.All(p=>p.Bodies.All(b=>!b.Rigidbody.isKinematic && b.Rigidbody.detectCollisions && b.Rigidbody.useGravity)));
    Assert.AreEqual(1,run.GuidanceEntries);Assert.AreEqual(1,run.GuidanceExits);
    // The connection guarantees transfer, not solving the lower physics puzzle.
    // Physical drill/finish success is covered by CompletesAcrossScalesRatesAndRetries.
    Assert.IsFalse(run.SplineParked);
   }
  }
  void StartAt(float distance,float speed)
  {
   var pose=course.SurfacePose(course.Sample(distance),parts,parts[0],1);
   var origin=parts[0].MainBody.Rigidbody.position;var delta=pose.rotation*Quaternion.Inverse(parts[0].MainBody.Rigidbody.rotation);
   foreach(var b in parts.SelectMany(p=>p.Bodies)) {b.Rigidbody.position=pose.position+delta*(b.Rigidbody.position-origin);b.Rigidbody.rotation=delta*b.Rigidbody.rotation;b.transform.SetPositionAndRotation(b.Rigidbody.position,b.Rigidbody.rotation);}
   Physics.SyncTransforms();run.Bind(course,parts,parts[0]);run.StartRun();
   foreach(var b in parts.SelectMany(p=>p.Bodies))b.Rigidbody.linearVelocity=course.Sample(distance).Tangent*speed*Mathf.Sqrt(course.WorldScale);
  }
  [UnityTest]public IEnumerator SavedHybridTransitionReleasesToPhysicalLowerRoad()
  {
   yield return BuildSavedTrack();Assert.IsTrue(course.SplineZone.ConnectionArea);
   StartAt(course.TurnStart-1,4);float deadline=Time.realtimeSinceStartup+15;
   while(run.GuidanceExits==0 && !run.Finished && Time.realtimeSinceStartup<deadline)yield return new WaitForFixedUpdate();
   Assert.AreEqual(1,run.GuidanceEntries,run.Feedback+" progress="+run.Progress);
   Assert.AreEqual(1,run.GuidanceExits,run.Feedback);
   Assert.IsFalse(run.SplineActive);Assert.IsFalse(run.Finished,run.Feedback);
   Assert.IsTrue(parts.All(p=>p.Bodies.All(b=>!b.Rigidbody.isKinematic && b.Rigidbody.useGravity)));
   Assert.Greater(Vector3.Dot(parts[0].MainBody.Rigidbody.linearVelocity,course.Sample(course.LowerStart).Tangent),0);
  }
  [UnityTest]public IEnumerator ReverseEntryRollsThroughConnectionAreaAndReleasesWithoutFailure()
  {
   Build();AddSpline();course.SplineZone.GuidanceMode=SandboxGuidanceMode.ConnectionArea;
   StartAt(course.LowerStart+.8f,-5);float deadline=Time.realtimeSinceStartup+15;
   bool reverse=false;
   while(run.GuidanceExits==0 && !run.Finished && Time.realtimeSinceStartup<deadline){if(run.SplineActive && run.SplineSignedSpeed<0)reverse=true;yield return new WaitForFixedUpdate();}
   Assert.IsTrue(reverse,run.Feedback);Assert.AreEqual(1,run.GuidanceExits,run.Feedback);
   Assert.IsFalse(run.Finished);Assert.IsFalse(parts[0].MainBody.Rigidbody.isKinematic);
  }
  [UnityTest]public IEnumerator ConnectionCarriesEntrySpeedAndAcceleratesSmoothly()
  {
   Build();AddSpline();course.SplineZone.GuidanceMode=SandboxGuidanceMode.ConnectionArea;
   StartAt(course.TurnStart-1,4);float deadline=Time.realtimeSinceStartup+15;
   while(!run.SplineActive && !run.Finished && Time.realtimeSinceStartup<deadline)yield return new WaitForFixedUpdate();
   Assert.IsTrue(run.SplineActive,run.Feedback);
   float entry=run.SplineSignedSpeed;Assert.Greater(entry,0);
   float acceleration=course.SplineZone.TransferAcceleration;
   float step=Time.fixedDeltaTime/Mathf.Sqrt(course.WorldScale);
   while(run.SplineActive && Time.realtimeSinceStartup<deadline) {
    float before=run.SplineSignedSpeed;
    yield return new WaitForFixedUpdate();
    Assert.GreaterOrEqual(run.SplineSignedSpeed,before);
    Assert.LessOrEqual(run.SplineSignedSpeed-before,acceleration*step+.001f);
   }
   Assert.AreEqual(1,run.GuidanceExits);Assert.Greater(run.SplineSignedSpeed,entry);
   Assert.IsFalse(parts[0].MainBody.Rigidbody.isKinematic);
  }
  [UnityTest]public IEnumerator ConnectionFreezesPuzzleClockAndRetryRestoresCollision()
  {
   Build();AddSpline();course.SplineZone.GuidanceMode=SandboxGuidanceMode.ConnectionArea;
   StartAt(course.TurnStart-1,4);float deadline=Time.realtimeSinceStartup+10;
   while(!run.SplineActive && !run.Finished && Time.realtimeSinceStartup<deadline)yield return new WaitForFixedUpdate();
   Assert.IsTrue(run.SplineActive,run.Feedback);float elapsed=run.Elapsed,progress=run.Progress;
   for(int i=0;i<10;i++)yield return new WaitForFixedUpdate();
   Assert.AreEqual(elapsed,run.Elapsed);Assert.AreEqual(progress,run.Progress);
   Assert.IsTrue(parts.All(p=>p.Bodies.All(b=>!b.Rigidbody.detectCollisions)));
   run.StartRun();Assert.IsFalse(run.SplineActive);
   Assert.IsTrue(parts.All(p=>p.Bodies.All(b=>b.Rigidbody.detectCollisions && !b.Rigidbody.isKinematic)));
  }
  [UnityTest]public IEnumerator CompletesAcrossScalesRatesAndRetries()
  {
   foreach(float scale in new[]{.4f,.8f,1f}) {
    Build(scale);
    foreach(float playback in new[]{.5f,1f,3f})for(int repeat=0;repeat<2;repeat++){
     Time.timeScale=playback;run.StartRun();Assert.That(parts.All(p=>p.Bodies.All(b=>b.Rigidbody.linearVelocity==Vector3.zero)));
     while(!run.Finished)yield return null;
     string context="scale="+scale+" rate="+playback+" repeat="+repeat+" "+run.Feedback+" progress="+run.Progress;
     Debug.Log("VALLEY_MATRIX "+context);Assert.IsTrue(run.Success,context);Assert.IsTrue(course.WallBroken);Assert.AreEqual(parts.Length-1,world.Connections.Count);
    }
    Object.DestroyImmediate(root);Object.DestroyImmediate(level);root=null;level=null;
   }
  }
  [UnityTest]public IEnumerator RearwardMassHasPhysicalDisadvantage()
  {
   float frontTilt=0,frontTime=0;
   for(int rear=0;rear<2;rear++){
    Build(.8f,rear==1);run.StartRun();while(!run.Finished)yield return null;
    Debug.Log("VALLEY_BALANCE rear="+rear+" tilt="+run.MaxTilt+" time="+run.Elapsed+" d="+course.Project(parts[0].MainBody.Rigidbody.position).Distance+" p="+course.Coordinates(parts[0].MainBody.Rigidbody.position)+" "+run.Feedback);
    if(rear==0){Assert.IsTrue(run.Success,run.Feedback);frontTilt=run.MaxTilt;frontTime=run.Elapsed;}
    else Assert.IsTrue(!run.Success||run.MaxTilt>frontTilt+.1f||run.Elapsed>frontTime+.1f,"Rearward load must have measurable disadvantage.");
    Object.DestroyImmediate(root);Object.DestroyImmediate(level);root=null;level=null;
   }
  }
  [Test]public void SamplesProjectContinuouslyAndReturnLeftward()
  {
   Build();float last=-1;
   for(float d=0;d<course.Length;d+=.2f){var sample=course.Sample(d);var projected=course.Project(sample.Position);Assert.That(projected.Distance,Is.EqualTo(d).Within(.002f));Assert.Greater(projected.Distance,last);last=projected.Distance;Assert.That(Vector3.Dot(sample.Normal,sample.Tangent),Is.EqualTo(0).Within(.0001f));}
   Assert.Less(course.Sample(course.LowerStart+2).Tangent.x,0);
   Assert.AreSame(course.Road.GetComponent<MeshFilter>().sharedMesh,((MeshCollider)course.Road).sharedMesh);
  }
  void AddSpline()
  {
   var zone=course.gameObject.AddComponent<SandboxSplineZone>();
   zone.Roads=new[]{course.Road};zone.Points=course.RoutePoints.Select((p,i)=>{
    var point=new GameObject("Route "+i).transform;point.SetParent(course.transform,false);point.localPosition=p;return point;
   }).ToArray();
   zone.Stop=new GameObject("Spline stop").transform;zone.Stop.SetParent(course.transform,false);zone.Stop.position=course.Sample(course.Length-1).Position;
   zone.Speed=6;course.SplineZone=zone;
  }
  [UnityTest]public IEnumerator SplineParksCenteredWithNoDriftAndRetries()
  {
   Build();AddSpline();
   for(int attempt=0;attempt<2;attempt++) {
    run.StartRun();Assert.IsFalse(run.SplineActive);Assert.IsFalse(course.WallBroken);
    float deadline=Time.realtimeSinceStartup+20;
    while(!run.SplineParked && !run.Finished && Time.realtimeSinceStartup<deadline)yield return null;
    Assert.IsTrue(run.SplineParked,run.Feedback+" progress="+run.Progress);
    Assert.IsFalse(run.Finished,"Parking should not auto-complete the level.");Assert.IsTrue(course.WallBroken);
    var rb=parts[0].MainBody.Rigidbody;var position=rb.position;
    Assert.Less(Vector3.Distance(position,run.SplineTargetPose.position),.00001f);
    Assert.Less(Quaternion.Angle(rb.rotation,run.SplineTargetPose.rotation),.01f);
    var sample=course.SplineZone.Sample(course.SplineZone.StopDistance);
    Assert.Less(Mathf.Abs(Vector3.Dot(position-sample.Position,Vector3.Cross(sample.Normal,sample.Tangent))),.00001f);
    Assert.IsTrue(parts.SelectMany(p=>p.Bodies).All(b=>b.Rigidbody.isKinematic && !b.Rigidbody.useGravity));
    Assert.AreEqual(parts.Length-1,world.Connections.Count);
    float elapsed=run.Elapsed;
    for(int i=0;i<30;i++)yield return new WaitForFixedUpdate();
    Assert.Less(Vector3.Distance(position,rb.position),.000001f);Assert.AreEqual(elapsed,run.Elapsed);
   }
  }
  [UnityTest]public IEnumerator SplineRequiresRoadContactAndStopsAtUnbrokenWall()
  {
   Build();AddSpline();Physics.gravity=Vector3.zero;
   var shift=Vector3.up*course.WorldScale*2;
   foreach(var b in parts.SelectMany(p=>p.Bodies)){b.Rigidbody.position+=shift;b.transform.position=b.Rigidbody.position;}
   run.Bind(course,parts,parts[0]);run.StartRun();
   for(int i=0;i<10;i++)yield return new WaitForFixedUpdate();
   Assert.IsFalse(run.SplineActive,"An airborne car must not be captured by the road spline.");
   foreach(var b in parts.SelectMany(p=>p.Bodies)){b.Rigidbody.position-=shift;b.transform.position=b.Rigidbody.position;}
   var drill=parts.Single(p=>p.Kind==PartKind.DownhillDrill);
   world.RemoveConnection(world.Connections.Single(c=>c.a.partId==drill.InstanceId||c.b.partId==drill.InstanceId).id);
   drill.gameObject.SetActive(false);parts=parts.Where(p=>p!=drill).ToArray();
   run.Bind(course,parts,parts[0]);Physics.gravity=gravity;run.StartRun();
   float deadline=Time.realtimeSinceStartup+20;
   while(!run.Finished && Time.realtimeSinceStartup<deadline)yield return null;
   Assert.IsTrue(run.SplineActive);Assert.IsTrue(run.Finished);Assert.IsFalse(run.Success);Assert.IsFalse(course.WallBroken);
   StringAssert.Contains("Drill wall blocked",run.Feedback);
   Assert.Less(course.Project(parts[0].MainBody.Rigidbody.position).Distance,course.WallDistance);
  }
  [Test]public void WeakPointRejectsHighLowAndSideMissesAndResets()
  {
   Build();var target=course.transform.TransformPoint(course.WeakPoint);
   foreach(var offset in new[]{Vector3.up,Vector3.down,Vector3.forward*2})Assert.IsFalse(course.TryDrill(target+(Vector3.right+offset)*course.WorldScale,target+(-Vector3.right+offset)*course.WorldScale,.01f*course.WorldScale));
   Assert.IsTrue(course.TryDrill(target+Vector3.right*course.WorldScale,target-Vector3.right*course.WorldScale,.01f*course.WorldScale));Assert.IsFalse(course.TryDrill(target+Vector3.right*course.WorldScale,target-Vector3.right*course.WorldScale,0));
   course.ResetCourse();Assert.IsFalse(course.WallBroken);Assert.IsTrue(course.Wall.activeSelf);
  }
  [UnityTest]public IEnumerator WaterAndUnsupportedShortcutCannotFinish()
  {
   Build();run.StartRun();var shift=course.Sample(course.Length-1).Position-parts[0].MainBody.Rigidbody.position+Vector3.up*course.WorldScale*2;
   foreach(var b in parts.SelectMany(p=>p.Bodies)){b.Rigidbody.position+=shift;b.transform.position=b.Rigidbody.position;}Physics.SyncTransforms();yield return new WaitForFixedUpdate();Assert.IsFalse(run.Success);
   run.StartRun();shift=course.transform.TransformPoint(new Vector3(0,-.3f,0))-parts[0].MainBody.Rigidbody.position;
   foreach(var b in parts.SelectMany(p=>p.Bodies)){b.Rigidbody.position+=shift;b.transform.position=b.Rigidbody.position;}Physics.SyncTransforms();yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();Assert.IsTrue(run.Finished);StringAssert.Contains("water",run.Feedback);
   run.ResetVehicle();Assert.IsFalse(run.Finished);Assert.IsFalse(course.WallBroken);Assert.AreEqual(0,run.Elapsed);
  }
  [UnityTest]public IEnumerator MissingAndDetachedDrillsCannotClearWall()
  {
   Build();var drill=parts.Single(p=>p.Kind==PartKind.DownhillDrill);world.RemoveConnection(world.Connections.Single(c=>c.a.partId==drill.InstanceId||c.b.partId==drill.InstanceId).id);drill.gameObject.SetActive(false);run.StartRun();
   while(!run.Finished)yield return null;
   Assert.IsFalse(run.Success);Assert.IsFalse(course.WallBroken);Assert.IsFalse(run.DrillActive(drill));
  }
  [UnityTest]public IEnumerator GravityIsTheOnlyLaunchForce()
  {
   Build();Physics.gravity=Vector3.zero;var initial=parts[0].MainBody.Rigidbody.position;run.StartRun();for(int i=0;i<30;i++)yield return new WaitForFixedUpdate();Assert.Less(Vector3.Distance(initial,parts[0].MainBody.Rigidbody.position),.01f*course.WorldScale);
  }
  [UnityTest]public IEnumerator RollbackAndOverturnReportFailure()
  {
   Build();run.StartRun();for(int i=0;i<30;i++)yield return new WaitForFixedUpdate();foreach(var b in parts.SelectMany(p=>p.Bodies))b.Rigidbody.linearVelocity=-course.Sample(2).Tangent*2*Mathf.Sqrt(course.WorldScale);
   while(!run.Finished&&run.Elapsed<3)yield return null;Assert.IsTrue(run.Finished);StringAssert.Contains("backward",run.Feedback);
   run.StartRun();var center=parts[0].MainBody.Rigidbody.position;var turn=Quaternion.AngleAxis(180,course.Sample(2).Tangent);
   foreach(var b in parts.SelectMany(p=>p.Bodies)){b.Rigidbody.position=center+turn*(b.Rigidbody.position-center)+Vector3.up*.7f*course.WorldScale;b.Rigidbody.rotation=turn*b.Rigidbody.rotation;b.transform.SetPositionAndRotation(b.Rigidbody.position,b.Rigidbody.rotation);}Physics.SyncTransforms();
   while(!run.Finished&&run.Elapsed<3)yield return null;Assert.IsTrue(run.Finished);StringAssert.Contains("overturned",run.Feedback);
  }
  static AssemblyPart[] CreateCar(AssemblyWorld world,float scale,bool rearLoad=false)
  {
   var list=new System.Collections.Generic.List<AssemblyPart>();
   AssemblyPart Make(PartKind kind){var p=DownhillParts.Create(world,kind,new Pose(Vector3.zero,Quaternion.identity),scale,null,10,kind==PartKind.DownhillCore);p.transform.SetParent(world.transform,true);list.Add(p);foreach(var b in p.Bodies){b.Rigidbody.isKinematic=true;b.Rigidbody.useGravity=false;}return p;}
   void Join(AssemblyPart source,AssemblyPart target,Vector3 normal,bool front=false){var ts=target.Sockets.Where(s=>Vector3.Dot(s.Normal,normal)>.99f&&!world.SocketOccupied(s)).OrderBy(s=>front?-s.WorldPosition.z:s.WorldPosition.z).First();var ss=source.Sockets.OrderBy(s=>Vector3.Dot(s.Normal,normal)).First();var candidate=new AttachmentCandidate(ss.Body,ts.Body,ss.WorldPosition,ts.WorldPosition,ss,ts);Assert.IsTrue(world.PlacementClear(candidate,new[]{source}),"Reference overlap: "+source.Kind);foreach(var b in source.Bodies){var rb=b.Rigidbody;rb.position=candidate.targetPoint+candidate.rotationDelta*(rb.position-candidate.sourcePoint);rb.rotation=candidate.rotationDelta*rb.rotation;b.transform.SetPositionAndRotation(rb.position,rb.rotation);}Physics.SyncTransforms();Assert.IsTrue(world.Connect(new ConnectionRecord{id=System.Guid.NewGuid().ToString("N"),a=new BodyRef(source.InstanceId,ss.Body.Id),b=new BodyRef(target.InstanceId,ts.Body.Id),socketA=ss.Id,socketB=ts.Id,anchorA=ss.transform.localPosition,anchorB=ts.transform.localPosition}));}
   var core=Make(PartKind.DownhillCore);var beam=Make(PartKind.DownhillLongBlock);Join(beam,core,rearLoad?Vector3.forward:Vector3.back);var rear=Make(PartKind.DownhillShortBlock);Join(rear,beam,rearLoad?Vector3.forward:Vector3.back);
   foreach(int side in new[]{-1,1}){Join(Make(PartKind.DownhillLargeWheel),rearLoad?rear:beam,Vector3.right*side,true);Join(Make(PartKind.DownhillSmallWheel),rearLoad?beam:rear,Vector3.right*side,false);}
   Join(Make(PartKind.DownhillDrill),rearLoad?rear:core,Vector3.forward);
   return list.ToArray();
  }
  [TearDown]public void Cleanup(){if(root)Object.DestroyImmediate(root);if(level)Object.DestroyImmediate(level);foreach(var go in suspendedSceneRoots)if(go)go.SetActive(true);Physics.gravity=gravity;Time.fixedDeltaTime=step>0?step:.02f;Time.timeScale=rate>0?rate:1;}
 }
}

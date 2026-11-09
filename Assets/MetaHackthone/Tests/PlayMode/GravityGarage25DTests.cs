using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PhysicalAssembly.Tests
{
    public sealed class GravityGarage25DTests
    {
        GameObject root;GravityGarage25DCourse course;GravityGarage25DRun run;AssemblyWorld world;AssemblyPart[] parts;float oldStep,oldTime;
        void Build(GravityGarage25DParameters p,float scale=.8f)
        {
            oldStep=Time.fixedDeltaTime;oldTime=Time.timeScale;root=new GameObject("25D test fixture");
            var track=new GameObject("Track");track.transform.SetParent(root.transform);course=track.AddComponent<GravityGarage25DCourse>();course.Build(p,GravityGarage25DCourse.UnitScale*scale);
            var car=new GameObject("Car");car.transform.SetParent(root.transform);world=car.AddComponent<AssemblyWorld>();parts=GravityGarage25DVehicle.Create(world,p,new Pose(Vector3.zero,Quaternion.identity),scale);GravityGarage25DVehicle.Place(parts,course);
            run=root.AddComponent<GravityGarage25DRun>();run.Bind(course,parts,parts[0]);Time.fixedDeltaTime=Mathf.Sqrt(course.WorldScale)/240;Time.timeScale=3;
        }
        [TearDown] public void Cleanup(){if(root)Object.DestroyImmediate(root);Time.fixedDeltaTime=oldStep>0?oldStep:.02f;Time.timeScale=oldTime>0?oldTime:1;}
        [UnityTest] public IEnumerator SourcePresetsKeepCourseParametersAndReportRealOutcomes()
        {
            var source=new GravityGarage25DParameters();string sourceJson=GravityGarageLevelAsset.Export(source);
            for(int preset=0;preset<4;preset++) {
                var p=source.Copy();p.Preset(preset);Build(p);run.StartRun();
                while(!run.Finished && run.Elapsed<30)yield return null;
                Debug.Log("25D_RESULT preset="+preset+" "+run.Stage+" "+run.Feedback+" time="+run.Elapsed+" max="+run.MaxSpeed+" position="+course.Coordinates(run.Core.position));
                if(run.Stage==Garage25DStage.Complete){Assert.AreEqual(1,run.PipeTransfers);Assert.IsTrue(run.ValidUpperLanding);}
                Assert.IsTrue(run.Finished);Assert.IsTrue(float.IsFinite(run.MaxSpeed));Assert.AreEqual(6,world.Connections.Count);Assert.AreEqual(sourceJson,GravityGarageLevelAsset.Export(source));
                Cleanup();root=null;
            }
        }
        [UnityTest] public IEnumerator CalibratedExampleCompletesBothTracks()
        {
            var p=GravityGarage25DParameters.UnityCalibrated();Build(p);run.StartRun();while(!run.Finished&&run.Elapsed<30)yield return null;
            Debug.Log("25D_CALIBRATED "+run.Stage+" "+run.Feedback+" time="+run.Elapsed+" max="+run.MaxSpeed+" position="+course.Coordinates(run.Core.position));
            Assert.AreEqual(Garage25DStage.Complete,run.Stage,run.Feedback+" at "+course.Coordinates(run.Core.position));Assert.AreEqual(1,run.PipeTransfers);
        }
        [UnityTest] public IEnumerator CurrentCarCompletesTunedCourseAcrossScalesAndPlaybackRates()
        {
            foreach(float scale in new[]{.4f,.8f,1f}) {
                var p=GravityGarage25DParameters.UnityCalibrated();Assert.AreEqual(.37f,p.frontWheel);Assert.AreEqual(.20f,p.rearWheel);Build(p,scale);
                foreach(float rate in new[]{.5f,1f,3f}) for(int repeat=0;repeat<2;repeat++) {
                    Time.timeScale=rate;run.StartRun();while(!run.Finished&&run.Elapsed<30)yield return null;
                    string context="scale="+scale+" playback="+rate+" repeat="+repeat+" "+run.Feedback+" at "+course.Coordinates(run.Core.position);
                    Debug.Log("25D_CURRENT_CAR "+context+" time="+run.Elapsed);
                    Assert.AreEqual(Garage25DStage.Complete,run.Stage,context);Assert.IsTrue(run.ValidUpperLanding);Assert.AreEqual(1,run.PipeTransfers);Assert.AreEqual(6,world.Connections.Count);
                }
                Cleanup();root=null;
            }
        }
        [UnityTest] public IEnumerator PipeFreezesWholeVehicleAndRepeatedRunsKeepOneTransfer()
        {
            Build(GravityGarage25DParameters.UnityCalibrated());
            foreach(float rate in new[]{.5f,1f,3f}) {
                Time.timeScale=rate;run.StartRun();bool pipeSeen=false;float entered=0;Vector3[] frozen=null;
                while(!run.Finished&&run.Elapsed<30){
                    if(run.Stage==Garage25DStage.Pipe){
                        if(!pipeSeen){pipeSeen=true;entered=run.Elapsed;frozen=parts.SelectMany(p=>p.Bodies).Select(b=>b.Rigidbody.position).ToArray();}
                        Assert.IsTrue(parts.SelectMany(p=>p.Bodies).All(b=>b.Rigidbody.isKinematic&&!b.Rigidbody.detectCollisions));Assert.IsTrue(parts.SelectMany(p=>p.VisualRenderers).All(r=>!r.enabled));
                        CollectionAssert.AreEqual(frozen,parts.SelectMany(p=>p.Bodies).Select(b=>b.Rigidbody.position).ToArray());
                    }
                    if(run.Stage==Garage25DStage.Lower&&pipeSeen){Assert.GreaterOrEqual(run.Elapsed-entered,1f);Assert.AreEqual(1,run.PipeTransfers);}
                    yield return null;
                }
                Assert.IsTrue(pipeSeen);Assert.AreEqual(Garage25DStage.Complete,run.Stage,run.Feedback);Assert.AreEqual(1,run.PipeTransfers);Assert.AreEqual(6,world.Connections.Count);Assert.AreEqual(1,root.GetComponents<GravityGarage25DRun>().Length);
            }
        }
        [UnityTest] public IEnumerator PauseAndResetPreserveConnections()
        {
            Build(new GravityGarage25DParameters());run.StartRun();yield return new WaitForSeconds(.3f);run.SetPaused(true);var before=run.Core.position;float time=run.Elapsed;yield return new WaitForSeconds(.3f);Assert.AreEqual(before,run.Core.position);Assert.AreEqual(time,run.Elapsed);
            run.SetPaused(false);yield return new WaitForSeconds(.1f);Assert.Greater(run.Elapsed,time);run.ResetVehicle();Assert.AreEqual(Garage25DStage.Ready,run.Stage);Assert.AreEqual(6,world.Connections.Count);Assert.AreEqual(0,run.PipeTransfers);
        }
        [UnityTest] public IEnumerator FallingBeyondGoalIsNotSuccess()
        {
            Build(new GravityGarage25DParameters());run.StartRun();foreach(var b in parts.SelectMany(p=>p.Bodies)){b.Rigidbody.position+=course.transform.right*course.WorldScale*18+Vector3.up*course.WorldScale*2;b.transform.position=b.Rigidbody.position;}Physics.SyncTransforms();yield return new WaitForSeconds(.1f);Assert.AreNotEqual(Garage25DStage.Complete,run.Stage);Assert.AreEqual(0,run.PipeTransfers);
        }
        [UnityTest] public IEnumerator WideGapCannotTeleportWithoutLanding()
        {
            var p=new GravityGarage25DParameters{gap=2.6f,upperDrop=.8f};Build(p);run.StartRun();while(!run.Finished&&run.Elapsed<30)yield return null;Assert.AreEqual(Garage25DStage.Failed,run.Stage);Assert.AreEqual(0,run.PipeTransfers);
        }
    }
}

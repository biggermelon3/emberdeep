#if DEVELOPMENT_BUILD && UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using PhysicalAssembly;
using UnityEngine;

namespace CloudGarden.Integration
{
    // Explicit, local development-build test. No listener or remote command surface.
    // Cold-launch with --ez metahackthone.sandbox_smoke_test true to run once.
    public sealed partial class QuestSandboxSmokeTest : MonoBehaviour
    {
        const string Prefix="QUEST_SANDBOX_SMOKE ";
        int runtimeErrors;
        bool level2Acceptance;
        bool polishAcceptance;
        bool victoryAcceptance;
        bool creekAcceptance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartIfRequested()
        {
            using var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity=player.GetStatic<AndroidJavaObject>("currentActivity");
            using var intent=activity.Call<AndroidJavaObject>("getIntent");
            bool acceptance=intent.Call<bool>("getBooleanExtra","metahackthone.level2_acceptance",false);
            bool polish=intent.Call<bool>("getBooleanExtra","metahackthone.sandbox_polish_test",false);
            bool victory=intent.Call<bool>("getBooleanExtra","metahackthone.victory_test",false);
            bool creek=intent.Call<bool>("getBooleanExtra","metahackthone.creek_test",false);
            if(!creek && !victory && !polish && !acceptance && !intent.Call<bool>("getBooleanExtra","metahackthone.sandbox_smoke_test",false)) return;
            intent.Call("removeExtra","metahackthone.creek_test");
            intent.Call("removeExtra","metahackthone.victory_test");
            intent.Call("removeExtra","metahackthone.sandbox_polish_test");
            intent.Call("removeExtra","metahackthone.level2_acceptance");
            intent.Call("removeExtra","metahackthone.sandbox_smoke_test");
            var runner=new GameObject("Quest Sandbox smoke test");
            DontDestroyOnLoad(runner); var test=runner.AddComponent<QuestSandboxSmokeTest>(); test.level2Acceptance=acceptance; test.polishAcceptance=polish; test.victoryAcceptance=victory;test.creekAcceptance=creek;
        }
        IEnumerator Start()
        {
            Application.logMessageReceived+=CountErrors;
            var test=creekAcceptance?RunCreek():victoryAcceptance?RunVictory():polishAcceptance?RunPolish():level2Acceptance?RunLevel2():Run();
            while(true)
            {
                bool next;
                try { next=test.MoveNext(); }
                catch(Exception error)
                {
                    Debug.LogError(Prefix+"FAIL "+error);
                    var nav=GardenNavigation.Current;
                    if(nav && nav.DownhillWorkshop) nav.DownhillWorkshop.Downhill.RestoreBuildImmediately();
                    if(level2Acceptance) DesignModeSession.SetEnabled(false);
                    if(nav) nav.ReturnToMainMenu();
                    break;
                }
                if(!next) break;
                yield return test.Current;
            }
            Destroy(gameObject);
        }
        void CountErrors(string message,string stack,LogType type)
        { if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert) runtimeErrors++; }
        void OnDestroy() => Application.logMessageReceived-=CountErrors;
        static void Require(bool condition,string message)
        { if(!condition) throw new InvalidOperationException(message); }
        static void Stage(string name)
        {
            var renderers=FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                .Where(r=>r.enabled && r.gameObject.activeInHierarchy).ToArray();
            Require(renderers.Length>0,"No active mesh renderers in "+name);
            foreach(var renderer in renderers)
                foreach(var material in renderer.sharedMaterials)
                    Require(material && material.shader && material.shader.isSupported,"Unsupported material: "+renderer.name);
            var stereo=FindObjectsByType<Camera>(FindObjectsSortMode.None).Any(c=>c.enabled && c.stereoEnabled);
            Require(stereo,"XR stereo camera is not rendering");
            Debug.Log(Prefix+"CAPTURE "+name+" meshes="+renderers.Length+" stereo="+stereo);
        }
        IEnumerator RunPolish()
        {
            float deadline=Time.realtimeSinceStartup+30;
            while(!GardenNavigation.Current && Time.realtimeSinceStartup<deadline) yield return null;
            var nav=GardenNavigation.Current; Require(nav,"Navigation missing");
            DesignModeSession.SetEnabled(false); nav.EnterSandbox(); deadline=Time.realtimeSinceStartup+30;
            while((nav.IsTransitioning || !nav.DownhillWorkshop) && Time.realtimeSinceStartup<deadline) yield return null;
            Require(nav.DownhillWorkshop,"Sandbox missing");
            var level=nav.DownhillWorkshop.Downhill; yield return new WaitForSecondsRealtime(2);
            Require(level.Course.SandboxPolish && level.PalmMenu && !level.PalmMenu.IsOpen,"Palm menu must start hidden");
            Require(level.TryResetBuild(),"Reset failed"); yield return null;
            Require(SandboxPartHighlights.Draws.Count>0,"No loose outlines registered");
            Stage("Polish-build"); Debug.Log(Prefix+"POLISH_READY");
            yield return new WaitForSecondsRealtime(8);
            var frames=new System.Collections.Generic.List<float>();
            for(int i=0;i<180;i++) { frames.Add(Time.unscaledDeltaTime*1000); yield return null; }
            frames.Sort(); Debug.Log(Prefix+"POLISH_FRAME_TIME median_ms="+frames[90]+" p95_ms="+frames[171]);
            var part=level.Kit.First(p=>p!=level.Core); var home=part.MainBody.Rigidbody.position;
            var offset=level.Course.BuildArea.transform.TransformPoint(level.Course.BuildArea.Size)-home;
            foreach(var body in part.Bodies) { body.Rigidbody.position+=offset; body.transform.position=body.Rigidbody.position; }
            Physics.SyncTransforms(); yield return new WaitForSecondsRealtime(.2f);
            Require(level.IsReturning(part),"Boundary return did not start");
            Require(!level.Workshop.World.TryGrab(2,part,new Pose(part.MainBody.Rigidbody.position,part.MainBody.Rigidbody.rotation)),"Returning part accepted grab");
            yield return new WaitForSecondsRealtime(1.2f);
            Require(!level.IsReturning(part) && Vector3.Distance(home,part.MainBody.Rigidbody.position)<.001f,"Return pose incorrect");
            Require(part.VisualRenderers.All(r=>r.enabled),"Return left hidden renderers");
            Require(runtimeErrors==0,"Runtime errors during polish test: "+runtimeErrors);
            Debug.Log(Prefix+"POLISH_PASS menu_hidden=pass outline_registry=pass return=pass stereo=pass manual_palm_and_visual_review_required");
        }
        IEnumerator RunLevel2()
        {
            float deadline=Time.realtimeSinceStartup+30;
            while(!GardenNavigation.Current && Time.realtimeSinceStartup<deadline)yield return null;
            var nav=GardenNavigation.Current;Require(nav,"Navigation missing");
            DesignModeSession.SetEnabled(true);DesignModeSession.SelectLevel(DesignLevelId.WaterfallValley);
            nav.EnterSandbox();deadline=Time.realtimeSinceStartup+30;
            while((nav.IsTransitioning || !nav.DownhillWorkshop) && Time.realtimeSinceStartup<deadline)yield return null;
            Require(nav.DownhillWorkshop,"Level 2 load timed out");
            var level=nav.DownhillWorkshop.Downhill;
            Require(level.CurrentCourse==SandboxCourseId.TeleportPipes,"Wrong course");
            for(int run=1;run<=3;run++){
                level.RestoreBuildImmediately();Require(level.TryLoadDesignerExample(),"Default car assembly failed");
                Debug.Log(Prefix+"LEVEL2_READY run="+run+" body=DownhillShortBlock front=Large rear=Small drill=Front");
                yield return new WaitForSecondsRealtime(5);
                level.Presentation.ReadyButton.Press();deadline=Time.realtimeSinceStartup+20;
                while(level.State!=DownhillState.Running && Time.realtimeSinceStartup<deadline)yield return null;
                Require(level.State==DownhillState.Running,"START timed out");
                Debug.Log(Prefix+"LEVEL2_DRIVING run="+run);
                var frames=new System.Collections.Generic.List<float>();deadline=Time.realtimeSinceStartup+120;
                while(level.State==DownhillState.Running && Time.realtimeSinceStartup<deadline){frames.Add(Time.unscaledDeltaTime*1000);yield return null;}
                frames.Sort();Require(frames.Count>0,"No driving frames");
                Debug.Log(Prefix+"LEVEL2_RESULT run="+run+" state="+level.State+" frames="+frames.Count+" p95_ms="+frames[(int)((frames.Count-1)*.95f)]+" max_ms="+frames[frames.Count-1]+" over_budget="+frames.Count(ms=>ms>1000f/90));
                Require(level.State==DownhillState.Success,"Level 2 was not completed");
            }
            Require(runtimeErrors==0,"Runtime errors");
            Debug.Log(Prefix+"LEVEL2_GAMEPLAY_PASS performance_requires_Quest_trace_verification");
            level.RestoreBuildImmediately();DesignModeSession.SetEnabled(false);nav.ReturnToMainMenu();
        }
        IEnumerator Run()
        {
            Debug.Log(Prefix+"BEGIN");
            float deadline=Time.realtimeSinceStartup+20;
            while(!GardenNavigation.Current && Time.realtimeSinceStartup<deadline) yield return null;
            var nav=GardenNavigation.Current; Require(nav,"Navigation missing");
            yield return new WaitForSecondsRealtime(2);
            Require(nav.InMainMenu,"Test must start in MainMenu");
            Stage("main-menu"); yield return new WaitForSecondsRealtime(10);

            nav.EnterSandbox(); deadline=Time.realtimeSinceStartup+20;
            while(nav.IsTransitioning && Time.realtimeSinceStartup<deadline) yield return null;
            Require(!nav.IsTransitioning && nav.InDownhill,"Sandbox navigation timed out");
            yield return new WaitForSecondsRealtime(1);
            var workshop=nav.DownhillWorkshop; Require(workshop && workshop.Downhill,"Sandbox missing");
            var level=workshop.Downhill; var world=workshop.World;
            Require(level.IsBuilding,"Wrong build mode");
            Require(level.Kit.Count==7 && world.Parts.Count==7,"Expected seven authored parts");
            Require(level.Kit.All(p=>p.Kind!=PartKind.DownhillIrregular),"Empty slot spawned a part");
            Require(level.Presentation.WorkbenchVisuals.gameObject.activeSelf,"Table visibility incorrect");
            var original=level.Kit.Select(p=>new Pose(p.MainBody.Rigidbody.position,p.MainBody.Rigidbody.rotation)).ToArray();
            var loose=level.Kit.First(p=>p.Kind==PartKind.DownhillShortBlock);
            var body=loose.MainBody.Rigidbody; var home=body.position;
            Require(world.TryGrab(2,loose,new Pose(home,body.rotation)),"Could not grab loose part");
            world.SetHoldPose(2,new Pose(home+Vector3.up*.1f,body.rotation));
            yield return new WaitForSecondsRealtime(.6f);
            world.Release(2,false);
            Require(Vector3.Distance(home,body.position)>.02f,"Part did not move");
            var wheels=level.Kit.Where(p=>p.Kind==PartKind.DownhillLargeWheel).ToArray();
            AttachWheel(workshop,wheels[0],1); AttachWheel(workshop,wheels[1],-1);
            Require(world.Connections.Count==2,"Wheel connections missing");
            Stage("Workbench-build"); yield return new WaitForSecondsRealtime(10);
            level.Presentation.ReadyButton.Press(); deadline=Time.realtimeSinceStartup+15;
            while((level.State==DownhillState.Transition || level.State==DownhillState.Countdown) && Time.realtimeSinceStartup<deadline) yield return null;
            Require(level.State==DownhillState.Running || level.State==DownhillState.Success || level.State==DownhillState.Failed,"START did not run physics");
            Require(level.SimulationWorld && level.SimulationWorld.Parts.Count==3 && level.SimulationWorld.Connections.Count==2,"Simulation did not preserve attached vehicle");
            Stage("Workbench-simulation"); yield return new WaitForSecondsRealtime(10);
            level.Presentation.BackButton.Press(); deadline=Time.realtimeSinceStartup+10;
            while(!level.IsBuilding && Time.realtimeSinceStartup<deadline) yield return null;
            Require(level.IsBuilding && !level.SimulationWorld && world.Connections.Count==2,"BACK TO BUILD failed");
            level.Presentation.Interface.ResetButton.Press();
            Require(world.Connections.Count==0,"RESET left connections behind");
            for(int i=0;i<level.Kit.Count;i++)
            {
                var rb=level.Kit[i].MainBody.Rigidbody;
                Require(Vector3.Distance(original[i].position,rb.position)<.001f && Quaternion.Angle(original[i].rotation,rb.rotation)<.1f,"RESET did not restore part "+i);
            }
            Debug.Log(Prefix+"MODE_PASS Workbench parts=7 move=pass attach=pass start=pass back=pass reset=pass");
            nav.ReturnToMainMenu(); deadline=Time.realtimeSinceStartup+20;
            while(nav.IsTransitioning && Time.realtimeSinceStartup<deadline) yield return null;
            Require(nav.InMainMenu && !nav.IsTransitioning,"Return to main menu failed");
            yield return new WaitForSecondsRealtime(1);

            Require(runtimeErrors==0,"Runtime errors during smoke test: "+runtimeErrors);
            Debug.Log(Prefix+"PASS");
        }
        static void AttachWheel(WorkshopBootstrap workshop,AssemblyPart wheel,int side)
        {
            var core=workshop.Downhill.Core;
            var socket=core.Sockets.Where(s=>s.Id.Contains(side>0?"x+":"x-") && !workshop.World.SocketOccupied(s))
                .OrderBy(s=>Mathf.Abs(s.transform.localPosition.z)+Mathf.Abs(s.transform.localPosition.y)*2).First();
            var mount=wheel.FindSocket("mount"); var rotation=core.MainBody.Rigidbody.rotation*Quaternion.Euler(0,side>0?0:180,0);
            foreach(var body in wheel.Bodies)
            {
                body.Rigidbody.position=socket.WorldPosition-rotation*mount.transform.localPosition;
                body.Rigidbody.rotation=rotation; body.transform.SetPositionAndRotation(body.Rigidbody.position,rotation);
            }
            Physics.SyncTransforms();
            Require(workshop.World.Connect(new ConnectionRecord {
                id=Guid.NewGuid().ToString("N"),a=new BodyRef(wheel.InstanceId,mount.Body.Id),b=new BodyRef(core.InstanceId,socket.Body.Id),
                socketA=mount.Id,socketB=socket.Id,anchorA=mount.transform.localPosition,anchorB=socket.transform.localPosition }),"Could not attach wheel");
            Require(Vector3.Distance(mount.WorldPosition,socket.WorldPosition)<.001f,"Mesh and attachment point separated");
        }
    }
}
#endif

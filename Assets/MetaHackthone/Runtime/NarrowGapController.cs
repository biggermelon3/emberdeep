using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PhysicalAssembly
{
    public enum ChallengeState { Building, InFlight, Resolved, LaunchReady, Transition }

    public sealed partial class NarrowGapController : MonoBehaviour, IBuildModeTarget
    {
        public WorkshopBootstrap Workshop { get; private set; }
        public NarrowGapSettings Settings { get; private set; }
        public NarrowGapCourse Course { get; private set; }
        public ChallengeState State { get; private set; }=ChallengeState.LaunchReady;
        public int SavedCount { get; private set; }
        public bool Won { get; private set; }
        public int LaunchedBallCount => spheres.Count(p=>p && launched.Contains(p.MainBody));
        public bool CanRetry => !launchPending && snapshot.Count>0 && (State==ChallengeState.InFlight || State==ChallengeState.Resolved) && BuildCyclesUsed<10 && resetAt<0;
        public event Action Changed;
        public bool TrySwitchMode() => IsBuilding?TryEnterLaunch():TryEnterBuilding();
        public IReadOnlyList<AssemblyPart> Spheres => spheres;
        readonly List<AssemblyPart> spheres=new();
        readonly List<BodyState> snapshot=new();
        readonly HashSet<AssemblyBody> launched=new();
        readonly Dictionary<string,Vector3> previous=new();
        readonly HashSet<string> enteredGate=new(), passedGate=new(), invalidRoute=new();
        bool launchPending;
        int launchWarmup;
        bool clearingPad;
        float elapsed, settled;
        struct BodyState
        {
            public AssemblyBody Body;
            public Vector3 Position;
            public Quaternion Rotation;
            public CollisionDetectionMode Detection;
            public RigidbodyInterpolation Interpolation;
        }
        public void Initialize(WorkshopBootstrap workshop,NarrowGapSettings settings,NarrowGapCourse course)
        { Workshop=workshop; Settings=settings; Course=course; InitializeBuilding(); }

        public void SpawnKit()
        {
            var arena=Workshop.Arena; var factory=Workshop.Factory;
            if(Workshop.Layout)
            {
                foreach(var spawn in Workshop.Layout.Spawns)
                {
                    var part=factory.Create(spawn.Kind,spawn.transform.position,spawn.transform.rotation);
                    if(spawn.Kind==PartKind.PhysicsSphere) spheres.Add(part);
                }
            }
            else
            {
            for(int i=0;i<16;i++) factory.Create(PartKind.Cuboid,arena.TransformPoint(new Vector3(.61f+(i%4)*.19f,.56f+(i/8)*.095f,-.37f+((i/4)%2)*.34f)),arena.rotation*Quaternion.Euler(0,90,0));
            for(int i=0;i<12;i++) factory.Create(PartKind.SmallCube,arena.TransformPoint(new Vector3(-.65f-(i%3)*.12f,.56f,-.38f+(i/3)*.12f)),arena.rotation);
            for(int i=0;i<4;i++) factory.Create(PartKind.Beam,arena.TransformPoint(new Vector3(1.28f,.55f+i*.025f,0)),arena.rotation);
            for(int i=0;i<5;i++) spheres.Add(factory.Create(PartKind.PhysicsSphere,arena.TransformPoint(new Vector3(-.85f+i*.065f,.56f,.29f)),arena.rotation));
            }
            SpawnCore(); Workshop.Session.Select(Core);
            Workshop.Session.Notify("Build: thumb up 2s, then index + middle. Aim: index + thumb out 1s, then fold thumb to launch.");
            Changed?.Invoke();
        }

        public bool TryLaunch()
        {
            if(State!=ChallengeState.LaunchReady) return false;
            if(Workshop.World.IsBusy || Workshop.World.HasHolds || Workshop.RepositionRequested)
            { Workshop.Session.Notify("Release your parts and finish moving or attaching before Launch."); return false; }
            if(!Core) return false;
            FreezeAimForLaunch();
            Workshop.Session.EnterBuild(); Workshop.World.EditingEnabled=false;
            State=ChallengeState.InFlight; launchPending=true; elapsed=settled=0; Won=false; SavedCount=0;
            // Let physics commit a just-docked pose before enabling swept collision.
            // Otherwise CCD can interpret the docking teleport as a downward impact.
            launchWarmup=1;
            Changed?.Invoke(); return true;
        }
        HashSet<AssemblyBody> FindLoad()
        {
            var result=new HashSet<AssemblyBody>(CoreParts.SelectMany(p=>p.Bodies));
            Physics.SyncTransforms();
            // Propagate support through touching balls, never through spare construction parts.
            for(int pass=0;pass<spheres.Count;pass++) foreach(var sphere in spheres)
            {
                if(!sphere || result.Contains(sphere.MainBody)) continue;
                var centre=sphere.MainBody.Rigidbody.position;
                foreach(var hit in Physics.SphereCastAll(centre+Up*.005f,.019f,-Up,.014f,~0,QueryTriggerInteraction.Ignore))
                {
                    var support=hit.collider.GetComponentInParent<AssemblyBody>();
                    if(support && support!=sphere.MainBody && result.Contains(support) && Vector3.Dot(hit.normal,Up)>.15f)
                    { result.Add(sphere.MainBody); break; }
                }
            }
            return result;
        }
        void BeginLaunch()
        {
            launchPending=false; launched.Clear();
            if(aimedLaunch) { foreach(var saved in aimBodies) if(saved.Body) launched.Add(saved.Body); RestoreAimBodies(false); }
            else launched.UnionWith(FindLoad());
            if(launched.Count==0) { State=ChallengeState.LaunchReady; Workshop.World.EditingEnabled=true; Changed?.Invoke(); return; }
            if(!aimedLaunch) CaptureSnapshot();
            previous.Clear(); enteredGate.Clear(); passedGate.Clear(); invalidRoute.Clear();
            foreach(var ball in spheres) if(ball) previous[ball.InstanceId]=Workshop.Arena.InverseTransformPoint(ball.MainBody.Rigidbody.position);
            RemovePadRestraint();
            var velocity=aimedLaunch?AimVelocity:Workshop.Arena.TransformDirection(Settings.LaunchVelocity);
            aimedLaunch=false; aimBodies.Clear(); aimColliders.Clear();
            foreach(var body in launched)
            {
                var rb=body.Rigidbody; rb.collisionDetectionMode=body.Part.Kind==PartKind.PhysicsSphere?CollisionDetectionMode.ContinuousDynamic:CollisionDetectionMode.ContinuousSpeculative;
                rb.linearVelocity=velocity; rb.angularVelocity=Vector3.zero; rb.WakeUp();
            }
            Workshop.World.Collisions.Reapply();
            // The pad releases the whole welded load. Its contact skin must not
            // catch bottom edges during the first swept step away from the dock.
            var pad=Course.PadCollider;
            foreach(var body in launched) foreach(var c in body.Colliders) Workshop.World.Collisions.Acquire(c,pad,"launch-pad");
            clearingPad=true;
            Course?.Launch(); Workshop.Session.Notify("Through the gap! Land all five balls on the flag platform."); Changed?.Invoke();
        }
        void FixedUpdate()
        {
            if(State!=ChallengeState.InFlight) return;
            if(launchPending) { if(launchWarmup--<=0) BeginLaunch(); return; }
            elapsed+=Time.fixedDeltaTime;
            if(clearingPad && elapsed>=.15f) { Workshop.World.Collisions.Release("launch-pad"); clearingPad=false; }
            int count=0;
            foreach(var ball in spheres)
            {
                if(!ball || !launched.Contains(ball.MainBody)) continue;
                var rb=ball.MainBody.Rigidbody; var pos=Workshop.Arena.InverseTransformPoint(rb.position);
                if(previous.TryGetValue(ball.InstanceId,out var old)) TrackGate(ball.InstanceId,old,pos,.02f);
                previous[ball.InstanceId]=pos;
                if(passedGate.Contains(ball.InstanceId) && (Course.TargetArea?Course.TargetArea.Contains(rb.position):Settings.TargetBounds.Contains(pos)) && rb.linearVelocity.sqrMagnitude<Settings.SettleSpeed*Settings.SettleSpeed) count++;
            }
            if(count!=SavedCount) { SavedCount=count; Changed?.Invoke(); }
            settled=count==5?settled+Time.fixedDeltaTime:0;
            if(settled>=Settings.SettleDuration) Resolve(true);
            else if(elapsed>=Settings.AttemptDuration) Resolve(false);
        }
        public static bool CrossesOpening(Vector3 a,Vector3 b,float plane,float halfWidth,float bottom,float top,float radius)
        {
            if(a.z>=plane || b.z<plane || b.z<=a.z) return false;
            var p=Vector3.Lerp(a,b,(plane-a.z)/(b.z-a.z));
            return Mathf.Abs(p.x)+radius<=halfWidth && p.y-radius>=bottom && p.y+radius<=top;
        }
        void TrackGate(string id,Vector3 a,Vector3 b,float radius)
        {
            if(invalidRoute.Contains(id) || passedGate.Contains(id)) return;
            float near=Settings.GateDistance-Settings.GateThickness*.5f-radius;
            float far=Settings.GateDistance+Settings.GateThickness*.5f+radius;
            float halfWidth=Settings.GapWidth*.5f,bottom=.5f,top=.5f+Settings.GateHeight;
            if(Course.GateOpening)
            {
                var gate=Course.GateOpening;
                a=gate.transform.InverseTransformPoint(Workshop.Arena.TransformPoint(a));
                b=gate.transform.InverseTransformPoint(Workshop.Arena.TransformPoint(b));
                near=-gate.Size.z*.5f-radius; far=gate.Size.z*.5f+radius;
                halfWidth=gate.Size.x*.5f; bottom=-gate.Size.y*.5f; top=gate.Size.y*.5f;
            }
            bool crossedNear=a.z<near && b.z>=near;
            if(crossedNear)
            {
                if(CrossesOpening(a,b,near,halfWidth,bottom,top,radius)) enteredGate.Add(id);
                else invalidRoute.Add(id);
            }
            if(b.z>=near && b.z<=far && (Mathf.Abs(b.x)+radius>halfWidth || b.y+radius>top || b.y-radius<bottom)) invalidRoute.Add(id);
            if(a.z<far && b.z>=far)
            {
                if(enteredGate.Contains(id) && !invalidRoute.Contains(id) && CrossesOpening(a,b,far,halfWidth,bottom,top,radius)) passedGate.Add(id);
                else invalidRoute.Add(id);
            }
        }
        void Resolve(bool won)
        {
            Won=won; State=ChallengeState.Resolved;
            Workshop.World.Collisions.Release("launch-pad"); clearingPad=false;
            Workshop.Session.Notify(won?"Five saved! Your container made it. Retry to build another solution.":$"{SavedCount}/5 saved. Retry keeps your container and restores every ball.");
            if(BuildCyclesUsed>=10) { resetAt=Time.unscaledTime+5; Workshop.Session.Notify($"{SavedCount}/5 saved. Ten cycles complete — restarting in 5 seconds."); }
            if(won) Course?.Celebrate(); Changed?.Invoke();
        }
        public bool ResetLevel()
        {
            var world=Workshop.World;
            if(world.IsBusy || world.HasHolds || Workshop.RepositionRequested)
            { Workshop.Session.Notify("Release your parts and finish moving or attaching before Reset Level."); return false; }
            CancelAim(); launchPending=false; elapsed=settled=0;
            world.Collisions.Release("launch-pad"); clearingPad=false;
            ResetBuilding();
            State=ChallengeState.LaunchReady; SavedCount=0; Won=false;
            snapshot.Clear(); launched.Clear(); previous.Clear(); enteredGate.Clear(); passedGate.Clear(); invalidRoute.Clear(); spheres.Clear();
            world.EditingEnabled=true; Workshop.Session.EnterBuild(); Workshop.Session.Selected=null;
            foreach(var part in world.Parts.Values.ToArray())
            {
                world.UnregisterPart(part); part.gameObject.SetActive(false); Destroy(part.gameObject);
            }
            Course?.ResetEffects(); SpawnKit(); Physics.SyncTransforms();
            Workshop.Session.Notify("Level reset. Fresh building parts and five balls are ready.");
            Changed?.Invoke(); return true;
        }
        public bool Retry()
        {
            if(!CanRetry || Workshop.World.IsBusy || Workshop.World.HasHolds) return false;
            CancelAim();
            launchPending=false;
            Workshop.World.Collisions.Release("launch-pad"); clearingPad=false;
            foreach(var state in snapshot) if(state.Body)
            {
                var rb=state.Body.Rigidbody; rb.interpolation=RigidbodyInterpolation.None;
                rb.position=Workshop.Arena.TransformPoint(state.Position); rb.rotation=Workshop.Arena.rotation*state.Rotation;
                state.Body.transform.SetPositionAndRotation(rb.position,rb.rotation);
                if(!rb.isKinematic) { rb.linearVelocity=Vector3.zero; rb.angularVelocity=Vector3.zero; rb.WakeUp(); }
                rb.collisionDetectionMode=state.Detection;
            }
            Physics.SyncTransforms();
            foreach(var state in snapshot) if(state.Body) state.Body.Rigidbody.interpolation=state.Interpolation;
            Workshop.World.Collisions.Reapply(); launched.Clear(); previous.Clear(); enteredGate.Clear(); passedGate.Clear(); invalidRoute.Clear();
            State=ChallengeState.LaunchReady; SavedCount=0; Won=false; elapsed=settled=0; Workshop.World.EditingEnabled=true;
            AttachToPad(); ModeGesture.Reset(true); GestureFeedback?.Clear();
            Course?.ResetEffects(); Workshop.Session.EnterBuild(); Workshop.Session.Notify("Setup restored. Switch to Building to edit, or launch again."); Changed?.Invoke(); return true;
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PhysicalAssembly
{
    public sealed partial class NarrowGapController
    {
        public AssemblyPart Core { get; private set; }
        public int BuildCyclesUsed { get; private set; }
        public int RemainingBuildCycles => Mathf.Max(0, 10 - BuildCyclesUsed);
        public bool IsBuilding => State == ChallengeState.Building;
        public ModeGestureDirection GestureDirection => IsBuilding?ModeGestureDirection.Close:ModeGestureDirection.Open;
        public bool CanSwitchMode => Core && !Workshop.World.HasHolds && !Workshop.World.IsBusy && !Workshop.RepositionRequested &&
            (IsBuilding || State == ChallengeState.LaunchReady && RemainingBuildCycles > 0);
        public AssemblySocket DockSocket { get; private set; }
        public bool DockValid { get; private set; }
        public string DockProblem { get; private set; }
        public bool SupportsModeGesture => true;
        public BuildModeGesture ModeGesture { get; } = new();
        public ChallengeGestureFeedback GestureFeedback { get; private set; }
        readonly Dictionary<AssemblyBody, PhysicsDefaults> physicsDefaults = new();
        readonly List<AssemblyPart> coreParts = new();
        readonly HashSet<AssemblyPart> coreSet = new();
        readonly Collider[] surfaceOverlaps=new Collider[64];
        readonly RaycastHit[] surfaceHits=new RaycastHit[64];
        int coreTopology = -1;
        bool padDocked;
        LineRenderer dockLaser;
        float nextDockPreview, resetAt = -1, disabledAt = -1;
        struct PhysicsDefaults { public bool gravity; public float linear, angular; }
        Vector3 PadPoint => Course.DockPoint?Course.DockPoint.position:Workshop.Arena.TransformPoint(new Vector3(0, Settings.PadHeight, 0));
        Vector3 Up => Course.transform.up;
        public IReadOnlyList<AssemblyPart> CoreParts { get { RefreshCoreGraph(); return coreParts; } }
        void RefreshCoreGraph()
        {
            if (coreTopology == Workshop.World.TopologyVersion && Core) return;
            coreTopology = Workshop.World.TopologyVersion; coreParts.Clear(); coreSet.Clear();
            Workshop.World.Collisions.Release("challenge-core");
            if (!Core) return;
            coreParts.AddRange(Workshop.World.GetConnectedParts(Core.InstanceId)); coreSet.UnionWith(coreParts);
            // A welded container must not fight its own touching, non-adjacent faces.
            // Keep balls and detached parts collidable with the complete hull.
            for(int i=0;i<coreParts.Count;i++) for(int j=i+1;j<coreParts.Count;j++)
                foreach(var a in coreParts[i].Bodies) foreach(var b in coreParts[j].Bodies)
                    foreach(var ac in a.Colliders) foreach(var bc in b.Colliders)
                        Workshop.World.Collisions.Acquire(ac,bc,"challenge-core");
        }
        public bool IsCoreConnected(AssemblyPart part) { RefreshCoreGraph(); return part && coreSet.Contains(part); }
        void InitializeBuilding()
        {
            Workshop.World.Challenge = this;
            Workshop.World.TopologyChanged += RefreshCoreGraph;
            GestureFeedback = gameObject.AddComponent<ChallengeGestureFeedback>(); GestureFeedback.Initialize(this);
            var go = new GameObject("Dock preview laser"); go.transform.SetParent(Workshop.Arena, false);
            dockLaser = go.AddComponent<LineRenderer>(); dockLaser.positionCount = 2; dockLaser.startWidth = .003f; dockLaser.endWidth = .0015f;
            dockLaser.sharedMaterial = PartFactory.Material(Color.green); dockLaser.enabled = false;
        }
        void SpawnCore()
        {
            Core = Workshop.Factory.Create(PartKind.SmallCube, PadPoint + Up * (BuildingGeometry.Unit * .5f), Course.DockPoint?Course.DockPoint.rotation:Workshop.Arena.rotation);
            Core.name = "Gold building core";
            var gold = PartFactory.Material(new Color(1f, .64f, .06f));
            if (gold.HasProperty("_Metallic")) gold.SetFloat("_Metallic", .7f);
            foreach (var r in Core.GetComponentsInChildren<MeshRenderer>()) if (r.name != "Grid points") r.sharedMaterial = gold;
            coreTopology = -1; State = ChallengeState.LaunchReady;
            UpdateDockCandidate(); AttachToPad();
        }
        public bool TryEnterBuilding()
        {
            if (!CanSwitchMode || State != ChallengeState.LaunchReady) return false;
            CancelAim();
            State = ChallengeState.Transition; RemovePadRestraint();
            RefreshCoreGraph();
            float bottom = coreParts.SelectMany(p => p.Bodies).SelectMany(b => b.Colliders).Min(c => Vector3.Dot(c.bounds.min - PadPoint, Up));
            float lift = Mathf.Max(Settings.BuildLift, .10f - bottom);
            Core.MainBody.Anchored = true; Core.MainBody.Rigidbody.isKinematic = true;
            MoveStructure(Up * lift + Vector3.ProjectOnPlane(PadPoint-Core.MainBody.Rigidbody.position,Up));
            foreach (var ball in spheres) if (ball) ResetBallToTray(ball, spheres.IndexOf(ball));
            State = ChallengeState.Building; BuildCyclesUsed++; SetConstructionPhysics(true);
            snapshot.Clear(); Won = false; SavedCount = 0; ModeGesture.Reset(true);
            Workshop.Session.Notify($"BUILDING — {RemainingBuildCycles} cycles remaining. Right hand attaches; left hand removes outer parts.");
            UpdateDockCandidate(); GestureFeedback.ModeChanged(true); Changed?.Invoke(); return true;
        }
        public bool TryEnterLaunch()
        {
            if (!CanSwitchMode || !IsBuilding) return false;
            UpdateDockCandidate();
            if (!DockValid) { Workshop.Session.Notify(DockProblem); return false; }
            State = ChallengeState.Transition;
            MoveStructure(PadPoint - DockSocket.WorldPosition);
            SetConstructionPhysics(false); Core.MainBody.Anchored = false; Core.MainBody.Rigidbody.isKinematic = false;
            AttachToPad(); State = ChallengeState.LaunchReady; dockLaser.enabled = false; ModeGesture.Reset(true);
            Workshop.Session.Notify("LAUNCH — load balls. Hold index + thumb out for 1 second to aim; fold thumb to launch."); GestureFeedback.ModeChanged(false); Changed?.Invoke(); return true;
        }
        void MoveStructure(Vector3 delta)
        {
            foreach (var part in CoreParts) foreach (var body in part.Bodies)
            {
                var rb = body.Rigidbody; rb.position += delta; body.transform.position = rb.position;
                if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            }
            Physics.SyncTransforms();
        }
        void ResetBallToTray(AssemblyPart ball, int index)
        {
            var rb = ball.MainBody.Rigidbody;
            var markers=Workshop.Layout?Workshop.Layout.Spawns.Where(s=>s.Kind==PartKind.PhysicsSphere).ToArray():null;
            rb.position = markers!=null && index<markers.Length?markers[index].transform.position:Workshop.Arena.TransformPoint(new Vector3(-.85f + index * .065f, .56f, .29f));
            ball.MainBody.transform.position = rb.position; rb.linearVelocity = rb.angularVelocity = Vector3.zero; rb.useGravity = true;
        }
        public void ApplyBuildingPhysics(AssemblyPart part)
        {
            if (!IsBuilding || part.Kind == PartKind.PhysicsSphere) return;
            foreach (var body in part.Bodies)
            {
                var rb = body.Rigidbody;
                if (!physicsDefaults.ContainsKey(body)) physicsDefaults.Add(body, new PhysicsDefaults { gravity = rb.useGravity, linear = rb.linearDamping, angular = rb.angularDamping });
                rb.useGravity = false; rb.linearDamping = Settings.BuildDamping; rb.angularDamping = Settings.BuildDamping;
                if (!rb.isKinematic) rb.linearVelocity = rb.angularVelocity = Vector3.zero;
            }
        }
        void SetConstructionPhysics(bool building)
        {
            if (building) { foreach (var p in Workshop.World.Parts.Values) ApplyBuildingPhysics(p); return; }
            foreach (var pair in physicsDefaults) if (pair.Key)
            {
                var rb = pair.Key.Rigidbody; rb.useGravity = pair.Value.gravity; rb.linearDamping = pair.Value.linear; rb.angularDamping = pair.Value.angular;
                if (!rb.isKinematic) rb.linearVelocity = rb.angularVelocity = Vector3.zero;
            }
            physicsDefaults.Clear();
        }
        void AttachToPad()
        {
            RemovePadRestraint(); if (!DockSocket) return;
            // Docking restrains the complete rigid construction, not a long chain
            // hanging from one tiny joint. Balls remain dynamic for hand loading.
            foreach(var p in CoreParts) foreach(var b in p.Bodies) b.Rigidbody.isKinematic=true;
            padDocked=true;
        }
        public void ArenaMoved()
        {
            if(State==ChallengeState.LaunchReady && DockSocket) AttachToPad();
            nextDockPreview=0;
        }
        void RemovePadRestraint()
        {
            if(!padDocked) return;
            foreach(var p in CoreParts) foreach(var b in p.Bodies) b.Rigidbody.isKinematic=b.Anchored;
            padDocked=false;
        }
        public bool SocketExposed(AssemblySocket socket, AssemblyPart ignored = null)
        {
            if(!socket) return false;
            var origin = socket.WorldPosition + socket.Normal * .001f;
            int overlaps=Physics.OverlapSphereNonAlloc(origin,.0005f,surfaceOverlaps,~0,QueryTriggerInteraction.Ignore);
            if(overlaps==surfaceOverlaps.Length) return false;
            for(int i=0;i<overlaps;i++)
            {
                var c=surfaceOverlaps[i];
                var body=c.GetComponentInParent<AssemblyBody>();
                if(body && body.Part!=socket.Body.Part && body.Part!=ignored && body.Part.Kind!=PartKind.PhysicsSphere) return false;
            }
            int hits=Physics.RaycastNonAlloc(origin,socket.Normal,surfaceHits,.025f,~0,QueryTriggerInteraction.Ignore);
            if(hits==surfaceHits.Length) return false;
            for(int i=0;i<hits;i++)
            {
                var hit=surfaceHits[i];
                var body = hit.collider.GetComponentInParent<AssemblyBody>();
                if (body && body.Part != socket.Body.Part && body.Part != ignored && body.Part.Kind != PartKind.PhysicsSphere) return false;
            }
            return true;
        }
        public bool CanAttach(AssemblyPart source, AssemblyPart target, AssemblySocket targetSocket)
            => IsBuilding && source != Core && !IsCoreConnected(source) && IsCoreConnected(target) && SocketExposed(targetSocket,source);
        public bool CanDetach(AssemblyPart part)
        {
            if (!IsBuilding || !part || part == Core || !IsCoreConnected(part) || !part.Sockets.Any(s=>SocketExposed(s))) return false;
            var seen = new HashSet<string> { Core.InstanceId }; var queue = new Queue<string>(); queue.Enqueue(Core.InstanceId);
            while (queue.Count > 0)
            {
                string id = queue.Dequeue();
                foreach (var c in Workshop.World.Connections)
                {
                    string next = c.a.partId == id ? c.b.partId : c.b.partId == id ? c.a.partId : null;
                    if (next != null && next != part.InstanceId && seen.Add(next)) queue.Enqueue(next);
                }
            }
            return coreParts.All(p => p == part || seen.Contains(p.InstanceId));
        }
        public bool PrepareGrab(int hand, AssemblyPart part, Pose pose)
        {
            if (IsAiming || LaunchGesture.BlocksInput) return false;
            if (State == ChallengeState.LaunchReady) return part.Kind == PartKind.PhysicsSphere;
            if (!IsBuilding || part == Core || part.Kind == PartKind.PhysicsSphere) return false;
            var nearest = part.Bodies.SelectMany(b => b.Colliders).OrderBy(c => (c.ClosestPoint(pose.position) - pose.position).sqrMagnitude).FirstOrDefault();
            if (!nearest) return false;
            var point = nearest.ClosestPoint(pose.position); var delta = point - pose.position;
            foreach (var hit in Physics.RaycastAll(pose.position, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                var other = hit.collider.GetComponentInParent<AssemblyBody>();
                if (other && other.Part != part) return false;
            }
            if (IsCoreConnected(part)) return hand == 0 && CanDetach(part) && Workshop.World.TryDetachPart(part);
            return true;
        }
        public void UpdateDockCandidate()
        {
            DockSocket = null; DockValid = false; DockProblem = "No exposed downward connection beneath the core. Add a flat bottom part.";
            if (!Core) return;
            var core = Workshop.Arena.InverseTransformPoint(Core.MainBody.Rigidbody.position);
            var candidates = CoreParts.SelectMany(p => p.Sockets).Where(s => !Workshop.World.SocketOccupied(s) && Vector3.Dot(s.Normal, -Up) >= .9848f && SocketExposed(s))
                .Where(s => { var p = Workshop.Arena.InverseTransformPoint(s.WorldPosition); return Mathf.Abs(p.x - core.x) <= BuildingGeometry.Unit * .5f + .001f && Mathf.Abs(p.z - core.z) <= BuildingGeometry.Unit * .5f + .001f; })
                .OrderBy(s => Vector3.Dot(s.WorldPosition, Up)).ThenBy(s => Vector3.ProjectOnPlane(s.WorldPosition - Core.MainBody.Rigidbody.position, Up).sqrMagnitude)
                .ThenBy(s => s.Body.Part.InstanceId).ThenBy(s => s.Id);
            DockSocket = candidates.FirstOrDefault(); if (!DockSocket) return;
            DockValid = DockPlacementClear(PadPoint - DockSocket.WorldPosition);
            DockProblem = DockValid ? "" : "Docking is blocked. Remove parts below or beside the highlighted connection.";
        }
        bool DockPlacementClear(Vector3 delta)
        {
            foreach (var part in CoreParts) foreach (var body in part.Bodies) foreach (var c in body.Colliders)
            {
                foreach (var other in Physics.OverlapBox(c.bounds.center + delta, c.bounds.extents + Vector3.one * .001f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                {
                    var b = other.GetComponentInParent<AssemblyBody>();
                    if (b && (coreSet.Contains(b.Part) || b.Part.Kind == PartKind.PhysicsSphere)) continue;
                    if (!b && !other.GetComponentInParent<WorkshopEnvironment>()) continue;
                    if (Physics.ComputePenetration(c, c.transform.position + delta, c.transform.rotation, other, other.transform.position, other.transform.rotation, out _, out float depth) && depth > .002f) return false;
                }
            }
            return true;
        }
        void Update()
        {
            if (resetAt >= 0 && Time.unscaledTime >= resetAt) { if (ResetLevel()) resetAt = -1; }
            if (!dockLaser) return;
            dockLaser.enabled = IsBuilding;
            if (!IsBuilding || Time.unscaledTime < nextDockPreview) return;
            nextDockPreview = Time.unscaledTime + .08f; UpdateDockCandidate();
            dockLaser.SetPosition(0, PadPoint); dockLaser.SetPosition(1, DockSocket ? DockSocket.WorldPosition : Core.MainBody.Rigidbody.position);
            dockLaser.startColor = dockLaser.endColor = DockValid ? Color.green : Color.red;
            dockLaser.sharedMaterial.color=DockValid?Color.green:Color.red;
        }
        void ResetBuilding()
        {
            RemovePadRestraint(); SetConstructionPhysics(false); Core = null; coreTopology = -1; BuildCyclesUsed = 0;
            resetAt = -1; ModeGesture.Reset(true); GestureFeedback?.Clear(); DockSocket = null;
        }
        void OnDestroy()
        {
            if(Workshop && Workshop.World) { Workshop.World.TopologyChanged-=RefreshCoreGraph; Workshop.World.Collisions.Release("challenge-core"); }
        }
        void OnDisable() { disabledAt=Time.unscaledTime; ModeGesture.Reset(true); CancelAim(); }
        void OnEnable() { if(disabledAt>=0 && resetAt>=0) resetAt+=Time.unscaledTime-disabledAt; disabledAt=-1; }
    }
}

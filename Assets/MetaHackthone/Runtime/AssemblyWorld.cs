using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PhysicalAssembly
{
    public sealed class AssemblyWorld : MonoBehaviour
    {
        public float AttachDistance = .10f, SocketDistance = .04f, AlignmentTime = .25f;
        public bool EditingEnabled { get; set; }=true;
        public bool FloatingBuild, ConstrainBuildMotion, QuarterTurnBlocks;
        public SandboxDragBuilder DragBuilder;
        public BuildSpringSettings BuildSpring=new();
        public bool IsBuildBlocker(Collider collider) => !(Rules is IAssemblyCollisionRules policy) || policy.IsBuildBlocker(collider);
        public NarrowGapController Challenge { get; set; }
        public IAssemblyInteractionRules Rules { get; set; }
        public bool ExternalHold { get; set; }
        public readonly Dictionary<string, AssemblyPart> Parts = new();
        public readonly List<ConnectionRecord> Connections = new();
        public readonly CollisionPairRegistry Collisions = new();
        readonly Dictionary<string, ConfigurableJoint> joints = new();
        readonly Dictionary<int, Hold> holds = new();
        readonly HashSet<string> reserved = new();
        readonly HashSet<Action> alignmentCleanup = new();
        readonly HashSet<AssemblySocket> occupiedSockets=new();
        readonly Dictionary<string,(AssemblySocket a,AssemblySocket b)> connectionSockets=new();
        AttachmentSearch search;
        AttachmentSearch Search => search??=new AttachmentSearch(this);
        AssemblyAttachmentGraph graph;
        public AssemblyAttachmentGraph AttachmentGraph => graph??=new AssemblyAttachmentGraph(this);
        public int TopologyVersion { get; private set; }
        public int HoldVersion { get; private set; }
        public int LastSocketPairChecks => Search.PairChecks;
        public int LastPenetrationChecks => Search.PenetrationChecks;
        public long QueryCount => Search.QueryCount;
        void NotifyTopologyChanged() { TopologyVersion++; TopologyChanged?.Invoke(); }
        internal bool IsReserved(AssemblyPart part) => reserved.Contains(part.InstanceId);
        public event Action TopologyChanged;
        public event Action HoldsChanged;
        public event Action<string> Feedback;
        public bool IsBusy => reserved.Count > 0;
        public bool HasHolds => holds.Count > 0 || ExternalHold;
        public bool IsSuspended(AssemblyPart p) => reserved.Contains(p.InstanceId) || holds.Values.Any(h => h.parts.Contains(p));
        sealed class Snapshot
        {
            public AssemblyBody body; public Vector3 position, velocity, angular; public Quaternion rotation; public bool kinematic;
            public RigidbodyInterpolation interpolation;
            public Snapshot(AssemblyBody b) { body=b; var r=b.Rigidbody; position=r.position; rotation=r.rotation; velocity=r.linearVelocity; angular=r.angularVelocity; kinematic=r.isKinematic; interpolation=r.interpolation; }
            public void Restore(bool pose, bool velocity)
            {
                if (!body) return; var r=body.Rigidbody;
                if (pose) { r.position=position; r.rotation=rotation; }
                bool floating=body.Part && body.Part.World && body.Part.World.FloatingBuild;
                // Floating construction is pose editing, including detached assemblies.
                
                if(floating && !r.isKinematic) { r.linearVelocity=Vector3.zero; r.angularVelocity=Vector3.zero; }
                r.isKinematic=floating || kinematic || body.Anchored;
                if(floating) { r.useGravity=false; r.linearDamping=r.angularDamping=1; }
                r.interpolation=interpolation;
                if (!r.isKinematic) { r.linearVelocity=velocity?this.velocity:Vector3.zero; r.angularVelocity=velocity?angular:Vector3.zero; r.WakeUp(); }
            }
        }
        sealed class Hold
        {
            public AssemblyPart selected; public List<AssemblyPart> parts; public Snapshot[] snapshots;
            public Pose origin, desired;
            public BuildHoldSpring spring;
            public BuildMotionConstraint.Group motion;
            public void ResetMotion(Pose pose)
            {
                spring=new BuildHoldSpring(pose);
                motion=new BuildMotionConstraint.Group(snapshots.Select(s=>s.body).ToArray(),pose);
            }
        }
        public void RegisterPart(AssemblyPart p)
        {
            if (Parts.ContainsKey(p.InstanceId)) throw new InvalidOperationException("Duplicate part identity");
            if (p.Bodies.Select(b=>b.Id).Distinct().Count()!=p.Bodies.Length) throw new InvalidOperationException("Duplicate body identity");
            if(p.Sockets.Any(s=>string.IsNullOrEmpty(s.Id)||!s.Body||s.Body.Part!=p) || p.Sockets.Select(s=>s.Id).Distinct().Count()!=p.Sockets.Length) throw new InvalidOperationException("Invalid or duplicate socket identity");
            Parts.Add(p.InstanceId,p); Search.Register(p); NotifyTopologyChanged();
        }
        public void UnregisterPart(AssemblyPart p)
        {
            foreach (var key in holds.Where(kv=>kv.Value.parts.Contains(p)).Select(kv=>kv.Key).ToArray()) Release(key,false);
            foreach (var c in Connections.Where(c=>c.a.partId==p.InstanceId || c.b.partId==p.InstanceId).ToArray()) RemoveConnection(c.id);
            Search.Unregister(p); Parts.Remove(p.InstanceId); reserved.Remove(p.InstanceId); Collisions.Release("internal:"+p.InstanceId); NotifyTopologyChanged();
        }
        public List<AssemblyPart> GetConnectedParts(string id)
        {
            var seen=new HashSet<string>(); var queue=new Queue<string>(); var result=new List<AssemblyPart>(); queue.Enqueue(id);
            while(queue.Count>0)
            {
                var next=queue.Dequeue(); if(!seen.Add(next) || !Parts.TryGetValue(next,out var part) || !part) continue;
                result.Add(part);
                foreach(var c in Connections) { if(c.a.partId==next) queue.Enqueue(c.b.partId); if(c.b.partId==next) queue.Enqueue(c.a.partId); }
            }
            return result;
        }
        internal void FillConnectedParts(AssemblyPart start,List<AssemblyPart> result,HashSet<AssemblyPart> seen)
        {
            if(!start) return; result.Add(start); seen.Add(start);
            for(int i=0;i<result.Count;i++) foreach(var edge in Connections)
            {
                string id=edge.a.partId==result[i].InstanceId?edge.b.partId:edge.b.partId==result[i].InstanceId?edge.a.partId:null;
                if(id!=null && Parts.TryGetValue(id,out var part) && part && seen.Add(part)) result.Add(part);
            }
        }
        internal bool IsAssemblyPart(AssemblyPart part) => part && !part.GetComponentInParent<WorkshopEnvironment>()
            && Parts.TryGetValue(part.InstanceId,out var registered) && registered==part;
        public IReadOnlyList<AssemblyPart> HeldParts(int hand) => holds.TryGetValue(hand,out var h)?h.parts:Array.Empty<AssemblyPart>();
        public bool CanAttachFromHand(int hand) => Rules!=null?Rules.CanAttachFromHand(hand):!Challenge || hand!=0;

        // Excluding the selected vertex preserves every alternate path to the core.
        public List<AssemblyPart> GetDependentBranch(AssemblyPart selected,AssemblyPart core)
        {
            if(!IsAssemblyPart(selected) || !IsAssemblyPart(core) || selected==core) return new List<AssemblyPart>();
            var component=GetConnectedParts(selected.InstanceId);
            if(!component.Contains(core)) {
                var root=AttachmentGraph.Root(selected);
                if(!root || root==selected) return component;
                core=root;
            }
            var reachable=new HashSet<string>{core.InstanceId}; var queue=new Queue<string>(); queue.Enqueue(core.InstanceId);
            while(queue.Count>0)
            {
                var id=queue.Dequeue();
                foreach(var edge in Connections)
                {
                    var next=edge.a.partId==id?edge.b.partId:edge.b.partId==id?edge.a.partId:null;
                    if(next!=null && next!=selected.InstanceId && reachable.Add(next)) queue.Enqueue(next);
                }
            }
            return component.Where(p=>!reachable.Contains(p.InstanceId)).ToList();
        }
        bool CanDetachBranch(AssemblyPart selected,AssemblyPart core,List<AssemblyPart> group)
            => EditingEnabled && !IsBusy && IsAssemblyPart(selected) && IsAssemblyPart(core) && selected!=core && group.Count>0
                && (Rules==null || Rules.CanDetach(selected)) && (!Challenge || Challenge.CanDetach(selected))
                && !group.Any(IsSuspended);
        void DetachBoundary(List<AssemblyPart> group)
        {
            var ids=new HashSet<string>(group.Select(p=>p.InstanceId));
            var boundary=Connections.Where(c=>ids.Contains(c.a.partId)!=ids.Contains(c.b.partId)).ToArray();
            foreach(var edge in boundary) RemoveConnection(edge.id,false);
            if(boundary.Length>0) NotifyTopologyChanged();
        }
        public bool TryDetachBranch(AssemblyPart selected,AssemblyPart core)
        {
            var group=GetDependentBranch(selected,core);
            if(!CanDetachBranch(selected,core,group)) return false;
            DetachBoundary(group); return true;
        }
        bool IsLooseDesignerGroup(List<AssemblyPart> group)
            => DragBuilder && Rules?.BranchRoot && !group.Contains(Rules.BranchRoot);
        void SplitLoosePart(int owner,int receivingHand,Hold held,AssemblyPart member,Pose pose)
        {
            var old=held.snapshots.ToDictionary(s=>s.body);
            var previousParts=held.parts.ToArray();
            // Remove only the picked part's external joints. Internal multi-body joints remain intact.
            foreach(var edge in Connections.Where(e=>e.a.partId==member.InstanceId || e.b.partId==member.InstanceId).ToArray())
                RemoveConnection(edge.id,false);
            var retained=GetConnectedParts(held.selected.InstanceId);
            var released=previousParts.Where(p=>p!=member && !retained.Contains(p)).ToArray();
            Snapshot[] Rebase(List<AssemblyPart> parts) {
                var snapshots=Capture(parts);
                foreach(var snap in snapshots) { snap.kinematic=old[snap.body].kinematic; snap.interpolation=old[snap.body].interpolation; }
                return snapshots;
            }
            held.parts=retained; held.snapshots=Rebase(retained); held.origin=held.desired;
            if(ConstrainBuildMotion) held.ResetMotion(held.desired);
            var taken=new List<AssemblyPart>{member};
            var split=new Hold {selected=member,parts=taken,snapshots=Rebase(taken),origin=pose,desired=pose};
            if(ConstrainBuildMotion) split.ResetMotion(pose);
            holds.Add(receivingHand,split);
            foreach(var part in released) foreach(var body in part.Bodies) old[body].Restore(false,false);
            DragBuilder.ClearAim(owner); DragBuilder.ClearAim(receivingHand);
            // Observers must see the final topology and disjoint ownership together.
            HoldVersion++; NotifyTopologyChanged(); HoldsChanged?.Invoke();
        }
        public bool TryTransferHold(int receivingHand,AssemblyPart member,Pose pose)
        {
            if(!EditingEnabled || !IsAssemblyPart(member) || holds.ContainsKey(receivingHand) || Rules==null) return false;
            var owner=holds.FirstOrDefault(kv=>kv.Value.parts.Contains(member));
            if(owner.Value==null || !Rules.CanTransferHold(owner.Key,receivingHand,member) || !Rules.PrepareGrab(receivingHand,member,pose)
                || owner.Value.parts.Any(IsReserved)) return false;
            var h=owner.Value;
            bool loose=IsLooseDesignerGroup(GetConnectedParts(member.InstanceId));
            if(loose && (IsBusy || member!=h.selected && !Rules.CanDetach(member))) return false;
            if(!ConstrainBuildMotion) ApplyHold(h);
            if(loose && member!=h.selected) { SplitLoosePart(owner.Key,receivingHand,h,member,pose); return true; }
            if(!loose && Rules.BranchRoot && member!=AttachmentGraph.Root(member)) {
                var branch=GetDependentBranch(member,Rules.BranchRoot);
                if(branch.Count==0 || branch.Count==h.parts.Count || branch.Any(p=>!h.parts.Contains(p)) || IsBusy || !Rules.CanDetach(member)) return false;
                var old=h.snapshots.ToDictionary(s=>s.body);
                DetachBoundary(branch);
                h.parts=h.parts.Except(branch).ToList();
                h.snapshots=Capture(h.parts);
                foreach(var s in h.snapshots) {s.kinematic=old[s.body].kinematic;s.interpolation=old[s.body].interpolation;}
                h.origin=h.desired; if(ConstrainBuildMotion) h.ResetMotion(h.desired);
                var split=new Hold {selected=member,parts=branch,snapshots=Capture(branch),origin=pose,desired=pose};
                foreach(var s in split.snapshots) {s.kinematic=old[s.body].kinematic;s.interpolation=old[s.body].interpolation;}
                if(ConstrainBuildMotion) split.ResetMotion(pose);
                holds.Add(receivingHand,split); HoldVersion++; HoldsChanged?.Invoke(); return true;
            }
            var rebased=Capture(h.parts);
            for(int i=0;i<rebased.Length;i++)
            { rebased[i].kinematic=h.snapshots[i].kinematic; rebased[i].interpolation=h.snapshots[i].interpolation; }
            h.snapshots=rebased; h.selected=member; h.origin=h.desired=pose;
            if(ConstrainBuildMotion) h.ResetMotion(pose);
            holds.Remove(owner.Key); holds.Add(receivingHand,h);
            if(loose) { DragBuilder.ClearAim(owner.Key); DragBuilder.ClearAim(receivingHand); }
            HoldVersion++; HoldsChanged?.Invoke(); return true;
        }
        public bool TryGrab(int hand, AssemblyPart part, Pose pose)
        {
            if(!EditingEnabled || !IsAssemblyPart(part) || holds.ContainsKey(hand)) return false;
            if(holds.Values.Any(h=>h.parts.Contains(part))) return TryTransferHold(hand,part,pose);
            if(IsReserved(part)) return false;
            if(Challenge && !Challenge.PrepareGrab(hand,part,pose)) return false;
            if(Rules!=null && !Rules.PrepareGrab(hand,part,pose)) return false;
            var core=Rules?.BranchRoot;
            var component=GetConnectedParts(part.InstanceId);
            bool loose=IsLooseDesignerGroup(component);
            if(loose && IsBusy) return false;
            var group=core && !loose?GetDependentBranch(part,core):component;
            if(core && !loose)
            {
                if(!CanDetachBranch(part,core,group)) return false;
                // All rejection paths precede the first connection removal.
                DetachBoundary(group);
            }
            else if(group.Any(p=>IsSuspended(p) || p.Bodies.Any(b=>b.Anchored))) return false;
            var snapshots=Capture(group);
            foreach(var s in snapshots) { s.body.Rigidbody.isKinematic=true; s.body.Rigidbody.interpolation=RigidbodyInterpolation.None; }
            var hold=new Hold { selected=part,parts=group,snapshots=snapshots,origin=pose,desired=pose };
            if(ConstrainBuildMotion) hold.ResetMotion(pose);
            holds.Add(hand,hold);
            HoldVersion++; HoldsChanged?.Invoke(); return true;
        }
        public void FreezeHold(int hand) {
            if(!holds.TryGetValue(hand,out var h)) return;
            var pose=h.spring!=null?h.spring.Pose:h.desired;
            RebaseHold(hand,pose);
        }
        public void RebaseHold(int hand,Pose pose) {
            if(!holds.TryGetValue(hand,out var h)) return;
            var previous=h.snapshots.ToDictionary(s=>s.body);
            h.snapshots=Capture(h.parts);
            foreach(var s in h.snapshots) {s.kinematic=previous[s.body].kinematic;s.interpolation=previous[s.body].interpolation;}
            h.origin=h.desired=pose;h.ResetMotion(pose);
        }
        public AssemblyPart HeldPart(int hand) => holds.TryGetValue(hand,out var h)?h.selected:null;
        public void SetHoldPose(int hand,Pose pose) { if(holds.TryGetValue(hand,out var h)) { h.desired=pose; if(!ConstrainBuildMotion) ApplyHold(h); } }
        public void Release(int hand,bool attach)
        {
            if(!holds.TryGetValue(hand,out var h)) return;
            if(!ConstrainBuildMotion) ApplyHold(h);
            var candidate=attach && CanAttachFromHand(hand)?Query(h.selected):default;
            bool pointed=DragBuilder && DragBuilder.HasAim(h.selected,out _);
            holds.Remove(hand); HoldVersion++; HoldsChanged?.Invoke();
            // Alignment owns reserved bodies until its finally block restores them.
            foreach(var s in h.snapshots) if(s.body && !reserved.Contains(s.body.Part.InstanceId)) s.Restore(false,false);
            if(candidate.IsValid) TryBeginAssembly(candidate,pointed);
            else if(attach && BlockedCandidate(h.selected).IsValid) Feedback?.Invoke("Placement intersects another part.");
            DragBuilder?.ClearAim(hand);
        }
        public bool RotateBranch(AssemblyPart selected,Vector3 up,float degrees)
        {
            if(!EditingEnabled || IsBusy || !selected || selected.BuildingCore) return false;
            var owner=holds.FirstOrDefault(kv=>kv.Value.selected==selected);
            if(owner.Value==null && !TryDetachPart(selected)) return false;
            var parts=owner.Value!=null?owner.Value.parts:GetConnectedParts(selected.InstanceId);
            if(owner.Value==null && parts.Any(IsSuspended)) return false;
            var bodies=parts.SelectMany(p=>p.Bodies).ToArray();
            var pivot=selected.MainBody.Rigidbody.position; var delta=Quaternion.AngleAxis(degrees,up);
            var poses=bodies.Select(b=>new Pose(b.Rigidbody.position,b.Rigidbody.rotation)).ToArray();
            if(!BuildMotionConstraint.Move(bodies,poses.Select(p=>pivot+delta*(p.position-pivot)).ToArray(),poses.Select(p=>delta*p.rotation).ToArray(),IsBuildBlocker)) {
                for(int i=0;i<bodies.Length;i++) { bodies[i].Rigidbody.position=poses[i].position; bodies[i].Rigidbody.rotation=poses[i].rotation; bodies[i].transform.SetPositionAndRotation(poses[i].position,poses[i].rotation); }
                Physics.SyncTransforms(); return false;
            }
            if(owner.Value!=null) {
                var h=owner.Value; var old=h.snapshots.ToDictionary(s=>s.body); h.snapshots=Capture(parts);
                foreach(var s in h.snapshots) { s.kinematic=old[s.body].kinematic; s.interpolation=old[s.body].interpolation; }
                h.origin=h.desired; if(ConstrainBuildMotion) h.ResetMotion(h.desired);
            }
            return true;
        }
        public void ReleaseAll() { foreach(var id in holds.Keys.ToArray()) Release(id,false); }
        void FixedUpdate()
        {
            foreach(var h in holds.Values) ApplyHold(h);
            for(int i=Connections.Count-1;i>=0;i--) { var c=Connections[i]; if(!joints.TryGetValue(c.id,out var j) || !j) RemoveConnection(c.id); }
        }
        void ApplyHold(Hold h)
        {
            var delta=h.desired.rotation*Quaternion.Inverse(h.origin.rotation);
            if(ConstrainBuildMotion)
            {
                if(h.spring==null) h.ResetMotion(h.origin);
                h.spring.Tick(h.desired,Time.fixedDeltaTime,BuildSpring??=new BuildSpringSettings(),h.motion,IsBuildBlocker);
                return;
            }
            foreach(var s in h.snapshots) if(s.body)
            { var r=s.body.Rigidbody; r.position=h.desired.position+delta*(s.position-h.origin.position); r.rotation=delta*s.rotation; }
        }
        public AttachmentCandidate Query(AssemblyPart held)
        {
            foreach(var owner in holds) if(owner.Value.parts.Contains(held) && !CanAttachFromHand(owner.Key)) return default;
            return EditingEnabled?(DragBuilder?DragBuilder.Query(held):Search.Query(held)):default;
        }
        public AttachmentCandidate BlockedCandidate(AssemblyPart held) => DragBuilder?DragBuilder.Blocked(held):Search.BlockedCandidate(held);
        public bool SocketOccupied(AssemblySocket socket) => occupiedSockets.Contains(socket);
        static bool Compatible(AssemblySocket a,AssemblySocket b) => a && b && a.Body && b.Body &&
            a.Body.Attachable && b.Body.Attachable && a.Family==b.Family;
        public bool PlacementClear(AttachmentCandidate candidate,IEnumerable<AssemblyPart> movingParts) => Search.PlacementClear(candidate,movingParts);
        public bool TryBeginAssembly(AttachmentCandidate c) => TryBeginAssembly(c,false);
        bool TryBeginAssembly(AttachmentCandidate c,bool pointed)
        {
            if(!EditingEnabled || !c.IsValid || !IsAssemblyPart(c.source.Part) || !IsAssemblyPart(c.target.Part) || c.source.Part==c.target.Part) return false;
            if(Challenge && !Challenge.CanAttach(c.source.Part,c.target.Part,c.targetSocket)) return false;
            if(Rules!=null && !Rules.CanAttach(c.source.Part,c.target.Part,c.targetSocket)) return false;
            var a=GetConnectedParts(c.source.Part.InstanceId); var b=GetConnectedParts(c.target.Part.InstanceId);
            if(a.Intersect(b).Any() || a.Concat(b).Any(p=>reserved.Contains(p.InstanceId))) return false;
            if(!pointed && Vector3.Distance(c.sourcePoint,c.targetPoint)>AttachDistance+.001f) return false;
            if(!Compatible(c.sourceSocket,c.targetSocket) || c.sourceSocket.Body!=c.source || c.targetSocket.Body!=c.target ||
                SocketOccupied(c.sourceSocket)||SocketOccupied(c.targetSocket)) return false;
            if(!pointed && !c.sourceSocket.CanAcquire(c.targetSocket,SocketDistance+.001f)) return false;
            c=new AttachmentCandidate(c.source,c.target,c.sourceSocket.WorldPosition,c.targetSocket.WorldPosition,c.sourceSocket,c.targetSocket);
            if(!PlacementClear(c,a)) return false;
            // Releasing the source may attach it to a target still held by the other
            // hand. Never steal that hand's ownership or detach its construction.
            if(holds.Values.Any(h=>h.parts.Intersect(a).Any())) return false;
            if(a.Any(p=>p.Bodies.Any(body=>body.Anchored))) return false;
            foreach(var p in a.Concat(b)) reserved.Add(p.InstanceId);
            StartCoroutine(Align(c,a,b)); return true;
        }
        static Snapshot[] Capture(IEnumerable<AssemblyPart> parts) => parts.SelectMany(p=>p.Bodies).Select(b=>new Snapshot(b)).ToArray();
        IEnumerator Align(AttachmentCandidate c,List<AssemblyPart> a,List<AssemblyPart> b)
        {
            var moving=Capture(a); var stationary=Capture(b); bool committed=false;
            var targetHold=holds.Values.FirstOrDefault(h=>h.parts.Intersect(b).Any());
            if(targetHold!=null) foreach(var s in stationary)
            {
                var original=targetHold.snapshots.First(x=>x.body==s.body);
                s.kinematic=original.kinematic; s.interpolation=original.interpolation;
            }
            var targetLocalPoint=Quaternion.Inverse(c.target.Rigidbody.rotation)*(c.targetPoint-c.target.Rigidbody.position);
            var targetRotation=c.target.Rigidbody.rotation;
            foreach(var s in moving.Concat(stationary)) { s.body.Rigidbody.isKinematic=true; s.body.Rigidbody.interpolation=RigidbodyInterpolation.None; }
            var delta=c.rotationDelta;
            Action cleanup=null;
            cleanup=()=> {
                if(!alignmentCleanup.Remove(cleanup)) return;
                foreach(var s in moving.Concat(stationary))
                    if(s.body && !holds.Values.Any(h=>h.parts.Contains(s.body.Part))) s.Restore(!committed,!committed);
                foreach(var p in a.Concat(b)) if(p) reserved.Remove(p.InstanceId);
                if(!committed) Feedback?.Invoke("Attachment cancelled");
            };
            alignmentCleanup.Add(cleanup);
            try
            {
                float elapsed=0;
                while(!DragBuilder && elapsed<AlignmentTime)
                {
                    if(!c.IsValid || moving.Concat(stationary).Any(s=>!s.body)) yield break;
                    elapsed+=Time.fixedDeltaTime; float t=HustleBehavior.GlueCurve.Evaluate(Mathf.Clamp01(elapsed/AlignmentTime)*.25f);
                    if(ConstrainBuildMotion)
                    {
                        var targetDelta=c.target.Rigidbody.rotation*Quaternion.Inverse(targetRotation);
                        var targetPoint=c.target.Rigidbody.position+c.target.Rigidbody.rotation*targetLocalPoint;
                        if(!BuildMotionConstraint.Move(moving.Select(s=>s.body).ToArray(),
                            moving.Select(s=>Vector3.Lerp(s.position,targetPoint+targetDelta*delta*(s.position-c.sourcePoint),t)).ToArray(),
                            moving.Select(s=>Quaternion.Slerp(s.rotation,targetDelta*delta*s.rotation,t)).ToArray(),IsBuildBlocker))
                        { Feedback?.Invoke("Attachment path blocked."); yield break; }
                    }
                    else foreach(var s in moving)
                    {
                        var targetDelta=c.target.Rigidbody.rotation*Quaternion.Inverse(targetRotation);
                        var targetPoint=c.target.Rigidbody.position+c.target.Rigidbody.rotation*targetLocalPoint;
                        s.body.Rigidbody.position=Vector3.Lerp(s.position,targetPoint+targetDelta*delta*(s.position-c.sourcePoint),t);
                        s.body.Rigidbody.rotation=Quaternion.Slerp(s.rotation,targetDelta*delta*s.rotation,t);
                    }
                    yield return new WaitForFixedUpdate();
                }
                Physics.SyncTransforms();
                if(!c.IsValid) yield break;
                var finalDelta=c.target.Rigidbody.rotation*Quaternion.Inverse(targetRotation);
                var finalPoint=c.target.Rigidbody.position+c.target.Rigidbody.rotation*targetLocalPoint;
                if(!DragBuilder && ConstrainBuildMotion && !BuildMotionConstraint.Move(moving.Select(s=>s.body).ToArray(),
                    moving.Select(s=>finalPoint+finalDelta*delta*(s.position-c.sourcePoint)).ToArray(),
                    moving.Select(s=>finalDelta*delta*s.rotation).ToArray(),IsBuildBlocker)) yield break;
                if(DragBuilder && !PlacementClear(new AttachmentCandidate(c.source,c.target,c.sourceSocket.WorldPosition,c.targetSocket.WorldPosition,c.sourceSocket,c.targetSocket),a)) yield break;
                foreach(var s in moving)
                {
                    s.body.Rigidbody.position=finalPoint+finalDelta*delta*(s.position-c.sourcePoint);
                    s.body.Rigidbody.rotation=finalDelta*delta*s.rotation;
                }
                Physics.SyncTransforms();
                var record=new ConnectionRecord { id=Guid.NewGuid().ToString("N"),a=new BodyRef(c.source.Part.InstanceId,c.source.Id),b=new BodyRef(c.target.Part.InstanceId,c.target.Id),socketA=c.sourceSocket?c.sourceSocket.Id:"",socketB=c.targetSocket?c.targetSocket.Id:"",anchorA=Quaternion.Inverse(c.source.Rigidbody.rotation)*(finalPoint-c.source.Rigidbody.position),anchorB=targetLocalPoint };
                if(!PlacementClear(new AttachmentCandidate(c.source,c.target,finalPoint,finalPoint,c.sourceSocket,c.targetSocket),a)) { Feedback?.Invoke("Placement intersects another part."); yield break; }
                committed=ConnectInternal(record,true); if(committed) Feedback?.Invoke("Attached");
                if(committed && targetHold!=null && holds.Values.Contains(targetHold))
                {
                    var originals=targetHold.snapshots.Concat(moving).ToDictionary(s=>s.body);
                    targetHold.parts=GetConnectedParts(targetHold.selected.InstanceId);
                    targetHold.snapshots=Capture(targetHold.parts);
                    foreach(var s in targetHold.snapshots)
                    {
                        s.kinematic=originals[s.body].kinematic; s.interpolation=originals[s.body].interpolation;
                        s.body.Rigidbody.isKinematic=true; s.body.Rigidbody.interpolation=RigidbodyInterpolation.None;
                    }
                    targetHold.origin=targetHold.desired;
                    if(ConstrainBuildMotion) targetHold.ResetMotion(targetHold.desired);
                }
            }
            finally { cleanup(); }
        }

        // Select the nearest orientation among the target's 24 orthogonal frames; never round Euler angles.
        public static Quaternion NearestAxisRotation(Quaternion current,Quaternion target)
        {
            var axes=new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
            Quaternion best=current; float angle=float.MaxValue;
            foreach(var f in axes) foreach(var u in axes) if(Mathf.Abs(Vector3.Dot(f,u))<.01f)
            { var q=target*Quaternion.LookRotation(f,u); float d=Quaternion.Angle(current,q); if(d<angle) { angle=d; best=q; } }
            return best;
        }
        public AssemblyBody Resolve(BodyRef r) => Parts.TryGetValue(r.partId,out var p)?p.FindBody(r.bodyId):null;
        public bool Connect(ConnectionRecord c) => EditingEnabled && ConnectInternal(c,false);
        bool ConnectInternal(ConnectionRecord c,bool aligning)
        {
            if(c==null || string.IsNullOrEmpty(c.id)) return false;
            Physics.SyncTransforms();
            var a=Resolve(c.a); var b=Resolve(c.b);
            if(!a || !b || !IsAssemblyPart(a.Part) || !IsAssemblyPart(b.Part) || a.Part==b.Part || Connections.Any(x=>x.id==c.id)) return false;
            if(!aligning && (reserved.Contains(a.Part.InstanceId)||reserved.Contains(b.Part.InstanceId))) return false;
            var sa=a.Part.FindSocket(c.socketA); var sb=b.Part.FindSocket(c.socketB);
            if(!Compatible(sa,sb) || sa.Body!=a || sb.Body!=b || SocketOccupied(sa) || SocketOccupied(sb)) return false;
            if(Challenge && !Challenge.CanAttach(a.Part,b.Part,sb)) return false;
            if(Rules!=null && !Rules.CanAttach(a.Part,b.Part,sb)) return false;
            if((sa.transform.localPosition-c.anchorA).sqrMagnitude>1e-6f || (sb.transform.localPosition-c.anchorB).sqrMagnitude>1e-6f) return false;
            if(Connections.Any(x=>(x.a.partId==c.a.partId && x.a.bodyId==c.a.bodyId && x.b.partId==c.b.partId && x.b.bodyId==c.b.bodyId && (x.anchorA-c.anchorA).sqrMagnitude<.000001f && (x.anchorB-c.anchorB).sqrMagnitude<.000001f)||(x.a.partId==c.b.partId && x.a.bodyId==c.b.bodyId && x.b.partId==c.a.partId && x.b.bodyId==c.a.bodyId && (x.anchorA-c.anchorB).sqrMagnitude<.000001f && (x.anchorB-c.anchorA).sqrMagnitude<.000001f))) return false;
            var j=a.gameObject.AddComponent<ConfigurableJoint>(); j.connectedBody=b.Rigidbody; j.autoConfigureConnectedAnchor=false; j.anchor=c.anchorA; j.connectedAnchor=c.anchorB;
            j.xMotion=j.yMotion=j.zMotion=ConfigurableJointMotion.Locked;
            j.angularXMotion=j.angularYMotion=j.angularZMotion=ConfigurableJointMotion.Locked;
            j.breakForce=j.breakTorque=float.PositiveInfinity; j.enableCollision=false;
            if(Challenge)
            {
                a.Rigidbody.solverIterations=b.Rigidbody.solverIterations=32;
                a.Rigidbody.solverVelocityIterations=b.Rigidbody.solverVelocityIterations=12;
            }
            foreach(var ac in a.Colliders) foreach(var bc in b.Colliders) Collisions.Acquire(ac,bc,c.id);
            occupiedSockets.Add(sa); occupiedSockets.Add(sb); connectionSockets.Add(c.id,(sa,sb));
            AttachmentGraph.Commit(c); Connections.Add(c); joints.Add(c.id,j); Debug.Log($"METAHACKTHONE_ATTACHED {a.Part.Kind}/{a.Id} to {b.Part.Kind}/{b.Id}"); NotifyTopologyChanged(); return true;
        }
        public bool TryDetachPart(AssemblyPart p)
        {
            if(Rules?.BranchRoot) return TryDetachBranch(p,Rules.BranchRoot);
            if(!EditingEnabled || !p || IsBusy) return false;
            if(Challenge && !Challenge.CanDetach(p)) { Feedback?.Invoke("Remove an exposed outer part without disconnecting the rest from the core."); return false; }
            if(Rules!=null && !Rules.CanDetach(p)) { Feedback?.Invoke("Remove an exposed outer part without disconnecting the remaining structure."); return false; }
            foreach(var key in holds.Where(kv=>kv.Value.parts.Contains(p)).Select(kv=>kv.Key).ToArray()) Release(key,false);
            foreach(var c in Connections.Where(c=>c.a.partId==p.InstanceId || c.b.partId==p.InstanceId).ToArray()) RemoveConnection(c.id);
            Feedback?.Invoke("Part detached"); return true;
        }
        public void RemoveConnection(string id) => RemoveConnection(id,true);
        void RemoveConnection(string id,bool notify)
        {
            if(joints.Remove(id,out var joint) && joint)
            {
                joint.xMotion=joint.yMotion=joint.zMotion=ConfigurableJointMotion.Free;
                joint.angularXMotion=joint.angularYMotion=joint.angularZMotion=ConfigurableJointMotion.Free;
                joint.connectedBody=null; if(Application.isPlaying) Destroy(joint); else DestroyImmediate(joint);
            }
            if(connectionSockets.Remove(id,out var sockets)) { occupiedSockets.Remove(sockets.a); occupiedSockets.Remove(sockets.b); }
            Connections.RemoveAll(c=>c.id==id); Collisions.Release(id); if(notify) NotifyTopologyChanged();
        }
        void OnDisable() { ReleaseAll(); StopAllCoroutines(); foreach(var cleanup in alignmentCleanup.ToArray()) cleanup(); reserved.Clear(); }
    }
}

using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace PhysicalAssembly
{
    // All scratch storage belongs to one world. Rebuilt from physics poses per query,
    // never from interpolated render transforms or last frame's target positions.
    internal sealed class AttachmentSearch
    {
        static readonly ProfilerMarker QueryMarker=new("Assembly.Query");
        static readonly ProfilerMarker PlacementMarker=new("Assembly.Placement");
        readonly AssemblyWorld world;
        readonly Dictionary<AssemblyBody,BodyGeometry> geometry=new();
        readonly Dictionary<Vector3Int,int> cells=new(2048);
        readonly List<SocketPose> sources=new(1024),targets=new(4096);
        readonly List<Pair> pairs=new(1024);
        readonly List<AssemblyPart> moving=new(64);
        readonly HashSet<AssemblyPart> own=new();
        readonly List<ColliderPose> sourceColliders=new(128),targetColliders=new(256);
        readonly Dictionary<AssemblyPart,AttachmentCandidate> blocked=new();
        public AttachmentCandidate BlockedCandidate(AssemblyPart held) => held && blocked.TryGetValue(held,out var c)?c:default;
        public int PairChecks { get; private set; }
        public int PenetrationChecks { get; private set; }
        public long QueryCount { get; private set; }
        struct LocalSocket { public AssemblySocket socket; public Vector3 position; public Quaternion rotation; public int order; }
        struct LocalCollider
        {
            public Collider collider;
            public Vector3 position,center,extents;
            public Quaternion rotation;
        }
        sealed class BodyGeometry
        {
            public readonly List<LocalSocket> sockets=new();
            public readonly List<LocalCollider> colliders=new();
        }
        struct SocketPose
        {
            public AssemblySocket socket;
            public Vector3 position,bodyPosition;
            public Quaternion rotation,bodyRotation;
            public int next,order;
        }
        struct ColliderPose { public Collider collider; public Vector3 position,center,extents; public Quaternion rotation; }
        struct Pair { public SocketPose source,target; public float score; public int sourceOrder; }
        sealed class PairComparer : IComparer<Pair>
        {
            public static readonly PairComparer Instance=new();
            public int Compare(Pair a,Pair b)
            {
                int c=a.score.CompareTo(b.score); if(c!=0) return c;
                c=string.CompareOrdinal(a.target.socket.Body.Part.InstanceId,b.target.socket.Body.Part.InstanceId); if(c!=0) return c;
                c=a.sourceOrder.CompareTo(b.sourceOrder); return c!=0?c:a.target.order.CompareTo(b.target.order);
            }
        }
        public AttachmentSearch(AssemblyWorld world) { this.world=world; }
        public void Register(AssemblyPart part)
        {
            foreach(var body in part.Bodies)
            {
                var data=new BodyGeometry(); geometry[body]=data;
                for(int i=0;i<part.Sockets.Length;i++)
                {
                    var s=part.Sockets[i]; if(s.Body!=body) continue;
                    data.sockets.Add(new LocalSocket { socket=s,position=s.transform.localPosition,rotation=s.transform.localRotation,order=i });
                }
                foreach(var collider in body.Colliders)
                {
                    Vector3 center,extents;
                    if(collider is BoxCollider box) { center=box.center; extents=box.size*.5f; }
                    else if(collider is SphereCollider sphere)
                    {
                        var scale=collider.transform.lossyScale;
                        float radius=sphere.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
                        data.colliders.Add(new LocalCollider { collider=collider,position=body.transform.InverseTransformPoint(collider.transform.position),rotation=Quaternion.Inverse(body.transform.rotation)*collider.transform.rotation,center=Vector3.Scale(sphere.center,scale),extents=Vector3.one*radius });
                        continue;
                    }
                    else if(collider is MeshCollider mesh && mesh.sharedMesh) { center=mesh.sharedMesh.bounds.center; extents=mesh.sharedMesh.bounds.extents; }
                    else throw new InvalidOperationException("Unsupported building collider: "+collider.GetType().Name);
                    var size=collider.transform.lossyScale;
                    data.colliders.Add(new LocalCollider { collider=collider,position=body.transform.InverseTransformPoint(collider.transform.position),rotation=Quaternion.Inverse(body.transform.rotation)*collider.transform.rotation,center=Vector3.Scale(center,size),extents=Vector3.Scale(extents,new Vector3(Mathf.Abs(size.x),Mathf.Abs(size.y),Mathf.Abs(size.z))) });
                }
            }
        }
        public void Unregister(AssemblyPart part) { blocked.Clear(); foreach(var body in part.Bodies) geometry.Remove(body); }
        void Prepare(AssemblyPart held,bool sockets)
        {
            moving.Clear(); own.Clear(); world.FillConnectedParts(held,moving,own);
            sources.Clear(); targets.Clear(); cells.Clear(); pairs.Clear(); sourceColliders.Clear(); targetColliders.Clear();
            // Preserve connected-group source order for deterministic nearest-distance ties.
            foreach(var part in moving) Snapshot(part,true,sockets);
            foreach(var part in world.Parts.Values)
                if(!own.Contains(part) && world.IsAssemblyPart(part)) Snapshot(part,false,sockets);
        }
        void Snapshot(AssemblyPart part,bool source,bool sockets)
        {
            bool searchable=sockets && !world.IsReserved(part) && (!world.Challenge || world.Challenge.IsBuilding && (source || world.Challenge.IsCoreConnected(part)));
            foreach(var body in part.Bodies)
            {
                var position=body.Rigidbody.position; var rotation=body.Rigidbody.rotation; var data=geometry[body];
                if(searchable && body.Attachable) foreach(var s in data.sockets)
                {
                    if(!s.socket || world.SocketOccupied(s.socket)) continue;
                    if(!source && world.Rules!=null && !world.Rules.CanSearchTarget(part,s.socket,moving.Count>0?moving[0]:null)) continue;
                    if(!source && world.Challenge && !world.Challenge.SocketExposed(s.socket,moving.Count>0?moving[0]:null)) continue;
                    var pose=new SocketPose { socket=s.socket,position=position+rotation*s.position,rotation=rotation*s.rotation,bodyPosition=position,bodyRotation=rotation,order=s.order,next=-1 };
                    if(source)
                    {
                        sources.Add(pose);
                    }
                    else
                    {
                        var cell=Cell(pose.position); if(cells.TryGetValue(cell,out int head)) pose.next=head;
                        cells[cell]=targets.Count; targets.Add(pose);
                    }
                }
                if(!source && part.Kind==PartKind.PhysicsSphere) continue;
                foreach(var c in data.colliders)
                {
                    if(!c.collider || !c.collider.enabled || c.collider.isTrigger || !c.collider.gameObject.activeInHierarchy) continue;
                    if(!source && !world.IsBuildBlocker(c.collider)) continue;
                    var q=rotation*c.rotation; var p=position+rotation*c.position;
                    (source?sourceColliders:targetColliders).Add(new ColliderPose { collider=c.collider,position=p,rotation=q,center=c.center,extents=c.extents });
                }
            }
        }
        Vector3Int Cell(Vector3 p)
        {
            float inverse=1f/world.SocketDistance;
            return new Vector3Int(Mathf.FloorToInt(p.x*inverse),Mathf.FloorToInt(p.y*inverse),Mathf.FloorToInt(p.z*inverse));
        }
        public AttachmentCandidate Query(AssemblyPart held)
        {
            using(QueryMarker.Auto())
            {
                if(held) blocked.Remove(held);
                QueryCount++; PairChecks=PenetrationChecks=0;
                if(!world.IsAssemblyPart(held) || world.IsReserved(held) || world.SocketDistance<=0) return default;
                Prepare(held,true); float radius2=world.SocketDistance*world.SocketDistance;
                for(int i=0;i<sources.Count;i++)
                {
                    var a=sources[i]; var cell=Cell(a.position);
                    for(int x=-1;x<=1;x++) for(int y=-1;y<=1;y++) for(int z=-1;z<=1;z++)
                    {
                        if(!cells.TryGetValue(cell+new Vector3Int(x,y,z),out int index)) continue;
                        while(index>=0)
                        {
                            var b=targets[index]; index=b.next;
                            if(a.socket.Family!=b.socket.Family) continue;
                            PairChecks++; float distance=(a.position-b.position).sqrMagnitude;
                            if(distance<=radius2 && a.socket.ApproachAllowed(b.socket,a.position,a.rotation*Vector3.forward,b.position,b.rotation*Vector3.forward))
                            {
                                // Prefer the faces the player has lined up over a
                                // slightly closer socket that would turn the part sideways.
                                float facing=Mathf.Clamp(Vector3.Dot(a.rotation*Vector3.forward,b.rotation*Vector3.forward),-1,1);
                                pairs.Add(new Pair { source=a,target=b,score=distance+(1+facing)*radius2,sourceOrder=i });
                            }
                        }
                    }
                }
                pairs.Sort(PairComparer.Instance);
                foreach(var pair in pairs)
                {
                    var a=pair.source; var b=pair.target;
                    var candidate=new AttachmentCandidate(a.socket.Body,b.socket.Body,a.socket.WorldPosition,b.position,a.socket,b.socket,BuildingGeometry.SnapDelta(a.socket.WorldRotation,b.rotation,BuildingGeometry.AttachmentStep(a.socket,b.socket)),new Pose(a.bodyPosition,a.bodyRotation));
                    if(Clear(candidate)) { blocked.Remove(held); return candidate; }
                    if(!blocked.ContainsKey(held)) blocked[held]=candidate;
                }
                return default;
            }
        }
        public bool PlacementClear(AttachmentCandidate candidate,IEnumerable<AssemblyPart> movingParts)
        {
            if(!candidate.IsValid) return false;
            moving.Clear(); own.Clear();
            foreach(var part in movingParts) if(part && own.Add(part)) moving.Add(part);
            sourceColliders.Clear(); targetColliders.Clear();
            foreach(var part in moving) Snapshot(part,true,false);
            foreach(var part in world.Parts.Values) if(!own.Contains(part) && world.IsAssemblyPart(part)) Snapshot(part,false,false);
            return Clear(candidate);
        }
        static Vector3 Extents(Quaternion rotation,Vector3 extents)
        {
            var x=rotation*new Vector3(extents.x,0,0); var y=rotation*new Vector3(0,extents.y,0); var z=rotation*new Vector3(0,0,extents.z);
            return new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z));
        }
        bool Clear(AttachmentCandidate c)
        {
            using(PlacementMarker.Auto())
            {
                if(!c.sourceSocket || c.sourceSocket.Family!="grid") return true;
                foreach(var a in sourceColliders)
                {
                    var position=c.targetPoint+c.rotationDelta*(a.position-c.sourcePoint); var rotation=c.rotationDelta*a.rotation;
                    var center=position+rotation*a.center; var extents=Extents(rotation,a.extents);
                    foreach(var b in targetColliders)
                    {
                        var difference=center-(b.position+b.rotation*b.center); var combined=extents+Extents(b.rotation,b.extents);
                        if(Mathf.Abs(difference.x)>combined.x || Mathf.Abs(difference.y)>combined.y || Mathf.Abs(difference.z)>combined.z) continue;
                        PenetrationChecks++;
                        var ap=a.collider.GetComponentInParent<AssemblyPart>(); var bp=b.collider.GetComponentInParent<AssemblyPart>();
                        float tolerance=ap && bp && DownhillParts.IsWheel(ap.Kind) && DownhillParts.IsWheel(bp.Kind)? .00001f*Mathf.Min(ap.GeometryScale,bp.GeometryScale):.001f;
                        if(Physics.ComputePenetration(a.collider,position,rotation,b.collider,b.position,b.rotation,out _,out float depth) && depth>tolerance) return false;
                    }
                }
                return true;
            }
        }
    }
}

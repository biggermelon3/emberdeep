using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PhysicalAssembly
{
    // Sandbox-only intent selection. Physics/joints and save ancestry remain owned by AssemblyWorld.
    public sealed class SandboxDragBuilder : MonoBehaviour
    {
        public DownhillController Level { get; private set; }
        AssemblyWorld World => Level.Workshop.World;
        public bool Active => Level && Level.IsBuilding && World.EditingEnabled;
        public struct Aim
        {
            public Ray ray;
            public Camera camera;
            public Vector2 cursor;
            public float maxDistance, angle;
        }
        readonly Dictionary<int,Aim> aims=new();
        readonly Dictionary<AssemblyPart,AttachmentCandidate> blocked=new();
        readonly Dictionary<AssemblySocket,Renderer> markers=new();
        readonly Dictionary<string,LineRenderer> locks=new();
        Material markerMaterial,lockMaterial;
        Mesh markerMesh;
        string lastHint;
        MaterialPropertyBlock color;
        public void Initialize(DownhillController level) { Level=level; World.DragBuilder=this; color=new MaterialPropertyBlock(); }
        public void SetAim(int hand,Aim aim) { if(World.HeldPart(hand)) aims[hand]=aim; }
        public void ClearAim(int hand) => aims.Remove(hand);
        public bool HasAim(AssemblyPart selected,out Aim aim)
        {
            foreach(var entry in aims) if(World.HeldPart(entry.Key)==selected) { aim=entry.Value; return true; }
            aim=default; return false;
        }
        public AttachmentCandidate Blocked(AssemblyPart part) => part && blocked.TryGetValue(part,out var c)?c:default;
        public bool SocketAvailable(AssemblySocket socket)
        {
            if(!socket || World.SocketOccupied(socket) || DownhillParts.IsWheel(socket.Body.Part.Kind)) return false;
            var surface=Level.Workshop.Layout.Surface;
            if(surface && surface.gameObject.activeInHierarchy &&
                Vector3.Dot(socket.Normal,Level.Course.WorkbenchFrame.up)<-.5f &&
                Vector3.Distance(surface.ClosestPoint(socket.WorldPosition),socket.WorldPosition)<.003f) return false;
            return true;
        }
        public bool Compatible(AssemblyPart source,AssemblyPart target)
            => source && target && source!=target && !source.BuildingCore && !DownhillParts.IsWheel(target.Kind);
        bool Visible(AssemblySocket socket,Aim aim,HashSet<AssemblyPart> moving)
        {
            Vector3 delta=socket.WorldPosition-aim.ray.origin;
            if(delta.magnitude>aim.maxDistance || Vector3.Dot(socket.Normal,-delta.normalized)<.08f) return false;
            // Ignore the carried branch; the target's own face terminates the ray.
            foreach(var hit in Physics.RaycastAll(aim.ray.origin,delta.normalized,Mathf.Max(0,delta.magnitude-.002f),~0,QueryTriggerInteraction.Ignore)) {
                var part=hit.collider.GetComponentInParent<AssemblyPart>();
                if(part && (moving.Contains(part) || part==socket.Body.Part)) continue;
                return false;
            }
            return true;
        }
        float AimDistance(AssemblySocket socket,Aim aim)
        {
            if(aim.camera) {
                var point=aim.camera.WorldToScreenPoint(socket.WorldPosition);
                return point.z<=0?float.PositiveInfinity:Vector2.Distance(new Vector2(point.x,point.y),aim.cursor)/72f;
            }
            return Vector3.Angle(aim.ray.direction,socket.WorldPosition-aim.ray.origin)/Mathf.Max(.1f,aim.angle);
        }
        public AttachmentCandidate Query(AssemblyPart selected)
        {
            if(selected) blocked.Remove(selected);
            if(!Active || !selected || World.IsReserved(selected) || !World.IsAssemblyPart(selected)) return default;
            var moving=World.GetConnectedParts(selected.InstanceId); var own=new HashSet<AssemblyPart>(moving);
            bool pointed=HasAim(selected,out var aim);
            AssemblySocket bestSource=null,bestTarget=null; float bestTargetScore=float.PositiveInfinity,bestSourceScore=float.PositiveInfinity;
            foreach(var targetPart in World.Parts.Values) {
                if(!World.IsAssemblyPart(targetPart) || own.Contains(targetPart) || World.IsSuspended(targetPart) || !Compatible(selected,targetPart)) continue;
                foreach(var target in targetPart.Sockets) {
                    if(!SocketAvailable(target) || !Level.CanSearchTarget(targetPart,target,selected)) continue;
                    float targetScore=pointed?AimDistance(target,aim):float.PositiveInfinity;
                    if(pointed && (targetScore>1 || !Visible(target,aim,own))) continue;
                    AssemblySocket sourceChoice=null; float sourceScore=float.PositiveInfinity,nearest=float.PositiveInfinity;
                    foreach(var source in selected.Sockets) {
                        if(World.SocketOccupied(source) || source.Family!=target.Family) continue;
                        float distance=Vector3.Distance(source.WorldPosition,target.WorldPosition);
                        if(!pointed && distance>World.SocketDistance) continue;
                        float score=distance+(1+Vector3.Dot(source.Normal,target.Normal))*World.SocketDistance;
                        if(score<sourceScore) { sourceScore=score; sourceChoice=source; nearest=distance; }
                    }
                    if(!sourceChoice) continue;
                    if(!pointed) targetScore=nearest;
                    if(targetScore<bestTargetScore || Mathf.Approximately(targetScore,bestTargetScore) && sourceScore<bestSourceScore) {
                        bestTargetScore=targetScore; bestSourceScore=sourceScore; bestSource=sourceChoice; bestTarget=target;
                    }
                }
            }
            if(!bestSource) return default;
            var result=new AttachmentCandidate(bestSource.Body,bestTarget.Body,bestSource.WorldPosition,bestTarget.WorldPosition,bestSource,bestTarget);
            if(World.PlacementClear(result,moving)) return result;
            blocked[selected]=result; return default;
        }
        public AssemblyPart RayPart(Ray ray,float range,out Vector3 point)
        {
            point=default;
            foreach(var hit in Physics.RaycastAll(ray,range,~0,QueryTriggerInteraction.Collide).OrderBy(h=>h.distance)) {
                if(hit.collider.GetComponent<WorkshopButton>()) return null;
                if(hit.collider.isTrigger) continue;
                var body=hit.collider.GetComponentInParent<AssemblyBody>();
                if(body && World.IsAssemblyPart(body.Part) && !body.Part.BuildingCore) { point=hit.point; return body.Part; }
                return null;
            }
            return null;
        }
        public bool RotateSelected(int direction)
        {
            var selected=Level.Workshop.Session.Selected;
            if(!DesignModeSession.Enabled || !Active || !selected || selected.BuildingCore || World.IsBusy || Level.Rotating) return false;
            bool success=World.RotateBranch(selected,Level.Course.WorkbenchFrame.up,direction*90f);
            if(success) Level.Workshop.Session.Notify("Rotated 90 degrees.");
            return success;
        }
        void LateUpdate()
        {
            if(!Level) return;
            if(!markerMaterial) { markerMaterial=PartFactory.Material(new Color(.4f,.9f,.85f)); markerMaterial.enableInstancing=true; }
            if(!markerMesh) {
                markerMesh=new Mesh {name="Socket octahedron"};
                markerMesh.vertices=new[]{Vector3.up*.5f,Vector3.down*.5f,Vector3.left*.5f,Vector3.right*.5f,Vector3.forward*.5f,Vector3.back*.5f};
                markerMesh.triangles=new[]{0,4,3,0,3,5,0,5,2,0,2,4,1,3,4,1,5,3,1,2,5,1,4,2}; markerMesh.RecalculateNormals(); markerMesh.RecalculateBounds();
            }
            if(!lockMaterial) lockMaterial=PartFactory.Material(new Color(.25f,1,.5f));
            AssemblySocket highlighted=null; bool invalid=false;
            for(int hand=0;hand<3;hand++) {
                var held=World.HeldPart(hand); if(!held) { aims.Remove(hand); continue; }
                var candidate=Query(held); if(!candidate.IsValid) { candidate=Blocked(held); invalid=candidate.IsValid; }
                if(candidate.IsValid) highlighted=candidate.targetSocket;
            }
            if(Active && World.HasHolds) {
                string hint=highlighted?(invalid?"Blocked by another part. Choose another socket.":"Socket clear. Release to connect."):"Move near a socket, or point at one to preview the connection.";
                if(hint!=lastHint) { lastHint=hint; Level.Workshop.Session.Notify(hint); }
            } else lastHint=null;
            foreach(var part in World.Parts.Values) if(World.IsAssemblyPart(part)) foreach(var socket in part.Sockets) {
                if(DownhillParts.IsWheel(part.Kind)) continue;
                if(!markers.TryGetValue(socket,out var marker)) {
                    var go=new GameObject("Available socket"); go.transform.SetParent(transform,false);
                    go.AddComponent<MeshFilter>().sharedMesh=markerMesh;
                    marker=go.AddComponent<MeshRenderer>(); marker.sharedMaterial=markerMaterial;
                    marker.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; marker.receiveShadows=false; markers.Add(socket,marker);
                }
                marker.gameObject.SetActive(Active && SocketAvailable(socket));
                marker.transform.position=socket.WorldPosition+socket.Normal*.001f;
                marker.transform.localScale=Vector3.one*Mathf.Max(.004f,.012f*part.GeometryScale);
                color.SetColor("_BaseColor",socket==highlighted?(invalid?Color.red:Color.green):part.BuildingCore?Color.cyan:new Color(.4f,.9f,.85f)); marker.SetPropertyBlock(color);
            }
            foreach(var key in markers.Keys.Where(s=>!s).ToArray()) { if(markers[key]) Destroy(markers[key].gameObject); markers.Remove(key); }
            var ids=new HashSet<string>();
            foreach(var edge in World.Connections) {
                ids.Add(edge.id); var part=World.Resolve(edge.b)?.Part; var socket=part?part.FindSocket(edge.socketB):null; if(!socket) continue;
                if(!locks.TryGetValue(edge.id,out var line)) {
                    var go=new GameObject("Locked connection"); go.transform.SetParent(transform,false); line=go.AddComponent<LineRenderer>();
                    line.sharedMaterial=lockMaterial; line.loop=true; line.positionCount=32; line.useWorldSpace=true; locks.Add(edge.id,line);
                }
                line.gameObject.SetActive(Active); float radius=.022f*part.GeometryScale;
                line.startWidth=line.endWidth=Mathf.Max(.001f,.003f*part.GeometryScale);
                for(int i=0;i<32;i++) { float a=i*Mathf.PI/16; line.SetPosition(i,socket.WorldPosition+socket.WorldRotation*new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,.001f)); }
            }
            foreach(var key in locks.Keys.Where(id=>!ids.Contains(id)).ToArray()) { Destroy(locks[key].gameObject); locks.Remove(key); }
        }
        void OnDisable() { aims.Clear(); blocked.Clear(); foreach(var r in markers.Values) if(r) r.gameObject.SetActive(false); foreach(var r in locks.Values) if(r) r.gameObject.SetActive(false); }
        void OnDestroy() { if(markerMaterial) Destroy(markerMaterial); if(lockMaterial) Destroy(lockMaterial); if(markerMesh) Destroy(markerMesh); }
    }
}

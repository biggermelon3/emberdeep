using System.Collections.Generic;
using System.Linq;
namespace PhysicalAssembly
{
    // Physics edges remain authoritative; this spanning forest retains attachment ancestry.
    public sealed class AssemblyAttachmentGraph
    {
        readonly AssemblyWorld world;
        long sequence;
        public AssemblyAttachmentGraph(AssemblyWorld owner) { world=owner; }
        public AssemblyPart Root(AssemblyPart member)
        {
            var seen=new HashSet<string>(); var current=member;
            while(current && seen.Add(current.InstanceId)) {
                var edge=world.Connections.FirstOrDefault(e=>e.hierarchyEdge && (e.parentIsA?e.b.partId:e.a.partId)==current.InstanceId);
                if(edge==null) return current;
                if(!world.Parts.TryGetValue(edge.parentIsA?edge.a.partId:edge.b.partId,out current)) return member;
            }
            return member;
        }
        public void Commit(ConnectionRecord edge)
        {
            if(edge.attachmentOrder>0) {
                if(world.Connections.Any(e=>e.attachmentOrder==edge.attachmentOrder)) edge.attachmentOrder=++sequence;
                sequence=System.Math.Max(sequence,edge.attachmentOrder); return;
            }
            edge.attachmentOrder=++sequence;
            var source=world.GetConnectedParts(edge.a.partId);
            if(source.Any(p=>p.InstanceId==edge.b.partId)) return;
            // Re-root only the moving component, keeping its internal joints intact.
            var visited=new HashSet<string>{edge.a.partId}; var queue=new Queue<string>(); queue.Enqueue(edge.a.partId);
            while(queue.Count>0) {
                var id=queue.Dequeue();
                foreach(var e in world.Connections.Where(e=>e.hierarchyEdge).OrderBy(e=>e.attachmentOrder)) {
                    var next=e.a.partId==id?e.b.partId:e.b.partId==id?e.a.partId:null;
                    if(next==null || !visited.Add(next)) continue;
                    e.parentIsA=e.a.partId==id; queue.Enqueue(next);
                }
            }
            edge.hierarchyEdge=true; edge.parentIsA=false;
        }
    }
}

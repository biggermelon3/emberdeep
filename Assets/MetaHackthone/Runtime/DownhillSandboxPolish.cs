using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PhysicalAssembly
{
    public sealed partial class DownhillController
    {
        public float BoundaryGrace=.1f, DecomposeSeconds=.2f, FlowSeconds=.6f, RecomposeSeconds=.2f;
        readonly Dictionary<AssemblyPart,float> outsideTimes=new();
        readonly Dictionary<AssemblyPart,SandboxReturnEffect> boundaryReturns=new();
        SandboxBuildVolume volume;
        public int BoundaryReturnCount => boundaryReturns.Count;
        void TickSandboxPolish(float delta)
        {
            if(!Core || kit.Count==0) return;
            EnsureReward();
            if(!Course.BuildArea) {
                var go=new GameObject("Sandbox building boundary"); go.transform.SetParent(Course.WorkbenchFrame,false);
                Course.BuildArea=go.AddComponent<BuildingArea>();
                var bounds=new Bounds(Course.WorkbenchFrame.InverseTransformPoint(Core.MainBody.transform.position),Vector3.zero);
                foreach(var part in kit) foreach(var r in part.VisualRenderers) {
                    var b=r.bounds;
                    for(int i=0;i<8;i++) bounds.Encapsulate(Course.WorkbenchFrame.InverseTransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
                }
                bounds.Expand(.3f/Mathf.Max(.01f,Course.WorkbenchFrame.lossyScale.x));
                go.transform.localPosition=bounds.center;
                Course.BuildArea.Size=Vector3.one*Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
            }
            if(!volume) { volume=Course.BuildArea.GetComponent<SandboxBuildVolume>(); if(!volume) volume=Course.BuildArea.gameObject.AddComponent<SandboxBuildVolume>(); volume.Rebuild(); }
            volume.SetVisible(IsBuilding);
            if(!IsBuilding || Workshop.RepositionRequested) return;
            foreach(var pair in boundaryReturns.ToArray()) {
                if(!pair.Key) { if(pair.Value) Destroy(pair.Value.gameObject); boundaryReturns.Remove(pair.Key); continue; }
                if(pair.Value.Advance(delta)) CompleteBoundaryReturn(pair.Key,pair.Value);
            }
            foreach(var part in kit) {
                if(!part || part==Core || IsCoreConnected(part) || IsReturning(part) || Workshop.World.IsSuspended(part) || Course.BuildArea.Contains(part.MainBody.Rigidbody.position)) { outsideTimes.Remove(part); continue; }
                outsideTimes.TryGetValue(part,out float time); time+=delta; outsideTimes[part]=time;
                if(time<BoundaryGrace) continue;
                var group=Workshop.World.GetConnectedParts(part.InstanceId);
                if(group.Any(p=>!spawns.ContainsKey(p) || IsCoreConnected(p) || IsReturning(p) || Workshop.World.IsSuspended(p))) continue;
                var ids=new HashSet<string>(group.Select(p=>p.InstanceId));
                foreach(var edge in Workshop.World.Connections.Where(e=>ids.Contains(e.a.partId)||ids.Contains(e.b.partId)).ToArray()) Workshop.World.RemoveConnection(edge.id);
                foreach(var member in group) {
                    outsideTimes.Remove(member);
                    var effect=new GameObject("Yellow return "+member.name).AddComponent<SandboxReturnEffect>();
                    effect.transform.SetParent(transform,false);
                    var target=spawns[member].First(p=>p.Body==member.MainBody);
                    effect.Initialize(member,Course.WorkbenchFrame,target.Position,target.Rotation,DecomposeSeconds,FlowSeconds,RecomposeSeconds,Mathf.Max(1,512/kit.Count));
                    boundaryReturns.Add(member,effect);
                }
            }
        }
        void CompleteBoundaryReturn(AssemblyPart part,SandboxReturnEffect effect)
        {
            if(part && spawns.ContainsKey(part)) Restore(spawns[part]);
            if(effect) { effect.Restore(); Destroy(effect.gameObject); }
            boundaryReturns.Remove(part);
            if(Workshop && Workshop.World) Workshop.World.Collisions.Reapply();
        }
        void CompleteBoundaryReturns()
        {
            foreach(var pair in boundaryReturns.ToArray()) CompleteBoundaryReturn(pair.Key,pair.Value);
            outsideTimes.Clear();
        }
    }
}


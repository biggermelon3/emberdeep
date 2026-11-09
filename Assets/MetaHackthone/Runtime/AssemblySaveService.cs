using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace PhysicalAssembly
{
    [Serializable] public sealed class AssemblySaveV1 { public int formatVersion=4; public List<PartSaveV1> parts=new(); public List<ConnectionRecord> connections=new(); }
    [Serializable] public sealed class PartSaveV1 { public string id; public PartKind kind; public int gridVersion; public bool buildingCore; public float geometryScale=1; public float designWheelRadius; public List<BodyPoseV1> bodies=new(); }
    [Serializable] public sealed class BodyPoseV1 { public string id; public Vector3 position; public Quaternion rotation; }
    public sealed class AssemblySaveService : MonoBehaviour
    {
        public AssemblyWorld World; public PartFactory Factory; public WorkshopSession Session;
        public string SavePath => Path.Combine(Application.persistentDataPath,"Constructions","construction-v3.json");
        public AssemblySaveV1 Capture(AssemblyPart part)
        {
            if(!World.EditingEnabled) throw new InvalidOperationException("Retry before saving a construction.");
            if(!part || World.IsBusy || World.HasHolds) throw new InvalidOperationException("Release parts and finish attachments before saving.");
            if(part.Kind==PartKind.PhysicsSphere) throw new InvalidOperationException("Select a construction; test spheres are temporary.");
            var group=World.GetConnectedParts(part.InstanceId); var ids=group.Select(p=>p.InstanceId).ToHashSet();
            var origin=part.MainBody.Rigidbody; var inverse=Quaternion.Inverse(origin.rotation); var save=new AssemblySaveV1();
            foreach(var p in group)
            {
                var record=new PartSaveV1 { id=p.InstanceId,kind=p.Kind,gridVersion=p.GridVersion,buildingCore=p.BuildingCore,geometryScale=p.GeometryScale,designWheelRadius=p.DesignWheelRadius };
                foreach(var b in p.Bodies) record.bodies.Add(new BodyPoseV1 { id=b.Id,position=inverse*(b.Rigidbody.position-origin.position),rotation=inverse*b.Rigidbody.rotation });
                save.parts.Add(record);
            }
            // Clone records; save data must never alias the live graph.
            foreach(var c in World.Connections.Where(c=>ids.Contains(c.a.partId)&&ids.Contains(c.b.partId))) save.connections.Add(JsonUtility.FromJson<ConnectionRecord>(JsonUtility.ToJson(c)));
            return save;
        }
        public void Save()
        {
            try
            {
                if(Session.Mode!=WorkshopMode.Build) throw new InvalidOperationException("Switch to Build before saving.");
                var part=Session.Selected?Session.Selected:Session.Vehicle;
                if(!part) part=World.Parts.Values.FirstOrDefault(p=>p.Kind==PartKind.Chassis);
                var save=Capture(part); Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
                File.WriteAllText(SavePath+".tmp",JsonUtility.ToJson(save,true));
                if(File.Exists(SavePath)) File.Replace(SavePath+".tmp",SavePath,null); else File.Move(SavePath+".tmp",SavePath);
                Session.Notify("Construction saved on this device.");
            }
            catch(Exception e) { Session.Notify("Save failed: "+e.Message); }
        }
        public static void Validate(AssemblySaveV1 save)
        {
            if(save!=null && save.formatVersion<3) throw new InvalidDataException("This save predates exclusive grid connections. Rebuild it with the container kit; the original file is preserved.");
            if(save==null || (save.formatVersion!=3 && save.formatVersion!=4) || save.parts==null || save.connections==null || save.parts.Count<1 || save.parts.Count>100 || save.connections.Count>200) throw new InvalidDataException("Unsupported or oversized construction.");
            var ids=new HashSet<string>(); var bodies=new HashSet<string>();
            foreach(var p in save.parts)
            {
                if(!float.IsFinite(p.geometryScale) || p.geometryScale<=0) throw new InvalidDataException("Invalid geometry scale.");
                if(p.gridVersion==9 && DownhillParts.IsWheel(p.kind) && (!float.IsFinite(p.designWheelRadius)||p.designWheelRadius<.16f||p.designWheelRadius>.44f)) throw new InvalidDataException("Invalid wheel radius.");
                if(p.gridVersion<0 || p.gridVersion>PartFactory.LatestGridVersion) throw new InvalidDataException("Unsupported connection grid.");
                if(string.IsNullOrEmpty(p.id)||!ids.Add(p.id)||(!Enum.IsDefined(typeof(PartKind),p.kind)||p.kind==PartKind.PhysicsSphere)||p.bodies==null||p.bodies.Count<1||p.bodies.Count>4) throw new InvalidDataException("Invalid part definition or identity.");
                foreach(var b in p.bodies)
                {
                    if(string.IsNullOrEmpty(b.id)||!bodies.Add(p.id+"/"+b.id)||!Finite(b.position)||b.position.sqrMagnitude>10000 || !Finite(new Vector3(b.rotation.x,b.rotation.y,b.rotation.z)) || !float.IsFinite(b.rotation.w)) throw new InvalidDataException("Invalid body pose.");
                    float norm=Quaternion.Dot(b.rotation,b.rotation); if(norm<.9f||norm>1.1f) throw new InvalidDataException("Invalid body rotation.");
                }
            }
            var edges=new HashSet<string>(); var endpoints=new HashSet<string>();
            foreach(var c in save.connections)
                if(c==null||string.IsNullOrEmpty(c.socketA)||string.IsNullOrEmpty(c.socketB)||!endpoints.Add(c.a.partId+"/"+c.a.bodyId+"/"+c.socketA)||!endpoints.Add(c.b.partId+"/"+c.b.bodyId+"/"+c.socketB)||string.IsNullOrEmpty(c.id)||!edges.Add(c.id)||c.a.partId==c.b.partId||!bodies.Contains(c.a.partId+"/"+c.a.bodyId)||!bodies.Contains(c.b.partId+"/"+c.b.bodyId)||!Finite(c.anchorA)||!Finite(c.anchorB)||c.anchorA.sqrMagnitude>100||c.anchorB.sqrMagnitude>100) throw new InvalidDataException("Invalid connection.");
            if(save.formatVersion==4) {
                var orders=new HashSet<long>();var parents=new Dictionary<string,string>();
                foreach(var e in save.connections) {
                    if(e.attachmentOrder<=0 || !orders.Add(e.attachmentOrder)) throw new InvalidDataException("Invalid attachment order.");
                    if(!e.hierarchyEdge) continue;
                    var child=e.parentIsA?e.b.partId:e.a.partId;var parent=e.parentIsA?e.a.partId:e.b.partId;
                    if(parents.ContainsKey(child)) throw new InvalidDataException("Multiple hierarchy parents.");
                    parents.Add(child,parent);
                }
                foreach(var id in parents.Keys) {
                    var seen=new HashSet<string>();var current=id;
                    while(parents.TryGetValue(current,out var next)) {if(!seen.Add(current)) throw new InvalidDataException("Cyclic attachment hierarchy.");current=next;}
                }
            }
        }
        static bool Finite(Vector3 v) => float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z);
        public List<AssemblyPart> Restore(AssemblySaveV1 save,Pose placement)
        {
            if(!World.EditingEnabled) throw new InvalidOperationException("Retry before loading a construction.");
            Validate(save); if(World.IsBusy||World.HasHolds) throw new InvalidOperationException("Release parts before loading.");
            var created=new List<AssemblyPart>(); var idMap=new Dictionary<string,AssemblyPart>();
            var internalJoints=new List<(Joint joint,Rigidbody connected)>();
            try
            {
                foreach(var data in save.parts)
                {
                    // Saves made before gridVersion existed deserialize as zero and
                    // retain their original endpoints. Versions 0/1 also retain
                    // the three-unit Cuboid dimensions; version 2 uses two units.
                    var part=Factory.Create(data.kind,placement.position,placement.rotation,data.gridVersion,data.buildingCore,data.geometryScale,data.designWheelRadius); created.Add(part); idMap.Add(data.id,part);
                    if(part.Bodies.Length!=data.bodies.Count) throw new InvalidDataException("Part body count changed.");
                    foreach(var joint in part.GetComponentsInChildren<Joint>())
                    { internalJoints.Add((joint,joint.connectedBody)); joint.connectedBody=null; }
                    foreach(var body in part.Bodies)
                    {
                        body.Rigidbody.interpolation=RigidbodyInterpolation.None;
                        body.Rigidbody.detectCollisions=false;
                        body.Rigidbody.isKinematic=true;
                    }
                    foreach(var b in data.bodies)
                    {
                        var body=part.FindBody(b.id); if(!body) throw new InvalidDataException("Unknown body: "+b.id);
                        var position=placement.position+placement.rotation*b.position;
                        var rotation=placement.rotation*b.rotation;
                        body.transform.SetPositionAndRotation(position,rotation);
                        body.Rigidbody.position=position; body.Rigidbody.rotation=rotation;
                    }
                }
                Physics.SyncTransforms();
                // Rebind only after ALL compound bodies have their final poses and
                // dynamic mass properties. No physics step occurs during staging.
                foreach(var p in created) foreach(var b in p.Bodies) b.Rigidbody.isKinematic=b.Anchored;
                foreach(var pair in internalJoints) pair.joint.connectedBody=pair.connected;
                foreach(var original in save.connections)
                {
                    var c=JsonUtility.FromJson<ConnectionRecord>(JsonUtility.ToJson(original)); c.id=Guid.NewGuid().ToString("N");
                    c.a.partId=idMap[original.a.partId].InstanceId; c.b.partId=idMap[original.b.partId].InstanceId;
                    if(!string.IsNullOrEmpty(c.socketA)&&!idMap[original.a.partId].FindSocket(c.socketA)) throw new InvalidDataException("Unknown socket.");
                    if(!string.IsNullOrEmpty(c.socketB)&&!idMap[original.b.partId].FindSocket(c.socketB)) throw new InvalidDataException("Unknown socket.");
                    if(!World.Connect(c)) throw new InvalidDataException("Could not reconstruct connection.");
                }
                foreach(var p in created) foreach(var b in p.Bodies)
                {
                    b.Rigidbody.detectCollisions=true; b.Rigidbody.interpolation=RigidbodyInterpolation.Interpolate;
                    if(!b.Anchored) { b.Rigidbody.linearVelocity=Vector3.zero; b.Rigidbody.angularVelocity=Vector3.zero; }
                }
                World.Collisions.Reapply();
                Physics.SyncTransforms();
                return created;
            }
            catch { foreach(var p in created) { World.UnregisterPart(p); p.gameObject.SetActive(false); if(Application.isPlaying) Destroy(p.gameObject); else DestroyImmediate(p.gameObject); } throw; }
        }
        public void Load(Pose placement)
        {
            try
            {
                if(!File.Exists(SavePath)) throw new FileNotFoundException("No saved construction yet. Older car saves are preserved but require rebuilding with grid connections.");
                if(new FileInfo(SavePath).Length>1024*1024) throw new InvalidDataException("Save file too large.");
                var replaced=Session.Selected?World.GetConnectedParts(Session.Selected.InstanceId):new List<AssemblyPart>();
                Session.EnterBuild(); var loaded=Restore(JsonUtility.FromJson<AssemblySaveV1>(File.ReadAllText(SavePath)),placement);
                foreach(var old in replaced) { World.UnregisterPart(old); old.gameObject.SetActive(false); Destroy(old.gameObject); }
                Session.Select(loaded.FirstOrDefault(p=>p.Kind==PartKind.Chassis)??loaded[0]); Session.Notify("Construction loaded. Physics is active in Build mode.");
            }
            catch(Exception e) { Session.Notify("Load failed: "+e.Message); }
        }
    }
}

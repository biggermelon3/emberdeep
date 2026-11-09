using System.Linq;
using UnityEngine;

namespace PhysicalAssembly
{
    // Owns only the simulation vehicle. The workbench construction and its joints are untouched.
    public sealed class SandboxSplineRide
    {
        readonly GravityGarage3DCourse course;
        readonly SandboxSplineZone zone;
        readonly AssemblyPart core;
        readonly AssemblyPart[] parts;
        Rigidbody[] bodies;
        AssemblyPart[] vehicle;
        Pose[] relative;
        Pose entry;
        Vector3 surfaceOffset,entryOffset;
        Quaternion surfaceRotation;
        float distance,age,vehicleRadius;
        SandboxTrackMomentum momentum;
        Vector3 previousPosition;
        SandboxConnectionTransfer transfer;
        float transferSpeed,transferEntrySpeed;
        GarageContact[] rideContacts;
        public int ExitDirection { get; private set; }
        public bool EnteredFromUpper { get; private set; }
        public int EntryCount { get; private set; }
        public int ExitCount { get; private set; }
        public float SignedSpeed => zone.ConnectionArea?transferSpeed:momentum?.Speed ?? 0;
        public bool Active { get; private set; }
        public bool Parked { get; private set; }
        public bool Blocked { get; private set; }
        public float Distance => distance;
        public Pose TargetPose => zone.ConnectionArea?new Pose(core.MainBody.Rigidbody.position,core.MainBody.Rigidbody.rotation):Target(distance);

        public SandboxSplineRide(GravityGarage3DCourse course,AssemblyPart[] parts,AssemblyPart core)
        {this.course=course;zone=course.SplineZone;this.parts=parts;this.core=core;previousPosition=core.MainBody.Rigidbody.position;}

        public bool TryEnter(GarageContact[] contacts)
        {
            if(!zone || !zone.isActiveAndEnabled)return false;
            var current=core.MainBody.Rigidbody.position;
            bool forward=false,backward=false;
            if(zone.ConnectionArea){forward=zone.CrossesGate(previousPosition,current,true);backward=zone.CrossesGate(previousPosition,current,false);previousPosition=current;if(!forward && !backward)return false;}
            var connected=core.World.GetConnectedParts(core.InstanceId);
            vehicle=parts.Where(connected.Contains).ToArray();
            bodies=vehicle.SelectMany(p=>p.Bodies).Select(b=>b.Rigidbody).ToArray();
            var rb=core.MainBody.Rigidbody;entry=new Pose(rb.position,rb.rotation);
            var wheels=vehicle.SelectMany(p=>p.GetComponentsInChildren<PassiveWheel>()).ToArray();
            var center=wheels.Length>0?wheels.Aggregate(Vector3.zero,(sum,w)=>sum+w.transform.position)/wheels.Length:rb.position;
            float projected=zone.Project(center).Distance;
            if(zone.ConnectionArea) {
                EnteredFromUpper=forward;distance=forward?zone.GuidanceStart:zone.GuidanceEnd;
                var gate=zone.Sample(distance);var gatePose=new Pose(gate.Position,Quaternion.LookRotation(gate.Tangent,gate.Normal));
                transferSpeed=bodies.Sum(b=>b.mass*Vector3.Dot(b.linearVelocity,gate.Tangent))/bodies.Sum(b=>b.mass)/Mathf.Sqrt(course.WorldScale);
                transferEntrySpeed=transferSpeed;
                transfer=new SandboxConnectionTransfer(bodies,gatePose);
                rideContacts=contacts;ResetContactHistory();EntryCount++;ExitDirection=0;
                age=0;Active=true;Parked=Blocked=false;return true;
            } else if(!contacts.Any(c=>zone.Roads.Any(r=>r && c.Supports(r))))return false;
            distance=Mathf.Min(projected,zone.StopDistance);
            var sample=zone.Sample(distance);
            float entrySpeed=bodies.Sum(b=>b.mass*Vector3.Dot(b.linearVelocity,sample.Tangent))/bodies.Sum(b=>b.mass)/Mathf.Sqrt(course.WorldScale);
            momentum=new SandboxTrackMomentum(distance,entrySpeed);
            var frame=Quaternion.LookRotation(sample.Tangent,sample.Normal);
            var centered=course.SurfacePose(sample,vehicle,core,1);
            surfaceOffset=Quaternion.Inverse(frame)*(centered.position-sample.Position);
            surfaceRotation=Quaternion.Inverse(frame)*centered.rotation;
            relative=bodies.Select(b=>new Pose(Quaternion.Inverse(entry.rotation)*(b.position-entry.position),Quaternion.Inverse(entry.rotation)*b.rotation)).ToArray();
            vehicleRadius=vehicle.SelectMany(p=>p.Bodies).SelectMany(b=>b.Colliders)
                .Max(c=>Vector3.Distance(c.bounds.center,entry.position)+c.bounds.extents.magnitude);
            foreach(var body in bodies) {
                if(!body.isKinematic){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}
                body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
                body.isKinematic=true;body.useGravity=false;
            }
            entryOffset=Vector3.ProjectOnPlane(entry.position-Target(distance).position,sample.Tangent);
            rideContacts=contacts;ResetContactHistory();EntryCount++;ExitDirection=0;
            Active=true;age=0;Parked=Blocked=false;return true;
        }
        Pose Target(float d)
        {
            var s=zone.Sample(d);var rotation=Quaternion.LookRotation(s.Tangent,s.Normal);
            return new Pose(s.Position+rotation*surfaceOffset,rotation*surfaceRotation);
        }
        public void Tick(float dt)
        {
            if(!Active || Parked || Blocked)return;
            if(zone.ConnectionArea) {
                float direction=EnteredFromUpper?1:-1;
                float speed=Mathf.Max(0,direction*transferSpeed);
                float acceleration=Mathf.Max(0,zone.TransferAcceleration);
                float destination=EnteredFromUpper?zone.GuidanceEnd:zone.GuidanceStart;
                float remaining=Mathf.Abs(destination-distance);
                float travel=speed*dt+.5f*acceleration*dt*dt;
                bool arrived=travel>=remaining;
                float step=dt;
                if(arrived)step=remaining<=0?0:acceleration>0?2*remaining/(Mathf.Sqrt(speed*speed+2*acceleration*remaining)+speed):remaining/Mathf.Max(speed,.000001f);
                transferSpeed=direction*(speed+acceleration*step);
                distance=arrived?destination:distance+direction*travel;
                var sample=zone.Sample(distance);transfer.Move(new Pose(sample.Position,Quaternion.LookRotation(sample.Tangent,sample.Normal)));
                if(arrived){transfer.Release(core.MainBody.Rigidbody,course.SurfacePose(sample,vehicle,core,1),sample.Tangent*transferSpeed*Mathf.Sqrt(course.WorldScale));transfer=null;core.World.Collisions.Reapply();ResetContactHistory();previousPosition=core.MainBody.Rigidbody.position;Active=false;ExitDirection=EnteredFromUpper?1:-1;ExitCount++;}
                return;
            }
            age+=dt;
            momentum.Tick(zone,Physics.gravity,dt);
            float next=momentum.Distance;
            var pose=Target(next);
            float blend=zone.CenteringDuration<=0?1:Mathf.SmoothStep(0,1,age/zone.CenteringDuration);
            // Decay only the initial transverse offset: centering cannot advance the car.
            pose=new Pose(pose.position+Vector3.ProjectOnPlane(entryOffset,zone.Sample(next).Tangent)*(1-blend),Quaternion.Slerp(entry.rotation,pose.rotation,blend));
            if(zone.RequireDrillWall && course.Wall && course.Wall.activeInHierarchy && !course.WallBroken) {
                TryDrill(pose);
                if(!course.WallBroken && HitsWall(pose)){Blocked=true;return;}
            }
            distance=next;Apply(pose);
            if(momentum.Arrived && blend>=1){Apply(Target(zone.StopDistance));Parked=true;}
        }
        void ResetContactHistory()
        {
            foreach(var c in rideContacts)c.ResetContact();
            foreach(var drill in parts.SelectMany(p=>p.GetComponentsInChildren<SandboxDrill>()))drill.ResetSample();
        }
        public void Cancel()
        {
            if(transfer!=null){transfer.Restore();transfer=null;Active=false;}
        }
        void Apply(Pose pose)
        {
            for(int i=0;i<bodies.Length;i++) {
                var body=bodies[i];body.position=pose.position+pose.rotation*relative[i].position;body.rotation=pose.rotation*relative[i].rotation;
                body.transform.SetPositionAndRotation(body.position,body.rotation);
            }
        }
        void TryDrill(Pose next)
        {
            if(!course.Markers && course.Project(next.position).Distance<course.LowerStart)return;
            var rb=core.MainBody.Rigidbody;var delta=next.rotation*Quaternion.Inverse(rb.rotation);
            var connected=core.World.GetConnectedParts(core.InstanceId);
            foreach(var part in parts.Where(p=>p.Kind==PartKind.DownhillDrill && connected.Contains(p)))
                foreach(var drill in part.GetComponentsInChildren<SandboxDrill>()) {
                    var tip=drill.transform.TransformPoint(SandboxDrill.Tip(BuildingGeometry.Unit,part.GeometryScale));
                    course.TryDrill(tip,next.position+delta*(tip-rb.position),BuildingGeometry.Unit*part.GeometryScale*.1f);
                }
        }
        bool HitsWall(Pose next)
        {
            var wall=course.Wall.GetComponent<Collider>();if(!wall || !wall.enabled)return false;
            var rb=core.MainBody.Rigidbody;var delta=next.rotation*Quaternion.Inverse(rb.rotation);
            float reach=vehicleRadius+Vector3.Distance(rb.position,next.position);
            if(wall.bounds.SqrDistance(next.position)>reach*reach)return false;
            foreach(var body in bodies) {
                var shift=next.position+delta*(body.position-rb.position)-body.position;
                if(shift.sqrMagnitude>1e-12f && body.SweepTestAll(shift.normalized,shift.magnitude,QueryTriggerInteraction.Ignore).Any(h=>h.collider==wall))return true;
                foreach(var collider in body.GetComponentsInChildren<Collider>()) if(collider.enabled && !collider.isTrigger)
                    if(Physics.ComputePenetration(collider,next.position+delta*(collider.transform.position-rb.position),delta*collider.transform.rotation,wall,wall.transform.position,wall.transform.rotation,out _,out _))return true;
            }
            return false;
        }
    }
}

using System.Linq;
using UnityEngine;

namespace PhysicalAssembly
{
    public sealed class GravityGarage3DRun : MonoBehaviour, ISandboxDrillTarget
    {
        public GravityGarage3DCourse Course { get; private set; }
        public bool Finished { get; private set; }
        public bool Success { get; private set; }
        public bool Running { get; private set; }
        public float Elapsed { get; private set; }
        public float Progress { get; private set; }
        public float MaxTilt { get; private set; }
        public string Feedback { get; private set; }
        public Vector3 CenterOfMass { get; private set; }
        public int GuidanceEntries => splineRide?.EntryCount ?? 0;
        public int GuidanceExits => splineRide?.ExitCount ?? 0;
        bool UsesConnectionArea => Course.SplineZone && Course.SplineZone.ConnectionArea;
        public bool SplineActive => splineRide!=null && splineRide.Active;
        public float SplineSignedSpeed => splineRide?.SignedSpeed ?? 0;
        public bool SplineParked => splineRide!=null && splineRide.Parked;
        public Pose SplineTargetPose => SplineActive?splineRide.TargetPose:default;
        SandboxSplineRide splineRide;
        AssemblyPart core;AssemblyPart[] parts;Rigidbody[] bodies;GarageContact[] contacts;
        Pose[] initial;
        float furthest,inverted,rollback;int checkpoint;
        readonly SandboxWaterContactHistory waterContacts=new();
        Collider[] vehicleColliders;
        Rigidbody[] massBodies;
        int massTopology=-1;
        readonly System.Collections.Generic.Dictionary<BoxCollider,SandboxWaterContactHistory> authoredWaterContacts=new();
        public void Bind(GravityGarage3DCourse course,AssemblyPart[] vehicle,AssemblyPart chassis)
        {
            Course=course;Course.BindAuthored();core=chassis;parts=vehicle;bodies=parts.SelectMany(p=>p.Bodies).Select(b=>b.Rigidbody).ToArray();
            initial=bodies.Select(b=>new Pose(b.position,b.rotation)).ToArray();
            contacts=parts.SelectMany(p=>p.GetComponentsInChildren<PassiveWheel>()).Select(w=>{var c=w.GetComponent<GarageContact>();return c?c:w.gameObject.AddComponent<GarageContact>();}).ToArray();
            var colliders=vehicleColliders=parts.SelectMany(p=>p.Bodies).SelectMany(b=>b.Colliders).ToArray();
            for(int i=0;i<colliders.Length;i++)for(int j=i+1;j<colliders.Length;j++)core.World.Collisions.Acquire(colliders[i],colliders[j],"3d-run");
            ResetVehicle();
        }
        public void ResetVehicle()
        {
            splineRide?.Cancel();
            if(Course.SplineZone)Course.SplineZone.RebuildPath();
            splineRide=null;
            Running=Finished=Success=false;Elapsed=Progress=MaxTilt=furthest=inverted=rollback=0;checkpoint=0;Feedback="Balance the load forward and aim the drill at the yellow weak point.";Course.ResetCourse();waterContacts.Reset();authoredWaterContacts.Clear();
            for(int i=0;i<bodies.Length;i++){var b=bodies[i];if(!b.isKinematic){b.linearVelocity=Vector3.zero;b.angularVelocity=Vector3.zero;}b.isKinematic=true;b.useGravity=false;b.position=initial[i].position;b.rotation=initial[i].rotation;b.transform.SetPositionAndRotation(b.position,b.rotation);}
            foreach(var c in contacts)c.ResetContact();foreach(var drill in parts.SelectMany(p=>p.GetComponentsInChildren<SandboxDrill>()))drill.ResetSample();Physics.SyncTransforms();UpdateMass();
            splineRide=Course.SplineZone?new SandboxSplineRide(Course,parts,core):null;
        }
        public void StartRun(){ResetVehicle();furthest=Course.Project(core.MainBody.Rigidbody.position).Distance;Running=true;foreach(var b in bodies){b.isKinematic=false;b.useGravity=true;b.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;b.ResetInertiaTensor();b.linearVelocity=Vector3.zero;b.angularVelocity=Vector3.zero;b.WakeUp();}core.World.Collisions.Reapply();}
        void UpdateMass(){if(massBodies==null || massTopology!=core.World.TopologyVersion){massTopology=core.World.TopologyVersion;massBodies=core.World.GetConnectedParts(core.InstanceId).SelectMany(p=>p.Bodies).Select(b=>b.Rigidbody).ToArray();}float mass=0;var sum=Vector3.zero;foreach(var b in massBodies){mass+=b.mass;sum+=b.worldCenterOfMass*b.mass;}CenterOfMass=sum/Mathf.Max(.0001f,mass);}
        public bool DrillActive(AssemblyPart drill)=>Running&&!Finished&&!(UsesConnectionArea && SplineActive)&&(Course.Markers || checkpoint>=2)&&drill.Kind==PartKind.DownhillDrill&&core.World.GetConnectedParts(core.InstanceId).Contains(drill);
        public void SweepDrill(AssemblyPart part,Vector3 from,Vector3 to,float radius){if(DrillActive(part))Course.TryDrill(from,to,radius);}
        void FixedUpdate()
        {
            if(!Running||Finished)return;
            float dt=Time.fixedDeltaTime/Mathf.Sqrt(Course.WorldScale);
            if(splineRide!=null && (splineRide.Active || splineRide.TryEnter(contacts))) {
                if(UsesConnectionArea) {
                    if(splineRide.EnteredFromUpper)checkpoint=Mathf.Max(checkpoint,1);
                    splineRide.Tick(dt);UpdateMass();
                    if(!splineRide.Active) {
                        furthest=Mathf.Max(furthest,Course.Project(core.MainBody.Rigidbody.position).Distance);
                        if(splineRide.ExitDirection>0 && checkpoint>=1)checkpoint=2;
                        waterContacts.Reset();authoredWaterContacts.Clear();
                    }
                    Feedback=splineRide.Active?"Transferring through connection area":splineRide.ExitDirection>0?"Lower road: aim the drill at the yellow weak point":"Upper balance challenge";
                    return;
                }
                if(!splineRide.Parked)Elapsed+=dt;
                splineRide.Tick(dt);UpdateMass();
                float splineDistance=Course.Project(core.MainBody.Rigidbody.position).Distance;Progress=splineDistance/Course.Length;
                checkpoint=Mathf.Max(checkpoint,splineDistance>=Course.LowerStart?2:splineDistance>=Course.TurnStart?1:0);
                if(splineRide.Blocked){Finish(false,"Drill wall blocked the car. Attach and align the drill, then retry.");return;}
                if(!splineRide.Parked && Elapsed>Course.Level.Timeout){Finish(false,"Timeout: insufficient momentum. Adjust the track or starting height.");return;}
                Feedback=splineRide.Parked?"Parked at the end of the wooden road. Retry or Back to Build.":splineRide.SignedSpeed<-.001f?"Rolling backward":Mathf.Abs(splineRide.SignedSpeed)<.001f?"Stalled: insufficient momentum":Vector3.Dot(Physics.gravity,Course.SplineZone.Sample(splineRide.Distance).Tangent)<0?"Climbing: losing speed":"Rolling along the wooden road";
                if(splineRide.Parked && Course.SplineZone.CompleteLevelOnArrival)Finish(true,"Waterfall Valley complete!");
                return;
            }
            UpdateMass();Elapsed+=dt;
            var rb=core.MainBody.Rigidbody;var s=Course.Project(rb.position);float d=s.Distance;
            bool supported=contacts.Count(c=>Course.Markers?Course.Markers.OnRoad(c):c.Supports(Course.Road))>=2;
            float up=Vector3.Dot(rb.rotation*Vector3.up,s.Normal);
            if(d<Course.TurnStart)MaxTilt=Mathf.Max(MaxTilt,Mathf.Acos(Mathf.Clamp(up,-1,1))*Mathf.Rad2Deg);
            // Gates cannot be skipped by falling from the upper road onto the return lane.
            if(checkpoint==0&&d>=Course.TurnStart-.5f&&d<Course.TurnStart+1&&supported)checkpoint=1;
            if(checkpoint==1&&d>=Course.LowerStart-.5f&&d<Course.LowerStart+1&&supported)checkpoint=2;
            
            Progress=UsesConnectionArea?(Mathf.Min(d,Course.TurnStart)+Mathf.Max(0,d-Course.LowerStart))/Mathf.Max(.001f,Course.Length-(Course.LowerStart-Course.TurnStart)):d/Course.Length;
            inverted=up<.1f?inverted+dt:0;
            rollback=supported&&Vector3.Dot(rb.linearVelocity,s.Tangent)<-.15f*Mathf.Sqrt(Course.WorldScale)?rollback+dt:0;
            if(CheckWater()){Finish(false,"Vehicle fell into the water.");return;}
            if(Course.Markers?!Course.Markers.Inside(rb.position):Course.Coordinates(rb.position).y<-2){Finish(false,"Vehicle fell off the course.");return;}
            if(d>furthest+1.5f){Finish(false,"Missed a course section. Stay on the continuous road.");return;}
            furthest=Mathf.Max(furthest,d);
            if(inverted>.6f){Finish(false,"Vehicle overturned. Move the load lower or forward.");return;}
            if(!UsesConnectionArea&&rollback>.5f&&furthest-d>.25f){Finish(false,"Vehicle rolled backward. Move the load forward.");return;}
            if(Elapsed>Course.Level.Timeout){Finish(false,"Timeout: vehicle stalled. Adjust the balance.");return;}
            if((Course.Markers?Course.Markers.Passed(Course.Markers.Finish,rb.position):d>=Course.Length-1)&&checkpoint==2&&Course.WallBroken&&supported&&up>.75f&&Vector3.Dot(rb.rotation*Vector3.forward,s.Tangent)>.65f){Finish(true,"Waterfall Valley complete!");return;}
            Feedback=UsesConnectionArea && rollback>0?"Rolling backward":checkpoint==0?"Upper balance challenge":checkpoint==1?"Descending connection area":"Return lane: aim the drill at the yellow weak point";
        }
        bool CheckWater()
        {
            if(!Course.Markers) return waterContacts.Check(vehicleColliders,Course.transform.up,Course.Coordinates,Course.WaterVolumes,out _);
            foreach(var volume in Course.Markers.WaterRegions) if(volume) {
                if(!authoredWaterContacts.TryGetValue(volume,out var history)) authoredWaterContacts.Add(volume,history=new SandboxWaterContactHistory());
                if(history.Check(vehicleColliders,volume.transform.up,volume.transform.InverseTransformPoint,new Bounds(volume.center,volume.size),out _))return true;
            }
            return false;
        }
        void Finish(bool success,string feedback){splineRide?.Cancel();Finished=true;Running=false;Success=success;Feedback=feedback;foreach(var b in bodies){if(!b.isKinematic){b.linearVelocity=Vector3.zero;b.angularVelocity=Vector3.zero;}b.isKinematic=true;b.useGravity=false;}}
        void OnDisable(){splineRide?.Cancel();}
        void OnDestroy(){splineRide?.Cancel();if(core&&core.World)core.World.Collisions.Release("3d-run");}
    }
}

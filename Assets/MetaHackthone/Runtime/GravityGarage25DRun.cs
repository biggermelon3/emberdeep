using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PhysicalAssembly
{
    internal static class PresetAttachment
    {
        internal static void Connect(AssemblyWorld world,AssemblyPart part,AssemblyPart target,Quaternion rotation,Vector3 normal,string overlapMessage,string connectionMessage)
        {
                var ts=target.Sockets.Where(s=>Vector3.Dot(s.Normal,rotation*normal)>.99f).OrderBy(s=>Vector3.Distance(s.WorldPosition,part.MainBody.Rigidbody.position)).First();
                var ss=part.Sockets.Where(s=>Vector3.Dot(s.Normal,ts.Normal)<-.99f).OrderBy(s=>Vector3.Distance(s.WorldPosition,ts.WorldPosition)).First();
                var shift=ts.WorldPosition-ss.WorldPosition;
                foreach(var b in part.Bodies){b.Rigidbody.position+=shift;b.transform.position=b.Rigidbody.position;}
                Physics.SyncTransforms();
                if(!world.PlacementClear(new AttachmentCandidate(ss.Body,ts.Body,ss.WorldPosition,ts.WorldPosition,ss,ts),new[]{part}))throw new InvalidOperationException(overlapMessage+part.Kind);
                if(!world.Connect(new ConnectionRecord{id=Guid.NewGuid().ToString("N"),a=new BodyRef(part.InstanceId,ss.Body.Id),b=new BodyRef(target.InstanceId,ts.Body.Id),socketA=ss.Id,socketB=ts.Id,anchorA=ss.transform.localPosition,anchorB=ts.transform.localPosition}))throw new InvalidOperationException(connectionMessage);
        }
    }
    public static class GravityGarage25DVehicle
    {
        public static AssemblyPart[] Create(AssemblyWorld world,GravityGarage25DParameters p,Pose pose,float scale)
        {
            p.Validate();var parts=new List<AssemblyPart>();
            AssemblyPart Make(PartKind kind,Vector3 offset,bool core=false,float radius=0)
            {
                var part=DownhillParts.Create(world,kind,new Pose(pose.position+pose.rotation*offset*scale,pose.rotation),scale,null,9,core,radius);
                part.transform.SetParent(world.transform,true);parts.Add(part);
                foreach(var b in part.Bodies){b.Rigidbody.isKinematic=true;b.Rigidbody.useGravity=false;}
                return part;
            }
            void Join(AssemblyPart part,AssemblyPart target,Vector3 normal)
            {
                PresetAttachment.Connect(world,part,target,pose.rotation,normal,"The reference vehicle overlaps at ","Reference vehicle connection failed.");
            }
            var chassis=Make(PartKind.DownhillLongBlock,Vector3.zero,true);
            for(int end=-1;end<=1;end+=2){var beam=Make(PartKind.DownhillShortBlock,Vector3.forward*(end*1.5f*BuildingGeometry.Unit));Join(beam,chassis,Vector3.forward*end);
                for(int side=-1;side<=1;side+=2){float radius=end>0?p.frontWheel:p.rearWheel;var wheel=Make(end>0?PartKind.DownhillLargeWheel:PartKind.DownhillSmallWheel,new Vector3(side*(BuildingGeometry.Unit+.02f),0,end*1.5f*BuildingGeometry.Unit),false,radius);Join(wheel,beam,Vector3.right*side);}}
            return parts.ToArray();
        }
        public static Pose ReleasePose(AssemblyPart[] parts,AssemblyPart chassis,GravityGarage25DCourse course,float transfer=1,bool lower=false)
        {
            var core=chassis.MainBody.Rigidbody;var inverse=Quaternion.Inverse(core.rotation);
            float x=lower?2.16f:2.15f,z=lower?2.25f:-1.55f;
            Func<float,float> surface=lower?course.LowerSurface:course.Parameters.Upper;
            var wheels=parts.SelectMany(a=>a.GetComponentsInChildren<PassiveWheel>()).ToArray();
            if(wheels.Length==0)return new Pose(course.Point(x,surface(x)+.4f,z),course.Heading(x,lower));
            var local=wheels.Select(w=>inverse*(w.transform.position-core.position)*transfer/course.WorldScale).ToArray();
            float min=local.Min(p=>p.z),max=local.Max(p=>p.z),middle=(min+max)*.5f,angle=0;
            for(int iteration=0;iteration<8 && max-min>.01f;iteration++) {
                float sumFront=0,sumRear=0;int nf=0,nr=0;
                for(int i=0;i<wheels.Length;i++){float px=x+local[i].z*Mathf.Cos(angle)-local[i].y*Mathf.Sin(angle);float y=surface(px)+wheels[i].Radius*transfer/course.WorldScale-local[i].y*Mathf.Cos(angle);if(local[i].z>middle){sumFront+=y;nf++;}else{sumRear+=y;nr++;}}
                if(nf==0||nr==0)break;angle=Mathf.Atan2(sumFront/nf-sumRear/nr,max-min);
            }
            var rotation=Quaternion.LookRotation(course.transform.TransformDirection(new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)),course.transform.up);
            var delta=rotation*inverse;float center=float.NegativeInfinity;
            for(int i=0;i<wheels.Length;i++){var offset=course.transform.InverseTransformDirection(delta*(wheels[i].transform.position-core.position)*transfer)/course.WorldScale;float px=x+offset.x;float slope=(surface(px+.01f)-surface(px-.01f))/.02f;float radius=wheels[i].Radius*transfer/course.WorldScale;center=Mathf.Max(center,surface(px)+radius*Mathf.Sqrt(1+slope*slope)-offset.y);}
            return new Pose(course.Point(x,center+.003f,z),rotation);
        }
        public static void Place(AssemblyPart[] parts,GravityGarage25DCourse course,bool lower=false)
        {
            var chassis=parts.First(p=>p.BuildingCore);var core=chassis.MainBody.Rigidbody;var origin=core.position;var pose=ReleasePose(parts,chassis,course,1,lower);var delta=pose.rotation*Quaternion.Inverse(core.rotation);
            foreach(var body in parts.SelectMany(a=>a.Bodies)){var rb=body.Rigidbody;rb.position=pose.position+delta*(rb.position-origin);rb.rotation=delta*rb.rotation;body.transform.SetPositionAndRotation(rb.position,rb.rotation);}
            Physics.SyncTransforms();
        }
    }
    public enum Garage25DStage { Ready, Upper, Airborne, Landing, Pipe, Lower, Complete, Failed }
    public sealed class GravityGarage25DRun : MonoBehaviour, ISandboxDrillTarget
    {
        public GravityGarage25DCourse Course { get; private set; }
        public Garage25DStage Stage { get; private set; }
        public float Elapsed { get; private set; }
        public float Speed { get; private set; }
        public float MaxSpeed { get; private set; }
        public float MinSpeed { get; private set; }
        public float Progress { get; private set; }
        public string Feedback { get; private set; }="Ready. Start a gravity test.";
        public bool Finished => Stage==Garage25DStage.Complete||Stage==Garage25DStage.Failed;
        public bool Paused { get; private set; }
        public int PipeTransfers { get; private set; }
        public bool ValidUpperLanding { get; private set; }
        public Rigidbody Core { get; private set; }
        AssemblyPart[] parts; Rigidbody[] bodies;GarageContact[] contacts;Renderer[] visuals;
        Pose[] initial;Vector3[] velocities,angular;float stable,pipeTime,entrySpeed,inverted;
        public void Bind(GravityGarage25DCourse course,AssemblyPart[] vehicle,AssemblyPart core)
        {
            Course=course;parts=vehicle;Core=core.MainBody.Rigidbody;bodies=parts.SelectMany(p=>p.Bodies).Select(b=>b.Rigidbody).ToArray();
            // Match the sandbox: welded parts must not brake their own tires through solver drift.
            var colliders=parts.SelectMany(p=>p.Bodies).SelectMany(b=>b.Colliders).ToArray();
            for(int i=0;i<colliders.Length;i++)for(int j=i+1;j<colliders.Length;j++)core.World.Collisions.Acquire(colliders[i],colliders[j],"25d-run");
            initial=bodies.Select(b=>new Pose(b.position,b.rotation)).ToArray();visuals=parts.SelectMany(p=>p.VisualRenderers).ToArray();
            contacts=parts.SelectMany(p=>p.GetComponentsInChildren<PassiveWheel>()).Select(w=>{var c=w.GetComponent<GarageContact>();if(!c)c=w.gameObject.AddComponent<GarageContact>();c.Landing=course.UpperLanding;return c;}).ToArray();
            ResetVehicle();
        }
        public void ResetVehicle()
        {
            if(bodies==null)return;Course.ResetSandstone();if(Course.Water)Course.Water.ResetContact();Paused=false;Stage=Garage25DStage.Ready;Elapsed=Speed=MaxSpeed=Progress=stable=pipeTime=inverted=0;MinSpeed=float.PositiveInfinity;PipeTransfers=0;ValidUpperLanding=false;
            for(int i=0;i<bodies.Length;i++){var b=bodies[i];if(!b.isKinematic){b.linearVelocity=Vector3.zero;b.angularVelocity=Vector3.zero;}b.isKinematic=true;b.useGravity=false;b.position=initial[i].position;b.rotation=initial[i].rotation;b.transform.SetPositionAndRotation(b.position,b.rotation);b.detectCollisions=true;}
            foreach(var r in visuals)if(r)r.enabled=true;foreach(var c in contacts){c.Landing=Course.UpperLanding;c.ResetContact();}Physics.SyncTransforms();Feedback="Ready. Start a gravity test.";
        }
        public void StartRun(){ResetVehicle();Stage=Garage25DStage.Upper;Feedback="Upper track: build speed and clear Rock A.";Release(Vector3.zero);}
        void Release(Vector3 velocity){foreach(var b in bodies){b.isKinematic=false;b.useGravity=true;b.detectCollisions=true;b.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;b.ResetInertiaTensor();b.linearVelocity=velocity;b.angularVelocity=Vector3.zero;b.WakeUp();}parts[0].World.Collisions.Reapply();}
        public void SetPaused(bool pause)
        {
            if(Finished||Stage==Garage25DStage.Ready||Paused==pause)return;
            if(pause){velocities=bodies.Select(b=>b.linearVelocity).ToArray();angular=bodies.Select(b=>b.angularVelocity).ToArray();foreach(var b in bodies)b.isKinematic=true;}
            else if(Stage!=Garage25DStage.Pipe){for(int i=0;i<bodies.Length;i++){bodies[i].isKinematic=false;bodies[i].linearVelocity=velocities[i];bodies[i].angularVelocity=angular[i];}}
            Paused=pause;
        }
        public bool DrillActive(AssemblyPart part)=>!Paused&&!Finished&&Stage==Garage25DStage.Lower;
        public void SweepDrill(AssemblyPart part,Vector3 from,Vector3 to,float radius)
        {
            if(!DrillActive(part)||!Course.Sandstone||!Course.Sandstone.activeSelf)return;
            var collider=Course.Sandstone.GetComponent<Collider>();bool hit=(collider.ClosestPoint(to)-to).sqrMagnitude<=radius*radius;
            var delta=to-from;if(!hit&&delta.sqrMagnitude>0)foreach(var contact in Physics.SphereCastAll(from,radius,delta.normalized,delta.magnitude))if(contact.collider==collider){hit=true;break;}
            if(hit){Course.Sandstone.SetActive(false);Course.gameObject.AddComponent<SandboxDrillBurst>().Begin(to,Course.WorldScale);}
        }
        void FixedUpdate()
        {
            if(!Course||!Core||Paused||Stage==Garage25DStage.Ready||Finished)return;
            float root=Mathf.Sqrt(Course.WorldScale),dt=Time.fixedDeltaTime/root;Elapsed+=dt;
            if(Stage==Garage25DStage.Pipe){pipeTime+=dt;Progress=55+10*Mathf.Clamp01(pipeTime/1.08f);if(pipeTime>=1.08f)ExitPipe();return;}
            if(Course.Water && Course.Water.CheckContact(parts.SelectMany(a=>a.Bodies).SelectMany(b=>b.Colliders))) { Finish(false,"Vehicle fell into the water.");return; }
            var p=Course.Coordinates(Core.position);Speed=Core.linearVelocity.magnitude/root;MaxSpeed=Mathf.Max(MaxSpeed,Speed);MinSpeed=Mathf.Min(MinSpeed,Speed);
            bool upright=Vector3.Dot(Core.rotation*Vector3.up,Course.transform.up)>.75f && Vector3.Dot(Core.rotation*Vector3.forward,Course.transform.right)>0;
            bool supported=contacts.Count(c=>c.Supported)>=2;
            bool calm=Core.angularVelocity.magnitude*root<1.4f;
            if(Stage!=Garage25DStage.Lower){Progress=Mathf.Clamp01((p.x-2.15f)/(16-2.15f))*55;
                if(p.x>9.55f && !ValidUpperLanding)Stage=Garage25DStage.Airborne;
                if(p.x>Course.Parameters.LandingX&&contacts.Count(c=>c.OnLanding)>=2&&upright&&calm){stable+=dt;Stage=Garage25DStage.Landing;if(stable>=.12f)ValidUpperLanding=true;}else stable=0;
                if(p.x>=16&&ValidUpperLanding&&supported&&upright){EnterPipe();return;}
            }else{Progress=65+35*Mathf.Clamp01((p.x-2.16f)/(16.8f-2.16f));if(supported&&upright&&calm&&(!Course.DrillCourse||contacts.Count(c=>c.OnLanding)>=2)){stable+=dt;if(p.x>=16.8f&&stable>=.12f){Finish(true,"Complete! Stable arrival at the goal.");return;}}else stable=0;}
            inverted=Vector3.Dot(Core.rotation*Vector3.up,Course.transform.up)>.1f?0:inverted+dt;
            if(p.y<-1.5f || (Course.Water ? !SandboxWater.InFootprint(p) : Mathf.Abs(p.z-(Stage==Garage25DStage.Lower?2.25f:-1.55f))>1.45f))Finish(false,"Vehicle fell off the course.");
            else if(inverted>.85f && (!Course.Water || supported))Finish(false,"Vehicle overturned. Adjust the wheels or course.");
            else if(Vector3.Dot(Core.linearVelocity,Course.transform.right)/root<-.8f && (!Course.Water || supported))Finish(false,"Vehicle rolled back.");
            else if(Elapsed>=29)Finish(false,"29-second timeout: vehicle stalled.");
            else if(p.x>18)Finish(false,"Vehicle missed a stable landing.");
        }
        void EnterPipe(){Stage=Garage25DStage.Pipe;pipeTime=0;entrySpeed=Speed;Feedback="Pipe transfer…";foreach(var b in bodies){b.linearVelocity=Vector3.zero;b.angularVelocity=Vector3.zero;b.isKinematic=true;b.detectCollisions=false;}foreach(var r in visuals)if(r)r.enabled=false;foreach(var c in contacts)c.ResetContact();}
        void ExitPipe()
        {
            // Restore the complete connected vehicle in its release pose, retaining joints and ancestry.
            for(int i=0;i<bodies.Length;i++){bodies[i].position=initial[i].position;bodies[i].rotation=initial[i].rotation;bodies[i].transform.SetPositionAndRotation(initial[i].position,initial[i].rotation);}
            GravityGarage25DVehicle.Place(parts,Course,true);foreach(var c in contacts){c.ResetContact();c.Landing=Course.LowerLanding;}foreach(var r in visuals)if(r)r.enabled=true;
            if(Course.Water)Course.Water.ResetContact();
            Stage=Garage25DStage.Lower;PipeTransfers++;stable=0;inverted=0;Release(Course.transform.right*(Mathf.Max(1.1f,entrySpeed*.52f)*Mathf.Sqrt(Course.WorldScale)));Feedback="Lower track: clear Rock B and reach the goal.";
        }
        void OnDestroy(){if(parts!=null && parts.Length>0 && parts[0] && parts[0].World)parts[0].World.Collisions.Release("25d-run");}
        void Finish(bool success,string message){Stage=success?Garage25DStage.Complete:Garage25DStage.Failed;Feedback=message;Speed=0;if(success)Progress=100;foreach(var b in bodies){b.linearVelocity=Vector3.zero;b.angularVelocity=Vector3.zero;b.isKinematic=true;}}
    }
}

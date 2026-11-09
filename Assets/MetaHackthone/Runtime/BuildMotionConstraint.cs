using System;
using System.Collections.Generic;
using UnityEngine;

namespace PhysicalAssembly
{
    // One rigid transform for an entire held branch, including compound wheel bodies.
    public static class BuildMotionConstraint
    {
        const float Skin=.0005f, Step=.003f;
        public struct Result
        {
            public Pose Pose;
            public bool Contact, BudgetLimited, RotationBlocked, Reached;
            internal Vector3 normal0,normal1,normal2;
            internal int normalCount;
            internal void AddNormal(Vector3 normal)
            {
                if(normalCount==0) normal0=normal;
                else if(normalCount==1) normal1=normal;
                else normal2=normal;
                normalCount++;
            }
            public Vector3 ClipVelocity(Vector3 velocity)
            {
                // Repeat projections for corners; never retain energy into a solid.
                for(int pass=0;pass<3;pass++) for(int i=0;i<normalCount;i++)
                {
                    var n=i==0?normal0:i==1?normal1:normal2;
                    float into=Vector3.Dot(velocity,n);
                    if(into<0) velocity-=n*into;
                }
                return velocity;
            }
        }

        public sealed class Group
        {
            readonly AssemblyBody[] bodies;
            readonly Vector3[] positions;
            readonly Quaternion[] rotations;
            readonly HashSet<Collider> own=new();
            readonly List<Shape> shapes=new();
            readonly Collider[] overlaps=new Collider[128];
            struct Shape { public Collider collider; public Vector3 position; public Quaternion rotation; }
            public Group(AssemblyBody[] bodies,Pose origin)
            {
                this.bodies=bodies; positions=new Vector3[bodies.Length]; rotations=new Quaternion[bodies.Length];
                Physics.SyncTransforms();
                var inverse=Quaternion.Inverse(origin.rotation);
                for(int i=0;i<bodies.Length;i++)
                {
                    positions[i]=inverse*(bodies[i].Rigidbody.position-origin.position);
                    rotations[i]=inverse*bodies[i].Rigidbody.rotation;
                    foreach(var collider in bodies[i].Colliders)
                    {
                        own.Add(collider);
                        shapes.Add(new Shape { collider=collider,position=inverse*(collider.transform.position-origin.position),rotation=inverse*collider.transform.rotation });
                    }
                }
            }
            bool Blocker(Collider collider,Func<Collider,bool> blocks)
                => collider && collider.enabled && !collider.isTrigger && collider.gameObject.activeInHierarchy && !own.Contains(collider) && (blocks==null || blocks(collider));

            bool Clear(Pose pose,Func<Collider,bool> blocks,out Vector3 normal)
            {
                normal=Vector3.zero;
                foreach(var shape in shapes)
                {
                    var collider=shape.collider;
                    if(!collider || !collider.enabled || collider.isTrigger) continue;
                    var position=pose.position+pose.rotation*shape.position;
                    var rotation=pose.rotation*shape.rotation;
                    float radius=collider.bounds.extents.magnitude+Vector3.Distance(collider.bounds.center,collider.transform.position);
                    int count=Physics.OverlapCapsuleNonAlloc(collider.transform.position,position,radius,overlaps,~0,QueryTriggerInteraction.Ignore);
                    // Never silently miss a blocker in unusually dense scenes.
                    var candidates=count==overlaps.Length?Physics.OverlapCapsule(collider.transform.position,position,radius,~0,QueryTriggerInteraction.Ignore):overlaps;
                    if(candidates!=overlaps) count=candidates.Length;
                    for(int j=0;j<count;j++)
                    {
                        var other=candidates[j]; if(!Blocker(other,blocks)) continue;
                        Physics.ComputePenetration(collider,collider.transform.position,collider.transform.rotation,other,other.transform.position,other.transform.rotation,out _,out float original);
                        if(Physics.ComputePenetration(collider,position,rotation,other,other.transform.position,other.transform.rotation,out var direction,out float depth)
                            && depth>Mathf.Max(Skin,original+.00001f)) { normal=direction; return false; }
                    }
                }
                return true;
            }
            void Place(Pose pose)
            {
                for(int i=0;i<bodies.Length;i++) if(bodies[i])
                {
                    var rb=bodies[i].Rigidbody;
                    rb.position=pose.position+pose.rotation*positions[i]; rb.rotation=pose.rotation*rotations[i];
                    rb.transform.SetPositionAndRotation(rb.position,rb.rotation);
                }
                Physics.SyncTransforms();
            }
            static Pose Between(Pose a,Pose b,float t) => new(Vector3.Lerp(a.position,b.position,t),Quaternion.Slerp(a.rotation,b.rotation,t));
            Result Sweep(Pose from,Pose to,Func<Collider,bool> blocks)
            {
                Physics.SyncTransforms();
                var movement=to.position-from.position; float distance=movement.magnitude;
                float angle=Quaternion.Angle(from.rotation,to.rotation),radius=0;
                foreach(var shape in shapes) if(shape.collider)
                    radius=Mathf.Max(radius,Vector3.Distance(shape.collider.bounds.center,from.position)+shape.collider.bounds.extents.magnitude);
                int requested=Mathf.Max(1,Mathf.CeilToInt((distance+angle*Mathf.Deg2Rad*radius)/Step));
                float limit=Mathf.Min(1,64f/requested);
                var result=new Result { Pose=from,BudgetLimited=limit<1 };
                // Rotation uses conservative short arc steps, not straight chords between members.
                if(angle<.001f && distance>Skin)
                    foreach(var body in bodies) if(body)
                        foreach(var hit in body.Rigidbody.SweepTestAll(movement/distance,distance*limit,QueryTriggerInteraction.Ignore))
                            if(Blocker(hit.collider,blocks) && Vector3.Dot(movement,hit.normal)<-.000001f)
                            {
                                float fraction=Mathf.Max(0,hit.distance-Skin)/distance;
                                if(fraction<=limit) { limit=fraction; result.Contact=true; result.normal0=hit.normal; result.normalCount=1; }
                            }
                float safe=0; int steps=Mathf.Max(1,Mathf.CeilToInt(requested*limit));
                for(int step=1;step<=steps;step++)
                {
                    float t=limit*step/steps;
                    if(!Clear(Between(from,to,t),blocks,out var normal))
                    {
                        result.Contact=true; result.normal0=normal; result.normalCount=1;
                        float high=t;
                        for(int k=0;k<6;k++)
                        {
                            float mid=(safe+high)*.5f;
                            if(Clear(Between(from,to,mid),blocks,out _)) safe=mid; else high=mid;
                        }
                        break;
                    }
                    safe=t;
                }
                result.Pose=Between(from,to,safe);
                result.RotationBlocked=result.Contact && angle>.001f && safe<.99999f;
                result.Reached=safe>=.99999f;
                Place(result.Pose); return result;
            }
            public Result Move(Pose from,Pose to,Func<Collider,bool> blocks,bool slide)
            {
                var result=new Result { Pose=from }; var next=to;
                for(int pass=0;pass<(slide?3:1);pass++)
                {
                    var moved=Sweep(result.Pose,next,blocks); result.Pose=moved.Pose;
                    result.BudgetLimited|=moved.BudgetLimited; result.RotationBlocked|=moved.RotationBlocked;
                    if(!moved.Contact) break;
                    result.Contact=true; result.AddNormal(moved.normal0);
                    var remaining=result.ClipVelocity(to.position-result.Pose.position);
                    next=new Pose(result.Pose.position+remaining,result.RotationBlocked?result.Pose.rotation:to.rotation);
                    if(remaining.sqrMagnitude<1e-10f && Quaternion.Angle(next.rotation,result.Pose.rotation)<.001f) break;
                }
                result.Reached=Vector3.Distance(result.Pose.position,to.position)<.00001f && Quaternion.Angle(result.Pose.rotation,to.rotation)<.01f;
                return result;
            }
        }

        // Alignment shares the blocker policy but does not have a hand spring.
        public static bool Move(AssemblyBody[] bodies,Vector3[] positions,Quaternion[] rotations,Func<Collider,bool> blocks=null)
        {
            if(bodies.Length==0) return true;
            var from=new Pose(bodies[0].Rigidbody.position,bodies[0].Rigidbody.rotation);
            return new Group(bodies,from).Move(from,new Pose(positions[0],rotations[0]),blocks,false).Reached;
        }
    }
}

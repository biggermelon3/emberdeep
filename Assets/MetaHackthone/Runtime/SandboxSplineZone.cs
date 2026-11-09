using System;
using System.Collections.Generic;
using UnityEngine;

namespace PhysicalAssembly
{
    public enum SandboxGuidanceMode { WholeRoad=0, ConnectionArea=1 }

    // Authored Level 2 uses the exact saved road path, without a second interpolation.
    public sealed class SandboxSplineZone : MonoBehaviour
    {
        public SandboxGuidanceMode GuidanceMode;
        [HideInInspector] public float TransferDuration=2; // Legacy scene data; travel now follows entry speed.
        [Min(0), Tooltip("Gentle acceleration along the connection, in course units per simulation second squared.")]
        public float TransferAcceleration=.35f;
        [Min(0), Tooltip("Zero uses the road width, in course units.")] public float GateWidth;
        public float GateBottom=-.25f;
        public float GateTop=2;
        public float EffectiveGateWidth => GateWidth>0?GateWidth:GetComponent<GravityGarage3DCourse>().Level.DeckWidth;
        public bool ConnectionArea => GuidanceMode==SandboxGuidanceMode.ConnectionArea;
        public float GuidanceStart => BezierTrack?BezierTrack.TurnDistance:GetComponent<GravityGarage3DCourse>().TurnStart;
        public float GuidanceEnd => BezierTrack?BezierTrack.LowerDistance:GetComponent<GravityGarage3DCourse>().LowerStart;
        public Collider[] Roads=Array.Empty<Collider>();
        public Transform[] Points=Array.Empty<Transform>();
        [HideInInspector] public Level2BezierTrack BezierTrack;
        [Tooltip("The car stops on the centerline nearest this marker.")]
        public Transform Stop;
        [HideInInspector] // Legacy serialized value; no longer propels the car.
        public float Speed=3;
        [Min(0), Tooltip("Rolling deceleration in course units per simulation second squared.")]
        public float RollingResistance=.035f;
        [Min(.01f)] public float BrakingDistance=1.5f;
        [Min(0)] public float CenteringDuration=.4f;
        public bool RequireDrillWall=true;
        public bool CompleteLevelOnArrival;
        readonly List<Vector3> samples=new();
        readonly List<float> distances=new();
        public float Length => distances.Count>0?distances[distances.Count-1]:0;
        public float StopDistance { get; private set; }

        public void RebuildPath()
        {
            samples.Clear();distances.Clear();
            if(BezierTrack) {
                foreach(var p in BezierTrack.Samples) Add(transform.InverseTransformPoint(BezierTrack.transform.TransformPoint(p)));
                StopDistance=Stop?Project(Stop.position).Distance:Length;
                return;
            }
            var nodes=new List<Vector3>();
            foreach(var point in Points) {
                if(!point) throw new InvalidOperationException("Spline zone contains a missing route point.");
                var p=transform.InverseTransformPoint(point.position);
                if(nodes.Count==0 || (nodes[nodes.Count-1]-p).sqrMagnitude>1e-8f) nodes.Add(p);
            }
            if(nodes.Count<2) throw new InvalidOperationException("Spline zone needs at least two different route points.");
            Add(nodes[0]);
            for(int i=0;i<nodes.Count-1;i++) for(int j=1;j<=8;j++) {
                var a=nodes[Mathf.Max(0,i-1)];var b=nodes[i];var c=nodes[i+1];var d=nodes[Mathf.Min(nodes.Count-1,i+2)];
                float t=j/8f;
                Add(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t));
            }
            StopDistance=Stop?Project(Stop.position).Distance:Length;
        }
        void Add(Vector3 point)
        {
            if(samples.Count>0 && (point-samples[samples.Count-1]).sqrMagnitude<1e-12f)return;
            distances.Add(samples.Count==0?0:Length+Vector3.Distance(samples[samples.Count-1],point));samples.Add(point);
        }
        // At a boundary select the segment in the direction of travel.
        public void Segment(float distance, int direction, out float start, out float end, out Vector3 tangent)
        {
            int i=distances.BinarySearch(distance);
            if(i<0)i=~i-1;else if(direction<0)i--;
            i=Mathf.Clamp(i,0,samples.Count-2);
            start=distances[i];end=distances[i+1];
            tangent=transform.TransformDirection((samples[i+1]-samples[i]).normalized);
        }
        public Garage3DSample Sample(float distance)
        {
            distance=Mathf.Clamp(distance,0,Length);
            int i=distances.BinarySearch(distance);if(i<0)i=~i-1;i=Mathf.Clamp(i,0,samples.Count-2);
            float t=(distance-distances[i])/(distances[i+1]-distances[i]);
            var tangent=transform.TransformDirection((samples[i+1]-samples[i]).normalized);
            return new Garage3DSample {Distance=distance,Position=transform.TransformPoint(Vector3.Lerp(samples[i],samples[i+1],t)),Tangent=tangent,Normal=Vector3.ProjectOnPlane(transform.up,tangent).normalized};
        }
        public Garage3DSample Project(Vector3 world)
        {
            var p=transform.InverseTransformPoint(world);float best=float.PositiveInfinity,distance=0;
            for(int i=0;i<samples.Count-1;i++) {
                var delta=samples[i+1]-samples[i];float t=Mathf.Clamp01(Vector3.Dot(p-samples[i],delta)/delta.sqrMagnitude);
                float error=(p-samples[i]-delta*t).sqrMagnitude;
                if(error<best){best=error;distance=Mathf.Lerp(distances[i],distances[i+1],t);}
            }
            return Sample(distance);
        }
        public bool CrossesGate(Vector3 from,Vector3 to,bool forward)
        {
            var s=Sample(forward?GuidanceStart:GuidanceEnd);
            var inverse=Quaternion.Inverse(Quaternion.LookRotation(s.Tangent,s.Normal));
            var a=inverse*(from-s.Position)/transform.lossyScale.x;
            var b=inverse*(to-s.Position)/transform.lossyScale.x;
            if(forward?!(a.z<=0 && b.z>0):!(a.z>=0 && b.z<0))return false;
            var hit=Vector3.Lerp(a,b,-a.z/(b.z-a.z));
            return Mathf.Abs(hit.x)<=EffectiveGateWidth*.5f && hit.y>=GateBottom && hit.y<=GateTop;
        }
        void DrawGate(float d)
        {
            var s=Sample(d);var old=Gizmos.matrix;
            Gizmos.matrix=Matrix4x4.TRS(s.Position,Quaternion.LookRotation(s.Tangent,s.Normal),Vector3.one*transform.lossyScale.x);
            Gizmos.DrawWireCube(new Vector3(0,(GateTop+GateBottom)*.5f,0),new Vector3(EffectiveGateWidth,GateTop-GateBottom,.02f));Gizmos.matrix=old;
        }
        void OnDrawGizmosSelected()
        {
            if(Points==null || Points.Length<2 || Array.Exists(Points,p=>!p))return;
            RebuildPath();Gizmos.color=Color.yellow;
            for(int i=1;i<samples.Count;i++) {Gizmos.color=ConnectionArea && (distances[i]<GuidanceStart || distances[i-1]>GuidanceEnd)?Color.gray:Color.yellow;Gizmos.DrawLine(transform.TransformPoint(samples[i-1]),transform.TransformPoint(samples[i]));}
            if(ConnectionArea){DrawGate(GuidanceStart);DrawGate(GuidanceEnd);return;}
            var end=Sample(StopDistance);Gizmos.DrawWireSphere(end.Position,.15f*transform.lossyScale.x);
            Gizmos.DrawLine(end.Position,end.Position+end.Tangent*.5f*transform.lossyScale.x);
        }
    }
}

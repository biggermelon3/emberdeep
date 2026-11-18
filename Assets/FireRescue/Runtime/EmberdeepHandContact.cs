using System.Collections.Generic;
using UnityEngine;

namespace FireRescue
{
    // Render-only contact constraints against the actual part transforms. Hollow
    // handles are collections of solids, never one filled whole-object AABB.
    public sealed class EmberdeepHandContact
    {
        readonly List<MeshFilter> parts=new();
        struct Shape {public MeshFilter Part;public Matrix4x4 ToLocal,ToWorld;public Bounds Local,World;public Vector3 InverseScale;}
        readonly List<Shape> shapes=new();
        bool dirty=true;
        public void Clear(){parts.Clear();shapes.Clear();dirty=true;}
        public void Add(Transform root)
        {if(root)parts.AddRange(root.GetComponentsInChildren<MeshFilter>(true));dirty=true;}
        public void Prepare()
        {
            shapes.Clear();dirty=false;
            foreach(var part in parts)
            {
                if(!Active(part))continue;
                var t=part.transform;var scale=t.lossyScale;
                if(Mathf.Min(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z))<1e-5f)continue;
                var b=part.sharedMesh.bounds;var m=t.localToWorldMatrix;var e=b.extents;
                var worldExtents=new Vector3(Mathf.Abs(m.m00)*e.x+Mathf.Abs(m.m01)*e.y+Mathf.Abs(m.m02)*e.z,
                    Mathf.Abs(m.m10)*e.x+Mathf.Abs(m.m11)*e.y+Mathf.Abs(m.m12)*e.z,
                    Mathf.Abs(m.m20)*e.x+Mathf.Abs(m.m21)*e.y+Mathf.Abs(m.m22)*e.z);
                shapes.Add(new Shape{Part=part,ToLocal=t.worldToLocalMatrix,ToWorld=m,Local=b,
                    World=new Bounds(m.MultiplyPoint3x4(b.center),worldExtents*2),
                    InverseScale=new Vector3(1/Mathf.Abs(scale.x),1/Mathf.Abs(scale.y),1/Mathf.Abs(scale.z))});
            }
        }
        public static void Surface(MeshFilter mesh,Vector3 world,out Vector3 point,out Vector3 normal)
        {
            var t=mesh.transform;var b=mesh.sharedMesh.bounds;var p=t.InverseTransformPoint(world);
            var q=b.ClosestPoint(p);var delta=p-q;
            if(delta.sqrMagnitude>1e-10f)normal=t.TransformDirection(delta.normalized).normalized;
            else
            {
                var edge=b.extents;var local=p-b.center;
                var gap=edge-new Vector3(Mathf.Abs(local.x),Mathf.Abs(local.y),Mathf.Abs(local.z));
                int axis=gap.x<gap.y?(gap.x<gap.z?0:2):(gap.y<gap.z?1:2);
                var n=Vector3.zero;n[axis]=local[axis]<0?-1:1;
                q=p;q[axis]=b.center[axis]+n[axis]*edge[axis];normal=t.TransformDirection(n).normalized;
            }
            point=t.TransformPoint(q);
        }
        static bool Active(MeshFilter p)=>p && p.sharedMesh && p.gameObject.activeInHierarchy && !p.name.StartsWith("Grab dot");
        public Vector3 Resolve(Vector3 position,float radius)
        {
            if(dirty)Prepare();
            for(int iteration=0;iteration<3;iteration++)foreach(var shape in shapes)
            {
                var broad=shape.World;broad.Expand(radius*2);
                if(!broad.Contains(position) || !Active(shape.Part))continue;
                var bounds=shape.Local;bounds.Expand(shape.InverseScale*(2*radius));
                var local=shape.ToLocal.MultiplyPoint3x4(position);if(!bounds.Contains(local))continue;
                var d=local-bounds.center;var e=bounds.extents;
                var gap=Vector3.Scale(e-new Vector3(Mathf.Abs(d.x),Mathf.Abs(d.y),Mathf.Abs(d.z)),
                    new Vector3(1/shape.InverseScale.x,1/shape.InverseScale.y,1/shape.InverseScale.z));
                int axis=gap.x<gap.y?(gap.x<gap.z?0:2):(gap.y<gap.z?1:2);
                local[axis]=bounds.center[axis]+(d[axis]<0?-1:1)*(e[axis]+.0002f*shape.InverseScale[axis]);
                position=shape.ToWorld.MultiplyPoint3x4(local);
            }
            return position;
        }
        public Vector3 Sweep(Vector3 from,Vector3 to,float radius)
        {
            if(dirty)Prepare();
            Vector3 result=to;
            foreach(var shape in shapes)
            {
                if(!Active(shape.Part))continue;
                var b=shape.Local;b.Expand(shape.InverseScale*(2*radius));
                var a=shape.ToLocal.MultiplyPoint3x4(from);var delta=shape.ToLocal.MultiplyPoint3x4(result)-a;
                if(b.Contains(a) || delta.sqrMagnitude<1e-10f)continue;
                if(b.IntersectRay(new Ray(a,delta.normalized),out float distance) && distance<=delta.magnitude)
                    result=shape.ToWorld.MultiplyPoint3x4(a+delta.normalized*Mathf.Max(0,distance-.001f));
            }
            return Resolve(result,radius);
        }
    }
}

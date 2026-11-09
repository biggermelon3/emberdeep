using System.Collections.Generic;
using System;
using UnityEngine;

namespace PhysicalAssembly
{
    public interface ISandboxDrillTarget
    {
        bool DrillActive(AssemblyPart part);
        void SweepDrill(AssemblyPart part,Vector3 from,Vector3 to,float radius);
    }

    // Shared swept water query, independent of either course runner or visual meshes.
    public sealed class SandboxWaterContactHistory
    {
        readonly Dictionary<Collider,Vector3> previous=new();
        public void Reset()=>previous.Clear();
        public bool Check(IEnumerable<Collider> colliders,Vector3 up,Func<Vector3,Vector3> coordinates,Bounds volume,out Vector3 hit)
            =>Check(colliders,up,coordinates,new[]{volume},out hit);
        public bool Check(IEnumerable<Collider> colliders,Vector3 up,Func<Vector3,Vector3> coordinates,IEnumerable<Bounds> volumes,out Vector3 hit)
        {
            hit=default;
            foreach(var collider in colliders){if(!collider||!collider.enabled||collider.isTrigger||!collider.gameObject.activeInHierarchy)continue;var bounds=collider.bounds;var p=coordinates(collider.ClosestPoint(bounds.center-up*(bounds.size.magnitude+1)));if(!previous.TryGetValue(collider,out var last))last=p;previous[collider]=p;foreach(var volume in volumes)if(Crosses(last,p,volume,out hit))return true;}
            return false;
        }
        public static bool Crosses(Vector3 a,Vector3 b,Bounds volume,out Vector3 hit)
        {
            float enter=0,exit=1;var delta=b-a;
            for(int axis=0;axis<3;axis++){if(Mathf.Abs(delta[axis])<.000001f){if(a[axis]<volume.min[axis]||a[axis]>volume.max[axis]){hit=default;return false;}continue;}float low=(volume.min[axis]-a[axis])/delta[axis],high=(volume.max[axis]-a[axis])/delta[axis];if(low>high)(low,high)=(high,low);enter=Mathf.Max(enter,low);exit=Mathf.Min(exit,high);if(enter>exit){hit=default;return false;}}
            hit=a+delta*enter;return true;
        }
    }
}

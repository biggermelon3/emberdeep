using UnityEngine;

namespace PhysicalAssembly
{
    // One stable drag plane and grab offset per pointer, shared by mouse and XR.
    public sealed class SandboxPlaneDrag
    {
        Plane plane;
        Pose last;
        public void Begin(Vector3 point,Vector3 up,Quaternion rotation) { plane=new Plane(up,point); last=new Pose(point,rotation); }
        public Pose Sample(Ray ray) { if(plane.Raycast(ray,out float distance) && distance>0 && distance<20) last.position=ray.GetPoint(distance); return last; }
    }
}

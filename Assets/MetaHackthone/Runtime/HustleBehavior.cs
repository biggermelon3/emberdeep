using System.Collections.Generic;
using UnityEngine;
namespace PhysicalAssembly
{
    // Keys and tangents transcribed from UnityPortDoc/PhysicalAssemblySystem-Audit.md.
    public static class HustleBehavior
    {
        public static AnimationCurve ControlTorqueCurve() => new(
            new Keyframe(-5972.700195f,25.000021f,0,.002862f),
            new Keyframe(-2377.600098f,75,.027975f,.027975f),
            new Keyframe(-1176.900024f,125.000008f,.078652f,.078652f),
            new Keyframe(-577.599976f,250.000015f,.357272f,.357272f),
            new Keyframe(-.7f,350,0,0),
            new Keyframe(605.5f,250.000015f,-.319646f,-.319646f),
            new Keyframe(1195.800049f,125.000008f,-.071873f,-.071873f),
            new Keyframe(2402.300049f,75,-.025177f,-.025177f),
            new Keyframe(6009,25.000021f,-.005780f,0));
        public static readonly AnimationCurve GlueCurve=new(
            new Keyframe(0,0,5.717333f,5.717336f),
            new Keyframe(.049f,.488308f,7.409317f,7.409308f),new Keyframe(.25f,1,0,0));
        static readonly Vector3[] directions=BuildDirections();
        public static Vector3 FromUnrealDirection(Vector3 v) => new(v.y,v.z,v.x);
        static Vector3[] BuildDirections()
        {
            var x=Vector3.right; var y=Vector3.up; var z=Vector3.forward;
            var first=new List<Vector3>{x,z,y,(x+z).normalized,(z+y).normalized,(x+y).normalized,(z+(x+y).normalized).normalized};
            var baseSet=first.ToArray(); foreach(var d in baseSet) Add(first,-d);
            var second=new List<Vector3>(); foreach(var d in first) { Add(second,Quaternion.AngleAxis(90,Vector3.forward)*d); Add(second,d); }
            var third=new List<Vector3>(); foreach(var d in second) { Add(third,Quaternion.AngleAxis(180,Vector3.forward)*d); Add(third,d); }
            for(int i=0;i<third.Count;i++) third[i]=FromUnrealDirection(third[i]).normalized;
            return third.ToArray();
        }
        static void Add(List<Vector3> list,Vector3 value) { foreach(var v in list) if((v-value).sqrMagnitude<1e-8f) return; list.Add(value); }
        public static Quaternion SnapRotation(Quaternion current,Quaternion reference)
        {
            Vector3 forward=current*Vector3.forward; Vector3 selected=forward; float smallest=float.MaxValue;
            foreach(var d in directions) { var v=reference*d; float angle=Vector3.Angle(forward,v); if(angle<smallest) { smallest=angle; selected=v; } }
            Quaternion best=current; smallest=float.MaxValue;
            foreach(var right in directions) foreach(var up in directions)
            {
                var r=reference*right; var u=reference*up;
                if(Mathf.Abs(Vector3.Angle(selected,r)-90)>1 || Mathf.Abs(Vector3.Angle(selected,u)-90)>1 || Mathf.Abs(Vector3.Angle(r,u)-90)>1 || Vector3.Dot(Vector3.Cross(u,selected),r)<.99f) continue;
                var candidate=Quaternion.LookRotation(selected,u); float angle=Quaternion.Angle(current,candidate);
                if(angle<smallest) { smallest=angle; best=candidate; }
            }
            return best;
        }
    }
}

using System;
using UnityEngine;

namespace PhysicalAssembly
{
    // One dimensional constrained motion. Exact constant-acceleration integration on each
    // baked line segment preserves energy across crests, reversals and sample boundaries.
    public sealed class SandboxTrackMomentum
    {
        public float Distance { get; private set; }
        public float Speed { get; private set; }
        public int ExitDirection { get; private set; }
        public bool Arrived { get; private set; }
        public SandboxTrackMomentum(float distance,float speed) { Distance=distance;Speed=speed; }
        public void Tick(SandboxSplineZone zone,Vector3 gravity,float dt,float minimum=0,float maximum=-1,bool releaseAtBoundary=false)
        {
            float limit=maximum<0?zone.StopDistance:maximum;
            if(Arrived || dt<=0)return;
            double remainingTime=dt;
            for(int step=0;step<100000 && remainingTime>1e-9;step++) {
                int direction=Speed<0?-1:1;
                zone.Segment(Distance,direction,out float start,out float end,out var tangent);
                double g=Vector3.Dot(gravity,tangent), resistance=Math.Max(0,zone.RollingResistance);
                if(Math.Abs(Speed)<1e-7) {
                    Speed=0;
                    if(Math.Abs(g)<=resistance)return;
                    direction=g<0?-1:1;
                    zone.Segment(Distance,direction,out start,out end,out tangent);
                    g=Vector3.Dot(gravity,tangent);
                    if(g*direction<=resistance)return; // Rest at a valley corner.
                }
                if(Distance<=minimum && direction<0){Distance=minimum;if(releaseAtBoundary){Arrived=true;ExitDirection=-1;}else Speed=0;return;}
                double v=Speed,a=g-direction*resistance;
                float boundary=direction>0?Mathf.Min(end,limit):Mathf.Max(minimum,start);
                float brakeStart=Mathf.Max(0,zone.StopDistance-zone.BrakingDistance);
                if(!releaseAtBoundary && direction>0 && zone.BrakingDistance>0) {
                    if(Distance<brakeStart)boundary=Mathf.Min(boundary,brakeStart);
                    else if(v>0 && zone.StopDistance>Distance)
                        a=Math.Min(a,-v*v/(2*(zone.StopDistance-Distance)));
                }
                double travel=(boundary-Distance)*direction,u=v*direction,acc=a*direction;
                double stopTime=acc<0?-u/acc:double.PositiveInfinity;
                double discriminant=u*u+2*acc*travel;
                double hitTime=double.PositiveInfinity;
                if(travel<=0)hitTime=0;
                else if(discriminant>=-1e-8 && u+Math.Sqrt(Math.Max(0,discriminant))>0)
                    hitTime=2*travel/(u+Math.Sqrt(Math.Max(0,discriminant)));
                double t=Math.Min(remainingTime,Math.Min(stopTime,hitTime));
                Distance=(float)(Distance+v*t+.5*a*t*t);
                Speed=(float)(v+a*t);remainingTime-=t;
                if(hitTime<=t+1e-9) {
                    Distance=boundary;
                    if(direction>0 && Distance>=limit){if(!releaseAtBoundary)Speed=0;Arrived=true;ExitDirection=1;return;}
                    if(direction<0 && Distance<=minimum){if(releaseAtBoundary){Arrived=true;ExitDirection=-1;}else Speed=0;return;}
                }
                if(stopTime<=t+1e-9)Speed=0;
                if(t==0 && hitTime>0)return;
            }
        }
    }
}

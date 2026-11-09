using System;
using UnityEngine;

namespace FireRescue
{
    // World-space SI units. Rendered droplets and damage use these same moving packets.
    public sealed class WaterJet
    {
        public struct Packet { public Vector3 Position, Velocity; public float Amount, Age, Distance, Range, Radius, Wetness; public FireDelivery Delivery; }
        public struct Impact { public Vector3 Point, Normal; public float Amount, Radius, Wetness; public int Tree; public FireDelivery Delivery; }
        public readonly Packet[] Packets = new Packet[256];
        public int Count { get; private set; }
        int pattern;
        public static readonly Vector3 Gravity = new Vector3(0,-9.81f,0);
        public void Clear() => Count=0;

        public static Vector3 MouseTarget(Ray ray,Transform arena,Vector3[] targets)
        {
            float nearest=float.PositiveInfinity;Vector3 target=default;
            foreach(var center in targets)
            {
                var offset=ray.origin-center;float b=Vector3.Dot(offset,ray.direction);
                float d=b*b-offset.sqrMagnitude+.038f*.038f;if(d<0)continue;
                float t=-b-Mathf.Sqrt(d);if(t<0 || t>=nearest)continue;
                nearest=t;target=center;
            }
            if(!float.IsPositiveInfinity(nearest))return target;
            var plane=new Plane(arena.up,arena.TransformPoint(Vector3.up*.7f));
            return plane.Raycast(ray,out float distance)?ray.GetPoint(distance):ray.GetPoint(1);
        }

        public static Vector3 AimVelocity(Vector3 from, Vector3 to, float speed)
        {
            var delta=to-from;var horizontal=Vector3.ProjectOnPlane(delta,Vector3.up);float x=horizontal.magnitude;
            float square=speed*speed, discriminant=square*square-9.81f*(9.81f*x*x+2*delta.y*square);
            if(x<.001f || discriminant<0)return delta.normalized*speed;
            float angle=Mathf.Atan((square-Mathf.Sqrt(discriminant))/(9.81f*x));
            return (horizontal/x*Mathf.Cos(angle)+Vector3.up*Mathf.Sin(angle))*speed;
        }
        // Solve the trajectory and the rotating physical muzzle together. Repeatedly
        // aiming from a previous muzzle position oscillates when a long barrel is
        // close to its target; solving flight time avoids that fixed-point failure.
        public static Vector3 AimFromGrip(Vector3 grip,Vector3 target,float speed,Vector3 muzzleOffset)
        {
            var delta=target-grip;var flat=Vector3.ProjectOnPlane(delta,Vector3.up);
            float horizontal=flat.magnitude;
            if(speed<=0 || float.IsNaN(speed) || float.IsInfinity(speed) || horizontal<Mathf.Abs(muzzleOffset.x))
                return AimVelocity(grip,target,speed);
            float forwardDistance=Mathf.Sqrt(Mathf.Max(0,horizontal*horizontal-muzzleOffset.x*muzzleOffset.x));
            var flatDirection=horizontal>.00001f?flat/horizontal:Vector3.forward;
            if(Mathf.Abs(muzzleOffset.x)>.00001f)
                flatDirection=Quaternion.AngleAxis(-Mathf.Atan2(muzzleOffset.x,forwardDistance)*Mathf.Rad2Deg,Vector3.up)*flatDirection;

            // In the barrel's vertical plane, target + gravity compensation is
            // a rotated vector (muzzle forward + speed*time, muzzle up).
            float low=0,previous=FlightEquation(0,forwardDistance,delta.y,speed,muzzleOffset);
            float flight=0;bool found=Mathf.Abs(previous)<.0000001f;
            for(int sample=1;sample<=150 && !found;sample++)
            {
                float high=sample*.01f;
                float value=FlightEquation(high,forwardDistance,delta.y,speed,muzzleOffset);
                if((previous>0 && value<=0) || (previous<0 && value>=0))
                {
                    for(int iteration=0;iteration<22;iteration++)
                    {
                        float middle=(low+high)*.5f;
                        float atMiddle=FlightEquation(middle,forwardDistance,delta.y,speed,muzzleOffset);
                        if((previous>0 && atMiddle>0) || (previous<0 && atMiddle<0)){low=middle;previous=atMiddle;}
                        else high=middle;
                    }
                    flight=(low+high)*.5f;found=true;
                }
                else{low=high;previous=value;}
            }
            if(!found)return AimVelocity(grip,target,speed);
            float elevation=Mathf.Atan2(delta.y+4.905f*flight*flight,forwardDistance)
                -Mathf.Atan2(muzzleOffset.y,muzzleOffset.z+speed*flight);
            return (flatDirection*Mathf.Cos(elevation)+Vector3.up*Mathf.Sin(elevation))*speed;
        }
        static float FlightEquation(float time,float horizontal,float vertical,float speed,Vector3 offset)
        {
            float lifted=vertical+4.905f*time*time,travel=offset.z+speed*time;
            return horizontal*horizontal+lifted*lifted-travel*travel-offset.y*offset.y;
        }
        public void Step(float dt,Vector3 origin,Vector3 velocity,float amount,float range,Transform arena,Vector3[] targets,Action<Impact> impact,float radius=.82f,float wetness=1,float spread=0,FireDelivery delivery=FireDelivery.Pulse)
        {
            dt=Mathf.Clamp(dt,0,.1f);
            for(int i=Count-1;i>=0;i--)if(!Advance(ref Packets[i],dt,arena,targets,impact))Packets[i]=Packets[--Count];
            if(amount<=0 || dt<=0)return;
            int n=Mathf.Min(delivery==FireDelivery.Charge?1:Mathf.CeilToInt(dt*160),Packets.Length-Count);
            for(int i=0;i<n;i++)
            {
                var side=Vector3.Cross(velocity.normalized,Vector3.up).normalized;
                var axis=Vector3.Cross(side,velocity.normalized).normalized;
                float angle=((pattern++%5)-2)*spread*.5f;
                var packet=new Packet{Position=origin,Velocity=Quaternion.AngleAxis(angle,axis)*velocity,Amount=amount/n,Range=range,Radius=radius,Wetness=wetness,Delivery=delivery};
                if(Advance(ref packet,dt*(i+.5f)/n,arena,targets,impact))Packets[Count++]=packet;
            }
        }
        static bool Advance(ref Packet packet,float dt,Transform arena,Vector3[] targets,Action<Impact> callback)
        {
            while(dt>0)
            {
                float step=Mathf.Min(.008f,dt);dt-=step;
                Vector3 next=packet.Position+packet.Velocity*step+Gravity*(.5f*step*step);
                float length=Vector3.Distance(packet.Position,next);
                if(packet.Distance+length>packet.Range || packet.Age>1.5f)return false;
                if(Segment(packet.Position,next,arena,targets,out var hit))
                { hit.Amount=packet.Amount;hit.Radius=packet.Radius;hit.Wetness=packet.Wetness;hit.Delivery=packet.Delivery;callback?.Invoke(hit);return false; }
                packet.Position=next;packet.Velocity+=Gravity*step;packet.Age+=step;packet.Distance+=length;
            }
            return true;
        }
        public static bool Predict(Vector3 from,Vector3 velocity,float range,Transform arena,Vector3[] targets,out Impact hit)
        {
            float distance=0;hit=default;
            for(int i=0;i<180;i++)
            {
                var next=from+velocity*.008f+Gravity*(.5f*.008f*.008f);distance+=Vector3.Distance(from,next);
                if(distance>range)return false;
                if(Segment(from,next,arena,targets,out hit))return true;
                from=next;velocity+=Gravity*.008f;
            }
            return false;
        }
        static bool Segment(Vector3 from,Vector3 to,Transform arena,Vector3[] targets,out Impact hit)
        {
            hit=default;float best=2;var delta=to-from;float a=delta.sqrMagnitude;
            if(a<1e-12f)return false;
            for(int i=0;i<targets.Length;i++)
            {
                var offset=from-targets[i];float b=Vector3.Dot(offset,delta),c=offset.sqrMagnitude-.038f*.038f;
                float discriminant=b*b-a*c;if(discriminant<0)continue;
                float t=c<=0?0:(-b-Mathf.Sqrt(discriminant))/a;
                if(t<0 || t>1 || t>=best)continue;
                best=t;hit.Point=from+delta*t;hit.Normal=(hit.Point-targets[i]).normalized;hit.Tree=i;
            }
            var p=arena.InverseTransformPoint(from);var q=arena.InverseTransformPoint(to);
            if(p.y>=0 && q.y<=0)
            {
                float t=p.y/(p.y-q.y);var point=Vector3.Lerp(p,q,t);
                if(t<best && Mathf.Abs(point.x)<=5.5f && point.z>=-.5f && point.z<=12.5f)
                { best=t;hit.Point=Vector3.Lerp(from,to,t);hit.Normal=arena.up;hit.Tree=-1; }
            }
            return best<=1;
        }
    }
}

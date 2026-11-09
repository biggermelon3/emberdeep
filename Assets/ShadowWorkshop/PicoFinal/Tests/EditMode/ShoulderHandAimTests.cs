using CloudGarden.Integration;
using NUnit.Framework;
using UnityEngine;

namespace ShapeOfMemory.Tests
{
    public class ShoulderHandAimTests
    {
        // A hand held still with tracking noise: the filtered aim moves far less than the raw one; a deliberate sweep still
        // arrives within a tenth of a second.
        [Test] public void OneEuroSteadiesAStillHandAndFollowsASweep()
        {
            var filter=new OneEuro(.8f,1.5f);var random=new System.Random(4);const float dt=1/72f;
            var rest=Vector3.forward;float raw=0,smoothed=0;Vector3 last=filter.Filter(rest,dt),lastRaw=rest;
            for(int i=0;i<300;i++)
            {
                var noisy=(rest+new Vector3((float)random.NextDouble()-.5f,(float)random.NextDouble()-.5f,0)*.02f).normalized;
                var output=filter.Filter(noisy,dt);raw+=Vector3.Angle(noisy,lastRaw);smoothed+=Vector3.Angle(output,last);lastRaw=noisy;last=output;
            }
            Assert.Less(smoothed,raw/4,$"jitter {smoothed:0.0} vs raw {raw:0.0} degrees");
            var target=Quaternion.AngleAxis(40,Vector3.up)*rest;Vector3 aim=last;
            for(int i=0;i<36;i++)aim=filter.Filter(Vector3.Slerp(rest,target,Mathf.Clamp01(i/18f)),dt);
            for(int i=0;i<7;i++)aim=filter.Filter(target,dt);
            Assert.Less(Vector3.Angle(aim,target),3,"a sweep lands within 0.1 s of stopping");
        }
        // The ray runs from the shoulder through the hand: tilting the head does not move it, only the hand does.
        [Test] public void PointingComesFromTheShoulderNotTheHead()
        {
            var head=new GameObject("Head").transform;
            try
            {
                head.SetPositionAndRotation(new Vector3(0,1.6f,0),Quaternion.identity);
                var hand=new Vector3(.25f,1.25f,.45f);var level=ShoulderHandAim.Pointing(head,false,hand);
                var shoulder=new Vector3(.16f,1.46f,-.04f);Assert.Less(Vector3.Angle(level,hand-shoulder),.01f);
                head.rotation=Quaternion.Euler(35,0,0);Assert.Less(Vector3.Angle(level,ShoulderHandAim.Pointing(head,false,hand)),.01f,"looking down");
                Assert.Greater(ShoulderHandAim.Pointing(head,true,hand).x,level.x,"the left shoulder is on the other side");
            }
            finally{Object.DestroyImmediate(head.gameObject);}
        }
    }
}

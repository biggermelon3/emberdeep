using UnityEngine;

namespace FireRescue
{
    // Explicit two-hand spatial gesture. No pinch, pointer or rendered hand feeds this state.
    public sealed class EmberdeepPalmGesture
    {
        public bool Aiming {get;private set;}
        public bool Casting {get;private set;}
        public Vector3 Origin {get;private set;}
        public Vector3 Target {get;private set;}
        float dwell;
        public void Reset(){Aiming=Casting=false;dwell=0;}
        public void Sample(FireToolManipulation.Hand left,Vector3[] rightTips,int validTips,bool fingersOpen,
            bool allowed,Transform arena,float range,float dt)
        {
            if(!allowed || !left.Usable || !fingersOpen || !arena || !float.IsFinite(dt) || dt<=0){Reset();return;}
            Vector3 normal=left.GripRotation*Vector3.up;
            Origin=left.Contact+normal*.018f;
            var ray=new Ray(Origin,normal);
            var ground=new Plane(arena.up,arena.position);
            if(!ground.Raycast(ray,out float distance) || distance<.04f || distance>range){Reset();return;}
            var target=ray.GetPoint(distance);var local=arena.InverseTransformPoint(target);
            if(Mathf.Abs(local.x)>5.5f || local.z<1.6f || local.z>12){Reset();return;}
            Target=target;Aiming=true;
            bool touching=false;float margin=Casting?.012f:0;
            if(rightTips!=null)for(int i=0;i<Mathf.Min(5,rightTips.Length);i++)
            {
                if((validTips&(1<<i))==0 || !float.IsFinite(rightTips[i].sqrMagnitude))continue;
                var delta=Quaternion.Inverse(left.GripRotation)*(rightTips[i]-left.Contact);
                // Dorsal contact only; the right palm can stay away from the left fingers.
                if(delta.y>=-.042f-margin && delta.y<=-.004f+margin*.25f
                    && Mathf.Abs(delta.x)<.042f+margin && Mathf.Abs(delta.z)<.048f+margin){touching=true;break;}
            }
            dwell=touching?dwell+Mathf.Min(dt,.1f):0;Casting=touching && dwell>=.10f;
        }
    }
}

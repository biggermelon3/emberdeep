using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;
using Hand = Oculus.Interaction.Input.Hand;

namespace CloudGarden.Integration
{
    // A hand ray's aim from an estimated shoulder through the hand, the way Meta's own aim works: you point with the arm,
    // not the wrist. It starts between the index and thumb knuckles, which barely move as a pinch closes. A One Euro
    // filter steadies it: heavy smoothing while the hand is nearly still, little while it sweeps. As the fingers close
    // for a pinch the ray slows almost to a stop, so the pinch itself does not knock it off the button.
    // Used when the runtime has no aim of its own (a mocked pose) or, with force, always: Pico's own aim jitters and
    // follows the wrist. It sits on the ray's HandPointerPose object and runs after it on every hand update.
    public sealed class ShoulderHandAim : MonoBehaviour
    {
        const float ShoulderDrop=.14f,ShoulderWidth=.16f,ShoulderBack=.04f;
        // Pinch span (index tip to thumb tip) where the ray starts to slow, and where it is all but held.
        const float SlowFrom=.065f,HeldAt=.03f;
        public bool Overriding{get;private set;}
        public bool Force;
        IHand hand;
        Transform head;
        OneEuro origin=new OneEuro(1.2f,3f),direction=new OneEuro(.8f,1.5f);
        Vector3 lastDirection;

        public static ShoulderHandAim Attach(HandPointerPose pointer,Transform head,bool force)
        {
            var aim=pointer.GetComponent<ShoulderHandAim>();
            if(!aim&&pointer.Hand!=null){aim=pointer.gameObject.AddComponent<ShoulderHandAim>();aim.hand=pointer.Hand;aim.head=head;aim.Subscribe();}
            if(aim)aim.Force=force;
            return aim;
        }
        void Subscribe(){if(hand!=null&&isActiveAndEnabled)hand.WhenHandUpdated+=Aim;}
        void OnEnable()=>Subscribe();
        void OnDisable(){if(hand!=null)hand.WhenHandUpdated-=Aim;}
        static bool Synthetic(IHand hand)=>hand is Hand data&&data.GetData().PointerPoseOrigin==PoseOrigin.SyntheticPose;
        void Aim()
        {
            Overriding=head&&(Force||Synthetic(hand));
            if(!Overriding||!hand.GetJointPose(HandJointId.HandIndex1,out var index)||!hand.GetJointPose(HandJointId.HandThumb2,out var thumb))
            {origin.Reset();direction.Reset();lastDirection=Vector3.zero;return;}
            float dt=Mathf.Max(Time.unscaledDeltaTime,1e-4f);
            var at=origin.Filter(Vector3.Lerp(index.position,thumb.position,.5f),dt);
            var wanted=Pointing(head,hand.Handedness==Handedness.Left,at);
            // Closing fingers: the ray follows the filter ever more slowly, so it all but holds where it was when the pinch began.
            float hold=hand.GetJointPose(HandJointId.HandIndexTip,out var tip)&&hand.GetJointPose(HandJointId.HandThumbTip,out var thumbTip)
                ?Mathf.InverseLerp(SlowFrom,HeldAt,Vector3.Distance(tip.position,thumbTip.position)):0;
            var filtered=direction.Filter(wanted,dt);if(filtered.sqrMagnitude<1e-6f)filtered=wanted;filtered.Normalize();
            lastDirection=lastDirection.sqrMagnitude>0?Vector3.Slerp(lastDirection,filtered,1-.96f*hold).normalized:filtered;
            transform.SetPositionAndRotation(at,Quaternion.LookRotation(lastDirection,Vector3.up));
        }
        // From an estimated shoulder (below and beside the head, a little behind it) through the hand.
        public static Vector3 Pointing(Transform head,bool left,Vector3 hand)
        {
            var flat=Vector3.ProjectOnPlane(head.forward,Vector3.up);if(flat.sqrMagnitude<1e-4f)flat=Vector3.forward;flat.Normalize();
            var right=Vector3.Cross(Vector3.up,flat);
            var shoulder=head.position+Vector3.down*ShoulderDrop+right*(left?-ShoulderWidth:ShoulderWidth)-flat*ShoulderBack;
            var direction=hand-shoulder;return direction.sqrMagnitude<1e-6f?flat:direction.normalized;
        }
    }

    // One Euro filter (Casiez, Roussel and Vogel, 2012) on a vector: the cutoff rises with speed, so slow movement is
    // smoothed hard and fast movement follows closely. minCutoff in Hz; beta per unit of speed.
    public sealed class OneEuro
    {
        readonly float minCutoff,beta,derivativeCutoff;
        Vector3 value,speed;
        bool started;
        public OneEuro(float minCutoff,float beta,float derivativeCutoff=1){this.minCutoff=minCutoff;this.beta=beta;this.derivativeCutoff=derivativeCutoff;}
        static float Alpha(float cutoff,float dt){float tau=1/(2*Mathf.PI*cutoff);return 1/(1+tau/dt);}
        public Vector3 Filter(Vector3 x,float dt)
        {
            if(!started){started=true;value=x;speed=Vector3.zero;return x;}
            speed=Vector3.Lerp(speed,(x-value)/dt,Alpha(derivativeCutoff,dt));
            value=Vector3.Lerp(value,x,Alpha(minCutoff+beta*speed.magnitude,dt));
            return value;
        }
        public void Reset()=>started=false;
    }
}

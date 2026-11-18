using UnityEngine;
namespace FireRescue
{
    // Near-hand contact only. No collision callbacks, remote grabs or currency writes.
    public sealed class EmberdeepSheepInteraction
    {
        readonly int[] held={-1,-1},candidate={-1,-1};
        readonly bool[] armed=new bool[2];
        readonly float[] dwell=new float[2],lost=new float[2],petDwell=new float[2];
        readonly Vector3[] offset=new Vector3[2];
        FireRun current;
        public bool OwnsHand(int hand)=>held[hand]>=0;
        public int HeldAnimal(int hand)=>held[hand];
        public static Vector3 Center(FireRun.Animal a)=>a.Position+Vector3.up*.34f;
        static bool Available(FireRun.Animal a)=>a.State==AnimalState.Waiting || a.State==AnimalState.Running || a.State==AnimalState.Celebrating;
        public void Reset()
        {
            for(int h=0;h<2;h++){Release(h);armed[h]=false;candidate[h]=-1;dwell[h]=petDwell[h]=lost[h]=0;}
        }
        void Release(int hand)
        {
            if(current!=null && held[hand]>=0 && held[hand]<current.Animals.Length)
            {
                var a=current.Animals[held[hand]];a.HeldBy=-1;
                a.Landing=new Vector3(Mathf.Clamp(a.Position.x,-5.2f,5.2f),0,Mathf.Clamp(a.Position.z,2.65f,12));
                a.ReturningToGround=true;a.Home=a.Landing;a.Refuge=new Vector3(a.Landing.x,0,2.65f);
            }
            held[hand]=-1;dwell[hand]=lost[hand]=0;
        }
        public void Step(FireRun run,Transform arena,FireToolManipulation.Hand left,FireToolManipulation.Hand right,float dt,Vector3? leftPet=null,Vector3? rightPet=null)
        {
            if(current!=run){Reset();current=run;}
            if(!arena || !float.IsFinite(dt) || dt<=0)return;dt=Mathf.Min(dt,.1f);
            for(int hand=0;hand<2;hand++)
            {
                var h=hand==0?left:right;
                if(held[hand]>=0)
                {
                    if(held[hand]>=run.Animals.Length || !Available(run.Animals[held[hand]]) || run.Animals[held[hand]].HeldBy!=hand){held[hand]=-1;continue;}
                    if(!h.Usable){lost[hand]+=dt;if(lost[hand]>.35f)Release(hand);continue;}
                    lost[hand]=0;
                    if(h.Closure<.20f){Release(hand);armed[hand]=true;continue;}
                    var a=run.Animals[held[hand]];
                    var target=arena.InverseTransformPoint(h.Contact+offset[hand])-Vector3.up*.34f;
                    target=new Vector3(Mathf.Clamp(target.x,-20,20),Mathf.Clamp(target.y,0,20),Mathf.Clamp(target.z,-15,25));
                    a.Position=Vector3.Lerp(a.Position,target,1-Mathf.Exp(-dt*24));a.Velocity=Vector3.zero;
                    continue;
                }
                if(!h.Usable){armed[hand]=false;candidate[hand]=-1;dwell[hand]=petDwell[hand]=0;continue;}
                if(h.Closure<.24f)armed[hand]=true;
                int nearest=-1;float best=.065f;
                for(int i=0;i<run.Animals.Length;i++)
                {
                    var a=run.Animals[i];if(!Available(a) || a.HeldBy>=0)continue;
                    var pet=hand==0?leftPet:rightPet;
                    float distance=Vector3.Distance(h.Contact,arena.TransformPoint(Center(a)));
                    if(h.Closure<.25f && pet.HasValue)distance=Mathf.Min(distance,Vector3.Distance(pet.Value,arena.TransformPoint(Center(a))));
                    if(distance<best){best=distance;nearest=i;}
                }
                if(candidate[hand]!=nearest){candidate[hand]=nearest;dwell[hand]=petDwell[hand]=0;}
                if(nearest<0)continue;
                var sheep=run.Animals[nearest];
                if(armed[hand] && h.Closure>=.42f)
                {
                    dwell[hand]+=dt;
                    if(dwell[hand]>=.08f)
                    {
                        held[hand]=nearest;sheep.HeldBy=hand;sheep.ReturningToGround=false;sheep.State=AnimalState.Waiting;sheep.StateTime=0;
                        offset[hand]=arena.TransformPoint(Center(sheep))-h.Contact;armed[hand]=false;
                    }
                }
                else dwell[hand]=0;
                if(h.Closure<.25f && best<.043f)
                {
                    petDwell[hand]+=dt;
                    if(petDwell[hand]>=.18f && sheep.PetCooldown<=0)
                    {sheep.Affection=1.5f;sheep.PetCooldown=2;petDwell[hand]=0;}
                }
                else petDwell[hand]=0;
            }
        }
    }
}

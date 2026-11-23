using UnityEngine;
namespace FireRescue
{
    // Near-hand contact only. No collision callbacks, remote grabs or currency writes.
    // Grab: Waiting/Running/Celebrating sheep. Pet: those plus Rescued sheep on the safe bank
    // (a rescued sheep is never grabbed: that would send it back to Waiting and pay its reward again).
    // A pet needs an open hand resting on the wool for PetDwell; the next pet on the same sheep needs
    // the cooldown AND either leaving the sheep or a stroke of PetStroke along it.
    // Headset test 2026-11-22: camp petting was unreliable. The radii are measured from the sheep's centre, so a camp sheep
    // (~2x a board sheep) sets Reach to its scale ratio; the camp also accepts a relaxed hand (PetClosure) since its
    // sheep are never grabbed; and a tracking or reach blip shorter than PetGrace no longer restarts a stroke.
    // Headset test 2: players tap a sheep with a fingertip (a pointing hand, touching for well under PetDwell). A tap is a
    // pet point (the fingertips the callers pass) entering the wool (PetRadius) after TapRearm outside it; it pets at once,
    // under the same rules (PetAllowed, cooldown, not held, not on fire). On a sheep that can be held, an open hand (still
    // arming a grab) or a palm within the pick radius is a grab coming, not a tap; and a tap disarms that hand's grab until it
    // opens again, so a tap never turns into a grab. Resting in the wool never re-taps: the tip has to leave first.
    public sealed class EmberdeepSheepInteraction
    {
        public const float PetDwell=.18f,PetRadius=.043f,PetStroke=.03f,PetGrace=.15f,BoardPetClosure=.25f;
        public const float TapRearm=.10f,GrabArmClosure=.24f,GrabClosure=.42f,PickRadius=.065f;
        // Pets that came from a tap (tests, tuning).
        public int Taps {get;private set;}
        // Sheep size against a board sheep (1 on the board). Scales the pick and pet radii.
        public float Reach=1;
        // Hand openness that still strokes (0 open .. 1 fist). The board keeps .25 so a closing hand grabs instead.
        public float PetClosure=BoardPetClosure;
        // Petting is off while false (the fireground: only once every fire is out). Grabbing is unaffected.
        public bool PetAllowed=true;
        readonly int[] held={-1,-1},candidate={-1,-1};
        readonly bool[] armed=new bool[2],petLatched=new bool[2],tapArmed=new bool[2];
        readonly float[] dwell=new float[2],lost=new float[2],petDwell=new float[2],stroke=new float[2],petGrace=new float[2],tapOut=new float[2];
        readonly Vector3[] offset=new Vector3[2],lastPet=new Vector3[2];
        FireRun current;
        public bool OwnsHand(int hand)=>held[hand]>=0;
        public int HeldAnimal(int hand)=>held[hand];
        // Raised once per pet with the sheep index, after Affection and PetCooldown are set.
        public event System.Action<int> Petted;
        public static Vector3 Center(FireRun.Animal a)=>a.Position+Vector3.up*.34f;
        static bool Available(FireRun.Animal a)=>FireRun.CanHold(a);
        public void Reset()
        {
            // A tip already in the wool when tracking (re)starts has to leave it before it can tap.
            for(int h=0;h<2;h++){Release(h);armed[h]=false;candidate[h]=-1;dwell[h]=petDwell[h]=lost[h]=stroke[h]=petGrace[h]=tapOut[h]=0;petLatched[h]=tapArmed[h]=false;}
        }
        // Applies a pet to one sheep if its cooldown allows. Shared by hands and scripted callers.
        public static bool TryPet(FireRun.Animal a)
        {
            if(a==null || !FireRun.CanPet(a) || a.HeldBy>=0 || a.PetCooldown>0 || a.OnFire)return false;
            a.Affection=FireRun.PetAffection;a.PetCooldown=FireRun.PetCooldownSeconds;return true;
        }
        void Release(int hand)
        {
            if(current!=null && held[hand]>=0 && held[hand]<current.Animals.Length)
            {
                var a=current.Animals[held[hand]];a.HeldBy=-1;
                // Authored board z, mapped onto the compact board (LoopSpec §5 Reach) by BoardZ.
                float refuge=current.BoardZ(2.65f);
                a.Landing=new Vector3(Mathf.Clamp(a.Position.x,-5.2f,5.2f),0,Mathf.Clamp(a.Position.z,refuge,current.BoardZ(12)));
                a.ReturningToGround=true;a.Home=a.Landing;a.Refuge=new Vector3(a.Landing.x,0,refuge);
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
                if(!h.Usable)
                {
                    armed[hand]=false;dwell[hand]=0;
                    // A blink of tracking (low-confidence fingers over the wool) keeps the stroke; a real loss is leaving.
                    if((petGrace[hand]+=dt)>PetGrace){candidate[hand]=-1;petDwell[hand]=stroke[hand]=0;petLatched[hand]=false;}
                    continue;
                }
                if(h.Closure<GrabArmClosure)armed[hand]=true;
                float reach=float.IsFinite(Reach) && Reach>0?Reach:1;
                Tap(run,arena,hand,h,hand==0?leftPet:rightPet,reach,dt);
                int nearest=-1;float best=PickRadius*reach;
                for(int i=0;i<run.Animals.Length;i++)
                {
                    var a=run.Animals[i];if(!FireRun.CanPet(a) || a.HeldBy>=0)continue;
                    var pet=hand==0?leftPet:rightPet;
                    float distance=Vector3.Distance(h.Contact,arena.TransformPoint(Center(a)));
                    if(h.Closure<PetClosure && pet.HasValue)distance=Mathf.Min(distance,Vector3.Distance(pet.Value,arena.TransformPoint(Center(a))));
                    if(distance<best){best=distance;nearest=i;}
                }
                if(nearest>=0 && candidate[hand]!=nearest){candidate[hand]=nearest;dwell[hand]=petDwell[hand]=stroke[hand]=petGrace[hand]=0;petLatched[hand]=false;}
                if(nearest<0)
                {
                    dwell[hand]=0;
                    if(candidate[hand]>=0 && (petGrace[hand]+=dt)>PetGrace){candidate[hand]=-1;petDwell[hand]=stroke[hand]=0;petLatched[hand]=false;}
                    continue;
                }
                var sheep=run.Animals[nearest];
                if(armed[hand] && h.Closure>=GrabClosure && Available(sheep))
                {
                    dwell[hand]+=dt;
                    if(dwell[hand]>=.08f)
                    {
                        held[hand]=nearest;sheep.HeldBy=hand;sheep.ReturningToGround=false;sheep.State=AnimalState.Waiting;sheep.StateTime=0;
                        offset[hand]=arena.TransformPoint(Center(sheep))-h.Contact;armed[hand]=false;
                    }
                }
                else dwell[hand]=0;
                if(PetAllowed && h.Closure<PetClosure && best<PetRadius*reach)
                {
                    petGrace[hand]=0;
                    var touch=(hand==0?leftPet:rightPet)??h.Contact;
                    if(petDwell[hand]>0 || petLatched[hand])stroke[hand]+=Vector3.Distance(touch,lastPet[hand]);
                    lastPet[hand]=touch;petDwell[hand]+=dt;
                    if(petLatched[hand] && stroke[hand]>=PetStroke)petLatched[hand]=false;
                    if(!petLatched[hand] && petDwell[hand]>=PetDwell && TryPet(sheep))
                    {petDwell[hand]=stroke[hand]=0;petLatched[hand]=true;Petted?.Invoke(nearest);}
                }
                else if((petGrace[hand]+=dt)>PetGrace){petDwell[hand]=stroke[hand]=0;petLatched[hand]=false;}
            }
        }
        // One hand's tap, before the grab and stroke rules of this frame (so a tap that disarms also stops this frame's grab).
        void Tap(FireRun run,Transform arena,int hand,FireToolManipulation.Hand h,Vector3? tip,float reach,float dt)
        {
            int touched=-1;float best=PetRadius*reach;
            if(tip.HasValue)for(int i=0;i<run.Animals.Length;i++)
            {
                var a=run.Animals[i];if(!FireRun.CanPet(a) || a.HeldBy>=0)continue;
                float distance=Vector3.Distance(tip.Value,arena.TransformPoint(Center(a)));
                if(distance<best){best=distance;touched=i;}
            }
            if(touched<0){if((tapOut[hand]+=dt)>=TapRearm)tapArmed[hand]=true;return;}
            tapOut[hand]=0;
            if(!tapArmed[hand])return;
            tapArmed[hand]=false; // this entry is spent, pet or not
            var sheep=run.Animals[touched];bool holdable=Available(sheep);
            if(holdable && (h.Closure<GrabArmClosure || Vector3.Distance(h.Contact,arena.TransformPoint(Center(sheep)))<PickRadius*reach))return;
            if(!PetAllowed || !TryPet(sheep))return;
            Taps++;
            if(holdable){armed[hand]=false;dwell[hand]=0;}
            // The stroke rules carry on from here: a hand left resting does not pet again without a stroke.
            candidate[hand]=touched;petDwell[hand]=stroke[hand]=petGrace[hand]=0;petLatched[hand]=true;lastPet[hand]=tip.Value;
            Petted?.Invoke(touched);
        }
    }
}

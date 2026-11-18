using UnityEngine;
namespace FireRescue
{
    public sealed partial class FireRun
    {
        public float CountdownRemaining {get;private set;}=-1;
        public bool BeginCountdown()
        {
            if(Phase!=FirePhase.Preparing || CountdownRemaining>=0)return false;
            CountdownRemaining=3;return true;
        }
        public bool TickPreparation(float dt)
        {
            if(Phase!=FirePhase.Preparing || CountdownRemaining<0 || !float.IsFinite(dt) || dt<=0)return false;
            CountdownRemaining=Mathf.Max(0,CountdownRemaining-Mathf.Min(dt,.1f));
            if(CountdownRemaining>.0001f)return false;
            CountdownRemaining=-1;Phase=FirePhase.Fighting;return true;
        }
        public void TickPreparedAnimals(float dt)
        {
            if(Phase!=FirePhase.Preparing || !float.IsFinite(dt) || dt<=0)return;dt=Mathf.Min(dt,.1f);
            foreach(var a in Animals)
            {
                a.AnimationTime+=dt;a.Affection=Mathf.Max(0,a.Affection-dt);a.PetCooldown=Mathf.Max(0,a.PetCooldown-dt);
                if(a.HeldBy<0 && a.ReturningToGround)SettleAnimal(a,dt);
            }
        }
    }
}

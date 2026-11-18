using UnityEngine;
namespace FireRescue
{
    public sealed partial class FireRescueGame
    {
        readonly EmberdeepSheepInteraction sheepInteraction=new();
        public EmberdeepSheepInteraction SheepInteraction=>sheepInteraction;
        void ReadSheepHands(float dt)
        {
            if(ShopOpen || QuitConfirmationOpen || QuitRequested || Run.Phase==FirePhase.Lost || Run.Supply.Locked)
            {sheepInteraction.Reset();return;}
            var left=toolHands[0];var right=toolHands[1];
            if(ToolOwnsHand(0) || (refill.OwnsControl && refill.OperatingHand==0))left.Valid=false;
            if(ToolOwnsHand(1) || (refill.OwnsControl && refill.OperatingHand==1))right.Valid=false;
            // During a committed cast, do not acquire a sheep along the aiming ray.
            if(palmMagic.Casting){left.Valid=false;right.Valid=false;}
            sheepInteraction.Step(Run,Arena,left,right,dt,NearestSheepFinger(true),NearestSheepFinger(false));
        }
        Vector3? NearestSheepFinger(bool left)
        {
            Vector3? closest=null;float best=.045f;
            for(int f=0;f<5;f++)if(hands.TryReadFingertip(left,f,out var tip))foreach(var a in Run.Animals)
            {
                float distance=Vector3.Distance(tip,Arena.TransformPoint(EmberdeepSheepInteraction.Center(a)));
                if(distance<best){best=distance;closest=tip;}
            }
            return closest;
        }
    }
}

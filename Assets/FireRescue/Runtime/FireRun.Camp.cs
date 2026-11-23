using UnityEngine;

namespace FireRescue
{
    // LoopSpec §9 camp: the camp island as a tiny FireRun scene, so the shared fire VFX (FireRescueVfx), the sheep
    // view, B's pet interaction (EmberdeepSheepInteraction) and the flock audio run at camp unchanged. Trees are fire
    // points (the campfire, the sapling) and Animals are the camp sheep. Nothing ticks the board sim: the camp
    // (EmberdeepCampPlay) sets heat, fire and sheep motion itself. Legacy rules, so the VFX sees no wind.
    public sealed partial class FireRun
    {
        public static FireRun CampScene(Vector3[] fires,Vector3[] sheep)
        {
            var run=new FireRun(false);
            run.Trees=new Tree[fires.Length];
            for(int i=0;i<fires.Length;i++)run.Trees[i]=new Tree{Position=fires[i],State=TreeState.Safe};
            run.FieldColumns=fires.Length;
            run.Animals=new Animal[sheep.Length];
            for(int i=0;i<sheep.Length;i++)
                // Rescued: pettable, never grabbable; StateTime past the coin pop.
                run.Animals[i]=new Animal{Position=sheep[i],Home=sheep[i],Refuge=sheep[i],Pasture=sheep[i],State=AnimalState.Rescued,StateTime=10,AnimationTime=i*.73f};
            return run;
        }
    }
}

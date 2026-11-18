namespace FireRescue
{
    public sealed partial class FireRun
    {
        public bool[] LearnedActions {get;}=new bool[13];
        public bool NeedsTutorial(EmberdeepInteractionDemo.Action action)=>!LearnedActions[(int)action];
        public void Learn(EmberdeepInteractionDemo.Action action)=>LearnedActions[(int)action]=true;
        public void LearnWeapon(FireEquipmentId id)=>Learn(id==FireEquipmentId.Extinguisher?EmberdeepInteractionDemo.Action.Extinguisher:
            id==FireEquipmentId.PressureCannon?EmberdeepInteractionDemo.Action.Cannon:EmberdeepInteractionDemo.Action.Gun);
    }
}

using UnityEngine;
namespace PhysicalAssembly
{
    // Exported BP_Wood / BP_Wheel / BP_Stick bounds, centimetres -> metres,
    // at the hand-sized fallback scale. See Docs/HustlePhysicsPort.md.
    public static class HustlePartProfile
    {
        public const float Scale=.2f;
        public static readonly Vector3 ChassisSize=new(1.2772678f*Scale,.08854435f*Scale,2.6184137f*Scale);
        public static readonly Vector3 BeamSize=new(.1f*Scale,.1f*Scale,2.8958211f*Scale);
        public const float WheelDiameter=1.1407916f*Scale, WheelWidth=.7002921f*Scale;
        public const float MountWidth=.8058551f*Scale, HubWidth=.6105715f*Scale;
        public const float SuspensionTravel=.3f*Scale;
        public const float MountSocketX=-MountWidth*.5f;
        public const float RideHeight=.16f;
    }
}

using UnityEngine;
namespace PhysicalAssembly
{
    public enum SandboxCourseId { GravityGarage, TeleportPipes }
    public static class SandboxCampaignSession
    {
        public static SandboxCourseId Current { get; private set; }
        public static bool FirstComplete { get; private set; }
        public static bool DrillClaimed { get; private set; }
        public static bool AllComplete { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset() { Current=SandboxCourseId.GravityGarage; FirstComplete=AllComplete=DrillClaimed=false; }
        public static void Complete(SandboxCourseId course) { if(course==SandboxCourseId.GravityGarage) FirstComplete=true; else AllComplete=true; }
        public static void Advance() { if(FirstComplete) { Current=SandboxCourseId.TeleportPipes; DrillClaimed=true; } }
    }
}

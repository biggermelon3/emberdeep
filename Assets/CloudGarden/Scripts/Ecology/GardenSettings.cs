using UnityEngine;

namespace CloudGarden
{
    [CreateAssetMenu(menuName = "Cloud Garden/Garden Settings")]
    public sealed class GardenSettings : ScriptableObject
    {
        public Shader surfaceShader, glowShader, particleShader;
        [Min(.001f)] public float rainCost = .07f;
        [Min(.001f)] public float rainGain = .22f;
        [Min(.001f)] public float growthRate = .09f;
        [Min(.001f)] public float transpirationRate = .025f;
        [Min(.001f)] public float springRate = .018f;
        [Min(.001f)] public float seedCost = .35f;
        [Min(.001f)] public float stormCost = .4f;
        [Min(.1f)] public float trackingGrace = .3f;
        [Range(2, 6)] public int maxClouds = 4;
        [Min(.1f)] public float fistHoldSeconds = .6f;
        [Min(.01f)] public float kneadTravel = .06f;
        [Min(.01f)] public float patTravel = .022f;
        [Min(.1f)] public float gestureCooldown = .35f;
        [Min(0)] public float cloudContactGap = .06f;
        [Min(0)] public float proximityChargeRate = .018f;
        [Min(0)] public float frictionChargePerMeter = .65f;
        [Min(0)] public float contactChargeRateLimit = .25f;
    }
}

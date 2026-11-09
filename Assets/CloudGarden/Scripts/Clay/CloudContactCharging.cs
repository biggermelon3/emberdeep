using System.Collections.Generic;
using UnityEngine;

namespace CloudGarden
{
    // Measure local cloud motion, never mesh breathing or a change of the table anchor.
    public sealed class CloudContactCharging
    {
        readonly Dictionary<CloudShapeController, Vector3> previous = new Dictionary<CloudShapeController, Vector3>();
        float[] gains = new float[0];
        public void Reset() => previous.Clear();

        public static float ChargeDelta(float distance, float radii, float relativeTravel, float dt, GardenSettings settings)
        {
            if (dt <= 0 || dt > .1f || distance > radii + settings.cloudContactGap) return 0;
            float proximity = 1 - Mathf.InverseLerp(radii, radii + Mathf.Max(.001f, settings.cloudContactGap), distance);
            // Ignore jumps and small input jitter; stationary contact still charges slowly.
            float travel = relativeTravel > dt * 2.5f + .01f ? 0 : Mathf.Max(0, relativeTravel - dt * .03f);
            return Mathf.Min(settings.contactChargeRateLimit * dt,
                proximity * (settings.proximityChargeRate * dt + travel * settings.frictionChargePerMeter));
        }

        public void Tick(IReadOnlyList<CloudShapeController> clouds, float dt, GardenSettings settings)
        {
            if (gains.Length < clouds.Count) gains = new float[clouds.Count];
            System.Array.Clear(gains, 0, gains.Length);
            for (int i = 0; i < clouds.Count; i++)
                for (int j = i + 1; j < clouds.Count; j++)
                {
                    var a = clouds[i]; var b = clouds[j];
                    var pa = a.transform.localPosition; var pb = b.transform.localPosition;
                    float travel = previous.TryGetValue(a, out var oldA) && previous.TryGetValue(b, out var oldB)
                        ? ((pa - oldA) - (pb - oldB)).magnitude : 0;
                    float gain = ChargeDelta(Vector3.Distance(pa, pb), a.Radius + b.Radius, travel, dt, settings);
                    gains[i] += gain; gains[j] += gain;
                }
            previous.Clear();
            for (int i = 0; i < clouds.Count; i++)
            {
                var cloud = clouds[i];
                cloud.resources.charge = Mathf.Clamp01(cloud.resources.charge + Mathf.Min(gains[i], settings.contactChargeRateLimit * Mathf.Max(0, dt)));
                previous[cloud] = cloud.transform.localPosition;
            }
        }
    }
}

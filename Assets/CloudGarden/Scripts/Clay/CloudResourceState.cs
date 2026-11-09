using System;
using UnityEngine;

namespace CloudGarden
{
    [Serializable]
    public sealed class CloudResourceState
    {
        public float moisture = .85f, charge = .8f, mass = 1, stability = 1;
        public CloudResourceState Copy() => (CloudResourceState)MemberwiseClone();
        public float SpendRain(float dt, float cost)
        {
            float spend = Mathf.Min(moisture, Mathf.Max(0, dt) * cost);
            moisture -= spend;
            return cost > 0 ? spend / cost : 0;
        }
        public bool SpendCharge(float cost)
        {
            if (charge < cost) return false;
            charge -= cost;
            return true;
        }
    }
}

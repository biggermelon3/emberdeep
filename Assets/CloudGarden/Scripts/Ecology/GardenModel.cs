using System;
using UnityEngine;

namespace CloudGarden
{
    [Serializable]
    public sealed class PlantState
    {
        public float water, growth, startled;
        public PlantState Copy() => (PlantState)MemberwiseClone();
    }

    [Serializable]
    public sealed class GardenModel
    {
        public static readonly Vector2[] Beds = { new Vector2(-.35f, -.12f), new Vector2(.35f, -.12f) };
        public static readonly Vector2 Pond = new Vector2(0, .18f);
        public static readonly Vector2 Tower = new Vector2(.40f, .25f);
        public PlantState[] plants = { new PlantState(), new PlantState() };
        public float pond, vapor = .12f, energy, energyDelivered, pumpedWater, transpired, returnedWater;
        public int seedsCreated, cycles;

        public GardenModel Copy()
        {
            var copy = (GardenModel)MemberwiseClone();
            copy.plants = new[] { plants[0].Copy(), plants[1].Copy() };
            return copy;
        }

        // Weather is a range event; rain particles are presentation only.
        public void Rain(Vector2 position, float radius, float amount)
        {
            for (int i = 0; i < plants.Length; i++)
                if (Vector2.Distance(position, Beds[i]) <= radius + .09f)
                    plants[i].water = Mathf.Clamp01(plants[i].water + amount);
            if (Vector2.Distance(position, Pond) <= radius + .14f)
                pond = Mathf.Clamp01(pond + amount);
        }

        public bool Strike(Vector2 position, float radius)
        {
            bool hit = Vector2.Distance(position, Tower) < radius + .08f;
            if (hit) { energy = Mathf.Clamp01(energy + .55f); energyDelivered += .55f; }
            else
                for (int i = 0; i < plants.Length; i++)
                    if (Vector2.Distance(position, Beds[i]) < radius + .1f) plants[i].startled = 3;
            return hit;
        }

        public void Tick(float dt, GardenSettings settings)
        {
            if (dt <= 0 || float.IsNaN(dt)) return;
            vapor = Mathf.Clamp01(vapor + settings.springRate * dt);
            for (int i = 0; i < plants.Length; i++)
            {
                var p = plants[i];
                p.startled = Mathf.Max(0, p.startled - dt);
                if (p.water > .12f && p.startled <= 0)
                {
                    p.growth = Mathf.Clamp01(p.growth + settings.growthRate * dt);
                    float evaporated = Mathf.Min(p.water, settings.transpirationRate * p.growth * dt);
                    p.water -= evaporated;
                    vapor = Mathf.Clamp01(vapor + evaporated);
                    transpired += evaporated;
                    returnedWater += evaporated;
                }
            }
            if (pond > 0)
            {
                float evaporated = Mathf.Min(pond, .008f * dt);
                pond -= evaporated;
                vapor = Mathf.Clamp01(vapor + evaporated);
                returnedWater += evaporated;
            }
            if (energy > 0 && pond > 0)
            {
                float pumped = Mathf.Min(.04f * dt, pond, energy * 2);
                pond -= pumped;
                energy = Mathf.Max(0, energy - pumped * .5f);
                pumpedWater += pumped;
                foreach (var p in plants) p.water = Mathf.Clamp01(p.water + pumped);
            }
        }

        public bool TryGrowSeed(GardenSettings settings, int cloudCount, CloudResourceState mother)
        {
            if (cloudCount >= settings.maxClouds || vapor < settings.seedCost || mother.mass < .45f) return false;
            vapor -= settings.seedCost;
            mother.mass -= .22f;
            seedsCreated++;
            if (returnedWater >= .2f) { cycles++; returnedWater -= .2f; }
            return true;
        }
    }
}

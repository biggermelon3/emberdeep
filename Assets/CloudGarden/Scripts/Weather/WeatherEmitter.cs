using UnityEngine;

namespace CloudGarden
{
    public sealed class WeatherEmitter : MonoBehaviour
    {
        public CloudShapeController cloud;
        public CloudGardenGame game;
        ParticleSystem rain;
        LineRenderer lightning;
        float flashUntil;

        public void Initialize(CloudGardenGame owner, CloudShapeController source, Material rainMaterial)
        {
            game = owner; cloud = source;
            var go = new GameObject("Rain droplets"); go.transform.SetParent(transform, false);
            rain = go.AddComponent<ParticleSystem>(); rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = rain.main; main.startLifetime = .55f; main.startSpeed = 0; main.startSize = .012f;
            main.maxParticles = 140; main.simulationSpace = ParticleSystemSimulationSpace.Custom; main.customSimulationSpace = game.GardenRoot;
            main.startColor = new Color(.52f, .82f, .95f, .85f);
            var emission = rain.emission; emission.rateOverTime = 65;
            var shape = rain.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(.32f, .02f, .23f);
            var velocity = rain.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local; velocity.y = -1.2f;
            var renderer = rain.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = rainMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.lengthScale = 3; renderer.velocityScale = .04f;
            var lineGo = new GameObject("Lightning"); lineGo.transform.SetParent(transform, false);
            lightning = lineGo.AddComponent<LineRenderer>(); lightning.sharedMaterial = game.GlowMaterial;
            lightning.useWorldSpace = false;
            lightning.startColor = lightning.endColor = new Color(1, .84f, .36f); lightning.widthMultiplier = .012f;
            lightning.positionCount = 7; lightning.enabled = false;
        }

        public void Tick(float dt)
        {
            if (!cloud.raining) { if (rain.isPlaying) rain.Stop(); return; }
            float duration = cloud.resources.SpendRain(dt, game.settings.rainCost);
            var pos = game.WorldToGarden(cloud.transform.position);
            game.Model.Rain(new Vector2(pos.x, pos.z), cloud.Radius, duration * game.settings.rainGain);
            var shape = rain.shape; shape.scale = new Vector3(cloud.Radius * 1.8f, .015f, .22f * cloud.shape.z);
            if (!rain.isPlaying) rain.Play();
            if (cloud.resources.moisture <= .001f) { cloud.raining = false; rain.Stop(); }
        }

        public void Flash(Vector3 target)
        {
            Vector3 start = Vector3.zero;
            target = transform.InverseTransformPoint(target);
            for (int i = 0; i < 7; i++)
            {
                Vector3 p = Vector3.Lerp(start, target, i / 6f);
                if (i > 0 && i < 6) p.x += Mathf.Sin(i * 12.3f) * .045f;
                lightning.SetPosition(i, p);
            }
            flashUntil = Time.time + .32f;
        }
        void Update() { if (lightning != null) lightning.enabled = Time.time < flashUntil; }
    }
}

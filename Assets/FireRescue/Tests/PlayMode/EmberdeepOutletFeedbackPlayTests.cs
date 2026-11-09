using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    public sealed class EmberdeepOutletFeedbackPlayTests
    {
        [UnityTest] public IEnumerator EmptyOutletBurstFallsOutwardAndExpiresWithinItsBoundedBudget()
        {
            var owner=new GameObject("Outlet presentation regression");
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            try
            {
                owner.transform.SetPositionAndRotation(new Vector3(0,1,0),Quaternion.Euler(0,35,0));
                var view=owner.AddComponent<EmberdeepWaterStationView>();view.Build(material,material,material,false);
                int objects=owner.GetComponentsInChildren<Transform>(true).Length;
                view.EmitOutletSplash();Assert.IsTrue(view.OutletActive);
                var arc=owner.transform.Find("Empty outlet water arc").GetComponent<LineRenderer>();
                Assert.IsTrue(arc.enabled);Assert.AreEqual(12,arc.positionCount);
                var start=arc.GetPosition(0);var end=arc.GetPosition(arc.positionCount-1);
                Assert.That(Vector3.Distance(start,view.Socket.position),Is.LessThan(.012f));
                Assert.That(Vector3.Dot(end-start,view.Socket.forward),Is.GreaterThan(.05f),"Water leaves the open mating face");
                Assert.Less(end.y,start.y-.08f,"The decorative arc drops under gravity");
                Assert.That(view.OutletParticleCount,Is.GreaterThan(0).And.LessThanOrEqualTo(EmberdeepWaterStationView.OutletParticleBudget));
                for(int i=0;i<50;i++)view.EmitOutletSplash();
                Assert.AreEqual(objects,owner.GetComponentsInChildren<Transform>(true).Length,"Repeated strokes reuse the existing effects");
                Assert.LessOrEqual(view.OutletParticleCount,EmberdeepWaterStationView.OutletParticleBudget);
                view.TickOutlet(float.NaN);view.TickOutlet(-1);Assert.IsTrue(view.OutletActive);
                var drops=owner.transform.Find("Empty outlet droplets").GetComponent<ParticleSystem>();
                var particles=new ParticleSystem.Particle[EmberdeepWaterStationView.OutletParticleBudget];
                Assert.Greater(drops.GetParticles(particles),0);float firstHeight=particles[0].position.y;
                view.TickOutlet(.1f);
                Assert.Greater(drops.GetParticles(particles),0);Assert.Less(particles[0].position.y,firstHeight,"Explicit feedback ticks advance falling drops");
                view.TickOutlet(.1f);
                Assert.IsTrue(owner.transform.Find("Empty outlet ripple").GetComponent<LineRenderer>().enabled);
                view.TickOutlet(.1f);view.TickOutlet(.05f);
                Assert.IsFalse(view.OutletActive);Assert.Zero(view.OutletParticleCount);Assert.IsFalse(arc.enabled);
                Assert.IsFalse(owner.transform.Find("Empty outlet ripple").GetComponent<LineRenderer>().enabled);
            }
            finally{Object.Destroy(owner);Object.Destroy(material);}
            yield return null;
        }

        [UnityTest] public IEnumerator StopDisableAndTierChangeClearOutletWithoutAutomaticRestart()
        {
            var owner=new GameObject("Outlet interruption regression");
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            try
            {
                var view=owner.AddComponent<EmberdeepWaterStationView>();view.Build(material,material,material,false);
                view.EmitOutletSplash();view.StopOutlet();view.TickOutlet(.1f);
                Assert.IsFalse(view.OutletActive);Assert.Zero(view.OutletParticleCount);
                view.EmitOutletSplash();view.enabled=false;
                Assert.IsFalse(view.OutletActive);Assert.Zero(view.OutletParticleCount);
                view.EmitOutletSplash();Assert.IsFalse(view.OutletActive,"Disabled or paused presentation cannot restart itself");
                view.enabled=true;view.TickOutlet(.1f);Assert.IsFalse(view.OutletActive);
                view.EmitOutletSplash();view.SetTier(WaterStationTier.RotaryHydrant);
                Assert.IsFalse(view.OutletActive);Assert.Zero(view.OutletParticleCount);
                Assert.IsFalse(owner.transform.Find("Empty outlet water arc").GetComponent<LineRenderer>().enabled);
            }
            finally{Object.Destroy(owner);Object.Destroy(material);}
            yield return null;
        }
    }
}

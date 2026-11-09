using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    public sealed class EmberdeepStationTierViewTests
    {
        [UnityTest] public IEnumerator TierChangesKeepOneControlAndDockStableWhileShowingDistinctMotion()
        {
            var owner=new GameObject("Station tier visual regression");
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            try
            {
                var view=owner.AddComponent<EmberdeepWaterStationView>();view.Build(material,material,material,false);
                var socketPosition=view.Socket.position;var socketRotation=view.Socket.rotation;
                Assert.AreEqual(WaterStationTier.LeverWell,view.Tier);
                foreach(var tier in new[]{WaterStationTier.LeverWell,WaterStationTier.RotaryHydrant,WaterStationTier.PushButton})
                {
                    view.SetTier(tier);
                    Assert.AreEqual(tier==WaterStationTier.LeverWell,view.LeverVisual.gameObject.activeInHierarchy);
                    Assert.AreEqual(tier==WaterStationTier.RotaryHydrant,view.Valve.gameObject.activeInHierarchy);
                    Assert.AreEqual(tier==WaterStationTier.PushButton,view.ButtonVisual.gameObject.activeInHierarchy);
                    Assert.IsFalse(view.PumpVisual.gameObject.activeInHierarchy);
                    Assert.AreEqual(socketPosition,view.Socket.position);Assert.AreEqual(socketRotation,view.Socket.rotation);
                }
                view.SetTier(WaterStationTier.LeverWell);
                view.SetControlAnimation(.08f,0,0);
                Assert.That(view.Lever.position.y-view.LeverVisual.position.y,Is.EqualTo(.08f).Within(.0001f));
                Assert.That(view.LeverVisual.position.z,Is.GreaterThan(view.Lever.position.z),"A real pivot arc moves the tip backwards as it lowers");
                view.SetTier(WaterStationTier.LeverWell);
                Assert.That(view.Lever.position.y-view.LeverVisual.position.y,Is.EqualTo(.08f).Within(.0001f),"Refreshing an unchanged tier cannot reset its moving handle");
                view.SetLeftHanded(true);
                Assert.That(view.Lever.position.x,Is.GreaterThan(.12f));
                Assert.That(view.LeverVisual.position.x,Is.EqualTo(view.Lever.position.x).Within(.0001f));
                Assert.That(view.Lever.position.y-view.LeverVisual.position.y,Is.EqualTo(.08f).Within(.0001f));
                view.SetTier(WaterStationTier.RotaryHydrant);view.SetControlAnimation(0,37,0);
                var wheel=view.Valve.Find("Valve detent wheel");
                Assert.That(Quaternion.Angle(wheel.localRotation,Quaternion.Euler(0,0,37)),Is.LessThan(.01f));
                view.SetState("FLOW",.5f,true,true,0,false);
                Assert.That(Quaternion.Angle(wheel.localRotation,Quaternion.Euler(0,0,37)),Is.LessThan(.01f),"Flow feedback cannot replace accepted rotary motion with a canned angle");
                view.SetTier(WaterStationTier.PushButton);view.SetControlAnimation(0,0,1);
                Assert.That(view.Button.InverseTransformPoint(view.ButtonVisual.position).z,Is.EqualTo(.01f).Within(.0001f));
                view.SetControlAnimation(0,0,0);Assert.AreEqual(view.Button.position,view.ButtonVisual.position);
                foreach(var at in owner.GetComponentsInChildren<Transform>(true))
                    Assert.IsTrue(at.localScale.x>0 && at.localScale.y>0 && at.localScale.z>0,"Mirroring must preserve positive scale: "+at.name);
            }
            finally{Object.Destroy(owner);Object.Destroy(material);}
            yield return null;
        }

        [UnityTest] public IEnumerator CreekRetainsItsOwnPullHandleAndIntakeAcrossStationUpgrades()
        {
            var owner=new GameObject("Creek tier isolation regression");
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            try
            {
                var view=owner.AddComponent<EmberdeepWaterStationView>();view.Build(material,material,material,true);
                view.SetTier(WaterStationTier.PushButton);view.SetState("PUMP",.25f,false,true,.08f,true);
                Assert.IsTrue(view.PumpVisual.gameObject.activeInHierarchy);Assert.IsTrue(view.IntakeVisual.gameObject.activeInHierarchy);
                Assert.IsFalse(view.Valve.gameObject.activeInHierarchy);Assert.IsFalse(view.LeverVisual.gameObject.activeInHierarchy);Assert.IsFalse(view.ButtonVisual.gameObject.activeInHierarchy);
                Assert.That(Vector3.Dot(view.Pump.position-view.PumpVisual.position,owner.transform.forward),Is.EqualTo(.08f).Within(.0001f));
                Assert.AreEqual(view.IntakeSocket.position,view.IntakeVisual.position);
            }
            finally{Object.Destroy(owner);Object.Destroy(material);}
            yield return null;
        }
    }
}

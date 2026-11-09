using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    // Real station/tool anchors and the production refill adapter, driven by
    // synthetic joint samples. These do not establish headset tracking quality.
    public sealed class EmberdeepStationTierInteractionTests
    {
        const float Dt=.025f,Open=.04f,Closed=.01f;
        GameObject root;
        Material material;
        FireRun run;
        FireEquipmentView tool;
        EmberdeepWaterStationView station,creek;
        EmberdeepRefillInteraction interaction;

        [TearDown] public void Cleanup(){if(root)Object.Destroy(root);if(material)Object.Destroy(material);}
        static void Clear(FireRun run)
        {foreach(var tree in run.Trees)if(tree.State==TreeState.Burning)run.ApplyWater(tree.Position,100,.1f);}
        void Setup(WaterStationTier tier,int hand)
        {
            run=new FireRun();
            while((int)run.StationTier<(int)tier)
            {
                while(run.Coins<run.StationUpgradeCost){Assert.IsTrue(run.StartStage());Clear(run);}
                Assert.IsTrue(run.BuyStationUpgrade());
            }
            run.StartStage();run.Tick(Dt,false);
            for(int i=0;i<100 && run.Water>0;i++)run.Tick(.1f,true);
            run.CancelTrigger();Assert.Zero(run.Water);
            root=new GameObject("Tier interaction fixture");material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            station=new GameObject("Tier station").AddComponent<EmberdeepWaterStationView>();station.transform.SetParent(root.transform);
            station.Build(material,material,material,false);station.SetTier(tier);station.SetLeftHanded(hand==0);
            creek=new GameObject("Creek").AddComponent<EmberdeepWaterStationView>();creek.transform.SetParent(root.transform);
            creek.Build(material,material,material,true);creek.transform.position=Vector3.right*2;
            tool=new GameObject("Actual tool ports").AddComponent<FireEquipmentView>();tool.transform.SetParent(root.transform);
            tool.Build(FireEquipmentId.Extinguisher,material,material,material);
            interaction=new EmberdeepRefillInteraction();EmberdeepRefillInteraction.Snap(tool,station.Socket);
            var palm=new Pose(tool.Grip.position,Quaternion.identity);
            for(int i=0;i<12;i++)Sample(hand,palm,Open,palm.position);
            Assert.IsTrue(run.Supply.Locked);Assert.Zero(run.Water);
        }
        void Sample(int hand,Pose palm,float pinch,Vector3 contact,bool tracked=true,float dt=Dt)
        {
            var sample=new EmberdeepRefillInteraction.Hand(tracked,palm,pinch,contact);
            var idle=new EmberdeepRefillInteraction.Hand(true,new Pose(Vector3.up*2,Quaternion.identity),Open);
            interaction.Process(run,tool,station,creek,true,hand,hand==0?sample:idle,hand==1?sample:idle,dt);
        }
        void SampleGrasp(int hand,Pose palm,float closure,Vector3 contact,bool tracked=true,float dt=Dt)
        {
            var sample=new EmberdeepRefillInteraction.Hand(tracked,palm,closure>.5f?Closed:Open,contact)
                {GripStrength=0,GripValid=false};
            var idle=new EmberdeepRefillInteraction.Hand(true,new Pose(Vector3.up*2,Quaternion.identity),Open);
            interaction.Process(run,tool,station,creek,true,hand,hand==0?sample:idle,hand==1?sample:idle,dt);
        }
        void CloseGrasp(int hand,Pose palm)
        {for(int i=0;i<3;i++)SampleGrasp(hand,palm,1,station.Lever.position);}
        Pose Ready(int hand,Transform target)
        {
            var palm=new Pose(target.position+Vector3.right*.16f,Quaternion.identity);
            for(int i=0;i<8;i++)
                if(target==station.Lever)SampleGrasp(hand,palm,0,target.position);else Sample(hand,palm,Open,target.position);
            return palm;
        }
        [UnityTest] public IEnumerator LeftLeverCompletesPhysicalCyclesAndPublishesControlOwnership()=>Lever(0);
        [UnityTest] public IEnumerator RightLeverCompletesPhysicalCyclesAndPublishesControlOwnership()=>Lever(1);
        [UnityTest] public IEnumerator ConnectedLeverUsesPalmStrokeAndKeepsOneContinuousPinchWithoutGripProvider()
        {
            Setup(WaterStationTier.LeverWell,1);var rest=Ready(1,station.Lever);var contact=station.Lever.position;
            SampleGrasp(1,rest,1,contact);
            Assert.AreEqual(EmberdeepRefillInteraction.ActionKind.Lever,interaction.CurrentAction);
            SampleGrasp(1,rest,1,contact);
            Assert.AreEqual(EmberdeepRefillInteraction.ActionKind.Lever,interaction.CurrentAction,"Acquiring the lever cannot reset its owning pinch");
            SampleGrasp(1,rest,1,contact-Vector3.up*.09f);SampleGrasp(1,rest,1,contact);
            Assert.Zero(run.Water);Assert.IsFalse(run.Supply.LeverReturnRequired,"Index-only motion cannot load a stroke");
            var down=rest;down.position-=station.transform.up*.085f;
            SampleGrasp(1,down,1,contact);Assert.IsTrue(run.Supply.LeverReturnRequired);Assert.Zero(run.Water);
            SampleGrasp(1,rest,1,contact);Assert.AreEqual(12,run.Water);Assert.AreEqual(12,run.SuppliedWater);
            for(int i=0;i<8;i++)SampleGrasp(1,rest,1,contact);
            Assert.AreEqual(12,run.Water);Assert.IsTrue(run.Supply.Locked);yield return null;
        }
        IEnumerator Lever(int hand)
        {
            Setup(WaterStationTier.LeverWell,hand);var rest=Ready(hand,station.Lever);
            CloseGrasp(hand,rest);
            Assert.AreEqual(EmberdeepRefillInteraction.ActionKind.Lever,interaction.CurrentAction);
            Assert.AreEqual(hand,interaction.OperatingHand);Assert.AreSame(station.LeverVisual,interaction.ActionAnchor);
            for(int stroke=0;stroke<5;stroke++)
            {
                var down=rest;down.position-=station.transform.up*.085f;
                SampleGrasp(hand,down,1,station.Lever.position-station.transform.up*.085f);
                Assert.That(interaction.ActionAmount,Is.EqualTo(.085f).Within(.0001f));
                Assert.AreEqual(stroke*12,run.Water);Assert.IsTrue(run.Supply.LeverReturnRequired);
                SampleGrasp(hand,rest,1,station.Lever.position);Assert.AreEqual((stroke+1)*12,run.Water);
                for(int i=0;i<4;i++)SampleGrasp(hand,rest,1,station.Lever.position);
                Assert.AreEqual((stroke+1)*12,run.Water,"A held hand at the rest cannot pay repeatedly");
            }
            Assert.AreEqual(run.Capacity,run.Water);Assert.AreEqual(run.Capacity,run.SuppliedWater);
            SampleGrasp(hand,rest,0,station.Lever.position);
            Assert.AreEqual(EmberdeepRefillInteraction.ActionKind.None,interaction.CurrentAction);Assert.IsNull(interaction.ActionAnchor);
            Assert.AreEqual(-1,interaction.OperatingHand);Assert.Zero(interaction.ActionAmount);yield return null;
        }
        [UnityTest] public IEnumerator LeverReleaseAndTrackingLossCancelPendingStroke()
        {
            Setup(WaterStationTier.LeverWell,1);var rest=Ready(1,station.Lever);
            CloseGrasp(1,rest);
            var down=rest;down.position-=station.transform.up*.085f;
            SampleGrasp(1,down,1,station.Lever.position-Vector3.up*.085f);SampleGrasp(1,rest,0,station.Lever.position);
            Assert.Zero(run.Water);rest=Ready(1,station.Lever);CloseGrasp(1,rest);
            SampleGrasp(1,down,1,station.Lever.position-Vector3.up*.085f);SampleGrasp(1,rest,1,station.Lever.position,false);
            Assert.IsTrue(interaction.Interrupted);Assert.IsFalse(run.Supply.Locked);Assert.Zero(run.Water);
            Assert.AreEqual(EmberdeepRefillInteraction.ActionKind.None,interaction.CurrentAction);yield return null;
        }
        [UnityTest] public IEnumerator RotaryTapDoesNothingButSignedRotationOpensAndVisualNeverDoubleAddsDetent()
        {
            Setup(WaterStationTier.RotaryHydrant,0);var rest=Ready(0,station.Valve);
            Sample(0,rest,Closed,station.Valve.position);Sample(0,rest,Open,station.Valve.position);
            Assert.IsFalse(run.Supply.ValveOpen);Assert.Zero(run.Water);
            rest=Ready(0,station.Valve);Sample(0,rest,Closed,station.Valve.position);
            var turned=rest;turned.rotation=Quaternion.AngleAxis(30,station.Valve.forward)*rest.rotation;
            Sample(0,turned,Closed,station.Valve.position);Assert.That(interaction.ValveVisualDegrees,Is.EqualTo(30).Within(.01f));
            Assert.IsFalse(run.Supply.ValveOpen);
            turned.rotation=Quaternion.AngleAxis(45,station.Valve.forward)*rest.rotation;
            Sample(0,turned,Closed,station.Valve.position);Assert.IsTrue(run.Supply.ValveOpen);
            Assert.AreSame(station.ValveVisual,interaction.ActionAnchor);Assert.That(interaction.ValveVisualDegrees,Is.EqualTo(45).Within(.01f));
            Sample(0,turned,Open,station.Valve.position);Assert.AreEqual(45,interaction.ValveVisualDegrees);
            rest=Ready(0,station.Valve);Sample(0,rest,Closed,station.Valve.position);
            turned=rest;turned.rotation=Quaternion.AngleAxis(-45,station.Valve.forward)*rest.rotation;
            Sample(0,turned,Closed,station.Valve.position);Assert.IsFalse(run.Supply.ValveOpen);
            Assert.Zero(interaction.ValveVisualDegrees);yield return null;
        }
        [UnityTest] public IEnumerator ButtonEachFreshNearPressTogglesAndHoldingDoesNotRepeat()
        {
            Setup(WaterStationTier.PushButton,1);var palm=Ready(1,station.Button);
            Sample(1,palm,Closed,station.Button.position);
            Assert.IsTrue(run.Supply.ValveOpen);Assert.AreEqual(EmberdeepRefillInteraction.ActionKind.Button,interaction.CurrentAction);
            Assert.AreSame(station.ButtonVisual,interaction.ActionAnchor);Assert.AreEqual(1,interaction.ActionAmount);
            float start=run.Water;
            for(int i=0;i<10;i++)Sample(1,palm,Closed,station.Button.position);
            Assert.IsTrue(run.Supply.ValveOpen);Assert.Greater(run.Water,start);
            Sample(1,palm,Open,station.Button.position);Assert.Zero(interaction.ActionAmount);
            palm=Ready(1,station.Button);Sample(1,palm,Closed,station.Button.position);
            Assert.IsFalse(run.Supply.ValveOpen);float stopped=run.Water;
            for(int i=0;i<10;i++)Sample(1,palm,Closed,station.Button.position);
            Assert.AreEqual(stopped,run.Water);
            Sample(1,palm,Open,station.Button.position);palm=Ready(1,station.Button);Sample(1,palm,Closed,station.Button.position);
            Assert.IsTrue(run.Supply.ValveOpen);
            Sample(1,palm,Closed,station.Button.position,false);Assert.IsFalse(run.Supply.Locked);Assert.IsFalse(run.Supply.ValveOpen);
            yield return null;
        }
        [UnityTest] public IEnumerator FarButtonPinchCannotOpenFlow()
        {
            Setup(WaterStationTier.PushButton,0);var palm=new Pose(station.Button.position+Vector3.up*.5f,Quaternion.identity);
            for(int i=0;i<8;i++)Sample(0,palm,Open,palm.position);
            Sample(0,palm,Closed,palm.position);Sample(0,palm,Open,palm.position);
            Assert.IsFalse(run.Supply.ValveOpen);Assert.Zero(run.Water);Assert.AreEqual(EmberdeepRefillInteraction.ActionKind.None,interaction.CurrentAction);
            yield return null;
        }
        [UnityTest] public IEnumerator NaNFrameCannotCompleteLeverStroke()=>InvalidFrame(float.NaN);
        [UnityTest] public IEnumerator InfiniteFrameCannotCompleteLeverStroke()=>InvalidFrame(float.PositiveInfinity);
        [UnityTest] public IEnumerator ZeroFrameCannotCompleteLeverStroke()=>InvalidFrame(0);
        [UnityTest] public IEnumerator NegativeFrameCannotCompleteLeverStroke()=>InvalidFrame(-.02f);
        IEnumerator InvalidFrame(float invalid)
        {
            Setup(WaterStationTier.LeverWell,0);var rest=Ready(0,station.Lever);
            CloseGrasp(0,rest);var down=rest;down.position-=Vector3.up*.085f;
            SampleGrasp(0,down,1,station.Lever.position-Vector3.up*.085f);Assert.IsTrue(run.Supply.LeverReturnRequired);
            SampleGrasp(0,rest,1,station.Lever.position,true,invalid);
            Assert.IsFalse(run.Supply.Locked);Assert.IsTrue(interaction.Interrupted);Assert.Zero(run.Water);Assert.Zero(run.SuppliedWater);
            Assert.AreEqual(EmberdeepRefillInteraction.ActionKind.None,interaction.CurrentAction);yield return null;
        }
        [UnityTest] public IEnumerator LeavingLeverCancelsCreditAndHeldPinchCannotReacquire()
        {
            Setup(WaterStationTier.LeverWell,1);var rest=Ready(1,station.Lever);CloseGrasp(1,rest);
            var down=rest;down.position-=Vector3.up*.085f;SampleGrasp(1,down,1,station.Lever.position-Vector3.up*.085f);
            var far=rest;far.position+=Vector3.up*.5f;SampleGrasp(1,far,1,station.Lever.position+Vector3.up*.5f);
            Assert.IsTrue(run.Supply.Locked);Assert.IsFalse(run.Supply.LeverReturnRequired);
            for(int i=0;i<10;i++)SampleGrasp(1,rest,1,station.Lever.position);
            Assert.AreEqual(EmberdeepRefillInteraction.ActionKind.None,interaction.CurrentAction);Assert.Zero(run.Water);
            rest=Ready(1,station.Lever);CloseGrasp(1,rest);
            SampleGrasp(1,down,1,station.Lever.position-Vector3.up*.085f);SampleGrasp(1,rest,1,station.Lever.position);
            Assert.AreEqual(12,run.Water);yield return null;
        }
        [UnityTest] public IEnumerator LeavingButtonKeepsAcceptedFlowButCannotReuseHeldPinch()
        {
            Setup(WaterStationTier.PushButton,0);var rest=Ready(0,station.Button);Sample(0,rest,Closed,station.Button.position);
            Assert.IsTrue(run.Supply.ValveOpen);var far=rest;far.position+=Vector3.up*.5f;
            Sample(0,far,Closed,station.Button.position+Vector3.up*.5f);
            Assert.AreEqual(EmberdeepRefillInteraction.ActionKind.None,interaction.CurrentAction);Assert.IsTrue(run.Supply.ValveOpen);
            for(int i=0;i<6;i++)Sample(0,rest,Closed,station.Button.position);
            Assert.AreEqual(EmberdeepRefillInteraction.ActionKind.None,interaction.CurrentAction);Assert.IsTrue(run.Supply.ValveOpen);
            rest=Ready(0,station.Button);Sample(0,rest,Closed,station.Button.position);
            Assert.IsFalse(run.Supply.ValveOpen);yield return null;
        }
        [UnityTest] public IEnumerator LeavingWheelBeforeTurningCannotOperateItRemotely()
        {
            Setup(WaterStationTier.RotaryHydrant,1);var rest=Ready(1,station.Valve);Sample(1,rest,Closed,station.Valve.position);
            var far=rest;far.position+=Vector3.up*.5f;far.rotation=Quaternion.AngleAxis(60,station.Valve.forward);
            Sample(1,far,Closed,station.Valve.position+Vector3.up*.5f);
            Assert.AreEqual(EmberdeepRefillInteraction.ActionKind.None,interaction.CurrentAction);Assert.IsFalse(run.Supply.ValveOpen);Assert.Zero(run.Water);
            Sample(1,far,Closed,station.Valve.position);Assert.IsFalse(run.Supply.ValveOpen);
            yield return null;
        }
    }
}

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    // Full-hand samples target the real station anchors. Empty strokes drive
    // only a presentation event, without pretending a tank is docked.
    public sealed class EmberdeepFreeLeverGripTests
    {
        GameObject root;
        Material material;
        EmberdeepWaterStationView station,creek;
        FireEquipmentView tool;
        FireRun run;
        EmberdeepRefillInteraction input;
        int events;
        float outletUnits;
        [SetUp] public void Setup()
        {
            root=new GameObject("Free well grasp test");material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            station=new GameObject("Well").AddComponent<EmberdeepWaterStationView>();station.transform.SetParent(root.transform);
            station.Build(material,material,material,false);station.SetTier(WaterStationTier.LeverWell);
            creek=new GameObject("Creek").AddComponent<EmberdeepWaterStationView>();creek.transform.SetParent(root.transform);
            creek.Build(material,material,material,true);creek.transform.position=Vector3.left*2;
            tool=new GameObject("Undocked tool").AddComponent<FireEquipmentView>();tool.transform.SetParent(root.transform);
            tool.Build(FireEquipmentId.Extinguisher,material,material,material);tool.transform.position=Vector3.right*2;
            run=new FireRun();input=new EmberdeepRefillInteraction();events=0;outletUnits=0;
            input.OutletStroke+=v=>{events++;outletUnits+=v;};
        }
        [TearDown] public void Cleanup(){if(root)Object.Destroy(root);if(material)Object.Destroy(material);}
        void Sample(int hand,float strength,Vector3 contact,bool held=false,int toolHand=1,bool valid=true,bool gripValid=true,float pinch=.04f,float dt=.025f)
        {
            var active=new EmberdeepRefillInteraction.Hand(valid,new Pose(contact+Vector3.right*.06f,Quaternion.identity),pinch,contact)
                {GripValid=gripValid,GripStrength=strength,GripContact=contact,HasGripContact=true};
            var idle=new EmberdeepRefillInteraction.Hand(true,new Pose(Vector3.up*2,Quaternion.identity),.04f)
                {GripValid=true,GripStrength=0};
            input.Process(run,tool,station,creek,held,toolHand,hand==0?active:idle,hand==1?active:idle,dt);
        }
        void Grasp(int hand,bool held=false,int toolHand=1)
        {
            for(int i=0;i<4;i++)Sample(hand,0,station.Lever.position,held,toolHand);
            for(int i=0;i<3;i++)Sample(hand,1,station.Lever.position,held,toolHand);
        }
        void Stroke(int hand,bool held=false,int toolHand=1)
        {Sample(hand,1,station.Lever.position-Vector3.up*.085f,held,toolHand);Sample(hand,1,station.Lever.position,held,toolHand);}
        [UnityTest] public IEnumerator LeftFistPumpsEmptyOutletWithoutPinchingOrChangingEconomy()=>EmptyCycles(0);
        [UnityTest] public IEnumerator RightFistPumpsEmptyOutletWithoutPinchingOrChangingEconomy()=>EmptyCycles(1);
        IEnumerator EmptyCycles(int hand)
        {
            run.StartStage();var first=run.Trees[0];float heat=first.Heat,water=run.Water;int coins=run.Coins;
            Grasp(hand);Assert.IsTrue(input.OwnsControl);Assert.IsTrue(input.EmptyOutlet);Assert.AreEqual(hand,input.OperatingHand);
            Assert.AreSame(station.LeverVisual,input.ActionAnchor);Assert.IsFalse(run.Supply.Locked);
            Stroke(hand);Stroke(hand);for(int i=0;i<10;i++)Sample(hand,1,station.Lever.position);
            Assert.AreEqual(2,events);Assert.AreEqual(24,outletUnits);Assert.AreEqual(water,run.Water);Assert.AreEqual(coins,run.Coins);
            Assert.Zero(run.SuppliedWater);Assert.Zero(run.ConsumedWater);Assert.AreEqual(heat,first.Heat);Assert.AreEqual(TreeState.Burning,first.State);
            Assert.IsFalse(run.Supply.Locked);Assert.IsFalse(input.Interrupted);yield return null;
        }
        [UnityTest] public IEnumerator PinchOnlySamplesNeverImplyFullHandGrip()
        {
            for(int i=0;i<5;i++)run.Supply.SampleDock(WaterSourceKind.Hydrant,1,0,0,true,.05f);
            Assert.IsTrue(run.Supply.Locked);
            for(int i=0;i<8;i++)Sample(0,0,station.Lever.position,gripValid:false);
            for(int i=0;i<8;i++)Sample(0,0,station.Lever.position,gripValid:false,pinch:.01f);
            Assert.IsFalse(input.OwnsControl);Assert.Zero(events);Assert.IsTrue(run.Supply.Locked);Assert.Zero(run.SuppliedWater);yield return null;
        }
        [UnityTest] public IEnumerator OtherHandCanPumpWhileToolHandCannotStealTheLever()
        {
            Grasp(0,true,0);Assert.IsFalse(input.OwnsControl);Assert.IsFalse(run.Supply.Locked);
            Grasp(1,true,0);Assert.IsTrue(input.EmptyOutlet);Assert.AreEqual(1,input.OperatingHand);
            Stroke(1,true,0);Assert.AreEqual(1,events);Assert.IsFalse(run.Supply.Locked);Assert.IsFalse(input.Detached);
            Assert.AreEqual(Vector3.right*2,tool.transform.position,"Empty lever use cannot snap another hand's tool");yield return null;
        }
        [UnityTest] public IEnumerator PartialJitterReleaseAndTrackingLossNeverEmitEmptyStroke()
        {
            Grasp(0);
            for(int i=0;i<20;i++){Sample(0,1,station.Lever.position-Vector3.up*.07f);Sample(0,1,station.Lever.position);}
            Assert.Zero(events);Sample(0,1,station.Lever.position-Vector3.up*.085f);Assert.IsTrue(input.EmptyLeverReturnRequired);
            Sample(0,0,station.Lever.position);Assert.Zero(events);Assert.IsFalse(input.OwnsControl);
            Grasp(0);Sample(0,1,station.Lever.position-Vector3.up*.085f);Sample(0,1,station.Lever.position,valid:false);
            Assert.Zero(events);Assert.IsFalse(input.OwnsControl);Assert.IsFalse(input.Interrupted);
            for(int i=0;i<10;i++)Sample(0,1,station.Lever.position);
            Assert.IsFalse(input.OwnsControl,"Recovery with a closed fist requires an open hand first");yield return null;
        }
        [UnityTest] public IEnumerator DockingAfterEmptyHalfStrokeCannotCreditTheNewTank()
        {
            run.StartStage();for(int i=0;i<100 && run.Water>0;i++)run.Tick(.1f,true);run.CancelTrigger();Assert.Zero(run.Water);
            Grasp(0);Sample(0,1,station.Lever.position-Vector3.up*.085f);Assert.IsTrue(input.EmptyLeverReturnRequired);
            for(int i=0;i<5;i++)run.Supply.SampleDock(WaterSourceKind.Hydrant,1,0,0,true,.05f);
            Assert.IsTrue(run.Supply.Locked);
            Sample(0,1,station.Lever.position,held:true,toolHand:1);
            Assert.IsFalse(input.OwnsControl);Assert.IsFalse(input.EmptyOutlet);Assert.Zero(run.Water);Assert.Zero(events);
            for(int i=0;i<8;i++)Sample(0,1,station.Lever.position,held:true,toolHand:1);
            Assert.Zero(run.Water);Assert.IsFalse(input.OwnsControl);
            Grasp(0,true,1);Stroke(0,true,1);Assert.AreEqual(12,run.Water);Assert.AreEqual(12,run.SuppliedWater);Assert.Zero(events);
            yield return null;
        }
        [UnityTest] public IEnumerator InvalidTimeOrChangedOwnershipCancelsEmptyHalfStroke()
        {
            Grasp(1);Sample(1,1,station.Lever.position-Vector3.up*.085f);
            Sample(1,1,station.Lever.position,dt:float.NaN);Assert.Zero(events);Assert.IsFalse(input.OwnsControl);
            Grasp(1);Sample(1,1,station.Lever.position-Vector3.up*.085f);
            Sample(1,1,station.Lever.position,held:true,toolHand:1);
            Assert.Zero(events);Assert.IsFalse(input.OwnsControl);Assert.IsFalse(run.Supply.Locked);Assert.IsFalse(input.Interrupted);yield return null;
        }
        [UnityTest] public IEnumerator ConsumeInputDropsEmptyStrokeAndRequiresFreshOpenHand()
        {
            Grasp(0);Sample(0,1,station.Lever.position-Vector3.up*.085f);input.ConsumeInput(run);
            Assert.IsFalse(input.EmptyOutlet);Assert.IsNull(input.ActiveView);
            for(int i=0;i<10;i++)Sample(0,1,station.Lever.position);
            Assert.Zero(events);Assert.IsFalse(input.OwnsControl);yield return null;
        }
        [UnityTest] public IEnumerator EmptyLeverDoesNotRequireAnyToolObject()
        {
            Object.Destroy(tool.gameObject);tool=null;
            Grasp(0);Stroke(0);Assert.AreEqual(1,events);Assert.IsTrue(input.EmptyOutlet);Assert.IsFalse(run.Supply.Locked);
            Assert.AreEqual(60,run.Water);Assert.Zero(run.SuppliedWater);yield return null;
        }
        [UnityTest] public IEnumerator ResetAlsoDiscardsConnectedHalfStrokeWithoutReleasingTheDock()
        {
            run.StartStage();for(int i=0;i<100 && run.Water>0;i++)run.Tick(.1f,true);run.CancelTrigger();Assert.Zero(run.Water);
            for(int i=0;i<5;i++)run.Supply.SampleDock(WaterSourceKind.Hydrant,1,0,0,true,.05f);
            Grasp(0);Sample(0,1,station.Lever.position-Vector3.up*.085f);Assert.IsTrue(run.Supply.LeverReturnRequired);
            input.Reset();Assert.IsTrue(run.Supply.Locked);Assert.IsFalse(run.Supply.LeverReturnRequired);
            for(int i=0;i<8;i++)Sample(0,1,station.Lever.position);
            Assert.Zero(run.Water);Assert.IsFalse(input.OwnsControl);
            Grasp(0);Sample(0,1,station.Lever.position);Assert.Zero(run.Water);
            Stroke(0);Assert.AreEqual(12,run.Water);Assert.Zero(events);yield return null;
        }
    }
}

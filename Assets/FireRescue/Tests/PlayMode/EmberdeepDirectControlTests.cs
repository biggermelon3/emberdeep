using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    public sealed class EmberdeepDirectControlTests
    {
        GameObject root;Material material;
        FireRun run;FireEquipmentView tool;EmberdeepWaterStationView station,creek;
        EmberdeepRefillInteraction input;
        [TearDown] public void Cleanup(){if(root)Object.DestroyImmediate(root);if(material)Object.DestroyImmediate(material);}
        void Setup(WaterStationTier tier,bool dock=true)
        {
            run=new FireRun();
            while(run.StationTier<tier)
            {
                while(run.Coins<run.StationUpgradeCost){Assert.IsTrue(run.StartStage());foreach(var t in run.Trees)if(t.State==TreeState.Burning)run.ApplyWater(t.Position,100,.1f);}
                Assert.IsTrue(run.BuyStationUpgrade());
            }
            run.StartStage();for(int i=0;i<100 && run.Water>0;i++)run.Tick(.1f,true);run.CancelTrigger();Assert.Zero(run.Water);
            root=new GameObject("Direct controls");material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            station=new GameObject("Station").AddComponent<EmberdeepWaterStationView>();station.transform.SetParent(root.transform);station.Build(material,material,material,false);station.SetTier(tier);
            creek=new GameObject("Creek").AddComponent<EmberdeepWaterStationView>();creek.transform.SetParent(root.transform);creek.Build(material,material,material,true);creek.transform.position=Vector3.right*2;
            tool=new GameObject("Tool").AddComponent<FireEquipmentView>();tool.transform.SetParent(root.transform);tool.Build(FireEquipmentId.Extinguisher,material,material,material);
            input=new EmberdeepRefillInteraction();
            if(dock){EmberdeepRefillInteraction.Snap(tool,station.Socket);for(int i=0;i<12;i++)run.Supply.SampleDock(WaterSourceKind.Hydrant,1,0,0,true,.025f);Assert.IsTrue(run.Supply.Locked);}
            else tool.transform.position=Vector3.right;
        }
        static EmberdeepRefillInteraction.Hand Hand(Vector3 point,float strength,bool valid=true)=>new(true,new Pose(point,Quaternion.identity),.04f,point)
            {UsesGrip=true,GripValid=valid,GripStrength=strength,GripContact=point,HasGripContact=true};
        void Sample(Vector3 point,float strength=0,bool valid=true,Quaternion? rotation=null)
        {
            var left=Hand(point,strength,valid);if(rotation.HasValue)left.Palm.rotation=rotation.Value;
            var right=Hand(Vector3.up*2,.5f);
            input.Process(run,tool,station,creek,true,1,left,right,.025f);
        }
        void Grab(Vector3 point){Sample(point);for(int i=0;i<4;i++)Sample(point,.5f);}
        [Test] public void LightGraspOperatesWellWithoutPinchingAndRequiresCompleteReturn()
        {
            Setup(WaterStationTier.LeverWell);Grab(station.Lever.position);Assert.AreEqual(EmberdeepRefillInteraction.ActionKind.Lever,input.CurrentAction);
            Sample(station.Lever.position-Vector3.up*.08f,.5f);Assert.Zero(run.Water);
            Sample(station.Lever.position,.5f);Assert.AreEqual(12,run.Water);
            Sample(station.Lever.position-Vector3.up*.08f,.5f);Sample(station.Lever.position,0);Assert.AreEqual(12,run.Water,"Letting go cannot finish a stroke");
        }
        [Test] public void MissingGripDoesNotFallbackToPinchOrResumeAnOldStroke()
        {
            Setup(WaterStationTier.LeverWell);Grab(station.Lever.position);Sample(station.Lever.position-Vector3.up*.08f,.5f);
            Sample(station.Lever.position,.5f,false);Assert.IsFalse(run.Supply.Locked);Assert.Zero(run.Water);
            for(int i=0;i<5;i++)Sample(station.Lever.position,1);Assert.IsFalse(input.OwnsControl);Assert.Zero(run.Water);
        }
        [Test] public void ValveNeedsRimMotionNotWristRotationOrTranslationAlongAxis()
        {
            Setup(WaterStationTier.RotaryHydrant);var rim=station.Valve.position+station.Valve.up*.045f;Grab(rim);
            Assert.AreEqual(EmberdeepRefillInteraction.ActionKind.Valve,input.CurrentAction);
            Sample(rim,.5f,true,Quaternion.Euler(0,0,70));Assert.IsFalse(run.Supply.ValveOpen);
            Sample(rim+station.Valve.forward*.02f,.5f);Assert.IsFalse(run.Supply.ValveOpen);
            for(int i=1;i<=5;i++)Sample(station.Valve.position+Quaternion.AngleAxis(i*10,station.Valve.forward)*station.Valve.up*.045f,.5f);
            Assert.IsTrue(run.Supply.ValveOpen);Assert.Greater(run.Water,0);
        }
        [Test] public void ButtonNeedsApproachAndPhysicalPressAndTogglesOncePerPress()
        {
            Setup(WaterStationTier.PushButton);var front=station.Button.TransformPoint(new Vector3(0,0,-.025f));var down=station.Button.TransformPoint(new Vector3(0,0,.01f));
            Sample(down);Assert.IsFalse(run.Supply.ValveOpen,"Starting inside the button cannot press it");
            Sample(front);Sample(down);Assert.IsTrue(run.Supply.ValveOpen);
            for(int i=0;i<8;i++)Sample(down);Assert.IsTrue(run.Supply.ValveOpen,"Holding cannot toggle repeatedly");
            Sample(front);Sample(front);Sample(down);Assert.IsFalse(run.Supply.ValveOpen);
        }
        [Test] public void CannonPumpRequiresPhysicalGraspAndCancelsOnTrackingLoss()
        {
            var pump=new FirePumpGesture();var pose=new Pose(Vector3.zero,Quaternion.identity);
            pump.SampleGrasp(true,0,Vector3.zero,pose,.025f);for(int i=0;i<4;i++)pump.SampleGrasp(true,.5f,Vector3.zero,pose,.025f);
            Assert.IsTrue(pump.Active);pump.SampleGrasp(true,.5f,Vector3.back*.09f,pose,.025f);Assert.Greater(pump.Pull01,.99f);
            pump.SampleGrasp(false,0,Vector3.back*.09f,pose,.025f);Assert.IsTrue(pump.Cancelled);Assert.IsFalse(pump.Released);
        }
    }
}

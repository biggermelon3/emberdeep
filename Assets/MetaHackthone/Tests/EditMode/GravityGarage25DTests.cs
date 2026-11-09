using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace PhysicalAssembly.Tests.Editor
{
    public sealed class GravityGarage25DEditTests
    {
        [Test] public void SourceDefaultsAndChallengeKeepUnrelatedValues()
        {
            var p=new GravityGarage25DParameters();Assert.AreEqual(1.35f,p.gap);Assert.AreEqual(.37f,p.frontWheel);Assert.AreEqual(.20f,p.rearWheel);Assert.AreEqual(13,typeof(GravityGarage25DParameters).GetFields().Length);
            p.frontWheel=.29f;p.rock2Enabled=false;p.cameraAngle=-12;p.zoom=1.12f;p.Challenge();Assert.AreEqual(1.65f,p.gap);Assert.AreEqual(.29f,p.frontWheel);Assert.IsFalse(p.rock2Enabled);Assert.AreEqual(-12,p.cameraAngle);Assert.AreEqual(1.12f,p.zoom);p.Validate();
        }
        [Test] public void JsonRoundTripAndInvalidImportAreAtomic()
        {
            var p=new GravityGarage25DParameters{showLabels=false,rock2Enabled=false};var json=GravityGarageLevelAsset.Export(p);var copy=GravityGarageLevelAsset.Import(json);Assert.AreEqual(json,GravityGarageLevelAsset.Export(copy));
            Assert.Throws<InvalidDataException>(()=>GravityGarageLevelAsset.Import(json.Replace("\"gap\": 1.35","\"gap\": 12.0")));Assert.Throws<InvalidDataException>(()=>GravityGarageLevelAsset.Import("{}"));Assert.Throws<InvalidDataException>(()=>GravityGarageLevelAsset.Import(json.Replace("\"schemaVersion\": 1","\"schemaVersion\": 2")));Assert.AreEqual(1.35f,p.gap);
            Assert.Throws<InvalidDataException>(()=>GravityGarageLevelAsset.Import(json.Replace("\"showLabels\": false","\"showLabels\": \"false\"")));Assert.Throws<InvalidDataException>(()=>GravityGarageLevelAsset.Import(json.Replace("\"frontWheel\": 0.37","\"frontWheel\": \"0.37\"")));
        }
        [Test] public void CurvesKeepGapAndUseGentleLaunchAndLanding()
        {
            var p=new GravityGarage25DParameters();Assert.AreEqual(5.45f,p.Upper(1.7f));Assert.That(p.Upper(5.15f),Is.EqualTo(3.45f).Within(.0001f));Assert.IsTrue(float.IsNaN(p.Upper(10)));Assert.That(p.Upper(16.35f),Is.EqualTo(2.96f).Within(.0001f));Assert.That(p.Lower(17.35f),Is.EqualTo(.76f).Within(.0001f));p.rockPos=10;Assert.AreEqual(9.05f,p.SupportedRockX);
            Assert.That(p.Upper(9.55f)-p.Upper(8.6f),Is.EqualTo(.09f).Within(.0001f));Assert.That(p.Upper(9.55f)-p.Upper(p.LandingX),Is.EqualTo(.15f).Within(.0001f));
        }
        [Test] public void CalibratedCourseKeepsCurrentCarAndRoundTripsSmallerObstacles()
        {
            var p=GravityGarage25DParameters.UnityCalibrated();Assert.AreEqual(.37f,p.frontWheel);Assert.AreEqual(.20f,p.rearWheel);p.Validate();
            var json=GravityGarageLevelAsset.Export(p);Assert.AreEqual(json,GravityGarageLevelAsset.Export(GravityGarageLevelAsset.Import(json)));
            p.gap=.2f;p.rockSize=p.rock2Size=.02f;p.Validate();p.gap=.19f;Assert.Throws<InvalidDataException>(()=>p.Validate());p.gap=.4f;p.rockSize=.01f;Assert.Throws<InvalidDataException>(()=>p.Validate());p.rockSize=.02f;p.rock2Size=.01f;Assert.Throws<InvalidDataException>(()=>p.Validate());
        }
        [TestCase(.16f,.16f)][TestCase(.44f,.44f)][TestCase(.16f,.44f)][TestCase(.44f,.16f)]
        public void WheelRangeUsesSharedGridWithoutOverlap(float front,float rear)
        {
            var root=new GameObject("Range test");try{var world=root.AddComponent<AssemblyWorld>();var p=new GravityGarage25DParameters{frontWheel=front,rearWheel=rear};var parts=GravityGarage25DVehicle.Create(world,p,new Pose(Vector3.zero,Quaternion.identity),.8f);Assert.AreEqual(8,parts[0].Sockets.Length);Assert.AreEqual(6,world.Connections.Count);Assert.IsTrue(parts.All(a=>a.GridVersion==9));Assert.IsTrue(parts.SelectMany(a=>a.GetComponentsInChildren<HingeJoint>()).All(j=>!j.useMotor));
                var wheels=parts.Where(a=>DownhillParts.IsWheel(a.Kind)).ToArray();foreach(var wheel in wheels){Assert.That(wheel.GetComponentInChildren<PassiveWheel>().Radius,Is.EqualTo(wheel.DesignWheelRadius*GravityGarage25DCourse.UnitScale*.8f).Within(.000001f));Assert.AreEqual(.003f,wheel.GetComponentInChildren<PassiveWheel>().Resistance);}
                var service=root.AddComponent<AssemblySaveService>();service.World=world;service.Factory=root.AddComponent<PartFactory>();service.Factory.World=world;var save=service.Capture(parts[0]);AssemblySaveService.Validate(save);var restored=service.Restore(save,new Pose(Vector3.up*5,Quaternion.identity));foreach(var restoredPart in restored)restoredPart.transform.SetParent(root.transform);Assert.AreEqual(7,restored.Count);Assert.AreEqual(12,world.Connections.Count);CollectionAssert.AreEquivalent(wheels.Select(w=>w.DesignWheelRadius),restored.Where(a=>DownhillParts.IsWheel(a.Kind)).Select(w=>w.DesignWheelRadius));
            }finally{Object.DestroyImmediate(root);}
        }
        [Test] public void LevelAssetSupportsUndoAndReloadWithoutRoundingDefaults()
        {
            string path=AssetDatabase.GenerateUniqueAssetPath("Assets/MetaHackthone/Tests/EditMode/Temporary25DLevel.asset");var asset=ScriptableObject.CreateInstance<GravityGarageLevelAsset>();
            try{AssetDatabase.CreateAsset(asset,path);Undo.RecordObject(asset,"Change level gap");asset.parameters.gap=2.6f;Undo.FlushUndoRecordObjects();Undo.PerformUndo();Assert.AreEqual(1.35f,asset.parameters.gap);asset.parameters.Challenge();EditorUtility.SetDirty(asset);AssetDatabase.SaveAssets();AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);var loaded=AssetDatabase.LoadAssetAtPath<GravityGarageLevelAsset>(path);Assert.AreEqual(1.65f,loaded.parameters.gap);Assert.AreEqual(GravityGarageLevelAsset.Export(asset.parameters),GravityGarageLevelAsset.Export(loaded.parameters));}
            finally{Undo.ClearUndo(asset);AssetDatabase.DeleteAsset(path);}
        }
        [Test] public void LegacyPuzzleWheelKeepsItsRadiusAndResistance()
        {
            var root=new GameObject("Legacy wheel");try{var world=root.AddComponent<AssemblyWorld>();var p=DownhillParts.Create(world,PartKind.DownhillLargeWheel,new Pose(Vector3.zero,Quaternion.identity),1,null,8);p.transform.SetParent(root.transform);Assert.AreEqual(.46f*GravityGarageDesign.UnitScale,p.GetComponentInChildren<PassiveWheel>().Radius);Assert.AreEqual(3,p.GetComponentInChildren<PassiveWheel>().Resistance);}finally{Object.DestroyImmediate(root);}
        }
    }
}

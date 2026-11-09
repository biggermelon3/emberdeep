using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace PhysicalAssembly.Tests
{
    public sealed class BuildingSceneAssetTests
    {
        [TestCase("Workshop",15)]
        [TestCase("NarrowGap",37)]
        [TestCase("DownhillSandbox",9)]
        public void SavedScenesContainEditableGeometryAndPersistentReferences(string name,int count)
        {
            var scene=EditorSceneManager.OpenPreviewScene($"Assets/MetaHackthone/Scenes/{name}.unity");
            try
            {
                var layouts=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BuildingSceneLayout>(true)).ToArray();
                Assert.That(layouts.Length,Is.EqualTo(1)); var layout=layouts[0];
                Assert.IsTrue(layout.Authored); Assert.NotNull(layout.Menu); Assert.NotNull(layout.StatusLabel); Assert.NotNull(layout.Surface);
                Assert.That(layout.Spawns.Count(spawn=>spawn),Is.EqualTo(count));
                Assert.That(layout.GetComponentsInChildren<AssemblyPart>(true),Is.Empty,"Previews must not run building physics.");
                foreach(var spawn in layout.Spawns.Where(spawn=>spawn))
                {
                    Assert.NotNull(spawn); Assert.NotNull(spawn.Preview); Assert.AreEqual("EditorOnly",spawn.Preview.tag);
                }
                foreach(var filter in layout.GetComponentsInChildren<UnityEngine.MeshFilter>(true))
                {
                    Assert.NotNull(filter.sharedMesh,filter.name); Assert.IsTrue(AssetDatabase.Contains(filter.sharedMesh),filter.name);
                }
                foreach(var renderer in layout.GetComponentsInChildren<UnityEngine.Renderer>(true))
                    foreach(var material in renderer.sharedMaterials) { Assert.NotNull(material,renderer.name); Assert.IsTrue(AssetDatabase.Contains(material),renderer.name); }
                foreach(var button in layout.GetComponentsInChildren<WorkshopButton>(true))
                {
                    Assert.IsNotEmpty(button.Command);
                    Assert.NotNull(button.GetComponentInChildren<UnityEngine.TextMesh>(),"Button text must move with its control.");
                }
                if(layout.Downhill)
                {
                    Assert.NotNull(layout.Downhill.WorkbenchFrame); Assert.NotNull(layout.Downhill.SandboxFrame);
                    Assert.NotNull(layout.Downhill.StartPoint); Assert.NotNull(layout.Downhill.FinishArea);
                    Assert.NotNull(layout.Downhill.ModeButton); Assert.NotNull(layout.Downhill.RotationWheel);
                    var presentation=layout.Downhill.GetComponent<DownhillPresentation>();
                    Assert.NotNull(presentation); Assert.NotNull(presentation.ReadyButton); Assert.NotNull(presentation.RetryButton);
                    Assert.NotNull(presentation.BackButton); Assert.NotNull(presentation.ExitButton); Assert.NotNull(presentation.Countdown);
                    Assert.NotNull(presentation.ReadyButton.GetComponent<FloatingStartButton>());
                    Assert.NotNull(presentation.Interface);
                    Assert.NotNull(presentation.Interface.CoursePreview);
                    Assert.NotNull(presentation.Interface.ResetButton);
                    Assert.IsFalse(presentation.Interface.Root.IsChildOf(layout.Downhill.SandboxFrame));
                    Assert.AreEqual("Sandbox Quit",presentation.ExitButton.Command);
                    Assert.AreEqual("START",presentation.ReadyButton.GetComponentInChildren<UnityEngine.TextMesh>().text);
                    Assert.That(presentation.WorkbenchVisuals.localScale.x,Is.EqualTo(.5f).Within(.0001f));
                    var pad=presentation.FindGridElement("Centered grid pad");
                    Assert.AreEqual("CenteredGridPad",pad.GetComponent<UnityEngine.MeshFilter>().sharedMesh.name);
                    Assert.Greater(pad.GetComponent<UnityEngine.MeshFilter>().sharedMesh.subMeshCount,1);
                    Assert.AreEqual(1,pad.GetComponentsInChildren<UnityEngine.MeshRenderer>(true).Length);
                    Assert.That(pad.lossyScale.x,Is.EqualTo(pad.lossyScale.z).Within(.0001f));
                    var group=presentation.WorkbenchVisuals.Find("Core direction grid");
                    Assert.NotNull(group);
                    Assert.IsTrue(pad.IsChildOf(group));
                    Assert.IsTrue(layout.Downhill.RotationWheel.transform.IsChildOf(group));
                    Assert.IsNull(group.Find("FRONT direction marking"));
                    Assert.IsNull(group.Find("Grid X")); Assert.IsNull(group.Find("Grid Z"));
                    Assert.IsNull(group.Find("Circular grid border"));
                }
                if(layout.NarrowGap)
                {
                    Assert.NotNull(layout.NarrowGap.PadCollider); Assert.NotNull(layout.NarrowGap.DockPoint);
                    Assert.NotNull(layout.NarrowGap.GateOpening); Assert.NotNull(layout.NarrowGap.TargetArea);
                    Assert.That(layout.NarrowGap.GetComponentsInChildren<UnityEngine.MeshFilter>(true).Length,Is.GreaterThan(80),"Keep obstacles individually editable.");
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}

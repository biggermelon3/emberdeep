using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PhysicalAssembly.Tests
{
    public sealed class DesignLayoutTests
    {
        [Test] public void SavedWorkshopHasEditableDesignCarSpawn()
        {
            var scene=EditorSceneManager.OpenPreviewScene("Assets/MetaHackthone/Scenes/Workshop.unity");
            try
            {
                var layout=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BuildingSceneLayout>(true)).Single();
                Assert.NotNull(layout.DesignCarSpawn);
                Assert.IsTrue(layout.DesignCarSpawn.IsChildOf(layout.transform));
                Assert.Greater(layout.DesignCarSpawn.position.y,layout.Surface.bounds.max.y);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        [Test] public void AuthoredCourseControlsAndRegionsSurviveInitializationAndTestReturn()
        {
            var scene=EditorSceneManager.OpenPreviewScene("Assets/MetaHackthone/Scenes/DownhillSandbox.unity");
            try
            {
                var layout=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BuildingSceneLayout>(true)).Single();
                var course=layout.Downhill; var presentation=course.GetComponent<DownhillPresentation>();
                var settings=AssetDatabase.LoadAssetAtPath<DownhillSettings>("Assets/MetaHackthone/Generated/DownhillSettings.asset");
                course.SandboxFrame.localPosition+=new Vector3(.2f,.11f,.17f);
                course.SandboxFrame.localRotation=Quaternion.Euler(0,12,0);
                course.SandboxFrame.localScale*=1.1f;
                var position=course.SandboxFrame.localPosition; var rotation=course.SandboxFrame.localRotation; var scale=course.SandboxFrame.localScale;
                presentation.BuildControls.localPosition+=Vector3.left*.13f;
                presentation.TestControls.localPosition+=Vector3.up*.14f;
                presentation.Countdown.transform.localPosition+=Vector3.up*.18f;
                course.StatusLabel.transform.localPosition+=Vector3.forward*.09f;
                presentation.WorkbenchVisuals.localScale=new Vector3(.6f,1,.7f);
                course.StartPoint.localPosition+=Vector3.right*.06f;
                course.FinishArea.Size+=Vector3.right*.1f;
                course.PlayArea.Size+=Vector3.forward*.15f;
                var controls=presentation.BuildControls.localPosition; var test=presentation.TestControls.localPosition;
                var countdown=presentation.Countdown.transform.localPosition; var status=course.StatusLabel.transform.localPosition;
                var start=course.StartPoint.localPosition; var finish=course.FinishArea.Size; var safe=course.PlayArea.Size;
                int colliders=layout.GetComponentsInChildren<Collider>(true).Length;
                presentation.Initialize(course,settings,layout);
                for(int i=0;i<3;i++)
                {
                    presentation.Configure();
                    presentation.SetCoursePosition(presentation.TestPosition);
                    presentation.Initialize(course,settings,layout);
                    presentation.Configure();
                    Assert.Less(Vector3.Distance(position,course.SandboxFrame.localPosition),.00001f);
                    Assert.Less(Quaternion.Angle(rotation,course.SandboxFrame.localRotation),.001f);
                    Assert.AreEqual(scale,course.SandboxFrame.localScale);
                }
                Assert.AreEqual(controls,presentation.BuildControls.localPosition); Assert.AreEqual(test,presentation.TestControls.localPosition);
                Assert.AreEqual(countdown,presentation.Countdown.transform.localPosition); Assert.AreEqual(status,course.StatusLabel.transform.localPosition);
                Assert.AreEqual(new Vector3(.6f,1,.7f),presentation.WorkbenchVisuals.localScale);
                Assert.AreEqual(start,course.StartPoint.localPosition); Assert.AreEqual(finish,course.FinishArea.Size); Assert.AreEqual(safe,course.PlayArea.Size);
                Assert.AreEqual(colliders,layout.GetComponentsInChildren<Collider>(true).Length);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}

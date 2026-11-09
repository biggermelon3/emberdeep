using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PhysicalAssembly.Editor;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace PhysicalAssembly.Tests
{
    public sealed class Level2BezierTrackTests
    {
        Scene scene;Level2BezierTrack track;string asset;
        [SetUp] public void Open()
        {
            Level2TrackEditing.Suspended=true;
            scene=EditorSceneManager.OpenScene(SandboxSceneAuthoring.ScenePath,OpenSceneMode.Additive);
            track=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Level2BezierTrack>(true)).Single();
            asset="Assets/Level2BakeTest-"+Guid.NewGuid().ToString("N")+".asset";
            foreach(var strip in track.Strips) {
                var copy=Object.Instantiate(strip.Mesh);
                if(!AssetDatabase.LoadMainAssetAtPath(asset))AssetDatabase.CreateAsset(copy,asset);else AssetDatabase.AddObjectToAsset(copy,asset);
                strip.Mesh=copy;strip.Filter.sharedMesh=copy;strip.Collider.sharedMesh=copy;
            }
            track.MeshAssetPath=asset;
            Assert.IsTrue(Level2TrackBaker.Rebuild(track,true),track.ValidationError);
        }
        [TearDown] public void Close()
        {
            if(scene.IsValid() && scene.isLoaded)EditorSceneManager.CloseScene(scene,true);
            if(!string.IsNullOrEmpty(asset))AssetDatabase.DeleteAsset(asset);
            Level2TrackEditing.Suspended=false;
        }
        void MoveAnchor()
        {
            var knot=track.Curve.Spline[3];knot.Position+=new float3(.1f,.35f,.4f);track.Curve.Spline[3]=knot;
        }
        void CheckPathAndCollision()
        {
            track.Course.gameObject.SetActive(true);track.Course.BindAuthored();track.Course.SplineZone.RebuildPath();Physics.SyncTransforms();
            Assert.AreEqual(track.Length,track.Course.Length,.0001f);
            Assert.AreEqual(track.Length,track.Course.SplineZone.Length,.001f);
            for(float d=.1f;d<track.Length-.1f;d+=.25f) {
                var s=track.Sample(d);
                Assert.Less(Vector3.Distance(s.Position,track.Course.Sample(d).Position),.00001f);
                Assert.Less(Vector3.Distance(s.Position,track.Course.SplineZone.Sample(d).Position),.0001f);
                var ray=new Ray(s.Position+track.transform.up*track.Course.WorldScale,-track.transform.up);
                bool hit=false;
                foreach(var road in track.Course.Markers.Roads)if(road.Raycast(ray,out var h,track.Course.WorldScale*2)) {
                    Assert.Less(Vector3.Distance(h.point,s.Position),.002f*track.Course.WorldScale);hit=true;break;
                }
                Assert.IsTrue(hit,"No road collision at "+d);
            }
            foreach(var strip in track.Strips)Assert.AreSame(strip.Filter.sharedMesh,strip.Collider.sharedMesh);
        }
        [Test] public void AnchorAndTangentEditsUpdateRoadCollisionAndMovementTogether()
        {
            var before=track.Samples.ToArray();var meshes=track.Strips.Select(s=>s.Mesh.GetInstanceID()).ToArray();
            MoveAnchor();Assert.IsTrue(Level2TrackBaker.Rebuild(track),track.ValidationError);CheckPathAndCollision();
            Assert.IsFalse(before.SequenceEqual(track.Samples));
            var knot=track.Curve.Spline[4];knot.TangentOut*=1.4f;track.Curve.Spline[4]=knot;
            Assert.IsTrue(Level2TrackBaker.Rebuild(track),track.ValidationError);CheckPathAndCollision();
            CollectionAssert.AreEqual(meshes,track.Strips.Select(s=>s.Mesh.GetInstanceID()));
        }
        [UnityTest] public IEnumerator EditorUpdateBakesWithoutAnExplicitRebuildCall()
        {
            string before=track.BakedSignature;MoveAnchor();Level2TrackEditing.Suspended=false;
            double deadline=EditorApplication.timeSinceStartup+3;
            while(track.BakedSignature==before && EditorApplication.timeSinceStartup<deadline)yield return null;
            Level2TrackEditing.Suspended=true;
            Assert.AreNotEqual(before,track.BakedSignature);CheckPathAndCollision();
        }
        [UnityTest] public IEnumerator EditButtonSelectsCurveAndActivatesOfficialSplineTools()
        {
            track.gameObject.SetActive(true);Selection.activeGameObject=track.Course.gameObject;
            Level2TrackEditing.Edit(track);
            double deadline=EditorApplication.timeSinceStartup+3;
            while(UnityEditor.EditorTools.ToolManager.activeToolType.Name!="SplineMoveTool" && EditorApplication.timeSinceStartup<deadline)yield return null;
            Assert.AreSame(track.Curve.gameObject,Selection.activeGameObject);
            Assert.AreEqual("SplineToolContext",UnityEditor.EditorTools.ToolManager.activeContextType.Name);
            Assert.AreEqual("SplineMoveTool",UnityEditor.EditorTools.ToolManager.activeToolType.Name);
        }
        [Test] public void InsertionDeletionUndoRedoPreserveSectionBoundaries()
        {
            var spline=track.Curve.Spline;int count=spline.Count;
            var boundary=track.Curve.Spline.GetOrCreateFloatData(Level2BezierTrack.BoundaryData);
            Vector3 turn=track.Samples.First(p=>Vector3.Distance(p,track.SampleLocal(track.TurnDistance).Position)<.00001f);
            Undo.RegisterCompleteObjectUndo(track.Curve,"Insert track anchor");
            // This is the same split operation used by the official Scene tool (internal in Splines 2.9.2).
            typeof(Spline).GetMethod("InsertOnCurve",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(spline,new object[]{2,.5f});
            Assert.IsTrue(Level2TrackBaker.Rebuild(track,true),track.ValidationError);
            Assert.AreEqual(count+1,spline.Count);
            Assert.Less(Vector3.Distance(turn,track.SampleLocal(track.TurnDistance).Position),.002f);
            Undo.FlushUndoRecordObjects();Undo.PerformUndo();
            Assert.AreEqual(count,track.Curve.Spline.Count);Assert.IsTrue(Level2TrackBaker.Rebuild(track,true),track.ValidationError);
            Undo.PerformRedo();Assert.AreEqual(count+1,track.Curve.Spline.Count);
            Assert.IsTrue(Level2TrackBaker.Rebuild(track,true),track.ValidationError);
            track.Curve.Spline.RemoveAt(2);Assert.IsTrue(Level2TrackBaker.Rebuild(track,true),track.ValidationError);CheckPathAndCollision();
        }
        [Test] public void AttachmentsFollowAndWallTargetKeepsItsLocalAlignment()
        {
            var wall=track.Course.Wall.transform;var target=track.Course.Markers.WeakPoint;
            var local=target.localPosition;var rotation=target.localRotation;
            var knot=track.Curve.Spline[track.Curve.Spline.Count-3];knot.Position+=new float3(.2f,.4f,.3f);track.Curve.Spline[track.Curve.Spline.Count-3]=knot;
            var before=wall.position;Assert.IsTrue(Level2TrackBaker.Rebuild(track),track.ValidationError);
            Assert.Greater(Vector3.Distance(before,wall.position),.001f);
            Assert.Less(Vector3.Distance(local,target.localPosition),.000001f);Assert.Less(Quaternion.Angle(rotation,target.localRotation),.001f);
            foreach(var a in track.Attachments.Where(a=>a.UprightPost)) {
                Assert.Less(Vector3.Angle(a.Target.up,track.transform.up),.001f);
                Assert.AreEqual(a.BaseHeight,a.Target.localPosition.y-a.Target.localScale.y*.5f,.0001f);
            }
        }
        [Test] public void InvalidCurveRetainsLastGeometryAndRejectsBuild()
        {
            var vertices=track.Strips[0].Mesh.vertices;var samples=track.Samples.ToArray();
            track.Curve.Spline.Closed=true;
            LogAssert.Expect(LogType.Error,"Level 2 curve: The track must be open and have at least two anchors.");
            Assert.IsFalse(Level2TrackBaker.Rebuild(track));
            CollectionAssert.AreEqual(vertices,track.Strips[0].Mesh.vertices);CollectionAssert.AreEqual(samples,track.Samples);
            Assert.Throws<UnityEditor.Build.BuildFailedException>(()=>new Level2TrackBuild().OnProcessScene(scene,null));
            track.Curve.Spline.Closed=false;Assert.IsTrue(Level2TrackBaker.Rebuild(track,true),track.ValidationError);
        }
        [Test] public void SaveAndReopenPreservesEditedSplineMeshesAndPath()
        {
            MoveAnchor();Assert.IsTrue(Level2TrackBaker.Rebuild(track),track.ValidationError);
            string signature=track.BakedSignature;var samples=track.Samples.ToArray();
            string path=asset.Replace(".asset",".unity");
            try {
                AssetDatabase.SaveAssetIfDirty(track.Strips[0].Mesh);Assert.IsTrue(EditorSceneManager.SaveScene(scene,path,true));
                EditorSceneManager.CloseScene(scene,true);scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                track=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Level2BezierTrack>(true)).Single();
                Assert.AreEqual(signature,track.BakedSignature);Assert.AreEqual(signature,Level2TrackBaker.Signature(track));
                CollectionAssert.AreEqual(samples,track.Samples);CheckPathAndCollision();
            } finally {AssetDatabase.DeleteAsset(path);}
        }
        [Test] public void UnloadedInvalidSavedSceneIsRejectedWithoutRebaking()
        {
            string path=asset.Replace(".asset",".unity");
            try {
                track.Curve.Spline.Closed=true;EditorUtility.SetDirty(track.Curve);
                Assert.IsTrue(EditorSceneManager.SaveScene(scene,path,true));
                EditorSceneManager.CloseScene(scene,true);
                Assert.Throws<UnityEditor.Build.BuildFailedException>(()=>Level2TrackEditing.ValidateSavedSceneIfUnloaded(path));
            } finally {AssetDatabase.DeleteAsset(path);}
        }
    }
}

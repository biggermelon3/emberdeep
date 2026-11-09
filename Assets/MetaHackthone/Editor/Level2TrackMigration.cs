using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

namespace PhysicalAssembly.Editor
{
    public static class Level2TrackMigration
    {
        public const string MeshPath="Assets/MetaHackthone/Art/SceneLayouts/Level2BezierTrack.asset";
        [MenuItem("MetaHackthone/Downhill/Migrate Level 2 to Bezier (one time)")]
        public static void Migrate()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play before migration.");
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=SandboxSceneAuthoring.ScenePath)throw new InvalidOperationException("Open DownhillSandbox first.");
            var course=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<AuthoredSandboxLayout>(true)).Single().Level2;
            if(course.BezierTrack || course.GetComponent<Level2BezierTrack>() || AssetDatabase.LoadMainAssetAtPath(MeshPath))
                throw new InvalidOperationException("Level 2 is already migrated. Automatic reconversion is forbidden.");
            string backup=Path.GetFullPath("Backups/Level2Bezier/"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"/DownhillSandbox.unity");
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            // Save a copy, including unsaved scene edits, without replacing the active scene.
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Could not create scene backup.");
            course.BindAuthored();
            var original=course.RoutePoints;
            var roads=course.Markers.Roads.Cast<MeshCollider>().ToArray();
            if(roads.Length!=3 || original.Length<4)throw new InvalidOperationException("Expected three authored road sections.");
            var points=new List<Vector3>();var boundaries=new List<int>();
            foreach(var road in roads) {
                var vertices=road.sharedMesh.vertices;
                for(int i=0;i<vertices.Length;i+=2) {
                    var p=course.transform.InverseTransformPoint(road.transform.TransformPoint((vertices[i]+vertices[i+1])*.5f));
                    if(points.Count==0 || Vector3.Distance(points[points.Count-1],p)>.001f)points.Add(p);
                }
                boundaries.Add(points.Count-1);
            }
            var indices=Fit(points.ToArray(),boundaries[0],boundaries[1]);
            var spline=new Spline();
            Vector3 Direction(int i) => (points[Mathf.Min(i+1,points.Count-1)]-points[Mathf.Max(0,i-1)]).normalized;
            for(int i=0;i<indices.Count;i++) {
                int index=indices[i];
                float before=i==0?Vector3.Distance(points[index],points[indices[1]]):Vector3.Distance(points[index],points[indices[i-1]]);
                float after=i==indices.Count-1?before:Vector3.Distance(points[index],points[indices[i+1]]);
                var rotation=Quaternion.LookRotation(Direction(index),Vector3.up);
                spline.Add(new BezierKnot(points[index],new float3(0,0,-before/3),new float3(0,0,after/3),new quaternion(rotation.x,rotation.y,rotation.z,rotation.w)),TangentMode.Continuous);
            }
            var boundary=spline.GetOrCreateFloatData(Level2BezierTrack.BoundaryData);
            boundary.PathIndexUnit=PathIndexUnit.Knot;
            boundary.Add(indices.IndexOf(boundaries[0]),0);boundary.Add(indices.IndexOf(boundaries[1]),1);
            Level2TrackEditing.Suspended=true;
            try {
                var go=new GameObject("Track curve - edit anchors and tangents");go.transform.SetParent(course.transform,false);
                var container=go.AddComponent<SplineContainer>();container.Spline=spline;
                var track=course.gameObject.AddComponent<Level2BezierTrack>();
                track.Course=course;track.Curve=container;track.DeckWidth=course.Level.DeckWidth;track.RailHeight=course.Level.RailHeight;
                track.MeshAssetPath=MeshPath;
                var path=Level2TrackBaker.SampleCurve(track);
                float error=MaximumError(points.ToArray(),path);
                if(error>.025f)throw new InvalidOperationException($"Curve fit error {error:F6} exceeds 0.025 course units. Restore {backup}");
                var strips=new List<TrackStrip>();
                for(int section=0;section<3;section++) {
                    foreach(var collider in roads[section].transform.parent.GetComponentsInChildren<MeshCollider>(true)) {
                        bool road=collider==roads[section];
                        if(!road && collider.name!="Physical guardrail")continue;
                        int side=0;
                        if(!road) {
                            var v=collider.sharedMesh.vertices;
                            var p=course.transform.InverseTransformPoint(collider.transform.TransformPoint(v[0]));
                            int first=section==0?0:boundaries[section-1];
                            side=Vector3.Dot(p-points[first],Vector3.Cross(Vector3.up,Direction(first)))<0?-1:1;
                        }
                        var mesh=UnityEngine.Object.Instantiate(collider.sharedMesh);mesh.name=$"Level2 section {section} {(side==0?"road":"rail "+side)}";
                        if(strips.Count==0)AssetDatabase.CreateAsset(mesh,MeshPath);else AssetDatabase.AddObjectToAsset(mesh,MeshPath);
                        var filter=collider.GetComponent<MeshFilter>();
                        strips.Add(new TrackStrip{Filter=filter,Collider=collider,Mesh=mesh,Section=section,Side=side});
                    }
                }
                track.Strips=strips.ToArray();
                var attachments=new List<TrackAttachment>();
                var data=spline.GetOrCreateFloatData(Level2BezierTrack.AttachmentData);data.PathIndexUnit=PathIndexUnit.Knot;
                void Attach(Transform target,bool post=false,float? boundaryIndex=null) {
                    if(!target || attachments.Any(a=>a.Target==target))return;
                    var p=course.transform.InverseTransformPoint(target.position);
                    if(post)p.y+=target.localScale.y*.5f;
                    var sample=course.SampleLocal(course.Project(course.transform.TransformPoint(p)).Distance);
                    var frame=Quaternion.LookRotation(sample.Tangent,sample.Normal);
                    float parameter=boundaryIndex??path.ParameterAt(path.Project(sample.Position));
                    data.Add(parameter,attachments.Count);
                    attachments.Add(new TrackAttachment{Target=target,Offset=Quaternion.Inverse(frame)*(p-sample.Position),
                        Rotation=Quaternion.Inverse(course.transform.rotation*frame)*target.rotation,UprightPost=post,
                        BaseHeight=target.localPosition.y-target.localScale.y*.5f,Scale=target.localScale});
                }
                var markers=course.Markers;
                Attach(markers.Start);Attach(markers.Finish);Attach(markers.Turn,false,boundary[0].Index);Attach(markers.Lower,false,boundary[1].Index);
                Attach(course.Wall.transform);Attach(course.SplineZone.Stop);
                foreach(Transform child in course.transform) {
                    if(child.name=="Start arch" || child.name=="Goal arch") {Attach(child);continue;}
                    if(!child.name.StartsWith("Scenery piece "))continue;
                    var scale=child.localScale;
                    bool post=Mathf.Abs(scale.x-.16f)<.001f && Mathf.Abs(scale.z-.16f)<.001f;
                    bool plank=Mathf.Abs(scale.x-track.DeckWidth)<.001f && Mathf.Abs(scale.y-.14f)<.001f && Mathf.Abs(scale.z-.025f)<.001f;
                    bool beam=Mathf.Abs(scale.x-track.DeckWidth-.3f)<.001f && Mathf.Abs(scale.y-.23f)<.001f && Mathf.Abs(scale.z-.2f)<.001f;
                    if(post || plank || beam)Attach(child,post);
                }
                track.Attachments=attachments.ToArray();
                track.CompatibilityFractions=markers.Route.Select(t=>course.Project(t.position).Distance/course.Length).ToArray();
                foreach(var marker in markers.Route) {
                    marker.hideFlags|=HideFlags.NotEditable;
                    marker.parent.name="Derived route samples - use Edit Track Curve";
                }
                course.BezierTrack=track;course.SplineZone.BezierTrack=track;
                if(!Level2TrackBaker.Rebuild(track,true))throw new InvalidOperationException(track.ValidationError);
                EditorUtility.SetDirty(course.SplineZone);EditorUtility.SetDirty(course);EditorUtility.SetDirty(container);
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save migrated scene.");
                Debug.Log($"LEVEL2_BEZIER_MIGRATED anchors={indices.Count}, samples={path.Points.Count}, maxError={error:F6}, attachments={attachments.Count}, backup={backup}");
                Level2TrackEditing.Edit(track);
            } finally {Level2TrackEditing.Suspended=false;}
        }

        static List<int> Fit(Vector3[] p,int turn,int lower)
        {
            var indices=new SortedSet<int>{0,turn,lower,p.Length-1};
            Vector3 Direction(int i)=>(p[Mathf.Min(i+1,p.Length-1)]-p[Mathf.Max(0,i-1)]).normalized;
            void Refine(int a,int b) {
                if(b<=a+1)return;
                float length=Vector3.Distance(p[a],p[b]);
                var curve=new BezierCurve(p[a],p[a]+Direction(a)*length/3,p[b]-Direction(b)*length/3,p[b]);
                float max=0;int split=(a+b)/2;float total=0;var d=new float[b-a+1];
                for(int i=a+1;i<=b;i++){total+=Vector3.Distance(p[i-1],p[i]);d[i-a]=total;}
                for(int i=a+1;i<b;i++) {
                    float error=Vector3.Distance(p[i],CurveUtility.EvaluatePosition(curve,d[i-a]/total));
                    if(error>max){max=error;split=i;}
                }
                if(max>.009f || total>3) {
                    if(max<=.009f)split=(a+b)/2;
                    indices.Add(split);Refine(a,split);Refine(split,b);
                }
            }
            Refine(0,turn);Refine(turn,lower);Refine(lower,p.Length-1);return indices.ToList();
        }
        public static float MaximumError(Vector3[] original,Level2TrackBaker.Path path)
        {
            float Error(Vector3 p,IList<Vector3> line) {
                float best=float.PositiveInfinity;
                for(int i=0;i<line.Count-1;i++) {
                    var delta=line[i+1]-line[i];float t=Mathf.Clamp01(Vector3.Dot(p-line[i],delta)/delta.sqrMagnitude);
                    best=Mathf.Min(best,Vector3.Distance(p,line[i]+t*delta));
                }
                return best;
            }
            float max=path.Points.Max(p=>Error(p,original));
            // Include old segment interiors, not just the old vertices.
            for(int i=0;i<original.Length-1;i++)for(int j=0;j<=4;j++)max=Mathf.Max(max,Error(Vector3.Lerp(original[i],original[i+1],j/4f),path.Points));
            return max;
        }
    }
}

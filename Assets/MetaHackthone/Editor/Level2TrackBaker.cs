using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

namespace PhysicalAssembly.Editor
{
    public static class Level2TrackBaker
    {
        public sealed class Path
        {
            public readonly List<Vector3> Points = new();
            public readonly List<float> Parameters = new();
            public readonly List<float> Distances = new();
            public float Length => Distances[Distances.Count - 1];
            public float DistanceAt(float parameter)
            {
                int i = Parameters.BinarySearch(parameter);
                if (i >= 0) return Distances[i];
                i = Mathf.Clamp(~i - 1, 0, Parameters.Count - 2);
                return Mathf.Lerp(Distances[i], Distances[i + 1], Mathf.InverseLerp(Parameters[i], Parameters[i + 1], parameter));
            }
            public float ParameterAt(float distance)
            {
                int i = Distances.BinarySearch(distance);
                if (i >= 0) return Parameters[i];
                i = Mathf.Clamp(~i - 1, 0, Distances.Count - 2);
                return Mathf.Lerp(Parameters[i], Parameters[i + 1], Mathf.InverseLerp(Distances[i], Distances[i + 1], distance));
            }
            public float Project(Vector3 point)
            {
                float best = float.PositiveInfinity, d = 0;
                for(int i=0;i<Points.Count-1;i++) {
                    var delta=Points[i+1]-Points[i];
                    float t=Mathf.Clamp01(Vector3.Dot(point-Points[i],delta)/delta.sqrMagnitude);
                    float error=(point-Points[i]-delta*t).sqrMagnitude;
                    if(error<best) {best=error;d=Mathf.Lerp(Distances[i],Distances[i+1],t);}
                }
                return d;
            }
        }

        static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
        public static string Signature(Level2BezierTrack track)
        {
            if(!track.Curve) return "missing-curve";
            var text=new StringBuilder("Level2Bake-v2|");
            text.Append(JsonUtility.ToJson(track.Curve));
            void Matrix(Transform t) {
                if(!t) {text.Append("missing");return;}
                var m=track.transform.worldToLocalMatrix*t.localToWorldMatrix;
                for(int i=0;i<16;i++) text.Append('|').Append(m[i].ToString("F5",CultureInfo.InvariantCulture));
            }
            Matrix(track.Curve.transform);
            foreach(var s in track.Strips) Matrix(s.Filter?s.Filter.transform:null);
            return Hash128.Compute(text.ToString()).ToString();
        }

        public static Path SampleCurve(Level2BezierTrack track)
        {
            if(!track.Curve || track.Curve.Splines.Count!=1) throw new InvalidOperationException("Use exactly one spline.");
            var spline=track.Curve.Spline;
            if(spline.Closed || spline.Count<2) throw new InvalidOperationException("The track must be open and have at least two anchors.");
            if(spline.Count>512) throw new InvalidOperationException("The track exceeds 512 anchors.");
            var matrix=track.transform.worldToLocalMatrix*track.Curve.transform.localToWorldMatrix;
            var path=new Path();
            var boundaries=spline.GetOrCreateFloatData(Level2BezierTrack.BoundaryData);
            Vector3 Position(int curve,float t) => matrix.MultiplyPoint3x4(CurveUtility.EvaluatePosition(spline.GetCurve(curve),t));
            void Add(int curve,float t) {
                Vector3 p=Position(curve,t);
                if(!Finite(p) || p.sqrMagnitude>1e8f) throw new InvalidOperationException("Anchor or tangent coordinates are invalid or too large.");
                if(path.Points.Count>0) {
                    var delta=p-path.Points[path.Points.Count-1];
                    if(delta.sqrMagnitude<1e-12f) throw new InvalidOperationException("The curve has coincident points or a collapsed segment.");
                    if(Vector3.Cross(Vector3.up,delta.normalized).sqrMagnitude<.0001f) throw new InvalidOperationException("Vertical track segments have no road width direction.");
                    path.Distances.Add(path.Length+delta.magnitude);
                } else path.Distances.Add(0);
                path.Points.Add(p);path.Parameters.Add(curve+t);
                if(path.Points.Count>50000) throw new InvalidOperationException("The curve is too long or complex to bake.");
            }
            void Subdivide(int curve,float a,float b,int depth) {
                var p=Position(curve,a);var q=Position(curve,b);
                float mid=(a+b)*.5f;
                float deviation=Mathf.Max(Vector3.Distance(Position(curve,mid),Vector3.Lerp(p,q,.5f)),
                    Mathf.Max(Vector3.Distance(Position(curve,Mathf.Lerp(a,b,.25f)),Vector3.Lerp(p,q,.25f)),Vector3.Distance(Position(curve,Mathf.Lerp(a,b,.75f)),Vector3.Lerp(p,q,.75f))));
                if(Vector3.Distance(p,q)>.05f || deviation>.001f) {
                    if(depth>=18) throw new InvalidOperationException("The curve could not be sampled safely.");
                    Subdivide(curve,a,mid,depth+1);Subdivide(curve,mid,b,depth+1);
                } else Add(curve,b);
            }
            Add(0,0);
            for(int i=0;i<spline.Count-1;i++) {
                if(Vector3.Distance((Vector3)spline[i].Position,(Vector3)spline[i+1].Position)<.001f)
                    throw new InvalidOperationException("Adjacent anchors must be at least 0.001 course units apart.");
                var cuts=new List<float>{0,1};
                foreach(var b in boundaries) if(b.Index>i+1e-5f && b.Index<i+1-1e-5f) cuts.Add(b.Index-i);
                cuts.Sort();
                for(int j=1;j<cuts.Count;j++) Subdivide(i,cuts[j-1],cuts[j],0);
            }
            if(path.Length<.1f) throw new InvalidOperationException("The track is too short.");
            return path;
        }

        public static bool Rebuild(Level2BezierTrack track, bool force=false)
        {
            if(Application.isPlaying) return true;
            string signature=Signature(track);
            if(!force && signature==track.BakedSignature && string.IsNullOrEmpty(track.ValidationError)) return true;
            var temporary=new List<Mesh>();
            try {
                var path=SampleCurve(track);
                if(!track.Course || !track.Course.Markers || !track.Course.SplineZone ||
                    Enumerable.Range(0,3).Any(section=>track.Strips.Count(s=>s.Section==section && s.Side==0)!=1))
                    throw new InvalidOperationException("Track bindings are incomplete. Restore the migration backup.");
                var boundary=track.Curve.Spline.GetOrCreateFloatData(Level2BezierTrack.BoundaryData);
                if(boundary.Count!=2 || boundary[0].Index<=0 || boundary[1].Index>=track.Curve.Spline.Count-1 || boundary[0].Index>=boundary[1].Index)
                    throw new InvalidOperationException("Keep the upper, connection area and lower sections nonempty. Undo the boundary anchor deletion.");
                int[] cuts={0,NearestParameter(path,boundary[0].Index),NearestParameter(path,boundary[1].Index),path.Points.Count-1};
                if(cuts[1]==cuts[0] || cuts[2]<=cuts[1] || cuts[3]<=cuts[2]) throw new InvalidOperationException("A road section is empty.");
                var data=track.Curve.Spline.GetOrCreateFloatData(Level2BezierTrack.AttachmentData);
                if(data.Count!=track.Attachments.Length || track.Attachments.Any(a=>!a.Target) ||
                    data.Select(p=>Mathf.RoundToInt(p.Value)).Distinct().Count()!=track.Attachments.Length ||
                    data.Any(p=>p.Value<0 || p.Value>=track.Attachments.Length)) throw new InvalidOperationException("An attached track object is missing. Undo its deletion.");
                if(track.CompatibilityFractions.Length!=track.Course.Markers.Route.Length || track.Course.Markers.Route.Any(t=>!t))
                    throw new InvalidOperationException("Derived route samples are missing. Undo their deletion.");
                foreach(var strip in track.Strips) {
                    if(!strip.Filter || !strip.Collider || !strip.Mesh || AssetDatabase.GetAssetPath(strip.Mesh)!=track.MeshAssetPath)
                        throw new InvalidOperationException("Road geometry must use the dedicated Level 2 mesh asset.");
                    temporary.Add(CreateStrip(track,path,strip,cuts[strip.Section],cuts[strip.Section+1]));
                }
                // Validate everything before changing any persistent geometry.
                for(int i=0;i<track.Strips.Length;i++) {
                    var s=track.Strips[i];var mesh=temporary[i];
                    s.Collider.sharedMesh=null;
                    // Static roads must retain valleys and hills, not a convex hull.
                    s.Collider.convex=false;
                    s.Mesh.Clear();s.Mesh.indexFormat=mesh.indexFormat;
                    s.Mesh.vertices=mesh.vertices;s.Mesh.triangles=mesh.triangles;s.Mesh.normals=mesh.normals;s.Mesh.bounds=mesh.bounds;
                    s.Filter.sharedMesh=s.Mesh;s.Collider.sharedMesh=s.Mesh;
                    EditorUtility.SetDirty(s.Mesh);EditorUtility.SetDirty(s.Filter);EditorUtility.SetDirty(s.Collider);
                }
                track.Samples=path.Points.ToArray();track.Distances=path.Distances.ToArray();
                track.TurnDistance=path.Distances[cuts[1]];track.LowerDistance=path.Distances[cuts[2]];
                foreach(var point in data) {
                    var a=track.Attachments[Mathf.RoundToInt(point.Value)];
                    var s=track.SampleLocal(path.DistanceAt(point.Index));
                    var rotation=Quaternion.LookRotation(s.Tangent,s.Normal);
                    var position=s.Position+rotation*a.Offset;
                    if(a.UprightPost) {
                        // Posts keep a fixed ground elevation and remain upright in course space.
                        float top=position.y;
                        position.y=(top+a.BaseHeight)*.5f;
                        a.Target.SetPositionAndRotation(track.transform.TransformPoint(position),track.transform.rotation);
                        var scale=a.Scale;scale.y=Mathf.Max(.01f,top-a.BaseHeight);a.Target.localScale=scale;
                    } else a.Target.SetPositionAndRotation(track.transform.TransformPoint(position),track.transform.rotation*rotation*a.Rotation);
                    EditorUtility.SetDirty(a.Target);
                }
                var route=track.Course.Markers.Route;
                for(int i=0;i<route.Length;i++) {
                    var s=track.Sample(track.CompatibilityFractions[i]*track.Length);
                    route[i].SetPositionAndRotation(s.Position,Quaternion.LookRotation(s.Tangent,s.Normal));EditorUtility.SetDirty(route[i]);
                }
                track.Course.BindAuthored();track.Course.SplineZone.RebuildPath();
                track.ValidationError="";track.BakedSignature=Signature(track);
                EditorUtility.SetDirty(track);EditorUtility.SetDirty(track.Course);
                EditorSceneManager.MarkSceneDirty(track.gameObject.scene);
                Physics.SyncTransforms();SceneView.RepaintAll();return true;
            } catch(Exception e) {
                if(track.ValidationError!=e.Message) Debug.LogError("Level 2 curve: "+e.Message,track);
                track.ValidationError=e.Message;EditorUtility.SetDirty(track);return false;
            } finally { foreach(var mesh in temporary) UnityEngine.Object.DestroyImmediate(mesh); }
        }

        static int NearestParameter(Path path,float parameter)
        {
            int i=path.Parameters.BinarySearch(parameter);
            if(i>=0)return i;i=~i;
            return i==path.Parameters.Count?i-1:i>0 && Mathf.Abs(path.Parameters[i-1]-parameter)<Mathf.Abs(path.Parameters[i]-parameter)?i-1:i;
        }
        static Mesh CreateStrip(Level2BezierTrack track,Path path,TrackStrip strip,int start,int end)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();int stride=strip.Side==0?3:4;
            var matrix=strip.Filter.transform.worldToLocalMatrix*track.transform.localToWorldMatrix;
            for(int i=start;i<=end;i++) {
                // Shared cross section at each seam, independent of section object pivots.
                var tangent=(path.Points[Mathf.Min(i+1,path.Points.Count-1)]-path.Points[Mathf.Max(0,i-1)]).normalized;
                var right=Vector3.Cross(Vector3.up,tangent).normalized;
                var p=path.Points[i];
                void Vertex(float side,float height) => vertices.Add(matrix.MultiplyPoint3x4(p+right*side+Vector3.up*height));
                // A center vertex makes every baked path segment an actual collider edge,
                // including when 3D edits make a road quad non-planar.
                if(strip.Side==0) {Vertex(-track.DeckWidth*.5f,0);Vertex(0,0);Vertex(track.DeckWidth*.5f,0);}
                else {
                    float edge=strip.Side*track.DeckWidth*.5f,outside=edge+strip.Side*.1f;
                    Vertex(edge,0);Vertex(edge,track.RailHeight);Vertex(outside,track.RailHeight);Vertex(outside,0);
                }
            }
            for(int i=0;i<end-start;i++) {
                int a=i*stride;
                if(stride==3) triangles.AddRange(new[]{a,a+3,a+1,a+1,a+3,a+4,a+1,a+4,a+2,a+2,a+4,a+5});
                else for(int j=0;j<4;j++) {
                    int x=a+j,y=a+4+j,z=a+(j+1)%4,w=a+4+(j+1)%4;
                    triangles.AddRange(strip.Side>0?new[]{x,y,z,z,y,w}:new[]{x,z,y,z,w,y});
                }
            }
            var mesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}

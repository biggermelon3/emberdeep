using System;
using System.Collections.Generic;
using System.Linq;
using CloudGarden.Integration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace PhysicalAssembly.Editor
{
    public static class SandboxSceneAuthoring
    {
        public const string ScenePath="Assets/MetaHackthone/Scenes/DownhillSandbox.unity";
        public static void Open(DesignLevelId level)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=ScenePath) {
                if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene=EditorSceneManager.OpenScene(ScenePath);
            }
            var authored=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<AuthoredSandboxLayout>(true)).Single();
            Undo.RecordObject(authored,"Select level to edit");
            Undo.RecordObjects(new Object[]{authored.Level1.gameObject,authored.Level2.gameObject},"Show course");
            var spawns=authored.GetComponent<BuildingSceneLayout>().Spawns;
            Undo.RecordObjects(spawns.Where(s=>s).Select(s=>(Object)s.gameObject).ToArray(),"Show level parts");
            authored.EditorLevel=level;authored.Show(level);
            EditorSceneManager.MarkSceneDirty(scene);
            var target=level==DesignLevelId.GravityGarage?authored.Level1.transform:authored.Level2.transform;
            Selection.activeGameObject=target.gameObject;
            var view=SceneView.lastActiveSceneView??EditorWindow.GetWindow<SceneView>();
            var renderers=target.GetComponentsInChildren<Renderer>();
            var bounds=renderers[0].bounds;foreach(var r in renderers) bounds.Encapsulate(r.bounds);
            view.Frame(bounds,false);view.Show();view.Focus();
        }
        // Explicit one-time conversion only. Never called by Open, OnValidate, or Play.
        public static void ConvertCurrentScene()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=ScenePath) throw new InvalidOperationException("Open DownhillSandbox first.");
            var entry=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DownhillEntry>(true)).Single();
            var layout=entry.Layout;var frame=layout.Downhill.SandboxFrame;
            if(layout.GetComponent<AuthoredSandboxLayout>()) throw new InvalidOperationException("Scene is already authored; conversion would overwrite edits.");
            foreach(Transform child in frame) if(child!=layout.Downhill.StartPoint && child!=layout.Downhill.FinishArea.transform && child!=layout.Downhill.PlayArea.transform) child.gameObject.SetActive(false);
            var authored=layout.gameObject.AddComponent<AuthoredSandboxLayout>();
            float worldScale=GravityGarage25DCourse.UnitScale*DownhillSettings.VehicleScale*entry.Settings.PresentationScale;

            var first=new GameObject("Level 1 - Gravity Garage");first.transform.SetParent(frame,false);
            var temp=new GameObject("Conversion owner");var owner=temp.AddComponent<DownhillCourse>();owner.SandboxFrame=first.transform;
            owner.StartPoint=Marker(first.transform,"START - vehicle surface pose",Vector3.zero,Quaternion.identity);
            var garage=first.AddComponent<GravityGarageCourse>();
            garage.Rebuild(owner,JsonUtility.FromJson<GravityGarageDesign>(JsonUtility.ToJson(entry.Settings.Garage)),GravityGarageDesign.UnitScale*DownhillSettings.VehicleScale*entry.Settings.PresentationScale);
            var markers=first.AddComponent<CourseLayoutMarkers>();
            markers.Start=owner.StartPoint;
            var roads=first.GetComponentsInChildren<Collider>().Where(c=>c.name.StartsWith("Garage road") || c.name=="Garage landing").ToArray();
            markers.Roads=roads;
            // Move the release marker with its supporting road.
            markers.Start.SetParent(roads[0].transform,true);
            markers.Jump=Marker(roads.First(c=>c.name=="Garage road 4").transform,"JUMP checkpoint",garage.Point(new Vector2(GravityGarageDesign.Edge+.2f,garage.Design.LandingY)),Quaternion.LookRotation(first.transform.right,first.transform.up));
            markers.Finish=Marker(garage.Landing.transform,"FINISH - stable landing",garage.Point(new Vector2(garage.Design.GoalX,garage.Design.LandingY)),Quaternion.LookRotation(first.transform.right,first.transform.up));
            markers.SafeRegion=Area(first.transform,"Safe region (edit bounds)",new Bounds(new Vector3(0,1.2f,.65f),new Vector3(8,4,5)));
            garage.Markers=markers;authored.Level1=garage;
            Object.DestroyImmediate(temp);

            var second=new GameObject("Level 2 - Waterfall Valley");second.transform.SetParent(frame,false);
            float scale=worldScale/frame.lossyScale.x;
            second.transform.localPosition=new Vector3(0,.25f,.65f+(entry.Settings.Level3D.Radius+entry.Settings.Level3D.DeckWidth*.5f)*scale);
            var valley=second.AddComponent<GravityGarage3DCourse>();valley.Build(entry.Settings.Level3D,scale);
            BakeValley(valley);authored.Level2=valley;
            authored.EditorLevel=DesignLevelId.GravityGarage;authored.Show(authored.EditorLevel);
            BuildingSceneAuthoring.PersistResources(layout,"AuthoredSandboxCourses");
            garage.ReleaseResourceOwnership();valley.ReleaseResourceOwnership();
            foreach(var scenery in layout.GetComponentsInChildren<ValleyEnvironment>(true)) scenery.ReleaseResourceOwnership();
            EditorUtility.SetDirty(authored);EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save authored sandbox.");
        }
        static Transform Marker(Transform parent,string name,Vector3 position,Quaternion rotation)
        {
            var t=new GameObject(name).transform;t.SetParent(parent,false);t.SetPositionAndRotation(position,rotation);return t;
        }
        static BoxCollider Area(Transform parent,string name,Bounds bounds)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);var box=go.AddComponent<BoxCollider>();box.center=bounds.center;box.size=bounds.size;box.isTrigger=true;box.enabled=false;return box;
        }
        static void BakeValley(GravityGarage3DCourse course)
        {
            // Split the baked road and rails into independently movable upper/turn/lower groups.
            var segments=new[]{new GameObject("Upper road section").transform,new GameObject("Connection area").transform,new GameObject("Lower road section").transform};
            foreach(var t in segments)t.SetParent(course.transform,false);
            int[] cuts={0,112,208,320};
            var strips=course.GetComponentsInChildren<MeshCollider>().Where(c=>c.name=="Continuous wooden road" || c.name=="Physical guardrail").ToArray();
            var roads=new List<Collider>();
            foreach(var original in strips) {
                var mesh=original.sharedMesh;int stride=original.name=="Continuous wooden road"?2:4;
                for(int section=0;section<3;section++) {
                    int start=cuts[section]*stride,end=(cuts[section+1]+1)*stride;
                    var triangles=new List<int>();var source=mesh.triangles;
                    for(int i=0;i<source.Length;i+=3) if(source[i]>=start && source[i]<end && source[i+1]>=start && source[i+1]<end && source[i+2]>=start && source[i+2]<end)
                        triangles.AddRange(new[]{source[i]-start,source[i+1]-start,source[i+2]-start});
                    var part=new Mesh{name=original.name+" "+section};part.vertices=mesh.vertices.Skip(start).Take(end-start).ToArray();part.triangles=triangles.ToArray();part.RecalculateNormals();part.RecalculateBounds();
                    var go=new GameObject(original.name);go.transform.SetParent(segments[section],false);
                    go.AddComponent<MeshFilter>().sharedMesh=part;go.AddComponent<MeshRenderer>().sharedMaterials=original.GetComponent<Renderer>().sharedMaterials;
                    var collider=go.AddComponent<MeshCollider>();collider.sharedMesh=part;collider.sharedMaterial=original.sharedMaterial;
                    if(stride==2)roads.Add(collider);
                }
                Object.DestroyImmediate(original.gameObject);
            }
            var markers=course.gameObject.AddComponent<CourseLayoutMarkers>();markers.Roads=roads.ToArray();
            var nodes=new List<Transform>();
            var route=course.RoutePoints;
            float[] breaks={0,course.TurnStart,course.LowerStart,course.Length};
            for(int section=0;section<3;section++) {
                var group=new GameObject("Route samples - move with road").transform;group.SetParent(segments[section],false);
                int count=cuts[section+1]-cuts[section];
                for(int i=section==0?0:1;i<=count;i++) {
                    // Preserve the original exact samples, including the varying upper slope.
                    int index=cuts[section]+i;
                    var position=course.transform.TransformPoint(route[index]);
                    var tangent=course.transform.TransformDirection(route[Mathf.Min(index+1,route.Length-1)]-route[Mathf.Max(0,index-1)]).normalized;
                    nodes.Add(Marker(group,"Route "+nodes.Count,position,Quaternion.LookRotation(tangent,course.transform.up)));
                }
            }
            markers.Route=nodes.ToArray();
            Transform At(float distance,Transform parent,string name) {var p=course.Sample(distance);return Marker(parent,name,p.Position,Quaternion.LookRotation(p.Tangent,p.Normal));}
            markers.Start=At(2,segments[0],"START - vehicle surface pose");
            markers.Turn=At(course.TurnStart,segments[1],"CONNECTION ENTRANCE");
            markers.Lower=At(course.LowerStart,segments[2],"CONNECTION EXIT");
            markers.Finish=At(course.Length-1,segments[2],"FINISH - stable arrival");
            course.Wall.transform.SetParent(segments[2],true);
            markers.WeakPoint=Marker(course.Wall.transform,"DRILL target - forward is travel",course.transform.TransformPoint(course.WeakPoint)+course.transform.right*(.12f*course.WorldScale),Quaternion.LookRotation(-course.transform.right,course.transform.up));
            markers.SafeRegion=Area(course.transform,"Safe region (edit bounds)",new Bounds(new Vector3(0,9,0),new Vector3(60,22,40)));
            markers.WaterRegions=course.WaterVolumes.Select((b,i)=>Area(course.transform,"Water hazard "+i,b)).ToArray();
            // Center each section's pivot without changing geometry or marker world positions.
            foreach(var section in segments) {
                var children=section.Cast<Transform>().ToArray();var road=section.GetComponentInChildren<MeshCollider>();var center=road.bounds.center;
                foreach(var child in children)child.SetParent(course.transform,true);
                section.position=center;
                foreach(var child in children)child.SetParent(section,true);
            }
            course.Markers=markers;course.BindAuthored();
        }
    }

    [CustomEditor(typeof(AuthoredSandboxLayout))]
    public sealed class AuthoredSandboxLayoutEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var layout=(AuthoredSandboxLayout)target;
            EditorGUILayout.HelpBox("Level 1 uses its saved road sections. Level 2 uses Edit Track Curve for anchors and tangent handles. Save with Ctrl+S outside Play mode; playing uses the saved geometry.",MessageType.Info);
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode)) {
                if(GUILayout.Button("Edit Level 1"))SandboxSceneAuthoring.Open(DesignLevelId.GravityGarage);
                if(GUILayout.Button("Edit Level 2"))SandboxSceneAuthoring.Open(DesignLevelId.WaterfallValley);
                if(layout.Level2)Level2TrackEditing.DrawButton(layout.Level2);
                if(GUILayout.Button("Test selected level"))DesignModeEditor.Play(layout.GetComponent<BuildingSceneLayout>(),layout.EditorLevel);
            }
            DrawDefaultInspector();
        }
    }
}

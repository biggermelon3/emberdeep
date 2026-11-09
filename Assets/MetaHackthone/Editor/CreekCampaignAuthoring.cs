using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace PhysicalAssembly.Editor
{
    public static class CreekCampaignAuthoring
    {
        const string Art="Assets/CreekDiorama/";
        const string Output="Assets/MetaHackthone/Art/CreekCampaign";
        static Material Mat(string name)=>AssetDatabase.LoadAssetAtPath<Material>(Art+"Materials/"+name+".mat");
        static GameObject Prefab(string name)=>AssetDatabase.LoadAssetAtPath<GameObject>(Art+"Prefabs/"+name+".prefab");

        [MenuItem("MetaHackthone/Sandbox/Align Level 1 pillars")]
        public static void AlignFirstPillars()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first.");
            var layout=Object.FindFirstObjectByType<AuthoredSandboxLayout>();
            if(!layout || layout.gameObject.scene.name!="DownhillSandbox")throw new InvalidOperationException("Open DownhillSandbox.");
            var theme=layout.Level1.GetComponent<CreekCampaignTheme>();
            var poses=theme.Visuals.Cast<Transform>().ToDictionary(t=>t.name,t=>(t.localPosition,t.localRotation,t.localScale));
            var before=CreekCampaignValidation.PhysicsSnapshot(layout);
            ApplyCourse(layout.Level1.transform,"Level1");
            foreach(Transform t in theme.Visuals)if(poses.TryGetValue(t.name,out var pose)) {
                t.localPosition=pose.Item1;t.localRotation=pose.Item2;t.localScale=pose.Item3;
            }
            CreekCampaignValidation.AssertUnchanged(layout,before);
            EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(layout.gameObject.scene);
        }

        static bool IsPillar(MeshRenderer r)
        {
            var s=r.transform.localScale;var p=r.transform.localPosition;
            return Near(r.sharedMaterial.color,new Color(.27f,.16f,.08f)) &&
                Mathf.Abs(s.x-.16f)<.001f && Mathf.Abs(s.z-.16f)<.001f && Mathf.Abs(Mathf.Abs(p.z)-1.05f)<.001f;
        }

        static void AddFirstPillars(Transform course,Transform visual,MeshRenderer[] sources,GameObject plank,Dictionary<Material,List<CombineInstance>> buckets)
        {
            var pillars=sources.Where(IsPillar).OrderBy(r=>r.transform.localPosition.x).ThenBy(r=>r.transform.localPosition.z).ToArray();
            var roads=course.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("Garage road ")||f.name=="Garage landing").OrderBy(f=>f.name=="Garage landing"?"z":f.name).ToArray();
            int[] pairs={1,2,1,1,1,4};
            if(roads.Length!=6||pillars.Length!=20)throw new InvalidOperationException("Unexpected Level 1 support layout.");
            int index=0;
            for(int segment=0;segment<roads.Length;segment++)for(int pair=0;pair<pairs[segment];pair++)for(int side=0;side<2;side++) {
                var source=pillars[index++].GetComponent<MeshFilter>();var road=roads[segment];var b=road.sharedMesh.bounds;
                var top=visual.InverseTransformPoint(road.transform.TransformPoint(new Vector3(Mathf.Lerp(b.min.x,b.max.x,(pair+.5f)/pairs[segment]),b.min.y,Mathf.Lerp(b.min.z,b.max.z,side==0?.08f:.92f))));
                var basePoint=visual.InverseTransformPoint(source.transform.TransformPoint(new Vector3(0,-.5f,0)));
                var width=visual.InverseTransformVector(source.transform.TransformVector(Vector3.right)).magnitude;
                float height=top.y-basePoint.y;
                if(height<=0)throw new InvalidOperationException("Pillar base above track.");
                AddFitted(plank,visual,new Bounds(new Vector3(top.x,(top.y+basePoint.y)*.5f,top.z),new Vector3(width,height,width)),Quaternion.identity,buckets);
            }
        }

        [MenuItem("MetaHackthone/Sandbox/Apply Beautiful Corner visuals")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            var layout=Object.FindFirstObjectByType<AuthoredSandboxLayout>();
            if(!layout || layout.gameObject.scene.name!="DownhillSandbox")throw new InvalidOperationException("Open DownhillSandbox.");
            Directory.CreateDirectory(Output); Directory.CreateDirectory("Logs/CreekCampaign");AssetDatabase.Refresh();
            var before=CreekCampaignValidation.PhysicsSnapshot(layout);
            File.WriteAllText("Logs/CreekCampaign/physics-before.txt",before);
            ApplyCourse(layout.Level1.transform,"Level1");ApplyCourse(layout.Level2.transform,"Level2");
            CreekCampaignValidation.AssertUnchanged(layout,before);
            File.WriteAllText("Logs/CreekCampaign/physics-after.txt",CreekCampaignValidation.PhysicsSnapshot(layout));
            EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(layout.gameObject.scene);
        }

        static void ApplyCourse(Transform course,string id)
        {
            var theme=course.GetComponent<CreekCampaignTheme>();
            if(!theme)theme=course.gameObject.AddComponent<CreekCampaignTheme>();
            if(theme.Visuals)Object.DestroyImmediate(theme.Visuals.gameObject);
            if(theme.ReplacedRenderers!=null)foreach(var r in theme.ReplacedRenderers)if(r)r.enabled=true;
            theme.Plank=Prefab("BridgePlank");theme.Boulder=Prefab("RiverBoulder");theme.Pine=Prefab("PineTree");
            theme.Terrace=Prefab("Modules/Cliff_Terrace_040");theme.Support=Prefab("Modules/Support_Pier_040");
            if(!theme.Plank||!theme.Boulder||!theme.Pine||!theme.Terrace||!theme.Support)throw new InvalidOperationException("Creek source prefab missing.");
            string waterPath=Output+"/Campaign water.mat";
            var water=AssetDatabase.LoadAssetAtPath<Material>(waterPath);
            if(!water){water=new Material(Mat("Modular river"));AssetDatabase.CreateAsset(water,waterPath);}
            water.SetFloat("_UseDepth",0);water.SetFloat("_WorldSpace",1);EditorUtility.SetDirty(water);theme.Water=water;
            var visual=new GameObject("Beautiful Corner scenery").transform;visual.SetParent(course,false);theme.Visuals=visual;
            var replaced=new List<Renderer>();var buckets=new Dictionary<Material,List<CombineInstance>>();
            var sources=course.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name.StartsWith("Scenery piece ")).ToArray();
            var pillarBuckets=new Dictionary<Material,List<CombineInstance>>();
            if(id=="Level1")AddFirstPillars(course,visual,sources,theme.Plank,pillarBuckets);
            var leaves=sources.Where(r=>Near(r.sharedMaterial.color,new Color(.09f,.24f,.18f))).ToArray();
            // Each old tree has three conical leaf tiers. Replace the entire crown once.
            foreach(var group in leaves.GroupBy(r=>new Vector2(r.transform.localPosition.x,r.transform.localPosition.z))) {
                var items=group.ToArray();var bounds=LocalBounds(visual,items);
                bounds.SetMinMax(new Vector3(bounds.min.x,bounds.min.y-bounds.size.y*.16f,bounds.min.z),bounds.max);
                AddFitted(theme.Pine,visual,bounds,Quaternion.identity,buckets);
                foreach(var r in items){r.enabled=false;replaced.Add(r);}
            }
            foreach(var r in sources.Except(leaves)) {
                if(id=="Level1" && IsPillar(r)){r.enabled=false;replaced.Add(r);continue;}
                var c=r.sharedMaterial.color;var f=r.GetComponent<MeshFilter>();
                var bounds=LocalBounds(visual,new[]{r});
                if(Near(c,new Color(.30f,.36f,.35f)) || Near(c,new Color(.35f,.48f,.49f))) {
                    // Preserve the low display foundation; replace individual stone formations.
                    if(bounds.size.x>bounds.size.y*12) AddMesh(f,visual,Mat("Basalt enamel"),buckets);
                    else AddFitted(theme.Boulder,visual,bounds,Quaternion.identity,buckets);
                } else if(Near(c,new Color(.27f,.41f,.20f))) {
                    var terraceBounds=bounds;
                    terraceBounds.SetMinMax(bounds.min-Vector3.up*Mathf.Min(bounds.size.x,bounds.size.z)*.22f,bounds.max);
                    AddFitted(theme.Terrace,visual,terraceBounds,Quaternion.identity,buckets);
                } else {
                    // Timber slats and supports already follow the exact authored route.
                    // Fit the art in each source's own axes rather than its world AABB.
                    AddInSourceFrame(theme.Plank,f,visual,buckets);
                }
                r.enabled=false;replaced.Add(r);
            }
            foreach(var r in course.GetComponentsInChildren<MeshRenderer>(true)) {
                if(r.name=="Source pool, spillway, curtain and descending stream") SplitWater(r,water,id);
                if(r.name=="River beneath the bridge")r.sharedMaterial=water;
                if(r.name.StartsWith("Garage road") || r.name=="Garage landing" || r.name=="Continuous wooden road")r.sharedMaterial=Mat("Honey timber 02");
                if(r.name=="Physical guardrail")r.sharedMaterial=Mat("Honey timber 00");
                if(r.name=="Garage rock A" || r.name=="Garage rock B" || r.name=="Drill wall")r.sharedMaterial=Mat("Slate rock 02");
            }
            int index=0;
            foreach(var entry in buckets.OrderBy(p=>p.Key.name)) {
                var mesh=new Mesh{name=id+" "+entry.Key.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(entry.Value.ToArray());mesh.RecalculateBounds();
                string path=Output+"/"+id+"_"+index+++".asset";
                var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;EditorUtility.SetDirty(mesh);}else AssetDatabase.CreateAsset(mesh,path);
                var go=new GameObject(entry.Key.name);go.transform.SetParent(visual,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=entry.Key;
            }
            theme.ReplacedRenderers=replaced.ToArray();EditorUtility.SetDirty(theme);
            foreach(var entry in pillarBuckets) {
                var mesh=new Mesh{name="Level1 pillars "+entry.Key.name};mesh.CombineMeshes(entry.Value.ToArray());mesh.RecalculateBounds();
                string path=Output+"/Level1_Pillars_"+entry.Key.name+".asset";
                var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;EditorUtility.SetDirty(mesh);}else AssetDatabase.CreateAsset(mesh,path);
                var go=new GameObject("Pillars - "+entry.Key.name);go.transform.SetParent(visual,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=entry.Key;
            }
            if(visual.GetComponentsInChildren<Collider>(true).Length!=0)throw new InvalidOperationException("Decorations must not collide.");
        }
        static bool Near(Color a,Color b)=>Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)<.02f;
        static void SplitWater(MeshRenderer renderer,Material water,string id)
        {
            var filter=renderer.GetComponent<MeshFilter>();var mesh=Object.Instantiate(filter.sharedMesh);
            var vertices=mesh.vertices;var triangles=mesh.triangles;
            var horizontal=new List<int>();var falling=new List<int>();
            for(int i=0;i<triangles.Length;i+=3) {
                var n=Vector3.Cross(vertices[triangles[i+1]]-vertices[triangles[i]],vertices[triangles[i+2]]-vertices[triangles[i]]).normalized;
                var list=Mathf.Abs(n.y)<.5f?falling:horizontal;
                list.Add(triangles[i]);list.Add(triangles[i+1]);list.Add(triangles[i+2]);
            }
            mesh.name=id+" creek and waterfall";mesh.subMeshCount=2;mesh.SetTriangles(horizontal,0);mesh.SetTriangles(falling,1);
            if(falling.Count>0) {
                var uv=mesh.uv;float top=falling.Max(i=>vertices[i].y),bottom=falling.Min(i=>vertices[i].y);
                foreach(int i in falling.Distinct())uv[i].y=Mathf.InverseLerp(top,bottom,vertices[i].y)*.98f;
                mesh.uv=uv;
            }
            string path=Output+"/"+id+"_Water.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;EditorUtility.SetDirty(mesh);}else AssetDatabase.CreateAsset(mesh,path);
            filter.sharedMesh=mesh;renderer.sharedMaterials=new[]{water,Mat("Silk waterfall")};
        }
        static Bounds LocalBounds(Transform frame,IEnumerable<Renderer> renderers)
        {
            var b=new Bounds();bool first=true;
            foreach(var r in renderers) {
                var f=r.GetComponent<MeshFilter>();if(!f||!f.sharedMesh)continue;var mb=f.sharedMesh.bounds;
                for(int i=0;i<8;i++) {var p=frame.InverseTransformPoint(f.transform.TransformPoint(mb.center+Vector3.Scale(mb.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));if(first){b=new Bounds(p,Vector3.zero);first=false;}else b.Encapsulate(p);}
            }return b;
        }
        static void AddFitted(GameObject prefab,Transform frame,Bounds target,Quaternion rotation,Dictionary<Material,List<CombineInstance>> buckets)
        {
            var b=LocalBounds(prefab.transform,prefab.GetComponentsInChildren<Renderer>(true));
            var scale=new Vector3(target.size.x/Mathf.Max(.00001f,b.size.x),target.size.y/Mathf.Max(.00001f,b.size.y),target.size.z/Mathf.Max(.00001f,b.size.z));
            AddPrefab(prefab,Matrix4x4.TRS(target.center,rotation,scale)*Matrix4x4.Translate(-b.center),buckets);
        }
        static void AddInSourceFrame(GameObject prefab,MeshFilter source,Transform frame,Dictionary<Material,List<CombineInstance>> buckets)
        {
            var b=LocalBounds(prefab.transform,prefab.GetComponentsInChildren<Renderer>(true));var target=source.sharedMesh.bounds;
            var scale=new Vector3(target.size.x/b.size.x,target.size.y/b.size.y,target.size.z/b.size.z);
            var matrix=frame.worldToLocalMatrix*source.transform.localToWorldMatrix*Matrix4x4.Translate(target.center)*Matrix4x4.Scale(scale)*Matrix4x4.Translate(-b.center);
            AddPrefab(prefab,matrix,buckets);
        }
        static void AddPrefab(GameObject prefab,Matrix4x4 matrix,Dictionary<Material,List<CombineInstance>> buckets)
        {
            foreach(var f in prefab.GetComponentsInChildren<MeshFilter>(true)) {
                var materials=f.GetComponent<Renderer>().sharedMaterials;
                for(int i=0;i<f.sharedMesh.subMeshCount;i++)Add(materials[Mathf.Min(i,materials.Length-1)],new CombineInstance{mesh=f.sharedMesh,subMeshIndex=i,transform=matrix*prefab.transform.worldToLocalMatrix*f.transform.localToWorldMatrix},buckets);
            }
        }
        static void AddMesh(MeshFilter f,Transform frame,Material mat,Dictionary<Material,List<CombineInstance>> buckets)
            =>Add(mat,new CombineInstance{mesh=f.sharedMesh,transform=frame.worldToLocalMatrix*f.transform.localToWorldMatrix},buckets);
        static void Add(Material mat,CombineInstance item,Dictionary<Material,List<CombineInstance>> buckets)
        {if(!buckets.TryGetValue(mat,out var list))buckets.Add(mat,list=new());list.Add(item);}
    }
}

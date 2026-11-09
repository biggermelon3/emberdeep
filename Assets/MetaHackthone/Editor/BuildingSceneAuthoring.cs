using System;
using System.IO;
using System.Linq;
using CloudGarden.Integration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace PhysicalAssembly.Editor
{
    public static class BuildingSceneAuthoring
    {
        [MenuItem("MetaHackthone/Scene editing/Convert building scenes (preserve existing layouts)")]
        public static void ConvertAll()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            for(int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before converting.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach(var name in new[]{"Workshop","NarrowGap","DownhillSandbox"})
                {
                    var scene=EditorSceneManager.OpenScene($"Assets/MetaHackthone/Scenes/{name}.unity");
                    if(ConvertScene(scene)) EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        public static void ProtectAuthoredScene(string path)
        {
            string guid=AssetDatabase.AssetPathToGUID("Assets/MetaHackthone/Runtime/BuildingSceneLayout.cs");
            if(File.Exists(path) && !string.IsNullOrEmpty(guid) && File.ReadAllText(path).Contains(guid))
                throw new InvalidOperationException("This scene has an editable layout. Open and edit it directly; the legacy generator cannot overwrite authored scenes: "+path);
        }
        public static bool ConvertScene(Scene scene)
        {
            var roots=scene.GetRootGameObjects();
            var owner=roots.SelectMany(r=>r.GetComponentsInChildren<WorkshopBootstrap>(true)).FirstOrDefault();
            var downhill=roots.SelectMany(r=>r.GetComponentsInChildren<DownhillEntry>(true)).FirstOrDefault();
            var narrow=roots.SelectMany(r=>r.GetComponentsInChildren<NarrowGapEntry>(true)).FirstOrDefault();
            if((owner && owner.Layout) || (downhill && downhill.Layout) || (narrow && narrow.Layout)) return false;
            if(!owner && !downhill && !narrow) throw new InvalidOperationException("No building-level entry in "+scene.path);
            GameObject temporary=null;
            try
            {
                if(!owner)
                {
                    temporary=new GameObject("Scene conversion"); temporary.SetActive(false);
                    owner=temporary.AddComponent<WorkshopBootstrap>();
                    if(downhill) owner.DownhillSettings=downhill.Settings;
                    else owner.ChallengeSettings=narrow.Settings;
                }
                var layout=owner.CreateSceneLayout(true);
                if(downhill) downhill.Layout=layout;
                if(narrow) narrow.Layout=layout;
                if(layout.Downhill) AuthorDownhill(layout.Downhill,owner.DownhillSettings);
                if(layout.NarrowGap) AuthorNarrowGap(layout.NarrowGap,owner.ChallengeSettings);
                CreatePreviews(layout,owner.DownhillSettings);
                PersistResources(layout,string.IsNullOrEmpty(scene.name)?(downhill?"DownhillSandbox":narrow?"NarrowGap":"Workshop"):scene.name);
                EditorUtility.SetDirty(owner);
                if(downhill) EditorUtility.SetDirty(downhill);
                if(narrow) EditorUtility.SetDirty(narrow);
                EditorSceneManager.MarkSceneDirty(scene);
                return true;
            }
            finally { if(temporary) Object.DestroyImmediate(temporary); }
        }
        static Transform Anchor(Transform parent,string name,Vector3 position)
        {
            var t=new GameObject(name).transform; t.SetParent(parent,false); t.localPosition=position; return t;
        }
        static BuildingArea Area(Transform parent,string name,Bounds bounds)
        {
            var area=Anchor(parent,name,bounds.center).gameObject.AddComponent<BuildingArea>(); area.Size=bounds.size; return area;
        }
        static void AuthorDownhill(DownhillCourse c,DownhillSettings s)
        {
            if(!c.StartPoint) c.StartPoint=Anchor(c.SandboxFrame,"Simulation start",s.StartPosition);
            c.StartPoint.localRotation=s.StartRotation;
            c.FinishArea=Area(c.SandboxFrame,"Finish region",s.FinishBounds);
            // Match the existing lower, lateral and forward failure boundaries.
            c.PlayArea=Area(c.SandboxFrame,"Course safe region",new Bounds(new Vector3(-.075f,1.26f,.65f),new Vector3(3.15f,1f,.92f)));
            c.EnsureControls(s);
        }
        static void AuthorNarrowGap(NarrowGapCourse c,NarrowGapSettings s)
        {
            var pad=Anchor(c.transform,"Launch pad (move this group)",Vector3.zero);
            var gate=Anchor(c.transform,"Gate (move this group)",new Vector3(0,0,s.GateDistance));
            var target=Anchor(c.transform,"Landing platform (move this group)",new Vector3(0,0,s.TargetDistance));
            foreach(var child in c.transform.Cast<Transform>().ToArray())
            {
                string n=child.name;
                if(n.StartsWith("Pad ") || n=="Jump pad collider" || n=="Red spring pad" || n=="Coiled steel spring" || n=="Brass fixing") child.SetParent(pad,true);
                else if(n.StartsWith("Stake ") || n=="Solid wooden stake" || n=="Gap dimension") child.SetParent(gate,true);
                else if(n.StartsWith("Target ") || n.StartsWith("Flag") || n=="Red finish pennant" || child==c.SuccessStars) child.SetParent(target,true);
            }
            c.DockPoint=Anchor(pad,"Core docking point",new Vector3(0,s.PadHeight,0));
            c.GateOpening=Area(gate,"Valid opening",new Bounds(new Vector3(0,.5f+s.GateHeight*.5f,0),new Vector3(s.GapWidth,s.GateHeight,s.GateThickness)));
            var bounds=s.TargetBounds; bounds.center-=Vector3.forward*s.TargetDistance;
            c.TargetArea=Area(target,"Landing region",bounds);
            // Reuse the existing imported audio clips; generated AudioClip sample data cannot be saved as a subasset.
            Object.DestroyImmediate(c.LaunchSound); Object.DestroyImmediate(c.WinSound);
            c.LaunchSound=s.CoursePrefab.LaunchSound; c.WinSound=s.CoursePrefab.WinSound;
        }
        internal static void CreatePreviews(BuildingSceneLayout layout,DownhillSettings settings,bool wheelsOnly=false,bool missingOnly=false,bool coreOnly=false)
        {
            var temporary=new GameObject("Preview factory"); var world=temporary.AddComponent<AssemblyWorld>();
            try
            {
                foreach(var spawn in layout.Spawns)
                {
                    if(coreOnly && spawn!=layout.Spawns[0]) continue;
                    if(!spawn || (missingOnly && spawn.Preview)) continue;
                    if(wheelsOnly && !DownhillParts.IsWheel(spawn.Kind)) continue;
                    var part=settings?DownhillParts.Create(world,spawn.Kind,new Pose(Vector3.zero,Quaternion.identity),settings:settings,gridVersion:settings.DesignerBuilder?DownhillParts.DesignerGridVersion:-1,buildingCore:spawn==layout.Spawns[0]):PartFactory.BuildDefinition(spawn.Kind);
                    try
                    {
                        if(spawn.Preview) Object.DestroyImmediate(spawn.Preview);
                        var preview=new GameObject("Part preview (editor only)"); preview.tag="EditorOnly";
                        preview.transform.SetParent(spawn.transform,false); spawn.Preview=preview;
                        CopyVisuals(part.transform,preview.transform);
                        if(settings && settings.DesignerBuilder) part.GetComponent<DownhillPartResources>().ReleaseOwnership();
                    }
                    finally { Object.DestroyImmediate(part.gameObject); }
                }
            }
            finally { Object.DestroyImmediate(temporary); }
        }
        static void CopyVisuals(Transform source,Transform destination)
        {
            var filter=source.GetComponent<MeshFilter>(); var renderer=source.GetComponent<MeshRenderer>();
            if(filter && renderer)
            {
                destination.gameObject.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
                var copy=destination.gameObject.AddComponent<MeshRenderer>(); copy.sharedMaterials=renderer.sharedMaterials; copy.enabled=renderer.enabled;
            }
            foreach(Transform child in source)
            {
                if(child.GetComponent<AssemblySocket>()) continue;
                var copy=Anchor(destination,child.name,child.localPosition);
                copy.localRotation=child.localRotation; copy.localScale=child.localScale;
                CopyVisuals(child,copy);
            }
        }
        internal static void PersistResources(BuildingSceneLayout layout,string level)
        {
            const string folder="Assets/MetaHackthone/Art/SceneLayouts";
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            var container=ScriptableObject.CreateInstance<BuildingSceneResources>();
            AssetDatabase.CreateAsset(container,AssetDatabase.GenerateUniqueAssetPath($"{folder}/{level}.asset"));
            void Save(Object value)
            {
                if(value && !AssetDatabase.Contains(value)) AssetDatabase.AddObjectToAsset(value,container);
            }
            foreach(var renderer in layout.GetComponentsInChildren<Renderer>(true)) foreach(var material in renderer.sharedMaterials)
            {
                if(!material || AssetDatabase.Contains(material)) continue;
                if(material.HasProperty("_BaseMap")) Save(material.GetTexture("_BaseMap"));
                else if(material.HasProperty("_MainTex")) Save(material.GetTexture("_MainTex"));
                Save(material);
            }
            foreach(var mesh in layout.GetComponentsInChildren<MeshFilter>(true)) Save(mesh.sharedMesh);
            foreach(var collider in layout.GetComponentsInChildren<Collider>(true))
            {
                Save(collider.sharedMaterial);
                if(collider is MeshCollider mesh) Save(mesh.sharedMesh);
            }
            foreach(var tile in layout.GetComponentsInChildren<FloatingStartButton>(true)) tile.ReleaseGeneratedResourceOwnership();
            foreach(var ui in layout.GetComponentsInChildren<SandboxInterface>(true)) ui.ReleaseGeneratedResourceOwnership();
            if(layout.Downhill) layout.Downhill.ReleaseGeneratedMaterialOwnership();
            if(layout.Downhill && layout.Downhill.RotationWheel) layout.Downhill.RotationWheel.ReleaseGeneratedMeshOwnership();
            EditorUtility.SetDirty(container); AssetDatabase.SaveAssets();
        }
    }
}

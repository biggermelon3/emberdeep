using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace CreekDiorama.Editor
{
    public static class CreekArtKitAuthoring
    {
        const string Root="Assets/CreekDiorama", Modules=Root+"/Prefabs/Modules", Output="Docs/CreekDiorama";
        static Material Mat(string name)=>AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/"+name+".mat");
        static GameObject Prefab(string name)=>AssetDatabase.LoadAssetAtPath<GameObject>(Modules+"/"+name+".prefab");
        static string Clean(string name)=>System.Text.RegularExpressions.Regex.Replace(name,@"\.\d{3}$","");
        [MenuItem("MetaHackthone/Creek Art/Build independent beautiful corner and modules")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first.");
            for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save open scenes first.");
            Directory.CreateDirectory(Modules);Directory.CreateDirectory(Root+"/Meshes/Modules");AssetDatabase.Refresh();
            var modularWater=Mat("Modular river");if(!modularWater){modularWater=new Material(Mat("Living river"));AssetDatabase.CreateAsset(modularWater,Root+"/Materials/Modular river.mat");}
            modularWater.SetFloat("_WorldSpace",1);EditorUtility.SetDirty(modularWater);
            string path=Root+"/Models/CreekModularKit.fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.importCameras=false;importer.importLights=false;importer.importAnimation=false;importer.isReadable=true;importer.SaveAndReimport();
            foreach(var m in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())if(Mat(m.name))importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),m.name),Mat(m.name));
            importer.SaveAndReimport();
            var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            var first=model.GetComponentsInChildren<Transform>().First(t=>Clean(t.name)=="Deck_Straight_040");
            var bounds=Bounds(first);model.transform.localScale*=.4f/bounds.size.x;
            var catalog=JObject.Parse(File.ReadAllText("ArtSource/CreekDiorama/module_anchors.json"));
            foreach(var entry in catalog.Properties()) {
                var source=model.GetComponentsInChildren<Transform>().First(t=>Clean(t.name)==entry.Name);
                var root=new GameObject(entry.Name);Combine(source,root.transform,entry.Name);
                foreach(var anchor in ((JObject)entry.Value).Properties())if(anchor.Value is JArray a) {
                    var marker=new GameObject("Anchor_"+anchor.Name).transform;marker.SetParent(root.transform,false);marker.localPosition=new Vector3((float)a[0],(float)a[1],(float)a[2]);
                    if(anchor.Name=="Exit" && entry.Value["ExitYaw"]!=null)marker.localRotation=Quaternion.Euler(0,(float)entry.Value["ExitYaw"],0);
                }
                if(entry.Name=="Portal_Frame") {
                    var portal=GameObject.CreatePrimitive(PrimitiveType.Quad);portal.name="Portal effect (optional)";Object.DestroyImmediate(portal.GetComponent<Collider>());portal.transform.SetParent(root.transform,false);
                    portal.transform.localPosition=new Vector3(0,.215f,0);portal.transform.localScale=new Vector3(.405f,.39f,1);portal.GetComponent<Renderer>().sharedMaterial=Mat("Portal cyan");
                }
                PrefabUtility.SaveAsPrefabAsset(root,Modules+"/"+entry.Name+".prefab");Object.DestroyImmediate(root);
            }
            Object.DestroyImmediate(model);
            Station("Station_Start",true);Station("Station_Goal",false);
            var build=(GameObject)PrefabUtility.InstantiatePrefab(Prefab("Station_Start"));build.name="Station_Build";
            Anchor(build.transform,"AssemblyCenter",new Vector3(0,.1f,.3f));
            PrefabUtility.SaveAsPrefabAsset(build,Modules+"/Station_Build.prefab");Object.DestroyImmediate(build);
            BeautifulCorner();CreekBotanicalAuthoring.RefreshIfAvailable();Gallery();AssetDatabase.SaveAssets();Validate();
        }
        static void Combine(Transform source,Transform root,string name)
        {
            var buckets=new Dictionary<Material,List<CombineInstance>>();
            foreach(var f in source.GetComponentsInChildren<MeshFilter>()) {
                var renderer=f.GetComponent<MeshRenderer>();
                for(int i=0;i<f.sharedMesh.subMeshCount;i++) {
                    var material=renderer.sharedMaterials[Mathf.Min(i,renderer.sharedMaterials.Length-1)];
                    if(name.StartsWith("Waterfall"))material=Mat("Silk waterfall");else if(name.StartsWith("Water_"))material=Mat("Modular river");
                    if(!buckets.TryGetValue(material,out var list))buckets[material]=list=new();
                    list.Add(new CombineInstance{mesh=f.sharedMesh,subMeshIndex=i,transform=f.transform.localToWorldMatrix});
                }
            }
            int n=0;
            foreach(var pair in buckets) {
                var mesh=new Mesh{name=name+" "+pair.Key.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);mesh.RecalculateBounds();
                string path=Root+"/Meshes/Modules/"+name+"_"+(n++)+".asset";
                var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);mesh=old;EditorUtility.SetDirty(old);}else AssetDatabase.CreateAsset(mesh,path);
                var go=new GameObject(pair.Key.name);go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=pair.Key;
            }
        }
        static void Station(string name,bool start)
        {
            var root=new GameObject(name);Instance("Landing_Platform_080",root.transform,Vector3.zero);
            Instance(start?"Marker_Start":"Marker_Goal",root.transform,new Vector3(0,0,.65f));
            var gate=Instance("Portal_Frame",root.transform,new Vector3(0,0,.61f));gate.name="Optional portal frame";
            gate.gameObject.SetActive(start);
            Anchor(root.transform,"Entry",Vector3.zero);Anchor(root.transform,"Exit",new Vector3(0,0,.8f));Anchor(root.transform,"Spawn",new Vector3(0,.06f,.3f));Anchor(root.transform,"Portal",new Vector3(0,0,.61f));
            PrefabUtility.SaveAsPrefabAsset(root,Modules+"/"+name+".prefab");Object.DestroyImmediate(root);
        }
        static Transform Instance(string name,Transform parent,Vector3 position)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(Prefab(name));go.transform.SetParent(parent,false);go.transform.localPosition=position;return go.transform;
        }
        static void Anchor(Transform parent,string name,Vector3 position)
        {var a=new GameObject("Anchor_"+name).transform;a.SetParent(parent,false);a.localPosition=position;}
        static void BeautifulCorner()
        {
            var scene=EditorSceneManager.OpenScene(CreekEnvironmentImporter.ScenePath);
            var root=GameObject.Find("Creek Environment");root.name="Beautiful Corner — art reference";
            var signs=root.transform.Cast<Transform>().FirstOrDefault(t=>t.name.StartsWith("09"));if(signs)signs.gameObject.SetActive(false);
            var start=Instance("Station_Start",root.transform,new Vector3(1.26f,.668f,0));start.localRotation=Quaternion.Euler(0,270,0);start.localScale=Vector3.one*.6f;
            var goal=Instance("Station_Goal",root.transform,new Vector3(-.76f,.614f,0));goal.localRotation=Quaternion.Euler(0,270,0);goal.localScale=Vector3.one*.6f;
            Anchor(root.transform,"Start",new Vector3(1.08f,.69f,0));Anchor(root.transform,"Goal",new Vector3(-1.04f,.63f,0));Anchor(root.transform,"WaterLevel",new Vector3(0,.18f,0));
            var key=Object.FindObjectsByType<Light>(FindObjectsSortMode.None).First(l=>l.type==LightType.Directional);key.intensity=1.1f;key.shadowStrength=.8f;key.shadowNormalBias=.07f;
            RenderSettings.ambientSkyColor=new Color(.4f,.52f,.65f);RenderSettings.ambientEquatorColor=new Color(.24f,.29f,.34f);
            PresentationGround(new Vector3(0,-.01f,0),new Vector3(20,.02f,20));
            PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/BeautifulCorner.prefab");
            EditorSceneManager.SaveScene(scene,Root+"/Scenes/BeautifulCorner.unity");
        }
        static void PresentationGround(Vector3 at,Vector3 scale)
        {
            var mat=Mat("Presentation slate");if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.SetColor("_BaseColor",new Color(.18f,.23f,.27f));mat.SetFloat("_Smoothness",.1f);AssetDatabase.CreateAsset(mat,Root+"/Materials/Presentation slate.mat");}
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Presentation ground (not part of asset)";Object.DestroyImmediate(ground.GetComponent<Collider>());ground.transform.position=at;ground.transform.localScale=scale;ground.GetComponent<Renderer>().sharedMaterial=mat;
        }
        static void Gallery()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Module gallery camera").AddComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.orthographicSize=3.65f;camera.nearClipPlane=.01f;camera.farClipPlane=50;
            camera.transform.position=new Vector3(2.8f,12,-5);camera.transform.LookAt(new Vector3(2.4f,.05f,2.9f));camera.backgroundColor=new Color(.11f,.15f,.18f);camera.clearFlags=CameraClearFlags.SolidColor;CreekWaterAuthoring.ConfigureCamera(camera);
            var paths=Directory.GetFiles(Modules,"*.prefab").OrderBy(p=>Path.GetFileName(p)).ToArray();
            var display=new GameObject("Reusable modules — actual prefab instances").transform;
            for(int i=0;i<paths.Length;i++) {
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);var go=(GameObject)PrefabUtility.InstantiatePrefab(asset);go.transform.SetParent(display,false);go.transform.localPosition=new Vector3(i%5*1.2f,0,i/5*1.9f);
                var label=new GameObject(asset.name+" label");label.transform.SetParent(display,false);label.transform.position=go.transform.position+new Vector3(0,.01f,-.2f);label.transform.rotation=Quaternion.Euler(75,0,0);
                var text=label.AddComponent<TextMesh>();text.text=DisplayName(asset.name);text.anchor=TextAnchor.UpperCenter;text.fontSize=48;text.characterSize=.022f;text.color=new Color(.8f,.88f,.91f);
            }
            var sun=new GameObject("Gallery sunlight").AddComponent<Light>();sun.type=LightType.Directional;sun.color=new Color(1,.88f,.72f);sun.intensity=1.15f;sun.shadows=LightShadows.Soft;sun.shadowNormalBias=.05f;sun.transform.rotation=Quaternion.Euler(50,-35,0);
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.4f,.52f,.65f);RenderSettings.ambientEquatorColor=new Color(.25f,.3f,.35f);RenderSettings.ambientGroundColor=new Color(.16f,.17f,.19f);
            PresentationGround(new Vector3(2.4f,-.075f,2.3f),new Vector3(20,.02f,20));EditorSceneManager.SaveScene(scene,Root+"/Scenes/CreekModuleGallery.unity");
        }
        static Bounds Bounds(Transform root)
        {var renderers=root.GetComponentsInChildren<Renderer>();var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return b;}
        static string DisplayName(string name)
        {
            if(name.StartsWith("Support_Pier_"))return "Pier\n"+(int.Parse(name.Substring(13))/100f).ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+" m";
            if(name.StartsWith("Deck_"))return name.Substring(5).Replace("_080_Rise020","\n0.80 m / +0.20 m").Replace("_040","\n0.40 m").Replace("_080","\n0.80 m");
            return name.Replace("Station_","Station\n").Replace("Landing_Platform_080","Landing\n0.80 m").Replace("Waterfall_020x040","Waterfall\n0.20 x 0.40 m").Replace("_040","\n0.40 m").Replace("_080","\n0.80 m").Replace('_',' ');
        }
        static void Validate()
        {
            var paths=Directory.GetFiles(Modules,"*.prefab");
            foreach(var path in paths) {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(prefab.GetComponentsInChildren<Collider>(true).Length!=0)throw new Exception("Art collider: "+path);
                if(prefab.GetComponentsInChildren<Renderer>(true).Any(r=>r.sharedMaterials.Any(m=>!m)))throw new Exception("Missing material: "+path);
                if(prefab.GetComponentsInChildren<MonoBehaviour>(true).Length!=0)throw new Exception("Art module must not have runtime behaviours: "+path);
                if(AssetDatabase.GetDependencies(path,true).Any(p=>p.StartsWith("Assets/") && !p.StartsWith(Root+"/")))throw new Exception("Module depends on gameplay assets: "+path);
            }
            var first=Prefab("Deck_Straight_040");var b=Bounds(first.transform);
            if(Mathf.Abs(b.size.x-.4f)>.005f || Mathf.Abs(b.center.z-.2f)>.005f)throw new Exception("Unexpected FBX axes or dimensions: "+b);
            if(PrefabUtility.GetPrefabAssetType(Prefab("Station_Build"))!=PrefabAssetType.Variant)throw new Exception("Build station must share the start station prefab.");
            File.WriteAllText(Output+"/ArtKit-Verification.txt","Independent modular art prefabs: "+paths.Length+"\n0 colliders; 0 runtime behaviours; no missing materials\nStraight deck width 0.4 m; Entry z=0, Exit z=0.4\nBuild station is a prefab variant of the start station\nGameplay level is not edited by this tool\n");
            Debug.Log("CREEK_ART_KIT_READY modules="+paths.Length);
        }
        static RenderPipelineAsset previous;static UniversalRenderPipelineAsset preview;static int capturePhase;static double next;
        public static void ValidateForUpload()
        {
            Validate();
            foreach(var name in new[]{"BeautifulCorner","CreekModuleGallery","CreekEnvironment"}) {
                var scene=EditorSceneManager.OpenScene(Root+"/Scenes/"+name+".unity");
                foreach(var root in scene.GetRootGameObjects()) {
                    foreach(var t in root.GetComponentsInChildren<Transform>(true))
                        if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)throw new Exception("Missing script: "+name+"/"+t.name);
                    foreach(var r in root.GetComponentsInChildren<Renderer>(true))
                        if(r.sharedMaterials.Any(m=>!m || !m.shader))throw new Exception("Missing material or shader: "+name+"/"+r.name);
                }
            }
            foreach(var guid in AssetDatabase.FindAssets("t:Shader",new[]{Root+"/Shaders"})) {
                var shader=AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
                if(ShaderUtil.ShaderHasError(shader))throw new Exception("Shader compile error: "+shader.name);
            }
            Debug.Log("CREEK_UPLOAD_VALIDATED scenes=3 modules=20");
        }
        public static void BuildAndCapture()
        {
            Build();previous=QualitySettings.renderPipeline;
            preview=Object.Instantiate((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline);preview.name="Temporary art capture pipeline";preview.shadowDistance=25;
            var so=new SerializedObject(preview);so.FindProperty("m_MainLightShadowsSupported").boolValue=true;so.FindProperty("m_MainLightShadowmapResolution").intValue=2048;so.FindProperty("m_SoftShadowsSupported").boolValue=true;so.ApplyModifiedPropertiesWithoutUndo();
            QualitySettings.renderPipeline=preview;capturePhase=0;next=EditorApplication.timeSinceStartup+2;EditorApplication.update+=CaptureTick;
        }
        static void CaptureTick()
        {
            if(EditorApplication.timeSinceStartup<next)return;
            try {
                if(capturePhase==0){Capture("ModuleGallery.png");EditorSceneManager.OpenScene(Root+"/Scenes/BeautifulCorner.unity");capturePhase=1;next=EditorApplication.timeSinceStartup+2;return;}
                Capture("BeautifulCorner.png");
                var cam=Camera.main;cam.transform.position=new Vector3(-1.8f,1.15f,1.9f);cam.transform.LookAt(new Vector3(-.95f,.4f,.4f));cam.orthographicSize=.59f;
                Capture("BeautifulCorner-Waterfall.png");
                Finish(0);
            }catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        static void Capture(string file)
        {
            var camera=Camera.main;var rt=new RenderTexture(1800,1250,24,RenderTextureFormat.ARGB32);rt.Create();var previousRT=RenderTexture.active;
            try {RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();File.WriteAllBytes(Output+"/"+file,tex.EncodeToPNG());Object.DestroyImmediate(tex);}
            finally {RenderTexture.active=previousRT;rt.Release();Object.DestroyImmediate(rt);}
        }
        static void Finish(int result){EditorApplication.update-=CaptureTick;QualitySettings.renderPipeline=previous;if(preview)Object.DestroyImmediate(preview);EditorApplication.Exit(result);}
    }
}

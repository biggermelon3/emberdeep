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
    // Standalone art only. Mesh batches render; hidden nested prefab instances remain editable.
    public static class CreekBotanicalAuthoring
    {
        const string Root="Assets/CreekDiorama", Kit=Root+"/Prefabs/Botanical", Meshes=Root+"/Meshes/Botanical", Output="Docs/CreekDiorama";
        static System.Random rng;
        static Material Mat(string name)=>AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/"+name+".mat");
        static float R(float a,float b)=>a+(float)rng.NextDouble()*(b-a);
        static Vector3 Old(float x,float y,float z)=>new Vector3(-x*.2f,z*.2f,-y*.2f);
        static string Clean(string name)=>System.Text.RegularExpressions.Regex.Replace(name,@"\.\d{3}$","");
        [MenuItem("MetaHackthone/Creek Art/Build botanical dressing")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play first.");
            for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save open scenes first.");
            rng=new System.Random(329);Directory.CreateDirectory(Kit);Directory.CreateDirectory(Meshes);AssetDatabase.Refresh();
            Import();Dress();Gallery();AssetDatabase.SaveAssets();Verify();
        }
        static void Import()
        {
            foreach(var file in new[]{"Limestone surface","Oak grain"}) {
                string normalPath=Root+"/Textures/"+file+" Normal.png";
                var ti=(TextureImporter)AssetImporter.GetAtPath(normalPath);ti.textureType=TextureImporterType.NormalMap;ti.sRGBTexture=false;ti.wrapMode=TextureWrapMode.Repeat;ti.SaveAndReimport();
                var color=(TextureImporter)AssetImporter.GetAtPath(Root+"/Textures/"+file+".png");color.wrapMode=TextureWrapMode.Repeat;color.SaveAndReimport();
            }
            var palette=JObject.Parse(File.ReadAllText("ArtSource/CreekDiorama/botanical_palette.json"));
            foreach(var e in palette["materials"]) {
                string name=(string)e["name"];var mat=Mat(name);
                if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,Root+"/Materials/"+name+".mat");}
                bool foliage=name.StartsWith("Botanical leaf") || name=="Botanical ivory" || name=="Botanical lavender";
                mat.shader=Shader.Find(foliage?"Creek Diorama/Soft Foliage":"Universal Render Pipeline/Lit");
                var c=(JArray)e["color"];mat.SetColor("_BaseColor",new Color((float)c[0],(float)c[1],(float)c[2],1).gamma);
                mat.SetFloat("_Smoothness",1-(float)e["roughness"]);mat.SetFloat("_Metallic",(float)e["metallic"]);mat.SetFloat("_Cull",0);mat.enableInstancing=true;mat.doubleSidedGI=true;
                string texture=name=="Botanical limestone"?"Limestone surface":name=="Botanical oak" || name=="Botanical bark"?"Oak grain":null;
                if(texture!=null) {
                    mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+texture+".png"));
                    mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+texture+" Normal.png"));mat.SetFloat("_BumpScale",.16f);mat.EnableKeyword("_NORMALMAP");
                    mat.SetTextureScale("_BaseMap",Vector2.one*2);mat.SetTextureScale("_BumpMap",Vector2.one*2);
                }
                if(name=="Botanical lantern glow"){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",new Color(1,.32f,.025f)*2.5f);}
                EditorUtility.SetDirty(mat);
            }
            string path=Root+"/Models/CreekBotanicalKit.fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.importCameras=false;importer.importLights=false;importer.importAnimation=false;importer.isReadable=true;importer.SaveAndReimport();
            foreach(var mat in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())if(Mat(mat.name))importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),mat.name),Mat(mat.name));
            importer.SaveAndReimport();var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            var first=model.GetComponentsInChildren<Transform>().First(t=>Clean(t.name)=="Rock_Stratum_A");
            // Blender FBX exporter can encode metres in either scale metadata or transforms.
            var bounds=Bounds(first);if(bounds.size.x>10)model.transform.localScale*=.01f;
            foreach(var nameToken in palette["modules"]) {
                string name=(string)nameToken;var source=model.GetComponentsInChildren<Transform>().First(t=>Clean(t.name)==name);
                var output=new GameObject(name);Batch(source,output.transform,name);
                var anchor=new GameObject("Anchor_Ground").transform;anchor.SetParent(output.transform,false);
                PrefabUtility.SaveAsPrefabAsset(output,Kit+"/"+name+".prefab");Object.DestroyImmediate(output);
            }
            Object.DestroyImmediate(model);
        }
        static Bounds Bounds(Transform root)
        {var rr=root.GetComponentsInChildren<Renderer>();var b=rr[0].bounds;foreach(var r in rr)b.Encapsulate(r.bounds);return b;}
        static Transform Put(string name,Transform parent,Vector3 position,float scale=1,float yaw=0)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Kit+"/"+name+".prefab"));
            go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=Vector3.one*scale;go.transform.localRotation=Quaternion.Euler(0,yaw,0);return go.transform;
        }
        static Transform Group(Transform parent,string name){var t=new GameObject(name).transform;t.SetParent(parent,false);return t;}
        static void Batch(Transform source,Transform output,string name)
        {
            var batches=new Dictionary<Material,List<CombineInstance>>();
            foreach(var filter in source.GetComponentsInChildren<MeshFilter>()) {
                var renderer=filter.GetComponent<MeshRenderer>();if(!renderer)continue;
                for(int i=0;i<filter.sharedMesh.subMeshCount;i++) {
                    var m=renderer.sharedMaterials[Mathf.Min(i,renderer.sharedMaterials.Length-1)];if(Mat(m.name))m=Mat(m.name);
                    if(!batches.TryGetValue(m,out var list))batches[m]=list=new List<CombineInstance>();
                    list.Add(new CombineInstance{mesh=filter.sharedMesh,subMeshIndex=i,transform=output.worldToLocalMatrix*filter.transform.localToWorldMatrix});
                }
            }
            int part=0;
            foreach(var pair in batches) {
                var mesh=new Mesh{name=name+" "+pair.Key.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);mesh.RecalculateBounds();
                string path=Meshes+"/"+name+"_"+(part++)+".asset";mesh=SaveMesh(mesh,path);
                var go=new GameObject(pair.Key.name);go.transform.SetParent(output,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=pair.Key;
            }
        }
        static Mesh SaveMesh(Mesh mesh,string path)
        {var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;}AssetDatabase.CreateAsset(mesh,path);return mesh;}
        static Vector2[] Left={new(-6.8f,-2.5f),new(-5.65f,-2.95f),new(-4.65f,-2.65f),new(-3.9f,-1.7f),new(-3.85f,.8f),new(-4.6f,1.55f),new(-4.9f,2.85f),new(-6.6f,3.1f),new(-6.95f,1.8f)};
        static Vector2[] Right={new(3.95f,-1.3f),new(4.2f,-2.45f),new(5.65f,-2.8f),new(6.8f,-2.4f),new(6.95f,2.5f),new(6.1f,3.05f),new(4.9f,2.8f),new(4.25f,1.5f)};
        static void Dress()
        {
            var scene=EditorSceneManager.OpenScene(Root+"/Scenes/BeautifulCorner.unity");var root=scene.GetRootGameObjects().First(g=>g.name.StartsWith("Beautiful Corner"));
            var previous=root.transform.Find("10 Botanical dressing");if(previous)Object.DestroyImmediate(previous.gameObject);
            foreach(Transform child in root.transform)if(new[]{"02","03","05","06"}.Any(prefix=>child.name.StartsWith(prefix)))child.gameObject.SetActive(false);
            var dressing=Group(root.transform,"10 Botanical dressing");var editable=Group(dressing,"Editable placement — enable this and hide render batches to rearrange");
            Transform terrain=Group(editable,"Stratified rock and soil"),plants=Group(editable,"Moss ivy ferns and meadow"),trees=Group(editable,"Layered canopy"),props=Group(editable,"Story props");
            Island(Left,3.30f,terrain,plants,"West");Island(Right,2.98f,terrain,plants,"East");
            float[,] ledges={{-5.1f,-2.55f,1.55f,.9f,.55f},{-6.45f,-2.60f,2.03f,.65f,.55f},{-4.05f,1.7f,1.83f,.8f,.9f},{4.5f,-2.55f,1.45f,.8f,.63f},{6.1f,-2.8f,1.83f,.78f,.43f},{3.95f,2.2f,1.75f,.75f,.9f},{-2.8f,2.85f,1.1f,.85f,.38f},{1.9f,3,1.12f,1.3f,.35f}};
            for(int j=0;j<ledges.GetLength(0);j++) {
                var poly=Enumerable.Range(0,7).Select(i=>new Vector2(ledges[j,0]+Mathf.Cos(i*Mathf.PI*2/7)*ledges[j,3],ledges[j,1]+Mathf.Sin(i*Mathf.PI*2/7)*ledges[j,4])).ToArray();
                Island(poly,ledges[j,2],terrain,plants,"Ledge"+j);
            }
            float[,] pines={{-6.1f,2.2f,3.35f,.92f},{-5.2f,2.5f,3.35f,.65f},{-6.4f,.9f,3.35f,.7f},{-6.4f,-1.7f,3.35f,.65f},{-4.5f,-1.9f,3.35f,.5f},{-4.05f,1.85f,1.88f,.55f},{-6.45f,-2.55f,2.08f,.45f},{5.8f,2.25f,3.03f,.84f},{6.5f,.8f,3.03f,.65f},{4.8f,1.85f,3.03f,.67f},{6.3f,-1.8f,3.03f,.51f},{4.2f,-1.9f,3.03f,.46f},{3.8f,2.15f,1.8f,.58f},{-2.8f,2.85f,1.15f,.43f},{1.9f,2.9f,1.17f,.46f}};
            for(int i=0;i<pines.GetLength(0);i++)Put("Pine_Branching",trees,Old(pines[i,0],pines[i,1],pines[i,2]),pines[i,3]*.68f,R(0,360));
            Put("Tree_Round_Canopy",trees,Old(-5.15f,-1.8f,3.35f),.63f,40);Put("Tree_Round_Canopy",trees,Old(5.3f,1.7f,3.03f),.72f,100);
            float[,] stones={{-2.2f,-1.9f,.24f,1.25f},{.7f,-2.1f,.20f,1.05f},{-.7f,1.8f,.18f,.7f},{2.4f,.4f,.19f,.7f},{-3.4f,-2.9f,.18f,.65f},{3.1f,-2.7f,.18f,.6f},{5.9f,-3.1f,.18f,.55f}};
            for(int i=0;i<stones.GetLength(0);i++) {
                var p=Old(stones[i,0],stones[i,1],0);p.y=stones[i,2]-.065f;float s=stones[i,3];
                Put("Rock_River_Boulder",terrain,p,s,R(0,360));Put("Rock_Pebble_Cluster",terrain,p+new Vector3(.06f,.02f,.05f),s,R(0,360));
                if(i%2==0)Put("Moss_Overhang",plants,p+new Vector3(0,.145f*s,0),s*.5f,R(0,360));
            }
            Put("Prop_Lantern",props,Old(-5.8f,-1.45f,3.35f),.65f,0);Put("Prop_Lantern",props,Old(6.0f,-1.45f,3.03f),.65f,180);
            Put("Prop_Barrel",props,Old(5.8f,-2.25f,3.03f),.8f,20);Put("Prop_Crate",props,Old(5.15f,-2.25f,3.03f),.7f,15);
            foreach(var p in new[]{new Vector3(-.55f,.181f,.48f),new Vector3(.45f,.181f,-.33f),new Vector3(-.85f,.181f,.57f)})Put("Water_Lily_Pads",plants,p,.8f,R(0,360));
            var render=Group(dressing,"Render batches");foreach(Transform group in editable)Batch(group,Group(render,group.name),"Dressing_"+group.GetSiblingIndex());
            editable.gameObject.SetActive(false);
            var light=Object.FindObjectsByType<Light>(FindObjectsSortMode.None).First(l=>l.type==LightType.Directional);light.color=new Color(1,.91f,.76f);light.intensity=1.2f;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.36f,.46f,.57f);RenderSettings.ambientEquatorColor=new Color(.21f,.27f,.28f);RenderSettings.ambientGroundColor=new Color(.12f,.13f,.10f);
            var cam=Camera.main;cam.transform.position=new Vector3(-2.4f,2.25f,3.8f);cam.transform.LookAt(new Vector3(0,.42f,0));cam.orthographicSize=1.16f;
            PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/BeautifulCorner.prefab");EditorSceneManager.SaveScene(scene);
        }
        public static void RefreshIfAvailable()
        {
            if(!AssetDatabase.LoadAssetAtPath<GameObject>(Kit+"/Rock_Stratum_A.prefab"))return;
            rng=new System.Random(329);Dress();
        }
        static void Island(Vector2[] oldPoly,float top,Transform terrain,Transform plants,string id)
        {
            var poly=oldPoly.Select(p=>Old(p.x,p.y,top)).ToArray();var center=poly.Aggregate(Vector3.zero,(a,b)=>a+b)/poly.Length;
            Solid(poly,.17f,top*.2f-.012f,terrain,"Earth_"+id,Mat("Botanical limestone"));
            Solid(poly,top*.2f-.014f,top*.2f+.006f,terrain,"Sod_"+id,Mat("Botanical moss"));
            for(int edge=0;edge<poly.Length;edge++) {
                var a=poly[edge];var b=poly[(edge+1)%poly.Length];float length=Vector3.Distance(a,b);var along=(b-a).normalized;
                var outward=Vector3.Cross(Vector3.up,along);if(Vector3.Dot(outward,(a+b)*.5f-center)<0)outward=-outward;
                float yaw=Mathf.Atan2(outward.x,outward.z)*Mathf.Rad2Deg;
                int pieces=Mathf.Max(1,Mathf.CeilToInt(length/.24f));int layers=Mathf.Max(1,Mathf.CeilToInt((top*.2f-.17f)/.24f));float layerHeight=(top*.2f-.17f)/layers;
                for(int j=0;j<pieces;j++) {
                    var p=Vector3.Lerp(a,b,(j+.5f)/pieces);
                    bool waterfall=p.x<-.94f && p.x>-1.17f && p.z>.44f;
                    bool roadCut=Mathf.Abs(p.z)<.245f && Mathf.Abs(p.x)>.70f && Mathf.Abs(p.x)<1.3f;
                    if(!waterfall && !roadCut)for(int k=0;k<layers;k++) {
                        var at=p-outward*R(.008f,.03f)+along*R(-.025f,.025f);at.y=.17f+k*layerHeight+R(-.025f,.005f);
                        var rock=Put(k%2==0?"Rock_Stratum_A":"Rock_Stratum_B",terrain,at,1,yaw+R(-9,9));
                        rock.localScale=new Vector3(length/pieces/(k%2==0?.30f:.25f)*R(1.02f,1.17f),layerHeight/(k%2==0?.34f:.24f)*R(1.0f,1.13f),R(.7f,1));
                    }
                    var rim=p-outward*.022f;rim.y=top*.2f+.008f;
                    if(!roadCut)Put("Moss_Overhang",plants,rim+outward*.055f,.85f,yaw);
                    if(!waterfall && !roadCut && j%2==0 && top>1.6f)Put("Ivy_Cascade",plants,rim+outward*.105f,Mathf.Min(R(.5f,.88f),(top*.2f-.19f)/.42f),yaw);
                    if(Mathf.Abs(rim.z)>.25f || Mathf.Abs(rim.x)<.70f) {
                        Put("Grass_Soft_Tuft",plants,rim-outward*.035f,R(.7f,1.1f),R(0,360));
                        if(j%2==0)Put("Fern_Rosette",plants,rim-outward*.06f,R(.5f,.9f),R(0,360));
                        if((j+edge)%3==0)Put("Flowers_Daisies",plants,rim-outward*.035f,.7f,R(0,360));
                        if((j+edge)%5==0)Put("Flowers_Lupin",plants,rim-outward*.07f,.75f,R(0,360));
                        if((j+edge)%3==1)Put("Shrub_Broadleaf",plants,rim-outward*.07f,R(.45f,.75f),R(0,360));
                    }
                }
            }
            // Small pockets inside the grass terrace, kept clear of the platform/bridge corridor.
            for(int i=0;i<poly.Length*3;i++) {
                var rim=Vector3.Lerp(poly[i%poly.Length],poly[(i+1)%poly.Length],R(.1f,.9f));var p=Vector3.Lerp(center,rim,R(.65f,.92f));p.y=top*.2f+.008f;
                if(Mathf.Abs(p.z)<.25f && Mathf.Abs(p.x)>.70f)continue;
                Put(i%7==0?"Flowers_Daisies":"Grass_Soft_Tuft",plants,p,R(.6f,1),R(0,360));
            }
        }
        static void Solid(Vector3[] poly,float bottom,float top,Transform parent,string name,Material mat)
        {
            int n=poly.Length;var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();
            void Tri(Vector3 a,Vector3 b,Vector3 c,Vector3 normal) {
                if(Vector3.Dot(Vector3.Cross(b-a,c-a),normal)<0){var t=b;b=c;c=t;}
                int k=vertices.Count;vertices.AddRange(new[]{a,b,c});triangles.AddRange(new[]{k,k+1,k+2});uv.AddRange(new[]{new Vector2(a.x,a.z)*3,new Vector2(b.x,b.z)*3,new Vector2(c.x,c.z)*3});
            }
            var center=poly.Aggregate(Vector3.zero,(a,b)=>a+b)/n;center.y=top;
            for(int i=0;i<n;i++) {
                var a=poly[i];a.y=top;var b=poly[(i+1)%n];b.y=top;Tri(center,a,b,Vector3.up);
                var lowA=a;lowA.y=bottom;var lowB=b;lowB.y=bottom;var normal=(a+b)*.5f-center;normal.y=0;Tri(a,lowA,lowB,normal);Tri(a,lowB,b,normal);
            }
            var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();mesh=SaveMesh(mesh,Meshes+"/"+name+".asset");
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;
        }
        static void Gallery()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var root=new GameObject("Botanical modular prop library").transform;
            var names=Directory.GetFiles(Kit,"*.prefab").Select(Path.GetFileNameWithoutExtension).OrderBy(n=>n).ToArray();
            for(int i=0;i<names.Length;i++) {
                var at=new Vector3(i%6*.72f,0,i/6*1.3f);Put(names[i],root,at);
                var label=new GameObject(names[i]+" label");label.transform.SetParent(root,false);label.transform.position=at+new Vector3(0,.005f,-.27f);label.transform.rotation=Quaternion.Euler(75,0,0);
                var text=label.AddComponent<TextMesh>();text.text=names[i].Replace('_',' ');text.anchor=TextAnchor.UpperCenter;text.fontSize=48;text.characterSize=.011f;text.color=new Color(.87f,.91f,.85f);
            }
            var camera=new GameObject("Botanical gallery camera").AddComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.orthographicSize=2.1f;camera.transform.position=new Vector3(1.8f,6,-4);camera.transform.LookAt(new Vector3(1.8f,.08f,1.25f));camera.nearClipPlane=.01f;camera.farClipPlane=30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.1f,.095f);CreekWaterAuthoring.ConfigureCamera(camera);
            var sun=new GameObject("Gallery sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.2f;sun.color=new Color(1,.91f,.76f);sun.shadows=LightShadows.Soft;sun.shadowNormalBias=.02f;sun.transform.rotation=Quaternion.Euler(48,-35,0);
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.4f,.5f,.6f);RenderSettings.ambientEquatorColor=new Color(.24f,.29f,.28f);RenderSettings.ambientGroundColor=new Color(.12f,.14f,.10f);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);Object.DestroyImmediate(floor.GetComponent<Collider>());floor.transform.position=new Vector3(1.8f,-.03f,1.2f);floor.transform.localScale=new Vector3(20,.03f,20);floor.GetComponent<Renderer>().sharedMaterial=Mat("Presentation slate");
            EditorSceneManager.SaveScene(scene,Root+"/Scenes/CreekBotanicalGallery.unity");
        }
        static void Verify()
        {
            if(ShaderUtil.ShaderHasError(Shader.Find("Creek Diorama/Soft Foliage")))throw new Exception("Foliage shader failed to compile");
            int tris=0;foreach(var path in Directory.GetFiles(Kit,"*.prefab")) {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(prefab.GetComponentsInChildren<Collider>(true).Length>0 || prefab.GetComponentsInChildren<MonoBehaviour>(true).Length>0)throw new Exception("Art must be passive: "+path);
                foreach(var r in prefab.GetComponentsInChildren<Renderer>(true))if(r.sharedMaterials.Any(m=>!m || !m.shader))throw new Exception("Missing material: "+path);
                foreach(var m in prefab.GetComponentsInChildren<MeshFilter>())tris+=m.sharedMesh.triangles.Length/3;
            }
            var full=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/BeautifulCorner.prefab");
            int activeTris=full.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3);
            File.WriteAllText(Output+"/Botanical-Verification.txt",$"18 modular botanical props; no colliders or runtime scripts\nLibrary triangles: {tris}\nActive beautiful corner triangles: {activeTris}\nActive renderers: {full.GetComponentsInChildren<Renderer>().Length}\nEditable nested prefab placements retained inactive beside render batches\n");
            Debug.Log("BOTANICAL_ART_READY library=18 activeTriangles="+activeTris);
        }
        static RenderPipelineAsset oldPipeline;static UniversalRenderPipelineAsset preview;static double next;static int phase;
        public static void BuildAndCapture()
        {
            Build();oldPipeline=QualitySettings.renderPipeline;preview=Object.Instantiate((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline);preview.hideFlags=HideFlags.HideAndDontSave;preview.shadowDistance=12;
            var so=new SerializedObject(preview);so.FindProperty("m_MainLightShadowsSupported").boolValue=true;so.FindProperty("m_MainLightShadowmapResolution").intValue=4096;so.FindProperty("m_SoftShadowsSupported").boolValue=true;so.FindProperty("m_ShadowCascadeCount").intValue=1;so.FindProperty("m_ShadowDepthBias").floatValue=.02f;so.FindProperty("m_ShadowNormalBias").floatValue=.08f;so.ApplyModifiedPropertiesWithoutUndo();
            QualitySettings.renderPipeline=preview;phase=0;next=EditorApplication.timeSinceStartup+2;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            if(EditorApplication.timeSinceStartup<next)return;
            try {
                if(phase==0){Capture("BotanicalGallery.png");EditorSceneManager.OpenScene(Root+"/Scenes/BeautifulCorner.unity");QualitySettings.renderPipeline=preview;phase=1;next=EditorApplication.timeSinceStartup+2;return;}
                if(phase==1){Capture("BeautifulCorner-Botanical.png");var cam=Camera.main;cam.transform.position=new Vector3(-1.9f,1.15f,2.0f);cam.transform.LookAt(new Vector3(-1.03f,.4f,.4f));cam.orthographicSize=.52f;phase=2;next=EditorApplication.timeSinceStartup+1;return;}
                Capture("Botanical-WaterfallDetail.png");EditorApplication.update-=Tick;QualitySettings.renderPipeline=oldPipeline;Object.DestroyImmediate(preview);EditorApplication.Exit(0);
            }catch(Exception e){Debug.LogException(e);EditorApplication.update-=Tick;QualitySettings.renderPipeline=oldPipeline;if(preview)Object.DestroyImmediate(preview);EditorApplication.Exit(1);}
        }
        static void Capture(string name)
        {
            QualitySettings.renderPipeline=preview;
            var cam=Camera.main;var rt=new RenderTexture(2000,1400,24,RenderTextureFormat.ARGB32);rt.Create();var old=RenderTexture.active;
            Debug.Log("BOTANICAL_CAPTURE "+name+" pipeline="+GraphicsSettings.currentRenderPipeline.name+" shadowDistance="+((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).shadowDistance);
            try {RenderPipeline.SubmitRenderRequest(cam,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();File.WriteAllBytes(Output+"/"+name,tex.EncodeToPNG());Object.DestroyImmediate(tex);}
            finally{RenderTexture.active=old;rt.Release();Object.DestroyImmediate(rt);}
        }
    }
}

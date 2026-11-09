using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace CreekDiorama.Editor
{
    // All generated assets stay inside the art package. No project-wide render settings are changed.
    public static class CreekWaterAuthoring
    {
        const string Root="Assets/CreekDiorama";

        [MenuItem("MetaHackthone/Creek Environment/Upgrade water materials")]
        public static void Upgrade()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
                if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes first.");
            var scene=EditorSceneManager.OpenScene(CreekEnvironmentImporter.ScenePath);
            var root=scene.GetRootGameObjects().First(g=>g.name=="Creek Environment");
            ApplyToRoot(root);
            foreach(Transform group in root.transform)
                PrefabUtility.SaveAsPrefabAsset(group.gameObject,Root+"/Prefabs/"+group.name.Replace(' ','_')+".prefab");
            PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/CreekEnvironment.prefab");
            ConfigureCamera(Camera.main);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("CREEK_WATER_READY: water, two flowing curtains and two impact sheets; art scene and prefabs saved.");
        }

        public static void ConfigureCamera(Camera camera)
        {
            var data=camera.GetUniversalAdditionalCameraData();
            data.requiresDepthTexture=true;
            data.requiresColorTexture=true;
        }

        public static void ApplyToRoot(GameObject root)
        {
            var river=Material("Living river","Creek Diorama/Living Water");
            var fall=Material("Silk waterfall","Creek Diorama/Silk Waterfall");
            var foam=Material("Waterfall impact","Creek Diorama/Waterfall Impact");
            var spray=Material("Waterfall spray","Creek Diorama/Waterfall Spray");
            var source=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Models/CreekDiorama.fbx"));
            source.hideFlags=HideFlags.HideAndDontSave;
            try
            {
                var renderers=source.GetComponentsInChildren<Renderer>();
                var bounds=renderers[0].bounds;
                foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                source.transform.localScale*=2.86f/bounds.size.x;
                var filters=source.GetComponentsInChildren<MeshFilter>();
                var riverGroup=root.GetComponentsInChildren<Transform>(true).First(t=>t.name.StartsWith("07 River")||t.name.StartsWith("07_River"));
                var fallGroup=root.GetComponentsInChildren<Transform>(true).First(t=>t.name.StartsWith("08 Waterfalls")||t.name.StartsWith("08_Waterfalls"));
                ClearGenerated(riverGroup);ClearGenerated(fallGroup);
                var surface=filters.First(f=>f.name.StartsWith("Faceted turquoise river"));
                var riverMesh=CopyMesh(surface,"Living river surface",false);
                AddMesh(riverGroup,"Living river surface",riverMesh,river);
                var waterBounds=riverMesh.bounds;
                foam.SetVector("_WaterBounds",new Vector4(waterBounds.min.x,waterBounds.min.z,waterBounds.max.x,waterBounds.max.z));
                int index=0;
                foreach(var f in filters.Where(f=>f.name.StartsWith("Waterfall curtain")).OrderBy(f=>f.name))
                {
                    index++;
                    var mesh=CopyMesh(f,"Flowing curtain "+index,true);
                    AddMesh(fallGroup,"Flowing curtain "+index,mesh,fall);
                    var b=mesh.bounds;
                    var center=new Vector3(b.center.x,waterBounds.max.y+.004f,Mathf.Min(b.max.z-.018f,waterBounds.max.z-.03f));
                    var radius=b.size.x*1.05f;
                    AddMesh(fallGroup,"Impact ripples "+index,ImpactMesh(center,radius,"Impact ripples "+index),foam);
                    AddMesh(fallGroup,"Airborne droplets "+index,SprayMesh(center,b.size.x,"Airborne droplets "+index),spray);
                    Debug.Log("CREEK_CURTAIN "+index+" center="+b.center+" bounds="+b.size);
                }
            }
            finally { Object.DestroyImmediate(source); }
        }
        static Material Material(string name,string shaderName)
        {
            var shader=Shader.Find(shaderName);
            if(!shader)throw new InvalidOperationException("Shader missing: "+shaderName);
            var path=Root+"/Materials/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat) {mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}
            mat.shader=shader;mat.enableInstancing=true;EditorUtility.SetDirty(mat);return mat;
        }
        static void ClearGenerated(Transform group)
        {
            // These two groups are procedural water only; preserve the source FBX and other environment groups.
            foreach(Transform child in group.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);
        }
        static Mesh CopyMesh(MeshFilter source,string name,bool flowUV)
        {
            var original=source.sharedMesh;
            var verts=original.vertices.Select(v=>source.transform.TransformPoint(v)).ToArray();
            var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32,vertices=verts,triangles=original.triangles};
            mesh.RecalculateBounds();mesh.RecalculateNormals();
            if(flowUV)
            {
                var b=mesh.bounds;
                // UV.y follows the lip, vertical drop and foot, so the same current wraps around each bend.
                var length=b.size.y+b.size.z*.4f;
                mesh.uv=verts.Select(v=>new Vector2((v.x-b.min.x)/b.size.x,((b.max.y-v.y)+(v.z-b.min.z)*.4f)/length)).ToArray();
            }
            return SaveMesh(mesh,name);
        }
        static Mesh ImpactMesh(Vector3 c,float radius,string name)
        {
            var m=new Mesh{name=name};
            m.vertices=new[]{c+new Vector3(-radius,0,-radius*.55f),c+new Vector3(radius,0,-radius*.55f),c+new Vector3(radius,0,radius*.55f),c+new Vector3(-radius,0,radius*.55f)};
            m.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};m.triangles=new[]{0,2,1,0,3,2};
            m.RecalculateNormals();m.RecalculateBounds();return SaveMesh(m,name);
        }
        static Mesh SprayMesh(Vector3 center,float width,string name)
        {
            var vertices=new System.Collections.Generic.List<Vector3>();
            var uv=new System.Collections.Generic.List<Vector2>();
            var particles=new System.Collections.Generic.List<Vector4>();
            var indices=new System.Collections.Generic.List<int>();
            var random=new System.Random(72);
            var corners=new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1)};
            for(int i=0;i<18;i++)
            {
                var c=center+new Vector3(((float)random.NextDouble()-.5f)*width*.7f,0,0);
                var data=new Vector4((float)random.NextDouble(),(float)random.NextDouble(),.0018f+(float)random.NextDouble()*.0025f,.025f+(float)random.NextDouble()*.04f);
                foreach(var corner in corners){vertices.Add(c);uv.Add(corner);particles.Add(data);}
                int k=i*4;indices.AddRange(new[]{k,k+2,k+1,k,k+3,k+2});
            }
            var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetUVs(1,particles);mesh.SetTriangles(indices,0);
            mesh.bounds=new Bounds(center+Vector3.up*.04f,new Vector3(width+.1f,.12f,.1f));
            return SaveMesh(mesh,name);
        }
        static Mesh SaveMesh(Mesh mesh,string name)
        {
            var path=Root+"/Meshes/"+name.Replace(' ','_')+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!existing) {AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);return existing;
        }
        static void AddMesh(Transform parent,string name,Mesh mesh,Material mat)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=mat;
            r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
        }

        // Batch entry point validates actual Play-mode time and renders frames through URP.
        public static void BatchUpgradeAndVerify()
        {
            Upgrade();
            SessionState.SetBool("CreekWaterVerification",true);
            ResumeVerification();
            EditorApplication.isPlaying=true;
        }

        [InitializeOnLoadMethod]
        static void ResumeVerification()
        {
            if(!SessionState.GetBool("CreekWaterVerification",false))return;
            EditorApplication.playModeStateChanged-=OnPlayMode;
            EditorApplication.playModeStateChanged+=OnPlayMode;
            if(EditorApplication.isPlaying)BeginCapture();
        }
        static void OnPlayMode(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.EnteredPlayMode)BeginCapture();
            if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool("CreekWaterVerification",false))
            {
                SessionState.SetBool("CreekWaterVerification",false);
                EditorApplication.Exit(SessionState.GetInt("CreekWaterExit",1));
            }
        }
        static int phase,firstFrame;static float firstTime;static double deadline;
        static void BeginCapture()
        {
            Application.runInBackground=true;
            phase=0;deadline=EditorApplication.timeSinceStartup+90;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Play-mode water verification timed out.");
                if(!EditorApplication.isPlaying || Time.frameCount<12 || Time.time<.8f)return;
                var camera=Camera.main;
                if(phase==0)
                {
                    Capture(camera,"CreekDiorama-Water-A.png");firstFrame=Time.frameCount;firstTime=Time.time;phase=1;return;
                }
                if(phase==1 && Time.time>firstTime+1.3f && Time.frameCount>firstFrame+5)
                {
                    Capture(camera,"CreekDiorama-Water.png");
                    var waterfall=GameObject.Find("Flowing curtain 1").GetComponent<Renderer>().bounds;
                    var target=waterfall.center+new Vector3(0,-.03f,0);
                    camera.transform.position=target+new Vector3(-.36f,.23f,.64f);camera.transform.LookAt(target);
                    camera.orthographicSize=.36f;phase=2;return;
                }
                if(phase==2)
                {
                    Capture(camera,"CreekDiorama-Waterfall.png");
                    camera.orthographic=false;camera.fieldOfView=50;phase=3;return;
                }
                if(phase==3)
                {
                    Capture(camera,"CreekDiorama-Waterfall-Perspective.png");
                    var shaderNames=new[]{"Creek Diorama/Living Water","Creek Diorama/Silk Waterfall","Creek Diorama/Waterfall Impact","Creek Diorama/Waterfall Spray"};
                    var errors=shaderNames.SelectMany(n=>ShaderUtil.GetShaderMessages(Shader.Find(n))).Where(m=>m.severity.ToString()=="Error").ToArray();
                    if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(e=>e.message)));
                    var report="Shader errors: 0\nPlay frames: "+firstFrame+" -> "+Time.frameCount+"\nPlay time: "+firstTime+" -> "+Time.time+"\nActive art renderers: "+GameObject.Find("Creek Environment").GetComponentsInChildren<Renderer>().Length+"\n";
                    File.WriteAllText("Docs/CreekDiorama/Water-Verification.txt",report);
                    Debug.Log("CREEK_WATER_VERIFIED "+report);
                    Finish(0);
                }
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        static void Finish(int code)
        {
            EditorApplication.update-=Tick;SessionState.SetInt("CreekWaterExit",code);EditorApplication.isPlaying=false;
        }
        static void Capture(Camera camera,string filename)
        {
            var rt=new RenderTexture(1600,1100,24,RenderTextureFormat.ARGB32);rt.Create();
            var previous=RenderTexture.active;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
                File.WriteAllBytes("Docs/CreekDiorama/"+filename,image.EncodeToPNG());Object.DestroyImmediate(image);
            }
            finally {RenderTexture.active=previous;rt.Release();Object.DestroyImmediate(rt);}
        }
    }
}

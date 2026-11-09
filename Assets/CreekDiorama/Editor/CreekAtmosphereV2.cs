using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;
namespace CreekDiorama.Editor {
public static class CreekAtmosphereV2 {
const string R="Assets/CreekDiorama";
public static void Apply() {
var s=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(R+"/Scenes/BeautifulCorner.unity");
var root=s.GetRootGameObjects().First(g=>g.name.StartsWith("Beautiful Corner"));
var effects=root.transform.Find("Atmosphere and assembly area");
var box=effects.GetChild(0);box.name="AssemblyBounds";
if(PrefabUtility.IsPartOfPrefabInstance(box.gameObject))PrefabUtility.UnpackPrefabInstance(box.gameObject,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
foreach(var duplicate in box.Cast<Transform>().Where(t=>t.name=="Neon rounded edge core").Skip(1).ToArray())Object.DestroyImmediate(duplicate.gameObject);
var cam=s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).First();
cam.orthographic=false;cam.fieldOfView=37;cam.transform.position=new Vector3(0,2.7f,5.6f);cam.transform.LookAt(new Vector3(0,.55f,.65f));
cam.backgroundColor=new Color(.22f,.42f,.67f);cam.allowHDR=true;
cam.GetUniversalAdditionalCameraData().renderPostProcessing=true;
var rpPath=R+"/Materials/BeautifulCornerPreviewPipeline.asset";
var rp=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(rpPath);
if(!rp){rp=Object.Instantiate((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline);AssetDatabase.CreateAsset(rp,rpPath);}rp.supportsHDR=true;EditorUtility.SetDirty(rp);QualitySettings.renderPipeline=rp;
box.localScale=new Vector3(2.85f,.62f,1.3f);box.localPosition=new Vector3(0,.26f,1.65f);
var mat=box.GetComponent<Renderer>().sharedMaterial;
mat.SetColor("_EdgeColor",new Color(.03f,2.5f,3.5f));mat.SetColor("_CornerColor",new Color(3.3f,5,5.5f));
mat.SetFloat("_EdgeWidth",.0015f);mat.SetFloat("_GlowWidth",.024f);mat.SetFloat("_CornerLength",.07f);
mat.SetFloat("_FaceOpacity",.055f);mat.SetFloat("_FloorOpacity",.11f);mat.SetFloat("_GridOpacity",.14f);mat.SetFloat("_BevelRadius",.025f);
box.GetComponent<MeshFilter>().sharedMesh=RoundedGrid();EditorUtility.SetDirty(mat);
Frame(box);
var texturePath=R+"/Textures/Clouds/RealisticCumulus.png";
var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);importer.alphaIsTransparency=true;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
var bank=effects.Find("Distant Clouds - six billboard cards");if(!bank)bank=effects.Find("Layered realistic cloud banks");bank.name="Layered realistic cloud banks";
foreach(Transform child in bank.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);
var cloudPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(R+"/Prefabs/CloudBillboard.prefab");
var positions=new Vector2[]{new Vector2(.05f,.7f),new Vector2(.36f,.87f),new Vector2(.82f,.78f),new Vector2(1.12f,.46f),new Vector2(-.05f,.39f),new Vector2(.12f,.16f),new Vector2(.87f,.12f),new Vector2(-.04f,-.03f),new Vector2(1.08f,.0f),new Vector2(.55f,.99f)};
float[] depth={9,10,9.4f,8,7,6.7f,6.2f,4.8f,4.8f,11};
float[] width={5,5.3f,5.5f,4.2f,3.6f,3.1f,3.4f,2.7f,2.8f,6};
positions[7]=new Vector2(-.16f,-.16f);positions[8]=new Vector2(1.16f,-.16f);
positions[5]=new Vector2(-.08f,.02f);positions[6]=new Vector2(1.08f,.02f);
cam.aspect=1600f/1100;
for(int j=0;j<positions.Length;j++) {
var go=(GameObject)PrefabUtility.InstantiatePrefab(cloudPrefab,s);go.name="Cloud layer "+j.ToString("00");go.transform.SetParent(bank,false);
go.transform.position=cam.ViewportToWorldPoint(new Vector3(positions[j].x,positions[j].y,depth[j]));go.transform.localScale=new Vector3(width[j],width[j]*.5f,1);
var cm=new Material(Shader.Find("Creek Diorama/Cloud Billboard"));cm.name="Cloud atmosphere "+j;cm.SetTexture("_MainTex",tex);cm.SetFloat("_Mirror",j%2);cm.SetFloat("_Opacity",j<3?.6f:.85f);
cm.SetColor("_Tint",j<3?new Color(.68f,.79f,.99f):j<7?new Color(.85f,.88f,1):new Color(.92f,.92f,1));
string path=R+"/Materials/Cloud layer "+j.ToString("00")+".mat";var old=AssetDatabase.LoadAssetAtPath<Material>(path);if(old){EditorUtility.CopySerialized(cm,old);Object.DestroyImmediate(cm);cm=old;}else AssetDatabase.CreateAsset(cm,path);
go.GetComponent<Renderer>().sharedMaterial=cm;
}
cam.ResetAspect();
var vol=effects.Find("Hologram bloom");if(!vol){vol=new GameObject("Hologram bloom").transform;vol.SetParent(effects,false);}
var volume=vol.GetComponent<Volume>();if(!volume)volume=vol.gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=10;
var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(R+"/Materials/Assembly bloom.asset");if(!profile){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,R+"/Materials/Assembly bloom.asset");}
Bloom bloom;if(!profile.TryGet(out bloom)){bloom=profile.Add<Bloom>(true);AssetDatabase.AddObjectToAsset(bloom,profile);}bloom.intensity.Override(.75f);bloom.threshold.Override(1.1f);bloom.scatter.Override(.65f);volume.sharedProfile=profile;EditorUtility.SetDirty(profile);
PrefabUtility.SaveAsPrefabAsset(bank.gameObject,R+"/Prefabs/DistantClouds.prefab");
// Store a neutral cube so level authors can stretch XYZ directly.
var neutral=Object.Instantiate(box.gameObject);neutral.transform.SetParent(null);neutral.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);neutral.transform.localScale=Vector3.one;PrefabUtility.SaveAsPrefabAsset(neutral,R+"/Prefabs/AssemblyBounds.prefab");Object.DestroyImmediate(neutral);
PrefabUtility.SaveAsPrefabAsset(root,R+"/Prefabs/BeautifulCorner.prefab");EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s);AssetDatabase.SaveAssets();Selection.activeGameObject=box.gameObject;
CreekCloudNoiseAuthoring.Apply();
}
static Mesh RoundedGrid(){
string path=R+"/Meshes/Effects/RoundedBoundsGrid.asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old)return old;
var verts=new System.Collections.Generic.List<Vector3>();var normals=new System.Collections.Generic.List<Vector3>();var tris=new System.Collections.Generic.List<int>();int n=24;
var faceNormals=new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
foreach(var normal in faceNormals){var u=Mathf.Abs(normal.y)>.5f?Vector3.right:Vector3.up;var v=Vector3.Cross(normal,u);int start=verts.Count;for(int y=0;y<=n;y++)for(int x=0;x<=n;x++){verts.Add(normal*.5f+u*((float)x/n-.5f)+v*((float)y/n-.5f));normals.Add(normal);}for(int y=0;y<n;y++)for(int x=0;x<n;x++){int a=start+y*(n+1)+x;tris.Add(a);tris.Add(a+1);tris.Add(a+n+2);tris.Add(a);tris.Add(a+n+2);tris.Add(a+n+1);}}
var mesh=new Mesh();mesh.name="Tessellated bounds for scale-independent rounded corners";mesh.SetVertices(verts);mesh.SetNormals(normals);mesh.SetTriangles(tris,0);mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
}
static void Frame(Transform parent){
var child=parent.Find("Neon rounded edge core");if(!child){child=new GameObject("Neon rounded edge core").transform;child.SetParent(parent,false);child.gameObject.AddComponent<MeshFilter>();child.gameObject.AddComponent<MeshRenderer>();}
var vertices=new System.Collections.Generic.List<Vector3>();var normals=new System.Collections.Generic.List<Vector3>();var colors=new System.Collections.Generic.List<Color>();var triangles=new System.Collections.Generic.List<int>();
Action<Vector3,Vector3,bool> segment=(a,b,bright)=>{var dir=(b-a).normalized;var u=Vector3.Cross(dir,Mathf.Abs(dir.y)>.9f?Vector3.right:Vector3.up).normalized;var v=Vector3.Cross(dir,u);int start=vertices.Count;for(int end=0;end<2;end++)for(int k=0;k<8;k++){vertices.Add(end==0?a:b);normals.Add(u*Mathf.Cos(k*Mathf.PI/4)+v*Mathf.Sin(k*Mathf.PI/4));colors.Add(new Color(bright?1:0,0,0,1));}for(int k=0;k<8;k++){int q=(k+1)%8;triangles.Add(start+k);triangles.Add(start+q);triangles.Add(start+8+q);triangles.Add(start+k);triangles.Add(start+8+q);triangles.Add(start+8+k);}};
float rx=.025f/2.85f,rz=.025f/1.3f;
foreach(float y in new[]{-.5f,.5f}){
var points=new System.Collections.Generic.List<Vector3>();
for(int corner=0;corner<4;corner++){float angle=corner*Mathf.PI/2;float cx=corner==0||corner==3?.5f-rx:-.5f+rx;float cz=corner<2?.5f-rz:-.5f+rz;for(int k=0;k<=8;k++){float a=angle+k*Mathf.PI/16;points.Add(new Vector3(cx+rx*Mathf.Cos(a),y,cz+rz*Mathf.Sin(a)));}}
for(int j=0;j<points.Count;j++){var a=points[j];var b=points[(j+1)%points.Count];segment(a,b,j%9<8);}
}
foreach(float x in new[]{-1f,1f})foreach(float z in new[]{-1f,1f}){var a=new Vector3(x*(.5f-rx*.293f),-.5f,z*(.5f-rz*.293f));var b=a+Vector3.up;segment(a,b,false);}
var mesh=new Mesh();mesh.name="Rounded cyan edge tubes";mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.bounds=new Bounds(Vector3.zero,Vector3.one*1.2f);
string path=R+"/Meshes/Effects/NeonFrame.asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);mesh=old;}else AssetDatabase.CreateAsset(mesh,path);child.GetComponent<MeshFilter>().sharedMesh=mesh;
string mp=R+"/Materials/Neon frame.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(mp);if(!material){material=new Material(Shader.Find("Creek Diorama/Neon Frame"));AssetDatabase.CreateAsset(material,mp);}child.GetComponent<Renderer>().sharedMaterial=material;child.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
}
}
}

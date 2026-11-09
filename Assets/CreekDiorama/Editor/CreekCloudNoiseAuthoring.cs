using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace CreekDiorama.Editor {
public static class CreekCloudNoiseAuthoring {
const string R="Assets/CreekDiorama";
public static void Apply(){
foreach(var file in new[]{"CloudPerimeterNoise","CloudWispNoise"}){
var imp=(TextureImporter)AssetImporter.GetAtPath(R+"/Textures/Clouds/"+file+".png");imp.sRGBTexture=false;imp.wrapMode=TextureWrapMode.Repeat;imp.mipmapEnabled=true;imp.maxTextureSize=1024;imp.textureCompression=TextureImporterCompression.CompressedHQ;imp.SaveAndReimport();}
var broad=AssetDatabase.LoadAssetAtPath<Texture2D>(R+"/Textures/Clouds/CloudPerimeterNoise.png");var fine=AssetDatabase.LoadAssetAtPath<Texture2D>(R+"/Textures/Clouds/CloudWispNoise.png");
foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{R+"/Materials"})){
var mat=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));if(mat.shader.name!="Creek Diorama/Cloud Billboard")continue;
mat.SetTexture("_NoiseTex",broad);mat.SetTexture("_WispTex",fine);mat.SetFloat("_EdgeWarp",.022f);mat.SetFloat("_WispAmount",.55f);mat.SetFloat("_NoiseSpeed",.008f);EditorUtility.SetDirty(mat);
}
var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(R+"/Scenes/BeautifulCorner.unity");
var root=scene.GetRootGameObjects().First(g=>g.name.StartsWith("Beautiful Corner"));var box=root.transform.Find("Atmosphere and assembly area/AssemblyBounds");
if(PrefabUtility.IsPartOfPrefabInstance(box.gameObject))PrefabUtility.UnpackPrefabInstance(box.gameObject,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
var boundsMat=box.GetComponent<Renderer>().sharedMaterial;boundsMat.SetFloat("_SurfaceMode",0);boundsMat.SetFloat("_EmblemScale",.52f);EditorUtility.SetDirty(boundsMat);
var table=box.Find("Assembly tabletop - shared footprint");if(!table){table=new GameObject("Assembly tabletop - shared footprint").transform;table.SetParent(box,false);table.gameObject.AddComponent<MeshFilter>();table.gameObject.AddComponent<MeshRenderer>();}
table.localPosition=Vector3.zero;table.localRotation=Quaternion.identity;table.localScale=Vector3.one;
string path=R+"/Meshes/Effects/AssemblyTabletop.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
if(!mesh){var source=box.GetComponent<MeshFilter>().sharedMesh;var verts=source.vertices;var normals=source.normals;var tris=source.triangles;var selected=new System.Collections.Generic.List<int>();for(int i=0;i<tris.Length;i+=3)if(normals[tris[i]].y<-.5f){selected.Add(tris[i]);selected.Add(tris[i+1]);selected.Add(tris[i+2]);}mesh=new Mesh();mesh.name="Tabletop exactly matching bounds footprint";mesh.vertices=verts;mesh.normals=normals;mesh.triangles=selected.ToArray();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);}
// Remove vertices belonging to the other five cube faces, keeping exact tabletop bounds.
var used=mesh.triangles.Distinct().ToArray();
if(used.Length<mesh.vertexCount){var v=mesh.vertices;var n=mesh.normals;var tr=mesh.triangles;var remap=new System.Collections.Generic.Dictionary<int,int>();for(int j=0;j<used.Length;j++)remap[used[j]]=j;mesh.Clear();mesh.vertices=used.Select(j=>v[j]).ToArray();mesh.normals=used.Select(j=>n[j]).ToArray();mesh.triangles=tr.Select(j=>remap[j]).ToArray();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);}
table.GetComponent<MeshFilter>().sharedMesh=mesh;
string mp=R+"/Materials/Assembly tabletop.mat";var surface=AssetDatabase.LoadAssetAtPath<Material>(mp);if(!surface){surface=new Material(boundsMat);AssetDatabase.CreateAsset(surface,mp);}else EditorUtility.CopySerialized(boundsMat,surface);
surface.name="Assembly tabletop";surface.SetFloat("_SurfaceMode",1);surface.SetFloat("_EmblemScale",.52f);EditorUtility.SetDirty(surface);
var renderer=table.GetComponent<MeshRenderer>();renderer.sharedMaterial=surface;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
// Both the grid and boundary use identical unit-space XZ extents and inherit the same transform.
var neutral=Object.Instantiate(box.gameObject);neutral.transform.SetParent(null);neutral.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);neutral.transform.localScale=Vector3.one;PrefabUtility.SaveAsPrefabAsset(neutral,R+"/Prefabs/AssemblyBounds.prefab");Object.DestroyImmediate(neutral);
var clouds=root.transform.Find("Atmosphere and assembly area/Layered realistic cloud banks");PrefabUtility.SaveAsPrefabAsset(clouds.gameObject,R+"/Prefabs/DistantClouds.prefab");PrefabUtility.SaveAsPrefabAsset(root,R+"/Prefabs/BeautifulCorner.prefab");
EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Selection.activeGameObject=box.gameObject;
}
}
}

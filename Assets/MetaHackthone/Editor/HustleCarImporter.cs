using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
namespace PhysicalAssembly.Editor {
 public static class HustleCarImporter {
  const string Art="Assets/MetaHackthone/Art/HustleCar";
  const string Generated=Art+"/Unity";
  [MenuItem("MetaHackthone/Import Hustle car visuals")]
  public static void Import() {
   Directory.CreateDirectory(Generated); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
   var body=MakeMaterial("Body"); var wheel=MakeMaterial("Wheel"); var camera=MakeMaterial("Camera");
   var neutral=new Material(Shader.Find("Universal Render Pipeline/Lit")); neutral.name="Hustle metal"; neutral.color=new Color(.38f,.4f,.43f); neutral.SetFloat("_Metallic",.5f); neutral.SetFloat("_Smoothness",.4f); neutral=Persist(neutral,Generated+"/Metal.mat");
   foreach(var file in Directory.GetFiles(Art,"*.fbx")) {
    var path=file.Replace('\\','/'); var importer=(ModelImporter)AssetImporter.GetAtPath(path);
    importer.isReadable=true; importer.addCollider=false; importer.importAnimation=false;
    var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
    foreach(var material in model.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m).Distinct()) {
     var mapped=material.name.Contains("Wheel")?wheel:material.name.Contains("Body")?body:material.name.Contains("Camera")?camera:neutral;
     importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),material.name),mapped);
    }
    importer.SaveAndReimport();
   }
   var library=ScriptableObject.CreateInstance<HustleVisualLibrary>();
   library.entries=new[]{
    Entry(PartKind.Chassis,"chassis","CarParts_Body_Extension_sm",HustlePartProfile.ChassisSize,90,body),
    Entry(PartKind.Wheel,"wheel","CarParts_Wheel_sm",new Vector3(HustlePartProfile.WheelWidth,HustlePartProfile.WheelDiameter,HustlePartProfile.WheelDiameter),0,wheel),
    Entry(PartKind.Wheel,"mount","CarParts_Wheel_Axle_sm",new Vector3(HustlePartProfile.MountWidth,.04f,.04f),0,wheel),
    Entry(PartKind.Wheel,"hub","CarParts_Spring_sm",new Vector3(HustlePartProfile.HubWidth,.04f,.04f),0,neutral),
    Entry(PartKind.Weapon,"weapon","CarParts_MainBody_sm",new Vector3(.12f,.08f,.18f),90,neutral),
    Entry(PartKind.Beam,"body","CarParts_Body_Extension_Thin_sm",HustlePartProfile.BeamSize,90,neutral),
    Entry(PartKind.Block,"body","CarParts_Body_Extension_1x1_sm",Vector3.one*.1f,90,neutral)
   };
   library=Persist(library,"Assets/MetaHackthone/Resources/HustleVisualLibrary.asset");
   AssetDatabase.SaveAssets(); ProjectBuilder.RebuildPartPrefabs();
   Debug.Log("HUSTLE_VISUAL_IMPORT_OK entries="+library.entries.Length);
  }
  static HustleVisualLibrary.Entry Entry(PartKind kind,string body,string source,Vector3 size,float yaw,Material material) {
   var model=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/"+source+".fbx");
   if(!model) throw new InvalidOperationException("Missing source mesh: "+source);
   var filters=model.GetComponentsInChildren<MeshFilter>();
   var mesh=new Mesh(); mesh.name=source+"_"+body;
   mesh.CombineMeshes(filters.Select(f=>new CombineInstance {mesh=f.sharedMesh,transform=Matrix4x4.Rotate(Quaternion.Euler(0,yaw,0))*f.transform.localToWorldMatrix}).ToArray(),true,true);
   var bounds=mesh.bounds; var vertices=mesh.vertices;
   var scale=new Vector3(size.x/bounds.size.x,size.y/bounds.size.y,size.z/bounds.size.z);
   for(int i=0;i<vertices.Length;i++) vertices[i]=Vector3.Scale(vertices[i]-bounds.center,scale);
   mesh.vertices=vertices; mesh.RecalculateBounds(); mesh.RecalculateNormals(); mesh.RecalculateTangents();
   mesh=Persist(mesh,Generated+"/"+kind+"_"+body+".asset");
   return new HustleVisualLibrary.Entry {kind=kind,body=body,mesh=mesh,material=material};
  }
  static Material MakeMaterial(string name) {
   string prefix=Art+"/RCCar_"+name;
   var baseColor=Texture(prefix+"_Basecolor.tga",true,false);
   var normal=Texture(prefix+"_Normal.tga",false,true);
   var arm=Texture(prefix+"_ARM.tga",false,false,true);
   var pixels=arm.GetPixels(); var metal=new Color[pixels.Length]; var occlusion=new Color[pixels.Length];
   for(int i=0;i<pixels.Length;i++) { metal[i]=new Color(pixels[i].b,0,0,1-pixels[i].g); occlusion[i]=new Color(1,pixels[i].r,1,1); }
   var packed=new Texture2D(arm.width,arm.height,TextureFormat.RGBA32,false,true); packed.SetPixels(metal); packed.Apply();
   var metalPath=Generated+"/"+name+"_MetalSmooth.png"; File.WriteAllBytes(metalPath,packed.EncodeToPNG());
   packed.SetPixels(occlusion); packed.Apply(); var aoPath=Generated+"/"+name+"_Occlusion.png"; File.WriteAllBytes(aoPath,packed.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(packed);
   AssetDatabase.ImportAsset(metalPath); AssetDatabase.ImportAsset(aoPath);
   var material=new Material(Shader.Find("Universal Render Pipeline/Lit")); material.name="Hustle "+name;
   material.SetTexture("_BaseMap",baseColor); material.SetTexture("_BumpMap",normal); material.EnableKeyword("_NORMALMAP");
   material.SetTexture("_MetallicGlossMap",Texture(metalPath,false,false)); material.SetFloat("_Metallic",1); material.SetFloat("_Smoothness",1); material.EnableKeyword("_METALLICSPECGLOSSMAP");
   material.SetTexture("_OcclusionMap",Texture(aoPath,false,false)); material.EnableKeyword("_OCCLUSIONMAP");
   if(File.Exists(prefix+"_Emissive.tga")) { material.SetTexture("_EmissionMap",Texture(prefix+"_Emissive.tga",true,false)); material.SetColor("_EmissionColor",Color.white); material.EnableKeyword("_EMISSION"); }
   return Persist(material,Generated+"/"+name+".mat");
  }
  static Texture2D Texture(string path,bool srgb,bool normal,bool readable=false) {
   var importer=(TextureImporter)AssetImporter.GetAtPath(path);
   importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
   importer.sRGBTexture=srgb; importer.isReadable=readable; importer.mipmapEnabled=true; importer.maxTextureSize=2048;
   if(normal) importer.flipGreenChannel=true;
   importer.textureCompression=readable?TextureImporterCompression.Uncompressed:TextureImporterCompression.Compressed;
   importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
  }
  static T Persist<T>(T value,string path) where T:UnityEngine.Object {
   var existing=AssetDatabase.LoadAssetAtPath<T>(path);
   if(!existing) { AssetDatabase.CreateAsset(value,path); return value; }
   EditorUtility.CopySerialized(value,existing); UnityEngine.Object.DestroyImmediate(value); return existing;
  }
 }
}

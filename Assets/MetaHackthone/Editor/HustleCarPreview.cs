using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace PhysicalAssembly.Editor {
 public static class HustleCarPreview {
  public static void Render() {
   var previous=SceneManager.GetActiveScene(); if(string.IsNullOrEmpty(previous.path)) previous=EditorSceneManager.OpenScene("Assets/MetaHackthone/Scenes/Workshop.unity"); var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
   try {
    var chassis=Spawn(PartKind.Chassis,Vector3.zero,Quaternion.identity);
    foreach(var socket in chassis.Sockets) {
     bool wheel=socket.Family=="wheel"; var part=Spawn(wheel?PartKind.Wheel:PartKind.Weapon,Vector3.zero,wheel&&socket.transform.localPosition.x<0?Quaternion.Euler(0,180,0):Quaternion.identity);
     part.transform.position+=socket.transform.position-part.FindSocket("mount").transform.position;
    }
    var floor=GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.position=new Vector3(0,-.15f,0); floor.transform.localScale=new Vector3(3,.02f,3); floor.GetComponent<Renderer>().sharedMaterial=PartFactory.Material(new Color(.12f,.15f,.19f));
    var key=new GameObject("Key").AddComponent<Light>(); key.type=LightType.Directional; key.intensity=2.5f; key.transform.rotation=Quaternion.Euler(40,-30,0);
    var fill=new GameObject("Fill").AddComponent<Light>(); fill.type=LightType.Directional; fill.intensity=1f; fill.transform.rotation=Quaternion.Euler(25,150,0);
    var camera=new GameObject("Preview camera").AddComponent<Camera>(); camera.transform.position=new Vector3(.85f,.65f,1f); camera.transform.LookAt(new Vector3(0,0,0)); camera.fieldOfView=40; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.07f,.09f,.12f);
    foreach(var root in scene.GetRootGameObjects()) foreach(var node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer=31;
    camera.cullingMask=1<<31;
    var target=new RenderTexture(1280,960,24); target.antiAliasing=4; camera.targetTexture=target; camera.Render();
    var old=RenderTexture.active; RenderTexture.active=target; var image=new Texture2D(1280,960,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1280,960),0,0); image.Apply();
    Directory.CreateDirectory("Docs"); File.WriteAllBytes("Docs/HustleCarPreview.png",image.EncodeToPNG()); RenderTexture.active=old; camera.targetTexture=null; UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(target);
    Debug.Log("HUSTLE_PREVIEW_OK");
   } finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene,true); }
  }
  static AssemblyPart Spawn(PartKind kind,Vector3 position,Quaternion rotation) {
   var prefab=AssetDatabase.LoadAssetAtPath<AssemblyPart>("Assets/MetaHackthone/Generated/"+kind+".prefab");
   var part=UnityEngine.Object.Instantiate(prefab,position,rotation);
   foreach(var body in part.GetComponentsInChildren<Rigidbody>()) body.isKinematic=true;
   return part;
  }
 }
}

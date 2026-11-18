using System.IO;
using UnityEditor;
using UnityEngine;

namespace FireRescue.Editor
{
    // Review renders of kit prefabs under the game's key light. Editor only; nothing is saved into a scene.
    public static class EmberdeepArtPreview
    {
        // The SDK's left hand in its bind pose wearing the glove shader, cuff and crystal.
        public static string RenderHand(string output,float yaw,float pitch,float fill=.6f,int width=1400,int height=900)
        {
            var art=AssetDatabase.LoadAssetAtPath<EmberdeepArtLibrary>("Assets/FireRescue/Resources/EmberdeepArtV2/Library.asset");
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.meta.xr.sdk.interaction/Runtime/Meshes/OpenXRLeftHand.fbx");
            if(!art || !source)return "missing assets";
            var root=new GameObject("Hand preview (temporary)");root.hideFlags=HideFlags.HideAndDontSave;
            var texture=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){antiAliasing=4};
            Mesh baked=null;Material hand=null;
            try
            {
                var skinned=source.GetComponentInChildren<SkinnedMeshRenderer>(true);var bones=skinned.bones;
                int wrist=System.Array.FindIndex(bones,b=>b.name=="XRHand_Wrist"),middle=System.Array.FindIndex(bones,b=>b.name=="XRHand_MiddleProximal");
                baked=EmberdeepHandPresentation.BakeGlovePanels(skinned.sharedMesh,bones,wrist,middle,true);
                hand=new Material(Shader.Find("Emberdeep/Tracked Hand Interior Occlusion"));
                var model=new GameObject("Glove");model.transform.SetParent(root.transform,false);
                model.AddComponent<MeshFilter>().sharedMesh=baked;model.AddComponent<MeshRenderer>().sharedMaterial=hand;
                // Bind pose: +Y toward the fingers, +Z out of the palm.
                var frame=Quaternion.LookRotation(Vector3.up,Vector3.forward);
                var cuff=art.Create("GloveCuff",root.transform,Vector3.zero);cuff.localRotation=frame;
                foreach(var r in cuff.GetComponentsInChildren<Renderer>())if(r.name.StartsWith("Sleeve"))r.sharedMaterial=hand;
                var crystal=new GameObject("Crystal").AddComponent<EmberdeepManaCrystal>();crystal.transform.SetParent(root.transform,false);
                crystal.transform.localPosition=new Vector3(.001f,.048f,-.016f);crystal.transform.localRotation=frame;
                crystal.SendMessage("Awake",SendMessageOptions.DontRequireReceiver);crystal.SetFill(fill);
                foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.hideFlags=HideFlags.HideAndDontSave;
                var lamp=new GameObject("Preview sun").AddComponent<Light>();lamp.transform.SetParent(root.transform,false);lamp.gameObject.hideFlags=HideFlags.HideAndDontSave;
                lamp.type=LightType.Directional;lamp.intensity=1.1f;lamp.color=new Color(1,.91f,.79f);lamp.transform.rotation=Quaternion.Euler(48,-35,0);
                var camera=new GameObject("Preview camera").AddComponent<Camera>();camera.transform.SetParent(root.transform,false);camera.gameObject.hideFlags=HideFlags.HideAndDontSave;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.80f,.87f,.86f);camera.fieldOfView=30;
                camera.nearClipPlane=.02f;camera.farClipPlane=3;
                var rotation=Quaternion.Euler(pitch,yaw,0);
                camera.transform.SetPositionAndRotation(new Vector3(0,.06f,0)-rotation*Vector3.forward*.62f,rotation);
                camera.targetTexture=texture;camera.Render();
                RenderTexture.active=texture;var image=new Texture2D(width,height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();RenderTexture.active=null;camera.targetTexture=null;
                Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllBytes(output,image.EncodeToPNG());Object.DestroyImmediate(image);
                return "rendered "+output+" baked="+(baked!=skinned.sharedMesh);
            }
            finally
            {
                Object.DestroyImmediate(root);texture.Release();Object.DestroyImmediate(texture);
                if(hand)Object.DestroyImmediate(hand);if(baked && AssetDatabase.GetAssetPath(baked)=="")Object.DestroyImmediate(baked);
            }
        }
        public static string Render(string ids,string output,float yaw=-28,float pitch=18,float zoom=1,int width=1400,int height=900)
        {
            var art=AssetDatabase.LoadAssetAtPath<EmberdeepArtLibrary>("Assets/FireRescue/Resources/EmberdeepArtV2/Library.asset");
            if(!art)return "library missing";
            var root=new GameObject("Art preview (temporary)");root.hideFlags=HideFlags.HideAndDontSave;
            var texture=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){antiAliasing=4};
            try
            {
                float cursor=0;var bounds=new Bounds();bool any=false;
                foreach(var id in ids.Split(','))
                {
                    var instance=art.Create(id.Trim(),root.transform,Vector3.zero);
                    if(!instance)return "missing "+id;
                    foreach(var t in instance.GetComponentsInChildren<Transform>(true))t.gameObject.hideFlags=HideFlags.HideAndDontSave;
                    var b=new Bounds(instance.position,Vector3.zero);
                    foreach(var r in instance.GetComponentsInChildren<Renderer>())b.Encapsulate(r.bounds);
                    instance.position+=Vector3.right*(cursor-b.min.x);cursor+=b.size.x*1.15f;
                    b.center+=Vector3.right*(instance.position.x);
                    if(any)bounds.Encapsulate(b);else{bounds=b;any=true;}
                }
                var lamp=new GameObject("Preview sun").AddComponent<Light>();lamp.transform.SetParent(root.transform,false);lamp.gameObject.hideFlags=HideFlags.HideAndDontSave;
                lamp.type=LightType.Directional;lamp.intensity=1.1f;lamp.color=new Color(1,.91f,.79f);lamp.transform.rotation=Quaternion.Euler(48,-35,0);
                var camera=new GameObject("Preview camera").AddComponent<Camera>();camera.transform.SetParent(root.transform,false);camera.gameObject.hideFlags=HideFlags.HideAndDontSave;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.80f,.87f,.86f);camera.fieldOfView=30;
                float radius=bounds.extents.magnitude;float distance=radius/Mathf.Sin(15*Mathf.Deg2Rad)/zoom;
                camera.nearClipPlane=Mathf.Max(.005f,distance-radius*2);camera.farClipPlane=distance+radius*4;
                var rotation=Quaternion.Euler(pitch,yaw,0);
                camera.transform.SetPositionAndRotation(bounds.center-rotation*Vector3.forward*distance,rotation);
                camera.targetTexture=texture;camera.Render();
                RenderTexture.active=texture;var image=new Texture2D(width,height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();RenderTexture.active=null;camera.targetTexture=null;
                Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllBytes(output,image.EncodeToPNG());Object.DestroyImmediate(image);
                return "rendered "+output+" size "+bounds.size.ToString("F3");
            }
            finally{Object.DestroyImmediate(root);texture.Release();Object.DestroyImmediate(texture);}
        }
    }
}

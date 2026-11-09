using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PhysicalAssembly.Editor
{
    [InitializeOnLoad]
    public static class PreviewCapture
    {
        const string Pending="MetaHackthone.CapturePreview";
        static double readyAt;
        static PreviewCapture() { EditorApplication.update+=Update; }
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/MetaHackthone/Scenes/Workshop.unity");
            SessionState.SetBool(Pending,true); EditorApplication.EnterPlaymode();
        }
        static void Update()
        {
            if(!SessionState.GetBool(Pending,false) || !EditorApplication.isPlaying) return;
            if(readyAt==0) { readyAt=EditorApplication.timeSinceStartup+3; return; }
            if(EditorApplication.timeSinceStartup<readyAt) return;
            SessionState.EraseBool(Pending);
            try
            {
                var desktop=UnityEngine.Object.FindFirstObjectByType<DesktopInputAdapter>();
                var camera=desktop.View;
                desktop.FramePlatform(); camera.fieldOfView=55;
                var target=new RenderTexture(1440,960,24,RenderTextureFormat.ARGB32); target.Create();
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination=target });
                var old=RenderTexture.active; RenderTexture.active=target;
                var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,target.width,target.height),0,0); image.Apply();
                Directory.CreateDirectory("Docs"); File.WriteAllBytes("Docs/Preview.png",image.EncodeToPNG());
                RenderTexture.active=old; target.Release(); UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(target);
                Debug.Log("METAHACKTHONE_PREVIEW_OK"); EditorApplication.Exit(0);
            }
            catch(Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}

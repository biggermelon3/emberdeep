using System.Collections;
using CloudGarden.Integration;
using NUnit.Framework;
using Oculus.Interaction;
using PhysicalAssembly.MetaXR;
using UnityEngine;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    public sealed class EmberdeepHandVisualPlayTests
    {
        [UnityTest] public IEnumerator ReusesRigHandMeshAndSuppressesOnlyRayVisualsThenRestoresSharedState()
        {
            var owner=new GameObject("Emberdeep hand adapter test");
            var rig=new GameObject("Existing workshop rig");rig.SetActive(false);
            var player=owner.AddComponent<MetaPlayerRig>();player.XRRig=rig.transform;
            var provider=owner.AddComponent<MetaShadowPlayer>();provider.enabled=false;provider.Player=player;
            var skinObject=new GameObject("Existing SDK hand");skinObject.transform.SetParent(rig.transform);
            var skin=skinObject.AddComponent<SkinnedMeshRenderer>();
            var original=new Material(Shader.Find("Universal Render Pipeline/Unlit"));skin.sharedMaterial=original;
            var hand=skinObject.AddComponent<HandVisual>();hand.enabled=false;hand.ForceOffVisibility=true;
            var rayObject=GameObject.CreatePrimitive(PrimitiveType.Cube);rayObject.name="Existing SDK ray visual";rayObject.transform.SetParent(rig.transform);
            var renderer=rayObject.GetComponent<Renderer>();var rayVisual=rayObject.AddComponent<RayInteractorRayVisual>();
            var ray=rayObject.AddComponent<RayInteractor>();
            var adapter=owner.AddComponent<EmberdeepHandVisuals>();adapter.Configure(provider);
            try
            {
            Assert.AreEqual(1,adapter.BoundVisualCount);Assert.NotNull(adapter.RuntimeMaterial);
            Assert.AreSame(adapter.RuntimeMaterial,skin.sharedMaterial);Assert.AreEqual(1,rig.GetComponentsInChildren<HandVisual>(true).Length);
            Assert.IsTrue(renderer.forceRenderingOff,"Native SDK ray must not draw alongside Emberdeep's target-only ray");
            Assert.IsFalse(rayVisual.enabled);Assert.IsTrue(ray.enabled,"Visual suppression must leave the input interactor untouched");
            Assert.IsTrue(hand.ForceOffVisibility,"Existing hidden hands cannot be exposed by configuration");
            adapter.enabled=false;
            Assert.AreSame(original,skin.sharedMaterial);Assert.IsFalse(renderer.forceRenderingOff);Assert.IsTrue(rayVisual.enabled);Assert.IsTrue(ray.enabled);
            Assert.IsTrue(hand.ForceOffVisibility);Assert.IsNull(adapter.RuntimeMaterial);
            }
            finally{Object.Destroy(owner);Object.Destroy(rig);Object.Destroy(original);}
            yield return null;
        }

        // Uses a real material and URP render to verify an inside-solid hand
        // fragment disappears even with no physical solid drawing in front of it.
        [UnityTest] public IEnumerator InteriorPixelsAreClippedWithoutHidingTheWholeHand()
        {
            var shader=Resources.Load<Shader>("EmberdeepTrackedHand");Assert.NotNull(shader);
            var material=new Material(shader);var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name="Hand fragment test surface";quad.layer=31;quad.transform.position=new Vector3(40,40,40);quad.GetComponent<Renderer>().sharedMaterial=material;
            var cameraObject=new GameObject("Occlusion test camera");var camera=cameraObject.AddComponent<Camera>();
            camera.transform.position=quad.transform.position+Vector3.back*2;camera.orthographic=true;camera.orthographicSize=.6f;camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=Color.black;camera.cullingMask=1<<31;camera.enabled=false;
            var target=new RenderTexture(64,64,24,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;
            var matrices=new Matrix4x4[EmberdeepHandVisuals.MaxOcclusionBoxes];var sizes=new Vector4[EmberdeepHandVisuals.MaxOcclusionBoxes];
            var pixels=new Texture2D(64,64,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try
            {
            for(int i=0;i<matrices.Length;i++)matrices[i]=Matrix4x4.identity;
            matrices[0]=Matrix4x4.Translate(-quad.transform.position);sizes[0]=new Vector4(.18f,.6f,.1f,1);
            material.SetMatrixArray("_HandClipWorldToLocal",matrices);material.SetVectorArray("_HandClipHalfSize",sizes);
            camera.Render();
            RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,64,64),0,0);pixels.Apply();RenderTexture.active=previous;
            Assert.Less(pixels.GetPixel(32,32).grayscale,.03f,"Interior hand pixels must be discarded");
            Assert.Greater(pixels.GetPixel(12,32).grayscale,.1f,"Hand outside the solid remains visible");
            sizes[0]=Vector4.zero;material.SetVectorArray("_HandClipHalfSize",sizes);camera.Render();
            previous=RenderTexture.active;RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,64,64),0,0);pixels.Apply();RenderTexture.active=previous;
            Assert.Greater(pixels.GetPixel(32,32).grayscale,.1f,"Removing a solid restores the tracked hand pixels");
            }
            finally
            {
                RenderTexture.active=previous;camera.targetTexture=null;Object.Destroy(cameraObject);Object.Destroy(quad);
                Object.Destroy(material);Object.Destroy(pixels);target.Release();Object.Destroy(target);
            }
            yield return null;
        }
    }
}

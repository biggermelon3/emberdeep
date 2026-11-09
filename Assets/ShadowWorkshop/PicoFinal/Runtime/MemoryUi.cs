using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShapeOfMemory
{
    // Materials the game's physical UI is built from, and what it owns (runtime materials and textures).
    public sealed class MemoryKit
    {
        public Material Surface,Screen,Icon;
        public readonly List<Object> Owned=new List<Object>();
        public void Release(){foreach(var item in Owned)if(item)Object.Destroy(item);Owned.Clear();}
    }

    // Shared palette and builders for the console, cards and labels. Graybox until art arrives; flat things face up
    // with Quaternion.Euler(90,0,0) and read with their top away from the player.
    public static class MemoryUi
    {
        public static readonly Color Paper=new Color(.90f,.86f,.78f),Ink=new Color(.14f,.13f,.13f),Slate=new Color(.10f,.14f,.16f),
            Mint=new Color(.45f,.88f,.75f),Gold=new Color(.94f,.70f,.39f),Dim=new Color(.34f,.36f,.37f),Shade=new Color(.15f,.13f,.12f);
        public static readonly Quaternion FaceUp=Quaternion.Euler(90,0,0);
        static MaterialPropertyBlock block;

        public static void Tint(Renderer r,Color color){block??=new MaterialPropertyBlock();r.GetPropertyBlock(block);block.SetColor("_BaseColor",color);r.SetPropertyBlock(block);}
        public static GameObject Shape(PrimitiveType type,string name,Transform parent,Vector3 position,Quaternion rotation,Vector3 scale,Material material,Color color)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;Kill(go.GetComponent<Collider>());
            go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=rotation;go.transform.localScale=scale;
            var r=go.GetComponent<Renderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;Tint(r,color);return go;
        }
        // A quad readable from -Z of its rotation, showing texture through an instance of material (tinted by color).
        public static Renderer Picture(string name,Transform parent,Vector3 position,Quaternion rotation,Vector2 size,Material material,Texture texture,Color color,List<Object> owned)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Quad);go.name=name;Kill(go.GetComponent<Collider>());
            go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=rotation;go.transform.localScale=new Vector3(size.x,size.y,1);
            var m=new Material(material){name=name};m.SetTexture("_BaseMap",texture);m.SetColor("_BaseColor",Color.white);owned.Add(m);
            var r=go.GetComponent<Renderer>();r.sharedMaterial=m;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;Tint(r,color);return r;
        }
        public static TMP_Text Label(string text,Transform parent,Vector3 position,Quaternion rotation,float size,Vector2 bounds,Color color,TextAlignmentOptions align=TextAlignmentOptions.Center)
        {
            var go=new GameObject("Label",typeof(TextMeshPro));go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=rotation;
            var label=go.GetComponent<TextMeshPro>();label.text=text;label.fontSize=size*10;label.color=color;label.alignment=align;
            label.rectTransform.sizeDelta=bounds;label.textWrappingMode=TextWrappingModes.Normal;return label;
        }
        // A short dry tick for every press.
        public static AudioClip Click(List<Object> owned)
        {
            var samples=new float[2200];var random=new System.Random(7);
            for(int i=0;i<samples.Length;i++){float t=i/44100f;samples[i]=((float)random.NextDouble()*2-1)*.30f*Mathf.Exp(-t*700)+Mathf.Sin(t*Mathf.PI*2*1650)*.40f*Mathf.Exp(-t*220);}
            var clip=AudioClip.Create("Memory click",samples.Length,1,44100,false);clip.SetData(samples,0);owned.Add(clip);return clip;
        }
        public static void Kill(Object o){if(o)Object.Destroy(o);}
    }
}

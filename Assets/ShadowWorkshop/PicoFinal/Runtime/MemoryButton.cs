using System;
using TMPro;
using UnityEngine;

namespace ShapeOfMemory
{
    // A round push button on the console: dark collar, paper cap that sinks while pressed, a tinted icon on the cap and
    // its name on the console in front. Optional pips behind the cap (hints left) and, on a hold button, a ring of dots
    // that fills while it is held.
    public sealed class MemoryButton : MonoBehaviour
    {
        public const float Cap=.064f,Height=.016f,Travel=.007f;
        public Pressable Press{get;private set;}
        public string Caption{get=>label.text;set=>label.text=value;}
        public int PipsLit;
        Transform cap;
        Renderer body,icon;
        TMP_Text label;
        Renderer[] dots=Array.Empty<Renderer>(),pips=Array.Empty<Renderer>();
        float sink;

        public static MemoryButton Create(Transform parent,string name,string caption,Texture2D iconTexture,Vector3 position,MemoryKit kit,
            Action action,Func<bool> enabled,Func<bool> handsAllowed,float hold=0)
        {
            var root=new GameObject("Button "+name).transform;root.SetParent(parent,false);root.localPosition=position;
            var b=root.gameObject.AddComponent<MemoryButton>();
            MemoryUi.Shape(PrimitiveType.Cylinder,"Collar",root,new Vector3(0,.003f,0),Quaternion.identity,new Vector3(Cap+.014f,.003f,Cap+.014f),kit.Surface,MemoryUi.Slate*.6f);
            b.cap=new GameObject("Cap").transform;b.cap.SetParent(root,false);
            b.body=MemoryUi.Shape(PrimitiveType.Cylinder,"Cap body",b.cap,new Vector3(0,Height*.5f,0),Quaternion.identity,new Vector3(Cap,Height*.5f,Cap),kit.Surface,MemoryUi.Paper).GetComponent<Renderer>();
            if(iconTexture)b.icon=MemoryUi.Picture("Icon",b.cap,new Vector3(0,Height+.0006f,0),MemoryUi.FaceUp,Vector2.one*Cap*.6f,kit.Icon,iconTexture,MemoryUi.Ink,kit.Owned);
            b.label=MemoryUi.Label(caption,root,new Vector3(0,.0012f,-(Cap*.5f+.017f)),MemoryUi.FaceUp,.0125f,new Vector2(.10f,.02f),MemoryUi.Paper);
            // The pressable face is the cap's top at rest, so a poke selects as the finger meets the paper.
            var hit=new GameObject("Press",typeof(BoxCollider));hit.transform.SetParent(root,false);
            hit.transform.localPosition=new Vector3(0,Height*.5f,0);hit.transform.localScale=new Vector3(Cap+.01f,Height,Cap+.01f);
            b.Press=Pressable.Add(hit,action,enabled,handsAllowed,new Vector3(0,.51f,0),MemoryUi.FaceUp,hold);
            if(hold>0)
            {
                b.dots=new Renderer[16];float r=Cap*.5f+.013f;
                for(int i=0;i<b.dots.Length;i++)
                {
                    // Clockwise from twelve o'clock as seen from above.
                    float a=i*Mathf.PI*2/b.dots.Length;
                    b.dots[i]=MemoryUi.Shape(PrimitiveType.Cylinder,"Hold dot",root,new Vector3(Mathf.Sin(a)*r,.0015f,Mathf.Cos(a)*r),Quaternion.identity,new Vector3(.006f,.0015f,.006f),kit.Surface,MemoryUi.Slate).GetComponent<Renderer>();
                }
            }
            return b;
        }
        // A row of small dots behind the cap; PipsLit of them glow.
        public void Pips(int count)
        {
            pips=new Renderer[count];
            for(int i=0;i<count;i++)pips[i]=MemoryUi.Shape(PrimitiveType.Cylinder,"Pip",transform,new Vector3((i-(count-1)*.5f)*.014f,.0015f,Cap*.5f+.012f),Quaternion.identity,new Vector3(.007f,.0015f,.007f),body.sharedMaterial,MemoryUi.Slate).GetComponent<Renderer>();
        }
        void Update()
        {
            bool available=Press.Available,down=available&&Press.Down;
            sink=Mathf.MoveTowards(sink,down?Travel:0,Time.unscaledDeltaTime*.15f);cap.localPosition=new Vector3(0,-sink,0);
            MemoryUi.Tint(body,!available?MemoryUi.Dim:down?MemoryUi.Mint:Press.Hover?Color.Lerp(MemoryUi.Paper,Color.white,.65f):MemoryUi.Paper);
            if(icon)MemoryUi.Tint(icon,available?MemoryUi.Ink:new Color(.22f,.22f,.23f));
            label.color=available?MemoryUi.Paper:MemoryUi.Dim;
            int lit=Mathf.FloorToInt(Press.Held*dots.Length+.001f);
            for(int i=0;i<dots.Length;i++)MemoryUi.Tint(dots[i],i<lit?MemoryUi.Mint:MemoryUi.Slate);
            for(int i=0;i<pips.Length;i++)MemoryUi.Tint(pips[i],i<PipsLit?MemoryUi.Gold:MemoryUi.Slate);
        }
    }
}

using System;
using CloudGarden.ShadowPuzzle;
using PhysicalAssembly.MetaXR;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShapeOfMemory
{
    // One press path for hands and mouse. Hands poke the face or ray-pinch it through the rig's own interactors
    // (MetaMenuButton; the Pico build's UnityXR rig has the same ones); the desktop clicks it (ShadowControl, which
    // Torch2Input also honours during play). With HoldSeconds set the action fires only after a hold that long, so a
    // brushing hand cannot reset a level. Actions run on the next Update, outside the interactors' event dispatch,
    // because most of them swap the visible buttons.
    public sealed class Pressable : MonoBehaviour
    {
        public float HoldSeconds;
        public bool Hover{get;private set;}
        public bool Down=>hands||mouse;
        public bool Available=>control&&control.Available;
        // 0..1 while a hold is under way.
        public float Held=>holding&&HoldSeconds>0?Mathf.Clamp01(held/HoldSeconds):0;
        public ShadowControl Control=>control;
        public MetaMenuButton Native{get;private set;}
        ShadowControl control;
        Action action;
        bool hands,mouse,holding,pending;
        float held;

        // go: a unit cube scaled to the pressable part. face, facing: where hands press, in that cube's space (normal -Z).
        public static Pressable Add(GameObject go,Action action,Func<bool> enabled,Func<bool> handsAllowed,Vector3 face,Quaternion facing,float hold=0)
        {
            var p=go.AddComponent<Pressable>();p.action=action;p.HoldSeconds=hold;
            p.control=go.AddComponent<ShadowControl>();p.control.action=p.MousePress;p.control.enabledWhen=enabled;
            p.Native=go.AddComponent<MetaMenuButton>();
            p.Native.Initialize(p.Begin,()=>p.Available&&(handsAllowed==null||handsAllowed()),(hover,selected)=>{p.Hover=hover;p.hands=selected;},face,facing);
            return p;
        }
        void MousePress(){mouse=true;Begin();}
        void Begin(){if(HoldSeconds>0){holding=true;held=0;}else pending=true;}
        void Update()
        {
            if(mouse&&(Mouse.current==null||!Mouse.current.leftButton.isPressed))mouse=false;
            if(pending){pending=false;if(Available)action();return;}
            if(!holding)return;
            if(!Down||!Available){holding=false;return;}
            held+=Time.unscaledDeltaTime;if(held>=HoldSeconds){holding=false;action();}
        }
        void OnDisable(){hands=mouse=holding=pending=false;Hover=false;}
    }
}

using System;
using UnityEngine;
namespace PhysicalAssembly
{
    public sealed class WorkshopButton : MonoBehaviour
    {
        [Tooltip("Runtime action; independent of the object name or displayed label.")]
        public string Command;
        public Action Pressed;
        public bool IsHovered { get; private set; }
        public bool IsSelected { get; private set; }
        bool interactable=true;
        public bool Interactable
        {
            get=>interactable;
            set { if(interactable==value) return; interactable=value; SetInteractionState(false,false); }
        }
        Renderer visual; MaterialPropertyBlock properties;
        float lastPress=-10;
        void Awake() { visual=GetComponent<Renderer>(); properties=new MaterialPropertyBlock(); }
        public void SetInteractionState(bool hovered,bool selected)
        {
            IsHovered=hovered; IsSelected=selected;
            if(!visual) return;
            if(!interactable) { properties.SetColor("_BaseColor",new Color(.12f,.13f,.13f)); visual.SetPropertyBlock(properties); return; }
            if(!hovered&&!selected) { visual.SetPropertyBlock(null); return; }
            properties.SetColor("_BaseColor",selected?new Color(.2f,1,.75f):new Color(.16f,.46f,.56f));
            visual.SetPropertyBlock(properties);
        }
        public void Press()
        {
            if(!interactable || Time.unscaledTime-lastPress<.45f) return;
            lastPress=Time.unscaledTime; Debug.Log("METAHACKTHONE_MENU " + name); Pressed?.Invoke();
        }
    }
}

using Unity.Profiling;
using UnityEngine;
namespace PhysicalAssembly
{
    public sealed class AssemblyFeedback : MonoBehaviour
    {
        static readonly ProfilerMarker Marker=new("Assembly.Highlight");
        public WorkshopBootstrap Workshop;
        AssemblyPart selected,target;
        MaterialPropertyBlock properties;
        void Awake() { properties=new MaterialPropertyBlock(); }
        public void Show(AssemblyPart selection,AssemblyPart candidateTarget)
        {
            if(Workshop && Workshop.Downhill) {
                Clear(selected);Clear(target);selected=target=null;
                var outline=GetComponent<SandboxGrabOutline>();if(!outline)outline=gameObject.AddComponent<SandboxGrabOutline>();outline.Workshop=Workshop;return;
            }
            using(Marker.Auto())
            {
                if(selected==selection && target==candidateTarget) return;
                Clear(selected); Clear(target); selected=selection; target=candidateTarget;
                Tint(selected,new Color(.2f,.85f,1)); Tint(target,new Color(.3f,1,.5f));
            }
        }
        static void Clear(AssemblyPart part)
        {
            if(part && part.VisualRenderers!=null) foreach(var renderer in part.VisualRenderers) if(renderer) renderer.SetPropertyBlock(null);
        }
        void Tint(AssemblyPart part,Color color)
        {
            if(!part || part.VisualRenderers==null) return;
            if(Workshop && Workshop.IsChallenge && part==Workshop.Challenge.Core) return;
            properties.Clear(); properties.SetColor("_BaseColor",color);
            foreach(var renderer in part.VisualRenderers) if(renderer) renderer.SetPropertyBlock(properties);
        }
        void OnDisable() { Clear(selected); Clear(target); selected=target=null; }
    }
}

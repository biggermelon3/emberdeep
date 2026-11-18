using UnityEngine;

namespace FireRescue
{
    // Fit after localization swaps text/font, preserving the caller's authored
    // size as the maximum. Both dimensions matter for multi-line CJK text.
    [DefaultExecutionOrder(10010)]
    public sealed class EmberdeepTextBounds : MonoBehaviour
    {
        public float Width=1.72f,Height=.10f;
        void LateUpdate()=>Fit();
        public void Fit()
        {
            var label=GetComponent<TextMesh>();var bounds=GetComponent<Renderer>().localBounds.size;
            float scale=Mathf.Min(1,Width/Mathf.Max(.0001f,bounds.x),Height/Mathf.Max(.0001f,bounds.y));
            if(scale<1)label.characterSize*=scale;
        }
    }
}

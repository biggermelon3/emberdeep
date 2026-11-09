using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PhysicalAssembly
{
    public enum PalmMenuState { Closed, Opening, Open, Closing }
    public sealed class SandboxPalmMenu : MonoBehaviour
    {
        public float HoldSeconds=.35f, OpenDot=.45f, CloseDot=-.2f, HeightBelowHead=.75f;
        public float LowerCloseHeight=.95f, LowerCloseSeconds=.3f, DownCloseSeconds=.2f;
        public float OpeningGrace=.15f, TrackingGrace=.6f;
        public int OpeningFingers=3;
        public float PalmOffset=.12f, FollowSpeed=18f, PanelWidth=.20f;
        public PalmMenuState State { get; private set; }
        public bool IsOpen => State==PalmMenuState.Open || State==PalmMenuState.Closing;
        public bool TrackingReliable { get; private set; }
        public bool CanPress => IsOpen && (desktop || TrackingReliable) && !Blocked;
        public Vector3 PalmPosition { get; private set; }
        public float Progress { get; private set; }
        public event Action<bool> VisibilityChanged;
        DownhillController level;
        Transform panel;
        TextMesh hint;
        bool rearm, desktop, learned;
        float elapsed, interrupted, missing, down, lowered, lastSample=-10;
        Vector3 authoredPosition;
        Quaternion authoredRotation;
        bool Blocked => level && (level.Workshop.World.HasHolds || level.Workshop.World.IsBusy || level.Workshop.RepositionRequested);
        public void Initialize(DownhillController owner)
        {
            level=owner; panel=owner.Presentation.Interface.LeftPanel;
            authoredPosition=panel.localPosition; authoredRotation=panel.localRotation;
            var go=new GameObject("Palm menu instruction"); go.transform.SetParent(owner.Presentation.Interface.Root,false);
            go.transform.localPosition=authoredPosition;
            hint=go.AddComponent<TextMesh>(); hint.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hint.GetComponent<Renderer>().sharedMaterial=hint.font.material;
            hint.fontSize=64; hint.characterSize=.002f; hint.anchor=TextAnchor.MiddleCenter;
            hint.alignment=TextAlignment.Center; hint.color=SandboxInterface.Cyan;
            hint.text="LEFT PALM UP · HOLD 0.35s\nRELAX FINGERS · RIGHT FINGER TO TAP\nTURN PALM DOWN TO CLOSE";
            panel.gameObject.SetActive(false);
        }
        public bool Owns(WorkshopButton button) => panel && button && button.transform.IsChildOf(panel);
        // Action closure intentionally requires leaving the opening pose.
        public void Close() => Hide(true);
        void Hide(bool requireReset)
        {
            bool wasOpen=IsOpen;
            rearm |= requireReset && wasOpen;
            State=PalmMenuState.Closed; elapsed=Progress=interrupted=down=lowered=0;
            if(panel) panel.gameObject.SetActive(false);
            if(wasOpen) VisibilityChanged?.Invoke(false);
        }
        void Show()
        {
            State=PalmMenuState.Open; learned=true; panel.gameObject.SetActive(true);
            if(hint) hint.gameObject.SetActive(false);
            VisibilityChanged?.Invoke(true);
        }
        public void Sample(bool valid, Vector3 palm, Vector3 normal, Vector3 head, float delta, int extendedFingers=4, bool suppressed=false)
        {
            if(!level || !panel || desktop) return;
            lastSample=Time.unscaledTime;
            delta=Mathf.Max(0,delta); TrackingReliable=valid && !suppressed;
            if(valid) PalmPosition=palm;
            if(suppressed || Blocked) { Hide(true); return; }
            if(!valid) {
                down=lowered=0;
                missing+=delta;
                if(IsOpen) { if(missing>TrackingGrace) Hide(false); }
                else InterruptOpening(delta);
                return;
            }
            missing=0;
            float dot=Vector3.Dot(normal,Vector3.up), height=head.y-palm.y;
            bool opening=height<=HeightBelowHead && dot>=OpenDot && extendedFingers>=OpeningFingers;
            if(rearm) { if(!opening) rearm=false; else return; }
            if(IsOpen) {
                down=dot<=CloseDot?down+delta:0;
                lowered=height>LowerCloseHeight?lowered+delta:0;
                State=down>0 || lowered>0?PalmMenuState.Closing:PalmMenuState.Open;
                if(down>=DownCloseSeconds || lowered>=LowerCloseSeconds) { Hide(false); return; }
            } else {
                if(!opening) { InterruptOpening(delta); return; }
                interrupted=0; State=PalmMenuState.Opening;
                elapsed+=delta; Progress=Mathf.Clamp01(elapsed/Mathf.Max(.01f,HoldSeconds));
                if(Progress<1) return;
                Show();
            }
            Follow(palm,head,delta);
        }
        void InterruptOpening(float delta)
        {
            interrupted+=delta;
            if(interrupted>OpeningGrace) Hide(false);
        }
        void Follow(Vector3 palm,Vector3 head,float delta)
        {
            var forward=Vector3.ProjectOnPlane(palm-head,Vector3.up);
            var rotation=forward.sqrMagnitude>.0001f?Quaternion.LookRotation(forward,Vector3.up):panel.rotation;
            float bottom=0, width=.01f;
            foreach(var button in panel.GetComponentsInChildren<WorkshopButton>()) {
                bottom=Mathf.Min(bottom,button.transform.localPosition.y-button.transform.localScale.y*.5f);
                width=Mathf.Max(width,button.transform.localScale.x);
            }
            float scale=PanelWidth/width;
            var parentScale=panel.parent.lossyScale;
            panel.localScale=new Vector3(scale/parentScale.x,scale/parentScale.y,scale/parentScale.z);
            Vector3 target=palm+Vector3.up*(PalmOffset-bottom*scale);
            float t=1-Mathf.Exp(-FollowSpeed*delta);
            panel.SetPositionAndRotation(Vector3.Lerp(panel.position,target,t),Quaternion.Slerp(panel.rotation,rotation,t));
        }
        void Update()
        {
            if(!level) return;
            var input=level.Workshop.GetComponent<DesktopInputAdapter>(); desktop=input && input.IsDesktop;
            if(desktop && Keyboard.current!=null && Keyboard.current.mKey.wasPressedThisFrame) {
                if(IsOpen) Close();
                else if(!Blocked) {
                    panel.localPosition=authoredPosition; panel.localRotation=authoredRotation; panel.localScale=Vector3.one; Show();
                }
            }
            if(Blocked) Hide(true);
            if(!desktop && Time.unscaledTime-lastSample>.05f) {
                TrackingReliable=false;
                if(Time.unscaledTime-lastSample>(IsOpen?TrackingGrace:OpeningGrace)) Hide(false);
            }
            if(hint && !learned && desktop) hint.text="M · OPEN ACTION MENU";
        }
        void OnDisable() { TrackingReliable=false; Hide(false); }
        void OnDestroy() { if(hint) Destroy(hint.gameObject); }
    }
}

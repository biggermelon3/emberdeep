using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace PhysicalAssembly
{
    public enum SandboxRewardState { Dormant, Revealing, Delivering, PalmReminder, ToMenu, AtMenu, ToGoal, Claimed }
    public sealed class SandboxRewardCoordinator : MonoBehaviour
    {
        public float RevealSeconds=.6f, DeliverySeconds=.65f, PreviewTravelSeconds=.45f, ButtonClearance=.035f;
        public SandboxRewardState State { get; private set; }
        public Transform Preview { get; private set; }
        public bool HaloVisible { get; private set; }
        DownhillController level;
        Transform goal, frame;
        SandboxPalmMenu menu;
        FloatingStartButton nextTile;
        SandboxFragments fragments;
        readonly List<(Renderer renderer,Material[] materials)> visuals=new();
        readonly List<Vector3> samples=new();
        Material dissolve;
        Vector3 travelStart, goalOffset, previewExtents;
        Quaternion travelRotation;
        float age;
        bool cancelled;
        public void Initialize(DownhillController owner)
        {
            level=owner; menu=owner.PalmMenu; goal=owner.RewardGoal; frame=owner.Course.SandboxFrame;
            if(menu) menu.VisibilityChanged+=MenuChanged;
            var next=owner.Presentation.Interface.NextLevelButton;
            if(next) nextTile=next.GetComponent<FloatingStartButton>();
        }
        public void Reveal()
        {
            if(cancelled || State!=SandboxRewardState.Dormant || SandboxCampaignSession.DrillClaimed) return;
            var drill=level.Kit.FirstOrDefault(p=>p.Kind==PartKind.DownhillDrill);
            if(!drill || !goal || !menu) return;
            Preview=new GameObject("Drill reward preview (visual only)").transform;
            Preview.SetParent(frame,false);
            // Copy only mesh data and materials; never instantiate the interactive part.
            var origin=drill.MainBody.transform;
            var bounds=new Bounds(Vector3.zero,Vector3.zero);
            dissolve=new Material(Resources.Load<Shader>("SandboxDissolve"));
            foreach(var renderer in drill.VisualRenderers) {
                var filter=renderer.GetComponent<MeshFilter>(); if(!filter || !filter.sharedMesh) continue;
                var child=new GameObject("Reward mesh").transform; child.SetParent(Preview,false);
                child.localPosition=Quaternion.Inverse(origin.rotation)*(renderer.transform.position-origin.position);
                child.localRotation=Quaternion.Inverse(origin.rotation)*renderer.transform.rotation;
                child.localScale=renderer.transform.lossyScale;
                child.gameObject.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
                var visual=child.gameObject.AddComponent<MeshRenderer>(); visual.shadowCastingMode=ShadowCastingMode.Off;
                visuals.Add((visual,renderer.sharedMaterials));
                visual.sharedMaterials=Enumerable.Repeat(dissolve,filter.sharedMesh.subMeshCount).ToArray();
                var mesh=filter.sharedMesh;
                var vertices=mesh.isReadable?mesh.vertices:new[]{mesh.bounds.center};
                for(int i=0;i<vertices.Length;i+=Mathf.Max(1,vertices.Length/64)) {
                    var point=child.localPosition+child.localRotation*Vector3.Scale(vertices[i],child.localScale);
                    samples.Add(point); bounds.Encapsulate(point);
                }
                var b=mesh.bounds;
                for(int i=0;i<8;i++) bounds.Encapsulate(child.localPosition+child.localRotation*Vector3.Scale(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)),child.localScale));
            }
            previewExtents=new Vector3(Mathf.Max(Mathf.Abs(bounds.min.x),Mathf.Abs(bounds.max.x)),Mathf.Max(Mathf.Abs(bounds.min.y),Mathf.Abs(bounds.max.y)),Mathf.Max(Mathf.Abs(bounds.min.z),Mathf.Abs(bounds.max.z)));
            goalOffset=Vector3.up*(previewExtents.y+.045f);
            SetPose(GoalPosition,goal.rotation);
            fragments=new SandboxFragments(transform,96);
            State=SandboxRewardState.Revealing; age=0;
            if(menu.IsOpen) MenuChanged(true);
        }
        Vector3 GoalPosition => goal.position+goal.rotation*goalOffset;
        void SetPose(Vector3 position,Quaternion rotation)
        {
            Preview.SetPositionAndRotation(position,rotation);
            var scale=Preview.parent.lossyScale;
            Preview.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
        }
        void FinishReveal()
        {
            foreach(var v in visuals) if(v.renderer) v.renderer.sharedMaterials=v.materials;
        }
        void MenuChanged(bool open)
        {
            if(!Preview || State==SandboxRewardState.Claimed) return;
            FinishReveal(); fragments.Clear(); HaloVisible=false;
            if(nextTile) nextTile.RewardPulse=open;
            // Capture the current pose on every reversal, including mid-flight.
            travelStart=frame.InverseTransformPoint(Preview.position);
            travelRotation=Quaternion.Inverse(frame.rotation)*Preview.rotation;
            State=open?SandboxRewardState.ToMenu:SandboxRewardState.ToGoal; age=0;
        }
        void LateUpdate() => Tick(Time.unscaledDeltaTime);
        public void Tick(float delta)
        {
            if(!Preview || !goal || cancelled || State==SandboxRewardState.Claimed) return;
            age+=Mathf.Max(0,delta); HaloVisible=false;
            if(State==SandboxRewardState.Revealing) {
                SetPose(GoalPosition,goal.rotation);
                float t=Mathf.Clamp01(age/Mathf.Max(.01f,RevealSeconds)); dissolve.SetFloat("_Dissolve",1-t);
                for(int i=0;i<fragments.Points.Length;i++) {
                    Vector3 end=Preview.TransformPoint(samples.Count>0?samples[i%samples.Count]:Vector3.zero);
                    var start=GoalPosition+goal.rotation*new Vector3(Mathf.Sin(i*2.4f)*.18f,-.07f,Mathf.Cos(i*2.4f)*.18f);
                    fragments.Set(i,Vector3.Lerp(start,end,Mathf.SmoothStep(0,1,t)),.004f,1-t);
                }
                fragments.Draw();
                if(t>=1) { FinishReveal(); State=SandboxRewardState.Delivering; age=0; fragments.Clear(); }
                return;
            }
            if(State==SandboxRewardState.ToMenu || State==SandboxRewardState.AtMenu || State==SandboxRewardState.ToGoal) {
                bool toMenu=State!=SandboxRewardState.ToGoal;
                var button=level.Presentation.Interface.NextLevelButton.transform;
                Vector3 target=toMenu?button.position+button.right*(button.lossyScale.x*.5f+previewExtents.x+ButtonClearance):GoalPosition;
                Quaternion rotation=toMenu?button.rotation:goal.rotation;
                float t=State==SandboxRewardState.AtMenu?1:Mathf.SmoothStep(0,1,age/Mathf.Max(.01f,PreviewTravelSeconds));
                SetPose(Vector3.Lerp(frame.TransformPoint(travelStart),target,t),Quaternion.Slerp(frame.rotation*travelRotation,rotation,t));
                if(t>=1 && State!=SandboxRewardState.AtMenu) { State=toMenu?SandboxRewardState.AtMenu:SandboxRewardState.Delivering; age=0; }
                return;
            }
            SetPose(GoalPosition,goal.rotation);
            if(!menu.TrackingReliable || level.Workshop.RepositionRequested) { fragments.Clear(); age=0; return; }
            if(State==SandboxRewardState.Delivering) {
                float t=Mathf.Clamp01(age/Mathf.Max(.01f,DeliverySeconds));
                for(int i=0;i<fragments.Points.Length;i++) {
                    float u=Mathf.Clamp01(t*1.4f-i/(float)fragments.Points.Length*.4f);
                    fragments.Set(i,Vector3.Lerp(GoalPosition,menu.PalmPosition,u)+Vector3.up*(.08f*Mathf.Sin(u*Mathf.PI)),.003f,Mathf.Sin(u*Mathf.PI));
                }
                fragments.Draw();
                if(t>=1) { State=SandboxRewardState.PalmReminder; age=0; }
            } else if(State==SandboxRewardState.PalmReminder) {
                HaloVisible=true;
                float pulse=.5f+.5f*Mathf.Sin(Time.unscaledTime*2.5f);
                for(int i=0;i<fragments.Points.Length;i++) {
                    float a=i*Mathf.PI*2/fragments.Points.Length;
                    fragments.Set(i,menu.PalmPosition+new Vector3(Mathf.Cos(a),.15f,Mathf.Sin(a))*(.045f+.004f*pulse),.0025f,.35f+.3f*pulse);
                }
                fragments.Draw();
            }
        }
        public void Claim() { if(State==SandboxRewardState.Claimed) return; Cancel(); State=SandboxRewardState.Claimed; }
        public void Cancel()
        {
            cancelled=true; HaloVisible=false;
            if(nextTile) nextTile.RewardPulse=false;
            fragments?.Release(); fragments=null;
            if(Preview) { Preview.gameObject.SetActive(false); Destroy(Preview.gameObject); Preview=null; }
            if(dissolve) Destroy(dissolve);
            visuals.Clear(); samples.Clear();
        }
        void OnDisable() { Cancel(); }
        void OnDestroy() { if(menu) menu.VisibilityChanged-=MenuChanged; Cancel(); }
    }
}

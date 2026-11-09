using System;
using System.Collections.Generic;
using System.Linq;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using UnityEngine;

namespace PhysicalAssembly.MetaXR
{
    // Floor targeting independent of a workshop or any gameplay scene.
    public sealed class MetaFloorPlacement : MonoBehaviour
    {
        public bool IsPlacing { get; private set; }
        public RayInteractable Target { get; private set; }
        public bool HasPreview => outline && outline.enabled;
        MetaPlayerRig player;
        Func<bool> available;
        Action<Pose> confirmed;
        GameObject surface;
        LineRenderer outline;
        Material material;
        bool released;
        int openedFrame;
        readonly HashSet<int> pointers=new();
        public void Initialize(MetaPlayerRig rig,Func<bool> canPlace,Action<Pose> onConfirmed)
        {
            player=rig; available=canPlace; confirmed=onConfirmed;
            surface=new GameObject("Menu floor placement target"); surface.transform.SetParent(transform,false);
            surface.transform.rotation=Quaternion.Euler(90,0,0);
            var plane=surface.AddComponent<PlaneSurface>(); plane.InjectAllPlaneSurface(PlaneSurface.NormalFacing.Backward,false);
            var bounds=surface.AddComponent<BoundsClipper>(); bounds.Size=new Vector3(10,10,.1f);
            var clipped=surface.AddComponent<ClippedPlaneSurface>(); clipped.InjectAllClippedPlaneSurface(plane,new IBoundsClipper[]{bounds});
            Target=surface.AddComponent<RayInteractable>(); Target.InjectAllRayInteractable(clipped); Target.WhenPointerEventRaised+=Handle;
            outline=new GameObject("Play area placement marker").AddComponent<LineRenderer>(); outline.transform.SetParent(transform,false);
            material=PartFactory.Material(new Color(.25f,1,.78f)); outline.sharedMaterial=material;
            outline.startWidth=outline.endWidth=.012f; outline.positionCount=7; outline.enabled=false; surface.SetActive(false);
        }
        public void Begin()
        {
            IsPlacing=true; released=false; openedFrame=Time.frameCount; pointers.Clear();
            var head=player.ViewCamera.transform.position;
            surface.transform.position=new Vector3(head.x,player.FloorHeight+.002f,head.z); surface.SetActive(true);
        }
        public void Cancel() { IsPlacing=false; pointers.Clear(); if(surface) surface.SetActive(false); if(outline) outline.enabled=false; }
        void Update()
        {
            if(!IsPlacing || Time.frameCount<=openedFrame+1 || !player.IsXR) return;
            var selectors=player.XRRig.GetComponentsInChildren<MetaPinchSelector>(true);
            if(selectors.Length>0 && selectors.All(s=>!s.IsPinching)) released=true;
        }
        Quaternion Heading()
        {
            var forward=Vector3.ProjectOnPlane(player.ViewCamera.transform.forward,Vector3.up);
            return Quaternion.LookRotation(forward.sqrMagnitude>.01f?forward:Vector3.forward);
        }
        void Preview(Vector3 point)
        {
            point.y=player.FloorHeight+.008f; var rotation=Heading();
            var corners=new[]{new Vector3(-.35f,0,-.25f),new Vector3(-.35f,0,.25f),new Vector3(.35f,0,.25f),new Vector3(.35f,0,-.25f),new Vector3(-.35f,0,-.25f),Vector3.zero,new Vector3(0,0,.4f)};
            for(int i=0;i<corners.Length;i++) outline.SetPosition(i,point+rotation*corners[i]);
            outline.enabled=true;
        }
        void Confirm(Vector3 point)
        {
            if(!IsPlacing || !released || Time.frameCount<=openedFrame+1 || available?.Invoke()==false) return;
            point.y=player.FloorHeight; var pose=new Pose(point,Heading()); Cancel(); confirmed?.Invoke(pose);
        }
        void Handle(PointerEvent evt)
        {
            if(!IsPlacing) return;
            if(evt.Type==PointerEventType.Unhover || evt.Type==PointerEventType.Cancel)
            { pointers.Remove(evt.Identifier); outline.enabled=pointers.Count>0; return; }
            if(evt.Type==PointerEventType.Unselect) { released=true; return; }
            if(evt.Type!=PointerEventType.Hover && evt.Type!=PointerEventType.Move && evt.Type!=PointerEventType.Select) return;
            pointers.Add(evt.Identifier); Preview(evt.Pose.position);
            if(evt.Type==PointerEventType.Select) Confirm(evt.Pose.position);
        }
        public void SampleDesktop(Vector3 point,bool pressed,bool held)
        {
            if(!IsPlacing) return;
            if(!held && Time.frameCount>openedFrame+1) released=true;
            Preview(point); if(pressed) Confirm(point);
        }
        void OnDestroy() { if(Target) Target.WhenPointerEventRaised-=Handle; if(material) Destroy(material); }
    }
}

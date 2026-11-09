using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using UnityEngine;

namespace PhysicalAssembly.MetaXR
{
    public sealed class MetaArenaPlacement : MonoBehaviour
    {
        public RayInteractable Target { get; private set; }
        public bool HasPreview => outline && outline.enabled;
        WorkshopBootstrap workshop;
        MetaHandInputAdapter input;
        Transform head;
        GameObject surfaceObject;
        LineRenderer outline;
        readonly HashSet<int> pointers=new();
        static readonly Vector3[] Corners={new(-1.4f,0,-1.2f),new(-1.4f,0,1.2f),new(1.4f,0,1.2f),new(1.4f,0,-1.2f),new(-1.4f,0,-1.2f)};
        public void Initialize(WorkshopBootstrap owner,Transform headAnchor)
        {
            workshop=owner; head=headAnchor; input=owner.GetComponent<MetaHandInputAdapter>();
            surfaceObject=new GameObject("Floor placement target");
            surfaceObject.transform.SetParent(transform,false);
            surfaceObject.transform.rotation=Quaternion.Euler(90,0,0);
            var plane=surfaceObject.AddComponent<PlaneSurface>(); plane.InjectAllPlaneSurface(PlaneSurface.NormalFacing.Backward,false);
            var bounds=surfaceObject.AddComponent<BoundsClipper>(); bounds.Size=new Vector3(10,10,.1f);
            var clipped=surfaceObject.AddComponent<ClippedPlaneSurface>(); clipped.InjectAllClippedPlaneSurface(plane,new IBoundsClipper[]{bounds});
            Target=surfaceObject.AddComponent<RayInteractable>(); Target.InjectAllRayInteractable(clipped);
            Target.WhenPointerEventRaised+=HandlePointer;
            outline=new GameObject("Arena placement outline").AddComponent<LineRenderer>(); outline.transform.SetParent(transform,false);
            outline.sharedMaterial=Resources.Load<Material>("AssemblyPreview");
            outline.startWidth=outline.endWidth=.012f; outline.positionCount=5; outline.enabled=false;
            surfaceObject.SetActive(false);
        }
        void Update()
        {
            if(!workshop) return;
            bool active=workshop.RepositionRequested;
            if(active && !surfaceObject.activeSelf)
                surfaceObject.transform.position=new Vector3(head.position.x,workshop.FloorHeight+.002f,head.position.z);
            if(surfaceObject.activeSelf!=active) surfaceObject.SetActive(active);
            if(!active) { pointers.Clear(); outline.enabled=false; }
        }
        Quaternion Heading()
        {
            var forward=Vector3.ProjectOnPlane(head.forward,Vector3.up);
            return Quaternion.LookRotation(forward.sqrMagnitude>.01f?forward:Vector3.forward);
        }
        void HandlePointer(PointerEvent evt)
        {
            if(!workshop.RepositionRequested) return;
            if(evt.Type==PointerEventType.Unhover || evt.Type==PointerEventType.Cancel)
            { pointers.Remove(evt.Identifier); outline.enabled=pointers.Count>0; return; }
            if(evt.Type!=PointerEventType.Hover && evt.Type!=PointerEventType.Move && evt.Type!=PointerEventType.Select) return;
            pointers.Add(evt.Identifier);
            var point=evt.Pose.position; point.y=workshop.PlacementSurfaceHeight+.008f;
            var rotation=Heading();
            for(int i=0;i<Corners.Length;i++) outline.SetPosition(i,point+rotation*Corners[i]);
            outline.enabled=true;
            if(evt.Type==PointerEventType.Select && !workshop.World.HasHolds && !workshop.World.IsBusy && (!input || !input.BlocksMenuInput()))
            {
                workshop.PlaceArena(point,rotation);
                Debug.Log($"METAHACKTHONE_PLACED {workshop.Arena.position}");
            }
        }
        void OnDestroy() { if(Target) Target.WhenPointerEventRaised-=HandlePointer; }
    }
}

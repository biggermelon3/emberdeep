using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using UnityEngine;

namespace PhysicalAssembly.MetaXR
{
    // Register the existing workshop buttons with the comprehensive rig's native
    // ray/pinch and near-touch interactors, including its pointer visuals.
    public sealed class MetaMenuButton : MonoBehaviour
    {
        public RayInteractable Ray { get; private set; }
        public PokeInteractable Poke { get; private set; }
        readonly HashSet<int> hovering=new();
        readonly HashSet<int> selecting=new();
        WorkshopButton button;
        WorkshopBootstrap workshop;
        MetaHandInputAdapter input;
        System.Action press;
        System.Func<bool> canPress;
        System.Action<bool,bool> feedback;

        public void Initialize(WorkshopBootstrap owner)
        {
            workshop=owner; button=GetComponent<WorkshopButton>(); input=owner.GetComponent<MetaHandInputAdapter>();
            bool rotate=button.Command=="Sandbox Rotate Left" || button.Command=="Sandbox Rotate Right";
            Initialize(button.Press,()=>!workshop.World.IsBusy && (rotate || !workshop.World.HasHolds && (!input || !input.BlocksMenuInput())) && (!workshop.RepositionRequested || name=="Move arena"),
                button.SetInteractionState,new Vector3(0,0,-.51f),Quaternion.identity);
        }

        // Reuse the comprehensive rig's ray/poke registration for other workshop games.
        public void Initialize(System.Action onPress,System.Func<bool> available,System.Action<bool,bool> onFeedback,
            Vector3 localSurfacePosition,Quaternion localSurfaceRotation)
        {
            press=onPress; canPress=available; feedback=onFeedback;
            var surfaceObject=new GameObject("Meta menu interaction surface");
            surfaceObject.transform.SetParent(transform,false);
            // Buttons are unit cubes scaled to their physical dimensions.
            surfaceObject.transform.localPosition=localSurfacePosition;
            surfaceObject.transform.localRotation=localSurfaceRotation;
            var plane=surfaceObject.AddComponent<PlaneSurface>();
            plane.InjectAllPlaneSurface(PlaneSurface.NormalFacing.Backward,false);
            var bounds=surfaceObject.AddComponent<BoundsClipper>(); bounds.Size=new Vector3(1,1,.1f);
            var surface=surfaceObject.AddComponent<ClippedPlaneSurface>();
            surface.InjectAllClippedPlaneSurface(plane,new IBoundsClipper[]{bounds});
            Ray=surfaceObject.AddComponent<RayInteractable>(); Ray.InjectAllRayInteractable(surface);
            Poke=surfaceObject.AddComponent<PokeInteractable>(); Poke.InjectAllPokeInteractable(surface);
            Ray.WhenPointerEventRaised+=HandlePointer;
            Poke.WhenPointerEventRaised+=HandlePointer;
        }

        void HandlePointer(PointerEvent evt)
        {
            var palm=workshop && workshop.Downhill?workshop.Downhill.PalmMenu:null;
            if(palm && palm.Owns(button) && (!palm.CanPress || !input || !input.IsRightPoke(evt.Identifier))) return;
            switch(evt.Type)
            {
                case PointerEventType.Hover: hovering.Add(evt.Identifier); break;
                case PointerEventType.Unhover: hovering.Remove(evt.Identifier); break;
                case PointerEventType.Select:
                    if(selecting.Add(evt.Identifier) && (!input || !input.PointerOwnsHold(evt.Identifier)) && (canPress==null || canPress())) press?.Invoke();
                    break;
                case PointerEventType.Unselect: selecting.Remove(evt.Identifier); break;
                case PointerEventType.Cancel:
                    hovering.Remove(evt.Identifier); selecting.Remove(evt.Identifier); break;
            }
            feedback?.Invoke(hovering.Count>0,selecting.Count>0);
        }
        void OnDisable()
        {
            hovering.Clear(); selecting.Clear();
            feedback?.Invoke(false,false);
        }
        void OnDestroy()
        {
            if(Ray) Ray.WhenPointerEventRaised-=HandlePointer;
            if(Poke) Poke.WhenPointerEventRaised-=HandlePointer;
        }
    }
}

using System;
using System.Collections.Generic;
using CloudGarden.Integration;
using PhysicalAssembly;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FireRescue
{
    // One Emberdeep UI adapter for camp, modals, HUD and shop. It proposes one
    // accepted button, leaving the scene owner to cancel gameplay before invoking it.
    public sealed class EmberdeepHandRayMenu : MonoBehaviour
    {
        readonly EmberdeepHandRay[] rays={new(),new()};
        readonly EmberdeepGazePinch[] pinches={new(),new()};
        readonly LineRenderer[] lines=new LineRenderer[2];
        readonly Transform[] cursors=new Transform[2];
        readonly WorkshopButton[] targets=new WorkshopButton[2];
        readonly bool[] visible=new bool[2];
        public Camera ViewCamera { get; set; }
        // Fireground requires attention near the panel; camp keeps its own policy.
        public float MaximumHeadAngle { get; set; }=180f;
        public bool RayVisible(int hand)=>hand>=0 && hand<2 && visible[hand];
        Material material;
        IReadOnlyList<WorkshopButton> currentButtons;
        public WorkshopButton HoveredButton { get; private set; }
        public WorkshopButton PressedButton { get; private set; }
        public int PointerHand { get; private set; }=1;
        public bool OwnsInput=>targets[0] || targets[1] || PressedButton;
        public bool HasPointer { get; private set; }
        public bool HasTarget(int hand)=>hand>=0 && hand<2 && targets[hand];
        public bool HasIntent(int hand,EmberdeepHandRay.Sample sample,IReadOnlyList<WorkshopButton> buttons,Predicate<WorkshopButton> allowed=null)
            =>hand>=0 && hand<2 && EmberdeepHandRay.Valid(sample) && FindTarget(sample,buttons,targets[hand],allowed,out _);
        public bool IsHovered(WorkshopButton button)=>button && (targets[0]==button || targets[1]==button);

        public void Initialize(Transform parent,Material fallback)
        {
            if(material)return;
            var source=Resources.Load<Material>("EmberdeepMenuUnlit");
            material=new Material(source?source:fallback);material.name="Emberdeep hand ray cyan";
            material.SetColor("_BaseColor",new Color(.275f,.831f,.898f));
            for(int hand=0;hand<2;hand++)
            {
                var go=new GameObject(hand==0?"Left contextual hand ray":"Right contextual hand ray");go.transform.SetParent(parent,false);
                lines[hand]=go.AddComponent<LineRenderer>();lines[hand].sharedMaterial=material;
                lines[hand].useWorldSpace=true;lines[hand].positionCount=2;
                lines[hand].startWidth=.0012f;lines[hand].endWidth=.0018f;
                lines[hand].numCapVertices=2;lines[hand].shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                lines[hand].receiveShadows=false;lines[hand].enabled=false;
                var cursor=GameObject.CreatePrimitive(PrimitiveType.Sphere);cursor.name=hand==0?"Left index UI cursor":"Right index UI cursor";
                cursor.transform.SetParent(go.transform,false);cursor.transform.localScale=Vector3.one*.009f;
                var collider=cursor.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
                cursor.GetComponent<Renderer>().sharedMaterial=material;cursor.SetActive(false);cursors[hand]=cursor.transform;
            }
        }
        public bool Process(MetaShadowPlayer provider,Camera camera,bool xr,IReadOnlyList<WorkshopButton> buttons,
            bool available,float dt,Predicate<WorkshopButton> allowed=null,Func<int,bool> handAllowed=null,
            Func<int,EmberdeepHandRay.Sample,bool> sampleAllowed=null)
        {
            ViewCamera=camera;
            EmberdeepHandRay.Sample left=default,right=default;
            if(available && xr)
            {
                if(handAllowed==null || handAllowed(0))left=sampleAllowed==null?rays[0].Read(provider,true):ReadIntent(provider,true);else rays[0].Reset();
                if(handAllowed==null || handAllowed(1))right=sampleAllowed==null?rays[1].Read(provider,false):ReadIntent(provider,false);else rays[1].Reset();
                if(sampleAllowed!=null)
                {
                    if(!sampleAllowed(0,left))left=default;
                    if(!sampleAllowed(1,right))right=default;
                }
            }
            else if(available && camera && Mouse.current!=null)
                right=new EmberdeepHandRay.Sample(true,camera.ScreenPointToRay(Mouse.current.position.ReadValue()),Mouse.current.leftButton.isPressed?.01f:.04f);
            return ProcessSamples(left,right,buttons,available,dt,allowed);
        }
        // Game-only arbitration needs valid pointing even on the first closed
        // sample. Reserving a target does not click it: the existing pinch filter
        // below still requires an observed open hand and a fresh edge. Camp keeps
        // using EmberdeepHandRay.Read and its unchanged acquisition policy.
        static EmberdeepHandRay.Sample ReadIntent(MetaShadowPlayer provider,bool left)
        {
            if(!provider || !provider.ReadHand(left,out var palm,out _,out var pinch)
                || !provider.ReadInteractionAim(left,out var pointer))return default;
            var q=pointer.rotation;float norm=q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w;
            if(!(norm>=.5f && norm<=1.5f))return default;
            var sample=new EmberdeepHandRay.Sample(true,new Ray(pointer.position,q*Vector3.forward),pinch){VisualOrigin=palm.position};
            return EmberdeepHandRay.Valid(sample)?sample:default;
        }
        public bool ProcessSamples(EmberdeepHandRay.Sample left,EmberdeepHandRay.Sample right,
            IReadOnlyList<WorkshopButton> buttons,bool available,float dt,Predicate<WorkshopButton> allowed=null)
        {
            currentButtons=buttons;PressedButton=null;
            if(!available || float.IsNaN(dt) || float.IsInfinity(dt) || dt<=0){ResetInput();return false;}
            HasPointer=false;
            int pressedHand=-1;
            for(int hand=0;hand<2;hand++)
            {
                var sample=hand==0?left:right;bool valid=EmberdeepHandRay.Valid(sample);
                var previous=targets[hand];targets[hand]=null;Vector3 end=default;
                if(valid)
                {
                    HasPointer=true;
                    targets[hand]=FindTarget(sample,buttons,previous,allowed,out end);
                }
                else rays[hand].Reset();
                visible[hand]=valid && targets[hand];
                if(lines[hand])
                {
                    // No idle laser in the forest or while directly manipulating a source.
                    lines[hand].enabled=visible[hand];
                    if(visible[hand]){lines[hand].SetPosition(0,sample.VisualOrigin);lines[hand].SetPosition(1,end);}
                    cursors[hand].gameObject.SetActive(visible[hand]);cursors[hand].position=end;
                    cursors[hand].localScale=Vector3.one*.009f;
                }
                bool pressed=pinches[hand].Sample(targets[hand]?targets[hand].GetInstanceID():0,valid,
                    valid,sample.Pinch,false,float.PositiveInfinity,dt);
                if(pressed && (pressedHand<0 || hand==PointerHand))pressedHand=hand;
            }
            if(pressedHand>=0)PointerHand=pressedHand;
            else if(!targets[PointerHand] && targets[1-PointerHand])PointerHand=1-PointerHand;
            HoveredButton=targets[PointerHand];
            foreach(var button in buttons)if(button)button.SetInteractionState(IsHovered(button),false);
            if(pressedHand<0)return false;
            PressedButton=targets[pressedHand];HoveredButton=PressedButton;
            foreach(var pinch in pinches)pinch.Reset(); // Both hands must reopen after page or action ownership changes.
            PressedButton.SetInteractionState(true,true);
            return true;
        }
        bool Eligible(WorkshopButton b,Predicate<WorkshopButton> allowed)
        {
            if(!b || !b.gameObject.activeInHierarchy || !b.Interactable || (allowed!=null && !allowed(b)))return false;
            if(!ViewCamera)return true;
            if(Vector3.Angle(ViewCamera.transform.forward,b.transform.position-ViewCamera.transform.position)>MaximumHeadAngle)return false;
            var p=ViewCamera.WorldToViewportPoint(b.transform.position);
            return p.z>0 && p.x>=-.05f && p.x<=1.05f && p.y>=-.05f && p.y<=1.05f;
        }
        WorkshopButton FindTarget(EmberdeepHandRay.Sample sample,IReadOnlyList<WorkshopButton> buttons,
            WorkshopButton previous,Predicate<WorkshopButton> allowed,out Vector3 end)
        {
            end=default;
            // A small selection envelope keeps a closing pinch on its settled
            // button; it never acquires a new target outside the normal hit area.
            if(sample.Pinch<.035f && Eligible(previous,allowed) &&
                ExpandedHit(previous.GetComponent<Collider>(),sample.Ray,.022f,out end,out _))return previous;
            WorkshopButton best=null;float nearest=3;
            foreach(var b in buttons)
            {
                if(!Eligible(b,allowed))continue;
                var collider=b.GetComponent<Collider>();
                if(collider && collider.enabled && collider.Raycast(sample.Ray,out var hit,nearest))
                {nearest=hit.distance;end=hit.point-sample.Ray.direction*.008f;best=b;}
            }
            if(best)return best;
            float closest=.012f;
            foreach(var b in buttons)
            {
                if(!Eligible(b,allowed))continue;
                if(ExpandedHit(b.GetComponent<Collider>(),sample.Ray,.012f,out var point,out float gap) && gap<closest)
                {closest=gap;end=point;best=b;}
            }
            return best;
        }
        static bool ExpandedHit(Collider collider,Ray ray,float margin,out Vector3 hit,out float gap)
        {
            hit=default;gap=float.PositiveInfinity;
            if(!collider || !collider.enabled || collider is not BoxCollider box)return false;
            var tr=box.transform;var origin=tr.InverseTransformPoint(ray.origin);var direction=tr.InverseTransformVector(ray.direction);
            if(Mathf.Abs(direction.z)<.00001f)return false;
            float plane=box.center.z-Mathf.Sign(direction.z)*box.size.z*.5f;
            float t=(plane-origin.z)/direction.z;if(t<=0 || t>3)return false;
            var local=origin+direction*t;var d=local-box.center;var scale=tr.lossyScale;
            if(Mathf.Abs(d.x)>box.size.x*.5f+margin/Mathf.Abs(scale.x) || Mathf.Abs(d.y)>box.size.y*.5f+margin/Mathf.Abs(scale.y))return false;
            var raw=ray.GetPoint(t);gap=Vector3.Distance(raw,box.ClosestPoint(raw));
            hit=box.ClosestPoint(raw)-ray.direction*.008f;return true;
        }
        public void ResetInput()
        {
            HoveredButton=PressedButton=null;HasPointer=false;
            for(int i=0;i<2;i++)
            {
                rays[i].Reset();pinches[i].Reset();targets[i]=null;visible[i]=false;
                if(lines[i])lines[i].enabled=false;if(cursors[i])cursors[i].gameObject.SetActive(false);
            }
            if(currentButtons!=null)foreach(var button in currentButtons)if(button)button.SetInteractionState(false,false);
        }
        void OnDisable()=>ResetInput();
        void OnDestroy(){if(material)Destroy(material);}
    }
}

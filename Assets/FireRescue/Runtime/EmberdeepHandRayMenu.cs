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
    // A button is acquired only by a real hit, and the cursor always sits on the true ray, so the hysteresis below
    // never shows as a snap or a cursor jump.
    // A hand's ray is drawn only while it is on a button or passes near one (a panel), so it never hangs beside the
    // camp's palm-water aim (headset test 2, 2026-11-22). Clicks: EmberdeepPinchClick (closing edge, ordinary release).
    // Headset test 3 (2026-11-22): water first. A hand in the open-palm aim pose (PalmAiming) gets no near-panel ray, only a
    // ray on a button it actually hits, and the owner can shrink the near-panel reach (ShowReach / HideReach: the camp
    // tutorial). Sliders (EmberdeepSlider, AddSlider): a pinch click on a slider's track grabs it instead of pressing it,
    // and the drag runs here, in the same frame as the samples, until the pinch's release.
    public sealed class EmberdeepHandRayMenu : MonoBehaviour
    {
        // Hover hysteresis: the hovered button stays hovered until the ray is this far past its edge.
        public const float HoverRetention=.005f;
        // While a pinch is closing (span under PinchClosingSpan) the hovered button keeps the click until the ray is
        // this far past its edge, even over a neighbour, so the pinch's residual dip cannot cancel the click.
        // PICO's fallback joint ray also holds its direction near the click (PortableJointAim.HoldFrom); the system
        // aim rays are the runtime's own and get no hold.
        public const float PinchRetention=.012f,PinchClosingSpan=.035f;
        // A visible ray with no button ends on the button plane it crosses within this reach of a button (so the
        // cursor glides across the panel between buttons), else this far out.
        public const float PanelReach=.15f,IdleRayLength=1.1f;
        // A ray with no button shows once it passes within PanelShowReach of an eligible button's face, and stays until
        // it is PanelHideReach away (edge jitter cannot flicker it). The line grows in and shrinks out over RayFadeSeconds.
        public const float PanelShowReach=.08f,PanelHideReach=.12f,RayFadeSeconds=.12f;
        // The near-panel reach in use (defaults above; the camp tutorial shrinks them).
        public float ShowReach {get;set;}=PanelShowReach;
        public float HideReach {get;set;}=PanelHideReach;
        // hand -> that hand is in the open-palm aim pose (the camp's palm water). It then shows a ray only on a real button
        // hit: no near-panel ray, so aiming water past the sign never raises the ray and blocks the water.
        public Func<int,bool> PalmAiming {get;set;}
        const float LineStart=.0012f,LineEnd=.0018f,CursorSize=.009f;
        // Development device builds log one EMBERDEEP_AIM line per pinch edge and per ray show/hide.
        public static bool DeviceLog=>Debug.isDebugBuild && !Application.isEditor;
        readonly EmberdeepHandRay[] rays={new(),new()};
        readonly EmberdeepPinchClick[] clicks={new(),new()};
        readonly float[] fades=new float[2];
        readonly bool[] drawn=new bool[2];
        readonly LineRenderer[] lines=new LineRenderer[2];
        readonly Transform[] cursors=new Transform[2];
        readonly WorkshopButton[] targets=new WorkshopButton[2];
        readonly bool[] visible=new bool[2];
        readonly Vector3[] ends=new Vector3[2];
        readonly List<EmberdeepSlider> sliders=new();
        readonly EmberdeepSliderDrag drag=new();
        EmberdeepSlider dragSlider;
        // Where the hand's ray and cursor end this frame (valid while RayVisible).
        public Vector3 RayEnd(int hand)=>hand>=0 && hand<2?ends[hand]:default;
        public Camera ViewCamera { get; set; }
        // Fireground requires attention near the panel; camp keeps its own policy.
        public float MaximumHeadAngle { get; set; }=180f;
        // True (camp): a ray with no button also shows while it passes near the sign's buttons. False (fireground): only
        // on a button, so no ray hangs beside a HUD while the hands work the board.
        public bool ShowUnfocusedRays { get; set; }
        // The hand's ray is up this frame: on a button, or near a panel (immediate; the line itself fades).
        public bool RayVisible(int hand)=>hand>=0 && hand<2 && visible[hand];
        // The hand's line is on screen, fading in, up, or fading out. Callers that must not draw a second line from the
        // same hand (the camp's palm-water aim) wait for this.
        public bool RayShown(int hand)=>hand>=0 && hand<2 && (visible[hand] || drawn[hand]);
        public EmberdeepPinchClick Click(int hand)=>hand>=0 && hand<2?clicks[hand]:null;
        Material material;
        IReadOnlyList<WorkshopButton> currentButtons;
        public WorkshopButton HoveredButton { get; private set; }
        public WorkshopButton PressedButton { get; private set; }
        public int PointerHand { get; private set; }=1;
        public bool OwnsInput=>targets[0] || targets[1] || PressedButton || drag.Active;
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
                lines[hand].startWidth=LineStart;lines[hand].endWidth=LineEnd;
                lines[hand].numCapVertices=2;lines[hand].shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                lines[hand].receiveShadows=false;lines[hand].enabled=false;
                var cursor=GameObject.CreatePrimitive(PrimitiveType.Sphere);cursor.name=hand==0?"Left index UI cursor":"Right index UI cursor";
                cursor.transform.SetParent(go.transform,false);cursor.transform.localScale=Vector3.one*CursorSize;
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
                if(handAllowed==null || handAllowed(0))left=rays[0].Read(provider,true);else rays[0].Reset();
                if(handAllowed==null || handAllowed(1))right=rays[1].Read(provider,false);else rays[1].Reset();
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
                bool wasUp=visible[hand],near=false;
                if(valid)
                {
                    HasPointer=true;
                    // A ray drag holds its hand on the slider: nothing else is hovered and the cursor rides the track's plane.
                    if(RayDragging(hand))
                    {
                        targets[hand]=dragSlider.Track;
                        end=dragSlider.PointOnTrack(sample.Ray,out var onTrack)?onTrack-sample.Ray.direction*.008f:sample.Ray.GetPoint(IdleRayLength);
                    }
                    else targets[hand]=FindTarget(sample,buttons,previous,allowed,out end);
                    // No idle laser in the forest, beside the palm-water aim or while directly manipulating a source.
                    if(!targets[hand] && ShowUnfocusedRays && !(PalmAiming!=null && PalmAiming(hand)))
                        near=NearPanel(sample.Ray,buttons,allowed,wasUp?HideReach:ShowReach);
                }
                else rays[hand].Reset();
                visible[hand]=valid && (targets[hand] || near);
                // A lost hand's line goes at once; a hand that leaves the panel shrinks its line out.
                fades[hand]=valid?Mathf.MoveTowards(fades[hand],visible[hand]?1:0,dt/RayFadeSeconds):0;
                drawn[hand]=fades[hand]>0;
                if(valid && !targets[hand])end=visible[hand] || drawn[hand]?IdleEnd(sample.Ray,buttons,allowed):sample.Ray.GetPoint(IdleRayLength);
                ends[hand]=end;
                if(lines[hand])
                {
                    float fade=fades[hand];
                    lines[hand].enabled=drawn[hand];
                    if(drawn[hand])
                    {
                        lines[hand].SetPosition(0,sample.VisualOrigin);lines[hand].SetPosition(1,end);
                        lines[hand].startWidth=LineStart*fade;lines[hand].endWidth=LineEnd*fade;
                    }
                    cursors[hand].gameObject.SetActive(drawn[hand]);cursors[hand].position=end;
                    cursors[hand].localScale=Vector3.one*(CursorSize*fade);
                }
                if(visible[hand]!=wasUp && DeviceLog)
                    Debug.Log("EMBERDEEP_AIM ray="+(visible[hand]?"up":"down")+" hand="+(hand==0?'L':'R')
                        +" why="+(!valid?"no_hand":targets[hand]?"button":near?"panel":"away"));
                var click=clicks[hand];
                bool pressed=click.Sample(targets[hand]?targets[hand].GetInstanceID():0,valid?EmberdeepPinchClick.Of(sample):default,dt);
                if(click.Last!=EmberdeepPinchClick.Event.None && DeviceLog)Debug.Log(DescribeClick(hand,sample,click,targets[hand]));
                // A click on a slider's track grabs the slider instead of pressing it (one drag at a time: while the other
                // hand holds one, the click is spent).
                if(pressed && SliderFor(targets[hand]) is {} grabbed){if(!drag.Active)BeginRayDrag(hand,grabbed,sample);pressed=false;}
                if(RayDragging(hand))TickRayDrag(hand,sample,valid,click,dt);
                if(pressed && (pressedHand<0 || hand==PointerHand))pressedHand=hand;
            }
            if(pressedHand>=0)PointerHand=pressedHand;
            else if(drag.Active && drag.By==EmberdeepSliderDrag.Source.Ray)PointerHand=drag.Hand;
            else if(!targets[PointerHand] && targets[1-PointerHand])PointerHand=1-PointerHand;
            HoveredButton=targets[PointerHand];
            foreach(var button in buttons)if(button)button.SetInteractionState(IsHovered(button),false);
            if(pressedHand<0)return false;
            PressedButton=targets[pressedHand];HoveredButton=PressedButton;
            // The page may change under the other hand: its next click needs a fresh dwell on whatever it points at.
            // The pressing hand is closed and needs a release anyway (EmberdeepPinchClick).
            clicks[1-pressedHand].RestartDwell();
            PressedButton.SetInteractionState(true,true);
            return true;
        }
        // One device-log line per pinch edge: what it did and the select/tip values behind it.
        static string DescribeClick(int hand,EmberdeepHandRay.Sample sample,EmberdeepPinchClick click,WorkshopButton target)
        {
            string what=click.Last switch
            {
                EmberdeepPinchClick.Event.Press=>"press",
                EmberdeepPinchClick.Event.Rearm=>"rearm",
                EmberdeepPinchClick.Event.Unarmed=>"ignored why=not_released",
                EmberdeepPinchClick.Event.NoTarget=>"ignored why=no_button",
                _=>"ignored why=dwell",
            };
            var text=new System.Text.StringBuilder("EMBERDEEP_AIM pinch=").Append(what).Append(" hand=").Append(hand==0?'L':'R')
                .Append(" button=").Append(target?target.Command:"none");
            if(sample.SystemSelect)
                text.Append(" select=").Append(F(sample.Select,2)).Append(" runtime=").Append(sample.SelectPressed?1:0)
                    .Append(" tip_mm=").Append(float.IsFinite(sample.TipSpan)?F(sample.TipSpan*1000,0):"none");
            else text.Append(" span_mm=").Append(F(sample.Pinch*1000,0));
            return text.Append(" dwell_ms=").Append(F(click.Dwell*1000,0)).ToString();
        }
        static string F(float value,int digits)=>value.ToString("F"+digits,System.Globalization.CultureInfo.InvariantCulture);
        // The ray passes within reach of an eligible button's face (a panel the hand is aiming at).
        bool NearPanel(Ray ray,IReadOnlyList<WorkshopButton> buttons,Predicate<WorkshopButton> allowed,float reach)
        {
            if(buttons==null)return false;
            foreach(var b in buttons)
                if(Eligible(b,allowed) && ExpandedHit(b.GetComponent<Collider>(),ray,reach,out _,out _))return true;
            return false;
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
            bool closing=!sample.PinchUnavailable && sample.Pinch<PinchClosingSpan;
            bool settled=Eligible(previous,allowed);
            // A closing pinch belongs to the settled button, even if the dip crosses into a neighbour.
            if(settled && closing && ExpandedHit(previous.GetComponent<Collider>(),sample.Ray,PinchRetention,out end,out _))return previous;
            WorkshopButton best=null;float nearest=3;
            foreach(var b in buttons)
            {
                if(!Eligible(b,allowed))continue;
                var collider=b.GetComponent<Collider>();
                if(collider && collider.enabled && collider.Raycast(sample.Ray,out var hit,nearest))
                {nearest=hit.distance;end=hit.point-sample.Ray.direction*.008f;best=b;}
            }
            if(best)return best;
            // Hover hysteresis: only the settled button, only a few millimetres, so edge jitter cannot keep
            // restarting the click's dwell. New buttons need a real hit.
            if(settled && ExpandedHit(previous.GetComponent<Collider>(),sample.Ray,HoverRetention,out end,out _))return previous;
            return null;
        }
        Vector3 IdleEnd(Ray ray,IReadOnlyList<WorkshopButton> buttons,Predicate<WorkshopButton> allowed)
        {
            var end=ray.GetPoint(IdleRayLength);float closest=float.PositiveInfinity;
            if(buttons!=null)
                foreach(var b in buttons)
                    if(Eligible(b,allowed) && ExpandedHit(b.GetComponent<Collider>(),ray,PanelReach,out var point,out float gap) && gap<closest)
                    {closest=gap;end=point;}
            return end;
        }
        // The ray's true point on the box's near face plane (pulled 8 mm toward the hand, like a real hit), if it lies
        // within margin of the face; gap = how far outside the box that point is.
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
            hit=raw-ray.direction*.008f;return true;
        }
        // ---- Sliders ----------------------------------------------------------------------------------------------
        public void AddSlider(EmberdeepSlider slider){if(slider!=null && slider.Track && !sliders.Contains(slider))sliders.Add(slider);}
        public EmberdeepSlider Dragging=>drag.Active?dragSlider:null;
        public int DragHand=>drag.Active?drag.Hand:-1;
        public EmberdeepSliderDrag.Source DragSource=>drag.Active?drag.By:EmberdeepSliderDrag.Source.None;
        // How many drags have ended and committed (tests, logs).
        public int DragCommits {get;private set;}
        EmberdeepSlider SliderFor(WorkshopButton button)
        {
            if(!button)return null;
            foreach(var s in sliders)if(s.Track==button)return s;
            return null;
        }
        bool RayDragging(int hand)=>drag.Active && drag.By==EmberdeepSliderDrag.Source.Ray && drag.Hand==hand && dragSlider!=null;
        void BeginRayDrag(int hand,EmberdeepSlider slider,EmberdeepHandRay.Sample sample)
        {
            float current=slider.Get01!=null?slider.Get01():0;
            float pointer=slider.Pointer01(sample.Ray,out float t)?t:current;
            dragSlider=slider;PointerHand=hand;
            if(drag.Begin(hand,EmberdeepSliderDrag.Source.Ray,pointer,current,slider.Steps))slider.Drag01?.Invoke(drag.Value01);
            if(DeviceLog)Debug.Log("EMBERDEEP_AIM slider=grab hand="+(hand==0?'L':'R')+" by=ray name="+slider.Name+" value="+F(drag.Value01,2));
        }
        void TickRayDrag(int hand,EmberdeepHandRay.Sample sample,bool valid,EmberdeepPinchClick click,float dt)
        {
            bool tracked=valid && EmberdeepPinchClick.Of(sample).Tracked;
            if(tracked && dragSlider.Pointer01(sample.Ray,out float t) && drag.Move(t))dragSlider.Drag01?.Invoke(drag.Value01);
            if(!drag.Tick(tracked,click.Closed,dt))FinishDrag();
        }
        // A fingertip, once per frame per hand after Process (XR owners; the panel must be within reach). It grabs a
        // slider by touching its track and drags it until lifted off.
        public void Poke(int hand,bool tracked,Vector3 tip,float dt)
        {
            if(hand<0 || hand>1)return;
            if(drag.Active && drag.Hand==hand)
            {
                if(drag.By!=EmberdeepSliderDrag.Source.Poke || dragSlider==null)return; // the ray owns this hand's drag
                float along=0,gap=float.PositiveInfinity;
                if(tracked)dragSlider.Poke01(tip,out along,out gap);
                bool held=tracked && gap<=EmberdeepSliderDrag.PokeLift && gap>=-EmberdeepSliderDrag.PokeThrough;
                if(held && drag.Move(along))dragSlider.Drag01?.Invoke(drag.Value01);
                if(!drag.Tick(tracked,held,dt))FinishDrag();
                return;
            }
            if(!tracked || !float.IsFinite(tip.sqrMagnitude) || drag.Active)return; // one drag at a time
            foreach(var s in sliders)
            {
                if(!Eligible(s.Track,null) || !s.Poke01(tip,out float t,out float depth))continue;
                if(depth>EmberdeepSliderDrag.PokeTouch || depth<-EmberdeepSliderDrag.PokeThrough)continue;
                float current=s.Get01!=null?s.Get01():0;dragSlider=s;
                if(drag.Begin(hand,EmberdeepSliderDrag.Source.Poke,t,current,s.Steps))s.Drag01?.Invoke(drag.Value01);
                if(DeviceLog)Debug.Log("EMBERDEEP_AIM slider=grab hand="+(hand==0?'L':'R')+" by=poke name="+s.Name+" value="+F(drag.Value01,2));
                return;
            }
        }
        void FinishDrag()
        {
            var slider=dragSlider;
            if(drag.Active)drag.End();
            dragSlider=null;
            if(slider==null)return;
            slider.Drag01?.Invoke(drag.Value01);slider.Commit?.Invoke();DragCommits++;
            if(DeviceLog)Debug.Log("EMBERDEEP_AIM slider=release name="+slider.Name+" value="+F(drag.Value01,2));
        }

        public void ResetInput()
        {
            // A page change, a closed panel or a pressed button ends a drag where it is, and commits it.
            if(drag.Active)FinishDrag();
            HoveredButton=PressedButton=null;HasPointer=false;
            for(int i=0;i<2;i++)
            {
                rays[i].Reset();clicks[i].Reset();targets[i]=null;visible[i]=drawn[i]=false;fades[i]=0;
                if(lines[i])lines[i].enabled=false;if(cursors[i])cursors[i].gameObject.SetActive(false);
            }
            if(currentButtons!=null)foreach(var button in currentButtons)if(button)button.SetInteractionState(false,false);
        }
        void OnDisable()=>ResetInput();
        void OnDestroy(){if(material)Destroy(material);}
    }
}

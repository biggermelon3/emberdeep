using PhysicalAssembly;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FireRescue
{
    // Headset test 2 (2026-11-22): the board's placement can be adjusted inside a level, on the pause panel (the owner
    // picked this over rim arrows or grabbing the board). Headset test 3 (2026-11-22): the steppers became sliders.
    // Under the run's buttons:
    //   [icon] HEIGHT    [-] ====o======= [+]  value
    //   [icon] DISTANCE  [-] ======o===== [+]  value
    //   [icon]           [ FLAT ][ TILTED ]
    // Pinch the knob or anywhere on the track and drag (EmberdeepSlider, run by EmberdeepHandRayMenu): the board re-places
    // live through EmberdeepPlayArea.Changed (FireRescueGame.Placement.cs) while the pause panel holds still (PlaceScreens'
    // pause lock), snapped to whole centimetres, and the value is saved on release. The -/+ ends nudge 2 cm per press and
    // repeat while held (after RepeatDelay, every RepeatEvery). Values read like the camp settings page
    // (EmberdeepCamp.Settings.cs): centimetres from the default, "0 cm" at it. The tilt pair sets EmberdeepPlayArea.Tilted;
    // the current one is a lit gold tile, the other muted (EmberdeepForestUi.Pair).
    public sealed partial class FireRescueGame
    {
        // Pause panel rows (panel units). The board runs PauseTop..PauseBottom; PauseBelow (degrees from its centre to its
        // lowest edge) follows from PauseBottom at PanelUnitFor's angular size (~15.4 deg per unit) plus a margin.
        public const float PauseTop=.53f,PauseBottom=-.6f,PauseTitleY=.40f,PauseButtonsY=.16f;
        // Slider rows replace the stepper rows (label -.1, steppers -.24) inside the same board, so PauseBelow is unchanged.
        public const float PauseHeightY=-.1f,PauseDistanceY=-.27f,PauseTiltY=-.46f;
        // Across a row: icon, label, [-], the track (the knob's travel), [+], the readout.
        // The label is left-aligned at PauseLabelX in one size for both rows (DISTANCE just fits PauseLabelWidth); the readout
        // ends well inside the frame (the first layout's "+10 cm" touched the right rail).
        public const float PauseIconX=-.88f,PauseLabelX=-.81f,PauseLabelWidth=.32f,PauseMinusX=-.40f,PauseTrackLeft=-.24f,PauseTrackRight=.36f,PausePlusX=.48f,PauseValueX=.74f;
        public const float PauseRowHeight=.14f,PauseStepWidth=.14f,PauseKnobWidth=.045f,PauseKnobHeight=.11f;
        public const float RepeatDelay=.45f,RepeatEvery=.12f;  // s
        public const float HeldPinch=.03f;                     // m: a stepper pinch counts as held while the span stays under this
        static readonly Color UiTrack=new(.05f,.09f,.08f),UiFill=new(.85f,.64f,.26f);
        sealed class PauseSlider
        {
            public EmberdeepSlider Slider;public Transform Knob,Fill;public TextMesh Value;
            public System.Func<float> Get01;public System.Func<string> Format;
        }
        PauseSlider pauseHeight,pauseDistance;
        TextMesh pauseFlatLabel,pauseTiltedLabel;
        WorkshopButton pauseFlat,pauseTilted;
        WorkshopButton heldStepper;System.Action heldStep;int heldHand=-1;
        readonly EmberdeepStepperHold stepperHold=new();
        public int BoardStepRepeats {get;private set;}
        public WorkshopButton HeldStepper=>heldStepper;

        void BuildBoardControls()
        {
            pauseHeight=SliderRow("Height","HEIGHT",EmberdeepUiIcons.Height,PauseHeightY,steps=>BoardNudge(steps,0),
                ()=>EmberdeepPlayArea.Height01,t=>EmberdeepPlayArea.SetHeight01(t,false),EmberdeepPlayArea.HeightSteps,EmberdeepPlayArea.DefaultHeight01,
                ()=>BoardOffsetLabel(EmberdeepPlayArea.Height-EmberdeepPlayArea.DefaultHeight));
            pauseDistance=SliderRow("Distance","DISTANCE",EmberdeepUiIcons.Distance,PauseDistanceY,steps=>BoardNudge(0,steps),
                ()=>EmberdeepPlayArea.Distance01,t=>EmberdeepPlayArea.SetDistance01(t,false),EmberdeepPlayArea.DistanceSteps,EmberdeepPlayArea.DefaultDistance01,
                ()=>BoardOffsetLabel(EmberdeepPlayArea.Distance-EmberdeepPlayArea.DefaultDistance));
            UiIconAt(pauseRoot,EmberdeepUiIcons.Tilt,new Vector3(PauseIconX,PauseTiltY,-.04f),.08f,UiCream);
            pauseFlat=WordButton("Pause board flat",pauseRoot,new Vector3(-.2f,PauseTiltY,0),new Vector2(.36f,.14f),"FLAT",()=>SetBoardTilted(false));
            pauseTilted=WordButton("Pause board tilted",pauseRoot,new Vector3(.2f,PauseTiltY,0),new Vector2(.36f,.14f),"TILTED",()=>SetBoardTilted(true));
            pauseFlatLabel=FaceWord(pauseFlat);pauseTiltedLabel=FaceWord(pauseTilted);
        }
        TextMesh FaceWord(WorkshopButton button)
        {
            var face=button.transform.parent.Find(button.name+" face");return face?face.GetComponentInChildren<TextMesh>(true):null;
        }
        PauseSlider SliderRow(string name,string label,Mesh icon,float y,System.Action<int> nudge,System.Func<float> get01,System.Action<float> live,int steps,
            float defaultTick,System.Func<string> format)
        {
            string key=name.ToLowerInvariant();
            UiIconAt(pauseRoot,icon,new Vector3(PauseIconX,y,-.04f),.065f,UiCream);
            var word=Word(pauseRoot,label,new Vector3(PauseLabelX,y,-.04f),.032f,PauseLabelWidth);word.anchor=TextAnchor.MiddleLeft;word.alignment=TextAlignment.Left;
            WorkshopButton down=null,up=null;
            down=IconButton("Pause "+key+" down",pauseRoot,new Vector3(PauseMinusX,y,0),new Vector2(PauseStepWidth,PauseRowHeight),EmberdeepUiIcons.Minus,()=>StepperPressed(down,()=>nudge(-1)));
            up=IconButton("Pause "+key+" up",pauseRoot,new Vector3(PausePlusX,y,0),new Vector2(PauseStepWidth,PauseRowHeight),EmberdeepUiIcons.Plus,()=>StepperPressed(up,()=>nudge(1)));
            // The grab target: the track plus a margin, a pill like the steppers; the bar, fill, default tick and knob on it.
            float centre=(PauseTrackLeft+PauseTrackRight)*.5f,length=PauseTrackRight-PauseTrackLeft;
            var track=TrackButton("Pause "+key+" track",pauseRoot,new Vector3(centre,y,0),new Vector2(length+.08f,PauseRowHeight));
            var bar=UiIconAt(pauseRoot,EmberdeepUiIcons.Minus,new Vector3(centre,y,-.035f),1,UiTrack);bar.name="Pause "+key+" track bar";
            bar.transform.localScale=new Vector3(length/.68f,.026f/.14f,1);
            var fill=UiIconAt(pauseRoot,EmberdeepUiIcons.Minus,new Vector3(PauseTrackLeft,y,-.037f),1,UiFill);fill.name="Pause "+key+" track fill";
            var tick=UiIconAt(pauseRoot,EmberdeepUiIcons.Minus,new Vector3(Mathf.Lerp(PauseTrackLeft,PauseTrackRight,defaultTick),y,-.039f),1,UiCream);
            tick.name="Pause "+key+" default tick";tick.transform.localScale=new Vector3(.008f/.68f,.07f/.14f,1);
            var knob=UiIconAt(pauseRoot,EmberdeepUiIcons.Disc,new Vector3(centre,y,-.042f),1,UiGold);knob.name="Pause "+key+" knob";
            knob.transform.localScale=new Vector3(PauseKnobWidth,PauseKnobHeight,1);
            var row=new PauseSlider{Knob=knob.transform,Fill=fill.transform,Value=Word(pauseRoot,"",new Vector3(PauseValueX,y,-.04f),.04f,.26f),Get01=get01,Format=format};
            row.Slider=new EmberdeepSlider{Name="Pause "+key,Track=track,Space=pauseRoot,Left=PauseTrackLeft,Right=PauseTrackRight,Y=y,Face=-.0275f,
                HalfHeight=PauseRowHeight*.5f+.01f,Steps=steps,Get01=get01,
                Drag01=t=>{live(t);RefreshBoardControls();},Commit=()=>{EmberdeepPlayArea.Commit();if(Feedback)Feedback.Impact(false);RefreshBoardControls();}};
            EnsureHandMenu();handMenu.AddSlider(row.Slider);
            return row;
        }
        // A pill with nothing on it: the slider's grab target.
        WorkshopButton TrackButton(string command,Transform parent,Vector3 at,Vector2 size)
        {
            var go=Shape(command,PrimitiveType.Cube,parent,at,new Vector3(size.x,size.y,.055f),PanelMaterial,true);
            EmberdeepForestUi.Apply(go,true);
            var button=go.AddComponent<WorkshopButton>();button.Command=command;button.Pressed=null;buttons.Add(button);
            return button;
        }
        // The press itself, then a hold on the same stepper repeats it (TickBoardStepper).
        void StepperPressed(WorkshopButton button,System.Action step)
        {
            step();
            heldStepper=button;heldStep=step;heldHand=handMenu?handMenu.PointerHand:-1;stepperHold.Press(Time.unscaledTime);
        }
        void BoardNudge(int height,int distance)
        {
            if(height!=0)EmberdeepPlayArea.NudgeHeight(height);
            if(distance!=0)EmberdeepPlayArea.NudgeDistance(distance);
            if(Feedback)Feedback.Impact(false);
            RefreshBoardControls();
        }
        void SetBoardTilted(bool tilted)
        {
            heldStepper=null;
            if(EmberdeepPlayArea.Tilted!=tilted){EmberdeepPlayArea.SetTilted(tilted);if(Feedback)Feedback.Impact(false);}
            RefreshBoardControls();
        }
        // Tests and scripts: the same steps the stepper buttons take.
        public void PauseNudgeHeight(int steps){if(PauseOpen)BoardNudge(steps,0);}
        public void PauseNudgeDistance(int steps){if(PauseOpen)BoardNudge(0,steps);}
        public void PauseSetTilted(bool tilted){if(PauseOpen)SetBoardTilted(tilted);}
        public static string BoardOffsetLabel(float metres){int cm=Mathf.RoundToInt(metres*100);return cm==0?"0 cm":(cm>0?"+":"")+cm+" cm";}
        public string PauseHeightLabel=>pauseHeight!=null && pauseHeight.Value?pauseHeight.Value.text:"";
        public string PauseDistanceLabel=>pauseDistance!=null && pauseDistance.Value?pauseDistance.Value.text:"";
        // Tests: the knob's place along its track (0..1), whether a pause slider is held, and which tilt tile is lit.
        public float PauseKnob01(bool height){var row=height?pauseHeight:pauseDistance;return row==null?float.NaN:Mathf.InverseLerp(PauseTrackLeft,PauseTrackRight,row.Knob.localPosition.x);}
        public bool PauseSliderHeld=>handMenu && handMenu.Dragging!=null && (handMenu.Dragging==pauseHeight?.Slider || handMenu.Dragging==pauseDistance?.Slider);
        public bool PauseTiltChosen(bool tilted)
        {
            var b=tilted?pauseTilted:pauseFlat;var skin=b?b.GetComponent<EmberdeepForestUi>():null;
            return skin && skin.Pick==EmberdeepForestUi.Choice.Chosen;
        }

        // RefreshScreens, every frame the pause panel shows.
        void RefreshBoardControls()
        {
            if(pauseHeight==null)return;
            RefreshPauseSlider(pauseHeight);RefreshPauseSlider(pauseDistance);
            EmberdeepForestUi.Pair(pauseFlat,pauseFlatLabel,pauseTilted,pauseTiltedLabel,!EmberdeepPlayArea.Tilted);
        }
        void RefreshPauseSlider(PauseSlider row)
        {
            Say(row.Value,row.Format());
            float x=Mathf.Lerp(PauseTrackLeft,PauseTrackRight,Mathf.Clamp01(row.Get01()));
            var k=row.Knob.localPosition;if(!Mathf.Approximately(k.x,x))row.Knob.localPosition=new Vector3(x,k.y,k.z);
            // The held knob grows, so the grab reads at panel distance.
            float grow=handMenu && handMenu.Dragging==row.Slider?1.3f:1;
            var size=new Vector3(PauseKnobWidth*grow,PauseKnobHeight*grow,1);if(row.Knob.localScale!=size)row.Knob.localScale=size;
            float width=Mathf.Max(.002f,x-PauseTrackLeft);
            row.Fill.localScale=new Vector3(width/.68f,.026f/.14f,1);
            var f=row.Fill.localPosition;row.Fill.localPosition=new Vector3(PauseTrackLeft+width*.5f,f.y,f.z);
        }
        // Hold to repeat: the hand that pressed keeps its pinch closed with its ray on the stepper (desktop: the mouse button
        // held over it). Any release, a ray that leaves, or the panel closing ends the hold. Also, once per frame while the
        // pause panel shows: a fingertip on a slider's track drags it (the panel is usually out of reach, ~1 m away).
        void TickBoardStepper()
        {
            PokeBoardSliders();
            if(!heldStepper)return;
            if(!PauseOpen || !heldStepper.gameObject.activeInHierarchy || !handMenu){heldStepper=null;stepperHold.Release();return;}
            bool hovered=handMenu.IsHovered(heldStepper),held=false;
            if(heldHand>=0 && heldHand<2){var hand=interfacePhysical[heldHand];held=hand.Tracked && hand.Pinch<HeldPinch && hovered;}
            if(!held && !Player.IsXR)held=Mouse.current!=null && Mouse.current.leftButton.isPressed && hovered;
            int repeats=stepperHold.Tick(held,Time.unscaledTime);
            if(!stepperHold.Holding)heldStepper=null;
            for(int i=0;i<repeats;i++){BoardStepRepeats++;heldStep?.Invoke();}
        }
        void PokeBoardSliders()
        {
            if(!Player.IsXR || !handMenu || !PauseOpen || !hands)return;
            for(int hand=0;hand<2;hand++)
            {
                var tip=Vector3.zero;
                bool tracked=HandSeen(hand==0) && hands.TryReadFingertip(hand==0,1,out tip);
                handMenu.Poke(hand,tracked,tip,Time.unscaledDeltaTime);
            }
        }
    }

    // Hold-to-repeat timing for a stepper: Press starts it; while Tick says held, a repeat fires RepeatDelay after the
    // press and then every RepeatEvery (at most one per tick, so a long frame never jumps the board several steps);
    // the first tick that is not held ends it.
    public sealed class EmberdeepStepperHold
    {
        float next;
        public bool Holding {get;private set;}
        public void Press(float now){Holding=true;next=now+FireRescueGame.RepeatDelay;}
        public void Release()=>Holding=false;
        public int Tick(bool held,float now)
        {
            if(!Holding)return 0;
            if(!held){Holding=false;return 0;}
            if(now<next)return 0;
            next=Mathf.Max(next+FireRescueGame.RepeatEvery,now+FireRescueGame.RepeatEvery*.5f);return 1;
        }
    }
}

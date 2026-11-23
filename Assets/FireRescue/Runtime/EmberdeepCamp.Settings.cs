using System;
using System.Collections.Generic;
using PhysicalAssembly;
using UnityEngine;

namespace FireRescue
{
    // LoopSpec §10 options (workstream O): the camp settings page. Every control is a hand-ray button (pinch to press).
    // Sliders (headset test 3, 2026-11-22): pinch the knob or anywhere on the track and drag along it (EmberdeepSlider,
    // run by EmberdeepHandRayMenu); the value follows live and is saved on release. A pinch off the knob jumps it there
    // first. The -/+ ends stay as 1-step nudges. Paired choices (TILT, WATER HAND) show the current one as a lit gold tile
    // and the other muted (EmberdeepForestUi.Pair), not an underline. Short labels with icons; the words are
    // phrase-matched by Translations.tsv.
    //   HEIGHT / DISTANCE: the compact board's seated placement (EmberdeepPlayArea), re-placed live in the fireground.
    //     The readout is the offset from the default (headset test 2: stored -14 / +6 cm), so the default reads 0 cm and
    //     its tick marks it; the stored values stay the absolute offsets (EmberdeepPlayArea);
    //   TILT: FLAT (default) / TILTED, the board pitched toward the player about its front row (EmberdeepPlayArea.Tilted);
    //   WATER HAND: EmberdeepWaterHand (gem, aim, pump side, rim UI mirror with it);
    //   MUSIC / SOUND: EmberdeepAudioSettings, separate volumes;
    //   RECENTER: re-places the camp menu;
    //   SHOW HINTS: brings the first-time hand hints back (EmberdeepFirstHints.ResetAll); dimmed while none has been
    //     used up yet (EmberdeepFirstHints.AllFresh). Requested by the rim/hints session, 2026-11-22.
    public sealed partial class EmberdeepCamp
    {
        // Six rows (TILT joined on 2026-11-22): the title and first row moved up and the step tightened from .075 so the
        // last row still clears RECENTER / BACK (.0175 gap; rows .018 apart, ~1.5 deg at the menu's distance).
        public const float SettingsTitleY=.215f,SettingsRowY=.155f,SettingsRowStep=.068f,SettingsBottomY=-.255f;
        public const float TrackLeft=-.115f,TrackRight=.195f,ControlHeight=.05f;
        const float IconX=-.405f,LabelLeft=-.38f,LabelWidth=.16f,MinusX=-.18f,PlusX=.26f,ValueX=.36f;
        public const float ControlZ=-.03f;
        sealed class SliderRow
        {
            public string Name;public WorkshopButton Track,Down,Up;public Transform Knob,Fill;public TextMesh Value;
            public Func<float> Get01;public Action<float> Set01,Live;public Action<int> Nudge;public Func<string> Format;
            public Action Commit;public EmberdeepSlider Slider;
        }
        readonly List<SliderRow> sliders=new();
        WorkshopButton waterLeftButton,waterRightButton,flatButton,tiltedButton;
        TextMesh waterLeftLabel,waterRightLabel,flatLabel,tiltedLabel;
        TextMesh showHintsLabel,lastControlLabel;
        // The knob grows while it is held, so the grab reads at arm's length.
        public const float KnobHeld=1.35f;
        static readonly Vector3 KnobSize=new(.018f,.038f,.008f);
        // Bottom row: RECENTER, SHOW HINTS, BACK (three pills since SHOW HINTS joined).
        public const float BottomPillWidth=.25f,BottomPillStep=.28f;
        Material iconMaterial,trackMaterial;
        public WorkshopButton SettingsButton(string command){foreach(var b in buttons)if(b && b.Command==command)return b;return null;}
        // Where on a slider's track (0..1) a page-space point falls.
        public static float TrackValue(float pageX)=>Mathf.InverseLerp(TrackLeft,TrackRight,pageX);

        void BuildSettingsPage()
        {
            iconMaterial=new Material(boardMaterial);iconMaterial.SetColor("_BaseColor",new Color(1,.73f,.38f));
            trackMaterial=new Material(boardMaterial);trackMaterial.SetColor("_BaseColor",new Color(.05f,.10f,.11f));
            Text(settingsPage,"SETTINGS",new Vector3(0,SettingsTitleY,-.035f),.02f,Color.white);
            float y=SettingsRowY;
            Slider("Height","HEIGHT",SettingsIcons.Height,y,()=>EmberdeepPlayArea.Height01,EmberdeepPlayArea.SetHeight01,EmberdeepPlayArea.NudgeHeight,
                ()=>Centimetres(EmberdeepPlayArea.Height-EmberdeepPlayArea.DefaultHeight),EmberdeepPlayArea.DefaultHeight01,
                t=>EmberdeepPlayArea.SetHeight01(t,false),EmberdeepPlayArea.Commit,EmberdeepPlayArea.HeightSteps);
            y-=SettingsRowStep;
            Slider("Distance","DISTANCE",SettingsIcons.Distance,y,()=>EmberdeepPlayArea.Distance01,EmberdeepPlayArea.SetDistance01,EmberdeepPlayArea.NudgeDistance,
                ()=>Centimetres(EmberdeepPlayArea.Distance-EmberdeepPlayArea.DefaultDistance),EmberdeepPlayArea.DefaultDistance01,
                t=>EmberdeepPlayArea.SetDistance01(t,false),EmberdeepPlayArea.Commit,EmberdeepPlayArea.DistanceSteps);
            y-=SettingsRowStep;
            RowLabel("TILT",SettingsIcons.Tilt,y);
            flatButton=Control(settingsPage,"Board flat","FLAT",new Vector3(-.04f,y,ControlZ),new Vector2(.22f,ControlHeight),()=>SetTilted(false),null,.0135f);flatLabel=lastControlLabel;
            tiltedButton=Control(settingsPage,"Board tilted","TILTED",new Vector3(.2f,y,ControlZ),new Vector2(.22f,ControlHeight),()=>SetTilted(true),null,.0135f);tiltedLabel=lastControlLabel;
            y-=SettingsRowStep;
            RowLabel("WATER HAND",EmberdeepUiIcons.Drop,y);
            waterLeftButton=Control(settingsPage,"Water hand left","LEFT",new Vector3(-.04f,y,ControlZ),new Vector2(.22f,ControlHeight),()=>SetWaterHand(true),null,.0135f);waterLeftLabel=lastControlLabel;
            waterRightButton=Control(settingsPage,"Water hand right","RIGHT",new Vector3(.2f,y,ControlZ),new Vector2(.22f,ControlHeight),()=>SetWaterHand(false),null,.0135f);waterRightLabel=lastControlLabel;
            y-=SettingsRowStep;
            Slider("Music","MUSIC",SettingsIcons.Music,y,()=>EmberdeepAudioSettings.Music,EmberdeepAudioSettings.SetMusic,EmberdeepAudioSettings.NudgeMusic,
                ()=>Percent(EmberdeepAudioSettings.Music),-1,t=>EmberdeepAudioSettings.SetMusic(t,false),EmberdeepAudioSettings.Commit,EmberdeepAudioSettings.Steps);
            y-=SettingsRowStep;
            Slider("Sound","SOUND",SettingsIcons.Speaker,y,()=>EmberdeepAudioSettings.Sfx,EmberdeepAudioSettings.SetSfx,EmberdeepAudioSettings.NudgeSfx,
                ()=>Percent(EmberdeepAudioSettings.Sfx),-1,t=>EmberdeepAudioSettings.SetSfx(t,false),EmberdeepAudioSettings.Commit,EmberdeepAudioSettings.Steps);
            Control(settingsPage,"Recenter Menu","RECENTER",new Vector3(-BottomPillStep,SettingsBottomY,ControlZ),new Vector2(BottomPillWidth,.055f),Recenter,EmberdeepUiIcons.Target);
            Control(settingsPage,"Show hints","SHOW HINTS",new Vector3(0,SettingsBottomY,ControlZ),new Vector2(BottomPillWidth,.055f),()=>{EmberdeepFirstHints.ResetAll();RefreshSettings();},EmberdeepUiIcons.Retry);
            showHintsLabel=lastControlLabel;
            Control(settingsPage,"Back","BACK",new Vector3(BottomPillStep,SettingsBottomY,ControlZ),new Vector2(BottomPillWidth,.055f),()=>ShowPage(MenuPage.Main),EmberdeepUiIcons.Back);
            EmberdeepWaterHand.Changed+=RefreshSettings;EmberdeepPlayArea.Changed+=RefreshSettings;EmberdeepFirstHints.Reset+=RefreshSettings;
            RefreshSettings();
        }
        void Slider(string name,string label,Mesh icon,float y,Func<float> get01,Action<float> set01,Action<int> nudge,Func<string> format,float defaultTick,
            Action<float> live,Action commit,int steps)
        {
            var row=new SliderRow{Name=name,Get01=get01,Set01=set01,Nudge=nudge,Format=format,Live=live,Commit=commit};
            RowLabel(label,icon,y);
            row.Down=Control(settingsPage,name+" down",null,new Vector3(MinusX,y,ControlZ),new Vector2(ControlHeight,ControlHeight),()=>{row.Nudge(-1);RefreshSettings();},SettingsIcons.Minus);
            row.Up=Control(settingsPage,name+" up",null,new Vector3(PlusX,y,ControlZ),new Vector2(ControlHeight,ControlHeight),()=>{row.Nudge(1);RefreshSettings();},SettingsIcons.Plus);
            float centre=(TrackLeft+TrackRight)*.5f,length=TrackRight-TrackLeft;
            // The pinch target spans the whole track plus a margin, so both ends are easy to hit.
            row.Track=Control(settingsPage,name+" track",null,new Vector3(centre,y,ControlZ),new Vector2(length+.04f,ControlHeight),()=>PressTrack(row));
            Shape(name+" track bar",settingsPage,new Vector3(centre,y,ControlZ-.016f),new Vector3(length,.009f,.004f),trackMaterial);
            row.Fill=Shape(name+" track fill",settingsPage,new Vector3(TrackLeft,y,ControlZ-.017f),new Vector3(.001f,.009f,.004f),accentMaterial).transform;
            if(defaultTick>=0)Shape(name+" default tick",settingsPage,new Vector3(Mathf.Lerp(TrackLeft,TrackRight,defaultTick),y,ControlZ-.018f),new Vector3(.003f,.03f,.004f),iconMaterial);
            row.Knob=Shape(name+" knob",settingsPage,new Vector3(centre,y,ControlZ-.02f),KnobSize,iconMaterial).transform;
            row.Value=Text(settingsPage,"",new Vector3(ValueX,y,-.035f),.014f,new Color(.86f,.92f,.9f));Fit(row.Value,.085f);
            // The drag (EmberdeepHandRayMenu): the knob's travel on the track's front face; registered once the menu exists.
            row.Slider=new EmberdeepSlider{Name=name,Track=row.Track,Space=settingsPage,Left=TrackLeft,Right=TrackRight,Y=y,Face=ControlZ-.0125f,
                HalfHeight=ControlHeight*.5f+.005f,Steps=steps,Get01=get01,Drag01=t=>{row.Live(t);RefreshSettings();},Commit=()=>{row.Commit();RefreshSettings();}};
            sliders.Add(row);
        }
        void RegisterSettingsSliders(){foreach(var row in sliders)menuInput.AddSlider(row.Slider);}
        void RowLabel(string label,Mesh icon,float y)
        {
            EmberdeepUiIcons.Icon(label+" icon",settingsPage,icon,iconMaterial,new Vector3(IconX,y,-.035f),.03f);
            // One size for every row label: the longest English one (WATER HAND) just fits the column.
            var text=Text(settingsPage,label,new Vector3(LabelLeft,y,-.035f),.0115f,Color.white);
            text.anchor=TextAnchor.MiddleLeft;text.alignment=TextAlignment.Left;Fit(text,LabelWidth);
        }
        // Shrink-to-fit after localization swaps the words (the authored size is the maximum).
        static void Fit(TextMesh text,float width,float height=.03f)
        {var bounds=text.gameObject.AddComponent<EmberdeepTextBounds>();bounds.Width=width;bounds.Height=height;}
        // A hand-ray button: pill skin, trigger box, ray outline, optional label and icon.
        WorkshopButton Control(Transform parent,string command,string label,Vector3 at,Vector2 size,Action action,Mesh icon=null,float textSize=.016f)
        {
            var go=Shape(command,parent,at,new Vector3(size.x,size.y,.025f),buttonMaterial);
            var collider=go.AddComponent<BoxCollider>();collider.isTrigger=true;
            EmberdeepForestUi.Apply(go,true);
            var button=go.AddComponent<WorkshopButton>();button.Command=command;button.Pressed=action;buttons.Add(button);
            var highlight=Shape(command+" ray outline",parent,at+Vector3.forward*.01f,new Vector3(size.x+.015f,size.y+.015f,.01f),accentMaterial);
            highlights.Add(button,highlight);highlight.SetActive(false);
            float iconSize=Mathf.Min(size.y*.62f,.03f);
            if(icon && label==null)EmberdeepUiIcons.Icon(command+" icon",parent,icon,iconMaterial,at+Vector3.back*.016f,iconSize);
            else if(icon)EmberdeepUiIcons.Icon(command+" icon",parent,icon,iconMaterial,at+new Vector3(-size.x*.5f+size.y*.55f,0,-.016f),iconSize);
            // Text pass: the label's line box may take 75% of the pill. The old fixed .03 bound shrank every label (SKIP to
            // 67% in English, 52% in Chinese: Noto CJK's line box is 2.9x the size, LiberationSans 2.24x).
            lastControlLabel=null;
            if(label!=null)Fit(lastControlLabel=Text(parent,label,at+new Vector3(icon?size.y*.3f:0,0,-.016f),textSize,Color.white),size.x-(icon?size.y*1.3f:.03f),size.y*.75f);
            return button;
        }
        // Which tile of a pair is the current choice (tests).
        public bool OptionChosen(string command)
        {
            var b=SettingsButton(command);var skin=b?b.GetComponent<EmberdeepForestUi>():null;
            return skin && skin.Pick==EmberdeepForestUi.Choice.Chosen;
        }
        public bool SliderHeld(string name)=>menuInput && menuInput.Dragging!=null && menuInput.Dragging.Name==name;
        // Per frame while the page shows: the held knob grows.
        void TickSettingsSliders()
        {
            if(ActivePage!=MenuPage.Settings)return;
            foreach(var row in sliders)
            {
                var size=KnobSize*(SliderHeld(row.Name)?KnobHeld:1);
                if(row.Knob.localScale!=size)row.Knob.localScale=size;
            }
        }
        // Only a direct Press() reaches this (scripts): the menu turns a ray pinch on a track into a drag instead.
        void PressTrack(SliderRow row)
        {
            // Sets the value where the pointing hand's ray ends on the track (EmberdeepHandRayMenu).
            var end=settingsPage.InverseTransformPoint(menuInput.RayEnd(menuInput.PointerHand));
            row.Set01(TrackValue(end.x));RefreshSettings();
        }
        void SetWaterHand(bool left){EmberdeepWaterHand.Set(left);RefreshSettings();}
        void SetTilted(bool tilted){EmberdeepPlayArea.SetTilted(tilted);RefreshSettings();}
        public void RefreshSettings()
        {
            if(!settingsPage)return;
            foreach(var row in sliders)
            {
                float t=Mathf.Clamp01(row.Get01());float x=Mathf.Lerp(TrackLeft,TrackRight,t);
                row.Knob.localPosition=new Vector3(x,row.Knob.localPosition.y,row.Knob.localPosition.z);
                float width=Mathf.Max(.001f,x-TrackLeft);
                row.Fill.localScale=new Vector3(width,row.Fill.localScale.y,row.Fill.localScale.z);
                row.Fill.localPosition=new Vector3(TrackLeft+width*.5f,row.Fill.localPosition.y,row.Fill.localPosition.z);
                row.Value.text=row.Format();
            }
            EmberdeepForestUi.Pair(waterLeftButton,waterLeftLabel,waterRightButton,waterRightLabel,EmberdeepWaterHand.Left);
            EmberdeepForestUi.Pair(flatButton,flatLabel,tiltedButton,tiltedLabel,!EmberdeepPlayArea.Tilted);
            // Nothing to bring back: the label dims (the button still works).
            ShowHintsDimmed=EmberdeepFirstHints.AllFresh;
            if(showHintsLabel)showHintsLabel.color=ShowHintsDimmed?HintsDimmed:Color.white;
        }
        public static readonly Color HintsDimmed=new(.55f,.62f,.62f);
        public bool ShowHintsDimmed {get;private set;} // TextMesh keeps 8-bit colour, so tests read this flag
        public float SliderKnob01(string name){foreach(var row in sliders)if(row.Name==name)return Mathf.InverseLerp(TrackLeft,TrackRight,row.Knob.localPosition.x);return float.NaN;}
        static string Centimetres(float metres){int cm=Mathf.RoundToInt(metres*100);return cm==0?"0 cm":(cm>0?"+":"")+cm+" cm";}
        static string Percent(float volume)=>Mathf.RoundToInt(volume*100)+"%";
        void ReleaseSettings()
        {
            EmberdeepWaterHand.Changed-=RefreshSettings;EmberdeepPlayArea.Changed-=RefreshSettings;EmberdeepFirstHints.Reset-=RefreshSettings;
            if(iconMaterial)Destroy(iconMaterial);if(trackMaterial)Destroy(trackMaterial);
        }

        // Flat two-sided icons in a unit square, like EmberdeepUiIcons.
        static class SettingsIcons
        {
            static readonly Dictionary<string,Mesh> cache=new();
            public static Mesh Minus=>Get("minus",()=>Build(Rect(-.34f,-.07f,.68f,.14f)));
            public static Mesh Plus=>Get("plus",()=>Build(Rect(-.34f,-.07f,.68f,.14f),Rect(-.07f,-.34f,.14f,.68f)));
            // Up and down: how high the board sits.
            public static Mesh Height=>Get("height",()=>Build(Rect(-.07f,-.3f,.14f,.6f),Tri(new(-.3f,.2f),new(0,.5f),new(.3f,.2f)),Tri(new(-.3f,-.2f),new(.3f,-.2f),new(0,-.5f)),
                Rect(-.48f,-.04f,.24f,.08f),Rect(.24f,-.04f,.24f,.08f)));
            // Near and far: a board plank with an arrow away from you.
            public static Mesh Distance=>Get("distance",()=>Build(new[]{new Vector2(-.5f,-.5f),new Vector2(-.3f,-.18f),new Vector2(.3f,-.18f),new Vector2(.5f,-.5f)},
                Rect(-.06f,-.1f,.12f,.36f),Tri(new(-.24f,.24f),new(0,.5f),new(.24f,.24f))));
            // Tilt: the table line with the board plank raised 30 deg about its near (left) end.
            public static Mesh Tilt=>Get("tilt",()=>Build(Rect(-.48f,-.46f,.96f,.08f),
                new[]{new Vector2(-.42f,-.34f),new Vector2(-.48f,-.236f),new Vector2(.30f,.214f),new Vector2(.36f,.11f)},
                Circle(new(-.42f,-.34f),.07f,.07f,10)));
            public static Mesh Music=>Get("music",()=>Build(Circle(new(-.16f,-.3f),.18f,.14f,16),Rect(-.01f,-.3f,.09f,.78f),
                new[]{new Vector2(.08f,.48f),new Vector2(.38f,.3f),new Vector2(.38f,.16f),new Vector2(.08f,.32f)}));
            public static Mesh Speaker=>Get("speaker",()=>BuildWithStrips(new[]{Rect(-.48f,-.14f,.2f,.28f),new[]{new Vector2(-.28f,-.14f),new Vector2(-.28f,.14f),new Vector2(.02f,.4f),new Vector2(.02f,-.4f)}},
                new[]{Arc(.18f,.26f),Arc(.34f,.42f)}));
            static Mesh Get(string key,Func<Mesh> build)
            {
                if(cache.TryGetValue(key,out var mesh) && mesh)return mesh;
                mesh=build();mesh.name="Emberdeep settings icon "+key;mesh.hideFlags=HideFlags.DontSave;cache[key]=mesh;return mesh;
            }
            static Vector2[] Rect(float x,float y,float w,float h)=>new[]{new Vector2(x,y),new Vector2(x,y+h),new Vector2(x+w,y+h),new Vector2(x+w,y)};
            static Vector2[] Tri(Vector2 a,Vector2 b,Vector2 c)=>new[]{a,b,c};
            static Vector2[] Circle(Vector2 c,float rx,float ry,int n){var p=new Vector2[n];for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n;p[i]=c+new Vector2(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry);}return p;}
            // A sound wave: a thin ring segment right of the speaker; outer points 0..n, then inner points n..0.
            static Vector2[] Arc(float inner,float outer)
            {
                const int n=8;var p=new Vector2[(n+1)*2];
                for(int i=0;i<=n;i++){float a=Mathf.Lerp(-.9f,.9f,i/(float)n);var d=new Vector2(Mathf.Cos(a),Mathf.Sin(a));p[i]=new Vector2(.06f,0)+d*outer;p[p.Length-1-i]=new Vector2(.06f,0)+d*inner;}
                return p;
            }
            // Convex outlines fan from their centre; arcs are strips of outer/inner pairs. Both windings.
            static Mesh Build(params Vector2[][] fans)=>BuildWithStrips(fans,Array.Empty<Vector2[]>());
            static Mesh BuildWithStrips(Vector2[][] fans,Vector2[][] strips)
            {
                var v=new List<Vector3>();var t=new List<int>();
                foreach(var p in fans)
                {
                    int s=v.Count;var centre=Vector2.zero;foreach(var q in p)centre+=q;centre/=p.Length;
                    v.Add(centre);foreach(var q in p)v.Add(q);
                    for(int i=0;i<p.Length;i++)Both(t,s,s+1+i,s+1+(i+1)%p.Length);
                }
                foreach(var p in strips)
                {
                    int s=v.Count,half=p.Length/2;foreach(var q in p)v.Add(q);
                    for(int i=0;i<half-1;i++){int a=s+i,b=s+i+1,c=s+p.Length-1-i,d=s+p.Length-2-i;Both(t,a,b,c);Both(t,b,d,c);}
                }
                var mesh=new Mesh();mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
            }
            static void Both(List<int> t,int a,int b,int c){t.Add(a);t.Add(b);t.Add(c);t.Add(a);t.Add(c);t.Add(b);}
        }
    }
}

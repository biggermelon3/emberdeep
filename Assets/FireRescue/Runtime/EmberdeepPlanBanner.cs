using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FireRescue
{
    // Headset test 4 (2026-11-22, owner: "say clearly it's the Planning Phase at each level start"). Each level's Plan
    // opens with a card over the piece tray: PLAN (a LoopSpec §13 label) and one prompt line (Plan is not a fight, so a
    // sentence is allowed). After BigSeconds it shrinks into a small PLAN tag standing on the tray's front edge, and stays
    // until the bell. Presentation only: FireRescueGame.Pieces decides when (PlanBannerWanted) and where (the tray).
    // Placement (clear of everything else that shows in Plan, from the default tilted seat):
    // - the card centres BigLift over the tray, ~38 deg under eye level, over the front rows: the bell hangs over the
    //   field's middle at canopy height (~20 deg down), the readout strip and the weathervane are behind the board
    //   (~10-17 deg), the rim's token and hourglass are on the front corners (~30+ deg aside), and the Plan spirit and its
    //   subtitle are over the back corner on the water-hand side (FireRescueGame.Spirit.cs, ~20-25 deg down, aside);
    // - the tower tip (over the tray's first tower) waits until the card has shrunk, so the two never stack;
    // - the outcome banner and the results panel only come after the fight, when this is gone.
    // World-upright and turned to the eye, so it reads the same on the flat and the tilted board.
    [DefaultExecutionOrder(10020)] // after EmberdeepLocalizedText swaps the words, so the fit measures the shown text
    public sealed class EmberdeepPlanBanner : MonoBehaviour
    {
        public const string Title="PLAN",Line="Place pieces. Ring the bell to start.",LineNoPieces="Ring the bell to start";
        // m: letter heights (the rendered line box), gap, padding, border, widest line.
        // The card is ~43 cm from the default seat: title ~5 deg tall, the line ~1.9 deg, at most ~40 deg wide.
        public const float TitleHeight=.04f,LineHeight=.014f,LineGap=.006f,Pad=.012f,Border=.002f,MaxWidth=.30f;
        // The small tag: title only, SmallTitle letter height (m).
        public const float SmallTitle=.014f;
        // Big card: its centre BigLift (m) over the tray's top (clear over the towers, 11.5 cm tall in the tray). Small tag:
        // its foot SmallLift (m) over the tray's front edge, just over the rim (1 cm), turned up to the eye.
        // BigToward (m) toward the eye, flat: on the tilted board the front row's fire cues lean toward the player and
        // stood in front of the card's top half (first capture); 6 cm puts the whole card clearly nearer than them.
        public const float BigLift=.22f,BigToward=.06f,SmallLift=.012f;
        public const float BigSeconds=3f,ShrinkSeconds=.45f,AppearSeconds=.25f;

        Transform panel;TextMesh title,line;Renderer fill,rim;
        Material plate,titleMaterial,lineMaterial;Mesh fillMesh,rimMesh;MaterialPropertyBlock block;
        // Drawn after the board's fire cues, smoke and readouts (first capture: the fire icons over the field drew over the
        // card), like the rim HUD (EmberdeepRimHud AfterVeil/TextQueue): the plate depth-tested, so a hand in front of it
        // still shows; the words on top (the TextMesh font shader ignores depth).
        public const int PlateQueue=3150,TextQueue=3200;
        float age;bool small;string lineSource=Line;
        // The game's per-frame answer: show or not, where, and whether a new Plan just started. Polled in LateUpdate, so
        // frames where the game's Update returns early (rim input, pause) never make it blink.
        public delegate bool Frame(out Vector3 big,out Vector3 front,out Camera eye,out bool hasPieces,out bool restart);
        Frame frame;
        public void Bind(Frame source)=>frame=source;
        Vector3 bigAt,smallAt;Camera eye;
        public bool Visible=>panel && panel.gameObject.activeSelf;
        // True while the card is (or is turning into) the big one.
        public bool Big=>Visible && age<BigSeconds;
        public bool Small=>Visible && age>=BigSeconds;
        public float Age=>age;
        public TextMesh TitleText=>title;
        public TextMesh LineText=>line;
        public bool LineShown=>line && line.gameObject.activeSelf;
        // Panel size (m) after the fit, at the current scale.
        public Vector2 Size {get;private set;}
        public Vector2 ShownSize=>panel?Size*panel.localScale.x*Mathf.Abs(transform.lossyScale.x):Vector2.zero;
        public Vector3 Center=>panel?panel.position:transform.position;
        public void Corners(Vector3[] four)
        {
            var h=new Vector3(Size.x*.5f,Size.y*.5f,0);
            four[0]=panel.TransformPoint(new Vector3(-h.x,-h.y,0));four[1]=panel.TransformPoint(new Vector3(-h.x,h.y,0));
            four[2]=panel.TransformPoint(new Vector3(h.x,h.y,0));four[3]=panel.TransformPoint(new Vector3(h.x,-h.y,0));
        }

        void Ensure()
        {
            if(panel)return;
            block=new MaterialPropertyBlock();
            panel=new GameObject("Plan banner panel").transform;panel.SetParent(transform,false);
            var source=Resources.Load<Material>("EmberdeepMenuUnlit");
            plate=source?new Material(source){name="Plan banner plate",renderQueue=PlateQueue}:new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Plan banner plate",renderQueue=PlateQueue};
            fillMesh=new Mesh{name="Plan banner plate"};rimMesh=new Mesh{name="Plan banner rim"};
            rim=Quad("Plan rim",panel,rimMesh,.002f);fill=Quad("Plan plate",panel,fillMesh,.001f);
            title=Text("Plan title",Title,new Color(1,.83f,.42f));line=Text("Plan line",Line,new Color(.97f,.94f,.86f));
            Tint(rim,new Color(.78f,.6f,.27f));Tint(fill,new Color(.12f,.09f,.07f));
            panel.gameObject.SetActive(false);
        }
        Renderer Quad(string name,Transform parent,Mesh mesh,float z)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=new Vector3(0,0,z);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=plate;
            r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;return r;
        }
        TextMesh Text(string name,string value,Color color)
        {
            var go=new GameObject(name);go.transform.SetParent(panel,false);
            var t=go.AddComponent<TextMesh>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");go.GetComponent<MeshRenderer>().sharedMaterial=t.font.material;
            t.fontSize=64;t.characterSize=.001f;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=color;t.text=value;
            go.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;return t;
        }
        void Tint(Renderer r,Color color){if(!r)return;block.Clear();block.SetColor("_BaseColor",color);r.SetPropertyBlock(block);}

        // A new Plan phase: the big card again.
        public void Restart(){age=0;small=false;if(panel)panel.gameObject.SetActive(false);}
        // Each frame it shows (from LateUpdate, or a test). big: world point (the tray's top centre); front: the tray's
        // front edge (world).
        // Time only runs while it is shown, so a pause does not eat the big card.
        public void Show(Vector3 big,Vector3 front,Camera view,bool hasPieces,float dt)
        {
            Ensure();bigAt=big;smallAt=front;eye=view;
            // Written only when the English changes: the localizer swaps the shown text, so never write it back each frame.
            string want=hasPieces?Line:LineNoPieces;if(lineSource!=want){lineSource=want;line.text=want;}
            if(!panel.gameObject.activeSelf){panel.gameObject.SetActive(true);}
            else age+=Mathf.Clamp(dt,0,.1f);
            bool nowSmall=age>=BigSeconds;
            if(nowSmall!=small){small=nowSmall;line.gameObject.SetActive(!small);Size=Vector2.zero;}
            Fit();Place();
        }
        public void Hide(){if(panel && panel.gameObject.activeSelf)panel.gameObject.SetActive(false);}
        void LateUpdate()
        {
            if(frame==null)return;
            bool show=frame(out var big,out var front,out var view,out bool hasPieces,out bool restart);
            if(restart)Restart();
            if(show)Show(big,front,view,hasPieces,Time.unscaledDeltaTime);else Hide();
        }
        void Place()
        {
            float appear=Mathf.SmoothStep(.85f,1,Mathf.Clamp01(age/AppearSeconds));
            float shrink=Mathf.SmoothStep(0,1,Mathf.Clamp01((age-BigSeconds)/ShrinkSeconds));
            var up=Vector3.up;
            // Big: centred BigLift over the tray. Small: foot on the tray's front edge.
            float scale=Mathf.Lerp(1,SmallTitle/TitleHeight,shrink)*(age<BigSeconds?appear:1);
            var toEye=eye?eye.transform.position-bigAt:Vector3.back;toEye.y=0;
            var bigCentre=bigAt+up*BigLift+(toEye.sqrMagnitude>1e-6f?toEye.normalized*BigToward:Vector3.zero);
            // The tag's foot sits on the edge; it rises along its own up (turned to the eye), so it leans back over the rim.
            var foot=smallAt+up*SmallLift;
            var toFoot=eye?foot-eye.transform.position:Vector3.forward;if(toFoot.sqrMagnitude<1e-6f)toFoot=Vector3.forward;
            var smallCentre=foot+Quaternion.LookRotation(toFoot.normalized,up)*Vector3.up*(Size.y*SmallTitle/TitleHeight*.5f);
            var centre=Vector3.Lerp(bigCentre,smallCentre,shrink);
            var look=eye?centre-eye.transform.position:Vector3.forward;
            if(look.sqrMagnitude<1e-6f)look=Vector3.forward;
            // Sizes are metres: undo the parent's scale (it hangs under the arena, which is scaled to the miniature).
            float inv=1/Mathf.Max(1e-4f,Mathf.Abs(transform.lossyScale.x));
            panel.SetPositionAndRotation(centre,Quaternion.LookRotation(look.normalized,up));panel.localScale=Vector3.one*scale*inv;
        }
        // Letter heights fixed in metres whatever the font (the localizer swaps it), the plate sized to the words. One step
        // a frame toward the target: a TextMesh's bounds can lag its text by a frame.
        // The localizer swaps the font (and so the material) per locale: keep a late-queue copy of whichever is shown.
        static void OnTop(TextMesh t,ref Material m)
        {
            var f=t.font;if(!f || !f.material)return;var r=t.GetComponent<MeshRenderer>();
            if(!m || m.mainTexture!=f.material.mainTexture || m.shader!=f.material.shader){if(m)Destroy(m);m=new Material(f.material){name="Plan banner text",renderQueue=TextQueue};}
            if(r.sharedMaterial!=m)r.sharedMaterial=m;
        }
        void Fit()
        {
            OnTop(title,ref titleMaterial);OnTop(line,ref lineMaterial);
            bool withLine=line.gameObject.activeSelf;
            float width=0;
            FitOne(title,TitleHeight,ref width);
            if(withLine)FitOne(line,LineHeight,ref width);
            float h=withLine?TitleHeight+LineGap+LineHeight:TitleHeight;
            var next=new Vector2(width+Pad*2,h+Pad*2);
            float y=h*.5f;
            title.transform.localPosition=new Vector3(0,y-TitleHeight*.5f,0);y-=TitleHeight+LineGap;
            if(withLine)line.transform.localPosition=new Vector3(0,y-LineHeight*.5f,0);
            if((next-Size).sqrMagnitude<1e-7f)return;
            Size=next;
            RoundedRect(fillMesh,Size.x,Size.y,Corner);RoundedRect(rimMesh,Size.x+Border*2,Size.y+Border*2,Corner+Border);
        }
        static void FitOne(TextMesh t,float height,ref float width)
        {
            var size=t.GetComponent<Renderer>().localBounds.size;
            if(size.y>1e-6f && size.x>1e-6f)
            {
                float s=Mathf.Min(height/size.y,MaxWidth/size.x);
                if(Mathf.Abs(s-1)>.005f){t.characterSize*=s;size*=s;}
            }
            width=Mathf.Max(width,size.x);
        }
        const float Corner=.008f;
        // Rounded rectangle in the XY plane, centred, both faces (the eye looks along +Z at its -Z side).
        static void RoundedRect(Mesh m,float w,float h,float r)
        {
            r=Mathf.Min(r,w*.5f,h*.5f);
            var v=new List<Vector3>{Vector3.zero};var t=new List<int>();const int arc=6;
            Vector2[] corners={new Vector2(w*.5f-r,h*.5f-r),new Vector2(-w*.5f+r,h*.5f-r),new Vector2(-w*.5f+r,-h*.5f+r),new Vector2(w*.5f-r,-h*.5f+r)};
            for(int c=0;c<4;c++)for(int i=0;i<=arc;i++)
            {
                float a=(c+i/(float)arc)*Mathf.PI*.5f;v.Add(new Vector3(corners[c].x+Mathf.Cos(a)*r,corners[c].y+Mathf.Sin(a)*r,0));
            }
            int n=v.Count-1;for(int i=0;i<n;i++){int a=1+i,b=1+(i+1)%n;t.Add(0);t.Add(b);t.Add(a);t.Add(0);t.Add(a);t.Add(b);}
            m.Clear();m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();
        }
        void OnDestroy(){if(plate)Destroy(plate);if(titleMaterial)Destroy(titleMaterial);if(lineMaterial)Destroy(lineMaterial);if(fillMesh)Destroy(fillMesh);if(rimMesh)Destroy(rimMesh);}
    }
}

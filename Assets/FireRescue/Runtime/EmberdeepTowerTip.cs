using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FireRescue
{
    // Headset test 3 (2026-11-22): towers arrive on TOWER HILL and nothing said what they are. A short tip floats over the
    // tray's first tower in Plan, the first time towers appear: TOWER, how to place it, what it does (LoopSpec §13 prompt
    // voice; Plan is not a fight, and the tip never shows once the fire runs). It goes once a tower is set on the board
    // (MarkDone) and comes back only with the first-time hints (EmberdeepFirstHints.ResetAll: SHOW HINTS, HOW TO PLAY),
    // under the hints' key version. Presentation only: FireRescueGame.Pieces decides when and where.
    // World-upright and turned to the eye, so it reads the same on the flat and the tilted board.
    [DefaultExecutionOrder(10020)] // after EmberdeepLocalizedText swaps the words, so the fit measures the shown text
    public sealed class EmberdeepTowerTip : MonoBehaviour
    {
        public const string Title="TOWER",Line1="Pick it up and set it near a fire.",Line2="It rains once when fire gets close.";
        // m: letter heights (the rendered line box), gaps, padding, the border, how far over the tower top it hangs, widest line.
        public const float TitleHeight=.018f,LineHeight=.013f,LineGap=.003f,Pad=.008f,Border=.0016f,Lift=.028f,MaxWidth=.22f,StemWidth=.0016f;
        public const float AppearSeconds=.25f,ClearanceRate=6; // 1/s: how fast it rises or settles when something nearer moves into its way

        // ---- Seen / reset (PlayerPrefs, beside the first-time hints' keys) -----------------------------------------
        public static string Key=>"Emberdeep.Hint."+EmberdeepFirstHints.KeyVersion+".TowerTip";
        public static bool Needed=>PlayerPrefs.GetInt(Key,0)==0;
        public static void MarkDone(){if(!Needed)return;PlayerPrefs.SetInt(Key,1);PlayerPrefs.Save();}
        static void OnHintsReset(){PlayerPrefs.DeleteKey(Key);PlayerPrefs.Save();}
        // SHOW HINTS lives in the camp scene, where no fireground exists: listen from load, not from an instance.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Hook(){EmberdeepFirstHints.Reset-=OnHintsReset;EmberdeepFirstHints.Reset+=OnHintsReset;}
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod] static void HookInEditor()=>Hook();
#endif

        Transform panel,stem;TextMesh title,line1,line2;Renderer fill,rim,stemRenderer;
        Material plate;Mesh fillMesh,rimMesh;MaterialPropertyBlock block;
        float shown,clearance,clearanceTarget;Vector3 anchor;Camera eye;
        // Extra height over Lift (m) right now: the game raises the tip so nothing nearer the eye (the pump) hides it.
        public float Clearance=>clearance;
        public bool Visible=>panel && panel.gameObject.activeSelf;
        public TextMesh TitleText=>title;
        public TextMesh[] Lines=>new[]{line1,line2};
        // Panel size (m) after the fit, and its world corners for tests (bottom-left, top-left, top-right, bottom-right).
        public Vector2 Size {get;private set;}
        public void Corners(Vector3[] four)
        {
            var h=new Vector3(Size.x*.5f,Size.y*.5f,0);
            four[0]=panel.TransformPoint(new Vector3(-h.x,-h.y,0));four[1]=panel.TransformPoint(new Vector3(-h.x,h.y,0));
            four[2]=panel.TransformPoint(new Vector3(h.x,h.y,0));four[3]=panel.TransformPoint(new Vector3(h.x,-h.y,0));
        }
        public Vector3 Center=>panel?panel.position:transform.position;
        public Vector3 Facing=>panel?panel.forward:Vector3.forward;

        void Ensure()
        {
            if(panel)return;
            block=new MaterialPropertyBlock();
            panel=new GameObject("Tower tip panel").transform;panel.SetParent(transform,false);
            var source=Resources.Load<Material>("EmberdeepMenuUnlit");
            plate=source?new Material(source){name="Tower tip plate"}:new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Tower tip plate"};
            fillMesh=new Mesh{name="Tower tip plate"};rimMesh=new Mesh{name="Tower tip rim"};
            rim=Quad("Tip rim",panel,rimMesh,.002f);fill=Quad("Tip plate",panel,fillMesh,.001f);
            title=Text("Tip title",Title,new Color(1,.83f,.42f));line1=Text("Tip line 1",Line1,new Color(.97f,.94f,.86f));line2=Text("Tip line 2",Line2,new Color(.97f,.94f,.86f));
            var s=GameObject.CreatePrimitive(PrimitiveType.Cube);s.name="Tip stem";var c=s.GetComponent<Collider>();c.enabled=false;Destroy(c);
            stem=s.transform;stem.SetParent(transform,false);stemRenderer=s.GetComponent<Renderer>();stemRenderer.sharedMaterial=plate;stemRenderer.shadowCastingMode=ShadowCastingMode.Off;
            Tint(rim,new Color(.78f,.6f,.27f));Tint(fill,new Color(.12f,.09f,.07f));Tint(stemRenderer,new Color(.78f,.6f,.27f));
            panel.gameObject.SetActive(false);stem.gameObject.SetActive(false);
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

        // Shows the tip hanging over anchor (world: the tower's top), turned to the eye, raised by extra (m) on top of Lift.
        public void Show(Vector3 at,Camera view,float extra=0)
        {
            Ensure();anchor=at;eye=view;clearanceTarget=Mathf.Max(0,extra);
            if(!panel.gameObject.activeSelf){panel.gameObject.SetActive(true);stem.gameObject.SetActive(true);shown=0;clearance=clearanceTarget;}
            clearance=Mathf.Lerp(clearance,clearanceTarget,1-Mathf.Exp(-Time.unscaledDeltaTime*ClearanceRate));
            Place(Time.unscaledDeltaTime);
        }
        public void Hide()
        {
            if(panel && panel.gameObject.activeSelf){panel.gameObject.SetActive(false);stem.gameObject.SetActive(false);}
        }
        void LateUpdate(){if(Visible){Fit();Place(0);}}
        void Place(float dt)
        {
            shown=Mathf.Min(1,shown+dt/AppearSeconds);float pop=Mathf.SmoothStep(.85f,1,shown);
            var up=Vector3.up;float half=Mathf.Max(Size.y,.04f)*.5f;
            var centre=anchor+up*(Lift+clearance+half);
            var look=eye?centre-eye.transform.position:Vector3.forward;
            if(look.sqrMagnitude<1e-6f)look=Vector3.forward;
            // Sizes are metres: undo the parent's scale (the tip hangs under the arena, which is scaled to the miniature).
            float inv=1/Mathf.Max(1e-4f,Mathf.Abs(transform.lossyScale.x));
            // Faces the eye, rolled upright: the eye looks along +Z, so the TextMesh reads the right way round.
            panel.SetPositionAndRotation(centre,Quaternion.LookRotation(look.normalized,up));panel.localScale=Vector3.one*pop*inv;
            var bottom=panel.TransformPoint(new Vector3(0,-Size.y*.5f,.002f));var d=anchor-bottom;float length=d.magnitude;
            stem.gameObject.SetActive(length>.002f);
            if(length>.002f){stem.SetPositionAndRotation((anchor+bottom)*.5f,Quaternion.FromToRotation(Vector3.up,d/length));stem.localScale=new Vector3(StemWidth,length,StemWidth)*inv;}
        }
        // Letter heights fixed in metres whatever the font (the localizer swaps it), the plate sized to the words. One step
        // a frame toward the target: a TextMesh's bounds can lag its text by a frame, so this converges instead of overshooting.
        void Fit()
        {
            var labels=new[]{title,line1,line2};var heights=new[]{TitleHeight,LineHeight,LineHeight};
            float width=0;
            for(int i=0;i<3;i++)
            {
                var t=labels[i];var size=t.GetComponent<Renderer>().localBounds.size;
                if(size.y>1e-6f && size.x>1e-6f)
                {
                    float s=Mathf.Min(heights[i]/size.y,MaxWidth/size.x);
                    if(Mathf.Abs(s-1)>.005f){t.characterSize*=s;size*=s;}
                }
                width=Mathf.Max(width,size.x);
            }
            float h=TitleHeight+LineGap*2+LineHeight*2;
            var next=new Vector2(width+Pad*2,h+Pad*2);
            float y=h*.5f;
            title.transform.localPosition=new Vector3(0,y-TitleHeight*.5f,0);y-=TitleHeight+LineGap*2;
            line1.transform.localPosition=new Vector3(0,y-LineHeight*.5f,0);y-=LineHeight+LineGap;
            line2.transform.localPosition=new Vector3(0,y-LineHeight*.5f,0);
            if((next-Size).sqrMagnitude<1e-7f)return;
            Size=next;
            RoundedRect(fillMesh,Size.x,Size.y,Corner);RoundedRect(rimMesh,Size.x+Border*2,Size.y+Border*2,Corner+Border);
        }
        const float Corner=.006f;
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
        void OnDestroy(){if(plate)Destroy(plate);if(fillMesh)Destroy(fillMesh);if(rimMesh)Destroy(rimMesh);}
    }
}

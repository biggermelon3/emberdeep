using System;
using System.Collections;
using System.Collections.Generic;
using CloudGarden.Integration;
using CloudGarden.ShadowPuzzle;
using Oculus.Interaction;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShapeOfMemory
{
    // The book the game is played in, in one of two layouts (Layout):
    // - Standing: a giant hardback standing open at a right angle on the table, its two pages exactly where the lamps'
    //   walls are (Torch2Projection), so the shadows fall on the pages and every level's picture still fits. Pinch a
    //   page's bottom outer corner with the hand ray and draw it across: the corner follows the hand (Reach of hand travel
    //   takes it all the way). Its points rise up to Lift as they swing, since its bottom corner passes over the console.
    // - Flat: the same book at FlatScale, lying open on the table in front of the orb (the shadows fall on the room's own
    //   walls). Pinch a page anywhere along its outer edge with the hand itself and the page comes up by that point and
    //   follows the hand round the spine.
    // Either way the page bends after the hand, and let go it turns or falls back by how far and how fast it was going
    // (PageTurn); a tap (or a click, or a ray pinch on the flat book) turns it by itself. The host fills a spread's two
    // pages (Fill); the page being turned is a bendable paper sheet (PageSheet) printed with the page it leaves on one
    // side and the next on the other, built without buttons (live = false), and it shades the pages it passes over.
    public sealed class MemoryBook : MonoBehaviour
    {
        public enum Layout{Standing,Flat}
        public const float RiffleSeconds=.26f,Lift=.07f,Reach=.30f,Block=.028f,Cover=.012f,Overhang=.022f,Ear=.10f;
        // The flat book: its scale, and where its spine's near end lies (stage space), the covers on the table top.
        public const float FlatScale=.31f;
        public static Vector3 FlatPlace=>new Vector3(0,Torch2Transit.TableTop+(.002f+Block+Cover)*FlatScale,-.45f);
        public static readonly Color Leather=new Color(.30f,.12f,.11f),Edge=new Color(.86f,.80f,.68f),PaperTone=new Color(.97f,.93f,.84f);
        // A page's size, in the book's own units (the flat book scales them down).
        public static float Width=>Torch2Room.WallSize.x;
        public static float Height=>Torch2Room.WallSize.y;

        public Layout Mode{get;private set;}
        public BookShape Shape{get;private set;}
        public int Spread{get;private set;}
        public int Count;
        public bool Turning=>flip!=null;
        public bool Dragging=>flip!=null&&(flip.hand>=0||flip.direct);
        // Builds one page: spread, right page or left, the page's content root (place things at (u - Width/2, v -
        // Height/2, z) with -z toward the reader), and whether its buttons should work.
        public Action<int,bool,Transform,bool> Fill;
        // After a page the player turned lands (TurnTo has its own done instead).
        public Action<int> Turned;
        // Whether a corner may turn the page now (forward = the right page's), and whether it calls attention to itself.
        public Func<bool,bool> Open,Beckon;
        // A press on a corner the host handles itself (returns true), e.g. leaving a solved level before the page turns.
        public Func<bool,bool> Pressed;
        public Transform LeftFace=>faces[0];
        public Transform RightFace=>faces[1];
        public Pressable Corner(bool forward)=>corners[forward?1:0].press;

        MetaShadowPlayer xr;
        MemoryKit kit;
        MemorySounds sounds;
        AudioSource voice,rustle;
        Func<bool> handsAllowed;
        // Standing pages: left 0, right 1. Their paper, and the print roots the turning page's two sides are built on.
        readonly Transform[] faces=new Transform[2],prints=new Transform[2];
        readonly List<Renderer> paper=new List<Renderer>();
        PageSheet sheet;
        float brightness=1;
        sealed class PageCorner{public Transform root;public Pressable press;public Renderer fold;public AimZone zone;public float lift;}
        readonly PageCorner[] corners=new PageCorner[2];
        sealed class Flip
        {
            public bool forward,last,player;public int from,to;public readonly PageTurn turn=new PageTurn();
            // How much faster than real time it runs (the blank pages of a riffle).
            public float rate=1;
            // The hand drawing it with its ray (-1: turning by itself), where and when its pinch started, and how far it
            // has drawn the corner across (metres, negative the wrong way).
            public int hand=-1;public Vector3 start;public float began,along;public Pressable press;public Action done;
            // Held in the hand itself (flat book): by which hand (-1: Grab called directly), how far up the edge (0 the
            // near end .. 1 the far end; the row that leads), and how far the hand has moved since (metres).
            public bool direct;public int grabber=-1;public float grab,moved;
        }
        Flip flip;

        public static MemoryBook Build(Transform stage,MemoryKit kit,MemorySounds sounds,MetaShadowPlayer xr,Func<bool> handsAllowed,Layout layout=Layout.Standing)
        {
            var go=new GameObject("Book");go.transform.SetParent(stage,false);var book=go.AddComponent<MemoryBook>();
            book.kit=kit;book.sounds=sounds;book.xr=xr;book.handsAllowed=handsAllowed;book.Mode=layout;
            book.Shape=layout==Layout.Flat?BookShape.Flat:BookShape.Standing;
            if(layout==Layout.Flat){go.transform.localPosition=FlatPlace;go.transform.localScale=Vector3.one*FlatScale;}
            book.Make();return book;
        }
        void Make()
        {
            voice=gameObject.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=0;
            rustle=gameObject.AddComponent<AudioSource>();rustle.playOnAwake=false;rustle.spatialBlend=0;rustle.loop=true;rustle.volume=0;rustle.clip=sounds?.Rustle;
            float w=Width,h=Height;var right=Paper(true);var left=Paper(false);bool flat=Mode==Layout.Flat;
            // Two page blocks behind the paper, the covers behind them (overhanging the outer edge and the top, and lying
            // flat the near edge too), the spine round the corner.
            for(int k=0;k<2;k++)
            {
                var page=Shape.Page(k==1);float outward=k==1?1:-1,overY=flat?0:Overhang*.5f,overH=flat?Overhang*2:Overhang;
                var frame=new GameObject(k==1?"Right page":"Left page").transform;frame.SetParent(transform,false);
                frame.localPosition=page.position+page.rotation*new Vector3(w*.5f,h*.5f,0);frame.localRotation=page.rotation;
                paper.Add(MemoryUi.Picture("Paper",frame,new Vector3(0,0,.002f),Quaternion.identity,new Vector2(w,h),kit.Screen,k==1?right:left,PaperTone,kit.Owned));
                MemoryUi.Shape(PrimitiveType.Cube,"Page block",frame,new Vector3(0,0,.002f+Block*.5f),Quaternion.identity,new Vector3(w,h,Block),kit.Surface,Edge);
                MemoryUi.Shape(PrimitiveType.Cube,"Cover",frame,new Vector3(outward*Overhang*.5f,overY,.002f+Block+Cover*.5f),Quaternion.identity,new Vector3(w+Overhang,h+overH,Cover),kit.Surface,Leather);
                // Gilt rule along the cover's outer edge, seen past the paper.
                MemoryUi.Shape(PrimitiveType.Cube,"Cover rule",frame,new Vector3(outward*(w*.5f+Overhang*.55f),overY,.002f+Block-.001f),Quaternion.identity,new Vector3(.006f,h+overH*.4f,.004f),kit.Surface,MemoryUi.Gold);
                faces[k]=new GameObject("Face").transform;faces[k].SetParent(frame,false);faces[k].localPosition=new Vector3(0,0,-.001f);
                // Where the turning page's side for this page is built (lit as the page is) before it is printed.
                prints[k]=new GameObject("Print").transform;prints[k].SetParent(frame,false);prints[k].localPosition=faces[k].localPosition;prints[k].gameObject.SetActive(false);
                corners[k]=flat?MakeEdge(frame,k==1):MakeEar(frame,k==1);
            }
            // The spine: behind the corner the pages meet at, along the page's height.
            var behind=-(Shape.Facing(true)+Shape.Facing(false)).normalized;float length=h+(flat?Overhang*2:Overhang);
            MemoryUi.Shape(PrimitiveType.Cylinder,"Spine",transform,Shape.Corner+behind*(Block+Cover)*.85f+Shape.Up*(flat?h*.5f:length*.5f),Quaternion.FromToRotation(Vector3.up,Shape.Up),
                new Vector3((Block+Cover)*2.1f,length*.5f,(Block+Cover)*2.1f),kit.Surface,Leather);
            // The turning page: its front is a right page, its back a left page.
            sheet=new PageSheet(transform,kit,Shape,right,left);paper.Add(sheet.Renderer);MemoryUi.Tint(sheet.Renderer,PaperTone);
        }
        // Cream paper with a faint grain, a thin margin rule and the gutter's shade on the spine side.
        Texture2D Paper(bool right)
        {
            const int W=256,H=240;var t=new Texture2D(W,H,TextureFormat.RGBA32,true){name=right?"Book right paper":"Book left paper",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=4};kit.Owned.Add(t);
            var pixels=new Color32[W*H];var random=new System.Random(right?11:12);
            for(int y=0;y<H;y++)for(int x=0;x<W;x++)
            {
                float u=(x+.5f)/W,v=(y+.5f)/H,gutter=right?u:1-u;
                float shade=1-.22f*Mathf.Exp(-gutter*18)-.05f*Mathf.Exp(-(1-gutter)*30)-.04f*Mathf.Exp(-v*40)-.04f*Mathf.Exp(-(1-v)*40);
                float grain=1+((float)random.NextDouble()-.5f)*.035f;
                float rule=Mathf.Abs(gutter-.06f)<.002f||Mathf.Abs(gutter-.955f)<.002f||Mathf.Abs(v-.045f)<.002f||Mathf.Abs(v-.955f)<.002f?.86f:1;
                float c=Mathf.Clamp01(shade*grain*rule);pixels[y*W+x]=new Color(c,c*.985f,c*.96f,1);
            }
            t.SetPixels32(pixels);t.Apply(true,true);return t;
        }
        // The bottom outer corner, folded over: the ray aims at it (AimZone), a pinch or click turns the page.
        PageCorner MakeEar(Transform frame,bool right)
        {
            float w=Width,h=Height,s=right?1:-1;var ear=new PageCorner();
            ear.root=new GameObject(right?"Corner (turn forward)":"Corner (turn back)").transform;ear.root.SetParent(frame,false);
            ear.root.localPosition=new Vector3(s*(w*.5f-Ear*.5f),-h*.5f+Ear*.5f,-.003f);
            // A triangle at the very corner, the paper's underside showing.
            var mesh=new Mesh{name="Page corner fold"};kit.Owned.Add(mesh);float e=Ear*.5f;
            mesh.vertices=new[]{new Vector3(s*e,-e,0),new Vector3(s*(e-Ear*.8f),-e,0),new Vector3(s*e,-e+Ear*.8f,0)};mesh.triangles=new[]{0,1,2,0,2,1};mesh.RecalculateBounds();
            var fold=new GameObject("Fold",typeof(MeshFilter),typeof(MeshRenderer));fold.transform.SetParent(ear.root,false);fold.GetComponent<MeshFilter>().sharedMesh=mesh;
            var material=new Material(kit.Screen){name="Page corner"};material.SetFloat("_Cull",0);kit.Owned.Add(material);ear.fold=fold.GetComponent<MeshRenderer>();ear.fold.sharedMaterial=material;ear.fold.shadowCastingMode=ShadowCastingMode.Off;
            var hit=new GameObject("Press",typeof(BoxCollider));hit.transform.SetParent(ear.root,false);hit.transform.localScale=new Vector3(Ear*1.6f,Ear*1.6f,.01f);
            ear.press=Pressable.Add(hit,()=>Press(right),()=>Ready(right),handsAllowed,new Vector3(0,0,-.51f),Quaternion.identity);
            ear.zone=ear.root.gameObject.AddComponent<AimZone>();ear.zone.Box=new Bounds(Vector3.zero,new Vector3(Ear*2.2f,Ear*2.2f,.14f));
            return ear;
        }
        // The flat book's turning edge: a gilt line down the page's whole outer edge. A hand pinches anywhere along it
        // (Hands); a ray pinch, a poke from afar or a click on it turns the page by itself.
        PageCorner MakeEdge(Transform frame,bool right)
        {
            float w=Width,h=Height,s=right?1:-1;var edge=new PageCorner();
            edge.root=new GameObject(right?"Edge (turn forward)":"Edge (turn back)").transform;edge.root.SetParent(frame,false);
            edge.root.localPosition=new Vector3(s*(w*.5f-Ear*.3f),0,-.003f);
            edge.fold=MemoryUi.Shape(PrimitiveType.Quad,"Line",edge.root,new Vector3(s*Ear*.22f,0,0),Quaternion.identity,new Vector3(.018f,h*.96f,1),kit.Screen,MemoryUi.Gold).GetComponent<Renderer>();
            var hit=new GameObject("Press",typeof(BoxCollider));hit.transform.SetParent(edge.root,false);hit.transform.localScale=new Vector3(Ear*.9f,h,.03f);
            edge.press=Pressable.Add(hit,()=>Press(right),()=>Ready(right),handsAllowed,new Vector3(0,0,-.51f),Quaternion.identity);
            edge.zone=edge.root.gameObject.AddComponent<AimZone>();edge.zone.Box=new Bounds(Vector3.zero,new Vector3(Ear*1.4f,h+Ear,.14f));
            return edge;
        }
        bool Ready(bool forward)=>flip==null&&(forward?Spread<Count-1:Spread>0)&&(Open==null||Open(forward));
        public void SetBrightness(float value){brightness=Mathf.Clamp01(value);foreach(var r in paper)MemoryUi.Tint(r,PaperTone*brightness);}
        public float Brightness=>brightness;

        // ---- Spreads
        // Shows a spread at once, both pages live.
        public void Show(int spread){StopAllCoroutines();EndFlip();Spread=Mathf.Clamp(spread,0,Mathf.Max(0,Count-1));Refill();}
        // Rebuilds the open spread's pages (after the host's state changed).
        public void Refill(){Build(faces[0],Spread,false,true);Build(faces[1],Spread,true,true);Corners();}
        void Build(Transform face,int spread,bool right,bool live)
        {
            for(int i=face.childCount-1;i>=0;i--){var child=face.GetChild(i).gameObject;child.SetActive(false);Destroy(child);}
            if(spread>=0&&spread<Count)Fill?.Invoke(spread,right,face,live);
        }
        // Turns to a spread: one page, or a riffle through several (the pages between stay blank). done runs on landing.
        public void TurnTo(int spread,Action done=null)=>StartCoroutine(Riffle(Mathf.Clamp(spread,0,Mathf.Max(0,Count-1)),done));
        IEnumerator Riffle(int to,Action done)
        {
            while(flip!=null)yield return null;
            bool forward=to>Spread;int steps=Mathf.Abs(to-Spread);
            for(int i=0;i<steps;i++)
            {
                int from=Spread,next=from+(forward?1:-1);bool first=i==0,last=i==steps-1;
                Begin(forward,from,next,first,last);flip.turn.Auto();flip.rate=last?1:PageTurn.AutoSeconds/RiffleSeconds;
                if(i==0)Say(sounds?.Crinkle,.9f,.16f);
                while(flip!=null)yield return null;
            }
            done?.Invoke();
        }
        // Lays the turning page over the page it leaves: front (right page) and back (left page) printed, the page beneath
        // it showing what the turn uncovers. first/last: whether the leaving and the arriving pages are real or blank.
        void Begin(bool forward,int from,int to,bool first=true,bool last=true)
        {
            flip=new Flip{forward=forward,from=from,to=to,last=last};flip.turn.Finish=Mode==Layout.Flat?PageTurn.FlatFinish:PageTurn.StandingFinish;
            int rightSpread=forward?from:to,leftSpread=forward?to:from;bool rightReal=forward?first:last,leftReal=forward?last:first;
            Print(1,rightReal?rightSpread:-1);Print(0,leftReal?leftSpread:-1);
            // The page beneath: forward, the right page of the next spread; back, the left page of the previous one. The page
            // it lands on stays; if it has tiles, it is built again without buttons so they lie flat under the landing sheet.
            if(forward)Build(faces[1],last?to:-1,true,false);else Build(faces[0],last?to:-1,false,false);
            int under=forward?0:1;if(faces[under].GetComponentInChildren<MemoryTile>())Build(faces[under],first?from:-1,under==1,false);
            sheet.Show(true);Pose();
            if(rustle.clip){rustle.volume=0;rustle.Play();}
        }
        void Print(int side,int spread){prints[side].gameObject.SetActive(true);Build(prints[side],spread,side==1,false);sheet.Print(prints[side],side);}
        void Pose()=>sheet.Pose(flip.turn.Root,flip.turn.Tip,flip.forward,flip.grab);
        // Landed on the far side, or fallen back.
        void Land()
        {
            var f=flip;bool turned=f.turn.Goal==1;
            if(turned)
            {
                Spread=f.to;
                // Mid-riffle the faces stay blank until the last page lands.
                if(f.last)Refill();else{Build(f.forward?faces[0]:faces[1],-1,!f.forward,false);}
            }
            else Refill();
            EndFlip();
            var done=f.done;done?.Invoke();
            if(turned&&f.player)Turned?.Invoke(Spread);
        }
        void EndFlip(){flip=null;sheet?.Show(false);if(rustle)rustle.Stop();}

        // ---- Corners
        void Press(bool forward)
        {
            if(flip!=null)return;
            // On the flat book a hand at an edge takes the page itself (Hands); its poke or pinch there is not a press.
            if(Mode==Layout.Flat&&(handAt[0]||handAt[1]))return;
            if(Pressed!=null&&Pressed(forward))return;
            int to=Spread+(forward?1:-1);
            // The hand whose ray pinched this corner draws the page; a click or a poke turns it by itself, as anything
            // does on the flat book.
            int hand=-1;var target=corners[forward?1:0].root;
            if(xr&&xr.Active&&Mode==Layout.Standing)foreach(var (ray,rayHand) in xr.HandRays())
                if(ray.State==InteractorState.Select&&ray.Interactable is Component c&&c&&c.transform.IsChildOf(target)){hand=rayHand.Handedness==Oculus.Interaction.Input.Handedness.Left?0:1;break;}
            Begin(forward,Spread,to);flip.player=true;Say(sounds?.Crinkle,1,.2f);
            if(hand>=0&&xr.ReadHand(hand==0,out _,out var tip,out _)){flip.hand=hand;flip.start=transform.parent.InverseTransformPoint(tip);flip.began=Time.unscaledTime;flip.press=corners[forward?1:0].press;flip.turn.Hold(0);}
            else flip.turn.Auto();
        }
        // Tests and captures: hold a turn where it is, or show it as a tapped turn is at p of its way (0 = the page it
        // leaves, 1 = landed).
        public bool Frozen;
        public void Seek(float p){if(flip==null)return;PageTurn.Sample(p,out flip.turn.Root,out flip.turn.Tip);Pose();}
        void Update(){if(!Frozen)Hands();if(flip!=null&&!Frozen)Step();Corners();}

        // ---- Hands on the flat book
        // A pinch (thumb to index tip, metres; it lets go a little wider than it takes) that starts at a page's outer edge
        // takes the page by that point. How near the edge counts, in page units: in over the page, out past the edge, up
        // off it.
        public const float PinchOn=.022f,PinchOff=.04f,ReachIn=.32f,ReachOut=.22f,ReachUp=.45f;
        readonly bool[] pinching=new bool[2],handAt=new bool[2],sideNear=new bool[2];
        // Whether a hand is at the flat book's edge (its ray is hidden then: the hand is about to take the page).
        public bool HandAt(bool left)=>handAt[left?0:1];
        void Hands()
        {
            for(int k=0;k<2;k++)handAt[k]=sideNear[k]=false;
            if(Mode!=Layout.Flat||!xr||!xr.Active)return;
            for(int hand=0;hand<2;hand++)
            {
                bool held=flip!=null&&flip.direct&&flip.grabber==hand;
                if(!xr.ReadHand(hand==0,out _,out var tip,out float pinch)){pinching[hand]=false;if(held)Drop();continue;}
                bool was=pinching[hand];pinching[hand]=pinch<(was?PinchOff:PinchOn);
                int side=EdgeAt(tip);if(side>=0){handAt[hand]=true;sideNear[side]=true;}
                if(held){if(pinching[hand])Drag(tip);else Drop();continue;}
                if(pinching[hand]&&!was&&side>=0&&(handsAllowed==null||handsAllowed())&&Grab(tip)&&flip!=null&&flip.direct)flip.grabber=hand;
            }
        }
        // Which page's outer edge a point (world space) is at: 1 the right page's (it turns forward), 0 the left page's,
        // -1 neither.
        public int EdgeAt(Vector3 point)
        {
            var p=transform.InverseTransformPoint(point)-Shape.Corner;float up=Vector3.Dot(p,Shape.Up);
            if(up<-ReachOut||up>Height+ReachOut)return -1;
            for(int k=1;k>=0;k--)
            {
                float across=Vector3.Dot(p,Shape.Direction(k==1)),off=Vector3.Dot(p,Shape.Facing(k==1));
                if(across>Width-ReachIn&&across<Width+ReachOut&&off>-.1f&&off<ReachUp)return k;
            }
            return -1;
        }
        // Takes the page whose edge is at point (world space) by that point, if it may turn. Returns whether it did (a
        // press the host handles itself counts).
        public bool Grab(Vector3 point)
        {
            int side=EdgeAt(point);if(side<0||flip!=null)return false;bool forward=side==1;
            if(!Ready(forward))return false;
            if(Pressed!=null&&Pressed(forward))return true;
            Begin(forward,Spread,Spread+(forward?1:-1));flip.player=true;flip.direct=true;flip.start=point;flip.began=Time.unscaledTime;
            flip.grab=Mathf.Clamp01(Vector3.Dot(transform.InverseTransformPoint(point)-Shape.Corner,Shape.Up)/Height);
            Say(sounds?.Crinkle,1,.2f);Drag(point);return true;
        }
        // The held edge goes where the hand is round the spine; the page bends after it.
        public void Drag(Vector3 point)
        {
            if(flip==null||!flip.direct)return;
            var p=transform.InverseTransformPoint(point)-Shape.Corner;
            float x=Vector3.Dot(p,Shape.Direction(flip.forward)),y=Vector3.Dot(p,Shape.Facing(flip.forward));
            float angle=Mathf.Clamp(Mathf.Atan2(Mathf.Max(0,y),x),0,Shape.Opening);
            flip.moved=Mathf.Max(flip.moved,Vector3.Distance(point,flip.start));
            flip.turn.Hold(PageBend.TipFor(angle,flip.turn.Root,Shape.Opening,flip.grab));
        }
        // Let go: a quick pinch that hardly moved turns the page by itself; otherwise it turns or falls back.
        public void Drop()
        {
            if(flip==null||!flip.direct)return;
            bool tap=Time.unscaledTime-flip.began<.3f&&flip.moved<.03f;flip.direct=false;flip.grabber=-1;
            if(tap)flip.turn.Auto();else flip.turn.Release();
        }
        // Each corner shows (and can be aimed at) only while it may turn its page.
        void Corners()
        {
            for(int k=0;k<2;k++)
            {
                // The corner being drawn stays live until it is let go.
                var ear=corners[k];bool held=flip!=null&&flip.press==ear.press,open=Ready(k==1)||held,beckon=open&&Beckon!=null&&Beckon(k==1);
                ear.root.gameObject.SetActive(open);if(!open)continue;
                // The paper itself lifts while it turns.
                ear.fold.enabled=flip==null;
                float target=ear.press.Hover||ear.press.Down||sideNear[k]?1:beckon?.55f+.45f*Mathf.Sin(Time.unscaledTime*4):.25f;ear.lift=Mathf.MoveTowards(ear.lift,target,Time.unscaledDeltaTime*3);
                var under=PaperTone*.78f*brightness;under.a=1;
                MemoryUi.Tint(ear.fold,Color.Lerp(under,Color.Lerp(PaperTone,MemoryUi.Gold,.6f),ear.lift));
                ear.fold.transform.localPosition=new Vector3(0,0,-.004f*ear.lift);
            }
        }
        void Step()
        {
            var f=flip;float dt=Time.unscaledDeltaTime;
            if(f.hand>=0&&!f.direct)
            {
                bool held=f.press&&f.press.Down;
                if(held&&xr.ReadHand(f.hand==0,out _,out var tip,out _))
                {
                    var now=transform.parent.InverseTransformPoint(tip);f.along=f.forward?f.start.x-now.x:now.x-f.start.x;f.turn.Hold(f.along/Reach);
                }
                if(!held)
                {
                    // A quick pinch (a tap or a flick) turns the page unless it pulled the corner back; a longer one leaves
                    // it to how far and how fast the page was going.
                    bool quick=Time.unscaledTime-f.began<.35f&&f.along>-.02f;f.hand=-1;
                    if(quick)f.turn.Auto();else f.turn.Release();
                }
            }
            f.turn.Advance(dt*f.rate);Pose();Sound(f,dt);
            if(f.turn.Landed)Land();
        }
        // Paper rustles as fast as the page moves; its edge slaps down when it lands.
        void Sound(Flip f,float dt)
        {
            float speed=Mathf.Clamp01(Mathf.Max(Mathf.Abs(f.turn.RootSpeed),Mathf.Abs(f.turn.TipSpeed))/4);
            rustle.volume=Mathf.MoveTowards(rustle.volume,speed*.22f,dt*1.5f);rustle.pitch=.85f+.3f*speed;
            if(f.turn.Impact>.5f)Say(sounds?.Flap,.9f+.2f*Mathf.Clamp01(f.turn.Impact/4),.1f+.25f*Mathf.Clamp01(f.turn.Impact/4));
        }
        void Say(AudioClip clip,float pitch,float volume){if(!clip)return;voice.pitch=pitch;voice.PlayOneShot(clip,volume);}
    }

    // The open book's geometry in its own space: the spine's foot (Corner), the right page's direction from the spine
    // (Along) and the way it faces (Normal, into the open book), the way up its pages (Up, along the spine), and how far
    // it opens (radians): the left page lies that far round from the right, past its facing.
    public sealed class BookShape
    {
        public readonly Vector3 Corner,Along,Normal,Up;public readonly float Opening,Lift;
        public BookShape(Vector3 corner,Vector3 along,Vector3 normal,Vector3 up,float openingDegrees,float lift)
        {Corner=corner;Along=along.normalized;Normal=normal.normalized;Up=up.normalized;Opening=openingDegrees*Mathf.Deg2Rad;Lift=lift;}
        // Standing on the lamps' walls (stage space); lying flat (the book's own space: pages along x, up the page +z).
        public static BookShape Standing=>new BookShape(Torch2Projection.Corner,Torch2Projection.WallRight[1],Torch2Projection.WallNormal[1],Vector3.up,90,MemoryBook.Lift);
        public static BookShape Flat=>new BookShape(Vector3.zero,Vector3.right,Vector3.up,Vector3.forward,180,0);
        // A page's direction from the spine, and the way it faces.
        public Vector3 Direction(bool right)=>right?Along:Mathf.Cos(Opening)*Along+Mathf.Sin(Opening)*Normal;
        public Vector3 Facing(bool right)=>right?Normal:Mathf.Sin(Opening)*Along-Mathf.Cos(Opening)*Normal;
        // A page's frame: origin at its bottom corner on the reader's left, x to the reader's right, y up the page, -z
        // toward the reader. The right page starts at the spine, the left page at its outer edge.
        public Pose Page(bool right)=>new Pose(right?Corner:Corner+Direction(false)*MemoryBook.Width,Quaternion.LookRotation(-Facing(right),Up));
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FireRescue
{
    // LoopSpec §11b layout A: the readouts live on the board itself, not on a panel.
    // - The readout strip stands just behind the plinth's BACK edge, centred and facing the eye like a scoreboard
    //   (headset test 2026-11-22: on the front lip it sat ~61 deg below eye level and was barely seen). Its bottom edge
    //   rides StripClear over the eye's sight line across the crowns of the deepest back row, so the forest never hides
    //   it, and it is StripScale larger to keep its angular size at about twice the distance.
    //   Left plank: one sheep icon each (waiting / on fire / saved / lost). Right plank: the burn meter (charred trees
    //   against the limit; it took over session B's floating meter) and the plane charge ring (hidden until L3).
    //   The gap between the planks is for the rooster weathervane (L2), which stands on the centre line of the back half.
    // - The pause token, a wooden coin on the water-hand front corner, is poked with a fingertip. It sat on the
    //   free-hand corner until the hammer pump moved there (headset fixes, 2026-11-22). Either hand pokes.
    // - The start bell, a service bell floating over the middle of the play area, is struck with a fist in Plan
    //   (headset test 2: it stood on the front corner; now its purpose and its hammer hint sit where the player looks).
    // Everything is a child of the arena, laid out from the MEASURED plinth (LayoutFor): the edges, the top and the
    // width come from the terrain's plinth meshes, re-measured every second, and the UI scale follows the board
    // width. So when the board shrinks, widens, rises or moves (options), the rim follows with no new numbers.
    // It re-lays out from the eye whenever the arena is placed. Positions are arena units; sizes inside are metres x Size.
    // The strip draws after the outcome veil so a loss never hides the burn meter that explains it.
    [DefaultExecutionOrder(10030)]
    public sealed class EmberdeepRimHud : MonoBehaviour
    {
        // Layout relative to the plinth (arena units in, out of LayoutFor). Across = fraction of the half width toward the
        // water-hand side (LayoutFor's side is still the FREE-hand sign, as callers pass it); the other offsets are metres (x Size) from the edges and the top. Token and bell reproduce the
        // tuned placement on the 11.1-unit board: token x 4.7, bell 3.0.
        public static readonly Bounds DefaultBoard=new(new Vector3(.015f,-.405f,5.95f),new Vector3(11.11f,.81f,13f));
        public const float ReferenceWidth=11.11f,MinSize=.75f,MaxSize=1.3f;
        // Strip: StripBehind metres behind the back edge; its bottom StripClear over the sight line from the eye across
        // CanopyTop (arena units over the top: the incident trees' crowns measure 2.7-2.9) at the deepest back row
        // (SightRowZ). StripScale: x Size.
        public const float StripBehind=.015f,StripClear=.008f,StripScale=1.4f,CanopyTop=2.8f;
        public static readonly float SightRowZ=EmberdeepBoardLayout.FrontRowZ+(EmberdeepBoardLayout.MaxRows-1)*EmberdeepBoardLayout.RowSpacing;
        // The compact board's front corners are rounded (EmberdeepBoardFit squeezes the apron): the token sits in from the
        // measured corner so it rests on the plinth top (at .847 / .034 it floated off it). L3's hourglass stands on the
        // same corner, further in (where the bell stood until headset test 2).
        public const float TokenAcross=.72f,TokenRaise=.0048f,TokenIn=.055f;   // on the corner of the top
        // Headset test 2: the token sank into the path and moss at that corner. It was laid at a fixed height over the
        // plinth's top, then tipped toward the eye (FaceEye), so its near edge dipped ~6.5 mm under the top, and the path
        // stones and moss there stand above the top anyway. Now FaceEye measures the highest ground under the coin
        // (GroundUnder: the terrain's own vertices within TokenGroundRadius) and lifts the tipped coin until its lowest
        // edge clears that by TokenClear; a short wooden peg (TokenPeg) holds it up, so it reads as a token on a stand.
        public const float TokenClear=.0015f,TokenGroundRadius=.03f,TokenPeg=.012f; // m
        public const float HourglassAcross=.59f,HourglassIn=.068f;             // its foot stays on the top
        // The start bell floats over the middle of the play area (headset test 2, 2026-11-22: on the front corner its
        // purpose wasn't obvious and its hammer hint played off to one side): over the centre of the level's field (its
        // trees' bounds), its base BellClear over the canopy (CanopyTop), at BellScale x the rim size. It hangs upright in the
        // world on a tilted board (FaceEye), so a fist still strikes straight down. Struck: it rings, then rises BellLift and
        // shrinks away (BellRingHold, BellLeaveSeconds) while the countdown counts where it hung.
        // Shifted aside (owner's call, 2026-11-22): dead centre, the bell (~14-24 deg down) hid the rooster weathervane's head
        // (<= ~17 deg down, same centre line) from the default seat in Plan, where the wind matters; the front row did not
        // clear it either (16.7-28 deg). So the bell keeps the field's centre unless that is within BellVaneClear (arena
        // units) of the vane's line (EmberdeepWeathervane.Anchor x), and then stands that far off it toward the water-hand
        // side (the free hand's side has the pump and the tray), still mid-depth (BellDepth: 0 front row .. 1 back row).
        // A fraction of the field's width (tried first, .35) did not clear the off-centre palm-lesson grove.
        // BellVaneClear 1.8 (14.4 cm) puts ~15 deg between their centres from the default seat; the bell's box is ~13 deg
        // wide and the vane's 7, so they part by a few degrees. EmberdeepHeadsetTest2PlayTests asserts that on every level.
        public const float BellClear=.03f,BellScale=1.2f,BellLift=.07f,BellRingHold=.45f,BellLeaveSeconds=.8f; // m, -, m, s, s
        public const float BellDepth=.5f,BellAcross=0,BellVaneClear=1.8f;
        public static readonly Vector2 DefaultField=new(0,EmberdeepBoardLayout.FrontRowZ+(EmberdeepBoardLayout.MaxRows-1)*EmberdeepBoardLayout.RowSpacing*BellDepth);
        // Where the bell hangs over this level's field (x, z): BellAcross / BellDepth into the trees' bounds, kept
        // BellVaneClear off the weathervane's line toward the water hand.
        // waterSign: +1 when the water hand is the right one (+x, LayoutFor's corner for side -1), -1 for the left.
        public static Vector2 BellOver(FireRun run,float waterSign=-1)
        {
            if(run==null || run.Trees.Length==0)return DefaultField;
            float x0=float.MaxValue,x1=float.MinValue,z0=float.MaxValue,z1=float.MinValue;
            foreach(var t in run.Trees){var p=t.Position;x0=Mathf.Min(x0,p.x);x1=Mathf.Max(x1,p.x);z0=Mathf.Min(z0,p.z);z1=Mathf.Max(z1,p.z);}
            float x=(x0+x1)*.5f+waterSign*BellAcross*(x1-x0)*.5f,vane=EmberdeepWeathervane.Anchor.x;
            x=waterSign>0?Mathf.Max(x,vane+BellVaneClear):Mathf.Min(x,vane-BellVaneClear);
            return new Vector2(x,Mathf.Lerp(z0,z1,BellDepth));
        }
        // The default seated eye in arena units, for a layout with no camera yet: the base placement (EmberdeepBoardLayout)
        // plus the default play-area offsets (EmberdeepPlayArea, -14 / +6 cm since headset test 2), flat.
        public static readonly Vector3 DefaultEye=new(0,(EmberdeepBoardLayout.EyeDrop-EmberdeepPlayArea.DefaultHeight)/FireMiniature.Scale,
            EmberdeepBoardLayout.FrontRowZ-(EmberdeepBoardLayout.EyeToFrontRow+EmberdeepPlayArea.DefaultDistance)/FireMiniature.Scale);
        public struct RimLayout {public Vector3 Strip,Token,Bell,Hourglass;public float Size,UnitsPerMetre,StripUnitsPerMetre;}
        public static RimLayout LayoutFor(Bounds board,float side)=>LayoutFor(board,side,DefaultEye);
        public static RimLayout LayoutFor(Bounds board,float side,Vector3 eye)=>LayoutFor(board,side,eye,new Vector2(board.center.x,DefaultField.y));
        // eye: the seated eye in arena units (the strip's height follows it, so a raised or pushed-back board keeps
        // the strip just over its forest). field: where the bell floats over the field (BellOver).
        public static RimLayout LayoutFor(Bounds board,float side,Vector3 eye,Vector2 field)
        {
            float halfW=board.extents.x,cx=board.center.x,front=board.min.z,back=board.max.z,top=board.max.y;
            float size=Mathf.Clamp(board.size.x/ReferenceWidth,MinSize,MaxSize),m=size/FireMiniature.Scale,sm=m*StripScale;
            float corner=-side; // token and hourglass: the water-hand front corner (the pump stands on the free-hand one)
            float stripZ=back+StripBehind*m,canopy=top+CanopyTop,bottom=canopy;
            float run=SightRowZ-eye.z;
            if(run>.5f && stripZ>SightRowZ && float.IsFinite(eye.y))bottom=eye.y+(canopy-eye.y)*(stripZ-eye.z)/run;
            bottom=Mathf.Max(bottom,top)+StripClear*m;
            if(!float.IsFinite(field.x) || !float.IsFinite(field.y))field=new Vector2(cx,DefaultField.y);
            return new RimLayout{Size=size,UnitsPerMetre=m,StripUnitsPerMetre=sm,
                Strip=new Vector3(cx,bottom+StripHeight*.5f*sm,stripZ),
                Token=new Vector3(cx+corner*halfW*TokenAcross,top+TokenRaise*m,front+TokenIn*m),
                Hourglass=new Vector3(cx+corner*halfW*HourglassAcross,top,front+HourglassIn*m),
                Bell=new Vector3(Mathf.Clamp(field.x,board.min.x,board.max.x),canopy+BellClear*m,Mathf.Clamp(field.y,front,back))};
        }
        // Degrees under eye level (eye and points in arena units). Between-level panels stay above the strip's top edge
        // and the forest's skyline (FireRescueGame.PlaceScreens).
        public static float Down(Vector3 p,Vector3 eye)=>Mathf.Atan2(eye.y-p.y,new Vector2(p.x-eye.x,p.z-eye.z).magnitude)*Mathf.Rad2Deg;
        public static float StripTopDown(Bounds board,Vector3 eye)
        {
            var l=LayoutFor(board,1,eye);return Down(l.Strip+Vector3.up*StripHeight*.5f*l.StripUnitsPerMetre,eye);
        }
        public static float SkylineDown(Bounds board,Vector3 eye)=>Down(new Vector3(eye.x,board.max.y+CanopyTop,SightRowZ),eye);
        // The plinth: the authored terrain's slate and soil, plus the moss beside them (logs and cabins have their own
        // "Moss" meshes, so only moss under the same parent counts), or the primitive island and plinth without art.
        static readonly string[] PlinthParts={"Slate","Soil","Diorama plinth","Forest island"};
        static readonly List<MeshRenderer> scan=new();static readonly HashSet<Transform> plinthParents=new();
        static bool IsPart(MeshRenderer r)
        {
            foreach(var name in PlinthParts)if(r.name.StartsWith(name,System.StringComparison.Ordinal))return true;
            return r.name.StartsWith("Moss",System.StringComparison.Ordinal) && plinthParents.Contains(r.transform.parent);
        }
        public static bool MeasurePlinth(Transform arena,out Bounds board)
        {
            board=default;bool any=false;scan.Clear();plinthParents.Clear();arena.GetComponentsInChildren(true,scan);
            foreach(var r in scan)if(r.name.StartsWith("Slate",System.StringComparison.Ordinal) || r.name.StartsWith("Soil",System.StringComparison.Ordinal))plinthParents.Add(r.transform.parent);
            foreach(var r in scan)
            {
                var filter=IsPart(r)?r.GetComponent<MeshFilter>():null;if(!filter || !filter.sharedMesh)continue;
                var local=filter.sharedMesh.bounds;
                for(int i=0;i<8;i++)
                {
                    var corner=local.center+Vector3.Scale(local.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    var p=arena.InverseTransformPoint(r.transform.TransformPoint(corner));
                    if(!any){board=new Bounds(p,Vector3.zero);any=true;}else board.Encapsulate(p);
                }
            }
            scan.Clear();plinthParents.Clear();return any;
        }
        // Strip contents in strip metres (x StripScale x Size in the world). StripGap: the centred gap for the vane.
        public const float StripHeight=.046f,StripGap=.10f,SheepPlankWidth=.19f,MeterPlankWidth=.215f;
        public const float StripWidth=SheepPlankWidth+StripGap+MeterPlankWidth;
        public const float SheepPlankX=-(StripGap+SheepPlankWidth)*.5f,MeterPlankX=(StripGap+MeterPlankWidth)*.5f;
        // The token grew 3.6 -> 4.4 cm with the poke fix (EmberdeepFingerPoke.Radius) so the coin matches what pokes.
        public const float TokenDiameter=.044f,TokenThickness=.008f;
        public const float SheepStep=.021f,SheepSize=.018f; // the flock is centred on its plank
        public const float MeterIconX=StripGap*.5f+.017f,MeterPipsX=MeterIconX+.048f,MeterCountX=MeterIconX+.09f,MeterPip=.0085f,MeterStep=.0115f,MeterRowStep=.0125f,CountHeight=.022f;
        public const int MeterRow=7;
        public const float PlaneX=MeterIconX+.175f,PlaneOuter=.017f,PlaneInner=.0118f;
        const float DividerB=MeterIconX+.152f;
        public static float SheepX(int i,int count)=>SheepPlankX+(i-(count-1)*.5f)*SheepStep;
        public const float BellTopHeight=.09f; // m above the base: the knob the fist hits
        public const float MeterPop=.35f,MeterWarnHz=2.2f;
        const int AfterVeil=3100,TextQueue=3200;
        static readonly Color SheepWaiting=new(.93f,.89f,.78f),SheepSaved=new(.45f,.95f,.55f),SheepLost=new(.14f,.12f,.11f),SheepFire=new(1,.36f,.08f),SheepFireHot=new(1,.8f,.32f);
        static readonly Color PipFree=new(.86f,.78f,.58f),PipCharred=new(.07f,.06f,.055f),PipEmber=new(1,.45f,.12f),PipWarn=new(1,.22f,.08f);
        static readonly Color PlankColor=new(.075f,.17f,.13f),LossBoard=new(.22f,.07f,.05f);
        static readonly Color Wood=new(.62f,.40f,.22f),Brass=new(.98f,.74f,.30f),Cream=new(.96f,.92f,.80f);

        FireRescueGame game;Transform arena;
        Transform root,strip,sheepRow,meterPips,token,tokenFace,tokenPeg,bell,bellBody,bellCup,bellHalo,plane;
        TextMesh meterCount,countdown;Material countMaterial,countdownMaterial;
        Renderer plank,meterPlank,planeFill,planeBack,planeIcon,tokenRenderer,tokenBars,bellRenderer,bellHaloRenderer;
        Mesh planeArc;
        readonly List<Renderer> sheep=new(),pips=new();readonly List<float> pipFilledAt=new();
        int builtLimit=-1,shownBurnt;
        Material unlit,clay;MaterialPropertyBlock block;
        Matrix4x4 placed;float side=1,measureClock;bool laidOut;Vector2 field=DefaultField;
        float ringClock=99,pressClock=99;bool tokenHover,bellHover;
        AudioSource voice;AudioClip bellClip,tockClip;

        public bool Built=>root;
        // The measured plinth in arena units, its UI scale and a counter that changes when either changes.
        public Bounds Board {get;private set;}=DefaultBoard;
        public float Size {get;private set;}=1;
        public int BoardVersion {get;private set;}
        public Transform Root=>root;
        public Transform Strip=>strip;
        public Transform Meter=>strip; // the burn meter is part of the strip now
        public Transform Token=>token;
        public Transform Bell=>bell;
        public int MeterPips=>pips.Count;
        public int MeterCharred {get;private set;}
        public int SheepIcons {get;private set;}
        public bool StripVisible=>strip && strip.gameObject.activeInHierarchy;
        public bool TokenVisible=>token && token.gameObject.activeInHierarchy;
        public bool BellVisible=>bell && bell.gameObject.activeInHierarchy;
        // Plane charge (L3 provides the plane). Hidden until PlaneAvailable.
        public bool PlaneAvailable {get;set;}
        public float PlaneCharge01 {get;set;}
        public bool PlaneReady=>PlaneAvailable && PlaneCharge01>=1;
        public bool PlaneVisible=>plane && plane.gameObject.activeInHierarchy;
        // World poke target of the token: centre of its face and the face normal.
        public Vector3 TokenCenter=>token?tokenFace.position:Vector3.zero;
        public Vector3 TokenNormal=>token?token.up:Vector3.up;
        // The knob the fist hits, at rest (the body's bob, lift and shrink never move the strike target).
        public Vector3 BellTop=>bell?bell.TransformPoint(Vector3.up*BellTopHeight*BellScale):Vector3.zero;
        // Where the bell floats over the field (BellOver), arena units.
        public Vector2 Field=>field;
        // The highest ground under the token (arena y) and the gap from it to the coin's lowest edge (m), from FaceEye.
        public float TokenGround {get;private set;}
        public float TokenGap {get;private set;}
        // 0 resting .. 1 gone: the bell's leave after a strike.
        public float BellLeave {get;private set;}
        public Vector3 BellUp=>bell?bell.up:Vector3.up;
        public Color SheepColor(int i)=>i>=0 && i<sheep.Count?CurrentSheep[i]:Color.clear;
        readonly List<Color> CurrentSheep=new();

        public void Initialize(FireRescueGame owner){game=owner;block=new MaterialPropertyBlock();}
        public void RingBell(){ringClock=0;if(voice && BellClip)voice.PlayOneShot(BellClip,.55f);}
        public void PressToken(){pressClock=0;if(voice && TockClip)voice.PlayOneShot(TockClip,.6f);}
        public void SetHover(bool bellFist,bool tokenTip){bellHover=bellFist;tokenHover=tokenTip;}

        void LateUpdate()
        {
            if(!game || !game.Arena)return;
            if((measureClock-=Time.unscaledDeltaTime)<=0){measureClock=1;Measure();}
            if(!root)
            {
                // ForestUi bakes world sizes at build, so wait for the arena's miniature scale.
                if(Mathf.Abs(game.Arena.lossyScale.x-FireMiniature.Scale)>.001f)return;
                Build();
            }
            var run=game.Run;float now=Time.unscaledTime,dt=Mathf.Min(Time.unscaledDeltaTime,.1f);
            // The level select and the upgrades stand where the strip would be seen: the rim stands down under them.
            bool live=run.SpellMode && arena.gameObject.activeInHierarchy && run.Trees.Length>0 && !game.LevelSelectOpen && !game.ShopOpen;
            bool fightOrPlan=run.Phase==FirePhase.Preparing || run.Phase==FirePhase.Fighting;
            Show(strip,live && run.Phase!=FirePhase.Ready);
            Show(token,live && fightOrPlan);Show(tokenPeg,live && fightOrPlan);
            Show(bell,live && run.Phase==FirePhase.Preparing);
            if(!live)return;
            ApplyLayout();
            var centre=BellOver(run,-side);if((centre-field).sqrMagnitude>1e-4f){field=centre;placed=Matrix4x4.zero;}
            if(placed!=arena.localToWorldMatrix)FaceEye();
            TickSheep(run,now);TickMeter(run,now);TickPlane(now);TickToken(dt);TickBell(run,dt,now);
        }
        static void Show(Transform t,bool visible){if(t && t.gameObject.activeSelf!=visible)t.gameObject.SetActive(visible);}
        void Measure()
        {
            var arenaNow=game.Arena;if(!arenaNow)return;
            if(!MeasurePlinth(arenaNow,out var b))b=DefaultBoard;
            if((b.min-Board.min).sqrMagnitude<1e-4f && (b.max-Board.max).sqrMagnitude<1e-4f)return;
            Board=b;Size=LayoutFor(b,1).Size;BoardVersion++;laidOut=false;
        }
        // Mirrors the token and bell with the hands (side = free-hand sign); FaceEye then lays everything out from the eye.
        void ApplyLayout()
        {
            float s=EmberdeepWaterHand.Left?1:-1;if(s==side && laidOut)return;
            side=s;laidOut=true;placed=Matrix4x4.zero;
        }
        int groundVersion=-1;float groundY=float.NaN;
        // The highest point of the board's own ground (terrain and plinth meshes, readable ones) within radius of xz,
        // arena units; at least floor (the measured plinth top). Rare: on layout changes only.
        static readonly List<MeshRenderer> groundScan=new();static readonly List<Vector3> groundVertices=new();
        public static float GroundUnder(Transform arena,Vector2 xz,float radius,float floor)
        {
            float best=floor,r2=radius*radius;groundScan.Clear();arena.GetComponentsInChildren(false,groundScan);
            foreach(var r in groundScan)
            {
                if(!r.enabled || !IsGround(r.transform,arena))continue;
                var f=r.GetComponent<MeshFilter>();var mesh=f?f.sharedMesh:null;if(!mesh || !mesh.isReadable)continue;
                var toArena=arena.worldToLocalMatrix*r.transform.localToWorldMatrix;
                // Quick reject: the mesh bounds' footprint in arena space.
                var b=mesh.bounds;var ab=new Bounds(toArena.MultiplyPoint3x4(b.min),Vector3.zero);
                for(int i=1;i<8;i++)ab.Encapsulate(toArena.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
                if(xz.x+radius<ab.min.x || xz.x-radius>ab.max.x || xz.y+radius<ab.min.z || xz.y-radius>ab.max.z)continue;
                mesh.GetVertices(groundVertices);
                foreach(var v in groundVertices)
                {
                    var p=toArena.MultiplyPoint3x4(v);float dx=p.x-xz.x,dz=p.z-xz.y;
                    if(dx*dx+dz*dz<=r2 && p.y>best)best=p.y;
                }
            }
            groundScan.Clear();groundVertices.Clear();return best;
        }
        // The authored terrain (EDV2_Terrain and everything under it) or the primitive plinth parts.
        static bool IsGround(Transform t,Transform arena)
        {
            for(var p=t;p && p!=arena;p=p.parent)
            {
                if(p.name.StartsWith("EDV2_Terrain",System.StringComparison.Ordinal))return true;
                foreach(var name in PlinthParts)if(p.name.StartsWith(name,System.StringComparison.Ordinal))return true;
            }
            return false;
        }
        void Place(RimLayout l)
        {
            strip.localPosition=l.Strip;token.localPosition=l.Token;bell.localPosition=l.Bell;
            strip.localScale=Vector3.one*l.StripUnitsPerMetre;token.localScale=bell.localScale=Vector3.one*l.UnitsPerMetre;
        }
        // Lays the rim out from the eye seen from the arena and aims the strip and token at it (whenever the arena moves).
        public void FaceEye()
        {
            var cam=game.Player?game.Player.ViewCamera:Camera.main;if(!cam || !root)return;
            var eye=arena.InverseTransformPoint(cam.transform.position);
            Place(LayoutFor(Board,side,eye,field));
            // Pitch only: the strip stays parallel to the edges and tips its face to the eye.
            var look=strip.localPosition-eye;look.x=0;
            strip.localRotation=Quaternion.LookRotation(look.sqrMagnitude>1e-4f?look:Vector3.forward,Vector3.up);
            // The token lies on the board but tips toward the eye; its local up is the poke normal. It stands on its peg just
            // over the highest ground under it, so the tipped near edge never cuts into the path or the moss.
            var toEye=(eye-token.localPosition).normalized;
            var normal=Vector3.Slerp(Vector3.up,toEye,.45f);
            token.localRotation=Quaternion.FromToRotation(Vector3.up,normal);
            float m=token.localScale.x,cos=Mathf.Clamp01(normal.normalized.y),sin=Mathf.Sqrt(1-cos*cos);
            if(groundVersion!=BoardVersion*2+(side>0?1:0) || float.IsNaN(groundY))
            {
                groundVersion=BoardVersion*2+(side>0?1:0);
                groundY=GroundUnder(arena,new Vector2(token.localPosition.x,token.localPosition.z),TokenGroundRadius*m,Board.max.y);
            }
            float drop=TokenDiameter*.5f*sin+TokenThickness*.5f*cos; // centre to the coin's lowest edge, m
            var p=token.localPosition;p.y=groundY+(drop+TokenClear)*m;token.localPosition=p;
            TokenGround=groundY;TokenGap=TokenClear;
            float underside=p.y-TokenThickness*.5f*cos*m,peg=Mathf.Max(.001f,underside-groundY+.002f*m);
            tokenPeg.localPosition=new Vector3(p.x,groundY-.002f*m+peg*.5f,p.z);tokenPeg.localScale=new Vector3(TokenPeg*m,peg*.5f,TokenPeg*m);
            // The bell hangs upright in the WORLD (a tilted board pitches the arena), its face to the eye: a fist strikes
            // straight down whatever the board's tilt.
            var flat=Vector3.ProjectOnPlane(cam.transform.position-bell.position,Vector3.up);
            bell.rotation=Quaternion.LookRotation(flat.sqrMagnitude>1e-6f?-flat.normalized:arena.forward,Vector3.up);
            placed=arena.localToWorldMatrix;
        }

        // ---- Sheep -------------------------------------------------------------------------------------
        void TickSheep(FireRun run,float now)
        {
            int count=run.Animals.Length;
            while(sheep.Count<count)
            {
                var r=EmberdeepUiIcons.Icon("Sheep icon "+(sheep.Count+1),sheepRow,EmberdeepUiIcons.Sheep,unlit,Vector3.zero,SheepSize);
                sheep.Add(r);CurrentSheep.Add(SheepWaiting);
            }
            SheepIcons=count;
            for(int i=0;i<sheep.Count;i++)
            {
                bool shown=i<count;if(sheep[i].gameObject.activeSelf!=shown)sheep[i].gameObject.SetActive(shown);if(!shown)continue;
                var a=run.Animals[i];Color c;float scale=1;
                if(a.State==AnimalState.Lost)c=SheepLost;
                else if(a.State==AnimalState.Rescued || a.State==AnimalState.Celebrating)c=SheepSaved;
                else if(a.OnFire){float w=.5f+.5f*Mathf.Sin(now*9+i);c=Color.Lerp(SheepFire,SheepFireHot,w);scale=1.12f+.08f*w;}
                else c=SheepWaiting;
                CurrentSheep[i]=c;Tint(sheep[i],c);
                var t=sheep[i].transform;t.localPosition=new Vector3(SheepX(i,count),0,-.004f);t.localScale=Vector3.one*SheepSize*scale;
            }
        }

        // ---- Burn meter (from EmberdeepOutcomeView) ---------------------------------------------------
        void TickMeter(FireRun run,float now)
        {
            int limit=run.BurnLimit;if(limit!=builtLimit)BuildPips(limit);
            int burnt=Mathf.Min(run.Burnt,limit);MeterCharred=burnt;
            bool lost=run.Phase==FirePhase.Lost;
            for(int i=0;i<pips.Count;i++)
            {
                bool charred=i<burnt;
                if(charred && float.IsNegativeInfinity(pipFilledAt[i]))pipFilledAt[i]=i<shownBurnt?now-MeterPop:now;
                if(!charred)pipFilledAt[i]=float.NegativeInfinity;
                float since=now-pipFilledAt[i],pop=charred?Mathf.Clamp01(1-since/MeterPop):0;
                Color color=charred?Color.Lerp(PipCharred,PipEmber,pop):PipFree;
                bool warn=!charred && i==burnt && burnt==limit-1 && run.Phase==FirePhase.Fighting;
                if(warn){float w=.5f+.5f*Mathf.Sin(now*MeterWarnHz*Mathf.PI*2);color=Color.Lerp(PipFree,PipWarn,w);}
                // §11b Lost: the meter glows full so the reason is visible.
                float glow=0;if(lost){glow=.5f+.5f*Mathf.Sin(now*4+i*.4f);color=Color.Lerp(PipEmber,new Color(1,.85f,.4f),glow*.6f);}
                Tint(pips[i],color);
                pips[i].transform.localScale=Vector3.one*MeterPip*(1+.5f*pop*pop+(warn?.12f:0)+(lost?.18f*glow:0));
            }
            shownBurnt=burnt;
            string count=$"{burnt}/{limit}";if(meterCount.text!=count)meterCount.text=count;
            KeepQueue(meterCount,ref countMaterial,"Rim burn count");Fit(meterCount,CountHeight,.055f);
            meterCount.color=burnt>=limit?PipWarn:burnt==limit-1?new Color(1,.6f,.3f):Color.white;
            block.Clear();block.SetColor("_BaseColor",lost?Color.Lerp(LossBoard,new Color(.5f,.1f,.05f),.5f+.5f*Mathf.Sin(now*4)):PlankColor);
            plank.SetPropertyBlock(block);meterPlank.SetPropertyBlock(block);
        }
        void BuildPips(int limit)
        {
            foreach(var p in pips)if(p)Destroy(p.gameObject);
            pips.Clear();pipFilledAt.Clear();builtLimit=limit;shownBurnt=0;
            int rows=Mathf.Max(1,Mathf.CeilToInt(limit/(float)MeterRow));
            for(int i=0;i<limit;i++)
            {
                int row=i/MeterRow,col=i%MeterRow,inRow=row<rows-1?MeterRow:limit-row*MeterRow;
                float width=(Mathf.Min(MeterRow,inRow)-1)*MeterStep;
                var go=new GameObject("Charred tree pip "+(i+1));go.transform.SetParent(meterPips,false);
                go.transform.localPosition=new Vector3(-width*.5f+col*MeterStep,(rows-1)*MeterRowStep*.5f-row*MeterRowStep,0);
                go.AddComponent<MeshFilter>().sharedMesh=EmberdeepCraftDetails.SoftBox;
                var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=unlit;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
                pips.Add(r);pipFilledAt.Add(float.NegativeInfinity);
            }
        }

        // ---- Plane ring ------------------------------------------------------------------------------
        float shownCharge=-1;
        void TickPlane(float now)
        {
            Show(plane,PlaneAvailable);if(!PlaneAvailable)return;
            float charge=Mathf.Clamp01(PlaneCharge01);
            if(Mathf.Abs(charge-shownCharge)>.002f){EmberdeepUiIcons.Arc(planeArc,PlaneOuter,PlaneInner,charge);shownCharge=charge;}
            bool ready=charge>=1;float w=.5f+.5f*Mathf.Sin(now*5);
            Tint(planeFill,ready?Color.Lerp(Brass,Color.white,w*.5f):new Color(.62f,.86f,1));
            Tint(planeIcon,ready?Color.Lerp(Brass,Color.white,w):new Color(.7f,.76f,.8f));
            plane.localScale=Vector3.one*(ready?1+.08f*w:1);
        }

        // ---- Token -----------------------------------------------------------------------------------
        void TickToken(float dt)
        {
            pressClock+=dt;
            float press=pressClock<.25f?Mathf.Sin(pressClock/.25f*Mathf.PI):0;
            tokenFace.localPosition=Vector3.up*(TokenThickness*.5f+.0003f);
            float lift=tokenHover?.002f:0;
            token.GetChild(0).localPosition=Vector3.up*(lift-press*.003f);
            Tint(tokenRenderer,Color.Lerp(Wood,new Color(.85f,.6f,.32f),tokenHover?.5f:press));
            Tint(tokenBars,tokenHover || press>0?Color.white:Cream);
        }

        // ---- Bell ------------------------------------------------------------------------------------
        void TickBell(FireRun run,float dt,float now)
        {
            if(!bell.gameObject.activeSelf)return;
            ringClock+=dt;
            bool counting=run.CountdownRemaining>=0;
            // Resting: a slow float. Struck (counting): rings in place for BellRingHold, then rises and shrinks away.
            float leave=counting?Mathf.Clamp01((ringClock-BellRingHold)/BellLeaveSeconds):0;leave=leave*leave*(3-2*leave);BellLeave=leave;
            bellBody.localPosition=Vector3.up*(counting?BellLift*leave:.003f*Mathf.Sin(now*2.1f));
            bellBody.localScale=Vector3.one*BellScale*(1-leave);
            bellBody.gameObject.SetActive(leave<.999f);
            float decay=Mathf.Exp(-ringClock*3.2f),wobble=ringClock<2?Mathf.Sin(ringClock*38)*decay:0;
            bellCup.localRotation=Quaternion.Euler(wobble*7,0,wobble*4);
            bellCup.localScale=Vector3.one*(1+.08f*decay*(ringClock<2?1:0));
            Tint(bellRenderer,Color.Lerp(Brass,new Color(1,.95f,.75f),ringClock<2?decay:0));
            bool halo=bellHover && !counting;
            bellHalo.gameObject.SetActive(halo || ringClock<.6f);
            float pulse=.5f+.5f*Mathf.Sin(now*6);
            bellHalo.localScale=Vector3.one*(ringClock<.6f?.088f+.1f*ringClock:.084f+.008f*pulse);
            // Opaque unlit: a ring-out expands and is gone at .6 s; a hovering fist gets a pulsing gold ring.
            Tint(bellHaloRenderer,Color.Lerp(new Color(1,.8f,.35f),Color.white,ringClock<.6f?1-ringClock/.6f:.35f*pulse));
            countdown.gameObject.SetActive(counting);
            if(counting)
            {
                string digit=Mathf.CeilToInt(run.CountdownRemaining).ToString();
                if(countdown.text!=digit)countdown.text=digit;
                KeepQueue(countdown,ref countdownMaterial,"Rim countdown");Fit(countdown,.06f,.07f);
                var cam=game.Player?game.Player.ViewCamera:Camera.main;
                if(cam)countdown.transform.rotation=Quaternion.LookRotation(countdown.transform.position-cam.transform.position,Vector3.up);
                float frac=run.CountdownRemaining-Mathf.Floor(run.CountdownRemaining);
                countdown.transform.localScale=Vector3.one*(1+.25f*Mathf.Clamp01((frac-.8f)/.2f));
            }
        }

        // ---- Build -----------------------------------------------------------------------------------
        void Build()
        {
            arena=game.Arena;
            var source=Resources.Load<Material>("EmberdeepMenuUnlit");
            unlit=source?new Material(source){name="Rim unlit",renderQueue=AfterVeil}:new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Rim unlit",renderQueue=AfterVeil};
            var clayShader=Resources.Load<Shader>("EmberdeepTutorial");
            clay=clayShader?new Material(clayShader){name="Rim clay"}:new Material(unlit){name="Rim clay"};
            root=new GameObject("Rim HUD").transform;root.SetParent(arena,false);
            Measure();field=BellOver(game.Run,EmberdeepWaterHand.Left?-1:1);var layout=LayoutFor(Board,EmberdeepWaterHand.Left?1:-1,DefaultEye,field);
            float metres=layout.UnitsPerMetre; // ForestUi bakes the plank's world size, so build at the final scale
            // Strip: two planks (sheep | meter + plane) either side of the vane's gap
            strip=new GameObject("Rim readout strip").transform;strip.SetParent(root,false);
            strip.localPosition=layout.Strip;strip.localScale=Vector3.one*layout.StripUnitsPerMetre;
            plank=Plank("Rim strip board",SheepPlankX,SheepPlankWidth);meterPlank=Plank("Rim strip meter board",MeterPlankX,MeterPlankWidth);
            sheepRow=new GameObject("Sheep").transform;sheepRow.SetParent(strip,false);
            Divider(DividerB);
            var tree=EmberdeepUiIcons.Icon("Charred tree icon",strip,EmberdeepUiIcons.Tree,unlit,new Vector3(MeterIconX,0,-.004f),.03f);Tint(tree,new Color(.85f,.42f,.16f));
            meterPips=new GameObject("Burn meter pips").transform;meterPips.SetParent(strip,false);meterPips.localPosition=new Vector3(MeterPipsX,0,-.005f);
            meterCount=Text(strip,"",new Vector3(MeterCountX,0,-.005f));meterCount.anchor=TextAnchor.MiddleLeft;
            plane=new GameObject("Plane charge ring").transform;plane.SetParent(strip,false);plane.localPosition=new Vector3(PlaneX,0,-.004f);
            planeBackMesh=new Mesh{name="Plane ring back"};var back=planeBackMesh;EmberdeepUiIcons.Arc(back,PlaneOuter,PlaneInner,1);
            planeBack=EmberdeepUiIcons.Icon("Ring back",plane,back,unlit,Vector3.zero,1);Tint(planeBack,new Color(.16f,.24f,.26f));
            planeArc=new Mesh{name="Plane ring charge"};planeFill=EmberdeepUiIcons.Icon("Ring charge",plane,planeArc,unlit,new Vector3(0,0,-.001f),1);
            planeIcon=EmberdeepUiIcons.Icon("Plane icon",plane,EmberdeepUiIcons.Plane,unlit,new Vector3(0,0,-.001f),.02f);
            plane.gameObject.SetActive(false);
            // Token
            token=new GameObject("Pause token").transform;token.SetParent(root,false);token.localPosition=layout.Token;token.localScale=Vector3.one*metres;
            var body=new GameObject("Token body").transform;body.SetParent(token,false);
            var coin=GameObject.CreatePrimitive(PrimitiveType.Cylinder);coin.name="Wooden token";coin.transform.SetParent(body,false);
            coin.transform.localScale=new Vector3(TokenDiameter,TokenThickness*.5f,TokenDiameter);
            coin.GetComponent<Collider>().isTrigger=true; // desktop click only; the ray never targets it
            tokenRenderer=coin.GetComponent<Renderer>();tokenRenderer.sharedMaterial=clay;tokenRenderer.shadowCastingMode=ShadowCastingMode.Off;
            tokenFace=new GameObject("Token face").transform;tokenFace.SetParent(body,false);
            tokenBars=EmberdeepUiIcons.Icon("Pause bars",tokenFace,EmberdeepUiIcons.PauseBars,unlit,Vector3.zero,TokenDiameter*.5f);
            tokenBars.transform.localRotation=Quaternion.Euler(90,0,0);
            // The peg under it (a child of the HUD root, upright on the board; FaceEye sizes it).
            var pegShape=GameObject.CreatePrimitive(PrimitiveType.Cylinder);pegShape.name="Token peg";pegShape.transform.SetParent(root,false);RemoveCollider(pegShape);
            tokenPeg=pegShape.transform;var pegRenderer=pegShape.GetComponent<Renderer>();pegRenderer.sharedMaterial=clay;pegRenderer.shadowCastingMode=ShadowCastingMode.Off;
            Tint(pegRenderer,new Color(.36f,.22f,.12f));
            // Bell
            bell=new GameObject("Start bell").transform;bell.SetParent(root,false);bell.localPosition=layout.Bell;bell.localScale=Vector3.one*metres;
            bellBody=new GameObject("Bell body").transform;bellBody.SetParent(bell,false);bellBody.localScale=Vector3.one*BellScale;
            var foot=GameObject.CreatePrimitive(PrimitiveType.Cylinder);foot.name="Bell base";foot.transform.SetParent(bellBody,false);RemoveCollider(foot);
            foot.transform.localScale=new Vector3(.072f,.004f,.072f);foot.transform.localPosition=Vector3.up*.004f;
            var footRenderer=foot.GetComponent<Renderer>();footRenderer.sharedMaterial=clay;footRenderer.shadowCastingMode=ShadowCastingMode.Off;Tint(footRenderer,new Color(.36f,.22f,.12f));
            bellCup=new GameObject("Bell cup").transform;bellCup.SetParent(bellBody,false);bellCup.localPosition=Vector3.up*.008f;
            var cup=new GameObject("Brass bell");cup.transform.SetParent(bellCup,false);
            cup.AddComponent<MeshFilter>().sharedMesh=EmberdeepUiIcons.Bell;bellRenderer=cup.AddComponent<MeshRenderer>();bellRenderer.sharedMaterial=clay;
            bellRenderer.shadowCastingMode=ShadowCastingMode.Off;
            var hit=cup.AddComponent<SphereCollider>();hit.isTrigger=true;hit.radius=.042f;hit.center=Vector3.up*.04f;
            bellHalo=new GameObject("Bell halo").transform;bellHalo.SetParent(bellBody,false);bellHalo.localPosition=Vector3.up*.0085f;
            bellHaloRenderer=EmberdeepUiIcons.Icon("Halo",bellHalo,RingMesh(),unlit,Vector3.zero,1);bellHaloRenderer.transform.localRotation=Quaternion.Euler(90,0,0);
            bellHalo.gameObject.SetActive(false);
            // The countdown counts where the bell hung (inside it until it rises away), not on the bell.
            countdown=Text(bell,"",Vector3.up*BellTopHeight*BellScale*.45f);
            voice=root.gameObject.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=.85f;voice.minDistance=.4f;voice.dopplerLevel=0;
            side=0;laidOut=false;ApplyLayout();Place(LayoutFor(Board,side,DefaultEye,field));FaceEye();
        }
        Mesh ringMesh,planeBackMesh;
        Mesh RingMesh(){if(!ringMesh){ringMesh=new Mesh{name="Bell halo ring"};EmberdeepUiIcons.Arc(ringMesh,.5f,.42f,1,48);}return ringMesh;}
        Renderer Plank(string name,float x,float width)
        {
            var board=GameObject.CreatePrimitive(PrimitiveType.Cube);board.name=name;board.transform.SetParent(strip,false);
            board.transform.localScale=new Vector3(width,StripHeight,.01f);board.transform.localPosition=new Vector3(x,0,.005f);RemoveCollider(board);
            EmberdeepForestUi.Apply(board);var r=board.GetComponent<Renderer>();r.shadowCastingMode=ShadowCastingMode.Off;r.sharedMaterial.renderQueue=AfterVeil;
            return r;
        }
        void Divider(float x)
        {
            var r=EmberdeepUiIcons.Icon("Divider",strip,EmberdeepUiIcons.Disc,unlit,new Vector3(x,0,-.004f),1);
            r.transform.localScale=new Vector3(.0016f,StripHeight*.62f,1);Tint(r,new Color(.7f,.54f,.24f));
        }
        static void RemoveCollider(GameObject go){var c=go.GetComponent<Collider>();if(c){c.enabled=false;Destroy(c);}}
        static TextMesh Text(Transform parent,string value,Vector3 at)
        {
            var go=new GameObject("Rim label");go.transform.SetParent(parent,false);go.transform.localPosition=at;
            var t=go.AddComponent<TextMesh>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");go.GetComponent<MeshRenderer>().sharedMaterial=t.font.material;
            t.fontSize=64;t.characterSize=.004f;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=Color.white;t.text=value;return t;
        }
        // The localizer re-assigns the font's shared material; keep a copy that draws after the veil.
        static void KeepQueue(TextMesh text,ref Material material,string name)
        {
            var r=text.GetComponent<MeshRenderer>();var font=text.font?text.font.material:null;
            if(font && (!material || material.mainTexture!=font.mainTexture)){if(material)Destroy(material);material=new Material(font){name=name,renderQueue=TextQueue};}
            if(material && r.sharedMaterial!=material)r.sharedMaterial=material;
        }
        static void Fit(TextMesh text,float height,float width)
        {
            if(!text || string.IsNullOrEmpty(text.text))return;
            var size=text.GetComponent<Renderer>().localBounds.size;if(size.y<1e-6f || size.x<1e-6f)return;
            float s=Mathf.Min(height/size.y,width/size.x);if(Mathf.Abs(s-1)>.01f)text.characterSize*=s;
        }
        void Tint(Renderer r,Color color){if(!r)return;block.Clear();block.SetColor("_BaseColor",color);r.SetPropertyBlock(block);}

        // ---- Audio: a struck service bell and a wooden tock, synthesized once -----------------------------
        static AudioClip sharedBell,sharedTock;
        AudioClip BellClip=>sharedBell?sharedBell:sharedBell=Synth("Rim bell",1.6f,(t)=>
            (Mathf.Sin(2*Mathf.PI*1320*t)*Mathf.Exp(-t*2.2f)+.6f*Mathf.Sin(2*Mathf.PI*2640.0f*1.003f*t)*Mathf.Exp(-t*3.4f)
            +.35f*Mathf.Sin(2*Mathf.PI*3640*t)*Mathf.Exp(-t*5f)+.2f*Mathf.Sin(2*Mathf.PI*7130*t)*Mathf.Exp(-t*9f))*.42f*Mathf.Clamp01(t*400));
        AudioClip TockClip=>sharedTock?sharedTock:sharedTock=Synth("Rim tock",.09f,(t)=>
            (Mathf.Sin(2*Mathf.PI*520*t)+.5f*Mathf.Sin(2*Mathf.PI*1270*t))*Mathf.Exp(-t*55)*.5f*Mathf.Clamp01(t*900));
        static AudioClip Synth(string name,float seconds,System.Func<float,float> wave)
        {
            const int rate=44100;int n=Mathf.CeilToInt(seconds*rate);var data=new float[n];
            for(int i=0;i<n;i++)data[i]=wave(i/(float)rate);
            var clip=AudioClip.Create(name,n,1,rate,false);clip.SetData(data,0);return clip;
        }
        void OnDestroy()
        {
            if(root)Destroy(root.gameObject);
            if(unlit)Destroy(unlit);if(clay)Destroy(clay);if(countMaterial)Destroy(countMaterial);if(countdownMaterial)Destroy(countdownMaterial);
            if(planeArc)Destroy(planeArc);if(ringMesh)Destroy(ringMesh);if(planeBackMesh)Destroy(planeBackMesh);
        }
    }
}

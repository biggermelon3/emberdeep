using System.Collections.Generic;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FireRescue
{
    // LoopSpec §5 pieces and §6 Plan look, game side (workstream L3). Spell flow only.
    // - Pieces are taken from the tray by hand (EmberdeepPieceGrasp: a pinch or a grab near the piece's visible box, with a
    //   hover highlight first) and set on a square with a placement ghost; never by ray (the hand ray is off in Plan and
    //   Fight anyway, §11b). In Plan a placed piece can be picked up again, by the same rules.
    // - An excavator's line is drawn with an index fingertip: touch the excavator with the tip (the palm and thumb stay
    //   away, so a grab or pinch still picks it up), trace across the squares, lift the finger to finish.
    // - The plane token appears in the tray once the charge is full (fight only); its drop line follows the yaw of the
    //   hand that carries it.
    // - Interplay (headset test 3): a sheep and a piece within the same hand's reach go to the nearer (Rival /
    //   PieceClaimsHand); the bell never takes a carrying hand (PieceOwnsHand); palm water never comes from a hand that
    //   carries, is about to take, or has just let go of a piece (PieceBlocksWater); a hand in the pump's strike zone never
    //   takes a piece.
    // - The tower tip (headset test 3): a short text over the tray's first tower in Plan, until a tower is set down.
    // - Desktop: drag with the mouse (Q/E turn the plane line), drag from an excavator to draw. Tests use CarryPiece /
    //   MoveCarried / DropCarried / DrawLine, or synthetic hands through SetScriptPieceHands (the tracked-hand path).
    public sealed partial class FireRescueGame
    {
        public const float DrawStartRadius=.045f; // m: fingertip to the excavator to start a line
        public const float DrawCeiling=2.6f;      // arena units (~21 cm) above the board: lift the tip past this to finish
        public const float CarryHeight=.35f;      // arena units: a mouse-carried piece floats this far over the board
        public const float CarrySnap=25;          // 1/s: a taken piece glides into the fingers instead of jumping
        public const float ReleaseSpraySeconds=.35f; // an opening hand is both the release and the spray: hold the water this long
        const int DesktopHand=2,ScriptHand=3; // the mouse, and tests/dev calls (never touched by the mouse path)
        EmberdeepPieceView pieceView;EmberdeepPlanLook planLook;
        readonly EmberdeepPieceGrasp pieceGrasp=new EmberdeepPieceGrasp();
        readonly List<EmberdeepPieceGrasp.Candidate> pieceCandidates=new List<EmberdeepPieceGrasp.Candidate>();
        readonly EmberdeepPieceGrasp.Hand[] pieceHands=new EmberdeepPieceGrasp.Hand[2];
        readonly float[] pieceReleasedAt={-10,-10};
        bool scriptHandsSet;EmberdeepPieceGrasp.Hand scriptLeft,scriptRight;
        int desktopHover=-1;
        public EmberdeepPieceView PieceView=>pieceView;
        public EmberdeepPlanLook PlanLook=>planLook;
        public EmberdeepPieceGrasp PieceGrasp=>pieceGrasp;
        public int CarriedPiece {get;private set;}=-1;
        public int GhostSquare {get;private set;}=-1;
        public FirePlaceBlock GhostBlock {get;private set;}=FirePlaceBlock.NoSquare;
        public float CarriedYaw {get;private set;}
        public int DrawingPiece {get;private set;}=-1;
        public int PiecesPlaced {get;private set;}
        Vector3 carriedWorld,grabLocal;int carryHand=-1,drawHand=-1;float drawLost;
        // Time stopped (Plan): FireRescueGame.Hands.cs holds the environment clock with it.
        bool PlanFrozen=>planLook && planLook.Planning;
        bool PieceOwnsHand(int hand)=>pieceGrasp.OwnsHand(hand) || drawHand==hand || carryHand==hand;
        // Also a hand about to take a piece: hovering one nearer than any sheep it could grab (the sheep pass leaves it).
        bool PieceClaimsHand(int hand)=>PieceOwnsHand(hand) || hand>=0 && hand<2 && pieceGrasp.Hover(hand)>=0;
        // Palm water (FireRescueGame.Magic.cs) checks this for the water hand.
        public bool PieceBlocksWater(int hand)=>PieceClaimsHand(hand) || hand>=0 && hand<2 && Time.unscaledTime-pieceReleasedAt[hand]<ReleaseSpraySeconds;

        // BuildIncident: the level's pieces.
        void BuildBoardPieces()
        {
            if(!Run.SpellMode)return;
            if(!planLook){planLook=gameObject.AddComponent<EmberdeepPlanLook>();planLook.Initialize(this);}
            if(!pieceView)
            {
                pieceView=new GameObject("Board pieces").AddComponent<EmberdeepPieceView>();pieceView.transform.SetParent(Arena,false);
                pieceView.Initialize(Effects);
            }
            pieceView.Bind(Run);pieceView.Build(Run);
            if(!planBanner){planBanner=new GameObject("Plan banner").AddComponent<EmberdeepPlanBanner>();planBanner.transform.SetParent(pieceView.transform,false);planBanner.Bind(PlanBannerFrame);}
            planBannerWas=false; // every level build (a retry from the pause panel too) opens with the PLAN card
            pieceGrasp.Reset();CarriedPiece=-1;carryHand=-1;DrawingPiece=drawHand=-1;GhostSquare=-1;desktopHover=-1;
        }

        // Update, every live frame in Plan and Fight (after the rim input), before the fight sim ticks.
        void TickBoardPieces(float dt)
        {
            if(!pieceView)return;
            bool active=Run.BoardRules && Run.HasPieces && (Run.Phase==FirePhase.Preparing || Run.Phase==FirePhase.Fighting);
            if(!active){CancelCarry();pieceGrasp.Reset();desktopHover=-1;}
            else if(scriptHandsSet)StepPieceHands(scriptLeft,scriptRight,dt);
            else if(Player.IsXR){ReadPieceHands();StepPieceHands(pieceHands[0],pieceHands[1],dt);}
            else DesktopPieces();
            RenderBoardPieces(dt);
        }
        void RenderBoardPieces(float dt)
        {
            if(!pieceView)return;
            var board=RimHud && RimHud.Built?RimHud.Board:EmberdeepRimHud.DefaultBoard;
            var carry=EmberdeepPieceView.Carry.None;
            if(CarriedPiece>=0)
            {
                carry.Id=CarriedPiece;carry.Local=Arena.InverseTransformPoint(carriedWorld);carry.Square=GhostSquare;
                carry.Valid=GhostBlock==FirePlaceBlock.None;carry.Yaw=CarriedYaw;
            }
            carry.Drawing=DrawingPiece;
            if(scriptHandsSet || Player.IsXR){carry.Hover0=pieceGrasp.Hover(0);carry.Hover1=pieceGrasp.Hover(1);}
            else carry.Hover0=CarriedPiece<0 && DrawingPiece<0?desktopHover:-1;
            pieceView.Render(Run,board,carry,Player?Player.ViewCamera:null,dt);
            RenderTowerTip();
            // Dug trees are gone (dirt), and water can no longer land on them.
            for(int i=0;i<trees.Count && i<Run.Trees.Length;i++)
            {
                bool shown=Run.Trees[i].State!=TreeState.Cleared;
                if(trees[i].Root && trees[i].Root.gameObject.activeSelf!=shown)trees[i].Root.gameObject.SetActive(shown);
            }
            // The rim's plane-charge ring (EmberdeepRimHud): on the plane's levels, empty while the plane is out. Written when
            // the run's values change, so anything else that drives the ring (a capture, a demo) isn't overwritten every frame.
            if(RimHud)
            {
                bool available=Run.PlaneEarnable && (Run.Phase==FirePhase.Preparing || Run.Phase==FirePhase.Fighting);
                float charge=Run.PlaneState==FireRun.PlaneStage.None?Run.PlaneCharge01:0;
                if(RimHud!=ringHud || available!=ringAvailable){RimHud.PlaneAvailable=ringAvailable=available;}
                if(RimHud!=ringHud || Mathf.Abs(charge-ringCharge)>1e-4f){RimHud.PlaneCharge01=ringCharge=charge;}
                ringHud=RimHud;
            }
        }
        EmberdeepRimHud ringHud;bool ringAvailable;float ringCharge;

        // ---- PLAN banner (headset test 4) ------------------------------------------------------------------------------
        // Each level's Plan opens with the PLAN card over the tray; it shrinks to a small tag on the tray's front edge after
        // EmberdeepPlanBanner.BigSeconds and goes with the bell (the countdown) or the end of Plan. The tower tip waits
        // until the card has shrunk (PlanCardBig).
        EmberdeepPlanBanner planBanner;bool planBannerWas;
        public EmberdeepPlanBanner PlanBanner=>planBanner;
        public bool PlanBannerWanted
        {
            get
            {
                var cam=Player?Player.ViewCamera:null;
                return cam && pieceView && PlanFrozen && Run.BoardRules && Run.Phase==FirePhase.Preparing && Run.CountdownRemaining<0
                    && focused && !paused && !acquiringPose && !returningToCamp
                    && !QuitConfirmationOpen && !PauseOpen && !ShopOpen && !RimBlocksPlay;
            }
        }
        // The card is up (or still shrinking, or a new Plan's card is about to start): the tower tip holds back.
        public bool PlanCardBig=>planBanner && PlanBannerWanted
            && (!planBannerWas || planBanner.Age<EmberdeepPlanBanner.BigSeconds+EmberdeepPlanBanner.ShrinkSeconds);
        // Polled by the banner's LateUpdate (EmberdeepPlanBanner.Frame), every frame, whatever Update did.
        bool PlanBannerFrame(out Vector3 big,out Vector3 front,out Camera eye,out bool hasPieces,out bool restart)
        {
            big=front=default;eye=null;hasPieces=false;restart=false;
            if(!pieceView || !pieceView.Tray || Run==null)return false;
            bool plan=PlanFrozen && Run.Phase==FirePhase.Preparing;
            restart=plan && !planBannerWas;planBannerWas=plan;
            if(!PlanBannerWanted)return false;
            var tray=pieceView.Tray;
            big=tray.position;front=pieceView.transform.TransformPoint(tray.localPosition+Vector3.back*EmberdeepPieceView.TrayDepth*.5f);
            eye=Player.ViewCamera;hasPieces=Run.Pieces.Length>0;
            return true;
        }

        // ---- Tower tip (headset test 3) ------------------------------------------------------------------------------
        // Plan only (never text in a fight, §13), before the bell, over the tray's first tower, until a tower is set on the
        // board (EmberdeepTowerTip.MarkDone in EndCarry). SHOW HINTS / HOW TO PLAY bring it back (EmberdeepFirstHints.Reset).
        public int TowerTipPiece=>System.Array.FindIndex(Run.Pieces,p=>p.Kind==FirePieceKind.Tower && !p.Placed);
        public bool TowerTipWanted
        {
            get
            {
                var cam=Player?Player.ViewCamera:null;
                return cam && pieceView && EmberdeepTowerTip.Needed && Run.BoardRules && Run.Phase==FirePhase.Preparing && Run.CountdownRemaining<0
                    && TowerTipPiece>=0 && pieceView.TrayVisible && focused && !paused && !acquiringPose && !returningToCamp
                    && !QuitConfirmationOpen && !PauseOpen && !ShopOpen && !RimBlocksPlay && !PlanCardBig;
            }
        }
        void RenderTowerTip()
        {
            var tip=pieceView.Tip;if(!tip)return;
            if(!TowerTipWanted){tip.Hide();return;}
            var anchor=pieceView.TipAnchor(TowerTipPiece);var cam=Player.ViewCamera;
            tip.Show(anchor,cam,TipClearance(anchor,cam,Mathf.Max(tip.Size.x,.12f)*.5f));
        }
        public const float TipClearAngle=1.5f; // degrees between the tip's lower edge and the top of what stands in front of it
        readonly List<Renderer> tipOccluders=new List<Renderer>();Object tipOccluderSource;
        // From the seat the pump stands in front of the tray (headset test 3 capture, tilted board): how much higher (m) the
        // tip's lower edge must be to clear, by TipClearAngle, every part of it that is nearer than the tip and inside its
        // width in view. 0 when nothing is in the way.
        float TipClearance(Vector3 anchor,Camera cam,float halfWidth)
        {
            if(!cam || !pumpView || !pumpView.gameObject.activeInHierarchy)return 0;
            if(tipOccluderSource!=pumpView){tipOccluders.Clear();pumpView.GetComponentsInChildren(false,tipOccluders);tipOccluderSource=pumpView;}
            var eye=cam.transform.position;var flat=anchor-eye;flat.y=0;float horiz=flat.magnitude;if(horiz<.05f)return 0;
            var forward=flat/horiz;var side=Vector3.Cross(Vector3.up,forward);float halfYaw=Mathf.Atan2(halfWidth,horiz);
            float top=float.NegativeInfinity;
            foreach(var r in tipOccluders)
            {
                if(!r || !r.enabled || !r.gameObject.activeInHierarchy)continue;
                var b=r.bounds;float yawMin=float.PositiveInfinity,yawMax=float.NegativeInfinity,elevation=float.NegativeInfinity,nearest=float.PositiveInfinity;
                for(int i=0;i<8;i++)
                {
                    var d=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))-eye;
                    float along=Vector3.Dot(d,forward),across=Vector3.Dot(d,side);if(along<=.01f)continue;
                    float yaw=Mathf.Atan2(across,along);yawMin=Mathf.Min(yawMin,yaw);yawMax=Mathf.Max(yawMax,yaw);
                    elevation=Mathf.Max(elevation,Mathf.Atan2(d.y,new Vector2(along,across).magnitude));nearest=Mathf.Min(nearest,along);
                }
                if(nearest>=horiz || yawMax<-halfYaw || yawMin>halfYaw)continue; // behind the tip, or beside it
                top=Mathf.Max(top,elevation);
            }
            if(float.IsNegativeInfinity(top))return 0;
            float want=horiz*Mathf.Tan(top+TipClearAngle*Mathf.Deg2Rad)-(anchor.y+EmberdeepTowerTip.Lift-eye.y);
            return Mathf.Clamp(want,0,.3f);
        }

        // ---- Carry -----------------------------------------------------------------------------------------------
        bool BeginCarry(int id,int hand)
        {
            if(CarriedPiece>=0)return false;
            if(id==EmberdeepPieceView.PlaneId){if(!Run.PlaneReady)return false;}
            else
            {
                if(id<0 || id>=Run.Pieces.Length)return false;
                var p=Run.Pieces[id];
                if(p.Placed && !Run.LiftPiece(id))return false; // Plan: picked back up off the board
                if(p.Used || p.Driving || p.Finished)return false;
            }
            CarriedPiece=id;carryHand=hand;GhostSquare=-1;GhostBlock=FirePlaceBlock.NoSquare;
            if(DrawingPiece==id){DrawingPiece=-1;drawHand=-1;}
            return true;
        }
        // A hand just took the carried piece: it hangs from where the fingers met its box (clamped onto the box, so it
        // glides into a hand that closed a few cm off), carried at full size, upright on the board.
        void HoldFrom(int hand)
        {
            var root=CarriedPiece==EmberdeepPieceView.PlaneId?pieceView.PlaneToken:pieceView.Piece(CarriedPiece);
            var anchor=Arena.InverseTransformPoint(pieceGrasp.Anchor(hand));
            if(!root){grabLocal=Vector3.up*.5f;carriedWorld=pieceGrasp.Anchor(hand);return;}
            var basePoint=Arena.InverseTransformPoint(root.position);float s=Mathf.Max(root.localScale.x,1e-3f);
            var box=pieceView.LocalBox(CarriedPiece);var local=(anchor-basePoint)/s;
            grabLocal=new Vector3(Mathf.Clamp(local.x,box.min.x,box.max.x),Mathf.Clamp(local.y,box.min.y,box.max.y),Mathf.Clamp(local.z,box.min.z,box.max.z));
            carriedWorld=root.position;
        }
        // Where the carried piece's base belongs this frame: the hand's anchor minus the held point.
        Vector3 CarryTarget(int hand)=>Arena.TransformPoint(Arena.InverseTransformPoint(pieceGrasp.Anchor(hand))-grabLocal);
        void UpdateGhost()
        {
            var local=Arena.InverseTransformPoint(carriedWorld);
            GhostSquare=local.y>-1 && local.y<8?Run.NearestSquare(local,.8f):-1;
            GhostBlock=CarriedPiece==EmberdeepPieceView.PlaneId?Run.CanCallPlane(GhostSquare):Run.CanPlace(CarriedPiece,GhostSquare);
        }
        // Puts the carried piece down on the ghost square if it fits; otherwise it goes back to the tray.
        bool EndCarry()
        {
            if(CarriedPiece<0)return false;
            UpdateGhost();bool placed=false;
            if(GhostSquare>=0 && GhostBlock==FirePlaceBlock.None)
                placed=CarriedPiece==EmberdeepPieceView.PlaneId?Run.CallPlane(GhostSquare,CarriedYaw):Run.PlacePiece(CarriedPiece,GhostSquare);
            if(placed)
            {
                PiecesPlaced++;pieceView.PlayPlace(Arena.TransformPoint(Run.Squares[GhostSquare]));
                if(CarriedPiece!=EmberdeepPieceView.PlaneId && Run.Pieces[CarriedPiece].Kind==FirePieceKind.Tower)EmberdeepTowerTip.MarkDone();
            }
            CarriedPiece=-1;carryHand=-1;GhostSquare=-1;GhostBlock=FirePlaceBlock.NoSquare;
            return placed;
        }
        void CancelCarry()
        {
            if(carryHand>=0 && carryHand<2)pieceGrasp.Cancel(carryHand);
            CarriedPiece=-1;carryHand=-1;GhostSquare=-1;DrawingPiece=-1;drawHand=-1;
        }

        // ---- Hands -------------------------------------------------------------------------------------------------
        // Tracked hands: palm centre and closure (the same sample as the sheep and the pump), fingertips, thumb-index pinch.
        void ReadPieceHands()
        {
            for(int i=0;i<2;i++)
            {
                bool left=i==0;var h=EmberdeepPieceGrasp.Hand.From(toolHands[i]);
                if(h.Valid)
                {
                    for(int f=0;f<5;f++)if(hands.TryReadFingertip(left,f,out var tip))h.SetTip(f,tip);
                    if(hands.ReadObjectPose(left,out _,out var pinch,out bool pinchValid) && pinchValid && float.IsFinite(pinch)){h.PinchValid=true;h.Pinch=pinch;}
                    if(h.HasTip(0) && h.HasTip(1)){h.PinchPointValid=true;h.PinchPoint=(h.Thumb+h.Index)*.5f;}
                    else if(!hands.UsesPortableGrip)
                    {
                        // Pinching tips often drop to low confidence as they meet; Meta still poses them (the pinch reading
                        // uses the same joints), so the pinch point comes from there.
                        var tracked=hands.TrackedHand(left);
                        if(tracked && tracked.GetJointPose(HandJointId.HandThumbTip,out var thumb) && tracked.GetJointPose(HandJointId.HandIndexTip,out var index))
                        {h.PinchPointValid=true;h.PinchPoint=(thumb.position+index.position)*.5f;}
                    }
                }
                pieceHands[i]=h;
            }
        }
        void StepPieceHands(EmberdeepPieceGrasp.Hand left,EmberdeepPieceGrasp.Hand right,float dt)
        {
            pieceHands[0]=left;pieceHands[1]=right;
            pieceCandidates.Clear();
            if(CarriedPiece<0)
            {
                for(int i=0;i<Run.Pieces.Length;i++)
                {
                    var p=Run.Pieces[i];
                    if(!p.Placed || Run.CanLift(i))pieceCandidates.Add(pieceView.GraspBox(i));
                }
                if(Run.PlaneReady && pieceView.PlaneToken && pieceView.PlaneToken.gameObject.activeInHierarchy)pieceCandidates.Add(pieceView.GraspBox(EmberdeepPieceView.PlaneId));
            }
            pieceGrasp.Rival[0]=SheepRival(left);pieceGrasp.Rival[1]=SheepRival(right);
            pieceGrasp.Step(left,right,pieceCandidates,dt,PieceHandBlocked(0,left),PieceHandBlocked(1,right));
            for(int h=0;h<2;h++)
            {
                if(pieceGrasp.Grabbed[h]>=0)
                {
                    // A pinch or grab on an excavator beats a line that hasn't left its square yet.
                    if(drawHand==h){DrawingPiece=-1;drawHand=-1;}
                    if(BeginCarry(pieceGrasp.Grabbed[h],h))HoldFrom(h);else pieceGrasp.Cancel(h);
                }
                if(pieceGrasp.Released[h]>=0)
                {
                    pieceReleasedAt[h]=Time.unscaledTime;
                    if(carryHand==h){carriedWorld=CarryTarget(h);EndCarry();}
                }
            }
            if(carryHand>=0 && carryHand<2 && pieceGrasp.OwnsHand(carryHand))
            {
                carriedWorld=Vector3.Lerp(carriedWorld,CarryTarget(carryHand),1-Mathf.Exp(-Mathf.Max(dt,0)*CarrySnap));
                if(CarriedPiece==EmberdeepPieceView.PlaneId)CarriedYaw=HandYaw(carryHand);
                UpdateGhost();
            }
            else if(carryHand>=0 && carryHand<2)CancelCarry();
            TickFingerDrawing(left,right,dt);
        }
        bool PieceHandBlocked(int h,in EmberdeepPieceGrasp.Hand hand)
        {
            if(sheepInteraction.OwnsHand(h) || SpellOwnsHand(h) || (palmMagic.Casting && h==WaterHand) || (carryHand>=0 && carryHand!=h))return true;
            // Drawing a line that has left the excavator's square: the finger is busy.
            if(drawHand==h && DrawingPiece>=0 && DrawingPiece<Run.Pieces.Length && Run.Pieces[DrawingPiece].Line.Count>0)return true;
            return InPumpStrike(hand);
        }
        // A fist over the pump cap hammers; it never takes a tray piece on the way. Fight only (the pump only strikes then):
        // in Plan a hand reaching over the pump to the tray's front slot still picks up.
        bool InPumpStrike(in EmberdeepPieceGrasp.Hand hand)
        {
            if(Run.Phase!=FirePhase.Fighting || !hand.Usable || !pumpView || !pumpView.gameObject.activeInHierarchy)return false;
            var d=hand.Palm-pumpView.CapTop;float along=Vector3.Dot(d,pumpView.Up);
            return along>-.03f && along<.25f && (d-pumpView.Up*along).magnitude<EmberdeepWaterTuning.StrikeRadius;
        }
        // The nearest sheep this hand could grab right now (its own rule: the palm within PickRadius of its middle), as a
        // surface distance from the hand's nearest point; a piece farther than that loses (the closest target wins).
        float SheepRival(in EmberdeepPieceGrasp.Hand hand)
        {
            if(!hand.Usable || !Arena)return float.PositiveInfinity;
            float best=float.PositiveInfinity,scale=Mathf.Abs(Arena.lossyScale.x),radius=FireRun.AnimalRadius*scale;
            foreach(var a in Run.Animals)
            {
                if(!FireRun.CanHold(a) || a.HeldBy>=0)continue;
                var centre=Arena.TransformPoint(EmberdeepSheepInteraction.Center(a));
                if(Vector3.Distance(hand.Palm,centre)>EmberdeepSheepInteraction.PickRadius)continue;
                float d=Vector3.Distance(hand.Palm,centre);
                for(int f=0;f<5;f++)if(hand.HasTip(f))d=Mathf.Min(d,Vector3.Distance(hand.Tip(f),centre));
                best=Mathf.Min(best,d-radius);
            }
            return best;
        }
        // Grasp at the piece's visible middle (the mouse path picks by it).
        Vector3 GraspPoint(int i)=>pieceView.GraspBox(i).Center;
        // The carrying hand's yaw on the board (fingers' direction), in board degrees.
        float HandYaw(int hand)
        {
            var h=pieceHands[hand];if(!h.HasTip(2))return CarriedYaw;
            var d=Arena.InverseTransformDirection(h.Middle-h.Palm);d.y=0;
            return d.sqrMagnitude<1e-6f?CarriedYaw:Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;
        }
        void TickFingerDrawing(in EmberdeepPieceGrasp.Hand left,in EmberdeepPieceGrasp.Hand right,float dt)
        {
            if(DrawingPiece<0)
            {
                for(int i=0;i<Run.Pieces.Length && DrawingPiece<0;i++)
                {
                    if(!CanStartLine(i))continue;
                    var foot=Arena.TransformPoint(Run.ExcavatorPosition(i));var top=Arena.TransformPoint(Run.ExcavatorPosition(i)+Vector3.up*.9f);
                    var box=pieceView.GraspBox(i);
                    for(int h=0;h<2;h++)
                    {
                        var hand=h==0?left:right;
                        if(!hand.Usable || !hand.HasTip(1) || PieceOwnsHand(h) || sheepInteraction.OwnsHand(h) || SpellOwnsHand(h) || palmMagic.Casting)continue;
                        // A fingertip poke, not a grasp: the palm and the thumb stay off the excavator (those pick it up).
                        if(box.Distance(hand.Palm)<EmberdeepPieceGrasp.GrabReach)continue;
                        if(hand.HasTip(0) && box.Distance(hand.Thumb)<EmberdeepPieceGrasp.PinchReach)continue;
                        if(DistanceToSegment(hand.Index,foot,top)>DrawStartRadius)continue;
                        if(Run.StartLine(i)){DrawingPiece=i;drawHand=h;drawLost=0;break;}
                    }
                }
                return;
            }
            if(drawHand<0 || drawHand>1)return; // the mouse and the script draw on their own paths
            var drawing=drawHand==0?left:right;
            if(!drawing.Usable || !drawing.HasTip(1)){drawLost+=dt;if(drawLost>.3f)EndDrawing();return;}
            drawLost=0;var local=Arena.InverseTransformPoint(drawing.Index);
            if(local.y>DrawCeiling){EndDrawing();return;}
            int square=Run.NearestSquare(local,.75f);if(square>=0)Run.TraceLine(DrawingPiece,square);
        }
        bool CanStartLine(int i)=>i>=0 && i<Run.Pieces.Length && i!=CarriedPiece && Run.Pieces[i].Kind==FirePieceKind.Excavator && Run.Pieces[i].Placed
            && (Run.CanDraw(i) || Run.Phase==FirePhase.Preparing && !Run.Pieces[i].Driving);
        void EndDrawing()
        {
            if(DrawingPiece>=0 && DrawingPiece<Run.Pieces.Length && Run.Pieces[DrawingPiece].Line.Count>0)Run.FinishLine(DrawingPiece);
            DrawingPiece=-1;drawHand=-1;
        }
        static float DistanceToSegment(Vector3 p,Vector3 a,Vector3 b)
        {
            var ab=b-a;float t=ab.sqrMagnitude>1e-8f?Mathf.Clamp01(Vector3.Dot(p-a,ab)/ab.sqrMagnitude):0;
            return Vector3.Distance(p,a+ab*t);
        }

        // ---- Desktop ---------------------------------------------------------------------------------------------
        void DesktopPieces()
        {
            var mouse=Mouse.current;if(mouse==null || !Player){desktopHover=-1;return;}
            var camera=Player.ViewCamera;var screen=mouse.position.ReadValue();var ray=camera.ScreenPointToRay(screen);
            bool onBoard=BoardPoint(ray,out var local);
            if(CarriedPiece<0 && DrawingPiece<0)
            {
                int best=-1;float bestD=36*36;bool draw=false;
                void Try(int id,Vector3 world,bool line)
                {
                    var s=camera.WorldToScreenPoint(world);if(s.z<=0)return;
                    float d=((Vector2)s-screen).sqrMagnitude;if(d<bestD){bestD=d;best=id;draw=line;}
                }
                for(int i=0;i<Run.Pieces.Length;i++)
                {
                    var p=Run.Pieces[i];
                    // A placed excavator: drag from it to draw its line (Shift drags it to another square in Plan).
                    bool shift=Keyboard.current!=null && Keyboard.current.shiftKey.isPressed;
                    if(p.Kind==FirePieceKind.Excavator && CanStartLine(i) && !shift)Try(i,GraspPoint(i),true);
                    else if(!p.Placed || Run.CanLift(i))Try(i,GraspPoint(i),false);
                }
                if(Run.PlaneReady && pieceView.PlaneToken)Try(EmberdeepPieceView.PlaneId,pieceView.PlaneToken.TransformPoint(Vector3.up*.3f),false);
                desktopHover=best;
                if(best>=0 && mouse.leftButton.wasPressedThisFrame)
                {
                    if(draw){if(Run.StartLine(best)){DrawingPiece=best;drawHand=DesktopHand;}}
                    else BeginCarry(best,DesktopHand);
                }
            }
            else desktopHover=-1;
            if(carryHand==DesktopHand)
            {
                var keys=Keyboard.current;
                if(keys!=null){if(keys.qKey.isPressed)CarriedYaw-=120*Time.unscaledDeltaTime;if(keys.eKey.isPressed)CarriedYaw+=120*Time.unscaledDeltaTime;}
                if(onBoard)carriedWorld=Arena.TransformPoint(local+Vector3.up*CarryHeight);
                UpdateGhost();
                if(!mouse.leftButton.isPressed)EndCarry();
            }
            if(drawHand==DesktopHand)
            {
                if(onBoard){int square=Run.NearestSquare(local,.75f);if(square>=0)Run.TraceLine(DrawingPiece,square);}
                if(!mouse.leftButton.isPressed)EndDrawing();
            }
        }
        // The mouse ray on the board top (arena y 0), arena units.
        bool BoardPoint(Ray ray,out Vector3 local)
        {
            local=default;var o=Arena.InverseTransformPoint(ray.origin);var d=Arena.InverseTransformDirection(ray.direction);
            if(Mathf.Abs(d.y)<1e-5f)return false;float t=-o.y/d.y;if(t<=0)return false;
            local=o+d*t;return true;
        }

        // ---- Test / dev API (the desktop path) ---------------------------------------------------------------------
        // Carry a tray piece (or PlaneId) to a world point, as a hand or the mouse would.
        public bool CarryPiece(int id,Vector3 world)
        {
            if(!BeginCarry(id,ScriptHand))return false;
            MoveCarried(world);return true;
        }
        public void MoveCarried(Vector3 world,float yaw=float.NaN)
        {
            if(CarriedPiece<0)return;
            carriedWorld=world;if(!float.IsNaN(yaw))CarriedYaw=yaw;UpdateGhost();
        }
        // World point just above a square's centre.
        public Vector3 SquareWorld(int square)=>Arena.TransformPoint(Run.Squares[square]+Vector3.up*CarryHeight);
        public bool DropCarried()=>EndCarry();
        // Draws an excavator's line through these squares (the finger path) and lifts the finger.
        public bool DrawLine(int piece,params int[] squares)
        {
            if(!Run.StartLine(piece))return false;
            DrawingPiece=piece;drawHand=ScriptHand;
            foreach(var s in squares)Run.TraceLine(piece,s);
            EndDrawing();return Run.Pieces[piece].LineDone;
        }
        // Synthetic tracked hands (tests, simulator scripts): the hand path (grasp, hover, carry, drawing) runs on these
        // instead of tracking or the mouse until cleared.
        public void SetScriptPieceHands(EmberdeepPieceGrasp.Hand left,EmberdeepPieceGrasp.Hand right){scriptLeft=left;scriptRight=right;scriptHandsSet=true;}
        public void ClearScriptPieceHands(){scriptHandsSet=false;pieceGrasp.Reset();if(carryHand==0 || carryHand==1)CancelCarry();}
        public EmberdeepPieceGrasp.Candidate PieceBox(int id)=>pieceView.GraspBox(id);
        public int CarryHand=>carryHand;
    }
}

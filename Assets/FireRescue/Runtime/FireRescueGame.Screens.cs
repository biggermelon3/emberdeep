using System.Collections.Generic;
using PhysicalAssembly;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FireRescue
{
    // LoopSpec §11b between-level screens. Each panel is a child of the arena (so placement options carry it) and
    // faces the eye from where the arena was placed. Panel units are the HUD's (.25 m), so a small button is
    // 7.5 x 5 cm and a big one 17.5 x 5.5 cm: every target is well over 3 cm. Buttons are icons with one word.
    // - Pause: behind the board, opened by poking the rim token. Resume / retry / camp / recenter.
    // - Results: rise behind the board after the one-word banner. Cleared: NEXT big, retry + upgrades small.
    //   Lost: RETRY big, camp small (the rim's burn meter glows full).
    // - Level select: a card carousel above the next level's dimmed board. Open-hand slap browses; ray-pinch PLAY.
    // - Upgrades: 4 tiles (gem, pump, slot, tower), a rendered picture of the model, one word, the value with its unit, a
    //   price; pointing at one shows the value after buying and what it does (FireRescueGame.Upgrades.cs). While one is
    //   affordable, the UPGRADES buttons (results, level select) carry a pulsing yellow star.
    public sealed partial class FireRescueGame
    {
        // Placement follows the measured plinth (EmberdeepRimHud.Board) and the eye at placement, so a shrunk, raised or
        // moved board carries the panels along. Results/pause/upgrades: BehindBoard past the back edge (~1.05 m from the
        // default seat). Carousel: over the board at CarouselDepth of its depth (~.9 m).
        // Headset test 2026-11-22: a fixed drop under the eye (15 / 19 cm) put the buttons 9-24 deg below eye level and
        // the ray needed a wrist bent far down. Now a panel's centre sits at most PanelDown degrees under eye level, and
        // higher where needed so its lowest edge (...Below degrees under its centre: PanelUnitFor keeps each panel's
        // angular size, so these hold at any distance) stays PanelClear over what it must not cover: the back-edge
        // readout strip's top (pause, results) or, with the strip and the vane stood down, the forest's skyline
        // (upgrades, level select). Degrees, so the height and distance options carry the panels along.
        public const float BehindBoard=.06f,RiseAbove=.176f,CarouselDepth=.85f; // m, m, -
        // PauseBelow 6.2 -> 9.9 with the board placement rows (FireRescueGame.PauseBoard.cs, headset test 2).
        // UpgradesBelow 8.6 -> 11.2 with the model tiles and the explanation line (FireRescueGame.Upgrades.cs).
        public const float PanelDown=3,PanelClear=.8f,PauseBelow=9.9f,ResultsBelow=7.9f,UpgradesBelow=11.2f,CarouselBelow=14.4f; // deg
        // A panel centre's angle under eye level, given the angle of the line it must stay over (floor).
        public static float PanelCentreDown(float floor,float below)=>Mathf.Min(PanelDown,floor-PanelClear-below);
        // Headset test 3 (2026-11-22): on the tilted board (EmberdeepPlayArea.Tilted) the plinth's back edge rises 29 cm and,
        // from the default seat, stands ~12 deg under eye level: HIGHER in view than the back row's crowns (~16 deg) that the
        // upgrades page and the level select were laid over, so the board's top hid the upgrades page's lower 2-3 deg. A tilted
        // board therefore adds its top outline to every panel's floor (BoardOutlineDown): the plinth top's front-to-back line and
        // the canopy over the field rows, each cut at the panel's own depth (the carousel stands over the board, so only what is in
        // front of it can hide it). The panels go only as much higher as that takes, at the same distance. Results and pause
        // were already clear (they ride the strip, which stands on the back edge); upgrades and the carousel rise ~3 deg, to
        // just under eye level. A flat board never uses it: its strip and crowns always bind first, so its panels are unchanged.
        public const float TiltedOutlineFrom=.5f; // deg of board pitch
        // .38 m per panel unit at the reference distances keeps a one-word label over 1 deg tall (Quest 2: ~20 px per deg).
        // Closer panels shrink with distance so their angular size stays the same.
        public const float PanelUnit=.38f,ResultsReference=1.384f,CarouselReference=1.175f,MinUnit=.2f,MaxUnit=.5f;
        // ResultsDelay: the outcome banner holds the view this long, then shrinks away as the panel rises (they would overlap
        // from the seat). 1.5 s covers the loss flare + darken (1.44 s) and the clear sting (1.35 s); it was 1.1 s.
        public const float ResultsDelay=1.5f,ResultsRise=.55f,BrowseSettle=.45f,CardStep=.74f;
        // Text pass (LoopSpec §13): small-button captions sit .045 under the pill (was .055), and the results button row
        // sits at -.22 (was -.26), so the captions clear the board's bottom frame.
        public const float CaptionGap=.045f,ResultsRowY=-.22f;
        public static Vector3 ResultsAnchorFor(Bounds board,Vector3 eye,float down=PanelDown)=>
            UnderEye(new Vector3(board.center.x,0,board.max.z+BehindBoard/FireMiniature.Scale),eye,down);
        public static Vector3 CarouselAnchorFor(Bounds board,Vector3 eye,float down=PanelDown)=>
            UnderEye(new Vector3(board.center.x,0,Mathf.Lerp(board.min.z,board.max.z,CarouselDepth)),eye,down);
        // The anchor's height: degrees under eye level at its horizontal distance (arena units throughout).
        static Vector3 UnderEye(Vector3 anchor,Vector3 eye,float degrees)
        {
            float flat=new Vector2(anchor.x-eye.x,anchor.z-eye.z).magnitude;
            anchor.y=eye.y-flat*Mathf.Tan(degrees*Mathf.Deg2Rad);return anchor;
        }
        public static float PanelUnitFor(Vector3 anchor,Vector3 eye,float reference)=>
            Mathf.Clamp(PanelUnit*Vector3.Distance(anchor,eye)*FireMiniature.Scale/reference,MinUnit,MaxUnit);
        public Vector3 ResultsAnchor {get;private set;}
        public Vector3 CarouselAnchor {get;private set;}
        Vector3 resultsRiseFrom;int screensBoard=-1;
        // The pause dim is lighter since the board is adjusted under it (headset test 2).
        public const float PauseDim=.35f,SelectDim=1;
        // The dim is the outcome veil (EmberdeepOutcomeView): a see-through sheet just above the canopy. Any panel part
        // below its plane was seen through it, which drew a hard grey band across the pause panel's foot and the lower
        // half of the level cards and their buttons. Screens draw after the veil instead, as the rim HUD does
        // (EmberdeepRimHud.AfterVeil): boards and icons at ScreenQueue, text above them at ScreenTextQueue.
        public const int ScreenQueue=3100,ScreenTextQueue=3200;
        readonly Dictionary<Material,Material> afterVeil=new();
        readonly List<Renderer> screenRenderers=new();readonly List<bool> screenOwned=new();
        readonly List<Material> materialScratch=new();
        EmberdeepLocalizedText screenLocalizer;
        static readonly Color UiCream=new(.96f,.92f,.8f),UiGold=new(1,.8f,.3f),UiDim=new(.3f,.34f,.32f),UiStarOff=new(.22f,.27f,.25f);
        Transform pauseRoot,resultsRoot,selectRoot,upgradesRoot;
        bool screensBuilt;Matrix4x4 screensPlaced;
        Material uiIcon;MaterialPropertyBlock uiTint;
        readonly EmberdeepFingerPoke tokenPoke=new();
        readonly EmberdeepFistHammer bellHammer=new();
        readonly EmberdeepSlapGesture slap=new();
        FirePhase rimPhase=(FirePhase)(-1);float rimPhaseAt;int stageCoinsAtStart;
        public float ResultsShownAt {get;private set;}=-1;
        public bool ResultsVisible=>resultsRoot && resultsRoot.gameObject.activeInHierarchy;
        public bool PausePanelVisible=>pauseRoot && pauseRoot.gameObject.activeInHierarchy;
        public bool UpgradesVisible=>upgradesRoot && upgradesRoot.gameObject.activeInHierarchy;
        public bool LevelSelectVisible=>selectRoot && selectRoot.gameObject.activeInHierarchy;
        public int SelectedLevel {get;private set;}
        public int BellStrikes {get;private set;}
        public int Slaps {get;private set;}
        public EmberdeepSlapGesture Slap=>slap;
        public EmberdeepFistHammer BellHammer=>bellHammer;
        public EmberdeepFingerPoke TokenPoke=>tokenPoke;
        float selectView,selectChangedAt;

        // ---- Results widgets
        readonly Renderer[] resultStars=new Renderer[3];Transform resultStarRow,resultCharredRow;TextMesh resultSheep,resultCoins,resultEarned,resultCharred;
        WorkshopButton resultNext,resultRetry,resultUpgrades,resultRetryBig,resultCamp,selectUpgrades;
        // ---- Upgrade star (headset test 3, 2026-11-22)
        // A yellow star on the UPGRADES buttons (results and level select) while at least one upgrade is affordable, gently
        // pulsing, so the player is prompted to spend; none when nothing is affordable or everything is maxed (AnyUpgradeBuyable,
        // FireRescueGame.Upgrades.cs), or while the button is off. It sits on the pill's top-right corner like a badge, with a
        // dark rim so it reads on the wood and on the board. No collider: the pill stays the ray target.
        // .13 is ~2 deg from the seat (the pill's icon is ~2.4), and clears the coins line above.
        public const float UpgradeStarSize=.13f,UpgradeStarPulse=.12f,UpgradeStarRate=2.6f; // panel units, scale swing, rad/s (~.4 Hz)
        static readonly Color UiStarOn=new(1,.86f,.22f),UiStarRim=new(.16f,.1f,.03f);
        sealed class UpgradeStar {public Transform Root;public Renderer Face;}
        UpgradeStar resultsStar,selectStar;
        public bool ResultsUpgradeStarShown=>resultsStar!=null && resultsStar.Root && resultsStar.Root.gameObject.activeInHierarchy;
        public bool LevelSelectUpgradeStarShown=>selectStar!=null && selectStar.Root && selectStar.Root.gameObject.activeInHierarchy;
        public Transform ResultsUpgradeStar=>resultsStar?.Root;
        UpgradeStar BuildUpgradeStar(Transform parent,Vector3 button,Vector2 size)
        {
            var root=new GameObject("Upgrade star").transform;root.SetParent(parent,false);
            root.localPosition=button+new Vector3(size.x*.5f-.015f,size.y*.5f-.005f,-.07f);
            UiIconAt(root,EmberdeepUiIcons.Star,new Vector3(0,0,.005f),UpgradeStarSize*1.32f,UiStarRim);
            var face=UiIconAt(root,EmberdeepUiIcons.Star,Vector3.zero,UpgradeStarSize,UiStarOn);face.name="Upgrade star face";
            root.gameObject.SetActive(false);return new UpgradeStar{Root=root,Face=face};
        }
        void RefreshUpgradeStar(UpgradeStar star,WorkshopButton button,float now)
        {
            if(star==null || !star.Root)return;
            bool on=button && button.gameObject.activeInHierarchy && button.Interactable && AnyUpgradeBuyable;
            if(star.Root.gameObject.activeSelf!=on)star.Root.gameObject.SetActive(on);
            if(!on)return;
            float s=Mathf.Sin(now*UpgradeStarRate);
            star.Root.localScale=Vector3.one*(1+UpgradeStarPulse*s);UiTint(star.Face,Color.Lerp(UiStarOn,Color.white,.3f*(s*.5f+.5f)));
        }
        // ---- Level select widgets
        sealed class LevelCard
        {
            public Transform Root;public TextMesh Number,Name,Chip;public Renderer ChipIcon,Lock;
            public readonly Renderer[] Stars=new Renderer[3];public int Stage=-1;
        }
        readonly LevelCard[] cards=new LevelCard[3];
        WorkshopButton selectPlay,selectPrevious,selectNext;Renderer selectPreviousArrow,selectNextArrow;
        // The browse arrows (headset test 2: keep them, and make them visible): a gold chevron outside each side card; the
        // side card and its chevron are one ray target. Slapping still browses (EmberdeepSlapGesture).
        public const float ArrowX=CardStep+.36f,ArrowSize=.2f;
        public bool BrowseArrowsVisible=>selectPreviousArrow && selectNextArrow && (selectPreviousArrow.gameObject.activeInHierarchy || selectNextArrow.gameObject.activeInHierarchy);

        // ---- Level cards: what each stage introduces ----------------------------------------------------------
        // LoopSpec §8 after the stage cut (T): what each of the 6 stages introduces. Names follow FireRun.StageNameFor.
        // Text pass (§13): the grass level's chip names its new piece; the level name (HAYFIELD) carries the grass.
        public static string LevelName(FireRun run,int stage)=>FireRun.StageNameFor(run.GoddessTutorial,Mathf.Max(0,stage));
        static readonly string[] Chips={"PALM","PUMP","TOWER","WIND","GRASS","FINALE"}; // excavator + plane cut 2026-11-22
        public static string LevelChip(int stage)=>Chips[Mathf.Clamp(stage,0,Chips.Length-1)];
        static Mesh ChipIcon(int stage)=>stage==0?EmberdeepUiIcons.Drop:stage==1?EmberdeepUiIcons.Fist:stage==2?EmberdeepUiIcons.Up:
            stage==5?EmberdeepUiIcons.Star:EmberdeepUiIcons.Tree;

        // ---- Open / close ------------------------------------------------------------------------------
        public bool OpenPause()
        {
            if(!RimUi || PauseOpen || ShopOpen || LevelSelectOpen || QuitConfirmationOpen || (Run.Phase!=FirePhase.Preparing && Run.Phase!=FirePhase.Fighting))return false;
            PauseOpen=true;pauseLocked=false;screensPlaced=default;heldStepper=null;ResetInput(false,true);handMenu?.ResetInput();Refresh();return true;
        }
        public void ClosePause()
        {
            if(!PauseOpen)return;PauseOpen=false;pauseLocked=false;heldStepper=null;screensPlaced=default;
            tokenPoke.Reset();bellHammer.Reset();handMenu?.ResetInput();Refresh();
        }
        public void OpenLevelSelect(int focus)
        {
            if(!RimUi)return;
            LevelSelectOpen=true;PauseOpen=false;SelectedLevel=Mathf.Clamp(focus,0,Run.StageCount-1);selectView=SelectedLevel;selectChangedAt=Time.unscaledTime;
            slap.Reset();handMenu?.ResetInput();Refresh();
        }
        public void CloseLevelSelect(){if(!LevelSelectOpen)return;LevelSelectOpen=false;slap.Reset();handMenu?.ResetInput();Refresh();}
        public void BrowseLevel(int step)
        {
            if(!LevelSelectOpen || ShopOpen)return;
            int next=Mathf.Clamp(SelectedLevel+step,0,Run.StageCount-1);if(next==SelectedLevel)return;
            SelectedLevel=next;selectChangedAt=Time.unscaledTime;Feedback.Impact(false);
        }
        public bool PlaySelectedLevel()
        {
            if(!LevelSelectOpen || !Run.StageUnlocked(SelectedLevel))return false;
            if(Run.Stage!=SelectedLevel || Run.Phase!=FirePhase.Preparing)
            {if(!Run.SelectStage(SelectedLevel))return false;BuildIncident();}
            CloseLevelSelect();return true;
        }
        void ResultsNext()
        {
            if(Run.NeedsMagicUpgrade){OpenShop();return;}
            if(Run.Phase==FirePhase.Cleared){StartNext();if(Run.Phase!=FirePhase.Preparing)return;}
            OpenLevelSelect(Run.Stage);
        }
        void RimRetry(){ClosePause();RetrySector();}
        void RimCamp(){PauseOpen=false;pauseLocked=false;LevelSelectOpen=false;ReturnToMenu();}
        void RimRecenter(){Recenter();}
        public bool StrikeBell()
        {
            if(!RimUi || Run.Phase!=FirePhase.Preparing || Run.CountdownRemaining>=0 || RimBlocksPlay)return false;
            if(!BeginIncident())return false;
            if(Feedback)Feedback.Alarm(); // placeholder alarm ring (FireFeedback), headset test 2026-11-22
            BellStrikes++;if(RimHud)RimHud.RingBell();Hints?.Performed(EmberdeepFirstHints.Move.Bell);return true;
        }
        public bool UpgradeGem(){bool ok=Run.BuyMagicUpgrade();if(ok)Feedback.Rescue(1);Refresh();return ok;}
        public bool UpgradePump(){bool ok=Run.BuyPumpUpgrade();if(ok)Feedback.Rescue(1);Refresh();return ok;}

        // ---- Per frame input (Update, before the ray) ---------------------------------------------------------
        // Returns true when a rim action took this frame (the frame then casts nothing).
        bool TickRimInput(float dt)
        {
            if(!RimUi || !RimHud || !RimHud.Built)return false;
            dt=Mathf.Clamp(dt,0,.1f);
            if(PauseOpen && Run.Phase!=FirePhase.Preparing && Run.Phase!=FirePhase.Fighting)ClosePause();
            if(LevelSelectOpen && !(Run.Phase==FirePhase.Cleared || Run.Phase==FirePhase.Won || Run.Phase==FirePhase.Ready
                || Run.Phase==FirePhase.Preparing && Run.CountdownRemaining<0))CloseLevelSelect();
            // Browsing settles, then the board behind the carousel becomes that level (dimmed).
            if(LevelSelectOpen && !ShopOpen && SelectedLevel!=Run.Stage && Time.unscaledTime-selectChangedAt>BrowseSettle
                && Run.CanSelectStage(SelectedLevel) && Run.SelectStage(SelectedLevel)){BuildIncident();return true;}
            bool panelUp=PauseOpen || LevelSelectOpen || ShopOpen || QuitConfirmationOpen;
            bool tokenLive=!panelUp && RimHud.TokenVisible;
            bool bellLive=!panelUp && RimHud.BellVisible && Run.Phase==FirePhase.Preparing && Run.CountdownRemaining<0;
            if(!Player.IsXR)return DesktopRimInput(tokenLive,bellLive);
            // Fingertip poke on the token (either hand's index tip; a fist never pokes). A pointing hand has three fingers
            // folded, so its four-finger closure reads like a fist: the index curl (ReadSpellHands, this frame) tells
            // them apart (headset test 2026-11-22: only a pinch, with its open fingers, used to poke).
            bool poked=false;
            if(tokenLive)
            {
                EmberdeepFingerPoke.Tip Tip(int i)
                {
                    bool ok=hands.TryReadFingertip(i==0,1,out var p) && !sheepInteraction.OwnsHand(i) && !SpellOwnsHand(i);
                    var spell=spellHands[i];
                    return new EmberdeepFingerPoke.Tip(ok,p,toolHands[i].Usable?toolHands[i].Closure:0,spell.CurlsValid?spell.Index:float.NaN);
                }
                poked=tokenPoke.Sample(Tip(0),Tip(1),RimHud.TokenCenter,RimHud.TokenNormal);
            }
            else tokenPoke.Reset();
            bool struck=false;
            if(bellLive)struck=bellHammer.Sample(BellHand(0),BellHand(1),RimHud.BellTop,RimHud.BellUp,dt);else bellHammer.Reset();
            RimHud.SetHover(bellLive && bellHammer.Hovering,tokenLive && tokenPoke.Hovering);
            if(LevelSelectOpen && !ShopOpen && selectRoot)
            {
                EmberdeepSlapGesture.Hand H(int i)
                {
                    bool left=i==0;var hand=hands.TrackedHand(left);
                    bool open=hands.UsesPortableGrip?hands.IsPortablePalmOpen(left):EmberdeepHandGrasp.IsOpenPalm(hand);
                    return new EmberdeepSlapGesture.Hand(toolHands[i].Usable,toolHands[i].Palm.position,open,objectHands[i].Pinch<.035f);
                }
                int dir=slap.Sample(H(0),H(1),SlapZone(),dt);
                if(dir!=0){Slaps++;BrowseLevel(-dir);Hints?.Performed(EmberdeepFirstHints.Move.Slap);}
            }
            else slap.Reset();
            if(poked){RimHud.PressToken();OpenPause();return true;}
            if(struck)return StrikeBell();
            return false;
        }
        // The bell floats over the middle of the field, right where pieces are carried and set down in Plan: a hand that
        // holds a piece (or draws an excavator line) closes into a grip over the board and could cross the knob going
        // down. Only a free fist strikes; the pump's rules otherwise (fist closure, speed, rearm height).
        EmberdeepFistHammer.Hand BellHand(int index)
        {
            var hand=PumpHand(index);if(PieceOwnsHand(index))hand.Valid=false;return hand;
        }
        // Synthetic bell strikes (tests) through the same rules as tracking, including the carried-piece guard.
        public bool SampleBellHands(EmberdeepFistHammer.Hand left,EmberdeepFistHammer.Hand right,float dt)
        {
            if(!RimUi || !RimHud || !RimHud.BellVisible || Run.Phase!=FirePhase.Preparing || Run.CountdownRemaining>=0 || RimBlocksPlay){bellHammer.Reset();return false;}
            if(PieceOwnsHand(0))left.Valid=false;if(PieceOwnsHand(1))right.Valid=false;
            return bellHammer.Sample(left,right,RimHud.BellTop,RimHud.BellUp,dt) && StrikeBell();
        }
        // The slap zone: a box between the eye and the carousel, aligned with the panel. World space.
        public EmberdeepSlapGesture.Zone SlapZone()
        {
            var eye=Player.ViewCamera.transform.position;var panel=selectRoot?selectRoot.position:eye+Vector3.forward;
            var forward=Vector3.ProjectOnPlane(panel-eye,Vector3.up);if(forward.sqrMagnitude<1e-4f)forward=Vector3.forward;
            var center=eye+(panel-eye)*.42f+Vector3.down*.1f;
            return new EmberdeepSlapGesture.Zone(new Pose(center,Quaternion.LookRotation(forward.normalized,Vector3.up)),new Vector3(.42f,.3f,.26f));
        }
        bool DesktopRimInput(bool tokenLive,bool bellLive)
        {
            var keys=Keyboard.current;var mouse=Mouse.current;
            if(keys!=null)
            {
                if((keys.escapeKey.wasPressedThisFrame || keys.pKey.wasPressedThisFrame))
                {
                    if(PauseOpen){ClosePause();return true;}
                    if(tokenLive){RimHud.PressToken();return OpenPause();}
                }
                if(bellLive && (keys.nKey.wasPressedThisFrame || keys.enterKey.wasPressedThisFrame))return StrikeBell();
                if(LevelSelectOpen && !ShopOpen)
                {
                    if(keys.rightArrowKey.wasPressedThisFrame || keys.dKey.wasPressedThisFrame){BrowseLevel(1);return true;}
                    if(keys.leftArrowKey.wasPressedThisFrame || keys.aKey.wasPressedThisFrame){BrowseLevel(-1);return true;}
                    if(keys.enterKey.wasPressedThisFrame)return PlaySelectedLevel();
                }
            }
            if(mouse!=null && mouse.leftButton.wasPressedThisFrame && (tokenLive || bellLive))
            {
                var ray=Player.ViewCamera.ScreenPointToRay(mouse.position.ReadValue());
                var hits=Physics.RaycastAll(ray,5,~0,QueryTriggerInteraction.Collide);
                System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance)); // the nearest rim object wins
                foreach(var hit in hits)
                {
                    if(tokenLive && hit.collider.transform.IsChildOf(RimHud.Token)){RimHud.PressToken();return OpenPause();}
                    if(bellLive && CarriedPiece<0 && hit.collider.transform.IsChildOf(RimHud.Bell))return StrikeBell();
                }
            }
            return false;
        }

        // ---- Refresh (from RefreshRim) ---------------------------------------------------------------------
        void RefreshScreens()
        {
            if(!Arena)return;
            if(!screensBuilt)
            {
                if(Mathf.Abs(Arena.lossyScale.x-FireMiniature.Scale)>.001f)return;
                BuildScreens();
            }
            float now=Time.unscaledTime;
            if(Run.Phase!=rimPhase)
            {
                if(Run.Phase==FirePhase.Preparing || Run.Phase==FirePhase.Fighting && rimPhase!=FirePhase.Preparing)stageCoinsAtStart=Run.Coins;
                rimPhase=Run.Phase;rimPhaseAt=now;
            }
            if(screensPlaced!=Arena.localToWorldMatrix || RimHud && RimHud.BoardVersion!=screensBoard)PlaceScreens();
            bool world=Arena.gameObject.activeInHierarchy;
            bool outcome=Run.Phase==FirePhase.Cleared || Run.Phase==FirePhase.Won || Run.Phase==FirePhase.Lost;
            bool results=world && outcome && !ShopOpen && !LevelSelectOpen && !QuitConfirmationOpen && now-rimPhaseAt>=ResultsDelay;
            SetActive(pauseRoot,world && PauseOpen);
            SetActive(resultsRoot,results);
            SetActive(selectRoot,world && LevelSelectOpen && !ShopOpen);
            SetActive(upgradesRoot,world && ShopOpen);
            if(results)RefreshResults(now);else ResultsShownAt=-1;
            if(PauseOpen){RefreshBoardControls();TickBoardStepper();}
            if(LevelSelectOpen && !ShopOpen)RefreshLevelSelect();
            if(ShopOpen)RefreshUpgrades();
            if(Feedback && Feedback.Outcome)Feedback.Outcome.Dim=PauseOpen?PauseDim:LevelSelectOpen && !ShopOpen?SelectDim:0;
            // The carousel stands over the board's back half, where the rooster weathervane (L2) stands: it poked up
            // through the middle card and PLAY. The next level's wind shows again when its Plan phase opens.
            if(Weathervane)
            {
                bool vane=Run.BoardRules && !(world && (LevelSelectOpen || ShopOpen)); // the upgrades stand there too (2026-11-22)
                if(Weathervane.gameObject.activeSelf!=vane)Weathervane.gameObject.SetActive(vane);
            }
            // The localizer re-assigns a label's font material whenever its text changes (in its LateUpdate). Let it
            // catch up now, then put the late copies back, so no frame draws a label under the veil.
            if(PausePanelVisible || ResultsVisible || LevelSelectVisible || UpgradesVisible)
            {if(screenLocalizer)screenLocalizer.RefreshLabels(false);KeepScreensAfterVeil();}
        }
        static void SetActive(Transform t,bool on){if(t && t.gameObject.activeSelf!=on)t.gameObject.SetActive(on);}
        void CollectScreenRenderers()
        {
            screenRenderers.Clear();screenOwned.Clear();
            foreach(var root in new[]{pauseRoot,resultsRoot,selectRoot,upgradesRoot})
                if(root)foreach(var r in root.GetComponentsInChildren<Renderer>(true)){screenRenderers.Add(r);screenOwned.Add(r.GetComponent<EmberdeepForestUi>());}
        }
        // Boards and pills (EmberdeepForestUi) and the screen icons own their materials, so their queue is set in place.
        // Shared materials (font atlases, the board frame's art kit) get one late copy each.
        void KeepScreensAfterVeil()
        {
            for(int k=0;k<screenRenderers.Count;k++)
            {
                var r=screenRenderers[k];if(!r)continue;
                r.GetSharedMaterials(materialScratch);bool changed=false;
                for(int i=0;i<materialScratch.Count;i++)
                {
                    var m=materialScratch[i];if(!m || m.renderQueue>=ScreenQueue)continue;
                    if(screenOwned[k] || m==uiIcon){m.renderQueue=ScreenQueue;continue;}
                    if(!afterVeil.TryGetValue(m,out var late) || !late)
                    {
                        late=new Material(m){name=m.name+" (after veil)",renderQueue=m.renderQueue>2500?ScreenTextQueue:ScreenQueue};
                        afterVeil[m]=late;
                    }
                    materialScratch[i]=late;changed=true;
                }
                if(changed)r.sharedMaterials=materialScratch.ToArray();
            }
        }
        void DestroyScreenMaterials(){foreach(var late in afterVeil.Values)if(late)Destroy(late);afterVeil.Clear();if(uiIcon)Destroy(uiIcon);if(uiClay)Destroy(uiClay);ReleaseUpgradePictures();}
        // Panels are laid out in the LEVEL frame: the arena's origin, scale and heading, with world up. On a flat board it is
        // the arena frame itself; on the tilted board (EmberdeepPlayArea.Tilted pitches the arena about its front row) the
        // panels still stand upright and their angles stay degrees under the real eye level, measured to where the strip and
        // the back crowns really are. Anchors come back in arena units (the roots are the arena's children).
        Matrix4x4 levelToArena,arenaToLevel;Quaternion arenaInLevel=Quaternion.identity;
        void PlaceScreens()
        {
            var cam=Player?Player.ViewCamera:Camera.main;if(!cam)return;
            var heading=Vector3.ProjectOnPlane(Arena.forward,Vector3.up);if(heading.sqrMagnitude<1e-6f)heading=Vector3.forward;
            var yaw=Quaternion.LookRotation(heading.normalized,Vector3.up);
            var level=Matrix4x4.TRS(Arena.position,yaw,Arena.lossyScale);
            arenaToLevel=level.inverse*Arena.localToWorldMatrix;levelToArena=arenaToLevel.inverse;arenaInLevel=Quaternion.Inverse(yaw)*Arena.rotation;
            var eyeArena=Arena.InverseTransformPoint(cam.transform.position);var eye=arenaToLevel.MultiplyPoint3x4(eyeArena);
            var boardArena=RimHud?RimHud.Board:EmberdeepRimHud.DefaultBoard;screensBoard=RimHud?RimHud.BoardVersion:-1;
            var board=TransformBounds(boardArena,arenaToLevel);
            // Pause and results stay over the readout strip; upgrades and level select (the rim stands down) over the forest.
            var rim=EmberdeepRimHud.LayoutFor(boardArena,1,eyeArena);
            var stripTop=rim.Strip+Vector3.up*EmberdeepRimHud.StripHeight*.5f*rim.StripUnitsPerMetre;
            var crowns=new Vector3(eyeArena.x,boardArena.max.y+EmberdeepRimHud.CanopyTop,EmberdeepRimHud.SightRowZ);
            float strip=EmberdeepRimHud.Down(arenaToLevel.MultiplyPoint3x4(stripTop),eye),sky=EmberdeepRimHud.Down(arenaToLevel.MultiplyPoint3x4(crowns),eye);
            // Tilted: the board's own top outline in front of each panel's plane (behind the board, or over it at CarouselDepth).
            ScreensTilted=Quaternion.Angle(arenaInLevel,Quaternion.identity)>TiltedOutlineFrom;
            float behind=board.max.z+BehindBoard/FireMiniature.Scale,over=Mathf.Lerp(board.min.z,board.max.z,CarouselDepth);
            OutlineBehindDown=ScreensTilted?BoardOutlineDown(boardArena,eye,behind):float.PositiveInfinity;
            OutlineOverDown=ScreensTilted?BoardOutlineDown(boardArena,eye,over):float.PositiveInfinity;
            float stripFloor=Mathf.Min(strip,OutlineBehindDown),skyFloor=Mathf.Min(sky,OutlineBehindDown),carouselFloor=Mathf.Min(sky,OutlineOverDown);
            var results=ResultsAnchorFor(board,eye,PanelCentreDown(stripFloor,ResultsBelow));
            var pause=ResultsAnchorFor(board,eye,PanelCentreDown(stripFloor,PauseBelow));
            var upgrades=ResultsAnchorFor(board,eye,PanelCentreDown(skyFloor,UpgradesBelow));
            var carousel=CarouselAnchorFor(board,eye,PanelCentreDown(carouselFloor,CarouselBelow));
            ResultsAnchor=levelToArena.MultiplyPoint3x4(results);CarouselAnchor=levelToArena.MultiplyPoint3x4(carousel);
            resultsRiseFrom=levelToArena.MultiplyPoint3x4(new Vector3(results.x,board.max.y+RiseAbove/FireMiniature.Scale,results.z));
            // The pause panel holds still while it is open: its board steppers move the board under it, and a button that
            // slid away mid-press would lose the ray. It only moves when the board came up into it (a higher board or the
            // tilt raise the strip over its lower edge): then it lifts to clear the strip again.
            bool lift=false;
            if(PauseOpen && pauseLocked)
            {
                float want=PanelCentreDown(stripFloor,PauseBelow),have=EmberdeepRimHud.Down(level.inverse.MultiplyPoint3x4(pauseLockPosition),eye);
                lift=want<have-PauseLiftSlack;
            }
            if(PauseOpen && pauseLocked && !lift)pauseRoot.SetPositionAndRotation(pauseLockPosition,pauseLockRotation);
            else
            {
                Place(pauseRoot,pause,eye,PanelUnitFor(pause,eye,ResultsReference));
                if(PauseOpen){pauseLocked=true;pauseLockPosition=pauseRoot.position;pauseLockRotation=pauseRoot.rotation;}
            }
            Place(resultsRoot,results,eye,PanelUnitFor(results,eye,ResultsReference));
            Place(upgradesRoot,upgrades,eye,PanelUnitFor(upgrades,eye,ResultsReference));
            Place(selectRoot,carousel,eye,PanelUnitFor(carousel,eye,CarouselReference));
            screensPlaced=Arena.localToWorldMatrix;
        }
        bool pauseLocked;Vector3 pauseLockPosition;Quaternion pauseLockRotation;
        public const float PauseLiftSlack=.3f; // deg
        // The last placement's tilt and board outlines (deg under eye level; +inf when flat), for tests and captures.
        public bool ScreensTilted {get;private set;}
        public float OutlineBehindDown {get;private set;}=float.PositiveInfinity;
        public float OutlineOverDown {get;private set;}=float.PositiveInfinity;
        // The highest point (least degrees under eye level) of the board's top outline nearer than depth, straight ahead from the
        // eye (eye and depth in the level frame; board in arena units). The outline: the plinth top's line from its front edge to its
        // back edge and the canopy line over the field rows (CanopyTop over the top, front row to the deepest back row), at the
        // eye's x. Each is a straight line, so its highest point in view is an end of the part nearer than depth. Comparing
        // straight ahead is exact across the panel's width: the outline and a panel's lower edge are both lines across the view.
        float BoardOutlineDown(Bounds boardArena,Vector3 eye,float depth)
        {
            float top=boardArena.max.y,canopy=top+EmberdeepRimHud.CanopyTop,best=float.PositiveInfinity;
            void Line(Vector3 a,Vector3 b)
            {
                a=arenaToLevel.MultiplyPoint3x4(a);b=arenaToLevel.MultiplyPoint3x4(b);
                if(a.z>b.z)(a,b)=(b,a);
                if(a.z>depth)return;
                if(b.z>depth)b=Vector3.Lerp(a,b,(depth-a.z)/Mathf.Max(b.z-a.z,1e-5f));
                best=Mathf.Min(best,Mathf.Min(EmberdeepRimHud.Down(a,eye),EmberdeepRimHud.Down(b,eye)));
            }
            // Arena x = level x (the tilt pitches about the arena's x axis), so x = eye.x is straight ahead in both.
            Line(new Vector3(eye.x,top,boardArena.min.z),new Vector3(eye.x,top,boardArena.max.z));
            Line(new Vector3(eye.x,canopy,EmberdeepBoardLayout.FrontRowZ),new Vector3(eye.x,canopy,EmberdeepRimHud.SightRowZ));
            return best;
        }
        static Bounds TransformBounds(Bounds b,Matrix4x4 m)
        {
            var result=new Bounds(m.MultiplyPoint3x4(b.min),Vector3.zero);
            for(int i=1;i<8;i++)result.Encapsulate(m.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
            return result;
        }
        // anchor and eye in the level frame.
        void Place(Transform root,Vector3 anchor,Vector3 eye,float unit)
        {
            root.localPosition=levelToArena.MultiplyPoint3x4(anchor);root.localScale=Vector3.one*unit/FireMiniature.Scale;
            var look=anchor-eye;look.y*=.35f; // mostly upright: a panel tilted fully to the eye reads as leaning over
            root.localRotation=Quaternion.Inverse(arenaInLevel)*Quaternion.LookRotation(look,Vector3.up);
        }

        void RefreshResults(float now)
        {
            if(ResultsShownAt<0)ResultsShownAt=now;
            float u=Mathf.Clamp01((now-ResultsShownAt)/ResultsRise);u=1-(1-u)*(1-u)*(1-u);
            resultsRoot.localPosition=Vector3.LerpUnclamped(resultsRiseFrom,ResultsAnchor,u);
            bool lost=Run.Phase==FirePhase.Lost;
            resultStarRow.gameObject.SetActive(!lost);resultCharredRow.gameObject.SetActive(lost);
            if(lost)Say(resultCharred,$"{Mathf.Min(Run.Burnt,Run.BurnLimit)}/{Run.BurnLimit}"); // same clamp as the rim meter
            for(int i=0;i<3;i++)
            {
                bool lit=i<Run.Stars;float pop=lit?Mathf.Clamp01((now-ResultsShownAt-.35f-i*.18f)/.25f):1;
                UiTint(resultStars[i],lit && pop>0?UiGold:UiStarOff);resultStars[i].transform.localScale=Vector3.one*.16f*(lit?Mathf.Lerp(.4f,1,pop)+.25f*Mathf.Sin(pop*Mathf.PI):1);
            }
            int saved=0;foreach(var a in Run.Animals)if(a.State==AnimalState.Rescued || a.State==AnimalState.Celebrating)saved++;
            Say(resultSheep,$"{saved}/{Run.Animals.Length}");
            Say(resultCoins,$"{Run.Coins}");
            int earned=Mathf.Max(0,Run.Coins-stageCoinsAtStart);Say(resultEarned,earned>0?$"+{earned}":"");
            ShowButton(resultNext,!lost);ShowButton(resultRetry,!lost);ShowButton(resultUpgrades,!lost);
            ShowButton(resultRetryBig,lost);ShowButton(resultCamp,lost);
            resultUpgrades.Interactable=Run.InSupply;RefreshUpgradeStar(resultsStar,resultUpgrades,now);
            var theme=resultNext.GetComponent<EmberdeepForestUi>();if(theme)theme.Guided=!lost;
            var retryTheme=resultRetryBig.GetComponent<EmberdeepForestUi>();if(retryTheme)retryTheme.Guided=lost;
        }
        void RefreshLevelSelect()
        {
            float dt=Mathf.Min(Time.unscaledDeltaTime,.1f);
            selectView=Mathf.Lerp(selectView,SelectedLevel,1-Mathf.Exp(-dt*12));if(Mathf.Abs(selectView-SelectedLevel)<.001f)selectView=SelectedLevel;
            for(int k=0;k<3;k++)
            {
                var card=cards[k];int stage=SelectedLevel+k-1;bool exists=stage>=0 && stage<Run.StageCount;
                card.Root.gameObject.SetActive(exists);if(!exists)continue;
                float offset=stage-selectView,distance=Mathf.Abs(offset);
                card.Root.localPosition=new Vector3(offset*CardStep,0,distance*.12f);
                card.Root.localScale=Vector3.one*Mathf.Lerp(1,.74f,Mathf.Clamp01(distance));
                bool unlocked=Run.StageUnlocked(stage);float fade=Mathf.Lerp(1,.55f,Mathf.Clamp01(distance));
                if(card.Stage!=stage)
                {
                    card.Stage=stage;Say(card.Number,(stage+1).ToString());Say(card.Name,LevelName(Run,stage));Say(card.Chip,LevelChip(stage));
                    card.ChipIcon.GetComponent<MeshFilter>().sharedMesh=ChipIcon(stage);
                }
                card.Number.gameObject.SetActive(unlocked);card.Lock.gameObject.SetActive(!unlocked);
                card.Number.color=UiCream*fade;card.Name.color=(unlocked?UiCream:UiDim)*fade;card.Chip.color=UiCream*fade;
                for(int i=0;i<3;i++)UiTint(card.Stars[i],(i<Run.IncidentStars[Mathf.Clamp(stage,0,Run.IncidentStars.Length-1)]?UiGold:UiStarOff)*fade);
                UiTint(card.ChipIcon,UiCream*fade);UiTint(card.Lock,UiDim);
            }
            selectPrevious.gameObject.SetActive(SelectedLevel>0);selectNext.gameObject.SetActive(SelectedLevel<Run.StageCount-1);
            if(selectPreviousArrow.gameObject.activeSelf!=SelectedLevel>0)selectPreviousArrow.gameObject.SetActive(SelectedLevel>0);
            if(selectNextArrow.gameObject.activeSelf!=SelectedLevel<Run.StageCount-1)selectNextArrow.gameObject.SetActive(SelectedLevel<Run.StageCount-1);
            float pulse=.8f+.2f*Mathf.Sin(Time.unscaledTime*3);
            UiTint(selectPreviousArrow,(selectPrevious.IsHovered?Color.white:UiGold)*pulse);UiTint(selectNextArrow,(selectNext.IsHovered?Color.white:UiGold)*pulse);
            selectPlay.Interactable=Run.StageUnlocked(SelectedLevel) && !Run.NeedsMagicUpgrade;
            var theme=selectPlay.GetComponent<EmberdeepForestUi>();if(theme)theme.Guided=selectPlay.Interactable;
            RefreshUpgradeStar(selectStar,selectUpgrades,Time.unscaledTime);
        }
        // Sets a label only when its source changes: the localizer rewrites the shown text, so comparing against
        // TextMesh.text would re-translate every frame.
        readonly Dictionary<TextMesh,string> said=new();
        void Say(TextMesh label,string value){if(!label || said.TryGetValue(label,out var prior) && prior==value)return;said[label]=value;label.text=value;}
        static void ShowButton(WorkshopButton button,bool visible)
        {
            if(!button)return;if(button.gameObject.activeSelf!=visible)button.gameObject.SetActive(visible);
            var deco=button.transform.parent.Find(button.name+" face");if(deco && deco.gameObject.activeSelf!=visible)deco.gameObject.SetActive(visible);
        }

        // ---- Build -----------------------------------------------------------------------------------------
        void BuildScreens()
        {
            screensBuilt=true;uiTint=new MaterialPropertyBlock();
            var source=Resources.Load<Material>("EmberdeepMenuUnlit");
            uiIcon=source?new Material(source){name="Screen icons"}:new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Screen icons"};
            pauseRoot=ScreenRoot("Pause panel");resultsRoot=ScreenRoot("Results panel");selectRoot=ScreenRoot("Level select");upgradesRoot=ScreenRoot("Upgrades panel");
            PlaceScreens(); // ForestUi bakes world sizes, so the roots take their final scale before any board is built
            // Pause: title, the run's buttons, then the board's placement (headset test 2: adjust it in a level).
            Board(pauseRoot,"Pause board",new Vector2(2.0f,PauseTop-PauseBottom),(PauseTop+PauseBottom)*.5f);
            UiIconAt(pauseRoot,EmberdeepUiIcons.PauseBars,new Vector3(-.42f,PauseTitleY,-.04f),.09f,UiCream);
            Word(pauseRoot,"PAUSED",new Vector3(.05f,PauseTitleY,-.04f),.06f,1.1f);
            UiButton("Pause resume",pauseRoot,new Vector3(-.5f,PauseButtonsY,0),new Vector2(.7f,.24f),EmberdeepUiIcons.Triangle,"RESUME",ClosePause);
            // The small buttons sit .02-.08 further in than before: RECENTER's caption ran under the frame's right edge.
            UiButton("Pause retry",pauseRoot,new Vector3(.02f,PauseButtonsY,0),new Vector2(.28f,.2f),EmberdeepUiIcons.Retry,"RETRY",RimRetry);
            UiButton("Pause camp",pauseRoot,new Vector3(.33f,PauseButtonsY,0),new Vector2(.28f,.2f),EmberdeepUiIcons.Tent,"CAMP",RimCamp);
            UiButton("Pause recenter",pauseRoot,new Vector3(.64f,PauseButtonsY,0),new Vector2(.28f,.2f),EmberdeepUiIcons.Target,"RECENTER",RimRecenter);
            BuildBoardControls();
            // Results
            Board(resultsRoot,"Results board",new Vector2(2.0f,.92f));
            // Lost: the charred count takes the stars' place (the rim meter glows full too).
            resultCharredRow=new GameObject("Charred").transform;resultCharredRow.SetParent(resultsRoot,false);resultCharredRow.localPosition=new Vector3(0,.26f,-.04f);
            UiIconAt(resultCharredRow,EmberdeepUiIcons.Tree,new Vector3(-.12f,0,0),.13f,new Color(1,.42f,.14f));
            resultCharred=Word(resultCharredRow,"",new Vector3(.06f,0,0),.07f,.36f);resultCharred.color=new Color(1,.55f,.3f);
            resultStarRow=new GameObject("Stars").transform;resultStarRow.SetParent(resultsRoot,false);resultStarRow.localPosition=new Vector3(0,.26f,-.04f);
            for(int i=0;i<3;i++)resultStars[i]=UiIconAt(resultStarRow,EmberdeepUiIcons.Star,new Vector3((i-1)*.2f,0,0),.16f,UiStarOff);
            UiIconAt(resultsRoot,EmberdeepUiIcons.Sheep,new Vector3(-.62f,.04f,-.04f),.12f,UiCream);
            resultSheep=Word(resultsRoot,"",new Vector3(-.36f,.04f,-.04f),.065f,.4f);
            UiIconAt(resultsRoot,EmberdeepUiIcons.Coin,new Vector3(.1f,.04f,-.04f),.1f,UiGold);
            resultCoins=Word(resultsRoot,"",new Vector3(.34f,.04f,-.04f),.065f,.36f);
            resultEarned=Word(resultsRoot,"",new Vector3(.66f,.04f,-.04f),.045f,.3f);resultEarned.color=new Color(.6f,1,.7f);
            resultNext=UiButton("Results next",resultsRoot,new Vector3(-.32f,ResultsRowY,0),new Vector2(.8f,.24f),EmberdeepUiIcons.Triangle,"NEXT",ResultsNext);
            resultRetry=UiButton("Results retry",resultsRoot,new Vector3(.3f,ResultsRowY,0),new Vector2(.28f,.2f),EmberdeepUiIcons.Retry,"RETRY",RimRetry);
            resultUpgrades=UiButton("Results upgrades",resultsRoot,new Vector3(.7f,ResultsRowY,0),new Vector2(.28f,.2f),EmberdeepUiIcons.Up,"UPGRADES",OpenShop);
            resultsStar=BuildUpgradeStar(resultsRoot,new Vector3(.7f,ResultsRowY,0),new Vector2(.28f,.2f));
            resultRetryBig=UiButton("Results retry big",resultsRoot,new Vector3(-.2f,ResultsRowY,0),new Vector2(.8f,.24f),EmberdeepUiIcons.Retry,"RETRY",RimRetry);
            resultCamp=UiButton("Results camp",resultsRoot,new Vector3(.5f,ResultsRowY,0),new Vector2(.28f,.2f),EmberdeepUiIcons.Tent,"CAMP",RimCamp);
            // Level select
            for(int k=0;k<3;k++)cards[k]=BuildCard(selectRoot,k);
            float arrowSpan=ArrowX+ArrowSize*.6f-(CardStep-.25f);
            selectPrevious=HitTarget("Level previous",selectRoot,new Vector3(-(CardStep-.25f+arrowSpan*.5f),0,-.02f),new Vector2(arrowSpan,.62f),()=>BrowseLevel(-1));
            selectNext=HitTarget("Level next",selectRoot,new Vector3(CardStep-.25f+arrowSpan*.5f,0,-.02f),new Vector2(arrowSpan,.62f),()=>BrowseLevel(1));
            selectPreviousArrow=UiIconAt(selectRoot,EmberdeepUiIcons.Chevron,new Vector3(-ArrowX,0,-.03f),ArrowSize,UiGold);selectPreviousArrow.name="Level previous arrow";
            selectPreviousArrow.transform.localRotation=Quaternion.Euler(0,0,180);
            selectNextArrow=UiIconAt(selectRoot,EmberdeepUiIcons.Chevron,new Vector3(ArrowX,0,-.03f),ArrowSize,UiGold);selectNextArrow.name="Level next arrow";
            selectPlay=UiButton("Level play",selectRoot,new Vector3(0,-.62f,0),new Vector2(.7f,.24f),EmberdeepUiIcons.Triangle,"PLAY",()=>PlaySelectedLevel());
            UiButton("Level camp",selectRoot,new Vector3(-.78f,-.62f,0),new Vector2(.28f,.2f),EmberdeepUiIcons.Tent,"CAMP",RimCamp);
            selectUpgrades=UiButton("Level upgrades",selectRoot,new Vector3(.78f,-.62f,0),new Vector2(.28f,.2f),EmberdeepUiIcons.Up,"UPGRADES",OpenShop);
            selectStar=BuildUpgradeStar(selectRoot,new Vector3(.78f,-.62f,0),new Vector2(.28f,.2f));
            // Upgrades: FireRescueGame.Upgrades.cs
            BuildUpgrades();
            CollectScreenRenderers();KeepScreensAfterVeil();
            foreach(var r in new[]{pauseRoot,resultsRoot,selectRoot,upgradesRoot})r.gameObject.SetActive(false);
            PlaceScreens();
            screenLocalizer=GetComponent<EmberdeepLocalizedText>();
            if(screenLocalizer){screenLocalizer.RefreshLabels();KeepScreensAfterVeil();}
        }
        Transform ScreenRoot(string name){var t=new GameObject(name).transform;t.SetParent(Arena,false);t.localScale=Vector3.one*PanelUnit/FireMiniature.Scale;return t;}
        void Board(Transform parent,string name,Vector2 size,float centreY=0)
        {
            var go=Shape(name,PrimitiveType.Cube,parent,new Vector3(0,centreY,.025f),new Vector3(size.x,size.y,.06f),PanelMaterial);EmberdeepForestUi.Apply(go);
        }
        // A pill with only an icon (steppers).
        WorkshopButton IconButton(string command,Transform parent,Vector3 at,Vector2 size,Mesh icon,System.Action press)
        {
            var go=Shape(command,PrimitiveType.Cube,parent,at,new Vector3(size.x,size.y,.055f),PanelMaterial,true);
            EmberdeepForestUi.Apply(go,true);
            var button=go.AddComponent<WorkshopButton>();button.Command=command;button.Pressed=press;buttons.Add(button);
            var face=new GameObject(command+" face").transform;face.SetParent(parent,false);face.localPosition=at+Vector3.back*.04f;
            UiIconAt(face,icon,Vector3.zero,size.y*.5f,UiCream);
            return button;
        }
        // A pill with only a word (the FLAT / TILTED pair).
        WorkshopButton WordButton(string command,Transform parent,Vector3 at,Vector2 size,string word,System.Action press)
        {
            var go=Shape(command,PrimitiveType.Cube,parent,at,new Vector3(size.x,size.y,.055f),PanelMaterial,true);
            EmberdeepForestUi.Apply(go,true);
            var button=go.AddComponent<WorkshopButton>();button.Command=command;button.Pressed=press;buttons.Add(button);
            var face=new GameObject(command+" face").transform;face.SetParent(parent,false);face.localPosition=at+Vector3.back*.04f;
            Word(face,word,Vector3.zero,.045f,size.x*.8f);
            return button;
        }
        Renderer UiIconAt(Transform parent,Mesh mesh,Vector3 at,float size,Color color)
        {var r=EmberdeepUiIcons.Icon("Icon",parent,mesh,uiIcon,at,size);UiTint(r,color);return r;}
        void UiTint(Renderer r,Color c){if(!r)return;uiTint.Clear();uiTint.SetColor("_BaseColor",c);r.SetPropertyBlock(uiTint);}
        TextMesh Word(Transform parent,string value,Vector3 at,float size,float width)
        {
            var t=Text(parent,value,at,size,UiCream);
            var fit=t.gameObject.AddComponent<EmberdeepTextBounds>();fit.Width=width;fit.Height=size*2.4f;return t;
        }
        // A pill with an icon and one word. Big buttons: icon left of the word. Small: icon on the pill, word under it.
        WorkshopButton UiButton(string command,Transform parent,Vector3 at,Vector2 size,Mesh icon,string caption,System.Action press)
        {
            var go=Shape(command,PrimitiveType.Cube,parent,at,new Vector3(size.x,size.y,.055f),PanelMaterial,true);
            EmberdeepForestUi.Apply(go,true);
            var button=go.AddComponent<WorkshopButton>();button.Command=command;button.Pressed=press;buttons.Add(button);
            var face=new GameObject(command+" face").transform;face.SetParent(parent,false);face.localPosition=at+Vector3.back*.04f;
            bool big=size.x>=.6f;
            if(big)
            {
                UiIconAt(face,icon,new Vector3(-size.x*.32f,0,0),size.y*.46f,UiCream);
                Word(face,caption,new Vector3(size.x*.1f,0,0),.055f,size.x*.5f);
            }
            else
            {
                UiIconAt(face,icon,Vector3.zero,size.y*.55f,UiCream);
                Word(face,caption,new Vector3(0,-size.y*.5f-CaptionGap,0),.04f,size.x*1.3f);
            }
            return button;
        }
        WorkshopButton HitTarget(string command,Transform parent,Vector3 at,Vector2 size,System.Action press)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=command;go.transform.SetParent(parent,false);go.transform.localPosition=at;
            go.transform.localScale=new Vector3(size.x,size.y,.05f);go.GetComponent<Collider>().isTrigger=true;
            DestroyImmediate(go.GetComponent<MeshRenderer>());DestroyImmediate(go.GetComponent<MeshFilter>());
            var button=go.AddComponent<WorkshopButton>();button.Command=command;button.Pressed=press;buttons.Add(button);return button;
        }
        LevelCard BuildCard(Transform parent,int index)
        {
            var c=new LevelCard{Root=new GameObject("Level card "+index).transform};c.Root.SetParent(parent,false);
            var board=Shape("Level card board",PrimitiveType.Cube,c.Root,new Vector3(0,0,.02f),new Vector3(.62f,.8f,.04f),PanelMaterial);EmberdeepForestUi.Apply(board);
            c.Number=Text(c.Root,"",new Vector3(0,.17f,-.03f),.17f,UiCream);
            c.Lock=UiIconAt(c.Root,EmberdeepUiIcons.Lock,new Vector3(0,.17f,-.03f),.2f,UiDim);
            c.Name=Word(c.Root,"",new Vector3(0,-.04f,-.03f),.04f,.54f);
            for(int i=0;i<3;i++)c.Stars[i]=UiIconAt(c.Root,EmberdeepUiIcons.Star,new Vector3((i-1)*.1f,-.15f,-.03f),.085f,UiStarOff);
            var chip=Shape("Level chip",PrimitiveType.Cube,c.Root,new Vector3(0,-.28f,-.005f),new Vector3(.44f,.1f,.02f),PanelMaterial);EmberdeepForestUi.Apply(chip,true);
            c.ChipIcon=UiIconAt(c.Root,EmberdeepUiIcons.Drop,new Vector3(-.15f,-.28f,-.03f),.065f,UiCream);
            c.Chip=Word(c.Root,"",new Vector3(.04f,-.28f,-.03f),.036f,.28f);
            return c;
        }
    }
}

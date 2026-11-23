using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FireRescue
{
    // LoopSpec §3 water loop: the water hand's open palm aims, a fingertip of the other hand
    // on its gem sprays, the gem is the only water store, and a fist-hammer pump beside the
    // player refills it. EmberdeepWaterHand picks which hand is the water hand.
    public sealed partial class FireRescueGame
    {
        readonly EmberdeepPalmGesture palmMagic=new();
        EmberdeepManaCrystal manaCrystal;
        EmberdeepPalmAimVfx palmMotes;
        EmberdeepInteractionDemo palmLessonDemo;
        readonly Vector3[] magicTouchTips=new Vector3[5];
        EmberdeepHammerPumpView pumpView;
        readonly EmberdeepFistHammer hammer=new();
        readonly EmberdeepPumpRhythm rhythm=new();
        EmberdeepWaterAudio waterAudio;
        readonly List<EmberdeepPalmAimVfx.Crown> aimCrowns=new();
        float gemDisplay=-1,gemPulse;
        int magicRenderFrame=-1;
        bool waterLeft=true;
        // Desktop has no hand: the gem sits low-left in view, beside the palm-water origin.
        static readonly Vector3 DesktopGemLocal=new(-.165f,-.112f,.42f), DesktopPalmLocal=new(-.12f,-.14f,.30f);
        public bool MagicSpraying {get;private set;}
        public bool MagicAiming=>palmMagic.Aiming;
        public EmberdeepManaCrystal ManaCrystal=>manaCrystal;
        public EmberdeepHammerPumpView Pump=>pumpView;
        public EmberdeepFistHammer Hammer=>hammer;
        public EmberdeepPumpRhythm PumpRhythm=>rhythm;
        public EmberdeepPalmAimVfx AimVfx=>palmMotes;
        public EmberdeepWaterAudio WaterAudio=>waterAudio;
        public bool WaterHandLeft=>waterLeft;
        public bool GemSputtering {get;private set;}
        public float GemDisplayFill=>Mathf.Max(0,gemDisplay);
        public int PumpStrikes {get;private set;}
        public int PumpBeatStrikes {get;private set;}
        int WaterHand=>waterLeft?0:1;
        int OtherHand=>waterLeft?1:0;
        string GemName=>waterLeft?"Left hand mana crystal":"Right hand mana crystal";
        bool MagicOwnsHand(int hand)=>(palmMagic.Aiming && (hand==WaterHand || palmMagic.Casting)) || SpellOwnsHand(hand);
        void BuildMagic()
        {
            waterLeft=EmberdeepWaterHand.Left;
            manaCrystal=new GameObject(GemName).AddComponent<EmberdeepManaCrystal>();
            manaCrystal.transform.SetParent(transform,false);manaCrystal.transform.localScale=Vector3.one*EmberdeepWaterTuning.GemScale;
            manaCrystal.gameObject.SetActive(false);manaCrystal.FollowHand(handVisuals,waterLeft);
            palmMotes=new GameObject("Palm to target water motes").AddComponent<EmberdeepPalmAimVfx>();palmMotes.transform.SetParent(transform,false);
            palmLessonDemo=new GameObject("Goddess hand visual lesson").AddComponent<EmberdeepInteractionDemo>();
            palmLessonDemo.transform.SetParent(transform,false);palmLessonDemo.Show(EmberdeepInteractionDemo.Action.Goddess);palmLessonDemo.SetCaptionVisible(false);
            pumpView=new GameObject("Hammer pump").AddComponent<EmberdeepHammerPumpView>();pumpView.transform.SetParent(transform,false);
            pumpView.Build(PanelMaterial,PanelMaterial,WaterMaterial,art);pumpView.SplashLanded+=OnSplashLanded;
            pumpView.gameObject.SetActive(Run.SpellMode);
            waterAudio=gameObject.AddComponent<EmberdeepWaterAudio>();waterAudio.Initialize();
            MirrorLessonDemo();PositionPump();ConfigureHandOcclusion();
        }
        // Called from PositionWaterStations: beside the player on the FREE-hand side (EmberdeepWaterTuning.PumpLeft), so
        // the free fist strikes while the gem on the water hand stays in view.
        void PositionPump()
        {
            if(!pumpView || !Arena)return;
            float side=EmberdeepWaterTuning.PumpSide(waterLeft);
            // A real fist strikes it: on a Tilted board it stays world-upright where the flat board puts it (the upright
            // frame, FireRescueGame.Placement.cs); flat, that frame is the arena itself.
            // Height follows the play-area height option in part (EmberdeepBoardLayout.PumpHeightFor); the legacy board has none.
            float height=EmberdeepBoardLayout.PumpHeightFor(Run!=null && Run.CompactBoard?EmberdeepPlayArea.Height:EmberdeepPlayArea.DefaultHeight);
            var local=new Vector3(side*EmberdeepWaterTuning.PumpSideX,height,EmberdeepWaterTuning.PumpForward);
            bool tilted=BoardTilt!=0;
            pumpView.transform.position=tilted?UprightPoint(local):Arena.TransformPoint(local);
            pumpView.SetSide(EmberdeepWaterTuning.PumpLeft(waterLeft),tilted?UprightRotation:Arena.rotation);
        }
        // EmberdeepHandVisuals.Seen: the ISDK chain bridges a lost hand at its last pose; palm magic must not aim from it.
        bool HandSeen(bool left)=>!handVisuals || handVisuals.Seen(left);
        // The setting can change from anywhere (camp page, dev key); the fireground follows it.
        void SyncWaterHand()
        {
            bool left=EmberdeepWaterHand.Left;
            if(left==waterLeft || !manaCrystal)return;
            waterLeft=left;palmMagic.Reset();MagicSpraying=false;hammer.Reset();
            manaCrystal.FollowHand(handVisuals,left);manaCrystal.name=GemName;
            foreach(var crystal in spellCrystals)if(crystal)crystal.FollowHand(handVisuals,left);
            MirrorLessonDemo();PositionPump();
        }
        void MirrorLessonDemo()
        {
            // The blue demonstration hands are authored left-palm; a mirrored copy teaches the right.
            if(palmLessonDemo)palmLessonDemo.transform.localScale=new Vector3(waterLeft?1:-1,1,1);
        }
        void ProcessWaterDevKeys()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var keys=Keyboard.current;if(keys==null)return;
            if(keys.f8Key.wasPressedThisFrame)EmberdeepWaterHand.Toggle();
            if(keys.f9Key.wasPressedThisFrame){EmberdeepWaterTuning.RhythmEnabled=!EmberdeepWaterTuning.RhythmEnabled;rhythm.Reset();}
#endif
        }
        void ReadMagicHands(float dt)
        {
            SyncWaterHand();
            // The pause / level-select panels freeze the board like the shop: no palm water, and the menu ray keeps the hand.
            bool free=!ShopOpen && !QuitConfirmationOpen && !QuitRequested && !RimBlocksPlay && !Run.Supply.Locked && !refill.OwnsControl
                && !ToolOwnsHand(0) && !ToolOwnsHand(1) && !sheepInteraction.OwnsHand(0) && !sheepInteraction.OwnsHand(1) && Run.Phase==FirePhase.Fighting && !SpellBusy
                && HandSeen(waterLeft) && !PieceBlocksWater(WaterHand); // a hand carrying or just releasing a piece never sprays (towers)
            bool open=hands.UsesPortableGrip?hands.IsPortablePalmOpen(waterLeft):EmberdeepHandGrasp.IsOpenPalm(hands.TrackedHand(waterLeft));
            int mask=0;if(HandSeen(!waterLeft))for(int i=0;i<5;i++)if(hands.TryReadFingertip(!waterLeft,i,out magicTouchTips[i]))mask|=1<<i;
            assistPrevious=palmMagic.AssistTree;assistPass=-1;
            palmMagic.Sample(toolHands[WaterHand],magicTouchTips,mask,open,free,Arena,Run.MagicRange,dt,PalmAssist);
            jet.MagicPass=assistPass; // a root aim's stream flies through its tree's own sphere (EmberdeepPalmAim.RootPass)
            if(palmMagic.Casting)introStarted=true;
            LogPalmEdges(toolHands[WaterHand].Closure,mask);
        }
        // Development device builds: one EMBERDEEP_GEM line per aim or press edge (headset test 3 tuning: the openness the
        // aim starts and stops at, and whether a press rode a blink of the water hand).
        bool loggedAim,loggedCast;
        void LogPalmEdges(float closure,int tips)
        {
            if(palmMagic.Aiming==loggedAim && palmMagic.Casting==loggedCast)return;
            bool aimEdge=palmMagic.Aiming!=loggedAim;loggedAim=palmMagic.Aiming;loggedCast=palmMagic.Casting;
            if(!EmberdeepHandRayMenu.DeviceLog)return;
            Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,"EMBERDEEP_GEM {0} hand={1} open={2:F2} tips={3} bridged={4} presses={5}",
                aimEdge?(palmMagic.Aiming?"aim=on":"aim=off"):(palmMagic.Casting?"press":"lift"),waterLeft?'L':'R',
                EmberdeepPalmGesture.Openness(closure),tips,palmMagic.Bridged?1:0,palmMagic.Presses));
        }
        // Production pump input, called once per live frame. XR: tracked fists. Desktop: R.
        void ProcessHammerPump(float dt)
        {
            if(!pumpView || !pumpView.gameObject.activeInHierarchy || Run.Phase!=FirePhase.Fighting){hammer.Reset();return;}
            if(!Player.IsXR)
            {
                if(!interfaceHovered && Keyboard.current!=null && Keyboard.current.rKey.wasPressedThisFrame)StrikePump();
                return;
            }
            if(hammer.Sample(PumpHand(0),PumpHand(1),pumpView.CapTop,pumpView.Up,dt))StrikePump();
        }
        EmberdeepFistHammer.Hand PumpHand(int index)
        {
            var hand=toolHands[index];
            bool free=hand.Usable && !sheepInteraction.OwnsHand(index) && !interfacePhysicalBlocked[index] && !SpellOwnsHand(index);
            return new EmberdeepFistHammer.Hand(free,hand.Contact,hand.Closure);
        }
        // Synthetic hand samples (tests, simulator scripts) use the same strike rules as tracking.
        public bool SamplePumpHands(EmberdeepFistHammer.Hand left,EmberdeepFistHammer.Hand right,float dt)
        {
            if(!pumpView || !pumpView.gameObject.activeInHierarchy || Run.Phase!=FirePhase.Fighting){hammer.Reset();return false;}
            if(!hammer.Sample(left,right,pumpView.CapTop,pumpView.Up,dt))return false;
            StrikePump();return true;
        }
        void StrikePump()
        {
            var grade=rhythm.StrikeGraded(Time.unscaledTimeAsDouble,EmberdeepWaterTuning.RhythmEnabled);
            bool onBeat=grade==EmberdeepPumpGrade.OnBeat;
            float units=Run.PumpGem(Run.PumpStrikeUnits(onBeat)); // PUMP upgrade (FireRun.Progression)
            introStarted=true;PumpStrikes++;if(onBeat)PumpBeatStrikes++;
            pumpView.Strike(units,grade);if(waterAudio)waterAudio.Strike(grade);
        }
        void OnSplashLanded(float units,bool onBeat)
        {
            gemPulse=onBeat?1:.7f;
            if(waterAudio)waterAudio.Gulp(onBeat);
        }
        void ResetMagic()
        {
            palmMagic.Reset();MagicSpraying=false;GemSputtering=false;if(palmMotes)palmMotes.Hide();
            hammer.Reset();rhythm.Reset();if(pumpView)pumpView.ClearSplashes();gemDisplay=-1;
            if(waterAudio)waterAudio.Silence();
            if(manaCrystal)manaCrystal.gameObject.SetActive(false);
        }
        void RenderMagic()
        {
            if(!manaCrystal || !Player)return;
            // ConfigureHandOcclusion also lands here; time and keys advance once per frame.
            bool fresh=magicRenderFrame!=Time.frameCount;magicRenderFrame=Time.frameCount;
            if(fresh)ProcessWaterDevKeys();
            SyncWaterHand();
            bool available=focused && !paused && !acquiringPose && !returningToCamp && hands && hands.HeadReady;
            float dt=fresh?Time.unscaledDeltaTime:0;
            var camera=Player.ViewCamera;
            bool live=available && Run.Phase==FirePhase.Fighting && !ShopOpen && !QuitConfirmationOpen && !RimBlocksPlay;
            bool visible=available && Player.IsXR && toolHands[WaterHand].Usable && HandSeen(waterLeft); // not on a lost hand's last pose
            bool desktopGem=available && !Player.IsXR && Run.GemMode && !ShopOpen && !QuitConfirmationOpen && Arena && Arena.gameObject.activeInHierarchy;
            manaCrystal.gameObject.SetActive(visible || desktopGem);
            if(visible)
            {
                var h=toolHands[WaterHand];var frame=h.GripRotation;var center=h.Contact;
                manaCrystal.transform.SetPositionAndRotation(center-frame*Vector3.up*.016f,frame);
            }
            else if(desktopGem)
            {
                var view=camera.transform;
                manaCrystal.transform.SetPositionAndRotation(view.TransformPoint(DesktopGemLocal),Quaternion.LookRotation(view.up,view.forward));
            }
            RenderGem(dt,live);
            RenderPump(dt,available,live);
            RenderAim(dt,available,live);
            // Spell flow: the first-time ghost hints (FireRescueGame.Hints.cs) teach the palm instead.
            palmLessonDemo.gameObject.SetActive(!Run.SpellMode && available && !Run.PalmTutorialComplete && Run.IsPalmLesson && Run.Phase==FirePhase.Fighting && !ShopOpen && !QuitConfirmationOpen);
            if(palmLessonDemo.gameObject.activeSelf)
            {palmLessonDemo.transform.position=Arena.TransformPoint(new Vector3(waterLeft?-2.5f:2.5f,4.6f,3.8f));palmLessonDemo.transform.rotation=camera.transform.rotation;}
        }
        void RenderGem(float dt,bool live)
        {
            dt=float.IsFinite(dt)?Mathf.Clamp(dt,0,.1f):0;
            // Water still flying from the spout is shown when it lands, not when the strike lands.
            float inFlight=pumpView?pumpView.InFlightUnits/Mathf.Max(1,Run.MagicCapacity):0;
            float target=Mathf.Clamp01(Run.GemFill01-inFlight);
            if(gemDisplay<0)gemDisplay=target;
            bool rising=target>gemDisplay+.002f;
            gemDisplay=Mathf.MoveTowards(gemDisplay,target,dt*(rising?1.4f:2.5f));
            gemPulse=Mathf.MoveTowards(gemPulse,0,dt*3);
            float flow=MagicSpraying?-1:rising?1:Run.GemPassiveRefilling?.45f:0;
            bool trying=live && (Player.IsXR?palmMagic.Casting:Keyboard.current!=null && Keyboard.current.spaceKey.isPressed);
            GemSputtering=trying && Run.GemMode && Run.GemEmpty;
            manaCrystal.SetState(gemDisplay,flow,Run.GemMode && gemDisplay<.01f && Run.GemEmpty,gemPulse);
            if(waterAudio){waterAudio.Tick(GemSputtering,dt);if(Run.GemMode)waterAudio.Fill(gemDisplay,dt);}
        }
        void RenderPump(float dt,bool available,bool live)
        {
            if(!pumpView)return;
            bool shown=Run.SpellMode && Arena && Arena.gameObject.activeInHierarchy;
            if(pumpView.gameObject.activeSelf!=shown)pumpView.gameObject.SetActive(shown);
            if(!shown)return;
            double now=Time.unscaledTimeAsDouble;
            bool beat=live && EmberdeepWaterTuning.RhythmEnabled && rhythm.Active(now);
            // LoopSpec §3.1: an empty gem lights the pump up.
            bool glow=live && Run.GemEmpty && gemDisplay<.03f;
            pumpView.Render(dt,available?Player.ViewCamera:null,manaCrystal.transform.position,glow,live && hammer.Hovering,beat,rhythm.Phase01(now),beat && rhythm.InWindow(now));
        }
        void RenderAim(float dt,bool available,bool live)
        {
            var camera=Player.ViewCamera;
            bool clear=live && !SpellBusy && !fan.Ready && Arena;
            bool aiming;Vector3 origin,target;int assisted=-1;
            if(Player.IsXR){aiming=clear && palmMagic.Aiming;origin=palmMagic.Origin;target=palmMagic.Target;assisted=palmMagic.AssistTree;}
            else
            {
                aiming=clear && Run.GemMode && !grip.Held && !interfaceHovered && Mouse.current!=null;
                origin=camera.transform.TransformPoint(DesktopPalmLocal);
                target=aiming?WaterJet.MouseTarget(camera.ScreenPointToRay(Mouse.current.position.ReadValue()),Arena,waterTargets):origin;
            }
            if(!aiming){palmMotes.Hide();return;}
            var shot=SolvePalmShot(origin,target,aimReach,aimSheep);SetAssist(ref shot,assisted);LastPalmShot=shot;
            aimCrowns.Clear();
            foreach(int i in aimReach)
                aimCrowns.Add(new EmberdeepPalmAimVfx.Crown{Center=Arena.TransformPoint(Run.Trees[i].Position+Vector3.up*(EmberdeepWaterTuning.CrownRingHeight-.7f)),
                    Radius=EmberdeepWaterTuning.CrownRingRadius*FireMiniature.Scale*(1+(i%4)*.08f),Burning=Run.Trees[i].State==TreeState.Burning});
            // A ring round each burning sheep the predicted splash puts out (the same FireRun.WaterReachesSheep rule).
            foreach(int i in aimSheep)
                aimCrowns.Add(new EmberdeepPalmAimVfx.Crown{Center=Arena.TransformPoint(Run.Animals[i].Position+Vector3.up*SheepRingHeight),
                    Radius=SheepRingRadius*FireMiniature.Scale,Burning=true});
            palmMotes.ShowAim(true,origin,shot.Velocity,Run.MagicRange,shot.Hit,shot.Impact,shot.Normal,camera,!Run.GemEmpty,MagicSpraying,dt,aimCrowns,aimCrowns.Count,Arena.up);
        }
        // Palm water flight speed (m/s). The spray in FireRescueGame.cs launches with the same WaterJet.AimVelocity call.
        public const float PalmWaterSpeed=2.8f;
        public struct PalmShot
        {
            public bool Aiming,Hit;public Vector3 Origin,Target,Velocity,Impact,Normal;
            public int ImpactTarget; // waterTargets index the water hits first (-1: the board), valid while Hit
            public int AssistTree;   // the tree the aim snapped to (-1: none, see AssistSheep, or the plain ground ray)
            public int AssistSheep;  // the burning sheep the aim snapped to (-1: none)
        }
        // Sheep rings: arena units over the sheep's ground point (mid-flame) and radius, x FireMiniature.Scale.
        const float SheepRingHeight=.55f,SheepRingRadius=.5f;
        readonly List<int> aimReach=new(),aimSheep=new();
        void SetAssist(ref PalmShot shot,int target)
        {
            bool sheep=EmberdeepPalmAim.IsSheep(target,Run.Trees.Length,out int index);
            shot.AssistTree=target>=0 && !sheep?target:-1;shot.AssistSheep=target>=0 && sheep?index:-1;
        }
        // One flight for the preview and the spray: the same velocity, the first surface WaterJet hits, and exactly the trees
        // FireRun.ApplyWater changes from there (OnWaterImpact: palm water on a cabin waters only the cabin). Burnt and dug
        // trees get no ring: water changes nothing on them. sheep: the burning sheep that splash puts out (ApplyAnimalWater).
        PalmShot SolvePalmShot(Vector3 origin,Vector3 target,List<int> reached,List<int> sheep=null)
        {
            var shot=new PalmShot{Aiming=true,Origin=origin,Target=target,ImpactTarget=-1,AssistTree=-1,AssistSheep=-1};
            shot.Velocity=WaterJet.AimVelocity(origin,target,PalmWaterSpeed);
            shot.Hit=WaterJet.Predict(origin,shot.Velocity,Run.MagicRange,Arena,waterTargets,out var impact,assistPass); // the stream's MagicPass
            reached?.Clear();sheep?.Clear();
            if(!shot.Hit)return shot;
            shot.Impact=impact.Point;shot.Normal=impact.Normal;shot.ImpactTarget=impact.Tree;
            bool prop=impact.Tree>=Run.Trees.Length && impact.Tree<Run.Trees.Length+Run.Props.Length;
            if(prop)return shot;
            var local=Arena.InverseTransformPoint(impact.Point);
            if(reached!=null)for(int i=0;i<Run.Trees.Length;i++)
            {
                var tree=Run.Trees[i];if(tree.State==TreeState.Burnt || tree.State==TreeState.Cleared)continue;
                if(FireRun.WaterReaches(tree,local,Run.MagicRadius,PalmAimRoots))reached.Add(i); // OnWaterImpact's PalmColumn rule
            }
            if(sheep!=null)for(int i=0;i<Run.Animals.Length;i++)
                if(FireRun.CanDouseSheep(Run.Animals[i]) && FireRun.WaterReachesSheep(Run.Animals[i],local,Run.MagicRadius))sheep.Add(i);
            return shot;
        }
        public PalmShot LastPalmShot {get;private set;}
        readonly EmberdeepPalmGesture previewGesture=new();
        // Test/tool hook: what the live aim would do for this water-hand pose right now (same gesture, assist and flight as
        // RenderAim and the spray). reached gets the trees the splash would water, sheep the burning sheep it puts out.
        // False when the palm aims at nothing.
        public bool PreviewPalmShot(FireToolManipulation.Hand waterHand,List<int> reached,out PalmShot shot,List<int> sheep=null)
        {
            shot=default;shot.ImpactTarget=shot.AssistTree=shot.AssistSheep=-1;reached?.Clear();sheep?.Clear();
            if(!Arena || Run==null)return false;
            UpdateTargets(); // the water spheres where the sim has the trees and sheep now (a caller may have moved them this frame)
            previewGesture.Reset();assistPrevious=-1;
            previewGesture.Sample(waterHand,null,0,true,true,Arena,Run.MagicRange,.02f,PalmAssist);
            if(!previewGesture.Aiming)return false;
            shot=SolvePalmShot(previewGesture.Origin,previewGesture.Target,reached,sheep);SetAssist(ref shot,previewGesture.AssistTree);
            return true;
        }

        // Aim assist (EmberdeepPalmAim): the palm ray against each tree's visible shape, then an arc that lands on that tree.
        readonly EmberdeepPalmAim palmAim=new();
        Transform palmAimGrove;
        int assistPrevious=-1;
        int assistPass=-1; // the water target the snapped arc flies through (root aim), -1 none: WaterJet.MagicPass
        EmberdeepPalmGesture.AimAssist palmAssist;
        EmberdeepPalmGesture.AimAssist PalmAssist=>palmAssist??=AssistPalm;
        public EmberdeepPalmAim PalmAim{get{EnsurePalmAim();return palmAim;}}
        void EnsurePalmAim()
        {
            if(palmAimGrove==grove && palmAim.Count==trees.Count)return;
            var roots=new Transform[trees.Count];for(int i=0;i<roots.Length;i++)roots[i]=trees[i].Root;
            palmAim.Build(Arena,roots);palmAimGrove=grove;
        }
        // Off: the palm ray lands on the board plane as before headset test 2 (A/B and the harness's "before" pass).
        public static bool PalmAimAssist=true;
        // Off: burning sheep are no assist targets, as before headset test 3 (A/B and the sheep harness's "before" pass).
        public static bool PalmAimSheep=true;
        // Playtest 2026-11-22 "roots count". Off: as before (A/B and the harness's "before" pass): saved trees are assist
        // targets and hide trees behind them, the arc aims at the tree's .7 point first, and palm water counts by distance to
        // that point. On: saved trees are skipped (EmberdeepPalmAim.SkipSaved), the arc aims at the aimed height on the trunk
        // column first (PickPoint), and palm hits count along the trunk column (FireRun.WaterReaches column).
        public static bool PalmAimRoots=true;
        // Palm water only (the jet's Magic Foam packets, FireRescueGame.cs): spells, tools, towers and the plane keep the point.
        static bool PalmColumn(WaterJet.Impact hit)=>PalmAimRoots && hit.Magic && hit.Delivery==FireDelivery.Foam;
        // A palm splash on the board (a root aim, RootPass) that reaches a burning tree is a hit on fire (OnWaterImpact: the
        // sizzle, the palm lesson), as a hit on its sphere is.
        bool PalmSplashReachesFire(WaterJet.Impact hit)
        {
            if(!PalmColumn(hit))return false;var local=Arena.InverseTransformPoint(hit.Point);
            foreach(var tree in Run.Trees)if(tree.State==TreeState.Burning && FireRun.WaterReaches(tree,local,hit.Radius,true))return true;
            return false;
        }
        bool AssistPalm(Vector3 origin,Vector3 direction,float range,out Vector3 target,out int tree)
        {
            target=default;tree=-1;assistPass=-1;
            if(!PalmAimAssist || !Arena || !grove || Run==null || Run.Trees.Length==0)return false;
            EnsurePalmAim();
            var view=Player?Player.ViewCamera:null;
            palmAim.SkipSaved=PalmAimRoots;
            tree=palmAim.Pick(Arena,origin,direction,range,Run.Trees,assistPrevious,view?view.transform.position:null,PalmAimSheep?Run.Animals:null);
            if(tree<0)return false;
            if(EmberdeepPalmAim.IsSheep(tree,Run.Trees.Length,out int sheep))
                EmberdeepPalmAim.ArcTargetSheep(origin,Run.Animals[sheep],Arena,waterTargets,PalmWaterSpeed,Run.MagicRange,Run.MagicRadius,out target);
            else EmberdeepPalmAim.ArcTarget(origin,Run.Trees[tree],PalmAimRoots?tree:-1,Arena,waterTargets,PalmWaterSpeed,Run.MagicRange,Run.MagicRadius,out target,
                out assistPass,PalmAimRoots?palmAim.PickPoint:(Vector3?)null,PalmAimRoots);
            return true;
        }
        void ConfigurePalmLesson()
        {
            bool lesson=Run.IsPalmLesson;
            if(lesson){ReturnEquipment();introStarted=false;}
            if(rack)rack.gameObject.SetActive(!Run.SpellMode);
            if(rackView)rackView.gameObject.SetActive(!lesson && !grip.Held);
            if(hydrantStation)hydrantStation.gameObject.SetActive(!Run.SpellMode);
            if(creekStation)creekStation.gameObject.SetActive(!Run.GoddessTutorial && Run.Stage>0);
            if(pumpView)pumpView.gameObject.SetActive(Run.SpellMode);
            hammer.Reset();rhythm.Reset();if(pumpView)pumpView.ClearSplashes();gemDisplay=-1;if(waterAudio)waterAudio.ResetFill();
        }
        string PalmHint=>waterLeft?"Open left palm: aim. Touch its back with any right fingertip: water.":"Open right palm: aim. Touch its back with any left fingertip: water.";
        string TouchHint=>waterLeft?"Touch the back of your left hand with any right fingertip. Lift to stop.":"Touch the back of your right hand with any left fingertip. Lift to stop.";
        const string GemEmptyHint="GEM EMPTY - hammer the pump with your fist";
        const string DesktopGemHint="Mouse: aim / hold SPACE: spray / R: hammer the pump";
        void RefreshMagicText()
        {
            // LoopSpec §3.1: in the gem flow the gem itself is the water meter.
            if(Run.GemMode){if(Run.IsPalmLesson)economy.text=$"$ {Run.Coins}";}
            else if(Run.IsPalmLesson)economy.text=$"$ {Run.Coins}  |  MANA {Run.MagicWater:0}/{Run.MagicCapacity:0}";
            else economy.text+=$"  |  MANA {Run.MagicWater:0}/{Run.MagicCapacity:0}";
            if(Run.IsPalmLesson && Run.Phase==FirePhase.Fighting)
            {
                status.text=$"{Run.Stage+1}/{Run.StageCount}  {Run.StageName}";
                help.text=Run.PalmTutorialComplete?"PALM WATER READY":Player.IsXR?PalmHint:Run.GemMode?DesktopGemHint:"Mouse: aim / hold SPACE: palm water";
                tactics.text=Run.GemMode?(Run.GemEmpty?GemEmptyHint:"Lift your fingertip to stop. Rest or hammer the pump to refill."):
                    Run.PalmTutorialComplete?$"FULL RECHARGE {Run.MagicFillSeconds:0}s":"Lift your fingertip to stop. Crystal refills over time.";
            }
            else if(Run.GoddessTutorial && !Run.SpellMode && Run.Stage==3 && !grip.Held && Run.Phase==FirePhase.Fighting)
                help.text="Right hand: gold handle. Left hand: nozzle. Squeeze to spray.";
            else if(palmMagic.Aiming)help.text=Run.MagicWater<.01f?(Run.GemMode?GemEmptyHint:"MANA EMPTY - let the crystal recharge"):TouchHint;
            if(Run.NeedsMagicUpgrade){actionText.text="UPGRADE YOUR MAGIC >";help.text="Visit the shop and buy your first magic upgrade.";}
            // The first incident is stage FireRun.PalmLessons+1 since the stage cut (was level 4); phrase-matched in Translations.tsv.
            if(Run.IsPalmLesson && rackLabel){rackLabel.gameObject.SetActive(true);rackLabel.text="EQUIPMENT FROM LEVEL 2";}
            handText.transform.gameObject.SetActive(!Run.IsPalmLesson);
            foreach(var b in buttons)if(b.Command=="Return equipment")b.gameObject.SetActive(!Run.IsPalmLesson);
        }
    }
}

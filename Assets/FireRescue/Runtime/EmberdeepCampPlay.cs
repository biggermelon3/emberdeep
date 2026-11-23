using System;
using System.Collections.Generic;
using CloudGarden.Integration;
using UnityEngine;
using UnityEngine.InputSystem;
using Step=FireRescue.EmberdeepCampTutorialFlow.Step;

namespace FireRescue
{
    // LoopSpec §9 camp: the hand actions at camp scale (island .27 against the board's .08). Always live at camp, with
    // the tutorial (EmberdeepCampTutorialFlow) on top. Everything is the wave-1 water loop, reused as-is:
    //   palm water: EmberdeepPalmGesture + EmberdeepPalmAimVfx + WaterJet, the gem on the water hand (EmberdeepManaCrystal);
    //   gem + pump: EmberdeepWaterTuning's gem rules and the fist-hammer pump (EmberdeepHammerPumpView, EmberdeepFistHammer,
    //   EmberdeepPumpRhythm, EmberdeepWaterAudio), on the free-hand side of the island;
    //   fire + sheep: a FireRun camp scene (FireRun.CampScene) drawn by FireRescueVfx and EmberdeepSheepView, with B's
    //   pet + hop (EmberdeepSheepInteraction, EmberdeepSheepHop via the view) and flock audio.
    // Fires and sheep live in a camp frame under the island: FrameScale island units per frame unit, origin FrameZ in front
    // of the island, so the frame has board-like proportions for the VFX and the palm gesture's arena bounds cover the island.
    // The campfire relights after a while, so palm water can be practised any time.
    // Voice-over (owner, 2026-11-22): a forest spirit (EmberdeepForestSpirit) comes out of the tent and talks the player
    // through it. Its lines (EmberdeepSpiritLines) are picked by EmberdeepSpiritDirector; a step's done moment waits for
    // the spirit to finish talking, and a queued line whose deed is already done is dropped. The pump step teaches on
    // whatever water is left after the campfire (no emptying, no relight): it is done when a strike tops the gem up.
    [DefaultExecutionOrder(100)]
    public sealed class EmberdeepCampPlay : MonoBehaviour
    {
        public const float FrameScale=.6f,FrameZ=-4.2f;
        // Island units (EmberdeepCamp.CampKit space).
        public static readonly Vector3 CampfireAt=new(.55f,0,-1.55f);
        // Both sheep graze on the free front strip of the island (between the front log and the edge), the part a hand
        // reaches best.
        public static readonly Vector3 SheepHome=new(-.1f,0,-2.56f),LambHome=new(-.55f,0,-2.46f);
        // The burning sheep's dash along the front edge to the sapling, where it panics until put out (and is petted).
        public static readonly Vector3[] SheepDash={new(.3f,0,-2.56f),new(.75f,0,-2.38f)};
        // Headset test 2026-11-22: sheep walked off the island and stood on air (the dash ran at z -2.8). The flat moss top
        // of EDV2_CampGround (measured 2026-11-22, island units) ends at z -2.72 in front of the path and curves back to
        // the sides (-2.62 at |x| .75, -2.4 at |x| 1.1). Sheep centres stay inside this ellipse, >= .12 inside that edge
        // along the front strip, so the hooves always stand on moss.
        public static readonly Vector2 SheepWalkCenter=new(0,-1.3f),SheepWalkRadii=new(1.4f,1.3f);
        // Fire point heights in frame units: FireRescueVfx draws tongues .4 below the point and the glow .68 below.
        // The sheep's own fire point rides on its back so the flock burns with the full tree flames, not just the
        // board's small sheep tongues.
        public const float CampfireY=.7f,SaplingY=1f,SheepFireY=.72f;
        public const float CampfireHeat=100,SaplingHeat=100,SheepHeat=30;
        public const float RelightSeconds=6;
        // The forest spirit, camp-set space (metres): it comes out of the tent's door (tent at island (-1.02,-1.42)) and
        // hovers over the island's left half in front of the posts, ~.3 m under the eye, above every aiming line down
        // to the island and clear of the pump (either side).
        public static readonly Vector3 SpiritHome=new(-.24f,.05f,-.2f),SpiritHover=new(-.17f,.44f,-.1f);
        // Heat per second at full flow, as FireRun.MagicPower at GODDESS HAND level 0. Radii and range in metres.
        public const float WaterPower=45,WaterRadius=.09f,WaterRange=1.6f,AimSpeed=2.8f;
        // The pump in camp-set space (metres): out from the island's front corner on the free-hand side (the free fist
        // strikes, the gem stays in view), its cap ~35 cm below the eye and ~45 cm ahead, like the fireground pump.
        public const float PumpSide=.40f,PumpUp=.12f,PumpForward=-.62f;
        public const float SheepDashSpeed=1.6f,SparkWait=1.2f,SparkFlight=.9f,TreeCatchDelay=.5f; // island units/s, s
        public const int Campfire=0,Sapling=1,SheepFire=2,TutorialSheep=0,Lamb=1;
        static readonly Vector3 DesktopGemLocal=new(-.165f,-.112f,.42f),DesktopPalmLocal=new(-.12f,-.14f,.30f),DesktopHintLocal=new(-.11f,-.06f,.42f);
        public enum Moment { None, Wait, Spark, Dash, Burning, Saved }

        EmberdeepCamp camp;MetaShadowPlayer hands;EmberdeepHandVisuals handVisuals;
        Transform frame,campSet,island;
        public Transform Frame=>frame;
        public FireRun Scene {get;private set;}
        public EmberdeepSheepView Flock {get;private set;}
        public FireRescueVfx Effects {get;private set;}
        public FireFeedback Feedback {get;private set;}
        public EmberdeepHammerPumpView Pump {get;private set;}
        public EmberdeepManaCrystal Gem {get;private set;}
        public EmberdeepPalmAimVfx Aim {get;private set;}
        public EmberdeepInteractionDemo Ghost {get;private set;}
        public EmberdeepCampTutorialFlow Flow {get;}=new();
        public EmberdeepForestSpirit Spirit {get;private set;}
        public EmberdeepSpiritDirector Director {get;}=new();
        int pumpStrikesAtStep;
        public event Action<bool> TutorialFinished;
        readonly WaterJet jet=new();
        readonly EmberdeepPalmGesture gesture=new();
        readonly EmberdeepFistHammer hammer=new();
        readonly EmberdeepPumpRhythm rhythm=new();
        readonly EmberdeepSheepInteraction petting=new();
        readonly FireToolManipulation.Hand[] handSamples=new FireToolManipulation.Hand[2];
        readonly Vector3[] tips=new Vector3[5];
        readonly List<EmberdeepPalmAimVfx.Crown> crowns=new();
        readonly List<Vector3> sparkPath=new();
        Vector3[] targets=Array.Empty<Vector3>();
        Renderer[] campfireArt=Array.Empty<Renderer>();
        EmberdeepWaterAudio waterAudio;
        EmberdeepFxBuilder sparkFx;GameObject sparkHost;
        bool waterLeft=true,built,lastSpraying,saplingCaught,pumpedFull,sheepPetted;
        float gemDisplay=-1,gemPulse,gemIdle=EmberdeepWaterTuning.PassiveDelaySeconds,outFor,momentTime,sheepHeat;
        int dashLeg;
        Step enteredStep;
        Vector3? scriptedTarget;

        public float GemWater {get;private set;}=EmberdeepWaterTuning.GemCapacity;
        public static float GemCapacity=>EmberdeepWaterTuning.GemCapacity;
        public float GemFill01=>Mathf.Clamp01(GemWater/GemCapacity);
        public bool GemEmpty=>GemWater<.01f;
        // The pump step's deed: a strike in the step left the gem full (PumpMet: and the water has landed).
        public bool GemPumpedFull=>pumpedFull;
        public bool PumpMet=>pumpedFull && Pump && Pump.InFlightUnits<=.001f;
        public bool GemSputtering {get;private set;}
        public bool Spraying {get;private set;}
        public bool Aiming {get;private set;}
        public int PumpStrikes {get;private set;}
        public bool PumpGlowing=>Pump && Pump.Glowing;
        public Moment SheepMoment {get;private set;}
        public bool Built=>built;
        public bool WaterHandLeft=>waterLeft;
        // Pump gate (2026-11-22 20:10): with the pump hidden until its lines, the gem refills by itself again in the intro
        // and the campfire step, so a player who wastes water there is never stuck; it stays off in the pump step only.
        public bool PassiveRefill=>Flow.Current!=Step.Pump;
        // In the tutorial the pump stays hidden until the pump step's lines are said (both finished), then rises in, so the
        // lines can't be missed (owner's playtest). Outside the tutorial it shows as before.
        public const float PumpRiseSeconds=.6f,PumpRiseDrop=.18f;
        public bool PumpHeld=>Flow.Active && (Flow.Current<Step.Pump || (Flow.Current==Step.Pump && !PumpLinesSaid));
        bool PumpLinesSaid=>Director.Said(EmberdeepSpiritLines.PumpHow) && !Spirit.Speaking;
        public bool PumpShown=>Pump && Pump.gameObject.activeSelf;
        float pumpRise=1;Vector3 pumpScale=Vector3.one;
        public bool CampfireLit=>Scene!=null && Scene.Trees[Campfire].State==TreeState.Burning;
        public bool SaplingBurning=>Scene!=null && Scene.Trees[Sapling].State==TreeState.Burning;
        public bool SheepOnFire=>Scene!=null && Scene.Animals[TutorialSheep].OnFire;
        public EmberdeepInteractionDemo.Action? HintShowing {get;private set;}
        int WaterIndex=>waterLeft?0:1;
        int OtherIndex=>waterLeft?1:0;
        string GemName=>waterLeft?"Left hand camp gem":"Right hand camp gem";
        public static Vector3 ToFrame(Vector3 island,float y)=>new(island.x/FrameScale,y,(island.z-FrameZ)/FrameScale);
        public static Vector3 ToIsland(Vector3 frame)=>new(frame.x*FrameScale,0,frame.z*FrameScale+FrameZ);
        public static bool OnSheepWalk(Vector3 island)
        {
            float x=(island.x-SheepWalkCenter.x)/SheepWalkRadii.x,z=(island.z-SheepWalkCenter.y)/SheepWalkRadii.y;
            return x*x+z*z<=1.0001f;
        }
        // The point of the sheep walk toward its centre (radial in the ellipse's own space), on the ground.
        public static Vector3 KeepOnIsland(Vector3 island)
        {
            var d=new Vector2((island.x-SheepWalkCenter.x)/SheepWalkRadii.x,(island.z-SheepWalkCenter.y)/SheepWalkRadii.y);
            float m=d.magnitude;if(!float.IsFinite(m))return new Vector3(SheepWalkCenter.x,0,SheepWalkCenter.y);
            if(m>1)d/=m;
            return new Vector3(SheepWalkCenter.x+d.x*SheepWalkRadii.x,0,SheepWalkCenter.y+d.y*SheepWalkRadii.y);
        }
        static Vector3 KeepOnFrame(Vector3 frame)=>ToFrame(KeepOnIsland(ToIsland(frame)),0);
        // World points: the heart of a fire (where water puts it out), a sheep's back.
        public Vector3 FireWorld(int i)=>frame.TransformPoint(Scene.Trees[i].Position-Vector3.up*.3f);
        public Vector3 SheepWorld(int i)=>frame.TransformPoint(EmberdeepSheepInteraction.Center(Scene.Animals[i]));

        public void Initialize(EmberdeepCamp owner,MetaShadowPlayer source,EmberdeepHandVisuals visuals)
        {
            camp=owner;hands=source;handVisuals=visuals;campSet=camp.CampSet;island=camp.CampIsland;
            if(built || !campSet || !island)return; // no art kit, no camp playground
            frame=new GameObject("Camp fire and flock frame").transform;frame.SetParent(island,false);
            frame.localPosition=new Vector3(0,0,FrameZ);frame.localScale=Vector3.one*FrameScale;
            Scene=FireRun.CampScene(new[]{ToFrame(CampfireAt,CampfireY),ToFrame(EmberdeepCamp.SaplingAt,SaplingY),ToFrame(SheepHome,SheepFireY)},new[]{ToFrame(SheepHome,0),ToFrame(LambHome,0)});
            Flock=new GameObject("Camp sheep").AddComponent<EmberdeepSheepView>();Flock.transform.SetParent(frame,false);Flock.Initialize(Scene);
            Effects=new GameObject("Camp fire and water effects").AddComponent<FireRescueVfx>();Effects.transform.SetParent(transform,false);Effects.Initialize();
            Feedback=gameObject.AddComponent<FireFeedback>();Feedback.Initialize();
            waterAudio=gameObject.AddComponent<EmberdeepWaterAudio>();waterAudio.Initialize();
            waterLeft=EmberdeepWaterHand.Left;
            Gem=new GameObject(GemName).AddComponent<EmberdeepManaCrystal>();Gem.transform.SetParent(transform,false);
            Gem.transform.localScale=Vector3.one*EmberdeepWaterTuning.GemScale;Gem.gameObject.SetActive(false);Gem.FollowHand(handVisuals,waterLeft);
            Aim=new GameObject("Camp palm aim").AddComponent<EmberdeepPalmAimVfx>();Aim.transform.SetParent(transform,false);
            // Parented before Build: the pump keeps its glow and splash host beside itself, under the camp set.
            Pump=new GameObject("Camp hammer pump").AddComponent<EmberdeepHammerPumpView>();Pump.transform.SetParent(campSet,false);
            Pump.Build(camp.PanelMaterial,camp.PanelMaterial,camp.PanelMaterial,EmberdeepArtLibrary.Load());Pump.SplashLanded+=OnSplashLanded;
            pumpScale=Pump.transform.localScale;
            PositionPump();
            if(handVisuals)handVisuals.Contacts.Add(Pump.transform);
            Spirit=new GameObject("Forest spirit").AddComponent<EmberdeepForestSpirit>();Spirit.transform.SetParent(transform,false);
            Ghost=new GameObject("Camp tutorial ghost").AddComponent<EmberdeepInteractionDemo>();Ghost.Ghost=true;
            Ghost.transform.SetParent(transform,false);Ghost.gameObject.SetActive(false);
            sparkHost=new GameObject("Camp spark");sparkHost.transform.SetParent(transform,false);sparkFx=new EmberdeepFxBuilder(sparkHost,"Camp spark trail");
            var art=new List<Renderer>();
            foreach(var r in island.GetComponentsInChildren<Renderer>(true))
                if(r.transform.parent && r.transform.parent.name.StartsWith("EDV2_Campfire") && (r.name.StartsWith("Flame") || r.name.StartsWith("Embers")))art.Add(r);
            campfireArt=art.ToArray();
            petting.Petted+=OnPetted;
            Ignite(Campfire,CampfireHeat);
            built=true;
        }

        // ---- Tutorial ----------------------------------------------------------------------------------------------
        public void BeginTutorial()
        {
            if(!built)return;
            Flow.Begin(true);enteredStep=Step.Off;Director.Begin();pumpRise=0;
            GemWater=GemCapacity;gemDisplay=-1;waterAudio.ResetFill();pumpedFull=sheepPetted=false;
            ResetSheepMoment();Ignite(Campfire,CampfireHeat);
            Spirit.Silence();Spirit.Appear();
        }
        // Skip, or leaving the camp: the scripted moment calms down, the rest of the camp keeps going.
        public void StopTutorial()
        {
            Flow.Stop();enteredStep=Step.Off;HideGhost();Director.Stop();pumpRise=1; // SKIP: the pump is simply back
            if(Spirit){Spirit.Silence();Spirit.Leave();}
            if(SheepMoment!=Moment.None && SheepMoment!=Moment.Saved)ResetSheepMoment();
        }
        // What the spirit sees this frame (EmberdeepSpiritDirector).
        public EmberdeepSpiritDirector.Facts SpiritFacts=>new()
        {
            Step=Flow.Current,CampfireOut=!CampfireLit,Spraying=Spraying,PumpMet=PumpMet,PumpStrikes=PumpStrikes,
            SheepLit=SheepMoment>=Moment.Dash,SheepBurning=SheepOnFire,SaplingBurning=SaplingBurning,Petted=sheepPetted,
        };
        void TickTutorial(float dt,bool live)
        {
            if(!Flow.Active)return;
            if(Flow.Current!=enteredStep)EnterStep(Flow.Current);
            // The spirit's timers (reminders, the arrival) hold while the camp is not live.
            var line=Director.Tick(live?dt:0,SpiritFacts,Spirit.Busy); // Busy: speaking or in the pause after a line
            if(line!=null)Spirit.Say(line);
            bool quiet=!Spirit.Speaking && Director.QueueEmpty;
            if(Flow.Tick(dt,!CampfireLit,PumpMet,SheepMoment==Moment.Saved,sheepPetted,Director.IntroDone,quiet))EnterStep(Flow.Current);
            if(Flow.Finished){Flow.Stop();enteredStep=Step.Off;HideGhost();Director.Stop();Spirit.Leave();TutorialFinished?.Invoke(true);}
        }
        void EnterStep(Step step)
        {
            enteredStep=step;
            // The campfire is not relit on entry: a player who put it out during the intro has done the step.
            if(step==Step.Pump){pumpStrikesAtStep=PumpStrikes;pumpedFull=false;}
            if(step==Step.Sheep){ResetSheepMoment();SheepMoment=Moment.Wait;momentTime=0;sheepPetted=false;if(!CampfireLit)Ignite(Campfire,CampfireHeat);}
            if(step==Step.Done)
            {
                Effects.Celebrate(new[]{FireWorld(Campfire)+frame.up*.06f,SheepWorld(TutorialSheep)+frame.up*.04f,FireWorld(Sapling)+frame.up*.08f},frame.up,2.5f);
                Feedback.Complete(true);
            }
        }

        // ---- Frame -------------------------------------------------------------------------------------------------
        void LateUpdate()
        {
            if(!built)return;
            float dt=Mathf.Min(Time.unscaledDeltaTime,.1f);
            bool shown=campSet.gameObject.activeInHierarchy;
            bool live=shown && camp.InputLive;
            var cam=camp.Player?camp.Player.ViewCamera:null;
            SyncWaterHand();
            ReadHands(live);
            TickTutorial(dt,live);
            TickFlock(dt,live,cam);
            TickFires(dt);
            TickWater(dt,live,cam);
            TickPump(dt,live,cam);
            RenderGem(dt,live,cam);
            RenderHints(live,cam);
            RenderSpark(cam);
            if(Spirit)Spirit.Tick(dt,shown,live,cam,campSet.TransformPoint(SpiritHome),campSet.TransformPoint(SpiritHover),campSet.up);
            if(shown){Effects.Tick(Scene,frame,jet,dt);Flock.Render();if(Flock.Audio)Flock.Audio.Tick();}
            Feedback.Tick(Spraying,0,FireDelivery.Foam,dt);
            scriptedTarget=null;
        }
        void SyncWaterHand()
        {
            bool left=EmberdeepWaterHand.Left;if(left==waterLeft)return;
            waterLeft=left;gesture.Reset();hammer.Reset();Gem.FollowHand(handVisuals,left);Gem.name=GemName;PositionPump();
        }
        void PositionPump()
        {
            Pump.transform.localPosition=new Vector3(EmberdeepWaterTuning.PumpSide(waterLeft)*PumpSide,PumpUp,PumpForward);
            Pump.SetSide(EmberdeepWaterTuning.PumpLeft(waterLeft),campSet.rotation);
        }
        // A hand the runtime has lost is not used, even while the ISDK chain still bridges it at its last pose
        // (EmberdeepHandVisuals.Seen, EmberdeepHandSight): no gem, aim, pump strike or pet from a hand left in the air.
        bool Seen(bool left)=>!handVisuals || handVisuals.Seen(left);
        // The water hand's menu ray is up (on or near the sign): its palm-water aim steps aside, so one hand never draws
        // two lines (headset test 2, 2026-11-22), and the palm gesture cannot fire.
        public bool WaterRayUp=>camp.MenuInput && camp.MenuInput.RayShown(WaterIndex);
        // Headset test 3 (2026-11-22): water first. The water hand's open palm points at the island (the palm gesture's own
        // aim rule, run without the menu gate). While it does, the menu shows that hand a ray only on a direct button hit
        // (EmberdeepHandRayMenu.PalmAiming), so aiming past the sign never raises the ray.
        public bool PalmAimPose(int hand)=>hand==WaterIndex && (PalmAimOverride ?? palmAimPose);
        // Tests on the desktop (no tracked hands): stands in for the measured pose.
        public bool? PalmAimOverride {get;set;}
        // The palm gesture may run: nothing on the menu holds the water hand.
        public bool WaterHandFree {get;private set;}
        readonly EmberdeepPalmGesture aimProbe=new();
        bool palmAimPose;
        bool MenuHoldsWaterHand=>camp.MenuInput && (WaterRayUp || camp.MenuInput.DragHand==WaterIndex);
        void ReadHands(bool live)
        {
            WaterHandFree=!MenuHoldsWaterHand && !petting.OwnsHand(0) && !petting.OwnsHand(1);
            if(!live || !camp.Player.IsXR){handSamples[0]=handSamples[1]=default;gesture.Reset();aimProbe.Reset();palmAimPose=false;return;}
            for(int i=0;i<2;i++)
            {
                bool left=i==0;
                bool poseValid=hands.ReadObjectPose(left,out var palm,out _,out _) && Seen(left);
                float strength;Vector3 contact;Quaternion grip;
                bool closureValid=hands.UsesPortableGrip?hands.TryReadPortableGrip(left,out strength,out contact):EmberdeepHandGrasp.TryReadClosure(hands.TrackedHand(left),out strength,out contact);
                bool frameValid=hands.UsesPortableGrip?hands.TryReadPortableGripFrame(left,out grip):EmberdeepHandGrasp.TryReadPalmFrame(hands.TrackedHand(left),out grip);
                handSamples[i]=new FireToolManipulation.Hand{Valid=poseValid && closureValid && frameValid,Palm=palm,Contact=contact,Closure=strength,HasGripFrame=frameValid,GripRotation=grip};
            }
            bool open=hands.UsesPortableGrip?hands.IsPortablePalmOpen(waterLeft):EmberdeepHandGrasp.IsOpenPalm(hands.TrackedHand(waterLeft));
            int mask=0;if(Seen(!waterLeft))for(int i=0;i<5;i++)if(hands.TryReadFingertip(!waterLeft,i,out tips[i]))mask|=1<<i;
            // The water hand on the sign (its ray up, or holding a slider) is using the menu, not the gem. The other hand at
            // the sign no longer stops the water (headset test 3: water first).
            aimProbe.Sample(handSamples[WaterIndex],null,0,open,true,frame,WaterRange,Time.unscaledDeltaTime);palmAimPose=aimProbe.Aiming;
            gesture.Sample(handSamples[WaterIndex],tips,mask,open,WaterHandFree,frame,WaterRange,Time.unscaledDeltaTime);
        }

        // ---- Fires -------------------------------------------------------------------------------------------------
        void Ignite(int i,float heat)
        {
            var t=Scene.Trees[i];t.State=TreeState.Burning;t.Heat=heat;t.Fuel=100;t.Wetness=0;
            if(i==Campfire){outFor=0;foreach(var r in campfireArt)if(r)r.enabled=true;}
        }
        void PutOut(int i)
        {
            var t=Scene.Trees[i];t.State=TreeState.Saved;t.Heat=0;
            Effects.Extinguish(FireWorld(i)+frame.up*.02f,.05f,frame.up);Feedback.Rescue(1);
            if(i==Campfire)foreach(var r in campfireArt)if(r)r.enabled=false;
        }
        void TickFires(float dt)
        {
            if(CampfireLit){outFor=0;return;}
            outFor+=dt;
            // Out stays out through the intro, the campfire step and the pump step (the pump step needs no target: it
            // teaches on the water the campfire left).
            bool hold=Flow.Current==Step.Intro || Flow.Current==Step.Campfire || Flow.Current==Step.Pump;
            float wait=hold?float.PositiveInfinity:RelightSeconds;
            if(outFor>=wait)Ignite(Campfire,CampfireHeat);
        }

        // ---- Water -------------------------------------------------------------------------------------------------
        // Tests and scripted captures: one frame of palm water from the desktop palm toward a world point.
        public void SprayAt(Vector3 world)=>scriptedTarget=world;
        float TickGem(float dt,bool cast)
        {
            if(cast)
            {
                gemIdle=0;float used=Mathf.Min(GemWater,EmberdeepWaterTuning.GemFlow*dt);GemWater-=used;
                if(GemEmpty)GemWater=0;
                return used;
            }
            gemIdle=Mathf.Min(gemIdle+dt,1000);
            if(PassiveRefill && gemIdle>=EmberdeepWaterTuning.PassiveDelaySeconds)GemWater=Mathf.Min(GemCapacity,GemWater+EmberdeepWaterTuning.PassivePerSecond*dt);
            return 0;
        }
        void UpdateTargets()
        {
            int count=Scene.Trees.Length+Scene.Animals.Length;
            if(targets.Length!=count)targets=new Vector3[count];
            for(int i=0;i<Scene.Trees.Length;i++)
                // The sheep's fire point only catches water while that sheep burns.
                targets[i]=i==SheepFire && Scene.Trees[i].State!=TreeState.Burning?new Vector3(0,-1000,0):frame.TransformPoint(Scene.Trees[i].Position);
            for(int i=0;i<Scene.Animals.Length;i++)targets[Scene.Trees.Length+i]=SheepWorld(i);
        }
        void TickWater(float dt,bool live,Camera cam)
        {
            UpdateTargets();
            bool aiming=false,cast=false;Vector3 origin=default,target=default;
            if(live && cam)
            {
                if(scriptedTarget.HasValue){aiming=cast=true;origin=cam.transform.TransformPoint(DesktopPalmLocal);target=scriptedTarget.Value;}
                else if(camp.Player.IsXR){aiming=gesture.Aiming;cast=gesture.Casting;origin=gesture.Origin;target=gesture.Target;}
                else if(Mouse.current!=null && !camp.HoveredButton)
                {
                    // Desktop: the mouse picks the landing spot on the island, SPACE sprays (as in the fireground).
                    target=WaterJet.MouseTarget(cam.ScreenPointToRay(Mouse.current.position.ReadValue()),frame,targets);
                    origin=cam.transform.TransformPoint(DesktopPalmLocal);
                    var local=frame.InverseTransformPoint(target);
                    aiming=Mathf.Abs(local.x)<=5.5f && local.z>=1.2f && local.z<=12;
                    cast=aiming && Keyboard.current!=null && Keyboard.current.spaceKey.isPressed;
                }
            }
            float used=TickGem(dt,cast);
            Spraying=used>0;Aiming=aiming;GemSputtering=cast && GemEmpty;
            if(Spraying && !lastSpraying)CountPerformance(EmberdeepFirstHints.Move.Water);
            lastSpraying=Spraying;
            var velocity=aiming?WaterJet.AimVelocity(origin,target,AimSpeed):Vector3.zero;
            float unit=Mathf.Max(.0001f,frame.lossyScale.x);
            jet.Step(dt,origin,velocity,used*WaterPower/EmberdeepWaterTuning.GemFlow,WaterRange,frame,targets,OnImpact,WaterRadius/unit,3,0,FireDelivery.Foam,true);
            if(!aiming || !cam){Aim.Hide();return;}
            bool hit=WaterJet.Predict(origin,velocity,WaterRange,frame,targets,out var impact);
            crowns.Clear();
            for(int i=0;i<Scene.Trees.Length;i++)
            {
                if(Scene.Trees[i].State!=TreeState.Burning || !hit || Vector3.Distance(FireWorld(i),impact.Point)>WaterRadius)continue;
                crowns.Add(new EmberdeepPalmAimVfx.Crown{Center=FireWorld(i)+frame.up*.03f,Radius=i==Campfire?.055f:.07f,Burning=true});
            }
            Aim.ShowAim(true,origin,velocity,WaterRange,hit,impact.Point,impact.Normal,cam,!GemEmpty,Spraying,dt,crowns,crowns.Count,frame.up);
        }
        void OnImpact(WaterJet.Impact hit)
        {
            bool hot=false;
            for(int i=0;i<Scene.Trees.Length;i++)
            {
                var t=Scene.Trees[i];
                if(i==SheepFire || t.State!=TreeState.Burning || (hit.Tree!=i && Vector3.Distance(hit.Point,FireWorld(i))>WaterRadius))continue;
                hot=true;t.Heat=Mathf.Max(0,t.Heat-hit.Amount);
                if(t.Heat<=0)PutOut(i);
            }
            for(int i=0;i<Scene.Animals.Length;i++)
            {
                var a=Scene.Animals[i];
                bool onBack=i==TutorialSheep && hit.Tree==SheepFire;
                if(!onBack && hit.Tree!=Scene.Trees.Length+i && Vector3.Distance(hit.Point,SheepWorld(i))>WaterRadius)continue;
                a.WaterReaction=.85f;
                if(!a.OnFire)continue;
                hot=true;sheepHeat-=hit.Amount;
                if(sheepHeat<=0)CalmSheep(i,true);
            }
            Effects.Impact(hit,hot);Feedback.Impact(hot);
        }

        // ---- Pump --------------------------------------------------------------------------------------------------
        EmberdeepFistHammer.Hand PumpHand(int i)
        {
            var h=handSamples[i];
            bool free=h.Usable && !petting.OwnsHand(i) && !(gesture.Casting && i==OtherIndex);
            return new EmberdeepFistHammer.Hand(free,h.Contact,h.Closure);
        }
        void TickPump(float dt,bool live,Camera cam)
        {
            PositionPump();
            if(PumpHeld)
            {
                // Hidden: inactive, so hand contact, the sky outline and the strikes all leave it alone; its glow host (a
                // sibling) is cleared once.
                pumpRise=0;hammer.Reset();
                if(Pump.gameObject.activeSelf){Pump.Render(0,null,default,false,false,false,0,false);Pump.gameObject.SetActive(false);}
                return;
            }
            if(!Pump.gameObject.activeSelf)Pump.gameObject.SetActive(true);
            if(pumpRise<1)
            {
                pumpRise=Mathf.MoveTowards(pumpRise,1,dt/PumpRiseSeconds);float e=Mathf.SmoothStep(0,1,pumpRise);
                Pump.transform.localPosition-=Vector3.up*PumpRiseDrop*(1-e);Pump.transform.localScale=pumpScale*Mathf.Lerp(.3f,1,e);
            }
            else Pump.transform.localScale=pumpScale;
            bool shown=Pump.gameObject.activeInHierarchy;
            if(live && shown)
            {
                if(camp.Player.IsXR){if(hammer.Sample(PumpHand(0),PumpHand(1),Pump.CapTop,Pump.Up,dt))StrikePump();}
                else if(Keyboard.current!=null && Keyboard.current.rKey.wasPressedThisFrame && !camp.HoveredButton)StrikePump();
            }
            else hammer.Reset();
            double now=Time.unscaledTimeAsDouble;
            bool beat=live && EmberdeepWaterTuning.RhythmEnabled && rhythm.Active(now);
            // LoopSpec §3.1: an empty gem lights the pump up; so does the tutorial's pump step, until a strike tops it up.
            bool glow=live && ((GemEmpty && gemDisplay<.03f) || (Flow.Current==Step.Pump && !pumpedFull));
            Pump.Render(dt,live?cam:null,Gem.transform.position,glow,live && hammer.Hovering,beat,rhythm.Phase01(now),beat && rhythm.InWindow(now));
        }
        // One accepted strike (fist, or R on desktop): the gem takes the water now, the gem shows it when the splash lands.
        public void StrikePump()
        {
            if(!built || PumpHeld)return; // hidden until its lines are said
            var grade=rhythm.StrikeGraded(Time.unscaledTimeAsDouble,EmberdeepWaterTuning.RhythmEnabled);
            bool onBeat=grade==EmberdeepPumpGrade.OnBeat;
            float units=Mathf.Min(onBeat?EmberdeepWaterTuning.OnBeatWater:EmberdeepWaterTuning.StrikeWater,Mathf.Max(0,GemCapacity-GemWater));
            GemWater+=units;PumpStrikes++;
            if(GemCapacity-GemWater<.001f){GemWater=GemCapacity;if(Flow.Current==Step.Pump && PumpStrikes>pumpStrikesAtStep)pumpedFull=true;}
            Pump.Strike(units,grade);waterAudio.Strike(grade);
            CountPerformance(EmberdeepFirstHints.Move.Pump);
        }
        // A move done at camp counts toward the fireground's first-time hints (EmberdeepFirstHints: shown 2 times).
        static void CountPerformance(EmberdeepFirstHints.Move move)
        {
            int count=EmberdeepFirstHints.Count(move);if(count>=EmberdeepFirstHints.Shows)return;
            PlayerPrefs.SetInt(EmberdeepFirstHints.Key(move),count+1);PlayerPrefs.Save();
        }
        void OnSplashLanded(float units,bool onBeat){gemPulse=onBeat?1:.7f;waterAudio.Gulp(onBeat);}
        void RenderGem(float dt,bool live,Camera cam)
        {
            bool xr=camp.Player.IsXR;var h=handSamples[WaterIndex];
            bool onHand=live && xr && h.Usable;
            // Desktop: the gem shows beside the view while it is in use, not over the sign on the main page.
            bool desktop=live && !xr && cam && (Flow.Active || Aiming || GemFill01<.999f || Pump.InFlightUnits>0);
            Gem.gameObject.SetActive(onHand || desktop);
            if(onHand)Gem.transform.SetPositionAndRotation(h.Contact-h.GripRotation*Vector3.up*.016f,h.GripRotation);
            else if(desktop){var v=cam.transform;Gem.transform.SetPositionAndRotation(v.TransformPoint(DesktopGemLocal),Quaternion.LookRotation(v.up,v.forward));}
            // Water still flying from the spout shows when it lands.
            float target=Mathf.Clamp01(GemFill01-Pump.InFlightUnits/GemCapacity);
            if(gemDisplay<0)gemDisplay=target;
            bool rising=target>gemDisplay+.002f;
            gemDisplay=Mathf.MoveTowards(gemDisplay,target,dt*(rising?1.4f:2.5f));
            gemPulse=Mathf.MoveTowards(gemPulse,0,dt*3);
            bool refilling=PassiveRefill && gemIdle>=EmberdeepWaterTuning.PassiveDelaySeconds && GemWater<GemCapacity;
            Gem.SetState(gemDisplay,Spraying?-1:rising?1:refilling?.45f:0,gemDisplay<.01f && GemEmpty,gemPulse);
            waterAudio.Tick(GemSputtering,dt);waterAudio.Fill(gemDisplay,dt);
        }

        // ---- Sheep -------------------------------------------------------------------------------------------------
        void TickFlock(float dt,bool live,Camera cam)
        {
            bool calm=true;
            foreach(var a in Scene.Animals)
            {
                a.AnimationTime+=dt;a.StateTime+=dt;a.Velocity=Vector3.zero;
                a.WaterReaction=Mathf.Max(0,a.WaterReaction-dt);a.Affection=Mathf.Max(0,a.Affection-dt);a.PetCooldown=Mathf.Max(0,a.PetCooldown-dt);
                calm&=a.State==AnimalState.Rescued;
            }
            TickSheepMoment(dt);
            // Every camp sheep keeps its hooves on the moss, whatever moved it.
            foreach(var a in Scene.Animals)if(a.HeldBy<0)a.Position=KeepOnFrame(a.Position);
            var rider=Scene.Trees[SheepFire];var carrier=Scene.Animals[TutorialSheep];
            rider.Position=carrier.Position+Vector3.up*SheepFireY;rider.State=carrier.OnFire?TreeState.Burning:TreeState.Saved;rider.Heat=carrier.OnFire?100:0;
            // Petting at any time; never while a sheep is on the run (a running sheep could be grabbed).
            if(live && calm && camp.Player.IsXR)
            {
                var left=handSamples[0];var right=handSamples[1];
                if(gesture.Casting){left.Valid=false;right.Valid=false;}
                // Camp sheep are ~2x a board sheep: the pet radii grow with them, and a relaxed hand still strokes (they
                // are never grabbed at camp).
                petting.Reach=PetReach;petting.PetClosure=CampPetClosure;
                petting.Step(Scene,frame,left,right,dt,NearestSheepTouch(0),NearestSheepTouch(1));
            }
            else petting.Reset();
            if(live && calm && !camp.Player.IsXR && cam && Mouse.current!=null && Mouse.current.leftButton.wasPressedThisFrame && !camp.HoveredButton)
            {
                // Desktop: click a sheep to pet it.
                var ray=cam.ScreenPointToRay(Mouse.current.position.ReadValue());int best=-1;float near=.07f;
                for(int i=0;i<Scene.Animals.Length;i++)
                {
                    var c=SheepWorld(i);float along=Vector3.Dot(c-ray.origin,ray.direction);if(along<0)continue;
                    float off=Vector3.Distance(ray.GetPoint(along),c);if(off<near){near=off;best=i;}
                }
                if(best>=0)PetSheep(best);
            }
        }
        public const float CampPetClosure=.42f;
        // Camp sheep size against a board sheep (EmberdeepSheepInteraction.Reach): ~2.
        public float PetReach=>frame?Mathf.Max(1,frame.lossyScale.x/FireMiniature.Scale):1;
        // The point of a hand nearest a sheep: any fingertip, or the middle of a finger (a flat hand over a small sheep
        // often has only its fingers on the wool while the palm and the tips hang past it).
        Vector3? NearestSheepTouch(int hand)
        {
            Vector3? closest=null;float best=.045f*PetReach;
            if(!Seen(hand==0))return null;
            var palm=handSamples[hand];
            for(int f=0;f<5;f++)if(hands.TryReadFingertip(hand==0,f,out var tip))
            {
                Consider(tip);
                if(palm.Usable)Consider(Vector3.Lerp(palm.Contact,tip,.55f));
            }
            return closest;
            void Consider(Vector3 p)
            {
                for(int i=0;i<Scene.Animals.Length;i++)
                {
                    float distance=Vector3.Distance(p,SheepWorld(i));
                    if(distance<best){best=distance;closest=p;}
                }
            }
        }
        // Same cooldown and effect as a hand pet (the view plays the hop and the boing).
        public bool PetSheep(int i)
        {
            if(!built || i<0 || i>=Scene.Animals.Length || !EmberdeepSheepInteraction.TryPet(Scene.Animals[i]))return false;
            OnPetted(i);return true;
        }
        // A pat once the sheep is saved counts, even before the spirit asks for it (its line is then skipped).
        void OnPetted(int i){if(i==TutorialSheep && (Flow.Current==Step.Pet || (Flow.Current==Step.Sheep && SheepMoment==Moment.Saved)))sheepPetted=true;}
        void ResetSheepMoment()
        {
            SheepMoment=Moment.None;momentTime=0;dashLeg=0;saplingCaught=false;
            CalmSheep(TutorialSheep,false);
            var a=Scene.Animals[TutorialSheep];a.Position=a.Home=a.Pasture=ToFrame(SheepHome,0);a.StateTime=10;
            var tree=Scene.Trees[Sapling];tree.State=TreeState.Safe;tree.Heat=0;
        }
        void CalmSheep(int i,bool rescued)
        {
            var a=Scene.Animals[i];bool wasOnFire=a.OnFire;
            a.OnFire=false;a.State=AnimalState.Rescued;a.Velocity=Vector3.zero;a.Home=a.Pasture=a.Position;
            // A sheep saved from the fire pops its rescue coin, as on the board.
            if(rescued && wasOnFire){a.StateTime=0;Effects.Extinguish(SheepWorld(i),.04f,frame.up);Feedback.Rescue(1);}
        }
        void TickSheepMoment(float dt)
        {
            if(SheepMoment==Moment.None || SheepMoment==Moment.Saved)return;
            momentTime+=dt;var a=Scene.Animals[TutorialSheep];
            switch(SheepMoment)
            {
                case Moment.Wait:
                    if(!CampfireLit)Ignite(Campfire,CampfireHeat);
                    if(momentTime>=SparkWait){SheepMoment=Moment.Spark;momentTime=0;}
                    break;
                case Moment.Spark:
                    if(momentTime<SparkFlight)break;
                    // The spark lands: the sheep catches fire and bolts (its alarm comes from the flock audio).
                    a.OnFire=true;a.State=AnimalState.Running;a.StateTime=0;sheepHeat=SheepHeat;
                    SheepMoment=Moment.Dash;momentTime=0;dashLeg=0;
                    break;
                case Moment.Dash:
                    if(!a.OnFire){SheepMoment=Moment.Burning;momentTime=0;break;}
                    var goal=ToFrame(SheepDash[dashLeg],0);
                    Move(a,goal,SheepDashSpeed/FrameScale,dt);
                    if(Vector3.Distance(a.Position,goal)<.01f && ++dashLeg>=SheepDash.Length){SheepMoment=Moment.Burning;momentTime=0;}
                    break;
                case Moment.Burning:
                    // A sheep still alight beside the sapling lights it.
                    if(!saplingCaught && a.OnFire && momentTime>=TreeCatchDelay){saplingCaught=true;Ignite(Sapling,SaplingHeat);}
                    if(a.OnFire)
                    {
                        float t=a.AnimationTime;var spot=ToFrame(SheepDash[SheepDash.Length-1],0);
                        Move(a,spot+new Vector3(Mathf.Sin(t*4)*.15f,0,Mathf.Cos(t*5)*.08f),2.2f,dt);
                    }
                    if(!a.OnFire && !SaplingBurning)SheepMoment=Moment.Saved;
                    break;
            }
        }
        static void Move(FireRun.Animal a,Vector3 target,float speed,float dt)
        {
            if(dt<=0)return;var next=Vector3.MoveTowards(a.Position,target,speed*dt);a.Velocity=(next-a.Position)/dt;a.Position=next;
        }
        void RenderSpark(Camera cam)
        {
            bool flying=SheepMoment==Moment.Spark && cam && campSet.gameObject.activeInHierarchy;
            if(!flying){sparkFx.Renderer.enabled=false;return;}
            sparkFx.Renderer.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);sparkFx.Renderer.transform.localScale=Vector3.one;
            float p=Mathf.Clamp01(momentTime/SparkFlight);
            var from=FireWorld(Campfire)+frame.up*.05f;var to=SheepWorld(TutorialSheep)+frame.up*.015f;
            sparkPath.Clear();for(int i=0;i<=10;i++)sparkPath.Add(Arc(from,to,Mathf.Lerp(Mathf.Max(0,p-.3f),p,i/10f)));
            var eye=cam.transform.position;var right=cam.transform.right;var up=cam.transform.up;
            sparkFx.Begin();
            sparkFx.Ribbon(sparkPath,sparkPath.Count,eye,.006f,new Color(1,.55f,.12f,.95f),.2f);
            var head=Arc(from,to,p);float flicker=.8f+.2f*Mathf.Sin(momentTime*40);
            sparkFx.Dot(head,right,up,.022f*flicker,new Color(1,.5f,.1f,.35f));
            sparkFx.Dot(head,right,up,.012f*flicker,new Color(1,.7f,.25f,.8f));
            sparkFx.Dot(head,right,up,.006f,new Color(1,.97f,.8f,1));
            for(int i=1;i<=4;i++)sparkFx.Dot(Arc(from,to,Mathf.Max(0,p-.06f*i))+up*(.004f*Mathf.Sin(i*2.3f+momentTime*25)),right,up,.004f,new Color(1,.6f,.2f,.8f-.15f*i));
            sparkFx.End();sparkFx.Renderer.enabled=true;
        }
        static Vector3 Arc(Vector3 a,Vector3 b,float t)=>Vector3.Lerp(a,b,t)+Vector3.up*(.12f*4*t*(1-t));

        // ---- Ghost hints (LoopSpec §11b look, no words) ---------------------------------------------------------------
        void RenderHints(bool live,Camera cam)
        {
            EmberdeepInteractionDemo.Action? want=null;Pose pose=default;float scale=1;bool mirror=false;
            if(Flow.Running && live && cam && !Flow.Completing)
            {
                var step=Flow.Current;
                // An empty gem (or a pump step that has emptied it) always points at the pump.
                bool pump=!PumpHeld && (GemEmpty || (step==Step.Pump && !pumpedFull));
                // While the pump waits for its lines, the pump step shows no ghost at all (not the palm-water one either).
                bool water=step==Step.Campfire || (step==Step.Pump && !PumpHeld) || (step==Step.Sheep && SheepMoment>=Moment.Dash && (SheepOnFire || SaplingBurning));
                // The free fist strikes (the pump stands on the free-hand side): the authored-left ghost mirrors for a right fist.
                if(water && pump){want=EmberdeepInteractionDemo.Action.Hammer;pose=Facing(Pump.CapTop+Pump.Up*.01f,cam);mirror=waterLeft;}
                else if(water){want=EmberdeepInteractionDemo.Action.Goddess;pose=WaterHintPose(cam,out scale);mirror=!waterLeft;}
                else if(step==Step.Pet && !SheepOnFire)
                {
                    // An open hand strokes the saved sheep's back, palm down.
                    want=EmberdeepInteractionDemo.Action.Slap;scale=.8f;
                    var flat=Vector3.ProjectOnPlane(SheepWorld(TutorialSheep)-cam.transform.position,frame.up);
                    pose=new Pose(SheepWorld(TutorialSheep)+frame.up*.028f,Quaternion.LookRotation(-frame.up,flat.sqrMagnitude>1e-4f?flat.normalized:campSet.forward));
                }
            }
            if(want==null){HideGhost();return;}
            if(!Ghost.gameObject.activeSelf)Ghost.gameObject.SetActive(true);
            if(HintShowing!=want){Ghost.Show(want.Value);Ghost.SetCaptionVisible(false);HintShowing=want;}
            Ghost.transform.SetPositionAndRotation(pose.position,pose.rotation);
            Ghost.transform.localScale=new Vector3(mirror?-scale:scale,scale,scale);
        }
        void HideGhost(){if(Ghost && Ghost.gameObject.activeSelf)Ghost.gameObject.SetActive(false);HintShowing=null;}
        static Pose Facing(Vector3 at,Camera cam)
        {
            var flat=Vector3.ProjectOnPlane(at-cam.transform.position,Vector3.up);
            return new Pose(at,Quaternion.LookRotation(flat.sqrMagnitude>1e-4f?flat.normalized:Vector3.forward,Vector3.up));
        }
        // On the player's own water hand when it is tracked; desktop: beside the on-screen gem.
        Pose WaterHintPose(Camera cam,out float scale)
        {
            if(!camp.Player.IsXR){scale=.55f;return new Pose(cam.transform.TransformPoint(DesktopHintLocal),cam.transform.rotation);}
            scale=.9f;var h=handSamples[WaterIndex];
            return new Pose(h.Usable?h.Contact+Vector3.up*.07f:FireWorld(Campfire)+Vector3.up*.18f,cam.transform.rotation);
        }

        void OnDisable(){if(Aim)Aim.Hide();if(sparkFx!=null && sparkFx.Renderer)sparkFx.Renderer.enabled=false;}
        void OnDestroy()
        {
            petting.Petted-=OnPetted;if(Pump)Pump.SplashLanded-=OnSplashLanded;
            sparkFx?.Destroy();
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using CloudGarden.Integration;
using CloudGarden.ShadowPuzzle;
using PhysicalAssembly;
using PhysicalAssembly.MetaXR;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace FireRescue
{
    public sealed partial class FireRescueGame : MonoBehaviour
    {
        public GameObject PlayerPrefab;
        public bool StandaloneShowcase;
        public Material GroundMaterial, FoliageMaterial, BarkMaterial, FlameMaterial, WaterMaterial, PanelMaterial;
        public FireRun Run { get; private set; } = new FireRun();
        public MetaPlayerRig Player { get; private set; }
        public bool Spraying { get; private set; }
        public Transform Arena { get; private set; }
        public bool LeftHanded { get; private set; }
        public EmberdeepImmersiveSky ImmersiveSky { get; private set; }
        public EmberdeepEnvironmentVfx EnvironmentVfx { get; private set; }
        GardenNavigation navigation;
        MetaShadowPlayer hands;
        readonly FireSprayGesture gesture = new FireSprayGesture();
        readonly List<WorkshopButton> buttons = new List<WorkshopButton>();
        readonly List<TreeView> trees = new List<TreeView>();
        readonly WaterJet jet = new WaterJet();
        public FireRescueVfx Effects { get; private set; }
        public FireFeedback Feedback { get; private set; }
        FireDiorama diorama;
        public EmberdeepSheepView SheepView { get; private set; }
        EmberdeepArtLibrary art;
        UnityEngine.Rendering.RenderPipelineAsset previousPipeline;
        bool changedPipeline;
        readonly FirePumpGesture pump=new FirePumpGesture();
        FirePhase previousPhase;
        Vector3[] waterTargets=System.Array.Empty<Vector3>();
        sealed class TreeView { public Transform Root; public Renderer Leaves, Trunk; public Renderer[] Authored; }
        Transform grove, hud, dashboard, shop, rack, rackControls, aimMarker;
        FireEquipmentView heldView, rackView;
        LineRenderer coverage;
        readonly FireEquipmentGrab grip=new FireEquipmentGrab();
        public bool HoldingEquipment => grip.Held;
        public bool ShopOpen { get; private set; }
        public FireEquipmentId SelectedEquipment { get; private set; }
        TextMesh shopName,shopDescription,shopStats,shopState,shopBuyText,shopUpgradeText,rackLabel,stationUpgradeText;
        WorkshopButton shopBuy,shopUpgrade,previousOwned,nextOwned,stationUpgrade;
        bool introStarted;
        TextMesh status, economy, help, mission, tactics, actionText, upgradeText, handText, reward;
        WorkshopButton actionButton, upgradeButton;
        MaterialPropertyBlock tint;
        float rewardUntil;
        bool focused=true, paused, desktopReady, xrPlaced, returningToCamp;
        Vector3 rewardStart;
        bool savedFog; Color savedAmbient; UnityEngine.Rendering.AmbientMode savedAmbientMode;

        IEnumerator Start()
        {
            gameObject.AddComponent<EmberdeepLocalizedText>();
            if(StandaloneShowcase){Run=EmberdeepSession.Run;SelectedEquipment=Run.Equipped;}
            navigation=GardenNavigation.Current;
            Player=navigation?navigation.Player:null;
            if(!Player)
            {
                var instance=Instantiate(PlayerPrefab);
                Player=instance.GetComponentInChildren<MetaPlayerRig>(true);
                Player.Initialize(Player.XRRig,Player.DesktopCamera);
                navigation=new GameObject("Fire navigation").AddComponent<GardenNavigation>();
                navigation.InitializeStandaloneFire(Player);
            }
            hands=gameObject.AddComponent<MetaShadowPlayer>(); hands.Player=Player;hands.IndependentPortableAim=true;
            handVisuals=gameObject.AddComponent<EmberdeepHandVisuals>();handVisuals.Configure(hands);
            savedAmbient=RenderSettings.ambientLight; savedAmbientMode=RenderSettings.ambientMode;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight=new Color(.62f,.69f,.68f);
            savedFog=RenderSettings.fog; RenderSettings.fog=false;
            tint=new MaterialPropertyBlock();
            art=EmberdeepArtLibrary.Load();
            if(art){GroundMaterial=art.Ground;FoliageMaterial=art.Foliage;BarkMaterial=art.Wood;PanelMaterial=art.Panel;WaterMaterial=art.Accent;}
            if(StandaloneShowcase && art && art.Pipeline)
            {previousPipeline=QualitySettings.renderPipeline;QualitySettings.renderPipeline=art.Pipeline;changedPipeline=true;}
            Effects=new GameObject("Fire and water effects").AddComponent<FireRescueVfx>();Effects.transform.SetParent(transform,false);Effects.Initialize();
            Feedback=gameObject.AddComponent<FireFeedback>();Feedback.Initialize();
            diorama=gameObject.AddComponent<FireDiorama>();
            Arena=new GameObject("Fire rescue arena").transform; Arena.SetParent(transform,false);
            BuildWorld();
            EnvironmentVfx=new GameObject("Creek and forest atmosphere").AddComponent<EmberdeepEnvironmentVfx>();
            EnvironmentVfx.Initialize(Arena);
            BuildWaterStations();BuildMagic();BuildSpells();
            Recenter();
            Run.Extinguished+=OnExtinguished;
            Run.AnimalRescued+=OnAnimalRescued;
            Run.PropActivated+=OnPropActivated;Run.CabinLost+=OnCabinLost;
            if(StandaloneShowcase && Run.Trees.Length>0)BuildIncident();else StartNext();
            if(!Player.IsXR && !Run.IsPalmLesson)TakeEquipment();
            if(StandaloneShowcase)
            {ImmersiveSky=gameObject.AddComponent<EmberdeepImmersiveSky>();ImmersiveSky.Initialize(Player.ViewCamera,Arena,true);}
            yield break;
        }
        void BuildWorld()
        {
            var terrain=art?art.Create("Terrain",Arena,Vector3.zero):null;bool authored=terrain;
            if(authored)
            {
                FitBoard(terrain); // compact board (§5 Reach): FireRescueGame.Placement.cs
                var border=new GameObject("Decorative healthy rim grove").transform;border.SetParent(Arena,false);
                BuildRimGrove(border);
            }
            if(!authored)
            {
                Shape("Forest island",PrimitiveType.Cube,Arena,new Vector3(0,-.12f,6),new Vector3(11,.22f,13),GroundMaterial);
                Shape("Fire break",PrimitiveType.Cube,Arena,new Vector3(0,.004f,1.7f),new Vector3(10,.02f,.12f),PanelMaterial);
                Shape("Diorama plinth",PrimitiveType.Cube,Arena,new Vector3(0,-.5f,6),new Vector3(11.3f,.6f,13.3f),PanelMaterial);
                for(int i=0;i<4;i++)Shape("Miniature rock",PrimitiveType.Sphere,Arena,new Vector3(i%2==0?-4.7f:4.7f,.08f,3+i*2.1f),new Vector3(.7f,.45f,.8f),BarkMaterial);
                FitPrimitiveBoard(); // compact board (§5 Reach): FireRescueGame.Placement.cs
            }
            hud=new GameObject("Rescue station").transform; hud.SetParent(transform,false); hud.localScale=Vector3.one*.25f;
            dashboard=new GameObject("Main controls").transform;dashboard.SetParent(hud,false);
            Shape("Dashboard",PrimitiveType.Cube,dashboard,new Vector3(0,-.23f,.025f),new Vector3(3.05f,1.48f,.06f),PanelMaterial);
            Text(dashboard,StandaloneShowcase?"E M B E R D E E P  /  FIRE EXPEDITION":"F I R E   R E S C U E  /  MINI WORLD",new Vector3(0,.39f,-.03f),.055f,new Color(1,.67f,.22f));
            status=Text(dashboard,"FIRST RESPONSE",new Vector3(0,.25f,-.03f),.053f,Color.white);
            economy=Text(dashboard,"",new Vector3(0,.12f,-.03f),.039f,new Color(.45f,.9f,1));
            mission=Text(dashboard,"",new Vector3(0,0,-.03f),.033f,new Color(1,.85f,.55f));
            tactics=Text(dashboard,"",new Vector3(0,-.12f,-.03f),.033f,new Color(.6f,1,.8f));
            help=Text(dashboard,"",new Vector3(0,-.26f,-.03f),.031f,new Color(.78f,.84f,.82f));
            // Controls sit below the aim path. Release the trigger before pointing at them.
            actionButton=Button("Next incident",new Vector3(-.88f,-.47f,0),()=>{if(Run.Supply.Locked)Run.Supply.BeginAssistedRefill();else StartNext();},out actionText);
            upgradeButton=Button("Gear shop",new Vector3(0,-.47f,0),OpenShop,out upgradeText);
            Button("Return equipment",new Vector3(.88f,-.47f,0),()=>{if(grip.Held)ReturnEquipment();else TakeEquipment();},out handText);
            Button("Recenter",new Vector3(-.88f,-.64f,0),Recenter,out var recenter); recenter.text="RECENTER";
            Button("Retry sector",new Vector3(0,-.64f,0),RetrySector,out var reset);reset.text="RETRY SECTOR";
            Button(StandaloneShowcase?"Return to camp":"Main menu",new Vector3(.88f,-.64f,0),ReturnToMenu,out var back);back.text=StandaloneShowcase?"FIRE CAMP":"MAIN MENU";
            rack=new GameObject("Equipment rack").transform;rack.SetParent(transform,false);
            Shape("Equipment pedestal",PrimitiveType.Cube,rack,new Vector3(0,-.17f,.04f),new Vector3(.24f,.025f,.3f),PanelMaterial);
            EmberdeepCraftDetails.Rack(rack);
            rackLabel=Text(rack,"",new Vector3(0,-.21f,-.12f),.017f,Color.white);
            rackControls=new GameObject("Owned equipment carousel").transform;rackControls.SetParent(rack,false);rackControls.localPosition=new Vector3(0,-.18f,-.28f);rackControls.localScale=Vector3.one*.18f;
            previousOwned=Button("Previous owned tool",new Vector3(-.46f,0,0),()=>CycleOwnedEquipment(-1),out var previousTool,rackControls);previousTool.text="< TOOL";
            nextOwned=Button("Next owned tool",new Vector3(.46f,0,0),()=>CycleOwnedEquipment(1),out var nextTool,rackControls);nextTool.text="TOOL >";
            previousTool.characterSize=nextTool.characterSize=.06f*20/64;
            BuildShop();BuildQuitPanel();RefreshEquipment();
            aimMarker=Shape("Spray aim",PrimitiveType.Sphere,Arena,Vector3.zero,Vector3.one*.18f,WaterMaterial).transform;
            coverage=new GameObject("Splash footprint preview").AddComponent<LineRenderer>();coverage.transform.SetParent(transform,false);coverage.sharedMaterial=WaterMaterial;coverage.positionCount=32;coverage.loop=true;coverage.startWidth=coverage.endWidth=.0015f;coverage.enabled=false;
            reward=Text(Arena,"",Vector3.zero,.17f,new Color(.5f,1,.7f));
            var lamp=new GameObject("Warm evening sun").AddComponent<Light>();lamp.transform.SetParent(Arena,false);
            lamp.type=LightType.Directional;lamp.intensity=1.1f;lamp.color=new Color(1,.91f,.79f);lamp.shadows=art?LightShadows.Soft:LightShadows.None;lamp.shadowStrength=.72f;lamp.transform.localRotation=Quaternion.Euler(48,-35,0);
        }
        public void StartNext()
        {
            if(Run.NeedsMagicUpgrade){OpenShop();return;}
            StopRefill();
            if(Run.Phase==FirePhase.Preparing){BeginIncident();return;}
            if(!Run.StartStage(StandaloneShowcase && Run.GoddessTutorial))return;
            BuildIncident();
        }
        void BuildIncident()
        {
            rewardUntil=0;celebrationUntil=0;
            if(ShopOpen)CloseShop();previousPhase=Run.Phase;
            ResetInput(); if(grove){grove.gameObject.SetActive(false);Destroy(grove.gameObject);} trees.Clear();
            grove=new GameObject("Incident forest").transform;grove.SetParent(Arena,false);
            for(int i=0;i<Run.Trees.Length;i++)
            {
                var data=Run.Trees[i];var root=new GameObject("Tree "+i).transform;root.SetParent(grove,false);root.localPosition=data.Position-Vector3.up*.7f;
                float scale=1+(i%4)*.08f;
                var model=art?art.Create(i%3==1?"Broadleaf":"Cedar",root,Vector3.zero,scale):null;
                if(model)
                {
                    model.localRotation=Quaternion.Euler(0,i*137.5f,0);
                    trees.Add(new TreeView{Root=root,Authored=model.GetComponentsInChildren<Renderer>()});
                    continue;
                }
                var trunk=Shape("Trunk",PrimitiveType.Cylinder,root,new Vector3(0,.55f,0),new Vector3(.13f,.55f,.13f),BarkMaterial);
                var leaves=Shape("Canopy",PrimitiveType.Sphere,root,new Vector3(0,1.22f,0),new Vector3(.75f,1.05f,.75f)*scale,FoliageMaterial);
                if(i%3!=1)leaves.GetComponent<MeshFilter>().sharedMesh=diorama.PineMesh;
                trees.Add(new TreeView{Root=root,Leaves=leaves.GetComponent<Renderer>(),Trunk=trunk.GetComponent<Renderer>()});
            }
            // Heat bar, burning pin and see-through fade live in EmberdeepTreeReadout (§4.6).
            BuildTreeReadout();
            diorama.Build(Run,Arena,FoliageMaterial,BarkMaterial,WaterMaterial,PanelMaterial,art);
            BuildBoardPieces(); // tray, pieces, Plan look (FireRescueGame.Pieces.cs)
            if(SheepView){SheepView.gameObject.SetActive(false);Destroy(SheepView.gameObject);}
            SheepView=new GameObject("Woodland rescue flock").AddComponent<EmberdeepSheepView>();
            SheepView.transform.SetParent(Arena,false);SheepView.Initialize(Run);
            if(creekStation)creekStation.gameObject.SetActive(!Run.GoddessTutorial && Run.Stage>0);
            waterTargets=new Vector3[Run.Trees.Length+Run.Props.Length];UpdateTargets();ConfigurePalmLesson();
            Refresh();
        }
        public void ReturnToMenu()
        {
            if(returningToCamp)return;
            Run.EvacuateSafeAnimals();
            ResetInput();
            if(StandaloneShowcase){returningToCamp=true;SceneManager.LoadSceneAsync(EmberdeepSession.CampScene,LoadSceneMode.Single);}
            else navigation.ReturnToMainMenu();
        }
        public bool BuyUpgrade() { bool result=Run.BuyUpgrade();if(result){reward.text="NOZZLE UPGRADED";rewardStart=new Vector3(0,3,4);rewardUntil=Time.unscaledTime+2;} Refresh();return result; }
        public void Recenter()
        {
            if(!Player || !Arena)return;
            ResetInput();
            if(Player.IsXR){if(!acquiringPose)StartCoroutine(PlaceStableFireground());return;}
            ApplyWorldPose();
        }
        void ApplyWorldPose()
        {
            if(!Player.IsXR)
            {
                Player.ViewCamera.transform.position=new Vector3(0,1.65f,0);
                Player.ViewCamera.transform.rotation=Quaternion.Euler(Run.CompactBoard?DesktopPitchCompact:DesktopPitchLegacy,0,0);
                Player.ViewCamera.fieldOfView=70;
                Player.ViewCamera.backgroundColor=new Color(.065f,.12f,.145f);
            }
            var view=Player.ViewCamera.transform;
            PlaceArena(view); // compact or legacy, plus the play-area options: FireRescueGame.Placement.cs
            PlaceHud();
            xrPlaced=Player.IsXR && hands.HeadReady;
            PositionRack();UpdateTargets();
            PositionWaterStations();
        }

        void Update()
        {
            if(!Player || !Arena || trees.Count==0 || acquiringPose){Run.ResetRecovery();return;}
            bool ready=focused && !paused && hands.HeadReady && !navigation.IsTransitioning && !returningToCamp;
            if(!ready){ResetInput(true,!returningToCamp && !navigation.IsTransitioning);return;} // Input pauses; last held pose stays visual only.
            if(hydrantStation)hydrantStation.TickOutlet(Time.unscaledDeltaTime);
            if(Player.IsXR && !xrPlaced){Recenter();return;}
            UpdateTargets();
            if(Player.IsXR){ReadObjectHands();ReadSheepHands(Time.unscaledDeltaTime);ReadMagicHands(Time.unscaledDeltaTime);ReadSpellHands(Time.unscaledDeltaTime);}
            if(TickRimInput(Time.unscaledDeltaTime)){Run.ResetRecovery();CancelWeaponInput();Refresh();return;} // token, bell, slap (§11b)
            if(ProcessInterfaceInput()){Run.ResetRecovery();Refresh();return;}
            if(QuitConfirmationOpen || QuitRequested || ShopOpen || RimBlocksPlay){Run.ResetRecovery();CancelWeaponInput();Refresh();return;}
            TickBoardPieces(Time.unscaledDeltaTime); // pieces by hand, Plan and Fight (FireRescueGame.Pieces.cs)
            if(Run.Phase==FirePhase.Preparing)
            {
                float prepareDt=Mathf.Min(Time.unscaledDeltaTime,.1f);
                if(Player.IsXR){ProcessToolPickup(prepareDt);ProcessCarriedTool(prepareDt,!interfaceHovered);}
                if(Run.SpellMode)TickSpellUpkeep(prepareDt);
                CancelWeaponInput();Run.TickPreparation(prepareDt);
                Run.TickPreparedAnimals(prepareDt);
                Effects.Tick(Run,Arena,jet,prepareDt);diorama.Tick(Run,Player.ViewCamera,prepareDt);
                if(Run.Phase==FirePhase.Fighting){introStarted=true;previousPhase=Run.Phase;handMenu?.ResetInput();}
                Refresh();return;
            }
            if(Player.IsXR && !Run.Supply.Locked && !interfaceHovered)
            {
                ProcessToolPickup(Time.unscaledDeltaTime);
            }
            bool trigger=false, valid=true;float pinch=float.PositiveInfinity;
            Ray ray=new Ray(Player.ViewCamera.transform.position,Player.ViewCamera.transform.forward);
            Ray menuRay=ray;Pose palm=default;
            WorkshopButton hovered=null;
            if(Player.IsXR)
            {
                ProcessCarriedTool(Time.unscaledDeltaTime,!interfaceHovered && !Run.Supply.Locked);
                var primary=toolHands[grip.Hand];valid=primary.Usable;palm=primary.Palm;
                pinch=objectHands[grip.Hand].Pinch;
                ray=new Ray(heldView.Muzzle.position,heldView.Muzzle.forward);
            }
            else if(Mouse.current!=null)
            {
                var mouse=Mouse.current;ray=Player.ViewCamera.ScreenPointToRay(mouse.position.ReadValue());menuRay=ray;
                if(!mouse.leftButton.isPressed)desktopReady=true;
                trigger=desktopReady && mouse.leftButton.isPressed;
                if(Keyboard.current!=null)
                {
                    if(Keyboard.current.uKey.wasPressedThisFrame)BuyUpgrade();
                    if(Keyboard.current.bKey.wasPressedThisFrame)OpenShop();
                    for(int i=0;i<4 && !Run.SpellMode;i++)if(Keyboard.current[(Key)((int)Key.Digit1+i)].wasPressedThisFrame && Run.Owns((FireEquipmentId)i)){SelectedEquipment=(FireEquipmentId)i;TakeEquipment();}
                    if(Keyboard.current.nKey.wasPressedThisFrame)StartNext();
                }
            }
            bool overMenu=interfaceHovered;
            if(Player.IsXR)trigger=toolManipulation.Firing;
            if(overMenu)
            {
                trigger=false;
            }
            if(overMenu || !valid || !grip.Held){Run.CancelTrigger();pump.Reset();}
            heldView.gameObject.SetActive(grip.Held);
            var nozzlePosition=Player.IsXR?GripPoint(LeftHanded,palm):Player.ViewCamera.transform.TransformPoint(FireMiniature.DesktopGripLocal);
            float speed=Run.Speed;
            Vector3 velocity=ray.direction*speed;
            if(!Player.IsXR)
            {
                var target=WaterJet.MouseTarget(ray,Arena,waterTargets);
                // Mouse points at a landing spot; VR uses the actual hand-held barrel direction.
                var offset=heldView.MuzzleOffset;
                velocity=WaterJet.AimFromGrip(nozzlePosition,target,speed,offset);
            }
            var rotation=Quaternion.LookRotation(velocity);
            if(valid && !Player.IsXR){heldView.transform.SetPositionAndRotation(nozzlePosition-rotation*heldView.Grip.localPosition,rotation);heldView.UpdateHose();}
            if(ProcessRefillInput(palm,valid,Time.unscaledDeltaTime))
            {
                Run.ResetRecovery();
                CancelWeaponInput();
                float supplyDt=Run.Supply.Assisted?0:Mathf.Min(Time.unscaledDeltaTime,.1f);
                Run.Tick(introStarted?supplyDt:0,false);Run.TickMagic(supplyDt,false,introStarted);
                FanSpraying=false;TickSpellUpkeep(supplyDt);
                if(!Run.Supply.Assisted)jet.Step(supplyDt,heldView.Muzzle.position,Vector3.zero,0,Run.Range*FireMiniature.Scale,Arena,waterTargets,OnWaterImpact);
                heldView.gameObject.SetActive(Run.Supply.Locked || grip.Held);
                heldView.Animate(Run.Water/Run.Capacity,0,false,Time.unscaledDeltaTime);
                Feedback.Tick(Run.Supply.ValveOpen,0,FireDelivery.Foam,Time.unscaledDeltaTime);
                Effects.Tick(Run,Arena,jet,Time.unscaledDeltaTime);diorama.Tick(Run,Player.ViewCamera,Time.unscaledDeltaTime);
                Refresh();return;
            }
            var origin=heldView.Muzzle.position;
            float dt=Mathf.Min(Time.unscaledDeltaTime,.1f);
            if(Player.IsXR && heldView.Pump && valid && grip.Held && !overMenu && toolManipulation.TriggerValid)
            {
                var other=toolHands[1-grip.Hand];
                pump.SampleGrasp(other.Usable,other.Closure,other.Contact,new Pose(heldView.Pump.position,heldView.transform.rotation),dt);
                if(pump.Cancelled){Run.CancelTrigger();gesture.Reset();toolManipulation.CancelActions();trigger=false;}
                if(pump.Active || pump.Released)
                {Run.PrimeCannon(pump.Pull01);trigger=pump.Active && toolManipulation.Region==FireToolRegion.Trigger;gesture.Reset();}
            }
            bool magicCast=Player.IsXR?palmMagic.Casting:Keyboard.current!=null && Keyboard.current.spaceKey.isPressed;
            bool magicAllowed=!overMenu && !ShopOpen && !Run.Supply.Locked
                && (Player.IsXR?palmMagic.Aiming && !ToolOwnsHand(0) && !ToolOwnsHand(1):true);
            if(magicCast && magicAllowed)introStarted=true;
            float magicAmount=Run.TickMagic(dt,magicCast && magicAllowed,introStarted);
            MagicSpraying=magicAmount>0;
            if(MagicSpraying){trigger=false;Run.CancelTrigger();}
            float launchCharge=Run.Charge01;
            Spraying=Run.Tick(introStarted?dt:0,trigger && valid && grip.Held && !ShopOpen && !Run.IsPalmLesson,pump.Active);
            ProcessWaterRecovery(dt,RecoveryTrackingAvailable(valid),RecoveryTriggerHeld(trigger,pinch));
            if(Spraying && Run.Equipment.Delivery==FireDelivery.Charge)
            {Effects.Launch(origin,velocity.normalized,launchCharge);Feedback.Launch(launchCharge);}
            if(MagicSpraying)
            {
                origin=Player.IsXR?palmMagic.Origin:Player.ViewCamera.transform.TransformPoint(new Vector3(-.12f,-.14f,.30f));
                var target=Player.IsXR?palmMagic.Target:WaterJet.MouseTarget(ray,Arena,waterTargets);
                velocity=WaterJet.AimVelocity(origin,target,2.8f);
                jet.Step(dt,origin,velocity,magicAmount,Run.MagicRange,Arena,waterTargets,OnWaterImpact,Run.MagicRadius,3,0,FireDelivery.Foam,true);
            }
            else jet.Step(dt,origin,velocity,Run.SprayAmount,Run.Range*FireMiniature.Scale,Arena,waterTargets,OnWaterImpact,Run.ShotRadius,Run.ShotWetness,Run.ShotSpread,Run.Equipment.Delivery);
            TickSpells(dt,overMenu,ray);
            bool predicted=WaterJet.Predict(origin,velocity,Run.Range*FireMiniature.Scale,Arena,waterTargets,out var impact);
            aimMarker.gameObject.SetActive(valid && grip.Held && !overMenu && predicted);
            coverage.enabled=aimMarker.gameObject.activeSelf;
            if(predicted)
            {
                aimMarker.position=impact.Point;
                float radius=(Run.Equipment.Delivery==FireDelivery.Charge?1.2f+2*Run.Charge01+Run.Upgrade*.2f:.6f+Run.Width)*FireMiniature.Scale;
                for(int i=0;i<32;i++){float angle=i*Mathf.PI*2/32;coverage.SetPosition(i,impact.Point+Arena.up*.003f+(Arena.right*Mathf.Cos(angle)+Arena.forward*Mathf.Sin(angle))*radius);}
            }
            heldView.Animate(Run.Water/Run.Capacity,Run.Charge01,Spraying,dt);
            heldView.AnimatePump(pump.Pull01);
            Feedback.Tick(Spraying || MagicSpraying || FanSpraying,Mathf.Max(Run.Charge01,SpellCharge01),FanSpraying?FireDelivery.Fan:MagicSpraying?FireDelivery.Foam:Run.Equipment.Delivery,dt);
            for(int i=0;i<Run.Props.Length;i++)if(Run.Props[i].Kind==FirePropKind.Reservoir && Run.Props[i].ActiveRemaining>0)
                Effects.Sprinkler(Arena.TransformPoint(Run.Props[i].Position),FireRun.ReservoirRadius*FireMiniature.Scale,dt);
            Effects.Tick(Run,Arena,jet,dt);
            diorama.Tick(Run,Player.ViewCamera,dt);
            if(Run.Phase!=previousPhase)
            {
                Feedback.Complete(Run.Phase!=FirePhase.Lost);
                if(Run.Phase==FirePhase.Cleared || Run.Phase==FirePhase.Won)BeginCelebration(Run.Phase==FirePhase.Won?8:5);
                previousPhase=Run.Phase;
            }
            Refresh();TickShopLesson(dt);
        }
        void UpdateTargets()
        {
            int count=Run.Trees.Length+Run.Props.Length+Run.Animals.Length;
            if(waterTargets.Length!=count)waterTargets=new Vector3[count];
            for(int i=0;i<Run.Trees.Length;i++)waterTargets[i]=Arena.TransformPoint(Run.Trees[i].State==TreeState.Cleared?new Vector3(0,-1000,0):Run.Trees[i].Position); // dug: gone
            for(int i=0;i<Run.Props.Length;i++)waterTargets[Run.Trees.Length+i]=Arena.TransformPoint(Run.Props[i].Position);
            for(int i=0;i<Run.Animals.Length;i++)
            {
                var a=Run.Animals[i];bool active=a.State==AnimalState.Waiting || a.State==AnimalState.Running;
                waterTargets[Run.Trees.Length+Run.Props.Length+i]=Arena.TransformPoint(active?a.Position+Vector3.up*FireRun.AnimalHeight:new Vector3(0,-1000,0));
            }
        }
        void OnWaterImpact(WaterJet.Impact hit)
        {
            bool hot=hit.Tree>=0 && hit.Tree<Run.Trees.Length && Run.Trees[hit.Tree].State==TreeState.Burning;
            bool prop=hit.Tree>=Run.Trees.Length && hit.Tree<Run.Trees.Length+Run.Props.Length;
            if(!hot && !prop && hit.Tree<0)hot=PalmSplashReachesFire(hit); // a palm root splash on the board (before ApplyWater saves it)
            if(prop)Run.ApplyPropWater(hit.Tree-Run.Trees.Length,hit.Amount);
            // Palm water counts along the trunk column (FireRun.WaterReaches column, PalmColumn); everything else at the .7 point.
            if(!prop || hit.Delivery==FireDelivery.Charge)Run.ApplyWater(Arena.InverseTransformPoint(hit.Point),hit.Amount,hit.Radius,hit.Wetness,PalmColumn(hit));
            if(hot)
            {
                if(!hit.Magic)Run.LearnWeapon(Run.Equipped);
                else if(hit.Delivery==FireDelivery.Foam)Run.CompletePalmTutorial();
                // The orb is the only charged spell cast with a soaking splash.
                else Run.Learn(hit.Delivery==FireDelivery.Fan?EmberdeepInteractionDemo.Action.Fan:hit.Wetness>=1.5f?EmberdeepInteractionDemo.Action.Orb:EmberdeepInteractionDemo.Action.Bolt);
            }
            Effects.Impact(hit,hot);
            Feedback.Impact(hot);
            if(hit.Delivery==FireDelivery.Charge)Effects.Burst(hit.Point,hit.Radius*FireMiniature.Scale);
        }
        void ResetInput(bool clearProjectiles=true,bool retainHeldPresentation=false)
        {
            sheepInteraction.Reset();
            ResetMagic();ResetSpells();Run.ResetRecovery();
            if(recoveryLabel)recoveryLabel.gameObject.SetActive(false);
            if(recoveryDroplet)recoveryDroplet.gameObject.SetActive(false);
            StopRefill();CancelWeaponInput();if(handMenu)handMenu.ResetInput();interfaceHovered=false;
            if(hydrantStation)hydrantStation.ShowTutorial(Run,heldView,grip.Held,null,false);
            if(creekStation)creekStation.ShowTutorial(Run,heldView,grip.Held,null,false);
            if(clearProjectiles)jet.Clear();
            if(Feedback)Feedback.Silence();
            if(Effects && clearProjectiles)Effects.Clear();if(heldView)heldView.gameObject.SetActive(retainHeldPresentation && grip.Held);if(aimMarker)aimMarker.gameObject.SetActive(false);if(coverage)coverage.enabled=false;
        }
        public void Refresh()
        {
            if(!status)return;
            if(SheepView)SheepView.Render();
            float now=Time.unscaledTime;
            RefreshTrees(); // tint, readout and see-through fade: FireRescueGame.Trees.cs
            string phase=Run.Phase==FirePhase.Won?"FOREST SAVED!":Run.Phase==FirePhase.Lost?"FOREST LOST - TRY AGAIN":Run.Phase==FirePhase.Cleared?"INCIDENT CONTAINED":Run.StageName;
            status.text=$"{Run.Stage+1}/{Run.StageCount}  {phase}    SHEEP {Run.AnimalsSaved}/{Run.Animals.Length}";
            economy.text=$"$ {Run.Coins}  |  {Run.Equipment.Name} +{Run.Upgrade}  |  WATER {Run.Water:0}/{Run.Capacity:0}";
            mission.text=Run.Objective;
            tactics.text=Run.Phase==FirePhase.Cleared || Run.Phase==FirePhase.Won?$"STARS {new string('*',Run.Stars)}  |  CABIN BONUS $ {Run.StageBonus}  |  SCORE {Run.Score}":$"CABINS {Run.CabinsSaved}/{Run.CabinsTotal}  |  CHAIN x{Run.Combo}  |  {Run.Elapsed:0}s / {Run.ParTime:0}s PAR";
            help.text=Run.Phase==FirePhase.Cleared?"Spend your reward, then take on the next fire.":Run.Phase==FirePhase.Won?"Forest saved. Return to camp.":Run.Phase==FirePhase.Lost?"Too many trees burned. Retry and keep your earned coins.":Run.Water<1?"EMPTY - dock at the free cyan water source":Player.IsXR?"Grip the handle; squeeze to fire.":"Mouse: spray | H: water station | C: creek | B: shop";
            if(!grip.Held)help.text="GRASP the gold top handle / other hand: cyan nozzle.";
            else if(Run.Phase==FirePhase.Fighting)help.text=Run.Charge01>0?$"CHARGING {Run.Charge01:P0} - release to launch":Run.Equipment.Controls;
            if(Run.Phase==FirePhase.Fighting && Run.Equipment.Delivery==FireDelivery.Charge && Player.IsXR && grip.Held && Run.Charge01<=0)
                help.text="SQUEEZE grip to charge / or grasp cyan pump, pull, release.";
            if(!Player.IsXR && !grip.Held)help.text="TAKE OWNED TOOL below. B: shop between incidents. 1-4: owned gear.";
            if(Run.Water<1)help.text="EMPTY - dock the cyan coupling at the free water source";
            if(Run.Equipment.Delivery==FireDelivery.Charge && grip.Held)
                help.text=Run.Water<8?"REFILL - cannon needs at least 8 water":$"CHARGE {Run.Charge01:P0} / LIMIT {Run.AffordableCharge01:P0} | COST {8+20*Run.Charge01:0} | RELEASE TO LAUNCH";
            if(Player.IsXR && grip.Held && !Run.Supply.Locked)
            {
                if(toolManipulation.Adjusting)help.text="GRASP the top handle to spray / GRASP with the other hand to transfer";
                else if(toolManipulation.Region!=FireToolRegion.Trigger)help.text="OPEN this hand, then GRASP the top handle / other hand: GRASP to transfer";
                else if(heldView.Nozzle && !toolManipulation.NozzleHeld)help.text="GRASP the nozzle with your other hand to aim";
                else if(toolManipulation.NeedsTriggerRelax)help.text="RELAX the top hand once, then SQUEEZE to spray. Your tool stays held.";
                else help.text="SQUEEZE to spray / RELAX to stop / OPEN PALM to float / re-grasp within 5 seconds";
            }
            if(Run.Supply.Locked)help.text=Run.Supply.Status;
            if(!Player.IsXR && !Run.Supply.Locked)help.text+=" | H: dock water station";
            RefreshWaterStations();
            RefreshShop();
            RefreshRecoveryFeedback();
            if(rackControls)rackControls.rotation=Player.ViewCamera.transform.rotation;
            actionButton.Interactable=Run.Supply.Locked || Run.Phase==FirePhase.Cleared;
            actionText.text=Run.Supply.Locked?(Run.Supply.Assisted?"REFILL ASSIST ON":"REFILL ASSIST"):Run.Phase==FirePhase.Cleared?"NEXT INCIDENT >":"CONTAIN THE FIRE";
            upgradeButton.Interactable=Run.InSupply;
            upgradeText.text=Run.InSupply?"GEAR SHOP >":Run.Phase==FirePhase.Won?"SHOP CLOSED":"SHOP AFTER ROUND";
            handText.text=grip.Held?"RETURN TOOL":"TAKE OWNED TOOL";RefreshMagicText();RefreshSpellText();
            RefreshPreparation();RefreshRim();
            reward.gameObject.SetActive(now<rewardUntil);
            if(now<rewardUntil){reward.transform.localPosition=rewardStart+Vector3.up*(2-(rewardUntil-now))*.16f;reward.transform.rotation=Player.ViewCamera.transform.rotation;}
        }
        void PositionRack()
        {
            if(Arena && rack)rack.SetPositionAndRotation(Arena.TransformPoint(new Vector3(6,2.2f,1)),Arena.rotation*Quaternion.Euler(0,-25,0));
        }
        public bool TakeEquipment(int hand=1)
        {
            if(Run.SpellMode || Run.IsPalmLesson || ShopOpen || !Run.Equip(SelectedEquipment))return false;
            ResetInput(false);grip.EquipForDesktop(hand);toolManipulation.Prepare(hand);LeftHanded=hand==0;introStarted=true;RefreshEquipment();PositionWaterStations();return true;
        }
        public void ReturnEquipment(){grip.Return();toolManipulation.Reset();ResetInput(false);PositionRack();if(rackView)rackView.gameObject.SetActive(!Run.IsPalmLesson && !Run.SpellMode);}
        public void CycleOwnedEquipment(int direction)
        {
            if(ShopOpen)return;
            int current=(int)SelectedEquipment;
            for(int step=1;step<=4;step++)
            {
                var next=(FireEquipmentId)((current+(direction<0?-step:step)+8)%4);
                if(!Run.Owns(next) || (int)next==current)continue;
                ReturnEquipment();SelectedEquipment=next;RefreshEquipment();Refresh();return;
            }
        }
        void RefreshEquipment()
        {
            if(heldView){heldView.gameObject.SetActive(false);Destroy(heldView.gameObject);}
            if(rackView){rackView.gameObject.SetActive(false);Destroy(rackView.gameObject);}
            heldView=new GameObject("Held "+Run.Equipment.Name).AddComponent<FireEquipmentView>();heldView.transform.SetParent(transform,false);heldView.Build(Run.Equipped,FoliageMaterial,PanelMaterial,WaterMaterial);heldView.gameObject.SetActive(grip.Held);
            heldView.SetLeftHanded(LeftHanded);
            rackView=new GameObject("Rack "+FireEquipment.Get(SelectedEquipment).Name).AddComponent<FireEquipmentView>();rackView.transform.SetParent(rack,false);rackView.Build(SelectedEquipment,FoliageMaterial,PanelMaterial,WaterMaterial);rackView.gameObject.SetActive(!grip.Held && !Run.IsPalmLesson && !Run.SpellMode);
            ConfigureHandOcclusion();
        }
        public void OpenShop()
        {
            if(!Run.InSupply)return;
            if(Run.NeedsMagicUpgrade)Run.EvacuateSafeAnimals();
            ReturnEquipment();ShopOpen=true;ShopPage=Run.IsPalmLesson?EmberdeepShopPage.Magic:EmberdeepShopPage.Equipment;
            // A spell that has just become affordable is what the player came for.
            if(Run.SpellMode && !Run.NeedsMagicUpgrade)for(int i=1;i<4;i++)if(Run.CanBuy((FireEquipmentId)i)){SelectedEquipment=(FireEquipmentId)i;ShopPage=EmberdeepShopPage.Equipment;break;}
            // The spell flow shows the upgrade tiles (FireRescueGame.Screens.cs) instead of the gear shop panel.
            ShopShowingCurrentStation=false;dashboard.gameObject.SetActive(false);shop.gameObject.SetActive(!RimUi);PlaceHud();RefreshShop();
            if(RimUi)RefreshScreens();
        }
        public void CloseShop()
        {
            ShopOpen=false;shop.gameObject.SetActive(false);if(!RimUi)dashboard.gameObject.SetActive(true);
            if(!Run.Owns(SelectedEquipment))SelectedEquipment=Run.Equipped;
            RefreshEquipment();ResetInput();PlaceHud();Refresh();
        }
        public void SelectEquipment(int index){if(Run.NeedsMagicUpgrade)return;SelectedEquipment=(FireEquipmentId)((index%4+4)%4);ShopPage=EmberdeepShopPage.Equipment;ShopShowingCurrentStation=false;handMenu?.ResetInput();if(!ShopOpen)RefreshEquipment();RefreshShop();}
        public bool BuySelectedEquipment()
        {
            bool bought=Run.BuyEquipment(SelectedEquipment);RefreshShop();return bought;
        }
        void OnExtinguished(int i)
        {
            // LoopSpec §13: no sentences in a fight. The spell flow pops only a chain count (the out puff marks one tree).
            reward.text=RimUi?(Run.Combo>1?$"CHAIN x{Run.Combo}":""):Run.Combo>1?$"PATH CLEAR   CHAIN x{Run.Combo}":"PATH CLEAR";rewardStart=Run.Trees[i].Position+Vector3.up*.9f;rewardUntil=Time.unscaledTime+2;
            Effects.Rescue(Arena.TransformPoint(Run.Trees[i].Position),Run.Combo);Feedback.Rescue(Run.Combo);
        }
        void OnAnimalRescued(int i)
        {
            reward.text=RimUi?$"SAVED +{Run.AnimalReward}":$"SHEEP SAFE!  + ${Run.AnimalReward}";rewardStart=Run.Animals[i].Refuge+Vector3.up;rewardUntil=Time.unscaledTime+1.5f;
            Feedback.Rescue(1);
        }
        void OnPropActivated(int i){Effects.Burst(Arena.TransformPoint(Run.Props[i].Position),FireRun.ReservoirRadius*FireMiniature.Scale);Feedback.Launch(.4f);reward.text="RAIN ACTIVATED";rewardStart=Run.Props[i].Position+Vector3.up;rewardUntil=Time.unscaledTime+2;}
        void OnCabinLost(int i){reward.text=RimUi?"CABIN LOST":"CABIN LOST - SAVE THE OTHERS";rewardStart=Run.Props[i].Position+Vector3.up;rewardUntil=Time.unscaledTime+2;}
        void UpdateShopInput()
        {
            if(Player.IsXR || Mouse.current==null)return;
            var ray=Player.ViewCamera.ScreenPointToRay(Mouse.current.position.ReadValue());WorkshopButton hovered=null;
            if(Physics.Raycast(ray,out var hit,30,~0,QueryTriggerInteraction.Collide))hovered=hit.collider.GetComponent<WorkshopButton>();
            foreach(var button in buttons)button.SetInteractionState(button==hovered,false);
            if(hovered && buttons.Contains(hovered) && Mouse.current.leftButton.wasPressedThisFrame)hovered.Press();
        }
        WorkshopButton Button(string name,Vector3 at,System.Action press,out TextMesh label,Transform parent=null)
        {
            parent=parent?parent:dashboard;
            var go=Shape(name,PrimitiveType.Cube,parent,at,new Vector3(.82f,.14f,.055f),PanelMaterial,true);
            EmberdeepForestUi.Apply(go,true);
            var button=go.AddComponent<WorkshopButton>();button.Command=name;button.Pressed=press;buttons.Add(button);
            // Emberdeep's shared hand-aim/same-hand pinch gate is the sole UI dispatcher.
            // Independent SDK callbacks could otherwise reuse a refill or trigger pinch.
            // Text uses metre-scale siblings to avoid inheriting the button's nonuniform scale.
            label=Text(parent,name,at+Vector3.back*.035f,.035f,Color.white);return button;
        }
        GameObject Shape(string name,PrimitiveType kind,Transform parent,Vector3 at,Vector3 scale,Material mat,bool collider=false)
        {
            var go=GameObject.CreatePrimitive(kind);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=at;go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=mat;
            if(name=="Dashboard" || name=="Shop panel" || name=="Quit confirmation backdrop" || name=="Preview recess")EmberdeepForestUi.Apply(go);
            go.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            var c=go.GetComponent<Collider>();if(collider)c.isTrigger=true;else{c.enabled=false;Destroy(c);}return go;
        }
        void Tint(Renderer renderer,Color color){tint.Clear();tint.SetColor("_BaseColor",color);renderer.SetPropertyBlock(tint);}
        TextMesh Text(Transform parent,string value,Vector3 at,float size,Color color)
        {
            var go=new GameObject("Label "+value);go.transform.SetParent(parent,false);go.transform.localPosition=at;
            var t=go.AddComponent<TextMesh>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");go.GetComponent<MeshRenderer>().sharedMaterial=t.font.material;
            t.fontSize=64;t.characterSize=size*20/64;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=color;t.text=value;return t;
        }
        void OnApplicationFocus(bool value){focused=value;if(!value)ResetInput(true,true);}
        void OnApplicationPause(bool value){paused=value;if(value)ResetInput(true,true);}
        void OnDisable(){ResetInput();}
        void OnDestroy(){UnhookPlacement();DestroyScreenMaterials();if(recoveryIconMesh)Destroy(recoveryIconMesh);Run.Extinguished-=OnExtinguished;Run.AnimalRescued-=OnAnimalRescued;Run.PropActivated-=OnPropActivated;Run.CabinLost-=OnCabinLost;Run.Supply.DockConfirmed-=OnSupplyDock;Run.Supply.PumpStroke-=OnSupplyStroke;Run.Supply.Disconnected-=OnSupplyDisconnect;RenderSettings.ambientLight=savedAmbient;RenderSettings.ambientMode=savedAmbientMode;RenderSettings.fog=savedFog;if(changedPipeline && QualitySettings.renderPipeline==art.Pipeline)QualitySettings.renderPipeline=previousPipeline;}
    }
}


using UnityEngine;

namespace FireRescue
{
    // WS-01/02/03 interaction blockout. All dimensions are real metres, not forest units.
    // Presentation consumes accepted state; it never transfers water or awards strokes.
    public sealed class EmberdeepWaterStationView : MonoBehaviour
    {
        public Transform Socket { get; private set; }
        public Transform Valve { get; private set; }
        public Transform ValveVisual=>valveVisual;
        public Transform Pump { get; private set; }
        public Transform Intake { get; private set; }
        public Transform IntakeSocket { get; private set; }
        public Transform IntakeVisual { get; private set; }
        public Transform PumpVisual { get; private set; }
        public Transform Lever { get; private set; }
        public Transform LeverVisual { get; private set; }
        public Transform Button { get; private set; }
        public Transform ButtonVisual { get; private set; }
        public WaterStationTier Tier { get; private set; }
        public bool IsCreek { get; private set; }
        public const int OutletParticleBudget=12;
        public const float OutletBurstSeconds=.34f;
        public bool OutletActive=>outletRemaining>0;
        public int OutletParticleCount=>outletDrops?outletDrops.particleCount:0;

        static readonly Color Cyan = new Color(.275f, .831f, .898f);
        static readonly Color Mint = new Color(.545f, .886f, .682f);
        static readonly Color Cream = new Color(.941f, .875f, .753f);
        static readonly Color Charcoal = new Color(.08f, .13f, .15f);
        Material bodyMaterial, darkMaterial, waterMaterial;
        MaterialPropertyBlock properties;
        readonly Renderer[] socketSegments = new Renderer[12];
        Transform valveVisual, gaugeFill, gaugeFrame, controls, intakeRest, intakeArea, instructionPlate;
        Transform wellBody, hydrantBody, buttonBody, leverPivot, leverStand, leverInstruction, buttonInstruction;
        const float LeverLength=.125f;
        float leverTravel, valveAngle, buttonPress;
        Renderer flowLamp;
        TextMesh stateLabel, quantityLabel, tutorialLabel;
        LineRenderer hose, tutorialLine;
        Transform tutorialPlate, tutorialMarker;
        Renderer tutorialMarkerRenderer;
        public EmberdeepRefillTutorial.Cue TutorialCue { get; private set; }
        bool built, wasIntakePlaced, leftHanded;
        float lastTravel;
        LineRenderer outletArc,outletRipple;
        ParticleSystem outletDrops;
        Material outletDropMaterial;
        float outletRemaining;

        public void Build(Material body, Material dark, Material water, bool creek)
        {
            if (built) return;
            properties = new MaterialPropertyBlock();
            built = true;
            IsCreek = creek;
            bodyMaterial = body; darkMaterial = dark; waterMaterial = water;
            transform.localScale = Vector3.one;
            Part("Stable supply base", PrimitiveType.Cube, transform, new Vector3(0, -.012f, .015f),
                new Vector3(.35f, .025f, .27f), Charcoal, darkMaterial);
            Socket = Anchor("Socket", transform, new Vector3(0, .12f, 0));
            Socket.localRotation = Quaternion.Euler(0, 180, 0);
            Part("Socket throat", PrimitiveType.Cylinder, Socket, new Vector3(0, 0, -.012f),
                new Vector3(.073f, .018f, .073f), Charcoal, darkMaterial).transform.localRotation = Quaternion.Euler(90, 0, 0);
            for (int i = 0; i < socketSegments.Length; i++)
            {
                float angle = i * Mathf.PI * 2 / socketSegments.Length;
                var segment = Part("Socket guide " + i, PrimitiveType.Cube, Socket,
                    new Vector3(Mathf.Cos(angle) * .046f, Mathf.Sin(angle) * .046f, .003f),
                    new Vector3(.027f, .009f, .012f), Cyan, waterMaterial);
                segment.transform.localRotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg + 90);
                socketSegments[i] = segment.GetComponent<Renderer>();
            }
            Part("Port support", PrimitiveType.Cube, transform, new Vector3(0, .05f, .043f),
                new Vector3(.09f, .09f, .08f), Charcoal, darkMaterial);
            controls = Anchor("Mirrored controls", transform, Vector3.zero);
            Valve = Anchor("Valve", controls, new Vector3(-.13f, .17f, -.015f));
            valveVisual = Anchor("Valve detent wheel", Valve, Vector3.zero);
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60;
                var spoke = Part("Valve grip rib " + i, PrimitiveType.Cube, valveVisual,
                    Quaternion.Euler(0, 0, angle) * new Vector3(0, .025f, 0),
                    new Vector3(.015f, .055f, .019f), Cyan, waterMaterial);
                spoke.transform.localRotation = Quaternion.Euler(0, 0, angle);
            }
            Part("Valve hub", PrimitiveType.Sphere, valveVisual, Vector3.zero,
                new Vector3(.035f, .035f, .025f), Cream, bodyMaterial);
            Label("Valve instruction", controls, new Vector3(-.13f, .232f, -.022f), "GRIP + TURN 45", .008f, Cream);
            Pump = Anchor("Pump", controls, new Vector3(-.13f, .12f, -.015f));
            PumpVisual = Anchor("RefillPump handle", controls, Pump.localPosition);
            Part("Broad pump handle", PrimitiveType.Capsule, PumpVisual, Vector3.zero,
                new Vector3(.055f, .018f, .04f), Cyan, waterMaterial).transform.localRotation = Quaternion.Euler(0, 0, 90);
            Tube("RefillPump rail", controls, Pump.localPosition + Vector3.back * .10f,
                Pump.localPosition + Vector3.forward * .045f, .006f, Charcoal, darkMaterial);
            Label("Pump instruction", controls, new Vector3(-.13f, .195f, -.025f), "PULL + RETURN", .008f, Cream);
            Valve.gameObject.SetActive(!creek);
            controls.Find("Valve instruction").gameObject.SetActive(!creek);
            Pump.gameObject.SetActive(creek); PumpVisual.gameObject.SetActive(creek);
            controls.Find("RefillPump rail").gameObject.SetActive(creek);
            controls.Find("Pump instruction").gameObject.SetActive(creek);

            // WS-04 replacement contract: keep Lever neutral and animate the
            // separate LeverVisual endpoint around LeverPivot. All scales stay
            // positive when controls move to the other side of the socket.
            Lever=Anchor("Lever",controls,new Vector3(-.13f,.20f,-.06f));
            leverPivot=Anchor("LeverPivot",controls,new Vector3(-.13f,.20f,.065f));
            leverStand=Part("Well lever support",PrimitiveType.Cube,controls,new Vector3(-.13f,.10f,.065f),
                new Vector3(.025f,.19f,.035f),new Color(.13f,.43f,.39f),bodyMaterial).transform;
            Tube("Well lever arm",leverPivot,Vector3.zero,new Vector3(0,0,-LeverLength),.008f,Cream,bodyMaterial);
            LeverVisual=Anchor("LeverVisual",leverPivot,new Vector3(0,0,-LeverLength));
            Part("Well broad lever grip",PrimitiveType.Capsule,LeverVisual,Vector3.zero,
                new Vector3(.035f,.031f,.035f),Cyan,waterMaterial).transform.localRotation=Quaternion.Euler(0,0,90);
            Part("Well lever hinge",PrimitiveType.Cylinder,leverPivot,Vector3.zero,
                new Vector3(.032f,.019f,.032f),Charcoal,darkMaterial).transform.localRotation=Quaternion.Euler(0,0,90);
            leverInstruction=Label("Lever instruction",controls,new Vector3(-.13f,.255f,.005f),"PINCH / LOWER + RETURN",.008f,Cream).transform;
            // WS-05 replacement contract: Button is the neutral contact point;
            // ButtonVisual depresses into the station (+local Z), never the anchor.
            Button=Anchor("Button",controls,new Vector3(-.13f,.17f,-.025f));
            Part("Button control housing",PrimitiveType.Cube,Button,new Vector3(0,-.035f,.055f),
                new Vector3(.09f,.14f,.06f),new Color(.19f,.32f,.39f),bodyMaterial);
            Part("Button surround",PrimitiveType.Cylinder,Button,new Vector3(0,0,.009f),
                new Vector3(.075f,.012f,.075f),Charcoal,darkMaterial).transform.localRotation=Quaternion.Euler(90,0,0);
            ButtonVisual=Anchor("ButtonVisual",Button,Vector3.zero);
            Part("Broad supply button",PrimitiveType.Cylinder,ButtonVisual,Vector3.zero,
                new Vector3(.054f,.008f,.054f),Cyan,waterMaterial).transform.localRotation=Quaternion.Euler(90,0,0);
            Label("Button icon",ButtonVisual,new Vector3(0,0,-.009f),"WATER",.006f,Cream);
            buttonInstruction=Label("Button instruction",controls,new Vector3(-.13f,.232f,-.022f),"PRESS TO FILL",.008f,Cream).transform;

            gaugeFrame = Part("Tank gauge frame", PrimitiveType.Cube, transform, new Vector3(.088f, .115f, -.014f),
                new Vector3(.028f, .105f, .015f), Charcoal, darkMaterial).transform;
            gaugeFill = Part("Tank gauge fill", PrimitiveType.Cube, transform, new Vector3(.088f, .115f, -.024f),
                new Vector3(.016f, .09f, .006f), Cyan, waterMaterial).transform;
            flowLamp = Part("Flow indicator", PrimitiveType.Sphere, transform, new Vector3(.088f, .182f, -.019f),
                Vector3.one * .018f, Charcoal, bodyMaterial).GetComponent<Renderer>();
            quantityLabel = Label("Tank status", transform, new Vector3(.088f, .047f, -.026f), "EMPTY", .008f, Cream);
            stateLabel = Label("Supply status", transform, new Vector3(0, .34f, .055f),
                creek ? "CREEK / OPTIONAL REFILL" : "HYDRANT / DOCK TOOL", .009f, Cream);
            instructionPlate=Part("Instruction plate", PrimitiveType.Cube, transform, new Vector3(0, .34f, .064f),
                new Vector3(.30f, .044f, .012f), Charcoal, darkMaterial).transform;
            tutorialLabel=Label("Refill step",transform,new Vector3(0,.34f,.055f),"1  BRING THE TOOL HERE\nMatch the two blue rings",.009f,Cream);
            tutorialPlate=Part("Refill step plate",PrimitiveType.Cube,transform,new Vector3(0,.34f,.064f),
                new Vector3(.36f,.065f,.012f),Charcoal,darkMaterial).transform;
            tutorialLabel.gameObject.SetActive(false);tutorialPlate.gameObject.SetActive(false);
            tutorialMarker=Part("Refill next action marker",PrimitiveType.Sphere,transform,Vector3.zero,Vector3.one*.018f,Mint,waterMaterial).transform;
            tutorialMarkerRenderer=tutorialMarker.GetComponent<Renderer>();tutorialMarker.gameObject.SetActive(false);
            tutorialLine=new GameObject("Refill alignment guide").AddComponent<LineRenderer>();
            tutorialLine.transform.SetParent(transform,false);tutorialLine.useWorldSpace=true;tutorialLine.positionCount=2;
            tutorialLine.startWidth=tutorialLine.endWidth=.002f;tutorialLine.sharedMaterial=waterMaterial;
            tutorialLine.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;tutorialLine.receiveShadows=false;tutorialLine.enabled=false;

            intakeArea = Anchor("Creek intake area", transform, Vector3.zero);
            intakeRest = Anchor("Intake rest", intakeArea, new Vector3(.135f, .055f, .075f));
            IntakeSocket = Anchor("IntakeSocket", intakeArea, new Vector3(.12f, .018f, -.075f));
            Part("Marked shallow water", PrimitiveType.Cube, intakeArea, new Vector3(.12f, .006f, -.055f),
                new Vector3(.09f, .012f, .12f), new Color(.08f, .36f, .47f), waterMaterial);
            Part("Intake socket marker", PrimitiveType.Cylinder, intakeArea, IntakeSocket.localPosition,
                new Vector3(.055f, .003f, .055f), Mint, waterMaterial);
            IntakeVisual = Anchor("Creek intake strainer", intakeArea, intakeRest.localPosition);
            Intake = Anchor("Intake", IntakeVisual, Vector3.zero);
            Part("Strainer body", PrimitiveType.Cylinder, IntakeVisual, Vector3.zero,
                new Vector3(.043f, .018f, .043f), Cyan, waterMaterial);
            for (int i = -1; i <= 1; i++)
                Part("Strainer slot " + i, PrimitiveType.Cube, IntakeVisual, new Vector3(i * .011f, .019f, 0),
                    new Vector3(.005f, .003f, .026f), Charcoal, darkMaterial);
            Label("Intake instruction", intakeArea, new Vector3(.125f, .086f, .035f), "INTAKE", .008f, Cream);
            hose = new GameObject("Source to pump hose").AddComponent<LineRenderer>();
            hose.transform.SetParent(intakeArea, false); hose.useWorldSpace = true;
            hose.positionCount = 4; hose.startWidth = hose.endWidth = .009f;
            hose.sharedMaterial = darkMaterial; hose.numCornerVertices = 2; hose.numCapVertices = 2;
            intakeArea.gameObject.SetActive(creek);
            if (!creek)
            {
                wellBody=Anchor("WellBody_WS04",transform,Vector3.zero);
                Part("Well plinth",PrimitiveType.Cylinder,wellBody,new Vector3(0,.025f,.075f),
                    new Vector3(.14f,.025f,.14f),new Color(.43f,.50f,.46f),bodyMaterial);
                Part("Well pump casing",PrimitiveType.Cylinder,wellBody,new Vector3(0,.115f,.075f),
                    new Vector3(.09f,.085f,.09f),new Color(.13f,.43f,.39f),bodyMaterial);
                Part("Well pump cap",PrimitiveType.Sphere,wellBody,new Vector3(0,.205f,.075f),
                    new Vector3(.10f,.04f,.10f),Cream,bodyMaterial);
                hydrantBody=Anchor("HydrantBody_WS01",transform,Vector3.zero);
                Part("Hydrant pressure body", PrimitiveType.Cylinder, hydrantBody, new Vector3(0, .11f, .075f),
                    new Vector3(.105f, .105f, .105f), new Color(.76f, .20f, .09f), bodyMaterial);
                Part("Hydrant cap", PrimitiveType.Sphere, hydrantBody, new Vector3(0, .205f, .075f),
                    new Vector3(.11f, .035f, .11f), Cream, bodyMaterial);
                buttonBody=Anchor("ButtonStationBody_WS05",transform,Vector3.zero);
                Part("Button station cabinet",PrimitiveType.Cube,buttonBody,new Vector3(0,.115f,.075f),
                    new Vector3(.13f,.21f,.10f),new Color(.19f,.32f,.39f),bodyMaterial);
                Part("Button station canopy",PrimitiveType.Cube,buttonBody,new Vector3(0,.225f,.075f),
                    new Vector3(.15f,.02f,.12f),Cream,bodyMaterial);
                Tube("Valve pipe", controls, new Vector3(-.035f, .17f, .075f), Valve.localPosition + Vector3.forward * .02f,
                    .014f, Charcoal, darkMaterial);
            }
            ApplyAuthoredBody();
            if(!creek)BuildOutletFeedback();
            SetTier(Tier);
            SetState(creek ? "DOCK / PLACE INTAKE / PUMP" : "DOCK / LOWER LEVER / RETURN", 0, false, false, 0, false);
        }

        void ApplyAuthoredBody()
        {
            var art=EmberdeepArtLibrary.Load();if(!art)return;
            var model=art.Create(IsCreek?"CreekPump":"Hydrant",IsCreek?controls:hydrantBody,
                IsCreek?new Vector3(-.13f,0,.075f):new Vector3(0,0,.075f));
            if(!model)return;
            // Blender supplies the shell; accepted gameplay anchors own moving controls.
            foreach(var r in model.GetComponentsInChildren<Renderer>())
                if(IsCreek?(r.name.Contains("Handle") || r.name.Contains("Pipe") || r.name.Contains("Strainer")):
                    (r.name.Contains("Coupling") || r.name.Contains("Valve")))r.gameObject.SetActive(false);
            if(!IsCreek)
            {
                hydrantBody.Find("Hydrant pressure body").gameObject.SetActive(false);
                hydrantBody.Find("Hydrant cap").gameObject.SetActive(false);
            }
        }

        public void SetTier(WaterStationTier tier)
        {
            bool changed=Tier!=tier;
            if(changed)StopOutlet();
            Tier=tier;
            if(!built)return;
            bool well=!IsCreek && tier==WaterStationTier.LeverWell;
            bool rotary=!IsCreek && tier==WaterStationTier.RotaryHydrant;
            bool button=!IsCreek && tier==WaterStationTier.PushButton;
            Lever.gameObject.SetActive(well);leverPivot.gameObject.SetActive(well);leverStand.gameObject.SetActive(well);leverInstruction.gameObject.SetActive(well);
            Valve.gameObject.SetActive(rotary);controls.Find("Valve instruction").gameObject.SetActive(rotary);
            var pipe=controls.Find("Valve pipe");if(pipe)pipe.gameObject.SetActive(rotary);
            Button.gameObject.SetActive(button);buttonInstruction.gameObject.SetActive(button);
            if(wellBody)wellBody.gameObject.SetActive(well);
            if(hydrantBody)hydrantBody.gameObject.SetActive(rotary);
            if(buttonBody)buttonBody.gameObject.SetActive(button);
            if(changed)SetControlAnimation(0,0,0);
        }

        public void SetControlAnimation(float leverTravelMetres,float valveDegrees,float buttonPress01)
        {
            if(!built)return;
            leverTravel=float.IsFinite(leverTravelMetres)?Mathf.Clamp(leverTravelMetres,0,.08f):0;
            valveAngle=float.IsFinite(valveDegrees)?valveDegrees:0;
            buttonPress=float.IsFinite(buttonPress01)?Mathf.Clamp01(buttonPress01):0;
            leverPivot.localRotation=Quaternion.Euler(-Mathf.Asin(leverTravel/LeverLength)*Mathf.Rad2Deg,0,0);
            valveVisual.localRotation=Quaternion.Euler(0,0,valveAngle);
            ButtonVisual.localPosition=Vector3.forward*(buttonPress*.01f);
        }

        // Cosmetic WS-04 outlet only: no WaterJet, collision, damage or tank API.
        // One reusable burst replaces the previous one, even if a caller repeats
        // an event. Root drives TickOutlet once per frame and stops it on pause.
        void BuildOutletFeedback()
        {
            outletArc=OutletLine("Empty outlet water arc",12,.005f);
            outletRipple=OutletLine("Empty outlet ripple",16,.0015f);outletRipple.loop=true;
            var shader=Resources.Load<Shader>("EmberdeepV2Particles");
            if(!shader)return; // The readable curve remains available without particles.
            outletDropMaterial=new Material(shader){name="Emberdeep small outlet droplets"};
            outletDropMaterial.SetFloat("_Style",3);
            var go=new GameObject("Empty outlet droplets");go.transform.SetParent(transform,false);
            outletDrops=go.AddComponent<ParticleSystem>();outletDrops.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=outletDrops.main;main.playOnAwake=false;main.loop=false;main.maxParticles=OutletParticleBudget;
            main.startLifetime=.26f;main.startSize=.005f;main.startSpeed=0;main.gravityModifier=1;
            main.simulationSpace=ParticleSystemSimulationSpace.World;main.scalingMode=ParticleSystemScalingMode.Shape;
            var emission=outletDrops.emission;emission.enabled=false;var shape=outletDrops.shape;shape.enabled=false;
            var renderer=outletDrops.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=outletDropMaterial;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.maxParticleSize=.025f;
        }
        LineRenderer OutletLine(string name,int points,float width)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);
            var line=go.AddComponent<LineRenderer>();line.useWorldSpace=true;line.positionCount=points;
            line.startWidth=width;line.endWidth=width*.5f;line.sharedMaterial=waterMaterial;
            line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;
            line.numCapVertices=2;line.enabled=false;Tint(line,Cyan);return line;
        }
        Vector3 OutletPoint(float time)=>Socket.position+Socket.forward*(.009f+.40f*time)
            +Vector3.up*(.08f*time)+Physics.gravity*(.5f*time*time);
        public void EmitOutletSplash()
        {
            if(!built || IsCreek || !isActiveAndEnabled || !outletArc)return;
            StopOutlet();outletRemaining=OutletBurstSeconds;
            if(outletDrops)
            {
                for(int i=0;i<6;i++)
                {
                    float side=(i-2.5f)*.012f;
                    outletDrops.Emit(new ParticleSystem.EmitParams{position=OutletPoint(.005f),
                        velocity=Socket.forward*(.32f+i*.018f)+Vector3.up*(.06f+i*.012f)+Socket.right*side,
                        startSize=.004f+(i%2)*.002f,startLifetime=.24f,startColor=Cyan,randomSeed=(uint)(i+1)},1);
                }
                // Explicit simulation keeps the effect bounded by the root's
                // accepted update/pause clock rather than an independent loop.
                outletDrops.Pause();
            }
            DrawOutlet();
        }
        public void TickOutlet(float dt)
        {
            if(!OutletActive || !float.IsFinite(dt) || dt<=0)return;
            outletRemaining=Mathf.Max(0,outletRemaining-dt);
            if(outletRemaining<=0){StopOutlet();return;}
            if(outletDrops)outletDrops.Simulate(Mathf.Min(dt,.1f),false,false,false);
            DrawOutlet();
        }
        void DrawOutlet()
        {
            float phase=1-outletRemaining/OutletBurstSeconds;
            outletArc.enabled=phase<.7f;
            outletArc.startWidth=.005f*Mathf.Clamp01((.7f-phase)*3);outletArc.endWidth=outletArc.startWidth*.5f;
            for(int i=0;i<outletArc.positionCount;i++)outletArc.SetPosition(i,OutletPoint(i/(float)(outletArc.positionCount-1)*.16f));
            outletRipple.enabled=phase>.35f;
            var end=OutletPoint(.16f);float radius=Mathf.Lerp(.008f,.028f,phase);
            for(int i=0;i<outletRipple.positionCount;i++)
            {
                float angle=i*Mathf.PI*2/outletRipple.positionCount;
                outletRipple.SetPosition(i,end+(transform.right*Mathf.Cos(angle)+transform.forward*Mathf.Sin(angle))*radius);
            }
        }
        public void StopOutlet()
        {
            outletRemaining=0;if(outletArc)outletArc.enabled=false;if(outletRipple)outletRipple.enabled=false;
            if(outletDrops)outletDrops.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        void OnDisable()=>StopOutlet();
        void OnDestroy()
        {
            if(outletDropMaterial){if(Application.isPlaying)Destroy(outletDropMaterial);else DestroyImmediate(outletDropMaterial);}
        }

        public void SetState(string status, float water01, bool flowing, bool docked, float pumpTravel, bool intakePlaced)
        {
            if (!built) return;
            water01 = float.IsNaN(water01) || float.IsInfinity(water01) ? 0 : Mathf.Clamp01(water01);
            lastTravel = float.IsNaN(pumpTravel) || float.IsInfinity(pumpTravel) ? 0 : Mathf.Clamp(pumpTravel, 0, .10f);
            stateLabel.text = status ?? "";
            quantityLabel.text = water01 >= .999f ? "FULL" : water01 <= .001f ? "EMPTY" : "WATER";
            float height = Mathf.Max(.002f, .09f * water01);
            gaugeFill.localScale = new Vector3(.016f, height, .006f);
            gaugeFill.localPosition = new Vector3(leftHanded ? -.088f : .088f, .07f + height * .5f, -.024f);
            Color ring = docked ? Mint : Cyan;
            foreach (var segment in socketSegments) Tint(segment, ring);
            Tint(flowLamp, flowing ? Cyan : water01 >= .999f ? Mint : Charcoal);
            PumpVisual.localPosition = Pump.localPosition + Vector3.back * lastTravel;
            if (intakePlaced != wasIntakePlaced)
            {
                IntakeVisual.position = intakePlaced ? IntakeSocket.position : intakeRest.position;
                IntakeVisual.rotation = intakePlaced ? IntakeSocket.rotation : intakeRest.rotation;
                wasIntakePlaced = intakePlaced;
            }
            if (IsCreek)
            {
                hose.SetPosition(0, IntakeVisual.position);
                hose.SetPosition(1, transform.TransformPoint(new Vector3(leftHanded ? -.13f : .13f, .035f, .12f)));
                hose.SetPosition(2, transform.TransformPoint(new Vector3(leftHanded ? .10f : -.10f, .035f, .12f)));
                hose.SetPosition(3, Pump.position + transform.forward * .035f);
            }
        }

        public void SetLeftHanded(bool left)
        {
            if (!built || leftHanded == left) return;
            // Move anchors rather than using negative scale (which would mirror text and normals).
            leftHanded = left;
            float controlSign = left ? 1 : -1;
            foreach (Transform child in controls)
            {
                var at = child.localPosition; at.x = Mathf.Abs(at.x) * controlSign;
                child.localPosition = at;
            }
            var pipe = controls.Find("Valve pipe");
            if (pipe) pipe.localRotation = Quaternion.FromToRotation(Vector3.up,
                new Vector3(controlSign * .095f, 0, -.07f));
            MirrorX(gaugeFrame, !left); MirrorX(gaugeFill, !left);
            MirrorX(quantityLabel.transform, !left); MirrorX(flowLamp.transform, !left);
            intakeArea.localPosition = new Vector3(left ? -.255f : 0, 0, 0);
            if (wasIntakePlaced) IntakeVisual.position = IntakeSocket.position;
            else IntakeVisual.position = intakeRest.position;
        }

        public void RestoreIntake()
        {
            if(!built)return;
            IntakeVisual.SetPositionAndRotation(intakeRest.position,intakeRest.rotation);
            wasIntakePlaced=false;
        }
        public void ShowTutorial(FireRun run,FireEquipmentView tool,bool held,Transform pickupGrip,bool available,bool carryingIntake=false,
            bool emptyOutlet=false,bool emptyReturnRequired=false)
        {
            if(!built)return;
            TutorialCue=EmberdeepRefillTutorial.Describe(run,IsCreek,held,available,carryingIntake,emptyOutlet,emptyReturnRequired);
            tutorialLabel.text=TutorialCue.Text;
            // One small instruction panel per source, not stacked status/tutorial
            // boards obscuring the forest. Desktop keeps its keyboard instructions.
            tutorialLabel.gameObject.SetActive(available);tutorialPlate.gameObject.SetActive(available);
            stateLabel.gameObject.SetActive(!available);instructionPlate.gameObject.SetActive(!available);
            Transform target=TutorialCue.Focus switch {
                EmberdeepRefillTutorial.Target.Pickup=>pickupGrip,
                EmberdeepRefillTutorial.Target.Socket=>Socket,
                EmberdeepRefillTutorial.Target.Valve=>Valve,
                EmberdeepRefillTutorial.Target.Lever=>LeverVisual,
                EmberdeepRefillTutorial.Target.Button=>ButtonVisual,
                EmberdeepRefillTutorial.Target.Intake=>Intake,
                EmberdeepRefillTutorial.Target.IntakeSocket=>IntakeSocket,
                EmberdeepRefillTutorial.Target.Pump=>PumpVisual,
                EmberdeepRefillTutorial.Target.Grip=>tool?tool.Grip:null,
                _=>null };
            bool visible=target && target.gameObject.activeInHierarchy;
            tutorialMarker.gameObject.SetActive(visible);
            if(visible)
            {
                tutorialMarker.position=target.position+Vector3.up*.045f;
                tutorialMarker.localScale=Vector3.one*(.016f+Mathf.Sin(Time.unscaledTime*4)*.003f);
                Tint(tutorialMarkerRenderer,Mint);
            }
            bool align=available && held && tool && !run.Supply.Locked && TutorialCue.Focus==EmberdeepRefillTutorial.Target.Socket
                && Vector3.Distance(tool.RefillPort.position,Socket.position)<.45f;
            tutorialLine.enabled=align;
            if(align){tutorialLine.SetPosition(0,tool.RefillPort.position);tutorialLine.SetPosition(1,Socket.position);}
            if(!run.Supply.Locked && run.Supply.Alignment01>0)
                for(int i=0;i<socketSegments.Length;i++)Tint(socketSegments[i],i<run.Supply.Alignment01*socketSegments.Length?Mint:Cyan);
        }
        public void FaceLabels(Quaternion rotation)
        {
            if(!built)return;
            stateLabel.transform.rotation=rotation;
            instructionPlate.rotation=rotation;instructionPlate.position=stateLabel.transform.position+rotation*Vector3.forward*.009f;
            quantityLabel.transform.rotation=rotation;
            tutorialLabel.transform.rotation=rotation;
            tutorialPlate.rotation=rotation;tutorialPlate.position=tutorialLabel.transform.position+rotation*Vector3.forward*.009f;
        }

        static void MirrorX(Transform target, bool right)
        {
            var at = target.localPosition; at.x = Mathf.Abs(at.x) * (right ? 1 : -1);
            target.localPosition = at;
        }

        static Transform Anchor(string name, Transform parent, Vector3 position)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(parent, false); anchor.localPosition = position;
            return anchor;
        }
        GameObject Part(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Color color, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name;
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
            var collider = go.GetComponent<Collider>(); collider.enabled = false;
            Destroy(collider);
            var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            Tint(renderer, color); return go;
        }
        void Tube(string name, Transform parent, Vector3 from, Vector3 to, float radius, Color color, Material material)
        {
            var part = Part(name, PrimitiveType.Cylinder, parent, (from + to) * .5f,
                new Vector3(radius * 2, Vector3.Distance(from, to) * .5f, radius * 2), color, material);
            part.transform.localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
        }
        void Tint(Renderer renderer, Color color)
        {
            properties.Clear(); properties.SetColor("_BaseColor", color); properties.SetColor("_Color", color);
            renderer.SetPropertyBlock(properties);
        }
        static TextMesh Label(string name, Transform parent, Vector3 position, string value, float size, Color color)
        {
            var at = Anchor(name, parent, position); var label = at.gameObject.AddComponent<TextMesh>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
            label.fontSize = 64; label.characterSize = size * 20 / 64;
            label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
            label.color = color; label.text = value; return label;
        }
    }
}

> **Scene editing:** Open **MetaHackthone > Design Mode > Level 1 - Gravity Garage** or **Level 2 - Waterfall Valley** to edit the saved course in Scene view. Save outside Play mode, then press Play to test the selected level. Both course roots live in DownhillSandbox. See [the current authoring guide](Docs/DesignModeGuide.md).

# MetaHackthone

## Emberdeep development specifications

**0.2.6 / Android code 9 — anchored contact and natural well grasp:** display hands settle onto calibrated grip frames with staged closure/squeeze/release. Starter lever uses whole-hand grasp (open then wrap fingers), not pinch. Lower 8 cm and return: without a tool, a brief outlet splash only; with a docked tool, 12 water units capped by capacity. Both hands and other-hand tool holding are supported; tracking loss requires reopening. 167 EditMode and 84 PlayMode passed. Quest/PICO APKs audited. User requested APK delivery only this round; 0.2.6 installation and worn-headset acceptance pending. [Current rules](Docs/Emberdeep/ContactGripIteration.json); evidence: `Logs/EmberdeepContactGrip`. Geometry/poses remain graybox; M2/M3 and durable saves remain proposed. Earlier entries below are historical.

**0.2.5 / Android code 8 — hand continuity and water station progression:** last app-valid hand pose remains visible during unavailable tracking while input is safely stopped. Contextual display poses distinguish grip, squeeze, trigger, lever, rotary valve, intake/pump and button press. Start with a free lever well (8 cm down and return adds 12); the existing shop upgrades it for 100 coins to a 45-degree rotary hydrant, then for 200 coins to a one-press automatic filling station. Upgrades give no water. Keyboard: H dock, hold/release R for well cycle, V for rotary/button, X disconnect. Both APKs built and audited; Quest installed. Current tests: 156 EditMode and 64 PlayMode passed. [Rules and implementation](Docs/Emberdeep/InteractionStages.json); evidence: `Logs/EmberdeepInteractionStages`. User confirmed system-menu hand hold/recovery and starter well refill on Quest 3. New station geometry is an interaction graybox; detailed grip fit, rotary/button upgrades and PICO performance require physical acceptance. M2/M3 and durable expedition saves remain proposed. Earlier version entries below are historical.

**0.2.4 / Android code 7 — live hand model fix:** excludes distance-grab preview hands, preserves SDK skeleton and visibility ownership, and masks the independent pinch indicator. Current tests: 142 EditMode and 43 PlayMode passed. See [fix and evidence mapping](Docs/Emberdeep/HandVisualFix.json). Both APKs built and audited; Quest 0.2.4/code 7 installed. Evidence: `Logs/EmberdeepHandFreeze`. The user confirmed normal hand-model following after New Game on Quest 3; other device checks remain pending. Previous version sections below are historical.

**0.2.2 / Android code 5 — hydrant launcher icon:** the standalone builder now uses a Blender render of the existing `EDV2_Hydrant` model for legacy, round and adaptive Android icons. `Tools/RenderEmberdeepHydrantIcon.py` reproduces it. Quest output: `Deliverables/Emberdeep/Emberdeep-0.2.2.apk`; the PICO build command targets `Emberdeep-0.2.2-Pico.apk` (not built by this installation task). Gameplay remains the 0.2.1 implementation below. Build, icon-resource and connected-device installation evidence is recorded under `Logs/EmberdeepInstall`; earlier version records below remain historical.

**0.2.1 / Android code 4 — index-ray UI and separate Art V2:** point with either index finger and pinch with that same hand after the button highlights cyan. The ray stays stable while the finger curls. Head pose places the menu but no longer selects its buttons. Camp, settings, quit, HUD and shop share this route; UI ownership cancels firing and charge. See `Docs/Emberdeep/MenuUsabilityFix.json` for the reported old interaction failure and version-scoped evidence. `ArtSource/EmberdeepV2` contains the generated concept, editable Blender source, preview and geometry manifest; `Tools/BuildEmberdeepArtV2.py` reproduces its nine models. `Emberdeep > Art V2 > Import Blender kit` rebuilds only the new prefabs/materials. The current APK outputs are `Emberdeep-0.2.1.apk` and `Emberdeep-0.2.1-Pico.apk`. Current verification is under `Logs/EmberdeepV2`; prior M1 logs remain historical.

**M1 manual-water baseline 0.2.0 / Android code 3 (retained in 0.2.1):** hydrant coupling and valve, creek intake and complete refill strokes, free reachable supply, explicit tank accounting, empty purchased tools and sector checkpoint retry replace passive regeneration. The stable menu placement below is retained; 0.2.1 replaces gaze selection with index rays and supplies separate Art V2 visuals. The original supply interaction anchors remain; safe search, animals and durable expedition saves remain M2 work. See [requirement and evidence mapping](Docs/Emberdeep/ImplementationStatus.json). Desktop: **H** docks at the hydrant, **V** operates the valve, **X** disconnects; from sector two, **C** docks at the creek, **I** places/removes its intake, and hold/release **R** completes one refill stroke. In-headset, align the cyan coupling, turn or tap the valve, or move the creek intake and pull/return its separate pump. Release and make a fresh grip pinch to disconnect, then open before firing.

With an idle Unity Editor open, `powershell -ExecutionPolicy Bypass -File Tools/Test-EmberdeepM1.ps1` runs the standalone rule and interaction regressions and restores the original scene setup/list. For reproducible APK builds with the Editor closed, run `unity build . --target Android --execute-method FireRescue.Editor.FireRescueBuilder.BuildShowcaseQuest --allow-dirty-build` and then the same command with `BuildShowcasePico`. Outputs are `Deliverables/Emberdeep/Emberdeep-0.2.1.apk` and `Emberdeep-0.2.1-Pico.apk`; the builder preserves authored scenes and restores temporary app/icon/scene settings. Device acceptance and Word visual verification are recorded separately; automated passes do not establish hand-tracking comfort or performance.

The standalone firefighting game is **Emberdeep**. The detailed English specifications distinguish inspected v0.1 behavior from proposed v0.2 work:

The historical **0.1.1 menu placement repair** keeps the menu at eye level in front after stable tracking. The user's subsequent unusable-button report prompted the 0.2.1 index-ray correction above. New Game enters the fireground; Settings provides sound and recenter; Quit requires confirmation. See [implemented changes and QA boundaries](Docs/Emberdeep/MenuUsabilityFix.json). Front placement is the historical positive device confirmation; new input and art still require headset acceptance.

- [Game design and development specification](Deliverables/Emberdeep/Emberdeep-Design-and-Development.docx): physical water collection, fire and equipment rules, safe search, animal rescue, economy, architecture, saves, milestones and acceptance tests.
- [Art V2 delivery](Deliverables/Emberdeep/Emberdeep-Art-V2-Delivery.docx): separate Blender assets, Unity rendering integration, index-ray correction and version-scoped evidence.
- [Art production requirements](Deliverables/Emberdeep/Emberdeep-Art-Production-Requirements.docx): asset inventory, dimensions, anchors, animation, effects, shader properties, import settings and device acceptance.

[DocumentManifest.json](Docs/Emberdeep/DocumentManifest.json) lists the authoritative JSON sources in reading order. Future developers and agents should read these before changing Emberdeep. Edit those sources, retain requirement IDs, record accepted decisions and regenerate with `python Tools/BuildEmberdeepDesignDocs.py` using Python with `python-docx`. Render and visually inspect every page before delivering updated Word documents. Proposed requirements are not evidence that an existing APK contains those features. The earlier one-page showcase remains a presentation summary.

A Quest 3/3S mixed-reality workshop: build a container with modular parts, drop physics balls inside, or reuse the pieces to build and drive a car.

## Designer quick start (no headset)

Choose **MetaHackthone > Developer Tools > Design > Play Design Mode Menu**, then select **Car Workshop**, **Sandbox / Workbench Build**. Each opens with a ready-built test car. Click **Build / Drive** in Workshop or **READY / TEST** in Sandbox.

To make a blockout, stop Play mode, choose **Open Car Workshop** or **Open Sandbox** from the same Design Mode menu, edit the children of **Editable building layout**, and use its Inspector **Test in Design Mode** buttons. Save scene edits outside Play mode with **Ctrl+S**.

See the [designer walkthrough](Docs/DesignModeGuide.md) for screenshots, exact steps, controls, reset/retry behavior, and troubleshooting. [Scene authoring details](Docs/SceneEditing.md) describe the editable objects and regions.

## Local desktop setup (Windows)

Unity **6000.3.21f1** is installed at `G:\UnityProject\Unity\6000.3.21f1\Editor\Unity.exe`; Unity Hub is at `G:\UnityProject\Unity\Hub\Unity Hub.exe`.

1. Activate your Unity license in Unity Hub on first use.
2. Double-click **Open Desktop.cmd** in this project. It starts the local Unity MCP server and opens the workshop with the Windows desktop target.
3. Choose **MetaHackthone > Play Desktop Workshop**, then click the Game view. Use **Default Car** to begin driving with W/S, A/D and Space.

From PowerShell, `powershell -ExecutionPolicy Bypass -File .\Tools\Open-Desktop.ps1 -Play` opens directly into Play mode. No device build is required. Android modules are only needed for a later Quest build.

The minimal Android SDK platform-tools are at `G:\UnityProject\Unity\AndroidSDK`, configured in the editor to prevent Meta's SDK-not-found popup during desktop Play mode. This is not a full Android build toolchain.

Unity MCP uses the existing pinned Unity package and matching **10.3.0** Python server. The launcher connects the editor to `http://127.0.0.1:8080`; Codex's `UnityMCP` connection uses `http://127.0.0.1:8080/mcp`. Restart Codex after adding the connection to load its tools. If needed, use **MetaHackthone > Connect Unity MCP**. Server and editor logs are in `Logs/`.

## Open and run

**Shadow Workshop / 影子工坊:** choose **Shadow Workshop** in the desktop toolbar or Quest workshop panel. It opens on **A Life**, the first everyday-object level; **< / >** cycle through all thirteen shadow levels: the everyday-object levels (A Life, Italia, Nippon), the flashlight levels (Encore, Bright Idea, Good Boy: turn a pile of objects on a turntable with one hand and aim a flashlight with the other until its shadow becomes a picture), the second flashlight iteration (Birthday, Bon Voyage, Good Morning, Home: fixed lamps in a room corner; the pile sits in an orb you roll by its rings and carry by its hub; Home must cast two pictures on two walls at once), Three Wishes, Moon Courier and the boat tutorial. **MetaHackthone > Shadow Workshop > Play Everyday Objects** opens A Life directly, **Play Torch Levels** opens Encore and **Play Torch Levels 2** opens Birthday. Designers build new orb levels in the **Shadow Gym** (**MetaHackthone > Shadow Workshop > Gym > Open gym**): arrange any meshes and see the wall shadows live without Play mode, then check, export and play the level from the gym's inspector. The games reuse the shared Meta player, native menu interactions, navigation and snapping math. **RETURN TO WORKSHOP** restores your construction. See [migration, controls and handoff notes](Docs/ShadowWorkshop.md).

**Downhill Sandbox:** choose **Workbench Build** from MainMenu. Assemble a passive vehicle and press **START** for the transfer, countdown, and gravity test. Complete Gravity Garage to progress to Teleport Pipes. The START arch rotates with the build pad as a direction cue; the car keeps its fixed course launch direction. See [Sandbox build modes](Docs/SandboxBuildModes.md).

**CloudGarden:** choose **CloudGarden** in the workshop menu or desktop toolbar to enter the garden demo using the same MetaHackathon player. **Return to Workshop** restores your unsaved construction. For direct entry, use **MetaHackthone > CloudGarden > Play demo**. See [CloudGarden integration and controls](Docs/CloudGarden.md).

- Unity **6000.3.21f1**, Universal Render Pipeline. Android Build Support (SDK, NDK, OpenJDK) is required only for device builds.
- Meta XR All-in-One **207.0.0**, Unity OpenXR **1.17.0**. The SDK's component versions are resolved by its official dependency manifest; Audio and Voice intentionally have different version numbers.
- Open `Assets/MetaHackthone/Scenes/Workshop.unity` and press Play for mouse/keyboard preview.
- If regenerating the project, use **MetaHackthone > Generate workshop and configure Quest**. This recreates the workshop scene; preserve any scene edits first.
- Switch the active build target to **Android**, wait for importing/compilation, then use **MetaHackthone > Build Quest APK**. The build guard rejects mismatched active targets before URP collects shader settings. Both headset workflows use the existing Android Mobile rendering profile.
- For a clean Quest build, close the Editor and run `powershell -ExecutionPolicy Bypass -File .\Tools\Unity.ps1 -Task BuildClean`. This launches Unity with Android selected, rebuilds the player, and verifies that the Mobile profile was used and Lit/Unlit drawing shader variants survived stripping. Output: `Builds/MetaHackthone.apk`; log: `Logs/BuildClean.log`. Normal incremental builds remain available through `-Task Build`.
- A headset with developer mode and authorized USB debugging is required for installation. Install with `adb install -r Builds/MetaHackthone.apk` and launch `com.metahackathon2026.metahackthone`.
- With one Quest attached by USB, `./Tools/Deploy.ps1 -Launch` installs and opens the app. Use `-Device SERIAL` when multiple headsets are connected.

## Container building

Near grabbing accepts a fully closed fist or a thumb/index pinch. A fist grab stays held until the fist opens; a pinch grab releases immediately when the tracked fingertips separate, even if the other fingers curl. Right-hand release still attaches valid nearby parts in Narrow Gap; left-hand release leaves removed parts detached.

In Narrow Gap Launch mode, hold index and thumb extended (other fingers curled) for one second with either hand to aim. Point the index, then fold the thumb to launch. The pad and load rotate together, with a continuous fading trajectory. Other poses or tracking loss cancel; Retry restores the setup from before aiming. Launch/Space remains available. See [Narrow Gap](Docs/NarrowGap.md).

Launch and **Reset kit** provide the existing base, eight small cubes, four cuboids, and two square pyramids. Cube sides and pyramid width/depth/height are the base length divided by six (8.728 cm). Cuboids are 1×2 blocks measuring 17.456 × 8.728 × 8.728 cm. Existing car geometry remains available through **Default Car**.

All levels with part-to-part building share the default Workshop connection rule. A small cube has **6 points (one centered on each face)**. A 1×2 Cuboid has **10 points (two on each long face and one on each end)**, with each point centered in a cube-sized section. New rectangular blocks, beams, chassis and Downhill parts use this same centered layout, without corner or extra axle points. A pyramid has five points, one on each face. Wheel and launcher mounts retain their specialized sockets. Each connection point accepts only one connection; detaching frees it for reuse. Saved constructions retain their versioned dimensions, point layouts and joints. Cubes and Cuboids already use layout version 2; the other updated building parts use version 3. The current Sandbox building core uses the six-face cube layout; older saved cores retain versioned compatibility. The workbench and simulation use the same versioned socket identities when transferring a construction; see [Sandbox build modes](Docs/SandboxBuildModes.md).

Use **+ Sphere** to spawn a grabbable 4 cm physics ball, then drop it into your construction. Gravity and collisions run in Build mode, without wheels or Drive mode. Desktop spawn buttons are in the toolbar; Quest uses the workshop panel. The green ghost previews flush face alignment and 45-degree twist snapping. Snapping favors faces you have already lined up, reducing unexpected turns toward nearby side-facing points. Desktop rotation keys also rotate the held object in 45-degree steps before attachment.

## Hands

Start in Build mode. Open your hand, then fully close all five fingers or pinch thumb and index together to pick up a nearby part. Pinch contact uses one hand-scale-normalized 15 mm boundary for grabbing and releasing, with no extra release margin or timer. A fist grab stays held until the fist opens. Each grab keeps the gesture that started it; changing to another gesture cannot prolong that hold. Release happens before the opening-hand pose moves the part. Missing tracking data preserves the hold for up to 0.35 seconds; valid finger separation releases immediately. This behavior applies to Workshop, Narrow Gap, and Downhill Sandbox. Keep the pinch while moving and rotating, then separate your fingertips to release when the green connection preview appears. Only compatible, unoccupied connection points attach. Faces meet flush and the twist snaps to the nearest multiple of 45 degrees; the green ghost shows the resulting placement. Intersecting placements are rejected. Selecting a part and pressing **Detach** removes its external connections while preserving wheel suspension and axles. You can hold the chassis in one hand and release a new part onto it with the other: the completed group stays in the supporting hand until you release it.

On launch, you stand **inside the platform footprint**, centred on your tracked position. The menu sits at the far edge facing inward toward you, with the chassis and parts in front of you. The driving surface is locked **50 cm above the real floor**, using Quest's initial Stage floor calibration. Its position and level orientation stay fixed while you build and drive. Use **Move arena** explicitly to choose a different horizontal location; holding, attaching, and nearby part handling block menu/placement selection.

Movement uses your physical room position. The comprehensive rig's VR locomotion, player capsule and ground snapping are disabled so the virtual platform cannot move your viewpoint. Parts can rest on the platform, but assembly connections are restricted to other parts.

Connect four wheel modules to the chassis. Add the orange launcher on top. Release all parts, then press **Drive / Build**. The bright stripe on the chassis indicates forward.

The current kit uses Hustle's part proportions at a hand-sized 20% scale: a 52 cm chassis and 23 cm wheels. See [source dimensions and physics mapping](Docs/HustlePhysicsPort.md). Use **Reset kit** or **Default Car** for the new parts. New constructions use `construction-v3.json`; old `car-v1.json` and `car-v2.json` saves are preserved but must be rebuilt for the exclusive grid connections.

| Pose | Command |
|---|---|
| Right index | Forward |
| Right index + middle | Reverse |
| Left index | Left |
| Left index + middle | Right |
| Left index + middle + thumb | Fire repeatedly; steering neutral |

Fold fingers not listed in the pose. Hold a pose for 150 ms to activate it. Fingers may point in any direction. Neutral right hand brakes. Tracking loss clears that hand's command immediately; focus loss returns to Build mode. Right-hand throttle remains available while the left hand fires. A detached launcher cannot fire through its former chassis.

Aim at a panel button until Meta's pointer appears and the button highlights, then pinch your thumb and index finger to select it. You can also directly touch a button with your index finger. Buttons use the comprehensive rig's SDK ray and poke interactors, with hover and pressed feedback. **Move arena** lets you point at the tracking-space floor and pinch to reposition the arena; the preview and driving surface remain 50 cm above that floor. This prototype uses no room scan or persistent room anchors.

Open your thumb and index before the first pinch after tracking resumes. The pointer's own tracked hand supplies both menu selection and placement; pinch detection also reads fingertip distance when the provider pinch flag is absent. For **Move arena**, release the opening pinch, aim down until the green arena outline appears, then pinch again to place it. Select **Move arena** again to cancel. The existing arena stays where it is until you confirm.

**Save construction** writes the selected connected construction to the local construction slot. Switch to Build first. **Load construction** replaces the selected construction only after successfully recreating the save with fresh IDs and zero velocity. **Reset kit** clears constructions and supplies fresh parts. The panel provides extra cubes, cuboids, pyramids, physics spheres, beams, blocks, wheels, and launchers, up to 50 objects total. Loose test spheres are temporary and are not saved.

## Desktop preview

Choose **MetaHackthone > Play Desktop Workshop**, then click the **Game** view. You can also open `Workshop.unity` and press Play. Editor Play mode always uses mouse/keyboard and the desktop camera, including when the active build target is Android.

Click **Default Car - replaces kit** in the desktop toolbar to replace the current unsaved parts with a complete car (chassis, four working wheel/suspension modules, and launcher) and immediately enter Drive mode. The VR workshop menu also has a **Default Car** button. This does not overwrite your saved car. **Reset Parts** returns to the loose fifteen-part container kit for manual assembly. Release held parts and let any attachment finish before using the toolbar actions.

| Input | Action |
|---|---|
| Left- or right-drag / release | Grab a part or assembly; release the same button near the connection preview to attach |
| Wheel while holding either button | Move the held object away from the camera (wheel up) or closer (wheel down) |
| W / S while holding | Pitch +45 / -45 degrees around world X |
| A / D while holding | Yaw -45 / +45 degrees around world Y |
| Q / E while holding | Roll -45 / +45 degrees around world Z |
| Escape | Release without attachment; cancel arena placement |
| Delete | Detach selected part |
| Right-drag empty space / middle-drag | Orbit / pan the camera |
| Wheel when not holding | Zoom |
| F | Frame selected assembly (or the platform when nothing is selected) |
| Overview button | Restore the full-platform view |
| Tab | Switch Build / Drive |
| W / S, A / D, Space | Forward / reverse, left / right, fire |

Camera movement is blocked while holding a part. Clicking the desktop toolbar does not select objects behind it. Leaving the Game view clears driving commands and releases held parts without attachment.

Use **Save** in Build mode to keep the selected construction between Editor sessions, then **Load** to restore it. Editor and Quest saves are device-local. Desktop testing exercises the same assembly and vehicle physics as Quest; it does not certify headset hand tracking.

Connection preview performance and profiling instructions are documented in [Attachment performance](Docs/AttachmentPerformance.md).

## Validation

### Emberdeep / 火线深处 standalone showcase

The firefighting app has its own identity (`com.metahackathon2026.emberdeep`, M1 and Art V2 candidate `0.2.1`, Android code `4`). Its build contains only `EmberdeepCamp.unity` and `EmberdeepFireground.unity`: a dedicated camp and five fire incidents. The camp resumes the current expedition, including tools, water, cash, fire state and stars. Progress lasts for the running app session; **New Game** resets it.

Use the existing scene assets. Scene generation is a separate authoring operation and must not overwrite shared scene edits. Build with **Emberdeep > Build standalone Quest showcase**, or:

```powershell
unity build . --target Android --execute-method FireRescue.Editor.FireRescueBuilder.BuildShowcaseQuest --allow-dirty-build
```

The builder temporarily applies the independent app identity and a strict two-scene whitelist, then restores project settings. It writes `Deliverables/Emberdeep/Emberdeep-0.2.1.apk` and a build-report manifest containing the scene audit and APK SHA256. Shadow, car assembly and the shared game-selection menu are excluded from this APK. Shared hand tracking and the player rig are retained; Emberdeep buttons use index rays and same-hand fresh-pinch confirmation with shared gesture filtering and exclusive input ownership.

For PICO 4, use **Emberdeep > Build standalone PICO showcase** or `unity build . --target Android --execute-method FireRescue.Editor.FireRescueBuilder.BuildShowcasePico --allow-dirty-build`. It writes `Deliverables/Emberdeep/Emberdeep-0.2.1-Pico.apk`. This output uses the existing portable UnityXR player in both fire scenes, PICO OpenXR hand tracking, and Android API 29 as its minimum. The build restores prior settings afterward and leaves the Quest APK intact. The installed PICO OpenXR SDK requires PICO OS 5.13.0 or later; use a later system update because the SDK notes a pinch interaction issue on exactly 5.13.0. Headset interaction and performance still require device testing. SDK source and release notes: https://github.com/Pico-Developer/PICO-Unity-OpenXR-SDK.

Authoritative design and art sources are indexed by `Docs/Emberdeep/DocumentManifest.json`; the detailed English Word documents are generated from those sources. The old one-page showcase is historical, not the development specification. M1 manual water is implemented as a candidate; safe search, equipment discoveries, animals and durable saves remain pending M2/M3 work.

For isolated PlayMode QA, call `FireRescue.Editor.FireRescueBuilder.PrepareShowcaseTests` first, then run the `FireRescue.Tests.EmberdeepShowcaseTests` filter. This preparation deliberately installs the two-scene build list; back up and restore `ProjectSettings/EditorBuildSettings.asset` around the test run. The normal integrated tests require the original multi-game build list.

```
./Tools/Unity.ps1 -Task EditTests
./Tools/Unity.ps1 -Task PlayTests
./Tools/Unity.ps1 -Task Build
```

Logs and NUnit XML are written under `Logs/`. See `Docs/PortNotes.md` for source mapping, tuning choices, and device acceptance steps. Build artifacts, caches, and logs are excluded from source control.



The car now uses meshes exported from Hustle Level2 `CarDemoSet/CarParts`. The original FBX meshes and textures are in `Assets/MetaHackthone/Art/HustleCar`, including a source manifest. Default Car, loose parts and loaded constructions use the new visuals with the existing physics bodies. The camera, antenna, case and propeller meshes are also imported for future parts. Rebuild materials and visual assets with **MetaHackthone > Import Hustle car visuals**; **Rebuild part prefabs** retains the visual library. Unreal ARM textures are converted to Unity metallic/smoothness and occlusion maps; normal-map green channels are flipped for Unity. Generic source materials use a neutral metal material. Visual meshes are centered and fitted to the current part dimensions; they do not replace collision geometry.

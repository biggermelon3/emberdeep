# Sandbox building — implementation and verification

## 2026-11-02: start and goal arches

Added low-poly timber-post arches based on `E:/Unity/Image/Start.png` and
`End.png`: a blue START banner and red GOAL banner, with gold caps and white
lettering on both sides. Each prefab is one mesh with four material slots and
a baked font atlas; it has no collider, grab interaction, or live TextMesh.
Assets are under `Assets/MetaHackthone/Resources/CourseArches`, with an editor
bake command under MetaHackthone / Scene editing / Bake start and goal arches.

Both Gravity Garage and Teleport Pipes place arches across their start and
goal sections. The first course shares its goal coordinate with the existing
success condition; stable landing is still required. Course labels can be
hidden without hiding the arch lettering.

`Core direction grid / Start arch - build direction` is saved in front of the
centered pad, aligned with its FRONT arrow. It rotates with the pad and wheel,
preserves editor placement, hides during simulation, and returns in build mode.
The course arches and the simulation car's launch direction remain fixed while
the player rotates the construction for editing. No Quest build or install has
been performed for this change.

Verification: 92/92 PlayMode tests passed, including wheel rotation in normal
and Design Mode, unchanged launch pose, both course markers, absence of arch
colliders, build/test visibility, and repeated physical course completion.
Results and visual captures are in `Logs/CourseArches`.

## 2026-11-01: full-fist and immediate pinch grabbing

Object grabs now start with either a closed five-finger fist or a thumb/index
pinch. The gesture that starts the grab owns its lifetime: a fist remains held
with separated fingertips until the fist opens; a pinch releases on the first
tracked sample beyond contact, even if the other fingers curl into a fist.
Release keeps the last held object pose instead of applying the opening pose.
Open the active gestures before starting a fresh grab or switching grab types.

Pinch contact uses a 15 mm hand-scale-normalized tip distance, with the same
boundary for grabbing and releasing, no extra release margin, and no timer.
This is a tracked contact approximation, not a physical zero-gap measurement.
Full-fist input requires all four finger curls and the folded-thumb score to
reach 0.9. The adapter reads all five fingers; partial curls do not start grabs.
Existing brief tracking-loss recovery remains separate from tracked separation.
The rotation wheel uses pinch input and the same contact boundary.

Verification: 326/326 EditMode and 128/128 PlayMode tests passed, including
gesture boundaries, synthetic tracked joints at three hand sizes, tracking
recovery, two-hand behavior, wheel rotation, and course progression. Evidence
is saved in `Logs/GrabPolish`.

Deployment: Android development build succeeded in 89.6 seconds with 0 errors
and 2 warnings. Installed `Builds/MetaHackthone-SandboxCampaign-Quest.apk` on
Quest 3 `2G0YC5ZG5V04ZW`, preserving app data. The headset reports update time
2026-11-01 23:44:46. Evidence: `Logs/GrabPolish/QuestInstall.json`.
Physical threshold validation is pending.


## 2026-11-01: loose-group grabbing and two-hand separation

In the designer sandbox (normal play and Design Mode), grabbing any member of
a group disconnected from the core moves the entire connected component.
A second hand grabbing a different member removes only that part's incident
connections and holds that part alone. The first hand keeps the connected
component containing its originally selected part; other disconnected components
float at their current poses. Picking the same selected part hands over the
whole group. Internal multi-body joints remain intact.

AssemblyWorld owns the change and publishes events after final ownership is
established. Both held groups rebase at their resolved poses, with stale aim
previews cleared. Core-connected branch detachment and other modes retain their
existing behavior. Saved hierarchy data and socket layouts are unchanged.
Verification: 102/102 PlayMode tests and 326/326 EditMode tests passed. The
initial run was interrupted by an editor reload/test-runner error; the complete
rerun passed. Evidence is saved in `Logs/LooseGroupGrab`. Quest deployment and
physical two-hand validation remain separate steps.

Deployment: Android development build succeeded in 127.3 seconds with 0 errors
and 4 warnings. Installed `Builds/MetaHackthone-SandboxCampaign-Quest.apk`
(196,502,682 bytes) on Quest 3 `2G0YC5ZG5V04ZW`, preserving app data.
The headset reports update time 2026-11-01 22:56:59. Evidence:
`Logs/LooseGroupGrab/QuestInstall.json`. Physical two-hand playtesting is pending.

## 2026-11-01: compact Retry and Back controls

RETRY and BACK TO BUILD now use the same one-third tile scale as START, RESET,
and QUIT. Build and test panels use the same compact spacing, padding, and border
thickness, keeping the panel footprint consistent across Start/Retry/Back.
Saved scene transforms were updated. Live editor verification confirmed equal
world sizes, successful Retry and Back actions, and readable labels; capture:
`Logs/CompactTestUI/RetryBack.png`. No Quest rebuild/install was performed.

## 2026-11-01: combined centered grid pad

The saved DownhillSandbox scene now renders the pad, grid lines, circular border,
arrow, and FRONT lettering as one persistent mesh with four material slots.
The lettering uses a baked font atlas, so no live TextMesh remains on the pad.
Core direction grid has two children: Centered grid pad and Build rotation wheel.
Authored transforms and wheel interaction are preserved. The circular-pad upgrade
recognizes the combined mesh instead of replacing it. An editor bake command is
available under MetaHackthone / Scene editing / Combine centered grid pad.

Validation: 5 focused EditMode tests and 29 PlayMode tests passed. The normal-play
capture and results are saved under `Logs/CombinedGrid`. No Quest rebuild or install
was performed for this change.

## 2026-11-01: VR rotation wheel restored

The designer builder now shows the build rotation wheel and accepts near-hand
pinches with either hand in normal play and Design Mode. Turning rotates the
core and attached construction; loose parts remain stationary. The wheel remains
under Core direction grid at its authored local pose. Its parent follows the
core once, while hand input uses a fixed center captured at pinch start to
prevent feedback from the offset group moving. Course launch orientation remains
unchanged. The wheel hides during simulation and returns in build mode.

The active hand is reserved from part grabs and ray-menu selection. Existing
pinch thresholds and tracking-loss grace apply; Start, Reset, and attachments
stay blocked while turning. Controller input was not added.

Verification: 89/89 focused PlayMode tests and 326/326 EditMode tests passed.
The first PlayMode job timed out during initialization; its retry completed.
A normal-play editor capture confirms the wheel is visible beneath the pad.
Evidence: `Logs/VRRotationWheel`. Quest build/install and physical-hand
acceptance testing have not been performed for this change.

## 2026-11-01: Workbench only

Removed the alternate Build on Sandbox mode completely. MainMenu now has one
centered WORKBENCH BUILD entry; the desktop Design Mode menu uses the same
entry. Runtime and editor launch paths no longer accept a build-layout choice.
Removed the alternate placement/scaling path, session selection, mode enum,
unused settings, and serialized mode-specific fields. Workbench construction,
Design Mode tools, and the Gravity Garage → Teleport Pipes campaign remain.

Tests now exercise the Workbench flow, including menu resume, detached branches,
reset, test/retry/return, and campaign construction carryover. The saved-scene
check verifies that the pad and rotation wheel share Core direction grid while
preserving their authored offsets. The fixed-panel test checks stationary panel
anchors while allowing action buttons and backing to resize for build/test.

Verification: **326/326 EditMode tests** passed. The focused PlayMode run
passed 88/89; its sole failure was a wheel-size fixture that had relied on the
removed layout switch to respawn parts. After changing that fixture to set the
authored size before spawning, it passed with all seven scene runtime tests
(**96 distinct PlayMode tests verified** across the two runs). Final menu visual
inspection confirms one centered Workbench card. Evidence is in
`Logs/WorkbenchOnly`. This change has not been rebuilt or installed on Quest.

Older sections below are historical records. Their references to two build
layouts describe the removed implementation.

## 2026-11-01: two-level campaign and Design Mode controls

Normal sandbox play starts with **Level 1: Gravity Garage**, the original
single-track course without teleport pipes. A successful landing unlocks
**NEXT LEVEL** in the world UI and desktop toolbar. Advancing opens
**Level 2: Teleport Pipes** in build mode, using the existing calibrated
2.5D course. It retains the complete kit, connections, loose pieces, selected
part, reset poses, and Workbench / On Sandbox layout. Retry and Back to Build
stay on the current level. Finishing Level 2 displays **ALL LEVELS COMPLETE**.

LEFT 90, RIGHT 90, EXAMPLE, and desktop Q/E rotation are available only in
Design Mode. Direct hand rotation remains available during normal building.
Campaign progress survives visits to MainMenu in the same application session;
it resets when the application restarts. Design Mode previews and standalone
course fixtures do not participate in campaign progress. The course editor's
2.5D asset remains the Level 2 teleport course.

Implementation: `SandboxCampaignSession` stores session-only progression;
`DownhillCampaign` handles course switching and construction carryover.
`DownhillEntry` opts normal entry into the campaign using a runtime settings
clone, preserving the authored settings asset.

Verification: **36/36 focused PlayMode tests** and **326/326 EditMode tests**
passed. Coverage includes hidden and guarded design controls, premature/failed
advance rejection, carryover in both layouts, retry/reset, menu resume,
Design Mode isolation, desktop interaction, and the legacy teleport fixture.
`LegalDesignerVehicleCompletesBothCourses` assembles crosswise axle beams using
legal shared sockets and completes both courses through actual physics with
the same vehicle. The long design example stalls on Level 1; it is a building
example, not a guaranteed winning construction. Course physics were not tuned.

Evidence is in `Logs/SandboxCampaign`: test summaries and three world-UI
captures. The completion UI capture uses an injected success state for visual
inspection; the physics test separately verifies genuine completion.
The capture tool emitted Unity recursive PlayerLoop messages while rendering;
these were separate from the passing test runs. Quest hardware playtesting of
this campaign change has not been performed.

Android development build succeeded with 0 errors and 4 warnings in 84.7 seconds:
Build-log warnings include the existing PICO platform app ID configuration and
the PICO platform loader's lack of Android 15 16 KB alignment.
`Builds/MetaHackthone-SandboxCampaign-Quest.apk` (196,443,076 bytes).
SHA-256: `4E3AB5284F7A79D6BC057C8D287D578D359E493BD79BD13779F228FB38890B77`.
Rebuilt successfully (0 errors, 2 warnings) and installed on Quest 3
`2G0YC5ZG5V04ZW` on 2026-11-01 at 19:57:46 local time. The package
`com.metahackathon2026.metahackthone` was updated in place, preserving app data;
the device's package information confirmed the new installation and update time.
Unity was left stopped on the
clean `DownhillSandbox` scene. Existing unrelated pending changes were retained.

## 2026-11-01: designer Drag & Snap builder

The current sandbox replaces the automatic Garage vehicle with the designer's
`SandBoxDesign/Gravity_Workshop_Drag_Builder_v5.html` interaction model. Both
**WORKBENCH BUILD** and **BUILD ON SANDBOX** are available from MainMenu.
The two-level campaign above supersedes the original single-course flow;
Start/Retry/Back remains available.
Older reports below describe superseded behavior.

### Building

- Nine pieces: a fixed blue core, long and short blocks, cube, triangular prism,
  two large wheels, and two small wheels. New geometry uses grid version 10.
- Shared rectangular geometry: core 1x1x1 (6 sockets), short 1x1x2 (10), long
  1x1x3 (14), and cube 2x2x2 (24). Triangle sockets follow its actual surfaces.
  Socket definitions are generated before scaling. Workbench-contact sockets
  are unavailable while their face touches the surface.
- The core and structures accept structures or wheels, including direct
  wheel-to-core mounts. Wheels have one inward axle and never act as attachment
  targets.
- Build loose subassemblies before mounting them to the core. Grabbing a piece
  detaches that piece and its downstream branch, retaining internal joints.
- Green previews indicate a valid placement; red means the intended socket's
  placement overlaps another piece, including loose pieces. Release checks
  the same placement again. Connected sockets receive green lock rings.
- Direct pinch moves in 3D. Point-and-pinch moves distant pieces on a horizontal
  plane at the initial grab height; socket targeting resolves attachment height.
  Nearby pieces take priority, and a drag retains its input mode until release.
- In Design Mode, LEFT 90 / RIGHT 90 rotate the selected branch. A free hand may press these
  during a hold; other menu actions remain blocked. Tracking loss cancels the
  hold without attachment after the existing 0.35-second grace period.
- Desktop: left-drag to build; Design Mode Q/E for 90-degree yaw; right-drag to inspect
  within +/-90 degrees; scroll to zoom. Camera inspection does not change launch
  heading. RESET returns all nine pieces; Design Mode EXAMPLE builds a legal sample vehicle.
- Direct-on-course construction uses simulation size. Changing layouts retains
  part IDs, internal connections, and loose branches while resizing geometry,
  sockets, and joint anchors together. Back to Build restores the editable kit.

### Implementation and compatibility

`DownhillSettings.DesignerBuilder` selects the new builder independently of
`UseGravityGarage` and `Level25D`. Normal sandbox entry enables it. Legacy
Garage fixtures and other games retain their existing behavior. Version 10
has an explicit factory branch and does not inherit version 5–9 Garage geometry
or the version 9 wheel-radius validation. Existing saves keep their original
geometry versions and socket identities; they are not automatically converted.

`SandboxDragBuilder` owns pointer intent and socket feedback; `AssemblyWorld`
continues to own holds, graph ancestry, placement validation, and joints.
Desktop targets use a 72-pixel radius. VR uses `RaySocketAngle` (default 3 degrees)
and the rig's existing maximum ray distance. Source selection is limited to the
grabbed piece; the entire detached branch is tested and transformed together.

The authored scene includes nine editable spawn markers, previews, rotation
buttons, and EXAMPLE. The idempotent editor command is **MetaHackthone > Downhill >
Upgrade designer drag builder**. Original marker transforms remain inactive as
an authoring reference. Current course parameters are unchanged.

### Verification

The full EditMode suite passed 326/326. The authored-scene checks passed 3/3
again after the final control-layout update. The focused builder/navigation/regression run passed 23/23, including both
previously failing legacy Garage physics checks on their isolated rerun. Actual
mouse drag/Q/E/socket release and native rotation-button arbitration passed 2/2.
The first broad PlayMode run passed 184/188: two obsolete sandbox expectations
were corrected, while two legacy physics completion checks passed on rerun.
This is not a claim that a single broad run was entirely green.
The final builder/desktop pass verified all 14 designer tests and five desktop
tests. Its remaining obsolete turntable test was replaced with actual mouse
clicks on LEFT 90 / RIGHT 90, fixed-core assertions, and Start/Back, then passed
on rerun. The old core-rotation ring is disabled only for the designer builder.
The authoring command was run twice and the second run left the scene unchanged.
Runtime Workbench, Example, and OnSandbox captures were inspected; course labels
hide during building and return during testing.
Evidence is recorded under `Logs/DesignerSandbox/`. Worn-headset targeting,
reach, and comfort remain manual acceptance checks; automated input callbacks
cannot establish them. No headset installation is included in this change.

Android development build succeeded in 215 seconds with zero errors and four
warnings. Output: `Builds/MetaHackthone-DesignerSandbox-Quest.apk` (196,447,476
bytes). SHA-256: `2E4F3C0C4DD1E576CD364719ED2D06D978A3224EBC8417008E3FC6FD6EA566E4`.
Warnings concern the unconfigured appID, two missing scripts in PICO package
hand prefabs, and the PICO loader's 16KB alignment on Android 15+; these package
and platform configuration issues were not changed by the builder work.
The build includes all nine enabled scenes with MainMenu first. The editor was
left stopped in the clean, saved DownhillSandbox scene. No check-in was made.


## 2026-10-30: eight-socket core

New sandbox cores use grid version 4: one rigid 2 x 1 x 2 block with eight shared face-center sockets on the X/Z sides and no Y sockets. Dimensions are derived from BuildingGeometry.Unit; at the authored 0.4 spawn scale the core is approximately 6.98 x 3.49 x 6.98 cm. Wheel dimensions and physics are unchanged. Adjacent wheel placements can therefore be blocked: eight sockets are placement choices, not eight guaranteed simultaneous wheel mounts.

A blocked placement displays a red ghost when no clear candidate exists; release preserves the existing construction and reports the obstruction. Wheel-wheel penetration uses a scale-aware 0.01 mm numerical tolerance at scale 1. Other attachment filters retain their prior behavior. The Design Mode preset uses a symmetric diagonal pair of large wheels. Legacy core grid versions retain their two original endpoints; new saves also record sandbox geometryScale (old records default to 1).

Validation: 135/135 PhysicalAssembly EditMode tests passed, including all 28 socket pairs, four wheel-size combinations, both mounting faces, both placement directions, 16 tire angles, and three scales. The initial 54-test PlayMode run passed 50; after updating obsolete core assertions and the reference fixture to the new diagonal preset, all six focused checks passed, including the repeated gravity-course completion and scale/return checks. Two unrelated tests remain unresolved: mode switching currently forces Workbench, and changing a spawn scale after startup does not recreate existing parts. Thus both-mode ten-cycle validation is not claimed. No APK was built or deployed.


**2026-10-27:** The [fixed sandbox interface](SandboxInterface.md) adds Start/Reset/Quit,
in-place test controls, a current-course card, and stationary countdown/status
anchors. Quit now returns to the main menu on Android as well as desktop. The
historical Exit behavior and head-following descriptions below are superseded.

Updated 2026-10-24. This supersedes the earlier sixteen-socket core, timed tray returns, and single Start/Return control described in the historical Downhill notes.

## Launch and play

Open `Assets/MetaHackthone/Scenes/MainMenu.unity` in Unity **6000.3.21f1**, then press Play, or use **MetaHackthone > Play Desktop Game**. The APK also starts at MainMenu.

1. Select **WORKBENCH BUILD** or **BUILD ON SANDBOX** in the top row. The other four game choices remain available.
2. Workbench Build has an editable bench. Sandbox Build has no active workbench and places the core above the course start. Both provide the same seven authored parts; the optional irregular-block slot is empty.
3. Pinch a floating part, bring its connector near the core or attached vehicle, and release to snap. Only the red core has two sockets, on its local left and right. Loose parts stay where released. The side wheel rotates the assembled design.
4. Release held parts and press the floating **START** tile on the left of the building area. It stays anchored there when you move or turn your head. The build controls and unused kit disappear, the course approaches without moving the XR rig, and **3 → 2 → 1 → GO** finishes before gravity starts.
5. **RETRY** resets the same test instance and repeats the countdown. **BACK TO BUILD** restores the saved editable design and the selected layout. **EXIT** quits Android; in the editor and other runtimes it safely returns to the main menu.
6. From the build menu, **Main Menu** lets you choose either layout again. The connected design is retained. Loose standalone parts return to the selected layout's palette; detached connected branches retain their internal connections.

Desktop controls: click menu/buttons, drag parts, drag the side wheel, use Delete to detach a branch, and Tab to test/return. The desktop toolbar also exposes Ready/Test or the three test controls. Open MainMenu to exercise both choices; opening DownhillSandbox directly defaults to Workbench Build.

## Implementation and tuning

The existing Unity/URP, Meta XR ray/poke controls, custom `AssemblyWorld` grab system, Rigidbody joints, passive wheels, authored course, and navigation retention are reused. No package dependency was added.

`GardenNavigation.SelectedBuildMode` chooses the layout. `DownhillPresentation` owns bench visibility, the palette, side controls, hazard markers and course positions. `DownhillController` owns Build → Transition → Countdown → Running/Result, snapshots, Retry, Back and Exit. It freezes the test vehicle throughout the transition/countdown and restores the global clock on return, disable, pause, focus loss or exit.

The existing disposable simulation-copy approach preserves the original editable design. Only the connected vehicle is copied into the test world; originals and unused pieces are inactive during testing. Retry reuses that simulation instance. Back destroys it and restores the original body poses. The direct-on-level layout transfers at scale 1, preventing a second visible vehicle or size jump. The workbench vehicle is transferred at the established miniature scale multiplied by presentation scale.

Only the spawned core carries `AssemblyPart.BuildingCore`. Factory/save records propagate this role; ordinary long blocks and every accessory retain their existing versioned sockets. Wheel `mount` and `mount_opposite` are unchanged. No four-point or front/rear wheel rule was added. Quarter-turn snapping is scoped to rectangular-part attachments in the sandbox assembly world; other game modes retain their rules.

Held branches use Rigidbody sweeps and penetration-checked rigid translation/rotation steps. During assembly, only the core and its directly or indirectly attached parts block held movement. Loose parts, other held branches, the bench and terrain are ignored. Snap previews, socket exposure, animated alignment and final placement use the same core-only filter. Preview and committed block attachments retain the 90° orientation rule, socket occupancy, reservations and branch ownership checks.

Contact activates a damped spring between the resolved object pose and the tracked hand target. In free space the object follows closely; against connected solids it stops or slides, then recovers smoothly when clear. The real hand pose is unchanged and there is no tether graphic. Translation and rotation spring velocities are bounded, and release leaves the object floating at its actual pose without throwing it. Hand transfer rebases the grip and clears spring velocity. Tracking loss, pause, reset and teardown discard hold state. Test-mode physics and other game modes retain their existing rules.

Tune `Assets/MetaHackthone/Generated/DownhillSettings.asset`:

| Parameter | Default / purpose |
|---|---|
| WorkbenchScale | 0.5 horizontally; height and parts stay independent |
| PresentationScale | 1.2 for course geometry; simulation parts scale coherently |
| WorkbenchLevelOffset / SandboxBuildStart | Bench-mode course offset / direct-build start at `(0, 0.95, 0)` |
| SnapRadius | 0.09 m, previously 0.04 m |
| BuildSpring.Frequency / DampingRatio | 6 Hz / 0.85; contact-triggered spring response |
| BuildSpring.MaximumDisplacement / MaximumAngularDisplacement | 0.15 m / 45 degrees; bounds spring force, not hand travel |
| BuildSpring.MaximumSpeed / MaximumAngularSpeed | 1.5 m/s / 540 degrees/s during recovery |
| BuildSpring.PositionTolerance / AngularTolerance | 0.001 m / 1 degree; return to direct tracking once velocity also settles |
| TransitionDuration / TransitionEasing | 1.2 s / ease in and out |
| WorkbenchApproach / SandboxApproach | 0.45 m / 0.15 m toward the player in course-local Z |
| PhysicsSpeed | 0.8; scales both timeScale and fixedDeltaTime, preserving real-time physics cadence |
| CountdownStep / GoDuration | 1 s per number / 0.3 s GO before physics |

The authored workbench markers remain editable and are reused at runtime. **MetaHackthone > Downhill > Upgrade build experiences** reapplies the default presentation/palette and refreshes editor previews/materials; it resets those presentation defaults, so use it deliberately after custom layout edits.

## Acceptance report

PASS below means implemented and verified by the cited editor tests, code inspection, or screenshots. It does **not** certify worn-headset reach, optical tracking, perceived comfort, or sustained device frame timing. Those checks are listed separately below.

| # | Acceptance criterion | Status | Implementation and check |
|---|---|---|---|
| 1 | Side-by-side WORKBENCH BUILD / BUILD ON SANDBOX menu | PASS | `MainMenuBootstrap`; `MainMenuTests` checks labels, positions and real ray/poke selection; MainMenu screenshot |
| 2 | Working assembly with approximately half-size bench | PASS | `DownhillPresentation`, saved scene; collider dimensions and authored-scene tests; WorkbenchFinal screenshot |
| 3 | Direct-on-level assembly with no workbench | PASS | `Configure(OnSandbox)` / `SetBuilding`; two-layout tests and cold direct-mode editor launch; SandboxFinal screenshot |
| 4 | Usable floating parts and side Ready/Test control in both | PASS | Physical factory parts, existing grab and MetaMenuButton bindings; grab/regrab, palette-clearance and actual mouse-input tests |
| 5 | Core has only local left/right sockets | PASS | `DownhillParts.Create(buildingCore:true)`; exact count/ID and save round-trip tests |
| 6 | Accessories retain original connection points | PASS | Role-specific factory branch; accessory-by-accessory socket ID/position comparison and existing socket/save tests |
| 7 | No new four-point/front/rear wheel requirement | PASS | Wheel-generation branch unchanged; both existing hub faces and free tire rotation tested |
| 8 | Configurable snap distance greater than 4 cm | PASS | Settings → world radius 9 cm; acquisition boundary and 7 cm block attachment tests |
| 9 | Held solids cannot pass through assembled solids | PASS | `BuildMotionConstraint`; fast held-block sweep across core is blocked and movement away succeeds; branch reattachment tests |
| 10 | Committed block rotations use 90° steps | PASS | `BuildingGeometry.AttachmentStep`, `AttachmentSearch`, alignment; preview/commit tests from 44°, 46°, 135°, 225°, 315° |
| 11 | Released pieces float in both layouts | PASS | Sandbox `FloatingBuild` and snapshot restoration; release, wait, regrab and detached-branch tests in both modes |
| 12 | Ready/Test clears clutter and approaches without XR camera animation | PASS | Presentation + controller transition; hidden kit/bench, disabled editing, unchanged rig and coherent frozen body tests; screenshot review |
| 13 | Visible 3, 2, 1, GO before physics | PASS | Unscaled countdown then dynamic/gravity release; countdown delay/body immobility tests and Countdown screenshot |
| 14 | Clearer hazards and slower stable testing | PASS | 1.2× course, orange barrier/gap/slope cues; screenshot review, 0.8 clock/fixed-step assertions, three completed gravity-course attempts |
| 15 | Retry preserves design and stays in test | PASS | `TryRetry`; same simulation core and part IDs across retry, reset poses and repeated countdown |
| 16 | Back restores correct editable layout without duplicates | PASS | `TryReturn` / `RestoreBuildImmediately`; exact original poses/IDs, eight parts, restored clock/input and mode checks |
| 17 | Exit quits or safely returns to menu | PASS | Android `Application.Quit`, editor/other runtime menu callback; editor fallback and cleanup test. Android OS quit needs device confirmation |
| 18 | Repeated build/test/retry/back cycles preserve parts/input/physics | PASS | Ten cycles per layout, each including Retry; stable original/test IDs, connection counts, restored gravity/kinematic state and cleared simulation roots |

Primary implementation: [controller](../Assets/MetaHackthone/Runtime/DownhillController.cs), [presentation](../Assets/MetaHackthone/Runtime/DownhillPresentation.cs), [collision constraint](../Assets/MetaHackthone/Runtime/BuildMotionConstraint.cs), [core/parts](../Assets/MetaHackthone/Runtime/DownhillParts.cs). Focused checks: [sandbox tests](../Assets/MetaHackthone/Tests/PlayMode/SandboxExperienceTests.cs), [downhill tests](../Assets/MetaHackthone/Tests/PlayMode/DownhillTests.cs), [main menu tests](../Assets/CloudGarden/Tests/PlayMode/MainMenuTests.cs).

## Checks and artifacts

- Startup readiness passed for Meta CLI 1.6.0.14, Quest MCP, Unity MCP 10.3.0 and Unity 6000.3.21f1. The active session was verified against this project; Quest discovery succeeded. This is connection verification, not an in-headset playtest.
- **272/272 EditMode tests passed** across PhysicalAssembly, CloudGarden and ShadowWorkshop (`ba9b8a5af0dd4bda97f90940c36650c5`). The final saved scene was checked again: **3/3 authored-scene tests passed** (`2f72a36013ba4e22a27ae20aeb28535d`).
- **58/58 focused PlayMode tests passed** on the final runtime changes (`554fa6e7ad044802922f46414911f13b`): all Downhill tests, authored runtime layouts, actual desktop sandbox controls, and MainMenu tests.
- The earlier broad PlayMode run passed **134/137** (`e5537c1c35964901af7cccee77742361`). Its authored-layout and desktop-control failures were corrected and passed in the final focused run. The remaining unrelated `NarrowGapTests.WideHullPhysicallyHitsStakes` failure reproduces the issue already recorded in the historical Downhill documentation: core Z=0.915294 versus expected <0.90. Its physics and assertion were not changed.
- Final interactive editor review had **zero console errors**. MainMenu, Workbench Build, Sandbox Build, Countdown and Test HUD were rendered and inspected. The XR rig stayed at the same pose through launch/return. No hardware deployment or worn-headset test was performed for this build.
- Evidence: `Logs/VehicleBuildImplementation/FinalEditMode272.json`, `FinalBuildModes58.json`, `BroadPlayMode137.json`, `MainMenu.png`, `WorkbenchFinal.png`, `SandboxFinal.png`, `Countdown.png`, `TestHUD.png`.
- **Android development build succeeded**, ARM64 / IL2CPP, all nine enabled scenes, MainMenu first (`build-d7602fcf3e`): **0 errors, 4 warnings**, 290.5 seconds. APK: [MetaHackthone-BuildModes-Quest.apk](../Builds/MetaHackthone-BuildModes-Quest.apk), **147,575,705 bytes**. SHA256: `F95CF0FBC02E628CB7B882282C0287FF1944C9FC7B12733FC4AC24DC094A11DA`. Evidence: `Logs/VehicleBuildImplementation/QuestBuild.json`.
- Build warnings concern the existing Pico SDK/configuration: appID unconfigured, missing scripts in its `leftHand.prefab` and `rightHand.prefab`, and `libpxrplatformloader.so` lacking 16 KB alignment for Android 15+. The Quest OpenXR configuration was used; the Pico feature was disabled. Those package/configuration issues were not changed by this task.
- Unity was left outside Play mode in the saved, clean MainMenu scene. Final console error count: **0**. The APK has not been installed or run on hardware during this implementation.

## Files changed

- `Assets/CloudGarden/Integration/`: `MainMenuBootstrap.cs`, `GardenNavigation.cs`, `DownhillEntry.cs`.
- `Assets/MetaHackthone/Runtime/`: new `DownhillPresentation.cs` and `BuildMotionConstraint.cs`; updated `DownhillController.cs`, `DownhillSettings.cs`, `DownhillCourse.cs`, `DownhillParts.cs`, `DownhillSpawnPreview.cs`, `WorkshopBootstrap.cs`, `DesktopInputAdapter.cs`, `AssemblyWorld.cs`, `AssemblyPart.cs`, `AssemblySaveService.cs`, `PartFactory.cs`, `AttachmentSearch.cs`, `BuildingGeometry.cs`.
- `Assets/MetaHackthone/Editor/`: new `SandboxExperienceAuthoring.cs`; updated `BuildingSceneAuthoring.cs` for the core preview and shared start anchor.
- `Assets/MetaHackthone/Scenes/DownhillSandbox.unity`, `Generated/DownhillSettings.asset`, and persisted mesh/material resources in `Assets/MetaHackthone/Art/SceneLayouts/DownhillSandbox 1.asset`, `DownhillSandbox 2.asset`, and `DownhillSandbox 3.asset`.
- Tests: new `SandboxExperienceTests.cs`; updated `DownhillTests.cs`, `DownhillBranchTests.cs`, `DownhillInteractionTests.cs`, `DesktopTests.cs`, `BuildingSceneAssetTests.cs`, and CloudGarden `MainMenuTests.cs`.
- Documentation: this report and the historical `DownhillSandbox.md` pointer. New Unity scripts include generated `.meta` files. Existing unrelated workspace changes were retained; no check-in/push was performed.

## Quest installation — 2026-10-24

Rebuilt the current project successfully (`build-b61d1e3d89`, 11.7 seconds, zero errors, two existing warnings: unconfigured appID and Pico library alignment). The APK hash is unchanged from the build recorded above. Installed as an update on Quest 3 `2G0YC5ZG5V04ZW`; package verification reports update time `2026-10-24 21:14:31`, retains the original first-install time and data directory, and replacement installation preserves app data. Launched successfully; process 3680 remained running and OpenXR reached `XR_SESSION_STATE_FOCUSED`. The captured launch sample is `Logs/VehicleBuildImplementation/QuestInstalledLaunch.log`. This supersedes the earlier not-installed status; physical gameplay and comfort checks below remain pending.

## Core-only collision and contact spring update — 2026-10-24

Implemented in both build layouts using the existing grab system. `IAssemblyCollisionRules` supplies the core-connected collider set; `AttachmentSearch`, socket exposure and `BuildMotionConstraint` use it consistently. `BuildHoldSpring` integrates bounded linear/angular spring velocities once per physics tick. Branch members retain a single rigid transform. This is a contact-response spring, not a replacement XR hand or a new dynamic joint on the core.

| Check | Result | Evidence |
|---|---|---|
| Only core-connected solids block; membership updates after detach/reattach | PASS | Both-layout `CoreOnlyBlockerMembershipUpdatesForBothLayoutsAndWholeBranches`; covers indirect parts and tire colliders |
| Loose/other-held/environment solids do not block movement or snapping | PASS | Both-layout `LooseAndOtherHeldSolidsDoNotBlockMovementOrCommittedSnap`; preview and actual committed connection checked |
| Contact response, bounded recovery, settling, sliding and rotational obstruction | PASS | Nine new EditMode cases in `BuildHoldSpringTests`, including 10/20/50 ms steps, large deflection, rigid branches and overlap escape |
| Floating release, handoff, tracking loss, pause and reset | PASS | Both-layout `SpringReleaseHandoffPauseAndTrackingLossPreserveResolvedPose`; existing branch ownership tests |
| Existing 90-degree alignment and repeated test flow | PASS | Final Downhill suite, including ten build/test/retry/back cycles per layout |
| Android build, update installation and launch | PASS | Build `build-a4d8396003`, 127.6 seconds, zero errors; package update and running process verified |
| Worn-headset spring comfort and sustained frame timing | NOT VERIFIED | Requires physical interaction on Quest; launch logs cannot establish these |

Final automated results: **281/281 EditMode tests** (`a82ea148e665497ba1b67b9ebc5cc510`) and **58/58 focused PlayMode tests** (`edd9588d45be4a528b39fe3ad896a7ff`). The PlayMode run covers all Downhill, DownhillWheel, Desktop and CarLoad tests. Evidence is in `Logs/BuildSpring/EditMode281.json` and `FinalPlayMode58.json`.

The intermediate broad PhysicalAssembly run passed 110/114. Two new test-fixture issues were corrected: rotated sandbox placement in the wheel helper and an exact floating-point comparison. Both pass in the final run. The car steering test measured 3.55 degrees against its >4-degree threshold in the broad run, then passed unchanged on rerun. The existing unrelated Narrow Gap wide-hull regression remains (Z=0.91793 versus <0.90); it was not changed. `BroadPlayMode114.json` retains these results. One additional test attempt was interrupted by a Unity script reload before any tests ran; the clean rerun completed, and its generated temporary scene was removed.

APK: [MetaHackthone-Spring-Quest.apk](../Builds/MetaHackthone-Spring-Quest.apk), **147,598,110 bytes**, SHA256 `67E9D60DE132EE0D15C7E5B6B59A9A61757D26F9BB6CE1A3F5A5800C0CDE4775`. Installed with replacement/preserve-data semantics on Quest 3 `2G0YC5ZG5V04ZW`, package `com.metahackathon2026.metahackthone`, update time `2026-10-24 22:09:11`. Process 8372 launched and remained alive. The sampled Unity/AndroidRuntime log contained no errors; OpenXR had focus and subsequently entered idle. This is a launch smoke check, not a worn-headset gameplay test. Logs: `QuestBuild.json`, `QuestLaunch.log`, `QuestUnityLaunch.log` under `Logs/BuildSpring`.

Four existing build warnings remain: unconfigured appID, missing scripts in the Pico left/right hand prefabs, and the Pico library's Android 15+ 16 KB alignment warning. The connected Quest runs Android 14.

Runtime files changed for this update: `AssemblyLevelInterfaces.cs`, `AssemblyWorld.cs`, `AttachmentSearch.cs`, `BuildMotionConstraint.cs`, new `BuildHoldSpring.cs`, `DownhillController.cs`, `DownhillSettings.cs`, and `Generated/DownhillSettings.asset`. Test changes: new `BuildHoldSpringTests.cs` and `SandboxSpringTests.cs`; updates to `SandboxExperienceTests.cs`, `DownhillInteractionTests.cs` and `DownhillTests.cs`. New scripts include Unity-generated metadata. No package, scene-layout, socket or save-format migration was needed.

To try the update, launch the installed app and choose either **WORKBENCH BUILD** or **BUILD ON SANDBOX**. Hold a block through a loose part, then press it against the core or an attached wheel/block. It should pass through the loose piece, resist the assembled vehicle, and recover smoothly when you move your hand away. Release during recovery, transfer between hands, and repeat in the other layout. Tune the `BuildSpring` fields on the generated settings asset if a different feel is desired.

## Floating START update — 2026-10-24

**Superseded on 2026-10-25:** START is now anchored beside the assembly area in both layouts. Head movement, camera rotation, hover and Back to Build do not reposition or rotate it. The existing test HUD and rotation wheel remain anchored. Explicitly relocating the entire building area moves its UI with it. Tune `StartBuildOffset` (default -0.70, +0.30, -0.10 m relative to the assembly position in the workbench frame) and `StartButtonSize`; the camera-offset and follow-delay settings have been removed. The account below records the earlier head-following design.

Anchored-UI verification: **4/4 EditMode checks passed** (saved-scene resources and idempotent configuration), and **3/3 final PlayMode checks passed** (head translation/rotation and test-return pose stability in both layouts, native ray/pinch, native poke, and actual desktop click). Final runtime evidence: `Logs/AnchoredStart/PlayMode3.json`; visual placement: `Logs/AnchoredStart/WorkbenchAnchored.png`. Unity console errors: zero. Publication build on 2026-10-25 succeeded: Android ARM64/IL2CPP development APK, zero errors and four existing SDK warnings, 218.1 seconds (`build-beb6b222ed`). Replaced `E:/Unity/Build/MetaHackthone-FloatingStart-Quest.apk` with the anchored-UI build: 147,673,477 bytes, SHA256 `69686917FDF4722D4734706A5FDC7223D8F795DF3853E3CC6D416521D8F5BAFC`. Evidence: `Logs/AnchoredStart/QuestBuild.json`. The seven checks above ran during implementation immediately before publication; they were not rerun during the build. This replacement APK has not been installed on the headset.

Both menu choices now share a translucent navy START tile with a cyan rounded outline, halo and play icon, matching the reference image in `E:/Unity/Image`. It follows the active XR/desktop view independently of the rotation wheel and test HUD. Hover/select freezes its pose; it resumes after a short delay. Starting a test hides it, and Back to Build reacquires the current view without creating another tile. Existing held-part guards, ray/pinch, direct poke and desktop click paths remain in use.

Tuning is in `Generated/DownhillSettings.asset`: `StartViewOffset` = (-0.32, -0.16, 0.60) m, `StartButtonSize` = (0.22, 0.24) m, `StartFollowTime` = 0.20 s, `StartResumeDelay` = 0.25 s. The offset is preferred: fitting keeps all tile corners in the outer-left 4–26% of the view, adjusting depth for narrow views. Recovery eases toward that band and clamps immediately only at the screen edges. The tile faces the eye and uses camera up so looking down does not leave the panel strongly tilted. No follower moves the camera or level.

The saved Downhill scene includes the component and persisted mesh/material references in `Art/SceneLayouts/FloatingStart.asset`. **MetaHackthone > Downhill > Upgrade floating Start** refreshes only the Start presentation and preserves authored palette/course markers. Runtime creation uses the same idempotent configuration path.

| Check | Result | Evidence |
|---|---|---|
| Left-side styling in both layouts | PASS | Rendered and inspected `Logs/FloatingStart/WorkbenchFinal-1.png` and `SandboxFinal.png` |
| Follow, camera-FOV fit, hover/select freeze, delayed smooth recovery | PASS | Five `FloatingStartButtonTests` cases; final complete EditMode run **286/286**, job `2b3f35faf05e450cb4161a7f07c152ec` |
| Native ray/pinch and direct poke in both modes; held-part guard and return | PASS | `FloatingStartFlowTests` uses actual SDK interactors |
| Desktop click and rotation wheel; ten test/retry/back cycles per layout; countdown cancellation | PASS | Final focused PlayMode run **5/5**, job `cff73f01f6b04d4f82d1e9c2cb2bb142` |
| Saved scene, retained resources, repeated configuration without duplicates | PASS | Authored scene and follower tests; final editor review had zero console errors |
| Physical reach, eye comfort and hand tracking on a worn headset | NOT VERIFIED | Requires in-headset interaction; synthetic SDK tests and camera renders cannot establish these |

Evidence: `Logs/FloatingStart/FinalEditMode286.json`, `FinalPlayMode5.json`. An earlier broader PlayMode run was **62/63** (`PlayMode63.json`): existing `ReturnCancelsCountdownAndRestartGetsFullDelay` returned to Build unexpectedly. A preceding run also had an intermittent `TransferScalesEveryBodyAndRestoresWithoutDrift` state failure. Both passed unchanged in the focused four-test rerun (`FinalFocused4.json`); countdown cancellation also passed in the final five-test run. These intermittent editor state failures are recorded rather than counted as a clean broad-suite pass.

Files changed for this update: new runtime `FloatingStartButton.cs`; `DownhillSettings.cs`, `DownhillPresentation.cs`, `DownhillController.cs`, `DesktopInputAdapter.cs`, `XR/MetaHandInputAdapter.cs`; editor `SandboxExperienceAuthoring.cs`, `BuildingSceneAuthoring.cs`; saved `DownhillSandbox.unity`, `Generated/DownhillSettings.asset`, `Art/SceneLayouts/FloatingStart.asset`; new `FloatingStartButtonTests.cs` and `FloatingStartFlowTests.cs`, plus `BuildingSceneAssetTests.cs` and `DownhillTests.cs`. New scripts/assets include Unity metadata. No packages or save formats changed.

Android development build **PASS** (`build-fa411ac584`, 107.2 seconds, zero errors, four existing SDK warnings described above). APK: [MetaHackthone-FloatingStart-Quest.apk](../Builds/MetaHackthone-FloatingStart-Quest.apk), **147,608,468 bytes**, SHA256 `9B60C2E9BA92B76C776D4F34F0CDE43EAA2FAFA53DCD525D601811D63AA326FE`. Installed with replacement/preserve-data semantics on Quest 3 `2G0YC5ZG5V04ZW`; package update verified as `2026-10-24 22:47:43`. Launch **PASS**: process 15213 remained alive, OpenXR reached FOCUSED then paused to IDLE, and the sampled Unity/AndroidRuntime log contained no errors. Evidence: `Logs/FloatingStart/QuestBuild.json`, `QuestInstalledInfo.json`, `QuestLaunch.log`. Unity was left outside Play in the saved, clean MainMenu scene.

To verify on Quest, launch the app, choose either build mode, and look around: START should stay on the left and settle smoothly. Aim a hand ray or approach with a fingertip, move your head while hovering, and confirm the tile holds still. Release any grabbed part, activate START, then use Back to Build and repeat in the other mode. Check near-field reach, text readability and hover stability while looking down at the vehicle.

## Remaining headset checks

On a worn, floor-calibrated headset, try both choices from a cold menu. Check side-control reach and text, direct-build height, grabbing/regrabbing both wheel faces, fast hand motion and rotating held blocks against solids, branch handoff, optical tracking loss, transition comfort and visibility of GO. Repeat test/retry/back at least ten times per mode, then exercise Android Exit. Profile sustained device frame times with the collision-constrained grab path. Editor physics tests do not establish those human/device-specific results.

The existing SDK/runtime hand support is unchanged. Quest APK compilation does not verify Pico hardware behavior. Long or deliberately intersecting player-built branches may require repositioning against the connected assembly; blocked alignment is cancelled rather than passing through core-connected solids. Loose overlaps no longer veto snapping. The separate Narrow Gap collision regression remains open as described above.

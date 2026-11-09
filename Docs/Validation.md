# Palm menu, sandbox polish and victory rewards - 2026-11-05

- Publishes the pending sandbox polish, forgiving palm menu, separate-part victory return, and drill reward/reminder flow. See SandboxInterface.md.
- Prior implementation validation: 10/10 focused PlayMode tests and 8/8 return/boundary checks passed (overlapping coverage). The 15 remaining broad-suite failures reproduced against the original controller. Evidence remains in Logs/PalmVictory20261105.
- Android development build build-906e141eaf succeeded with zero errors and 20 warnings. The verified APK is now Build/MetaHackthone-PalmVictory-Quest.apk, 233,649,862 bytes, SHA256 36A0B3ECB01ED5BD1A983CC1EDBE69037318A9388A081060B56D3038D24A4CCD. This is the build already installed on Quest 3 2G0YC5ZG5V04ZW; no rebuild was needed for moving the artifact.
- Automated Quest acceptance passed; peak fragments 511/512. Frame samples had a 34.74 ms maximum; Perfetto showed a transition hitch. Physical-hand comfort, touch activation and both-eye animation acceptance remain deferred by the user.
- Publication cleanup removed seven disposable editing scripts/backups (113,136 bytes), retained test evidence and historical builds, and cleared the disabled development agent's generated token and machine address. The APK remains local and ignored by version control. Metadata and credential-field checks passed before submission.
- Existing local package dependency E:/Unity/Tools/PICO-Unity-OpenXR-SDK remains required on another machine. Destination is Unity Version Control cloud repository MetaHackathon2026/MetaHackthon2026, branch /main.

# Sandbox campaign and interaction publication — 2026-11-02

- Includes the designer building rules, Workbench-only gameplay, Gravity Garage to Teleport Pipes progression, Design Mode controls, compact action panels, combined pad and wheel direction group, loose-group two-hand separation, fist/pinch grabbing, and START/GOAL arch meshes. See [SandboxBuildModes.md](SandboxBuildModes.md).
- Fresh publication checks: **326/326 EditMode** and **28/28 PlayMode** tests passed. These cover saved assets, gestures, campaign completion/progression, menus, Design Mode, and desktop controls. During arch implementation, **92/92 PlayMode** tests also passed, including wheel/arch rotation, unchanged launch pose, and repeated physical course runs. Evidence: `Logs/CleanPublish20261102` and `Logs/CourseArches`.
- Cleanup removed 15 failed/intermediate arch captures (3,168,427 bytes), restored 10 formatting-only generated files, and cleared the disabled development agent's injected token. Final captures, test evidence, source assets, Unity caches, and the current APK were retained. README interaction instructions now match the shipped source.
- The existing local package dependency `E:/Unity/Tools/PICO-Unity-OpenXR-SDK` is still required on another machine. The arch assets are self-contained; the reference PNG files are not runtime dependencies. No additional APK build or headset installation was performed for publication; the installed grabbing-polish APK predates the arches.
- Destination: Unity Version Control cloud repository `MetaHackathon2026/MetaHackthon2026`, branch `/main`.

# Design Mode publication — 2026-10-25

- Added Editor-only Design Mode for Car Workshop and both Sandbox build choices, editable test-car spawn, direct tests of unsaved scene edits, and preset reset controls. Authored transforms and player construction saves are preserved. Saved-course contact margins and passive tire scrub damping make the supplied Sandbox car repeatably complete the course.
- Designer instructions, controls, screenshots, and troubleshooting are in [DesignModeGuide.md](DesignModeGuide.md), linked from the consolidated README and [SceneEditing.md](SceneEditing.md). The README's three overlapping copies were reduced to one, preserving current shared-grid, pinch-only, and Shadow Gym guidance.
- Fresh pre-publication validation: all **19 Play-mode tests** passed (`d15e5b6e74fe4d6388eb787df0f1a848`) and all **5 Edit-mode scene checks** passed (`53bbe44b4d0f47acb4e406e11417c15d`). Coverage includes six saved-Sandbox finishes, mode switching, save isolation, mouse/keyboard interaction, navigation, wheel behavior, and authored scene transforms. Documentation links, exact menu labels, and new Unity script metadata were checked.
- Cleanup removed 19 obsolete review snapshots and failed captures (669,873 bytes), restored formatting-only/generated settings changes, and cleared the disabled development agent's injected token. Useful screenshots, active logs, Unity caches, source assets, and build outputs were retained. The pending fast Play-mode setting is preserved.
- The existing local dependency `E:/Unity/Tools/PICO-Unity-OpenXR-SDK` remains required on another machine; see the deployment notes below. No APK was built or deployed for this publication. Submission uses the existing Unity Version Control cloud repository `MetaHackathon2026/MetaHackthon2026`, branch `/main`.

# Build modes, collision spring and floating START publication — 2026-10-24

- Published scope: shared main menu and floor placement, both sandbox build layouts, core-only collision with contact spring response, and the view-following START tile. See [Sandbox build modes](SandboxBuildModes.md) for implementation, acceptance checks, changed files, and headset instructions.
- Fresh publication checks: **59/59 PlayMode tests passed** across Downhill, Desktop and Shadow integration (`390abc8f0c734b378e34d1de57b0adf3`); **14/14 main-menu/navigation tests passed** (`830a1091c1904a01a8cacb928930bbb0`). Evidence: `Logs/CleanPush.PlayMode59.json`, `CleanPush.Menu14.json`. The earlier **286/286 EditMode** result belongs to the implementation turn and was not rerun for this cleanup. The known unrelated Narrow Gap regression was not part of these focused publication runs.
- Fresh Android ARM64/IL2CPP development build **succeeded**, zero errors and two existing warnings (unconfigured appID and Pico Android 15+ native-library alignment), 11.3 seconds (`build-a14bf7a974`). Output: `E:/Unity/Build/MetaHackthone-FloatingStart-Quest.apk`, **147,608,468 bytes**, SHA256 `9B60C2E9BA92B76C776D4F34F0CDE43EAA2FAFA53DCD525D601811D63AA326FE`. This matches the previously installed and launch-checked APK; no additional device installation was needed for this publication request. Evidence: `Logs/CleanPush.QuestBuild.json`.
- Cleanup removed seven obsolete import-worker logs (**559,132 bytes**), restored ten formatting-only generated asset/settings files, and cleared the disabled development agent's build-generated access token. Source artwork, Unity metadata, current builds, active logs, screenshots and test evidence were preserved. Existing ignore rules already cover generated builds, logs and credentials.
- The existing local package `E:/Unity/Tools/PICO-Unity-OpenXR-SDK` remains required on another machine. No package migration was performed. In-headset reach, tracking and comfort remain manual checks. Publication uses the existing Unity Version Control cloud repository `MetaHackathon2026/MetaHackthon2026`, branch `/main`; APK output remains outside the source repository.

# Downhill launch countdown and publication - 2026-10-22

- After START teleports the car to the sandbox, all bodies remain kinematic for three seconds. The status shows 3, 2, 1 before physics begins. RETURN cancels immediately; restarting gets a full new countdown. Run/stall timers begin with physics, and disabling the level cancels the pending launch.
- **131/131 EditMode tests passed** (`3c5ac86a220a4440a95d1dd728f59b7b`). **116/117 PlayMode tests passed** across PhysicalAssembly and CloudGarden (`1c4f2b7366bf4478b9a95d84eb5041ae`). All countdown, cancellation/restart, branch/transfer and interaction regressions passed. Evidence: `Logs/Countdown.EditMode.json`, `Logs/Countdown.PlayMode.json`.
- The existing Downhill reference-car gravity-course test still fails because the car stops before the finish. The previously intermittent Narrow Gap wide-hull test passed in this run. Neither physics assertion was relaxed.
- Quest development APK: `E:/Unity/Build/MetaHackthone-SandboxCountdown-Quest.apk` (147,185,192 bytes). Final build `build-2bb10c4d4d` succeeded with zero errors and two existing warnings: unset platform app ID and Pico native-library Android 15 16KB alignment. Evidence: `Logs/Countdown.QuestBuild.json`. The earlier hand/wheel build below was installed on Quest; this countdown APK has not been installed or tested while wearing the headset.
- Cleanup removed nine obsolete review scripts, manifests and temporary snapshots (685,920 bytes), retained current builds and validation evidence, and restored generated settings noise. The Meta SDK automatically injects a development token during builds; it is cleared from the disabled agent's source settings before publication. The documented local dependency `E:/Unity/Tools/PICO-Unity-OpenXR-SDK` remains required on another machine.

# Downhill symmetric hands, two-sided wheels and larger simulation - 2026-10-22

- Downhill now supports either-hand attachment and dependent-branch detachment, atomic hand transfers from any held member, and one cancellable 10-second return timer per dropped group. Desktop grabs use the same branch rules. Workshop and Narrow Gap interaction regressions pass.
- Both sandbox wheel faces have independent hub sockets and mirrored markings; `mount` and its anchor remain compatible with existing saves. Vehicle and live-preview scale is now 2/3, exactly twice the previous dimensions. Building dimensions, course geometry, masses and resistance settings are unchanged.
- **112/112 EditMode tests passed** (`1bb418d56ad9485d94bc87374083b2db`). **112/114 PlayMode tests passed** across PhysicalAssembly and CloudGarden (`03fa952fd3a64b1783cb9d42deb9b27e`). All new branch, transfer, return, wheel, save and scale checks pass. Results: `Logs/SandboxHands.EditMode.json`, `Logs/SandboxHands.PlayMode.json`.
- The pre-existing physics failures remain: Downhill's reference car stops before the finish, and Narrow Gap's wide hull reaches Z=0.910613239 against Z<0.90. Their assertions are unchanged. Historical wheel comparison tests still exercise their original one-third-scale rigs; the new scale checks verify enlarged geometry, passive tire rotation, ramp clearance and preview/launch placement.
- Refreshed authored wheel previews without regenerating layouts. All 190 retained transforms and 57 course colliders are unchanged; repeating the refresh leaves the scene file unchanged (`Logs/SandboxHands.SceneReview.json`). Visually reviewed [wheel symmetry](DownhillWheelFaces.png) and the [enlarged 37-mesh car preview](DownhillTwoSidedPreview.png).
- Unity is outside Play mode in the saved Downhill scene. Compilation and runtime visual checks had no errors. Worn-headset hand-transfer feel remains unverified.
- Quest development build `build-21abfd4803` succeeded with zero errors and four existing SDK/configuration warnings (unset app ID, two Pico controller-prefab scripts, Pico Android 15 library alignment). `Builds/MetaHackthone-SandboxHands-Quest.apk` is 147,185,995 bytes. Installed with replacement on Quest 3 `2G0YC5ZG5V04ZW`; update time `2026-10-22 20:11:15`, original first-install time and app-data directory retained. Launch reached `METAHACKTHONE_READY parts=15 mode=Build`, with no Unity errors or fatal exceptions in `Logs/SandboxHands.QuestLaunch.log`. Open Downhill from the Workshop menu to test the changes.

# Downhill controls and Quest deployment - 2026-10-22

- Added the table Start/Return button, fixed horizontal rotation wheel, direct sandbox-start transfer and live car preview. Wheel attachment acquisition now accepts either flat face while retaining one physical mount. The authored Workshop, Narrow Gap and Downhill layouts and their shared assets are included.
- Earlier in this implementation session, all **103 EditMode tests passed** (`2cbca059c07a46ccb03e1c1841dc9908`). The broad runtime run passed 100/103; its obsolete collider-count expectation was corrected, and all **five focused follow-up checks passed** (`41d37dba67cb43a6a5aa7ec3799b6d17`). The existing Downhill gravity-finish and Narrow Gap wide-hull physics assertions remain failing. These tests were not rerun for the publication cleanup.
- Quest development build `build-0232123ea7` succeeded with zero errors and four SDK/configuration warnings. `Builds/MetaHackthone-DownhillControls-Quest.apk` is 147,181,013 bytes. Warnings concern the unset platform app ID, two Pico controller-prefab scripts, and Pico native-library Android 15 alignment.
- Installed as an update on Quest 3 `2G0YC5ZG5V04ZW`, retaining app data. Package `com.metahackathon2026.metahackthone`, version 0.1.0 (1), reported update time 2026-10-22 19:17:23. Launch reached `METAHACKTHONE_READY parts=15 mode=Build`; physical testing of the new controls remains pending.
- Publication cleanup removed ten stale logs/scratch files (1,377,136 bytes), restored formatting-only generated settings, and cleared the disabled development agent's local access token. Current build outputs, active logs, source assets, screenshots and validation evidence were retained. The documented local Pico SDK dependency remains required; see the deployment section below.

# Grab and connection tuning - 2026-10-14

- Rotation snapping and desktop rotation keys now use 45 degrees.
- New small cubes have 12 connection points, two per face, including face centers for flush quarter-turn placement. Cuboids have 28 and pyramids have six. Generated prefabs match runtime definitions. Existing construction saves retain their original grid layout and anchors; new parts use the reduced layout.
- Near grabs accept a relaxed pinch, partial four-finger curl, or thumb/index/middle grip with the other fingers open. Grab hysteresis, a 120 ms release delay, and 350 ms tracking grace preserve holds through brief jitter. Menu pinching and driving gestures retain their existing rules.
- Attachment search favors opposing faces already lined up by the player. A regression covers a container side wall that otherwise chose a slightly closer perpendicular socket and turned into the interior. The retention fixture checks sphere extents against the actual snapped enclosure, allowing for grid translation rather than assuming a centered opening.
- Final Unity MCP runs: **55/55 EditMode and 46/46 PlayMode tests passed**. Coverage includes grip pickup/movement/release, input jitter and tracking loss, 12-point cube geometry, 45-degree snapping, old/new save round trips, preview performance, container retention, Narrow Gap launch/retry/reset, desktop controls, and Cloud Garden/navigation integration.
- Evidence: `Logs/InteractionTuning.EditMode.json`, `Logs/InteractionTuning.PlayMode.json`. Real optical hand comfort still requires a worn-headset check.

# Narrow Gap challenge validation - 2026-10-14

- Implemented the separate physics challenge, baked course assets, launch and retry controls, gate/landing scoring, and session-preserving workshop navigation.
- **49/49 EditMode and 33/33 PlayMode tests passed** in Unity 6000.3.21f1. The final PlayMode run includes the existing driving, desktop input, attachment performance, and scene navigation regressions.
- A reference connected container physically delivers all five spheres; a wider container collides with the stakes. Reset preserves body identities and connections. Meta ray/poke button callbacks and single-rig navigation are covered by automated tests.
- An earlier combined run encountered an intermittent existing steering-test Build/Drive failure; the final complete 33-test run passed without changing production focus-loss behavior.
- Results: `Logs/NarrowGap.EditMode.json`, `Logs/NarrowGap.PlayMode.json`. Controls and tuning: [Narrow Gap](NarrowGap.md). Render: [Preview](NarrowGapPreview.png).
- Quest deployment and manual headset validation have not been performed for this revision.

---
# Pickup performance validation ? 2026-10-14

- Replaced duplicate all-pairs attachment searches with a shared preview and spatial socket lookup, cached physics geometry, indexed occupancy, and pooled ghost meshes.
- **43/43 EditMode and 22/22 PlayMode tests passed** in the connected Unity Editor. Allocation recorders were calibrated against known allocations; warmed search and cached preview updates produced no managed allocations.
- Median query CPU time decreased from 66?70 ms to 0.32?0.33 ms for 15 parts, and from 163?169 ms to 0.50?0.54 ms for the defined 50-part layout.
- Controlled frame comparison, methodology, p95 results, limitations, and profiler markers: [Attachment performance](AttachmentPerformance.md).
- Test results: `Logs/AttachmentSearch.EditMode.json` and `Logs/AttachmentSearch.PlayMode.json`. Earlier NUnit XML files predate this revision.

---

# Container system validation ? 2026-10-14

- Unity 6000.3.21f1 compiled the changes and regenerated all nine part prefabs without regenerating the workshop scene.
- **35/35 EditMode tests passed**. Coverage includes dimensions and collider surfaces, matching prefab/runtime grids, pyramid slopes, 15-degree twist snapping and wraparound, exclusive endpoint occupancy, detach/reuse, malformed saves, version compatibility, transactional restore, and loose physics spheres.
- **19/19 PlayMode tests passed**. Coverage includes a connected container retaining three spheres while lifted and tilted, cancellation restoring dynamic bodies, moving two-hand attachment, mouse controls, kit/menu integration, and existing default-car driving, steering, loading, and wheel suspension behavior.
- The detached-wheel steering regression now keeps detached wheels physically clear of the chassis while still grounded, so collision impulses do not masquerade as steering assistance.
- Results: `Logs/EditTests.xml`, `Logs/PlayTests.xml`. Rendered workshop: `Docs/Preview.png`.
- This revision has not been deployed to or manually checked on a Quest headset. Real hand interaction, readability, and headset frame rate still require device validation.

---

# Validation — 2026-10-11

## Desktop workshop revision

- Follow-up: right-drag on a part now grabs it before camera input is processed; the wheel changes object depth and only the initiating button releases the grab. Right-drag beginning on empty space retains camera orbit. Added regression coverage for depth in both directions, camera isolation, other-button release, and empty-space orbit; updated existing camera input coverage. All five assemblies compile after this change. The new Play Mode test was not executed because the user's Unity Editor has this project open; the active session was left running.

- Added the Play Desktop Workshop Editor menu, Editor-default desktop input, screen-space toolbar, orbit/pan/zoom and selection framing, 15-degree rotation on three axes, focus-loss cleanup, and toolbar/world-click separation.
- Quick Drive generates a six-part, five-connection car using the existing factory, wheel suspension/axles, launcher, and assembly connection graph. It replaces the current kit, disables the assembly stand, and enters Drive. Reset Parts restores the loose kit. Save/Load uses the existing local construction format.
- New Play Mode tests exercise generated-car topology/socket alignment, forward/reverse and firing through keyboard input, JSON capture/restore, repeated replacement, hold rejection, actual mouse acquisition/movement/rotation/release-to-attach, detachment, camera isolation, toolbar click suppression and focus loss.
- **Compilation passed for all five project assemblies**, including Runtime, MetaXR, Editor, PlayTests and EditTests, using Unity's installed Roslyn compiler and cached Editor reference assemblies. Compiler outputs and the repeatable check are under `Logs/CompileDesktop` and `Logs/CompileDesktop.ps1`.
- **New runtime tests and rendered UI verification are pending.** The normal batch launch could not connect to Package Manager under restricted execution. An elevated retry was rejected by the session policy. Launching through the existing approved PowerShell path reached Unity services, but Unity exited 198 with “No valid Unity Editor license found.” The earlier 30 passing tests below predate this desktop revision. Run `Tools/Unity.ps1 -Task PlayTests` and `-Task EditTests` after restoring the Editor license, then check the Game view toolbar and controls interactively.

## Entire-platform motion investigation

- User confirmed the whole platform, rails, and menu appear to move with head movement. The existing scene creates a root-level static platform with no Rigidbody or AssemblyBody; no physics-driven platform movement has been reproduced. `Logs/DevicePlatformReport.log` captured hand input and a Reset kit action, with no captured Move arena action. This does not establish the cause of the visual movement.
- Runtime now explicitly enables positional/rotational tracking with Stage origin and disables automatic recentering. Environment hierarchy is explicitly excluded from grab, attachment query, alignment, and direct connection/save-load paths.
- Development APK logs `METAHACKTHONE_WORLD_POSE` every two seconds (head, rig, arena, floor offset, origin, holds, joint count), `METAHACKTHONE_PLATFORM_PLACED` for deliberate placement, `METAHACKTHONE_PLATFORM_CORRECTED` for unexpected transform changes, and the two actual parts for every `METAHACKTHONE_ATTACHED` event.
- Play-mode regression: **10/10 passed**, including collision support, drop/re-grab without a platform joint, a moving head leaving the platform fixed, and rejection of accidental assembly components under the environment. These tests do not validate optical world locking on the headset; a new headset reproduction with pose logs is still needed.
- The diagnostic APK reproduced a **camera-rig jump from (0,0,0) to (0,0.5,0)** while the arena stayed fixed at (0.9058,0,0.3587). The bundled comprehensive rig contains `Locomotor/PlayerController` with `FirstPersonLocomotor`, gravity, ground snapping and player collision. It was lifting the tracking origin onto the floating platform. This also offsets tracked hands relative to passthrough and permits collision-driven camera movement as the player moves.
- Corrective change: disable the complete SDK Locomotor hierarchy (including player physics) and locomotion input groups before XR activation; retain hand rendering, menu ray/poke and custom assembly. Added assertions against the actual comprehensive rig in the scene integration test. The scene generator applies the same MR configuration.
- Final checks: **20/20 edit-mode and 10/10 play-mode tests passed** (play tests rerun after the locomotion fix). APK built at 19:01 local time, 123,469,119 bytes, installed with ADB Success and launched.
- `Logs/DeviceRoomScaleFix.log` now records actual head translation/rotation and part holds while rig stays (0,0,0), arena stays (-0.3918,0,0.3797), arena yaw stays 28.60, and surface stays 0.5000 m. A `Wheel/mount to Chassis/chassis` attachment completed during this capture without moving either origin. No captured Unity errors/exceptions or platform correction events. User's visual confirmation remains pending.

Environment: Unity 6000.3.21f1, Meta XR All-in-One 207.0.0, Unity OpenXR 1.17.0; Quest 3 connected over USB.

## Automated checks

- Edit mode: 18/18 passed. Gesture mapping/debounce/loss, graph traversal and connection identity, attachment ownership, detach/collision bookkeeping, and save validation/round-trip.
- Play mode: 5/5 passed after the comfort/layout changes. Four-wheel car moves forward and turns both ways; detached wheel stops receiving vehicle input; attachment aligns and recovers from endpoint destruction; projectile pool hits targets and reuses a projectile; workshop spawns eight parts and nine functional button components.
- NUnit results: `Logs/EditTests.xml` and `Logs/PlayTests.xml`.
- Desktop layout capture: `Docs/Preview.png`. This is an editor camera view, not a through-lens headset capture.

## Headset observations

- Earlier APK installed and launched on Quest 3. Device logs showed a running OpenXR session and the OpenXR hand skeleton.
- User confirmed passthrough, both hand visuals, and UI. User reported that everything was too close.
- Corrective changes: place the workshop relative to the initial tracked head direction, centre 2.2 m ahead; panel approximately 1 m ahead; physical text sizing and larger button text. User confirmation of the corrected comfort is pending.
- Corrected APK built successfully at 17:19 local time (123,457,461 bytes), installed with ADB `Success`, and launched. The new process logged `METAHACKTHONE_READY parts=8 mode=Build` and the OpenXR hand skeleton. No `Exception`, `FATAL`, or `E/Unity` entries were found in that process's captured startup log, `Logs/DeviceComfortUpdate.log`.

## Still requires hands-on validation

Building the complete car with tracked hands, all five real hand poses, simultaneous driving/firing, occlusion and system-menu transitions, save/restart/load on device, ten-minute stability, and measured 72 Hz performance. Quest 3S has not been tested. Automated physics tests do not certify real-hand pose recognition or headset comfort.

## Menu and floor revision

- User confirmed the previous UI layout looks correct, but reported no hand-menu interaction and requested real-floor driving height.
- Replaced custom menu hit/press checks with Meta SDK `RayInteractable` and `PokeInteractable` surfaces. The existing comprehensive rig supplies pointers and pinch/touch selection. Buttons show hover/selection colours and log successful actions.
- Kept the menu elevated independently; arena collider top, rails, stand, parts, and targets now use floor-relative positions. Initial placement uses Stage tracking-space height, and subsequent movement preserves it.
- Play mode: 5/5 passed, including expanded workshop integration coverage: rig contains hand sources and ray/poke interactors, all nine buttons register SDK surfaces, ray hits inside and misses outside, SDK selection creates a part, held selection does not duplicate it, cancel clears feedback, touch invokes its action, and floor height remains correct after initial placement and relocation.
- Real headset selection and floor alignment for this revision remain pending until reported by the user.
- APK built successfully at 17:44 local time (123,459,672 bytes), installed with ADB `Success`, and launched. Device log `Logs/DeviceMenuFloor.log` confirms 18 hand data components, four ray interactors, four poke interactors, nine registered buttons, and eight starting parts. No Unity errors, exceptions, or fatal entries appeared in the captured startup log. Counts include the comprehensive rig's alternate hand/controller paths, not simultaneous physical hands.

## Pinch and placement revision

- User confirmed highlighting, pointing, and direct touch work, but pinch selection and relocating the arena did not.
- Replaced the pointer's selector with a shared hand-source selector using thumb/index tip distance plus the SDK pinch flag. Close/open hysteresis is 28/45 mm, normalized by hand scale. Tracking loss releases and requires an open hand before rearming. Menu pointing/visuals still use Meta's ray interactor.
- Arena placement now uses a registered SDK floor target, the same selector as menu clicks, a visible arena footprint, and a cancel action through Move arena. Removed the separate first-matching-hand ray/plane confirmation path.
- Latest automated results: **20/20 edit-mode and 6/6 play-mode tests passed**. The new end-to-end test feeds pinch distance samples into a real SDK ray interactor, invokes a real menu action, checks no duplicate on hold, opens placement, previews a target, requires a fresh pinch to confirm, and cancels through the menu. These tests do not simulate optical hand tracking.
- The expanded test order exposed a physics-step assumption in the earlier car test. It now explicitly uses the application's 60 Hz step; full steering brakes the inside wheels so both directions pass against tire friction.
- Actual headset pinch reliability and relocation for this revision still require user confirmation. Diagnostic logs distinguish pinch edges (`METAHACKTHONE_PINCH`), hand/ray binding (`METAHACKTHONE_PINCH_BIND`), button actions, and completed placement (`METAHACKTHONE_PLACED`).
- APK built at 18:06 local time (123,460,990 bytes), installed successfully and launched on Quest 3. Startup log `Logs/DevicePinchPlacement.log` shows the workshop ready with nine buttons and no Unity errors/exceptions in the initial capture.

## Floating platform revision

- User requested the driving surface float 50 cm above the real floor, replacing the earlier floor-level requirement.
- Added a 0.5 m driving-surface offset. Arena geometry, initial/reset/extra parts, loaded cars, and the placement outline use the raised surface; tracking-space floor origin and menu height remain separate.
- Play mode: 6/6 passed. Placement checks verify collider top at 0.50 m for a zero-height tracked floor and 0.62 m for a tracked floor at 0.12 m, including after relocating the arena.
- APK built at 18:18 local time (123,458,328 bytes), installed successfully and launched. `Logs/DeviceFloatingPlatform.log` records the workshop ready with eight parts and no Unity errors/exceptions in the startup capture.

## Stable grip and inside-platform revision

- User reported unreliable holds and platform movement while attaching, and requested standing inside the platform with inward-facing UI and height locked at 50 cm.
- Near grabbing now has its own distance-based hold state, independent of menu-ray activation, with sustained-open release and brief tracking-gap tolerance. Kinematic held bodies follow the latest hand pose directly with interpolation restored on release.
- Attaching to an assembly held by the other hand preserves that hand's ownership, follows its movement during alignment, and adds the new part to the same hold. Releasing during alignment restores dynamic bodies after it completes.
- Platform starts centred on the player; menu faces inward from the far edge. Initial floor calibration and platform pose are stored. Only explicit relocation changes horizontal position; part handling/attachment blocks menu and floor-placement selection.
- **20/20 edit-mode and 9/9 play-mode tests passed.** Added coverage for compound grip following, brief pinch/tracking interruptions, intentional release, prolonged tracking loss, two-hand attachment while moving, and release during alignment. Scene assertions check the inside-platform position, inward-facing UI, menu suppression while holding, transform/height lock, and rejection of later height recalibration.
- The regression run caught a 9 mm joint-anchor error from interpolated transforms during moving attachment; alignment now disables interpolation temporarily and calculates anchors from rigidbody poses.
- Real-headset grab feel and optical tracking reliability still require hands-on confirmation.
- Built at 18:38 local time (123,464,752 bytes), installed successfully and launched on Quest 3. `Logs/DeviceStableGrip.log` records workshop readiness with no Unity errors/exceptions in the startup capture. `Docs/Preview.png` shows the editor view from inside the platform.

The editor-only preview tool can log SDK MRUK shutdown and Unity Search indexing errors during batch exit. The preview is still produced; these are separate from APK runtime validation.

## Hustle dimensions and vehicle physics — 2026-10-12

- Extracted constructed source Blueprint dimensions/body settings and audited wheel motor graphs from Hustle; used the user's 20%-scale hand-sized fallback because no placed assembly car was found in the two inspected Level 2 gameplay maps. See `HustlePhysicsPort.md` and the accompanying source JSON exports.
- Rebuilt the five generated part prefabs without replacing the scene/XR setup. Verified saved chassis bounds (0.2555 × 0.0177 × 0.5237 m), wheel bounds (0.1401 × 0.2282 × 0.2282 m), mass, motor torque and steering torque through the live Unity Editor.
- **20/20 Edit Mode and 13/13 Play Mode tests passed** through Unity MCP after final changes. Coverage includes forward/reverse, both steering directions, upright stability, detached-wheel isolation, prefab Quick Drive, saving/restoring, hand-held attachment, desktop rotation/scroll, menu pinch, floor relocation and the locked 50 cm platform height.
- Desktop input tests now isolate synthetic devices and explicitly route test events to Game view, restoring all settings/devices afterward. Placement tests use a point inside the camera-centred plane and clean up even on assertion failures.
- New format-2 save slot preserves the old file without loading incompatible old body poses/anchors into resized parts.
- This revision is validated in the Editor. No new APK deployment or Quest comfort/drive-feel validation was performed for this revision.

## Save/load stability and default car (2026-10-12)

Loading now stages all rigidbody poses with collisions disabled, synchronizes transforms, rebinds internal suspension/axle joints, reconstructs assembly joints, and reapplies collision exclusions before simulation resumes. The regression reproduced catastrophic separation before the fix and passes afterward at three placement headings.

The workshop menu includes Default Car; the desktop toolbar uses Default Car - replaces kit. Both create a chassis, four wheel modules and launcher, replace the current kit, and enter Drive without overwriting the saved file.

Unity MCP validation: 20/20 EditMode and 15/15 PlayMode tests passed; console contained no errors. Loaded-car tests cover forward-left, forward-right, reverse-left and reverse-right with travel direction, signed steering, uprightness and joint separation assertions. The focused run measured 0.50-0.59 m travel and 4.2-6.5 degrees of turn over 90 physics steps. These are steering tests while driving, separate from the placement-heading save/load regression. Tests round-trip JSON in memory and preserve the user's save slot. Quest APK was not rebuilt or deployed for this revision.

## Default Car steering correction (2026-10-12)

Preserved car geometry, sockets, masses, wheel layout, suspension and launcher. Added ChassisSteering to generated and procedural chassis; removed the old wheel yaw-assist torque. Chassis torque uses whole-assembly yaw inertia, a 60 degrees/sec target, gain 40/sec, and a 60 rad/sec² acceleration bound. At least two connected wheels must be grounded. WorkshopSession gates control on Drive mode, valid vehicle, released holds, completed assembly and input focus.

Actual keyboard regression runs W+A, W+D, S+A and S+D on the workshop platform for both freshly generated and JSON-restored cars. All eight cases passed the 30-degree minimum within 90 fixed steps (1.5 sec), positive forward/reverse travel, rail clearance, uprightness, joint stability and release-settling assertions. Focused measurements were 47.7–51.9 degrees and 0.330–0.523 m. Airborne and disconnected-wheel tests passed with no artificial chassis turn. Full suite: 17/17 PlayMode, 20/20 EditMode; no console errors. Existing save format and user save file are unchanged. Editor only; no Quest APK deployment.


## Hustle Level2 visual import (2026-10-12)

Exported 18 static meshes and 10 textures from `/Game/LevelBind/Level2/CarDemoSet/CarParts` using Unreal's FBX/TGA exporters. SourceManifest.json records source asset paths, bounds, material slots and the BP_CarBase/BP_CarWheel/BP_CarSpring references. BP_CarBase uses CarParts_Body_Extension_sm and BP_CarWheel uses CarParts_Wheel_sm plus CarParts_Wheel_Axle_sm; those meshes now supply the corresponding Unity visuals. Spring, main-body and extension meshes also dress the existing hub, launcher housing, beam and block. The launcher barrel and gameplay remain intact. Camera, case, antenna and propeller source meshes are imported but do not add new gameplay parts.

URP materials use exported base color and normal maps. Unreal ARM channels become Unity metallic/smoothness and occlusion textures; normal green channels are inverted. Untextured generic source material slots use neutral metal. Baked visual meshes are centered and fitted to the existing body dimensions, independently of the collision meshes, rigidbodies, joints and socket transforms. Generated prefabs and procedural fallback share a Resources visual library. Existing saves acquire the new visuals without a save migration.

Final validation: 21/21 EditMode and 17/17 PlayMode tests passed with the new visuals. The additional EditMode regression checks valid materials, finite mesh vertices, collider independence, retained chassis steering and original masses. The isolated preview was rendered and visually inspected. Actual keyboard steering remains about 50 degrees per 1.5 seconds for default and reloaded cars. Preview: Docs/HustleCarPreview.png. Export script: Tools/ExportHustleCarVisuals.py; Unity rebuild command: MetaHackthone > Import Hustle car visuals. Quest APK was not rebuilt or deployed.
# Pinch-only release — 2026-10-18

- Near grabs now require thumb/index proximity: close at 20 mm, release at
  25 mm of hand-scale-normalized joint distance. Curled fingers and provider
  pinch flags cannot initiate or sustain a separated grab. Menu pinches retain
  their own thresholds.
- Removed the 120 ms opening delay. The first separated tracked sample releases
  at the last held pose, without pulling the object to the opening fingers'
  midpoint. Brief tracking loss still has the existing 350 ms grace period;
  prolonged loss cancels without attachment.
- Active Codex Unity MCP readiness passed for the correct project, Unity
  6000.3.21f1, Android build target, and clean NarrowGap scene. Meta CLI and
  active-session Quest discovery passed.
- **15/15 Edit Mode gesture tests and 12/12 targeted Play Mode tests passed**
  through Unity MCP. Jobs: `cdd0ecd523744d1c9f8eab014d5b5d74` and
  `2924c8039ef34119a957f54f8c0d0c79`. Coverage includes both hands, curled-finger
  rejection, stale provider flags, immediate release, physics spheres, compound
  bodies, tracking grace, two-hand attachment, menu pinch/placement, and Narrow
  Gap right-hand attachment and left-hand removal. Console error query was empty.
- Restored the test runner's temporary Enter Play Mode option change; active
  scene remains clean NarrowGap. No APK was built or installed for this change.
  Optical hand-tracking feel at the tighter thresholds still needs headset testing.

# Quest and Pico deployment — 2026-10-18

Both development APKs include the pinch-only release change and were installed
as `com.metahackathon2026.metahackthone`, version 0.1.0 (1), using explicit serials.

| Device | APK | Build result | Install verification |
| --- | --- | --- | --- |
| Quest 3 `2G0YC5ZG5V04ZW` | `Builds/MetaHackthone-Quest.apk` (146,512,901 bytes) | 0 errors, 0 warnings; job `build-42cb50e7d5` | package info confirmed update at 15:12:47 |
| Pico A9210 `PA94Y0MGL7230204G` | `Builds/MetaHackthone-Pico.apk` (147,075,677 bytes) | 0 errors, 6 warnings; job `build-b28336ea4d` | package info confirmed install at 15:23:12 |

Quest startup reached `METAHACKTHONE_READY parts=15 mode=Build` and reported
18 hand sources, 4 rays, 4 pokes and 17 buttons. See
`Logs/QuestPinchOnly.Startup.log`. Controller/haptics OpenXR warnings occurred;
physical pinch interaction was not verified remotely.

Pico launch was requested, but the system intercepted it with a confirmation
to leave the existing VREP application (`showStartAppConfirmDialog [VREP]`).
The new app had no PID at the time of checking. Installation is verified;
Pico rendering, passthrough and hand tracking remain unverified until the
headset confirmation is accepted. See `Logs/Pico.Startup.log`.

Pico uses official PICO OpenXR SDK 1.4.1, checkout
`3aa3e62bff41df618529eeb60ff02c29a515dafe` at
`E:/Unity/Tools/PICO-Unity-OpenXR-SDK`, referenced as a local package. Recreate
that checkout when moving this project to another machine. The build helper
generates a UnityXR ISDK player and substitutes it only in build scenes.
The late Gradle hook removes Quest manifest requirements and the competing
OVRPlugin AAR from Pico builds. Earlier failed attempts are not validation.

To repeat: use `MetaHackthone > Pico > Prepare build`, build Android to a path
ending in `-Pico.apk`, then use `Restore Quest configuration` even if the build
fails. Quest configuration was restored after this deployment; NarrowGap remains
the clean active scene and Android remains the build target. Pico builds warn
about unused controller-prefab scripts, an unset platform-services app ID,
OVRInput profile configuration, and two SDK libraries lacking Android 15 16KB
alignment. This Pico is Android 14; Android 15 compatibility was not tested.

SHA256:
- Quest: `44A7EF6F807AC1F79143A0742A9426C4B1ECA83592EC095F2ABFE6BE156DC51C`
- Pico: `1FEEE6B9E1C5FD2B90E7CC360DF2401405261A82EE101D8E8D43EEEB70E69F8B`


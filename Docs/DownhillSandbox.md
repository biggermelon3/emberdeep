# Downhill Sandbox (KAN-17)

**Current physics lab (2026-10-30):** The saved scene now starts with [Gravity Garage](GravityGarage.md): an eight-socket 2×1×2 core, four-wheel reference cars, adjustable two-descent course, stable-landing scoring and desktop comparison controls. The original course remains selectable in the lab panel. Earlier notes below describe the baseline and historical revisions.

**Current implementation (2026-10-24):** See [Sandbox build modes and verification](SandboxBuildModes.md) for both main-menu choices, weightless assembly, the two-socket core, Ready/Test transition, Retry, Back to Build, and Exit. The text below records earlier revisions; its sixteen-socket core, timed tray return, and single Start/Return control have been superseded.

Open **Downhill** from the Workshop menu or desktop toolbar, or open `Assets/MetaHackthone/Scenes/DownhillSandbox.unity` and press Play. Returning to Workshop preserves each level's construction.

![Two-sided wheels and enlarged live car preview](DownhillTwoSidedPreview.png)

## Build and test

- Build around the anchored red square core. The inventory remains two small wheels, two large wheels, one short block, one triangle and one irregular block.
- Rectangular parts use the shared Workshop face-center sockets. No extra axle or corner sockets are added. The square core has sixteen connections.
- Pinch the teal horizontal rotation wheel beneath the core and move your hand around its rim. The whole connected car rotates horizontally around the fixed core. Its 36 cm rim has a 3 cm clear gap above the tabletop.
- Either hand can rotate the construction while the other holds a loose part. Attachment and detachment pause until the rotation wheel is released. Dropping a part during rotation does not attach it.
- Either hand can pinch a loose part or group, move it near an available socket on the core-connected car, and release to attach. The existing distance, alignment, preview and overlap checks apply to both hands.
- Pinching a connected part lifts that part and its dependent branch. Parts with another path to the red core stay anchored. The core stays fixed, and rejected grabs preserve every connection.
- A fresh pinch by the other hand on any member of a held group transfers the whole group immediately, preserving its pose and connections. The receiving hand must be empty. The previous hand must open and pinch again to take it back; its release cannot drop or attach the transferred group.
- Both flat wheel faces have independent centered hub sockets and matching rotation marks. Both sockets may be connected at once while the tire rotates freely. The face you approach is the face that attaches; a blocked face cannot flip the wheel to its other mount.
- A translucent teal car at **Simulation start** shows the connected construction at two-thirds scale, exactly twice its previous dimensions, including its current heading. Loose parts and socket markers are excluded.
- Press **START** on the table to send the car directly to the previewed position. The car stays frozen for a **3-second countdown**, then gravity and wheel physics begin. The run and stall timers start with physics. Press **RETURN** during the countdown to cancel and restore the build. Finger presses and ray/pinch selection both work.
- Press the same button, now **RETURN**, during a run or after success/failure to restore the exact full-size build and heading. Release held parts and the rotation wheel before switching.
- There is no transfer plate, three-finger mode gesture, width limit or minimum wheel requirement. Players can test any connected design.
- Desktop: click START/RETURN or press Tab; drag the teal rim to rotate; drag a part with its dependent branch and release near a socket to attach. Delete detaches the selected branch.
- A dropped group gets one 10-second return timer. Picking up any member cancels it; releasing again starts a fresh timer. On expiry, internal connections are removed and every member returns to its own original tray. Attached groups and the core are exempt. Hand transfers do not start a timer.

All wheels remain passive. Building dimensions, course geometry, masses, wheel resistance, barrier and bridge gap are unchanged. Meshes, colliders, body offsets, socket/joint anchors and tire radii use the same two-thirds simulation scale. Preview and launch share a geometry-based ramp-clearance calculation. Finish, leaving the course, falling, three seconds stalled, or the existing 30-second attempt limit resolve the run. Return restores the build for another attempt without accumulating scale changes.

## Scene editing and implementation

The saved scene owns the table, controls, course and spawn markers. The worktop remains 70 cm above the tracked floor, with the core centered 90 cm high and 65 cm ahead of the player's initial horizontal head position. See [Scene editing](SceneEditing.md).

`DownhillCourse` holds the table button and rotation-wheel references. `DownhillController` owns rotation and mode transitions. `DownhillSpawnPreview` pools visual meshes and shares the exact sandbox-placement calculation with the physics-copy creation path. Preview geometry has no colliders, rigidbodies, assembly registration or shadows.

`AssemblyWorld` owns atomic hold transfer and branch detachment. Downhill's explicit attachment and transfer policies enable both hands; Workshop and Narrow Gap retain their prior policies. Branch membership is the selected part plus everything that loses core reachability when it is excluded. Only boundary connections are removed, after all guards pass. Holds and return timers track all group members.

Wheels retain the original `mount` identifier and local anchor and add `mount_opposite` on the other face. Preview and release select real sockets. Existing single-mount saves load without a format change, and rectangular grids retain their versioned layouts.

**MetaHackthone > Scene editing > Refresh Downhill two-sided wheels** refreshes only authored wheel preview children and the instruction label. It preserves every spawn marker, table control and course transform. Running it again leaves the scene unchanged.

**MetaHackthone > Scene editing > Upgrade Downhill table controls** upgrades an older authored Downhill scene in place. It removes only the legacy plate group, adds missing controls, persists their meshes/materials and saves the scene. Running it again leaves an upgraded scene unchanged. Legacy scene generation remains unable to overwrite authored layouts.

The known gravity-finish regression predates these controls and remains a separate physics issue. No acceptance thresholds or propulsion behavior were changed.

## Current verification — symmetric hands, two-sided wheels and enlarged car

Unity 6000.3.21f1 compiled without errors. All **112 EditMode tests passed** (`1bb418d56ad9485d94bc87374083b2db`). The broad PhysicalAssembly/CloudGarden PlayMode run passed **112/114** (`03fa952fd3a64b1783cb9d42deb9b27e`). All new interaction and geometry checks passed, including either-hand/desktop branch release-to-attach, alternate core paths, rejected grabs, child-member transfers in both directions, repeated transfers without pose jumps or duplicate ownership, previous-owner release, tracking loss, occupied receiving hands, timer cancellation/expiry, two occupied wheel hubs with a freely rotating tire, single-mount and two-mount saves, exact doubled geometry, launch clearance, preview/launch agreement, and repeated Return restoration. Existing Workshop/Narrow Gap interaction, desktop, save/load and attachment-performance regressions passed.

The two documented physics failures remain separate and their assertions are unchanged: `DownhillTests.SquareCoreReferenceCompletesGravityCourseOnRepeatedAttempts` stopped at `(0.40, 0.87, 0.98)` before finishing; `NarrowGapTests.WideHullPhysicallyHitsStakes` reached Z=`0.910613239` against Z<`0.90`. No masses, resistance settings, course geometry or physics acceptance thresholds were adjusted. Historical wheel comparison fixtures retain their original one-third-scale comparison rigs.

Evidence is saved in `Logs/SandboxHands.EditMode.json`, `Logs/SandboxHands.PlayMode.json` and `Logs/SandboxHands.SceneReview.json`. The authored refresh preserved all 190 retained transforms and all 57 course colliders; only wheel preview children and instruction text changed. Repeating the refresh left the scene file unchanged. The two-wheel car has a complete 37-mesh live preview. The enlarged preview and [matching wheel faces](DownhillWheelFaces.png) were visually reviewed.

Unity is left outside Play mode in the saved Downhill Sandbox scene. `Builds/MetaHackthone-SandboxHands-Quest.apk` was subsequently built successfully (zero errors, four existing SDK/configuration warnings) and installed as an update on Quest 3 `2G0YC5ZG5V04ZW` at `2026-10-22 20:11:15`, retaining app data. Launch reached `METAHACKTHONE_READY` in Workshop without Unity errors or fatal exceptions in the captured log. Open Downhill from Workshop. Physical hand-tracking feel remains a headset playtest item.

## Previous table-control verification

The table-control revision compiled without errors. All 103 EditMode tests passed (`2cbca059c07a46ccb03e1c1841dc9908`), including shared socket search, saved-construction and authored-scene validation.

The broader PhysicalAssembly/CloudGarden PlayMode run passed 100/103 (`94b1c2105f634702a4db0edbe299ef17`). One failure was an obsolete fixture expectation for the removed move-handle collider; the fixture now requires startup to add no scenery colliders. The other two failures are the documented `SquareCoreReferenceCompletesGravityCourseOnRepeatedAttempts` and `NarrowGapTests.WideHullPhysicallyHitsStakes`; their physics and assertions remain unchanged.

After correcting that fixture, all five focused checks passed (`41d37dba67cb43a6a5aa7ec3799b6d17`): authored startup for all three building scenes, actual mouse button/rim input with native poke/ray registration, and recovery after failed simulation reconstruction. The broader run also passed both wheel-face release/attachment paths, two-hand rotation, tracking-loss release, repeated scale/return, full preview-mesh pose matching, shared attachment performance, wheel behavior and level round trips.

Visual review confirmed the horizontal wheel, 3 cm rim clearance, fitting button text, and a complete 21-mesh two-wheel car preview at the sandbox start. Running the scene upgrade again left the scene file unchanged. Unity is left outside Play mode in the saved Downhill Sandbox scene; no APK was built or installed. Physical reach and hand-tracking feel still require a headset playtest.

## Historical verification


Integration onto `/main` changeset 15 on 2026-10-22 passed all 100 edit-mode tests (`bc6c467d0ed244d194b3936782bac4d0`). The 94-test PhysicalAssembly/CloudGarden play run passed 92 (`ed7b69e20a1d4b0280e2e6119f83700d`): the known Downhill gravity-finish test failed, and `NarrowGapTests.WideHullPhysicallyHitsStakes` ended with the core at Z=0.901129 m against its strict Z<0.90 m assertion. The latter reproduced in isolation at Z=0.902634 m (`650df27462ab42ab8e2c7abffa73c436`). Both assertions remain unchanged and unresolved. Main's existing aiming, pinch release, course assets, and Pico package are retained; the pending shared-grid fixture tiles the original Narrow Gap footprint with current 1x2 blocks and cubes. Shadow Workshop branch changes are excluded from this integration.

The integrated Android development build `Builds/MetaHackthone-MainDownhill.apk` succeeded with zero errors and 37 warnings (`build-4098e3ca96`, 150,421,973 bytes). Warnings include the existing Pico SDK's obsolete APIs and unconfigured app ID. This APK has not been installed on a headset. Main's local `Tools/PICO-Unity-OpenXR-SDK` package dependency remains as documented in `Docs/Validation.md`. Pre-migration work is preserved in shelveset 6 and a separate local backup.

Before migration to main on 2026-10-22, the pending work based on changeset 13 passed all 87 edit-mode tests (`ea9b150dbf6c4425a58c36159244c94c`) and 83 of 84 play-mode tests across PhysicalAssembly, CloudGarden and ShadowWorkshop (`b38b4c453e0642ae9e2f852d42dabebc`). The sole failure remains `SquareCoreReferenceCompletesGravityCourseOnRepeatedAttempts`. These results validate the pre-merge workspace; tests must run again after integration with main.

Automated tests cover inventory, passive recessed wheels, timed respawn/regrab, attachment exemptions, plate/hold guards, repeated scale-and-return cycles, a gravity-only finish, comparative coasting and barrier clearance, and direct/round-trip navigation. Existing assembly, gestures, save/load, desktop driving and Narrow Gap tests are also exercised.

On 2026-10-21, Unity 6000.3.21f1 passed all 63 `PhysicalAssembly.EditTests` and all 62 tests across `PhysicalAssembly.PlayTests` and `CloudGarden.PlayTests`. The final desktop preview was inspected with no console errors.

The centered-layout revision passed all 76 edit-mode tests and 79 of 80 play-mode tests. The sole failure is `SquareCoreReferenceCompletesGravityCourseOnRepeatedAttempts`: the reference leaves the road before finishing. Layout/floor/heading placement, gesture directions and safety guards, exact transfer/restore, wheel comparisons, and Narrow Gap regressions pass. The final desktop preview above was visually reviewed. Test job IDs: edit `b9a271cac6134dad9cc0ef16231965fd`, play `eb48e24996534323973f74207feaca6f`.

Physical height, reach and real hand gestures still require an in-headset playtest on Quest 3 `2G0YC5ZG5V04ZW`; editor simulation does not verify human reach or hand-tracking feel.

Floor-height investigation: the user reported that the dark workbench looked about 20 cm above the physical floor. The installed settings and scene scale were verified as 0.70 m and 1:1. Quest logs showed Stage tracking, arena floor Y=0, and a stationary headset Y≈0.50 m when the user said the headset was on the real floor. This is consistent with an offset in the headset's floor reference; physical confirmation after boundary floor calibration is pending. Do not compensate by changing the authored table height to 1.20 m. The placement outline and diagnostics now use Downhill's 0.70 m surface instead of the default Workshop's 0.50 m surface. Logs also include the tracked floor and whether head position is tracked. All six targeted floor/heading/placement tests pass (job `4b9b7a7c07604b3fadb298e8bcf1917f`).

`Builds/MetaHackthone-FloorHeightDiagnostics.apk` built with zero errors/warnings (`build-189b025707`) and was installed on the selected Quest with replacement enabled at `2026-10-22 10:38:56`, retaining app data. The app launches. The initial diagnostic sample reports `headTracked=False`, so stationary/off-head poses alone must not be treated as a precise physical floor measurement. Verification while the user is wearing the headset after floor calibration remains pending.

The subsequent shared-connection revision passed all 87 edit-mode tests (`b639a028892b4eff95a6399fe861bd16`). The 83-test play run passed 81 checks; its container construction fixture still targeted removed corner points, and the known gravity-finish test failed. After aligning the fixture to centered sections, both container physics tests passed (`870391b7c6ff45c090dde40b5ae90149`). No production physics or acceptance thresholds changed. The remaining failure is the gravity-only reference finish. Downhill's visible points were reviewed, and the unit-cube six-point rule is tested at full size and one-third scale. Old grid versions retain their original socket identities and saved connections.

This revision is installed on Quest 3 `2G0YC5ZG5V04ZW`: `Builds/MetaHackthone-SharedConnections.apk` (116,728,986 bytes), built with zero errors/warnings, build job `build-0bcdce489e`. Replacement installation retained app data and the original first-install date; update time is `2026-10-22 10:14:22`. Launch reached `METAHACKTHONE_READY` with no Unity exceptions or fatal errors in `Logs/SharedConnectionsQuestLaunch.log`.

On 2026-10-22, `Builds/MetaHackthone-CenteredDownhill.apk` built successfully for Android with all seven enabled scenes, zero errors and zero warnings (build job `build-5854dc647f`). The 122,073,886-byte APK was installed with replacement enabled on the selected Quest 3 and launched successfully. The original first-install time (`2026-10-16 02:16:36`) and app data directory were retained; update time is `2026-10-22 01:33:34`. Launch logs show `METAHACKTHONE_READY` in Workshop with no Unity exceptions or Android fatal errors in the captured sample (`Logs/DownhillCenteredQuestLaunch.log`). Open Downhill from Workshop for the pending physical review. This is a layout/gesture review build with the gravity-finish failure described above.

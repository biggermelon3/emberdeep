# Gravity Garage sandbox

## Waterfall Valley — campaign Level 2 (2026-11-03)

The sandbox's second course uses `GravityGarage3D_WaterfallValley.asset` through
`DownhillSettings.Level3D`. Level 1 and its saved construction carry forward;
the numeric campaign ID remains `TeleportPipes` for session compatibility.
`Level3D` takes precedence over `Level25D`. The 2.5D assets and JSON schema remain
available to the legacy editor.

The 2.4-design-metre timber deck is continuous: downhill launch, upper balance
rise, descending right-hand connection area, leftward return lane, drill wall, finish.
Render and collision use the same mesh. The passive wheels, part masses, geometry
versions, and face-center sockets are unchanged. The upper and lower roads use
full wheel and collision physics. The connection area guarantees a visible transfer,
preserving each body's incoming motion in the exit frame while puzzle time is paused.
See [the authoring guide](DesignModeGuide.md) for transfer and gate settings.
The physics fixed step remains `sqrt(worldScale) / 240`, independent of playback rate.

The yellow indicator follows the mass-weighted center of the connected build.
For the reference construction, place the core at the front, the long block
behind it, and the short block at the rear. Mount large wheels at the front of
the long block and small wheels at the back of the short block; mount the drill
on the core's forward face. These placements are checked with the normal overlap
and socket rules. Moving the same blocks rearward produced a slower completion
in calibration (13.1 versus 11.4 design-seconds), without changing masses or
forcing a failure.

The drill must remain connected and sweep through the visible yellow band on
the wall. Missing, detached, high, low, and sideways misses cannot break it.
Completion requires ordered course traversal, a broken wall, forward alignment,
an upright vehicle, and at least two wheels supported by the road. Natural rollback
remains allowed. Water entry, overturning, skipped sections, and a 45-design-second
puzzle timeout report failures; connection travel does not consume that time. Retry restores the wall, contacts, drill sampling, and run
telemetry; Back to Build restores the original kit and connections.

The environment is decorative: combined timber, rock, grass and conifer meshes,
distant peaks, and a connected source-pool / spillway / falling curtain / plunge
pool / stream / river. Water flow uses a URP stereo-compatible shader and bounded
foam/spray. Desktop testing switches to an oblique course view and restores the
building view on return; headset tracking is unchanged.


### Validation of Waterfall Valley

- Nine new Play Mode tests passed, including 18 successful completions (two at
  each combination of 0.4/0.8/1.0 build scale and 0.5x/1x/3x playback).
- Zero-gravity launch, rollback, overturning, wrong drill heights, detached
  drills, water contact, unsupported shortcuts, course projection, retry state,
  campaign carryover, and three build/retry/return cycles passed.
- The reference car also completed twice in the saved DownhillSandbox scene at
  normal playback, in 14.29 and 12.85 design-seconds, with seven connections.
  Campaign completion and restoration of the nine-part editable kit were checked.
- Final player-view captures are in `Logs/WaterfallValley`; the assigned course
  thumbnail is `Assets/MetaHackthone/Levels/WaterfallValleyPreview.png`.
- Broader regression run: 108/111 older selected Play Mode tests passed, including
  the legacy 2.5D scale/rate matrix and building, socket, and retry coverage.
  Three older puzzle-physics tests failed: `CompareMiniaturePlanar`,
  `MixedWheelCarCompletesRepeatedRuns`, and `OnlyFrontLargeCompletesPuzzle`.
  The repeated-run failure also reproduced when run separately. Their legacy
  course geometry and part physics were not retuned as part of this change.
- Edit Mode: 152/153 passed. The saved DownhillSandbox layout has an existing null
  `Surface` reference, which fails `BuildingSceneAssetTests` at line 20. The saved
  scene was not edited to mask this failure.

## Gravity-driven Sandbox start (2026-11-02)

The first Sandbox course now starts on a 14.5-degree downhill ramp, extending
from design x=0.18 to x=2.70 with a 0.65 height drop. Both axles of the tested
compact and lengthwise builds start on this ramp instead of straddling a flat
shelf and the main descent. Release pitch accounts for wheel radii and axle
heights; the road collider's top now matches the surface used for placement.
The saved scene and course preview include the new ramp.

The old start was reproduced with wheels turned 180 degrees around their mounts:
the lengthwise car briefly rolled uphill (minimum -0.0595 design m/s) and advanced
only 0.114 design metres in 1.5 design seconds. The compact layout did not fail,
so this was a footprint-dependent start problem, not a wheel motor reversing.
The launch already used zero initial velocity; the ramp now supplies the starting
force through gravity. No forward impulse, wheel motor, or direction clamp was
added. Teleport transport behavior is unchanged.

Sandbox regression coverage includes both wheel-mount orientations, both
front/rear size arrangements, short and long layouts, a zero-gravity/no-drive
check, and completion of both campaign courses. Reproduction evidence is saved
under `Logs/GravityStart`. Only Sandbox behavior was tested for this change.

Small and large wheels can also mount directly to the core's existing face-center
sockets, using either nearby grabs or ray targeting. Occupancy, overlap checks,
and the fixed core still apply. All 43 targeted Sandbox Play Mode tests passed
before publication, including the gravity start and direct core-wheel attachment
regressions. The APK uses the latest saved Sandbox scene layout.

## Gravity Garage 2.5D level editor

Open **MetaHackthone > Gravity Garage 2.5D Level Editor**. This dockable English-language window follows `SandBoxDesign/Gravity_Garage_25D.html`, with a wooden two-track diorama, vehicle presets, parameter panels, camera drag/zoom, five journey stages, and live speed/time/progress telemetry.

Level assets live in `Assets/MetaHackthone/Levels`:

- **GravityGarage25D_Default** preserves every HTML default, including the unsnapped 1.35 m gap.
- **GravityGarage25D_Challenge** applies only the HTML Challenge changes, including its 1.65 m gap.
- **GravityGarage25D_UnityCalibrated** tunes the course for the existing Big front car (0.37 m front / 0.20 m rear wheels): 2.40 m upper drop, 1.40 m lower drop, 0.40 m gap, Rock A at 12 m, and 0.02 m radii for both obstacles. The car's blocks, mass, sockets, and wheel sizes are unchanged.

The HTML uses a simplified wheel-support solver. Unity uses actual rigid bodies, hinges, contacts, and passive rolling resistance. The original parameter defaults remain available for reference. The calibrated course demonstrates the complete journey with the existing mixed-wheel car, without a motor, scripted jump, or forced preset outcome. The deliberate pipe transfer is the only transport exception.

The Unity course now has a 2.40 m wide deck (previously 1.62 m), a smooth 0.09 m launch-lip rise (previously 0.32 m), and a 0.15 m drop from takeoff to landing (previously 0.39 m). These geometry changes apply to all 2.5D levels and give the low rear axle clearance and landing margin. The 13 parameter names/defaults are retained; obstacle minima are extended to 0.02 m and the gap minimum to 0.20 m for Unity tuning.

**Start / Restart test** automatically enters a dedicated Play Mode preview scene. **Pause / Resume** freezes the vehicle and its simulation timer; playback speed changes preserve physics step precision. **Reset vehicle** retains the draft, camera, and playback speed. Stop the test to edit physical parameters or save. The previous Play Mode start scene and clock settings are restored on exit. The editor does not take over an unrelated Play session.

Use **New**, **Open**, **Save**, and **Save as** for reusable Unity assets. Unsaved replacements prompt Save / Discard / Cancel, and edits support Undo / Redo. **Import JSON** validates the schema, required fields, finite values, and ranges before replacing the draft. **Export JSON** uses the original HTML parameter names inside a version-1 envelope. Camera changes remain available during tests and never reset the run. **Restore view** changes only camera angle and zoom; **Restore all parameters** preserves playback speed.

**Apply to sandbox** saves the draft and assigns its asset to the sandbox entry's `DownhillSettings.Level25D` (`Generated/GravityGarageSettings.asset` in the saved scene). The next sandbox run builds that level using the same course builder, release pose, vehicle geometry, and run controller as the preview. Saving a level also saves its thumbnail for the sandbox's course card. Opening the editor alone does not change the sandbox's selected course. The tuned UnityCalibrated level is now assigned to the sandbox. Clear `Level25D` to use the existing changeset-34 course.

| HTML parameter | Default | Range | Slider step |
| --- | ---: | --- | ---: |
| `frontWheel` | 0.37 | 0.16–0.44 m | 0.01 |
| `rearWheel` | 0.20 | 0.16–0.44 m | 0.01 |
| `upperDrop` | 2.0 | 0.8–2.8 m | 0.1 |
| `rockPos` | 6.4 | 2.8–15.2 m | 0.1 |
| `rockSize` | 0.21 | 0.02–0.40 m | 0.01 |
| `gap` | 1.35 | 0.2–2.6 m | 0.1 |
| `lowerDrop` | 1.20 | 0.4–1.7 m | 0.1 |
| `rock2Enabled` | true | boolean | — |
| `rock2Pos` | 10.8 | 2.0–15.9 m | 0.1 |
| `rock2Size` | 0.12 | 0.02–0.33 m | 0.01 |
| `cameraAngle` | 18 | −34–34° | 1 |
| `zoom` | 0.94 | 0.72–1.23 | 0.01 |
| `showLabels` | true | boolean | — |

Geometry version 9 adds serialized per-wheel radii. Versions 0–8, the changeset-34 wheel mounts, and format-4 attachment ancestry remain compatible. The new car retains the shared face-center grid and the core's eight horizontal sockets. Its three-unit wheelbase represents 0.98 design meters, leaving clearance between wheels throughout the radius range. Tires use real generated colliders and mass proportional to radius squared.

Track meshes use the HTML course structure and gap boundaries with the gentler launch and landing geometry described above; decorative plank seams have no colliders. Rock A snaps out of the gap. The runner requires an upright supported upper landing before admitting the vehicle to the pipe, hides and freezes the whole connected vehicle for 1.08 design seconds, then releases it at lower x=2.16 with `max(1.1, entrySpeed × 0.52)`. A grounded stable crossing of lower x=16.8 completes the level. Airborne crossings cannot finish. Falls, inversion, retreat, and a 29-second timeout report failure.

This editor is a desktop authoring workflow. Headset deployment and device performance profiling are separate tasks.

Current-car course tuning validation:

- All ten 2.5D Edit Mode tests and all eight 2.5D Play Mode tests passed.
- The unchanged 0.37/0.20 m mixed-wheel car completed 18 consecutive runs: two runs at each combination of 0.4/0.8/1.0 build scale and 0.5×/1×/3× playback. Finish times were 13.15–15.04 design seconds, with valid upper landings, exactly one pipe transfer, and all six connections intact.
- Three additional pipe-state runs passed; the sandbox's initial transfer and retry both completed. Falling beyond the goal and an unsupported wide-gap crossing still cannot succeed.
- The actual saved `DownhillSandbox` scene loaded the assigned asset and its unchanged seven-part car, then completed at normal 1× playback in 12.95 design seconds with one pipe transfer and all six connections intact. No console errors were reported during this check.

Initial editor implementation validation against changeset 34 (before the tuning above):

- All 144 assembly Edit Mode tests passed. The nine new editor/data tests also passed after the final JSON type-validation change.
- All seven new Play Mode tests passed, including repeated pipe transfers at 0.5×, 1×, and 3×, sandbox transfer/retry, pause/reset, and invalid-finish rejection.
- The combined new/legacy run passed 18/20 tests. Two legacy repeated-run checks intermittently stalled after landing; both passed on an isolated rerun (20 mixed-wheel runs and ten four-preset comparisons). Legacy physics tuning was retained.
- The actual editor Start/Stop workflow completed the calibrated course in about 12.4 design seconds and restored the prior scene, clock, and Play Mode start-scene setting. An unsaved camera edit survived script reload. Wide/narrow layouts and generated-resource survival through Unity's unload-unused pass were checked visually and through editor queries.
- After repeated Play Mode/script reload checks, the Meta XR MRUK package logged "Global context has already been created" and "Failed to create global context" from its runtime initialization hook. The editor preview remained functional; this SDK lifecycle issue remains unresolved. No headset validation was performed.

## Existing sandbox course

Open **MetaHackthone > Design Mode > Level 1 - Gravity Garage**, or open `Assets/MetaHackthone/Scenes/DownhillSandbox.unity`. The saved scene contains the Gravity Garage track, visible before Play. It replaces the old downhill map in place and retains the scene GUID. The original-course switch has been removed.

The right-hand desktop panel contains course and comparison controls. **Focus course** frames the track; **Overview** returns to the whole workbench.

## Wheel puzzle

The default car has two large front wheels and two small rear wheels. Geometry version 8 uses large/small radii of **0.46/0.26 design metres**, with diameters of 0.92/0.52: large wheels are approximately 77% greater in diameter. Both mounting faces connect at the axle center, without a drop bracket.

The default puzzle targets large-front/small-rear as the only successful configuration among the four reference presets. Large wheels use passive rolling resistance 3.0; small wheels use 0.003. This deliberate balancing difference from the HTML makes four large wheels lose more speed than the mixed pair. Resistance only dissipates energy during tire contact. There is no engine, launch boost, or wheel-configuration check in scoring. Custom vehicles and course edits can change which builds succeed.

Success requires crossing the bridge edge, supported tire contacts on the landing, and more than 0.52 design seconds of continuous upright, low-angular-speed support beyond any landing obstacle. Crossing the finish in mid-air is insufficient. Pitch and roll are measured relative to the course axes.

## Building interactions

The circular grid pad and FRONT arrow follow the core position and heading during building. Launching aligns the car with the course.

Removing a parent carries its dependent parts. Children can then be removed from the released group, or with the other hand while holding the parent. Grabbing a held group's root transfers the whole group. The attachment graph records parent relationships and attachment order while physical connections remain authoritative.

## Design controls

- Four reference vehicles: large front / small rear, small front / large rear, four large, and four small. Each uses the same eight-socket core and two axle blocks joined through standard building sockets.
- Start/restart, back to build, pause/resume, and 0.5x-4x playback.
- First drop, rock A size/position, second descent position/drop, optional rock B, gap, landing drop, lip rise, and release position. Rocks can also be dragged along supported track surfaces in Build mode.
- **Compare four** runs all presets sequentially and restores the previous construction, detached parts, and selection. Other build/run controls are locked during comparison. Individual preset switches reset the construction.
- Constrained-plane calibration locks sideways movement and out-of-plane rotation. Leave it off for 3D gameplay.
- CSV export includes outcomes, time, takeoff speed, maximum pitch/roll, landing attitude, physical scale, and exact course parameters. Files go to Unity's `Application.persistentDataPath`; the panel displays the filename.

Runtime controls use a cloned settings asset, so slider edits do not overwrite the saved profile.

## Reference and physics

Reference: `G:/UnityProject/Tmp/Gravity_Garage_2D_物理测试器_v2.html`. Its two descents, rocks, gap, and lower landing are represented as colliders. Rock cross sections span both wheel tracks.

**HTML defaults** restores the original 1.20 m gap and other course dimensions. The initial **Unity calibration** profile uses a 0.80 m gap. The Unity chassis is longer/taller than the HTML rectangle, has four wheels and joints, and uses a 3D solver with different losses. The wheel radii and passive resistance above are tuned for this Unity profile.

The 1.04 m reference wheelbase maps to three shared building units. Wheel assembly mass scales with radius squared: 20% belongs to the hub and 80% to the rotating tire. Unity computes inertia from collider geometry.

The build table retains its authored size. Garage transfer normalizes the reference core to a common simulation size, preserving relative part positions and dimensions, and scales the course with it. Physics steps are 1/240 of a design second, converted by the square root of world scale. Playback changes the clock rather than step precision. Displayed time and speed use design units.

## Save compatibility

New constructions use geometry version 8. Existing saved constructions retain their explicit geometry version and scale, including earlier wheel sizes, axle offsets, and resistance. Format-4 saves preserve attachment ancestry and order; format-3 saves remain readable. The existing save filename is retained for compatibility.

## Validation

Validated in Unity 6000.3.21f1 on 2026-10-30:

- All 135 PhysicalAssembly Edit Mode tests passed.
- All 13 Gravity Garage Play Mode tests passed. The mixed car completed 20 repeated runs across build scales 0.4 and 1.0. Ten complete four-preset comparisons at those scales each produced only BigFront as the winner.
- Coverage includes centered sockets, passive wheels, radius-dependent mass, 3D/planar comparisons, construction restoration, detached groups and two-hand splitting, core-following pad, versioned save round trips, and rejection of unsupported finishes.
- Saved-scene Design Mode at normal 1x playback: BigFront succeeded; SmallFront and BothSmall stalled; BothBig failed its landing. No console errors were reported.
- The broader Downhill suite initially passed 49/54. Three pad-location tests were updated and passed on rerun. Two existing failures remain: runtime spawn-scale edits do not respawn parts, and the historical two-layout cycle test expects the retired OnSandbox mode.

Evidence and calibration logs remain under ignored `Logs/`. No headset build or worn-headset test was performed. The high-frequency solver and 96/32 iteration settings still need device profiling. Contact ordering and floating-point differences can affect borderline outcomes.

## Design Mode Level 2 preview

Use MetaHackthone > Design Mode > Level 2 - Waterfall Valley to enter Play Mode directly in the new course. The in-game Design Mode menu also includes Preview Level 2 - Waterfall Valley. Changing sandbox courses from that menu resets the temporary sandbox construction; campaign progress and saved level assets are unchanged. Stop Play Mode to return to editing.

Verified the direct preview, legacy sandbox return, repeated selection, and normal campaign restart with the Level2PreviewSwitchesCoursesWithoutCampaignProgress Play Mode test.


## Level 1 valley art

Level 1 shares Level 2's conifers, grassy rock terraces, mountains, flowing waterfall and river. Timber seams, beams and trestles follow each existing ramp and stop at the jump. The scenery has no colliders; road dimensions, obstacle meshes, friction and course rules are unchanged. Its foreground is shortened to keep the building area clear. Overview framing still uses the gameplay course bounds.


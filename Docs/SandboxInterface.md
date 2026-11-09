## Polished campaign palm menu and rewards — 2026-11-05

The polished sandbox uses a left-palm menu. Hold three extended fingers with the
palm roughly upward (up-dot at least 0.45) for 0.35 seconds. Opening accepts a palm
up to 75 cm below the head and preserves dwell through interruptions up to 0.15
seconds; invalid samples never add dwell. Once open, curling fingers does not
close it. Turn the palm down (up-dot at most -0.2) for 0.2 seconds, or lower it more
than 95 cm below the head for 0.3 seconds, to close. A downward closure permits a
fresh upward dwell immediately. Successful menu actions require leaving the opening
pose before reopening. Tracking loss freezes the panel and blocks pressing for up
to 0.6 seconds, after which it closes. Grabbing, repositioning, and system gestures
suppress the menu. `SandboxPalmMenu` exposes these values for tuning.

Successful polished campaign runs freeze the simulation and return only the winning
vehicle's parts separately to their original build spawns. All of their building
connections, including core connections, are removed. Loose parts keep their poses.
The default sequence is 0.25 seconds decomposition, 0.9 seconds travel, and 0.35
seconds reconstruction. Retry, Start, Reset, Back, and Next Level cannot interrupt
it; Quit can. Build mode returns automatically and retains the success message.

Level 1 then reveals a mesh-only drill preview at the course goal. Yellow particles
notify the tracked left palm, followed by a pulsing halo. Opening the menu redirects
the preview to the right of Next Level over 0.45 seconds and pulses that button.
Closing reverses from the current pose and resumes the reminder at the goal.
Tracking loss defers palm delivery. Only successful Next Level selection claims the
reward and activates the real drill in its existing level-2 slot. Final-level wins
return the car without another reward. Completion and claim state remain session-only.

`SandboxRewardCoordinator` owns reveal, delivery, reminder, preview travel, and claim
states. `SandboxReturnEffect` accepts distinct simulation/build parts, including
their different meshes and scales. All return and reminder fragments share the
`SandboxFragments` pool and a 512-particle limit; no physics debris is generated.
Other courses retain their existing success/return flow.

For a development APK, cold-launch with Android intent boolean
`metahackthone.victory_test=true` to run the isolated device acceptance sequence.
It explicitly injects success and palm samples, reports frame times and the particle
budget, checks stereo rendering availability, and leaves the reward ready for a
physical-hand review. Comfortable opening, right-finger touch, both-eye appearance,
and repeated down/up reopening still require a wearer to confirm.

## Earlier sandbox behavior — 2026-10-29

Workbench Build is the only player-facing sandbox option. Turning the wheel rotates the workbench assembly for inspection and editing. The course preview and test vehicle retain the course start orientation. Back to Build restores the workbench view. Legacy mode APIs remain pending removal; historical two-mode reports below describe earlier versions.

# Fixed sandbox interface

The reference layout in `G:/UnityProject/RefPicture` is implemented in the existing
`DownhillSandbox` scene for Workbench Build and Build on Sandbox.

The left panel shows **Start / Reset / Quit** while building and **Retry / Back to
Build / Quit** while testing. Both sets occupy exactly the same positions. Reset
disconnects the construction and restores all authored parts and their
heading; Back to Build preserves the construction. Quit restores simulation/time
state and returns to MainMenu, including on Android. Start and Reset reject actions
while holding, rotating, attaching, or relocating the arena.

`SandboxInterface` owns the stationary UI root, action groups, course card,
countdown, and status anchors. Its root is separate from the animated course and
the tracked camera. Head translation, rotation, FOV, hover, and test/return never
write UI transforms. Only initial placement, an explicit arena relocation, or a
build-mode change relocates the interface. Course approach remains enabled.

## Editing

Stop Play mode and open DownhillSandbox. Under the course, expand **Stationary
sandbox UI**. Move/rotate **Left action panel**, **Current course card**, **Fixed
countdown anchor**, or **Fixed status anchor** and save. These child transforms are
authoritative. Use `WorkbenchOffset` / `SandboxOffset` on `SandboxInterface` to
adjust the complete interface relative to each mode's assembly anchor; the root
itself is placed from those offsets at initialization. Keep Build/Test action
groups aligned so state changes do not move the buttons.

The card's number, title, and preview texture are serialized on `SandboxInterface`.
There is one course, so the card has no swipe or arrow navigation. The thumbnail
is rendered from the actual authored course into
`Assets/MetaHackthone/Art/UI/SandboxCoursePreview.png`. The course geometry, kit
sizes, sockets, gravity rules, and player rig remain unchanged. Workbench Build
has a cyan grid and FRONT marking; Build on Sandbox remains table-free.

The centered grid is now circular, with grid lines clipped to its edge. It sits
9 cm above the worktop, with the rotation wheel centered underneath and a 3 cm
gap between its rim and the pad. The exposed rim remains grabbable. Build on
Sandbox keeps its side wheel clear of the sloped course because it has no table.

Circular-pad verification: all eight targeted Play Mode checks passed, including
mouse rotation, both hands, and head-motion stability. Geometry/persistence and
repeat-upgrade checks passed. Four of five broader scene/layout Edit Mode checks
passed; the remaining check flags the eighth spawn marker removed in the user's
current scene. That existing edit was preserved for this pad-only update.

**MetaHackthone > Downhill > Upgrade fixed sandbox interface** upgrades an older
scene once, persists generated resources, captures the thumbnail, and saves.
Repeating it on an upgraded scene makes no changes. Runtime fallback construction
uses the same presentation and input setup. The original floating Start component
name remains for serialized compatibility; it now styles all action tiles and
only updates color feedback, never poses.

Verification includes both-mode UI-pose invariance, full kit reset, native poke,
existing native ray/pinch and desktop input, ten test/retry/back cycles, and
gravity-course regressions. Worn-headset reach, readability, and tracking comfort
still require a device playtest. No APK deployment is part of this UI change.

## Verification — 2026-10-27

- All 54 sandbox Play Mode checks passed, including the three new UI/reset tests.
- The combined 66-test sandbox/desktop/navigation run passed 65 tests and exposed
  desktop toolbar overlap with the rotation wheel in a small Game view. The
  sandbox toolbar was compacted; all five desktop tests then passed on rerun.
  All seven navigation checks passed in the combined run.
- All 124 PhysicalAssembly Edit Mode checks passed after repairing a pre-existing
  null eighth spawn marker. The missing irregular-part marker now uses its
  default pose and size. The seven existing marker transforms were preserved,
  and runtime spawning also tolerates a missing marker.
- Repeating the scene upgrade leaves the scene file identical. Existing collider
  records were unchanged; no existing scene objects were removed. The original
  settings asset is byte-for-byte unchanged.
- Both runtime build layouts and the countdown/test panel were visually reviewed;
  the course card clears the rotation wheel. The console reported no errors.

Evidence: `Logs/SandboxUI/` contains test-result JSON and runtime screenshots.
See [Workbench](../Logs/SandboxUI/Workbench-Runtime.png),
[Build on Sandbox](../Logs/SandboxUI/OnSandbox-Runtime.png), and
[test countdown](../Logs/SandboxUI/Test-Runtime.png).

## Seven-part scene and Quest rendering repair — 2026-10-28

The scene intentionally keeps seven parts. The empty irregular-block slot stays
empty at runtime, across build modes and after Reset. Empty optional slots retain
their indices so removing one does not change the kinds or poses of later parts.
This supersedes the eighth-marker restoration described in the historical report.

Android builds now reject a non-Android active Editor target before URP collects
pipeline assets. The clean build validates Mobile_RPAsset and retained Lit and
Unlit drawing variants; see the build commands in README.

Development APKs include an opt-in device smoke test. Cold-launch the app with
Android intent boolean `metahackthone.sandbox_smoke_test=true`. It opens both
Sandbox modes, verifies seven parts, moves a loose part, attaches two wheels,
presses Start/Back to Build/Reset, and returns to the main menu. Logcat messages
begin with `QUEST_SANDBOX_SMOKE`; `CAPTURE` stages pause ten seconds for Quest CLI
stereo screenshots. The final result is `PASS` or `FAIL`. This exercises gameplay
and actual button callbacks; physical hand tracking and worn-headset comfort
still require a person. Normal launches and non-development builds do not run it.

After installing the development APK, run from the project root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Test-QuestSandbox.ps1 -Serial <quest-serial>
```

The runner captures each stage to `Logs/QuestSandbox`, saves the process-specific
Unity log and result JSON, and restores the proximity sensor after the test.
The headset must be in a lit area with its tracking cameras uncovered; a system
tracking-loss dialog pauses the app and prevents this test from running.

Validation on 2026-10-28: 128 Edit Mode checks passed; 121 of 122 Play Mode checks
passed in the full run, and the remaining Narrow Gap check passed after its
assertion was corrected to require actual stake contact and failure to clear the
stake's far face. The Android build retained 192 Lit/ForwardLit variants and 8
Unlit variants and was installed on Quest 3. The automated Quest run was blocked
by the system tracking-loss dialog; the user subsequently confirmed the build
works on the headset and authorized publication. This is user-confirmed device
acceptance, not a completed automated Quest smoke test.

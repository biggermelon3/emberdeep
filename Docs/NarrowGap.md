# Narrow Gap physics challenge

Open **Narrow Gap** from the workshop menu, or use **MetaHackthone > Narrow Gap > Play level** in Unity. The standalone scene is `Assets/MetaHackthone/Scenes/NarrowGap.unity` and is included in Build Settings.

Start in Launch mode with a gold core docked on a cube-sized pad. Build a thin container around that core using the loose kit (16 cuboids, 12 cubes, 4 beams). Load the five orange physics spheres after docking. All five must cross the opening and settle together on the landing deck for half a second to win; flight otherwise ends after eight seconds.

- To enter Building, hold your thumb extended and other fingers curled for two seconds, then extend index and middle in either order while keeping the thumb extended. Either free hand can perform the complete sequence.
- To return to Launch, hold thumb, index, and middle extended for two seconds, then close those three fingers in any order. Keep ring and pinky curled during both sequences. Leave the arming pose before starting another sequence.
- In Building, the core floats at least 35 cm above the pad; construction parts have no gravity and strong movement/rotation damping. The right hand attaches loose parts to the core-connected structure. The left hand removes exposed outer parts only when the rest stays connected. The core cannot be removed.
- A laser previews the lowest exposed downward socket inside the core footprint. Docking aligns it with the pad centre without rotating the structure. A blocked destination keeps you in Building.
- Load balls with either hand in Launch mode. Returning to Building places balls back on their tray. The Build / Dock button provides an alternative to gestures, including on desktop.
- Green finger rings and rising piano notes mark the three opening actions. Four descending notes mark the return sequence. The register drops two semitones per building cycle (overall MIDI 52–79). Rings use pooled geometry and sparkle effects; sustained application FPS below 80 for two seconds disables sparkles for the remainder of the session.
- Each successful Building entry spends one of ten cycles. The tenth build and launch are allowed to finish; its result remains visible for five seconds before the demo resets. Cancelled gestures spend nothing.
- To aim in Launch mode, extend index and thumb with the other three fingers curled for one continuous second. Either hand works; the first to finish owns aiming (left wins a tie). Point the index to steer, then fold the thumb or bring it to the index to fire once. Other pose changes, tracking loss, system gestures, or disabled input cancel and restore the setup. Open the thumb/index pinch before interacting again.
- Aiming rotates the upper pad, connected construction, and supported balls around the fixed dock. Horizontal aiming is unrestricted; elevation is 5–80 degrees, smoothed over 60 ms without wrist roll. Blocked rotations retain the last clear direction. Grabs, menus, and Build/Dock gestures are reserved while arming or aiming.
- The cream 4 mm ballistic guide is visible only while aiming. It starts at the core center, uses the actual launch velocity and gravity, and stops at the first course collision or two seconds. Opacity stays full through half its displayed arc length and fades to zero at the end.
- **Launch** or **Space** still fires in the default direction when aiming is inactive. Release held parts first. Launch speed is unchanged.
- Press **Retry**, or **R**, after launching to restore the setup from before aiming with zero velocity. Parts and connections remain intact in Launch mode. Retry spends no building cycle; enter Building again to edit. Retry is unavailable before a launch and during the final automatic-reset countdown.
- Press **Reset Level** to restore the gold core, starting kit, five balls, ten cycles, and original musical register. It preserves the course position. Release held parts and finish any attachment or arena move first. The car workshop is unaffected.
- Construction and arena movement are locked during an attempt and until Retry or Reset Level.
- **Workshop** returns to the original car workspace. Both constructions survive level switching during the current app session; an inactive challenge pauses. Challenge progress is not saved across app restarts.

Tune `Assets/MetaHackthone/Generated/NarrowGapSettings.asset`. Defaults: 29 cm opening, gate 0.9 m from the pad, target 1.8 m away, and 0.8 s nominal flight time. Velocity is derived from target distance, flight time, and gravity. Edit the saved scene directly outside Play mode: the launch pad, gate, landing platform, controls and part spawn markers are visible in the Hierarchy. Move their named groups to keep their gameplay markers together; adjust the Valid opening and Landing region when changing obstacle dimensions. The old generator refuses to overwrite an authored scene. See [Scene editing](SceneEditing.md). Runtime timing and scoring parameters are read directly from the settings asset.

## Gun aiming validation — 2026-10-18

Unity 6000.3.21f1: 74/74 PhysicalAssembly EditMode tests, 48/48 workshop PlayMode tests, and 5/5 cross-level navigation tests passed. Coverage includes one-second holds, ownership and cancellation, thumb-fold/joint-distance firing, pinch consumption, fixed docking pivot, supported balls, preserved joints, blocked rotation, elevation limits, matching preview/launch velocity, warmup interruption, Retry, Reset Level, Building, rotated arenas, and leaving/re-entering while aiming. Existing pinch-only release and menu regressions passed. Results are in `Logs/GunAim.EditMode.json`, `Logs/GunAim.PlayMode.json`, and `Logs/GunAim.Navigation.json`.

The saved course prefab contains the independent upper pad and reusable trajectory line. Editor renders were inspected: [aiming in the course](NarrowGapAimPreview.png) and [isolated arc-length fade](NarrowGapAimFade.png). No headset was selected or deployed to for this change. Optical gun-pose recognition, comfort, and on-device visual/performance validation remain pending; editor tests do not establish those results.

After the navigation restoration fix, the combined 25/25 Narrow Gap and navigation tests passed (`Logs/GunAim.FinalIntegration.json`). Exiting the manual preview emitted Meta Interaction SDK `UniqueIdentifier.Release`/`Interactor.OnDestroy` teardown exceptions; the subsequent integration run passed. This change does not modify that SDK lifecycle code.

## Core challenge validation — 2026-10-16

**82/82 Edit Mode and 52/52 Play Mode tests passed.** Results: `Logs/CoreChallenge.EditMode.json` and `Logs/CoreChallenge.PlayMode.json`. Coverage includes gesture order/cancellation/ownership, floating physics, lowest-socket docking, obstacle rejection, right-hand release attachment, safe left-hand removal, ten-cycle restart, mesh fallback, five-ball delivery, wide-hull collisions, Retry, reset, and cross-level navigation.

The dock restrains every connected body while balls are loaded. Launch releases that restraint and briefly ignores pad contact while the load clears its edge. Connected construction parts do not collide internally; balls and detached parts retain normal collision. This avoids sagging and edge catches in long, narrow constructions. Construction uses speculative collision during flight; balls use continuous dynamic collision.

## Earlier versions

Validation on 2026-10-14: **49/49 EditMode and 33/33 PlayMode tests passed**. Coverage includes a physically connected container delivering all five spheres, wide-container stake collisions, swept gate crossing, reset identity/connection preservation, edit locks, rotated arenas, desktop controls, and workshop/challenge round trips with one shared player rig. Results are in `Logs/NarrowGap.EditMode.json` and `Logs/NarrowGap.PlayMode.json`.

Reset Level follow-up: both new reset tests and all five navigation tests pass, including the Meta poke callback, fresh-kit counts, removal of old connections, a moved course, lost-ball recovery, cancellation before the launch physics step, and preserving the car workspace. The combined 13-test run passed 12 tests; the existing reference-container flight test failed intermittently and passed in its isolated rerun. The physics fixture now unloads leftover gameplay scenes, and the wide-hull check allows balls to continue flying after the hull hits a stake, consistent with the ball-based win rule. Results: `Logs/NarrowGap.ResetLevel.PlayMode.json` and `Logs/NarrowGap.ResetLevel.ReferenceRetry.json`.

The final editor render is [NarrowGapPreview.png](NarrowGapPreview.png). The APK has been installed on Quest 3 and remote synthetic-hand checks passed for loading the level, sphere pickup, launch, completion, retry, and navigation. See [Quest 3 testing](Quest3Testing.md) for evidence and remaining optical hand tracking, visual, container-building, and performance checks.

# Editing the building scenes

Start with the [designer walkthrough](DesignModeGuide.md) for the edit, test, reset, and save workflow, screenshots, controls, and troubleshooting.

## Sandbox table and part sizes

Open `DownhillSandbox` and stop Play mode before editing.

- Select the `BuildingSceneLayout` object and click **Select / frame table (scale this group)**. This selects `Workbench visuals`; change its Transform scale to resize the complete table, including its colliders. X/Z change its footprint; Y also changes its height. Floating parts and controls have separate anchors and do not automatically move onto a resized tabletop.
- Under **Part spawn markers**, select a whole marker such as `01 DownhillLargeWheel spawn`. Use **Part Size** in the `BuildingPartSpawn` Inspector: 1 is original size, 0.5 is half, and 2 is double. This scales the complete preview and generates matching playable meshes, colliders, sockets and wheel radii. Sizes are preserved when switching build modes and transferring the design onto the test course.
- Use W/E on the marker to move/rotate the complete part. Scene clicks select the marker by default. Avoid moving `Rubber tire`, `Painted block`, `Grid points`, or other preview children individually: those are separate visual elements. If you already changed a child, undo that edit before adjusting the parent.
- Use uniform **Part Size** for parts, rather than stretching individual Transform axes. Table scale remains independently editable. The part-size control currently applies to the Sandbox kit.
- Save with Ctrl+S, then use **MetaHackthone > Design Mode > Test Current Scene - Workbench** to test your scene edits.

## Design Mode: Car Workshop and Sandbox

In the Unity Editor, open MainMenu and press Play, then click **Design Mode** in the upper-left corner. Choose **Car Workshop**, **Sandbox / Workbench Build**, or **Sandbox / Build on Sandbox**. Mouse and keyboard work without a headset, including when the active build target is Android. Design Mode is an Editor-only option and defaults off each Play session.

Car Workshop opens with a chassis, four wheels and a launcher already connected, in Build mode. Choose **Build / Drive** and use W/S for throttle, A/D for steering, and Space to fire. Sandbox opens with the core and two large wheels connected; press **READY / TEST** to start the normal transition, countdown and gravity simulation. Both Sandbox build choices use the same saved course and the same preset at their respective build scales.

**Reset Test Car** restores the level's preset in Build mode. It is unavailable while holding parts, attaching, rotating, or running a Sandbox test; use **BACK TO BUILD** first. **RETRY** keeps the current construction and resets its simulation. **Overview** frames the current edited level. Returning to the Design Mode menu retains your temporary construction; switching between Design Mode and normal modes discards the temporary Car Workshop/Sandbox builds. Scene files and player construction saves are not written by Design Mode; construction Save/Load controls are disabled there.

For blockouts, stop Play mode and use **MetaHackthone > Design Mode > Open Car Workshop** or **Open Sandbox**. Select **Editable building layout**. Its Inspector provides **Test in Design Mode** (both build choices for Sandbox) and selection/framing buttons for the course, vehicle spawn, simulation start, finish and safe regions. **MetaHackthone > Design Mode > Test Current Scene** is the equivalent menu entry. These test the currently open scene, including unsaved edits, without reopening it or changing build targets. The previous Play Mode start-scene override is restored when testing ends.

Car Workshop's **Design car spawn** marker controls the test car's position and heading; its wireframe and arrow are editor guides. Move or rotate it and save the scene. Sandbox uses its existing core spawn marker for Workbench Build and **Simulation start** for placement on the course. Change roads, ramps, barriers and decorations through their existing scene objects and colliders. Update finish/safe regions to match a changed route.

Saved course position, rotation, scale, bench scale, and control positions are authoritative. Runtime presentation uses those transforms as its baseline, applies the chosen build-mode placement and approach animation, then restores the baseline. Presentation settings seed newly generated layouts; they no longer overwrite authored transforms at startup. Sandbox controls stay fixed in the placed arena and never follow the view.

The sandbox now uses the [fixed world-space interface](SandboxInterface.md). All panels and countdown/status text stay anchored as the headset moves; the course alone approaches during Start. The card currently represents the single existing course.

The course's **Contact Offset** sets the miniature collision margin at runtime (Unity does not persist individual collider contact offsets). Saved and generated courses use the same 1 mm default. Passive tires resist sideways twisting while touching the road, without applying propulsion or steering toward the finish.

Save persistent edits outside Play mode with **Ctrl+S**. Play-mode geometry edits follow Unity's normal discard behavior. The supplied Sandbox preset is intended to complete the supplied course; a designer can create an impossible blockout, and testing reports the real physics outcome. Narrow Gap, Shadow and Cloud Garden retain their existing workflows; this version does not add additional course assets or a level picker.

## Design Mode verification, 2026-10-25

All 19 targeted Play-mode tests passed (`31b3cb54743644548223f46945eef166`): Design Mode transitions/save isolation, all existing main-menu and desktop-input checks, the repeated gravity reference, and both wheel-behavior comparisons. The default car completed the saved Sandbox three times in each build mode using real gravity and the existing finish/failure checks. All five scene-asset and authored-transform Edit-mode checks passed (`8d27b00bd8714397a4e8296049ed9d35`).

Direct Editor launches confirmed unsaved Workshop spawn edits and Sandbox baseline edits are used, On Sandbox is selected correctly, XR remains disabled, and the previous Play Mode start scene is restored on exit. A subsequent ordinary Play session starts with Design Mode off. Game-view screenshots are in `Logs/DesignMode/`. No APK was built or deployed.

## Existing scene authoring

Open `Assets/MetaHackthone/Scenes/Workshop.unity`, `NarrowGap.unity`, or `DownhillSandbox.unity` outside Play mode. Expand **Editable building layout** in the Hierarchy. Select a child and use Unity's normal Move, Rotate, Scale and Inspector tools, then save with **Ctrl+S**. The layout Inspector includes **Frame editable layout**.

The saved scene owns its tables, rails, obstacles, decorations, text, controls, materials and spawn markers. Startup binds gameplay to those objects without recreating the environment. Added scenery under the layout moves with the arena. Edit the layout's children; its root is the tracked-floor origin and is repositioned relative to the player in a headset.

- **Part spawn markers** show the initial kit. Move or rotate a marker to change its spawn and reset pose. The preview child is an editor guide, hidden in Play mode and stripped from builds. Use the marker's **Part Size** to resize playable geometry and sockets together. Downhill has eight indexed slots, with seven parts in the current scene. Empty optional slots omit that part; preserve marker order and kinds. Slot 0 is the core and falls back to the default core pose if its marker is missing.
- **Controls** can be moved or renamed. Each button's text follows it, and its `Command` field identifies the action independently of its name. Status and score labels are updated during gameplay; decorative text is editable.
- **Downhill:** the **Start Return** button and **Build rotation wheel** are authored controls beneath **Centered workbench frame**. The rim sits beneath the core with 3 cm tabletop clearance; its bottom is 1 cm below the wheel object's center. Keep it centered beneath the core if you move the core's spawn marker. The transfer plate is removed. **Simulation start**, **Finish region**, and **Course safe region** are editable under **Sandbox frame coordinates**. The safe region's lower face, side faces and forward face determine failures; its upper/back faces are only drawing guides. The table defaults remain 70 cm above the floor. Placement feedback reads the main tabletop collider's actual height.
- **Narrow Gap:** move **Launch pad**, **Gate**, or **Landing platform** groups to keep the physical objects and gameplay markers together. **Core docking point** controls the dock; **Valid opening** and **Landing region** control scoring. If resizing individual stakes or the landing deck, update the corresponding region's `Size` too. Keep gameplay group scales at one and change region `Size` for dimensions. Launch strength and nominal direction remain controlled by `NarrowGapSettings` and the existing aiming gesture.

Part dimensions, mass, sockets, wheel behavior and gestures remain in their shared definitions/settings. Existing saved-construction compatibility is unchanged. Layout settings are defaults for generating a new scene; changing those seed values does not overwrite an authored layout.

**MetaHackthone > Scene editing > Convert building scenes (preserve existing layouts)** converts legacy scenes once and skips scenes that already have a layout. Save open scenes before using it. The old level generators refuse to overwrite authored scenes. Generated meshes and materials are stored in `Assets/MetaHackthone/Art/SceneLayouts` and referenced by the scenes; keep these assets with the scenes.

The editor conversion does not deploy an APK. Changes made during Play mode follow Unity's normal rules and are discarded when Play mode ends.

## Verification, 2026-10-22

All 103 edit-mode tests passed (`5d15a531ad88416eb828ee2a788fe13c`). After the final control grouping and tabletop-height changes, all three saved-scene checks passed again (`6d41b4465a914e898e0dfc5b7a439e14`) and all ten targeted runtime/layout/floor tests passed (`6d1e10f7acc84cc3a291f84244ead43f`). Coverage includes edited spawn poses, reset poses, existing-object reuse, renamed button actions, persistent mesh/material references, and an edited 82 cm worktop above a tracked floor at 1.4 m.

The broader play run passed 95/98 (`1bb8566722144af69c1a2fdf96cf012c`). The known Downhill gravity-finish and Narrow Gap wide-hull assertions remain failing. A car-steering check also failed in that run and passed unchanged in isolation (`c8ea5f7b9e7f4959a0899d1cf9383a8e`). No physics tuning or acceptance thresholds were changed for scene editing.

Direct Play-mode visual checks confirmed one authored layout per level, 15 Workshop parts, 38 Narrow Gap parts, eight Downhill parts, hidden editor previews and no console errors. Narrow Gap's grouped pad restored its exact position after rotation. Running the converter again left all three scene files unchanged. Screenshots are in `Logs/WorkshopAuthoredPlay.png`, `Logs/NarrowGapAuthoredPlay.png`, `Logs/DownhillAuthoredPlay.png` and `Logs/DownhillEditableScene-1.png`. No APK was built or installed for this change.

# Edit Level 1 and Level 2 in the Scene view

## Open and edit

Stop Play mode. Choose **MetaHackthone > Design Mode > Level 1 - Gravity Garage** or **Level 2 - Waterfall Valley**.
These commands open the saved sandbox scene, select the course, and frame it in the Scene view. They do not start Play mode or generate geometry.

Both levels are saved in `Assets/MetaHackthone/Scenes/DownhillSandbox.unity`, under **Editable building layout > Sandbox frame coordinates** (expand the hierarchy to find the two named course roots). The menu shows the selected course and hides the other. This is one shared scene, not two copies of the same layout.

Use Unity's Move, Rotate, and Scale tools to edit objects. Save with **Ctrl+S outside Play mode**.
The scene's **Authored Sandbox Layout** component records which level is selected for direct Play.

## What can be edited?

- **Level 1:** the normal gameplay downhill/jump course. Move and resize the individual **Garage road** blocks, **Garage landing**, rocks, and scenery.
- **Level 2:** use **Edit Track Curve** to reshape anchors and tangents. Road meshes, collision surfaces, and movement samples are baked together. Route samples are derived data, not independent editing handles.
- Add ordinary GameObjects or prefab instances for new scenery and obstacles. Add colliders to solid obstacles.
- Move **START**, **JUMP**, **TURN**, **LOWER**, **FINISH**, and **DRILL target** markers to change gameplay locations. Marker forward arrows indicate travel direction. Markers are parented to the corresponding road or wall so moving those objects also moves the gameplay location.
- **Safe region** and **Water hazard** objects have disabled BoxColliders used as editable volume data. Keep them disabled; select and edit their Center/Size fields. Adjust them if you substantially relocate the road.
- The drill wall and its target should move together. Keep the yellow target visual aligned with the DRILL marker.
- The workbench, part spawn markers, and controls remain shared by both levels.

Moving road sections can create gaps and steps. Ensure adjoining meshes still meet and that the ordered route passes through the drivable surface. These edits can intentionally change difficulty; run a test after editing.

## Test the saved layout

Press Unity's **Play** button while this scene is open to test the selected course in Design Mode, or select **Editable building layout** and click **Test selected level** in its Authored Sandbox Layout Inspector.

Gameplay uses the authored objects. It does not rebuild the roads, rocks, or scenery on entering Play, resetting a construction, retrying, or advancing the campaign. Normal gameplay still enters via MainMenu, starts with Level 1, and advances to Level 2.

Stop Play before making persistent layout changes. Runtime vehicle construction and effects remain temporary. Design Mode does not write player construction saves or campaign progress.

## Level 2 wooden road movement

Level 2 uses **Connection Area** transfers between its upper and lower physics puzzles. The connection is not a physics challenge: crossing its entrance gate always transports the connected vehicle along the visible curve, even if it arrives tilted or airborne. Falling outside the gate does not trigger a transfer.

- **Transfer Duration** defaults to 2 playback seconds. Pause and playback speed affect the animation; course scale does not. Connection length, height, gravity and friction do not change outgoing physics.
- Every connected body's pose, velocity and angular velocity is preserved relative to the entrance and rotated into the exit frame. Wheel spin, wobble and joint configuration carry over; there is no boost or stabilization.
- Collisions, puzzle time, progress and failure timers are paused during transfer. Physics and hazards resume at the destination. Reverse entry transfers back toward the upper road.
- The Scene view shows entrance and exit gates. **Gate Width** defaults to road width (zero override); **Gate Bottom/Top** default to -0.25 and 2 course units relative to the road.
- Use **Edit Track Curve** to reshape the visual connection and puzzle roads. Save outside Play mode. Retry restores all temporary physics settings.
- Clear the lower drill wall and reach the physical finish to complete the puzzle. Legacy **Whole Road** guidance retains its separate momentum and parking settings.


## Legacy tools

The older **Gravity Garage 2.5D Level Editor** is a separate pipe-course prototype, not the authoritative Level 1 layout. Changing its parameters or the old level-generation settings does not reshape either saved course. Use the Scene view for the two game levels.

The initial conversion is an explicit editor-only operation and refuses to run on an already-authored scene. There is no automatic generation or refresh that can overwrite your layout.

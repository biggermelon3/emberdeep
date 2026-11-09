# Beautiful Corner campaign environments

The saved Gravity Garage and Waterfall Valley courses use the existing Creek Diorama art kit. Their track geometry, colliders, physics materials, start/finish markers, water hazards, and guided sections are preserved. The workbench and its controls are unchanged.

`CreekCampaignTheme` stores the source prefab references and the baked visual root. The editor command **MetaHackthone > Sandbox > Apply Beautiful Corner visuals** fits timber slats, rocks, trees, and terraces to the authored scenery placements and batches them by shared material. It never rebuilds the road. Water uses a campaign-local material with depth/opaque-texture sampling disabled; vertical waterfall faces use the existing silk-waterfall shader with normalized flow UVs. Original scenery renderers remain disabled for reversible authoring. The standalone art sample is untouched.

The command compares collider/ancestor-transform snapshots before and after authoring and refuses to save if they differ. New visual roots have no colliders or attachment registration. Generated meshes are saved under `Assets/MetaHackthone/Art/CreekCampaign`; these are required source assets, not disposable build output.

The first course's old multi-block preset stalled before the jump in baseline testing. Its themed-campaign preset now uses the core and two large wheels attached through the standard sockets. Unused parts remain available on the workbench. The second course retains its drill-equipped preset. Other courses retain their original preset behavior.

## Verification

Level 1 pillars are now baked separately from other timber. **MetaHackthone > Sandbox > Align Level 1 pillars** positions the existing twenty posts under the current road sections and landing, retains their base elevation, and preserves track physics and other visual batch transforms. This is an editor placement correction, not automatic runtime following. Reapplying the environment also uses the corrected pillar placement.

After this correction, `SavedFirstPresetFinishesFiveRuns` passed all five completions and returns (job `fecdb8ab7d49439a899eaef0235fc511`). Android build `build-3128b8f9ff` succeeded with zero errors and 22 warnings and was installed on the selected Quest 3. The latest APK is `Builds/MetaHackthone-BeautifulCorner-Quest.apk`; build outputs remain local and ignored. The earlier build and device measurements below describe the preceding environment validation.

- Saved level one: five consecutive physical completions and victory returns, job `7c4217f822a04bd9bcdea35c039a73a4`.
- Saved level two: five consecutive physical completions and victory returns, job `3415261101e9475e9862e0876722fdd1`.
- Victory/reward regression and saved-track no-drill barrier check: 7/7 passed, job `6d590f4137514cf6a21c2a41aacd8136`.
- Collider/ancestor-transform snapshots matched after each visual bake. Evidence and captures are in `Logs/CreekCampaign`.
- Repeated full scene loads within one PlayMode test caused the desktop editor to hang after a successful run. The completion tests therefore load a fresh saved scene once per test and use in-scene build resets for subsequent attempts. Full repeated scene-load acceptance is not claimed.

Quest development acceptance can be run with `Tools/Test-QuestCreek.ps1 -Serial <authorized serial>`. It drives both presets through normal launch and finish logic, advances through the real Next Level action, checks drill availability and stereo rendering, and reports frame samples. It does not inject success or synthesize palm input. The script restores its temporary proximity override in `finally`.

## Build and device status — 2026-11-05

- Android development build `build-d518120303` succeeded: zero errors, 22 warnings, 92.91 seconds. APK: `Build/MetaHackthone-BeautifulCorner-Quest.apk` (148,839,501 bytes), SHA256 `291D888567D98D334DBDD1DEC0C052385695112C4A37749C6BF468C545C88CA2`.
- Installed with replacement on the selected Quest 3 `2G0YC5ZG5V04ZW`, retaining app data.
- The first device attempt paused behind “Finding position in room” and timed out. After the user restored tracking, the repeated acceptance run **passed**: both presets completed through normal physics/gameplay, both victory returns finished, and the real Next Level action unlocked the drill. No success injection was used. Both-eye captures show the environment, and all active materials passed shader-support checks. Proximity overrides were restored and verified after both attempts.
- Device frame samples (milliseconds): level one median **11.20**, p95 **12.34**, maximum **33.06**; level two median **11.26**, p95 **13.48**, maximum **26.81**. Earlier palm/victory samples were median 11.14 / p95 12.17 / max 34.74, but exercised different content, so these are context rather than a controlled before/after benchmark. Spikes remain; hitch-free 90 FPS is not claimed. Evidence: `Logs/CreekCampaign/DeviceTracked/result.json`, `device.log`, and both `Creek-level-*.png` captures.
- Baked decoration counts: level one has 14 material batches / 21,889 vertices; level two has 14 batches / 31,573 vertices. Both visual roots have zero colliders. These counts exclude the unchanged track and gameplay objects.
- Final desktop captures: `Logs/CreekCampaign/level1-final.png` and `Logs/CreekCampaign/level2-final.png`. The complete-frame desktop views show the current road layout, timber decking, terraces, pines, animated water and waterfall materials. Physical-hand comfort remains a wearer check; the automated device run disables hand input temporarily and restores it afterward.

# Emberdeep

Emberdeep is a hand-tracked VR miniature-world firefighting game for Quest 3 and PICO 4. This folder is the Unity project root and independent Git repository root. Current candidate: **0.2.14 / Android versionCode 17**, retaining hand input on both platforms.

## Current sky and art direction

Candidate **0.2.14 / Android versionCode 17** adds a four-second bottom-to-top surrounding forest sky after fireground placement. It covers the passthrough background while leaving scene geometry and hands in front; recenter/sector advance do not replay it, and returning to camp removes it. Hand tracking and both refill stations remain intact.

See the [implementation and proposal contract](Docs/Emberdeep/ImmersiveSkyAndArtDirection.json), [Chinese art plan](Deliverables/Emberdeep/Emberdeep-0.2.14-Art-Plan.txt), [Editor view](Deliverables/Emberdeep/Emberdeep-0.2.14-Surrounded.png) and [device checks](Deliverables/Emberdeep/Emberdeep-0.2.14-Device-Checks.txt). HUD simplification, foliage/riverbank polish and removal of the optional creek pump from ordinary levels are proposals only.

FinalAcceptance passed **208 EditMode and 161 PlayMode** tests, including rendered alpha coverage, foreground visibility and live-frame lifecycle checks. Both APKs passed version, ARM64, signature, scene-whitelist and 28-setting restoration audits. Real compositor occlusion, stereo, hand visibility and performance acceptance remain pending. Word revision 0.13 remains the 0.2.11 baseline supplemented by these JSON contracts.

Quest 3 installation of 0.2.14/code 17 was verified; a later launch check could not run after the device disconnected. See the [installation and old-APK cleanup record](Deliverables/Emberdeep/Emberdeep-0.2.14-Installation.json) and [0.2.14 changelist](Docs/Emberdeep/Changelist-0.2.14.txt).

## Retained hand-tracking fix (0.2.13)

Retained input baseline: **0.2.13 / Android versionCode 16**. Both Quest and PICO use **hand aiming and a fresh thumb-index pinch**. The user rejected the 0.2.12 head-dwell workaround; it has been removed. Looking at a button alone never selects it. Use the [current hand-tracking contract](Docs/Emberdeep/PicoHandTrackingFix.json) and [Chinese device checks](Deliverables/Emberdeep/Emberdeep-0.2.13-Device-Checks.txt).

PICO menu aim reads live OpenXR wrist/knuckle poses and per-joint validity, independently of SDK pointer objects and update bindings. Pinch availability is separate: missing tips stop clicks while valid aiming joints keep the ray live. Lost native updates, system-owned gestures, focus loss and pause still suppress input. A fresh opening is required after recovery. The camp displays live hand rays before acquiring a button, and small edge jitter no longer repeatedly cancels an acquired target. Fireground rays remain contextual. No camera-level tracking or OS system-menu fix is claimed.

FinalAcceptance passed **208 EditMode and 157 PlayMode** tests. Both APKs passed version, ARM64, signature, scene-whitelist and 28-setting restoration audits. Worn-headset acceptance is pending. The tester's PICO model and OS are unknown. Design revision 0.13 and its generated Word files remain the **0.2.11 baseline**, supplemented by the current JSON contract. The inherited default Editor scene list still requires the standalone test/build runner's whitelist.

## Open

1. Install **Unity 6000.3.21f1** and Git LFS. For APK builds, install Android Build Support with its SDK, NDK and OpenJDK.
2. Clone this repository. Run `git lfs install` and `git lfs pull` in the repository root.
3. Add this root folder to Unity Hub. Allow Unity to restore packages and import assets.
4. Open `Assets/FireRescue/Scenes/EmberdeepCamp.unity` to start Emberdeep.

PICO OpenXR 1.4.1 is pinned to official commit `3aa3e62bff41df618529eeb60ff02c29a515dafe` in the package manifest and lockfile. No external workstation SDK checkout is required. Unity restores Meta XR and other UPM dependencies; the first import needs internet access.

## Previous design baseline (0.2.11)

Design revision **0.13** implements the approved 0.12 direction for slow recovery and per-purchase shop previews. The recovery transaction waits 3 eligible seconds, then adds 0.25 units/second to the active owned tank, including when racked; manual refill remains much faster. Recovery has separate accounting, fresh-delay interruption rules and a quiet gauge cue. Shop Equipment, Tuning and Station pages now display one stable model preview, explicit numeric comparisons and station current/result views. View Tuning/View Water browse; Buy remains a separate deliberate action. Missing models show PREVIEW UNAVAILABLE. Direct display geometry has no gameplay/physics/audio/particle components; optional thumbnail tiles and rotate/demo controls are not implemented. See [the current implementation contract](Docs/Emberdeep/WaterRecoveryAndShopPreview.json) and [draft device checks](Docs/Emberdeep/RecoveryShopDeviceAcceptance.json). FinalVerification passed 204 EditMode and 144 PlayMode under `Logs/EmberdeepRecoveryShop`. Twelve final Editor captures show all tools, tuning, station comparison and recovery states. Both APKs are delivered and audited; device acceptance remains pending. The increase includes 20 new EditMode and 26 new PlayMode cases plus 3 restored existing PlayMode cases, not 29 newly written cases.

## Current gameplay

Historical 0.2.10 interaction baseline retained: Quest 0.2.9 feedback confirmed some position improvement but requested more height. The user clarified that attempting to click a menu may trigger pump/gameplay actions, with uncertain cause; the second-incident creek remains only a suspicion. In 0.2.10 the panel pivot is 0.04 m below eyes (raised 8 cm), still 0.9 m ahead and world anchored. The optional creek stays stage-gated and is labelled CREEK / OPTIONAL REFILL. Its final mirrored position is 0.32 m sideways and about 0.60 m forward at the current miniature scale, separating it from the rack and tools. Tool guidance requires open pinch distance at least 0.035 m and a docking-grip target within 0.16 m; actual port distance/angle/dwell still govern docking. Closed firing pinch cannot newly dock. Eligible ray intent within 25 degrees reserves UI before dwell or first closed-pinch acquisition can trigger new physical/weapon actions. Release/rearm is still required to click. An existing refill operator stays exclusive; the other UI hand stays truly tracked with physical pinch suppressed. Without an actual refill operator, UI reservation cancels pending strokes and blocks new docking/pumping. Raw SDK pointer acquisition remains a device-test boundary. Camp retains its 180-degree policy. Both intermediate 184/109 runs precede the final UI-priority change. A subsequent FinalAcceptance run passed 184 EditMode and 114/115 PlayMode; one existing Quit test timed out in stable-placement SetUp before reaching Quit behavior. Its cause is unconfirmed; only diagnostics were added, with the 10-second threshold retained. The subsequent full `Logs/EmberdeepInteractionGuard/ReleaseAcceptance` rerun passed 184/115. The 19 net new PlayMode cases are included in 115; existing HUD edit cases were updated, not newly added. Five final Editor captures show seated/standing HUD/Shop and a separately tilted stage-two overview with synthetic near-well physical-hand/ray input; these are not headset or final-art evidence. The user deferred current device checks; these changes do not claim to eliminate every accidental action.

Historical 0.2.9 placement (its eye drop is now superseded): the user confirmed Shop highlight and entry on Quest 3 0.2.8, then reported that fireground/Shop panels sit near the legs or seat. The 0.2.9 implementation separates them from the miniature Arena: upright yaw-only, 0.9 m in front and pivot 0.12 m below tracked eyes, world anchored between stable initial placement, recenter and Shop transitions. Seated/standing placement uses eye position; no camera movement or continuous head following. Finite eye pose and at least 0.45 m eye height above floor are required. Near-vertical heading uses previous HUD heading (Arena heading initially); invalid fallback rejects placement. Reposition resets UI target/pinch state. Desktop setup/recenter uses pitch 25 degrees/FOV 70; XR viewpoint is unchanged. Four Editor captures use horizontal synthetic gaze at 1.1 m and 1.7 m eye heights without LookAt correction; the panels are visible and Shop opens. These do not verify physical headset comfort.

Historical 0.2.8 access repair: the user reported that Quest 0.2.7 Shop could not highlight or activate after firefighting. The 0.2.8 repair targets free-hand UI access near supply and a safe transition from a docked tool to Gear Shop when `Run.InSupply` permits trading. Opening the shop returns/disconnects the tool and cancels unfinished actions without adding water. Buying during Fighting remains disallowed. Each hand retains its own near-station/lever exclusion and operating-hand priority. Won shows SHOP CLOSED. The five new Shop regressions pass within the 92 PlayMode total. Editor policy tests do not establish raw SDK hand sampling or physical usability. Synthetic Editor captures show Shop highlight/open and unchanged water/coins.

The implemented M1 loop includes manual water collection, firefighting, conserved water, equipment selection/upgrades and sector-entry retry. Candidate 0.2.11 adds accounted slow recovery only during eligible Fighting/Cleared fireground time. A held dry trigger, charge, manual operation, shop, modal, pause, tracking loss or transition blocks it; resume, switch, purchase, upgrade and undock restart the delay. Display hands settle onto calibrated grip frames with staged closure, squeeze and release. The starter well restores thumb-index pinch after the user reported that the 0.2.6 whole-hand gesture was unusable. Release near the handle, then pinch with index contact within 10 cm; keep pinching while lowering the palm 8 cm and returning within 2 cm. Whole-hand curl, GripValid and all-finger confidence are not additional acquisition gates. An undocked stroke produces an outlet splash only; a docked stroke adds 12 water units capped by capacity. Release, tracking loss, cancellation and modal ownership discard half-strokes. Recovery requires a released pinch followed by a new pinch. The anchored closed-hand display animation remains visual feedback.

Safe searching, animals, reward settlement, durable expedition saves and planned M2/M3 content remain proposed. Editor tests do not establish headset comfort or performance. User confirmations of earlier versions retain their specific version and question scope.

## Test and build

With an idle Editor open on this project, run from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/Test-EmberdeepM1.ps1
```

The runner creates a fresh timestamped output directory, temporarily selects the two Emberdeep scenes and restores the original Editor setup/list.

For APKs use **Emberdeep > Build standalone Quest showcase** or **Emberdeep > Build standalone PICO showcase** in Unity. Automation entry points are `FireRescue.Editor.FireRescueBuilder.BuildShowcaseQuest` and `FireRescue.Editor.FireRescueBuilder.BuildShowcasePico`.

Current 0.2.13 outputs are `Deliverables/Emberdeep/Emberdeep-0.2.13.apk` (Quest) and `Deliverables/Emberdeep/Emberdeep-0.2.13-Pico.apk` (PICO), Android versionCode 16. Both passed APK audits with zero build errors; warnings and hashes are in `Logs/EmberdeepStableHands/ReleaseEvidence.json`.

Withdrawn 0.2.12: the user rejected the head-dwell replacement. Its APKs and evidence are historical and must not be distributed as the current hand-tracking fix.

Historical 0.2.11 outputs are `Deliverables/Emberdeep/Emberdeep-0.2.11.apk` and `Emberdeep-0.2.11-Pico.apk`. Quest and PICO APKs passed version 0.2.11/code 14, ARM64, signature/icon, exact Camp/Fireground scene whitelist and settings-restoration audits. Both builds report zero errors, with 8 Quest warnings and 12 PICO warnings. Source integrity is recorded in Logs/EmberdeepRecoveryShop/ReleaseEvidence.json; these build checks do not establish headset operation or performance. Quest 3 replacement installation and app info verified 0.2.11/code 14. The device was Asleep and no app process was observed; physical hand tracking, comfort, stereo readability and performance remain pending. No PICO hardware acceptance is claimed.

Historical verified 0.2.10 outputs are `Deliverables/Emberdeep/Emberdeep-0.2.10.apk` and `Emberdeep-0.2.10-Pico.apk`. Both Quest and PICO APKs passed version 0.2.10/code 13, ARM64, signature/icon, camp/fireground scene-whitelist and 28-setting restoration audits. Both builds report zero errors, with 8 Quest warnings and 12 PICO warnings; they are not warning-free builds. Quest 3 replacement installation and app info verified 0.2.10/code 13. The headset was Asleep, no app process was observed, and launch was not attempted. The user explicitly deferred testing until later this round; current placement/menu interaction acceptance remains pending. Do not infer acceptance from old feedback or ask again this round. No PICO 4 physical usability or performance acceptance.

Historical verified 0.2.9 outputs are `Deliverables/Emberdeep/Emberdeep-0.2.9.apk` and `Emberdeep-0.2.9-Pico.apk`. Both passed version, signature, ARM64, scene-whitelist and settings-restoration audits. Quest 3 replacement installation preserves data and installed version 0.2.9/code 12 is verified. Launch was requested but is not confirmed: no app PID was observed and the recorded power state is Asleep. Device placement/Shop acceptance remains pending. No PICO 4 physical usability or performance acceptance. Prior 0.2.8 Shop highlight/entry confirmation is historical and does not validate this placement change.

Historical verified 0.2.8 outputs are `Deliverables/Emberdeep/Emberdeep-0.2.8.apk` and `Emberdeep-0.2.8-Pico.apk`. Both passed version, ARM64, signature, scene-whitelist and settings-restoration audits. Quest 3 received a replacement install without clearing data and reports 0.2.8/code 11. Subsequent launch and a running app process are confirmed. User acceptance is limited to Shop highlight and entry after firefighting. The builder temporarily applies Emberdeep app settings, the hydrant icon and scene whitelist, then restores the prior settings. Only `EmberdeepCamp` and `EmberdeepFireground` may enter standalone APKs. Use these builders rather than inherited shared-workshop builders.

Historical only: the 0.2.6 pre-migration release verification passed **167 EditMode and 84 PlayMode tests**. Both existing APKs passed version, signature, scene and settings-restoration audits. These results belong to the earlier package/repository migration and do not verify this revision. Migration verification is recorded in [GitMigration.json](Docs/Emberdeep/GitMigration.json).

## Specifications and evidence

- [Document manifest](Docs/Emberdeep/DocumentManifest.json): authoritative English JSON reading order.
- [Implementation status](Docs/Emberdeep/ImplementationStatus.json): current, proposed and target scope.
- [Current hand-tracking fix](Docs/Emberdeep/PicoHandTrackingFix.json).
- [Recovery and preview candidate](Docs/Emberdeep/WaterRecoveryAndShopPreview.json).
- [Retained 0.2.10 interaction intent and HUD height](Docs/Emberdeep/InteractionIntentFix.json).
- [Historical 0.2.9 placement contract](Docs/Emberdeep/HudPlacementFix.json).
- [Retained Shop access rules from 0.2.8](Docs/Emberdeep/ShopAccessFix.json).
- [Retained pinch and pump rules from 0.2.7](Docs/Emberdeep/PinchRestore.json).
- [Candidate 0.2.11 device checklist](Docs/Emberdeep/RecoveryShopDeviceAcceptance.json) and [Chinese steps](Deliverables/Emberdeep/Emberdeep-0.2.11-Device-Checks.txt).
- [Historical 0.2.10 Chinese device checklist](Deliverables/Emberdeep/Emberdeep-0.2.10-Device-Checks.txt).
- [Historical 0.2.6 verification archive](Deliverables/Emberdeep/Emberdeep-0.2.6-Verification.zip): Editor screenshots, tests and build reproduction scripts. Extract at this root to restore historical evidence paths.
- [Repository setup and migration notes](Docs/Emberdeep/GitMigration.md).

English Word documents are generated from JSON using `Tools/BuildEmberdeepDesignDocs.py`; `Tools/ValidateEmberdeepDocs.py` checks exact source content. The 0.13 Word files were regenerated and exact ordered source comparison passed for all three documents (`Logs/EmberdeepRecoveryShop/Docs/qa.json`). Fresh packaged render attempts failed because LibreOffice soffice.exe is unavailable. A bounded hidden native Word COM fallback also failed before opening the task document (null Application.Hwnd); no PDF or page images were produced. Final APK facts are included in the regenerated source and DOCX. Generated DOCX remain drafts without visual layout approval.

## Source layout

`Assets/FireRescue` holds Emberdeep runtime code, builders, tests, scenes and assets. `ArtSource/EmberdeepV2` holds editable artwork. `Docs`, `Tools` and `Deliverables/Emberdeep` hold specifications, tooling and delivery records.

The complete existing shared source tree is retained because Emberdeep reuses XR, hand and grab components. Original shared-workspace instructions are preserved in [LegacyWorkspaceREADME.md](Docs/LegacyWorkspaceREADME.md). Unity caches, raw logs, generated APKs, user preferences and Plastic metadata remain local and are ignored by Git. Large binary assets use Git LFS; retain all Unity `.meta` files.

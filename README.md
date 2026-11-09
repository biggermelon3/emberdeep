# Emberdeep

Emberdeep is a hand-tracked VR miniature-world firefighting game for Quest 3 and PICO 4. This folder is the Unity project root and independent Git repository root. Current gameplay candidate: **0.2.7 / Android versionCode 10**. Current automated verification passed 167 EditMode and 87 PlayMode tests. Quest/PICO APKs passed their build audits; installation and device acceptance remain pending.

## Open

1. Install **Unity 6000.3.21f1** and Git LFS. For APK builds, install Android Build Support with its SDK, NDK and OpenJDK.
2. Clone this repository. Run `git lfs install` and `git lfs pull` in the repository root.
3. Add this root folder to Unity Hub. Allow Unity to restore packages and import assets.
4. Open `Assets/FireRescue/Scenes/EmberdeepCamp.unity` to start Emberdeep.

PICO OpenXR 1.4.1 is pinned to official commit `3aa3e62bff41df618529eeb60ff02c29a515dafe` in the package manifest and lockfile. No external workstation SDK checkout is required. Unity restores Meta XR and other UPM dependencies; the first import needs internet access.

## Current gameplay

The implemented M1 loop includes manual water collection, firefighting, conserved water, equipment selection/upgrades and sector-entry retry. Display hands settle onto calibrated grip frames with staged closure, squeeze and release. The starter well restores thumb-index pinch after the user reported that the 0.2.6 whole-hand gesture was unusable. Release near the handle, then pinch with index contact within 10 cm; keep pinching while lowering the palm 8 cm and returning within 2 cm. Whole-hand curl, GripValid and all-finger confidence are not additional acquisition gates. An undocked stroke produces an outlet splash only; a docked stroke adds 12 water units capped by capacity. Release, tracking loss, cancellation and modal ownership discard half-strokes. Recovery requires a released pinch followed by a new pinch. The anchored closed-hand display animation remains visual feedback.

Safe searching, animals, reward settlement, durable expedition saves and planned M2/M3 content remain proposed. Editor tests do not establish headset comfort or performance. User confirmations of earlier versions retain their specific version and question scope.

## Test and build

With an idle Editor open on this project, run from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/Test-EmberdeepM1.ps1
```

The runner creates a fresh timestamped output directory, temporarily selects the two Emberdeep scenes and restores the original Editor setup/list.

For APKs use **Emberdeep > Build standalone Quest showcase** or **Emberdeep > Build standalone PICO showcase** in Unity. Automation entry points are `FireRescue.Editor.FireRescueBuilder.BuildShowcaseQuest` and `FireRescue.Editor.FireRescueBuilder.BuildShowcasePico`.

Verified 0.2.7 outputs are `Deliverables/Emberdeep/Emberdeep-0.2.7.apk` and `Emberdeep-0.2.7-Pico.apk`. Both passed version, ARM64, signature, scene whitelist and settings-restoration audits. The builder temporarily applies Emberdeep app settings, the hydrant icon and scene whitelist, then restores the prior settings. Only `EmberdeepCamp` and `EmberdeepFireground` may enter standalone APKs. Use these builders rather than inherited shared-workshop builders.

Historical only: the 0.2.6 pre-migration release verification passed **167 EditMode and 84 PlayMode tests**. Both existing APKs passed version, signature, scene and settings-restoration audits. This migration changes package resolution and repository setup; those APKs remain the previous verified builds. New migration verification is recorded in [GitMigration.json](Docs/Emberdeep/GitMigration.json).

## Specifications and evidence

- [Document manifest](Docs/Emberdeep/DocumentManifest.json): authoritative English JSON reading order.
- [Implementation status](Docs/Emberdeep/ImplementationStatus.json): current, proposed and target scope.
- [Current pinch and pump rules](Docs/Emberdeep/PinchRestore.json).
- [Chinese device checklist](Deliverables/Emberdeep/Emberdeep-0.2.7-Device-Checks.txt).
- [Historical 0.2.6 verification archive](Deliverables/Emberdeep/Emberdeep-0.2.6-Verification.zip): Editor screenshots, tests and build reproduction scripts. Extract at this root to restore historical evidence paths.
- [Repository setup and migration notes](Docs/Emberdeep/GitMigration.md).

English Word documents are generated from JSON using `Tools/BuildEmberdeepDesignDocs.py`; `Tools/ValidateEmberdeepDocs.py` checks exact source content. Current Word files remain drafts without visual layout approval because the rendering environment lacks LibreOffice.

## Source layout

`Assets/FireRescue` holds Emberdeep runtime code, builders, tests, scenes and assets. `ArtSource/EmberdeepV2` holds editable artwork. `Docs`, `Tools` and `Deliverables/Emberdeep` hold specifications, tooling and delivery records.

The complete existing shared source tree is retained because Emberdeep reuses XR, hand and grab components. Original shared-workspace instructions are preserved in [LegacyWorkspaceREADME.md](Docs/LegacyWorkspaceREADME.md). Unity caches, raw logs, generated APKs, user preferences and Plastic metadata remain local and are ignored by Git. Large binary assets use Git LFS; retain all Unity `.meta` files.

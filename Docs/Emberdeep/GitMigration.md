# Emberdeep repository setup

The Unity project and Git repository root is `E:\GameDevelopment\MetaHackson\Emberdeep`, a sibling of the original shared `Project` directory. The active Emberdeep development workspace was already at this location before Git initialization; no second copy or destructive move was needed.

This repository preserves the complete current Unity source tree, including shared XR, car and shadow modules that Emberdeep references. Their presence in the source tree does not add scenes to Emberdeep APKs. The standalone builder allows only `EmberdeepCamp` and `EmberdeepFireground`.

## Reopen on another machine

1. Install Git, Git LFS, and Unity **6000.3.21f1**. Install Android Build Support with the matching SDK, NDK and OpenJDK for APK builds.
2. Clone the private repository and run `git lfs install` followed by `git lfs pull` in its root.
3. Add the repository root to Unity Hub. Let Unity Package Manager restore the dependencies in `Packages/manifest.json` and `Packages/packages-lock.json`.
4. Open `Assets/FireRescue/Scenes/EmberdeepCamp.unity` for Emberdeep. Use the standalone Emberdeep build commands, not shared-workshop builders.

The PICO OpenXR **1.4.1** dependency previously pointed outside this project to `E:\GameDevelopment\Tools\PICO-Unity-OpenXR-SDK`. It now references the same official Git commit, `3aa3e62bff41df618529eeb60ff02c29a515dafe`, through Unity Package Manager. The original external checkout remains unchanged. Its two modified `.so.meta` files contained Unity importer serialization updates, not SDK code changes.

The original `.plastic` directory remains locally available for recovery and comparison, but is ignored by Git. Do not assume a Git commit also checks in Plastic changes. The sibling shared project is untouched.

## Repository contents

- `Assets`, all associated `.meta` files, `Packages`, and `ProjectSettings` are the complete Unity source snapshot.
- `ArtSource`, `Docs`, `Tools`, and document/verification deliverables are included.
- Binary artwork and large baked mesh assets use Git LFS.
- `Library`, `Temp`, `UserSettings`, raw `Logs`, Plastic metadata and APK/build products are local-only. Unity recreates its caches after cloning. Existing local files were not deleted.
- The versioned `Deliverables/Emberdeep/Emberdeep-0.2.6-Verification.zip` contains the curated 0.2.6 test results, Editor screenshots and build reproduction scripts. Extract it into the repository root to restore the historical `Logs/EmberdeepContactGrip` evidence paths. Those are pre-migration gameplay/build records, not a new build after the package-reference migration.

The original mixed-project README is preserved in `Docs/LegacyWorkspaceREADME.md`. Some historical helper scripts refer to their original workstation paths; use the Emberdeep commands in the root README for this project.

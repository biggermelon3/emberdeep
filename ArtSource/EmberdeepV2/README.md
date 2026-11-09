# Emberdeep V2 modular art kit

Original Blender geometry authored for the generated Emberdeep V2 concept. This is a separate art kit; it does not replace the existing interaction anchors, colliders, input logic, or gameplay tree placement.

## Rebuild

From the project root in PowerShell:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 4.3/blender.exe' --background --python Tools/BuildEmberdeepArtV2.py
```

The deterministic script writes `Emberdeep-ArtV2.blend`, nine `Assets/FireRescue/ArtV2/Models/EDV2_*.fbx` files, `ArtV2-Manifest.json`, and `Emberdeep-ArtV2-Preview.png`. Blender 4.3.2 was used. The preview is a Blender Cycles studio render, not a Unity capture or a headset performance result. The preview assembly and studio are excluded from the FBX exports.

## Asset contract

- `Terrain`: layered slate/soil and moss tabletop, low path, sparse perimeter rocks and rooted ferns. The central gameplay lanes contain no baked trees or cabin. The front strip below Z=1.6 has only low ground/path. `CreekSurface` and `CreekFoam` are separate named meshes around Z=10.9. Terrain has 9 material slots across its semantic meshes.
- `Cedar`, `Broadleaf`: independent trunk and canopy meshes, 3 total material slots each; approximately 1.88 m and 1.77 m high in forest authoring units. These are placed by existing gameplay tree logic.
- `Cabin`: plank walls, terracotta tile roof, chimney, framed warm windows, door and timber porch. Intended as a peripheral landmark, outside active tree lanes.
- `Rock`, `Fern`, `Log`: standalone reusable border props. Fern leaflets are closed geometry; no alpha cards or textures are required.
- `Hydrant`, `CreekPump`: cosmetic metre-scale fixtures, approximately 0.229 m and 0.227 m tall. They contain no interaction behavior. Preserve the existing station anchors and functional controls. Hide hydrant `Coupling` and `Valve` meshes, and pump `Handle`, `Pipe`, and `Strainer` meshes when those parts are supplied by runtime gameplay controls. Blender may append `.001` suffixes to shared semantic names; match the base semantic name.

All model roots have position zero, identity scale and a ground-level pivot; terrain's ground plane is Y=0 and its soil/slate extends below it. The manifest records exact Unity-coordinate bounds for each asset. Source coordinates are converted as Blender=(UnityX,-UnityZ,UnityY); FBX uses -Z forward and Y up. An initial Z inversion was found by Unity integration and corrected in this generator. Verify imported terrain centre Z is positive, approximately 6 m.

## Materials and integration

Each semantic material exports matching `diffuse_color` and Principled Base Color. Manifest palette values are Blender linear RGB. Unity should read the imported material color, then assign the project's mobile URP material; the kit does not require external textures. Material names identify moss, soil, slate, foliage, bark, warm wood, terracotta, enamel, brass and creek water. A neutral `ArtTint` vertex-color channel provides subtle per-face variation when the runtime shader supports it.

The FBX kit is geometry only. Runtime fire, wetness, scorch, flow, searches and interaction highlights belong to the Unity shader/feedback integration. The two fixture models are optional decorative bodies and must not introduce a second valve/port interaction path.

## Verification and limits

The generator asserts a 40,000-triangle ceiling for the sum of the nine unique models. Exact geometry and material counts are in `ArtV2-Manifest.json`. This sum is not the instantiated scene budget: repeated trees, props, gameplay effects and URP passes add cost. PICO 4 and Quest 3 profiling remains required.

Blender preview inspection caught and corrected overlapping terrain-cap triangles, oversized outer rim rocks, an inverted export depth axis, floating rear decoration and unrooted fern stems. The final preview has a continuous moss surface and readable cabin/tree/fixture silhouettes. Unity scene screenshots, interactions and device acceptance are separate evidence owned by the integration task.

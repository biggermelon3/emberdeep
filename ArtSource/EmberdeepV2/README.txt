EMBERDEEP ART V2 / 8 November 2026

This is a separate new art delivery. The original FireRescue blockout assets are retained.

Concepts/Emberdeep-ArtV2-Concept.png is an AI-generated direction reference, not a game capture.
Concepts/Generation.json records the complete prompt and built-in generation tool.
Emberdeep-ArtV2.blend contains original editable Blender meshes and the preview scene.
Emberdeep-ArtV2-Preview.png is a Blender render; it is not evidence of headset rendering.
ArtV2-Manifest.json records pivots, bounds, semantic materials, geometry and provenance.

Reproduce geometry:
  "C:/Program Files/Blender Foundation/Blender 4.3/blender.exe" --background --python Tools/BuildEmberdeepArtV2.py
Run from the Unity project root. Outputs go to ArtSource/EmberdeepV2 and Assets/FireRescue/ArtV2/Models.
In Unity, run Emberdeep > Art V2 > Import Blender kit (EmberdeepArtBuilder.Build).
This generates only EmberdeepArtV2 prefabs, materials and its Resources library. It does not regenerate scenes.

Unity coordinates: metres, Y up, Z forward, scale one. Forest models use simulation units;
the existing arena scales the whole forest by .08. Station shell meshes use real physical metres.
The terrain is approximately 10.8 x 12.9 forest units. The front remains accessible and the creek is at the back.
Dynamic target trees, heat, water, reward and interaction state remain owned by gameplay.

Station shell integration hides cosmetic control copies. Existing Socket/Valve/Pump/Intake/IntakeSocket
and equipment RefillPort anchors remain authoritative; mirrored hands do not use negative scaling.

Unity shaders:
  Emberdeep/V2 Sculpted Surface: pigment, wetness, heat, scorch, roughness and small canopy movement.
  Emberdeep/V2 Shallow Creek: opaque animated shallow-water ripples without depth copies/refraction.
  Emberdeep V2 particle shader: bounded flame, smoke, steam, splash and ballistic water strands.
Runtime records, Editor captures and current verification are under Logs/EmberdeepV2.

The kit is an implemented stylized interpretation of the concept, not a pixel-identical reproduction.
Real PICO 4 / Quest 3 hand comfort, stereo rendering and performance still require device acceptance.
No external downloaded mesh, texture or audio was used in this new kit.

# Hustle part dimensions and physics port

Source: the local `E:/Unity/Hustle_ref/Hustle_Main` project, exported on 2026-10-12 using Unreal's asset loader and the existing HustleAssemblyAudit commandlet. `HustleSourcePhysics.json` records constructed Blueprint component bounds, scales and body properties. `HustleLevelScale.json` records the assembly-related actors found in Level2GamePlay and Level2_TempObjects. Neither map contained placed PhysicalAssemblySystem parts, so these exports do not establish the scale of dynamically printed cars.

The user selected a hand-sized fallback with Hustle proportions when an in-level car scale could not be established. Dimensions below convert centimetres to metres, then apply a uniform 0.2 scale. Geometry remains primitive-based; this is a dimensions/physics port, not an import of Unreal meshes or textures.

| Unity part | Source | Resulting dimensions |
|---|---|---|
| Chassis | BP_Wood / SM_Wood bounds | 25.55 cm wide × 1.77 cm thick × 52.37 cm long |
| Wheel tread | BP_Wheel / SM_Wheel bounds | 22.82 cm diameter × 14.01 cm wide |
| Wheel mount | BP_Wheel Cube1 | 16.12 × 4 × 4 cm |
| Wheel hub | BP_Wheel Cube2 | 12.21 × 4 × 4 cm |
| Beam | BP_Stick component-scaled cube | 2 × 2 × 57.92 cm |
| Block | BP_Cube component-scaled cube | 10 × 10 × 10 cm |
| Launcher | Unity-specific part; no corresponding source asset established | Existing 12 × 8 × 18 cm body retained |

## Physics mapping

- Retained separate mount, suspended hub and hinged wheel bodies, with collisions disabled between bodies of the same wheel module.
- Source wheel/mount/hub mass overrides are 10/5/5 kg; BP_Wood's mass override is 20 kg. For the miniature, these become 0.08/0.04/0.04/0.16 kg using volume scaling (0.2 cubed). Exported `get_mass()` returns zero in this headless construction world and is not used as the physical mass. Block, beam and launcher masses remain Unity tuning values.
- Linear damping is 0.01, wheel angular damping 1, chassis angular damping 2, matching the source body settings. Keep Unity's higher solver iteration counts for the compound construction.
- Suspension travel is 30 cm × 0.2 = 6 cm. Unity force-based suspension uses 120 N/m and 6 N·s/m damping. This is an explicit PhysX adaptation of the source acceleration-mode drive, not a numeric copy. Angular suspension motion remains locked for stability, rather than importing the source's 11-degree strongly driven swing limits.
- The actual BP_Wheel material override is Engine PhysMat_Rubber (friction 0.7), not the project's PM_Rubber (friction 10). Unity uses 0.7 friction with Average combine and zero bounce; Unreal's restitution of 1 is deliberately not reproduced for this tabletop car.
- Retained the audited ControlStick torque curve with angular speed supplied in degrees/sec. Unity applies fixed-step torque with a 0.12 N·m maximum, 0.04 N·m brake limit and 12 rad/sec speed limit. At 11.41 cm radius, the no-slip top speed is about 1.37 m/s. Reversing retains opposing torque even when above the forward speed limit. Source per-Tick angular impulses are not copied verbatim: their engine units, time dependence and full-size inertia differ.
- Differential steering is supplemented by one chassis yaw controller, targeting 60 degrees/sec with response gain 40/sec and a 60 rad/sec² acceleration bound. The controller computes torque using the connected assembly's yaw inertia and requires at least two grounded connected wheels. This replaces the ineffective 0.08 N·m per-wheel assistance. The initially proposed gain 8 and acceleration bound 12 produced only 9.3 degrees in 1.5 seconds; the tuned settings produce roughly 48–52 degrees with the default geometry. A/D command left/right heading changes during both forward and reverse driving. These settings are Unity gameplay adaptations, not exact Chaos/PhysX equivalence.

## Assets and persistence

`MetaHackthone > Rebuild part prefabs` updates the existing prefab assets and generated mesh/material assets while retaining GUIDs. It does not regenerate the scene or XR setup. Reset Kit and Quick Drive instantiate these prefabs; the runtime fallback factory uses the same definitions.

New cars use save format 2 and `Constructions/car-v2.json`. Old `car-v1.json` files remain untouched; their body positions and joint anchors do not fit the new dimensions and are not silently loaded into the new geometry. Rebuild old cars using the resized kit.

## Validation

The prefab-based Quick Drive test passed forward/reverse, firing, reset and save/load. The compound-car physics test passed forward motion, both steering directions, upright stability and detached-wheel input isolation. Headset comfort and driving feel still require a Quest run; Editor tests cannot establish those subjective results.

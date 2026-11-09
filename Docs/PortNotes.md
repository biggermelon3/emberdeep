# PhysicalAssemblySystem port notes

Source: `E:/Unity/Hustle_ref/Hustle_Main/UnityPortDoc/PhysicalAssemblySystem-{Audit,BlueprintBehavior,UnityCSharpDesign}.md`. Hustle remains a read-only reference.

## Source mapping

| Hustle concept | Unity implementation |
|---|---|
| BP_AssemblySystem candidate search / glue / separation | AssemblyWorld: body queries, sockets, alignment coroutine, explicit graph, connected-component traversal |
| BP_FunctionLibrary CalcMatrix / FindSnapRotate | HustleBehavior: normalized blended axes, sign/rotation variants, nearest forward and orthogonal frame search |
| BP_Glue animation | Audited 0.25-second keys and tangents in HustleBehavior.GlueCurve |
| BP_Wheel | Three bodies: mount, suspension hub, rotating wheel; internal ConfigurableJoint and HingeJoint |
| C_WheelTorqueCurve_ControlStick | Audited keys and tangents in WheelMotor.TorqueCurve; evaluated in degrees/second |
| BP_ControlSticks connected-group routing | WorkshopSession receiver list, rebuilt after topology changes |
| Save structures | Versioned JSON with stable part/body/socket IDs; generated IDs for loaded instances |
| VR controller adapter | MetaHandInputAdapter reads Interaction SDK IHand poses and pinch state; AssemblyWorld owns movement |
| Vehicle weapon | New pooled toy-projectile system; not a port of BP_BulletButton's power-switch behavior |

The Meta adapter deliberately does not attach an independent SDK grab transformer to physics bodies. Pinch selection and poses go to the assembly movement coordinator, preserving one writer for a whole connected group. Meta's comprehensive rig supplies hand data and hand visuals. Finger extension is measured from tracked joint directions, with tunable thresholds and temporal debounce in a package-independent recognizer.

## Unity tuning

- Metres, kilograms, fixed step 1/60 s; solver iterations 12 / velocity iterations 4.
- Chassis: 0.32 × 0.065 × 0.60 m, 1.5 kg. The completed car is about 0.6 m long.
- Wheel module: mount 0.15 kg, hub 0.12 kg, wheel 0.20 kg; wheel radius 0.09 m.
- Toy-scale suspension travel ±0.025 m, spring 350, damping 18. This is deliberately scaled down from Hustle's 0.30 m source travel.
- Peak wheel torque 0.22 Nm, neutral brake 0.12 Nm, speed envelope 24 rad/s. Source torque curve is normalized by 350 and applied about the wheel axle with reaction torque to the hub.
- Steering mixes demand by the wheel's chassis-relative side. This is a controllable differential-drive adaptation of Hustle's position-dependent steering, not a claim of identical Chaos trajectories.
- Full steering at full throttle brakes the inside wheels. This avoids weak yaw against tire friction at the application's 60 Hz physics step; both turning directions are covered at that step in play-mode tests.
- Fixed external glue joints; unbreakable in this prototype. Internal joints never enter the external connection graph.
- Wheel modules accept external glue on the mount body. Tires and suspension hubs remain grabbable but cannot be accidentally glued solid to the chassis.
- Release-to-attach moves the released group onto the target group. An unheld target stays stationary during alignment. Failed alignment restores snapshots.
- When the target group is held, alignment follows that hand and merges the newly attached group into its hold. Joint anchors use current rigidbody poses. Grip following disables interpolation until release and preserves each body's original physics state.
- Near grabs use thumb/index fingertip distance independently of the menu selector: close at 20 mm, release at 25 mm (both normalized by hand scale). Release occurs on the first separated tracked sample, at the last held pose; curled fingers and provider pinch flags cannot keep holding. Tracking interruptions below 350 ms preserve the hold, while longer loss releases without attachment. Commands stop immediately on missing hand data.
- Socket capture 4 cm, surface capture 10 cm, candidate refresh 30 Hz. Authored sockets take priority over free surface candidates.
- Initial 50-part UI limit is a guardrail, not a measured mobile performance claim.

## Delivery boundaries

Car-first catalog: chassis, wheel, beam, block, launcher. Generated Unity meshes/materials are used instead of Unreal binary assets. The project uses one local save slot. Full catalog migration, controller interactions, multiplayer, persistent spatial anchors, real-room collision meshes, and automatic gear meshing are outside this delivery.

## Device acceptance

1. Fresh launch: passthrough and both tracked hand visuals appear; no controller is needed.
2. Pinch-grab with each hand, attach four wheels and launcher, then drive. Check every pose individually and while combining two hands.
3. Test partially curled fingers, gesture transitions, UI pointing, system menu, hand occlusion, and headset removal. Confirm no stuck throttle or repeated fire after tracking loss.
4. Detach one wheel, reconnect it, and verify suspension/axle motion remains intact.
5. Save, restart, load, and drive; verify malformed saves leave existing constructions intact.
6. Run for ten minutes, then profile 72 Hz frame timing, physics time, and allocation rates on Quest 3 and 3S separately. Human comfort and pose reliability require actual hands-on testing.

Do not infer completion of these checks from a successful build or ADB launch. Recorded automated and device results belong in `Validation.md`.

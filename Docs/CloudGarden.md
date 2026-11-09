# CloudGarden demo

CloudGarden's garden gameplay lives in `Assets/CloudGarden`. It includes cloud manipulation, rain and lightning, cloud resources, ecology, six guided lessons, free play, undo/reset, and the existing device-local checkpoint/resume flow. The source project in `Tmp/CloudGarden` is unchanged. Its separate clay/sculpting demos and original XR player were not imported.

## Play

Start the existing Workshop scene and select **CloudGarden** in the desktop toolbar or Quest workshop menu. Release held parts and wait for attachments first. **Return to Workshop** restores the same unsaved construction, selection, and arena placement in Build mode. The workshop remains loaded but inactive during the garden, including loose parts and physics objects.

For direct entry, open `Assets/CloudGarden/Scenes/CloudGardenDemo.unity`, or use **MetaHackthone > CloudGarden > Play demo**. Direct entry uses the MetaHackathon player prefab; returning loads a new Workshop.

Desktop: drag clouds to move; right-drag or scroll to shape; Shift+scroll changes height; Tab selects a cloud. Keys 1/2/3 trigger rain/spark/seed, Z undoes, R resets, and Enter continues. The garden UI exposes the same actions and Return to Workshop.

Quest: use MetaHackathon's existing hands. Pinch to hold/release, use two hands to stretch, and point/pinch at garden buttons. Fist, kneading and patting retain the garden's gesture rules. The garden is placed ahead of the initial tracked position, 78 cm above the Stage floor; the player tracking origin is never repositioned. Passthrough remains active.

## Integration

`MetaPlayerRig` shares the existing room-scale player setup. `MetaGardenHands` supplies the garden's `IHandPoseProvider` interface from Meta joints. `CloudGardenGame.ConfigurePlayer` accepts the existing camera/provider before activation and does not install a tracking driver or XR Hands subsystem.

`GardenNavigation` loads the demo additively, disables workshop-owned roots while retaining its player, and restores them on return. Repeated transition requests are ignored. Missing/load-failed garden scenes restore the workshop. Garden progress follows the source's explicit saved-checkpoint resume behavior; entering a new garden session does not silently resume it.

The standalone player prefab is extracted from the Workshop scene. After intentionally changing that rig, rebuild it with **MetaHackthone > CloudGarden > Create or rebuild demo**, with open scenes saved. This rebuilds the garden scene/prefab and preserves the Workshop scene. It also registers both build scenes. The Quest build uses all enabled build scenes with Workshop first.

TextMesh Pro resources and their licenses are included under the CloudGarden folder. Project packages, render-pipeline settings, and XR configuration remain those of MetaHackathon.

## Checks

Garden EditMode tests cover rules, resource accounting and gestures. Garden PlayMode tests cover all six lessons, save/resume, shaping, weather, UI input, and tracking recovery. Navigation tests cover direct entry, repeated round trips with a connected car, frozen workshop bodies, preserved selection/connections, one player, and blocked entry while holding a part. Existing workshop tests remain part of the regression suite.

Quest hand tracking, gesture calibration, passthrough and physical UI comfort require an on-device smoke test. Desktop tests do not certify those behaviors.

Integration validation: 62/62 EditMode tests passed. The full PlayMode run passed 31/34 cases, including all 12 garden/navigation cases; the remaining three passed on a targeted rerun after updating the expected workshop button count and using Full HD for the existing desktop input tests. At a small Game view size, the workshop toolbar covers their test target. Results are in `Logs/CloudGarden-EditTests.xml`, `Logs/CloudGarden-AllPlayTests.xml`, and `Logs/CloudGarden-RegressionRerun.xml`. A rendered demo preview is in `Logs/CloudGarden-Demo.png`.

Only Windows standalone support is installed in this editor; Android build and headset acceptance remain unverified. `CloudGarden.Editor.CloudGardenDemoBuilder.BuildWindows` builds both enabled scenes to `Builds/CloudGardenDemo/MetaHackathon.exe` for desktop validation.

The Windows development build succeeded with zero errors and includes Workshop followed by CloudGardenDemo. The generated executable is `Builds/CloudGardenDemo/MetaHackathon.exe`.

The build reported one environment warning: passthrough over Meta Quest Link is disabled on this machine. Desktop mouse/keyboard validation does not use Link; headset passthrough must be checked separately.

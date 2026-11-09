using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace CloudGarden
{
    public sealed class GardenInteraction : MonoBehaviour
    {
        public CloudGardenGame game;
        public IHandPoseProvider hands;
        public IHandPoseProvider PoseProvider { get; set; }
        bool dragging, shaping;
        Vector3 offset;
        readonly bool[] gripping = new bool[2], previousPinch = new bool[2];
        readonly bool[] pinchGrab = new bool[2];
        readonly float[] lastSeen = new float[2], dwell = new float[2];
        readonly GardenHandPose[] poses = new GardenHandPose[2];
        float previousSpan;
        Vector3 previousCenter;
        bool previouslyBoth;
        bool xrGesture;
        readonly bool[] wasTracked = new bool[2];
        readonly GardenGestureRecognizer gestures = new GardenGestureRecognizer();
        CloudShapeController gestureCloud;
        float handTime;
        Matrix4x4 lastGardenMatrix;

        void Update()
        {
            if (game == null || game.ActiveCloud == null) return;
            if (game.IsXR || PoseProvider != null) ProcessHands(Time.unscaledDeltaTime); else UpdateMouse();
        }

        public void CancelGesture()
        {
            dragging = shaping = previouslyBoth = xrGesture = false;
            for (int i = 0; i < 2; i++)
            { gripping[i] = pinchGrab[i] = previousPinch[i] = wasTracked[i] = false; dwell[i] = 0; poses[i] = default; }
            gestures.Reset(); gestureCloud = null;
        }

        void OnDisable() => CancelGesture();
        void OnApplicationFocus(bool focused) { if (!focused) CancelGesture(); }
        void OnApplicationPause(bool paused) { if (paused) CancelGesture(); }

        void UpdateMouse()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (mouse == null) return;
            Ray ray = game.ViewCamera.ScreenPointToRay(mouse.position.ReadValue());
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!overUI && mouse.leftButton.wasPressedThisFrame && Physics.Raycast(ray, out var hit, 20))
            {
                var cloud = hit.collider.GetComponentInParent<CloudShapeController>();
                if (cloud != null)
                {
                    game.Select(cloud); game.SaveUndo(); dragging = true;
                    offset = cloud.transform.position - OnPlane(ray, game.WorldToGarden(cloud.transform.position).y);
                }
            }
            var active = game.ActiveCloud;
            if (dragging && mouse.leftButton.isPressed)
                active.transform.position = game.ClampCloud(OnPlane(ray, game.WorldToGarden(active.transform.position).y) + offset);
            if (dragging && mouse.leftButton.wasReleasedThisFrame)
            { dragging = false; game.ReleaseCloud(); }
            if (!overUI && mouse.rightButton.wasPressedThisFrame) { game.SaveUndo(); shaping = true; }
            if (shaping && mouse.rightButton.isPressed)
            {
                var delta = mouse.delta.ReadValue();
                active.Sculpt(active.shape.x + delta.x * .007f, active.shape.y + delta.y * .006f);
            }
            if (mouse.rightButton.wasReleasedThisFrame) shaping = false;
            if (!overUI && Mathf.Abs(mouse.scroll.ReadValue().y) > .1f)
            {
                game.SaveUndo();
                float step = Mathf.Sign(mouse.scroll.ReadValue().y);
                if (keyboard != null && keyboard.shiftKey.isPressed)
                    active.transform.position = game.ClampCloud(active.transform.position + game.GardenRoot.up * step * .04f);
                else active.Sculpt(active.shape.x + step * .1f, active.shape.y - step * .04f);
            }
            if (keyboard == null) return;
            if (keyboard.digit1Key.wasPressedThisFrame) game.ToggleRain();
            if (keyboard.digit2Key.wasPressedThisFrame) game.Storm();
            if (keyboard.digit3Key.wasPressedThisFrame) game.GrowSeed();
            if (keyboard.zKey.wasPressedThisFrame) game.Undo();
            if (keyboard.rKey.wasPressedThisFrame) game.RestartLevel();
            if (keyboard.tabKey.wasPressedThisFrame) game.SelectNext();
            if (keyboard.enterKey.wasPressedThisFrame) game.NextLevel();
            if (keyboard.kKey.wasPressedThisFrame) game.KneadCloud();
            if (keyboard.pKey.wasPressedThisFrame) game.PatCloud(game.ActiveCloud.transform.position + game.GardenRoot.up * .1f);
            if (keyboard.f10Key.wasPressedThisFrame) game.SetVirtualEnvironmentVisible(!game.VirtualEnvironmentVisible);
        }

        Vector3 OnPlane(Ray ray, float height)
        {
            var plane = new Plane(game.GardenRoot.up, game.GardenToWorld(Vector3.up * height));
            return plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : game.ActiveCloud.transform.position;
        }

        public void ProcessHands(float dt)
        {
            var provider = PoseProvider ?? hands;
            if (provider == null || game.ActiveCloud == null) return;
            if (lastGardenMatrix != game.GardenRoot.localToWorldMatrix)
            { CancelGesture(); lastGardenMatrix = game.GardenRoot.localToWorldMatrix; }
            if (dt <= 0 || dt > .1f) { CancelGesture(); return; }
            handTime += dt;
            var left = provider.Read(true); var right = provider.Read(false);
            if (gestureCloud != game.ActiveCloud) { gestures.Reset(); gestureCloud = game.ActiveCloud; }
            var gesture = gestures.Step(left, right, game.ActiveCloud.transform.position, game.GardenRoot.up,
                game.ActiveCloud.Radius + .08f, dt, !gripping[0] && !gripping[1], game.settings);
            if (gesture.toggleRain) game.ToggleRain();
            if (gesture.knead) game.KneadCloud();
            if (gesture.pat) game.PatCloud(gesture.patPoint);
            for (int i = 0; i < 2; i++)
            {
                var pose = i == 0 ? left : right;
                game.ShowHand(i, pose);
                if (!pose.tracked)
                {
                    wasTracked[i] = false; previouslyBoth = false;
                    if (handTime - lastSeen[i] > game.settings.trackingGrace)
                    { gripping[i] = false; dwell[i] = 0; previousPinch[i] = false; }
                    continue;
                }
                bool regained = !wasTracked[i] || (pose.palm - poses[i].palm).magnitude > dt * 2.5f + .01f;
                wasTracked[i] = true;
                if (regained) previouslyBoth = false;
                lastSeen[i] = handTime;
                bool pressed = pose.pinching && !previousPinch[i];
                previousPinch[i] = pose.pinching;
                if (!gripping[i] && pressed && game.Hud.TryHandButton(pose)) { poses[i] = pose; continue; }
                var candidate = gripping[1 - i] ? game.ActiveCloud : game.NearestCloud(pose.palm);
                float reach = candidate == null ? 99 : Vector3.Distance(candidate.transform.position, pose.palm);
                if (!gripping[i] && candidate != null && reach < candidate.Radius + .08f && !GardenGestureRecognizer.IsFist(pose))
                {
                    dwell[i] += dt;
                    if (pose.pinching || dwell[i] > .35f)
                    {
                        if (!gripping[1 - i]) { game.Select(candidate); game.SaveUndo(); }
                        gripping[i] = true; pinchGrab[i] = pose.pinching; xrGesture = true; poses[i] = pose; regained = true;
                    }
                }
                else if (!gripping[i]) dwell[i] = 0;
                if (gripping[i])
                {
                    if (pose.pinching) pinchGrab[i] = true;
                    if (pinchGrab[i] && !pose.pinching) { gripping[i] = false; dwell[i] = -.65f; }
                    if (!pose.pinching && reach > game.ActiveCloud.Radius + .16f) gripping[i] = false;
                    if (!gripping[1 - i] && !regained)
                        game.ActiveCloud.transform.position = game.ClampCloud(game.ActiveCloud.transform.position + pose.palm - poses[i].palm);
                    if (pose.pinching && !gripping[1 - i]) game.ActiveCloud.Indent(pose.tip, .018f);
                }
                poses[i] = pose;
            }
            bool both = gripping[0] && gripping[1];
            if (both)
            {
                float span = Vector3.Distance(poses[0].palm, poses[1].palm);
                Vector3 center = (poses[0].palm + poses[1].palm) * .5f;
                if (previouslyBoth && left.tracked && right.tracked)
                {
                    var cloud = game.ActiveCloud;
                    cloud.Sculpt(cloud.shape.x + (span - previousSpan) * 4, cloud.shape.y - (span - previousSpan) * 1.7f);
                    cloud.transform.position = game.ClampCloud(cloud.transform.position + center - previousCenter);
                }
                previousSpan = span; previousCenter = center;
            }
            previouslyBoth = both;
            if (xrGesture && !gripping[0] && !gripping[1]) { xrGesture = false; game.ReleaseCloud(); }
        }
    }
}

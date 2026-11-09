using CloudGarden.ShadowPuzzle;
using UnityEngine;

namespace FireRescue
{
    // Simulation coordinates stay in forest units; only the diorama is scaled, never the player or hands.
    public static class FireMiniature
    {
        public const float Scale = .08f;
        public const float MuzzleOffset = .135f;
        public static readonly Vector3 DesktopGripLocal = new Vector3(.45f,.17f,.50f);
        public static void Place(Transform arena, Vector3 head, Vector3 forward, float floor)
        {
            forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            var rotation = Quaternion.LookRotation(forward);
            var center = head + forward * .8f;
            center.y = Mathf.Max(floor + .45f, head.y - .65f);
            arena.localScale = Vector3.one * Scale;
            arena.SetPositionAndRotation(center - rotation * new Vector3(0, 0, 6 * Scale), rotation);
        }
        public static Ray ToForest(Transform arena, Ray ray) => new Ray(arena.InverseTransformPoint(ray.origin), arena.InverseTransformDirection(ray.direction));

        // Converge the hand-held barrel on the same point as Meta's pointer, avoiding near-field parallax.
        public static Ray FromHand(Transform arena, Pose palm, Pose pointer)
        {
            var pointerRay = new Ray(pointer.position, pointer.rotation * Vector3.forward);
            var plane = new Plane(arena.up, arena.TransformPoint(Vector3.up * .7f));
            var target = plane.Raycast(pointerRay, out float distance) && distance < 2.5f
                ? pointerRay.GetPoint(distance) : pointerRay.GetPoint(1.2f);
            var direction = (target - palm.position).normalized;
            return new Ray(palm.position + direction * MuzzleOffset, direction);
        }
    }

    public sealed class FireSprayGesture
    {
        readonly ShadowPinchGesture pinch = new ShadowPinchGesture();
        public bool Held { get; private set; }
        public bool Sample(bool valid, float distance, bool overMenu, float dt)
        {
            if (!valid || overMenu) { Reset(); return false; }
            pinch.Sample(true, distance, dt);
            if (pinch.Pressed) Held = true;
            if (!pinch.Closed) Held = false;
            return Held;
        }
        public void Reset() { Held = false; pinch.Reset(); }
    }
}

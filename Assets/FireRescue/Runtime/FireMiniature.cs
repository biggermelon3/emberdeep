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
        // LoopSpec §5 Reach, Emberdeep flow: the compact board in front of a seated player. The front tree row is
        // EyeToFrontRow ahead of the eye (horizontally, along the flat heading) and the board top EyeDrop below it,
        // fine-tuned by the play-area options (EmberdeepPlayArea: + height raises, + distance pushes away). tilt (degrees,
        // EmberdeepPlayArea.TiltDegrees) pitches the arena toward the player about the front row's line on the board top,
        // so the front row stays put and the back rises (EmberdeepBoardLayout.TiltDegrees). The floor guard keeps the
        // lowest board-top point (the front tip, which dips when tilted) FloorClearance above the floor; callers place
        // only after the head-pose gate (EmberdeepMenuPose.MinimumEyeHeight).
        public static void PlaceCompact(Transform arena, Vector3 head, Vector3 forward, float floor, float height = 0, float distance = 0, float tilt = 0)
        {
            var rotation = CompactRotation(forward, tilt);
            var pivot = CompactPivot(head, forward, floor, height, distance, tilt);
            arena.localScale = Vector3.one * Scale;
            arena.SetPositionAndRotation(pivot - rotation * new Vector3(0, 0, EmberdeepBoardLayout.FrontRowZ * Scale), rotation);
        }
        // The world point the compact board pivots about: the front row's centre on the board top.
        public static Vector3 CompactPivot(Vector3 head, Vector3 forward, float floor, float height = 0, float distance = 0, float tilt = 0)
        {
            forward = FlatHeading(forward);
            var pivot = head + forward * (EmberdeepBoardLayout.EyeToFrontRow + distance);
            pivot.y = Mathf.Max(floor + EmberdeepBoardLayout.FloorClearance + EmberdeepBoardLayout.FrontTipDip(tilt),
                head.y - EmberdeepBoardLayout.EyeDrop + height);
            return pivot;
        }
        // The compact arena's frame with the tilt taken out: same pivot (the front row's centre), flat heading, same
        // scale. Equal to the arena's localToWorld when flat. Things struck or held by a real hand (the pump) stand in
        // this frame so they stay upright in the world and where the flat board puts them.
        public static Matrix4x4 CompactUpright(Vector3 head, Vector3 forward, float floor, float height = 0, float distance = 0, float tilt = 0)
        {
            var rotation = CompactRotation(forward);
            var pivot = CompactPivot(head, forward, floor, height, distance, tilt);
            return Matrix4x4.TRS(pivot - rotation * new Vector3(0, 0, EmberdeepBoardLayout.FrontRowZ * Scale), rotation, Vector3.one * Scale);
        }
        // Flat heading, then pitched about the board's x axis: a positive tilt raises +z (the back) toward the eye.
        public static Quaternion CompactRotation(Vector3 forward, float tilt = 0) =>
            Quaternion.LookRotation(FlatHeading(forward)) * Quaternion.Euler(-Mathf.Max(0, tilt), 0, 0);
        static Vector3 FlatHeading(Vector3 forward)
        {
            forward = Vector3.ProjectOnPlane(forward, Vector3.up);
            return forward.sqrMagnitude < 1e-4f ? Vector3.forward : forward.normalized;
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

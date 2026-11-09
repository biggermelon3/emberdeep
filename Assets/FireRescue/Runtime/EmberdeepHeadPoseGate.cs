using UnityEngine;
using UnityEngine.XR;

namespace FireRescue
{
    /// <summary>Read-only headset pose validation, independent of hand or eye tracking.</summary>
    public static class EmberdeepHeadPoseGate
    {
        /// <summary>
        /// World pose and floor must share a coordinate system. Raw position is the XR device position
        /// before rig height offsets. Unavailable optional features do not impersonate a reported failure;
        /// callers must also require their device-valid, focus and presence policy.
        /// </summary>
        public static bool Allows(Pose world, float floor, bool trackedReadable, bool tracked,
            bool stateReadable, InputTrackingState state, TrackingOriginModeFlags origin,
            bool positionReadable, Vector3 rawPosition)
        {
            if (!Finite(world.position) || !Finite(world.rotation) || !Finite(floor)) return false;
            float eyeHeight = world.position.y - floor;
            if (!Finite(eyeHeight) || eyeHeight < EmberdeepMenuPose.MinimumEyeHeight) return false;
            float norm = world.rotation.x * world.rotation.x + world.rotation.y * world.rotation.y
                + world.rotation.z * world.rotation.z + world.rotation.w * world.rotation.w;
            if (!Finite(norm) || norm < .000001f) return false;
            if (trackedReadable && !tracked) return false;
            const InputTrackingState required = InputTrackingState.Position | InputTrackingState.Rotation;
            if (stateReadable && (state & required) != required) return false;
            if (origin == TrackingOriginModeFlags.Unknown) return false;
            if (positionReadable)
            {
                if (!Finite(rawPosition)) return false;
                // Floor tracking reports physical eye height. A prefab camera offset must not make
                // an initial zero pose pass validation. Device tracking legitimately starts at zero.
                if ((origin & TrackingOriginModeFlags.Floor) != 0
                    && rawPosition.y < EmberdeepMenuPose.MinimumEyeHeight) return false;
            }
            return true;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static bool Finite(Quaternion value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);
    }
}

using UnityEngine;

namespace FireRescue
{
    /// <summary>World-space menu placement policy; never writes the tracked camera or follows it after placement.</summary>
    public sealed class EmberdeepMenuPose
    {
        public const float RequiredStableSeconds = .35f;
        public const float PositionTolerance = .05f;
        public const float HeadingToleranceDegrees = 5f;
        public const float MinimumEyeHeight = .45f;
        public const float PanelDistance = 1.05f;

        public bool IsPlaced { get; private set; }
        public float StableSeconds { get; private set; }
        public Pose Placement { get; private set; }

        bool hasSample, hasHeading;
        Vector3 samplePosition, sampleForward, lastHorizontalForward;
        float sampleFloor;

        /// <summary>
        /// Returns true once per placement request. Supply camera pose and floor in the same world space,
        /// with trackingReady including focus/presence. Invalid input restarts acquisition, never times out.
        /// The owner requests recenter after a tracking-origin change; an already placed panel stays anchored.
        /// </summary>
        public bool TryUpdate(bool trackingReady, Pose head, float floorHeight, float deltaTime, out Pose panelPose)
        {
            panelPose = Placement;
            if (IsPlaced) return false;
            if (!trackingReady || !Finite(head.position) || !Finite(head.rotation) || !Finite(floorHeight)
                || !Finite(deltaTime) || deltaTime < 0 || head.position.y - floorHeight < MinimumEyeHeight)
            {
                ResetSample();
                return false;
            }

            float norm = head.rotation.x * head.rotation.x + head.rotation.y * head.rotation.y
                + head.rotation.z * head.rotation.z + head.rotation.w * head.rotation.w;
            if (!Finite(norm) || norm < .000001f)
            {
                ResetSample();
                return false;
            }
            float inverseNorm = 1f / Mathf.Sqrt(norm);
            var rotation = new Quaternion(head.rotation.x * inverseNorm, head.rotation.y * inverseNorm,
                head.rotation.z * inverseNorm, head.rotation.w * inverseNorm);
            var forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);
            // Near vertical, yaw is ill-conditioned. Keep the last observed horizontal heading instead.
            if (forward.sqrMagnitude >= .01f)
            {
                lastHorizontalForward = forward.normalized;
                hasHeading = true;
            }
            if (!hasHeading)
            {
                ResetSample();
                return false;
            }
            forward = lastHorizontalForward;
            if (!hasSample || Vector3.Distance(head.position, samplePosition) > PositionTolerance
                || Vector3.Angle(forward, sampleForward) > HeadingToleranceDegrees
                || Mathf.Abs(floorHeight - sampleFloor) > PositionTolerance)
            {
                hasSample = true;
                samplePosition = head.position;
                sampleForward = forward;
                sampleFloor = floorHeight;
                StableSeconds = 0;
                return false;
            }

            // A loading hitch is not evidence of multiple stable tracking observations.
            StableSeconds += Mathf.Min(deltaTime, .1f);
            if (StableSeconds + .000001f < RequiredStableSeconds) return false;
            var position = head.position + forward * PanelDistance;
            position.y = head.position.y;
            Placement = new Pose(position, Quaternion.LookRotation(forward, Vector3.up));
            IsPlaced = true;
            panelPose = Placement;
            return true;
        }

        /// <summary>Invalidate the placement and stability window, preserving heading for vertical gaze.</summary>
        public void RequestRecenter()
        {
            IsPlaced = false;
            ResetSample();
        }

        void ResetSample()
        {
            hasSample = false;
            StableSeconds = 0;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static bool Finite(Quaternion value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);
    }
}

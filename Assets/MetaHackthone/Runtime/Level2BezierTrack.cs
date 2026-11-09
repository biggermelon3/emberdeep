using System;
using UnityEngine;
using UnityEngine.Splines;

namespace PhysicalAssembly
{
    // Authoring input is the SplineContainer. All runtime consumers use this saved bake.
    // Deliberately has no Awake/OnEnable/OnValidate geometry generation.
    [DisallowMultipleComponent]
    public sealed class Level2BezierTrack : MonoBehaviour
    {
        public const string BoundaryData = "Level2.SectionBoundaries";
        public const string AttachmentData = "Level2.Attachments";
        [HideInInspector] public SplineContainer Curve;
        [HideInInspector] public GravityGarage3DCourse Course;
        [HideInInspector] public TrackStrip[] Strips = Array.Empty<TrackStrip>();
        [HideInInspector] public TrackAttachment[] Attachments = Array.Empty<TrackAttachment>();
        [HideInInspector] public float[] CompatibilityFractions = Array.Empty<float>();
        [HideInInspector] public Vector3[] Samples = Array.Empty<Vector3>();
        [HideInInspector] public float[] Distances = Array.Empty<float>();
        [HideInInspector] public float TurnDistance, LowerDistance;
        [HideInInspector] public string BakedSignature, ValidationError, MeshAssetPath;
        [HideInInspector] public float DeckWidth, RailHeight;
        public float Length => Distances.Length == 0 ? 0 : Distances[Distances.Length - 1];

        public Garage3DSample SampleLocal(float distance)
        {
            if (Samples.Length < 2) throw new InvalidOperationException("Level 2 track has no saved path.");
            distance = Mathf.Clamp(distance, 0, Length);
            int i = Array.BinarySearch(Distances, distance);
            if (i < 0) i = ~i - 1;
            i = Mathf.Clamp(i, 0, Samples.Length - 2);
            var tangent = (Samples[i + 1] - Samples[i]).normalized;
            return new Garage3DSample {
                Distance = distance,
                Position = Vector3.Lerp(Samples[i], Samples[i + 1], (distance - Distances[i]) / (Distances[i + 1] - Distances[i])),
                Tangent = tangent, Normal = Vector3.ProjectOnPlane(Vector3.up, tangent).normalized,
                Section = distance < TurnDistance ? Garage3DSection.Upper : distance < LowerDistance ? Garage3DSection.ConnectionArea : distance < Length - 2 ? Garage3DSection.Lower : Garage3DSection.Finish
            };
        }

        public Garage3DSample Sample(float distance)
        {
            var s = SampleLocal(distance);
            s.Position = transform.TransformPoint(s.Position);
            s.Tangent = transform.TransformDirection(s.Tangent);
            s.Normal = transform.TransformDirection(s.Normal);
            return s;
        }

        public Garage3DSample Project(Vector3 world)
        {
            var p = transform.InverseTransformPoint(world);
            float best = float.PositiveInfinity, distance = 0;
            for (int i = 0; i < Samples.Length - 1; i++) {
                var delta = Samples[i + 1] - Samples[i];
                float t = Mathf.Clamp01(Vector3.Dot(p - Samples[i], delta) / delta.sqrMagnitude);
                float error = (p - Samples[i] - t * delta).sqrMagnitude;
                if (error < best) { best = error; distance = Mathf.Lerp(Distances[i], Distances[i + 1], t); }
            }
            return Sample(distance);
        }
    }

    [Serializable]
    public sealed class TrackStrip
    {
        public MeshFilter Filter;
        public MeshCollider Collider;
        public Mesh Mesh;
        public int Section;
        // Zero is road, -1 and +1 are rails. Dimensions are captured during migration.
        public int Side;
    }

    [Serializable]
    public sealed class TrackAttachment
    {
        public Transform Target;
        public Vector3 Offset;
        public Quaternion Rotation;
        public bool UprightPost;
        public float BaseHeight;
        public Vector3 Scale;
    }
}

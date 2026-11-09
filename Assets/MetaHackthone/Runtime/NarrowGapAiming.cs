using System.Collections.Generic;
using UnityEngine;

namespace PhysicalAssembly
{
    public sealed partial class NarrowGapController
    {
        public LaunchGesture LaunchGesture { get; } = new();
        public bool IsAiming { get; private set; }
        public bool AimBlocked { get; private set; }
        public Vector3 AimVelocity { get; private set; }
        public bool CanAim => State == ChallengeState.LaunchReady && Core && !Workshop.World.HasHolds &&
            !Workshop.World.IsBusy && !Workshop.RepositionRequested && !ModeGesture.Armed;
        readonly List<AimBody> aimBodies = new();
        readonly HashSet<Collider> aimColliders = new();
        readonly Collider[] aimOverlaps = new Collider[256];
        bool aimedLaunch;
        Quaternion aimRotation = Quaternion.identity;
        Vector3 smoothedDirection;
        struct AimBody
        {
            public AssemblyBody Body;
            public Vector3 Position;
            public Quaternion Rotation;
            public bool Kinematic;
            public RigidbodyInterpolation Interpolation;
            public CollisionDetectionMode Detection;
        }
        void CaptureSnapshot()
        {
            snapshot.Clear();
            foreach (var part in Workshop.World.Parts.Values) foreach (var body in part.Bodies)
            {
                var rb = body.Rigidbody;
                snapshot.Add(new BodyState { Body=body, Position=Workshop.Arena.InverseTransformPoint(rb.position),
                    Rotation=Quaternion.Inverse(Workshop.Arena.rotation)*rb.rotation, Detection=rb.collisionDetectionMode, Interpolation=rb.interpolation });
            }
        }
        public bool BeginAim()
        {
            if (!CanAim || IsAiming) return false;
            CaptureSnapshot(); aimBodies.Clear(); aimColliders.Clear();
            foreach (var body in FindLoad())
            {
                var rb = body.Rigidbody;
                aimBodies.Add(new AimBody { Body=body, Position=Workshop.Arena.InverseTransformPoint(rb.position),
                    Rotation=Quaternion.Inverse(Workshop.Arena.rotation)*rb.rotation, Kinematic=rb.isKinematic,
                    Interpolation=rb.interpolation, Detection=rb.collisionDetectionMode });
                if (!rb.isKinematic) rb.linearVelocity=rb.angularVelocity=Vector3.zero;
                rb.collisionDetectionMode=CollisionDetectionMode.Discrete;
                rb.isKinematic=true; rb.interpolation=RigidbodyInterpolation.None;
                foreach (var c in body.Colliders) aimColliders.Add(c);
            }
            IsAiming=true; AimBlocked=false; aimedLaunch=false; aimRotation=Quaternion.identity;
            smoothedDirection=Workshop.Arena.TransformDirection(Settings.LaunchVelocity).normalized;
            AimVelocity=smoothedDirection*Settings.LaunchVelocity.magnitude;
            Course.ShowTrajectory(Core.MainBody.Rigidbody.position,AimVelocity,aimColliders);
            Workshop.Session.Notify("Fold thumb to launch"); return true;
        }
        public static Vector3 LimitAim(Vector3 direction, Vector3 up, Vector3 fallback)
        {
            direction=direction.normalized;
            var horizontal=Vector3.ProjectOnPlane(direction,up);
            if (horizontal.sqrMagnitude<.000001f) horizontal=Vector3.ProjectOnPlane(fallback,up);
            float angle=Mathf.Clamp(Mathf.Asin(Mathf.Clamp(Vector3.Dot(direction,up),-1,1))*Mathf.Rad2Deg,5,80)*Mathf.Deg2Rad;
            return horizontal.normalized*Mathf.Cos(angle)+up*Mathf.Sin(angle);
        }
        public void UpdateAim(Vector3 direction,float deltaTime)
        {
            if (!IsAiming) return;
            if (!CanAim || direction.sqrMagnitude<.000001f) { CancelAim(); return; }
            var limited=LimitAim(direction,Up,smoothedDirection);
            smoothedDirection=LimitAim(Vector3.Slerp(smoothedDirection,limited,1-Mathf.Exp(-Mathf.Max(0,deltaTime)/.06f)),Up,limited);
            var rotation=Quaternion.LookRotation(smoothedDirection,Up)*Quaternion.Inverse(Quaternion.LookRotation(Workshop.Arena.TransformDirection(Settings.LaunchVelocity),Up));
            var step=rotation*Quaternion.Inverse(aimRotation);
            AimBlocked=!RotationClear(step);
            if (!AimBlocked)
            {
                foreach (var saved in aimBodies) if (saved.Body)
                {
                    var rb=saved.Body.Rigidbody;
                    rb.position=PadPoint+rotation*(Workshop.Arena.TransformPoint(saved.Position)-PadPoint);
                    rb.rotation=rotation*Workshop.Arena.rotation*saved.Rotation;
                    saved.Body.transform.SetPositionAndRotation(rb.position,rb.rotation);
                }
                Course.RotatePad(PadPoint,rotation); aimRotation=rotation;
                AimVelocity=smoothedDirection*Settings.LaunchVelocity.magnitude; Physics.SyncTransforms();
            }
            Course.ShowTrajectory(Core.MainBody.Rigidbody.position,AimVelocity,aimColliders);
            Workshop.Session.Notify(AimBlocked?"Aim blocked — holding last clear direction. Fold thumb to launch":"Fold thumb to launch");
        }
        bool RotationClear(Quaternion step)
        {
            Physics.SyncTransforms();
            foreach (var c in aimColliders) if (!ColliderClear(c,step)) return false;
            return !Course.PadCollider || ColliderClear(Course.PadCollider,step);
        }
        bool ColliderClear(Collider c,Quaternion step)
        {
            var position=PadPoint+step*(c.transform.position-PadPoint);
            var rotation=step*c.transform.rotation;
            var center=PadPoint+step*(c.bounds.center-PadPoint);
            int count=Physics.OverlapSphereNonAlloc(center,c.bounds.extents.magnitude+.002f,aimOverlaps,~0,QueryTriggerInteraction.Ignore);
            if (count==aimOverlaps.Length) return false;
            for(int i=0;i<count;i++)
            {
                var other=aimOverlaps[i];
                if(other==c || other==Course.PadCollider || aimColliders.Contains(other)) continue;
                if(Physics.ComputePenetration(c,position,rotation,other,other.transform.position,other.transform.rotation,out _,out float depth) && depth>.002f) return false;
            }
            return true;
        }
        void RestoreAimBodies(bool poses)
        {
            foreach(var saved in aimBodies) if(saved.Body)
            {
                var rb=saved.Body.Rigidbody;
                if(poses)
                {
                    var position=Workshop.Arena.TransformPoint(saved.Position); var rotation=Workshop.Arena.rotation*saved.Rotation;
                    rb.position=position; rb.rotation=rotation;
                    saved.Body.transform.SetPositionAndRotation(position,rotation);
                }
                rb.isKinematic=saved.Kinematic; rb.collisionDetectionMode=saved.Detection; rb.interpolation=saved.Interpolation;
                if(!rb.isKinematic) rb.linearVelocity=rb.angularVelocity=Vector3.zero;
            }
            Physics.SyncTransforms();
        }
        public void CancelAim()
        {
            LaunchGesture.Reset();
            if(IsAiming || aimedLaunch) RestoreAimBodies(true);
            if(aimedLaunch) { launchPending=false; State=ChallengeState.LaunchReady; Workshop.World.EditingEnabled=true; }
            IsAiming=false; aimedLaunch=false; AimBlocked=false; aimBodies.Clear(); aimColliders.Clear();
            if(Course) { Course.HideTrajectory(); Course.RestorePad(); }
        }
        void FreezeAimForLaunch()
        {
            LaunchGesture.Reset();
            if(!IsAiming) return;
            IsAiming=false; aimedLaunch=true; Course.HideTrajectory();
        }
    }
}

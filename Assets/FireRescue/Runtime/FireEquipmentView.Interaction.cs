using UnityEngine;

namespace FireRescue
{
    public enum FireToolRegion { Body, Trigger, Nozzle, Pump }
    public sealed partial class FireEquipmentView
    {
        public Transform Nozzle { get; private set; }
        public Transform SurfaceGrip { get; private set; }
        public Transform SupportGrip { get; private set; }
        MeshFilter[] grabMeshes;
        public void CaptureGrip(FireToolRegion region,Vector3 contact,bool left=false)
        {if(region==FireToolRegion.Body)SurfaceGrip=CaptureSurface(SurfaceGrip,"Body hand contact",new Vector3(0,Id==FireEquipmentId.Extinguisher?-.09f:.015f,.0f));}
        public void CaptureSupport(Vector3 contact)
        {SupportGrip=CaptureSurface(SupportGrip,"Support hand contact",Id==FireEquipmentId.PressureCannon?new Vector3(0,-.035f,.14f):new Vector3(0,.015f,.12f));}
        Transform CaptureSurface(Transform frame,string label,Vector3 localContact)
        {
            if(!frame){frame=new GameObject(label).transform;frame.SetParent(transform,false);}
            frame.localPosition=localContact;frame.localRotation=Quaternion.identity;return frame;
        }
        public Pose PrimaryContact(FireToolRegion region,bool left)
        {
            Transform frame=Grip;
            var kind=Id==FireEquipmentId.Extinguisher?EmberdeepHandPoseKind.ExtinguisherGrip:
                Id==FireEquipmentId.PressureCannon?EmberdeepHandPoseKind.CannonGrip:EmberdeepHandPoseKind.PistolGrip;
            if(region==FireToolRegion.Body){frame=SurfaceGrip;kind=EmberdeepHandPoseKind.BodyGrip;}
            else if(region==FireToolRegion.Nozzle){frame=Nozzle;kind=EmberdeepHandPoseKind.NozzleGrip;}
            else if(region==FireToolRegion.Pump){frame=PumpGrip;kind=EmberdeepHandPoseKind.CannonPump;}
            var contact=EmberdeepHandPose.GripContact(kind,left);
            return new Pose(transform.InverseTransformPoint(frame.TransformPoint(contact.position)),
                Quaternion.Inverse(transform.rotation)*frame.rotation*contact.rotation);
        }
        LineRenderer flexibleHose;
        readonly Vector3 nozzleRest=new(.035f,.015f,.12f);
        public Vector3 MuzzleOffset=>transform.InverseTransformPoint(Muzzle.position)-Grip.localPosition;
        void BuildHoseNozzle()
        {
            Nozzle=new GameObject("Independently held nozzle").transform;Nozzle.SetParent(transform,false);
            Nozzle.localPosition=nozzleRest;
            var horn=Tube("Discharge horn",nozzleRest,nozzleRest+Vector3.forward*.07f,.022f,trim);
            horn.transform.SetParent(Nozzle,true);
            Muzzle.SetParent(Nozzle,false);Muzzle.localPosition=Vector3.forward*.075f;
            flexibleHose=new GameObject("Flexible extinguisher hose").AddComponent<LineRenderer>();
            flexibleHose.transform.SetParent(transform,false);flexibleHose.useWorldSpace=true;flexibleHose.positionCount=9;
            flexibleHose.startWidth=flexibleHose.endWidth=.012f;flexibleHose.numCornerVertices=2;
            flexibleHose.sharedMaterial=trim;flexibleHose.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            ResetNozzle();
        }
        public void ResetNozzle()
        {if(Nozzle){Nozzle.localPosition=nozzleRest;Nozzle.localRotation=Quaternion.identity;UpdateHose();}}
        public void MoveNozzle(Pose pose)
        {
            if(!Nozzle)return;
            var outlet=transform.TransformPoint(new Vector3(.025f,.08f,0));
            Nozzle.SetPositionAndRotation(outlet+Vector3.ClampMagnitude(pose.position-outlet,.45f),pose.rotation);UpdateHose();
        }
        public void UpdateHose()
        {
            if(!flexibleHose)return;
            var a=transform.TransformPoint(new Vector3(.025f,.08f,0));var b=Nozzle.position;
            for(int i=0;i<9;i++){float t=i/8f;flexibleHose.SetPosition(i,Vector3.Lerp(a,b,t)+Vector3.down*(Mathf.Sin(t*Mathf.PI)*.055f));}
        }
        // Contact volumes follow the actual rendered parts, including rotations.
        // Grabbing the body never silently snaps it onto the functional handle.
        public bool TryGrabRegion(Vector3 contact,out FireToolRegion region,out Vector3 localPoint,bool includeHidden=false)
        {
            region=FireToolRegion.Body;localPoint=default;
            if(!float.IsFinite(contact.sqrMagnitude))return false;
            if(!includeHidden && !gameObject.activeInHierarchy)return false;
            var gripPoint=transform.InverseTransformPoint(contact);
            bool atHandle=Id==FireEquipmentId.Extinguisher
                ?Mathf.Abs(gripPoint.x)<.075f && gripPoint.y>.06f && gripPoint.y<.18f && gripPoint.z>-.145f && gripPoint.z<.05f
                :gripPoint.y<-.005f && gripPoint.z<.055f && Vector3.Distance(contact,Grip.position)<.075f;
            if(atHandle){region=FireToolRegion.Trigger;localPoint=Grip.localPosition;return true;}
            float closest=.065f;Vector3 point=default;bool found=false;
            grabMeshes??=GetComponentsInChildren<MeshFilter>(true);
            foreach(var mesh in grabMeshes)
            {
                if(!mesh.sharedMesh || (!includeHidden && !mesh.gameObject.activeInHierarchy))continue;
                var at=mesh.transform.TransformPoint(mesh.sharedMesh.bounds.ClosestPoint(mesh.transform.InverseTransformPoint(contact)));
                float distance=Vector3.Distance(contact,at);
                if(distance>closest)continue;
                found=true;closest=distance;point=at;
            }
            if(!found)return false;
            localPoint=transform.InverseTransformPoint(point);
            if(Nozzle && Vector3.Distance(contact,Nozzle.position)<.065f)region=FireToolRegion.Nozzle;
            else if(PumpGrip && Vector3.Distance(contact,PumpGrip.position)<.055f)region=FireToolRegion.Pump;
            return true;
        }
    }
}

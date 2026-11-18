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
        public bool TryPrimaryGrab(Vector3 contact,int hand,out FireToolRegion region,out Vector3 point)
        {
            if(Id!=FireEquipmentId.Extinguisher)return TryGrabRegion(contact,out region,out point)
                && region!=FireToolRegion.Nozzle && region!=FireToolRegion.Pump;
            region=FireToolRegion.Body;point=default;
            if(!gameObject.activeInHierarchy || !float.IsFinite(contact.sqrMagnitude))return false;
            var p=transform.InverseTransformPoint(contact);
            if(hand==1 && Vector3.Distance(p,Grip.localPosition)<.105f)
            {region=FireToolRegion.Trigger;point=Grip.localPosition;return true;}
            // Distinct side contacts, separated from the left-hand nozzle.
            point=new Vector3(hand==0?-.055f:.055f,-.07f,0);
            return Vector3.Distance(p,point)<.095f && (hand==0?p.x<.015f:p.x>-.015f);
        }
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
        Vector3 nozzleRest=new(-.10f,.015f,.12f);
        public bool NearNozzle(Vector3 point,float radius=.10f)
        {
            if(!Nozzle || !float.IsFinite(point.sqrMagnitude))return false;
            var a=Nozzle.position;var segment=Muzzle.position-a;
            var t=segment.sqrMagnitude>1e-6f?Mathf.Clamp01(Vector3.Dot(point-a,segment)/segment.sqrMagnitude):0;
            return Vector3.Distance(point,a+segment*t)<radius;
        }
        public Vector3 MuzzleOffset=>transform.InverseTransformPoint(Muzzle.position)-Grip.localPosition;
        void BuildHoseNozzle()
        {
            Nozzle=new GameObject("Independently held nozzle").transform;Nozzle.SetParent(transform,false);
            Nozzle.localPosition=nozzleRest;
            var horn=Tube("Discharge horn",nozzleRest,nozzleRest+Vector3.forward*.07f,.022f,trim);
            horn.transform.SetParent(Nozzle,true);
            var sleeve=Part("Cyan nozzle grasp sleeve",PrimitiveType.Cylinder,nozzleRest+Vector3.forward*.025f,new Vector3(.048f,.023f,.048f),new Color(.18f,.86f,.8f));
            sleeve.transform.localRotation=Quaternion.Euler(90,0,0);sleeve.transform.SetParent(Nozzle,true);
            var dot=Part("Grab dot - left nozzle",PrimitiveType.Sphere,nozzleRest+new Vector3(-.032f,.018f,.025f),Vector3.one*.014f,new Color(.25f,.9f,1),bright);
            dot.transform.SetParent(Nozzle,true);
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
                ?Mathf.Abs(gripPoint.x)<.085f && gripPoint.y>.025f && gripPoint.y<.20f && gripPoint.z>-.16f && gripPoint.z<.065f
                :gripPoint.y<-.005f && gripPoint.z<.055f && Vector3.Distance(contact,Grip.position)<.075f;
            if(atHandle){region=FireToolRegion.Trigger;localPoint=Grip.localPosition;return true;}
            float closest=.065f;Vector3 point=default;bool found=false;
            grabMeshes??=GetComponentsInChildren<MeshFilter>(true);
            foreach(var mesh in grabMeshes)
            {
                if(mesh.name.StartsWith("Grab dot") || !mesh.sharedMesh || (!includeHidden && !mesh.gameObject.activeInHierarchy))continue;
                var at=mesh.transform.TransformPoint(mesh.sharedMesh.bounds.ClosestPoint(mesh.transform.InverseTransformPoint(contact)));
                float distance=Vector3.Distance(contact,at);
                if(distance>closest)continue;
                found=true;closest=distance;point=at;
            }
            if(!found)return false;
            localPoint=transform.InverseTransformPoint(point);
            if(NearNozzle(contact,.075f))region=FireToolRegion.Nozzle;
            else if(PumpGrip && Vector3.Distance(contact,PumpGrip.position)<.055f)region=FireToolRegion.Pump;
            return true;
        }
    }
}

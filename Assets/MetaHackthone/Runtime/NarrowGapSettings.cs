using UnityEngine;

namespace PhysicalAssembly
{
    [CreateAssetMenu(menuName="MetaHackthone/Narrow Gap settings")]
    public sealed class NarrowGapSettings : ScriptableObject
    {
        public NarrowGapCourse CoursePrefab;
        public float PadHeight=.68f;
        public float BuildLift=.35f, BuildDamping=20f;
        public Vector3 PadSize=new(BuildingGeometry.Unit,.06f,BuildingGeometry.Unit);
        public float LoadingHeight=.55f;
        public float GapWidth=.29f, GateDistance=.9f, GateHeight=1.3f, GateThickness=.16f;
        public float TargetDistance=1.8f, TargetWidth=.9f, TargetLength=1.25f;
        public float FlightTime=.8f, AttemptDuration=8f, SettleSpeed=.15f, SettleDuration=.5f;
        public Vector3 LaunchVelocity => new(0,-Physics.gravity.y*FlightTime*.5f,TargetDistance/FlightTime);
        public Bounds LoadingBounds => new(new Vector3(0,PadHeight+LoadingHeight*.5f,0),new Vector3(PadSize.x,LoadingHeight,PadSize.z));
        public Bounds TargetBounds => new(new Vector3(0,PadHeight+.21f,TargetDistance),new Vector3(TargetWidth-.04f,.42f,TargetLength-.04f));
    }
}

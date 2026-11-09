using System;
using UnityEngine;

namespace PhysicalAssembly
{
    public enum GravityGaragePreset { BigFront, SmallFront, BothBig, BothSmall }
    [Serializable] public sealed class GravityGarageDesign
    {
        [Range(.65f,2.65f)] public float FirstDrop=2.35f;
        [Range(.08f,.52f)] public float RockRadius=.10f;
        [Range(.55f,14.35f)] public float RockX=5.75f;
        [Range(5.1f,7.1f)] public float SecondStart=6.15f;
        [Range(0,1.1f)] public float SecondDrop=.70f;
        public bool SecondRock;
        [Range(.08f,.52f)] public float Rock2Radius=.14f;
        [Range(.55f,14.35f)] public float Rock2X=10.95f;
        [Range(.55f,2.35f)] public float Gap=1.20f;
        [Range(.15f,1.2f)] public float LandingDrop=.80f;
        [Range(0,.60f)] public float LipRise;
        [Range(1.68f,3.2f)] public float ReleaseX=1.68f;
        public const float Edge=8.32f;
        // Keep both axles on a downhill starter ramp at the normal release point.
        // The former flat shelf made launch depend on which axle reached its edge.
        public const float StartRampEnd=2.7f, StartRampRise=.65f;
        public static GravityGarageDesign UnityDefaults() => new GravityGarageDesign {Gap=.8f};
        // Reference axle separation is three shared building units, matching the HTML's 1.04 m wheelbase.
        public static float UnitScale => 3*BuildingGeometry.Unit/1.04f;
        public float LandingX => Edge+Gap;
        public float LandingY => 2.96f+SecondDrop-LipRise+LandingDrop;
        public float GoalX {
            get {
                float x=LandingX+.55f;
                if(RockX>=LandingX) x=Mathf.Max(x,RockX+RockRadius+.55f);
                if(SecondRock && Rock2X>=LandingX) x=Mathf.Max(x,Rock2X+Rock2Radius+.55f);
                return x;
            }
        }
        public float SupportedX(float x) { x=Mathf.Clamp(x,.55f,14.35f); return x>Edge-.02f && x<LandingX+.02f?(x-Edge<LandingX-x?Edge-.02f:LandingX+.02f):x; }
        public Vector2[] Points => new[]{new Vector2(.18f,2.96f-FirstDrop-StartRampRise),new Vector2(StartRampEnd,2.96f-FirstDrop),new Vector2(4.45f,2.96f),new Vector2(SecondStart,2.96f),new Vector2(7.56f,2.96f+SecondDrop),new Vector2(Edge,2.96f+SecondDrop-LipRise),new Vector2(LandingX,LandingY),new Vector2(14.60f,LandingY)};
        public Vector2 Surface(float x,out Vector2 normal)
        {
            var points=Points;
            for(int i=0;i<points.Length-1;i++) if(i!=5 && x>=points[i].x-.001f && x<=points[i+1].x+.001f) {
                var d=points[i+1]-points[i]; normal=new Vector2(d.y,-d.x).normalized;
                return Vector2.Lerp(points[i],points[i+1],Mathf.Clamp01((x-points[i].x)/d.x));
            }
            throw new ArgumentOutOfRangeException(nameof(x),"Rock needs a supported surface.");
        }
    }
    [Serializable] public sealed class GravityGarageResult
    {
        public string Configuration,Outcome="Running",Stage="Descent";
        public string DesignJson;
        public float WorldScale;
        public bool Success;
        public float FinalX;
        public float Time,TakeoffSpeed=-1,MaxPitch,MaxRoll,LandingPitch,LandingRoll;
    }
}

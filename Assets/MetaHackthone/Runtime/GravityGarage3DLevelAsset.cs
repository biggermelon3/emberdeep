using UnityEngine;

namespace PhysicalAssembly
{
    [CreateAssetMenu(menuName="MetaHackthone/Gravity Garage 3D")]
    public sealed class GravityGarage3DLevelAsset : ScriptableObject
    {
        public string Title="Waterfall Valley";
        public Texture2D Thumbnail;
        [Min(2)] public float Radius=4;
        [Min(8)] public float LaneLength=14;
        public float StartHeight=8.4f, UpperDrop=2.4f, TurnDrop=3.2f, LowerDrop=1.4f;
        public float DeckWidth=2.4f, RailHeight=.65f;
        public float WeakPointHeight=.48f, WeakPointRadius=.18f, WeakPointHalfWidth=1.05f;
        public float Timeout=45;
        public bool Scenery=true;
        public float BalanceRise=.16f;
    }
    public enum Garage3DSection { Upper=0, ConnectionArea=1, Lower=2, Finish=3 }
    public struct Garage3DSample
    {
        public Vector3 Position,Tangent,Normal;
        public float Distance;
        public Garage3DSection Section;
    }
}

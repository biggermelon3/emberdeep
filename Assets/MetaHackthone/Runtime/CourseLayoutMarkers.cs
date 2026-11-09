using System.Linq;
using UnityEngine;

namespace PhysicalAssembly
{
    // Markers are parented to the relevant road section so they follow layout edits.
    public sealed class CourseLayoutMarkers : MonoBehaviour
    {
        public Transform Start, Finish, Jump, Turn, Lower, WeakPoint;
        public BoxCollider SafeRegion;
        public BoxCollider[] WaterRegions=System.Array.Empty<BoxCollider>();
        public Collider[] Roads=System.Array.Empty<Collider>();
        [Tooltip("Ordered route samples, parented to the road sections. Move sections together with their samples.")]
        public Transform[] Route=System.Array.Empty<Transform>();
        public bool Passed(Transform marker,Vector3 point) => marker && Vector3.Dot(point-marker.position,marker.forward)>=0;
        public bool Inside(Vector3 point) => !SafeRegion || new Bounds(SafeRegion.center,SafeRegion.size).Contains(SafeRegion.transform.InverseTransformPoint(point));
        public bool OnRoad(GarageContact contact) => Roads.Any(r=>r && contact.Supports(r));
        void OnDrawGizmosSelected()
        {
            Gizmos.color=Color.cyan;
            foreach(var marker in new[]{Start,Finish,Jump,Turn,Lower,WeakPoint}) if(marker) {
                float size=.12f*transform.lossyScale.x;
                Gizmos.DrawWireSphere(marker.position,size);
                Gizmos.DrawLine(marker.position,marker.position+marker.forward*size*4);
            }
            for(int i=1;i<Route.Length;i++) if(Route[i-1] && Route[i]) Gizmos.DrawLine(Route[i-1].position,Route[i].position);
            foreach(var area in WaterRegions.Concat(new[]{SafeRegion})) if(area) {
                Gizmos.matrix=area.transform.localToWorldMatrix;
                Gizmos.DrawWireCube(area.center,area.size);
            }
            Gizmos.matrix=Matrix4x4.identity;
        }
    }
}

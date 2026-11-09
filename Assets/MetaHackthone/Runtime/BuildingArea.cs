using UnityEngine;
namespace PhysicalAssembly
{
    // A visible authoring volume with no physics collider or hand-interaction surface.
    public sealed class BuildingArea : MonoBehaviour
    {
        public Vector3 Size=Vector3.one;
        public Bounds LocalBounds => new(Vector3.zero,Size);
        public bool Contains(Vector3 worldPoint) => LocalBounds.Contains(transform.InverseTransformPoint(worldPoint));
        void OnDrawGizmos()
        {
            var old=Gizmos.matrix; Gizmos.matrix=transform.localToWorldMatrix;
            Gizmos.color=new Color(.2f,1,.65f,.7f); Gizmos.DrawWireCube(Vector3.zero,Size); Gizmos.matrix=old;
        }
    }
}

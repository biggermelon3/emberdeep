using UnityEngine;

namespace CloudGarden
{
    // Replace this transform's pose with a future table-placement provider. No MR services are started here.
    public sealed class GardenPlacementAnchor : MonoBehaviour
    {
        public static Vector3 DefaultPosition => new Vector3(0, .78f, .65f);
        public void SetPose(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
        }

        public static GardenPlacementAnchor CreateDefault(Transform parent)
        {
            var anchor = new GameObject("Garden placement anchor").AddComponent<GardenPlacementAnchor>();
            anchor.transform.SetParent(parent, false);
            anchor.transform.localPosition = DefaultPosition;
            return anchor;
        }
    }
}

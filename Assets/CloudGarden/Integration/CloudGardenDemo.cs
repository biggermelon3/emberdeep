using System.Collections;
using PhysicalAssembly.MetaXR;
using UnityEngine;

namespace CloudGarden.Integration
{
    public sealed class CloudGardenDemo : MonoBehaviour
    {
        public GardenSettings Settings;
        public GameObject StandalonePlayer;
        public CloudGardenGame Game { get; private set; }
        IEnumerator Start()
        {
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(gameObject.scene);
            var navigation = GardenNavigation.Current;
            MetaPlayerRig player = navigation ? navigation.Player : null;
            if (!player)
            {
                var root = Instantiate(StandalonePlayer);
                player = root.GetComponentInChildren<MetaPlayerRig>(true);
                player.Initialize(player.XRRig, player.DesktopCamera);
                navigation = new GameObject("Garden navigation").AddComponent<GardenNavigation>();
                navigation.InitializeStandalone(player);
            }
            // Wait for tracking to settle, without delaying desktop test entry.
            if (player.IsXR)
            {
                float deadline = Time.realtimeSinceStartup + 3;
                while (player.ViewCamera.transform.position.y - player.XRRig.position.y < .4f && Time.realtimeSinceStartup < deadline) yield return null;
            }
            var content = new GameObject("CloudGarden gameplay"); content.SetActive(false); content.transform.SetParent(transform, false);
            var anchor = GardenPlacementAnchor.CreateDefault(content.transform);
            if (player.IsXR)
            {
                var head = player.ViewCamera.transform;
                var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
                if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
                float floor = player.FloorHeight;
                anchor.SetPose(new Vector3(head.position.x, floor + .78f, head.position.z) + forward * .65f, Quaternion.LookRotation(forward));
            }
            else
            {
                player.ViewCamera.transform.position = new Vector3(1.15f, 1.95f, -1.65f);
                player.ViewCamera.transform.LookAt(new Vector3(0, 1.03f, .66f));
                player.ViewCamera.fieldOfView = 37;
            }
            if(PhysicalAssembly.PlayAreaPose.IsSet)
            {
                var pose=PhysicalAssembly.PlayAreaPose.AtHeight(.78f); anchor.SetPose(pose.position,pose.rotation);
                if(!player.IsXR) { player.ViewCamera.transform.position=pose.position+pose.rotation*new Vector3(1.15f,1.17f,-2.30f); player.ViewCamera.transform.LookAt(pose.position+Vector3.up*.25f); }
            }
            var hands = content.AddComponent<MetaGardenHands>(); hands.Rig = player.XRRig;
            Game = content.AddComponent<CloudGardenGame>(); Game.settings = Settings; Game.placementAnchor = anchor;
            Game.ConfigurePlayer(player.ViewCamera, hands, player.IsXR);
            Game.ReturnToWorkshop = navigation.ReturnToWorkshop;
            content.SetActive(true);
            if (player.IsXR) Game.SetVirtualEnvironmentVisible(false);
        }
    }
}

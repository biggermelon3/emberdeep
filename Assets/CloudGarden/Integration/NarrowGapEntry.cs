using PhysicalAssembly;
using PhysicalAssembly.MetaXR;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloudGarden.Integration
{
    public sealed class NarrowGapEntry : MonoBehaviour
    {
        public NarrowGapSettings Settings;
        public GameObject StandalonePlayer;
        public BuildingSceneLayout Layout;
        public WorkshopBootstrap Workshop { get; private set; }
        void Start()
        {
            SceneManager.SetActiveScene(gameObject.scene);
            var navigation=GardenNavigation.Current;
            var player=navigation?navigation.Player:null;
            if(!player)
            {
                var rig=Instantiate(StandalonePlayer);
                player=rig.GetComponentInChildren<MetaPlayerRig>(true); player.Initialize(player.XRRig,player.DesktopCamera);
                navigation=new GameObject("Narrow Gap navigation").AddComponent<GardenNavigation>(); navigation.InitializeStandalone(player,false);
            }
            var content=new GameObject("Narrow Gap workshop"); content.SetActive(false);
            Workshop=content.AddComponent<WorkshopBootstrap>(); Workshop.ChallengeSettings=Settings; Workshop.Layout=Layout;
            Workshop.ReturnToWorkshopRequested=navigation.ReturnToWorkshop;
            var hands=content.AddComponent<MetaHandInputAdapter>(); hands.Workshop=Workshop; hands.XRRig=player.XRRig; hands.DesktopCamera=player.DesktopCamera;
            var desktop=content.AddComponent<DesktopInputAdapter>(); desktop.Workshop=Workshop; desktop.View=player.DesktopCamera;
            navigation.ChallengeWorkshop=Workshop;
            if(!player.IsXR) { player.DesktopCamera.backgroundColor=new Color(.12f,.15f,.17f); player.DesktopCamera.fieldOfView=45; }
            RenderSettings.fog=false;
            content.SetActive(true);
        }
    }
}

using PhysicalAssembly;
using PhysicalAssembly.MetaXR;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloudGarden.Integration
{
    public sealed class DownhillEntry : MonoBehaviour
    {
        public DownhillSettings Settings;
        public GameObject StandalonePlayer;
        public BuildingSceneLayout Layout;
        public WorkshopBootstrap Workshop { get; private set; }
        DownhillSettings runtimeSettings;
        void Start()
        {
            if(!DesignModeSession.Enabled) SandboxCampaignSession.Reset();
            SceneManager.SetActiveScene(gameObject.scene);
            var navigation=GardenNavigation.Current; var player=navigation?navigation.Player:null;
            if(!player)
            {
                var rig=Instantiate(StandalonePlayer); player=rig.GetComponentInChildren<MetaPlayerRig>(true); player.Initialize(player.XRRig,player.DesktopCamera);
                navigation=new GameObject("Downhill navigation").AddComponent<GardenNavigation>(); navigation.InitializeStandaloneDownhill(player);
            }
            var content=new GameObject("Downhill workshop"); content.SetActive(false);
            runtimeSettings=Instantiate(Settings); runtimeSettings.SandboxDrillCourse=true; runtimeSettings.UseGravityGarage=true; runtimeSettings.DesignerBuilder=true; runtimeSettings.Campaign=!DesignModeSession.Enabled;
            if(DesignModeSession.Enabled && DesignModeSession.SelectedLevel==DesignLevelId.GravityGarage)runtimeSettings.Level3D=null;
            var authored=Layout?Layout.GetComponent<AuthoredSandboxLayout>():null;
            if(authored) {
                if(!authored.Level1 || !authored.Level2) throw new System.InvalidOperationException("The authored sandbox is missing a course.");
                runtimeSettings.Level25D=null;
                runtimeSettings.Level3D=DesignModeSession.Enabled && DesignModeSession.SelectedLevel==DesignLevelId.GravityGarage?null:authored.Level2.Level;
            }
            Workshop=content.AddComponent<WorkshopBootstrap>(); Workshop.DownhillSettings=runtimeSettings; Workshop.Layout=Layout; Workshop.ReturnToWorkshopRequested=navigation.ReturnToWorkshop;
            var hands=content.AddComponent<MetaHandInputAdapter>(); hands.Workshop=Workshop; hands.XRRig=player.XRRig; hands.DesktopCamera=player.DesktopCamera;
            var desktop=content.AddComponent<DesktopInputAdapter>(); desktop.Workshop=Workshop; desktop.View=player.DesktopCamera;
            navigation.DownhillWorkshop=Workshop;
            if(!player.IsXR) { player.DesktopCamera.backgroundColor=new Color(.13f,.18f,.20f); player.DesktopCamera.fieldOfView=45; }
            RenderSettings.fog=false; content.SetActive(true);
        }
        void OnDestroy() { if(runtimeSettings) Destroy(runtimeSettings); }
    }
}

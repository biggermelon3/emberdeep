using UnityEngine;
using UnityEngine.InputSystem;

namespace FireRescue
{
    public sealed partial class FireRescueGame
    {
        TextMesh recoveryLabel;
        Transform recoveryDroplet;
        Mesh recoveryIconMesh;

        // Production and synthetic-provider tests share all scene gates. The
        // caller supplies current physical tracking and raw held-trigger intent,
        // never a successful shot: an empty held trigger still blocks recovery.
        public float ProcessWaterRecovery(float dt,bool trackingAvailable,bool triggerHeld)
        {
            bool eligible=Player && hands && Arena && focused && !paused && Time.timeScale>0
                && !ShopOpen && !acquiringPose && !returningToCamp && introStarted
                && hands.HeadReady && (!Player.IsXR || xrPlaced) && trackingAvailable
                && navigation && !navigation.IsTransitioning && !refill.OwnsControl
                && !refill.EmptyOutlet && desktopDock==WaterSourceKind.None && desktopOutletTravel<=0;
            return Run.TickRecovery(dt,eligible,triggerHeld || pump.Active || pump.Released);
        }

        bool RecoveryTrackingAvailable(bool activeHandValid)
        {
            if(!Player.IsXR)return true;
            if(grip.Held)return activeHandValid;
            // A resting equipped tool may recover with either hand visible.
            return hands.ReadHand(true,out _,out _,out _) || hands.ReadHand(false,out _,out _,out _);
        }

        bool RecoveryTriggerHeld(bool gestureTrigger,float pinch)
        {
            if(Player.IsXR)return gestureTrigger || (grip.Held && pinch<.035f);
            return gestureTrigger || (Mouse.current!=null && Mouse.current.leftButton.isPressed);
        }

        void RefreshRecoveryFeedback()
        {
            if(!dashboard)return;
            if(!recoveryLabel)
            {
                recoveryLabel=Text(dashboard,"",new Vector3(.055f,-.355f,-.04f),.029f,new Color(.45f,.9f,1));
                recoveryLabel.gameObject.name="Slow recovery status";
                recoveryDroplet=new GameObject("Recovery droplet").transform;
                recoveryDroplet.SetParent(dashboard,false);
                recoveryDroplet.localPosition=new Vector3(-.69f,-.355f,-.04f);
                recoveryIconMesh=new Mesh{name="Recovery droplet icon"};
                recoveryIconMesh.vertices=new[]{Vector3.zero,new Vector3(0,.031f,0),new Vector3(.016f,.005f,0),
                    new Vector3(.017f,-.01f,0),new Vector3(.01f,-.022f,0),new Vector3(-.01f,-.022f,0),
                    new Vector3(-.017f,-.01f,0),new Vector3(-.016f,.005f,0)};
                recoveryIconMesh.triangles=new[]{0,1,2,0,2,3,0,3,4,0,4,5,0,5,6,0,6,7,0,7,1};
                recoveryIconMesh.RecalculateNormals();recoveryIconMesh.RecalculateBounds();
                recoveryDroplet.gameObject.AddComponent<MeshFilter>().sharedMesh=recoveryIconMesh;
                var renderer=recoveryDroplet.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=WaterMaterial;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            bool show=!ShopOpen && (Run.RecoveryActive || Run.RecoveryWaiting);
            recoveryLabel.gameObject.SetActive(show);recoveryDroplet.gameObject.SetActive(show);
            if(show)recoveryLabel.text=Run.RecoveryActive?"SLOW RECOVERY  +0.25/s":"RECOVERY  Waiting";
            if(Run.Water<1 && !Run.Supply.Locked && Run.RecoveryActive)
                help.text="Slowly recovering / Refill at the pump for faster water";
            // The rack gauge is an actual owned tank, not a preview granting a fill.
            if(rackView && !ShopOpen)
                rackView.Animate(Run.WaterFor(SelectedEquipment)/Run.CapacityFor(SelectedEquipment),0,false,0);
        }
    }
}

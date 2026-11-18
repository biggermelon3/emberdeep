using UnityEngine;
using UnityEngine.InputSystem;

namespace FireRescue
{
    public sealed partial class FireRescueGame
    {
        readonly EmberdeepPalmGesture palmMagic=new();
        EmberdeepManaCrystal manaCrystal;
        LineRenderer palmCircle;
        EmberdeepPalmAimVfx palmMotes;
        EmberdeepInteractionDemo palmLessonDemo;
        readonly Vector3[] magicTouchTips=new Vector3[5];
        public bool MagicSpraying {get;private set;}
        public bool MagicAiming=>palmMagic.Aiming;
        public EmberdeepManaCrystal ManaCrystal=>manaCrystal;
        bool MagicOwnsHand(int hand)=>(palmMagic.Aiming && (hand==0 || palmMagic.Casting)) || SpellOwnsHand(hand);
        void BuildMagic()
        {
            manaCrystal=new GameObject("Left hand mana crystal").AddComponent<EmberdeepManaCrystal>();
            manaCrystal.transform.SetParent(transform,false);manaCrystal.gameObject.SetActive(false);
            manaCrystal.FollowLeftHand(handVisuals);
            palmCircle=new GameObject("Palm water ground circle").AddComponent<LineRenderer>();
            palmCircle.transform.SetParent(transform,false);palmCircle.sharedMaterial=WaterMaterial;
            palmCircle.positionCount=64;palmCircle.loop=true;palmCircle.startWidth=palmCircle.endWidth=.005f;
            palmCircle.numCornerVertices=2;palmCircle.enabled=false;
            palmMotes=new GameObject("Palm to target water motes").AddComponent<EmberdeepPalmAimVfx>();palmMotes.transform.SetParent(transform,false);
            palmLessonDemo=new GameObject("Goddess hand visual lesson").AddComponent<EmberdeepInteractionDemo>();
            palmLessonDemo.transform.SetParent(transform,false);palmLessonDemo.Show(EmberdeepInteractionDemo.Action.Goddess);palmLessonDemo.SetCaptionVisible(false);
        }
        void ReadMagicHands(float dt)
        {
            bool free=!ShopOpen && !QuitConfirmationOpen && !QuitRequested && !Run.Supply.Locked && !refill.OwnsControl
                && !ToolOwnsHand(0) && !ToolOwnsHand(1) && !sheepInteraction.OwnsHand(0) && !sheepInteraction.OwnsHand(1) && Run.Phase==FirePhase.Fighting && !SpellBusy;
            bool open=hands.UsesPortableGrip?hands.IsPortablePalmOpen(true):EmberdeepHandGrasp.IsOpenPalm(hands.TrackedHand(true));
            int mask=0;for(int i=0;i<5;i++)if(hands.TryReadFingertip(false,i,out magicTouchTips[i]))mask|=1<<i;
            palmMagic.Sample(toolHands[0],magicTouchTips,mask,open,free,Arena,Run.MagicRange,dt);
            if(palmMagic.Casting)introStarted=true;
        }
        void ResetMagic()
        {
            palmMagic.Reset();MagicSpraying=false;if(palmMotes)palmMotes.Hide();
            if(palmCircle)palmCircle.enabled=false;
            if(manaCrystal)manaCrystal.gameObject.SetActive(false);
        }
        void RenderMagic()
        {
            if(!manaCrystal || !Player)return;
            bool available=focused && !paused && !acquiringPose && !returningToCamp && hands && hands.HeadReady;
            bool visible=available && Player.IsXR && toolHands[0].Usable;
            manaCrystal.gameObject.SetActive(visible);
            if(visible)
            {
                var h=toolHands[0];var frame=h.GripRotation;var center=h.Contact;
                manaCrystal.transform.SetPositionAndRotation(center-frame*Vector3.up*.016f,frame);
                manaCrystal.SetFill(Run.MagicWater/Run.MagicCapacity);
            }
            palmCircle.enabled=available && palmMagic.Aiming && !ShopOpen && !QuitConfirmationOpen && !SpellBusy && !fan.Ready;
            palmMotes.Show(palmCircle.enabled,palmMagic.Origin,palmMagic.Target,Player.ViewCamera,Run.MagicWater>.01f,Time.unscaledDeltaTime,MagicSpraying,Run.MagicRadius*FireMiniature.Scale,Arena.up);
            if(palmCircle.enabled)
            {
                float radius=Run.MagicRadius*FireMiniature.Scale;
                for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;palmCircle.SetPosition(i,palmMagic.Target+Arena.up*.002f+(Arena.right*Mathf.Cos(a)+Arena.forward*Mathf.Sin(a))*radius);}
                palmCircle.startColor=palmCircle.endColor=Run.MagicWater>.01f?new Color(.25f,.85f,1):new Color(.35f,.4f,.45f);
            }
            palmLessonDemo.gameObject.SetActive(available && !Run.PalmTutorialComplete && Run.IsPalmLesson && Run.Phase==FirePhase.Fighting && !ShopOpen && !QuitConfirmationOpen);
            if(palmLessonDemo.gameObject.activeSelf)
            {palmLessonDemo.transform.position=Arena.TransformPoint(new Vector3(-2.5f,4.6f,3.8f));palmLessonDemo.transform.rotation=Player.ViewCamera.transform.rotation;}
        }
        void ConfigurePalmLesson()
        {
            bool lesson=Run.IsPalmLesson;
            if(lesson){ReturnEquipment();introStarted=false;}
            if(rack)rack.gameObject.SetActive(!Run.SpellMode);
            if(rackView)rackView.gameObject.SetActive(!lesson && !grip.Held);
            if(hydrantStation)hydrantStation.gameObject.SetActive(true);
            if(creekStation)creekStation.gameObject.SetActive(!Run.GoddessTutorial && Run.Stage>0);
        }
        void RefreshMagicText()
        {
            if(Run.IsPalmLesson)economy.text=$"$ {Run.Coins}  |  MANA {Run.MagicWater:0}/{Run.MagicCapacity:0}";
            else economy.text+=$"  |  MANA {Run.MagicWater:0}/{Run.MagicCapacity:0}";
            if(Run.IsPalmLesson && Run.Phase==FirePhase.Fighting)
            {
                status.text=$"{Run.Stage+1}/{Run.StageCount}  {Run.StageName}";
                help.text=Run.PalmTutorialComplete?"PALM WATER READY":Player.IsXR?"Open left palm: aim. Touch its back with any right fingertip: water.":"Mouse: aim / hold SPACE: palm water";
                tactics.text=Run.PalmTutorialComplete?$"FULL RECHARGE {Run.MagicFillSeconds:0}s":"Lift your fingertip to stop. Crystal refills over time.";
            }
            else if(Run.GoddessTutorial && Run.Stage==3 && !grip.Held && Run.Phase==FirePhase.Fighting)
                help.text="Right hand: gold handle. Left hand: nozzle. Squeeze to spray.";
            else if(palmMagic.Aiming)help.text=Run.MagicWater<.01f?"MANA EMPTY - let the crystal recharge":"Touch the back of your left hand with any right fingertip. Lift to stop.";
            if(Run.NeedsMagicUpgrade){actionText.text="UPGRADE YOUR MAGIC >";help.text="Visit the shop and buy your first magic upgrade.";}
            if(Run.IsPalmLesson && rackLabel){rackLabel.gameObject.SetActive(true);rackLabel.text="EQUIPMENT FROM LEVEL 4";}
            handText.transform.gameObject.SetActive(!Run.IsPalmLesson);
            foreach(var b in buttons)if(b.Command=="Return equipment")b.gameObject.SetActive(!Run.IsPalmLesson);
        }
    }
}

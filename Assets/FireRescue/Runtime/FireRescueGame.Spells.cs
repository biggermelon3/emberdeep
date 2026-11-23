using UnityEngine;
using UnityEngine.InputSystem;

namespace FireRescue
{
    public static class EmberdeepSpellText
    {
        public static string Name(FireEquipmentId id)=>id==FireEquipmentId.Extinguisher?"PALM WATER":id==FireEquipmentId.PulsePistol?"WATER BOLT":id==FireEquipmentId.RainFan?"RAIN FAN":"WATER ORB";
        public static string Short(FireEquipmentId id)=>id==FireEquipmentId.Extinguisher?"PALM":id==FireEquipmentId.PulsePistol?"BOLT":id==FireEquipmentId.RainFan?"FAN":"ORB";
        public static string Controls(FireEquipmentId id)=>id==FireEquipmentId.Extinguisher?"Open left palm at the fire.\nRight fingertip on its back: spray.":
            id==FireEquipmentId.PulsePistol?"Left hand points at the fire.\nRight fist: charge. Open: fire.":
            id==FireEquipmentId.RainFan?"Push both open palms forward\nfor 1 second. Close hands: stop.":
            "Left palm up, right fingers under it.\nFlip the palm down and open: drop.";
        public static Color Tint(FireEquipmentId id)=>id==FireEquipmentId.PulsePistol?new Color(.10f,.55f,1):id==FireEquipmentId.RainFan?new Color(.12f,.85f,.45f):
            id==FireEquipmentId.PressureCannon?new Color(1,.55f,.12f):new Color(.06f,.65f,1);
    }

    // Gesture spells WATER BOLT, RAIN FAN and WATER ORB. LoopSpec §11 cuts them from the
    // production flow; they are only owned with EmberdeepSession.PlaytestAllSpells.
    // Gestures decide intent, FireRun owns every unit of water, and the shared
    // WaterJet carries it to the forest.
    public sealed partial class FireRescueGame
    {
        readonly EmberdeepBoltGesture bolt=new();
        readonly EmberdeepFanGesture fan=new();
        readonly EmberdeepOrbGesture orb=new();
        readonly EmberdeepSpellHand[] spellHands=new EmberdeepSpellHand[2];
        readonly EmberdeepManaCrystal[] spellCrystals=new EmberdeepManaCrystal[3];
        EmberdeepSpellVfx spellVfx;
        EmberdeepInteractionDemo spellLessonDemo;
        Vector3 orbTarget,boltTarget,fanTarget;
        bool orbTargetValid;
        float desktopBolt=-1,desktopOrb=-1;
        public EmberdeepBoltGesture BoltSpell=>bolt;
        public EmberdeepFanGesture FanSpell=>fan;
        public EmberdeepOrbGesture OrbSpell=>orb;
        public EmberdeepSpellVfx SpellVfx=>spellVfx;
        public bool FanSpraying {get;private set;}
        public float SpellCharge01=>Mathf.Max(bolt.Charge01,orb.State==EmberdeepOrbGesture.Stage.Charging?orb.Charge01:0,
            desktopBolt>=0?Mathf.Clamp01(desktopBolt/EmberdeepBoltGesture.FullChargeSeconds):0,
            desktopOrb>=0?Mathf.Clamp01(desktopOrb/EmberdeepOrbGesture.FullChargeSeconds):0);
        bool OrbIdle=>orb.State==EmberdeepOrbGesture.Stage.Idle;
        bool SpellBusy=>bolt.Charging || fan.Active || !OrbIdle;
        bool SpellOwnsHand(int hand)=>bolt.Charging || fan.Active || (!OrbIdle && (hand==0 || orb.State==EmberdeepOrbGesture.Stage.Charging));

        void BuildSpells()
        {
            spellVfx=new GameObject("Gesture spell effects").AddComponent<EmberdeepSpellVfx>();
            spellVfx.transform.SetParent(transform,false);spellVfx.Initialize(WaterMaterial);
            // Three small crystals are set into the leather strap of the glove cuff, behind the
            // main palm-water crystal (the sleeve would cover them on the back of the hand).
            Vector3[] offsets={new(-.021f,-.011f,-.071f),new(0,-.0175f,-.071f),new(.021f,-.011f,-.071f)};
            for(int i=0;i<3;i++)
            {
                var id=(FireEquipmentId)(i+1);
                var crystal=new GameObject("Left hand "+EmberdeepSpellText.Name(id)+" crystal").AddComponent<EmberdeepManaCrystal>();
                crystal.transform.SetParent(transform,false);crystal.transform.localScale=Vector3.one*.42f;
                crystal.Offset=offsets[i];crystal.SetTint(EmberdeepSpellText.Tint(id));crystal.FollowHand(handVisuals,EmberdeepWaterHand.Left);
                crystal.gameObject.SetActive(false);spellCrystals[i]=crystal;
            }
            spellLessonDemo=new GameObject("New spell visual lesson").AddComponent<EmberdeepInteractionDemo>();
            spellLessonDemo.transform.SetParent(transform,false);spellLessonDemo.gameObject.SetActive(false);
        }
        void ReadSpellHands(float dt)
        {
            for(int i=0;i<2;i++)spellHands[i]=ReadSpellHand(i);
            SampleSpells(spellHands[0],spellHands[1],dt);
        }
        EmberdeepSpellHand ReadSpellHand(int index)
        {
            bool left=index==0;var tool=toolHands[index];
            var hand=new EmberdeepSpellHand{Valid=tool.Usable && tool.HasGripFrame,Contact=tool.Contact,Frame=tool.GripRotation};
            if(!hands.UsesPortableGrip)
            {
                // The system-gesture and head gates still apply; finger confidence does not.
                if(hands.ReadObjectPose(left,out _,out _,out _))EmberdeepHandGrasp.ReadSpellFeatures(hands.TrackedHand(left),ref hand);
                return hand;
            }
            if(!hands.TryReadPortableSample(left,out var s))return hand;
            hand.CurlsValid=s.CurlsValid;hand.Index=s.IndexCurl;hand.Middle=s.MiddleCurl;hand.Ring=s.RingCurl;hand.Little=s.LittleCurl;
            hand.Open=s.OpenPalm;hand.Wrist=s.Wrist.position;hand.IndexBase=s.IndexBase;hand.PointValid=s.AimValid && (s.TipMask&2)!=0;
            hand.TipMask=s.TipMask;hand.ThumbTip=s.ThumbTip;hand.IndexTip=s.IndexTip;hand.MiddleTip=s.MiddleTip;hand.RingTip=s.RingTip;hand.LittleTip=s.LittleTip;
            return hand;
        }
        // Production samples and synthetic test samples share this entry point.
        public void SampleSpells(EmberdeepSpellHand left,EmberdeepSpellHand right,float dt)
        {
            spellHands[0]=left;spellHands[1]=right;
            bool live=Run.SpellMode && Run.Phase==FirePhase.Fighting && !ShopOpen && !QuitConfirmationOpen && !QuitRequested && !Run.Supply.Locked;
            bool free=live && !refill.OwnsControl && !sheepInteraction.OwnsHand(0) && !sheepInteraction.OwnsHand(1);
            // A formed orb stays in the palm while the other hand does something else.
            bool orbOwned=Run.Owns(FireEquipmentId.PressureCannon) && (!OrbIdle || Run.SpellWater(FireEquipmentId.PressureCannon)>=8);
            orbTargetValid=TryOrbTarget(left,out orbTarget);
            orb.Sample(left,right,live && orbOwned && (!OrbIdle || (free && !fan.Active && !bolt.Charging && !palmMagic.Casting)),orbTargetValid,dt);
            bool others=free && OrbIdle && !palmMagic.Casting;
            bolt.Sample(left,right,others && !fan.Active && Run.Owns(FireEquipmentId.PulsePistol) && Run.SpellWater(FireEquipmentId.PulsePistol)>=1,dt);
            var toward=Arena?Vector3.ProjectOnPlane(Arena.TransformPoint(new Vector3(0,0,6))-(left.Contact+right.Contact)*.5f,Vector3.up):Vector3.zero;
            fan.Sample(left,right,toward,others && !bolt.Charging && Run.Owns(FireEquipmentId.RainFan) && Run.SpellWater(FireEquipmentId.RainFan)>.01f,dt);
            if(SpellBusy)introStarted=true;
        }
        // The orb drops along the open palm; a palm aimed off the island falls straight down instead.
        bool TryOrbTarget(EmberdeepSpellHand left,out Vector3 target)
        {
            target=default;
            if(!Arena || !left.Usable)return false;
            var ground=new Plane(Arena.up,Arena.position);
            if(ground.Raycast(new Ray(left.Contact,left.Normal),out float distance) && distance>.03f && distance<2.5f && OnIsland(target=left.Contact+left.Normal*distance))return true;
            target=ground.ClosestPointOnPlane(left.Contact);
            return ground.GetDistanceToPoint(left.Contact)>.02f && OnIsland(target);
        }
        bool OnIsland(Vector3 world)
        {var local=Arena.InverseTransformPoint(world);return Mathf.Abs(local.x)<=5.5f && local.z>=.5f && local.z<=12.5f;}

        void TickSpells(float dt,bool overMenu,Ray pointer)
        {
            FanSpraying=false;
            if(!Run.SpellMode)return;
            bool allowed=!overMenu && !ShopOpen && Run.Phase==FirePhase.Fighting;
            // Gesture results cast the same way for tracked hands and synthetic samples.
            if(bolt.TakeFired() && allowed)FireBolt(bolt.Origin,bolt.Direction,bolt.FiredCharge);
            if(orb.TakeThrown() && allowed)ThrowOrb(orb.Center,orbTarget,orb.ThrownCharge);
            float amount=Run.TickFan(dt,fan.Active && allowed);
            if(amount>0)SprayFan(fan.Origin,fan.Direction,amount,dt);
            if(!Player.IsXR)DesktopSpells(dt,allowed,pointer);
            TickSpellUpkeep(dt);
        }
        void FireBolt(Vector3 origin,Vector3 direction,float charge)
        {
            if(!Run.CastBolt(charge,out float amount,out float radius))return;
            var id=FireEquipmentId.PulsePistol;
            // A ray through a tree snaps to it; the arc is solved so the shot lands where the finger points.
            var target=WaterJet.MouseTarget(new Ray(origin,direction),Arena,waterTargets);
            var velocity=WaterJet.AimVelocity(origin,target,Run.SpeedFor(id));
            jet.Emit(.02f,origin,velocity,amount,Run.RangeFor(id)*FireMiniature.Scale,Arena,waterTargets,OnWaterImpact,radius,1,0,FireDelivery.Charge,true);
            Effects.Launch(origin,velocity.normalized,charge);Feedback.Launch(charge);introStarted=true;
        }
        void ThrowOrb(Vector3 origin,Vector3 target,float charge)
        {
            if(!Run.CastOrb(charge,out float amount,out float radius))return;
            var velocity=WaterJet.AimVelocity(origin,target,2.4f);
            jet.Emit(.02f,origin,velocity,amount,3,Arena,waterTargets,OnWaterImpact,radius,2,0,FireDelivery.Charge,true);
            Effects.Launch(origin,velocity.normalized,charge);Feedback.Launch(charge);introStarted=true;
        }
        Vector3 FanVelocity(Vector3 direction)=>direction.normalized*(Run.SpeedFor(FireEquipmentId.RainFan)*.75f);
        void SprayFan(Vector3 origin,Vector3 direction,float amount,float dt)
        {
            var id=FireEquipmentId.RainFan;
            jet.Emit(dt,origin,FanVelocity(direction),amount,Run.RangeFor(id)*FireMiniature.Scale,Arena,waterTargets,OnWaterImpact,
                .6f+Run.WidthFor(id),1,FireRun.FanSpreadDegrees,FireDelivery.Fan,true);
            FanSpraying=true;introStarted=true;
        }
        // Desktop preview: hold 2 to charge a bolt, hold 3 to fan, hold 4 to grow an orb.
        void DesktopSpells(float dt,bool allowed,Ray pointer)
        {
            var keys=Keyboard.current;
            if(keys==null || !allowed){desktopBolt=desktopOrb=-1;return;}
            var origin=Player.ViewCamera.transform.TransformPoint(new Vector3(-.12f,-.14f,.30f));
            var target=WaterJet.MouseTarget(pointer,Arena,waterTargets);
            if(Run.Owns(FireEquipmentId.PulsePistol))
            {
                if(keys.digit2Key.isPressed)desktopBolt=Mathf.Max(0,desktopBolt)+dt;
                else if(desktopBolt>=0){FireBolt(origin,(target-origin).normalized,Mathf.Clamp01(desktopBolt/EmberdeepBoltGesture.FullChargeSeconds));desktopBolt=-1;}
            }
            if(Run.Owns(FireEquipmentId.RainFan) && keys.digit3Key.isPressed)
            {
                float amount=Run.TickFan(dt,true);
                if(amount>0)SprayFan(origin,WaterJet.AimVelocity(origin,target,FanVelocity(Vector3.forward).magnitude),amount,dt);
            }
            if(Run.Owns(FireEquipmentId.PressureCannon))
            {
                if(keys.digit4Key.isPressed)desktopOrb=Mathf.Max(0,desktopOrb)+dt;
                else if(desktopOrb>=0){ThrowOrb(origin,target,Mathf.Clamp01(desktopOrb/EmberdeepOrbGesture.FullChargeSeconds));desktopOrb=-1;}
            }
        }
        // Slow spell-tank recovery runs every live frame (playtest spells only).
        void TickSpellUpkeep(float dt)
        {
            if(Run.SpellMode)Run.TickSpellRecovery(dt,introStarted);
        }
        void ResetSpells()
        {
            bolt.Reset();fan.Reset();orb.Reset();FanSpraying=false;desktopBolt=desktopOrb=-1;
            if(spellVfx)spellVfx.Hide();
        }
        void RenderSpells()
        {
            if(!spellVfx || !Player)return;
            bool available=Run.SpellMode && focused && !paused && !acquiringPose && !returningToCamp && hands && hands.HeadReady;
            int water=EmberdeepWaterHand.Index;
            bool handVisible=available && Player.IsXR && toolHands[water].Usable;
            for(int i=0;i<3;i++)
            {
                var id=(FireEquipmentId)(i+1);var crystal=spellCrystals[i];bool show=handVisible && Run.Owns(id);
                crystal.gameObject.SetActive(show);
                if(!show)continue;
                var palm=toolHands[water];
                crystal.transform.SetPositionAndRotation(palm.Contact+palm.GripRotation*(crystal.Offset-Vector3.up*.016f),palm.GripRotation);
                crystal.SetFill(Run.SpellFill01(id));
            }
            RenderLessons(available);
            bool live=available && Run.Phase==FirePhase.Fighting && !ShopOpen && !QuitConfirmationOpen;
            float dt=Time.unscaledDeltaTime;
            bool orbOn=live && !OrbIdle;
            spellVfx.ShowOrb(orbOn,orb.Center,orb.Radius,orb.Charge01,dt);
            bool aiming=live && OrbIdle && !fan.Ready && bolt.Aiming && Run.Owns(FireEquipmentId.PulsePistol);
            if(aiming)boltTarget=WaterJet.MouseTarget(new Ray(bolt.Origin,bolt.Direction),Arena,waterTargets);
            var boltColor=Run.SpellWater(FireEquipmentId.PulsePistol)>=1?new Color(.35f,.8f,1):new Color(.4f,.45f,.5f);
            spellVfx.ShowBead(aiming && bolt.Charging,bolt.Origin+bolt.Direction*.014f,bolt.Charge01);
            spellVfx.ShowBeam(aiming,bolt.Origin,boltTarget,boltColor);
            if(orbOn && orb.State==EmberdeepOrbGesture.Stage.Held && orb.Flipped && orbTargetValid)
                spellVfx.ShowCircle(true,orbTarget,Arena.up,(1.2f+2*orb.Charge01)*FireMiniature.Scale,new Color(1,.7f,.3f));
            else if(live && OrbIdle && fan.Ready && WaterJet.Predict(fan.Origin,FanVelocity(fan.Direction),
                Run.RangeFor(FireEquipmentId.RainFan)*FireMiniature.Scale,Arena,waterTargets,out var fanHit))
            {
                fanTarget=fanHit.Point;
                float reach=Vector3.Distance(fan.Origin,fanTarget)*Mathf.Tan(FireRun.FanSpreadDegrees*Mathf.Deg2Rad);
                spellVfx.ShowCircle(true,fanTarget,Arena.up,Mathf.Lerp(.03f,reach+.05f,fan.Arm01),fan.Active?new Color(.3f,1,.55f):new Color(.6f,.8f,.7f));
            }
            else if(aiming)
                spellVfx.ShowCircle(true,boltTarget,Arena.up,(.5f+.6f*bolt.Charge01+Run.WidthFor(FireEquipmentId.PulsePistol))*FireMiniature.Scale,boltColor);
            else spellVfx.ShowCircle(false,default,default,0,default);
        }
        // One blue hand demonstration at a time: the newest purchased spell until it has put out a fire.
        void RenderLessons(bool available)
        {
            if(!spellLessonDemo)return;
            bool stage=available && Arena && Arena.gameObject.activeInHierarchy && !ShopOpen && !QuitConfirmationOpen
                && (Run.Phase==FirePhase.Fighting || Run.Phase==FirePhase.Preparing);
            int spell=Run.SpellLesson;
            bool palmLesson=Run.IsPalmLesson && !Run.PalmTutorialComplete;
            bool show=stage && spell>0 && !palmLesson;
            spellLessonDemo.gameObject.SetActive(show);
            if(show)
            {
                spellLessonDemo.Show(FireRun.LessonFor((FireEquipmentId)spell));
                spellLessonDemo.transform.SetPositionAndRotation(Arena.TransformPoint(new Vector3(-2.5f,4.6f,3.8f)),Player.ViewCamera.transform.rotation);
            }
        }
        public EmberdeepInteractionDemo SpellLessonDemo=>spellLessonDemo;
        float celebrationUntil;
        public bool Celebrating=>Time.unscaledTime<celebrationUntil;
        // Every fire is out: the whole grove bursts into sparks and the trees hop.
        void BeginCelebration(float seconds)
        {
            celebrationUntil=Time.unscaledTime+seconds;
            var crowns=new Vector3[Run.Trees.Length];int count=0;
            for(int i=0;i<Run.Trees.Length;i++)if(Run.Trees[i].State!=TreeState.Burnt)crowns[count++]=Arena.TransformPoint(Run.Trees[i].Position+Vector3.up*.9f);
            if(count==0)crowns[count++]=Arena.TransformPoint(new Vector3(0,1.6f,6));
            System.Array.Resize(ref crowns,count);
            Effects.Celebrate(crowns,Arena.up,seconds);
        }
        void AnimateCelebration()
        {
            if(trees.Count==0 || trees.Count!=Run.Trees.Length)return;
            float remaining=celebrationUntil-Time.unscaledTime;
            // Fade the hop out so every tree lands back exactly on its root.
            float strength=remaining>0?Mathf.Clamp01(remaining/1.2f):0;
            for(int i=0;i<trees.Count;i++)
            {
                var data=Run.Trees[i];var root=trees[i].Root;if(!root)continue;
                var home=data.Position-Vector3.up*.7f;
                if(strength<=0 || data.State==TreeState.Burnt){if(root.localPosition!=home){root.localPosition=home;root.localRotation=Quaternion.identity;}continue;}
                float phase=Time.unscaledTime*7+i*1.7f;
                root.localPosition=home+Vector3.up*(Mathf.Abs(Mathf.Sin(phase))*.14f*strength);
                root.localRotation=Quaternion.Euler(Mathf.Sin(phase*.5f)*5*strength,0,Mathf.Cos(phase*.5f)*5*strength);
            }
        }
        void RefreshSpellText()
        {
            if(!Run.SpellMode)return;
            // LoopSpec §3.1: the gem is the only water meter, so the dashboard shows no water numbers.
            economy.text=$"$ {Run.Coins}";
            handText.gameObject.SetActive(false);
            foreach(var b in buttons)if(b.Command=="Return equipment")b.gameObject.SetActive(false);
            if(Run.NeedsMagicUpgrade)return;
            if(Run.Phase!=FirePhase.Fighting)
            {
                help.text=Run.Phase==FirePhase.Cleared?"Spend your reward, then take on the next fire.":Run.Phase==FirePhase.Won?"Forest saved. Return to camp.":
                    Run.Phase==FirePhase.Lost?"Too many trees burned. Retry and keep your earned coins.":help.text;
                return;
            }
            string hint=null;
            if(orb.State==EmberdeepOrbGesture.Stage.Charging)hint="ORB GROWING - move your right hand away to hold it";
            else if(orb.State==EmberdeepOrbGesture.Stage.Held)hint=orb.Flipped && !orbTargetValid?"Aim your palm at the forest, then open your hand":"ORB READY - turn your left palm down and open your hand to drop it";
            else if(fan.Active)hint="RAIN FAN spraying - close your hands to stop";
            else if(fan.Ready)hint="RAIN FAN - hold both palms forward";
            else if(bolt.Charging)hint="CHARGING - open your right hand to fire";
            else if(bolt.Aiming && Run.Owns(FireEquipmentId.PulsePistol))hint="WATER BOLT - make a right fist to charge, open it to fire";
            else if(palmMagic.Aiming || Run.IsPalmLesson)hint=null;
            else if(Run.GemEmpty)hint=GemEmptyHint;
            else if(!Player.IsXR)hint=Run.SpellsCut?DesktopGemHint:"SPACE: palm water | 2: bolt | 3: fan | 4: orb | R: hammer the pump";
            else hint=Run.SpellLesson==1?"WATER BOLT - make a right fist to charge, open it to fire":Run.SpellLesson==2?"RAIN FAN - hold both palms forward":
                Run.SpellLesson==3?"WATER ORB - left palm up, right fingers under it":PalmHint;
            if(hint!=null)help.text=hint;
        }
    }
}

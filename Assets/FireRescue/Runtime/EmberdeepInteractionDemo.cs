using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FireRescue
{
    // Visual-only tabletop teaching model: no colliders, tracked data or input.
    // Each articulated hand is one reusable mesh rather than 30 finger renderers.
    public sealed class EmberdeepInteractionDemo : MonoBehaviour
    {
        // Hammer and Slap are first-time hint moves (LoopSpec §11b); FireRun.LearnedActions never indexes them.
        public enum Action { Extinguisher, Gun, Cannon, Lever, Valve, Button, Dock, Intake, Pump, Goddess, Bolt, Fan, Orb, Hammer, Slap }
        static bool IsSpell(Action action)=>action==Action.Bolt || action==Action.Fan || action==Action.Orb;
        public Action Current {get;private set;}
        public float AnimationTime {get;private set;}
        Transform model,tool,control,nozzle;
        ModelHand left,right;
        LineRenderer water,hose;
        EmberdeepManaCrystal gem;
        TextMesh caption;
        public void SetCaptionVisible(bool value){if(caption)caption.gameObject.SetActive(value);}
        // Ghost: pale see-through-looking hands for first-time hints on the player's own hand or object.
        public bool Ghost;
        Material blue,pale,glove;
        bool built;
        public void Show(Action action)
        {
            if(built && action==Current)return;
            if(model){model.gameObject.SetActive(false);Destroy(model.gameObject);}
            left?.Dispose();right?.Dispose();
            if(!blue)
            {
                blue=new Material(Resources.Load<Shader>("EmberdeepTutorial")){name="Tutorial blue model",renderQueue=3010};
                blue.SetColor("_BaseColor",new Color(.20f,.65f,1));
                pale=new Material(blue){name="Tutorial pale blue hands"};pale.SetColor("_BaseColor",new Color(.68f,.92f,1));
                // The demonstration wears the guardian gloves; tones come from the hand mesh's vertex colours.
                glove=new Material(blue){name="Tutorial guardian gloves"};glove.SetColor("_BaseColor",Color.white);
            }
            built=true;Current=action;AnimationTime=0;
            model=Node("Animated instruction model",transform,Vector3.zero);model.localRotation=Quaternion.Euler(12,-22,0);
            if(action==Action.Goddess)model.localRotation=Quaternion.Euler(-40,-25,0);
            if(IsSpell(action))model.localRotation=Quaternion.Euler(10,-28,0);
            if(action==Action.Hammer || action==Action.Slap)model.localRotation=Quaternion.identity;
            model.localScale=Vector3.one*.68f;
            tool=Node("Demonstration prop",model,Vector3.zero);
            bool weapon=action==Action.Extinguisher || action==Action.Gun || action==Action.Cannon || action==Action.Dock;
            if(weapon)
            {
                bool bottle=action==Action.Extinguisher || action==Action.Dock;
                Shape(tool,bottle?PrimitiveType.Capsule:PrimitiveType.Cube,new Vector3(0,-.035f,0),bottle?new Vector3(.10f,.135f,.10f):new Vector3(.07f,.08f,.19f));
                control=Node("Handle",tool,bottle?new Vector3(0,.11f,-.075f):new Vector3(0,-.04f,-.015f));
                Shape(control,PrimitiveType.Cube,Vector3.zero,bottle?new Vector3(.032f,.025f,.108f):new Vector3(.035f,.09f,.045f));
                nozzle=Node("Aim nozzle",tool,bottle?new Vector3(-.10f,.015f,.12f):new Vector3(0,.015f,.12f));
                Shape(nozzle,PrimitiveType.Cube,new Vector3(0,0,.02f),new Vector3(.04f,.04f,.09f));
                if(bottle)
                {
                    Shape(tool,PrimitiveType.Cube,new Vector3(0,.085f,-.02f),new Vector3(.04f,.04f,.06f));
                    Shape(tool,PrimitiveType.Cylinder,new Vector3(0,-.15f,0),new Vector3(.105f,.012f,.105f));
                    hose=Node("Demonstration hose",model,Vector3.zero).gameObject.AddComponent<LineRenderer>();
                    hose.sharedMaterial=blue;hose.useWorldSpace=false;hose.positionCount=10;hose.startWidth=hose.endWidth=.009f;hose.numCornerVertices=2;
                }
                else hose=null;
                if(action==Action.Dock)Shape(model,PrimitiveType.Cylinder,new Vector3(0,-.20f,.055f),new Vector3(.12f,.018f,.12f));
            }
            else if(IsSpell(action))
            {
                // One sphere serves as the bolt's charge bead and as the growing orb.
                hose=null;control=Node("Demonstrated spell water",tool,Vector3.zero);
                Shape(control,PrimitiveType.Sphere,Vector3.zero,Vector3.one);control.localScale=Vector3.zero;
            }
            else if(action==Action.Hammer || action==Action.Slap)
            {
                // Hammer: a fist comes down on a cap (pump or bell). Slap: an open hand sweeps sideways.
                hose=null;control=Node(action==Action.Hammer?"Struck cap":"Slap path",tool,Vector3.zero);
                // No cap of its own: the hint plays on the real bell or pump.
            }
            else if(action==Action.Goddess)
            {
                hose=null;control=Node("Open palm direction",tool,Vector3.zero);
                var circle=Node("Palm target circle",model,new Vector3(-.045f,-.16f,0)).gameObject.AddComponent<LineRenderer>();
                circle.sharedMaterial=blue;circle.useWorldSpace=false;circle.loop=true;circle.positionCount=32;circle.startWidth=circle.endWidth=.004f;
                for(int i=0;i<32;i++){float a=i*Mathf.PI/16;circle.SetPosition(i,new Vector3(Mathf.Cos(a)*.065f,0,Mathf.Sin(a)*.065f));}
            }
            else
            {
                hose=null;
                Shape(tool,PrimitiveType.Cylinder,new Vector3(0,-.10f,.05f),new Vector3(.11f,.09f,.11f));
                control=Node("Moving station control",tool,new Vector3(0,.04f,0));
                if(action==Action.Valve)
                {
                    for(int i=0;i<12;i++){float a=i*Mathf.PI/6;Shape(control,PrimitiveType.Sphere,new Vector3(Mathf.Cos(a)*.07f,Mathf.Sin(a)*.07f,0),Vector3.one*.027f);}
                    Shape(control,PrimitiveType.Cube,Vector3.zero,new Vector3(.14f,.015f,.02f));
                }
                else Shape(control,action==Action.Button || action==Action.Intake?PrimitiveType.Cylinder:PrimitiveType.Cube,Vector3.zero,
                    action==Action.Button || action==Action.Intake?new Vector3(.07f,.018f,.07f):new Vector3(.15f,.028f,.035f));
            }
            left=new ModelHand(model,Ghost?pale:glove,true,Ghost);right=new ModelHand(model,Ghost?pale:glove,false,Ghost);
            gem=null;
            if(action==Action.Goddess){gem=Node("Left hand lesson crystal",left.Root,new Vector3(0,-.018f,0)).gameObject.AddComponent<EmberdeepManaCrystal>();}
            water=Node("Demonstrated water",model,Vector3.zero).gameObject.AddComponent<LineRenderer>();water.sharedMaterial=pale;
            water.useWorldSpace=false;water.positionCount=8;water.startWidth=.009f;water.endWidth=.003f;water.numCapVertices=2;
            water.shadowCastingMode=ShadowCastingMode.Off;
            caption=Node("Short action caption",model,new Vector3(0,-.28f,-.05f)).gameObject.AddComponent<TextMesh>();
            caption.anchor=TextAnchor.MiddleCenter;caption.alignment=TextAlignment.Center;caption.fontSize=48;caption.characterSize=.009f;
            caption.transform.localRotation=Quaternion.Inverse(model.localRotation);
            caption.transform.position=transform.TransformPoint(new Vector3(0,-.20f,0));
            caption.color=new Color(.68f,.92f,1);
            Animate(0);
            GetComponentInParent<EmberdeepLocalizedText>()?.RefreshLabels();
        }
        void Update(){if(built)Animate(Time.unscaledDeltaTime);}
        public void Animate(float dt)
        {
            AnimationTime+=Mathf.Clamp(dt,0,.1f);float t=AnimationTime%6;
            float approach=Mathf.SmoothStep(0,1,t),curl=Mathf.SmoothStep(0,1,Mathf.Clamp01(t-.65f));
            bool weapon=Current==Action.Extinguisher || Current==Action.Gun || Current==Action.Cannon;
            if(hose)
            {
                var a=model.InverseTransformPoint(tool.TransformPoint(new Vector3(.025f,.08f,0)));var b=model.InverseTransformPoint(nozzle.position);
                for(int i=0;i<10;i++){float u=i/9f;hose.SetPosition(i,Vector3.Lerp(a,b,u)+Vector3.down*Mathf.Sin(u*Mathf.PI)*.08f);}
            }
            water.enabled=false;left.Root.gameObject.SetActive(Current==Action.Extinguisher || Current==Action.Cannon);
            tool.localPosition=Vector3.zero;
            if(IsSpell(Current)){AnimateSpell(t);return;}
            if(Current==Action.Hammer || Current==Action.Slap){AnimateHint(AnimationTime);return;}
            if(Current==Action.Goddess)
            {
                left.Root.gameObject.SetActive(true);left.Root.localPosition=new Vector3(-.045f,0,0);left.Root.localRotation=Quaternion.LookRotation(Vector3.right,Vector3.down);
                right.Root.localPosition=Vector3.Lerp(new Vector3(.22f,.09f,-.0255f),new Vector3(.045f,.023f,-.0255f),Mathf.SmoothStep(0,1,t-1.5f));
                right.Root.localRotation=Quaternion.LookRotation(Vector3.left,Vector3.down);left.Draw(EmberdeepHandPoseKind.Free,0);right.Draw(EmberdeepHandPoseKind.Free,0,true);
                bool cast=t>2.5f && t<4.5f;
                if(cast){water.enabled=true;for(int i=0;i<8;i++)water.SetPosition(i,Vector3.Lerp(new Vector3(-.045f,-.018f,0),new Vector3(-.045f,-.16f,0),i/7f));}
                if(t>4.5f)right.Root.localPosition+=Vector3.right*(t-4.5f)*.1f;
                gem.SetFill(t<2.5f?1:t<4.5f?1-(t-2.5f)*.4f:.2f+(t-4.5f)*.53f);
                caption.text=t<2.5f?"OPEN LEFT PALM: AIM":cast?"TOUCH LEFT HAND BACK: WATER":"LIFT FINGER: STOP / RECHARGE";
            }
            else if(weapon)
            {
                bool spray=t>3 && t<4.25f;
                float hold=t>5?1-Mathf.SmoothStep(0,1,t-5):curl;
                tool.localPosition=Vector3.up*(Mathf.SmoothStep(0,1,t-1)*.035f);
                var kind=Current==Action.Extinguisher?EmberdeepHandPoseKind.ExtinguisherGrip:Current==Action.Cannon?EmberdeepHandPoseKind.CannonGrip:EmberdeepHandPoseKind.PistolGrip;
                PoseHand(right,control,kind,Vector3.right*.11f*(1-approach),hold*(spray?1:.66f), false);
                if(Current==Action.Extinguisher)
                {
                    float aim=Mathf.SmoothStep(0,1,t-2);
                    nozzle.localPosition=new Vector3(-.10f-.045f*aim,.015f+.045f*aim,.12f);
                    nozzle.localRotation=Quaternion.Euler(-12*aim,Mathf.Sin(t)*15*aim,0);
                    PoseHand(left,nozzle,EmberdeepHandPoseKind.NozzleGrip,Vector3.left*.09f*(1-aim),aim*hold,true);
                }
                else if(Current==Action.Cannon)PoseHand(left,nozzle,EmberdeepHandPoseKind.ForwardSupport,Vector3.zero,hold,true);
                if(Current==Action.Cannon?t>=4.25f && t<4.7f:spray)Stream(model.InverseTransformPoint(nozzle.position)+nozzle.localRotation*Vector3.forward*.07f,nozzle.localRotation*Vector3.forward);
                caption.text=t<2?"GRASP THE GOLD HANDLE":t<3?"LEFT HAND: AIM NOZZLE":spray?"SQUEEZE: WATER":t<5?"HALF OPEN: STOP":"OPEN PALM: FLOAT";
                if(Current!=Action.Extinguisher && t>=2 && t<3)caption.text="POINT THE BARREL";
                if(Current==Action.Cannon && spray)caption.text="SQUEEZE: CHARGE / RELAX: LAUNCH";
            }
            else
            {
                float stroke=(1-Mathf.Cos(Mathf.Clamp01((t-1)/3)*Mathf.PI*2))*.5f;
                control.localPosition=new Vector3(0,.04f,0);control.localRotation=Quaternion.identity;
                var kind=EmberdeepHandPoseKind.LeverGrip;
                Vector3 offset=Vector3.zero;
                switch(Current)
                {
                    case Action.Lever:control.localPosition+=new Vector3(0,-stroke*.10f,stroke*.035f);caption.text=t<1?"GRASP":t<2.5f?"PRESS DOWN":"LIFT: COLLECT WATER";break;
                    case Action.Pump:control.localPosition+=Vector3.back*stroke*.10f;caption.text="GRASP / PULL / PUSH BACK";break;
                    case Action.Valve:control.localRotation=Quaternion.Euler(0,0,stroke*-55);kind=EmberdeepHandPoseKind.ValveGrip;offset=control.right*.07f;caption.text="GRASP RIM / TURN";break;
                    case Action.Button:control.localPosition+=Vector3.down*stroke*.018f;kind=EmberdeepHandPoseKind.ButtonPress;caption.text="INDEX FINGER: PRESS";break;
                    case Action.Intake:control.localPosition+=new Vector3(.10f*stroke,-.15f*stroke,0);kind=EmberdeepHandPoseKind.IntakeGrip;caption.text="GRASP / PLACE IN WATER / OPEN";break;
                    case Action.Dock:tool.localPosition=Vector3.down*stroke*.09f;kind=EmberdeepHandPoseKind.ExtinguisherGrip;caption.text="ALIGN BLUE RINGS / HOLD STILL";break;
                }
                PoseHand(right,control,kind,Vector3.up*.10f*(1-approach)+offset,curl,false);
                if(stroke<.3f && t>2.5f && Current!=Action.Dock && Current!=Action.Intake)Stream(new Vector3(.08f,-.08f,.01f),Vector3.down);
            }
        }
        // Gesture spells have no prop: both hands act in the air in front of the player.
        void AnimateSpell(float t)
        {
            left.Root.gameObject.SetActive(true);
            if(Current==Action.Bolt)
            {
                bool fist=t>1.2f && t<3.4f;float charge=Mathf.Clamp01((t-1.2f)/2);
                left.Root.localPosition=new Vector3(-.07f,0,0);left.Root.localRotation=Quaternion.LookRotation(Vector3.forward,Vector3.left);
                right.Root.localPosition=new Vector3(.09f,-.02f,-.03f);right.Root.localRotation=Quaternion.LookRotation(Vector3.forward,Vector3.right);
                // The left hand first forms the pointing shape, then holds it.
                left.Draw(EmberdeepHandPoseKind.Free,0,t>=.6f);right.Draw(EmberdeepHandPoseKind.LeverGrip,fist?1:0);
                var tip=left.Root.localPosition+new Vector3(0,.0255f,.10f);
                control.localPosition=tip;control.localScale=Vector3.one*(fist?.012f+.03f*charge:0);
                if(t>=3.4f && t<4.1f)
                {
                    water.enabled=true;float travel=(t-3.4f)/.7f*.30f;
                    for(int i=0;i<8;i++)water.SetPosition(i,tip+Vector3.forward*(travel+i*.012f));
                }
                caption.text=t<1.2f?"LEFT HAND: POINT AT THE FIRE":fist?"RIGHT FIST: CHARGE":"OPEN RIGHT HAND: FIRE";
            }
            else if(Current==Action.Fan)
            {
                float rise=Mathf.SmoothStep(0,1,t);bool spray=t>2 && t<4.4f,closed=t>=4.4f;
                var palmsForward=Quaternion.LookRotation(Vector3.up,Vector3.forward);
                left.Root.localPosition=new Vector3(-.075f,-.04f+.04f*rise,0);right.Root.localPosition=new Vector3(.075f,-.04f+.04f*rise,0);
                left.Root.localRotation=right.Root.localRotation=palmsForward;
                // Both hands open as they rise, and close again to stop.
                float closure=closed?1:1-Mathf.SmoothStep(0,1,t/.8f);
                left.Draw(EmberdeepHandPoseKind.LeverGrip,closure);right.Draw(EmberdeepHandPoseKind.LeverGrip,closure);
                control.localScale=Vector3.zero;
                if(spray)
                {
                    // The single stream sweeps quickly to read as a wide fan.
                    water.enabled=true;var direction=Quaternion.Euler(0,Mathf.Sin(t*9)*28,0)*Vector3.forward;
                    for(int i=0;i<8;i++){float d=i*.03f;water.SetPosition(i,new Vector3(0,0,.03f)+direction*d+Vector3.down*d*d*1.5f);}
                }
                caption.text=t<1?"BOTH OPEN PALMS FORWARD":t<2?"HOLD FOR 1 SECOND":spray?"WIDE SPRAY":"CLOSE YOUR HANDS: STOP";
            }
            else
            {
                float grow=Mathf.Clamp01((t-1.2f)/1.8f),leave=Mathf.SmoothStep(0,1,(t-3)/.6f),flip=Mathf.SmoothStep(0,1,(t-3.8f)/.7f),fall=Mathf.Clamp01((t-4.6f)/.7f);
                left.Root.localPosition=new Vector3(-.03f,0,0);left.Root.localRotation=Quaternion.AngleAxis(180*flip,Vector3.forward);
                // A flat palm cups into a bowl, and opens flat again to let the orb go.
                left.Draw(EmberdeepHandPoseKind.BodyGrip,flip>.9f?0:Mathf.SmoothStep(0,1,(t-.3f)/.8f));
                right.Root.localPosition=new Vector3(.13f+.12f*(1-Mathf.SmoothStep(0,1,t))+.14f*leave,-.055f,.01f);
                right.Root.localRotation=Quaternion.LookRotation(Vector3.left,Vector3.up);right.Draw(EmberdeepHandPoseKind.Free,0,true);
                float size=t<1.2f || fall>=1?0:.02f+.05f*grow;
                control.localPosition=left.Root.localPosition+left.Root.localRotation*Vector3.up*(.02f+size*.5f)+Vector3.down*fall*.22f;
                control.localScale=Vector3.one*size;
                caption.text=t<1.2f?"LEFT PALM UP":t<3?"RIGHT FINGERS UNDER IT: GROW":t<3.8f?"MOVE RIGHT HAND AWAY: HOLD":"FLIP AND OPEN: DROP";
            }
        }
        // First-time hint moves, looped quickly so the rhythm reads.
        void AnimateHint(float time)
        {
            left.Root.gameObject.SetActive(false);
            if(Current==Action.Hammer)
            {
                float t=time%1.3f,fall=t<.55f?0:Mathf.Clamp01((t-.55f)/.16f),rise=Mathf.Clamp01((t-.85f)/.45f);
                float height=t<.71f?Mathf.Lerp(.15f,.035f,fall*fall):Mathf.Lerp(.035f,.15f,Mathf.SmoothStep(0,1,rise));
                right.Root.localPosition=new Vector3(0,height,-.02f);
                right.Root.localRotation=Quaternion.LookRotation(Vector3.forward,Vector3.up)*Quaternion.Euler(90,0,0);
                // The fist clenches on the way down and eases while it rises.
                right.Draw(EmberdeepHandPoseKind.LeverGrip,.72f+.28f*(.5f+.5f*Mathf.Cos(2*Mathf.PI*(t-.71f)/1.3f)));
                float hit=t>=.71f && t<.9f?1-(t-.71f)/.19f:0;
                control.localScale=new Vector3(1+.25f*hit,1-.35f*hit,1+.25f*hit);
            }
            else
            {
                float t=time%1.6f,sweep=t<.35f?Mathf.SmoothStep(0,1,t/.35f):t<.7f?1:1-Mathf.SmoothStep(0,1,(t-.7f)/.8f);
                right.Root.localPosition=new Vector3(Mathf.Lerp(.11f,-.11f,sweep),0,0);
                // Palm faces the way it slaps.
                // Fingers up, palm toward the panel: the player sees the back of a hand sweeping across, tipped into the sweep.
                right.Root.localRotation=Quaternion.Euler(0,0,(sweep>0 && sweep<1?18:6)*(t<.7f?1:-1))*Quaternion.LookRotation(Vector3.up,Vector3.back);
                // Flat and open through the slap; fingers relax on the slow way back.
                right.Draw(EmberdeepHandPoseKind.LeverGrip,t<.7f?0:.3f*Mathf.Sin(Mathf.Clamp01((t-.7f)/.8f)*Mathf.PI));
            }
        }
        void PoseHand(ModelHand h,Transform anchor,EmberdeepHandPoseKind kind,Vector3 offset,float curl,bool isLeft)
        {
            var pose=EmberdeepHandPose.GripContact(kind,isLeft);
            h.Root.SetPositionAndRotation(anchor.TransformPoint(pose.position)+model.TransformVector(offset),anchor.rotation*pose.rotation);
            h.Draw(kind,curl);
        }
        void Stream(Vector3 origin,Vector3 direction)
        {water.enabled=true;for(int i=0;i<8;i++){float d=i*.025f;water.SetPosition(i,origin+direction*d+Vector3.down*d*d*2);}}
        static Transform Node(string label,Transform parent,Vector3 at)
        {var t=new GameObject(label).transform;t.SetParent(parent,false);t.localPosition=at;return t;}
        void Shape(Transform parent,PrimitiveType type,Vector3 at,Vector3 scale)
        {
            var go=GameObject.CreatePrimitive(type);go.name="Blue demonstration geometry";go.transform.SetParent(parent,false);go.transform.localPosition=at;go.transform.localScale=scale;
            var c=go.GetComponent<Collider>();c.enabled=false;Destroy(c);var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=blue;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
        }
        void OnDestroy(){left?.Dispose();right?.Dispose();if(blue)Destroy(blue);if(pale)Destroy(pale);if(glove)Destroy(glove);}
        sealed class ModelHand
        {
            public readonly Transform Root;
            readonly Mesh mesh;
            readonly bool left;
            readonly List<Vector3> v=new(600);
            readonly List<int> indices=new(2400);
            readonly List<Color> tones=new(600);
            static readonly Color Suede=new(.40f,.56f,.22f),Leather=new(.50f,.29f,.14f),Fleece=new(.97f,.90f,.72f),Pad=new(.31f,.45f,.17f);
            readonly bool plain; // ghost hints: one pale tone instead of the glove's suede, leather and fleece
            public ModelHand(Transform parent,Material material,bool isLeft,bool plainTone=false)
            {
                left=isLeft;plain=plainTone;Root=Node(isLeft?"Left demonstration hand":"Right demonstration hand",parent,Vector3.zero);
                mesh=new Mesh{name="Articulated tutorial hand"};mesh.MarkDynamic();Root.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                var r=Root.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            }
            public void Draw(EmberdeepHandPoseKind kind,float closure,bool pointing=false)
            {
                v.Clear();indices.Clear();tones.Clear();
                // Fleece roll, leather strap and suede sleeve, then the palm.
                Limb(new Vector3(0,0,-.074f),new Vector3(0,0,-.064f),.027f,.019f,Fleece);
                Limb(new Vector3(0,0,-.062f),new Vector3(0,0,-.044f),.024f,.016f,Leather);
                Limb(new Vector3(0,0,-.046f),new Vector3(0,0,-.02f),.021f,.013f,Suede);
                Limb(new Vector3(0,0,-.026f),new Vector3(0,0,.025f),.033f,.012f,Suede);
                for(int f=0;f<5;f++)
                {
                    bool thumb=f==0;float side=left?-1:1;
                    var p=thumb?new Vector3(-side*.030f,0,-.01f):new Vector3((f-2.5f)*.017f,0,.025f);
                    float authored=EmberdeepHandPose.Curl(kind,(Oculus.Interaction.Input.HandFinger)f);
                    float bend=closure*authored;
                    if(pointing)bend=f==(left?4:1)?0:.85f;
                    var q=thumb?Quaternion.Euler(0,-side*48,0):Quaternion.identity;
                    for(int j=0;j<3;j++)
                    {
                        q*=Quaternion.Euler(-bend*(j==0?65:85),0,0);
                        var end=p+q*Vector3.forward*(thumb?.019f:j==0?.027f:.019f);
                        Limb(p,end,thumb?.009f:.0075f,thumb?.009f:.0075f,j==2?Pad:Suede);p=end;
                    }
                }
                mesh.Clear();mesh.SetVertices(v);mesh.SetColors(tones);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            }
            void Limb(Vector3 a,Vector3 b,float width,float depth,Color tone)
            {
                int start=v.Count;var z=(b-a).normalized;var x=Vector3.Cross(Vector3.up,z).normalized;if(x.sqrMagnitude<.1f)x=Vector3.right;var y=Vector3.Cross(z,x);
                const int sides=10;
                // Overlap the rounded ends to keep joints visually connected.
                var begin=a-z*width*.30f;var end=b+z*width*.30f;
                if(plain)tone=Color.white;
                for(int ring=0;ring<4;ring++)for(int side=0;side<sides;side++)
                {float r=ring==0 || ring==3?.70f:1;float angle=side*Mathf.PI*2/sides;v.Add(Vector3.Lerp(begin,end,ring/3f)+(x*Mathf.Cos(angle)*width+y*Mathf.Sin(angle)*depth)*r);tones.Add(tone);}
                int cap=v.Count;v.Add(begin);v.Add(end);tones.Add(tone);tones.Add(tone);
                for(int side=0;side<sides;side++)
                {indices.Add(cap);indices.Add(start+(side+1)%sides);indices.Add(start+side);indices.Add(cap+1);indices.Add(start+3*sides+side);indices.Add(start+3*sides+(side+1)%sides);}
                for(int ring=0;ring<3;ring++)for(int side=0;side<sides;side++)
                {int a0=start+ring*sides+side,b0=start+ring*sides+(side+1)%sides;indices.Add(a0);indices.Add(b0);indices.Add(a0+sides);indices.Add(b0);indices.Add(b0+sides);indices.Add(a0+sides);}
            }
            public void Dispose(){if(mesh)Object.Destroy(mesh);}
        }
    }
}

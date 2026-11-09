using UnityEngine;

namespace FireRescue
{
    public sealed class FireEquipmentView : MonoBehaviour
    {
        public Transform Muzzle { get; private set; }
        public Transform Grip { get; private set; }
        public Transform Pump { get; private set; }
        public Transform PumpGrip => pumpSlide;
        public Transform RefillPort { get; private set; }
        public FireEquipmentId Id { get; private set; }
        Transform fill, needle, lever, barrel, pumpSlide, pumpRail;
        Renderer indicator;
        Material shell, trim, bright;
        Color bodyColor;
        MaterialPropertyBlock block;
        void Awake()=>block=new MaterialPropertyBlock();
        float recoil;
        Vector3 barrelRest;
        public void Build(FireEquipmentId id,Material body,Material dark,Material emissive)
        {
            Id=id;shell=body;trim=dark;bright=emissive;bodyColor=FireEquipment.Get(id).Color;
            Muzzle=new GameObject("Water outlet").transform;Muzzle.SetParent(transform,false);
            Grip=new GameObject("Grip anchor").transform;Grip.SetParent(transform,false);Grip.localPosition=id==FireEquipmentId.Extinguisher?new Vector3(0,.085f,-.025f):new Vector3(0,-.04f,-.015f);
            if(id==FireEquipmentId.Extinguisher)Extinguisher();else Gun(id);
            RefillPort=new GameObject("RefillPort").transform;
            RefillPort.SetParent(transform,false);
            RefillPort.localPosition=new Vector3(0,-.09f,.055f);
            Part("Cyan refill coupling",PrimitiveType.Cylinder,RefillPort.localPosition,new Vector3(.04f,.012f,.04f),Color.cyan,bright).transform.localRotation=Quaternion.Euler(90,0,0);
        }
        void Extinguisher()
        {
            Part("Red pressure cylinder",PrimitiveType.Cylinder,new Vector3(0,-.045f,0),new Vector3(.1f,.105f,.1f),bodyColor);
            Part("Rounded shoulder",PrimitiveType.Sphere,new Vector3(0,.06f,0),new Vector3(.1f,.045f,.1f),bodyColor);
            Part("Base boot",PrimitiveType.Cylinder,new Vector3(0,-.15f,0),new Vector3(.105f,.012f,.105f),Color.gray,trim);
            Part("Valve block",PrimitiveType.Cube,new Vector3(0,.085f,0),new Vector3(.04f,.035f,.04f),Color.gray,trim);
            lever=Part("Squeeze handle",PrimitiveType.Cube,new Vector3(0,.105f,-.025f),new Vector3(.025f,.012f,.09f),Color.gray,trim).transform;
            Part("Safety pin",PrimitiveType.Sphere,new Vector3(.035f,.086f,-.01f),new Vector3(.025f,.025f,.008f),new Color(1,.8f,.15f));
            Tube("Flexible hose A",new Vector3(.025f,.08f,0),new Vector3(.065f,.06f,.07f),.009f,trim);
            Tube("Flexible hose B",new Vector3(.065f,.06f,.07f),new Vector3(.035f,.015f,.12f),.009f,trim);
            Tube("Discharge horn",new Vector3(.035f,.015f,.12f),new Vector3(0,.015f,.18f),.022f,trim);
            Muzzle.localPosition=new Vector3(0,.015f,.18f);
            Part("Instruction band",PrimitiveType.Cube,new Vector3(0,-.045f,-.051f),new Vector3(.061f,.075f,.003f),new Color(.95f,.9f,.72f));
            Gauge(new Vector3(0,.065f,-.055f));Tank(new Vector3(.044f,-.05f,-.03f));
        }
        void Gun(FireEquipmentId id)
        {
            bool cannon=id==FireEquipmentId.PressureCannon,fan=id==FireEquipmentId.RainFan;
            Part("Contoured receiver",PrimitiveType.Cube,new Vector3(0,.012f,.025f),new Vector3(cannon?.105f:.065f,.065f,.15f),bodyColor);
            Part("Grip",PrimitiveType.Cube,new Vector3(0,-.055f,-.018f),new Vector3(.04f,.115f,.05f),new Color(.09f,.11f,.13f),trim).transform.localRotation=Quaternion.Euler(-12,0,0);
            Part("Trigger guard",PrimitiveType.Cube,new Vector3(0,-.035f,.04f),new Vector3(.035f,.01f,.055f),Color.gray,trim);
            lever=Part("Trigger",PrimitiveType.Cube,new Vector3(0,-.012f,.035f),new Vector3(.014f,.035f,.008f),Color.white).transform;
            Part("Reservoir",PrimitiveType.Capsule,new Vector3(0,.075f,-.025f),new Vector3(cannon?.075f:.055f,.055f,.075f),new Color(.3f,.75f,.9f));
            Part("Reservoir cap",PrimitiveType.Cylinder,new Vector3(0,.13f,-.025f),new Vector3(.045f,.008f,.045f),Color.gray,trim);
            float length=cannon?.26f:.18f;
            barrel=new GameObject("Recoil barrel").transform;barrel.SetParent(transform,false);barrelRest=barrel.localPosition;
            var pipe=Tube("Pressure barrel",new Vector3(0,.015f,.06f),new Vector3(0,.015f,length),cannon?.038f:.018f,trim);pipe.transform.SetParent(barrel,true);
            for(int i=0;i<3;i++)
            {
                var ring=Part("Barrel band "+i,PrimitiveType.Cylinder,new Vector3(0,.015f,.09f+i*(length-.1f)/3),new Vector3(cannon?.088f:.05f,.007f,cannon?.088f:.05f),bodyColor);
                ring.transform.localRotation=Quaternion.Euler(90,0,0);ring.transform.SetParent(barrel,true);
            }
            if(fan)
            {
                Part("Fan manifold",PrimitiveType.Cube,new Vector3(0,.015f,.16f),new Vector3(.19f,.04f,.045f),bodyColor);
                for(int i=-2;i<=2;i++)Tube("Fan outlet "+i,new Vector3(i*.035f,.015f,.17f),new Vector3(i*.042f,.015f,.205f),.011f,trim);
                length=.205f;
            }
            if(cannon)
            {
                Part("Forward support grip",PrimitiveType.Cube,new Vector3(0,-.05f,.14f),new Vector3(.04f,.1f,.045f),Color.gray,trim);
                Pump=new GameObject("Pump grab anchor").transform;Pump.SetParent(transform,false);Pump.localPosition=new Vector3(-.095f,.01f,.13f);
                pumpRail=Tube("Pump rail",new Vector3(-.095f,.01f,-.01f),Pump.localPosition,.006f,trim).transform;
                pumpSlide=Part("Cyan pull handle",PrimitiveType.Capsule,Pump.localPosition,new Vector3(.035f,.035f,.035f),Color.cyan).transform;
                for(int i=0;i<5;i++)Part("Pressure cell "+i,PrimitiveType.Cube,new Vector3(.054f,.01f,-.025f+i*.021f),new Vector3(.006f,.035f,.012f),new Color(.2f,.85f,1),bright);
            }
            Muzzle.localPosition=new Vector3(0,.015f,length);
            Gauge(new Vector3(0,.035f,-.055f));Tank(new Vector3(-.038f,.02f,0));
        }
        void Gauge(Vector3 at)
        {
            Part("Gauge rim",PrimitiveType.Sphere,at,new Vector3(.038f,.038f,.009f),Color.gray,trim);
            Part("Gauge face",PrimitiveType.Sphere,at+Vector3.back*.005f,new Vector3(.03f,.03f,.004f),new Color(.95f,.94f,.8f));
            needle=Part("Pressure needle",PrimitiveType.Cube,at+Vector3.back*.008f,new Vector3(.002f,.021f,.002f),Color.red).transform;
            indicator=Part("Status lamp",PrimitiveType.Sphere,at+new Vector3(.025f,0,0),Vector3.one*.012f,Color.cyan,bright).GetComponent<Renderer>();
        }
        void Tank(Vector3 at)
        {
            Part("Tank window",PrimitiveType.Cube,at,new Vector3(.009f,.084f,.022f),Color.gray,trim);
            fill=Part("Water gauge",PrimitiveType.Cube,at+Vector3.back*.012f,new Vector3(.01f,.08f,.005f),Color.cyan,bright).transform;
        }
        public void Animate(float water,float charge,bool firing,float dt)
        {
            fill.localScale=new Vector3(.01f,Mathf.Max(.004f,water*.08f),.005f);
            needle.localRotation=Quaternion.Euler(0,0,Mathf.Lerp(65,-65,Id==FireEquipmentId.PressureCannon?charge:water));
            if(firing)recoil=1;else recoil=Mathf.MoveTowards(recoil,0,dt*12);
            if(barrel)barrel.localPosition=barrelRest+Vector3.back*(recoil*.012f);
            if(lever)lever.localRotation=Quaternion.Euler(firing?12:0,0,0);
            Tint(indicator,Color.Lerp(new Color(.1f,.7f,1),new Color(1,.4f,.05f),charge));
        }
        public void AnimatePump(float pull){if(pumpSlide)pumpSlide.localPosition=Pump.localPosition+Vector3.back*(Mathf.Clamp01(pull)*.09f);}
        public void SetLeftHanded(bool left)
        {
            if(!Pump)return;
            var p=Pump.localPosition;p.x=left?.095f:-.095f;Pump.localPosition=p;
            p=pumpRail.localPosition;p.x=Pump.localPosition.x;pumpRail.localPosition=p;AnimatePump(0);
        }
        GameObject Tube(string name,Vector3 from,Vector3 to,float radius,Material material)
        {
            var go=Part(name,PrimitiveType.Cylinder,(from+to)*.5f,new Vector3(radius*2,Vector3.Distance(from,to)*.5f,radius*2),Color.white,material);
            go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,to-from);return go;
        }
        GameObject Part(string name,PrimitiveType type,Vector3 at,Vector3 size,Color color,Material material=null)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(transform,false);go.transform.localPosition=at;go.transform.localScale=size;
            var collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            var r=go.GetComponent<Renderer>();r.sharedMaterial=material?material:shell;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;Tint(r,color);return go;
        }
        void Tint(Renderer r,Color color){block.Clear();block.SetColor("_BaseColor",color);r.SetPropertyBlock(block);}
    }
}

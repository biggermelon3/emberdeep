using UnityEngine;

namespace FireRescue
{
    // Small readable landmarks make each incident a place with something worth saving.
    // Rebuilt only between incidents; all interaction still comes from the shared water simulation.
    public sealed class FireDiorama : MonoBehaviour
    {
        sealed class PropView { public Transform Fill, Rotor, Root; public Renderer Body; public TextMesh Label; public Renderer[] Authored; }
        PropView[] views=System.Array.Empty<PropView>();
        Transform scenery;
        Material foliage,bark,water,panel;
        EmberdeepArtLibrary art;
        MaterialPropertyBlock block;
        Mesh pine;
        void Awake(){block=new MaterialPropertyBlock();pine=MakePine();}
        public Mesh PineMesh=>pine;
        public void Build(FireRun run,Transform arena,Material green,Material wood,Material blue,Material dark,EmberdeepArtLibrary authored=null)
        {
            art=authored;
            foliage=green;bark=wood;water=blue;panel=dark;
            if(scenery){scenery.gameObject.SetActive(false);Destroy(scenery.gameObject);}
            scenery=new GameObject("Incident landmarks").transform;scenery.SetParent(arena,false);
            // A shallow stream, stepping stones and perimeter fencing stay outside the aim lanes.
            if(!art)Part("Creek",PrimitiveType.Cube,scenery,new Vector3(0,.015f,10.9f),new Vector3(10.4f,.035f,1.2f),new Color(.08f,.44f,.58f),water);
            if(!art)
            {
            for(int i=0;i<6;i++)Part("Creek stone",PrimitiveType.Sphere,scenery,new Vector3(-3.8f+i*1.5f,.06f,10.8f+(i%2)*.22f),new Vector3(.58f,.2f,.45f),new Color(.45f,.52f,.5f),bark);
            for(int side=-1;side<=1;side+=2)
            {
                Part("Park fence rail",PrimitiveType.Cube,scenery,new Vector3(side*5.1f,.35f,6),new Vector3(.08f,.08f,8.9f),new Color(.72f,.54f,.31f),bark);
                for(int i=0;i<5;i++)Part("Fence post",PrimitiveType.Cube,scenery,new Vector3(side*5.1f,.25f,2+i*2),new Vector3(.14f,.5f,.14f),new Color(.68f,.49f,.28f),bark);
            }
            Part("Station walkway",PrimitiveType.Cube,scenery,new Vector3(0,.015f,1.35f),new Vector3(9.6f,.025f,.45f),new Color(.64f,.57f,.39f),bark);
            }
            views=new PropView[run.Props.Length];
            for(int i=0;i<views.Length;i++)
            {
                var data=run.Props[i];var root=new GameObject(data.Kind+" "+i).transform;root.SetParent(scenery,false);root.localPosition=new Vector3(data.Position.x,0,data.Position.z);
                var view=new PropView{Root=root};views[i]=view;
                if(data.Kind==FirePropKind.Reservoir)BuildReservoir(view,data.Position.y);else BuildCabin(view);
            }
        }
        void BuildReservoir(PropView view,float height)
        {
            var root=view.Root;
            Part("Sprinkler footing",PrimitiveType.Cylinder,root,new Vector3(0,.03f,0),new Vector3(.85f,.04f,.85f),new Color(.26f,.35f,.36f),panel);
            for(int i=-1;i<=1;i+=2)Part("Tower legs",PrimitiveType.Cube,root,new Vector3(i*.22f,(height-.32f)*.5f,0),new Vector3(.075f,height-.32f,.1f),new Color(.65f,.7f,.71f),panel);
            view.Body=Part("Blue pressure tank",PrimitiveType.Cylinder,root,new Vector3(0,height,0),new Vector3(.69f,.32f,.69f),new Color(.08f,.42f,.65f),foliage).GetComponent<Renderer>();
            Part("Top rim",PrimitiveType.Cylinder,root,new Vector3(0,height+.33f,0),new Vector3(.76f,.045f,.76f),new Color(.74f,.9f,.94f),panel);
            Part("Fill gauge surround",PrimitiveType.Cube,root,new Vector3(0,height+.02f,-.35f),new Vector3(.2f,.5f,.035f),new Color(.03f,.17f,.22f),panel);
            view.Fill=Part("Water level",PrimitiveType.Cube,root,new Vector3(0,height-.22f,-.38f),new Vector3(.14f,.01f,.025f),Color.cyan,water).transform;
            view.Rotor=Part("Rotating sprinkler",PrimitiveType.Cube,root,new Vector3(0,height+.43f,0),new Vector3(.85f,.07f,.09f),new Color(.7f,.92f,1),panel).transform;
            view.Label=Label(root,"FILL TO RAIN",new Vector3(0,height+.75f,0),.17f,new Color(.5f,.93f,1));
        }
        void BuildCabin(PropView view)
        {
            var root=view.Root;
            var cabin=art?art.Create("Cabin",root,Vector3.zero):null;
            if(cabin)
            {
                view.Authored=cabin.GetComponentsInChildren<Renderer>();
                view.Body=view.Authored[0];
                view.Fill=Part("Cabin safety",PrimitiveType.Cube,root,new Vector3(0,1.4f,0),new Vector3(.8f,.055f,.035f),new Color(.3f,1,.6f),foliage).transform;
                view.Label=Label(root,"PROTECT +$50",new Vector3(0,1.65f,0),.13f,new Color(1,.86f,.5f));
                return;
            }
            Part("Cabin foundation",PrimitiveType.Cube,root,new Vector3(0,.06f,0),new Vector3(1.02f,.12f,.9f),new Color(.38f,.4f,.34f),bark);
            view.Body=Part("Cabin walls",PrimitiveType.Cube,root,new Vector3(0,.43f,0),new Vector3(.87f,.73f,.75f),new Color(.88f,.67f,.38f),foliage).GetComponent<Renderer>();
            for(int side=-1;side<=1;side+=2)
            {var roof=Part("Pitched roof",PrimitiveType.Cube,root,new Vector3(side*.24f,.86f,0),new Vector3(.65f,.09f,1.02f),new Color(.68f,.25f,.18f),bark);roof.transform.localRotation=Quaternion.Euler(0,0,-side*32);}
            Part("Door",PrimitiveType.Cube,root,new Vector3(-.18f,.29f,-.38f),new Vector3(.23f,.43f,.03f),new Color(.25f,.23f,.19f),bark);
            Part("Lit window",PrimitiveType.Cube,root,new Vector3(.2f,.5f,-.39f),new Vector3(.23f,.23f,.03f),new Color(1,.82f,.31f),foliage);
            Part("Chimney",PrimitiveType.Cube,root,new Vector3(.25f,1.02f,.15f),new Vector3(.16f,.42f,.18f),new Color(.39f,.36f,.31f),bark);
            view.Fill=Part("Cabin safety",PrimitiveType.Cube,root,new Vector3(0,1.25f,0),new Vector3(.8f,.055f,.035f),new Color(.3f,1,.6f),foliage).transform;
            view.Label=Label(root,"PROTECT +$50",new Vector3(0,1.55f,0),.16f,new Color(1,.86f,.5f));
        }
        public void Tick(FireRun run,Camera camera,float dt)
        {
            for(int i=0;i<views.Length;i++)
            {
                var v=views[i];var p=run.Props[i];v.Label.transform.rotation=camera.transform.rotation;
                if(p.Kind==FirePropKind.Reservoir)
                {
                    float amount=p.ActiveRemaining>0?p.ActiveRemaining/4:p.Used?0:p.Charge01;
                    v.Fill.localScale=new Vector3(.14f,Mathf.Max(.01f,amount*.48f),.025f);v.Fill.localPosition=new Vector3(0,p.Position.y-.22f+amount*.24f,-.38f);
                    v.Rotor.Rotate(0,p.ActiveRemaining>0?dt*420:0,0,Space.Self);
                    v.Label.text=p.ActiveRemaining>0?"RAIN!":p.Used?"EMPTY":$"FILL {p.Charge01:P0}";
                    Tint(v.Body,p.Used?new Color(.2f,.35f,.37f):new Color(.08f,.42f,.65f));
                }
                else
                {
                    v.Fill.localScale=new Vector3(Mathf.Max(.02f,.8f*p.Health01),.055f,.035f);
                    Tint(v.Fill.GetComponent<Renderer>(),Color.Lerp(new Color(1,.2f,.07f),new Color(.3f,1,.6f),p.Health01));
                    v.Label.text=p.Health01<=0?"CABIN LOST":$"PROTECT {p.Health01:P0}";
                    if(v.Authored!=null)
                    {
                        block.Clear();block.SetFloat("_Scorch",1-p.Health01);block.SetFloat("_Wetness",Mathf.Clamp01(p.Wetness));
                        foreach(var renderer in v.Authored)renderer.SetPropertyBlock(block);
                    }
                    else Tint(v.Body,Color.Lerp(new Color(.15f,.13f,.12f),new Color(.88f,.67f,.38f),p.Health01));
                }
            }
        }
        GameObject Part(string name,PrimitiveType type,Transform parent,Vector3 position,Vector3 size,Color color,Material material)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;
            var c=go.GetComponent<Collider>();c.enabled=false;Destroy(c);var r=go.GetComponent<Renderer>();r.sharedMaterial=material;Tint(r,color);return go;
        }
        void Tint(Renderer r,Color color){block.Clear();block.SetColor("_BaseColor",color);r.SetPropertyBlock(block);}
        static TextMesh Label(Transform parent,string value,Vector3 position,float size,Color color)
        {
            var go=new GameObject("Landmark label");go.transform.SetParent(parent,false);go.transform.localPosition=position;
            var t=go.AddComponent<TextMesh>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");go.GetComponent<MeshRenderer>().sharedMaterial=t.font.material;
            t.fontSize=64;t.characterSize=size*20/64;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.text=value;t.color=color;return t;
        }
        static Mesh MakePine()
        {
            // Shared faceted double-tier canopy (no extra renderer for each tier).
            var vertices=new Vector3[48];var triangles=new int[48];int n=0;
            for(int tier=0;tier<2;tier++)for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI/4,b=(i+1)*Mathf.PI/4,r=tier==0?.62f:.46f,y=tier==0?-.5f:0;
                vertices[n]=new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r);triangles[n]=n;n++;
                vertices[n]=new Vector3(0,tier==0?.42f:.85f,0);triangles[n]=n;n++;
                vertices[n]=new Vector3(Mathf.Cos(b)*r,y,Mathf.Sin(b)*r);triangles[n]=n;n++;
            }
            var mesh=new Mesh{name="Shared low poly pine"};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        void OnDestroy(){if(pine)Destroy(pine);}
    }
}

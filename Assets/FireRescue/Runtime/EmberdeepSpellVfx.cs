using UnityEngine;
using UnityEngine.Rendering;

namespace FireRescue
{
    // Presentation only for the gesture spells: the spinning water orb, the
    // charge bead on the pointing finger and the landing preview. None of
    // these objects has a collider or any authority over water or fire.
    public sealed class EmberdeepSpellVfx : MonoBehaviour
    {
        Material orbMaterial,beadMaterial;
        Transform orb,bead;
        readonly Transform[] droplets=new Transform[3];
        LineRenderer circle,beam;
        float clock;
        public bool OrbVisible=>orb && orb.gameObject.activeSelf;
        public bool BeadVisible=>bead && bead.gameObject.activeSelf;
        public bool CircleVisible=>circle && circle.enabled;
        public bool BeamVisible=>beam && beam.enabled;
        public float OrbRadius {get;private set;}
        public void Initialize(Material line)
        {
            var shader=Resources.Load<Shader>("EmberdeepWaterOrb");
            orbMaterial=shader?new Material(shader){name="Spinning water orb"}:new Material(line);
            beadMaterial=new Material(orbMaterial){name="Bolt charge bead"};
            orb=Sphere("Water orb",orbMaterial);bead=Sphere("Bolt charge bead",beadMaterial);
            for(int i=0;i<droplets.Length;i++)droplets[i]=Sphere("Orb droplet "+i,orbMaterial);
            circle=Line("Spell landing circle",line,48,true,.004f);
            beam=Line("Bolt aim line",line,2,false,.0025f);
            Hide();
        }
        Transform Sphere(string name,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name=name;go.transform.SetParent(transform,false);
            var c=go.GetComponent<Collider>();c.enabled=false;Destroy(c);
            var r=go.GetComponent<Renderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            return go.transform;
        }
        LineRenderer Line(string name,Material material,int points,bool loop,float width)
        {
            var line=new GameObject(name).AddComponent<LineRenderer>();line.transform.SetParent(transform,false);
            line.sharedMaterial=material;line.useWorldSpace=true;line.positionCount=points;line.loop=loop;
            line.startWidth=line.endWidth=width;line.numCornerVertices=2;
            line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.enabled=false;return line;
        }
        public void ShowOrb(bool visible,Vector3 center,float radius,float charge01,float dt)
        {
            if(!orb)return;
            visible=visible && float.IsFinite(center.sqrMagnitude) && float.IsFinite(radius) && radius>0;
            orb.gameObject.SetActive(visible);foreach(var d in droplets)d.gameObject.SetActive(visible);
            OrbRadius=visible?radius:0;
            if(!visible)return;
            if(float.IsFinite(dt))clock+=Mathf.Clamp(dt,0,.1f);
            orb.position=center;orb.localScale=Vector3.one*radius*2;orb.localRotation=Quaternion.Euler(0,clock*140,0);
            orbMaterial.SetFloat("_Charge",Mathf.Clamp01(charge01));
            // Three droplets circle the orb on tilted rings so it never looks static.
            for(int i=0;i<droplets.Length;i++)
            {
                var axis=Quaternion.Euler(i*55,i*120,0)*Vector3.up;
                var arm=Vector3.Cross(axis,Vector3.forward);if(arm.sqrMagnitude<.01f)arm=Vector3.Cross(axis,Vector3.right);
                droplets[i].position=center+Quaternion.AngleAxis(clock*(200+i*45)+i*120,axis)*arm.normalized*radius*1.35f;
                droplets[i].localScale=Vector3.one*radius*(.26f+.08f*Mathf.Sin(clock*5+i));
            }
        }
        public void ShowBead(bool visible,Vector3 tip,float charge01)
        {
            if(!bead)return;
            visible=visible && float.IsFinite(tip.sqrMagnitude);
            bead.gameObject.SetActive(visible);if(!visible)return;
            charge01=Mathf.Clamp01(charge01);
            bead.position=tip;bead.localScale=Vector3.one*Mathf.Lerp(.012f,.034f,charge01);
            beadMaterial.SetFloat("_Charge",charge01);
        }
        public void ShowCircle(bool visible,Vector3 center,Vector3 normal,float radius,Color color)
        {
            if(!circle)return;
            visible=visible && float.IsFinite(center.sqrMagnitude) && float.IsFinite(radius) && radius>0;
            circle.enabled=visible;if(!visible)return;
            normal=normal.sqrMagnitude>.01f?normal.normalized:Vector3.up;
            var right=Vector3.Cross(normal,Vector3.forward);if(right.sqrMagnitude<.01f)right=Vector3.Cross(normal,Vector3.right);
            right.Normalize();var forward=Vector3.Cross(right,normal);
            for(int i=0;i<circle.positionCount;i++)
            {
                float a=i*Mathf.PI*2/circle.positionCount;
                circle.SetPosition(i,center+normal*.003f+(right*Mathf.Cos(a)+forward*Mathf.Sin(a))*radius);
            }
            circle.startColor=circle.endColor=color;
        }
        public void ShowBeam(bool visible,Vector3 from,Vector3 to,Color color)
        {
            if(!beam)return;
            visible=visible && float.IsFinite(from.sqrMagnitude) && float.IsFinite(to.sqrMagnitude);
            beam.enabled=visible;if(!visible)return;
            beam.SetPosition(0,from);beam.SetPosition(1,to);beam.startColor=color;beam.endColor=new Color(color.r,color.g,color.b,.25f);
        }
        public void Hide()
        {
            if(orb)orb.gameObject.SetActive(false);if(bead)bead.gameObject.SetActive(false);
            foreach(var d in droplets)if(d)d.gameObject.SetActive(false);
            if(circle)circle.enabled=false;if(beam)beam.enabled=false;OrbRadius=0;
        }
        void OnDestroy(){if(orbMaterial)Destroy(orbMaterial);if(beadMaterial)Destroy(beadMaterial);}
    }

    // The altar basin beside the well. The lever, wheel and automatic feed pour
    // into it; a hand held in the water drinks it into the spell crystals.
    public sealed class EmberdeepBasinView : MonoBehaviour
    {
        public const float Radius=.07f;
        Transform water;
        TextMesh label;
        public float Fill01 {get;private set;}
        public Vector3 Center=>transform.TransformPoint(new Vector3(0,.05f,0));
        public string LabelText=>label?label.text:"";
        public bool Authored {get;private set;}
        public void Build(Material stone,Material waterMaterial,EmberdeepArtLibrary art=null)
        {
            // The carved stone bowl comes from the Blender kit; the primitives are the fallback.
            var bowl=art?art.Create("Basin",transform,Vector3.zero):null;
            if(bowl)
            {
                Authored=true;
                water=Part("Basin water",PrimitiveType.Cylinder,new Vector3(0,.036f,0),new Vector3(.11f,.0012f,.11f),art.Creek?art.Creek:waterMaterial);
                BuildLabel();SetFill(0);return;
            }
            Part("Altar basin foot",PrimitiveType.Cylinder,new Vector3(0,.008f,0),new Vector3(.075f,.008f,.075f),stone);
            Part("Altar basin bowl",PrimitiveType.Cylinder,new Vector3(0,.03f,0),new Vector3(.15f,.016f,.15f),stone);
            for(int i=0;i<16;i++)
            {
                float a=i*Mathf.PI*2/16;
                var rim=Part("Altar basin rim "+i,PrimitiveType.Cube,new Vector3(Mathf.Cos(a)*.074f,.05f,Mathf.Sin(a)*.074f),new Vector3(.031f,.012f,.012f),stone);
                rim.localRotation=Quaternion.Euler(0,90-a*Mathf.Rad2Deg,0);
            }
            water=Part("Basin water",PrimitiveType.Cylinder,new Vector3(0,.047f,0),new Vector3(.13f,.0015f,.13f),waterMaterial);
            BuildLabel();SetFill(0);
        }
        void BuildLabel()
        {
            var go=new GameObject("Basin label");go.transform.SetParent(transform,false);go.transform.localPosition=new Vector3(0,.10f,0);
            label=go.AddComponent<TextMesh>();label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            go.GetComponent<MeshRenderer>().sharedMaterial=label.font.material;
            label.fontSize=64;label.characterSize=.009f*20/64;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;
            label.color=new Color(.941f,.875f,.753f);
        }
        Transform Part(string name,PrimitiveType kind,Vector3 at,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(kind);go.name=name;go.transform.SetParent(transform,false);
            go.transform.localPosition=at;go.transform.localScale=scale;
            var c=go.GetComponent<Collider>();c.enabled=false;if(Application.isPlaying)Destroy(c);else DestroyImmediate(c);
            var r=go.GetComponent<Renderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;
            return go.transform;
        }
        public void SetFill(float value)
        {
            Fill01=float.IsFinite(value)?Mathf.Clamp01(value):0;
            if(!water)return;
            water.gameObject.SetActive(Fill01>.01f);
            // The authored bowl is hollow: the surface rises inside it and widens toward the rim.
            float width=Authored?.078f+.044f*Fill01:.085f+.045f*Fill01;
            water.localPosition=new Vector3(0,Authored?.032f+.016f*Fill01:.047f+.006f*Fill01,0);water.localScale=new Vector3(width,Authored?.0012f:.0015f,width);
        }
        public void SetLabel(string text,Quaternion facing)
        {if(!label)return;if(label.text!=text)label.text=text;label.transform.rotation=facing;}
        // A hand counts as in the water from the bowl up to a few centimetres above the rim.
        public bool Contains(Vector3 point)
        {
            if(!float.IsFinite(point.sqrMagnitude))return false;
            var local=transform.InverseTransformPoint(point);
            return new Vector2(local.x,local.z).magnitude<=Radius+.015f && local.y>=-.01f && local.y<=.12f;
        }
    }
}

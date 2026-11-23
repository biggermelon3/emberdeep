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
}

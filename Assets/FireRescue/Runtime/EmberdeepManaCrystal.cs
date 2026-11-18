using UnityEngine;
using UnityEngine.Rendering;

namespace FireRescue
{
    // A cut gem in a brass leaf setting; fill travels wrist-to-knuckles in the hand's own frame.
    [DefaultExecutionOrder(11000)]
    public sealed class EmberdeepManaCrystal : MonoBehaviour
    {
        Material material;Mesh mesh;
        EmberdeepHandVisuals handVisuals;
        public void FollowLeftHand(EmberdeepHandVisuals source)=>handVisuals=source;
        void LateUpdate()
        {
            if(handVisuals && handVisuals.TryDisplayPalm(true,out var palm))
                transform.SetPositionAndRotation(palm.position+palm.rotation*(Offset-Vector3.up*.016f),palm.rotation);
        }
        // Palm-frame offset: the small spell crystals sit beside the main one.
        public Vector3 Offset;
        public void SetTint(Color color){Ensure();material.SetColor("_Tint",color);}
        public float Fill {get;private set;}
        public bool Authored {get;private set;}
        void Awake()=>Ensure();
        // Built on first use as well as on Awake: a crystal created under an inactive
        // parent (a closed shop page) is tinted and filled before it ever wakes.
        void Ensure()
        {
            if(material)return;
            material=new Material(Resources.Load<Shader>("EmberdeepManaCrystal")){name="Left hand water crystal"};material.SetColor("_Tint",new Color(.04f,.76f,.88f));
            // Blender kit: faceted stone, brass leaf setting and a cream leather patch.
            var art=EmberdeepArtLibrary.Load();var kit=art?art.Create("Crystal",transform,Vector3.zero):null;
            if(kit)
            {
                kit.name="Crystal in brass leaf setting";
                foreach(var part in kit.GetComponentsInChildren<Renderer>(true))
                {
                    part.shadowCastingMode=ShadowCastingMode.Off;part.receiveShadows=false;
                    if(part.name.StartsWith("Gem"))part.sharedMaterial=material;
                }
                Authored=true;return;
            }
            Vector3[] corners={new(-.018f,0,0),new(0,0,.029f),new(.018f,0,0),new(0,0,-.029f)};
            var vertices=new Vector3[24];var triangles=new int[24];
            for(int i=0;i<4;i++)for(int side=0;side<2;side++)
            {int k=(i*2+side)*3;vertices[k]=new Vector3(0,side==0?-.012f:.006f,0);vertices[k+1]=corners[side==0?i:(i+1)%4];vertices[k+2]=corners[side==0?(i+1)%4:i];for(int j=0;j<3;j++)triangles[k+j]=k+j;}
            mesh=new Mesh{name="Faceted mana crystal"};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var r=gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            EmberdeepCraftDetails.Crystal(transform);
        }
        public void SetFill(float value){Ensure();Fill=Mathf.Clamp01(value);material.SetFloat("_Fill",Fill);}
        void OnDestroy(){if(material)Destroy(material);if(mesh)Destroy(mesh);}
    }
}

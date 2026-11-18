using System.Collections.Generic;
using PhysicalAssembly;
using UnityEngine;
using UnityEngine.Rendering;

namespace FireRescue
{
    // Forest signboards: painted boards in a carved timber frame, pill buttons with a
    // brass lip. Preserves the existing BoxCollider hit targets.
    [DefaultExecutionOrder(11010)]
    public sealed class EmberdeepForestUi : MonoBehaviour
    {
        Mesh mesh;Material material;Renderer surface;WorkshopButton button;MaterialPropertyBlock tint;
        Transform frame;
        public bool Guided;
        public Transform Frame=>frame;
        public static void Apply(GameObject go,bool isButton=false)
        {var skin=go.AddComponent<EmberdeepForestUi>();skin.Build(isButton);}
        void Build(bool isButton)
        {
            surface=GetComponent<Renderer>();tint=new MaterialPropertyBlock();
            material=new Material(Resources.Load<Shader>("EmberdeepForestUi"));surface.sharedMaterial=material;
            material.SetColor("_BaseColor",isButton?new Color(.13f,.29f,.22f):new Color(.075f,.17f,.13f));
            material.SetColor("_EdgeColor",isButton?new Color(.78f,.60f,.26f):new Color(.70f,.54f,.24f));
            var scale=transform.localScale;var world=transform.lossyScale;
            bool framed=!isButton && name!="Preview recess" && Mathf.Min(Mathf.Abs(world.x),Mathf.Abs(world.y))>=.12f;
            // Buttons are pills; framed boards keep nearly square corners under the timber knots.
            float radius=Mathf.Min(scale.x,scale.y)*(isButton?.46f:framed?.035f:.12f);
            float metres=Mathf.Abs(scale.x)>1e-6f?Mathf.Abs(world.x/scale.x):1;
            material.SetVector("_Size",new Vector4(Mathf.Abs(world.x),Mathf.Abs(world.y),0,0));
            material.SetFloat("_Radius",radius*metres);material.SetFloat("_Button",isButton?1:0);
            var ring=new List<Vector3>();
            for(int c=0;c<4;c++)for(int i=0;i<7;i++)
            {float a=(c*90+i*90/6f)*Mathf.Deg2Rad;float x=(c==0 || c==3?1:-1)*(.5f-radius/scale.x);float y=(c<2?1:-1)*(.5f-radius/scale.y);ring.Add(new Vector3(x+Mathf.Cos(a)*radius/scale.x,y+Mathf.Sin(a)*radius/scale.y,-.5f));}
            var v=new List<Vector3>{new Vector3(0,0,-.5f),new Vector3(0,0,.5f)};var indices=new List<int>();
            foreach(var p in ring){v.Add(p);v.Add(new Vector3(p.x,p.y,.5f));}
            for(int i=0;i<ring.Count;i++)
            {int a=2+i*2,b=2+(i+1)%ring.Count*2;indices.AddRange(new[]{0,b,a,1,a+1,b+1,a,b,b+1,a,b+1,a+1});}
            mesh=new Mesh{name="Rounded forest sign"};mesh.SetVertices(v);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();GetComponent<MeshFilter>().sharedMesh=mesh;
            if(framed)BuildFrame(world);
        }
        // Timber rails, corner knots with brass leaves, a crest, moss and two lanterns from the
        // Blender kit. The frame root cancels the board's stretch, so pieces are placed in metres.
        void BuildFrame(Vector3 world)
        {
            var art=EmberdeepArtLibrary.Load();
            if(!art || !art.Find("SignRail") || !art.Find("SignCorner"))return;
            float sx=Mathf.Abs(world.x),sy=Mathf.Abs(world.y),sz=Mathf.Abs(world.z);
            if(sx<1e-5f || sy<1e-5f || sz<1e-5f)return;
            frame=new GameObject("Carved forest frame").transform;frame.SetParent(transform,false);
            frame.localScale=new Vector3(1/sx,1/sy,1/sz);
            float w=sx*.5f,h=sy*.5f,front=-sz*.5f,k=Mathf.Clamp(Mathf.Min(sx,sy)/.36f,.5f,1.1f);
            Rail(art,new Vector3(0,h,front),0,sx,k);Rail(art,new Vector3(0,-h,front),0,sx,k);
            Rail(art,new Vector3(-w,0,front),90,sy,k);Rail(art,new Vector3(w,0,front),90,sy,k);
            for(int c=0;c<4;c++)
            {
                // Authored for the top-right corner; quarter turns bring the leaves inward elsewhere.
                var at=new Vector3(c==0 || c==3?w:-w,c<2?h:-h,front);
                Piece(art,"SignCorner",at,Quaternion.Euler(0,0,c*90),k);
            }
            if(sx>=.30f)Piece(art,"SignCrest",new Vector3(0,h+.012f*k,front),Quaternion.identity,k);
            if(sx>=.45f)
            {
                Piece(art,"SignLantern",new Vector3(w+.006f,h-.03f*k,front+.004f),Quaternion.identity,k);
                Piece(art,"SignLantern",new Vector3(-w-.006f,h-.03f*k,front+.004f),Quaternion.Euler(0,180,0),k);
            }
            if(sx>=.30f)
            {
                Piece(art,"SignMoss",new Vector3(-w*.80f,-h+.006f*k,front-.004f),Quaternion.identity,k);
                Piece(art,"SignMoss",new Vector3(w*.78f,-h+.006f*k,front-.004f),Quaternion.Euler(0,0,4),k*.9f);
            }
            foreach(var r in frame.GetComponentsInChildren<Renderer>(true))
            {r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.gameObject.layer=gameObject.layer;}
        }
        void Rail(EmberdeepArtLibrary art,Vector3 at,float angle,float length,float thickness)
        {
            var rail=art.Create("SignRail",frame,at);if(!rail)return;
            rail.localRotation=Quaternion.Euler(0,0,angle);rail.localScale=new Vector3(length,thickness,thickness);
        }
        void Piece(EmberdeepArtLibrary art,string id,Vector3 at,Quaternion rotation,float scale)
        {
            var piece=art.Create(id,frame,at,scale);if(piece)piece.localRotation=rotation;
        }
        void LateUpdate()
        {
            if(!surface)return;if(!button)button=GetComponent<WorkshopButton>();if(!button)return;
            Color color=!button.Interactable?new Color(.105f,.14f,.115f):button.IsHovered?new Color(.24f,.43f,.29f):new Color(.13f,.29f,.22f);
            if(Guided && button.Interactable)color=Color.Lerp(color,new Color(.45f,.35f,.13f),.65f+.2f*Mathf.Sin(Time.unscaledTime*3));
            tint.SetColor("_BaseColor",color);surface.SetPropertyBlock(tint);
        }
        void OnDestroy(){if(mesh)Destroy(mesh);if(material)Destroy(material);}
    }
}

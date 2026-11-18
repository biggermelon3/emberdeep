using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FireRescue
{
    // Decorative meshes only. Never supplies colliders, input or gameplay anchors.
    public sealed class EmberdeepCraftDetails : MonoBehaviour
    {
        readonly List<Vector3> vertices=new();
        readonly List<Color> colors=new();
        readonly List<int> triangles=new();
        Mesh mesh;Material material;
        static Mesh softBox;
        public static readonly Color Brass=new(.68f,.43f,.16f),Rubber=new(.055f,.085f,.072f),Cream=new(.90f,.82f,.61f),Wood=new(.32f,.17f,.075f);
        public static Mesh SoftBox
        {
            get
            {
                if(softBox)return softBox;
                var v=new List<Vector3>();var n=new List<Vector3>();var t=new List<int>();
                Vector3[] axes={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
                foreach(var normal in axes)
                {
                    var u=Vector3.Cross(normal,Mathf.Abs(normal.y)>.5f?Vector3.forward:Vector3.up).normalized;
                    var w=Vector3.Cross(normal,u);int start=v.Count;
                    const int steps=4;
                    for(int y=0;y<=steps;y++)for(int x=0;x<=steps;x++)
                    {
                        var p=normal*.5f+u*(x/(float)steps-.5f)+w*(y/(float)steps-.5f);
                        var core=new Vector3(Mathf.Clamp(p.x,-.39f,.39f),Mathf.Clamp(p.y,-.39f,.39f),Mathf.Clamp(p.z,-.39f,.39f));
                        var outward=(p-core).normalized;v.Add(core+outward*.11f);n.Add(outward);
                    }
                    for(int y=0;y<steps;y++)for(int x=0;x<steps;x++)
                    {int a=start+y*(steps+1)+x;t.AddRange(new[]{a,a+1,a+steps+1,a+1,a+steps+2,a+steps+1});}
                }
                softBox=new Mesh{name="Shared crafted soft box"};softBox.SetVertices(v);softBox.SetNormals(n);softBox.SetTriangles(t,0);softBox.RecalculateBounds();return softBox;
            }
        }
        public static EmberdeepCraftDetails Create(Transform owner,string name)
        {
            var go=new GameObject(name);go.transform.SetParent(owner,false);return go.AddComponent<EmberdeepCraftDetails>();
        }
        public void Box(Vector3 p,Vector3 size,Color color,Quaternion rotation=default)
        {
            if(rotation==default)rotation=Quaternion.identity;
            var source=SoftBox;int start=vertices.Count;
            foreach(var v in source.vertices){vertices.Add(p+rotation*Vector3.Scale(v,size));colors.Add(color);}
            foreach(int i in source.triangles)triangles.Add(start+i);
        }
        public void Ring(Vector3 center,float radius,float tube,Color color,Quaternion rotation=default,int count=24)
        {
            if(rotation==default)rotation=Quaternion.identity;
            int start=vertices.Count;const int sides=6;
            for(int i=0;i<count;i++)for(int j=0;j<sides;j++)
            {
                float a=i*Mathf.PI*2/count,b=j*Mathf.PI*2/sides,r=radius+Mathf.Cos(b)*tube;
                vertices.Add(center+rotation*new Vector3(Mathf.Cos(a)*r,Mathf.Sin(b)*tube,Mathf.Sin(a)*r));colors.Add(color);
            }
            for(int i=0;i<count;i++)for(int j=0;j<sides;j++)
            {
                int a=start+i*sides+j,b=start+((i+1)%count)*sides+j,c=start+((i+1)%count)*sides+(j+1)%sides,d=start+i*sides+(j+1)%sides;
                triangles.AddRange(new[]{a,c,b,a,d,c});
            }
        }
        public void Finish()
        {
            mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            material=new Material(Resources.Load<Shader>("EmberdeepCraft")){name="Warm crafted details"};
            var r=gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
        }
        public static void Tool(Transform owner,FireEquipmentId id)
        {
            var d=Create(owner,"Crafted equipment fittings");
            if(id==FireEquipmentId.Extinguisher)
            {
                d.Ring(new Vector3(0,-.125f,0),.050f,.003f,Brass);
                d.Ring(new Vector3(0,.046f,0),.047f,.0025f,Brass);
                for(int i=0;i<4;i++)d.Box(new Vector3(0,-.075f+i*.016f,-.053f),new Vector3(.039f-i*.004f,.002f,.001f),Wood);
                for(int side=-1;side<=1;side+=2)d.Box(new Vector3(side*.017f,.100f,-.075f),new Vector3(.003f,.013f,.065f),Rubber);
            }
            else
            {
                bool cannon=id==FireEquipmentId.PressureCannon;
                float w=cannon?.055f:.035f;
                for(int side=-1;side<=1;side+=2)
                {
                    d.Box(new Vector3(side*w,.013f,.024f),new Vector3(.005f,.042f,.095f),Cream);
                    for(int j=0;j<3;j++)d.Box(new Vector3(side*(w+.003f),.012f,.0f+j*.023f),new Vector3(.002f,.022f,.005f),Brass);
                }
                d.Ring(new Vector3(0,.125f,-.025f),.023f,.003f,Brass);
                for(int j=0;j<4;j++)d.Box(new Vector3(0,-.035f-j*.017f,-.045f),new Vector3(.037f,.004f,.004f),Cream);
            }
            d.Ring(new Vector3(0,id==FireEquipmentId.Extinguisher?.065f:.035f,-.062f),.017f,.0014f,Brass,Quaternion.Euler(90,0,0));
            d.Finish();
        }
        public static void Station(Transform owner)
        {
            var d=Create(owner,"Crafted station fittings");
            d.Box(new Vector3(0,.008f,.055f),new Vector3(.19f,.014f,.15f),Wood);
            for(int side=-1;side<=1;side+=2)
            {
                d.Box(new Vector3(side*.075f,.017f,.055f),new Vector3(.014f,.008f,.12f),Brass);
                d.Box(new Vector3(side*.045f,.11f,.025f),new Vector3(.006f,.105f,.008f),Brass);
            }
            d.Finish();
        }
        public static void Crystal(Transform owner)
        {
            var d=Create(owner,"Leaf bezel");
            // The crystal's visible back-of-hand face points along local -Y.
            d.Ring(new Vector3(0,-.004f,0),.023f,.0016f,Brass,count:32);
            d.transform.localScale=new Vector3(.87f,1,1.32f);
            for(int side=-1;side<=1;side+=2)
            {
                d.Box(new Vector3(side*.022f,-.004f,-.016f),new Vector3(.010f,.002f,.019f),Brass,Quaternion.Euler(0,side*35,0));
                d.Box(new Vector3(side*.023f,-.004f,.003f),new Vector3(.008f,.002f,.015f),Cream,Quaternion.Euler(0,-side*30,0));
            }
            d.Finish();
        }
        public static void Rack(Transform owner)
        {
            var d=Create(owner,"Timber equipment cradle");
            for(int i=0;i<3;i++)d.Box(new Vector3((i-1)*.075f,-.153f,.04f),new Vector3(.071f,.015f,.27f),i==1?Wood:Wood*1.17f);
            for(int side=-1;side<=1;side+=2)
            {
                d.Box(new Vector3(side*.108f,-.139f,.04f),new Vector3(.007f,.010f,.27f),Brass);
                d.Box(new Vector3(side*.103f,-.12f,.16f),new Vector3(.018f,.07f,.019f),Wood);
            }
            d.Finish();
        }
        public static void Reservoir(Transform owner,float height)
        {
            var d=Create(owner,"Sprinkler copper bands and supports");
            d.Ring(new Vector3(0,height-.24f,0),.347f,.014f,Brass);
            d.Ring(new Vector3(0,height+.24f,0),.347f,.014f,Brass);
            for(int side=-1;side<=1;side+=2)
            {
                d.Box(new Vector3(side*.24f,.09f,0),new Vector3(.18f,.06f,.38f),Wood);
                d.Box(new Vector3(side*.12f,height,-.36f),new Vector3(.018f,.50f,.018f),Brass);
            }
            d.Finish();
        }
        void OnDestroy(){if(mesh)Destroy(mesh);if(material)Destroy(material);}
    }
}

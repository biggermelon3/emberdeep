using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PhysicalAssembly
{
    public enum SandboxTileIcon { Play, Reset, Back, Quit }
    // Visual feedback only; DownhillPresentation anchors this tile to the build area.
    [RequireComponent(typeof(WorkshopButton))]
    public sealed class FloatingStartButton : MonoBehaviour
    {
        [SerializeField] DownhillSettings settings;
        [SerializeField] Renderer border,glow,icon;
        [SerializeField] TextMesh label;
        [SerializeField] bool styled;
        [SerializeField] string caption="START";
        [SerializeField] Color accentColor=new(.12f,.75f,1,.95f);
        [SerializeField] SandboxTileIcon tileIcon;
        [SerializeField] int iconVersion;
        readonly List<Object> ownedResources=new();
        WorkshopButton button;
        Renderer panel;
        MaterialPropertyBlock properties;
        public void Configure(DownhillSettings tuning)
        {
            settings=tuning; button=GetComponent<WorkshopButton>(); panel=GetComponent<Renderer>();
            panel.shadowCastingMode=ShadowCastingMode.Off; panel.receiveShadows=false;
            properties??=new MaterialPropertyBlock();
            var size=Size;
            transform.localScale=new Vector3(size.x,size.y,.012f);
            // Scale the entire tile, including its caption, icon, and touch collider.
            if(caption=="START" || caption=="RESET" || caption=="QUIT" || caption=="RETRY" || caption=="BACK TO\nBUILD") transform.localScale/=3f;
            name=caption.Replace('\n',' ');
            label=GetComponentInChildren<TextMesh>(true);
            if(!styled)
            {
                panel.GetComponent<MeshFilter>().sharedMesh=RoundedMesh(size,0,0);
                panel.sharedMaterial=Material(new Color(.025f,.065f,.13f,.78f));
                glow=Shape("Cyan halo",RoundedMesh(size,-.003f,.010f),Material(new Color(.08f,.6f,1,.16f)), -.53f);
                border=Shape("Cyan outline",RoundedMesh(size,.007f,.002f),Material(new Color(.12f,.75f,1,.95f)), -.56f);
                var play=new Mesh { name="Start play icon" };
                play.vertices=new[]{new Vector3(-.12f,.02f,0),new Vector3(-.12f,.29f,0),new Vector3(.17f,.155f,0)};
                play.triangles=new[]{0,1,2}; play.RecalculateNormals(); play.RecalculateBounds(); ownedResources.Add(play);
                icon=Shape("Play icon",play,Material(new Color(.3f,.85f,1,1)),-.60f);
                styled=true;
            }
            if(label)
            {
                label.text=caption; label.fontSize=64; label.characterSize=(caption.Contains("\n")?.026f:.028f)*10/64;
                label.alignment=TextAlignment.Center;
                label.transform.localPosition=new Vector3(0,-.22f,-.65f);
                label.transform.localScale=new Vector3(1/size.x,1/size.y,1/.012f);
                label.GetComponent<Renderer>().sortingOrder=4;
            }
            // Explicit layers prevent the transparent panel tinting its own text.
            if(glow) glow.sortingOrder=1;
            if(border) border.sortingOrder=2;
            if(icon) icon.sortingOrder=3;
            RefreshColors();
        }
        public void Configure(DownhillSettings tuning,string title,Color accent,SandboxTileIcon kind)
        {
            bool replace=iconVersion!=1 || tileIcon!=kind;
            caption=title; accentColor=accent; tileIcon=kind;
            Configure(tuning);
            if(replace)
            {
                var filter=icon.GetComponent<MeshFilter>(); var previous=filter.sharedMesh;
                filter.sharedMesh=ActionIcon(kind); iconVersion=1;
                if(previous && ownedResources.Remove(previous))
                { if(Application.isPlaying) Destroy(previous); else DestroyImmediate(previous); }
            }
        }
        public bool RewardPulse { get; set; }
        public void ConfigurePalmTile()
        {
            transform.localScale=new Vector3(.20f,.055f,.008f);
            if(icon) { icon.transform.localScale=new Vector3(.25f,.9f,1); icon.transform.localPosition=new Vector3(-.32f,-.117f,-.60f); }
            if(label) {
                label.text=caption.Replace('\n',' '); label.characterSize=.0018f;
                label.transform.localPosition=new Vector3(.09f,0,-.65f);
                label.transform.localScale=new Vector3(5,1/.055f,125);
            }
        }
        Mesh ActionIcon(SandboxTileIcon kind)
        {
            var vertices=new List<Vector3>(); var triangles=new List<int>();
            void Line(Vector2 a,Vector2 b,float width=.018f)
            {
                var n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f; int i=vertices.Count;
                vertices.Add(a+n); vertices.Add(b+n); vertices.Add(b-n); vertices.Add(a-n);
                triangles.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});
            }
            var center=new Vector2(0,.13f);
            for(int i=0;i<48;i++)
            {
                float a=i*Mathf.PI*2/48,b=(i+1)*Mathf.PI*2/48;
                Line(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.29f,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*.29f,.012f);
            }
            if(kind==SandboxTileIcon.Play)
            {
                int i=vertices.Count;
                vertices.Add(new Vector3(-.09f,0,0)); vertices.Add(new Vector3(-.09f,.26f,0)); vertices.Add(new Vector3(.14f,.13f,0));
                triangles.AddRange(new[]{i,i+1,i+2});
            }
            else if(kind==SandboxTileIcon.Reset)
            {
                for(int i=0;i<24;i++)
                {
                    float a=(45+i*11)*Mathf.Deg2Rad,b=(45+(i+1)*11)*Mathf.Deg2Rad;
                    Line(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.15f,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*.15f,.027f);
                }
                Line(new Vector2(.106f,.236f),new Vector2(.10f,.34f),.025f);
                Line(new Vector2(.106f,.236f),new Vector2(.21f,.25f),.025f);
            }
            else
            {
                float direction=kind==SandboxTileIcon.Back?-1:1;
                Line(new Vector2(-.14f*direction,.13f),new Vector2(.15f*direction,.13f),.026f);
                Line(new Vector2(.15f*direction,.13f),new Vector2(.045f*direction,.24f),.026f);
                Line(new Vector2(.15f*direction,.13f),new Vector2(.045f*direction,.02f),.026f);
                if(kind==SandboxTileIcon.Quit)
                {
                    Line(new Vector2(-.02f,.32f),new Vector2(-.19f,.32f));
                    Line(new Vector2(-.19f,.32f),new Vector2(-.19f,-.06f));
                    Line(new Vector2(-.19f,-.06f),new Vector2(-.02f,-.06f));
                }
            }
            var mesh=new Mesh { name="Sandbox "+kind+" icon" }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); ownedResources.Add(mesh); return mesh;
        }
        Vector2 Size => settings?new Vector2(Mathf.Max(.08f,settings.StartButtonSize.x),Mathf.Max(.1f,settings.StartButtonSize.y)):new Vector2(.22f,.24f);
        void LateUpdate() => RefreshColors();
        void RefreshColors()
        {
            if(!button || !panel) return;
            properties??=new MaterialPropertyBlock();
            bool enabled=button.Interactable;
            Color accent=!enabled?new Color(.26f,.36f,.43f,.45f):Color.Lerp(accentColor,Color.white,button.IsSelected?.55f:button.IsHovered?.3f:0);
            SetColor(panel,!enabled?new Color(.035f,.05f,.07f,.7f):button.IsHovered?new Color(.04f,.16f,.27f,.9f):new Color(.025f,.065f,.13f,.78f));
            if(enabled && RewardPulse) accent=Color.Lerp(accent,new Color(1,.78f,.08f),.5f+.5f*Mathf.Sin(Time.unscaledTime*3));
            SetColor(border,accent); SetColor(icon,accent);
            accent.a=enabled?(button.IsHovered?.3f:.16f):.06f; SetColor(glow,accent);
            if(label) label.color=enabled?new Color(.65f,.9f,1):new Color(.4f,.48f,.53f);
        }
        void SetColor(Renderer renderer,Color color)
        {
            if(!renderer) return;
            if(!Application.isPlaying) renderer.sharedMaterial.SetColor("_BaseColor",color);
            properties.Clear(); properties.SetColor("_BaseColor",color); renderer.SetPropertyBlock(properties);
        }
        Material Material(Color color)
        {
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name="Floating Start",renderQueue=3000 };
            material.SetFloat("_Surface",1); material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha); material.SetFloat("_ZWrite",0);
            material.SetFloat("_Cull",(float)CullMode.Off); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetColor("_BaseColor",color); ownedResources.Add(material); return material;
        }
        Renderer Shape(string title,Mesh mesh,Material material,float z)
        {
            var go=new GameObject(title); go.transform.SetParent(transform,false); go.transform.localPosition=new Vector3(0,0,z);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>(); renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false; return renderer;
        }
        Mesh RoundedMesh(Vector2 size,float inset,float thickness)
        {
            const int segments=8, count=4*(segments+1);
            var vertices=new Vector3[thickness>0?count*2:count+1];
            var triangles=new int[thickness>0?count*6:count*3];
            for(int ring=0;ring<(thickness>0?2:1);ring++)
            {
                float edge=inset+ring*thickness;
                float radius=Mathf.Max(.002f,.018f-edge);
                var half=size*.5f-Vector2.one*edge;
                for(int corner=0;corner<4;corner++) for(int j=0;j<=segments;j++)
                {
                    float angle=(corner*90f+j*90f/segments)*Mathf.Deg2Rad;
                    var center=new Vector2((corner==0||corner==3?1:-1)*(half.x-radius),(corner<2?1:-1)*(half.y-radius));
                    var point=center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
                    vertices[ring*count+corner*(segments+1)+j]=new Vector3(point.x/size.x,point.y/size.y,thickness>0?0:-.5f);
                }
            }
            if(thickness<=0) vertices[count]=new Vector3(0,0,-.5f);
            for(int i=0;i<count;i++)
            {
                int next=(i+1)%count;
                if(thickness<=0) { triangles[i*3]=count; triangles[i*3+1]=next; triangles[i*3+2]=i; }
                else
                {
                    int t=i*6;
                    triangles[t]=i; triangles[t+1]=next; triangles[t+2]=count+i;
                    triangles[t+3]=next; triangles[t+4]=count+next; triangles[t+5]=count+i;
                }
            }
            var mesh=new Mesh { name=thickness>0?"Start rounded outline":"Start rounded panel",vertices=vertices,triangles=triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); ownedResources.Add(mesh); return mesh;
        }
        // Authoring transfers procedural meshes/materials into persisted scene resources.
        public void ReleaseGeneratedResourceOwnership() => ownedResources.Clear();
        void OnDestroy()
        {
            foreach(var resource in ownedResources) if(resource)
            { if(Application.isPlaying) Destroy(resource); else DestroyImmediate(resource); }
            ownedResources.Clear();
        }
    }
}

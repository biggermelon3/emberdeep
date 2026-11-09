using System;
using System.Collections.Generic;
using CloudGarden.ShadowPuzzle;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShapeOfMemory
{
    // What gets printed on the book's pages: text, rules and ornaments, picture plates and the button tiles. Places are
    // in page units (u from the reader's left, v up, metres) on a page's content root (MemoryBook.Fill). Shares one
    // material per texture and one picture per level, so pages can be rebuilt freely.
    public sealed class MemoryPages
    {
        public static readonly Color Ink=new Color(.17f,.11f,.09f),Faded=new Color(.42f,.35f,.30f),Pale=new Color(.90f,.84f,.72f),Gilt=new Color(.80f,.58f,.26f);
        // In front of the lamps' walls and their shadows (Torch2Room draws them up to 3 mm off the page).
        public const float Front=-.006f;
        readonly MemoryKit kit;
        readonly Dictionary<(Material,Texture),Material> materials=new Dictionary<(Material,Texture),Material>();
        readonly Dictionary<string,Texture2D> pictures=new Dictionary<string,Texture2D>();
        public MemoryPages(MemoryKit kit){this.kit=kit;}

        public static Vector3 At(float u,float v,float z=Front)=>new Vector3(u-MemoryBook.Width*.5f,v-MemoryBook.Height*.5f,z);
        public static float Middle=>MemoryBook.Width*.5f;
        public TMP_Text Text(Transform face,string text,float u,float v,float size,float width,float height,Color color,FontStyles style=FontStyles.Normal,float spacing=0,TextAlignmentOptions align=TextAlignmentOptions.Center)
        {
            var label=MemoryUi.Label(text,face,At(u,v),Quaternion.identity,size,new Vector2(width,height),color,align);
            label.fontStyle=style;label.characterSpacing=spacing;label.enableAutoSizing=true;label.fontSizeMin=size*4;label.fontSizeMax=size*10;return label;
        }
        public Renderer Quad(Transform face,string name,float u,float v,Vector2 size,Material material,Texture texture,Color color,float z=Front,float turn=0)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Quad);go.name=name;MemoryUi.Kill(go.GetComponent<Collider>());go.transform.SetParent(face,false);
            go.transform.localPosition=At(u,v,z);go.transform.localRotation=Quaternion.Euler(0,0,turn);go.transform.localScale=new Vector3(size.x,size.y,1);
            var r=go.GetComponent<Renderer>();r.sharedMaterial=Material(material,texture);r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;MemoryUi.Tint(r,color);return r;
        }
        Material Material(Material material,Texture texture)
        {
            if(materials.TryGetValue((material,texture),out var m)&&m)return m;
            m=new Material(material){name=material.name+" "+(texture?texture.name:"plain")};if(texture)m.SetTexture("_BaseMap",texture);m.SetColor("_BaseColor",Color.white);
            kit.Owned.Add(m);return materials[(material,texture)]=m;
        }
        // A gilt rule with a diamond in its middle.
        public void Rule(Transform face,float u,float v,float width,Color color)
        {
            Quad(face,"Rule",u,v,new Vector2(width,.004f),kit.Screen,null,color);
            Quad(face,"Diamond",u,v,Vector2.one*.022f,kit.Screen,null,color,Front-.0005f,45);
        }
        // A framed picture plate: a thin dark frame, the picture or a mark inside it.
        public void Plate(Transform face,float u,float v,float size,Texture picture,string mark,Color markColor)
        {
            Quad(face,"Plate frame",u,v,Vector2.one*(size+.016f),kit.Screen,null,Ink,Front+.0006f);
            Quad(face,"Plate",u,v,Vector2.one*size,kit.Screen,picture,picture?Color.white:new Color(.93f,.88f,.79f));
            if(!picture&&mark!=null)Text(face,mark,u,v,size*.45f,size,size,markColor,FontStyles.Bold);
        }
        public void Icon(Transform face,float u,float v,float size,Texture2D icon,Color color){if(icon)Quad(face,"Icon",u,v,Vector2.one*size,kit.Icon,icon,color);}
        // The level's found picture (both walls side by side for two), ink on paper, kept for the session.
        public Texture2D Picture(Torch2Level level)
        {
            if(pictures.TryGetValue(level.name,out var cached)&&cached)return cached;
            const int G=Torch2Projection.Grid;var projection=new Torch2Projection(level,false);int walls=projection.walls.Length;
            var texture=new Texture2D(G,G,TextureFormat.RGBA32,false){name="Memory "+level.name,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};kit.Owned.Add(texture);
            var pixels=new Color32[G*G];Color32 paper=new Color(.93f,.88f,.79f),ink=Color.Lerp(level.accent,Ink,.6f);
            for(int y=0;y<G;y++)for(int x=0;x<G;x++)
            {
                bool on;
                if(walls==1)on=projection.target[0][y*G+x];
                else{int half=G/2,top=(G-half)/2,slot=x<half?0:1;int sx=(x%half)*2,sy=(y-top)*2;on=y>=top&&y<top+half&&projection.target[slot][sy*G+sx];}
                pixels[y*G+x]=on?ink:paper;
            }
            texture.SetPixels32(pixels);texture.Apply(false,true);return pictures[level.name]=texture;
        }
        // One wall's picture only (two-wall levels print each on its own page).
        public Texture2D Picture(Torch2Level level,int slot)
        {
            string key=level.name+"/"+slot;if(pictures.TryGetValue(key,out var cached)&&cached)return cached;
            const int G=Torch2Projection.Grid;var projection=new Torch2Projection(level,false);
            var texture=new Texture2D(G,G,TextureFormat.RGBA32,false){name="Memory "+key,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};kit.Owned.Add(texture);
            var pixels=new Color32[G*G];Color32 paper=new Color(.93f,.88f,.79f),ink=Color.Lerp(level.accent,Ink,.6f);
            for(int i=0;i<pixels.Length;i++)pixels[i]=projection.target[slot][i]?ink:paper;
            texture.SetPixels32(pixels);texture.Apply(false,true);return pictures[key]=texture;
        }
        // The title, set like a book's title page: SHAPE, a small italic "of", MEMORY, a gilt rule, the progress line.
        public void TitlePage(Transform face,string progress)
        {
            float m=Middle;
            Text(face,"A  BOOK  OF  SHADOWS",m,.90f,.024f,.8f,.05f,Faded,FontStyles.Normal,14);
            foreach(var (word,v) in new[]{("SHAPE",.70f),("MEMORY",.44f)})
            {
                // A gilt shadow a little down and right, as if stamped into the paper; drawn before the ink whatever the
                // view (on a flat page it can be the nearer).
                var stamp=Text(face,word,m+.005f,v-.005f,.135f,1f,.17f,Gilt,FontStyles.Bold,10);stamp.transform.localPosition+=new Vector3(0,0,.0008f);stamp.GetComponent<Renderer>().sortingOrder=-1;
                Text(face,word,m,v,.135f,1f,.17f,Ink,FontStyles.Bold,10);
            }
            Text(face,"of",m,.575f,.055f,.3f,.08f,Gilt,FontStyles.Italic);
            Rule(face,m,.31f,.46f,Gilt);
            Text(face,progress,m,.20f,.03f,.8f,.06f,Faded,FontStyles.Italic);
        }
        public static string Roman(int n)=>n switch{1=>"I",2=>"II",3=>"III",4=>"IV",5=>"V",6=>"VI",7=>"VII",8=>"VIII",9=>"IX",10=>"X",_=>n.ToString()};
    }

    // A button printed as a raised tile on a page: a leather square with a gilt icon, its name beneath. It rises toward
    // the hand on hover and sinks into the page when pressed. On a page that is turning or about to be uncovered it is
    // built without Press and pressed nearly flat (Flat), so the turning page can pass over it; a live one pops up out of
    // the page (PopSeconds) when its page lands.
    public sealed class MemoryTile : MonoBehaviour
    {
        public const float Depth=.016f,Flat=.15f,PopSeconds=.25f;
        public static readonly Color Leather=new Color(.36f,.15f,.13f),Lit=new Color(.52f,.24f,.19f);
        public Pressable Press{get;private set;}
        public string Caption{get;private set;}
        Transform body;
        Renderer skin,icon;
        float rise,pop;

        public static MemoryTile Create(MemoryPages pages,Transform face,string name,string caption,Texture2D iconTexture,float u,float v,float size,MemoryKit kit,
            Action action,Func<bool> enabled,Func<bool> handsAllowed,bool live)
        {
            var root=new GameObject("Tile "+name).transform;root.SetParent(face,false);root.localPosition=MemoryPages.At(u,v,0);
            var tile=root.gameObject.AddComponent<MemoryTile>();tile.Caption=caption;
            tile.body=new GameObject("Body").transform;tile.body.SetParent(root,false);
            tile.skin=MemoryUi.Shape(PrimitiveType.Cube,"Leather",tile.body,new Vector3(0,0,-Depth*.5f-.002f),Quaternion.identity,new Vector3(size,size,Depth),kit.Surface,Leather).GetComponent<Renderer>();
            if(iconTexture)tile.icon=pages.Quad(tile.body,"Icon",MemoryPages.Middle,MemoryBook.Height*.5f,Vector2.one*size*.55f,kit.Icon,iconTexture,MemoryUi.Gold,-Depth-.0035f);
            pages.Text(face,caption,u,v-size*.5f-.045f,.032f,size+.16f,.06f,MemoryPages.Ink,FontStyles.Bold,6);
            tile.body.localScale=new Vector3(1,1,Flat);
            if(live)
            {
                var hit=new GameObject("Press",typeof(BoxCollider));hit.transform.SetParent(root,false);hit.transform.localPosition=new Vector3(0,0,-Depth*.5f-.002f);hit.transform.localScale=new Vector3(size,size,Depth);
                tile.Press=Pressable.Add(hit,action,enabled,handsAllowed,new Vector3(0,0,-.51f),Quaternion.identity);
            }
            return tile;
        }
        void Update()
        {
            if(Press==null)return;
            bool available=Press.Available,down=available&&Press.Down,hover=available&&Press.Hover;
            if(pop<1){pop=Mathf.Min(1,pop+Time.unscaledDeltaTime/PopSeconds);float eased=1-(1-pop)*(1-pop)*(1-pop);body.localScale=new Vector3(1,1,Mathf.LerpUnclamped(Flat,1,eased));}
            rise=Mathf.MoveTowards(rise,down?-.008f:hover?.006f:0,Time.unscaledDeltaTime*.1f);body.localPosition=new Vector3(0,0,-rise);
            MemoryUi.Tint(skin,!available?MemoryUi.Dim:down?Color.Lerp(Lit,MemoryUi.Gold,.35f):hover?Lit:Leather);
            if(icon)MemoryUi.Tint(icon,available?MemoryUi.Gold:new Color(.5f,.45f,.38f));
        }
    }
}

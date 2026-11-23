using System.Collections.Generic;
using PhysicalAssembly;
using UnityEngine;
using UnityEngine.Rendering;

namespace FireRescue
{
    // LoopSpec §7 upgrades page (headset test 2 rework, 2026-11-22). Four tiles, each with a picture of the real thing (the
    // gem, the pump, the tray with its towers, a tower with its rain circle), one word, the CURRENT value with its unit, and
    // the price (coin + number, MAX at the top level). Pointing at a tile turns its number green with an up arrow and the value
    // after buying, shows the change in the picture (a ghost tower in the next tray slot, the wider rain circle), and the line
    // under the tiles says what the upgrade does.
    //   GEM    gem capacity             "60" WATER          -> 75
    //   PUMP   water per pump strike    "+5" PER STRIKE     -> +6
    //   SLOT   towers added on every level that hands out pieces   "0" EXTRA TOWERS -> 1
    //   TOWER  rain radius on the board "19 cm" RAIN REACH  -> 22 cm
    // Every tile is ray-targetable, so an unaffordable one still explains itself; its price and coin dim and a press buys
    // nothing (UpgradeBuyable is the buy rule).
    // Headset test 3 (2026-11-22): the slowly turning live models felt jarring and stood over the words (they stood .1 in front
    // of the pill and their tipped, turning bounds swept past their box). Each model is now rendered ONCE to a picture
    // (Pictures below) and shown flat in the tile's top band, PictureY, clear of the word, number, unit and price.
    public sealed partial class FireRescueGame
    {
        sealed class UpgradeTile
        {
            public WorkshopButton Button;public TextMesh Word,Number,Unit,Price;public Renderer Coin,Lock,Arrow,Picture;public Transform Root;
            public bool Buyable,Max;public string Now,Next,Explain;public int Index;
            // The rendered pictures (owned by the game, released in ReleaseUpgradePictures) and what they show.
            public RenderTexture Idle,Gain;public string Shows;public float Grow=1;
        }
        // Panel units. A label's cap height is ~1.45x its size: word .058, number .087, unit .041, price .061, so the rows
        // below keep ~.02 between them (the first layout let the number run into the word and the unit).
        public const float TileWidth=.44f,TileHeight=.74f,TileY=.12f,UpgradesCoinsY=.56f,UpgradesExplainY=-.4f,UpgradesBackY=-.55f,UpgradesBoardHeight=1.36f;
        public const float WordY=.055f,NumberY=-.035f,NumberSize=.06f,UnitY=-.115f,UnitSize=.028f,PriceY=-.2f;
        // The picture band over the word (panel units, tile-local): .12 .. .335. The word's glyph box tops out at ~.1, the tile at .37.
        public const float PictureY=.2275f,PictureWidth=.4f,PictureHeight=.215f,PictureHover=1.05f;
        // The model's size in the picture rig (panel units, as the live model stood on the panel), so art-kit materials keep the
        // texel density and lighting they had live.
        public const float ModelHeight=.17f,ModelWidth=.32f;
        static readonly Color UiGain=new(.45f,1,.5f);
        static readonly string[] UpgradeWords={"GEM","PUMP","SLOT","TOWER"};
        static readonly string[] UpgradeUnits={"WATER","PER STRIKE","EXTRA TOWERS","RAIN REACH"};
        static readonly string[] UpgradeExplains={"A bigger gem sprays longer","Each pump strike puts more water in the gem",
            "One more tower in the tray on every level with pieces","Towers rain over a wider circle"};
        public const string UpgradesPrompt="Point at an upgrade to see what it does";
        readonly UpgradeTile[] tiles=new UpgradeTile[4];
        TextMesh upgradeCoins,upgradeExplain;Material uiClay;int hoveredTile=-1;
        public int HoveredUpgrade=>hoveredTile;
        public string UpgradeExplainText=>upgradeExplain?upgradeExplain.text:"";
        public string UpgradeNumberText(int i)=>i>=0 && i<4 && tiles[i]!=null?tiles[i].Number.text:"";
        public bool UpgradeShowsGain(int i)=>i>=0 && i<4 && tiles[i]!=null && tiles[i].Arrow.gameObject.activeSelf;
        // What a press would do: buy. Read live from the run, so it also holds while the page is closed (the results panel's
        // star, FireRescueGame.Screens.cs). The tiles stay targetable for hover either way.
        public bool UpgradeBuyable(int i)=>i switch{0=>Run.CanUpgradeMagic,1=>Run.CanUpgradePump,2=>Run.CanUpgradeSlot,3=>Run.CanUpgradeTower,_=>false};
        public bool AnyUpgradeBuyable=>UpgradeBuyable(0) || UpgradeBuyable(1) || UpgradeBuyable(2) || UpgradeBuyable(3);
        // Tests and desktop captures: pretend the ray hovers tile i (-1: none) until cleared.
        public int ForcedUpgradeHover {get;set;}=-1;

        void BuildUpgrades()
        {
            var tutorial=Resources.Load<Shader>("EmberdeepTutorial");
            uiClay=tutorial?new Material(tutorial){name="Upgrade models",renderQueue=ScreenQueue}:new Material(uiIcon){name="Upgrade models"};
            var picture=Resources.Load<Shader>("EmberdeepUiPicture");
            pictureMaterial=picture?new Material(picture){name="Upgrade pictures",renderQueue=PictureQueue}:new Material(uiIcon){name="Upgrade pictures",renderQueue=PictureQueue};
            Board(upgradesRoot,"Upgrades board",new Vector2(2.1f,UpgradesBoardHeight));
            UiIconAt(upgradesRoot,EmberdeepUiIcons.Coin,new Vector3(-.14f,UpgradesCoinsY,-.04f),.09f,UiGold);
            upgradeCoins=Word(upgradesRoot,"",new Vector3(.08f,UpgradesCoinsY,-.04f),.06f,.4f);
            System.Action[] buys={()=>UpgradeGem(),()=>UpgradePump(),()=>UpgradeSlot(),()=>UpgradeTower()};
            for(int i=0;i<4;i++)tiles[i]=BuildTile(upgradesRoot,i,new Vector3(-.72f+i*.48f,TileY,0),buys[i]);
            upgradeExplain=Word(upgradesRoot,UpgradesPrompt,new Vector3(0,UpgradesExplainY,-.04f),.036f,1.9f);
            // Text pass: a wide pill carries its word; a small one's caption would hang off the board's bottom edge.
            UiButton("Upgrades back",upgradesRoot,new Vector3(0,UpgradesBackY,0),new Vector2(.6f,.14f),EmberdeepUiIcons.Back,"BACK",CloseShop);
        }
        UpgradeTile BuildTile(Transform parent,int index,Vector3 at,System.Action buy)
        {
            string word=UpgradeWords[index];
            var tile=new UpgradeTile{Index=index,Root=new GameObject("Upgrade tile "+word).transform,Explain=UpgradeExplains[index]};
            tile.Root.SetParent(parent,false);tile.Root.localPosition=at;
            var go=Shape("Upgrade "+word.ToLowerInvariant(),PrimitiveType.Cube,tile.Root,Vector3.zero,new Vector3(TileWidth,TileHeight,.05f),PanelMaterial,true);
            EmberdeepForestUi.Apply(go,true);
            tile.Button=go.AddComponent<WorkshopButton>();tile.Button.Command=go.name;tile.Button.Pressed=buy;buttons.Add(tile.Button);
            // The picture lies flat on the pill's face, under the labels' plane (-.04); hidden until it is rendered.
            var quad=new GameObject("Upgrade picture");quad.transform.SetParent(tile.Root,false);
            quad.transform.localPosition=new Vector3(0,PictureY,-.035f);quad.transform.localScale=new Vector3(PictureWidth,PictureHeight,1);
            quad.AddComponent<MeshFilter>().sharedMesh=PictureQuad;tile.Picture=quad.AddComponent<MeshRenderer>();tile.Picture.sharedMaterial=pictureMaterial;
            tile.Picture.shadowCastingMode=ShadowCastingMode.Off;tile.Picture.receiveShadows=false;tile.Picture.enabled=false;
            tile.Word=Word(tile.Root,word,new Vector3(0,WordY,-.04f),.04f,.38f);
            tile.Number=Word(tile.Root,"",new Vector3(0,NumberY,-.04f),NumberSize,.3f);
            tile.Arrow=UiIconAt(tile.Root,EmberdeepUiIcons.Up,new Vector3(-.15f,NumberY,-.04f),.055f,UiGain);tile.Arrow.gameObject.SetActive(false);
            tile.Unit=Word(tile.Root,UpgradeUnits[index],new Vector3(0,UnitY,-.04f),UnitSize,.4f);
            tile.Lock=UiIconAt(tile.Root,EmberdeepUiIcons.Lock,new Vector3(0,NumberY,-.04f),.1f,UiDim);
            tile.Coin=UiIconAt(tile.Root,EmberdeepUiIcons.Coin,new Vector3(-.1f,PriceY,-.04f),.055f,UiGold);
            tile.Price=Word(tile.Root,"",new Vector3(.045f,PriceY,-.04f),.042f,.24f);
            return tile;
        }

        // ---- Pictures (headset test 3, 2026-11-22) --------------------------------------------------------------------
        // Each model is built in a rig far under the panel (PictureRigDrop) with the panel's own world rotation, scale and
        // stage tilt, so its clay (fixed world light), gem and pump art are lit and seen as they were on the live tile, and
        // rendered by an orthographic camera that sees only the rig (PictureLayer) onto a transparent background (premultiplied:
        // EmberdeepUiPicture blends One / OneMinusSrcAlpha). It renders at twice the size with 2x MSAA and is box-filtered down
        // into a mipmapped texture, so the picture stays clean at panel distance. Then the rig is destroyed.
        // SLOT and TOWER also render their pointed-at state (the next tower as a green ghost; the wider green rain circle) with
        // the same framing, and the picture swaps to it on hover; GEM and PUMP grow PictureHover instead.
        // A picture depends only on what it shows (Shows: the towers in the tray, the rain radius), so it renders on the page's
        // first open and again only after a buy changes that; one tile per frame, so opening never renders six cameras at once.
        // The textures live with the game and are released in ReleaseUpgradePictures (DestroyScreenMaterials).
        public const int PictureLayer=31,PictureWidthPixels=320,PictureHeightPixels=172,PictureQueue=ScreenQueue+10;
        public const float PictureRigDrop=200,PictureMargin=1.06f; // m, framing margin
        // Stage tilt (deg, toward the eye) and turn: the live models' tilts, except the tower, tipped further (-14 -> -30) so its
        // rain circle reads as a circle, not a sliver; the turns are fixed 3/4 views (the tray square on, so its slots count).
        static readonly float[] PictureTilt={72,-14,-40,-30},PictureSpin={0,40,0,30};
        Material pictureMaterial;MaterialPropertyBlock pictureBlock;Mesh pictureQuad;
        public int UpgradePicturesRendered {get;private set;}
        Mesh PictureQuad
        {
            get
            {
                if(pictureQuad)return pictureQuad;
                pictureQuad=new Mesh{name="Upgrade picture quad"};
                pictureQuad.SetVertices(new[]{new Vector3(-.5f,-.5f),new Vector3(-.5f,.5f),new Vector3(.5f,.5f),new Vector3(.5f,-.5f)});
                pictureQuad.SetUVs(0,new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(1,1),new Vector2(1,0)});
                pictureQuad.SetTriangles(new[]{0,1,2,0,2,3},0);pictureQuad.RecalculateNormals();pictureQuad.RecalculateBounds();return pictureQuad;
            }
        }
        // Tests: the texture tile i shows now, and whether it is the pointed-at one.
        public Texture UpgradePicture(int i)=>i>=0 && i<4 && tiles[i]!=null && tiles[i].Picture.enabled?(ShowsGainPicture(tiles[i])?tiles[i].Gain:tiles[i].Idle):null;
        public bool UpgradePictureShowsGain(int i)=>i>=0 && i<4 && tiles[i]!=null && ShowsGainPicture(tiles[i]);
        public Texture UpgradeIdlePicture(int i)=>i>=0 && i<4 && tiles[i]!=null?tiles[i].Idle:null;
        public Texture UpgradeGainPicture(int i)=>i>=0 && i<4 && tiles[i]!=null?tiles[i].Gain:null;
        bool ShowsGainPicture(UpgradeTile tile)=>hoveredTile==tile.Index && tile.Gain && tile.Gain.IsCreated();
        int TrayTowers=>Mathf.Min(4,2+Run.SlotLevel);
        // What tile i's picture shows, and whether it has a pointed-at state.
        string PictureState(UpgradeTile tile,out bool gain)
        {
            switch(tile.Index)
            {
                case 2:gain=!tile.Max && TrayTowers<4;return $"slot {TrayTowers} {gain}";
                case 3:gain=!tile.Max;return $"tower {Run.TowerRadius:F3} {gain}";
                default:gain=false;return tile.Index==0?"gem":"pump";
            }
        }
        void RefreshUpgradePictures()
        {
            pictureBlock??=new MaterialPropertyBlock();
            bool rendered=false;float dt=Mathf.Min(Time.unscaledDeltaTime,.1f);
            foreach(var tile in tiles)
            {
                string shows=PictureState(tile,out bool gain);
                bool lost=tile.Idle && !tile.Idle.IsCreated() || tile.Gain && !tile.Gain.IsCreated(); // a lost GPU context
                if(!rendered && (tile.Shows!=shows || lost)){RenderUpgradePictures(tile,gain);tile.Shows=shows;rendered=true;}
                var shown=ShowsGainPicture(tile)?tile.Gain:tile.Idle;
                bool on=shown && shown.IsCreated();
                if(tile.Picture.enabled!=on)tile.Picture.enabled=on;
                if(!on)continue;
                pictureBlock.Clear();pictureBlock.SetTexture("_BaseMap",shown);pictureBlock.SetColor("_BaseColor",Color.white);tile.Picture.SetPropertyBlock(pictureBlock);
                // GEM and PUMP have no pointed-at picture: they grow a little instead (the old live model grew 1.1).
                bool grow=hoveredTile==tile.Index && !tile.Gain;
                tile.Grow=Mathf.MoveTowards(tile.Grow,grow?PictureHover:1,dt*.5f);
                tile.Picture.transform.localScale=new Vector3(PictureWidth,PictureHeight,1)*tile.Grow;
            }
        }
        void RenderUpgradePictures(UpgradeTile tile,bool gain)
        {
            ReleasePicture(ref tile.Idle);ReleasePicture(ref tile.Gain);
            if(!upgradesRoot)return;
            var made=new List<Object>();var gainParts=new List<Renderer>();var unfitted=new List<Renderer>();
            var rig=new GameObject("Upgrade picture rig").transform;
            rig.SetPositionAndRotation(upgradesRoot.position+Vector3.down*PictureRigDrop,upgradesRoot.rotation);rig.localScale=upgradesRoot.lossyScale;
            var stage=new GameObject("Upgrade model stage").transform;stage.SetParent(rig,false);
            stage.localRotation=Quaternion.Euler(PictureTilt[tile.Index],0,0)*Quaternion.Euler(0,PictureSpin[tile.Index],0);
            var model=new GameObject("Upgrade model").transform;model.SetParent(stage,false);
            var camera=new GameObject("Upgrade picture camera").AddComponent<Camera>();
            try
            {
                BuildUpgradeModel(tile.Index,model,gain,gainParts,unfitted,made);
                FitModel(model,tile.Index,unfitted);
                foreach(var t in rig.GetComponentsInChildren<Transform>(true))t.gameObject.layer=PictureLayer;
                FramePicture(camera,rig);
                if(gain)tile.Gain=ShootPicture(camera,tile.Index,"next");
                foreach(var r in gainParts)if(r)r.gameObject.SetActive(false);
                tile.Idle=ShootPicture(camera,tile.Index,"now");
            }
            finally
            {
                rig.gameObject.SetActive(false);Destroy(rig.gameObject);camera.enabled=false;Destroy(camera.gameObject);
                foreach(var o in made)if(o)Destroy(o);
            }
        }
        // Orthographic, along the panel's facing (the eye's view of the tile), fitted to every renderer in the rig.
        void FramePicture(Camera camera,Transform rig)
        {
            camera.enabled=false;camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
            camera.cullingMask=1<<PictureLayer;camera.allowHDR=false;camera.allowMSAA=true;camera.useOcclusionCulling=false;
            // No stereoTargetEye (built-in renderer only; URP warns): URP renders a camera with a target texture as a plain,
            // non-XR camera.
            camera.aspect=PictureWidthPixels/(float)PictureHeightPixels;
            var inverse=Quaternion.Inverse(rig.rotation);bool any=false;var b=new Bounds();
            foreach(var r in rig.GetComponentsInChildren<Renderer>())
            {
                if(!r.enabled)continue; // the pump's gauge
                var f=r.GetComponent<MeshFilter>();var local=f && f.sharedMesh?f.sharedMesh.bounds:new Bounds(r.transform.InverseTransformPoint(r.bounds.center),Vector3.zero);
                for(int i=0;i<8;i++)
                {
                    var p=inverse*(r.transform.TransformPoint(local.center+Vector3.Scale(local.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)))-rig.position);
                    if(!any){b=new Bounds(p,Vector3.zero);any=true;}else b.Encapsulate(p);
                }
            }
            if(!any)b=new Bounds(Vector3.zero,Vector3.one*.1f);
            float back=b.size.z+.5f;
            camera.transform.SetPositionAndRotation(rig.position+rig.rotation*new Vector3(b.center.x,b.center.y,b.min.z-.25f),rig.rotation);
            camera.orthographicSize=Mathf.Max(b.extents.y,b.extents.x/camera.aspect,.001f)*PictureMargin;
            camera.nearClipPlane=.01f;camera.farClipPlane=back;
        }
        RenderTexture ShootPicture(Camera camera,int index,string state)
        {
            var picture=new RenderTexture(PictureWidthPixels,PictureHeightPixels,0,RenderTextureFormat.ARGB32)
                {name=$"Upgrade picture {UpgradeWords[index]} {state}",useMipMap=true,autoGenerateMips=true,filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Clamp};
            picture.Create();
            var big=RenderTexture.GetTemporary(PictureWidthPixels*2,PictureHeightPixels*2,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,2);
            var active=RenderTexture.active;
            try{camera.targetTexture=big;camera.Render();Graphics.Blit(big,picture);}
            finally{camera.targetTexture=null;RenderTexture.active=active;RenderTexture.ReleaseTemporary(big);}
            UpgradePicturesRendered++;return picture;
        }
        static void ReleasePicture(ref RenderTexture picture){if(picture){picture.Release();Destroy(picture);}picture=null;}
        void ReleaseUpgradePictures()
        {
            foreach(var tile in tiles)if(tile!=null){ReleasePicture(ref tile.Idle);ReleasePicture(ref tile.Gain);tile.Shows=null;}
            if(pictureMaterial)Destroy(pictureMaterial);if(pictureQuad)Destroy(pictureQuad);
        }

        // ---- Models (Unity primitives and the art kit, like the real board pieces) --------------------------------
        // gainParts: shown only in the pointed-at picture. unfitted: left out of the fit (the rain circles, sized to the rain
        // radius after). made: meshes to destroy with the rig.
        void BuildUpgradeModel(int index,Transform m,bool gain,List<Renderer> gainParts,List<Renderer> unfitted,List<Object> made)
        {
            switch(index)
            {
                case 0:
                {
                    // The gem from the back of the water hand (EmberdeepManaCrystal), full, its face toward the eye.
                    var gem=new GameObject("Upgrade gem crystal").AddComponent<EmberdeepManaCrystal>();gem.transform.SetParent(m,false);
                    gem.SetState(1,0,false,0); // the art kit's stone faces -y (the palm side is its leather patch): the stage tips -y to the eye
                    break;
                }
                case 1:
                {
                    // The hammer pump (art kit WellPump, as EmberdeepHammerPumpView), with its cream cap.
                    var art=EmberdeepArtLibrary.Load();var pump=art?art.Create("WellPump",m,new Vector3(.13f,0,-.065f)):null;
                    if(pump){foreach(var r in pump.GetComponentsInChildren<Renderer>(true)){r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;if(r.name.StartsWith("Gauge"))r.enabled=false;}}
                    else
                    {
                        ModelPart(m,PrimitiveType.Cylinder,new Vector3(0,.012f,0),new Vector3(.17f,.012f,.17f),new Color(.43f,.5f,.46f));
                        ModelPart(m,PrimitiveType.Cylinder,new Vector3(0,.11f,0),new Vector3(.09f,.095f,.09f),new Color(.13f,.43f,.39f));
                    }
                    ModelPart(m,PrimitiveType.Cylinder,new Vector3(0,.226f,0),new Vector3(.014f,.03f,.014f),new Color(.18f,.2f,.2f));
                    ModelPart(m,PrimitiveType.Sphere,new Vector3(0,EmberdeepHammerPumpView.CapTopHeight-.014f,0),new Vector3(.08f,.028f,.08f),new Color(.95f,.92f,.82f));
                    ModelPart(m,PrimitiveType.Cylinder,new Vector3(0,EmberdeepHammerPumpView.CapTopHeight-.019f,0),new Vector3(.082f,.004f,.082f),new Color(.98f,.74f,.3f));
                    break;
                }
                case 2:
                {
                    // The tray (EmberdeepPieceView's wood and slots): base towers (2 on the piece levels) plus the bought ones;
                    // the next one is a green ghost in the pointed-at picture.
                    var wood=new Color(.5f,.32f,.17f);int have=TrayTowers;
                    ModelPart(m,PrimitiveType.Cube,new Vector3(0,-.02f,0),new Vector3(3.2f,.12f,1.1f),wood);
                    for(int s=0;s<4;s++)ModelPart(m,PrimitiveType.Cylinder,new Vector3(-1.2f+s*.8f,.045f,0),new Vector3(.62f,.012f,.62f),new Color(.3f,.19f,.1f));
                    for(int s=0;s<have;s++){var t=ModelTower(m,new Vector3(-1.2f+s*.8f,.05f,0),.55f,new Color(.12f,.45f,.74f));t.name="Tray tower "+s;}
                    // The ghost's slot is framed in both pictures (a full tray's width), so the swap never jumps.
                    var ghost=ModelTower(m,new Vector3(-1.2f+Mathf.Min(have,3)*.8f,.05f,0),.55f,UiGain);ghost.name="Tray tower next";
                    foreach(var r in ghost.GetComponentsInChildren<Renderer>()){ModelTint(r,UiGain);gainParts.Add(r);}
                    break;
                }
                default:
                {
                    // A tower with the circle it rains over: radius ~ the rain radius (a third of it in tower units, so the top
                    // level still fits), and the next level's in green in the pointed-at picture.
                    ModelTower(m,Vector3.zero,1,new Color(.12f,.45f,.74f));
                    float now=Run.TowerRadius*.33f,next=(Run.TowerRadius+EmberdeepPieceTuning.TowerRadiusPerLevel)*.33f;
                    var ring=FlatRing(m,"Rain circle",new Color(.6f,.85f,1),made);ring.transform.localScale=Vector3.one*now*2;unfitted.Add(ring);
                    var wider=FlatRing(m,"Rain circle next",UiGain,made);wider.transform.localScale=Vector3.one*next*2;unfitted.Add(wider);gainParts.Add(wider);
                    break;
                }
            }
        }
        Transform ModelTower(Transform parent,Vector3 at,float scale,Color tank)
        {
            var root=new GameObject("Model tower").transform;root.SetParent(parent,false);root.localPosition=at;root.localScale=Vector3.one*scale;
            var steel=new Color(.62f,.66f,.66f);
            for(int i=0;i<4;i++){float a=i*Mathf.PI*.5f+Mathf.PI*.25f;ModelPart(root,PrimitiveType.Cube,new Vector3(Mathf.Cos(a)*.22f,.56f,Mathf.Sin(a)*.22f),new Vector3(.07f,1.12f,.07f),steel);}
            ModelPart(root,PrimitiveType.Cylinder,new Vector3(0,1.32f,0),new Vector3(.7f,.3f,.7f),tank);
            ModelPart(root,PrimitiveType.Cylinder,new Vector3(0,1.66f,0),new Vector3(.76f,.05f,.76f),new Color(.92f,.95f,.95f));
            ModelPart(root,PrimitiveType.Cube,new Vector3(0,1.75f,0),new Vector3(.82f,.05f,.08f),new Color(.98f,.78f,.32f));
            return root;
        }
        Renderer ModelPart(Transform parent,PrimitiveType type,Vector3 at,Vector3 scale,Color color)
        {
            var go=GameObject.CreatePrimitive(type);go.name="Upgrade model part";go.transform.SetParent(parent,false);go.transform.localPosition=at;go.transform.localScale=scale;
            var c=go.GetComponent<Collider>();c.enabled=false;Destroy(c);
            var r=go.GetComponent<Renderer>();r.sharedMaterial=uiClay;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            ModelTint(r,color);return r;
        }
        void ModelTint(Renderer r,Color c){if(!r)return;uiTint.Clear();uiTint.SetColor("_BaseColor",c);r.SetPropertyBlock(uiTint);}
        Renderer FlatRing(Transform parent,string name,Color color,List<Object> made)
        {
            var mesh=new Mesh{name=name};EmberdeepUiIcons.Arc(mesh,.5f,.43f,1,48);made.Add(mesh);
            var r=EmberdeepUiIcons.Icon(name,parent,mesh,uiIcon,new Vector3(0,.02f,0),1);r.transform.localRotation=Quaternion.Euler(90,0,0);UiTint(r,color);return r;
        }
        // Scales the model to its stage (ModelWidth x ModelHeight panel units) from its renderers' bounds, base on the stage.
        // The tower's rain circles are left out: they are sized to the rain radius.
        static void FitModel(Transform m,int index,List<Renderer> unfitted)
        {
            bool any=false;var b=new Bounds();
            foreach(var r in m.GetComponentsInChildren<Renderer>(true))
            {
                if(unfitted.Contains(r) || !r.enabled)continue;
                var f=r.GetComponent<MeshFilter>();if(!f || !f.sharedMesh)continue;var local=f.sharedMesh.bounds;
                for(int i=0;i<8;i++)
                {
                    var p=m.InverseTransformPoint(r.transform.TransformPoint(local.center+Vector3.Scale(local.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
                    if(!any){b=new Bounds(p,Vector3.zero);any=true;}else b.Encapsulate(p);
                }
            }
            if(!any)return;
            // The gem is flat (tipped up to face the eye on its stage), so its width is what shows: a smaller box.
            float wide=Mathf.Max(b.size.x,b.size.z),width=index==0?ModelWidth*.55f:ModelWidth;
            float s=Mathf.Min(ModelHeight/Mathf.Max(b.size.y,1e-4f),width/Mathf.Max(wide,1e-4f));
            m.localScale=Vector3.one*s;m.localPosition=new Vector3(-b.center.x*s,-ModelHeight*.5f-b.min.y*s,-b.center.z*s);
        }

        // ---- Refresh -----------------------------------------------------------------------------------------------
        void RefreshUpgrades()
        {
            Say(upgradeCoins,Run.Coins.ToString());
            hoveredTile=ForcedUpgradeHover;
            if(hoveredTile<0)for(int i=0;i<4;i++)if(tiles[i].Button.IsHovered || handMenu && handMenu.IsHovered(tiles[i].Button)){hoveredTile=i;break;}
            // Gem: capacity (water units)
            int gemPrice=Run.MagicUpgradeCost;
            SetTile(tiles[0],$"{Run.MagicCapacity:0}",$"{Run.MagicCapacity+EmberdeepWaterTuning.GemCapacityPerLevel:0}",gemPrice,Run.CanUpgradeMagic,gemPrice<=0,true);
            var gemTheme=tiles[0].Button.GetComponent<EmberdeepForestUi>();if(gemTheme)gemTheme.Guided=Run.NeedsMagicUpgrade && Run.CanUpgradeMagic;
            // Pump: water per strike
            int pumpPrice=Run.PumpUpgradeCost;
            SetTile(tiles[1],$"+{Run.PumpStrikeWater:0}",$"+{Run.PumpStrikeWater+FireRun.PumpWaterPerLevel:0}",pumpPrice,Run.CanUpgradePump,pumpPrice<=0,true);
            // Slot and tower (L3's upgrades, FireRun.Pieces.cs)
            int slotPrice=Run.SlotUpgradeCost;
            SetTile(tiles[2],$"{Run.SlotLevel}",$"{Run.SlotLevel+1}",slotPrice,Run.CanUpgradeSlot,slotPrice<=0,Run.TowersIntroduced); // locked until towers (FireRun.TowerStage)
            int towerPrice=Run.TowerUpgradeCost;
            SetTile(tiles[3],$"{TowerRadiusCentimetres(Run.TowerRadius):0} cm",$"{TowerRadiusCentimetres(Run.TowerRadius+EmberdeepPieceTuning.TowerRadiusPerLevel):0} cm",
                towerPrice,Run.CanUpgradeTower,towerPrice<=0,Run.TowersIntroduced);
            Say(upgradeExplain,hoveredTile>=0?tiles[hoveredTile].Explain:UpgradesPrompt);
            upgradeExplain.color=hoveredTile>=0?UiCream:UiCream*.75f;
            RefreshUpgradePictures();
        }
        void SetTile(UpgradeTile tile,string now,string next,int price,bool affordable,bool max,bool available)
        {
            tile.Buyable=available && affordable;tile.Max=max;tile.Now=now;tile.Next=next;
            tile.Button.Interactable=available;
            bool gain=available && !max && hoveredTile==tile.Index;
            tile.Number.gameObject.SetActive(available);tile.Unit.gameObject.SetActive(available);tile.Lock.gameObject.SetActive(!available);
            Say(tile.Number,!available?"":gain?next:now);
            if(tile.Arrow.gameObject.activeSelf!=gain)tile.Arrow.gameObject.SetActive(gain);
            tile.Number.transform.localPosition=new Vector3(gain?.03f:0,NumberY,-.04f);
            Say(tile.Price,!available?"":max?"MAX":price.ToString());
            tile.Coin.gameObject.SetActive(available && !max);
            var ink=available?UiCream:UiDim;
            tile.Word.color=tile.Unit.color=ink;tile.Number.color=gain?UiGain:ink;
            tile.Price.color=max?UiGold:affordable?UiCream:new Color(.75f,.42f,.32f);
            UiTint(tile.Coin,affordable?UiGold:UiDim);UiTint(tile.Lock,UiDim);
        }
    }
}

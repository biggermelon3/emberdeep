using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FireRescue
{
    // End-of-level moments in the world (LoopSpec §2.3, §4.5). The HUD line stays, but only as a backup.
    // - Lost: the board flares ember-red, then darkens and stays dark; a carved banner "FOREST LOST" pops up
    //   over the board.
    // - Cleared / Won: a gold flare that settles back; banner "FIRE OUT" / "FOREST SAVED" with stars and
    //   the sheep count.
    // The board's V2 shader has a fixed ambient, so dimming the lamp alone barely reads (~20%). A feathered
    // veil just above the canopy (one alpha-blended sheet, no post pass) carries the flare and the darkness;
    // the lamp dims under it for shading. The banner sits above the veil plane and below the HUD's bottom row,
    // as seen from the seated eye, and the meter draws after the veil so it stays bright.
    // - The banner shrinks away when the results panel rises (FireRescueGame.ResultsDelay) or the level select opens.
    // - The burn meter moved to the board's rim (EmberdeepRimHud, LoopSpec §11b); Meter/MeterPips/MeterCharred forward
    //   there. In the spell flow the banner is the one word only: the results panel carries stars, sheep and coins.
    // - Dim: a plain ash veil for the pause panel and the level select, used only while no moment holds the veil.
    // The sting is FireFeedback.Complete, which the game already plays on the same phase change.
    // FireFeedback.Initialize creates this view when FireFeedback shares a GameObject with FireRescueGame.
    [DefaultExecutionOrder(10020)] // after EmberdeepLocalizedText swaps the banner text, so the fit sees it
    public sealed class EmberdeepOutcomeView : MonoBehaviour
    {
        // Banner: anchor in arena units (board scale .08); the sign itself in metres. With the board .65 m under
        // and .8 m ahead of the eye, the top edge stays ~20 deg below eye level (HUD bottom row: 17.6 deg) and the
        // bottom edge stays above the veil.
        public static readonly Vector3 BannerAnchor=new(0,4.0f,4.4f);
        // The anchor follows the measured plinth (EmberdeepRimHud.Board): BannerAnchor.y above its top, BannerDepth of the
        // way from its front to its back edge (4.4 on today's board), centred across it.
        public const float BannerDepth=.3808f;
        Vector3 BannerPoint()
        {
            var b=game.RimHud?game.RimHud.Board:EmberdeepRimHud.DefaultBoard;
            return new Vector3(b.center.x,b.max.y+BannerAnchor.y,Mathf.Lerp(b.min.z,b.max.z,BannerDepth));
        }
        public const float BannerWidth=.5f,BannerHeight=.15f,TitleHeight=.044f,SubtitleHeight=.021f,BannerTextWidth=.43f;
        public const float BannerPop=.55f,BannerOvershoot=1.7f,BannerDrop=.05f,BannerHide=.25f,BannerBob=.005f;
        // Board light. Loss: flare to ember red, then darken. Clear: gold flare, then settle back.
        public const float LossFlare=.14f,LossDarken=1.3f,LossFlareIntensity=2.4f,LossIntensity=.3f;
        public const float ClearFlare=.22f,ClearSettle=1.6f,ClearFlareIntensity=1.9f;
        // Veil: arena units. Covers the board plus a front apron, feathered at the edges.
        public const float VeilHeight=3f,VeilHalfWidth=6.2f,VeilFront=-2.5f,VeilBack=13.2f,VeilFeather=1.4f;
        public const float LossFlareVeil=.32f,LossVeil=.58f,ClearVeil=.26f;
        static readonly Color VeilEmber=new(1,.38f,.12f),VeilAsh=new(.03f,.018f,.015f),VeilGold=new(1,.84f,.42f);
        static readonly Color LossFlareColor=new(1,.42f,.18f),LossLightColor=new(.78f,.56f,.5f),ClearFlareColor=new(1,.86f,.52f);
        public static readonly Vector3 SeatedEye=new(0,.65f/FireMiniature.Scale,6-.8f/FireMiniature.Scale);
        public const float MeterPop=EmberdeepRimHud.MeterPop;
        public const float DimVeil=.55f;
        static readonly Color LossBoard=new(.22f,.07f,.05f),LossEdge=new(.95f,.42f,.16f),ClearBoard=new(.06f,.19f,.12f),ClearEdge=new(.95f,.76f,.3f);

        FireRescueGame game;
        Transform arena;
        EmberdeepLocalizedText localizer;
        FirePhase lastPhase,moment=FirePhase.Fighting;
        bool seenPhase,hiding;
        float clock,hideClock;
        // Light capture
        Light lamp;bool captured;float baseIntensity;Color baseLampColor;
        Renderer veil;Material veilMaterial;Mesh veilMesh;
        // Banner
        Transform banner,starRow;TextMesh title,subtitle;Renderer board;Material boardMaterial;
        readonly Renderer[] stars=new Renderer[3];
        Material unlit;Mesh starMesh;MaterialPropertyBlock block;
        // Pause panel / level select: 0..1, eased.
        public float Dim {get;set;}
        float dimShown;

        public bool BannerVisible=>banner && banner.gameObject.activeSelf;
        public string BannerTitle=>title?title.text:"";
        public FirePhase Moment=>moment;
        public FirePhase ObservedPhase=>lastPhase;
        public string DebugState=>$"observed={lastPhase} moment={moment} banner={(banner?banner.gameObject.activeSelf:false)} hiding={hiding} arena={(arena?arena.gameObject.activeInHierarchy:false)} clock={clock:F2}";
        public Transform Banner=>banner;
        public Transform Meter=>game && game.RimHud?game.RimHud.Meter:null;
        public int MeterPips=>game && game.RimHud?game.RimHud.MeterPips:0;
        public int MeterCharred=>game && game.RimHud?game.RimHud.MeterCharred:0;
        public float LightLevel=>lamp && captured?lamp.intensity:-1;
        public float VeilStrength=>veil && veil.enabled?veilMaterial.color.a:0;

        public void Initialize(FireRescueGame owner){game=owner;block=new MaterialPropertyBlock();}

        void LateUpdate()
        {
            if(!game)return;
            if(!arena && game.Arena)Build();
            if(!arena)return;
            var run=game.Run;float dt=Mathf.Min(Time.unscaledDeltaTime,.1f);
            var phase=run.Phase;
            if(!seenPhase){lastPhase=phase;seenPhase=true;}
            if(phase!=lastPhase)
            {
                if(lastPhase==FirePhase.Fighting && IsOutcome(phase))Begin(phase);
                else if(!IsOutcome(phase))End();
                lastPhase=phase;
            }
            clock+=dt;
            TickLight();TickDim(dt);TickBanner(dt);
        }
        static bool IsOutcome(FirePhase p)=>p==FirePhase.Lost || p==FirePhase.Cleared || p==FirePhase.Won;

        // Starts a moment now. Public for tests and for a future debug menu; the game drives it by phase.
        public void Begin(FirePhase outcome)
        {
            if(!arena || !IsOutcome(outcome))return;
            Restore();moment=outcome;clock=0;hiding=false;
            Capture();ShowBanner(outcome);
        }
        void End()
        {
            if(moment==FirePhase.Fighting)return;
            moment=FirePhase.Fighting;Restore();
            if(BannerVisible){hiding=true;hideClock=0;}
        }

        // ---- Light ---------------------------------------------------------------------------------
        void Capture()
        {
            if(!lamp)foreach(var l in arena.GetComponentsInChildren<Light>(true))if(l.type==LightType.Directional && (!lamp || l.name=="Warm evening sun"))lamp=l;
            if(lamp){baseIntensity=lamp.intensity;baseLampColor=lamp.color;}
            captured=true;
        }
        void Restore()
        {
            if(veil)veil.enabled=false;
            if(!captured)return;
            if(lamp){lamp.intensity=baseIntensity;lamp.color=baseLampColor;}
            captured=false;
        }
        void TickLight()
        {
            if(!captured)return;
            float intensity;Color color,sheet;
            if(moment==FirePhase.Lost)
            {
                if(clock<LossFlare)
                {
                    float u=clock/LossFlare;intensity=Mathf.Lerp(baseIntensity,LossFlareIntensity,u);color=Color.Lerp(baseLampColor,LossFlareColor,u);
                    sheet=VeilEmber;sheet.a=LossFlareVeil*u;
                }
                else
                {
                    float u=Smooth((clock-LossFlare)/LossDarken);
                    intensity=Mathf.Lerp(LossFlareIntensity,LossIntensity,u);color=Color.Lerp(LossFlareColor,LossLightColor,u);
                    sheet=Color.Lerp(VeilEmber,VeilAsh,u);sheet.a=Mathf.Lerp(LossFlareVeil,LossVeil,u);
                }
            }
            else
            {
                if(clock<ClearFlare)
                {
                    float u=clock/ClearFlare;intensity=Mathf.Lerp(baseIntensity,ClearFlareIntensity,u);color=Color.Lerp(baseLampColor,ClearFlareColor,u);
                    sheet=VeilGold;sheet.a=ClearVeil*u;
                }
                else
                {
                    float u=Smooth((clock-ClearFlare)/ClearSettle);
                    if(u>=1){Restore();return;}
                    intensity=Mathf.Lerp(ClearFlareIntensity,baseIntensity,u);color=Color.Lerp(ClearFlareColor,baseLampColor,u);
                    sheet=VeilGold;sheet.a=ClearVeil*(1-u);
                }
            }
            if(lamp){lamp.intensity=intensity;lamp.color=color;}
            if(veil){veilMaterial.color=sheet;veil.enabled=sheet.a>.002f;}
        }

        void TickDim(float dt)
        {
            dimShown=Mathf.MoveTowards(dimShown,Mathf.Clamp01(Dim),dt*3);
            if(captured || !veil)return;
            bool on=dimShown>.002f;
            if(on){var c=VeilAsh;c.a=DimVeil*dimShown;veilMaterial.color=c;}
            if(veil.enabled!=on)veil.enabled=on;
        }

        // ---- Banner --------------------------------------------------------------------------------
        void ShowBanner(FirePhase outcome)
        {
            bool lost=outcome==FirePhase.Lost;var run=game.Run;
            title.text=lost?"FOREST LOST":outcome==FirePhase.Won?"FOREST SAVED":"FIRE OUT"; // §13: 1-2 words, no "!"
            title.color=lost?new Color(1,.55f,.32f):new Color(1,.86f,.42f);
            subtitle.text=lost?"Too many trees burned":$"SHEEP {run.AnimalsSaved+CelebratingSheep(run)}/{run.Animals.Length}";
            subtitle.color=lost?new Color(1,.86f,.78f):new Color(.86f,1,.9f);
            boardMaterial.SetColor("_BaseColor",lost?LossBoard:ClearBoard);boardMaterial.SetColor("_EdgeColor",lost?LossEdge:ClearEdge);
            // §11b: one word over the board; the results panel shows stars, sheep and coins.
            bool word=run.SpellMode;
            starRow.gameObject.SetActive(!lost && !word);subtitle.gameObject.SetActive(!word);
            title.transform.localPosition=new Vector3(0,word?0:.02f,-.012f);
            subtitle.transform.localPosition=new Vector3(lost?0:.065f,-.036f,-.012f);
            for(int i=0;i<3;i++)Tint(stars[i],i<run.Stars?new Color(1,.8f,.22f):new Color(.25f,.3f,.27f));
            banner.gameObject.SetActive(true);
            if(localizer)localizer.RefreshLabels();
            Fit(title,game.Run.SpellMode?TitleHeight*1.4f:TitleHeight,BannerTextWidth);Fit(subtitle,SubtitleHeight,lost?BannerTextWidth:.26f);
            PoseBanner(0,0);
        }
        // Sheep still in their arrival hop when the fire goes out are as good as saved.
        static int CelebratingSheep(FireRun run){int n=0;foreach(var a in run.Animals)if(a.State==AnimalState.Celebrating)n++;return n;}
        void TickBanner(float dt)
        {
            if(!BannerVisible)return;
            if(!arena.gameObject.activeInHierarchy){banner.gameObject.SetActive(false);hiding=false;return;}
            // The banner hands over to the results panel: on the compact board it stands in front of the panel's lower half
            // (banner ~-7..-27 deg from the seated eye, panel ~+2..-19 deg), and the height/distance options move both, so
            // they take turns instead of sharing the view. It also leaves for the level select.
            if(!hiding && (game.LevelSelectOpen || game.ResultsVisible)){hiding=true;hideClock=0;}
            if(hiding)
            {
                hideClock+=dt;float u=1-Smooth(hideClock/BannerHide);
                if(u<=0){banner.gameObject.SetActive(false);hiding=false;return;}
                PoseBanner(1,u);return;
            }
            Fit(title,game.Run.SpellMode?TitleHeight*1.4f:TitleHeight,BannerTextWidth);Fit(subtitle,SubtitleHeight,moment==FirePhase.Lost?BannerTextWidth:.26f);
            float pop=Mathf.Clamp01(clock/BannerPop);
            PoseBanner(pop,BackOut(pop));
        }
        void PoseBanner(float progress,float scale)
        {
            var cam=game.Player?game.Player.ViewCamera:Camera.main;
            var at=arena.TransformPoint(BannerPoint())+Vector3.up*((1-progress)*BannerDrop+Mathf.Sin(clock*1.7f)*BannerBob*progress);
            banner.position=at;
            if(cam){var look=at-cam.transform.position;if(look.sqrMagnitude>1e-6f)banner.rotation=Quaternion.LookRotation(look,Vector3.up);}
            banner.localScale=Vector3.one*Mathf.Max(.001f,scale);
        }
        static float BackOut(float u){u=Mathf.Clamp01(u);float c=BannerOvershoot;float v=u-1;return 1+(c+1)*v*v*v+c*v*v;}
        static void Fit(TextMesh text,float height,float width)
        {
            if(!text || string.IsNullOrEmpty(text.text))return;
            var size=text.GetComponent<Renderer>().localBounds.size;
            if(size.y<1e-5f || size.x<1e-5f)return;
            float s=Mathf.Min(height/size.y,width/size.x);
            if(Mathf.Abs(s-1)>.01f)text.characterSize*=s;
        }

        // ---- Build ---------------------------------------------------------------------------------
        void Build()
        {
            arena=game.Arena;localizer=game.GetComponent<EmberdeepLocalizedText>();
            var source=Resources.Load<Material>("EmberdeepMenuUnlit");
            unlit=source?new Material(source){name="Outcome unlit"}:new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Outcome unlit"};
            starMesh=StarMesh();
            // Banner (metres, faces the head).
            banner=new GameObject("Outcome banner").transform;banner.SetParent(transform,false);
            var plate=GameObject.CreatePrimitive(PrimitiveType.Cube);plate.name="Outcome banner board";plate.transform.SetParent(banner,false);
            plate.transform.localScale=new Vector3(BannerWidth,BannerHeight,.014f);RemoveCollider(plate);
            EmberdeepForestUi.Apply(plate);board=plate.GetComponent<Renderer>();boardMaterial=board.sharedMaterial;
            board.shadowCastingMode=ShadowCastingMode.Off;
            title=Text(banner,"",new Vector3(0,.02f,-.012f),Color.white);
            subtitle=Text(banner,"",new Vector3(0,-.036f,-.012f),Color.white);
            starRow=new GameObject("Stars").transform;starRow.SetParent(banner,false);starRow.localPosition=new Vector3(-.11f,-.036f,-.012f);
            for(int i=0;i<3;i++)
            {
                var star=new GameObject("Star "+(i+1));star.transform.SetParent(starRow,false);
                star.transform.localPosition=new Vector3((i-1)*.044f,0,0);star.transform.localScale=Vector3.one*.019f;
                star.AddComponent<MeshFilter>().sharedMesh=starMesh;
                var r=star.AddComponent<MeshRenderer>();r.sharedMaterial=unlit;r.shadowCastingMode=ShadowCastingMode.Off;stars[i]=r;
            }
            banner.gameObject.SetActive(false);
            // Veil (arena units).
            var shader=Resources.Load<Shader>("EmberdeepOutcomeVeil");
            if(shader)
            {
                veilMesh=VeilMesh();veilMaterial=new Material(shader){name="Outcome veil",color=Color.clear};
                var sheet=new GameObject("Outcome veil");sheet.transform.SetParent(arena,false);sheet.transform.localPosition=Vector3.up*VeilHeight;
                sheet.AddComponent<MeshFilter>().sharedMesh=veilMesh;veil=sheet.AddComponent<MeshRenderer>();veil.sharedMaterial=veilMaterial;
                veil.shadowCastingMode=ShadowCastingMode.Off;veil.receiveShadows=false;veil.enabled=false;
            }
        }
        static void RemoveCollider(GameObject go){var c=go.GetComponent<Collider>();if(c){c.enabled=false;Destroy(c);}}
        static TextMesh Text(Transform parent,string value,Vector3 at,Color color)
        {
            var go=new GameObject("Outcome label");go.transform.SetParent(parent,false);go.transform.localPosition=at;
            var t=go.AddComponent<TextMesh>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");go.GetComponent<MeshRenderer>().sharedMaterial=t.font.material;
            t.fontSize=64;t.characterSize=.01f;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=color;t.text=value;return t;
        }
        void Tint(Renderer r,Color color){if(!r)return;block.Clear();block.SetColor("_BaseColor",color);r.SetPropertyBlock(block);}
        static float Smooth(float u){u=Mathf.Clamp01(u);return u*u*(3-2*u);}
        // 4x4 grid: the inner rectangle is solid, the outer ring feathers to zero alpha.
        static Mesh VeilMesh()
        {
            float[] xs={-VeilHalfWidth-VeilFeather,-VeilHalfWidth,VeilHalfWidth,VeilHalfWidth+VeilFeather};
            float[] zs={VeilFront-VeilFeather,VeilFront,VeilBack,VeilBack+VeilFeather};
            var v=new Vector3[16];var c=new Color[16];var t=new List<int>();
            for(int z=0;z<4;z++)for(int x=0;x<4;x++){int i=z*4+x;v[i]=new Vector3(xs[x],0,zs[z]);c[i]=new Color(1,1,1,x>0 && x<3 && z>0 && z<3?1:0);}
            for(int z=0;z<3;z++)for(int x=0;x<3;x++){int a=z*4+x;t.AddRange(new[]{a,a+4,a+1,a+1,a+4,a+5});}
            var m=new Mesh{name="Outcome veil"};m.vertices=v;m.colors=c;m.SetTriangles(t,0);m.RecalculateBounds();return m;
        }
        // Five-pointed star, double-sided, unit radius, facing -Z.
        static Mesh StarMesh()
        {
            var v=new List<Vector3>{Vector3.zero};var t=new List<int>();
            for(int i=0;i<10;i++){float a=Mathf.PI*.5f+i*Mathf.PI/5;float r=i%2==0?1:.45f;v.Add(new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,0));}
            for(int i=0;i<10;i++){int a=1+i,b=1+(i+1)%10;t.AddRange(new[]{0,a,b,0,b,a});}
            var m=new Mesh{name="Outcome star"};m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();return m;
        }
        void OnDisable(){Restore();}
        void OnDestroy()
        {
            Restore();
            if(unlit)Destroy(unlit);if(starMesh)Destroy(starMesh);
            if(veilMaterial)Destroy(veilMaterial);if(veilMesh)Destroy(veilMesh);if(veil)Destroy(veil.gameObject);
        }
    }
}

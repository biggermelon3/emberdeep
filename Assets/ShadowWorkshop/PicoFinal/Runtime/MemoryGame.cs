using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CloudGarden.Integration;
using CloudGarden.ShadowPuzzle;
using PhysicalAssembly.MetaXR;
using Oculus.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace ShapeOfMemory
{
    // Shape of Memory: the four posable-pile levels (Torch2Level) as one game in one scene, played in a book (MemoryBook)
    // standing open on the table, its pages where the lamps' walls are. Spread 0 is the title page (title left, tiles
    // right), then one spread per memory (its chapter: name and picture left, riddle and PLAY right), then the ending.
    // Turning the pages browses; a chapter's PLAY makes its spread the stage, the riddle and hints printed on the page the
    // lamp leaves dark (or in the margins when both are lit). A solved memory's page corner beckons: turning it plays the
    // outro and the next chapter starts. The console at the front right keeps the buttons that are not pages (hint, reset,
    // menu, lower and raise). A level swaps in place, so the player rig and passthrough are never reloaded. The scene owns
    // its player; GardenNavigation is not involved.
    public sealed class MemoryGame : MonoBehaviour, IShadowHost
    {
        public enum Phase{Title,Levels,Intro,Playing,Paused,Solved,Leaving,Ending}
        public const string GameTitle="SHAPE OF MEMORY";
        // RevealPause: seconds the reveal plays before the solved buttons appear. Lift: one LOWER / RAISE step. Dimmed: the
        // paper's brightness while the lamps light the pages (their walls' unlit colour).
        public const float RevealPause=1.6f,HoldToReset=1f,Lift=.025f,Pitch=.105f,RowPitch=.115f,Dimmed=.24f,PaperFade=1.6f;
        [Tooltip("Player prefab. The Pico build swaps in its own (PicoBuild.OnProcessScene looks for this field name).")]
        public GameObject StandalonePlayer;
        public Material Surface,Screen,Glow,IconMaterial;
        [Tooltip("White-on-transparent button icons, found by file name (Tools/ShadowArt/build_icons.py).")]
        public Texture2D[] Icons=Array.Empty<Texture2D>();
        [Tooltip("Standing: the book open at a right angle, its pages the lamps' walls; turn a page by ray-pinching its corner. Flat: a smaller book lying open on the table, the shadows on the room's own walls; turn a page by pinching its edge in the hand.")]
        public MemoryBook.Layout BookLayout=MemoryBook.Layout.Flat;
        // Tests keep progress in memory instead of PlayerPrefs, and may pick the book's layout. Cleared on entering Play,
        // since domain reload is off.
        public static MemoryProgress.IStore StoreOverride;
        public static MemoryBook.Layout? LayoutOverride;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetOverride(){StoreOverride=null;LayoutOverride=null;}

        public MetaPlayerRig Player{get;private set;}
        public MemoryProgress Progress{get;private set;}
        public Phase Current{get;private set;}
        public int LevelIndex{get;private set;}=-1;
        public Torch2Workshop Level{get;private set;}
        public int HintStage{get;private set;}
        public Transform Stage{get;private set;}
        public bool Ready{get;private set;}
        public string ShownSet{get;private set;}
        public bool Busy=>Level&&Level.Busy;
        public MemoryBook Book{get;private set;}
        public MemoryBook.Layout Layout{get;private set;}
        bool Flat=>Layout==MemoryBook.Layout.Flat;
        // A level's spread, and the ending's.
        public static int SpreadOf(int level)=>level+1;
        public int EndingSpread=>Progress.Count+1;
        // The open pages' words (tests, logs).
        public string PageText(bool right)=>string.Join("\n",(right?Book.RightFace:Book.LeftFace).GetComponentsInChildren<TMP_Text>().Select(t=>t.text));
        public MemoryTile Tile(string id)=>tiles.TryGetValue(id,out var tile)&&tile?tile:null;
        MetaShadowPlayer xr;
        readonly MemoryKit kit=new MemoryKit(),roomKit=new MemoryKit();
        MemoryPages pages;
        Transform console,idleRoom;
        Torch2Room idle;
        GameObject slab;
        AimZone consoleZone;
        readonly Dictionary<string,GameObject> sets=new Dictionary<string,GameObject>();
        readonly Dictionary<string,Vector2Int> setSizes=new Dictionary<string,Vector2Int>();
        readonly Dictionary<string,MemoryButton> buttons=new Dictionary<string,MemoryButton>();
        readonly Dictionary<string,MemoryTile> tiles=new Dictionary<string,MemoryTile>();
        AudioSource audioSource;
        AudioClip click;
        float solvedAt,nextReport,paper=1;
        MemorySounds sounds;
        public MemoryIntro Intro{get;private set;}
        public MemoryGuide Guide{get;private set;}
        // System recenter: the frame to re-place the table on (-1 = none), and the head pose last frame.
        int recenterFrame=-1;
        Pose lastHead;
        bool headSeen;
        readonly List<XRInputSubsystem> inputs=new List<XRInputSubsystem>();

        sealed class Spec
        {
            public readonly string id,caption,icon;public readonly Action action;public readonly Func<bool> enabled;public readonly float hold;
            public Spec(string id,string caption,string icon,Action action,Func<bool> enabled=null,float hold=0){this.id=id;this.caption=caption;this.icon=icon;this.action=action;this.enabled=enabled;this.hold=hold;}
        }

        IEnumerator Start()
        {
            Progress=new MemoryProgress(StoreOverride??new MemoryProgress.Prefs());Layout=LayoutOverride??BookLayout;
            foreach(var k in new[]{kit,roomKit}){k.Surface=Surface;k.Screen=Screen;k.Icon=IconMaterial;}
            pages=new MemoryPages(kit);
            var root=Instantiate(StandalonePlayer);Player=root.GetComponentInChildren<MetaPlayerRig>(true);Player.Initialize(Player.XRRig,Player.DesktopCamera);
            xr=gameObject.AddComponent<MetaShadowPlayer>();xr.Player=Player;xr.Host=this;
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.spatialBlend=0;audioSource.volume=.35f;click=MemoryUi.Click(kit.Owned);
            sounds=new MemorySounds(kit.Owned);
            var room=gameObject.AddComponent<AudioSource>();room.clip=sounds.Room;room.loop=true;room.spatialBlend=0;room.volume=.07f;room.Play();
            if(Player.IsXR&&xr.UnityXRRig)SilenceMrukTicker();
            Stage=new GameObject("Stage").transform;Stage.SetParent(transform,false);
            BuildConsole();BuildBook();
            if(Player.IsXR)
            {
                float deadline=Time.realtimeSinceStartup+3;
                while(Player.ViewCamera.transform.position.y-Player.XRRig.position.y<.4f&&Time.realtimeSinceStartup<deadline)yield return null;
                SubsystemManager.GetSubsystems(inputs);foreach(var input in inputs)input.trackingOriginUpdated+=OnTrackingOriginUpdated;
                if(xr.UnityXRRig)ShowRealHands();
                // Rays stay drawn while they point into an AimZone (the console, the pages' tiles and corners), not only on a button.
                Rays=ZoneRays.Add(gameObject,xr,Glow,kit.Owned);Rays.Hidden=hand=>HandAtOrb(hand==Oculus.Interaction.Input.Handedness.Left)||Book.HandAt(hand==Oculus.Interaction.Input.Handedness.Left);
            }
            else{Player.ViewCamera.fieldOfView=48;Player.ViewCamera.backgroundColor=new Color(.045f,.075f,.09f);}
            Recenter();ShowTitle();Ready=true;
            Debug.Log($"SHAPE_OF_MEMORY_READY xr={Player.IsXR} rig={(Player.IsXR?xr.UnityXRRig?"unityxr":"ovr":"desktop")} inputs={inputs.Count} solved={Progress.SolvedCount}");
        }
        // On the Pico rig the SDK's hand mesh, fitted to Meta's skeleton, sits off the real hands that passthrough already
        // shows. Hide it and draw the tracked joints themselves (MemoryHands).
        void ShowRealHands()
        {
            foreach(var visual in Player.XRRig.GetComponentsInChildren<HandVisual>(true))visual.ForceOffVisibility=true;
            Hands=MemoryHands.Add(gameObject,xr,Surface,Glow,kit.Owned);
        }
        public MemoryHands Hands{get;private set;}
        public ZoneRays Rays{get;private set;}
        // A hand at the orb pinches the pile directly; a ray from it would only mislead.
        bool HandAtOrb(bool left)=>Level&&Level.input.enabled&&xr.ReadHand(left,out _,out var tip,out _)&&Level.input.Near(tip);

        // ---- Phases
        public void ShowTitle()
        {
            if(Busy)return;
            ClearLevel();IdleRoom(true);Current=Phase.Title;paper=1;Open(0,"title");
        }
        // The contents: a memory's chapter (the next to play when none is given).
        public void ShowLevels()=>ShowLevels(Mathf.Max(0,Progress.Continue));
        public void ShowLevels(int index)
        {
            if(Busy)return;
            ClearLevel();IdleRoom(true);Current=Phase.Levels;paper=1;Open(SpreadOf(Mathf.Clamp(index,0,Progress.Count-1)),"levels");
        }
        // After Home: the last spread, every picture found.
        public void ShowEnding()
        {
            if(Busy)return;
            ClearLevel();IdleRoom(true);Current=Phase.Ending;paper=1;Open(EndingSpread,"ending");
            audioSource.PlayOneShot(sounds.Ending,.6f);Debug.Log("SHAPE_OF_MEMORY_ENDING");
        }
        // Turns (riffling) to a spread, then shows its buttons.
        void Open(int spread,string set)
        {
            if(Book.Spread==spread&&!Book.Turning){Book.Refill();Show(set);return;}
            Show(null);Book.TurnTo(spread,()=>{Book.Refill();Show(set);});
        }
        void Play(){int next=Progress.Continue;Begin(next<0?0:next);}
        // From the title or a chapter: to the memory's spread, then its intro.
        void Begin(int index)
        {
            if(Busy||Book.Turning)return;
            if(Book.Spread==SpreadOf(index)){Load(index);return;}
            Show(null);Book.TurnTo(SpreadOf(index),()=>Load(index));
        }
        // force: open a locked level (development keys and tests).
        public void Load(int index,bool force=false)
        {
            if(Busy||index<0||index>=Progress.Count||!(force||Progress.Unlocked(index)))return;
            var asset=Torch2Level.Load(MemoryProgress.Levels[index]);if(!asset)return;
            ClearLevel();DestroyIdleRoom();
            var content=new GameObject(asset.name+" memory");content.SetActive(false);content.transform.SetParent(Stage,false);
            var game=content.AddComponent<Torch2Workshop>();game.bare=true;game.level=asset;game.viewCamera=Player.ViewCamera;game.xr=xr;game.roomStyle=Style;game.hostShowsPictures=true;
            game.surfaceMaterial=Surface;game.screenMaterial=Screen;game.glowMaterial=Glow;
            // The standing book's pages are the lamps' walls: dimmed while they light them. The flat book stays lit.
            content.SetActive(true);Level=game;LevelIndex=index;HintStage=0;Current=Phase.Intro;paper=Flat?1:Dimmed;Book.SetBrightness(paper);
            if(Book.Spread!=SpreadOf(index))Book.Show(SpreadOf(index));else Book.Refill();
            Show(null);Intro=MemoryIntro.Play(game,Progress.SeenIntro(index),sounds);Guide=null;
        }
        void Hint()
        {
            if(Current!=Phase.Playing||HintStage>=MemoryProgress.MaxHints)return;
            HintStage++;Progress.UsedHints(LevelIndex,HintStage);Book.Refill();buttons["play/hint"].PipsLit=MemoryProgress.MaxHints-HintStage;
        }
        void ResetOrb(){if(Level&&!Level.Busy&&!Level.Completed)Level.ResetChallenge();}
        // Jump to the end of the running intro (tests and the development K key; players skip with a pinch once seen).
        public void FinishIntro(){if(Intro&&Intro.Running)Intro.Finish();}
        // Meta's MR Utility Kit ticks a hidden global context every frame through OVRPlugin, which the Pico build strips:
        // a DllNotFoundException every frame. Nothing here uses MRUK, so switch the ticker off (destroying it makes MRUK
        // recreate it).
        static void SilenceMrukTicker()
        {
            var ticker=GameObject.Find("MRUKGlobalContext");
            if(ticker)foreach(var behaviour in ticker.GetComponents<MonoBehaviour>())behaviour.enabled=false;
            Debug.Log("SHAPE_OF_MEMORY_MRUK silenced="+(ticker!=null));
        }
        void Pause(){if(Current!=Phase.Playing||Busy)return;Current=Phase.Paused;Level.input.enabled=false;Level.world.Hover=Torch2Input.Grip.None;Book.Refill();Show("pause");}
        void Resume(){if(Current!=Phase.Paused)return;Current=Phase.Playing;Level.input.enabled=true;Book.Refill();Show("play");}
        // The page turn after a solved memory: the outro, then the next chapter (or the ending) and its intro.
        void Next()
        {
            int next=LevelIndex+1;
            Leave(()=>{ClearLevel();IdleRoom(false);Book.TurnTo(next<Progress.Count?SpreadOf(next):EndingSpread,()=>{if(next<Progress.Count)Load(next,true);else ShowEnding();});});
        }
        void Replay(){int index=LevelIndex;Leave(()=>Load(index));}
        void Contents(){int index=LevelIndex;Leave(()=>ShowLevels(index));}
        // Out of a level through its outro (MemoryOutro), then on to whatever comes next.
        void Leave(Action then)
        {
            if(!Level){then();return;}
            if(Busy||Current==Phase.Leaving||Current==Phase.Intro)return;
            Current=Phase.Leaving;Show(null);MemoryOutro.Play(Level,sounds,then);
        }
        void Update()
        {
            if(!Ready)return;
            if(Current==Phase.Intro&&Level&&!Intro.Running)
            {
                Current=Phase.Playing;Progress.SawIntro(LevelIndex);Show("play");
                Guide=MemoryGuide.Add(Level,LevelIndex==0&&!Progress.Taught,xr.Active,Glow,()=>Progress.Taught=true);
            }
            if(Current==Phase.Playing&&Level&&Level.Completed){Current=Phase.Solved;solvedAt=Time.unscaledTime;Progress.Solve(LevelIndex);Show(null);Book.Refill();}
            if(Current==Phase.Solved&&ShownSet==null&&Time.unscaledTime>=solvedAt+RevealPause)Show("solved");
            if(!Mathf.Approximately(Book.Brightness,paper))Book.SetBrightness(Mathf.MoveTowards(Book.Brightness,paper,Time.unscaledDeltaTime*PaperFade));
            if(Player.IsXR)FollowSystemRecenter();
            if(Player.IsXR&&Time.unscaledTime>=nextReport){nextReport=Time.unscaledTime+10;Debug.Log($"SHAPE_OF_MEMORY_HANDS phase={Current} spread={Book.Spread} {xr.Report()}");}
            DesktopClick();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevelopmentKeys();
#endif
        }

        // ---- The book
        void BuildBook()
        {
            Book=MemoryBook.Build(Stage,kit,sounds,xr,HandsFree,Layout);Book.Count=Progress.Count+2;Book.Fill=Fill;
            Book.Open=CornerOpen;Book.Beckon=forward=>forward&&Current==Phase.Solved;
            Book.Pressed=forward=>{if(Current!=Phase.Solved||!forward)return false;Debug.Log("SHAPE_OF_MEMORY_PRESS corner/next");Next();return true;};
            Book.Turned=Turned;
        }
        // Corners turn pages while browsing; in a memory only the solved one's, which goes on to the next.
        bool CornerOpen(bool forward)
        {
            if(Current==Phase.Solved)return forward&&ShownSet=="solved";
            if(Level||Current==Phase.Leaving||Book.Spread!=Expected)return false;
            // The ending's spread opens once the last memory is found.
            return !forward||Book.Spread<Progress.Count||Finished;
        }
        bool Finished=>Progress.Solved(Progress.Count-1);
        // The spread the phase stands on (a browsed corner turn updates the phase once it lands).
        int Expected=>Current==Phase.Title?0:Current==Phase.Ending?EndingSpread:Book.Spread;
        void Turned(int spread)
        {
            if(Level||Current==Phase.Leaving)return;
            Current=spread==0?Phase.Title:spread==EndingSpread?Phase.Ending:Phase.Levels;Show(spread==0?"title":spread==EndingSpread?"ending":"levels");
            Debug.Log($"SHAPE_OF_MEMORY_PAGE {spread}");
        }
        void Fill(int spread,bool right,Transform face,bool live)
        {
            int index=spread-1;
            if(spread==0)TitlePage(face,right,live);
            else if(index<Progress.Count&&Level&&LevelIndex==index){if(Flat)FlatStagePage(face,right);else StagePage(face,right);}
            else if(index<Progress.Count)ChapterPage(index,face,right,live);
            else EndingPage(face,right);
        }
        void TitlePage(Transform face,bool right,bool live)
        {
            int found=Progress.SolvedCount;bool all=Progress.Continue<0;
            if(!right){pages.TitlePage(face,found==0?"Every pile on this table hides a picture in its shadow.":all?"Every memory is found.":$"{found} of {Progress.Count} memories found.");return;}
            float m=MemoryPages.Middle;
            pages.Text(face,"Turn the pile until the shadow remembers.",m,.86f,.03f,.9f,.08f,MemoryPages.Faded,TMPro.FontStyles.Italic);
            Tile(face,live,"title/play",found==0?"PLAY":all?"PLAY AGAIN":"CONTINUE","play",m-.2f,.52f,Play);
            Tile(face,live,"title/levels","MEMORIES","levels",m+.2f,.52f,()=>ShowLevels(0));
            pages.Text(face,Flat?"Pinch a page's outer edge and turn it over.":"Pinch a page's bottom corner and draw it across to turn it.",m,.13f,.024f,.9f,.06f,MemoryPages.Faded,TMPro.FontStyles.Italic);
            if(live)Aim(face);
        }
        // A chapter as the contents show it: number, name and picture on the left; riddle and PLAY on the right.
        void ChapterPage(int index,Transform face,bool right,bool live)
        {
            var level=Torch2Level.Load(MemoryProgress.Levels[index]);if(!level)return;
            bool solved=Progress.Solved(index),locked=!Progress.Unlocked(index);float m=MemoryPages.Middle;var accent=Color.Lerp(level.accent,MemoryPages.Ink,.35f);
            if(!right)
            {
                pages.Text(face,"CHAPTER  "+MemoryPages.Roman(index+1),m,.93f,.026f,.8f,.05f,MemoryPages.Gilt,TMPro.FontStyles.Bold,12);
                pages.Text(face,locked?"LOCKED":level.button,m,.84f,.07f,1f,.1f,locked?MemoryUi.Dim:accent,TMPro.FontStyles.Bold,6);
                pages.Plate(face,m,.47f,.46f,solved?pages.Picture(level):null,locked?null:"?",accent);
                if(locked)pages.Icon(face,m,.47f,.14f,Icon("lock"),MemoryPages.Faded);
                pages.Text(face,solved?"Found.":locked?"Find the memory before it first.":"Not found yet.",m,.16f,.03f,.8f,.06f,MemoryPages.Faded,TMPro.FontStyles.Italic);
                return;
            }
            if(locked){pages.Text(face,"This page is still blank.",m,.6f,.034f,.8f,.1f,MemoryPages.Faded,TMPro.FontStyles.Italic);return;}
            pages.Text(face,level.riddle,m,.74f,.034f,.86f,.3f,MemoryPages.Ink,TMPro.FontStyles.Italic);
            Tile(face,live,"chapter/play",solved?"PLAY AGAIN":"PLAY","play",m,.36f,()=>Begin(index));
            if(live)Aim(face);
        }
        // A memory being played: the shadow falls on the page its lamp lights; the page the lamps leave dark carries the
        // chapter, the riddle, the hints and (after the second hint) the picture; with both lit they go in the margins.
        void StagePage(Transform face,bool right)
        {
            var level=Level.level;int wall=right?1:0;bool lit=Array.IndexOf(level.lamps,wall)>=0,both=level.lamps.Length>1;float m=MemoryPages.Middle,w=MemoryBook.Width,h=MemoryBook.Height;
            string chapter="CHAPTER  "+MemoryPages.Roman(LevelIndex+1);var accent=Color.Lerp(level.accent,Color.white,.35f);
            string state=Current==Phase.Paused?"PAUSED":Current==Phase.Solved||Current==Phase.Leaving?"FOUND":"";
            if(!lit)
            {
                pages.Text(face,chapter,m,.93f,.024f,.8f,.05f,MemoryPages.Gilt,TMPro.FontStyles.Bold,12);
                pages.Text(face,level.button,m,.85f,.06f,1f,.09f,accent,TMPro.FontStyles.Bold,6);
                pages.Rule(face,m,.79f,.36f,MemoryPages.Gilt);
                if(Current==Phase.Solved||Current==Phase.Leaving)
                {
                    pages.Text(face,level.complete,m,.66f,.04f,.86f,.14f,MemoryPages.Pale,TMPro.FontStyles.Italic);
                    pages.Plate(face,m,.36f,.34f,pages.Picture(level),null,accent);
                    // Beside the corner that turns it (on the right page; the left page's words point across).
                    pages.Text(face,right?"Turn the page":"Turn the page on the right",right?w-.30f:m,.07f,.028f,.4f,.05f,MemoryUi.Gold,TMPro.FontStyles.Italic,0,right?TMPro.TextAlignmentOptions.Right:TMPro.TextAlignmentOptions.Center);
                    return;
                }
                if(state!="")pages.Text(face,state,m,.72f,.04f,.8f,.06f,MemoryUi.Gold,TMPro.FontStyles.Bold,14);
                pages.Text(face,level.riddle,m,.60f,.031f,.86f,.2f,MemoryPages.Pale,TMPro.FontStyles.Italic);
                if(HintStage>=1)pages.Text(face,level.hint,m,.39f,.028f,.86f,.18f,Color.Lerp(MemoryPages.Pale,MemoryUi.Gold,.4f));
                if(HintStage>=2)
                {
                    if(both)return;
                    pages.Plate(face,m,.15f,.20f,pages.Picture(level),null,accent);
                    pages.Text(face,"The picture",m+.2f,.15f,.022f,.2f,.05f,MemoryPages.Pale,TMPro.FontStyles.Italic);
                }
                return;
            }
            // A lit page: only its margins (the spot leaves the corners and the top edge dark).
            if(!both)return;
            if(!right)
            {
                pages.Text(face,chapter+"   "+level.button+(state!=""?"   "+state:""),m,h-.045f,.026f,.9f,.05f,accent,TMPro.FontStyles.Bold,8);
                if(HintStage>=1)pages.Text(face,level.hint,m,.06f,.022f,.9f,.09f,Color.Lerp(MemoryPages.Pale,MemoryUi.Gold,.4f));
            }
            else pages.Text(face,Current==Phase.Solved||Current==Phase.Leaving?level.complete:level.riddle,m,h-.06f,.022f,.9f,.09f,MemoryPages.Pale,TMPro.FontStyles.Italic);
            if(HintStage>=2)pages.Plate(face,right?w-.12f:.12f,h-.22f,.16f,pages.Picture(level,Array.IndexOf(level.lamps,wall)),null,accent);
        }
        // A memory being played in the flat book (no shadows on its pages): the chapter and its picture plate on the left
        // (a ? until found); the state, the riddle (the found line once solved), the hints and, after the second, the
        // picture on the right, and once solved where to turn the page.
        void FlatStagePage(Transform face,bool right)
        {
            var level=Level.level;float m=MemoryPages.Middle,w=MemoryBook.Width;var accent=Color.Lerp(level.accent,MemoryPages.Ink,.35f);
            bool found=Current==Phase.Solved||Current==Phase.Leaving;
            if(!right)
            {
                pages.Text(face,"CHAPTER  "+MemoryPages.Roman(LevelIndex+1),m,.93f,.026f,.8f,.05f,MemoryPages.Gilt,TMPro.FontStyles.Bold,12);
                pages.Text(face,level.button,m,.84f,.07f,1f,.1f,accent,TMPro.FontStyles.Bold,6);
                pages.Rule(face,m,.77f,.36f,MemoryPages.Gilt);
                pages.Plate(face,m,.40f,.44f,found?pages.Picture(level):null,"?",accent);
                return;
            }
            string state=Current==Phase.Paused?"PAUSED":found?"FOUND":"";
            if(state!="")pages.Text(face,state,m,.90f,.04f,.8f,.06f,MemoryUi.Gold,TMPro.FontStyles.Bold,14);
            pages.Text(face,found?level.complete:level.riddle,m,.74f,.034f,.86f,.22f,MemoryPages.Ink,TMPro.FontStyles.Italic);
            if(HintStage>=1&&!found)pages.Text(face,level.hint,m,.50f,.03f,.86f,.18f,Color.Lerp(MemoryPages.Ink,MemoryPages.Gilt,.45f));
            if(HintStage>=2&&!found)
            {
                pages.Plate(face,m-.12f,.24f,.22f,pages.Picture(level),null,accent);
                pages.Text(face,"The picture",m+.14f,.24f,.026f,.24f,.05f,MemoryPages.Faded,TMPro.FontStyles.Italic);
            }
            if(found)pages.Text(face,"Pinch the page's edge and turn it",w-.34f,.07f,.028f,.6f,.05f,MemoryUi.Gold,TMPro.FontStyles.Italic,0,TMPro.TextAlignmentOptions.Right);
        }
        void EndingPage(Transform face,bool right)
        {
            float m=MemoryPages.Middle;
            if(!right)
            {
                if(!Finished){pages.Text(face,"Find the last memory to finish the book.",m,.55f,.034f,.8f,.12f,MemoryPages.Faded,TMPro.FontStyles.Italic);return;}
                pages.Text(face,"THANK YOU",m,.62f,.1f,1f,.14f,MemoryPages.Ink,TMPro.FontStyles.Bold,10);
                pages.Text(face,"for playing",m,.50f,.045f,.6f,.07f,MemoryPages.Gilt,TMPro.FontStyles.Italic);
                pages.Rule(face,m,.40f,.4f,MemoryPages.Gilt);
                pages.Text(face,GameTitle,m,.30f,.03f,.8f,.05f,MemoryPages.Faded,TMPro.FontStyles.Bold,12);
                return;
            }
            for(int i=0;i<Progress.Count;i++)
            {
                var level=Torch2Level.Load(MemoryProgress.Levels[i]);if(!level)continue;float u=m+(i%2==0?-.22f:.22f),v=i<2?.70f:.30f;
                pages.Plate(face,u,v,.3f,Progress.Solved(i)?pages.Picture(level):null,"?",MemoryPages.Faded);
                pages.Text(face,level.button,u,v-.19f,.022f,.4f,.04f,MemoryPages.Ink,TMPro.FontStyles.Bold,6);
            }
        }
        void Tile(Transform face,bool live,string id,string caption,string icon,float u,float v,Action action)
        {
            var tile=MemoryTile.Create(pages,face,id,caption,Icon(icon),u,v,.17f,kit,()=>{Debug.Log("SHAPE_OF_MEMORY_PRESS "+id);audioSource.PlayOneShot(click);action();},
                ()=>!Busy&&!Book.Turning,HandsFree,live);
            if(live)tiles[id]=tile;
        }
        // Rays stay drawn over a page with tiles on it.
        static void Aim(Transform face)
        {
            var zone=new GameObject("Aim zone").AddComponent<AimZone>();zone.transform.SetParent(face,false);
            zone.Box=new Bounds(new Vector3(0,0,-.05f),new Vector3(MemoryBook.Width,MemoryBook.Height,.12f));
        }

        // ---- Placement
        // The table is placed from the head at start and again after the headset's own recenter, which moves the tracking
        // origin; there is no in-game recenter (one made the table chase wherever the head had turned to press it). The
        // runtime reports a recenter through trackingOriginUpdated; a head pose that jumps further in one frame than a head
        // can move counts too, in case a runtime does not report it. Re-placed a couple of frames later, once the head pose
        // is the new one, and never while a hand holds the orb.
        void OnTrackingOriginUpdated(XRInputSubsystem input){recenterFrame=Time.frameCount+2;Debug.Log("SHAPE_OF_MEMORY_RECENTER event");}
        void FollowSystemRecenter()
        {
            var head=Player.ViewCamera.transform;var pose=new Pose(head.position,head.rotation);
            if(headSeen&&xr.HeadReady&&recenterFrame<0)
            {
                float yaw=Vector3.Angle(Vector3.ProjectOnPlane(lastHead.forward,Vector3.up),Vector3.ProjectOnPlane(pose.forward,Vector3.up));
                if(yaw>45||Vector3.Distance(lastHead.position,pose.position)>.5f){recenterFrame=Time.frameCount+2;Debug.Log($"SHAPE_OF_MEMORY_RECENTER jump yaw={yaw:0} move={Vector3.Distance(lastHead.position,pose.position):0.00}");}
            }
            lastHead=pose;headSeen=xr.HeadReady;
            if(recenterFrame>=0&&Time.frameCount>=recenterFrame&&!Busy){recenterFrame=-1;Recenter();}
        }
        public void Recenter()
        {
            if(Busy||!Player)return;
            float lift=Progress.Lift;
            if(!Player.IsXR){Stage.SetPositionAndRotation(new Vector3(0,.72f+lift,0),Quaternion.identity);Frame();return;}
            var head=Player.ViewCamera.transform;var forward=Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.1f)forward=Vector3.forward;
            Stage.SetPositionAndRotation(new Vector3(head.position.x,Player.FloorHeight+.72f+lift,head.position.z)+forward*.65f,Quaternion.LookRotation(forward));
        }
        void Raise(float amount){float before=Progress.Lift;Progress.Lift=before+amount;Stage.position+=Stage.up*(Progress.Lift-before);if(!Player.IsXR)Frame();}
        // Desktop: the book, the orb and the console in one view (from higher and nearer over the flat book).
        void Frame()
        {
            var view=Player.ViewCamera.transform;
            view.position=Stage.TransformPoint(Flat?new Vector3(0,.95f,-1.2f):new Vector3(0,1.2f,-2.05f));view.LookAt(Stage.TransformPoint(Flat?new Vector3(0,.08f,-.12f):new Vector3(0,.42f,.28f)));
        }

        // ---- Console
        void BuildConsole()
        {
            // In front of the standing book's right page; beside the flat book, which takes the front of the table, and
            // behind the right lamp so the lamp is not in the way.
            console=new GameObject("Console").transform;console.SetParent(Stage,false);console.localPosition=Flat?new Vector3(.62f,0,-.18f):new Vector3(.25f,0,-.33f);
            slab=MemoryUi.Shape(PrimitiveType.Cube,"Console slab",console,new Vector3(0,.007f,0),Quaternion.identity,new Vector3(.3f,.014f,.14f),Surface,MemoryUi.Slate);
            consoleZone=console.gameObject.AddComponent<AimZone>();
            Spec Lower()=>new Spec("lower","LOWER","down",()=>Raise(-Lift));Spec Higher()=>new Spec("raise","RAISE","up",()=>Raise(Lift));
            Set("title",2,Lower(),Higher());
            Set("levels",3,new Spec("title","TITLE","home",ShowTitle),Lower(),Higher());
            Set("play",3,new Spec("hint","HINT","hint",Hint,()=>Current==Phase.Playing&&HintStage<MemoryProgress.MaxHints),
                new Spec("reset","HOLD TO RESET","reset",ResetOrb,()=>Current==Phase.Playing&&Level&&!Level.Completed,HoldToReset),
                new Spec("menu","MENU","menu",Pause,()=>Current==Phase.Playing));
            Set("pause",3,new Spec("resume","RESUME","play",Resume),new Spec("restart","RESTART","replay",Replay),new Spec("levels","CONTENTS","levels",Contents),
                new Spec("title","TITLE","home",()=>Leave(ShowTitle)),Lower(),Higher());
            Set("solved",2,new Spec("replay","REPLAY","replay",Replay),new Spec("levels","CONTENTS","levels",Contents));
            Set("ending",1,new Spec("title","TITLE","home",ShowTitle));
            buttons["play/hint"].Pips(MemoryProgress.MaxHints);
        }
        // Up to columns per row, first row furthest from the player; each button's name sits in front of its cap.
        void Set(string name,int columns,params Spec[] specs)
        {
            var set=new GameObject("Buttons "+name);set.transform.SetParent(console,false);set.SetActive(false);sets[name]=set;
            int rows=(specs.Length+columns-1)/columns;setSizes[name]=new Vector2Int(Mathf.Min(columns,specs.Length),rows);
            for(int i=0;i<specs.Length;i++)
            {
                int row=i/columns,inRow=Mathf.Min(columns,specs.Length-row*columns),column=i%columns;var spec=specs[i];
                var position=new Vector3((column-(inRow-1)*.5f)*Pitch,.014f,((rows-1)*.5f-row)*RowPitch+.008f);
                string id=name+"/"+spec.id;
                var button=MemoryButton.Create(set.transform,spec.id,spec.caption,Icon(spec.icon),position,kit,()=>{Debug.Log("SHAPE_OF_MEMORY_PRESS "+id);audioSource.PlayOneShot(click);spec.action();},
                    ()=>!Busy&&!Book.Turning&&(spec.enabled==null||spec.enabled()),HandsFree,spec.hold);
                buttons[name+"/"+spec.id]=button;
            }
        }
        // null hides every button and the slab (intro, reveal, outro, page turns the game makes).
        void Show(string name)
        {
            foreach(var pair in sets)pair.Value.SetActive(pair.Key==name);ShownSet=name;slab.SetActive(name!=null);consoleZone.enabled=name!=null;
            if(name=="play")buttons["play/hint"].PipsLit=MemoryProgress.MaxHints-HintStage;
            if(name!=null)
            {
                var size=setSizes[name];slab.transform.localScale=new Vector3(size.x*Pitch+.03f,.014f,size.y*RowPitch+.02f);
                // The buttons and a margin round them, up to a hand's height over the caps.
                consoleZone.Box=new Bounds(new Vector3(0,.06f,0),new Vector3(slab.transform.localScale.x+.08f,.14f,slab.transform.localScale.z+.08f));
            }
        }
        public MemoryButton Button(string set,string id)=>buttons.TryGetValue(set+"/"+id,out var button)?button:null;
        Texture2D Icon(string name){foreach(var icon in Icons)if(icon&&icon.name==name)return icon;return null;}
        // Hands may press a button unless one of them is pinching at the orb: that pinch belongs to the pile.
        bool HandsFree()
        {
            if(!xr.HeadReady)return false;
            if(!Level||Current!=Phase.Playing)return true;
            for(int hand=0;hand<2;hand++)if(xr.ReadHand(hand==0,out _,out var tip,out float pinch)&&pinch<.03f&&Level.input.Near(tip))return false;
            return true;
        }
        // During play Torch2Input clicks buttons on the desktop; elsewhere (title, contents, paused) this does.
        void DesktopClick()
        {
            if(Player.IsXR||(Level&&Level.input.enabled))return;
            var mouse=Mouse.current;if(mouse==null||!mouse.leftButton.wasPressedThisFrame)return;
            if(Physics.Raycast(Player.ViewCamera.ScreenPointToRay(mouse.position.ReadValue()),out var hit,10)&&hit.collider.GetComponent<ShadowControl>() is ShadowControl control)control.Press();
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // N: next level, locked or not. K: solve the current level where it stands.
        void DevelopmentKeys()
        {
            var keyboard=Keyboard.current;if(keyboard==null)return;
            if(keyboard.nKey.wasPressedThisFrame&&!Book.Turning)Load(LevelIndex<0?0:(LevelIndex+1)%Progress.Count,true);
            if(keyboard.kKey.wasPressedThisFrame&&Current==Phase.Intro)FinishIntro();
            else if(keyboard.kKey.wasPressedThisFrame&&Current==Phase.Playing&&Level.BeginHold(7)){Level.Hold(7,Level.Position,Level.Projection.Solution);Level.EndHold(7);}
        }
#endif

        // ---- Rooms
        // The book's rooms: a table deep and wide enough for the book. Standing, the book is the walls' frame and their
        // dark is the dimmed paper; lying flat, the walls keep their own frames and colour.
        void Style(Torch2Room room)
        {
            if(!Flat)
            {
                foreach(var frame in room.frames)if(frame)frame.gameObject.SetActive(false);
                var dark=MemoryBook.PaperTone*Dimmed;dark.a=1;room.Base=dark;
            }
            room.Table.transform.localPosition=new Vector3(0,-.035f,.21f);room.Table.transform.localScale=new Vector3(1.78f,.06f,1.34f);
        }
        // Table and lamps between memories; the standing book's pages show through (no walls).
        void IdleRoom(bool lamps)
        {
            if(!idleRoom)
            {
                idleRoom=new GameObject("Book room").transform;idleRoom.SetParent(Stage,false);
                idle=new Torch2Room(idleRoom,Surface,Screen,Glow,new[]{0,1},roomKit.Owned);Style(idle);
                if(!Flat)for(int k=0;k<2;k++){if(idle.wallQuads[k])idle.wallQuads[k].gameObject.SetActive(false);if(idle.shadowQuads[k])idle.shadowQuads[k].gameObject.SetActive(false);}
            }
            idle.Lamps(lamps?1:0);
        }
        void DestroyIdleRoom(){if(idleRoom){Destroy(idleRoom.gameObject);idleRoom=null;idle=null;}roomKit.Release();}
        void ClearLevel(){if(Level){Destroy(Level.gameObject);Level=null;}Intro=null;Guide=null;LevelIndex=-1;HintStage=0;}
        void IShadowHost.LoadLevel(string level)=>Load(MemoryProgress.IndexOf(level));
        void OnDestroy(){foreach(var input in inputs)input.trackingOriginUpdated-=OnTrackingOriginUpdated;kit.Release();roomKit.Release();}
    }
}

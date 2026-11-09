using System.Collections;
using System.Linq;
using CloudGarden.ShadowPuzzle;
using NUnit.Framework;
using Oculus.Interaction;
using PhysicalAssembly.MetaXR;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShapeOfMemory.Tests
{
    // Drives the deliverable scene the way hands do: pointer events on each button's, tile's and page corner's ray
    // interactable (MetaMenuButton). A tapped corner turns its page by itself, as a quick pinch does. Runs for both book
    // layouts.
    [TestFixture(MemoryBook.Layout.Standing)]
    [TestFixture(MemoryBook.Layout.Flat)]
    public class MemoryGamePlayTests
    {
        const string Scene="Assets/ShadowWorkshop/PicoFinal/Scenes/PicoFinal.unity";
        static readonly string[] DebugWords={"WARMTH","RESET x2","MAIN MENU","PREV","NEXT LEVEL","THIS WALL","LOWER","RAISE"};
        readonly MemoryBook.Layout layout;
        int pointer=4000;
        public MemoryGamePlayTests(MemoryBook.Layout layout){this.layout=layout;}

        IEnumerator Open(System.Action<MemoryGame> found,MemoryProgress.IStore store=null)
        {
            MemoryGame.StoreOverride=store??new MemoryProgress.InMemory();MemoryGame.LayoutOverride=layout;
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(Scene,new LoadSceneParameters(LoadSceneMode.Single));
#endif
            MemoryGame game=null;float deadline=Time.realtimeSinceStartup+10;
            while((!game||!game.Ready)&&Time.realtimeSinceStartup<deadline){game=Object.FindFirstObjectByType<MemoryGame>();yield return null;}
            Assert.IsTrue(game&&game.Ready,"the game starts");Assert.AreEqual(layout,game.Book.Mode);found(game);
            yield return Settle(game);
        }
        [TearDown] public void Clear(){MemoryGame.StoreOverride=null;MemoryGame.LayoutOverride=null;}
        // A hand's ray pinch: select, a frame, release. Actions run on the next Update.
        IEnumerator Tap(Pressable press,string what)
        {
            Assert.NotNull(press,what);Assert.IsTrue(press.Available,what+" is available");
            var ray=press.Native.Ray;var pose=new Pose(press.transform.position,Quaternion.identity);int id=pointer++;
            ray.PublishPointerEvent(new PointerEvent(id,PointerEventType.Hover,pose));ray.PublishPointerEvent(new PointerEvent(id,PointerEventType.Select,pose));
            yield return null;
            ray.PublishPointerEvent(new PointerEvent(id,PointerEventType.Unselect,pose));ray.PublishPointerEvent(new PointerEvent(id,PointerEventType.Unhover,pose));
            yield return null;yield return null;
        }
        IEnumerator Tap(MemoryButton button)=>Tap(button?button.Press:null,button?button.name:"button");
        IEnumerator Tap(MemoryTile tile)=>Tap(tile?tile.Press:null,tile?tile.name:"tile");
        // Until the book stops turning.
        static IEnumerator Settle(MemoryGame game,float seconds=6)
        {
            float deadline=Time.realtimeSinceStartup+seconds;yield return null;
            while(game.Book.Turning&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return null;Assert.IsFalse(game.Book.Turning,"the page lands");
        }
        static void AssertNoDebugText(GameObject root)
        {
            foreach(var label in root.GetComponentsInChildren<TMP_Text>(true))foreach(var word in DebugWords)
                Assert.IsFalse(label.text.Contains(word),$"debug text '{label.text}' on {label.name}");
        }
        static void Solve(Torch2Workshop level){Assert.IsTrue(level.BeginHold(7));level.Hold(7,level.Position,level.Projection.Solution);level.EndHold(7);}
        static int Plates(Transform face)=>face.GetComponentsInChildren<Transform>().Count(t=>t.name=="Plate");

        [UnityTest] public IEnumerator TitlePlaySolveAndTurnThePage()
        {
            MemoryGame game=null;var store=new MemoryProgress.InMemory();yield return Open(g=>game=g,store);
            Assert.AreEqual(MemoryGame.Phase.Title,game.Current);Assert.AreEqual(0,game.Book.Spread);Assert.AreEqual("title",game.ShownSet);
            StringAssert.Contains("SHAPE",game.PageText(false));StringAssert.Contains("MEMORY",game.PageText(false));Assert.AreEqual("PLAY",game.Tile("title/play").Caption);
            Assert.AreEqual(1,Object.FindObjectsByType<MetaPlayerRig>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length,"one player");
            yield return Tap(game.Tile("title/play"));Assert.IsTrue(game.Book.Turning,"PLAY turns to the first chapter");yield return Settle(game);
            Assert.AreEqual(MemoryGame.SpreadOf(0),game.Book.Spread);Assert.AreEqual(MemoryGame.Phase.Intro,game.Current);yield return SkipIntro(game);
            Assert.AreEqual(MemoryGame.Phase.Playing,game.Current);Assert.AreEqual("Birthday",game.Level.level.name);Assert.IsTrue(game.Level.bare);
            AssertNoDebugText(game.Level.gameObject);Assert.IsEmpty(game.Level.world.controls,"no debug buttons in the room");
            Assert.IsFalse(game.Level.GetComponentsInChildren<Transform>(true).Any(t=>t.name.StartsWith("Picture card")),"pictures are printed in the book, not stood by the lamps");
            // Birthday lights the left page; the right one carries the chapter.
            StringAssert.Contains(game.Level.level.riddle,game.PageText(true));Assert.AreEqual(2,game.Button("play","hint").PipsLit);
            yield return Tap(game.Button("play","hint"));Assert.AreEqual(1,game.HintStage);StringAssert.Contains(game.Level.level.hint,game.PageText(true));Assert.AreEqual(0,Plates(game.Book.RightFace));
            yield return Tap(game.Button("play","hint"));Assert.AreEqual(2,game.HintStage);Assert.AreEqual(1,Plates(game.Book.RightFace),"the picture is printed on the page");
            Assert.IsFalse(game.Button("play","hint").Press.Available,"two hints only");Assert.AreEqual(0,game.Button("play","hint").PipsLit);
            Solve(game.Level);yield return WaitFor(game,MemoryGame.Phase.Solved,3);
            Assert.IsNull(game.ShownSet,"buttons wait for the reveal");StringAssert.Contains(game.Level.level.complete.Trim(),game.PageText(true));
            Assert.IsTrue(game.Progress.Solved(0));Assert.AreEqual(2,game.Progress.Hints(0));
            yield return new WaitForSecondsRealtime(MemoryGame.RevealPause+.2f);Assert.AreEqual("solved",game.ShownSet);
            Assert.IsNull(game.Button("solved","next"),"the next memory is a page turn, not a button");
            yield return Tap(game.Book.Corner(true),"the solved page's corner");Assert.AreEqual(MemoryGame.Phase.Leaving,game.Current,"the outro plays first");
            yield return WaitFor(game,MemoryGame.Phase.Intro,8);Assert.AreEqual(MemoryGame.SpreadOf(1),game.Book.Spread);yield return SkipIntro(game);
            Assert.AreEqual("BonVoyage",game.Level.level.name);Assert.AreEqual(0,game.HintStage);
            // A new session reads the same store: one found, CONTINUE goes to the next memory.
            yield return Open(g=>game=g,store);Assert.AreEqual("CONTINUE",game.Tile("title/play").Caption);
            yield return Tap(game.Tile("title/play"));yield return Settle(game);Assert.AreEqual("BonVoyage",game.Level.level.name);
        }
        [UnityTest] public IEnumerator ResetNeedsAFullHold()
        {
            MemoryGame game=null;yield return Open(g=>game=g);game.Load(0);yield return SkipIntro(game);
            var level=game.Level;var turned=Quaternion.AngleAxis(60,Vector3.up)*level.Projection.Start;
            Assert.IsTrue(level.BeginHold(7));level.Hold(7,level.Position,turned);level.EndHold(7);
            var button=game.Button("play","reset");var ray=button.Press.Native.Ray;var pose=new Pose(button.transform.position,Quaternion.identity);
            ray.PublishPointerEvent(new PointerEvent(77,PointerEventType.Hover,pose));ray.PublishPointerEvent(new PointerEvent(77,PointerEventType.Select,pose));
            yield return new WaitForSecondsRealtime(.4f);Assert.Greater(button.Press.Held,.2f);
            ray.PublishPointerEvent(new PointerEvent(77,PointerEventType.Unselect,pose));yield return new WaitForSecondsRealtime(.8f);
            Assert.AreEqual(0,Quaternion.Angle(turned,level.Rotation),.01f,"a short press does nothing");
            ray.PublishPointerEvent(new PointerEvent(78,PointerEventType.Select,pose));yield return new WaitForSecondsRealtime(MemoryGame.HoldToReset+.3f);
            ray.PublishPointerEvent(new PointerEvent(78,PointerEventType.Unselect,pose));
            Assert.AreEqual(0,Quaternion.Angle(level.Projection.Start,level.Rotation),.01f,"held long enough: back to the start");
        }
        [UnityTest] public IEnumerator PauseFreezesTheOrbAndTheContentsHonourLocks()
        {
            MemoryGame game=null;yield return Open(g=>game=g);game.Load(0);yield return SkipIntro(game);
            yield return Tap(game.Button("play","menu"));
            Assert.AreEqual(MemoryGame.Phase.Paused,game.Current);Assert.IsFalse(game.Level.input.enabled);StringAssert.Contains("PAUSED",game.PageText(true));
            float height=game.Stage.position.y;yield return Tap(game.Button("pause","raise"));
            Assert.AreEqual(height+MemoryGame.Lift,game.Stage.position.y,1e-4f);Assert.AreEqual(MemoryGame.Lift,game.Progress.Lift,1e-4f);
            yield return Tap(game.Button("pause","resume"));Assert.AreEqual(MemoryGame.Phase.Playing,game.Current);Assert.IsTrue(game.Level.input.enabled);
            yield return Tap(game.Button("play","menu"));yield return Tap(game.Button("pause","levels"));
            yield return WaitFor(game,MemoryGame.Phase.Levels);Assert.IsNull(game.Level);Assert.AreEqual(MemoryGame.SpreadOf(0),game.Book.Spread);
            Assert.IsTrue(CloudGarden.Integration.AimZone.Active.Any(z=>z.transform.IsChildOf(game.Book.RightFace)),"rays stay drawn over the chapter's page");
            Assert.NotNull(game.Tile("chapter/play"));
            // The next chapter is locked: its page has no PLAY.
            yield return Tap(game.Book.Corner(true),"the forward corner");yield return Settle(game);
            Assert.AreEqual(MemoryGame.SpreadOf(1),game.Book.Spread);Assert.AreEqual(MemoryGame.Phase.Levels,game.Current);
            StringAssert.Contains("LOCKED",game.PageText(false));Assert.IsNull(game.Tile("chapter/play"),"locked memories do not open");
            yield return Tap(game.Book.Corner(false),"the back corner");yield return Settle(game);Assert.AreEqual(MemoryGame.SpreadOf(0),game.Book.Spread);
            yield return Tap(game.Tile("chapter/play"));
            Assert.AreEqual(MemoryGame.Phase.Intro,game.Current);Assert.AreEqual("Birthday",game.Level.level.name);
            // Back on the title page, the first page's back corner is gone.
            yield return SkipIntro(game);yield return Tap(game.Button("play","menu"));yield return Tap(game.Button("pause","title"));
            yield return WaitFor(game,MemoryGame.Phase.Title);yield return Settle(game);Assert.AreEqual(0,game.Book.Spread);Assert.IsFalse(game.Book.Corner(false).isActiveAndEnabled);
        }
        [UnityTest] public IEnumerator IntroBuildsTheOrbThenLightsTheLampsAndTheGuideTeachesOnce()
        {
            MemoryGame game=null;var store=new MemoryProgress.InMemory();yield return Open(g=>game=g,store);
            game.Load(0);var intro=game.Intro;intro.Frozen=true;intro.Seek(0);var level=game.Level;var world=level.world;
            Assert.AreEqual(MemoryGame.Phase.Intro,game.Current);Assert.IsNull(game.ShownSet,"no buttons during the intro");
            Assert.AreEqual(0,CloudGarden.Integration.AimZone.Active.Count,"nothing to aim at during the intro");
            Assert.IsFalse(intro.Skippable,"the first intro plays through");Assert.IsFalse(level.input.enabled);
            Assert.AreEqual(0,world.LampLevel);Assert.AreEqual(0,world.Cue.Level);Assert.IsTrue(world.OrbParts.All(p=>!p.gameObject.activeSelf),"no orb yet");
            foreach(var piece in world.Pieces){Assert.Less(level.transform.InverseTransformPoint(piece.position).y,.12f,piece.name+" lies on the table");Assert.AreEqual(Vector3.zero,piece.localScale,piece.name+" grows in");}
            intro.Seek(intro.Length*.5f);Assert.IsTrue(world.Pieces.Any(p=>level.transform.InverseTransformPoint(p.position).y>.15f),"halfway, some have risen");
            Assert.AreEqual(0,world.LampLevel,"lamps stay off until the orb is built");
            intro.Frozen=false;float deadline=Time.realtimeSinceStartup+intro.Length+2;
            while(game.Current==MemoryGame.Phase.Intro&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.AreEqual(MemoryGame.Phase.Playing,game.Current);Assert.AreEqual("play",game.ShownSet);Assert.IsTrue(level.input.enabled);
            Assert.AreEqual(1,world.LampLevel);Assert.AreEqual(1,world.Cue.Level);Assert.IsTrue(world.OrbParts.All(p=>p.gameObject.activeSelf));
            for(int i=0;i<world.Pieces.Length;i++)
            {
                var piece=level.level.pieces[i];var t=world.Pieces[i];
                Assert.Less(Vector3.Distance(piece.position,t.localPosition),1e-4f,piece.label+" in its place");Assert.AreEqual(piece.Scale,t.localScale,piece.label+" full size");Assert.Less(Quaternion.Angle(Quaternion.Euler(piece.euler),t.localRotation),.05f,piece.label+" turned right");
            }
            Assert.IsTrue(game.Progress.SeenIntro(0));Assert.AreEqual(0,Quaternion.Angle(level.Projection.Start,level.Rotation),.01f,"the orb starts at the start pose");
            // The first level teaches the roll until the orb has turned 30 degrees.
            Assert.IsTrue(game.Guide.Teaching);yield return null;
            Assert.IsTrue(level.GetComponentsInChildren<Transform>().Any(t=>t.name=="Ghost pinch"&&t.gameObject.activeInHierarchy),"ghost pinch shows");
            Assert.IsTrue(level.BeginHold(7));level.Hold(7,level.Position,Quaternion.AngleAxis(40,Vector3.up)*level.Rotation);level.EndHold(7);
            yield return null;Assert.IsFalse(game.Guide.Teaching);Assert.IsTrue(game.Progress.Taught);
            // Seen once: the next time it can be skipped, and the guide stays away.
            game.Load(0);Assert.IsTrue(game.Intro.Skippable);yield return SkipIntro(game);Assert.IsFalse(game.Guide.Teaching);
        }
        [UnityTest] public IEnumerator HomeEndsTheBookThroughTheOutro()
        {
            MemoryGame game=null;var store=new MemoryProgress.InMemory();yield return Open(g=>game=g,store);
            game.Load(3,true);yield return SkipIntro(game);Assert.AreEqual("Home",game.Level.level.name);Assert.AreEqual(MemoryGame.SpreadOf(3),game.Book.Spread);
            // Both pages are lit: the words go in the margins.
            StringAssert.Contains("HOME",game.PageText(false));StringAssert.Contains(game.Level.level.riddle,game.PageText(true));
            Solve(game.Level);yield return WaitFor(game,MemoryGame.Phase.Solved);
            yield return new WaitForSecondsRealtime(MemoryGame.RevealPause+.2f);Assert.AreEqual("solved",game.ShownSet);
            var level=game.Level;var world=level.world;
            yield return Tap(game.Book.Corner(true),"the solved page's corner");
            Assert.AreEqual(MemoryGame.Phase.Leaving,game.Current);Assert.IsFalse(level.input.enabled);Assert.IsNull(game.ShownSet);
            yield return new WaitForSecondsRealtime(MemoryOutro.Dim+.1f);
            Assert.AreEqual(0,world.LampLevel,.01f,"the lamps go out first");Assert.AreEqual(0,world.Cue.Level,.01f);
            yield return WaitFor(game,MemoryGame.Phase.Ending,8);yield return Settle(game);Assert.IsNull(game.Level);Assert.AreEqual("ending",game.ShownSet);
            Assert.AreEqual(game.EndingSpread,game.Book.Spread);StringAssert.Contains("THANK YOU",game.PageText(false));Assert.AreEqual(4,Plates(game.Book.RightFace),"every memory's picture");
            Assert.IsFalse(game.Book.Corner(true).isActiveAndEnabled,"the last page");
            yield return Tap(game.Button("ending","title"));Assert.AreEqual(MemoryGame.Phase.Title,game.Current);yield return Settle(game);Assert.AreEqual(0,game.Book.Spread);
        }
        // The flat book, by hand: a pinch at the right page's edge lifts the page by that point and it follows the hand round
        // the spine; let go short of upright it falls back, carried over it turns. The back page's edge turns it back.
        [UnityTest] public IEnumerator FlatPagesFollowTheHandThatHoldsTheirEdge()
        {
            if(layout!=MemoryBook.Layout.Flat){Assert.Pass("flat book only");yield break;}
            MemoryGame game=null;yield return Open(g=>game=g);var book=game.Book;Assert.AreEqual(0,book.Spread);
            float w=MemoryBook.Width,h=MemoryBook.Height;
            // Book space: the spine's near foot at the origin, the right page along +x, up the page +z, the pages facing +y.
            Vector3 At(float angleDegrees,float radius,float up){float a=angleDegrees*Mathf.Deg2Rad;return book.transform.TransformPoint(new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,up));}
            Assert.AreEqual(-1,book.EdgeAt(At(0,w*.5f,h*.5f)),"the middle of the page is not its edge");
            Assert.AreEqual(1,book.EdgeAt(At(2,w,h*.3f)),"the right page's edge, anywhere along it");
            Assert.AreEqual(1,book.EdgeAt(At(2,w,h*.9f)));
            Assert.IsTrue(book.Grab(At(2,w*.98f,h*.5f)),"a pinch at the edge takes the page");
            Assert.IsTrue(book.Turning&&book.Dragging,"pinched at the edge, the page is held");
            // Lifted to 60°: the page comes up with the hand; let go, it falls back.
            for(int i=1;i<=20;i++){book.Drag(At(60f*i/20,w*.95f,h*.5f));yield return null;}
            yield return new WaitForSecondsRealtime(.4f);
            book.Drop();yield return Settle(game);Assert.AreEqual(0,book.Spread,"let go short of upright, it falls back");
            // Carried over to 140°: it turns.
            Assert.IsTrue(book.Grab(At(1,w,h*.2f)));
            for(int i=1;i<=30;i++){book.Drag(At(140f*i/30,w*.95f,h*.2f));yield return null;}
            yield return new WaitForSecondsRealtime(.3f);book.Drop();yield return Settle(game);
            Assert.AreEqual(1,book.Spread,"carried over, it turns");Assert.AreEqual(MemoryGame.Phase.Levels,game.Current,"a turned page opens the contents");
            // The left page's edge turns back; a quick pinch there turns it by itself.
            Assert.AreEqual(0,book.EdgeAt(At(178,w,h*.5f)));
            Assert.IsTrue(book.Grab(At(178,w,h*.5f)));book.Drop();yield return Settle(game);Assert.AreEqual(0,book.Spread,"a quick pinch turns the page back");
        }
        static IEnumerator SkipIntro(MemoryGame game){game.FinishIntro();yield return null;yield return null;}
        static IEnumerator WaitFor(MemoryGame game,MemoryGame.Phase phase,float seconds=6)
        {
            float deadline=Time.realtimeSinceStartup+seconds;while(game.Current!=phase&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.AreEqual(phase,game.Current);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PhysicalAssembly;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    // Headset test 3 (2026-11-22), the between-level screens: no panel is hidden by the tilted board, the upgrade tiles show
    // rendered pictures instead of live models, and the UPGRADES buttons carry a star while an upgrade is affordable.
    // Writes captures and numbers to Logs/EmberdeepUpgradeScreens/ (rewritten each run).
    public sealed class EmberdeepUpgradeScreensPlayTests
    {
        const string Output="Logs/EmberdeepUpgradeScreens";
        static readonly CultureInfo C=CultureInfo.InvariantCulture;
        static readonly EmberdeepFirstHints.Move[] Moves=(EmberdeepFirstHints.Move[])System.Enum.GetValues(typeof(EmberdeepFirstHints.Move));
        static readonly string[] Screens={"Pause panel","Results panel","Level select","Upgrades panel"};
        int[] hints;EmberdeepSettingsPrefs prefs;
        [SetUp] public void Save()
        {
            hints=Moves.Select(m=>PlayerPrefs.GetInt(EmberdeepFirstHints.Key(m),-1)).ToArray();
            prefs=EmberdeepSettingsPrefs.Save();
            PlayerPrefs.DeleteKey(EmberdeepPlayArea.HeightKey);PlayerPrefs.DeleteKey(EmberdeepPlayArea.DistanceKey);PlayerPrefs.DeleteKey(EmberdeepPlayArea.TiltedKey);
            EmberdeepPlayArea.Reload();Directory.CreateDirectory(Output);
        }
        [TearDown] public void Restore()
        {
            for(int i=0;i<Moves.Length;i++)if(hints[i]<0)PlayerPrefs.DeleteKey(EmberdeepFirstHints.Key(Moves[i]));else PlayerPrefs.SetInt(EmberdeepFirstHints.Key(Moves[i]),hints[i]);
            prefs.Restore();EmberdeepPlayArea.Reload();
        }
        static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
        static void Clear(FireRun run){foreach(var a in run.Animals)run.ApplyAnimalWater(a.Position,100,.6f);foreach(var t in run.Trees)if(t.State==TreeState.Burning)run.ApplyWater(t.Position,1000,.1f);}
        static void AdvanceTo(FireRun run,int stage)
        {
            for(int guard=0;guard<20 && run.Stage<stage;guard++)
            {
                if(run.Phase==FirePhase.Ready && !run.StartStage(true))break;
                EmberdeepTestFlow.StartPrepared(run);
                for(int i=0;i<400 && run.Phase==FirePhase.Fighting;i++){Clear(run);run.Tick(.05f,false);}
                if(run.Phase!=FirePhase.Cleared)break;
                for(int i=0;i<400 && run.AnimalsSaved<run.Animals.Length;i++)run.Tick(.05f,false);
                if(run.NeedsMagicUpgrade && !run.BuyMagicUpgrade())break;
                if(!run.StartStage(true))break;
            }
        }
        static IEnumerator Load(int stage)
        {
            EmberdeepSession.NewExpedition();AdvanceTo(EmberdeepSession.Run,stage);Assert.AreEqual(stage,EmberdeepSession.Run.Stage);
            yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);yield return null;yield return null;
            Object.FindFirstObjectByType<FireRescueGame>().SendMessage("OnApplicationFocus",true);
            yield return Wait(1.2f); // the rim re-measures the plinth every second
        }
        static WorkshopButton Button(FireRescueGame game,string command)=>game.GetComponentsInChildren<WorkshopButton>(true).Single(b=>b.Command==command);
        static float Down(Vector3 eye,Vector3 p)=>Mathf.Atan2(eye.y-p.y,Vector3.ProjectOnPlane(p-eye,Vector3.up).magnitude)*Mathf.Rad2Deg;
        static void Note(string text){File.AppendAllText(Output+"/numbers.txt",text+"\n");Debug.Log("EMBERDEEP_UPGRADE_SCREENS "+text);}
        static string Path(Transform t){var s=t.name;for(var p=t.parent;p && p.name!="Arena" && p.parent;p=p.parent)s=p.name+"/"+s;return s;}
        static void SetCoins(FireRun run,int coins)=>typeof(FireRun).GetProperty("Coins").SetValue(run,coins);
        // From the seated eye, looking at a point (pitch only, along the board's heading), headset-like field of view.
        static void Capture(FireRescueGame game,string name,Vector3 look,float fov=92)
        {
            var camera=game.Player.ViewCamera;var eye=camera.transform.position;
            var rotation=camera.transform.rotation;float priorFov=camera.fieldOfView;var prior=camera.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(1200,1100,24,RenderTextureFormat.ARGB32){antiAliasing=4};var texture=new Texture2D(1200,1100,TextureFormat.RGBA32,false);
            try
            {
                var flat=Vector3.ProjectOnPlane(game.Arena.forward,Vector3.up).normalized;
                var dir=look-eye;var side=Vector3.Cross(Vector3.up,flat);dir-=side*Vector3.Dot(dir,side); // no yaw: the seat faces the board
                camera.transform.rotation=Quaternion.LookRotation(dir.normalized,Vector3.up);camera.fieldOfView=fov;
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1200,1100),0,0);texture.Apply();
                File.WriteAllBytes(Output+"/"+name+".png",texture.EncodeToPNG());
            }
            finally{camera.transform.rotation=rotation;camera.fieldOfView=priorFov;camera.targetTexture=prior;RenderTexture.active=active;target.Release();Object.Destroy(target);Object.Destroy(texture);}
        }
        // Zoomed: straight at the point (yaw too), from the seat, on a camera of its own.
        static void CaptureAt(FireRescueGame game,string name,Vector3 look,float fov)
        {
            var view=game.Player.ViewCamera;var from=view.transform.position;var go=new GameObject("Capture camera");var camera=go.AddComponent<Camera>();
            var target=new RenderTexture(1200,1100,24,RenderTextureFormat.ARGB32){antiAliasing=4};var texture=new Texture2D(1200,1100,TextureFormat.RGBA32,false);var active=RenderTexture.active;
            try
            {
                camera.CopyFrom(view);camera.nearClipPlane=.005f;camera.fieldOfView=fov;
                go.transform.SetPositionAndRotation(from,Quaternion.LookRotation(look-from,Vector3.up));
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1200,1100),0,0);texture.Apply();
                File.WriteAllBytes(Output+"/"+name+".png",texture.EncodeToPNG());
            }
            finally{RenderTexture.active=active;camera.targetTexture=null;target.Release();Object.Destroy(target);Object.Destroy(texture);Object.Destroy(go);}
        }
        static Texture2D Read(Texture texture)
        {
            var rt=(RenderTexture)texture;var active=RenderTexture.active;var copy=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);
            try{RenderTexture.active=rt;copy.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);copy.Apply();}finally{RenderTexture.active=active;}
            return copy;
        }

        // ---- 1. The tilted board hides no between-level panel -------------------------------------------------------
        // Everything solid the board shows from the seat: every visible renderer under the arena except the screens themselves
        // and see-through effects (every material in the transparent queues: the creek and forest mist and streaks, the outcome
        // veil; the rim strip draws there too but is a solid board, so it counts), as mesh vertices where the mesh is readable
        // (the terrain), else its mesh bounds' corners (conservative), else its bounds.
        static List<(Vector3 p,string name)> BoardPoints(FireRescueGame game,out int readable,out int boxed)
        {
            var points=new List<(Vector3,string)>();readable=boxed=0;
            var roots=Screens.Select(n=>game.Arena.Find(n)).Where(t=>t).ToArray();var rim=game.RimHud?game.RimHud.Root:null;
            foreach(var r in game.Arena.GetComponentsInChildren<Renderer>())
            {
                if(!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)continue;
                if(roots.Any(root=>r.transform.IsChildOf(root)))continue;
                if(!(rim && r.transform.IsChildOf(rim)) && r.sharedMaterials.All(m=>!m || m.renderQueue>=2500))continue;
                string name=Path(r.transform);var t=r.transform;
                Mesh mesh=null;if(r is MeshRenderer){var f=r.GetComponent<MeshFilter>();if(f)mesh=f.sharedMesh;}
                if(mesh && mesh.isReadable){readable++;foreach(var v in mesh.vertices)points.Add((t.TransformPoint(v),name));continue;}
                boxed++;
                var b=mesh?mesh.bounds:r.bounds;
                for(int i=0;i<8;i++)
                {
                    var c=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    points.Add((mesh?t.TransformPoint(c):c,name));
                }
            }
            return points;
        }
        // The faces a panel shows: its boards and its button pills (the ray targets with a mesh), each the front face of its
        // local bounds, facing the eye.
        struct Face {public Vector3 C,R,U,N;public float W,H,T;public string Name;}
        static List<Face> Faces(Transform panel,Vector3 eye)
        {
            var faces=new List<Face>();
            foreach(var r in panel.GetComponentsInChildren<MeshRenderer>())
            {
                if(!r.enabled || !(r.name.EndsWith(" board") || r.GetComponent<WorkshopButton>()))continue;
                var b=r.localBounds;var t=r.transform;
                Vector3 L(float x,float y,float z)=>t.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z)));
                // The face nearer the eye.
                float front=Vector3.Distance(eye,L(0,0,-1))<Vector3.Distance(eye,L(0,0,1))?-1:1;
                Vector3 bl=L(-1,-1,front),br=L(1,-1,front),tl=L(-1,1,front),c=(bl+br+tl+L(1,1,front))*.25f;
                var n=Vector3.Cross(br-bl,tl-bl).normalized;if(Vector3.Dot(n,c-eye)<0)n=-n;
                faces.Add(new Face{C=c,R=(br-bl).normalized,U=(tl-bl).normalized,N=n,W=(br-bl).magnitude,H=(tl-bl).magnitude,T=(L(-1,-1,-front)-bl).magnitude,Name=r.name});
            }
            return faces;
        }
        // Board points that hide part of a face from the eye (between the eye and the face, inside it as seen from the eye) or
        // stand inside the panel. worst: how far into the face (degrees above its lower edge) the deepest one reaches.
        static int Hidden(List<(Vector3 p,string name)> board,List<Face> faces,Vector3 eye,out float worst,out string where)
        {
            int hidden=0;worst=0;where="";
            foreach(var f in faces)
            {
                float plane=Vector3.Dot(f.C-eye,f.N);
                foreach(var (p,name) in board)
                {
                    float depth=Vector3.Dot(p-eye,f.N);if(depth<=.01f || depth>plane+f.T)continue;
                    var q=depth<plane?eye+(p-eye)*(plane/depth):p; // through the face, or the point itself inside the panel
                    float u=Vector3.Dot(q-f.C,f.R),v=Vector3.Dot(q-f.C,f.U);
                    if(Mathf.Abs(u)>f.W*.5f || Mathf.Abs(v)>f.H*.5f)continue;
                    hidden++;float into=Mathf.Atan2(v+f.H*.5f,Vector3.Distance(eye,q))*Mathf.Rad2Deg;
                    if(into>worst){worst=into;where=$"{name} into {f.Name}";}
                }
            }
            return hidden;
        }
        // Each face's lower edge against the board's top edge under it, in degrees under eye level as seen from the seat (the
        // board's top edge: its highest point in front of that face, across the face's width). Out: the face with the least
        // room (its lower edge, the board top under it, what makes that top).
        static void Edges(List<(Vector3 p,string name)> board,List<Face> faces,Vector3 eye,Vector3 forward,out float panelBottom,out float boardTop,out string top)
        {
            var right=Vector3.Cross(Vector3.up,forward);
            float Across(Vector3 p)=>Mathf.Atan2(Vector3.Dot(p-eye,right),Vector3.Dot(p-eye,forward))*Mathf.Rad2Deg;
            panelBottom=0;boardTop=float.MaxValue;top="nothing in front";float least=float.MaxValue;
            foreach(var f in faces)
            {
                Vector3 a=f.C-f.R*f.W*.5f-f.U*f.H*.5f,b=f.C+f.R*f.W*.5f-f.U*f.H*.5f;
                float bottom=Mathf.Max(Down(eye,a),Down(eye,b)),left=Mathf.Min(Across(a),Across(b)),rightEdge=Mathf.Max(Across(a),Across(b));
                float plane=Vector3.Dot(f.C-eye,f.N),under=float.MaxValue;string what="nothing in front";
                foreach(var (p,name) in board)
                {
                    float x=Across(p);if(x<left || x>rightEdge || Vector3.Dot(p-eye,f.N)>plane)continue;
                    float d=Down(eye,p);if(d<under){under=d;what=name;}
                }
                if(under-bottom<least){least=under-bottom;panelBottom=bottom;boardTop=under;top=f.Name+" over "+what;}
            }
        }
        static void Measure(FireRescueGame game,string mode,string panelName,bool assertClear)
        {
            var eye=game.Player.ViewCamera.transform.position;var forward=Vector3.ProjectOnPlane(game.Arena.forward,Vector3.up).normalized;
            var panel=game.Arena.Find(panelName);Assert.True(panel && panel.gameObject.activeInHierarchy,panelName+" shows");
            var board=BoardPoints(game,out int readable,out int boxed);var faces=Faces(panel,eye);Assert.IsNotEmpty(faces,panelName);
            int hidden=Hidden(board,faces,eye,out float worst,out string where);
            Edges(board,faces,eye,forward,out float bottom,out float top,out string topName);
            Note(string.Format(C,"{0} {1}: centre {2:F1} deg down, {3:F3} m away; tightest lower edge {4:F1} deg, board top under it {5:F1} deg ({6}); {7} board points hide it (worst {8:F2} deg in: {9}); outlines behind {10:F1} / over {11:F1}; {12} readable meshes, {13} boxed",
                mode,panelName,Down(eye,panel.position),Vector3.Distance(eye,panel.position),bottom,top,topName,hidden,worst,where,game.OutlineBehindDown,game.OutlineOverDown,readable,boxed));
            if(assertClear)
            {
                Assert.AreEqual(0,hidden,$"{mode} {panelName}: the board hides part of it from the seat (worst {worst:F2} deg into it: {where})");
                Assert.Less(bottom,top,$"{mode} {panelName}: its lower edge sits under the board's top edge as seen from the seat ({topName})");
            }
        }
        // Flat: exactly the placement before headset test 3 (the strip / crowns floors, no outline).
        static void AssertFlatPlacement(FireRescueGame game,string panelName)
        {
            Assert.False(game.ScreensTilted);Assert.True(float.IsPositiveInfinity(game.OutlineBehindDown) && float.IsPositiveInfinity(game.OutlineOverDown));
            var board=game.RimHud.Board;var eye=game.Arena.InverseTransformPoint(game.Player.ViewCamera.transform.position);
            float strip=EmberdeepRimHud.StripTopDown(board,eye),sky=EmberdeepRimHud.SkylineDown(board,eye);
            var want=panelName switch
            {
                "Pause panel"=>FireRescueGame.ResultsAnchorFor(board,eye,FireRescueGame.PanelCentreDown(strip,FireRescueGame.PauseBelow)),
                "Results panel"=>FireRescueGame.ResultsAnchorFor(board,eye,FireRescueGame.PanelCentreDown(strip,FireRescueGame.ResultsBelow)),
                "Upgrades panel"=>FireRescueGame.ResultsAnchorFor(board,eye,FireRescueGame.PanelCentreDown(sky,FireRescueGame.UpgradesBelow)),
                _=>FireRescueGame.CarouselAnchorFor(board,eye,FireRescueGame.PanelCentreDown(sky,FireRescueGame.CarouselBelow)),
            };
            var have=game.Arena.Find(panelName).localPosition;
            Assert.Less(Vector3.Distance(want,have),2e-3f,$"Flat {panelName} moved: {have:F4} vs {want:F4}");
        }
        IEnumerator AllPanels(string mode,bool tilted)
        {
            EmberdeepPlayArea.SetTilted(tilted); // either way: tilted is the default since headset test 4
            yield return Load(3);
            var game=Object.FindFirstObjectByType<FireRescueGame>();var run=game.Run;
            Assert.AreEqual(tilted,EmberdeepPlayArea.Tilted);if(tilted)Assert.Greater(Vector3.Angle(game.Arena.up,Vector3.up),20,"The board tilts");
            yield return null;Assert.AreEqual(tilted,game.ScreensTilted);
            // Pause, in Plan.
            Assert.True(game.OpenPause());yield return null;yield return null;
            Measure(game,mode,"Pause panel",tilted);if(!tilted)AssertFlatPlacement(game,"Pause panel");
            var pause=game.Arena.Find("Pause panel");
            yield return Wait(.2f);Capture(game,"1-"+mode+"-pause",pause.position+Vector3.down*.15f);
            game.ClosePause();yield return null;
            // Level select, over the board.
            game.OpenLevelSelect(run.Stage);yield return null;yield return null;yield return Wait(.6f);
            Measure(game,mode,"Level select",tilted);if(!tilted)AssertFlatPlacement(game,"Level select");
            Capture(game,"1-"+mode+"-level-select",game.Arena.Find("Level select").position+Vector3.down*.1f);
            game.CloseLevelSelect();yield return null;
            // Results, then upgrades from its button.
            EmberdeepTestFlow.StartPrepared(run);yield return null;
            for(int i=0;i<200 && run.Phase==FirePhase.Fighting;i++){Clear(run);run.Tick(.05f,false);}
            Assert.AreEqual(FirePhase.Cleared,run.Phase);run.EvacuateSafeAnimals();
            yield return Wait(FireRescueGame.ResultsDelay+FireRescueGame.ResultsRise+.4f);Assert.True(game.ResultsVisible);
            Measure(game,mode,"Results panel",tilted);if(!tilted)AssertFlatPlacement(game,"Results panel");
            Capture(game,"1-"+mode+"-results",game.Arena.Find("Results panel").position+Vector3.down*.15f);
            Button(game,"Results upgrades").Press();yield return null;yield return null;Assert.True(game.UpgradesVisible);
            yield return Wait(.3f);
            Measure(game,mode,"Upgrades panel",tilted);if(!tilted)AssertFlatPlacement(game,"Upgrades panel");
            var upgrades=game.Arena.Find("Upgrades panel");
            Capture(game,"1-"+mode+"-upgrades",upgrades.position+Vector3.down*.12f);
            Capture(game,"1-"+mode+"-upgrades-and-board",upgrades.position+Vector3.down*.3f);
            game.CloseShop();
        }
        [UnityTest] public IEnumerator FlatPanelsKeepTheirPlace(){File.WriteAllText(Output+"/numbers.txt","");yield return AllPanels("flat",false);}
        [UnityTest] public IEnumerator TiltedBoardHidesNoBetweenLevelPanel(){yield return AllPanels("tilted",true);}

        // ---- 2. Pictures instead of live models ---------------------------------------------------------------------
        static IEnumerator OpenUpgrades(FireRescueGame game)
        {
            var run=game.Run;EmberdeepTestFlow.StartPrepared(run);yield return null;
            for(int i=0;i<200 && run.Phase==FirePhase.Fighting;i++){Clear(run);run.Tick(.05f,false);}
            Assert.AreEqual(FirePhase.Cleared,run.Phase);run.EvacuateSafeAnimals();
            yield return Wait(FireRescueGame.ResultsDelay+FireRescueGame.ResultsRise+.4f);Assert.True(game.ResultsVisible);
            Button(game,"Results upgrades").Press();yield return null;yield return null;Assert.True(game.UpgradesVisible);
        }
        [UnityTest] public IEnumerator UpgradeTilesShowRenderedPicturesClearOfTheirText()
        {
            yield return Load(3);
            var game=Object.FindFirstObjectByType<FireRescueGame>();var run=game.Run;
            yield return OpenUpgrades(game);
            for(int i=0;i<8;i++)yield return null; // one tile per frame
            var root=game.Arena.Find("Upgrades panel");int rendered=game.UpgradePicturesRendered;
            Assert.AreEqual(0,Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Count(t=>t.name=="Upgrade picture rig" || t.name=="Upgrade picture camera"),"The picture rigs are gone");
            Assert.AreEqual(0,root.GetComponentsInChildren<Transform>(true).Count(t=>t.name=="Upgrade model part" || t.name=="Upgrade model stage"),"No live models on the tiles");
            var words=new[]{"GEM","PUMP","SLOT","TOWER"};var shown=new List<Texture>();
            for(int i=0;i<4;i++)
            {
                var picture=game.UpgradePicture(i);Assert.NotNull(picture,words[i]+" picture");shown.Add(picture);
                Assert.AreEqual(FireRescueGame.PictureWidthPixels,picture.width);Assert.AreEqual(FireRescueGame.PictureHeightPixels,picture.height);
                Assert.False(game.UpgradePictureShowsGain(i));
                var pixels=Read(picture);
                try
                {
                    var c=pixels.GetPixels32();int solid=c.Count(p=>p.a>230),clear=c.Count(p=>p.a<8);
                    Note(string.Format(C,"{0} picture: {1:P1} solid, {2:P1} clear, corner alpha {3}",words[i],solid/(float)c.Length,clear/(float)c.Length,c[0].a));
                    Assert.Less(c[0].a,8,words[i]+": transparent background");Assert.Less(c[c.Length-1].a,8);
                    Assert.Greater(solid,c.Length/50,words[i]+": the model shows");Assert.Greater(clear,c.Length/5,words[i]+": not a filled box");
                    File.WriteAllBytes(Output+"/2-picture-"+words[i].ToLowerInvariant()+".png",pixels.EncodeToPNG());
                }
                finally{Object.Destroy(pixels);}
                // Nothing on the tile overlaps the picture: its word, number, arrow, unit, price, coin and lock.
                var tile=root.Find("Upgrade tile "+words[i]);var quad=tile.Find("Upgrade picture").GetComponent<MeshRenderer>();
                Rect R(Renderer r)
                {
                    var b=r.localBounds;float x0=float.MaxValue,x1=float.MinValue,y0=float.MaxValue,y1=float.MinValue;
                    for(int k=0;k<8;k++){var p=tile.InverseTransformPoint(r.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1))));
                        x0=Mathf.Min(x0,p.x);x1=Mathf.Max(x1,p.x);y0=Mathf.Min(y0,p.y);y1=Mathf.Max(y1,p.y);}
                    return Rect.MinMaxRect(x0,y0,x1,y1);
                }
                // The hover growth (GEM, PUMP) included.
                var pictureRect=R(quad);float grow=(FireRescueGame.PictureHover-1)*.5f;
                pictureRect=Rect.MinMaxRect(pictureRect.xMin-pictureRect.width*grow,pictureRect.yMin-pictureRect.height*grow,pictureRect.xMax+pictureRect.width*grow,pictureRect.yMax+pictureRect.height*grow);
                foreach(var r in tile.GetComponentsInChildren<Renderer>(true))
                {
                    if(r==quad || r.GetComponent<WorkshopButton>() || !(r.GetComponent<TextMesh>() || r.name=="Icon"))continue;
                    var other=R(r);Assert.False(pictureRect.Overlaps(other),$"{words[i]}: the picture {pictureRect} overlaps {Path(r.transform)} {other}");
                    if(r.GetComponent<TextMesh>())Note(string.Format(C,"  {0}: '{1}' y {2:F3}..{3:F3} (picture from {4:F3})",words[i],r.GetComponent<TextMesh>().text,other.yMin,other.yMax,pictureRect.yMin));
                }
                Assert.Less(pictureRect.yMax,FireRescueGame.TileHeight*.5f,"Inside the tile");
            }
            Assert.AreEqual(4,shown.Distinct().Count(),"Four pictures");
            yield return Wait(.3f);Assert.AreEqual(rendered,game.UpgradePicturesRendered,"Rendered once, not every frame");
            CaptureAt(game,"2-upgrades-pictures",root.position,40);Capture(game,"2b-upgrades-seat",root.position);
            // Hover: the green number and arrow and the line stay; SLOT and TOWER swap to their pointed-at pictures.
            for(int i=0;i<4;i++)
            {
                game.ForcedUpgradeHover=i;yield return null;yield return Wait(.25f);
                Assert.AreEqual(i,game.HoveredUpgrade);Assert.AreNotEqual(FireRescueGame.UpgradesPrompt,game.UpgradeExplainText);
                Assert.True(game.UpgradeShowsGain(i),"Green arrow and the value after buying");
                if(i>=2)
                {
                    Assert.True(game.UpgradePictureShowsGain(i),words[i]+" shows its pointed-at picture");Assert.AreSame(game.UpgradeGainPicture(i),game.UpgradePicture(i));
                    Assert.AreNotSame(game.UpgradeIdlePicture(i),game.UpgradePicture(i));
                    var a=Read(game.UpgradeIdlePicture(i));var b=Read(game.UpgradeGainPicture(i));
                    try
                    {
                        var pa=a.GetPixels32();var pb=b.GetPixels32();int green=0,differ=0;
                        for(int k=0;k<pa.Length;k++){if(Mathf.Abs(pa[k].g-pb[k].g)+Mathf.Abs(pa[k].a-pb[k].a)>40)differ++;if(pb[k].a>200 && pb[k].g>pb[k].r+60 && pb[k].g>pb[k].b+40)green++;}
                        Note(string.Format(C,"{0} pointed-at picture: {1:P1} of pixels differ, {2} green",words[i],differ/(float)pa.Length,green));
                        Assert.Greater(differ,pa.Length/200,words[i]+": the pointed-at picture shows the change");Assert.Greater(green,20,words[i]+": in green");
                        File.WriteAllBytes(Output+"/2-picture-"+words[i].ToLowerInvariant()+"-next.png",b.EncodeToPNG());
                    }
                    finally{Object.Destroy(a);Object.Destroy(b);}
                }
                else Assert.False(game.UpgradePictureShowsGain(i));
                CaptureAt(game,"2"+(char)('c'+i)+"-hover-"+words[i].ToLowerInvariant(),root.position,40);
            }
            game.ForcedUpgradeHover=-1;yield return null;
            Assert.AreEqual(rendered,game.UpgradePicturesRendered,"Hovering renders nothing");
            // A buy that changes what the tray shows renders the SLOT picture again (and only it).
            SetCoins(run,9999);yield return null;
            if(run.CanUpgradeSlot)
            {
                var before=game.UpgradeIdlePicture(2);Assert.True(game.UpgradeSlot());
                for(int i=0;i<4;i++)yield return null;
                Assert.AreNotSame(before,game.UpgradeIdlePicture(2),"The tray picture follows the bought tower");Assert.True(before==null,"The old one is released");
                Assert.LessOrEqual(game.UpgradePicturesRendered-rendered,2);
            }
            // Released with the game.
            var all=Enumerable.Range(0,4).SelectMany(i=>new[]{game.UpgradeIdlePicture(i),game.UpgradeGainPicture(i)}).Where(t=>t).ToArray();
            Assert.Greater(all.Length,4);
            yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);yield return null;yield return null; // the old game is destroyed
            Assert.True(all.All(t=>t==null),"Pictures released on destroy");
        }

        // ---- 3. The upgrade star ------------------------------------------------------------------------------------
        [UnityTest] public IEnumerator UpgradeStarShowsOnlyWhileAnUpgradeIsAffordable()
        {
            yield return Load(3);
            var game=Object.FindFirstObjectByType<FireRescueGame>();var run=game.Run;
            EmberdeepTestFlow.StartPrepared(run);yield return null;
            for(int i=0;i<200 && run.Phase==FirePhase.Fighting;i++){Clear(run);run.Tick(.05f,false);}
            Assert.AreEqual(FirePhase.Cleared,run.Phase);run.EvacuateSafeAnimals();
            SetCoins(run,9999);
            yield return Wait(FireRescueGame.ResultsDelay+FireRescueGame.ResultsRise+.4f);Assert.True(game.ResultsVisible);
            Assert.True(game.AnyUpgradeBuyable);Assert.True(game.ResultsUpgradeStarShown,"Affordable: the star shows on UPGRADES");
            var star=game.ResultsUpgradeStar;var button=Button(game,"Results upgrades");var results=game.Arena.Find("Results panel");
            var local=results.InverseTransformPoint(star.position)-results.InverseTransformPoint(button.transform.position);
            Assert.Greater(local.x,0);Assert.Greater(local.y,0);Assert.Less(local.z,0,"On the button's top-right corner, in front of it");
            float s0=star.localScale.x;yield return Wait(.6f);float s1=star.localScale.x;
            Assert.AreNotEqual(s0,s1,"It pulses");Assert.Less(Mathf.Max(s0,s1),1+FireRescueGame.UpgradeStarPulse+1e-3f,"Gently");
            Note(string.Format(C,"star: scale {0:F3} then {1:F3}",s0,s1));
            Capture(game,"3-results-star",results.position+Vector3.down*.15f);CaptureAt(game,"3b-results-star-zoom",button.transform.position,22);
            // Nothing affordable: no star.
            SetCoins(run,0);yield return null;yield return null;
            Assert.False(game.AnyUpgradeBuyable);Assert.False(game.ResultsUpgradeStarShown,"Broke: no star");
            CaptureAt(game,"3c-results-no-star-zoom",button.transform.position,22);
            // Level select carries it too.
            SetCoins(run,9999);game.OpenLevelSelect(run.Stage);yield return null;yield return null;yield return Wait(.4f);
            Assert.True(game.LevelSelectVisible);Assert.True(game.LevelSelectUpgradeStarShown,"Level select's UPGRADES");
            Capture(game,"3d-level-select-star",game.Arena.Find("Level select").position+Vector3.down*.1f);
            SetCoins(run,0);yield return null;Assert.False(game.LevelSelectUpgradeStarShown);
            game.CloseLevelSelect();yield return null;
            // Everything maxed: no star, however many coins are left.
            SetCoins(run,999999);
            for(int guard=0;guard<200 && game.AnyUpgradeBuyable;guard++)
                if(!(run.BuyMagicUpgrade() | run.BuyPumpUpgrade() | run.BuySlotUpgrade() | run.BuyTowerUpgrade()))break;
            yield return null;yield return null;
            Assert.False(game.AnyUpgradeBuyable,"All maxed");Assert.Greater(run.Coins,0);
            for(int i=0;i<4;i++)Assert.False(game.UpgradeBuyable(i));
            Assert.True(game.ResultsVisible);Assert.False(game.ResultsUpgradeStarShown,"Maxed: no star");
            Note(string.Format(C,"maxed with {0} coins left: gem {1}, pump {2}, slot {3}, tower {4}",run.Coins,run.MagicLevel,run.PumpLevel,run.SlotLevel,run.TowerLevel));
        }
    }
}

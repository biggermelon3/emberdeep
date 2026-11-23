using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FireRescue.Tests
{
    // Headset test 3, "sometimes when I move my head around, the skybox changes". EmberdeepImmersiveSky's window is
    // measured from the seat at placement over a static board footprint, so head moves, swinging hands and pieces
    // carried over the board leave it alone, and the (tilted) board stays inside its sky from the seated head's reach.
    // Captures: .agents/local/captures/emberdeep-sky-stability/<flat|tilted>-<head>-<before|after>.png. Passthrough is
    // the grey checker; red = background pixels next to the board's silhouette that lack full sky. "before" is the
    // pre-fix window, re-derived from the live eye (LegacyWindow). Numbers: report.txt in the same folder.
    public sealed class EmberdeepSkyStabilityPlayTests
    {
        const string Folder=".agents/local/captures/emberdeep-sky-stability";
        const float Tolerance=1f;   // degrees
        const int Width=1100,Height=900,Ring=6;
        const float Fov=96,WideFov=140;
        static readonly (string Name,float Right,float Up)[] Heads={("centre",0,0),("left10",-.1f,0),("right10",.1f,0),("up10",0,.1f)};

        static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
        static void Report(string line)
        {
            Directory.CreateDirectory(Folder);
            File.AppendAllText(Folder+"/report.txt",System.DateTime.Now.ToString("HH:mm:ss",CultureInfo.InvariantCulture)+" "+line+"\n");
        }
        static string F(float v)=>v.ToString("F2",CultureInfo.InvariantCulture);

        static IEnumerator LoadFireground(bool tilted)
        {
            PlayerPrefs.DeleteKey(EmberdeepPlayArea.HeightKey);PlayerPrefs.DeleteKey(EmberdeepPlayArea.DistanceKey);PlayerPrefs.DeleteKey(EmberdeepPlayArea.TiltedKey);
            EmberdeepPlayArea.Reload();EmberdeepPlayArea.SetTilted(tilted);
            EmberdeepSession.NewExpedition();var run=EmberdeepSession.Run;typeof(FireRun).GetProperty("Stage").SetValue(run,5);
            Assert.IsTrue(run.StartStage(true)); // incident 4: 4 rows
            yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);yield return null;yield return null;
            var game=Object.FindFirstObjectByType<FireRescueGame>();game.SendMessage("OnApplicationFocus",true);
            yield return Wait(1.2f);
        }
        static Vector3 MidField(FireRescueGame game)=>game.Arena.TransformPoint(new Vector3(0,0,
            EmberdeepBoardLayout.FrontRowZ+(EmberdeepBoardLayout.MaxRows-1)*.5f*EmberdeepBoardLayout.RowSpacing));
        static void SeatFrame(FireRescueGame game,out Vector3 forward,out Vector3 right)
        {
            forward=Vector3.ProjectOnPlane(game.Arena.forward,Vector3.up).normalized;right=Vector3.Cross(Vector3.up,forward);
        }

        // The window before the fix (EmberdeepImmersiveSky as of headset test 3), from the LIVE eye: arena, rack, held
        // tool, pump (spell flow) and creek station as subjects, live renderer bounds, crown over the forest.
        static void LegacyWindow(FireRescueGame game,Vector3 eye,out Vector3 axis,out float radius)
        {
            var arena=game.Arena;var points=new List<Vector2>();
            var subjects=new[]{arena,game.transform.Find("Equipment rack"),null,game.Pump?game.Pump.transform:null,game.CreekStation?game.CreekStation.transform:null};
            var floor=arena.TransformPoint(new Vector3(0,-.4f,6));
            var forward=Vector3.ProjectOnPlane(floor-eye,Vector3.up).normalized;var right=Vector3.Cross(Vector3.up,forward);
            void Project(Vector3 world)
            {
                var d=world-eye;if(d.sqrMagnitude<.0001f || Vector3.Dot(d,forward)<=.01f)return;
                points.Add(new Vector2(Mathf.Atan2(Vector3.Dot(d,right),Vector3.Dot(d,forward)),Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)));
            }
            for(int group=0;group<subjects.Length;group++)
            {
                bool found=false;Bounds bounds=default;
                if(subjects[group])
                    foreach(var r in subjects[group].GetComponentsInChildren<MeshRenderer>(true))
                    {
                        var m=r.sharedMaterial;
                        if(r.GetComponent<TextMesh>() || r.name=="Spray aim" || r.name=="Instruction plate" || r.name=="Refill step plate" || !m || m.renderQueue>=3000)continue;
                        if(!r.enabled || !r.gameObject.activeInHierarchy)continue;
                        if(!found){bounds=r.bounds;found=true;}else bounds.Encapsulate(r.bounds);
                    }
                if(found && !bounds.Contains(eye))for(int c=0;c<8;c++)Project(bounds.center+Vector3.Scale(bounds.extents,new Vector3((c&1)==0?-1:1,(c&2)==0?-1:1,(c&4)==0?-1:1)));
                if(group!=0)continue;
                if(points.Count==0)for(int c=0;c<8;c++)Project(arena.TransformPoint(new Vector3((c&1)==0?-5.7f:5.7f,(c&2)==0?-.8f:5,(c&4)==0?-.7f:12.7f)));
                float lo=0,hi=0,top=-Mathf.PI*.5f;
                foreach(var p in points){lo=Mathf.Min(lo,p.x);hi=Mathf.Max(hi,p.x);top=Mathf.Max(top,p.y);}
                top=Mathf.Clamp(top+18*Mathf.Deg2Rad,8*Mathf.Deg2Rad,40*Mathf.Deg2Rad);
                points.Add(new Vector2(lo*.55f,top));points.Add(new Vector2(hi*.55f,top));
            }
            var low=new Vector2(float.MaxValue,float.MaxValue);var high=new Vector2(float.MinValue,float.MinValue);
            foreach(var p in points){low=Vector2.Min(low,p);high=Vector2.Max(high,p);}
            var center=(low+high)*.5f;
            Vector3 Direction(Vector2 p)=>(forward*Mathf.Cos(p.x)+right*Mathf.Sin(p.x))*Mathf.Cos(p.y)+Vector3.up*Mathf.Sin(p.y);
            axis=Direction(center);radius=0;
            foreach(var p in points)radius=Mathf.Max(radius,Vector3.Angle(axis,Direction(p)));
            radius=Mathf.Min(radius+4,72);
        }
        // The camp's window before the fix: sign + camp set (sheep and pump included), live bounds, from the live eye.
        static void LegacyCampWindow(EmberdeepCamp camp,Vector3 eye,out Vector3 axis,out float radius)
        {
            var panel=camp.transform.Find("Emberdeep eye level menu");var set=camp.transform.Find("Forest rescue camp set");
            var points=new List<Vector2>();
            var forward=Vector3.ProjectOnPlane(panel.position-eye,Vector3.up).normalized;var right=Vector3.Cross(Vector3.up,forward);
            foreach(var subject in new[]{panel,set})
            {
                bool found=false;Bounds bounds=default;
                if(subject)
                    foreach(var r in subject.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        var m=r.sharedMaterial;
                        if(r.GetComponent<TextMesh>() || r.name=="Spray aim" || r.name=="Instruction plate" || r.name=="Refill step plate" || !m || m.renderQueue>=3000)continue;
                        if(!r.enabled || !r.gameObject.activeInHierarchy)continue;
                        if(!found){bounds=r.bounds;found=true;}else bounds.Encapsulate(r.bounds);
                    }
                if(!found || bounds.Contains(eye))continue;
                for(int c=0;c<8;c++)
                {
                    var d=bounds.center+Vector3.Scale(bounds.extents,new Vector3((c&1)==0?-1:1,(c&2)==0?-1:1,(c&4)==0?-1:1))-eye;
                    if(d.sqrMagnitude<.0001f || Vector3.Dot(d,forward)<=.01f)continue;
                    points.Add(new Vector2(Mathf.Atan2(Vector3.Dot(d,right),Vector3.Dot(d,forward)),Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)));
                }
            }
            var low=new Vector2(float.MaxValue,float.MaxValue);var high=new Vector2(float.MinValue,float.MinValue);
            foreach(var p in points){low=Vector2.Min(low,p);high=Vector2.Max(high,p);}
            var center=(low+high)*.5f;
            Vector3 Direction(Vector2 p)=>(forward*Mathf.Cos(p.x)+right*Mathf.Sin(p.x))*Mathf.Cos(p.y)+Vector3.up*Mathf.Sin(p.y);
            axis=Direction(center);radius=0;
            foreach(var p in points)radius=Mathf.Max(radius,Vector3.Angle(axis,Direction(p)));
            radius=Mathf.Min(radius+2,72);
        }

        // [1] The live head moves +-10 cm sideways and up, both hands sweep through the view and a piece is carried
        // over the board: the window, in world directions from the seat, moves under 1 deg (drawn and target).
        [UnityTest] public IEnumerator WindowHoldsStillWhileTheHeadMovesAndHandsSwing([Values(false,true)] bool tilted)
        {
            var saved=EmberdeepSettingsPrefs.Save();var made=new List<GameObject>();
            try
            {
                yield return LoadFireground(tilted);
                var game=Object.FindFirstObjectByType<FireRescueGame>();var sky=game.ImmersiveSky;Assert.NotNull(sky);
                Assert.IsTrue(game.SeatAnchor.HasValue,"placed");var seat=game.SeatAnchor.Value;
                Assert.AreEqual(tilted?EmberdeepBoardLayout.TiltDegrees:0,game.BoardTilt,1e-3f);
                var cam=game.Player.ViewCamera.transform;var startPose=new Pose(cam.position,cam.rotation);
                Assert.Less(Vector3.Distance(sky.Anchor,seat),1e-4f,"the window is measured from the seat");
                Assert.IsTrue(sky.Seated);
                var axis0=sky.WindowAxis;float radius0=sky.WindowRadius;
                Assert.Less(Vector3.Angle(axis0,sky.TargetAxis),.05f,"settled before the moves");
                LegacyWindow(game,seat,out var legacyAxis0,out float legacyRadius0);

                var material=Resources.Load<Material>("EmberdeepMenuUnlit");
                Transform Block(string name,Transform parent,Vector3 scale)
                {
                    var b=GameObject.CreatePrimitive(PrimitiveType.Cube);b.name=name;var c=b.GetComponent<Collider>();c.enabled=false;Object.Destroy(c);
                    b.transform.SetParent(parent,false);b.transform.localScale=scale;b.GetComponent<Renderer>().sharedMaterial=material;made.Add(b);return b.transform;
                }
                var leftHand=Block("Test left hand",game.transform,new Vector3(.09f,.03f,.18f));
                var rightHand=Block("Test right hand",game.transform,new Vector3(.09f,.03f,.18f));
                var carried=Block("Test carried piece",game.Arena,Vector3.one*.9f); // arena units: a lifted piece / sheep
                SeatFrame(game,out var forward,out var right);var look=MidField(game);

                float drawnAxis=0,drawnRadius=0,targetAxis=0,targetRadius=0,anchorDrift=0,oldAxis=0,oldRadius=0;
                const int Frames=24;
                foreach(var head in Heads)
                    for(int f=0;f<Frames;f++)
                    {
                        float s=f/(Frames-1f);
                        var eye=seat+right*head.Right+Vector3.up*head.Up;
                        cam.SetPositionAndRotation(eye,Quaternion.LookRotation(look-eye,Vector3.up));
                        float sweep=Mathf.Lerp(-.34f,.34f,s);
                        leftHand.SetPositionAndRotation(eye+forward*.30f+right*sweep-Vector3.up*.16f,Quaternion.LookRotation(forward));
                        rightHand.SetPositionAndRotation(eye+forward*(.24f+.14f*s)-right*sweep-Vector3.up*(.34f-.2f*s),Quaternion.LookRotation(forward));
                        carried.localPosition=new Vector3(Mathf.Lerp(-4.5f,4.5f,s),3.4f,Mathf.Lerp(2,10,s));
                        yield return null; // FireRescueGame.LateUpdate ticks the sky with the live delta
                        drawnAxis=Mathf.Max(drawnAxis,Vector3.Angle(axis0,sky.WindowAxis));drawnRadius=Mathf.Max(drawnRadius,Mathf.Abs(sky.WindowRadius-radius0));
                        targetAxis=Mathf.Max(targetAxis,Vector3.Angle(axis0,sky.TargetAxis));targetRadius=Mathf.Max(targetRadius,Mathf.Abs(sky.TargetRadius-radius0));
                        anchorDrift=Mathf.Max(anchorDrift,Vector3.Distance(sky.Anchor,seat));
                        LegacyWindow(game,eye,out var la,out float lr);
                        oldAxis=Mathf.Max(oldAxis,Vector3.Angle(legacyAxis0,la));oldRadius=Mathf.Max(oldRadius,Mathf.Abs(lr-legacyRadius0));
                    }
                Report($"stability {(tilted?"tilted":"flat")}: radius {F(radius0)} (before: {F(legacyRadius0)}); over head +-10 cm, hands, carried piece: "
                    +$"axis {F(drawnAxis)} / target {F(targetAxis)} deg, radius {F(drawnRadius)} / target {F(targetRadius)} deg, anchor drift {F(anchorDrift*100)} cm; "
                    +$"before the fix the same moves swung the axis {F(oldAxis)} deg and the radius {F(oldRadius)} deg");
                Assert.Less(anchorDrift,1e-4f,"the seat stays the anchor");
                Assert.Less(targetAxis,Tolerance,"window axis target");Assert.Less(targetRadius,Tolerance,"window radius target");
                Assert.Less(drawnAxis,Tolerance,"drawn window axis");Assert.Less(drawnRadius,Tolerance,"drawn window radius");
                cam.SetPositionAndRotation(startPose.position,startPose.rotation);
            }
            finally{foreach(var g in made)if(g)Object.Destroy(g);saved.Restore();}
        }

        // [2] From the centre seat, 10 cm left, 10 cm right and 10 cm up, flat and tilted: every background pixel within
        // Ring px of the board's rendered silhouette has full sky (no sky edge through or along the board).
        [UnityTest] public IEnumerator TheBoardStaysInsideItsSkyFromTheSeatedHeadsReach([Values(false,true)] bool tilted)
        {
            var saved=EmberdeepSettingsPrefs.Save();
            try
            {
                yield return LoadFireground(tilted);
                var game=Object.FindFirstObjectByType<FireRescueGame>();var sky=game.ImmersiveSky;Assert.NotNull(sky);
                float until=Time.realtimeSinceStartup+8;while(sky.Progress<1 && Time.realtimeSinceStartup<until)yield return null;
                Assert.AreEqual(1,sky.Progress,"revealed");
                var seat=game.SeatAnchor.Value;var view=game.Player.ViewCamera;var cam=view.transform;
                var startPose=new Pose(cam.position,cam.rotation);float startFov=view.fieldOfView;
                SeatFrame(game,out _,out var right);var look=MidField(game);
                var material=sky.Backdrop.GetComponent<MeshRenderer>().sharedMaterial;
                Assert.IsTrue(EmberdeepRimHud.MeasurePlinth(game.Arena,out var board));
                string tilt=tilted?"tilted":"flat";var failures=new StringBuilder();
                Directory.CreateDirectory(Folder);
                try
                {
                    view.fieldOfView=Fov;
                    foreach(var head in Heads)
                    {
                        var eye=seat+right*head.Right+Vector3.up*head.Up;
                        cam.SetPositionAndRotation(eye,Quaternion.LookRotation(look-eye,Vector3.up));
                        sky.Tick(0,false);
                        var mask=BoardMask(view,sky);
                        int after=CaptureAndCheck(view,mask,material,Folder+$"/{tilt}-{head.Name}-after.png");
                        float margin=SilhouetteMargin(game,board,eye,sky.WindowAxis,sky.WindowRadius);
                        // The pre-fix window from this eye, drawn with the same shader.
                        LegacyWindow(game,eye,out var la,out float lr);Apply(material,la,lr,12);
                        int before=CaptureAndCheck(view,mask,material,Folder+$"/{tilt}-{head.Name}-before.png");
                        sky.Tick(0,false);
                        LegacyWindow(game,seat,out var seatAxis,out _);
                        Report($"coverage {tilt} {head.Name}: window {F(sky.WindowRadius)} deg, plinth/canopy corners at least {F(margin)} deg inside; "
                            +$"ring pixels without full sky: {after} (before the fix: {before}, its axis {F(Vector3.Angle(la,seatAxis))} deg off its seat axis)");
                        if(after>0 || margin<0)failures.Append($"{head.Name}: {after} px, margin {F(margin)}; ");
                        // Wide (140 deg) views show where the window's edge sits, before and after; not asserted.
                        view.fieldOfView=WideFov;sky.Tick(0,false);var wideMask=BoardMask(view,sky);
                        CaptureAndCheck(view,wideMask,material,Folder+$"/{tilt}-{head.Name}-wide-after.png");
                        Apply(material,la,lr,12);CaptureAndCheck(view,wideMask,material,Folder+$"/{tilt}-{head.Name}-wide-before.png");
                        view.fieldOfView=Fov;sky.Tick(0,false);
                    }
                }
                finally{cam.SetPositionAndRotation(startPose.position,startPose.rotation);view.fieldOfView=startFov;sky.Tick(0,false);}
                Assert.IsEmpty(failures.ToString(),"the board must stay inside full sky from the seated head's reach ("+tilt+")");
            }
            finally{saved.Restore();}
        }

        // [3] Tilt (the play-area option) moves the target; the drawn window glides there instead of cutting, and the
        // seat stays the anchor.
        [UnityTest] public IEnumerator TiltChangeGlidesTheWindowAndKeepsTheSeat()
        {
            var saved=EmberdeepSettingsPrefs.Save();
            try
            {
                yield return LoadFireground(false);
                var game=Object.FindFirstObjectByType<FireRescueGame>();var sky=game.ImmersiveSky;var seat=game.SeatAnchor.Value;
                var axis0=sky.WindowAxis;float radius0=sky.WindowRadius;
                EmberdeepPlayArea.SetTilted(true);yield return null;
                float jumpAxis=Vector3.Angle(axis0,sky.TargetAxis),jumpRadius=Mathf.Abs(sky.TargetRadius-radius0);
                float stepAxis=Vector3.Angle(axis0,sky.WindowAxis),stepRadius=Mathf.Abs(sky.WindowRadius-radius0);
                Report($"tilt glide: target moves {F(jumpAxis)} deg (axis) / {F(jumpRadius)} deg (radius); first frame {F(stepAxis)} / {F(stepRadius)} deg, dt {Time.unscaledDeltaTime:F3} s");
                Assert.Greater(Mathf.Max(jumpAxis,jumpRadius),1,"the tilt changes the board's window");
                Assert.Less(stepAxis,jumpAxis*.6f+.01f,"axis glides");Assert.Less(stepRadius,jumpRadius*.6f+.01f,"radius glides");
                yield return Wait(1.5f);
                Assert.Less(Vector3.Angle(sky.WindowAxis,sky.TargetAxis),.05f,"arrives");Assert.AreEqual(sky.TargetRadius,sky.WindowRadius,.05f);
                Assert.Less(Vector3.Distance(sky.Anchor,seat),1e-4f,"the options keep the seat");
                EmberdeepPlayArea.SetTilted(false);yield return Wait(1.5f);
                Assert.Less(Vector3.Angle(sky.WindowAxis,axis0),.05f,"flat again");Assert.AreEqual(radius0,sky.WindowRadius,.05f);
            }
            finally{saved.Restore();}
        }

        // [4] The camp: the head moves +-10 cm and the playground's sheep and pump move; the sign's window holds still.
        [UnityTest] public IEnumerator CampWindowHoldsStillWhileTheHeadAndPlaygroundMove()
        {
            yield return SceneManager.LoadSceneAsync(EmberdeepSession.CampScene);yield return null;
            var camp=Object.FindFirstObjectByType<EmberdeepCamp>();Assert.NotNull(camp);
            float deadline=Time.realtimeSinceStartup+10;while(!camp.IsPlaced && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(camp.IsPlaced);yield return Wait(.6f);
            var sky=camp.ImmersiveSky;var cam=camp.Player.ViewCamera.transform;var start=new Pose(cam.position,cam.rotation);
            var anchor=sky.Anchor;var axis0=sky.WindowAxis;float radius0=sky.WindowRadius;
            Assert.Less(Vector3.Distance(anchor,start.position),.01f,"latched at placement");
            var play=camp.Play;var sheep=play && play.Flock?play.Flock.transform:null;var pump=play && play.Pump?play.Pump.transform:null;
            var sheepStart=sheep?sheep.localPosition:Vector3.zero;var pumpStart=pump?pump.localPosition:Vector3.zero;
            float maxAxis=0,maxRadius=0,oldAxis=0,oldRadius=0;
            LegacyCampWindow(camp,start.position,out var legacyAxis0,out float legacyRadius0);
            try
            {
                foreach(var head in Heads)
                    for(int f=0;f<16;f++)
                    {
                        float s=Mathf.Sin(f/15f*Mathf.PI);
                        cam.position=start.position+cam.right*head.Right+Vector3.up*head.Up;
                        if(sheep)sheep.localPosition=sheepStart+Vector3.up*.12f*s/Mathf.Max(1e-4f,sheep.parent.lossyScale.y);
                        if(pump)pump.localPosition=pumpStart+Vector3.up*.08f*s;
                        yield return null;
                        maxAxis=Mathf.Max(maxAxis,Vector3.Angle(axis0,sky.TargetAxis),Vector3.Angle(axis0,sky.WindowAxis));
                        maxRadius=Mathf.Max(maxRadius,Mathf.Abs(sky.TargetRadius-radius0),Mathf.Abs(sky.WindowRadius-radius0));
                        LegacyCampWindow(camp,cam.position,out var la,out float lr);
                        oldAxis=Mathf.Max(oldAxis,Vector3.Angle(legacyAxis0,la));oldRadius=Mathf.Max(oldRadius,Mathf.Abs(lr-legacyRadius0));
                    }
            }
            finally{cam.SetPositionAndRotation(start.position,start.rotation);if(sheep)sheep.localPosition=sheepStart;if(pump)pump.localPosition=pumpStart;}
            Report($"camp: radius {F(radius0)} deg (before: {F(legacyRadius0)}, axis {F(Vector3.Angle(legacyAxis0,axis0))} deg apart); head +-10 cm, sheep and pump moving: "
                +$"axis {F(maxAxis)} deg, radius {F(maxRadius)} deg (sheep {(bool)sheep}, pump {(bool)pump}); before the fix: axis {F(oldAxis)}, radius {F(oldRadius)} deg");
            Assert.Less(maxAxis,Tolerance);Assert.Less(maxRadius,Tolerance);
            Assert.Less(Vector3.Distance(sky.Anchor,anchor),1e-4f);
        }

        // ---- capture helpers ----
        static Texture2D Render(Camera view)
        {
            var previous=view.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32);
            try
            {
                view.targetTexture=target;view.Render();RenderTexture.active=target;
                var pixels=new Texture2D(Width,Height,TextureFormat.RGBA32,false);pixels.ReadPixels(new Rect(0,0,Width,Height),0,0);pixels.Apply();return pixels;
            }
            finally{view.targetTexture=previous;RenderTexture.active=active;target.Release();Object.Destroy(target);}
        }
        // The board's silhouette: what renders over the clear with the sky dome hidden.
        static bool[] BoardMask(Camera view,EmberdeepImmersiveSky sky)
        {
            sky.Backdrop.gameObject.SetActive(false);
            var t=Render(view);sky.Backdrop.gameObject.SetActive(true);
            var p=t.GetPixels32();Object.Destroy(t);var mask=new bool[p.Length];
            for(int i=0;i<p.Length;i++)mask[i]=p[i].a>128 || p[i].r>5 || p[i].g>5 || p[i].b>5;
            return mask;
        }
        static void Apply(Material m,Vector3 axis,float radius,float feather)
        {
            float r=radius*Mathf.Deg2Rad,f=feather*Mathf.Deg2Rad,el=Mathf.Asin(Mathf.Clamp(axis.y,-1,1));
            m.SetVector("_MaskAxis",axis);m.SetVector("_MaskCos",new Vector4(Mathf.Cos(r+f),Mathf.Cos(r),0,0));
            m.SetVector("_RevealRange",new Vector4(el-r-12*Mathf.Deg2Rad,el+r+12*Mathf.Deg2Rad,0,0));
        }
        static float SmoothStep(float a,float b,float x){float t=Mathf.Clamp01((x-a)/(b-a));return t*t*(3-2*t);}
        // The sky shader's alpha for a view direction (EmberdeepImmersiveSky.shader frag), from the material's values.
        static float Coverage(Vector3 d,Material m,float reveal)
        {
            var axis=(Vector3)m.GetVector("_MaskAxis");var cos=m.GetVector("_MaskCos");var range=m.GetVector("_RevealRange");
            float elevation=Mathf.Asin(Mathf.Clamp(d.y,-1,1));
            float coverage=SmoothStep(cos.x,cos.y,Vector3.Dot(d,axis))*(1-SmoothStep(1.22f,1.36f,Mathf.Abs(elevation)));
            float edge=Mathf.Lerp(range.x-.15f,range.y+.15f,SmoothStep(0,1,reveal));
            return coverage*(1-SmoothStep(edge-.1f,edge+.1f,elevation));
        }
        // Renders the frame, composites it over a passthrough checker, paints ring failures red; returns their count.
        static int CaptureAndCheck(Camera view,bool[] board,Material material,string path)
        {
            var t=Render(view);var p=t.GetPixels32();
            // Dilate the silhouette by Ring px (separable max), so the ring = near the board but not on it.
            var row=new bool[p.Length];var near=new bool[p.Length];
            for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)
            {
                bool any=false;for(int k=-Ring;k<=Ring && !any;k++){int xx=x+k;if(xx>=0 && xx<Width && board[y*Width+xx])any=true;}
                row[y*Width+x]=any;
            }
            for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)
            {
                bool any=false;for(int k=-Ring;k<=Ring && !any;k++){int yy=y+k;if(yy>=0 && yy<Height && row[yy*Width+x])any=true;}
                near[y*Width+x]=any;
            }
            float tanY=Mathf.Tan(view.fieldOfView*.5f*Mathf.Deg2Rad),tanX=tanY*Width/(float)Height;var rotation=view.transform.rotation;
            int failures=0;
            for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)
            {
                int i=y*Width+x;if(board[i])continue;
                var d=(rotation*new Vector3(((x+.5f)/Width*2-1)*tanX,((y+.5f)/Height*2-1)*tanY,1)).normalized;
                float a=Coverage(d,material,1);
                bool checker=((x>>4)+(y>>4)&1)==0;float g=checker?.42f:.30f;
                var c=p[i];
                var rgb=new Color(c.r/255f+g*(1-a),c.g/255f+g*(1-a),c.b/255f+g*(1-a));
                if(near[i] && a<.98f){failures++;rgb=new Color(1,0,0);}
                p[i]=new Color32((byte)(Mathf.Clamp01(rgb.r)*255),(byte)(Mathf.Clamp01(rgb.g)*255),(byte)(Mathf.Clamp01(rgb.b)*255),255);
            }
            for(int i=0;i<p.Length;i++)if(board[i])p[i].a=255;
            t.SetPixels32(p);t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());Object.Destroy(t);
            return failures;
        }
        // Smallest angle (deg) by which the plinth's top corners and the canopy over its back edge sit inside the
        // window's full-sky radius, seen from eye.
        static float SilhouetteMargin(FireRescueGame game,Bounds board,Vector3 eye,Vector3 axis,float radius)
        {
            float margin=float.MaxValue;
            for(int c=0;c<8;c++)
            {
                var local=new Vector3((c&1)==0?board.min.x:board.max.x,(c&2)==0?board.max.y:board.max.y+EmberdeepRimHud.CanopyTop,(c&4)==0?board.min.z:board.max.z);
                if((c&2)!=0 && (c&4)==0)continue; // canopy at the back edge only
                margin=Mathf.Min(margin,radius-Vector3.Angle(axis,game.Arena.TransformPoint(local)-eye));
            }
            return margin;
        }
    }
}

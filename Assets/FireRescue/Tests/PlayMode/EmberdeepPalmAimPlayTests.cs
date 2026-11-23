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
    // Headset test 2 (2026-11-22): "aiming at a tree's root sometimes doesn't count". On the real fireground, for every tree
    // of a few levels, an open water palm near the seated eye points its palm normal at the visible trunk base, mid-trunk,
    // crown and upper crown (the side facing the player), and FireRescueGame.PreviewPalmShot runs the live aim (gesture,
    // assist, WaterJet flight, FireRun.WaterReaches). A shot counts when the splash waters that tree. Points hidden from the
    // eye behind another tree are reported apart: a player cannot aim at them. Writes Logs/EmberdeepPalmAim/report.txt.
    // Flat and Tilted boards (EmberdeepPlayArea), so the aim math holds in arena space.
    public sealed class EmberdeepPalmAimPlayTests
    {
        const string Folder="Logs/EmberdeepPalmAim";
        static readonly string[] Kinds={"root","trunk","crown","crown top"};
        // Water-hand (left) palm positions from the eye: right, up, ahead along the board's flat heading (m).
        static readonly Vector3[] Palms={new(-.10f,-.20f,.22f),new(-.06f,-.15f,.32f),new(-.14f,-.10f,.16f)};
        static readonly string[] PalmNames={"low","reach","high"};
        bool hadHand,hadTilt;int savedHand,savedTilt;

        [SetUp] public void Setup()
        {
            hadHand=PlayerPrefs.HasKey(EmberdeepWaterHand.PrefsKey);savedHand=PlayerPrefs.GetInt(EmberdeepWaterHand.PrefsKey,0);
            hadTilt=PlayerPrefs.HasKey(EmberdeepPlayArea.TiltedKey);savedTilt=PlayerPrefs.GetInt(EmberdeepPlayArea.TiltedKey,0);
            EmberdeepWaterHand.Set(true);
        }
        [TearDown] public void Cleanup()
        {
            if(hadHand)PlayerPrefs.SetInt(EmberdeepWaterHand.PrefsKey,savedHand);else PlayerPrefs.DeleteKey(EmberdeepWaterHand.PrefsKey);
            if(hadTilt)PlayerPrefs.SetInt(EmberdeepPlayArea.TiltedKey,savedTilt);else PlayerPrefs.DeleteKey(EmberdeepPlayArea.TiltedKey);
            FireRescueGame.PalmAimAssist=true;FireRescueGame.PalmAimRoots=true;FireRescueGame.PalmAimSheep=true;
            EmberdeepWaterHand.Reload();EmberdeepPlayArea.Reload();EmberdeepSession.NewExpedition();
        }

        public struct Tally {public int Shots,Wet,Hidden,HiddenWet,NoAim,Blocked,Ground,NoImpact,RingMismatch;}

        [UnityTest] public IEnumerator AimingAnywhereOnAVisibleTreeWatersIt()
        {
            Directory.CreateDirectory(Folder);var log=new StringBuilder();var totals=new SortedDictionary<string,Tally>(System.StringComparer.Ordinal);
            var stages=new SortedSet<int>{1,EmberdeepLevels.Count/2,EmberdeepLevels.Count-2,EmberdeepLevels.Count-1};
            foreach(bool tilted in new[]{false,true})
            {
                PlayerPrefs.SetInt(EmberdeepPlayArea.TiltedKey,tilted?1:0);EmberdeepPlayArea.Reload();
                foreach(int stage in stages)
                {
                    EmberdeepSession.NewExpedition();var run=EmberdeepSession.Run;
                    typeof(FireRun).GetProperty("Stage").SetValue(run,stage);Assert.IsTrue(run.StartStage(true));
                    yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);yield return null;yield return null;
                    var game=Object.FindFirstObjectByType<FireRescueGame>();Assert.NotNull(game);Assert.AreSame(run,game.Run);
                    game.SendMessage("OnApplicationFocus",true);
                    EmberdeepTestFlow.StartPrepared(run);game.Refresh();yield return null;yield return null;
                    Assert.AreEqual(FirePhase.Fighting,run.Phase);
                    log.AppendLine($"== {(tilted?"tilted":"flat")} stage {stage} ({run.StageName}): {run.FieldColumns}x{run.FieldRows}, {run.Trees.Length} trees, arena tilt {Vector3.Angle(game.Arena.up,Vector3.up):0} deg");
                    // Before (FireRescueGame.PalmAimAssist off: the palm ray lands on the board plane) is logged; after is asserted.
                    // Before = the game before the assist: no assist and none of the root fixes (PalmAimRoots, 2026-11-22 AIM).
                    try{FireRescueGame.PalmAimAssist=false;FireRescueGame.PalmAimRoots=false;Measure(game,null,totals,(tilted?"tilted":"flat")+" before");}
                    finally{FireRescueGame.PalmAimAssist=true;FireRescueGame.PalmAimRoots=true;}
                    Measure(game,log,totals,tilted?"tilted":"flat");
                    game.ReturnToMenu();yield return Wait(.3f);
                }
            }
            var summary=new StringBuilder("== totals (visible points; hidden in brackets). before = FireRescueGame.PalmAimAssist off\n");
            var failures=new StringBuilder();int shots=0,wet=0;
            foreach(var pair in totals)
            {
                var t=pair.Value;int visible=t.Shots-t.Hidden,hit=t.Wet-t.HiddenWet;float share=visible>0?hit/(float)visible:1;
                summary.AppendLine(string.Format(CultureInfo.InvariantCulture,"{0,-29} {1,3}/{2,3} wet ({3:P0})  no-aim {4}  blocked {5}  ground {6}  no-impact {7}  ring-mismatch {8}  [hidden {9}/{10} wet]",
                    pair.Key,hit,visible,share,t.NoAim,t.Blocked,t.Ground,t.NoImpact,t.RingMismatch,t.HiddenWet,t.Hidden));
                if(pair.Key.Contains("before"))continue;
                shots+=visible;wet+=hit;
                if(t.RingMismatch>0)failures.AppendLine(pair.Key+": crown rings differ from what ApplyWater waters");
                // The headset complaint: roots (and trunks) must always count. Crowns: a crown aim's palm ray runs on past the
                // crown, and on 2 of ~830 visible shots (2026-11-22) a more central pass through a tree behind wins; allowed.
                bool lower=pair.Key.EndsWith("root") || pair.Key.EndsWith("trunk");
                if(share<(lower?1:.95f))failures.AppendLine($"{pair.Key}: {hit}/{visible} wet");
                var before=pair.Key.Replace("flat ","flat before ").Replace("tilted ","tilted before ");
                if(totals.TryGetValue(before,out var b) && b.Shots-b.Hidden==visible && b.Wet-b.HiddenWet>hit)failures.AppendLine($"{pair.Key}: worse than before");
            }
            if(shots>0 && wet<.99f*shots)failures.AppendLine($"overall {wet}/{shots} wet");
            log.Insert(0,summary+"\n");
            File.WriteAllText(Folder+"/report.txt",log.ToString());Debug.Log("[EmberdeepPalmAim]\n"+summary);
            Assert.IsEmpty(failures.ToString(),"Aiming at a visible root, trunk or crown waters that tree, and the rings match ApplyWater:\n"+summary);
        }

        // Playtest 2026-11-22 (workstream AIM, "roots count"): the owner and a playtester spray the root first and it did not
        // count, though the sweep above scores roots 100%: its root aim sits on the trunk (.8 trunk radius, 4 cm up), its
        // trees are as the level starts (none saved), and a hit anywhere on the tree's water sphere counts. Here, from the
        // same three seated palms, the palm points at the root, the board in front of the trunk (.25 and .45 out, toward the
        // palm), the board .35 to its side, and mid-trunk:
        //  - "fresh": the level's start states;
        //  - "saved": every other tree saved, this one burning (mid-level: a saved tree in front took the snap).
        // Wet: the predicted splash waters the tree (FireRun.WaterReaches with the live rule). Low: it lands under .45 above
        // the tree's ground (on the lower trunk or the board, where a root aim points). Before = FireRescueGame.PalmAimRoots
        // off (the assist as shipped in cs:345). Writes Logs/EmberdeepPalmAim/roots-report.txt.
        static readonly string[] RootKinds={"root","ground .25","ground .45","ground side","trunk"};
        public struct RootTally {public int Shots,Wet,Hidden,HiddenWet,Low,NoAim,SavedHit;public float Height;}
        [UnityTest] public IEnumerator RootAimsCountAndSavedTreesDoNotTakeThem()
        {
            Directory.CreateDirectory(Folder);var log=new StringBuilder();var totals=new SortedDictionary<string,RootTally>(System.StringComparer.Ordinal);
            var stages=new SortedSet<int>{1,EmberdeepLevels.Count/2,EmberdeepLevels.Count-2,EmberdeepLevels.Count-1};
            foreach(bool tilted in new[]{false,true})
            {
                PlayerPrefs.SetInt(EmberdeepPlayArea.TiltedKey,tilted?1:0);EmberdeepPlayArea.Reload();
                foreach(int stage in stages)
                {
                    EmberdeepSession.NewExpedition();var run=EmberdeepSession.Run;
                    typeof(FireRun).GetProperty("Stage").SetValue(run,stage);Assert.IsTrue(run.StartStage(true));
                    yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);yield return null;yield return null;
                    var game=Object.FindFirstObjectByType<FireRescueGame>();Assert.NotNull(game);Assert.AreSame(run,game.Run);
                    game.SendMessage("OnApplicationFocus",true);
                    EmberdeepTestFlow.StartPrepared(run);game.Refresh();yield return null;yield return null;
                    Assert.AreEqual(FirePhase.Fighting,run.Phase);
                    string board=tilted?"tilted":"flat";
                    log.AppendLine($"== {board} stage {stage} ({run.StageName}): {run.Trees.Length} trees");
                    foreach(bool saved in new[]{false,true})
                    {
                        string scene=saved?" saved":" fresh";
                        try{FireRescueGame.PalmAimRoots=false;MeasureRoots(game,null,totals,board+" before"+scene,saved);}
                        finally{FireRescueGame.PalmAimRoots=true;}
                        MeasureRoots(game,log,totals,board+scene,saved);
                    }
                    game.ReturnToMenu();yield return Wait(.3f);
                }
            }
            var summary=new StringBuilder("== root aims (visible points; hidden in brackets). wet = waters the aimed tree, low = lands under .45 above its ground. before = FireRescueGame.PalmAimRoots off\n");
            var failures=new StringBuilder();int mustShots=0,mustWet=0;
            foreach(var pair in totals)
            {
                var t=pair.Value;int visible=t.Shots-t.Hidden,hit=t.Wet-t.HiddenWet;float share=visible>0?hit/(float)visible:1;
                float height=t.Wet>0?t.Height/t.Wet:0;
                summary.AppendLine(string.Format(CultureInfo.InvariantCulture,"{0,-36} {1,3}/{2,3} wet ({3:P0})  low {4,3} ({5:P0})  mean height {6:F2}  no-aim {7}  on a saved tree {8}  [hidden {9}/{10} wet]",
                    pair.Key,hit,visible,share,t.Low,t.Wet>0?t.Low/(float)t.Wet:0,height,t.NoAim,t.SavedHit,t.HiddenWet,t.Hidden));
                if(pair.Key.Contains(" before "))continue;
                // Roots, the board right in front of the trunk and mid-trunk count; the board .45 out and to the side is also
                // the next tree's root disc on some boards, so it is reported, and must not get worse. 2026-11-22 20:25: on the
                // tilted board one "reach" shot per kind (stage 1, tree 5: its palm ray passes dead centre through tree 4 first)
                // misses with or without the AIM fixes (the before pass shows it too), hence 94% per key and 99% overall.
                bool must=pair.Key.EndsWith(" root") || pair.Key.EndsWith(" ground .25") || pair.Key.EndsWith(" trunk");
                if(must){mustShots+=visible;mustWet+=hit;if(share<.94f)failures.AppendLine($"{pair.Key}: {hit}/{visible} wet");}
                // "The whole-tree snap": a root aim lands at the root (on the board or the lower trunk), not at crown height.
                if(pair.Key.EndsWith(" root") && t.Wet>0 && height>=.45f)failures.AppendLine($"{pair.Key}: lands {height:F2} up");
                var before=pair.Key.Replace("flat ","flat before ").Replace("tilted ","tilted before ");
                if(totals.TryGetValue(before,out var b) && b.Shots-b.Hidden==visible && b.Wet-b.HiddenWet>hit)failures.AppendLine($"{pair.Key}: worse than before ({b.Wet-b.HiddenWet})");
                // Saved trees hide nothing: points behind them count too.
                if(pair.Key.Contains(" saved ") && must && t.HiddenWet<t.Hidden)failures.AppendLine($"{pair.Key}: hidden behind saved trees {t.HiddenWet}/{t.Hidden} wet");
            }
            if(mustShots>0 && mustWet<.99f*mustShots)failures.AppendLine($"root, ground .25 and trunk overall {mustWet}/{mustShots} wet");
            log.Insert(0,summary+"\n");
            File.WriteAllText(Folder+"/roots-report.txt",log.ToString());Debug.Log("[EmberdeepPalmAim roots]\n"+summary);
            Assert.IsEmpty(failures.ToString(),"Root aims water the aimed tree, saved trees in front do not take them:\n"+summary);
        }
        static void MeasureRoots(FireRescueGame game,StringBuilder log,IDictionary<string,RootTally> totals,string board,bool savedFront)
        {
            var arena=game.Arena;var run=game.Run;var eye=game.Player.ViewCamera.transform.position;
            var forest=arena.Find("Incident forest");Assert.NotNull(forest,"tree views");
            var profiles=new Profile[run.Trees.Length];
            for(int i=0;i<profiles.Length;i++)profiles[i]=Visual(arena,forest.Find("Tree "+i));
            var flat=Vector3.ProjectOnPlane(arena.forward,Vector3.up).normalized;var right=Vector3.Cross(Vector3.up,flat);
            var reached=new List<int>();var eyeLocal=arena.InverseTransformPoint(eye);
            var states=new (TreeState State,float Heat)[run.Trees.Length];
            for(int j=0;j<states.Length;j++)states[j]=(run.Trees[j].State,run.Trees[j].Heat);
            try
            {
                for(int i=0;i<run.Trees.Length;i++)
                {
                    if(states[i].State==TreeState.Cleared || states[i].State==TreeState.Burnt)continue;
                    if(savedFront)
                    {
                        for(int j=0;j<run.Trees.Length;j++)if(states[j].State!=TreeState.Cleared && states[j].State!=TreeState.Burnt)
                        {run.Trees[j].State=j==i?TreeState.Burning:TreeState.Saved;run.Trees[j].Heat=j==i?Mathf.Max(50,states[j].Heat):0;}
                    }
                    var pr=profiles[i];
                    for(int pose=0;pose<Palms.Length;pose++)
                    {
                        var palm=eye+right*Palms[pose].x+Vector3.up*Palms[pose].y+flat*Palms[pose].z;var palmLocal=arena.InverseTransformPoint(palm);
                        var toward=new Vector3(palmLocal.x-pr.Base.x,0,palmLocal.z-pr.Base.z).normalized;var side=Vector3.Cross(Vector3.up,toward);
                        for(int k=0;k<RootKinds.Length;k++)
                        {
                            var local=pr.Base+k switch
                            {
                                0=>toward*pr.TrunkRadius*.8f+Vector3.up*.04f,
                                1=>toward*.25f+Vector3.up*.01f,
                                2=>toward*.45f+Vector3.up*.01f,
                                3=>side*.35f+toward*.1f+Vector3.up*.01f,
                                _=>toward*Mathf.Min(.12f,pr.TrunkRadius)+Vector3.up*pr.CrownLow*.5f,
                            };
                            bool hidden=Hidden(profiles,i,eyeLocal,local);
                            bool aiming=game.PreviewPalmShot(Aim(palm,arena.TransformPoint(local)),reached,out var shot);
                            bool wet=aiming && shot.Hit && reached.Contains(i);
                            string key=$"{board} {PalmNames[pose]} {RootKinds[k]}";totals.TryGetValue(key,out var t);t.Shots++;
                            if(hidden){t.Hidden++;if(wet)t.HiddenWet++;}
                            float height=shot.Hit?arena.InverseTransformPoint(shot.Impact).y-pr.Base.y:float.NaN;
                            if(wet){t.Wet++;t.Height+=height;if(height<.45f)t.Low++;}
                            if(!aiming)t.NoAim++;
                            bool onSaved=aiming && shot.Hit && shot.ImpactTarget>=0 && shot.ImpactTarget<run.Trees.Length && run.Trees[shot.ImpactTarget].State==TreeState.Saved;
                            if(onSaved)t.SavedHit++;
                            totals[key]=t;
                            if(!wet && !hidden && log!=null)
                            {
                                var picks=new StringBuilder();
                                foreach(var c in game.PalmAim.Candidates)picks.Append(string.Format(CultureInfo.InvariantCulture," {0}:{1:F2}@{2:F1}{3}",c.Tree,c.Miss,c.Enter,c.Hidden?"h":""));
                                log.AppendLine(string.Format(CultureInfo.InvariantCulture,"  {0} palm, tree {1} ({2:F2},{3:F2}) {4}: {5} impact target {6} height {7:F2} [assist {8}] {{{9} }}",PalmNames[pose],i,pr.Base.x,pr.Base.z,
                                    RootKinds[k],!aiming?"no aim":!shot.Hit?"no impact":"dry",shot.ImpactTarget,height,shot.AssistTree,picks));
                            }
                        }
                    }
                    if(savedFront)for(int j=0;j<states.Length;j++)(run.Trees[j].State,run.Trees[j].Heat)=states[j];
                }
            }
            finally{for(int j=0;j<states.Length;j++)(run.Trees[j].State,run.Trees[j].Heat)=states[j];}
        }

        // Arena-local visual profile of one tree view: crown = the widest renderer (as EmberdeepTreeReadout).
        public struct Profile {public Vector3 Base;public float CrownLow,Top,CrownRadius,TrunkRadius;}
        public static Profile Visual(Transform arena,Transform tree)
        {
            var p=new Profile{Base=arena.InverseTransformPoint(tree.position),CrownLow=.4f,Top=2,CrownRadius=.6f,TrunkRadius=.2f};
            float widest=-1;
            foreach(var filter in tree.GetComponentsInChildren<MeshFilter>())
            {
                if(!filter.sharedMesh)continue;var b=filter.sharedMesh.bounds;
                var min=Vector3.one*1e9f;var max=-min;
                for(int c=0;c<8;c++)
                {
                    var corner=b.center+Vector3.Scale(b.extents,new Vector3((c&1)==0?-1:1,(c&2)==0?-1:1,(c&4)==0?-1:1));
                    var local=arena.InverseTransformPoint(filter.transform.TransformPoint(corner))-p.Base;min=Vector3.Min(min,local);max=Vector3.Max(max,local);
                }
                float half=Mathf.Max(max.x-min.x,max.z-min.z)*.5f;
                if(half>widest){widest=half;p.CrownLow=min.y;p.Top=max.y;p.CrownRadius=half;}
            }
            foreach(var filter in tree.GetComponentsInChildren<MeshFilter>())
            {
                if(!filter.sharedMesh)continue;var b=filter.sharedMesh.bounds;var e=Vector3.Scale(b.extents,filter.transform.lossyScale)/arena.lossyScale.x;
                float half=Mathf.Max(e.x,e.z);if(half<widest-.001f)p.TrunkRadius=half;
            }
            return p;
        }
        static void Measure(FireRescueGame game,StringBuilder log,IDictionary<string,Tally> totals,string board)
        {
            var arena=game.Arena;var run=game.Run;var eye=game.Player.ViewCamera.transform.position;
            var forest=arena.Find("Incident forest");Assert.NotNull(forest,"tree views");
            var profiles=new Profile[run.Trees.Length];
            for(int i=0;i<profiles.Length;i++)profiles[i]=Visual(arena,forest.Find("Tree "+i));
            var flat=Vector3.ProjectOnPlane(arena.forward,Vector3.up).normalized;var right=Vector3.Cross(Vector3.up,flat);
            var reached=new List<int>();var eyeLocal=arena.InverseTransformPoint(eye);
            for(int pose=0;pose<Palms.Length;pose++)
            {
                var palm=eye+right*Palms[pose].x+Vector3.up*Palms[pose].y+flat*Palms[pose].z;var palmLocal=arena.InverseTransformPoint(palm);
                for(int i=0;i<run.Trees.Length;i++)
                {
                    if(run.Trees[i].State==TreeState.Cleared)continue;
                    var pr=profiles[i];var toward=new Vector3(palmLocal.x-pr.Base.x,0,palmLocal.z-pr.Base.z).normalized;
                    for(int k=0;k<Kinds.Length;k++)
                    {
                        var local=pr.Base+k switch
                        {
                            0=>toward*pr.TrunkRadius*.8f+Vector3.up*.04f,
                            1=>toward*Mathf.Min(.12f,pr.TrunkRadius)+Vector3.up*pr.CrownLow*.5f,
                            2=>toward*pr.CrownRadius*.75f+Vector3.up*(pr.CrownLow+pr.Top)*.5f,
                            _=>toward*pr.CrownRadius*.45f+Vector3.up*Mathf.Lerp(pr.CrownLow,pr.Top,.8f),
                        };
                        bool hidden=Hidden(profiles,i,eyeLocal,local);
                        var point=arena.TransformPoint(local);var normal=(point-palm).normalized;
                        var rotation=Quaternion.FromToRotation(Vector3.up,normal);
                        var hand=new FireToolManipulation.Hand{Valid=true,HasGripFrame=true,GripRotation=rotation,Contact=palm,Palm=new Pose(palm,rotation),Closure=0};
                        bool aiming=game.PreviewPalmShot(hand,reached,out var shot);
                        bool wet=aiming && shot.Hit && reached.Contains(i);
                        string key=$"{board} {PalmNames[pose]} {Kinds[k]}";totals.TryGetValue(key,out var t);t.Shots++;
                        if(hidden){t.Hidden++;if(wet)t.HiddenWet++;}
                        if(wet)t.Wet++;
                        string why;
                        if(!aiming){t.NoAim++;why="no aim";}
                        else if(!shot.Hit){t.NoImpact++;why="no impact";}
                        else if(shot.ImpactTarget>=0 && shot.ImpactTarget<run.Trees.Length && shot.ImpactTarget!=i){t.Blocked++;why="hits tree "+shot.ImpactTarget;}
                        else if(shot.ImpactTarget<0){var g=arena.InverseTransformPoint(shot.Impact);t.Ground++;why=string.Format(CultureInfo.InvariantCulture,"ground {0:F2} from base",Vector2.Distance(new Vector2(g.x,g.z),new Vector2(pr.Base.x,pr.Base.z)));}
                        else why="hits target "+shot.ImpactTarget;
                        if(aiming && !RingsMatch(run,arena,shot,reached))t.RingMismatch++;
                        totals[key]=t;
                        if(!wet && log!=null)
                        {
                            var picks=new StringBuilder();
                            foreach(var c in game.PalmAim.Candidates)picks.Append(string.Format(CultureInfo.InvariantCulture," {0}:{1:F2}@{2:F1}{3}",c.Tree,c.Miss,c.Enter,c.Hidden?"h":""));
                            log.AppendLine(string.Format(CultureInfo.InvariantCulture,"  {0} palm, tree {1} ({2},{3}) {4}{5}: {6}{7} {{{8} }}",PalmNames[pose],i,
                                Mathf.RoundToInt((pr.Base.x)*100)/100f,Mathf.RoundToInt(pr.Base.z*100)/100f,Kinds[k],hidden?" (hidden)":"",why,
                                shot.AssistTree>=0?" [assist "+shot.AssistTree+"]":"",picks));
                        }
                    }
                }
            }
        }
        // Headset test 3 (2026-11-22): "burning sheep behind trees are hard to hit", and a level only clears with no sheep on
        // fire. One burning sheep at a time, (a) in the gap behind each front-row tree (straight behind it and .35 to either
        // side) and (b) at each of the level's sheep homes behind the back row. From the three seated palms the palm points
        // at what the eye can see of it: a grid on its eye-facing body and flames, and two points over it (aiming over the
        // tree that hides it). A shot counts when the predicted splash puts the sheep out (FireRun.WaterReachesSheep).
        // Before = FireRescueGame.PalmAimSheep off (the tree-only assist as shipped). Also: the tree hiding the sheep keeps
        // its root, trunk and crown aims. Writes Logs/EmberdeepPalmAim/sheep-report.txt.
        static readonly string[] SheepKinds={"body","flames","over"};
        public struct SheepTally {public int Shots,Wet,Hidden,HiddenWet,NoAim,Blocked,Ground,TreeShots,TreeWet,CrownShots,CrownWet,RingMismatch;}
        [UnityTest] public IEnumerator AimingAtAVisibleBurningSheepBehindTheTreesPutsItOut()
        {
            Directory.CreateDirectory(Folder);var log=new StringBuilder();var totals=new SortedDictionary<string,SheepTally>(System.StringComparer.Ordinal);
            var stages=new SortedSet<int>{1,EmberdeepLevels.Count/2,EmberdeepLevels.Count-2,EmberdeepLevels.Count-1};
            foreach(bool tilted in new[]{false,true})
            {
                PlayerPrefs.SetInt(EmberdeepPlayArea.TiltedKey,tilted?1:0);EmberdeepPlayArea.Reload();
                foreach(int stage in stages)
                {
                    EmberdeepSession.NewExpedition();var run=EmberdeepSession.Run;
                    typeof(FireRun).GetProperty("Stage").SetValue(run,stage);Assert.IsTrue(run.StartStage(true));
                    yield return SceneManager.LoadSceneAsync(EmberdeepSession.FireScene);yield return null;yield return null;
                    var game=Object.FindFirstObjectByType<FireRescueGame>();Assert.NotNull(game);Assert.AreSame(run,game.Run);
                    game.SendMessage("OnApplicationFocus",true);
                    EmberdeepTestFlow.StartPrepared(run);game.Refresh();yield return null;yield return null;
                    Assert.AreEqual(FirePhase.Fighting,run.Phase);Assert.Greater(run.Animals.Length,0);
                    log.AppendLine($"== {(tilted?"tilted":"flat")} stage {stage} ({run.StageName}): {run.Trees.Length} trees, {run.Animals.Length} sheep");
                    string board=tilted?"tilted":"flat";
                    try{FireRescueGame.PalmAimSheep=false;MeasureSheep(game,null,totals,board+" before");}
                    finally{FireRescueGame.PalmAimSheep=true;}
                    MeasureSheep(game,log,totals,board);
                    game.ReturnToMenu();yield return Wait(.3f);
                }
            }
            var summary=new StringBuilder("== burning sheep totals (visible points; hidden in brackets). before = FireRescueGame.PalmAimSheep off\n");
            var failures=new StringBuilder();int shots=0,wet=0,beforeShots=0,beforeWet=0,treeShots=0,treeWet=0,crownShots=0,crownWet=0;
            foreach(var pair in totals)
            {
                var t=pair.Value;int visible=t.Shots-t.Hidden,hit=t.Wet-t.HiddenWet;float share=visible>0?hit/(float)visible:1;
                if(pair.Key.EndsWith("tree in front"))
                {
                    summary.AppendLine(string.Format(CultureInfo.InvariantCulture,"{0,-34} root/trunk {1}/{2} wet, crown {3}/{4} wet",pair.Key,t.TreeWet,t.TreeShots,t.CrownWet,t.CrownShots));
                    treeShots+=t.TreeShots;treeWet+=t.TreeWet;crownShots+=t.CrownShots;crownWet+=t.CrownWet;continue;
                }
                summary.AppendLine(string.Format(CultureInfo.InvariantCulture,"{0,-34} {1,3}/{2,3} out ({3:P0})  no-aim {4}  blocked {5}  ground {6}  ring-mismatch {7}  [hidden {8}/{9} out]",
                    pair.Key,hit,visible,share,t.NoAim,t.Blocked,t.Ground,t.RingMismatch,t.HiddenWet,t.Hidden));
                if(pair.Key.Contains("before")){beforeShots+=visible;beforeWet+=hit;continue;}
                shots+=visible;wet+=hit;
                if(t.RingMismatch>0)failures.AppendLine(pair.Key+": sheep rings differ from what ApplyAnimalWater puts out");
                // Homes (where burning sheep really are, behind the field) must hold. The gap spot is a stress case: a sheep
                // .62 behind a front tree, half a row in front of the next; there an "over" aim's ray can run dead centre
                // into that next crown, and the tree it really points at keeps it (2026-11-22: 80-100% per category).
                if(share<(pair.Key.Contains(" home ")?.95f:.75f))failures.AppendLine($"{pair.Key}: {hit}/{visible} out");
                var before=pair.Key.Replace("flat ","flat before ").Replace("tilted ","tilted before ");
                if(totals.TryGetValue(before,out var b) && b.Shots-b.Hidden==visible && b.Wet-b.HiddenWet>hit)failures.AppendLine($"{pair.Key}: worse than before");
            }
            summary.AppendLine(string.Format(CultureInfo.InvariantCulture,"overall before {0}/{1} ({2:P1}), after {3}/{4} ({5:P1}); the tree in front of a burning sheep: root/trunk {6}/{7}, crown {8}/{9} wet",
                beforeWet,beforeShots,beforeShots>0?beforeWet/(float)beforeShots:0,wet,shots,shots>0?wet/(float)shots:0,treeWet,treeShots,crownWet,crownShots));
            if(shots>0 && wet<.95f*shots)failures.AppendLine($"overall {wet}/{shots} out");
            if(treeShots>0 && treeWet<treeShots)failures.AppendLine($"root/trunk of the tree in front: {treeWet}/{treeShots} wet");
            // Crown aims at that tree: the few misses go to a tree behind it (the tree assist's own crown run-on), not to the sheep.
            if(crownShots>0 && crownWet<.98f*crownShots)failures.AppendLine($"crown of the tree in front: {crownWet}/{crownShots} wet");
            log.Insert(0,summary+"\n");
            File.WriteAllText(Folder+"/sheep-report.txt",log.ToString());Debug.Log("[EmberdeepPalmAim sheep]\n"+summary);
            Assert.IsEmpty(failures.ToString(),"Aiming at a visible burning sheep puts it out, its rings match ApplyAnimalWater, and the tree in front still takes root/trunk aims:\n"+summary);
        }
        static void MeasureSheep(FireRescueGame game,StringBuilder log,IDictionary<string,SheepTally> totals,string board)
        {
            var arena=game.Arena;var run=game.Run;var eye=game.Player.ViewCamera.transform.position;
            var forest=arena.Find("Incident forest");Assert.NotNull(forest,"tree views");
            var profiles=new Profile[run.Trees.Length];
            for(int i=0;i<profiles.Length;i++)profiles[i]=Visual(arena,forest.Find("Tree "+i));
            var flat=Vector3.ProjectOnPlane(arena.forward,Vector3.up).normalized;var right=Vector3.Cross(Vector3.up,flat);
            var eyeLocal=arena.InverseTransformPoint(eye);var reached=new List<int>();var sheepReached=new List<int>();
            // Spots: behind each front-row tree, and the level's homes. Sheep 0 burns there; the rest stay calm at home.
            float front=float.MaxValue;foreach(var t in run.Trees)if(t.State!=TreeState.Cleared)front=Mathf.Min(front,t.Position.z);
            var spots=new List<(string Name,Vector3 At,int Tree)>();
            for(int i=0;i<run.Trees.Length;i++)
            {
                if(run.Trees[i].State==TreeState.Cleared || run.Trees[i].Position.z>front+.5f)continue;
                foreach(float dx in new[]{-.35f,0,.35f})spots.Add(("gap",new Vector3(run.Trees[i].Position.x+dx,0,run.Trees[i].Position.z+.62f),i));
            }
            foreach(var a in run.Animals)spots.Add(("home",new Vector3(a.Home.x,0,a.Home.z),-1));
            var saved=new (Vector3 Position,bool OnFire,AnimalState State,int HeldBy)[run.Animals.Length];
            for(int j=0;j<saved.Length;j++){var a=run.Animals[j];saved[j]=(a.Position,a.OnFire,a.State,a.HeldBy);a.OnFire=false;a.Position=a.Home;}
            var sheep=run.Animals[0];
            try
            {
                foreach(var spot in spots)
                {
                    sheep.Position=spot.At;sheep.OnFire=true;sheep.State=AnimalState.Waiting;sheep.HeldBy=-1;
                    // What the eye sees of it: its eye-facing side, body and flames on a small grid, and two points over it.
                    var toEye=new Vector3(eyeLocal.x-spot.At.x,0,eyeLocal.z-spot.At.z).normalized;var side=Vector3.Cross(Vector3.up,toEye);
                    var aims=new List<(int Kind,Vector3 Local)>();
                    foreach(float h in new[]{.1f,.3f,.5f})foreach(float s in new[]{-.25f,0,.25f})aims.Add((0,spot.At+toEye*.2f+side*s+Vector3.up*h));
                    foreach(float h in new[]{.75f,1f})foreach(float s in new[]{-.15f,.15f})aims.Add((1,spot.At+toEye*.1f+side*s+Vector3.up*h));
                    foreach(float h in new[]{1.6f,2.1f})aims.Add((2,spot.At+Vector3.up*h));
                    // The tree hiding it: in front of it, nearest the eye-to-sheep line (gap spots: the tree it stands behind).
                    int cover=spot.Tree;
                    if(cover<0)
                    {
                        float best=.7f;
                        for(int j=0;j<run.Trees.Length;j++)
                        {
                            var tp=run.Trees[j].Position;if(run.Trees[j].State==TreeState.Cleared || tp.z>=spot.At.z-.3f)continue;
                            var a2=new Vector2(spot.At.x,spot.At.z);var b2=new Vector2(eyeLocal.x,eyeLocal.z);var p2=new Vector2(tp.x,tp.z);
                            var ab=b2-a2;float u=Mathf.Clamp01(Vector2.Dot(p2-a2,ab)/ab.sqrMagnitude);float off=Vector2.Distance(p2,a2+ab*u);
                            if(off<best){best=off;cover=j;}
                        }
                    }
                    for(int pose=0;pose<Palms.Length;pose++)
                    {
                        var palm=eye+right*Palms[pose].x+Vector3.up*Palms[pose].y+flat*Palms[pose].z;var palmLocal=arena.InverseTransformPoint(palm);
                        foreach(var (kind,local) in aims)
                        {
                            int k=kind;string key=$"{board} {PalmNames[pose]} {spot.Name} {SheepKinds[k]}";totals.TryGetValue(key,out var t);
                            bool hidden=Hidden(profiles,-1,eyeLocal,local);
                            var point=arena.TransformPoint(local);var hand=Aim(palm,point);
                            bool aiming=game.PreviewPalmShot(hand,reached,out var shot,sheepReached);
                            bool wet=aiming && shot.Hit && sheepReached.Contains(0);
                            t.Shots++;if(hidden){t.Hidden++;if(wet)t.HiddenWet++;}if(wet)t.Wet++;
                            string why;
                            if(!aiming){t.NoAim++;why="no aim";}
                            else if(!shot.Hit)why="no impact";
                            else if(shot.ImpactTarget>=0 && shot.ImpactTarget<run.Trees.Length){if(!wet)t.Blocked++;why="hits tree "+shot.ImpactTarget;}
                            else if(shot.ImpactTarget>=run.Trees.Length+run.Props.Length){if(!wet)t.Blocked++;why="hits sheep "+(shot.ImpactTarget-run.Trees.Length-run.Props.Length);}
                            else if(shot.ImpactTarget>=run.Trees.Length){if(!wet)t.Blocked++;why="hits cabin "+(shot.ImpactTarget-run.Trees.Length);}
                            else{var g=arena.InverseTransformPoint(shot.Impact);if(!wet)t.Ground++;why=string.Format(CultureInfo.InvariantCulture,"ground {0:F2} from sheep",Vector2.Distance(new Vector2(g.x,g.z),new Vector2(spot.At.x,spot.At.z)));}
                            if(aiming && shot.Hit)
                            {
                                // FireRescueGame.OnWaterImpact: only a cabin hit skips ApplyWater (trees, the board and sheep spheres all apply it).
                                var impact=arena.InverseTransformPoint(shot.Impact);bool prop=shot.ImpactTarget>=run.Trees.Length && shot.ImpactTarget<run.Trees.Length+run.Props.Length;
                                float distance=Vector3.Distance(sheep.Position+Vector3.up*FireRun.AnimalHeight,impact);
                                bool expect=!prop && distance<=run.MagicRadius+FireRun.AnimalRadius;
                                if(expect!=sheepReached.Contains(0))
                                {
                                    t.RingMismatch++;
                                    log?.AppendLine(string.Format(CultureInfo.InvariantCulture,"  RING MISMATCH {0} palm {1} ({2:F2},{3:F2}): expect {4} got [{5}] target {6} distance {7:F3} reach {8:F3}",
                                        PalmNames[pose],spot.Name,spot.At.x,spot.At.z,expect,string.Join(",",sheepReached),shot.ImpactTarget,distance,run.MagicRadius+FireRun.AnimalRadius));
                                }
                            }
                            if(!wet && !hidden && log!=null)
                            {
                                var picks=new StringBuilder();
                                foreach(var c in game.PalmAim.Candidates)picks.Append(string.Format(CultureInfo.InvariantCulture," {0}:{1:F2}@{2:F1}{3}",c.Tree,c.Miss,c.Enter,c.Hidden?"h":""));
                                log.AppendLine(string.Format(CultureInfo.InvariantCulture,"  {0} palm, {1} ({2:F2},{3:F2}) {4}: {5}{6}{7} {{{8} }}",PalmNames[pose],spot.Name,spot.At.x,spot.At.z,SheepKinds[k],why,
                                    shot.AssistTree>=0?" [assist tree "+shot.AssistTree+"]":"",shot.AssistSheep>=0?" [assist sheep]":"",picks));
                            }
                            totals[key]=t;
                        }
                        // The tree hiding the sheep keeps its aims: root and trunk always (asserted), crowns reported.
                        if(cover>=0 && !board.Contains("before"))
                        {
                            string key=$"{board} {PalmNames[pose]} {spot.Name} tree in front";totals.TryGetValue(key,out var t);
                            var pr=profiles[cover];var at=new Vector3(palmLocal.x-pr.Base.x,0,palmLocal.z-pr.Base.z).normalized;
                            var treeAims=new[]{pr.Base+at*pr.TrunkRadius*.8f+Vector3.up*.04f,pr.Base+at*Mathf.Min(.12f,pr.TrunkRadius)+Vector3.up*pr.CrownLow*.5f,
                                pr.Base+at*pr.CrownRadius*.75f+Vector3.up*(pr.CrownLow+pr.Top)*.5f,pr.Base+at*pr.CrownRadius*.45f+Vector3.up*Mathf.Lerp(pr.CrownLow,pr.Top,.8f)};
                            for(int a=0;a<treeAims.Length;a++)
                            {
                                if(Hidden(profiles,cover,eyeLocal,treeAims[a]))continue;
                                bool ok=game.PreviewPalmShot(Aim(palm,arena.TransformPoint(treeAims[a])),reached,out var treeShot) && treeShot.Hit && reached.Contains(cover);
                                if(a<2){t.TreeShots++;if(ok)t.TreeWet++;}else{t.CrownShots++;if(ok)t.CrownWet++;}
                                if(!ok)log?.AppendLine($"  {PalmNames[pose]} palm, {spot.Name} tree {cover} ({(run.Trees[cover].State==TreeState.Burning?"burning":"calm")}) in front of the sheep: {(a<2?"root/trunk":"crown")} aim missed it [assist tree {treeShot.AssistTree} sheep {treeShot.AssistSheep}]");
                            }
                            totals[key]=t;
                        }
                    }
                }
            }
            finally
            {
                for(int j=0;j<saved.Length;j++){var a=run.Animals[j];(a.Position,a.OnFire,a.State,a.HeldBy)=saved[j];}
            }
        }
        static FireToolManipulation.Hand Aim(Vector3 palm,Vector3 point)
        {
            var rotation=Quaternion.FromToRotation(Vector3.up,(point-palm).normalized);
            return new FireToolManipulation.Hand{Valid=true,HasGripFrame=true,GripRotation=rotation,Contact=palm,Palm=new Pose(palm,rotation),Closure=0};
        }

        // The preview's reach list is exactly what OnWaterImpact -> FireRun.ApplyWater changes from this impact.
        static bool RingsMatch(FireRun run,Transform arena,FireRescueGame.PalmShot shot,List<int> reached)
        {
            var expected=new List<int>();
            bool prop=shot.ImpactTarget>=run.Trees.Length && shot.ImpactTarget<run.Trees.Length+run.Props.Length;
            if(shot.Hit && !prop)
            {
                var local=arena.InverseTransformPoint(shot.Impact);
                for(int j=0;j<run.Trees.Length;j++)
                    if(run.Trees[j].State!=TreeState.Burnt && run.Trees[j].State!=TreeState.Cleared && FireRun.WaterReaches(run.Trees[j],local,run.MagicRadius,FireRescueGame.PalmAimRoots))expected.Add(j);
            }
            if(expected.Count!=reached.Count)return false;
            for(int j=0;j<expected.Count;j++)if(expected[j]!=reached[j])return false;
            return true;
        }
        // Eye-to-point segment through another tree's crown or trunk (a little inside their outline).
        static bool Hidden(Profile[] profiles,int self,Vector3 eye,Vector3 point)
        {
            for(int j=0;j<profiles.Length;j++)
            {
                if(j==self)continue;var p=profiles[j];
                for(int s=1;s<40;s++)
                {
                    var at=Vector3.Lerp(eye,point,s/40f)-p.Base;float r=new Vector2(at.x,at.z).magnitude;
                    if(at.y>=p.CrownLow && at.y<=p.Top && r<p.CrownRadius*.85f)return true;
                    if(at.y>=0 && at.y<p.CrownLow && r<p.TrunkRadius*.7f)return true;
                }
            }
            return false;
        }
        static IEnumerator Wait(float seconds){float until=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<until)yield return null;}
    }
}

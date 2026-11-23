using System.Collections.Generic;
using UnityEngine;

namespace FireRescue
{
    // LoopSpec §3.7 palm aim assist. Headset test 2 (2026-11-22): aiming the palm at a tree's root, trunk or crown often
    // put no water on it. EmberdeepPalmAimPlayTests measured why:
    //  - the palm ray is resolved on the board (y 0), so a crown aim lands far behind the tree or never meets the board;
    //  - the water targets are spheres (r .038 m at .7 units up, WaterJet.Segment), larger than a visible trunk, so the
    //    low arc to a back tree's root, seen under a front Broadleaf's crown, splashes on the front tree.
    // So the palm ray is tested against each tree's visible shape first (root flare, trunk, crown, + flames while burning),
    // and the tree it points at gets an arc that really lands on it: WaterJet.Predict decides, FireRun.WaterReaches
    // confirms. Water physics, damage and the sphere targets are unchanged; the arc, marker and crown rings show the
    // snapped shot. Arena units throughout (x FireMiniature.Scale = metres), so it holds on a tilted board.
    public sealed class EmberdeepPalmAim
    {
        // *tune* Visible shapes, arena units. Crown: an ellipsoid in the crown's mesh bounds (Cedar cones and Broadleaf
        // domes; a box let rays that only passed a neighbour's crown corner take its snap). Trunk: a cylinder up to the
        // crown. Root: a disc on the board, everything within RootCore of the axis is dead centre.
        public const float RootRadius=.42f, RootCore=.25f, RootHeight=.3f;
        public const float TrunkRadius=.2f;
        public const float CrownMargin=.08f;
        public const float FireTop=.5f, FireMargin=.10f; // burning trees: the flames and the readout pin above the crown
        // Choice when the ray crosses several trees (miss: 0 dead centre .. 1 edge). Players aim at what they see, so a tree
        // counts only where the eye can see the ray's pass through it (EyeBlock: another tree's inner crown hides it). The
        // palm sits below the eye, so its ray often clips a front or side crown the eye sees past: that clip is no block. Only
        // a dead-centre pass (under CoreMiss) hides what lies further along the ray (a crown aim's ray goes on to the board
        // behind the tree, often near another root). Among the rest the most central pass wins. Burning gets no bonus: a bonus
        // let a burning neighbour the ray merely grazed steal a calm tree's aim.
        public const float EyeBlock=.7f, CoreMiss=.15f;
        public const float StickyBonus=.15f;    // the tree aimed at last frame keeps the snap through jitter
        // Aim heights tried on the picked tree, above its water target (FireRun.Tree.Position, .7 up): its centre first, then
        // higher (the arc clears a front tree and drops onto this one), then lower. ArcTarget tries the aimed height before
        // these (playtest 2026-11-22: a root aim landed at the crown, "the whole-tree snap feels awkward").
        static readonly float[] ArcHeights={0,.25f,.45f,.65f,.85f,-.25f,-.5f,-.7f};
        // Playtest 2026-11-22: saved trees are no targets (a root aim at a burning tree behind a saved one snapped to the
        // saved one), and they hide nothing: no eye block, no dead-centre pass. Safe trees stay targets (pre-wetting,
        // LoopSpec §4.3). Their water spheres still catch the jet; ArcTarget's higher arcs pass over them. False: as before.
        public bool SkipSaved=true;

        // *tune* Burning sheep (headset test 3, 2026-11-22). A level only clears with no sheep on fire (FireRun.CheckOutcome),
        // and the assist only knew trees, so a palm aimed at a burning sheep behind the trees snapped to a tree, or its low
        // arc splashed on a front tree's water sphere. A sheep that water can still put out (FireRun.CanDouseSheep) is a
        // target like a tree: Pick tests its visible shape, the eye must see the pass, and ArcTargetSheep finds an arc that
        // lands on it. Target index Trees.Length + sheep (IsSheep). Calm sheep are not targets: water does nothing useful
        // on them, and the aim must not jump to the flock grazing behind the trees.
        // Shape, arena units from the sheep's ground point: wool body and head (EmberdeepSheepView kit at .65: up to ~.64
        // high, ~.4 half length) and its three flame tongues (FireRescueVfx: +-.18 out, tops ~1.1 high), as one upright
        // cylinder (SheepMargin as CrownMargin). Like a root flare, every pass within SheepCore of its axis is dead centre:
        // a palm aimed at the body stops at the board just behind it, so its closest approach is at the body's foot, not
        // at its middle (an ellipsoid scored those aims .8-.95 and lost them to the trees in front).
        public const float SheepRadius=.42f,SheepTop=1.1f,SheepMargin=.08f,SheepCore=.25f;
        // Over the flames, up to SheepCatchTop (above the tallest crowns, ~2), a column of the same radius: a sheep hidden
        // behind a tree is reached by aiming just over that tree (the ring shows when it is picked). A pass there counts
        // as at least SheepOverMiss off centre, so a tree the ray goes through first, fairly centrally, keeps the aim.
        public const float SheepCatchTop=2.4f,SheepOverMiss=.35f;
        // Aim points tried on a sheep: (height above its ground point, distance short of it toward the palm). A walking sheep
        // is a water target (FireRescueGame.UpdateTargets: a sphere at its body), so an open line splashes on it; otherwise
        // the arc must come down within FireRun.WaterReachesSheep of it: first straight at the body and its feet, then higher
        // arcs aimed a little short, which pass over a front tree's water sphere and drop onto the sheep.
        static readonly Vector2[] SheepArcs={new(.3f,0),new(0,0),new(.6f,.3f),new(.9f,.5f),new(1.2f,.7f),new(1.5f,.9f),new(.6f,0),new(1.2f,.3f),new(1.8f,1.1f),new(2.2f,1.4f)};
        public static bool IsSheep(int target,int treeCount,out int sheep){sheep=target-treeCount;return target>=treeCount && treeCount>=0;}

        public struct Volume {public Vector3 Base;public float CrownLow,Top,CrownRadius;public bool Measured;}
        public struct Candidate {public int Tree;public float Miss,Enter,Score;public Vector3 Point;public bool Hidden;} // Tree: Pick's target index
        Volume[] volumes=System.Array.Empty<Volume>();
        public int Count=>volumes.Length;
        public Volume this[int i]=>volumes[i];
        // Every tree the last Pick's ray crossed (debug and tests).
        public readonly List<Candidate> Candidates=new();
        // The winning candidate's closest pass (arena units), valid while the last Pick returned >= 0: ArcTarget's aimed height.
        public Vector3 PickPoint {get;private set;}

        // Measures each tree view (mesh bounds in arena space; the widest renderer is the crown, as EmberdeepTreeReadout).
        public void Build(Transform arena,IReadOnlyList<Transform> roots)
        {
            int n=roots?.Count??0;volumes=new Volume[n];
            for(int i=0;i<n;i++)
            {
                var root=roots[i];var v=new Volume{CrownLow=.4f,Top=2,CrownRadius=.6f};
                if(!root || !arena){volumes[i]=v;continue;}
                v.Base=arena.InverseTransformPoint(root.position);float widest=-1;
                foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mesh=filter.sharedMesh;if(!mesh)continue;var b=mesh.bounds;
                    var min=Vector3.one*float.MaxValue;var max=-min;
                    for(int c=0;c<8;c++)
                    {
                        var corner=b.center+Vector3.Scale(b.extents,new Vector3((c&1)==0?-1:1,(c&2)==0?-1:1,(c&4)==0?-1:1));
                        var local=arena.InverseTransformPoint(filter.transform.TransformPoint(corner))-v.Base;
                        min=Vector3.Min(min,local);max=Vector3.Max(max,local);
                    }
                    float half=Mathf.Max(max.x-min.x,max.z-min.z)*.5f;
                    if(half>widest){widest=half;v.CrownLow=Mathf.Max(0,min.y);v.Top=max.y;v.CrownRadius=half;v.Measured=true;}
                }
                volumes[i]=v;
            }
        }

        // The target the palm ray points at: a tree index, trees.Length + i for burning sheep i (IsSheep), or -1 for none.
        // origin/direction world space; range metres, along the ray, and the board top stops the ray. eye (world, optional):
        // the head, for "aim at what you see". Burnt and dug trees, and saved ones (SkipSaved), are skipped: the ray passes
        // them to what is behind.
        // sheep (optional): the flock; only sheep water can put out count.
        public int Pick(Transform arena,Vector3 origin,Vector3 direction,float range,FireRun.Tree[] trees,int previous=-1,Vector3? eye=null,FireRun.Animal[] sheep=null)
        {
            Candidates.Clear();PickPoint=default;
            if(!arena || trees==null || direction.sqrMagnitude<1e-8f)return -1;
            var o=arena.InverseTransformPoint(origin);var d=arena.InverseTransformDirection(direction).normalized;
            Vector3? eyeLocal=eye.HasValue?arena.InverseTransformPoint(eye.Value):null;
            float scale=Mathf.Max(1e-5f,arena.lossyScale.x),reach=range/scale;
            if(d.y<-1e-5f && o.y>0)reach=Mathf.Min(reach,-o.y/d.y); // where it meets the board
            int count=Mathf.Min(trees.Length,volumes.Length);
            for(int i=0;i<count;i++)
            {
                var tree=trees[i];if(!Target(tree))continue;
                var v=volumes[i];bool burning=tree.State==TreeState.Burning;
                float miss=float.MaxValue,enter=float.MaxValue,at=0;var p=o-v.Base;
                float off=Disc(p,d,reach,RootRadius,-.1f,RootHeight,out float t,out float tc);
                if(off<=1){miss=Mathf.Max(0,off*RootRadius-RootCore)/(RootRadius-RootCore);enter=t;at=tc;}
                if(v.CrownLow>RootHeight){off=Disc(p,d,reach,TrunkRadius,RootHeight,v.CrownLow,out t,out tc);if(off<miss){miss=off;enter=t;at=tc;}}
                Crown(v,burning,out var centre,out var axes);
                off=Ellipsoid(p,d,reach,centre,axes,out t,out tc);
                if(off<miss){miss=off;enter=t;at=tc;}
                if(miss>1)continue;
                var k=new Candidate{Tree=i,Miss=miss,Enter=enter,Point=o+d*at,Score=miss-(i==previous?StickyBonus:0)};
                if(eyeLocal.HasValue)k.Hidden=Blocked(eyeLocal.Value,k.Point,i,trees);
                Candidates.Add(k);
            }
            if(sheep!=null)for(int i=0;i<sheep.Length;i++)
            {
                var a=sheep[i];if(!FireRun.CanDouseSheep(a))continue;
                var p=o-a.Position;float r=SheepRadius+SheepMargin;
                float off=Disc(p,d,reach,r,-.1f,SheepTop,out float t,out float tc);
                if(off<=1)off=Mathf.Max(0,off*r-SheepCore)/(r-SheepCore);
                float over=Disc(p,d,reach,r,SheepTop,SheepCatchTop,out float t2,out float tc2);
                if(over<=1){over=SheepOverMiss+(1-SheepOverMiss)*over;if(over<off){off=over;t=t2;tc=tc2;}}
                if(off>1)continue;
                int index=trees.Length+i;
                var k=new Candidate{Tree=index,Miss=off,Enter=t,Point=o+d*tc,Score=off-(index==previous?StickyBonus:0)};
                if(eyeLocal.HasValue)k.Hidden=Blocked(eyeLocal.Value,k.Point,-1,trees);
                Candidates.Add(k);
            }
            float core=float.MaxValue;foreach(var k in Candidates)if(k.Miss<CoreMiss && !k.Hidden)core=Mathf.Min(core,k.Enter);
            for(int c=0;c<Candidates.Count;c++)if(Candidates[c].Enter>core+.05f){var k=Candidates[c];k.Hidden=true;Candidates[c]=k;}
            bool anyVisible=false;foreach(var k in Candidates)anyVisible|=!k.Hidden;
            int best=-1;float bestScore=float.MaxValue,bestEnter=float.MaxValue;
            foreach(var k in Candidates)
            {
                if(k.Hidden && anyVisible)continue;
                if(k.Score<bestScore-.05f || (k.Score<bestScore+.05f && k.Enter<bestEnter)){best=k.Tree;bestScore=k.Score;bestEnter=k.Enter;PickPoint=k.Point;}
            }
            return best;
        }
        static bool Solid(FireRun.Tree tree)=>tree!=null && tree.State!=TreeState.Burnt && tree.State!=TreeState.Cleared;
        // A tree the assist can pick, and one that can hide another (eye block, dead-centre pass).
        bool Target(FireRun.Tree tree)=>Solid(tree) && !(SkipSaved && tree.State==TreeState.Saved);
        void Crown(Volume v,bool burning,out Vector3 centre,out Vector3 axes)
        {
            float low=v.CrownLow,top=v.Top+(burning?FireTop:0),radius=v.CrownRadius+CrownMargin+(burning?FireMargin:0);
            centre=new Vector3(0,(low+top)*.5f,0);axes=new Vector3(radius,(top-low)*.5f+CrownMargin,radius);
        }
        // The eye's line to a point (arena units) runs through another tree's inner crown (mesh bounds, no margin). Trunks are
        // too thin to hide a tree.
        bool Blocked(Vector3 eye,Vector3 point,int self,FireRun.Tree[] trees)
        {
            var delta=point-eye;float length=delta.magnitude;if(length<1e-4f)return false;var d=delta/length;
            for(int j=0;j<Mathf.Min(trees.Length,volumes.Length);j++)
            {
                if(j==self || !Target(trees[j]))continue;var v=volumes[j];
                var centre=new Vector3(0,(v.CrownLow+v.Top)*.5f,0);var axes=new Vector3(v.CrownRadius,(v.Top-v.CrownLow)*.5f,v.CrownRadius);
                if(Ellipsoid(eye-v.Base,d,length,centre,axes,out _,out _)<EyeBlock)return true;
            }
            return false;
        }
        // Ray (p from the tree base, unit d, t in [0, reach]) against a vertical cylinder of the given radius around the tree
        // axis, y in [low, high]. Returns the closest approach to the axis inside the overlap over the radius (0 centre .. 1
        // edge), or MaxValue when the ray misses; t: where the ray enters, tc: where it passes closest.
        static float Disc(Vector3 p,Vector3 d,float reach,float radius,float low,float high,out float t,out float tc)
        {
            t=tc=float.MaxValue;if(high<=low || radius<=0)return float.MaxValue;
            float t0=0,t1=reach;
            if(Mathf.Abs(d.y)<1e-6f){if(p.y<low || p.y>high)return float.MaxValue;}
            else
            {
                float a=(low-p.y)/d.y,b=(high-p.y)/d.y;if(a>b){var s=a;a=b;b=s;}
                t0=Mathf.Max(t0,a);t1=Mathf.Min(t1,b);if(t0>t1)return float.MaxValue;
            }
            float dx=d.x,dz=d.z,A=dx*dx+dz*dz,B=p.x*dx+p.z*dz,C=p.x*p.x+p.z*p.z-radius*radius;
            if(A<1e-8f){if(C>0)return float.MaxValue;}
            else
            {
                float disc=B*B-A*C;if(disc<0)return float.MaxValue;float root=Mathf.Sqrt(disc);
                t0=Mathf.Max(t0,(-B-root)/A);t1=Mathf.Min(t1,(-B+root)/A);if(t0>t1)return float.MaxValue;
            }
            tc=A<1e-8f?t0:Mathf.Clamp(-B/A,t0,t1);t=t0;
            return new Vector2(p.x+dx*tc,p.z+dz*tc).magnitude/radius;
        }
        // Ray against an axis-aligned ellipsoid (centre c, semi-axes r) in [0, reach]: the closest approach to its centre in
        // the unit-sphere frame (0 centre .. 1 surface), or MaxValue; t: where the ray enters, tc: where it passes closest.
        static float Ellipsoid(Vector3 p,Vector3 d,float reach,Vector3 c,Vector3 r,out float t,out float tc)
        {
            t=tc=float.MaxValue;if(r.x<=0 || r.y<=0 || r.z<=0)return float.MaxValue;
            var q=new Vector3((p.x-c.x)/r.x,(p.y-c.y)/r.y,(p.z-c.z)/r.z);var e=new Vector3(d.x/r.x,d.y/r.y,d.z/r.z);
            float A=Vector3.Dot(e,e),B=Vector3.Dot(q,e),C=Vector3.Dot(q,q)-1,disc=B*B-A*C;
            if(A<1e-10f || disc<0)return float.MaxValue;
            float root=Mathf.Sqrt(disc),t0=Mathf.Max(0,(-B-root)/A),t1=Mathf.Min(reach,(-B+root)/A);
            if(t0>t1)return float.MaxValue;
            tc=Mathf.Clamp(-B/A,t0,t1);t=t0;
            return (q+e*tc).magnitude;
        }

        // A launch target on the tree whose arc (WaterJet.AimVelocity at speed, then WaterJet.Predict against the live
        // water targets) splashes within radius of it (FireRun.WaterReaches; column: palm water's trunk-column rule). aimed
        // (arena units, optional: Pick's PickPoint) is tried first, clamped onto the tree's trunk column (FireRun.TrunkPoint),
        // so a root aim lands at the root and a crown aim at the top of the column; then ArcHeights. False: no tried arc
        // reaches it; target is then its centre, so the marker honestly shows where that water would land.
        public static bool ArcTarget(Vector3 origin,FireRun.Tree tree,Transform arena,Vector3[] targets,float speed,float range,float radius,out Vector3 target,
            Vector3? aimed=null,bool column=false)=>ArcTarget(origin,tree,-1,arena,targets,speed,range,radius,out target,out _,aimed,column);
        // *tune* Root pass (playtest 2026-11-22, the default Tilted board): the tree's water sphere (r .475 at .7 up) sits in
        // front of its root, so even the arc aimed at the root stopped on the sphere at ~1.0 up, at the crown's foot. An
        // aimed point under RootPassHeight on the column is a root aim: its arc goes to the board in front of the trunk
        // (RootPassNear..RootRadius out, toward the palm, where the ray passed) and flies through this tree's own sphere
        // (pass = index: WaterJet.MagicPass / Predict pass). Other trees' spheres still stop it. Off: RootPass false.
        public const bool RootPass=true;
        public const float RootPassHeight=RootHeight,RootPassNear=.2f;
        // index: the tree's water-target index (FireRescueGame.UpdateTargets: trees first), -1 = no root pass.
        public static bool ArcTarget(Vector3 origin,FireRun.Tree tree,int index,Transform arena,Vector3[] targets,float speed,float range,float radius,
            out Vector3 target,out int pass,Vector3? aimed=null,bool column=false)
        {
            target=arena.TransformPoint(tree.Position);pass=-1;
            if(aimed.HasValue && float.IsFinite(aimed.Value.sqrMagnitude))
            {
                var on=FireRun.TrunkPoint(tree,aimed.Value);float ground=tree.Position.y-FireRun.TrunkColumn;
                if(RootPass && index>=0 && on.y-ground<RootPassHeight)
                {
                    var o=arena.InverseTransformPoint(origin);
                    var off=new Vector3(aimed.Value.x-on.x,0,aimed.Value.z-on.z);var toward=new Vector3(o.x-on.x,0,o.z-on.z);
                    if(off.magnitude<RootPassNear)off=(toward.sqrMagnitude>1e-6f?toward.normalized:Vector3.back)*RootPassNear;
                    else if(off.magnitude>RootRadius)off=off.normalized*RootRadius;
                    var p=arena.TransformPoint(new Vector3(on.x,ground,on.z)+off);
                    if(Lands(origin,p,tree,arena,targets,speed,range,radius,column,index)){target=p;pass=index;return true;}
                }
                var q=arena.TransformPoint(on);
                if(Lands(origin,q,tree,arena,targets,speed,range,radius,column,-1)){target=q;return true;}
            }
            foreach(float k in ArcHeights)
            {
                var p=arena.TransformPoint(tree.Position+Vector3.up*k);
                if(Lands(origin,p,tree,arena,targets,speed,range,radius,column,-1)){target=p;return true;}
            }
            return false;
        }
        static bool Lands(Vector3 origin,Vector3 p,FireRun.Tree tree,Transform arena,Vector3[] targets,float speed,float range,float radius,bool column,int pass)
        {
            var velocity=WaterJet.AimVelocity(origin,p,speed);
            return WaterJet.Predict(origin,velocity,range,arena,targets,out var hit,pass) && FireRun.WaterReaches(tree,arena.InverseTransformPoint(hit.Point),radius,column);
        }
        // The same for a burning sheep (SheepArcs): an arc whose splash reaches it (FireRun.WaterReachesSheep at its current
        // spot; the stream re-aims every frame as it runs, and one splash in reach puts it out). False: no tried arc
        // reaches it; target is then its body, so the marker honestly shows where that water would land.
        public static bool ArcTargetSheep(Vector3 origin,FireRun.Animal sheep,Transform arena,Vector3[] targets,float speed,float range,float radius,out Vector3 target)
        {
            target=arena.TransformPoint(sheep.Position+Vector3.up*FireRun.AnimalHeight);
            var o=arena.InverseTransformPoint(origin);var toward=new Vector3(o.x-sheep.Position.x,0,o.z-sheep.Position.z);
            toward=toward.sqrMagnitude>1e-6f?toward.normalized:Vector3.back;
            foreach(var k in SheepArcs)
            {
                var p=arena.TransformPoint(sheep.Position+Vector3.up*k.x+toward*k.y);
                var velocity=WaterJet.AimVelocity(origin,p,speed);
                if(WaterJet.Predict(origin,velocity,range,arena,targets,out var hit) && FireRun.WaterReachesSheep(sheep,arena.InverseTransformPoint(hit.Point),radius))
                {target=p;return true;}
            }
            return false;
        }
    }
}

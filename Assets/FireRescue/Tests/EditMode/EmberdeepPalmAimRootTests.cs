using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    // Playtest 2026-11-22 "roots count" (workstream AIM): the owner and a playtester spray the root first, and it did not
    // count. (a) a saved tree in front took the snap; (b) palm water reached only ~.43 from the trunk at ground level (the
    // .7-up point against MagicRadius .82); (c) the snapped arc aimed at the tree's .7 point, not at the root. Same two-tree
    // column as EmberdeepPalmAimTests (Broadleaf shape: bare trunk to 1.0, crown 1.0-1.96). The scene sweep is
    // EmberdeepPalmAimPlayTests.RootAimsCountAndSavedTreesDoNotTakeThem.
    public sealed class EmberdeepPalmAimRootTests
    {
        const float Speed=FireRescueGame.PalmWaterSpeed,Range=1.12f,Radius=.82f;
        static GameObject Board(float tilt,TreeState front,out Transform arena,out Transform[] roots,out FireRun.Tree[] trees,out Vector3[] targets)
        {
            var go=new GameObject("Palm root board");arena=go.transform;arena.localScale=Vector3.one*FireMiniature.Scale;
            arena.SetPositionAndRotation(new Vector3(0,.8f,.16f),Quaternion.Euler(-tilt,0,0));
            trees=new[]{new FireRun.Tree{Position=new Vector3(0,.7f,3),State=front},new FireRun.Tree{Position=new Vector3(0,.7f,4.15f),State=TreeState.Burning,Heat=50}};
            roots=new Transform[trees.Length];targets=new Vector3[trees.Length];
            for(int i=0;i<trees.Length;i++)
            {
                roots[i]=new GameObject("Tree "+i).transform;roots[i].SetParent(arena,false);roots[i].localPosition=trees[i].Position-Vector3.up*.7f;
                Part(roots[i],new Vector3(0,1.48f,0),new Vector3(1.16f,.96f,1.16f));
                Part(roots[i],new Vector3(0,.5f,0),new Vector3(.18f,1,.18f));
                targets[i]=arena.TransformPoint(trees[i].Position); // a saved tree's water sphere still catches the jet (UpdateTargets)
            }
            return go;
        }
        static void Part(Transform root,Vector3 at,Vector3 size)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Cube);Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(root,false);part.transform.localPosition=at;part.transform.localScale=size;
        }
        static Vector3 Palm(Transform arena)
        {
            var flat=Vector3.ProjectOnPlane(arena.forward,Vector3.up).normalized;
            return arena.TransformPoint(new Vector3(0,0,3))+Vector3.up*.16f-flat*.18f-Vector3.Cross(Vector3.up,flat)*.05f;
        }

        [Test] public void PalmWaterCountsAlongTheTrunkColumnOtherWaterAtThePoint()
        {
            var tree=new FireRun.Tree{Position=new Vector3(1,.7f,4),State=TreeState.Burning,Heat=10};
            var ground=new Vector3(1.6f,0,4); // .6 from the trunk on the board
            Assert.False(FireRun.WaterReaches(tree,ground,Radius),"the .7 point: a ground splash .6 out is .92 away");
            Assert.True(FireRun.WaterReaches(tree,ground,Radius,true),"the trunk column: .6");
            Assert.True(FireRun.WaterReaches(tree,new Vector3(1.8f,0,4),Radius,true));Assert.False(FireRun.WaterReaches(tree,new Vector3(1.83f,0,4),Radius,true));
            // Above the column top the column rule is the point rule; below the board it clamps to the ground.
            var high=new Vector3(1.3f,1.4f,4.2f);Assert.AreEqual(FireRun.WaterReaches(tree,high,Radius),FireRun.WaterReaches(tree,high,Radius,true));
            Assert.AreEqual(new Vector3(1,0,4),FireRun.TrunkPoint(tree,new Vector3(3,-2,9)));Assert.AreEqual(new Vector3(1,.7f,4),FireRun.TrunkPoint(tree,new Vector3(0,5,0)));
            // ApplyWater: the default (towers, plane, tools, spells) keeps the point; the palm's column call saves the tree.
            var run=new FireRun(true);run.StartStage();
            var burning=System.Array.Find(run.Trees,t=>t.State==TreeState.Burning);Assert.NotNull(burning,"a burning tree at the start");
            var at=burning.Position+new Vector3(.6f,-.7f,0);
            float heat=burning.Heat;run.ApplyWater(at,1,Radius);Assert.AreEqual(heat,burning.Heat,"point rule: out of reach");
            run.ApplyWater(at,1,Radius,1,true);Assert.Less(burning.Heat,heat,"column rule: the root splash counts");
        }

        // The back tree's root seen past a SAVED front tree, the eye 30 cm over the palm, so it sees that root through the
        // front crown. Before: the eye block hid the back tree and the saved tree (its trunk, grazed by the palm ray) took
        // the snap. Now a saved tree is no target and hides nothing, and the arc still clears its water sphere. The picks are
        // laid out on the flat board (as EmberdeepPalmAimTests); the arc is checked on both.
        [Test] public void ASavedTreeInFrontNeitherTakesNorHidesTheRootAimBehindIt([Values(0f,30f)] float tilt)
        {
            var go=Board(tilt,TreeState.Saved,out var arena,out var roots,out var trees,out var targets);
            try
            {
                var aim=new EmberdeepPalmAim();aim.Build(arena,roots);
                var origin=Palm(arena);var rootLocal=new Vector3(0,.04f,4.15f-.2f);var ray=(arena.TransformPoint(rootLocal)-origin).normalized;
                var eye=origin+Vector3.up*.3f;Vector3 aimed=rootLocal;
                if(tilt==0)
                {
                    aim.SkipSaved=false;Assert.AreEqual(0,aim.Pick(arena,origin,ray,Range,trees,-1,eye),"before: the saved tree takes the root aim");
                    aim.SkipSaved=true;Assert.AreEqual(1,aim.Pick(arena,origin,ray,Range,trees,-1,eye),"the burning tree behind");
                    foreach(var c in aim.Candidates)Assert.AreNotEqual(0,c.Tree,"a saved tree is no candidate");
                    aimed=aim.PickPoint;
                    // A dead-centre ray through the saved crown runs on to the tree behind it.
                    var through=(arena.TransformPoint(new Vector3(0,1.5f,3))-origin).normalized;
                    aim.SkipSaved=false;Assert.AreEqual(0,aim.Pick(arena,origin,through,Range*2,trees,-1,eye),"before: the saved crown");
                    aim.SkipSaved=true;Assert.AreEqual(1,aim.Pick(arena,origin,through,Range*2,trees,-1,eye),"a saved crown hides nothing");
                }
                // The arc to the aimed root, or a higher one, lands on the burning tree past the saved tree's sphere.
                Assert.True(EmberdeepPalmAim.ArcTarget(origin,trees[1],arena,targets,Speed,Range,Radius,out var target,aimed,true));
                Assert.True(WaterJet.Predict(origin,WaterJet.AimVelocity(origin,target,Speed),Range,arena,targets,out var hit));
                Assert.AreNotEqual(0,hit.Tree,"not on the saved tree's sphere");
                Assert.True(FireRun.WaterReaches(trees[1],arena.InverseTransformPoint(hit.Point),Radius,true),"the snapped arc waters the burning tree");
                // Safe trees stay targets (pre-wetting, LoopSpec §4.3).
                trees[0].State=TreeState.Safe;var front=arena.TransformPoint(new Vector3(0,.04f,3-.2f));
                if(tilt==0)Assert.AreEqual(0,aim.Pick(arena,origin,(front-origin).normalized,Range,trees,-1,eye),"a safe tree's root is a target");
            }
            finally{Object.DestroyImmediate(go);}
        }

        // A root aim lands at the root: the aimed height on the trunk column is tried before the tree's .7 point. On the
        // tilted board this palm sits almost over the tree and the arc to the root runs through the tree's own water sphere,
        // so there only the reach is checked.
        [Test] public void ARootAimLandsAtTheRootNotAtTheCrown([Values(0f,30f)] float tilt)
        {
            var go=Board(tilt,TreeState.Burning,out var arena,out var roots,out var trees,out var targets);
            try
            {
                var aim=new EmberdeepPalmAim();aim.Build(arena,roots);var origin=Palm(arena);
                var root=arena.TransformPoint(new Vector3(.1f,.02f,3-.25f));
                Assert.AreEqual(0,aim.Pick(arena,origin,(root-origin).normalized,Range,trees));
                Assert.Less(aim.PickPoint.y,.35f,"the pick passes at root height");
                Assert.True(EmberdeepPalmAim.ArcTarget(origin,trees[0],arena,targets,Speed,Range,Radius,out var target,aim.PickPoint,true));
                var local=arena.InverseTransformPoint(target);Assert.Less(local.y,.35f,"aimed at the root, not the .7 point");
                Assert.True(WaterJet.Predict(origin,WaterJet.AimVelocity(origin,target,Speed),Range,arena,targets,out var hit));
                var impact=arena.InverseTransformPoint(hit.Point);
                if(tilt==0)Assert.Less(impact.y,.5f,"the water lands on the lower trunk or the ground");
                Assert.True(FireRun.WaterReaches(trees[0],impact,Radius,true));
                // Root pass (EmberdeepPalmAim.RootPass): the stream flies through this tree's own sphere and lands on the board
                // in front of the trunk, on both boards (tilted: the arc above stopped on that sphere at ~1.0 up).
                Assert.True(EmberdeepPalmAim.ArcTarget(origin,trees[0],0,arena,targets,Speed,Range,Radius,out var foot,out int pass,aim.PickPoint,true));
                Assert.AreEqual(0,pass,"passes its own sphere");
                Assert.True(WaterJet.Predict(origin,WaterJet.AimVelocity(origin,foot,Speed),Range,arena,targets,out var landed,pass));
                var at=arena.InverseTransformPoint(landed.Point);
                Assert.AreEqual(-1,landed.Tree,"on the board");Assert.Less(at.y,.05f);
                float apart=new Vector2(at.x,at.z-3).magnitude;Assert.GreaterOrEqual(apart,EmberdeepPalmAim.RootPassNear-.05f,"in front of the trunk, not inside it");
                Assert.LessOrEqual(apart,EmberdeepPalmAim.RootRadius+.05f);
                Assert.True(FireRun.WaterReaches(trees[0],at,Radius,true));
                // Without the aimed point (as before) the arc goes to the .7 point.
                Assert.True(EmberdeepPalmAim.ArcTarget(origin,trees[0],arena,targets,Speed,Range,Radius,out var old));
                Assert.AreEqual(.7f,arena.InverseTransformPoint(old).y,.01f);
                // A crown aim clamps to the column top: the same arc as before.
                var crown=arena.TransformPoint(new Vector3(-.3f,1.6f,3));
                Assert.AreEqual(0,aim.Pick(arena,origin,(crown-origin).normalized,Range,new[]{trees[0]}));
                Assert.True(EmberdeepPalmAim.ArcTarget(origin,trees[0],arena,targets,Speed,Range,Radius,out var top,aim.PickPoint,true));
                Assert.AreEqual(.7f,arena.InverseTransformPoint(top).y,.01f);
            }
            finally{Object.DestroyImmediate(go);}
        }
    }
}

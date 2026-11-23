using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    // EmberdeepPalmAim without a scene: two trees in one column of the compact board, Broadleaf-shaped (bare trunk to 1.0
    // units, crown 1.0-1.96), the seated left palm aiming at the back tree's root seen under the front crown. The plain arc
    // to that root splashes on the front tree's water sphere (headset test 2, 2026-11-22); the assist picks the back tree
    // and finds an arc that waters it. The real-scene sweep is EmberdeepPalmAimPlayTests.
    public sealed class EmberdeepPalmAimTests
    {
        const float Speed=FireRescueGame.PalmWaterSpeed,Range=1.12f,Radius=.82f;
        static GameObject Board(float tilt,out Transform arena,out Transform[] roots,out FireRun.Tree[] trees,out Vector3[] targets)
        {
            var go=new GameObject("Palm aim board");arena=go.transform;arena.localScale=Vector3.one*FireMiniature.Scale;
            arena.SetPositionAndRotation(new Vector3(0,.8f,.16f),Quaternion.Euler(-tilt,0,0));
            trees=new[]{new FireRun.Tree{Position=new Vector3(0,.7f,3),State=TreeState.Safe},new FireRun.Tree{Position=new Vector3(0,.7f,4.15f),State=TreeState.Burning,Heat=50}};
            roots=new Transform[trees.Length];targets=new Vector3[trees.Length];
            for(int i=0;i<trees.Length;i++)
            {
                roots[i]=new GameObject("Tree "+i).transform;roots[i].SetParent(arena,false);roots[i].localPosition=trees[i].Position-Vector3.up*.7f;
                Part(roots[i],new Vector3(0,1.48f,0),new Vector3(1.16f,.96f,1.16f)); // crown
                Part(roots[i],new Vector3(0,.5f,0),new Vector3(.18f,1,.18f));        // trunk
                targets[i]=arena.TransformPoint(trees[i].Position);
            }
            return go;
        }
        static void Part(Transform root,Vector3 at,Vector3 size)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Cube);Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(root,false);part.transform.localPosition=at;part.transform.localScale=size;
        }
        // Left water palm 16 cm above the board top, 18 cm before the front row and 5 cm to the left of the column.
        static Vector3 Palm(Transform arena)
        {
            var flat=Vector3.ProjectOnPlane(arena.forward,Vector3.up).normalized;
            return arena.TransformPoint(new Vector3(0,0,3))+Vector3.up*.16f-flat*.18f-Vector3.Cross(Vector3.up,flat)*.05f;
        }

        [Test] public void TheBackTreesRootGetsAnArcThatClearsTheFrontTree([Values(0f,30f)] float tilt)
        {
            var go=Board(tilt,out var arena,out var roots,out var trees,out var targets);
            try
            {
                var aim=new EmberdeepPalmAim();aim.Build(arena,roots);
                Assert.AreEqual(1,aim[0].CrownLow,.02f,"Measured crown bottom");Assert.AreEqual(1.96f,aim[0].Top,.02f);
                var origin=Palm(arena);var root=arena.TransformPoint(new Vector3(0,.04f,4.15f-.2f)); // the near side of its root flare
                if(tilt==0)
                {
                    // Before: the plain arc to the root splashes on the front tree, out of the back tree's reach.
                    Assert.True(WaterJet.Predict(origin,WaterJet.AimVelocity(origin,root,Speed),Range,arena,targets,out var blocked));
                    Assert.AreEqual(0,blocked.Tree,"The front tree's sphere catches the low arc");
                    Assert.False(FireRun.WaterReaches(trees[1],arena.InverseTransformPoint(blocked.Point),Radius));
                }
                // After: the palm ray picks the back tree (flat board: the ray passes under the front crown; tilted, the same
                // palm sits higher over the board and its ray goes through that crown, so only the arc is checked there),
                // and its arc waters it.
                if(tilt==0)Assert.AreEqual(1,aim.Pick(arena,origin,(root-origin).normalized,Range,trees));
                Assert.True(EmberdeepPalmAim.ArcTarget(origin,trees[1],arena,targets,Speed,Range,Radius,out var target));
                Assert.True(WaterJet.Predict(origin,WaterJet.AimVelocity(origin,target,Speed),Range,arena,targets,out var hit));
                Assert.True(FireRun.WaterReaches(trees[1],arena.InverseTransformPoint(hit.Point),Radius),"The snapped arc lands on the back tree");
            }
            finally{Object.DestroyImmediate(go);}
        }

        // Headset test 3 (2026-11-22): a burning sheep 1.05 behind the front tree (a sheep home behind the field). Its body,
        // or just over the tree hiding it, picks it; its arc puts it out; a calm or held sheep is never a target; a dead-centre
        // aim at the crown in front keeps the tree.
        [Test] public void ABurningSheepBehindATreeIsPickedAndItsArcPutsItOut([Values(0f,30f)] float tilt)
        {
            var go=Board(tilt,out var arena,out var roots,out var trees,out _);
            try
            {
                // A seated palm 32 cm over the board top (the real ones sit 30-40 cm over it; Palm() is a low 16 cm).
                var front=new[]{trees[0]};var aim=new EmberdeepPalmAim();aim.Build(arena,new[]{roots[0]});var origin=Palm(arena)+Vector3.up*.16f;
                var sheep=new[]{new FireRun.Animal{Position=new Vector3(0,0,4.05f),OnFire=true,State=AnimalState.Waiting}};
                // Water targets as FireRescueGame.UpdateTargets: the tree, then the walking sheep's body sphere.
                var targets=new[]{arena.TransformPoint(trees[0].Position),arena.TransformPoint(sheep[0].Position+Vector3.up*FireRun.AnimalHeight)};
                int sheepTarget=front.Length;
                Assert.True(EmberdeepPalmAim.IsSheep(sheepTarget,front.Length,out int index));Assert.AreEqual(0,index);
                Assert.False(EmberdeepPalmAim.IsSheep(0,front.Length,out _));
                var over=(arena.TransformPoint(new Vector3(0,2.15f,4.05f))-origin).normalized;
                // Arc first (both boards): it lands on the sheep, over the front tree's water sphere.
                Assert.True(EmberdeepPalmAim.ArcTargetSheep(origin,sheep[0],arena,targets,Speed,Range,Radius,out var target));
                Assert.True(WaterJet.Predict(origin,WaterJet.AimVelocity(origin,target,Speed),Range,arena,targets,out var hit));
                Assert.True(FireRun.WaterReachesSheep(sheep[0],arena.InverseTransformPoint(hit.Point),Radius),"the snapped arc puts it out");
                if(tilt!=0)return; // the picks below are laid out on the flat board (as the root test above)
                Assert.AreEqual(sheepTarget,aim.Pick(arena,origin,over,Range,front,-1,null,sheep),"just over the tree hiding it");
                Assert.AreEqual(-1,aim.Pick(arena,origin,over,Range,front),"tree-only assist: nothing there");
                var body=(arena.TransformPoint(new Vector3(.15f,.3f,4.05f-.2f))-origin).normalized;
                Assert.AreEqual(sheepTarget,aim.Pick(arena,origin,body,Range,front,-1,null,sheep),"its body: the ray clips the front crown off centre, then runs dead centre into the sheep");
                // Dead centre through the front crown: the tree keeps it (the ray runs on into the sheep behind).
                var crown=(arena.TransformPoint(new Vector3(0,1.5f,3))-origin).normalized;
                Assert.AreEqual(0,aim.Pick(arena,origin,crown,Range,front,-1,null,sheep));
                // Calm, held or lost sheep are not targets.
                sheep[0].OnFire=false;Assert.AreEqual(-1,aim.Pick(arena,origin,over,Range,front,-1,null,sheep));sheep[0].OnFire=true;
                sheep[0].HeldBy=1;Assert.AreEqual(-1,aim.Pick(arena,origin,over,Range,front,-1,null,sheep));sheep[0].HeldBy=-1;
                sheep[0].State=AnimalState.Lost;Assert.AreEqual(-1,aim.Pick(arena,origin,over,Range,front,-1,null,sheep));
            }
            finally{Object.DestroyImmediate(go);}
        }

        [Test] public void CrownsAndFlamesCountBareBoardDoesNotAndBurntTreesAreSeeThrough()
        {
            var go=Board(0,out var arena,out var roots,out var trees,out _);
            try
            {
                var aim=new EmberdeepPalmAim();aim.Build(arena,roots);var origin=Palm(arena);
                var front=new[]{trees[0]};
                // Into the front crown (on the ground plane this ray would land rows behind the tree).
                var crown=arena.TransformPoint(new Vector3(-.3f,1.6f,3));
                Assert.AreEqual(0,aim.Pick(arena,origin,(crown-origin).normalized,Range,front));
                // Just over the crown: a calm tree does not count, a burning one does (its flames and pin).
                var over=(arena.TransformPoint(new Vector3(0,2.2f,3))-origin).normalized;
                Assert.AreEqual(-1,aim.Pick(arena,origin,over,Range,front));
                trees[0].State=TreeState.Burning;Assert.AreEqual(0,aim.Pick(arena,origin,over,Range,front));trees[0].State=TreeState.Safe;
                // Between the columns, at bare board: no snap.
                var gap=arena.TransformPoint(new Vector3(.75f,0,3.6f));
                Assert.AreEqual(-1,aim.Pick(arena,origin,(gap-origin).normalized,Range,trees));
                // A burnt front tree does not take the snap from the tree behind it.
                trees[0].State=TreeState.Burnt;trees[1].State=TreeState.Safe;
                var back=arena.TransformPoint(new Vector3(0,1.3f,4.15f));
                Assert.AreEqual(1,aim.Pick(arena,origin,(back-origin).normalized,Range,trees));
            }
            finally{Object.DestroyImmediate(go);}
        }
    }
}

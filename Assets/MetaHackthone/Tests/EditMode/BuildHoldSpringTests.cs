using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace PhysicalAssembly.Tests
{
    public sealed class BuildHoldSpringTests
    {
        readonly List<GameObject> objects=new();
        readonly Vector3 origin=new(100,100,100);
        AssemblyBody Body(Vector3 offset,Vector3 size)
        {
            var go=new GameObject("Spring test solid"); objects.Add(go); go.transform.position=origin+offset;
            go.AddComponent<BoxCollider>().size=size;
            var body=go.AddComponent<AssemblyBody>(); body.Initialize(); body.Rigidbody.isKinematic=true; body.Rigidbody.useGravity=false;
            return body;
        }
        static Pose At(AssemblyBody body) => new(body.Rigidbody.position,body.Rigidbody.rotation);
        [TearDown] public void Cleanup() { foreach(var go in objects) Object.DestroyImmediate(go); objects.Clear(); }

        [Test] public void FreeMotionTracksImmediatelyAndBudgetLimitDoesNotActivateSpring()
        {
            var body=Body(Vector3.zero,Vector3.one*.1f); var group=new BuildMotionConstraint.Group(new[]{body},At(body));
            var spring=new BuildHoldSpring(At(body)); var settings=new BuildSpringSettings();
            var near=new Pose(origin+Vector3.up*.03f,Quaternion.Euler(0,10,0));
            var result=spring.Tick(near,.02f,settings,group,_=>false);
            Assert.IsTrue(result.Reached); Assert.IsFalse(spring.Recovering);
            var far=new Pose(origin+Vector3.up*4,near.rotation);
            result=spring.Tick(far,.02f,settings,group,_=>false);
            Assert.IsTrue(result.BudgetLimited); Assert.IsFalse(result.Contact); Assert.IsFalse(spring.Recovering);
            Assert.Less(Vector3.Distance(near.position,spring.Pose.position),.193f);
        }

        [TestCase(.01f)] [TestCase(.02f)] [TestCase(.05f)]
        public void ContactStopsThenRecoveryIsBoundedAndSettles(float dt)
        {
            var body=Body(Vector3.left*.3f,Vector3.one*.1f); var obstacle=Body(Vector3.zero,Vector3.one*.1f);
            var group=new BuildMotionConstraint.Group(new[]{body},At(body)); var spring=new BuildHoldSpring(At(body)); var settings=new BuildSpringSettings();
            bool Blocks(Collider c)=>c==obstacle.Colliders[0];
            var blocked=new Pose(origin+Vector3.right*3,Quaternion.identity);
            for(int i=0;i<80;i++) spring.Tick(blocked,dt,settings,group,Blocks);
            Assert.IsTrue(spring.Recovering); Assert.Less(spring.Pose.position.x,origin.x-.099f);
            Assert.Less(spring.Velocity.magnitude,.001f,"No stored velocity into the blocker.");
            Assert.AreEqual(origin,obstacle.Rigidbody.position,"Held motion cannot push the core.");
            var target=new Pose(origin+Vector3.left*.3f,Quaternion.Euler(0,70,0));
            var before=spring.Pose; spring.Tick(target,dt,settings,group,Blocks);
            Assert.Greater(Vector3.Distance(target.position,spring.Pose.position),.1f,"Recovery must not teleport.");
            for(int i=0;i<Mathf.CeilToInt(2/dt);i++)
            {
                before=spring.Pose; spring.Tick(target,dt,settings,group,Blocks);
                Assert.LessOrEqual(Vector3.Distance(before.position,spring.Pose.position),settings.MaximumSpeed*dt+.001f);
                Assert.LessOrEqual(Quaternion.Angle(before.rotation,spring.Pose.rotation),settings.MaximumAngularSpeed*dt+.1f);
                Assert.Less(spring.Pose.position.x,origin.x-.099f);
            }
            Assert.IsFalse(spring.Recovering);
            Assert.Less(Vector3.Distance(target.position,spring.Pose.position),.001f);
            Assert.Less(Quaternion.Angle(target.rotation,spring.Pose.rotation),1);
        }

        [Test] public void ContactAllowsSlidingAndIgnoredSolidsDoNotBlock()
        {
            var body=Body(Vector3.left*.3f,Vector3.one*.1f); var obstacle=Body(Vector3.zero,new Vector3(.1f,2,2));
            Body(Vector3.left*.2f,Vector3.one*.15f);
            var group=new BuildMotionConstraint.Group(new[]{body},At(body)); var spring=new BuildHoldSpring(At(body));
            bool Blocks(Collider c)=>c==obstacle.Colliders[0];
            var settings=new BuildSpringSettings(); var target=new Pose(origin+new Vector3(.3f,.2f,0),Quaternion.identity);
            for(int i=0;i<90;i++) spring.Tick(target,.02f,settings,group,Blocks);
            Assert.Less(spring.Pose.position.x,origin.x-.099f);
            Assert.That(spring.Pose.position.y,Is.EqualTo(target.position.y).Within(.001f));
            obstacle.Colliders[0].enabled=false;
            for(int i=0;i<90;i++) spring.Tick(target,.02f,settings,group,Blocks);
            Assert.Less(Vector3.Distance(target.position,spring.Pose.position),.001f);
        }

        [Test] public void RotationCannotSweepLongBlockThroughSolid()
        {
            var body=Body(Vector3.left*.16f,new Vector3(.06f,.06f,.3f)); var obstacle=Body(Vector3.zero,Vector3.one*.1f);
            var start=At(body); var group=new BuildMotionConstraint.Group(new[]{body},start); var spring=new BuildHoldSpring(start);
            for(int i=0;i<60;i++) spring.Tick(new Pose(start.position,Quaternion.Euler(0,90,0)),.02f,new BuildSpringSettings(),group,c=>c==obstacle.Colliders[0]);
            Assert.IsTrue(spring.Recovering); Assert.Less(Quaternion.Angle(start.rotation,spring.Pose.rotation),80);
            bool overlap=Physics.ComputePenetration(body.Colliders[0],body.transform.position,body.transform.rotation,obstacle.Colliders[0],obstacle.transform.position,obstacle.transform.rotation,out _,out float depth);
            Assert.IsTrue(!overlap || depth<.001f);
            for(int i=0;i<100;i++) spring.Tick(start,.02f,new BuildSpringSettings(),group,c=>c==obstacle.Colliders[0]);
            Assert.Less(Quaternion.Angle(start.rotation,spring.Pose.rotation),1);
        }

        [Test] public void BranchKeepsRigidOffsetsDuringBudgetLimitedRotation()
        {
            var a=Body(Vector3.zero,Vector3.one*.1f); var b=Body(Vector3.up*.3f,Vector3.one*.1f);
            var start=At(a); var group=new BuildMotionConstraint.Group(new[]{a,b},start);
            var target=new Pose(start.position,Quaternion.Euler(120,75,30)); var current=start;
            for(int i=0;i<20;i++)
            {
                var result=group.Move(current,target,_=>false,true); current=result.Pose;
                Assert.Less(Vector3.Distance(b.Rigidbody.position,a.Rigidbody.position+current.rotation*Vector3.up*.3f),.0001f);
                Assert.Less(Quaternion.Angle(a.Rigidbody.rotation,b.Rigidbody.rotation),.05f);
            }
            Assert.Less(Quaternion.Angle(current.rotation,target.rotation),.05f);
        }

        [Test] public void NewBlockerOverlappingHeldPartAllowsEscape()
        {
            var body=Body(Vector3.left*.06f,Vector3.one*.1f); var obstacle=Body(Vector3.zero,Vector3.one*.1f);
            var start=At(body); var group=new BuildMotionConstraint.Group(new[]{body},start);
            var deeper=group.Move(start,new Pose(origin,Quaternion.identity),c=>c==obstacle.Colliders[0],true);
            Assert.IsTrue(deeper.Contact); Assert.LessOrEqual(deeper.Pose.position.x,start.position.x+.0001f);
            var escaped=group.Move(deeper.Pose,new Pose(origin+Vector3.left*.2f,Quaternion.identity),c=>c==obstacle.Colliders[0],true);
            Assert.IsTrue(escaped.Reached);
        }

        [Test] public void DefaultPolicyStillBlocksEnvironmentInOtherWorlds()
        {
            var body=Body(Vector3.left*.15f,Vector3.one*.1f); Body(Vector3.zero,Vector3.one*.1f);
            Assert.IsFalse(BuildMotionConstraint.Move(new[]{body},new[]{origin+Vector3.right*.15f},new[]{Quaternion.identity}));
            Assert.Less(body.Rigidbody.position.x,origin.x-.099f);
        }
    }
}

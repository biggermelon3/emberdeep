using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace CloudGarden.Integration
{
    // Read native joint validity before the SDK converts it into a full hand
    // skeleton (which can retain an old joint pose after TryGetPose fails).
    // No guessed poses, grace-period clicks, pointer objects or SDK events.
    public sealed class PortableXRHandInput : IDisposable
    {
        public struct Sample
        {
            public bool AimValid,PinchAvailable;
            public Pose Wrist;
            public Vector3 IndexBase,ThumbBase,IndexTip,ThumbTip;
            public float Pinch=>Vector3.Distance(IndexTip,ThumbTip);
            public Sample ToWorld(Transform space)
            {
                var result=this;
                result.Wrist=new Pose(space.TransformPoint(Wrist.position),space.rotation*Wrist.rotation);
                result.IndexBase=space.TransformPoint(IndexBase);result.ThumbBase=space.TransformPoint(ThumbBase);
                result.IndexTip=space.TransformPoint(IndexTip);result.ThumbTip=space.TransformPoint(ThumbTip);
                return result;
            }
        }
        readonly List<XRHandSubsystem> found=new();
        readonly Sample[] samples=new Sample[2];
        readonly int[] frames={-1,-1};
        readonly float[] times=new float[2];
        XRHandSubsystem subsystem;
        public void Bind()
        {
            if(subsystem!=null && subsystem.running)return;
            Dispose();SubsystemManager.GetSubsystems(found);
            foreach(var candidate in found)if(candidate.running){subsystem=candidate;break;}
            if(subsystem==null)return;
            subsystem.updatedHands+=Updated;subsystem.trackingLost+=Lost;
        }
        void Updated(XRHandSubsystem source,XRHandSubsystem.UpdateSuccessFlags flags,XRHandSubsystem.UpdateType type)
        {
            if(type!=XRHandSubsystem.UpdateType.Dynamic)return;
            Record(0,Read(source.leftHand,(flags & XRHandSubsystem.UpdateSuccessFlags.LeftHandJoints)!=0),Time.frameCount,Time.unscaledTime);
            Record(1,Read(source.rightHand,(flags & XRHandSubsystem.UpdateSuccessFlags.RightHandJoints)!=0),Time.frameCount,Time.unscaledTime);
        }
        static Sample Read(XRHand hand,bool jointsUpdated)
        {
            if(!jointsUpdated || !hand.isTracked)return default;
            bool wristValid=hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out var wrist);
            bool indexValid=hand.GetJoint(XRHandJointID.IndexProximal).TryGetPose(out var index);
            bool thumbValid=hand.GetJoint(XRHandJointID.ThumbProximal).TryGetPose(out var thumb);
            bool indexTipValid=hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var indexTip);
            bool thumbTipValid=hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumbTip);
            return FromJoints(wristValid,wrist,indexValid,index.position,thumbValid,thumb.position,
                indexTipValid,indexTip.position,thumbTipValid,thumbTip.position);
        }
        public static Sample FromJoints(bool wristValid,Pose wrist,bool indexValid,Vector3 index,bool thumbValid,Vector3 thumb,
            bool indexTipValid,Vector3 indexTip,bool thumbTipValid,Vector3 thumbTip)
        {
            var q=wrist.rotation;float norm=q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w;
            bool aim=wristValid && indexValid && thumbValid && Finite(wrist.position) && Finite(index) && Finite(thumb)
                && norm>=.5f && norm<=1.5f;
            bool pinch=aim && indexTipValid && thumbTipValid && Finite(indexTip) && Finite(thumbTip);
            return new Sample{AimValid=aim,PinchAvailable=pinch,Wrist=wrist,IndexBase=index,ThumbBase=thumb,
                IndexTip=indexTip,ThumbTip=thumbTip};
        }
        public void Record(int hand,Sample sample,int frame,float time)
        {
            if(frame<0 || !float.IsFinite(time)){Invalidate(hand);return;}
            samples[hand]=sample;frames[hand]=frame;times[hand]=time;
        }
        public bool TryRead(bool left,Transform space,out Sample sample)
        {
            Bind();sample=default;
            if(subsystem==null || !subsystem.running || !space)return false;
            if(!TryReadFresh(left?0:1,Time.frameCount,Time.unscaledTime,out var local))return false;
            sample=local.ToWorld(space);return true;
        }
        // One previous frame permits Update/LateUpdate ordering; an explicit
        // tracking-lost or invalid-joint update clears immediately.
        public bool TryReadFresh(int hand,int frame,float time,out Sample sample)
        {
            sample=default;
            if(frames[hand]<0 || frame<frames[hand] || frame-frames[hand]>1 || !float.IsFinite(time)
                || time<times[hand] || time-times[hand]>.08f || !samples[hand].AimValid)return false;
            sample=samples[hand];return true;
        }
        void Lost(XRHand hand)=>Invalidate(hand.handedness==UnityEngine.XR.Hands.Handedness.Left?0:1);
        public void Invalidate(int hand){samples[hand]=default;frames[hand]=-1;}
        public void Invalidate(){Invalidate(0);Invalidate(1);}
        public void Dispose()
        {
            if(subsystem!=null){subsystem.updatedHands-=Updated;subsystem.trackingLost-=Lost;subsystem=null;}
            Invalidate();
        }
        static bool Finite(Vector3 value)=>float.IsFinite(value.sqrMagnitude);
    }
}

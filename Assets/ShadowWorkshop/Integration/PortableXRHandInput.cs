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
            public bool GripAvailable,OpenPalm;
            public float GripStrength;
            public Vector3 GripContact;
            public Quaternion GripRotation;
            public Pose Wrist;
            public Vector3 IndexBase,ThumbBase,IndexTip,ThumbTip;
            public Vector3 MiddleTip,RingTip,LittleTip;
            public int TipMask;
            // Per-finger curl (index, middle, ring, little) for gesture spells.
            public bool CurlsValid;
            public float IndexCurl,MiddleCurl,RingCurl,LittleCurl;
            public bool TryTip(int finger,out Vector3 tip)
            {tip=finger switch{0=>ThumbTip,1=>IndexTip,2=>MiddleTip,3=>RingTip,4=>LittleTip,_=>default};return finger>=0 && finger<5 && (TipMask&(1<<finger))!=0 && Finite(tip);}
            public float Pinch=>Vector3.Distance(IndexTip,ThumbTip);
            public Sample ToWorld(Transform space)
            {
                var result=this;
                result.Wrist=new Pose(space.TransformPoint(Wrist.position),space.rotation*Wrist.rotation);
                result.IndexBase=space.TransformPoint(IndexBase);result.ThumbBase=space.TransformPoint(ThumbBase);
                result.IndexTip=space.TransformPoint(IndexTip);result.ThumbTip=space.TransformPoint(ThumbTip);
                result.MiddleTip=space.TransformPoint(MiddleTip);result.RingTip=space.TransformPoint(RingTip);result.LittleTip=space.TransformPoint(LittleTip);
                result.GripContact=space.TransformPoint(GripContact);
                result.GripRotation=space.rotation*GripRotation;
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
            var sample=FromJoints(wristValid,wrist,indexValid,index.position,thumbValid,thumb.position,
                indexTipValid,indexTip.position,thumbTipValid,thumbTip.position);
            sample.GripAvailable=sample.AimValid && TryGrip(hand,out sample.GripStrength,out sample.GripContact,out sample.GripRotation);
            sample.OpenPalm=sample.GripAvailable && IsOpenPalm(hand);
            sample.TipMask=(thumbTipValid?1:0)|(indexTipValid?2:0);
            if(hand.GetJoint(XRHandJointID.MiddleTip).TryGetPose(out var middleTip)){sample.MiddleTip=middleTip.position;sample.TipMask|=4;}
            if(hand.GetJoint(XRHandJointID.RingTip).TryGetPose(out var ringTip)){sample.RingTip=ringTip.position;sample.TipMask|=8;}
            if(hand.GetJoint(XRHandJointID.LittleTip).TryGetPose(out var littleTip)){sample.LittleTip=littleTip.position;sample.TipMask|=16;}
            sample.CurlsValid=TryCurl(hand,0,out sample.IndexCurl) & TryCurl(hand,1,out sample.MiddleCurl)
                & TryCurl(hand,2,out sample.RingCurl) & TryCurl(hand,3,out sample.LittleCurl);
            return sample;
        }
        static readonly XRHandJointID[] roots=new[]{XRHandJointID.IndexProximal,XRHandJointID.MiddleProximal,XRHandJointID.RingProximal,XRHandJointID.LittleProximal};
        static readonly XRHandJointID[] mids=new[]{XRHandJointID.IndexIntermediate,XRHandJointID.MiddleIntermediate,XRHandJointID.RingIntermediate,XRHandJointID.LittleIntermediate};
        static readonly XRHandJointID[] tips=new[]{XRHandJointID.IndexTip,XRHandJointID.MiddleTip,XRHandJointID.RingTip,XRHandJointID.LittleTip};
        static bool TryCurl(XRHand hand,int finger,out float curl)
        {
            curl=0;
            if(!hand.GetJoint(roots[finger]).TryGetPose(out var a) || !hand.GetJoint(mids[finger]).TryGetPose(out var b)
                || !hand.GetJoint(tips[finger]).TryGetPose(out var c) || !Finite(a.position) || !Finite(b.position) || !Finite(c.position)
                || (b.position-a.position).sqrMagnitude<1e-8f || (c.position-b.position).sqrMagnitude<1e-8f)return false;
            curl=PhysicalAssembly.GrabLatch.FingerCurl(a.position,b.position,c.position);return true;
        }
        static bool IsOpenPalm(XRHand hand)
        {
            for(int i=0;i<4;i++)
                if(!hand.GetJoint(roots[i]).TryGetPose(out var a) || !hand.GetJoint(mids[i]).TryGetPose(out var b)
                    || !hand.GetJoint(tips[i]).TryGetPose(out var c) || !Open(a.position,b.position,c.position,.24f))return false;
            return hand.GetJoint(XRHandJointID.ThumbProximal).TryGetPose(out var ta)
                && hand.GetJoint(XRHandJointID.ThumbDistal).TryGetPose(out var tb)
                && hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var tc) && Open(ta.position,tb.position,tc.position,.3f);
        }
        static bool Open(Vector3 a,Vector3 b,Vector3 c,float limit)=>Finite(a) && Finite(b) && Finite(c)
            && (b-a).sqrMagnitude>1e-8f && (c-b).sqrMagnitude>1e-8f && PhysicalAssembly.GrabLatch.FingerCurl(a,b,c)<limit;
        static bool TryGrip(XRHand hand,out float strength,out Vector3 contact,out Quaternion rotation)
        {
            strength=0;contact=default;rotation=Quaternion.identity;
            if(!hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out var wrist)
                || !hand.GetJoint(XRHandJointID.MiddleProximal).TryGetPose(out var middle))return false;
            contact=(wrist.position+middle.position)*.5f;
            var distal=middle.position-wrist.position;
            // Unity XR Hands/OpenXR wrist coordinates: palm faces local -Y.
            var palmar=wrist.rotation*Vector3.down;
            if(!Finite(distal) || Vector3.Cross(distal,palmar).sqrMagnitude<.000001f)return false;
            rotation=Quaternion.LookRotation(distal,palmar);
            float total=0;
            for(int i=0;i<4;i++)
            {
                if(!hand.GetJoint(roots[i]).TryGetPose(out var a) || !hand.GetJoint(mids[i]).TryGetPose(out var b)
                    || !hand.GetJoint(tips[i]).TryGetPose(out var c) || !Finite(a.position) || !Finite(b.position) || !Finite(c.position)
                    || (b.position-a.position).sqrMagnitude<1e-8f || (c.position-b.position).sqrMagnitude<1e-8f)return false;
                total+=PhysicalAssembly.GrabLatch.FingerCurl(a.position,b.position,c.position);
            }
            strength=total/4;return Finite(contact);
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

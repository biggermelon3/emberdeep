using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Utilities;

namespace CloudGarden.Integration
{
    // The runtime's own hand aim on the portable OpenXR rig: XR_EXT_hand_interaction's /input/aim/pose and its
    // /input/aim_activate_ext select, read through Unity OpenXR's Hand Interaction Profile (device layout
    // "HandInteraction"). PICO 4 Ultra's runtime lists XR_EXT_hand_interaction v2 but not XR_FB_hand_tracking_aim
    // (Unity XR Hands' MetaAimHand), so this is PICO's system ray. Poses are in tracking space, like XR Hands joints.
    //
    // Unity's HandInteraction device is an XRController with the LeftHand/RightHand usage, so every
    // <XRController>{LeftHand} binding (ISDK's FromUnityXRControllerDataSource on the UnityXR rig) would take a tracked
    // hand for a connected controller. From startup, each such device trades its hand usage for Usages[hand] in its own
    // Added callback, before any state reaches it. Nothing else is touched: other devices keep their usages.
    public sealed class PortableSystemAim
    {
        public const string Layout="HandInteraction";
        public static readonly string[] Usages={"EmberdeepAimLeft","EmberdeepAimRight"};
        // aim_activate value hysteresis: the select closes at SelectClose and opens again at SelectOpen. SelectOpen matches
        // Emberdeep's click release (EmberdeepPinchClick.ReleaseValue, .6): an ordinary release drops the pressed state,
        // so the next pinch gives a new pressed edge (was .5, which needed a wider opening; headset test 2, 2026-11-22).
        // Only Emberdeep reads this (MetaShadowPlayer.IndependentPortableAim).
        public const float SelectClose=.8f,SelectOpen=.6f;
        // Light smoothing only (One Euro: Hz at rest, per unit of speed). The runtime already filters its aim; this
        // just takes the edge off tracking noise without a visible lag in a sweep.
        public const float OriginCutoff=6,OriginBeta=10,DirectionCutoff=4,DirectionBeta=6;
        public enum Report
        {
            // No system aim for this hand (no device, or it has never tracked): the caller may fall back.
            Unreported,
            // The system has aimed this hand before and now reports it untracked: no ray, as with Meta's pointer pose.
            Lost,
            Tracked,
        }
        // One hand's runtime values, in tracking space.
        public struct Raw
        {
            public bool Tracked;public Pose Aim;public int TrackingState;
            public bool SelectAvailable,SelectReady;public float Select;
        }
        public struct Reading
        {
            // Aim: lightly smoothed, world space. RawAim: unsmoothed, world space.
            public Pose Aim,RawAim;public int TrackingState;
            // SelectAvailable: the device reports aim_activate. SelectProven: some hand has closed it this session.
            public bool SelectAvailable,SelectProven,SelectReady,SelectPressed;public float Select;
        }

        static readonly InputDevice[] devices=new InputDevice[2];
        static bool installed;
        public static bool Installed=>installed;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Install()
        {
            InputSystem.onDeviceChange-=Changed;InputSystem.onDeviceChange+=Changed;installed=true;
            devices[0]=devices[1]=null;
            foreach(var device in InputSystem.devices)Adopt(device);
        }
        public static void Uninstall(){InputSystem.onDeviceChange-=Changed;installed=false;devices[0]=devices[1]=null;}
        public static InputDevice Device(bool left)=>devices[left?0:1];
        static void Changed(InputDevice device,InputDeviceChange change)
        {
            switch(change)
            {
                case InputDeviceChange.Added:case InputDeviceChange.Reconnected:case InputDeviceChange.UsageChanged:Adopt(device);break;
                case InputDeviceChange.Removed:case InputDeviceChange.Disconnected:
                    for(int i=0;i<2;i++)if(devices[i]==device)devices[i]=null;break;
            }
        }
        public static bool IsHandInteraction(InputDevice device)
        {
            if(device==null)return false;
            if(device.layout==Layout)return true;
            try{return InputSystem.IsFirstLayoutBasedOnSecond(device.layout,Layout);}catch{return false;}
        }
        // Takes a HandInteraction device off the controller usages; returns its hand, or -1 for any other device.
        public static int Adopt(InputDevice device)
        {
            if(!IsHandInteraction(device))return -1;
            int hand=HandOf(device);if(hand<0)return -1;
            var usages=device.usages;
            if(usages.Count!=1 || usages[0]!=new InternedString(Usages[hand]))InputSystem.SetDeviceUsage(device,Usages[hand]);
            devices[hand]=device;devices[1-hand]=devices[1-hand]==device?null:devices[1-hand];
            return hand;
        }
        static int HandOf(InputDevice device)
        {
            foreach(var usage in device.usages)
            {
                if(usage==CommonUsages.LeftHand || usage==new InternedString(Usages[0]))return 0;
                if(usage==CommonUsages.RightHand || usage==new InternedString(Usages[1]))return 1;
            }
            try
            {
                var descriptor=UnityEngine.InputSystem.XR.XRDeviceDescriptor.FromJson(device.description.capabilities);
                if(descriptor!=null)
                {
                    if((descriptor.characteristics & UnityEngine.XR.InputDeviceCharacteristics.Left)!=0)return 0;
                    if((descriptor.characteristics & UnityEngine.XR.InputDeviceCharacteristics.Right)!=0)return 1;
                }
            }
            catch{}
            return -1;
        }
        // The device's current values, if this hand has a HandInteraction device. Reads the pose's child controls by
        // path, so either PoseControl type (USE_INPUT_SYSTEM_POSE_CONTROL or not) works.
        public static bool TryReadRaw(bool left,out Raw raw)
        {
            raw=default;
            if(!installed)Install();
            var device=devices[left?0:1];
            return device!=null && device.added && device.enabled && TryReadRaw(device,out raw);
        }
        public static bool TryReadRaw(InputDevice device,out Raw raw)
        {
            raw=default;
            var position=device.TryGetChildControl<Vector3Control>("pointer/position");
            var rotation=device.TryGetChildControl<QuaternionControl>("pointer/rotation");
            if(position==null || rotation==null)return false;
            var tracked=device.TryGetChildControl<ButtonControl>("pointer/isTracked");
            var state=device.TryGetChildControl<IntegerControl>("pointer/trackingState");
            raw.TrackingState=state!=null?state.ReadValue():0;
            var p=position.ReadValue();var q=rotation.ReadValue();
            const int both=(int)(UnityEngine.XR.InputTrackingState.Position|UnityEngine.XR.InputTrackingState.Rotation);
            float norm=q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w;
            // isTracked is a byte (PoseState's bool): true reads as 1/255, under ButtonControl's press point, so any
            // non-zero value counts.
            raw.Tracked=(tracked==null || tracked.ReadValue()>0) && (raw.TrackingState&both)==both
                && float.IsFinite(p.sqrMagnitude) && float.IsFinite(norm) && norm>.5f && norm<1.5f;
            raw.Aim=new Pose(p,raw.Tracked?Quaternion.Normalize(q):Quaternion.identity);
            var value=device.TryGetChildControl<AxisControl>("pointerActivateValue");
            var ready=device.TryGetChildControl<ButtonControl>("pointerActivateReady");
            if(value!=null){float v=value.ReadValue();raw.SelectAvailable=float.IsFinite(v);raw.Select=raw.SelectAvailable?Mathf.Clamp01(v):0;}
            raw.SelectReady=ready!=null && ready.ReadValue()>0;
            return true;
        }

        readonly OneEuro[] origins={new(OriginCutoff,OriginBeta),new(OriginCutoff,OriginBeta)};
        readonly OneEuro[] directions={new(DirectionCutoff,DirectionBeta),new(DirectionCutoff,DirectionBeta)};
        readonly bool[] seen=new bool[2],pressed=new bool[2];
        readonly int[] frames={-1,-1};
        readonly Report[] reports=new Report[2];
        readonly Reading[] readings=new Reading[2];
        // Once any hand has closed the system select this session, it drives clicks; until then the caller keeps its
        // own pinch (a runtime that offers the value but never moves it cannot lock clicks out).
        public bool SelectProven{get;private set;}
        public Report Read(bool left,Transform space,int frame,float dt,out Reading reading)
        {
            int hand=left?0:1;
            if(frame>=0 && frame==frames[hand]){reading=readings[hand];return reports[hand];}
            bool reported=TryReadRaw(left,out var raw);
            return Sample(hand,reported,raw,space,frame,dt,out reading);
        }
        // The reading for one frame's runtime values (Read without the device, for tests).
        public Report Sample(int hand,bool reported,Raw raw,Transform space,int frame,float dt,out Reading reading)
        {
            reading=default;
            if(frame<frames[hand] || frame-frames[hand]>1)ResetFilters(hand);
            frames[hand]=frame;
            Report report;
            if(!reported || !space){seen[hand]=false;pressed[hand]=false;ResetFilters(hand);report=Report.Unreported;}
            else if(!raw.Tracked){pressed[hand]=false;ResetFilters(hand);report=seen[hand]?Report.Lost:Report.Unreported;}
            else
            {
                seen[hand]=true;report=Report.Tracked;
                var world=new Pose(space.TransformPoint(raw.Aim.position),space.rotation*raw.Aim.rotation);
                dt=Mathf.Clamp(float.IsFinite(dt)?dt:.0001f,.0001f,.05f);
                var at=origins[hand].Filter(world.position,dt);
                var forward=world.rotation*Vector3.forward;
                var direction=directions[hand].Filter(forward,dt);
                if(direction.sqrMagnitude<1e-6f)direction=forward;
                reading.Aim=new Pose(at,Quaternion.LookRotation(direction.normalized,world.rotation*Vector3.up));
                reading.RawAim=world;reading.TrackingState=raw.TrackingState;
                if(raw.SelectAvailable)
                {
                    if(raw.Select>=SelectClose)SelectProven=true;
                    pressed[hand]=pressed[hand]?raw.Select>SelectOpen:raw.Select>=SelectClose;
                }
                else pressed[hand]=false;
                reading.SelectAvailable=raw.SelectAvailable;reading.SelectReady=raw.SelectReady;
                reading.Select=raw.Select;reading.SelectPressed=pressed[hand];
            }
            reading.SelectProven=SelectProven;
            reports[hand]=report;readings[hand]=reading;
            return report;
        }
        void ResetFilters(int hand){origins[hand].Reset();directions[hand].Reset();}
        // Tracking, focus or a system gesture interrupted: the next reading starts fresh (proof is kept).
        public void Reset(bool left){int hand=left?0:1;ResetFilters(hand);pressed[hand]=false;frames[hand]=-1;}
        public void Reset(){Reset(true);Reset(false);}
    }
}

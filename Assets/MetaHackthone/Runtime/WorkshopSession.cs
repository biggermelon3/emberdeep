using System;
using System.Collections.Generic;
using UnityEngine;

namespace PhysicalAssembly
{
    public sealed class WorkshopSession : MonoBehaviour
    {
        public AssemblyWorld World;
        public bool AllowDriving=true;
        public WorkshopMode Mode { get; private set; }
        public AssemblyPart Selected, Vehicle;
        public HandCommand Left,Right;
        public string Status { get; private set; }="Grab parts. Release near a highlighted connection to attach.";
        public int Score { get; private set; }
        readonly List<IAssemblyControlReceiver> receivers=new();
        public event Action Changed;
        void Start() { World.TopologyChanged+=OnTopologyChanged; World.Feedback+=Notify; }
        public void Notify(string text) { Status=text; Changed?.Invoke(); }
        public void Select(AssemblyPart part) { if(part) Selected=part; }
        public void ToggleMode()
        {
            if(!AllowDriving || !World.EditingEnabled) { Notify("Use Launch to test your container, and Retry to build again."); return; }
            if(Mode==WorkshopMode.Drive) { Mode=WorkshopMode.Build; ClearInput(); Notify("Build mode — grab, attach, or detach parts."); return; }
            if(World.IsBusy || World.HasHolds) { Notify("Release both hands and wait for attachment to finish."); return; }
            Vehicle=null;
            if(Selected) foreach(var p in World.GetConnectedParts(Selected.InstanceId)) if(p.Kind==PartKind.Chassis) { Vehicle=p; break; }
            if(!Vehicle) foreach(var p in World.Parts.Values) if(p.Kind==PartKind.Chassis) { Vehicle=p; break; }
            if(!Vehicle) { Notify("Add a chassis before driving."); return; }
            int wheels=0; foreach(var p in World.GetConnectedParts(Vehicle.InstanceId)) if(p.Kind==PartKind.Wheel) wheels++;
            if(wheels<4) { Notify($"Connect four wheels first ({wheels}/4 connected)."); return; }
            ClearInput(); Mode=WorkshopMode.Drive; RebuildReceivers(); Notify("Drive mode — right hand throttle, left hand steering / fire.");
        }
        void OnTopologyChanged() { ClearInput(); RebuildReceivers(); }
        void RebuildReceivers()
        {
            receivers.Clear(); if(!Vehicle || !World.Parts.ContainsKey(Vehicle.InstanceId)) { Vehicle=null; Mode=WorkshopMode.Build; return; }
            foreach(var part in World.GetConnectedParts(Vehicle.InstanceId))
                foreach(var component in part.GetComponents<MonoBehaviour>()) if(component is IAssemblyControlReceiver receiver) receivers.Add(receiver);
        }
        public void ClearInput() { Left=Right=HandCommand.None; }
        public void EnterBuild() { Mode=WorkshopMode.Build; ClearInput(); Changed?.Invoke(); }
        public void AddScore() { Score++; Changed?.Invoke(); }
        public AssemblyControlFrame CurrentFrame()
        {
            float drive=Right==HandCommand.Forward?1:Right==HandCommand.Reverse?-1:0;
            float steer=Left==HandCommand.Left?-1:Left==HandCommand.Right?1:0;
            return new AssemblyControlFrame(drive,steer,Left==HandCommand.Fire,Vehicle);
        }
        void FixedUpdate()
        {
            if(Mode!=WorkshopMode.Drive || !Vehicle || World.IsBusy || World.HasHolds) return;
            var frame=CurrentFrame(); foreach(var receiver in receivers) if(receiver is MonoBehaviour b && b) receiver.ApplyControl(frame,Time.fixedDeltaTime);
        }
        void OnApplicationFocus(bool focus) { if(!focus) { EnterBuild(); World.ReleaseAll(); if(AllowDriving) Notify("Paused — choose Drive to resume."); } }
        void OnApplicationPause(bool pause) { if(pause) { EnterBuild(); World.ReleaseAll(); } }
        void OnDestroy() { if(World) { World.TopologyChanged-=OnTopologyChanged; World.Feedback-=Notify; } }
    }
}

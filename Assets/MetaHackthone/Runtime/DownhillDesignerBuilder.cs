using System;
using System.Linq;
using UnityEngine;

namespace PhysicalAssembly
{
    public sealed partial class DownhillController
    {
        public static string DesignerInstructions => DesignModeSession.Enabled?"DRAG & SNAP\nPinch nearby, or point + pinch to drag.\nGreen connects. Red is blocked.\nUse the 90 degree buttons to rotate.":"DRAG & SNAP\nPinch nearby, or point + pinch to drag.\nGreen connects. Red is blocked.\nSTART begins the countdown.";
        public static Vector3 DesignerOffset(int index) => index switch {
            1=>new Vector3(-.25f,0,-.13f), 2=>new Vector3(-.25f,0,.13f),
            3=>new Vector3(.25f,.04f,-.13f), 4=>new Vector3(.25f,.04f,.13f),
            5=>new Vector3(-.38f,0,0), 6=>new Vector3(.38f,0,0),
            7=>new Vector3(-.14f,0,-.28f), 8=>new Vector3(.14f,0,-.28f), _=>Vector3.zero };
        void SpawnDesignerKit()
        {
            var markers=Workshop.Layout.Spawns;
            bool authored=markers!=null && markers.Length==9 && markers[0] && markers[0].Kind==PartKind.DownhillCore;
            float scale=markers!=null && markers.Length>0 && markers[0]?markers[0].SizeMultiplier:.4f;
            for(int i=0;i<DownhillParts.DesignerKit.Length;i++) {
                var pose=new Pose(Presentation.AssemblyAnchor.TransformPoint(DesignerOffset(i)),Presentation.AssemblyAnchor.rotation);
                if(authored && markers[i]) pose=markers[i].Pose;
                // The authored drill slot overlaps the front wheel. Keep the loose
                // tool in its own workbench space, including while locked in level 1.
                if(Settings.SandboxDrillCourse && i==4) pose=new Pose(Presentation.AssemblyAnchor.TransformPoint(new Vector3(-.25f,.04f,.13f)),Presentation.AssemblyAnchor.rotation);
                var part=DownhillParts.Create(Workshop.World,(Settings.SandboxDrillCourse && i==4?PartKind.DownhillDrill:DownhillParts.DesignerKit[i]),pose,scale,Settings,DownhillParts.DesignerGridVersion,i==0);
                kit.Add(part); spawns[part]=Capture(part.Bodies).ToArray(); if(i==0) Core=part;
            }
            RefreshDesignerKitAvailability();
            TopologyChanged(); RebuildGarageCourse();
            Workshop.Session.Select(null); Workshop.Session.Notify(DesignerInstructions); UpdateControls();
        }
        bool DesignerPartAvailable(PartKind kind) => DesignLevelCatalog.PartAvailable(
            CurrentCourse==SandboxCourseId.GravityGarage?DesignLevelId.GravityGarage:DesignLevelId.WaterfallValley,kind);

        void RefreshDesignerKitAvailability()
        {
            if(!Settings.DesignerBuilder) return;
            // Retain authored slots and identities while keeping locked parts out of
            // physics, grabbing, and socket search until the second course starts.
            foreach(var part in kit)
            {
                bool available=DesignerPartAvailable(part.Kind);
                part.gameObject.SetActive(available);
                bool registered=Workshop.World.Parts.ContainsKey(part.InstanceId);
                if(available && !registered) Workshop.World.RegisterPart(part);
                else if(!available && registered) Workshop.World.UnregisterPart(part);
            }
        }
        void TranslateDesignerKit(Vector3 shift)
        {
            foreach(var p in kit) foreach(var b in p.Bodies) {
                var rb=b.Rigidbody;
                var position=(b.gameObject.activeInHierarchy?rb.position:b.transform.position)+shift;
                var rotation=b.gameObject.activeInHierarchy?rb.rotation:b.transform.rotation;
                b.transform.SetPositionAndRotation(position,rotation);rb.position=position;rb.rotation=rotation;
            }
            Physics.SyncTransforms();
        }
        public bool TryLoadDesignerExample()
        {
            if(!DesignModeSession.Enabled || !Settings.DesignerBuilder || !IsBuilding || !CanSwitchMode || Rotating || !TryResetBuild()) return false;
            var world=Workshop.World;
            bool Join(AssemblyPart source,AssemblyPart target,Vector3 direction,bool far)
            {
                var normal=Presentation.AssemblyAnchor.rotation*direction;
                var sockets=target.Sockets.Where(s=>!world.SocketOccupied(s) && Vector3.Dot(s.Normal,normal)>.99f);
                var socket=(far?sockets.OrderByDescending(s=>Presentation.AssemblyAnchor.InverseTransformPoint(s.WorldPosition).z):sockets.OrderBy(s=>Presentation.AssemblyAnchor.InverseTransformPoint(s.WorldPosition).z)).FirstOrDefault();
                if(!socket) return false;
                var sourceSocket=source.Sockets.Where(s=>!world.SocketOccupied(s)).OrderBy(s=>Vector3.Dot(s.Normal,normal)).FirstOrDefault();
                if(!sourceSocket) return false;
                var candidate=new AttachmentCandidate(sourceSocket.Body,socket.Body,sourceSocket.WorldPosition,socket.WorldPosition,sourceSocket,socket);
                var group=world.GetConnectedParts(source.InstanceId);
                if(!world.PlacementClear(candidate,group)) return false;
                foreach(var b in group.SelectMany(p=>p.Bodies)) {
                    var rb=b.Rigidbody; rb.position=candidate.targetPoint+candidate.rotationDelta*(rb.position-candidate.sourcePoint);
                    rb.rotation=candidate.rotationDelta*rb.rotation; b.transform.SetPositionAndRotation(rb.position,rb.rotation);
                }
                Physics.SyncTransforms();
                return world.Connect(new ConnectionRecord {id=Guid.NewGuid().ToString("N"),a=new BodyRef(source.InstanceId,sourceSocket.Body.Id),b=new BodyRef(target.InstanceId,socket.Body.Id),socketA=sourceSocket.Id,socketB=socket.Id,anchorA=sourceSocket.transform.localPosition,anchorB=socket.transform.localPosition});
            }
            bool level2=DesignModeSession.SelectedLevel==DesignLevelId.WaterfallValley;
            bool creekPreset=!level2 && Settings.Campaign && GarageCourse && GarageCourse.GetComponent<CreekCampaignTheme>();
            bool success=level2
                ? kit[4].Kind==PartKind.DownhillDrill
                    && Join(kit[2],Core,Vector3.forward,false)
                    && Join(kit[4],kit[2],Vector3.forward,false)
                    && Join(kit[5],kit[2],Vector3.right,true) && Join(kit[6],kit[2],Vector3.left,true)
                    && Join(kit[7],Core,Vector3.right,false) && Join(kit[8],Core,Vector3.left,false)
                : creekPreset
                ? Join(kit[5],Core,Vector3.right,false) && Join(kit[6],Core,Vector3.left,false)
                : Join(kit[1],Core,Vector3.forward,false) && Join(kit[2],kit[1],Vector3.forward,false)
                && Join(kit[5],kit[1],Vector3.right,false) && Join(kit[6],kit[1],Vector3.left,false)
                && Join(kit[7],kit[2],Vector3.right,true) && Join(kit[8],kit[2],Vector3.left,true);
            if(!success) { TryResetBuild(); Workshop.Session.Notify("Example placement blocked; kit reset."); return false; }
            Workshop.Session.Select(creekPreset?Core:kit[level2?2:1]); Workshop.Session.Notify("Example ready. Drag a block to detach its branch, or press START."); return true;
        }
    }
}

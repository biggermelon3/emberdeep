using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PhysicalAssembly
{
    public sealed partial class DownhillController
    {
        public GravityGarage25DCourse Course25D { get; private set; }
        public GravityGarage25DRun Run25D { get; private set; }
        public GravityGarage3DCourse Course3D { get; private set; }
        public GravityGarage3DRun Run3D { get; private set; }
        public float GarageWorldScale => Settings.Level3D && Course3D ? Course3D.WorldScale : Course25D?Course25D.WorldScale:GarageCourse.WorldScale;
        public GravityGarageCourse GarageCourse { get; private set; }
        public GravityGarageResult GarageResult { get; private set; }
        public GravityGaragePreset GaragePreset { get; private set; }
        public bool GarageComparing { get; private set; }
        bool garageAdvancing;
        public bool GaragePlanar;
        public readonly List<GravityGarageResult> GarageComparisons=new();
        public float GaragePlaybackRate=1;
        public void SetGaragePlayback(float rate) { GaragePlaybackRate=Mathf.Clamp(rate,.5f,4); if(ownsClock)Time.timeScale=previousTimeScale*GaragePlaybackRate; }
        float garageLandingTime=-1,garagePreviousSpeed;
        bool garageCrossedEdge;
        GarageContact[] garageContacts;

        Quaternion GarageReleaseRotation(Quaternion surfaceRotation)
        {
            var origin=Core.MainBody.Rigidbody;
            var wheels=connected.SelectMany(p=>p.GetComponentsInChildren<PassiveWheel>()).ToArray();
            if(wheels.Length<2) return surfaceRotation;
            Vector3 Local(PassiveWheel w) => Quaternion.Inverse(origin.rotation)*(w.transform.position-origin.position);
            float Z(PassiveWheel w) => Local(w).z;
            float front=wheels.Max(Z),rear=wheels.Min(Z),wheelbase=front-rear;
            if(wheelbase<.001f) return surfaceRotation;
            float middle=(front+rear)*.5f;
            float frontRadius=wheels.Where(w=>Z(w)>middle).Average(w=>w.Radius);
            float rearRadius=wheels.Where(w=>Z(w)<=middle).Average(w=>w.Radius);
            float height=wheels.Where(w=>Z(w)>middle).Average(w=>Local(w).y)
                -wheels.Where(w=>Z(w)<=middle).Average(w=>Local(w).y);
            // Match both tire support heights to the ramp, including reversed sizes
            // or axles mounted at different heights. Mount facing does not set travel.
            float span=Mathf.Sqrt(wheelbase*wheelbase+height*height);
            float pitch=Mathf.Asin(Mathf.Clamp((frontRadius-rearRadius)/span,-1,1))-Mathf.Atan2(height,wheelbase);
            return surfaceRotation*Quaternion.Euler(-pitch*Mathf.Rad2Deg,0,0);
        }

        public void RebuildGarageCourse()
        {
            if(!Settings.UseGravityGarage || !Core) return;
            var authored=Workshop.Layout.GetComponent<AuthoredSandboxLayout>();
            if(authored) {
                bool second=Settings.Level3D;
                authored.Show(second?DesignLevelId.WaterfallValley:DesignLevelId.GravityGarage);
                Course25D=null;
                Course3D=second?authored.Level2:null; GarageCourse=second?null:authored.Level1;
                if(Course3D) Course3D.BindAuthored();
                var markers=second?Course3D.Markers:GarageCourse.Markers;
                Course.StartPoint.SetPositionAndRotation(markers.Start.position,markers.Start.rotation);
                if(Presentation.Interface) {
                    var ui=Presentation.Interface;ui.CourseNumber=second?2:1;ui.CourseCount=2;
                    ui.CourseTitle=second?"Waterfall Valley":"Gravity Garage";
                    if(second) ui.CoursePreview=Settings.Level3D.Thumbnail;
                    ui.RefreshCourseCard();
                }
                RefreshCampaignCard();Presentation.SetBuilding(IsBuilding);SpawnPreview.Refresh();return;
            }
            if(Settings.Level3D) { Rebuild3D(); return; }
            if(Settings.Level25D) { Rebuild25D(); return; }
            if(!GarageCourse) GarageCourse=Course.gameObject.AddComponent<GravityGarageCourse>();
            GarageCourse.Rebuild(Course,Settings.Garage,GravityGarageDesign.UnitScale*Core.GeometryScale*TransferScale);
            RefreshCampaignCard();
            Presentation.SetBuilding(IsBuilding);
            SpawnPreview.Refresh();
        }
        public bool SelectGarageCourse(bool enabled)
        {
            if(!enabled || !CanSwitchMode || Rotating) return false;
            RestoreBuildImmediately(); Settings.UseGravityGarage=true;
            return LoadGaragePreset(GravityGaragePreset.BigFront);
        }
        void ClearGarageKit()
        {
            Workshop.World.Collisions.Release("downhill-build");
            foreach(var edge in Workshop.World.Connections.ToArray()) Workshop.World.RemoveConnection(edge.id);
            foreach(var p in kit.ToArray()) { Workshop.World.UnregisterPart(p); p.gameObject.SetActive(false); Destroy(p.gameObject); }
            kit.Clear(); spawns.Clear(); saved.Clear(); connected.Clear(); buildBlockers.Clear(); previouslyHeld.Clear(); returns.Clear(); Core=null;
        }
        public bool LoadGaragePreset(GravityGaragePreset preset)
        {
            if(!Settings.UseGravityGarage || !CanSwitchMode || Rotating) return false;
            RestoreBuildImmediately();
            float scale=Core?Core.GeometryScale:(Workshop.Layout.Spawns[0]?Workshop.Layout.Spawns[0].SizeMultiplier:1);
            var origin=Core?Core.MainBody.Rigidbody.position:Presentation.AssemblyAnchor.position;
            var rotation=Presentation.AssemblyAnchor.rotation;
            ClearGarageKit(); GaragePreset=preset;
            if(Settings.Level25D) {
                var vehicle=GravityGarage25DVehicle.Create(Workshop.World,Settings.Level25D.parameters,new Pose(origin,rotation),scale);
                kit.AddRange(vehicle);Core=vehicle.First(p=>p.BuildingCore);
                foreach(var p in kit)spawns[p]=Capture(p.Bodies).ToArray();
                TopologyChanged();RebuildGarageCourse();Workshop.Session.Select(Core);UpdateControls();return true;
            }
            AssemblyPart Create(PartKind kind,Vector3 offset,bool core=false) {
                var p=DownhillParts.Create(Workshop.World,kind,new Pose(origin+rotation*offset*scale,rotation),scale,Settings,8,core);
                kit.Add(p); if(core) Core=p;
                foreach(var b in p.Bodies) { b.Rigidbody.isKinematic=true; b.Rigidbody.useGravity=false; }
                return p;
            }
            void Join(AssemblyPart part,AssemblyPart target,Vector3 normal) {
                PresetAttachment.Connect(Workshop.World,part,target,rotation,normal,"Reference vehicle intersects: ","Reference connection rejected.");
            }
            try {
                Create(PartKind.DownhillLongBlock,Vector3.zero,true);
                for(int end=-1;end<=1;end+=2) {
                    var beam=Create(PartKind.DownhillShortBlock,Vector3.forward*(end*1.5f*BuildingGeometry.Unit));
                    Join(beam,Core,Vector3.forward*end);
                    bool big=preset==GravityGaragePreset.BothBig || (end>0?preset==GravityGaragePreset.BigFront:preset==GravityGaragePreset.SmallFront);
                    for(int side=-1;side<=1;side+=2) {
                        var wheel=Create(big?PartKind.DownhillLargeWheel:PartKind.DownhillSmallWheel,new Vector3(side*(BuildingGeometry.Unit+.016f),0,end*1.5f*BuildingGeometry.Unit));
                        Join(wheel,beam,Vector3.right*side);
                    }
                }
                foreach(var p in kit) spawns[p]=Capture(p.Bodies).ToArray();
                TopologyChanged(); RebuildGarageCourse(); Workshop.Session.Select(Core); UpdateControls(); return true;
            } catch(Exception error) { Debug.LogException(error); TopologyChanged(); return false; }
        }
        void BeginGarageRun()
        {
            if(Settings.Level3D) { if(!Run3D)Run3D=simulationRoot.AddComponent<GravityGarage3DRun>();Run3D.Bind(Course3D,SimulationWorld.Parts.Values.ToArray(),SimulationCore);Run3D.StartRun();return; }
            if(Settings.Level25D) { if(!Run25D)Run25D=simulationRoot.AddComponent<GravityGarage25DRun>();Run25D.Bind(Course25D,SimulationWorld.Parts.Values.ToArray(),SimulationCore);Run25D.StartRun();return; }
            GarageResult=new GravityGarageResult{Configuration=GaragePreset.ToString()+(GaragePlanar?" / planar":" / 3D"),WorldScale=GarageCourse.WorldScale,DesignJson=JsonUtility.ToJson(Settings.Garage)};
            garageLandingTime=-1; garageCrossedEdge=false; garagePreviousSpeed=0;
            var contacts=new List<GarageContact>();
            foreach(var part in SimulationWorld.Parts.Values) foreach(var b in part.Bodies) {
                if(b.Id=="tire") { var c=b.GetComponent<GarageContact>(); if(!c)c=b.gameObject.AddComponent<GarageContact>(); c.Landing=GarageCourse.Landing; c.ResetContact(); contacts.Add(c); }
                if(GaragePlanar && !b.GetComponent<GaragePlaneJoint>()) {
                    b.gameObject.AddComponent<GaragePlaneJoint>();
                    var j=b.gameObject.AddComponent<ConfigurableJoint>(); j.autoConfigureConnectedAnchor=false;
                    j.anchor=Vector3.zero; j.connectedAnchor=b.Rigidbody.position;
                    j.axis=b.transform.InverseTransformDirection(Course.SandboxFrame.forward); j.secondaryAxis=b.transform.InverseTransformDirection(Course.SandboxFrame.up);
                    j.xMotion=ConfigurableJointMotion.Locked; j.yMotion=j.zMotion=ConfigurableJointMotion.Free;
                    j.angularXMotion=ConfigurableJointMotion.Free; j.angularYMotion=j.angularZMotion=ConfigurableJointMotion.Locked;
                }
            }
            garageContacts=contacts.ToArray();
        }
        void UpdateGarageRun()
        {
            if(Settings.Level3D) {
                if(Run3D && Run3D.Finished){Resolve(Run3D.Success);if(!VictoryInProgress) Workshop.Session.Notify(Run3D.Feedback);}
                else if(Run3D && Run3D.Running && Workshop.Session.Status!=Run3D.Feedback)Workshop.Session.Notify(Run3D.Feedback);
                return;
            }
            if(Settings.Level25D) { if(Run25D && Run25D.Finished)Resolve(Run25D.Stage==Garage25DStage.Complete);return; }
            var rb=SimulationCore.MainBody.Rigidbody; var frame=GarageCourse.Frame; var p=GarageCourse.Coordinates(rb.position);
            float rootScale=Mathf.Sqrt(GarageCourse.WorldScale);
            GarageResult.Time+=Time.fixedDeltaTime/rootScale; GarageResult.FinalX=p.x;
            var forward=frame.InverseTransformDirection(rb.rotation*Vector3.forward); var right=frame.InverseTransformDirection(rb.rotation*Vector3.right); var up=frame.InverseTransformDirection(rb.rotation*Vector3.up);
            float pitch=Mathf.Abs(Mathf.Atan2(forward.y,forward.x)*Mathf.Rad2Deg),roll=Mathf.Abs(Mathf.Atan2(right.y,-right.z)*Mathf.Rad2Deg);
            GarageResult.MaxPitch=Mathf.Max(GarageResult.MaxPitch,pitch); GarageResult.MaxRoll=Mathf.Max(GarageResult.MaxRoll,roll);
            int landed=garageContacts.Count(c=>c.OnLanding);
            // Match the reference crossing rule; a long car may bridge the gap without all tires losing contact together.
            if(!garageCrossedEdge && (GarageCourse.Markers?GarageCourse.Markers.Passed(GarageCourse.Markers.Jump,rb.position):p.x>GravityGarageDesign.Edge+.2f)) {
                garageCrossedEdge=true; GarageResult.TakeoffSpeed=garagePreviousSpeed;
            }
            garagePreviousSpeed=rb.linearVelocity.magnitude/rootScale;
            GarageResult.Stage=garageCrossedEdge?(landed>0?"Landing":"Airborne"):(p.x<4.45f?"First descent":p.x<Settings.Garage.SecondStart?"Rock A":"Second descent");
            float finalX=Settings.Garage.GoalX;
            bool upright=GarageCourse.Markers?
                Vector3.Dot(rb.rotation*Vector3.up,GarageCourse.Markers.Finish.up)>.78f && Vector3.Dot(rb.rotation*Vector3.forward,GarageCourse.Markers.Finish.forward)>0 && rb.angularVelocity.magnitude*rootScale<.8f:
                up.y>.78f && forward.x>0 && rb.angularVelocity.magnitude*rootScale<.8f;
            bool settled=garageCrossedEdge && landed>=Mathf.Min(2,garageContacts.Length) && landed>0 && upright;
            if(!settled) garageLandingTime=-1;
            else if(garageLandingTime<0) garageLandingTime=GarageResult.Time;
            if(garageLandingTime>=0 && GarageResult.Time-garageLandingTime>.52f && (GarageCourse.Markers?GarageCourse.Markers.Passed(GarageCourse.Markers.Finish,rb.position):p.x>finalX)) {
                GarageResult.Success=true; GarageResult.Outcome="Stable landing"; GarageResult.LandingPitch=pitch; GarageResult.LandingRoll=roll; Resolve(true);
            } else if((GarageCourse.Markers?!GarageCourse.Markers.Inside(rb.position):p.y>5.75f || Mathf.Abs(p.z)>1.7f || p.x>14.7f) || GarageResult.Time>=16f) {
                GarageResult.Outcome=GarageResult.Time>=16f?"Timeout / stalled":garageCrossedEdge?"Failed landing":"Left course"; Resolve(false);
            }
        }

        void Rebuild3D()
        {
            if(Course3D){Course3D.gameObject.SetActive(false);Destroy(Course3D.gameObject);}
            foreach(Transform child in Course.SandboxFrame)if(child!=Course.StartPoint && (!Course.FinishArea||child!=Course.FinishArea.transform) && (!Course.PlayArea||child!=Course.PlayArea.transform))child.gameObject.SetActive(false);
            float scale=GravityGarage25DCourse.UnitScale*Core.GeometryScale*TransferScale/Course.SandboxFrame.lossyScale.x;
            var root=new GameObject("Waterfall Valley");root.transform.SetParent(Course.SandboxFrame,false);root.transform.localPosition=new Vector3(0,.25f,.65f+(Settings.Level3D.Radius+Settings.Level3D.DeckWidth*.5f)*scale);
            Course3D=root.AddComponent<GravityGarage3DCourse>();Course3D.Build(Settings.Level3D,scale);
            var start=Course3D.Sample(2);Course.StartPoint.SetPositionAndRotation(start.Position,Quaternion.LookRotation(start.Tangent,start.Normal));
            if(Presentation.Interface){Presentation.Interface.CourseNumber=2;Presentation.Interface.CourseCount=2;Presentation.Interface.CourseTitle=Settings.Level3D.Title;Presentation.Interface.CoursePreview=Settings.Level3D.Thumbnail;Presentation.Interface.AlignThumbnailWithBacking();Presentation.Interface.RefreshCourseCard();}
            RefreshCampaignCard();Presentation.SetBuilding(IsBuilding);SpawnPreview.Refresh();
        }
        void Rebuild25D()
        {
            if(Course25D){Course25D.gameObject.SetActive(false);Destroy(Course25D.gameObject);}
            foreach(Transform child in Course.SandboxFrame)if(child!=Course.StartPoint && (!Course.FinishArea||child!=Course.FinishArea.transform) && (!Course.PlayArea||child!=Course.PlayArea.transform))child.gameObject.SetActive(false);
            var root=new GameObject("Gravity Garage 2.5D");root.transform.SetParent(Course.SandboxFrame,false);root.transform.localPosition=new Vector3(0,.25f,.65f);
            Course25D=root.AddComponent<GravityGarage25DCourse>();Course25D.Build(Settings.Level25D.parameters,GravityGarage25DCourse.UnitScale*Core.GeometryScale*TransferScale/Course.SandboxFrame.lossyScale.x,Settings.SandboxDrillCourse);
            if(Presentation.Interface){Presentation.Interface.CourseTitle=Settings.DesignerBuilder?"Gravity Garage":Settings.Level25D.name;Presentation.Interface.CoursePreview=Settings.Level25D.thumbnail;Presentation.Interface.RefreshCourseCard();}
            Course.StartPoint.position=Course25D.Point(2.15f,Course25D.Parameters.Upper(2.15f),-1.55f);Course.StartPoint.rotation=Course25D.Heading(2.15f);
            RefreshCampaignCard();
            if(Settings.DesignerBuilder) Presentation.SetBuilding(IsBuilding);
            SpawnPreview.Refresh();
        }
        public IEnumerator CompareGaragePresets()
        {
            if(Settings.Level3D || Settings.Level25D || GarageComparing || !Settings.UseGravityGarage || !CanSwitchMode || Rotating) yield break;
            RestoreBuildImmediately(); GarageComparing=true; GarageComparisons.Clear();
            var originals=kit.Select(p=>new GaragePartSnapshot {Kind=p.Kind,Scale=p.GeometryScale,Version=p.GridVersion,Radius=p.DesignWheelRadius,Core=p.BuildingCore,Id=p.InstanceId,Poses=p.Bodies.Select(b=>new Pose(b.Rigidbody.position,b.Rigidbody.rotation)).ToArray()}).ToArray();
            var edges=Workshop.World.Connections.Select(e=>JsonUtility.FromJson<ConnectionRecord>(JsonUtility.ToJson(e))).ToArray();
            var selected=GaragePreset;
            var selectedPart=Workshop.Session.Selected?Workshop.Session.Selected.InstanceId:null;
            try {
                foreach(GravityGaragePreset preset in Enum.GetValues(typeof(GravityGaragePreset))) {
                    garageAdvancing=true;
                    try {if(!LoadGaragePreset(preset) || !TryStart()) throw new InvalidOperationException("Comparison could not start.");}
                    finally {garageAdvancing=false;UpdateControls();}
                    while(State==DownhillState.Transition || State==DownhillState.Countdown || State==DownhillState.Running) yield return null;
                    GarageComparisons.Add(JsonUtility.FromJson<GravityGarageResult>(JsonUtility.ToJson(GarageResult)));
                    RestoreBuildImmediately();
                }
            } finally {
                RestoreBuildImmediately(); ClearGarageKit(); var map=new Dictionary<string,AssemblyPart>();
                foreach(var s in originals) {
                    var p=DownhillParts.Create(Workshop.World,s.Kind,s.Poses[0],s.Scale,Settings,s.Version,s.Core,s.Radius); kit.Add(p); map.Add(s.Id,p); if(s.Core)Core=p;
                    for(int i=0;i<p.Bodies.Length;i++) {var b=p.Bodies[i]; b.Rigidbody.isKinematic=true;b.Rigidbody.useGravity=false;b.transform.SetPositionAndRotation(s.Poses[i].position,s.Poses[i].rotation);b.Rigidbody.position=s.Poses[i].position;b.Rigidbody.rotation=s.Poses[i].rotation;}
                    spawns[p]=Capture(p.Bodies).ToArray();
                }
                foreach(var e in edges) {e.a=new BodyRef(map[e.a.partId].InstanceId,e.a.bodyId);e.b=new BodyRef(map[e.b.partId].InstanceId,e.b.bodyId);if(!Workshop.World.Connect(e))Debug.LogError("Comparison restore failed.");}
                GaragePreset=selected; TopologyChanged(); RebuildGarageCourse(); GarageComparing=false; Workshop.Session.Select(selectedPart!=null && map.TryGetValue(selectedPart,out var selection)?selection:Core);UpdateControls();
            }
        }
        sealed class GaragePartSnapshot { public PartKind Kind; public float Scale,Radius; public int Version; public bool Core; public string Id; public Pose[] Poses; }
    }
    public sealed class GaragePlaneJoint : MonoBehaviour { }
    public sealed class GarageContact : MonoBehaviour
    {
        public Collider Landing;
        readonly HashSet<Collider> support=new();
        public bool Supported => support.Count>0;
        public bool Supports(Collider collider)=>support.Contains(collider);
        public bool OnLanding => support.Contains(Landing);
        public void ResetContact() => support.Clear();
        void OnCollisionStay(Collision c) => Contact(c);
        void OnCollisionEnter(Collision c) => Contact(c);
        void OnCollisionExit(Collision c) => support.Remove(c.collider);
        void Contact(Collision c) {for(int i=0;i<c.contactCount;i++) if(c.GetContact(i).normal.y>.15f) {support.Add(c.collider);return;}support.Remove(c.collider);}
    }
}

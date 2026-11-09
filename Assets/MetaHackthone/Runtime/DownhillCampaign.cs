using System.Linq;
using UnityEngine;
namespace PhysicalAssembly
{
    public sealed partial class DownhillController
    {
        GravityGarageLevelAsset pipeCourse;
        GravityGarage3DLevelAsset valleyCourse;
        Texture2D firstCoursePreview;
        public SandboxCourseId CurrentCourse { get; private set; }
        public bool CanAdvanceLevel => Settings.Campaign && !DesignModeSession.Enabled && (pipeCourse || valleyCourse) &&
            CurrentCourse==SandboxCourseId.GravityGarage && SandboxCampaignSession.FirstComplete &&
            (IsBuilding || State==DownhillState.Success) && CanSwitchMode && !Rotating;
        void InitializeCampaign()
        {
            pipeCourse=Settings.Level25D;valleyCourse=Settings.Level3D;
            CurrentCourse=Settings.Campaign?SandboxCampaignSession.Current:((pipeCourse || valleyCourse)?SandboxCourseId.TeleportPipes:SandboxCourseId.GravityGarage);
            if(Settings.Campaign && CurrentCourse==SandboxCourseId.GravityGarage) {Settings.Level25D=null;Settings.Level3D=null;}
        }
        void RefreshCampaignCard()
        {
            if(!Settings.Campaign || !Presentation.Interface) return;
            var ui=Presentation.Interface;
            if(!firstCoursePreview) firstCoursePreview=ui.CoursePreview;
            ui.CourseNumber=(int)CurrentCourse+1; ui.CourseCount=2;
            ui.CourseTitle=CurrentCourse==SandboxCourseId.GravityGarage?"Gravity Garage":valleyCourse?valleyCourse.Title:"Teleport Pipes";
            ui.CoursePreview=CurrentCourse==SandboxCourseId.GravityGarage?firstCoursePreview:valleyCourse?valleyCourse.Thumbnail:pipeCourse.thumbnail;
            ui.RefreshCourseCard();
        }
        public bool TryAdvanceLevel()
        {
            if(!CanAdvanceLevel) return false;
            CompleteBoundaryReturns();
            if(PalmMenu) PalmMenu.Close();
            RestoreBuildImmediately();
            var origin=Presentation.AssemblyAnchor.position;
            Settings.Level25D=pipeCourse;Settings.Level3D=valleyCourse;
            CurrentCourse=SandboxCourseId.TeleportPipes; SandboxCampaignSession.Advance();
            RebuildGarageCourse(); Presentation.Configure();
            var shift=Presentation.AssemblyAnchor.position-origin;
            TranslateDesignerKit(shift);
            if(Course.BuildArea) Course.BuildArea.transform.position+=shift;
            foreach(var records in spawns.Values) for(int i=0;i<records.Length;i++)
                records[i].Position+=Course.WorkbenchFrame.InverseTransformVector(shift);
            RefreshDesignerKitAvailability();
            if(Reward) Reward.Claim();
            TopologyChanged(); SpawnPreview.Refresh(); UpdateControls();
            Workshop.Session.Notify("LEVEL 2: WATERFALL VALLEY\nAdjust your construction, then press START.");
            var desktop=Workshop.GetComponent<DesktopInputAdapter>(); if(desktop && desktop.IsDesktop) desktop.FramePlatform();
            return true;
        }
    }
}

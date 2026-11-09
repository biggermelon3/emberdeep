using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PhysicalAssembly
{
    // Desktop designer controls. Headset interaction retains the existing native build/test controls.
    public sealed class GravityGaragePanel : MonoBehaviour
    {
        public DownhillController Level;
        Vector2 scroll;
        int drag=-1;
        bool paused;
        float resumeScale;
        string exportMessage;
        Rect Panel => new Rect(Mathf.Max(12,Screen.width-350),130,338,Mathf.Max(120,Screen.height-142));
        public bool Visible => Application.isEditor && Level && !Level.Settings.DesignerBuilder && !Level.Settings.Level25D && GetComponent<DesktopInputAdapter>() && GetComponent<DesktopInputAdapter>().IsDesktop;
        public bool Contains(Vector2 screen) => Visible && Panel.Contains(new Vector2(screen.x,Screen.height-screen.y));
        void Unpause() { if(paused) {Time.timeScale=resumeScale;paused=false;} }
        void OnDisable() => Unpause();
        void Update()
        {
            if(!Visible || !Level.Settings.UseGravityGarage || Level.GarageComparing || !Level.IsBuilding || Mouse.current==null) return;
            var mouse=Mouse.current; var view=GetComponent<DesktopInputAdapter>().View;
            if(mouse.leftButton.wasPressedThisFrame && !Contains(mouse.position.ReadValue()) && !Level.Workshop.World.HasHolds) {
                if(Physics.Raycast(view.ScreenPointToRay(mouse.position.ReadValue()),out var hit,100)) {var rock=hit.collider.GetComponent<GravityGarageRock>(); if(rock)drag=rock.Index;}
            }
            if(!mouse.leftButton.isPressed) drag=-1;
            if(drag<0) return;
            var plane=new Plane(Level.Course.SandboxFrame.forward,Level.Course.SandboxFrame.TransformPoint(new Vector3(0,0,.65f)));
            var ray=view.ScreenPointToRay(mouse.position.ReadValue());
            if(!plane.Raycast(ray,out float distance)) return;
            var d=Level.Settings.Garage; float x=d.SupportedX(Level.GarageCourse.Coordinates(ray.GetPoint(distance)).x);
            if(drag==0)d.RockX=x;else d.Rock2X=x;Level.RebuildGarageCourse();
        }
        float Slider(string label,float value,float min,float max)
        { GUILayout.Label(label+"  "+value.ToString("F2",CultureInfo.InvariantCulture)+" m");return GUILayout.HorizontalSlider(value,min,max); }
        void OnGUI()
        {
            if(!Visible) return;
            GUILayout.BeginArea(Panel,GUI.skin.box); scroll=GUILayout.BeginScrollView(scroll);
            GUILayout.Label("GRAVITY GARAGE / PHYSICS LAB");
            if(GUILayout.Button("Focus course")) GetComponent<DesktopInputAdapter>().FrameGarage();
            GUI.enabled=!Level.GarageComparing && Level.CanSwitchMode && !Level.Rotating;
            if(Level.Settings.UseGravityGarage) {
                GUILayout.Label("Presets replace the current build");
                foreach(GravityGaragePreset preset in Enum.GetValues(typeof(GravityGaragePreset)))
                    if(GUILayout.Button(PresetLabel(preset))) {Unpause();Level.LoadGaragePreset(preset);}
                bool planar=GUILayout.Toggle(Level.GaragePlanar,"Constrained plane calibration (reference only)");
                if(planar!=Level.GaragePlanar) {Unpause();Level.RestoreBuildImmediately();Level.GaragePlanar=planar;}
                if(GUILayout.Button("Compare four / restore my build")) {Unpause();StartCoroutine(Level.CompareGaragePresets());}
                GUILayout.BeginHorizontal();
                if(GUILayout.Button("Start / restart")) {Unpause();if(Level.IsBuilding)Level.TryStart();else Level.TryRetry();}
                if(GUILayout.Button("Back to build")) {Unpause();Level.RestoreBuildImmediately();}
                GUILayout.EndHorizontal();
                if(GUILayout.Button(paused?"Resume":"Pause") && Level.State==DownhillState.Running) {if(paused)Unpause();else {resumeScale=Time.timeScale;Time.timeScale=0;paused=true;}}
                GUILayout.Label("Playback speed");GUILayout.BeginHorizontal();
                foreach(float rate in new[]{.5f,1f,2f,4f}) if(GUILayout.Button(rate+"x")) {Unpause();Level.SetGaragePlayback(rate);}
                GUILayout.EndHorizontal();
                var d=Level.Settings.Garage; var before=JsonUtility.ToJson(d);
                d.ReleaseX=Slider("Release position",d.ReleaseX,1.68f,3.2f);
                d.FirstDrop=Slider("First drop",d.FirstDrop,.65f,2.65f);
                d.RockRadius=Slider("Rock A radius",d.RockRadius,.08f,.52f);
                d.RockX=Slider("Rock A position",d.RockX,.55f,14.35f);
                d.SecondStart=Slider("Second descent starts",d.SecondStart,5.1f,7.1f);
                d.SecondDrop=Slider("Second drop",d.SecondDrop,0,1.1f);
                d.SecondRock=GUILayout.Toggle(d.SecondRock,"Enable rock B");
                if(d.SecondRock) {d.Rock2Radius=Slider("Rock B radius",d.Rock2Radius,.08f,.52f);d.Rock2X=Slider("Rock B position",d.Rock2X,.55f,14.35f);}
                d.Gap=Slider("Gap",d.Gap,.55f,2.35f);d.LandingDrop=Slider("Landing drop",d.LandingDrop,.15f,1.2f);d.LipRise=Slider("Takeoff lip rise",d.LipRise,0,.6f);
                if(before!=JsonUtility.ToJson(d)) {Unpause();Level.RestoreBuildImmediately();Level.RebuildGarageCourse();}
                if(GUILayout.Button("Restore HTML course defaults")) {Unpause();Level.RestoreBuildImmediately();Level.Settings.Garage=new GravityGarageDesign();Level.RebuildGarageCourse();}
                if(GUILayout.Button("Restore Unity calibration (0.80 m gap)")) {Unpause();Level.RestoreBuildImmediately();Level.Settings.Garage=GravityGarageDesign.UnityDefaults();Level.RebuildGarageCourse();}
                GUI.enabled=true;
                GUILayout.Label("Metres and seconds below use the design scale. No engine or launch boost.");
                if(Level.GarageResult!=null) DrawResult(Level.GarageResult);
                foreach(var result in Level.GarageComparisons) DrawResult(result);
                if(Level.GarageComparisons.Count>0 && GUILayout.Button("Export comparison CSV")) Export();
                if(!string.IsNullOrEmpty(exportMessage)) GUILayout.Label(exportMessage);
            }
            GUI.enabled=true;GUILayout.EndScrollView();GUILayout.EndArea();
        }
        static void DrawResult(GravityGarageResult r) {
            GUILayout.Label($"{r.Configuration}: {r.Outcome} / {r.Stage}\n{r.Time:F2} s | takeoff {(r.TakeoffSpeed<0?"--":r.TakeoffSpeed.ToString("F2"))} m/s\npitch {r.MaxPitch:F1} / roll {r.MaxRoll:F1} degrees");
        }
        static string PresetLabel(GravityGaragePreset p) => p switch {
            GravityGaragePreset.BigFront=>"Large front / small rear",GravityGaragePreset.SmallFront=>"Small front / large rear",
            GravityGaragePreset.BothBig=>"Four large wheels",_=>"Four small wheels"
        };
        void Export() {
            var lines=new[]{"Configuration,Outcome,Stage,Success,Time,TakeoffSpeed,MaxPitch,MaxRoll,LandingPitch,LandingRoll,WorldScale,DesignParameters"}.Concat(Level.GarageComparisons.Select(r=>string.Join(",",r.Configuration,r.Outcome,r.Stage,r.Success,r.Time.ToString("R",CultureInfo.InvariantCulture),r.TakeoffSpeed.ToString("R",CultureInfo.InvariantCulture),r.MaxPitch.ToString("R",CultureInfo.InvariantCulture),r.MaxRoll.ToString("R",CultureInfo.InvariantCulture),r.LandingPitch.ToString("R",CultureInfo.InvariantCulture),r.LandingRoll.ToString("R",CultureInfo.InvariantCulture),r.WorldScale.ToString("R",CultureInfo.InvariantCulture),"\""+(r.DesignJson??"").Replace("\"","\"\"")+"\"")));
            var path=Path.Combine(Application.persistentDataPath,"GravityGarage-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".csv");File.WriteAllLines(path,lines);exportMessage=path;
        }
    }
}

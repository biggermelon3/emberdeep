using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace PhysicalAssembly.Editor
{
    public sealed class GravityGarage25DEditor : EditorWindow
    {
        public const string PreviewScene="Assets/MetaHackthone/Scenes/GravityGarage25DPreview.unity";
        const string Session="Garage25D.OwnedPlay",PreviousScene="Garage25D.PreviousStart",Clock="Garage25D.Clock",Step="Garage25D.Step";
        [SerializeField] GravityGarageLevelAsset draft,asset;
        [SerializeField] string savedJson;
        [SerializeField] float playback=1;
        PreviewRenderUtility preview;GravityGarage25DCourse editCourse,playCourse;GravityGarage25DRun run;
        GameObject playRoot;Camera playCamera;RenderTexture texture;
        VisualElement panels,body;Label feedback,presetLabel;IMGUIContainer viewport;
        readonly System.Collections.Generic.Dictionary<string,Action> syncFields=new();
        readonly System.Collections.Generic.List<Label> statValues=new(),stageLabels=new();
        bool rebuild=true,dragging;double lastPaint;
        static readonly Color Background=new(.063f,.106f,.129f),Panel=new(.09f,.145f,.169f),Mint=new(.612f,.878f,.749f),Text=new(.93f,.96f,.94f);
        [InitializeOnLoadMethod]
        static void RegisterRecovery(){EditorApplication.playModeStateChanged+=state=>{
            if(!SessionState.GetBool(Session,false))return;
            if(state==PlayModeStateChange.ExitingPlayMode){Time.timeScale=SessionState.GetFloat(Clock,1);Time.fixedDeltaTime=SessionState.GetFloat(Step,.02f);}
            if(state==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=()=>{EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(PreviousScene,""));SessionState.SetBool(Session,false);SessionState.SetBool(Session+".Started",false);};
        };}
        [MenuItem("MetaHackthone/Gravity Garage 2.5D Level Editor")]
        public static void OpenWindow(){var w=GetWindow<GravityGarage25DEditor>();w.titleContent=new GUIContent("Gravity Garage 2.5D");w.minSize=new Vector2(620,500);w.Show();}
        void OnEnable()
        {
            if(!draft){draft=CreateInstance<GravityGarageLevelAsset>();draft.hideFlags=HideFlags.HideAndDontSave;}
            savedJson??=GravityGarageLevelAsset.Export(draft.parameters);
            Undo.undoRedoPerformed+=UndoRedo;EditorApplication.playModeStateChanged+=PlayChanged;EditorApplication.update+=Tick;
            saveChangesMessage="Save changes to this Gravity Garage level?";rebuild=true;
            if(EditorApplication.isPlaying&&SessionState.GetBool(Session+".Started",false))EditorApplication.delayCall+=()=>{if(SessionState.GetBool(Session,false))EditorApplication.ExitPlaymode();};
        }
        void OnDisable(){Undo.undoRedoPerformed-=UndoRedo;EditorApplication.playModeStateChanged-=PlayChanged;EditorApplication.update-=Tick;DisposePreview();ReleaseTexture();}
        void OnDestroy(){if(SessionState.GetBool(Session,false)&&EditorApplication.isPlaying)EditorApplication.ExitPlaymode();if(draft)DestroyImmediate(draft);}
        void UndoRedo(){rebuild=true;Dirty();CreateGUI();}
        void Dirty(){hasUnsavedChanges=GravityGarageLevelAsset.Export(draft.parameters)!=savedJson;}
        void Change(Action<GravityGarage25DParameters> action,bool physical=true)
        {
            Undo.RecordObject(draft,"Edit Gravity Garage level");action(draft.parameters);EditorUtility.SetDirty(draft);Dirty();if(physical)rebuild=true;
            if(presetLabel!=null)presetLabel.text=draft.parameters.PresetName+" • 0.98 m wheelbase";
            if(playCourse)playCourse.SetLabels(draft.parameters.showLabels);foreach(var sync in syncFields.Values)sync();
        }
        public void CreateGUI()
        {
            if(!draft)return;var root=rootVisualElement;root.Clear();syncFields.Clear();statValues.Clear();stageLabels.Clear();var page=new ScrollView();page.style.flexGrow=1;root.Add(page);root=page.contentContainer;root.style.backgroundColor=Background;root.style.color=Text;root.style.paddingLeft=16;root.style.paddingRight=16;root.style.paddingTop=12;root.style.paddingBottom=12;
            var title=new Label("GRAVITY GARAGE  /  2.5D LEVEL EDITOR");title.style.fontSize=22;title.style.unityFontStyleAndWeight=FontStyle.Bold;title.style.color=Mint;title.style.marginBottom=6;root.Add(title);
            var subtitle=new Label("Build a gravity-powered vehicle. Clear the upper track, take the pipe, and reach the goal.");subtitle.style.whiteSpace=WhiteSpace.Normal;subtitle.style.marginBottom=10;root.Add(subtitle);
            var files=new Toolbar();files.style.height=StyleKeyword.Auto;files.style.minHeight=32;files.style.flexWrap=Wrap.Wrap;root.Add(files);
            Add(files,"New",()=>Replace(null));Add(files,"Open…",OpenAsset);Add(files,"Save",()=>Save(false));Add(files,"Save as…",()=>Save(true));Add(files,"Import JSON…",Import);Add(files,"Export JSON…",Export);Add(files,"Apply to sandbox",Apply);
            files.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
            var controls=new VisualElement();controls.style.flexDirection=FlexDirection.Row;controls.style.flexWrap=Wrap.Wrap;controls.style.marginTop=8;controls.style.marginBottom=8;root.Add(controls);
            Add(controls,"Start / Restart test",StartTest,true);Add(controls,"Pause / Resume",()=>{if(run)run.SetPaused(!run.Paused);});Add(controls,"Reset vehicle",()=>{if(run)run.ResetVehicle();else rebuild=true;});
            var rates=new PopupField<string>(new System.Collections.Generic.List<string>{"0.5×","1×","2×","3×"},playback==.5f?0:playback==2?2:playback==3?3:1);rates.style.width=70;rates.RegisterValueChangedCallback(e=>{playback=e.newValue=="0.5×"?.5f:e.newValue=="2×"?2:e.newValue=="3×"?3:1;if(run)Time.timeScale=playback;});controls.Add(rates);
            Add(controls,"Restore view",()=>{Change(p=>{p.cameraAngle=18;p.zoom=.94f;},false);CreateGUI();});
            if(EditorApplication.isPlaying)Add(controls,"Stop test / Edit",()=>{if(SessionState.GetBool(Session,false))EditorApplication.ExitPlaymode();});
            
            body=new VisualElement();body.style.flexGrow=1;body.style.flexDirection=FlexDirection.Row;root.Add(body);
            var left=new VisualElement();left.style.flexGrow=1;left.style.minWidth=280;left.style.backgroundColor=Panel;left.style.borderTopLeftRadius=15;left.style.borderTopRightRadius=15;left.style.marginRight=12;body.Add(left);
            viewport=new IMGUIContainer(DrawViewport);viewport.style.flexGrow=1;viewport.style.minHeight=220;left.Add(viewport);
            var stats=new VisualElement();stats.style.flexDirection=FlexDirection.Row;stats.style.backgroundColor=Background;left.Add(stats);
            foreach(var name in new[]{"CURRENT SPEED","PHYSICS TIME","PROGRESS","MAX / MIN SPEED","VEHICLE STATE"}){var cell=new VisualElement();cell.style.flexGrow=1;cell.style.flexBasis=0;cell.style.paddingLeft=10;cell.style.paddingTop=10;cell.style.paddingBottom=10;var caption=new Label(name);caption.style.fontSize=10;caption.style.color=new Color(.60f,.70f,.72f);caption.style.whiteSpace=WhiteSpace.Normal;cell.Add(caption);var val=new Label("—");val.style.fontSize=15;val.style.whiteSpace=WhiteSpace.Normal;cell.Add(val);stats.Add(cell);statValues.Add(val);}
            feedback=new Label("Ready. Start a gravity test.");feedback.style.whiteSpace=WhiteSpace.Normal;feedback.style.paddingLeft=12;feedback.style.paddingTop=8;feedback.style.paddingBottom=8;feedback.style.color=Mint;left.Add(feedback);
            var journey=new VisualElement();journey.style.flexDirection=FlexDirection.Row;journey.style.backgroundColor=Background;left.Add(journey);
            int stageIndex=1;foreach(var name in new[]{"Upper track","Jump & landing","Pipe","Lower track","Goal"}){var label=new Label("0"+stageIndex+"\n"+name);label.style.flexGrow=1;label.style.flexBasis=0;label.style.whiteSpace=WhiteSpace.Normal;label.style.unityTextAlign=TextAnchor.MiddleCenter;label.style.paddingTop=10;label.style.paddingBottom=12;journey.Add(label);stageLabels.Add(label);stageIndex++;}
            var scroll=new ScrollView();scroll.style.width=322;scroll.style.flexShrink=0;body.Add(scroll);panels=scroll;
            var vehicle=Card("01  VEHICLE");presetLabel=new Label(draft.parameters.PresetName+" • 0.98 m wheelbase");vehicle.Add(presetLabel);
            var presets=new VisualElement();presets.style.flexDirection=FlexDirection.Row;presets.style.flexWrap=Wrap.Wrap;vehicle.Add(presets);
            string[] names={"Big front","Small front","Both big","Both small"};for(int i=0;i<4;i++){int n=i;Add(presets,names[i],()=>{Change(p=>p.Preset(n));CreateGUI();});}
            Slider(vehicle,"Front wheel radius","frontWheel",.16f,.44f,.01f);Slider(vehicle,"Rear wheel radius","rearWheel",.16f,.44f,.01f);
            var upper=Card("02  UPPER TRACK");Slider(upper,"Upper drop","upperDrop",.8f,2.8f,.1f);Slider(upper,"Rock A position","rockPos",2.8f,15.2f,.1f);Slider(upper,"Rock A radius","rockSize",.02f,.40f,.01f);Slider(upper,"Gap width","gap",.2f,2.6f,.1f);
            var lower=Card("03  LOWER TRACK");Slider(lower,"Lower drop","lowerDrop",.4f,1.7f,.1f);Toggle(lower,"Rock B enabled","rock2Enabled");Slider(lower,"Rock B position","rock2Pos",2,15.9f,.1f);Slider(lower,"Rock B radius","rock2Size",.02f,.33f,.01f);
            var camera=Card("04  CAMERA");Slider(camera,"Camera angle","cameraAngle",-34,34,1,false);Slider(camera,"Zoom","zoom",.72f,1.23f,.01f,false);Toggle(camera,"Show labels","showLabels",false);
            var note=new Label("UnityCalibrated tunes the course for the standard Big front car. Smaller obstacles, a narrow gap, and gentler landings support the existing build.");note.style.whiteSpace=WhiteSpace.Normal;note.style.marginBottom=12;note.style.fontSize=11;scroll.Add(note);
            Add(scroll,"Challenge course",()=>{Change(p=>p.Challenge());CreateGUI();});Add(scroll,"Restore all parameters",()=>{Change(p=>{draft.parameters=new GravityGarage25DParameters();});CreateGUI();});
            if(EditorApplication.isPlayingOrWillChangePlaymode){vehicle.SetEnabled(false);upper.SetEnabled(false);lower.SetEnabled(false);foreach(var button in scroll.Children().OfType<Button>())button.SetEnabled(false);}
            body.RegisterCallback<GeometryChangedEvent>(e=>{bool narrow=position.width<1000;body.style.height=Mathf.Max(narrow?800:460,position.height-190);body.style.flexDirection=narrow?FlexDirection.Column:FlexDirection.Row;scroll.style.width=narrow?Length.Percent(100):322;scroll.style.maxHeight=narrow?320:StyleKeyword.None;left.style.marginRight=narrow?0:12;});
        }
        VisualElement Card(string title){var card=new VisualElement();card.style.backgroundColor=Panel;card.style.paddingLeft=12;card.style.paddingRight=12;card.style.paddingTop=10;card.style.paddingBottom=12;card.style.marginBottom=10;card.style.borderTopLeftRadius=12;card.style.borderBottomRightRadius=12;var label=new Label(title);label.style.color=Mint;label.style.unityFontStyleAndWeight=FontStyle.Bold;label.style.marginBottom=8;card.Add(label);panels.Add(card);return card;}
        static void Add(VisualElement parent,string text,Action action,bool primary=false){var b=new Button(action){text=text};b.style.minHeight=27;b.style.marginRight=4;b.style.marginBottom=3;if(primary){b.style.backgroundColor=Mint;b.style.color=Background;}parent.Add(b);}
        void Slider(VisualElement parent,string title,string field,float min,float max,float step,bool physical=true)
        {
            var info=typeof(GravityGarage25DParameters).GetField(field);float value=(float)info.GetValue(draft.parameters);
            string Format(float v)=>title+"   "+v.ToString(field=="cameraAngle"?"F0":"F2")+(field=="cameraAngle"?"°":field=="zoom"?"×":" m");
            var label=new Label(Format(value));label.style.marginTop=9;parent.Add(label);
            var slider=new Slider(min,max);slider.SetValueWithoutNotify(value);parent.Add(slider);
            syncFields[field]=()=>{float v=(float)info.GetValue(draft.parameters);slider.SetValueWithoutNotify(v);label.text=Format(v);};
            slider.RegisterValueChangedCallback(e=>{float v=Mathf.Clamp(Mathf.Round(e.newValue/step)*step,min,max);Change(p=>info.SetValue(p,v),physical);label.text=Format(v);slider.SetValueWithoutNotify(v);});
        }
        void Toggle(VisualElement parent,string title,string field,bool physical=true){var info=typeof(GravityGarage25DParameters).GetField(field);var toggle=new Toggle(title){value=(bool)info.GetValue(draft.parameters)};toggle.style.marginTop=8;toggle.RegisterValueChangedCallback(e=>Change(p=>info.SetValue(p,e.newValue),physical));parent.Add(toggle);syncFields[field]=()=>toggle.SetValueWithoutNotify((bool)info.GetValue(draft.parameters));}
        void DrawViewport()
        {
            var rect=GUILayoutUtility.GetRect(100,100,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));if(rect.width<2||rect.height<2)return;
            var e=Event.current;
            if(e.type==EventType.MouseDown&&rect.Contains(e.mousePosition)&&e.button==0){dragging=true;e.Use();}
            if(e.type==EventType.MouseUp)dragging=false;
            if(e.type==EventType.MouseDrag&&dragging){Change(p=>p.cameraAngle=Mathf.Clamp(p.cameraAngle+e.delta.x*.24f,-34,34),false);e.Use();}
            if(e.type==EventType.ScrollWheel&&rect.Contains(e.mousePosition)){Change(p=>p.zoom=Mathf.Clamp(p.zoom-e.delta.y*.01f,.72f,1.23f),false);e.Use();}
            if(e.type!=EventType.Repaint)return;
            if(EditorApplication.isPlaying){if(playCamera&&playCourse){EnsureTexture((int)rect.width,(int)rect.height);playCamera.targetTexture=texture;playCamera.aspect=rect.width/rect.height;playCourse.FrameCamera(playCamera,draft.parameters.cameraAngle,draft.parameters.zoom);playCamera.Render();GUI.DrawTexture(rect,texture,ScaleMode.StretchToFill);}else GUI.Label(rect,"A different Play session is active. Stop it to test this level.");return;}
            if(rebuild||preview==null||!editCourse||!((MeshCollider)editCourse.UpperLanding).sharedMesh)BuildPreview();if(preview==null)return;
            preview.BeginPreview(rect,GUIStyle.none);preview.camera.aspect=rect.width/rect.height;editCourse.FrameCamera(preview.camera,draft.parameters.cameraAngle,draft.parameters.zoom);editCourse.SetLabels(draft.parameters.showLabels);preview.Render(true);var image=preview.EndPreview();GUI.DrawTexture(rect,image,ScaleMode.StretchToFill);
        }
        void BuildPreview()
        {
            DisposePreview();preview=new PreviewRenderUtility();preview.ambientColor=new Color(.5f,.5f,.5f);preview.lights[0].intensity=1.5f;preview.lights[0].transform.rotation=Quaternion.Euler(45,145,0);preview.lights[1].intensity=.5f;
            var root=new GameObject("25D editor preview");preview.AddSingleGO(root);editCourse=CreateContent(root,out _,out _,false);rebuild=false;
        }
        GravityGarage25DCourse CreateContent(GameObject root,out GravityGarage25DRun controller,out Camera camera,bool playing)
        {
            var courseRoot=new GameObject("Course");courseRoot.transform.SetParent(root.transform,false);var course=courseRoot.AddComponent<GravityGarage25DCourse>();course.Build(draft.parameters,GravityGarage25DCourse.UnitScale*.8f);
            var worldRoot=new GameObject("Vehicle");worldRoot.transform.SetParent(root.transform,false);var world=worldRoot.AddComponent<AssemblyWorld>();var parts=GravityGarage25DVehicle.Create(world,draft.parameters,new Pose(Vector3.right*100,Quaternion.identity),.8f);GravityGarage25DVehicle.Place(parts,course);
            controller=null;camera=null;if(playing){controller=root.AddComponent<GravityGarage25DRun>();controller.Bind(course,parts,parts.First(p=>p.BuildingCore));var cam=new GameObject("Preview camera");cam.transform.SetParent(root.transform,false);camera=cam.AddComponent<Camera>();camera.enabled=false;var light=new GameObject("Diorama light");light.transform.SetParent(root.transform,false);light.transform.rotation=Quaternion.Euler(45,145,0);var l=light.AddComponent<Light>();l.type=LightType.Directional;l.intensity=1.2f;}
            if(!playing)ProtectPreviewResources(root);return course;
        }
        static void ProtectPreviewResources(GameObject root)
        {
            // Preview scenes are excluded from the normal scene resource scan.
            // Keep generated resources alive across Unity's unload-unused pass.
            var resources=root.GetComponentsInChildren<MeshFilter>(true).Select(f=>(UnityEngine.Object)f.sharedMesh)
                .Concat(root.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Cast<UnityEngine.Object>())
                .Concat(root.GetComponentsInChildren<Collider>(true).Select(c=>(UnityEngine.Object)c.sharedMaterial));
            foreach(var resource in resources)if(resource&&!EditorUtility.IsPersistent(resource))resource.hideFlags=HideFlags.HideAndDontSave;
        }
        void StartTest()
        {
            if(EditorApplication.isPlaying){if(run){run.StartRun();Time.timeScale=playback;}return;}
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            EnsureAssets();SessionState.SetString(PreviousScene,AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));SessionState.SetFloat(Clock,Time.timeScale);SessionState.SetFloat(Step,Time.fixedDeltaTime);SessionState.SetBool(Session+".Started",false);SessionState.SetBool(Session,true);
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(PreviewScene);DisposePreview();EditorApplication.EnterPlaymode();
        }
        void PlayChanged(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.ExitingEditMode)DisposePreview();
            if(!SessionState.GetBool(Session,false)){if(state==PlayModeStateChange.EnteredEditMode)rebuild=true;CreateGUI();return;}
            if(state==PlayModeStateChange.EnteredPlayMode){SessionState.SetBool(Session+".Started",true);playRoot=new GameObject("Gravity Garage 2.5D test");playCourse=CreateContent(playRoot,out run,out playCamera,true);Time.fixedDeltaTime=Mathf.Sqrt(playCourse.WorldScale)/240;Time.timeScale=playback;run.StartRun();CreateGUI();}
            if(state==PlayModeStateChange.ExitingPlayMode){Time.timeScale=SessionState.GetFloat(Clock,1);Time.fixedDeltaTime=SessionState.GetFloat(Step,.02f);}
            if(state==PlayModeStateChange.EnteredEditMode){EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(PreviousScene,""));SessionState.SetBool(Session,false);SessionState.SetBool(Session+".Started",false);ReleaseTexture();playRoot=null;run=null;playCourse=null;playCamera=null;rebuild=true;CreateGUI();}
        }
        void Tick()
        {
            if(EditorApplication.timeSinceStartup-lastPaint<1.0/30)return;lastPaint=EditorApplication.timeSinceStartup;
            if(EditorApplication.isPlaying&&SessionState.GetBool(Session,false)&&!run){var active=GameObject.Find("Gravity Garage 2.5D test");if(active){playRoot=active;run=active.GetComponent<GravityGarage25DRun>();playCourse=active.GetComponentInChildren<GravityGarage25DCourse>();playCamera=active.GetComponentInChildren<Camera>();}}
            if(statValues.Count==5){statValues[0].text=(run?run.Speed:0).ToString("F2")+" m/s";statValues[1].text=(run?run.Elapsed:0).ToString("F2")+" s";statValues[2].text=(run?run.Progress:0).ToString("F0")+"%";statValues[3].text=run?$"{run.MaxSpeed:F2} / {(float.IsInfinity(run.MinSpeed)?0:run.MinSpeed):F2}":"0.00 / 0.00";statValues[4].text=run?(run.Paused?"Paused":run.Stage.ToString()):"Ready";}
            if(feedback!=null&&run){feedback.text=run.Feedback;feedback.style.color=run.Stage==Garage25DStage.Failed?new Color(.91f,.47f,.43f):Mint;}
            int current=!run?0:run.Stage==Garage25DStage.Complete?4:run.Stage==Garage25DStage.Lower?3:run.Stage==Garage25DStage.Pipe?2:run.Stage==Garage25DStage.Airborne||run.Stage==Garage25DStage.Landing?1:0;
            for(int i=0;i<stageLabels.Count;i++)stageLabels[i].style.color=i<current?Mint:i==current?new Color(1,.80f,.51f):new Color(.60f,.70f,.72f);
            viewport?.MarkDirtyRepaint();
        }
        void DisposePreview(){preview?.Cleanup();preview=null;editCourse=null;}
        void EnsureTexture(int w,int h){if(texture&&texture.width==w&&texture.height==h)return;ReleaseTexture();texture=new RenderTexture(w,h,24){hideFlags=HideFlags.HideAndDontSave};texture.Create();}
        void ReleaseTexture(){if(playCamera)playCamera.targetTexture=null;if(texture){texture.Release();DestroyImmediate(texture);}texture=null;}
        bool ConfirmDraft(){if(!hasUnsavedChanges)return true;int answer=EditorUtility.DisplayDialogComplex("Unsaved level","Save this level before replacing the draft?","Save","Cancel","Discard");return answer==2||(answer==0&&Save(false));}
        void Replace(GravityGarageLevelAsset selected){if(!ConfirmDraft())return;Undo.RecordObject(draft,"Open Gravity Garage level");asset=selected;draft.parameters=selected?selected.parameters.Copy():new GravityGarage25DParameters();savedJson=GravityGarageLevelAsset.Export(draft.parameters);hasUnsavedChanges=false;rebuild=true;CreateGUI();}
        void OpenAsset(){var path=EditorUtility.OpenFilePanel("Open Gravity Garage level",Application.dataPath,"asset");if(string.IsNullOrEmpty(path))return;var relative=FileUtil.GetProjectRelativePath(path);var selected=AssetDatabase.LoadAssetAtPath<GravityGarageLevelAsset>(relative);if(!selected){EditorUtility.DisplayDialog("Open level","Choose a Gravity Garage level asset inside this project.","OK");return;}Replace(selected);}
        bool Save(bool saveAs)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return false;string path=asset&&!saveAs?AssetDatabase.GetAssetPath(asset):EditorUtility.SaveFilePanelInProject("Save Gravity Garage level",asset?asset.name:"New Gravity Garage Level","asset","Choose a level asset path.","Assets/MetaHackthone/Levels");if(string.IsNullOrEmpty(path))return false;
            var existing=AssetDatabase.LoadMainAssetAtPath(path);if(existing&&existing is not GravityGarageLevelAsset){EditorUtility.DisplayDialog("Save level","This path contains another asset type.","OK");return false;}
            asset=existing as GravityGarageLevelAsset;if(!asset){asset=CreateInstance<GravityGarageLevelAsset>();AssetDatabase.CreateAsset(asset,path);}Undo.RecordObject(asset,"Save Gravity Garage level");asset.parameters=draft.parameters.Copy();asset.schemaVersion=1;UpdateThumbnail(asset);EditorUtility.SetDirty(asset);AssetDatabase.SaveAssets();savedJson=GravityGarageLevelAsset.Export(draft.parameters);hasUnsavedChanges=false;return true;
        }
        public override void SaveChanges(){if(Save(false))base.SaveChanges();}
        public override void DiscardChanges(){base.DiscardChanges();}
        void Import(){var path=EditorUtility.OpenFilePanel("Import level JSON","","json");if(string.IsNullOrEmpty(path))return;try{var p=GravityGarageLevelAsset.Import(File.ReadAllText(path));if(!ConfirmDraft())return;Undo.RecordObject(draft,"Import Gravity Garage level");draft.parameters=p;asset=null;Dirty();rebuild=true;CreateGUI();}catch(Exception error){EditorUtility.DisplayDialog("Invalid level JSON",error.Message,"OK");}}
        void Export(){var path=EditorUtility.SaveFilePanel("Export level JSON","",asset?asset.name:"GravityGarage","json");if(!string.IsNullOrEmpty(path))File.WriteAllText(path,GravityGarageLevelAsset.Export(draft.parameters));}
        void Apply()
        {
            if(!asset||hasUnsavedChanges){if(!Save(false))return;}
            // Changeset 34's scene entry owns a separate settings asset from the old builder.
            var entry=Resources.FindObjectsOfTypeAll<CloudGarden.Integration.DownhillEntry>().FirstOrDefault(e=>e.gameObject.scene.path=="Assets/MetaHackthone/Scenes/DownhillSandbox.unity");
            var settings=entry?entry.Settings:AssetDatabase.LoadAssetAtPath<DownhillSettings>("Assets/MetaHackthone/Generated/GravityGarageSettings.asset");
            if(!settings)throw new InvalidOperationException("Sandbox settings asset is missing.");
            Undo.RecordObject(settings,"Apply Gravity Garage 2.5D level");settings.Level25D=asset;settings.UseGravityGarage=true;EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();feedback.text="Applied "+asset.name+". Open DownhillSandbox to build and test this level.";
        }
        static void UpdateThumbnail(GravityGarageLevelAsset level)
        {
            var renderer=new PreviewRenderUtility();var root=new GameObject("Level thumbnail");renderer.AddSingleGO(root);
            try {
                renderer.ambientColor=new Color(.5f,.5f,.5f);renderer.lights[0].intensity=1.5f;renderer.lights[0].transform.rotation=Quaternion.Euler(45,145,0);renderer.lights[1].intensity=.5f;
                var course=root.AddComponent<GravityGarage25DCourse>();course.Build(level.parameters,GravityGarage25DCourse.UnitScale*.8f);
                var car=new GameObject("Reference car");car.transform.SetParent(root.transform,false);var world=car.AddComponent<AssemblyWorld>();var parts=GravityGarage25DVehicle.Create(world,level.parameters,new Pose(Vector3.right*100,Quaternion.identity),.8f);GravityGarage25DVehicle.Place(parts,course);
                ProtectPreviewResources(root);var rect=new Rect(0,0,384,216);renderer.BeginPreview(rect,GUIStyle.none);renderer.camera.aspect=rect.width/rect.height;course.FrameCamera(renderer.camera,level.parameters.cameraAngle,level.parameters.zoom);renderer.Render(true);var output=renderer.EndPreview();
                var previous=RenderTexture.active;var temporary=RenderTexture.GetTemporary(384,216,0);
                try{Graphics.Blit(output,temporary);RenderTexture.active=temporary;if(!level.thumbnail){level.thumbnail=new Texture2D(384,216,TextureFormat.RGB24,false){name="Level preview",hideFlags=HideFlags.HideInHierarchy};AssetDatabase.AddObjectToAsset(level.thumbnail,level);}level.thumbnail.ReadPixels(rect,0,0);level.thumbnail.Apply();EditorUtility.SetDirty(level.thumbnail);}
                finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(temporary);}
            }finally{renderer.Cleanup();}
        }
        public static void EnsureAssets()
        {
            const string folder="Assets/MetaHackthone/Levels";if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/MetaHackthone","Levels");
            foreach(var name in new[]{"GravityGarage25D_Default","GravityGarage25D_Challenge","GravityGarage25D_UnityCalibrated"}){
                string path=folder+"/"+name+".asset";var a=AssetDatabase.LoadAssetAtPath<GravityGarageLevelAsset>(path);
                if(!a){a=CreateInstance<GravityGarageLevelAsset>();if(name.EndsWith("Challenge"))a.parameters.Challenge();if(name.EndsWith("UnityCalibrated"))a.parameters=GravityGarage25DParameters.UnityCalibrated();AssetDatabase.CreateAsset(a,path);}
                if(!a.thumbnail){UpdateThumbnail(a);EditorUtility.SetDirty(a);}
            }
            if(!AssetDatabase.LoadAssetAtPath<SceneAsset>(PreviewScene)){var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Additive);EditorSceneManager.SaveScene(scene,PreviewScene);EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(previous);}AssetDatabase.SaveAssets();
        }
    }
}

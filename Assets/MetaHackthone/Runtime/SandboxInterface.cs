using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PhysicalAssembly
{
    // Authored world-space presentation. Only explicit layout/arena placement writes a pose.
    public sealed class SandboxInterface : MonoBehaviour
    {
        public Transform Root, LeftPanel, RightPanel, BuildActions, TestActions, CountdownAnchor, StatusAnchor;
        public WorkshopButton ResetButton,NextLevelButton;
        public WorkshopButton RotateLeftButton,RotateRightButton,ExampleButton;
        public int DesignerControlsVersion;
        public Vector3 WorkbenchOffset;
        public int CourseNumber=1,CourseCount=1;
        public string CourseTitle="Downhill Sandbox";
        public Texture2D CoursePreview;
        [SerializeField] TextMesh courseNumberLabel,courseTitleLabel;
        [SerializeField] Renderer previewRenderer;
        [SerializeField] bool configured;
        [SerializeField] Transform frontMarker;
        readonly List<Object> resources=new();
        DownhillPresentation presentation;
        public bool IsConfigured => configured;
        public static readonly Color Cyan=new(.12f,.75f,1);
        public static readonly Color Green=new(.35f,1,.65f);
        public static readonly Color Orange=new(1,.35f,.22f);

        public void Initialize(DownhillPresentation owner,DownhillCourse level,BuildingSceneLayout layout,DownhillSettings settings)
        {
            presentation=owner;
            if(!configured)
            {
                Root=Child("Stationary sandbox UI",transform);
                Root.SetPositionAndRotation(owner.AssemblyAnchor.position,level.WorkbenchFrame.rotation);
                LeftPanel=Child("Left action panel",Root);
                LeftPanel.localPosition=settings.StartBuildOffset; LeftPanel.localRotation=Quaternion.Euler(0,-15,0);
                BuildActions=Child("Build actions",LeftPanel); TestActions=Child("Test actions",LeftPanel);
                var oldTest=owner.TestControls;
                Place(owner.ReadyButton.transform,BuildActions,Vector3.zero);
                Place(owner.RetryButton.transform,TestActions,Vector3.zero);
                Place(owner.BackButton.transform,TestActions,Vector3.down*.26f);
                Place(owner.ExitButton.transform,LeftPanel,Vector3.down*.52f);
                owner.TestControls=TestActions;
                if(oldTest && oldTest!=TestActions) oldTest.gameObject.SetActive(false);
                ResetButton=CreateButton("RESET","Sandbox Reset",BuildActions,Vector3.down*.26f);
                owner.ExitButton.Command="Sandbox Quit";
                Panel("Action panel backing",LeftPanel,new Vector3(0,-.26f,.018f),new Vector2(.26f,.79f));

                RightPanel=Child("Current course card",Root);
                RightPanel.localPosition=new Vector3(-settings.StartBuildOffset.x+.15f,settings.StartBuildOffset.y+.10f,settings.StartBuildOffset.z);
                RightPanel.localRotation=Quaternion.Euler(0,15,0);
                Panel("Course card backing",RightPanel,Vector3.zero,new Vector2(.36f,.38f));
                Text("Card heading",RightPanel,new Vector3(0,.145f,-.014f),"CURRENT LEVEL",.017f,Cyan);
                courseNumberLabel=Text("Course number",RightPanel,new Vector3(0,.10f,-.014f),"LEVEL 1",.022f,Color.white);
                previewRenderer=Quad("Course thumbnail",RightPanel,new Vector3(0,-.004f,-.014f),new Vector2(.31f,.155f),Color.white);
                courseTitleLabel=Text("Course title",RightPanel,new Vector3(0,-.124f,-.014f),CourseTitle,.022f,Color.white);
                Text("Course count",RightPanel,new Vector3(0,-.16f,-.014f),"1 / 1",.014f,Cyan);

                CountdownAnchor=Child("Fixed countdown anchor",Root); CountdownAnchor.localPosition=new Vector3(0,.65f,.25f);
                StatusAnchor=Child("Fixed status anchor",Root); StatusAnchor.localPosition=new Vector3(0,.47f,.20f);
                Place(owner.Countdown.transform,CountdownAnchor,Vector3.zero);
                owner.Countdown.color=Color.white;
                Place(level.StatusLabel.transform,StatusAnchor,Vector3.zero);
                level.StatusLabel.fontSize=64; level.StatusLabel.characterSize=.0044f;
                level.StatusLabel.alignment=TextAlignment.Center; level.StatusLabel.anchor=TextAnchor.MiddleCenter;
                StyleWorkbench(owner,level,layout);
                foreach(var label in level.SandboxFrame.GetComponentsInChildren<TextMesh>(true))
                    if(label.text=="DOWNHILL SANDBOX") label.gameObject.SetActive(false);
                configured=true;
            }
            Style(owner.ReadyButton,settings,"START",Green,SandboxTileIcon.Play);
            Style(ResetButton,settings,"RESET",Cyan,SandboxTileIcon.Reset);
            Style(owner.RetryButton,settings,"RETRY",Green,SandboxTileIcon.Reset);
            Style(owner.BackButton,settings,"BACK TO\nBUILD",Cyan,SandboxTileIcon.Back);
            Style(owner.ExitButton,settings,"QUIT",Orange,SandboxTileIcon.Quit);
            if(settings.DesignerBuilder) EnsureDesignerControls(settings);
            if(level.SandboxPolish) foreach(var control in new[]{RotateLeftButton,RotateRightButton,ExampleButton})
                if(control) control.transform.SetParent(Root,true);
            if(settings.Campaign) {
                if(!NextLevelButton) NextLevelButton=CreateButton("NEXT LEVEL","Sandbox Next Level",LeftPanel,Vector3.zero);
                // Migrate authored buttons away from the old below-workbench position.
                Place(NextLevelButton.transform,LeftPanel,Vector3.zero);
                Style(NextLevelButton,settings,"NEXT\nLEVEL",Green,SandboxTileIcon.Play);
                NextLevelButton.transform.localScale=owner.RetryButton.transform.localScale;
                SetNextLevelAvailable(false);
            }
            if(level.SandboxPolish) foreach(var tile in LeftPanel.GetComponentsInChildren<FloatingStartButton>(true)) tile.ConfigurePalmTile();
            FitActionPanel(BuildActions.gameObject.activeSelf);
            RefreshCourseCard();
            if(layout.Menu) layout.Menu.gameObject.SetActive(false);
        }
        public void SetNextLevelAvailable(bool available)
        {
            if(!NextLevelButton) return;
            bool changed=NextLevelButton.gameObject.activeSelf!=available;
            NextLevelButton.gameObject.SetActive(available);
            NextLevelButton.Interactable=available;
            if(changed) FitActionPanel(BuildActions.gameObject.activeSelf);
        }
        void EnsureDesignerControls(DownhillSettings settings)
        {
            if(!RotateLeftButton) RotateLeftButton=CreateButton("LEFT 90", "Sandbox Rotate Left",BuildActions,new Vector3(.31f,0,0));
            if(!RotateRightButton) RotateRightButton=CreateButton("RIGHT 90", "Sandbox Rotate Right",BuildActions,new Vector3(.31f,-.26f,0));
            if(!ExampleButton) ExampleButton=CreateButton("EXAMPLE", "Sandbox Example",BuildActions,new Vector3(.31f,-.52f,0));
            Style(RotateLeftButton,settings,"LEFT 90",Cyan,SandboxTileIcon.Reset);
            Style(RotateRightButton,settings,"RIGHT 90",Cyan,SandboxTileIcon.Reset);
            Style(ExampleButton,settings,"EXAMPLE",Green,SandboxTileIcon.Play);
            var controls=new[]{RotateLeftButton,RotateRightButton,ExampleButton};
            for(int i=0;i<controls.Length;i++) {
                controls[i].transform.SetPositionAndRotation(Root.TransformPoint(new Vector3((i-1)*.23f,-.22f,-.40f)),Root.rotation);
                controls[i].transform.localScale*=.8f;
            }
            foreach(var t in presentation.WorkbenchVisuals.GetComponentsInChildren<Transform>(true))
                if(t.name.Contains("tray") || t.name=="LARGE" || t.name=="SMALL") t.gameObject.SetActive(false);
            foreach(var button in controls) button.gameObject.SetActive(DesignModeSession.Enabled);
            DesignerControlsVersion=1;
        }
        public void Place(Vector3 assemblyPosition,Quaternion frameRotation)
        {
            Root.SetPositionAndRotation(assemblyPosition+frameRotation*WorkbenchOffset,frameRotation);
        }
        public void SetBuilding(bool building)
        {
            BuildActions.gameObject.SetActive(building); TestActions.gameObject.SetActive(!building);
            presentation.ExitButton.gameObject.SetActive(true);
            FitActionPanel(building);
        }
        public void FitActionPanel(bool building)
        {
            if(!presentation) presentation=GetComponent<DownhillPresentation>();
            var buttons=building?new[]{presentation.ReadyButton,ResetButton,presentation.ExitButton}
                :new[]{presentation.RetryButton,presentation.BackButton,presentation.ExitButton};
            float gap=.02f/3f, padding=gap*.75f;
            float y=0, width=0, previousHeight=0;
            for(int i=0;i<buttons.Length;i++) {
                var t=buttons[i].transform; float height=t.localScale.y;
                if(i>0) y-=previousHeight*.5f+gap+height*.5f;
                t.localPosition=new Vector3(0,y,0);
                width=Mathf.Max(width,t.localScale.x); previousHeight=height;
            }
            width+=gap*2;
            float top=buttons[0].transform.localScale.y*.5f+padding;
            if(NextLevelButton) {
                var next=NextLevelButton.transform;
                next.localPosition=new Vector3(0,buttons[0].transform.localScale.y*.5f+gap+next.localScale.y*.5f,0);
                if(NextLevelButton.gameObject.activeSelf) {
                    top=next.localPosition.y+next.localScale.y*.5f+padding;
                    width=Mathf.Max(width,next.localScale.x+gap*2);
                }
            }
            float bottom=y-previousHeight*.5f-padding, center=(top+bottom)*.5f;
            float heightTotal=top-bottom, border=.001f;
            void Resize(string suffix,Vector3 position,Vector2 size) {
                var part=LeftPanel.Find("Action panel backing"+suffix);
                if(!part) return;
                var original=part.GetComponent<MeshFilter>().sharedMesh.bounds.size;
                part.localPosition=position;
                part.localScale=new Vector3(size.x/original.x,size.y/original.y,1);
            }
            Resize("",new Vector3(0,center,.018f),new Vector2(width,heightTotal));
            Resize(" top",new Vector3(0,top,.016f),new Vector2(width,border));
            Resize(" bottom",new Vector3(0,bottom,.016f),new Vector2(width,border));
            Resize(" left",new Vector3(-width*.5f,center,.016f),new Vector2(border,heightTotal));
            Resize(" right",new Vector3(width*.5f,center,.016f),new Vector2(border,heightTotal));
        }
        public void AlignThumbnailWithBacking()
        {
            var backing=RightPanel?RightPanel.Find("Course card backing"):null;
            if(backing && previewRenderer)previewRenderer.transform.SetPositionAndRotation(backing.TransformPoint(new Vector3(0,-.004f,-.002f)),backing.rotation);
        }
        public void RefreshCourseCard()
        {
            if(RightPanel) foreach(var label in RightPanel.GetComponentsInChildren<TextMesh>(true)) if(label.name=="Course count") label.text=CourseNumber+" / "+CourseCount;
            if(courseNumberLabel) courseNumberLabel.text="LEVEL "+CourseNumber;
            if(courseTitleLabel) courseTitleLabel.text=CourseTitle;
            if(previewRenderer)
            {
                if(!Application.isPlaying)
                {
                    previewRenderer.sharedMaterial.SetTexture("_BaseMap",CoursePreview?CoursePreview:Texture2D.whiteTexture);
                    previewRenderer.sharedMaterial.SetColor("_BaseColor",CoursePreview?Color.white:new Color(.08f,.19f,.28f));
                }
                var properties=new MaterialPropertyBlock();
                properties.SetTexture("_BaseMap",CoursePreview?CoursePreview:Texture2D.whiteTexture);
                properties.SetColor("_BaseColor",CoursePreview?Color.white:new Color(.08f,.19f,.28f));
                previewRenderer.SetPropertyBlock(properties);
            }
        }
        void StyleWorkbench(DownhillPresentation owner,DownhillCourse level,BuildingSceneLayout layout)
        {
            foreach(var r in owner.WorkbenchVisuals.GetComponentsInChildren<Renderer>(true))
            {
                if(r.name=="Grid X" || r.name=="Grid Z") r.sharedMaterial=Material(Cyan);
                else if(r.name=="Centered grid pad") r.sharedMaterial=Material(new Color(.025f,.08f,.14f));
            }
            frontMarker=Child("FRONT direction marking",owner.WorkbenchVisuals);
            var point=level.WorkbenchFrame.TransformPoint(new Vector3(0,0,-.12f));
            point.y=layout.Surface?layout.Surface.bounds.max.y+.006f:level.WorkbenchFrame.TransformPoint(new Vector3(0,.706f,0)).y;
            var pad=owner.FindGridElement("Centered grid pad");
            if(pad && pad.GetComponent<MeshFilter>().sharedMesh.name=="Circular build pad")
                point=pad.position+level.WorkbenchFrame.up*.005f-level.WorkbenchFrame.forward*(pad.lossyScale.z*.31f);
            frontMarker.SetPositionAndRotation(point,level.WorkbenchFrame.rotation*Quaternion.Euler(90,0,0));
            // Keep physical dimensions independent of an authored table scale.
            var parentScale=frontMarker.parent.lossyScale;
            frontMarker.localScale=new Vector3(1/parentScale.x,1/parentScale.z,1/parentScale.y);
            Text("FRONT",frontMarker,Vector3.zero,"FRONT",.026f,Color.white);
            var arrow=Child("Direction arrow",frontMarker); arrow.localPosition=new Vector3(0,.065f,0);
            var mesh=new Mesh { name="Front arrow" };
            mesh.vertices=new[]{new Vector3(-.012f,0,0),new Vector3(.012f,0,0),new Vector3(.012f,.04f,0),new Vector3(.035f,.04f,0),new Vector3(0,.08f,0),new Vector3(-.035f,.04f,0),new Vector3(-.012f,.04f,0)};
            mesh.triangles=new[]{0,1,2,0,2,6,5,3,4}; mesh.RecalculateNormals(); mesh.RecalculateBounds(); resources.Add(mesh);
            arrow.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            arrow.gameObject.AddComponent<MeshRenderer>().sharedMaterial=Material(Cyan);
        }
        static void Style(WorkshopButton button,DownhillSettings settings,string title,Color color,SandboxTileIcon icon)
        {
            var tile=button.GetComponent<FloatingStartButton>(); if(!tile) tile=button.gameObject.AddComponent<FloatingStartButton>();
            tile.Configure(settings,title,color,icon);
        }
        WorkshopButton CreateButton(string title,string command,Transform parent,Vector3 position)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=title;
            Place(go.transform,parent,position); go.GetComponent<Collider>().isTrigger=true;
            Text("Caption",go.transform,Vector3.zero,title,.025f,Color.white);
            var button=go.AddComponent<WorkshopButton>(); button.Command=command; return button;
        }
        void Panel(string title,Transform parent,Vector3 position,Vector2 size)
        {
            Quad(title,parent,position,size,new Color(.018f,.04f,.075f));
            Quad(title+" top",parent,position+new Vector3(0,size.y*.5f,-.002f),new Vector2(size.x,.003f),Cyan);
            Quad(title+" bottom",parent,position+new Vector3(0,-size.y*.5f,-.002f),new Vector2(size.x,.003f),Cyan);
            Quad(title+" left",parent,position+new Vector3(-size.x*.5f,0,-.002f),new Vector2(.003f,size.y),Cyan);
            Quad(title+" right",parent,position+new Vector3(size.x*.5f,0,-.002f),new Vector2(.003f,size.y),Cyan);
        }
        Renderer Quad(string title,Transform parent,Vector3 position,Vector2 size,Color color)
        {
            var t=Child(title,parent); t.localPosition=position;
            var mesh=new Mesh { name=title };
            mesh.vertices=new[]{new Vector3(-size.x/2,-size.y/2,0),new Vector3(-size.x/2,size.y/2,0),new Vector3(size.x/2,size.y/2,0),new Vector3(size.x/2,-size.y/2,0)};
            mesh.uv=new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(1,1),new Vector2(1,0)};
            mesh.triangles=new[]{0,1,2,0,2,3}; mesh.RecalculateNormals(); mesh.RecalculateBounds(); resources.Add(mesh);
            t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=t.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial=Material(color);
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false; return renderer;
        }
        Material Material(Color color)
        {
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name="Sandbox UI" };
            material.SetColor("_BaseColor",color); material.SetFloat("_Cull",(float)CullMode.Off); resources.Add(material); return material;
        }
        static TextMesh Text(string name,Transform parent,Vector3 position,string text,float height,Color color)
        {
            var t=Child(name,parent); t.localPosition=position;
            var label=t.gameObject.AddComponent<TextMesh>(); label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<Renderer>().sharedMaterial=label.font.material;
            label.fontSize=64; label.characterSize=height*10/64; label.anchor=TextAnchor.MiddleCenter; label.alignment=TextAlignment.Center;
            label.text=text; label.color=color; return label;
        }
        static Transform Child(string name,Transform parent)
        { var t=new GameObject(name).transform; t.SetParent(parent,false); return t; }
        static void Place(Transform child,Transform parent,Vector3 position)
        { child.SetParent(parent,false); child.localPosition=position; child.localRotation=Quaternion.identity; }
        public void ReleaseGeneratedResourceOwnership() => resources.Clear();
        void OnDestroy()
        {
            foreach(var resource in resources) if(resource)
            { if(Application.isPlaying) Destroy(resource); else DestroyImmediate(resource); }
        }
    }
}

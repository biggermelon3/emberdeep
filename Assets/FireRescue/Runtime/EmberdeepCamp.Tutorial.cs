using UnityEngine;

namespace FireRescue
{
    // LoopSpec §9: the camp tutorial's menu side (workstream T). "How to play" on the main page plays it; SKIP leaves it.
    // Finishing or skipping is remembered in PlayerPrefs.
    // First launch (headset test 2026-11-22): it never starts by itself. Pressing NEW GAME or CONTINUE before the
    // tutorial was ever done or skipped asks first (NEW HERE? HOW TO PLAY / SKIP). HOW TO PLAY runs it and then starts the
    // game; SKIP starts the game. Either answer is remembered (the same TutorialPrefsKey), so it asks once.
    // While it runs the sign shows three step chips (palm water, pump, sheep) and SKIP, and no sentences: a forest spirit
    // talks the player through it (voice-over with subtitles under the spirit, EmberdeepForestSpirit) and the ghost hands
    // in the camp show the moves (EmberdeepCampPlay).
    // Headset test 3 (2026-11-22): aiming palm water at the campfire pointed the hand's ray at the full sign (SKIP sat
    // just over the island), so the menu ray came up and took the water. While the tutorial runs, the big board folds
    // away and a small plank hangs at the top of the posts instead (TutorialBoard*: one row, chips then SKIP), far above
    // every aiming line down to the island; the near-panel ray reach shrinks to TutorialShowReach / TutorialHideReach, and
    // a water hand in the open-palm aim pose gets a ray only on a direct button hit (EmberdeepHandRayMenu.PalmAiming).
    public sealed partial class EmberdeepCamp
    {
        // The plank, menu-board space: as wide as the board (it spans the posts), .16 tall, its top 3 cm under the board's.
        public const float TutorialBoardY=.30f,TutorialBoardWidth=.90f,TutorialBoardHeight=.16f;
        public const float TutorialShowReach=.03f,TutorialHideReach=.05f;
        public const string TutorialPrefsKey="Emberdeep.CampTutorial";
        public static bool TutorialSeen=>PlayerPrefs.GetInt(TutorialPrefsKey,0)==1;
        public static void ResetTutorialSeen(){PlayerPrefs.DeleteKey(TutorialPrefsKey);PlayerPrefs.Save();}
        // The editor/desktop path never asks (dev runs and tests press NEW GAME straight through); a test can opt in.
        public static bool TutorialPromptOnDesktop;
        public const float PromptTitleY=.16f,PromptTextY=.095f,PromptFirstY=-.055f,PromptSkipY=-.18f;
        public const string PromptTitle="NEW HERE?",PromptText="Learn the hand moves at camp first";
        // Desktop only: the view tips down while the tutorial runs, so the island and the pump are in sight.
        public const float DesktopTutorialPitch=30;
        // One row on the plank: the chips left of centre, SKIP right (was chips at -.06 and SKIP at -.22 on the full board).
        public const float ChipsY=TutorialBoardY,ChipsX=-.19f,ChipStep=.12f,ChipSize=.06f,SkipX=.24f,SkipY=TutorialBoardY;
        public static readonly Vector2 SkipSize=new(.26f,.062f);
        public bool TutorialSignShown=>tutorialBoard && tutorialBoard.activeInHierarchy;
        public Bounds TutorialSignBounds=>tutorialBoard?tutorialBoard.GetComponent<Renderer>().bounds:default;
        public EmberdeepCampPlay Play {get;private set;}
        public bool TutorialActive=>Play && Play.Flow.Active;
        // Placed, tracked, focused, and not leaving: hands may play at camp.
        public bool InputLive=>wasReady && placement.IsPlaced && !Transitioning && !QuitRequested;
        Transform tutorialPage,promptPage;
        GameObject tutorialBoard;
        System.Action pendingPlay;
        // True while a NEW GAME / CONTINUE press waits on the first-play question or on the tutorial it started.
        public bool PlayPending=>pendingPlay!=null;
        public bool AsksBeforePlay=>!TutorialSeen && Play && Play.Built && (Player.IsXR || TutorialPromptOnDesktop);
        readonly Renderer[] chipDiscs=new Renderer[EmberdeepCampTutorialFlow.Chips],chipIcons=new Renderer[EmberdeepCampTutorialFlow.Chips];
        Material chipDim,chipLit,chipDone,chipIconDim;

        void BuildTutorial()
        {
            tutorialPage=Page("Camp tutorial");
            // The small plank stands in for the board while the page shows (SetTutorialSign hides the board).
            tutorialBoard=Shape("Tutorial board",tutorialPage,new Vector3(0,TutorialBoardY,0),new Vector3(TutorialBoardWidth,TutorialBoardHeight,.025f),boardMaterial);
            chipDim=Tinted(new Color(.09f,.20f,.24f));chipLit=Tinted(new Color(1,.73f,.38f));
            chipDone=Tinted(new Color(.25f,.88f,.91f));chipIconDim=Tinted(new Color(.55f,.68f,.70f));
            var icons=new[]{EmberdeepUiIcons.Drop,EmberdeepUiIcons.Fist,EmberdeepUiIcons.Sheep};
            for(int i=0;i<icons.Length;i++)
            {
                float x=ChipsX+(i-1)*ChipStep;
                if(i>0)Shape("Step link "+i,tutorialPage,new Vector3(x-ChipStep*.5f,ChipsY,-.028f),new Vector3(ChipStep-ChipSize,.005f,.004f),buttonMaterial);
                chipDiscs[i]=EmberdeepUiIcons.Icon("Step chip "+(i+1),tutorialPage,EmberdeepUiIcons.Disc,chipDim,new Vector3(x,ChipsY,-.031f),ChipSize);
                chipIcons[i]=EmberdeepUiIcons.Icon("Step icon "+(i+1),tutorialPage,icons[i],chipIconDim,new Vector3(x,ChipsY,-.036f),ChipSize*.62f);
            }
            Control(tutorialPage,"Skip tutorial","SKIP",new Vector3(SkipX,SkipY,ControlZ),SkipSize,SkipTutorial,EmberdeepUiIcons.Chevron,.018f);
            Play=new GameObject("Camp hand play").AddComponent<EmberdeepCampPlay>();Play.transform.SetParent(transform,false);
            Play.Initialize(this,hands,GetComponent<EmberdeepHandVisuals>());Play.TutorialFinished+=OnTutorialFinished;
            // The first-play question: a label, one prompt line, two buttons (LoopSpec §13), laid out like the quit page.
            promptPage=Page("Tutorial prompt");
            Text(promptPage,PromptTitle,new Vector3(0,PromptTitleY,-.035f),.03f,Color.white);
            Text(promptPage,PromptText,new Vector3(0,PromptTextY,-.035f),.014f,new Color(.85f,.87f,.88f));
            Button(promptPage,"Prompt how to play",PromptFirstY,PlayTutorialFirst,"HOW TO PLAY");
            Button(promptPage,"Prompt skip",PromptSkipY,SkipTutorialPrompt,"SKIP");
            promptPage.gameObject.SetActive(false);
        }
        // NEW GAME / CONTINUE go through here: straight on, or the first-play question first.
        void PlayOrAsk(System.Action play)
        {
            if(Transitioning || QuitRequested || play==null)return;
            if(!AsksBeforePlay){play();return;}
            pendingPlay=play;ShowPage(MenuPage.TutorialPrompt);
        }
        public void PlayTutorialFirst()
        {
            if(ActivePage!=MenuPage.TutorialPrompt)return;
            if(!Play || !Play.Built){SkipTutorialPrompt();return;}
            StartTutorial(); // EndTutorial then carries on into the game (pendingPlay)
        }
        public void SkipTutorialPrompt()
        {
            if(ActivePage!=MenuPage.TutorialPrompt)return;
            PlayerPrefs.SetInt(TutorialPrefsKey,1);PlayerPrefs.Save();
            var play=pendingPlay;pendingPlay=null;
            ShowPage(MenuPage.Main);play?.Invoke();
        }
        Material Tinted(Color color){var m=new Material(boardMaterial);m.SetColor("_BaseColor",color);return m;}
        // The tutorial page swaps the full board (and its title) for the plank, with the hands' occlusion box and the
        // menu's near-panel reach to match; every other page gets the board back.
        void SetTutorialSign(bool tutorial)
        {
            if(menuBoard)menuBoard.SetActive(!tutorial);
            if(signTitle)signTitle.gameObject.SetActive(!tutorial);
            var visuals=GetComponent<EmberdeepHandVisuals>();
            if(visuals)
            {
                if(tutorial)visuals.SetOcclusionBox(0,panel,new Vector3(0,TutorialBoardY,0),new Vector3(TutorialBoardWidth*.5f-.002f,TutorialBoardHeight*.5f-.002f,.011f));
                else visuals.SetOcclusionBox(0,panel,Vector3.zero,BoardOcclusionHalf);
            }
            if(menuInput)
            {
                menuInput.ShowReach=tutorial?TutorialShowReach:EmberdeepHandRayMenu.PanelShowReach;
                menuInput.HideReach=tutorial?TutorialHideReach:EmberdeepHandRayMenu.PanelHideReach;
            }
        }

        // "How to play", and HOW TO PLAY on the first-play question.
        public void StartTutorial()
        {
            if(Transitioning || QuitRequested || !Play || !Play.Built)return;
            if(ActivePage!=MenuPage.TutorialPrompt)pendingPlay=null; // the main page's How to play returns to the menu
            EmberdeepFirstHints.ResetAll(); // learning the moves again brings the in-level ghost hints back (§11b)
            ShowPage(MenuPage.Tutorial);Play.BeginTutorial();
            if(!Player.IsXR)Player.ViewCamera.transform.rotation=Quaternion.Euler(DesktopTutorialPitch,0,0);
            RefreshTutorialChips();
        }
        public void SkipTutorial()
        {
            if(Play)Play.StopTutorial();
            EndTutorial();
        }
        void OnTutorialFinished(bool completed)=>EndTutorial();
        void EndTutorial()
        {
            PlayerPrefs.SetInt(TutorialPrefsKey,1);PlayerPrefs.Save();
            if(!Player.IsXR && Player.ViewCamera)Player.ViewCamera.transform.rotation=Quaternion.identity;
            if(ActivePage==MenuPage.Tutorial)ShowPage(MenuPage.Main);
            // Asked from NEW GAME / CONTINUE: done (or skipped) with the tutorial, the game starts.
            var play=pendingPlay;pendingPlay=null;play?.Invoke();
        }
        void TickTutorialMenu()
        {
            if(ActivePage==MenuPage.Tutorial)RefreshTutorialChips();
        }
        // Done chips turn teal, the current one glows gold and breathes, the rest wait dim.
        void RefreshTutorialChips()
        {
            if(!Play || !tutorialPage)return;
            int lit=Play.Flow.Chip;bool completing=Play.Flow.Completing;
            float pulse=.5f+.5f*Mathf.Sin(Time.unscaledTime*5);
            for(int i=0;i<chipDiscs.Length;i++)
            {
                bool done=i<lit || (i==lit && completing && Play.Flow.Current!=EmberdeepCampTutorialFlow.Step.Sheep);
                bool current=i==lit && !done;
                chipDiscs[i].sharedMaterial=done?chipDone:current?chipLit:chipDim;
                chipIcons[i].sharedMaterial=done || current?boardMaterial:chipIconDim;
                chipDiscs[i].transform.localScale=Vector3.one*ChipSize*(current?1+.08f*pulse:1);
            }
        }
        void ReleaseTutorial()
        {
            if(Play)Play.TutorialFinished-=OnTutorialFinished;
            foreach(var m in new[]{chipDim,chipLit,chipDone,chipIconDim})if(m)Destroy(m);
        }
    }
}

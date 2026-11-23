using UnityEngine;
using UnityEngine.InputSystem;

namespace FireRescue
{
    // Workstream C, LoopSpec §4.6: tree tint, burning readout and see-through fade. The tree build
    // and refresh regions of FireRescueGame.cs call in here. The tint API (_Heat/_Wetness/_Scorch on
    // a MaterialPropertyBlock, plus _Fade) is stable; the aim highlight draws its own mesh.
    public sealed partial class FireRescueGame
    {
        EmberdeepTreeReadout treeReadout;
        public EmberdeepTreeReadout TreeReadout=>treeReadout;
        // Workstream L2 (§4.1-4.4): grass patches and the rooster weathervane. Both persist across
        // incidents (pooled); only their content is rebuilt.
        EmberdeepGrassView grassView;
        EmberdeepWeathervane weathervane;
        public EmberdeepGrassView GrassView=>grassView;
        public EmberdeepWeathervane Weathervane=>weathervane;
        int boardCueFrame=-1;

        void BuildTreeReadout()
        {
            if(!treeReadout){treeReadout=new GameObject("Tree readability").AddComponent<EmberdeepTreeReadout>();treeReadout.transform.SetParent(transform,false);}
            var roots=new Transform[trees.Count];var parts=new Renderer[trees.Count][];
            for(int i=0;i<trees.Count;i++){roots[i]=trees[i].Root;parts[i]=trees[i].Authored??new[]{trees[i].Leaves,trees[i].Trunk};}
            treeReadout.Build(Run,roots,parts);
            BuildBoardCues();
        }

        void BuildBoardCues()
        {
            if(!Arena)return;
            if(!grassView){grassView=new GameObject("Grass patches").AddComponent<EmberdeepGrassView>();grassView.transform.SetParent(Arena,false);}
            grassView.Build(Run);
            if(!weathervane){weathervane=new GameObject("Rooster weathervane").AddComponent<EmberdeepWeathervane>();weathervane.Build(Arena);}
            weathervane.gameObject.SetActive(Run.BoardRules);
            weathervane.Place(Run);weathervane.Settle(Run);
        }

        void RefreshTrees()
        {
            if(treeReadout && Player && Arena)treeReadout.Tick(Run,Arena,Player.ViewCamera,Effects,TreeAimPoint(out var aim),aim);
            float pulse=treeReadout?treeReadout.Pulse:1;
            if(boardCueFrame!=Time.frameCount)
            {
                boardCueFrame=Time.frameCount;
                if(grassView)grassView.Tick(Run,pulse);
                if(weathervane && weathervane.gameObject.activeSelf)weathervane.Tick(Run,Time.unscaledDeltaTime);
            }
            for(int i=0;i<trees.Count && i<Run.Trees.Length;i++)
            {
                var tree=trees[i];var data=Run.Trees[i];bool burning=data.State==TreeState.Burning;
                if(tree.Authored!=null)
                {
                    // A low-heat tree keeps its underside fire-glow until Heat reaches 0.
                    float heat=burning?Mathf.Lerp(EmberdeepTreeReadout.MinimumHeatTint,1,Mathf.Clamp01(data.Heat/100)):0;
                    tint.Clear();tint.SetFloat("_Heat",heat);
                    // Board rules: looks wet exactly while it is protected from catching (§4.3).
                    tint.SetFloat("_Wetness",Run.BoardRules?EmberdeepBoardTuning.WetLook(data.Wetness):Mathf.Clamp01(data.Wetness));
                    tint.SetFloat("_Ember",EmberdeepGrassView.Ember(Run,i,pulse));
                    tint.SetFloat("_Scorch",data.State==TreeState.Burnt?1:data.State==TreeState.Saved?.12f:0);
                    tint.SetFloat("_Fade",treeReadout?treeReadout.FadeOf(i):0);
                    foreach(var renderer in tree.Authored)renderer.SetPropertyBlock(tint);
                }
                else
                {
                    Tint(tree.Leaves,data.State==TreeState.Burnt?new Color(.13f,.12f,.10f):data.State==TreeState.Saved?new Color(.12f,.64f,.37f):new Color(.18f,.36f,.23f));
                    tree.Leaves.transform.localScale=new Vector3(.75f,1.05f,.75f)*(data.State==TreeState.Burnt?.35f:1)*(1+(i%4)*.08f);
                }
            }
        }

        // The point the water is aimed at, if any: palm aim in XR, the held tool's landing marker,
        // else the desktop mouse landing point while a fight is on.
        bool TreeAimPoint(out Vector3 aim)
        {
            aim=default;
            if(!Player || !Arena || ShopOpen || QuitConfirmationOpen)return false;
            if(palmMagic.Aiming){aim=palmMagic.Target;return true;}
            if(aimMarker && aimMarker.gameObject.activeSelf){aim=aimMarker.position;return true;}
            if(Player.IsXR || Mouse.current==null || Run.Phase!=FirePhase.Fighting)return false;
            aim=WaterJet.MouseTarget(Player.ViewCamera.ScreenPointToRay(Mouse.current.position.ReadValue()),Arena,waterTargets);
            return true;
        }
    }
}

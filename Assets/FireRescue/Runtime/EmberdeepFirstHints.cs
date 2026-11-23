using UnityEngine;
using UnityEngine.Rendering;

namespace FireRescue
{
    // LoopSpec §11b first-time hints: a ghost hand plays the move on the player's own hand or object, with pips
    // that count down. A move stops being hinted after the player has done it Shows times; the count lives in
    // PlayerPrefs. No text: this replaces every tutorial line in the spell flow.
    // The game decides which move is relevant and where (FireRescueGame.Hints.cs); this only presents it.
    // Bringing them back (headset test 2 2026-11-22: the owner never saw the bell or slap hint, because test 1 had
    // already retired both and PlayerPrefs survive an APK update):
    // - KeyVersion: a bump makes every hint new again once, on every device (the old counts are ignored, then dropped).
    // - ResetAll: the camp's SHOW HINTS button and starting the camp tutorial (HOW TO PLAY).
    public sealed class EmberdeepFirstHints : MonoBehaviour
    {
        public enum Move { Water, Pump, Bell, Slap }
        public const int Shows=2;
        public const float PipSize=.009f,PipStep=.016f;
        public const string KeyVersion="v2"; // v1 = "Emberdeep.Hint.<Move>" (test builds up to 2026-11-22)
        public static string Key(Move move)=>"Emberdeep.Hint."+KeyVersion+"."+move;
        static string OldKey(Move move)=>"Emberdeep.Hint."+move;
        public static int Count(Move move)=>PlayerPrefs.GetInt(Key(move),0);
        public static int Remaining(Move move)=>Mathf.Max(0,Shows-Count(move));
        public static bool Needed(Move move)=>Remaining(move)>0;
        static readonly Move[] Moves=(Move[])System.Enum.GetValues(typeof(Move));
        // No hint has been performed yet (a settings button can show there is nothing to bring back).
        public static bool AllFresh{get{foreach(var m in Moves)if(Count(m)>0)return false;return true;}}
        public static event System.Action Reset;
        public static void ResetAll()
        {
            foreach(var m in Moves){PlayerPrefs.DeleteKey(Key(m));PlayerPrefs.DeleteKey(OldKey(m));}
            PlayerPrefs.Save();Reset?.Invoke();
        }

        EmberdeepInteractionDemo demo;Transform pips;Renderer[] pipRenderers;Material pipMaterial;MaterialPropertyBlock block;
        public Move? Showing {get;private set;}
        public EmberdeepInteractionDemo Demo=>demo;
        public bool Visible=>demo && demo.gameObject.activeInHierarchy;

        public void Performed(Move move)
        {
            int count=Count(move);if(count>=Shows)return;
            if(PlayerPrefs.HasKey(OldKey(move)))PlayerPrefs.DeleteKey(OldKey(move)); // v1 count: ignored, dropped
            PlayerPrefs.SetInt(Key(move),count+1);PlayerPrefs.Save();
        }
        // pose: where the ghost plays (its +Z faces away from the eye). mirror: water hand on the right.
        public void Present(Move move,Pose pose,float scale,bool mirror,Camera eye)
        {
            if(!Needed(move)){Hide();return;}
            Ensure();
            var action=move==Move.Water?EmberdeepInteractionDemo.Action.Goddess:move==Move.Slap?EmberdeepInteractionDemo.Action.Slap:EmberdeepInteractionDemo.Action.Hammer;
            if(Showing!=move){demo.Show(action);demo.SetCaptionVisible(false);Showing=move;}
            if(!demo.gameObject.activeSelf)demo.gameObject.SetActive(true);
            demo.transform.SetPositionAndRotation(pose.position,pose.rotation);
            demo.transform.localScale=new Vector3(mirror?-scale:scale,scale,scale);
            // Pips: one per remaining repetition, beside the ghost, facing the eye.
            int remaining=Remaining(move);
            pips.gameObject.SetActive(true);
            pips.position=pose.position+(eye?eye.transform.right:Vector3.right)*.075f*scale+Vector3.up*.03f*scale;
            if(eye)pips.rotation=Quaternion.LookRotation(pips.position-eye.transform.position,Vector3.up);
            float pulse=.75f+.25f*Mathf.Sin(Time.unscaledTime*4);
            for(int i=0;i<pipRenderers.Length;i++)
            {
                bool on=i<remaining;pipRenderers[i].gameObject.SetActive(on);if(!on)continue;
                block.Clear();block.SetColor("_BaseColor",new Color(.68f,.92f,1)*pulse);pipRenderers[i].SetPropertyBlock(block);
            }
        }
        public void Hide()
        {
            if(demo && demo.gameObject.activeSelf)demo.gameObject.SetActive(false);
            if(pips && pips.gameObject.activeSelf)pips.gameObject.SetActive(false);
        }
        void Ensure()
        {
            if(demo)return;
            block=new MaterialPropertyBlock();
            var go=new GameObject("First-time hint ghost");go.transform.SetParent(transform,false);go.SetActive(false);
            demo=go.AddComponent<EmberdeepInteractionDemo>();demo.Ghost=true;
            pips=new GameObject("First-time hint pips").transform;pips.SetParent(transform,false);
            var source=Resources.Load<Material>("EmberdeepMenuUnlit");
            pipMaterial=source?new Material(source){name="Hint pips"}:new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Hint pips"};
            pipRenderers=new Renderer[Shows];
            for(int i=0;i<Shows;i++)
            {
                pipRenderers[i]=EmberdeepUiIcons.Icon("Hint pip "+(i+1),pips,EmberdeepUiIcons.Disc,pipMaterial,new Vector3(0,-i*PipStep,0),PipSize);
                pipRenderers[i].shadowCastingMode=ShadowCastingMode.Off;
            }
        }
        void OnDestroy(){if(pipMaterial)Destroy(pipMaterial);}
    }
}

using CloudGarden.ShadowPuzzle;
using TMPro;
using UnityEngine;

namespace ShapeOfMemory
{
    // Teaching by showing, on the level's own orb. On the first level, until the player has rolled the orb about 30
    // degrees once, a glowing ghost pinch (index and thumb tips on two short fingers) reaches the orb's shell, closes,
    // drags it round and lets go, with "PINCH THE ORB AND DRAG" above it. On any level, if the shadow leaves the light
    // for a moment, a line says how to carry the orb back.
    public sealed class MemoryGuide : MonoBehaviour
    {
        public const float TaughtAngle=30,Loop=3.4f,LostFor=1.5f;
        // Labels sit just over the orb's top, a little toward the player.
        static readonly Vector3 Above=new Vector3(0,.35f,-.08f);
        public bool Teaching{get;private set;}
        public bool Hinting=>lostLabel&&lostLabel.gameObject.activeSelf;
        Torch2Workshop game;
        System.Action taught;
        Transform ghost;
        Transform[] fingers=new Transform[2],tips=new Transform[2];
        TMP_Text teachLabel,lostLabel;
        float clock,lost;

        public static MemoryGuide Add(Torch2Workshop game,bool teach,bool hands,Material glow,System.Action taught)
        {
            var guide=game.gameObject.AddComponent<MemoryGuide>();guide.game=game;guide.taught=taught;guide.Teaching=teach;
            guide.lostLabel=MemoryUi.Label(hands?"PINCH THE BALL IN THE MIDDLE\n<size=70%>and carry the orb back into the light</size>":"RIGHT-DRAG THE ORB\n<size=70%>to move it back into the light</size>",
                game.transform,Vector3.zero,Quaternion.identity,.026f,new Vector2(.8f,.1f),MemoryUi.Paper);
            guide.lostLabel.gameObject.SetActive(false);
            if(!teach)return guide;
            guide.teachLabel=MemoryUi.Label(hands?"PINCH THE ORB AND DRAG\n<size=70%>to roll it any way</size>":"LEFT-DRAG THE ORB\n<size=70%>to roll it any way</size>",
                game.transform,Vector3.zero,Quaternion.identity,.026f,new Vector2(.8f,.1f),MemoryUi.Mint);
            guide.ghost=new GameObject("Ghost pinch").transform;guide.ghost.SetParent(game.transform,false);
            for(int i=0;i<2;i++)
            {
                guide.fingers[i]=MemoryUi.Shape(PrimitiveType.Capsule,"Ghost finger",guide.ghost,Vector3.zero,Quaternion.identity,Vector3.one,glow,Color.Lerp(MemoryUi.Mint,Color.white,.4f)).transform;
                guide.tips[i]=MemoryUi.Shape(PrimitiveType.Sphere,"Ghost tip",guide.ghost,Vector3.zero,Quaternion.identity,Vector3.one*.024f,glow,Color.white).transform;
            }
            return guide;
        }
        void Update()
        {
            var centre=game.Position;
            // The shadow off the wall for a moment, and not while held: say how to bring it back.
            lost=game.Projection.Score.visible||game.Busy||game.Completed||!game.input.enabled?0:lost+Time.unscaledDeltaTime;
            lostLabel.gameObject.SetActive(lost>=LostFor&&!Teaching);
            if(lostLabel.gameObject.activeSelf)lostLabel.transform.localPosition=centre+Above;
            if(!Teaching)return;
            if(Quaternion.Angle(game.Projection.Start,game.Rotation)>=TaughtAngle){Stop();taught?.Invoke();return;}
            bool show=game.input.enabled&&!game.Busy&&!game.Completed;
            ghost.gameObject.SetActive(show);teachLabel.gameObject.SetActive(show);if(!show){clock=0;return;}
            teachLabel.transform.localPosition=centre+Above;
            clock=(clock+Time.unscaledDeltaTime)%Loop;Animate(centre,clock);
        }
        // Reach in (0-.7 s), close (.7-1 s), drag 70 degrees round the orb (1-2.3 s), open (2.3-2.6 s), fade away.
        void Animate(Vector3 centre,float t)
        {
            float reach=Smooth(t/.7f),close=Smooth((t-.7f)/.3f)*(1-Smooth((t-2.3f)/.3f)),drag=Smooth((t-1f)/1.3f),fade=1-Smooth((t-2.7f)/.5f);
            // The pinch point on the shell's front right, where a right hand reaches, dragged across the front to the left.
            var start=new Vector3(.45f,.05f,-1f).normalized*Torch2Input.OrbRadius;
            var on=Quaternion.AngleAxis(70*drag,Vector3.up)*start;
            var away=on.normalized*.14f+new Vector3(0,.02f,-.06f);
            var pinch=centre+on+away*(1-reach);
            float gap=Mathf.Lerp(.06f,.008f,close);var up=Vector3.up;var outward=on.normalized;
            var knuckle=pinch+outward*.095f+up*.03f;
            Finger(0,knuckle,pinch+up*gap*.5f,fade);Finger(1,knuckle-up*.045f,pinch-up*gap*.5f,fade);
        }
        void Finger(int i,Vector3 from,Vector3 to,float size)
        {
            var axis=to-from;float length=axis.magnitude;
            fingers[i].localPosition=(from+to)*.5f;fingers[i].localRotation=Quaternion.FromToRotation(Vector3.up,axis);
            fingers[i].localScale=new Vector3(.02f,length*.5f,.02f)*size;
            tips[i].localPosition=to;tips[i].localScale=Vector3.one*.024f*size;
        }
        static float Smooth(float t){t=Mathf.Clamp01(t);return t*t*(3-2*t);}
        void Stop(){Teaching=false;if(ghost)Destroy(ghost.gameObject);if(teachLabel)Destroy(teachLabel.gameObject);}
    }
}

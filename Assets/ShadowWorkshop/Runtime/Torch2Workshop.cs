using UnityEngine;

namespace CloudGarden.ShadowPuzzle
{
    // Iteration 2: the lamps are fixed; all freedom is in the pile, which sits in an orb the player rolls freely
    // and carries by its hub. A level is solved when every lamp it uses shows its picture at the same time.
    public sealed class Torch2Workshop : MonoBehaviour
    {
        public const float SettleSeconds=.5f;
        public static readonly Vector3 MinPosition=new Vector3(-.30f,.30f,-.25f),MaxPosition=new Vector3(.30f,.70f,.35f);
        public Camera viewCamera;
        public ShadowPlayerContext xr;
        public Material surfaceMaterial,screenMaterial,glowMaterial;
        public Torch2Level level;
        public Torch2World world;
        public Torch2Input input;
        // Set before the first enable: no debug labels, buttons or prompts; the host game (Shape of Memory) draws its own.
        public bool bare;
        // A bare host's second hint: the target picture cards stand by the lamps.
        public bool CardsShown;
        // Set before the first enable: the host restyles the room as soon as it is built, before the walls are first lit.
        [System.NonSerialized] public System.Action<Torch2Room> roomStyle;
        // Set before the first enable: the host shows the target pictures itself, so no picture cards stand by the lamps.
        [System.NonSerialized] public bool hostShowsPictures;
        public Torch2Projection Projection {get;private set;}
        public Quaternion Rotation {get;private set;}
        public Vector3 Position {get;private set;}
        public int Owner {get;private set;}=-1;
        public bool Busy=>Owner>=0;
        public bool Completed {get;private set;}
        public float StableTime {get;private set;}
        public string Message {get;private set;}
        public bool HintActive=>Time.unscaledTime<hintUntil;
        public bool TwoLamps=>level.lamps.Length>1;
        // The orb turning itself into the answer after a close release (TrySnap).
        public bool Snapping=>snapLength>0;
        float nextWall,nextScore,hintUntil,snapTime,snapLength;
        Quaternion snapFrom;
        void Awake()
        {
            Projection=new Torch2Projection(level);SetPose(Torch2Projection.Home,Projection.Start);
            world=gameObject.AddComponent<Torch2World>();world.game=this;world.Build();
            input=gameObject.AddComponent<Torch2Input>();input.game=this;Refresh();
        }
        void Update()
        {
            // The shadow meshes follow the pile every frame (Torch2World); the scoring raster runs at 10 Hz and the
            // wall light only changes while the reveal glows.
            if(Snapping)
            {
                snapTime+=Time.unscaledDeltaTime;float t=Mathf.Clamp01(snapTime/snapLength);
                Rotation=Quaternion.Slerp(snapFrom,Projection.Solution,1-(1-t)*(1-t)*(1-t));if(t>=1)snapLength=0;
            }
            if(world.Animating&&Time.unscaledTime>=nextWall){nextWall=Time.unscaledTime+1/30f;world.RefreshWalls();}
            if(Time.unscaledTime>=nextScore){nextScore=Time.unscaledTime+.1f;Refresh();}
        }
        void Refresh(){Projection.Render(Rotation,Position);Projection.Evaluate();Evaluate(.1f);world.Refresh();}
        public void Evaluate(float dt)
        {
            if(Completed)return;
            StableTime=Projection.Score.Ready&&!Busy&&!Snapping?StableTime+dt:0;
            if(StableTime>=SettleSeconds){Completed=true;StableTime=0;hintUntil=0;Message=level.complete;world.Reveal();Debug.Log($"TORCH2_COMPLETE: {level.name} {Quaternion.Angle(Rotation,Projection.Solution):0} degrees from the design pose");}
            else UpdateMessage();
        }
        void UpdateMessage()
        {
            if(HintActive){Message=level.hint;return;}
            var s=Projection.Score;bool hands=xr&&xr.Active;
            string how=hands?"Pinch the orb's rings and drag to roll it any way. Pinch the hub in the middle to carry it.":"Left-drag rolls the orb. Right-drag moves it, the wheel brings it nearer or further.";
            if(s.Ready)Message="There it is. Let go and hold still...";
            else if(s.warmth>.75f)Message=TwoLamps?"Very warm. Both walls are close - small turns now.":"Very warm. Small turns now.";
            else if(s.warmth>.45f)Message=TwoLamps?"Warmer. Watch both walls: the weaker one decides.":"Warmer. Keep turning, and try tipping it too.";
            else if(!s.visible)Message="Keep the pile inside the light so its shadow reaches the wall.\n"+how;
            else Message=level.riddle+"\n"+how;
        }
        public void SetPose(Vector3 position,Quaternion rotation)
        {
            Position=Vector3.Max(MinPosition,Vector3.Min(MaxPosition,position));Rotation=Quaternion.Normalize(rotation);
        }
        // Grabbing stops a snap where it is: the hand wins.
        public bool BeginHold(int owner){if(Completed||owner<0||(Owner>=0&&Owner!=owner))return false;Owner=owner;StableTime=0;snapLength=0;return true;}
        public void Hold(int owner,Vector3 position,Quaternion rotation){if(Owner==owner)SetPose(position,rotation);}
        public void EndHold(int owner){if(Owner!=owner)return;Owner=-1;TrySnap();}
        // Let go within snapAngle of the answer, passing or at snapWarmth, and the orb eases into the exact answer (only the
        // rotation; the score ignores where the pile sits). Never while held, and never from a lookalike far away.
        public bool TrySnap()
        {
            if(Completed||Busy)return false;
            Projection.Render(Rotation,Position);Projection.Evaluate();
            var s=Projection.Score;float angle=Quaternion.Angle(Rotation,Projection.Solution);
            if(angle<.5f||angle>level.snapAngle||!(s.Ready||s.warmth>=level.snapWarmth))return false;
            snapFrom=Rotation;snapTime=0;snapLength=Mathf.Lerp(.25f,.5f,angle/Mathf.Max(1,level.snapAngle));return true;
        }
        public void Hint(){if(Completed||bare)return;hintUntil=Time.unscaledTime+12;UpdateMessage();world.Refresh();}
        public void ResetChallenge()
        {
            if(Busy)return;Completed=false;StableTime=0;hintUntil=0;snapLength=0;SetPose(Torch2Projection.Home,Projection.Start);world.ResetReveal();Refresh();
        }
        public void Height(float amount){if(!Busy)transform.position+=Vector3.up*amount;}
    }
}

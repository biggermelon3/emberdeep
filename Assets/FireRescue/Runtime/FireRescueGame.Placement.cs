using UnityEngine;

namespace FireRescue
{
    // Workstream O (LoopSpec §5 Reach, §10 options): arena placement and the compact board. The Emberdeep flow
    // (Run.CompactBoard) squeezes the authored terrain (EmberdeepBoardFit), plants the rim grove clear of the compact
    // fields and places the board for a seated player; the legacy tool flow keeps FireMiniature.Place and the
    // authored layout. The play-area options re-place the board live from the last placement anchor.
    public sealed partial class FireRescueGame
    {
        Vector3 placedHead,placedForward;
        bool placedOnce,playAreaHooked;
        // The seat the board was placed from (recenter moves it, the play-area options keep it): the sky window's anchor.
        public Vector3? SeatAnchor=>placedOnce?placedHead:null;
        public EmberdeepBoardFit BoardFit {get;private set;}
        // The desktop seat looks at the middle of the compact 4-row field for the current play area (default -14 / +6:
        // ~40 deg down; flat 0 / 0 was ~34; tilted ~37), re-aimed live on EmberdeepPlayArea.Changed; the legacy board at
        // 25 deg. The desktop eye is FireRescueGame.ApplyWorldPose's (0, 1.65, 0) over a floor at 0.
        public const float DesktopPitchLegacy=25,DesktopEyeHeight=1.65f;
        public static float DesktopPitchCompact=>DesktopPitchFor(EmberdeepPlayArea.Height,EmberdeepPlayArea.Distance,EmberdeepPlayArea.TiltDegrees);
        public static float DesktopPitchFor(float height,float distance,float tilt)
        {
            var eye=new Vector3(0,DesktopEyeHeight,0);
            var pivot=FireMiniature.CompactPivot(eye,Vector3.forward,0,height,distance,tilt);
            var middle=pivot+FireMiniature.CompactRotation(Vector3.forward,tilt)*new Vector3(0,0,(EmberdeepBoardLayout.MaxRows-1)*.5f*EmberdeepBoardLayout.RowSpacing*FireMiniature.Scale);
            return Mathf.Atan2(eye.y-middle.y,middle.z)*Mathf.Rad2Deg;
        }

        void FitBoard(Transform terrain)
        {
            if(!Run.CompactBoard || !terrain)return;
            BoardFit=EmberdeepBoardFit.Apply(terrain,Arena);
        }
        // Without the art kit: the blockout island, plinth, fire break and rocks get the same z squeeze.
        void FitPrimitiveBoard()
        {
            if(!Run.CompactBoard)return;
            foreach(Transform part in Arena)
            {
                if(part.name!="Forest island" && part.name!="Diorama plinth" && part.name!="Fire break" && part.name!="Miniature rock")continue;
                var p=part.localPosition;var s=part.localScale;
                float near=EmberdeepBoardLayout.Z(p.z-s.z*.5f),far=EmberdeepBoardLayout.Z(p.z+s.z*.5f);
                if(part.name=="Miniature rock")far=near+s.z; // pebbles keep their shape
                part.localPosition=new Vector3(p.x,p.y,(near+far)*.5f);part.localScale=new Vector3(s.x,s.y,far-near);
            }
        }
        void BuildRimGrove(Transform border)
        {
            for(int side=-1;side<=1;side+=2)
            {
                if(Run.CompactBoard)
                {
                    foreach(var tree in EmberdeepBoardLayout.RimGrove)art.Create(tree.Id,border,EmberdeepBoardLayout.RimGrovePoint(tree.At,side),tree.Scale);
                    continue;
                }
                art.Create("Cedar",border,new Vector3(side*4.65f,0,7.8f),.62f);
                art.Create("Broadleaf",border,new Vector3(side*4.1f,0,9.65f),.8f);
                art.Create("Fern",border,new Vector3(side*4.55f,0,5.9f),1.2f);
                art.Create("Log",border,new Vector3(side*4.4f,0,2.1f),.75f);
            }
        }
        void PlaceArena(Transform view)
        {
            placedHead=view.position;placedForward=view.forward;placedOnce=true;
            if(!playAreaHooked){playAreaHooked=true;EmberdeepPlayArea.Changed+=OnPlayAreaChanged;}
            PlaceArenaFromAnchor();
        }
        void PlaceArenaFromAnchor()
        {
            if(Run.CompactBoard)
            {
                float h=EmberdeepPlayArea.Height,d=EmberdeepPlayArea.Distance;BoardTilt=EmberdeepPlayArea.TiltDegrees;
                FireMiniature.PlaceCompact(Arena,placedHead,placedForward,Player.FloorHeight,h,d,BoardTilt);
                UprightToWorld=FireMiniature.CompactUpright(placedHead,placedForward,Player.FloorHeight,h,d,BoardTilt);
                if(Arena.parent)UprightToWorld=Matrix4x4.TRS(UprightToWorld.GetPosition(),UprightToWorld.rotation,Arena.lossyScale);
            }
            else
            {
                FireMiniature.Place(Arena,placedHead,placedForward,Player.FloorHeight);
                BoardTilt=0;UprightToWorld=Arena.localToWorldMatrix;
            }
        }
        // The arena with the play-area tilt taken out (FireMiniature.CompactUpright): arena units, the same pivot (the
        // front row), flat heading. Identical to Arena.localToWorldMatrix when the board is flat. For things a real hand
        // strikes or holds that must stay upright in the world on a tilted board, e.g. the pump:
        // UprightPoint(local) / UprightRotation instead of Arena.TransformPoint(local) / Arena.rotation.
        public Matrix4x4 UprightToWorld {get;private set;}=Matrix4x4.identity;
        public Quaternion UprightRotation=>UprightToWorld.rotation;
        public Vector3 UprightPoint(Vector3 arenaLocal)=>UprightToWorld.MultiplyPoint3x4(arenaLocal);
        // The board's current pitch toward the player, degrees (0 flat, EmberdeepBoardLayout.TiltDegrees tilted).
        public float BoardTilt {get;private set;}
        // Live from the options: same anchor, new offsets and tilt; everything hung off the arena follows. The desktop
        // seat re-aims at the middle of the field (its anchor's heading is flat, so the pitch never moves the board).
        void OnPlayAreaChanged()
        {
            if(!this || !Arena || !Player || !placedOnce || !Run.CompactBoard)return;
            PlaceArenaFromAnchor();
            if(!Player.IsXR && Player.ViewCamera)
            {
                var e=Player.ViewCamera.transform.eulerAngles;
                Player.ViewCamera.transform.rotation=Quaternion.Euler(DesktopPitchCompact,e.y,0);
            }
            PositionRack();UpdateTargets();PositionWaterStations();Physics.SyncTransforms();
        }
        void UnhookPlacement(){if(playAreaHooked)EmberdeepPlayArea.Changed-=OnPlayAreaChanged;playAreaHooked=false;}
    }
}

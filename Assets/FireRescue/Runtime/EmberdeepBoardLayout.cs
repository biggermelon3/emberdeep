using UnityEngine;

namespace FireRescue
{
    // LoopSpec §5 "Reach: shrink the board" (workstream O), Emberdeep (goddess) flow only; the legacy tool flow keeps
    // the authored layout. Arena units throughout (x FireMiniature.Scale = metres) unless a name says metres.
    //
    // The field is at most 4 rows deep and up to 8 columns wide (FireRun.StartStage). The authored terrain was laid out
    // for a 6-row field with a 3.55-unit apron (front tip -> creek -> path -> front row), which would put the plinth's
    // front tip at the player's chest once the front row sits 30 cm from the eye. So the board is squeezed along z:
    //   - apron (authored -0.55 .. 3.0)  -> compact 1.1 .. 3.0, linear: creek, path, refuge and pasture keep their order;
    //   - field (3.0 .. 6.7)             -> unchanged, the trees never move;
    //   - back  (6.7 .. 12.45)           -> 6.7 .. 10.3, linear: room for 2 rows of sheep homes and the weathervane.
    // EmberdeepBoardFit applies Z() to the authored meshes once per scene load (flat parts per vertex, rocks, lanterns
    // and rim growth rigidly per piece). Anything authored in board z (refuge, pasture, creek) goes through Z() too.
    public static class EmberdeepBoardLayout
    {
        // Field grid (FireRun.StartStage).
        public const float FrontRowZ=3f,RowSpacing=1.15f,ColumnSpacing=1.18f,AccessLane=.24f;
        public const int MaxRows=4,MaxColumns=8;

        // Authored terrain (EDV2_Terrain) tips along z, and the compact board's.
        public const float AuthoredFront=-.55f,AuthoredBack=12.45f;
        public const float CompactFront=1.1f,FieldKeep=6.7f,CompactBack=10.3f;
        public static readonly float ApronSlope=(FrontRowZ-CompactFront)/(FrontRowZ-AuthoredFront);
        public static readonly float BackSlope=(CompactBack-FieldKeep)/(AuthoredBack-FieldKeep);

        // Authored board z -> compact board z. Monotonic, continuous; identity over the field.
        public static float Z(float authored)
        {
            if(authored<FrontRowZ)return FrontRowZ-(FrontRowZ-authored)*ApronSlope;
            if(authored>FieldKeep)return FieldKeep+(authored-FieldKeep)*BackSlope;
            return authored;
        }
        // dZ/dz at an authored z (normals of the squeezed flat parts).
        public static float Slope(float authored)=>authored<FrontRowZ?ApronSlope:authored>FieldKeep?BackSlope:1;
        // Compact -> authored, the inverse of Z().
        public static float Authored(float compact)
        {
            if(compact<FrontRowZ)return FrontRowZ-(FrontRowZ-compact)/ApronSlope;
            if(compact>FieldKeep)return FieldKeep+(compact-FieldKeep)/BackSlope;
            return compact;
        }
        // The creek lies in the apron band. Its shader draws in authored board space (centre z 2.1, half width .5), so
        // its frame maps compact arena space back to authored: multiply before arena.worldToLocalMatrix.
        public static Matrix4x4 CreekFrame
        {
            get
            {
                var m=Matrix4x4.identity;m.m22=1/ApronSlope;m.m23=FrontRowZ*(1-1/ApronSlope);return m;
            }
        }

        // Default seated placement (FireMiniature.PlaceCompact). Distances are horizontal from the eye, along the
        // player's flat heading; the drop is from the eye to the board top (arena y 0).
        // Headset test 2026-11-22: .30 m / .45 m was far too close and steep (front row 56 deg down, 46-52 deg to the
        // middle of the field, the front-lip readouts at 61 deg). Now the front row is 40 cm out and the board 9 cm
        // higher: ~34 deg down to the middle of a 4-row field (36 / 39 deg for 3 / 2 rows), the back row at ~.68 m and the
        // back-row piece squares at ~.72 m (a seated lean). The options (EmberdeepPlayArea) still reach the old spot.
        // These are the BASE of the play-area offsets, not the default: headset test 2 (2026-11-22) made -14 cm / +6 cm the
        // default offsets (EmberdeepPlayArea.DefaultHeight/DefaultDistance), so the default board top is 50 cm under the
        // eye and the front row 46 cm out. Kept as they were so saved offsets keep their meaning.
        public const float EyeToFrontRow=.40f;  // m; the back row (3 rows further) lands at ~.68 m (+ the distance offset)
        public const float EyeDrop=.36f;        // m; a little above desk height for a seated player (+ the height offset)
        public const float FloorClearance=.45f; // m; the lowest board-top point never goes lower than this above the floor

        // Tilted play area (EmberdeepPlayArea.Tilted, off by default): the whole arena pitches toward the player about the
        // front row's line on the board top (FrontRowZ, y 0), so the front row stays where the flat board puts it, at hand
        // height, and the back rises. 30 deg lifts the 4th row (3.45 units = 28 cm behind the front row) by 14 cm and
        // brings it 4 cm closer; the plinth's back edge (7.3 units) rises 29 cm and the apron's front tip (1.9 units)
        // dips 8 cm (FloorClearance holds that tip, not the pivot). From the default seat (-14 / +6) the back row comes up
        // from ~34 to ~27 deg under eye level and the middle of the field from ~40 to ~37 (the near rows stay at ~47), and
        // the board faces the eye: the sight line meets the middle of the field at ~67 deg instead of ~40, so the back
        // rows read without leaning over them or holding the head down.
        public const float TiltDegrees=30f;
        // How far the board top's front tip (CompactFront) sinks under the pivot at a given tilt, metres.
        public static float FrontTipDip(float degrees)=>(FrontRowZ-CompactFront)*FireMiniature.Scale*Mathf.Sin(Mathf.Max(0,degrees)*Mathf.Deg2Rad);

        // The hammer pump (real metres, cap .272 m over its origin) beside the board's rounded front corner on the
        // free-hand side (EmberdeepWaterTuning.PumpSide; it was the water-hand side until the 2026-11-22 headset fixes),
        // 40 cm out from the player's line. The island's front bulge is only +-3.4 wide here.
        // Headset test 3 (2026-11-22): at the -14 / +6 cm default the cap was ~42 cm under the eye and ~32 cm ahead (the
        // pump followed the board 1:1 from a spot set for the old 0 / 0 seat). Now both numbers are set FOR THE DEFAULT
        // SEAT (EyeDrop - DefaultHeight, EyeToFrontRow + DefaultDistance): the cap PumpCapBelowEye under the eye (~20 cm
        // over the board top: a downward fist from chest height, not a raised arm) and PumpAhead ahead of it (5 cm nearer).
        // The options then move it so:
        //  - distance: 1:1 with the board (PumpForward is arena z), so it keeps its front corner and the tray's keep-out
        //    (EmberdeepPieceView.TrayCentre) stays exact;
        //  - height: PumpHeightFollow of the change. 1:1 would put the cap 1 cm under the eye at the +15 cm top of the range;
        //    0 would sink it under the plinth there. Half keeps the cap 16-41 cm under the eye over the whole range
        //    (5-30 cm over the board top).
        // On a tilted board it stands in FireRescueGame.UprightToWorld (upright, at its flat spot).
        public const float PumpCapBelowEye=.30f,PumpAhead=.275f,PumpHeightFollow=.5f;
        public const float PumpSideX=5.0f;
        public const float PumpForward=FrontRowZ-(EyeToFrontRow+EmberdeepPlayArea.DefaultDistance-PumpAhead)/FireMiniature.Scale;
        // Pump origin height (arena y) for a play-area height offset (EmberdeepPlayArea.Height, metres).
        public static float PumpHeightFor(float heightOffset)
        {
            if(!float.IsFinite(heightOffset))heightOffset=EmberdeepPlayArea.DefaultHeight;
            float capBelowEye=PumpCapBelowEye-PumpHeightFollow*(heightOffset-EmberdeepPlayArea.DefaultHeight);
            return (EyeDrop-heightOffset-capBelowEye-EmberdeepHammerPumpView.CapTopHeight)/FireMiniature.Scale;
        }
        // At the default seat (const: EmberdeepWaterTuning.PumpHeight, tests).
        public const float PumpHeight=(EyeDrop-EmberdeepPlayArea.DefaultHeight-PumpCapBelowEye-EmberdeepHammerPumpView.CapTopHeight)/FireMiniature.Scale;

        // Decorative healthy trees on the island's rim, clear of every compact field, cabin and sheep home.
        public static readonly (string Id,Vector3 At,float Scale)[] RimGrove=
        {
            ("Cedar",new Vector3(4.75f,0,8.0f),.62f),
            ("Broadleaf",new Vector3(4.0f,0,9.3f),.6f), // .8 until headset test 4: from the farther tilted seat its crown reached into the Level select panel
            ("Fern",new Vector3(4.95f,0,8.9f),1.0f),
            ("Log",new Vector3(4.4f,0,2.1f),.75f), // authored z: lies across the creek's end, follows Z()
        };
        public static Vector3 RimGrovePoint(Vector3 at,int side)=>new Vector3(side*at.x,at.y,at.z<FrontRowZ?Z(at.z):at.z);

        // The board's wind streaks (EmberdeepEnvironmentVfx): centre of the compact content (creek to the last sheep
        // home), travel and lane spread along the wind.
        public const float WindCenterZ=5.4f,WindTravel=4.8f,WindLaneStep=1.2f;
    }
}

using UnityEngine;

namespace FireRescue
{
    // LoopSpec §10 options: fine-tune the compact board's seated placement (EmberdeepBoardLayout, §5 Reach).
    // Height and distance are metres added to the layout's base placement (EmberdeepBoardLayout.EyeDrop / EyeToFrontRow:
    // board top 36 cm under the eye, front row 40 cm ahead): + height raises the board toward the eye, + distance pushes
    // it away. The stored values are these absolute offsets, not offsets from the default, so changing the default never
    // moves a player's saved board and nothing is applied twice. Tilted (on by default since headset test 4) pitches the board toward the
    // player about its front row (EmberdeepBoardLayout.TiltDegrees). Set from the camp settings page and the pause panel;
    // the fireground re-places the board live on Changed.
    public static class EmberdeepPlayArea
    {
        public const string HeightKey="Emberdeep.PlayHeight",DistanceKey="Emberdeep.PlayDistance",TiltedKey="Emberdeep.PlayTilted";
        // Headset test 2, 2026-11-22: the owner settled on -14 / +6 (board top 50 cm under the eye, front row 46 cm out,
        // ~40 deg down to the middle of a 4-row field). Headset test 4 (same day): "tilted is good, want it farther", and
        // the piece tray moved to the front lip (EmberdeepPieceView.TrayCentre), so the board goes 6 cm further back and
        // Tilted is the default: front row 52 cm out, the tray row ~43 cm out and ~52 deg down, the back row ~76 cm out.
        // A player who never moved a slider gets this; a saved value wins.
        public const float DefaultHeight=-.14f,DefaultDistance=.12f;
        public const bool DefaultTilted=true;
        // Range (same test: even -14 was not low enough for an overview of the whole field):
        //   height -35: board top 71 cm under the eye. A seated eye is ~1.15-1.25 m over the floor (seat ~.45 m), so this
        //     puts the top at ~.45-.54 m, i.e. down at EmberdeepBoardLayout.FloorClearance (.45 m, seat height); any lower
        //     and the floor guard in FireMiniature.PlaceCompact clamps it anyway for most seated players (it engages under a
        //     1.16 m eye).
        //   height +15: top 21 cm under the eye. The front crowns (CanopyTop 2.8 units = 22 cm) then reach eye level and
        //     hide the back rows; no higher.
        //   distance -15: front row 25 cm ahead, the plinth's front tip (1.9 units = 15 cm before the front row) ~10 cm
        //     from the eye, at the chest.
        //   distance +30: front row 70 cm, back row (3 x 1.15 units = 28 cm further) ~98 cm. With -35 that keeps the
        //     default's ~40 deg look-down to the middle of the field, only smaller and farther: the overview. The cost is
        //     reach: from the shoulder (~25 cm under and ~13 cm behind the eye, PortableJointAim) a seated arm plus a lean
        //     reaches ~.72 m from the eye at the default height (the default back row is .74 m), so past ~+5 the back-row
        //     piece squares need a deeper lean, and at +30 the player stands or places pieces on the front rows only.
        //     Water is aimed, not reached, so it is not limited by this.
        public const float HeightMin=-.35f,HeightMax=.15f,DistanceMin=-.15f,DistanceMax=.30f;
        public const float Step=.01f;
        public static event System.Action Changed;
        static float height=float.NaN,distance=float.NaN;
        static int tilted=-1;

        public static float Height{get{if(float.IsNaN(height))height=Clean(PlayerPrefs.GetFloat(HeightKey,DefaultHeight),HeightMin,HeightMax,DefaultHeight);return height;}}
        public static float Distance{get{if(float.IsNaN(distance))distance=Clean(PlayerPrefs.GetFloat(DistanceKey,DefaultDistance),DistanceMin,DistanceMax,DefaultDistance);return distance;}}
        public static bool Tilted{get{if(tilted<0)tilted=PlayerPrefs.GetInt(TiltedKey,DefaultTilted?1:0)!=0?1:0;return tilted==1;}}
        // The board's pitch toward the player, degrees (0 when flat).
        public static float TiltDegrees=>Tilted?EmberdeepBoardLayout.TiltDegrees:0;
        // 0..1 along each slider, and where the default sits on it.
        public static float Height01=>Mathf.InverseLerp(HeightMin,HeightMax,Height);
        public static float Distance01=>Mathf.InverseLerp(DistanceMin,DistanceMax,Distance);
        public static float DefaultHeight01=>Mathf.InverseLerp(HeightMin,HeightMax,DefaultHeight);
        public static float DefaultDistance01=>Mathf.InverseLerp(DistanceMin,DistanceMax,DefaultDistance);

        public static void SetHeight(float metres){if(StoreHeight(metres))Changed?.Invoke();}
        public static void SetDistance(float metres){if(StoreDistance(metres))Changed?.Invoke();}
        // Slider drags (headset test 3, 2026-11-22): the value applies live (Changed re-places the board) but is written to
        // disk only on release, by Commit. PlayerPrefs holds it in memory meanwhile, so nothing is lost if the drag is cut.
        public static void SetHeight01(float t,bool save){if(StoreHeight(Mathf.Lerp(HeightMin,HeightMax,Mathf.Clamp01(t)),save))Changed?.Invoke();}
        public static void SetDistance01(float t,bool save){if(StoreDistance(Mathf.Lerp(DistanceMin,DistanceMax,Mathf.Clamp01(t)),save))Changed?.Invoke();}
        public static void Commit()=>PlayerPrefs.Save();
        // Whole-centimetre steps along each slider (EmberdeepSlider.Steps).
        public static int HeightSteps=>Mathf.RoundToInt((HeightMax-HeightMin)/Step);
        public static int DistanceSteps=>Mathf.RoundToInt((DistanceMax-DistanceMin)/Step);
        public static void SetTilted(bool value){if(StoreTilted(value))Changed?.Invoke();}
        public static void SetHeight01(float t)=>SetHeight(Mathf.Lerp(HeightMin,HeightMax,Mathf.Clamp01(t)));
        public static void SetDistance01(float t)=>SetDistance(Mathf.Lerp(DistanceMin,DistanceMax,Mathf.Clamp01(t)));
        public static void NudgeHeight(int steps)=>SetHeight(Height+steps*Step*2);
        public static void NudgeDistance(int steps)=>SetDistance(Distance+steps*Step*2);
        // Height, distance and tilt back to the defaults; one Changed at most.
        public static void ResetToDefault()
        {
            bool changed=StoreHeight(DefaultHeight);changed|=StoreDistance(DefaultDistance);changed|=StoreTilted(DefaultTilted);
            if(changed)Changed?.Invoke();
        }
        // Tests and domain reloads re-read the stored values.
        public static void Reload(){height=distance=float.NaN;tilted=-1;}

        // Whole centimetres, inside the range; NaN/infinity fall back to the default.
        static float Clean(float metres,float min,float max,float fallback)
        {
            if(float.IsNaN(metres) || float.IsInfinity(metres))return fallback;
            return Mathf.Clamp(Mathf.Round(metres/Step)*Step,min,max);
        }
        static bool StoreHeight(float metres,bool save=true)=>Store(HeightKey,Height,ref height,Clean(metres,HeightMin,HeightMax,DefaultHeight),save);
        static bool StoreDistance(float metres,bool save=true)=>Store(DistanceKey,Distance,ref distance,Clean(metres,DistanceMin,DistanceMax,DefaultDistance),save);
        static bool Store(string key,float before,ref float cache,float value,bool save=true)
        {
            cache=value;PlayerPrefs.SetFloat(key,value);if(save)PlayerPrefs.Save();
            return !Mathf.Approximately(before,value);
        }
        static bool StoreTilted(bool value)
        {
            bool before=Tilted;
            tilted=value?1:0;PlayerPrefs.SetInt(TiltedKey,tilted);PlayerPrefs.Save();
            return before!=value;
        }
    }
}

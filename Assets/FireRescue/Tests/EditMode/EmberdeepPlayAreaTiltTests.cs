using NUnit.Framework;
using UnityEngine;

namespace FireRescue.Tests
{
    // Saves and restores the play-area prefs around a test (EditMode shares the editor's PlayerPrefs).
    public sealed class EmberdeepPlayAreaPrefs
    {
        static readonly string[] FloatKeys={EmberdeepPlayArea.HeightKey,EmberdeepPlayArea.DistanceKey};
        readonly float?[] floats=new float?[FloatKeys.Length];int? tilted;
        public static EmberdeepPlayAreaPrefs Save()
        {
            var s=new EmberdeepPlayAreaPrefs();
            for(int i=0;i<FloatKeys.Length;i++)s.floats[i]=PlayerPrefs.HasKey(FloatKeys[i])?PlayerPrefs.GetFloat(FloatKeys[i]):null;
            s.tilted=PlayerPrefs.HasKey(EmberdeepPlayArea.TiltedKey)?PlayerPrefs.GetInt(EmberdeepPlayArea.TiltedKey):null;
            return s;
        }
        public void Restore()
        {
            for(int i=0;i<FloatKeys.Length;i++)if(floats[i].HasValue)PlayerPrefs.SetFloat(FloatKeys[i],floats[i].Value);else PlayerPrefs.DeleteKey(FloatKeys[i]);
            if(tilted.HasValue)PlayerPrefs.SetInt(EmberdeepPlayArea.TiltedKey,tilted.Value);else PlayerPrefs.DeleteKey(EmberdeepPlayArea.TiltedKey);
            PlayerPrefs.Save();EmberdeepPlayArea.Reload();
        }
        public static void Clear()
        {
            foreach(var key in FloatKeys)PlayerPrefs.DeleteKey(key);
            PlayerPrefs.DeleteKey(EmberdeepPlayArea.TiltedKey);EmberdeepPlayArea.Reload();
        }
    }

    // Headset test 2 (2026-11-22): -14 / +6 defaults, the wider range, the Flat / Tilted play area (EmberdeepPlayArea,
    // FireMiniature.PlaceCompact) and the tilt audit's water check.
    public sealed class EmberdeepPlayAreaTiltTests
    {
        static readonly Vector3 Head=new(.4f,1.2f,-.3f);
        static readonly Vector3 Forward=Quaternion.Euler(20,37,0)*Vector3.forward;
        static Vector3 Flat=>Vector3.ProjectOnPlane(Forward,Vector3.up).normalized;
        static float Ahead(Vector3 world)=>Vector3.Dot(world-Head,Flat);
        const float BackRowZ=EmberdeepBoardLayout.FrontRowZ+(EmberdeepBoardLayout.MaxRows-1)*EmberdeepBoardLayout.RowSpacing;

        [Test] public void DefaultsAreTheHeadsetTestSpotAndSavedValuesStayAbsolute()
        {
            var saved=EmberdeepPlayAreaPrefs.Save();
            try
            {
                EmberdeepPlayAreaPrefs.Clear();
                Assert.AreEqual(-.14f,EmberdeepPlayArea.Height,1e-5f);Assert.AreEqual(.12f,EmberdeepPlayArea.Distance,1e-5f,"headset test 4: 6 cm further back");
                Assert.IsTrue(EmberdeepPlayArea.Tilted,"tilted by default (headset test 4)");Assert.AreEqual(EmberdeepBoardLayout.TiltDegrees,EmberdeepPlayArea.TiltDegrees);
                // A player who saved the spot during the test keeps exactly it (not -28 / +12).
                PlayerPrefs.SetFloat(EmberdeepPlayArea.HeightKey,-.14f);PlayerPrefs.SetFloat(EmberdeepPlayArea.DistanceKey,.06f);EmberdeepPlayArea.Reload();
                Assert.AreEqual(-.14f,EmberdeepPlayArea.Height,1e-5f);Assert.AreEqual(.06f,EmberdeepPlayArea.Distance,1e-5f);
                // One who saved the old default keeps the old spot.
                PlayerPrefs.SetFloat(EmberdeepPlayArea.HeightKey,0);PlayerPrefs.SetFloat(EmberdeepPlayArea.DistanceKey,0);EmberdeepPlayArea.Reload();
                Assert.AreEqual(0,EmberdeepPlayArea.Height,1e-5f);Assert.AreEqual(0,EmberdeepPlayArea.Distance,1e-5f);
                // The wider range: -35..+15 and -15..+30, the defaults inside both.
                Assert.AreEqual(-.35f,EmberdeepPlayArea.HeightMin,1e-6f);Assert.AreEqual(.15f,EmberdeepPlayArea.HeightMax,1e-6f);
                Assert.AreEqual(-.15f,EmberdeepPlayArea.DistanceMin,1e-6f);Assert.AreEqual(.30f,EmberdeepPlayArea.DistanceMax,1e-6f);
                Assert.That(EmberdeepPlayArea.DefaultHeight01,Is.InRange(.4f,.45f));Assert.That(EmberdeepPlayArea.DefaultDistance01,Is.InRange(.58f,.62f));
                // Reset: all three back, one event.
                EmberdeepPlayArea.SetTilted(true);EmberdeepPlayArea.SetHeight(.1f);
                int changes=0;void Count()=>changes++;EmberdeepPlayArea.Changed+=Count;
                try
                {
                    EmberdeepPlayArea.ResetToDefault();Assert.AreEqual(1,changes,"one Changed for the whole reset");
                    Assert.AreEqual(EmberdeepPlayArea.DefaultHeight,EmberdeepPlayArea.Height,1e-5f);Assert.AreEqual(EmberdeepPlayArea.DefaultDistance,EmberdeepPlayArea.Distance,1e-5f);
                    Assert.IsTrue(EmberdeepPlayArea.Tilted);
                    EmberdeepPlayArea.ResetToDefault();Assert.AreEqual(1,changes,"no event when already default");
                }
                finally{EmberdeepPlayArea.Changed-=Count;}
            }
            finally{saved.Restore();}
        }

        [Test] public void TiltIsStoredAndAnnouncedOnlyOnAChange()
        {
            var saved=EmberdeepPlayAreaPrefs.Save();
            int changes=0;void Count()=>changes++;
            try
            {
                EmberdeepPlayAreaPrefs.Clear();EmberdeepPlayArea.Changed+=Count;
                // Tilted by default since headset test 4: the change is to flat.
                EmberdeepPlayArea.SetTilted(true);Assert.AreEqual(0,changes);
                EmberdeepPlayArea.SetTilted(false);Assert.AreEqual(1,changes);Assert.IsFalse(EmberdeepPlayArea.Tilted);
                Assert.AreEqual(0,EmberdeepPlayArea.TiltDegrees,1e-6f);
                EmberdeepPlayArea.SetTilted(false);Assert.AreEqual(1,changes,"no event without a change");
                EmberdeepPlayArea.Reload();Assert.IsFalse(EmberdeepPlayArea.Tilted,"stored");
                Assert.AreEqual(0,PlayerPrefs.GetInt(EmberdeepPlayArea.TiltedKey,1));
            }
            finally{EmberdeepPlayArea.Changed-=Count;saved.Restore();}
        }

        [Test] public void TiltedBoardKeepsTheFrontRowAndRaisesTheBack()
        {
            var flat=new GameObject("flat arena").transform;var tilted=new GameObject("tilted arena").transform;
            try
            {
                float h=EmberdeepPlayArea.DefaultHeight,d=EmberdeepPlayArea.DefaultDistance,tilt=EmberdeepBoardLayout.TiltDegrees;
                FireMiniature.PlaceCompact(flat,Head,Forward,0,h,d);
                FireMiniature.PlaceCompact(tilted,Head,Forward,0,h,d,tilt);
                // The front row stays where the flat board puts it (centre and the outer columns).
                foreach(float x in new[]{0f,-3.5f*EmberdeepBoardLayout.ColumnSpacing,3.5f*EmberdeepBoardLayout.ColumnSpacing})
                {
                    var front=new Vector3(x,0,EmberdeepBoardLayout.FrontRowZ);
                    Assert.Less(Vector3.Distance(flat.TransformPoint(front),tilted.TransformPoint(front)),1e-4f,"front row at x "+x);
                }
                // The back row rises by its depth x sin(tilt) and comes in by depth x (1 - cos(tilt)).
                float depth=(BackRowZ-EmberdeepBoardLayout.FrontRowZ)*FireMiniature.Scale;
                var backFlat=flat.TransformPoint(new Vector3(0,0,BackRowZ));var backTilted=tilted.TransformPoint(new Vector3(0,0,BackRowZ));
                Assert.AreEqual(depth*Mathf.Sin(tilt*Mathf.Deg2Rad),backTilted.y-backFlat.y,1e-4f,"back row rises");
                Assert.AreEqual(.138f,backTilted.y-backFlat.y,.002f,"~14 cm at 30 deg");
                Assert.AreEqual(depth*(1-Mathf.Cos(tilt*Mathf.Deg2Rad)),Ahead(backFlat)-Ahead(backTilted),1e-4f,"and comes a little closer");
                // Pitched toward the player about the board's x axis only: no yaw or roll, the heading kept.
                Assert.AreEqual(tilt,Vector3.Angle(tilted.up,Vector3.up),.01f);
                Assert.Less(Vector3.Dot(tilted.up,Flat),0,"the board faces the player");
                Assert.AreEqual(0,Vector3.Dot(tilted.right,Vector3.up),1e-5f,"no roll");
                Assert.Less(Vector3.Angle(Vector3.ProjectOnPlane(tilted.forward,Vector3.up),Flat),.01f,"same heading");
                // The front tip dips by FrontTipDip; looking at the back row is ~7 deg less steep.
                var tip=new Vector3(0,0,EmberdeepBoardLayout.CompactFront);
                Assert.AreEqual(EmberdeepBoardLayout.FrontTipDip(tilt),flat.TransformPoint(tip).y-tilted.TransformPoint(tip).y,1e-4f);
                float Down(Vector3 world)=>Mathf.Atan2(Head.y-world.y,Ahead(world))*Mathf.Rad2Deg;
                Assert.That(Down(backFlat)-Down(backTilted),Is.InRange(6f,8f));
                // The upright frame is the flat board (same pivot): the pump stands where the flat board puts it.
                var upright=FireMiniature.CompactUpright(Head,Forward,0,h,d,tilt);
                for(int i=0;i<16;i++)Assert.AreEqual(flat.localToWorldMatrix[i],upright[i],1e-4f,"upright frame element "+i);
                // Flat placement is unchanged by the new argument.
                var again=new GameObject("again").transform;
                try{FireMiniature.PlaceCompact(again,Head,Forward,0,h,d,0);Assert.Less(Vector3.Distance(again.position,flat.position),1e-6f);Assert.Less(Quaternion.Angle(again.rotation,flat.rotation),1e-3f);}
                finally{Object.DestroyImmediate(again.gameObject);}
            }
            finally{Object.DestroyImmediate(flat.gameObject);Object.DestroyImmediate(tilted.gameObject);}
        }

        [Test] public void FloorGuardHoldsTheLowestBoardPointAtTheNewMinimum()
        {
            var arena=new GameObject("arena").transform;
            try
            {
                float min=EmberdeepPlayArea.HeightMin;
                // A typical seated eye (1.25 m): -35 is free, the top 71 cm under the eye, 54 cm over the floor.
                FireMiniature.PlaceCompact(arena,new Vector3(0,1.25f,0),Vector3.forward,0,min);
                Assert.AreEqual(1.25f-EmberdeepBoardLayout.EyeDrop+min,arena.position.y,1e-4f);
                Assert.Greater(arena.position.y,EmberdeepBoardLayout.FloorClearance);
                // A low seated eye (1.10 m): the guard holds the top at 45 cm.
                FireMiniature.PlaceCompact(arena,new Vector3(0,1.10f,0),Vector3.forward,0,min);
                Assert.AreEqual(EmberdeepBoardLayout.FloorClearance,arena.position.y,1e-4f);
                // The guard engages under a 1.16 m eye at the minimum.
                Assert.AreEqual(1.16f,EmberdeepBoardLayout.FloorClearance+EmberdeepBoardLayout.EyeDrop-min,1e-4f);
                // Tilted: the front tip is the lowest board-top point and the guard holds it, not the pivot.
                float tilt=EmberdeepBoardLayout.TiltDegrees;
                FireMiniature.PlaceCompact(arena,new Vector3(0,1.10f,0),Vector3.forward,0,min,0,tilt);
                Assert.AreEqual(EmberdeepBoardLayout.FloorClearance,arena.TransformPoint(new Vector3(0,0,EmberdeepBoardLayout.CompactFront)).y,1e-4f);
                Assert.Greater(arena.TransformPoint(new Vector3(0,0,EmberdeepBoardLayout.FrontRowZ)).y,EmberdeepBoardLayout.FloorClearance);
                // Unguarded, tilted keeps the flat front row height.
                FireMiniature.PlaceCompact(arena,new Vector3(0,1.6f,0),Vector3.forward,0,min,0,tilt);
                Assert.AreEqual(1.6f-EmberdeepBoardLayout.EyeDrop+min,arena.TransformPoint(new Vector3(0,0,EmberdeepBoardLayout.FrontRowZ)).y,1e-4f);
            }
            finally{Object.DestroyImmediate(arena.gameObject);}
        }

        // Tilt audit: the palm shot (FireRescueGame.SolvePalmShot: WaterJet.AimVelocity + Predict) lands on the cell it aims
        // at on a tilted board. Gravity stays world; WaterJet.Segment finds the ground in arena space.
        [Test] public void WaterShotLandsOnTheAimedCellOfATiltedBoard()
        {
            var arena=new GameObject("arena").transform;
            try
            {
                foreach(float tilt in new[]{0f,EmberdeepBoardLayout.TiltDegrees})
                {
                    FireMiniature.PlaceCompact(arena,Head,Forward,0,EmberdeepPlayArea.DefaultHeight,EmberdeepPlayArea.DefaultDistance,tilt);
                    // From a palm in front of the chest, to the front, middle and back rows, centre and sides.
                    var palm=Head+Flat*.18f+Vector3.down*.28f+Vector3.Cross(Vector3.up,Flat)*.12f;
                    foreach(var cell in new[]{new Vector3(0,0,3),new Vector3(-3*1.18f,0,3+1.15f),new Vector3(2*1.18f,0,3+2*1.15f),new Vector3(1.18f,0,BackRowZ),new Vector3(-4*1.18f,0,BackRowZ)})
                    {
                        var target=arena.TransformPoint(cell);
                        var velocity=WaterJet.AimVelocity(palm,target,FireRescueGame.PalmWaterSpeed);
                        Assert.IsTrue(WaterJet.Predict(palm,velocity,1.5f,arena,new Vector3[0],out var hit),$"tilt {tilt}: shot at {cell} lands");
                        var local=arena.InverseTransformPoint(hit.Point);
                        Assert.AreEqual(0,local.y,1e-3f,"on the board top");
                        Assert.Less(Vector2.Distance(new Vector2(local.x,local.z),new Vector2(cell.x,cell.z)),.05f,$"tilt {tilt}: lands on {cell}, got {local}");
                        Assert.Less(Vector3.Angle(hit.Normal,arena.up),.01f,"splash normal is the board's");
                    }
                }
            }
            finally{Object.DestroyImmediate(arena.gameObject);}
        }

        // The desktop seat looks at the middle of the 4-row field for the current play area.
        [Test] public void DesktopPitchFollowsThePlayArea()
        {
            Assert.AreEqual(33.8f,FireRescueGame.DesktopPitchFor(0,0,0),.1f,"base placement (the old default)");
            Assert.AreEqual(39.9f,FireRescueGame.DesktopPitchFor(EmberdeepPlayArea.DefaultHeight,.06f,0),.1f,"headset test 2 default");
            Assert.AreEqual(37.2f,FireRescueGame.DesktopPitchFor(EmberdeepPlayArea.DefaultHeight,EmberdeepPlayArea.DefaultDistance,0),.1f,"headset test 4 default, flat");
            Assert.AreEqual(34.0f,FireRescueGame.DesktopPitchFor(EmberdeepPlayArea.DefaultHeight,EmberdeepPlayArea.DefaultDistance,EmberdeepBoardLayout.TiltDegrees),.1f,"headset test 4 default (tilted)");
            Assert.AreEqual(40.3f,FireRescueGame.DesktopPitchFor(EmberdeepPlayArea.HeightMin,EmberdeepPlayArea.DistanceMax,0),.1f,"low + far: the overview keeps ~40 deg");
        }
    }
}

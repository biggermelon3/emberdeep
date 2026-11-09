using System.Collections.Generic;
using UnityEngine;

namespace PhysicalAssembly
{
    // Scene-owned presentation. Gameplay binds these references without rebuilding them.
    public sealed class BuildingSceneLayout : MonoBehaviour
    {
        public bool Authored;
        [Tooltip("Main tabletop collider, used to report its actual height above the tracked floor.")]
        public Collider Surface;
        public Transform Menu;
        public GameObject BuildStand;
        public TextMesh ModeLabel, StatusLabel, CommandLabel, ScoreLabel;
        public DownhillCourse Downhill;
        public NarrowGapCourse NarrowGap;
        public BuildingPartSpawn[] Spawns;
        [Tooltip("Design Mode car pose. Move this marker in Edit Mode, then save the scene.")]
        public Transform DesignCarSpawn;

        public void HidePreviews()
        {
            foreach(var spawn in Spawns) if(spawn && spawn.Preview) spawn.Preview.SetActive(false);
        }

        public void CreateDefaultSpawns(DownhillSettings downhill, bool narrow)
        {
            var parent=new GameObject("Part spawn markers").transform;
            parent.SetParent(downhill?Downhill.WorkbenchFrame:transform,false);
            var spawns=new List<BuildingPartSpawn>();
            void Add(PartKind kind,Vector3 position,Quaternion rotation)
            {
                var go=new GameObject($"{spawns.Count:00} {kind} spawn"); go.transform.SetParent(parent,false);
                go.transform.SetLocalPositionAndRotation(position,rotation);
                var spawn=go.AddComponent<BuildingPartSpawn>(); spawn.Kind=kind; spawns.Add(spawn);
            }
            if(downhill)
            {
                var kinds=new[]{PartKind.DownhillLongBlock,PartKind.DownhillLargeWheel,PartKind.DownhillLargeWheel,
                    PartKind.DownhillSmallWheel,PartKind.DownhillSmallWheel,PartKind.DownhillShortBlock,PartKind.DownhillTriangle,PartKind.DownhillIrregular};
                for(int i=0;i<kinds.Length;i++) Add(kinds[i],downhill.KitPosition(i),DownhillParts.IsWheel(kinds[i])?Quaternion.Euler(0,90,0):Quaternion.identity);
            }
            else if(narrow)
            {
                for(int i=0;i<16;i++) Add(PartKind.Cuboid,new Vector3(.61f+(i%4)*.19f,.56f+(i/8)*.095f,-.37f+((i/4)%2)*.34f),Quaternion.Euler(0,90,0));
                for(int i=0;i<12;i++) Add(PartKind.SmallCube,new Vector3(-.65f-(i%3)*.12f,.56f,-.38f+(i/3)*.12f),Quaternion.identity);
                for(int i=0;i<4;i++) Add(PartKind.Beam,new Vector3(1.28f,.55f+i*.025f,0),Quaternion.identity);
                for(int i=0;i<5;i++) Add(PartKind.PhysicsSphere,new Vector3(-.85f+i*.065f,.56f,.29f),Quaternion.identity);
            }
            else
            {
                Add(PartKind.Chassis,new Vector3(0,.69f,.45f),Quaternion.identity);
                for(int i=0;i<8;i++) Add(PartKind.SmallCube,new Vector3(-.75f+(i%4)*.16f,.62f,-.15f+(i/4)*.18f),Quaternion.identity);
                for(int i=0;i<4;i++) Add(PartKind.Cuboid,new Vector3(.55f,.62f,-.3f+i*.18f),Quaternion.identity);
                for(int i=0;i<2;i++) Add(PartKind.Pyramid,new Vector3(-.6f+i*.2f,.62f,.4f),Quaternion.identity);
            }
            Spawns=spawns.ToArray();
        }
    }
}

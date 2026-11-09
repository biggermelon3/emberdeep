using UnityEngine;

namespace PhysicalAssembly
{
    // Decorative meshes only: never participate in grabbing or vehicle collisions.
    public static class CourseArchVisuals
    {
        public const string PadName="Start arch - build direction";
        public static Transform Place(Transform parent,bool start,Vector3 position,Quaternion rotation,float width,string name=null)
        {
            name=name??(start?"Start arch":"Goal arch");
            var arch=parent.Find(name);
            if(!arch) {
                var prefab=Resources.Load<GameObject>("CourseArches/"+(start?"StartArch":"GoalArch"));
                if(!prefab) throw new System.InvalidOperationException("Course arch prefab is missing.");
                arch=Object.Instantiate(prefab,parent).transform; arch.name=name;
            }
            arch.SetPositionAndRotation(position,rotation);
            arch.localScale=Vector3.one;
            var scale=arch.lossyScale;
            arch.localScale=new Vector3(width/scale.x,width/scale.y,width/scale.z);
            return arch;
        }
        public static Transform EnsurePad(DownhillPresentation owner)
        {
            var group=owner.EnsureGridPadGroup();
            if(!group) return null;
            var existing=group.Find(PadName); if(existing) return existing; // Keep editor placement.
            var pad=owner.FindGridElement("Centered grid pad");
            var bounds=pad.GetComponent<MeshFilter>().sharedMesh.bounds;
            float radius=pad.TransformVector(Vector3.forward*bounds.extents.z).magnitude;
            float width=pad.TransformVector(Vector3.right*bounds.size.x).magnitude*.9f;
            var top=pad.TransformPoint(new Vector3(bounds.center.x,bounds.max.y,bounds.center.z));
            return Place(group,true,top+group.forward*(radius+width*.12f),group.rotation,width,PadName);
        }
    }
}

using UnityEngine;

namespace PhysicalAssembly
{
    public sealed class DownhillRotationWheel : MonoBehaviour
    {
        [System.NonSerialized] public DownhillController Controller;
        public const float Radius=.17f;
        Mesh generatedMesh;

        public bool IsNearRim(Vector3 point)
        {
            var local=transform.InverseTransformPoint(point);
            float radial=new Vector2(local.x,local.z).magnitude-Radius;
            return radial*radial+local.y*local.y<.035f*.035f;
        }

        public static DownhillRotationWheel Create(Transform parent,Vector3 position,Material material)
        {
            var root=new GameObject("Build rotation wheel"); root.transform.SetParent(parent,false); root.transform.localPosition=position;
            var wheel=root.AddComponent<DownhillRotationWheel>();
            var rim=new GameObject("Steering rim"); rim.transform.SetParent(root.transform,false);
            rim.transform.localRotation=Quaternion.Euler(0,0,90);
            wheel.generatedMesh=DownhillParts.Ring(.18f,.16f,.02f);
            rim.AddComponent<MeshFilter>().sharedMesh=wheel.generatedMesh;
            rim.AddComponent<MeshRenderer>().sharedMaterial=material;
            for(int i=0;i<3;i++)
            {
                var spoke=GameObject.CreatePrimitive(PrimitiveType.Cube); spoke.name="Steering spoke";
                DestroyImmediate(spoke.GetComponent<Collider>());
                spoke.transform.SetParent(root.transform,false);
                var rotation=Quaternion.Euler(0,i*120,0);
                spoke.transform.localPosition=rotation*Vector3.forward*.08f; spoke.transform.localRotation=rotation;
                spoke.transform.localScale=new Vector3(.018f,.015f,.16f);
                spoke.GetComponent<Renderer>().sharedMaterial=material;
            }
            for(int i=0;i<24;i++)
            {
                var segment=new GameObject("Rim grip"); segment.transform.SetParent(root.transform,false);
                var rotation=Quaternion.Euler(0,i*15,0);
                segment.transform.localPosition=rotation*Vector3.forward*Radius; segment.transform.localRotation=rotation;
                var collider=segment.AddComponent<BoxCollider>(); collider.size=new Vector3(.045f,.02f,.02f); collider.isTrigger=true;
            }
            return wheel;
        }
        public void ReleaseGeneratedMeshOwnership() => generatedMesh=null;
        void OnDestroy()
        {
            if(generatedMesh) { if(Application.isPlaying) Destroy(generatedMesh); else DestroyImmediate(generatedMesh); }
        }
    }
}

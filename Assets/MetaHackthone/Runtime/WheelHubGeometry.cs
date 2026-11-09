using UnityEngine;

namespace PhysicalAssembly
{
    public static class WheelHubGeometry
    {
        // Keep the established three-body suspension and saved body/socket poses.
        // Replace protruding axle art with a recessed bearing and a thin mounting arm.
        public static void Apply(AssemblyPart part)
        {
            if(part.Kind!=PartKind.Wheel) return;
            var hub=part.FindBody("hub"); var mount=part.FindBody("mount");
            if(!hub || !mount) return;
            PreserveMassProperties(hub,HustlePartProfile.HubWidth);
            PreserveMassProperties(mount,HustlePartProfile.MountWidth);
            foreach(var body in new[]{hub,mount}) foreach(var c in body.GetComponentsInChildren<Collider>()) if(!c.enabled) Object.DestroyImmediate(c);
            if(hub.transform.Find("Recessed bearing")) return;
            foreach(var body in new[]{hub,mount})
            {
                foreach(var r in body.GetComponentsInChildren<Renderer>()) if(!r.GetComponentInParent<AssemblySocket>()) r.enabled=false;
                foreach(var c in body.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            }
            var bearing=new GameObject("Recessed bearing"); bearing.transform.SetParent(hub.transform,false);
            var mesh=DownhillParts.Cylinder(.029f,HustlePartProfile.WheelWidth*.82f);
            bearing.AddComponent<MeshFilter>().sharedMesh=mesh; bearing.AddComponent<MeshRenderer>().sharedMaterial=PartFactory.Material(new Color(.82f,.51f,.18f));
            var collider=bearing.AddComponent<MeshCollider>(); collider.sharedMesh=mesh; collider.convex=true;
            var arm=GameObject.CreatePrimitive(PrimitiveType.Cube); arm.name="Suspension mounting arm"; arm.transform.SetParent(mount.transform,false);
            arm.transform.localPosition=new Vector3(.015f,0,0); arm.transform.localScale=new Vector3(HustlePartProfile.MountWidth+.03f,.012f,.018f);
            arm.GetComponent<Renderer>().sharedMaterial=PartFactory.Material(new Color(.18f,.20f,.22f));
        }
        static void PreserveMassProperties(AssemblyBody body,float width)
        {
            // The recessed art must not retune the existing driven suspension.
            var rb=body.GetComponent<Rigidbody>(); rb.centerOfMass=Vector3.zero;
            rb.inertiaTensorRotation=Quaternion.identity;
            rb.inertiaTensor=new Vector3(.04f*.04f*2,width*width+.04f*.04f,width*width+.04f*.04f)*(rb.mass/12);
        }
    }
}

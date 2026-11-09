using System.Collections.Generic;
using UnityEngine;

namespace PhysicalAssembly
{
    public static class BuildingGeometry
    {
        public static float Unit => HustlePartProfile.ChassisSize.z / 6f;
        public static float Pitch => Unit / 2f;
        public const float SnapAngle=45f;
        public static Vector3 SandboxCoreSize => new Vector3(Unit * 2, Unit, Unit * 2);
        public static Vector3 CubeSize => Vector3.one * Unit;
        public static Vector3 CuboidSize => new Vector3(Unit * 2, Unit, Unit);
        public static Vector3 LegacyCuboidSize => new Vector3(Unit * 3, Unit, Unit);

        // Shared default for every building level: one point per unit face section.
        public static void BoxGrid(AssemblyBody body,Vector3 size) => BoxUnitCenters(body,size);
        // Explicit compatibility paths only, for previously saved constructions.
        public static void BoxGrid(AssemblyBody body, Vector3 size,bool legacyGrid)
            => BoxGrid(body,size,legacyGrid,false);
        public static void BoxGrid(AssemblyBody body,Vector3 size,int gridVersion)
            => BoxGrid(body,size,gridVersion==0,gridVersion>=3);
        public static void BoxUnitCenters(AssemblyBody body,Vector3 size,bool includeVerticalFaces=true)
            => BoxGrid(body,size,false,true,includeVerticalFaces);
        static void BoxGrid(AssemblyBody body,Vector3 size,bool legacyGrid,bool centersOnly,bool includeVerticalFaces=true)
        {
            Face(body,"x+",Vector3.right*size.x/2,Vector3.right,Vector3.forward,Vector3.up,size.z,size.y,legacyGrid,centersOnly);
            Face(body,"x-",Vector3.left*size.x/2,Vector3.left,Vector3.back,Vector3.up,size.z,size.y,legacyGrid,centersOnly);
            if(includeVerticalFaces) Face(body,"y+",Vector3.up*size.y/2,Vector3.up,Vector3.right,Vector3.forward,size.x,size.z,legacyGrid,centersOnly);
            if(includeVerticalFaces) Face(body,"y-",Vector3.down*size.y/2,Vector3.down,Vector3.right,Vector3.back,size.x,size.z,legacyGrid,centersOnly);
            Face(body,"z+",Vector3.forward*size.z/2,Vector3.forward,Vector3.left,Vector3.up,size.x,size.y,legacyGrid,centersOnly);
            Face(body,"z-",Vector3.back*size.z/2,Vector3.back,Vector3.right,Vector3.up,size.x,size.y,legacyGrid,centersOnly);
        }
        static void Face(AssemblyBody body,string id,Vector3 center,Vector3 normal,Vector3 u,Vector3 v,float width,float height,bool legacyGrid,bool centersOnly=false)
        {
            if(centersOnly)
            {
                // Round to whole building units, keeping partial-size faces centered
                // rather than adding an extra row of half-cell or corner sockets.
                int columns=Mathf.Max(1,Mathf.RoundToInt(width/Unit));
                int rows=Mathf.Max(1,Mathf.RoundToInt(height/Unit));
                for(int x=0;x<columns;x++) for(int y=0;y<rows;y++)
                    PartFactory.Socket(body,$"grid_center_{id}_{x*2}_{y*2}",center+u*((x-(columns-1)*.5f)*Unit)+v*((y-(rows-1)*.5f)*Unit),normal,"grid",v);
                return;
            }
            int nx=Mathf.Max(1,Mathf.FloorToInt((width-.006f)/Pitch)+1);
            int ny=Mathf.Max(1,Mathf.FloorToInt((height-.006f)/Pitch)+1);
            if(legacyGrid)
            {
                for(int x=0;x<nx;x++) for(int y=0;y<ny;y++)
                    PartFactory.Socket(body,$"grid_{id}_{x}_{y}",center+u*((x-(nx-1)*.5f)*Pitch)+v*((y-(ny-1)*.5f)*Pitch),normal,"grid",v);
                return;
            }
            // Replace each four-point cell with its center and one corner. Center
            // points let cubes meet flush even after a quarter turn; long faces
            // also keep centers at whole-cube intervals for building container walls.
            for(int x=0;x<nx;x+=2) for(int y=0;y<ny;y+=2)
            {
                int dx=Mathf.Min(1,nx-1-x),dy=Mathf.Min(1,ny-1-y);
                var midpoint=center+u*((x+dx*.5f-(nx-1)*.5f)*Pitch)+v*((y+dy*.5f-(ny-1)*.5f)*Pitch);
                PartFactory.Socket(body,$"grid_center_{id}_{x}_{y}",midpoint,normal,"grid",v);
                if(centersOnly || dx==0 || dy==0) continue;
                int cornerX=x+(Vector3.Dot(u,Vector3.one)<0?dx:0),cornerY=y+(Vector3.Dot(v,Vector3.one)<0?dy:0);
                PartFactory.Socket(body,$"grid_{id}_{cornerX}_{cornerY}",center+u*((cornerX-(nx-1)*.5f)*Pitch)+v*((cornerY-(ny-1)*.5f)*Pitch),normal,"grid",v);
            }
        }
        public static Mesh Pyramid(AssemblyBody body,int gridVersion=PartFactory.LatestGridVersion)
        {
            bool legacyGrid=gridVersion==0;
            float h=Unit/2;
            var corners=new[]{new Vector3(-h,-h,-h),new Vector3(h,-h,-h),new Vector3(h,-h,h),new Vector3(-h,-h,h)};
            var apex=new Vector3(0,h,0); var vertices=new List<Vector3>(); var triangles=new List<int>();
            void Triangle(Vector3 a,Vector3 b,Vector3 c) { int i=vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); triangles.Add(i); triangles.Add(i+1); triangles.Add(i+2); }
            Triangle(corners[0],corners[1],corners[2]); Triangle(corners[0],corners[2],corners[3]);
            Face(body,"base",Vector3.down*h,Vector3.down,Vector3.right,Vector3.back,Unit,Unit,legacyGrid,gridVersion>=3);
            for(int face=0;face<4;face++)
            {
                var a=corners[face]; var b=corners[(face+1)%4]; Triangle(b,a,apex);
                var normal=Vector3.Cross(a-b,apex-b).normalized;
                var center=(a+b+apex)/3; var u=(b-a).normalized; var v=Vector3.Cross(normal,u).normalized;
                // A centered planar lattice, clipped to the triangular face.
                for(int x=-2;x<=2;x++) for(int y=-2;y<=2;y++)
                {
                    var p=center+Pitch*(u*x+v*y);
                    var ab=b-a; var ac=apex-a; var ap=p-a;
                    float d00=Vector3.Dot(ab,ab),d01=Vector3.Dot(ab,ac),d11=Vector3.Dot(ac,ac);
                    float d20=Vector3.Dot(ap,ab),d21=Vector3.Dot(ap,ac),den=d00*d11-d01*d01;
                    float s=(d11*d20-d01*d21)/den,t=(d00*d21-d01*d20)/den;
                    if(s>.03f && t>.03f && s+t<.97f && (legacyGrid || (x+y)%2==0))
                        PartFactory.Socket(body,$"grid_slope{face}_{x}_{y}",p,normal,"grid",v);
                }
            }
            var mesh=new Mesh { name="ContainerPyramid" }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        public static void Markers(AssemblyBody body)
        {
            var vertices=new List<Vector3>(); var triangles=new List<int>();
            foreach(var socket in body.GetComponentsInChildren<AssemblySocket>())
            {
                if(socket.Family!="grid") continue;
                var center=socket.transform.localPosition+socket.transform.localRotation*Vector3.forward*.0005f;
                var right=socket.transform.localRotation*Vector3.right*.0025f;
                var up=socket.transform.localRotation*Vector3.up*.0025f;
                int i=vertices.Count;
                vertices.Add(center-right-up); vertices.Add(center-right+up); vertices.Add(center+right+up); vertices.Add(center+right-up);
                triangles.Add(i); triangles.Add(i+2); triangles.Add(i+1); triangles.Add(i); triangles.Add(i+3); triangles.Add(i+2);
            }
            if(vertices.Count==0) return;
            var mesh=new Mesh { name="GridMarkers" }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go=new GameObject("Grid points"); go.transform.SetParent(body.transform,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh; go.AddComponent<MeshRenderer>().sharedMaterial=PartFactory.Material(new Color(1,.9f,.3f));
        }
        public static float AttachmentStep(AssemblySocket source,AssemblySocket target)
            => source.Body.Part.World && source.Body.Part.World.QuarterTurnBlocks &&
                (Rectangular(source.Body.Part.Kind) || Rectangular(target.Body.Part.Kind))?90f:SnapAngle;
        static bool Rectangular(PartKind kind) => kind is PartKind.Chassis or PartKind.SmallCube or PartKind.Cuboid or PartKind.Block or PartKind.Beam or PartKind.DownhillLongBlock or PartKind.DownhillShortBlock or PartKind.DownhillCore or PartKind.DownhillCube;
        public static Quaternion SnapDelta(AssemblySocket source,AssemblySocket target) => SnapDelta(source.WorldRotation,target.WorldRotation,AttachmentStep(source,target));
        public static Quaternion SnapDelta(Quaternion source,Quaternion target,float step=SnapAngle)
        {
            var normal=target*Vector3.forward; var up=target*Vector3.up;
            var facing=Quaternion.LookRotation(-normal,up);
            var best=SnapGeometry.AroundAxis(source,facing,-normal,step);
            return best*Quaternion.Inverse(source);
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CloudGarden.ShadowPuzzle
{
    public struct Torch2Score
    {
        public float cover,spill,iou,warmth,tilt;
        public bool visible,Ready;
    }

    // Two fixed point lamps light the two walls of a room corner; the pile sits in an orb the player rolls freely.
    // Same rules as TorchProjection per wall (CPU raster, centroid/RMS normalisation, eased compare), plus the
    // picture may sit up to 10 degrees tilted on its wall. A pose passes only when every lamp the level uses shows its
    // picture. Mirrors Tools/ShadowArt/build_torch2.py.
    public sealed class Torch2Projection
    {
        public const int Width=128,Height=120,Grid=64;
        public const float Pixel=.0088f,HalfAngle=20,RmsCells=12,LampDistance=.80f,LampRise=-.12f;
        // The answer picture is compared at each of these tilts; a passing tilt wins, then the best IoU.
        public static readonly float[] Tilts={-10,-5,0,5,10};
        public static readonly Vector3 Corner=new Vector3(0,0,.80f),Home=new Vector3(0,.34f,.10f);
        // Index 0 is the left wall (lit from the front right), 1 the right wall (lit from the front left).
        public static readonly Vector3[] WallOrigin=new Vector3[2],WallRight=new Vector3[2],WallNormal=new Vector3[2],Lamps=new Vector3[2];
        static Torch2Projection()
        {
            float s=1/Mathf.Sqrt(2),length=Width*Pixel;
            WallRight[0]=new Vector3(s,0,s);WallNormal[0]=new Vector3(s,0,-s);WallOrigin[0]=Corner-WallRight[0]*length;
            WallRight[1]=new Vector3(s,0,-s);WallNormal[1]=new Vector3(-s,0,-s);WallOrigin[1]=Corner;
            for(int k=0;k<2;k++)Lamps[k]=Home+WallNormal[k]*LampDistance+Vector3.up*LampRise;
        }
        // Level start and solution poses are stored as yaw then pitch.
        public static Quaternion Pose(float yaw,float pitch)=>Quaternion.AngleAxis(pitch,Vector3.right)*Quaternion.AngleAxis(yaw,Vector3.up);
        public static Vector3 WallPoint(int wall,int x,int y)=>WallOrigin[wall]+WallRight[wall]*((x+.5f)*Pixel)+Vector3.up*((y+.5f)*Pixel);

        public readonly Torch2Level level;
        public readonly int[] walls;
        public readonly bool[][] shadow,visible,target,current;
        public readonly float[][] brightness;
        public readonly Torch2Score[] scores;
        public Torch2Score Score {get;private set;}
        public float Baseline {get;private set;}
        public Quaternion Solution=>level.solution;
        public Quaternion Start=>level.start;
        readonly bool[][][] targets,halos;
        readonly bool[] near=new bool[Grid*Grid];
        readonly int[][] targetArea;
        readonly Vector3[][] local;
        readonly int[][] triangles;
        readonly Vector3[] world;
        readonly Vector2[] projected;
        readonly bool[] valid;
        readonly Vector2Int[] disc;
        bool drawn;
        Quaternion drawnRotation;
        Vector3 drawnPosition;

        // calibrate=false skips the warmth baseline (the gym's live preview rebuilds this on every edit).
        public Torch2Projection(Torch2Level level,bool calibrate=true)
        {
            this.level=level;walls=level.lamps;int n=walls.Length,most=0;
            var casting=Array.FindAll(level.pieces,p=>!p.visualOnly);int count=casting.Length;
            shadow=new bool[n][];visible=new bool[n][];target=new bool[n][];current=new bool[n][];brightness=new float[n][];scores=new Torch2Score[n];
            targets=new bool[n][][];halos=new bool[n][][];targetArea=new int[n][];
            for(int j=0;j<n;j++)
            {
                shadow[j]=new bool[Width*Height];visible[j]=new bool[Width*Height];brightness[j]=new float[Width*Height];current[j]=new bool[Grid*Grid];
                targets[j]=new bool[Tilts.Length][];halos[j]=new bool[Tilts.Length][];targetArea[j]=new int[Tilts.Length];
                for(int t=0;t<Tilts.Length;t++){targets[j][t]=new bool[Grid*Grid];halos[j][t]=new bool[Grid*Grid];}
                target[j]=targets[j][Array.IndexOf(Tilts,0f)];
            }
            local=new Vector3[count][];triangles=new int[count][];
            for(int i=0;i<count;i++)
            {
                var piece=casting[i];var mesh=piece.ShadowMesh;
                if(!mesh||!mesh.isReadable)throw new InvalidOperationException($"{level.name}: piece {piece.label} needs a readable shadow mesh");
                var matrix=Matrix4x4.TRS(piece.position,Quaternion.Euler(piece.euler),piece.Scale);var v=mesh.vertices;
                for(int k=0;k<v.Length;k++)v[k]=matrix.MultiplyPoint3x4(v[k]);
                local[i]=v;triangles[i]=mesh.triangles;most=Mathf.Max(most,v.Length);
            }
            world=new Vector3[most];projected=new Vector2[most];valid=new bool[most];
            var offsets=new List<Vector2Int>();int r=Mathf.Max(0,level.tolerance);
            for(int dy=-r;dy<=r;dy++)for(int dx=-r;dx<=r;dx++)if(dx*dx+dy*dy<=r*r+r)offsets.Add(new Vector2Int(dx,dy));
            disc=offsets.ToArray();
            // Lamps never move, so each wall's spot is computed once.
            float edge=Mathf.Cos(HalfAngle*Mathf.Deg2Rad),inner=Mathf.Cos(HalfAngle*.72f*Mathf.Deg2Rad);
            for(int j=0;j<n;j++)
            {
                var lamp=Lamps[walls[j]];var aim=(Home-lamp).normalized;
                for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)
                {
                    float c=Vector3.Dot((WallPoint(walls[j],x,y)-lamp).normalized,aim);
                    brightness[j][y*Width+x]=c<=edge?0:Mathf.SmoothStep(0,1,(c-edge)/(inner-edge));
                }
            }
            Render(Solution,Home);
            for(int j=0;j<n;j++)for(int t=0;t<Tilts.Length;t++){Normalise(visible[j],targets[j][t],Tilts[t]);foreach(bool on in targets[j][t])if(on)targetArea[j][t]++;Dilate(targets[j][t],halos[j][t]);}
            if(!calibrate)return;
            // Typical weakest-wall match from wrong poses, so the warmth meter starts near zero.
            var samples=new List<float>();
            for(int k=1;k<12;k++){Render(Pose(k*30,(k%3-1)*35)*Solution,Home);Evaluate();samples.Add(Score.iou);}
            samples.Sort();Baseline=Mathf.Min(samples[samples.Count/2],level.iouMin-.15f);Score=default;
        }

        // Pile pose in workbench space. Returns false when nothing moved.
        public bool Render(Quaternion pose,Vector3 position)
        {
            if(drawn&&pose.Equals(drawnRotation)&&position==drawnPosition)return false;
            drawn=true;drawnRotation=pose;drawnPosition=position;
            for(int j=0;j<walls.Length;j++)
            {
                int w=walls[j];var mask=shadow[j];Array.Clear(mask,0,mask.Length);
                for(int i=0;i<local.Length;i++)
                {
                    Project(i,pose,position,w,Pixel);var t=triangles[i];
                    for(int e=0;e<t.Length;e+=3)if(Facing(t[e],t[e+1],t[e+2],Lamps[w]))Raster(mask,projected[t[e]],projected[t[e+1]],projected[t[e+2]]);
                }
                var seen=visible[j];var lit=brightness[j];for(int p=0;p<seen.Length;p++)seen[p]=mask[p]&&lit[p]>0;
            }
            return true;
        }
        // Display only: the same lamp-facing triangles on wall slot j, clipped to the wall, three points each, in wall
        // metres (x along WallRight, y up). Exact at any resolution; the score still uses the raster.
        public void Outline(int slot,Quaternion pose,Vector3 position,List<Vector2> output)
        {
            output.Clear();int w=walls[slot];
            for(int i=0;i<local.Length;i++)
            {
                Project(i,pose,position,w,1);var t=triangles[i];
                for(int e=0;e<t.Length;e+=3)if(Facing(t[e],t[e+1],t[e+2],Lamps[w]))Clip(output,projected[t[e]],projected[t[e+1]],projected[t[e+2]]);
            }
        }
        void Project(int piece,Quaternion pose,Vector3 position,int w,float unit)
        {
            var lamp=Lamps[w];var toward=-WallNormal[w];var origin=WallOrigin[w];var right=WallRight[w];
            float reach=Vector3.Dot(origin-lamp,toward);var v=local[piece];
            for(int k=0;k<v.Length;k++)
            {
                var p=pose*v[k]+position;world[k]=p;float d=Vector3.Dot(p-lamp,toward);valid[k]=d>.001f;
                if(!valid[k])continue;var q=lamp+(p-lamp)*(reach/d)-origin;
                projected[k]=new Vector2(Vector3.Dot(q,right)/unit,q.y/unit);
            }
        }
        // Closed meshes cast the same silhouette from their lamp-facing half; bothFaces levels (single-sided or mirrored
        // store meshes) use every triangle.
        bool Facing(int a,int b,int c,Vector3 lamp)=>valid[a]&&valid[b]&&valid[c]&&(level.bothFaces||Vector3.Dot(Vector3.Cross(world[b]-world[a],world[c]-world[a]),lamp-world[a])>0);
        readonly Vector2[] polygon=new Vector2[8],clipped=new Vector2[8];
        void Clip(List<Vector2> output,Vector2 a,Vector2 b,Vector2 c)
        {
            float w=Width*Pixel,h=Height*Pixel;
            if(Mathf.Abs((b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x))<1e-10f)return;
            if(a.x>=0&&b.x>=0&&c.x>=0&&a.x<=w&&b.x<=w&&c.x<=w&&a.y>=0&&b.y>=0&&c.y>=0&&a.y<=h&&b.y<=h&&c.y<=h){output.Add(a);output.Add(b);output.Add(c);return;}
            polygon[0]=a;polygon[1]=b;polygon[2]=c;
            int n=Edge(polygon,3,clipped,0,0,1);n=Edge(clipped,n,polygon,0,w,-1);n=Edge(polygon,n,clipped,1,0,1);n=Edge(clipped,n,polygon,1,h,-1);
            for(int k=1;k+1<n;k++){output.Add(polygon[0]);output.Add(polygon[k]);output.Add(polygon[k+1]);}
        }
        // One Sutherland-Hodgman step: keeps the side where sign*(p[axis]-bound) >= 0.
        static int Edge(Vector2[] source,int n,Vector2[] output,int axis,float bound,float sign)
        {
            int m=0;
            for(int k=0;k<n;k++)
            {
                var p=source[k];var q=source[(k+1)%n];float dp=sign*(p[axis]-bound),dq=sign*(q[axis]-bound);
                if(dp>=0)output[m++]=p;
                if((dp>=0)!=(dq>=0))output[m++]=Vector2.Lerp(p,q,dp/(dp-dq));
            }
            return m;
        }
        public void Evaluate()
        {
            var all=new Torch2Score{visible=true,Ready=true,cover=1,iou=1,warmth=1};
            for(int j=0;j<walls.Length;j++)
            {
                int count=Normalise(visible[j],current[j],0);Dilate(current[j],near);var c=current[j];var s=default(Torch2Score);
                for(int k=0;k<Tilts.Length;k++)
                {
                    int hit=0,spill=0,both=0,either=0;var t=targets[j][k];var h=halos[j][k];int area=Mathf.Max(1,targetArea[j][k]);
                    for(int p=0;p<Grid*Grid;p++){if(t[p]&&near[p])hit++;if(c[p]&&!h[p])spill++;if(t[p]&&c[p])both++;if(t[p]||c[p])either++;}
                    var e=new Torch2Score{visible=count>=12,cover=hit/(float)area,spill=spill/(float)area,iou=both/(float)Mathf.Max(1,either),tilt=Tilts[k]};
                    e.Ready=e.visible&&e.cover>=level.coverMin&&e.spill<=level.spillMax&&e.iou>=level.iouMin;
                    if(k==0||(e.Ready&&!s.Ready)||(e.Ready==s.Ready&&e.iou>s.iou))s=e;
                }
                s.warmth=s.Ready?1:Mathf.Clamp(Mathf.Min((s.iou-Baseline)/Mathf.Max(.05f,level.iouMin-Baseline),s.cover/level.coverMin),0,.97f);
                scores[j]=s;
                // The weakest wall decides: every lamp must show its picture at once.
                all.visible&=s.visible;all.Ready&=s.Ready;all.cover=Mathf.Min(all.cover,s.cover);all.spill=Mathf.Max(all.spill,s.spill);all.iou=Mathf.Min(all.iou,s.iou);all.warmth=Mathf.Min(all.warmth,s.warmth);
            }
            Score=all;
        }
        // Centroid + RMS radius onto the Grid x Grid compare image, the sampling grid turned by tilt degrees.
        int Normalise(bool[] source,bool[] output,float tilt)
        {
            Array.Clear(output,0,output.Length);
            int count=0;double sx=0,sy=0;
            for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)if(source[y*Width+x]){count++;sx+=x+.5;sy+=y+.5;}
            if(count<12)return count;
            float cx=(float)(sx/count),cy=(float)(sy/count);double ss=0;
            for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)if(source[y*Width+x]){float dx=x+.5f-cx,dy=y+.5f-cy;ss+=dx*dx+dy*dy;}
            float scale=Mathf.Sqrt((float)(ss/count))/RmsCells,cos=Mathf.Cos(tilt*Mathf.Deg2Rad),sin=Mathf.Sin(tilt*Mathf.Deg2Rad);
            for(int gy=0;gy<Grid;gy++)for(int gx=0;gx<Grid;gx++)
            {
                int hits=0;
                for(int s=0;s<4;s++)
                {
                    float ux=gx+.5f+((s&1)==0?-.25f:.25f)-Grid*.5f,uy=gy+.5f+((s&2)==0?-.25f:.25f)-Grid*.5f;
                    int x=Mathf.FloorToInt(cx+(ux*cos-uy*sin)*scale),y=Mathf.FloorToInt(cy+(ux*sin+uy*cos)*scale);
                    if(x>=0&&x<Width&&y>=0&&y<Height&&source[y*Width+x])hits++;
                }
                output[gy*Grid+gx]=hits>=2;
            }
            return count;
        }
        void Dilate(bool[] source,bool[] output)
        {
            Array.Clear(output,0,output.Length);
            for(int y=0;y<Grid;y++)for(int x=0;x<Grid;x++)if(source[y*Grid+x])
                foreach(var d in disc){int nx=x+d.x,ny=y+d.y;if(nx>=0&&nx<Grid&&ny>=0&&ny<Grid)output[ny*Grid+nx]=true;}
        }
        // Same inside test as TorchProjection, edge functions stepped incrementally.
        static void Raster(bool[] mask,Vector2 a,Vector2 b,Vector2 c)
        {
            float area=(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);if(area>-.001f&&area<.001f)return;
            if(area<0){var t=b;b=c;c=t;}
            int x0=Mathf.Clamp((int)Mathf.Floor(Mathf.Min(a.x,Mathf.Min(b.x,c.x))),0,Width-1),x1=Mathf.Clamp((int)Mathf.Ceil(Mathf.Max(a.x,Mathf.Max(b.x,c.x))),0,Width-1);
            int y0=Mathf.Clamp((int)Mathf.Floor(Mathf.Min(a.y,Mathf.Min(b.y,c.y))),0,Height-1),y1=Mathf.Clamp((int)Mathf.Ceil(Mathf.Max(a.y,Mathf.Max(b.y,c.y))),0,Height-1);
            float px=x0+.5f;
            for(int y=y0;y<=y1;y++)
            {
                float py=y+.5f;
                float e0=(b.x-a.x)*(py-a.y)-(b.y-a.y)*(px-a.x),e1=(c.x-b.x)*(py-b.y)-(c.y-b.y)*(px-b.x),e2=(a.x-c.x)*(py-c.y)-(a.y-c.y)*(px-c.x);
                float s0=-(b.y-a.y),s1=-(c.y-b.y),s2=-(a.y-c.y);int row=y*Width;
                for(int x=x0;x<=x1;x++){if(e0>=0&&e1>=0&&e2>=0)mask[row+x]=true;e0+=s0;e1+=s1;e2+=s2;}
            }
        }
    }
}

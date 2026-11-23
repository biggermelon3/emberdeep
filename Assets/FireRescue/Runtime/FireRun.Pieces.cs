using System;
using System.Collections.Generic;
using UnityEngine;

namespace FireRescue
{
    public enum FirePieceKind { Tower, Excavator, Plane }
    // Why a piece can't go on a square; the placement ghost shows it.
    public enum FirePlaceBlock { None, Phase, NoSquare, Burning, Occupied, Spent }

    // LoopSpec §5-§6 + the Plan bits of §11b (workstream L3): every piece, Plan-look and piece-upgrade number in one
    // block. All values are *tune*. Pieces exist in the Emberdeep (goddess) flow only; the legacy tool flow keeps its
    // pre-placed spray-to-fill reservoirs (§11).
    public static class EmberdeepPieceTuning
    {
        // ---- Squares ----------------------------------------------------------------------------------------
        // Pieces stand on the board's squares: the gaps between four trees (where grass grows and the old towers
        // stood), plus a ring half a square outside the field on the left, right and back. A front ring would sit in
        // the creek and the sheep refuge, so there is none.
        public const float BurningSquareRadius=.5f; // burning fuel this close (the square's own grass) makes it a burning square
        public const float CabinKeepOut=.9f, SheepKeepOut=.45f; // a waiting sheep's home is ~.48 behind the back squares

        // ---- Water tower (arrives full, rains once on its own) --------------------------------------------------
        public const float TowerRadius=2.4f, TowerRadiusPerLevel=.4f, TowerSeconds=4, TowerRainPerSecond=35;
        public const float TowerWetness=EmberdeepBoardTuning.FullSoak; // soaks like palm water: 8 s of protection after
        public const float RainHeight=.7f; // sim point of the rain (tree anchor height), so the radius is the radius on the trees

        // ---- Excavator --------------------------------------------------------------------------------------
        public const int ExcavatorMaxSquares=4;          // N squares drawn after its own
        public const float ExcavatorSquaresPerSecond=1;  // slow on purpose: start it early
        public const float DugBlockRadius=.4f;           // a catch whose path passes this close to a dug line never happens
        public const float DugClearRadius=.35f;          // fuel this close to a dug line is cleared (dirt, not charred)

        // ---- Plane drop -------------------------------------------------------------------------------------
        public const float PlaneDelaySeconds=3, PlaneSpeed=7.5f, PlaneHalfWidth=.75f, PlaneWater=140, PlaneLeadIn=3;
        public const float PlaneHeight=4.2f;            // flight height (arena units) for the view
        public const float PlanePerSheep=.5f, PlanePerCombo=.25f; // earned in the fight, like a killstreak
        public const int PlaneComboStep=3;               // every 3rd tree in a chain charges PlanePerCombo
        public const float PointRadius=.05f;             // strip water hits exactly the fuel under it

        // ---- Upgrades (§7) ----------------------------------------------------------------------------------
        public static readonly int[] SlotPrices={120,240};     // SLOT: +1 tower on every level that hands out pieces
        public static readonly int[] TowerPrices={80,160,260}; // TOWER: +TowerRadiusPerLevel rain radius

        // ---- Plan look (§6, EmberdeepPlanLook) -------------------------------------------------------------------
        public const float Grade=1;              // strength of the whitish grade while time is stopped
        public const float FloodSeconds=.9f;     // colour floods back from the board centre when the fire resumes
        public const float FloodEdge=.05f;       // m, the soft, warm front of the flood
        public const float FloodReach=.75f;      // m, a radius past the farthest board corner from the centre
        public const float ParticleGrade=.3f;    // the frozen fire keeps most of its colour (it is the thing to read)
        public const float WarmupSeconds=.7f;    // fire and smoke are simulated this long before they freeze
        public const float HumVolume=.16f, CrackleVolume=.11f, WhooshVolume=.55f, PlaneVolume=.6f;
    }

    // One level's pieces, keyed by IncidentIndex (never Stage: the palm-lesson count is changing).
    public readonly struct FirePieceSet
    {
        public readonly int Towers, Excavators;
        public readonly bool Plane; // earnable in the fight
        public FirePieceSet(int towers,int excavators,bool plane){Towers=towers;Excavators=excavators;Plane=plane;}
        public bool Any=>Towers>0 || Excavators>0 || Plane;
    }

    public sealed partial class FireRun
    {
        // Keyed by IncidentIndex: the incident stages' sets, authored with the levels (EmberdeepLevels, workstream LV).
        public static readonly FirePieceSet[] PieceSets=EmberdeepLevels.IncidentPieceSets();
        public sealed class Piece
        {
            public FirePieceKind Kind;
            public int Square=-1; // -1 while in the tray
            public bool Placed=>Square>=0;
            // Tower
            public bool Raining, Used;
            public float RainRemaining;
            // Excavator: the drawn line (squares after its own, in order) and how far it has driven it.
            public readonly List<int> Line=new List<int>();
            public bool LineDone, Driving, Finished;
            public int Driven;
            public float DriveClock;
        }
        public enum PlaneStage { None, Marked, Flying }

        public Piece[] Pieces { get; private set; }=Array.Empty<Piece>();
        // Square centres (arena units, y 0), row-major: SquareColumns across, SquareRows deep.
        public Vector3[] Squares { get; private set; }=Array.Empty<Vector3>();
        public int SquareColumns { get; private set; }
        public int SquareRows { get; private set; }
        public FirePieceSet PieceSet=>!BoardRules || IsPalmLesson?default:Level!=null?Level.Pieces:PieceSets[Mathf.Clamp(IncidentIndex,0,PieceSets.Length-1)];
        public bool HasPieces=>Pieces.Length>0 || PieceSet.Plane;
        public int PiecesInTray { get { int n=0; foreach(var p in Pieces) if(!p.Placed)n++; return n; } }
        public event Action<int> TowerRained;       // piece index
        public event Action<int,int> ExcavatorDug;  // piece index, square reached
        public event Action PlaneArrived;           // the plane starts its run over the board

        // ---- Upgrades -----------------------------------------------------------------------------------------
        public int SlotLevel { get; private set; }
        public int TowerLevel { get; private set; }
        public int SlotLevels=>EmberdeepPieceTuning.SlotPrices.Length;
        public int TowerLevels=>EmberdeepPieceTuning.TowerPrices.Length;
        public float TowerRadius=>EmberdeepPieceTuning.TowerRadius+TowerLevel*EmberdeepPieceTuning.TowerRadiusPerLevel;
        public int SlotUpgradeCost=>SlotLevel<SlotLevels?EmberdeepPieceTuning.SlotPrices[SlotLevel]:0;
        public int TowerUpgradeCost=>TowerLevel<TowerLevels?EmberdeepPieceTuning.TowerPrices[TowerLevel]:0;
        // Playtest 2026-11-22: SLOT and TOWER were for sale before the player had ever seen a tower. Both stay locked until
        // the level that introduces towers: in its Plan (the tray shows them), or for good once it has been cleared.
        public static readonly int TowerStage=Mathf.Max(0,Array.FindIndex(EmberdeepLevels.Stages,l=>l.Pieces.Towers>0));
        public bool TowersIntroduced=>Stage>=TowerStage || HighestUnlockedStage>TowerStage;
        public bool CanUpgradeSlot=>GemMode && InSupply && !Supply.Locked && !NeedsMagicUpgrade && TowersIntroduced && SlotUpgradeCost>0 && Coins>=SlotUpgradeCost;
        public bool CanUpgradeTower=>GemMode && InSupply && !Supply.Locked && !NeedsMagicUpgrade && TowersIntroduced && TowerUpgradeCost>0 && Coins>=TowerUpgradeCost;
        // A bought slot shows up in the tray of the level being planned right now.
        public bool BuySlotUpgrade()
        {
            if(!CanUpgradeSlot)return false;
            Coins-=SlotUpgradeCost;SlotLevel++;
            if(Phase==FirePhase.Preparing && PieceSet.Towers+PieceSet.Excavators>0)AddPiece(FirePieceKind.Tower);
            return true;
        }
        public bool BuyTowerUpgrade()
        {
            if(!CanUpgradeTower)return false;
            Coins-=TowerUpgradeCost;TowerLevel++;version++;return true;
        }
        void ResetPieceUpgrades(){SlotLevel=TowerLevel=0;}
        void ResetPieces(){Pieces=Array.Empty<Piece>();Squares=Array.Empty<Vector3>();SquareColumns=SquareRows=0;cutCount=0;dug.Clear();ResetPlane();PlaneCharge01=0;}

        // After the trees, grass, cabins and sheep of a new level are built (StartStage).
        void BuildPieces()
        {
            ResetPieces();planeSheep=0;planeCombo=0;
            var set=PieceSet;if(!set.Any)return;
            int columns=FieldColumns,rows=FieldRows; // the field grid: an authored level can have clearings
            if(columns<1 || rows<1)return;
            SquareColumns=columns+1;SquareRows=rows;Squares=new Vector3[SquareColumns*SquareRows];
            float half=EmberdeepBoardLayout.ColumnSpacing*.5f,halfRow=EmberdeepBoardLayout.RowSpacing*.5f;
            for(int h=0;h<rows;h++)for(int g=0;g<=columns;g++)
            {
                // Between tree columns g-1 and g (outside the field at the ends), behind tree row h.
                float x=g==0?FieldX(0)-half:g==columns?FieldX(columns-1)+half:(FieldX(g-1)+FieldX(g))*.5f;
                Squares[h*SquareColumns+g]=new Vector3(x,0,FieldZ(h)+halfRow);
            }
            var list=new List<Piece>();
            int towers=set.Towers+(set.Towers+set.Excavators>0?SlotLevel:0);
            for(int i=0;i<towers;i++)list.Add(new Piece{Kind=FirePieceKind.Tower});
            for(int i=0;i<set.Excavators;i++)list.Add(new Piece{Kind=FirePieceKind.Excavator});
            Pieces=list.ToArray();
        }
        void AddPiece(FirePieceKind kind)
        {
            var list=new List<Piece>(Pieces);
            int at=kind==FirePieceKind.Tower?list.FindLastIndex(p=>p.Kind==FirePieceKind.Tower)+1:list.Count;
            list.Insert(at,new Piece{Kind=kind});Pieces=list.ToArray();
        }

        // ---- Squares ------------------------------------------------------------------------------------------
        public bool ValidSquare(int square)=>square>=0 && square<Squares.Length;
        public int SquareColumn(int square)=>SquareColumns>0?square%SquareColumns:0;
        public int SquareRow(int square)=>SquareColumns>0?square/SquareColumns:0;
        public bool Adjacent(int a,int b)
        {
            if(!ValidSquare(a) || !ValidSquare(b) || a==b)return false;
            return Mathf.Abs(SquareColumn(a)-SquareColumn(b))<=1 && Mathf.Abs(SquareRow(a)-SquareRow(b))<=1;
        }
        // The square nearest a board point (arena units), or -1 if it is farther than maxDistance from every square.
        public int NearestSquare(Vector3 point,float maxDistance=.75f)
        {
            int best=-1;float bestD=maxDistance*maxDistance;
            for(int i=0;i<Squares.Length;i++){float d=Flat2(Squares[i],point);if(d<bestD){bestD=d;best=i;}}
            return best;
        }
        static float Flat2(Vector3 a,Vector3 b){float dx=a.x-b.x,dz=a.z-b.z;return dx*dx+dz*dz;}
        public bool SquareBurning(int square)
        {
            if(!ValidSquare(square))return false;
            var at=Squares[square];float r2=EmberdeepPieceTuning.BurningSquareRadius*EmberdeepPieceTuning.BurningSquareRadius;
            for(int c=0;c<CellCount;c++){var fuel=Cell(c);if(fuel.State==TreeState.Burning && Flat2(fuel.Position,at)<r2)return true;}
            foreach(var a in Animals)if(a.OnFire && a.State!=AnimalState.Lost && Flat2(a.Position,at)<r2)return true;
            return false;
        }
        // Another piece, an excavator's line, a cabin or a waiting sheep is on that square.
        public bool SquareTaken(int square,int exceptPiece=-1)
        {
            if(!ValidSquare(square))return true;
            for(int i=0;i<Pieces.Length;i++)
            {
                if(i==exceptPiece)continue;var p=Pieces[i];
                if(p.Square==square || p.Kind==FirePieceKind.Excavator && p.Line.Contains(square))return true;
            }
            var at=Squares[square];
            foreach(var prop in Props)if(prop.Kind==FirePropKind.Cabin && Flat2(prop.Position,at)<EmberdeepPieceTuning.CabinKeepOut*EmberdeepPieceTuning.CabinKeepOut)return true;
            foreach(var a in Animals)
                if((a.State==AnimalState.Waiting || a.State==AnimalState.Running) && a.HeldBy<0
                    && Flat2(a.Position,at)<EmberdeepPieceTuning.SheepKeepOut*EmberdeepPieceTuning.SheepKeepOut)return true;
            return false;
        }

        // ---- Placing (by hand only: the game never calls this from the ray) ---------------------------------------
        // Plan: every piece. Fight: the ones still in the tray (a placed piece stays put once the fire runs).
        public FirePlaceBlock CanPlace(int piece,int square)
        {
            if(piece<0 || piece>=Pieces.Length)return FirePlaceBlock.Spent;
            if(Phase!=FirePhase.Preparing && Phase!=FirePhase.Fighting)return FirePlaceBlock.Phase;
            var p=Pieces[piece];
            if(p.Used || p.Raining || p.Driving || p.Finished || p.Placed && Phase==FirePhase.Fighting)return FirePlaceBlock.Spent;
            if(!ValidSquare(square))return FirePlaceBlock.NoSquare;
            if(SquareBurning(square))return FirePlaceBlock.Burning;
            if(SquareTaken(square,piece))return FirePlaceBlock.Occupied;
            return FirePlaceBlock.None;
        }
        public bool PlacePiece(int piece,int square)
        {
            if(CanPlace(piece,square)!=FirePlaceBlock.None)return false;
            var p=Pieces[piece];p.Square=square;p.Line.Clear();p.LineDone=false;version++;return true;
        }
        // Plan only: pick a placed piece back up (it goes back to the tray).
        public bool CanLift(int piece)=>Phase==FirePhase.Preparing && piece>=0 && piece<Pieces.Length && Pieces[piece].Placed
            && !Pieces[piece].Used && !Pieces[piece].Driving;
        public bool LiftPiece(int piece)
        {
            if(!CanLift(piece))return false;
            var p=Pieces[piece];p.Square=-1;p.Line.Clear();p.LineDone=false;version++;return true;
        }

        // ---- Water tower -------------------------------------------------------------------------------------------
        public Vector3 RainPoint(Piece p)=>p.Placed?new Vector3(Squares[p.Square].x,EmberdeepPieceTuning.RainHeight,Squares[p.Square].z):Vector3.zero;
        // "Fire within its radius" = something burning that its rain would reach.
        bool FireWithin(Vector3 point,float radius)
        {
            for(int c=0;c<CellCount;c++){var fuel=Cell(c);if(fuel.State==TreeState.Burning && Vector3.Distance(fuel.Position,point)<=radius)return true;}
            foreach(var a in Animals)
                if(a.OnFire && (a.State==AnimalState.Waiting || a.State==AnimalState.Running)
                    && Vector3.Distance(a.Position+Vector3.up*AnimalHeight,point)<=radius+AnimalRadius)return true;
            return false;
        }
        void TickTower(int index,Piece p,float dt)
        {
            if(!p.Placed)return;
            var point=RainPoint(p);
            if(!p.Used && FireWithin(point,TowerRadius))
            {p.Used=p.Raining=true;p.RainRemaining=EmberdeepPieceTuning.TowerSeconds;version++;TowerRained?.Invoke(index);}
            if(!p.Raining)return;
            float d=Mathf.Min(dt,p.RainRemaining);p.RainRemaining=Mathf.Max(0,p.RainRemaining-d);
            if(d>0)ApplyWater(point,d*EmberdeepPieceTuning.TowerRainPerSecond,TowerRadius,EmberdeepPieceTuning.TowerWetness);
            if(p.RainRemaining<=0)p.Raining=false;
        }

        // ---- Excavator ---------------------------------------------------------------------------------------------
        Vector4[] cuts=new Vector4[8];int cutCount;
        readonly HashSet<int> dug=new HashSet<int>();
        // Dug line segments (x0, z0, x1, z1 in arena units); a single dug square is a zero-length segment.
        public int DugCount=>cutCount;
        public Vector4 DugSegment(int i)=>i>=0 && i<cutCount?cuts[i]:default;
        public bool IsDug(int square)=>dug.Contains(square);
        Piece Excavator(int piece)=>piece>=0 && piece<Pieces.Length && Pieces[piece].Kind==FirePieceKind.Excavator?Pieces[piece]:null;
        public bool CanDraw(int piece)
        {
            var p=Excavator(piece);
            return p!=null && p.Placed && !p.LineDone && !p.Driving && !p.Finished && (Phase==FirePhase.Preparing || Phase==FirePhase.Fighting);
        }
        // Starts (or, in Plan, restarts) the line from the excavator's own square.
        public bool StartLine(int piece)
        {
            var p=Excavator(piece);
            if(p==null || !p.Placed || p.Driving || p.Finished || (Phase!=FirePhase.Preparing && Phase!=FirePhase.Fighting))return false;
            if(p.LineDone && Phase!=FirePhase.Preparing)return false;
            p.Line.Clear();p.LineDone=false;return true;
        }
        public int LineEnd(int piece){var p=Excavator(piece);return p==null?-1:p.Line.Count>0?p.Line[p.Line.Count-1]:p.Square;}
        public bool ExtendLine(int piece,int square)
        {
            if(!CanDraw(piece))return false;var p=Pieces[piece];
            if(p.Line.Count>=EmberdeepPieceTuning.ExcavatorMaxSquares || !Adjacent(LineEnd(piece),square))return false;
            if(square==p.Square || p.Line.Contains(square) || SquareBurning(square) || SquareTaken(square,piece))return false;
            p.Line.Add(square);return true;
        }
        // A fingertip can skip squares between frames: step one square at a time toward it (diagonals allowed).
        // Going back onto the previous square takes the last one off. Returns the squares added.
        public int TraceLine(int piece,int square)
        {
            if(!CanDraw(piece) || !ValidSquare(square))return 0;
            var p=Pieces[piece];
            int previous=p.Line.Count>1?p.Line[p.Line.Count-2]:p.Line.Count==1?p.Square:-1;
            if(square==previous){p.Line.RemoveAt(p.Line.Count-1);return 0;}
            int added=0;
            for(int guard=0;guard<=EmberdeepPieceTuning.ExcavatorMaxSquares;guard++)
            {
                int last=LineEnd(piece);if(last==square)break;
                int c=SquareColumn(last),r=SquareRow(last);
                int next=(r+Math.Sign(SquareRow(square)-r))*SquareColumns+c+Math.Sign(SquareColumn(square)-c);
                if(!ExtendLine(piece,next))break;added++;
            }
            return added;
        }
        public bool FinishLine(int piece)
        {
            if(!CanDraw(piece) || Pieces[piece].Line.Count==0)return false;
            Pieces[piece].LineDone=true;return true;
        }
        // Where the excavator is on the board right now (arena units).
        public Vector3 ExcavatorPosition(int piece)
        {
            var p=Excavator(piece);if(p==null || !p.Placed)return Vector3.zero;
            if(!p.Driving || p.Finished || p.Driven>=p.Line.Count)return Squares[p.Driven==0?p.Square:p.Line[p.Driven-1]];
            int from=p.Driven==0?p.Square:p.Line[p.Driven-1];
            return Vector3.Lerp(Squares[from],Squares[p.Line[p.Driven]],Mathf.Clamp01(p.DriveClock));
        }
        // It drives only while the fire runs (time is stopped in Plan), and stops for good where fire got there first.
        void TickExcavator(int index,Piece p,float dt)
        {
            if(!p.Placed || !p.LineDone || p.Finished)return;
            if(!p.Driving){p.Driving=true;p.DriveClock=0;Dig(index,p.Square,p.Square);}
            p.DriveClock+=dt*EmberdeepPieceTuning.ExcavatorSquaresPerSecond;
            for(int guard=0;guard<=EmberdeepPieceTuning.ExcavatorMaxSquares && !p.Finished;guard++)
            {
                if(p.Driven>=p.Line.Count){p.Finished=true;p.DriveClock=0;break;}
                int from=p.Driven==0?p.Square:p.Line[p.Driven-1],to=p.Line[p.Driven];
                if(LineBurning(from,to)){p.Finished=true;p.DriveClock=0;break;}
                if(p.DriveClock<1)break;
                p.DriveClock-=1;p.Driven++;Dig(index,from,to);
            }
        }
        bool LineBurning(int from,int to)
        {
            if(SquareBurning(to))return true;
            var a=Squares[from];var b=Squares[to];
            for(int c=0;c<CellCount;c++)
            {
                var fuel=Cell(c);
                if(fuel.State==TreeState.Burning && PointSegment(fuel.Position.x,fuel.Position.z,a.x,a.z,b.x,b.z)<=EmberdeepPieceTuning.DugClearRadius)return true;
            }
            return false;
        }
        // Dirt from square a to b: fuel on it is cleared (never counts as charred) and spread can't cross it.
        void Dig(int piece,int a,int b)
        {
            var A=Squares[a];var B=Squares[b];
            if(cutCount>=cuts.Length)Array.Resize(ref cuts,cuts.Length*2);
            cuts[cutCount++]=new Vector4(A.x,A.z,B.x,B.z);dug.Add(a);dug.Add(b);
            for(int c=0;c<CellCount;c++)
            {
                var fuel=Cell(c);if(fuel.State!=TreeState.Safe && fuel.State!=TreeState.Saved)continue;
                if(PointSegment(fuel.Position.x,fuel.Position.z,A.x,A.z,B.x,B.z)>EmberdeepPieceTuning.DugClearRadius)continue;
                fuel.State=TreeState.Cleared;fuel.Heat=0;fuel.Wetness=0;
            }
            version++;ExcavatorDug?.Invoke(piece,b);
        }
        // The explicit spread test (LoopSpec §5): a catch from a to b never crosses a dug line, so wind's 2.2 downwind
        // reach can't jump a one-square gap. Used by Choose() for play and the forecast alike.
        public bool SpreadBlocked(Vector3 from,Vector3 to)=>CutBetween(cuts,cutCount,from.x,from.z,to.x,to.z);
        static bool CutBetween(Vector4[] cuts,int count,float ax,float az,float bx,float bz)
        {
            for(int k=0;k<count;k++)
                if(SegmentDistance(ax,az,bx,bz,cuts[k].x,cuts[k].y,cuts[k].z,cuts[k].w)<EmberdeepPieceTuning.DugBlockRadius)return true;
            return false;
        }
        public static float PointSegment(float px,float pz,float ax,float az,float bx,float bz)
        {
            float dx=bx-ax,dz=bz-az,len=dx*dx+dz*dz;
            float t=len>1e-8f?Mathf.Clamp01(((px-ax)*dx+(pz-az)*dz)/len):0;
            float x=ax+dx*t-px,z=az+dz*t-pz;return Mathf.Sqrt(x*x+z*z);
        }
        public static float SegmentDistance(float ax,float az,float bx,float bz,float cx,float cz,float dx,float dz)
        {
            float d1=Cross(cx,cz,dx,dz,ax,az),d2=Cross(cx,cz,dx,dz,bx,bz),d3=Cross(ax,az,bx,bz,cx,cz),d4=Cross(ax,az,bx,bz,dx,dz);
            if((d1>0 && d2<0 || d1<0 && d2>0) && (d3>0 && d4<0 || d3<0 && d4>0))return 0;
            return Mathf.Min(Mathf.Min(PointSegment(ax,az,cx,cz,dx,dz),PointSegment(bx,bz,cx,cz,dx,dz)),
                Mathf.Min(PointSegment(cx,cz,ax,az,bx,bz),PointSegment(dx,dz,ax,az,bx,bz)));
        }
        static float Cross(float ax,float az,float bx,float bz,float px,float pz)=>(bx-ax)*(pz-az)-(bz-az)*(px-ax);

        // ---- Plane drop --------------------------------------------------------------------------------------------
        public bool PlaneEarnable=>PieceSet.Plane;
        public float PlaneCharge01 { get; private set; }
        public PlaneStage PlaneState { get; private set; }
        public bool PlaneReady=>PlaneEarnable && Phase==FirePhase.Fighting && PlaneState==PlaneStage.None && PlaneCharge01>=1;
        public int PlaneSquare { get; private set; }=-1;
        public Vector3 PlaneMarker { get; private set; }
        public Vector3 PlaneDirection { get; private set; }=Vector3.forward;
        public float PlaneCountdown { get; private set; }
        public float PlaneHead { get; private set; }
        public float PlaneStart { get; private set; }
        public float PlaneEnd { get; private set; }
        public Vector3 PlanePosition=>PlaneMarker+PlaneDirection*PlaneHead+Vector3.up*EmberdeepPieceTuning.PlaneHeight;
        int planeSheep,planeCombo;
        bool[] planeWet=Array.Empty<bool>(),planeSheepWet=Array.Empty<bool>();
        void ResetPlane(){PlaneState=PlaneStage.None;PlaneSquare=-1;PlaneCountdown=PlaneHead=0;}
        // Dev/test: fill the charge (the plane is still earnable only on its levels).
        public void ChargePlaneForTest(){if(PlaneEarnable)PlaneCharge01=1;}
        // Board yaw in degrees (0 = away from the player, 90 = right), as the wind uses.
        public static Vector3 YawDirection(float degrees)=>FireWindPlan.Direction(degrees);
        public FirePlaceBlock CanCallPlane(int square)
        {
            if(!PlaneReady)return Phase==FirePhase.Fighting?FirePlaceBlock.Spent:FirePlaceBlock.Phase;
            // The bomber is aimed AT the fire, so unlike the pieces its marker may sit on a burning square.
            return ValidSquare(square)?FirePlaceBlock.None:FirePlaceBlock.NoSquare;
        }
        public bool CallPlane(int square,float yawDegrees)
        {
            if(CanCallPlane(square)!=FirePlaceBlock.None)return false;
            PlaneSquare=square;PlaneMarker=Squares[square];PlaneDirection=YawDirection(yawDegrees);
            PlaneCountdown=EmberdeepPieceTuning.PlaneDelaySeconds;PlaneCharge01=0;PlaneState=PlaneStage.Marked;
            float min=0,max=0;
            for(int c=0;c<CellCount;c++){float along=Along(Cell(c).Position);min=Mathf.Min(min,along);max=Mathf.Max(max,along);}
            PlaneStart=min-EmberdeepPieceTuning.PlaneLeadIn;PlaneEnd=max+EmberdeepPieceTuning.PlaneLeadIn;PlaneHead=PlaneStart;
            planeWet=new bool[CellCount];planeSheepWet=new bool[Animals.Length];
            return true;
        }
        float Along(Vector3 p)=>(p.x-PlaneMarker.x)*PlaneDirection.x+(p.z-PlaneMarker.z)*PlaneDirection.z;
        float Across(Vector3 p)=>Mathf.Abs((p.x-PlaneMarker.x)*PlaneDirection.z-(p.z-PlaneMarker.z)*PlaneDirection.x);
        public bool InPlaneStrip(Vector3 p)=>PlaneState!=PlaneStage.None && Across(p)<=EmberdeepPieceTuning.PlaneHalfWidth;
        void TickPlane(float dt)
        {
            if(PlaneState==PlaneStage.None)return;
            if(PlaneState==PlaneStage.Marked)
            {
                PlaneCountdown-=dt;if(PlaneCountdown>0)return;
                dt=-PlaneCountdown;PlaneCountdown=0;PlaneState=PlaneStage.Flying;PlaneArrived?.Invoke();
            }
            PlaneHead=Mathf.Min(PlaneEnd,PlaneHead+dt*EmberdeepPieceTuning.PlaneSpeed);
            // Every fuel and sheep in the strip gets one full drop as the plane passes over it.
            for(int c=0;c<CellCount && c<planeWet.Length && Phase==FirePhase.Fighting;c++)
            {
                var fuel=Cell(c);if(planeWet[c] || Along(fuel.Position)>PlaneHead || Across(fuel.Position)>EmberdeepPieceTuning.PlaneHalfWidth)continue;
                planeWet[c]=true;if(fuel.State==TreeState.Cleared)continue;
                ApplyWater(fuel.Position,EmberdeepPieceTuning.PlaneWater,EmberdeepPieceTuning.PointRadius,EmberdeepBoardTuning.FullSoak);
            }
            for(int i=0;i<Animals.Length && i<planeSheepWet.Length && Phase==FirePhase.Fighting;i++)
            {
                var a=Animals[i];if(planeSheepWet[i] || Along(a.Position)>PlaneHead || Across(a.Position)>EmberdeepPieceTuning.PlaneHalfWidth)continue;
                planeSheepWet[i]=true;ApplyAnimalWater(a.Position+Vector3.up*AnimalHeight,EmberdeepPieceTuning.PlaneWater,EmberdeepPieceTuning.PointRadius);
            }
            if(PlaneHead>=PlaneEnd)ResetPlane();
        }
        // Charge from play: each sheep saved and every PlaneComboStep-th tree of a chain. The plane's own drop and
        // the time it is out don't charge the next one.
        void ChargePlane()
        {
            int saved=AnimalsSaved,combo=Combo;float gain=0;
            if(saved>planeSheep)gain+=(saved-planeSheep)*EmberdeepPieceTuning.PlanePerSheep;
            if(combo<planeCombo)planeCombo=0;
            gain+=(combo/EmberdeepPieceTuning.PlaneComboStep-planeCombo/EmberdeepPieceTuning.PlaneComboStep)*EmberdeepPieceTuning.PlanePerCombo;
            planeSheep=saved;planeCombo=combo;
            if(PlaneEarnable && PlaneState==PlaneStage.None && gain>0)PlaneCharge01=Mathf.Min(1,PlaneCharge01+gain);
        }

        // Once per fighting frame, after the board (FireRun.Tick).
        void TickPieces(float dt)
        {
            if(!BoardRules || Phase!=FirePhase.Fighting)return;
            ChargePlane();
            for(int i=0;i<Pieces.Length && Phase==FirePhase.Fighting;i++)
            {
                var p=Pieces[i];
                if(p.Kind==FirePieceKind.Tower)TickTower(i,p,dt);else if(p.Kind==FirePieceKind.Excavator)TickExcavator(i,p,dt);
            }
            if(Phase==FirePhase.Fighting)TickPlane(dt);
        }
    }
}

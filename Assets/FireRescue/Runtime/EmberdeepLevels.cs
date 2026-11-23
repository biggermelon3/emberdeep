using System;
using System.Collections.Generic;
using UnityEngine;

namespace FireRescue
{
    // LoopSpec §8 (workstream LV): the hand-authored Emberdeep levels. One palm lesson (stage 0), then the five
    // incidents (stages 1-5 = FireRun.IncidentIndex 0-4). Each level brings in one new thing and teaches it through the
    // board itself, with no text. FireRun.StartStage builds the Emberdeep board from this table only; the legacy tool
    // flow keeps its generated field (FireRun.cs Counts/Starts).
    //
    // MAP: one string per line, the far side of the board on top (as the seated player sees it), the front row last.
    // Width 2*columns+1. Below the optional sheep lines, square lines and tree lines alternate, ending with the front
    // tree row. Tree column c is character 2c+1; the gap (lane, square) g between tree columns g-1 and g is character 2g.
    //   Sheep line   gaps: S = a sheep home in that lane. The lowest sheep line is 1.05 behind the back tree row, each
    //                line above it 1.15 further back.
    //   Square line  gaps: the board's piece squares behind the tree row below it (FireRun.Pieces.cs).
    //                . square   g grass on it   G grass on fire at the start
    //   Tree line    columns: T tree   F tree on fire at the start   . clearing (no fuel)   H cabin   g meadow grass
    //                gaps:    S a sheep home between two trees of that row (in a lane)
    // Positions (arena units): column x=(c-(columns-1)/2)*1.18, row z=3+r*1.15, square = between its two columns, half
    // a row behind its row (EmberdeepBoardLayout). The field is at most 4 rows by 8 columns (§5 Reach).
    //
    // Fire clocks are per level: TreeCatch is the seconds between tree catches (one new tree per tick over the whole
    // board; keep it a whole number so tree catches never tie the half-second grass clock), TreeBurn the seconds a tree
    // burns before it chars. Grass keeps EmberdeepBoardTuning.Grass. Wind shifts stay off whole and half seconds.
    //
    // Hints are what a reasonable player does in Plan: the EditMode level gate (EmberdeepLevelTests) places the pieces
    // there and plays the level with a scripted hand. Squares are (g, h): gap g, behind tree row h.
    public sealed class EmberdeepLevel
    {
        public string[] Map;
        public float TreeCatch=10, TreeBurn=60, StartHeat=100;
        public FireWindPlan Wind;
        public FirePieceSet Pieces;
        public int BurnLimit=1;
        public float Par=60;
        public Vector2Int[] TowerHints=Array.Empty<Vector2Int>();
        public Vector2Int[] DigHint=Array.Empty<Vector2Int>(); // [0] = the excavator's square, then its line in order
        // Cabins off the tree grid (board x, z), after the map's H cells. Keep them .55+ from trunks and on the board.
        public Vector2[] CabinSpots=Array.Empty<Vector2>();

        // ---- Parsed from Map ----------------------------------------------------------------------------------------
        public readonly struct TreeCell
        {
            public readonly int Column, Row;public readonly bool Burning;
            public TreeCell(int column,int row,bool burning){Column=column;Row=row;Burning=burning;}
        }
        public readonly struct GrassPatch
        {
            public readonly Vector2 At;public readonly bool Burning;
            public GrassPatch(Vector2 at,bool burning){At=at;Burning=burning;}
        }
        public int Columns{get{Parse();return columns;}}
        public int Rows{get{Parse();return rows;}}
        // Row-major from the front row, left to right: the order of FireRun.Trees.
        public TreeCell[] Trees{get{Parse();return trees;}}
        public Vector2[] Cabins{get{Parse();return cabins;}}            // board x, z
        public Vector2[] SheepHomes{get{Parse();return sheep;}}         // nearest the field first, then left to right
        public GrassPatch[] Grass{get{Parse();return grass;}}
        public int Starts{get{int n=0;foreach(var t in Trees)if(t.Burning)n++;foreach(var g in Grass)if(g.Burning)n++;return n;}}

        int columns,rows;bool parsed;
        TreeCell[] trees;Vector2[] cabins,sheep;GrassPatch[] grass;

        public float ColumnX(int c){Parse();return X(c);}
        public static float RowZ(int r)=>EmberdeepBoardLayout.FrontRowZ+r*EmberdeepBoardLayout.RowSpacing;
        // The gap g between tree columns g-1 and g (half a column outside the field at either end).
        public float GapX(int g){Parse();return Gap(g);}
        float X(int c)=>(c-(columns-1)*.5f)*EmberdeepBoardLayout.ColumnSpacing;
        float Gap(int g)=>g<=0?X(0)-EmberdeepBoardLayout.ColumnSpacing*.5f:g>=columns?X(columns-1)+EmberdeepBoardLayout.ColumnSpacing*.5f:(X(g-1)+X(g))*.5f;
        public static float SquareZ(int h)=>RowZ(h)+EmberdeepBoardLayout.RowSpacing*.5f;
        // FireRun.Squares index of square (g, h), as FireRun.BuildPieces lays them out.
        public int Square(Vector2Int gh)=>gh.y*(Columns+1)+gh.x;

        void Parse()
        {
            if(parsed)return;
            if(Map==null || Map.Length<2)throw new ArgumentException("level map needs at least a square line and a tree line");
            int width=Map[0].Length;
            if(width<3 || width%2==0)throw new ArgumentException($"level map width {width}: must be 2*columns+1");
            columns=(width-1)/2;
            foreach(var line in Map)if(line.Length!=width)throw new ArgumentException($"level map line \"{line}\" is not {width} wide");
            // Sheep lines: only spaces and S. The rest alternates square, tree, ..., tree (front).
            int top=0;
            while(top<Map.Length && IsSheepLine(Map[top]))top++;
            int body=Map.Length-top;
            if(body<2 || body%2!=0)throw new ArgumentException("level map: square and tree lines must alternate and end with the front tree row");
            rows=body/2;
            if(rows>EmberdeepBoardLayout.MaxRows || columns>EmberdeepBoardLayout.MaxColumns)throw new ArgumentException($"level map {columns}x{rows} is larger than the board");
            var treeList=new List<TreeCell>();var cabinList=new List<Vector2>();var sheepList=new List<Vector2>();var grassList=new List<GrassPatch>();
            for(int r=0;r<rows;r++)
            {
                string treeLine=Map[Map.Length-1-2*r],squareLine=Map[Map.Length-2-2*r];
                float z=RowZ(r);
                for(int c=0;c<columns;c++)
                {
                    char k=treeLine[2*c+1];var at=new Vector2(X(c),z);
                    switch(k)
                    {
                        case 'T':treeList.Add(new TreeCell(c,r,false));break;
                        case 'F':treeList.Add(new TreeCell(c,r,true));break;
                        case 'H':cabinList.Add(at);break;
                        case 'g':grassList.Add(new GrassPatch(at,false));break;
                        case 'G':grassList.Add(new GrassPatch(at,true));break;
                        case '.':break;
                        default:throw new ArgumentException($"level map: '{k}' is not a tree-cell glyph (T F . H g G)");
                    }
                }
                for(int g=0;g<=columns;g++)
                {
                    char k=treeLine[2*g];
                    if(k=='S')sheepList.Add(new Vector2(Gap(g),z));else if(k!=' ')throw new ArgumentException($"level map: '{k}' between trees (S or space)");
                    k=squareLine[2*g];var at=new Vector2(Gap(g),SquareZ(r));
                    if(k=='g')grassList.Add(new GrassPatch(at,false));else if(k=='G')grassList.Add(new GrassPatch(at,true));
                    else if(k!='.')throw new ArgumentException($"level map: '{k}' is not a square glyph (. g G)");
                }
                for(int c=0;c<columns;c++)if(squareLine[2*c+1]!=' ')throw new ArgumentException("level map: square lines hold glyphs in the gaps only");
            }
            // Sheep behind the field, nearest line first.
            for(int line=top-1;line>=0;line--)
            {
                float z=RowZ(rows-1)+1.05f+(top-1-line)*EmberdeepBoardLayout.RowSpacing;
                for(int g=0;g<=columns;g++)if(Map[line][2*g]=='S')sheepList.Add(new Vector2(Gap(g),z));
            }
            if(CabinSpots!=null)cabinList.AddRange(CabinSpots);
            trees=treeList.ToArray();cabins=cabinList.ToArray();sheep=sheepList.ToArray();grass=grassList.ToArray();
            if(trees.Length==0)throw new ArgumentException("level map has no trees");
            parsed=true;
        }
        static bool IsSheepLine(string line)
        {
            bool any=false;
            foreach(char k in line){if(k=='S')any=true;else if(k!=' ')return false;}
            return any;
        }
    }

    public static class EmberdeepLevels
    {
        // Indexed by FireRun.Stage. Names and level-card chips live with the text pass (FireRun.Names, Screens.cs).
        public static readonly EmberdeepLevel[] Stages=
        {
            // ---- Stage 0, palm lesson: palm water + sheep (T's PALM RESCUE grove, FireRun.PalmLesson*) -----------------
            // Three fires in a small grove. The back-left fire is beside the left sheep: put it out first or that sheep
            // catches fire at 8 s. Each lane opens as the fires next to it go out, and its sheep run to the creek.
            new EmberdeepLevel
            {
                Map=new[]{
                    "  S    ",
                    "  S S  ",
                    ". . . .",
                    " F T . ",
                    ". . . .",
                    " F F T ",
                },
                // Playtest 2026-11-22: a first-time player lost here on the first try while still learning to aim, and the
                // levels after felt easier. It was the fastest burn (44 s) and the tightest limit (2) in the game. Now one
                // saved starting fire of three is enough, and the no-action loss (~58 s) stays under stage 1's (60 s).
                TreeCatch=13,TreeBurn=58,StartHeat=FireRun.PalmLessonHeat,BurnLimit=3,Par=35,
            },
            // ---- Stage 1, incident 0: the pump --------------------------------------------------------------------------
            // Four corner fires: four trees of water against a gem that holds a little over one. The gem runs dry on the
            // second corner and the pump glows; passive refill alone is too slow for the other two. Calm, slow spread,
            // the middle lanes open so the sheep run at once: nothing else to think about.
            new EmberdeepLevel
            {
                Map=new[]{
                    "      S      ",
                    "    S S S    ",
                    ". . . . . . .",
                    " F T T T T F ",
                    ". . . . . . .",
                    " F T T T T F ",
                },
                TreeCatch=16,TreeBurn=60,BurnLimit=4,Par=60,
            },
            // ---- Stage 2, incident 1: towers + the Plan phase -----------------------------------------------------------
            // The fire starts in the near grove; a far pocket behind a clearing (back right) is alight beside two sheep,
            // too far to fight at the same time. A tower set in the pocket during Plan rains on it the moment the fire
            // starts (it goes off at once: fire is already within its radius).
            new EmberdeepLevel
            {
                Map=new[]{
                    "    S S     S S",
                    ". . . . . . . .",
                    " T T T T . T F ",
                    ". . . . . . . .",
                    " T T T T . F T ",
                    ". . . . . . . .",
                    " F T F T . . . ",
                },
                TreeCatch=9,TreeBurn=62,BurnLimit=3,Par=60,
                Pieces=new FirePieceSet(1,0,false),
                TowerHints=new[]{new Vector2Int(6,1)},
            },
            // ---- Stage 3, incident 2: wind ------------------------------------------------------------------------------
            // Four fires along the near row and a strong wind blowing away from the player: each fire runs straight back
            // toward the cabin and the sheep behind it (the vane, the streaks and the smoke all point there). The one
            // tower is best on the two middle fires, the ones heading for the cabin. At 24 s the vane swings and the
            // wind turns to the right, along the rows.
            new EmberdeepLevel
            {
                Map=new[]{
                    "      S S S      ",
                    ". . . . . . . . .",
                    " T T T T H T T T ",
                    ". . . . . . . . .",
                    " T T T T T T T T ",
                    ". . . . . . . . .",
                    " F T F T T F T F ",
                },
                TreeCatch=8,TreeBurn=62,BurnLimit=5,Par=45,
                Wind=new FireWindPlan(0,1,24.25f,90,.8f),
                Pieces=new FirePieceSet(1,0,false),
                TowerHints=new[]{new Vector2Int(4,0)},
            },
            // ---- Stage 4, incident 3: grass + the excavator -------------------------------------------------------------
            // A dry meadow runs down the middle between the burning left grove and the cabin grove, its left edge already
            // alight, and a light wind blows toward the cabin. Fire races through grass (a patch every 3 s) and lights the
            // trees along it. A trench dug down the meadow's right edge, started in Plan, keeps the cabin side clean; the
            // gap in the near row closes the end the trench can't reach (there are no squares in front of the field). The
            // tower takes two of the four grove fires, never all of them (a clear needs every tree out; grass doesn't count).
            new EmberdeepLevel
            {
                Map=new[]{
                    "            S S  ",
                    ". . . g g g . . .",
                    " F T F G g T T T ",
                    ". . . g g g . . .",
                    " T T T g g T H T ",
                    ". . . g g g . . .",
                    " T T T g g T T T ",
                    ". . . g g g . . .",
                    " F T F G g . T T ",
                },
                TreeCatch=10,TreeBurn=68,BurnLimit=5,Par=105,
                Wind=new FireWindPlan(90,.4f),
                // Owner 2026-11-22 (store build): the excavator and the plane are cut. Its trench became a second tower on
                // the meadow's right edge, beside the cabin.
                Pieces=new FirePieceSet(2,0,false),
                TowerHints=new[]{new Vector2Int(2,1),new Vector2Int(5,1)},
            },
            // ---- Stage 5, incident 4: the plane, everything together ----------------------------------------------------
            // A wide front over the two near rows, a fire in each back corner beside the sheep, two cabins at the back,
            // grass, and a wind that blows the front toward the cabins, then swings right. Towers on the near corners
            // open two sheep lanes; the sheep that run charge the plane; a trench behind the front holds it off the
            // cabins; the plane drops along the front.
            new EmberdeepLevel
            {
                Map=new[]{
                    "    S       S    ",
                    "  S S   S   S S  ",
                    ". . . . . . . . .",
                    " F T H T T H T F ",
                    ". . . g . g . . .",
                    " T T T T T T T T ",
                    ". . g . . . g . .",
                    " T F T F F T F T ",
                    ". . . . . . . . .",
                    " F T F T T F T F ",
                },
                TreeCatch=6,TreeBurn=82,BurnLimit=7,Par=45,
                Wind=new FireWindPlan(0,.7f,35.25f,90,.8f),
                // Owner 2026-11-22 (store build): no excavator, no plane. The trench behind the front became a third tower
                // in the middle of that line. With 2 towers the bot loses (7/7 burnt at 100 s); with 3 it wins in ~14 s
                // (towers set in Plan rain out the starts at the bell), so the finale is easy: retune after the store build.
                Pieces=new FirePieceSet(3,0,false),
                TowerHints=new[]{new Vector2Int(1,0),new Vector2Int(7,0),new Vector2Int(4,1)},
            },
        };
        // Dev/test fixture (FireRun.LevelOverride): the generated compact field the board-sim, piece and layout tests were
        // written against before the levels were authored (workstreams L2, L3, O). Full grids of 3x1, 5x2, 6x3, 8x3
        // and 7x4 trees, fires every count/starts trees, grass on every other inner square, the old cabins, flocks,
        // winds, piece sets and per-incident clocks. Not used by the game.
        public static EmberdeepLevel GeneratedGrid(int stage)
        {
            if(stage<FireRun.PalmLessons)
            {
                // T's PALM RESCUE grove: 3 + 2 trees, three fires, three sheep.
                return new EmberdeepLevel{Map=new[]{"  S    ","  S S  ",". . . ."," F T . ",". . . ."," F F T "},
                    TreeCatch=10,TreeBurn=100/.8f,StartHeat=FireRun.PalmLessonHeat,BurnLimit=2,Par=25};
            }
            int i=Mathf.Clamp(stage-FireRun.PalmLessons,0,4);
            int[] columnsOf={3,5,6,8,7},rowsOf={1,2,3,3,4},startsOf={3,5,8,10,12},flocks={3,4,5,6,8},parity={-1,0,1,0,1};
            float[] pars={25,50,70,90,110};
            int columns=columnsOf[i],rows=rowsOf[i],count=columns*rows,starts=startsOf[i];
            var fire=new bool[count];for(int k=0;k<starts;k++)fire[k*count/starts]=true;
            int lanes=columns-1,sheepLines=(flocks[i]+lanes-1)/lanes;
            var map=new List<string>();
            for(int line=sheepLines-1;line>=0;line--)
            {
                var chars=new string(' ',2*columns+1).ToCharArray();
                for(int s=line*lanes;s<Mathf.Min(flocks[i],(line+1)*lanes);s++)chars[2*(1+s%lanes)]='S';
                map.Add(new string(chars));
            }
            for(int r=rows-1;r>=0;r--)
            {
                var squares=new string(' ',2*columns+1).ToCharArray();
                for(int g=0;g<=columns;g++)
                {
                    int c=g-1;bool grass=parity[i]>=0 && r+1<rows && c>=0 && c+1<columns && ((r+c)&1)==parity[i];
                    squares[2*g]=grass?'g':'.';
                }
                map.Add(new string(squares));
                var trees=new string(' ',2*columns+1).ToCharArray();
                for(int c=0;c<columns;c++)trees[2*c+1]=fire[r*columns+c]?'F':'T';
                map.Add(new string(trees));
            }
            var cabins=i==2?new[]{new Vector2(4.25f,4.15f)}:i==3?new[]{new Vector2(4.5f,6.75f)}:i==4?new[]{new Vector2(-4.7f,5.3f),new Vector2(4.7f,4.15f)}:Array.Empty<Vector2>();
            var winds=new[]{new FireWindPlan(0,0),new FireWindPlan(90,.45f),new FireWindPlan(0,.7f,30.25f,90,.7f),new FireWindPlan(270,.85f,26.25f,180,.85f),new FireWindPlan(180,1,22.25f,45,.9f)};
            var sets=new[]{new FirePieceSet(0,0,false),new FirePieceSet(1,0,false),new FirePieceSet(2,0,false),new FirePieceSet(2,1,false),new FirePieceSet(2,1,true)};
            return new EmberdeepLevel{Map=map.ToArray(),TreeCatch=Mathf.Max(1,10-i),TreeBurn=100/(.8f+.15f*i),BurnLimit=Mathf.Max(1,Mathf.CeilToInt(count*.3f)),
                Par=pars[i],Wind=winds[i],Pieces=sets[i],CabinSpots=cabins};
        }
        public static int Count=>Stages.Length;
        public static EmberdeepLevel For(int stage)=>Stages[Mathf.Clamp(stage,0,Stages.Length-1)];
        // FireRun's per-incident tables (FireRun.Winds, FireRun.PieceSets) read the incident stages.
        public static FireWindPlan[] IncidentWinds()
        {
            var winds=new FireWindPlan[Stages.Length-FireRun.PalmLessons];
            for(int i=0;i<winds.Length;i++)winds[i]=Stages[FireRun.PalmLessons+i].Wind;
            return winds;
        }
        public static FirePieceSet[] IncidentPieceSets()
        {
            var sets=new FirePieceSet[Stages.Length-FireRun.PalmLessons];
            for(int i=0;i<sets.Length;i++)sets[i]=Stages[FireRun.PalmLessons+i].Pieces;
            return sets;
        }
    }
}

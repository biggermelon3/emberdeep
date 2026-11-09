using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShapeOfMemory
{
    // What the player has done, kept on the device: solved levels, the most hints used per level and the table height.
    // Levels open in order; a solved level stays open, so after one full run every level is open.
    public sealed class MemoryProgress
    {
        public interface IStore{int Get(string key,int fallback);void Set(string key,int value);}
        // Saved at once, so taking the headset off mid-session keeps what was found.
        public sealed class Prefs:IStore
        {
            public int Get(string key,int fallback)=>PlayerPrefs.GetInt(key,fallback);
            public void Set(string key,int value){PlayerPrefs.SetInt(key,value);PlayerPrefs.Save();}
        }
        public sealed class InMemory:IStore
        {
            readonly Dictionary<string,int> values=new Dictionary<string,int>();
            public int Get(string key,int fallback)=>values.TryGetValue(key,out int value)?value:fallback;
            public void Set(string key,int value)=>values[key]=value;
        }
        // Play order: Birthday teaches on one wall, Home ends on two.
        public static readonly string[] Levels={"Birthday","BonVoyage","GoodMorning","Home"};
        public const int MaxHints=2;
        public const float MaxLift=.25f;
        const string Prefix="ShapeOfMemory.";
        readonly IStore store;
        public MemoryProgress(IStore store){this.store=store;}
        public int Count=>Levels.Length;
        public static int IndexOf(string id)=>Array.IndexOf(Levels,id);
        public bool Solved(int level)=>store.Get(Prefix+"Solved."+Levels[level],0)!=0;
        public void Solve(int level)=>store.Set(Prefix+"Solved."+Levels[level],1);
        public bool Unlocked(int level)=>level==0||Solved(level)||Solved(level-1);
        public int SolvedCount{get{int n=0;for(int i=0;i<Levels.Length;i++)if(Solved(i))n++;return n;}}
        // The first unsolved level, or -1 once every memory is found.
        public int Continue{get{for(int i=0;i<Levels.Length;i++)if(!Solved(i))return i;return -1;}}
        // A level's intro can be skipped once it has played through.
        public bool SeenIntro(int level)=>store.Get(Prefix+"Intro."+Levels[level],0)!=0;
        public void SawIntro(int level)=>store.Set(Prefix+"Intro."+Levels[level],1);
        // The ghost pinch stops for good once the player has rolled the orb.
        public bool Taught{get=>store.Get(Prefix+"Taught",0)!=0;set=>store.Set(Prefix+"Taught",value?1:0);}
        public int Hints(int level)=>store.Get(Prefix+"Hints."+Levels[level],0);
        public void UsedHints(int level,int stage){stage=Mathf.Clamp(stage,0,MaxHints);if(stage>Hints(level))store.Set(Prefix+"Hints."+Levels[level],stage);}
        // Table height above the standing default (metres; millimetres in the store).
        public float Lift
        {
            get=>store.Get(Prefix+"Lift",0)/1000f;
            set=>store.Set(Prefix+"Lift",Mathf.RoundToInt(Mathf.Clamp(value,-MaxLift,MaxLift)*1000));
        }
    }
}

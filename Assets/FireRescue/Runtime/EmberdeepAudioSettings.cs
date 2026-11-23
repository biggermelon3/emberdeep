using UnityEngine;

namespace FireRescue
{
    // LoopSpec §10: music and sound-effect volume, separately (0..1, PlayerPrefs). Every Emberdeep sound today is an
    // effect, so the effects volume is the listener volume. Music sources (none yet) call ApplyMusic: they ignore the
    // listener volume and take the music volume instead, so the two stay independent.
    public static class EmberdeepAudioSettings
    {
        public const string MusicKey="Emberdeep.MusicVolume",SfxKey="Emberdeep.SfxVolume";
        public const string LegacySoundKey="Emberdeep.Sound"; // the old on/off toggle (0 = off)
        public const float DefaultMusic=.8f,DefaultSfx=1,Step=.05f;
        public static event System.Action Changed;
        static float music=float.NaN,sfx=float.NaN;

        public static float Music{get{if(float.IsNaN(music))music=Clean(PlayerPrefs.GetFloat(MusicKey,DefaultMusic));return music;}}
        public static float Sfx
        {
            get
            {
                if(float.IsNaN(sfx))sfx=PlayerPrefs.HasKey(SfxKey)?Clean(PlayerPrefs.GetFloat(SfxKey,DefaultSfx))
                    :PlayerPrefs.GetInt(LegacySoundKey,1)==0?0:DefaultSfx;
                return sfx;
            }
        }
        public static void SetMusic(float volume)=>SetMusic(volume,true);
        public static void SetSfx(float volume)=>SetSfx(volume,true);
        // Slider drags (headset test 3, 2026-11-22): heard live, written to disk on release (Commit).
        public static void SetMusic(float volume,bool save){float before=Music;music=Clean(volume);Save(MusicKey,music,before,save);}
        public static void SetSfx(float volume,bool save){float before=Sfx;sfx=Clean(volume);Save(SfxKey,sfx,before,save);Apply();}
        public static void Commit()=>PlayerPrefs.Save();
        public static int Steps=>Mathf.RoundToInt(1/Step);
        public static void NudgeMusic(int steps)=>SetMusic(Music+steps*Step*2);
        public static void NudgeSfx(int steps)=>SetSfx(Sfx+steps*Step*2);
        // Effects: everything that plays through the listener, times the duck (the camp spirit's voice ignores the listener
        // volume, takes Sfx itself, and ducks the rest while it speaks: EmberdeepForestSpirit).
        public static void Apply()=>AudioListener.volume=Sfx*duck;
        static float duck=1;
        public static float Ducking=>duck;
        public static void SetDuck(float value)
        {
            value=float.IsNaN(value)?1:Mathf.Clamp01(value);
            if(value==duck)return;
            duck=value;Apply();
        }
        // Music: call once per music source and again on Changed.
        public static void ApplyMusic(AudioSource source,float authoredVolume=1)
        {if(!source)return;source.ignoreListenerVolume=true;source.volume=authoredVolume*Music;}
        // Tests and domain reloads re-read the stored values.
        public static void Reload(){music=sfx=float.NaN;}

        static float Clean(float volume)=>float.IsNaN(volume) || float.IsInfinity(volume)?0:Mathf.Clamp01(Mathf.Round(volume/Step)*Step);
        static void Save(string key,float value,float before,bool save=true)
        {
            PlayerPrefs.SetFloat(key,value);if(save)PlayerPrefs.Save();
            if(!Mathf.Approximately(before,value))Changed?.Invoke();
        }
    }
}

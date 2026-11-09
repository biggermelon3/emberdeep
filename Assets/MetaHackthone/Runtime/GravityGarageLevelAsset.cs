using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace PhysicalAssembly
{
    [Serializable]
    public sealed class GravityGarage25DParameters
    {
        public float frontWheel=.37f, rearWheel=.20f, upperDrop=2f, rockPos=6.4f, rockSize=.21f, gap=1.35f;
        public float lowerDrop=1.2f, rock2Pos=10.8f, rock2Size=.12f, cameraAngle=18f, zoom=.94f;
        public bool rock2Enabled=true, showLabels=true;
        public GravityGarage25DParameters Copy() => JsonUtility.FromJson<GravityGarage25DParameters>(JsonUtility.ToJson(this));
        public void Validate()
        {
            void Check(float v,float min,float max,string name) { if(!float.IsFinite(v)||v<min||v>max) throw new InvalidDataException(name+" is outside its supported range."); }
            Check(frontWheel,.16f,.44f,nameof(frontWheel)); Check(rearWheel,.16f,.44f,nameof(rearWheel));
            Check(upperDrop,.8f,2.8f,nameof(upperDrop)); Check(rockPos,2.8f,15.2f,nameof(rockPos)); Check(rockSize,.02f,.40f,nameof(rockSize)); Check(gap,.2f,2.6f,nameof(gap));
            Check(lowerDrop,.4f,1.7f,nameof(lowerDrop)); Check(rock2Pos,2,15.9f,nameof(rock2Pos)); Check(rock2Size,.02f,.33f,nameof(rock2Size));
            Check(cameraAngle,-34,34,nameof(cameraAngle)); Check(zoom,.72f,1.23f,nameof(zoom));
        }
        // Keep the standard mixed-wheel car; tune only the course for its low rear clearance.
        public static GravityGarage25DParameters UnityCalibrated() => new(){upperDrop=2.4f,rockPos=12f,rockSize=.02f,gap=.4f,lowerDrop=1.4f,rock2Size=.02f};
        public void Challenge() { upperDrop=2.5f; rockSize=.4f; rockPos=6.4f; gap=1.65f; rock2Size=.12f; lowerDrop=1.2f; }
        public void Preset(int index) { frontWheel=index==1||index==3?.20f:.37f; rearWheel=index==1||index==2?.37f:.20f; }
        public string PresetName => frontWheel==.37f&&rearWheel==.20f?"Big front":frontWheel==.20f&&rearWheel==.37f?"Small front":frontWheel==.37f&&rearWheel==.37f?"Both big":frontWheel==.20f&&rearWheel==.20f?"Both small":"Custom";
        static float Smooth(float t) { t=Mathf.Clamp01(t); return t*t*(3-2*t); }
        public float LandingX => 9.55f+gap;
        public float SupportedRockX { get { float left=9.05f,right=LandingX+.65f; return rockPos>left&&rockPos<right?(rockPos-left<right-rockPos?left:right):rockPos; } }
        public float Upper(float x)
        {
            float down=5.45f-upperDrop;
            if(x<=1.7f)return 5.45f;
            if(x<=5.15f)return Mathf.Lerp(5.45f,down,Smooth((x-1.7f)/3.45f));
            if(x<=6.95f)return down;
            // The shared block car has low rear clearance. A shallow, tangent-continuous
            // launch approach avoids grounding its rear beam in the HTML's deep trough.
            if(x<=8.60f)return Mathf.Lerp(down,down-.12f,Smooth((x-6.95f)/1.65f));
            if(x<=9.55f)return Mathf.Lerp(down-.12f,down-.03f,Smooth((x-8.6f)/.95f));
            if(x<LandingX)return float.NaN;
            return Mathf.Lerp(down-.18f,down-.49f,Smooth((x-LandingX)/(16.35f-LandingX)));
        }
        public float Lower(float x)
        {
            float low=2.3f-lowerDrop;
            if(x<=2.5f)return 2.3f;
            if(x<=7.6f)return Mathf.Lerp(2.3f,low,Smooth((x-2.5f)/5.1f));
            if(x<=10.5f)return low+.15f*Smooth((x-7.6f)/2.9f);
            if(x<=13)return low+.15f-.23f*Smooth((x-10.5f)/2.5f);
            return Mathf.Lerp(low-.08f,low-.34f,Smooth((x-13)/4.35f));
        }
    }
    [CreateAssetMenu(menuName="MetaHackthone/Gravity Garage 2.5D Level")]
    public sealed class GravityGarageLevelAsset : ScriptableObject
    {
        public int schemaVersion=1;
        public Texture2D thumbnail;
        public GravityGarage25DParameters parameters=new();
        [Serializable] sealed class Envelope { public int schemaVersion=1; public GravityGarage25DParameters parameters; }
        public static string Export(GravityGarage25DParameters p) { p.Validate(); return JsonUtility.ToJson(new Envelope{parameters=p},true); }
        public static GravityGarage25DParameters Import(string json)
        {
            if(string.IsNullOrWhiteSpace(json)||json.Length>16384)throw new InvalidDataException("Empty or oversized level JSON.");
            foreach(var key in new[]{"schemaVersion","parameters","frontWheel","rearWheel","upperDrop","rockPos","rockSize","gap","lowerDrop","rock2Enabled","rock2Pos","rock2Size","cameraAngle","zoom","showLabels"})
                if(Regex.Matches(json,"\""+key+"\"\\s*:").Count!=1)throw new InvalidDataException("Missing or duplicate field: "+key);
            if(!Regex.IsMatch(json,"\"schemaVersion\"\\s*:\\s*1(?=\\s*[,}])"))throw new InvalidDataException("Unsupported level schema.");
            foreach(var field in typeof(GravityGarage25DParameters).GetFields()) {
                string literal=field.FieldType==typeof(bool)?"(?:true|false)":"-?(?:0|[1-9]\\d*)(?:\\.\\d+)?(?:[eE][+-]?\\d+)?";
                if(!Regex.IsMatch(json,"\""+field.Name+"\"\\s*:\\s*"+literal+"(?=\\s*[,}])"))throw new InvalidDataException("Invalid value type: "+field.Name);
            }
            var data=JsonUtility.FromJson<Envelope>(json);
            if(data==null||data.schemaVersion!=1||data.parameters==null)throw new InvalidDataException("Unsupported level schema.");
            data.parameters.Validate(); return data.parameters.Copy();
        }
    }
}

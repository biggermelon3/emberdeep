using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Localization.Settings;

namespace FireRescue
{
    // Adapter for the project's existing procedural TextMesh UI. Locale ownership,
    // table loading and font loading all belong to Unity Localization. Runtime
    // numeric strings keep their values; longest phrase matching handles names
    // embedded in HUD strings without modifying gameplay state or button commands.
    [DefaultExecutionOrder(10000)]
    public sealed class EmberdeepLocalizedText : MonoBehaviour
    {
        sealed class Label {public TextMesh Text;public string Source,Rendered;public int Revision=-1;}
        readonly Dictionary<TextMesh,Label> labels=new();
        readonly List<TextMesh> found=new();
        readonly Dictionary<string,string> phrases=new(System.StringComparer.OrdinalIgnoreCase);
        Regex expression;
        Font font;
        float scanAt;
        int revision;
        Coroutine loading;
        AsyncOperationHandle<Font> fontHandle;
        public bool Ready {get;private set;}
        public string LocaleCode=>LocalizationSettings.SelectedLocale?LocalizationSettings.SelectedLocale.Identifier.Code:"";
        IEnumerator Start()
        {
            yield return LocalizationSettings.InitializationOperation;
            LocalizationSettings.SelectedLocaleChanged+=ChangeLocale;
            ChangeLocale(LocalizationSettings.SelectedLocale);
        }
        void ChangeLocale(Locale locale)
        {if(loading!=null)StopCoroutine(loading);loading=StartCoroutine(Load(locale));}
        IEnumerator Load(Locale locale)
        {
            Ready=false;
            if(fontHandle.IsValid())Addressables.Release(fontHandle);
            var english=LocalizationSettings.StringDatabase.GetTableAsync("EmberdeepUI",LocalizationSettings.AvailableLocales.GetLocale("en"));
            var translated=LocalizationSettings.StringDatabase.GetTableAsync("EmberdeepUI",locale);
            var asset=LocalizationSettings.AssetDatabase.GetLocalizedAssetAsync<Font>("EmberdeepFonts","ui.font",locale);
            fontHandle=Addressables.ResourceManager.Acquire(asset);
            yield return english;yield return translated;yield return fontHandle;
            if(!english.Result || !translated.Result || !fontHandle.Result){Debug.LogError("Emberdeep localization tables or font missing");yield break;}
            phrases.Clear();
            if(locale.Identifier.Code!="en")foreach(var e in english.Result.Values)
            {
                var entry=translated.Result.GetEntry(e.KeyId);
                if(entry!=null && !string.IsNullOrEmpty(e.LocalizedValue))phrases[e.LocalizedValue]=entry.LocalizedValue;
            }
            expression=phrases.Count==0?null:new Regex("(?<![A-Za-z])(?:"+string.Join("|",phrases.Keys.OrderByDescending(k=>k.Length).Select(Regex.Escape))+")(?![A-Za-z])",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
            font=fontHandle.Result;revision++;Ready=true;RefreshLabels();
        }
        public string Translate(string value)=>string.IsNullOrEmpty(value) || expression==null?value:expression.Replace(value,m=>phrases[m.Value]);
        void LateUpdate(){if(Ready)RefreshLabels(false);}
        public void RefreshLabels(bool rescan=true)
        {
            if(!Ready)return;
            if(rescan || Time.unscaledTime>=scanAt)
            {
                found.Clear();GetComponentsInChildren(true,found);
                foreach(var text in found)if(!labels.ContainsKey(text))labels.Add(text,new Label{Text=text});
                foreach(var key in labels.Keys.Where(k=>!k).ToArray())labels.Remove(key);
                scanAt=Time.unscaledTime+.25f;
            }
            foreach(var label in labels.Values)
            {
                var text=label.Text;if(!text)continue;
                if(text.text!=label.Rendered)label.Source=text.text;
                if(label.Revision==revision && text.text==label.Rendered)continue;
                label.Revision=revision;label.Rendered=Translate(label.Source);
                text.font=font;text.GetComponent<MeshRenderer>().sharedMaterial=font.material;
                font.RequestCharactersInTexture(label.Rendered,text.fontSize,text.fontStyle);
                text.text=label.Rendered;
            }
        }
        void OnDestroy(){LocalizationSettings.SelectedLocaleChanged-=ChangeLocale;if(fontHandle.IsValid())Addressables.Release(fontHandle);}
    }
}

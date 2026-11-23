using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace FireRescue.Editor
{
    public static class EmberdeepLocalizationBuilder
    {
        const string Root="Assets/FireRescue/Localization";
        // Batch entry point: Unity.exe -executeMethod FireRescue.Editor.EmberdeepLocalizationBuilder.RebuildTables
        public static void RebuildTables()=>Debug.Log("Emberdeep localization rows: "+BuildTables());
        public static int BuildTables()
        {
            var settings=LocalizationEditorSettings.ActiveLocalizationSettings;
            if(!settings)
            {
                settings=ScriptableObject.CreateInstance<LocalizationSettings>();
                AssetDatabase.CreateAsset(settings,Root+"/EmberdeepLocalization.asset");
                LocalizationEditorSettings.ActiveLocalizationSettings=settings;
            }
            foreach(var code in new[]{"en","zh-Hans"})
            {
                if(LocalizationEditorSettings.GetLocales().Any(l=>l.Identifier.Code==code))continue;
                var locale=Locale.CreateLocale(code);AssetDatabase.CreateAsset(locale,Root+"/"+code+".asset");LocalizationEditorSettings.AddLocale(locale);
            }
            settings.GetStartupLocaleSelectors().Clear();settings.GetStartupLocaleSelectors().Add(new EmberdeepSystemLocaleSelector());
            EditorUtility.SetDirty(settings);
            Directory.CreateDirectory(Root+"/Tables");AssetDatabase.Refresh();
            var collection=LocalizationEditorSettings.GetStringTableCollection("EmberdeepUI")
                ??LocalizationEditorSettings.CreateStringTableCollection("EmberdeepUI",Root+"/Tables");
            int count=0;var keys=new System.Collections.Generic.HashSet<string>();
            foreach(var line in File.ReadAllLines(Root+"/Translations.tsv"))
            {
                if(string.IsNullOrWhiteSpace(line))continue;
                var values=line.Split('\t');if(values.Length!=2)throw new InvalidOperationException("Invalid translation row: "+line);
                string en=values[0].Replace("\\n","\n"),zh=values[1].Replace("\\n","\n");
                string key="ui."+BitConverter.ToString(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(en))).Replace("-","").Substring(0,16).ToLowerInvariant();
                foreach(var table in collection.StringTables)
                {
                    var entry=table.GetEntry(key)??table.AddEntry(key,"");entry.Value=table.LocaleIdentifier.Code=="zh-Hans"?zh:en;
                    EditorUtility.SetDirty(table);
                }
                keys.Add(key);count++;
            }
            // The TSV is the source of truth: entries whose row was removed or rewritten leave the tables too, so a stale
            // English phrase can no longer match at runtime.
            foreach(var stale in collection.SharedData.Entries.Select(e=>e.Key).Where(k=>!keys.Contains(k)).ToArray())
                collection.RemoveEntry(stale);
            var fonts=LocalizationEditorSettings.GetAssetTableCollection("EmberdeepFonts")
                ??LocalizationEditorSettings.CreateAssetTableCollection("EmberdeepFonts",Root+"/Tables");
            foreach(var table in fonts.AssetTables)
            {
                string path=Root+"/Fonts/"+(table.LocaleIdentifier.Code=="zh-Hans"?"NotoSansCJKsc-Regular.otf":"LiberationSans.ttf");
                fonts.AddAssetToTable(table,"ui.font",AssetDatabase.LoadAssetAtPath<Font>(path));
                EditorUtility.SetDirty(table);
            }
            EditorUtility.SetDirty(collection);EditorUtility.SetDirty(collection.SharedData);
            EditorUtility.SetDirty(fonts);EditorUtility.SetDirty(fonts.SharedData);
            LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null,collection);
            LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null,fonts);
            AssetDatabase.SaveAssets();return count;
        }
    }
}

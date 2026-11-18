using System;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace FireRescue
{
    [Serializable]
    public sealed class EmberdeepSystemLocaleSelector : IStartupLocaleSelector
    {
        public static string Code(SystemLanguage language)=>language==SystemLanguage.Chinese || language==SystemLanguage.ChineseSimplified
            || language==SystemLanguage.ChineseTraditional?"zh-Hans":"en";
        public Locale GetStartupLocale(ILocalesProvider locales)=>locales.GetLocale(new LocaleIdentifier(Code(Application.systemLanguage)));
    }
}

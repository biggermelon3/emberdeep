using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using Debug=UnityEngine.Debug;

namespace FireRescue.Editor
{
    // One-click Emberdeep APKs in Builds/, like the other games here (Builds/<Game>-Pico.apk, Builds/<Game>-Quest.apk).
    // Same two scenes, package id, version, icons, render pipeline, localization content and PICO swap as the
    // Deliverables showcase (FireRescueBuilder.BuildShowcase), but incremental, optionally development, and with no
    // manifest or Deliverables files. Quest stays the project's resting Android configuration; a PICO build prepares
    // PICO (PicoBuild.Prepare) and puts Quest back afterwards, even when the build fails.
    // Command line, editor closed (-pico and/or -quest; optional -development, -install):
    //   Unity.exe -batchmode -quit -projectPath . -buildTarget Android -executeMethod FireRescue.Editor.EmberdeepQuickBuild.BuildFromCommandLine -pico
    public static class EmberdeepQuickBuild
    {
        public const string PicoApk="Builds/Emberdeep-Pico.apk",QuestApk="Builds/Emberdeep-Quest.apk";
        const string Menu="Emberdeep/";

        [MenuItem(Menu+"Build PICO APK",false,100)] static void PicoMenu()=>Finish(true,Build(true,false),false);
        [MenuItem(Menu+"Build PICO APK (development)",false,101)] static void PicoDevelopmentMenu()=>Finish(true,Build(true,true),false);
        [MenuItem(Menu+"Build PICO APK and run",false,102)] static void PicoRunMenu()=>Finish(true,Build(true,false),true);
        [MenuItem(Menu+"Install last PICO APK and run",false,103)] static void PicoInstallMenu()=>InstallAndLaunch(true);

        [MenuItem(Menu+"Build Quest APK",false,120)] static void QuestMenu()=>Finish(false,Build(false,false),false);
        [MenuItem(Menu+"Build Quest APK (development)",false,121)] static void QuestDevelopmentMenu()=>Finish(false,Build(false,true),false);
        [MenuItem(Menu+"Build Quest APK and run",false,122)] static void QuestRunMenu()=>Finish(false,Build(false,false),true);
        [MenuItem(Menu+"Install last Quest APK and run",false,123)] static void QuestInstallMenu()=>InstallAndLaunch(false);

        [MenuItem(Menu+"Build PICO + Quest APKs",false,140)]
        static void BothMenu()
        {
            Build(true,false);Build(false,false);
            EditorUtility.RevealInFinder(Path.GetFullPath(PicoApk));
        }

        public static string Apk(bool pico)=>pico?PicoApk:QuestApk;

        // Throws (with the build result) when the build fails, so the menu shows the error.
        public static BuildReport Build(bool pico,bool development)=>FireRescueBuilder.BuildShowcase(pico,Apk(pico),development,false);

        public static void BuildFromCommandLine()
        {
            var args=Environment.GetCommandLineArgs();
            bool pico=args.Contains("-pico"),quest=args.Contains("-quest"),development=args.Contains("-development");
            if(!pico && !quest)throw new ArgumentException("Pass -pico and/or -quest.");
            if(pico)Build(true,development);
            if(quest)Build(false,development);
            if(args.Contains("-install"))
            {
                if(pico)InstallAndLaunch(true);
                if(quest)InstallAndLaunch(false);
            }
        }

        static void Finish(bool pico,BuildReport report,bool run)
        {
            if(report.summary.result!=BuildResult.Succeeded || !File.Exists(Apk(pico)))return;
            if(run)InstallAndLaunch(pico);else EditorUtility.RevealInFinder(Path.GetFullPath(Apk(pico)));
        }

        // Installs on every connected headset of that platform (other devices are left alone), then launches.
        // A headset that is not being worn keeps the app paused until it is put on.
        public static void InstallAndLaunch(bool pico)
        {
            string apk=Apk(pico),platform=pico?"PICO":"Quest",id=FireRescueBuilder.ShowcasePackageId;
            if(!File.Exists(apk))throw new FileNotFoundException("Build the Emberdeep "+platform+" APK first.",apk);
            var serials=Devices().Where(s=>pico?IsPico(s):IsQuest(s)).ToArray();
            if(serials.Length==0){Debug.LogWarning("EMBERDEEP_INSTALL platform="+platform+" no "+platform+" headset among the connected adb devices.");return;}
            foreach(var serial in serials)
            {
                try
                {
                    EditorUtility.DisplayProgressBar("Emberdeep","Installing on "+platform+" "+serial+"...",.5f);
                    string install=Adb("-s "+serial+" install -r \""+Path.GetFullPath(apk)+"\"",300).Trim();
                    bool ok=install.Contains("Success");
                    Debug.Log("EMBERDEEP_INSTALL platform="+platform+" device="+serial+" ok="+ok+" "+install);
                    if(!ok)
                    {
                        if(install.Contains("INSTALL_FAILED_UPDATE_INCOMPATIBLE"))
                            Debug.LogWarning("A differently signed Emberdeep is installed on "+serial+"; uninstall it first (adb -s "+serial+" uninstall "+id+"). That clears its saved progress.");
                        continue;
                    }
                    Adb("-s "+serial+" shell monkey -p "+id+" -c android.intent.category.LAUNCHER 1",30);
                    Debug.Log("EMBERDEEP_LAUNCHED platform="+platform+" device="+serial);
                }
                finally { EditorUtility.ClearProgressBar(); }
            }
        }

        static bool IsPico(string serial)=>Maker(serial).IndexOf("pico",StringComparison.OrdinalIgnoreCase)>=0;
        // Quest 2 reports manufacturer and brand "Oculus"; later headsets may report "Meta".
        static bool IsQuest(string serial)
        {
            var maker=Maker(serial);
            return maker.IndexOf("oculus",StringComparison.OrdinalIgnoreCase)>=0 || maker.IndexOf("meta",StringComparison.OrdinalIgnoreCase)>=0;
        }
        static string Maker(string serial)=>Adb("-s "+serial+" shell getprop ro.product.manufacturer",15)+" "+Adb("-s "+serial+" shell getprop ro.product.brand",15);
        static string[] Devices()=>Adb("devices",15).Split('\n').Select(l=>l.Trim().Split('\t')).Where(p=>p.Length>=2 && p[1]=="device").Select(p=>p[0]).ToArray();

        // The editor's own adb: a second adb (the one on PATH) would restart the server under the editor.
        static string Adb(string arguments,int seconds)
        {
            string sdk=UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath,adb=Path.Combine(sdk??"","platform-tools","adb.exe");
            if(!File.Exists(adb))adb="adb";
            try
            {
                using(var process=Process.Start(new ProcessStartInfo(adb,arguments){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true}))
                {
                    var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
                    if(!process.WaitForExit(seconds*1000)){process.Kill();return "adb timed out after "+seconds+" s";}
                    return output.Result+error.Result;
                }
            }
            catch(Exception e){Debug.LogWarning("adb "+arguments+" failed: "+e.Message);return "";}
        }
    }
}

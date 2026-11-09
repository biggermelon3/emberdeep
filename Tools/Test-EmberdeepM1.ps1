param(
    [string]$ProjectPath = (Join-Path $PSScriptRoot '..'),
    [string]$OutputDirectory = '',
    [ValidateRange(30,3600)][int]$TimeoutSeconds = 600
)
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath($ProjectPath)
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $project ('Logs/EmberdeepM1/TestRuns/' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
}
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$utf8 = New-Object Text.UTF8Encoding($false)
$snapshot = Join-Path $output 'EditorSnapshot.json'
if (Test-Path -LiteralPath $snapshot) { throw "Use a fresh output directory; snapshot already exists: $snapshot" }

function Invoke-Unity {
    param([string[]]$Arguments,[string]$Record,[switch]$AllowUnavailable)
    $errorFile = Join-Path $output 'cli-stderr.txt'
    $raw = (& unity @Arguments 2> $errorFile | Out-String)
    $exitCode = $LASTEXITCODE
    if ($Record) { [IO.File]::WriteAllText((Join-Path $output $Record),$raw,$utf8) }
    if ($exitCode -ne 0) {
        if ($AllowUnavailable) { return $null }
        throw "Unity CLI failed ($exitCode): $raw See $errorFile"
    }
    try { $result = $raw | ConvertFrom-Json }
    catch { throw "Unity CLI returned non-JSON output: $raw" }
    if ($result.PSObject.Properties.Name -contains 'success' -and $result.success -eq $false) {
        if ($AllowUnavailable) { return $null }
        throw "Unity command failed: $raw"
    }
    return $result
}

$common = @('--project-path',$project,'--caller','plugin','--skill','unity-cli','--result-only','--format','json')
function Invoke-EvalFile {
    param([string]$Name,[string]$Code)
    $file = Join-Path $output ($Name + '.cs')
    [IO.File]::WriteAllText($file,$Code,$utf8)
    Invoke-Unity -Arguments (@('command','script','eval','--file',$file) + $common) -Record ($Name + '.json') | Out-Null
}

function Invoke-TestSuite {
    param([string]$Mode,[string]$Filter,[string]$Name)
    Invoke-Unity -Arguments (@('command','test','run','--mode',$Mode,'--filter',$Filter,'--async-tests','true') + $common) -Record ($Name + '-request.json') | Out-Null
    $script:testRunning = $true
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Seconds 2
        $state = Invoke-Unity -Arguments (@('command','test','status') + $common) -AllowUnavailable
        if ($null -eq $state) { continue } # Domain reload may briefly close the local server.
        if ($state.status -in @('completed','failed','cancelled','error')) {
            $script:testRunning = $false
            [IO.File]::WriteAllText((Join-Path $output ($Name + '-results.json')),($state | ConvertTo-Json -Depth 50),$utf8)
            if ($state.status -ne 'completed' -or $state.summary.total -le 0 -or $state.summary.failed -gt 0 -or $state.summary.inconclusive -gt 0) {
                throw "$Name did not pass. See $output/$Name-results.json"
            }
            Write-Output "$Name passed: $($state.summary.passed)/$($state.summary.total)"
            return
        }
    }
    throw "$Name timed out after $TimeoutSeconds seconds"
}

# Live Editor only. This runner does not open/close Unity, regenerate scenes, or build APKs.
# The initial eval rejects unsaved work, snapshots the original scene setup and list,
# and temporarily installs only the standalone camp/fireground scenes for PlayMode.
$snapshotCs = '@"' + $snapshot.Replace('"','""') + '"'
$prepare = @'
using System.Linq;
if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode || UnityEditor.EditorApplication.isCompiling || UnityEditor.BuildPipeline.isBuildingPlayer)
    throw new System.InvalidOperationException("Stop Play mode and wait for compilation/builds before running Emberdeep QA.");
for (int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++) {
    var scene=UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
    if (scene.isDirty || (string.IsNullOrEmpty(scene.path) && scene.rootCount>0))
        throw new System.InvalidOperationException("Save open scenes before running Emberdeep QA: "+scene.name);
}
foreach (var path in FireRescue.Editor.FireRescueBuilder.ShowcaseScenes)
    if (!UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(path))
        throw new System.InvalidOperationException("Missing standalone scene: "+path);
var record=new {
    scenes=UnityEditor.EditorBuildSettings.scenes.Select(s=>new {path=s.path,enabled=s.enabled}).ToArray(),
    setup=UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup(),
    runInBackground=UnityEngine.Application.runInBackground
};
System.IO.File.WriteAllText(SNAPSHOT,Newtonsoft.Json.JsonConvert.SerializeObject(record,Newtonsoft.Json.Formatting.Indented));
UnityEditor.EditorBuildSettings.scenes=FireRescue.Editor.FireRescueBuilder.ShowcaseScenes.Select(p=>new UnityEditor.EditorBuildSettingsScene(p,true)).ToArray();
UnityEngine.Application.runInBackground=true;
UnityEditor.AssetDatabase.SaveAssets();
return new {prepared=true,scenes=FireRescue.Editor.FireRescueBuilder.ShowcaseScenes};
'@
$restore = @'
using System.Linq;
if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode || UnityEditor.BuildPipeline.isBuildingPlayer)
    throw new System.InvalidOperationException("Editor must be idle to restore the QA snapshot.");
var record=Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(SNAPSHOT));
UnityEditor.EditorBuildSettings.scenes=record["scenes"].Select(s=>new UnityEditor.EditorBuildSettingsScene((string)s["path"],(bool)s["enabled"])).ToArray();
var setup=record["setup"].ToObject<UnityEditor.SceneManagement.SceneSetup[]>();
if (setup.Length>0 && setup.All(s=>!string.IsNullOrEmpty(s.path))) UnityEditor.SceneManagement.EditorSceneManager.RestoreSceneManagerSetup(setup);
else UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
UnityEngine.Application.runInBackground=(bool)record["runInBackground"];
UnityEditor.AssetDatabase.SaveAssets();
return new {restored=true,scenes=UnityEditor.EditorBuildSettings.scenes.Select(s=>new {path=s.path,enabled=s.enabled}).ToArray()};
'@
$testRunning = $false
Push-Location $project
try {
    Invoke-Unity -Arguments @('status','--project-path',$project,'--format','json') -Record 'Editor-status.json' | Out-Null
    Copy-Item -LiteralPath (Join-Path $project 'ProjectSettings/EditorBuildSettings.asset') -Destination (Join-Path $output 'EditorBuildSettings.before')
    Invoke-EvalFile 'Prepare' ($prepare.Replace('SNAPSHOT',$snapshotCs))
    Invoke-TestSuite 'editor' 'FireRescue.Tests' 'EditMode'
    # Emberdeep selects the camp/menu/standalone campaign and water regressions;
    # legacy FireRescuePlayTests require shared-game scenes and are intentionally separate.
    Invoke-TestSuite 'playmode' 'Emberdeep' 'PlayMode'
}
finally {
    try {
        if ($testRunning) {
            Invoke-Unity -Arguments (@('command','test','cancel') + $common) -Record 'Cancel.json' -AllowUnavailable | Out-Null
        }
        if (Test-Path -LiteralPath $snapshot) {
            # Test cancellation and domain reload may briefly leave the Editor busy.
            # Restore's first check is read-only, so retrying cannot partly apply it.
            $restored=$false
            for($attempt=0;$attempt -lt 10 -and -not $restored;$attempt++) {
                try { Invoke-EvalFile 'Restore' ($restore.Replace('SNAPSHOT',$snapshotCs)); $restored=$true }
                catch { if($attempt -eq 9){throw}; Start-Sleep -Seconds 2 }
            }
            Copy-Item -LiteralPath (Join-Path $project 'ProjectSettings/EditorBuildSettings.asset') -Destination (Join-Path $output 'EditorBuildSettings.after')
            $beforeHash=(Get-FileHash -LiteralPath (Join-Path $output 'EditorBuildSettings.before') -Algorithm SHA256).Hash
            $afterHash=(Get-FileHash -LiteralPath (Join-Path $output 'EditorBuildSettings.after') -Algorithm SHA256).Hash
            if ($beforeHash -ne $afterHash) { Write-Warning 'Scene list restored through Unity API; serialized settings bytes changed. Inspect before/after and Restore.json.' }
        }
    }
    finally { Pop-Location }
}
Write-Output "Evidence: $output"

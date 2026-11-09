param(
    [Parameter(Mandatory=$true)][string]$Serial,
    [string]$Adb='G:\UnityProject\Unity\6000.3.21f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe',
    [string]$OutputDirectory=(Join-Path $PSScriptRoot '..\Logs\QuestSandbox')
)
$ErrorActionPreference='Stop'
$package='com.metahackathon2026.metahackthone'
$outputPath=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$activity=(& $Adb -s $Serial shell cmd package resolve-activity --brief $package | Select-Object -Last 1).Trim()
if($LASTEXITCODE -ne 0 -or $activity -notlike "$package/*") { throw 'Installed application activity was not found.' }
$captures=@{}
$logText=''
try {
    & metavr.cmd -d $Serial device proximity --disable --duration-ms 180000
    if($LASTEXITCODE -ne 0) { throw 'Could not temporarily keep the Quest awake.' }
    & metavr.cmd -d $Serial device wake
    & $Adb -s $Serial shell am force-stop $package
    & $Adb -s $Serial shell am start -a android.intent.action.MAIN -c android.intent.category.LAUNCHER -n $activity --ez metahackthone.sandbox_smoke_test true
    if($LASTEXITCODE -ne 0) { throw 'Could not launch the smoke test.' }
    $timer=[Diagnostics.Stopwatch]::StartNew()
    $appProcessId=''
    while(!$appProcessId -and $timer.Elapsed.TotalSeconds -lt 15) {
        $appProcessId=(& $Adb -s $Serial shell pidof $package | Out-String).Trim()
        if(!$appProcessId) { Start-Sleep -Milliseconds 500 }
    }
    if(!$appProcessId) { throw 'Application did not start.' }
    while($timer.Elapsed.TotalSeconds -lt 150) {
        $logText=(& $Adb -s $Serial logcat -d -v threadtime "--pid=$appProcessId" -s Unity:V | Out-String)
        foreach($match in [regex]::Matches($logText,'QUEST_SANDBOX_SMOKE CAPTURE ([A-Za-z-]+)')) {
            $stage=$match.Groups[1].Value
            if($captures.ContainsKey($stage)) { continue }
            $file=Join-Path $outputPath ($stage+'.png')
            & metavr.cmd -d $Serial capture screenshot --method screencap --output $file
            if($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $file)) { throw "Capture failed: $stage" }
            $captures[$stage]=$file
            Write-Output "Captured $stage"
        }
        if($logText -match 'QUEST_SANDBOX_SMOKE FAIL') { throw 'Device smoke test failed. See device.log.' }
        if($logText -match 'QUEST_SANDBOX_SMOKE PASS') {
            foreach($stage in @('main-menu','Workbench-build','Workbench-simulation','OnSandbox-build','OnSandbox-simulation')) {
                if(!$captures.ContainsKey($stage)) { throw "Missing screenshot: $stage" }
            }
            [pscustomobject]@{
                Result='Passed'; Device=$Serial; Package=$package; Captures=$captures
                PhysicalHandTrackingTested=$false; Log='device.log'
            } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $outputPath 'result.json') -Encoding UTF8
            Write-Output 'Quest Sandbox smoke test passed. Review the five stereo captures.'
            exit 0
        }
        Start-Sleep -Seconds 1
    }
    throw 'Device smoke test timed out (requires the development APK with the opt-in test).'
}
finally {
    $logText | Set-Content -LiteralPath (Join-Path $outputPath 'device.log') -Encoding UTF8
    & metavr.cmd -d $Serial device proximity --enable
}

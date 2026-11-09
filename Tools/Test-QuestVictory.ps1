param(
    [Parameter(Mandatory=$true)][string]$Serial,
    [string]$Adb='adb',
    [string]$MetaCli='C:\Program Files\Meta VR Developer Hub\resources\bin\metavr.exe',
    [string]$OutputDirectory
)
$ErrorActionPreference='Stop'
if(!$OutputDirectory) { $OutputDirectory=Join-Path $PSScriptRoot '..\Logs\PalmVictory20261105\Device' }
$package='com.metahackathon2026.metahackthone'
$outputPath=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$activity=(& $Adb -s $Serial shell cmd package resolve-activity --brief $package | Select-Object -Last 1).Trim()
if($LASTEXITCODE -ne 0 -or $activity -notlike "$package/*") { throw 'Installed activity not found.' }
$captures=@{}
$logText=''
try {
    & $MetaCli -d $Serial device proximity --disable --duration-ms 120000
    if($LASTEXITCODE -ne 0) { throw 'Cannot temporarily keep Quest awake.' }
    & $MetaCli -d $Serial device wake
    & $Adb -s $Serial shell am force-stop $package
    & $Adb -s $Serial shell am start -a android.intent.action.MAIN -c android.intent.category.LAUNCHER -n $activity --ez metahackthone.victory_test true
    if($LASTEXITCODE -ne 0) { throw 'Cannot launch acceptance test.' }
    $timer=[Diagnostics.Stopwatch]::StartNew()
    $appProcessId=''
    while($timer.Elapsed.TotalSeconds -lt 90) {
        if(!$appProcessId) { $appProcessId=(& $Adb -s $Serial shell pidof $package | Out-String).Trim() }
        if($appProcessId) {
            $logText=(& $Adb -s $Serial logcat -d -v threadtime "--pid=$appProcessId" -s Unity:V | Out-String)
            foreach($match in [regex]::Matches($logText,'QUEST_SANDBOX_SMOKE CAPTURE (Victory-[A-Za-z0-9-]+)')) {
                $stage=$match.Groups[1].Value
                if($captures.ContainsKey($stage)) { continue }
                $file=Join-Path $outputPath ($stage+'.png')
                & $MetaCli -d $Serial capture screenshot --method screencap --output $file
                if($LASTEXITCODE -ne 0) { throw "Capture failed: $stage" }
                $captures[$stage]=$file
                Write-Output "Captured $stage"
            }
            if($logText -match 'QUEST_SANDBOX_SMOKE FAIL') { throw 'Acceptance failed; see device.log.' }
            if($logText -match 'QUEST_SANDBOX_SMOKE VICTORY_PASS') {
                [pscustomobject]@{ Result='Passed'; Device=$Serial; Package=$package; Captures=$captures; SyntheticSuccess=$true; SyntheticPalmSamples=$true; PhysicalHandsVerified=$false } |
                    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $outputPath 'result.json') -Encoding UTF8
                Write-Output ($logText -split "`n" | Where-Object { $_ -match 'QUEST_SANDBOX_SMOKE' })
                exit 0
            }
        }
        Start-Sleep -Milliseconds 700
    }
    throw 'Acceptance timed out.'
}
finally {
    $logText | Set-Content -LiteralPath (Join-Path $outputPath 'device.log') -Encoding UTF8
    & $MetaCli -d $Serial device proximity --enable
}

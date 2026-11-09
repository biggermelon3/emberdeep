param(
    [string]$Device='',
    [switch]$Launch,
    [string]$Adb='C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe'
)
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$apk=Join-Path $projectRoot 'Builds/MetaHackthone.apk'
if(!(Test-Path -LiteralPath $apk)) { throw "Build the APK first: $apk" }
[string[]]$deviceArguments=@('-d')
if($Device) { $deviceArguments=@('-s',$Device) }
& $Adb @deviceArguments install -r $apk
if($LASTEXITCODE -ne 0) { throw 'APK installation failed.' }
if($Launch) {
    & $Adb @deviceArguments shell monkey -p com.metahackathon2026.metahackthone -c android.intent.category.LAUNCHER 1
    if($LASTEXITCODE -ne 0) { throw 'Application launch failed.' }
}

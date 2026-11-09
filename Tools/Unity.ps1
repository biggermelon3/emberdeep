param(
    [ValidateSet('Setup','EditTests','PlayTests','Build','BuildClean','Preview','Prefabs')][string]$Task='Setup',
    [string]$TestFilter='',
    [string]$UnityEditor='G:\UnityProject\Unity\6000.3.21f1\Editor\Unity.exe'
)
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$logDirectory=Join-Path $projectRoot 'Logs'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$arguments=@('-batchmode','-nographics','-projectPath',('"'+$projectRoot+'"'),'-logFile',('"'+(Join-Path $logDirectory ($Task+'.log'))+'"'))
if($Task -eq 'Preview') { $arguments=$arguments | Where-Object { $_ -ne '-nographics' } }
switch($Task) {
    'Prefabs' { $arguments+=@('-quit','-executeMethod','PhysicalAssembly.Editor.ProjectBuilder.RebuildPartPrefabs') }
    'Setup' { $arguments+=@('-quit','-executeMethod','PhysicalAssembly.Editor.ProjectBuilder.Setup') }
    'Build' { $arguments+=@('-quit','-buildTarget','Android','-executeMethod','PhysicalAssembly.Editor.ProjectBuilder.BuildQuest') }
    'BuildClean' { $arguments+=@('-quit','-buildTarget','Android','-executeMethod','PhysicalAssembly.Editor.ProjectBuilder.BuildQuestClean') }
    'EditTests' { $arguments+=@('-runTests','-testPlatform','EditMode','-testResults',('"'+(Join-Path $logDirectory 'EditTests.xml')+'"')) }
    'PlayTests' { $arguments+=@('-runTests','-testPlatform','PlayMode','-testResults',('"'+(Join-Path $logDirectory 'PlayTests.xml')+'"')) }
    'Preview' { $arguments+=@('-executeMethod','PhysicalAssembly.Editor.PreviewCapture.Run') }
}
if($TestFilter) { $arguments+=@('-testFilter',$TestFilter) }
$process=Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
# Wait for the Editor itself, not long-lived SDK/ADB descendant processes.
$process.WaitForExit()
if($process.ExitCode -eq 0 -and $Task -eq 'BuildClean') {
    & (Join-Path $PSScriptRoot 'Validate-AndroidShaders.ps1') -LogPath (Join-Path $logDirectory 'BuildClean.log')
}
Write-Output "$Task finished with exit code $($process.ExitCode). Log: $logDirectory\$Task.log"
exit $process.ExitCode

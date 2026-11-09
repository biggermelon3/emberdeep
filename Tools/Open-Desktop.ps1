param(
    [switch]$Play,
    [string]$UnityEditor='C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe'
)
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$editor=[IO.Path]::GetFullPath($UnityEditor)
if(!(Test-Path $editor)) { throw "Unity Editor not installed: $editor" }
& (Join-Path $PSScriptRoot 'Start-Mcp.ps1')
$arguments=@('-projectPath',('"'+$projectRoot+'"'),'-buildTarget','Win64','-workshopMcp','-logFile',('"'+(Join-Path $projectRoot 'Logs\Desktop.log')+'"'))
if($Play) { $arguments+=@('-executeMethod','PhysicalAssembly.Editor.DesktopWorkshop.Play') }
else { $arguments+=@('-executeMethod','PhysicalAssembly.Editor.DesktopWorkshop.Open') }
Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden

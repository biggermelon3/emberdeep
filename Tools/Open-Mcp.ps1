param(
    [string]$UnityEditor='C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe',
    [ValidateRange(1,600)][int]$TimeoutSeconds=180
)
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectName=Split-Path $projectRoot -Leaf
$sha=[Security.Cryptography.SHA1]::Create()
try { $projectHash=([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($projectRoot.Replace('\','/')+'/Assets')))).Replace('-','').ToLowerInvariant().Substring(0,16) }
finally { $sha.Dispose() }
$log=Join-Path $projectRoot 'Logs\McpEditor.log'
function Get-ConnectedEditor {
    try {
        $result=Invoke-RestMethod 'http://127.0.0.1:8080/api/instances' -TimeoutSec 2
        return @($result.instances | Where-Object { $_.project -eq $projectName -and $_.hash -eq $projectHash })
    } catch { return @() }
}
if(!(Test-Path -LiteralPath $UnityEditor)) { throw "Unity Editor not installed: $UnityEditor" }
& (Join-Path $PSScriptRoot 'Start-Mcp.ps1')
if(@(Get-ConnectedEditor).Count -gt 0) { Write-Output "Unity editor $projectName is already connected."; return }
$lockPath=Join-Path $projectRoot 'Temp\UnityLockfile'
$locked=$false
if(Test-Path -LiteralPath $lockPath) {
    try { $handle=[IO.File]::Open($lockPath,'Open','ReadWrite','None'); $handle.Dispose() }
    catch [IO.IOException] { $locked=$true }
}
$editorProcess=$null
if(!$locked) {
    # Preserve selected platform and scene by omitting buildTarget and executeMethod.
    $arguments=@('-projectPath',('"'+$projectRoot+'"'),'-workshopMcp','-logFile',('"'+$log+'"'))
    try { $editorProcess=Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru }
    catch { throw "Unity editor process launch failed or denied: $($_.Exception.Message)" }
}
else { Write-Output 'Project is already open; waiting for its MCP connection.' }
$deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
while([DateTime]::UtcNow -lt $deadline) {
    $instances=@(Get-ConnectedEditor)
    if($instances.Count -gt 0) { Write-Output "Unity editor connected: $($instances[0].project)@$($instances[0].hash)"; return }
    if($editorProcess) {
        $editorProcess.Refresh()
        if($editorProcess.HasExited) { throw "Unity Editor exited with code $($editorProcess.ExitCode) before connecting. See $log" }
    }
    Start-Sleep -Seconds 1
}
throw "Unity MCP server responds, but $projectName did not connect within $TimeoutSeconds seconds. In Unity choose MetaHackthone > Connect Unity MCP. Check $log for compilation or licensing errors."

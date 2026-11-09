param([ValidateRange(1,300)][int]$TimeoutSeconds=60)
$ErrorActionPreference='Stop'
$unityRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$server=Join-Path $unityRoot 'Tools\unity-mcp\Server\.venv\Scripts\mcp-for-unity.exe'
$logs=Join-Path $PSScriptRoot '..\Logs'
New-Item -ItemType Directory -Path $logs -Force | Out-Null
try {
    $health=Invoke-RestMethod 'http://127.0.0.1:8080/health' -TimeoutSec 2
    if($health.status -eq 'healthy' -and $health.version) {
        Write-Output 'Unity MCP server is responding; editor connection is checked separately.'
        return
    }
} catch {}
if(!(Test-Path $server)) { throw "Unity MCP server not installed: $server" }
# A listener or a process still starting must not cause a duplicate launch.
$existing=@(Get-Process -Name 'mcp-for-unity' -ErrorAction SilentlyContinue)
$probe=[Net.Sockets.TcpClient]::new()
try { $listener=$probe.ConnectAsync('127.0.0.1',8080).Wait(1000) -and $probe.Connected }
catch { $listener=$false }
finally { $probe.Dispose() }
if($listener -and $existing.Count -eq 0) { throw 'Port 8080 is occupied by an unhealthy or unknown server; inspect its owner before retrying.' }
$env:UNITY_MCP_LOG_DIR=[IO.Path]::GetFullPath($logs)
$serverProcess=$null
if($existing.Count -eq 0) {
    try { $serverProcess=Start-Process -FilePath $server -WorkingDirectory (Split-Path $server -Parent) -ArgumentList @('--transport','http','--http-url','http://127.0.0.1:8080') -WindowStyle Hidden -RedirectStandardOutput (Join-Path $logs 'Mcp.stdout.log') -RedirectStandardError (Join-Path $logs 'Mcp.stderr.log') -PassThru }
    catch { throw "Unity server process launch failed or denied: $($_.Exception.Message)" }
}
$deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
while([DateTime]::UtcNow -lt $deadline) {
    try {
        $health=Invoke-RestMethod 'http://127.0.0.1:8080/health' -TimeoutSec 2
        if($health.status -eq 'healthy' -and $health.version) { Write-Output 'Unity MCP server is responding; editor connection is checked separately.'; return }
    } catch {}
    if($serverProcess) {
        $serverProcess.Refresh()
        if($serverProcess.HasExited) { throw "Unity MCP exited with code $($serverProcess.ExitCode). See $logs\Mcp.stderr.log" }
    }
    Start-Sleep -Seconds 1
}
throw "Unity MCP did not become ready. See $logs\Mcp.stderr.log"

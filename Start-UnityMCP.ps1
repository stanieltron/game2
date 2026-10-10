$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$server = Join-Path $root '.setup/unity-mcp/Server/.venv/Scripts/mcp-for-unity.exe'
if (-not (Test-Path $server)) { throw 'Run uv sync --project .setup/unity-mcp/Server first.' }
if (-not (Get-NetTCPConnection -LocalPort 8080 -State Listen -ErrorAction SilentlyContinue)) {
    Start-Process -FilePath $server -ArgumentList '--transport http --http-host 127.0.0.1 --http-port 8080' -WindowStyle Hidden
}
Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe' -ArgumentList "-projectPath `"$root`""

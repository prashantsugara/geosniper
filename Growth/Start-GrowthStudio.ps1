$ErrorActionPreference = 'Stop'
$studioDirectory = $PSScriptRoot
$nodeCommand = (Get-Command node -ErrorAction Stop).Source
$studioUrl = 'http://127.0.0.1:4318'
try {
    $running = Invoke-RestMethod -Uri "$studioUrl/api/state" -TimeoutSec 2
    if (-not $running.settings.brand) { throw 'Port 4318 is occupied by a different application.' }
} catch {
    $studioData = Join-Path $studioDirectory 'data'
    New-Item -ItemType Directory -Path $studioData -Force | Out-Null
    Start-Process -FilePath $nodeCommand -ArgumentList 'server.mjs' -WorkingDirectory $studioDirectory -WindowStyle Hidden -RedirectStandardOutput (Join-Path $studioData 'server.log') -RedirectStandardError (Join-Path $studioData 'server-error.log')
    Start-Sleep -Seconds 2
}
Start-Process $studioUrl
Write-Host "Growth Studio: $studioUrl"
Write-Host 'The local worker runs while this computer is awake. No account is connected automatically.'

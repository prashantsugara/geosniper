param([switch]$Compact, [switch]$AllPages)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root '.utmp/review-unity'
if (Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -like "*$project*" }) {
    throw 'The isolated review Unity is still running. Wait for it to finish before capturing.'
}
if (!(Test-Path "$project/Assets/Plugins/ReviewRuntime.dll")) { throw 'Run run_review_checks.ps1 first and wait for Unity to exit.' }
Copy-Item "$root/Assets/Editor/LobbyPreviewCapture.cs" "$project/Assets/Editor/" -Force
$manifestPath = "$project/Packages/manifest.json"
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$manifest.dependencies | Add-Member -NotePropertyName 'com.unity.modules.screencapture' -NotePropertyValue '1.0.0' -Force
$manifest | ConvertTo-Json -Depth 5 | Set-Content $manifestPath
$unity = 'C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Unity.exe'
$method = if ($Compact) { 'LobbyPreviewCapture.RunCompact' } else { 'LobbyPreviewCapture.Run' }
if ($AllPages) { $method = if ($Compact) { 'LobbyPreviewCapture.RunAllCompact' } else { 'LobbyPreviewCapture.RunAll' } }
$process = Start-Process -FilePath $unity -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode', '-projectPath', ('"' + $project + '"'), '-executeMethod', $method, '-logFile', ('"' + $project + '/lobby-capture.log"'))
"Lobby capture Unity PID: $($process.Id)"

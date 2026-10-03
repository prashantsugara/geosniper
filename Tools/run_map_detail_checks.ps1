$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location $root
& "$PSScriptRoot/check_code.ps1"
$project=Join-Path $root '.utmp/review-unity'
if (!(Test-Path "$project/Packages/manifest.json")) {throw 'Run Tools/run_review_checks.ps1 once to prepare the isolated Unity project.'}
Copy-Item '.utmp/review-checks/ReviewRuntime.dll' "$project/Assets/Plugins/ReviewRuntime.dll" -Force
Copy-Item 'Assets/Editor/MapDetailChecks.cs' "$project/Assets/Editor/MapDetailChecks.cs" -Force
$unity='C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Unity.exe'
$process=Start-Process -FilePath $unity -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode','-projectPath',('"'+$project+'"'),'-executeMethod','MapDetailChecks.Run','-quit','-logFile',('"'+$project+'/map-details.log"'))
"Map detail Unity PID: $($process.Id)"
"Report: $project/Logs/MapDetailChecks.txt"
"Preview: $project/Logs/MapDetailPreview.png"

$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$project=Join-Path $root '.utmp/review-unity'
if(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -like "*$project*" }) {throw 'Wait for the isolated review Unity to finish first.'}
if(!(Test-Path "$project/Assets/Plugins/ReviewRuntime.dll")) {throw 'Run run_review_checks.ps1 first.'}
Copy-Item "$root/Assets/Editor/CampaignGameplayChecks.cs" "$project/Assets/Editor/" -Force
$unity='C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Unity.exe'
$process=Start-Process -FilePath $unity -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode','-projectPath',('"'+$project+'"'),'-executeMethod','CampaignGameplayChecks.Run','-logFile',('"'+$project+'/campaign-play.log"'))
"Campaign Unity PID: $($process.Id)"
"Log: $project/campaign-play.log"

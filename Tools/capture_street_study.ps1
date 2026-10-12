param([switch]$RealStreet,[switch]$TrainingStreet)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location $root
$project=Join-Path $root '.utmp/review-unity'
if(!(Test-Path "$project/Assets/Plugins")){throw 'Prepare the isolated project with run_review_checks.ps1 first.'}
$busy=Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {$_.CommandLine -like '*review-unity*'}
if($busy){throw 'The isolated review project is already running.'}
& "$PSScriptRoot/check_code.ps1"
Copy-Item '.utmp/review-checks/ReviewRuntime.dll' "$project/Assets/Plugins/ReviewRuntime.dll" -Force
Copy-Item 'Assets/Editor/StreetStudyCapture.cs' "$project/Assets/Editor/" -Force
foreach($name in @('AtmosphericSky','MobilePostProcess','RealisticBuilding','StreetStudySurface','StreetStudyShopGlass')) {
    Copy-Item "Assets/Resources/Shaders/$name.shader" "$project/Assets/Resources/Shaders/" -Force
}
if($RealStreet -and $TrainingStreet){throw 'Choose cached real map or offline training, not both.'}
$captureMethod=if($RealStreet){'StreetStudyCapture.RunReal'}elseif($TrainingStreet){'StreetStudyCapture.RunTraining'}else{'StreetStudyCapture.Run'}
$process=Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Unity.exe' -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode','-force-d3d11','-projectPath',('"'+$project+'"'),'-executeMethod',$captureMethod,'-logFile',('"'+$project+'/street-study.log"'))
"Street study Unity PID: $($process.Id)"
"Capture and checks: $project/Logs/StreetStudyGameplay.png and StreetStudy.txt"

param([string]$CacheFile)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location $root
$project=Join-Path $root '.utmp/review-unity'
if(!(Test-Path "$project/Assets/Plugins")){throw 'Prepare the isolated review project with run_review_checks.ps1 first.'}
$busy=Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {$_.CommandLine -like '*review-unity*'}
if($busy){throw 'The isolated review project is already running.'}
& "$PSScriptRoot/check_code.ps1"
Copy-Item '.utmp/review-checks/ReviewRuntime.dll' "$project/Assets/Plugins/ReviewRuntime.dll" -Force
Copy-Item 'Assets/Editor/RealSectorPreview.cs' "$project/Assets/Editor/" -Force
Copy-Item 'Assets/Resources/Shaders/*' "$project/Assets/Resources/Shaders/" -Force
Copy-Item 'Assets/Resources/AssetCalibration.json' "$project/Assets/Resources/" -Force
if(!$CacheFile){$CacheFile=(Get-ChildItem "$env:USERPROFILE/AppData/LocalLow/Bittruth/Geo Sniper/overture-direct-sectors-v1" -File | Sort-Object LastWriteTime -Descending | Where-Object {((Get-Content $_.FullName -Raw | ConvertFrom-Json).styleVersion) -eq 5} | Select-Object -First 1).FullName}
if(!$CacheFile){throw 'No current cached sector available.'}
Copy-Item -LiteralPath $CacheFile -Destination "$project/real-sector.json" -Force
'PENDING' | Set-Content "$project/Logs/real-sector-result.txt"
$process=Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Unity.exe' -WindowStyle Hidden -PassThru -Wait -ArgumentList @('-batchmode','-projectPath',('"'+$project+'"'),'-executeMethod','RealSectorPreview.Run','-logFile',('"'+$project+'/real-sector.log"'))
Get-Content "$project/Logs/real-sector-result.txt"

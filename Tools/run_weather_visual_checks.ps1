$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$project=Join-Path $root '.utmp/weather-review'
foreach($dir in @('Assets/Editor','Assets/Resources/Shaders','Packages','ProjectSettings','Logs')) {
    New-Item -ItemType Directory -Force (Join-Path $project $dir) | Out-Null
}
Copy-Item (Join-Path $root 'Assets/Scripts/WorldRainSystem.cs') "$project/Assets/WorldRainSystem.cs" -Force
Copy-Item (Join-Path $root 'Assets/Resources/Shaders/WorldRain.shader') "$project/Assets/Resources/Shaders/WorldRain.shader" -Force
Copy-Item (Join-Path $root 'Tests/WeatherVisualChecks/WeatherReview.cs') "$project/Assets/Editor/WeatherReview.cs" -Force
Copy-Item (Join-Path $root 'Tests/WeatherVisualChecks/Stubs.cs') "$project/Assets/Stubs.cs" -Force
Copy-Item (Join-Path $root 'ProjectSettings/ProjectVersion.txt') "$project/ProjectSettings/ProjectVersion.txt" -Force
$dependencies=@{}
foreach($module in @('audio','particlesystem','physics','imageconversion')) {$dependencies['com.unity.modules.'+$module]='1.0.0'}
@{dependencies=$dependencies}|ConvertTo-Json -Depth 4|Set-Content "$project/Packages/manifest.json"
'PENDING' | Set-Content "$project/Logs/result.txt"
$process=Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Unity.exe' -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode','-projectPath',('"'+$project+'"'),'-executeMethod','WeatherReview.Run','-logFile',('"'+$project+'/review.log"'))
"Weather review PID: $($process.Id). Results and screenshots: $project/Logs"

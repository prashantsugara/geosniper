$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$project=Join-Path $root '.utmp/enemy-cover-review'
foreach($dir in @('Assets/Editor','Packages','ProjectSettings','Logs')) {New-Item -ItemType Directory -Force (Join-Path $project $dir)|Out-Null}
Copy-Item "$root/Assets/Scripts/EnemyCoverGeometry.cs" "$project/Assets/EnemyCoverGeometry.cs" -Force
Copy-Item "$root/Tests/EnemyCoverChecks/CombatActor.cs" "$project/Assets/CombatActor.cs" -Force
Copy-Item "$root/Tests/EnemyCoverChecks/EnemyCoverReview.cs" "$project/Assets/Editor/EnemyCoverReview.cs" -Force
Copy-Item "$root/ProjectSettings/ProjectVersion.txt" "$project/ProjectSettings/ProjectVersion.txt" -Force
'{"dependencies":{"com.unity.modules.physics":"1.0.0"}}'|Set-Content "$project/Packages/manifest.json"
'PENDING'|Set-Content "$project/Logs/result.txt"
$process=Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Unity.exe' -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode','-projectPath',('"'+$project+'"'),'-executeMethod','EnemyCoverReview.Run','-logFile',('"'+$project+'/review.log"'))
"Cover review PID: $($process.Id). Result: $project/Logs/result.txt"

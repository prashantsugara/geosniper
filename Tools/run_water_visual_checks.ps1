$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$project=Join-Path $root '.utmp/water-review'
foreach($dir in @('Assets/Editor','Assets/Resources/Shaders','Packages','ProjectSettings','Logs')) {
    New-Item -ItemType Directory -Force (Join-Path $project $dir) | Out-Null
}
foreach($file in @('WaterGeometry.cs','MapData.cs','MapFeatureStyle.cs','SectorWorld.Water.cs','SectorScenery.cs','SceneryDistanceCull.cs','MappedSurfaceTextures.cs','TrafficVehicle.cs','TrafficBrakeLights.cs','TrafficRoadRoutes.cs','RoadJunctionMask.cs','StreetSurfaceDetails.cs','StreetPlacement.cs','ImportedVisual.cs','AssetCalibration.cs')) { Copy-Item (Join-Path $root ('Assets/Scripts/'+$file)) "$project/Assets/$file" -Force }
Copy-Item (Join-Path $root 'Assets/Resources/Shaders/MappedWater.shader') "$project/Assets/Resources/Shaders/MappedWater.shader" -Force
Copy-Item (Join-Path $root 'Assets/Resources/Shaders/WaterFoam.shader') "$project/Assets/Resources/Shaders/WaterFoam.shader" -Force
Copy-Item (Join-Path $root 'Assets/Resources/Shaders/TerrainBlend.shader') "$project/Assets/Resources/Shaders/TerrainBlend.shader" -Force
Copy-Item (Join-Path $root 'Tests/WaterVisualChecks/WaterReview.cs') "$project/Assets/Editor/WaterReview.cs" -Force
Copy-Item (Join-Path $root 'Tests/WaterVisualChecks/Stubs.cs') "$project/Assets/Stubs.cs" -Force
New-Item -ItemType Directory -Force "$project/Assets/Resources/Models/Cars/Textures" | Out-Null
foreach($car in @('audi','audi.001','Ford')) {
    Copy-Item "$root/Assets/Resources/Models/Cars/$car.fbx" "$project/Assets/Resources/Models/Cars/" -Force
    Copy-Item "$root/Assets/Resources/Models/Cars/$car.fbx.meta" "$project/Assets/Resources/Models/Cars/" -Force
}
Copy-Item "$root/Assets/Resources/Models/PoliceCar.fbx" "$project/Assets/Resources/Models/" -Force
Copy-Item "$root/Assets/Resources/Models/PoliceCar.fbx.meta" "$project/Assets/Resources/Models/" -Force
Copy-Item "$root/Assets/Resources/Models/Cars/Textures/cars_0.png" "$project/Assets/Resources/Models/Cars/Textures/" -Force
Copy-Item "$root/Assets/Resources/AssetCalibration.json" "$project/Assets/Resources/" -Force
Copy-Item (Join-Path $root 'ProjectSettings/ProjectVersion.txt') "$project/ProjectSettings/ProjectVersion.txt" -Force
$jsonDll=Get-ChildItem (Join-Path $root 'Library/PackageCache/com.unity.nuget.newtonsoft-json*/Runtime/Newtonsoft.Json.dll') | Select-Object -First 1
Copy-Item $jsonDll.FullName "$project/Assets/Newtonsoft.Json.dll" -Force
$dependencies=@{}
foreach($module in @('audio','particlesystem','physics','imageconversion','animation','jsonserialize')) {$dependencies['com.unity.modules.'+$module]='1.0.0'}
@{dependencies=$dependencies}|ConvertTo-Json -Depth 4|Set-Content "$project/Packages/manifest.json"
'PENDING' | Set-Content "$project/Logs/result.txt"
$process=Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Unity.exe' -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode','-projectPath',('"'+$project+'"'),'-executeMethod','WaterReview.Run','-logFile',('"'+$project+'/review.log"'))
"Water review PID: $($process.Id). Results and screenshots: $project/Logs"


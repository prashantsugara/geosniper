param([string]$ExecuteMethod='DuelVisualChecks.RunAll')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location $root
& "$PSScriptRoot/check_code.ps1"
$project=Join-Path $root '.utmp/duel-unity'
foreach($folder in @('Assets/Plugins','Assets/Editor','Assets/Resources/Models','Packages','ProjectSettings')) {
    New-Item -ItemType Directory -Force (Join-Path $project $folder) | Out-Null
}
Copy-Item '.utmp/review-checks/ReviewRuntime.dll' "$project/Assets/Plugins/ReviewRuntime.dll" -Force
Copy-Item 'Library/ScriptAssemblies/Unity.AI.Navigation.dll','Library/ScriptAssemblies/UnityEngine.UI.dll','Library/PackageCache/com.unity.nuget.newtonsoft-json@4dfd81071c64/Runtime/Newtonsoft.Json.dll' "$project/Assets/Plugins/" -Force
Get-ChildItem 'Library/PackageCache/com.google.ads.mobile@0e7fbe087950/GoogleMobileAds' -Filter '*.dll' | Copy-Item -Destination "$project/Assets/Plugins/" -Force
Get-ChildItem 'Library/ScriptAssemblies' -Filter 'Unity.Services.*.dll' | Where-Object {$_.Name -notlike '*Editor*'} | Copy-Item -Destination "$project/Assets/Plugins/" -Force
Copy-Item 'Library/ScriptAssemblies/Firebase.App.Internal.dll' "$project/Assets/Plugins/" -Force
Get-ChildItem 'Library/PackageCache/com.google.firebase.app@82cfbb10525b/Firebase/Plugins','Library/PackageCache/com.google.firebase.analytics@37191eea6760/Firebase/Plugins' -Filter '*.dll' | Copy-Item -Destination "$project/Assets/Plugins/" -Force
Copy-Item 'Assets/Editor/DuelVisualChecks.cs','Assets/Editor/DuelGameplayChecks.cs','Assets/Editor/DuelKillReplayChecks.cs','Assets/Editor/MobileModelImport.cs','Assets/Scripts/Editor/MixamoEnemyPostprocessor.cs' "$project/Assets/Editor/" -Force
if($ExecuteMethod -eq 'CampaignEnemyReactionChecks.RunAll') {
    Copy-Item 'Assets/Editor/CampaignEnemyReactionChecks.cs','Assets/Editor/CampaignAnimationChecks.cs' "$project/Assets/Editor/" -Force
}
if($ExecuteMethod -eq 'PresentationRegressionChecks.RunAll') {
    Copy-Item 'Assets/Editor/PresentationRegressionChecks.cs','Assets/Editor/AdFlowChecks.cs','Assets/Editor/DaylightRoofChecks.cs' "$project/Assets/Editor/" -Force
}
if($ExecuteMethod -eq 'MultiplayerPolishChecks.RunAll' -or $ExecuteMethod -eq 'WeaponExpansionChecks.Run') {
    Copy-Item 'Assets/Editor/WeaponExpansionChecks.cs','Assets/Editor/WeaponEconomyChecks.cs','Assets/Editor/MultiplayerPolishChecks.cs','Assets/Editor/MobileControlLayoutChecks.cs','Assets/Editor/HudLayoutChecks.cs','Assets/Editor/DuelRoofAccessChecks.cs' "$project/Assets/Editor/" -Force
}
if($ExecuteMethod -eq 'CampaignProgressionChecks.Rewards') {
    Copy-Item 'Assets/Editor/CampaignProgressionChecks.cs' "$project/Assets/Editor/" -Force
}
foreach($folder in @('Weapons','FPSArms','Enemies')) {
    & robocopy "Assets/Resources/Models/$folder" "$project/Assets/Resources/Models/$folder" /E /NFL /NDL /NJH /NJS /NP
    if($LASTEXITCODE -gt 7) {throw "Asset copy failed: $folder"}
}
Copy-Item 'Assets/Resources/AssetCalibration.json','Assets/Resources/AssetCalibration.json.meta' "$project/Assets/Resources/" -Force
New-Item -ItemType Directory -Force "$project/Assets/Resources/Shaders" | Out-Null
Copy-Item 'Assets/Resources/Shaders/MobilePostProcess.shader','Assets/Resources/Shaders/MobilePostProcess.shader.meta','Assets/Resources/Shaders/ThermalSignature.shader','Assets/Resources/Shaders/ThermalSignature.shader.meta' "$project/Assets/Resources/Shaders/" -Force
Copy-Item 'ProjectSettings/ProjectVersion.txt' "$project/ProjectSettings/" -Force
$dependencies=@{}
foreach($module in @('ai','physics','animation','audio','imgui','particlesystem','ui','unitywebrequest','unitywebrequestaudio','androidjni','imageconversion')) {
    $dependencies["com.unity.modules.$module"]='1.0.0'
}
[IO.File]::WriteAllText("$project/Packages/manifest.json",(@{dependencies=$dependencies}|ConvertTo-Json -Depth 4))
$unity='C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Unity.exe'
$process=Start-Process -FilePath $unity -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode','-force-d3d11','-job-worker-count','2','-projectPath',('"'+$project+'"'),'-executeMethod',$ExecuteMethod,'-quit','-logFile',('"'+$project+'/duel-visual.log"'))
"Duel verification Unity PID: $($process.Id)"
"Report: $project/Logs/DuelVisualChecks.txt"

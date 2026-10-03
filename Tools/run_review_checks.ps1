$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location $root
& "$PSScriptRoot/check_code.ps1"
$project=Join-Path $root '.utmp/review-unity'
foreach($folder in @('Assets/Plugins','Assets/Editor','Packages','ProjectSettings')) {New-Item -ItemType Directory -Force (Join-Path $project $folder)|Out-Null}
Copy-Item '.utmp/review-checks/ReviewRuntime.dll' "$project/Assets/Plugins/ReviewRuntime.dll" -Force
Copy-Item 'Library/ScriptAssemblies/Unity.AI.Navigation.dll' "$project/Assets/Plugins/" -Force
Copy-Item 'Library/ScriptAssemblies/UnityEngine.UI.dll' "$project/Assets/Plugins/" -Force
Copy-Item 'Library/PackageCache/com.unity.nuget.newtonsoft-json@4dfd81071c64/Runtime/Newtonsoft.Json.dll' "$project/Assets/Plugins/" -Force
Get-ChildItem 'Library/PackageCache/com.google.ads.mobile@0e7fbe087950/GoogleMobileAds' -Filter '*.dll' | Copy-Item -Destination "$project/Assets/Plugins/" -Force
Copy-Item 'Assets/Editor/ReviewRegressionChecks.cs','Assets/Editor/CampaignProgressionChecks.cs','Assets/Editor/GameplayGeometryAudit.cs','Assets/Editor/CombatHitChecks.cs','Assets/Editor/MobileModelImport.cs' "$project/Assets/Editor/" -Force
Copy-Item 'Assets/Editor/AdFlowChecks.cs' "$project/Assets/Editor/" -Force
Copy-Item 'Assets/Editor/GameplayFixChecks.cs','Assets/Editor/ArmyAnimationChecks.cs','Assets/Editor/WeaponPoseChecks.cs','Assets/Scripts/Editor/MixamoEnemyPostprocessor.cs' "$project/Assets/Editor/" -Force
Copy-Item 'Assets/Editor/MapDetailChecks.cs' "$project/Assets/Editor/" -Force
Copy-Item 'Assets/Editor/OverturePriorityChecks.cs' "$project/Assets/Editor/" -Force
Copy-Item 'Assets/Editor/OvertureDetailChecks.cs','Assets/Editor/MapWorldChecks.cs','Assets/Editor/MapLibreGradleFix.cs' "$project/Assets/Editor/" -Force
Copy-Item 'Assets/Editor/SwatMaterialChecks.cs' "$project/Assets/Editor/" -Force
& robocopy 'Assets/Resources' "$project/Assets/Resources" /E /NFL /NDL /NJH /NJS /NP
if($LASTEXITCODE -gt 7) {throw 'Asset copy failed'}
Copy-Item 'ProjectSettings/ProjectVersion.txt' "$project/ProjectSettings/" -Force
$modules=@('ai','animation','audio','imgui','physics','imageconversion','screencapture','unitywebrequest','unitywebrequestaudio','androidjni','particlesystem','ui')
$dependencies=@{}
foreach($module in $modules){$dependencies["com.unity.modules.$module"]='1.0.0'}
$dependencies['com.google.firebase.app']='file:../GooglePackages/com.google.firebase.app-13.17.0.tgz'
$dependencies['com.google.firebase.analytics']='file:../GooglePackages/com.google.firebase.analytics-13.17.0.tgz'
$dependencies['com.google.ads.mobile']='11.5.0'
$dependencies['com.unity.services.analytics']='6.3.0'
$dependencies['com.unity.services.core']='1.18.0'
$dependencies['com.unity.ugui']='2.5.0'
$dependencies['com.unity.nuget.newtonsoft-json']='3.2.2'
$registry=@{name='package.openupm.com';url='https://package.openupm.com';scopes=@('com.google.ads.mobile','com.google.external-dependency-manager')}
[IO.File]::WriteAllText("$project/Packages/manifest.json",(@{dependencies=$dependencies;scopedRegistries=@($registry)}|ConvertTo-Json -Depth 8))
$unity='C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Unity.exe'
$process=Start-Process -FilePath $unity -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode','-projectPath',('"'+$project+'"'),'-executeMethod','ReviewRegressionChecks.Run','-quit','-logFile',('"'+$project+'/review.log"'))
"Review Unity PID: $($process.Id)"
"Log: $project/review.log"
